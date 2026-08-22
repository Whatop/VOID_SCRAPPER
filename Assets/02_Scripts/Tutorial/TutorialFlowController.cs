using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialFlowController : MonoBehaviour
{
    private const string FirstSettlementPendingFlag = "story_first_settlement_unknown_core_pending";
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
    [SerializeField] private RewardDefinition highValueTeachingReward;
    [SerializeField] private ReinforcementDefinition defensiveReinforcementDefinition;
    [SerializeField] private GameObject[] combatEnemyPrefabs = Array.Empty<GameObject>();
    [SerializeField] private Vector2[] combatEnemyArrivalPositions = Array.Empty<Vector2>();

    [Header("Signal Relay")]
    [SerializeField] private GameObject radarTarget;
    [SerializeField] private RadarTarget radarScanTarget;
    [SerializeField] private GameObject interactionTarget;
    [SerializeField] private TutorialInteractionTarget interactionTargetComponent;
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

    [Header("Pixel Curse Transformation")]
    [SerializeField] private TraitDefinition pixelCurseDefinition;
    [SerializeField] private PlayerVisualStateController playerVisualStateController;
    [SerializeField] private StatusEffectHUDPresenter statusEffectPresenter;
    [SerializeField, Min(0.1f)] private float corruptionPeakDelay = 0.65f;
    [SerializeField, Min(0.1f)] private float acquisitionFeedbackDuration = 0.75f;
    [SerializeField, Min(0.01f)] private float corruptionDarkenDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float corruptionRecoveryDuration = 0.2f;
    [SerializeField, Min(0f)] private float corruptionShakeAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float corruptionShakeDuration = 0.24f;
    [SerializeField] private Color alienSignalPeakColor = new Color(0.75f, 0.2f, 1f, 1f);
    [SerializeField, TextArea(1, 2)] private string curseAcquiredFeedback =
        "??? 신호가 기체 구조를 침식했습니다.";

    [Header("Settlement Communication")]
    [SerializeField] private string openingConversation = "TUTORIAL_OperatorOpening";
    [SerializeField, Min(0f)] private float rescueSignalDelay = 0.7f;

    [Header("Manual Emergency Return")]
    [SerializeField] private ReinforcementDefinition emergencyReturnDefinition;
    [SerializeField] private ReinforcementPickup reinforcementPickupPrefab;
    [SerializeField, Min(0.1f)] private float pickupSpawnClearance = 0.7f;

    private readonly List<GameObject> spawnedDecorations = new List<GameObject>(8);
    private readonly List<EnemyHealth> activeCombatEnemies = new List<EnemyHealth>(4);
    private readonly Collider2D[] placementBuffer = new Collider2D[24];

    private bool isCompleting;
    private bool tutorialRunInitialized;
    private bool eventsSubscribed;
    private bool normalHarvestDestroyed;
    private bool highValueDestroyed;
    private bool normalSalvagePositioned;
    private bool unknownMissionPresented;
    private bool combatSpawned;
    private bool openingConversationStarted;
    private bool openingConversationCompleted;
    private bool weaponGuidanceCompleted;
    private bool radarGuidanceCompleted;
    private bool harvestVisibilityGuidanceCompleted;
    private bool cargoGuidanceCompleted;
    private bool activePickupGuidanceCompleted;
    private bool activeUseGuidanceCompleted;
    private bool highValueAutoRegistered;
    private bool signalDeviceAutoRegistered;
    private bool alienSignalApproachPulsePlayed;
    private bool emergencyReturnPickupSpawned;
    private bool warnedMissingProductionMenu;
    private bool awaitingRescueConversationEnd;
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
    private Vector2 lastMovementSamplePosition;
    private float accumulatedMovementDistance;
    private Sequence alienSignalTween;
    private Tween dialogueGuideHandoffTween;
    private GameplayPauseManager transformationPauseManager;
    private bool ownsTransformationScreenFade;
    private DialogueSystemController rescueDialogueController;
    private TutorialStep activeOperatorGuidanceStep = TutorialStep.Complete;
    private Vector3 alienSignalVisualRestScale = Vector3.one;
    private Vector3 alienSignalPulseRestScale = Vector3.one;
    private Color alienSignalCoreRestColor = Color.white;
    private Color alienSignalPulseRestColor = Color.white;

    public TutorialStep CurrentStep => currentStep;
    public bool IsCompleting => isCompleting;
    public Transform PlayerRoot => playerRoot;
    public GameObject HarvestTarget => harvestTarget;
    public GameObject RadarTarget => radarTarget;
    public GameObject InteractionTarget => interactionTarget;
    public GameObject AlienSignal => alienSignal;

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
        ClearTutorialHealthFloor();
        StopAllStepRoutines();
        StopRescueSequence();
        StopCurseTransformation();
        ClearPurpleCoreRadarReveal();
        promptUI?.Hide();
        expeditionHUD?.HideObjectiveBriefing();
        expeditionMapPanel?.ClearExternalObjective();
        expeditionMapPanel?.ClearExternalSearchRegion();
        UnsubscribeGameplayEvents();
        UnsubscribeSpawnedCombatEnemies();
    }

    private void OnDestroy()
    {
        ClearTutorialHealthFloor();
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

        if (nextStep == TutorialStep.Move && playerRoot != null)
        {
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
        openingConversationCompleted = false;
        weaponGuidanceCompleted = false;
        radarGuidanceCompleted = false;
        harvestVisibilityGuidanceCompleted = false;
        cargoGuidanceCompleted = false;
        activePickupGuidanceCompleted = false;
        activeUseGuidanceCompleted = false;
        highValueAutoRegistered = false;
        signalDeviceAutoRegistered = false;
        activeOperatorGuidanceStep = TutorialStep.Complete;
        emergencyReturnPickupSpawned = false;
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
        alienSignalTargetComponent ??= alienSignal != null
            ? alienSignal.GetComponentInChildren<TutorialInteractionTarget>(true)
            : null;
        alienSignalRadarTarget ??= alienSignal != null
            ? alienSignal.GetComponentInChildren<RadarTarget>(true)
            : null;
        expeditionMenuController ??= FindFirstObjectByType<ExpeditionMenuController>(FindObjectsInactive.Include);
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        expeditionMapPanel ??= FindFirstObjectByType<ExpeditionMapPanelUI>(FindObjectsInactive.Include);
        routePlanner ??= FindFirstObjectByType<ExpeditionRoutePlanner>(FindObjectsInactive.Include);
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
    }

    private void UnsubscribeGameplayEvents()
    {
        if (playerDash != null)
        {
            playerDash.DashEnded -= HandleDashEnded;
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
        if (!ReferenceEquals(target, harvestTargetHealth) || currentStep != TutorialStep.DestroyNormalSalvage)
        {
            return;
        }

        normalHarvestDestroyed = true;
        TryAdvanceCheckpoint(TutorialStep.DestroyNormalSalvage);
    }

    private void HandleCargoChanged(int currentLoad, int maxCapacity)
    {
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
            TryAdvanceCheckpoint(TutorialStep.RoutePing);
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

            radarScanner?.CloseRadar();

            TryAdvanceCheckpoint(currentStep);
            return;
        }
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
            TryAdvanceCheckpoint(TutorialStep.InteractSignalDevice);
            return;
        }

        if (currentStep != TutorialStep.InteractPurpleCore ||
            alienSignalTargetComponent == null ||
            targetComponent != alienSignalTargetComponent)
        {
            return;
        }

        if (!ValidatePixelCurseDefinition(true))
        {
            alienSignalTargetComponent.ResetTarget();
            alienSignalTargetComponent.SetInteractionEnabled(true);
            return;
        }

        alienSignalTargetComponent.SetInteractionEnabled(false);
        TryAdvanceCheckpoint(TutorialStep.InteractPurpleCore);
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
                currentStep == TutorialStep.DestroyNormalSalvage,
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

        bool relayVisible = currentStep >= TutorialStep.FindSignalDevice &&
                            currentStep <= TutorialStep.UnknownMission;

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
            radarScanTarget.SetShowOnMap(relayVisible);
        }

        interactionTargetComponent?.SetInteractionEnabled(currentStep == TutorialStep.InteractSignalDevice);

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
                              currentStep >= TutorialStep.TravelSearchArea &&
                              currentStep <= TutorialStep.CurseTransformation;

        if (alienSignal != null)
        {
            alienSignal.SetActive(showPurpleCore);
        }

        if (alienSignalVisualRoot != null)
        {
            bool showAlienVisual = showPurpleCore && currentStep >= TutorialStep.TravelPurpleCore;
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
            if (currentStep == TutorialStep.InteractPurpleCore && !curseOwned)
            {
                alienSignalTargetComponent.ResetTarget();
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

        if (openingConversationCompleted)
        {
            TryAdvanceCheckpoint(TutorialStep.IntroCommunication);
            yield break;
        }

        TryStartOperatorConversation(TutorialStep.IntroCommunication, 1);
    }

    private void RefreshOperatorGuidance()
    {
        if (currentStep == TutorialStep.AimAndFire && !weaponGuidanceCompleted)
        {
            TryStartOperatorConversation(TutorialStep.AimAndFire, 2);
        }
        else if (currentStep == TutorialStep.RadarDiscoverSalvage && !radarGuidanceCompleted)
        {
            TryStartOperatorConversation(TutorialStep.RadarDiscoverSalvage, 3);
        }
        else if (currentStep == TutorialStep.DestroyNormalSalvage &&
                 !harvestVisibilityGuidanceCompleted &&
                 harvestTarget != null &&
                 IsInsideCameraView(harvestTarget.transform.position, 0f))
        {
            TryStartOperatorConversation(TutorialStep.DestroyNormalSalvage, 7);
        }
        else if (currentStep == TutorialStep.EquipDefensiveActive && !activePickupGuidanceCompleted)
        {
            TryStartOperatorConversation(TutorialStep.EquipDefensiveActive, 5);
        }
        else if (currentStep == TutorialStep.Combat && !activeUseGuidanceCompleted)
        {
            TryStartOperatorConversation(TutorialStep.Combat, 6);
        }
        else if (currentStep == TutorialStep.Combat)
        {
            SpawnGuaranteedCombatGroup();
        }
    }

    private void TryStartOperatorConversation(TutorialStep guidanceStep, int entryId)
    {
        if (openingConversationStarted || currentStep != guidanceStep)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(openingConversation) ||
            !DialogueManager.hasInstance ||
            DialogueManager.MasterDatabase == null ||
            DialogueManager.MasterDatabase.GetConversation(openingConversation) == null)
        {
            Debug.LogError(
                $"Tutorial operator conversation '{openingConversation}' is unavailable; continuing through the gameplay objective fallback.",
                this
            );
            CompleteOperatorGuidance(guidanceStep);
            return;
        }

        openingConversationStarted = true;
        activeOperatorGuidanceStep = guidanceStep;
        rescueDialogueController = DialogueManager.instance;
        rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
        rescueDialogueController.conversationEnded += HandleOpeningConversationEnded;
        DialogueManager.StartConversation(openingConversation, playerRoot, null, entryId);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(DialogueManager.lastConversationStarted, openingConversation, StringComparison.Ordinal))
        {
            rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
            openingConversationStarted = false;
            activeOperatorGuidanceStep = TutorialStep.Complete;
            Debug.LogError($"Tutorial opening conversation '{openingConversation}' failed to start.", this);
            CompleteOperatorGuidance(guidanceStep);
        }
    }

    private void HandleOpeningConversationEnded(Transform actor)
    {
        if (!openingConversationStarted ||
            !string.Equals(DialogueManager.lastConversationEnded, openingConversation, StringComparison.Ordinal))
        {
            return;
        }

        if (rescueDialogueController != null)
        {
            rescueDialogueController.conversationEnded -= HandleOpeningConversationEnded;
        }

        openingConversationStarted = false;
        TutorialStep completedGuidance = activeOperatorGuidanceStep;
        activeOperatorGuidanceStep = TutorialStep.Complete;

        CompleteOperatorGuidance(completedGuidance);
    }

    private void CompleteOperatorGuidance(TutorialStep completedGuidance)
    {
        if (completedGuidance == TutorialStep.IntroCommunication)
        {
            openingConversationCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, true, false);
        }
        else if (completedGuidance == TutorialStep.AimAndFire)
        {
            weaponGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, false, false);
        }
        else if (completedGuidance == TutorialStep.RadarDiscoverSalvage)
        {
            radarGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, false, false);
        }
        else if (completedGuidance == TutorialStep.DestroyNormalSalvage)
        {
            harvestVisibilityGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, false, false);
        }
        else if (completedGuidance == TutorialStep.Cargo)
        {
            cargoGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, true, false);
        }
        else if (completedGuidance == TutorialStep.EquipDefensiveActive)
        {
            activePickupGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, false, false);
        }
        else if (completedGuidance == TutorialStep.Combat)
        {
            activeUseGuidanceCompleted = true;
            ScheduleDialogueGuideHandoff(completedGuidance, false, true);
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

        if (cargoGuidanceCompleted)
        {
            TryAdvanceCheckpoint(TutorialStep.Cargo);
            yield break;
        }

        TryStartOperatorConversation(TutorialStep.Cargo, 4);
    }

    private void RefreshObjectiveChecks()
    {
        bool needsCheck = currentStep == TutorialStep.TravelNormalSalvage ||
                          currentStep == TutorialStep.DestroyNormalSalvage ||
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

            if (currentStep == TutorialStep.TravelNormalSalvage && harvestTarget != null)
            {
                float distance = Vector2.Distance(playerRoot.position, harvestTarget.transform.position);

                if (IsInsideCameraView(harvestTarget.transform.position, 0f) ||
                    distance <= Mathf.Max(1.5f, highValueDiscoveryDistance))
                {
                    objectiveCheckRoutine = null;
                    TryAdvanceCheckpoint(TutorialStep.TravelNormalSalvage);
                    yield break;
                }
            }
            else if (currentStep == TutorialStep.DestroyNormalSalvage &&
                     !harvestVisibilityGuidanceCompleted &&
                     harvestTarget != null &&
                     IsInsideCameraView(harvestTarget.transform.position, 0f))
            {
                objectiveCheckRoutine = null;
                RefreshOperatorGuidance();
                yield break;
            }
            else if (currentStep == TutorialStep.TravelHighValue && highValueSalvageInstance != null)
            {
                float distance = Vector2.Distance(playerRoot.position, highValueSalvageInstance.transform.position);

                if (!highValueAutoRegistered &&
                    radarScanner != null &&
                    radarScanner.RegisterNearbyTarget(
                        highValueSalvageRadarTarget,
                        Mathf.Max(highValueDiscoveryDistance, proximityAutoRegistrationDistance)))
                {
                    highValueAutoRegistered = true;
                }

                if (distance <= Mathf.Max(0.5f, highValueDiscoveryDistance))
                {
                    objectiveCheckRoutine = null;
                    TryAdvanceCheckpoint(TutorialStep.TravelHighValue);
                    yield break;
                }
            }
            else if (currentStep == TutorialStep.FindSignalDevice && radarScanTarget != null)
            {
                if (!signalDeviceAutoRegistered &&
                    radarScanner != null &&
                    radarScanner.RegisterNearbyTarget(
                        radarScanTarget,
                        Mathf.Max(0.5f, proximityAutoRegistrationDistance)))
                {
                    signalDeviceAutoRegistered = true;
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
                        RevealPurpleCoreOnProductionNavigation();
                        TryAdvanceCheckpoint(TutorialStep.RevealPurpleCore);
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
        if (currentStep != TutorialStep.RevealPurpleCore || alienSignalRadarTarget == null)
        {
            return;
        }

        alienSignalRadarTarget.SetVisible(false);
        alienSignalRadarTarget.SetShowOnMap(false);
        alienSignalRadarTarget.SetMapDiscovered(false);
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
            AudioManager.Play(SoundEventIds.MissionReceived);
            expeditionMapPanel?.SetExternalSearchRegion(
                ResolveUnknownSearchAreaPosition(),
                Mathf.Max(1f, unknownSearchAreaRadius)
            );
        }

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

    private void ClearPurpleCoreRadarReveal()
    {
        alienSignalRadarTarget?.ClearTemporaryReveal(this);
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
        if (!ValidatePixelCurseDefinition(true))
        {
            curseTransformationRoutine = null;
            yield break;
        }

        transformationPauseManager = GameplayPauseManager.Instance;
        transformationPauseManager?.PushPause(this, "Tutorial Pixel Curse transformation");
        PlayAlienSignalTransformation();

        float peakDelay = Mathf.Max(0.1f, corruptionPeakDelay);
        float darkenLead = Mathf.Min(0.2f, peakDelay * 0.35f);
        if (darkenLead > 0f)
        {
            yield return new WaitForSecondsRealtime(darkenLead);
        }

        ScreenFader screenFader = ScreenFader.Instance;
        float darkenDuration = Mathf.Max(0.01f, corruptionDarkenDuration);
        if (screenFader != null)
        {
            ownsTransformationScreenFade = true;
            yield return screenFader.FadeIn(this, darkenDuration);
        }

        float remainingPeakDelay = peakDelay - darkenLead - (screenFader != null ? darkenDuration : 0f);
        if (remainingPeakDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingPeakDelay);
        }

        bool acquired = RunTraitAcquisitionService.TryAcquirePersistentStoryTrait(
            pixelCurseDefinition,
            playerRoot != null ? playerRoot.gameObject : null
        );

        if (!acquired && !HasPersistentPixelCurse())
        {
            Debug.LogError("Pixel Curse acquisition failed; Tutorial remains at CurseTransformation.", this);
            ClearTransformationScreenFade();
            ReleaseTransformationPause();
            RestoreAlienSignalVisualState();
            curseTransformationRoutine = null;
            yield break;
        }

        playerVisualStateController?.RefreshVisualState();
        playerVisualStateController?.PlayCurseAcquiredGlitch();
        statusEffectPresenter?.SetExternalVisible(true);
        statusEffectPresenter?.RefreshStatuses();

        if (screenFader != null && ownsTransformationScreenFade)
        {
            yield return screenFader.FadeOut(this, Mathf.Max(0.01f, corruptionRecoveryDuration));
            ownsTransformationScreenFade = false;
        }

        expeditionHUD?.ShowObjectiveBriefing(
            "시스템 오류",
            curseAcquiredFeedback,
            alienSignalPeakColor
        );

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, acquisitionFeedbackDuration));

        curseTransformationRoutine = null;
        alienSignalTween?.Kill();
        alienSignalTween = null;
        RestoreAlienSignalVisualState();
        ReleaseTransformationPause();
        ClearPurpleCoreRadarReveal();
        SetStep(TutorialStep.SettlementCommunication);
    }

    private void CacheAlienSignalVisualState()
    {
        if (alienSignalVisualRoot != null)
        {
            alienSignalVisualRestScale = alienSignalVisualRoot.localScale;
        }

        if (alienSignalPulseRenderer != null)
        {
            alienSignalPulseRestScale = alienSignalPulseRenderer.transform.localScale;
            alienSignalPulseRestColor = alienSignalPulseRenderer.color;
        }

        if (alienSignalCoreRenderer != null)
        {
            alienSignalCoreRestColor = alienSignalCoreRenderer.color;
        }
    }

    private void PlayAlienSignalTransformation()
    {
        alienSignalTween?.Kill();
        RestoreAlienSignalVisualState();

        if (alienSignal != null)
        {
            AudioManager.PlayAt(SoundEventIds.CoreActivate, alienSignal.transform.position, 0.9f);
        }

        if (corruptionShakeAmplitude > 0f && corruptionShakeDuration > 0f)
        {
            GungeonStyleCamera2D.RequestShake(corruptionShakeAmplitude, corruptionShakeDuration);
        }

        float duration = Mathf.Max(0.1f, corruptionPeakDelay);
        alienSignalTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        if (alienSignalVisualRoot != null)
        {
            alienSignalTween.Join(
                alienSignalVisualRoot.DOScale(alienSignalVisualRestScale * 1.2f, duration)
                    .SetEase(Ease.InOutSine)
            );
        }

        if (alienSignalCoreRenderer != null)
        {
            alienSignalTween.Join(alienSignalCoreRenderer.DOColor(alienSignalPeakColor, duration));
        }

        if (alienSignalPulseRenderer != null)
        {
            Color transparentPeak = alienSignalPeakColor;
            transparentPeak.a = 0f;
            alienSignalTween.Join(
                alienSignalPulseRenderer.transform.DOScale(alienSignalPulseRestScale * 3.2f, duration)
                    .SetEase(Ease.OutQuad)
            );
            alienSignalTween.Join(alienSignalPulseRenderer.DOColor(transparentPeak, duration));
        }
    }

    private void PlayAlienSignalApproachPulse()
    {
        if (alienSignalPulseRenderer == null)
        {
            return;
        }

        alienSignalTween?.Kill();
        RestoreAlienSignalVisualState();

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
        });
    }

    private void StopCurseTransformation()
    {
        if (curseTransformationRoutine != null)
        {
            StopCoroutine(curseTransformationRoutine);
            curseTransformationRoutine = null;
        }

        alienSignalTween?.Kill();
        alienSignalTween = null;
        RestoreAlienSignalVisualState();
        ClearTransformationScreenFade();
        ReleaseTransformationPause();
    }

    private void RestoreAlienSignalVisualState()
    {
        if (alienSignalVisualRoot != null)
        {
            alienSignalVisualRoot.localScale = alienSignalVisualRestScale;
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
            TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
            return;
        }

        if (!DialogueManager.hasInstance || DialogueManager.instance == null)
        {
            Debug.LogError("Persistent Boot-owned Dialogue Manager is unavailable.", this);
            TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
            return;
        }

        DialogueDatabase database = DialogueManager.MasterDatabase;

        if (database == null || database.GetConversation(trigger.conversation) == null)
        {
            Debug.LogError(
                $"Tutorial conversation '{trigger.conversation}' is missing from the active Dialogue Database.",
                this
            );
            TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
            return;
        }

        rescueDialogueController = DialogueManager.instance;
        rescueDialogueController.conversationEnded -= HandleRescueConversationEnded;
        rescueDialogueController.conversationEnded += HandleRescueConversationEnded;
        awaitingRescueConversationEnd = true;

        FindFirstObjectByType<GameplayPauseMenuController>(FindObjectsInactive.Include)?.Close();
        trigger.TryStart(playerRoot);

        if (!DialogueManager.isConversationActive ||
            !string.Equals(DialogueManager.lastConversationStarted, trigger.conversation, StringComparison.Ordinal))
        {
            StopWaitingForRescueConversation();
            Debug.LogError($"Tutorial conversation '{trigger.conversation}' failed to start.", this);
            TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
            return;
        }

        promptUI?.Hide();
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

        StopWaitingForRescueConversation();
        TryAdvanceCheckpoint(TutorialStep.SettlementCommunication);
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
        progress.AddUnlockFlag(FirstSettlementPendingFlag);
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

        if ((previousStep == TutorialStep.TravelNormalSalvage ||
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

        if (previousStep == TutorialStep.UnknownMission && unknownMissionRoutine != null)
        {
            StopCoroutine(unknownMissionRoutine);
            unknownMissionRoutine = null;
        }
    }

    private void StopAllStepRoutines()
    {
        KillDialogueGuideHandoff();

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
        return step == TutorialStep.AimAndFire && !weaponGuidanceCompleted ||
               step == TutorialStep.RadarDiscoverSalvage && !radarGuidanceCompleted ||
               step == TutorialStep.DestroyNormalSalvage && !harvestVisibilityGuidanceCompleted ||
               step == TutorialStep.EquipDefensiveActive && !activePickupGuidanceCompleted ||
               step == TutorialStep.Combat && !activeUseGuidanceCompleted;
    }

    private static string ResolveObjectiveTitle(TutorialStep step)
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
            TutorialStep.UnknownMission => "???",
            TutorialStep.TravelSearchArea => "미확인 지역 조사",
            TutorialStep.TravelPurpleCore => "미확인 코어 조사",
            TutorialStep.AcquireEmergencyReturn => "긴급복귀 장비 획득",
            TutorialStep.EmergencyReturn => "정착지로 복귀",
            _ => "튜토리얼"
        };
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
