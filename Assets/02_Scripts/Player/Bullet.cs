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

    private Rigidbody2D rb;

    private Vector2 moveDirection;
    private Vector2 spawnPosition;

    private float speed;
    private float lifeTime;
    private float lifeTimer;
    private float range;
    private float damage;

    private int remainingPierceCount;

    private bool useHoming;
    private float homingAngle;
    private float homingRange;

    private ProjectileOwner owner;

    private readonly HashSet<int> damagedTargets = new HashSet<int>();

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
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            ReleaseSelf();
            return;
        }

        if (range > 0f)
        {
            float traveledDistance = Vector2.Distance(spawnPosition, transform.position);
            if (traveledDistance >= range)
            {
                ReleaseSelf();
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
        float homingRangeBonus = 0f)
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

        homingAngle += homingAngleBonus;
        homingRange += homingRangeBonus;

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

        Vector2 targetDirection = (target.position - transform.position).normalized;
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

        // 운석은 모든 탄환을 막는다.
        MeteorObstacle meteorObstacle = other.GetComponentInParent<MeteorObstacle>();
        if (meteorObstacle != null)
        {
            meteorObstacle.TakeDamage(Mathf.CeilToInt(damage));
            ReleaseSelf();
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            if (owner == ProjectileOwner.Player && damageable is PlayerHealth)
            {
                return;
            }

            if (owner == ProjectileOwner.Enemy && damageable is EnemyHealth)
            {
                return;
            }

            Component targetComponent = damageable as Component;
            ApplyDamageToTarget(targetComponent, damageable.TakeDamage);
            return;
        }

        // 이전 구조 호환용. EnemyHealth가 아직 IDamageable로 교체되지 않은 경우 대비.
        if (owner == ProjectileOwner.Player)
        {
            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                ApplyDamageToTarget(enemyHealth, enemyHealth.TakeDamage);
            }

            return;
        }

        if (owner == ProjectileOwner.Enemy)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                ApplyDamageToTarget(playerHealth, playerHealth.TakeDamage);
            }
        }
    }

    private void ApplyDamageToTarget(Component targetComponent, Action<float> damageAction)
    {
        if (targetComponent == null || damageAction == null)
        {
            return;
        }

        int targetId = targetComponent.GetInstanceID();
        if (damagedTargets.Contains(targetId))
        {
            return;
        }

        damagedTargets.Add(targetId);
        damageAction.Invoke(damage);

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            return;
        }

        ReleaseSelf();
    }

    private void ReleaseSelf()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
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