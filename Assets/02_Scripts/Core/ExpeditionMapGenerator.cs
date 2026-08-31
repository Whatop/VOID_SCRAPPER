using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

public class ExpeditionMapGenerator : MonoBehaviour
{
    public readonly struct Region2BossCorridorData
    {
        public float CenterX { get; }
        public float TopY { get; }
        public float BottomY { get; }
        public float HalfWidth { get; }
        public Bounds CorridorBounds { get; }
        public Bounds ReservedBounds { get; }
        public Vector2 TerminalPoint => new Vector2(CenterX, BottomY);

        public Region2BossCorridorData(
            float centerX,
            float topY,
            float bottomY,
            float halfWidth,
            Bounds corridorBounds,
            Bounds reservedBounds)
        {
            CenterX = centerX;
            TopY = topY;
            BottomY = bottomY;
            HalfWidth = halfWidth;
            CorridorBounds = corridorBounds;
            ReservedBounds = reservedBounds;
        }
    }

    public static event Action<ExpeditionMapGenerator> AnyMapGenerated;
    public event Action MapGenerated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        AnyMapGenerated = null;
    }
    private enum MapSpawnCategory
    {
        None,
        HighValueWreck,
        SupplyContainer,
        DestroyedHull,
        Meteor,
        LargeMeteor,
        SpecialActiveContainer,
        SpecialPassiveContainer
    }

    private enum PoiType
    {
        SalvageField,
        HighValueSalvage,
        FieldBase,
        Shop,
        Event,
        Core
    }

    private enum EnemyPoiPreference
    {
        AnySalvage,
        HighValueOnly
    }

    private enum Region3StartSector
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    private enum Region3ReflectorLayoutPattern
    {
        None = -1,
        OuterRing,
        BrokenRing,
        DiagonalLanes,
        SplitArcs,
        OuterFrameInnerPair,
        CrossDiamond,
        CompactPerimeterFallback
    }

    private const int Region3ReflectorLayoutPatternCount = 6;
    private const int Region3ReflectorCount = 12;
    private const int Region3CompactFallbackCandidateCount = 16;
    private const int Region3RequiredQuadrantCount = 4;
    private const float Region3MinimumNormalizedSpan = 0.52f;
    private const float Region3CompactFallbackScale = 0.68f;

    private sealed class PoiAnchor
    {
        public PoiType Type;
        public Vector2 Center;
        public float Radius;
        public bool IsLowRisk;
        public float ThreatCapacity;
        public float AssignedThreat;
        public int AssignedHarvest;
    }

    [Header("Config")]
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearPreviousGeneratedObjects = true;

    [Header("Region 2 Boss Corridor Runtime")]
    [SerializeField] private SalvageDevourerCorridorController region2BossCorridorRuntimePrefab;

    [Header("Region 3 Boss Foundation")]
    [SerializeField] private PhaseGatekeeperBossController region3BossEncounterPrefab;
    [SerializeField] private PhaseReflectorPlate region3ReflectorPlatePrefab;
    [SerializeField, Min(1f)] private float region3ReflectorPlateLength = 4f;
    [SerializeField, Min(4f)] private float region3BossReservationSize = 16f;

    [Header("Region 3 Boss-Focused Generation")]
    [SerializeField] private Vector2 region3BossFocusedMapSize = new Vector2(80f, 80f);
    [SerializeField, Range(0, 4)] private int region3HighValueWreckCount = 1;
    [SerializeField, Range(0, 8)] private int region3SupplyContainerCount = 2;
    [SerializeField, Range(0, 8)] private int region3DestroyedHullCount = 2;
    [SerializeField, Range(0, 16)] private int region3SmallMeteorCount = 6;
    [SerializeField, Range(0, 24)] private int region3EnvironmentDressingCount = 10;

    [Header("Region 3 Player Start")]
    [SerializeField] private Vector2 region3StartEdgeInsetRange = new Vector2(8f, 12f);
    [SerializeField, Range(0.1f, 0.75f)] private float region3CardinalSectorHalfSpan = 0.45f;
    [SerializeField, Range(1, 24)] private int region3StartCandidateAttempts = 12;
    [SerializeField, Min(0.1f)] private float region3StartBlockingRadius = 1f;

    [Header("Region 3 Reflector Layout")]
    [SerializeField, Range(0f, 2f)] private float region3ReflectorPositionJitter = 1f;
    [SerializeField, Range(0f, 12f)] private float region3ReflectorRotationJitter = 10f;
    [SerializeField, Range(0f, 20f)] private float region3ReflectorLayoutRotationVariation = 12f;
    [FormerlySerializedAs("region3ReflectorStartClearancePadding")]
    [SerializeField, Range(8f, 12f)] private float region3ReflectorPlayerClearance = 8f;
    [SerializeField, Range(6f, 10f)] private float region3ReflectorBossClearance = 8f;
    [SerializeField, Range(4.5f, 6f)] private float region3ReflectorMinimumSpacing = 5.5f;
    [SerializeField, Range(1, 32)] private int region3ReflectorLayoutAttempts = 24;
    [SerializeField, Range(16f, 24f)] private float region3BossMinimumStartClearance = 18f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Region 3 Development")]
    [SerializeField, Range(-1, Region3ReflectorLayoutPatternCount - 1)]
    private int debugForcedRegion3ReflectorTemplate = -1;
