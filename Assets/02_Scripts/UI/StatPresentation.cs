using System;

// UI semantics only. Values and gameplay ownership remain in their existing owners.
public enum StatCategory { Health, Defense, Damage, FireRate, Movement, Dash, Cargo, Harvest, ProjectileSpeed, Range, Accuracy, Homing, Pierce, Charge, Radar, Active, Recovery, Special }
public enum StatSummaryGroup { Survival, Combat, Mobility, Salvage, Utility, Special }

public static class StatPresentation
{
    public static string Hex(StatCategory category) => category switch
    {
        StatCategory.Health => "#FF6B6B",
        StatCategory.Defense => "#55D6BE",
        StatCategory.Damage => "#FF9F43",
        StatCategory.FireRate => "#FFD166",
        StatCategory.Movement => "#9BE564",
        StatCategory.Dash => "#46E6C8",
        StatCategory.Cargo => "#D7A75E",
        StatCategory.Harvest => "#6FD08C",
        StatCategory.ProjectileSpeed => "#6CCBFF",
        StatCategory.Range => "#5B8CFF",
        StatCategory.Accuracy => "#7EE7F2",
        StatCategory.Homing => "#B58CFF",
        StatCategory.Pierce => "#D77BFF",
        StatCategory.Charge => "#FF82C8",
        StatCategory.Radar => "#44DDE7",
        StatCategory.Active => "#7AA8FF",
        StatCategory.Recovery => "#70E08F",
        StatCategory.Special => "#FFD76A",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static string Rich(StatCategory category, string plain) =>
        string.IsNullOrEmpty(plain) ? string.Empty : $"<color={Hex(category)}>{plain}</color>";

    public static StatCategory Category(TraitEffectType effect) => effect switch
    {
        TraitEffectType.MaxHpBonus => StatCategory.Health,
        TraitEffectType.CloseRangeDamageReductionPercent or
        TraitEffectType.PeriodicReflectiveShield => StatCategory.Defense,
        TraitEffectType.DamagePercent or
        TraitEffectType.ShotgunCloseRangeDamagePercent => StatCategory.Damage,
        TraitEffectType.FireRatePercent => StatCategory.FireRate,
        TraitEffectType.MoveSpeedPercent => StatCategory.Movement,
        TraitEffectType.DashCooldownReduction or
        TraitEffectType.DashDistanceBonus or
        TraitEffectType.DashDamageReductionPercent or
        TraitEffectType.MachineGunDashMissileSalvo or
        TraitEffectType.SniperDashEchoShot => StatCategory.Dash,
        TraitEffectType.CargoCapacityBonus or
        TraitEffectType.EmergencyReturnCapacityRatioBonus => StatCategory.Cargo,
        TraitEffectType.PickupRangeBonus or
        TraitEffectType.HarvestYieldPercent or
        TraitEffectType.HarvestObjectDamagePercent => StatCategory.Harvest,
        TraitEffectType.ProjectileSpeedPercent => StatCategory.ProjectileSpeed,
        TraitEffectType.RangePercent => StatCategory.Range,
        TraitEffectType.SpreadReductionPercent => StatCategory.Accuracy,
        TraitEffectType.HomingAngleBonus or
        TraitEffectType.HomingRangeBonus or
        TraitEffectType.MachineGunTerminalGuidance or
        TraitEffectType.MachineGunTargetDistribution => StatCategory.Homing,
        TraitEffectType.PierceCountBonus or
        TraitEffectType.RemovePierceDamageFalloff => StatCategory.Pierce,
        TraitEffectType.ChargeDamagePercent or
        TraitEffectType.ChargeTimeReductionPercent or
        TraitEffectType.ChargedProjectileSizePercent or
        TraitEffectType.ChargeSightBonusPercent or
        TraitEffectType.SniperMovingChargeBonus or
        TraitEffectType.SniperReserveCapacitor => StatCategory.Charge,
        TraitEffectType.RadarScanRadiusBonus or
        TraitEffectType.RadarTauntDurationBonus or
        TraitEffectType.RadarStealthDurationBonus => StatCategory.Radar,
        TraitEffectType.ActiveCooldownReductionPercent => StatCategory.Active,
        TraitEffectType.HealEfficiencyPercent => StatCategory.Recovery,
        TraitEffectType.ProjectileCountBonus or
        TraitEffectType.CloseRangeSuppressionPercent or
        TraitEffectType.SectorBarrierProtocol or
        TraitEffectType.MatterReconstructorProtocol or
        TraitEffectType.PhaseAfterimageProtocol or
        TraitEffectType.SniperSemiAutoMode or
        TraitEffectType.MachineGunCoolingRatePercent or
        TraitEffectType.MachineGunCoolingDelayReduction or
        TraitEffectType.MachineGunTwinFeed or
        TraitEffectType.ShotgunSlugCoupler or
        TraitEffectType.ShotgunBreachSequence or
        TraitEffectType.ShotgunImpactDisplacement => StatCategory.Special,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Missing stat presentation mapping.")
    };

    public static StatCategory Category(ReinforcementEffectType effect) => effect switch
    {
        ReinforcementEffectType.HealFlat or
        ReinforcementEffectType.ConvertCreditsToHealing => StatCategory.Recovery,
        ReinforcementEffectType.AddArmor or
        ReinforcementEffectType.AddInvincibleTime or
        ReinforcementEffectType.ClearEnemyProjectiles or
        ReinforcementEffectType.ClearEnemyProjectilesInCone => StatCategory.Defense,
        ReinforcementEffectType.DamageNearbyEnemies or
        ReinforcementEffectType.DamageEnemiesInCone or
        ReinforcementEffectType.TemporaryDamagePercent => StatCategory.Damage,
        ReinforcementEffectType.TemporaryFireRatePercent => StatCategory.FireRate,
        ReinforcementEffectType.TemporaryMoveSpeedPercent => StatCategory.Movement,
        ReinforcementEffectType.TemporaryDashCooldownReductionPercent => StatCategory.Dash,
        ReinforcementEffectType.TemporaryEnemyRadarJamming or
        ReinforcementEffectType.RevealEnemyVisionAndState or
        ReinforcementEffectType.RevealRadarTargets or
        ReinforcementEffectType.DisruptEnemyTracking => StatCategory.Radar,
        ReinforcementEffectType.TemporaryScrapGainPercent => StatCategory.Harvest,
        ReinforcementEffectType.TemporaryHomingAngleBonus or
        ReinforcementEffectType.TemporaryHomingRangeBonus => StatCategory.Homing,
        ReinforcementEffectType.TemporaryPierceCountBonus or
        ReinforcementEffectType.TemporaryRemovePierceDamageFalloff => StatCategory.Pierce,
        ReinforcementEffectType.KnockbackNearbyEnemies or
        ReinforcementEffectType.SpawnPrefabAtPlayer or
        ReinforcementEffectType.EmergencyReturn or
        ReinforcementEffectType.PlaceOrReturnToMarker => StatCategory.Special,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Missing reinforcement presentation mapping.")
    };

    public static StatSummaryGroup Group(StatCategory category) => category switch
    {
        StatCategory.Health or StatCategory.Defense or StatCategory.Recovery => StatSummaryGroup.Survival,
        StatCategory.Movement or StatCategory.Dash => StatSummaryGroup.Mobility,
        StatCategory.Cargo or StatCategory.Harvest => StatSummaryGroup.Salvage,
        StatCategory.Radar or StatCategory.Active => StatSummaryGroup.Utility,
        StatCategory.Special => StatSummaryGroup.Special,
        _ => StatSummaryGroup.Combat
    };

    // Stable semantic ordering, independent of acquisition order or numeric enum IDs.
    public static int SortKey(TraitEffectType effect)
    {
        StatCategory category = Category(effect);
        int order = category switch
        {
            StatCategory.Health => 0, StatCategory.Defense => 1, StatCategory.Recovery => 2,
            StatCategory.Damage => 0, StatCategory.FireRate => 1, StatCategory.Accuracy => 2,
            StatCategory.ProjectileSpeed => 3, StatCategory.Range => 4, StatCategory.Homing => 5,
            StatCategory.Pierce => 6, StatCategory.Charge => 7,
            StatCategory.Movement or StatCategory.Cargo or StatCategory.Radar => 0,
            _ => 1
        };
        return (int)Group(category) * 10000 + order * 100 + (int)effect;
    }

    public static string Trait(TraitEffectType effect, float value) =>
        Rich(Category(effect), TraitEffectTextUtility.FormatEffect(effect, value));
}
