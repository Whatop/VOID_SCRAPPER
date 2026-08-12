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

    [Header("Projectile Layer By Owner")]
    [Tooltip("풀링된 같은 탄환 프리팹을 적/상점 포탑이 함께 사용해도 충돌 레이어를 소유자에 맞춰 복구합니다.")]
    [SerializeField] private bool assignProjectileLayerByOwner = true;
    [SerializeField] private string playerProjectileLayerName = "PlayerProjectile";
    [SerializeField] private string enemyProjectileLayerName = "EnemyProjectile";
    [Tooltip("상점 방어탄 전용 레이어가 없다면 PlayerProjectile을 사용합니다. 적과 충돌하고 플레이어와는 충돌하지 않는 설정을 재사용합니다.")]
    [SerializeField] private string shopDefenseProjectileLayerName = "PlayerProjectile";
    [SerializeField] private bool applyOwnerLayerToColliderChildren = true;

    [Header("Fallback Impact VFX")]
    [Tooltip("ProjectileDefinition에 Impact VFX가 없을 때만 사용하는 기존 프리팹용 fallback입니다.")]
    [SerializeField] private GameObject impactEffectPrefab;
    [Min(0.01f)]
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
    private bool destroyLargeMeteorOnHit;
    private bool destroySmallMeteorOnHit;
    private bool destroySupplyContainerOnHit;
    private bool destroyHighValueWreckOnHit;
    private bool destroyDestroyedHullOnHit;

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

    private GameObject runtimeImpactEffectPrefab;
    private float runtimeImpactEffectLifeTime;
    private bool runtimeRotateImpactEffect;
    private Vector2 impactPosition;

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
        ApplyProjectileLayerByOwner();
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

            runtimeImpactEffectPrefab = projectileDefinition.ImpactEffectPrefab != null
                ? projectileDefinition.ImpactEffectPrefab
                : impactEffectPrefab;
            runtimeImpactEffectLifeTime = projectileDefinition.ImpactEffectPrefab != null
                ? projectileDefinition.ImpactEffectLifeTime
                : Mathf.Max(0.01f, impactEffectLifeTime);
            runtimeRotateImpactEffect = projectileDefinition.ImpactEffectPrefab != null
                ? projectileDefinition.RotateImpactEffectToProjectile
                : rotateImpactEffectToBullet;
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

            runtimeImpactEffectPrefab = impactEffectPrefab;
            runtimeImpactEffectLifeTime = Mathf.Max(0.01f, impactEffectLifeTime);
            runtimeRotateImpactEffect = rotateImpactEffectToBullet;
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

    public void ConfigureMeteorImpact(bool destroyLargeMeteor)
    {
        destroyLargeMeteorOnHit = destroyLargeMeteor;
    }

    /// <summary>
    /// 보스 탄환의 월드 오브젝트 파괴 규칙을 설정합니다.
    /// 일반 적탄과 플레이어탄에는 호출하지 않으면 기존 규칙을 그대로 사용합니다.
    /// </summary>
    public void ConfigureBossWorldImpact(
        bool destroyLargeMeteor,
        bool destroySmallMeteor,
        bool destroySupplyContainer,
        bool destroyHighValueWreck,
        bool destroyDestroyedHull)
    {
        destroyLargeMeteorOnHit = destroyLargeMeteor;
        destroySmallMeteorOnHit = destroySmallMeteor;
        destroySupplyContainerOnHit = destroySupplyContainer;
        destroyHighValueWreckOnHit = destroyHighValueWreck;
        destroyDestroyedHullOnHit = destroyDestroyedHull;
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
        destroyLargeMeteorOnHit = false;
        destroySmallMeteorOnHit = false;
        destroySupplyContainerOnHit = false;
        destroyHighValueWreckOnHit = false;
        destroyDestroyedHullOnHit = false;
        harvestObjectDamageMultiplier = 1f;

        runtimeImpactEffectPrefab = impactEffectPrefab;
        runtimeImpactEffectLifeTime = Mathf.Max(0.01f, impactEffectLifeTime);
        runtimeRotateImpactEffect = rotateImpactEffectToBullet;
        impactPosition = transform.position;

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
        impactPosition = hitPoint;

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

        FieldBaseLaserGate laserGate = other.GetComponentInParent<FieldBaseLaserGate>();

        if (laserGate != null && laserGate.ShouldBlockProjectile(owner))
        {
            ReleaseSelf(true);
            return;
        }

        if (ShouldBlockAsWorldSolid(other))
        {
            ReleaseSelf(true);
            return;
        }

        HarvestObjectHealth harvestObject = other.GetComponentInParent<HarvestObjectHealth>();

        if (harvestObject != null)
        {
            if (ignoreShopSecurityTargets)
            {
                ReleaseSelf(true);
            }
            else if (ShouldForceDestroyHarvestObject(harvestObject))
            {
                TryApplyDamageToTarget(
                    harvestObject,
                    _ => harvestObject.TakeDamage(
                        Mathf.Max(1f, harvestObject.CurrentHp + harvestObject.MaxHp),
                        hitPoint,
                        moveDirection
                    )
                );
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
            bool isLargeMeteor = IsLargeMeteor(meteorObstacle);
            bool forceDestroyMeteor =
                (isLargeMeteor && destroyLargeMeteorOnHit) ||
                (!isLargeMeteor && destroySmallMeteorOnHit);

            if (ignoreShopSecurityTargets)
            {
                ReleaseSelf(true);
            }
            else if (forceDestroyMeteor)
            {
                ApplyDamageToMeteor(meteorObstacle, hitPoint, true);
            }
            else if (isLargeMeteor && owner != ProjectileOwner.Player)
            {
                // 플레이어 탄환을 제외한 비-차징 탄환은 대형 운석에 피해를 주지 못한다.
                ReleaseSelf(true);
            }
            else if (meteorObstacle.CanReceiveProjectileDamage(owner))
            {
                ApplyDamageToMeteor(meteorObstacle, hitPoint, false);
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

    private bool ShouldForceDestroyHarvestObject(HarvestObjectHealth harvestObject)
    {
        if (harvestObject == null)
        {
            return false;
        }

        return harvestObject.ObjectKind switch
        {
            HarvestObjectKind.SupplyContainer => destroySupplyContainerOnHit,
            HarvestObjectKind.HighValueWreck => destroyHighValueWreckOnHit,
            HarvestObjectKind.DestroyedHull => destroyDestroyedHullOnHit,
            _ => false
        };
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

    private void ApplyProjectileLayerByOwner()
    {
        if (!assignProjectileLayerByOwner)
        {
            return;
        }

        string targetLayerName = owner switch
        {
            ProjectileOwner.Player => playerProjectileLayerName,
            ProjectileOwner.Enemy => enemyProjectileLayerName,
            ProjectileOwner.ShopDefense => shopDefenseProjectileLayerName,
            _ => string.Empty
        };

        int targetLayer = string.IsNullOrWhiteSpace(targetLayerName)
            ? -1
            : LayerMask.NameToLayer(targetLayerName);

        if (targetLayer < 0 && owner == ProjectileOwner.ShopDefense)
        {
            targetLayer = LayerMask.NameToLayer(playerProjectileLayerName);
        }

        if (targetLayer < 0)
        {
            return;
        }

        gameObject.layer = targetLayer;

        if (!applyOwnerLayerToColliderChildren)
        {
            return;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].gameObject.layer = targetLayer;
            }
        }
    }

    private static bool ShouldBlockAsWorldSolid(Collider2D other)
    {
        if (other == null || other.isTrigger)
        {
            return false;
        }

        int worldSolidLayer = LayerMask.NameToLayer("WorldSolid");

        if (worldSolidLayer < 0 || other.gameObject.layer != worldSolidLayer)
        {
            return false;
        }

        // 대형 운석은 WorldSolid를 사용하지만 별도 MeteorObstacle 규칙으로 처리한다.
        return other.GetComponentInParent<MeteorObstacle>() == null;
    }

    private static bool IsLargeMeteor(MeteorObstacle meteorObstacle)
    {
        if (meteorObstacle == null)
        {
            return false;
        }

        if (meteorObstacle.MotionMode == MeteorMotionMode.StaticTerrain)
        {
            return true;
        }

        int worldSolidLayer = LayerMask.NameToLayer("WorldSolid");
        return worldSolidLayer >= 0 && meteorObstacle.gameObject.layer == worldSolidLayer;
    }

    private void ApplyDamageToMeteor(
        MeteorObstacle meteorObstacle,
        Vector2 hitPoint,
        bool forceDestroy)
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

        int meteorDamage = forceDestroy
            ? int.MaxValue
            : Mathf.CeilToInt(damage);

        meteorObstacle.TakeDamage(meteorDamage, hitPoint, moveDirection);
        SpawnImpactEffect();

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            return;
        }

        ReleaseSelf(false);
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
        SpawnImpactEffect();

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            return true;
        }

        ReleaseSelf(false);
        return true;
    }

    private void SpawnImpactEffect()
    {
        if (runtimeImpactEffectPrefab == null)
        {
            return;
        }

        Quaternion rotation = runtimeRotateImpactEffect
            ? transform.rotation
            : Quaternion.identity;

        Vector3 spawnPosition = impactPosition;
        GameObject effect = PoolManager.Instance != null
            ? PoolManager.Instance.Get(runtimeImpactEffectPrefab, spawnPosition, rotation)
            : Instantiate(runtimeImpactEffectPrefab, spawnPosition, rotation);

        if (effect == null)
        {
            return;
        }

        float effectLifeTime = Mathf.Max(0.01f, runtimeImpactEffectLifeTime);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(effect, effectLifeTime);
        }
        else
        {
            Destroy(effect, effectLifeTime);
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
