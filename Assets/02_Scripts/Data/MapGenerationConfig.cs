using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Map/Map Generation Config")]
public class MapGenerationConfig : ScriptableObject
{
    [Header("Map Size - Campaign Regions")]
    [Tooltip("기존 에셋 호환용이자 1해역 기본 크기입니다.")]
    [SerializeField] private Vector2 mapSize = new Vector2(120f, 120f);
    [SerializeField] private bool useRegionSpecificMapSizes = true;
    [SerializeField] private Vector2 region1MapSize = new Vector2(120f, 120f);
    [SerializeField] private Vector2 region2MapSize = new Vector2(128f, 128f);
    [SerializeField] private Vector2 region3MapSize = new Vector2(116f, 116f);
    [SerializeField] private Vector2 finalNetworkMapSize = new Vector2(96f, 96f);

    [Header("Placement Rules")]
    [SerializeField] private float startSafeRadius = 10f;
    [SerializeField] private float importantPointMinDistance = 20f;

    [Header("POI Cluster Layout")]
    [SerializeField] private bool enablePoiClusterLayout = true;
    [SerializeField] private int salvagePoiCount = 2;
    [SerializeField] private int highValuePoiCount = 2;
    [SerializeField] private float poiMinSpacing = 14f;
    [SerializeField] private float salvagePoiRadius = 7f;
    [SerializeField] private float highValuePoiRadius = 8f;

    [Range(0f, 1f)]
    [SerializeField] private float clusteredHarvestRatio = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float clusteredEnemyRatio = 0.75f;

    [Header("Environment Dressing")]
    [SerializeField] private bool enableEnvironmentDressing = true;
    [SerializeField] private int salvageDressingCount = 12;
    [SerializeField] private int highValueDressingCount = 18;
    [SerializeField] private int transitDressingCount = 24;
    [SerializeField] private float poiDressingRadiusMultiplier = 1.15f;

    [Header("Finite World Boundary")]
    [SerializeField] private bool enableFiniteBoundary = true;
    [SerializeField] private float boundaryWarningDistance = 7f;
    [SerializeField] private float boundaryResistanceDistance = 3f;

    [Range(0f, 0.95f)]
    [SerializeField] private float boundaryMaxOutwardSpeedReduction = 0.65f;

    [SerializeField] private float boundaryInwardPushSpeed = 2.4f;
    [SerializeField] private float boundaryHardInset = 0.35f;

    [Header("Core / Boss Camera Safety")]
    [SerializeField] private bool keepCoreInsideBossCameraSafeArea = true;
    [SerializeField] private Vector2 bossArenaHalfExtents = new Vector2(14f, 9.8f);
    [SerializeField] private float bossIntroMaxZoomMultiplier = 3.5f;
    [SerializeField] private float bossCameraEdgePadding = 2.5f;
    [SerializeField] private float fallbackBossCameraBaseOrthographicSize = 4.2f;
    [SerializeField] private float fallbackBossCameraAspect = 1.7777778f;

    [Header("Important Points")]
    [Tooltip("적 기지 프리팹 배치 수입니다. 한 해역에 초록/파랑 기지를 둘 경우 2로 설정합니다.")]
    [SerializeField] private int fieldBaseCount = 2;
    [SerializeField] private int shopCount = 2;
    [SerializeField] private int eventCount = 4;
    [SerializeField] private int coreCount = 1;
    [SerializeField] private int fieldNpcCount = 2;

    [Header("Objects")]
    [SerializeField] private int highValueWreckCount = 3;
    [SerializeField] private int supplyContainerCount = 16;
    [SerializeField] private int destroyedHullCount = 8;

    [Header("Meteors by Size")]
    [FormerlySerializedAs("meteorCount")]
    [Tooltip("소형 운석. 수량이 많고 이동 가능한 환경 오브젝트용입니다.")]
    [SerializeField] private int smallMeteorCount = 30;
    [Tooltip("대형 운석. 수량이 적고 벽/LOS 차단 지형용입니다.")]
    [SerializeField] private int largeMeteorCount = 3;

    [Header("Enemies")]
    [SerializeField] private int basicEnemyCount = 20;
    [SerializeField] private int shotgunEnemyCount = 5;
    [SerializeField] private int chargingEnemyCount = 4;
    [SerializeField] private int meleeChargerCount = 2;
    [Tooltip("분할 엘리트 사용을 끈 경우에만 사용하는 기존 엘리트 총수입니다.")]
    [SerializeField] private int eliteEnemyCount = 2;

