using System;
using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Header("Fire Point")]
    [Tooltip("일반 적과 터렛의 기본/중앙 발사 위치입니다.")]
    [SerializeField] private Transform firePoint;

    [Header("Runtime Turret Fire Points")]
    [Tooltip("BaseTurretController가 런타임에 연결합니다. 기관총 점사에서 좌/우 포인트를 번갈아 사용합니다.")]
    [SerializeField] private Transform runtimeCenterFirePoint;
    [SerializeField] private Transform runtimeMachineGunLeftFirePoint;
    [SerializeField] private Transform runtimeMachineGunRightFirePoint;

    [Header("Projectile Allegiance")]
    [SerializeField] private ProjectileOwner projectileOwner = ProjectileOwner.Enemy;
    [Tooltip("중립 상점 포탑처럼 적을 공격하지만 상점 보안 드론/포탑에는 피해를 주지 않아야 할 때 사용합니다.")]
    [SerializeField] private bool ignoreShopSecurityTargets;

    [Header("Fallback Primary Attack")]
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle;
    [SerializeField] private float chargeTime;

    [Header("Fallback Secondary Attack")]
    [SerializeField] private bool useSecondaryProjectile;
    [SerializeField] private ProjectileDefinition secondaryProjectileDefinition;
    [SerializeField] private int secondaryProjectileCount = 1;
    [SerializeField] private float secondarySpreadAngle;

    [Header("Fallback Special Pattern")]
    [SerializeField] private EnemyRangedAttackPattern rangedAttackPattern = EnemyRangedAttackPattern.Standard;

    [Header("Fallback Machine Gun Burst")]
    [Min(1)]
    [SerializeField] private int burstShotCount = 10;
    [Min(0.01f)]
    [SerializeField] private float burstShotInterval = 0.1f;
    [Range(0f, 45f)]
    [SerializeField] private float burstHalfAngle = 10f;
    [SerializeField] private bool burstTrackTargetEachShot;

    [Header("Fallback Shaking Shotgun")]
    [Min(0f)]
    [SerializeField] private float shakeLateralSpeed = 1.35f;
    [Min(0f)]
    [SerializeField] private float shakeFrequency = 4.5f;

    [Header("Fallback Staggered Shotgun")]
    [Min(1)]
    [SerializeField] private int staggeredVolleyCount = 2;
    [Min(0.01f)]
    [SerializeField] private float staggeredVolleyInterval = 0.25f;
    [Range(0f, 45f)]
    [SerializeField] private float staggeredVolleyAngleOffset = 9f;
    [SerializeField] private bool staggeredTrackTargetEachVolley = true;

    [Header("Fallback Charging Split")]
    [SerializeField] private ProjectileDefinition splitProjectileDefinition;
    [Min(1)]
    [SerializeField] private int splitProjectileCount = 6;
    [Min(0.05f)]
    [SerializeField] private float splitDelay = 0.95f;
    [SerializeField] private float splitAngleOffset;
    [SerializeField] private bool removeParentProjectileOnSplit = true;

    [Header("Fallback Predictive Aim")]
    [Range(0f, 1f)]
    [SerializeField] private float predictiveShotChance;
    [Min(0f)]
    [SerializeField] private float predictiveMinimumTargetSpeed = 0.5f;
    [Min(0f)]
    [SerializeField] private float predictiveMaxLeadTime = 0.65f;
    [Min(0f)]
    [SerializeField] private float predictiveVelocityMultiplier = 1f;

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
    private Coroutine attackRoutine;

    private Vector2 lockedChargeDirection = Vector2.up;
    private Vector2 currentChargeDirection = Vector2.up;
    private bool usePredictiveAimForCurrentAttack;
    private Rigidbody2D currentTargetBody;

    public float AttackRange => attackRange;
    public bool IsAttacking => isAttacking;
    public bool IsCharging => isCharging;
    public bool CanAttack => !isAttacking && !isCharging && attackTimer <= 0f;
    public bool IsAimDirectionLocked => isCharging && lockAimDirectionOnChargeStart;
    public Vector2 LockedChargeDirection => lockedChargeDirection;
    public Transform FirePoint => ResolveCenterFirePoint();
    public EnemyRangedAttackPattern RangedAttackPattern => rangedAttackPattern;

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

        rangedAttackPattern = enemyDefinition.RangedAttackPattern;
        burstShotCount = enemyDefinition.BurstShotCount;
        burstShotInterval = enemyDefinition.BurstShotInterval;
        burstHalfAngle = enemyDefinition.BurstHalfAngle;
        burstTrackTargetEachShot = enemyDefinition.BurstTrackTargetEachShot;

        shakeLateralSpeed = enemyDefinition.ShakeLateralSpeed;
        shakeFrequency = enemyDefinition.ShakeFrequency;

        staggeredVolleyCount = enemyDefinition.StaggeredVolleyCount;
        staggeredVolleyInterval = enemyDefinition.StaggeredVolleyInterval;
        staggeredVolleyAngleOffset = enemyDefinition.StaggeredVolleyAngleOffset;
        staggeredTrackTargetEachVolley = enemyDefinition.StaggeredTrackTargetEachVolley;

        splitProjectileDefinition = enemyDefinition.SplitProjectileDefinition;
        splitProjectileCount = enemyDefinition.SplitProjectileCount;
        splitDelay = enemyDefinition.SplitDelay;
        splitAngleOffset = enemyDefinition.SplitAngleOffset;
        removeParentProjectileOnSplit = enemyDefinition.RemoveParentProjectileOnSplit;

        predictiveShotChance = enemyDefinition.PredictiveShotChance;
        predictiveMinimumTargetSpeed = enemyDefinition.PredictiveMinimumTargetSpeed;
        predictiveMaxLeadTime = enemyDefinition.PredictiveMaxLeadTime;
        predictiveVelocityMultiplier = enemyDefinition.PredictiveVelocityMultiplier;

        if (aimLineLength <= 0f)
        {
            aimLineLength = Mathf.Max(attackRange, 1f);
        }
    }

    public void SetProjectileOwner(
        ProjectileOwner owner,
        bool ignoreShopSecurity = false)
    {
        projectileOwner = owner;
        ignoreShopSecurityTargets = ignoreShopSecurity;
    }


    /// <summary>
    /// 터렛 전용 발사 위치를 연결합니다.
    /// 기관총 점사는 Left/Right를 번갈아 사용하고, 나머지 공격은 Center를 사용합니다.
    /// </summary>
    public void ConfigureFirePoints(
        Transform center,
        Transform machineGunLeft,
        Transform machineGunRight)
    {
        runtimeCenterFirePoint = center;
        runtimeMachineGunLeftFirePoint = machineGunLeft;
        runtimeMachineGunRightFirePoint = machineGunRight;
    }

    public bool TryAttack(Transform target)
    {
        if (target == null || !CanAttack)
        {
            return false;
        }

        Vector2 origin = GetFireOrigin();
        currentTargetBody = target.GetComponentInParent<Rigidbody2D>();
        usePredictiveAimForCurrentAttack = ShouldUsePredictiveAim(currentTargetBody);
        Vector2 startDirection = GetAimDirection(origin, target, usePredictiveAimForCurrentAttack);

        isAttacking = true;
        AttackStarted?.Invoke(this);

        if (rangedAttackPattern == EnemyRangedAttackPattern.MachineGunBurst)
        {
            attackRoutine = StartCoroutine(BurstFireRoutine(target, startDirection));
            return true;
        }

        if (rangedAttackPattern == EnemyRangedAttackPattern.StaggeredShotgun)
        {
            attackRoutine = StartCoroutine(StaggeredShotgunRoutine(target, startDirection));
            return true;
        }

        if (chargeTime > 0f || rangedAttackPattern == EnemyRangedAttackPattern.ChargingSplit)
        {
            isCharging = true;
            lockedChargeDirection = startDirection;
            currentChargeDirection = startDirection;

            UpdateAimLine(origin, currentChargeDirection);
            ChargeStarted?.Invoke(this);
            AudioManager.PlayAt(SoundEventIds.EnemyChargerAimLoop, transform.position, 0.75f);

            attackRoutine = StartCoroutine(ChargeAndFireRoutine(target));
            return true;
        }

        if (rangedAttackPattern == EnemyRangedAttackPattern.ShakingShotgun)
        {
            FireShakingShotgun(origin, startDirection);
        }
        else
        {
            FireStandardAttack(origin, startDirection);
        }

        FinishAttackWithCooldown();
        return true;
    }

    /// <summary>
    /// 기존 호출부 호환용 이름입니다. 차징뿐 아니라 점사 코루틴도 함께 취소합니다.
    /// </summary>
    public void CancelCharge()
    {
        bool hadRoutine = attackRoutine != null;
        bool wasCharging = isCharging;
        bool wasAttacking = isAttacking;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isCharging = false;
        isAttacking = false;
        usePredictiveAimForCurrentAttack = false;
        currentTargetBody = null;

        HideAimLine();

        if (wasCharging || (hadRoutine && wasCharging))
        {
            ChargeCanceled?.Invoke(this);
        }

        if (wasAttacking)
        {
            AttackFinished?.Invoke(this);
        }
    }

    private IEnumerator BurstFireRoutine(Transform target, Vector2 initialDirection)
    {
        int shotCount = Mathf.Max(1, burstShotCount);
        float shotInterval = Mathf.Max(0.01f, burstShotInterval);
        Vector2 lockedDirection = initialDirection.sqrMagnitude > 0.001f
            ? initialDirection.normalized
            : Vector2.up;

        for (int i = 0; i < shotCount; i++)
        {
            if (target == null)
            {
                break;
            }

            Vector2 origin = GetBurstFireOrigin(i);
            Vector2 aimDirection = lockedDirection;

            if (burstTrackTargetEachShot)
            {
                aimDirection = GetAimDirection(
                    origin,
                    target,
                    usePredictiveAimForCurrentAttack
                );
            }

            float angle = UnityEngine.Random.Range(-burstHalfAngle, burstHalfAngle);
            Vector2 shotDirection = RotateVector(aimDirection, angle);
            Bullet bullet = SpawnProjectile(projectileDefinition, origin, shotDirection);

            if (bullet != null)
            {
                AudioManager.PlayAt(SoundEventIds.EnemyBasicFire, transform.position, 0.75f);
                ProjectileFired?.Invoke(this);
            }

            if (i < shotCount - 1)
            {
                yield return new WaitForSeconds(shotInterval);
            }
        }

        FinishAttackWithCooldown();
        attackRoutine = null;
    }

    private IEnumerator StaggeredShotgunRoutine(Transform target, Vector2 initialDirection)
    {
        int volleyCount = Mathf.Max(1, staggeredVolleyCount);
        float volleyInterval = Mathf.Max(0.01f, staggeredVolleyInterval);
        Vector2 lockedDirection = initialDirection.sqrMagnitude > 0.001f
            ? initialDirection.normalized
            : Vector2.up;

        for (int volleyIndex = 0; volleyIndex < volleyCount; volleyIndex++)
        {
            if (target == null)
            {
                break;
            }

            Vector2 origin = GetFireOrigin();
            Vector2 aimDirection = lockedDirection;

            if (staggeredTrackTargetEachVolley)
            {
                aimDirection = GetAimDirection(
                    origin,
                    target,
                    usePredictiveAimForCurrentAttack
                );
            }

            float signedOffset = volleyCount <= 1
                ? 0f
                : (volleyIndex % 2 == 0 ? -staggeredVolleyAngleOffset : staggeredVolleyAngleOffset);

            bool firedAny = FireProjectilePattern(
                projectileDefinition,
                origin,
                aimDirection,
                projectileCount,
                spreadAngle,
                signedOffset
            );

            NotifyAttackFired(firedAny, SoundEventIds.EnemyShotgunFire);

            if (volleyIndex < volleyCount - 1)
            {
                yield return new WaitForSeconds(volleyInterval);
            }
        }

        FinishAttackWithCooldown();
        attackRoutine = null;
    }

    private IEnumerator ChargeAndFireRoutine(Transform target)
    {
        float duration = Mathf.Max(0.05f, chargeTime);
        float timer = 0f;

        while (timer < duration)
        {
            if (target == null)
            {
                FinishChargeCanceledFromRoutine();
                yield break;
            }

            Vector2 origin = GetFireOrigin();

            currentChargeDirection = lockAimDirectionOnChargeStart
                ? lockedChargeDirection
                : GetAimDirection(origin, target, usePredictiveAimForCurrentAttack);

            UpdateAimLine(origin, currentChargeDirection);

            timer += Time.deltaTime;
            yield return null;
        }

        HideAimLine();
        isCharging = false;
        ChargeReleased?.Invoke(this);

        if (rangedAttackPattern == EnemyRangedAttackPattern.ChargingSplit)
        {
            FireChargingSplit(GetFireOrigin(), currentChargeDirection);
        }
        else
        {
            FireStandardAttack(GetFireOrigin(), currentChargeDirection);
        }

        FinishAttackWithCooldown();
        attackRoutine = null;
    }

    private void FinishChargeCanceledFromRoutine()
    {
        bool wasCharging = isCharging;
        bool wasAttacking = isAttacking;

        isCharging = false;
        isAttacking = false;
        attackRoutine = null;
        usePredictiveAimForCurrentAttack = false;
        currentTargetBody = null;

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

    private void FinishAttackWithCooldown()
    {
        attackTimer = Mathf.Max(0.01f, attackInterval);
        isAttacking = false;
        isCharging = false;
        usePredictiveAimForCurrentAttack = false;
        currentTargetBody = null;
        AttackFinished?.Invoke(this);
    }

    private void FireStandardAttack(Vector2 origin, Vector2 direction)
    {
        bool firedAnyProjectile = FireProjectilePattern(
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

        NotifyAttackFired(firedAnyProjectile, ResolveFireSoundEventId());
    }

    private void FireShakingShotgun(Vector2 origin, Vector2 direction)
    {
        if (projectileDefinition == null || projectileDefinition.ProjectilePrefab == null)
        {
            return;
        }

        int count = Mathf.Max(1, projectileCount);
        float totalSpread = Mathf.Max(0f, spreadAngle);
        float step = count <= 1 ? 0f : totalSpread / (count - 1);
        float startAngle = -totalSpread * 0.5f;
        bool firedAny = false;

        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1 ? 0f : startAngle + step * i;
            Vector2 shotDirection = RotateVector(direction, angle);
            Bullet bullet = SpawnProjectile(projectileDefinition, origin, shotDirection);

            if (bullet == null)
            {
                continue;
            }

            float phase = count <= 1
                ? UnityEngine.Random.Range(0f, Mathf.PI * 2f)
                : (i / (float)count) * Mathf.PI * 2f;

            bullet.ConfigureSineWave(
                Mathf.Max(0f, shakeLateralSpeed),
                Mathf.Max(0f, shakeFrequency),
                phase
            );

            firedAny = true;
        }

        NotifyAttackFired(firedAny, SoundEventIds.EnemyShotgunFire);
    }

    private void FireChargingSplit(Vector2 origin, Vector2 direction)
    {
        Bullet bullet = SpawnProjectile(projectileDefinition, origin, direction);

        if (bullet == null)
        {
            return;
        }

        ProjectileDefinition splitDefinition = splitProjectileDefinition != null
            ? splitProjectileDefinition
            : secondaryProjectileDefinition;

        if (splitDefinition == null || splitDefinition.ProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"[{name}] 차징 엘리트 분해탄 Definition 또는 Projectile Prefab이 비어 있습니다.",
                this
            );
        }
        else if (!bullet.ConfigureRadialSplit(
                     ResolveSafeSplitDelay(projectileDefinition),
                     splitDefinition,
                     Mathf.Max(1, splitProjectileCount),
                     splitAngleOffset,
                     removeParentProjectileOnSplit))
        {
            Debug.LogWarning($"[{name}] 차징탄 분해 설정에 실패했습니다.", this);
        }

        NotifyAttackFired(true, SoundEventIds.EnemyChargerFire);
    }

    private float ResolveSafeSplitDelay(ProjectileDefinition carrierDefinition)
    {
        float requestedDelay = Mathf.Max(0.05f, splitDelay);

        if (carrierDefinition == null)
        {
            return requestedDelay;
        }

        float availableTime = Mathf.Max(0.05f, carrierDefinition.LifeTime);

        if (carrierDefinition.Speed > 0.01f && carrierDefinition.Range > 0f)
        {
            availableTime = Mathf.Min(
                availableTime,
                carrierDefinition.Range / carrierDefinition.Speed
            );
        }

        float latestSafeTime = Mathf.Max(0.05f, availableTime - 0.08f);
        return Mathf.Min(requestedDelay, latestSafeTime);
    }

    private void NotifyAttackFired(bool firedAnyProjectile, string soundEventId)
    {
        if (!firedAnyProjectile)
        {
            return;
        }

        AudioManager.PlayAt(soundEventId, transform.position);
        ProjectileFired?.Invoke(this);
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
        float totalSpread,
        float centerAngleOffset = 0f)
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
            Vector2 singleDirection = RotateVector(baseDirection, centerAngleOffset);
            return SpawnProjectile(definition, origin, singleDirection) != null;
        }

        float step = totalSpread / (count - 1);
        float startAngle = centerAngleOffset - totalSpread * 0.5f;
        bool spawnedAny = false;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector2 direction = RotateVector(baseDirection, angle);
            spawnedAny |= SpawnProjectile(definition, origin, direction) != null;
        }

        return spawnedAny;
    }

    private Bullet SpawnProjectile(
        ProjectileDefinition definition,
        Vector2 origin,
        Vector2 direction)
    {
        if (definition == null || definition.ProjectilePrefab == null)
        {
            return null;
        }

        GameObject prefab = definition.ProjectilePrefab;
        GameObject projectileObject = PoolManager.Instance != null
            ? PoolManager.Instance.Get(prefab, origin, Quaternion.identity)
            : Instantiate(prefab, origin, Quaternion.identity);

        if (projectileObject == null)
        {
            return null;
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

            return null;
        }

        bullet.Initialize(
            direction,
            projectileOwner,
            definition,
            -1f,
            -1f,
            -1f,
            -1,
            0f,
            0f,
            1f,
            ignoreShopSecurityTargets
        );

        return bullet;
    }

    private Vector2 GetFireOrigin()
    {
        Transform point = ResolveCenterFirePoint();
        return point != null
            ? (Vector2)point.position
            : (Vector2)transform.position;
    }

    private Vector2 GetBurstFireOrigin(int shotIndex)
    {
        Transform point = null;
        bool hasLeft = runtimeMachineGunLeftFirePoint != null;
        bool hasRight = runtimeMachineGunRightFirePoint != null;

        if (hasLeft && hasRight)
        {
            point = shotIndex % 2 == 0
                ? runtimeMachineGunLeftFirePoint
                : runtimeMachineGunRightFirePoint;
        }
        else if (hasLeft)
        {
            point = runtimeMachineGunLeftFirePoint;
        }
        else if (hasRight)
        {
            point = runtimeMachineGunRightFirePoint;
        }

        if (point == null)
        {
            point = ResolveCenterFirePoint();
        }

        return point != null
            ? (Vector2)point.position
            : (Vector2)transform.position;
    }

    private Transform ResolveCenterFirePoint()
    {
        if (runtimeCenterFirePoint != null)
        {
            return runtimeCenterFirePoint;
        }

        return firePoint != null ? firePoint : transform;
    }

    private bool ShouldUsePredictiveAim(Rigidbody2D targetBody)
    {
        if (targetBody == null || predictiveShotChance <= 0f)
        {
            return false;
        }

        float minimumSpeed = Mathf.Max(0f, predictiveMinimumTargetSpeed);
        if (targetBody.linearVelocity.sqrMagnitude < minimumSpeed * minimumSpeed)
        {
            return false;
        }

        ProjectileDefinition aimProjectile = projectileDefinition != null
            ? projectileDefinition
            : secondaryProjectileDefinition;

        if (aimProjectile == null || aimProjectile.Speed <= 0.01f)
        {
            return false;
        }

        return UnityEngine.Random.value < Mathf.Clamp01(predictiveShotChance);
    }

    private Vector2 GetAimDirection(
        Vector2 origin,
        Transform target,
        bool usePrediction)
    {
        if (target == null)
        {
            return firePoint != null ? (Vector2)firePoint.up : Vector2.up;
        }

        if (!usePrediction)
        {
            return GetDirectionToTarget(origin, target.position);
        }

        Rigidbody2D targetBody = currentTargetBody != null
            ? currentTargetBody
            : target.GetComponentInParent<Rigidbody2D>();

        if (targetBody == null)
        {
            return GetDirectionToTarget(origin, target.position);
        }

        ProjectileDefinition aimProjectile = projectileDefinition != null
            ? projectileDefinition
            : secondaryProjectileDefinition;

        float projectileSpeed = aimProjectile != null ? aimProjectile.Speed : 0f;
        if (projectileSpeed <= 0.01f)
        {
            return GetDirectionToTarget(origin, target.position);
        }

        Vector2 targetPosition = target.position;
        Vector2 targetVelocity = targetBody.linearVelocity * Mathf.Max(0f, predictiveVelocityMultiplier);
        Vector2 relativePosition = targetPosition - origin;

        float leadTime = SolveInterceptTime(relativePosition, targetVelocity, projectileSpeed);

        if (leadTime <= 0f)
        {
            leadTime = relativePosition.magnitude / projectileSpeed;
        }

        leadTime = Mathf.Clamp(leadTime, 0f, Mathf.Max(0f, predictiveMaxLeadTime));
        Vector2 predictedPosition = targetPosition + targetVelocity * leadTime;
        return GetDirectionToTarget(origin, predictedPosition);
    }

    private static float SolveInterceptTime(
        Vector2 relativePosition,
        Vector2 targetVelocity,
        float projectileSpeed)
    {
        float speedSquared = projectileSpeed * projectileSpeed;
        float a = Vector2.Dot(targetVelocity, targetVelocity) - speedSquared;
        float b = 2f * Vector2.Dot(relativePosition, targetVelocity);
        float c = Vector2.Dot(relativePosition, relativePosition);

        const float epsilon = 0.0001f;

        if (Mathf.Abs(a) < epsilon)
        {
            if (Mathf.Abs(b) < epsilon)
            {
                return 0f;
            }

            float linearTime = -c / b;
            return linearTime > 0f ? linearTime : 0f;
        }

        float discriminant = (b * b) - (4f * a * c);
        if (discriminant < 0f)
        {
            return 0f;
        }

        float sqrt = Mathf.Sqrt(discriminant);
        float denominator = 2f * a;
        float timeA = (-b - sqrt) / denominator;
        float timeB = (-b + sqrt) / denominator;

        bool validA = timeA > 0f;
        bool validB = timeB > 0f;

        if (validA && validB)
        {
            return Mathf.Min(timeA, timeB);
        }

        if (validA)
        {
            return timeA;
        }

        return validB ? timeB : 0f;
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

        if (aimLineRenderer.sharedMaterial == null && Application.isPlaying)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                aimLineRenderer.sharedMaterial = new Material(shader);
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

    private static Vector2 RotateVector(Vector2 vector, float angle)
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
