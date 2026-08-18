using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialFlowController : MonoBehaviour
{
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
    [SerializeField] private TutorialStep currentStep = TutorialStep.Intro;
    [SerializeField] private StepPresentation[] stepPresentations = new StepPresentation[0];
    [SerializeField, Min(0f)] private float introAdvanceDelay = 0.75f;

    [Header("Scene References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private TutorialPromptUI promptUI;

    [Header("Gameplay Event Sources")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerRadarScanner radarScanner;
    [SerializeField] private PlayerRadarVFXController radarVFX;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameObject radarUiRoot;

    [Header("Authored Tutorial Targets")]
    [SerializeField] private GameObject harvestTarget;
    [SerializeField] private HarvestObjectHealth harvestTargetHealth;
    [SerializeField] private GameObject dashHazard;
    [SerializeField] private GameObject radarTarget;
    [SerializeField] private RadarTarget radarScanTarget;
    [SerializeField] private GameObject interactionTarget;
    [SerializeField] private TutorialInteractionTarget interactionTargetComponent;
    [SerializeField] private GameObject alienSignal;
    [SerializeField] private TutorialInteractionTarget alienSignalTargetComponent;
    [SerializeField] private Transform alienSignalVisualRoot;
    [SerializeField] private SpriteRenderer alienSignalCoreRenderer;
    [SerializeField] private SpriteRenderer alienSignalPulseRenderer;
    [SerializeField] private GameObject rescueExit;

    [Header("Pixel Curse Transformation")]
    [SerializeField] private TraitDefinition pixelCurseDefinition;
    [SerializeField] private PlayerVisualStateController playerVisualStateController;
    [SerializeField] private StatusEffectHUDPresenter statusEffectPresenter;
    [SerializeField, Min(0.1f)] private float corruptionPeakDelay = 0.85f;
    [SerializeField, Min(0.1f)] private float acquisitionFeedbackDuration = 0.85f;
    [SerializeField] private Color alienSignalPeakColor = new Color(0.75f, 0.2f, 1f, 1f);
    [SerializeField, TextArea(1, 2)] private string curseAcquiredFeedback = "픽셀화 저주가 기체에 침식되었습니다.";

    [Header("Rescue Handoff")]
    [SerializeField, Min(0f)] private float rescueSignalDelay = 1f;
    [SerializeField, Min(0f)] private float completeDisplayDuration = 0.75f;

    private bool isCompleting;
    private bool eventsSubscribed;
    private bool awaitingRescueConversationEnd;
    private PlayerWeaponBase observedWeapon;
    private Coroutine introAdvanceRoutine;
    private Coroutine curseTransformationRoutine;
    private Coroutine rescueSequenceRoutine;
    private Coroutine completionRoutine;
    private Sequence alienSignalTween;
    private GameplayPauseManager transformationPauseManager;
    private DialogueSystemController rescueDialogueController;
    private Vector3 alienSignalVisualRestScale = Vector3.one;
    private Vector3 alienSignalPulseRestScale = Vector3.one;
    private Color alienSignalCoreRestColor = Color.white;
    private Color alienSignalPulseRestColor = Color.white;

    public TutorialStep CurrentStep => currentStep;
    public bool IsCompleting => isCompleting;
    public Transform PlayerRoot => playerRoot;
    public GameObject HarvestTarget => harvestTarget;
    public GameObject DashHazard => dashHazard;
    public GameObject RadarTarget => radarTarget;
    public GameObject InteractionTarget => interactionTarget;
    public GameObject AlienSignal => alienSignal;
    public GameObject RescueExit => rescueExit;

    public event Action<TutorialStep, TutorialStep> StepChanged;

    private void Awake()
    {
        CacheRuntimeReferences();
        CacheAlienSignalVisualState();
    }

    private void OnEnable()
    {
        CacheRuntimeReferences();
        SubscribeGameplayEvents();
    }

    private void OnDisable()
    {
        StopIntroTransition();
        StopRescueSequence();
        StopCurseTransformation();

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        UnsubscribeGameplayEvents();
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Tutorial);
        }

        ResumeFromPersistentStoryCheckpoint();
        PlacePlayerAtSpawn();
        ApplyCurrentStepState();
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

    [ContextMenu("Tutorial/Complete Tutorial")]
    public void CompleteTutorial()
    {
        if (isCompleting)
        {
            return;
        }

        if (DialogueManager.isConversationActive)
        {
            Debug.LogWarning("Tutorial completion was deferred because a Dialogue System conversation is still active.", this);
            return;
        }

        if (currentStep != TutorialStep.Complete)
        {
            TutorialStep previousStep = currentStep;
            currentStep = TutorialStep.Complete;
            ApplyCurrentStepState();
            StepChanged?.Invoke(previousStep, currentStep);
        }

        BeginTutorialCompletion();
    }

    private void BeginTutorialCompletion()
    {
        if (isCompleting || currentStep != TutorialStep.Complete)
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saveManager = SaveManager.Instance;
        SceneFlowManager sceneFlowManager = SceneFlowManager.Instance;

        if (progress == null || saveManager == null || sceneFlowManager == null)
        {
            Debug.LogError(
                "Tutorial cannot complete because progression, save, or scene-flow services are missing.",
                this
            );
            return;
        }

        isCompleting = true;
        progress.TryMarkTutorialCompleted();

        SaveData previousSave = saveManager.CurrentSaveData;
        saveManager.Save(progress);

        // SaveManager updates CurrentSaveData only after its atomic write and
        // validation have succeeded. Do not leave Tutorial on a failed write.
        if (ReferenceEquals(previousSave, saveManager.CurrentSaveData))
        {
            Debug.LogError(
                "Tutorial completion could not be saved. Remaining in Tutorial so the save can be retried.",
                this
            );
            isCompleting = false;
            return;
        }

        completionRoutine = StartCoroutine(CompleteAndLoadSettlementRoutine(sceneFlowManager));
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

        if (currentStep == TutorialStep.Intro)
        {
            ApplyCurrentStepState();
        }
        else
        {
            SetStep(TutorialStep.Intro);
        }
#else
        Debug.LogWarning("Tutorial checkpoint controls are only available in the Editor or Development Builds.", this);
#endif
    }

    private void PlacePlayerAtSpawn()
    {
        if (playerRoot == null || playerSpawn == null)
        {
            return;
        }

        playerRoot.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);
    }

    private void CacheRuntimeReferences()
    {
        if (playerRoot != null)
        {
            playerController ??= playerRoot.GetComponent<PlayerController2D>();
            weaponController ??= playerRoot.GetComponent<PlayerWeaponController>();
            playerDash ??= playerRoot.GetComponent<PlayerDash>();
            radarScanner ??= playerRoot.GetComponent<PlayerRadarScanner>();
            radarVFX ??= playerRoot.GetComponent<PlayerRadarVFXController>();
            playerInteractor ??= playerRoot.GetComponent<PlayerInteractor>();
        }

        if (harvestTargetHealth == null && harvestTarget != null)
        {
            harvestTargetHealth = harvestTarget.GetComponentInChildren<HarvestObjectHealth>(true);
        }

        if (radarScanTarget == null && radarTarget != null)
        {
            radarScanTarget = radarTarget.GetComponentInChildren<RadarTarget>(true);
        }

        if (interactionTargetComponent == null && interactionTarget != null)
        {
            interactionTargetComponent = interactionTarget.GetComponentInChildren<TutorialInteractionTarget>(true);
        }

        if (alienSignalTargetComponent == null && alienSignal != null)
        {
            alienSignalTargetComponent = alienSignal.GetComponentInChildren<TutorialInteractionTarget>(true);
        }

        if (playerVisualStateController == null && playerRoot != null)
        {
            playerVisualStateController = playerRoot.GetComponentInChildren<PlayerVisualStateController>(true);
        }
    }

    private void SubscribeGameplayEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.MovementStarted += HandleMovementStarted;
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

        if (playerDash != null)
        {
            playerDash.DashStarted += HandleDashStarted;
        }

        if (radarScanner != null)
        {
            radarScanner.ScanCompleted += HandleRadarScanCompleted;
        }

        if (playerInteractor != null)
        {
            playerInteractor.Interacted += HandlePlayerInteracted;
        }

        eventsSubscribed = true;
    }

    private void UnsubscribeGameplayEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.MovementStarted -= HandleMovementStarted;
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

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
        }

        if (radarScanner != null)
        {
            radarScanner.ScanCompleted -= HandleRadarScanCompleted;
        }

        if (playerInteractor != null)
        {
            playerInteractor.Interacted -= HandlePlayerInteracted;
        }

        eventsSubscribed = false;
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

    private void HandleMovementStarted()
    {
        TryAdvanceCheckpoint(TutorialStep.Move);
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTree, PlayerWeaponBase weapon)
    {
        BindObservedWeapon(weapon);
    }

    private void HandleWeaponFired(PlayerWeaponBase weapon, float powerRatio)
    {
        if (weaponController == null ||
            weaponController.CurrentWeaponTree != WeaponTreeType.MachineGun ||
            !ReferenceEquals(weaponController.CurrentWeapon, weapon))
        {
            return;
        }

        TryAdvanceCheckpoint(TutorialStep.AimAndFire);
    }

    private void HandleHarvestTargetDied(HarvestObjectHealth target)
    {
        if (ReferenceEquals(target, harvestTargetHealth))
        {
            TryAdvanceCheckpoint(TutorialStep.Harvest);
        }
    }

    private void HandleDashStarted(Vector2 direction)
    {
        TryAdvanceCheckpoint(TutorialStep.Dash);
    }

    private void HandleRadarScanCompleted(
        Vector2 origin,
        float radius,
        IReadOnlyList<RadarTarget> scannedTargets)
    {
        if (currentStep != TutorialStep.Radar || radarScanTarget == null || scannedTargets == null)
        {
            return;
        }

        for (int i = 0; i < scannedTargets.Count; i++)
        {
            if (ReferenceEquals(scannedTargets[i], radarScanTarget))
            {
                TryAdvanceCheckpoint(TutorialStep.Radar);
                return;
            }
        }
    }

    private void HandlePlayerInteracted(IInteractable target)
    {
        if (target is not Component targetComponent)
        {
            return;
        }

        if (currentStep == TutorialStep.Interact &&
            interactionTargetComponent != null &&
            targetComponent == interactionTargetComponent)
        {
            TryAdvanceCheckpoint(TutorialStep.Interact);
            return;
        }

        if (currentStep != TutorialStep.AlienSignal ||
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
        TryAdvanceCheckpoint(TutorialStep.AlienSignal);
    }

    private void ApplyCurrentStepState()
    {
        ApplyCurrentStepPresentation();
        ApplyCurrentStepWorldState();
        RestartIntroTransition();
        RefreshRescueSequence();
    }

    private void ApplyCurrentStepWorldState()
    {
        if (harvestTarget != null)
        {
            harvestTarget.SetActive(currentStep == TutorialStep.Harvest);
        }

        bool radarIntroduced = currentStep >= TutorialStep.Radar && currentStep <= TutorialStep.AlienSignal;

        if (!radarIntroduced)
        {
            interactionTargetComponent?.ResetTarget();
            radarScanTarget?.SetMapDiscovered(false);
        }

        if (radarTarget != null)
        {
            radarTarget.SetActive(radarIntroduced);
        }

        if (interactionTarget != null && interactionTarget != radarTarget)
        {
            interactionTarget.SetActive(radarIntroduced);
        }

        interactionTargetComponent?.SetInteractionEnabled(currentStep == TutorialStep.Interact);

        if (radarScanner != null)
        {
            // Keep the RadarPanelAnimator owner active. Closing before the
            // scanner is disabled prevents OnDisable from trying to animate an
            // inactive panel during later Tutorial transitions.
            if (!radarIntroduced && radarScanner.enabled)
            {
                radarScanner.CloseRadar();
            }

            radarScanner.enabled = radarIntroduced;
        }

        if (radarUiRoot != null && !radarUiRoot.activeSelf)
        {
            radarUiRoot.SetActive(true);
        }

        if (radarVFX != null)
        {
            radarVFX.enabled = radarIntroduced;
        }

        bool curseOwned = HasPersistentPixelCurse();
        bool alienSignalActive = !curseOwned &&
                                 (currentStep == TutorialStep.AlienSignal ||
                                  currentStep == TutorialStep.CurseTransformation);

        if (alienSignal != null)
        {
            alienSignal.SetActive(alienSignalActive);
        }

        if (alienSignalTargetComponent != null)
        {
            if (currentStep == TutorialStep.AlienSignal && !curseOwned)
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

        if (currentStep == TutorialStep.CurseTransformation && !curseOwned)
        {
            StartCurseTransformation();
        }
    }

    private void RestartIntroTransition()
    {
        StopIntroTransition();

        if (isActiveAndEnabled && !isCompleting && currentStep == TutorialStep.Intro)
        {
            introAdvanceRoutine = StartCoroutine(IntroAdvanceRoutine());
        }
    }

    private IEnumerator IntroAdvanceRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, introAdvanceDelay));
        introAdvanceRoutine = null;
        TryAdvanceCheckpoint(TutorialStep.Intro);
    }

    private void StopIntroTransition()
    {
        if (introAdvanceRoutine == null)
        {
            return;
        }

        StopCoroutine(introAdvanceRoutine);
        introAdvanceRoutine = null;
    }

    private void ResumeFromPersistentStoryCheckpoint()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress != null &&
            !progress.IsTutorialCompleted &&
            ValidatePixelCurseDefinition(false) &&
            progress.HasPersistentStoryTrait(pixelCurseDefinition))
        {
            currentStep = TutorialStep.Rescue;
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
        transformationPauseManager.PushPause(this, "Tutorial Pixel Curse transformation");
        PlayAlienSignalTransformation();

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, corruptionPeakDelay));

        bool acquired = RunTraitAcquisitionService.TryAcquirePersistentStoryTrait(
            pixelCurseDefinition,
            playerRoot != null ? playerRoot.gameObject : null
        );

        if (!acquired && !HasPersistentPixelCurse())
        {
            Debug.LogError("Pixel Curse acquisition failed; the Tutorial remains at CurseTransformation.", this);
            ReleaseTransformationPause();
            RestoreAlienSignalVisualState();
            curseTransformationRoutine = null;
            yield break;
        }

        playerVisualStateController?.RefreshVisualState();
        playerVisualStateController?.PlayCurseAcquiredGlitch();
        statusEffectPresenter?.SetExternalVisible(true);
        statusEffectPresenter?.RefreshStatuses();

        if (promptUI != null)
        {
            promptUI.ShowInstruction(curseAcquiredFeedback, string.Empty, string.Empty, string.Empty, false);
            promptUI.SetProgress((int)TutorialStep.CurseTransformation + 1, (int)TutorialStep.Complete + 1);
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, acquisitionFeedbackDuration));

        curseTransformationRoutine = null;
        alienSignalTween?.Kill();
        alienSignalTween = null;
        RestoreAlienSignalVisualState();
        ReleaseTransformationPause();
        SetStep(TutorialStep.Rescue);
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
        alienSignalTween = null;
        RestoreAlienSignalVisualState();

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
                alienSignalPulseRenderer.transform.DOScale(alienSignalPulseRestScale * 2.4f, duration)
                    .SetEase(Ease.OutQuad)
            );
            alienSignalTween.Join(alienSignalPulseRenderer.DOColor(transparentPeak, duration));
        }
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
        if (transformationPauseManager == null)
        {
            return;
        }

        transformationPauseManager.PopPause(this);
        transformationPauseManager = null;
    }

    private void RefreshRescueSequence()
    {
        if (currentStep != TutorialStep.Rescue)
        {
            StopRescueSequence();
            return;
        }

        if (!isActiveAndEnabled ||
            isCompleting ||
            awaitingRescueConversationEnd ||
            rescueSequenceRoutine != null)
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
        StepPresentation rescuePresentation = FindPresentation(TutorialStep.Rescue);
        DialogueSystemTrigger trigger = rescuePresentation != null
            ? rescuePresentation.dialogueTrigger
            : null;

        if (trigger == null || string.IsNullOrWhiteSpace(trigger.conversation))
        {
            Debug.LogError(
                "Tutorial Rescue cannot start because its production DialogueSystemTrigger is not assigned or has no conversation.",
                this
            );
            return;
        }

        if (!DialogueManager.hasInstance || DialogueManager.instance == null)
        {
            Debug.LogError(
                "Tutorial Rescue cannot start because the persistent Dialogue Manager is unavailable.",
                this
            );
            return;
        }

        DialogueDatabase database = DialogueManager.MasterDatabase;

        if (database == null || database.GetConversation(trigger.conversation) == null)
        {
            Debug.LogError(
                $"Tutorial Rescue conversation '{trigger.conversation}' is missing from the active Dialogue Database.",
                this
            );
            return;
        }

        rescueDialogueController = DialogueManager.instance;
        rescueDialogueController.conversationEnded -= HandleRescueConversationEnded;
        rescueDialogueController.conversationEnded += HandleRescueConversationEnded;
        awaitingRescueConversationEnd = true;

        GameplayPauseMenuController pauseMenu = FindFirstObjectByType<GameplayPauseMenuController>(
            FindObjectsInactive.Include
        );
        pauseMenu?.Close();

        trigger.TryStart(playerRoot);

        // DialogueSystemTrigger.TryStart is void. Validate the synchronous
        // package state so a missing/blocked conversation cannot complete the
        // Tutorial through a timer.
        if (!awaitingRescueConversationEnd)
        {
            return;
        }

        if (!DialogueManager.isConversationActive ||
            !string.Equals(DialogueManager.lastConversationStarted, trigger.conversation, StringComparison.Ordinal))
        {
            StopWaitingForRescueConversation();
            Debug.LogError(
                $"Tutorial Rescue conversation '{trigger.conversation}' failed to start. Remaining at Rescue.",
                this
            );
            return;
        }

        promptUI?.Hide();
    }

    private void HandleRescueConversationEnded(Transform actor)
    {
        if (!awaitingRescueConversationEnd || currentStep != TutorialStep.Rescue)
        {
            return;
        }

        StepPresentation rescuePresentation = FindPresentation(TutorialStep.Rescue);
        string expectedConversation = rescuePresentation != null && rescuePresentation.dialogueTrigger != null
            ? rescuePresentation.dialogueTrigger.conversation
            : string.Empty;

        if (string.IsNullOrWhiteSpace(expectedConversation) ||
            !string.Equals(DialogueManager.lastConversationEnded, expectedConversation, StringComparison.Ordinal))
        {
            return;
        }

        StopWaitingForRescueConversation();

        if (!TryAdvanceCheckpoint(TutorialStep.Rescue))
        {
            return;
        }

        BeginTutorialCompletion();
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

    private IEnumerator CompleteAndLoadSettlementRoutine(SceneFlowManager sceneFlowManager)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, completeDisplayDuration));
        completionRoutine = null;

        CleanupBeforeSettlementTransition();
        sceneFlowManager.LoadSettlement();
    }

    private void CleanupBeforeSettlementTransition()
    {
        StopRescueSequence();
        StopCurseTransformation();
        radarScanner?.CloseRadar();
        promptUI?.Hide();

        GameplayPauseMenuController pauseMenu = FindFirstObjectByType<GameplayPauseMenuController>(
            FindObjectsInactive.Include
        );
        pauseMenu?.Close();
    }

    private void ApplyCurrentStepPresentation()
    {
        StepPresentation currentPresentation = null;

        if (stepPresentations == null)
        {
            promptUI?.Hide();
            return;
        }

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

        if (promptUI == null)
        {
            return;
        }

        if (currentPresentation == null || string.IsNullOrWhiteSpace(currentPresentation.instructionTemplate))
        {
            promptUI.Hide();
            return;
        }

        promptUI.ShowInstruction(
            currentPresentation.instructionTemplate,
            currentPresentation.actionMapName,
            currentPresentation.actionName,
            currentPresentation.fallbackBindingDisplay,
            currentPresentation.useCompositeBindingParts
        );
        promptUI.SetProgress((int)currentStep + 1, (int)TutorialStep.Complete + 1);
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