    [Header("Elite Enemy Variants")]
    [SerializeField] private bool useSplitEliteCounts = true;
    [SerializeField] private int eliteMachineGunCount = 1;
    [SerializeField] private int eliteShotgunCount = 1;
    [SerializeField] private int eliteChargingCount;

    [Header("Enemy Roles - Total enemy count is preserved")]
    [SerializeField] private bool enableEnemyRoles = true;
    [SerializeField] private int defenderBasicCount = 4;
    [SerializeField] private int defenderShotgunCount = 2;
    [SerializeField] private int rivalHarvesterCount = 1;
    [SerializeField] private int scavengerCount = 1;

    [Header("Enemy Role Simulation")]
    [SerializeField] private float roleStartGraceSeconds = 12f;
    [SerializeField] private float roleStartMovementUnlockDistance = 8f;
    [SerializeField] private float rolePreviewDistance = 26f;
    [SerializeField] private float roleActiveDistance = 18f;
    [SerializeField] private float roleActiveHoldSeconds = 8f;
    [SerializeField] private float roleRadarPreviewDuration = 10f;
    [SerializeField] private int maxConcurrentRivalActions = 1;
    [SerializeField] private int maxConcurrentScavengerActions = 1;

    [Header("Campaign Region Modifiers")]
    [Tooltip("2해역 기존 심부 보정. 기존 에셋 호환을 위해 필드명을 유지합니다.")]
    [SerializeField] private float deepZoneEnemyHpMultiplier = 1.2f;
    [SerializeField] private float deepZoneEnemyDamageMultiplier = 1.15f;
    [SerializeField] private float deepZoneScrapMultiplier = 1.25f;
    [SerializeField] private int deepZoneAdditionalCoreReward = 1;

    [SerializeField] private float region3EnemyHpMultiplier = 1.4f;
    [SerializeField] private float region3EnemyDamageMultiplier = 1.3f;
    [SerializeField] private float region3ScrapMultiplier = 1.45f;
    [SerializeField] private int region3CoreReward = 2;

    [SerializeField] private float finalNetworkEnemyHpMultiplier = 1.6f;
    [SerializeField] private float finalNetworkEnemyDamageMultiplier = 1.45f;
    [SerializeField] private float finalNetworkScrapMultiplier = 1.6f;
    [SerializeField] private int finalNetworkCoreReward = 0;

    public Vector2 MapSize => GetMapSize(ExpeditionDepth.Normal);

    public Vector2 GetMapSize(ExpeditionDepth depth)
    {
        if (!useRegionSpecificMapSizes)
        {
            return SanitizeMapSize(mapSize, CampaignProgressionCatalog.GetDefaultMapSize(depth));
        }

        Vector2 configured = depth switch
        {
            ExpeditionDepth.Normal => region1MapSize,
            ExpeditionDepth.DeepZone1 => region2MapSize,
            ExpeditionDepth.DeepZone2 => region3MapSize,
            ExpeditionDepth.FinalNetwork => finalNetworkMapSize,
            _ => mapSize
        };

        return SanitizeMapSize(configured, CampaignProgressionCatalog.GetDefaultMapSize(depth));
    }

    public float StartSafeRadius => startSafeRadius;
    public float ImportantPointMinDistance => importantPointMinDistance;
    public bool EnablePoiClusterLayout => enablePoiClusterLayout;
    public int SalvagePoiCount => Mathf.Max(0, salvagePoiCount);
    public int HighValuePoiCount => Mathf.Max(0, highValuePoiCount);
    public float PoiMinSpacing => Mathf.Max(0f, poiMinSpacing);
    public float SalvagePoiRadius => Mathf.Max(1f, salvagePoiRadius);
    public float HighValuePoiRadius => Mathf.Max(1f, highValuePoiRadius);
    public float ClusteredHarvestRatio => Mathf.Clamp01(clusteredHarvestRatio);
    public float ClusteredEnemyRatio => Mathf.Clamp01(clusteredEnemyRatio);
    public bool EnableEnvironmentDressing => enableEnvironmentDressing;
    public int SalvageDressingCount => Mathf.Max(0, salvageDressingCount);
    public int HighValueDressingCount => Mathf.Max(0, highValueDressingCount);
    public int TransitDressingCount => Mathf.Max(0, transitDressingCount);
    public float PoiDressingRadiusMultiplier => Mathf.Max(0.5f, poiDressingRadiusMultiplier);

    public bool EnableFiniteBoundary => enableFiniteBoundary;
    public float BoundaryWarningDistance => Mathf.Max(0f, boundaryWarningDistance);
    public float BoundaryResistanceDistance => Mathf.Max(0.05f, boundaryResistanceDistance);
    public float BoundaryMaxOutwardSpeedReduction => Mathf.Clamp(boundaryMaxOutwardSpeedReduction, 0f, 0.95f);
    public float BoundaryInwardPushSpeed => Mathf.Max(0f, boundaryInwardPushSpeed);
    public float BoundaryHardInset => Mathf.Max(0f, boundaryHardInset);

