using UnityEngine;

public class EnemyRangedAI : EnemyBaseAI
{
    [Header("Ranged Attack Settings")]
    [SerializeField] private Transform firePoint;       // 탄환이 생성될 위치
    [SerializeField] private GameObject bulletPrefab;   // 적 탄환 프리팹
    [SerializeField] private int burstCount = 2;        // 한 번의 공격에서 몇 차례 나눠 쏠지
    [SerializeField] private int bulletsPerBurst = 1;   // 한 번에 몇 발을 쏠지
    [SerializeField] private float spreadAngle = 0f;   // 부채꼴 각도
    [SerializeField] private float burstInterval = 0.15f; // 탄막 사이 간격
    [SerializeField] private float attackCooldown = 2.5f; // 전체 공격 후 쿨타임

    private float stateTimer;
    private int remainingBursts;

    protected override void ResetStateData()
    {
        stateTimer = 0f;
        remainingBursts = 0;
    }

    /// <summary>
    /// 원거리 적은 공격 범위에 들어오면 Attack 상태로 들어갑니다.
    /// </summary>
    protected override void BeginAttack(Vector2 direction)
    {
        remainingBursts = burstCount;
        stateTimer = 0f; // 바로 첫 탄막을 쏘기 위해 0으로 설정
        desiredVelocity = Vector2.zero;
        ChangeState(EnemyState.Attack);
    }

    protected override void UpdateCharge(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;
    }

    protected override void UpdateDash(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;
    }

    protected override void UpdateAttack(float distance, Vector2 direction)
    {
        desiredVelocity = Vector2.zero;

        // 공격 중 플레이어가 너무 멀어지면 다시 추적으로 돌아갑니다.
        if (distance > attackRange * 1.25f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        FireSpread(direction);
        remainingBursts--;

        if (remainingBursts > 0)
        {
            stateTimer = burstInterval;
        }
        else
        {
            stateTimer = attackCooldown;
            ChangeState(EnemyState.Cooldown);
        }
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

    /// <summary>
    /// 현재 플레이어 방향을 기준으로 여러 발을 부채꼴로 발사합니다.
    /// </summary>
    private void FireSpread(Vector2 direction)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("원거리 적의 Bullet Prefab이 연결되지 않았습니다.", this);
            return;
        }

        Transform spawnPoint = firePoint != null ? firePoint : transform;
        Vector2 baseDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : (Vector2)spawnPoint.up;

        float startAngle = -spreadAngle * 0.5f;
        float angleStep = bulletsPerBurst > 1 ? spreadAngle / (bulletsPerBurst - 1) : 0f;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Vector2 shotDirection = RotateVector(baseDirection, currentAngle);

            GameObject bulletObject;
            if (PoolManager.Instance != null)
            {
                bulletObject = PoolManager.Instance.Get(bulletPrefab, spawnPoint.position, Quaternion.identity);
            }
            else
            {
                bulletObject = Instantiate(bulletPrefab, spawnPoint.position, Quaternion.identity);
            }

            Bullet bullet = bulletObject.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Initialize(shotDirection, ProjectileOwner.Enemy);
            }
        }
    }
    /// <summary>
    /// 방향 벡터를 특정 각도만큼 회전시켜 부채꼴 탄막을 만들 때 사용합니다.
    /// </summary>
    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radian);
        float sin = Mathf.Sin(radian);

        return new Vector2(
            (cos * vector.x) - (sin * vector.y),
            (sin * vector.x) + (cos * vector.y)
        );
    }
}