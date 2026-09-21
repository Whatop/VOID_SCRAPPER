using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class TutorialFlowController : MonoBehaviour
{
    private const float ObjectiveCheckInterval = 0.1f;

    [Serializable]
    private sealed class StepPresentation
    {
        public TutorialStep step;
        public GameObject worldRoot;
        [TextArea(1, 2)] public string instructionTemplate;
        public string actionMapName = "Player";
        public string actionName;
        public string fallbackBindingDisplay = "?";
        public bool useCompositeBindingParts;
        public DialogueSystemTrigger dialogueTrigger;
    }

    [Header("Checkpoint")]
    [SerializeField] private TutorialStep currentStep = TutorialStep.IntroCommunication;
    [SerializeField] private StepPresentation[] stepPresentations = Array.Empty<StepPresentation>();
    [SerializeField, Min(0f)] private float introAdvanceDelay = 0.75f;

    [Header("Scene References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform authoredContentRoot;
    [SerializeField] private TutorialPromptUI promptUI;
    [SerializeField] private GungeonStyleCamera2D tutorialCamera;

    [Header("Production Gameplay Sources")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerRadarScanner radarScanner;
    [SerializeField] private PlayerRadarVFXController radarVFX;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private EmergencyReturnController emergencyReturnController;
    [SerializeField] private EmergencyReturnExitSequence emergencyReturnExitSequence;
    [SerializeField] private ExpeditionMenuController expeditionMenuController;
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private ExpeditionMapPanelUI expeditionMapPanel;
    [SerializeField] private MapDiscoveryController mapDiscoveryController;
    [SerializeField] private ExpeditionRoutePlanner routePlanner;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject radarUiRoot;

    [Header("Tutorial Safety")]
    [SerializeField, Min(1f)] private float tutorialMinimumHealth = 10f;

    [Header("Control Confirmation Pacing")]
    [SerializeField, Min(0.5f)] private float movementTravelRequired = 2.75f;
    [SerializeField, Min(0f)] private float movementCompletionDelay = 0.4f;
    [SerializeField, Min(0f)] private float dashCompletionDelay = 0.5f;
    [SerializeField, Min(0f)] private float dialogueGuideHandoffDelay = 0.14f;
    [Tooltip("Incoming cue for explicitly remote tutorial transmissions. Older scenes with an empty value use DialogueCommIncoming.")]
    [SerializeField] private string incomingTransmissionSoundEventId = SoundEventIds.DialogueCommIncoming;

    [Header("Authored 40x40 Layout")]
    [SerializeField] private Vector2 mapCenter = Vector2.zero;
    [SerializeField] private Vector2 mapSize = new Vector2(40f, 40f);
    [SerializeField] private GameObject[] decorationPrefabs = Array.Empty<GameObject>();
    [SerializeField] private Vector2[] decorationPositions = Array.Empty<Vector2>();
    [SerializeField, Min(0)] private int movingSmallMeteorCount = 1;

    [Header("Normal Harvest")]
    [SerializeField] private GameObject harvestTarget;
    [SerializeField] private HarvestObjectHealth harvestTargetHealth;
    [SerializeField] private RadarTarget harvestRadarTarget;
    [SerializeField, Min(4)] private int salvagePlacementAttempts = 20;
    [SerializeField, Min(0.25f)] private float salvagePlacementClearance = 1.25f;
    [SerializeField, Min(0.25f)] private float salvageBoundaryMargin = 2f;
    [SerializeField, Min(0f)] private float salvageViewportMargin = 0.08f;
    [SerializeField] private LayerMask salvagePlacementBlockingLayers;
    [SerializeField] private Vector2[] salvageFallbackPositions = Array.Empty<Vector2>();
    [SerializeField, Min(0.1f)] private float cargoFeedbackDuration = 0.55f;

    [Header("Guaranteed High Value Encounter")]
    [SerializeField] private GameObject highValueSalvagePrefab;
    [SerializeField] private Vector2 highValueSalvagePosition = new Vector2(0f, 0f);
    [SerializeField, Min(0.5f)] private float highValueDiscoveryDistance = 4.5f;
    [FormerlySerializedAs("highValueFocusDuration")]
    [SerializeField, Range(0.5f, 0.7f)] private float targetFocusDuration = 0.6f;
    [FormerlySerializedAs("highValueReturnDuration")]
    [SerializeField, Range(0.4f, 0.6f)] private float targetReturnDuration = 0.5f;
    [SerializeField] private Vector2 targetSafeViewportMin = new Vector2(0.1f, 0.12f);
    [SerializeField] private Vector2 targetSafeViewportMax = new Vector2(0.9f, 0.88f);
    [SerializeField, Range(0.1f, 0.2f)] private float targetSafeViewportHoldDuration = 0.15f;
    [SerializeField] private RewardDefinition highValueTeachingReward;
    [SerializeField] private ReinforcementDefinition defensiveReinforcementDefinition;
    [SerializeField] private GameObject[] combatEnemyPrefabs = Array.Empty<GameObject>();
    [SerializeField] private Vector2[] combatEnemyArrivalPositions = Array.Empty<Vector2>();

    [Header("Signal Relay")]
    [SerializeField] private GameObject radarTarget;
    [SerializeField] private RadarTarget radarScanTarget;
    [SerializeField] private GameObject interactionTarget;
    [SerializeField] private TutorialInteractionTarget interactionTargetComponent;
    [SerializeField] private SpriteRenderer signalDevicePulseRenderer;
    [SerializeField] private GameObject tutorialPurpleEffectPrefab;
    [SerializeField, Range(0.2f, 0.6f)] private float signalDevicePulseDuration = 0.38f;
    [SerializeField, Range(0.15f, 0.5f)] private float fakeOperatorTakeoverDelay = 0.3f;
    [SerializeField, Min(0.5f)] private float proximityAutoRegistrationDistance = 5f;
    [SerializeField] private Vector2 unknownSearchAreaOffset = new Vector2(-2.5f, 1.5f);
    [SerializeField, Min(1f)] private float unknownSearchAreaRadius = 5f;

    [Header("Purple Core")]
    [SerializeField] private GameObject alienSignal;
    [SerializeField] private TutorialInteractionTarget alienSignalTargetComponent;
    [SerializeField] private RadarTarget alienSignalRadarTarget;
    [SerializeField, Min(0.5f)] private float purpleCoreApproachDistance = 2.75f;
    [SerializeField, Min(0.5f)] private float purpleCorePulseDistance = 5f;
    [SerializeField, Min(0.1f)] private float purpleCoreApproachPulseDuration = 0.4f;
    [SerializeField] private Transform alienSignalVisualRoot;
    [SerializeField] private SpriteRenderer alienSignalCoreRenderer;
    [SerializeField] private SpriteRenderer alienSignalPulseRenderer;
    [SerializeField, Range(0.65f, 0.75f)] private float purpleCoreRevealStartScale = 0.7f;
    [SerializeField, Range(0.45f, 0.65f)] private float purpleCoreRevealDuration = 0.55f;
    [SerializeField, Range(0.08f, 0.15f)] private float purpleCoreIdleFloatDistance = 0.1f;
    [SerializeField, Range(1.5f, 2f)] private float purpleCoreIdleFloatDuration = 1.75f;
    [SerializeField, Min(1f)] private float purpleCoreForcedInteractionDamage = 15f;
    [SerializeField, Range(0.05f, 0.2f)] private float purpleCoreDamageContributionWindow = 0.12f;
    [SerializeField, Min(0.5f)] private float purpleCoreDamageContributionCap = 4f;
    [SerializeField, Range(0.08f, 0.18f)] private float purpleCoreHitPunchDuration = 0.12f;

    [Header("Pixel Curse Transformation")]
    [SerializeField] private TraitDefinition pixelCurseDefinition;
    [SerializeField] private PlayerVisualStateController playerVisualStateController;
    [SerializeField] private StatusEffectHUDPresenter statusEffectPresenter;
    [SerializeField, Min(0.1f)] private float acquisitionFeedbackDuration = 0.75f;
    [FormerlySerializedAs("corruptionDarkenDuration")]
    [SerializeField, Range(0.08f, 0.15f)] private float curseImpactPauseDuration = 0.12f;
    [SerializeField, Range(0.25f, 0.4f)] private float curseFadeOutDuration = 0.32f;
    [FormerlySerializedAs("corruptionRecoveryDuration")]
    [SerializeField, Range(0.3f, 0.45f)] private float curseFadeInDuration = 0.36f;
    [SerializeField, Min(0f)] private float corruptionShakeAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float corruptionShakeDuration = 0.24f;
    [FormerlySerializedAs("corruptionPeakDelay")]
    [SerializeField, Range(0.45f, 0.7f)] private float curseTransferDuration = 0.58f;
    [SerializeField, Range(0.12f, 0.2f)] private float curseCoreAbsorptionDuration = 0.16f;
    [SerializeField, Range(0.25f, 0.4f)] private float curseShipCorruptionDuration = 0.32f;
    [SerializeField, Range(1.5f, 2f)] private float curseTransferImpactScale = 1.75f;
    [SerializeField] private Color alienSignalPeakColor = new Color(0.75f, 0.2f, 1f, 1f);
    [SerializeField, Range(0.15f, 0.2f)] private float curseLetterboxDuration = 0.18f;
    [SerializeField, Range(0.45f, 0.7f)] private float curseCameraFocusDuration = 0.58f;
    [SerializeField, Range(0.55f, 0.9f)] private float curseCameraZoomMultiplier = 0.78f;
    [SerializeField, Range(0.05f, 0.12f)] private float curseLetterboxHeightRatio = 0.075f;

    [Header("Settlement Communication")]
    [SerializeField] private string openingConversation = "TUTORIAL_OperatorOpening";
    [SerializeField] private string radarConversation = "TUTORIAL_OperatorRadar";
    [SerializeField] private string supplyContainerConversation =
        "TUTORIAL_OperatorSupplyContainer";
    [SerializeField] private string ancientSignalConversation =
        "TUTORIAL_OperatorAncientSignal";
    [SerializeField] private string signalRelayAnalysisConversation =
        "TUTORIAL_SignalRelayAnalysis";
    [SerializeField] private string fakeOperatorTakeoverConversation =
        "TUTORIAL_FakeOperatorTakeover";
    [SerializeField] private string unknownAccessKeyConversation =
        "TUTORIAL_UnknownAccessKeyContact";
    [SerializeField, Min(0f)] private float rescueSignalDelay = 0.7f;

    [Header("Manual Emergency Return")]
    [SerializeField] private ReinforcementDefinition emergencyReturnDefinition;
    [SerializeField] private ReinforcementPickup reinforcementPickupPrefab;
    [SerializeField, Min(0.1f)] private float pickupSpawnClearance = 0.7f;

    private readonly List<GameObject> spawnedDecorations = new List<GameObject>(8);
    private readonly List<EnemyHealth> activeCombatEnemies = new List<EnemyHealth>(4);
    private readonly Collider2D[] placementBuffer = new Collider2D[24];
    private readonly Dictionary<string, string> localizationArguments =
        new Dictionary<string, string>(2, StringComparer.Ordinal);
    private readonly TutorialStoryGuidanceProgress operatorGuidanceProgress =
        new TutorialStoryGuidanceProgress();
    private readonly TutorialRadarGuidanceProgress radarGuidanceProgress =
        new TutorialRadarGuidanceProgress();
    private readonly TutorialTargetPresentationProgress targetPresentationProgress =
        new TutorialTargetPresentationProgress();
    private readonly TutorialOpeningTransmissionProgress openingTransmissionProgress =
        new TutorialOpeningTransmissionProgress();
    private readonly TutorialCorePresentationProgress corePresentationProgress =
        new TutorialCorePresentationProgress();
    private readonly TutorialRelayNarrativeProgress relayNarrativeProgress =
        new TutorialRelayNarrativeProgress();
    private readonly TutorialPurpleCoreAcquisitionLatch coreAcquisitionLatch =
        new TutorialPurpleCoreAcquisitionLatch();

    private bool isCompleting;
    private bool tutorialRunInitialized;
    private bool eventsSubscribed;
    private bool normalHarvestDestroyed;
    private bool highValueDestroyed;
    private bool normalSalvagePositioned;
    private bool unknownMissionPresented;
    private bool combatSpawned;
    private bool openingConversationStarted;
    private bool highValueAutoRegistered;
    private bool signalDeviceAutoRegistered;
    private bool alienSignalApproachPulsePlayed;
    private bool emergencyReturnPickupSpawned;
    private bool warnedMissingProductionMenu;
    private bool awaitingRescueConversationEnd;
    private bool awaitingUnknownAccessKeyConversationEnd;
    private int cargoLoadBeforePickup;
    private int routeWaypointCountBeforeStep;
    private PlayerWeaponBase observedWeapon;
    private HarvestObjectHealth highValueSalvageHealth;
    private RadarTarget highValueSalvageRadarTarget;
    private GameObject highValueSalvageInstance;
    private ReinforcementPickup emergencyReturnPickup;
    private RunManager subscribedRunManager;
    private Coroutine introAdvanceRoutine;
    private Coroutine cargoFeedbackRoutine;
    private Coroutine objectiveCheckRoutine;
    private Coroutine unknownMissionRoutine;
    private Coroutine curseTransformationRoutine;
    private Coroutine rescueSequenceRoutine;
    private Coroutine controlPacingRoutine;
    private Coroutine targetPresentationRoutine;
    private Coroutine relayNarrativeRoutine;
    private Vector2 lastMovementSamplePosition;
    private float accumulatedMovementDistance;
    private Sequence alienSignalTween;
    private Sequence alienSignalRevealTween;
    private Tween alienSignalIdleTween;
    private Sequence alienSignalIdleWaveTween;
    private Sequence curseInfiltrationTween;
    private Sequence signalDevicePulseTween;
    private Sequence tutorialPurpleEffectTween;
    private Sequence purpleCoreHitTween;
    private Tween playerCurseImpactTween;
    private Tween dialogueGuideHandoffTween;
    private GameplayPauseManager transformationPauseManager;
    private bool ownsTransformationScreenFade;
    private DialogueSystemController rescueDialogueController;
    private DialogueSystemController unknownAccessKeyDialogueController;
    private DialogueSystemController relayDialogueController;
    private string activeRelayConversationTitle = string.Empty;
    private RadarTarget unknownSignalObjectiveTarget;
    private bool ownsTargetPresentationInput;
    private readonly object discoveryCameraOwner = new object();
    private GungeonStyleCamera2D targetPresentationCamera;
    private Transform targetPresentationPlayer;
    private bool targetPresentationAwaitingRediscovery;
    private PlayerController2D lockedPresentationPlayer;
    private PlayerWeaponController lockedPresentationWeapon;
    private PlayerRadarScanner lockedPresentationRadar;
    private PlayerInteractor lockedPresentationInteractor;
    private PlayerReinforcementController lockedPresentationReinforcement;
    private ExpeditionMenuController lockedPresentationMenu;
    private TutorialStep activeTargetTriggerStep = TutorialStep.Complete;
    private TutorialStep activeTargetGuidanceStep = TutorialStep.Complete;
    private Transform activePresentationTarget;
    private HarvestObjectHealth activePresentationHealth;
    private Collider2D activePresentationCollider;
    private Renderer activePresentationRenderer;
    private string activeTargetConversationTitle = string.Empty;
    private TutorialStep activeOperatorGuidanceStep = TutorialStep.Complete;
    private string activeOperatorConversationTitle = string.Empty;
    private Vector3 alienSignalVisualRestScale = Vector3.one;
    private Vector3 alienSignalPulseRestScale = Vector3.one;
    private Color alienSignalCoreRestColor = Color.white;
    private Color alienSignalPulseRestColor = Color.white;
    private Vector3 alienSignalPulseRestLocalPosition;
    private Vector3 alienSignalVisualRestLocalPosition;
    private SpriteRenderer[] alienSignalVisualRenderers = Array.Empty<SpriteRenderer>();
    private Color[] alienSignalVisualRestColors = Array.Empty<Color>();
    private Collider2D[] alienSignalColliders = Array.Empty<Collider2D>();
    private bool[] alienSignalColliderRestStates = Array.Empty<bool>();
    private Transform playerCurseImpactVisual;
    private Vector3 playerCurseImpactRestScale = Vector3.one;
    private Color signalDevicePulseRestColor = Color.white;
    private bool signalDevicePulseStateCached;
    private GameObject activeTutorialPurpleEffect;
    private SpriteRenderer activeTutorialPurpleEffectRenderer;
    private Animator activeTutorialPurpleEffectAnimator;
    private DashShockwaveVFX activeTutorialPurpleShockwave;
    private Vector3 activeTutorialPurpleEffectRestScale = Vector3.one;
    private Color activeTutorialPurpleEffectRestColor = Color.white;
    private bool activeTutorialPurpleEffectFromPool;
    private BossCinematicLetterboxUI curseLetterbox;
    private bool ownsCurseLetterbox;
    private bool ownsCurseCameraPresentation;
    private TutorialPurpleCoreDamageReceiver alienSignalDamageReceiver;
    private Transform alienSignalCoreTravelTransform;
    private Transform alienSignalCoreOriginalParent;
    private Vector3 alienSignalCoreOriginalLocalPosition;
    private Quaternion alienSignalCoreOriginalLocalRotation = Quaternion.identity;
    private Vector3 alienSignalCoreOriginalLocalScale = Vector3.one;
    private bool alienSignalCoreOriginalActive;
    private bool alienSignalCoreTravelStateCached;

    public TutorialStep CurrentStep => currentStep;
    public bool IsCompleting => isCompleting;
    public Transform PlayerRoot => playerRoot;
    public GameObject HarvestTarget => harvestTarget;
    public GameObject RadarTarget => radarTarget;
    public GameObject InteractionTarget => interactionTarget;
    public GameObject AlienSignal => alienSignal;
    public TutorialTargetPresentationKind ActiveTargetPresentationKind =>
        targetPresentationProgress.Kind;
    public TutorialTargetPresentationPhase TargetPresentationPhase =>
        targetPresentationProgress.Phase;
    public bool HasConfirmedOpeningMovement =>
        openingTransmissionProgress.HasConfirmedMovement;
    public TutorialCorePresentationPhase CorePresentationPhase =>
        corePresentationProgress.Phase;
    public TutorialRelayNarrativePhase RelayNarrativePhase =>
        relayNarrativeProgress.Phase;
    public float PurpleCoreAccumulatedDamage =>
        alienSignalDamageReceiver != null
            ? alienSignalDamageReceiver.AccumulatedDamage
            : 0f;
    public float PurpleCoreRevealInitialScale =>
        Mathf.Clamp(purpleCoreRevealStartScale, 0.65f, 0.75f);
    public float PurpleCoreRevealSeconds =>
        Mathf.Clamp(purpleCoreRevealDuration, 0.45f, 0.65f);
    public float CurseTransferSeconds =>
        Mathf.Clamp(curseTransferDuration, 0.45f, 0.7f);
    public Vector2 TargetSafeViewportMin => targetSafeViewportMin;
    public Vector2 TargetSafeViewportMax => targetSafeViewportMax;
    public float TargetSafeViewportHoldSeconds =>
        Mathf.Clamp(targetSafeViewportHoldDuration, 0.1f, 0.2f);
    public GameObject TutorialPurpleEffectPrefab => tutorialPurpleEffectPrefab;

    public event Action<TutorialStep, TutorialStep> StepChanged;

    private void Awake()
    {
        CacheRuntimeReferences();
        CacheAlienSignalVisualState();
    }

    private void OnEnable()
    {
        CacheRuntimeReferences();
        ApplyTutorialHealthFloor();
        SubscribeGameplayEvents();
        RefreshRelayNarrative();
        if (corePresentationProgress.Phase == TutorialCorePresentationPhase.Ready)
        {
            StartAlienSignalIdleMotion();
        }
    }

    private void Start()
    {
        if (!EnsureFreshTutorialRun())
        {
            Debug.LogError(
                "Tutorial initialization stopped because the Boot-owned production run services are unavailable. " +
                "Enter Tutorial through Boot so physical pickups and progression use the authoritative RunContext.",
                this
            );
            enabled = false;
            return;
        }

        EnableProductionRuntimeComponents();
        ApplyTutorialHealthFloor();
        InitializeAuthoredMap();
        CreateAuthoredDecorations();
        CreateHighValueSalvage();
        RebindRuntimeEvents();
        ResumeFromPersistentStoryCheckpoint();
        PlacePlayerAtSpawn();
        ApplyCurrentStepState();
    }

    private void OnDisable()
    {
        // Release discovery ownership before unrelated teardown can touch objects
        // already destroyed by a scene exit. StopAllStepRoutines may repeat this.
        StopTargetPresentation(true, true);
        ClearTutorialHealthFloor();
        StopAllStepRoutines();
        StopRelayNarrative(true);
        StopRescueSequence();
        StopWaitingForUnknownAccessKeyConversation(true);
        StopCurseTransformation();
        StopSignalDevicePulse();
        ResetPurpleCorePresentation(false);
        ClearPurpleCoreRadarReveal();
        ClearUnknownSignalObjectiveMarker();
        promptUI?.Hide();
        expeditionHUD?.HideObjectiveBriefing();
        expeditionMapPanel?.ClearExternalObjective();
        expeditionMapPanel?.ClearExternalSearchRegion();
        UnsubscribeGameplayEvents();
        UnsubscribeSpawnedCombatEnemies();
    }

    private void OnDestroy()
    {
        StopTargetPresentation(true, true);
        StopRelayNarrative(true);
        StopSignalDevicePulse();
        StopCurseTransformation();
        ResetPurpleCorePresentation(true);
        ClearTutorialHealthFloor();
        ClearUnknownSignalObjectiveMarker();
        expeditionMapPanel?.ClearExternalObjective();
        expeditionMapPanel?.ClearExternalSearchRegion();

        if (highValueSalvageInstance != null)
        {
            Destroy(highValueSalvageInstance);
        }

        for (int i = 0; i < spawnedDecorations.Count; i++)
        {
            if (spawnedDecorations[i] != null)
            {
                Destroy(spawnedDecorations[i]);
            }
        }
    }

    public bool TryAdvanceCheckpoint(TutorialStep completedStep)
    {
        if (isCompleting || completedStep != currentStep || currentStep >= TutorialStep.Complete)
        {
            return false;
        }

        SetStep((TutorialStep)((int)currentStep + 1));
        return true;
    }

    public void SetStep(TutorialStep nextStep)
    {
        if (isCompleting || !Enum.IsDefined(typeof(TutorialStep), nextStep) || currentStep == nextStep)
        {
            return;
        }

        TutorialStep previousStep = currentStep;
        KillDialogueGuideHandoff();
        StopStepOwnedRoutine(previousStep);

        if (nextStep == TutorialStep.CollectResources)
        {
            cargoLoadBeforePickup = ResolveCurrentCargoLoad();
        }

        if (nextStep == TutorialStep.RoutePing)
        {
            routeWaypointCountBeforeStep = routePlanner != null ? routePlanner.WaypointCount : 0;
        }

        if (nextStep == TutorialStep.RadarDiscoverSalvage)
        {
            radarGuidanceProgress.Reset();
        }

        if (nextStep == TutorialStep.Move && playerRoot != null)
        {
            openingTransmissionProgress.Reset();
            accumulatedMovementDistance = 0f;
            lastMovementSamplePosition = playerRoot.position;
        }

        if (nextStep == TutorialStep.TravelPurpleCore)
        {
            alienSignalApproachPulsePlayed = false;
        }

        currentStep = nextStep;
        ApplyCurrentStepState();
        StepChanged?.Invoke(previousStep, currentStep);
    }

    public void StartCurrentStepDialogue()
    {
        StepPresentation presentation = FindPresentation(currentStep);

        if (presentation == null || presentation.dialogueTrigger == null)
        {
            Debug.LogWarning($"No DialogueSystemTrigger is assigned for tutorial step {currentStep}.", this);
            return;
        }

        presentation.dialogueTrigger.TryStart(playerRoot);
    }

    [ContextMenu("Tutorial/Complete Tutorial (Development)")]
    public void CompleteTutorial()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MarkTutorialCompletedAfterReturn();
#else
        Debug.LogWarning("Tutorial checkpoint controls are only available in the Editor or Development Builds.", this);
#endif
    }

    [ContextMenu("Tutorial/Advance Step (Development)")]
    public void AdvanceDevelopmentStep()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TryAdvanceCheckpoint(currentStep);
#else
        Debug.LogWarning("Tutorial checkpoint controls are only available in the Editor or Development Builds.", this);
#endif
    }

    [ContextMenu("Tutorial/Reset Step (Development)")]
    public void ResetDevelopmentStep()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isCompleting = false;
        normalHarvestDestroyed = false;
        highValueDestroyed = false;
        normalSalvagePositioned = false;
        unknownMissionPresented = false;
        combatSpawned = false;
        openingConversationStarted = false;
        activeOperatorConversationTitle = string.Empty;
        operatorGuidanceProgress.Reset();
        targetPresentationAwaitingRediscovery = false;
        openingTransmissionProgress.Reset();
        corePresentationProgress.Reset();
        relayNarrativeProgress.Reset();
        coreAcquisitionLatch.Reset();
        alienSignalDamageReceiver?.ResetProgress();
        radarGuidanceProgress.Reset();
        targetPresentationProgress.Reset();
        awaitingUnknownAccessKeyConversationEnd = false;
        highValueAutoRegistered = false;
        signalDeviceAutoRegistered = false;
        StopSignalDevicePulse();
        interactionTargetComponent?.ResetTarget();
        activeOperatorGuidanceStep = TutorialStep.Complete;
        emergencyReturnPickupSpawned = false;
        StopTargetPresentation(true, true);
        ResetPurpleCorePresentation(true);
        SetStep(TutorialStep.IntroCommunication);
#else
        Debug.LogWarning("Tutorial checkpoint controls are only available in the Editor or Development Builds.", this);
#endif
    }

    private bool EnsureFreshTutorialRun()
    {
        RunManager runManager = RunManager.Instance;

        if (runManager == null)
        {
            Debug.LogError("Tutorial requires the persistent Boot-owned RunManager.", this);
            return false;
        }

        if (tutorialRunInitialized && runManager.HasActiveRun)
        {
            return true;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        WeaponTreeType weaponTree = progress != null
            ? progress.LastSelectedWeaponTree
            : WeaponTreeType.MachineGun;
        string shipId = progress != null ? progress.SelectedShipId : "basic_ship";

        // A Tutorial death reloads this scene. Starting the authored Tutorial run
        // again clears partial cargo and combat state without inventing a second run model.
        runManager.StartNewRun(weaponTree, ExpeditionDepth.Normal, shipId);
        tutorialRunInitialized = runManager.HasActiveRun;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Tutorial);
        }

        return tutorialRunInitialized;
    }

    private void EnableProductionRuntimeComponents()
    {
        if (cargoController != null)
        {
            cargoController.enabled = true;
        }

        if (reinforcementController != null)
        {
            reinforcementController.enabled = true;
        }

        if (emergencyReturnExitSequence != null)
        {
            emergencyReturnExitSequence.enabled = true;
        }

        if (emergencyReturnController != null)
        {
            emergencyReturnController.enabled = true;
        }
    }

    private void InitializeAuthoredMap()
    {
        if (mapDiscoveryController == null)
        {
            mapDiscoveryController = FindFirstObjectByType<MapDiscoveryController>(FindObjectsInactive.Include);
        }

        if (mapDiscoveryController == null)
        {
            mapDiscoveryController = gameObject.AddComponent<MapDiscoveryController>();
        }

        Vector2 safeSize = new Vector2(Mathf.Max(1f, mapSize.x), Mathf.Max(1f, mapSize.y));
        mapDiscoveryController.Initialize(
            new Bounds(mapCenter, new Vector3(safeSize.x, safeSize.y, 1f))
        );
    }

    private void CreateAuthoredDecorations()
    {
        if (decorationPrefabs == null || decorationPositions == null)
        {
            return;
        }

        int count = Mathf.Min(decorationPrefabs.Length, decorationPositions.Length);
        int smallMeteorIndex = 0;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = decorationPrefabs[i];

            if (prefab == null)
            {
                continue;
            }

            Quaternion rotation = Quaternion.Euler(0f, 0f, (i * 53f + 17f) % 360f);
            Transform parent = authoredContentRoot != null ? authoredContentRoot : transform;
            GameObject instance = Instantiate(prefab, decorationPositions[i], rotation, parent);
            instance.name = $"TutorialDecoration_{i:00}_{prefab.name}";
            float scale = 0.72f + (i % 4) * 0.11f;
            instance.transform.localScale *= scale;

            MeteorObstacle meteor = instance.GetComponentInChildren<MeteorObstacle>(true);
            if (meteor != null)
            {
                meteor.SetRoamingBounds(
                    new Bounds(mapCenter, new Vector3(mapSize.x, mapSize.y, 1f))
                );

                if (meteor.MotionMode == MeteorMotionMode.RigidbodyDrift)
                {
                    meteor.SetRuntimeDriftEnabled(smallMeteorIndex < movingSmallMeteorCount);
                    smallMeteorIndex++;
                }
            }

            spawnedDecorations.Add(instance);
        }
    }

    private void CreateHighValueSalvage()
    {
        if (highValueSalvagePrefab == null)
        {
            Debug.LogError("Tutorial high-value salvage prefab is not assigned.", this);
            return;
        }

        Transform parent = authoredContentRoot != null ? authoredContentRoot : transform;
        highValueSalvageInstance = Instantiate(
            highValueSalvagePrefab,
            highValueSalvagePosition,
            Quaternion.identity,
            parent
        );
        highValueSalvageInstance.name = "TutorialHighValueSalvage";
        highValueSalvageHealth = highValueSalvageInstance.GetComponentInChildren<HarvestObjectHealth>(true);
        highValueSalvageRadarTarget = highValueSalvageInstance.GetComponentInChildren<RadarTarget>(true);

        if (highValueSalvageHealth == null)
        {
            Debug.LogError("Tutorial high-value salvage prefab has no HarvestObjectHealth.", highValueSalvageInstance);
            highValueSalvageInstance.SetActive(false);
            return;
        }

        highValueSalvageHealth.SetReinforcementSpawnChance(0f);
        highValueSalvageHealth.SetPlayerProjectileDamageEnabled(false, true);

        if (highValueTeachingReward != null)
        {
            highValueSalvageHealth.SetRewardDefinition(highValueTeachingReward);
        }
        else
        {
            Debug.LogError(
                "Tutorial high-value salvage needs its guaranteed defensive-Active reward definition.",
                this
            );
        }

        highValueSalvageHealth.Died += HandleHighValueSalvageDied;
        highValueSalvageInstance.SetActive(false);
    }

    private void PlacePlayerAtSpawn()
    {
        if (playerRoot != null && playerSpawn != null)
        {
            playerRoot.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);
        }
    }

    private void CacheRuntimeReferences()
    {
        if (playerRoot != null)
        {
            playerController ??= playerRoot.GetComponent<PlayerController2D>();
            weaponController ??= playerRoot.GetComponent<PlayerWeaponController>();
            radarScanner ??= playerRoot.GetComponent<PlayerRadarScanner>();
            radarVFX ??= playerRoot.GetComponent<PlayerRadarVFXController>();
            playerInteractor ??= playerRoot.GetComponent<PlayerInteractor>();
            cargoController ??= playerRoot.GetComponent<PlayerCargoController>();
            reinforcementController ??= playerRoot.GetComponent<PlayerReinforcementController>();
            playerDash ??= playerRoot.GetComponent<PlayerDash>();
            playerHealth ??= playerRoot.GetComponent<PlayerHealth>();
            emergencyReturnController ??= playerRoot.GetComponent<EmergencyReturnController>();
            emergencyReturnExitSequence ??= playerRoot.GetComponent<EmergencyReturnExitSequence>();
            playerVisualStateController ??= playerRoot.GetComponentInChildren<PlayerVisualStateController>(true);

            if (playerCurseImpactVisual == null)
            {
                PlayerShipVisualController shipVisualController =
                    playerRoot.GetComponentInChildren<PlayerShipVisualController>(true);
                SpriteRenderer targetRenderer = shipVisualController != null
                    ? shipVisualController.TargetSpriteRenderer
                    : playerRoot.GetComponentInChildren<SpriteRenderer>(true);
                playerCurseImpactVisual = targetRenderer != null
                    ? targetRenderer.transform
                    : null;

                if (playerCurseImpactVisual != null)
                {
                    playerCurseImpactRestScale = playerCurseImpactVisual.localScale;
                }
            }
        }

        harvestTargetHealth ??= harvestTarget != null
            ? harvestTarget.GetComponentInChildren<HarvestObjectHealth>(true)
            : null;
        harvestRadarTarget ??= harvestTarget != null
            ? harvestTarget.GetComponentInChildren<RadarTarget>(true)
            : null;
        radarScanTarget ??= radarTarget != null
            ? radarTarget.GetComponentInChildren<RadarTarget>(true)
            : null;
        interactionTargetComponent ??= interactionTarget != null
            ? interactionTarget.GetComponentInChildren<TutorialInteractionTarget>(true)
            : null;
        signalDevicePulseRenderer ??= interactionTarget != null
            ? interactionTarget.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        CacheSignalDevicePulseState();
        alienSignalTargetComponent ??= alienSignal != null
            ? alienSignal.GetComponentInChildren<TutorialInteractionTarget>(true)
            : null;
        if (alienSignal != null)
        {
            alienSignalDamageReceiver ??=
                alienSignal.GetComponent<TutorialPurpleCoreDamageReceiver>();
            if (alienSignalDamageReceiver == null)
            {
                alienSignalDamageReceiver =
                    alienSignal.AddComponent<TutorialPurpleCoreDamageReceiver>();
            }

            alienSignalDamageReceiver.Configure(
                this,
                purpleCoreForcedInteractionDamage,
                purpleCoreDamageContributionWindow,
                purpleCoreDamageContributionCap);
        }
        alienSignalRadarTarget ??= alienSignal != null
            ? alienSignal.GetComponentInChildren<RadarTarget>(true)
            : null;
        expeditionMenuController ??= FindFirstObjectByType<ExpeditionMenuController>(FindObjectsInactive.Include);
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        expeditionMapPanel ??= FindFirstObjectByType<ExpeditionMapPanelUI>(FindObjectsInactive.Include);
        routePlanner ??= FindFirstObjectByType<ExpeditionRoutePlanner>(FindObjectsInactive.Include);
        if (tutorialCamera == null)
        {
            tutorialCamera = GungeonStyleCamera2D.Instance != null
                ? GungeonStyleCamera2D.Instance
                : FindFirstObjectByType<GungeonStyleCamera2D>(FindObjectsInactive.Include);
        }
    }

    private void CacheSignalDevicePulseState()
    {
        if (signalDevicePulseStateCached || signalDevicePulseRenderer == null)
        {
            return;
        }

        signalDevicePulseRestColor = signalDevicePulseRenderer.color;
        signalDevicePulseStateCached = true;
    }

    private void PlaySignalDevicePulse()
    {
        Transform visualOrigin = signalDevicePulseRenderer != null
            ? signalDevicePulseRenderer.transform
            : interactionTarget != null
                ? interactionTarget.transform
                : null;
        if (visualOrigin == null)
        {
            return;
        }

        StopSignalDevicePulse();
        float duration = Mathf.Clamp(signalDevicePulseDuration, 0.2f, 0.6f);
        signalDevicePulseTween = PlayStationaryTutorialPurpleEffect(
            visualOrigin,
            ResolveRendererWorldCenter(signalDevicePulseRenderer, visualOrigin.position),
            1.35f,
            duration);
    }

    private void StopSignalDevicePulse()
    {
        if (signalDevicePulseTween != null &&
            ReferenceEquals(signalDevicePulseTween, tutorialPurpleEffectTween))
        {
            StopTutorialPurpleEffect();
        }
        else
        {
            signalDevicePulseTween?.Kill();
        }

        signalDevicePulseTween = null;

        if (signalDevicePulseStateCached && signalDevicePulseRenderer != null)
        {
            signalDevicePulseRenderer.color = signalDevicePulseRestColor;
        }
    }

    private bool TryStartRelayNarrative()
    {
        if (currentStep != TutorialStep.InteractSignalDevice ||
            !relayNarrativeProgress.TryBeginAnalysis())
        {
            return false;
        }

        interactionTargetComponent?.SetInteractionEnabled(false);
        SetTargetPresentationInputLocked(true);
        AudioManager.PlayAt(SoundEventIds.EventStart,
            interactionTarget != null ? interactionTarget.transform.position : transform.position, 0.65f);
        PlaySignalDevicePulse();
        relayNarrativeRoutine = StartCoroutine(RelayNarrativeRoutine());
        return true;
    }

    private void RefreshRelayNarrative()
    {
        if (!isActiveAndEnabled ||
            currentStep != TutorialStep.InteractSignalDevice ||
            relayNarrativeRoutine != null ||
            relayDialogueController != null)
        {
            return;
        }

        if (relayNarrativeProgress.Phase ==
                TutorialRelayNarrativePhase.AwaitingRealOperator ||
            relayNarrativeProgress.Phase ==
                TutorialRelayNarrativePhase.AwaitingFakeOperator)
        {
            SetTargetPresentationInputLocked(true);
            relayNarrativeRoutine = StartCoroutine(
                RelayNarrativeContinuationRoutine());
        }
    }

    private IEnumerator RelayNarrativeRoutine()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Clamp(signalDevicePulseDuration, 0.2f, 0.6f));
        relayNarrativeRoutine = null;

        if (!isActiveAndEnabled ||
            currentStep != TutorialStep.InteractSignalDevice ||
            !relayNarrativeProgress.TryCompleteAnalysis())
        {
            StopRelayNarrative(false);
            yield break;
        }

        ClearRelayMapGuidanceForUnknownTracking();
        RefreshRelayNarrative();
    }

    private IEnumerator RelayNarrativeContinuationRoutine()
    {
        bool fakeOperator = relayNarrativeProgress.Phase ==
                            TutorialRelayNarrativePhase.AwaitingFakeOperator;
        if (fakeOperator &&
            relayNarrativeProgress.TryMarkFakeTakeoverCuePresented())
        {
            AudioManager.Play(SoundEventIds.DialogueCommHijack);
            PlaySignalDevicePulse();
            yield return new WaitForSecondsRealtime(
                Mathf.Clamp(fakeOperatorTakeoverDelay, 0.15f, 0.5f));
        }

        relayNarrativeRoutine = null;
        if (!isActiveAndEnabled || currentStep != TutorialStep.InteractSignalDevice)
        {
            StopRelayNarrative(false);
            yield break;
        }

        TryStartRelayConversation(
            fakeOperator
                ? fakeOperatorTakeoverConversation
                : signalRelayAnalysisConversation,
            fakeOperator);
    }

    private bool TryStartRelayConversation(string conversationTitle, bool fakeOperator)
    {
        bool phaseStarted = fakeOperator
            ? relayNarrativeProgress.TryBeginFakeOperator()
            : relayNarrativeProgress.TryBeginRealOperator();
        if (!phaseStarted)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(conversationTitle) ||
            !DialogueManager.hasInstance ||
            DialogueManager.instance == null ||
            DialogueManager.MasterDatabase == null ||
            DialogueManager.MasterDatabase.GetConversation(conversationTitle) == null)
        {
            FinishRelayConversationPhase(false, fakeOperator);
            SetTargetPresentationInputLocked(false);
            Debug.LogError(
                $"Tutorial relay conversation '{conversationTitle}' is unavailable. " +
                "The relay checkpoint remains blocked.",
                this);
            return false;
        }

        relayDialogueController = DialogueManager.instance;
        relayDialogueController.conversationEnded -= HandleRelayConversationEnded;
        relayDialogueController.conversationEnded += HandleRelayConversationEnded;
        activeRelayConversationTitle = conversationTitle;
        StartRemoteConversation(
            conversationTitle,
            playerRoot,
            interactionTarget != null ? interactionTarget.transform : transform);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(
                DialogueManager.lastConversationStarted,
                conversationTitle,
                StringComparison.Ordinal))
        {
            relayDialogueController.conversationEnded -= HandleRelayConversationEnded;
            relayDialogueController = null;
            activeRelayConversationTitle = string.Empty;
            FinishRelayConversationPhase(false, fakeOperator);
            SetTargetPresentationInputLocked(false);
            Debug.LogError(
                $"Tutorial relay conversation '{conversationTitle}' failed to start.",
                this);
            return false;
        }

        promptUI?.Hide();
        return true;
    }

    private void HandleRelayConversationEnded(Transform actor)
    {
        if (relayDialogueController == null ||
            !string.Equals(
                DialogueManager.lastConversationEnded,
                activeRelayConversationTitle,
                StringComparison.Ordinal))
        {
            return;
        }

        string completedConversation = activeRelayConversationTitle;
        bool fakeOperator = string.Equals(
            completedConversation,
            fakeOperatorTakeoverConversation,
            StringComparison.Ordinal);
        bool completedNaturally = WasConversationCompletedNaturally(
            completedConversation);

        relayDialogueController.conversationEnded -= HandleRelayConversationEnded;
        relayDialogueController = null;
        activeRelayConversationTitle = string.Empty;
        bool phaseCompleted = FinishRelayConversationPhase(
            completedNaturally,
            fakeOperator);

        if (phaseCompleted && fakeOperator)
        {
            SetTargetPresentationInputLocked(false);
            TryAdvanceCheckpoint(TutorialStep.InteractSignalDevice);
            return;
        }

        RefreshRelayNarrative();
    }

    private bool FinishRelayConversationPhase(bool completedNaturally, bool fakeOperator)
    {
        return fakeOperator
            ? relayNarrativeProgress.TryFinishFakeOperator(completedNaturally)
            : relayNarrativeProgress.TryFinishRealOperator(completedNaturally);
    }

    private void StopRelayNarrative(bool stopOwnedConversation)
    {
        if (relayNarrativeRoutine != null)
        {
            StopCoroutine(relayNarrativeRoutine);
            relayNarrativeRoutine = null;
        }

        bool conversationOwned = stopOwnedConversation &&
                                 relayDialogueController != null &&
                                 DialogueManager.hasInstance &&
                                 DialogueManager.isConversationActive &&
                                 string.Equals(
                                     DialogueManager.lastConversationStarted,
                                     activeRelayConversationTitle,
                                     StringComparison.Ordinal);
        if (relayDialogueController != null)
        {
            relayDialogueController.conversationEnded -= HandleRelayConversationEnded;
        }

        relayDialogueController = null;
        activeRelayConversationTitle = string.Empty;
        relayNarrativeProgress.CancelActivePhase();
        StopSignalDevicePulse();
        SetTargetPresentationInputLocked(false);

        if (conversationOwned)
        {
            DialogueManager.StopConversation();
        }

        if (currentStep == TutorialStep.InteractSignalDevice &&
            relayNarrativeProgress.Phase == TutorialRelayNarrativePhase.Idle &&
            interactionTargetComponent != null)
        {
            interactionTargetComponent.ResetTarget();
            interactionTargetComponent.SetInteractionEnabled(true);
        }
    }

    private void ClearRelayMapGuidanceForUnknownTracking()
    {
        expeditionMapPanel?.ClearExternalSearchRegion();
        if (radarScanTarget == null)
        {
            return;
        }

        radarScanTarget.ClearTemporaryReveal(this);
        radarScanTarget.SetShowOnMap(false);
        radarScanTarget.SetVisible(false);
    }

    private Sequence PlayStationaryTutorialPurpleEffect(
        Transform visualOrigin,
        Vector3 worldPosition,
        float endScale,
        float duration)
    {
        if (!TryAcquireTutorialPurpleEffect(visualOrigin, worldPosition))
        {
            return null;
        }

        float safeDuration = Mathf.Max(0.05f, duration);
        Transform effectTransform = activeTutorialPurpleEffect.transform;
        Color startColor = activeTutorialPurpleEffectRenderer.color;
        startColor.r = Mathf.Max(0.45f, startColor.r);
        startColor.g = Mathf.Min(0.3f, startColor.g);
        startColor.b = Mathf.Max(0.8f, startColor.b);
        startColor.a = Mathf.Max(0.3f, startColor.a);
        activeTutorialPurpleEffectRenderer.color = startColor;
        effectTransform.localScale = Vector3.one * 0.15f;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        tutorialPurpleEffectTween = sequence;
        sequence.Join(
            effectTransform.DOScale(Vector3.one * Mathf.Max(0.2f, endScale), safeDuration)
                .SetEase(Ease.OutQuad));
        sequence.Join(
            activeTutorialPurpleEffectRenderer.DOFade(0f, safeDuration)
                .SetEase(Ease.InQuad));
        sequence.OnComplete(() => ReleaseTutorialPurpleEffect(sequence));
        return sequence;
    }

    private bool TryAcquireTutorialPurpleEffect(
        Transform visualOrigin,
        Vector3 worldPosition)
    {
        StopTutorialPurpleEffect();
        if (tutorialPurpleEffectPrefab == null || visualOrigin == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "Tutorial Purple presentation requires Assets/03_Prefabs/Purple.prefab.",
                this);
#endif
            return false;
        }

        activeTutorialPurpleEffectFromPool = PoolManager.Instance != null;
        activeTutorialPurpleEffect = activeTutorialPurpleEffectFromPool
            ? PoolManager.Instance.Get(
                tutorialPurpleEffectPrefab,
                worldPosition,
                Quaternion.identity)
            : Instantiate(
                tutorialPurpleEffectPrefab,
                worldPosition,
                Quaternion.identity);
        if (activeTutorialPurpleEffect == null)
        {
            activeTutorialPurpleEffectFromPool = false;
            return false;
        }

        activeTutorialPurpleEffect.transform.SetParent(visualOrigin, true);
        activeTutorialPurpleEffect.transform.position = worldPosition;
        activeTutorialPurpleEffect.transform.rotation = Quaternion.identity;
        activeTutorialPurpleEffectRestScale = activeTutorialPurpleEffect.transform.localScale;

        activeTutorialPurpleEffectAnimator =
            activeTutorialPurpleEffect.GetComponentInChildren<Animator>(true);
        if (activeTutorialPurpleEffectAnimator != null)
        {
            activeTutorialPurpleEffectAnimator.enabled = false;
        }

        activeTutorialPurpleShockwave =
            activeTutorialPurpleEffect.GetComponentInChildren<DashShockwaveVFX>(true);
        if (activeTutorialPurpleShockwave != null)
        {
            activeTutorialPurpleShockwave.enabled = false;
        }

        activeTutorialPurpleEffectRenderer =
            activeTutorialPurpleEffect.GetComponentInChildren<SpriteRenderer>(true);
        if (activeTutorialPurpleEffectRenderer != null)
        {
            activeTutorialPurpleEffectRestColor = activeTutorialPurpleEffectRenderer.color;
            return true;
        }

        StopTutorialPurpleEffect();
        return false;
    }

    private void StopTutorialPurpleEffect()
    {
        tutorialPurpleEffectTween?.Kill();
        tutorialPurpleEffectTween = null;
        ReleaseTutorialPurpleEffectInstance();
    }

    private void ReleaseTutorialPurpleEffect(Sequence completedSequence)
    {
        if (!ReferenceEquals(tutorialPurpleEffectTween, completedSequence))
        {
            return;
        }

        tutorialPurpleEffectTween = null;
        ReleaseTutorialPurpleEffectInstance();
    }

    private void ReleaseTutorialPurpleEffectInstance()
    {
        GameObject instance = activeTutorialPurpleEffect;
        bool releaseToPool = activeTutorialPurpleEffectFromPool;
        Animator animator = activeTutorialPurpleEffectAnimator;
        DashShockwaveVFX shockwave = activeTutorialPurpleShockwave;
        Vector3 restScale = activeTutorialPurpleEffectRestScale;
        Color restColor = activeTutorialPurpleEffectRestColor;
        activeTutorialPurpleEffect = null;
        activeTutorialPurpleEffectRenderer = null;
        activeTutorialPurpleEffectAnimator = null;
        activeTutorialPurpleShockwave = null;
        activeTutorialPurpleEffectRestScale = Vector3.one;
        activeTutorialPurpleEffectRestColor = Color.white;
        activeTutorialPurpleEffectFromPool = false;

        if (instance == null)
        {
            return;
        }

        instance.transform.localScale = restScale;
        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            renderer.color = restColor;
        }

        if (animator != null)
        {
            animator.enabled = true;
        }

        if (shockwave != null)
        {
            shockwave.enabled = true;
        }

        if (releaseToPool && PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(instance);
        }
        else
        {
            Destroy(instance);
        }
    }

    private static Vector3 ResolveRendererWorldCenter(
        Renderer renderer,
        Vector3 fallbackPosition)
    {
        return renderer != null ? renderer.bounds.center : fallbackPosition;
    }

    private void SubscribeGameplayEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        if (playerDash != null)
        {
            playerDash.DashEnded += HandleDashEnded;
        }

        if (playerController != null)
        {
            playerController.MovementStarted += HandlePlayerMovementStarted;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
            BindObservedWeapon(weaponController.CurrentWeapon);
        }

        if (harvestTargetHealth != null)
        {
            harvestTargetHealth.Died += HandleHarvestTargetDied;
        }

        if (radarScanner != null)
        {
            radarScanner.ScanCompleted += HandleRadarScanCompleted;
            radarScanner.RadarActiveChanged += HandleRadarActiveChanged;
        }

        if (playerInteractor != null)
        {
            playerInteractor.Interacted += HandlePlayerInteracted;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged += HandleReinforcementEquipmentChanged;
            reinforcementController.Used += HandleReinforcementUsed;
        }

        eventsSubscribed = true;
        RebindRuntimeEvents();
    }

    private void RebindRuntimeEvents()
    {
        BindRunManager(RunManager.Instance);

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
            cargoController.CargoChanged += HandleCargoChanged;
        }

        if (expeditionMenuController != null)
        {
            expeditionMenuController.TabChanged -= HandleMenuTabChanged;
            expeditionMenuController.TabChanged += HandleMenuTabChanged;
        }

        if (routePlanner != null)
        {
            routePlanner.RouteChanged -= HandleRouteChanged;
            routePlanner.RouteChanged += HandleRouteChanged;
        }

        if (mapDiscoveryController != null)
        {
            mapDiscoveryController.TargetDiscovered -= HandleMapTargetDiscovered;
            mapDiscoveryController.TargetDiscovered += HandleMapTargetDiscovered;
        }
    }

    private void UnsubscribeGameplayEvents()
    {
        if (playerDash != null)
        {
            playerDash.DashEnded -= HandleDashEnded;
        }

        if (playerController != null)
        {
            playerController.MovementStarted -= HandlePlayerMovementStarted;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }

        BindObservedWeapon(null);

        if (harvestTargetHealth != null)
        {
            harvestTargetHealth.Died -= HandleHarvestTargetDied;
        }

        if (highValueSalvageHealth != null)
        {
            highValueSalvageHealth.Died -= HandleHighValueSalvageDied;
        }

        if (radarScanner != null)
        {
            radarScanner.ScanCompleted -= HandleRadarScanCompleted;
            radarScanner.RadarActiveChanged -= HandleRadarActiveChanged;
        }

        if (playerInteractor != null)
        {
            playerInteractor.Interacted -= HandlePlayerInteracted;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            reinforcementController.Used -= HandleReinforcementUsed;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
        }

        if (expeditionMenuController != null)
        {
            expeditionMenuController.TabChanged -= HandleMenuTabChanged;
        }

        if (routePlanner != null)
        {
            routePlanner.RouteChanged -= HandleRouteChanged;
        }

        if (mapDiscoveryController != null)
        {
            mapDiscoveryController.TargetDiscovered -= HandleMapTargetDiscovered;
        }

        BindRunManager(null);
        eventsSubscribed = false;
    }

    private void BindRunManager(RunManager runManager)
    {
        if (ReferenceEquals(subscribedRunManager, runManager))
        {
            return;
        }

        if (subscribedRunManager != null)
        {
            subscribedRunManager.RunEnded -= HandleRunEnded;
        }

        subscribedRunManager = runManager;

        if (subscribedRunManager != null)
        {
            subscribedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void BindObservedWeapon(PlayerWeaponBase weapon)
    {
        if (observedWeapon != null)
        {
            observedWeapon.Fired -= HandleWeaponFired;
        }

        observedWeapon = weapon;

        if (observedWeapon != null)
        {
            observedWeapon.Fired += HandleWeaponFired;
        }
    }

    private void HandleDashEnded()
    {
        if (currentStep != TutorialStep.Dash || controlPacingRoutine != null)
        {
            return;
        }

        controlPacingRoutine = StartCoroutine(DashCompletionRoutine());
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTree, PlayerWeaponBase weapon)
    {
        BindObservedWeapon(weapon);
    }

    private void HandleWeaponFired(PlayerWeaponBase weapon, float powerRatio)
    {
        if (weaponController == null || !ReferenceEquals(weaponController.CurrentWeapon, weapon))
        {
            return;
        }

        TryAdvanceCheckpoint(TutorialStep.AimAndFire);
    }

    private void HandleHarvestTargetDied(HarvestObjectHealth target)
    {
        if (!ReferenceEquals(target, harvestTargetHealth))
        {
            return;
        }

        normalHarvestDestroyed = true;
        TryAdvanceSupplyProgression(TutorialSupplyProgressSource.TargetDestroyed);
    }

    private void HandleCargoChanged(int currentLoad, int maxCapacity)
    {
        if (cargoController != null && cargoController.IsApplyingExactOwnedRecollection)
        {
            return;
        }

        if (currentStep == TutorialStep.CollectResources &&
            normalHarvestDestroyed &&
            currentLoad > cargoLoadBeforePickup)
        {
            SetStep(TutorialStep.Cargo);
            return;
        }

    }

    private void HandleMenuTabChanged(ExpeditionMenuTab tab)
    {
        if (currentStep == TutorialStep.Map && tab == ExpeditionMenuTab.Map)
        {
            TryAdvanceCheckpoint(TutorialStep.Map);
        }
    }

    private void HandleRouteChanged()
    {
        if (currentStep == TutorialStep.RoutePing &&
            routePlanner != null &&
            routePlanner.WaypointCount > routeWaypointCountBeforeStep)
        {
            TryAdvanceSupplyProgression(TutorialSupplyProgressSource.RoutePlaced);
        }
    }

    private bool TryAdvanceSupplyProgression(TutorialSupplyProgressSource source)
    {
        if (!TutorialSupplyProgression.TryResolveNextStep(
                currentStep,
                source,
                out TutorialStep nextStep))
        {
            return false;
        }

        SetStep(nextStep);
        return currentStep == nextStep;
    }

    private void HandleMapTargetDiscovered(RadarTarget target)
    {
        if (ReferenceEquals(target, highValueSalvageRadarTarget))
        {
            RegisterHighValueWreckDiscovery();
        }
    }

    private void HandleRadarActiveChanged(bool active)
    {
        if (currentStep == TutorialStep.RadarDiscoverSalvage)
        {
            radarGuidanceProgress.RecordRadarActiveChanged(active);
        }

        if (currentStep == TutorialStep.FindSignalDevice)
        {
            ApplyCurrentStepPresentation();
        }
    }

    private void HandleRadarScanCompleted(
        Vector2 origin,
        float radius,
        IReadOnlyList<RadarTarget> scannedTargets)
    {
        if (scannedTargets == null)
        {
            return;
        }

        RadarTarget requiredTarget = currentStep switch
        {
            TutorialStep.RadarDiscoverSalvage => harvestRadarTarget,
            TutorialStep.TravelHighValue => highValueSalvageRadarTarget,
            TutorialStep.FindSignalDevice => radarScanTarget,
            _ => null
        };

        if (requiredTarget == null)
        {
            return;
        }

        for (int i = 0; i < scannedTargets.Count; i++)
        {
            if (!ReferenceEquals(scannedTargets[i], requiredTarget))
            {
                continue;
            }

            if (currentStep == TutorialStep.FindSignalDevice)
            {
                CompleteSignalDeviceDiscovery();
                return;
            }

            if (currentStep == TutorialStep.TravelHighValue)
            {
                RegisterHighValueWreckDiscovery();
                return;
            }

            radarGuidanceProgress.TryRecordSuccessfulTargetScan(true);
            if (radarGuidanceProgress.HasSuccessfulTargetScan)
            {
                targetPresentationAwaitingRediscovery = false;
                RequestTargetPresentation(
                    TutorialTargetPresentationKind.SupplyContainer);
            }

            return;
        }
    }

    private bool CompleteSignalDeviceDiscovery()
    {
        if (currentStep != TutorialStep.FindSignalDevice)
        {
            return false;
        }

        signalDeviceAutoRegistered = true;
        if (radarScanTarget != null)
        {
            radarScanTarget.SetMarkerType(RadarMarkerType.Unknown);
            radarScanTarget.SetMarkerVisual(
                radarScanTarget.MarkerSprite,
                new Color(0.82f, 0.72f, 1f, 1f),
                1.2f
            );
            radarScanTarget.SetTemporaryReveal(this, 3600f);
        }
        return TryAdvanceCheckpoint(TutorialStep.FindSignalDevice);
    }

    private void HandlePlayerInteracted(IInteractable target)
    {
        if (target is not Component targetComponent)
        {
            return;
        }

        if (currentStep == TutorialStep.InteractSignalDevice &&
            interactionTargetComponent != null &&
            targetComponent == interactionTargetComponent)
        {
            TryStartRelayNarrative();
            return;
        }

        if (currentStep != TutorialStep.InteractPurpleCore ||
            alienSignalTargetComponent == null ||
            targetComponent != alienSignalTargetComponent)
        {
            return;
        }

        TryRequestPurpleCoreAcquisition(
            TutorialPurpleCoreActivationSource.Interaction);
    }

    private void HandleHighValueSalvageDied(HarvestObjectHealth target)
    {
        if (!ReferenceEquals(target, highValueSalvageHealth) ||
            currentStep != TutorialStep.DestroyHighValue)
        {
            return;
        }

        highValueDestroyed = true;
        TryAdvanceCheckpoint(TutorialStep.DestroyHighValue);
    }

    private void HandleReinforcementEquipmentChanged(
        ReinforcementDefinition definition,
        int currentCharges,
        int maxCharges)
    {
        if (currentStep == TutorialStep.EquipDefensiveActive &&
            definition == defensiveReinforcementDefinition)
        {
            TryAdvanceCheckpoint(TutorialStep.EquipDefensiveActive);
            return;
        }

        if (currentStep == TutorialStep.AcquireEmergencyReturn &&
            definition == emergencyReturnDefinition)
        {
            TryAdvanceCheckpoint(TutorialStep.AcquireEmergencyReturn);
        }
    }

    private void HandleReinforcementUsed(ReinforcementDefinition definition)
    {
        if (currentStep != TutorialStep.UseDefensiveActive ||
            definition == null ||
            definition != defensiveReinforcementDefinition)
        {
            return;
        }

        SetStep(TutorialStep.Combat);
    }

    private void HandlePlayerMovementStarted()
    {
        if (currentStep != TutorialStep.Move ||
            !openingTransmissionProgress.TryConfirmMovement())
        {
            return;
        }

        RefreshOperatorGuidance();
    }

    private void HandleCombatEnemyDied(EnemyHealth enemyHealth)
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleCombatEnemyDied;
        }

        activeCombatEnemies.Remove(enemyHealth);

        if (currentStep == TutorialStep.Combat && activeCombatEnemies.Count == 0)
        {
            SetStep(TutorialStep.FindSignalDevice);
        }
    }

    private void HandleRunEnded(RunResultData resultData)
    {
        StopTargetPresentation(true, true);

        if (currentStep != TutorialStep.EmergencyReturn ||
            resultData == null ||
            resultData.endReason != RunEndReason.EmergencyReturn)
        {
            return;
        }

        MarkTutorialCompletedAfterReturn();
    }

    private void ApplyCurrentStepState()
    {
        ApplyCurrentStepPresentation();
        ApplyCurrentStepWorldState();
        RefreshControlPacing();
        RestartIntroTransition();
        RefreshOperatorGuidance();
        RefreshDynamicNormalSalvage();
        RefreshCargoFeedback();
        RefreshObjectiveChecks();
        RefreshPurpleCoreReveal();
        RefreshRelayNarrative();
        RefreshCurseTransformation();
        RefreshUnknownMission();
        RefreshRescueSequence();
        RefreshReinforcementCheckpoint();
        RefreshEmergencyReturn();
        ValidateProductionMenuDependency();
    }

    private void ApplyCurrentStepWorldState()
    {
        if (harvestTarget != null && (harvestTargetHealth == null || !harvestTargetHealth.IsDead))
        {
            bool showNormalHarvest = currentStep >= TutorialStep.RadarDiscoverSalvage &&
                                     currentStep <= TutorialStep.CollectResources &&
                                     !normalHarvestDestroyed;
            harvestTarget.SetActive(showNormalHarvest);
        }

        if (harvestTargetHealth != null && !harvestTargetHealth.IsDead)
        {
            harvestTargetHealth.SetPlayerProjectileDamageEnabled(
                TutorialSupplyProgression.IsTargetDamageEnabled(currentStep),
                true
            );
        }

        if (highValueSalvageInstance != null &&
            highValueSalvageHealth != null &&
            !highValueSalvageHealth.IsDead)
        {
            bool showHighValue = currentStep >= TutorialStep.TravelHighValue &&
                                 currentStep <= TutorialStep.DestroyHighValue;
            highValueSalvageInstance.SetActive(showHighValue);
        }

        if (highValueSalvageHealth != null && !highValueSalvageHealth.IsDead)
        {
            highValueSalvageHealth.SetPlayerProjectileDamageEnabled(
                currentStep == TutorialStep.DestroyHighValue,
                true
            );
        }

        bool relayVisible = currentStep >= TutorialStep.FindSignalDevice;

        if (radarTarget != null)
        {
            radarTarget.SetActive(relayVisible);
        }

        if (interactionTarget != null && interactionTarget != radarTarget)
        {
            interactionTarget.SetActive(relayVisible);
        }

        if (radarScanTarget != null)
        {
            bool relayObjectiveUnresolved = relayVisible &&
                                            currentStep <=
                                            TutorialStep.InteractSignalDevice;
            radarScanTarget.SetShowOnMap(relayObjectiveUnresolved);
            radarScanTarget.SetVisible(relayObjectiveUnresolved);
        }

        interactionTargetComponent?.SetInteractionEnabled(
            currentStep == TutorialStep.InteractSignalDevice &&
            relayNarrativeProgress.Phase == TutorialRelayNarrativePhase.Idle);

        bool coreCanReceiveInput = currentStep == TutorialStep.InteractPurpleCore &&
                                   corePresentationProgress.Phase ==
                                   TutorialCorePresentationPhase.Ready &&
                                   !coreAcquisitionLatch.IsActive &&
                                   !coreAcquisitionLatch.IsCompleted;
        alienSignalDamageReceiver?.SetReceivingEnabled(coreCanReceiveInput);

        bool radarAvailable = currentStep >= TutorialStep.RadarDiscoverSalvage &&
                              currentStep <= TutorialStep.EmergencyReturn;

        if (radarScanner != null)
        {
            if (!radarAvailable && radarScanner.enabled)
            {
                radarScanner.CloseRadar();
            }

            radarScanner.enabled = radarAvailable;
        }

        if (radarVFX != null)
        {
            radarVFX.enabled = radarAvailable;
        }

        if (radarUiRoot != null && !radarUiRoot.activeSelf)
        {
            radarUiRoot.SetActive(true);
        }

        bool curseOwned = HasPersistentPixelCurse();
        bool showPurpleCore = !curseOwned &&
                              currentStep >= TutorialStep.RevealPurpleCore &&
                              currentStep <= TutorialStep.CurseTransformation;

        if (alienSignal != null)
        {
            alienSignal.SetActive(showPurpleCore);
        }

        if (alienSignalVisualRoot != null)
        {
            bool showAlienVisual = showPurpleCore && currentStep >= TutorialStep.RevealPurpleCore;
            alienSignalVisualRoot.gameObject.SetActive(showAlienVisual);
        }

        if (alienSignalRadarTarget != null && !curseOwned)
        {
            bool coreRevealed = currentStep >= TutorialStep.TravelPurpleCore;
            alienSignalRadarTarget.SetVisible(coreRevealed);
            alienSignalRadarTarget.SetShowOnMap(coreRevealed);
            if (!coreRevealed)
            {
                alienSignalRadarTarget.SetMapDiscovered(false);
                alienSignalRadarTarget.ClearTemporaryReveal(this);
            }
        }

        if (alienSignalTargetComponent != null)
        {
            if (currentStep == TutorialStep.InteractPurpleCore &&
                !curseOwned &&
                corePresentationProgress.Phase == TutorialCorePresentationPhase.Ready)
            {
                alienSignalTargetComponent.ResetTarget();
                alienSignalTargetComponent.SetInteractionText(
                    ResolveLocalizedTutorialText(
                        Phase2CStoryDialogueIds.TutorialPurpleCoreInteractTextKey));
                alienSignalTargetComponent.SetInteractionEnabled(true);
            }
            else
            {
                alienSignalTargetComponent.SetInteractionEnabled(false);
            }
        }

        statusEffectPresenter?.SetExternalVisible(curseOwned);
    }

    private void RestartIntroTransition()
    {
        if (introAdvanceRoutine != null)
        {
            StopCoroutine(introAdvanceRoutine);
            introAdvanceRoutine = null;
        }

        if (isActiveAndEnabled && !isCompleting && currentStep == TutorialStep.IntroCommunication)
        {
            introAdvanceRoutine = StartCoroutine(IntroAdvanceRoutine());
        }
    }

    private void RefreshControlPacing()
    {
        if (currentStep == TutorialStep.Move && controlPacingRoutine == null)
        {
            if (playerRoot != null && accumulatedMovementDistance <= 0f)
            {
                lastMovementSamplePosition = playerRoot.position;
            }

            controlPacingRoutine = StartCoroutine(MovementCompletionRoutine());
        }
    }

    private IEnumerator MovementCompletionRoutine()
    {
        float requiredDistance = Mathf.Max(0.5f, movementTravelRequired);

        while (isActiveAndEnabled && currentStep == TutorialStep.Move)
        {
            if (playerRoot != null && !GameplayPauseManager.IsPaused)
            {
                Vector2 currentPosition = playerRoot.position;
                float traveled = Vector2.Distance(lastMovementSamplePosition, currentPosition);
                if (traveled <= 1f)
                {
                    accumulatedMovementDistance += traveled;
                }

                lastMovementSamplePosition = currentPosition;
                if (accumulatedMovementDistance >= requiredDistance)
                {
                    break;
                }
            }

            yield return null;
        }

        if (currentStep != TutorialStep.Move)
        {
            controlPacingRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, movementCompletionDelay));
        controlPacingRoutine = null;
        TryAdvanceCheckpoint(TutorialStep.Move);
    }

    private IEnumerator DashCompletionRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, dashCompletionDelay));
        controlPacingRoutine = null;
        TryAdvanceCheckpoint(TutorialStep.Dash);
    }

    private IEnumerator IntroAdvanceRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, introAdvanceDelay));
        introAdvanceRoutine = null;
        TryAdvanceCheckpoint(TutorialStep.IntroCommunication);
    }

    private void RefreshOperatorGuidance()
    {
        if (currentStep == TutorialStep.RadarDiscoverSalvage &&
            radarGuidanceProgress.HasSuccessfulTargetScan &&
            !operatorGuidanceProgress.IsComplete(
                TutorialStep.DestroyNormalSalvage))
        {
            RequestTargetPresentation(
                TutorialTargetPresentationKind.SupplyContainer);
            return;
        }

        if (currentStep == TutorialStep.TravelHighValue)
        {
            if (highValueAutoRegistered &&
                !operatorGuidanceProgress.IsComplete(TutorialStep.TravelHighValue))
            {
                RequestTargetPresentation(
                    TutorialTargetPresentationKind.HighValueWreck);
            }

            return;
        }

        if (currentStep == TutorialStep.DestroyNormalSalvage)
        {
            return;
        }

        if (Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                currentStep,
                highValueAutoRegistered,
                out _) &&
            !operatorGuidanceProgress.IsComplete(currentStep))
        {
            TryStartOperatorConversation(
                currentStep,
                ResolveOperatorConversationTitle(currentStep));
        }

        if (currentStep == TutorialStep.Combat)
        {
            SpawnGuaranteedCombatGroup();
        }
    }

    private string ResolveOperatorConversationTitle(TutorialStep guidanceStep)
    {
        return guidanceStep switch
        {
            TutorialStep.Move => openingConversation,
            TutorialStep.RadarDiscoverSalvage => radarConversation,
            TutorialStep.DestroyNormalSalvage => supplyContainerConversation,
            TutorialStep.TravelHighValue => ancientSignalConversation,
            _ => string.Empty
        };
    }

    private bool TryStartOperatorConversation(
        TutorialStep guidanceStep,
        string conversationTitle)
    {
        return TryStartOperatorConversation(
            guidanceStep,
            guidanceStep,
            conversationTitle);
    }

    private bool TryStartOperatorConversation(
        TutorialStep requiredCurrentStep,
        TutorialStep guidanceStep,
        string conversationTitle)
    {
        if (openingConversationStarted ||
            (guidanceStep == activeTargetGuidanceStep &&
             DialogueManager.hasInstance && DialogueManager.isConversationActive) ||
            currentStep != requiredCurrentStep ||
            operatorGuidanceProgress.IsComplete(guidanceStep) ||
            guidanceStep == TutorialStep.Move &&
            !openingTransmissionProgress.HasConfirmedMovement)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(conversationTitle) ||
            !DialogueManager.hasInstance ||
            DialogueManager.MasterDatabase == null ||
            DialogueManager.MasterDatabase.GetConversation(conversationTitle) == null)
        {
            Debug.LogError(
                $"Tutorial operator conversation '{conversationTitle}' is unavailable. " +
                "The tutorial story checkpoint remains blocked.",
                this
            );
            return false;
        }

        bool requestIncomingCue = false;
        if (guidanceStep == TutorialStep.Move &&
            !openingTransmissionProgress.TryBeginTransmission(
                true,
                out requestIncomingCue))
        {
            return false;
        }

        if (guidanceStep == TutorialStep.RadarDiscoverSalvage &&
            radarScanner != null)
        {
            DialogueLua.SetVariable(
                Phase2CStoryDialogueIds.RadarToggleLuaVariable,
                DialogueWordWrapUtility.ProtectBindingDisplayString(
                    radarScanner.RadarBindingDisplay));
            DialogueLua.SetVariable(
                Phase2CStoryDialogueIds.RadarScanLuaVariable,
                DialogueWordWrapUtility.ProtectBindingDisplayString(
                    radarScanner.QuickScanBindingDisplay));
        }

        openingConversationStarted = true;
        activeOperatorGuidanceStep = guidanceStep;
        activeOperatorConversationTitle = conversationTitle;
        rescueDialogueController = DialogueManager.instance;
        rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
        rescueDialogueController.conversationEnded += HandleOpeningConversationEnded;

        StartRemoteConversation(conversationTitle, playerRoot, null,
            guidanceStep != TutorialStep.Move || requestIncomingCue);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(
                DialogueManager.lastConversationStarted,
                conversationTitle,
                StringComparison.Ordinal))
        {
            rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
            rescueDialogueController = null;
            openingConversationStarted = false;
            openingTransmissionProgress.FinishTransmission();
            activeOperatorGuidanceStep = TutorialStep.Complete;
            activeOperatorConversationTitle = string.Empty;
            Debug.LogError(
                $"Tutorial operator conversation '{conversationTitle}' failed to start.",
                this);
            return false;
        }

        return true;
    }

    private void HandleOpeningConversationEnded(Transform actor)
    {
        if (!openingConversationStarted ||
            !string.Equals(
                DialogueManager.lastConversationEnded,
                activeOperatorConversationTitle,
                StringComparison.Ordinal))
        {
            return;
        }

        string completedConversation = activeOperatorConversationTitle;
        if (rescueDialogueController != null)
        {
            rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
        }

        rescueDialogueController = null;
        openingConversationStarted = false;
        TutorialStep completedGuidance = activeOperatorGuidanceStep;
        if (completedGuidance == TutorialStep.Move)
        {
            openingTransmissionProgress.FinishTransmission();
        }

        activeOperatorGuidanceStep = TutorialStep.Complete;
        activeOperatorConversationTitle = string.Empty;

        bool completedNaturally = WasConversationCompletedNaturally(
            completedConversation);
        if (completedGuidance == activeTargetGuidanceStep &&
            targetPresentationProgress.Phase ==
            TutorialTargetPresentationPhase.Dialogue)
        {
            targetPresentationProgress.TryBeginReturn(completedNaturally);
            return;
        }

        if (!completedNaturally)
        {
            ScheduleDialogueGuideHandoff(completedGuidance, false, false);
            return;
        }

        CompleteOperatorGuidance(completedGuidance);
    }

    private void CompleteOperatorGuidance(TutorialStep completedGuidance)
    {
        if (!operatorGuidanceProgress.TryComplete(completedGuidance, true))
        {
            return;
        }

        ScheduleDialogueGuideHandoff(
            completedGuidance,
            Phase2CStoryDialogueIds.AdvancesCheckpointAfterGuidance(
                completedGuidance),
            false);
    }

    private void RegisterHighValueWreckDiscovery()
    {
        if (currentStep != TutorialStep.TravelHighValue ||
            operatorGuidanceProgress.IsComplete(TutorialStep.TravelHighValue))
        {
            return;
        }

        highValueAutoRegistered = true;
        targetPresentationAwaitingRediscovery = false;
        RequestTargetPresentation(
            TutorialTargetPresentationKind.HighValueWreck);
    }

    private void RequestTargetPresentation(TutorialTargetPresentationKind kind)
    {
        if (TryReserveTargetPresentation(kind))
        {
            Coroutine routine = StartCoroutine(TargetPresentationRoutine());
            // StartCoroutine may finish synchronously if ownership is unavailable.
            targetPresentationRoutine = targetPresentationProgress.Kind != TutorialTargetPresentationKind.None
                ? routine : null;
        }
    }

    private bool TryReserveTargetPresentation(TutorialTargetPresentationKind kind)
    {
        if (!TryResolveTargetPresentation(
                kind,
                out TutorialStep triggerStep,
                out TutorialStep guidanceStep,
                out Transform target,
                out HarvestObjectHealth targetHealth,
                out string conversationTitle) ||
            !isActiveAndEnabled ||
            isCompleting ||
            targetPresentationAwaitingRediscovery ||
            currentStep != triggerStep ||
            !targetPresentationProgress.TryRequest(
                kind,
                IsPresentationTargetValid(target, targetHealth)))
        {
            return false;
        }

        activeTargetTriggerStep = triggerStep;
        activeTargetGuidanceStep = guidanceStep;
        activePresentationTarget = target;
        activePresentationHealth = targetHealth;
        activePresentationCollider = target.GetComponentInChildren<Collider2D>(true);
        activePresentationRenderer = target.GetComponentInChildren<Renderer>(true);
        activeTargetConversationTitle = conversationTitle;
        targetPresentationCamera = tutorialCamera;
        targetPresentationPlayer = playerRoot;
        return true;
    }

    private bool TryResolveTargetPresentation(
        TutorialTargetPresentationKind kind,
        out TutorialStep triggerStep,
        out TutorialStep guidanceStep,
        out Transform target,
        out HarvestObjectHealth targetHealth,
        out string conversationTitle)
    {
        triggerStep = TutorialStep.Complete;
        guidanceStep = TutorialStep.Complete;
        target = null;
        targetHealth = null;
        conversationTitle = string.Empty;

        switch (kind)
        {
            case TutorialTargetPresentationKind.SupplyContainer:
                if (!radarGuidanceProgress.HasSuccessfulTargetScan ||
                    operatorGuidanceProgress.IsComplete(
                        TutorialStep.DestroyNormalSalvage))
                {
                    return false;
                }

                triggerStep = TutorialStep.RadarDiscoverSalvage;
                guidanceStep = TutorialStep.DestroyNormalSalvage;
                target = harvestTarget != null ? harvestTarget.transform : null;
                targetHealth = harvestTargetHealth;
                conversationTitle = supplyContainerConversation;
                return true;

            case TutorialTargetPresentationKind.HighValueWreck:
                if (!highValueAutoRegistered ||
                    operatorGuidanceProgress.IsComplete(
                        TutorialStep.TravelHighValue))
                {
                    return false;
                }

                triggerStep = TutorialStep.TravelHighValue;
                guidanceStep = TutorialStep.TravelHighValue;
                target = highValueSalvageInstance != null
                    ? highValueSalvageInstance.transform
                    : null;
                targetHealth = highValueSalvageHealth;
                conversationTitle = ancientSignalConversation;
                return true;

            default:
                return false;
        }
    }

    private IEnumerator TargetPresentationRoutine()
    {
        while (targetPresentationProgress.Phase ==
               TutorialTargetPresentationPhase.WaitingForSafeViewport)
        {
            if (!CanContinueTargetPresentation())
            {
                FinishTargetPresentationWithoutCheckpoint();
                yield break;
            }

            bool insideSafeViewport = IsPresentationTargetInsideSafeViewport();
            if (targetPresentationProgress.TryBeginFocus(
                    insideSafeViewport,
                    Time.unscaledDeltaTime,
                    Mathf.Clamp(targetSafeViewportHoldDuration, 0.1f, 0.2f)))
            {
                break;
            }

            yield return null;
        }

        if (targetPresentationProgress.Phase !=
            TutorialTargetPresentationPhase.Focusing)
        {
            FinishTargetPresentationWithoutCheckpoint();
            yield break;
        }

        if (!CanContinueTargetPresentation() ||
            !targetPresentationCamera.TryBeginOwnedCinematicFocusBlend(
                discoveryCameraOwner,
                activePresentationTarget.position,
                Mathf.Clamp(targetFocusDuration, 0.5f, 0.7f),
                null,
                out _))
        {
            FinishTargetPresentationWithoutCheckpoint();
            yield break;
        }

        SetTutorialFocusHud(discoveryCameraOwner, true);
        SetTargetPresentationInputLocked(true);

        while (targetPresentationCamera != null && targetPresentationCamera.IsCinematicFocusBlendActive)
        {
            if (!CanContinueTargetPresentation())
            {
                InterruptTargetPresentationConversation();
                targetPresentationProgress.TryBeginReturn(false);
                break;
            }

            if (!OwnsDiscoveryCamera())
            {
                InterruptTargetPresentationConversation();
                FinishTargetPresentationWithoutCheckpoint();
                yield break;
            }

            yield return null;
        }

        if (targetPresentationProgress.Phase ==
            TutorialTargetPresentationPhase.Focusing)
        {
            if (!CanContinueTargetPresentation() ||
                !targetPresentationCamera.IsCinematicFocusOwnedBy(discoveryCameraOwner) ||
                !targetPresentationProgress.TryBeginDialogue() ||
                !TryStartOperatorConversation(
                    activeTargetTriggerStep,
                    activeTargetGuidanceStep,
                    activeTargetConversationTitle))
            {
                targetPresentationProgress.TryBeginReturn(false);
            }
        }

        while (targetPresentationProgress.Phase ==
               TutorialTargetPresentationPhase.Dialogue)
        {
            if (!CanContinueTargetPresentation() ||
                !DialogueManager.hasInstance || !DialogueManager.isConversationActive ||
                !string.Equals(DialogueManager.lastConversationStarted,
                    activeTargetConversationTitle, StringComparison.Ordinal))
            {
                InterruptTargetPresentationConversation();
                targetPresentationProgress.TryBeginReturn(false);
                break;
            }

            if (!targetPresentationCamera.IsCinematicFocusOwnedBy(discoveryCameraOwner))
            {
                InterruptTargetPresentationConversation();
                FinishTargetPresentationWithoutCheckpoint();
                yield break;
            }

            yield return null;
        }

        if (targetPresentationProgress.Phase !=
            TutorialTargetPresentationPhase.Returning)
        {
            FinishTargetPresentationWithoutCheckpoint();
            yield break;
        }

        if (!CanReturnTargetPresentation() ||
            !targetPresentationCamera.TryBeginOwnedPlayerReturnBlend(
                discoveryCameraOwner, Mathf.Clamp(targetReturnDuration, 0.4f, 0.6f)))
        {
            FinishTargetPresentationWithoutCheckpoint();
            yield break;
        }

        while (targetPresentationCamera != null && targetPresentationCamera.IsCinematicFocusBlendActive)
        {
            if (!CanReturnTargetPresentation())
            {
                FinishTargetPresentationWithoutCheckpoint();
                yield break;
            }

            if (!IsPresentationTargetValid(activePresentationTarget, activePresentationHealth))
            {
                targetPresentationProgress.CancelCheckpointCompletion();
            }

            yield return null;
        }

        if (!CanReturnTargetPresentation())
        {
            FinishTargetPresentationWithoutCheckpoint();
            yield break;
        }

        if (!IsPresentationTargetValid(activePresentationTarget, activePresentationHealth))
        {
            targetPresentationProgress.CancelCheckpointCompletion();
        }

        targetPresentationCamera.ReleaseOwnedCinematicFocus(discoveryCameraOwner, true);
        SetTargetPresentationInputLocked(false);
        targetPresentationRoutine = null;
        TutorialStep completedTriggerStep = activeTargetTriggerStep;
        TutorialStep completedGuidanceStep = activeTargetGuidanceStep;

        if (!targetPresentationProgress.TryFinishReturn(
                out _,
                out bool shouldAdvanceCheckpoint))
        {
            ClearActiveTargetPresentation();
            targetPresentationAwaitingRediscovery = true;
            yield break;
        }

        if (!shouldAdvanceCheckpoint ||
            !operatorGuidanceProgress.TryComplete(
                completedGuidanceStep,
                true))
        {
            ClearActiveTargetPresentation();
            targetPresentationAwaitingRediscovery = true;
            yield break;
        }

        ClearActiveTargetPresentation();
        TryAdvanceCheckpoint(completedTriggerStep);
    }

    private bool CanContinueTargetPresentation()
    {
        return isActiveAndEnabled &&
               !isCompleting &&
               !IsSceneExitPending() &&
               targetPresentationCamera != null &&
               targetPresentationCamera.isActiveAndEnabled &&
               IsPresentationPlayerValid() &&
               currentStep == activeTargetTriggerStep &&
               targetPresentationProgress.Kind !=
               TutorialTargetPresentationKind.None &&
               IsPresentationTargetValid(
                   activePresentationTarget,
                   activePresentationHealth);
    }

    private bool OwnsDiscoveryCamera()
    {
        return targetPresentationCamera != null &&
               targetPresentationCamera.isActiveAndEnabled &&
               targetPresentationCamera.IsCinematicFocusOwnedBy(discoveryCameraOwner);
    }

    private bool IsPresentationPlayerValid()
    {
        // Replacement during the trip aborts instead of leaving the new player's
        // input unlocked behind dialogue; release still uses the captured publishers.
        return targetPresentationPlayer != null &&
               targetPresentationPlayer.gameObject.activeInHierarchy &&
               playerRoot == targetPresentationPlayer &&
               targetPresentationCamera != null &&
               targetPresentationCamera.FollowTarget == targetPresentationPlayer;
    }

    private bool CanReturnTargetPresentation()
    {
        return isActiveAndEnabled && !isCompleting && !IsSceneExitPending() &&
               currentStep == activeTargetTriggerStep &&
               OwnsDiscoveryCamera() && IsPresentationPlayerValid();
    }

    private static bool IsSceneExitPending()
    {
        return SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLoading;
    }

    private static bool IsPresentationTargetValid(
        Transform target,
        HarvestObjectHealth targetHealth)
    {
        return target != null &&
               target.gameObject.activeInHierarchy &&
               target.gameObject.scene.IsValid() && target.gameObject.scene.isLoaded &&
               targetHealth != null &&
               targetHealth.isActiveAndEnabled &&
               !targetHealth.IsDead;
    }

    private bool IsPresentationTargetInsideSafeViewport()
    {
        Camera gameplayCamera = tutorialCamera != null
            ? tutorialCamera.GameplayCamera
            : Camera.main;
        if (gameplayCamera == null || activePresentationTarget == null)
        {
            return false;
        }

        Bounds targetBounds = new Bounds(activePresentationTarget.position, Vector3.zero);
        if (activePresentationCollider != null && activePresentationCollider.enabled)
        {
            targetBounds = activePresentationCollider.bounds;
        }
        else if (activePresentationRenderer != null && activePresentationRenderer.enabled)
        {
            targetBounds = activePresentationRenderer.bounds;
        }

        Vector2 safeMin = new Vector2(
            Mathf.Clamp(targetSafeViewportMin.x, 0f, 0.49f),
            Mathf.Clamp(targetSafeViewportMin.y, 0f, 0.49f));
        Vector2 safeMax = new Vector2(
            Mathf.Clamp(targetSafeViewportMax.x, 0.51f, 1f),
            Mathf.Clamp(targetSafeViewportMax.y, 0.51f, 1f));

        return IsWorldPointInsideSafeViewport(
                   gameplayCamera,
                   new Vector3(targetBounds.min.x, targetBounds.min.y, targetBounds.center.z),
                   safeMin,
                   safeMax) &&
               IsWorldPointInsideSafeViewport(
                   gameplayCamera,
                   new Vector3(targetBounds.min.x, targetBounds.max.y, targetBounds.center.z),
                   safeMin,
                   safeMax) &&
               IsWorldPointInsideSafeViewport(
                   gameplayCamera,
                   new Vector3(targetBounds.max.x, targetBounds.min.y, targetBounds.center.z),
                   safeMin,
                   safeMax) &&
               IsWorldPointInsideSafeViewport(
                   gameplayCamera,
                   new Vector3(targetBounds.max.x, targetBounds.max.y, targetBounds.center.z),
                   safeMin,
                   safeMax);
    }

    private static bool IsWorldPointInsideSafeViewport(
        Camera gameplayCamera,
        Vector3 worldPosition,
        Vector2 safeMin,
        Vector2 safeMax)
    {
        Vector3 viewport = gameplayCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f &&
               viewport.x >= safeMin.x && viewport.x <= safeMax.x &&
               viewport.y >= safeMin.y && viewport.y <= safeMax.y;
    }

    private void InterruptTargetPresentationConversation()
    {
        if (!openingConversationStarted ||
            activeOperatorGuidanceStep != activeTargetGuidanceStep)
        {
            return;
        }

        bool stopOwnedConversation = DialogueManager.hasInstance &&
            DialogueManager.isConversationActive &&
            string.Equals(DialogueManager.lastConversationStarted,
                activeTargetConversationTitle, StringComparison.Ordinal);
        if (rescueDialogueController != null)
        {
            rescueDialogueController.conversationEnded -=
                HandleOpeningConversationEnded;
        }

        rescueDialogueController = null;
        openingConversationStarted = false;
        openingTransmissionProgress.FinishTransmission();
        activeOperatorGuidanceStep = TutorialStep.Complete;
        activeOperatorConversationTitle = string.Empty;

        if (stopOwnedConversation)
        {
            DialogueManager.StopConversation();
        }
    }

    private void SetTargetPresentationInputLocked(bool locked)
    {
        if (locked == ownsTargetPresentationInput)
        {
            return;
        }

        ownsTargetPresentationInput = locked;
        if (locked)
        {
            lockedPresentationMenu = expeditionMenuController;
            lockedPresentationPlayer = playerController;
            lockedPresentationWeapon = weaponController;
            lockedPresentationRadar = radarScanner;
            lockedPresentationInteractor = playerInteractor;
            lockedPresentationReinforcement = reinforcementController;
            if (lockedPresentationMenu != null)
            {
                lockedPresentationMenu.Close();
            }
        }

        if (lockedPresentationMenu != null)
        {
            lockedPresentationMenu.SetExternalOpenLocked(this, locked);
        }
        if (lockedPresentationPlayer != null)
        {
            lockedPresentationPlayer.SetExternalControlLocked(this, locked);
        }
        if (lockedPresentationWeapon != null)
        {
            lockedPresentationWeapon.SetExternalInputLocked(this, locked);
        }
        if (lockedPresentationRadar != null)
        {
            lockedPresentationRadar.SetExternalInputLocked(this, locked);
        }
        if (lockedPresentationInteractor != null)
        {
            lockedPresentationInteractor.SetExternalInputLocked(this, locked);
        }
        if (lockedPresentationReinforcement != null)
        {
            lockedPresentationReinforcement.SetExternalInputLocked(this, locked);
        }
        if (!locked)
        {
            lockedPresentationMenu = null;
            lockedPresentationPlayer = null;
            lockedPresentationWeapon = null;
            lockedPresentationRadar = null;
            lockedPresentationInteractor = null;
            lockedPresentationReinforcement = null;
        }
    }

    private void FinishTargetPresentationWithoutCheckpoint()
    {
        InterruptTargetPresentationConversation();
        if (targetPresentationCamera != null)
        {
            targetPresentationCamera.ReleaseOwnedCinematicFocus(discoveryCameraOwner, true);
        }
        SetTargetPresentationInputLocked(false);
        targetPresentationProgress.Reset();
        targetPresentationRoutine = null;
        ClearActiveTargetPresentation();
        targetPresentationAwaitingRediscovery = true;
    }

    private void StopTargetPresentation(
        bool resetCameraImmediately,
        bool interruptConversation)
    {
        if (targetPresentationRoutine != null)
        {
            StopCoroutine(targetPresentationRoutine);
            targetPresentationRoutine = null;
        }

        if (interruptConversation)
        {
            InterruptTargetPresentationConversation();
        }

        if (targetPresentationCamera != null)
        {
            targetPresentationCamera.ReleaseOwnedCinematicFocus(
                discoveryCameraOwner, resetCameraImmediately);
        }
        if (targetPresentationProgress.Kind != TutorialTargetPresentationKind.None)
        {
            targetPresentationAwaitingRediscovery = true;
        }
        SetTargetPresentationInputLocked(false);
        targetPresentationProgress.Reset();
        ClearActiveTargetPresentation();
    }

    private void ClearActiveTargetPresentation()
    {
        SetTutorialFocusHud(discoveryCameraOwner, false);
        activeTargetTriggerStep = TutorialStep.Complete;
        activeTargetGuidanceStep = TutorialStep.Complete;
        activePresentationTarget = null;
        activePresentationHealth = null;
        activePresentationCollider = null;
        activePresentationRenderer = null;
        targetPresentationCamera = null;
        targetPresentationPlayer = null;
        activeTargetConversationTitle = string.Empty;
    }

    private void SetTutorialFocusHud(object owner, bool suppressed)
    {
        if (expeditionHUD != null)
        {
            expeditionHUD.SetCinematicMode(owner, suppressed, 0.22f);
        }
    }

    private void ScheduleDialogueGuideHandoff(
        TutorialStep guidanceStep,
        bool advanceStep,
        bool spawnCombat)
    {
        KillDialogueGuideHandoff();
        dialogueGuideHandoffTween = DOVirtual.DelayedCall(
            Mathf.Max(0f, dialogueGuideHandoffDelay),
            () =>
            {
                dialogueGuideHandoffTween = null;
                if (!isActiveAndEnabled || isCompleting || currentStep != guidanceStep)
                {
                    return;
                }

                if (advanceStep)
                {
                    TryAdvanceCheckpoint(guidanceStep);
                    return;
                }

                ApplyCurrentStepPresentation();
                RefreshOperatorGuidance();
                if (spawnCombat)
                {
                    SpawnGuaranteedCombatGroup();
                }
            }
        ).SetUpdate(true);
    }

    private void KillDialogueGuideHandoff()
    {
        dialogueGuideHandoffTween?.Kill();
        dialogueGuideHandoffTween = null;
    }

    private void RefreshDynamicNormalSalvage()
    {
        if (currentStep != TutorialStep.RadarDiscoverSalvage || normalSalvagePositioned)
        {
            return;
        }

        if (harvestTarget == null || playerRoot == null || radarScanner == null)
        {
            Debug.LogError("Tutorial Radar salvage placement is missing production references.", this);
            return;
        }

        Vector2 originalPosition = harvestTarget.transform.position;
        if (!TryFindDynamicSalvagePosition(out Vector2 targetPosition) &&
            !TryFindFallbackSalvagePosition(out targetPosition))
        {
            targetPosition = originalPosition;
            Debug.LogWarning(
                "Tutorial could not find a clear dynamic or fallback Radar-salvage position; using the authored position.",
                this
            );
        }

        harvestTarget.transform.position = targetPosition;
        normalSalvagePositioned = true;

        if (harvestRadarTarget != null)
        {
            harvestRadarTarget.SetVisible(true);
            harvestRadarTarget.SetShowOnMap(true);
            harvestRadarTarget.SetMapDiscovered(false);
        }
    }

    private bool TryFindDynamicSalvagePosition(out Vector2 position)
    {
        float radarRange = Mathf.Max(2f, radarScanner.ScanRadius);
        float minimumDistance = Mathf.Max(4f, radarRange * 0.55f);
        float maximumDistance = Mathf.Max(minimumDistance + 0.5f, radarRange * 0.88f);
        int attempts = Mathf.Max(4, salvagePlacementAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            Vector2 candidate = (Vector2)playerRoot.position +
                                direction * UnityEngine.Random.Range(minimumDistance, maximumDistance);

            if (IsValidSalvagePosition(candidate, true))
            {
                position = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }

    private bool TryFindFallbackSalvagePosition(out Vector2 position)
    {
        if (salvageFallbackPositions != null)
        {
            for (int i = 0; i < salvageFallbackPositions.Length; i++)
            {
                if (!IsValidSalvagePosition(salvageFallbackPositions[i], false))
                {
                    continue;
                }

                position = salvageFallbackPositions[i];
                Debug.LogWarning(
                    $"Tutorial Radar salvage used authored fallback position {i} after bounded dynamic placement failed.",
                    this
                );
                return true;
            }
        }

        position = default;
        return false;
    }

    private bool IsValidSalvagePosition(Vector2 position, bool requireOffscreen)
    {
        Vector2 half = mapSize * 0.5f;
        float margin = Mathf.Max(0.25f, salvageBoundaryMargin);
        if (position.x < mapCenter.x - half.x + margin ||
            position.x > mapCenter.x + half.x - margin ||
            position.y < mapCenter.y - half.y + margin ||
            position.y > mapCenter.y + half.y - margin)
        {
            return false;
        }

        float radarRange = radarScanner != null ? radarScanner.ScanRadius : 0f;
        if (playerRoot == null ||
            Vector2.Distance(playerRoot.position, position) > radarRange * 0.92f)
        {
            return false;
        }

        if (requireOffscreen && IsInsideCameraView(position, salvageViewportMargin))
        {
            return false;
        }

        if (highValueSalvageInstance != null &&
            Vector2.Distance(highValueSalvageInstance.transform.position, position) < 3f)
        {
            return false;
        }

        if (interactionTarget != null &&
            Vector2.Distance(interactionTarget.transform.position, position) < 3f)
        {
            return false;
        }

        if (alienSignal != null && Vector2.Distance(alienSignal.transform.position, position) < 3f)
        {
            return false;
        }

        int mask = salvagePlacementBlockingLayers.value;
        if (mask == 0)
        {
            mask = LayerMask.GetMask("WorldSolid", "Meteor", "HarvestObject", "Interactable");
        }

        int count = CountPlacementOverlaps(
            position,
            Mathf.Max(0.25f, salvagePlacementClearance),
            mask
        );
        return count == 0;
    }

    private static bool IsInsideCameraView(Vector2 worldPosition, float margin)
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return false;
        }

        Vector3 viewport = gameplayCamera.WorldToViewportPoint(worldPosition);
        margin = Mathf.Max(0f, margin);
        return viewport.z > 0f &&
               viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    private void RefreshCargoFeedback()
    {
        if (currentStep != TutorialStep.Cargo)
        {
            if (cargoFeedbackRoutine != null)
            {
                StopCoroutine(cargoFeedbackRoutine);
                cargoFeedbackRoutine = null;
            }

            return;
        }

        cargoFeedbackRoutine ??= StartCoroutine(CargoFeedbackRoutine());
    }

    private IEnumerator CargoFeedbackRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, cargoFeedbackDuration));
        cargoFeedbackRoutine = null;

        if (currentStep != TutorialStep.Cargo)
        {
            yield break;
        }

        TryAdvanceCheckpoint(TutorialStep.Cargo);
    }

    private void RefreshObjectiveChecks()
    {
        bool needsCheck = currentStep == TutorialStep.RoutePing ||
                          currentStep == TutorialStep.TravelNormalSalvage ||
                          currentStep == TutorialStep.TravelHighValue ||
                          currentStep == TutorialStep.FindSignalDevice ||
                          currentStep == TutorialStep.TravelSearchArea ||
                          currentStep == TutorialStep.TravelPurpleCore;

        if (!needsCheck)
        {
            if (objectiveCheckRoutine != null)
            {
                StopCoroutine(objectiveCheckRoutine);
                objectiveCheckRoutine = null;
            }

            return;
        }

        objectiveCheckRoutine ??= StartCoroutine(ObjectiveCheckRoutine());
    }

    private IEnumerator ObjectiveCheckRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(ObjectiveCheckInterval);

        while (isActiveAndEnabled)
        {
            if (playerRoot == null)
            {
                yield return wait;
                continue;
            }

            if ((currentStep == TutorialStep.RoutePing ||
                 currentStep == TutorialStep.TravelNormalSalvage) &&
                harvestTarget != null)
            {
                float distance = Vector2.Distance(playerRoot.position, harvestTarget.transform.position);

                if (IsInsideCameraView(harvestTarget.transform.position, 0f) ||
                    distance <= Mathf.Max(1.5f, highValueDiscoveryDistance))
                {
                    objectiveCheckRoutine = null;
                    TryAdvanceSupplyProgression(
                        TutorialSupplyProgressSource.TargetApproached);
                    yield break;
                }
            }
            else if (currentStep == TutorialStep.TravelHighValue && highValueSalvageInstance != null)
            {
                if (highValueAutoRegistered)
                {
                    objectiveCheckRoutine = null;
                    if (targetPresentationProgress.Phase ==
                        TutorialTargetPresentationPhase.Idle)
                    {
                        RequestTargetPresentation(
                            TutorialTargetPresentationKind.HighValueWreck);
                    }

                    yield break;
                }

                float distance = Vector2.Distance(playerRoot.position, highValueSalvageInstance.transform.position);

                if (!highValueAutoRegistered &&
                    highValueSalvageRadarTarget != null &&
                    highValueSalvageRadarTarget.IsMapDiscovered)
                {
                    objectiveCheckRoutine = null;
                    RegisterHighValueWreckDiscovery();
                    yield break;
                }

                if (!highValueAutoRegistered &&
                    radarScanner != null &&
                    radarScanner.RegisterNearbyTarget(
                        highValueSalvageRadarTarget,
                        Mathf.Max(highValueDiscoveryDistance, proximityAutoRegistrationDistance)))
                {
                    objectiveCheckRoutine = null;
                    RegisterHighValueWreckDiscovery();
                    yield break;
                }

                if (distance <= Mathf.Max(0.5f, highValueDiscoveryDistance))
                {
                    objectiveCheckRoutine = null;
                    RegisterHighValueWreckDiscovery();
                    yield break;
                }

            }
            else if (currentStep == TutorialStep.FindSignalDevice && radarScanTarget != null)
            {
                float registrationDistance = Mathf.Max(0.5f, proximityAutoRegistrationDistance);
                Vector2 playerPosition = playerRoot.position;
                Vector2 signalPosition = radarScanTarget.WorldPosition;
                bool insideRegistrationRange =
                    (playerPosition - signalPosition).sqrMagnitude <=
                    registrationDistance * registrationDistance;

                if (!signalDeviceAutoRegistered && insideRegistrationRange && radarScanner != null)
                {
                    bool registered = radarScanner.RegisterNearbyTarget(
                        radarScanTarget,
                        registrationDistance
                    );

                    if (registered || radarScanTarget.IsMapDiscovered)
                    {
                        objectiveCheckRoutine = null;
                        CompleteSignalDeviceDiscovery();
                        yield break;
                    }
                }
            }
            else if (currentStep == TutorialStep.TravelSearchArea && alienSignal != null)
            {
                float distance = Vector2.Distance(playerRoot.position, ResolveUnknownSearchAreaPosition());

                if (distance <= Mathf.Max(1f, unknownSearchAreaRadius))
                {
                    objectiveCheckRoutine = null;
                    if (TryAdvanceCheckpoint(TutorialStep.TravelSearchArea))
                    {
                        ResolveUnknownSearchArea();
                    }
                    yield break;
                }
            }
            else if (currentStep == TutorialStep.TravelPurpleCore && alienSignal != null)
            {
                float distance = Vector2.Distance(playerRoot.position, alienSignal.transform.position);

                if (!alienSignalApproachPulsePlayed &&
                    distance <= Mathf.Max(purpleCoreApproachDistance, purpleCorePulseDistance))
                {
                    alienSignalApproachPulsePlayed = true;
                    PlayAlienSignalApproachPulse();
                }

                if (distance <= Mathf.Max(0.5f, purpleCoreApproachDistance))
                {
                    objectiveCheckRoutine = null;
                    TryAdvanceCheckpoint(TutorialStep.TravelPurpleCore);
                    yield break;
                }
            }
            else
            {
                objectiveCheckRoutine = null;
                yield break;
            }

            yield return wait;
        }

        objectiveCheckRoutine = null;
    }

    private void SpawnGuaranteedCombatGroup()
    {
        if (combatSpawned)
        {
            return;
        }

        UnsubscribeSpawnedCombatEnemies();

        if (combatEnemyPrefabs == null || combatEnemyPrefabs.Length == 0 ||
            combatEnemyArrivalPositions == null || combatEnemyArrivalPositions.Length == 0)
        {
            Debug.LogError("Tutorial guaranteed combat group is not configured.", this);
            return;
        }

        combatSpawned = true;
        int count = combatEnemyArrivalPositions.Length;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = combatEnemyPrefabs[Mathf.Min(i, combatEnemyPrefabs.Length - 1)];

            if (prefab == null)
            {
                continue;
            }

            Vector2 arrivalPosition = combatEnemyArrivalPositions[i];
            GameObject enemyObject = PoolManager.Instance != null
                ? PoolManager.Instance.Get(prefab, arrivalPosition, Quaternion.identity)
                : Instantiate(prefab, arrivalPosition, Quaternion.identity);

            if (enemyObject == null)
            {
                continue;
            }

            EnemyHealth enemyHealth = enemyObject.GetComponentInChildren<EnemyHealth>(true);

            if (enemyHealth == null)
            {
                Debug.LogError("Tutorial enemy prefab has no EnemyHealth.", enemyObject);
                continue;
            }

            enemyHealth.SetRewardDropEnabled(false);
            enemyHealth.Died -= HandleCombatEnemyDied;
            enemyHealth.Died += HandleCombatEnemyDied;
            activeCombatEnemies.Add(enemyHealth);

            EnemyArrivalSpawnUtility.BeginArrival(
                enemyObject,
                arrivalPosition,
                playerRoot,
                true,
                highValueSalvagePosition
            );
        }

        if (activeCombatEnemies.Count == 0)
        {
            Debug.LogError("Tutorial combat could not spawn any valid enemies.", this);
            SetStep(TutorialStep.FindSignalDevice);
        }
    }

    private void UnsubscribeSpawnedCombatEnemies()
    {
        for (int i = 0; i < activeCombatEnemies.Count; i++)
        {
            if (activeCombatEnemies[i] != null)
            {
                activeCombatEnemies[i].Died -= HandleCombatEnemyDied;
            }
        }

        activeCombatEnemies.Clear();
    }

    private void RefreshPurpleCoreReveal()
    {
        if (currentStep != TutorialStep.RevealPurpleCore ||
            alienSignalVisualRoot == null ||
            !corePresentationProgress.TryBeginReveal())
        {
            return;
        }

        StopAlienSignalIdleMotion();
        alienSignalTween?.Kill();
        alienSignalTween = null;
        SetAlienSignalCollidersEnabled(false);
        alienSignalTargetComponent?.SetInteractionEnabled(false);
        alienSignalVisualRoot.gameObject.SetActive(true);
        alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition;
        alienSignalVisualRoot.localScale =
            alienSignalVisualRestScale * Mathf.Clamp(
                purpleCoreRevealStartScale,
                0.65f,
                0.75f);
        SetAlienSignalVisualAlpha(0f);

        if (alienSignalRadarTarget != null)
        {
            alienSignalRadarTarget.SetVisible(false);
            alienSignalRadarTarget.SetShowOnMap(false);
            alienSignalRadarTarget.SetMapDiscovered(false);
        }

        if (alienSignal != null)
        {
            AudioManager.PlayAt(
                SoundEventIds.CoreActivate,
                alienSignal.transform.position,
                0.72f);

            PlayStationaryTutorialPurpleEffect(
                alienSignalVisualRoot,
                ResolveRendererWorldCenter(
                    alienSignalCoreRenderer,
                    alienSignalVisualRoot.position),
                1.45f,
                Mathf.Clamp(purpleCoreRevealDuration, 0.45f, 0.65f));
        }

        float duration = Mathf.Clamp(purpleCoreRevealDuration, 0.45f, 0.65f);
        Sequence revealTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        alienSignalRevealTween = revealTween;
        alienSignalRevealTween.Append(
            alienSignalVisualRoot.DOScale(alienSignalVisualRestScale, duration)
                .SetEase(Ease.OutBack));

        for (int i = 0; i < alienSignalVisualRenderers.Length; i++)
        {
            SpriteRenderer renderer = alienSignalVisualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            alienSignalRevealTween.Join(
                renderer.DOFade(alienSignalVisualRestColors[i].a, duration)
                    .SetEase(Ease.OutQuad));
        }

        alienSignalRevealTween.OnComplete(CompletePurpleCoreReveal);
        alienSignalRevealTween.OnKill(() =>
        {
            if (!ReferenceEquals(alienSignalRevealTween, revealTween))
            {
                return;
            }

            alienSignalRevealTween = null;
            ResetPurpleCorePresentation(true);
        });
    }

    private void CompletePurpleCoreReveal()
    {
        alienSignalRevealTween = null;
        if (!isActiveAndEnabled ||
            currentStep != TutorialStep.RevealPurpleCore ||
            alienSignal == null ||
            !alienSignal.activeInHierarchy ||
            alienSignalVisualRoot == null ||
            !corePresentationProgress.TryCompleteReveal())
        {
            ResetPurpleCorePresentation(true);
            return;
        }

        RestoreAlienSignalVisualState();
        SetAlienSignalCollidersEnabled(true);
        StartAlienSignalIdleMotion();
        ClearUnknownSignalObjectiveMarker();
        RevealPurpleCoreOnProductionNavigation();
        TryAdvanceCheckpoint(TutorialStep.RevealPurpleCore);
    }

    private void RevealPurpleCoreOnProductionNavigation()
    {
        if (alienSignalRadarTarget == null)
        {
            Debug.LogError("Purple Core needs a RadarTarget for Tutorial Map/Radar reveal.", this);
            return;
        }

        alienSignalRadarTarget.SetVisible(true);
        alienSignalRadarTarget.SetShowOnMap(true);
        alienSignalRadarTarget.SetMapDiscovered(true);
        alienSignalRadarTarget.SetTemporaryReveal(this, 3600f);
        mapDiscoveryController?.DiscoverTarget(alienSignalRadarTarget, true);
    }

    private void RefreshUnknownMission()
    {
        if (currentStep != TutorialStep.UnknownMission)
        {
            if (unknownMissionRoutine != null)
            {
                StopCoroutine(unknownMissionRoutine);
                unknownMissionRoutine = null;
            }

            return;
        }

        if (!unknownMissionPresented)
        {
            unknownMissionPresented = true;
            ClearRelayMapGuidanceForUnknownTracking();
            AudioManager.Play(SoundEventIds.MissionReceived);
        }

        EnsureUnknownSignalObjectiveMarker();

        unknownMissionRoutine ??= StartCoroutine(UnknownMissionRoutine());
    }

    private IEnumerator UnknownMissionRoutine()
    {
        yield return new WaitForSecondsRealtime(1.1f);
        unknownMissionRoutine = null;
        TryAdvanceCheckpoint(TutorialStep.UnknownMission);
    }

    private Vector2 ResolveUnknownSearchAreaPosition()
    {
        Vector2 source = alienSignal != null ? alienSignal.transform.position : mapCenter;
        Vector2 position = source + unknownSearchAreaOffset;
        Vector2 half = mapSize * 0.5f;
        float margin = Mathf.Max(1f, unknownSearchAreaRadius);
        position.x = Mathf.Clamp(position.x, mapCenter.x - half.x + margin, mapCenter.x + half.x - margin);
        position.y = Mathf.Clamp(position.y, mapCenter.y - half.y + margin, mapCenter.y + half.y - margin);
        return position;
    }

    private void ResolveUnknownSearchArea()
    {
        expeditionMapPanel?.ClearExternalSearchRegion();
    }

    private void EnsureUnknownSignalObjectiveMarker()
    {
        if (unknownSignalObjectiveTarget != null)
        {
            unknownSignalObjectiveTarget.transform.position = ResolveUnknownSignalMarkerPosition();
            unknownSignalObjectiveTarget.SetTemporaryReveal(this, 3600f);
            return;
        }

        GameObject markerObject = new GameObject("TutorialUnknownSignalObjective");
        markerObject.transform.SetParent(transform, true);
        markerObject.transform.position = ResolveUnknownSignalMarkerPosition();
        unknownSignalObjectiveTarget = markerObject.AddComponent<RadarTarget>();
        unknownSignalObjectiveTarget.SetMarkerType(RadarMarkerType.Unknown);
        unknownSignalObjectiveTarget.SetMarkerVisual(
            null,
            new Color(0.82f, 0.72f, 1f, 1f),
            1.2f
        );
        unknownSignalObjectiveTarget.SetVisible(true);
        unknownSignalObjectiveTarget.SetShowOnMap(false);
        unknownSignalObjectiveTarget.SetMapDiscovered(false);
        unknownSignalObjectiveTarget.SetTemporaryReveal(this, 3600f);
    }

    private Vector2 ResolveUnknownSignalMarkerPosition()
    {
        return alienSignal != null
            ? alienSignal.transform.position
            : ResolveUnknownSearchAreaPosition();
    }

    private void ClearUnknownSignalObjectiveMarker()
    {
        if (unknownSignalObjectiveTarget == null)
        {
            return;
        }

        unknownSignalObjectiveTarget.ClearTemporaryReveal(this);
        GameObject markerObject = unknownSignalObjectiveTarget.gameObject;
        unknownSignalObjectiveTarget = null;
        markerObject.SetActive(false);
        Destroy(markerObject);
    }

    private void ClearPurpleCoreRadarReveal()
    {
        alienSignalRadarTarget?.ClearTemporaryReveal(this);
    }

    public bool TryRequestPurpleCoreAcquisition(
        TutorialPurpleCoreActivationSource activationSource)
    {
        if (currentStep != TutorialStep.InteractPurpleCore ||
            corePresentationProgress.Phase != TutorialCorePresentationPhase.Ready ||
            !coreAcquisitionLatch.TryBegin())
        {
            return false;
        }

        if (!ValidatePixelCurseDefinition(true))
        {
            RestorePurpleCoreAcquisitionRequest();
            return false;
        }

        alienSignalTargetComponent?.SetInteractionEnabled(false);
        alienSignalDamageReceiver?.SetReceivingEnabled(false);
        if (TryStartUnknownAccessKeyConversation())
        {
            return true;
        }

        RestorePurpleCoreAcquisitionRequest();
        return false;
    }

    public void HandlePurpleCorePlayerDamageFeedback(Vector2 hitPoint)
    {
        if (currentStep != TutorialStep.InteractPurpleCore ||
            coreAcquisitionLatch.IsActive ||
            alienSignalCoreRenderer == null)
        {
            return;
        }

        PlayStationaryTutorialPurpleEffect(
            alienSignalVisualRoot,
            hitPoint,
            0.65f,
            Mathf.Clamp(purpleCoreHitPunchDuration, 0.08f, 0.18f));
        purpleCoreHitTween?.Kill();
        Transform hitVisual = alienSignalCoreRenderer.transform;
        hitVisual.localScale = alienSignalCoreOriginalLocalScale;
        purpleCoreHitTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        purpleCoreHitTween.Append(
            hitVisual.DOPunchScale(
                alienSignalCoreOriginalLocalScale * 0.12f,
                Mathf.Clamp(purpleCoreHitPunchDuration, 0.08f, 0.18f),
                2,
                0.25f));
        purpleCoreHitTween.OnComplete(() => purpleCoreHitTween = null);
    }

    private bool TryStartUnknownAccessKeyConversation()
    {
        if (awaitingUnknownAccessKeyConversationEnd)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(unknownAccessKeyConversation) ||
            !DialogueManager.hasInstance ||
            DialogueManager.instance == null ||
            DialogueManager.MasterDatabase == null ||
            DialogueManager.MasterDatabase.GetConversation(
                unknownAccessKeyConversation) == null)
        {
            Debug.LogError(
                $"Tutorial conversation '{unknownAccessKeyConversation}' is unavailable. " +
                "The Pixel Curse checkpoint remains blocked.",
                this
            );
            return false;
        }

        unknownAccessKeyDialogueController = DialogueManager.instance;
        unknownAccessKeyDialogueController.conversationEnded -=
            HandleUnknownAccessKeyConversationEnded;
        unknownAccessKeyDialogueController.conversationEnded +=
            HandleUnknownAccessKeyConversationEnded;
        awaitingUnknownAccessKeyConversationEnd = true;

        FindFirstObjectByType<GameplayPauseMenuController>(
            FindObjectsInactive.Include)?.Close();
        StartRemoteConversation(
            unknownAccessKeyConversation,
            playerRoot,
            transform);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(
                DialogueManager.lastConversationStarted,
                unknownAccessKeyConversation,
                StringComparison.Ordinal))
        {
            StopWaitingForUnknownAccessKeyConversation(false);
            Debug.LogError(
                $"Tutorial conversation '{unknownAccessKeyConversation}' failed to start.",
                this
            );
            return false;
        }

        promptUI?.Hide();
        return true;
    }

    private void HandleUnknownAccessKeyConversationEnded(Transform actor)
    {
        if (!awaitingUnknownAccessKeyConversationEnd ||
            currentStep != TutorialStep.InteractPurpleCore ||
            !string.Equals(
                DialogueManager.lastConversationEnded,
                unknownAccessKeyConversation,
                StringComparison.Ordinal))
        {
            return;
        }

        bool completedNaturally = WasConversationCompletedNaturally(
            unknownAccessKeyConversation);
        StopWaitingForUnknownAccessKeyConversation(false);

        if (!completedNaturally)
        {
            RestorePurpleCoreAcquisitionRequest();
            return;
        }

        TryAdvanceCheckpoint(TutorialStep.InteractPurpleCore);
    }

    private void StopWaitingForUnknownAccessKeyConversation(
        bool restoreAcquisitionRequest = false)
    {
        bool conversationOwned = restoreAcquisitionRequest &&
                                 unknownAccessKeyDialogueController != null &&
                                 DialogueManager.hasInstance &&
                                 DialogueManager.isConversationActive &&
                                 string.Equals(
                                     DialogueManager.lastConversationStarted,
                                     unknownAccessKeyConversation,
                                     StringComparison.Ordinal);
        if (unknownAccessKeyDialogueController != null)
        {
            unknownAccessKeyDialogueController.conversationEnded -=
                HandleUnknownAccessKeyConversationEnded;
        }

        unknownAccessKeyDialogueController = null;
        awaitingUnknownAccessKeyConversationEnd = false;
        if (restoreAcquisitionRequest)
        {
            RestorePurpleCoreAcquisitionRequest();
        }

        if (conversationOwned)
        {
            DialogueManager.StopConversation();
        }
    }

    private void RestorePurpleCoreAcquisitionRequest()
    {
        coreAcquisitionLatch.Cancel();
        if (currentStep != TutorialStep.InteractPurpleCore ||
            corePresentationProgress.Phase != TutorialCorePresentationPhase.Ready)
        {
            return;
        }

        if (alienSignalTargetComponent != null)
        {
            alienSignalTargetComponent.ResetTarget();
            alienSignalTargetComponent.SetInteractionEnabled(true);
        }

        alienSignalDamageReceiver?.ResetProgress();
        alienSignalDamageReceiver?.SetReceivingEnabled(true);
    }

    private static bool WasConversationCompletedNaturally(string conversationTitle)
    {
        if (!DialogueManager.hasInstance || DialogueManager.instance == null)
        {
            return false;
        }

        DialoguePixelCrushersBridge bridge =
            DialogueManager.instance.GetComponent<DialoguePixelCrushersBridge>();
        return bridge != null &&
               bridge.WasConversationCompletedNaturally(conversationTitle);
    }

    private void RefreshCurseTransformation()
    {
        if (currentStep == TutorialStep.CurseTransformation && !HasPersistentPixelCurse())
        {
            StartCurseTransformation();
        }
    }

    private void ResumeFromPersistentStoryCheckpoint()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress != null &&
            !progress.IsTutorialCompleted &&
            ValidatePixelCurseDefinition(false) &&
            progress.HasPersistentStoryTrait(pixelCurseDefinition))
        {
            currentStep = TutorialStep.SettlementCommunication;
        }
    }

    private bool ValidatePixelCurseDefinition(bool logError)
    {
        bool valid = pixelCurseDefinition != null &&
                     pixelCurseDefinition.IsPersistentStoryTrait &&
                     pixelCurseDefinition.IsNegativeStatus &&
                     !pixelCurseDefinition.CanFieldDrop &&
                     !pixelCurseDefinition.CanDismantle;

        if (!valid && logError)
        {
            Debug.LogError(
                "Tutorial Pixel Curse reference must be a persistent negative story Trait that cannot be dropped or dismantled.",
                this
            );
        }

        return valid;
    }

    private bool HasPersistentPixelCurse()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        return progress != null &&
               ValidatePixelCurseDefinition(false) &&
               progress.HasPersistentStoryTrait(pixelCurseDefinition);
    }

    private void StartCurseTransformation()
    {
        if (!isActiveAndEnabled || curseTransformationRoutine != null)
        {
            return;
        }

        GameAudioLoopController.BeginTutorialCorruptionMusicTransition();
        curseTransformationRoutine = StartCoroutine(CurseTransformationRoutine());
    }

    private IEnumerator CurseTransformationRoutine()
    {
        if (!ValidatePixelCurseDefinition(true) ||
            playerRoot == null ||
            alienSignal == null ||
            !corePresentationProgress.TryBeginTransfer())
        {
            Debug.LogError(
                "Tutorial Curse transfer cannot start without the ready Purple Core and Player target.",
                this);
            curseTransformationRoutine = null;
            yield break;
        }

        SetTargetPresentationInputLocked(true);
        curseLetterbox = BossCinematicLetterboxUI.GetOrCreate();
        if (curseLetterbox == null)
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        ownsCurseLetterbox = true;
        yield return curseLetterbox.ShowRoutine(
            Mathf.Clamp(curseLetterboxHeightRatio, 0.05f, 0.12f),
            Mathf.Clamp(curseLetterboxDuration, 0.15f, 0.2f));

        if (!CanContinueCurseTransformation() ||
            !TryAcquireCurseCameraPresentation())
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        while (tutorialCamera.IsCinematicFocusBlendActive)
        {
            if (!CanContinueCurseTransformation() ||
                !tutorialCamera.IsCinematicFocusOwnedBy(this))
            {
                AbortCurseTransformationFromRoutine();
                yield break;
            }

            yield return null;
        }

        if (!CanContinueCurseTransformation() ||
            !tutorialCamera.IsCinematicFocusOwnedBy(this))
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        PlayStationaryTutorialPurpleEffect(
            alienSignalVisualRoot,
            ResolveRendererWorldCenter(
                alienSignalCoreRenderer,
                alienSignalVisualRoot.position),
            1.45f,
            Mathf.Clamp(curseImpactPauseDuration, 0.08f, 0.15f) + 0.08f);

        yield return new WaitForSecondsRealtime(
            Mathf.Clamp(curseImpactPauseDuration, 0.08f, 0.15f));

        transformationPauseManager = GameplayPauseManager.Instance;
        transformationPauseManager?.PushPause(this, "Tutorial Pixel Curse transformation");

        Sequence infiltration = BeginAlienSignalInfiltration();
        if (infiltration == null)
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        while (infiltration.IsActive() && !infiltration.IsComplete())
        {
            if (!CanContinueCurseTransformation())
            {
                AbortCurseTransformationFromRoutine();
                yield break;
            }

            yield return null;
        }

        if (!CanContinueCurseTransformation() ||
            !corePresentationProgress.TryBeginImpact())
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        float impactDuration = Mathf.Clamp(curseImpactPauseDuration, 0.08f, 0.15f);
        Tween impactTween = PlayPlayerCurseImpact(impactDuration);
        yield return new WaitForSecondsRealtime(impactDuration);

        while (impactTween != null &&
               impactTween.IsActive() &&
               !impactTween.IsComplete())
        {
            if (!CanContinueCurseTransformation())
            {
                AbortCurseTransformationFromRoutine();
                yield break;
            }

            yield return null;
        }

        ScreenFader screenFader = ScreenFader.Instance;
        if (screenFader == null)
        {
            Debug.LogError(
                "Tutorial Pixel Curse cinematic requires the persistent ScreenFader.",
                this);
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        ownsTransformationScreenFade = true;
        yield return screenFader.FadeIn(
            this,
            Mathf.Clamp(curseFadeOutDuration, 0.25f, 0.4f));

        if (!CanContinueCurseTransformation() ||
            !corePresentationProgress.TryApplyCurse())
        {
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        SetAlienSignalVisualAlpha(0f);
        SetAlienSignalCollidersEnabled(false);

        bool acquired = RunTraitAcquisitionService.TryAcquirePersistentStoryTrait(
            pixelCurseDefinition,
            playerRoot != null ? playerRoot.gameObject : null
        );

        if (!acquired && !HasPersistentPixelCurse())
        {
            Debug.LogError("Pixel Curse acquisition failed; Tutorial remains at CurseTransformation.", this);
            corePresentationProgress.RestoreReadyAfterFailedCurseApplication();
            AbortCurseTransformationFromRoutine();
            yield break;
        }

        coreAcquisitionLatch.TryComplete();
        alienSignalDamageReceiver?.SetReceivingEnabled(false);
        RefreshPlayerVisualState(true);
        statusEffectPresenter?.SetExternalVisible(true);
        statusEffectPresenter?.RefreshStatuses();
        ReleaseCurseCameraPresentation(true);

        if (ownsTransformationScreenFade)
        {
            yield return screenFader.FadeOut(
                this,
                Mathf.Clamp(curseFadeInDuration, 0.3f, 0.45f));
            ownsTransformationScreenFade = false;
        }

        if (ownsCurseLetterbox && curseLetterbox != null)
        {
            yield return curseLetterbox.HideRoutine(
                Mathf.Clamp(curseLetterboxDuration, 0.15f, 0.2f));
            ownsCurseLetterbox = false;
        }

        ShowLocalizedCurseAcquisitionBriefing();

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, acquisitionFeedbackDuration));

        curseTransformationRoutine = null;
        StopCurseInfiltration(false);
        StopTutorialPurpleEffect();
        SetTargetPresentationInputLocked(false);
        ReleaseTransformationPause();
        ClearPurpleCoreRadarReveal();
        SetStep(TutorialStep.SettlementCommunication);
    }

    private void CacheAlienSignalVisualState()
    {
        if (alienSignalVisualRoot != null)
        {
            alienSignalVisualRestScale = alienSignalVisualRoot.localScale;
            alienSignalVisualRestLocalPosition = alienSignalVisualRoot.localPosition;
            alienSignalVisualRenderers =
                alienSignalVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            alienSignalVisualRestColors = new Color[alienSignalVisualRenderers.Length];

            for (int i = 0; i < alienSignalVisualRenderers.Length; i++)
            {
                alienSignalVisualRestColors[i] = alienSignalVisualRenderers[i] != null
                    ? alienSignalVisualRenderers[i].color
                    : Color.white;
            }
        }

        alienSignalColliders = alienSignal != null
            ? alienSignal.GetComponentsInChildren<Collider2D>(true)
            : Array.Empty<Collider2D>();
        alienSignalColliderRestStates = new bool[alienSignalColliders.Length];

        for (int i = 0; i < alienSignalColliders.Length; i++)
        {
            alienSignalColliderRestStates[i] =
                alienSignalColliders[i] != null && alienSignalColliders[i].enabled;
        }

        if (alienSignalPulseRenderer != null)
        {
            alienSignalPulseRestScale = alienSignalPulseRenderer.transform.localScale;
            alienSignalPulseRestColor = alienSignalPulseRenderer.color;
            alienSignalPulseRestLocalPosition = alienSignalPulseRenderer.transform.localPosition;
        }

        if (alienSignalCoreRenderer != null)
        {
            alienSignalCoreRestColor = alienSignalCoreRenderer.color;
            alienSignalCoreTravelTransform = alienSignalCoreRenderer.transform;
            alienSignalCoreOriginalParent = alienSignalCoreTravelTransform.parent;
            alienSignalCoreOriginalLocalPosition =
                alienSignalCoreTravelTransform.localPosition;
            alienSignalCoreOriginalLocalRotation =
                alienSignalCoreTravelTransform.localRotation;
            alienSignalCoreOriginalLocalScale =
                alienSignalCoreTravelTransform.localScale;
            alienSignalCoreOriginalActive =
                alienSignalCoreTravelTransform.gameObject.activeSelf;
            alienSignalCoreTravelStateCached = true;
        }
    }

    private bool CanContinueCurseTransformation()
    {
        return isActiveAndEnabled &&
               (!ownsCurseCameraPresentation ||
                (tutorialCamera != null && tutorialCamera.IsCinematicFocusOwnedBy(this))) &&
               currentStep == TutorialStep.CurseTransformation &&
               playerRoot != null &&
               playerRoot.gameObject.activeInHierarchy &&
               playerCurseImpactVisual != null &&
               playerCurseImpactVisual.gameObject.activeInHierarchy &&
               alienSignal != null &&
               alienSignal.activeInHierarchy &&
               alienSignalVisualRoot != null &&
               alienSignalVisualRoot.gameObject.activeInHierarchy;
    }

    private Sequence BeginAlienSignalInfiltration()
    {
        purpleCoreHitTween?.Kill();
        purpleCoreHitTween = null;
        StopAlienSignalIdleMotion();
        StopCurseInfiltration(true);
        alienSignalTween?.Kill();
        alienSignalTween = null;
        RestoreAlienSignalVisualState();

        if (alienSignalVisualRoot == null ||
            alienSignalCoreRenderer == null ||
            alienSignalCoreTravelTransform == null ||
            playerCurseImpactVisual == null)
        {
            Debug.LogError(
                "Tutorial Purple Core infiltration requires Core and Player visual roots.",
                this);
            return null;
        }

        AudioManager.PlayAt(SoundEventIds.CoreActivate, alienSignal.transform.position, 0.9f);
        SetAlienSignalCollidersEnabled(false);
        alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition;
        alienSignalVisualRoot.localScale = alienSignalVisualRestScale;

        float travelDuration = Mathf.Clamp(curseTransferDuration, 0.45f, 0.7f);
        float absorptionDuration = Mathf.Clamp(
            curseCoreAbsorptionDuration,
            0.12f,
            0.2f);
        float corruptionDuration = Mathf.Clamp(
            curseShipCorruptionDuration,
            0.25f,
            0.4f);
        float impactScale = Mathf.Clamp(curseTransferImpactScale, 1.5f, 2f);
        Vector3 playerTarget = playerCurseImpactVisual.position;

        if (!TryAcquireTutorialPurpleEffect(playerCurseImpactVisual, playerTarget))
        {
            return null;
        }

        Transform corruptionTransform = activeTutorialPurpleEffect.transform;
        SpriteRenderer corruptionRenderer = activeTutorialPurpleEffectRenderer;
        Color corruptionColor = corruptionRenderer.color;
        corruptionColor.r = Mathf.Max(0.45f, corruptionColor.r);
        corruptionColor.g = Mathf.Min(0.3f, corruptionColor.g);
        corruptionColor.b = Mathf.Max(0.8f, corruptionColor.b);
        corruptionColor.a = 0f;
        corruptionRenderer.color = corruptionColor;
        corruptionTransform.position = playerTarget;
        corruptionTransform.localScale = Vector3.one * 0.25f;

        Transform coreTravelTransform = alienSignalCoreTravelTransform;
        coreTravelTransform.SetParent(null, true);
        coreTravelTransform.gameObject.SetActive(true);
        Color coreTravelColor = alienSignalCoreRestColor;
        coreTravelColor.a = Mathf.Max(0.01f, coreTravelColor.a);
        alienSignalCoreRenderer.color = coreTravelColor;

        curseInfiltrationTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        curseInfiltrationTween.Append(
            coreTravelTransform.DOMove(playerTarget, travelDuration)
                .SetEase(Ease.InOutQuad));
        curseInfiltrationTween.Join(
            coreTravelTransform.DOScale(
                    alienSignalCoreOriginalLocalScale * 1.08f,
                    travelDuration)
                .SetEase(Ease.OutQuad));
        curseInfiltrationTween.Append(
            coreTravelTransform.DOScale(
                    alienSignalCoreOriginalLocalScale * 0.72f,
                    absorptionDuration)
                .SetEase(Ease.InQuad));
        curseInfiltrationTween.Join(
            alienSignalCoreRenderer.DOFade(0f, absorptionDuration)
                .SetEase(Ease.InQuad));
        curseInfiltrationTween.AppendCallback(() =>
        {
            Color visibleCorruption = corruptionRenderer.color;
            visibleCorruption.a = Mathf.Max(
                0.65f,
                activeTutorialPurpleEffectRestColor.a);
            corruptionRenderer.color = visibleCorruption;
        });
        curseInfiltrationTween.Append(
            corruptionTransform.DOScale(
                    activeTutorialPurpleEffectRestScale * impactScale,
                    corruptionDuration)
                .SetEase(Ease.OutQuad));
        curseInfiltrationTween.Join(
            corruptionRenderer.DOFade(0f, corruptionDuration)
                .SetEase(Ease.InQuad));
        curseInfiltrationTween.OnComplete(() =>
        {
            RestoreAlienSignalCoreTravelTransform(false);
            curseInfiltrationTween = null;
            StopTutorialPurpleEffect();
        });

        return curseInfiltrationTween;
    }

    private Tween PlayPlayerCurseImpact(float duration)
    {
        if (corruptionShakeAmplitude > 0f && corruptionShakeDuration > 0f)
        {
            GungeonStyleCamera2D.RequestShake(
                corruptionShakeAmplitude,
                corruptionShakeDuration);
        }

        if (playerCurseImpactVisual == null)
        {
            return null;
        }

        playerCurseImpactTween?.Kill();
        playerCurseImpactVisual.localScale = playerCurseImpactRestScale;
        playerCurseImpactTween = playerCurseImpactVisual
            .DOPunchScale(
                playerCurseImpactRestScale * 0.18f,
                Mathf.Max(0.08f, duration),
                4,
                0.25f)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        return playerCurseImpactTween;
    }

    private void StopCurseInfiltration(bool restoreCoreVisual)
    {
        curseInfiltrationTween?.Kill();
        curseInfiltrationTween = null;
        StopTutorialPurpleEffect();
        playerCurseImpactTween?.Kill();
        playerCurseImpactTween = null;

        if (playerCurseImpactVisual != null)
        {
            playerCurseImpactVisual.localScale = playerCurseImpactRestScale;
        }

        if (restoreCoreVisual)
        {
            RestoreAlienSignalVisualState();
            SetAlienSignalCollidersEnabled(true);
        }
        else
        {
            RestoreAlienSignalCoreTravelTransform(false);
        }
    }

    private bool TryAcquireCurseCameraPresentation()
    {
        CacheRuntimeReferences();
        if (tutorialCamera == null ||
            alienSignalVisualRoot == null ||
            !tutorialCamera.AcquireGameplayFramingProfile(
                this,
                Vector2.zero,
                1f,
                Mathf.Clamp(curseCameraZoomMultiplier, 0.55f, 0.9f),
                false))
        {
            return false;
        }

        ownsCurseCameraPresentation = true;
        if (tutorialCamera.TryBeginOwnedCinematicFocusBlend(
                this,
                ResolveRendererWorldCenter(
                    alienSignalCoreRenderer,
                    alienSignalVisualRoot.position),
                Mathf.Clamp(curseCameraFocusDuration, 0.45f, 0.7f),
                null,
                out _))
        {
            SetTutorialFocusHud(this, true);
            return true;
        }

        ReleaseCurseCameraPresentation(true);
        return false;
    }

    private void ReleaseCurseCameraPresentation(bool immediate)
    {
        SetTutorialFocusHud(this, false);
        if (!ownsCurseCameraPresentation)
        {
            return;
        }

        ownsCurseCameraPresentation = false;
        if (tutorialCamera == null)
        {
            return;
        }

        tutorialCamera.ReleaseGameplayFramingProfile(this, immediate);
        tutorialCamera.ReleaseOwnedCinematicFocus(this, immediate);
    }

    private void ClearCurseLetterbox()
    {
        if (!ownsCurseLetterbox)
        {
            return;
        }

        ownsCurseLetterbox = false;
        curseLetterbox?.HideImmediate();
    }

    private void AbortCurseTransformationFromRoutine()
    {
        curseTransformationRoutine = null;
        StopCurseInfiltration(true);
        corePresentationProgress.RestoreReadyAfterInterruptedTransfer();
        ClearTransformationScreenFade();
        ReleaseCurseCameraPresentation(true);
        ClearCurseLetterbox();
        SetTargetPresentationInputLocked(false);
        ReleaseTransformationPause();
        coreAcquisitionLatch.Cancel();
        RefreshPlayerVisualState(false);
        StartAlienSignalIdleMotion();
    }

    private void ShowLocalizedCurseAcquisitionBriefing()
    {
        if (expeditionHUD == null || !VoidScrapperLocalizationService.HasInstance)
        {
            Debug.LogWarning(
                "Tutorial Curse briefing requires the persistent localization service.",
                this);
            return;
        }

        expeditionHUD.ShowObjectiveBriefing(
            VoidScrapperLocalizationService.Instance.GetText(
                Phase2CStoryDialogueIds.TutorialCurseErrorTitleTextKey),
            VoidScrapperLocalizationService.Instance.GetText(
                Phase2CStoryDialogueIds.TutorialCurseErrorBodyTextKey),
            alienSignalPeakColor);
    }

    private void PlayAlienSignalApproachPulse()
    {
        if (alienSignalPulseRenderer == null)
        {
            return;
        }

        alienSignalTween?.Kill();
        StopAlienSignalIdleWave();
        RestoreAlienSignalVisualState();
        alienSignalPulseRenderer.gameObject.SetActive(true);

        float duration = Mathf.Max(0.1f, purpleCoreApproachPulseDuration);
        Color pulseColor = alienSignalPeakColor;
        pulseColor.a = Mathf.Max(0.5f, alienSignalPulseRestColor.a);
        Color transparentPulse = pulseColor;
        transparentPulse.a = 0f;
        alienSignalPulseRenderer.color = pulseColor;

        alienSignalTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        alienSignalTween.Join(
            alienSignalPulseRenderer.transform.DOScale(alienSignalPulseRestScale * 1.75f, duration)
                .SetEase(Ease.OutQuad)
        );
        alienSignalTween.Join(alienSignalPulseRenderer.DOColor(transparentPulse, duration));

        if (alienSignalVisualRoot != null)
        {
            alienSignalTween.Join(
                alienSignalVisualRoot.DOPunchScale(
                    alienSignalVisualRestScale * 0.08f,
                    duration,
                    1,
                    0.15f
                )
            );
        }

        alienSignalTween.OnComplete(() =>
        {
            alienSignalTween = null;
            RestoreAlienSignalVisualState();
            StartAlienSignalIdleWave();
        });
    }

    private void StopCurseTransformation()
    {
        if (curseTransformationRoutine != null)
        {
            StopCoroutine(curseTransformationRoutine);
            curseTransformationRoutine = null;
        }

        bool restoreCoreVisual = corePresentationProgress.Phase !=
            TutorialCorePresentationPhase.CurseApplied;
        StopCurseInfiltration(restoreCoreVisual);
        corePresentationProgress.RestoreReadyAfterInterruptedTransfer();
        alienSignalTween?.Kill();
        alienSignalTween = null;
        ClearTransformationScreenFade();
        ReleaseCurseCameraPresentation(true);
        ClearCurseLetterbox();
        SetTargetPresentationInputLocked(false);
        ReleaseTransformationPause();
        if (corePresentationProgress.Phase != TutorialCorePresentationPhase.CurseApplied)
        {
            coreAcquisitionLatch.Cancel();
        }
        RefreshPlayerVisualState(false);
    }

    private void RefreshPlayerVisualState(bool playCurseGlitch)
    {
        if (playerVisualStateController == null)
        {
            playerVisualStateController = null;
            return;
        }

        playerVisualStateController.RefreshVisualState();
        if (playCurseGlitch)
        {
            playerVisualStateController.PlayCurseAcquiredGlitch();
        }
    }

    private void RestoreAlienSignalCoreTravelTransform(bool restoreVisual)
    {
        if (!alienSignalCoreTravelStateCached ||
            alienSignalCoreTravelTransform == null)
        {
            return;
        }

        alienSignalCoreTravelTransform.SetParent(
            alienSignalCoreOriginalParent,
            false);
        alienSignalCoreTravelTransform.localPosition =
            alienSignalCoreOriginalLocalPosition;
        alienSignalCoreTravelTransform.localRotation =
            alienSignalCoreOriginalLocalRotation;
        alienSignalCoreTravelTransform.localScale =
            alienSignalCoreOriginalLocalScale;
        alienSignalCoreTravelTransform.gameObject.SetActive(
            alienSignalCoreOriginalActive);

        if (restoreVisual && alienSignalCoreRenderer != null)
        {
            alienSignalCoreRenderer.color = alienSignalCoreRestColor;
        }
    }

    private void RestoreAlienSignalVisualState()
    {
        RestoreAlienSignalCoreTravelTransform(true);
        if (alienSignalVisualRoot != null)
        {
            alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition;
            alienSignalVisualRoot.localScale = alienSignalVisualRestScale;
        }

        for (int i = 0; i < alienSignalVisualRenderers.Length; i++)
        {
            if (alienSignalVisualRenderers[i] != null)
            {
                alienSignalVisualRenderers[i].color =
                    alienSignalVisualRestColors[i];
            }
        }

        if (alienSignalCoreRenderer != null)
        {
            alienSignalCoreRenderer.color = alienSignalCoreRestColor;
        }

        if (alienSignalPulseRenderer != null)
        {
            alienSignalPulseRenderer.transform.localScale = alienSignalPulseRestScale;
            alienSignalPulseRenderer.color = alienSignalPulseRestColor;
        }
    }

    private void SetAlienSignalVisualAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < alienSignalVisualRenderers.Length; i++)
        {
            SpriteRenderer renderer = alienSignalVisualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = alienSignalVisualRestColors[i];
            color.a *= clampedAlpha;
            renderer.color = color;
        }
    }

    private void SetAlienSignalCollidersEnabled(bool restoreAuthoredState)
    {
        for (int i = 0; i < alienSignalColliders.Length; i++)
        {
            if (alienSignalColliders[i] != null)
            {
                alienSignalColliders[i].enabled = restoreAuthoredState &&
                    alienSignalColliderRestStates[i];
            }
        }
    }

    private void StartAlienSignalIdleMotion()
    {
        if (!CanPresentIdlePurpleCore())
        {
            StopAlienSignalIdleMotion();
            return;
        }
        if (alienSignalIdleTween != null && alienSignalIdleTween.IsActive())
        {
            StartAlienSignalIdleWave();
            return;
        }

        float distance = Mathf.Clamp(purpleCoreIdleFloatDistance, 0.08f, 0.15f);
        float duration = Mathf.Clamp(purpleCoreIdleFloatDuration, 1.5f, 2f);
        alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition;
        alienSignalIdleTween = alienSignalVisualRoot
            .DOLocalMoveY(alienSignalVisualRestLocalPosition.y + distance, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        StartAlienSignalIdleWave();
    }

    private void StopAlienSignalIdleMotion()
    {
        StopAlienSignalIdleWave();
        alienSignalIdleTween?.Kill();
        alienSignalIdleTween = null;

        if (alienSignalVisualRoot != null)
        {
            alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition;
        }
    }

    private bool CanPresentIdlePurpleCore()
    {
        return isActiveAndEnabled && alienSignalVisualRoot != null &&
               alienSignalVisualRoot.gameObject.activeInHierarchy &&
               corePresentationProgress.Phase == TutorialCorePresentationPhase.Ready &&
               !HasPersistentPixelCurse();
    }

    private void StartAlienSignalIdleWave()
    {
        if (!CanPresentIdlePurpleCore() || alienSignalPulseRenderer == null ||
            alienSignalTween != null || alienSignalIdleWaveTween != null) return;
        Transform wave = alienSignalPulseRenderer.transform;
        wave.localPosition = alienSignalPulseRestLocalPosition;
        wave.localScale = alienSignalPulseRestScale;
        Color color = alienSignalPulseRestColor;
        color.a = Mathf.Min(color.a, 0.22f);
        alienSignalPulseRenderer.color = color;
        wave.gameObject.SetActive(true);
        alienSignalIdleWaveTween = DOTween.Sequence().SetUpdate(true)
            .SetLoops(-1, LoopType.Restart).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        alienSignalIdleWaveTween.Join(wave.DOScale(alienSignalPulseRestScale * 1.55f, 1.6f).SetEase(Ease.OutSine));
        alienSignalIdleWaveTween.Join(alienSignalPulseRenderer.DOFade(0f, 1.6f).SetEase(Ease.InSine));
    }

    private void StopAlienSignalIdleWave()
    {
        alienSignalIdleWaveTween?.Kill(false);
        alienSignalIdleWaveTween = null;
        if (alienSignalPulseRenderer == null) return;
        alienSignalPulseRenderer.transform.localPosition = alienSignalPulseRestLocalPosition;
        alienSignalPulseRenderer.transform.localScale = alienSignalPulseRestScale;
        alienSignalPulseRenderer.color = alienSignalPulseRestColor;
        alienSignalPulseRenderer.gameObject.SetActive(false);
    }

    private void ResetPurpleCorePresentation(bool resetProgress)
    {
        purpleCoreHitTween?.Kill();
        purpleCoreHitTween = null;
        alienSignalRevealTween?.Kill();
        alienSignalRevealTween = null;
        StopAlienSignalIdleMotion();
        bool restoreReadyState = !HasPersistentPixelCurse() &&
                                 corePresentationProgress.Phase !=
                                 TutorialCorePresentationPhase.CurseApplied;

        if (restoreReadyState)
        {
            RestoreAlienSignalVisualState();
        }

        SetAlienSignalCollidersEnabled(restoreReadyState);

        if (resetProgress)
        {
            corePresentationProgress.Reset();
            coreAcquisitionLatch.Reset();
            alienSignalDamageReceiver?.ResetProgress();
        }
    }

    private void ReleaseTransformationPause()
    {
        if (transformationPauseManager != null)
        {
            transformationPauseManager.PopPause(this);
            transformationPauseManager = null;
        }
    }

    private void ClearTransformationScreenFade()
    {
        if (!ownsTransformationScreenFade)
        {
            return;
        }

        ownsTransformationScreenFade = false;
        ScreenFader.Instance?.SetClearImmediate();
    }

    private void RefreshRescueSequence()
    {
        if (currentStep != TutorialStep.SettlementCommunication)
        {
            StopRescueSequence();
            return;
        }

        if (!isActiveAndEnabled || awaitingRescueConversationEnd || rescueSequenceRoutine != null)
        {
            return;
        }

        rescueSequenceRoutine = StartCoroutine(RescueSequenceRoutine());
    }

    private IEnumerator RescueSequenceRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, rescueSignalDelay));
        rescueSequenceRoutine = null;
        TryStartRescueConversation();
    }

    private void TryStartRescueConversation()
    {
        StepPresentation presentation = FindPresentation(TutorialStep.SettlementCommunication);
        DialogueSystemTrigger trigger = presentation != null ? presentation.dialogueTrigger : null;

        if (trigger == null || string.IsNullOrWhiteSpace(trigger.conversation))
        {
            Debug.LogError("Tutorial Settlement communication trigger is missing.", this);
            return;
        }

        if (!DialogueManager.hasInstance || DialogueManager.instance == null)
        {
            Debug.LogError("Persistent Boot-owned Dialogue Manager is unavailable.", this);
            return;
        }

        DialogueDatabase database = DialogueManager.MasterDatabase;

        if (database == null || database.GetConversation(trigger.conversation) == null)
        {
            Debug.LogError(
                $"Tutorial conversation '{trigger.conversation}' is missing from the active Dialogue Database.",
                this
            );
            return;
        }

        rescueDialogueController = DialogueManager.instance;
        rescueDialogueController.conversationEnded -= HandleRescueConversationEnded;
        rescueDialogueController.conversationEnded += HandleRescueConversationEnded;
        awaitingRescueConversationEnd = true;

        FindFirstObjectByType<GameplayPauseMenuController>(FindObjectsInactive.Include)?.Close();
        StartRemoteConversation(trigger.conversation, playerRoot, null, true, trigger);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(DialogueManager.lastConversationStarted, trigger.conversation, StringComparison.Ordinal))
        {
            StopWaitingForRescueConversation();
            Debug.LogError($"Tutorial conversation '{trigger.conversation}' failed to start.", this);
            return;
        }

        promptUI?.Hide();
    }

    private void StartRemoteConversation(
        string conversationTitle,
        Transform actor,
        Transform conversant,
        bool playIncomingCue = true,
        DialogueSystemTrigger trigger = null)
    {
        DialogueSystemController controller = DialogueManager.instance;
        // Only the explicit remote authorities above use this helper. In
        // particular, FieldNpcObjective's local RescueContact path does not.
        void HandleStarted(Transform startedActor)
        {
            if (playIncomingCue && string.Equals(
                    DialogueManager.lastConversationStarted, conversationTitle,
                    StringComparison.Ordinal))
            {
                AudioManager.Play(string.IsNullOrWhiteSpace(incomingTransmissionSoundEventId)
                    ? SoundEventIds.DialogueCommIncoming
                    : incomingTransmissionSoundEventId);
                DialogueCinematicPresentationController.BeginIncomingCommunication();
            }
        }

        controller.conversationStarted += HandleStarted;
        try
        {
            if (trigger != null)
            {
                trigger.TryStart(actor);
            }
            else
            {
                DialogueManager.StartConversation(conversationTitle, actor, conversant);
            }
        }
        finally
        {
            controller.conversationStarted -= HandleStarted;
        }
    }

    private void HandleRescueConversationEnded(Transform actor)
    {
        if (!awaitingRescueConversationEnd || currentStep != TutorialStep.SettlementCommunication)
        {
            return;
        }

        StepPresentation presentation = FindPresentation(TutorialStep.SettlementCommunication);
        string expectedConversation = presentation != null && presentation.dialogueTrigger != null
            ? presentation.dialogueTrigger.conversation
            : string.Empty;

        if (string.IsNullOrWhiteSpace(expectedConversation) ||
            !string.Equals(DialogueManager.lastConversationEnded, expectedConversation, StringComparison.Ordinal))
        {
            return;
        }

        bool completedNaturally = WasConversationCompletedNaturally(
            expectedConversation);
        StopWaitingForRescueConversation();

        if (completedNaturally)
        {
            TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
        }
        else
        {
            RefreshRescueSequence();
        }
    }

    private void StopRescueSequence()
    {
        if (rescueSequenceRoutine != null)
        {
            StopCoroutine(rescueSequenceRoutine);
            rescueSequenceRoutine = null;
        }

        StopWaitingForRescueConversation();
    }

    private void StopWaitingForRescueConversation()
    {
        if (rescueDialogueController != null)
        {
            rescueDialogueController.conversationEnded -= HandleRescueConversationEnded;
        }

        rescueDialogueController = null;
        awaitingRescueConversationEnd = false;
    }

    private void RefreshEmergencyReturn()
    {
        if (currentStep != TutorialStep.AcquireEmergencyReturn &&
            currentStep != TutorialStep.EmergencyReturn)
        {
            return;
        }

        if (reinforcementController == null || emergencyReturnController == null)
        {
            Debug.LogError("Tutorial Emergency Return production components are missing from Player.", this);
            return;
        }

        if (emergencyReturnDefinition == null ||
            !emergencyReturnDefinition.HasEffect(ReinforcementEffectType.EmergencyReturn))
        {
            Debug.LogError("Tutorial Emergency Return definition is missing or invalid.", this);
            return;
        }

        if (reinforcementController.EquippedDefinition == emergencyReturnDefinition)
        {
            if (currentStep == TutorialStep.AcquireEmergencyReturn)
            {
                TryAdvanceCheckpoint(TutorialStep.AcquireEmergencyReturn);
            }

            return;
        }

        if (currentStep == TutorialStep.EmergencyReturn)
        {
            SetStep(TutorialStep.AcquireEmergencyReturn);
            return;
        }

        if (!emergencyReturnPickupSpawned)
        {
            emergencyReturnPickupSpawned = TrySpawnReinforcementPickupNearPlayer(
                emergencyReturnDefinition,
                out emergencyReturnPickup
            );
        }
    }

    private void RefreshReinforcementCheckpoint()
    {
        if (reinforcementController == null)
        {
            return;
        }

        if (currentStep == TutorialStep.EquipDefensiveActive &&
            reinforcementController.EquippedDefinition == defensiveReinforcementDefinition)
        {
            TryAdvanceCheckpoint(TutorialStep.EquipDefensiveActive);
        }
        else if (currentStep == TutorialStep.AcquireEmergencyReturn &&
                 reinforcementController.EquippedDefinition == emergencyReturnDefinition)
        {
            TryAdvanceCheckpoint(TutorialStep.AcquireEmergencyReturn);
        }
    }

    private bool TrySpawnReinforcementPickupNearPlayer(
        ReinforcementDefinition definition,
        out ReinforcementPickup pickup)
    {
        pickup = null;
        if (definition == null || reinforcementPickupPrefab == null || playerRoot == null)
        {
            Debug.LogError("Tutorial production Reinforcement pickup references are incomplete.", this);
            return false;
        }

        Vector2 playerPosition = playerRoot.position;
        Vector2[] offsets =
        {
            new Vector2(0f, 1.6f),
            new Vector2(1.25f, 1.1f),
            new Vector2(-1.25f, 1.1f),
            new Vector2(1.5f, 0f),
            new Vector2(-1.5f, 0f)
        };

        Vector2 spawnPosition = playerPosition + offsets[0];
        bool found = false;
        int mask = salvagePlacementBlockingLayers.value;
        if (mask == 0)
        {
            mask = LayerMask.GetMask("WorldSolid", "Meteor", "HarvestObject");
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 candidate = ClampInsideTutorialBounds(playerPosition + offsets[i], 0.75f);
            int count = CountPlacementOverlaps(
                candidate,
                Mathf.Max(0.1f, pickupSpawnClearance),
                mask
            );

            if (count == 0)
            {
                spawnPosition = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning(
                "Tutorial Emergency Return pickup used its clamped near-player fallback position.",
                this
            );
        }

        GameObject instance = PoolManager.Instance != null
            ? PoolManager.Instance.Get(reinforcementPickupPrefab.gameObject, spawnPosition, Quaternion.identity)
            : Instantiate(reinforcementPickupPrefab.gameObject, spawnPosition, Quaternion.identity);

        pickup = instance != null ? instance.GetComponent<ReinforcementPickup>() : null;
        if (pickup == null)
        {
            Debug.LogError("Tutorial could not spawn the production ReinforcementPickup prefab.", this);
            return false;
        }

        pickup.Initialize(definition, -1, 0.25f);
        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, spawnPosition);
        return true;
    }

    private Vector2 ClampInsideTutorialBounds(Vector2 position, float margin)
    {
        Vector2 half = mapSize * 0.5f;
        position.x = Mathf.Clamp(position.x, mapCenter.x - half.x + margin, mapCenter.x + half.x - margin);
        position.y = Mathf.Clamp(position.y, mapCenter.y - half.y + margin, mapCenter.y + half.y - margin);
        return position;
    }

    private int CountPlacementOverlaps(Vector2 position, float radius, int layerMask)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layerMask);
        filter.useTriggers = true;
        return Physics2D.OverlapCircle(position, radius, filter, placementBuffer);
    }

    private static void DestroyRuntimeObject(GameObject runtimeObject)
    {
        if (runtimeObject != null)
        {
            Destroy(runtimeObject);
        }
    }

    private void MarkTutorialCompletedAfterReturn()
    {
        if (isCompleting)
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saveManager = SaveManager.Instance;

        if (progress == null || saveManager == null)
        {
            Debug.LogError("Tutorial return completed, but progression/save services are missing.", this);
            return;
        }

        isCompleting = true;
        ClearTutorialHealthFloor();
        progress.TryMarkTutorialCompleted();
        progress.AddUnlockFlag(StoryProgressionIds.FirstSettlementPendingFlag);
        saveManager.Save(progress);

        TutorialStep previousStep = currentStep;
        currentStep = TutorialStep.Complete;
        ApplyCurrentStepPresentation();
        StepChanged?.Invoke(previousStep, currentStep);
    }

    private void ValidateProductionMenuDependency()
    {
        if (warnedMissingProductionMenu ||
            expeditionMenuController != null ||
            currentStep != TutorialStep.Map && currentStep != TutorialStep.RoutePing)
        {
            return;
        }

        warnedMissingProductionMenu = true;
        Debug.LogError(
            "Tutorial requires the shared ExpeditionMenuController/Map/Inventory presentation in the scene. " +
            "The step will not be completed by input alone because that would fake production system use.",
            this
        );
    }

    private int ResolveCurrentCargoLoad()
    {
        if (cargoController != null)
        {
            return cargoController.CurrentLoad;
        }

        RunManager runManager = RunManager.Instance;
        return runManager != null && runManager.HasActiveRun
            ? runManager.CurrentRun.CurrentCargoLoad
            : 0;
    }

    private void ApplyTutorialHealthFloor()
    {
        playerHealth?.SetMinimumHealthFloor(Mathf.Max(1f, tutorialMinimumHealth));
    }

    private void ClearTutorialHealthFloor()
    {
        playerHealth?.ClearMinimumHealthFloor();
    }

    private void StopStepOwnedRoutine(TutorialStep previousStep)
    {
        if (previousStep == TutorialStep.InteractSignalDevice &&
            relayNarrativeProgress.Phase != TutorialRelayNarrativePhase.Completed)
        {
            StopRelayNarrative(true);
        }

        if ((previousStep == TutorialStep.Move || previousStep == TutorialStep.Dash) &&
            controlPacingRoutine != null)
        {
            StopCoroutine(controlPacingRoutine);
            controlPacingRoutine = null;
        }

        if (previousStep == TutorialStep.Cargo && cargoFeedbackRoutine != null)
        {
            StopCoroutine(cargoFeedbackRoutine);
            cargoFeedbackRoutine = null;
        }

        if ((previousStep == TutorialStep.RoutePing ||
             previousStep == TutorialStep.TravelNormalSalvage ||
             previousStep == TutorialStep.DestroyNormalSalvage ||
             previousStep == TutorialStep.TravelHighValue ||
             previousStep == TutorialStep.FindSignalDevice ||
             previousStep == TutorialStep.TravelSearchArea ||
             previousStep == TutorialStep.TravelPurpleCore) &&
            objectiveCheckRoutine != null)
        {
            StopCoroutine(objectiveCheckRoutine);
            objectiveCheckRoutine = null;
        }

        if (previousStep == activeTargetTriggerStep &&
            targetPresentationRoutine != null)
        {
            StopTargetPresentation(true, true);
        }

        if (previousStep == TutorialStep.UnknownMission && unknownMissionRoutine != null)
        {
            StopCoroutine(unknownMissionRoutine);
            unknownMissionRoutine = null;
        }
    }

    private void StopAllStepRoutines()
    {
        KillDialogueGuideHandoff();
        StopTargetPresentation(true, true);

        if (controlPacingRoutine != null)
        {
            StopCoroutine(controlPacingRoutine);
            controlPacingRoutine = null;
        }

        if (introAdvanceRoutine != null)
        {
            StopCoroutine(introAdvanceRoutine);
            introAdvanceRoutine = null;
        }

        if (cargoFeedbackRoutine != null)
        {
            StopCoroutine(cargoFeedbackRoutine);
            cargoFeedbackRoutine = null;
        }

        if (objectiveCheckRoutine != null)
        {
            StopCoroutine(objectiveCheckRoutine);
            objectiveCheckRoutine = null;
        }

        if (unknownMissionRoutine != null)
        {
            StopCoroutine(unknownMissionRoutine);
            unknownMissionRoutine = null;
        }

        if (rescueDialogueController != null)
        {
            rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
            rescueDialogueController.conversationEnded -= HandleRescueConversationEnded;
        }

        bool stopOwnedOperatorConversation =
            openingConversationStarted &&
            !string.IsNullOrWhiteSpace(activeOperatorConversationTitle) &&
            DialogueManager.hasInstance &&
            DialogueManager.isConversationActive &&
            string.Equals(
                DialogueManager.lastConversationStarted,
                activeOperatorConversationTitle,
                StringComparison.Ordinal);

        rescueDialogueController = null;
        openingConversationStarted = false;
        openingTransmissionProgress.FinishTransmission();
        activeOperatorGuidanceStep = TutorialStep.Complete;
        activeOperatorConversationTitle = string.Empty;
        if (stopOwnedOperatorConversation)
        {
            DialogueManager.StopConversation();
        }
    }

    private void ApplyCurrentStepPresentation()
    {
        StepPresentation currentPresentation = null;

        if (stepPresentations != null)
        {
            for (int i = 0; i < stepPresentations.Length; i++)
            {
                StepPresentation presentation = stepPresentations[i];

                if (presentation == null)
                {
                    continue;
                }

                bool isCurrent = presentation.step == currentStep;

                if (presentation.worldRoot != null)
                {
                    presentation.worldRoot.SetActive(isCurrent);
                }

                if (isCurrent && currentPresentation == null)
                {
                    currentPresentation = presentation;
                }
            }
        }

        promptUI?.Hide();

        string objectiveDetail = ResolveObjectiveDetail(currentPresentation);
        if (currentStep == TutorialStep.Complete || string.IsNullOrWhiteSpace(objectiveDetail))
        {
            expeditionMapPanel?.ClearExternalObjective();
            return;
        }

        expeditionMapPanel?.SetExternalObjective("튜토리얼", objectiveDetail);
        bool deferForOperatorDialogue = ShouldDeferBriefingForOperatorDialogue(currentStep);
        bool dialogueActive = DialogueManager.hasInstance && DialogueManager.isConversationActive;
        if (deferForOperatorDialogue || dialogueActive)
        {
            expeditionHUD?.HideObjectiveBriefing();
            return;
        }

        if (ShouldShowObjectiveBriefing(currentStep))
        {
            Color accent = currentStep >= TutorialStep.UnknownMission &&
                           currentStep <= TutorialStep.CurseTransformation
                ? alienSignalPeakColor
                : new Color(0.42f, 0.9f, 1f, 1f);
            expeditionHUD?.ShowObjectiveBriefing(
                ResolveObjectiveTitle(currentStep),
                objectiveDetail,
                accent
            );
        }
    }

    private string ResolveObjectiveDetail(StepPresentation presentation)
    {
        if (currentStep == TutorialStep.RadarDiscoverSalvage &&
            radarScanner != null &&
            VoidScrapperLocalizationService.HasInstance)
        {
            localizationArguments.Clear();
            localizationArguments["radar"] = radarScanner.RadarBindingDisplay;
            localizationArguments["scan"] = radarScanner.QuickScanBindingDisplay;
            return VoidScrapperLocalizationService.Instance.FormatText(
                Phase2CStoryDialogueIds.TutorialRadarObjectiveTextKey,
                localizationArguments);
        }

        if (currentStep == TutorialStep.FindSignalDevice &&
            radarScanner != null &&
            VoidScrapperLocalizationService.HasInstance)
        {
            localizationArguments.Clear();
            localizationArguments["radar"] = radarScanner.RadarBindingDisplay;
            localizationArguments["scan"] = radarScanner.QuickScanBindingDisplay;
            string textKey =
                Phase2CStoryDialogueIds.ResolveUnknownSignalRadarObjectiveTextKey(
                    radarScanner.IsRadarActive
                );
            return VoidScrapperLocalizationService.Instance.FormatText(
                textKey,
                localizationArguments
            );
        }

        if (currentStep == TutorialStep.RoutePing &&
            VoidScrapperLocalizationService.HasInstance)
        {
            return VoidScrapperLocalizationService.Instance.GetText(
                Phase2CStoryDialogueIds.TutorialRouteOptionalTextKey);
        }

        if (currentStep == TutorialStep.UnknownMission ||
            currentStep == TutorialStep.TravelSearchArea ||
            currentStep == TutorialStep.RevealPurpleCore)
        {
            return ResolveLocalizedTutorialText(
                Phase2CStoryDialogueIds.TutorialUnknownSignalObjectiveTextKey);
        }

        if (currentStep == TutorialStep.TravelPurpleCore)
        {
            return ResolveLocalizedTutorialText(
                Phase2CStoryDialogueIds.TutorialPurpleCoreTravelTextKey);
        }

        if (currentStep == TutorialStep.InteractPurpleCore)
        {
            return ResolveLocalizedTutorialText(
                Phase2CStoryDialogueIds.TutorialPurpleCoreInteractTextKey);
        }

        if (currentStep == TutorialStep.CurseTransformation)
        {
            return ResolveLocalizedTutorialText(
                Phase2CStoryDialogueIds.TutorialCurseErrorBodyTextKey);
        }

        if (currentStep == TutorialStep.DestroyNormalSalvage)
        {
            return "공격하여 자원을 회수하세요!";
        }

        if (currentStep == TutorialStep.TravelHighValue)
        {
            return "레이더를 사용해 고가치 잔해를 찾으세요.";
        }

        if (currentStep == TutorialStep.DestroyHighValue)
        {
            return "\uACF5\uACA9\uD558\uC5EC \uC218\uD655\uD558\uC138\uC694!";
        }

        if (currentStep == TutorialStep.Cargo)
        {
            int current = cargoController != null ? cargoController.CurrentLoad : 0;
            int capacity = cargoController != null ? cargoController.MaxCapacity : 0;
            return $"회수한 자원은 적재량을 차지합니다. ({current}/{capacity}) 적재 한도를 확인하세요.";
        }

        if (presentation == null || string.IsNullOrWhiteSpace(presentation.instructionTemplate))
        {
            return string.Empty;
        }

        if (promptUI != null)
        {
            return promptUI.ResolveInstruction(
                presentation.instructionTemplate,
                presentation.actionMapName,
                presentation.actionName,
                presentation.fallbackBindingDisplay,
                presentation.useCompositeBindingParts
            );
        }

        return presentation.instructionTemplate.Replace("{0}", presentation.fallbackBindingDisplay);
    }

    private static bool ShouldShowObjectiveBriefing(TutorialStep step)
    {
        return step == TutorialStep.Move ||
               step == TutorialStep.AimAndFire ||
               step == TutorialStep.Dash ||
               step == TutorialStep.RadarDiscoverSalvage ||
               step == TutorialStep.Map ||
               step == TutorialStep.RoutePing ||
               step == TutorialStep.TravelNormalSalvage ||
               step == TutorialStep.DestroyNormalSalvage ||
               step == TutorialStep.CollectResources ||
               step == TutorialStep.TravelHighValue ||
               step == TutorialStep.DestroyHighValue ||
               step == TutorialStep.EquipDefensiveActive ||
               step == TutorialStep.UseDefensiveActive ||
               step == TutorialStep.Combat ||
               step == TutorialStep.FindSignalDevice ||
               step == TutorialStep.InteractSignalDevice ||
               step == TutorialStep.UnknownMission ||
               step == TutorialStep.TravelSearchArea ||
               step == TutorialStep.TravelPurpleCore ||
               step == TutorialStep.AcquireEmergencyReturn ||
               step == TutorialStep.EmergencyReturn;
    }

    private bool ShouldDeferBriefingForOperatorDialogue(TutorialStep step)
    {
        if (step == TutorialStep.Move &&
            !openingTransmissionProgress.HasConfirmedMovement)
        {
            return false;
        }

        return Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                   step,
                   highValueAutoRegistered,
                   out _) &&
               !operatorGuidanceProgress.IsComplete(step);
    }

    private string ResolveObjectiveTitle(TutorialStep step)
    {
        return step switch
        {
            TutorialStep.Move => "이동",
            TutorialStep.AimAndFire => "무장 확인",
            TutorialStep.Dash => "대쉬 사용",
            TutorialStep.RadarDiscoverSalvage => "레이더 탐색",
            TutorialStep.Map => "지도 확인",
            TutorialStep.RoutePing => "경로 표시",
            TutorialStep.TravelNormalSalvage => "회수 신호 접근",
            TutorialStep.DestroyNormalSalvage => "잔해 수확",
            TutorialStep.CollectResources => "자원 회수",
            TutorialStep.Cargo => "적재량 확인",
            TutorialStep.TravelHighValue => "고가치 잔해 탐색",
            TutorialStep.DestroyHighValue => "고가치 잔해 회수",
            TutorialStep.EquipDefensiveActive => "액티브 장비 획득",
            TutorialStep.UseDefensiveActive => "액티브 장비 사용",
            TutorialStep.Combat => "위협 제거",
            TutorialStep.FindSignalDevice => "미확인 신호 탐색",
            TutorialStep.InteractSignalDevice => "신호 장치 조사",
            TutorialStep.UnknownMission => ResolveLocalizedTutorialText(
                Phase2CStoryDialogueIds.TutorialUnknownSignalTitleTextKey),
            TutorialStep.TravelSearchArea => "미확인 지역 조사",
            TutorialStep.TravelPurpleCore => "미확인 코어 조사",
            TutorialStep.AcquireEmergencyReturn => "긴급복귀 장비 획득",
            TutorialStep.EmergencyReturn => "정착지로 복귀",
            _ => "튜토리얼"
        };
    }

    private string ResolveLocalizedTutorialText(string textKey)
    {
        if (!VoidScrapperLocalizationService.HasInstance)
        {
            Debug.LogWarning(
                $"Tutorial localization service is unavailable for '{textKey}'.",
                this);
            return string.Empty;
        }

        return VoidScrapperLocalizationService.Instance.GetText(textKey);
    }

    private StepPresentation FindPresentation(TutorialStep step)
    {
        if (stepPresentations == null)
        {
            return null;
        }

        for (int i = 0; i < stepPresentations.Length; i++)
        {
            StepPresentation presentation = stepPresentations[i];

            if (presentation != null && presentation.step == step)
            {
                return presentation;
            }
        }

        return null;
    }
}
