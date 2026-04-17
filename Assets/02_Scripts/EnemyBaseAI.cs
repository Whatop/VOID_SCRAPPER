using UnityEngine;

public enum EnemyState
{
    Idle,       // 대기 상태
    Chase,      // 플레이어 추적
    Charge,     // 차징(근접 적 전용)
    Dash,       // 돌진(근접 적 전용)
    Attack,     // 공격(원거리 적 전용)
    Cooldown,   // 공격 후 딜레이
    Dead        // 사망
}

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBaseAI : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] protected Transform target;          // 추적할 대상(플레이어)
    [SerializeField] protected string playerTag = "Player";
    [SerializeField] protected float detectionRange = 8f; // 감지 범위
    [SerializeField] protected float attackRange = 3f;    // 공격 범위
    [SerializeField] protected float lostTargetRange = 11f; // 너무 멀어졌을 때 다시 Idle로 돌아가는 범위

    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 2.5f;    // 추적 이동 속도
    [SerializeField] protected float rotationOffset = -90f; // 스프라이트 기본 방향 보정값

    [Header("Current State")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    protected Rigidbody2D rb;
    protected Vector2 desiredVelocity;
    protected bool isDead;

    public EnemyState CurrentState => currentState;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    protected virtual void OnEnable()
    {
        isDead = false;
        desiredVelocity = Vector2.zero;
        currentState = EnemyState.Idle;

        FindTarget();
        ResetStateData();
    }

    protected virtual void OnDisable()
    {
        desiredVelocity = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    protected virtual void Update()
    {
        if (isDead)
        {
            return;
        }

        EnsureTarget();

        if (target == null)
        {
            desiredVelocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
            return;
        }

        Vector2 direction = GetDirectionToTarget();
        float distance = direction.magnitude;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle(distance, direction);
                break;

            case EnemyState.Chase:
                UpdateChase(distance, direction);
                break;

            case EnemyState.Charge:
                UpdateCharge(distance, direction);
                break;

            case EnemyState.Dash:
                UpdateDash(distance, direction);
                break;

            case EnemyState.Attack:
                UpdateAttack(distance, direction);
                break;

            case EnemyState.Cooldown:
                UpdateCooldown(distance, direction);
                break;
        }

        RotateToDirection(GetFacingDirection(direction));
    }

    protected virtual void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = desiredVelocity;
        }
    }

    /// <summary>
    /// Idle 상태에서는 멈춰 있다가 플레이어가 감지 범위 안으로 들어오면 Chase 상태로 전환합니다.
    /// </summary>
    protected virtual void UpdateIdle(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;

        if (distance <= detectionRange)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    /// <summary>
    /// Chase 상태에서는 플레이어를 향해 이동합니다.
    /// 공격 범위 안에 들어오면 각 적 유형의 공격 상태로 전환합니다.
    /// </summary>
    protected virtual void UpdateChase(float distance, Vector2 direction)
    {
        if (distance > lostTargetRange)
        {
            desiredVelocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
            return;
        }

        if (distance <= attackRange)
        {
            desiredVelocity = Vector2.zero;
            BeginAttack(direction);
            return;
        }

        desiredVelocity = direction.normalized * moveSpeed;
    }

    protected abstract void BeginAttack(Vector2 direction);
    protected abstract void UpdateCharge(float distance, Vector2 direction);
    protected abstract void UpdateDash(float distance, Vector2 direction);
    protected abstract void UpdateAttack(float distance, Vector2 direction);
    protected abstract void UpdateCooldown(float distance, Vector2 direction);

    protected virtual void ResetStateData()
    {
    }

    protected virtual Vector2 GetFacingDirection(Vector2 currentDirection)
    {
        // 돌진 중에는 현재 속도 방향을 바라보도록 합니다.
        if (currentState == EnemyState.Dash && desiredVelocity.sqrMagnitude > 0.001f)
        {
            return desiredVelocity;
        }

        return currentDirection;
    }

    protected void RotateToDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    protected Vector2 GetDirectionToTarget()
    {
        if (target == null)
        {
            return Vector2.zero;
        }

        return (Vector2)(target.position - transform.position);
    }

    protected void EnsureTarget()
    {
        if (target != null && !target.gameObject.activeInHierarchy)
        {
            target = null;
        }

        if (target == null)
        {
            FindTarget();
        }
    }

    protected void FindTarget()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }

    protected void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
        OnStateChanged(newState);
    }

    protected virtual void OnStateChanged(EnemyState newState)
    {
    }

    /// <summary>
    /// 스포너가 직접 플레이어 Transform을 연결할 수 있도록 만든 함수입니다.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// 적이 사망했을 때 공통으로 호출되는 함수입니다.
    /// </summary>
    public virtual void OnDeath()
    {
        isDead = true;
        desiredVelocity = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        currentState = EnemyState.Dead;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}