#endif

    [Header("Root")]
    [SerializeField] private Transform generatedRoot;

    [Header("Background Optional")]
    [Tooltip("배경은 맵 크기를 키워서 해결하지 않고, 배경 생성기 쪽에서 따로 커버합니다.")]
    [SerializeField] private SpaceBackgroundGenerator2D backgroundGenerator;

    [SerializeField] private bool syncBackgroundToMapSize = true;
    [SerializeField] private bool regenerateBackgroundOnGenerate = true;

    [Header("Player Start")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform startPoint;
    [SerializeField] private Vector2 fallbackStartPosition = new Vector2(-32f, 0f);
    [SerializeField] private bool movePlayerToStart = true;
    [SerializeField, Min(0f)] private float cameraSafeAdditionalPadding = 1f;

    [Header("Important Object Prefabs")]
    [Tooltip("Shop Zone Prefabs가 비어 있을 때 사용하는 기존 상점/상점 구역 단일 프리팹입니다.")]
    [SerializeField] private GameObject shopPrefab;
    [SerializeField] private GameObject[] eventPrefabs;
    [SerializeField] private GameObject corePrefab;

    [Header("Generated Field Base / Shop Zones")]
    [Tooltip("적 기지 완성 프리팹 후보입니다. FieldBaseController가 포함되어 있어야 합니다.")]
    [SerializeField] private GameObject[] fieldBasePrefabs;
    [Tooltip("배열이 비어 있을 때 사용하는 적 기지 단일 프리팹입니다.")]
    [SerializeField] private GameObject fieldBasePrefab;
    [Tooltip("상점 본체, 안전 구역, 포탑 포인트, 전력 장치가 포함된 완성 상점 구역 프리팹 후보입니다.")]
    [SerializeField] private GameObject[] shopZonePrefabs;

    [Header("Large Zone Placement")]
    [Tooltip("적 기지 전체가 차지하는 예약 크기입니다. 기지 외벽보다 약간 크게 설정하세요.")]
    [SerializeField] private Vector2 fieldBaseReservationSize = new Vector2(28f, 28f);
    [Tooltip("상점 안전 구역까지 포함한 예약 크기입니다.")]
    [SerializeField] private Vector2 shopZoneReservationSize = new Vector2(28f, 28f);
    [Min(0f)]
    [SerializeField] private float largeZoneSpacing = 4f;
    [Min(0f)]
    [SerializeField] private float shopPortalArrivalClearanceRadius = 2f;
    [SerializeField] private bool rotateLargeZonesByRightAngles;
    [SerializeField] private bool reserveBossArenaFromOtherSpawns = true;
    [SerializeField] private bool configureBasePortalsToNearestShop = true;
    [SerializeField] private bool assignRoleEnemiesToNearestGeneratedBase = true;

    [Header("Region 2 / 3 Mini Trader")]
    [SerializeField] private GameObject miniTraderPrefab;
    [SerializeField] private Vector2 miniTraderReservationSize = new Vector2(4f, 4f);
    [SerializeField, Min(0f)] private float miniTraderReservationPadding = 1f;
    [SerializeField, Min(0f)] private float miniTraderImportantClearance = 10f;
    [SerializeField, Min(8f)] private float region3MiniTraderArenaClearance = 24f;
    [SerializeField, Range(1, 3)] private int miniTraderTraitOfferCount = 2;
    [SerializeField, Range(1, 2)] private int miniTraderReinforcementOfferCount = 1;
    [SerializeField] private Color miniTraderMarkerColor = new Color(0.35f, 1f, 0.75f, 1f);

    [Header("Enemy Projectile World Damage")]
    [SerializeField] private bool enemyProjectilesDamageSupplyContainers = true;
    [SerializeField] private bool enemyProjectilesDamageHighValueWrecks = true;
    [SerializeField] private bool enemyProjectilesDamageDestroyedHulls;
    [SerializeField] private bool enemyProjectilesDamageSmallMeteors = true;

    [Header("Field NPC Objectives")]
    [SerializeField] private GameObject[] fieldNpcPrefabs;
    [Tooltip("모든 필드 NPC 구조 보상에 사용할 공용 보상 캡슐 프리팹입니다. 비워두면 NPC 프리팹의 개별 설정을 사용합니다.")]
    [SerializeField] private GameObject fieldNpcRewardCapsulePrefab;
    [SerializeField] private bool createFallbackFieldNpcWhenPrefabMissing = true;
    [SerializeField] private bool fallbackFieldNpcsRequireRescue = true;
    [SerializeField] private FieldNpcServiceType[] fallbackNpcServiceOrder =
    {
        FieldNpcServiceType.Technician,
        FieldNpcServiceType.Converter,
        FieldNpcServiceType.BlackMarket,
        FieldNpcServiceType.RescueContact
    };
    [SerializeField] private Sprite fieldNpcRadarMarkerSprite;
    [SerializeField] private Color fallbackFieldNpcColor = new Color(0.25f, 1f, 0.75f, 1f);

    [Header("Reward Choice Runtime")]
    [SerializeField] private RunLevelTraitSelectionUI rewardChoiceUI;

    [Header("Special Container Prefabs")]
    [Tooltip("한 해역에 1개만 배치하는 액티브 장비 특수 컨테이너 후보입니다.")]
    [SerializeField] private GameObject[] specialActiveContainerPrefabs;

    [Tooltip("특성/패시브 특수 컨테이너 후보입니다. 개수는 아래 Count로 조절합니다.")]
    [SerializeField] private GameObject[] specialPassiveContainerPrefabs;

    [Tooltip("배열이 비었을 때만 쓰는 액티브 특수 컨테이너 단일 프리팹입니다.")]
    [SerializeField] private GameObject specialActiveContainerPrefab;

    [Tooltip("배열이 비었을 때만 쓰는 패시브 특수 컨테이너 단일 프리팹입니다.")]
    [SerializeField] private GameObject specialPassiveContainerPrefab;

    [Header("Special Container Count")]
    [SerializeField] private int specialActiveContainerCount = 1;
    [SerializeField] private int specialPassiveContainerCount = 2;

    [Header("Harvest Object Prefab Arrays")]
    [Tooltip("고가치 잔해 프리팹 후보. 여러 개를 넣으면 매 스폰마다 랜덤 선택합니다.")]
    [SerializeField] private GameObject[] highValueWreckPrefabs;

    [Tooltip("보급 컨테이너 프리팹 후보. 여러 개를 넣으면 매 스폰마다 랜덤 선택합니다.")]
    [SerializeField] private GameObject[] supplyContainerPrefabs;

    [Tooltip("파괴된 선체 프리팹 후보. 여러 개를 넣으면 매 스폰마다 랜덤 선택합니다.")]
    [SerializeField] private GameObject[] destroyedHullPrefabs;

    [Tooltip("소형 운석 프리팹 후보. 이동 가능한 작은 운석만 넣으세요.")]
    [SerializeField] private GameObject[] meteorPrefabs;

    [Tooltip("대형 운석 프리팹 후보. 벽과 LOS를 막는 고정 지형 운석만 넣으세요.")]
    [SerializeField] private GameObject[] largeMeteorPrefabs;

    [Header("Legacy Single Prefab Fallback")]
    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject highValueWreckPrefab;

    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject supplyContainerPrefab;

    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject destroyedHullPrefab;

    [Tooltip("소형 운석 배열이 비어 있을 때 사용하는 단일 프리팹입니다.")]
    [SerializeField] private GameObject meteorPrefab;

    [Tooltip("대형 운석 배열이 비어 있을 때 사용하는 단일 프리팹입니다.")]
    [SerializeField] private GameObject largeMeteorPrefab;

    [Header("Enemy Definitions")]
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;
    [SerializeField] private EnemyDefinition chargingEnemyDefinition;
    [SerializeField] private EnemyDefinition meleeChargerDefinition;
    [SerializeField] private EnemyDefinition eliteMachineGunDefinition;
    [FormerlySerializedAs("eliteEnemyDefinition")]
    [SerializeField] private EnemyDefinition eliteShotgunDefinition;
    [SerializeField] private EnemyDefinition eliteChargingDefinition;

    [Header("Enemy Role Definitions")]
    [SerializeField] private GameObject defenderBasicPrefab;
    [SerializeField] private GameObject defenderShotgunPrefab;
    [Tooltip("경쟁 회수정 전용 기체/스탯 Definition. 비어 있으면 Basic Enemy Definition을 사용합니다.")]
    [SerializeField] private EnemyDefinition rivalHarvesterDefinition;
    [Tooltip("약탈자 전용 기체/스탯 Definition. 비어 있으면 Basic Enemy Definition을 사용합니다.")]
    [SerializeField] private EnemyDefinition scavengerDefinition;

    [Header("Enemy Role Placement")]
    [SerializeField] private bool useEnemyRoles = true;
    [SerializeField] private int fallbackDefenderBasicCount = 4;
    [SerializeField] private int fallbackDefenderShotgunCount = 2;
    [SerializeField] private int fallbackRivalHarvesterCount = 1;
    [SerializeField] private int fallbackScavengerCount = 1;
    [SerializeField] private float roleSpawnRadiusMin = 2.5f;
    [SerializeField] private float roleSpawnRadiusMax = 4.5f;

    [Header("Enemy Role Simulation Fallback")]
    [SerializeField] private float fallbackRoleStartGraceSeconds = 12f;
    [SerializeField] private float fallbackRoleStartMovementUnlockDistance = 8f;
    [SerializeField] private float fallbackRolePreviewDistance = 26f;
    [SerializeField] private float fallbackRoleActiveDistance = 18f;
    [SerializeField] private float fallbackRoleActiveHoldSeconds = 8f;
    [SerializeField] private float fallbackRoleRadarPreviewDuration = 10f;
    [SerializeField] private int fallbackMaxConcurrentRivalActions = 1;
    [SerializeField] private int fallbackMaxConcurrentScavengerActions = 1;

    [Header("Enemy Role Radar Sprite Optional")]
    [SerializeField] private Sprite defenderRoleMarkerSprite;
    [SerializeField] private Sprite rivalHarvesterRoleMarkerSprite;
    [SerializeField] private Sprite scavengerRoleMarkerSprite;

    [Header("Placement")]
    [SerializeField] private float edgePadding = 3f;
    [SerializeField] private float generalMinDistance = 1.5f;
    [SerializeField] private float enemyMinDistance = 1.2f;
    [SerializeField] private int maxBasicEnemiesInsideStartSafeRadius = 2;
    [SerializeField] private int maxPlacementAttempts = 500;

    [Header("Harvest Scatter")]
    [Tooltip("켜면 수확 오브젝트가 완전 균등 분포가 아니라 느슨한 덩어리 형태로 섞입니다.")]
    [SerializeField] private bool useHarvestClusters = true;

    [Range(0f, 1f)]
    [SerializeField] private float harvestClusterChance = 0.38f;

    [SerializeField] private Vector2 harvestClusterRadiusRange = new Vector2(3f, 8f);
    [SerializeField] private int clusterCandidateAttempts = 8;

    [Header("Harvest Visual Randomization")]
    [SerializeField] private bool randomizeHarvestRotation = true;
    [SerializeField] private bool randomizeHarvestScale = true;

    [SerializeField] private Vector2 highValueWreckScaleRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 supplyContainerScaleRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private Vector2 destroyedHullScaleRange = new Vector2(0.85f, 1.25f);
    [Tooltip("소형 운석의 추가 랜덤 스케일입니다.")]
    [SerializeField] private Vector2 meteorScaleRange = new Vector2(0.7f, 1.05f);
    [Tooltip("대형 운석의 추가 랜덤 스케일입니다.")]
    [SerializeField] private Vector2 largeMeteorScaleRange = new Vector2(0.95f, 1.2f);

    [Header("Environment Dressing Visuals")]
    [SerializeField] private Sprite[] environmentDebrisSprites;
    [SerializeField] private Sprite[] environmentWreckSprites;
    [SerializeField] private string environmentDressingSortingLayerName = "Background";
    [SerializeField] private int environmentDressingSortingOrder = -12;

    [Header("Physics Block Check Optional")]
    [SerializeField] private bool useBlockedLayerCheck;
    [SerializeField] private LayerMask blockedLayer;
    [SerializeField] private float blockedCheckRadius = 0.5f;

    [Header("Deep Zone")]
    [SerializeField] private bool applyDeepZoneEnemyHpMultiplier = true;

    [Header("Sea Region")]
    [SerializeField] private bool applySeaRegionObjectCountModifiers = true;
    [SerializeField] private bool applySeaRegionEnemyCountModifiers = true;
    [SerializeField] private bool applySeaRegionEnemyHpMultiplier = true;
    [SerializeField] private SeaRegionType fallbackSeaRegion = SeaRegionType.DenseDebris;

    [Header("Debug")]
    [SerializeField] private bool logGenerationResult = true;
    [SerializeField] private bool drawMapBounds = true;

    private readonly List<Vector2> occupiedPositions = new List<Vector2>(256);
    private readonly List<Vector2> importantPositions = new List<Vector2>(16);
    private readonly List<Vector2> harvestClusterAnchors = new List<Vector2>(64);
    private readonly List<HarvestObjectHealth> spawnedHarvestObjects = new List<HarvestObjectHealth>(64);
    private readonly List<ExpeditionEventObject> spawnedEventObjects = new List<ExpeditionEventObject>(8);
    private readonly List<CoreObject> spawnedCoreObjects = new List<CoreObject>(2);
    private readonly List<HarvestObjectHealth> spawnedDefenderTargets = new List<HarvestObjectHealth>(16);
    private readonly List<Bounds> reservedPlacementBounds = new List<Bounds>(16);
    private readonly List<FieldBaseController> spawnedFieldBases = new List<FieldBaseController>(4);
    private readonly List<ShopStructure> spawnedShopStructures = new List<ShopStructure>(4);
    private readonly List<PoiAnchor> poiAnchors = new List<PoiAnchor>(16);
    private readonly List<PoiAnchor> salvagePoiAnchors = new List<PoiAnchor>(4);
    private readonly List<PoiAnchor> highValuePoiAnchors = new List<PoiAnchor>(4);
    private readonly List<Vector2> environmentDressingPositions = new List<Vector2>(160);

    private Vector2 mapSize = new Vector2(80f, 80f);
    private Vector2 startPosition;
    private int basicEnemiesInsideStartSafeRadius;
    private SeaRegionDefinition currentSeaRegion;
    private TraitCatalog runtimeTraitCatalog;
    private ReinforcementCatalog runtimeReinforcementCatalog;
    private static Sprite runtimeFallbackNpcSprite;
    private int clusteredHarvestPlaced;
    private int scatteredHarvestPlaced;
    private int clusteredEnemiesPlaced;
    private int roamingEnemiesPlaced;
    private bool poiPlacementFallbackUsed;
    private int salvageDressingPlaced;
    private int highValueDressingPlaced;
    private int transitDressingPlaced;
    private int otherPoiDressingPlaced;
    private int failedDressingPlacements;
    private bool hasRegion2CoreCandidate;
    private Vector2 region2CoreCandidatePosition;
    private bool hasRegion2CoreFinalPosition;
    private Vector2 region2CoreFinalPosition;
    private bool hasRegion2BossCorridor;
    private Region2BossCorridorData region2BossCorridor;
    private bool hasLargeZoneDebugCandidateBounds;
    private Bounds largeZoneDebugCandidateBounds;
    private SalvageDevourerCorridorController currentRegion2BossCorridorRuntime;
    private PhaseGatekeeperBossController currentRegion3BossEncounter;
    private MiniTrader currentMiniTrader;
    private Bounds currentMiniTraderReservationBounds;
    private Region3StartSector currentRegion3StartSector;
    private Region3ReflectorLayoutPattern currentRegion3ReflectorLayoutPattern =
        Region3ReflectorLayoutPattern.None;
    private readonly PhaseReflectorPlate[] currentRegion3Reflectors =
        new PhaseReflectorPlate[Region3ReflectorCount];
    private readonly Vector2[] currentRegion3ReflectorPositions =
        new Vector2[Region3ReflectorCount];
    private readonly Vector2[] region3CompactFallbackPositions =
        new Vector2[Region3CompactFallbackCandidateCount];
    private readonly float[] region3CompactFallbackRotations =
        new float[Region3CompactFallbackCandidateCount];
    private readonly bool[] region3CompactFallbackAvailable =
        new bool[Region3CompactFallbackCandidateCount];
    private int currentRegion3ReflectorCandidateCount;
    private int currentRegion3ReflectorSpawnedCount;
    private int currentRegion3ReflectorCount;
    private int currentRegion3ConfiguredReflectorCount;
    private bool currentRegion3EncounterConfigured;
    private Vector2 currentRegion3BossPosition;
    private bool currentRegion3FoundationFailed;
    private string currentRegion3LayoutAttemptDescription;

    public Bounds MapBounds { get; private set; }
    public Bounds CameraSafeBounds { get; private set; }
    public Bounds CorePlacementSafeBounds { get; private set; }
    public Vector2 StartPosition => startPosition;
    public SeaRegionDefinition CurrentSeaRegion => currentSeaRegion;
    public IReadOnlyList<HarvestObjectHealth> SpawnedHarvestObjects => spawnedHarvestObjects;
    public IReadOnlyList<ExpeditionEventObject> SpawnedEventObjects => spawnedEventObjects;
    public IReadOnlyList<CoreObject> SpawnedCoreObjects => spawnedCoreObjects;
    public IReadOnlyList<FieldBaseController> SpawnedFieldBases => spawnedFieldBases;
    public bool HasRegion2BossCorridor => hasRegion2BossCorridor;
    public Region2BossCorridorData CurrentRegion2BossCorridor => region2BossCorridor;
    public SalvageDevourerCorridorController CurrentRegion2BossCorridorRuntime =>
        currentRegion2BossCorridorRuntime;
    public PhaseGatekeeperBossController CurrentRegion3BossEncounter =>
        currentRegion3BossEncounter;
    public MiniTrader CurrentMiniTrader => currentMiniTrader;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TryPrepareCoreForDebug(out CoreObject core)
    {
        core = null;

        ExpeditionDepth depth = ResolveCurrentDepth();
        if (depth != ExpeditionDepth.Normal &&
            depth != ExpeditionDepth.DeepZone1 &&
            depth != ExpeditionDepth.FinalNetwork)
        {
            return false;
        }

        for (int i = 0; i < spawnedCoreObjects.Count; i++)
        {
            CoreObject candidate = spawnedCoreObjects[i];

            if (candidate == null)
            {
                continue;
            }

            core = candidate;
            break;
        }

        if (core == null)
        {
            int previousCount = spawnedCoreObjects.Count;
            PlaceCoreBatch(1);

            for (int i = spawnedCoreObjects.Count - 1; i >= previousCount; i--)
            {
                CoreObject candidate = spawnedCoreObjects[i];

                if (candidate != null && !candidate.IsActivated)
                {
                    core = candidate;
                    break;
                }
            }
        }

        if (core == null)
        {
            return false;
        }

        core.gameObject.SetActive(true);
        core.SetTrackingLocked(false);
        core.MarkCoreLocationDiscovered(false);
        return true;
    }
#endif

    private void Reset()
    {
        backgroundGenerator = FindFirstObjectByType<SpaceBackgroundGenerator2D>();
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        ResolveConfig();
        ResolveGeneratedRoot();
        ResolvePlayer();
        EnsureProgressionRuntimeSystems();

        if (clearPreviousGeneratedObjects)
        {
            ClearGeneratedObjects();
        }
        else if (currentRegion2BossCorridorRuntime != null)
        {
            currentRegion2BossCorridorRuntime.CleanupCorridorRuntime();

            if (Application.isPlaying)
            {
                Destroy(currentRegion2BossCorridorRuntime.gameObject);
            }
            else
            {
                DestroyImmediate(currentRegion2BossCorridorRuntime.gameObject);
            }

            currentRegion2BossCorridorRuntime = null;
        }

        occupiedPositions.Clear();
        importantPositions.Clear();
        harvestClusterAnchors.Clear();
        spawnedHarvestObjects.Clear();
        spawnedEventObjects.Clear();
        spawnedCoreObjects.Clear();
        spawnedDefenderTargets.Clear();
        reservedPlacementBounds.Clear();
        spawnedFieldBases.Clear();
        spawnedShopStructures.Clear();
        poiAnchors.Clear();
        salvagePoiAnchors.Clear();
        highValuePoiAnchors.Clear();
        environmentDressingPositions.Clear();

        basicEnemiesInsideStartSafeRadius = 0;
        clusteredHarvestPlaced = 0;
        scatteredHarvestPlaced = 0;
        clusteredEnemiesPlaced = 0;
        roamingEnemiesPlaced = 0;
        poiPlacementFallbackUsed = false;
        salvageDressingPlaced = 0;
        highValueDressingPlaced = 0;
        transitDressingPlaced = 0;
        otherPoiDressingPlaced = 0;
        failedDressingPlacements = 0;
        hasRegion2CoreCandidate = false;
        hasRegion2CoreFinalPosition = false;
        hasRegion2BossCorridor = false;
        region2BossCorridor = default;
        hasLargeZoneDebugCandidateBounds = false;
        currentRegion2BossCorridorRuntime = null;
        currentRegion3BossEncounter = null;
        currentMiniTrader = null;
        currentMiniTraderReservationBounds = default;
        currentRegion3ReflectorLayoutPattern = Region3ReflectorLayoutPattern.None;
        Array.Clear(currentRegion3Reflectors, 0, currentRegion3Reflectors.Length);
        Array.Clear(
            currentRegion3ReflectorPositions,
            0,
            currentRegion3ReflectorPositions.Length
        );
        currentRegion3ReflectorCandidateCount = 0;
        currentRegion3ReflectorSpawnedCount = 0;
        currentRegion3ReflectorCount = 0;
        currentRegion3ConfiguredReflectorCount = 0;
        currentRegion3EncounterConfigured = false;
        currentRegion3BossPosition = Vector2.zero;
        currentRegion3FoundationFailed = false;
        currentRegion3LayoutAttemptDescription = string.Empty;
        startPosition = ResolveStartPosition();

        if (movePlayerToStart && player != null)
        {
            player.position = startPosition;
        }

        occupiedPositions.Add(startPosition);

        if (IsRegion3PhaseGatekeeperMap())
        {
            ReserveRegion3PlayerStart();
        }

        SyncBackgroundGenerator();

        PlaceImportantObjects();
        BuildPoiLayout();
        PlaceHarvestObjects();
        PlaceEnemies();
        PlaceEnvironmentDressing();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ValidateRegion2BossCorridorGeneration();
        ValidateRegion3BossFoundationGeneration();
        ValidateMiniTraderGeneration();
#endif

        if (logGenerationResult)
        {
            string seaText = currentSeaRegion != null ? currentSeaRegion.DisplayName : "None";

            Debug.Log(
                $"Expedition map generated. Size: {mapSize}, Start: {startPosition}, " +
                $"SeaRegion: {seaText}, Spawned Positions: {occupiedPositions.Count}",
                this
            );

            LogPoiGenerationSummary();
        }

        MapGenerated?.Invoke();
        AnyMapGenerated?.Invoke(this);
    }

    [ContextMenu("Clear Generated Objects")]
    public void ClearGeneratedObjects()
    {
        if (currentRegion2BossCorridorRuntime != null)
        {
            currentRegion2BossCorridorRuntime.CleanupCorridorRuntime();
            currentRegion2BossCorridorRuntime = null;
        }

        if (generatedRoot == null)
        {
            return;
        }

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = generatedRoot.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ResolveConfig()
    {
        currentSeaRegion = ResolveCurrentSeaRegion();

        ExpeditionDepth depth = ResolveCurrentDepth();

        if (config != null)
        {
            mapSize = config.GetMapSize(depth);
        }
        else
        {
            mapSize = CampaignProgressionCatalog.GetDefaultMapSize(depth);
        }

        if (mapSize.x <= 0f || mapSize.y <= 0f)
        {
            mapSize = CampaignProgressionCatalog.GetDefaultMapSize(depth);
        }

        if (IsRegion3PhaseGatekeeperMap())
        {
            mapSize = new Vector2(
                Mathf.Max(64f, region3BossFocusedMapSize.x),
                Mathf.Max(64f, region3BossFocusedMapSize.y)
            );
        }

        MapBounds = new Bounds(
            Vector3.zero,
            new Vector3(mapSize.x, mapSize.y, 0f)
        );

        CameraSafeBounds = ResolveCameraSafeBounds(mapSize);
        CorePlacementSafeBounds = ResolveCorePlacementSafeBounds(mapSize);
    }

    private SeaRegionDefinition ResolveCurrentSeaRegion()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return SeaRegionCatalog.Get(RunManager.Instance.CurrentRun.SeaRegionType);
        }

        return SeaRegionCatalog.Get(fallbackSeaRegion);
    }

    private void ResolveGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Generated_Map");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
        {
            player = found.transform;
        }
    }

    private void SyncBackgroundGenerator()
    {
        if (!syncBackgroundToMapSize)
        {
            return;
        }

        if (backgroundGenerator == null)
        {
            backgroundGenerator = FindFirstObjectByType<SpaceBackgroundGenerator2D>();
        }

        if (backgroundGenerator == null)
        {
            return;
        }

        backgroundGenerator.SetMapArea(Vector2.zero, mapSize);
        backgroundGenerator.SetTargetCamera(Camera.main);

        if (regenerateBackgroundOnGenerate)
        {
            backgroundGenerator.GenerateBackground();
        }
    }
    private Vector2 ResolveStartPosition()
    {
        if (IsRegion3PhaseGatekeeperMap())
        {
            return ResolveRegion3StartPosition();
        }

        if (startPoint != null)
        {
            return ClampToBounds(startPoint.position, CameraSafeBounds);
        }

        return ClampToBounds(fallbackStartPosition, CameraSafeBounds);
    }

    private Vector2 ResolveRegion3StartPosition()
    {
        currentRegion3StartSector = (Region3StartSector)UnityEngine.Random.Range(0, 8);
        int attempts = Mathf.Clamp(region3StartCandidateAttempts, 1, 24);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 candidate = CreateRegion3StartCandidate(currentRegion3StartSector, false);
            if (IsRegion3StartCandidateValid(candidate))
            {
                return candidate;
            }
        }

        Vector2 fallback = CreateRegion3StartCandidate(currentRegion3StartSector, true);
        if (IsRegion3StartCandidateValid(fallback))
        {
            return fallback;
        }

        Debug.LogWarning(
            $"Region-3 start sector {currentRegion3StartSector} had no clear candidate after " +
            $"{attempts} bounded attempts. The sector-center fallback is still inside CameraSafeBounds.",
            this
        );
        return fallback;
    }

    private Vector2 CreateRegion3StartCandidate(Region3StartSector sector, bool centered)
    {
        Vector3 boundsMin = CameraSafeBounds.min;
        Vector3 boundsMax = CameraSafeBounds.max;
        Vector2 center = CameraSafeBounds.center;
        Vector2 extents = CameraSafeBounds.extents;
        float minimumInset = Mathf.Max(0f, Mathf.Min(
            region3StartEdgeInsetRange.x,
            region3StartEdgeInsetRange.y
        ));
        float maximumInset = Mathf.Max(minimumInset, Mathf.Max(
            region3StartEdgeInsetRange.x,
            region3StartEdgeInsetRange.y
        ));
        float insetX = centered
            ? (minimumInset + maximumInset) * 0.5f
            : UnityEngine.Random.Range(minimumInset, maximumInset);
        float insetY = centered
            ? (minimumInset + maximumInset) * 0.5f
            : UnityEngine.Random.Range(minimumInset, maximumInset);
        float lateralX = centered
            ? 0f
            : UnityEngine.Random.Range(-extents.x, extents.x) * region3CardinalSectorHalfSpan;
        float lateralY = centered
            ? 0f
            : UnityEngine.Random.Range(-extents.y, extents.y) * region3CardinalSectorHalfSpan;
        Vector2 candidate;

        switch (sector)
        {
            case Region3StartSector.North:
                candidate = new Vector2(center.x + lateralX, boundsMax.y - insetY);
                break;
            case Region3StartSector.NorthEast:
                candidate = new Vector2(boundsMax.x - insetX, boundsMax.y - insetY);
                break;
            case Region3StartSector.East:
                candidate = new Vector2(boundsMax.x - insetX, center.y + lateralY);
                break;
            case Region3StartSector.SouthEast:
                candidate = new Vector2(boundsMax.x - insetX, boundsMin.y + insetY);
                break;
            case Region3StartSector.South:
                candidate = new Vector2(center.x + lateralX, boundsMin.y + insetY);
                break;
            case Region3StartSector.SouthWest:
                candidate = new Vector2(boundsMin.x + insetX, boundsMin.y + insetY);
                break;
            case Region3StartSector.West:
                candidate = new Vector2(boundsMin.x + insetX, center.y + lateralY);
                break;
            default:
                candidate = new Vector2(boundsMin.x + insetX, boundsMax.y - insetY);
                break;
        }

        return ClampToInsetBounds(
            candidate,
            CameraSafeBounds,
            Mathf.Max(0.1f, region3StartBlockingRadius)
        );
    }

    private bool IsRegion3StartCandidateValid(Vector2 candidate)
    {
        if (!CameraSafeBounds.Contains(candidate))
        {
            return false;
        }

        if (IsPointBlockedByReservedPlacement(
                candidate,
                Mathf.Max(0.1f, region3StartBlockingRadius)))
        {
            return false;
        }

        return !useBlockedLayerCheck ||
               Physics2D.OverlapCircle(
                   candidate,
                   Mathf.Max(0.1f, region3StartBlockingRadius),
                   blockedLayer
               ) == null;
    }

    private void ReserveRegion3PlayerStart()
    {
        float diameter = Mathf.Max(0.2f, region3StartBlockingRadius * 2f);
        ReservePlacementBounds(new Bounds(
            startPosition,
            new Vector3(diameter, diameter, 0f)
        ));
    }

    private Bounds ResolveCameraSafeBounds(Vector2 sourceMapSize)
    {
        float cameraHalfHeight = config != null
            ? config.FallbackBossCameraBaseOrthographicSize
            : 4.2f;
        float cameraAspect = config != null
            ? config.FallbackBossCameraAspect
            : 1.7777778f;

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.orthographic)
        {
            cameraHalfHeight = Mathf.Max(0.1f, mainCamera.orthographicSize);
            cameraAspect = Mathf.Max(0.1f, mainCamera.aspect);
        }

        CameraZoomController2D zoomController = FindFirstObjectByType<CameraZoomController2D>();
        if (zoomController != null)
        {
            cameraHalfHeight = Mathf.Max(cameraHalfHeight, zoomController.BaseOrthographicSize);
        }

        float warningDistance = config != null ? config.BoundaryWarningDistance : 7f;
        float safetyPadding = warningDistance + Mathf.Max(0f, cameraSafeAdditionalPadding);
        Vector2 mapHalfSize = sourceMapSize * 0.5f;
        Vector2 cameraHalfExtents = new Vector2(
            cameraHalfHeight * cameraAspect,
            cameraHalfHeight
        );
        Vector2 safeHalfSize = new Vector2(
            Mathf.Max(0.1f, mapHalfSize.x - cameraHalfExtents.x - safetyPadding),
            Mathf.Max(0.1f, mapHalfSize.y - cameraHalfExtents.y - safetyPadding)
        );

        return new Bounds(
            MapBounds.center,
            new Vector3(safeHalfSize.x * 2f, safeHalfSize.y * 2f, 0f)
        );
    }

    private void PlaceImportantObjects()
    {
        ExpeditionDepth depth = ResolveCurrentDepth();
        int fieldBaseCount = config != null ? config.FieldBaseCount : 2;
        int shopCount = config != null ? config.ShopCount : 2;
        int eventCount = config != null ? config.EventCount : 2;
        int coreCount = config != null ? config.CoreCount : 1;

        if (depth == ExpeditionDepth.DeepZone1 || depth == ExpeditionDepth.DeepZone2)
        {
            shopCount = 0;
        }

        if (IsRegion3PhaseGatekeeperMap())
        {
            fieldBaseCount = 0;
            shopCount = 0;
            eventCount = 0;
            coreCount = 0;
            PlaceRegion3BossFoundation();
        }

        // 코어 전장, 적 기지, 상점 구역 순으로 큰 공간을 먼저 예약한다.
        PlaceCoreBatch(coreCount);
        PlaceFieldBaseBatch(fieldBaseCount);
        PlaceShopZoneBatch(shopCount);
        ConfigureGeneratedBasePortalDestinations();

        if (depth == ExpeditionDepth.DeepZone1)
        {
            PlaceMiniTrader();
        }

        PlacePrefabBatch(
            eventPrefabs,
            null,
            eventCount,
            true,
            true,
            generalMinDistance,
            "Event",
            MapSpawnCategory.None
        );

        int fieldNpcCount = IsRegion3PhaseGatekeeperMap()
            ? 0
            : config != null ? config.FieldNpcCount : 2;
        PlaceFieldNpcBatch(fieldNpcCount);
    }

    private void PlaceRegion3BossFoundation()
    {
        if (region3BossEncounterPrefab == null || region3ReflectorPlatePrefab == null)
        {
            FailRegion3BossFoundation(
                "Region-3 Boss foundation requires both the Phase Gatekeeper Boss " +
                "and reflector plate prefabs.",
                null,
                null
            );
            return;
        }

        Vector2 bossPosition = ResolveRegion3BossPosition();
        float minimumBossDistance = ResolveRegion3BossMinimumStartClearance();
        if ((bossPosition - startPosition).sqrMagnitude < minimumBossDistance * minimumBossDistance)
        {
            FailRegion3BossFoundation(
                $"Region-3 Boss placement could not preserve the compact-map " +
                $"{minimumBossDistance:0.##}-unit Player separation. " +
                $"StartSector={currentRegion3StartSector}, Player={startPosition}, " +
                $"Boss={bossPosition}, Bounds={CameraSafeBounds}.",
                null,
                null
            );
            return;
        }

        Region3ReflectorLayoutPattern requestedPattern =
            SelectRegion3ReflectorLayoutPattern();
        Region3ReflectorLayoutPattern actualPattern = requestedPattern;
        int plateCount = Region3ReflectorCount;
        PhaseReflectorPlate[] plates = new PhaseReflectorPlate[plateCount];
        Vector2[] platePositions = new Vector2[plateCount];
        float[] plateRotations = new float[plateCount];
        string proceduralFailureReason = string.Empty;
        bool proceduralLayoutBuilt = TryBuildRegion3ReflectorLayout(
            requestedPattern,
            bossPosition,
            platePositions,
            plateRotations,
            plateCount
        );
        if (!proceduralLayoutBuilt)
        {
            proceduralFailureReason =
                $"bounded template attempts exhausted ({currentRegion3LayoutAttemptDescription})";
        }

        if (!proceduralLayoutBuilt ||
            !TryValidateRegion3ReflectorFoundationGeometry(
                platePositions,
                plateCount,
                bossPosition,
                out proceduralFailureReason))
        {
            actualPattern = Region3ReflectorLayoutPattern.CompactPerimeterFallback;

            if (!TryBuildDeterministicCompactRegion3Foundation(
                    bossPosition,
                    platePositions,
                    plateRotations,
                    plateCount,
                    out string fallbackFailureReason))
            {
                FailRegion3BossFoundation(
                    $"Region-3 compact reflector foundation failed. Requested={requestedPattern}, " +
                    $"Required={plateCount}, ProceduralFailure={proceduralFailureReason}, " +
                    $"FallbackFailure={fallbackFailureReason}, " +
                    $"StartSector={currentRegion3StartSector}, Player={startPosition}, " +
                    $"Boss={bossPosition}, BoundsCenter={(Vector2)CameraSafeBounds.center}, " +
                    $"BoundsExtents={(Vector2)CameraSafeBounds.extents}, " +
                    $"Attempt={currentRegion3LayoutAttemptDescription}.",
                    null,
                    plates
                );
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"Region-3 reflector template {requestedPattern} was rejected " +
                $"({proceduralFailureReason}); deterministic compact perimeter fallback used.",
                this
            );
#endif
        }

        currentRegion3ReflectorCandidateCount = plateCount;
        for (int i = 0; i < plateCount; i++)
        {
            Vector2 position = platePositions[i];
            float plateRotation = plateRotations[i];
            GameObject plateObject = Spawn(
                region3ReflectorPlatePrefab.gameObject,
                position,
                $"PhaseReflectorPlate_{i:00}",
                Quaternion.Euler(0f, 0f, plateRotation)
            );

            if (plateObject != null)
            {
                currentRegion3ReflectorSpawnedCount++;
            }

            if (plateObject == null ||
                !plateObject.TryGetComponent(out PhaseReflectorPlate plate))
            {
                if (plateObject != null)
                {
                    DestroyGeneratedInstance(plateObject);
                }

                FailRegion3BossFoundation(
                    $"Region-3 reflector spawn failed transactionally at index {i}. " +
                    $"Template={actualPattern}, Required={plateCount}, " +
                    $"Spawned={currentRegion3ReflectorSpawnedCount}.",
                    null,
                    plates
                );
                return;
            }

            plate.Configure(position, plateRotation, region3ReflectorPlateLength);
            plates[i] = plate;
        }

        GameObject bossObject = Spawn(
            region3BossEncounterPrefab.gameObject,
            bossPosition,
            "Boss_PhaseGatekeeper"
        );
        PhaseGatekeeperBossController bossController = null;
        bool hasPhaseGatekeeperController = bossObject != null &&
            bossObject.TryGetComponent(out bossController);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BossPatternController region1BossController = bossObject != null
            ? bossObject.GetComponent<BossPatternController>()
            : null;
        ExpeditionDepth depth = ResolveCurrentDepth();
        CampaignBossId bossId = CampaignProgressionCatalog.GetBossId(depth);

        Debug.Log(
            "[Region3 Boss Spawn] " +
            $"Depth={depth} BossId={bossId} " +
            $"Object={(bossObject != null ? bossObject.name : "null")} " +
            $"PhaseGatekeeper={hasPhaseGatekeeperController} " +
            $"Region1Controller={region1BossController != null} " +
            $"InstanceId={(bossObject != null ? bossObject.GetInstanceID() : 0)}",
            bossObject != null ? bossObject : this
        );
#endif

        if (!hasPhaseGatekeeperController)
        {
            FailRegion3BossFoundation(
                "Region-3 Phase Gatekeeper Boss failed to spawn after its reflector " +
                "foundation had been validated.",
                bossObject,
                plates
            );
            return;
        }

        currentRegion3BossPosition = bossPosition;
        currentRegion3BossEncounter = bossController;
        currentRegion3ReflectorLayoutPattern = actualPattern;
        occupiedPositions.Add(bossPosition);
        importantPositions.Add(bossPosition);
        ReservePlacementBounds(new Bounds(
            bossPosition,
            new Vector3(
                Mathf.Max(4f, region3BossReservationSize),
                Mathf.Max(4f, region3BossReservationSize),
                0f
            )
        ));

        float reservationSize = ResolveRegion3ReflectorReservationSize();
        for (int i = 0; i < plateCount; i++)
        {
            PhaseReflectorPlate plate = plates[i];
            Vector2 position = plate.transform.position;
            currentRegion3Reflectors[i] = plate;
            currentRegion3ReflectorPositions[i] = position;
            occupiedPositions.Add(position);
            ReservePlacementBounds(new Bounds(
                position,
                new Vector3(reservationSize, reservationSize, 0f)
            ));
        }

        currentRegion3ReflectorCount = plateCount;
        bossController.ConfigureEncounter(
            MapBounds,
            CameraSafeBounds,
            bossPosition,
            plates
        );
        currentRegion3ConfiguredReflectorCount = plateCount;
        currentRegion3EncounterConfigured = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logGenerationResult)
        {
            Debug.Log(
                $"Region-3 layout: start sector {currentRegion3StartSector}, " +
                $"reflector template {currentRegion3ReflectorLayoutPattern}, " +
                $"plates {currentRegion3ReflectorCount}/{plateCount}.",
                this
            );
        }
#endif
    }

    private Region3ReflectorLayoutPattern SelectRegion3ReflectorLayoutPattern()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugForcedRegion3ReflectorTemplate >= 0)
        {
            return (Region3ReflectorLayoutPattern)Mathf.Clamp(
                debugForcedRegion3ReflectorTemplate,
                0,
                Region3ReflectorLayoutPatternCount - 1
            );
        }
