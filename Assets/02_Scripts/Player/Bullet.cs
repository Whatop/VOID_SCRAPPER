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

public interface IPlayerOwnedAlly
{
    bool IsPlayerOwnedAlly { get; }
}

public interface IPlayerProjectileHitListener
{
    void HandlePlayerProjectileHit(EnemyHealth enemyHealth, EnemyBaseAI enemyAI);
}

public interface IBulletWorldCollisionHandler
{
    bool TryHandleWorldCollision(Bullet bullet, Collider2D other);
}

public readonly struct BulletReflectionSnapshot
{
    public BulletReflectionSnapshot(
        Vector2 travelDirection,
        float damage,
        float speed,
        Transform firingSource)
    {
        TravelDirection = travelDirection;
        Damage = damage;
        Speed = speed;
        FiringSource = firingSource;
    }

    public Vector2 TravelDirection { get; }
    public float Damage { get; }
    public float Speed { get; }
    public Transform FiringSource { get; }
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
    [Min(0.02f)]
    [SerializeField] private float homingTargetRefreshInterval = 0.08f;

    [Header("Terminal Guidance Presentation")]
    [SerializeField] private Color terminalGuidanceProjectileColor = new Color(0.55f, 1f, 0.4f, 1f);

    [Header("Close-Quarters Overpressure Presentation")]
    [SerializeField] private Transform projectileVisualRoot;
    [SerializeField] private Color overpressureProjectileColor = new Color(1f, 0.82f, 0.28f, 1f);
    [Range(1f, 2f)]
    [SerializeField] private float overpressureMaximumVisualScale = 1.4f;
    [Min(1f)]
    [SerializeField] private float overpressureImpactScale = 1.4f;
    [Min(1f)]
    [SerializeField] private float pointBlankImpactScale = 1.7f;

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
    private Collider2D[] projectileColliders;
    private bool[] authoredColliderStates;
    private SpriteRenderer projectileRenderer;
    private TrailRenderer projectileTrail;
    private IBulletWorldCollisionHandler worldCollisionHandler;
    private Color defaultProjectileColor = Color.white;
    private Vector3 defaultProjectileRootScale = Vector3.one;
    private Vector3 defaultProjectileVisualScale = Vector3.one;

    private Vector2 moveDirection;
    private Vector2 spawnPosition;

    private float speed;
    private float lifeTime;
    private float lifeTimer;
    private float range;
    private float damage;
    private float harvestObjectDamageMultiplier = 1f;

    private bool usePlayerEnemyCloseRangeDamage;
    private float closeRangeMaxBonusMultiplier;
    private float closeRangeFullBonusDistance;
    private float closeRangeFalloffEndDistance;

    private int remainingPierceCount;
    private float pierceDamageRetention = 1f;

    private bool useHoming;
    private float homingAngle;
    private float homingRange;
    private Transform homingTarget;
    private float homingTargetRefreshTimer;
    private bool allowHomingReacquisition = true;
    private bool useTimedHoming;
    private float homingTimeRemaining;
    private bool useTerminalGuidance;
    private float terminalGuidanceRetentionRangeMultiplier = 1f;
    private float terminalGuidanceCloseSteeringDistance;
    private readonly Collider2D[] homingTargetBuffer = new Collider2D[64];

    private ProjectileOwner owner;
    private Transform sourceRoot;
    private IPlayerProjectileHitListener playerProjectileHitListener;
    private bool ignoreShopSecurityTargets;
    private bool destroyLargeMeteorOnHit;
    private bool destroySmallMeteorOnHit;
    private bool destroySupplyContainerOnHit;
    private bool destroyHighValueWreckOnHit;
    private bool destroyDestroyedHullOnHit;
    private bool reflectionClaimed;

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
    public Transform SourceRoot => sourceRoot;

    public static int ReleaseAllActiveOwnedBy(ProjectileOwner projectileOwner)
    {
        Bullet[] activeBullets = FindObjectsByType<Bullet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        int releasedCount = 0;

        for (int i = 0; i < activeBullets.Length; i++)
        {
            Bullet bullet = activeBullets[i];
            if (bullet == null ||
                !bullet.gameObject.activeInHierarchy ||
                bullet.owner != projectileOwner)
            {
                continue;
            }

            bullet.ReleaseSelf(false);
            releasedCount++;
        }

        return releasedCount;
    }

    public static int ReleaseAllActiveFromSource(Transform projectileSource)
    {
        if (projectileSource == null)
        {
            return 0;
        }

        Bullet[] activeBullets = FindObjectsByType<Bullet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        int releasedCount = 0;

        for (int i = 0; i < activeBullets.Length; i++)
        {
            Bullet bullet = activeBullets[i];
            if (bullet == null ||
                !bullet.gameObject.activeInHierarchy ||
                bullet.sourceRoot != projectileSource)
            {
                continue;
            }

            bullet.ReleaseSelf(false);
            releasedCount++;
        }

        return releasedCount;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileColliders = GetComponentsInChildren<Collider2D>(true);
        authoredColliderStates = new bool[projectileColliders.Length];

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            authoredColliderStates[i] = projectileColliders[i] != null &&
                                        projectileColliders[i].enabled;
        }

        projectileRenderer = GetComponentInChildren<SpriteRenderer>(true);
        projectileTrail = GetComponentInChildren<TrailRenderer>(true);
        worldCollisionHandler = GetComponent<IBulletWorldCollisionHandler>();
        defaultProjectileRootScale = transform.localScale;

        if (projectileRenderer != null)
        {
            defaultProjectileColor = projectileRenderer.color;

            if (projectileVisualRoot == null && projectileRenderer.transform != transform)
            {
                projectileVisualRoot = projectileRenderer.transform;
            }
        }

        if (projectileVisualRoot != null)
        {
            defaultProjectileVisualScale = projectileVisualRoot.localScale;
        }
    }

    private void OnEnable()
    {
        ResetRuntimeState();
        SetProjectileCollisionActive(false);

        projectileTrail?.Clear();
    }

    private void OnDisable()
    {
        damagedTargets.Clear();
        homingTarget = null;
        RestoreProjectilePresentation();

        projectileTrail?.Clear();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (owner == ProjectileOwner.Enemy &&
            RunManager.Instance != null &&
            RunManager.Instance.IsCompletingRun)
        {
            ReleaseSelf(false);
            return;
        }

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
        UpdateCloseRangeVisual();
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
        bool ignoreShopSecurity = false,
        GameObject projectileSource = null,
        float pierceDamageRetentionOverride = -1f)
    {
        owner = projectileOwner;
        reflectionClaimed = false;
        sourceRoot = projectileSource != null ? projectileSource.transform : null;
        playerProjectileHitListener = sourceRoot != null
            ? sourceRoot.GetComponent<IPlayerProjectileHitListener>()
            : null;
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
            pierceDamageRetention = projectileDefinition.PierceDamageRetention;

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
            pierceDamageRetention = 1f;

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

        if (pierceDamageRetentionOverride >= 0f)
        {
            pierceDamageRetention = Mathf.Clamp01(pierceDamageRetentionOverride);
        }

        homingAngle = Mathf.Max(0f, homingAngle + homingAngleBonus);
        homingRange = Mathf.Max(0f, homingRange + homingRangeBonus);
        harvestObjectDamageMultiplier = Mathf.Max(0.05f, harvestDamageMultiplier);

        lifeTimer = Mathf.Max(0.05f, lifeTime);
        damagedTargets.Clear();
        homingTarget = null;
        homingTargetRefreshTimer = 0f;
        allowHomingReacquisition = true;

        ClearSpecialMotion();
        ApplyRotation();
        SetProjectileCollisionActive(true);
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

    public bool TryClaimReflection(out BulletReflectionSnapshot snapshot)
    {
        snapshot = default;

        if (owner != ProjectileOwner.Enemy ||
            reflectionClaimed ||
            useRadialSplit ||
            !gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 currentVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        Vector2 travelDirection = currentVelocity.sqrMagnitude > 0.001f
            ? currentVelocity.normalized
            : moveDirection;

        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float currentSpeed = currentVelocity.sqrMagnitude > 0.001f
            ? currentVelocity.magnitude
            : speed;

        reflectionClaimed = true;
        snapshot = new BulletReflectionSnapshot(
            travelDirection.normalized,
            Mathf.Max(0f, damage),
            Mathf.Max(0f, currentSpeed),
            sourceRoot
        );
        return true;
    }

    public void CancelReflectionClaim()
    {
        if (owner == ProjectileOwner.Enemy && gameObject.activeInHierarchy)
        {
            reflectionClaimed = false;
        }
    }

    public void ConfigureMeteorImpact(bool destroyLargeMeteor)
    {
        destroyLargeMeteorOnHit = destroyLargeMeteor;
    }

    public void ConfigurePlayerEnemyCloseRangeDamage(
        float maxBonusPercent,
        float fullBonusDistance,
        float falloffEndDistance)
    {
        closeRangeMaxBonusMultiplier = Mathf.Max(0f, maxBonusPercent) * 0.01f;
        closeRangeFullBonusDistance = Mathf.Max(0f, fullBonusDistance);
        closeRangeFalloffEndDistance = Mathf.Max(
            closeRangeFullBonusDistance,
            falloffEndDistance
        );
        usePlayerEnemyCloseRangeDamage =
            owner == ProjectileOwner.Player &&
            closeRangeMaxBonusMultiplier > 0f &&
            closeRangeFalloffEndDistance > 0f;

        UpdateCloseRangeVisual();
    }

    public void ConfigureTerminalGuidance(
        float turnRateBonus,
        float acquisitionRangeBonus,
        float retentionRangeMultiplier,
        float closeSteeringDistance)
    {
        if (owner != ProjectileOwner.Player || !useHoming)
        {
            return;
        }

        useTerminalGuidance = true;
        homingAngle = Mathf.Max(0f, homingAngle + Mathf.Max(0f, turnRateBonus));
        homingRange = Mathf.Max(0f, homingRange + Mathf.Max(0f, acquisitionRangeBonus));
        terminalGuidanceRetentionRangeMultiplier = Mathf.Max(1f, retentionRangeMultiplier);
        terminalGuidanceCloseSteeringDistance = Mathf.Max(0f, closeSteeringDistance);
        homingTarget = null;
        homingTargetRefreshTimer = 0f;

        if (projectileRenderer != null)
        {
            projectileRenderer.color = terminalGuidanceProjectileColor;
        }
    }

    public void ConfigureStandaloneHoming(
        Transform initialTarget,
        float turnRateDegreesPerSecond,
        float acquisitionRange,
        bool allowReacquisition)
    {
        if (owner != ProjectileOwner.Player)
        {
            return;
        }

        useHoming = turnRateDegreesPerSecond > 0f && acquisitionRange > 0f;
        homingAngle = Mathf.Max(0f, turnRateDegreesPerSecond);
        homingRange = Mathf.Max(0f, acquisitionRange);
        homingTarget = initialTarget;
        homingTargetRefreshTimer = 0f;
        allowHomingReacquisition = allowReacquisition;
        useTimedHoming = false;
        homingTimeRemaining = 0f;
        useTerminalGuidance = false;
        terminalGuidanceRetentionRangeMultiplier = 1f;
        terminalGuidanceCloseSteeringDistance = 0f;
    }

    public void ConfigureTimedHoming(
        Transform initialTarget,
        float turnRateDegreesPerSecond,
        float acquisitionRange,
        bool allowReacquisition,
        float duration)
    {
        useHoming = initialTarget != null &&
                    turnRateDegreesPerSecond > 0f &&
                    acquisitionRange > 0f &&
                    duration > 0f;
        homingAngle = Mathf.Max(0f, turnRateDegreesPerSecond);
        homingRange = Mathf.Max(0f, acquisitionRange);
        homingTarget = initialTarget;
        homingTargetRefreshTimer = 0f;
        allowHomingReacquisition = allowReacquisition;
        useTimedHoming = useHoming;
        homingTimeRemaining = Mathf.Max(0f, duration);
        useTerminalGuidance = false;
        terminalGuidanceRetentionRangeMultiplier = 1f;
        terminalGuidanceCloseSteeringDistance = 0f;
    }

    public bool TrySetTravelDirection(Vector2 direction)
    {
        if (!gameObject.activeInHierarchy || direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        moveDirection = direction.normalized;

        if (rb != null)
        {
            rb.linearVelocity = moveDirection * speed;
        }

        ApplyRotation();
        return true;
    }

    public void ConfigureProjectileColor(Color color)
    {
        if (projectileRenderer != null)
        {
            projectileRenderer.color = color;
        }
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
        pierceDamageRetention = 1f;

        useHoming = false;
        homingAngle = 0f;
        homingRange = 0f;
        homingTarget = null;
        homingTargetRefreshTimer = 0f;
        allowHomingReacquisition = true;
        useTimedHoming = false;
        homingTimeRemaining = 0f;
        useTerminalGuidance = false;
        terminalGuidanceRetentionRangeMultiplier = 1f;
        terminalGuidanceCloseSteeringDistance = 0f;

        RestoreProjectilePresentation();

        owner = ProjectileOwner.Player;
        sourceRoot = null;
        playerProjectileHitListener = null;
        ignoreShopSecurityTargets = false;
        destroyLargeMeteorOnHit = false;
        destroySmallMeteorOnHit = false;
        destroySupplyContainerOnHit = false;
        destroyHighValueWreckOnHit = false;
        destroyDestroyedHullOnHit = false;
        reflectionClaimed = false;
        harvestObjectDamageMultiplier = 1f;
        usePlayerEnemyCloseRangeDamage = false;
        closeRangeMaxBonusMultiplier = 0f;
        closeRangeFullBonusDistance = 0f;
        closeRangeFalloffEndDistance = 0f;

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

    private void SetProjectileCollisionActive(bool active)
    {
        if (projectileColliders == null || authoredColliderStates == null)
        {
            return;
        }

        int count = Mathf.Min(projectileColliders.Length, authoredColliderStates.Length);

        for (int i = 0; i < count; i++)
        {
            Collider2D projectileCollider = projectileColliders[i];

            if (projectileCollider != null)
            {
                projectileCollider.enabled = active && authoredColliderStates[i];
            }
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
                ignoreShopSecurityTargets,
                sourceRoot != null ? sourceRoot.gameObject : null
            );
        }

        if (releaseParentOnSplit)
        {
            ReleaseSelf(false);
        }
    }

    private void UpdateHoming()
    {
        if (useTimedHoming)
        {
            homingTimeRemaining -= Time.deltaTime;
            if (homingTimeRemaining <= 0f)
            {
                useTimedHoming = false;
                useHoming = false;
                homingTarget = null;
                return;
            }
        }

        if (!useHoming || homingAngle <= 0f || homingRange <= 0f)
        {
            return;
        }

        if (!IsHomingTargetValid(homingTarget))
        {
            homingTarget = null;

            if (!allowHomingReacquisition)
            {
                return;
            }

            homingTargetRefreshTimer -= Time.deltaTime;

            if (homingTargetRefreshTimer <= 0f)
            {
                homingTarget = FindNearestHomingTarget();
                homingTargetRefreshTimer = Mathf.Max(0.02f, homingTargetRefreshInterval);
            }
        }

        Transform target = homingTarget;

        if (target == null)
        {
            return;
        }

        Vector2 targetPosition = ResolveHomingTargetPosition(target);
        Vector2 targetOffset = targetPosition - (Vector2)transform.position;
        if (targetOffset.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector2 targetDirection = targetOffset.normalized;

        if (useTerminalGuidance &&
            terminalGuidanceCloseSteeringDistance > 0f &&
            targetOffset.sqrMagnitude <=
            terminalGuidanceCloseSteeringDistance * terminalGuidanceCloseSteeringDistance)
        {
            moveDirection = targetDirection;
            return;
        }

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

        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(targetLayer);
        contactFilter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(
            transform.position,
            homingRange,
            contactFilter,
            homingTargetBuffer
        );

        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = homingTargetBuffer[i];

            if (hit == null || IsFriendlyCollider(hit))
            {
                continue;
            }

            FrigateBossPart frigatePart = hit.GetComponentInParent<FrigateBossPart>();
            if (frigatePart != null && !frigatePart.CanReceiveDamage)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (owner == ProjectileOwner.Player && !IsPlayerHomingTargetEligible(enemyHealth))
            {
                continue;
            }

            if ((enemyHealth != null && enemyHealth.IsDead) ||
                (ignoreShopSecurityTargets && IsShopSecurityTarget(enemyHealth)))
            {
                continue;
            }

            Transform candidate = frigatePart != null
                ? frigatePart.transform
                : enemyHealth != null
                    ? enemyHealth.transform
                    : hit.transform;
            Vector2 aimPoint = frigatePart != null
                ? frigatePart.ResolveHomingAimPoint(transform.position)
                : candidate.position;
            float sqrDistance = (aimPoint - (Vector2)transform.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private bool IsHomingTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        float retentionRange = homingRange * (useTerminalGuidance
            ? terminalGuidanceRetentionRangeMultiplier
            : 1f);

        EnemyHealth enemyHealth = target.GetComponentInParent<EnemyHealth>();

        FrigateBossPart frigatePart = target.GetComponentInParent<FrigateBossPart>();
        if (frigatePart != null && !frigatePart.CanReceiveDamage)
        {
            return false;
        }

        Vector2 targetPosition = frigatePart != null
            ? frigatePart.ResolveHomingAimPoint(transform.position)
            : target.position;
        if ((targetPosition - (Vector2)transform.position).sqrMagnitude >
            retentionRange * retentionRange)
        {
            return false;
        }

        if (enemyHealth != null)
        {
            if (owner == ProjectileOwner.Player && !IsPlayerHomingTargetEligible(enemyHealth))
            {
                return false;
            }

            if (enemyHealth.IsDead || (ignoreShopSecurityTargets && IsShopSecurityTarget(enemyHealth)))
            {
                return false;
            }

            BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();
            if (turret != null && turret.IsPlayerAllied && owner == ProjectileOwner.Player)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 ResolveHomingTargetPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        FrigateBossPart frigatePart = target.GetComponent<FrigateBossPart>();
        if (frigatePart == null)
        {
            frigatePart = target.GetComponentInParent<FrigateBossPart>();
        }

        return frigatePart != null
            ? frigatePart.ResolveHomingAimPoint(transform.position)
            : target.position;
    }

    private bool IsPlayerHomingTargetEligible(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null || enemyHealth.IsDead)
        {
            return false;
        }

        BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();

        if (turret != null)
        {
            if (turret.IsPlayerAllied)
            {
                return false;
            }

            if (turret.IsShopDefense && turret.ShopOwner != null && !turret.ShopOwner.IsHostile)
            {
                return false;
            }
        }

        EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();

        if (enemyAI != null &&
            enemyAI.IsShopSecurityUnit &&
            !ShopRunBridge.IsShopHostileThisRun())
        {
            return false;
        }

        return true;
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
        if (owner == ProjectileOwner.Enemy &&
            RunManager.Instance != null &&
            RunManager.Instance.IsCompletingRun)
        {
            ReleaseSelf(false);
            return;
        }

        if (other == null || !gameObject.activeInHierarchy || IsFriendlyCollider(other))
        {
            return;
        }

        if (worldCollisionHandler != null &&
            worldCollisionHandler.TryHandleWorldCollision(this, other))
        {
            return;
        }

        Vector2 hitPoint = ResolveHitPoint(other);
        impactPosition = hitPoint;

        if (owner == ProjectileOwner.Player || owner == ProjectileOwner.ShopDefense)
        {
            FrigateBossPart frigatePart = other.GetComponentInParent<FrigateBossPart>();

            if (frigatePart != null)
            {
                if (!frigatePart.CanReceiveDamage)
                {
                    return;
                }

                float overpressureStrength = ResolvePlayerEnemyCloseRangeStrength(hitPoint);
                float closeRangeDamageMultiplier = 1f +
                                                   closeRangeMaxBonusMultiplier *
                                                   overpressureStrength;
                float impactScaleMultiplier = ResolveOverpressureImpactScale(
                    overpressureStrength
                );

                bool damaged = TryApplyDamageToFrigatePart(
                    frigatePart,
                    hitPoint,
                    closeRangeDamageMultiplier,
                    impactScaleMultiplier
                );

                if (damaged && owner == ProjectileOwner.Player)
                {
                    PlayOverpressureImpactFeedback(hitPoint, overpressureStrength);
                    playerProjectileHitListener?.HandlePlayerProjectileHit(
                        frigatePart.AggregateHealth,
                        null
                    );
                }

                return;
            }

            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

            if (enemyHealth != null)
            {
                if (enemyHealth.IsDead ||
                    (ignoreShopSecurityTargets && IsShopSecurityTarget(enemyHealth)))
                {
                    return;
                }

                float overpressureStrength = ResolvePlayerEnemyCloseRangeStrength(hitPoint);
                float closeRangeDamageMultiplier = 1f +
                                                   closeRangeMaxBonusMultiplier *
                                                   overpressureStrength;
                float impactScaleMultiplier = ResolveOverpressureImpactScale(
                    overpressureStrength
                );

                bool damaged = TryApplyDamageToTarget(
                    enemyHealth,
                    value => enemyHealth.TakeDamage(value, hitPoint, moveDirection),
                    closeRangeDamageMultiplier,
                    impactScaleMultiplier
                );

                if (damaged && owner == ProjectileOwner.Player)
                {
                    PlayOverpressureImpactFeedback(hitPoint, overpressureStrength);
                    EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
                    enemyAI?.NotifyDamagedByPlayer();
                    playerProjectileHitListener?.HandlePlayerProjectileHit(enemyHealth, enemyAI);
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
            BossArenaCover arenaCover = meteorObstacle.GetComponentInParent<BossArenaCover>();
            if (arenaCover != null && arenaCover.IsProtected)
            {
                arenaCover.PlayBlockedImpact(hitPoint);
                ReleaseSelf(true);
                return;
            }

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

        IProjectileDamageReceiver projectileDamageReceiver =
            other.GetComponentInParent<IProjectileDamageReceiver>();
        Component projectileDamageReceiverComponent =
            projectileDamageReceiver as Component;

        if (projectileDamageReceiver != null &&
            projectileDamageReceiverComponent != null)
        {
            int receiverId = projectileDamageReceiverComponent.GetInstanceID();
            if (!damagedTargets.Contains(receiverId))
            {
                damagedTargets.Add(receiverId);
                ProjectileDamageContext context = new ProjectileDamageContext(
                    owner,
                    sourceRoot,
                    damage,
                    hitPoint,
                    GetInstanceID());
                projectileDamageReceiver.TryReceiveProjectileDamage(in context);
            }

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

        if (IsSourceCollider(other))
        {
            return true;
        }

        if (owner == ProjectileOwner.Enemy &&
            other.GetComponentInParent<PlayerPeriodicReflector2D>() != null)
        {
            // The outer reflector trigger owns this contact. If reflection fails,
            // the projectile continues inward and the real Player hurtbox remains authoritative.
            return true;
        }

        if (owner == ProjectileOwner.Player)
        {
            if (other.GetComponentInParent<PlayerHealth>() != null)
            {
                return true;
            }

            IPlayerOwnedAlly playerOwnedAlly = other.GetComponentInParent<IPlayerOwnedAlly>();
            return playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly;
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

    private bool IsSourceCollider(Collider2D other)
    {
        if (sourceRoot == null || other == null)
        {
            return false;
        }

        Transform otherTransform = other.transform;
        return otherTransform == sourceRoot ||
               otherTransform.IsChildOf(sourceRoot);
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
            damage *= pierceDamageRetention;
            return;
        }

        ReleaseSelf(false);
    }

    private float ResolvePlayerEnemyCloseRangeStrength(Vector2 hitPoint)
    {
        if (!usePlayerEnemyCloseRangeDamage || owner != ProjectileOwner.Player)
        {
            return 0f;
        }

        float traveledDistance = Vector2.Distance(spawnPosition, hitPoint);

        return ResolvePlayerEnemyCloseRangeStrength(traveledDistance);
    }

    private float ResolvePlayerEnemyCloseRangeStrength(float traveledDistance)
    {
        if (!usePlayerEnemyCloseRangeDamage || owner != ProjectileOwner.Player)
        {
            return 0f;
        }

        if (traveledDistance <= closeRangeFullBonusDistance)
        {
            return 1f;
        }

        if (traveledDistance >= closeRangeFalloffEndDistance)
        {
            return 0f;
        }

        float falloffDistance = closeRangeFalloffEndDistance - closeRangeFullBonusDistance;

        if (falloffDistance <= 0.001f)
        {
            return 0f;
        }

        float normalizedFalloff = Mathf.Clamp01(
            (traveledDistance - closeRangeFullBonusDistance) / falloffDistance
        );
        return 1f - normalizedFalloff;
    }

    private void UpdateCloseRangeVisual()
    {
        if (!usePlayerEnemyCloseRangeDamage || projectileRenderer == null)
        {
            return;
        }

        float traveledDistance = Vector2.Distance(spawnPosition, transform.position);
        float strength = ResolvePlayerEnemyCloseRangeStrength(traveledDistance);
        projectileRenderer.color = Color.Lerp(
            defaultProjectileColor,
            overpressureProjectileColor,
            strength
        );

        if (projectileVisualRoot == null || projectileVisualRoot == transform)
        {
            return;
        }

        float visualScale = Mathf.Lerp(
            1f,
            Mathf.Max(1f, overpressureMaximumVisualScale),
            strength
        );
        projectileVisualRoot.localScale = defaultProjectileVisualScale * visualScale;
    }

    private void RestoreProjectilePresentation()
    {
        transform.localScale = defaultProjectileRootScale;

        if (projectileRenderer != null)
        {
            projectileRenderer.color = defaultProjectileColor;
        }

        if (projectileVisualRoot != null)
        {
            projectileVisualRoot.localScale = defaultProjectileVisualScale;
        }
    }

    private float ResolveOverpressureImpactScale(float strength)
    {
        if (strength <= 0.001f)
        {
            return 1f;
        }

        if (strength >= 0.85f)
        {
            return Mathf.Max(1f, pointBlankImpactScale);
        }

        return Mathf.Lerp(1f, Mathf.Max(1f, overpressureImpactScale), strength);
    }

    private void PlayOverpressureImpactFeedback(Vector2 hitPoint, float strength)
    {
        if (strength < 0.85f)
        {
            return;
        }

        CombatFeedbackManager.PlayHit(
            hitPoint,
            moveDirection,
            CombatFeedbackKind.Enemy,
            Mathf.Lerp(0.7f, 1f, strength),
            0f,
            0f,
            true
        );
    }

    private bool TryApplyDamageToTarget(
        Component targetComponent,
        Action<float> damageAction,
        float damageMultiplier = 1f,
        float impactScaleMultiplier = 1f)
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
        damageAction.Invoke(damage * Mathf.Max(0f, damageMultiplier));
        SpawnImpactEffect(impactScaleMultiplier);

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            damage *= pierceDamageRetention;
            return true;
        }

        ReleaseSelf(false);
        return true;
    }

    private bool TryApplyDamageToFrigatePart(
        FrigateBossPart part,
        Vector2 hitPoint,
        float damageMultiplier,
        float impactScaleMultiplier)
    {
        if (part == null || !part.CanReceiveDamage)
        {
            return false;
        }

        int targetId = part.GetInstanceID();
        if (damagedTargets.Contains(targetId))
        {
            return false;
        }

        float resolvedDamage = damage * Mathf.Max(0f, damageMultiplier);
        if (!part.TryTakeDamage(resolvedDamage, hitPoint, moveDirection))
        {
            return false;
        }

        damagedTargets.Add(targetId);
        SpawnImpactEffect(impactScaleMultiplier);

        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;
            damage *= pierceDamageRetention;
            return true;
        }

        ReleaseSelf(false);
        return true;
    }

    private void SpawnImpactEffect(float scaleMultiplier = 1f)
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

        effect.transform.localScale = runtimeImpactEffectPrefab.transform.localScale * Mathf.Max(0.01f, scaleMultiplier);

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
