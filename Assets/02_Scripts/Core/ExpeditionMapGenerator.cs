using System.Collections.Generic;
using UnityEngine;

public class ExpeditionMapGenerator : MonoBehaviour
{
    private enum MapSpawnCategory
    {
        None,
        HighValueWreck,
        SupplyContainer,
        DestroyedHull,
        Meteor,
        SpecialActiveContainer,
        SpecialPassiveContainer
    }

    [Header("Config")]
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearPreviousGeneratedObjects = true;

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

    [Header("Important Object Prefabs")]
    [SerializeField] private GameObject shopPrefab;
    [SerializeField] private GameObject[] eventPrefabs;
    [SerializeField] private GameObject corePrefab;

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

    [Tooltip("운석 프리팹 후보. 여러 개를 넣으면 매 스폰마다 랜덤 선택합니다.")]
    [SerializeField] private GameObject[] meteorPrefabs;

    [Header("Legacy Single Prefab Fallback")]
    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject highValueWreckPrefab;

    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject supplyContainerPrefab;

    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject destroyedHullPrefab;

    [Tooltip("위 배열이 비어 있을 때만 사용하는 기존 단일 프리팹 호환용입니다.")]
    [SerializeField] private GameObject meteorPrefab;

    [Header("Enemy Definitions")]
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;
    [SerializeField] private EnemyDefinition chargingEnemyDefinition;
    [SerializeField] private EnemyDefinition eliteEnemyDefinition;

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
    [SerializeField] private Vector2 meteorScaleRange = new Vector2(0.75f, 1.35f);

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
    private readonly List<HarvestObjectHealth> spawnedDefenderTargets = new List<HarvestObjectHealth>(16);

    private Vector2 mapSize = new Vector2(80f, 80f);
    private Vector2 startPosition;
    private int basicEnemiesInsideStartSafeRadius;
    private SeaRegionDefinition currentSeaRegion;
    private TraitCatalog runtimeTraitCatalog;
    private ReinforcementCatalog runtimeReinforcementCatalog;
    private static Sprite runtimeFallbackNpcSprite;

    public Bounds MapBounds { get; private set; }
    public Bounds CorePlacementSafeBounds { get; private set; }
    public Vector2 StartPosition => startPosition;
    public SeaRegionDefinition CurrentSeaRegion => currentSeaRegion;

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

        occupiedPositions.Clear();
        importantPositions.Clear();
        harvestClusterAnchors.Clear();
        spawnedHarvestObjects.Clear();
        spawnedDefenderTargets.Clear();

        basicEnemiesInsideStartSafeRadius = 0;
        startPosition = ResolveStartPosition();

        if (movePlayerToStart && player != null)
        {
            player.position = startPosition;
        }

        occupiedPositions.Add(startPosition);

        SyncBackgroundGenerator();

        PlaceImportantObjects();
        PlaceHarvestObjects();
        PlaceEnemies();

