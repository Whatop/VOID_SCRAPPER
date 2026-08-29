using System;
using System.Collections;
using UnityEngine;

public enum FrigateTriadBossState
{
    Dormant = 0,
    ThreeAlive = 1,
    Transition = 2,
    TwoAlive = 3,
    OneAlive = 4,
    FinalSequencePending = 5,
    Dead = 6
}

public enum SalvageDevourerCombatPattern
{
    None = 0,
    NWayVolley = 1,
    AimedFire = 2,
    WarningAreaStrike = 3,
    LimitedHomingMissiles = 4,
    WallRicochetBullets = 5,
    OneAliveLaser = 6,
    RotatingBullets = 7
}

public enum SalvageDevourerFinalSequenceState
{
    Inactive = 0,
    Warning = 1,
    LaneLocked = 2,
    Charging = 3,
    DeathHandoff = 4,
    Completed = 5,
    Interrupted = 6
}

[DefaultExecutionOrder(10100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class FrigateTriadBossController : MonoBehaviour
{
    private const float DeferredHealthFloor = 1f;
    private const int NWayProjectilesPerFrigate = 2;
    private static readonly WaitForFixedUpdate FixedUpdateYield = new WaitForFixedUpdate();

    [Header("Identity")]
    [SerializeField] private string bossDisplayName = "회수 포식자";
    [SerializeField] private string bossSubtitle = "SALVAGE DEVOURER";

    [Header("Aggregate Authority")]
    [SerializeField] private EnemyHealth aggregateHealth;
    [SerializeField] private BossDeathPresentation bossDeathPresentation;
    [SerializeField] private BossDummyController campaignDeathAuthority;
    [SerializeField] private Rigidbody2D bossRigidbody;

    [Header("Fixed Parts")]
    [SerializeField] private FrigateBossPart[] parts = new FrigateBossPart[3];

    [Header("Scrolling Formation")]
    [SerializeField] private Vector2 formationAnchorOffset = Vector2.zero;
    [SerializeField] private Vector2 threeAliveLeftOffset = new Vector2(-3f, 2.4f);
    [SerializeField] private Vector2 threeAliveCenterOffset = new Vector2(0f, 3f);
    [SerializeField] private Vector2 threeAliveRightOffset = new Vector2(3f, 2.4f);
    [SerializeField] private Vector2 twoAliveLeftOffset = new Vector2(-1.8f, 2.7f);
    [SerializeField] private Vector2 twoAliveRightOffset = new Vector2(1.8f, 2.7f);
    [SerializeField] private Vector2 oneAliveOffset = new Vector2(0f, 3f);
    [SerializeField, Min(0.01f)] private float formationRebalanceSpeed = 4.5f;

    [Header("Frigate Entry")]
    [SerializeField, Min(0.1f)] private float entryVerticalDistance = 5.5f;
    [SerializeField, Min(0.05f)] private float entryDuration = 0.9f;
    [SerializeField, Min(0f)] private float entryStagger = 0.12f;
    [SerializeField] private AnimationCurve entryCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Pattern Assets")]
    [SerializeField] private ProjectileDefinition bossProjectileDefinition;
    [SerializeField] private GameObject spreadProjectilePrefab;
    [SerializeField] private GameObject missileProjectilePrefab;
    [SerializeField] private SalvageDevourerWarningArea warningAreaPrefab;
    [SerializeField] private BossLaserHazard laserHazardPrefab;
    [SerializeField] private Material laserLineMaterial;
    [SerializeField] private Sprite warningIconSprite;

    [Header("Pattern Scheduler")]
    [SerializeField, Min(0.05f)] private float aliveCountTransitionDelay = 0.6f;
    [SerializeField, Min(0.05f)] private float threeAliveRecovery = 0.85f;
    [SerializeField, Min(0.05f)] private float twoAliveRecovery = 0.9f;
    [SerializeField, Min(0.05f)] private float oneAliveRecovery = 0.9f;

    [Header("Three Alive - N-Way Volley")]
    [SerializeField, Range(30f, 75f)] private float nWayTotalSpreadDegrees = 50f;
    [SerializeField, Range(1, 3)] private int nWayVolleyCount = 2;
    [SerializeField, Min(0.05f)] private float nWayVolleyInterval = 0.55f;
    [SerializeField, Min(0.1f)] private float nWayProjectileSpeed = 7.25f;
    [SerializeField, Range(0f, 18f)] private float nWayVolleyBiasDegrees = 10f;
    [SerializeField, Range(0.4f, 1f)] private float nWayProjectileScale = 0.68f;
    [SerializeField] private Color nWayProjectileColor = new Color(1f, 0.32f, 0.06f, 1f);

    [Header("Three Alive - Aimed Fire")]
    [SerializeField, Range(1, 5)] private int aimedBurstCount = 3;
    [SerializeField, Min(0.05f)] private float aimedLockDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float aimedBurstInterval = 0.38f;
    [SerializeField, Min(0f)] private float aimedPredictionTime = 0.22f;
    [SerializeField, Min(0f)] private float aimedMaximumLeadDistance = 0.8f;
    [SerializeField, Min(0f)] private float aimedHorizontalVariation = 0.28f;
    [SerializeField] private Color aimedProjectileColor = new Color(1f, 0.78f, 0.12f, 1f);

    [Header("Three Alive - Warning Area Strike")]
    [SerializeField, Range(2, 3)] private int warningAreaCount = 3;
    [SerializeField, Min(0.1f)] private float warningAreaRadius = 1.05f;
    [SerializeField, Min(0.1f)] private float warningAreaDuration = 1f;
    [SerializeField, Min(0f)] private float warningAreaDamage = 3f;
    [SerializeField, Min(0.1f)] private float warningAreaMinimumHorizontalSeparation = 2.8f;
    [SerializeField, Min(0.1f)] private float warningAreaEscapeLaneWidth = 1.4f;

    [Header("Two Alive - Limited Homing Missiles")]
    [SerializeField, Range(1, 2)] private int missilesPerFrigate = 2;
    [SerializeField, Min(0f)] private float missileDamage = 3f;
    [SerializeField, Min(0.1f)] private float missileSpeed = 7f;
    [SerializeField, Min(0.1f)] private float missileRange = 14f;
    [SerializeField, Min(0.05f)] private float missileLaunchInterval = 0.32f;
    [SerializeField, Min(0.05f)] private float missileHomingDuration = 1.5f;
    [SerializeField, Min(1f)] private float missileTurnRate = 105f;
    [SerializeField, Min(0.1f)] private float missileHomingRange = 24f;
    [SerializeField, Range(0f, 35f)] private float missileLaunchAngle = 12f;
    [SerializeField] private Color missileProjectileColor = new Color(1f, 0.12f, 0.48f, 1f);

    [Header("Two Alive - Wall Ricochet")]
    [SerializeField, Range(1, 4)] private int ricochetShotsPerFrigate = 2;
    [SerializeField, Min(0f)] private float ricochetDamage = 3f;
    [SerializeField, Min(0.1f)] private float ricochetSpeed = 9f;
    [SerializeField, Min(0.1f)] private float ricochetRange = 24f;
    [SerializeField, Range(1, 3)] private int ricochetMaximumBounces = 3;
    [SerializeField, Min(0.05f)] private float ricochetShotInterval = 0.3f;
    [SerializeField, Range(20f, 70f)] private float ricochetBaseAngleDegrees = 46f;
    [SerializeField, Range(0f, 20f)] private float ricochetAngleVariationDegrees = 11f;
    [SerializeField] private Color ricochetProjectileColor = new Color(0.1f, 0.92f, 1f, 1f);

    [Header("One Alive - Laser")]
    [SerializeField, Min(0.1f)] private float laserWarningDuration = 0.9f;
    [SerializeField, Min(0.05f)] private float laserLockDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float laserDuration = 0.65f;
    [SerializeField, Range(0.4f, 1.5f)] private float laserWidth = 1f;
    [SerializeField, Min(0f)] private float laserDamage = 4f;
    [SerializeField, Min(0.05f)] private float laserDamageInterval = 0.75f;
    [SerializeField, Min(0.1f)] private float oneAliveAlignmentSpeed = 12f;
    [SerializeField] private Color laserWarningColor = new Color(1f, 0.22f, 0.08f, 0.65f);
    [SerializeField] private Color laserLockedColor = new Color(1f, 0.85f, 0.18f, 0.95f);
    [SerializeField] private Color laserActiveColor = new Color(1f, 0.12f, 0.08f, 1f);

    [Header("One Alive - Rotating Bullets")]
    [SerializeField, Range(6, 8)] private int rotatingBulletsPerVolley = 7;
    [SerializeField, Range(3, 4)] private int rotatingVolleyCount = 3;
    [SerializeField, Min(0.05f)] private float rotatingVolleyInterval = 0.55f;
    [SerializeField, Range(10f, 20f)] private float rotatingVolleyStepDegrees = 15f;
    [SerializeField, Range(90f, 170f)] private float rotatingSectorDegrees = 140f;
    [SerializeField] private Color rotatingProjectileColor = new Color(0.82f, 0.3f, 1f, 1f);

    [Header("Non-Final Part Destruction Transition")]
    [SerializeField, Min(0.1f)] private float destructionChargeWarningDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float destructionChargeRotationDuration = 0.4f;
    [SerializeField, Min(0.05f)] private float destructionChargeHoldDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float destructionChargeSpeed = 18f;
    [SerializeField, Min(0f)] private float destructionChargeEndpointInset = 0.55f;
    [SerializeField, Min(0f)] private float destructionWreckFadeDelay = 0.35f;
    [SerializeField, Min(0.05f)] private float destructionWreckFadeDuration = 2f;
    [SerializeField] private Color destructionChargeWarningTint = new Color(1f, 0.26f, 0.05f, 1f);

    [Header("Final Charge")]
    [SerializeField, Min(0.1f)] private float finalWarningDuration = 0.85f;
    [SerializeField, Min(0.05f)] private float finalLaneLockDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float finalAlignmentSpeed = 12f;
    [SerializeField, Min(0.1f)] private float finalChargeSpeed = 16f;
    [SerializeField, Min(0f)] private float finalChargeDamage = 6f;
    [SerializeField] private Vector2 finalChargeHitboxSize = new Vector2(1f, 1.6f);
    [SerializeField] private Vector2 finalChargeHitboxOffset = new Vector2(0f, -0.1f);
    [SerializeField, Min(0.1f)] private float finalChargeEndpointPadding = 0.65f;
    [SerializeField, Min(0.05f)] private float finalSparkInterval = 0.18f;
    [SerializeField] private GameObject finalSparkPrefab;
    [SerializeField] private Color finalWarningTint = new Color(1f, 0.22f, 0.08f, 1f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Development Testing")]
    [SerializeField, Min(0.1f)] private float developmentDamage = 10f;
#endif

    private RunManager observedRunManager;
    private PlayerHealth observedPlayerHealth;
    private PlayerRadarScanner bossFightRadarScanner;
    private ExpeditionMenuController bossFightMenuController;
    private ExpeditionHUD bossFightHud;
    private RadarPanelAnimator bossFightRadarPanel;
    private BossHealthBarUI bossFightHealthBar;
    private SalvageDevourerCorridorController corridorController;
    private Rigidbody2D observedPlayerRigidbody;
    private FrigateBossPart finalPendingPart;
    private FrigateTriadBossState state = FrigateTriadBossState.Dormant;
    private SalvageDevourerCombatPattern activePattern;
    private SalvageDevourerCombatPattern lastPattern;
    private Coroutine patternRoutine;
    private int patternToken;
    private int nextThreeAlivePatternIndex;
    private int nextTwoAlivePatternIndex;
    private int nextOneAlivePatternIndex;
    private int warningPlacementSequence;
    private int nWayBiasSequence;
    private int alivePartCount;
    private bool initialized;
    private bool partEventsSubscribed;
    private bool runEventSubscribed;
    private bool deferredCompletionStarted;
    private bool finalPendingNotificationSent;
    private bool aggregateDeathSubscribed;
    private bool playerDeathSubscribed;
    private bool formationFollowing;
    private bool entryPlaying;
    private bool entryComplete;
    private bool entrySkipRequested;
    private bool cleanupInProgress;
    private bool aggregateDeathInProgress;
    private bool gameplayBegun;
    private bool missingPatternAssetWarningLogged;
    private bool formationMovementPaused;
    private bool finalChargeDamageAttempted;
    private bool finalChargeMissForced;
    private bool finalSequenceCompletionRequested;
    private bool bossFightUiModeActive;
    private bool nonFinalDestructionTransitionActive;
    private int finalSequenceToken;
    private int nonFinalDestructionToken;
    private float lockedPatternLaneX;
    private float lockedFinalChargeLaneX;
    private float finalChargeEndpointY;
    private float finalSparkTimer;
    private Coroutine finalSequenceRoutine;
    private Coroutine nonFinalDestructionRoutine;
    private FrigateBossPart nonFinalDestructionPart;
    private Quaternion nonFinalDestructionStartRotation = Quaternion.identity;
    private Color nonFinalDestructionOriginalColor = Color.white;
    private BossLaserHazard activeLaserHazard;
    private LineRenderer encounterLineRenderer;
    private SpriteRenderer encounterWarningIcon;
    private Collider2D observedPlayerCollider;
    private SalvageDevourerFinalSequenceState finalSequenceState;
    private Color finalPartOriginalColor = Color.white;
    private readonly Vector2[] formationTargets = new Vector2[3];
    private readonly Vector2[] entryStartOffsets = new Vector2[3];
    private readonly Vector2[] lockedAimDirections = new Vector2[3];
    private readonly Vector2[] warningPositions = new Vector2[3];
    private readonly SalvageDevourerWarningArea[] activeWarnings =
        new SalvageDevourerWarningArea[3];
    private readonly GameObject[] activeFinalSparks = new GameObject[8];

    public string BossDisplayName => bossDisplayName;
    public string BossSubtitle => bossSubtitle;
    public EnemyHealth AggregateHealth => aggregateHealth;
    public float TotalMaxHealth => ResolveTotalMaxHealth();
    public int AlivePartCount => alivePartCount;
    public FrigateTriadBossState State => state;
    public SalvageDevourerCombatPattern ActivePattern => activePattern;
    public SalvageDevourerFinalSequenceState FinalSequenceState => finalSequenceState;
    public bool IsGameplayActive => gameplayBegun &&
        state != FrigateTriadBossState.Dormant &&
        state != FrigateTriadBossState.FinalSequencePending &&
        state != FrigateTriadBossState.Dead;
    public bool IsFinalSequencePending => state == FrigateTriadBossState.FinalSequencePending;
    public bool IsEntryPlaying => entryPlaying;
    public bool IsEntryComplete => entryComplete;
    public bool IsFollowingCorridor => formationFollowing;
    public Vector2 CurrentScrollAnchor => ResolveCurrentScrollAnchor();
    public FrigateBossPart FinalPendingPart => finalPendingPart;
    public bool CanAcceptPartDamage =>
        initialized &&
        isActiveAndEnabled &&
        IsGameplayActive &&
        aggregateHealth != null &&
        !aggregateHealth.IsDead &&
        !nonFinalDestructionTransitionActive;

    public event Action<FrigateBossPart> PartDestroyed;
    public event Action<int> AlivePartCountChanged;
    public event Action<FrigateBossPart> FinalSequencePendingStarted;
    public event Action<FrigateTriadBossState> StateChanged;

    private void Awake()
    {
        ResolveSerializedReferences();
    }

    private void OnEnable()
    {
        PrepareDormantBossState();
    }

    private void OnDisable()
    {
        CleanupBossRuntime();
    }

    private void OnDestroy()
    {
        CleanupBossRuntime();

        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.DetachRuntime(this);
                parts[i] = null;
            }
        }

        aggregateHealth = null;
        bossDeathPresentation = null;
        campaignDeathAuthority = null;
        bossRigidbody = null;
        corridorController = null;
        observedPlayerHealth = null;
        observedPlayerRigidbody = null;
        observedPlayerCollider = null;
        bossFightHud = null;
        bossFightRadarPanel = null;
        finalPendingPart = null;
        activeLaserHazard = null;
        encounterLineRenderer = null;
        encounterWarningIcon = null;
        PartDestroyed = null;
        AlivePartCountChanged = null;
        FinalSequencePendingStarted = null;
        StateChanged = null;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (IsRunEnding() || corridorController == null ||
            !corridorController.IsRuntimeActive)
        {
            CleanupBossRuntime();
            return;
        }

        if (!formationFollowing)
        {
            return;
        }

        PositionBossRootAtScrollAnchor();
        if (!formationMovementPaused)
        {
            MoveLivingPartsTowardFormationTargets();
        }
    }

    private void OnValidate()
    {
        formationRebalanceSpeed = Mathf.Max(0.01f, formationRebalanceSpeed);
        entryVerticalDistance = Mathf.Max(0.1f, entryVerticalDistance);
        entryDuration = Mathf.Max(0.05f, entryDuration);
        entryStagger = Mathf.Max(0f, entryStagger);
        aliveCountTransitionDelay = Mathf.Max(0.05f, aliveCountTransitionDelay);
        threeAliveRecovery = Mathf.Max(0.05f, threeAliveRecovery);
        twoAliveRecovery = Mathf.Max(0.05f, twoAliveRecovery);
        oneAliveRecovery = Mathf.Max(0.05f, oneAliveRecovery);
        nWayVolleyCount = Mathf.Clamp(nWayVolleyCount, 1, 3);
        nWayVolleyInterval = Mathf.Max(0.05f, nWayVolleyInterval);
        nWayProjectileSpeed = Mathf.Max(0.1f, nWayProjectileSpeed);
        nWayVolleyBiasDegrees = Mathf.Clamp(nWayVolleyBiasDegrees, 0f, 18f);
        nWayProjectileScale = Mathf.Clamp(nWayProjectileScale, 0.4f, 1f);
        aimedBurstCount = Mathf.Clamp(aimedBurstCount, 1, 5);
        aimedLockDuration = Mathf.Max(0.05f, aimedLockDuration);
        aimedBurstInterval = Mathf.Max(0.05f, aimedBurstInterval);
        aimedPredictionTime = Mathf.Max(0f, aimedPredictionTime);
        aimedMaximumLeadDistance = Mathf.Max(0f, aimedMaximumLeadDistance);
        aimedHorizontalVariation = Mathf.Max(0f, aimedHorizontalVariation);
        warningAreaCount = Mathf.Clamp(warningAreaCount, 2, 3);
        warningAreaRadius = Mathf.Max(0.1f, warningAreaRadius);
        warningAreaDuration = Mathf.Max(0.1f, warningAreaDuration);
        warningAreaDamage = Mathf.Max(0f, warningAreaDamage);
        warningAreaMinimumHorizontalSeparation = Mathf.Max(
            warningAreaRadius * 2f,
            warningAreaMinimumHorizontalSeparation
        );
        warningAreaEscapeLaneWidth = Mathf.Max(0.1f, warningAreaEscapeLaneWidth);
        missilesPerFrigate = Mathf.Clamp(missilesPerFrigate, 1, 2);
        missileDamage = Mathf.Max(0f, missileDamage);
        missileSpeed = Mathf.Max(0.1f, missileSpeed);
        missileRange = Mathf.Max(0.1f, missileRange);
        missileLaunchInterval = Mathf.Max(0.05f, missileLaunchInterval);
        missileHomingDuration = Mathf.Max(0.05f, missileHomingDuration);
        missileTurnRate = Mathf.Max(1f, missileTurnRate);
        missileHomingRange = Mathf.Max(0.1f, missileHomingRange);
        ricochetShotsPerFrigate = Mathf.Clamp(ricochetShotsPerFrigate, 1, 4);
        ricochetDamage = Mathf.Max(0f, ricochetDamage);
        ricochetSpeed = Mathf.Max(0.1f, ricochetSpeed);
        ricochetRange = Mathf.Max(0.1f, ricochetRange);
        ricochetMaximumBounces = Mathf.Clamp(ricochetMaximumBounces, 1, 3);
        ricochetShotInterval = Mathf.Max(0.05f, ricochetShotInterval);
        ricochetBaseAngleDegrees = Mathf.Clamp(ricochetBaseAngleDegrees, 20f, 70f);
        ricochetAngleVariationDegrees = Mathf.Clamp(ricochetAngleVariationDegrees, 0f, 20f);
        laserWarningDuration = Mathf.Max(0.1f, laserWarningDuration);
        laserLockDuration = Mathf.Max(0.05f, laserLockDuration);
        laserDuration = Mathf.Max(0.1f, laserDuration);
        laserWidth = Mathf.Clamp(laserWidth, 0.4f, 1.5f);
        laserDamage = Mathf.Max(0f, laserDamage);
        laserDamageInterval = Mathf.Max(0.05f, laserDamageInterval);
        oneAliveAlignmentSpeed = Mathf.Max(0.1f, oneAliveAlignmentSpeed);
        rotatingBulletsPerVolley = Mathf.Clamp(rotatingBulletsPerVolley, 6, 8);
        rotatingVolleyCount = Mathf.Clamp(rotatingVolleyCount, 3, 4);
        rotatingVolleyInterval = Mathf.Max(0.05f, rotatingVolleyInterval);
        rotatingVolleyStepDegrees = Mathf.Clamp(rotatingVolleyStepDegrees, 10f, 20f);
        rotatingSectorDegrees = Mathf.Clamp(rotatingSectorDegrees, 90f, 170f);
        destructionChargeWarningDuration = Mathf.Max(0.1f, destructionChargeWarningDuration);
        destructionChargeRotationDuration = Mathf.Max(0.05f, destructionChargeRotationDuration);
        destructionChargeHoldDuration = Mathf.Max(0.05f, destructionChargeHoldDuration);
        destructionChargeSpeed = Mathf.Max(0.1f, destructionChargeSpeed);
        destructionChargeEndpointInset = Mathf.Max(0f, destructionChargeEndpointInset);
        destructionWreckFadeDelay = Mathf.Max(0f, destructionWreckFadeDelay);
        destructionWreckFadeDuration = Mathf.Max(0.05f, destructionWreckFadeDuration);
        finalWarningDuration = Mathf.Max(0.1f, finalWarningDuration);
        finalLaneLockDuration = Mathf.Max(0.05f, finalLaneLockDuration);
        finalAlignmentSpeed = Mathf.Max(0.1f, finalAlignmentSpeed);
        finalChargeSpeed = Mathf.Max(0.1f, finalChargeSpeed);
        finalChargeDamage = Mathf.Max(0f, finalChargeDamage);
        finalChargeHitboxSize.x = Mathf.Max(0.1f, finalChargeHitboxSize.x);
        finalChargeHitboxSize.y = Mathf.Max(0.1f, finalChargeHitboxSize.y);
        finalChargeEndpointPadding = Mathf.Max(0.1f, finalChargeEndpointPadding);
        finalSparkInterval = Mathf.Max(0.05f, finalSparkInterval);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        developmentDamage = Mathf.Max(0.1f, developmentDamage);
#endif
    }

    public FrigateBossPart GetPart(FrigateBossPartId partId)
    {
        if (parts == null)
        {
            return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.PartId == partId)
            {
                return part;
            }
        }

        return null;
    }

    public bool IsPartAlive(FrigateBossPartId partId)
    {
        FrigateBossPart part = GetPart(partId);
        return part != null && part.IsAlive;
    }

    public bool TryApplyPartDamage(
        FrigateBossPart part,
        float incomingDamage,
        Vector2 hitPoint,
        Vector2 incomingDirection)
    {
        if (!CanAcceptPartDamage ||
            part == null ||
            !OwnsPart(part) ||
            !part.IsAlive ||
            incomingDamage <= 0f)
        {
            return false;
        }

        bool isLethal = incomingDamage >= part.CurrentHealth;
        bool isFinalAlivePart = alivePartCount == 1;

        if (isLethal && isFinalAlivePart)
        {
            BeginFinalSequencePending(
                part,
                incomingDamage,
                hitPoint,
                incomingDirection
            );
            return true;
        }

        float acceptedDamage = part.ApplyAcceptedDamage(incomingDamage);
        if (acceptedDamage <= 0f)
        {
            return false;
        }

        aggregateHealth.TakeDamage(acceptedDamage, hitPoint, incomingDirection);

        if (isLethal)
        {
            BeginNonFinalDestructionTransition(part);
        }

        return true;
    }

    public bool CompleteDeferredBossDeath()
    {
        if (!initialized ||
            deferredCompletionStarted ||
            state != FrigateTriadBossState.FinalSequencePending ||
            finalPendingPart == null ||
            aggregateHealth == null ||
            aggregateHealth.IsDead)
        {
            return false;
        }

        deferredCompletionStarted = true;
        FrigateBossPart completingPart = finalPendingPart;

        if (bossDeathPresentation != null)
        {
            bossDeathPresentation.SetBodyRenderer(completingPart.VisualRenderer);
            bossDeathPresentation.SetDeathPositionOverride(completingPart.transform.position);
        }

        completingPart.CompleteDeferredDestruction();
        finalPendingPart = null;
        SetState(FrigateTriadBossState.Dead);

        float lethalDamage = Mathf.Max(DeferredHealthFloor, aggregateHealth.CurrentHp);
        aggregateHealth.TakeDamage(
            lethalDamage,
            completingPart.transform.position,
            Vector2.zero
        );
        return aggregateHealth.IsDead;
    }

    public bool PrepareForIntro(SalvageDevourerCorridorController corridor)
    {
        if (corridor == null || !corridor.IsPrepared)
        {
            return false;
        }

        if (!InitializeRuntimeState(false))
        {
            return false;
        }

        corridorController = corridor;
        formationFollowing = false;
        entryComplete = false;
        entryPlaying = false;
        entrySkipRequested = false;
        PositionBossRootAtScrollAnchor();
        ResolveFormationTargets(false);
        SetPartsAtEntryStartOffsets();
        SubscribePlayerDeath();
        EnterBossFightUiMode();
        return true;
    }

    public Vector3 ResolveIntroRootPosition(Vector2 corridorCameraStartCenter)
    {
        return corridorCameraStartCenter + formationAnchorOffset;
    }

    public IEnumerator PlayEntryRoutine()
    {
        if (!initialized || corridorController == null || !corridorController.IsPrepared)
        {
            yield break;
        }

        entryPlaying = true;
        entryComplete = false;
        entrySkipRequested = false;
        ResolveFormationTargets(false);
        CacheEntryStartOffsets();

        float duration = Mathf.Max(0.05f, entryDuration);
        float stagger = Mathf.Max(0f, entryStagger);
        float totalDuration = duration + stagger * Mathf.Max(0, parts.Length - 1);
        float elapsed = 0f;

        while (elapsed < totalDuration && !entrySkipRequested)
        {
            if (!initialized || corridorController == null ||
                !corridorController.IsRuntimeActive || IsRunEnding())
            {
                entryPlaying = false;
                yield break;
            }

            elapsed += Time.deltaTime;

            for (int i = 0; i < parts.Length; i++)
            {
                FrigateBossPart part = parts[i];
                if (part == null || !part.IsAlive)
                {
                    continue;
                }

                float normalized = Mathf.Clamp01((elapsed - stagger * i) / duration);
                float eased = entryCurve != null && entryCurve.length > 0
                    ? entryCurve.Evaluate(normalized)
                    : Mathf.SmoothStep(0f, 1f, normalized);
                part.transform.localPosition = Vector2.LerpUnclamped(
                    entryStartOffsets[i],
                    formationTargets[i],
                    eased
                );
            }

            yield return null;
        }

        SnapLivingPartsToFormationTargets();
        entryPlaying = false;
        entryComplete = true;
    }

    public bool BeginFormationFollowing()
    {
        if (!initialized || !entryComplete || corridorController == null ||
            !corridorController.IsRuntimeActive)
        {
            return false;
        }

        PositionBossRootAtScrollAnchor();
        formationFollowing = true;
        return true;
    }

    public bool BeginGameplay()
    {
        if (!initialized || !entryComplete || !formationFollowing ||
            corridorController == null || !corridorController.IsRuntimeActive || IsRunEnding())
        {
            return false;
        }

        gameplayBegun = true;
        SubscribePlayerDeath();
        SetState(ResolveCombatStateForAliveCount(alivePartCount));

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.IsAlive)
            {
                part.SetRuntimeDamageEnabled(true);
            }
        }

        if (campaignDeathAuthority != null)
        {
            campaignDeathAuthority.enabled = true;
        }

        StartPatternScheduler();

        return true;
    }

    public void InitializeBossState()
    {
        if (!InitializeRuntimeState(true))
        {
            return;
        }

        entryComplete = true;
        ResolveFormationTargets(true);
        SnapLivingPartsToFormationTargets();
    }

    public void CleanupBossRuntime()
    {
        if (cleanupInProgress)
        {
            return;
        }

        if (!initialized && !partEventsSubscribed && !runEventSubscribed &&
            !aggregateDeathSubscribed && !playerDeathSubscribed && corridorController == null &&
            !bossFightUiModeActive)
        {
            return;
        }

        cleanupInProgress = true;
        CancelFinalSequence(false);
        CancelNonFinalDestructionTransition();
        CancelPatternWork(true);
        initialized = false;
        gameplayBegun = false;
        formationFollowing = false;
        formationMovementPaused = false;
        entryPlaying = false;
        entryComplete = false;
        entrySkipRequested = true;
        UnsubscribePartEvents();
        UnsubscribeRunEnd();
        UnsubscribeAggregateDeath();
        UnsubscribePlayerDeath();
        ExitBossFightUiMode();

        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
        }

        if (!aggregateDeathInProgress && campaignDeathAuthority != null)
        {
            campaignDeathAuthority.enabled = false;
        }

        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i]?.SetRuntimeDamageEnabled(false);
                parts[i]?.StopDetachedWreckPresentation(true);
            }
        }

        SalvageDevourerCorridorController ownedCorridor = corridorController;
        corridorController = null;
        ownedCorridor?.CleanupCorridorRuntime();

        if (!aggregateDeathInProgress)
        {
            BossHealthBarUI.Instance?.Hide();
        }

        finalPendingPart = null;
        deferredCompletionStarted = false;
        finalPendingNotificationSent = false;
        finalSequenceState = SalvageDevourerFinalSequenceState.Inactive;
        nonFinalDestructionTransitionActive = false;
        alivePartCount = 0;
        SetState(aggregateDeathInProgress
            ? FrigateTriadBossState.Dead
            : FrigateTriadBossState.Dormant);
        cleanupInProgress = false;
    }

    private void PrepareDormantBossState()
    {
        InitializeRuntimeState(false);
    }

    private bool InitializeRuntimeState(bool activateGameplay)
    {
        ResolveSerializedReferences();
        UnsubscribePartEvents();
        UnsubscribeRunEnd();
        UnsubscribeAggregateDeath();
        UnsubscribePlayerDeath();

        if (aggregateHealth == null || bossRigidbody == null || !HasExactlyThreeValidParts())
        {
            initialized = false;
            SetState(FrigateTriadBossState.Dormant);
            Debug.LogWarning(
                "Salvage Devourer requires one aggregate EnemyHealth, one BossRoot " +
                "Rigidbody2D, and exactly three uniquely identified FrigateBossPart references.",
                this
            );
            return false;
        }

        float totalMaxHealth = ResolveTotalMaxHealth();
        aggregateHealth.SetRewardDropEnabled(false);
        aggregateHealth.SetMaxHp(totalMaxHealth, true);
        aggregateHealth.ResetHealth();

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].InitializeRuntime(this, aggregateHealth);
            parts[i].SetRuntimeDamageEnabled(activateGameplay);
        }

        if (campaignDeathAuthority != null)
        {
            campaignDeathAuthority.enabled = activateGameplay;
        }

        bossRigidbody.linearVelocity = Vector2.zero;
        bossRigidbody.angularVelocity = 0f;
        bossRigidbody.interpolation = RigidbodyInterpolation2D.None;
        alivePartCount = parts.Length;
        finalPendingPart = null;
        deferredCompletionStarted = false;
        finalPendingNotificationSent = false;
        formationFollowing = false;
        entryPlaying = false;
        entryComplete = activateGameplay;
        entrySkipRequested = false;
        gameplayBegun = false;
        activePattern = SalvageDevourerCombatPattern.None;
        lastPattern = SalvageDevourerCombatPattern.None;
        nextThreeAlivePatternIndex = 0;
        nextTwoAlivePatternIndex = 0;
        nextOneAlivePatternIndex = 0;
        warningPlacementSequence = 0;
        nWayBiasSequence = 0;
        formationMovementPaused = false;
        finalChargeDamageAttempted = false;
        finalChargeMissForced = false;
        finalSequenceCompletionRequested = false;
        finalSparkTimer = 0f;
        finalSequenceState = SalvageDevourerFinalSequenceState.Inactive;
        finalSequenceToken++;
        nonFinalDestructionToken++;
        nonFinalDestructionTransitionActive = false;
        nonFinalDestructionRoutine = null;
        nonFinalDestructionPart = null;
        HideEncounterLine();
        SetWarningIconVisible(false, Vector2.zero);
        ReleaseFinalSparks();
        initialized = true;
        SubscribePartEvents();
        SubscribeRunEnd();
        SubscribeAggregateDeath();
        SetState(FrigateTriadBossState.Dormant);
        AlivePartCountChanged?.Invoke(alivePartCount);
        return true;
    }

    private void BeginNonFinalDestructionTransition(FrigateBossPart part)
    {
        if (!initialized || !gameplayBegun || part == null || !OwnsPart(part) ||
            alivePartCount <= 1 || nonFinalDestructionTransitionActive ||
            !part.EnterDestructionTransitionState())
        {
            return;
        }

        CancelPatternWork(false);
        SetState(FrigateTriadBossState.Transition);
        formationMovementPaused = true;
        nonFinalDestructionTransitionActive = true;
        nonFinalDestructionPart = part;
        nonFinalDestructionStartRotation = part.transform.localRotation;
        nonFinalDestructionOriginalColor = part.VisualRenderer != null
            ? part.VisualRenderer.color
            : Color.white;
        nonFinalDestructionToken++;
        int token = nonFinalDestructionToken;
        nonFinalDestructionRoutine = StartCoroutine(
            NonFinalDestructionTransitionRoutine(token, part)
        );
    }

    private IEnumerator NonFinalDestructionTransitionRoutine(
        int token,
        FrigateBossPart part)
    {
        if (!TryGetCombatViewportBounds(out Bounds bounds))
        {
            nonFinalDestructionRoutine = null;
            nonFinalDestructionTransitionActive = false;
            nonFinalDestructionPart = null;
            part.EnterDestroyedState();
            yield break;
        }

        AudioManager.PlayAt(SoundEventIds.BossChargeAim, part.transform.position, 0.72f);
        float laneX = part.transform.position.x;
        float warningElapsed = 0f;
        float warningDuration = Mathf.Max(0.1f, destructionChargeWarningDuration);
        float rotationDuration = Mathf.Min(
            warningDuration,
            Mathf.Max(0.05f, destructionChargeRotationDuration)
        );
        Quaternion targetRotation = nonFinalDestructionStartRotation *
                                    Quaternion.Euler(0f, 0f, 180f);

        while (warningElapsed < warningDuration &&
               CanContinueNonFinalDestructionTransition(token, part))
        {
            warningElapsed += Mathf.Max(0f, Time.deltaTime);
            float rotationProgress = Mathf.Clamp01(warningElapsed / rotationDuration);
            part.transform.localRotation = Quaternion.Slerp(
                nonFinalDestructionStartRotation,
                targetRotation,
                Mathf.SmoothStep(0f, 1f, rotationProgress)
            );
            UpdateNonFinalDestructionWarning(part, bounds, laneX);
            yield return null;
        }

        if (!CanContinueNonFinalDestructionTransition(token, part))
        {
            yield break;
        }

        part.transform.localRotation = targetRotation;
        float holdElapsed = 0f;
        float holdDuration = Mathf.Max(0.05f, destructionChargeHoldDuration);
        while (holdElapsed < holdDuration &&
               CanContinueNonFinalDestructionTransition(token, part))
        {
            holdElapsed += Mathf.Max(0f, Time.deltaTime);
            UpdateNonFinalDestructionWarning(part, bounds, laneX);
            yield return null;
        }

        if (!CanContinueNonFinalDestructionTransition(token, part))
        {
            yield break;
        }

        SetWarningIconVisible(false, Vector2.zero);
        HideEncounterLine();
        RestoreNonFinalDestructionTint(part);
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, part.transform.position, 0.8f);

        Transform movementParent = part.transform.parent;
        float endpointWorldY = bounds.min.y + destructionChargeEndpointInset;
        float endpointLocalY = movementParent != null
            ? movementParent.InverseTransformPoint(new Vector3(laneX, endpointWorldY, 0f)).y
            : endpointWorldY;
        float lockedLocalX = part.transform.localPosition.x;

        while (part.transform.localPosition.y > endpointLocalY &&
               CanContinueNonFinalDestructionTransition(token, part))
        {
            Vector3 localPosition = part.transform.localPosition;
            localPosition.x = lockedLocalX;
            localPosition.y = Mathf.MoveTowards(
                localPosition.y,
                endpointLocalY,
                destructionChargeSpeed * Mathf.Max(0f, Time.deltaTime)
            );
            part.transform.localPosition = localPosition;
            yield return null;
        }

        if (!CanContinueNonFinalDestructionTransition(token, part))
        {
            yield break;
        }

        Vector3 endpoint = part.transform.localPosition;
        endpoint.x = lockedLocalX;
        endpoint.y = endpointLocalY;
        part.transform.localPosition = endpoint;

        nonFinalDestructionRoutine = null;
        nonFinalDestructionTransitionActive = false;
        nonFinalDestructionPart = null;
        formationMovementPaused = false;
        part.EnterDestroyedState();
        part.BeginDetachedWreckFade(
            destructionWreckFadeDelay,
            destructionWreckFadeDuration
        );
    }

    private bool CanContinueNonFinalDestructionTransition(
        int token,
        FrigateBossPart part)
    {
        return initialized &&
               gameplayBegun &&
               isActiveAndEnabled &&
               token == nonFinalDestructionToken &&
               state == FrigateTriadBossState.Transition &&
               nonFinalDestructionTransitionActive &&
               ReferenceEquals(nonFinalDestructionPart, part) &&
               part != null &&
               part.State == FrigateBossPartState.DestructionTransition &&
               !IsRunEnding() &&
               corridorController != null &&
               corridorController.IsRuntimeActive;
    }

    private void UpdateNonFinalDestructionWarning(
        FrigateBossPart part,
        Bounds bounds,
        float laneX)
    {
        Vector2 lineStart = new Vector2(laneX, part.transform.position.y - 0.35f);
        Vector2 lineEnd = new Vector2(laneX, bounds.min.y + destructionChargeEndpointInset);
        ShowEncounterLine(
            lineStart,
            lineEnd,
            finalChargeHitboxSize.x * 0.22f,
            laserWarningColor
        );
        SetWarningIconVisible(true, (Vector2)part.transform.position + Vector2.down * 0.8f);

        if (part.VisualRenderer != null)
        {
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 18f) * 0.5f;
            part.VisualRenderer.color = Color.Lerp(
                nonFinalDestructionOriginalColor,
                destructionChargeWarningTint,
                0.35f + pulse * 0.5f
            );
        }
    }

    private void CancelNonFinalDestructionTransition()
    {
        nonFinalDestructionToken++;
        if (nonFinalDestructionRoutine != null)
        {
            StopCoroutine(nonFinalDestructionRoutine);
            nonFinalDestructionRoutine = null;
        }

        if (nonFinalDestructionPart != null)
        {
            nonFinalDestructionPart.transform.localRotation = nonFinalDestructionStartRotation;
            RestoreNonFinalDestructionTint(nonFinalDestructionPart);
        }

        nonFinalDestructionPart = null;
        nonFinalDestructionTransitionActive = false;
        SetWarningIconVisible(false, Vector2.zero);
        HideEncounterLine();
    }

    private void RestoreNonFinalDestructionTint(FrigateBossPart part)
    {
        if (part != null && part.VisualRenderer != null)
        {
            part.VisualRenderer.color = nonFinalDestructionOriginalColor;
        }
    }

    private void BeginFinalSequencePending(
        FrigateBossPart part,
        float incomingDamage,
        Vector2 hitPoint,
        Vector2 incomingDirection)
    {
        float localDamageCapacity = Mathf.Max(
            0f,
            part.CurrentHealth - DeferredHealthFloor
        );
        float aggregateDamageCapacity = Mathf.Max(
            0f,
            aggregateHealth.CurrentHp - DeferredHealthFloor
        );
        float acceptedDamage = Mathf.Min(
            incomingDamage,
            Mathf.Min(localDamageCapacity, aggregateDamageCapacity)
        );

        if (acceptedDamage > 0f)
        {
            float appliedDamage = part.ApplyAcceptedDamage(acceptedDamage);
            aggregateHealth.TakeDamage(appliedDamage, hitPoint, incomingDirection);
        }
        else
        {
            part.PlayPendingLethalHitFeedback(incomingDamage);
        }

        finalPendingPart = part;
        CancelPatternWork(true);
        SetState(FrigateTriadBossState.FinalSequencePending);
        part.EnterFinalSequencePending();
    }

    private void HandlePartDestroyed(FrigateBossPart part)
    {
        if (!initialized || part == null || !OwnsPart(part))
        {
            return;
        }

        alivePartCount = Mathf.Max(0, alivePartCount - 1);
        ResolveFormationTargets(false);
        RequestAliveCountTransition();
        PartDestroyed?.Invoke(part);
        AlivePartCountChanged?.Invoke(alivePartCount);
    }

    private void HandleFinalSequencePending(FrigateBossPart part)
    {
        if (!initialized ||
            finalPendingNotificationSent ||
            part == null ||
            !ReferenceEquals(part, finalPendingPart))
        {
            return;
        }

        finalPendingNotificationSent = true;
        FinalSequencePendingStarted?.Invoke(part);
        StartFinalSequence(part, false);
    }

    private void StartFinalSequence(FrigateBossPart part, bool skipWarning)
    {
        if (!initialized ||
            !gameplayBegun ||
            state != FrigateTriadBossState.FinalSequencePending ||
            part == null ||
            !ReferenceEquals(part, finalPendingPart) ||
            finalSequenceRoutine != null ||
            IsRunEnding())
        {
            return;
        }

        CancelPatternWork(true);
        ReleaseActiveWarnings();
        ReleaseActiveLaser();
        Bullet.ReleaseAllActiveFromSource(transform);
        formationFollowing = false;
        formationMovementPaused = true;
        finalChargeDamageAttempted = false;
        finalChargeMissForced = false;
        finalSequenceCompletionRequested = false;
        finalSparkTimer = 0f;
        finalPartOriginalColor = part.VisualRenderer != null
            ? part.VisualRenderer.color
            : Color.white;

        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
        }

        corridorController?.SetScrollPaused(true);
        finalSequenceToken++;
        int token = finalSequenceToken;
        finalSequenceRoutine = StartCoroutine(
            FinalSequenceRoutine(token, part, skipWarning)
        );
    }

    private IEnumerator FinalSequenceRoutine(
        int token,
        FrigateBossPart part,
        bool skipWarning)
    {
        if (!TryGetCombatViewportBounds(out Bounds bounds))
        {
            finalSequenceRoutine = null;
            CancelFinalSequence(false);
            yield break;
        }

        float horizontalMargin = finalChargeHitboxSize.x * 0.5f + 0.4f;
        float sampledX = observedPlayerHealth != null
            ? observedPlayerHealth.transform.position.x
            : part.transform.position.x;
        lockedFinalChargeLaneX = Mathf.Clamp(
            sampledX,
            bounds.min.x + horizontalMargin,
            bounds.max.x - horizontalMargin
        );
        finalChargeEndpointY = bounds.min.y -
            Mathf.Max(0.1f, finalChargeEndpointPadding);

        if (!skipWarning)
        {
            finalSequenceState = SalvageDevourerFinalSequenceState.Warning;
            AudioManager.PlayAt(SoundEventIds.BossChargeAim, part.transform.position, 0.86f);
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, finalWarningDuration);

            while (elapsed < duration && CanContinueFinalSequence(token, part))
            {
                elapsed += Mathf.Max(0f, Time.deltaTime);
                MovePartHorizontallyToward(part, lockedFinalChargeLaneX, finalAlignmentSpeed);
                UpdateFinalWarningPresentation(part, bounds, false);
                UpdateFinalSparks(part);
                yield return null;
            }

            if (!CanContinueFinalSequence(token, part))
            {
                yield break;
            }
        }
        else
        {
            MovePartHorizontallyToward(part, lockedFinalChargeLaneX, 1000f);
        }

        lockedFinalChargeLaneX = part.transform.position.x;
        finalSequenceState = SalvageDevourerFinalSequenceState.LaneLocked;
        UpdateFinalWarningPresentation(part, bounds, true);
        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, part.transform.position, 0.72f);

        float lockElapsed = 0f;
        float lockDuration = skipWarning ? 0f : Mathf.Max(0.05f, finalLaneLockDuration);
        while (lockElapsed < lockDuration && CanContinueFinalSequence(token, part))
        {
            lockElapsed += Mathf.Max(0f, Time.deltaTime);
            UpdateFinalWarningPresentation(part, bounds, true);
            UpdateFinalSparks(part);
            yield return null;
        }

        if (!CanContinueFinalSequence(token, part))
        {
            yield break;
        }

        SetWarningIconVisible(false, Vector2.zero);
        HideEncounterLine();
        ReleaseFinalSparks();
        RestoreFinalPartTint(part);
        finalSequenceState = SalvageDevourerFinalSequenceState.Charging;
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, part.transform.position, 0.92f);

        while (part.transform.position.y > finalChargeEndpointY &&
               CanContinueFinalSequence(token, part))
        {
            Vector3 position = part.transform.position;
            position.x = lockedFinalChargeLaneX;
            position.y = Mathf.MoveTowards(
                position.y,
                finalChargeEndpointY,
                finalChargeSpeed * Time.fixedDeltaTime
            );
            part.transform.position = position;

            ShowEncounterLine(
                position + Vector3.up * 0.75f,
                position + Vector3.up * 2.2f,
                finalChargeHitboxSize.x * 0.55f,
                new Color(finalWarningTint.r, finalWarningTint.g, finalWarningTint.b, 0.7f)
            );
            TryApplyFinalChargeDamage(part);
            yield return FixedUpdateYield;
        }

        if (!CanContinueFinalSequence(token, part))
        {
            yield break;
        }

        Vector3 endpoint = part.transform.position;
        endpoint.x = lockedFinalChargeLaneX;
        endpoint.y = finalChargeEndpointY;
        part.transform.position = endpoint;
        finalChargeDamageAttempted = true;
        HideEncounterLine();
        SetWarningIconVisible(false, Vector2.zero);
        ReleaseFinalSparks();
        RestoreFinalPartTint(part);
        finalSequenceState = SalvageDevourerFinalSequenceState.DeathHandoff;
        finalSequenceCompletionRequested = true;
        finalSequenceRoutine = null;

        if (bossDeathPresentation != null)
        {
            bossDeathPresentation.SetBodyRenderer(part.VisualRenderer);
            bossDeathPresentation.SetDeathPositionOverride(part.transform.position);
        }

        bool completed = CompleteDeferredBossDeath();
        finalSequenceState = completed
            ? SalvageDevourerFinalSequenceState.Completed
            : SalvageDevourerFinalSequenceState.Interrupted;
    }

    private bool CanContinueFinalSequence(int token, FrigateBossPart part)
    {
        return initialized &&
               gameplayBegun &&
               isActiveAndEnabled &&
               token == finalSequenceToken &&
               state == FrigateTriadBossState.FinalSequencePending &&
               finalPendingPart != null &&
               ReferenceEquals(finalPendingPart, part) &&
               !IsRunEnding() &&
               corridorController != null &&
               corridorController.IsRuntimeActive;
    }

    private void UpdateFinalWarningPresentation(
        FrigateBossPart part,
        Bounds bounds,
        bool locked)
    {
        if (part == null)
        {
            return;
        }

        float laneX = locked ? part.transform.position.x : lockedFinalChargeLaneX;
        Vector2 lineStart = new Vector2(laneX, part.transform.position.y - 0.25f);
        Vector2 lineEnd = new Vector2(laneX, bounds.min.y - 0.35f);
        ShowEncounterLine(
            lineStart,
            lineEnd,
            finalChargeHitboxSize.x * (locked ? 0.5f : 0.28f),
            locked ? laserLockedColor : laserWarningColor
        );
        SetWarningIconVisible(
            true,
            new Vector2(laneX, Mathf.Lerp(lineStart.y, lineEnd.y, 0.18f))
        );

        if (part.VisualRenderer != null)
        {
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 18f) * 0.5f;
            part.VisualRenderer.color = Color.Lerp(
                finalPartOriginalColor,
                finalWarningTint,
                0.35f + pulse * 0.55f
            );
        }
    }

    private void UpdateFinalSparks(FrigateBossPart part)
    {
        if (part == null || finalSparkPrefab == null || PoolManager.Instance == null)
        {
            return;
        }

        finalSparkTimer -= Mathf.Max(0f, Time.deltaTime);
        if (finalSparkTimer > 0f)
        {
            return;
        }

        finalSparkTimer = Mathf.Max(0.05f, finalSparkInterval);
        for (int i = 0; i < activeFinalSparks.Length; i++)
        {
            if (activeFinalSparks[i] != null && activeFinalSparks[i].activeSelf)
            {
                continue;
            }

            Vector2 deterministicOffset = new Vector2(
                ((i % 3) - 1) * 0.24f,
                ((i & 1) == 0 ? -0.18f : 0.18f)
            );
            activeFinalSparks[i] = PoolManager.Instance.Get(
                finalSparkPrefab,
                (Vector2)part.transform.position + deterministicOffset,
                Quaternion.identity
            );
            break;
        }
    }

    private void ReleaseFinalSparks()
    {
        for (int i = 0; i < activeFinalSparks.Length; i++)
        {
            GameObject spark = activeFinalSparks[i];
            activeFinalSparks[i] = null;
            if (spark == null || !spark.activeSelf)
            {
                continue;
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(spark);
            }
            else
            {
                spark.SetActive(false);
            }
        }
    }

    private void TryApplyFinalChargeDamage(FrigateBossPart part)
    {
        if (finalChargeDamageAttempted ||
            finalChargeMissForced ||
            part == null ||
            observedPlayerHealth == null ||
            observedPlayerHealth.IsDead ||
            observedPlayerCollider == null ||
            IsRunEnding())
        {
            return;
        }

        Bounds chargeBounds = new Bounds(
            (Vector2)part.transform.position + finalChargeHitboxOffset,
            finalChargeHitboxSize
        );
        if (!chargeBounds.Intersects(observedPlayerCollider.bounds))
        {
            return;
        }

        finalChargeDamageAttempted = true;
        observedPlayerHealth.TakeDamage(finalChargeDamage);
    }

    private void RestoreFinalPartTint(FrigateBossPart part)
    {
        if (part != null && part.VisualRenderer != null)
        {
            part.VisualRenderer.color = finalPartOriginalColor;
        }
    }

    private void CancelFinalSequence(bool resumeScroll)
    {
        finalSequenceToken++;
        if (finalSequenceRoutine != null)
        {
            StopCoroutine(finalSequenceRoutine);
            finalSequenceRoutine = null;
        }

        ReleaseActiveLaser();
        ReleaseActiveWarnings();
        ReleaseFinalSparks();
        HideEncounterLine();
        SetWarningIconVisible(false, Vector2.zero);
        RestoreFinalPartTint(finalPendingPart);
        finalChargeDamageAttempted = true;
        formationMovementPaused = false;
        Bullet.ReleaseAllActiveFromSource(transform);

        if (resumeScroll && corridorController != null &&
            corridorController.IsRuntimeActive && !IsRunEnding())
        {
            corridorController.SetScrollPaused(false);
        }

        if (finalSequenceState != SalvageDevourerFinalSequenceState.Inactive &&
            finalSequenceState != SalvageDevourerFinalSequenceState.Completed)
        {
            finalSequenceState = finalSequenceCompletionRequested
                ? SalvageDevourerFinalSequenceState.Completed
                : SalvageDevourerFinalSequenceState.Interrupted;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        CleanupBossRuntime();
    }

    private void HandlePlayerDied()
    {
        CleanupBossRuntime();
    }

    private void HandleAggregateBossDied(EnemyHealth _)
    {
        aggregateDeathInProgress = true;
        CleanupBossRuntime();
        aggregateDeathInProgress = false;
    }

    private void SetState(FrigateTriadBossState nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;
        StateChanged?.Invoke(state);
    }

    private void ResolveSerializedReferences()
    {
        if (aggregateHealth == null)
        {
            aggregateHealth = GetComponent<EnemyHealth>();
        }

        if (bossDeathPresentation == null)
        {
            bossDeathPresentation = GetComponent<BossDeathPresentation>();
        }

        if (campaignDeathAuthority == null)
        {
            campaignDeathAuthority = GetComponent<BossDummyController>();
        }

        if (bossRigidbody == null)
        {
            bossRigidbody = GetComponent<Rigidbody2D>();
        }

        if (parts == null || parts.Length != 3 || HasMissingPartReference())
        {
            parts = GetComponentsInChildren<FrigateBossPart>(true);
        }
    }

    private bool HasMissingPartReference()
    {
        if (parts == null)
        {
            return true;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasExactlyThreeValidParts()
    {
        if (parts == null || parts.Length != 3)
        {
            return false;
        }

        bool hasLeft = false;
        bool hasCenter = false;
        bool hasRight = false;

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null)
            {
                return false;
            }

            switch (part.PartId)
            {
                case FrigateBossPartId.Left:
                    if (hasLeft)
                    {
                        return false;
                    }

                    hasLeft = true;
                    break;

                case FrigateBossPartId.Center:
                    if (hasCenter)
                    {
                        return false;
                    }

                    hasCenter = true;
                    break;

                case FrigateBossPartId.Right:
                    if (hasRight)
                    {
                        return false;
                    }

                    hasRight = true;
                    break;
            }
        }

        return hasLeft && hasCenter && hasRight;
    }

    private bool OwnsPart(FrigateBossPart part)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (ReferenceEquals(parts[i], part))
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveTotalMaxHealth()
    {
        float total = 0f;

        if (parts == null)
        {
            return total;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
            {
                total += Mathf.Max(1f, parts[i].MaxHealth);
            }
        }

        return Mathf.Max(1f, total);
    }

    private void SubscribePartEvents()
    {
        if (partEventsSubscribed)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].Destroyed += HandlePartDestroyed;
            parts[i].FinalSequencePendingEntered += HandleFinalSequencePending;
        }

        partEventsSubscribed = true;
    }

    private void UnsubscribePartEvents()
    {
        if (!partEventsSubscribed || parts == null)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            part.Destroyed -= HandlePartDestroyed;
            part.FinalSequencePendingEntered -= HandleFinalSequencePending;
        }

        partEventsSubscribed = false;
    }

    private void SubscribeRunEnd()
    {
        observedRunManager = RunManager.Instance;
        if (observedRunManager == null)
        {
            return;
        }

        observedRunManager.RunEnded += HandleRunEnded;
        runEventSubscribed = true;
    }

    private void UnsubscribeRunEnd()
    {
        if (runEventSubscribed && observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
        }

        observedRunManager = null;
        runEventSubscribed = false;
    }

    private void SubscribeAggregateDeath()
    {
        if (aggregateDeathSubscribed || aggregateHealth == null)
        {
            return;
        }

        aggregateHealth.Died += HandleAggregateBossDied;
        aggregateDeathSubscribed = true;
    }

    private void UnsubscribeAggregateDeath()
    {
        if (aggregateDeathSubscribed && aggregateHealth != null)
        {
            aggregateHealth.Died -= HandleAggregateBossDied;
        }

        aggregateDeathSubscribed = false;
    }

    private void SubscribePlayerDeath()
    {
        UnsubscribePlayerDeath();
        observedPlayerHealth = FindFirstObjectByType<PlayerHealth>();
        observedPlayerRigidbody = observedPlayerHealth != null
            ? observedPlayerHealth.GetComponent<Rigidbody2D>()
            : null;
        observedPlayerCollider = observedPlayerHealth != null
            ? observedPlayerHealth.GetComponentInChildren<Collider2D>()
            : null;

        if (observedPlayerHealth == null)
        {
            return;
        }

        observedPlayerHealth.Died += HandlePlayerDied;
        playerDeathSubscribed = true;
    }

    private void UnsubscribePlayerDeath()
    {
        if (playerDeathSubscribed && observedPlayerHealth != null)
        {
            observedPlayerHealth.Died -= HandlePlayerDied;
        }

        observedPlayerHealth = null;
        observedPlayerRigidbody = null;
        observedPlayerCollider = null;
        playerDeathSubscribed = false;
    }

    private void PositionBossRootAtScrollAnchor()
    {
        if (bossRigidbody == null)
        {
            return;
        }

        Vector2 target = ResolveCurrentScrollAnchor();
        bossRigidbody.linearVelocity = Vector2.zero;
        bossRigidbody.angularVelocity = 0f;

        Vector3 position = transform.position;
        position.x = target.x;
        position.y = target.y;
        transform.position = position;
    }

    private Vector2 ResolveCurrentScrollAnchor()
    {
        Vector2 scrollCenter = corridorController != null
            ? corridorController.CurrentCameraScrollCenter
            : bossRigidbody != null
                ? bossRigidbody.position
                : (Vector2)transform.position;
        return scrollCenter + formationAnchorOffset;
    }

    private void ResolveFormationTargets(bool includeDestroyedParts)
    {
        if (parts == null || parts.Length != 3)
        {
            return;
        }

        if (alivePartCount >= 3)
        {
            SetTargetForPartId(FrigateBossPartId.Left, threeAliveLeftOffset);
            SetTargetForPartId(FrigateBossPartId.Center, threeAliveCenterOffset);
            SetTargetForPartId(FrigateBossPartId.Right, threeAliveRightOffset);
            return;
        }

        int livingIndex = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null || (!includeDestroyedParts && !part.IsAlive))
            {
                continue;
            }

            if (alivePartCount == 2)
            {
                formationTargets[i] = livingIndex == 0
                    ? twoAliveLeftOffset
                    : twoAliveRightOffset;
            }
            else if (alivePartCount == 1)
            {
                formationTargets[i] = oneAliveOffset;
            }

            livingIndex++;
        }
    }

    private void SetTargetForPartId(FrigateBossPartId partId, Vector2 target)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.PartId == partId)
            {
                formationTargets[i] = target;
                return;
            }
        }
    }

    private void SetPartsAtEntryStartOffsets()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            Vector2 startOffset = formationTargets[i] + Vector2.up * entryVerticalDistance;
            entryStartOffsets[i] = startOffset;
            part.transform.localPosition = startOffset;
        }
    }

    private void CacheEntryStartOffsets()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            entryStartOffsets[i] = parts[i] != null
                ? (Vector2)parts[i].transform.localPosition
                : Vector2.zero;
        }
    }

    private void MoveLivingPartsTowardFormationTargets()
    {
        float maximumDelta = Mathf.Max(0.01f, formationRebalanceSpeed) * Time.deltaTime;

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null || !part.IsAlive)
            {
                continue;
            }

            Vector2 current = part.transform.localPosition;
            Vector2 next = Vector2.MoveTowards(current, formationTargets[i], maximumDelta);
            part.transform.localPosition = next;
        }
    }

    private void SnapLivingPartsToFormationTargets()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.IsAlive)
            {
                part.transform.localPosition = formationTargets[i];
            }
        }
    }

    private void StartPatternScheduler(
        SalvageDevourerCombatPattern firstPattern = SalvageDevourerCombatPattern.None)
    {
        FrigateTriadBossState expectedState = ResolveCombatStateForAliveCount(alivePartCount);
        if (!gameplayBegun ||
            (expectedState != FrigateTriadBossState.ThreeAlive &&
             expectedState != FrigateTriadBossState.TwoAlive &&
             expectedState != FrigateTriadBossState.OneAlive))
        {
            return;
        }

        CancelPatternWork(false);
        SetState(expectedState);
        int token = patternToken;
        patternRoutine = StartCoroutine(PatternSchedulerRoutine(
            token,
            alivePartCount,
            firstPattern
        ));
    }

    private IEnumerator PatternSchedulerRoutine(
        int token,
        int expectedAliveCount,
        SalvageDevourerCombatPattern firstPattern)
    {
        bool useFirstPattern = firstPattern != SalvageDevourerCombatPattern.None;

        while (CanContinuePattern(token, expectedAliveCount))
        {
            SalvageDevourerCombatPattern pattern = useFirstPattern
                ? firstPattern
                : SelectNextPattern(expectedAliveCount);
            useFirstPattern = false;

            if (pattern == SalvageDevourerCombatPattern.None)
            {
                break;
            }

            activePattern = pattern;
            lastPattern = pattern;
            yield return ExecutePatternRoutine(pattern, token, expectedAliveCount);

            if (!CanContinuePattern(token, expectedAliveCount))
            {
                break;
            }

            activePattern = SalvageDevourerCombatPattern.None;
            float recovery = expectedAliveCount switch
            {
                3 => threeAliveRecovery,
                2 => twoAliveRecovery,
                _ => oneAliveRecovery
            };
            yield return WaitPatternSeconds(recovery, token, expectedAliveCount);
        }

        if (token == patternToken)
        {
            activePattern = SalvageDevourerCombatPattern.None;
            patternRoutine = null;
        }
    }

    private IEnumerator ExecutePatternRoutine(
        SalvageDevourerCombatPattern pattern,
        int token,
        int expectedAliveCount)
    {
        switch (pattern)
        {
            case SalvageDevourerCombatPattern.NWayVolley:
                yield return NWayVolleyRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.AimedFire:
                yield return AimedFireRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.WarningAreaStrike:
                yield return WarningAreaStrikeRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.LimitedHomingMissiles:
                yield return LimitedHomingMissileRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.WallRicochetBullets:
                yield return WallRicochetRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.OneAliveLaser:
                yield return OneAliveLaserRoutine(token, expectedAliveCount);
                break;

            case SalvageDevourerCombatPattern.RotatingBullets:
                yield return RotatingBulletRoutine(token, expectedAliveCount);
                break;
        }
    }

    private IEnumerator NWayVolleyRoutine(int token, int expectedAliveCount)
    {
        float spread = Mathf.Clamp(nWayTotalSpreadDegrees, 30f, 75f);

        for (int volley = 0; volley < nWayVolleyCount; volley++)
        {
            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                FrigateBossPart part = parts[partIndex];
                if (!CanPartFire(part))
                {
                    continue;
                }

                Vector2 origin = part.PrimaryFirePoint.position;
                float bias = ResolveNWayVolleyBias(nWayBiasSequence + volley);
                for (int projectileIndex = 0;
                     projectileIndex < NWayProjectilesPerFrigate;
                     projectileIndex++)
                {
                    float side = projectileIndex == 0 ? -1f : 1f;
                    Vector2 direction = RotateDirection(
                        Vector2.down,
                        bias + side * spread * 0.5f
                    );
                    Bullet bullet = SpawnBossProjectile(
                        spreadProjectilePrefab,
                        origin,
                        direction,
                        bossProjectileDefinition != null ? bossProjectileDefinition.Damage : 3f,
                        nWayProjectileSpeed,
                        bossProjectileDefinition != null ? bossProjectileDefinition.Range : 12f,
                        nWayProjectileColor
                    );
                    if (bullet != null)
                    {
                        bullet.transform.localScale =
                            spreadProjectilePrefab.transform.localScale * nWayProjectileScale;
                    }
                }
            }

            AudioManager.PlayAt(SoundEventIds.BossSpreadFire, transform.position, 0.72f);
            if (volley + 1 < nWayVolleyCount)
            {
                yield return WaitPatternSeconds(
                    nWayVolleyInterval,
                    token,
                    expectedAliveCount
                );
            }
        }

        nWayBiasSequence = (nWayBiasSequence + nWayVolleyCount) % 3;
    }

    private float ResolveNWayVolleyBias(int sequenceIndex)
    {
        float bias = Mathf.Clamp(nWayVolleyBiasDegrees, 0f, 18f);
        return (sequenceIndex % 3) switch
        {
            0 => -bias,
            1 => bias,
            _ => 0f
        };
    }

    private IEnumerator AimedFireRoutine(int token, int expectedAliveCount)
    {
        for (int burst = 0; burst < aimedBurstCount; burst++)
        {
            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            CacheLockedAimDirections();
            AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position, 0.46f);
            yield return WaitPatternSeconds(aimedLockDuration, token, expectedAliveCount);

            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            bool fired = false;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                FrigateBossPart part = parts[partIndex];
                if (!CanPartFire(part))
                {
                    continue;
                }

                SpawnBossProjectile(
                    spreadProjectilePrefab,
                    part.PrimaryFirePoint.position,
                    lockedAimDirections[partIndex],
                    bossProjectileDefinition != null ? bossProjectileDefinition.Damage : 3f,
                    bossProjectileDefinition != null ? bossProjectileDefinition.Speed : 9f,
                    bossProjectileDefinition != null ? bossProjectileDefinition.Range : 12f,
                    aimedProjectileColor
                );
                fired = true;
            }

            if (fired)
            {
                AudioManager.PlayAt(SoundEventIds.BossSpreadFire, transform.position, 0.68f);
            }

            if (burst + 1 < aimedBurstCount)
            {
                yield return WaitPatternSeconds(
                    aimedBurstInterval,
                    token,
                    expectedAliveCount
                );
            }
        }
    }

    private IEnumerator WarningAreaStrikeRoutine(int token, int expectedAliveCount)
    {
        if (!TryBuildWarningPositions(out int positionCount) ||
            !SpawnWarningAreas(positionCount))
        {
            yield break;
        }

        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, transform.position, 0.8f);
        float duration = Mathf.Max(0.1f, warningAreaDuration);
        float elapsed = 0f;

        while (elapsed < duration && CanContinuePattern(token, expectedAliveCount))
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < activeWarnings.Length; i++)
            {
                activeWarnings[i]?.SetWarningProgress(progress);
            }

            yield return null;
        }

        if (!CanContinuePattern(token, expectedAliveCount))
        {
            ReleaseActiveWarnings();
            yield break;
        }

        AudioManager.PlayAt(SoundEventIds.BossChargeFire, transform.position, 0.78f);
        for (int i = 0; i < activeWarnings.Length; i++)
        {
            activeWarnings[i]?.Detonate();
        }

        GungeonStyleCamera2D.RequestShake(0.08f, 0.14f);
        ReleaseActiveWarnings();
    }

    private IEnumerator LimitedHomingMissileRoutine(int token, int expectedAliveCount)
    {
        for (int salvo = 0; salvo < missilesPerFrigate; salvo++)
        {
            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            bool fired = false;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                FrigateBossPart part = parts[partIndex];
                if (!CanPartFire(part))
                {
                    continue;
                }

                float side = part.PrimaryFirePoint.position.x < CurrentScrollAnchor.x ? -1f : 1f;
                float launchDegrees = side * missileLaunchAngle * (salvo == 0 ? 1f : 0.45f);
                Bullet missile = SpawnBossProjectile(
                    missileProjectilePrefab,
                    part.PrimaryFirePoint.position,
                    RotateDirection(Vector2.down, launchDegrees),
                    missileDamage,
                    missileSpeed,
                    missileRange,
                    missileProjectileColor
                );

                if (missile == null)
                {
                    continue;
                }

                Transform playerTarget = observedPlayerHealth != null &&
                                         !observedPlayerHealth.IsDead
                    ? observedPlayerHealth.transform
                    : null;
                missile.ConfigureTimedHoming(
                    playerTarget,
                    missileTurnRate,
                    missileHomingRange,
                    false,
                    missileHomingDuration
                );
                fired = true;
            }

            if (fired)
            {
                AudioManager.PlayAt(SoundEventIds.EnemyChargerFire, transform.position, 0.68f);
            }

            if (salvo + 1 < missilesPerFrigate)
            {
                yield return WaitPatternSeconds(
                    missileLaunchInterval,
                    token,
                    expectedAliveCount
                );
            }
        }
    }

    private IEnumerator WallRicochetRoutine(int token, int expectedAliveCount)
    {
        for (int shot = 0; shot < ricochetShotsPerFrigate; shot++)
        {
            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            bool fired = false;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                FrigateBossPart part = parts[partIndex];
                if (!CanPartFire(part))
                {
                    continue;
                }

                float inwardSign = part.PrimaryFirePoint.position.x <= CurrentScrollAnchor.x
                    ? 1f
                    : -1f;
                float alternatingOffset = (shot & 1) == 0
                    ? -ricochetAngleVariationDegrees
                    : ricochetAngleVariationDegrees;
                float identityOffset = ((int)part.PartId - 1) * 2f;
                float launchAngle = inwardSign * Mathf.Clamp(
                    ricochetBaseAngleDegrees + alternatingOffset + identityOffset,
                    18f,
                    72f
                );
                Vector2 direction = RotateDirection(Vector2.down, launchAngle);
                Bullet bullet = SpawnBossProjectile(
                    spreadProjectilePrefab,
                    part.PrimaryFirePoint.position,
                    direction,
                    ricochetDamage,
                    ricochetSpeed,
                    ricochetRange,
                    ricochetProjectileColor
                );
                SalvageDevourerRicochetProjectile ricochet = bullet != null
                    ? bullet.GetComponent<SalvageDevourerRicochetProjectile>()
                    : null;

                if (ricochet == null)
                {
                    bullet?.ForceRelease(false);
                    LogMissingPatternAsset(
                        "The Boss spread projectile prefab requires SalvageDevourerRicochetProjectile."
                    );
                    continue;
                }

                ricochet.Configure(ricochetMaximumBounces);
                fired = true;
            }

            if (fired)
            {
                AudioManager.PlayAt(SoundEventIds.BossSpreadFire, transform.position, 0.58f);
            }

            if (shot + 1 < ricochetShotsPerFrigate)
            {
                yield return WaitPatternSeconds(
                    ricochetShotInterval,
                    token,
                    expectedAliveCount
                );
            }
        }
    }

    private IEnumerator OneAliveLaserRoutine(int token, int expectedAliveCount)
    {
        FrigateBossPart part = GetOnlyLivingPart();
        if (!CanPartFire(part) || !TryGetCombatViewportBounds(out Bounds bounds))
        {
            yield break;
        }

        formationMovementPaused = true;
        float halfWidth = Mathf.Max(0.2f, laserWidth * 0.5f);
        float targetX = observedPlayerHealth != null
            ? observedPlayerHealth.transform.position.x
            : part.PrimaryFirePoint.position.x;
        lockedPatternLaneX = Mathf.Clamp(
            targetX,
            bounds.min.x + halfWidth + 0.35f,
            bounds.max.x - halfWidth - 0.35f
        );

        Vector2 lineStart = new Vector2(lockedPatternLaneX, part.PrimaryFirePoint.position.y);
        Vector2 lineEnd = new Vector2(lockedPatternLaneX, bounds.min.y - 0.35f);
        ShowEncounterLine(lineStart, lineEnd, laserWidth * 0.18f, laserWarningColor);
        SetWarningIconVisible(true, new Vector2(
            lockedPatternLaneX,
            Mathf.Lerp(lineStart.y, lineEnd.y, 0.22f)
        ));
        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, part.transform.position, 0.78f);

        float elapsed = 0f;
        float warning = Mathf.Max(0.1f, laserWarningDuration);
        while (elapsed < warning && CanContinuePattern(token, expectedAliveCount))
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            MovePartHorizontallyToward(part, lockedPatternLaneX, oneAliveAlignmentSpeed);
            if (TryGetCombatViewportBounds(out bounds))
            {
                lineStart = new Vector2(
                    lockedPatternLaneX,
                    part.PrimaryFirePoint.position.y
                );
                lineEnd = new Vector2(lockedPatternLaneX, bounds.min.y - 0.35f);
                ShowEncounterLine(
                    lineStart,
                    lineEnd,
                    laserWidth * 0.18f,
                    laserWarningColor
                );
                SetWarningIconVisible(true, new Vector2(
                    lockedPatternLaneX,
                    Mathf.Lerp(lineStart.y, lineEnd.y, 0.22f)
                ));
            }
            yield return null;
        }

        if (!CanContinuePattern(token, expectedAliveCount))
        {
            yield break;
        }

        ShowEncounterLine(lineStart, lineEnd, laserWidth * 0.28f, laserLockedColor);
        float lockElapsed = 0f;
        while (lockElapsed < laserLockDuration &&
               CanContinuePattern(token, expectedAliveCount))
        {
            lockElapsed += Mathf.Max(0f, Time.deltaTime);
            if (TryGetCombatViewportBounds(out bounds))
            {
                lineStart = new Vector2(
                    lockedPatternLaneX,
                    part.PrimaryFirePoint.position.y
                );
                lineEnd = new Vector2(lockedPatternLaneX, bounds.min.y - 0.35f);
                ShowEncounterLine(
                    lineStart,
                    lineEnd,
                    laserWidth * 0.28f,
                    laserLockedColor
                );
                SetWarningIconVisible(true, new Vector2(
                    lockedPatternLaneX,
                    Mathf.Lerp(lineStart.y, lineEnd.y, 0.22f)
                ));
            }
            yield return null;
        }

        if (!CanContinuePattern(token, expectedAliveCount))
        {
            yield break;
        }

        SetWarningIconVisible(false, Vector2.zero);
        HideEncounterLine();
        if (TryGetCombatViewportBounds(out bounds))
        {
            lineStart = new Vector2(lockedPatternLaneX, part.PrimaryFirePoint.position.y);
            lineEnd = new Vector2(lockedPatternLaneX, bounds.min.y - 0.35f);
        }
        SpawnLaserHazard(lineStart, lineEnd);
        AudioManager.PlayAt(SoundEventIds.BossLaserLoop, part.transform.position, 0.68f);
        yield return WaitPatternSeconds(laserDuration, token, expectedAliveCount);
        ReleaseActiveLaser();
        formationMovementPaused = false;
    }

    private IEnumerator RotatingBulletRoutine(int token, int expectedAliveCount)
    {
        int bulletCount = Mathf.Clamp(rotatingBulletsPerVolley, 6, 8);
        int volleyCount = Mathf.Clamp(rotatingVolleyCount, 3, 4);
        float sector = Mathf.Clamp(rotatingSectorDegrees, 90f, 170f);
        float projectileStep = bulletCount > 1 ? sector / (bulletCount - 1) : 0f;

        for (int volley = 0; volley < volleyCount; volley++)
        {
            if (!CanContinuePattern(token, expectedAliveCount))
            {
                yield break;
            }

            FrigateBossPart part = GetOnlyLivingPart();
            if (!CanPartFire(part))
            {
                yield break;
            }

            float volleyOffset =
                (volley - (volleyCount - 1) * 0.5f) * rotatingVolleyStepDegrees;
            float startAngle = -sector * 0.5f + volleyOffset;
            Vector2 origin = part.PrimaryFirePoint.position;

            for (int projectileIndex = 0; projectileIndex < bulletCount; projectileIndex++)
            {
                Vector2 direction = RotateDirection(
                    Vector2.down,
                    startAngle + projectileStep * projectileIndex
                );
                SpawnBossProjectile(
                    spreadProjectilePrefab,
                    origin,
                    direction,
                    bossProjectileDefinition != null ? bossProjectileDefinition.Damage : 3f,
                    bossProjectileDefinition != null ? bossProjectileDefinition.Speed : 9f,
                    bossProjectileDefinition != null ? bossProjectileDefinition.Range : 12f,
                    rotatingProjectileColor
                );
            }

            AudioManager.PlayAt(SoundEventIds.BossSpreadFire, part.transform.position, 0.66f);
            if (volley + 1 < volleyCount)
            {
                yield return WaitPatternSeconds(
                    rotatingVolleyInterval,
                    token,
                    expectedAliveCount
                );
            }
        }
    }

    private void SpawnLaserHazard(Vector2 lineStart, Vector2 lineEnd)
    {
        ReleaseActiveLaser();
        if (laserHazardPrefab == null || PoolManager.Instance == null)
        {
            LogMissingPatternAsset(
                laserHazardPrefab == null
                    ? "The Salvage Devourer laser-hazard prefab is not assigned."
                    : "The Expedition scene PoolManager is unavailable for the laser hazard."
            );
            return;
        }

        GameObject hazardObject = PoolManager.Instance.Get(
            laserHazardPrefab.gameObject,
            Vector3.zero,
            Quaternion.identity
        );
        activeLaserHazard = hazardObject != null
            ? hazardObject.GetComponent<BossLaserHazard>()
            : null;

        if (activeLaserHazard == null)
        {
            if (hazardObject != null)
            {
                PoolManager.Instance.Release(hazardObject);
            }

            LogMissingPatternAsset(
                "The configured Salvage Devourer laser prefab has no BossLaserHazard component."
            );
            return;
        }

        activeLaserHazard.InitializeBetween(
            lineStart,
            lineEnd,
            laserWidth,
            laserDuration,
            laserDamage,
            laserDamageInterval,
            laserLineMaterial,
            laserActiveColor,
            "Effect",
            76,
            0.08f
        );
    }

    private void ReleaseActiveLaser()
    {
        BossLaserHazard hazard = activeLaserHazard;
        activeLaserHazard = null;
        if (hazard != null && hazard.gameObject.activeSelf)
        {
            hazard.Deactivate();
        }
    }

    private FrigateBossPart GetOnlyLivingPart()
    {
        if (parts == null || alivePartCount != 1)
        {
            return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null && parts[i].IsAlive)
            {
                return parts[i];
            }
        }

        return null;
    }

    private bool TryGetCombatViewportBounds(out Bounds bounds)
    {
        bounds = default;
        return corridorController != null &&
               corridorController.TryGetCurrentCombatViewportBounds(out bounds);
    }

    private void MovePartHorizontallyToward(
        FrigateBossPart part,
        float targetWorldX,
        float speed)
    {
        if (part == null)
        {
            return;
        }

        Vector3 localPosition = part.transform.localPosition;
        float targetLocalX = targetWorldX - transform.position.x;
        localPosition.x = Mathf.MoveTowards(
            localPosition.x,
            targetLocalX,
            Mathf.Max(0.1f, speed) * Mathf.Max(0f, Time.deltaTime)
        );
        part.transform.localPosition = localPosition;
    }

    private void ShowEncounterLine(
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        EnsureEncounterPresentationObjects();
        if (encounterLineRenderer == null)
        {
            return;
        }

        encounterLineRenderer.enabled = true;
        encounterLineRenderer.positionCount = 2;
        encounterLineRenderer.SetPosition(0, start);
        encounterLineRenderer.SetPosition(1, end);
        encounterLineRenderer.startWidth = Mathf.Max(0.02f, width);
        encounterLineRenderer.endWidth = Mathf.Max(0.02f, width);
        encounterLineRenderer.startColor = color;
        encounterLineRenderer.endColor = color;
    }

    private void HideEncounterLine()
    {
        if (encounterLineRenderer != null)
        {
            encounterLineRenderer.enabled = false;
        }
    }

    private void SetWarningIconVisible(bool visible, Vector2 worldPosition)
    {
        if (visible)
        {
            EnsureEncounterPresentationObjects();
        }

        if (encounterWarningIcon == null)
        {
            return;
        }

        encounterWarningIcon.enabled = visible;
        if (visible)
        {
            encounterWarningIcon.transform.position = worldPosition;
        }
    }

    private void EnsureEncounterPresentationObjects()
    {
        if (encounterLineRenderer == null)
        {
            GameObject lineObject = new GameObject("Salvage Devourer Lane Telegraph");
            lineObject.transform.SetParent(transform, false);
            encounterLineRenderer = lineObject.AddComponent<LineRenderer>();
            encounterLineRenderer.useWorldSpace = true;
            encounterLineRenderer.textureMode = LineTextureMode.Stretch;
            encounterLineRenderer.numCapVertices = 0;
            encounterLineRenderer.numCornerVertices = 0;
            encounterLineRenderer.sortingLayerName = "Effect";
            encounterLineRenderer.sortingOrder = 75;
            encounterLineRenderer.sharedMaterial = laserLineMaterial;
            encounterLineRenderer.enabled = false;
        }

        if (encounterWarningIcon == null && warningIconSprite != null)
        {
            GameObject iconObject = new GameObject("Salvage Devourer Warning Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localScale = Vector3.one * 0.13f;
            encounterWarningIcon = iconObject.AddComponent<SpriteRenderer>();
            encounterWarningIcon.sprite = warningIconSprite;
            encounterWarningIcon.color = Color.white;
            encounterWarningIcon.sortingLayerName = "Effect";
            encounterWarningIcon.sortingOrder = 78;
            encounterWarningIcon.enabled = false;
        }
    }

    private void CacheLockedAimDirections()
    {
        Vector2 playerPosition = observedPlayerHealth != null
            ? (Vector2)observedPlayerHealth.transform.position
            : CurrentScrollAnchor + Vector2.down * 4f;
        Vector2 playerVelocity = observedPlayerRigidbody != null
            ? observedPlayerRigidbody.linearVelocity
            : Vector2.zero;
        Vector2 lead = Vector2.ClampMagnitude(
            playerVelocity * aimedPredictionTime,
            aimedMaximumLeadDistance
        );

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (!CanPartFire(part))
            {
                lockedAimDirections[i] = Vector2.down;
                continue;
            }

            float variation = ((int)part.PartId - 1) * aimedHorizontalVariation;
            Vector2 target = playerPosition + lead + Vector2.right * variation;
            Vector2 direction = target - (Vector2)part.PrimaryFirePoint.position;
            lockedAimDirections[i] = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.down;
        }
    }

    private Bullet SpawnBossProjectile(
        GameObject projectilePrefab,
        Vector2 origin,
        Vector2 direction,
        float damage,
        float speed,
        float range,
        Color projectileColor)
    {
        if (projectilePrefab == null || PoolManager.Instance == null)
        {
            LogMissingPatternAsset(
                projectilePrefab == null
                    ? "A required Boss projectile prefab is not assigned."
                    : "The Expedition scene PoolManager is unavailable."
            );
            return null;
        }

        GameObject projectileObject = PoolManager.Instance.Get(
            projectilePrefab,
            origin,
            Quaternion.identity
        );
        if (projectileObject != null)
        {
            projectileObject.transform.localScale = projectilePrefab.transform.localScale;
        }

        Bullet bullet = projectileObject != null
            ? projectileObject.GetComponent<Bullet>()
            : null;

        if (bullet == null)
        {
            if (projectileObject != null)
            {
                PoolManager.Instance.Release(projectileObject);
            }

            LogMissingPatternAsset("A configured Boss projectile prefab has no Bullet component.");
            return null;
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Enemy,
            bossProjectileDefinition,
            Mathf.Max(0f, damage),
            Mathf.Max(0.1f, speed),
            Mathf.Max(0.1f, range),
            0,
            0f,
            0f,
            1f,
            false,
            gameObject,
            1f
        );
        bullet.ConfigureProjectileColor(projectileColor);
        return bullet;
    }

    private bool TryBuildWarningPositions(out int positionCount)
    {
        positionCount = 0;
        if (corridorController == null ||
            !corridorController.TryGetConstrainedPlayerViewportBounds(out Bounds bounds))
        {
            return false;
        }

        float radius = Mathf.Max(0.1f, warningAreaRadius);
        float minimumX = bounds.min.x + radius + 0.2f;
        float maximumX = bounds.max.x - radius - 0.2f;
        float minimumY = bounds.min.y + radius + 0.2f;
        float maximumY = bounds.max.y - radius - 0.35f;
        if (minimumX >= maximumX || minimumY >= maximumY)
        {
            return false;
        }

        int requestedCount = Mathf.Clamp(warningAreaCount, 2, activeWarnings.Length);
        Vector2 playerPosition = observedPlayerHealth != null
            ? (Vector2)observedPlayerHealth.transform.position
            : bounds.center;
        warningPositions[0] = new Vector2(
            Mathf.Clamp(playerPosition.x, minimumX, maximumX),
            Mathf.Clamp(playerPosition.y + radius * 0.7f, minimumY, maximumY)
        );
        positionCount = 1;

        const int MaximumCandidateAttempts = 12;
        for (int zone = 1; zone < requestedCount; zone++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < MaximumCandidateAttempts; attempt++)
            {
                int sequence = warningPlacementSequence + zone * 3 + attempt;
                Vector2 candidate = new Vector2(
                    ResolveWarningCandidateX(sequence, minimumX, maximumX),
                    ResolveWarningCandidateY(sequence, minimumY, maximumY)
                );

                if (!IsWarningPositionSeparated(candidate, positionCount))
                {
                    continue;
                }

                warningPositions[positionCount] = candidate;
                positionCount++;
                placed = true;
                break;
            }

            if (!placed)
            {
                break;
            }
        }

        warningPlacementSequence = (warningPlacementSequence + 1) % 12;
        while (positionCount >= 2 && !HasWarningEscapeLane(bounds, positionCount))
        {
            positionCount--;
        }

        return positionCount >= 2;
    }

    private bool SpawnWarningAreas(int positionCount)
    {
        ReleaseActiveWarnings();
        if (warningAreaPrefab == null || PoolManager.Instance == null)
        {
            LogMissingPatternAsset(
                warningAreaPrefab == null
                    ? "The Salvage Devourer warning-area prefab is not assigned."
                    : "The Expedition scene PoolManager is unavailable for warning areas."
            );
            return false;
        }

        for (int i = 0; i < positionCount; i++)
        {
            GameObject warningObject = PoolManager.Instance.Get(
                warningAreaPrefab.gameObject,
                warningPositions[i],
                Quaternion.identity
            );
            SalvageDevourerWarningArea warning = warningObject != null
                ? warningObject.GetComponent<SalvageDevourerWarningArea>()
                : null;

            if (warning == null)
            {
                if (warningObject != null)
                {
                    PoolManager.Instance.Release(warningObject);
                }

                ReleaseActiveWarnings();
                LogMissingPatternAsset(
                    "The configured warning-area prefab has no SalvageDevourerWarningArea component."
                );
                return false;
            }

            activeWarnings[i] = warning;
            warning.BeginWarning(
                warningPositions[i],
                warningAreaRadius,
                warningAreaDamage,
                observedPlayerHealth
            );
        }

        return true;
    }

    private void ReleaseActiveWarnings()
    {
        for (int i = 0; i < activeWarnings.Length; i++)
        {
            SalvageDevourerWarningArea warning = activeWarnings[i];
            activeWarnings[i] = null;
            if (warning == null || !warning.gameObject.activeSelf)
            {
                continue;
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(warning.gameObject);
            }
            else
            {
                warning.gameObject.SetActive(false);
            }
        }
    }

    private float ResolveWarningCandidateX(int sequence, float minimum, float maximum)
    {
        float normalized = sequence % 6 switch
        {
            0 => 0.12f,
            1 => 0.88f,
            2 => 0.32f,
            3 => 0.68f,
            4 => 0.5f,
            _ => 0.22f
        };
        return Mathf.Lerp(minimum, maximum, normalized);
    }

    private static float ResolveWarningCandidateY(int sequence, float minimum, float maximum)
    {
        float normalized = sequence % 4 switch
        {
            0 => 0.24f,
            1 => 0.58f,
            2 => 0.4f,
            _ => 0.72f
        };
        return Mathf.Lerp(minimum, maximum, normalized);
    }

    private bool IsWarningPositionSeparated(Vector2 candidate, int existingCount)
    {
        float minimumSeparation = Mathf.Max(
            warningAreaRadius * 2f,
            warningAreaMinimumHorizontalSeparation
        );

        for (int i = 0; i < existingCount; i++)
        {
            if (Mathf.Abs(candidate.x - warningPositions[i].x) < minimumSeparation)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasWarningEscapeLane(Bounds bounds, int positionCount)
    {
        for (int i = 0; i < positionCount - 1; i++)
        {
            for (int j = i + 1; j < positionCount; j++)
            {
                if (warningPositions[j].x < warningPositions[i].x)
                {
                    (warningPositions[i], warningPositions[j]) =
                        (warningPositions[j], warningPositions[i]);
                }
            }
        }

        float cursor = bounds.min.x;
        float radius = Mathf.Max(0.1f, warningAreaRadius);
        float requiredWidth = Mathf.Max(0.1f, warningAreaEscapeLaneWidth);

        for (int i = 0; i < positionCount; i++)
        {
            float coveredMinimum = warningPositions[i].x - radius;
            if (coveredMinimum - cursor >= requiredWidth)
            {
                return true;
            }

            cursor = Mathf.Max(cursor, warningPositions[i].x + radius);
        }

        return bounds.max.x - cursor >= requiredWidth;
    }

    private void RequestAliveCountTransition()
    {
        if (!gameplayBegun ||
            state == FrigateTriadBossState.FinalSequencePending ||
            state == FrigateTriadBossState.Dead)
        {
            return;
        }

        CancelPatternWork(alivePartCount <= 1);
        SetState(FrigateTriadBossState.Transition);
        int token = patternToken;
        patternRoutine = StartCoroutine(AliveCountTransitionRoutine(token, alivePartCount));
    }

    private IEnumerator AliveCountTransitionRoutine(int token, int expectedAliveCount)
    {
        float elapsed = 0f;
        float delay = Mathf.Max(0.05f, aliveCountTransitionDelay);

        while (elapsed < delay)
        {
            if (!CanContinueTransition(token, expectedAliveCount))
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.deltaTime);
            yield return null;
        }

        if (!CanContinueTransition(token, expectedAliveCount))
        {
            yield break;
        }

        FrigateTriadBossState nextState = ResolveCombatStateForAliveCount(expectedAliveCount);
        SetState(nextState);
        patternRoutine = null;

        if (nextState == FrigateTriadBossState.ThreeAlive ||
            nextState == FrigateTriadBossState.TwoAlive ||
            nextState == FrigateTriadBossState.OneAlive)
        {
            StartPatternScheduler();
        }
    }

    private IEnumerator WaitPatternSeconds(float duration, int token, int expectedAliveCount)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration && CanContinuePattern(token, expectedAliveCount))
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            yield return null;
        }
    }

    private void CancelPatternWork(bool releaseEncounterProjectiles)
    {
        patternToken++;

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        activePattern = SalvageDevourerCombatPattern.None;
        ReleaseActiveWarnings();
        ReleaseActiveLaser();
        HideEncounterLine();

        if (state != FrigateTriadBossState.FinalSequencePending)
        {
            formationMovementPaused = false;
            SetWarningIconVisible(false, Vector2.zero);
        }

        if (releaseEncounterProjectiles)
        {
            Bullet.ReleaseAllActiveFromSource(transform);
        }
    }

    private SalvageDevourerCombatPattern SelectNextPattern(int expectedAliveCount)
    {
        int optionCount = expectedAliveCount == 3 ? 3 :
            expectedAliveCount == 2 ? 3 :
            expectedAliveCount == 1 ? 2 : 0;
        for (int attempt = 0; attempt < optionCount; attempt++)
        {
            SalvageDevourerCombatPattern candidate;
            if (expectedAliveCount == 3)
            {
                candidate = (nextThreeAlivePatternIndex % 3) switch
                {
                    0 => SalvageDevourerCombatPattern.NWayVolley,
                    1 => SalvageDevourerCombatPattern.AimedFire,
                    _ => SalvageDevourerCombatPattern.WarningAreaStrike
                };
                nextThreeAlivePatternIndex = (nextThreeAlivePatternIndex + 1) % 3;
            }
            else if (expectedAliveCount == 2)
            {
                candidate = (nextTwoAlivePatternIndex % 3) switch
                {
                    0 => SalvageDevourerCombatPattern.LimitedHomingMissiles,
                    1 => SalvageDevourerCombatPattern.AimedFire,
                    _ => SalvageDevourerCombatPattern.WallRicochetBullets
                };
                nextTwoAlivePatternIndex = (nextTwoAlivePatternIndex + 1) % 3;
            }
            else
            {
                candidate = nextOneAlivePatternIndex % 2 == 0
                    ? SalvageDevourerCombatPattern.OneAliveLaser
                    : SalvageDevourerCombatPattern.RotatingBullets;
                nextOneAlivePatternIndex = (nextOneAlivePatternIndex + 1) % 2;
            }

            if (candidate != lastPattern)
            {
                return candidate;
            }
        }

        return SalvageDevourerCombatPattern.None;
    }

    private bool CanContinuePattern(int token, int expectedAliveCount)
    {
        FrigateTriadBossState expectedState = ResolveCombatStateForAliveCount(expectedAliveCount);
        return initialized &&
               gameplayBegun &&
               isActiveAndEnabled &&
               token == patternToken &&
               alivePartCount == expectedAliveCount &&
               state == expectedState &&
               !IsRunEnding() &&
               corridorController != null &&
               corridorController.IsRuntimeActive;
    }

    private bool CanContinueTransition(int token, int expectedAliveCount)
    {
        return initialized &&
               gameplayBegun &&
               isActiveAndEnabled &&
               token == patternToken &&
               alivePartCount == expectedAliveCount &&
               state == FrigateTriadBossState.Transition &&
               !IsRunEnding() &&
               corridorController != null &&
               corridorController.IsRuntimeActive;
    }

    private static FrigateTriadBossState ResolveCombatStateForAliveCount(int count)
    {
        return count switch
        {
            >= 3 => FrigateTriadBossState.ThreeAlive,
            2 => FrigateTriadBossState.TwoAlive,
            1 => FrigateTriadBossState.OneAlive,
            _ => FrigateTriadBossState.Dead
        };
    }

    private static bool CanPartFire(FrigateBossPart part)
    {
        return part != null && part.IsAlive && part.CanParticipateInPatterns;
    }

    private void EnterBossFightUiMode()
    {
        if (bossFightUiModeActive)
        {
            return;
        }

        if (observedPlayerHealth != null)
        {
            bossFightRadarScanner = observedPlayerHealth.GetComponent<PlayerRadarScanner>();
        }

        bossFightMenuController = FindFirstObjectByType<ExpeditionMenuController>(
            FindObjectsInactive.Include
        );
        bossFightHud = FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        bossFightRadarPanel = FindFirstObjectByType<RadarPanelAnimator>(
            FindObjectsInactive.Include
        );
        bossFightHealthBar = BossHealthBarUI.Instance;

        bossFightMenuController?.SetExternalOpenLocked(this, true);
        bossFightRadarScanner?.CloseRadar();
        bossFightRadarScanner?.SetExternalInputLocked(this, true);
        bossFightRadarPanel?.SetPresentationSuppressed(this, true);
        bossFightHud?.SetMenuHintsSuppressed(this, true);
        bossFightHealthBar?.SetSalvageDevourerTriadPresentation(this, this, true);
        bossFightUiModeActive = true;
    }

    private void ExitBossFightUiMode()
    {
        if (!bossFightUiModeActive)
        {
            return;
        }

        bossFightMenuController?.SetExternalOpenLocked(this, false);
        bossFightRadarScanner?.SetExternalInputLocked(this, false);
        bossFightRadarPanel?.SetPresentationSuppressed(this, false);
        bossFightHud?.SetMenuHintsSuppressed(this, false);
        bossFightHealthBar?.SetSalvageDevourerTriadPresentation(this, this, false);
        bossFightMenuController = null;
        bossFightRadarScanner = null;
        bossFightRadarPanel = null;
        bossFightHud = null;
        bossFightHealthBar = null;
        bossFightUiModeActive = false;
    }

    private static Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine
        ).normalized;
    }

    private void LogMissingPatternAsset(string message)
    {
        if (missingPatternAssetWarningLogged)
        {
            return;
        }

        missingPatternAssetWarningLogged = true;
        Debug.LogWarning($"Salvage Devourer pattern setup: {message}", this);
    }

    private bool IsRunEnding()
    {
        return RunManager.Instance != null && RunManager.Instance.IsCompletingRun;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Patterns/Force Three Alive State")]
    private void DevelopmentForceThreeAliveState()
    {
        DevelopmentForcePatternState(3);
    }

    [ContextMenu("Development/Patterns/Force N-Way Volley")]
    private void DevelopmentForceNWayVolley()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.NWayVolley, 3);
    }

    [ContextMenu("Development/Patterns/Force Aimed Fire")]
    private void DevelopmentForceAimedFire()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.AimedFire, 3);
    }

    [ContextMenu("Development/Patterns/Force Warning Area Strike")]
    private void DevelopmentForceWarningAreaStrike()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.WarningAreaStrike, 3);
    }

    [ContextMenu("Development/Patterns/Force Two Alive State")]
    private void DevelopmentForceTwoAliveState()
    {
        if (alivePartCount == 3)
        {
            DevelopmentDestroyFirstLivingPart();
            return;
        }

        DevelopmentForcePatternState(2);
    }

    [ContextMenu("Development/Patterns/Force Limited-Homing Missiles")]
    private void DevelopmentForceMissiles()
    {
        DevelopmentForcePattern(
            SalvageDevourerCombatPattern.LimitedHomingMissiles,
            2
        );
    }

    [ContextMenu("Development/Patterns/Force Two Alive Aimed Fire")]
    private void DevelopmentForceTwoAliveAimedFire()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.AimedFire, 2);
    }

    [ContextMenu("Development/Patterns/Force Wall Ricochet Bullets")]
    private void DevelopmentForceRicochetBullets()
    {
        DevelopmentForcePattern(
            SalvageDevourerCombatPattern.WallRicochetBullets,
            2
        );
    }

    [ContextMenu("Development/Patterns/Force One Alive State")]
    private void DevelopmentForceOneAliveState()
    {
        if (!initialized || !gameplayBegun)
        {
            Debug.LogWarning(
                "OneAlive controls require the live post-BeginGameplay encounter.",
                this
            );
            return;
        }

        while (alivePartCount > 1)
        {
            DevelopmentDestroyFirstLivingPart();
        }
    }

    [ContextMenu("Development/Patterns/Force One Alive Laser")]
    private void DevelopmentForceOneAliveLaser()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.OneAliveLaser, 1);
    }

    [ContextMenu("Development/Patterns/Force Rotating Bullets")]
    private void DevelopmentForceRotatingBullets()
    {
        DevelopmentForcePattern(SalvageDevourerCombatPattern.RotatingBullets, 1);
    }

    [ContextMenu("Development/Patterns/Cancel One Alive Pattern")]
    private void DevelopmentCancelOneAlivePattern()
    {
        if (alivePartCount != 1 ||
            state == FrigateTriadBossState.FinalSequencePending)
        {
            return;
        }

        CancelPatternWork(false);
        SetState(FrigateTriadBossState.OneAlive);
    }

    [ContextMenu("Development/Patterns/Cancel Current Pattern")]
    private void DevelopmentCancelCurrentPattern()
    {
        CancelPatternWork(false);
        if (gameplayBegun)
        {
            SetState(ResolveCombatStateForAliveCount(alivePartCount));
        }
    }

    [ContextMenu("Development/Patterns/Log Current Pattern State")]
    private void DevelopmentLogPatternState()
    {
        Debug.Log(
            $"[SalvageDevourer/Pattern] state={state} pattern={activePattern} " +
            $"last={lastPattern} alive={alivePartCount} token={patternToken} " +
            $"gameplayBegun={gameplayBegun}",
            this
        );
    }

    [ContextMenu("Development/Patterns/Destroy One And Test Transition")]
    private void DevelopmentDestroyOneAndTestTransition()
    {
        DevelopmentDestroyFirstLivingPart();
    }

    private void DevelopmentForcePatternState(int requiredAliveCount)
    {
        if (!CanUseDevelopmentPatternControls(requiredAliveCount))
        {
            return;
        }

        StartPatternScheduler();
    }

    private void DevelopmentForcePattern(
        SalvageDevourerCombatPattern pattern,
        int requiredAliveCount)
    {
        if (!CanUseDevelopmentPatternControls(requiredAliveCount))
        {
            return;
        }

        StartPatternScheduler(pattern);
    }

    private bool CanUseDevelopmentPatternControls(int requiredAliveCount)
    {
        if (!initialized || !gameplayBegun || corridorController == null ||
            !corridorController.IsRuntimeActive)
        {
            Debug.LogWarning(
                "Salvage Devourer pattern controls require the live post-BeginGameplay encounter.",
                this
            );
            return false;
        }

        if (alivePartCount != requiredAliveCount)
        {
            Debug.LogWarning(
                $"This pattern control requires {requiredAliveCount} living Frigate(s); " +
                $"the encounter currently has {alivePartCount}.",
                this
            );
            return false;
        }

        return true;
    }

    private void DevelopmentDestroyFirstLivingPart()
    {
        if (!initialized || !gameplayBegun || alivePartCount <= 1)
        {
            Debug.LogWarning(
                "A live post-BeginGameplay encounter with at least two Frigates is required.",
                this
            );
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null || !part.IsAlive)
            {
                continue;
            }

            TryApplyPartDamage(
                part,
                Mathf.Max(DeferredHealthFloor, part.CurrentHealth),
                part.transform.position,
                Vector2.zero
            );
            return;
        }
    }

    [ContextMenu("Development/Skip Frigate Entry")]
    private void DevelopmentSkipFrigateEntry()
    {
        if (!initialized)
        {
            return;
        }

        entrySkipRequested = true;
        ResolveFormationTargets(false);
        SnapLivingPartsToFormationTargets();
        entryPlaying = false;
        entryComplete = true;
    }

    [ContextMenu("Development/Start Scroll After Formation")]
    private void DevelopmentStartScrollAfterFormation()
    {
        if (!entryComplete || corridorController == null)
        {
            return;
        }

        BeginFormationFollowing();
        BeginGameplay();
        corridorController.BeginScroll();
    }

    [ContextMenu("Development/Force Camera Terminal")]
    private void DevelopmentForceCameraTerminal()
    {
        corridorController?.ForceCameraToTerminal();
    }

    [ContextMenu("Development/Patterns/Fire Ricochet Test At Terminal")]
    private void DevelopmentFireRicochetTestAtTerminal()
    {
        if (corridorController == null || !corridorController.ForceCameraToTerminal())
        {
            return;
        }

        DevelopmentForcePattern(
            SalvageDevourerCombatPattern.WallRicochetBullets,
            2
        );
    }

    [ContextMenu("Development/Transition/Force Non-Final Charge Endpoint")]
    private void DevelopmentForceNonFinalChargeEndpoint()
    {
        if (!nonFinalDestructionTransitionActive ||
            nonFinalDestructionPart == null ||
            !TryGetCombatViewportBounds(out Bounds bounds))
        {
            return;
        }

        Vector3 endpoint = nonFinalDestructionPart.transform.position;
        endpoint.y = bounds.min.y + destructionChargeEndpointInset;
        nonFinalDestructionPart.transform.position = endpoint;
    }

    [ContextMenu("Development/Transition/Force Wreck Fade")]
    private void DevelopmentForceWreckFade()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null || part.State != FrigateBossPartState.Destroyed)
            {
                continue;
            }

            part.BeginDetachedWreckFade(
                destructionWreckFadeDelay,
                destructionWreckFadeDuration
            );
            return;
        }
    }

    [ContextMenu("Development/Stop And Cleanup Encounter")]
    private void DevelopmentStopAndCleanupEncounter()
    {
        CleanupBossRuntime();
    }

    [ContextMenu("Development/Damage Left Part")]
    private void DevelopmentDamageLeftPart()
    {
        DevelopmentDamagePart(FrigateBossPartId.Left, developmentDamage);
    }

    [ContextMenu("Development/Damage Center Part")]
    private void DevelopmentDamageCenterPart()
    {
        DevelopmentDamagePart(FrigateBossPartId.Center, developmentDamage);
    }

    [ContextMenu("Development/Damage Right Part")]
    private void DevelopmentDamageRightPart()
    {
        DevelopmentDamagePart(FrigateBossPartId.Right, developmentDamage);
    }

    [ContextMenu("Development/Destroy Left Part")]
    private void DevelopmentDestroyLeftPart()
    {
        DevelopmentDestroyPart(FrigateBossPartId.Left);
    }

    [ContextMenu("Development/Destroy Center Part")]
    private void DevelopmentDestroyCenterPart()
    {
        DevelopmentDestroyPart(FrigateBossPartId.Center);
    }

    [ContextMenu("Development/Destroy Right Part")]
    private void DevelopmentDestroyRightPart()
    {
        DevelopmentDestroyPart(FrigateBossPartId.Right);
    }

    [ContextMenu("Development/Force Final Lethal Pending")]
    private void DevelopmentForceFinalLethalPending()
    {
        if (!initialized || !gameplayBegun || corridorController == null ||
            !corridorController.IsRuntimeActive)
        {
            Debug.LogWarning(
                "Final-sequence controls require the live post-BeginGameplay encounter.",
                this
            );
            return;
        }

        for (int i = 0; i < parts.Length && alivePartCount > 1; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.IsAlive)
            {
                TryApplyPartDamage(
                    part,
                    part.CurrentHealth,
                    part.transform.position,
                    Vector2.zero
                );
            }
        }

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part != null && part.IsAlive)
            {
                TryApplyPartDamage(
                    part,
                    part.CurrentHealth,
                    part.transform.position,
                    Vector2.zero
                );
                break;
            }
        }
    }

    [ContextMenu("Development/Final Sequence/Run From Warning")]
    private void DevelopmentRunFinalSequenceFromWarning()
    {
        if (state != FrigateTriadBossState.FinalSequencePending ||
            finalPendingPart == null)
        {
            Debug.LogWarning("FinalSequencePending is required.", this);
            return;
        }

        CancelFinalSequence(false);
        StartFinalSequence(finalPendingPart, false);
    }

    [ContextMenu("Development/Final Sequence/Skip Warning And Charge")]
    private void DevelopmentSkipFinalWarning()
    {
        if (state != FrigateTriadBossState.FinalSequencePending ||
            finalPendingPart == null)
        {
            Debug.LogWarning("FinalSequencePending is required.", this);
            return;
        }

        CancelFinalSequence(false);
        StartFinalSequence(finalPendingPart, true);
    }

    [ContextMenu("Development/Final Sequence/Force Charge Miss")]
    private void DevelopmentForceChargeMiss()
    {
        finalChargeMissForced = true;
        finalChargeDamageAttempted = true;
    }

    [ContextMenu("Development/Final Sequence/Force Charge Collision Test")]
    private void DevelopmentForceChargeCollisionTest()
    {
        if (finalSequenceState != SalvageDevourerFinalSequenceState.Charging ||
            finalPendingPart == null || observedPlayerHealth == null)
        {
            Debug.LogWarning("An active Final Charge and live Player are required.", this);
            return;
        }

        Vector3 partPosition = finalPendingPart.transform.position;
        partPosition.x = observedPlayerHealth.transform.position.x;
        partPosition.y = observedPlayerHealth.transform.position.y;
        finalPendingPart.transform.position = partPosition;
        finalChargeMissForced = false;
        finalChargeDamageAttempted = false;
        TryApplyFinalChargeDamage(finalPendingPart);
    }

    [ContextMenu("Development/Final Sequence/Force Charge Endpoint")]
    private void DevelopmentForceChargeEndpoint()
    {
        if (finalSequenceState != SalvageDevourerFinalSequenceState.Charging ||
            finalPendingPart == null)
        {
            return;
        }

        Vector3 endpoint = finalPendingPart.transform.position;
        endpoint.x = lockedFinalChargeLaneX;
        endpoint.y = finalChargeEndpointY;
        finalPendingPart.transform.position = endpoint;
    }

    [ContextMenu("Development/Final Sequence/Cleanup")]
    private void DevelopmentCleanupFinalSequence()
    {
        CancelFinalSequence(true);
    }

    [ContextMenu("Development/Final Sequence/Log State")]
    private void DevelopmentLogFinalSequenceState()
    {
        Debug.Log(
            $"[SalvageDevourer/Final] combatState={state} " +
            $"sequenceState={finalSequenceState} laneX={lockedFinalChargeLaneX:0.##} " +
            $"endpointY={finalChargeEndpointY:0.##} damageAttempted={finalChargeDamageAttempted} " +
            $"token={finalSequenceToken}",
            this
        );
    }

    [ContextMenu("Development/Complete Deferred Boss Death")]
    private void DevelopmentCompleteDeferredBossDeath()
    {
        CompleteDeferredBossDeath();
    }

    [ContextMenu("Development/Reset Triad Health")]
    private void DevelopmentResetTriadHealth()
    {
        InitializeBossState();
    }

    [ContextMenu("Development/Log Triad Health")]
    private void DevelopmentLogTriadHealth()
    {
        string partStatus = string.Empty;

        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            partStatus +=
                $" {part.PartId}={part.CurrentHealth:0.##}/{part.MaxHealth:0.##}({part.State})";
        }

        float aggregateCurrent = aggregateHealth != null ? aggregateHealth.CurrentHp : 0f;
        float aggregateMaximum = aggregateHealth != null ? aggregateHealth.MaxHp : 0f;
        Debug.Log(
            $"[SalvageDevourer] aggregate=" +
            $"{aggregateCurrent:0.##}/{aggregateMaximum:0.##} " +
            $"alive={alivePartCount} state={state}.{partStatus}",
            this
        );
    }

    private void DevelopmentDamagePart(FrigateBossPartId partId, float damage)
    {
        FrigateBossPart part = GetPart(partId);
        if (part == null)
        {
            return;
        }

        TryApplyPartDamage(
            part,
            Mathf.Max(0.1f, damage),
            part.transform.position,
            Vector2.zero
        );
    }

    private void DevelopmentDestroyPart(FrigateBossPartId partId)
    {
        FrigateBossPart part = GetPart(partId);
        if (part == null)
        {
            return;
        }

        TryApplyPartDamage(
            part,
            Mathf.Max(DeferredHealthFloor, part.CurrentHealth),
            part.transform.position,
            Vector2.zero
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (parts == null || parts.Length != 3)
        {
            return;
        }

        Vector2 anchor = Application.isPlaying
            ? ResolveCurrentScrollAnchor()
            : (Vector2)transform.position + formationAnchorOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(anchor, 0.35f);

        Gizmos.color = new Color(0.2f, 1f, 0.85f, 0.95f);
        for (int i = 0; i < parts.Length; i++)
        {
            FrigateBossPart part = parts[i];
            if (part == null || (Application.isPlaying && !part.IsAlive))
            {
                continue;
            }

            Vector2 localTarget = Application.isPlaying
                ? formationTargets[i]
                : part.PartId switch
                {
                    FrigateBossPartId.Left => threeAliveLeftOffset,
                    FrigateBossPartId.Center => threeAliveCenterOffset,
                    FrigateBossPartId.Right => threeAliveRightOffset,
                    _ => Vector2.zero
                };
            Gizmos.DrawWireSphere(anchor + localTarget, 0.24f);
        }

        if (Application.isPlaying &&
            finalSequenceState != SalvageDevourerFinalSequenceState.Inactive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                new Vector3(lockedFinalChargeLaneX, anchor.y + 6f, 0f),
                new Vector3(lockedFinalChargeLaneX, finalChargeEndpointY, 0f)
            );
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(
                new Vector3(lockedFinalChargeLaneX, finalChargeEndpointY, 0f),
                finalChargeHitboxSize
            );
        }
    }
#endif
}
