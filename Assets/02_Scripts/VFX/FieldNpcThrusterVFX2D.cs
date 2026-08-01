using UnityEngine;

/// <summary>
/// 기체형 필드 NPC의 배기구 포인트에서 이동 중 배기 VFX를 생성합니다.
/// 기존 8방향 플랫폼 배기 방식 대신 플레이어 기체와 같은 포인트 기반 생성 방식을 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class FieldNpcThrusterVFX2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FieldNpcInertialMover2D mover;
    [SerializeField] private Rigidbody2D body;
    [Tooltip("실제 이동량을 측정할 Transform입니다. 비워두면 Rigidbody 또는 이 오브젝트를 사용합니다.")]
    [SerializeField] private Transform movementReference;

    [Header("Exhaust Points")]
    [Tooltip("기체 뒤쪽 배기구 위치 Transform들입니다. 각 포인트의 회전을 배기 방향에 맞춰 두세요.")]
    [SerializeField] private Transform[] exhaustPoints;

    [Header("VFX Prefab")]
    [SerializeField] private GameObject moveExhaustPrefab;

    [Header("Spawn Timing")]
    [Min(0.01f)]
    [SerializeField] private float moveSpawnInterval = 0.08f;
    [Min(0.01f)]
    [SerializeField] private float effectLifeTime = 0.25f;

    [Header("Movement Detection")]
    [Min(0f)]
    [SerializeField] private float minimumMoveSpeed = 0.04f;
    [SerializeField] private bool useRigidbodyVelocity = true;
    [SerializeField] private bool useMeasuredTransformMovement = true;
    [Tooltip("한 프레임에 이 거리보다 크게 이동하면 순간이동으로 보고 배기를 생성하지 않습니다.")]
    [Min(0.1f)]
    [SerializeField] private float teleportIgnoreDistance = 1.5f;

    [Header("Options")]
    [SerializeField] private bool spawnAllExhaustPoints = true;
    [SerializeField] private bool useExhaustPointRotation = true;
    [SerializeField] private bool rotateAgainstMoveDirection;
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float positionJitter = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool drawExhaustPointGizmos = true;

    private float spawnTimer;
    private int nextExhaustIndex;
    private Vector2 previousPosition;
    private Vector2 measuredVelocity;
    private Vector2 currentMoveDirection = Vector2.up;
    private bool positionInitialized;

    public Vector2 CurrentMoveVelocity => ResolveMovementVelocity();
    public Vector2 CurrentMoveDirection => currentMoveDirection;
    public bool IsMoving => CurrentMoveVelocity.magnitude >= minimumMoveSpeed;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ResetPositionObservation();
    }

    private void OnEnable()
    {
        spawnTimer = 0f;
        nextExhaustIndex = 0;
        ResetPositionObservation();
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            ResetPositionObservation();
            return;
        }

        UpdateMeasuredMovement();

        Vector2 moveVelocity = ResolveMovementVelocity();
        float speed = moveVelocity.magnitude;

        if (speed < minimumMoveSpeed || moveExhaustPrefab == null)
        {
            spawnTimer = 0f;
            return;
        }

        currentMoveDirection = moveVelocity.normalized;
        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = Mathf.Max(0.01f, moveSpawnInterval);
        SpawnExhaust(moveExhaustPrefab);
    }

    private void CacheReferences()
    {
        if (mover == null)
        {
            mover = GetComponentInParent<FieldNpcInertialMover2D>();
        }

        if (body == null)
        {
            body = GetComponentInParent<Rigidbody2D>();
        }

        if (movementReference == null)
        {
            movementReference = body != null ? body.transform : transform;
        }
    }

    private void ResetPositionObservation()
    {
        Vector2 position = GetReferencePosition();
        previousPosition = position;
        measuredVelocity = Vector2.zero;
        positionInitialized = true;
    }

    private void UpdateMeasuredMovement()
    {
        Vector2 currentPosition = GetReferencePosition();

        if (!positionInitialized)
        {
            previousPosition = currentPosition;
            measuredVelocity = Vector2.zero;
            positionInitialized = true;
            return;
        }

        Vector2 delta = currentPosition - previousPosition;
        previousPosition = currentPosition;

        if (!useMeasuredTransformMovement ||
            Time.deltaTime <= 0f ||
            delta.magnitude > Mathf.Max(0.1f, teleportIgnoreDistance))
        {
            measuredVelocity = Vector2.zero;
            return;
        }

        measuredVelocity = delta / Time.deltaTime;
    }

    private Vector2 ResolveMovementVelocity()
    {
        Vector2 bestVelocity = Vector2.zero;

        if (useRigidbodyVelocity && body != null)
        {
            bestVelocity = body.linearVelocity;
        }

        if (mover != null && mover.MeasuredVelocity.sqrMagnitude > bestVelocity.sqrMagnitude)
        {
            bestVelocity = mover.MeasuredVelocity;
        }

        if (useMeasuredTransformMovement && measuredVelocity.sqrMagnitude > bestVelocity.sqrMagnitude)
        {
            bestVelocity = measuredVelocity;
        }

        return bestVelocity;
    }

    private Vector2 GetReferencePosition()
    {
        if (movementReference != null)
        {
            return movementReference.position;
        }

        if (body != null)
        {
            return body.position;
        }

        return transform.position;
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

        if (rotateAgainstMoveDirection && currentMoveDirection.sqrMagnitude > 0.001f)
        {
            Vector2 direction = -currentMoveDirection.normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }

        return transform.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawExhaustPointGizmos || exhaustPoints == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.95f, 1f, 0.9f);

        for (int i = 0; i < exhaustPoints.Length; i++)
        {
            Transform point = exhaustPoints[i];

            if (point == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(point.position, 0.035f);
            Gizmos.DrawLine(point.position, point.position + point.up * 0.2f);
        }
    }
}
