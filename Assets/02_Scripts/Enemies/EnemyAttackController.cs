using System;
using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Header("Fire Point")]
    [SerializeField] private Transform firePoint;

    [Header("Fallback Primary Attack")]
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle = 0f;
    [SerializeField] private float chargeTime = 0f;

    [Header("Fallback Secondary Attack")]
    [SerializeField] private bool useSecondaryProjectile;
    [SerializeField] private ProjectileDefinition secondaryProjectileDefinition;
    [SerializeField] private int secondaryProjectileCount = 1;
    [SerializeField] private float secondarySpreadAngle = 0f;

    [Header("Charge Aim Line")]
    [SerializeField] private bool showAimLineDuringCharge = true;

    [Tooltip("켜면 차징 시작 순간의 방향으로 조준선과 발사 방향이 고정됩니다.")]
    [SerializeField] private bool lockAimDirectionOnChargeStart = true;

    [SerializeField] private LineRenderer aimLineRenderer;
    [SerializeField] private bool autoCreateAimLineRenderer = true;
    [SerializeField] private Color aimLineColor = new Color(1f, 0.05f, 0.05f, 0.85f);
    [SerializeField] private float aimLineWidth = 0.045f;
    [SerializeField] private float aimLineLength = 12f;

    [Tooltip("벽, 운석 같은 장애물 레이어만 넣으세요. Enemy 레이어를 넣으면 자기 콜라이더에 막힐 수 있습니다.")]
    [SerializeField] private LayerMask aimLineBlockLayer;

    [Header("Runtime")]
    [SerializeField] private bool isAttacking;
    [SerializeField] private bool isCharging;

    private float attackTimer;
    private Coroutine chargeRoutine;

    private Vector2 lockedChargeDirection = Vector2.up;
    private Vector2 currentChargeDirection = Vector2.up;

    public float AttackRange => attackRange;
    public bool IsAttacking => isAttacking;
    public bool IsCharging => isCharging;
    public bool CanAttack => !isAttacking && !isCharging && attackTimer <= 0f;

    public bool IsAimDirectionLocked => isCharging && lockAimDirectionOnChargeStart;
    public Vector2 LockedChargeDirection => lockedChargeDirection;

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

        EnsureAimLineRenderer();
        HideAimLine();
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

        HideAimLine();
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
        spreadAngle = Mathf.Max(0f, enemyDefinition.SpreadAngle);
        chargeTime = Mathf.Max(0f, enemyDefinition.ChargeTime);

        useSecondaryProjectile = enemyDefinition.UseSecondaryProjectile;
        secondaryProjectileDefinition = enemyDefinition.SecondaryProjectileDefinition;
        secondaryProjectileCount = Mathf.Max(1, enemyDefinition.SecondaryProjectileCount);
        secondarySpreadAngle = Mathf.Max(0f, enemyDefinition.SecondarySpreadAngle);

        if (aimLineLength <= 0f)
        {
            aimLineLength = Mathf.Max(attackRange, 1f);
        }
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

        Vector2 origin = GetFireOrigin();
        Vector2 startDirection = GetDirectionToTarget(origin, target.position);

        isAttacking = true;
        AttackStarted?.Invoke(this);

        if (chargeTime > 0f)
        {
            isCharging = true;

            lockedChargeDirection = startDirection;
            currentChargeDirection = startDirection;

            UpdateAimLine(origin, currentChargeDirection);

            ChargeStarted?.Invoke(this);
            AudioManager.PlayAt(SoundEventIds.EnemyChargerAimLoop, transform.position, 0.75f);
            chargeRoutine = StartCoroutine(ChargeAndFireRoutine(target));
            return true;
        }

        FireAttack(origin, startDirection);

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

        HideAimLine();

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

            Vector2 origin = GetFireOrigin();

            if (lockAimDirectionOnChargeStart)
            {
                currentChargeDirection = lockedChargeDirection;
            }
            else
            {
                currentChargeDirection = GetDirectionToTarget(origin, target.position);
            }

            UpdateAimLine(origin, currentChargeDirection);

            timer += Time.deltaTime;
            yield return null;
        }

        HideAimLine();

        isCharging = false;
        ChargeReleased?.Invoke(this);

        FireAttack(GetFireOrigin(), currentChargeDirection);

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

        HideAimLine();

        if (wasCharging)
        {
            ChargeCanceled?.Invoke(this);
        }

        if (wasAttacking)
        {
            AttackFinished?.Invoke(this);
        }
    }

    private void FireAttack(Vector2 origin, Vector2 direction)
    {
        bool firedAnyProjectile = false;

        firedAnyProjectile |= FireProjectilePattern(
            projectileDefinition,
            origin,
            direction,
            projectileCount,
            spreadAngle
        );

        if (useSecondaryProjectile)
        {
            firedAnyProjectile |= FireProjectilePattern(
                secondaryProjectileDefinition,
                origin,
                direction,
                secondaryProjectileCount,
                secondarySpreadAngle
            );
        }

        if (firedAnyProjectile)
        {
            AudioManager.PlayAt(ResolveFireSoundEventId(), transform.position);
            ProjectileFired?.Invoke(this);
        }
    }

    private string ResolveFireSoundEventId()
    {
        if (chargeTime > 0f)
        {
            return SoundEventIds.EnemyChargerFire;
        }

        if (useSecondaryProjectile)
        {
            return SoundEventIds.EnemyEliteSpreadFire;
        }

        if (projectileCount > 1 || spreadAngle > 0f)
        {
            return SoundEventIds.EnemyShotgunFire;
        }

        return SoundEventIds.EnemyBasicFire;
    }

    private bool FireProjectilePattern(
        ProjectileDefinition definition,
        Vector2 origin,
        Vector2 baseDirection,
        int count,
        float totalSpread)
    {
        if (definition == null || definition.ProjectilePrefab == null)
        {
            return false;
        }

        if (baseDirection.sqrMagnitude <= 0.001f)
        {
            baseDirection = firePoint != null ? (Vector2)firePoint.up : Vector2.up;
        }

        baseDirection.Normalize();

        count = Mathf.Max(1, count);
        totalSpread = Mathf.Max(0f, totalSpread);

        if (count == 1)
        {
            SpawnProjectile(definition, origin, baseDirection);
            return true;
        }

        float step = totalSpread / (count - 1);
        float startAngle = -totalSpread * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector2 direction = RotateVector(baseDirection, angle);
            SpawnProjectile(definition, origin, direction);
        }

        return true;
    }

    private void SpawnProjectile(ProjectileDefinition definition, Vector2 origin, Vector2 direction)
    {
        if (definition == null || definition.ProjectilePrefab == null)
        {
            return;
        }

        GameObject prefab = definition.ProjectilePrefab;
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
            definition
        );
    }

    private Vector2 GetFireOrigin()
    {
        return firePoint != null
            ? (Vector2)firePoint.position
            : (Vector2)transform.position;
    }

    private Vector2 GetDirectionToTarget(Vector2 origin, Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = firePoint != null ? (Vector2)firePoint.up : Vector2.up;
        }

        return direction.normalized;
    }

    private void EnsureAimLineRenderer()
    {
        if (!autoCreateAimLineRenderer)
        {
            ConfigureAimLineRenderer();
            return;
        }

        if (aimLineRenderer == null)
        {
            GameObject lineObject = new GameObject("ChargeAimLine");
            lineObject.transform.SetParent(transform, false);

            aimLineRenderer = lineObject.AddComponent<LineRenderer>();
            aimLineRenderer.useWorldSpace = true;
            aimLineRenderer.positionCount = 2;
        }

        ConfigureAimLineRenderer();
    }

    private void ConfigureAimLineRenderer()
    {
        if (aimLineRenderer == null)
        {
            return;
        }

        aimLineRenderer.useWorldSpace = true;
        aimLineRenderer.positionCount = 2;
        aimLineRenderer.startWidth = Mathf.Max(0.001f, aimLineWidth);
        aimLineRenderer.endWidth = Mathf.Max(0.001f, aimLineWidth);
        aimLineRenderer.startColor = aimLineColor;
        aimLineRenderer.endColor = aimLineColor;

        if (aimLineRenderer.material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                aimLineRenderer.material = new Material(shader);
            }
        }

        aimLineRenderer.enabled = false;
    }

    private void UpdateAimLine(Vector2 origin, Vector2 direction)
    {
        if (!showAimLineDuringCharge)
        {
            HideAimLine();
            return;
        }

        if (aimLineRenderer == null)
        {
            EnsureAimLineRenderer();
        }

        if (aimLineRenderer == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float lineLength = aimLineLength > 0f
            ? aimLineLength
            : Mathf.Max(attackRange, 1f);

        Vector2 end = origin + direction * lineLength;

        if (aimLineBlockLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, lineLength, aimLineBlockLayer);

            if (hit.collider != null)
            {
                end = hit.point;
            }
        }

        aimLineRenderer.SetPosition(0, origin);
        aimLineRenderer.SetPosition(1, end);
        aimLineRenderer.enabled = true;
    }

    private void HideAimLine()
    {
        if (aimLineRenderer != null)
        {
            aimLineRenderer.enabled = false;
        }
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