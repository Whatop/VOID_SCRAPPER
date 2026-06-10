public sealed class SeaRegionDefinition
{
    public SeaRegionType RegionType { get; }
    public string DisplayName { get; }
    public string Description { get; }

    public float PlayerMoveSpeedPercent { get; }
    public float PlayerDamagePercent { get; }
    public float PlayerProjectileSpeedPercent { get; }
    public float PlayerFireRatePercent { get; }
    public float PlayerDashCooldownDelta { get; }

    public float ScrapGainPercent { get; }
    public float CreditsGainPercent { get; }

    public float EnemyHpMultiplier { get; }

    public int ExtraHighValueWreckCount { get; }
    public int ExtraSupplyContainerCount { get; }
    public int ExtraDestroyedHullCount { get; }
    public int ExtraMeteorCount { get; }

    public int ExtraBasicEnemyCount { get; }
    public int ExtraShotgunEnemyCount { get; }
    public int ExtraChargingEnemyCount { get; }
    public int ExtraEliteEnemyCount { get; }

    public SeaRegionDefinition(
        SeaRegionType regionType,
        string displayName,
        string description,
        float playerMoveSpeedPercent = 0f,
        float playerDamagePercent = 0f,
        float playerProjectileSpeedPercent = 0f,
        float playerFireRatePercent = 0f,
        float playerDashCooldownDelta = 0f,
        float scrapGainPercent = 0f,
        float creditsGainPercent = 0f,
        float enemyHpMultiplier = 1f,
        int extraHighValueWreckCount = 0,
        int extraSupplyContainerCount = 0,
        int extraDestroyedHullCount = 0,
        int extraMeteorCount = 0,
        int extraBasicEnemyCount = 0,
        int extraShotgunEnemyCount = 0,
        int extraChargingEnemyCount = 0,
        int extraEliteEnemyCount = 0)
    {
        RegionType = regionType;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? regionType.ToString() : displayName;
        Description = description ?? string.Empty;

        PlayerMoveSpeedPercent = playerMoveSpeedPercent;
        PlayerDamagePercent = playerDamagePercent;
        PlayerProjectileSpeedPercent = playerProjectileSpeedPercent;
        PlayerFireRatePercent = playerFireRatePercent;
        PlayerDashCooldownDelta = playerDashCooldownDelta;

        ScrapGainPercent = scrapGainPercent;
        CreditsGainPercent = creditsGainPercent;

        EnemyHpMultiplier = enemyHpMultiplier <= 0f ? 1f : enemyHpMultiplier;

        ExtraHighValueWreckCount = extraHighValueWreckCount;
        ExtraSupplyContainerCount = extraSupplyContainerCount;
        ExtraDestroyedHullCount = extraDestroyedHullCount;
        ExtraMeteorCount = extraMeteorCount;

        ExtraBasicEnemyCount = extraBasicEnemyCount;
        ExtraShotgunEnemyCount = extraShotgunEnemyCount;
        ExtraChargingEnemyCount = extraChargingEnemyCount;
        ExtraEliteEnemyCount = extraEliteEnemyCount;
    }
}