using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectileOwner
{
    Player = 0,
    Enemy = 1,

    // 중립 상점 포탑 전용. 플레이어와 상점 보안 유닛에는 피해를 주지 않는다.
    ShopDefense = 2
}

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Fallback Projectile Settings")]
    [SerializeField] private float fallbackSpeed = 12f;
    [SerializeField] private float fallbackLifeTime = 2f;
    [SerializeField] private float fallbackRange = 8f;
    [SerializeField] private float fallbackDamage = 1f;
    [SerializeField] private int fallbackPierceCount;

    [Header("Homing")]
    [SerializeField] private LayerMask enemyTargetLayer;
    [SerializeField] private LayerMask playerTargetLayer;

    [Header("Rotation")]
    [SerializeField] private float rotationOffset = -90f;

    [Header("World Collision")]
    [Tooltip("알려진 피해 대상이 아니더라도 Trigger가 아닌 Collider2D에 닿으면 탄환을 제거합니다. 기지 벽 관통 방지용입니다.")]
    [SerializeField] private bool blockOnUnhandledSolidCollider = true;

    [Header("Impact VFX")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private float impactEffectLifeTime = 0.18f;
    [SerializeField] private bool rotateImpactEffectToBullet = true;

    private Rigidbody2D rb;

    private Vector2 moveDirection;
    private Vector2 spawnPosition;

    private float speed;
    private float lifeTime;
    private float lifeTimer;
    private float range;
    private float damage;
    private float harvestObjectDamageMultiplier = 1f;

    private int remainingPierceCount;

    private bool useHoming;
    private float homingAngle;
    private float homingRange;

    private ProjectileOwner owner;
    private bool ignoreShopSecurityTargets;

    private bool useSineWave;
    private float sineWaveLateralSpeed;
    private float sineWaveFrequency;
    private float sineWavePhase;
    private float sineWaveElapsed;

    private bool useRadialSplit;
    private float radialSplitTimer;
    private ProjectileDefinition radialSplitDefinition;
    private int radialSplitCount;
    private float radialSplitAngleOffset;
    private bool releaseParentOnSplit;

    private readonly HashSet<int> damagedTargets = new HashSet<int>();

    public ProjectileOwner Owner => owner;
    public Vector2 MoveDirection => moveDirection;
    public float Speed => speed;
    public bool HasPendingRadialSplit => useRadialSplit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        damagedTargets.Clear();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        sineWaveElapsed += Time.deltaTime;

        if (useRadialSplit)
        {
            radialSplitTimer -= Time.deltaTime;

            if (radialSplitTimer <= 0f)
            {
                ExecuteRadialSplit();

                if (!gameObject.activeInHierarchy)
                {
                    return;
                }
            }
        }

        if (lifeTimer <= 0f)
        {
            ReleaseSelf(false);
            return;
        }

        if (range > 0f)
        {
            float traveledDistance = Vector2.Distance(spawnPosition, transform.position);

            if (traveledDistance >= range)
            {
                ReleaseSelf(false);
                return;
            }
        }

        UpdateHoming();
        ApplyRotation();
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        Vector2 velocity = moveDirection * speed;

        if (useSineWave && moveDirection.sqrMagnitude > 0.001f)
        {
            Vector2 perpendicular = new Vector2(-moveDirection.y, moveDirection.x);
            float wave = Mathf.Sin(
                (sineWaveElapsed * Mathf.PI * 2f * sineWaveFrequency) + sineWavePhase
            );

            velocity += perpendicular * wave * sineWaveLateralSpeed;
        }

        rb.linearVelocity = velocity;
    }

    public void Initialize(Vector2 direction, ProjectileOwner projectileOwner)
    {
        Initialize(
            direction,
            projectileOwner,
            null,
            -1f,
            -1f,
            -1f,
            -1,
            0f,
            0f
        );
    }

    public void Initialize(
        Vector2 direction,
        ProjectileOwner projectileOwner,
        ProjectileDefinition projectileDefinition,
        float damageOverride = -1f,
        float speedOverride = -1f,
        float rangeOverride = -1f,
        int pierceOverride = -1,
        float homingAngleBonus = 0f,
        float homingRangeBonus = 0f,
        float harvestDamageMultiplier = 1f,
        bool ignoreShopSecurity = false)
    {
        owner = projectileOwner;
        ignoreShopSecurityTargets = ignoreShopSecurity;
        moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        spawnPosition = transform.position;

        if (projectileDefinition != null)
        {
            damage = projectileDefinition.Damage;
            speed = projectileDefinition.Speed;
            range = projectileDefinition.Range;
            lifeTime = projectileDefinition.LifeTime;
            remainingPierceCount = projectileDefinition.PierceCount;

            useHoming = projectileDefinition.UseHoming;
            homingAngle = projectileDefinition.HomingAngle;
            homingRange = projectileDefinition.HomingRange;
        }
        else
        {
            damage = fallbackDamage;
            speed = fallbackSpeed;
            range = fallbackRange;
            lifeTime = fallbackLifeTime;
            remainingPierceCount = fallbackPierceCount;

            useHoming = false;
            homingAngle = 0f;
            homingRange = 0f;
        }

        if (damageOverride >= 0f)
        {
            damage = damageOverride;
        }

        if (speedOverride >= 0f)
        {
            speed = speedOverride;
        }

        if (rangeOverride >= 0f)
        {
            range = rangeOverride;
        }

        if (pierceOverride >= 0)
        {
            remainingPierceCount = pierceOverride;
        }

        homingAngle = Mathf.Max(0f, homingAngle + homingAngleBonus);
        homingRange = Mathf.Max(0f, homingRange + homingRangeBonus);
        harvestObjectDamageMultiplier = Mathf.Max(0.05f, harvestDamageMultiplier);

        lifeTimer = Mathf.Max(0.05f, lifeTime);
        damagedTargets.Clear();

        ClearSpecialMotion();
        ApplyRotation();
    }

    public void ConfigureSineWave(
        float lateralSpeed,
        float frequency,
        float phaseRadians = 0f)
    {
        sineWaveLateralSpeed = Mathf.Max(0f, lateralSpeed);
        sineWaveFrequency = Mathf.Max(0f, frequency);
        sineWavePhase = phaseRadians;
        sineWaveElapsed = 0f;
        useSineWave = sineWaveLateralSpeed > 0f && sineWaveFrequency > 0f;
    }

    public bool ConfigureRadialSplit(
        float delay,
        ProjectileDefinition splitDefinition,
        int projectileCount,
        float angleOffset,
        bool removeParentOnSplit)
    {
        if (splitDefinition == null || splitDefinition.ProjectilePrefab == null)
        {
            useRadialSplit = false;
            return false;
        }

        radialSplitTimer = Mathf.Max(0.05f, delay);
        radialSplitDefinition = splitDefinition;
        radialSplitCount = Mathf.Max(1, projectileCount);
        radialSplitAngleOffset = angleOffset;
        releaseParentOnSplit = removeParentOnSplit;
        useRadialSplit = true;
        return true;
    }

    public void ForceRelease(bool spawnImpactEffect = false)
    {
        ReleaseSelf(spawnImpactEffect);
    }

    private void ResetRuntimeState()
    {
        moveDirection = Vector2.zero;
        spawnPosition = transform.position;

        speed = fallbackSpeed;
        lifeTime = fallbackLifeTime;
        lifeTimer = fallbackLifeTime;
        range = fallbackRange;
        damage = fallbackDamage;
        remainingPierceCount = fallbackPierceCount;

        useHoming = false;
        homingAngle = 0f;
        homingRange = 0f;

        owner = ProjectileOwner.Player;
        ignoreShopSecurityTargets = false;
        harvestObjectDamageMultiplier = 1f;
        damagedTargets.Clear();
        ClearSpecialMotion();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void ClearSpecialMotion()
    {
        useSineWave = false;
        sineWaveLateralSpeed = 0f;
        sineWaveFrequency = 0f;
        sineWavePhase = 0f;
        sineWaveElapsed = 0f;

        useRadialSplit = false;
        radialSplitTimer = 0f;
        radialSplitDefinition = null;
        radialSplitCount = 0;
        radialSplitAngleOffset = 0f;
        releaseParentOnSplit = false;
    }

    private void ExecuteRadialSplit()
    {
        if (!useRadialSplit || radialSplitDefinition == null)
        {
            return;
        }

        useRadialSplit = false;
        int count = Mathf.Max(1, radialSplitCount);
        Vector2 origin = transform.position;
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = radialSplitAngleOffset + step * i;
            float radian = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));

            GameObject projectileObject = PoolManager.Instance != null
                ? PoolManager.Instance.Get(
                    radialSplitDefinition.ProjectilePrefab,
                    origin,
                    Quaternion.identity
                )
                : Instantiate(
                    radialSplitDefinition.ProjectilePrefab,
                    origin,
                    Quaternion.identity
                );

            if (projectileObject == null)
            {
                continue;
            }

            Bullet splitBullet = projectileObject.GetComponent<Bullet>();

            if (splitBullet == null)
            {
                Debug.LogWarning(
                    $"분해탄 프리팹 루트에 Bullet 컴포넌트가 없습니다: {radialSplitDefinition.ProjectilePrefab.name}",
                    projectileObject
                );

                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(projectileObject);
                }
                else
                {
                    Destroy(projectileObject);
                }

                continue;
            }

            splitBullet.Initialize(
                direction,
                owner,
                radialSplitDefinition,
                -1f,
                -1f,
                -1f,
                -1,
                0f,
                0f,
                1f,
                ignoreShopSecurityTargets
            );
        }

        if (releaseParentOnSplit)
        {
            ReleaseSelf(false);
        }
    }

    private void UpdateHoming()
    {
        if (!useHoming || homingAngle <= 0f || homingRange <= 0f)
        {
            return;
        }

        Transform target = FindNearestHomingTarget();

        if (target == null)
        {
            return;
        }

        Vector2 targetDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
        float maxRadiansDelta = homingAngle * Mathf.Deg2Rad * Time.deltaTime;

        Vector3 newDirection = Vector3.RotateTowards(
            moveDirection,
            targetDirection,
            maxRadiansDelta,
            0f
        );

        moveDirection = ((Vector2)newDirection).normalized;
    }

    private Transform FindNearestHomingTarget()
    {
        LayerMask targetLayer = owner == ProjectileOwner.Enemy
            ? playerTargetLayer
            : enemyTargetLayer;

        if (targetLayer.value == 0)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, homingRange, targetLayer);

        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || IsFriendlyCollider(hit))
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (ignoreShopSecurityTargets && IsShopSecurityTarget(enemyHealth))
            {
                continue;
            }

            float sqrDistance = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    private void ApplyRotation()
    {
        if (moveDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        OnTriggerEnter2D(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !gameObject.activeInHierarchy || IsFriendlyCollider(other))
        {
            return;
        }

        Vector2 hitPoint = ResolveHitPoint(other);

        if (owner == ProjectileOwner.Player || owner == ProjectileOwner.ShopDefense)
        {
            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

            if (enemyHealth != null)
            {
                if (enemyHealth.IsDead ||
                    (ignoreShopSecurityTargets && IsShopSecurityTarget(enemyHealth)))
                {
                    return;
                }

                bool damaged = TryApplyDamageToTarget(
                    enemyHealth,
                    value => enemyHealth.TakeDamage(value, hitPoint, moveDirection)
                );

                if (damaged && owner == ProjectileOwner.Player)
                {
                    EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
                    enemyAI?.NotifyDamagedByPlayer();
                }

                return;
            }
        }
        else if (owner == ProjectileOwner.Enemy)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                if (!playerHealth.IsDead)
                {
                    TryApplyDamageToTarget(
                        playerHealth,
                        value => playerHealth.TakeDamage(value, hitPoint, moveDirection)
                    );
                }

                return;
            }
        }

        HarvestObjectHealth harvestObject = other.GetComponentInParent<HarvestObjectHealth>();

        if (harvestObject != null)
        {
            if (ignoreShopSecurityTargets)
            {
                ReleaseSelf(true);
            }
            else if (harvestObject.CanReceiveProjectileDamage(owner))
            {
                TryApplyDamageToTarget(
                    harvestObject,
                    value => harvestObject.TakeDamage(
                        owner == ProjectileOwner.Player
                            ? value * harvestObjectDamageMultiplier
                            : value,
                        hitPoint,
                        moveDirection
                    )
                );
            }
            else if (harvestObject.BlocksProjectileWhenDamageIgnored)
            {
                ReleaseSelf(true);
            }

            return;
        }

        MeteorObstacle meteorObstacle = other.GetComponentInParent<MeteorObstacle>();

        if (meteorObstacle != null)
        {
            if (ignoreShopSecurityTargets)
            {
                ReleaseSelf(true);
            }
            else if (meteorObstacle.CanReceiveProjectileDamage(owner))
            {
                ApplyDamageToMeteor(meteorObstacle, hitPoint);
            }
            else if (meteorObstacle.BlocksProjectileWhenDamageIgnored)
            {
                ReleaseSelf(true);
            }

            return;
        }

        FieldBaseSecurityNode securityNode = other.GetComponentInParent<FieldBaseSecurityNode>();

        if (securityNode != null)
        {
            if (owner == ProjectileOwner.Player && !ignoreShopSecurityTargets)
            {
                TryApplyDamageToTarget(securityNode, securityNode.TakeDamage);
            }
            else
            {
                ReleaseSelf(true);
            }

            return;
        }

        ShopStructure shopStructure = other.GetComponentInParent<ShopStructure>();

        if (shopStructure != null)
        {
            if (owner == ProjectileOwner.Player && !ignoreShopSecurityTargets)
            {
                TryApplyDamageToTarget(
                    shopStructure,
                    value => shopStructure.TakeDamage(value, hitPoint, moveDirection)
                );
            }
            else
            {
                ReleaseSelf(true);
            }

            return;
        }

        ExpeditionEventDamageReceiver eventDamageReceiver = other.GetComponentInParent<ExpeditionEventDamageReceiver>();

        if (eventDamageReceiver != null)
        {
            if (owner == ProjectileOwner.Player && !ignoreShopSecurityTargets)
            {
                TryApplyDamageToTarget(
                    eventDamageReceiver,
                    value => eventDamageReceiver.TakeDamage(value, hitPoint, moveDirection)
                );
            }
            else
            {
                ReleaseSelf(true);
            }

            return;
        }

        if (ignoreShopSecurityTargets)
        {
            ReleaseSelf(true);
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        Component damageableComponent = damageable as Component;

        if (damageable != null && damageableComponent != null)
        {
            TryApplyDamageToTarget(damageableComponent, damageable.TakeDamage);
            return;
        }

        if (blockOnUnhandledSolidCollider && !other.isTrigger)
        {
            ReleaseSelf(true);
        }
    }

    private static bool IsShopSecurityTarget(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return false;
        }

        if (enemyHealth.GetComponentInParent<BaseTurretController>() != null)
        {
            return true;
        }

        EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
        return enemyAI != null &&
               enemyAI.EnemyDefinition != null &&
               enemyAI.EnemyDefinition.EnemyType == EnemyType.ShopDrone;
    }

    private Vector2 ResolveHitPoint(Collider2D targetCollider)
    {
        if (targetCollider == null)
        {
            return transform.position;
        }

        Vector2 bulletPosition = transform.position;
        Vector2 closestPoint = targetCollider.ClosestPoint(bulletPosition);

        if (float.IsNaN(closestPoint.x) || float.IsInfinity(closestPoint.x) ||
            float.IsNaN(closestPoint.y) || float.IsInfinity(closestPoint.y))
        {
            return bulletPosition;
        }

        return closestPoint;
    }

    private bool IsFriendlyCollider(Collider2D other)
    {
        if (other == null)
        {
            return true;
        }

        if (owner == ProjectileOwner.Player)
        {
            return other.GetComponentInParent<PlayerHealth>() != null;
        }

        if (owner == ProjectileOwner.ShopDefense)
        {
            if (other.GetComponentInParent<PlayerHealth>() != null)
            {
                return true;
            }

            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
            return IsShopSecurityTarget(enemyHealth);
        }

        return other.GetComponentInParent<EnemyHealth>() != null;
    }

    private void ApplyDamageToMeteor(MeteorObstacle meteorObstacle, Vector2 hitPoint)
    {
        if (meteorObstacle == null)
        {
            return;
        }

        int targetId = meteorObstacle.GetInstanceID();

        if (damagedTargets.Contains(targetId))
        {
            return;
        }

        damagedTargets.Add(targetId);
        meteorObstacle.TakeDamage(Mathf.CeilToInt(damage), hitPoint, moveDirection);

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            return;
        }

        ReleaseSelf(true);
    }

    private bool TryApplyDamageToTarget(Component targetComponent, Action<float> damageAction)
    {
        if (targetComponent == null || damageAction == null)
        {
            return false;
        }

        int targetId = targetComponent.GetInstanceID();

        if (damagedTargets.Contains(targetId))
        {
            return false;
        }

        damagedTargets.Add(targetId);
        damageAction.Invoke(damage);

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            return true;
        }

        ReleaseSelf(true);
        return true;
    }

    private void SpawnImpactEffect()
    {
        if (impactEffectPrefab == null)
        {
            return;
        }

        Quaternion rotation = rotateImpactEffectToBullet
            ? transform.rotation
            : Quaternion.identity;

        GameObject effect = PoolManager.Instance != null
            ? PoolManager.Instance.Get(impactEffectPrefab, transform.position, rotation)
            : Instantiate(impactEffectPrefab, transform.position, rotation);

        if (effect == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(effect, impactEffectLifeTime);
        }
        else
        {
            Destroy(effect, impactEffectLifeTime);
        }
    }

    private void ReleaseSelf(bool spawnImpactEffect)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (spawnImpactEffect)
        {
            SpawnImpactEffect();
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
