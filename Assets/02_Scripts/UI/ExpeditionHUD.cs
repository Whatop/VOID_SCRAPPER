using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ExpeditionHUD : MonoBehaviour
{
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
    [SerializeField] private string mapHintFormat = "{0}";
    [SerializeField] private string inventoryHintFormat = "{0}";
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
    [SerializeField] private CargoGaugeTickGraphic cargoTickGraphic;
    [SerializeField, Min(1)] private int cargoTickInterval = 25;
    [SerializeField] private Color cargoTickColor = new Color(0.86f, 0.9f, 0.94f, 0.48f);
    [SerializeField] private string cargoValueFormat = "{0}/{1}";
    [SerializeField] private Color cargoNormalColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color cargoWarningColor = new Color(1f, 0.75f, 0.18f, 1f);
    [SerializeField] private Color cargoFullColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float cargoWarningRatio = 0.8f;

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

    [Header("Messages")]
    [SerializeField] private WarningMessageUI warningMessageUI;

    private bool cinematicMode;
    private bool subscribed;
    private bool[] additionalObjectVisibilityBeforeCinematic;
    private InputAction moveAction;
    private Sequence operationBriefingSequence;
    private RectTransform operationBriefingRect;
    private Vector2 operationBriefingBasePosition;
    private bool operationBriefingPending;
    private bool operationBriefingPresented;
    private bool resourceCounterOriginCached;
    private Vector2 resourceCounterOrigin;

    public bool IsCinematicMode => cinematicMode;

    private void Awake()
    {
        ResolveReferences();
        EnsureCoreTrackingPresentation();
        EnsureMenuHintPresentation();
        EnsureStabilizedAlloyCounter();
        CacheResourceCounterOrigin();
        EnsureCargoTickPresentation();
        ResolveCinematicCanvasGroup();
        SetCanvasGroupVisible(true);
        ConfigureDashIcon();
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputBindingPersistence.LoadOnce(inputActions);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureCoreTrackingPresentation();
        EnsureMenuHintPresentation();
        EnsureStabilizedAlloyCounter();
        CacheResourceCounterOrigin();
        EnsureCargoTickPresentation();
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
        Subscribe();
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
        InputSystem.onActionChange -= HandleInputActionChange;
        GameSettingsRuntime.Changed -= HandleGameSettingsChanged;
        UnbindOperationMovement();
        KillOperationBriefingTween();
    }

    private void Update()
    {
        if (cinematicMode)
        {
            return;
        }

        UpdateDashDisplay();
        UpdateReinforcementSlot();
        UpdateCargoDisplay();
    }

    public void SetCinematicMode(bool enabled)
    {
        if (cinematicMode == enabled)
        {
            return;
        }

        if (enabled)
        {
            CaptureAdditionalObjectVisibility();
        }

        cinematicMode = enabled;
        ApplyCinematicVisibility();
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
        EnsureOperationPresentation();
        if (operationRoot == null)
        {
            return;
        }

        if (!visible)
        {
            operationBriefingPending = false;
            operationBriefingPresented = false;
            UnbindOperationMovement();
            KillOperationBriefingTween();
            operationRoot.SetActive(false);
            return;
        }

        if (operationTitleText != null)
        {
            operationTitleText.text = string.IsNullOrWhiteSpace(title) ? "작전 수신" : $"작전 수신 · {title}";
            operationTitleText.color = accentColor;
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
        if (operationRoot == null || operationCanvasGroup == null)
        {
            return;
        }

        KillOperationBriefingTween();
        operationRoot.SetActive(true);
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

            operationRoot.SetActive(false);
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

        operationCanvasGroup?.DOKill();
    }

    private void ResolveReferences()
    {
        playerHealth ??= FindFirstObjectByType<PlayerHealth>();
        playerArmor ??= FindFirstObjectByType<PlayerArmor>();
        playerDash ??= FindFirstObjectByType<PlayerDash>();
        reinforcementController ??= FindFirstObjectByType<PlayerReinforcementController>();
        reinforcementSlotUI ??= FindFirstObjectByType<ReinforcementSlotUI>();
        statusEffectPresenter ??= FindFirstObjectByType<StatusEffectHUDPresenter>(FindObjectsInactive.Include);
        cargoController ??= FindFirstObjectByType<PlayerCargoController>();
        weaponHeatUI ??= FindFirstObjectByType<WeaponHeatUI>(FindObjectsInactive.Include);
        warningMessageUI ??= FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        coreTrackingController ??= FindFirstObjectByType<CoreTrackingSignalController>();
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);

        if (coreTrackingController == null && objectiveDirector == null && Application.isPlaying && !disableObjectiveDirectorAutoResolution)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }
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
        rootRect.anchoredPosition = new Vector2(-207f, -113f);
        rootRect.sizeDelta = new Vector2(58f, 24f);

        mapHintText = CreateMenuKeyHint(rootRect, "MapHint", mapHintIcon, -14f);
        inventoryHintText = CreateMenuKeyHint(rootRect, "InventoryHint", inventoryHintIcon, 14f);
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
        textRect.sizeDelta = new Vector2(27f, 8f);

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
        if (operationRoot != null || !createOperationPresentationIfMissing || objectiveRoot == null)
        {
            return;
        }

        RectTransform objectiveRect = objectiveRoot.transform as RectTransform;
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
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 78f);
        rootRect.sizeDelta = new Vector2(190f, 42f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.055f, 0.72f);
        background.raycastTarget = false;

        operationCanvasGroup = root.GetComponent<CanvasGroup>();
        operationCanvasGroup.interactable = false;
        operationCanvasGroup.blocksRaycasts = false;

        operationTitleText = CreateOperationText(
            "Title",
            rootRect,
            fontSource,
            new Vector2(5f, 24f),
            new Vector2(-4f, -2f),
            6.5f,
            FontStyles.Bold
        );
        operationDetailText = CreateOperationText(
            "Detail",
            rootRect,
            fontSource,
            new Vector2(5f, 3f),
            new Vector2(-5f, -17f),
            5.5f,
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

    private void ApplyCinematicVisibility()
    {
        bool visible = !cinematicMode;
        ResolveCinematicCanvasGroup();
        SetCanvasGroupVisible(visible);

        if (additionalObjectsToHideDuringCinematic != null)
        {
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

        RefreshBindingHints();
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

    private void HandleRunStarted(RunContext _) => RefreshAll();
    private void HandleWalletChanged(RunWallet wallet) => RefreshWallet(wallet);
    private void HandleHealthChanged(float current, float max) => RefreshHealthAndArmor(
        current,
        max,
        playerArmor != null ? playerArmor.CurrentArmor : 0f,
        playerArmor != null ? playerArmor.MaxArmor : 1f
    );

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
    private void HandleCargoChanged(int _, int __) => UpdateCargoDisplay();
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
    }

    private void RefreshArmorFill(float hpRatio, float combinedRatio, bool visible)
    {
        if (armorFillRect == null)
        {
            return;
        }

        armorFillRect.anchorMin = new Vector2(hpRatio, 0f);
        armorFillRect.anchorMax = new Vector2(Mathf.Max(hpRatio, combinedRatio), 1f);
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
        UpdateCargoDisplay();
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

        int safeCapacity = Mathf.Max(1, capacity);
        float ratio = Mathf.Clamp01(current / (float)safeCapacity);
        Color color = ratio >= 0.999f
            ? cargoFullColor
            : ratio >= cargoWarningRatio ? cargoWarningColor : cargoNormalColor;

        cargoGauge?.SetValue(current, safeCapacity);
        cargoGauge?.SetText(string.Format(cargoValueFormat, current, capacity));
        cargoGauge?.SetFillColor(color);
        EnsureCargoTickPresentation();
        cargoTickGraphic?.Configure(capacity, cargoTickInterval, cargoTickColor);

        if (cargoValueText != null)
        {
            cargoValueText.text = string.Format(cargoValueFormat, current, capacity);
            cargoValueText.color = color;
        }
    }

    private void EnsureCargoTickPresentation()
    {
        if (cargoTickGraphic != null || cargoGauge == null)
        {
            return;
        }

        RectTransform gaugeRect = cargoGauge.transform as RectTransform;
        if (gaugeRect == null)
        {
            return;
        }

        GameObject tickObject = new GameObject(
            "CargoCapacityTicks",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(CargoGaugeTickGraphic)
        );
        tickObject.layer = gaugeRect.gameObject.layer;
        RectTransform tickRect = tickObject.GetComponent<RectTransform>();
        tickRect.SetParent(gaugeRect, false);
        tickRect.anchorMin = Vector2.zero;
        tickRect.anchorMax = Vector2.one;
        tickRect.offsetMin = Vector2.zero;
        tickRect.offsetMax = Vector2.zero;
        tickRect.SetAsLastSibling();
        cargoTickGraphic = tickObject.GetComponent<CargoGaugeTickGraphic>();
        cargoTickGraphic.raycastTarget = false;
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
        SetGameObjectVisible(menuHintRoot, visible);

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