    public bool KeepCoreInsideBossCameraSafeArea => keepCoreInsideBossCameraSafeArea;
    public Vector2 BossArenaHalfExtents => new Vector2(
        Mathf.Max(0f, bossArenaHalfExtents.x),
        Mathf.Max(0f, bossArenaHalfExtents.y)
    );
    public float BossIntroMaxZoomMultiplier => Mathf.Max(1f, bossIntroMaxZoomMultiplier);
    public float BossCameraEdgePadding => Mathf.Max(0f, bossCameraEdgePadding);
    public float FallbackBossCameraBaseOrthographicSize => Mathf.Max(0.1f, fallbackBossCameraBaseOrthographicSize);
    public float FallbackBossCameraAspect => Mathf.Max(0.1f, fallbackBossCameraAspect);

    public int FieldBaseCount => Mathf.Max(0, fieldBaseCount);
    public int ShopCount => Mathf.Max(0, shopCount);
    public int EventCount => eventCount;
    public int CoreCount => coreCount;
    public int FieldNpcCount => Mathf.Max(0, fieldNpcCount);

    public int HighValueWreckCount => highValueWreckCount;
    public int SupplyContainerCount => supplyContainerCount;
    public int DestroyedHullCount => destroyedHullCount;
    public int SmallMeteorCount => Mathf.Max(0, smallMeteorCount);
    public int LargeMeteorCount => Mathf.Max(0, largeMeteorCount);

    // 기존 외부 코드 호환용. 이제 소형 운석 수를 반환합니다.
    public int MeteorCount => SmallMeteorCount;

    public int BasicEnemyCount => basicEnemyCount;
    public int ShotgunEnemyCount => shotgunEnemyCount;
    public int ChargingEnemyCount => chargingEnemyCount;
    public int MeleeChargerCount => Mathf.Max(0, meleeChargerCount);
    public bool UseSplitEliteCounts => useSplitEliteCounts;
    public int EliteMachineGunCount => useSplitEliteCounts ? Mathf.Max(0, eliteMachineGunCount) : 0;
    public int EliteShotgunCount => useSplitEliteCounts
        ? Mathf.Max(0, eliteShotgunCount)
        : Mathf.Max(0, eliteEnemyCount);
    public int EliteChargingCount => useSplitEliteCounts ? Mathf.Max(0, eliteChargingCount) : 0;
    public int EliteEnemyCount => EliteMachineGunCount + EliteShotgunCount + EliteChargingCount;

    public bool EnableEnemyRoles => enableEnemyRoles;
    public int DefenderBasicCount => Mathf.Max(0, defenderBasicCount);
    public int DefenderShotgunCount => Mathf.Max(0, defenderShotgunCount);
    public int RivalHarvesterCount => Mathf.Max(0, rivalHarvesterCount);
    public int ScavengerCount => Mathf.Max(0, scavengerCount);

    public float RoleStartGraceSeconds => Mathf.Max(0f, roleStartGraceSeconds);
    public float RoleStartMovementUnlockDistance => Mathf.Max(0f, roleStartMovementUnlockDistance);
    public float RolePreviewDistance => Mathf.Max(0.1f, rolePreviewDistance);
    public float RoleActiveDistance => Mathf.Clamp(roleActiveDistance, 0.1f, RolePreviewDistance);
    public float RoleActiveHoldSeconds => Mathf.Max(0f, roleActiveHoldSeconds);
    public float RoleRadarPreviewDuration => Mathf.Max(0f, roleRadarPreviewDuration);
    public int MaxConcurrentRivalActions => Mathf.Max(1, maxConcurrentRivalActions);
    public int MaxConcurrentScavengerActions => Mathf.Max(1, maxConcurrentScavengerActions);

    public float DeepZoneEnemyHpMultiplier => deepZoneEnemyHpMultiplier;
    public float DeepZoneEnemyDamageMultiplier => deepZoneEnemyDamageMultiplier;
    public float DeepZoneScrapMultiplier => deepZoneScrapMultiplier;
    public int DeepZoneAdditionalCoreReward => deepZoneAdditionalCoreReward;

