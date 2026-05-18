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

    [Header("Base Stats")]
    [SerializeField] private float baseMaxHp = 20f;
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float baseDashDistance = 5f;
    [SerializeField] private float baseDashCooldown = 0.7f;

    [Header("Trait Apply Rule")]
    [Tooltip("false면 현재 레벨 효과만 적용. true면 1레벨부터 현재 레벨까지 누적 적용.")]
    [SerializeField] private bool applyTraitEffectsCumulatively;

    [Header("Building Fallback")]
    [Tooltip("BuildingDefinition에 modifier가 비어 있으면 SettlementController의 기본 효과표 기준으로 적용.")]
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

        public RuntimeStats(float baseMaxHp, float baseMoveSpeed, float baseDashDistance, float baseDashCooldown)
        {
            maxHp = Mathf.Max(1f, baseMaxHp);
            moveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
            dashDistance = Mathf.Max(0.1f, baseDashDistance);
            dashCooldown = Mathf.Max(0.05f, baseDashCooldown);
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
            baseDashCooldown
        );

        ResetRuntimeModifiers();

        ShipDefinition selectedShip = FindShipDefinition(shipDefinitions, selectedShipId);
        ApplyShip(selectedShip);

        ApplyBuildings(progress, buildingDefinitions);
        ApplyPermanentTraits(progress, traitDefinitions, selectedWeaponTree);
        ApplyRunTraits(runContext, traitDefinitions, selectedWeaponTree);

        CommitStats(refillHealth);

        if (weaponController != null)
        {
            weaponController.EquipWeapon(selectedWeaponTree);
        }

        if (logApplyResult)
        {
            Debug.Log(
                $"Runtime Stat Apply 완료 / " +
                $"Weapon: {selectedWeaponTree}, Ship: {selectedShipId}, " +
                $"HP: {runtimeStats.maxHp}, Move: {runtimeStats.moveSpeed:0.##}, " +
                $"DashDistance: {runtimeStats.dashDistance:0.##}, DashCooldown: {runtimeStats.dashCooldown:0.##}",
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

    private void ApplyBuilding(
        PermanentProgress progress,
        IReadOnlyList<BuildingDefinition> buildingDefinitions,
        BuildingType buildingType)
    {
        int level = progress.GetBuildingLevel(buildingType);
        if (level <= 0)
        {
            return;
        }

        BuildingDefinition definition = FindBuildingDefinition(buildingDefinitions, buildingType);
        BuildingLevelDefinition levelDefinition = definition != null
            ? definition.GetLevelDefinition(level)
            : null;

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
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddRepairEfficiencyPercent(value);
                }
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
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddDamagePercent(value);
                }
                break;

            case BuildingModifierType.ProjectileSpeedPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddProjectileSpeedPercent(value);
                }
                break;

            case BuildingModifierType.FireRatePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddFireRatePercent(value);
                }
                break;

            case BuildingModifierType.ScrapGainPercent:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddScrapGainPercent(value);
                }
                break;

            case BuildingModifierType.HealEfficiencyPercent:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddHealEfficiencyPercent(value);
                }
                break;

            case BuildingModifierType.PickupRangeBonus:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddPickupRangeBonus(value);
                }
                break;

            case BuildingModifierType.CreditsGainPercent:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddCreditsGainPercent(value);
                }
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
                break;

            case 2:
                runtimeStats.maxHp += 4f;
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddRepairEfficiencyPercent(10f);
                }
                break;

            default:
                runtimeStats.maxHp += 6f;
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddRepairEfficiencyPercent(20f);
                }
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
                break;

            case 2:
                runtimeBonusState.AddScrapGainPercent(10f);
                runtimeBonusState.AddHealEfficiencyPercent(25f);
                break;

            default:
                runtimeBonusState.AddScrapGainPercent(10f);
                runtimeBonusState.AddHealEfficiencyPercent(25f);
                runtimeBonusState.AddPickupRangeBonus(1.5f);
                break;
        }
    }

    private void ApplyPermanentTraits(
        PermanentProgress progress,
        IReadOnlyList<TraitDefinition> traitDefinitions,
        WeaponTreeType selectedWeaponTree)
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

            int level = progress.GetTraitLevel(trait.TraitId);
            if (level <= 0)
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

    private void ApplyRunTraits(
        RunContext runContext,
        IReadOnlyList<TraitDefinition> traitDefinitions,
        WeaponTreeType selectedWeaponTree)
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

            bool shouldApply = applyTraitEffectsCumulatively
                ? effect.Level <= level
                : effect.Level == level;

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
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddDamagePercent(value);
                }
                break;

            case TraitEffectType.ProjectileSpeedPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddProjectileSpeedPercent(value);
                }
                break;

            case TraitEffectType.RangePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddRangePercent(value);
                }
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
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddHealEfficiencyPercent(value);
                }
                break;

            case TraitEffectType.PickupRangeBonus:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddPickupRangeBonus(value);
                }
                break;

            case TraitEffectType.SpreadReductionPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddSpreadReductionPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ProjectileCountBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddProjectileCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.PierceCountBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddPierceCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.ChargeTimeReductionPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddChargeSpeedPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ChargeDamagePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddChargeDamagePercent(value);
                }
                break;

            case TraitEffectType.HomingAngleBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddHomingAngle(value);
                }
                break;

            case TraitEffectType.HomingRangeBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddHomingRange(value);
                }
                break;

            case TraitEffectType.FireRatePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddFireRatePercent(value);
                }
                break;
        }
    }

    private void ApplyMoveSpeedPercent(float percent)
    {
        runtimeStats.moveSpeed *= 1f + (percent * 0.01f);
    }

    private void CommitStats(bool refillHealth)
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
    }

    private void ClampStats()
    {
        runtimeStats.maxHp = Mathf.Max(1f, runtimeStats.maxHp);
        runtimeStats.moveSpeed = Mathf.Max(0.1f, runtimeStats.moveSpeed);
        runtimeStats.dashDistance = Mathf.Max(0.1f, runtimeStats.dashDistance);
        runtimeStats.dashCooldown = Mathf.Max(0.05f, runtimeStats.dashCooldown);
    }
}