using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class PhaseGatekeeperBossController : MonoBehaviour
{
    public enum EncounterState
    {
        Dormant,
        Intro,
        Stealth,
        Decloaking,
        Tracking,
        TargetLocked,
        PreparingRoute,
        Charging,
        Firing,
        Exposed,
        Converging,
        Shrinking,
        Restoring,
        DeadOrCleanup
    }

    private enum CombatCyclePhase
    {
        Normal,
        Special
    }

    private enum PhaseTwoRouteMotif
    {
        Clockwise,
        CounterClockwise,
        DiagonalZigZag,
        CornerChain
    }

    private enum RouteFallbackType
    {
        None,
        ShortReflected,
        Direct
    }

    private const int LasersPerCycle = 3;
    private const int MaximumLaserSegments = 4;
    private const int MaximumReflections = 3;
    private const int MaximumRaycastHits = 32;
    private const int MaximumRepositionOverlaps = 16;
    private const int ArenaReflectorCount = 12;
    private const int ArenaWallCount = 4;
    private const int ArenaShrinkStageCount = 3;
    private const int PhaseTwoRoutePlateCount = 4;
    private const float ReflectionRayOffset = 0.04f;
    private static readonly int[] SpecialBossSlotCandidates = { 1, 4, 7, 10 };

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Collider2D damageCollider;
    [SerializeField] private Collider2D proximityTrigger;
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private BossLaserHazard laserHazardPrefab;
    [SerializeField] private Material laserMaterial;

    [Header("Radar Presentation")]
    [SerializeField] private Sprite bossRadarMarkerSprite;
    [SerializeField, Range(1f, 2f)] private float bossRadarMarkerScale = 1.35f;
    [SerializeField, Range(0.6f, 1f)] private float stealthRadarScaleMultiplier = 0.82f;

    [Header("Intro")]
    [SerializeField, Min(0.1f)] private float decloakDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float combatChargeDuration = 0.7f;
    [SerializeField] private Color stealthColor = new Color(0.25f, 0.75f, 1f, 0.08f);
    [SerializeField] private Color revealedColor = Color.white;
    [SerializeField] private Color chargingColor = new Color(0.45f, 0.9f, 1f, 1f);

    [Header("Boss Sprite Presentation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] chargeSprites;
    [SerializeField, Range(0.04f, 0.2f)] private float chargeFrameDuration = 0.075f;

    [Header("Reflected Laser")]
    [SerializeField, Range(0, MaximumReflections)] private int maxReflections = 3;
    [SerializeField] private LayerMask laserBlockingMask;
    [SerializeField, Range(0.1f, 0.5f)] private float laserLockDuration = 0.4f;
    [SerializeField, Range(0f, 20f)] private float attackAimBiasDegrees = 6f;
    [SerializeField, Min(0.05f)] private float laserFireDuration = 0.8f;
    [SerializeField, Min(0f)] private float laserRecoveryDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float laserRange = 72f;
    [SerializeField, Min(0.1f)] private float phaseTwoLaserRange = 180f;
    [SerializeField, Min(0.05f)] private float normalInitialLaserWidth = 2f;
    [FormerlySerializedAs("initialLaserWidth")]
    [SerializeField, Min(0.05f)] private float specialInitialLaserWidth = 2.4f;
    [SerializeField, Range(0.75f, 1f)] private float specialReflectionWidthMultiplier = 1f;
    [SerializeField, Range(0.15f, 0.6f)] private float specialBeamEndWidthMultiplier = 0.28f;
    [SerializeField, Min(0f)] private float laserDamage = 4f;
    [SerializeField, Min(0.05f)] private float laserDamageInterval = 0.5f;
    [SerializeField, Min(1f)] private float environmentBreakDamage = 999f;
    [SerializeField] private Color telegraphColor = new Color(0.25f, 0.9f, 1f, 0.55f);
    [SerializeField] private Color lockedTelegraphColor = new Color(0.65f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color activeLaserColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color specialActiveLaserColor = new Color(0.75f, 0.98f, 1f, 1f);
    [SerializeField, Range(0.1f, 0.65f)] private float previewTelegraphWidthScale = 0.42f;
    [SerializeField, Range(0.2f, 0.85f)] private float lockedTelegraphWidthScale = 0.62f;
    [SerializeField] private string laserSortingLayerName = "Default";
    [SerializeField] private int laserSortingOrder = 24;

    [Header("Player Hunt Targeting")]
    [SerializeField, Range(0.35f, 1.2f)] private float normalTrackingDuration = 0.65f;
    [SerializeField, Range(0.35f, 1.2f)] private float specialTrackingDuration = 0.55f;
    [SerializeField, Range(0f, 0.5f)] private float predictionLeadTime = 0.28f;
    [SerializeField, Range(0f, 1f)] private float targetThreatPadding = 0.3f;
    [SerializeField, Range(4, 24)] private int routeCandidateAttempts = 16;
    [SerializeField, Min(0.25f)] private float minimumReadableSegmentLength = 1.25f;
    [SerializeField, Min(0.05f)] private float targetMarkerRadius = 0.42f;
    [SerializeField, Min(0.01f)] private float targetMarkerLineWidth = 0.08f;
    [SerializeField] private Color trackingMarkerColor = new Color(0.25f, 0.95f, 1f, 0.8f);
    [SerializeField] private Color lockedMarkerColor = new Color(0.85f, 0.35f, 1f, 1f);

    [Header("Reflection Chain Readability")]
    [SerializeField, Range(0.05f, 0.3f)] private float chainOriginCueDuration = 0.12f;
    [SerializeField, Range(0.08f, 0.35f)] private float reflectorBlinkStepDuration = 0.18f;
    [SerializeField, Range(0.05f, 0.3f)] private float reflectorBlinkPulseDuration = 0.16f;

    [Header("Phase 2 Route Preparation")]
    [SerializeField, Min(0.1f)] private float reflectorOrientationDuration = 0.45f;
    [SerializeField, Min(0f)] private float phaseTwoThinChainHoldDuration = 0.25f;
    [SerializeField, Range(1, 12)] private int phaseTwoRouteCandidateAttempts = 8;

    [Header("Reflector Convergence")]
    [SerializeField, Min(0.1f)] private float convergencePulseDuration = 0.25f;
    [SerializeField, Range(1f, 1.4f)] private float convergenceMoveDuration = 1.1f;
    [SerializeField, Min(0f)] private float convergenceSettleDuration = 0.1f;
    [SerializeField, Min(6f)] private float initialArenaHalfSize = 11f;
    [SerializeField, Min(6f)] private float minimumArenaHalfSize = 6.5f;
    [SerializeField, Range(0.5f, 3f)] private float specialBossSlotInset = 2.2f;

    [Header("Shrinking Square Arena")]
    [SerializeField] private Vector3 arenaShrinkMultipliers = new Vector3(0.84f, 0.7f, 0.58f);
    [SerializeField, Range(0.6f, 0.9f)] private float shrinkWarningDuration = 0.75f;
    [SerializeField, Range(0.6f, 1f)] private float shrinkMoveDuration = 0.8f;
    [SerializeField, Min(0f)] private float shrinkPulseLeadDuration = 0.18f;
    [SerializeField, Min(0.05f)] private float arenaWallThickness = 0.28f;
    [SerializeField, Min(0f)] private float arenaBoundsPadding = 0.35f;
    [SerializeField] private Color arenaWallColor = new Color(0.3f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color nextArenaWarningColor = new Color(0.75f, 0.35f, 1f, 0.85f);
    [SerializeField] private Color outerDarknessColor = new Color(0.04f, 0.015f, 0.08f, 0.78f);
    [SerializeField] private string arenaSortingLayerName = "Default";
    [SerializeField] private int arenaBoundarySortingOrder = 20;
    [SerializeField] private int outerDarknessSortingOrder = 18;

    [Header("Combat Camera")]
    [SerializeField, Range(1f, 2f)] private float normalCombatCameraZoomMultiplier = 1.25f;
    [SerializeField, Min(0f)] private float specialArenaCameraPadding = 2f;
    [SerializeField, Min(0f)] private float attackCameraPadding = 2.2f;
    [SerializeField, Range(1.5f, 4f)] private float maximumAttackCameraZoomMultiplier = 3.1f;
    [SerializeField, Range(0.05f, 0.3f)] private float trackingCameraRefreshInterval = 0.12f;

    [Header("Stealth Reposition")]
    [SerializeField, Min(0.05f)] private float cloakDuration = 0.2f;
    [SerializeField, Min(0f)] private float hiddenRepositionHold = 0.1f;
    [SerializeField, Range(0.1f, 0.5f)] private float repositionRevealDuration = 0.25f;
    [SerializeField, Range(0.5f, 0.8f)] private float decloakPreparationDuration = 0.65f;
    [SerializeField, Range(4f, 20f)] private float minimumPlayerRepositionDistance = 10f;
    [SerializeField, Min(12f)] private float maximumNormalPlayerRepositionDistance = 32f;
    [SerializeField, Min(1f)] private float mapEdgePadding = 4f;
    [SerializeField, Min(0.25f)] private float repositionClearanceRadius = 1.75f;
    [SerializeField, Range(1, 24)] private int repositionCandidateAttempts = 12;
    [SerializeField] private LayerMask repositionBlockingMask;

    [Header("Exposure Cycle")]
    [SerializeField, Min(0.25f)] private float exposedDuration = 5f;
    [SerializeField, Min(2f)] private float exposedMaximumPlayerDistance = 6.5f;
    [SerializeField] private Color exposedColor = new Color(1f, 0.3f, 1f, 1f);
    [SerializeField, Range(1.05f, 1.35f)] private float exposedPulseScale = 1.22f;
    [SerializeField, Range(4f, 14f)] private float exposedPulseSpeed = 9f;

    [Header("Development Visualization")]
    [SerializeField] private bool drawReflectionChainGizmos = true;
    [SerializeField] private bool drawReflectorNormalGizmos = true;

    private readonly Vector2[] segmentStarts = new Vector2[MaximumLaserSegments];
    private readonly Vector2[] segmentEnds = new Vector2[MaximumLaserSegments];
    private readonly float[] segmentWidths = new float[MaximumLaserSegments];
    private readonly PhaseReflectorPlate[] segmentReflectors =
        new PhaseReflectorPlate[MaximumLaserSegments];
    private readonly PhaseReflectorPlate[] segmentHitReflectors =
        new PhaseReflectorPlate[MaximumLaserSegments];
    private readonly Collider2D[] segmentBlockingColliders =
        new Collider2D[MaximumLaserSegments];
    private readonly LineRenderer[] telegraphLines = new LineRenderer[MaximumLaserSegments];
    private readonly BossLaserHazard[] activeLaserHazards =
        new BossLaserHazard[MaximumLaserSegments];
    private readonly RaycastHit2D[] laserRaycastHits = new RaycastHit2D[MaximumRaycastHits];
    private readonly Collider2D[] repositionOverlapResults =
        new Collider2D[MaximumRepositionOverlaps];
    private readonly PhaseReflectorPlate[] arenaReflectors =
        new PhaseReflectorPlate[ArenaReflectorCount];
    private readonly Vector2[] arenaSlotPositions = new Vector2[ArenaReflectorCount];
    private readonly int[] phaseTwoRouteSlots = new int[PhaseTwoRoutePlateCount];
    private readonly float[] phaseTwoRouteRotations = new float[MaximumReflections];
    private readonly float[] preparedRouteOriginalRotations = new float[MaximumReflections];
    private readonly PhaseReflectorPlate[] phaseTwoRouteReflectors =
        new PhaseReflectorPlate[MaximumReflections];
    private readonly int[] candidateRouteSlots = new int[PhaseTwoRoutePlateCount];
    private readonly float[] candidateRouteRotations = new float[MaximumReflections];
    private readonly PhaseReflectorPlate[] candidateRouteReflectors =
        new PhaseReflectorPlate[MaximumReflections];
    private readonly BossArenaLaserWall[] arenaWalls =
        new BossArenaLaserWall[ArenaWallCount];
    private readonly LineRenderer[] nextArenaFrameLines = new LineRenderer[ArenaWallCount];
    private readonly LineRenderer[] outerDarknessLines = new LineRenderer[ArenaWallCount];
    private readonly Vector2[] normalReflectorPositions = new Vector2[ArenaReflectorCount];
    private readonly float[] normalReflectorRotations = new float[ArenaReflectorCount];

    private PhaseReflectorPlate[] reflectorPlates;
    private Bounds encounterBounds;
    private Bounds cameraSafeBounds;
    private Bounds currentArenaBounds;
    private Bounds nextArenaBounds;
    private Vector3 bodyBaseScale;
    private PlayerHealth playerHealth;
    private PlayerController2D playerController;
    private Rigidbody2D playerRigidbody;
    private RadarTarget radarTarget;
    private GungeonStyleCamera2D gameplayCamera;
    private ExpeditionHUD expeditionHud;
    private RunManager observedRunManager;
    private ContactFilter2D laserContactFilter;
    private ContactFilter2D repositionContactFilter;
    private Coroutine encounterRoutine;
    private EncounterState state = EncounterState.Dormant;
    private int segmentCount;
    private int reflectionCount;
    private int attackSequenceIndex;
    private int lasersCompletedInCycle;
    private int repositionSequenceIndex;
    private bool encounterConfigured;
    private bool introStarted;
    private bool cleanupComplete;
    private bool introLocksHeld;
    private bool laserChainLocked;
    private bool phaseTwoActive;
    private bool normalReflectorLayoutCached;
    private bool arenaConstraintHeld;
    private bool phaseTwoRoutePrepared;
    private int preparedRouteReflectorCount;
    private Vector2 predictedTargetPosition;
    private Vector2 lockedTargetPosition;
    private bool hasLockedTarget;
    private int threateningSegmentIndex = -1;
    private float selectedThreatDistance = float.PositiveInfinity;
    private float selectedRouteScore = float.PositiveInfinity;
    private int selectedRouteCandidateAttempts;
    private RouteFallbackType selectedRouteFallback;
    private int routeSelectionCount;
    private int reflectedRouteSelectionCount;
    private int shortFallbackSelectionCount;
    private int directFallbackSelectionCount;
    private int currentShrinkStage;
    private int specialOpenSlotIndex = -1;
    private PhaseTwoRouteMotif currentRouteMotif;
    private Vector2 phaseTwoLockedInitialDirection;
    private Transform arenaPresentationRoot;
    private Material arenaPresentationMaterial;
    private float activeInitialArenaHalfSize;
    private bool arenaConstraintWarningIssued;
    private bool combatCameraProfileHeld;
    private bool specialCameraFocusHeld;
    private bool attackCameraFocusHeld;
    private float combatCameraBaseOrthographicSize;
    private CombatCyclePhase combatCyclePhase;
    private GameObject targetMarkerObject;
    private LineRenderer targetMarkerLine;
    private Sprite radarMarkerSprite;
    private Color radarVisibleColor = Color.white;
    private float radarVisibleScale = 1f;
    private float chargeAnimationStartTime;
    private bool chargeAnimationPlaying;

    public EncounterState State => state;
    public bool IsExposed => state == EncounterState.Exposed;
    public bool RejectsIncomingDamage => state != EncounterState.Exposed || cleanupComplete;
    public int ReflectorPlateCount => reflectorPlates != null ? reflectorPlates.Length : 0;
    public int LasersCompletedInCycle => lasersCompletedInCycle;
    public Vector2 EncounterAnchor => proximityTrigger != null
        ? (Vector2)proximityTrigger.bounds.center
        : transform.position;

    public event System.Action EncounterStarted;
    public event System.Action EncounterEnded;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        damageCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (laserOrigin == null)
        {
            laserOrigin = transform;
        }

        bodyBaseScale = bodyRenderer != null
            ? bodyRenderer.transform.localScale
            : Vector3.one;
        ConfigurePhysicsFilters();
        EnsureTelegraphLines();
        ApplyDormantState();
    }

    private void OnEnable()
    {
        cleanupComplete = false;
        state = EncounterState.Dormant;
        attackSequenceIndex = 0;
        lasersCompletedInCycle = 0;
        repositionSequenceIndex = 0;
        phaseTwoActive = false;
        normalReflectorLayoutCached = false;
        arenaConstraintHeld = false;
        arenaConstraintWarningIssued = false;
        phaseTwoRoutePrepared = false;
        preparedRouteReflectorCount = 0;
        hasLockedTarget = false;
        threateningSegmentIndex = -1;
        selectedThreatDistance = float.PositiveInfinity;
        selectedRouteScore = float.PositiveInfinity;
        selectedRouteCandidateAttempts = 0;
        selectedRouteFallback = RouteFallbackType.None;
        routeSelectionCount = 0;
        reflectedRouteSelectionCount = 0;
        shortFallbackSelectionCount = 0;
        directFallbackSelectionCount = 0;
        currentShrinkStage = 0;
        specialOpenSlotIndex = -1;
        currentRouteMotif = PhaseTwoRouteMotif.Clockwise;
        combatCyclePhase = CombatCyclePhase.Normal;
        combatCameraProfileHeld = false;
        specialCameraFocusHeld = false;
        attackCameraFocusHeld = false;
        combatCameraBaseOrthographicSize = 0f;
        introStarted = false;
        playerHealth = null;
        playerController = null;
        playerRigidbody = null;
        chargeAnimationPlaying = false;
        chargeAnimationStartTime = 0f;
        ApplyDormantState();

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = true;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleBossDied;
        }

        SubscribeRunEnd();
    }

    private void OnDisable()
    {
        CleanupEncounter(true);
    }

    private void OnDestroy()
    {
        CleanupEncounter(true);
    }

    private void OnValidate()
    {
        maxReflections = Mathf.Clamp(maxReflections, 0, MaximumReflections);
        laserLockDuration = Mathf.Clamp(laserLockDuration, 0.1f, 0.5f);
        normalInitialLaserWidth = Mathf.Max(0.05f, normalInitialLaserWidth);
        specialInitialLaserWidth = Mathf.Max(0.05f, specialInitialLaserWidth);
        specialReflectionWidthMultiplier = Mathf.Clamp(
            specialReflectionWidthMultiplier,
            0.75f,
            1f
        );
        specialBeamEndWidthMultiplier = Mathf.Clamp(
            specialBeamEndWidthMultiplier,
            0.15f,
            0.6f
        );
        chargeFrameDuration = Mathf.Clamp(chargeFrameDuration, 0.04f, 0.2f);
        previewTelegraphWidthScale = Mathf.Clamp(previewTelegraphWidthScale, 0.1f, 0.65f);
        lockedTelegraphWidthScale = Mathf.Clamp(lockedTelegraphWidthScale, 0.2f, 0.85f);
        chainOriginCueDuration = Mathf.Clamp(chainOriginCueDuration, 0.05f, 0.3f);
        reflectorBlinkStepDuration = Mathf.Clamp(
            reflectorBlinkStepDuration,
            0.08f,
            0.35f
        );
        reflectorBlinkPulseDuration = Mathf.Clamp(
            reflectorBlinkPulseDuration,
            0.05f,
            0.3f
        );
        phaseTwoRouteCandidateAttempts = Mathf.Clamp(
            phaseTwoRouteCandidateAttempts,
            1,
            12
        );
        routeCandidateAttempts = Mathf.Clamp(routeCandidateAttempts, 4, 24);
        normalTrackingDuration = Mathf.Clamp(normalTrackingDuration, 0.35f, 1.2f);
        specialTrackingDuration = Mathf.Clamp(specialTrackingDuration, 0.35f, 1.2f);
        predictionLeadTime = Mathf.Clamp(predictionLeadTime, 0f, 0.5f);
        targetThreatPadding = Mathf.Clamp(targetThreatPadding, 0f, 1f);
        minimumReadableSegmentLength = Mathf.Max(0.25f, minimumReadableSegmentLength);
        targetMarkerRadius = Mathf.Max(0.05f, targetMarkerRadius);
        targetMarkerLineWidth = Mathf.Max(0.01f, targetMarkerLineWidth);
        convergenceMoveDuration = Mathf.Clamp(convergenceMoveDuration, 1f, 1.4f);
        shrinkWarningDuration = Mathf.Clamp(shrinkWarningDuration, 0.6f, 0.9f);
        shrinkMoveDuration = Mathf.Clamp(shrinkMoveDuration, 0.6f, 1f);
        environmentBreakDamage = Mathf.Max(1f, environmentBreakDamage);
        minimumArenaHalfSize = Mathf.Max(6f, minimumArenaHalfSize);
        initialArenaHalfSize = Mathf.Max(minimumArenaHalfSize, initialArenaHalfSize);
        specialBossSlotInset = Mathf.Clamp(specialBossSlotInset, 0.5f, 3f);
        arenaShrinkMultipliers.x = Mathf.Clamp(arenaShrinkMultipliers.x, 0.82f, 0.85f);
        arenaShrinkMultipliers.y = Mathf.Clamp(arenaShrinkMultipliers.y, 0.67f, 0.72f);
        arenaShrinkMultipliers.z = Mathf.Clamp(arenaShrinkMultipliers.z, 0.55f, 0.6f);
        normalCombatCameraZoomMultiplier = Mathf.Clamp(
            normalCombatCameraZoomMultiplier,
            1f,
            2f
        );
        specialArenaCameraPadding = Mathf.Max(0f, specialArenaCameraPadding);
        attackCameraPadding = Mathf.Max(0f, attackCameraPadding);
        maximumAttackCameraZoomMultiplier = Mathf.Clamp(
            maximumAttackCameraZoomMultiplier,
            1.5f,
            4f
        );
        trackingCameraRefreshInterval = Mathf.Clamp(
            trackingCameraRefreshInterval,
            0.05f,
            0.3f
        );
        repositionRevealDuration = Mathf.Clamp(repositionRevealDuration, 0.1f, 0.5f);
        maximumNormalPlayerRepositionDistance = Mathf.Max(
            minimumPlayerRepositionDistance + 2f,
            maximumNormalPlayerRepositionDistance
        );
        repositionCandidateAttempts = Mathf.Clamp(repositionCandidateAttempts, 1, 24);
        exposedPulseScale = Mathf.Clamp(exposedPulseScale, 1.05f, 1.35f);
        exposedPulseSpeed = Mathf.Clamp(exposedPulseSpeed, 4f, 14f);
        exposedMaximumPlayerDistance = Mathf.Max(2f, exposedMaximumPlayerDistance);

        if (Application.isPlaying)
        {
            ConfigurePhysicsFilters();
        }
    }

    public void ConfigureEncounter(
        Bounds mapBounds,
        Bounds safeCameraBounds,
        Vector2 bossAnchor,
        PhaseReflectorPlate[] plates)
    {
        encounterBounds = mapBounds;
        cameraSafeBounds = safeCameraBounds;
        reflectorPlates = plates;
        encounterConfigured = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (introStarted || cleanupComplete || other == null)
        {
            return;
        }

        PlayerHealth candidate = other.GetComponentInParent<PlayerHealth>();

        if (candidate == null || candidate.IsDead || !IsRegion3EncounterActive())
        {
            return;
        }

        BeginEncounter(candidate);
    }

    private void BeginEncounter(PlayerHealth candidate)
    {
        if (introStarted || !encounterConfigured || candidate == null)
        {
            return;
        }

        introStarted = true;
        playerHealth = candidate;
        playerController = candidate.GetComponent<PlayerController2D>();
        playerRigidbody = candidate.GetComponent<Rigidbody2D>();
        playerHealth.Died += HandlePlayerDied;
        BeginCombatRadarTracking();
        System.Action encounterStartedHandlers = EncounterStarted;
        EncounterStarted = null;
        encounterStartedHandlers?.Invoke();

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = false;
        }

        encounterRoutine = StartCoroutine(EncounterRoutine());
    }

    private IEnumerator EncounterRoutine()
    {
        state = EncounterState.Intro;
        SetDamageWindow(false);
        AcquireIntroLocks();
        GameAudioLoopController.EnterBossIntroMusic();
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);

        EventTitleDirector titleDirector = EventTitleDirector.Instance;
        if (titleDirector != null)
        {
            titleDirector.ShowBossEncounter(
                CampaignProgressionCatalog.GetBossDisplayName(CampaignBossId.PhaseGatekeeper),
                CampaignProgressionCatalog.GetBossSubtitle(CampaignBossId.PhaseGatekeeper)
            );
        }

        yield return FadeBodyColor(stealthColor, revealedColor, decloakDuration);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position);
        BeginChargeSpriteAnimation();
        yield return PulseChargingPresentation(combatChargeDuration);

        while (titleDirector != null && titleDirector.IsPlaying && !ShouldStopCombat())
        {
            yield return null;
        }

        if (ShouldStopCombat())
        {
            CleanupEncounter(true);
            yield break;
        }

        ReleaseIntroLocks(false);
        ApplyNormalCombatCameraProfile();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.BossBattle);
        }

        BossHealthBarUI.Instance?.ShowBossAnimated(
            enemyHealth,
            CampaignProgressionCatalog.GetBossDisplayName(CampaignBossId.PhaseGatekeeper)
        );

        yield return RunStealthReposition();

        while (!ShouldStopCombat())
        {
            yield return RunReflectedLaserAttack();

            if (ShouldStopCombat())
            {
                encounterRoutine = null;
                CleanupEncounter(true);
                yield break;
            }

            lasersCompletedInCycle++;
            attackSequenceIndex++;

            if (combatCyclePhase == CombatCyclePhase.Special &&
                phaseTwoActive &&
                currentShrinkStage < ArenaShrinkStageCount)
            {
                yield return RunArenaShrink(currentShrinkStage + 1);

                if (ShouldStopCombat())
                {
                    encounterRoutine = null;
                    CleanupEncounter(true);
                    yield break;
                }
            }

            if (lasersCompletedInCycle >= LasersPerCycle)
            {
                yield return RunExposedWindow();

                if (ShouldStopCombat())
                {
                    encounterRoutine = null;
                    CleanupEncounter(true);
                    yield break;
                }

                lasersCompletedInCycle = 0;

                if (combatCyclePhase == CombatCyclePhase.Normal)
                {
                    yield return RunReflectorConvergence();

                    if (ShouldStopCombat())
                    {
                        encounterRoutine = null;
                        CleanupEncounter(true);
                        yield break;
                    }

                    if (phaseTwoActive)
                    {
                        continue;
                    }
                }
                else
                {
                    yield return RunSpecialCycleRestore();

                    if (ShouldStopCombat())
                    {
                        encounterRoutine = null;
                        CleanupEncounter(true);
                        yield break;
                    }

                    if (!phaseTwoActive)
                    {
                        continue;
                    }
                }
            }

            yield return RunStealthReposition();
        }

        encounterRoutine = null;
        CleanupEncounter(true);
    }

    private IEnumerator RunReflectedLaserAttack()
    {
        SetDamageWindow(false);
        ClearLaserPresentation();
        RestoreRevealedPresentation();
        SetCombatRadarStealth(false);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position);
        BeginChargeSpriteAnimation();

        phaseTwoRoutePrepared = false;
        ClearPreparedRouteReflectors();
        hasLockedTarget = false;
        selectedRouteFallback = RouteFallbackType.None;
        selectedRouteCandidateAttempts = 0;
        threateningSegmentIndex = -1;
        selectedThreatDistance = float.PositiveInfinity;
        selectedRouteScore = float.PositiveInfinity;

        yield return RunPlayerTrackingAndLock();

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        state = EncounterState.PreparingRoute;
        yield return RunThreatRoutePreparation();

        if (ShouldStopCombat() || segmentCount <= 0)
        {
            ClearLaserPresentation();
            yield break;
        }

        laserChainLocked = true;
        ApplyLockedAttackCameraProfile();
        yield return RunReflectorOrderTelegraph(0f, true);

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        if (phaseTwoThinChainHoldDuration > 0f)
        {
            ShowTelegraphChain(false);
            float previewHoldTimer = 0f;
            while (previewHoldTimer < phaseTwoThinChainHoldDuration && !ShouldStopCombat())
            {
                previewHoldTimer += Time.deltaTime;
                SetTelegraphAlpha(telegraphColor.a);
                ApplyLockedOriginPulse(previewHoldTimer);
                yield return null;
            }
        }

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        ShowTelegraphChain(true);
        state = EncounterState.TargetLocked;
        float timer = 0f;
        float lockDuration = Mathf.Max(0.1f, laserLockDuration);
        while (timer < lockDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            SetTelegraphAlpha(lockedTelegraphColor.a);
            ApplyLockedOriginPulse(timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        HideTelegraphChain();
        StopChargeSpriteAnimation();
        RestoreRevealedPresentation();
        state = EncounterState.Firing;
        PulseFiringReflectors();
        SpawnLaserHazards();
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, transform.position);

        timer = 0f;
        while (timer < laserFireDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            if (phaseTwoActive)
            {
                float normalized = Mathf.Clamp01(
                    timer / Mathf.Max(0.05f, laserFireDuration)
                );
                UpdateSpecialLaserTaper(normalized);
            }

            yield return null;
        }

        DeactivateLaserHazards();
        RestoreRevealedPresentation();
        HideTargetMarker();

        if (phaseTwoRoutePrepared)
        {
            BeginRestorePreparedReflectorOrientations(
                Mathf.Min(0.3f, Mathf.Max(0.1f, laserRecoveryDuration))
            );
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        timer = 0f;
        while (timer < laserRecoveryDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        phaseTwoRoutePrepared = false;
        preparedRouteReflectorCount = 0;
        ReleaseAttackCameraProfile(false);
    }

    private IEnumerator RunPlayerTrackingAndLock()
    {
        state = EncounterState.Tracking;
        RestoreRevealedPresentation();
        SetCombatRadarStealth(false);
        EnsureTargetMarker();
        float duration = Mathf.Max(
            0.1f,
            phaseTwoActive ? specialTrackingDuration : normalTrackingDuration
        );
        float timer = 0f;
        float cameraRefreshTimer = 0f;

        while (timer < duration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            cameraRefreshTimer -= Time.deltaTime;
            predictedTargetPosition = ResolvePredictedPlayerTarget();
            UpdateTargetMarker(predictedTargetPosition, false);
            ShowTrackingAimLine(predictedTargetPosition);
            ApplyChargingBodyPulse(timer);

            if (!phaseTwoActive && cameraRefreshTimer <= 0f)
            {
                ApplyTrackingCameraProfile(predictedTargetPosition);
                cameraRefreshTimer = Mathf.Max(0.05f, trackingCameraRefreshInterval);
            }

            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        lockedTargetPosition = predictedTargetPosition;
        hasLockedTarget = true;
        state = EncounterState.TargetLocked;
        HideTelegraphChain();
        UpdateTargetMarker(lockedTargetPosition, true);
    }

    private Vector2 ResolvePredictedPlayerTarget()
    {
        Vector2 playerPosition = playerHealth != null
            ? (Vector2)playerHealth.transform.position
            : (Vector2)transform.position;
        Vector2 playerVelocity = playerRigidbody != null
            ? playerRigidbody.linearVelocity
            : Vector2.zero;
        Vector2 predicted = playerPosition + playerVelocity * predictionLeadTime;
        Bounds targetBounds = phaseTwoActive ? currentArenaBounds : encounterBounds;
        const float edgeInset = 0.25f;
        predicted.x = Mathf.Clamp(
            predicted.x,
            targetBounds.min.x + edgeInset,
            targetBounds.max.x - edgeInset
        );
        predicted.y = Mathf.Clamp(
            predicted.y,
            targetBounds.min.y + edgeInset,
            targetBounds.max.y - edgeInset
        );
        return predicted;
    }

    private void ShowTrackingAimLine(Vector2 targetPosition)
    {
        EnsureTelegraphLines();
        LineRenderer trackingLine = telegraphLines[0];
        trackingLine.positionCount = 2;
        trackingLine.SetPosition(0, ResolveLaserOrigin());
        trackingLine.SetPosition(1, targetPosition);
        float activeWidth = phaseTwoActive
            ? specialInitialLaserWidth
            : normalInitialLaserWidth;
        float width = Mathf.Max(
            0.03f,
            activeWidth * previewTelegraphWidthScale * 0.35f
        );
        trackingLine.startWidth = width;
        trackingLine.endWidth = width;
        trackingLine.startColor = trackingMarkerColor;
        trackingLine.endColor = trackingMarkerColor;
        trackingLine.enabled = true;

        for (int i = 1; i < telegraphLines.Length; i++)
        {
            ResetTelegraphLine(telegraphLines[i]);
        }
    }

    private void EnsureTargetMarker()
    {
        if (targetMarkerObject != null && targetMarkerLine != null)
        {
            targetMarkerObject.SetActive(true);
            return;
        }

        targetMarkerObject = new GameObject("PhaseGatekeeperTargetLock");
        targetMarkerObject.transform.SetParent(transform.parent, true);
        targetMarkerLine = targetMarkerObject.AddComponent<LineRenderer>();
        targetMarkerLine.useWorldSpace = false;
        targetMarkerLine.loop = true;
        targetMarkerLine.positionCount = 4;
        targetMarkerLine.numCapVertices = 0;
        targetMarkerLine.sharedMaterial = laserMaterial;
        targetMarkerLine.sortingLayerName = laserSortingLayerName;
        targetMarkerLine.sortingOrder = laserSortingOrder + 2;
        targetMarkerLine.startWidth = targetMarkerLineWidth;
        targetMarkerLine.endWidth = targetMarkerLineWidth;

        float radius = Mathf.Max(0.05f, targetMarkerRadius);
        targetMarkerLine.SetPosition(0, new Vector3(0f, radius, 0f));
        targetMarkerLine.SetPosition(1, new Vector3(radius, 0f, 0f));
        targetMarkerLine.SetPosition(2, new Vector3(0f, -radius, 0f));
        targetMarkerLine.SetPosition(3, new Vector3(-radius, 0f, 0f));
    }

    private void UpdateTargetMarker(Vector2 position, bool locked)
    {
        EnsureTargetMarker();
        targetMarkerObject.transform.position = position;
        Color color = locked ? lockedMarkerColor : trackingMarkerColor;
        float pulse = locked ? 1.35f : 1f + Mathf.Sin(Time.time * 10f) * 0.08f;
        targetMarkerObject.transform.localScale = Vector3.one * pulse;
        targetMarkerLine.startColor = color;
        targetMarkerLine.endColor = color;
        targetMarkerLine.enabled = true;
    }

    private void HideTargetMarker()
    {
        hasLockedTarget = false;

        if (targetMarkerObject != null)
        {
            targetMarkerObject.SetActive(false);
        }
    }

    private IEnumerator RunThreatRoutePreparation()
    {
        phaseTwoRoutePrepared = false;
        preparedRouteReflectorCount = 0;

        if (phaseTwoActive)
        {
            yield return RunPhaseTwoRoutePreparation();

            if (!phaseTwoRoutePrepared && !ShouldStopCombat())
            {
                yield return RunNormalRoutePreparation();
                if (phaseTwoRoutePrepared)
                {
                    selectedRouteFallback = RouteFallbackType.ShortReflected;
                }
            }
        }
        else
        {
            yield return RunNormalRoutePreparation();
        }

        if (ShouldStopCombat() || phaseTwoRoutePrepared)
        {
            if (phaseTwoRoutePrepared)
            {
                RecordRouteSelection();
            }

            yield break;
        }

        selectedRouteFallback = RouteFallbackType.Direct;
        Vector2 direction = lockedTargetPosition - ResolveLaserOrigin();
        BuildLaserChain(direction);
        phaseTwoRoutePrepared = TryScoreCurrentRoute(
            false,
            false,
            out selectedRouteScore,
            out selectedThreatDistance,
            out threateningSegmentIndex
        );
        RecordRouteSelection();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!phaseTwoRoutePrepared)
        {
            Debug.LogWarning(
                "Phase Gatekeeper direct locked-target fallback was blocked before it could " +
                "reach the locked position.",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"Phase Gatekeeper used Direct fallback after " +
                $"{selectedRouteCandidateAttempts} bounded candidates.",
                this
            );
        }
#endif
    }

    private void RecordRouteSelection()
    {
        routeSelectionCount++;
        switch (selectedRouteFallback)
        {
            case RouteFallbackType.ShortReflected:
                shortFallbackSelectionCount++;
                break;
            case RouteFallbackType.Direct:
                directFallbackSelectionCount++;
                break;
            default:
                reflectedRouteSelectionCount++;
                break;
        }
    }

    private IEnumerator RunNormalRoutePreparation()
    {
        if (!TrySelectNormalThreatRoute())
        {
            yield break;
        }

        float duration = Mathf.Max(0.1f, reflectorOrientationDuration);
        PhaseReflectorPlate plate = phaseTwoRouteReflectors[0];
        plate.SetGameplayEnabled(false);
        plate.BeginRotationTransition(phaseTwoRouteRotations[0], duration);

        float timer = 0f;
        while (timer < duration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        plate.SetWorldPose(plate.transform.position, phaseTwoRouteRotations[0]);
        plate.SetGameplayEnabled(true);
        Physics2D.SyncTransforms();
        BuildLaserChain(phaseTwoLockedInitialDirection);
        phaseTwoRoutePrepared = reflectionCount == 1 &&
            segmentHitReflectors[0] == plate &&
            TryScoreCurrentRoute(
                true,
                true,
                out selectedRouteScore,
                out selectedThreatDistance,
                out threateningSegmentIndex
            );

        if (!phaseTwoRoutePrepared)
        {
            plate.SetWorldPose(plate.transform.position, preparedRouteOriginalRotations[0]);
            Physics2D.SyncTransforms();
            preparedRouteReflectorCount = 0;
        }
    }

    private bool TrySelectNormalThreatRoute()
    {
        if (!hasLockedTarget || reflectorPlates == null || reflectorPlates.Length == 0)
        {
            return false;
        }

        PhaseReflectorPlate bestPlate = null;
        float bestRotation = 0f;
        float bestOriginalRotation = 0f;
        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.PositiveInfinity;
        float bestThreatDistance = float.PositiveInfinity;
        int bestThreatSegment = -1;
        int attempts = Mathf.Min(
            Mathf.Clamp(routeCandidateAttempts, 4, 24),
            reflectorPlates.Length
        );
        int startIndex = reflectorPlates.Length > 0
            ? attackSequenceIndex % reflectorPlates.Length
            : 0;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            selectedRouteCandidateAttempts++;
            PhaseReflectorPlate plate = reflectorPlates[(startIndex + attempt) % reflectorPlates.Length];
            if (plate == null ||
                (phaseTwoActive && plate.ArenaSlotIndex == specialOpenSlotIndex))
            {
                continue;
            }

            Vector2 origin = ResolveLaserOrigin();
            Vector2 platePosition = plate.transform.position;
            if (!TryCalculateReflectorRotation(
                origin,
                platePosition,
                lockedTargetPosition,
                out float targetRotation))
            {
                continue;
            }

            float originalRotation = plate.transform.eulerAngles.z;
            plate.SetWorldPose(platePosition, targetRotation);
            Physics2D.SyncTransforms();
            Vector2 direction = platePosition - origin;
            BuildLaserChain(direction);
            float score = float.PositiveInfinity;
            float threatDistance = float.PositiveInfinity;
            int threatSegment = -1;
            bool valid = segmentCount > 0 &&
                reflectionCount == 1 &&
                segmentHitReflectors[0] == plate &&
                TryScoreCurrentRoute(
                    true,
                    true,
                    out score,
                    out threatDistance,
                    out threatSegment
                );
            plate.SetWorldPose(platePosition, originalRotation);
            Physics2D.SyncTransforms();

            if (!valid || score >= bestScore)
            {
                continue;
            }

            bestPlate = plate;
            bestRotation = targetRotation;
            bestOriginalRotation = originalRotation;
            bestDirection = direction.normalized;
            bestScore = score;
            bestThreatDistance = threatDistance;
            bestThreatSegment = threatSegment;
        }

        if (bestPlate == null)
        {
            return false;
        }

        ClearPreparedRouteReflectors();
        phaseTwoRouteReflectors[0] = bestPlate;
        phaseTwoRouteRotations[0] = bestRotation;
        preparedRouteOriginalRotations[0] = bestOriginalRotation;
        preparedRouteReflectorCount = 1;
        phaseTwoLockedInitialDirection = bestDirection;
        selectedRouteScore = bestScore;
        selectedThreatDistance = bestThreatDistance;
        threateningSegmentIndex = bestThreatSegment;
        return true;
    }

    private static bool TryCalculateReflectorRotation(
        Vector2 previousPosition,
        Vector2 reflectorPosition,
        Vector2 nextPosition,
        out float rotationDegrees)
    {
        rotationDegrees = 0f;
        Vector2 incoming = reflectorPosition - previousPosition;
        Vector2 outgoing = nextPosition - reflectorPosition;
        if (incoming.sqrMagnitude <= 0.0001f || outgoing.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 surfaceNormal = incoming.normalized - outgoing.normalized;
        if (surfaceNormal.sqrMagnitude <= 0.0001f)
        {
            surfaceNormal = new Vector2(-incoming.y, incoming.x).normalized;
        }
        else
        {
            surfaceNormal.Normalize();
        }

        rotationDegrees = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        return true;
    }

    private void ClearPreparedRouteReflectors()
    {
        for (int i = 0; i < MaximumReflections; i++)
        {
            phaseTwoRouteReflectors[i] = null;
            phaseTwoRouteRotations[i] = 0f;
            preparedRouteOriginalRotations[i] = 0f;
        }

        preparedRouteReflectorCount = 0;
    }

    private IEnumerator RunExposedWindow()
    {
        ClearLaserPresentation();
        StopChargeSpriteAnimation();
        state = EncounterState.Exposed;
        RestoreRevealedPresentation();
        MoveBossToReachableExposedPosition();
        SetDamageWindow(true);
        AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);

        float timer = 0f;
        while (timer < exposedDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            float pulse = 0.5f + Mathf.Sin(timer * exposedPulseSpeed) * 0.5f;

            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(exposedColor, Color.white, pulse * 0.7f);
                bodyRenderer.transform.localScale = bodyBaseScale * Mathf.Lerp(
                    1.08f,
                    exposedPulseScale,
                    pulse
                );
            }

            yield return null;
        }

        RestoreRevealedPresentation();
        SetDamageWindow(false);
    }

    private IEnumerator RunStealthReposition()
    {
        ClearLaserPresentation();
        SetDamageWindow(false);

        if (phaseTwoActive)
        {
            state = EncounterState.Decloaking;
            RestoreRevealedPresentation();
            SetCombatRadarStealth(false);
            MoveBossToSpecialOpenSlot();

            float visibleHold = 0f;
            while (visibleHold < hiddenRepositionHold && !ShouldStopCombat())
            {
                visibleHold += Time.deltaTime;
                yield return null;
            }

            yield break;
        }

        state = EncounterState.Stealth;
        SetCombatRadarStealth(true);

        Color currentColor = bodyRenderer != null ? bodyRenderer.color : revealedColor;
        yield return FadeBodyColor(currentColor, stealthColor, cloakDuration);

        if (ShouldStopCombat())
        {
            yield break;
        }

        if (TryResolveRepositionTarget(out Vector2 target))
        {
            transform.position = target;
            Physics2D.SyncTransforms();
        }

        float timer = 0f;
        while (timer < hiddenRepositionHold && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        state = EncounterState.Decloaking;
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);
        yield return FadeBodyColor(
            stealthColor,
            revealedColor,
            Mathf.Max(0.1f, repositionRevealDuration)
        );
        SetCombatRadarStealth(false);
    }

    private IEnumerator RunReflectorConvergence()
    {
        if (phaseTwoActive || combatCyclePhase == CombatCyclePhase.Special)
        {
            yield break;
        }

        ClearLaserPresentation();
        SetDamageWindow(false);
        state = EncounterState.Converging;
        SetCombatRadarStealth(true);

        if (!TryCacheArenaReflectors())
        {
            Debug.LogWarning(
                "Phase Gatekeeper convergence requires exactly 12 valid world reflectors; " +
                "the encounter will remain in its full-map phase.",
                this
            );
            yield break;
        }

        Color currentColor = bodyRenderer != null ? bodyRenderer.color : revealedColor;
        yield return FadeBodyColor(currentColor, stealthColor, cloakDuration);

        if (ShouldStopCombat())
        {
            yield break;
        }

        currentArenaBounds = ResolveInitialArenaBounds();
        nextArenaBounds = currentArenaBounds;
        activeInitialArenaHalfSize = currentArenaBounds.extents.x;
        currentShrinkStage = 0;
        EnsureArenaPresentation();
        UpdateArenaPresentation(currentArenaBounds);
        ShowNextArenaFrame(currentArenaBounds);
        UpdatePlayerArenaConstraint(currentArenaBounds);
        ApplySpecialCombatCameraProfile(currentArenaBounds);

        float pulseDuration = Mathf.Max(0.1f, convergencePulseDuration);
        PulseAllArenaReflectors(pulseDuration);
        float timer = 0f;
        while (timer < pulseDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        ResolveArenaSlotPositions(currentArenaBounds, arenaSlotPositions);
        specialOpenSlotIndex = ResolveSpecialOpenSlotIndex();
        float moveDuration = Mathf.Clamp(convergenceMoveDuration, 1f, 1.4f);
        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            plate.SetGameplayEnabled(false);
            plate.BeginPoseTransition(
                arenaSlotPositions[i],
                ResolveArenaSlotRotation(i),
                moveDuration
            );
        }

        timer = 0f;
        while (timer < moveDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        phaseTwoActive = true;
        combatCyclePhase = CombatCyclePhase.Special;
        SetArenaReflectorPoses(currentArenaBounds, true);
        UpdateArenaPresentation(currentArenaBounds);
        HideNextArenaFrame();
        MoveBossToSpecialOpenSlot();
        Physics2D.SyncTransforms();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"Phase Gatekeeper Phase Cage active: open reflector slot=" +
            $"{specialOpenSlotIndex}, reflectors=11+Boss.",
            this
        );
#endif

        timer = 0f;
        while (timer < convergenceSettleDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        state = EncounterState.Decloaking;
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);
        yield return FadeBodyColor(
            stealthColor,
            revealedColor,
            Mathf.Max(0.5f, decloakPreparationDuration)
        );
        SetCombatRadarStealth(false);
    }

    private IEnumerator RunSpecialCycleRestore()
    {
        if (!phaseTwoActive || combatCyclePhase != CombatCyclePhase.Special)
        {
            yield break;
        }

        ClearLaserPresentation();
        SetDamageWindow(false);
        state = EncounterState.Restoring;
        SetCombatRadarStealth(true);

        Color currentColor = bodyRenderer != null ? bodyRenderer.color : revealedColor;
        yield return FadeBodyColor(currentColor, stealthColor, cloakDuration);

        if (ShouldStopCombat())
        {
            yield break;
        }

        HideNextArenaFrame();
        ReleasePlayerArenaConstraint();
        DisableArenaPresentationForNormalPhase();
        phaseTwoRoutePrepared = false;

        float moveDuration = Mathf.Clamp(convergenceMoveDuration, 1f, 1.4f);
        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate == null)
            {
                continue;
            }

            plate.CancelPresentationPulse();
            plate.SetGameplayEnabled(false);
            plate.SetPresentationVisible(true);
            plate.BeginPoseTransition(
                normalReflectorPositions[i],
                normalReflectorRotations[i],
                moveDuration
            );
        }

        ReleaseSpecialCameraFocus(false);
        ApplyNormalCombatCameraProfile();

        float timer = 0f;
        while (timer < moveDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        RestoreNormalReflectorLayout(true);
        phaseTwoActive = false;
        combatCyclePhase = CombatCyclePhase.Normal;
        currentShrinkStage = 0;
        specialOpenSlotIndex = -1;
        currentArenaBounds = default;
        nextArenaBounds = default;
        Physics2D.SyncTransforms();

        if (TryResolveRepositionTarget(out Vector2 target))
        {
            transform.position = target;
            Physics2D.SyncTransforms();
        }

        timer = 0f;
        while (timer < hiddenRepositionHold && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        state = EncounterState.Decloaking;
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);
        yield return FadeBodyColor(
            stealthColor,
            revealedColor,
            Mathf.Max(0.5f, decloakPreparationDuration)
        );
        SetCombatRadarStealth(false);
    }

    private IEnumerator RunArenaShrink(int targetStage)
    {
        int clampedStage = Mathf.Clamp(targetStage, 1, ArenaShrinkStageCount);
        if (!phaseTwoActive || clampedStage <= currentShrinkStage)
        {
            yield break;
        }

        ClearLaserPresentation();
        SetDamageWindow(false);
        state = EncounterState.Shrinking;
        nextArenaBounds = ResolveArenaBoundsForStage(clampedStage);
        ShowNextArenaFrame(nextArenaBounds);

        float timer = 0f;
        float warningDuration = Mathf.Clamp(shrinkWarningDuration, 0.6f, 0.9f);
        while (timer < warningDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        PulseAllArenaReflectors(Mathf.Max(0.1f, shrinkPulseLeadDuration + 0.12f));
        timer = 0f;
        while (timer < shrinkPulseLeadDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        Bounds startBounds = currentArenaBounds;
        ResolveArenaSlotPositions(nextArenaBounds, arenaSlotPositions);
        Vector2 bossStartPosition = transform.position;
        Vector2 bossTargetPosition = ResolveSpecialBossSlotPosition(
            nextArenaBounds,
            specialOpenSlotIndex
        );
        float moveDuration = Mathf.Clamp(shrinkMoveDuration, 0.6f, 1f);

        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate == null)
            {
                continue;
            }

            plate.SetGameplayEnabled(false);
            plate.BeginPoseTransition(
                arenaSlotPositions[i],
                ResolveArenaSlotRotation(i),
                moveDuration
            );
        }

        HideNextArenaFrame();
        timer = 0f;
        while (timer < moveDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / moveDuration);
            currentArenaBounds = LerpBounds(startBounds, nextArenaBounds, normalized);
            transform.position = Vector2.Lerp(
                bossStartPosition,
                bossTargetPosition,
                normalized
            );
            UpdatePlayerArenaConstraint(currentArenaBounds);
            UpdateArenaPresentation(currentArenaBounds);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        currentArenaBounds = nextArenaBounds;
        currentShrinkStage = clampedStage;
        SetArenaReflectorPoses(currentArenaBounds, true);
        UpdatePlayerArenaConstraint(currentArenaBounds);
        UpdateArenaPresentation(currentArenaBounds);
        ApplySpecialCombatCameraProfile(currentArenaBounds);
        MoveBossToSpecialOpenSlot();
        Physics2D.SyncTransforms();
    }

    private bool TryCacheArenaReflectors()
    {
        if (reflectorPlates == null || reflectorPlates.Length != ArenaReflectorCount)
        {
            return false;
        }

        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate == null)
            {
                return false;
            }

            arenaReflectors[i] = plate;
            if (!normalReflectorLayoutCached)
            {
                normalReflectorPositions[i] = plate.transform.position;
                normalReflectorRotations[i] = plate.transform.eulerAngles.z;
            }

            plate.AssignArenaSlot(i);
            plate.SetPresentationVisible(true);
        }

        normalReflectorLayoutCached = true;
        return true;
    }

    private void RestoreNormalReflectorLayout(bool gameplayEnabled)
    {
        if (!normalReflectorLayoutCached)
        {
            return;
        }

        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate == null)
            {
                continue;
            }

            plate.SetWorldPose(normalReflectorPositions[i], normalReflectorRotations[i]);
            plate.AssignArenaSlot(-1);
            plate.SetPresentationVisible(true);
            plate.SetGameplayEnabled(gameplayEnabled);
        }
    }

    private Bounds ResolveInitialArenaBounds()
    {
        Bounds safeBounds = cameraSafeBounds.size.x > 0.1f && cameraSafeBounds.size.y > 0.1f
            ? cameraSafeBounds
            : encounterBounds;
        float maximumHalfSize = Mathf.Max(
            4f,
            Mathf.Min(safeBounds.extents.x, safeBounds.extents.y) - arenaBoundsPadding
        );
        float halfSize = Mathf.Min(
            Mathf.Max(minimumArenaHalfSize, initialArenaHalfSize),
            maximumHalfSize
        );
        Vector2 requestedCenter = playerHealth != null
            ? playerHealth.transform.position
            : safeBounds.center;
        Vector2 center = new Vector2(
            Mathf.Clamp(
                requestedCenter.x,
                safeBounds.min.x + halfSize,
                safeBounds.max.x - halfSize
            ),
            Mathf.Clamp(
                requestedCenter.y,
                safeBounds.min.y + halfSize,
                safeBounds.max.y - halfSize
            )
        );

        return CreateSquareBounds(center, halfSize);
    }

    private Bounds ResolveArenaBoundsForStage(int stage)
    {
        float multiplier;
        switch (Mathf.Clamp(stage, 1, ArenaShrinkStageCount))
        {
            case 1:
                multiplier = Mathf.Clamp(arenaShrinkMultipliers.x, 0.82f, 0.85f);
                break;
            case 2:
                multiplier = Mathf.Clamp(arenaShrinkMultipliers.y, 0.67f, 0.72f);
                break;
            default:
                multiplier = Mathf.Clamp(arenaShrinkMultipliers.z, 0.55f, 0.6f);
                break;
        }

        float halfSize = Mathf.Max(
            minimumArenaHalfSize,
            activeInitialArenaHalfSize * multiplier
        );
        halfSize = Mathf.Min(halfSize, currentArenaBounds.extents.x);
        return CreateSquareBounds(currentArenaBounds.center, halfSize);
    }

    private static Bounds CreateSquareBounds(Vector2 center, float halfSize)
    {
        float size = Mathf.Max(0.1f, halfSize) * 2f;
        return new Bounds(center, new Vector3(size, size, 0f));
    }

    private static Bounds LerpBounds(Bounds from, Bounds to, float normalized)
    {
        Vector3 center = Vector3.Lerp(from.center, to.center, normalized);
        Vector3 size = Vector3.Lerp(from.size, to.size, normalized);
        return new Bounds(center, size);
    }

    private static void ResolveArenaSlotPositions(Bounds bounds, Vector2[] positions)
    {
        Vector2 center = bounds.center;
        Vector2 extents = bounds.extents;
        float thirdX = extents.x / 3f;
        float thirdY = extents.y / 3f;

        positions[0] = center + new Vector2(-extents.x, extents.y);
        positions[1] = center + new Vector2(-thirdX, extents.y);
        positions[2] = center + new Vector2(thirdX, extents.y);
        positions[3] = center + new Vector2(extents.x, extents.y);
        positions[4] = center + new Vector2(extents.x, thirdY);
        positions[5] = center + new Vector2(extents.x, -thirdY);
        positions[6] = center + new Vector2(extents.x, -extents.y);
        positions[7] = center + new Vector2(thirdX, -extents.y);
        positions[8] = center + new Vector2(-thirdX, -extents.y);
        positions[9] = center + new Vector2(-extents.x, -extents.y);
        positions[10] = center + new Vector2(-extents.x, -thirdY);
        positions[11] = center + new Vector2(-extents.x, thirdY);
    }

    private static float ResolveArenaSlotRotation(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
            case 6:
                return 45f;
            case 3:
            case 9:
                return -45f;
            case 4:
            case 5:
            case 10:
            case 11:
                return 90f;
            default:
                return 0f;
        }
    }

    private void SetArenaReflectorPoses(Bounds bounds, bool gameplayEnabled)
    {
        ResolveArenaSlotPositions(bounds, arenaSlotPositions);

        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate == null)
            {
                continue;
            }

            bool isOpenBossSlot = phaseTwoActive && i == specialOpenSlotIndex;
            plate.SetWorldPose(arenaSlotPositions[i], ResolveArenaSlotRotation(i));
            plate.SetPresentationVisible(!isOpenBossSlot);
            plate.SetGameplayEnabled(gameplayEnabled && !isOpenBossSlot);
        }
    }

    private void PulseAllArenaReflectors(float duration)
    {
        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate != null && (!phaseTwoActive || i != specialOpenSlotIndex))
            {
                plate.PlayArenaTransitionPulse(duration);
            }
        }
    }

    private IEnumerator RunPhaseTwoRoutePreparation()
    {
        phaseTwoRoutePrepared = false;
        if (!TrySelectPhaseTwoRouteCandidate())
        {
            ResetArenaReflectorOrientationsImmediate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "Phase Gatekeeper found no player-threatening Phase Cage motif in the " +
                "bounded candidate set; this attack will try a shorter reflected route.",
                this
            );
#endif
            yield break;
        }

        ResetArenaReflectorOrientationsImmediate();
        float duration = Mathf.Max(0.1f, reflectorOrientationDuration);

        for (int i = 0; i < MaximumReflections; i++)
        {
            PhaseReflectorPlate plate = phaseTwoRouteReflectors[i];
            if (plate == null)
            {
                continue;
            }

            plate.SetGameplayEnabled(false);
            plate.BeginRotationTransition(phaseTwoRouteRotations[i], duration);
        }

        float timer = 0f;
        while (timer < duration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        for (int i = 0; i < MaximumReflections; i++)
        {
            PhaseReflectorPlate plate = phaseTwoRouteReflectors[i];
            if (plate == null)
            {
                continue;
            }

            int slotIndex = plate.ArenaSlotIndex;
            plate.SetWorldPose(
                arenaSlotPositions[slotIndex],
                phaseTwoRouteRotations[i]
            );
            plate.SetGameplayEnabled(true);
        }

        Physics2D.SyncTransforms();
        BuildLaserChain(phaseTwoLockedInitialDirection);
        phaseTwoRoutePrepared = ValidatePhaseTwoRouteChain(phaseTwoRouteSlots) &&
            TryScoreCurrentRoute(
                true,
                false,
                out selectedRouteScore,
                out selectedThreatDistance,
                out threateningSegmentIndex
            );

        if (!phaseTwoRoutePrepared)
        {
            ResetArenaReflectorOrientationsImmediate();
            Physics2D.SyncTransforms();
            ClearPreparedRouteReflectors();
        }
    }

    private bool TrySelectPhaseTwoRouteCandidate()
    {
        if (!hasLockedTarget)
        {
            return false;
        }

        PhaseTwoRouteMotif preferredMotif =
            (PhaseTwoRouteMotif)(attackSequenceIndex % 4);
        int attempts = Mathf.Clamp(
            Mathf.Max(phaseTwoRouteCandidateAttempts, routeCandidateAttempts),
            4,
            24
        );
        float bestScore = float.PositiveInfinity;
        float bestThreatDistance = float.PositiveInfinity;
        int bestThreatSegment = -1;
        PhaseTwoRouteMotif bestMotif = preferredMotif;
        bool found = false;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            selectedRouteCandidateAttempts++;
            PhaseTwoRouteMotif candidateMotif = (PhaseTwoRouteMotif)(
                ((int)preferredMotif + attempt / 4) % 4
            );
            ResolvePhaseTwoRouteSlots(candidateMotif, attempt % 4, candidateRouteSlots);
            if (!TryCalculatePhaseTwoRouteRotations(
                candidateRouteSlots,
                candidateRouteReflectors,
                candidateRouteRotations))
            {
                continue;
            }

            ResetArenaReflectorOrientationsImmediate();
            for (int i = 0; i < MaximumReflections; i++)
            {
                PhaseReflectorPlate plate = candidateRouteReflectors[i];
                int slotIndex = plate.ArenaSlotIndex;
                plate.SetWorldPose(
                    arenaSlotPositions[slotIndex],
                    candidateRouteRotations[i]
                );
            }

            Physics2D.SyncTransforms();
            Vector2 firstTarget = arenaSlotPositions[candidateRouteSlots[0]];
            Vector2 initialDirection = firstTarget - ResolveLaserOrigin();
            if (initialDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            BuildLaserChain(initialDirection.normalized);
            if (!ValidatePhaseTwoRouteChain(candidateRouteSlots) ||
                !TryScoreCurrentRoute(
                    true,
                    false,
                    out float score,
                    out float threatDistance,
                    out int threatSegment) ||
                score >= bestScore)
            {
                continue;
            }

            found = true;
            bestScore = score;
            bestThreatDistance = threatDistance;
            bestThreatSegment = threatSegment;
            bestMotif = candidateMotif;
            phaseTwoLockedInitialDirection = initialDirection.normalized;

            for (int i = 0; i < PhaseTwoRoutePlateCount; i++)
            {
                phaseTwoRouteSlots[i] = candidateRouteSlots[i];
            }

            for (int i = 0; i < MaximumReflections; i++)
            {
                phaseTwoRouteReflectors[i] = candidateRouteReflectors[i];
                phaseTwoRouteRotations[i] = candidateRouteRotations[i];
                preparedRouteOriginalRotations[i] = ResolveArenaSlotRotation(
                    candidateRouteReflectors[i].ArenaSlotIndex
                );
            }
        }

        ResetArenaReflectorOrientationsImmediate();
        Physics2D.SyncTransforms();
        if (!found)
        {
            ClearPreparedRouteReflectors();
            return false;
        }

        preparedRouteReflectorCount = MaximumReflections;
        currentRouteMotif = bestMotif;
        selectedRouteScore = bestScore;
        selectedThreatDistance = bestThreatDistance;
        threateningSegmentIndex = bestThreatSegment;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"Phase Gatekeeper selected {currentRouteMotif} player-threat route through " +
            $"slots {phaseTwoRouteSlots[0]} -> {phaseTwoRouteSlots[1]} -> " +
            $"{phaseTwoRouteSlots[2]}, threat={selectedThreatDistance:0.###}, " +
            $"attempts={selectedRouteCandidateAttempts}.",
            this
        );
