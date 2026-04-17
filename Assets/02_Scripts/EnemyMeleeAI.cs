using UnityEngine;

public class EnemyMeleeAI : EnemyBaseAI
{
    [Header("Melee Attack Settings")]
    [SerializeField] private float chargeTime = 0.5f;   // 돌진 전 차징 시간
    [SerializeField] private float dashSpeed = 9f;      // 돌진 속도
    [SerializeField] private float dashDuration = 0.35f; // 돌진 지속 시간
    [SerializeField] private float cooldownTime = 1f;   // 돌진 후 대기 시간
    [SerializeField] private int contactDamage = 1;     // 돌진 중 플레이어에게 줄 대미지

    private float stateTimer;
    private Vector2 dashDirection;
    private bool hasDamagedPlayerThisDash;

    protected override void ResetStateData()
    {
        stateTimer = 0f;
        dashDirection = Vector2.zero;
        hasDamagedPlayerThisDash = false;
    }

    /// <summary>
    /// 근접 적은 공격 범위에 들어오면 먼저 차징 상태로 들어갑니다.
    /// </summary>
    protected override void BeginAttack(Vector2 direction)
    {
        stateTimer = chargeTime;
        desiredVelocity = Vector2.zero;
        ChangeState(EnemyState.Charge);
    }

    protected override void UpdateCharge(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;

        // 차징 도중 플레이어가 너무 멀어지면 다시 추적으로 돌아갑니다.
        if (distance > attackRange * 1.35f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            dashDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : (Vector2)transform.up;
            desiredVelocity = dashDirection * dashSpeed;

            stateTimer = dashDuration;
            hasDamagedPlayerThisDash = false;
            ChangeState(EnemyState.Dash);
        }
    }

    protected override void UpdateDash(float distance, Vector2 direction)
    {
        desiredVelocity = dashDirection * dashSpeed;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            desiredVelocity = Vector2.zero;
            stateTimer = cooldownTime;
            ChangeState(EnemyState.Cooldown);
        }
    }

    protected override void UpdateAttack(float distance, Vector2 direction)
    {
        // 근접 적은 Attack 상태를 사용하지 않고
        // Charge -> Dash -> Cooldown만 사용합니다.
        desiredVelocity = Vector2.zero;
    }

    protected override void UpdateCooldown(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;
        stateTimer -= Time.deltaTime;

        if (stateTimer > 0f)
        {
            return;
        }

        if (distance <= attackRange)
        {
            BeginAttack(direction);
        }
        else if (distance <= detectionRange)
        {
            ChangeState(EnemyState.Chase);
        }
        else
        {
            ChangeState(EnemyState.Idle);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 돌진 상태일 때만 플레이어에게 대미지를 줍니다.
        if (currentState != EnemyState.Dash)
        {
            return;
        }

        // 한 번의 돌진에서 여러 번 맞는 것을 방지합니다.
        if (hasDamagedPlayerThisDash)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(contactDamage);
            hasDamagedPlayerThisDash = true;
        }
    }
}