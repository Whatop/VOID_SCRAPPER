using UnityEngine;

/// <summary>
/// 필드 NPC를 생성 지점 주변에서 천천히 순항시키는 관성 이동 컨트롤러입니다.
/// 판정 Root에 붙이고, 회전은 별도의 ShipRotationRoot에 적용하는 것을 권장합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class FieldNpcInertialMover2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private FieldNpcObjective objective;
    [SerializeField] private Transform anchorOverride;

    [Header("Wander Area")]
    [Tooltip("생성 위치를 중심으로 NPC가 움직이는 최대 반경입니다.")]
    [Min(0f)]
    [SerializeField] private float wanderRadius = 1.8f;

    [Tooltip("목표 지점에 이 거리만큼 접근하면 잠시 정지한 뒤 다음 목표를 고릅니다.")]
    [Min(0.01f)]
    [SerializeField] private float destinationReachDistance = 0.2f;

    [SerializeField] private Vector2 destinationPauseRange = new Vector2(0.45f, 1.1f);

    [Header("Inertial Movement")]
    [Tooltip("NPC의 최대 순항 속도입니다.")]
    [Min(0f)]
    [SerializeField] private float maxSpeed = 0.75f;

    [Tooltip("목표 속도까지 가속되는 정도입니다. 낮을수록 묵직하게 출발합니다.")]
    [Min(0.01f)]
    [SerializeField] private float acceleration = 0.85f;

    [Tooltip("정지하거나 방향을 바꿀 때 감속되는 정도입니다. 가속보다 낮으면 관성이 더 오래 남습니다.")]
    [Min(0.01f)]
    [SerializeField] private float deceleration = 0.55f;

    [Tooltip("목표 지점 근처에서 속도를 줄이기 시작하는 거리입니다.")]
    [Min(0.05f)]
    [SerializeField] private float arrivalSlowRadius = 0.65f;

    [Tooltip("영역 밖으로 밀렸을 때 중심으로 복귀시키는 배율입니다.")]
    [Min(0.1f)]
    [SerializeField] private float returnSpeedMultiplier = 1.25f;

    [Header("Interaction Comfort")]
    [SerializeField] private bool stopNearPlayer = true;
    [Min(0f)]
    [SerializeField] private float playerStopRadius = 1.6f;
    [Min(0.05f)]
    [SerializeField] private float playerSearchInterval = 0.5f;

    [Header("State Locks")]
    [SerializeField] private bool holdDuringRescueCombat = true;
    [SerializeField] private bool holdWhileRewardPending = true;

    [Header("Ship Rotation")]
    [Tooltip("이동 방향으로 기체 비주얼을 회전합니다.")]
    [SerializeField] private bool rotateToMovement = true;

    [Tooltip("회전시킬 기체 비주얼 Root입니다. Collider가 있는 물리 Root보다 자식 ShipRotationRoot를 권장합니다.")]
    [SerializeField] private Transform rotationRoot;

    [Tooltip("스프라이트 정면이 위쪽(+Y)인 경우 -90을 사용합니다. 플레이어와 같은 기준입니다.")]
    [SerializeField] private float rotationOffset = -90f;

    [Min(0f)]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("이 속도보다 느리면 현재 방향을 유지합니다.")]
    [Min(0f)]
    [SerializeField] private float minimumRotationSpeed = 0.03f;

    [Tooltip("실제 이동이 거의 없을 때 목표 이동 방향을 보고 미리 회전합니다.")]
    [SerializeField] private bool useDesiredDirectionWhenNearlyStopped;

    [Tooltip("한 프레임에 이 거리보다 크게 위치가 바뀌면 순간이동으로 보고 회전 계산에서 제외합니다.")]
    [Min(0.1f)]
    [SerializeField] private float teleportIgnoreDistance = 1.5f;

    [Header("Rigidbody Setup")]
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private bool freezeRotation = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private Vector2 anchorPosition;
    private Vector2 destination;
    private Vector2 currentAcceleration;
    private Vector2 measuredVelocity;
    private Vector2 previousObservedPosition;
    private Vector2 facingDirection = Vector2.up;
    private Transform player;
    private float pauseTimer;
    private float playerSearchTimer;
    private bool forcedHold;
    private bool initialized;
    private bool observationInitialized;

    public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;
    public Vector2 CurrentAcceleration => currentAcceleration;
    public Vector2 MeasuredVelocity => measuredVelocity;
    public Vector2 FacingDirection => facingDirection;
    public Vector2 DesiredMoveDirection { get; private set; }
    public float MaxSpeed => maxSpeed;
    public float NormalizedSpeed => maxSpeed <= 0.001f
        ? 0f
        : Mathf.Clamp01(Mathf.Max(CurrentVelocity.magnitude, measuredVelocity.magnitude) / maxSpeed);
    public bool IsHoldingPosition => forcedHold || ShouldHoldForObjective() || IsPlayerTooClose();
    public bool IsActuallyMoving => Mathf.Max(CurrentVelocity.magnitude, measuredVelocity.magnitude) >= minimumRotationSpeed;
    public Vector2 AnchorPosition => anchorPosition;

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
        objective = GetComponent<FieldNpcObjective>();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureBody();
        CaptureAnchor();
        PickNextDestination();
        ResetMotionObservation();
        initialized = true;
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!initialized)
        {
            ConfigureBody();
            CaptureAnchor();
            PickNextDestination();
            initialized = true;
        }

        playerSearchTimer = 0f;
        ResetMotionObservation();
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        currentAcceleration = Vector2.zero;
        measuredVelocity = Vector2.zero;
        DesiredMoveDirection = Vector2.zero;
        observationInitialized = false;
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        UpdatePlayerReference(Time.fixedDeltaTime);

        Vector2 previousVelocity = body.linearVelocity;
        Vector2 targetVelocity = CalculateTargetVelocity(Time.fixedDeltaTime);
        float rate = targetVelocity.sqrMagnitude > previousVelocity.sqrMagnitude
            ? acceleration
            : deceleration;

        Vector2 nextVelocity = Vector2.MoveTowards(
            previousVelocity,
            targetVelocity,
            Mathf.Max(0.01f, rate) * Time.fixedDeltaTime
        );

        body.linearVelocity = nextVelocity;
        currentAcceleration = Time.fixedDeltaTime > 0f
            ? (nextVelocity - previousVelocity) / Time.fixedDeltaTime
            : Vector2.zero;

        DesiredMoveDirection = targetVelocity.sqrMagnitude > 0.0001f
            ? targetVelocity.normalized
            : Vector2.zero;
    }

    private void LateUpdate()
    {
        UpdateMeasuredMovement();
        UpdateShipRotation();
    }

    public void SetForcedHold(bool hold)
    {
        forcedHold = hold;
    }

    public void RecenterAnchor(bool stopImmediately = false)
    {
        CaptureAnchor();
        PickNextDestination();

        if (stopImmediately && body != null)
        {
            body.linearVelocity = Vector2.zero;
            currentAcceleration = Vector2.zero;
        }
    }

    public void SetAnchorPosition(Vector2 worldPosition, bool pickNewDestination = true)
    {
        anchorPosition = worldPosition;

        if (pickNewDestination)
        {
            PickNextDestination();
        }
    }

    public void AddVelocityImpulse(Vector2 velocityImpulse)
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity += velocityImpulse;
    }

    public void SetFacingDirection(Vector2 direction, bool immediate = false)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        facingDirection = direction.normalized;
        RotateToDirection(facingDirection, immediate);
    }

    private Vector2 CalculateTargetVelocity(float deltaTime)
    {
        if (IsHoldingPosition)
        {
            pauseTimer = Mathf.Max(pauseTimer, 0.1f);
            return Vector2.zero;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= deltaTime;
            return Vector2.zero;
        }

        Vector2 currentPosition = body.position;
        Vector2 fromAnchor = currentPosition - anchorPosition;
        bool outsideWanderArea = wanderRadius > 0f && fromAnchor.magnitude > wanderRadius;

        if (outsideWanderArea)
        {
            Vector2 returnDirection = (anchorPosition - currentPosition).normalized;
            return returnDirection * maxSpeed * Mathf.Max(0.1f, returnSpeedMultiplier);
        }

        Vector2 toDestination = destination - currentPosition;
        float distance = toDestination.magnitude;

        if (distance <= Mathf.Max(0.01f, destinationReachDistance))
        {
            PickNextDestination();
            pauseTimer = Random.Range(
                Mathf.Min(destinationPauseRange.x, destinationPauseRange.y),
                Mathf.Max(destinationPauseRange.x, destinationPauseRange.y)
            );
            return Vector2.zero;
        }

        Vector2 direction = toDestination / Mathf.Max(distance, 0.0001f);
        float speedRatio = Mathf.Clamp01(distance / Mathf.Max(0.05f, arrivalSlowRadius));
        float targetSpeed = maxSpeed * Mathf.Lerp(0.2f, 1f, speedRatio);
        return direction * targetSpeed;
    }

    private void UpdateMeasuredMovement()
    {
        Vector2 currentPosition = body != null
            ? body.position
            : (Vector2)transform.position;

        if (!observationInitialized)
        {
            previousObservedPosition = currentPosition;
            measuredVelocity = Vector2.zero;
            observationInitialized = true;
            return;
        }

        Vector2 delta = currentPosition - previousObservedPosition;
        previousObservedPosition = currentPosition;

        if (Time.deltaTime <= 0f || delta.magnitude > Mathf.Max(0.1f, teleportIgnoreDistance))
        {
            measuredVelocity = Vector2.zero;
            return;
        }

        measuredVelocity = delta / Time.deltaTime;
    }

    private void UpdateShipRotation()
    {
        if (!rotateToMovement)
        {
            return;
        }

        Vector2 direction = ResolveRotationDirection();

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        facingDirection = direction.normalized;
        RotateToDirection(facingDirection, false);
    }

    private Vector2 ResolveRotationDirection()
    {
        float minimumSqr = minimumRotationSpeed * minimumRotationSpeed;

        if (measuredVelocity.sqrMagnitude >= minimumSqr)
        {
            return measuredVelocity.normalized;
        }

        Vector2 bodyVelocity = CurrentVelocity;
        if (bodyVelocity.sqrMagnitude >= minimumSqr)
        {
            return bodyVelocity.normalized;
        }

        if (useDesiredDirectionWhenNearlyStopped && DesiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            return DesiredMoveDirection.normalized;
        }

        return Vector2.zero;
    }

    private void RotateToDirection(Vector2 direction, bool immediate)
    {
        Transform target = rotationRoot != null ? rotationRoot : transform;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

        if (immediate || rotationSpeed <= 0f)
        {
            target.rotation = targetRotation;
            return;
        }

        target.rotation = Quaternion.RotateTowards(
            target.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void ResetMotionObservation()
    {
        previousObservedPosition = body != null
            ? body.position
            : (Vector2)transform.position;

        measuredVelocity = Vector2.zero;
        observationInitialized = true;
    }

    private bool ShouldHoldForObjective()
    {
        if (objective == null)
        {
            return false;
        }

        if (objective.ShouldHoldMovement)
        {
            if (objective.State == FieldNpcState.RescueCombat)
            {
                return holdDuringRescueCombat;
            }

            if (objective.State == FieldNpcState.RewardPending)
            {
                return holdWhileRewardPending;
            }
        }

        return false;
    }

    private bool IsPlayerTooClose()
    {
        if (!stopNearPlayer || player == null || playerStopRadius <= 0f)
        {
            return false;
        }

        return ((Vector2)player.position - body.position).sqrMagnitude <= playerStopRadius * playerStopRadius;
    }

    private void UpdatePlayerReference(float deltaTime)
    {
        playerSearchTimer -= deltaTime;

        if (player != null && player.gameObject.activeInHierarchy)
        {
            return;
        }

        if (playerSearchTimer > 0f)
        {
            return;
        }

        playerSearchTimer = Mathf.Max(0.05f, playerSearchInterval);
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void PickNextDestination()
    {
        if (wanderRadius <= 0.01f)
        {
            destination = anchorPosition;
            return;
        }

        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        destination = anchorPosition + offset;
    }

    private void CaptureAnchor()
    {
        anchorPosition = anchorOverride != null
            ? (Vector2)anchorOverride.position
            : body != null
                ? body.position
                : (Vector2)transform.position;
    }

    private void ResolveReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (objective == null)
        {
            objective = GetComponent<FieldNpcObjective>();
        }
    }

    private void ConfigureBody()
    {
        if (!configureRigidbodyOnAwake || body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.freezeRotation = freezeRotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 center = Application.isPlaying
            ? anchorPosition
            : anchorOverride != null
                ? anchorOverride.position
                : transform.position;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0f, wanderRadius));

        if (Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.75f);
            Gizmos.DrawLine(transform.position, destination);
            Gizmos.DrawWireSphere(destination, 0.08f);

            Gizmos.color = new Color(0.3f, 1f, 0.55f, 0.85f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(facingDirection * 0.5f));
        }
    }
}
