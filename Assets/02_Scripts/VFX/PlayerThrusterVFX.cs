using UnityEngine;

[DisallowMultipleComponent]
public class PlayerThrusterVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Exhaust Points")]
    [Tooltip("플레이어 뒤쪽 배기구 위치 Transform들. 보통 2개 연결.")]
    [SerializeField] private Transform[] exhaustPoints;

    [Header("VFX Prefabs")]
    [SerializeField] private GameObject moveExhaustPrefab;
    [SerializeField] private GameObject dashExhaustPrefab;

    [Header("Spawn Timing")]
    [SerializeField] private float moveSpawnInterval = 0.08f;
    [SerializeField] private float dashSpawnInterval = 0.035f;
    [SerializeField] private float effectLifeTime = 0.25f;

    [Header("Options")]
    [SerializeField] private bool spawnWhileMoving = true;
    [SerializeField] private bool spawnWhileDashing = true;
    [SerializeField] private bool spawnAllExhaustPoints = true;

    [Header("Rotation")]
    [Tooltip("켜면 ExhaustPoint의 회전을 그대로 사용합니다. 보통 이게 제일 편합니다.")]
    [SerializeField] private bool useExhaustPointRotation = true;

    [Tooltip("켜면 이동 방향의 반대 방향으로 VFX를 회전시킵니다.")]
    [SerializeField] private bool rotateAgainstMoveDirection;

    [SerializeField] private float rotationOffset = -90f;

    [Header("Position Jitter")]
    [SerializeField] private float positionJitter = 0.02f;

    private float spawnTimer;
    private int nextExhaustIndex;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        spawnTimer = 0f;
        nextExhaustIndex = 0;
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        bool isMoving = playerController != null && playerController.IsMoving;
        bool isDashing = playerDash != null && playerDash.IsDashing;

        bool shouldSpawnMove = spawnWhileMoving && isMoving;
        bool shouldSpawnDash = spawnWhileDashing && isDashing;

        if (!shouldSpawnMove && !shouldSpawnDash)
        {
            spawnTimer = 0f;
            return;
        }

        GameObject prefab = shouldSpawnDash && dashExhaustPrefab != null
            ? dashExhaustPrefab
            : moveExhaustPrefab;

        if (prefab == null)
        {
            return;
        }

        float interval = shouldSpawnDash
            ? dashSpawnInterval
            : moveSpawnInterval;

        interval = Mathf.Max(0.01f, interval);

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = interval;
        SpawnExhaust(prefab);
    }

    private void CacheReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }
    }

    private void SpawnExhaust(GameObject prefab)
    {
        if (exhaustPoints == null || exhaustPoints.Length == 0)
        {
            SpawnAtPoint(transform, prefab);
            return;
        }

        if (spawnAllExhaustPoints)
        {
            for (int i = 0; i < exhaustPoints.Length; i++)
            {
                Transform point = exhaustPoints[i];

                if (point != null)
                {
                    SpawnAtPoint(point, prefab);
                }
            }

            return;
        }

        Transform selectedPoint = GetNextExhaustPoint();

        if (selectedPoint != null)
        {
            SpawnAtPoint(selectedPoint, prefab);
        }
    }

    private Transform GetNextExhaustPoint()
    {
        if (exhaustPoints == null || exhaustPoints.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < exhaustPoints.Length; i++)
        {
            int index = nextExhaustIndex % exhaustPoints.Length;
            nextExhaustIndex++;

            if (exhaustPoints[index] != null)
            {
                return exhaustPoints[index];
            }
        }

        return null;
    }

    private void SpawnAtPoint(Transform point, GameObject prefab)
    {
        if (point == null || prefab == null)
        {
            return;
        }

        Vector3 position = point.position;

        if (positionJitter > 0f)
        {
            Vector2 jitter = Random.insideUnitCircle * positionJitter;
            position += new Vector3(jitter.x, jitter.y, 0f);
        }

        Quaternion rotation = GetEffectRotation(point);

        GameObject instance;

        if (PoolManager.Instance != null)
        {
            instance = PoolManager.Instance.Get(prefab, position, rotation);
        }
        else
        {
            instance = Instantiate(prefab, position, rotation);
        }

        if (instance == null)
        {
            return;
        }

        float releaseDelay = Mathf.Max(0.01f, effectLifeTime);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(instance, releaseDelay);
        }
        else
        {
            Destroy(instance, releaseDelay);
        }
    }

    private Quaternion GetEffectRotation(Transform point)
    {
        if (useExhaustPointRotation && point != null)
        {
            return point.rotation;
        }

        if (rotateAgainstMoveDirection && playerController != null)
        {
            Vector2 moveInput = playerController.MoveInput;

            if (moveInput.sqrMagnitude > 0.001f)
            {
                Vector2 direction = -moveInput.normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return Quaternion.Euler(0f, 0f, angle + rotationOffset);
            }
        }

        return transform.rotation;
    }
}