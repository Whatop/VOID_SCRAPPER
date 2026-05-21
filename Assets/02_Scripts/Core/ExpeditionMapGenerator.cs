using System.Collections.Generic;
using UnityEngine;

public class ExpeditionMapGenerator : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearPreviousGeneratedObjects = true;

    [Header("Root")]
    [SerializeField] private Transform generatedRoot;

    [Header("Player Start")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform startPoint;
    [SerializeField] private Vector2 fallbackStartPosition = new Vector2(-32f, 0f);
    [SerializeField] private bool movePlayerToStart = true;

    [Header("Object Prefabs")]
    [SerializeField] private GameObject shopPrefab;
    [SerializeField] private GameObject[] eventPrefabs;
    [SerializeField] private GameObject corePrefab;
    [SerializeField] private GameObject highValueWreckPrefab;
    [SerializeField] private GameObject supplyContainerPrefab;
    [SerializeField] private GameObject destroyedHullPrefab;
    [SerializeField] private GameObject meteorPrefab;

    [Header("Enemy Definitions")]
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;
    [SerializeField] private EnemyDefinition chargingEnemyDefinition;
    [SerializeField] private EnemyDefinition eliteEnemyDefinition;

    [Header("Placement")]
    [SerializeField] private float edgePadding = 3f;
    [SerializeField] private float generalMinDistance = 1.5f;
    [SerializeField] private float enemyMinDistance = 1.2f;
    [SerializeField] private int maxBasicEnemiesInsideStartSafeRadius = 2;
    [SerializeField] private int maxPlacementAttempts = 500;

    [Header("Physics Block Check Optional")]
    [SerializeField] private bool useBlockedLayerCheck;
    [SerializeField] private LayerMask blockedLayer;
    [SerializeField] private float blockedCheckRadius = 0.5f;

    [Header("Deep Zone")]
    [SerializeField] private bool applyDeepZoneEnemyHpMultiplier = true;

    [Header("Debug")]
    [SerializeField] private bool logGenerationResult = true;
    [SerializeField] private bool drawMapBounds = true;

    private readonly List<Vector2> occupiedPositions = new List<Vector2>(256);
    private readonly List<Vector2> importantPositions = new List<Vector2>(16);

    private Vector2 mapSize = new Vector2(80f, 80f);
    private Vector2 startPosition;
    private int basicEnemiesInsideStartSafeRadius;

    public Bounds MapBounds { get; private set; }
    public Vector2 StartPosition => startPosition;

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    public void Generate()
    {
        ResolveConfig();
        ResolveGeneratedRoot();
        ResolvePlayer();

        if (clearPreviousGeneratedObjects)
        {
            ClearGeneratedObjects();
        }

        occupiedPositions.Clear();
        importantPositions.Clear();
        basicEnemiesInsideStartSafeRadius = 0;

        startPosition = ResolveStartPosition();

        if (movePlayerToStart && player != null)
        {
            player.position = startPosition;
        }

        occupiedPositions.Add(startPosition);

        PlaceImportantObjects();
        PlaceHarvestObjects();
        PlaceEnemies();

        if (logGenerationResult)
        {
            Debug.Log(
                $"Expedition map generated. Size: {mapSize}, Start: {startPosition}, Objects: {occupiedPositions.Count}",
                this
            );
        }
    }

    private void ResolveConfig()
    {
        if (config != null)
        {
            mapSize = config.MapSize;
        }

        if (mapSize.x <= 0f || mapSize.y <= 0f)
        {
            mapSize = new Vector2(80f, 80f);
        }

        MapBounds = new Bounds(Vector3.zero, new Vector3(mapSize.x, mapSize.y, 0f));
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

    private Vector2 ResolveStartPosition()
    {
        if (startPoint != null)
        {
            return ClampToMap(startPoint.position);
        }

        return ClampToMap(fallbackStartPosition);
    }

    private void ClearGeneratedObjects()
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

    private void PlaceImportantObjects()
    {
        int shopCount = config != null ? config.ShopCount : 2;
        int eventCount = config != null ? config.EventCount : 2;
        int coreCount = config != null ? config.CoreCount : 1;

        PlacePrefabBatch(shopPrefab, shopCount, true, true, generalMinDistance, "Shop");
        PlacePrefabBatch(GetRandomEventPrefab(), eventCount, true, true, generalMinDistance, "Event");
        PlacePrefabBatch(corePrefab, coreCount, true, true, generalMinDistance, "Core");
    }

    private void PlaceHarvestObjects()
    {
        int highValueWreckCount = config != null ? config.HighValueWreckCount : 3;
        int supplyContainerCount = config != null ? config.SupplyContainerCount : 16;
        int destroyedHullCount = config != null ? config.DestroyedHullCount : 8;
        int meteorCount = config != null ? config.MeteorCount : 24;

        PlacePrefabBatch(highValueWreckPrefab, highValueWreckCount, false, false, generalMinDistance, "HighValueWreck");
        PlacePrefabBatch(supplyContainerPrefab, supplyContainerCount, false, false, generalMinDistance, "SupplyContainer");
        PlacePrefabBatch(destroyedHullPrefab, destroyedHullCount, false, false, generalMinDistance, "DestroyedHull");
        PlacePrefabBatch(meteorPrefab, meteorCount, false, false, generalMinDistance, "Meteor");
    }

    private void PlaceEnemies()
    {
        int basicCount = config != null ? config.BasicEnemyCount : 20;
        int shotgunCount = config != null ? config.ShotgunEnemyCount : 5;
        int chargingCount = config != null ? config.ChargingEnemyCount : 4;
        int eliteCount = config != null ? config.EliteEnemyCount : 2;

        PlaceEnemyBatch(basicEnemyDefinition, basicCount, false, "BasicEnemy");
        PlaceEnemyBatch(shotgunEnemyDefinition, shotgunCount, true, "ShotgunEnemy");
        PlaceEnemyBatch(chargingEnemyDefinition, chargingCount, true, "ChargingEnemy");
        PlaceEnemyBatch(eliteEnemyDefinition, eliteCount, true, "EliteEnemy");
    }

    private void PlacePrefabBatch(
        GameObject prefab,
        int count,
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        string label)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            bool found = TryFindPosition(
                avoidStartSafeRadius,
                importantPoint,
                minDistance,
                out Vector2 position,
                out bool insideStartSafeRadius
            );

            if (!found)
            {
                Debug.LogWarning($"{label} 배치 실패. 배치 조건이 너무 빡빡할 수 있습니다.", this);
                continue;
            }

            GameObject spawned = Spawn(prefab, position, $"{label}_{i:00}");

            if (spawned != null)
            {
                occupiedPositions.Add(position);

                if (importantPoint)
                {
                    importantPositions.Add(position);
                }
            }
        }
    }

    private void PlaceEnemyBatch(EnemyDefinition definition, int count, bool avoidStartSafeRadius, string label)
    {
        if (definition == null || definition.EnemyPrefab == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            bool found = TryFindPosition(
                avoidStartSafeRadius,
                false,
                enemyMinDistance,
                out Vector2 position,
                out bool insideStartSafeRadius
            );

            if (!found)
            {
                Debug.LogWarning($"{label} 배치 실패. 배치 조건이 너무 빡빡할 수 있습니다.", this);
                continue;
            }

            if (insideStartSafeRadius)
            {
                if (basicEnemiesInsideStartSafeRadius >= maxBasicEnemiesInsideStartSafeRadius)
                {
                    i--;
                    continue;
                }

                basicEnemiesInsideStartSafeRadius++;
            }

            GameObject spawned = Spawn(definition.EnemyPrefab, position, $"{label}_{i:00}");

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

            ApplyDeepZoneEnemyModifier(spawned);
            occupiedPositions.Add(position);
        }
    }

    private void ApplyDeepZoneEnemyModifier(GameObject enemyObject)
    {
        if (!applyDeepZoneEnemyHpMultiplier)
        {
            return;
        }

        if (enemyObject == null || config == null)
        {
            return;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        if (RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1)
        {
            return;
        }

        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            return;
        }

        float hpMultiplier = Mathf.Max(0.01f, config.DeepZoneEnemyHpMultiplier);
        enemyHealth.SetMaxHp(enemyHealth.MaxHp * hpMultiplier, true);
    }

    private bool TryFindPosition(
        bool avoidStartSafeRadius,
        bool importantPoint,
        float minDistance,
        out Vector2 position,
        out bool insideStartSafeRadius)
    {
        float startSafeRadius = config != null ? config.StartSafeRadius : 10f;
        float importantMinDistance = config != null ? config.ImportantPointMinDistance : 20f;

        position = Vector2.zero;
        insideStartSafeRadius = false;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = GetRandomPointInMap();
            insideStartSafeRadius = Vector2.Distance(position, startPosition) < startSafeRadius;

            if (avoidStartSafeRadius && insideStartSafeRadius)
            {
                continue;
            }

            if (!HasMinimumDistance(position, occupiedPositions, minDistance))
            {
                continue;
            }

            if (importantPoint && !HasMinimumDistance(position, importantPositions, importantMinDistance))
            {
                continue;
            }

            if (useBlockedLayerCheck && Physics2D.OverlapCircle(position, blockedCheckRadius, blockedLayer) != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasMinimumDistance(Vector2 point, List<Vector2> positions, float minDistance)
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

        return new Vector2(
            Mathf.Clamp(point.x, -halfWidth, halfWidth),
            Mathf.Clamp(point.y, -halfHeight, halfHeight)
        );
    }

    private GameObject GetRandomEventPrefab()
    {
        if (eventPrefabs == null || eventPrefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> validPrefabs = new List<GameObject>();

        for (int i = 0; i < eventPrefabs.Length; i++)
        {
            if (eventPrefabs[i] != null)
            {
                validPrefabs.Add(eventPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    private GameObject Spawn(GameObject prefab, Vector2 position, string objectName)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity, generatedRoot);
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
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));

        Vector2 start = startPoint != null ? (Vector2)startPoint.position : fallbackStartPosition;
        float safeRadius = config != null ? config.StartSafeRadius : 10f;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, safeRadius);
    }
}