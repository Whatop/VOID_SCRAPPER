using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectileOwner
{
    Player,
    Enemy
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

    private readonly HashSet<int> damagedTargets = new HashSet<int>();

    public ProjectileOwner Owner => owner;

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
        if (rb != null)
        {
            rb.linearVelocity = moveDirection * speed;
        }
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
        float harvestDamageMultiplier = 1f)
    {
        owner = projectileOwner;
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

        ApplyRotation();
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
        harvestObjectDamageMultiplier = 1f;
        damagedTargets.Clear();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateHoming()
    {
        if (!useHoming)
        {
            return;
        }

        if (homingAngle <= 0f || homingRange <= 0f)
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
        LayerMask targetLayer = owner == ProjectileOwner.Player ? enemyTargetLayer : playerTargetLayer;

        if (targetLayer.value == 0)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, homingRange, targetLayer);

        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (IsFriendlyCollider(hit))
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (IsFriendlyCollider(other))
        {
            return;
        }

        if (owner == ProjectileOwner.Player)
        {
            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

            if (enemyHealth != null)
            {
                if (enemyHealth.IsDead)
                {
                    return;
                }

                bool damaged = TryApplyDamageToTarget(enemyHealth, enemyHealth.TakeDamage);

                if (damaged)
                {
                    EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();

                    if (enemyAI != null)
                    {
                        enemyAI.NotifyDamagedByPlayer();
                    }
                }

                return;
            }
        }
        else
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                if (playerHealth.IsDead)
                {
                    return;
                }

                TryApplyDamageToTarget(playerHealth, playerHealth.TakeDamage);
                return;
            }
        }

        if (owner == ProjectileOwner.Player)
        {
            HarvestObjectHealth harvestObject = other.GetComponentInParent<HarvestObjectHealth>();

            if (harvestObject != null)
            {
                TryApplyDamageToTarget(harvestObject, value => harvestObject.TakeDamage(value * harvestObjectDamageMultiplier));
                return;
            }
        }

        MeteorObstacle meteorObstacle = other.GetComponentInParent<MeteorObstacle>();

        if (meteorObstacle != null)
        {
            ApplyDamageToMeteor(meteorObstacle);
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        Component damageableComponent = damageable as Component;

        if (damageable != null && damageableComponent != null)
        {
            TryApplyDamageToTarget(damageableComponent, damageable.TakeDamage);
        }
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

        return other.GetComponentInParent<EnemyHealth>() != null;
    }

    private void ApplyDamageToMeteor(MeteorObstacle meteorObstacle)
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
        meteorObstacle.TakeDamage(Mathf.CeilToInt(damage));

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

        GameObject effect;

        if (PoolManager.Instance != null)
        {
            effect = PoolManager.Instance.Get(impactEffectPrefab, transform.position, rotation);
        }
        else
        {
            effect = Instantiate(impactEffectPrefab, transform.position, rotation);
        }

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