    public float GetEnemyHpMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => Mathf.Max(0.01f, deepZoneEnemyHpMultiplier),
            ExpeditionDepth.DeepZone2 => Mathf.Max(0.01f, region3EnemyHpMultiplier),
            ExpeditionDepth.FinalNetwork => Mathf.Max(0.01f, finalNetworkEnemyHpMultiplier),
            _ => 1f
        };
    }

    public float GetEnemyDamageMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => Mathf.Max(0.01f, deepZoneEnemyDamageMultiplier),
            ExpeditionDepth.DeepZone2 => Mathf.Max(0.01f, region3EnemyDamageMultiplier),
            ExpeditionDepth.FinalNetwork => Mathf.Max(0.01f, finalNetworkEnemyDamageMultiplier),
            _ => 1f
        };
    }

    public float GetScrapMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => Mathf.Max(0f, deepZoneScrapMultiplier),
            ExpeditionDepth.DeepZone2 => Mathf.Max(0f, region3ScrapMultiplier),
            ExpeditionDepth.FinalNetwork => Mathf.Max(0f, finalNetworkScrapMultiplier),
            _ => 1f
        };
    }

    public int GetCoreShardReward(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1,
            ExpeditionDepth.DeepZone1 => 1 + Mathf.Max(0, deepZoneAdditionalCoreReward),
            ExpeditionDepth.DeepZone2 => Mathf.Max(0, region3CoreReward),
            ExpeditionDepth.FinalNetwork => Mathf.Max(0, finalNetworkCoreReward),
            _ => 1
        };
    }

    private Vector2 SanitizeMapSize(Vector2 value, Vector2 fallback)
    {
        if (value.x <= 0f || value.y <= 0f)
        {
            value = fallback;
        }

        return new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        mapSize.x = Mathf.Max(1f, mapSize.x);
        mapSize.y = Mathf.Max(1f, mapSize.y);
        region1MapSize = SanitizeMapSize(region1MapSize, new Vector2(120f, 120f));
        region2MapSize = SanitizeMapSize(region2MapSize, new Vector2(128f, 128f));
        region3MapSize = SanitizeMapSize(region3MapSize, new Vector2(116f, 116f));
        finalNetworkMapSize = SanitizeMapSize(finalNetworkMapSize, new Vector2(96f, 96f));
        startSafeRadius = Mathf.Max(0f, startSafeRadius);
        importantPointMinDistance = Mathf.Max(0f, importantPointMinDistance);
        salvagePoiCount = Mathf.Max(0, salvagePoiCount);
        highValuePoiCount = Mathf.Max(0, highValuePoiCount);
        poiMinSpacing = Mathf.Max(0f, poiMinSpacing);
        salvagePoiRadius = Mathf.Max(1f, salvagePoiRadius);
        highValuePoiRadius = Mathf.Max(1f, highValuePoiRadius);
        clusteredHarvestRatio = Mathf.Clamp01(clusteredHarvestRatio);
        clusteredEnemyRatio = Mathf.Clamp01(clusteredEnemyRatio);
        salvageDressingCount = Mathf.Max(0, salvageDressingCount);
        highValueDressingCount = Mathf.Max(0, highValueDressingCount);
        transitDressingCount = Mathf.Max(0, transitDressingCount);
        poiDressingRadiusMultiplier = Mathf.Max(0.5f, poiDressingRadiusMultiplier);
        fieldBaseCount = Mathf.Max(0, fieldBaseCount);
        shopCount = Mathf.Max(0, shopCount);
        eventCount = Mathf.Max(0, eventCount);
        coreCount = Mathf.Max(0, coreCount);
        fieldNpcCount = Mathf.Max(0, fieldNpcCount);
        basicEnemyCount = Mathf.Max(0, basicEnemyCount);
        shotgunEnemyCount = Mathf.Max(0, shotgunEnemyCount);
        chargingEnemyCount = Mathf.Max(0, chargingEnemyCount);
        meleeChargerCount = Mathf.Max(0, meleeChargerCount);
        eliteEnemyCount = Mathf.Max(0, eliteEnemyCount);
        eliteMachineGunCount = Mathf.Max(0, eliteMachineGunCount);
        eliteShotgunCount = Mathf.Max(0, eliteShotgunCount);
        eliteChargingCount = Mathf.Max(0, eliteChargingCount);
        boundaryWarningDistance = Mathf.Max(0f, boundaryWarningDistance);
        boundaryResistanceDistance = Mathf.Max(0.05f, boundaryResistanceDistance);
        boundaryMaxOutwardSpeedReduction = Mathf.Clamp(boundaryMaxOutwardSpeedReduction, 0f, 0.95f);
        boundaryInwardPushSpeed = Mathf.Max(0f, boundaryInwardPushSpeed);
        boundaryHardInset = Mathf.Max(0f, boundaryHardInset);
    }
#endif

}
