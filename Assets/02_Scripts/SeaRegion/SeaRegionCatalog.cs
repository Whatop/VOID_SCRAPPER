using UnityEngine;

public static class SeaRegionCatalog
{
    private static readonly SeaRegionType[] regionTypes =
    {
        SeaRegionType.DenseDebris,
        SeaRegionType.ElectromagneticStorm,
        SeaRegionType.RaiderOccupied
    };

    private static readonly SeaRegionDefinition denseDebris = new SeaRegionDefinition(
        SeaRegionType.DenseDebris,
        "잔해 밀집 해역",
        "수확물이 많고 스크랩 획득량이 증가하지만 잔해가 많아 이동이 둔해진다.",
        playerMoveSpeedPercent: -5f,
        scrapGainPercent: 20f,
        extraSupplyContainerCount: 4,
        extraDestroyedHullCount: 2,
        extraMeteorCount: 10
    );

    private static readonly SeaRegionDefinition electromagneticStorm = new SeaRegionDefinition(
        SeaRegionType.ElectromagneticStorm,
        "전자기 폭풍 해역",
        "전자기 교란으로 플레이어 탄속이 낮아지지만, 적 장갑도 불안정해진다. 차징형 적 출현이 증가한다.",
        playerProjectileSpeedPercent: -15f,
        playerDashCooldownDelta: -0.1f,
        enemyHpMultiplier: 0.9f,
        extraChargingEnemyCount: 2
    );

    private static readonly SeaRegionDefinition raiderOccupied = new SeaRegionDefinition(
        SeaRegionType.RaiderOccupied,
        "약탈자 점거 해역",
        "적 밀도와 엘리트 출현이 증가한다. 전투 위험이 큰 대신 크레딧 획득량과 화력이 증가한다.",
        playerDamagePercent: 8f,
        creditsGainPercent: 25f,
        enemyHpMultiplier: 1.1f,
        extraBasicEnemyCount: 6,
        extraShotgunEnemyCount: 2,
        extraEliteEnemyCount: 1
    );

    public static SeaRegionType[] RegionTypes => regionTypes;

    public static SeaRegionDefinition Get(SeaRegionType regionType)
    {
        return regionType switch
        {
            SeaRegionType.DenseDebris => denseDebris,
            SeaRegionType.ElectromagneticStorm => electromagneticStorm,
            SeaRegionType.RaiderOccupied => raiderOccupied,
            _ => denseDebris
        };
    }

    public static string GetDisplayName(SeaRegionType regionType)
    {
        return Get(regionType).DisplayName;
    }

    public static string GetDescription(SeaRegionType regionType)
    {
        return Get(regionType).Description;
    }

    public static SeaRegionType GetRandom()
    {
        if (regionTypes == null || regionTypes.Length == 0)
        {
            return SeaRegionType.DenseDebris;
        }

        return regionTypes[Random.Range(0, regionTypes.Length)];
    }

    public static SeaRegionType GetRandom(SeaRegionType excludedType)
    {
        if (regionTypes == null || regionTypes.Length == 0)
        {
            return SeaRegionType.DenseDebris;
        }

        if (regionTypes.Length == 1)
        {
            return regionTypes[0];
        }

        for (int i = 0; i < 16; i++)
        {
            SeaRegionType selected = GetRandom();

            if (selected != excludedType)
            {
                return selected;
            }
        }

        for (int i = 0; i < regionTypes.Length; i++)
        {
            if (regionTypes[i] != excludedType)
            {
                return regionTypes[i];
            }
        }

        return regionTypes[0];
    }
}