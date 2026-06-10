using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBaseAI : MonoBehaviour
{
    private enum EnemyPurpose
    {
        Scout,
        Ambusher,
        Interceptor,
        Sentinel,
        Boss,
        ShopGuard
    }

    [Header("Definition")]
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Visual Facing")]
    [Tooltip("비워두면 자기 transform을 회전합니다. 권장: VisualRoot 또는 ShipBody를 넣으세요.")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool rotateToFacingDirection = true;
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float turnSpeed = 720f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private float ambusherMoveSpeedMultiplier = 1.15f;
    [SerializeField] private float interceptorMoveSpeedMultiplier = 1.35f;
    [SerializeField] private float sentinelStrafeSpeedMultiplier = 0.75f;

    [Header("Detection")]
    [SerializeField] private float visionRange = 9f;
    [SerializeField] private float loseSightTime = 2f;

    [Header("Alert")]
    [SerializeField] private float alertDuration = 3f;

    [Header("Search")]
    [SerializeField] private float searchDuration = 2.5f;

    [Header("Taunt")]
    [SerializeField] private float defaultTauntDuration = 4f;

    [Header("Combat Distance")]
    [SerializeField] private float preferredCombatDistanceRatio = 0.75f;
    [SerializeField] private float closeCombatDistanceRatio = 0.45f;

    [Header("Debug")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;
    [SerializeField] private EnemyPurpose purpose = EnemyPurpose.Scout;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private EnemyAttackController attackController;
    private RadarTarget radarTarget;

    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private Vector2 alertTarget;
    private Vector2 lastSeenPlayerPosition;
    private Vector2 tauntTarget;

    private Vector2 desiredVelocity;
    private Vector2 facingDirection = Vector2.up;

    private float stateTimer;
    private float lostSightTimer;
    private float strafeTimer;
    private int strafeDirection = 1;
    private bool initialized;

    public EnemyState CurrentState => currentState;
    public EnemyDefinition EnemyDefinition => enemyDefinition;
    public Transform Player => player;
    public Vector2 DesiredVelocity => desiredVelocity;
    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => desiredVelocity.sqrMagnitude > 0.01f;

    public bool IsAware =>
        currentState == EnemyState.Alert ||
        currentState == EnemyState.Combat ||
        currentState == EnemyState.Search ||
        currentState == EnemyState.Taunt;

    public bool IsRadarTauntable =>
        currentState != EnemyState.Dead &&
        purpose != EnemyPurpose.Boss &&
        purpose != EnemyPurpose.ShopGuard;

    public event Action<EnemyState, EnemyState> StateChanged;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        attackController = GetComponent<EnemyAttackController>();
        radarTarget = GetComponent<RadarTarget>();
        visualRoot = transform;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        attackController = GetComponent<EnemyAttackController>();
        radarTarget = GetComponent<RadarTarget>();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDied;
        }

        spawnPosition = transform.position;
        desiredVelocity = Vector2.zero;
        lostSightTimer = 0f;
        stateTimer = 0f;
        strafeTimer = 0f;
        initialized = true;

        ResolvePlayer();

        if (enemyDefinition != null)
        {
            ApplyDefinition(enemyDefinition);
        }

        SetState(EnemyState.Patrol, true);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }

        StopMoving();
        initialized = false;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (health != null && health.IsDead)
        {
            SetState(EnemyState.Dead);
            return;
        }

        ResolvePlayer();

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            case EnemyState.Alert:
                UpdateAlert();
                break;

            case EnemyState.Combat:
                UpdateCombat();
                break;

            case EnemyState.Search:
                UpdateSearch();
                break;

            case EnemyState.Return:
                UpdateReturn();
                break;

            case EnemyState.Taunt:
                UpdateTaunt();
                break;

            case EnemyState.Dead:
                StopMoving();
                break;
        }

        ApplyFacingRotation(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        if (currentState == EnemyState.Dead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = desiredVelocity;
    }

    public void ApplyDefinition(EnemyDefinition definition)
    {
        enemyDefinition = definition;

        if (enemyDefinition == null)
        {
            return;
        }

        moveSpeed = enemyDefinition.MoveSpeed;
        visionRange = enemyDefinition.VisionRange;
        purpose = ResolvePurpose(enemyDefinition.EnemyType);

        if (health != null)
        {
            health.ApplyDefinition(enemyDefinition);
        }

        if (attackController != null)
        {
            attackController.ApplyDefinition(enemyDefinition);
        }

        if (radarTarget != null)
        {
            radarTarget.SetMarkerType(enemyDefinition.RadarMarkerType);
        }

        if (initialized && currentState == EnemyState.Patrol)
        {
            PickNewPatrolTarget();
        }
    }

    public void SetTarget(Transform target)
    {
        player = target;
    }

    public void NotifyDamagedByPlayer()
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        if (player != null)
        {
            lastSeenPlayerPosition = player.position;
            FaceTo(player.position);
        }

        SetState(EnemyState.Combat);
    }

    public void AlertTo(Vector2 targetPosition)
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        alertTarget = targetPosition;
        FaceTo(alertTarget);
        SetState(EnemyState.Alert);
    }

    public void ApplyTaunt(Vector2 targetPosition, float duration)
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        if (!IsRadarTauntable)
        {
            return;
        }

        tauntTarget = targetPosition;
        stateTimer = duration > 0f ? duration : defaultTauntDuration;
        FaceTo(tauntTarget);
        SetState(EnemyState.Taunt);
    }

    public void ApplyRadarTaunt(Vector2 scanOrigin)
    {
        ApplyTaunt(scanOrigin, defaultTauntDuration);
    }

    public void ApplyRadarAlert(Vector2 scanOrigin)
    {
        AlertTo(scanOrigin);
    }

    public bool IsThreateningPlayer()
    {
        return currentState == EnemyState.Alert ||
               currentState == EnemyState.Combat ||
               currentState == EnemyState.Search ||
               currentState == EnemyState.Taunt;
    }

    private void UpdatePatrol()
    {
        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        if (Vector2.Distance(transform.position, patrolTarget) <= arriveDistance)
        {
            PickNewPatrolTarget();
        }

        MoveTo(patrolTarget, moveSpeed);
    }

    private void UpdateAlert()
    {
        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        stateTimer -= Time.deltaTime;

        FaceTo(alertTarget);
        MoveTo(alertTarget, moveSpeed);

        if (stateTimer <= 0f || Vector2.Distance(transform.position, alertTarget) <= arriveDistance)
        {
            lastSeenPlayerPosition = alertTarget;
            SetState(EnemyState.Search);
        }
    }

    private void UpdateCombat()
    {
        if (player == null)
        {
            SetState(EnemyState.Search);
            return;
        }

        FaceTo(player.position);

        if (CanSeePlayer())
        {
            lostSightTimer = 0f;
            lastSeenPlayerPosition = player.position;
        }
        else
        {
            lostSightTimer += Time.deltaTime;

            if (lostSightTimer >= loseSightTime)
            {
                SetState(EnemyState.Search);
                return;
            }
        }

        switch (purpose)
        {
            case EnemyPurpose.Ambusher:
                UpdateAmbusherCombat();
                break;

            case EnemyPurpose.Interceptor:
                UpdateInterceptorCombat();
                break;

            case EnemyPurpose.Sentinel:
            case EnemyPurpose.Boss:
            case EnemyPurpose.ShopGuard:
                UpdateSentinelCombat();
                break;

            default:
                UpdateScoutCombat();
                break;
        }
    }

    private void UpdateScoutCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackRange > 0f && distanceToPlayer <= attackRange)
        {
            StopMoving();
            TryAttackPlayer();
            return;
        }

        MoveTo(player.position, moveSpeed);
    }

    private void UpdateAmbusherCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();
        float desiredCloseDistance = Mathf.Max(0.5f, attackRange * closeCombatDistanceRatio);

        if (distanceToPlayer > desiredCloseDistance)
        {
            MoveTo(player.position, moveSpeed * ambusherMoveSpeedMultiplier);
        }
        else
        {
            StopMoving();
        }

        if (attackRange > 0f && distanceToPlayer <= attackRange)
        {
            TryAttackPlayer();
        }
    }

    private void UpdateInterceptorCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackRange > 0f && distanceToPlayer <= attackRange)
        {
            StopMoving();
            TryAttackPlayer();
            return;
        }

        MoveTo(player.position, moveSpeed * interceptorMoveSpeedMultiplier);
    }

    private void UpdateSentinelCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackRange <= 0f)
        {
            MoveTo(player.position, moveSpeed);
            return;
        }

        float preferredDistance = Mathf.Max(1f, attackRange * preferredCombatDistanceRatio);
        float tooCloseDistance = Mathf.Max(0.5f, preferredDistance * 0.65f);

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        Vector2 moveDirection = Vector2.zero;

        if (distanceToPlayer < tooCloseDistance)
        {
            moveDirection = -toPlayer.normalized;
        }
        else if (distanceToPlayer > preferredDistance)
        {
            moveDirection = toPlayer.normalized;
        }
        else
        {
            strafeTimer -= Time.deltaTime;

            if (strafeTimer <= 0f)
            {
                strafeTimer = UnityEngine.Random.Range(1.0f, 2.2f);
                strafeDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
            }

            moveDirection = new Vector2(-toPlayer.y, toPlayer.x).normalized * strafeDirection;
        }

        desiredVelocity = moveDirection * moveSpeed * sentinelStrafeSpeedMultiplier;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            SetFacing(moveDirection);
        }

        if (distanceToPlayer <= attackRange)
        {
            TryAttackPlayer();
        }
    }

    private void UpdateSearch()
    {
        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        stateTimer -= Time.deltaTime;

        FaceTo(lastSeenPlayerPosition);

        if (Vector2.Distance(transform.position, lastSeenPlayerPosition) > arriveDistance)
        {
            MoveTo(lastSeenPlayerPosition, moveSpeed);
        }
        else
        {
            StopMoving();
        }

        if (stateTimer <= 0f)
        {
            SetState(EnemyState.Return);
        }
    }

    private void UpdateReturn()
    {
        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        FaceTo(spawnPosition);
        MoveTo(spawnPosition, moveSpeed);

        if (Vector2.Distance(transform.position, spawnPosition) <= arriveDistance)
        {
            SetState(EnemyState.Patrol);
        }
    }

    private void UpdateTaunt()
    {
        if (CanSeePlayer() && player != null)
        {
            lastSeenPlayerPosition = player.position;
        }

        stateTimer -= Time.deltaTime;

        FaceTo(tauntTarget);
        MoveTo(tauntTarget, moveSpeed * ambusherMoveSpeedMultiplier);

        if (stateTimer <= 0f || Vector2.Distance(transform.position, tauntTarget) <= arriveDistance)
        {
            if (CanSeePlayer())
            {
                SetState(EnemyState.Combat);
            }
            else
            {
                lastSeenPlayerPosition = tauntTarget;
                SetState(EnemyState.Search);
            }
        }
    }

    private void TryAttackPlayer()
    {
        if (attackController == null || player == null)
        {
            return;
        }

        FaceTo(player.position);
        attackController.TryAttack(player);
    }

    private void SetState(EnemyState nextState, bool force = false)
    {
        if (!force && currentState == nextState)
        {
            return;
        }

        EnemyState previousState = currentState;
        currentState = nextState;

        switch (currentState)
        {
            case EnemyState.Patrol:
                lostSightTimer = 0f;
                PickNewPatrolTarget();
                break;

            case EnemyState.Alert:
                stateTimer = alertDuration;
                break;

            case EnemyState.Combat:
                lostSightTimer = 0f;

                if (player != null)
                {
                    lastSeenPlayerPosition = player.position;
                    FaceTo(player.position);
                }

                break;

            case EnemyState.Search:
                stateTimer = searchDuration;
                break;

            case EnemyState.Return:
                break;

            case EnemyState.Taunt:
                if (stateTimer <= 0f)
                {
                    stateTimer = defaultTauntDuration;
                }

                break;

            case EnemyState.Dead:
                StopMoving();
                break;
        }

        StateChanged?.Invoke(previousState, currentState);
    }

    private void PickNewPatrolTarget()
    {
        Vector2 target;

        switch (purpose)
        {
            case EnemyPurpose.Ambusher:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius * 0.65f;
                break;

            case EnemyPurpose.Interceptor:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle.normalized * patrolRadius;
                break;

            case EnemyPurpose.Sentinel:
            case EnemyPurpose.ShopGuard:
            case EnemyPurpose.Boss:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius * 0.45f;
                break;

            default:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius;
                break;
        }

        patrolTarget = target;
        FaceTo(patrolTarget);
    }

    private void MoveTo(Vector2 targetPosition, float speed)
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = targetPosition - currentPosition;

        if (direction.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            StopMoving();
            return;
        }

        direction.Normalize();
        desiredVelocity = direction * Mathf.Max(0f, speed);
        SetFacing(direction);
    }

    private void StopMoving()
    {
        desiredVelocity = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void FaceTo(Vector2 targetPosition)
    {
        if (IsFacingLockedByAttack())
        {
            return;
        }

        Vector2 direction = targetPosition - (Vector2)transform.position;
        SetFacing(direction);
    }

    private void SetFacing(Vector2 direction)
    {
        if (IsFacingLockedByAttack())
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        facingDirection = direction.normalized;
    }
    private bool IsFacingLockedByAttack()
    {
        return attackController != null && attackController.IsCharging;
    }
    private void ApplyFacingRotation(float deltaTime)
    {
        if (!rotateToFacingDirection)
        {
            return;
        }

        Transform targetRoot = visualRoot != null ? visualRoot : transform;

        if (targetRoot == null)
        {
            return;
        }

        if (facingDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        if (turnSpeed <= 0f)
        {
            targetRoot.rotation = targetRotation;
            return;
        }

        targetRoot.rotation = Quaternion.RotateTowards(
            targetRoot.rotation,
            targetRotation,
            turnSpeed * deltaTime
        );
    }

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        float sqrDistance = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
        return sqrDistance <= visionRange * visionRange;
    }

    private float GetDistanceToPlayer()
    {
        if (player == null)
        {
            return float.MaxValue;
        }

        return Vector2.Distance(transform.position, player.position);
    }

    private float GetAttackRange()
    {
        return attackController != null ? attackController.AttackRange : 0f;
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

    private EnemyPurpose ResolvePurpose(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Shotgun => EnemyPurpose.Ambusher,
            EnemyType.Charging => EnemyPurpose.Interceptor,
            EnemyType.Elite => EnemyPurpose.Sentinel,
            EnemyType.Boss => EnemyPurpose.Boss,
            EnemyType.ShopDrone => EnemyPurpose.ShopGuard,
            _ => EnemyPurpose.Scout
        };
    }

    private void HandleDied(EnemyHealth enemyHealth)
    {
        SetState(EnemyState.Dead);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.green;
        Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(facingDirection.normalized * 1.5f));
    }
}