#endif

        return (Region3ReflectorLayoutPattern)UnityEngine.Random.Range(
            0,
            Region3ReflectorLayoutPatternCount
        );
    }

    private bool TryBuildRegion3ReflectorLayout(
        Region3ReflectorLayoutPattern pattern,
        Vector2 bossPosition,
        Vector2[] positions,
        float[] rotations,
        int plateCount)
    {
        int attempts = Mathf.Clamp(region3ReflectorLayoutAttempts, 1, 32);
        float reservationSize = ResolveRegion3ReflectorReservationSize();
        float footprintRadius = reservationSize * 0.5f;
        Vector2 layoutExtents = new Vector2(
            Mathf.Max(7f, CameraSafeBounds.extents.x - footprintRadius),
            Mathf.Max(7f, CameraSafeBounds.extents.y - footprintRadius)
        );
        float layoutRotationSeed = UnityEngine.Random.Range(0f, 360f);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 layoutCenter = ResolveRegion3ReflectorLayoutCenter(pattern);
            float layoutRotation = ResolveRegion3ReflectorLayoutRotation(
                pattern,
                attempt,
                attempts,
                layoutRotationSeed
            );
            currentRegion3LayoutAttemptDescription =
                $"Template={pattern}, Attempt={attempt + 1}/{attempts}, " +
                $"Center={layoutCenter}, Rotation={layoutRotation:0.##}";
            bool valid = true;

            for (int i = 0; i < plateCount; i++)
            {
                Vector2 normalizedOffset = ResolveRegion3ReflectorTemplateOffset(
                    pattern,
                    i,
                    plateCount
                );
                Vector2 rotatedNormalizedOffset = RotateVector(
                    normalizedOffset,
                    layoutRotation
                );
                Vector2 rotatedOffset = new Vector2(
                    rotatedNormalizedOffset.x * layoutExtents.x,
                    rotatedNormalizedOffset.y * layoutExtents.y
                );
                float polarAngle = Mathf.Atan2(rotatedOffset.y, rotatedOffset.x) *
                    Mathf.Rad2Deg;
                float maximumRotationJitter = Mathf.Clamp(
                    region3ReflectorRotationJitter,
                    0f,
                    12f
                );
                bool candidateFound = false;

                for (int jitterAttempt = 0; jitterAttempt < 8; jitterAttempt++)
                {
                    Vector2 jitter = UnityEngine.Random.insideUnitCircle *
                        Mathf.Clamp(region3ReflectorPositionJitter, 0f, 2f);
                    Vector2 position = ClampToInsetBounds(
                        layoutCenter + rotatedOffset + jitter,
                        CameraSafeBounds,
                        footprintRadius
                    );
                    float rotation = polarAngle + 38f +
                        (i % 2 == 0 ? 8f : -8f) +
                        UnityEngine.Random.Range(
                            -maximumRotationJitter,
                            maximumRotationJitter
                        );

                    if (!IsRegion3ReflectorCandidateValid(
                            position,
                            rotation,
                            bossPosition,
                            positions,
                            i,
                            reservationSize,
                            footprintRadius))
                    {
                        continue;
                    }

                    positions[i] = position;
                    rotations[i] = rotation;
                    candidateFound = true;
                    break;
                }

                if (!candidateFound)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryBuildDeterministicCompactRegion3Foundation(
        Vector2 bossPosition,
        Vector2[] positions,
        float[] rotations,
        int plateCount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (positions == null || rotations == null ||
            plateCount != Region3ReflectorCount ||
            positions.Length < plateCount || rotations.Length < plateCount)
        {
            failureReason = "fallback buffers do not match the required 12 plates";
            return false;
        }

        float reservationSize = ResolveRegion3ReflectorReservationSize();
        float footprintRadius = reservationSize * 0.5f;
        Vector2 usableExtents = new Vector2(
            Mathf.Max(7f, CameraSafeBounds.extents.x - footprintRadius),
            Mathf.Max(7f, CameraSafeBounds.extents.y - footprintRadius)
        );

        Vector2 awayFromPlayer = (Vector2)CameraSafeBounds.center - startPosition;
        if (awayFromPlayer.sqrMagnitude > 0.0001f)
        {
            awayFromPlayer.Normalize();
        }

        Vector2 perpendicular = new Vector2(-awayFromPlayer.y, awayFromPlayer.x);
        for (int layoutAttempt = 0; layoutAttempt < 12; layoutAttempt++)
        {
            int scaleAttempt = layoutAttempt % 3;
            int centerVariant = layoutAttempt / 3;
            float scale = scaleAttempt == 0
                ? Region3CompactFallbackScale
                : scaleAttempt == 1 ? 0.62f : 0.74f;
            Vector2 layoutExtents = usableExtents * scale;
            Vector2 layoutCenter = CameraSafeBounds.center;
            if (centerVariant == 1)
            {
                layoutCenter += awayFromPlayer * 2.5f;
            }
            else if (centerVariant == 2)
            {
                layoutCenter += perpendicular * 2.5f;
            }
            else if (centerVariant == 3)
            {
                layoutCenter -= perpendicular * 2.5f;
            }
            currentRegion3LayoutAttemptDescription =
                $"Template=CompactPerimeterFallback, Scale={scale:0.##}, " +
                $"Center={layoutCenter}, Rotation=0";
            int availableMask = 0;

            for (int i = 0; i < Region3CompactFallbackCandidateCount; i++)
            {
                Vector2 offset = ResolveCompactPerimeterCandidate(i, layoutExtents);
                Vector2 position = layoutCenter + offset;
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                float rotation = angle + 90f + (i % 2 == 0 ? 6f : -6f);
                region3CompactFallbackPositions[i] = position;
                region3CompactFallbackRotations[i] = rotation;
                region3CompactFallbackAvailable[i] =
                    IsRegion3ReflectorCandidateValid(
                        position,
                        rotation,
                        bossPosition,
                        positions,
                        0,
                        reservationSize,
                        footprintRadius
                    );
                if (region3CompactFallbackAvailable[i])
                {
                    availableMask |= 1 << i;
                }
            }

            int bestSelectionMask = -1;
            float bestSelectionScore = float.NegativeInfinity;
            int fullSelectionMask = (1 << Region3CompactFallbackCandidateCount) - 1;
            for (int omittedA = 0;
                 omittedA < Region3CompactFallbackCandidateCount - 3;
                 omittedA++)
            {
                for (int omittedB = omittedA + 1;
                     omittedB < Region3CompactFallbackCandidateCount - 2;
                     omittedB++)
                {
                    for (int omittedC = omittedB + 1;
                         omittedC < Region3CompactFallbackCandidateCount - 1;
                         omittedC++)
                    {
                        for (int omittedD = omittedC + 1;
                             omittedD < Region3CompactFallbackCandidateCount;
                             omittedD++)
                        {
                            int omittedMask = (1 << omittedA) | (1 << omittedB) |
                                (1 << omittedC) | (1 << omittedD);
                            int selectionMask = fullSelectionMask & ~omittedMask;
                            EvaluateCompactSelection(
                                selectionMask,
                                availableMask,
                                bossPosition,
                                ref bestSelectionMask,
                                ref bestSelectionScore
                            );
                        }
                    }
                }
            }

            int selectedCount = 0;
            for (int i = 0;
                 i < Region3CompactFallbackCandidateCount && bestSelectionMask >= 0;
                 i++)
            {
                if ((bestSelectionMask & (1 << i)) == 0)
                {
                    continue;
                }

                positions[selectedCount] = region3CompactFallbackPositions[i];
                rotations[selectedCount] = region3CompactFallbackRotations[i];
                selectedCount++;
            }

            if (selectedCount == plateCount &&
                TryValidateRegion3ReflectorFoundationGeometry(
                    positions,
                    plateCount,
                    bossPosition,
                    out failureReason))
            {
                return true;
            }
        }

        if (string.IsNullOrEmpty(failureReason))
        {
            failureReason = "deterministic perimeter had fewer than 12 valid slots";
        }

        return false;
    }

    private void EvaluateCompactSelection(
        int selectionMask,
        int availableMask,
        Vector2 bossPosition,
        ref int bestSelectionMask,
        ref float bestSelectionScore)
    {
        if ((selectionMask & ~availableMask) != 0 ||
            !IsCompactSelectionWellDistributed(selectionMask))
        {
            return;
        }

        float score = 0f;
        for (int i = 0; i < Region3CompactFallbackCandidateCount; i++)
        {
            if ((selectionMask & (1 << i)) == 0)
            {
                continue;
            }

            Vector2 candidate = region3CompactFallbackPositions[i];
            score += (candidate - startPosition).sqrMagnitude +
                (candidate - bossPosition).sqrMagnitude * 0.1f;
        }

        if (score > bestSelectionScore)
        {
            bestSelectionScore = score;
            bestSelectionMask = selectionMask;
        }
    }

    private bool IsCompactSelectionWellDistributed(int selectionMask)
    {
        float minimumSpacingSqr = ResolveRegion3ReflectorMinimumSpacing();
        minimumSpacingSqr *= minimumSpacingSqr;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        int quadrantMask = 0;
        Vector2 center = CameraSafeBounds.center;

        for (int i = 0; i < Region3CompactFallbackCandidateCount; i++)
        {
            if ((selectionMask & (1 << i)) == 0)
            {
                continue;
            }

            Vector2 position = region3CompactFallbackPositions[i];
            minimumX = Mathf.Min(minimumX, position.x);
            maximumX = Mathf.Max(maximumX, position.x);
            minimumY = Mathf.Min(minimumY, position.y);
            maximumY = Mathf.Max(maximumY, position.y);
            Vector2 relative = position - center;
            int quadrant = relative.x >= 0f
                ? (relative.y >= 0f ? 0 : 3)
                : (relative.y >= 0f ? 1 : 2);
            quadrantMask |= 1 << quadrant;

            for (int other = 0; other < i; other++)
            {
                if ((selectionMask & (1 << other)) != 0 &&
                    (position - region3CompactFallbackPositions[other]).sqrMagnitude <
                    minimumSpacingSqr)
                {
                    return false;
                }
            }
        }

        float normalizedSpanX = (maximumX - minimumX) / CameraSafeBounds.size.x;
        float normalizedSpanY = (maximumY - minimumY) / CameraSafeBounds.size.y;
        return quadrantMask == 0x0F &&
            normalizedSpanX >= Region3MinimumNormalizedSpan &&
            normalizedSpanY >= Region3MinimumNormalizedSpan;
    }

    private static Vector2 ResolveCompactPerimeterCandidate(
        int index,
        Vector2 extents)
    {
        int wrappedIndex = ((index % Region3CompactFallbackCandidateCount) +
            Region3CompactFallbackCandidateCount) % Region3CompactFallbackCandidateCount;
        int side = wrappedIndex / 4;
        float t = (wrappedIndex % 4) * 0.25f;

        switch (side)
        {
            case 0:
                return new Vector2(Mathf.Lerp(-extents.x, extents.x, t), extents.y);
            case 1:
                return new Vector2(extents.x, Mathf.Lerp(extents.y, -extents.y, t));
            case 2:
                return new Vector2(Mathf.Lerp(extents.x, -extents.x, t), -extents.y);
            default:
                return new Vector2(-extents.x, Mathf.Lerp(-extents.y, extents.y, t));
        }
    }

    private bool TryValidateRegion3ReflectorFoundationGeometry(
        Vector2[] positions,
        int count,
        Vector2 bossPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (positions == null || count != Region3ReflectorCount || positions.Length < count)
        {
            failureReason = $"expected {Region3ReflectorCount} reflector positions, got {count}";
            return false;
        }

        float reservationSize = ResolveRegion3ReflectorReservationSize();
        float footprintRadius = reservationSize * 0.5f;
        float playerClearance = ResolveRegion3ReflectorPlayerClearance();
        float bossClearance = ResolveRegion3ReflectorBossClearance();
        float minimumSpacing = ResolveRegion3ReflectorMinimumSpacing();
        float minimumSpacingSqr = minimumSpacing * minimumSpacing;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        int quadrantMask = 0;
        Vector2 center = CameraSafeBounds.center;

        for (int i = 0; i < count; i++)
        {
            Vector2 position = positions[i];
            if (position.x < CameraSafeBounds.min.x + footprintRadius ||
                position.x > CameraSafeBounds.max.x - footprintRadius ||
                position.y < CameraSafeBounds.min.y + footprintRadius ||
                position.y > CameraSafeBounds.max.y - footprintRadius)
            {
                failureReason = $"reflector {i} at {position} is outside footprint-safe bounds";
                return false;
            }

            if ((position - startPosition).sqrMagnitude < playerClearance * playerClearance)
            {
                failureReason = $"reflector {i} violates Player clearance {playerClearance:0.##}";
                return false;
            }

            if ((position - bossPosition).sqrMagnitude < bossClearance * bossClearance)
            {
                failureReason = $"reflector {i} violates Boss clearance {bossClearance:0.##}";
                return false;
            }

            for (int other = 0; other < i; other++)
            {
                if ((position - positions[other]).sqrMagnitude < minimumSpacingSqr)
                {
                    failureReason = $"reflectors {other}/{i} violate spacing {minimumSpacing:0.##}";
                    return false;
                }
            }

            minimumX = Mathf.Min(minimumX, position.x);
            maximumX = Mathf.Max(maximumX, position.x);
            minimumY = Mathf.Min(minimumY, position.y);
            maximumY = Mathf.Max(maximumY, position.y);
            Vector2 relative = position - center;
            int quadrant = relative.x >= 0f
                ? (relative.y >= 0f ? 0 : 3)
                : (relative.y >= 0f ? 1 : 2);
            quadrantMask |= 1 << quadrant;
        }

        float normalizedSpanX = CameraSafeBounds.size.x > 0.0001f
            ? (maximumX - minimumX) / CameraSafeBounds.size.x
            : 0f;
        float normalizedSpanY = CameraSafeBounds.size.y > 0.0001f
            ? (maximumY - minimumY) / CameraSafeBounds.size.y
            : 0f;
        if (normalizedSpanX < Region3MinimumNormalizedSpan ||
            normalizedSpanY < Region3MinimumNormalizedSpan)
        {
            failureReason = $"layout span {normalizedSpanX:0.###}x{normalizedSpanY:0.###} " +
                $"is below {Region3MinimumNormalizedSpan:0.##}";
            return false;
        }

        int coveredQuadrants = 0;
        for (int i = 0; i < 4; i++)
        {
            if ((quadrantMask & (1 << i)) != 0)
            {
                coveredQuadrants++;
            }
        }

        if (coveredQuadrants < Region3RequiredQuadrantCount)
        {
            failureReason = $"layout covers only {coveredQuadrants}/4 quadrants";
            return false;
        }

        return true;
    }

    private void FailRegion3BossFoundation(
        string reason,
        GameObject bossObject,
        PhaseReflectorPlate[] plates)
    {
        if (!currentRegion3FoundationFailed)
        {
            currentRegion3FoundationFailed = true;
            Debug.LogError(reason ?? "Unknown Region-3 foundation failure.", this);
        }

        AbortRegion3BossFoundation(bossObject, plates);
    }

    private void AbortRegion3BossFoundation(
        GameObject bossObject,
        PhaseReflectorPlate[] plates)
    {
        if (plates != null)
        {
            for (int i = 0; i < plates.Length; i++)
            {
                PhaseReflectorPlate plate = plates[i];
                if (plate == null)
                {
                    continue;
                }

                plate.SetGameplayEnabled(false);
                DestroyGeneratedInstance(plate.gameObject);
            }
        }

        if (bossObject != null)
        {
            bossObject.SetActive(false);
            DestroyGeneratedInstance(bossObject);
        }

        currentRegion3BossEncounter = null;
        currentRegion3EncounterConfigured = false;
        currentRegion3ReflectorCandidateCount = 0;
        currentRegion3ReflectorSpawnedCount = 0;
        currentRegion3ReflectorCount = 0;
        currentRegion3ConfiguredReflectorCount = 0;
        currentRegion3ReflectorLayoutPattern = Region3ReflectorLayoutPattern.None;
        currentRegion3BossPosition = Vector2.zero;
        Array.Clear(currentRegion3Reflectors, 0, currentRegion3Reflectors.Length);
        Array.Clear(
            currentRegion3ReflectorPositions,
            0,
            currentRegion3ReflectorPositions.Length
        );
    }

    private static void DestroyGeneratedInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(instance);
        }
        else
        {
            DestroyImmediate(instance);
        }
    }

    private Vector2 ResolveRegion3ReflectorLayoutCenter(
        Region3ReflectorLayoutPattern pattern)
    {
        Vector2 center = CameraSafeBounds.center;
        Vector2 awayFromStart = center - startPosition;
        if (awayFromStart.sqrMagnitude <= 0.0001f)
        {
            return center;
        }

        if (pattern == Region3ReflectorLayoutPattern.OuterRing)
        {
            float boundedShift = Mathf.Min(
                3.5f,
                Mathf.Min(CameraSafeBounds.extents.x, CameraSafeBounds.extents.y) * 0.08f
            );
            Vector2 perpendicular = new Vector2(-awayFromStart.y, awayFromStart.x).normalized;
            return center + awayFromStart.normalized * boundedShift +
                perpendicular * UnityEngine.Random.Range(-2f, 2f);
        }

        if (pattern == Region3ReflectorLayoutPattern.SplitArcs)
        {
            float boundedShift = Mathf.Min(
                2.5f,
                Mathf.Min(CameraSafeBounds.extents.x, CameraSafeBounds.extents.y) * 0.06f
            );
            return center + awayFromStart.normalized * boundedShift;
        }

        return center;
    }

    private float ResolveRegion3ReflectorLayoutRotation(
        Region3ReflectorLayoutPattern pattern,
        int attempt,
        int attemptCount,
        float rotationSeed)
    {
        if (pattern == Region3ReflectorLayoutPattern.OuterRing)
        {
            float symmetricStep = 45f / Mathf.Max(1, attemptCount);
            return rotationSeed + attempt * symmetricStep;
        }

        float variation = Mathf.Clamp(
            region3ReflectorLayoutRotationVariation,
            0f,
            20f
        );
        if (pattern == Region3ReflectorLayoutPattern.DiagonalLanes)
        {
            int seedQuarterTurn = Mathf.FloorToInt(Mathf.Repeat(rotationSeed, 360f) / 90f);
            int quarterTurn = (seedQuarterTurn + attempt) % 4;
            return quarterTurn * 90f + UnityEngine.Random.Range(-variation, variation);
        }

        float rotationStep = 360f / Mathf.Max(1, attemptCount);
        return rotationSeed + attempt * rotationStep +
            UnityEngine.Random.Range(-variation, variation);
    }

    private static Vector2 ResolveRegion3ReflectorTemplateOffset(
        Region3ReflectorLayoutPattern pattern,
        int index,
        int plateCount)
    {
        switch (pattern)
        {
            case Region3ReflectorLayoutPattern.OuterRing:
                return ResolveRingTemplatePoint(index, plateCount, 0.62f, 15f);
            case Region3ReflectorLayoutPattern.BrokenRing:
                return ResolveBrokenRingTemplatePoint(index);
            case Region3ReflectorLayoutPattern.DiagonalLanes:
                return ResolveDiagonalLaneTemplatePoint(index);
            case Region3ReflectorLayoutPattern.SplitArcs:
                return ResolveSplitArcTemplatePoint(index);
            case Region3ReflectorLayoutPattern.OuterFrameInnerPair:
                return ResolveOuterFrameTemplatePoint(index);
            default:
                return ResolveCrossDiamondTemplatePoint(index);
        }
    }

    private static Vector2 ResolveRingTemplatePoint(
        int index,
        int plateCount,
        float radius,
        float angularOffset)
    {
        float angle = angularOffset + 360f * index / Mathf.Max(1, plateCount);
        return AngleToNormalizedOffset(angle, radius);
    }

    private static Vector2 ResolveBrokenRingTemplatePoint(int index)
    {
        int arcIndex = index / 4;
        int indexWithinArc = index % 4;
        float angle = 8f + arcIndex * 120f + indexWithinArc * 14f;
        float radius = indexWithinArc == 1 || indexWithinArc == 2
            ? 0.78f
            : 0.7f;
        return AngleToNormalizedOffset(angle, radius);
    }

    private static Vector2 ResolveSplitArcTemplatePoint(int index)
    {
        int arcIndex = index / 6;
        int indexWithinArc = index % 6;
        float angle = 32f + indexWithinArc * 18f + arcIndex * 180f;
        float radius = indexWithinArc == 2 || indexWithinArc == 3
            ? 0.82f
            : (indexWithinArc == 1 || indexWithinArc == 4 ? 0.76f : 0.68f);
        return AngleToNormalizedOffset(angle, radius);
    }

    private static Vector2 ResolveDiagonalLaneTemplatePoint(int index)
    {
        int laneIndex = index / 6;
        int indexWithinLane = index % 6;
        float alongLane = Mathf.Lerp(-0.72f, 0.72f, indexWithinLane / 5f);
        float laneOffset = laneIndex == 0 ? 0.14f : -0.14f;
        return new Vector2(alongLane, alongLane + laneOffset);
    }

    private static Vector2 ResolveOuterFrameTemplatePoint(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector2(-0.72f, -0.72f);
            case 1:
                return new Vector2(0f, -0.78f);
            case 2:
                return new Vector2(0.72f, -0.72f);
            case 3:
                return new Vector2(0.78f, 0f);
            case 4:
                return new Vector2(0.72f, 0.72f);
            case 5:
                return new Vector2(0f, 0.78f);
            case 6:
                return new Vector2(-0.72f, 0.72f);
            case 7:
                return new Vector2(-0.78f, 0f);
            case 8:
                return new Vector2(-0.24f, -0.24f);
            case 9:
                return new Vector2(0.24f, -0.24f);
            case 10:
                return new Vector2(0.24f, 0.24f);
            default:
                return new Vector2(-0.24f, 0.24f);
        }
    }

    private static Vector2 ResolveCrossDiamondTemplatePoint(int index)
    {
        if (index < 8)
        {
            float outerAngle = 22.5f + index * 45f;
            return AngleToNormalizedOffset(outerAngle, 0.78f);
        }

        float innerAngle = 45f + (index - 8) * 90f;
        return AngleToNormalizedOffset(innerAngle, 0.3f);
    }

    private static Vector2 AngleToNormalizedOffset(float angle, float radius)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius
        );
    }

    private bool IsRegion3ReflectorCandidateValid(
        Vector2 position,
        float rotation,
        Vector2 bossPosition,
        Vector2[] placedPositions,
        int placedCount,
        float reservationSize,
        float footprintRadius)
    {
        if (position.x < CameraSafeBounds.min.x + footprintRadius ||
            position.x > CameraSafeBounds.max.x - footprintRadius ||
            position.y < CameraSafeBounds.min.y + footprintRadius ||
            position.y > CameraSafeBounds.max.y - footprintRadius)
        {
            return false;
        }

        float startClearance = ResolveRegion3ReflectorPlayerClearance();
        if ((position - startPosition).sqrMagnitude < startClearance * startClearance)
        {
            return false;
        }

        float bossClearance = ResolveRegion3ReflectorBossClearance();
        if ((position - bossPosition).sqrMagnitude < bossClearance * bossClearance)
        {
            return false;
        }

        float minimumSpacing = ResolveRegion3ReflectorMinimumSpacing();
        float minimumSpacingSqr = minimumSpacing * minimumSpacing;
        for (int i = 0; i < placedCount; i++)
        {
            if ((position - placedPositions[i]).sqrMagnitude < minimumSpacingSqr)
            {
                return false;
            }
        }

        Bounds candidateBounds = new Bounds(
            position,
            new Vector3(reservationSize, reservationSize, 0f)
        );
        if (IntersectsReservedPlacementBounds(candidateBounds, 0f))
        {
            return false;
        }

        return !useBlockedLayerCheck ||
               Physics2D.OverlapBox(
                   position,
                   new Vector2(reservationSize, reservationSize),
                   rotation,
                   blockedLayer
               ) == null;
    }

    private float ResolveRegion3ReflectorReservationSize()
    {
        return Mathf.Max(1f, region3ReflectorPlateLength + 1.5f);
    }

    private float ResolveRegion3ReflectorPlayerClearance()
    {
        return Mathf.Clamp(region3ReflectorPlayerClearance, 8f, 12f);
    }

    private float ResolveRegion3ReflectorBossClearance()
    {
        return Mathf.Clamp(region3ReflectorBossClearance, 6f, 10f);
    }

    private float ResolveRegion3ReflectorMinimumSpacing()
    {
        return Mathf.Max(
            4.5f,
            Mathf.Max(
                region3ReflectorMinimumSpacing,
                region3ReflectorPlateLength + 0.75f
            )
        );
    }

    private float ResolveRegion3BossMinimumStartClearance()
    {
        return Mathf.Clamp(region3BossMinimumStartClearance, 16f, 24f);
    }

    private static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            vector.x * cosine - vector.y * sine,
            vector.x * sine + vector.y * cosine
        );
    }

    private Vector2 ResolveRegion3BossPosition()
    {
        Vector2 center = CameraSafeBounds.center;
        Vector2 extents = CameraSafeBounds.extents;
        Vector2 awayFromStart = center - startPosition;
        if (awayFromStart.sqrMagnitude <= 0.0001f)
        {
            awayFromStart = Vector2.right;
        }

        awayFromStart.Normalize();
        const float compactBossOffset = 0.24f;
        float minimumStartDistance = ResolveRegion3BossMinimumStartClearance();
        Vector2 best = center;
        float bestDistance = -1f;

        for (int i = 0; i < 8; i++)
        {
            float angleOffset = i == 0
                ? 0f
                : (i % 2 == 1 ? 1f : -1f) * ((i + 1) / 2) * 22.5f;
            Vector2 direction = RotateVector(awayFromStart, angleOffset).normalized;
            Vector2 candidate = center + new Vector2(
                direction.x * extents.x * compactBossOffset,
                direction.y * extents.y * compactBossOffset
            );
            candidate = ClampToInsetBounds(
                candidate,
                CameraSafeBounds,
                Mathf.Max(2f, region3BossReservationSize * 0.5f)
            );

            float distance = (candidate - startPosition).sqrMagnitude;
            if (distance < minimumStartDistance * minimumStartDistance ||
                !IsRegion3BossCandidateClear(candidate) ||
                distance <= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        if (bestDistance >= 0f)
        {
            return best;
        }

        Vector2 deterministicFallback = center + new Vector2(
            awayFromStart.x * extents.x * compactBossOffset,
            awayFromStart.y * extents.y * compactBossOffset
        );
        return ClampToInsetBounds(
            deterministicFallback,
            CameraSafeBounds,
            Mathf.Max(2f, region3BossReservationSize * 0.5f)
        );
    }

    private bool IsRegion3BossCandidateClear(Vector2 candidate)
    {
        if (!useBlockedLayerCheck)
        {
            return true;
        }

        return Physics2D.OverlapCircle(
            candidate,
            Mathf.Max(1f, region3StartBlockingRadius * 2f),
            blockedLayer
        ) == null;
    }

    private void BuildPoiLayout()
    {
        if (config == null || !config.EnablePoiClusterLayout)
        {
            return;
        }

        RegisterExistingPoiAnchors();

        if (IsRegion3PhaseGatekeeperMap())
        {
            return;
        }

        for (int i = 0; i < config.SalvagePoiCount; i++)
        {
            bool lowRisk = i == 0;

            if (!TryCreateSalvagePoi(PoiType.SalvageField, config.SalvagePoiRadius, lowRisk))
            {
                poiPlacementFallbackUsed = true;
            }
        }

        for (int i = 0; i < config.HighValuePoiCount; i++)
        {
            if (!TryCreateSalvagePoi(PoiType.HighValueSalvage, config.HighValuePoiRadius, false))
            {
                poiPlacementFallbackUsed = true;
            }
        }
    }

    private void RegisterExistingPoiAnchors()
    {
        float fieldBaseRadius = Mathf.Max(fieldBaseReservationSize.x, fieldBaseReservationSize.y) * 0.5f;
        float shopRadius = Mathf.Max(shopZoneReservationSize.x, shopZoneReservationSize.y) * 0.5f;

        for (int i = 0; i < spawnedFieldBases.Count; i++)
        {
            FieldBaseController fieldBase = spawnedFieldBases[i];
            if (fieldBase != null)
            {
                AddPoiAnchor(PoiType.FieldBase, fieldBase.transform.position, fieldBaseRadius, false);
            }
        }

        for (int i = 0; i < spawnedShopStructures.Count; i++)
        {
            ShopStructure shop = spawnedShopStructures[i];
            if (shop != null)
            {
                AddPoiAnchor(PoiType.Shop, shop.transform.position, shopRadius, false);
            }
        }

        for (int i = 0; i < spawnedEventObjects.Count; i++)
        {
            ExpeditionEventObject eventObject = spawnedEventObjects[i];
            if (eventObject != null)
            {
                AddPoiAnchor(PoiType.Event, eventObject.transform.position, 4f, false);
            }
        }

        float coreRadius = config != null
            ? Mathf.Max(config.BossArenaHalfExtents.x, config.BossArenaHalfExtents.y)
            : 14f;

        for (int i = 0; i < spawnedCoreObjects.Count; i++)
        {
            CoreObject core = spawnedCoreObjects[i];
            if (core != null)
            {
                AddPoiAnchor(PoiType.Core, core.transform.position, coreRadius, false);
            }
        }
    }

    private bool TryCreateSalvagePoi(PoiType type, float radius, bool lowRisk)
    {
        radius = Mathf.Max(1f, radius);
        bool found = TryFindPoiPosition(type, radius, lowRisk, out Vector2 position);

        if (!found)
        {
            if (logGenerationResult)
            {
                Debug.LogWarning($"POI placement failed for {type}; content will use general placement fallback.", this);
            }

            return false;
        }

        PoiAnchor anchor = AddPoiAnchor(type, position, radius, lowRisk);

        if (type == PoiType.SalvageField)
        {
            salvagePoiAnchors.Add(anchor);
        }
        else
        {
            highValuePoiAnchors.Add(anchor);
        }

        return true;
    }

    private PoiAnchor AddPoiAnchor(PoiType type, Vector2 center, float radius, bool lowRisk)
    {
        radius = Mathf.Max(1f, radius);

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor existing = poiAnchors[i];
            float mergeDistance = Mathf.Max(existing.Radius, radius);

            if (existing.Type == type &&
                (existing.Center - center).sqrMagnitude < mergeDistance * mergeDistance)
            {
                return existing;
            }
        }

        PoiAnchor anchor = new PoiAnchor
        {
            Type = type,
            Center = center,
            Radius = radius,
            IsLowRisk = lowRisk
        };

        poiAnchors.Add(anchor);
        return anchor;
    }

    private bool TryFindPoiPosition(
        PoiType type,
        float radius,
        bool preferNearStart,
        out Vector2 position)
    {
        position = Vector2.zero;
        float halfWidth = mapSize.x * 0.5f - edgePadding - radius;
        float halfHeight = mapSize.y * 0.5f - edgePadding - radius;

        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return false;
        }

        float safeRadius = config != null ? config.StartSafeRadius : 10f;
        float minimumStartDistance = safeRadius + radius + 1.5f;
        float bestScore = preferNearStart ? float.PositiveInfinity : float.NegativeInfinity;
        bool found = false;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(-halfWidth, halfWidth),
                UnityEngine.Random.Range(-halfHeight, halfHeight)
            );

            float startDistance = Vector2.Distance(candidate, startPosition);
            if (startDistance < minimumStartDistance)
            {
                continue;
            }

            Bounds candidateBounds = new Bounds(candidate, new Vector3(radius * 2f, radius * 2f, 0f));
            if (type == PoiType.HighValueSalvage &&
                IsAboveRegion2MajorPoiCeiling(candidateBounds.max.y))
            {
                continue;
            }

            if (IntersectsReservedPlacementBounds(candidateBounds, 1f))
            {
                continue;
            }

            if (!IsPoiSpacingValid(candidate, radius))
            {
                continue;
            }

            if (useBlockedLayerCheck && Physics2D.OverlapCircle(candidate, radius, blockedLayer) != null)
            {
                continue;
            }

            float score = preferNearStart
                ? startDistance
                : GetNearestPoiDistance(candidate);

            if (!found ||
                (preferNearStart && score < bestScore) ||
                (!preferNearStart && score > bestScore))
            {
                found = true;
                bestScore = score;
                position = candidate;
            }
        }

        return found;
    }

    private bool IsPoiSpacingValid(Vector2 candidate, float radius)
    {
        float configuredSpacing = config != null ? config.PoiMinSpacing : 14f;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor other = poiAnchors[i];
            float otherInfluence = other.Type == PoiType.SalvageField || other.Type == PoiType.HighValueSalvage
                ? other.Radius
                : Mathf.Min(other.Radius, 5f);
            float required = Mathf.Max(configuredSpacing, radius + otherInfluence + 1f);

            if ((candidate - other.Center).sqrMagnitude < required * required)
            {
                return false;
            }
        }

        return true;
    }

    private float GetNearestPoiDistance(Vector2 candidate)
    {
        float nearest = Vector2.Distance(candidate, startPosition);

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            nearest = Mathf.Min(nearest, Vector2.Distance(candidate, poiAnchors[i].Center));
        }

        return nearest;
    }

    private void PlaceFieldBaseBatch(int count)
    {
        PlaceLargeZoneBatch(
            fieldBasePrefabs,
            fieldBasePrefab,
            count,
            fieldBaseReservationSize,
            "FieldBase",
            true
        );
    }

    private void PlaceShopZoneBatch(int count)
    {
        PlaceLargeZoneBatch(
            shopZonePrefabs,
            shopPrefab,
            count,
            shopZoneReservationSize,
            "ShopZone",
            false
        );
    }

    private void PlaceLargeZoneBatch(
        GameObject[] prefabs,
        GameObject fallbackPrefab,
        int count,
        Vector2 reservationSize,
        string label,
        bool fieldBaseZone)
    {
        if (!HasValidPrefab(prefabs, fallbackPrefab) || count <= 0)
        {
            return;
        }

        reservationSize = new Vector2(
            Mathf.Max(1f, reservationSize.x),
            Mathf.Max(1f, reservationSize.y)
        );

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = PickPrefab(prefabs, fallbackPrefab);
            if (prefab == null)
            {
                continue;
            }

            int quarterTurns = rotateLargeZonesByRightAngles
                ? UnityEngine.Random.Range(0, 4)
                : 0;

            Quaternion rotation = Quaternion.Euler(0f, 0f, quarterTurns * 90f);
            Vector2 rotatedReservationSize = quarterTurns % 2 == 0
                ? reservationSize
                : new Vector2(reservationSize.y, reservationSize.x);

            Bounds localPlacementBounds = new Bounds(
                Vector3.zero,
                new Vector3(rotatedReservationSize.x, rotatedReservationSize.y, 0f)
            );
            Bounds localPortalArrivalBounds = default;
            bool hasPortalArrivalBounds = false;

            if (!fieldBaseZone)
            {
                ResolveShopPlacementGeometry(
                    prefab,
                    reservationSize,
                    quarterTurns,
                    out localPlacementBounds,
                    out localPortalArrivalBounds,
                    out hasPortalArrivalBounds
                );
            }

            if (!TryFindLargeZonePosition(
                    localPlacementBounds,
                    localPortalArrivalBounds,
                    hasPortalArrivalBounds,
                    out Vector2 position,
                    out bool rejectedByRegion2BossCorridor))
            {
                string warning = hasRegion2BossCorridor
                    ? $"Region-2 {label} placement failed after {maxPlacementAttempts} bounded attempts " +
                      $"with the Boss corridor reserved (corridor candidate rejection: {rejectedByRegion2BossCorridor}). " +
                      "No unsafe fallback was used."
                    : $"{label} 배치 실패. 예약 크기, 기지 수, 중요 지점 간격을 확인하세요.";
                Debug.LogWarning(warning, this);
                continue;
            }

            GameObject spawned = Spawn(
                prefab,
                position,
                $"{label}_{i:00}",
                rotation
            );

            if (spawned == null)
            {
                continue;
            }

            Bounds reserved = OffsetLocalBounds(localPlacementBounds, position);

            ReservePlacementBounds(reserved);
            if (hasPortalArrivalBounds)
            {
                ReservePlacementBounds(OffsetLocalBounds(localPortalArrivalBounds, position));
            }
            occupiedPositions.Add(position);
            importantPositions.Add(position);

            if (fieldBaseZone)
            {
                FieldBaseController[] bases = spawned.GetComponentsInChildren<FieldBaseController>(true);

                if (bases == null || bases.Length == 0)
                {
                    Debug.LogWarning(
                        $"[{spawned.name}] 적 기지 프리팹에 FieldBaseController가 없습니다.",
                        spawned
                    );
                }
                else
                {
                    for (int baseIndex = 0; baseIndex < bases.Length; baseIndex++)
                    {
                        if (bases[baseIndex] != null && !spawnedFieldBases.Contains(bases[baseIndex]))
                        {
                            spawnedFieldBases.Add(bases[baseIndex]);
                        }
                    }
                }
            }
            else
            {
                ShopStructure[] shops = spawned.GetComponentsInChildren<ShopStructure>(true);

                if (shops == null || shops.Length == 0)
                {
                    Debug.LogWarning(
                        $"[{spawned.name}] 상점 구역 프리팹에 ShopStructure가 없습니다.",
                        spawned
                    );
                }
                else
                {
                    for (int shopIndex = 0; shopIndex < shops.Length; shopIndex++)
                    {
                        if (shops[shopIndex] != null && !spawnedShopStructures.Contains(shops[shopIndex]))
                        {
                            spawnedShopStructures.Add(shops[shopIndex]);
                        }
                    }
                }
            }
        }
    }

    private bool TryFindLargeZonePosition(
        Bounds localPlacementBounds,
        Bounds localPortalArrivalBounds,
        bool hasPortalArrivalBounds,
        out Vector2 position,
        out bool rejectedByRegion2BossCorridor)
    {
        position = Vector2.zero;
        rejectedByRegion2BossCorridor = false;
        float mapHalfWidth = mapSize.x * 0.5f - edgePadding;
        float mapHalfHeight = mapSize.y * 0.5f - edgePadding;

        Bounds localEnvelope = localPlacementBounds;
        if (hasPortalArrivalBounds)
        {
            localEnvelope.Encapsulate(localPortalArrivalBounds.min);
            localEnvelope.Encapsulate(localPortalArrivalBounds.max);
        }

        float minimumRootX = -mapHalfWidth - localEnvelope.min.x;
        float maximumRootX = mapHalfWidth - localEnvelope.max.x;
        float minimumRootY = -mapHalfHeight - localEnvelope.min.y;
        float maximumRootY = mapHalfHeight - localEnvelope.max.y;

        if (minimumRootX >= maximumRootX || minimumRootY >= maximumRootY)
        {
            return false;
        }

        float startSafeRadius = config != null ? config.StartSafeRadius : 10f;
        float importantMinDistance = config != null ? config.ImportantPointMinDistance : 20f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(minimumRootX, maximumRootX),
                UnityEngine.Random.Range(minimumRootY, maximumRootY)
            );

            Bounds candidateBounds = OffsetLocalBounds(localPlacementBounds, candidate);
            Bounds portalArrivalBounds = hasPortalArrivalBounds
                ? OffsetLocalBounds(localPortalArrivalBounds, candidate)
                : default;
            largeZoneDebugCandidateBounds = candidateBounds;
            hasLargeZoneDebugCandidateBounds = true;

            if (IsAboveRegion2MajorPoiCeiling(candidateBounds.max.y))
            {
                continue;
            }

            if (IntersectsRegion2BossCorridor(candidateBounds, largeZoneSpacing))
            {
                rejectedByRegion2BossCorridor = true;
                continue;
            }

            if (hasPortalArrivalBounds &&
                IntersectsRegion2BossCorridor(portalArrivalBounds, largeZoneSpacing))
            {
                rejectedByRegion2BossCorridor = true;
                continue;
            }

            Vector3 startPoint3D = new Vector3(startPosition.x, startPosition.y, candidateBounds.center.z);
            if (candidateBounds.SqrDistance(startPoint3D) < startSafeRadius * startSafeRadius)
            {
                continue;
            }

            if (!HasMinimumDistance(candidate, importantPositions, importantMinDistance))
            {
                continue;
            }

            if (IntersectsReservedPlacementBounds(candidateBounds, largeZoneSpacing))
            {
                continue;
            }

            if (hasPortalArrivalBounds &&
                IntersectsReservedPlacementBounds(portalArrivalBounds, largeZoneSpacing))
            {
                continue;
            }

            if (useBlockedLayerCheck &&
                Physics2D.OverlapBox(
                    candidateBounds.center,
                    candidateBounds.size,
                    0f,
                    blockedLayer
                ) != null)
            {
                continue;
            }

            position = candidate;
            return true;
        }

        return false;
    }

    private void ResolveShopPlacementGeometry(
        GameObject prefab,
        Vector2 reservationSize,
        int quarterTurns,
        out Bounds localPlacementBounds,
        out Bounds localPortalArrivalBounds,
        out bool hasPortalArrivalBounds)
    {
        Bounds unrotatedPlacementBounds = new Bounds(
            Vector3.zero,
            new Vector3(reservationSize.x, reservationSize.y, 0f)
        );
        Transform prefabRoot = prefab.transform;

        Collider2D[] colliders = prefab.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D targetCollider = colliders[i];
            if (targetCollider != null &&
                targetCollider.enabled &&
                IsPrefabHierarchyActive(prefabRoot, targetCollider.transform) &&
                targetCollider.bounds.size.x > 0.01f &&
                targetCollider.bounds.size.y > 0.01f)
            {
                EncapsulateWorldBoundsInRootSpace(
                    prefabRoot,
                    targetCollider.bounds,
                    ref unrotatedPlacementBounds
                );
            }
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer != null &&
                targetRenderer.enabled &&
                IsPrefabHierarchyActive(prefabRoot, targetRenderer.transform) &&
                targetRenderer.bounds.size.x > 0.01f &&
                targetRenderer.bounds.size.y > 0.01f)
            {
                EncapsulateWorldBoundsInRootSpace(
                    prefabRoot,
                    targetRenderer.bounds,
                    ref unrotatedPlacementBounds
                );
            }
        }

        localPlacementBounds = RotateLocalBounds(unrotatedPlacementBounds, quarterTurns);
        localPortalArrivalBounds = default;
        hasPortalArrivalBounds = false;

        float arrivalDiameter = Mathf.Max(0.1f, shopPortalArrivalClearanceRadius * 2f);
        ShopStructure[] shops = prefab.GetComponentsInChildren<ShopStructure>(true);
        for (int i = 0; i < shops.Length; i++)
        {
            ShopStructure shop = shops[i];
            if (shop == null || shop.PortalArrivalPoint == null)
            {
                continue;
            }

            Vector2 localArrival = ToRootRotationSpace(prefabRoot, shop.PortalArrivalPoint.position);
            localArrival = RotateLocalPoint(localArrival, quarterTurns);
            Bounds arrivalBounds = new Bounds(
                localArrival,
                new Vector3(arrivalDiameter, arrivalDiameter, 0f)
            );

            if (!hasPortalArrivalBounds)
            {
                localPortalArrivalBounds = arrivalBounds;
                hasPortalArrivalBounds = true;
            }
            else
            {
                localPortalArrivalBounds.Encapsulate(arrivalBounds.min);
                localPortalArrivalBounds.Encapsulate(arrivalBounds.max);
            }
        }
    }

    private static void EncapsulateWorldBoundsInRootSpace(
        Transform root,
        Bounds worldBounds,
        ref Bounds localBounds)
    {
        Vector3 minimum = worldBounds.min;
        Vector3 maximum = worldBounds.max;
        localBounds.Encapsulate(ToRootRotationSpace(root, new Vector3(minimum.x, minimum.y, 0f)));
        localBounds.Encapsulate(ToRootRotationSpace(root, new Vector3(minimum.x, maximum.y, 0f)));
        localBounds.Encapsulate(ToRootRotationSpace(root, new Vector3(maximum.x, minimum.y, 0f)));
        localBounds.Encapsulate(ToRootRotationSpace(root, new Vector3(maximum.x, maximum.y, 0f)));
    }

    private static Vector2 ToRootRotationSpace(Transform root, Vector3 worldPoint)
    {
        Vector3 relative = Quaternion.Inverse(root.rotation) * (worldPoint - root.position);
        return new Vector2(relative.x, relative.y);
    }

    private static bool IsPrefabHierarchyActive(Transform root, Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }

            if (current == root)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Bounds RotateLocalBounds(Bounds bounds, int quarterTurns)
    {
        Vector2 minimum = bounds.min;
        Vector2 maximum = bounds.max;
        Vector2 corner = RotateLocalPoint(new Vector2(minimum.x, minimum.y), quarterTurns);
        Bounds rotated = new Bounds(corner, Vector3.zero);
        rotated.Encapsulate(RotateLocalPoint(new Vector2(minimum.x, maximum.y), quarterTurns));
        rotated.Encapsulate(RotateLocalPoint(new Vector2(maximum.x, minimum.y), quarterTurns));
        rotated.Encapsulate(RotateLocalPoint(new Vector2(maximum.x, maximum.y), quarterTurns));
        return rotated;
    }

    private static Vector2 RotateLocalPoint(Vector2 point, int quarterTurns)
    {
        switch ((quarterTurns % 4 + 4) % 4)
        {
            case 1:
                return new Vector2(-point.y, point.x);
            case 2:
                return -point;
            case 3:
                return new Vector2(point.y, -point.x);
            default:
                return point;
        }
    }

    private static Bounds OffsetLocalBounds(Bounds localBounds, Vector2 rootPosition)
    {
        localBounds.center += (Vector3)rootPosition;
        return localBounds;
    }

    private void ReservePlacementBounds(Bounds bounds)
    {
        bounds.size = new Vector3(
            Mathf.Max(0.1f, bounds.size.x),
            Mathf.Max(0.1f, bounds.size.y),
            0f
        );
        reservedPlacementBounds.Add(bounds);
    }

    private bool IntersectsReservedPlacementBounds(Bounds candidate, float padding)
    {
        Bounds expandedCandidate = candidate;
        expandedCandidate.Expand(new Vector3(
            Mathf.Max(0f, padding) * 2f,
            Mathf.Max(0f, padding) * 2f,
            0f
        ));

        for (int i = 0; i < reservedPlacementBounds.Count; i++)
        {
            if (expandedCandidate.Intersects(reservedPlacementBounds[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointBlockedByReservedPlacement(Vector2 point, float padding)
    {
        for (int i = 0; i < reservedPlacementBounds.Count; i++)
        {
            Bounds bounds = reservedPlacementBounds[i];
            bounds.Expand(new Vector3(
                Mathf.Max(0f, padding) * 2f,
                Mathf.Max(0f, padding) * 2f,
                0f
            ));

            Vector3 point3D = new Vector3(point.x, point.y, bounds.center.z);
            if (bounds.Contains(point3D))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureGeneratedBasePortalDestinations()
    {
        if (!configureBasePortalsToNearestShop ||
            spawnedFieldBases.Count == 0 ||
            spawnedShopStructures.Count == 0)
        {
            return;
        }

        for (int baseIndex = 0; baseIndex < spawnedFieldBases.Count; baseIndex++)
        {
            FieldBaseController fieldBase = spawnedFieldBases[baseIndex];
            if (fieldBase == null)
            {
                continue;
            }

            ShopStructure nearestShop = null;
            float nearestSqrDistance = float.MaxValue;

            for (int shopIndex = 0; shopIndex < spawnedShopStructures.Count; shopIndex++)
            {
                ShopStructure shop = spawnedShopStructures[shopIndex];
                if (shop == null || shop.IsDead || shop.PortalArrivalPoint == null)
                {
                    continue;
                }

                float sqrDistance = (shop.PortalArrivalPoint.position - fieldBase.transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestShop = shop;
                }
            }

            if (nearestShop != null)
            {
                fieldBase.ConfigurePortalDestination(nearestShop.PortalArrivalPoint, Vector2.zero);
            }
        }
    }

    private FieldBaseController FindClosestGeneratedFieldBase(Vector2 position)
    {
        FieldBaseController closest = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < spawnedFieldBases.Count; i++)
        {
            FieldBaseController candidate = spawnedFieldBases[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 approachPosition = candidate.ResolveClosestCargoApproachPosition(position);
            float sqrDistance = ((Vector2)approachPosition - position).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void EnsureProgressionRuntimeSystems()
    {
        _ = ExpeditionObjectiveDirector.Instance;

        if (rewardChoiceUI == null)
        {
            rewardChoiceUI = FindFirstObjectByType<RunLevelTraitSelectionUI>();
        }

        if (rewardChoiceUI != null)
        {
            runtimeTraitCatalog = rewardChoiceUI.TraitCatalog;
            runtimeReinforcementCatalog = rewardChoiceUI.ReinforcementCatalog;
        }

        ShopStockController shopStock = FindFirstObjectByType<ShopStockController>();

        if (shopStock != null)
        {
            if (runtimeTraitCatalog == null)
            {
                runtimeTraitCatalog = shopStock.TraitCatalog;
            }

            if (runtimeReinforcementCatalog == null)
            {
                runtimeReinforcementCatalog = shopStock.ReinforcementCatalog;
            }
        }

        if (rewardChoiceUI != null)
        {
            rewardChoiceUI.ConfigureCatalogs(runtimeTraitCatalog, runtimeReinforcementCatalog);
        }
    }

    private void PlaceMiniTrader()
    {
        if (miniTraderPrefab == null)
        {
            Debug.LogError(
                "Region 2/3 Mini Trader generation requires a Field-NPC prefab.",
                this
            );
            return;
        }

        EnsureProgressionRuntimeSystems();

        if (!TryFindMiniTraderPosition(out Vector2 position, out Bounds reservationBounds))
        {
            Debug.LogError(
                "Mini Trader placement failed after bounded attempts. No unsafe placement was used.",
                this
            );
            return;
        }

        GameObject spawned = Spawn(miniTraderPrefab, position, "MiniTrader_00");
        if (spawned == null)
        {
            Debug.LogError("Mini Trader prefab failed to spawn.", this);
            return;
        }

        Collider2D[] colliders = spawned.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].isTrigger = true;
            }
        }

        ShopStockController stock = spawned.GetComponent<ShopStockController>();
        if (stock == null)
        {
            stock = spawned.AddComponent<ShopStockController>();
        }

        MiniTrader trader = spawned.GetComponent<MiniTrader>();
        if (trader == null)
        {
            trader = spawned.AddComponent<MiniTrader>();
        }

        FieldNpcObjective objective = spawned.GetComponent<FieldNpcObjective>();
        if (objective == null)
        {
            objective = spawned.AddComponent<FieldNpcObjective>();
        }

        RadarTarget radarTarget = spawned.GetComponent<RadarTarget>();
        if (radarTarget == null)
        {
            radarTarget = spawned.AddComponent<RadarTarget>();
        }

        TraitCatalog traderTraitCatalog = runtimeTraitCatalog != null
            ? runtimeTraitCatalog
            : objective.TraitCatalog;
        ReinforcementCatalog traderReinforcementCatalog = runtimeReinforcementCatalog != null
            ? runtimeReinforcementCatalog
            : objective.ReinforcementCatalog;

        trader.Configure(
            stock,
            traderTraitCatalog,
            traderReinforcementCatalog,
            miniTraderTraitOfferCount,
            miniTraderReinforcementOfferCount
        );
        objective.ConfigureAsMiniTrader(trader);

        radarTarget.SetMarkerType(RadarMarkerType.FieldNpc);
        Sprite markerSprite = fieldNpcRadarMarkerSprite != null
            ? fieldNpcRadarMarkerSprite
            : radarTarget.MarkerSprite;
        radarTarget.SetMarkerVisual(markerSprite, miniTraderMarkerColor, 1.15f);
        radarTarget.SetVisible(true);
        radarTarget.SetShowOnMap(true);
        radarTarget.SetMapDiscovered(false);

        currentMiniTrader = trader;
        currentMiniTraderReservationBounds = reservationBounds;
        ReservePlacementBounds(reservationBounds);
        occupiedPositions.Add(position);
        importantPositions.Add(position);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logGenerationResult)
        {
            Debug.Log(
                $"[Mini Trader] Region={ResolveCurrentDepth()} Spawned=True " +
                $"Position={position} UsingPrefab={miniTraderPrefab.name} " +
                $"TradingAuthority={nameof(ShopStockController)}",
                trader
            );
        }
#endif
    }

    private bool TryFindMiniTraderPosition(
        out Vector2 position,
        out Bounds reservationBounds)
    {
        Vector2 reservationSize = new Vector2(
            Mathf.Max(1f, miniTraderReservationSize.x),
            Mathf.Max(1f, miniTraderReservationSize.y)
        );
        Vector2 halfSize = reservationSize * 0.5f;
        Vector2 minimum = (Vector2)CameraSafeBounds.min + halfSize;
        Vector2 maximum = (Vector2)CameraSafeBounds.max - halfSize;
        float startSafeRadius = config != null ? config.StartSafeRadius : 10f;
        float importantClearance = Mathf.Max(0f, miniTraderImportantClearance);

        position = Vector2.zero;
        reservationBounds = default;

        if (minimum.x >= maximum.x || minimum.y >= maximum.y)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(minimum.x, maximum.x),
                UnityEngine.Random.Range(minimum.y, maximum.y)
            );
            Bounds candidateBounds = new Bounds(
                candidate,
                new Vector3(reservationSize.x, reservationSize.y, 0f)
            );
            Vector3 startPoint3D = new Vector3(
                startPosition.x,
                startPosition.y,
                candidateBounds.center.z
            );

            if (candidateBounds.SqrDistance(startPoint3D) < startSafeRadius * startSafeRadius)
            {
                continue;
            }

            if (!HasMinimumDistance(candidate, importantPositions, importantClearance))
            {
                continue;
            }

            if (IsAboveRegion2MajorPoiCeiling(candidateBounds.max.y) ||
                IntersectsRegion2BossCorridor(
                    candidateBounds,
                    miniTraderReservationPadding
                ))
            {
                continue;
            }

            if (IsRegion3PhaseGatekeeperMap())
            {
                float encounterClearance = Mathf.Max(8f, region3MiniTraderArenaClearance);
                Bounds encounterBounds = new Bounds(
                    currentRegion3BossPosition,
                    new Vector3(encounterClearance * 2f, encounterClearance * 2f, 0f)
                );

                if (candidateBounds.Intersects(encounterBounds))
                {
                    continue;
                }
            }

            if (IntersectsReservedPlacementBounds(
                    candidateBounds,
                    miniTraderReservationPadding))
            {
                continue;
            }

            if (useBlockedLayerCheck &&
                Physics2D.OverlapBox(
                    candidateBounds.center,
                    candidateBounds.size,
                    0f,
                    blockedLayer
                ) != null)
            {
                continue;
            }

            position = candidate;
            reservationBounds = candidateBounds;
            return true;
        }

        return false;
    }

    private void PlaceFieldNpcBatch(int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureProgressionRuntimeSystems();

        for (int i = 0; i < count; i++)
        {
            if (!TryFindPosition(
                    true,
                    true,
                    generalMinDistance,
                    out Vector2 position,
                    out _))
            {
                Debug.LogWarning("Field NPC 배치 실패. 배치 조건을 확인하세요.", this);
                continue;
            }

            GameObject prefab = PickPrefab(fieldNpcPrefabs, null);
            GameObject spawned = prefab != null
                ? Spawn(prefab, position, $"FieldNPC_{i:00}")
                : CreateFallbackFieldNpc(position, i);

            if (spawned == null)
            {
                continue;
            }

            ConfigureFieldNpc(spawned, i);
            occupiedPositions.Add(position);
            importantPositions.Add(position);
        }
    }

    private GameObject CreateFallbackFieldNpc(Vector2 position, int index)
    {
        if (!createFallbackFieldNpcWhenPrefabMissing)
        {
            return null;
        }

        GameObject npcObject = new GameObject($"FieldNPC_Fallback_{index:00}");
        npcObject.transform.SetParent(generatedRoot, false);
        npcObject.transform.position = position;

        CircleCollider2D collider = npcObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.8f;

        SpriteRenderer renderer = npcObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateFallbackNpcSprite();
        renderer.color = fallbackFieldNpcColor;
        renderer.sortingOrder = 3;

        return npcObject;
    }

    private void ConfigureFieldNpc(GameObject spawned, int index)
    {
        if (spawned == null)
        {
            return;
        }

        FieldNpcObjective npc = spawned.GetComponent<FieldNpcObjective>();

        if (npc == null)
        {
            npc = spawned.AddComponent<FieldNpcObjective>();
        }

        RadarTarget radar = spawned.GetComponent<RadarTarget>();

        if (radar == null)
        {
            radar = spawned.AddComponent<RadarTarget>();
        }

        radar.SetMarkerType(RadarMarkerType.FieldNpc);
        radar.SetMarkerVisual(fieldNpcRadarMarkerSprite, fallbackFieldNpcColor, 1.15f);

        FieldNpcServiceType serviceType = ResolveFieldNpcService(index);
        npc.Configure(
            serviceType,
            fallbackFieldNpcsRequireRescue,
            basicEnemyDefinition,
            shotgunEnemyDefinition,
            runtimeTraitCatalog,
            runtimeReinforcementCatalog,
            rewardChoiceUI,
            $"field_npc_{index}_{spawned.GetInstanceID()}",
            fieldNpcRewardCapsulePrefab
        );
    }

    private FieldNpcServiceType ResolveFieldNpcService(int index)
    {
        if (fallbackNpcServiceOrder == null || fallbackNpcServiceOrder.Length <= 0)
        {
            return index % 2 == 0
                ? FieldNpcServiceType.Technician
                : FieldNpcServiceType.RescueContact;
        }

        int safeIndex = Mathf.Abs(index) % fallbackNpcServiceOrder.Length;
        return fallbackNpcServiceOrder[safeIndex];
    }

    private static Sprite GetOrCreateFallbackNpcSprite()
    {
        if (runtimeFallbackNpcSprite != null)
        {
            return runtimeFallbackNpcSprite;
        }

        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime_FieldNPC_Fallback",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool body = x >= 3 && x <= 12 && y >= 2 && y <= 13;
                bool notch = (x <= 5 || x >= 10) && y <= 4;
                pixels[y * size + x] = body && !notch ? solid : clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        runtimeFallbackNpcSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            16f
        );
        runtimeFallbackNpcSprite.name = "Runtime_FieldNPC_Fallback_Sprite";
        runtimeFallbackNpcSprite.hideFlags = HideFlags.HideAndDontSave;
        return runtimeFallbackNpcSprite;
    }

    private void PlaceHarvestObjects()
    {
        int highValueWreckCount = config != null ? config.HighValueWreckCount : 3;
        int supplyContainerCount = config != null ? config.SupplyContainerCount : 16;
        int destroyedHullCount = config != null ? config.DestroyedHullCount : 8;
        int smallMeteorCount = config != null ? config.SmallMeteorCount : 30;
        int largeMeteorCount = config != null ? config.LargeMeteorCount : 3;
        int activeContainerCount = Mathf.Max(0, specialActiveContainerCount);
        int passiveContainerCount = Mathf.Max(0, specialPassiveContainerCount);
        bool region3BossFocused = IsRegion3PhaseGatekeeperMap();

        if (region3BossFocused)
        {
            highValueWreckCount = Mathf.Max(0, region3HighValueWreckCount);
            supplyContainerCount = Mathf.Max(0, region3SupplyContainerCount);
            destroyedHullCount = Mathf.Max(0, region3DestroyedHullCount);
            smallMeteorCount = Mathf.Max(0, region3SmallMeteorCount);
            largeMeteorCount = 0;
            activeContainerCount = 0;
            passiveContainerCount = 0;
        }
        else if (applySeaRegionObjectCountModifiers && currentSeaRegion != null)
        {
            highValueWreckCount = ApplyCountModifier(highValueWreckCount, currentSeaRegion.ExtraHighValueWreckCount);
            supplyContainerCount = ApplyCountModifier(supplyContainerCount, currentSeaRegion.ExtraSupplyContainerCount);
            destroyedHullCount = ApplyCountModifier(destroyedHullCount, currentSeaRegion.ExtraDestroyedHullCount);
            // 해역의 추가 운석 수는 탐색 밀도를 만드는 소형 운석에만 적용한다.
            smallMeteorCount = ApplyCountModifier(smallMeteorCount, currentSeaRegion.ExtraMeteorCount);
        }

        PlaceHarvestPrefabBatchDistributed(
            specialActiveContainerPrefabs,
            specialActiveContainerPrefab,
            activeContainerCount,
            generalMinDistance,
            "SpecialActiveContainer",
            MapSpawnCategory.SpecialActiveContainer,
            1f,
            true
        );

        PlaceHarvestPrefabBatchDistributed(
            specialPassiveContainerPrefabs,
            specialPassiveContainerPrefab,
            passiveContainerCount,
            generalMinDistance,
            "SpecialPassiveContainer",
            MapSpawnCategory.SpecialPassiveContainer,
            1f,
            true
        );

        PlaceHarvestPrefabBatchDistributed(
            highValueWreckPrefabs,
            highValueWreckPrefab,
            highValueWreckCount,
            generalMinDistance,
            "HighValueWreck",
            MapSpawnCategory.HighValueWreck,
            1f,
            true
        );

        float clusteredHarvestRatio = config != null ? config.ClusteredHarvestRatio : 0.75f;

        PlaceHarvestPrefabBatchDistributed(
            supplyContainerPrefabs,
            supplyContainerPrefab,
            supplyContainerCount,
            generalMinDistance,
            "SupplyContainer",
            MapSpawnCategory.SupplyContainer,
            clusteredHarvestRatio,
            false
        );

        PlaceHarvestPrefabBatchDistributed(
            destroyedHullPrefabs,
            destroyedHullPrefab,
            destroyedHullCount,
            generalMinDistance,
            "DestroyedHull",
            MapSpawnCategory.DestroyedHull,
            clusteredHarvestRatio,
            false
        );

        PlaceHarvestPrefabBatchDistributed(
            meteorPrefabs,
            meteorPrefab,
            smallMeteorCount,
            generalMinDistance,
            "SmallMeteor",
            MapSpawnCategory.Meteor,
            0.58f,
            false
        );

        PlaceHarvestPrefabBatchDistributed(
            largeMeteorPrefabs,
            largeMeteorPrefab,
            largeMeteorCount,
            Mathf.Max(generalMinDistance, 3f),
            "LargeMeteor",
            MapSpawnCategory.LargeMeteor,
            0f,
            false
        );
    }

    private void PlaceEnemies()
    {
        if (IsRegion3PhaseGatekeeperMap())
        {
            return;
        }

        int basicCount = config != null ? config.BasicEnemyCount : 20;
        int shotgunCount = config != null ? config.ShotgunEnemyCount : 5;
        int chargingCount = config != null ? config.ChargingEnemyCount : 4;
        int meleeChargerCount = config != null ? config.MeleeChargerCount : 2;
        int eliteMachineGunCount = config != null ? config.EliteMachineGunCount : 1;
        int eliteShotgunCount = config != null ? config.EliteShotgunCount : 1;
        int eliteChargingCount = config != null ? config.EliteChargingCount : 0;

        ApplyCampaignEnemyCountModifiers(
            ResolveCurrentDepth(),
            ref basicCount,
            ref shotgunCount,
            ref chargingCount,
            ref meleeChargerCount,
            ref eliteMachineGunCount,
            ref eliteShotgunCount,
            ref eliteChargingCount
        );

        if (applySeaRegionEnemyCountModifiers && currentSeaRegion != null)
        {
            basicCount = ApplyCountModifier(basicCount, currentSeaRegion.ExtraBasicEnemyCount);
            shotgunCount = ApplyCountModifier(shotgunCount, currentSeaRegion.ExtraShotgunEnemyCount);
            chargingCount = ApplyCountModifier(chargingCount, currentSeaRegion.ExtraChargingEnemyCount);

            DistributeEliteBonus(
                currentSeaRegion.ExtraEliteEnemyCount,
                ref eliteMachineGunCount,
                ref eliteShotgunCount,
                ref eliteChargingCount
            );
        }

        bool roleEnabled = useEnemyRoles && (config == null || config.EnableEnemyRoles);

        if (roleEnabled)
        {
            int defenderBasicRequest = config != null
                ? config.DefenderBasicCount
                : Mathf.Max(0, fallbackDefenderBasicCount);

            int defenderShotgunRequest = config != null
                ? config.DefenderShotgunCount
                : Mathf.Max(0, fallbackDefenderShotgunCount);

            int rivalRequest = config != null
                ? config.RivalHarvesterCount
                : Mathf.Max(0, fallbackRivalHarvesterCount);

            int scavengerRequest = config != null
                ? config.ScavengerCount
                : Mathf.Max(0, fallbackScavengerCount);

            int placedDefenderBasic = PlaceDefenderBatch(
                basicEnemyDefinition,
                defenderBasicPrefab,
                Mathf.Min(defenderBasicRequest, basicCount),
                "DefenderBasic"
            );
            basicCount -= placedDefenderBasic;

            int placedDefenderShotgun = PlaceDefenderBatch(
                shotgunEnemyDefinition,
                defenderShotgunPrefab,
                Mathf.Min(defenderShotgunRequest, shotgunCount),
                "DefenderShotgun"
            );
            shotgunCount -= placedDefenderShotgun;

            EnemyDefinition resolvedRivalDefinition = rivalHarvesterDefinition != null
                ? rivalHarvesterDefinition
                : basicEnemyDefinition;
            EnemyDefinition resolvedScavengerDefinition = scavengerDefinition != null
                ? scavengerDefinition
                : basicEnemyDefinition;

            int placedRivals = PlaceRoleEnemyBatch(
                resolvedRivalDefinition,
                Mathf.Min(rivalRequest, basicCount),
                EnemyRoleType.RivalHarvester,
                "RivalHarvester"
            );
            basicCount -= placedRivals;

            int placedScavengers = PlaceRoleEnemyBatch(
                resolvedScavengerDefinition,
                Mathf.Min(scavengerRequest, basicCount),
                EnemyRoleType.Scavenger,
                "Scavenger"
            );
            basicCount -= placedScavengers;
        }

        float clusteredEnemyRatio = config != null ? config.ClusteredEnemyRatio : 0.75f;
        float clusteredThreat =
            GetClusteredCount(basicCount, clusteredEnemyRatio) +
            GetClusteredCount(shotgunCount, clusteredEnemyRatio) * 1.2f +
            GetClusteredCount(chargingCount, clusteredEnemyRatio) * 1.3f +
            GetClusteredCount(meleeChargerCount, clusteredEnemyRatio) * 1.4f +
            GetClusteredCount(eliteMachineGunCount, clusteredEnemyRatio) * 2f +
            GetClusteredCount(eliteShotgunCount, clusteredEnemyRatio) * 2f +
            GetClusteredCount(eliteChargingCount, clusteredEnemyRatio) * 2f;

        PreparePoiThreatBudgets(clusteredThreat);

        PlaceEnemyBatchDistributed(
            basicEnemyDefinition,
            basicCount,
            false,
            "BasicEnemy",
            EnemyPoiPreference.AnySalvage,
            1f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            shotgunEnemyDefinition,
            shotgunCount,
            true,
            "ShotgunEnemy",
            EnemyPoiPreference.HighValueOnly,
            1.2f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            chargingEnemyDefinition,
            chargingCount,
            true,
            "ChargingEnemy",
            EnemyPoiPreference.HighValueOnly,
            1.3f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            meleeChargerDefinition,
            meleeChargerCount,
            true,
            "MeleeCharger",
            EnemyPoiPreference.HighValueOnly,
            1.4f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            eliteMachineGunDefinition,
            eliteMachineGunCount,
            true,
            "EliteMachineGun",
            EnemyPoiPreference.HighValueOnly,
            2f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            eliteShotgunDefinition,
            eliteShotgunCount,
            true,
            "EliteShotgun",
            EnemyPoiPreference.HighValueOnly,
            2f,
            clusteredEnemyRatio
        );

        PlaceEnemyBatchDistributed(
            eliteChargingDefinition,
            eliteChargingCount,
            true,
            "EliteCharging",
            EnemyPoiPreference.HighValueOnly,
            2f,
            clusteredEnemyRatio
        );
    }

    private static int GetClusteredCount(int count, float ratio)
    {
        return Mathf.Clamp(Mathf.RoundToInt(count * Mathf.Clamp01(ratio)), 0, count);
    }

    private int ApplyCountModifier(int baseCount, int flatBonus)
    {
        return Mathf.Max(0, baseCount + flatBonus);
    }

    private void PlaceCoreBatch(int count)
    {
        if (corePrefab == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            bool useRegion2TopCorePlacement = IsRegion2SalvageDevourerMap();
            Vector2 position;
            bool found = useRegion2TopCorePlacement
                ? TryFindRegion2TopCorePosition(generalMinDistance, out position)
                : TryFindCorePosition(
                    generalMinDistance,
                    out position,
                    out _
                );

            if (!found && !useRegion2TopCorePlacement)
            {
                found = TryFindBestCoreFallbackPosition(out position);

                if (found)
                {
                    Debug.LogWarning(
                        "코어의 중요 지점 최소 거리를 완전히 만족하지 못해, 보스 카메라 안전 영역 안의 최선 위치를 사용합니다.",
                        this
                    );
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "코어 배치 실패. 맵 크기, 보스 줌 배율 또는 코어 안전 여백을 확인하세요.",
                    this
                );
                continue;
            }

            GameObject spawned = Spawn(corePrefab, position, $"Core_{i:00}");

            if (spawned == null)
            {
                continue;
            }

            CoreObject spawnedCore = spawned.GetComponent<CoreObject>();
            spawnedCore ??= spawned.GetComponentInChildren<CoreObject>(true);
            if (spawnedCore != null)
            {
                spawnedCoreObjects.Add(spawnedCore);
            }

            occupiedPositions.Add(position);
            importantPositions.Add(position);

            if (useRegion2TopCorePlacement)
            {
                hasRegion2CoreFinalPosition = true;
                region2CoreFinalPosition = position;
                ConfigureRegion2BossCorridor(position);
                CreateRegion2BossCorridorRuntime();
            }

            PlaceBossArenaCoverMeteors(position, i);

            if (reserveBossArenaFromOtherSpawns)
            {
                Vector2 arenaHalfExtents = config != null
                    ? config.BossArenaHalfExtents
                    : new Vector2(14f, 9.8f);

                ReservePlacementBounds(new Bounds(
                    position,
                    new Vector3(
                        Mathf.Max(1f, arenaHalfExtents.x * 2f),
                        Mathf.Max(1f, arenaHalfExtents.y * 2f),
                        0f
                    )
                ));
            }
        }
    }

    private void PlaceBossArenaCoverMeteors(Vector2 arenaCenter, int coreIndex)
    {
        if (IsRegion2SalvageDevourerMap())
        {
            return;
        }

        bool shouldSpawnCovers = config == null || config.SpawnBossArenaCoverMeteors;
        if (!shouldSpawnCovers || !HasValidPrefab(largeMeteorPrefabs, largeMeteorPrefab))
        {
            return;
        }

        Vector2 configuredOffset = config != null
            ? config.BossArenaCoverOffset
            : new Vector2(6.5f, -0.75f);

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            bool leftSide = sideIndex == 0;
            Vector2 sideOffset = new Vector2(
                configuredOffset.x * (leftSide ? -1f : 1f),
                configuredOffset.y
            );
            Vector2 coverPosition = arenaCenter + sideOffset;
            GameObject coverPrefab = PickPrefab(largeMeteorPrefabs, largeMeteorPrefab);
            string sideName = leftSide ? "Left" : "Right";
            GameObject cover = Spawn(
                coverPrefab,
                coverPosition,
                $"BossArenaCover_{sideName}_{coreIndex:00}",
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f))
            );

            if (cover == null)
            {
                if (logGenerationResult)
                {
                    Debug.LogWarning($"Boss arena {sideName.ToLowerInvariant()} cover Meteor failed to spawn.", this);
                }

                continue;
            }

            ApplySpawnVariation(cover, MapSpawnCategory.LargeMeteor);
            ConfigureSpawnedObject(cover, MapSpawnCategory.LargeMeteor);

            MeteorObstacle meteor = cover.GetComponentInChildren<MeteorObstacle>(true);
            meteor?.SetRuntimeDriftEnabled(false);

            BossArenaCover arenaCover = cover.GetComponent<BossArenaCover>();
            if (arenaCover == null)
            {
                arenaCover = cover.AddComponent<BossArenaCover>();
            }

            arenaCover.Configure(
                leftSide ? BossArenaCoverSide.Left : BossArenaCoverSide.Right,
                meteor
            );
            arenaCover.SetEncounterProtection(true);

            occupiedPositions.Add(coverPosition);
            ReserveBossCoverBounds(cover, coverPosition);
        }
    }

    private void ReserveBossCoverBounds(GameObject cover, Vector2 fallbackPosition)
    {
        Collider2D coverCollider = cover != null
            ? cover.GetComponentInChildren<Collider2D>(true)
            : null;
        Bounds coverBounds = coverCollider != null
            ? coverCollider.bounds
            : new Bounds(fallbackPosition, new Vector3(3f, 3f, 0f));

        coverBounds.Expand(new Vector3(2f, 2f, 0f));
        ReservePlacementBounds(coverBounds);
    }

    private bool TryFindRegion2TopCorePosition(float minDistance, out Vector2 position)
    {
        Bounds safeBounds = ResolveCorePlacementSafeBounds(mapSize);
        CorePlacementSafeBounds = safeBounds;

        Vector2 normalizedRange = config != null
            ? config.Region2TopCoreHorizontalNormalizedRange
            : new Vector2(-0.1f, 0.1f);
        float normalizedX = UnityEngine.Random.Range(normalizedRange.x, normalizedRange.y);
        float safetyMargin = config != null ? config.Region2TopCoreSafetyMargin : 4f;

        position = new Vector2(
            safeBounds.center.x + normalizedX * safeBounds.extents.x,
            Mathf.Clamp(safeBounds.max.y - safetyMargin, safeBounds.min.y, safeBounds.max.y)
        );
        hasRegion2CoreCandidate = true;
        region2CoreCandidatePosition = position;

        if (IsPositionValid(position, true, true, minDistance, out _))
        {
            return true;
        }

        position.x = safeBounds.center.x;
        region2CoreCandidatePosition = position;
        return IsPositionValid(position, true, true, minDistance, out _);
    }

    private void ConfigureRegion2BossCorridor(Vector2 corePosition)
    {
        float halfWidth = config != null ? config.Region2BossCorridorHalfWidth : 9f;
        float downwardLength = config != null ? config.Region2BossCorridorDownwardLength : 48f;
        float reservationPadding = config != null
            ? config.Region2BossCorridorReservationPadding
            : 2f;
        float bottomLimit = MapBounds.min.y + Mathf.Max(0f, edgePadding);
        float bottomY = Mathf.Max(bottomLimit, corePosition.y - downwardLength);
        float actualLength = Mathf.Max(1f, corePosition.y - bottomY);
        Vector2 corridorCenter = new Vector2(
            corePosition.x,
            corePosition.y - actualLength * 0.5f
        );
        Bounds corridorBounds = new Bounds(
            corridorCenter,
            new Vector3(halfWidth * 2f, actualLength, 0f)
        );
        Bounds reservedBounds = corridorBounds;
        reservedBounds.Expand(new Vector3(
            reservationPadding * 2f,
            reservationPadding * 2f,
            0f
        ));

        region2BossCorridor = new Region2BossCorridorData(
            corePosition.x,
            corePosition.y,
            bottomY,
            halfWidth,
            corridorBounds,
            reservedBounds
        );
        hasRegion2BossCorridor = true;
        ReservePlacementBounds(reservedBounds);
    }

    private void CreateRegion2BossCorridorRuntime()
    {
        if (!hasRegion2BossCorridor || currentRegion2BossCorridorRuntime != null)
        {
            return;
        }

        if (region2BossCorridorRuntimePrefab == null)
        {
            Debug.LogWarning(
                "Region-2 Boss corridor geometry was generated, but the " +
                "SalvageDevourerCorridorController prefab is not assigned.",
                this
            );
            return;
        }

        currentRegion2BossCorridorRuntime = Instantiate(
            region2BossCorridorRuntimePrefab,
            generatedRoot
        );
        currentRegion2BossCorridorRuntime.name = "Region2_SalvageDevourer_CorridorRuntime";
        currentRegion2BossCorridorRuntime.Initialize(this);
    }

    private bool IntersectsRegion2BossCorridor(Bounds candidateBounds, float padding)
    {
        if (!hasRegion2BossCorridor)
        {
            return false;
        }

        candidateBounds.Expand(new Vector3(
            Mathf.Max(0f, padding) * 2f,
            Mathf.Max(0f, padding) * 2f,
            0f
        ));
        return candidateBounds.Intersects(region2BossCorridor.ReservedBounds);
    }

    private bool IsAboveRegion2MajorPoiCeiling(float maximumY)
    {
        if (!hasRegion2CoreFinalPosition)
        {
            return false;
        }

        float clearance = config != null ? config.Region2MajorPoiVerticalClearance : 3f;
        return maximumY > region2CoreFinalPosition.y - clearance;
    }

    private bool IsRegion2SalvageDevourerMap()
    {
        ExpeditionDepth depth = ResolveCurrentDepth();
        return depth == ExpeditionDepth.DeepZone1 &&
               CampaignProgressionCatalog.GetBossId(depth) == CampaignBossId.SalvageDevourer;
    }

    private bool IsRegion3PhaseGatekeeperMap()
    {
        ExpeditionDepth depth = ResolveCurrentDepth();
        return depth == ExpeditionDepth.DeepZone2 &&
               CampaignProgressionCatalog.GetBossId(depth) == CampaignBossId.PhaseGatekeeper;
    }

    private bool TryFindCorePosition(
        float minDistance,
        out Vector2 position,
        out bool insideStartSafeRadius)
    {
        if (config == null || !config.KeepCoreInsideBossCameraSafeArea)
        {
            return TryFindPosition(
                true,
                true,
                minDistance,
                out position,
                out insideStartSafeRadius
            );
        }

        Bounds safeBounds = ResolveCorePlacementSafeBounds(mapSize);
        CorePlacementSafeBounds = safeBounds;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = GetRandomPointInBounds(safeBounds);

            if (IsPositionValid(
                    position,
                    true,
                    true,
                    minDistance,
                    out insideStartSafeRadius))
            {
                return true;
            }
        }

        position = Vector2.zero;
        insideStartSafeRadius = false;
        return false;
    }

    private bool TryFindBestCoreFallbackPosition(out Vector2 position)
    {
        Bounds safeBounds = ResolveCorePlacementSafeBounds(mapSize);
        CorePlacementSafeBounds = safeBounds;

        float bestScore = float.NegativeInfinity;
        Vector2 bestPosition = Vector2.zero;
        bool found = false;
        int sampleCount = Mathf.Max(128, maxPlacementAttempts);

        for (int i = 0; i < sampleCount; i++)
        {
            Vector2 candidate = GetRandomPointInBounds(safeBounds);

            if (!IsPositionValid(
                    candidate,
                    true,
                    false,
                    0.25f,
                    out _))
            {
                continue;
            }

            float score = Vector2.Distance(candidate, startPosition);

            for (int importantIndex = 0; importantIndex < importantPositions.Count; importantIndex++)
            {
                score = Mathf.Min(
                    score,
                    Vector2.Distance(candidate, importantPositions[importantIndex])
                );
            }

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestPosition = candidate;
            }
        }

        position = bestPosition;
        return found;
    }

    private Bounds ResolveCorePlacementSafeBounds(Vector2 sourceMapSize)
    {
        Vector2 mapHalfSize = new Vector2(
            Mathf.Max(0.5f, sourceMapSize.x * 0.5f),
            Mathf.Max(0.5f, sourceMapSize.y * 0.5f)
        );

        if (config == null || !config.KeepCoreInsideBossCameraSafeArea)
        {
            Vector2 regularHalf = new Vector2(
                Mathf.Max(0.1f, mapHalfSize.x - edgePadding),
                Mathf.Max(0.1f, mapHalfSize.y - edgePadding)
            );

            return new Bounds(
                Vector3.zero,
                new Vector3(regularHalf.x * 2f, regularHalf.y * 2f, 0f)
            );
        }

        Vector2 requiredMargin = ResolveBossCameraRequiredMargin();
        Vector2 allowedHalfSize = new Vector2(
            Mathf.Max(0.1f, mapHalfSize.x - edgePadding - requiredMargin.x),
            Mathf.Max(0.1f, mapHalfSize.y - edgePadding - requiredMargin.y)
        );

        return new Bounds(
            Vector3.zero,
            new Vector3(allowedHalfSize.x * 2f, allowedHalfSize.y * 2f, 0f)
        );
    }

    private Vector2 ResolveBossCameraRequiredMargin()
    {
        Vector2 arenaHalfExtents = config != null
            ? config.BossArenaHalfExtents
            : new Vector2(14f, 9.8f);

        float baseOrthographicSize = config != null
            ? config.FallbackBossCameraBaseOrthographicSize
            : 4.2f;

        float aspect = config != null
            ? config.FallbackBossCameraAspect
            : 1.7777778f;

        Camera mainCamera = Camera.main;

        if (mainCamera != null && mainCamera.orthographic)
        {
            baseOrthographicSize = Mathf.Max(0.1f, mainCamera.orthographicSize);
            aspect = Mathf.Max(0.1f, mainCamera.aspect);
        }

        CameraZoomController2D zoomController = FindFirstObjectByType<CameraZoomController2D>();

        if (zoomController != null)
        {
            baseOrthographicSize = Mathf.Max(0.1f, zoomController.BaseOrthographicSize);
        }

        float maxZoomMultiplier = config != null
            ? config.BossIntroMaxZoomMultiplier
            : 3.5f;

        float cameraHalfHeight = baseOrthographicSize * Mathf.Max(1f, maxZoomMultiplier);
        float cameraHalfWidth = cameraHalfHeight * aspect;
        Vector2 cameraHalfExtents = new Vector2(cameraHalfWidth, cameraHalfHeight);

        Vector2 required = new Vector2(
            Mathf.Max(arenaHalfExtents.x, cameraHalfExtents.x),
            Mathf.Max(arenaHalfExtents.y, cameraHalfExtents.y)
        );

        float padding = config != null ? config.BossCameraEdgePadding : 2.5f;
        required += Vector2.one * Mathf.Max(0f, padding);

        return required;
    }

    private Vector2 GetRandomPointInBounds(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new Vector2(
            UnityEngine.Random.Range(min.x, max.x),
            UnityEngine.Random.Range(min.y, max.y)
        );
    }

    private void PlacePrefabBatch(
        GameObject prefab,
        int count,
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        string label)
    {
        PlacePrefabBatch(
            null,
            prefab,
            count,
            avoidStartSafeRadius,
            importantPoint,
            minDistance,
            label,
            MapSpawnCategory.None
        );
    }

    private void PlaceHarvestPrefabBatch(
        GameObject[] prefabs,
        GameObject fallbackPrefab,
        int count,
        float minDistance,
        string label,
        MapSpawnCategory category)
    {
        PlacePrefabBatch(
            prefabs,
            fallbackPrefab,
            count,
            false,
            false,
            minDistance,
            label,
            category
        );
    }

    private void PlaceHarvestPrefabBatchDistributed(
        GameObject[] prefabs,
        GameObject fallbackPrefab,
        int count,
        float minDistance,
        string label,
        MapSpawnCategory category,
        float clusteredRatio,
        bool highValueOnly)
    {
        if (!HasValidPrefab(prefabs, fallbackPrefab) || count <= 0)
        {
            return;
        }

        bool usePoiLayout = config != null && config.EnablePoiClusterLayout;
        if (!usePoiLayout)
        {
            PlaceHarvestPrefabBatch(prefabs, fallbackPrefab, count, minDistance, label, category);
            return;
        }

        int clusteredTarget = Mathf.Clamp(
            Mathf.RoundToInt(count * Mathf.Clamp01(clusteredRatio)),
            0,
            count
        );

        for (int i = 0; i < count; i++)
        {
            bool requestedCluster = i < clusteredTarget;
            Vector2 position = Vector2.zero;
            PoiAnchor assignedPoi = null;
            bool enforceMajorPoiCeiling = IsRegion2MajorContentCategory(category);
            bool clustered = requestedCluster && TryFindPositionNearPoi(
                highValueOnly,
                minDistance,
                enforceMajorPoiCeiling,
                out position,
                out assignedPoi
            );

            if (!clustered && !TryFindScatteredPosition(category, minDistance, out position))
            {
                if (logGenerationResult)
                {
                    Debug.LogWarning($"{label} placement failed. Requested: {count}, placed: {i}", this);
                }

                poiPlacementFallbackUsed = true;
                continue;
            }

            if (requestedCluster && !clustered)
            {
                poiPlacementFallbackUsed = true;
            }

            GameObject prefab = PickPrefab(prefabs, fallbackPrefab);
            GameObject spawned = Spawn(prefab, position, $"{label}_{i:00}");

            if (spawned == null)
            {
                continue;
            }

            ApplySpawnVariation(spawned, category);
            ConfigureSpawnedObject(spawned, category);
            occupiedPositions.Add(position);

            if (clustered)
            {
                assignedPoi.AssignedHarvest++;
                if (IsHarvestContentCategory(category))
                {
                    clusteredHarvestPlaced++;
                }
            }
            else if (IsHarvestContentCategory(category))
            {
                scatteredHarvestPlaced++;
            }
        }
    }

    private bool TryFindPositionNearPoi(
        bool highValueOnly,
        float minDistance,
        bool enforceMajorPoiCeiling,
        out Vector2 position,
        out PoiAnchor assignedPoi)
    {
        position = Vector2.zero;
        assignedPoi = SelectPoiForHarvest(highValueOnly);

        if (assignedPoi == null)
        {
            return false;
        }

        float innerRadius = Mathf.Min(2f, assignedPoi.Radius * 0.3f);

        for (int attempt = 0; attempt < Mathf.Max(clusterCandidateAttempts, 12); attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            float distance = Mathf.Sqrt(UnityEngine.Random.value) *
                Mathf.Max(0.1f, assignedPoi.Radius - innerRadius) + innerRadius;
            Vector2 candidate = assignedPoi.Center + direction * distance;

            if (IsPositionValid(
                    candidate,
                    true,
                    false,
                    minDistance,
                    out _,
                    enforceMajorPoiCeiling))
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private PoiAnchor SelectPoiForHarvest(bool highValueOnly)
    {
        PoiAnchor selected = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            bool eligible = highValueOnly
                ? poi.Type == PoiType.HighValueSalvage
                : poi.Type == PoiType.SalvageField || poi.Type == PoiType.HighValueSalvage;

            if (!eligible)
            {
                continue;
            }

            float score = poi.AssignedHarvest + UnityEngine.Random.value * 0.25f;
            if (score < bestScore)
            {
                selected = poi;
                bestScore = score;
            }
        }

        return selected;
    }

    private bool TryFindScatteredPosition(
        MapSpawnCategory category,
        float minDistance,
        out Vector2 position)
    {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 candidate = GetRandomPointInMap();
            bool avoidStart = category == MapSpawnCategory.LargeMeteor;

            if (!IsPositionValid(
                    candidate,
                    avoidStart,
                    false,
                    minDistance,
                    out _,
                    IsRegion2MajorContentCategory(category)))
            {
                continue;
            }

            if (category == MapSpawnCategory.LargeMeteor && !IsOutsidePoiApproach(candidate, 2f))
            {
                continue;
            }

            position = candidate;
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    private bool IsOutsidePoiApproach(Vector2 position, float padding)
    {
        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            float exclusionRadius = poi.Radius + Mathf.Max(0f, padding);

            if ((position - poi.Center).sqrMagnitude < exclusionRadius * exclusionRadius)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHarvestContentCategory(MapSpawnCategory category)
    {
        return category == MapSpawnCategory.HighValueWreck ||
            category == MapSpawnCategory.SupplyContainer ||
            category == MapSpawnCategory.DestroyedHull ||
            category == MapSpawnCategory.SpecialActiveContainer ||
            category == MapSpawnCategory.SpecialPassiveContainer;
    }

    private static bool IsRegion2MajorContentCategory(MapSpawnCategory category)
    {
        return category == MapSpawnCategory.HighValueWreck ||
               category == MapSpawnCategory.SpecialActiveContainer ||
               category == MapSpawnCategory.SpecialPassiveContainer;
    }

    private void PlacePrefabBatch(
        GameObject[] prefabs,
        GameObject fallbackPrefab,
        int count,
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        string label,
        MapSpawnCategory category)
    {
        if (!HasValidPrefab(prefabs, fallbackPrefab) || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 position;
            bool insideStartSafeRadius;

            bool useHarvestPlacement =
                category != MapSpawnCategory.None &&
                category != MapSpawnCategory.LargeMeteor;

            bool found = useHarvestPlacement
                ? TryFindHarvestPosition(
                    minDistance,
                    IsRegion2MajorContentCategory(category),
                    out position,
                    out insideStartSafeRadius
                )
                : TryFindPosition(
                    avoidStartSafeRadius || category == MapSpawnCategory.LargeMeteor,
                    importantPoint,
                    minDistance,
                    out position,
                    out insideStartSafeRadius
                );

            if (!found)
            {
                Debug.LogWarning($"{label} 배치 실패. 배치 조건이 너무 빡빡할 수 있습니다.", this);
                continue;
            }

            GameObject prefab = PickPrefab(prefabs, fallbackPrefab);
            GameObject spawned = Spawn(prefab, position, $"{label}_{i:00}");

            if (spawned == null)
            {
                continue;
            }

            ApplySpawnVariation(spawned, category);
            ConfigureSpawnedObject(spawned, category);

            occupiedPositions.Add(position);

            if (importantPoint)
            {
                importantPositions.Add(position);
            }

            if ((config == null || !config.EnablePoiClusterLayout) &&
                category != MapSpawnCategory.None &&
                category != MapSpawnCategory.LargeMeteor)
            {
                harvestClusterAnchors.Add(position);
            }
        }
    }

    private void PlaceEnemyBatch(
        EnemyDefinition definition,
        int count,
        bool avoidStartSafeRadius,
        string label)
    {
        if (definition == null || definition.EnemyPrefab == null || count <= 0)
        {
            return;
        }

        int placed = 0;
        int attempts = 0;
        int maxTotalAttempts = Mathf.Max(maxPlacementAttempts, count * maxPlacementAttempts);

        while (placed < count && attempts < maxTotalAttempts)
        {
            attempts++;

            bool found = TryFindPosition(
                avoidStartSafeRadius,
                false,
                enemyMinDistance,
                out Vector2 position,
                out bool insideStartSafeRadius
            );

            if (!found)
            {
                break;
            }

            if (insideStartSafeRadius)
            {
                if (basicEnemiesInsideStartSafeRadius >= maxBasicEnemiesInsideStartSafeRadius)
                {
                    continue;
                }

                basicEnemiesInsideStartSafeRadius++;
            }

            GameObject spawned = Spawn(definition.EnemyPrefab, position, $"{label}_{placed:00}");

            if (spawned == null)
            {
                continue;
            }

            ConfigureHostileRadarTarget(spawned);

            if (definition.EnemyType == EnemyType.MeleeCharger &&
                spawned.GetComponent<EnemyMeleeChargeController2D>() == null)
            {
                spawned.AddComponent<EnemyMeleeChargeController2D>();
            }

            EnemyBaseAI enemyAI = spawned.GetComponent<EnemyBaseAI>();

            if (enemyAI != null)
            {
                enemyAI.ApplyDefinition(definition);

                if (player != null)
                {
                    enemyAI.SetTarget(player);
                }
            }

            ApplyEnemyHpModifiers(spawned);
            BeginEnemyArrival(spawned, position);
            occupiedPositions.Add(position);
            placed++;
        }

        if (placed < count)
        {
            Debug.LogWarning($"{label} 일부 배치 실패. 요청: {count}, 배치: {placed}", this);
        }
    }

    private void PreparePoiThreatBudgets(float totalThreat)
    {
        float totalWeight = 0f;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            poi.AssignedThreat = 0f;
            poi.ThreatCapacity = 0f;

            if (poi.Type == PoiType.SalvageField)
            {
                totalWeight += poi.IsLowRisk ? 0.55f : 1f;
            }
            else if (poi.Type == PoiType.HighValueSalvage)
            {
                totalWeight += 3f;
            }
        }

        if (totalWeight <= 0f || totalThreat <= 0f)
        {
            return;
        }

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            float weight = poi.Type switch
            {
                PoiType.SalvageField => poi.IsLowRisk ? 0.55f : 1f,
                PoiType.HighValueSalvage => 3f,
                _ => 0f
            };

            // Small packing slack prevents fractional budgets from forcing otherwise valid
            // enemies into transit space while the weighted distribution still limits density.
            poi.ThreatCapacity = totalThreat * weight / totalWeight + 0.75f;
        }
    }

    private void PlaceEnemyBatchDistributed(
        EnemyDefinition definition,
        int count,
        bool avoidStartSafeRadius,
        string label,
        EnemyPoiPreference preference,
        float threatCost,
        float clusteredRatio)
    {
        if (definition == null || definition.EnemyPrefab == null || count <= 0)
        {
            return;
        }

        bool usePoiLayout = config != null && config.EnablePoiClusterLayout;
        if (!usePoiLayout)
        {
            PlaceEnemyBatch(definition, count, avoidStartSafeRadius, label);
            return;
        }

        int clusteredTarget = GetClusteredCount(count, clusteredRatio);
        int placed = 0;
        int attempts = 0;
        int maxTotalAttempts = Mathf.Max(maxPlacementAttempts, count * maxPlacementAttempts);

        while (placed < count && attempts < maxTotalAttempts)
        {
            attempts++;
            bool requestedCluster = placed < clusteredTarget;
            PoiAnchor assignedPoi = null;
            Vector2 position = Vector2.zero;
            bool clustered = requestedCluster && TryFindEnemyPositionNearPoi(
                preference,
                threatCost,
                out position,
                out assignedPoi
            );

            bool insideStartSafeRadius = false;
            if (!clustered && !TryFindPosition(
                    avoidStartSafeRadius,
                    false,
                    enemyMinDistance,
                    out position,
                    out insideStartSafeRadius))
            {
                break;
            }

            if (insideStartSafeRadius)
            {
                if (basicEnemiesInsideStartSafeRadius >= maxBasicEnemiesInsideStartSafeRadius)
                {
                    continue;
                }

                basicEnemiesInsideStartSafeRadius++;
            }

            GameObject spawned = SpawnConfiguredEnemy(
                definition,
                position,
                $"{label}_{placed:00}"
            );

            if (spawned == null)
            {
                continue;
            }

            if (definition.EnemyType == EnemyType.MeleeCharger &&
                spawned.GetComponent<EnemyMeleeChargeController2D>() == null)
            {
                spawned.AddComponent<EnemyMeleeChargeController2D>();
            }

            BeginEnemyArrival(spawned, position);
            occupiedPositions.Add(position);

            if (clustered)
            {
                assignedPoi.AssignedThreat += threatCost;
                clusteredEnemiesPlaced++;
            }
            else
            {
                roamingEnemiesPlaced++;
                if (requestedCluster)
                {
                    poiPlacementFallbackUsed = true;
                }
            }

            placed++;
        }

        if (placed < count)
        {
            Debug.LogWarning($"{label} placement failed. Requested: {count}, placed: {placed}", this);
        }
    }

    private bool TryFindEnemyPositionNearPoi(
        EnemyPoiPreference preference,
        float threatCost,
        out Vector2 position,
        out PoiAnchor assignedPoi)
    {
        position = Vector2.zero;
        assignedPoi = SelectPoiForEnemy(preference, threatCost);

        if (assignedPoi == null)
        {
            return false;
        }

        float innerRadius = Mathf.Min(2.5f, assignedPoi.Radius * 0.35f);

        for (int attempt = 0; attempt < Mathf.Max(clusterCandidateAttempts, 16); attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            float distance = Mathf.Sqrt(UnityEngine.Random.value) *
                Mathf.Max(0.1f, assignedPoi.Radius - innerRadius) + innerRadius;
            Vector2 candidate = assignedPoi.Center + direction * distance;

            if (IsPositionValid(candidate, true, false, enemyMinDistance, out _))
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private PoiAnchor SelectPoiForEnemy(EnemyPoiPreference preference, float threatCost)
    {
        int passCount = preference == EnemyPoiPreference.AnySalvage ? 2 : 1;

        for (int pass = 0; pass < passCount; pass++)
        {
            PoiAnchor selected = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < poiAnchors.Count; i++)
            {
                PoiAnchor poi = poiAnchors[i];
                bool eligible = preference == EnemyPoiPreference.HighValueOnly || pass == 1
                    ? poi.Type == PoiType.HighValueSalvage
                    : poi.Type == PoiType.SalvageField;

                if (!eligible || poi.ThreatCapacity <= 0f)
                {
                    continue;
                }

                float remaining = poi.ThreatCapacity - poi.AssignedThreat;
                if (remaining + 0.01f < threatCost)
                {
                    continue;
                }

                float score = remaining / poi.ThreatCapacity + UnityEngine.Random.value * 0.08f;
                if (score > bestScore)
                {
                    selected = poi;
                    bestScore = score;
                }
            }

            if (selected != null)
            {
                return selected;
            }
        }

        return null;
    }

    private int PlaceDefenderBatch(
        EnemyDefinition definition,
        GameObject prefabOverride,
        int count,
        string label)
    {
        GameObject resolvedPrefab = prefabOverride != null
            ? prefabOverride
            : definition != null ? definition.EnemyPrefab : null;

        if (definition == null || resolvedPrefab == null || count <= 0)
        {
            return 0;
        }

        List<HarvestObjectHealth> targets = spawnedDefenderTargets.Count > 0
            ? spawnedDefenderTargets
            : spawnedHarvestObjects;

        if (targets.Count == 0)
        {
            return 0;
        }

        int placed = 0;
        int targetCursor = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(maxPlacementAttempts, count * maxPlacementAttempts);

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;
            HarvestObjectHealth target = FindNextDefenderTarget(targets, ref targetCursor);

            if (target == null)
            {
                break;
            }

            if (!TryFindRolePositionNear(target.transform.position, out Vector2 position))
            {
                continue;
            }

            GameObject spawned = SpawnConfiguredEnemy(
                definition,
                position,
                $"{label}_{placed:00}",
                resolvedPrefab
            );

            if (spawned == null)
            {
                continue;
            }

            EnemyRoleController role = GetOrAddRoleController(spawned);

            if (role != null)
            {
                role.ConfigureAsDefender(target.transform, MapBounds);
                ConfigureRoleSimulation(role);
                role.SetRoleMarkerSprite(defenderRoleMarkerSprite);
            }

            BeginEnemyArrival(spawned, position);
            occupiedPositions.Add(position);
            placed++;
        }

        return placed;
    }

    private int PlaceRoleEnemyBatch(
        EnemyDefinition definition,
        int count,
        EnemyRoleType roleType,
        string label)
    {
        if (definition == null || definition.EnemyPrefab == null || count <= 0)
        {
            return 0;
        }

        int placed = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(maxPlacementAttempts, count * maxPlacementAttempts);

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;
            float poiPreferenceChance = roleType == EnemyRoleType.RivalHarvester ? 0.7f : 0.55f;
            Vector2 position = Vector2.zero;
            bool placedNearPoi = config != null &&
                config.EnablePoiClusterLayout &&
                UnityEngine.Random.value < poiPreferenceChance &&
                TryFindRolePositionNearSalvagePoi(out position);

            if (!placedNearPoi && !TryFindPosition(
                    true,
                    false,
                    enemyMinDistance,
                    out position,
                    out _))
            {
                break;
            }

            GameObject spawned = SpawnConfiguredEnemy(
                definition,
                position,
                $"{label}_{placed:00}"
            );

            if (spawned == null)
            {
                continue;
            }

            EnemyRoleController role = GetOrAddRoleController(spawned);

            if (role != null)
            {
                switch (roleType)
                {
                    case EnemyRoleType.RivalHarvester:
                        role.ConfigureAsRivalHarvester(MapBounds);
                        ConfigureRoleSimulation(role);
                        role.SetRoleMarkerSprite(rivalHarvesterRoleMarkerSprite);
                        break;

                    case EnemyRoleType.Scavenger:
                        role.ConfigureAsScavenger(MapBounds);
                        ConfigureRoleSimulation(role);
                        role.SetRoleMarkerSprite(scavengerRoleMarkerSprite);
                        break;

                    case EnemyRoleType.Patrol:
                        role.ConfigureAsPatrol(MapBounds);
                        ConfigureRoleSimulation(role);
                        break;
                }

                if (assignRoleEnemiesToNearestGeneratedBase &&
                    (roleType == EnemyRoleType.RivalHarvester || roleType == EnemyRoleType.Scavenger))
                {
                    FieldBaseController closestBase = FindClosestGeneratedFieldBase(position);
                    if (closestBase != null)
                    {
                        role.SetHomeBase(closestBase);
                    }
                }
            }

            BeginEnemyArrival(spawned, position);
            occupiedPositions.Add(position);
            placed++;
        }

        return placed;
    }

    private bool TryFindRolePositionNearSalvagePoi(out Vector2 position)
    {
        position = Vector2.zero;
        PoiAnchor selected = null;
        int eligibleCount = 0;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            if (poi.Type != PoiType.SalvageField && poi.Type != PoiType.HighValueSalvage)
            {
                continue;
            }

            eligibleCount++;
            if (UnityEngine.Random.Range(0, eligibleCount) == 0)
            {
                selected = poi;
            }
        }

        if (selected == null)
        {
            return false;
        }

        return TryFindRolePositionNear(selected.Center, out position);
    }

    private HarvestObjectHealth FindNextDefenderTarget(
        List<HarvestObjectHealth> targets,
        ref int cursor)
    {
        if (targets == null || targets.Count == 0)
        {
            return null;
        }

        float safeRadius = config != null ? config.StartSafeRadius : 10f;

        for (int i = 0; i < targets.Count; i++)
        {
            int index = cursor % targets.Count;
            cursor++;

            HarvestObjectHealth target = targets[index];

            if (target == null || target.IsDead)
            {
                continue;
            }

            if (Vector2.Distance(target.transform.position, startPosition) < safeRadius)
            {
                continue;
            }

            return target;
        }

        return null;
    }

    private bool TryFindRolePositionNear(Vector2 center, out Vector2 position)
    {
        position = Vector2.zero;

        float minRadius = Mathf.Max(0.25f, Mathf.Min(roleSpawnRadiusMin, roleSpawnRadiusMax));
        float maxRadius = Mathf.Max(minRadius, Mathf.Max(roleSpawnRadiusMin, roleSpawnRadiusMax));

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            Vector2 candidate = ClampToMap(center + direction * UnityEngine.Random.Range(minRadius, maxRadius));

            if (IsPositionValid(
                    candidate,
                    true,
                    false,
                    enemyMinDistance,
                    out _))
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private void BeginEnemyArrival(GameObject enemyObject, Vector2 arrivalPosition)
    {
        if (enemyObject == null)
        {
            return;
        }

        Vector2 fallbackCenter = player != null
            ? (Vector2)player.position
            : startPosition;

        EnemyArrivalSpawnUtility.BeginArrival(
            enemyObject,
            arrivalPosition,
            player,
            false,
            fallbackCenter
        );
    }

    private GameObject SpawnConfiguredEnemy(
        EnemyDefinition definition,
        Vector2 position,
        string objectName,
        GameObject prefabOverride = null)
    {
        GameObject resolvedPrefab = prefabOverride != null
            ? prefabOverride
            : definition != null ? definition.EnemyPrefab : null;

        if (definition == null || resolvedPrefab == null)
        {
            return null;
        }

        GameObject spawned = Spawn(resolvedPrefab, position, objectName);

        if (spawned == null)
        {
            return null;
        }

        ConfigureHostileRadarTarget(spawned);

        EnemyBaseAI enemyAI = spawned.GetComponent<EnemyBaseAI>();

        if (enemyAI != null)
        {
            enemyAI.ApplyDefinition(definition);

            if (player != null)
            {
                enemyAI.SetTarget(player);
            }
        }

        ApplyEnemyHpModifiers(spawned);
        return spawned;
    }

    private static void ConfigureHostileRadarTarget(GameObject spawned)
    {
        if (spawned == null)
        {
            return;
        }

        RadarTarget radarTarget = spawned.GetComponentInChildren<RadarTarget>(true);

        if (radarTarget == null)
        {
            radarTarget = spawned.AddComponent<RadarTarget>();
        }

        radarTarget.SetMarkerType(RadarMarkerType.Enemy);
        radarTarget.SetShowOnMap(true);
    }

    private void ConfigureRoleSimulation(EnemyRoleController role)
    {
        if (role == null)
        {
            return;
        }

        role.ConfigureSimulationGate(
            config != null ? config.RoleStartGraceSeconds : fallbackRoleStartGraceSeconds,
            config != null ? config.RoleStartMovementUnlockDistance : fallbackRoleStartMovementUnlockDistance,
            config != null ? config.RolePreviewDistance : fallbackRolePreviewDistance,
            config != null ? config.RoleActiveDistance : fallbackRoleActiveDistance,
            config != null ? config.RoleActiveHoldSeconds : fallbackRoleActiveHoldSeconds,
            config != null ? config.RoleRadarPreviewDuration : fallbackRoleRadarPreviewDuration,
            config != null ? config.MaxConcurrentRivalActions : fallbackMaxConcurrentRivalActions,
            config != null ? config.MaxConcurrentScavengerActions : fallbackMaxConcurrentScavengerActions
        );
    }

    private EnemyRoleController GetOrAddRoleController(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return null;
        }

        EnemyRoleController role = enemyObject.GetComponent<EnemyRoleController>();

        if (role == null)
        {
            role = enemyObject.AddComponent<EnemyRoleController>();
        }

        return role;
    }

    private ExpeditionDepth ResolveCurrentDepth()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.ExpeditionDepth;
        }

        return ExpeditionDepth.Normal;
    }

    private void ApplyCampaignEnemyCountModifiers(
        ExpeditionDepth depth,
        ref int basicCount,
        ref int shotgunCount,
        ref int chargingCount,
        ref int meleeChargerCount,
        ref int eliteMachineGunCount,
        ref int eliteShotgunCount,
        ref int eliteChargingCount)
    {
        switch (depth)
        {
            case ExpeditionDepth.DeepZone1:
                chargingCount += 1;
                meleeChargerCount += 1;
                eliteChargingCount += 1;
                break;

            case ExpeditionDepth.DeepZone2:
                basicCount = Mathf.Max(0, basicCount - 2);
                shotgunCount += 1;
                chargingCount += 2;
                meleeChargerCount += 1;
                eliteMachineGunCount += 1;
                eliteShotgunCount += 1;
                break;

            case ExpeditionDepth.FinalNetwork:
                basicCount = Mathf.Max(0, basicCount - 4);
                shotgunCount += 2;
                chargingCount += 2;
                meleeChargerCount += 2;
                eliteMachineGunCount += 1;
                eliteShotgunCount += 1;
                eliteChargingCount += 1;
                break;
        }
    }

    private static void DistributeEliteBonus(
        int bonus,
        ref int eliteMachineGunCount,
        ref int eliteShotgunCount,
        ref int eliteChargingCount)
    {
        bonus = Mathf.Max(0, bonus);

        for (int i = 0; i < bonus; i++)
        {
            switch (i % 3)
            {
                case 0:
                    eliteMachineGunCount += 1;
                    break;

                case 1:
                    eliteShotgunCount += 1;
                    break;

                default:
                    eliteChargingCount += 1;
                    break;
            }
        }
    }

    private void ApplyEnemyHpModifiers(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            return;
        }

        float hpMultiplier = 1f;

        if (applyDeepZoneEnemyHpMultiplier)
        {
            ExpeditionDepth depth = ResolveCurrentDepth();
            hpMultiplier *= config != null
                ? config.GetEnemyHpMultiplier(depth)
                : CampaignProgressionCatalog.GetEnemyHpMultiplier(depth);
        }

        if (applySeaRegionEnemyHpMultiplier && currentSeaRegion != null)
        {
            hpMultiplier *= Mathf.Max(0.01f, currentSeaRegion.EnemyHpMultiplier);
        }

        if (Mathf.Approximately(hpMultiplier, 1f))
        {
            return;
        }

        enemyHealth.SetMaxHp(enemyHealth.MaxHp * hpMultiplier, true);
    }

    private bool TryFindHarvestPosition(
        float minDistance,
        bool enforceMajorPoiCeiling,
        out Vector2 position,
        out bool insideStartSafeRadius)
    {
        position = Vector2.zero;
        insideStartSafeRadius = false;

        if (useHarvestClusters &&
            harvestClusterAnchors.Count > 0 &&
            UnityEngine.Random.value < harvestClusterChance)
        {
            for (int attempt = 0; attempt < clusterCandidateAttempts; attempt++)
            {
                Vector2 anchor = harvestClusterAnchors[UnityEngine.Random.Range(0, harvestClusterAnchors.Count)];
                Vector2 offset = UnityEngine.Random.insideUnitCircle;

                if (offset.sqrMagnitude <= 0.001f)
                {
                    offset = Vector2.right;
                }

                offset.Normalize();

                float minRadius = Mathf.Min(harvestClusterRadiusRange.x, harvestClusterRadiusRange.y);
                float maxRadius = Mathf.Max(harvestClusterRadiusRange.x, harvestClusterRadiusRange.y);
                float radius = UnityEngine.Random.Range(minRadius, maxRadius);

                Vector2 candidate = ClampToMap(anchor + offset * radius);

                if (IsPositionValid(
                        candidate,
                        false,
                        false,
                        minDistance,
                        out insideStartSafeRadius,
                        enforceMajorPoiCeiling))
                {
                    position = candidate;
                    return true;
                }
            }
        }

        return TryFindPosition(
            false,
            false,
            minDistance,
            out position,
            out insideStartSafeRadius,
            enforceMajorPoiCeiling
        );
    }

    private bool TryFindPosition(
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        out Vector2 position,
        out bool insideStartSafeRadius,
        bool enforceMajorPoiCeiling = false)
    {
        position = Vector2.zero;
        insideStartSafeRadius = false;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = importantPoint
                ? GetRandomPointInBounds(CameraSafeBounds)
                : GetRandomPointInMap();

            if (IsPositionValid(
                    position,
                    avoidStartSafeRadius,
                    importantPoint,
                    minDistance,
                    out insideStartSafeRadius,
                    enforceMajorPoiCeiling))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPositionValid(
        Vector2 position,
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        out bool insideStartSafeRadius,
        bool enforceMajorPoiCeiling = false)
    {
        float startSafeRadius = config != null ? config.StartSafeRadius : 10f;
        float importantMinDistance = config != null ? config.ImportantPointMinDistance : 20f;

        insideStartSafeRadius = Vector2.Distance(position, startPosition) < startSafeRadius;

        if (avoidStartSafeRadius && insideStartSafeRadius)
        {
            return false;
        }

        if (!HasMinimumDistance(position, occupiedPositions, minDistance))
        {
            return false;
        }

        if (importantPoint && !HasMinimumDistance(position, importantPositions, importantMinDistance))
        {
            return false;
        }

        if ((importantPoint || enforceMajorPoiCeiling) &&
            IsAboveRegion2MajorPoiCeiling(position.y + Mathf.Max(0f, minDistance)))
        {
            return false;
        }

        if (IsPointBlockedByReservedPlacement(position, minDistance))
        {
            return false;
        }

        if (useBlockedLayerCheck &&
            Physics2D.OverlapCircle(position, blockedCheckRadius, blockedLayer) != null)
        {
            return false;
        }

        return true;
    }

    private bool HasMinimumDistance(
        Vector2 point,
        List<Vector2> positions,
        float minDistance)
    {
        float sqrMinDistance = minDistance * minDistance;

        for (int i = 0; i < positions.Count; i++)
        {
            if ((point - positions[i]).sqrMagnitude < sqrMinDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 GetRandomPointInMap()
    {
        float halfWidth = mapSize.x * 0.5f - edgePadding;
        float halfHeight = mapSize.y * 0.5f - edgePadding;

        halfWidth = Mathf.Max(0.1f, halfWidth);
        halfHeight = Mathf.Max(0.1f, halfHeight);

        return new Vector2(
            UnityEngine.Random.Range(-halfWidth, halfWidth),
            UnityEngine.Random.Range(-halfHeight, halfHeight)
        );
    }

    private Vector2 ClampToMap(Vector2 point)
    {
        float halfWidth = mapSize.x * 0.5f - edgePadding;
        float halfHeight = mapSize.y * 0.5f - edgePadding;

        halfWidth = Mathf.Max(0.1f, halfWidth);
        halfHeight = Mathf.Max(0.1f, halfHeight);

        return new Vector2(
            Mathf.Clamp(point.x, -halfWidth, halfWidth),
            Mathf.Clamp(point.y, -halfHeight, halfHeight)
        );
    }

    private static Vector2 ClampToBounds(Vector2 point, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new Vector2(
            Mathf.Clamp(point.x, min.x, max.x),
            Mathf.Clamp(point.y, min.y, max.y)
        );
    }

    private static Vector2 ClampToInsetBounds(Vector2 point, Bounds bounds, float inset)
    {
        float safeInset = Mathf.Max(0f, inset);
        float minimumX = bounds.min.x + safeInset;
        float maximumX = bounds.max.x - safeInset;
        float minimumY = bounds.min.y + safeInset;
        float maximumY = bounds.max.y - safeInset;

        if (minimumX > maximumX)
        {
            minimumX = bounds.center.x;
            maximumX = bounds.center.x;
        }

        if (minimumY > maximumY)
        {
            minimumY = bounds.center.y;
            maximumY = bounds.center.y;
        }

        return new Vector2(
            Mathf.Clamp(point.x, minimumX, maximumX),
            Mathf.Clamp(point.y, minimumY, maximumY)
        );
    }

    private bool HasValidPrefab(GameObject[] prefabs, GameObject fallbackPrefab)
    {
        if (fallbackPrefab != null)
        {
            return true;
        }

        if (prefabs == null || prefabs.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject PickPrefab(GameObject[] prefabs, GameObject fallbackPrefab)
    {
        if (prefabs != null && prefabs.Length > 0)
        {
            int validCount = 0;

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                int targetIndex = UnityEngine.Random.Range(0, validCount);
                int currentIndex = 0;

                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (prefabs[i] == null)
                    {
                        continue;
                    }

                    if (currentIndex == targetIndex)
                    {
                        return prefabs[i];
                    }

                    currentIndex++;
                }
            }
        }

        return fallbackPrefab;
    }

    private void ApplySpawnVariation(GameObject spawned, MapSpawnCategory category)
    {
        if (spawned == null || category == MapSpawnCategory.None)
        {
            return;
        }

        if (randomizeHarvestRotation)
        {
            spawned.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                UnityEngine.Random.Range(0f, 360f)
            );
        }

        if (!randomizeHarvestScale)
        {
            return;
        }

        Vector2 scaleRange = GetScaleRange(category);

        float minScale = Mathf.Min(scaleRange.x, scaleRange.y);
        float maxScale = Mathf.Max(scaleRange.x, scaleRange.y);

        minScale = Mathf.Max(0.01f, minScale);
        maxScale = Mathf.Max(0.01f, maxScale);

        float scale = UnityEngine.Random.Range(minScale, maxScale);

        Vector3 baseScale = spawned.transform.localScale;
        spawned.transform.localScale = new Vector3(
            baseScale.x * scale,
            baseScale.y * scale,
            baseScale.z
        );
    }

    private Vector2 GetScaleRange(MapSpawnCategory category)
    {
        return category switch
        {
            MapSpawnCategory.HighValueWreck => highValueWreckScaleRange,
            MapSpawnCategory.SupplyContainer => supplyContainerScaleRange,
            MapSpawnCategory.DestroyedHull => destroyedHullScaleRange,
            MapSpawnCategory.Meteor => meteorScaleRange,
            MapSpawnCategory.LargeMeteor => largeMeteorScaleRange,
            MapSpawnCategory.SpecialActiveContainer => supplyContainerScaleRange,
            MapSpawnCategory.SpecialPassiveContainer => supplyContainerScaleRange,
            _ => Vector2.one
        };
    }

    private void ConfigureSpawnedObject(GameObject spawned, MapSpawnCategory category)
    {
        if (spawned == null)
        {
            return;
        }

        ExpeditionEventObject eventObject = spawned.GetComponent<ExpeditionEventObject>();
        if (eventObject == null)
        {
            eventObject = spawned.GetComponentInChildren<ExpeditionEventObject>(true);
        }

        if (eventObject != null && !spawnedEventObjects.Contains(eventObject))
        {
            spawnedEventObjects.Add(eventObject);
        }

        HarvestObjectHealth harvestObject = spawned.GetComponent<HarvestObjectHealth>();

        if (harvestObject == null)
        {
            harvestObject = spawned.GetComponentInChildren<HarvestObjectHealth>(true);
        }

        if (harvestObject != null)
        {
            if (IsRegion3PhaseGatekeeperMap())
            {
                harvestObject.SetReinforcementSpawnChance(0f);
            }

            bool enemyProjectileDamageEnabled = category switch
            {
                MapSpawnCategory.SupplyContainer => enemyProjectilesDamageSupplyContainers,
                MapSpawnCategory.HighValueWreck => enemyProjectilesDamageHighValueWrecks,
                MapSpawnCategory.DestroyedHull => enemyProjectilesDamageDestroyedHulls,
                MapSpawnCategory.SpecialActiveContainer => false,
                MapSpawnCategory.SpecialPassiveContainer => false,
                _ => harvestObject.ObjectKind switch
                {
                    HarvestObjectKind.SupplyContainer => enemyProjectilesDamageSupplyContainers,
                    HarvestObjectKind.HighValueWreck => enemyProjectilesDamageHighValueWrecks,
                    HarvestObjectKind.DestroyedHull => enemyProjectilesDamageDestroyedHulls,
                    _ => false
                }
            };

            harvestObject.SetEnemyProjectileDamageEnabled(enemyProjectileDamageEnabled, true);
            spawnedHarvestObjects.Add(harvestObject);

            if (harvestObject.ObjectKind == HarvestObjectKind.HighValueWreck ||
                category == MapSpawnCategory.SpecialActiveContainer ||
                category == MapSpawnCategory.SpecialPassiveContainer)
            {
                spawnedDefenderTargets.Add(harvestObject);
            }
        }

        if (category == MapSpawnCategory.SpecialActiveContainer ||
            category == MapSpawnCategory.SpecialPassiveContainer)
        {
            EnsureProgressionRuntimeSystems();

            SpecialEquipmentContainer specialContainer = spawned.GetComponent<SpecialEquipmentContainer>();

            if (specialContainer == null)
            {
                specialContainer = spawned.AddComponent<SpecialEquipmentContainer>();
            }

            SpecialRewardMode mode = category == MapSpawnCategory.SpecialActiveContainer
                ? SpecialRewardMode.ReinforcementOnly
                : SpecialRewardMode.TraitOnly;

            specialContainer.Configure(
                mode,
                rewardChoiceUI,
                $"{category}_{spawned.GetInstanceID()}",
                3,
                1
            );
        }

        if (category == MapSpawnCategory.Meteor || category == MapSpawnCategory.LargeMeteor)
        {
            RadarTarget radarTarget = spawned.GetComponentInChildren<RadarTarget>(true);
            if (radarTarget == null)
            {
                radarTarget = spawned.AddComponent<RadarTarget>();
            }

            radarTarget.SetMarkerType(RadarMarkerType.Meteor);
            radarTarget.SetShowOnMap(true);

            MeteorObstacle meteor = spawned.GetComponent<MeteorObstacle>();

            if (meteor == null)
            {
                meteor = spawned.GetComponentInChildren<MeteorObstacle>(true);
            }

            if (meteor != null)
            {
                meteor.SetEnemyProjectileDamageEnabled(
                    category == MapSpawnCategory.Meteor && enemyProjectilesDamageSmallMeteors,
                    true
                );
                meteor.SetRoamingBounds(MapBounds);

                if (category == MapSpawnCategory.Meteor)
                {
                    meteor.SetRuntimeDriftEnabled(
                        ShouldEnableSmallMeteorDrift(spawned.transform.position)
                    );
                }
                else
                {
                    meteor.SetRuntimeDriftEnabled(false);
                }
            }
        }
    }

    private bool ShouldEnableSmallMeteorDrift(Vector2 position)
    {
        float movementRatio = config != null ? config.SmallMeteorDriftRatio : 0.28f;
        if (UnityEngine.Random.value >= Mathf.Clamp01(movementRatio))
        {
            return false;
        }

        float startSafeRadius = config != null ? config.StartSafeRadius : 10f;
        float startClearance = startSafeRadius + 2f;
        if ((position - startPosition).sqrMagnitude < startClearance * startClearance)
        {
            return false;
        }

        if (IsPointBlockedByReservedPlacement(position, 2f))
        {
            return false;
        }

        const float importantPointClearance = 4.5f;
        return HasMinimumDistance(position, importantPositions, importantPointClearance);
    }

    private GameObject Spawn(
        GameObject prefab,
        Vector2 position,
        string objectName)
    {
        return Spawn(prefab, position, objectName, Quaternion.identity);
    }

    private GameObject Spawn(
        GameObject prefab,
        Vector2 position,
        string objectName,
        Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject spawned = Instantiate(
            prefab,
            position,
            rotation,
            generatedRoot
        );

        spawned.name = objectName;
        return spawned;
    }

    private void PlaceEnvironmentDressing()
    {
        if (!HasEnvironmentDressingSprites())
        {
            return;
        }

        if (IsRegion3PhaseGatekeeperMap())
        {
            PlaceTransitEnvironmentDressing(Mathf.Max(0, region3EnvironmentDressingCount));
            return;
        }

        if (config == null || !config.EnableEnvironmentDressing)
        {
            return;
        }

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            int count = GetPoiDressingCount(poi.Type);

            for (int dressingIndex = 0; dressingIndex < count; dressingIndex++)
            {
                if (!TryFindDressingPositionNearPoi(poi, out Vector2 position))
                {
                    failedDressingPlacements++;
                    continue;
                }

                if (CreateEnvironmentDressing(position, poi.Type, false, dressingIndex) == null)
                {
                    failedDressingPlacements++;
                    continue;
                }

                environmentDressingPositions.Add(position);

                switch (poi.Type)
                {
                    case PoiType.SalvageField:
                        salvageDressingPlaced++;
                        break;

                    case PoiType.HighValueSalvage:
                        highValueDressingPlaced++;
                        break;

                    default:
                        otherPoiDressingPlaced++;
                        break;
                }
            }
        }

        PlaceTransitEnvironmentDressing(config.TransitDressingCount);
    }

    private int GetPoiDressingCount(PoiType type)
    {
        int salvageCount = config != null ? config.SalvageDressingCount : 16;

        return type switch
        {
            PoiType.SalvageField => salvageCount,
            PoiType.HighValueSalvage => config != null ? config.HighValueDressingCount : 24,
            PoiType.FieldBase => Mathf.Max(3, salvageCount / 2),
            PoiType.Shop => Mathf.Max(1, salvageCount / 6),
            PoiType.Event => Mathf.Max(2, salvageCount / 3),
            PoiType.Core => Mathf.Max(1, salvageCount / 6),
            _ => 0
        };
    }

    private bool TryFindDressingPositionNearPoi(PoiAnchor poi, out Vector2 position)
    {
        position = Vector2.zero;
        float radiusMultiplier = config != null ? config.PoiDressingRadiusMultiplier : 1.2f;
        float innerRadius;
        float outerRadius;

        switch (poi.Type)
        {
            case PoiType.FieldBase:
                innerRadius = poi.Radius + 1.5f;
                outerRadius = poi.Radius + 5f;
                break;

            case PoiType.Shop:
                innerRadius = poi.Radius + 2f;
                outerRadius = poi.Radius + 4f;
                break;

            case PoiType.Core:
                innerRadius = poi.Radius + 2f;
                outerRadius = poi.Radius + 4f;
                break;

            case PoiType.Event:
                innerRadius = 2.75f;
                outerRadius = Mathf.Max(5f, poi.Radius * radiusMultiplier);
                break;

            default:
                innerRadius = Mathf.Min(2.5f, poi.Radius * 0.35f);
                outerRadius = poi.Radius * radiusMultiplier;
                break;
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            float distance = Mathf.Lerp(innerRadius, outerRadius, Mathf.Sqrt(UnityEngine.Random.value));
            Vector2 candidate = poi.Center + direction * distance;

            if (IsDressingPositionValid(candidate, 1.15f, 0.7f))
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private void PlaceTransitEnvironmentDressing(int count)
    {
        count = Mathf.Max(0, count);

        for (int i = 0; i < count; i++)
        {
            bool found = false;
            Vector2 position = Vector2.zero;

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                Vector2 candidate = GetRandomPointInMap();

                if (!IsOutsidePoiDressingRegions(candidate) ||
                    !IsDressingPositionValid(candidate, 1.4f, 0.9f))
                {
                    continue;
                }

                position = candidate;
                found = true;
                break;
            }

            if (!found || CreateEnvironmentDressing(position, PoiType.SalvageField, true, i) == null)
            {
                failedDressingPlacements++;
                continue;
            }

            environmentDressingPositions.Add(position);
            transitDressingPlaced++;
        }
    }

    private bool IsOutsidePoiDressingRegions(Vector2 position)
    {
        float radiusMultiplier = config != null ? config.PoiDressingRadiusMultiplier : 1.2f;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            PoiAnchor poi = poiAnchors[i];
            float clearance = poi.Radius * radiusMultiplier + 3f;

            if ((position - poi.Center).sqrMagnitude < clearance * clearance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsDressingPositionValid(
        Vector2 position,
        float gameplayClearance,
        float dressingClearance)
    {
        float halfWidth = mapSize.x * 0.5f - edgePadding;
        float halfHeight = mapSize.y * 0.5f - edgePadding;

        if (Mathf.Abs(position.x) > halfWidth || Mathf.Abs(position.y) > halfHeight)
        {
            return false;
        }

        float safeRadius = config != null ? config.StartSafeRadius : 10f;
        if ((position - startPosition).sqrMagnitude < safeRadius * safeRadius)
        {
            return false;
        }

        if (!HasMinimumDistance(position, occupiedPositions, gameplayClearance) ||
            !HasMinimumDistance(position, environmentDressingPositions, dressingClearance) ||
            IsPointBlockedByReservedPlacement(position, 0.35f))
        {
            return false;
        }

        if (useBlockedLayerCheck &&
            Physics2D.OverlapCircle(position, blockedCheckRadius, blockedLayer) != null)
        {
            return false;
        }

        return true;
    }

    private GameObject CreateEnvironmentDressing(
        Vector2 position,
        PoiType poiType,
        bool transit,
        int index)
    {
        bool useWreck = !transit && ShouldUseWreckDressing(poiType);
        Sprite sprite = PickEnvironmentDressingSprite(useWreck);

        if (sprite == null)
        {
            return null;
        }

        GameObject dressing = new GameObject(
            transit ? $"TransitDressing_{index:000}" : $"{poiType}Dressing_{index:000}"
        );
        dressing.transform.SetParent(generatedRoot, false);
        dressing.transform.position = position;
        dressing.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        Vector2 scaleRange = GetDressingScaleRange(poiType, transit, useWreck);
        float targetWorldSize = UnityEngine.Random.Range(
            Mathf.Min(scaleRange.x, scaleRange.y),
            Mathf.Max(scaleRange.x, scaleRange.y)
        );
        float spriteWorldSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float scale = targetWorldSize / Mathf.Max(0.01f, spriteWorldSize);
        dressing.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer renderer = dressing.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = GetDressingColor(poiType, transit);
        renderer.sortingLayerName = environmentDressingSortingLayerName;
        renderer.sortingOrder = environmentDressingSortingOrder + UnityEngine.Random.Range(0, 3);
        renderer.flipX = UnityEngine.Random.value < 0.5f;
        renderer.flipY = UnityEngine.Random.value < 0.2f;
        return dressing;
    }

    private bool ShouldUseWreckDressing(PoiType poiType)
    {
        float chance = poiType switch
        {
            PoiType.HighValueSalvage => 0.38f,
            PoiType.FieldBase => 0.18f,
            PoiType.Event => 0.1f,
            _ => 0f
        };

        return UnityEngine.Random.value < chance;
    }

    private Vector2 GetDressingScaleRange(PoiType poiType, bool transit, bool wreck)
    {
        if (transit)
        {
            return new Vector2(0.18f, 0.5f);
        }

        if (wreck)
        {
            return poiType == PoiType.HighValueSalvage
                ? new Vector2(3.2f, 6f)
                : new Vector2(2.4f, 4.2f);
        }

        return poiType switch
        {
            PoiType.HighValueSalvage => new Vector2(0.35f, 0.8f),
            PoiType.SalvageField => new Vector2(0.25f, 0.65f),
            PoiType.Shop => new Vector2(0.18f, 0.38f),
            PoiType.Core => new Vector2(0.18f, 0.42f),
            _ => new Vector2(0.22f, 0.52f)
        };
    }

    private Color GetDressingColor(PoiType poiType, bool transit)
    {
        if (transit)
        {
            return new Color(0.42f, 0.5f, 0.58f, UnityEngine.Random.Range(0.1f, 0.18f));
        }

        return poiType switch
        {
            PoiType.SalvageField => new Color(0.48f, 0.56f, 0.62f, UnityEngine.Random.Range(0.16f, 0.28f)),
            PoiType.HighValueSalvage => new Color(0.58f, 0.6f, 0.66f, UnityEngine.Random.Range(0.22f, 0.36f)),
            PoiType.FieldBase => new Color(0.44f, 0.52f, 0.6f, UnityEngine.Random.Range(0.14f, 0.24f)),
            PoiType.Shop => new Color(0.5f, 0.62f, 0.66f, UnityEngine.Random.Range(0.06f, 0.12f)),
            PoiType.Event => new Color(0.4f, 0.58f, 0.64f, UnityEngine.Random.Range(0.12f, 0.22f)),
            PoiType.Core => new Color(0.48f, 0.32f, 0.58f, UnityEngine.Random.Range(0.08f, 0.16f)),
            _ => new Color(0.46f, 0.54f, 0.6f, 0.18f)
        };
    }

    private bool HasEnvironmentDressingSprites()
    {
        return HasValidSprite(environmentDebrisSprites) ||
            HasValidSprite(environmentWreckSprites);
    }

    private Sprite PickEnvironmentDressingSprite(bool useWreck)
    {
        Sprite sprite = PickEnvironmentSprite(useWreck ? environmentWreckSprites : environmentDebrisSprites);

        if (sprite != null)
        {
            return sprite;
        }

        return PickEnvironmentSprite(useWreck ? environmentDebrisSprites : environmentWreckSprites);
    }

    private static bool HasValidSprite(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return false;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Sprite PickEnvironmentSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        int startIndex = UnityEngine.Random.Range(0, sprites.Length);

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[(startIndex + i) % sprites.Length];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void LogPoiGenerationSummary()
    {
        if (config == null || !config.EnablePoiClusterLayout)
        {
            return;
        }

        Debug.Log(
            "POI layout: " +
            $"Salvage {CountPoiType(PoiType.SalvageField)}, " +
            $"HighValue {CountPoiType(PoiType.HighValueSalvage)}, " +
            $"FieldBase {CountPoiType(PoiType.FieldBase)}, " +
            $"Shop {CountPoiType(PoiType.Shop)}, " +
            $"Event {CountPoiType(PoiType.Event)}, " +
            $"Core {CountPoiType(PoiType.Core)} | " +
            $"Harvest clustered {clusteredHarvestPlaced}, scattered {scatteredHarvestPlaced} | " +
            $"Enemies clustered {clusteredEnemiesPlaced}, roaming {roamingEnemiesPlaced} | " +
            $"Dressing salvage {salvageDressingPlaced}, high-value {highValueDressingPlaced}, " +
            $"other POI {otherPoiDressingPlaced}, transit {transitDressingPlaced}, " +
            $"failed {failedDressingPlacements}" +
            (poiPlacementFallbackUsed ? " | fallback used" : string.Empty),
            this
        );
    }

    private int CountPoiType(PoiType type)
    {
        int count = 0;

        for (int i = 0; i < poiAnchors.Count; i++)
        {
            if (poiAnchors[i].Type == type)
            {
                count++;
            }
        }

        return count;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ValidateRegion3BossFoundationGeneration()
    {
        if (!IsRegion3PhaseGatekeeperMap())
        {
            return;
        }

        if (currentRegion3FoundationFailed)
        {
            return;
        }

        if (spawnedCoreObjects.Count != 0 ||
            spawnedFieldBases.Count != 0 ||
            spawnedShopStructures.Count != 0 ||
            spawnedEventObjects.Count != 0 ||
            currentMiniTrader != null)
        {
            Debug.LogError(
                "Region-3 map validation failed: Core, field-base, shop-base, event, " +
                "or Mini Trader generation remained active.",
                this
            );
        }

        if (currentRegion3BossEncounter == null || !currentRegion3EncounterConfigured)
        {
            Debug.LogError(
                "Region-3 map validation failed: the transactional Phase Gatekeeper " +
                "foundation did not publish a configured encounter.",
                this
            );
            return;
        }

        if (!CameraSafeBounds.Contains(startPosition))
        {
            Debug.LogError(
                $"Region-3 map validation failed: Player start {startPosition} is outside CameraSafeBounds.",
                this
            );
            return;
        }

        float bossDistance = Vector2.Distance(startPosition, currentRegion3BossPosition);
        float requiredBossDistance = ResolveRegion3BossMinimumStartClearance();
        if (bossDistance < requiredBossDistance)
        {
            Debug.LogError(
                $"Region-3 map validation failed: Boss is only {bossDistance:0.##} units from " +
                $"the Player start; required {requiredBossDistance:0.##}.",
                this
            );
            return;
        }

        int expectedCount = Region3ReflectorCount;
        if (currentRegion3ReflectorCandidateCount != expectedCount ||
            currentRegion3ReflectorSpawnedCount != expectedCount ||
            currentRegion3ReflectorCount != expectedCount ||
            currentRegion3ConfiguredReflectorCount != expectedCount)
        {
            Debug.LogError(
                $"Region-3 map validation failed: template {currentRegion3ReflectorLayoutPattern} " +
                $"Candidates={currentRegion3ReflectorCandidateCount}, " +
                $"Spawned={currentRegion3ReflectorSpawnedCount}, " +
                $"Registered={currentRegion3ReflectorCount}, " +
                $"Configured={currentRegion3ConfiguredReflectorCount}, " +
                $"Required={expectedCount}.",
                this
            );
            return;
        }

        for (int i = 0; i < expectedCount; i++)
        {
            PhaseReflectorPlate plate = currentRegion3Reflectors[i];
            if (plate == null)
            {
                Debug.LogError(
                    $"Region-3 map validation failed: the published 12-plate foundation " +
                    $"contains a null entry at index {i}.",
                    this
                );
                return;
            }

            currentRegion3ReflectorPositions[i] = plate.transform.position;
        }

        if (TryValidateRegion3ReflectorFoundationGeometry(
                currentRegion3ReflectorPositions,
                expectedCount,
                currentRegion3BossPosition,
                out string geometryFailureReason))
        {
            return;
        }

        StringBuilder details = new StringBuilder(768);
        details.Append("[Region3 Validation] ");
        details.Append("Template=").Append(currentRegion3ReflectorLayoutPattern);
        details.Append(" StartSector=").Append(currentRegion3StartSector);
        details.Append(" Player=").Append(startPosition);
        details.Append(" Boss=").Append(currentRegion3BossPosition);
        details.Append(" BoundsCenter=").Append((Vector2)CameraSafeBounds.center);
        details.Append(" BoundsExtents=").Append((Vector2)CameraSafeBounds.extents);
        details.Append(" Candidates=").Append(currentRegion3ReflectorCandidateCount);
        details.Append(" Spawned=").Append(currentRegion3ReflectorSpawnedCount);
        details.Append(" Registered=").Append(currentRegion3ReflectorCount);
        details.Append(" Configured=").Append(currentRegion3ConfiguredReflectorCount);
        details.Append(" Failure=").Append(geometryFailureReason);
        details.Append(" Attempt=").Append(currentRegion3LayoutAttemptDescription);

        for (int i = 0; i < expectedCount; i++)
        {
            details.Append("\n  Reflector[").Append(i).Append("]=");
            details.Append(currentRegion3ReflectorPositions[i]);
        }

        Debug.LogError(details.ToString(), this);
    }

    private void ValidateMiniTraderGeneration()
    {
        ExpeditionDepth depth = ResolveCurrentDepth();
        bool expectsMiniTrader = depth == ExpeditionDepth.DeepZone1;

        if (!expectsMiniTrader)
        {
            if (currentMiniTrader != null)
            {
                Debug.LogError(
                    $"Mini Trader validation failed: Region {depth} must not generate a Mini Trader.",
                    currentMiniTrader
                );
            }

            return;
        }

        if (spawnedShopStructures.Count != 0)
        {
            Debug.LogError(
                $"Mini Trader validation failed: Region {depth} generated " +
                $"{spawnedShopStructures.Count} fortified Shop structures.",
                this
            );
        }

        if (currentMiniTrader == null)
        {
            Debug.LogError(
                $"Mini Trader validation failed: Region {depth} did not generate its trader.",
                this
            );
            return;
        }

        Vector3 minimum = currentMiniTraderReservationBounds.min;
        Vector3 maximum = currentMiniTraderReservationBounds.max;
        if (!CameraSafeBounds.Contains(minimum) || !CameraSafeBounds.Contains(maximum))
        {
            Debug.LogError(
                $"Mini Trader validation failed: reservation " +
                $"{currentMiniTraderReservationBounds} is outside CameraSafeBounds.",
                currentMiniTrader
            );
        }

        if (depth == ExpeditionDepth.DeepZone1 &&
            IntersectsRegion2BossCorridor(currentMiniTraderReservationBounds, 0f))
        {
            Debug.LogError(
                "Mini Trader validation failed: Region-2 trader intersects the Boss corridor.",
                currentMiniTrader
            );
        }
    }

    private void ValidateRegion2BossCorridorGeneration()
    {
        if (!IsRegion2SalvageDevourerMap())
        {
            return;
        }

        if (!hasRegion2CoreFinalPosition || !hasRegion2BossCorridor)
        {
            Debug.LogError("Region-2 map validation failed: the top Core or Boss corridor was not generated.", this);
            return;
        }

        float majorPoiCeiling = region2CoreFinalPosition.y -
            (config != null ? config.Region2MajorPoiVerticalClearance : 3f);

        for (int i = 0; i < importantPositions.Count; i++)
        {
            Vector2 position = importantPositions[i];
            if ((position - region2CoreFinalPosition).sqrMagnitude <= 0.001f)
            {
                continue;
            }

            if (position.y > majorPoiCeiling)
            {
                Debug.LogError(
                    $"Region-2 map validation failed: major POI at {position} is above the Core ceiling {majorPoiCeiling:0.##}.",
                    this
                );
                break;
            }
        }

        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            Vector2 position = occupiedPositions[i];
            if ((position - startPosition).sqrMagnitude <= 0.001f ||
                (position - region2CoreFinalPosition).sqrMagnitude <= 0.001f)
            {
                continue;
            }

            if (region2BossCorridor.ReservedBounds.Contains(position))
            {
                Debug.LogError(
                    $"Region-2 map validation failed: generated point content at {position} is inside the Boss corridor.",
                    this
                );
                break;
            }
        }

        for (int i = 0; i < environmentDressingPositions.Count; i++)
        {
            Vector2 position = environmentDressingPositions[i];
            if (region2BossCorridor.ReservedBounds.Contains(position))
            {
                Debug.LogError(
                    $"Region-2 map validation failed: dressing at {position} is inside the Boss corridor.",
                    this
                );
                break;
            }
        }

        for (int i = 0; i < spawnedShopStructures.Count; i++)
        {
            ShopStructure shop = spawnedShopStructures[i];
            if (shop == null)
            {
                continue;
            }

            bool compositeOverlap = TryFindRegion2CorridorIntersection(
                shop.gameObject,
                out Component overlappingComponent
            );
            bool portalArrivalOverlap = false;
            if (shop.PortalArrivalPoint != null)
            {
                float arrivalDiameter = Mathf.Max(0.1f, shopPortalArrivalClearanceRadius * 2f);
                Bounds arrivalBounds = new Bounds(
                    shop.PortalArrivalPoint.position,
                    new Vector3(arrivalDiameter, arrivalDiameter, 0f)
                );
                portalArrivalOverlap = arrivalBounds.Intersects(region2BossCorridor.ReservedBounds);
            }

            if (compositeOverlap || portalArrivalOverlap)
            {
                string reason = portalArrivalOverlap
                    ? "portal arrival clearance"
                    : $"component '{overlappingComponent?.name ?? "unknown"}'";
                Debug.LogError(
                    $"Region-2 map validation failed: Shop '{shop.name}' {reason} intersects the Boss corridor.",
                    shop
                );
            }
        }

        for (int i = 0; i < spawnedFieldBases.Count; i++)
        {
            FieldBaseController fieldBase = spawnedFieldBases[i];
            if (fieldBase != null &&
                TryFindRegion2CorridorIntersection(fieldBase.gameObject, out Component overlappingComponent))
            {
                Debug.LogError(
                    $"Region-2 map validation failed: Field Base '{fieldBase.name}' component " +
                    $"'{overlappingComponent?.name ?? "unknown"}' intersects the Boss corridor.",
                    fieldBase
                );
            }
        }
    }

    private bool TryFindRegion2CorridorIntersection(GameObject root, out Component overlappingComponent)
    {
        overlappingComponent = null;
        if (root == null || !hasRegion2BossCorridor)
        {
            return false;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null &&
                collider.enabled &&
                collider.gameObject.activeInHierarchy &&
                collider.bounds.size.x > 0.01f &&
                collider.bounds.size.y > 0.01f &&
                collider.bounds.Intersects(region2BossCorridor.ReservedBounds))
            {
                overlappingComponent = collider;
                return true;
            }
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer != null &&
                targetRenderer.enabled &&
                targetRenderer.gameObject.activeInHierarchy &&
                targetRenderer.bounds.size.x > 0.01f &&
                targetRenderer.bounds.size.y > 0.01f &&
                targetRenderer.bounds.Intersects(region2BossCorridor.ReservedBounds))
            {
                overlappingComponent = targetRenderer;
                return true;
            }
        }

        return false;
    }
#endif

    private void OnDrawGizmosSelected()
    {
        if (!drawMapBounds)
        {
            return;
        }

        ExpeditionDepth depth = ResolveCurrentDepth();
        Vector2 size = config != null
            ? config.GetMapSize(depth)
            : CampaignProgressionCatalog.GetDefaultMapSize(depth);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(size.x, size.y, 0f)
        );

        Vector2 start = Application.isPlaying && depth == ExpeditionDepth.DeepZone2
            ? startPosition
            : startPoint != null
                ? (Vector2)startPoint.position
                : fallbackStartPosition;

        float safeRadius = config != null ? config.StartSafeRadius : 10f;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, safeRadius);

        if (config != null && config.KeepCoreInsideBossCameraSafeArea)
        {
            Bounds safeBounds = ResolveCorePlacementSafeBounds(size);
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 1f);
            Gizmos.DrawWireCube(safeBounds.center, safeBounds.size);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (depth == ExpeditionDepth.DeepZone1)
        {
            Bounds cameraSafeBounds = ResolveCameraSafeBounds(size);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.55f);
            Gizmos.DrawWireCube(cameraSafeBounds.center, cameraSafeBounds.size);

            if (hasRegion2CoreCandidate)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.95f);
                Gizmos.DrawWireSphere(region2CoreCandidatePosition, 0.65f);
            }

            if (hasRegion2CoreFinalPosition)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.15f, 1f);
                Gizmos.DrawWireSphere(region2CoreFinalPosition, 1f);
            }

            if (hasRegion2BossCorridor)
            {
                Gizmos.color = new Color(0.75f, 0.1f, 1f, 0.95f);
                Gizmos.DrawWireCube(
                    region2BossCorridor.CorridorBounds.center,
                    region2BossCorridor.CorridorBounds.size
                );

                Gizmos.color = new Color(1f, 0.2f, 0.75f, 0.55f);
                Gizmos.DrawWireCube(
                    region2BossCorridor.ReservedBounds.center,
                    region2BossCorridor.ReservedBounds.size
                );

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(region2BossCorridor.TerminalPoint, 0.8f);
            }

            if (hasLargeZoneDebugCandidateBounds)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.7f);
                Gizmos.DrawWireCube(
                    largeZoneDebugCandidateBounds.center,
                    largeZoneDebugCandidateBounds.size
                );
            }
        }

        if (depth == ExpeditionDepth.DeepZone2 && Application.isPlaying)
        {
            Bounds cameraSafeBounds = ResolveCameraSafeBounds(size);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.55f);
            Gizmos.DrawWireCube(cameraSafeBounds.center, cameraSafeBounds.size);

            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.85f);
            Gizmos.DrawWireSphere(currentRegion3BossPosition, 1f);

            float reservationSize = Mathf.Max(1f, region3ReflectorPlateLength + 1.5f);
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.65f);
            for (int i = 0; i < currentRegion3ReflectorCount; i++)
            {
                PhaseReflectorPlate plate = currentRegion3Reflectors[i];
                Vector2 position = plate != null
                    ? (Vector2)plate.transform.position
                    : currentRegion3ReflectorPositions[i];
                Gizmos.DrawWireCube(
                    position,
                    new Vector3(reservationSize, reservationSize, 0f)
                );
            }
        }
#endif

        if (config != null && config.EnablePoiClusterLayout)
        {
            for (int i = 0; i < poiAnchors.Count; i++)
            {
                PoiAnchor poi = poiAnchors[i];
                if (poi.Type == PoiType.SalvageField)
                {
                    Gizmos.color = poi.IsLowRisk
                        ? new Color(0.3f, 1f, 0.65f, 0.8f)
                        : new Color(0.25f, 0.8f, 1f, 0.8f);
                    Gizmos.DrawWireSphere(poi.Center, poi.Radius);
                }
                else if (poi.Type == PoiType.HighValueSalvage)
                {
                    Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.85f);
                    Gizmos.DrawWireSphere(poi.Center, poi.Radius);
                }
            }
        }
    }
}
