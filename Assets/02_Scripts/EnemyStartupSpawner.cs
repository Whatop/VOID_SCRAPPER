using System.Collections.Generic;
using UnityEngine;

public class EnemyStartupSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Collider2D worldArea;         // 배경 또는 맵 경계를 표시하는 콜라이더

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject meleeEnemyPrefab;
    [SerializeField] private GameObject rangedEnemyPrefab;

    [Header("Meteor Prefabs")]
    [SerializeField] private GameObject smallMeteorPrefab;
    [SerializeField] private GameObject mediumMeteorPrefab;
    [SerializeField] private GameObject largeMeteorPrefab;

    [Header("Target Counts")]
    [SerializeField] private int meleeTargetCount = 6;
    [SerializeField] private int rangedTargetCount = 4;
    [SerializeField] private int smallMeteorTargetCount = 5;
    [SerializeField] private int mediumMeteorTargetCount = 3;
    [SerializeField] private int largeMeteorTargetCount = 2;

    [Header("Spawn Settings")]
    [SerializeField] private float maintainInterval = 1f;  // 개체 수를 다시 확인하는 간격
    [SerializeField] private float minEnemySpawnDistance = 8f;
    [SerializeField] private float maxEnemySpawnDistance = 14f;
    [SerializeField] private float minMeteorSpawnDistanceFromPlayer = 3f;
    [SerializeField] private int maxSpawnAttempts = 20;

    private readonly List<GameObject> meleeRegistry = new List<GameObject>();
    private readonly List<GameObject> rangedRegistry = new List<GameObject>();
    private readonly List<GameObject> smallMeteorRegistry = new List<GameObject>();
    private readonly List<GameObject> mediumMeteorRegistry = new List<GameObject>();
    private readonly List<GameObject> largeMeteorRegistry = new List<GameObject>();

    private Bounds cachedWorldBounds;
    private PlayerHealth playerHealth;

    private void Start()
    {
        FindPlayer();
        CacheWorldBounds();
        MaintainPopulation();
        InvokeRepeating(nameof(MaintainPopulation), maintainInterval, maintainInterval);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(MaintainPopulation));
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }
    }

    private void CacheWorldBounds()
    {
        if (worldArea != null)
        {
            cachedWorldBounds = worldArea.bounds;
        }
        else
        {
            cachedWorldBounds = new Bounds(Vector3.zero, new Vector3(40f, 24f, 0f));
        }
    }

    /// <summary>
    /// 현재 활성화된 적과 운석 수를 확인하여 목표 수보다 부족하면 다시 생성합니다.
    /// 풀을 사용 중이라면 풀에서 꺼내고, 아니면 Instantiate를 사용합니다.
    /// </summary>
    private void MaintainPopulation()
    {
        if (player == null)
        {
            FindPlayer();
        }

        if (player == null)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        EnsureCount(meleeEnemyPrefab, meleeTargetCount, meleeRegistry, spawnAroundPlayer: true);
        EnsureCount(rangedEnemyPrefab, rangedTargetCount, rangedRegistry, spawnAroundPlayer: true);
        EnsureCount(smallMeteorPrefab, smallMeteorTargetCount, smallMeteorRegistry, spawnAroundPlayer: false);
        EnsureCount(mediumMeteorPrefab, mediumMeteorTargetCount, mediumMeteorRegistry, spawnAroundPlayer: false);
        EnsureCount(largeMeteorPrefab, largeMeteorTargetCount, largeMeteorRegistry, spawnAroundPlayer: false);
    }

    private void EnsureCount(GameObject prefab, int targetCount, List<GameObject> registry, bool spawnAroundPlayer)
    {
        if (prefab == null || targetCount <= 0)
        {
            return;
        }

        registry.RemoveAll(instance => instance == null);

        int activeCount = 0;
        for (int i = 0; i < registry.Count; i++)
        {
            if (registry[i] != null && registry[i].activeInHierarchy)
            {
                activeCount++;
            }
        }

        int missingCount = targetCount - activeCount;
        for (int i = 0; i < missingCount; i++)
        {
            SpawnInstance(prefab, registry, spawnAroundPlayer);
        }
    }

    private void SpawnInstance(GameObject prefab, List<GameObject> registry, bool spawnAroundPlayer)
    {
        Vector3 spawnPosition = spawnAroundPlayer ? GetEnemySpawnPosition() : GetMeteorSpawnPosition();

        GameObject spawnedObject;
        if (PoolManager.Instance != null)
        {
            spawnedObject = PoolManager.Instance.Get(prefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            spawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        }

        if (spawnedObject == null)
        {
            return;
        }

        if (!registry.Contains(spawnedObject))
        {
            registry.Add(spawnedObject);
        }

        EnemyBaseAI enemyAI = spawnedObject.GetComponent<EnemyBaseAI>();
        if (enemyAI != null)
        {
            enemyAI.SetTarget(player);
        }

        MeteorObstacle meteorObstacle = spawnedObject.GetComponent<MeteorObstacle>();
        if (meteorObstacle != null)
        {
            meteorObstacle.SetRoamingBounds(cachedWorldBounds);
        }
    }

    private Vector3 GetEnemySpawnPosition()
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            if (randomDirection.sqrMagnitude <= 0.001f)
            {
                randomDirection = Vector2.up;
            }

            float randomDistance = Random.Range(minEnemySpawnDistance, maxEnemySpawnDistance);
            Vector3 candidate = player.position + (Vector3)(randomDirection * randomDistance);
            candidate = ClampToWorldBounds(candidate, 0.5f);

            if (Vector2.Distance(candidate, player.position) >= minEnemySpawnDistance * 0.8f)
            {
                return candidate;
            }
        }

        return ClampToWorldBounds(player.position + Vector3.right * minEnemySpawnDistance, 0.5f);
    }

    private Vector3 GetMeteorSpawnPosition()
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 candidate = GetRandomPositionInsideWorld(0.8f);
            if (Vector2.Distance(candidate, player.position) >= minMeteorSpawnDistanceFromPlayer)
            {
                return candidate;
            }
        }

        return GetRandomPositionInsideWorld(0.8f);
    }

    private Vector3 GetRandomPositionInsideWorld(float padding)
    {
        float minX = cachedWorldBounds.min.x + padding;
        float maxX = cachedWorldBounds.max.x - padding;
        float minY = cachedWorldBounds.min.y + padding;
        float maxY = cachedWorldBounds.max.y - padding;

        return new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            0f
        );
    }

    private Vector3 ClampToWorldBounds(Vector3 position, float padding)
    {
        position.x = Mathf.Clamp(position.x, cachedWorldBounds.min.x + padding, cachedWorldBounds.max.x - padding);
        position.y = Mathf.Clamp(position.y, cachedWorldBounds.min.y + padding, cachedWorldBounds.max.y - padding);
        position.z = 0f;
        return position;
    }
}
