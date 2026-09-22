using System.Collections.Generic;
using UnityEngine;

public class PlayerRuntimeStatApplier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerWeaponModifiers weaponModifiers;
    [SerializeField] private PlayerRuntimeBonusState runtimeBonusState;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private PlayerShipVisualController shipVisualController;

    [Header("Base Stats")]
    [SerializeField] private float baseMaxHp = 20f;
    [SerializeField] private float baseMaxArmor;
    [SerializeField] private float baseStartingArmor;
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float baseDashDistance = 5f;
    [SerializeField] private float baseDashCooldown = 1.1f;
    [SerializeField] private int baseCargoCapacity = 100;
    [Range(0f, 1f)]
    [SerializeField] private float baseEmergencyReturnCapacityRatio = 0.7f;

    [Header("Cargo Weight")]
    [SerializeField] private int scrapCargoWeight = 2;
    [SerializeField] private int coreShardCargoWeight = 12;
    [SerializeField] private int stabilizedAlloyCargoWeight = 2;

    [Header("Trait Apply Rule")]
    [SerializeField] private bool applyTraitEffectsCumulatively;

    [Header("Debug")]
    [SerializeField] private bool logApplyResult = true;

    private RuntimeStats runtimeStats;
    private OperatingFrameProfile appliedOperatingFrame;
    private bool hasOperatingFrame;

    private struct RuntimeStats
    {
        public float maxHp;
        public float maxArmor;
        public float startingArmor;
        public float moveSpeed;
        public float dashDistance;
        public float dashCooldown;
        public int cargoCapacity;
        public float emergencyReturnCapacityRatio;

        public RuntimeStats(
            float baseMaxHp,
            float baseMaxArmor,
            float baseStartingArmor,
            float baseMoveSpeed,
            float baseDashDistance,
            float baseDashCooldown,
            int baseCargoCapacity,
            float baseEmergencyReturnCapacityRatio)
        {
            maxHp = Mathf.Max(1f, baseMaxHp);
            startingArmor = Mathf.Max(0f, baseStartingArmor);
            maxArmor = Mathf.Max(Mathf.Max(0f, baseMaxArmor), startingArmor);
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

    private void OnDisable()
    {
        ClearOperatingFrameMovement();
        PlayerPeriodicReflector2D.SetSourceEnabled(gameObject, this, false);
        PlayerMachineGunDashMissileSalvo.SetSourceEnabled(gameObject, this, false);
        PlayerSniperDashEchoShot.SetSourceEnabled(gameObject, this, false);
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
            baseMaxArmor,
            baseStartingArmor,
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
        ApplyOperatingFrame(runContext);

        ApplySectorTechnologies(progress);
        ApplyPermanentTraits(progress, traitDefinitions, selectedWeaponTree);

        // 런 중 Trait는 RunRuntimeTraitStore + RunTraitEffectApplier가 단독 적용한다.
        // 여기서 다시 적용하면 심부 해역 씬 전환 때 효과가 중복된다.
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
                $"HP: {runtimeStats.maxHp}, Armor: {runtimeStats.startingArmor}/{runtimeStats.maxArmor}, " +
                $"HealEfficiency: {(runtimeBonusState?.HealEfficiencyMultiplier ?? 1f):0.##}x, Move: {runtimeStats.moveSpeed:0.##}, " +
                $"DashDistance: {runtimeStats.dashDistance:0.##}, DashCooldown: {runtimeStats.dashCooldown:0.##}, " +
                $"Cargo: {runtimeStats.cargoCapacity}, EmergencyReturnRatio: {runtimeStats.emergencyReturnCapacityRatio:0.##}",
                this
            );
        }
    }

    public bool TryGetCurrentVitals(out float currentHp, out float currentArmor)
    {
        CacheReferences();

        currentHp = playerHealth != null ? playerHealth.CurrentHp : 0f;
        currentArmor = playerArmor != null ? playerArmor.CurrentArmor : 0f;

        return playerHealth != null &&
               playerArmor != null &&
               !playerHealth.IsDead &&
               currentHp > 0f;
    }

    public void RestoreCurrentVitals(float currentHp, float currentArmor)
    {
        CacheReferences();
        playerHealth?.RestoreCurrentHp(currentHp);
        playerArmor?.RestoreCurrentArmor(currentArmor);
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerArmor == null)
        {
            playerArmor = GetComponent<PlayerArmor>();
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
        ClearOperatingFrameMovement();
        hasOperatingFrame = false;
        PlayerPeriodicReflector2D.SetSourceEnabled(gameObject, this, false);
        PlayerMachineGunDashMissileSalvo.SetSourceEnabled(gameObject, this, false);
        PlayerSniperDashEchoShot.SetSourceEnabled(gameObject, this, false);

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
    private void ApplyOperatingFrame(RunContext run)
    {
        if (run == null || !run.IsActive) return;
        appliedOperatingFrame = run.FrameProfile;
        hasOperatingFrame = true;
        runtimeStats.maxHp = Mathf.Max(1f, runtimeStats.maxHp + appliedOperatingFrame.MaxHpBonus);
        runtimeStats.cargoCapacity = Mathf.Max(1, runtimeStats.cargoCapacity + appliedOperatingFrame.CargoBonus);
        runtimeStats.dashDistance += appliedOperatingFrame.DashDistanceBonus;
        weaponModifiers?.AddDamagePercent(appliedOperatingFrame.DamagePercent);
        runtimeBonusState?.AddHarvestYieldPercent(appliedOperatingFrame.HarvestYieldPercent);
        RestoreOperatingFrameMovement();
    }

    private void OnEnable()
    {
        RestoreOperatingFrameMovement();
    }

    private void RestoreOperatingFrameMovement()
    {
        if (!hasOperatingFrame) return;
        playerController?.SetExternalMoveSpeedMultiplier(this, appliedOperatingFrame.MoveMultiplier);
        playerDash?.SetExternalCooldownMultiplier(this, appliedOperatingFrame.DashCooldownMultiplier);
    }

    private void ClearOperatingFrameMovement()
    {
        playerController?.ClearExternalMoveSpeedMultiplier(this);
        playerDash?.ClearExternalCooldownMultiplier(this);
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

    private void ApplySectorTechnologies(PermanentProgress progress)
    {
        if (progress == null)
        {
            return;
        }

        IReadOnlyList<SectorTechnologyDefinition> definitions = SectorTechnologyCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorTechnologyDefinition definition = definitions[i];
            int level = progress.GetSectorTechnologyLevel(definition.Id);
            if (level <= 0)
            {
                continue;
            }

            float value = definition.GetEffectValue(level);
            switch (definition.EffectType)
            {
                case SectorTechnologyEffectType.MaxHp:
                    runtimeStats.maxHp += value;
                    break;

                case SectorTechnologyEffectType.StartingArmor:
                    runtimeStats.startingArmor += value;
                    runtimeStats.maxArmor = Mathf.Max(runtimeStats.maxArmor, runtimeStats.startingArmor);
                    break;

                case SectorTechnologyEffectType.HealEfficiencyPercent:
                    runtimeBonusState?.AddHealEfficiencyPercent(value);
                    break;

                case SectorTechnologyEffectType.DamagePercent:
                    weaponModifiers?.AddDamagePercent(value);
                    break;

                case SectorTechnologyEffectType.ScrapGainPercent:
                    runtimeBonusState?.AddScrapGainPercent(value);
                    break;
            }
        }

        ClampStats();
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

            bool ownsPersistentStoryTrait =
                trait.IsPersistentStoryTrait && progress.HasPersistentStoryTrait(trait);
            int level = ownsPersistentStoryTrait
                ? 1
                : 0;

            if (level <= 0)
            {
                continue;
            }

            if (!ownsPersistentStoryTrait && !progress.IsTraitActive(trait.TraitId))
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
        if (weaponModifiers != null && weaponModifiers.TryApplyDevelopmentEffect(effectType, value)) return;
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

            case TraitEffectType.RemovePierceDamageFalloff:
                weaponModifiers?.AddPierceDamageFalloffRemoval(Mathf.Max(1, Mathf.RoundToInt(value)));
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

            case TraitEffectType.SniperSemiAutoMode:
                weaponModifiers?.AddSniperSemiAutoMode(Mathf.Max(1, Mathf.RoundToInt(value)));
                break;

            case TraitEffectType.ShotgunCloseRangeDamagePercent:
                weaponModifiers?.AddShotgunCloseRangeDamagePercent(value);
                break;

            case TraitEffectType.MachineGunTerminalGuidance:
                weaponModifiers?.AddMachineGunTerminalGuidance(
                    Mathf.Max(1, Mathf.RoundToInt(value))
                );
                break;

            case TraitEffectType.PeriodicReflectiveShield:
                PlayerPeriodicReflector2D.SetSourceEnabled(
                    gameObject,
                    this,
                    true,
                    Mathf.Max(0.05f, value)
                );
                break;

            case TraitEffectType.MachineGunDashMissileSalvo:
                PlayerMachineGunDashMissileSalvo.SetSourceEnabled(gameObject, this, true);
                break;

            case TraitEffectType.SniperDashEchoShot:
                PlayerSniperDashEchoShot.SetSourceEnabled(gameObject, this, true);
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

        if (playerArmor != null)
        {
            playerArmor.SetMaxArmor(runtimeStats.maxArmor, false);
            if (refillHealth)
            {
                playerArmor.SetArmor(runtimeStats.startingArmor);
            }
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
            runContext.SetCargoRule(
                runtimeStats.cargoCapacity,
                runtimeStats.emergencyReturnCapacityRatio,
                scrapCargoWeight,
                coreShardCargoWeight,
                stabilizedAlloyCargoWeight
            );
        }

        if (cargoController != null)
        {
            cargoController.SetRuntimeCargoRule(runtimeStats.cargoCapacity, runtimeStats.emergencyReturnCapacityRatio);
        }
    }

    private void ClampStats()
    {
        runtimeStats.maxHp = Mathf.Max(1f, runtimeStats.maxHp);
        runtimeStats.maxArmor = Mathf.Max(0f, runtimeStats.maxArmor);
        runtimeStats.startingArmor = Mathf.Clamp(runtimeStats.startingArmor, 0f, runtimeStats.maxArmor);
        runtimeStats.moveSpeed = Mathf.Max(0.1f, runtimeStats.moveSpeed);
        runtimeStats.dashDistance = Mathf.Max(0.1f, runtimeStats.dashDistance);
        runtimeStats.dashCooldown = Mathf.Max(0.05f, runtimeStats.dashCooldown);
        runtimeStats.cargoCapacity = Mathf.Max(1, runtimeStats.cargoCapacity);
        runtimeStats.emergencyReturnCapacityRatio = Mathf.Clamp01(runtimeStats.emergencyReturnCapacityRatio);
    }
}
