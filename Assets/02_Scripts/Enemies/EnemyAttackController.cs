using System;
using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Header("Fire Point")]
    [SerializeField] private Transform firePoint;

    [Header("Fallback Attack")]
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle = 0f;
    [SerializeField] private float chargeTime = 0f;

    [Header("Runtime")]
    [SerializeField] private bool isAttacking;
    [SerializeField] private bool isCharging;

    private float attackTimer;
    private Coroutine chargeRoutine;

    public float AttackRange => attackRange;
    public bool IsAttacking => isAttacking;
    public bool IsCharging => isCharging;
    public bool CanAttack => !isAttacking && !isCharging && attackTimer <= 0f;

    public event Action<EnemyAttackController> AttackStarted;
    public event Action<EnemyAttackController> ProjectileFired;
    public event Action<EnemyAttackController> AttackFinished;
    public event Action<EnemyAttackController> ChargeStarted;
    public event Action<EnemyAttackController> ChargeReleased;
    public event Action<EnemyAttackController> ChargeCanceled;

    private void Awake()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    private void Update()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    private void OnDisable()
    {
        CancelCharge();
        isAttacking = false;
        isCharging = false;
        attackTimer = 0f;
    }

    public void ApplyDefinition(EnemyDefinition definition)
    {
        enemyDefinition = definition;

        if (enemyDefinition == null)
        {
            return;
        }

        projectileDefinition = enemyDefinition.ProjectileDefinition;
        attackRange = Mathf.Max(0f, enemyDefinition.AttackRange);
        attackInterval = Mathf.Max(0.01f, enemyDefinition.AttackInterval);
        projectileCount = Mathf.Max(1, enemyDefinition.ProjectileCount);
        spreadAngle = enemyDefinition.SpreadAngle;
        chargeTime = Mathf.Max(0f, enemyDefinition.ChargeTime);
    }

    public bool TryAttack(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (!CanAttack)
        {
            return false;
        }

        isAttacking = true;
        AttackStarted?.Invoke(this);

        if (chargeTime > 0f)
        {
            isCharging = true;
            ChargeStarted?.Invoke(this);
            chargeRoutine = StartCoroutine(ChargeAndFireRoutine(target));
            return true;
        }

        FireAt(target.position);
        attackTimer = attackInterval;

        isAttacking = false;
        AttackFinished?.Invoke(this);
        return true;
    }

    public void CancelCharge()
    {
        bool hadChargeRoutine = chargeRoutine != null;
        bool wasCharging = isCharging;
        bool wasAttacking = isAttacking;

        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        isCharging = false;
        isAttacking = false;

        if (wasCharging || hadChargeRoutine)
        {
            ChargeCanceled?.Invoke(this);
        }

        if (wasAttacking)
        {
            AttackFinished?.Invoke(this);
        }
    }

    private IEnumerator ChargeAndFireRoutine(Transform target)
    {
        float timer = 0f;

        while (timer < chargeTime)
        {
            if (target == null)
            {
                FinishChargeCanceledFromRoutine();
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        isCharging = false;
        ChargeReleased?.Invoke(this);

        if (target != null)
        {
            FireAt(target.position);
        }

        attackTimer = attackInterval;
        isAttacking = false;
        chargeRoutine = null;

        AttackFinished?.Invoke(this);
    }

    private void FinishChargeCanceledFromRoutine()
    {
        bool wasCharging = isCharging;
        bool wasAttacking = isAttacking;

        isCharging = false;
        isAttacking = false;
        chargeRoutine = null;

        if (wasCharging)
        {
            ChargeCanceled?.Invoke(this);
        }

        if (wasAttacking)
        {
            AttackFinished?.Invoke(this);
        }
    }

    private void FireAt(Vector2 targetPosition)
    {
        if (projectileDefinition == null || projectileDefinition.ProjectilePrefab == null)
        {
            Debug.LogWarning("Enemy projectileDefinition 또는 projectilePrefab이 없습니다.", this);
            return;
        }

        Vector2 origin = firePoint != null ? firePoint.position : transform.position;
        Vector2 baseDirection = targetPosition - origin;

        if (baseDirection.sqrMagnitude <= 0.001f)
        {
            baseDirection = firePoint != null ? (Vector2)firePoint.up : Vector2.up;
        }

        baseDirection.Normalize();

        int count = Mathf.Max(1, projectileCount);

        if (count == 1)
        {
            SpawnProjectile(origin, baseDirection);
            ProjectileFired?.Invoke(this);
            return;
        }

        float totalSpread = Mathf.Max(0f, spreadAngle);
        float step = count > 1 ? totalSpread / (count - 1) : 0f;
        float startAngle = -totalSpread * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector2 direction = RotateVector(baseDirection, angle);
            SpawnProjectile(origin, direction);
        }

        ProjectileFired?.Invoke(this);
    }

    private void SpawnProjectile(Vector2 origin, Vector2 direction)
    {
        GameObject prefab = projectileDefinition.ProjectilePrefab;
        GameObject projectileObject;

        if (PoolManager.Instance != null)
        {
            projectileObject = PoolManager.Instance.Get(prefab, origin, Quaternion.identity);
        }
        else
        {
            projectileObject = Instantiate(prefab, origin, Quaternion.identity);
        }

        if (projectileObject == null)
        {
            return;
        }

        Bullet bullet = projectileObject.GetComponent<Bullet>();

        if (bullet == null)
        {
            Debug.LogWarning("적 탄환 프리팹에 Bullet 컴포넌트가 없습니다.", projectileObject);

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(projectileObject);
            }
            else
            {
                Destroy(projectileObject);
            }

            return;
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Enemy,
            projectileDefinition
        );
    }

    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radian);
        float sin = Mathf.Sin(radian);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }
}