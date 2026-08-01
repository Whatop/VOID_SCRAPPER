using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossPatternController : MonoBehaviour
{
    private enum BossPattern
    {
        SectionLaser,
        SpreadBarrage,
        TrackingChargeCannon,
        Phase2HexagonRotatingLaser
    }
    private enum Phase2RotatingLaserColor
    {
        Purple,
        Red
    }

    [Header("Identity")]
    [SerializeField] private string bossName = "구획 관리자";

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform player;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform arenaCenterOverride;

    [Header("Compatibility")]
    [SerializeField] private bool disableEnemyBaseAIOnAwake = true;
    [SerializeField] private bool disableEnemyAttackControllerOnAwake = true;

    [Header("Boss Health")]
    [SerializeField] private bool overrideHealthOnEnable = true;
    [SerializeField] private float maxHp = 140f;

    [Header("Deep Zone Scaling")]
    [SerializeField] private bool applyDeepZoneScaling = true;
    [SerializeField] private float deepZoneHealthMultiplier = 1.2f;
    [SerializeField] private float deepZoneDamageMultiplier = 1.2f;

    [Header("Arena")]
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(14f, 14f);

    [Range(0.2f, 1f)]
    [SerializeField] private float verticalSpaceScale = 0.7f;

    [SerializeField] private float bossMoveRadius = 3f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.85f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private float moveTargetRefreshInterval = 1.4f;
    [SerializeField] private bool stopMovementWhileCasting;

    [Header("Facing")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool rotateToPlayer = true;
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float turnSpeed = 540f;

    [Header("Projectile")]
    [SerializeField] private ProjectileDefinition projectileDefinition;

    [Header("Pattern Loop")]
    [SerializeField] private float startDelay = 0.8f;
    [SerializeField] private float patternEndDelay = 0.35f;
    [SerializeField] private float sectionLaserCooldown = 4.0f;
    [SerializeField] private float spreadBarrageCooldown = 3.0f;
    [SerializeField] private float trackingChargeCooldown = 5.0f;
    [SerializeField] private float phase2HexagonLaserCooldown = 6.0f;

    [Header("Pattern 1 - Section Laser")]
    [SerializeField] private BossLaserHazard laserHazardPrefab;
    [SerializeField] private float laserTelegraphTime = 1.2f;
    [SerializeField] private int laserLineCount = 2;
    [SerializeField] private float laserDurationPhase1 = 2.5f;
    [SerializeField] private float laserDurationPhase2 = 3.0f;
    [SerializeField] private float laserDamage = 4f;
    [SerializeField] private float laserDamageInterval = 0.65f;
    [SerializeField] private float laserWidth = 0.9f;
    [SerializeField] private float laserLengthMultiplier = 3.2f;
    [SerializeField] private float secondLaserAngleOffset = 90f;
    [SerializeField] private float laserAngleJitter = 8f;

    [Header("Laser Visual")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color laserTelegraphColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color laserActiveColor = Color.white;
    [SerializeField] private float laserTelegraphWidth = 0.12f;
    [SerializeField] private string lineSortingLayerName = "Default";
    [SerializeField] private int lineSortingOrder = 35;

    [Header("Pattern 2 - Spread Barrage")]
    [SerializeField] private int spreadVolleyCountPhase1 = 2;
    [SerializeField] private int spreadVolleyCountPhase2 = 3;
    [SerializeField] private int spreadProjectileCount = 5;
    [SerializeField] private float spreadAngle = 70f;
    [SerializeField] private float spreadVolleyInterval = 0.25f;
    [SerializeField] private float spreadProjectileDamage = 2f;
    [SerializeField] private float spreadProjectileSpeedOverride = -1f;
    [SerializeField] private float spreadProjectileRangePhase1 = 18f;
    [SerializeField] private float spreadProjectileRangePhase2 = 20f;

    [Header("Pattern 3 - Tracking Charge Cannon")]
    [SerializeField] private int chargeShotCountPhase1 = 1;
    [SerializeField] private int chargeShotCountPhase2 = 2;
    [SerializeField] private float chargeAimTime = 0.9f;
    [SerializeField] private float chargeShotInterval = 0.3f;
    [SerializeField] private float chargeProjectileDamage = 6f;
    [SerializeField] private float chargeProjectileSpeedOverride = 24f;
    [SerializeField] private float chargeProjectileRangeOverride = 22f;
    [SerializeField] private float chargeProjectileScaleMultiplier = 3f;

    [Header("Charge Visual")]
    [SerializeField] private Color chargeAimLineColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private float chargeAimLineWidth = 0.08f;
    [SerializeField] private float chargeAimLineLength = 42f;

    [Header("Phase")]
    [Range(0.01f, 0.99f)]
    [SerializeField] private float phase2HpRatio = 0.3f;
    [SerializeField] private bool logPhaseChange = true;

    [Header("Laser Manager Ships - Boss Ability")]
    [Tooltip("구획 관리자 전용 레이저 관리기체 프리팹. 기존 CoreBossIntroSequence에 연결하던 프리팹을 여기로 옮긴다.")]
    [SerializeField] private GameObject laserManagerShipPrefab;

    [Tooltip("이전 필드 호환용. laserManagerShipPrefab이 비어 있으면 이 값을 사용한다.")]
    [SerializeField] private GameObject phase2LaserManagerShipPrefab;

    [SerializeField] private bool createRuntimeManagerIfMissing = true;
    [SerializeField] private float managerShipBaseRotationZ = 45f;
    [SerializeField] private float managerShipStartExtraDistance = 7f;
    [SerializeField] private float managerShipMoveDuration = 0.9f;
    [SerializeField] private AnimationCurve managerShipMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Color phase1ManagerTint = Color.white;
    [SerializeField] private Color phase2AddedManagerTint = new Color(0.9f, 0.9f, 1f, 1f);

    [Header("Runtime Manager Placeholder")]
    [SerializeField] private Color runtimePhase1ManagerColor = new Color(0.15f, 0.8f, 1f, 1f);
    [SerializeField] private Color runtimePhase2ManagerColor = new Color(0.8f, 0.8f, 1f, 1f);
    [SerializeField] private float runtimeManagerSize = 0.45f;
    [SerializeField] private float runtimeManagerLineWidth = 0.05f;

    [Header("Boundary Laser - Phase 1 / Phase 2")]
    [SerializeField] private BossArenaLaserWall arenaLaserWallPrefab;
    [SerializeField] private float boundaryWallThickness = 0.45f;
    [SerializeField] private bool createSolidBoundaryLaserWalls = true;
    [SerializeField] private float boundaryWallDamage = 4f;
    [SerializeField] private float boundaryWallDamageInterval = 0.5f;
    [SerializeField] private Color phase1BoundaryLaserColor = Color.white;
    [SerializeField] private Color phase2BoundaryLaserColor = new Color(0.55f, 0.55f, 0.75f, 1f);
    [SerializeField] private string boundaryWallLayerName = "Default";
    [SerializeField] private int boundaryLaserSortingOrder = 30;

    [Header("Phase 2 - Top / Bottom Manager Entry")]
    [SerializeField] private float phase2ManagerStartExtraDistance = 4f;
    [SerializeField] private float phase2ManagerMoveDuration = 0.8f;
    [SerializeField] private AnimationCurve phase2ManagerMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase 2 - Hexagon Layout")]
    [Tooltip("2페이즈에서 기존 4대 관리기체를 직사각형 꼭짓점에서 육각형 어깨 위치로 재배치할 때의 X 배율입니다.")]
    [SerializeField] private float phase2HexagonSideXScale = 1f;

    [Tooltip("2페이즈 육각형의 좌상/좌하/우하/우상 관리기체 Y 위치 비율입니다. 0.5면 정육각형에 가까운 어깨 위치가 됩니다.")]
    [SerializeField] private float phase2HexagonShoulderYRatio = 0.5f;

    [Header("Phase 2 - Opposite Pair Rotating Lasers")]
    [SerializeField] private float phase2HexagonLaserWarmup = 0.35f;
    [SerializeField] private int phase2RotatingLaserRepeatCount = 3;
    [SerializeField] private float phase2RotatingLaserOneSlotAngle = 60f;
    [SerializeField] private float phase2RotatingLaserStepDuration = 1.6f;
    [SerializeField] private float phase2RotatingLaserPostStepDelay = 0.15f;
    [SerializeField] private float phase2RotatingLaserPatternEndDelay = 0.35f;
    [SerializeField] private float phase2HexagonLaserWidth = 0.8f;
    [SerializeField] private float phase2HexagonLaserDamage = 4f;
    [SerializeField] private float phase2HexagonLaserDamageInterval = 0.5f;
    [SerializeField] private Color phase2PurpleLaserColor = new Color(0.65f, 0.15f, 1f, 1f);
    [SerializeField] private Color phase2RedLaserColor = new Color(1f, 0.05f, 0.02f, 1f);
    [SerializeField] private bool alternatePhase2RotatingLaserColor = true;
    [SerializeField] private Phase2RotatingLaserColor firstPhase2RotatingLaserColor = Phase2RotatingLaserColor.Purple;
    [SerializeField] private bool phase2BoundaryLasersFollowManagers = true;
    [SerializeField] private bool usePhase2HexagonTelegraph;
    [SerializeField] private float phase2HexagonTelegraphTime = 0.2f;
    [SerializeField] private float phase2HexagonTelegraphWidth = 0.12f;
    [Header("Debug")]
    [SerializeField] private bool logPattern;

    private readonly BossPattern[] phase1PatternSequence =
    {
        BossPattern.SectionLaser,
        BossPattern.SpreadBarrage,
        BossPattern.TrackingChargeCannon
    };

    private readonly BossPattern[] phase2PatternSequence =
   {
    BossPattern.SectionLaser,
    BossPattern.SpreadBarrage,
    BossPattern.Phase2HexagonRotatingLaser
};

    private readonly List<GameObject> transientVisualObjects = new List<GameObject>();
    private readonly List<LaserGuardianDrone> phase1ManagerShips = new List<LaserGuardianDrone>(4);
    private readonly List<GameObject> boundaryLaserWalls = new List<GameObject>(6);

    private Coroutine patternRoutine;
    private Coroutine phase2ManagerEntryRoutine;

    private Vector2 arenaCenter;
    private Vector2 moveTarget;
    private Vector2 externallyConfiguredArenaCenter;
    private Vector2 externallyConfiguredArenaHalfExtents;
    private float externallyConfiguredVerticalSpaceScale = 1f;

    private float moveTargetTimer;

    private float lastSectionLaserStartTime = float.NegativeInfinity;
    private float lastSpreadBarrageStartTime = float.NegativeInfinity;
    private float lastTrackingChargeStartTime = float.NegativeInfinity;
    private float lastPhase2HexagonLaserStartTime = float.NegativeInfinity;

    private int nextPatternIndex; 
    private bool nextPhase2RotatingLaserUsePurple;
    private bool initialized;
    private bool phase2;
    private bool casting;
    private bool deathHandled;
    private bool hasExternalArenaContext;
    private bool phase1ManagersSpawned;
    private bool phase1BoundaryLasersActive;
    private bool phase2TopBottomManagersSpawned;
    private bool phase2BoundaryRebuilt;

    private LaserGuardianDrone phase2TopManagerShip;
    private LaserGuardianDrone phase2BottomManagerShip;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        firePoint = transform;
        visualRoot = transform;
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        DisableLegacyEnemyControllersIfNeeded();
    }

    private void OnEnable()
    {
        deathHandled = false;
        phase2 = false;
        casting = false;
        phase2TopBottomManagersSpawned = false;
        phase2BoundaryRebuilt = false;
        nextPatternIndex = 0;
        nextPhase2RotatingLaserUsePurple = firstPhase2RotatingLaserColor == Phase2RotatingLaserColor.Purple;
        arenaCenter = ResolveArenaCenter();
        PickNewMoveTarget();

        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleDied;

            if (overrideHealthOnEnable)
            {
                enemyHealth.SetMaxHp(GetScaledMaxHp(), true);
            }
        }

        ResolvePlayer();

        initialized = true;
        patternRoutine = StartCoroutine(PatternLoopRoutine());
    }

    private void OnDisable()
    {
        initialized = false;

        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleDied;
        }

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        if (phase2ManagerEntryRoutine != null)
        {
            StopCoroutine(phase2ManagerEntryRoutine);
            phase2ManagerEntryRoutine = null;
        }

        ClearTransientVisualObjects();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!initialized || enemyHealth == null || enemyHealth.IsDead)
        {
            return;
        }

        ResolvePlayer();
        UpdatePhase();
        UpdateMovement(Time.deltaTime);
        UpdateFacing(Time.deltaTime);
    }

    public void ConfigureBossArena(Vector2 center, Vector2 halfExtents, float verticalScale)
    {
        hasExternalArenaContext = true;
        externallyConfiguredArenaCenter = center;
        externallyConfiguredArenaHalfExtents = new Vector2(
            Mathf.Max(0.1f, halfExtents.x),
            Mathf.Max(0.1f, halfExtents.y)
        );
        externallyConfiguredVerticalSpaceScale = Mathf.Clamp(verticalScale, 0.2f, 1f);
        arenaCenter = center;
    }

    public IEnumerator PlayIntroGuardianEntryRoutine()
    {
        arenaCenter = ResolveArenaCenter();
        DestroyPhase1ManagerShips();
        DestroyBoundaryLaserWalls();
        phase1ManagerShips.Clear();
        phase1ManagersSpawned = false;
        phase1BoundaryLasersActive = false;
        phase2BoundaryRebuilt = false;

        Vector2[] finalPositions = GetPhase1ManagerFinalPositions();
        Vector3[] startPositions = new Vector3[finalPositions.Length];
        Vector3[] endPositions = new Vector3[finalPositions.Length];
        float[] rotations = GetPhase1ManagerRotations();
        string[] names =
        {
            "BossLaserManager_01_BottomLeft",
            "BossLaserManager_02_TopLeft",
            "BossLaserManager_03_TopRight",
            "BossLaserManager_04_BottomRight"
        };

        for (int i = 0; i < finalPositions.Length; i++)
        {
            Vector2 finalPosition = finalPositions[i];
            Vector2 outward = finalPosition - arenaCenter;

            if (outward.sqrMagnitude <= 0.001f)
            {
                outward = Vector2.up;
            }

            endPositions[i] = finalPosition;
            startPositions[i] = finalPosition + outward.normalized * Mathf.Max(0f, managerShipStartExtraDistance);

            LaserGuardianDrone managerShip = CreateLaserManagerShip(
                names[i],
                i + 1,
                startPositions[i],
                rotations[i],
                phase1ManagerTint,
                runtimePhase1ManagerColor
            );

            if (managerShip != null)
            {
                phase1ManagerShips.Add(managerShip);
            }
        }

        yield return MoveManagerShipsRoutine(
            phase1ManagerShips,
            startPositions,
            endPositions,
            rotations,
            managerShipMoveDuration,
            managerShipMoveCurve
        );

        phase1ManagersSpawned = phase1ManagerShips.Count >= 4;
    }

    public void ActivatePhase1BoundaryLasers()
    {
        EnsurePhase1ManagersImmediate();

        if (!phase1ManagersSpawned || phase1ManagerShips.Count < 4)
        {
            Debug.LogWarning($"{bossName}: 1페이즈 레이저 관리기체가 부족해서 봉쇄 레이저를 활성화할 수 없습니다.", this);
            return;
        }

        DestroyBoundaryLaserWalls();

        CreateBoundaryWallBetweenDrones("BossBoundaryLaser_Phase1_Left", phase1ManagerShips[0], phase1ManagerShips[1], phase1BoundaryLaserColor);
        CreateBoundaryWallBetweenDrones("BossBoundaryLaser_Phase1_Top", phase1ManagerShips[1], phase1ManagerShips[2], phase1BoundaryLaserColor);
        CreateBoundaryWallBetweenDrones("BossBoundaryLaser_Phase1_Right", phase1ManagerShips[2], phase1ManagerShips[3], phase1BoundaryLaserColor);
        CreateBoundaryWallBetweenDrones("BossBoundaryLaser_Phase1_Bottom", phase1ManagerShips[3], phase1ManagerShips[0], phase1BoundaryLaserColor);

        phase1BoundaryLasersActive = true;
        phase2BoundaryRebuilt = false;
    }

    private IEnumerator PatternLoopRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (initialized && enemyHealth != null && !enemyHealth.IsDead)
        {
            ResolvePlayer();

            if (player == null)
            {
                yield return null;
                continue;
            }

            BossPattern pattern = GetNextPattern();
            yield return WaitForPatternCooldown(pattern);

            if (enemyHealth == null || enemyHealth.IsDead)
            {
                yield break;
            }

            MarkPatternStart(pattern);

            if (logPattern)
            {
                Debug.Log($"{bossName} 패턴 시작: {pattern}", this);
            }

            casting = true;

            switch (pattern)
            {
                case BossPattern.SectionLaser:
                    yield return SectionLaserRoutine();
                    break;

                case BossPattern.SpreadBarrage:
                    yield return SpreadBarrageRoutine();
                    break;

                case BossPattern.TrackingChargeCannon:
                    yield return TrackingChargeCannonRoutine();
                    break;

                case BossPattern.Phase2HexagonRotatingLaser:
                    yield return Phase2HexagonRotatingLaserRoutine();
                    break;
            }

            casting = false;

            if (patternEndDelay > 0f)
            {
                yield return new WaitForSeconds(patternEndDelay);
            }
        }
    }

    private BossPattern GetNextPattern()
    {
        BossPattern[] sequence = phase2 ? phase2PatternSequence : phase1PatternSequence;

        if (nextPatternIndex >= sequence.Length)
        {
            nextPatternIndex = 0;
        }

        BossPattern pattern = sequence[nextPatternIndex];
        nextPatternIndex = (nextPatternIndex + 1) % sequence.Length;

        return pattern;
    }

    private IEnumerator SectionLaserRoutine()
    {
        Vector2 center = arenaCenter;
        Vector2 baseDirection = ResolveDirectionFromCenterToPlayer();

        if (baseDirection.sqrMagnitude <= 0.001f)
        {
            baseDirection = Vector2.up;
        }

        float length = Mathf.Max(1f, GetArenaDiagonalLength() * laserLengthMultiplier);
        int count = Mathf.Max(1, laserLineCount);

        Vector2[] directions = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            float angleOffset = i == 0
                ? Random.Range(-laserAngleJitter, laserAngleJitter)
                : secondLaserAngleOffset + Random.Range(-laserAngleJitter, laserAngleJitter);

            directions[i] = RotateVector(baseDirection, angleOffset).normalized;

            GameObject telegraph = CreateLineObject(
                "Boss_SectionLaser_Telegraph",
                center,
                directions[i],
                length,
                laserTelegraphWidth,
                laserTelegraphColor,
                true
            );

            if (telegraph != null)
            {
                transientVisualObjects.Add(telegraph);
            }
        }

        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, center);

        if (laserTelegraphTime > 0f)
        {
            yield return new WaitForSeconds(laserTelegraphTime);
        }

        ClearTransientVisualObjects();

        float duration = phase2 ? laserDurationPhase2 : laserDurationPhase1;
        float scaledDamage = laserDamage * GetDamageMultiplier();

        AudioManager.PlayAt(SoundEventIds.BossLaserLoop, center);

        for (int i = 0; i < count; i++)
        {
            SpawnLaserHazard(
                center,
                directions[i],
                length,
                duration,
                scaledDamage,
                laserWidth,
                laserActiveColor,
                laserDamageInterval
            );
        }
    }

    private IEnumerator SpreadBarrageRoutine()
    {
        int volleyCount = phase2 ? spreadVolleyCountPhase2 : spreadVolleyCountPhase1;
        volleyCount = Mathf.Max(1, volleyCount);

        float range = phase2 ? spreadProjectileRangePhase2 : spreadProjectileRangePhase1;

        for (int i = 0; i < volleyCount; i++)
        {
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                yield break;
            }

            ResolvePlayer();

            Vector2 origin = ResolveFirePosition();
            Vector2 baseDirection = ResolveDirectionToPlayer(origin);

            AudioManager.PlayAt(SoundEventIds.BossSpreadFire, origin);
            FireSpread(
                origin,
                baseDirection,
                spreadProjectileCount,
                spreadAngle,
                spreadProjectileDamage * GetDamageMultiplier(),
                spreadProjectileSpeedOverride,
                range,
                1f
            );

            if (i < volleyCount - 1 && spreadVolleyInterval > 0f)
            {
                yield return new WaitForSeconds(spreadVolleyInterval);
            }
        }
    }

    private IEnumerator TrackingChargeCannonRoutine()
    {
        int shotCount = phase2 ? chargeShotCountPhase2 : chargeShotCountPhase1;
        shotCount = Mathf.Max(1, shotCount);

        for (int i = 0; i < shotCount; i++)
        {
            yield return TrackingChargeCannonSingleShotRoutine();

            if (i < shotCount - 1 && chargeShotInterval > 0f)
            {
                yield return new WaitForSeconds(chargeShotInterval);
            }
        }
    }

    private IEnumerator TrackingChargeCannonSingleShotRoutine()
    {
        if (enemyHealth == null || enemyHealth.IsDead)
        {
            yield break;
        }

        Vector2 lockedTargetPosition = player != null
            ? (Vector2)player.position
            : (Vector2)transform.position + Vector2.up;

        float aimLineLength = GetEffectiveChargeAimLineLength();

        AudioManager.PlayAt(SoundEventIds.BossChargeAim, ResolveFirePosition());

        GameObject aimLineObject = CreateLineObject(
            "Boss_Charge_AimLine",
            ResolveFirePosition(),
            Vector2.up,
            aimLineLength,
            chargeAimLineWidth,
            chargeAimLineColor,
            true
        );

        LineRenderer aimLine = aimLineObject != null
            ? aimLineObject.GetComponent<LineRenderer>()
            : null;

        if (aimLineObject != null)
        {
            transientVisualObjects.Add(aimLineObject);
        }

        float timer = 0f;

        while (timer < chargeAimTime)
        {
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                ClearTransientVisualObjects();
                yield break;
            }

            ResolvePlayer();

            Vector2 origin = ResolveFirePosition();

            if (player != null)
            {
                lockedTargetPosition = player.position;
            }

            Vector2 direction = lockedTargetPosition - origin;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();

            UpdateLineObject(
                aimLineObject,
                aimLine,
                origin,
                direction,
                aimLineLength
            );

            timer += Time.deltaTime;
            yield return null;
        }

        ClearTransientVisualObjects();

        Vector2 finalOrigin = ResolveFirePosition();
        Vector2 fireDirection = lockedTargetPosition - finalOrigin;

        if (fireDirection.sqrMagnitude <= 0.001f)
        {
            fireDirection = Vector2.up;
        }

        fireDirection.Normalize();

        AudioManager.PlayAt(SoundEventIds.BossChargeFire, finalOrigin);

        SpawnProjectile(
            finalOrigin,
            fireDirection,
            chargeProjectileDamage * GetDamageMultiplier(),
            chargeProjectileSpeedOverride,
            chargeProjectileRangeOverride,
            chargeProjectileScaleMultiplier
        );
    }
    private IEnumerator Phase2HexagonRotatingLaserRoutine()
    {
        if (!phase2)
        {
            yield break;
        }

        yield return EnsurePhase2HexagonManagersRoutine();

        LaserGuardianDrone[] slots = GetHexagonSlotsCounterClockwise();

        if (!AreHexagonSlotsValid(slots))
        {
            yield break;
        }

        Phase2RotatingLaserColor selectedColor = ResolveNextPhase2RotatingLaserColor();

        Color laserColor = selectedColor == Phase2RotatingLaserColor.Purple
            ? phase2PurpleLaserColor
            : phase2RedLaserColor;

        float rotationDirection = selectedColor == Phase2RotatingLaserColor.Purple
            ? 1f
            : -1f;

        if (usePhase2HexagonTelegraph && phase2HexagonTelegraphTime > 0f)
        {
            AddOppositePairTelegraphs(slots, laserColor);
            yield return new WaitForSeconds(phase2HexagonTelegraphTime);
            ClearTransientVisualObjects();
        }

        if (phase2HexagonLaserWarmup > 0f)
        {
            yield return new WaitForSeconds(phase2HexagonLaserWarmup);
        }

        float activeLaserLifetime = GetPhase2RotatingLaserLifetimeEstimate();
        List<BossDynamicLaserBeam> activeLasers = CreateOppositePairDynamicLasers(
            slots,
            laserColor,
            activeLaserLifetime,
            selectedColor == Phase2RotatingLaserColor.Purple
                ? "Boss_Phase2_PurpleOppositeLaser"
                : "Boss_Phase2_RedOppositeLaser"
        );

        int repeatCount = Mathf.Max(1, phase2RotatingLaserRepeatCount);

        for (int i = 0; i < repeatCount; i++)
        {
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                DeactivateDynamicLasers(activeLasers);
                yield break;
            }

            yield return RotateHexagonManagerShipsOneSlotRoutine(
                slots,
                rotationDirection,
                phase2RotatingLaserStepDuration
            );

            if (phase2RotatingLaserPostStepDelay > 0f)
            {
                yield return new WaitForSeconds(phase2RotatingLaserPostStepDelay);
            }

            yield return TrackingChargeCannonSingleShotRoutine();
        }

        DeactivateDynamicLasers(activeLasers);
        RebuildBoundaryLasersAsPhase2Hexagon();

        if (phase2RotatingLaserPatternEndDelay > 0f)
        {
            yield return new WaitForSeconds(phase2RotatingLaserPatternEndDelay);
        }
    }

    private Phase2RotatingLaserColor ResolveNextPhase2RotatingLaserColor()
    {
        Phase2RotatingLaserColor selectedColor = nextPhase2RotatingLaserUsePurple
            ? Phase2RotatingLaserColor.Purple
            : Phase2RotatingLaserColor.Red;

        if (alternatePhase2RotatingLaserColor)
        {
            nextPhase2RotatingLaserUsePurple = !nextPhase2RotatingLaserUsePurple;
        }
        else
        {
            nextPhase2RotatingLaserUsePurple = firstPhase2RotatingLaserColor == Phase2RotatingLaserColor.Purple;
        }

        return selectedColor;
    }

    private void AddOppositePairTelegraphs(LaserGuardianDrone[] slots, Color laserColor)
    {
        if (!AreHexagonSlotsValid(slots))
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            AddTransientTelegraphBetween(
                $"Boss_Phase2_OppositeLaser_Telegraph_{i + 1}",
                slots[i].transform.position,
                slots[i + 3].transform.position,
                laserColor,
                phase2HexagonTelegraphWidth
            );
        }
    }

    private List<BossDynamicLaserBeam> CreateOppositePairDynamicLasers(
        LaserGuardianDrone[] slots,
        Color laserColor,
        float lifetime,
        string namePrefix)
    {
        List<BossDynamicLaserBeam> activeLasers = new List<BossDynamicLaserBeam>(3);

        if (!AreHexagonSlotsValid(slots))
        {
            return activeLasers;
        }

        for (int i = 0; i < 3; i++)
        {
            BossDynamicLaserBeam activeLaser = CreateDynamicLaserBeam($"{namePrefix}_{i + 1:D2}");

            if (activeLaser == null)
            {
                continue;
            }

            activeLaser.Initialize(
                slots[i].transform,
                slots[i + 3].transform,
                phase2HexagonLaserWidth,
                lifetime,
                phase2HexagonLaserDamage * GetDamageMultiplier(),
                phase2HexagonLaserDamageInterval,
                lineMaterial,
                laserColor,
                lineSortingLayerName,
                lineSortingOrder
            );

            activeLasers.Add(activeLaser);
        }

        return activeLasers;
    }

    private void DeactivateDynamicLasers(List<BossDynamicLaserBeam> activeLasers)
    {
        if (activeLasers == null)
        {
            return;
        }

        for (int i = activeLasers.Count - 1; i >= 0; i--)
        {
            BossDynamicLaserBeam activeLaser = activeLasers[i];

            if (activeLaser != null)
            {
                activeLaser.Deactivate();
            }
        }

        activeLasers.Clear();
    }

    private IEnumerator RotateHexagonManagerShipsOneSlotRoutine(
        LaserGuardianDrone[] slots,
        float directionSign,
        float duration)
    {
        if (!AreHexagonSlotsValid(slots))
        {
            yield break;
        }

        duration = Mathf.Max(0.05f, duration);
        directionSign = directionSign >= 0f ? 1f : -1f;

        Vector2[] startOffsets = new Vector2[slots.Length];
        float[] startRotations = new float[slots.Length];

        for (int i = 0; i < slots.Length; i++)
        {
            LaserGuardianDrone managerShip = slots[i];

            if (managerShip == null)
            {
                continue;
            }

            startOffsets[i] = (Vector2)managerShip.transform.position - arenaCenter;
            startRotations[i] = managerShip.transform.eulerAngles.z;
        }

        float targetAngle = directionSign * Mathf.Abs(phase2RotatingLaserOneSlotAngle);
        float timer = 0f;

        while (timer < duration)
        {
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                yield break;
            }

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            ApplyHexagonManagerRotation(slots, startOffsets, startRotations, targetAngle * eased);
            yield return null;
        }

        ApplyHexagonManagerRotation(slots, startOffsets, startRotations, targetAngle);
    }

    private void ApplyHexagonManagerRotation(
        LaserGuardianDrone[] slots,
        Vector2[] startOffsets,
        float[] startRotations,
        float angle)
    {
        if (slots == null || startOffsets == null || startRotations == null)
        {
            return;
        }

        int count = Mathf.Min(slots.Length, Mathf.Min(startOffsets.Length, startRotations.Length));

        for (int i = 0; i < count; i++)
        {
            LaserGuardianDrone managerShip = slots[i];

            if (managerShip == null)
            {
                continue;
            }

            Vector2 rotatedOffset = RotateVector(startOffsets[i], angle);
            managerShip.transform.position = arenaCenter + rotatedOffset;
            managerShip.ApplySlotRotation(startRotations[i] + angle);
        }
    }

    private float GetPhase2RotatingLaserLifetimeEstimate()
    {
        int repeatCount = Mathf.Max(1, phase2RotatingLaserRepeatCount);

        return phase2HexagonLaserWarmup +
               repeatCount * (
                   Mathf.Max(0.05f, phase2RotatingLaserStepDuration) +
                   Mathf.Max(0f, phase2RotatingLaserPostStepDelay) +
                   Mathf.Max(0f, chargeAimTime) +
                   Mathf.Max(0f, chargeShotInterval)
               ) +
               Mathf.Max(0f, phase2RotatingLaserPatternEndDelay) +
               2f;
    }

    private float GetEffectiveChargeAimLineLength()
    {
        return Mathf.Max(
            Mathf.Max(1f, chargeAimLineLength),
            GetArenaDiagonalLength() * 1.25f
        );
    }

    private BossDynamicLaserBeam CreateDynamicLaserBeam(string objectName)
    {
        GameObject laserObject = new GameObject(string.IsNullOrWhiteSpace(objectName)
            ? "Boss_Phase2_RotatingLaser_Active"
            : objectName);

        return laserObject.AddComponent<BossDynamicLaserBeam>();
    }
    private IEnumerator EnsurePhase2HexagonManagersRoutine()
    {
        if (phase2TopBottomManagersSpawned)
        {
            if (!phase2BoundaryRebuilt)
            {
                RebuildBoundaryLasersAsPhase2Hexagon();
            }

            yield break;
        }

        if (phase2ManagerEntryRoutine != null)
        {
            while (phase2ManagerEntryRoutine != null)
            {
                yield return null;
            }

            yield break;
        }

        yield return Phase2TopBottomManagerEntryRoutine();
    }

    private IEnumerator Phase2TopBottomManagerEntryRoutine()
    {
        EnsurePhase1ManagersImmediate();

        LaserGuardianDrone bottomLeft = phase1ManagerShips.Count > 0 ? phase1ManagerShips[0] : null;
        LaserGuardianDrone topLeft = phase1ManagerShips.Count > 1 ? phase1ManagerShips[1] : null;
        LaserGuardianDrone topRight = phase1ManagerShips.Count > 2 ? phase1ManagerShips[2] : null;
        LaserGuardianDrone bottomRight = phase1ManagerShips.Count > 3 ? phase1ManagerShips[3] : null;

        Vector2[] finalPositions = GetPhase2HexagonFinalPositionsCounterClockwise();

        if (phase2TopManagerShip == null)
        {
            phase2TopManagerShip = CreateLaserManagerShip(
                "BossLaserManager_05_Top",
                5,
                GetSpawnFromOutside(finalPositions[0], phase2ManagerStartExtraDistance),
                GetManagerRotationForPosition(finalPositions[0]),
                phase2AddedManagerTint,
                runtimePhase2ManagerColor
            );
        }

        if (phase2BottomManagerShip == null)
        {
            phase2BottomManagerShip = CreateLaserManagerShip(
                "BossLaserManager_06_Bottom",
                6,
                GetSpawnFromOutside(finalPositions[3], phase2ManagerStartExtraDistance),
                GetManagerRotationForPosition(finalPositions[3]),
                phase2AddedManagerTint,
                runtimePhase2ManagerColor
            );
        }

        List<LaserGuardianDrone> managers = new List<LaserGuardianDrone>(6)
    {
        phase2TopManagerShip,
        topLeft,
        bottomLeft,
        phase2BottomManagerShip,
        bottomRight,
        topRight
    };

        Vector3[] startPositions = new Vector3[managers.Count];
        Vector3[] endPositions = new Vector3[managers.Count];
        float[] rotations = new float[managers.Count];

        for (int i = 0; i < managers.Count; i++)
        {
            LaserGuardianDrone managerShip = managers[i];
            Vector3 finalPosition = finalPositions[i];

            startPositions[i] = managerShip != null
                ? managerShip.transform.position
                : finalPosition;

            endPositions[i] = finalPosition;
            rotations[i] = GetManagerRotationForPosition(finalPosition);
        }

        yield return MoveManagerShipsRoutine(
            managers,
            startPositions,
            endPositions,
            rotations,
            phase2ManagerMoveDuration,
            phase2ManagerMoveCurve
        );

        phase2TopBottomManagersSpawned = phase2TopManagerShip != null && phase2BottomManagerShip != null;

        if (phase2TopBottomManagersSpawned)
        {
            RebuildBoundaryLasersAsPhase2Hexagon();
        }
    }

    private Vector2[] GetPhase2HexagonFinalPositionsCounterClockwise()
    {
        Vector2 half = GetEffectiveHalfExtents();
        float x = Mathf.Max(0.1f, half.x * Mathf.Max(0.1f, phase2HexagonSideXScale));
        float y = Mathf.Max(0.1f, half.y);
        float shoulderY = y * Mathf.Clamp(phase2HexagonShoulderYRatio, 0.05f, 0.95f);

        return new[]
        {
        arenaCenter + new Vector2(0f, y),
        arenaCenter + new Vector2(-x, shoulderY),
        arenaCenter + new Vector2(-x, -shoulderY),
        arenaCenter + new Vector2(0f, -y),
        arenaCenter + new Vector2(x, -shoulderY),
        arenaCenter + new Vector2(x, shoulderY)
    };
    }
    private float GetManagerRotationForPosition(Vector2 position)
    {
        Vector2 outward = position - arenaCenter;

        if (outward.sqrMagnitude <= 0.001f)
        {
            return managerShipBaseRotationZ;
        }

        float angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
        return angle - 90f + managerShipBaseRotationZ;
    }
    private void FireSpread(
        Vector2 origin,
        Vector2 baseDirection,
        int projectileCount,
        float totalSpreadAngle,
        float damage,
        float speedOverride,
        float rangeOverride,
        float scaleMultiplier)
    {
        int count = Mathf.Max(1, projectileCount);

        if (baseDirection.sqrMagnitude <= 0.001f)
        {
            baseDirection = Vector2.up;
        }

        baseDirection.Normalize();

        if (count == 1)
        {
            SpawnProjectile(
                origin,
                baseDirection,
                damage,
                speedOverride,
                rangeOverride,
                scaleMultiplier
            );
            return;
        }

        float spread = Mathf.Max(0f, totalSpreadAngle);
        float step = spread / (count - 1);
        float startAngle = -spread * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector2 direction = RotateVector(baseDirection, angle).normalized;

            SpawnProjectile(
                origin,
                direction,
                damage,
                speedOverride,
                rangeOverride,
                scaleMultiplier
            );
        }
    }

    private void SpawnProjectile(
        Vector2 origin,
        Vector2 direction,
        float damage,
        float speedOverride,
        float rangeOverride,
        float scaleMultiplier)
    {
        if (projectileDefinition == null || projectileDefinition.ProjectilePrefab == null)
        {
            Debug.LogWarning($"{bossName}: projectileDefinition 또는 ProjectilePrefab이 없습니다.", this);
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

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

        float safeScale = Mathf.Max(0.01f, scaleMultiplier);
        projectileObject.transform.localScale = prefab.transform.localScale * safeScale;

        Bullet bullet = projectileObject.GetComponent<Bullet>();

        if (bullet == null)
        {
            Debug.LogWarning($"{bossName}: 보스 탄환 프리팹에 Bullet 컴포넌트가 없습니다.", projectileObject);
            Destroy(projectileObject);
            return;
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Enemy,
            projectileDefinition,
            damage,
            speedOverride,
            rangeOverride
        );
    }

    private void SpawnLaserHazard(
        Vector2 center,
        Vector2 direction,
        float length,
        float duration,
        float damage,
        float width,
        Color color,
        float damageCooldown)
    {
        BossLaserHazard hazard = CreateLaserHazard(center);

        if (hazard == null)
        {
            return;
        }

        hazard.Initialize(
            center,
            direction,
            length,
            width,
            duration,
            damage,
            damageCooldown,
            lineMaterial,
            color,
            lineSortingLayerName,
            lineSortingOrder
        );
    }

    private void SpawnLaserHazardBetween(
        Vector2 start,
        Vector2 end,
        float duration,
        float damage,
        float width,
        Color color,
        float damageCooldown)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;

        SpawnLaserHazard(
            center,
            direction,
            length,
            duration,
            damage,
            width,
            color,
            damageCooldown
        );
    }

    private BossLaserHazard CreateLaserHazard(Vector2 position)
    {
        if (laserHazardPrefab != null)
        {
            GameObject prefab = laserHazardPrefab.gameObject;
            GameObject hazardObject;

            if (PoolManager.Instance != null)
            {
                hazardObject = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            }
            else
            {
                hazardObject = Instantiate(prefab, position, Quaternion.identity);
            }

            BossLaserHazard hazard = hazardObject.GetComponent<BossLaserHazard>();

            if (hazard == null)
            {
                hazard = hazardObject.AddComponent<BossLaserHazard>();
            }

            return hazard;
        }

        GameObject hazardRuntimeObject = new GameObject("Boss_Laser_Hazard_Runtime");
        return hazardRuntimeObject.AddComponent<BossLaserHazard>();
    }

    private void EnsurePhase1ManagersImmediate()
    {
        if (phase1ManagersSpawned && phase1ManagerShips.Count >= 4)
        {
            return;
        }

        DestroyPhase1ManagerShips();
        phase1ManagerShips.Clear();

        Vector2[] finalPositions = GetPhase1ManagerFinalPositions();
        float[] rotations = GetPhase1ManagerRotations();
        string[] names =
        {
            "BossLaserManager_01_BottomLeft",
            "BossLaserManager_02_TopLeft",
            "BossLaserManager_03_TopRight",
            "BossLaserManager_04_BottomRight"
        };

        for (int i = 0; i < finalPositions.Length; i++)
        {
            LaserGuardianDrone managerShip = CreateLaserManagerShip(
                names[i],
                i + 1,
                finalPositions[i],
                rotations[i],
                phase1ManagerTint,
                runtimePhase1ManagerColor
            );

            if (managerShip != null)
            {
                phase1ManagerShips.Add(managerShip);
            }
        }

        phase1ManagersSpawned = phase1ManagerShips.Count >= 4;
    }

    private Vector2[] GetPhase1ManagerFinalPositions()
    {
        Vector2 half = GetEffectiveHalfExtents();

        return new[]
        {
            arenaCenter + new Vector2(-half.x, -half.y),
            arenaCenter + new Vector2(-half.x,  half.y),
            arenaCenter + new Vector2( half.x,  half.y),
            arenaCenter + new Vector2( half.x, -half.y)
        };
    }

    private float[] GetPhase1ManagerRotations()
    {
        return new[]
        {
            managerShipBaseRotationZ + 0f,
            managerShipBaseRotationZ - 90f,
            managerShipBaseRotationZ + 180f,
            managerShipBaseRotationZ + 90f
        };
    }

    private Vector2 GetPhase2TopManagerFinalPosition()
    {
        return GetPhase2HexagonFinalPositionsCounterClockwise()[0];
    }

    private Vector2 GetPhase2BottomManagerFinalPosition()
    {
        return GetPhase2HexagonFinalPositionsCounterClockwise()[3];
    }

    private LaserGuardianDrone[] GetHexagonSlotsCounterClockwise()
    {
        LaserGuardianDrone bottomLeft = phase1ManagerShips.Count > 0 ? phase1ManagerShips[0] : null;
        LaserGuardianDrone topLeft = phase1ManagerShips.Count > 1 ? phase1ManagerShips[1] : null;
        LaserGuardianDrone topRight = phase1ManagerShips.Count > 2 ? phase1ManagerShips[2] : null;
        LaserGuardianDrone bottomRight = phase1ManagerShips.Count > 3 ? phase1ManagerShips[3] : null;

        return new[]
        {
            phase2TopManagerShip,
            topLeft,
            bottomLeft,
            phase2BottomManagerShip,
            bottomRight,
            topRight
        };
    }

    private bool AreHexagonSlotsValid(LaserGuardianDrone[] slots)
    {
        if (slots == null || slots.Length < 6)
        {
            return false;
        }

        for (int i = 0; i < 6; i++)
        {
            if (slots[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildBoundaryLasersAsPhase2Hexagon()
    {
        LaserGuardianDrone[] slots = GetHexagonSlotsCounterClockwise();

        if (!AreHexagonSlotsValid(slots))
        {
            return;
        }

        DestroyBoundaryLaserWalls();

        for (int i = 0; i < 6; i++)
        {
            LaserGuardianDrone first = slots[i];
            LaserGuardianDrone second = slots[(i + 1) % 6];

            CreateBoundaryWallBetweenDrones(
                $"BossBoundaryLaser_Phase2_Hexagon_{i + 1:D2}",
                first,
                second,
                phase2BoundaryLaserColor
            );
        }

        phase1BoundaryLasersActive = true;
        phase2BoundaryRebuilt = true;
    }

    private void CreateBoundaryWallBetweenDrones(
        string objectName,
        LaserGuardianDrone firstDrone,
        LaserGuardianDrone secondDrone,
        Color color)
    {
        if (firstDrone == null || secondDrone == null)
        {
            return;
        }

        Vector2 start = firstDrone.transform.position;
        Vector2 end = secondDrone.transform.position;
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;

        BossArenaLaserWall wall;

        if (arenaLaserWallPrefab != null)
        {
            wall = Instantiate(arenaLaserWallPrefab);
            wall.name = objectName;
        }
        else
        {
            GameObject wallObject = new GameObject(objectName);
            wall = wallObject.AddComponent<BossArenaLaserWall>();
        }

        if (phase2 && phase2BoundaryLasersFollowManagers)
        {
            wall.InitializeFollowBetween(
                firstDrone.transform,
                secondDrone.transform,
                boundaryWallThickness,
                createSolidBoundaryLaserWalls,
                boundaryWallDamage * GetDamageMultiplier(),
                boundaryWallDamageInterval,
                lineMaterial,
                color,
                lineSortingLayerName,
                boundaryLaserSortingOrder,
                boundaryWallLayerName
            );
        }
        else
        {
            wall.Initialize(
                center,
                direction,
                length,
                boundaryWallThickness,
                createSolidBoundaryLaserWalls,
                boundaryWallDamage * GetDamageMultiplier(),
                boundaryWallDamageInterval,
                lineMaterial,
                color,
                lineSortingLayerName,
                boundaryLaserSortingOrder,
                boundaryWallLayerName
            );
        }
        boundaryLaserWalls.Add(wall.gameObject);
    }

    private IEnumerator MoveManagerShipsRoutine(
        List<LaserGuardianDrone> managerShips,
        Vector3[] startPositions,
        Vector3[] endPositions,
        float[] rotations,
        float duration,
        AnimationCurve moveCurve)
    {
        if (managerShips == null || managerShips.Count == 0)
        {
            yield break;
        }

        duration = Mathf.Max(0.01f, duration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = moveCurve != null ? moveCurve.Evaluate(t) : t;

            for (int i = 0; i < managerShips.Count; i++)
            {
                LaserGuardianDrone managerShip = managerShips[i];

                if (managerShip == null || i >= startPositions.Length || i >= endPositions.Length)
                {
                    continue;
                }

                managerShip.transform.position = Vector3.LerpUnclamped(startPositions[i], endPositions[i], eased);

                if (rotations != null && i < rotations.Length)
                {
                    managerShip.ApplySlotRotation(rotations[i]);
                }
            }

            yield return null;
        }

        for (int i = 0; i < managerShips.Count; i++)
        {
            LaserGuardianDrone managerShip = managerShips[i];

            if (managerShip == null || i >= endPositions.Length)
            {
                continue;
            }

            managerShip.transform.position = endPositions[i];

            if (rotations != null && i < rotations.Length)
            {
                managerShip.ApplySlotRotation(rotations[i]);
            }
        }
    }

    private LaserGuardianDrone CreateLaserManagerShip(
        string objectName,
        int runtimeIndex,
        Vector3 position,
        float rotationZ,
        Color prefabTint,
        Color runtimeColor)
    {
        GameObject managerObject;
        GameObject prefab = ResolveManagerShipPrefab();

        if (prefab != null)
        {
            managerObject = Instantiate(
                prefab,
                position,
                Quaternion.Euler(0f, 0f, rotationZ)
            );
        }
        else
        {
            if (!createRuntimeManagerIfMissing)
            {
                return null;
            }

            managerObject = CreateRuntimeLaserManagerShip(objectName, position, rotationZ, runtimeColor);
        }

        managerObject.name = objectName;

        LaserGuardianDrone managerShip = managerObject.GetComponent<LaserGuardianDrone>();

        if (managerShip == null)
        {
            managerShip = managerObject.AddComponent<LaserGuardianDrone>();
        }

        managerShip.SetRuntimeIndex(runtimeIndex);
        managerShip.ApplySlotRotation(rotationZ);
        managerShip.SetTint(prefabTint);

        return managerShip;
    }

    private GameObject ResolveManagerShipPrefab()
    {
        if (laserManagerShipPrefab != null)
        {
            return laserManagerShipPrefab;
        }

        return phase2LaserManagerShipPrefab;
    }

    private GameObject CreateRuntimeLaserManagerShip(
        string objectName,
        Vector3 position,
        float rotationZ,
        Color color)
    {
        GameObject managerObject = new GameObject(objectName);
        managerObject.transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, 0f, rotationZ)
        );

        managerObject.AddComponent<LaserGuardianDrone>();

        LineRenderer lineRenderer = managerObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 5;

        float size = Mathf.Max(0.05f, runtimeManagerSize);

        lineRenderer.SetPosition(0, new Vector3(0f, size, 0f));
        lineRenderer.SetPosition(1, new Vector3(size, 0f, 0f));
        lineRenderer.SetPosition(2, new Vector3(0f, -size, 0f));
        lineRenderer.SetPosition(3, new Vector3(-size, 0f, 0f));
        lineRenderer.SetPosition(4, new Vector3(0f, size, 0f));

        lineRenderer.startWidth = Mathf.Max(0.001f, runtimeManagerLineWidth);
        lineRenderer.endWidth = Mathf.Max(0.001f, runtimeManagerLineWidth);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingLayerName = lineSortingLayerName;
        lineRenderer.sortingOrder = boundaryLaserSortingOrder + 1;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }

        return managerObject;
    }

    private Vector3 GetSpawnFromOutside(Vector2 finalPosition, float extraDistance)
    {
        Vector2 direction = finalPosition - arenaCenter;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        return finalPosition + direction.normalized * Mathf.Max(0f, extraDistance);
    }

    private void AddTransientTelegraphBetween(
        string objectName,
        Vector2 start,
        Vector2 end,
        Color color,
        float width)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        GameObject telegraph = CreateLineObject(
            objectName,
            (start + end) * 0.5f,
            delta.normalized,
            length,
            width,
            new Color(color.r, color.g, color.b, 0.35f),
            true
        );

        if (telegraph != null)
        {
            transientVisualObjects.Add(telegraph);
        }
    }

    private void UpdateMovement(float deltaTime)
    {
        if (stopMovementWhileCasting && casting)
        {
            StopMoving();
            return;
        }

        moveTargetTimer -= deltaTime;

        if (moveTargetTimer <= 0f ||
            Vector2.Distance(transform.position, moveTarget) <= arriveDistance)
        {
            PickNewMoveTarget();
        }

        Vector2 currentPosition = transform.position;
        Vector2 toTarget = moveTarget - currentPosition;

        if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            StopMoving();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = toTarget.normalized * Mathf.Max(0f, moveSpeed);
        }
    }

    private void PickNewMoveTarget()
    {
        moveTargetTimer = Mathf.Max(0.1f, moveTargetRefreshInterval);
        moveTarget = arenaCenter + Random.insideUnitCircle * Mathf.Max(0f, bossMoveRadius);
    }

    private void StopMoving()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateFacing(float deltaTime)
    {
        if (!rotateToPlayer || player == null)
        {
            return;
        }

        Transform targetRoot = visualRoot != null ? visualRoot : transform;

        Vector2 direction = (Vector2)player.position - (Vector2)transform.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        if (turnSpeed <= 0f)
        {
            targetRoot.rotation = targetRotation;
            return;
        }

        targetRoot.rotation = Quaternion.RotateTowards(
            targetRoot.rotation,
            targetRotation,
            turnSpeed * deltaTime
        );
    }

    private void UpdatePhase()
    {
        if (phase2 || enemyHealth == null)
        {
            return;
        }

        if (enemyHealth.HpRatio > phase2HpRatio)
        {
            return;
        }

        phase2 = true;
        nextPatternIndex = 0;
        AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);

        if (logPhaseChange)
        {
            Debug.Log($"{bossName}: 2페이즈 진입. 상/하 레이저 관리기체 추가.", this);
        }

        if (phase2ManagerEntryRoutine == null && isActiveAndEnabled)
        {
            phase2ManagerEntryRoutine = StartCoroutine(Phase2ManagerEntryWrapperRoutine());
        }
    }

    private IEnumerator Phase2ManagerEntryWrapperRoutine()
    {
        yield return Phase2TopBottomManagerEntryRoutine();
        phase2ManagerEntryRoutine = null;
    }

    private IEnumerator WaitForPatternCooldown(BossPattern pattern)
    {
        float cooldown = GetPatternCooldown(pattern);
        float lastStartTime = GetLastPatternStartTime(pattern);

        if (float.IsNegativeInfinity(lastStartTime))
        {
            yield break;
        }

        float remaining = cooldown - (Time.time - lastStartTime);

        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }
    }

    private float GetPatternCooldown(BossPattern pattern)
    {
        return pattern switch
        {
            BossPattern.SectionLaser => Mathf.Max(0f, sectionLaserCooldown),
            BossPattern.SpreadBarrage => Mathf.Max(0f, spreadBarrageCooldown),
            BossPattern.TrackingChargeCannon => Mathf.Max(0f, trackingChargeCooldown),
            BossPattern.Phase2HexagonRotatingLaser => Mathf.Max(0f, phase2HexagonLaserCooldown),
            _ => 0f
        };
    }

    private float GetLastPatternStartTime(BossPattern pattern)
    {
        return pattern switch
        {
            BossPattern.SectionLaser => lastSectionLaserStartTime,
            BossPattern.SpreadBarrage => lastSpreadBarrageStartTime,
            BossPattern.TrackingChargeCannon => lastTrackingChargeStartTime,
            BossPattern.Phase2HexagonRotatingLaser => lastPhase2HexagonLaserStartTime,
            _ => float.NegativeInfinity
        };
    }

    private void MarkPatternStart(BossPattern pattern)
    {
        switch (pattern)
        {
            case BossPattern.SectionLaser:
                lastSectionLaserStartTime = Time.time;
                break;

            case BossPattern.SpreadBarrage:
                lastSpreadBarrageStartTime = Time.time;
                break;

            case BossPattern.TrackingChargeCannon:
                lastTrackingChargeStartTime = Time.time;
                break;

            case BossPattern.Phase2HexagonRotatingLaser:
                lastPhase2HexagonLaserStartTime = Time.time;
                break;
        }
    }

    private Vector2 GetEffectiveHalfExtents()
    {
        Vector2 sourceHalfExtents = hasExternalArenaContext
            ? externallyConfiguredArenaHalfExtents
            : arenaHalfExtents;

        float sourceVerticalScale = hasExternalArenaContext
            ? externallyConfiguredVerticalSpaceScale
            : verticalSpaceScale;

        return new Vector2(
            Mathf.Max(0.1f, sourceHalfExtents.x),
            Mathf.Max(0.1f, sourceHalfExtents.y * Mathf.Clamp(sourceVerticalScale, 0.2f, 1f))
        );
    }

    private float GetArenaDiagonalLength()
    {
        Vector2 half = GetEffectiveHalfExtents();
        return new Vector2(half.x * 2f, half.y * 2f).magnitude;
    }

    private Vector2 ResolveArenaCenter()
    {
        if (hasExternalArenaContext)
        {
            return externallyConfiguredArenaCenter;
        }

        if (arenaCenterOverride != null)
        {
            return arenaCenterOverride.position;
        }

        return transform.position;
    }

    private Vector2 ResolveFirePosition()
    {
        return firePoint != null
            ? (Vector2)firePoint.position
            : (Vector2)transform.position;
    }

    private Vector2 ResolveDirectionToPlayer(Vector2 origin)
    {
        if (player == null)
        {
            return Vector2.up;
        }

        Vector2 direction = (Vector2)player.position - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return Vector2.up;
        }

        return direction.normalized;
    }

    private Vector2 ResolveDirectionFromCenterToPlayer()
    {
        if (player == null)
        {
            return Vector2.up;
        }

        Vector2 direction = (Vector2)player.position - arenaCenter;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return Vector2.up;
        }

        return direction.normalized;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            player = playerHealth.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private float GetScaledMaxHp()
    {
        float value = Mathf.Max(1f, maxHp);

        if (applyDeepZoneScaling &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            ExpeditionDepth depth = RunManager.Instance.CurrentRun.ExpeditionDepth;
            float multiplier = depth == ExpeditionDepth.DeepZone1
                ? Mathf.Max(0.01f, deepZoneHealthMultiplier)
                : CampaignProgressionCatalog.GetEnemyHpMultiplier(depth);
            value *= multiplier;
        }

        return value;
    }

    private float GetDamageMultiplier()
    {
        if (applyDeepZoneScaling &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            ExpeditionDepth depth = RunManager.Instance.CurrentRun.ExpeditionDepth;
            return depth == ExpeditionDepth.DeepZone1
                ? Mathf.Max(0.01f, deepZoneDamageMultiplier)
                : CampaignProgressionCatalog.GetEnemyDamageMultiplier(depth);
        }

        return 1f;
    }

    private GameObject CreateLineObject(
        string objectName,
        Vector2 center,
        Vector2 direction,
        float length,
        float width,
        Color color,
        bool worldSpace)
    {
        GameObject lineObject = new GameObject(objectName);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        ConfigureLineRenderer(lineRenderer, width, color, worldSpace);
        UpdateLineObject(lineObject, lineRenderer, center, direction, length);

        return lineObject;
    }

    private void UpdateLineObject(
        GameObject lineObject,
        LineRenderer lineRenderer,
        Vector2 center,
        Vector2 direction,
        float length)
    {
        if (lineObject == null || lineRenderer == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        if (lineRenderer.useWorldSpace)
        {
            Vector3 start = center - direction * (length * 0.5f);
            Vector3 end = center + direction * (length * 0.5f);

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
        else
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            lineObject.transform.SetPositionAndRotation(
                center,
                Quaternion.Euler(0f, 0f, angle)
            );

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
            lineRenderer.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
        }
    }

    private void ConfigureLineRenderer(
        LineRenderer lineRenderer,
        float width,
        Color color,
        bool worldSpace)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = worldSpace;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = Mathf.Max(0.001f, width);
        lineRenderer.endWidth = Mathf.Max(0.001f, width);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingLayerName = lineSortingLayerName;
        lineRenderer.sortingOrder = lineSortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }
    }

    private void ClearTransientVisualObjects()
    {
        for (int i = transientVisualObjects.Count - 1; i >= 0; i--)
        {
            GameObject target = transientVisualObjects[i];

            if (target != null)
            {
                Destroy(target);
            }
        }

        transientVisualObjects.Clear();
    }

    private void DestroyPhase1ManagerShips()
    {
        for (int i = phase1ManagerShips.Count - 1; i >= 0; i--)
        {
            LaserGuardianDrone managerShip = phase1ManagerShips[i];

            if (managerShip != null)
            {
                Destroy(managerShip.gameObject);
            }
        }

        phase1ManagerShips.Clear();
        phase1ManagersSpawned = false;
    }

    private void DestroyPhase2ManagerShips()
    {
        if (phase2TopManagerShip != null)
        {
            Destroy(phase2TopManagerShip.gameObject);
            phase2TopManagerShip = null;
        }

        if (phase2BottomManagerShip != null)
        {
            Destroy(phase2BottomManagerShip.gameObject);
            phase2BottomManagerShip = null;
        }

        phase2TopBottomManagersSpawned = false;
        phase2BoundaryRebuilt = false;
    }

    private void DestroyBoundaryLaserWalls()
    {
        for (int i = boundaryLaserWalls.Count - 1; i >= 0; i--)
        {
            GameObject wallObject = boundaryLaserWalls[i];

            if (wallObject == null)
            {
                continue;
            }

            BossArenaLaserWall wall = wallObject.GetComponent<BossArenaLaserWall>();

            if (wall != null)
            {
                wall.Deactivate();
            }
            else
            {
                Destroy(wallObject);
            }
        }

        boundaryLaserWalls.Clear();
        phase1BoundaryLasersActive = false;
    }

    private void DisableLegacyEnemyControllersIfNeeded()
    {
        if (disableEnemyBaseAIOnAwake)
        {
            EnemyBaseAI enemyBaseAI = GetComponent<EnemyBaseAI>();

            if (enemyBaseAI != null)
            {
                enemyBaseAI.enabled = false;
            }
        }

        if (disableEnemyAttackControllerOnAwake)
        {
            EnemyAttackController enemyAttackController = GetComponent<EnemyAttackController>();

            if (enemyAttackController != null)
            {
                enemyAttackController.enabled = false;
            }
        }
    }

    private void HandleDied(EnemyHealth deadHealth)
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;
        casting = false;
        AudioManager.PlayAt(SoundEventIds.BossDeath, transform.position);

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        if (phase2ManagerEntryRoutine != null)
        {
            StopCoroutine(phase2ManagerEntryRoutine);
            phase2ManagerEntryRoutine = null;
        }

        ClearTransientVisualObjects();
        DestroyBoundaryLaserWalls();
        DestroyPhase2ManagerShips();
        DestroyPhase1ManagerShips();
        StopMoving();
    }

    private int PositiveMod(int value, int modulo)
    {
        if (modulo <= 0)
        {
            return 0;
        }

        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radian);
        float sin = Mathf.Sin(radian);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying
            ? (Vector3)arenaCenter
            : hasExternalArenaContext
                ? (Vector3)externallyConfiguredArenaCenter
                : arenaCenterOverride != null
                    ? arenaCenterOverride.position
                    : transform.position;

        Vector2 half = GetEffectiveHalfExtents();

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, new Vector3(half.x * 2f, half.y * 2f, 0f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, bossMoveRadius);

        Gizmos.color = Color.magenta;
        Vector3 top = center + new Vector3(0f, half.y, 0f);
        Vector3 bottom = center + new Vector3(0f, -half.y, 0f);
        Gizmos.DrawWireSphere(top, 0.25f);
        Gizmos.DrawWireSphere(bottom, 0.25f);
    }
}