#endif
        return true;
    }

    private bool TryCalculatePhaseTwoRouteRotations(
        int[] routeSlots,
        PhaseReflectorPlate[] routeReflectors,
        float[] routeRotations)
    {
        Vector2 previousPosition = ResolveLaserOrigin();

        for (int i = 0; i < MaximumReflections; i++)
        {
            int currentSlot = routeSlots[i];
            int nextSlot = i < MaximumReflections - 1
                ? routeSlots[i + 1]
                : -1;
            if (currentSlot < 0 || currentSlot >= ArenaReflectorCount ||
                currentSlot == specialOpenSlotIndex ||
                (i < MaximumReflections - 1 &&
                 (nextSlot < 0 || nextSlot >= ArenaReflectorCount || currentSlot == nextSlot)))
            {
                return false;
            }

            PhaseReflectorPlate plate = arenaReflectors[currentSlot];
            if (plate == null)
            {
                return false;
            }

            Vector2 currentPosition = arenaSlotPositions[currentSlot];
            Vector2 nextPosition = i < MaximumReflections - 1
                ? arenaSlotPositions[nextSlot]
                : lockedTargetPosition;
            if (!TryCalculateReflectorRotation(
                previousPosition,
                currentPosition,
                nextPosition,
                out float rotation))
            {
                return false;
            }

            routeReflectors[i] = plate;
            routeRotations[i] = rotation;
            previousPosition = currentPosition;
        }

        return true;
    }

    private bool ValidatePhaseTwoRouteChain(int[] routeSlots)
    {
        if (segmentCount != MaximumLaserSegments ||
            reflectionCount != MaximumReflections)
        {
            return false;
        }

        for (int i = 0; i < MaximumReflections; i++)
        {
            int slot = routeSlots[i];
            if (slot < 0 || slot >= ArenaReflectorCount ||
                segmentHitReflectors[i] != arenaReflectors[slot])
            {
                return false;
            }
        }

        return true;
    }

    private static void ResolvePhaseTwoRouteSlots(
        PhaseTwoRouteMotif motif,
        int variant,
        int[] slots)
    {
        int shift = Mathf.Abs(variant) % 4 * 3;

        for (int i = 0; i < PhaseTwoRoutePlateCount; i++)
        {
            int baseSlot;
            switch (motif)
            {
                case PhaseTwoRouteMotif.Clockwise:
                    baseSlot = i == 0 ? 2 : i == 1 ? 4 : i == 2 ? 7 : 10;
                    break;
                case PhaseTwoRouteMotif.CounterClockwise:
                    baseSlot = i == 0 ? 1 : i == 1 ? 11 : i == 2 ? 8 : 5;
                    break;
                case PhaseTwoRouteMotif.DiagonalZigZag:
                    baseSlot = i == 0 ? 1 : i == 1 ? 7 : i == 2 ? 2 : 8;
                    break;
                default:
                    baseSlot = i == 0 ? 0 : i == 1 ? 6 : i == 2 ? 3 : 9;
                    break;
            }

            slots[i] = (baseSlot + shift) % ArenaReflectorCount;
        }
    }

    private void ResetArenaReflectorOrientationsImmediate()
    {
        if (!phaseTwoActive)
        {
            return;
        }

        ResolveArenaSlotPositions(currentArenaBounds, arenaSlotPositions);
        for (int i = 0; i < ArenaReflectorCount; i++)
        {
            PhaseReflectorPlate plate = arenaReflectors[i];
            if (plate == null)
            {
                continue;
            }

            if (i == specialOpenSlotIndex)
            {
                plate.SetWorldPose(arenaSlotPositions[i], ResolveArenaSlotRotation(i));
                plate.SetPresentationVisible(false);
                plate.SetGameplayEnabled(false);
                continue;
            }

            plate.SetWorldPose(arenaSlotPositions[i], ResolveArenaSlotRotation(i));
            plate.SetPresentationVisible(true);
            plate.SetGameplayEnabled(true);
        }
    }

    private void BeginRestorePreparedReflectorOrientations(float duration)
    {
        for (int i = 0; i < preparedRouteReflectorCount; i++)
        {
            PhaseReflectorPlate plate = phaseTwoRouteReflectors[i];
            if (plate != null)
            {
                float rotation = phaseTwoActive
                    ? ResolveArenaSlotRotation(plate.ArenaSlotIndex)
                    : preparedRouteOriginalRotations[i];
                plate.BeginRotationTransition(
                    rotation,
                    duration
                );
            }
        }
    }

    private void EnsureArenaPresentation()
    {
        if (arenaPresentationRoot != null)
        {
            arenaPresentationRoot.gameObject.SetActive(true);
            return;
        }

        GameObject rootObject = new GameObject("PhaseGatekeeperArenaRuntime");
        arenaPresentationRoot = rootObject.transform;
        arenaPresentationRoot.SetParent(transform.parent, true);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            arenaPresentationMaterial = new Material(shader)
            {
                name = "Runtime_PhaseGatekeeperArena",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        for (int i = 0; i < ArenaWallCount; i++)
        {
            GameObject wallObject = new GameObject($"ArenaWall_{i:00}");
            wallObject.transform.SetParent(arenaPresentationRoot, false);
            arenaWalls[i] = wallObject.AddComponent<BossArenaLaserWall>();
            arenaWalls[i].SetPresentationVisible(false);

            nextArenaFrameLines[i] = CreateArenaLineRenderer(
                $"NextArenaFrame_{i:00}",
                arenaBoundarySortingOrder + 1
            );
            nextArenaFrameLines[i].enabled = false;

            outerDarknessLines[i] = CreateArenaLineRenderer(
                $"OuterDarkness_{i:00}",
                outerDarknessSortingOrder
            );
            outerDarknessLines[i].enabled = false;
        }
    }

    private void DisableArenaPresentationForNormalPhase()
    {
        HideNextArenaFrame();

        if (arenaPresentationRoot != null)
        {
            arenaPresentationRoot.gameObject.SetActive(false);
        }
    }

    private LineRenderer CreateArenaLineRenderer(string objectName, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(arenaPresentationRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 0;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.sortingLayerName = arenaSortingLayerName;
        line.sortingOrder = sortingOrder;
        line.sharedMaterial = arenaPresentationMaterial != null
            ? arenaPresentationMaterial
            : laserMaterial;
        return line;
    }

    private void UpdateArenaPresentation(Bounds bounds)
    {
        if (arenaPresentationRoot == null)
        {
            return;
        }

        ConfigureArenaWall(
            0,
            new Vector2(bounds.min.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.max.y)
        );
        ConfigureArenaWall(
            1,
            new Vector2(bounds.max.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.min.y)
        );
        ConfigureArenaWall(
            2,
            new Vector2(bounds.max.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.min.y)
        );
        ConfigureArenaWall(
            3,
            new Vector2(bounds.min.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.max.y)
        );

        UpdateOuterDarkness(bounds);
    }

    private void ConfigureArenaWall(int index, Vector2 start, Vector2 end)
    {
        BossArenaLaserWall wall = arenaWalls[index];
        if (wall == null)
        {
            return;
        }

        wall.InitializeMovableBetween(
            start,
            end,
            arenaWallThickness,
            true,
            0f,
            1f,
            arenaPresentationMaterial != null ? arenaPresentationMaterial : laserMaterial,
            arenaWallColor,
            arenaSortingLayerName,
            arenaBoundarySortingOrder,
            "BaseLaser"
        );
    }

    private void UpdateOuterDarkness(Bounds activeBounds)
    {
        Bounds worldBounds = encounterBounds;
        float topHeight = Mathf.Max(0f, worldBounds.max.y - activeBounds.max.y);
        float bottomHeight = Mathf.Max(0f, activeBounds.min.y - worldBounds.min.y);
        float leftWidth = Mathf.Max(0f, activeBounds.min.x - worldBounds.min.x);
        float rightWidth = Mathf.Max(0f, worldBounds.max.x - activeBounds.max.x);

        ConfigureWideArenaLine(
            outerDarknessLines[0],
            new Vector2(worldBounds.min.x, activeBounds.max.y + topHeight * 0.5f),
            new Vector2(worldBounds.max.x, activeBounds.max.y + topHeight * 0.5f),
            topHeight,
            outerDarknessColor
        );
        ConfigureWideArenaLine(
            outerDarknessLines[1],
            new Vector2(worldBounds.min.x, activeBounds.min.y - bottomHeight * 0.5f),
            new Vector2(worldBounds.max.x, activeBounds.min.y - bottomHeight * 0.5f),
            bottomHeight,
            outerDarknessColor
        );
        ConfigureWideArenaLine(
            outerDarknessLines[2],
            new Vector2(activeBounds.min.x - leftWidth * 0.5f, worldBounds.min.y),
            new Vector2(activeBounds.min.x - leftWidth * 0.5f, worldBounds.max.y),
            leftWidth,
            outerDarknessColor
        );
        ConfigureWideArenaLine(
            outerDarknessLines[3],
            new Vector2(activeBounds.max.x + rightWidth * 0.5f, worldBounds.min.y),
            new Vector2(activeBounds.max.x + rightWidth * 0.5f, worldBounds.max.y),
            rightWidth,
            outerDarknessColor
        );
    }

    private static void ConfigureWideArenaLine(
        LineRenderer line,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = width > 0.01f;
        if (!line.enabled)
        {
            return;
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    private void ShowNextArenaFrame(Bounds bounds)
    {
        if (arenaPresentationRoot == null)
        {
            return;
        }

        ConfigureFrameLine(
            nextArenaFrameLines[0],
            new Vector2(bounds.min.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.max.y)
        );
        ConfigureFrameLine(
            nextArenaFrameLines[1],
            new Vector2(bounds.max.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.min.y)
        );
        ConfigureFrameLine(
            nextArenaFrameLines[2],
            new Vector2(bounds.max.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.min.y)
        );
        ConfigureFrameLine(
            nextArenaFrameLines[3],
            new Vector2(bounds.min.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.max.y)
        );
    }

    private void ConfigureFrameLine(LineRenderer line, Vector2 start, Vector2 end)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = true;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = Mathf.Max(0.05f, arenaWallThickness * 0.55f);
        line.endWidth = line.startWidth;
        line.startColor = nextArenaWarningColor;
        line.endColor = nextArenaWarningColor;
    }

    private void HideNextArenaFrame()
    {
        for (int i = 0; i < nextArenaFrameLines.Length; i++)
        {
            LineRenderer line = nextArenaFrameLines[i];
            if (line != null)
            {
                line.enabled = false;
            }
        }
    }

    private void UpdatePlayerArenaConstraint(Bounds bounds)
    {
        if (playerController == null)
        {
            return;
        }

        float inset = Mathf.Max(0f, arenaBoundsPadding + arenaWallThickness * 0.5f);
        Bounds allowedBounds = bounds;
        allowedBounds.Expand(new Vector3(-inset * 2f, -inset * 2f, 0f));
        arenaConstraintHeld = playerController.AcquireTemporaryWorldBoundsConstraint(
            this,
            allowedBounds
        );

        if (!arenaConstraintHeld && !arenaConstraintWarningIssued)
        {
            arenaConstraintWarningIssued = true;
            Debug.LogWarning(
                "Phase Gatekeeper could not acquire its owner-scoped Player arena constraint; " +
                "the physical square walls remain active as a fallback.",
                this
            );
        }
    }

    private void ReleasePlayerArenaConstraint()
    {
        if (playerController != null)
        {
            playerController.ReleaseTemporaryWorldBoundsConstraint(this);
        }

        arenaConstraintHeld = false;
    }

    private void EnsureBossInsideArena()
    {
        float inset = Mathf.Max(
            repositionClearanceRadius,
            arenaWallThickness + arenaBoundsPadding
        );
        Bounds innerBounds = currentArenaBounds;
        innerBounds.Expand(new Vector3(-inset * 2f, -inset * 2f, 0f));
        Vector2 position = transform.position;

        if (innerBounds.Contains(position) && IsRepositionLocationClear(position))
        {
            return;
        }

        if (TryResolveRepositionTarget(out Vector2 target))
        {
            transform.position = target;
            return;
        }

        Vector2 fallback = new Vector2(
            Mathf.Clamp(position.x, innerBounds.min.x, innerBounds.max.x),
            Mathf.Clamp(position.y, innerBounds.min.y, innerBounds.max.y)
        );
        Vector2 playerPosition = playerHealth != null
            ? playerHealth.transform.position
            : innerBounds.center;
        bool foundClearFallback = IsRepositionLocationClear(fallback);
        float bestDistanceSqr = foundClearFallback
            ? (fallback - playerPosition).sqrMagnitude
            : -1f;

        for (int i = 0; i < 4; i++)
        {
            Vector2 signs = new Vector2(
                i == 0 || i == 3 ? -1f : 1f,
                i < 2 ? 1f : -1f
            );
            Vector2 candidate = (Vector2)innerBounds.center + new Vector2(
                innerBounds.extents.x * 0.55f * signs.x,
                innerBounds.extents.y * 0.55f * signs.y
            );
            float distanceSqr = (candidate - playerPosition).sqrMagnitude;
            if (distanceSqr <= bestDistanceSqr || !IsRepositionLocationClear(candidate))
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            fallback = candidate;
            foundClearFallback = true;
        }

        transform.position = foundClearFallback ? fallback : (Vector2)innerBounds.center;
    }

    private int ResolveSpecialOpenSlotIndex()
    {
        Vector2 playerPosition = playerHealth != null
            ? (Vector2)playerHealth.transform.position
            : currentArenaBounds.center;
        int start = Mathf.Abs(attackSequenceIndex) % SpecialBossSlotCandidates.Length;
        int fallbackSlot = SpecialBossSlotCandidates[start];
        int selectedSlot = -1;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < SpecialBossSlotCandidates.Length; i++)
        {
            int slot = SpecialBossSlotCandidates[(start + i) % SpecialBossSlotCandidates.Length];
            Vector2 candidate = ResolveSpecialBossSlotPosition(currentArenaBounds, slot);
            float distanceSqr = (candidate - playerPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || !IsRepositionLocationClear(candidate))
            {
                continue;
            }

            selectedSlot = slot;
            bestDistanceSqr = distanceSqr;
        }

        return selectedSlot >= 0 ? selectedSlot : fallbackSlot;
    }

    private Vector2 ResolveSpecialBossSlotPosition(Bounds arenaBounds, int slotIndex)
    {
        ResolveArenaSlotPositions(arenaBounds, arenaSlotPositions);
        int safeSlot = Mathf.Clamp(slotIndex, 0, ArenaReflectorCount - 1);
        Vector2 slotPosition = arenaSlotPositions[safeSlot];
        Vector2 inward = (Vector2)arenaBounds.center - slotPosition;
        if (inward.sqrMagnitude <= 0.0001f)
        {
            inward = Vector2.down;
        }

        return slotPosition + inward.normalized * specialBossSlotInset;
    }

    private void MoveBossToSpecialOpenSlot()
    {
        if (!phaseTwoActive || specialOpenSlotIndex < 0)
        {
            EnsureBossInsideArena();
            return;
        }

        Vector2 target = ResolveSpecialBossSlotPosition(
            currentArenaBounds,
            specialOpenSlotIndex
        );
        transform.position = target;
    }

    private void MoveBossToReachableExposedPosition()
    {
        if (playerHealth == null)
        {
            return;
        }

        Vector2 playerPosition = playerHealth.transform.position;
        Vector2 currentPosition = transform.position;
        Vector2 fromPlayer = currentPosition - playerPosition;
        float maximumDistance = Mathf.Max(2f, exposedMaximumPlayerDistance);
        if (fromPlayer.sqrMagnitude <= maximumDistance * maximumDistance)
        {
            return;
        }

        if (fromPlayer.sqrMagnitude <= 0.0001f)
        {
            fromPlayer = Vector2.up;
        }

        Bounds activeBounds = phaseTwoActive ? currentArenaBounds : cameraSafeBounds;
        float inset = Mathf.Max(0.5f, repositionClearanceRadius);
        Vector2 candidate = playerPosition + fromPlayer.normalized * maximumDistance;
        candidate.x = Mathf.Clamp(candidate.x, activeBounds.min.x + inset, activeBounds.max.x - inset);
        candidate.y = Mathf.Clamp(candidate.y, activeBounds.min.y + inset, activeBounds.max.y - inset);

        if (IsRepositionLocationClear(candidate))
        {
            transform.position = candidate;
            Physics2D.SyncTransforms();
            return;
        }

        Vector2 fallback = activeBounds.center;
        if (IsRepositionLocationClear(fallback))
        {
            transform.position = fallback;
            Physics2D.SyncTransforms();
        }
    }

    private void CleanupArena()
    {
        ReleasePlayerArenaConstraint();
        HideNextArenaFrame();

        if (reflectorPlates != null)
        {
            for (int i = 0; i < reflectorPlates.Length; i++)
            {
                PhaseReflectorPlate plate = reflectorPlates[i];
                if (plate == null)
                {
                    continue;
                }

                plate.CancelTransitionTweens();
                plate.CancelPresentationPulse();
                plate.SetGameplayEnabled(false);
                plate.SetPresentationVisible(false);
            }
        }

        if (arenaPresentationRoot != null)
        {
            arenaPresentationRoot.gameObject.SetActive(false);
            Destroy(arenaPresentationRoot.gameObject);
            arenaPresentationRoot = null;
        }

        for (int i = 0; i < ArenaWallCount; i++)
        {
            arenaWalls[i] = null;
            nextArenaFrameLines[i] = null;
            outerDarknessLines[i] = null;
        }

        if (arenaPresentationMaterial != null)
        {
            Destroy(arenaPresentationMaterial);
            arenaPresentationMaterial = null;
        }

        phaseTwoActive = false;
        phaseTwoRoutePrepared = false;
        combatCyclePhase = CombatCyclePhase.Normal;
        currentShrinkStage = 0;
        specialOpenSlotIndex = -1;
    }

    private void BuildLaserChain(Vector2 initialDirection)
    {
        laserChainLocked = false;
        segmentCount = 0;
        reflectionCount = 0;

        for (int i = 0; i < MaximumLaserSegments; i++)
        {
            segmentReflectors[i] = null;
            segmentHitReflectors[i] = null;
            segmentBlockingColliders[i] = null;
        }

        Vector2 segmentOrigin = ResolveLaserOrigin();
        Vector2 castOrigin = segmentOrigin;
        Vector2 direction = initialDirection.sqrMagnitude > 0.0001f
            ? initialDirection.normalized
            : Vector2.down;
        float width = Mathf.Max(
            0.05f,
            phaseTwoActive ? specialInitialLaserWidth : normalInitialLaserWidth
        );
        float remainingDistance = Mathf.Max(
            0.1f,
            phaseTwoActive ? phaseTwoLaserRange : laserRange
        );
        int reflectionLimit = Mathf.Clamp(maxReflections, 0, MaximumReflections);

        while (segmentCount < MaximumLaserSegments && remainingDistance > 0.05f)
        {
            float maximumDistance = ResolveDistanceToBounds(
                castOrigin,
                direction,
                remainingDistance
            );
            Vector2 segmentEnd = segmentOrigin + direction * maximumDistance;
            PhaseReflectorPlate hitReflector = null;
            Vector2 hitPoint = segmentEnd;

            if (TryFindNearestLaserHit(
                castOrigin,
                direction,
                maximumDistance,
                out RaycastHit2D nearestHit))
            {
                hitPoint = nearestHit.point;
                hitReflector = ResolveReflector(nearestHit.collider);
            }

            segmentStarts[segmentCount] = segmentOrigin;
            segmentEnds[segmentCount] = hitPoint;
            segmentWidths[segmentCount] = width;

            bool canReflect = hitReflector != null && reflectionCount < reflectionLimit;
            segmentHitReflectors[segmentCount] = hitReflector;
            segmentBlockingColliders[segmentCount] = nearestHit.collider;
            segmentReflectors[segmentCount] = canReflect ? hitReflector : null;
            float traveledDistance = Vector2.Distance(segmentOrigin, hitPoint);
            segmentCount++;
            remainingDistance = Mathf.Max(0f, remainingDistance - traveledDistance);

            if (!canReflect || segmentCount >= MaximumLaserSegments)
            {
                break;
            }

            direction = hitReflector.Reflect(direction);
            segmentOrigin = hitPoint;
            castOrigin = hitPoint + direction * ReflectionRayOffset;
            remainingDistance = Mathf.Max(0f, remainingDistance - ReflectionRayOffset);
            reflectionCount++;
            width *= phaseTwoActive ? specialReflectionWidthMultiplier : 0.5f;
        }
    }

    private bool TryScoreCurrentRoute(
        bool requireReflection,
        bool enforceNormalCameraLimit,
        out float score,
        out float threatDistance,
        out int threatSegmentIndex)
    {
        score = float.PositiveInfinity;
        threatDistance = float.PositiveInfinity;
        threatSegmentIndex = -1;

        if (!hasLockedTarget || segmentCount <= 0 ||
            (requireReflection && reflectionCount <= 0))
        {
            return false;
        }

        float totalLength = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            float segmentLength = Vector2.Distance(segmentStarts[i], segmentEnds[i]);
            if (segmentLength < minimumReadableSegmentLength)
            {
                return false;
            }

            totalLength += segmentLength;
            float distance = DistancePointToSegment(
                lockedTargetPosition,
                segmentStarts[i],
                segmentEnds[i]
            );
            float damageRadius = segmentWidths[i] * 0.5f + targetThreatPadding;
            if (distance <= damageRadius && distance < threatDistance)
            {
                threatDistance = distance;
                threatSegmentIndex = i;
            }
        }

        if (threatSegmentIndex < 0)
        {
            return false;
        }

        float cameraMultiplier = ResolveRequiredAttackCameraMultiplier(threatSegmentIndex);
        if (enforceNormalCameraLimit &&
            cameraMultiplier > maximumAttackCameraZoomMultiplier)
        {
            return false;
        }

        float reflectionPreference = requireReflection
            ? Mathf.Max(0, MaximumReflections - reflectionCount) * 0.3f
            : 1.25f;
        score = threatDistance * 8f +
            cameraMultiplier * 0.75f +
            totalLength * 0.006f +
            reflectionPreference;
        return true;
    }

    private static float DistancePointToSegment(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        return Vector2.Distance(
            point,
            ClosestPointOnSegment(point, segmentStart, segmentEnd)
        );
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.000001f)
        {
            return segmentStart;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
        return segmentStart + segment * t;
    }

    private float ResolveRequiredAttackCameraMultiplier(int targetSegmentIndex)
    {
        CacheCombatCameraBaseSize();
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        if (playerHealth != null)
        {
            bounds.Encapsulate(playerHealth.transform.position);
        }

        if (hasLockedTarget)
        {
            bounds.Encapsulate(lockedTargetPosition);
        }

        int lastRelevantSegment = Mathf.Clamp(
            targetSegmentIndex,
            0,
            Mathf.Max(0, segmentCount - 1)
        );
        for (int i = 0; i <= lastRelevantSegment; i++)
        {
            bounds.Encapsulate(segmentStarts[i]);
            Vector2 relevantEnd = i == lastRelevantSegment
                ? ClosestPointOnSegment(
                    lockedTargetPosition,
                    segmentStarts[i],
                    segmentEnds[i]
                )
                : segmentEnds[i];
            bounds.Encapsulate(relevantEnd);

            PhaseReflectorPlate plate = segmentHitReflectors[i];
            if (plate != null && i < lastRelevantSegment)
            {
                bounds.Encapsulate(plate.transform.position);
            }
        }

        Camera activeCamera = gameplayCamera != null
            ? gameplayCamera.GameplayCamera
            : Camera.main;
        float aspect = activeCamera != null
            ? Mathf.Max(0.1f, activeCamera.aspect)
            : 16f / 9f;
        float padding = Mathf.Max(0f, attackCameraPadding);
        float requiredOrthographicSize = Mathf.Max(
            bounds.extents.y + padding,
            (bounds.extents.x + padding) / aspect
        );
        return requiredOrthographicSize /
            Mathf.Max(0.1f, combatCameraBaseOrthographicSize);
    }

    private bool TryFindNearestLaserHit(
        Vector2 origin,
        Vector2 direction,
        float maximumDistance,
        out RaycastHit2D nearestHit)
    {
        nearestHit = default;
        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            laserContactFilter,
            laserRaycastHits,
            maximumDistance
        );
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = laserRaycastHits[i].collider;
            if (!IsValidLaserBlockingCollider(collider) ||
                laserRaycastHits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = laserRaycastHits[i].distance;
            nearestHit = laserRaycastHits[i];
            found = true;
        }

        return found;
    }

    private bool IsValidLaserBlockingCollider(Collider2D collider)
    {
        if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        return playerHealth == null || !collider.transform.IsChildOf(playerHealth.transform);
    }

    private PhaseReflectorPlate ResolveReflector(Collider2D collider)
    {
        if (collider == null || reflectorPlates == null)
        {
            return null;
        }

        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate != null && plate.OwnsCollider(collider))
            {
                return plate;
            }
        }

        return null;
    }

    private float ResolveBiasedPlayerAimAngle(Vector2 origin)
    {
        Vector2 direction = Vector2.down;
        if (playerHealth != null)
        {
            Vector2 toPlayer = (Vector2)playerHealth.transform.position - origin;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                direction = toPlayer.normalized;
            }
        }

        float bias;
        switch (attackSequenceIndex % LasersPerCycle)
        {
            case 0:
                bias = -attackAimBiasDegrees;
                break;
            case 1:
                bias = attackAimBiasDegrees;
                break;
            default:
                bias = 0f;
                break;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + bias;
    }

    private float ResolveDistanceToBounds(
        Vector2 origin,
        Vector2 direction,
        float distanceLimit)
    {
        direction.Normalize();
        float distance = Mathf.Max(0.1f, distanceLimit);

        Bounds activeBounds = phaseTwoActive ? currentArenaBounds : encounterBounds;

        if (direction.x > 0.0001f)
        {
            distance = Mathf.Min(distance, (activeBounds.max.x - origin.x) / direction.x);
        }
        else if (direction.x < -0.0001f)
        {
            distance = Mathf.Min(distance, (activeBounds.min.x - origin.x) / direction.x);
        }

        if (direction.y > 0.0001f)
        {
            distance = Mathf.Min(distance, (activeBounds.max.y - origin.y) / direction.y);
        }
        else if (direction.y < -0.0001f)
        {
            distance = Mathf.Min(distance, (activeBounds.min.y - origin.y) / direction.y);
        }

        return Mathf.Max(0.1f, distance);
    }

    private Vector2 ResolveLaserOrigin()
    {
        return laserOrigin != null ? laserOrigin.position : transform.position;
    }

    private static Vector2 AngleToDirection(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void ShowTelegraphChain(bool locked)
    {
        EnsureTelegraphLines();
        Color color = locked ? lockedTelegraphColor : telegraphColor;
        float widthScale = locked
            ? lockedTelegraphWidthScale
            : previewTelegraphWidthScale;

        for (int i = 0; i < telegraphLines.Length; i++)
        {
            LineRenderer line = telegraphLines[i];
            bool visible = i < segmentCount;

            if (!visible)
            {
                ResetTelegraphLine(line);
                continue;
            }

            line.positionCount = 2;
            line.SetPosition(0, segmentStarts[i]);
            line.SetPosition(1, segmentEnds[i]);
            line.startWidth = segmentWidths[i] * widthScale;
            line.endWidth = segmentWidths[i] * widthScale;
            line.startColor = color;
            line.endColor = color;
            line.enabled = true;
        }
    }

    private void SetTelegraphAlpha(float alpha)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            LineRenderer line = telegraphLines[i];
            Color color = line.startColor;
            color.a = Mathf.Clamp01(alpha);
            line.startColor = color;
            line.endColor = color;
        }
    }

    private IEnumerator RunReflectorOrderTelegraph(
        float presentationTimer,
        bool showChainDuringSequence)
    {
        if (showChainDuringSequence)
        {
            ShowTelegraphChain(false);
            SetTelegraphAlpha(telegraphColor.a);
        }
        else
        {
            HideTelegraphChain();
        }

        float cueDuration = Mathf.Max(0.05f, chainOriginCueDuration);
        float timer = 0f;
        while (timer < cueDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyLockedOriginPulse(presentationTimer + timer);
            yield return null;
        }

        presentationTimer += cueDuration;
        float stepDuration = Mathf.Max(0.08f, reflectorBlinkStepDuration);
        float pulseDuration = Mathf.Min(
            Mathf.Max(0.05f, reflectorBlinkPulseDuration),
            stepDuration
        );
        int reflectorOrder = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            PhaseReflectorPlate plate = segmentHitReflectors[i];
            if (plate == null)
            {
                continue;
            }

            plate.PlayChainTelegraphPulse(pulseDuration, reflectorOrder);
            reflectorOrder++;
            timer = 0f;

            while (timer < stepDuration && !ShouldStopCombat())
            {
                timer += Time.deltaTime;
                ApplyLockedOriginPulse(presentationTimer + timer);
                yield return null;
            }

            presentationTimer += stepDuration;
        }
    }

    private void PulseFiringReflectors()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            PhaseReflectorPlate plate = segmentHitReflectors[i];
            if (plate != null)
            {
                plate.PlayLaserHitPulse();
            }
        }
    }

    private void ResetReflectorPresentation()
    {
        if (reflectorPlates == null)
        {
            return;
        }

        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate != null)
            {
                plate.CancelPresentationPulse();
            }
        }
    }

    private void HideTelegraphChain()
    {
        for (int i = 0; i < telegraphLines.Length; i++)
        {
            ResetTelegraphLine(telegraphLines[i]);
        }
    }

    private static void ResetTelegraphLine(LineRenderer line)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = false;
        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.zero);
        line.startWidth = 0f;
        line.endWidth = 0f;
        line.startColor = Color.clear;
        line.endColor = Color.clear;
    }

    private void SpawnLaserHazards()
    {
        DeactivateLaserHazards();

        if (!laserChainLocked || segmentCount <= 0)
        {
            Debug.LogWarning(
                "Phase Gatekeeper refused to fire without a locked reflection chain.",
                this
            );
            return;
        }

        if (laserHazardPrefab == null)
        {
            Debug.LogWarning("Phase Gatekeeper laser hazard prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject instance = PoolManager.Instance != null
                ? PoolManager.Instance.Get(
                    laserHazardPrefab.gameObject,
                    (segmentStarts[i] + segmentEnds[i]) * 0.5f,
                    Quaternion.identity
                )
                : Instantiate(
                    laserHazardPrefab.gameObject,
                    (segmentStarts[i] + segmentEnds[i]) * 0.5f,
                    Quaternion.identity
                );

            if (instance == null || !instance.TryGetComponent(out BossLaserHazard hazard))
            {
                continue;
            }

            activeLaserHazards[i] = hazard;
            hazard.InitializeBetween(
                segmentStarts[i],
                segmentEnds[i],
                segmentWidths[i],
                laserFireDuration,
                laserDamage,
                laserDamageInterval,
                laserMaterial,
                phaseTwoActive ? specialActiveLaserColor : activeLaserColor,
                laserSortingLayerName,
                laserSortingOrder,
                0.12f,
                2
            );
        }

        BreakLockedEnvironmentalBlockers();
    }

    private void BreakLockedEnvironmentalBlockers()
    {
        float breakDamage = Mathf.Max(1f, environmentBreakDamage);
        int meteorDamage = Mathf.Max(1, Mathf.CeilToInt(breakDamage));

        for (int i = 0; i < segmentCount; i++)
        {
            Collider2D blocker = segmentBlockingColliders[i];
            if (blocker == null || segmentHitReflectors[i] != null)
            {
                continue;
            }

            Vector2 direction = segmentEnds[i] - segmentStarts[i];
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }

            HarvestObjectHealth harvestObject = blocker.GetComponentInParent<HarvestObjectHealth>();
            if (harvestObject != null && !harvestObject.IsDead)
            {
                harvestObject.TakeDamage(breakDamage, segmentEnds[i], direction);
                continue;
            }

            MeteorObstacle meteor = blocker.GetComponentInParent<MeteorObstacle>();
            if (meteor != null)
            {
                meteor.TakeDamage(meteorDamage, segmentEnds[i], direction);
            }
        }
    }

    private void UpdateSpecialLaserTaper(float normalizedTime)
    {
        float widthMultiplier = Mathf.Lerp(
            1f,
            specialBeamEndWidthMultiplier,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime))
        );

        for (int i = 0; i < activeLaserHazards.Length; i++)
        {
            BossLaserHazard hazard = activeLaserHazards[i];
            if (hazard != null && hazard.gameObject.activeSelf)
            {
                hazard.SetRuntimeWidth(segmentWidths[i] * widthMultiplier);
            }
        }
    }

    private void DeactivateLaserHazards()
    {
        for (int i = 0; i < activeLaserHazards.Length; i++)
        {
            BossLaserHazard hazard = activeLaserHazards[i];
            activeLaserHazards[i] = null;

            if (hazard != null && hazard.gameObject.activeSelf)
            {
                hazard.Deactivate();
            }
        }
    }

    private void ClearLaserPresentation()
    {
        StopChargeSpriteAnimation();
        HideTargetMarker();
        HideTelegraphChain();
        DeactivateLaserHazards();
        ResetReflectorPresentation();
        laserChainLocked = false;
    }

    private void EnsureTelegraphLines()
    {
        for (int i = 0; i < telegraphLines.Length; i++)
        {
            if (telegraphLines[i] != null)
            {
                continue;
            }

            GameObject lineObject = new GameObject($"ReflectedLaserTelegraph_{i:00}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.sortingLayerName = laserSortingLayerName;
            line.sortingOrder = laserSortingOrder - 1;
            line.sharedMaterial = laserMaterial;
            ResetTelegraphLine(line);
            telegraphLines[i] = line;
        }
    }

    private void ConfigurePhysicsFilters()
    {
        int laserMask = laserBlockingMask.value != 0
            ? laserBlockingMask.value
            : Physics2D.AllLayers;
        laserContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        laserContactFilter.SetLayerMask(laserMask);

        int repositionMask = repositionBlockingMask.value != 0
            ? repositionBlockingMask.value
            : Physics2D.AllLayers;
        repositionContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        repositionContactFilter.SetLayerMask(repositionMask);
    }

    private bool TryResolveRepositionTarget(out Vector2 target)
    {
        Bounds repositionBounds = phaseTwoActive ? currentArenaBounds : encounterBounds;
        float minX = repositionBounds.min.x + mapEdgePadding;
        float maxX = repositionBounds.max.x - mapEdgePadding;
        float minY = repositionBounds.min.y + mapEdgePadding;
        float maxY = repositionBounds.max.y - mapEdgePadding;

        if (minX >= maxX || minY >= maxY)
        {
            target = transform.position;
            return false;
        }

        Vector2 playerPosition = playerHealth != null
            ? playerHealth.transform.position
            : repositionBounds.center;
        float minimumDistanceSqr = minimumPlayerRepositionDistance *
                                   minimumPlayerRepositionDistance;
        float maximumDistance = Mathf.Max(
            minimumPlayerRepositionDistance + 2f,
            maximumNormalPlayerRepositionDistance
        );
        float maximumDistanceSqr = maximumDistance * maximumDistance;
        int attempts = Mathf.Clamp(repositionCandidateAttempts, 1, 24);

        for (int i = 0; i < attempts; i++)
        {
            int sample = repositionSequenceIndex * attempts + i + 1;
            float xRatio = Mathf.Repeat(sample * 0.6180339f, 1f);
            float yRatio = Mathf.Repeat(sample * 0.4142136f + 0.2718281f, 1f);
            Vector2 candidate = new Vector2(
                Mathf.Lerp(minX, maxX, xRatio),
                Mathf.Lerp(minY, maxY, yRatio)
            );

            float playerDistanceSqr = (candidate - playerPosition).sqrMagnitude;
            if (playerDistanceSqr < minimumDistanceSqr ||
                (!phaseTwoActive && playerDistanceSqr > maximumDistanceSqr) ||
                !IsRepositionLocationClear(candidate))
            {
                continue;
            }

            repositionSequenceIndex++;
            target = candidate;
            return true;
        }

        repositionSequenceIndex++;
        target = transform.position;
        return false;
    }

    private bool IsRepositionLocationClear(Vector2 position)
    {
        int count = Physics2D.OverlapCircle(
            position,
            repositionClearanceRadius,
            repositionContactFilter,
            repositionOverlapResults
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = repositionOverlapResults[i];
            if (collider != null &&
                !collider.isTrigger &&
                !collider.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator FadeBodyColor(Color from, Color to, float duration)
    {
        if (bodyRenderer == null)
        {
            float waitTimer = 0f;
            while (waitTimer < duration && !ShouldStopCombat())
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            yield break;
        }

        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (timer < safeDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            bodyRenderer.color = Color.Lerp(from, to, Mathf.Clamp01(timer / safeDuration));
            yield return null;
        }

        if (!ShouldStopCombat())
        {
            bodyRenderer.color = to;
        }
    }

    private IEnumerator PulseChargingPresentation(float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);

        while (timer < safeDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        StopChargeSpriteAnimation();
        RestoreRevealedPresentation();
    }

    private void ApplyChargingBodyPulse(float timer)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        UpdateChargeSpriteAnimation();
        float pulse = 0.35f + Mathf.PingPong(timer * 3.5f, 0.65f);
        bodyRenderer.color = Color.Lerp(revealedColor, chargingColor, pulse);
    }

    private void ApplyLockedOriginPulse(float timer)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        UpdateChargeSpriteAnimation();
        float pulse = 0.75f + Mathf.PingPong(timer * 4.5f, 0.25f);
        bodyRenderer.color = Color.Lerp(revealedColor, chargingColor, pulse);
        bodyRenderer.transform.localScale = bodyBaseScale * Mathf.Lerp(1.04f, 1.12f, pulse);
    }

    private void RestoreRevealedPresentation()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.transform.localScale = bodyBaseScale;
        bodyRenderer.color = revealedColor;
        SetIdleBossSprite();
    }

    private void BeginChargeSpriteAnimation()
    {
        chargeAnimationStartTime = Time.time;
        chargeAnimationPlaying = chargeSprites != null && chargeSprites.Length > 0;
        UpdateChargeSpriteAnimation();
    }

    private void UpdateChargeSpriteAnimation()
    {
        if (!chargeAnimationPlaying || bodyRenderer == null ||
            chargeSprites == null || chargeSprites.Length == 0)
        {
            return;
        }

        int frameIndex = Mathf.Min(
            chargeSprites.Length - 1,
            Mathf.FloorToInt(
                Mathf.Max(0f, Time.time - chargeAnimationStartTime) /
                Mathf.Max(0.04f, chargeFrameDuration)
            )
        );
        Sprite frame = chargeSprites[frameIndex];
        if (frame != null)
        {
            bodyRenderer.sprite = frame;
        }
    }

    private void StopChargeSpriteAnimation()
    {
        chargeAnimationPlaying = false;
        SetIdleBossSprite();
    }

    private void SetIdleBossSprite()
    {
        if (bodyRenderer != null && idleSprite != null)
        {
            bodyRenderer.sprite = idleSprite;
        }
    }

    private void AcquireIntroLocks()
    {
        gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        expeditionHud = FindFirstObjectByType<ExpeditionHUD>();
        CacheCombatCameraBaseSize();

        playerController?.SetExternalControlLocked(this, true);
        playerHealth?.AddInvincibleTime(decloakDuration + combatChargeDuration + 0.5f);
        expeditionHud?.SetCinematicMode(this, true);

        if (gameplayCamera != null)
        {
            gameplayCamera.SetCinematicInputOffsetLocked(true);
            gameplayCamera.SetCinematicFocus(transform.position, false);
        }

        introLocksHeld = true;
    }

    private void CacheCombatCameraBaseSize()
    {
        if (combatCameraBaseOrthographicSize > 0.1f || gameplayCamera == null)
        {
            return;
        }

        Camera activeCamera = gameplayCamera.GameplayCamera;
        CameraZoomController2D zoomController =
            gameplayCamera.GetComponent<CameraZoomController2D>();
        if (zoomController == null && activeCamera != null)
        {
            zoomController = activeCamera.GetComponentInParent<CameraZoomController2D>();
        }

        if (zoomController != null)
        {
            combatCameraBaseOrthographicSize = Mathf.Max(
                0.1f,
                zoomController.BaseOrthographicSize
            );
        }
        else if (activeCamera != null)
        {
            combatCameraBaseOrthographicSize = Mathf.Max(
                0.1f,
                activeCamera.orthographicSize
            );
        }
    }

    private void ApplyNormalCombatCameraProfile()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        }

        if (gameplayCamera == null)
        {
            return;
        }

        CacheCombatCameraBaseSize();
        bool acquired = gameplayCamera.AcquireGameplayFramingProfile(
            this,
            Vector2.zero,
            1f,
            Mathf.Clamp(normalCombatCameraZoomMultiplier, 1f, 2f),
            false
        );
        combatCameraProfileHeld |= acquired;
    }

    private void ApplyTrackingCameraProfile(Vector2 trackedTarget)
    {
        if (phaseTwoActive)
        {
            ApplySpecialCombatCameraProfile(currentArenaBounds);
            return;
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        if (playerHealth != null)
        {
            bounds.Encapsulate(playerHealth.transform.position);
        }

        bounds.Encapsulate(trackedTarget);
        ApplyAttackBoundsCameraProfile(bounds);
    }

    private void ApplyLockedAttackCameraProfile()
    {
        if (phaseTwoActive)
        {
            ApplySpecialCombatCameraProfile(currentArenaBounds);
            return;
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        if (playerHealth != null)
        {
            bounds.Encapsulate(playerHealth.transform.position);
        }

        if (hasLockedTarget)
        {
            bounds.Encapsulate(lockedTargetPosition);
        }

        int lastRelevantSegment = threateningSegmentIndex >= 0
            ? Mathf.Min(threateningSegmentIndex, segmentCount - 1)
            : segmentCount - 1;
        for (int i = 0; i <= lastRelevantSegment; i++)
        {
            bounds.Encapsulate(segmentStarts[i]);
            Vector2 relevantEnd = i == lastRelevantSegment && hasLockedTarget
                ? ClosestPointOnSegment(
                    lockedTargetPosition,
                    segmentStarts[i],
                    segmentEnds[i]
                )
                : segmentEnds[i];
            bounds.Encapsulate(relevantEnd);

            PhaseReflectorPlate plate = segmentHitReflectors[i];
            if (plate != null && i < lastRelevantSegment)
            {
                bounds.Encapsulate(plate.transform.position);
            }
        }

        ApplyAttackBoundsCameraProfile(bounds);
    }

    private void ApplyAttackBoundsCameraProfile(Bounds bounds)
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        }

        if (gameplayCamera == null)
        {
            return;
        }

        CacheCombatCameraBaseSize();
        Camera activeCamera = gameplayCamera.GameplayCamera;
        float aspect = activeCamera != null
            ? Mathf.Max(0.1f, activeCamera.aspect)
            : 16f / 9f;
        float padding = Mathf.Max(0f, attackCameraPadding);
        float requiredOrthographicSize = Mathf.Max(
            bounds.extents.y + padding,
            (bounds.extents.x + padding) / aspect
        );
        float multiplier = Mathf.Clamp(
            requiredOrthographicSize / Mathf.Max(0.1f, combatCameraBaseOrthographicSize),
            normalCombatCameraZoomMultiplier,
            maximumAttackCameraZoomMultiplier
        );
        bool acquired = gameplayCamera.AcquireGameplayFramingProfile(
            this,
            Vector2.zero,
            1f,
            multiplier,
            false
        );
        combatCameraProfileHeld |= acquired;

        if (!acquired && !combatCameraProfileHeld)
        {
            return;
        }

        Vector3 focus = bounds.center;
        if (attackCameraFocusHeld)
        {
            gameplayCamera.UpdateCinematicFocus(focus);
        }
        else
        {
            gameplayCamera.SetCinematicFocus(focus, false);
            attackCameraFocusHeld = true;
        }
    }

    private void ReleaseAttackCameraProfile(bool immediate)
    {
        if (phaseTwoActive)
        {
            ApplySpecialCombatCameraProfile(currentArenaBounds);
            return;
        }

        if (attackCameraFocusHeld && gameplayCamera != null)
        {
            gameplayCamera.ClearCinematicFocus(immediate);
        }

        attackCameraFocusHeld = false;
        ApplyNormalCombatCameraProfile();
    }

    private void ApplySpecialCombatCameraProfile(Bounds arenaBounds)
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        }

        if (gameplayCamera == null)
        {
            return;
        }

        CacheCombatCameraBaseSize();
        Camera activeCamera = gameplayCamera.GameplayCamera;
        float aspect = activeCamera != null
            ? Mathf.Max(0.1f, activeCamera.aspect)
            : 16f / 9f;
        float padding = Mathf.Max(0f, specialArenaCameraPadding);
        float requiredOrthographicSize = Mathf.Max(
            arenaBounds.extents.y + padding,
            (arenaBounds.extents.x + padding) / aspect
        );
        float baseSize = Mathf.Max(0.1f, combatCameraBaseOrthographicSize);
        float fittedMultiplier = Mathf.Max(
            normalCombatCameraZoomMultiplier,
            requiredOrthographicSize / baseSize
        );
        bool acquired = gameplayCamera.AcquireGameplayFramingProfile(
            this,
            Vector2.zero,
            1f,
            fittedMultiplier,
            false
        );
        combatCameraProfileHeld |= acquired;

        if (!acquired && !combatCameraProfileHeld)
        {
            return;
        }

        if (specialCameraFocusHeld)
        {
            gameplayCamera.UpdateCinematicFocus(arenaBounds.center);
        }
        else
        {
            gameplayCamera.SetCinematicFocus(arenaBounds.center, false);
            specialCameraFocusHeld = true;
        }
    }

    private void ReleaseSpecialCameraFocus(bool immediate)
    {
        if (!specialCameraFocusHeld)
        {
            return;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.ClearCinematicFocus(immediate);
        }

        specialCameraFocusHeld = false;
    }

    private void ReleaseCombatCameraProfile(bool immediate)
    {
        if (attackCameraFocusHeld && gameplayCamera != null)
        {
            gameplayCamera.ClearCinematicFocus(immediate);
        }

        attackCameraFocusHeld = false;
        ReleaseSpecialCameraFocus(immediate);

        if (combatCameraProfileHeld && gameplayCamera != null)
        {
            gameplayCamera.ReleaseGameplayFramingProfile(this, immediate);
        }

        combatCameraProfileHeld = false;
    }

    private void ReleaseIntroLocks(bool immediateCameraReset)
    {
        if (!introLocksHeld)
        {
            return;
        }

        playerController?.SetExternalControlLocked(this, false);
        expeditionHud?.ReleaseCinematicMode(this);

        if (gameplayCamera != null)
        {
            gameplayCamera.SetCinematicInputOffsetLocked(false);
            gameplayCamera.ClearCinematicFocus(immediateCameraReset);
        }

        introLocksHeld = false;
    }

    private void ApplyDormantState()
    {
        ClearLaserPresentation();
        SetDamageWindow(false);

        if (bodyRenderer != null)
        {
            StopChargeSpriteAnimation();
            bodyRenderer.transform.localScale = bodyBaseScale;
            bodyRenderer.color = stealthColor;
        }
    }

    private void SetDamageWindow(bool enabled)
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = enabled;
        }
    }

    private bool ShouldStopCombat()
    {
        return cleanupComplete ||
               enemyHealth == null ||
               enemyHealth.IsDead ||
               playerHealth == null ||
               playerHealth.IsDead ||
               RunManager.Instance == null ||
               !RunManager.Instance.HasActiveRun ||
               RunManager.Instance.IsCompletingRun;
    }

    private bool IsRegion3EncounterActive()
    {
        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               !RunManager.Instance.IsCompletingRun &&
               RunManager.Instance.CurrentRun.ExpeditionDepth == ExpeditionDepth.DeepZone2 &&
               CampaignProgressionCatalog.GetBossId(ExpeditionDepth.DeepZone2) ==
               CampaignBossId.PhaseGatekeeper;
    }

    private void HandleBossDied(EnemyHealth _)
    {
        CleanupEncounter(false);
    }

    private void HandlePlayerDied()
    {
        CleanupEncounter(true);
    }

    private void SubscribeRunEnd()
    {
        observedRunManager = RunManager.Instance;

        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnsubscribeRunEnd()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        CleanupEncounter(true);
    }

    private void BeginCombatRadarTracking()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (radarTarget == null)
        {
            return;
        }

        radarMarkerSprite = bossRadarMarkerSprite != null
            ? bossRadarMarkerSprite
            : radarTarget.MarkerSprite;
        radarVisibleColor = Color.white;
        radarVisibleScale = Mathf.Max(1f, bossRadarMarkerScale);
        radarTarget.SetVisible(true);
        radarTarget.SetShowOnMap(true);
        radarTarget.SetMarkerType(RadarMarkerType.Boss);
        radarTarget.SetMarkerVisual(
            radarMarkerSprite,
            radarVisibleColor,
            radarVisibleScale
        );
        radarTarget.SetTemporaryReveal(this, float.MaxValue);
    }

    private void SetCombatRadarStealth(bool stealth)
    {
        if (radarTarget == null)
        {
            return;
        }

        radarTarget.SetVisible(true);
        radarTarget.SetMarkerType(RadarMarkerType.Boss);
        radarTarget.SetMarkerVisual(
            radarMarkerSprite,
            radarVisibleColor,
            stealth ? radarVisibleScale * stealthRadarScaleMultiplier : radarVisibleScale
        );
    }

    private void EndCombatRadarTracking()
    {
        if (radarTarget == null)
        {
            return;
        }

        radarTarget.ClearTemporaryReveal(this);
        radarTarget.SetVisible(false);
    }

    private void CleanupEncounter(bool resetCameraImmediately)
    {
        if (cleanupComplete)
        {
            return;
        }

        cleanupComplete = true;
        state = EncounterState.DeadOrCleanup;
        EncounterStarted = null;
        System.Action encounterEndedHandlers = EncounterEnded;
        EncounterEnded = null;

        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleBossDied;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }

        UnsubscribeRunEnd();
        ReleaseIntroLocks(resetCameraImmediately);
        ReleaseCombatCameraProfile(resetCameraImmediately);
        GameAudioLoopController.CancelBossIntroMusic();
        RestorePreparedReflectorsImmediate();
        HideTargetMarker();
        ClearLaserPresentation();
        CleanupArena();
        EndCombatRadarTracking();
        SetDamageWindow(false);
        BossHealthBarUI.Instance?.Hide();

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = false;
        }

        RestoreRevealedPresentation();

        if (targetMarkerObject != null)
        {
            Destroy(targetMarkerObject);
            targetMarkerObject = null;
            targetMarkerLine = null;
        }

        encounterEndedHandlers?.Invoke();
    }

    private void RestorePreparedReflectorsImmediate()
    {
        for (int i = 0; i < preparedRouteReflectorCount; i++)
        {
            PhaseReflectorPlate plate = phaseTwoRouteReflectors[i];
            if (plate == null)
            {
                continue;
            }

            float rotation = phaseTwoActive && plate.ArenaSlotIndex >= 0
                ? ResolveArenaSlotRotation(plate.ArenaSlotIndex)
                : preparedRouteOriginalRotations[i];
            plate.SetWorldPose(plate.transform.position, rotation);
            plate.SetGameplayEnabled(true);
            plate.CancelPresentationPulse();
        }

        ClearPreparedRouteReflectors();
        phaseTwoRoutePrepared = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TryForceStartEncounterForDevelopment(out string failureReason)
    {
        failureReason = string.Empty;

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            failureReason = "No active Run.";
            return false;
        }

        if (RunManager.Instance.IsCompletingRun ||
            RunManager.Instance.CurrentRun.BossDefeated ||
            cleanupComplete ||
            state == EncounterState.DeadOrCleanup ||
            enemyHealth == null ||
            enemyHealth.IsDead)
        {
            failureReason = "Boss encounter is already completed.";
            return false;
        }

        if (introStarted || encounterRoutine != null || state != EncounterState.Dormant)
        {
            failureReason = "Boss encounter is already active.";
            return false;
        }

        if (!encounterConfigured || !IsRegion3EncounterActive())
        {
            failureReason = "Current Region encounter authority was not found.";
            return false;
        }

        if (reflectorPlates == null || reflectorPlates.Length != 12)
        {
            failureReason = "Phase Gatekeeper reflector authority is incomplete.";
            return false;
        }

        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            if (reflectorPlates[i] == null)
            {
                failureReason = "Phase Gatekeeper reflector authority is incomplete.";
                return false;
            }
        }

        PlayerHealth candidate = FindFirstObjectByType<PlayerHealth>();
        if (candidate == null || candidate.IsDead)
        {
            failureReason = "Current Player was not found.";
            return false;
        }

        BeginEncounter(candidate);
        return introStarted;
    }

    [ContextMenu("Region 3 Boss/Begin Encounter")]
    private void DebugBeginEncounter()
    {
        if (!TryForceStartEncounterForDevelopment(out string failureReason))
        {
            Debug.LogWarning(failureReason, this);
        }
    }

    [ContextMenu("Region 3 Boss/Force Stealth")]
    private void DebugForceStealth()
    {
        StopDebugRoutine();
        ClearLaserPresentation();
        state = EncounterState.Stealth;
        SetDamageWindow(false);
        if (bodyRenderer != null)
        {
            bodyRenderer.color = stealthColor;
        }
    }

    [ContextMenu("Region 3 Boss/Force Reposition")]
    private void DebugForceReposition()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunStealthReposition());
    }

    [ContextMenu("Region 3 Boss/Force Decloak And Charge")]
    private void DebugForceDecloakAndCharge()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(DebugDecloakAndChargeRoutine());
    }

    [ContextMenu("Region 3 Boss/Preview Next Reflection Chain")]
    private void DebugPreviewNextReflectionChain()
    {
        EnsureDebugPlayer();
        BuildLaserChain(AngleToDirection(ResolveBiasedPlayerAimAngle(ResolveLaserOrigin())));
        laserChainLocked = true;
        ShowTelegraphChain(true);
        PulseFiringReflectors();
    }

    [ContextMenu("Region 3 Boss/Preview Sequential Reflector Blink")]
    private void DebugPreviewSequentialReflectorBlink()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(DebugSequentialReflectorBlinkRoutine());
    }

    [ContextMenu("Region 3 Boss/Fire Reflected Laser")]
    private void DebugFireReflectedLaser()
    {
        DebugPreviewNextReflectionChain();
        HideTelegraphChain();
        state = EncounterState.Firing;
        SetDamageWindow(false);
        SpawnLaserHazards();
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 1")]
    private void DebugForceLaserCountOne()
    {
        lasersCompletedInCycle = 1;
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 2")]
    private void DebugForceLaserCountTwo()
    {
        lasersCompletedInCycle = 2;
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 3")]
    private void DebugForceLaserCountThree()
    {
        lasersCompletedInCycle = 3;
    }

    [ContextMenu("Region 3 Boss/Force Exposed")]
    private void DebugForceExposed()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunExposedWindow());
    }

    [ContextMenu("Region 3 Boss/Force Convergence")]
    private void DebugForceConvergence()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunReflectorConvergence());
    }

    [ContextMenu("Region 3 Boss/Restore Normal Cycle")]
    private void DebugRestoreNormalCycle()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunSpecialCycleRestore());
    }

    [ContextMenu("Region 3 Boss/Force Shrink 1")]
    private void DebugForceShrinkOne()
    {
        StartDebugRoutine(RunArenaShrink(1));
    }

    [ContextMenu("Region 3 Boss/Force Shrink 2")]
    private void DebugForceShrinkTwo()
    {
        StartDebugRoutine(RunArenaShrink(2));
    }

    [ContextMenu("Region 3 Boss/Force Shrink 3")]
    private void DebugForceShrinkThree()
    {
        StartDebugRoutine(RunArenaShrink(3));
    }

    [ContextMenu("Region 3 Boss/End Exposed")]
    private void DebugEndExposed()
    {
        StopDebugRoutine();
        RestoreRevealedPresentation();
        state = EncounterState.Stealth;
        SetDamageWindow(false);
    }

    [ContextMenu("Region 3 Boss/Toggle Reflection Chain Gizmos")]
    private void DebugToggleReflectionChainGizmos()
    {
        drawReflectionChainGizmos = !drawReflectionChainGizmos;
    }

    [ContextMenu("Region 3 Boss/Toggle Reflector Normal Gizmos")]
    private void DebugToggleReflectorNormalGizmos()
    {
        drawReflectorNormalGizmos = !drawReflectorNormalGizmos;
    }

    [ContextMenu("Region 3 Boss/Log State And Reflection Chain")]
    private void DebugLogState()
    {
        string widths = segmentCount > 0
            ? $"{segmentWidths[0]:0.###}, {segmentWidths[1]:0.###}, " +
              $"{segmentWidths[2]:0.###}, {segmentWidths[3]:0.###}"
            : "none";
        Debug.Log(
            $"Phase Gatekeeper state={state}, cycleLasers={lasersCompletedInCycle}/3, " +
            $"totalLasers={attackSequenceIndex}, segments={segmentCount}, " +
            $"reflections={reflectionCount}, widths=[{widths}], " +
            $"reflectors={reflectorPlates?.Length ?? 0}, phaseTwo={phaseTwoActive}, " +
            $"shrink={currentShrinkStage}/{ArenaShrinkStageCount}, motif={currentRouteMotif}, " +
            $"lockedTarget={lockedTargetPosition}, threatSegment={threateningSegmentIndex}, " +
            $"threatDistance={selectedThreatDistance:0.###}, score={selectedRouteScore:0.###}, " +
            $"attempts={selectedRouteCandidateAttempts}, fallback={selectedRouteFallback}, " +
            $"routes={routeSelectionCount}, reflected={reflectedRouteSelectionCount}, " +
            $"shortFallback={shortFallbackSelectionCount}, " +
            $"directFallback={directFallbackSelectionCount}",
            this
        );
    }

    [ContextMenu("Region 3 Boss/Cleanup Encounter")]
    private void DebugCleanupEncounter()
    {
        CleanupEncounter(true);
    }

    private IEnumerator DebugDecloakAndChargeRoutine()
    {
        state = EncounterState.Decloaking;
        yield return FadeBodyColor(stealthColor, revealedColor, decloakPreparationDuration);
        yield return RunReflectedLaserAttack();
        encounterRoutine = null;
    }

    private IEnumerator DebugSequentialReflectorBlinkRoutine()
    {
        ClearLaserPresentation();
        BuildLaserChain(AngleToDirection(ResolveBiasedPlayerAimAngle(ResolveLaserOrigin())));
        laserChainLocked = true;
        ShowTelegraphChain(false);
        yield return RunReflectorOrderTelegraph(0f, true);

        if (!ShouldStopCombat())
        {
            ShowTelegraphChain(true);
        }

        encounterRoutine = null;
    }

    private void EnsureDebugPlayer()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerController = playerHealth != null
                ? playerHealth.GetComponent<PlayerController2D>()
                : null;
            playerRigidbody = playerHealth != null
                ? playerHealth.GetComponent<Rigidbody2D>()
                : null;
        }
    }

    private void StartDebugRoutine(IEnumerator routine)
    {
        StopDebugRoutine();
        cleanupComplete = false;
        encounterRoutine = StartCoroutine(routine);
    }

    private void StopDebugRoutine()
    {
        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (phaseTwoActive || state == EncounterState.Converging || state == EncounterState.Shrinking)
        {
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.85f);
            Gizmos.DrawWireCube(currentArenaBounds.center, currentArenaBounds.size);

            if (state == EncounterState.Shrinking)
            {
                Gizmos.color = new Color(0.8f, 0.25f, 1f, 0.85f);
                Gizmos.DrawWireCube(nextArenaBounds.center, nextArenaBounds.size);
            }
        }

        if (drawReflectionChainGizmos)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                Gizmos.color = Color.Lerp(Color.cyan, Color.blue, i / 3f);
                Gizmos.DrawLine(segmentStarts[i], segmentEnds[i]);

                PhaseReflectorPlate plate = segmentReflectors[i];
                if (plate != null)
                {
                    Gizmos.color = Color.Lerp(Color.yellow, Color.magenta, i / 3f);
                    Gizmos.DrawWireSphere(plate.transform.position, 0.75f);
                }
            }

            if (hasLockedTarget)
            {
                Gizmos.color = lockedMarkerColor;
                Gizmos.DrawWireSphere(lockedTargetPosition, targetMarkerRadius);
            }

            if (state == EncounterState.Tracking)
            {
                Gizmos.color = trackingMarkerColor;
                Gizmos.DrawWireSphere(predictedTargetPosition, targetMarkerRadius * 0.8f);
            }

            if (threateningSegmentIndex >= 0 && threateningSegmentIndex < segmentCount)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    segmentStarts[threateningSegmentIndex],
                    segmentEnds[threateningSegmentIndex]
                );
            }
        }

        if (!drawReflectorNormalGizmos || reflectorPlates == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate == null)
            {
                continue;
            }

            Vector3 origin = plate.transform.position;
            Gizmos.DrawLine(origin, origin + (Vector3)plate.SurfaceNormal * 1.5f);
        }
    }
#endif
}
