using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ExpeditionHUD : MonoBehaviour
{
    private readonly HashSet<object> menuHintSuppressors = new HashSet<object>();
    [SerializeField] private TextMeshProUGUI polarityText;
    private object polarityOwner;

    public void ShowPolarity(object source, string localizationKey)
    {
        if (source == null || polarityText == null) return;
        if (polarityOwner != null && polarityOwner != source) return;
        polarityOwner = source;
        polarityText.text = VoidScrapperLocalizationService.Instance != null
            ? VoidScrapperLocalizationService.Instance.GetText(localizationKey) : localizationKey;
        polarityText.gameObject.SetActive(true);
    }

    public void HidePolarity(object source)
    {
        if (polarityOwner != source) return;
        polarityOwner = null;
        if (polarityText != null) polarityText.gameObject.SetActive(false);
    }

    [Header("Player References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private StatusEffectHUDPresenter statusEffectPresenter;

    [Header("Objective")]
    [SerializeField] private CoreTrackingSignalController coreTrackingController;
    [SerializeField] private ExpeditionObjectiveDirector objectiveDirector;
    [Tooltip("Enable this in scenes such as Tutorial that intentionally do not use Expedition objective state.")]
    [SerializeField] private bool disableObjectiveDirectorAutoResolution;
    [SerializeField] private string coreSignalCountFormat = "{0}/{1}";
    [SerializeField] private string coreSignalReadyText = "코어 좌표 확인";
    [SerializeField] private TextMeshProUGUI coreTrackingObjectiveText;
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("Operation")]
    [SerializeField] private GameObject operationRoot;
    [SerializeField] private CanvasGroup operationCanvasGroup;
    [SerializeField] private TextMeshProUGUI operationTitleText;
    [SerializeField] private TextMeshProUGUI operationDetailText;
    [SerializeField] private Image operationAccentImage;
    [SerializeField] private string moveActionName = "Move";
    [SerializeField, Min(0.05f)] private float operationBriefingIntroDuration = 0.24f;
    [SerializeField, Min(0f)] private float operationBriefingVisibleDuration = 2.4f;
    [SerializeField, Min(0.05f)] private float operationBriefingOutroDuration = 0.24f;
    [SerializeField] private float operationBriefingSlideDistance = 10f;

    [Header("Input Binding Hints")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string mapActionName = "Map";
    [SerializeField] private string inventoryActionName = "Inventory";
    [SerializeField] private string reinforcementActionName = "UseReinforcement";
    [SerializeField] private GameObject menuHintRoot;
    [SerializeField] private TextMeshProUGUI mapHintText;
    [SerializeField] private TextMeshProUGUI inventoryHintText;
    [SerializeField] private TextMeshProUGUI reinforcementKeyText;
    [SerializeField] private Sprite mapHintIcon;
    [SerializeField] private Sprite inventoryHintIcon;
    [SerializeField] private string mapHintFormat = "[{0}] 지도";
    [SerializeField] private string inventoryHintFormat = "[{0}] 인벤토리";
    [SerializeField] private string reinforcementKeyFormat = "[{0}]";

    [Header("HUD Roots")]
    [Tooltip("CanvasGroup owned by ExpeditionHUD for cinematic fades. It must be on the gameplay HUD canvas, not on an individual panel.")]
    [SerializeField] private CanvasGroup cinematicCanvasGroup;
    [SerializeField] private RadarPanelAnimator cinematicRadarPanel;
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private GameObject resourceRoot;
    [SerializeField] private GameObject cargoRoot;
    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private GameObject[] additionalObjectsToHideDuringCinematic;
    [SerializeField, Min(0f)] private float cinematicRestoreFadeDuration = 0.15f;

    [Header("HP + Armor")]
    [SerializeField] private GaugeBarUI hpGauge;
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private GameObject armorBonusRoot;
    [SerializeField] private TextMeshProUGUI armorBonusText;
    [Tooltip("HP 아래에 고정되는 얇은 Armor 스트립입니다. 폭은 CurrentArmor / MaxArmor로 표시합니다.")]
    [SerializeField] private RectTransform armorFillRect;
    [SerializeField] private Image armorFillImage;
    [SerializeField] private Image armorTrackImage;
    [SerializeField] private string hpValueFormat = "{0:0}/{1:0}";
    [SerializeField] private string armorBonusFormat = "장갑 {0:0}";
    [SerializeField] private Color armorBonusColor = Color.white;
    [SerializeField] private Color armorFillColor = Color.white;
    [SerializeField] private Color hpNormalStateColor = new Color(0.95f, 0.24f, 0.28f, 1f);
    [SerializeField] private Color hpShieldStateColor = new Color(0.25f, 0.82f, 1f, 1f);
    [SerializeField] private Color hpInvulnerableStateColor = new Color(1f, 0.78f, 0.22f, 1f);
    [FormerlySerializedAs("armorGauge")]
    [SerializeField, HideInInspector] private GaugeBarUI legacyArmorGauge;

    [Header("Core Signal Icon")]
    [FormerlySerializedAs("objectiveSignalGauge")]
    [SerializeField] private GaugeBarUI legacyObjectiveSignalGauge;
    [SerializeField] private Image coreSignalIcon;
    [SerializeField] private Image[] coreSignalPips;
    [SerializeField] private TextMeshProUGUI coreSignalCountText;
    [SerializeField] private GameObject coreSignalReadyPulseRoot;
    [SerializeField] private Color coreSignalInactiveColor = new Color(0.22f, 0.25f, 0.3f, 0.75f);
    [SerializeField] private Color coreSignalActiveColor = new Color(1f, 0.76f, 0.16f, 1f);
    [SerializeField] private Color coreSignalReadyColor = new Color(1f, 0.95f, 0.45f, 1f);

    [Header("Dash Icon")]
    [SerializeField] private GaugeBarUI dashGauge;
    [SerializeField] private Image dashCooldownFill;
    [SerializeField] private Image dashIcon;
    [SerializeField] private GameObject dashReadyGlowRoot;
    [SerializeField] private TextMeshProUGUI dashCooldownText;
    [SerializeField] private Color dashReadyColor = Color.white;
    [SerializeField] private Color dashCooldownColor = new Color(0.45f, 0.48f, 0.55f, 1f);

    [Header("Cargo Bottom Bar")]
    [SerializeField] private GaugeBarUI cargoGauge;
    [SerializeField] private TextMeshProUGUI cargoValueText;
    [SerializeField] private CanvasGroup cargoCanvasGroup;
    [SerializeField] private string cargoLabel = "적재량";
    [SerializeField] private string cargoValueFormat = "{0}/{1}";
    [SerializeField] private Color cargoNormalColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color cargoWarningColor = new Color(1f, 0.75f, 0.18f, 1f);
    [SerializeField] private Color cargoCriticalColor = new Color(1f, 0.42f, 0.12f, 1f);
    [SerializeField] private Color cargoFullColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float cargoWarningRatio = 0.8f;
    [SerializeField, Range(0f, 1f)] private float cargoCriticalRatio = 0.95f;
    [SerializeField, Min(0f)] private float cargoVisibleDuration = 2.25f;
    [SerializeField, Min(0.05f)] private float cargoFadeDuration = 0.65f;
    [SerializeField, Range(0f, 1f)] private float cargoWarningIdleAlpha = 0.45f;

    [Header("Resource Counters - Vertical")]
    [SerializeField] private ResourceCounterUI creditsCounter;
    [SerializeField] private ResourceCounterUI scrapCounter;
    [SerializeField] private ResourceCounterUI coreShardCounter;
    [SerializeField] private ResourceCounterUI stabilizedAlloyCounter;
    [SerializeField] private ResourceCounterUI tuningChipCounter;
    [SerializeField] private bool hideZeroResources = true;
    [SerializeField] private Color stabilizedAlloyCounterColor = new Color(0.72f, 0.9f, 0.96f, 1f);
    [SerializeField] private Color tuningChipCounterColor = new Color(0.25f, 0.86f, 1f, 1f);
    [SerializeField, Min(1f)] private float resourceCounterRowSpacing = 12f;

    [Header("Reinforcement / Heat")]
    [SerializeField] private ReinforcementSlotUI reinforcementSlotUI;
    [SerializeField] private WeaponHeatUI weaponHeatUI;
    [SerializeField] private PlayerChargeGaugeUI playerChargeGaugeUI;
    [SerializeField] private Canvas worldGaugeCanvas;
    [SerializeField] private bool createWorldChargeGaugeIfMissing = true;

    [Header("Messages")]
    [SerializeField] private WarningMessageUI warningMessageUI;

    private bool cinematicMode;
    private bool legacyCinematicModeRequested;
    private readonly HashSet<object> cinematicModeOwners = new HashSet<object>();
    private Tween cinematicVisibilityTween;
    private bool additionalVisibilityCaptured;
    private bool subscribed;
    private PlayerHealth subscribedPlayerHealth;
    private PlayerArmor subscribedPlayerArmor;
    private PlayerDash subscribedPlayerDash;
    private ComponentShieldPassive subscribedComponentShield;
    private PlayerReinforcementController subscribedReinforcementController;
    private PlayerCargoController subscribedCargoController;
    private CoreTrackingSignalController subscribedCoreTrackingController;
    private ExpeditionObjectiveDirector subscribedObjectiveDirector;
    private bool[] additionalObjectVisibilityBeforeCinematic;
    private InputAction moveAction;
    private Sequence operationBriefingSequence;
    private RectTransform operationBriefingRect;
    private Vector2 operationBriefingBasePosition;
    private bool operationBriefingPending;
    private bool operationBriefingPresented;
    private bool operationPresentationShuttingDown;
    private bool missingAuthoredOperationReported;
    private bool resourceCounterOriginCached;
    private Vector2 resourceCounterOrigin;
    private readonly ResourceCounterUI[] resourceCounters = new ResourceCounterUI[5];
    private static readonly CurrencyType[] resourceCurrencies =
    {
        CurrencyType.Credits, CurrencyType.ScrapParts, CurrencyType.CoreShards,
        CurrencyType.StabilizedAlloy, CurrencyType.TuningChips
    };
    private int reportedResourceBindingMask;
    private RunManager subscribedRunManager;
    private Sequence cargoVisibilitySequence;
    private bool cargoPresentationInitialized;
    private int lastCargoLoad = -1;
    private int lastCargoCapacity = -1;
    [SerializeField] private Image cargoPanelImage;
    [SerializeField] private Image cargoTrackImage;
    [SerializeField] private Image cargoFillImage;
    [SerializeField] private Image cargoAccentImage;
    [SerializeField] private TextMeshProUGUI cargoLabelText;
    [SerializeField] private Outline cargoFrameOutline;
    [SerializeField] private Image hpPanelImage;
    [SerializeField] private Image hpTrackImage;
    [SerializeField] private Image hpAccentImage;
    [SerializeField] private TextMeshProUGUI hpLabelText;
    [SerializeField] private Outline hpFrameOutline;
    private readonly HashSet<string> missingPresentationFields = new HashSet<string>();

    private static string PresentationPath(Transform target) => target.parent != null
        ? PresentationPath(target.parent) + "/" + target.name : target.name;

    private void CheckPresentation(Object value, string property)
    {
        if (value != null || !missingPresentationFields.Add(property)) return;
        Debug.LogWarning($"[ExpeditionHUD] Missing {property} at '{PresentationPath(transform)}', scene '{gameObject.scene.path}'. " +
            "Restore the listed authored Inspector binding. Only the affected presentation is skipped.", this);
    }
    private ComponentShieldPassive componentShield;

    public bool IsCinematicMode => cinematicMode;
    public bool UsesAuthoredStatusPresentation => true;
    public bool IsCinematicVisibilityTransitionActive =>
        cinematicVisibilityTween != null &&
        cinematicVisibilityTween.IsActive() &&
        cinematicVisibilityTween.IsPlaying();

    private void Awake()
    {
        ResolveReferences();
        EnsureSharedStatusPresentation();
        EnsureCargoPresentation();
        EnsureCoreTrackingPresentation();
        EnsureMenuHintPresentation();
        ResolveCinematicCanvasGroup();
        SetCanvasGroupVisible(true);
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputBindingPersistence.LoadOnce(inputActions);
    }

    private void OnEnable()
    {
        operationPresentationShuttingDown = false;
        // Detach before resolving a new publisher; repeated initialization stays balanced.
        Unsubscribe();
        ResolveReferences();
        EnsureSharedStatusPresentation();
        EnsureCargoPresentation();
        EnsureCoreTrackingPresentation();
        EnsureMenuHintPresentation();
        Subscribe();
        InputSystem.onActionChange -= HandleInputActionChange;
        InputSystem.onActionChange += HandleInputActionChange;
        GameSettingsRuntime.Changed -= HandleGameSettingsChanged;
        GameSettingsRuntime.Changed += HandleGameSettingsChanged;
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void Start()
    {
        Unsubscribe();
        ResolveReferences();
        EnsureSharedStatusPresentation();
        EnsureCargoPresentation();
        Subscribe();
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void OnDisable()
    {
        HidePolarity(polarityOwner);
        operationPresentationShuttingDown = true;
        HideOperationDisplayImmediate();
        KillCinematicVisibilityTween();
        Unsubscribe();
        InputSystem.onActionChange -= HandleInputActionChange;
        GameSettingsRuntime.Changed -= HandleGameSettingsChanged;
        KillCargoVisibilityTween();
        cargoPresentationInitialized = false;
    }

    private void Update()
    {
        if (cinematicMode)
        {
            return;
        }

        UpdateDashDisplay();
        UpdateReinforcementSlot();
    }

    public void SetCinematicMode(bool enabled)
    {
        if (legacyCinematicModeRequested == enabled)
        {
            return;
        }

        legacyCinematicModeRequested = enabled;
        RefreshCinematicModeState(0f);
    }

    public void SetCinematicMode(object owner, bool enabled)
    {
        SetCinematicMode(owner, enabled, 0f);
    }

    public void SetCinematicMode(object owner, bool enabled, float fadeDuration)
    {
        if (owner == null)
        {
            SetCinematicMode(enabled);
            return;
        }

        bool changed = enabled
            ? cinematicModeOwners.Add(owner)
            : cinematicModeOwners.Remove(owner);

        if (changed)
        {
            RefreshCinematicModeState(Mathf.Max(0f, fadeDuration));
        }
    }

    public float ReleaseCinematicMode(object owner)
    {
        if (owner == null)
        {
            legacyCinematicModeRequested = false;
        }
        else if (!cinematicModeOwners.Remove(owner))
        {
            return 0f;
        }

        return RefreshCinematicModeState(cinematicRestoreFadeDuration);
    }

    public void CompleteCinematicVisibilityTransition()
    {
        if (cinematicVisibilityTween == null)
        {
            return;
        }

        KillCinematicVisibilityTween();
        ApplyCinematicVisibility(0f);
    }

    public void RefreshAll()
    {
        ResolveReferences();

        float hp = playerHealth != null ? playerHealth.CurrentHp : 0f;
        float maxHp = playerHealth != null ? playerHealth.MaxHp : 1f;
        float armor = playerArmor != null ? playerArmor.CurrentArmor : 0f;
        float maxArmor = playerArmor != null ? playerArmor.MaxArmor : 1f;
        RefreshHealthAndArmor(hp, maxHp, armor, maxArmor);

        RefreshObjectiveProgress();
        RefreshCurrentResourceBalances();

        RefreshBindingHints();
        UpdateDashDisplay();
        UpdateReinforcementSlot();
        UpdateCargoDisplay();
        InitializeCargoVisibilityIfNeeded();
    }

    public void ShowWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            warningMessageUI?.ShowMessage(message);
        }
    }

    public void SetCoreTrackingVisible(bool visible)
    {
        SetGameObjectVisible(objectiveRoot, visible);
    }

    public void ShowCommunication(
        ShipCommunicationChannel channel,
        string message,
        ShipCommunicationSeverity severity = ShipCommunicationSeverity.Warning,
        float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(message) || warningMessageUI == null)
        {
            return;
        }

        if (duration > 0f)
        {
            warningMessageUI.ShowCommunication(channel, message, severity, duration);
            return;
        }

        warningMessageUI.ShowCommunication(channel, message, severity);
    }

    public void SetOperationDisplay(
        string title,
        string detail,
        Color accentColor,
        bool visible,
        bool subdued)
    {
        if (!visible)
        {
            HideOperationDisplayImmediate();
            return;
        }

        if (operationPresentationShuttingDown || !isActiveAndEnabled)
        {
            return;
        }

        if (!TryPrepareOperationPresentation())
        {
            return;
        }

        if (operationTitleText != null)
        {
            operationTitleText.text = string.IsNullOrWhiteSpace(title) ? "작전 수신" : $"작전 수신 · {title}";
            operationTitleText.color = accentColor;
        }

        if (operationAccentImage != null)
        {
            operationAccentImage.color = accentColor;
        }

        if (operationDetailText != null)
        {
            operationDetailText.text = detail ?? string.Empty;
        }

        if (subdued || operationBriefingPresented)
        {
            operationBriefingPending = false;
            UnbindOperationMovement();
            KillOperationBriefingTween();
            operationRoot.SetActive(false);
            return;
        }

        operationRoot.SetActive(false);
        operationBriefingPending = true;
        BindOperationMovement();
    }

    public void ShowObjectiveBriefing(string title, string detail, Color accentColor)
    {
        if (operationPresentationShuttingDown || !isActiveAndEnabled)
        {
            return;
        }

        if (!TryPrepareOperationPresentation())
        {
            return;
        }

        operationBriefingPending = false;
        operationBriefingPresented = true;
        UnbindOperationMovement();

        if (operationTitleText != null)
        {
            operationTitleText.text = string.IsNullOrWhiteSpace(title)
                ? "목표 갱신"
                : $"목표 갱신 · {title}";
            operationTitleText.color = accentColor;
        }

        if (operationAccentImage != null)
        {
            operationAccentImage.color = accentColor;
        }

        if (operationDetailText != null)
        {
            operationDetailText.text = detail ?? string.Empty;
        }

        PlayOperationBriefing();
    }

    public void HideObjectiveBriefing()
    {
        HideOperationDisplayImmediate();
    }

    public void HideOperationDisplayImmediate()
    {
        operationBriefingPending = false;
        operationBriefingPresented = false;
        UnbindOperationMovement();
        KillOperationBriefingTween();

        if (operationRoot != null)
        {
            operationRoot.SetActive(false);
        }
    }

    private void BindOperationMovement()
    {
        if (!operationBriefingPending || moveAction != null)
        {
            return;
        }

        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        moveAction = InputBindingUtility.ResolveAction(inputActions, playerActionMapName, moveActionName);
        if (moveAction != null)
        {
            moveAction.performed += HandleFirstOperationMovement;
            moveAction.Enable();
        }
    }

    private void UnbindOperationMovement()
    {
        if (moveAction == null)
        {
            return;
        }

        moveAction.performed -= HandleFirstOperationMovement;
        moveAction = null;
    }

    private void HandleFirstOperationMovement(InputAction.CallbackContext context)
    {
        if (!operationBriefingPending || GameplayPauseManager.IsPaused ||
            context.ReadValue<Vector2>().sqrMagnitude <= 0.0001f)
        {
            return;
        }

        operationBriefingPending = false;
        operationBriefingPresented = true;
        UnbindOperationMovement();
        PlayOperationBriefing();
    }

    private void PlayOperationBriefing()
    {
        if (operationPresentationShuttingDown || !isActiveAndEnabled ||
            operationRoot == null || operationCanvasGroup == null)
        {
            return;
        }

        KillOperationBriefingTween();
        operationRoot.SetActive(true);
        if (operationBriefingRect == null) operationBriefingRect = operationRoot.transform as RectTransform;
        if (operationBriefingRect != null)
        {
            operationBriefingBasePosition = operationBriefingRect.anchoredPosition;
            operationBriefingRect.anchoredPosition = operationBriefingBasePosition + Vector2.up * operationBriefingSlideDistance;
        }

        operationCanvasGroup.alpha = 0f;
        operationCanvasGroup.interactable = false;
        operationCanvasGroup.blocksRaycasts = false;

        operationBriefingSequence = DOTween.Sequence().SetUpdate(true);
        operationBriefingSequence.Append(operationCanvasGroup.DOFade(1f, operationBriefingIntroDuration));
        if (operationBriefingRect != null)
        {
            operationBriefingSequence.Join(operationBriefingRect.DOAnchorPos(
                operationBriefingBasePosition,
                operationBriefingIntroDuration
            ));
        }

        operationBriefingSequence.AppendInterval(operationBriefingVisibleDuration);
        operationBriefingSequence.Append(operationCanvasGroup.DOFade(0f, operationBriefingOutroDuration));
        if (operationBriefingRect != null)
        {
            operationBriefingSequence.Join(operationBriefingRect.DOAnchorPos(
                operationBriefingBasePosition + Vector2.down * operationBriefingSlideDistance,
                operationBriefingOutroDuration
            ));
        }

        operationBriefingSequence.OnComplete(() =>
        {
            if (operationBriefingRect != null)
            {
                operationBriefingRect.anchoredPosition = operationBriefingBasePosition;
            }

            if (operationRoot != null)
            {
                operationRoot.SetActive(false);
            }

            operationBriefingSequence = null;
        });
    }

    private void KillOperationBriefingTween()
    {
        operationBriefingSequence?.Kill();
        operationBriefingSequence = null;
        if (operationBriefingRect != null)
        {
            operationBriefingRect.DOKill();
            operationBriefingRect.anchoredPosition = operationBriefingBasePosition;
        }

        if (operationCanvasGroup != null)
        {
            operationCanvasGroup.DOKill();
        }
    }

    private void ResolveReferences()
    {
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (componentShield == null) componentShield = playerHealth != null
            ? playerHealth.GetComponent<ComponentShieldPassive>()
            : null;
        if (playerArmor == null) playerArmor = FindFirstObjectByType<PlayerArmor>();
        if (playerDash == null) playerDash = FindFirstObjectByType<PlayerDash>();
        if (reinforcementController == null) reinforcementController = FindFirstObjectByType<PlayerReinforcementController>();
        if (cargoController == null) cargoController = FindFirstObjectByType<PlayerCargoController>(FindObjectsInactive.Include);
        if (playerChargeGaugeUI == null) playerChargeGaugeUI = FindFirstObjectByType<PlayerChargeGaugeUI>(FindObjectsInactive.Include);
        if (coreTrackingController == null) coreTrackingController = FindFirstObjectByType<CoreTrackingSignalController>();
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);

        if (coreTrackingController == null && objectiveDirector == null && Application.isPlaying && !disableObjectiveDirectorAutoResolution)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }
    }

    private void EnsureSharedStatusPresentation()
    {
        CheckPresentation(hpGauge, nameof(hpGauge));
        CheckPresentation(hpValueText, nameof(hpValueText));
        CheckPresentation(armorFillRect, nameof(armorFillRect));
        CheckPresentation(armorFillImage, nameof(armorFillImage));
        CheckPresentation(weaponHeatUI, nameof(weaponHeatUI));
        CheckPresentation(reinforcementSlotUI, nameof(reinforcementSlotUI));
        if (Application.isPlaying) EnsureWorldChargeGaugePresentation();
    }



    private void EnsureWorldChargeGaugePresentation()
    {
        if (playerChargeGaugeUI != null || !createWorldChargeGaugeIfMissing || playerHealth == null)
        {
            return;
        }

        ResolveWorldGaugeCanvas();
        RectTransform parent = worldGaugeCanvas != null
            ? worldGaugeCanvas.transform as RectTransform
            : null;
        if (parent == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "PlayerChargeGauge_Runtime",
            typeof(RectTransform),
            typeof(CanvasGroup)
        );
        root.layer = parent.gameObject.layer;
        root.SetActive(false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(54f, 6f);

        Image background = CreateRuntimeImage(
            "Background",
            rootRect,
            new Color(0.015f, 0.03f, 0.05f, 0.86f)
        );
        background.rectTransform.anchorMin = new Vector2(0f, 0.25f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        background.raycastTarget = false;

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.layer = root.layer;
        RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
        fillArea.SetParent(rootRect, false);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        Image fill = CreateRuntimeImage("Fill", fillArea, new Color(0.35f, 0.8f, 1f, 1f));

        Slider slider = root.AddComponent<Slider>();
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.targetGraphic = background;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = null;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(1f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GaugeBarUI gauge = root.AddComponent<GaugeBarUI>();
        gauge.ConfigureRuntime(fill, null, group, root);

        WorldGaugeFollower follower = root.AddComponent<WorldGaugeFollower>();
        follower.ConfigureRuntime(
            playerHealth.transform,
            new Vector3(0f, 1.1f, 0f),
            worldGaugeCanvas,
            Camera.main
        );

        PlayerWeaponController weaponController = FindFirstObjectByType<PlayerWeaponController>();
        playerChargeGaugeUI = root.AddComponent<PlayerChargeGaugeUI>();
        playerChargeGaugeUI.ConfigureRuntime(
            playerHealth.transform,
            gauge,
            follower,
            group,
            weaponController
        );
        root.SetActive(true);
    }

    private void ResolveWorldGaugeCanvas()
    {
        if (worldGaugeCanvas != null)
        {
            return;
        }

        if (playerChargeGaugeUI != null)
        {
            worldGaugeCanvas = playerChargeGaugeUI.GetComponentInParent<Canvas>();
            if (worldGaugeCanvas != null)
            {
                return;
            }
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate != null && candidate.name == "Canvas_WorldHUD")
            {
                worldGaugeCanvas = candidate;
                return;
            }
        }
    }


    private void EnsureCargoPresentation()
    {
        CheckPresentation(cargoGauge, nameof(cargoGauge));
        CheckPresentation(cargoValueText, nameof(cargoValueText));
        CheckPresentation(cargoCanvasGroup, nameof(cargoCanvasGroup));
    }





    private Image CreateRuntimeImage(string objectName, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.layer = parent.gameObject.layer;
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }



    private void EnsureCoreTrackingPresentation()
    {
        // Tutorial has no Expedition objective tracker; the entire absent role is optional.
        if (objectiveRoot == null && coreTrackingController == null && objectiveDirector == null) return;
        CheckPresentation(coreTrackingObjectiveText, nameof(coreTrackingObjectiveText));
        CheckPresentation(coreSignalCountText, nameof(coreSignalCountText));
    }




    private void EnsureMenuHintPresentation()
    {
        CheckPresentation(menuHintRoot, nameof(menuHintRoot));
        CheckPresentation(mapHintText, nameof(mapHintText));
        CheckPresentation(inventoryHintText, nameof(inventoryHintText));
    }

    public void SetMenuHintsSuppressed(object owner, bool suppressed)
    {
        if (owner == null)
        {
            return;
        }

        if (suppressed)
        {
            menuHintSuppressors.Add(owner);
        }
        else
        {
            menuHintSuppressors.Remove(owner);
        }

        RefreshBindingHints();
    }



    // The Tutorial installer binds this existing presentation and disables only its builder.
    public bool HasValidAuthoredOperationPresentation
    {
        get
        {
            if (operationRoot == null || !(operationRoot.transform is RectTransform) ||
                operationRoot.scene != gameObject.scene || operationRoot == gameObject ||
                !operationRoot.transform.IsChildOf(transform) ||
                operationCanvasGroup == null || operationCanvasGroup.gameObject != operationRoot ||
                operationAccentImage == null || operationTitleText == null || operationDetailText == null ||
                operationTitleText == operationDetailText)
            {
                return false;
            }

            Transform root = operationRoot.transform;
            return operationAccentImage.transform.IsChildOf(root) &&
                   operationTitleText.transform.IsChildOf(root) &&
                   operationDetailText.transform.IsChildOf(root);
        }
    }

    private bool TryPrepareOperationPresentation()
    {
        if (HasValidAuthoredOperationPresentation)
        {
            return true;
        }

        if (!missingAuthoredOperationReported)
        {
            missingAuthoredOperationReported = true;
            Debug.LogWarning(
                "[ExpeditionHUD] Authored operation briefing bindings are missing or invalid. " +
                $"ExpeditionHUD.operationRoot/operationCanvasGroup/operationAccentImage/operationTitleText/operationDetailText at {PresentationPath(transform)} in '{gameObject.scene.path}'. " +
                 "Restore the listed authored Inspector bindings. " +
                "Only the operation briefing visual was skipped.", this);
        }

        return false;
    }


    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribedPlayerHealth = playerHealth;
        subscribedPlayerArmor = playerArmor;
        subscribedPlayerDash = playerDash;
        subscribedComponentShield = componentShield;
        subscribedReinforcementController = reinforcementController;
        subscribedCargoController = cargoController;
        subscribedCoreTrackingController = coreTrackingController;
        subscribedObjectiveDirector = objectiveDirector;

        if (playerHealth != null)
        {
            playerHealth.Changed += HandleHealthChanged;
            playerHealth.InvincibilityChanged += HandleInvincibilityChanged;
        }

        if (componentShield != null)
        {
            componentShield.ChargeStateChanged += HandleShieldChargeStateChanged;
        }

        if (playerArmor != null)
        {
            playerArmor.Changed += HandleArmorChanged;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted += HandleDashStarted;
            playerDash.DashEnded += HandleDashEnded;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged += HandleReinforcementEquipmentChanged;
            reinforcementController.ChargesChanged += HandleReinforcementChargesChanged;
            reinforcementController.Used += HandleReinforcementUsed;
            reinforcementController.ActiveTimedStatusesChanged += HandleReinforcementTimedStatusesChanged;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged += HandleCargoChanged;
            cargoController.CargoFullRejected += HandleCargoFullRejected;
            cargoController.LoadStateChanged += HandleCargoLoadStateChanged;
        }

        if (coreTrackingController != null)
        {
            coreTrackingController.ProgressChanged += HandleObjectiveProgressChanged;
            coreTrackingController.CoreRevealed += HandleCoreRevealed;
        }
        else if (objectiveDirector != null)
        {
            objectiveDirector.ProgressChanged += HandleObjectiveProgressChanged;
            objectiveDirector.CoreRevealedEvent += HandleCoreRevealed;
        }

        subscribedRunManager = RunManager.Instance;
        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged += HandleWalletChanged;
            subscribedRunManager.RunStarted += HandleRunStarted;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (subscribedPlayerHealth != null)
        {
            subscribedPlayerHealth.Changed -= HandleHealthChanged;
            subscribedPlayerHealth.InvincibilityChanged -= HandleInvincibilityChanged;
        }

        if (subscribedComponentShield != null)
        {
            subscribedComponentShield.ChargeStateChanged -= HandleShieldChargeStateChanged;
        }

        if (subscribedPlayerArmor != null)
        {
            subscribedPlayerArmor.Changed -= HandleArmorChanged;
        }

        if (subscribedPlayerDash != null)
        {
            subscribedPlayerDash.DashStarted -= HandleDashStarted;
            subscribedPlayerDash.DashEnded -= HandleDashEnded;
        }

        if (subscribedReinforcementController != null)
        {
            subscribedReinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            subscribedReinforcementController.ChargesChanged -= HandleReinforcementChargesChanged;
            subscribedReinforcementController.Used -= HandleReinforcementUsed;
            subscribedReinforcementController.ActiveTimedStatusesChanged -= HandleReinforcementTimedStatusesChanged;
        }

        if (subscribedCargoController != null)
        {
            subscribedCargoController.CargoChanged -= HandleCargoChanged;
            subscribedCargoController.CargoFullRejected -= HandleCargoFullRejected;
            subscribedCargoController.LoadStateChanged -= HandleCargoLoadStateChanged;
        }

        if (subscribedCoreTrackingController != null)
        {
            subscribedCoreTrackingController.ProgressChanged -= HandleObjectiveProgressChanged;
            subscribedCoreTrackingController.CoreRevealed -= HandleCoreRevealed;
        }
        else if (subscribedObjectiveDirector != null)
        {
            subscribedObjectiveDirector.ProgressChanged -= HandleObjectiveProgressChanged;
            subscribedObjectiveDirector.CoreRevealedEvent -= HandleCoreRevealed;
        }

        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged -= HandleWalletChanged;
            subscribedRunManager.RunStarted -= HandleRunStarted;
        }
        subscribedRunManager = null;

        subscribed = false;
    }

    private float RefreshCinematicModeState(float restoreFadeDuration)
    {
        bool nextCinematicMode = legacyCinematicModeRequested || cinematicModeOwners.Count > 0;
        if (cinematicMode == nextCinematicMode)
        {
            return 0f;
        }

        if (nextCinematicMode && !additionalVisibilityCaptured)
        {
            CaptureAdditionalObjectVisibility();
            additionalVisibilityCaptured = true;
        }

        cinematicMode = nextCinematicMode;
        return ApplyCinematicVisibility(restoreFadeDuration);
    }

    private float ApplyCinematicVisibility(float restoreFadeDuration = 0f)
    {
        bool visible = !cinematicMode;
        ResolveCinematicCanvasGroup();
        KillCinematicVisibilityTween();

        if (!visible)
        {
            ApplyAdditionalCinematicVisibility(false);
            RefreshBindingHints();
            if (restoreFadeDuration > 0.0001f && cinematicCanvasGroup != null && isActiveAndEnabled)
            {
                cinematicCanvasGroup.interactable = false;
                cinematicCanvasGroup.blocksRaycasts = false;
                cinematicVisibilityTween = cinematicCanvasGroup.DOFade(0f, restoreFadeDuration)
                    .SetUpdate(true).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        cinematicVisibilityTween = null;
                        if (cinematicMode && cinematicRadarPanel != null)
                            cinematicRadarPanel.SetPresentationSuppressed(this, true);
                    });
                return restoreFadeDuration;
            }
            SetCanvasGroupVisible(false);
            if (cinematicRadarPanel != null) cinematicRadarPanel.SetPresentationSuppressed(this, true);
            return 0f;
        }

        if (cinematicRadarPanel != null) cinematicRadarPanel.SetPresentationSuppressed(this, false);
        float safeFadeDuration = Mathf.Max(0f, restoreFadeDuration);
        if (safeFadeDuration > 0.0001f &&
            cinematicCanvasGroup != null &&
            isActiveAndEnabled)
        {
            cinematicCanvasGroup.interactable = false;
            cinematicCanvasGroup.blocksRaycasts = false;

            cinematicVisibilityTween = cinematicCanvasGroup
                .DOFade(1f, safeFadeDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    cinematicVisibilityTween = null;

                    if (this == null || cinematicMode)
                    {
                        return;
                    }

                    SetCanvasGroupVisible(true);
                    ApplyAdditionalCinematicVisibility(true);
                    additionalVisibilityCaptured = false;
                    RefreshBindingHints();
                });

            return safeFadeDuration;
        }

        SetCanvasGroupVisible(true);
        ApplyAdditionalCinematicVisibility(true);
        additionalVisibilityCaptured = false;
        RefreshBindingHints();
        return 0f;
    }

    private void ApplyAdditionalCinematicVisibility(bool visible)
    {
        if (additionalObjectsToHideDuringCinematic == null)
        {
            return;
        }

        for (int i = 0; i < additionalObjectsToHideDuringCinematic.Length; i++)
        {
            GameObject target = additionalObjectsToHideDuringCinematic[i];
            if (target == null)
            {
                continue;
            }

            if (!visible)
            {
                target.SetActive(false);
            }
            else if (additionalObjectVisibilityBeforeCinematic != null &&
                     i < additionalObjectVisibilityBeforeCinematic.Length)
            {
                target.SetActive(additionalObjectVisibilityBeforeCinematic[i]);
            }
        }
    }

    private void ResolveCinematicCanvasGroup()
    {
        CheckPresentation(cinematicCanvasGroup, nameof(cinematicCanvasGroup));
    }

    private void SetCanvasGroupVisible(bool visible)
    {
        if (cinematicCanvasGroup == null)
        {
            return;
        }

        cinematicCanvasGroup.alpha = visible ? 1f : 0f;
        cinematicCanvasGroup.interactable = visible;
        cinematicCanvasGroup.blocksRaycasts = visible;
    }

    private void KillCinematicVisibilityTween()
    {
        if (cinematicVisibilityTween == null)
        {
            return;
        }

        cinematicVisibilityTween.Kill(false);
        cinematicVisibilityTween = null;
    }

    private void CaptureAdditionalObjectVisibility()
    {
        int count = additionalObjectsToHideDuringCinematic != null
            ? additionalObjectsToHideDuringCinematic.Length
            : 0;
        additionalObjectVisibilityBeforeCinematic = new bool[count];

        for (int i = 0; i < count; i++)
        {
            GameObject target = additionalObjectsToHideDuringCinematic[i];
            additionalObjectVisibilityBeforeCinematic[i] = target != null && target.activeSelf;
        }
    }

    private static void SetGameObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private void HandleRunStarted(RunContext _)
    {
        cargoPresentationInitialized = false;
        lastCargoLoad = -1;
        lastCargoCapacity = -1;
        RefreshAll();
    }
    private void HandleWalletChanged(RunWallet wallet) => RefreshWallet(wallet);
    private void HandleHealthChanged(float current, float max) => RefreshHealthAndArmor(
        current,
        max,
        playerArmor != null ? playerArmor.CurrentArmor : 0f,
        playerArmor != null ? playerArmor.MaxArmor : 1f
    );
    private void HandleInvincibilityChanged(bool _) => RefreshHealthStateVisual();
    private void HandleShieldChargeStateChanged(bool _) => RefreshHealthStateVisual();

    private void HandleArmorChanged(float current, float max) => RefreshHealthAndArmor(
        playerHealth != null ? playerHealth.CurrentHp : 0f,
        playerHealth != null ? playerHealth.MaxHp : 1f,
        current,
        max
    );
    private void HandleObjectiveProgressChanged(int current, int required) => RefreshObjectiveProgress(current, required);
    private void HandleCoreRevealed() => RefreshObjectiveProgress();
    private void HandleDashStarted(Vector2 _) => UpdateDashDisplay();
    private void HandleDashEnded() => UpdateDashDisplay();
    private void HandleReinforcementEquipmentChanged(ReinforcementDefinition _, int __, int ___) => UpdateReinforcementSlot();
    private void HandleReinforcementChargesChanged(int _, int __, float ___) => UpdateReinforcementSlot();
    private void HandleReinforcementUsed(ReinforcementDefinition _) => UpdateReinforcementSlot();
    private void HandleReinforcementTimedStatusesChanged() => UpdateReinforcementSlot();
    private void HandleCargoChanged(int current, int capacity)
    {
        bool actualCargoChanged = cargoPresentationInitialized &&
                                  (current != lastCargoLoad || capacity != lastCargoCapacity);
        UpdateCargoDisplay(current, capacity);

        if (!cargoPresentationInitialized)
        {
            InitializeCargoVisibilityIfNeeded();
        }
        else if (actualCargoChanged)
        {
            EmphasizeCargoVisibility();
        }
    }

    private void HandleCargoFullRejected()
    {
        UpdateCargoDisplay();
        InitializeCargoVisibilityIfNeeded();
        EmphasizeCargoVisibility();
    }

    private void HandleCargoLoadStateChanged(CargoLoadState previous, CargoLoadState current)
    {
        if (current >= CargoLoadState.Critical && previous < CargoLoadState.Critical)
        {
            ShowCommunication(
                ShipCommunicationChannel.Cargo,
                "적재 한계 임박",
                ShipCommunicationSeverity.Warning
            );
        }
        else if (current >= CargoLoadState.Overloaded && previous < CargoLoadState.Overloaded)
        {
            ShowCommunication(
                ShipCommunicationChannel.Cargo,
                "적재 과부하 - 기동 성능 저하",
                ShipCommunicationSeverity.Warning
            );
        }

        EmphasizeCargoVisibility();
    }
    private void HandleGameSettingsChanged() => RefreshBindingHints();

    private void HandleInputActionChange(object changedObject, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            RefreshBindingHints();
        }
    }

    private void RefreshHealthAndArmor(float currentHp, float maxHp, float armor, float maxArmor)
    {
        maxHp = Mathf.Max(1f, maxHp);
        maxArmor = Mathf.Max(0f, maxArmor);
        float totalCapacity = Mathf.Max(maxHp, maxHp + maxArmor);
        float hpRatio = Mathf.Clamp01(currentHp / totalCapacity);
        float combinedRatio = Mathf.Clamp01((currentHp + Mathf.Max(0f, armor)) / totalCapacity);
        hpGauge?.SetRatio(hpRatio);

        if (hpValueText != null)
        {
            hpValueText.text = string.Format(hpValueFormat, currentHp, maxHp);
        }

        bool hasArmor = armor > 0.001f;
        GameObject resolvedArmorRoot = armorBonusRoot != null
            ? armorBonusRoot
            : armorBonusText != null ? armorBonusText.gameObject : null;

        SetGameObjectVisible(resolvedArmorRoot, hasArmor);

        if (hasArmor && armorBonusText != null)
        {
            armorBonusText.text = string.Format(armorBonusFormat, armor);
            armorBonusText.color = armorBonusColor;
        }

        RefreshArmorFill(hpRatio, combinedRatio, hasArmor);

        if (legacyArmorGauge != null)
        {
            legacyArmorGauge.SetVisible(false);
        }

        RefreshHealthStateVisual();
    }

    private void RefreshHealthStateVisual()
    {
        Color stateColor = hpNormalStateColor;
        if (playerHealth != null && playerHealth.IsInvincible)
        {
            stateColor = hpInvulnerableStateColor;
        }
        else if (componentShield != null && componentShield.isActiveAndEnabled && componentShield.IsCharged)
        {
            stateColor = hpShieldStateColor;
        }

        if (hpFrameOutline != null)
        {
            hpFrameOutline.effectColor = new Color(stateColor.r, stateColor.g, stateColor.b, 0.62f);
        }

        if (hpAccentImage != null)
        {
            hpAccentImage.color = stateColor;
        }

        if (hpLabelText != null)
        {
            hpLabelText.color = Color.Lerp(Color.white, stateColor, 0.58f);
        }

        if (hpPanelImage != null)
        {
            Color panelTint = Color.Lerp(new Color(0.035f, 0.018f, 0.024f, 1f), stateColor, 0.08f);
            panelTint.a = 0.96f;
            hpPanelImage.color = panelTint;
        }

        if (hpTrackImage != null)
        {
            Color trackTint = Color.Lerp(new Color(0.1f, 0.025f, 0.035f, 1f), stateColor, 0.1f);
            trackTint.a = 0.98f;
            hpTrackImage.color = trackTint;
        }
    }

    private void RefreshArmorFill(float hpRatio, float combinedRatio, bool visible)
    {
        if (armorFillRect == null)
        {
            return;
        }

        armorFillRect.anchorMin = new Vector2(hpRatio, armorFillRect.anchorMin.y);
        armorFillRect.anchorMax = new Vector2(Mathf.Max(hpRatio, combinedRatio), armorFillRect.anchorMax.y);
        if (armorFillImage != null)
        {
            armorFillImage.color = armorFillColor;
        }
        SetGameObjectVisible(armorFillRect.gameObject, visible && combinedRatio > hpRatio + 0.0001f);
    }



    private void RefreshObjectiveProgress()
    {
        EnsureCoreTrackingPresentation();
        if (IsRegion3CorelessDepth())
        {
            SetCoreTrackingVisible(false);
            return;
        }

        coreTrackingController ??= FindFirstObjectByType<CoreTrackingSignalController>();

        if (coreTrackingController != null)
        {
            SetCoreTrackingVisible(coreTrackingController.IsTrackingActive);
            if (!coreTrackingController.IsTrackingActive)
            {
                return;
            }

            RefreshObjectiveProgress(
                coreTrackingController.CurrentSignalCount,
                coreTrackingController.RequiredSignalCount
            );
            return;
        }

        SetCoreTrackingVisible(true);

        if (objectiveDirector == null && Application.isPlaying && !disableObjectiveDirectorAutoResolution)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }

        int current = objectiveDirector != null ? objectiveDirector.SignalCount : 0;
        int required = objectiveDirector != null ? objectiveDirector.SignalsRequiredToRevealCore : 2;
        RefreshObjectiveProgress(current, required);
    }

    private static bool IsRegion3CorelessDepth()
    {
        RunManager runManager = RunManager.Instance;
        return runManager != null &&
               runManager.HasActiveRun &&
               runManager.CurrentRun.ExpeditionDepth == ExpeditionDepth.DeepZone2;
    }

    private void RefreshObjectiveProgress(int current, int required)
    {
        required = Mathf.Max(1, required);
        int clamped = Mathf.Clamp(current, 0, required);
        bool ready = current >= required;

        if (coreTrackingObjectiveText != null)
        {
            coreTrackingObjectiveText.text = ready
                ? "코어 좌표 확인 · 코어 활성화"
                : "코어 추적 신호 확보";
            coreTrackingObjectiveText.color = ready ? coreSignalReadyColor : coreSignalActiveColor;
        }

        if (coreSignalPips != null && coreSignalPips.Length > 0)
        {
            for (int i = 0; i < coreSignalPips.Length; i++)
            {
                if (coreSignalPips[i] != null)
                {
                    coreSignalPips[i].color = i < clamped
                        ? ready ? coreSignalReadyColor : coreSignalActiveColor
                        : coreSignalInactiveColor;
                }
            }
        }
        else if (legacyObjectiveSignalGauge != null)
        {
            legacyObjectiveSignalGauge.SetValue(clamped, required);
        }

        if (coreSignalIcon != null)
        {
            coreSignalIcon.color = ready ? coreSignalReadyColor : coreSignalActiveColor;
        }

        if (coreSignalCountText != null)
        {
            coreSignalCountText.text = string.Format(coreSignalCountFormat, clamped, required);
            coreSignalCountText.color = ready ? coreSignalReadyColor : Color.white;
        }

        SetGameObjectVisible(coreSignalReadyPulseRoot, ready);
    }

    private void RefreshCurrentResourceBalances()
    {
        RefreshWallet(RunManager.Instance != null && RunManager.Instance.CurrentRun != null
            ? RunManager.Instance.CurrentRun.Wallet
            : null);
    }

    private void RefreshWallet(RunWallet wallet)
    {
        resourceCounters[0] = creditsCounter;
        resourceCounters[1] = scrapCounter;
        resourceCounters[2] = coreShardCounter;
        resourceCounters[3] = stabilizedAlloyCounter;
        resourceCounters[4] = tuningChipCounter;
        int invalidMask = 0;
        // Both serialized consumers now have Editor authoring tools; no runtime cloning.
        {
            for (int i = 0; i < resourceCounters.Length; i++)
            {
                ResourceCounterUI counter = resourceCounters[i];
                bool valid = resourceRoot != null && resourceRoot.scene == gameObject.scene &&
                    resourceRoot.transform.IsChildOf(transform) && counter != null &&
                    counter.transform.parent == resourceRoot.transform && counter.HasAuthoredBindings;
                for (int j = 0; j < resourceCounters.Length; j++)
                {
                    if (i != j && counter != null && counter == resourceCounters[j]) valid = false;
                }
                if (!valid) invalidMask |= 1 << i;
            }
        }
        for (int i = 0; i < resourceCounters.Length; i++)
        {
            if ((invalidMask & (1 << i)) != 0)
            {
                if ((reportedResourceBindingMask & (1 << i)) == 0)
                {
                    reportedResourceBindingMask |= 1 << i;
                    Debug.LogWarning($"[ExpeditionHUD] Authored {resourceCurrencies[i]} counter has missing, invalid, or duplicate bindings. " +
                        $"Resource mapping index {i} at '{PresentationPath(transform)}', scene '{gameObject.scene.path}'. " +
                         "Restore the listed authored Inspector bindings. " +
                        "Only this resource counter was skipped.", this);
                }
                resourceCounters[i] = null;
                continue;
            }
            resourceCounters[i]?.SetHideWhenZero(hideZeroResources);
            resourceCounters[i]?.SetAmount(wallet != null ? wallet.GetAmount(resourceCurrencies[i]) : 0);
        }
        RepackVisibleResourceCounters(resourceCounters);
        if (cargoController == null)
        {
            UpdateCargoDisplay();
        }
    }

    private void CacheResourceCounterOrigin()
    {
        if (resourceCounterOriginCached)
        {
            return;
        }

        for (int i = 0; i < resourceCounters.Length; i++)
        {
            if (resourceCounters[i] != null && resourceCounters[i].transform is RectTransform anchorRect)
            {
                resourceCounterOrigin = anchorRect.anchoredPosition;
                resourceCounterOriginCached = true;
                break;
            }
        }
    }

    private void RepackVisibleResourceCounters(ResourceCounterUI[] counters)
    {
        CacheResourceCounterOrigin();

        if (!resourceCounterOriginCached || counters == null)
        {
            return;
        }

        int visibleIndex = 0;
        float spacing = Mathf.Max(1f, resourceCounterRowSpacing);

        for (int i = 0; i < counters.Length; i++)
        {
            ResourceCounterUI counter = counters[i];

            if (counter == null || !counter.gameObject.activeSelf)
            {
                continue;
            }

            if (counter.transform is RectTransform counterRect)
            {
                counterRect.anchoredPosition = resourceCounterOrigin + Vector2.down * (visibleIndex * spacing);
            }

            visibleIndex++;
        }
    }


    private void UpdateDashDisplay()
    {
        bool ready = playerDash == null || playerDash.CanDash;
        float ratio = 1f;
        float remaining = 0f;

        if (!ready && playerDash != null)
        {
            remaining = playerDash.RemainingCooldown;
            ratio = 1f - playerDash.CooldownRatio;
        }

        dashGauge?.SetRatio(ratio);
        dashGauge?.SetText(ready ? string.Empty : $"{remaining:0.0}");

        if (dashCooldownFill != null)
        {
            dashCooldownFill.fillAmount = ratio;
            dashCooldownFill.color = dashReadyColor;
        }

        if (dashIcon != null)
        {
            dashIcon.color = dashCooldownColor;
        }

        if (dashCooldownText != null)
        {
            dashCooldownText.text = ready ? string.Empty : $"{remaining:0.0}";
        }

        SetGameObjectVisible(dashReadyGlowRoot, ready);
    }


    private void UpdateCargoDisplay()
    {
        int current = 0;
        int capacity = 0;

        if (cargoController != null)
        {
            current = cargoController.CurrentLoad;
            capacity = cargoController.MaxCapacity;
        }
        else if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunContext runContext = RunManager.Instance.CurrentRun;
            current = runContext.CurrentCargoLoad;
            capacity = runContext.MaxCargoCapacity;
        }

        UpdateCargoDisplay(current, capacity);
    }

    private void UpdateCargoDisplay(int current, int capacity)
    {
        current = Mathf.Max(0, current);
        capacity = Mathf.Max(0, capacity);

        int safeCapacity = Mathf.Max(1, capacity);
        float ratio = Mathf.Clamp01(current / (float)safeCapacity);
        Color color = ratio >= 0.999f
            ? cargoFullColor
            : ratio >= cargoCriticalRatio
                ? cargoCriticalColor
                : ratio >= cargoWarningRatio ? cargoWarningColor : cargoNormalColor;

        cargoGauge?.SetValue(current, safeCapacity);
        string cargoText = string.Format(cargoValueFormat, current, capacity);
        cargoGauge?.SetText(cargoText);
        cargoGauge?.SetFillColor(color);

        if (cargoValueText != null)
        {
            cargoValueText.gameObject.SetActive(true);
            cargoValueText.text = cargoText;
            cargoValueText.color = ratio >= cargoWarningRatio
                ? Color.Lerp(Color.white, color, ratio >= 0.999f ? 0.6f : 0.35f)
                : Color.white;
        }

        if (cargoLabelText != null)
        {
            cargoLabelText.text = cargoLabel;
            cargoLabelText.color = ratio >= cargoWarningRatio
                ? Color.Lerp(new Color(0.62f, 0.74f, 0.79f, 1f), color, 0.35f)
                : new Color(0.62f, 0.74f, 0.79f, 1f);
        }

        if (cargoAccentImage != null)
        {
            cargoAccentImage.color = new Color(color.r, color.g, color.b, 0.9f);
        }

        if (cargoFrameOutline != null)
        {
            float frameAlpha = ratio >= 0.999f ? 0.78f : ratio >= cargoWarningRatio ? 0.58f : 0.34f;
            cargoFrameOutline.effectColor = new Color(color.r, color.g, color.b, frameAlpha);
        }

        lastCargoLoad = current;
        lastCargoCapacity = capacity;
    }

    private void ResolveCargoCanvasGroup()
    {
        CheckPresentation(cargoCanvasGroup, nameof(cargoCanvasGroup));
    }

    private void InitializeCargoVisibilityIfNeeded()
    {
        if (cargoPresentationInitialized)
        {
            return;
        }

        ResolveCargoCanvasGroup();
        cargoPresentationInitialized = true;
        SetCargoAlpha(GetCargoIdleAlpha());
    }

    private void EmphasizeCargoVisibility()
    {
        ResolveCargoCanvasGroup();
        if (cargoCanvasGroup == null)
        {
            return;
        }

        KillCargoVisibilityTween();
        SetCargoAlpha(1f);
        float idleAlpha = GetCargoIdleAlpha();

        if (idleAlpha >= 0.999f)
        {
            return;
        }

        cargoVisibilitySequence = DOTween.Sequence().SetUpdate(true);
        cargoVisibilitySequence.AppendInterval(Mathf.Max(0f, cargoVisibleDuration));
        cargoVisibilitySequence.Append(
            cargoCanvasGroup.DOFade(idleAlpha, Mathf.Max(0.05f, cargoFadeDuration))
                .SetEase(Ease.OutQuad)
        );
        cargoVisibilitySequence.OnComplete(() => cargoVisibilitySequence = null);
    }

    private float GetCargoIdleAlpha()
    {
        if (lastCargoCapacity <= 0)
        {
            return 0f;
        }

        float ratio = Mathf.Clamp01(lastCargoLoad / (float)lastCargoCapacity);
        if (ratio >= 0.999f)
        {
            return 1f;
        }

        return ratio >= cargoWarningRatio ? cargoWarningIdleAlpha : 0f;
    }

    private void SetCargoAlpha(float alpha)
    {
        ResolveCargoCanvasGroup();
        if (cargoCanvasGroup != null)
        {
            cargoCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void KillCargoVisibilityTween()
    {
        cargoVisibilitySequence?.Kill();
        cargoVisibilitySequence = null;
        cargoCanvasGroup?.DOKill();
    }

    private void UpdateReinforcementSlot()
    {
        if (reinforcementSlotUI == null)
        {
            return;
        }

        if (reinforcementController == null)
        {
            reinforcementSlotUI.SetEmpty();
            return;
        }

        reinforcementSlotUI.RefreshFrom(reinforcementController);
    }

    private void RefreshBindingHints()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        bool visible = !cinematicMode && GameSettingsRuntime.ShowHudKeyHints;
        bool menuHintsVisible = visible && menuHintSuppressors.Count == 0;
        SetGameObjectVisible(menuHintRoot, menuHintsVisible);

        if (!visible)
        {
            reinforcementSlotUI?.SetKeyLabel(string.Empty);

            if (reinforcementKeyText != null)
            {
                reinforcementKeyText.text = string.Empty;
            }

            return;
        }

        string mapKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, mapActionName, "Tab");
        string inventoryKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, inventoryActionName, "E");
        string reinforcementKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, reinforcementActionName, "R");
        string reinforcementLabel = string.Format(reinforcementKeyFormat, reinforcementKey);
        reinforcementSlotUI?.SetKeyLabel(reinforcementLabel);

        if (mapHintText != null)
        {
            mapHintText.text = string.Format(mapHintFormat, mapKey);
        }

        if (inventoryHintText != null)
        {
            inventoryHintText.text = string.Format(inventoryHintFormat, inventoryKey);
        }

        if (reinforcementKeyText != null)
        {
            reinforcementKeyText.text = reinforcementLabel;
        }
    }
}