        if (logGenerationResult)
        {
            string seaText = currentSeaRegion != null ? currentSeaRegion.DisplayName : "None";

            Debug.Log(
                $"Expedition map generated. Size: {mapSize}, Start: {startPosition}, " +
                $"SeaRegion: {seaText}, Spawned Positions: {occupiedPositions.Count}",
                this
            );
        }
    }

    [ContextMenu("Clear Generated Objects")]
    public void ClearGeneratedObjects()
    {
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

        if (config != null)
        {
            mapSize = config.MapSize;
        }

        if (mapSize.x <= 0f || mapSize.y <= 0f)
        {
            mapSize = new Vector2(80f, 80f);
        }

        MapBounds = new Bounds(
            Vector3.zero,
            new Vector3(mapSize.x, mapSize.y, 0f)
        );

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
        if (startPoint != null)
        {
            return ClampToMap(startPoint.position);
        }

        return ClampToMap(fallbackStartPosition);
    }

    private void PlaceImportantObjects()
    {
        int shopCount = config != null ? config.ShopCount : 2;
        int eventCount = config != null ? config.EventCount : 2;
        int coreCount = config != null ? config.CoreCount : 1;

        // 코어를 먼저 배치해야 보스 카메라 안전 영역 안에서 자리를 확보할 수 있습니다.
        PlaceCoreBatch(coreCount);

        PlacePrefabBatch(
            shopPrefab,
            shopCount,
            true,
            true,
            generalMinDistance,
            "Shop"
        );

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

        int fieldNpcCount = config != null ? config.FieldNpcCount : 2;
        PlaceFieldNpcBatch(fieldNpcCount);
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

        radar.SetMarkerType(RadarMarkerType.Event);
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
        int meteorCount = config != null ? config.MeteorCount : 24;

        if (applySeaRegionObjectCountModifiers && currentSeaRegion != null)
        {
            highValueWreckCount = ApplyCountModifier(highValueWreckCount, currentSeaRegion.ExtraHighValueWreckCount);
            supplyContainerCount = ApplyCountModifier(supplyContainerCount, currentSeaRegion.ExtraSupplyContainerCount);
            destroyedHullCount = ApplyCountModifier(destroyedHullCount, currentSeaRegion.ExtraDestroyedHullCount);
            meteorCount = ApplyCountModifier(meteorCount, currentSeaRegion.ExtraMeteorCount);
        }

        PlaceHarvestPrefabBatch(
            specialActiveContainerPrefabs,
            specialActiveContainerPrefab,
            Mathf.Max(0, specialActiveContainerCount),
            generalMinDistance,
            "SpecialActiveContainer",
            MapSpawnCategory.SpecialActiveContainer
        );

        PlaceHarvestPrefabBatch(
            specialPassiveContainerPrefabs,
            specialPassiveContainerPrefab,
            Mathf.Max(0, specialPassiveContainerCount),
            generalMinDistance,
            "SpecialPassiveContainer",
            MapSpawnCategory.SpecialPassiveContainer
        );

        PlaceHarvestPrefabBatch(
            highValueWreckPrefabs,
            highValueWreckPrefab,
            highValueWreckCount,
            generalMinDistance,
            "HighValueWreck",
            MapSpawnCategory.HighValueWreck
        );

        PlaceHarvestPrefabBatch(
            supplyContainerPrefabs,
            supplyContainerPrefab,
            supplyContainerCount,
            generalMinDistance,
            "SupplyContainer",
            MapSpawnCategory.SupplyContainer
        );

        PlaceHarvestPrefabBatch(
            destroyedHullPrefabs,
            destroyedHullPrefab,
            destroyedHullCount,
            generalMinDistance,
            "DestroyedHull",
            MapSpawnCategory.DestroyedHull
        );

        PlaceHarvestPrefabBatch(
            meteorPrefabs,
            meteorPrefab,
            meteorCount,
            generalMinDistance,
            "Meteor",
            MapSpawnCategory.Meteor
        );
    }

    private void PlaceEnemies()
    {
        int basicCount = config != null ? config.BasicEnemyCount : 20;
        int shotgunCount = config != null ? config.ShotgunEnemyCount : 5;
        int chargingCount = config != null ? config.ChargingEnemyCount : 4;
        int eliteCount = config != null ? config.EliteEnemyCount : 2;

        if (applySeaRegionEnemyCountModifiers && currentSeaRegion != null)
        {
            basicCount = ApplyCountModifier(basicCount, currentSeaRegion.ExtraBasicEnemyCount);
            shotgunCount = ApplyCountModifier(shotgunCount, currentSeaRegion.ExtraShotgunEnemyCount);
            chargingCount = ApplyCountModifier(chargingCount, currentSeaRegion.ExtraChargingEnemyCount);
            eliteCount = ApplyCountModifier(eliteCount, currentSeaRegion.ExtraEliteEnemyCount);
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
                Mathf.Min(defenderBasicRequest, basicCount),
                "DefenderBasic"
            );
            basicCount -= placedDefenderBasic;

            int placedDefenderShotgun = PlaceDefenderBatch(
                shotgunEnemyDefinition,
                Mathf.Min(defenderShotgunRequest, shotgunCount),
                "DefenderShotgun"
            );
            shotgunCount -= placedDefenderShotgun;

            int placedRivals = PlaceRoleEnemyBatch(
                basicEnemyDefinition,
                Mathf.Min(rivalRequest, basicCount),
                EnemyRoleType.RivalHarvester,
                "RivalHarvester"
            );
            basicCount -= placedRivals;

            int placedScavengers = PlaceRoleEnemyBatch(
                basicEnemyDefinition,
                Mathf.Min(scavengerRequest, basicCount),
                EnemyRoleType.Scavenger,
                "Scavenger"
            );
            basicCount -= placedScavengers;
        }

        PlaceEnemyBatch(
            basicEnemyDefinition,
            basicCount,
            false,
            "BasicEnemy"
        );

        PlaceEnemyBatch(
            shotgunEnemyDefinition,
            shotgunCount,
            true,
            "ShotgunEnemy"
        );

        PlaceEnemyBatch(
            chargingEnemyDefinition,
            chargingCount,
            true,
            "ChargingEnemy"
        );

        PlaceEnemyBatch(
            eliteEnemyDefinition,
            eliteCount,
            true,
            "EliteEnemy"
        );
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
            bool found = TryFindCorePosition(
                generalMinDistance,
                out Vector2 position,
                out _
            );

            if (!found)
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

            occupiedPositions.Add(position);
            importantPositions.Add(position);
        }
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
            Random.Range(min.x, max.x),
            Random.Range(min.y, max.y)
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

            bool found = category == MapSpawnCategory.None
                ? TryFindPosition(
                    avoidStartSafeRadius,
                    importantPoint,
                    minDistance,
                    out position,
                    out insideStartSafeRadius
                )
                : TryFindHarvestPosition(
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

            if (category != MapSpawnCategory.None)
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
            occupiedPositions.Add(position);
            placed++;
        }

        if (placed < count)
        {
            Debug.LogWarning($"{label} 일부 배치 실패. 요청: {count}, 배치: {placed}", this);
        }
    }

    private int PlaceDefenderBatch(
        EnemyDefinition definition,
        int count,
        string label)
    {
        if (definition == null || definition.EnemyPrefab == null || count <= 0)
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
                $"{label}_{placed:00}"
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

            if (!TryFindPosition(
                    true,
                    false,
                    enemyMinDistance,
                    out Vector2 position,
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
            }

            occupiedPositions.Add(position);
            placed++;
        }

        return placed;
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
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            Vector2 candidate = ClampToMap(center + direction * Random.Range(minRadius, maxRadius));

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

    private GameObject SpawnConfiguredEnemy(
        EnemyDefinition definition,
        Vector2 position,
        string objectName)
    {
        if (definition == null || definition.EnemyPrefab == null)
        {
            return null;
        }

        GameObject spawned = Spawn(definition.EnemyPrefab, position, objectName);

        if (spawned == null)
        {
            return null;
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
        return spawned;
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

        if (applyDeepZoneEnemyHpMultiplier &&
            config != null &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            if (RunManager.Instance.CurrentRun.ExpeditionDepth == ExpeditionDepth.DeepZone1)
            {
                hpMultiplier *= Mathf.Max(0.01f, config.DeepZoneEnemyHpMultiplier);
            }
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
        out Vector2 position,
        out bool insideStartSafeRadius)
    {
        position = Vector2.zero;
        insideStartSafeRadius = false;

        if (useHarvestClusters &&
            harvestClusterAnchors.Count > 0 &&
            Random.value < harvestClusterChance)
        {
            for (int attempt = 0; attempt < clusterCandidateAttempts; attempt++)
            {
                Vector2 anchor = harvestClusterAnchors[Random.Range(0, harvestClusterAnchors.Count)];
                Vector2 offset = Random.insideUnitCircle;

                if (offset.sqrMagnitude <= 0.001f)
                {
                    offset = Vector2.right;
                }

                offset.Normalize();

                float minRadius = Mathf.Min(harvestClusterRadiusRange.x, harvestClusterRadiusRange.y);
                float maxRadius = Mathf.Max(harvestClusterRadiusRange.x, harvestClusterRadiusRange.y);
                float radius = Random.Range(minRadius, maxRadius);

                Vector2 candidate = ClampToMap(anchor + offset * radius);

                if (IsPositionValid(
                        candidate,
                        false,
                        false,
                        minDistance,
                        out insideStartSafeRadius))
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
            out insideStartSafeRadius
        );
    }

    private bool TryFindPosition(
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        out Vector2 position,
        out bool insideStartSafeRadius)
    {
        position = Vector2.zero;
        insideStartSafeRadius = false;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = GetRandomPointInMap();

            if (IsPositionValid(
                    position,
                    avoidStartSafeRadius,
                    importantPoint,
                    minDistance,
                    out insideStartSafeRadius))
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
        out bool insideStartSafeRadius)
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
            Random.Range(-halfWidth, halfWidth),
            Random.Range(-halfHeight, halfHeight)
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
                int targetIndex = Random.Range(0, validCount);
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
                Random.Range(0f, 360f)
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

        float scale = Random.Range(minScale, maxScale);

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

        HarvestObjectHealth harvestObject = spawned.GetComponent<HarvestObjectHealth>();

        if (harvestObject != null)
        {
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

        if (category == MapSpawnCategory.Meteor)
        {
            MeteorObstacle meteor = spawned.GetComponent<MeteorObstacle>();

            if (meteor != null)
            {
                meteor.SetRoamingBounds(MapBounds);
            }
        }
    }

    private GameObject Spawn(
        GameObject prefab,
        Vector2 position,
        string objectName)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject spawned = Instantiate(
            prefab,
            position,
            Quaternion.identity,
            generatedRoot
        );

        spawned.name = objectName;
        return spawned;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawMapBounds)
        {
            return;
        }

        Vector2 size = config != null ? config.MapSize : mapSize;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(size.x, size.y, 0f)
        );

        Vector2 start = startPoint != null
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
    }
}