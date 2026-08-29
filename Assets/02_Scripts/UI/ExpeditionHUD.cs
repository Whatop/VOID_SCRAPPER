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
    [SerializeField] private bool createCoreTrackingPresentationIfMissing = true;
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("Operation")]
    [SerializeField] private GameObject operationRoot;
    [SerializeField] private CanvasGroup operationCanvasGroup;
    [SerializeField] private TextMeshProUGUI operationTitleText;
    [SerializeField] private TextMeshProUGUI operationDetailText;
    [SerializeField] private Image operationAccentImage;
    [SerializeField] private bool createOperationPresentationIfMissing = true;
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
    [SerializeField] private bool addCinematicCanvasGroupIfMissing = true;
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
    [SerializeField] private string hpValueFormat = "{0:0}/{1:0}";
    [SerializeField] private string armorBonusFormat = "장갑 {0:0}";
    [SerializeField] private Color armorBonusColor = Color.white;
    [SerializeField] private Color armorFillColor = Color.white;
    [SerializeField] private Color hpNormalStateColor = new Color(0.95f, 0.24f, 0.28f, 1f);
    [SerializeField] private Color hpShieldStateColor = new Color(0.25f, 0.82f, 1f, 1f);
    [SerializeField] private Color hpInvulnerableStateColor = new Color(1f, 0.78f, 0.22f, 1f);
    [Tooltip("HPValueText와 ArmorValueText가 같은 부모일 때 Armor 텍스트를 HP 텍스트 바로 옆으로 고정합니다.")]
    [FormerlySerializedAs("autoPositionArmorBonusBesideHpText")]
    [SerializeField] private bool anchorArmorBonusToHpText = true;
    [SerializeField] private Vector2 armorBonusOffset = new Vector2(2f, 0f);
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
    [SerializeField] private bool createCargoPresentationIfMissing = true;
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
    [SerializeField, Min(1f)] private float resourceCounterRowSpacing = 12f;

    [Header("Reinforcement / Heat")]
    [SerializeField] private ReinforcementSlotUI reinforcementSlotUI;
    [SerializeField] private WeaponHeatUI weaponHeatUI;
    [SerializeField] private PlayerChargeGaugeUI playerChargeGaugeUI;
    [SerializeField] private Canvas worldGaugeCanvas;
    [SerializeField] private bool createSharedStatusPresentationIfMissing = true;
    [SerializeField] private bool createWorldChargeGaugeIfMissing = true;

    [Header("Messages")]
    [SerializeField] private WarningMessageUI warningMessageUI;

    private bool cinematicMode;
    private bool legacyCinematicModeRequested;
    private readonly HashSet<object> cinematicModeOwners = new HashSet<object>();
    private Tween cinematicVisibilityTween;
    private bool additionalVisibilityCaptured;
    private bool subscribed;
    private bool[] additionalObjectVisibilityBeforeCinematic;
    private InputAction moveAction;
    private Sequence operationBriefingSequence;
    private RectTransform operationBriefingRect;
    private Vector2 operationBriefingBasePosition;
    private bool operationBriefingPending;
    private bool operationBriefingPresented;
    private bool operationPresentationShuttingDown;
    private bool resourceCounterOriginCached;
    private Vector2 resourceCounterOrigin;
    private Sequence cargoVisibilitySequence;
    private bool cargoPresentationInitialized;
    private int lastCargoLoad = -1;
    private int lastCargoCapacity = -1;
    private Image cargoPanelImage;
    private Image cargoTrackImage;
    private Image cargoFillImage;
    private Image cargoAccentImage;
    private TextMeshProUGUI cargoLabelText;
    private Outline cargoFrameOutline;
    private Image hpPanelImage;
    private Image hpTrackImage;
    private Image hpAccentImage;
    private TextMeshProUGUI hpLabelText;
    private Outline hpFrameOutline;
    private ComponentShieldPassive componentShield;

    public bool IsCinematicMode => cinematicMode;
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
        ApplySharedHudLayout();
        EnsureStabilizedAlloyCounter();
        CacheResourceCounterOrigin();
        ResolveCinematicCanvasGroup();
        SetCanvasGroupVisible(true);
        ConfigureDashIcon();
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputBindingPersistence.LoadOnce(inputActions);
    }

    private void OnEnable()
    {
        operationPresentationShuttingDown = false;
        ResolveReferences();
        EnsureSharedStatusPresentation();
        EnsureCargoPresentation();
        EnsureCoreTrackingPresentation();
        EnsureMenuHintPresentation();
        ApplySharedHudLayout();
        EnsureStabilizedAlloyCounter();
        CacheResourceCounterOrigin();
        Subscribe();
        InputSystem.onActionChange += HandleInputActionChange;
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
        ApplySharedHudLayout();
        Subscribe();
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void OnDisable()
    {
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
            RefreshCinematicModeState(0f);
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
        RefreshWallet(RunManager.Instance != null && RunManager.Instance.CurrentRun != null
            ? RunManager.Instance.CurrentRun.Wallet
            : null);

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

        EnsureOperationPresentation();
        if (operationRoot == null)
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

        EnsureOperationPresentation();
        if (operationRoot == null)
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
        operationRoot.transform.SetAsLastSibling();
        operationBriefingRect ??= operationRoot.transform as RectTransform;
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
        playerHealth ??= FindFirstObjectByType<PlayerHealth>();
        componentShield ??= playerHealth != null
            ? playerHealth.GetComponent<ComponentShieldPassive>()
            : null;
        playerArmor ??= FindFirstObjectByType<PlayerArmor>();
        playerDash ??= FindFirstObjectByType<PlayerDash>();
        reinforcementController ??= FindFirstObjectByType<PlayerReinforcementController>();
        reinforcementSlotUI ??= FindFirstObjectByType<ReinforcementSlotUI>();
        statusEffectPresenter ??= FindFirstObjectByType<StatusEffectHUDPresenter>(FindObjectsInactive.Include);
        cargoController ??= FindFirstObjectByType<PlayerCargoController>(FindObjectsInactive.Include);
        weaponHeatUI ??= FindFirstObjectByType<WeaponHeatUI>(FindObjectsInactive.Include);
        playerChargeGaugeUI ??= FindFirstObjectByType<PlayerChargeGaugeUI>(FindObjectsInactive.Include);
        warningMessageUI ??= FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        coreTrackingController ??= FindFirstObjectByType<CoreTrackingSignalController>();
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);

        if (coreTrackingController == null && objectiveDirector == null && Application.isPlaying && !disableObjectiveDirectorAutoResolution)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }
    }

    private void EnsureSharedStatusPresentation()
    {
        if (!Application.isPlaying || !createSharedStatusPresentationIfMissing)
        {
            return;
        }

        EnsureWeaponHeatPresentation();
        EnsureWorldChargeGaugePresentation();
        EnsureReinforcementPresentation();
        ApplyHealthVisualPolish();
    }

    private void EnsureWeaponHeatPresentation()
    {
        if (weaponHeatUI != null || statusRoot == null)
        {
            return;
        }

        RectTransform parent = statusRoot.transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "WeaponHeatStatus",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        root.layer = parent.gameObject.layer;
        root.SetActive(false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-186.5f, 111f);
        rootRect.sizeDelta = new Vector2(91f, 7f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.015f, 0.03f, 0.05f, 0.86f);
        background.raycastTarget = false;

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.layer = root.layer;
        RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
        fillArea.SetParent(rootRect, false);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(1f, 1f);
        fillArea.offsetMax = new Vector2(-1f, -1f);

        Image fill = CreateRuntimeImage("Fill", fillArea, new Color(0.35f, 0.9f, 1f, 1f));

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
        slider.SetValueWithoutNotify(0f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        PlayerWeaponController weaponController = FindFirstObjectByType<PlayerWeaponController>();
        GaugeBarUI heatGauge = root.AddComponent<GaugeBarUI>();
        heatGauge.ConfigureRuntime(fill, null, group, root);
        weaponHeatUI = root.AddComponent<WeaponHeatUI>();
        weaponHeatUI.ConfigureRuntime(root, group, weaponController, fill, null);
        root.SetActive(true);
    }

    private void ApplyHealthVisualPolish()
    {
        if (!Application.isPlaying || hpGauge == null)
        {
            return;
        }

        RectTransform rootRect = hpGauge.transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        hpPanelImage ??= hpGauge.GetComponent<Image>();
        hpPanelImage ??= hpGauge.gameObject.AddComponent<Image>();
        hpPanelImage.color = new Color(0.035f, 0.018f, 0.024f, 0.96f);
        hpPanelImage.raycastTarget = false;

        hpFrameOutline ??= hpGauge.GetComponent<Outline>();
        hpFrameOutline ??= hpGauge.gameObject.AddComponent<Outline>();
        hpFrameOutline.effectColor = new Color(0.95f, 0.24f, 0.28f, 0.6f);
        hpFrameOutline.effectDistance = new Vector2(1f, -1f);
        hpFrameOutline.useGraphicAlpha = false;

        Slider slider = hpGauge.GetComponent<Slider>();
        if (slider != null)
        {
            hpTrackImage ??= slider.targetGraphic as Image;
        }

        if (hpAccentImage == null)
        {
            Transform accentTransform = rootRect.Find("HPAccent");
            hpAccentImage = accentTransform != null
                ? accentTransform.GetComponent<Image>()
                : CreateRuntimeImage("HPAccent", rootRect, new Color(0.95f, 0.2f, 0.24f, 1f));
        }

        if (hpLabelText == null)
        {
            Transform labelTransform = rootRect.Find("HPLabel");
            hpLabelText = labelTransform != null
                ? labelTransform.GetComponent<TextMeshProUGUI>()
                : CreateRuntimeText("HPLabel", rootRect, 5.5f, TextAlignmentOptions.Left);
        }

        RectTransform hpValueRect = hpValueText != null ? hpValueText.rectTransform : null;
        if (hpValueRect != null && hpValueRect.parent != rootRect)
        {
            hpValueRect.SetParent(rootRect, false);
        }

        RectTransform armorTextRect = armorBonusText != null ? armorBonusText.rectTransform : null;
        if (armorTextRect != null && armorTextRect.parent != rootRect)
        {
            armorTextRect.SetParent(rootRect, false);
        }

        SetCargoPanelRect(
            hpAccentImage.rectTransform,
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(1f, 2f),
            new Vector2(3f, -2f)
        );
        SetCargoPanelRect(
            hpLabelText.rectTransform,
            new Vector2(0f, 0.42f),
            new Vector2(0.35f, 1f),
            new Vector2(7f, 0f),
            new Vector2(-1f, -1f)
        );
        SetCargoPanelRect(
            hpValueRect,
            new Vector2(0.48f, 0.42f),
            Vector2.one,
            Vector2.zero,
            new Vector2(-5f, -1f)
        );

        hpLabelText.text = "HP";
        hpLabelText.fontStyle = FontStyles.Bold;
        hpLabelText.fontSize = 5.5f;
        hpLabelText.color = new Color(0.94f, 0.62f, 0.64f, 1f);
        hpLabelText.raycastTarget = false;

        if (hpValueText != null)
        {
            hpValueText.fontStyle = FontStyles.Bold;
            hpValueText.fontSize = 6.5f;
            hpValueText.alignment = TextAlignmentOptions.Right;
            hpValueText.color = Color.white;
            hpValueText.raycastTarget = false;
        }

        hpValueFormat = "{0:0} / {1:0}";
        anchorArmorBonusToHpText = false;

        if (armorBonusText != null)
        {
            SetCargoPanelRect(
                armorTextRect,
                new Vector2(0.24f, 0.42f),
                new Vector2(0.57f, 1f),
                Vector2.zero,
                new Vector2(-1f, -1f)
            );
            armorBonusText.fontSize = 5.5f;
            armorBonusText.alignment = TextAlignmentOptions.Center;
            armorBonusText.raycastTarget = false;
        }

        if (hpTrackImage != null)
        {
            SetCargoPanelRect(
                hpTrackImage.rectTransform,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(5f, 3f),
                new Vector2(-4f, 8f)
            );
            hpTrackImage.color = new Color(0.1f, 0.025f, 0.035f, 0.98f);
            hpTrackImage.raycastTarget = false;
        }

        RectTransform fillAreaRect = slider != null && slider.fillRect != null
            ? slider.fillRect.parent as RectTransform
            : null;
        if (fillAreaRect != null)
        {
            SetCargoPanelRect(
                fillAreaRect,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(6f, 4f),
                new Vector2(-5f, 7f)
            );
        }

        if (hpGauge.FillImage != null)
        {
            hpGauge.FillImage.color = new Color(0.92f, 0.16f, 0.2f, 1f);
            hpGauge.FillImage.raycastTarget = false;
        }

        armorFillColor = new Color(0.88f, 0.9f, 0.94f, 1f);
        armorBonusColor = new Color(0.88f, 0.9f, 0.94f, 1f);
        if (armorFillImage != null)
        {
            armorFillImage.color = armorFillColor;
            armorFillImage.raycastTarget = false;
        }

        RefreshHealthStateVisual();
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

    private void EnsureReinforcementPresentation()
    {
        if (reinforcementSlotUI != null)
        {
            return;
        }

        RectTransform parent = transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "ReinforcementSlotUI_Runtime",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        root.layer = parent.gameObject.layer;
        root.SetActive(false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-220f, -103f);
        rootRect.sizeDelta = new Vector2(28f, 28f);
        rootRect.localScale = new Vector3(0.8f, 0.8f, 1f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.075f, 0.92f);
        background.raycastTarget = false;

        Image readyGlow = CreateRuntimeImage("ReadyGlow", rootRect, new Color(0.65f, 0.95f, 1f, 0.2f));
        SetRuntimeInset(readyGlow.rectTransform, -2f);
        readyGlow.gameObject.SetActive(false);

        Image icon = CreateRuntimeImage("Icon", rootRect, Color.white);
        SetRuntimeInset(icon.rectTransform, 2f);
        icon.preserveAspect = true;

        Image rechargeFill = CreateRuntimeImage("RechargeFill", rootRect, Color.white);
        SetRuntimeInset(rechargeFill.rectTransform, 2f);
        rechargeFill.preserveAspect = true;

        Image durationFill = CreateRuntimeImage("DurationFill", rootRect, new Color(0.35f, 0.9f, 1f, 0.55f));
        SetRuntimeInset(durationFill.rectTransform, 2f);
        durationFill.preserveAspect = true;

        Image disabledOverlay = CreateRuntimeImage("DisabledOverlay", rootRect, new Color(0f, 0f, 0f, 0.42f));
        SetRuntimeInset(disabledOverlay.rectTransform, 2f);

        TextMeshProUGUI chargeText = CreateRuntimeText("Charges", rootRect, 6f, TextAlignmentOptions.TopRight);
        chargeText.rectTransform.offsetMin = new Vector2(2f, 2f);
        chargeText.rectTransform.offsetMax = new Vector2(-2f, -2f);

        TextMeshProUGUI keyText = CreateRuntimeText("Binding", rootRect, 6f, TextAlignmentOptions.Center);
        RectTransform keyRect = keyText.rectTransform;
        keyRect.anchorMin = new Vector2(0.5f, 0f);
        keyRect.anchorMax = new Vector2(0.5f, 0f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -2f);
        keyRect.sizeDelta = new Vector2(36f, 9f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        reinforcementSlotUI = root.AddComponent<ReinforcementSlotUI>();
        reinforcementSlotUI.ConfigureRuntime(
            root,
            group,
            icon,
            rechargeFill,
            durationFill,
            readyGlow.gameObject,
            readyGlow,
            chargeText,
            keyText,
            disabledOverlay
        );
        root.SetActive(true);
    }

    private void EnsureCargoPresentation()
    {
        if (cargoRoot != null || !Application.isPlaying || !createCargoPresentationIfMissing || statusRoot == null)
        {
            ResolveCargoCanvasGroup();
            ApplyCargoVisualPolish();
            return;
        }

        RectTransform parent = statusRoot.transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "CargoStatus",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        root.layer = parent.gameObject.layer;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-232f, -67f);
        rootRect.sizeDelta = new Vector2(104f, 22f);

        cargoPanelImage = root.GetComponent<Image>();
        cargoPanelImage.color = new Color(0.012f, 0.026f, 0.04f, 0.94f);
        cargoPanelImage.raycastTarget = false;

        cargoTrackImage = CreateRuntimeImage(
            "CargoBarTrack",
            rootRect,
            new Color(0.025f, 0.065f, 0.085f, 0.96f)
        );
        cargoFillImage = CreateRuntimeImage("Fill", rootRect, cargoNormalColor);
        cargoFillImage.type = Image.Type.Filled;
        cargoFillImage.fillMethod = Image.FillMethod.Horizontal;
        cargoFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        cargoFillImage.fillClockwise = true;

        TextMeshProUGUI valueText = CreateRuntimeText("CargoValue", rootRect, 6.5f, TextAlignmentOptions.Right);
        valueText.fontStyle = FontStyles.Bold;
        valueText.color = Color.white;

        cargoRoot = root;
        cargoCanvasGroup = root.GetComponent<CanvasGroup>();
        cargoCanvasGroup.interactable = false;
        cargoCanvasGroup.blocksRaycasts = false;
        cargoValueText = valueText;
        cargoGauge = root.AddComponent<GaugeBarUI>();
        cargoGauge.ConfigureRuntime(cargoFillImage, valueText, cargoCanvasGroup, root);
        ApplyCargoVisualPolish();
    }

    private void ApplyCargoVisualPolish()
    {
        if (!Application.isPlaying || cargoRoot == null || cargoGauge == null)
        {
            return;
        }

        RectTransform rootRect = cargoRoot.transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        cargoPanelImage ??= cargoRoot.GetComponent<Image>();
        cargoPanelImage ??= cargoRoot.AddComponent<Image>();
        cargoPanelImage.color = new Color(0.012f, 0.026f, 0.04f, 0.94f);
        cargoPanelImage.raycastTarget = false;

        cargoFrameOutline ??= cargoRoot.GetComponent<Outline>();
        cargoFrameOutline ??= cargoRoot.AddComponent<Outline>();
        cargoFrameOutline.effectDistance = new Vector2(1f, -1f);
        cargoFrameOutline.useGraphicAlpha = false;

        Slider slider = cargoGauge.GetComponent<Slider>();
        if (slider != null)
        {
            cargoTrackImage ??= slider.targetGraphic as Image;
            cargoFillImage ??= slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        }

        if (cargoTrackImage == null)
        {
            Transform trackTransform = rootRect.Find("CargoBarTrack");
            cargoTrackImage = trackTransform != null ? trackTransform.GetComponent<Image>() : null;
        }

        if (cargoFillImage == null)
        {
            Transform fillTransform = rootRect.Find("Fill");
            cargoFillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        }

        if (cargoAccentImage == null)
        {
            Transform accentTransform = rootRect.Find("CargoAccent");
            cargoAccentImage = accentTransform != null
                ? accentTransform.GetComponent<Image>()
                : CreateRuntimeImage("CargoAccent", rootRect, cargoNormalColor);
        }

        if (cargoLabelText == null)
        {
            Transform labelTransform = rootRect.Find("CargoLabel");
            cargoLabelText = labelTransform != null
                ? labelTransform.GetComponent<TextMeshProUGUI>()
                : CreateRuntimeText("CargoLabel", rootRect, 5.5f, TextAlignmentOptions.Left);
        }

        SetCargoPanelRect(
            cargoAccentImage.rectTransform,
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(1f, 2f),
            new Vector2(3f, -2f)
        );
        SetCargoPanelRect(
            cargoLabelText.rectTransform,
            new Vector2(0f, 0.42f),
            new Vector2(0.58f, 1f),
            new Vector2(7f, 0f),
            new Vector2(-1f, -1f)
        );
        SetCargoPanelRect(
            cargoValueText.rectTransform,
            new Vector2(0.48f, 0.42f),
            Vector2.one,
            Vector2.zero,
            new Vector2(-5f, -1f)
        );

        cargoLabelText.text = cargoLabel;
        cargoLabelText.fontStyle = FontStyles.Normal;
        cargoLabelText.fontSize = 5.5f;
        cargoLabelText.alignment = TextAlignmentOptions.Left;
        cargoLabelText.raycastTarget = false;

        cargoValueText.fontStyle = FontStyles.Bold;
        cargoValueText.fontSize = 6.5f;
        cargoValueText.alignment = TextAlignmentOptions.Right;
        cargoValueText.raycastTarget = false;
        cargoValueText.gameObject.SetActive(true);

        if (cargoTrackImage != null)
        {
            SetCargoPanelRect(
                cargoTrackImage.rectTransform,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(5f, 3f),
                new Vector2(-4f, 8f)
            );
            cargoTrackImage.color = new Color(0.025f, 0.065f, 0.085f, 0.96f);
            cargoTrackImage.raycastTarget = false;
        }

        RectTransform fillAreaRect = slider != null && slider.fillRect != null
            ? slider.fillRect.parent as RectTransform
            : null;
        if (fillAreaRect != null)
        {
            SetCargoPanelRect(
                fillAreaRect,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(6f, 4f),
                new Vector2(-5f, 7f)
            );
        }
        else if (cargoFillImage != null)
        {
            SetCargoPanelRect(
                cargoFillImage.rectTransform,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(6f, 4f),
                new Vector2(-5f, 7f)
            );
        }

        if (cargoFillImage != null)
        {
            cargoFillImage.raycastTarget = false;
        }

        CargoGaugeTickGraphic tickGraphic = cargoRoot.GetComponentInChildren<CargoGaugeTickGraphic>(true);
        if (tickGraphic != null)
        {
            tickGraphic.gameObject.SetActive(false);
        }

        ResolveCargoCanvasGroup();
    }

    private static void SetCargoPanelRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void ApplySharedHudLayout()
    {
        SetCenteredRect(hpGauge != null ? hpGauge.transform as RectTransform : null,
            new Vector2(-184f, 123f), new Vector2(96f, 20f));
        SetCenteredRect(weaponHeatUI != null ? weaponHeatUI.transform as RectTransform : null,
            new Vector2(-186f, 110f), new Vector2(92f, 4f));

        RectTransform dashRect = dashIcon != null ? dashIcon.rectTransform.parent as RectTransform : null;
        SetCenteredRect(dashRect, new Vector2(-222f, 96f), new Vector2(20f, 20f));

        RectTransform statusEffectRect = statusEffectPresenter != null
            ? statusEffectPresenter.transform as RectTransform
            : null;
        SetCenteredRect(statusEffectRect, new Vector2(-210f, 96f), new Vector2(136f, 16f), new Vector2(0f, 0.5f));

        SetCenteredRect(menuHintRoot != null ? menuHintRoot.transform as RectTransform : null,
            new Vector2(-160f, -103f), new Vector2(96f, 24f));
        SetCenteredRect(reinforcementSlotUI != null ? reinforcementSlotUI.transform as RectTransform : null,
            new Vector2(-220f, -103f), new Vector2(28f, 28f));
    }

    private static void SetCenteredRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2? pivot = null)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

    private TextMeshProUGUI CreateRuntimeText(
        string objectName,
        RectTransform parent,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset resolvedFont = uiFont;
        if (resolvedFont == null)
        {
            TextMeshProUGUI source = GetComponentInChildren<TextMeshProUGUI>(true);
            resolvedFont = source != null ? source.font : null;
        }

        if (resolvedFont != null)
        {
            text.font = resolvedFont;
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRuntimeInset(RectTransform rect, float inset)
    {
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private void EnsureCoreTrackingPresentation()
    {
        if ((!createCoreTrackingPresentationIfMissing && coreTrackingObjectiveText == null && coreSignalCountText == null) ||
            objectiveRoot == null)
        {
            return;
        }

        RectTransform objectiveRect = objectiveRoot.transform as RectTransform;
        if (objectiveRect == null)
        {
            return;
        }

        TextMeshProUGUI fontSource = GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform panelRect = ResolveCoreTrackingPanel(objectiveRect);
        RectTransform textParent = panelRect != null ? panelRect : objectiveRect;

        if (panelRect != null)
        {
            panelRect.anchoredPosition = new Vector2(0f, 119f);
            panelRect.sizeDelta = new Vector2(154f, 28f);

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.025f, 0.075f, 0.11f, 0.82f);
                panelImage.raycastTarget = false;
            }

            ConfigureCoreSignalPips();
        }

        if (coreTrackingObjectiveText == null)
        {
            coreTrackingObjectiveText = CreateCoreTrackingText(
                "CoreTrackingObjective",
                textParent,
                fontSource,
                panelRect != null ? new Vector2(6f, 6f) : new Vector2(0f, 132f),
                new Vector2(132f, 10f),
                6f,
                TextAlignmentOptions.Center
            );
        }

        if (coreSignalCountText == null)
        {
            coreSignalCountText = CreateCoreTrackingText(
                "CoreTrackingCount",
                textParent,
                fontSource,
                panelRect != null ? new Vector2(23f, -6f) : new Vector2(43f, 119f),
                new Vector2(34f, 9f),
                6.5f,
                TextAlignmentOptions.Center
            );
        }

        ApplyHudFont(coreTrackingObjectiveText);
        ApplyHudFont(coreSignalCountText);
    }

    private RectTransform ResolveCoreTrackingPanel(RectTransform fallback)
    {
        if (coreSignalPips != null)
        {
            for (int i = 0; i < coreSignalPips.Length; i++)
            {
                Image pip = coreSignalPips[i];
                if (pip != null && pip.rectTransform.parent is RectTransform parent)
                {
                    return parent;
                }
            }
        }

        return fallback != null ? fallback.Find("BackGround") as RectTransform : null;
    }

    private void ConfigureCoreSignalPips()
    {
        if (coreSignalPips == null)
        {
            return;
        }

        for (int i = 0; i < coreSignalPips.Length; i++)
        {
            Image pip = coreSignalPips[i];
            if (pip == null)
            {
                continue;
            }

            pip.rectTransform.anchoredPosition = new Vector2(-31f + (i * 11f), -6f);
            pip.rectTransform.sizeDelta = new Vector2(8f, 8f);
            pip.preserveAspect = true;
            pip.raycastTarget = false;
        }
    }

    private static TextMeshProUGUI CreateCoreTrackingText(
        string objectName,
        RectTransform parent,
        TextMeshProUGUI fontSource,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = parent.gameObject.layer;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontSource != null)
        {
            text.font = fontSource.font;
        }

        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void EnsureMenuHintPresentation()
    {
        if (menuHintRoot != null || statusRoot == null)
        {
            ApplyHudFont(mapHintText);
            ApplyHudFont(inventoryHintText);
            return;
        }

        RectTransform parent = statusRoot.transform as RectTransform;
        if (parent == null)
        {
            return;
        }

        menuHintRoot = new GameObject("MenuKeyHints", typeof(RectTransform));
        menuHintRoot.layer = parent.gameObject.layer;
        RectTransform rootRect = menuHintRoot.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-160f, -103f);
        rootRect.sizeDelta = new Vector2(96f, 24f);

        mapHintText = CreateMenuKeyHint(rootRect, "MapHint", mapHintIcon, -24f);
        inventoryHintText = CreateMenuKeyHint(rootRect, "InventoryHint", inventoryHintIcon, 10);
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

    private TextMeshProUGUI CreateMenuKeyHint(RectTransform parent, string objectName, Sprite icon, float x)
    {
        GameObject iconObject = new GameObject($"{objectName}Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = parent.gameObject.layer;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(parent, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(x, 4.5f);
        iconRect.sizeDelta = new Vector2(9f, 9f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.color = new Color(0.72f, 0.92f, 1f, 0.92f);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(x, -6f);
        textRect.sizeDelta = new Vector2(46f, 8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = uiFont != null ? uiFont : GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        text.fontSize = 5.5f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 4.5f;
        text.fontSizeMax = 5.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color(0.88f, 0.94f, 1f, 0.94f);
        text.raycastTarget = false;
        return text;
    }

    private void ApplyHudFont(TextMeshProUGUI text)
    {
        if (text != null && uiFont != null)
        {
            text.font = uiFont;
        }
    }

    private void EnsureOperationPresentation()
    {
        if (operationRoot != null || !createOperationPresentationIfMissing ||
            operationPresentationShuttingDown || !isActiveAndEnabled)
        {
            return;
        }

        RectTransform objectiveRect = objectiveRoot != null
            ? objectiveRoot.transform as RectTransform
            : transform as RectTransform;
        if (objectiveRect == null)
        {
            return;
        }

        TextMeshProUGUI fontSource = GetComponentInChildren<TextMeshProUGUI>(true);
        GameObject root = new GameObject(
            "OperationStatus",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );
        root.layer = gameObject.layer;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(objectiveRect, false);
        bool useTutorialGuideLayout = disableObjectiveDirectorAutoResolution;
        rootRect.anchorMin = useTutorialGuideLayout ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = rootRect.anchorMin;
        rootRect.pivot = useTutorialGuideLayout ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = useTutorialGuideLayout ? new Vector2(0f, -14f) : new Vector2(0f, 91f);
        rootRect.sizeDelta = useTutorialGuideLayout ? new Vector2(214f, 44f) : new Vector2(210f, 40f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.055f, 0.88f);
        background.raycastTarget = false;

        operationAccentImage = CreateRuntimeImage(
            "Accent",
            rootRect,
            new Color(0.42f, 0.9f, 1f, 1f)
        );
        RectTransform accentRect = operationAccentImage.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.offsetMin = new Vector2(1f, 2f);
        accentRect.offsetMax = new Vector2(3f, -2f);

        operationCanvasGroup = root.GetComponent<CanvasGroup>();
        operationCanvasGroup.interactable = false;
        operationCanvasGroup.blocksRaycasts = false;

        operationTitleText = CreateOperationText(
            "Title",
            rootRect,
            fontSource,
            useTutorialGuideLayout ? new Vector2(7f, 25f) : new Vector2(5f, 24f),
            useTutorialGuideLayout ? new Vector2(-5f, -3f) : new Vector2(-4f, -2f),
            useTutorialGuideLayout ? 9f : 6.5f,
            FontStyles.Bold
        );
        operationDetailText = CreateOperationText(
            "Detail",
            rootRect,
            fontSource,
            useTutorialGuideLayout ? new Vector2(7f, 4f) : new Vector2(5f, 3f),
            useTutorialGuideLayout ? new Vector2(-6f, -18f) : new Vector2(-5f, -17f),
            useTutorialGuideLayout ? 7.5f : 5.5f,
            FontStyles.Normal
        );
        operationDetailText.color = new Color(0.86f, 0.92f, 0.98f, 1f);
        ApplyHudFont(operationTitleText);
        ApplyHudFont(operationDetailText);
        operationRoot = root;
        operationRoot.SetActive(false);
    }

    private static TextMeshProUGUI CreateOperationText(
        string objectName,
        RectTransform parent,
        TextMeshProUGUI fontSource,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = parent.gameObject.layer;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = offsetMin;
        textRect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontSource != null)
        {
            text.font = fontSource.font;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = objectName == "Detail" ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

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

        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged += HandleWalletChanged;
            RunManager.Instance.RunStarted += HandleRunStarted;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (playerHealth != null)
        {
            playerHealth.Changed -= HandleHealthChanged;
            playerHealth.InvincibilityChanged -= HandleInvincibilityChanged;
        }

        if (componentShield != null)
        {
            componentShield.ChargeStateChanged -= HandleShieldChargeStateChanged;
        }

        if (playerArmor != null)
        {
            playerArmor.Changed -= HandleArmorChanged;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
            playerDash.DashEnded -= HandleDashEnded;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            reinforcementController.ChargesChanged -= HandleReinforcementChargesChanged;
            reinforcementController.Used -= HandleReinforcementUsed;
            reinforcementController.ActiveTimedStatusesChanged -= HandleReinforcementTimedStatusesChanged;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
            cargoController.CargoFullRejected -= HandleCargoFullRejected;
            cargoController.LoadStateChanged -= HandleCargoLoadStateChanged;
        }

        if (coreTrackingController != null)
        {
            coreTrackingController.ProgressChanged -= HandleObjectiveProgressChanged;
            coreTrackingController.CoreRevealed -= HandleCoreRevealed;
        }
        else if (objectiveDirector != null)
        {
            objectiveDirector.ProgressChanged -= HandleObjectiveProgressChanged;
            objectiveDirector.CoreRevealedEvent -= HandleCoreRevealed;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged -= HandleWalletChanged;
            RunManager.Instance.RunStarted -= HandleRunStarted;
        }

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
        return ApplyCinematicVisibility(cinematicMode ? 0f : restoreFadeDuration);
    }

    private float ApplyCinematicVisibility(float restoreFadeDuration = 0f)
    {
        bool visible = !cinematicMode;
        ResolveCinematicCanvasGroup();
        KillCinematicVisibilityTween();

        if (!visible)
        {
            SetCanvasGroupVisible(false);
            ApplyAdditionalCinematicVisibility(false);
            RefreshBindingHints();
            return 0f;
        }

        float safeFadeDuration = Mathf.Max(0f, restoreFadeDuration);
        if (safeFadeDuration > 0.0001f &&
            cinematicCanvasGroup != null &&
            isActiveAndEnabled)
        {
            cinematicCanvasGroup.alpha = 0f;
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
        if (cinematicCanvasGroup != null)
        {
            return;
        }

        cinematicCanvasGroup = GetComponent<CanvasGroup>();
        if (cinematicCanvasGroup == null && addCinematicCanvasGroupIfMissing)
        {
            cinematicCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
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

        RefreshArmorBonusPosition();
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

        armorFillRect.anchorMin = new Vector2(hpRatio, 0.15f);
        armorFillRect.anchorMax = new Vector2(Mathf.Max(hpRatio, combinedRatio), 0.85f);
        armorFillRect.offsetMin = Vector2.zero;
        armorFillRect.offsetMax = Vector2.zero;
        if (armorFillImage != null)
        {
            armorFillImage.color = armorFillColor;
        }
        SetGameObjectVisible(armorFillRect.gameObject, visible && combinedRatio > hpRatio + 0.0001f);
    }


    private void RefreshArmorBonusPosition()
    {
        if (!anchorArmorBonusToHpText || hpValueText == null || armorBonusText == null)
        {
            return;
        }

        RectTransform hpRect = hpValueText.rectTransform;
        RectTransform armorRect = armorBonusText.rectTransform;

        if (hpRect.parent != armorRect.parent)
        {
            return;
        }

        LayoutGroup parentLayout = hpRect.parent.GetComponent<LayoutGroup>();
        if (parentLayout != null && parentLayout.isActiveAndEnabled)
        {
            return;
        }

        hpValueText.ForceMeshUpdate();
        Bounds textBounds = hpValueText.textBounds;
        float rightEdge = textBounds.size.x > 0.001f
            ? textBounds.max.x
            : hpRect.rect.xMax;
        float centerY = textBounds.size.y > 0.001f
            ? textBounds.center.y
            : hpRect.rect.center.y;

        armorRect.anchorMin = hpRect.anchorMin;
        armorRect.anchorMax = hpRect.anchorMax;
        armorRect.pivot = new Vector2(0f, 0.5f);
        armorRect.position = hpRect.TransformPoint(new Vector3(rightEdge, centerY, 0f));
        armorRect.anchoredPosition += armorBonusOffset;
    }

    private void RefreshObjectiveProgress()
    {
        EnsureCoreTrackingPresentation();
        coreTrackingController ??= FindFirstObjectByType<CoreTrackingSignalController>();

        if (coreTrackingController != null)
        {
            RefreshObjectiveProgress(
                coreTrackingController.CurrentSignalCount,
                coreTrackingController.RequiredSignalCount
            );
            return;
        }

        if (objectiveDirector == null && Application.isPlaying && !disableObjectiveDirectorAutoResolution)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }

        int current = objectiveDirector != null ? objectiveDirector.SignalCount : 0;
        int required = objectiveDirector != null ? objectiveDirector.SignalsRequiredToRevealCore : 2;
        RefreshObjectiveProgress(current, required);
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

    private void RefreshWallet(RunWallet wallet)
    {
        ResourceCounterUI[] counters =
        {
            creditsCounter,
            scrapCounter,
            coreShardCounter,
            stabilizedAlloyCounter,
            tuningChipCounter
        };
        for (int i = 0; i < counters.Length; i++)
        {
            counters[i]?.SetHideWhenZero(hideZeroResources);
        }

        creditsCounter?.SetAmount(wallet != null ? wallet.Credits : 0);
        scrapCounter?.SetAmount(wallet != null ? wallet.PendingScrapParts : 0);
        coreShardCounter?.SetAmount(wallet != null ? wallet.PendingCoreShards : 0);
        stabilizedAlloyCounter?.SetAmount(wallet != null ? wallet.PendingStabilizedAlloy : 0);
        tuningChipCounter?.SetAmount(wallet != null ? wallet.TuningChips : 0);
        RepackVisibleResourceCounters(counters);
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

        ResourceCounterUI anchorCounter = creditsCounter != null
            ? creditsCounter
            : scrapCounter != null
                ? scrapCounter
                : coreShardCounter;

        if (anchorCounter != null && anchorCounter.transform is RectTransform anchorRect)
        {
            resourceCounterOrigin = anchorRect.anchoredPosition;
            resourceCounterOriginCached = true;
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

    private void EnsureStabilizedAlloyCounter()
    {
        if (stabilizedAlloyCounter != null || scrapCounter == null)
        {
            return;
        }

        Transform counterParent = scrapCounter.transform.parent;
        stabilizedAlloyCounter = Instantiate(scrapCounter, counterParent);
        stabilizedAlloyCounter.name = "StabilizedAlloyCounter";
        stabilizedAlloyCounter.SetDisplayName("안정화 합금");
        stabilizedAlloyCounter.SetIconColor(stabilizedAlloyCounterColor);
        stabilizedAlloyCounter.SetHideWhenZero(hideZeroResources);

        if (stabilizedAlloyCounter.transform is RectTransform alloyRect)
        {
            RectTransform anchorRect = coreShardCounter != null
                ? coreShardCounter.transform as RectTransform
                : scrapCounter.transform as RectTransform;

            if (anchorRect != null)
            {
                alloyRect.anchoredPosition = anchorRect.anchoredPosition + Vector2.down * 12f;
            }
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

    private void ConfigureDashIcon()
    {
        if (dashCooldownFill == null)
        {
            return;
        }

        dashCooldownFill.type = Image.Type.Filled;
        dashCooldownFill.fillMethod = Image.FillMethod.Horizontal;
        dashCooldownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        dashCooldownFill.fillClockwise = true;
        dashCooldownFill.preserveAspect = true;
        dashCooldownFill.raycastTarget = false;

        if (dashIcon != null)
        {
            dashIcon.preserveAspect = true;
            dashIcon.raycastTarget = false;
        }
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
        if (cargoRoot == null)
        {
            return;
        }

        if (cargoCanvasGroup == null)
        {
            cargoCanvasGroup = cargoRoot.GetComponent<CanvasGroup>();
        }

        if (cargoCanvasGroup == null && Application.isPlaying)
        {
            cargoCanvasGroup = cargoRoot.AddComponent<CanvasGroup>();
        }

        if (cargoCanvasGroup != null)
        {
            cargoCanvasGroup.interactable = false;
            cargoCanvasGroup.blocksRaycasts = false;
        }
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
