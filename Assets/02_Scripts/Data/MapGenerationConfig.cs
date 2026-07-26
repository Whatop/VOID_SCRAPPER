using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Map/Map Generation Config")]
public class MapGenerationConfig : ScriptableObject
{
    [Header("Map Size")]
    [SerializeField] private Vector2 mapSize = new Vector2(80f, 80f);

    [Header("Placement Rules")]
    [SerializeField] private float startSafeRadius = 10f;
    [SerializeField] private float importantPointMinDistance = 20f;

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
    [SerializeField] private int shopCount = 2;
    [SerializeField] private int eventCount = 4;
    [SerializeField] private int coreCount = 1;
    [SerializeField] private int fieldNpcCount = 2;

    [Header("Objects")]
    [SerializeField] private int highValueWreckCount = 3;
    [SerializeField] private int supplyContainerCount = 16;
    [SerializeField] private int destroyedHullCount = 8;
    [SerializeField] private int meteorCount = 24;

    [Header("Enemies")]
    [SerializeField] private int basicEnemyCount = 20;
    [SerializeField] private int shotgunEnemyCount = 5;
    [SerializeField] private int chargingEnemyCount = 4;
    [SerializeField] private int eliteEnemyCount = 2;

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

    [Header("Deep Zone Modifier")]
    [SerializeField] private float deepZoneEnemyHpMultiplier = 1.2f;
    [SerializeField] private float deepZoneEnemyDamageMultiplier = 1.2f;
    [SerializeField] private float deepZoneScrapMultiplier = 1.3f;
    [SerializeField] private int deepZoneAdditionalCoreReward = 1;

    public Vector2 MapSize => mapSize;

    public float StartSafeRadius => startSafeRadius;
    public float ImportantPointMinDistance => importantPointMinDistance;

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

    public int ShopCount => shopCount;
    public int EventCount => eventCount;
    public int CoreCount => coreCount;
    public int FieldNpcCount => Mathf.Max(0, fieldNpcCount);

    public int HighValueWreckCount => highValueWreckCount;
    public int SupplyContainerCount => supplyContainerCount;
    public int DestroyedHullCount => destroyedHullCount;
    public int MeteorCount => meteorCount;

    public int BasicEnemyCount => basicEnemyCount;
    public int ShotgunEnemyCount => shotgunEnemyCount;
    public int ChargingEnemyCount => chargingEnemyCount;
    public int EliteEnemyCount => eliteEnemyCount;

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
#if UNITY_EDITOR
    private void OnValidate()
    {
        mapSize.x = Mathf.Max(1f, mapSize.x);
        mapSize.y = Mathf.Max(1f, mapSize.y);
        startSafeRadius = Mathf.Max(0f, startSafeRadius);
        importantPointMinDistance = Mathf.Max(0f, importantPointMinDistance);
        fieldNpcCount = Mathf.Max(0, fieldNpcCount);
        boundaryWarningDistance = Mathf.Max(0f, boundaryWarningDistance);
        boundaryResistanceDistance = Mathf.Max(0.05f, boundaryResistanceDistance);
        boundaryMaxOutwardSpeedReduction = Mathf.Clamp(boundaryMaxOutwardSpeedReduction, 0f, 0.95f);
        boundaryInwardPushSpeed = Mathf.Max(0f, boundaryInwardPushSpeed);
        boundaryHardInset = Mathf.Max(0f, boundaryHardInset);
    }
#endif

}
