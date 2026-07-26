using System.Collections.Generic;
using UnityEngine;

public class PlayerRuntimeStatApplier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerWeaponModifiers weaponModifiers;
    [SerializeField] private PlayerRuntimeBonusState runtimeBonusState;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private PlayerShipVisualController shipVisualController;

    [Header("Base Stats")]
    [SerializeField] private float baseMaxHp = 20f;
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float baseDashDistance = 5f;
    [SerializeField] private float baseDashCooldown = 1.1f;
    [SerializeField] private int baseCargoCapacity = 100;
    [Range(0f, 1f)]
    [SerializeField] private float baseEmergencyReturnCapacityRatio = 0.7f;

    [Header("Cargo Weight")]
    [SerializeField] private int scrapCargoWeight = 1;
    [SerializeField] private int coreShardCargoWeight = 12;

    [Header("Trait Apply Rule")]
    [SerializeField] private bool applyTraitEffectsCumulatively;

    [Header("Building Fallback")]
    [SerializeField] private bool useFallbackBuildingEffects = true;

    [Header("Debug")]
    [SerializeField] private bool logApplyResult = true;

    private RuntimeStats runtimeStats;

    private struct RuntimeStats
    {
        public float maxHp;
        public float moveSpeed;
        public float dashDistance;
        public float dashCooldown;
        public int cargoCapacity;
        public float emergencyReturnCapacityRatio;

        public RuntimeStats(
            float baseMaxHp,
            float baseMoveSpeed,
            float baseDashDistance,
            float baseDashCooldown,
            int baseCargoCapacity,
            float baseEmergencyReturnCapacityRatio)
        {
            maxHp = Mathf.Max(1f, baseMaxHp);
            moveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
            dashDistance = Mathf.Max(0.1f, baseDashDistance);
            dashCooldown = Mathf.Max(0.05f, baseDashCooldown);
            cargoCapacity = Mathf.Max(1, baseCargoCapacity);
            emergencyReturnCapacityRatio = Mathf.Clamp01(baseEmergencyReturnCapacityRatio);
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    public void Apply(
        RunContext runContext,
        PermanentProgress progress,
        IReadOnlyList<ShipDefinition> shipDefinitions,
        IReadOnlyList<BuildingDefinition> buildingDefinitions,
        IReadOnlyList<TraitDefinition> traitDefinitions,
        bool refillHealth)
    {
        CacheReferences();

        WeaponTreeType selectedWeaponTree = ResolveSelectedWeaponTree(runContext, progress);
        string selectedShipId = ResolveSelectedShipId(runContext, progress);

        runtimeStats = new RuntimeStats(
            baseMaxHp,
            baseMoveSpeed,
            baseDashDistance,
            baseDashCooldown,
            baseCargoCapacity,
            baseEmergencyReturnCapacityRatio
        );

        ResetRuntimeModifiers();

        ShipDefinition selectedShip = FindShipDefinition(shipDefinitions, selectedShipId);

        if (shipVisualController != null)
        {
            shipVisualController.SetShipDefinition(selectedShip, false);
        }

        ApplyShip(selectedShip);

        ApplyBuildings(progress, buildingDefinitions);
        ApplyPermanentTraits(progress, traitDefinitions, selectedWeaponTree);
        ApplyRunTraits(runContext, traitDefinitions, selectedWeaponTree);

        CommitStats(refillHealth, runContext);

        if (weaponController != null)
        {
            weaponController.EquipWeapon(selectedWeaponTree);
        }

        if (shipVisualController != null)
        {
            shipVisualController.ApplyVisual(selectedWeaponTree);
        }

        if (logApplyResult)
        {
            Debug.Log(
                $"Runtime Stat Apply 완료 / Weapon: {selectedWeaponTree}, Ship: {selectedShipId}, " +
                $"HP: {runtimeStats.maxHp}, Move: {runtimeStats.moveSpeed:0.##}, " +
                $"DashDistance: {runtimeStats.dashDistance:0.##}, DashCooldown: {runtimeStats.dashCooldown:0.##}, " +
                $"Cargo: {runtimeStats.cargoCapacity}, EmergencyReturnRatio: {runtimeStats.emergencyReturnCapacityRatio:0.##}",
                this
            );
        }
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (weaponModifiers == null)
        {
            weaponModifiers = GetComponent<PlayerWeaponModifiers>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = gameObject.AddComponent<PlayerRuntimeBonusState>();
        }

        if (cargoController == null)
        {
            cargoController = GetComponent<PlayerCargoController>();
        }

        if (cargoController == null)
        {
            cargoController = gameObject.AddComponent<PlayerCargoController>();
        }

        if (shipVisualController == null)
        {
            shipVisualController = GetComponent<PlayerShipVisualController>();
        }

        if (shipVisualController == null)
        {
            shipVisualController = gameObject.AddComponent<PlayerShipVisualController>();
        }
    }

    private void ResetRuntimeModifiers()
    {
        if (weaponModifiers != null)
        {
            weaponModifiers.ResetModifiers();
        }

        if (runtimeBonusState != null)
        {
            runtimeBonusState.ResetBonuses();
        }
    }

    private WeaponTreeType ResolveSelectedWeaponTree(RunContext runContext, PermanentProgress progress)
    {
        if (runContext != null && runContext.IsActive)
        {
            return runContext.SelectedWeaponTree;
        }

        if (progress != null)
        {
            return progress.LastSelectedWeaponTree;
        }

        return WeaponTreeType.MachineGun;
    }

    private string ResolveSelectedShipId(RunContext runContext, PermanentProgress progress)
    {
        if (runContext != null && runContext.IsActive)
        {
            return runContext.SelectedShipId;
        }

        if (progress != null)
        {
            return progress.SelectedShipId;
        }

        return "basic_ship";
    }

    private ShipDefinition FindShipDefinition(IReadOnlyList<ShipDefinition> shipDefinitions, string shipId)
    {
        if (shipDefinitions == null || string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        for (int i = 0; i < shipDefinitions.Count; i++)
        {
            ShipDefinition ship = shipDefinitions[i];
            if (ship != null && ship.ShipId == shipId)
            {
                return ship;
            }
        }

        return null;
    }

    private TraitDefinition FindTraitDefinition(IReadOnlyList<TraitDefinition> traitDefinitions, string traitId)
    {
        if (traitDefinitions == null || string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            TraitDefinition trait = traitDefinitions[i];
            if (trait != null && trait.TraitId == traitId)
            {
                return trait;
            }
        }

        return null;
    }

    private BuildingDefinition FindBuildingDefinition(
        IReadOnlyList<BuildingDefinition> buildingDefinitions,
        BuildingType buildingType)
    {
        if (buildingDefinitions == null)
        {
            return null;
        }

        for (int i = 0; i < buildingDefinitions.Count; i++)
        {
            BuildingDefinition definition = buildingDefinitions[i];
            if (definition != null && definition.BuildingType == buildingType)
            {
                return definition;
            }
        }

        return null;
    }

    private void ApplyShip(ShipDefinition ship)
    {
        if (ship == null)
        {
            return;
        }

        runtimeStats.maxHp = Mathf.Max(1f, ship.MaxHp);
        ApplyMoveSpeedPercent(ship.MoveSpeedBonusPercent);
        runtimeStats.dashDistance += ship.DashDistanceBonus;
        runtimeStats.dashCooldown -= Mathf.Abs(ship.DashCooldownReduction);
        runtimeStats.cargoCapacity = ship.CargoCapacity;
        runtimeStats.emergencyReturnCapacityRatio = ship.EmergencyReturnCapacityRatio;

        if (runtimeBonusState != null)
        {
            runtimeBonusState.AddHarvestYieldPercent(ship.HarvestYieldBonusPercent);
            runtimeBonusState.AddHarvestObjectDamagePercent(ship.HarvestObjectDamageBonusPercent);
        }

        ClampStats();
    }

    private void ApplyBuildings(PermanentProgress progress, IReadOnlyList<BuildingDefinition> buildingDefinitions)
    {
        if (progress == null)
        {
            return;
        }

        ApplyBuilding(progress, buildingDefinitions, BuildingType.Hangar);
        ApplyBuilding(progress, buildingDefinitions, BuildingType.EngineWorkshop);
        ApplyBuilding(progress, buildingDefinitions, BuildingType.WeaponLab);
        ApplyBuilding(progress, buildingDefinitions, BuildingType.RecoveryProcessor);

        ClampStats();
    }

    private void ApplyBuilding(PermanentProgress progress, IReadOnlyList<BuildingDefinition> buildingDefinitions, BuildingType buildingType)
    {
        int level = progress.GetBuildingLevel(buildingType);
        if (level <= 0)
        {
            return;
        }

        BuildingDefinition definition = FindBuildingDefinition(buildingDefinitions, buildingType);
        BuildingLevelDefinition levelDefinition = definition != null ? definition.GetLevelDefinition(level) : null;

        bool appliedFromDefinition = ApplyBuildingLevelDefinition(levelDefinition);

        if (!appliedFromDefinition && useFallbackBuildingEffects)
        {
            ApplyFallbackBuildingEffect(buildingType, level);
        }
    }

    private bool ApplyBuildingLevelDefinition(BuildingLevelDefinition levelDefinition)
    {
        if (levelDefinition == null || levelDefinition.Modifiers == null)
        {
            return false;
        }

        bool appliedAny = false;

        foreach (BuildingModifier modifier in levelDefinition.Modifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            ApplyBuildingModifier(modifier.ModifierType, modifier.Value);
            appliedAny = true;
        }

        return appliedAny;
    }

    private void ApplyBuildingModifier(BuildingModifierType modifierType, float value)
    {
        switch (modifierType)
        {
            case BuildingModifierType.MaxHpBonus:
                runtimeStats.maxHp += value;
                break;

            case BuildingModifierType.RepairEfficiencyBonus:
                runtimeBonusState?.AddRepairEfficiencyPercent(value);
                break;

            case BuildingModifierType.MoveSpeedPercent:
                ApplyMoveSpeedPercent(value);
                break;

            case BuildingModifierType.DashDistanceBonus:
                runtimeStats.dashDistance += value;
                break;

            case BuildingModifierType.DashCooldownReduction:
                runtimeStats.dashCooldown -= Mathf.Abs(value);
                break;

            case BuildingModifierType.DamagePercent:
                weaponModifiers?.AddDamagePercent(value);
                break;

            case BuildingModifierType.ProjectileSpeedPercent:
                weaponModifiers?.AddProjectileSpeedPercent(value);
                break;

            case BuildingModifierType.FireRatePercent:
                weaponModifiers?.AddFireRatePercent(value);
                break;

            case BuildingModifierType.ScrapGainPercent:
                runtimeBonusState?.AddScrapGainPercent(value);
                break;

            case BuildingModifierType.HealEfficiencyPercent:
                runtimeBonusState?.AddHealEfficiencyPercent(value);
                break;

            case BuildingModifierType.PickupRangeBonus:
                runtimeBonusState?.AddPickupRangeBonus(value);
                break;

            case BuildingModifierType.CreditsGainPercent:
                runtimeBonusState?.AddCreditsGainPercent(value);
                break;
        }
    }

    private void ApplyFallbackBuildingEffect(BuildingType buildingType, int level)
    {
        switch (buildingType)
        {
            case BuildingType.Hangar:
                ApplyFallbackHangar(level);
                break;

            case BuildingType.EngineWorkshop:
                ApplyFallbackEngineWorkshop(level);
                break;

            case BuildingType.WeaponLab:
                ApplyFallbackWeaponLab(level);
                break;

            case BuildingType.RecoveryProcessor:
                ApplyFallbackRecoveryProcessor(level);
                break;
        }
    }

    private void ApplyFallbackHangar(int level)
    {
        switch (level)
        {
            case 1:
                runtimeStats.maxHp += 2f;
                runtimeStats.cargoCapacity += 10;
                break;

            case 2:
                runtimeStats.maxHp += 4f;
                runtimeStats.cargoCapacity += 20;
                runtimeBonusState?.AddRepairEfficiencyPercent(10f);
                break;

            default:
                runtimeStats.maxHp += 6f;
                runtimeStats.cargoCapacity += 30;
                runtimeBonusState?.AddRepairEfficiencyPercent(20f);
                break;
        }
    }

    private void ApplyFallbackEngineWorkshop(int level)
    {
        switch (level)
        {
            case 1:
                ApplyMoveSpeedPercent(5f);
                break;

            case 2:
                ApplyMoveSpeedPercent(5f);
                runtimeStats.dashDistance += 0.5f;
                break;

            default:
                ApplyMoveSpeedPercent(8f);
                runtimeStats.dashDistance += 0.5f;
                runtimeStats.dashCooldown -= 0.1f;
                break;
        }
    }

    private void ApplyFallbackWeaponLab(int level)
    {
        if (weaponModifiers == null)
        {
            return;
        }

        switch (level)
        {
            case 1:
                weaponModifiers.AddDamagePercent(10f);
                break;

            case 2:
                weaponModifiers.AddDamagePercent(10f);
                weaponModifiers.AddProjectileSpeedPercent(10f);
                break;

            default:
                weaponModifiers.AddDamagePercent(10f);
                weaponModifiers.AddProjectileSpeedPercent(10f);
                weaponModifiers.AddFireRatePercent(8f);
                break;
        }
    }

    private void ApplyFallbackRecoveryProcessor(int level)
    {
        if (runtimeBonusState == null)
        {
            return;
        }

        switch (level)
        {
            case 1:
                runtimeBonusState.AddScrapGainPercent(10f);
                runtimeBonusState.AddHarvestYieldPercent(5f);
                break;

            case 2:
                runtimeBonusState.AddScrapGainPercent(10f);
                runtimeBonusState.AddHarvestYieldPercent(8f);
                runtimeBonusState.AddHealEfficiencyPercent(25f);
                break;

            default:
                runtimeBonusState.AddScrapGainPercent(10f);
                runtimeBonusState.AddHarvestYieldPercent(12f);
                runtimeBonusState.AddHealEfficiencyPercent(25f);
                runtimeBonusState.AddPickupRangeBonus(1.5f);
                break;
        }
    }

    private void ApplyPermanentTraits(PermanentProgress progress, IReadOnlyList<TraitDefinition> traitDefinitions, WeaponTreeType selectedWeaponTree)
    {
        if (progress == null || traitDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            TraitDefinition trait = traitDefinitions[i];
            if (trait == null)
            {
                continue;
            }

            int level = Mathf.Clamp(progress.GetTraitLevel(trait.TraitId), 0, trait.MaxLevel);
            if (level <= 0)
            {
                continue;
            }

            if (!progress.IsTraitActive(trait.TraitId))
            {
                continue;
            }

            if (!trait.IsAvailableFor(selectedWeaponTree))
            {
                continue;
            }

            ApplyTraitLevelEffects(trait, level);
        }

        ClampStats();
    }

    private void ApplyRunTraits(RunContext runContext, IReadOnlyList<TraitDefinition> traitDefinitions, WeaponTreeType selectedWeaponTree)
    {
        if (runContext == null || runContext.SelectedTraitIds == null || traitDefinitions == null)
        {
            return;
        }

        foreach (string traitId in runContext.SelectedTraitIds)
        {
            TraitDefinition trait = FindTraitDefinition(traitDefinitions, traitId);
            if (trait == null)
            {
                continue;
            }

            if (!trait.IsAvailableFor(selectedWeaponTree))
            {
                continue;
            }

            ApplyTraitLevelEffects(trait, 1);
        }

        ClampStats();
    }

    private void ApplyTraitLevelEffects(TraitDefinition trait, int level)
    {
        if (trait == null || trait.LevelEffects == null || level <= 0)
        {
            return;
        }

        for (int i = 0; i < trait.LevelEffects.Count; i++)
        {
            TraitLevelEffect effect = trait.LevelEffects[i];
            if (effect == null)
            {
                continue;
            }

            bool shouldApply = applyTraitEffectsCumulatively ? effect.Level <= level : effect.Level == level;

            if (!shouldApply)
            {
                continue;
            }

            ApplyTraitEffect(effect.EffectType, effect.Value);
        }
    }

    private void ApplyTraitEffect(TraitEffectType effectType, float value)
    {
        switch (effectType)
        {
            case TraitEffectType.DamagePercent:
                weaponModifiers?.AddDamagePercent(value);
                break;

            case TraitEffectType.ProjectileSpeedPercent:
                weaponModifiers?.AddProjectileSpeedPercent(value);
                break;

            case TraitEffectType.RangePercent:
                weaponModifiers?.AddRangePercent(value);
                break;

            case TraitEffectType.MoveSpeedPercent:
                ApplyMoveSpeedPercent(value);
                break;

            case TraitEffectType.DashCooldownReduction:
                runtimeStats.dashCooldown -= Mathf.Abs(value);
                break;

            case TraitEffectType.DashDistanceBonus:
                runtimeStats.dashDistance += value;
                break;

            case TraitEffectType.MaxHpBonus:
                runtimeStats.maxHp += value;
                break;

            case TraitEffectType.HealEfficiencyPercent:
                runtimeBonusState?.AddHealEfficiencyPercent(value);
                break;

            case TraitEffectType.PickupRangeBonus:
                runtimeBonusState?.AddPickupRangeBonus(value);
                break;

            case TraitEffectType.SpreadReductionPercent:
                weaponModifiers?.AddSpreadReductionPercent(Mathf.Abs(value));
                break;

            case TraitEffectType.ProjectileCountBonus:
                weaponModifiers?.AddProjectileCount(Mathf.RoundToInt(value));
                break;

            case TraitEffectType.PierceCountBonus:
                weaponModifiers?.AddPierceCount(Mathf.RoundToInt(value));
                break;

            case TraitEffectType.ChargeTimeReductionPercent:
                weaponModifiers?.AddChargeSpeedPercent(Mathf.Abs(value));
                break;

            case TraitEffectType.ChargeDamagePercent:
                weaponModifiers?.AddChargeDamagePercent(value);
                break;

            case TraitEffectType.HomingAngleBonus:
                weaponModifiers?.AddHomingAngle(value);
                break;

            case TraitEffectType.HomingRangeBonus:
                weaponModifiers?.AddHomingRange(value);
                break;

            case TraitEffectType.FireRatePercent:
                weaponModifiers?.AddFireRatePercent(value);
                break;

            case TraitEffectType.CargoCapacityBonus:
                runtimeStats.cargoCapacity += Mathf.RoundToInt(value);
                runtimeBonusState?.AddCargoCapacityBonus(value);
                break;

            case TraitEffectType.HarvestYieldPercent:
                runtimeBonusState?.AddHarvestYieldPercent(value);
                break;

            case TraitEffectType.HarvestObjectDamagePercent:
                runtimeBonusState?.AddHarvestObjectDamagePercent(value);
                break;

            case TraitEffectType.EmergencyReturnCapacityRatioBonus:
                runtimeStats.emergencyReturnCapacityRatio += value * 0.01f;
                runtimeBonusState?.AddEmergencyReturnCapacityRatioBonus(value);
                break;

            case TraitEffectType.RadarScanRadiusBonus:
                runtimeBonusState?.AddRadarScanRadiusBonus(value);
                break;

            case TraitEffectType.ActiveCooldownReductionPercent:
                runtimeBonusState?.AddActiveCooldownReductionPercent(value);
                break;

            case TraitEffectType.RadarTauntDurationBonus:
                runtimeBonusState?.AddRadarTauntDurationBonus(value);
                break;

            case TraitEffectType.RadarStealthDurationBonus:
                runtimeBonusState?.AddRadarStealthDurationBonus(value);
                break;
        }
    }

    private void ApplyMoveSpeedPercent(float percent)
    {
        runtimeStats.moveSpeed *= 1f + (percent * 0.01f);
    }

    private void CommitStats(bool refillHealth, RunContext runContext)
    {
        ClampStats();

        if (playerHealth != null)
        {
            playerHealth.SetMaxHp(runtimeStats.maxHp, refillHealth);
        }

        if (playerController != null)
        {
            playerController.SetMoveSpeed(runtimeStats.moveSpeed);
        }

        if (playerDash != null)
        {
            playerDash.SetDashDistance(runtimeStats.dashDistance);
            playerDash.SetDashCooldown(runtimeStats.dashCooldown);
        }

        if (runContext != null && runContext.IsActive)
        {
            runContext.SetCargoRule(runtimeStats.cargoCapacity, runtimeStats.emergencyReturnCapacityRatio, scrapCargoWeight, coreShardCargoWeight);
        }

        if (cargoController != null)
        {
            cargoController.SetRuntimeCargoRule(runtimeStats.cargoCapacity, runtimeStats.emergencyReturnCapacityRatio);
        }
    }

    private void ClampStats()
    {
        runtimeStats.maxHp = Mathf.Max(1f, runtimeStats.maxHp);
        runtimeStats.moveSpeed = Mathf.Max(0.1f, runtimeStats.moveSpeed);
        runtimeStats.dashDistance = Mathf.Max(0.1f, runtimeStats.dashDistance);
        runtimeStats.dashCooldown = Mathf.Max(0.05f, runtimeStats.dashCooldown);
        runtimeStats.cargoCapacity = Mathf.Max(1, runtimeStats.cargoCapacity);
        runtimeStats.emergencyReturnCapacityRatio = Mathf.Clamp01(runtimeStats.emergencyReturnCapacityRatio);
    }
}
