using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum PassiveStorageListMode
{
    OwnedOnly,
    CatalogWithOwnedCount
}

public enum PassiveStorageSortMode
{
    AcquiredOrder,
    RarityThenName,
    Name
}

public enum BuildStatusFieldDropTarget
{
    Active,
    Passive,
    Cargo
}

[Serializable]
public sealed class CargoManifestRowUI
{
    [SerializeField] private CurrencyType currencyType;
    [SerializeField] private GameObject root;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI primaryText;
    [SerializeField] private TextMeshProUGUI secondaryText;
    private Action<CurrencyType> selected;

    public CurrencyType CurrencyType => currencyType;
    public Selectable Selectable => button;
    public bool IsVisible => root != null && root.activeSelf;
    public bool IsConfigured => root != null &&
                                button != null &&
                                backgroundImage != null &&
                                iconImage != null &&
                                primaryText != null &&
                                secondaryText != null;

    public void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    public void Bind(Action<CurrencyType> onSelected)
    {
        selected = onSelected;
        button.onClick.RemoveListener(HandleClicked);
        button.onClick.AddListener(HandleClicked);
    }

    public void Unbind()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }

        selected = null;
    }

    public void ConfigureTypography(TMP_FontAsset font)
    {
        ConfigureText(primaryText, font);
        ConfigureText(secondaryText, font);
    }

    public void Refresh(
        Sprite icon,
        string displayName,
        int currentAmount,
        int unitWeight,
        int totalContribution,
        bool usesCargo,
        bool autoPickupEnabled,
        bool isSelected)
    {
        int amount = Mathf.Max(0, currentAmount);
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        primaryText.text = displayName;
        secondaryText.text = usesCargo
            ? $"{amount}개\n<size=4.2>적재 {totalContribution} · 자동 회수 {(autoPickupEnabled ? "켬" : "끔")}</size>"
            : $"{amount}개\n<size=4.2>임시 자원</size>";
        bool hasAmount = amount > 0;
        backgroundImage.color = isSelected
            ? hasAmount
                ? new Color(0.06f, 0.24f, 0.3f, 0.98f)
                : new Color(0.035f, 0.13f, 0.16f, 0.88f)
            : hasAmount
                ? new Color(0.035f, 0.075f, 0.1f, 0.96f)
                : new Color(0.025f, 0.045f, 0.06f, 0.72f);

        Color primaryColor = hasAmount
            ? new Color(0.9f, 0.97f, 1f, 1f)
            : new Color(0.58f, 0.66f, 0.7f, 0.62f);
        Color secondaryColor = hasAmount
            ? new Color(0.64f, 0.86f, 0.92f, 1f)
            : new Color(0.46f, 0.54f, 0.58f, 0.55f);
        primaryText.color = primaryColor;
        secondaryText.color = secondaryColor;
        iconImage.color = hasAmount
            ? Color.white
            : new Color(0.65f, 0.72f, 0.76f, 0.42f);
    }

    private void HandleClicked()
    {
        selected?.Invoke(currencyType);
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        TMP_FontAsset font)
    {
        if (text == null)
        {
            return;
        }

        if (font != null)
        {
            text.font = font;
        }

        text.raycastTarget = false;
    }
}

[DisallowMultipleComponent]
public class PlayerBuildStatusPanelUI : MonoBehaviour
{
    [Serializable]
    public sealed class StoryRecoverySlot
    {
        [SerializeField] private BossStoryPart part;
        [SerializeField] private BossCampaignDefinition definition;
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image acquiredHighlight;

        private Sequence feedbackTween;
        private Vector3 feedbackBaseScale;

        public BossStoryPart Part => part;

        public void Refresh(PermanentProgress progress)
        {
            bool acquired = progress != null && progress.HasBossStoryPart(part);
            Sprite icon = definition != null ? definition.StoryPartSprite : null;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
            string nameKey = part switch
            {
                BossStoryPart.SectorStabilizer => "ui.story_recovery.sector_stabilizer",
                BossStoryPart.MatterCompressor => "ui.story_recovery.matter_compressor",
                BossStoryPart.PhaseNavigationLens => "ui.story_recovery.phase_navigation_lens",
                _ => string.Empty
            };
            if (nameText != null)
            {
                nameText.text = ResolveStoryText(nameKey, CampaignProgressionCatalog.GetStoryPartDisplayName(part));
            }
            if (statusText != null)
            {
                statusText.text = acquired
                    ? ResolveStoryText("ui.story_recovery.acquired", "획득")
                    : ResolveStoryText("ui.story_recovery.unacquired", "미획득");
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = acquired ? 1f : 0.55f;
            }
            if (acquiredHighlight != null)
            {
                acquiredHighlight.enabled = acquired;
            }
        }

        public void Pulse()
        {
            StopFeedback();
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }
            feedbackBaseScale = root.localScale;
            try
            {
                feedbackTween = DOTween.Sequence().SetUpdate(true)
                    .SetLink(root.gameObject, LinkBehaviour.KillOnDisable);
                feedbackTween.Append(root.DOScale(feedbackBaseScale * 1.08f, 0.12f).SetEase(Ease.OutQuad));
                feedbackTween.Append(root.DOScale(feedbackBaseScale, 0.2f).SetEase(Ease.OutQuad));
                feedbackTween.OnKill(RestoreFeedbackScale);
            }
            catch (Exception)
            {
                StopFeedback();
            }
        }

        public void StopFeedback()
        {
            if (feedbackTween == null)
            {
                return;
            }
            feedbackTween.Kill();
            RestoreFeedbackScale();
        }

        private void RestoreFeedbackScale()
        {
            feedbackTween = null;
            if (root != null)
            {
                root.localScale = feedbackBaseScale;
            }
        }
    }

    [Header("Read-only Story Recovery")]
    [SerializeField] private TextMeshProUGUI storyRecoveryTitle;
    [SerializeField] private StoryRecoverySlot[] storyRecoverySlots = Array.Empty<StoryRecoverySlot>();

    [Serializable]
    private sealed class TraitFlavorOverride
    {
        [SerializeField] private string traitId;
        [TextArea(2, 4)]
        [SerializeField] private string flavorText;

        public string TraitId => traitId;
        public string FlavorText => flavorText;
    }

    private sealed class PassiveEntry
    {
        public TraitDefinition trait;
        public int permanentLevel;
        public int runtimeLevel;
        public int acquisitionOrder;

        public bool IsOwned => permanentLevel > 0 || runtimeLevel > 0;
        public int DisplayLevel => Mathf.Max(permanentLevel, runtimeLevel);
        public int OwnedAmount => IsOwned ? 1 : 0;
    }

    private sealed class TraitEffectSummary
    {
        public readonly List<TraitEffectType> order = new List<TraitEffectType>();
        public readonly Dictionary<TraitEffectType, float> values = new Dictionary<TraitEffectType, float>();

        public void Add(TraitEffectType effectType, float value)
        {
            if (!values.ContainsKey(effectType))
            {
                values.Add(effectType, 0f);
                order.Add(effectType);
            }

            values[effectType] += value;
        }
    }

    [Header("Root")]
    [Tooltip("실제 패널 비주얼 루트입니다. 가능하면 이 스크립트의 자식 오브젝트를 연결하세요.")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool deactivateVisualRootWhenClosed = true;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("External Menu Ownership")]
    [Tooltip("켜면 ExpeditionMenuController가 열기/닫기, Pause, Cursor, ESC를 전담합니다.")]
    [SerializeField] private bool externalMenuControlsLifecycle = true;

    [Header("Input")]
    [SerializeField] private bool holdTabToOpen = true;
    [SerializeField] private Key fallbackKey = Key.Tab;
    [SerializeField] private bool pauseWhileOpen = true;
    [SerializeField] private bool blockOpenWhileAnotherPauseActive = true;

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhileOpen = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    [Header("Fixed Ship Slot")]
    [SerializeField] private Image shipPreviewImage;
    [SerializeField] private TextMeshProUGUI shipNameText;
    [SerializeField] private TextMeshProUGUI shipWeaponText;
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private TextMeshProUGUI coreSignalValueText;
    [SerializeField] private TextMeshProUGUI cargoValueText;
    [SerializeField] private TextMeshProUGUI tuningChipValueText;
    [SerializeField] private TextMeshProUGUI emergencyReturnValueText;
    [SerializeField] private string tuningChipValueFormat = "{0}개";
    [SerializeField] private TextMeshProUGUI shipDescriptionText;
    [SerializeField] private TextMeshProUGUI shipPassiveText;
    [SerializeField] private TextMeshProUGUI shipTipText;
    [SerializeField] private Color normalStatColor = Color.white;
    [SerializeField] private Color coreReadyColor = new Color(0.35f, 1f, 0.45f, 1f);
    [TextArea(2, 4)]
    [SerializeField] private string coreTrackingTip = "코어 추적 신호를 수집해 코어 위치를 추적하십시오.\n긴급복귀 시 보존 한도를 초과한 적재물은 손실됩니다.";
    [TextArea(2, 4)]
    [SerializeField] private string coreReadyTip = "코어 위치가 공개되었습니다.\n보스전에 진입하기 전에 체력과 적재량을 확인하십시오.";
    [TextArea(2, 4)]
    [SerializeField] private string region3InvestigationTip =
        "확인되지 않은 위상 신호 위치를 조사하십시오.\n지도와 레이더의 미확인 표식을 추적하십시오.";

    [Header("Fixed Active Slot")]
    [SerializeField] private GameObject activeEquippedRoot;
    [SerializeField] private GameObject activeEmptyRoot;
    [SerializeField] private Image activeIconImage;
    [SerializeField] private TextMeshProUGUI activeNameText;
    [SerializeField] private TextMeshProUGUI activeDescriptionText;
    [SerializeField] private TextMeshProUGUI activeEffectText;
    [SerializeField] private TextMeshProUGUI activeCooldownText;
    [SerializeField] private TextMeshProUGUI activeChargeText;
    [SerializeField] private TextMeshProUGUI activeStateText;
    [SerializeField] private Slider activeChargeSlider;
    [SerializeField] private Image activeChargeFillImage;
    [SerializeField] private Color activeReadyColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color activeChargingColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color activeUnavailableColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private bool showDefaultActiveOutsideRun = true;
    [SerializeField] private string fallbackDefaultReinforcementId = "rf_emergency_return_anchor";

    [Header("Field Drop")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string fieldDropActionName = "Dismantle";
    [Tooltip("입력 액션이 없을 때 사용하는 필드 드랍 키입니다.")]
    [SerializeField] private Key fieldDropKey = Key.G;
    [Tooltip("액티브 슬롯 전체에 Button을 붙이고 연결하면 클릭으로 액티브를 드랍 대상으로 선택할 수 있습니다.")]
    [SerializeField] private Button activeSlotSelectButton;
    [SerializeField] private Button activeFieldDropButton;
    [SerializeField] private Button passiveFieldDropButton;
    [SerializeField] private TextMeshProUGUI fieldDropHintText;
    [SerializeField] private GameObject activeDropSelectedIndicator;
    [SerializeField] private TraitPickup traitDropPickupPrefab;
    [SerializeField] private RewardPickup cargoDropPickupPrefab;
    [SerializeField] private Transform fieldDropOrigin;
    [SerializeField] private float fieldDropDistance = 1.25f;
    [SerializeField] private float fieldDropBlockSeconds = 0.5f;
    [SerializeField] private Vector2 fallbackFieldDropDirection = Vector2.down;

    [Header("Cargo Jettison")]
    [SerializeField, Min(0.1f)] private float cargoJettisonHoldDuration = 0.65f;
    [SerializeField, Min(1)] private int scrapJettisonAmount = 5;
    [SerializeField, Min(1)] private int rareCargoJettisonAmount = 1;
    [SerializeField, Min(0.1f)] private float cargoJettisonDistance = 2f;
    [SerializeField, Min(0f)] private float cargoRepickupBlockSeconds = 0.85f;

    [Header("Prefab Cargo Layout")]
    [SerializeField] private RectTransform cargoManagementRoot;
    [SerializeField] private TextMeshProUGUI cargoLoadText;
    [SerializeField] private Slider cargoLoadSlider;
    [SerializeField] private List<CargoManifestRowUI> cargoManifestRows = new List<CargoManifestRowUI>();
    [SerializeField] private Button cargoShowAllButton;
    [SerializeField] private TextMeshProUGUI cargoShowAllText;
    [SerializeField] private GameObject cargoDetailActionsRoot;
    [SerializeField] private Image selectedCargoIconImage;
    [SerializeField] private TextMeshProUGUI selectedCargoNameText;
    [SerializeField] private TextMeshProUGUI selectedCargoStatsText;
    [SerializeField] private TextMeshProUGUI selectedCargoQuantityText;
    [SerializeField] private TextMeshProUGUI selectedCargoEmptyText;
    [SerializeField] private TextMeshProUGUI selectedCargoAutoPickupText;
    [SerializeField] private Slider cargoQuantitySlider;
    [SerializeField] private Button cargoQuantityOneButton;
    [SerializeField] private Button cargoQuantityHalfButton;
    [SerializeField] private Button cargoQuantityMaxButton;
    [SerializeField] private Button cargoJettisonButton;
    [SerializeField] private Button cargoAutoPickupButton;

    [Header("Passive Storage")]
    [SerializeField] private PassiveStorageListMode passiveListMode = PassiveStorageListMode.OwnedOnly;
    [SerializeField] private PassiveStorageSortMode passiveSortMode = PassiveStorageSortMode.AcquiredOrder;
    [SerializeField] private bool includePermanentActiveTraits = true;
    [SerializeField] private bool includeRuntimeTraits = true;
    [SerializeField] private bool includeHiddenTraits;
    [SerializeField] private int passiveColumnCount = 6;
    [SerializeField] private string ownedSlotLabelFormat = "Lv.{0}";
    [SerializeField] private string unownedSlotLabel = "x0";
    [SerializeField] private ScrollRect passiveScrollRect;
    [FormerlySerializedAs("contentRoot")]
    [SerializeField] private RectTransform passiveContentRoot;
    [FormerlySerializedAs("slotPrefab")]
    [SerializeField] private BuildStatusSlotButtonUI passiveSlotPrefab;
    [FormerlySerializedAs("gridLayoutGroup")]
    [SerializeField] private GridLayoutGroup passiveGridLayoutGroup;
    [SerializeField] private ContentSizeFitter passiveContentSizeFitter;
    [SerializeField] private TextMeshProUGUI passiveCountText;
    [SerializeField] private GameObject passiveEmptyRoot;
    [SerializeField] private bool resetScrollPositionOnOpen = true;

    [Header("Selected Passive Detail")]
    [SerializeField] private GameObject selectedPassiveRoot;
    [SerializeField] private GameObject selectedPassiveEmptyRoot;
    [FormerlySerializedAs("selectedIconImage")]
    [SerializeField] private Image selectedPassiveIconImage;
    [SerializeField] private TextMeshProUGUI selectedPassiveNameText;
    [SerializeField] private TextMeshProUGUI selectedPassiveCategoryText;
    [SerializeField] private TextMeshProUGUI selectedPassiveRarityText;
    [SerializeField] private Image selectedPassiveRarityBadgeImage;
    [SerializeField] private TextMeshProUGUI selectedPassiveOwnedText;
    [SerializeField] private TextMeshProUGUI selectedPassiveLevelText;
    [FormerlySerializedAs("descriptionText")]
    [SerializeField] private TextMeshProUGUI selectedPassiveDescriptionText;
    [SerializeField] private TextMeshProUGUI selectedPassiveEffectText;
    [SerializeField] private GameObject selectedPassiveFlavorRoot;
    [SerializeField] private TextMeshProUGUI selectedPassiveFlavorText;
    [SerializeField] private List<TraitFlavorOverride> traitFlavorOverrides = new List<TraitFlavorOverride>();

    [Header("References")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerShipVisualController shipVisualController;

    [Header("Catalogs")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeInspectorTraitDefinitions = true;
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private bool includeInspectorReinforcementDefinitions = true;
    [SerializeField] private List<ReinforcementDefinition> reinforcementDefinitions = new List<ReinforcementDefinition>();

    [Header("Fallback")]
    [SerializeField] private Sprite fallbackShipIcon;
    [SerializeField] private Sprite fallbackTraitIcon;
    [SerializeField] private Sprite fallbackReinforcementIcon;
    [TextArea(2, 4)]
    [SerializeField] private string fallbackShipDescription = "기체 설명이 없습니다.";

    private readonly List<PassiveEntry> passiveEntries = new List<PassiveEntry>();
    private readonly List<BuildStatusSlotButtonUI> passiveSlotInstances = new List<BuildStatusSlotButtonUI>();
    private readonly List<TraitDefinition> resolvedTraits = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> resolvedReinforcements = new List<ReinforcementDefinition>();
    private readonly Dictionary<string, PassiveEntry> passiveEntryById = new Dictionary<string, PassiveEntry>();

    private bool isOpen;
    private bool suppressOpenUntilKeyReleased;
    private bool runtimeEventsBound;
    private InputAction fieldDropAction;
    private int selectedPassiveIndex = -1;
    private string selectedPassiveId;
    private BuildStatusFieldDropTarget selectedFieldDropTarget = BuildStatusFieldDropTarget.Active;
    private CurrencyType selectedCargoType = CurrencyType.ScrapParts;
    private float cargoJettisonHoldTimer;
    private bool cargoJettisonConsumedUntilRelease;
    private int selectedCargoJettisonAmount = 1;
    private bool showAllCargoResources;
    private bool hasSelectedCargo;

    private bool storedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private ExpeditionObjectiveDirector subscribedObjectiveDirector;
    private CoreTrackingSignalController subscribedCoreTrackingController;
    private RunRuntimeTraitStore subscribedRuntimeTraitStore;
    private PermanentProgress subscribedPermanentProgress;
    private RunManager subscribedRunManager;
    private VoidScrapperLocalizationService subscribedStoryLocalization;

    public bool IsOpen => isOpen;
    public Selectable FirstCargoSelectable
    {
        get
        {
            if (cargoManifestRows == null)
            {
                return null;
            }

            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                CargoManifestRowUI row = cargoManifestRows[i];
                Selectable selectable = row?.Selectable;

                if (hasSelectedCargo &&
                    row != null &&
                    row.CurrencyType == selectedCargoType &&
                    selectable != null &&
                    selectable.IsActive() &&
                    selectable.IsInteractable())
                {
                    return selectable;
                }
            }

            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                Selectable selectable = cargoManifestRows[i]?.Selectable;

                if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                {
                    return selectable;
                }
            }

            return cargoShowAllButton != null &&
                   cargoShowAllButton.IsActive() &&
                   cargoShowAllButton.IsInteractable()
                ? cargoShowAllButton
                : null;
        }
    }
    public bool ExternalMenuControlsLifecycle => externalMenuControlsLifecycle;

    public event Action CloseRequested;

    private void OnValidate()
    {
        passiveColumnCount = Mathf.Max(1, passiveColumnCount);
    }

    private void Awake()
    {
        if (root == null)
        {
            // Legacy standalone panels may use their own GameObject as the visual
            // root. A scene-level HUD canvas is not a panel, however: claiming it
            // here would add a CanvasGroup and hide every HUD child when this panel
            // initializes closed.
            root = GetComponent<Canvas>() == null ? gameObject : null;
        }

        if (canvasGroup == null && root != null)
        {
            canvasGroup = root.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && root != null)
        {
            canvasGroup = root.AddComponent<CanvasGroup>();
        }

        ResolveReferences();
        BindCargoManagementLayout();
        ConfigureInventoryTypography();
        ConfigureCargoTypography();
        ConfigurePassiveScrollView();
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        StopStoryRecoveryFeedback();
        UnbindRuntimeEvents();
        UnbindCargoManagementLayout();
    }

    private void OnEnable()
    {
        BindFieldDropInput();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseFromButton);
        }

        if (activeSlotSelectButton != null)
        {
            activeSlotSelectButton.onClick.AddListener(SelectActiveForFieldDrop);
        }

        if (activeFieldDropButton != null)
        {
            activeFieldDropButton.onClick.AddListener(TryDropActiveFieldItem);
        }

        if (passiveFieldDropButton != null)
        {
            passiveFieldDropButton.onClick.AddListener(TryDropSelectedPassiveFieldItem);
        }

    }

    private void OnDisable()
    {
        StopStoryRecoveryFeedback();
        UnbindRuntimeEvents();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseFromButton);
        }

        if (activeSlotSelectButton != null)
        {
            activeSlotSelectButton.onClick.RemoveListener(SelectActiveForFieldDrop);
        }

        if (activeFieldDropButton != null)
        {
            activeFieldDropButton.onClick.RemoveListener(TryDropActiveFieldItem);
        }

        if (passiveFieldDropButton != null)
        {
            passiveFieldDropButton.onClick.RemoveListener(TryDropSelectedPassiveFieldItem);
        }

        if (isOpen)
        {
            CloseInternal(false, true);
        }
    }

    private void Update()
    {
        if (externalMenuControlsLifecycle)
        {
            if (isOpen)
            {
                HandleFieldDropInput();
            }

            return;
        }

        bool keyPressed = IsOpenKeyPressed();

        if (suppressOpenUntilKeyReleased)
        {
            if (!keyPressed)
            {
                suppressOpenUntilKeyReleased = false;
            }

            return;
        }

        if (holdTabToOpen)
        {
            if (keyPressed && !isOpen)
            {
                Open();
            }
            else if (!keyPressed && isOpen)
            {
                CloseInternal(true, true);
            }
        }
        else if (WasOpenKeyPressedThisFrame())
        {
            Toggle();
        }

        if (isOpen)
        {
            HandleFieldDropInput();
        }
    }

    public void Toggle()
    {
        if (isOpen)
        {
            CloseInternal(true, true);
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        if (!externalMenuControlsLifecycle &&
            blockOpenWhileAnotherPauseActive &&
            GameplayPauseManager.IsPaused &&
            !GameplayPauseManager.Instance.IsPausedBy(this))
        {
            return;
        }

        isOpen = true;

        if (root != null && root != gameObject)
        {
            root.SetActive(true);
        }

        ResolveReferences();
        ResolveCatalogs();
        ConfigurePassiveScrollView();
        selectedFieldDropTarget = reinforcementController != null && reinforcementController.HasEquipment
            ? BuildStatusFieldDropTarget.Active
            : BuildStatusFieldDropTarget.Passive;
        BindRuntimeEvents();

        if (!externalMenuControlsLifecycle)
        {
            StoreAndApplyCursorState();
        }

        SetPanelVisible(true);
        RefreshAll();

        if (!externalMenuControlsLifecycle)
        {
            if (pauseWhileOpen)
            {
                GameplayPauseManager.Instance.PushPause(this, "PlayerBuildStatusPanel");
            }

            GameplayPauseManager.Instance.RegisterCancelHandler(this, CloseFromCancel);
            AudioManager.Play(SoundEventIds.UiPanelOpen);
        }
    }

    public void Close()
    {
        CloseInternal(true, true);
    }

    public void RefreshAll()
    {
        if (!isOpen)
        {
            return;
        }

        RefreshShipSection();
        RefreshActiveSection();
        RebuildPassiveSection();
        RefreshStoryRecovery();
    }

    public void RefreshStoryRecovery()
    {
        SetText(storyRecoveryTitle, ResolveStoryText("ui.story_recovery.title", "스토리 회수품"));
        for (int i = 0; i < storyRecoverySlots.Length; i++)
        {
            storyRecoverySlots[i]?.Refresh(PermanentProgress.Instance);
        }
    }

    // Visual completion notification only. Ownership was committed before the
    // world flight; this method never opens the menu or changes progression.
    public void PresentStoryPartAcquired(BossStoryPart part)
    {
        if (!isOpen || !isActiveAndEnabled || PermanentProgress.Instance == null ||
            !PermanentProgress.Instance.HasBossStoryPart(part))
        {
            return;
        }
        RefreshStoryRecovery();
        for (int i = 0; i < storyRecoverySlots.Length; i++)
        {
            StoryRecoverySlot slot = storyRecoverySlots[i];
            if (slot != null && slot.Part == part)
            {
                slot.Pulse();
            }
        }
    }

    private void StopStoryRecoveryFeedback()
    {
        for (int i = 0; i < storyRecoverySlots.Length; i++)
        {
            storyRecoverySlots[i]?.StopFeedback();
        }
    }

    private static string ResolveStoryText(string key, string fallback)
    {
        VoidScrapperLocalizationService service = VoidScrapperLocalizationService.Instance;
        return service != null && service.Catalog != null &&
               service.Catalog.TryGetText(key, service.CurrentLanguageCode, out string text, out _)
            ? text : fallback;
    }

    public void RefreshShipSection()
    {
        RunContext run = ResolveRunContext();
        ShipDefinition ship = FindShip(run != null ? run.SelectedShipId : null);
        WeaponTreeType weaponTree = ResolveWeaponTree(run, ship);

        // Tab 기체 슬롯은 월드용 무기 스프라이트가 아니라 ShipDefinition의 전용 PreviewSprite를 사용합니다.
        Sprite shipSprite = ship != null ? ship.PreviewSprite : null;

        SetImage(shipPreviewImage, shipSprite != null ? shipSprite : fallbackShipIcon);
        SetText(shipNameText, "탐사 인벤토리");
        SetText(shipWeaponText, string.Empty);
        SetText(shipDescriptionText, ship != null ? ship.Description : fallbackShipDescription);
        SetText(shipPassiveText, ship != null ? ship.PassiveDescription : string.Empty);

        float currentHp = playerHealth != null
            ? playerHealth.CurrentHp
            : ship != null ? ship.MaxHp : 0f;

        float maxHp = playerHealth != null
            ? playerHealth.MaxHp
            : ship != null ? ship.MaxHp : 0f;

        SetText(hpValueText, $"{currentHp:0}/{maxHp:0}");
        SetColor(hpValueText, normalStatColor);

        CoreTrackingSignalController coreTracking = FindFirstObjectByType<CoreTrackingSignalController>();
        ExpeditionObjectiveDirector objectiveDirector = coreTracking == null
            ? ExpeditionObjectiveDirector.Instance
            : null;
        int signalCount = coreTracking != null
            ? coreTracking.CurrentSignalCount
            : objectiveDirector != null
                ? objectiveDirector.SignalCount
                : run != null ? run.ObjectiveSignalCount : 0;

        int signalRequired = coreTracking != null
            ? coreTracking.RequiredSignalCount
            : objectiveDirector != null
                ? objectiveDirector.SignalsRequiredToRevealCore
                : 2;

        bool coreReady = coreTracking != null
            ? coreTracking.IsCoreRevealed
            : objectiveDirector != null
                ? objectiveDirector.CoreRevealed
                : signalCount >= signalRequired;

        bool corelessRegion3 = run != null && run.ExpeditionDepth == ExpeditionDepth.DeepZone2;
        bool coreTrackingActive = !corelessRegion3 &&
                                  (coreTracking == null || coreTracking.IsTrackingActive);
        SetText(
            coreSignalValueText,
            coreTrackingActive ? $"{signalCount}/{signalRequired}" : "—"
        );
        SetColor(
            coreSignalValueText,
            coreTrackingActive && coreReady ? coreReadyColor : normalStatColor
        );

        int cargoCurrent = cargoController != null
            ? cargoController.CurrentLoad
            : run != null ? run.CurrentCargoLoad : 0;

        int cargoMax = cargoController != null
            ? cargoController.MaxCapacity
            : run != null ? run.MaxCargoCapacity : ship != null ? ship.CargoCapacity : 100;

        int emergencyLimit = cargoController != null
            ? cargoController.EmergencyReturnCapacityLimit
            : run != null
                ? Mathf.FloorToInt(run.MaxCargoCapacity * run.EmergencyReturnCapacityRatio)
                : ship != null
                    ? Mathf.FloorToInt(ship.CargoCapacity * ship.EmergencyReturnCapacityRatio)
                    : Mathf.FloorToInt(cargoMax * 0.7f);

        int tuningChips = run != null && run.Wallet != null ? run.Wallet.TuningChips : 0;

        SetText(cargoValueText, $"{cargoCurrent}/{cargoMax}");
        SetText(tuningChipValueText, FormatTuningChipValue(tuningChips));
        SetText(emergencyReturnValueText, $"{emergencyLimit}/{cargoMax}");
        SetColor(
            cargoValueText,
            selectedFieldDropTarget == BuildStatusFieldDropTarget.Cargo
                ? new Color(1f, 0.78f, 0.2f, 1f)
                : normalStatColor
        );
        SetColor(tuningChipValueText, normalStatColor);
        SetColor(emergencyReturnValueText, normalStatColor);
        string tip = !coreTrackingActive
            ? region3InvestigationTip
            : coreReady ? coreReadyTip : coreTrackingTip;
        SetText(shipTipText, selectedFieldDropTarget == BuildStatusFieldDropTarget.Cargo
            ? BuildCargoManifestText()
            : tip);
        RefreshCargoHeader(cargoCurrent, cargoMax);
        RefreshCargoManagement();
    }

    public void RefreshActiveSection()
    {
        RunContext run = ResolveRunContext();
        ActiveDisplayState state = ResolveActiveDisplayState(run);
        ReinforcementDefinition definition = state.definition;
        bool hasEquipment = definition != null;

        SetActive(activeEquippedRoot, hasEquipment);
        SetActive(activeEmptyRoot, !hasEquipment);

        if (!hasEquipment)
        {
            SetImage(activeIconImage, fallbackReinforcementIcon);
            SetText(activeNameText, "장비 없음");
            SetText(activeDescriptionText, "상점에서 액티브 장비를 장착할 수 있습니다.");
            SetText(activeEffectText, string.Empty);
            SetText(activeCooldownText, "재사용 -");
            SetText(activeChargeText, "보유 : 0개");
            SetText(activeStateText, "미장착");
            SetColor(activeStateText, activeUnavailableColor);
            SetActiveChargeRatio(0f, activeUnavailableColor);
            RefreshFieldDropSelectionVisual();
            return;
        }

        SetImage(activeIconImage, definition.Icon != null ? definition.Icon : fallbackReinforcementIcon);
        SetText(activeNameText, definition.DisplayName);
        SetText(activeDescriptionText, definition.Description);
        SetText(activeEffectText, definition.BuildEffectSummary());

        string cooldownText = definition.RechargeSeconds > 0f
            ? $"재사용 {definition.RechargeSeconds:0.#}초"
            : "재사용 없음";

        SetText(activeCooldownText, cooldownText);
        SetText(activeChargeText, $"보유 : {Mathf.Max(0, state.currentCharges)}/{Mathf.Max(1, state.maxCharges)}");

        bool ready = state.currentCharges > 0;
        Color stateColor;
        string stateText;

        if (ready)
        {
            stateText = "사용 가능";
            stateColor = activeReadyColor;
        }
        else if (state.isRecharging)
        {
            stateText = $"충전 중 {state.remainingSeconds:0.0}초";
            stateColor = activeChargingColor;
        }
        else
        {
            stateText = "사용 불가";
            stateColor = activeUnavailableColor;
        }

        SetText(activeStateText, stateText);
        SetColor(activeStateText, stateColor);
        SetActiveChargeRatio(CalculateTotalChargeRatio(definition, state.currentCharges, state.maxCharges, state.rechargeRatio), stateColor);
        RefreshFieldDropSelectionVisual();
    }

    public void RebuildPassiveSection()
    {
        string previousSelection = selectedPassiveId;

        ResolveTraits();
        BuildPassiveEntries();
        SortPassiveEntries();
        ClearPassiveSlots();

        int ownedCount = 0;

        for (int i = 0; i < passiveEntries.Count; i++)
        {
            PassiveEntry entry = passiveEntries[i];

            if (entry.IsOwned)
            {
                ownedCount++;
            }

            if (passiveContentRoot == null || passiveSlotPrefab == null)
            {
                continue;
            }

            int capturedIndex = i;
            BuildStatusSlotButtonUI slot = Instantiate(passiveSlotPrefab, passiveContentRoot);
            string amountLabel = entry.IsOwned
                ? string.Format(ownedSlotLabelFormat, Mathf.Max(1, entry.DisplayLevel))
                : unownedSlotLabel;

            slot.Bind(
                entry.trait != null && entry.trait.Icon != null ? entry.trait.Icon : fallbackTraitIcon,
                amountLabel,
                entry.trait != null ? entry.trait.GetRarityColor() : Color.white,
                entry.IsOwned,
                () => SelectPassive(capturedIndex)
            );

            passiveSlotInstances.Add(slot);
        }

        SetActive(passiveEmptyRoot, passiveEntries.Count == 0);

        if (passiveCountText != null)
        {
            passiveCountText.text = passiveListMode == PassiveStorageListMode.OwnedOnly
                ? $"보유 {ownedCount} · 제한 없음"
                : $"보유 {ownedCount} / 전체 {passiveEntries.Count}";
        }

        selectedPassiveIndex = FindPassiveIndex(previousSelection);

        if (selectedPassiveIndex < 0 && passiveEntries.Count > 0)
        {
            selectedPassiveIndex = FindFirstOwnedPassiveIndex();

            if (selectedPassiveIndex < 0)
            {
                selectedPassiveIndex = 0;
            }
        }

        if (selectedPassiveIndex >= 0)
        {
            SelectPassiveInternal(selectedPassiveIndex, false);
        }
        else
        {
            ClearSelectedPassiveDetail();
        }

        if (resetScrollPositionOnOpen && passiveScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            passiveScrollRect.verticalNormalizedPosition = 1f;
            passiveScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void SelectPassive(int index)
    {
        SelectPassiveInternal(index, true);
    }

    private void SelectPassiveInternal(int index, bool selectAsFieldDropTarget)
    {
        if (index < 0 || index >= passiveEntries.Count)
        {
            ClearSelectedPassiveDetail();
            return;
        }

        if (selectAsFieldDropTarget)
        {
            selectedFieldDropTarget = BuildStatusFieldDropTarget.Passive;
        }

        selectedPassiveIndex = index;
        PassiveEntry entry = passiveEntries[index];
        selectedPassiveId = entry.trait != null ? entry.trait.TraitId : string.Empty;

        for (int i = 0; i < passiveSlotInstances.Count; i++)
        {
            if (passiveSlotInstances[i] != null)
            {
                passiveSlotInstances[i].SetSelected(i == index);
            }
        }

        SetSelectedPassiveDetail(entry);
        RefreshFieldDropSelectionVisual();
    }

    private void SetSelectedPassiveDetail(PassiveEntry entry)
    {
        bool valid = entry != null && entry.trait != null;

        SetActive(selectedPassiveRoot, valid);
        SetActive(selectedPassiveEmptyRoot, !valid);

        if (!valid)
        {
            ClearSelectedPassiveDetailFields();
            return;
        }

        TraitDefinition trait = entry.trait;
        Color rarityColor = trait.GetRarityColor();

        SetImage(selectedPassiveIconImage, trait.Icon != null ? trait.Icon : fallbackTraitIcon);
        SetText(selectedPassiveNameText, trait.DisplayName);
        SetColor(selectedPassiveNameText, rarityColor);
        SetText(selectedPassiveCategoryText, trait.GetCategoryText());
        SetText(selectedPassiveRarityText, trait.GetRarityText());
        SetColor(selectedPassiveRarityText, rarityColor);

        if (selectedPassiveRarityBadgeImage != null)
        {
            selectedPassiveRarityBadgeImage.color = rarityColor;
        }

        SetText(selectedPassiveOwnedText, $"보유 수량 : {entry.OwnedAmount}개");
        SetText(selectedPassiveLevelText, BuildTraitLevelText(entry));
        SetText(selectedPassiveDescriptionText, trait.Description);
        SetText(selectedPassiveEffectText, BuildTraitEffectText(entry));

        string flavor = FindTraitFlavor(trait.TraitId);
        bool hasFlavor = !string.IsNullOrWhiteSpace(flavor);
        SetActive(selectedPassiveFlavorRoot, hasFlavor);
        SetText(selectedPassiveFlavorText, hasFlavor ? $"“{flavor}”" : string.Empty);
    }

    private void ClearSelectedPassiveDetail()
    {
        selectedPassiveIndex = -1;
        selectedPassiveId = string.Empty;

        for (int i = 0; i < passiveSlotInstances.Count; i++)
        {
            passiveSlotInstances[i]?.SetSelected(false);
        }

        SetActive(selectedPassiveRoot, false);
        SetActive(selectedPassiveEmptyRoot, true);
        ClearSelectedPassiveDetailFields();
    }

    private void ClearSelectedPassiveDetailFields()
    {
        SetImage(selectedPassiveIconImage, null);
        SetText(selectedPassiveNameText, string.Empty);
        SetText(selectedPassiveCategoryText, string.Empty);
        SetText(selectedPassiveRarityText, string.Empty);
        SetText(selectedPassiveOwnedText, string.Empty);
        SetText(selectedPassiveLevelText, string.Empty);
        SetText(selectedPassiveDescriptionText, string.Empty);
        SetText(selectedPassiveEffectText, string.Empty);
        SetText(selectedPassiveFlavorText, string.Empty);
        SetActive(selectedPassiveFlavorRoot, false);
    }

    private void BuildPassiveEntries()
    {
        passiveEntries.Clear();
        passiveEntryById.Clear();

        RunContext run = ResolveRunContext();
        WeaponTreeType selectedWeaponTree = ResolveWeaponTree(run, FindShip(run != null ? run.SelectedShipId : null));
        int order = 0;

        if (includeRuntimeTraits && run != null && run.SelectedTraitIds != null)
        {
            for (int i = 0; i < run.SelectedTraitIds.Count; i++)
            {
                string traitId = run.SelectedTraitIds[i];
                TraitDefinition trait = FindTrait(traitId);

                if (!CanDisplayTrait(trait, selectedWeaponTree))
                {
                    continue;
                }

                PassiveEntry entry = GetOrCreatePassiveEntry(trait, order++);
                int runtimeLevel = ResolveRuntimeTraitLevel(trait, run);
                entry.runtimeLevel = Mathf.Max(entry.runtimeLevel, runtimeLevel);
            }
        }

        if (includePermanentActiveTraits && PermanentProgress.Instance != null)
        {
            PermanentProgress progress = PermanentProgress.Instance;

            for (int i = 0; i < resolvedTraits.Count; i++)
            {
                TraitDefinition trait = resolvedTraits[i];

                bool ownsPersistentStoryTrait =
                    trait != null &&
                    trait.IsPersistentStoryTrait &&
                    progress.HasPersistentStoryTrait(trait);

                if (!CanDisplayTrait(trait, selectedWeaponTree) ||
                    (!ownsPersistentStoryTrait && !progress.IsTraitActive(trait.TraitId)))
                {
                    continue;
                }

                PassiveEntry entry = GetOrCreatePassiveEntry(trait, order++);
                entry.permanentLevel = ownsPersistentStoryTrait
                    ? 1
                    : 0;
            }
        }

        if (passiveListMode == PassiveStorageListMode.CatalogWithOwnedCount)
        {
            for (int i = 0; i < resolvedTraits.Count; i++)
            {
                TraitDefinition trait = resolvedTraits[i];

                if (!CanDisplayTrait(trait, selectedWeaponTree))
                {
                    continue;
                }

                GetOrCreatePassiveEntry(trait, order++);
            }
        }

        if (passiveListMode == PassiveStorageListMode.OwnedOnly)
        {
            for (int i = passiveEntries.Count - 1; i >= 0; i--)
            {
                if (!passiveEntries[i].IsOwned)
                {
                    passiveEntries.RemoveAt(i);
                }
            }
        }
    }

    private PassiveEntry GetOrCreatePassiveEntry(TraitDefinition trait, int acquisitionOrder)
    {
        if (trait == null)
        {
            return null;
        }

        string traitId = trait.TraitId;

        if (passiveEntryById.TryGetValue(traitId, out PassiveEntry existing))
        {
            return existing;
        }

        PassiveEntry entry = new PassiveEntry
        {
            trait = trait,
            acquisitionOrder = acquisitionOrder
        };

        passiveEntryById.Add(traitId, entry);
        passiveEntries.Add(entry);
        return entry;
    }

    private void SortPassiveEntries()
    {
        switch (passiveSortMode)
        {
            case PassiveStorageSortMode.RarityThenName:
                passiveEntries.Sort((a, b) =>
                {
                    int rarityCompare = b.trait.Rarity.CompareTo(a.trait.Rarity);
                    return rarityCompare != 0
                        ? rarityCompare
                        : string.Compare(a.trait.DisplayName, b.trait.DisplayName, StringComparison.Ordinal);
                });
                break;

            case PassiveStorageSortMode.Name:
                passiveEntries.Sort((a, b) =>
                    string.Compare(a.trait.DisplayName, b.trait.DisplayName, StringComparison.Ordinal));
                break;

            default:
                passiveEntries.Sort((a, b) => a.acquisitionOrder.CompareTo(b.acquisitionOrder));
                break;
        }
    }

    private bool CanDisplayTrait(TraitDefinition trait, WeaponTreeType selectedWeaponTree)
    {
        if (trait == null)
        {
            return false;
        }

        bool ownedPersistentStoryTrait =
            trait.IsPersistentStoryTrait &&
            PermanentProgress.Instance != null &&
            PermanentProgress.Instance.HasPersistentStoryTrait(trait);

        if (!includeHiddenTraits && trait.IsHidden && !ownedPersistentStoryTrait)
        {
            return false;
        }

        return trait.IsAvailableFor(selectedWeaponTree);
    }

    private int ResolveRuntimeTraitLevel(TraitDefinition trait, RunContext run)
    {
        if (trait == null)
        {
            return 0;
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int level = store != null ? store.GetLevel(trait.TraitId) : 0;

        if (level <= 0 && run != null && ContainsTraitId(run.SelectedTraitIds, trait.TraitId))
        {
            level = 1;
        }

        return Mathf.Clamp(level, 0, trait.MaxLevel);
    }

    private string BuildTraitLevelText(PassiveEntry entry)
    {
        if (entry == null || !entry.IsOwned)
        {
            return "미보유";
        }

        if (entry.permanentLevel > 0 && entry.runtimeLevel > 0)
        {
            return $"영구 · Lv.{entry.permanentLevel} / 탐사 · Lv.{entry.runtimeLevel}";
        }

        if (entry.runtimeLevel > 0)
        {
            return $"탐사 · Lv.{entry.runtimeLevel}/{entry.trait.MaxLevel}";
        }

        return $"영구 · Lv.{entry.permanentLevel}/{entry.trait.MaxLevel}";
    }

    private string BuildTraitEffectText(PassiveEntry entry)
    {
        if (entry == null || entry.trait == null)
        {
            return string.Empty;
        }

        int permanentLevel = entry.IsOwned ? Mathf.Max(0, entry.permanentLevel) : 1;
        int runtimeLevel = entry.IsOwned ? Mathf.Max(0, entry.runtimeLevel) : 0;

        return BuildTraitEffectsCombined(entry.trait, permanentLevel, runtimeLevel);
    }

    private string BuildTraitEffectsCombined(TraitDefinition trait, int permanentLevel, int runtimeLevel)
    {
        if (trait == null || trait.LevelEffects == null || trait.LevelEffects.Count == 0)
        {
            return "효과 정보 없음";
        }

        TraitEffectSummary summary = new TraitEffectSummary();

        AddTraitEffectsToSummary(summary, trait, permanentLevel);
        AddTraitEffectsToSummary(summary, trait, runtimeLevel);

        if (summary.order.Count == 0)
        {
            return "효과 정보 없음";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < summary.order.Count; i++)
        {
            TraitEffectType effectType = summary.order[i];

            if (!summary.values.TryGetValue(effectType, out float value))
            {
                continue;
            }

            if (IsZeroValueEffect(effectType, value))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("· ");
            builder.Append(FormatTraitEffect(effectType, value));
        }

        return builder.Length > 0 ? builder.ToString() : "효과 정보 없음";
    }

    private void AddTraitEffectsToSummary(TraitEffectSummary summary, TraitDefinition trait, int level)
    {
        if (summary == null || trait == null || trait.LevelEffects == null || level <= 0)
        {
            return;
        }

        level = Mathf.Clamp(level, 1, trait.MaxLevel);

        for (int i = 0; i < trait.LevelEffects.Count; i++)
        {
            TraitLevelEffect effect = trait.LevelEffects[i];

            if (effect == null || effect.Level > level)
            {
                continue;
            }

            summary.Add(effect.EffectType, effect.Value);
        }
    }

    private bool IsZeroValueEffect(TraitEffectType effectType, float value)
    {
        if (effectType == TraitEffectType.RemovePierceDamageFalloff)
        {
            return false;
        }

        return Mathf.Approximately(value, 0f);
    }

    private string FormatTraitEffect(TraitEffectType effectType, float value)
    {
        return effectType switch
        {
            TraitEffectType.DamagePercent => $"공격력 +{value:0.#}%",
            TraitEffectType.ProjectileSpeedPercent => $"탄속 +{value:0.#}%",
            TraitEffectType.RangePercent => $"사거리 +{value:0.#}%",
            TraitEffectType.MoveSpeedPercent => $"이동속도 +{value:0.#}%",
            TraitEffectType.DashCooldownReduction => $"대쉬 쿨다운 -{Mathf.Abs(value):0.##}초",
            TraitEffectType.DashDistanceBonus => $"대쉬 거리 +{value:0.#}",
            TraitEffectType.MaxHpBonus => $"최대 체력 +{value:0.#}",
            TraitEffectType.HealEfficiencyPercent => $"회복 효율 +{value:0.#}%",
            TraitEffectType.PickupRangeBonus => $"흡수 범위 +{value:0.#}",
            TraitEffectType.SpreadReductionPercent => $"탄 퍼짐 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.ProjectileCountBonus => $"발사체 수 +{Mathf.RoundToInt(value)}",
            TraitEffectType.PierceCountBonus => $"관통 횟수 +{Mathf.RoundToInt(value)}",
            TraitEffectType.ChargeTimeReductionPercent => $"차징 시간 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.ChargeDamagePercent => $"차징 피해 +{value:0.#}%",
            TraitEffectType.HomingAngleBonus => $"유도 각도 +{value:0.#}°",
            TraitEffectType.HomingRangeBonus => $"유도 거리 +{value:0.#}",
            TraitEffectType.FireRatePercent => $"연사력 +{value:0.#}%",
            TraitEffectType.CloseRangeDamageReductionPercent => $"근거리 피해 감소 +{value:0.#}%",
            TraitEffectType.DashDamageReductionPercent => $"대쉬 후 피해 감소 +{value:0.#}%",
            TraitEffectType.CloseRangeSuppressionPercent => $"근접 제압 +{value:0.#}%",
            TraitEffectType.ChargeSightBonusPercent => $"차징 시야 +{value:0.#}%",
            TraitEffectType.ChargedProjectileSizePercent => $"차징 탄 크기 +{value:0.#}%",
            TraitEffectType.RemovePierceDamageFalloff => "관통 피해 감쇠 제거",
            TraitEffectType.CargoCapacityBonus => $"기체용량 +{value:0.#}",
            TraitEffectType.HarvestYieldPercent => $"수확량 +{value:0.#}%",
            TraitEffectType.HarvestObjectDamagePercent => $"수확 오브젝트 피해 +{value:0.#}%",
            TraitEffectType.EmergencyReturnCapacityRatioBonus => $"긴급복귀 보존 한도 +{value:0.#}%",
            TraitEffectType.RadarScanRadiusBonus => $"레이더 반경 +{value:0.#}",
            TraitEffectType.ActiveCooldownReductionPercent => $"액티브 쿨다운 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.RadarTauntDurationBonus => $"레이더 도발 시간 +{value:0.#}초",
            TraitEffectType.RadarStealthDurationBonus => $"은밀 탐지 유지 +{value:0.#}초",
            TraitEffectType.SniperSemiAutoMode => "짧은 클릭으로 세미오토 레이저 발사",
            TraitEffectType.ShotgunCloseRangeDamagePercent => $"샷건 초근거리 피해 최대 +{value:0.#}%",
            TraitEffectType.MachineGunTerminalGuidance => "기관총 종말 유도 활성화",
            TraitEffectType.PeriodicReflectiveShield => $"반사 방벽 재충전 {value:0.#}초",
            TraitEffectType.MachineGunDashMissileSalvo => "대쉬 시 추격 미사일 3발 사출",
            TraitEffectType.SniperDashEchoShot => "대쉬 위치에서 다음 저격 사격을 40% 위력으로 복제",
            _ => $"{effectType} {value:0.##}"
        };
    }

    private void ResolveReferences()
    {
        if (playerObject == null)
        {
            PlayerHealth foundHealth = FindFirstObjectByType<PlayerHealth>();

            if (foundHealth != null)
            {
                playerObject = foundHealth.gameObject;
            }
        }

        if (playerObject == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        if (cargoController == null)
        {
            cargoController = playerObject.GetComponent<PlayerCargoController>();
        }

        if (reinforcementController == null)
        {
            reinforcementController = playerObject.GetComponent<PlayerReinforcementController>();
        }

        if (shipVisualController == null)
        {
            shipVisualController = playerObject.GetComponent<PlayerShipVisualController>();

            if (shipVisualController == null)
            {
                shipVisualController = playerObject.GetComponentInChildren<PlayerShipVisualController>(true);
            }
        }
    }

    private void ResolveCatalogs()
    {
        ResolveTraits();
        ResolveReinforcements();
    }

    private void ResolveTraits()
    {
        resolvedTraits.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraits);
        }

        if (!includeInspectorTraitDefinitions || traitDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            AppendUniqueTrait(traitDefinitions[i]);
        }
    }

    private void ResolveReinforcements()
    {
        resolvedReinforcements.Clear();

        if (reinforcementCatalog != null)
        {
            reinforcementCatalog.AppendAllTo(resolvedReinforcements);
        }

        if (!includeInspectorReinforcementDefinitions || reinforcementDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < reinforcementDefinitions.Count; i++)
        {
            AppendUniqueReinforcement(reinforcementDefinitions[i]);
        }
    }

    private void AppendUniqueTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return;
        }

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition existing = resolvedTraits[i];

            if (existing != null && existing.TraitId == trait.TraitId)
            {
                return;
            }
        }

        resolvedTraits.Add(trait);
    }

    private void AppendUniqueReinforcement(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        for (int i = 0; i < resolvedReinforcements.Count; i++)
        {
            ReinforcementDefinition existing = resolvedReinforcements[i];

            if (existing != null && existing.EquipmentId == definition.EquipmentId)
            {
                return;
            }
        }

        resolvedReinforcements.Add(definition);
    }

    private TraitDefinition FindTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition trait = resolvedTraits[i];

            if (trait != null && trait.TraitId == traitId)
            {
                return trait;
            }
        }

        return null;
    }

    private ReinforcementDefinition FindReinforcement(string reinforcementId)
    {
        if (string.IsNullOrWhiteSpace(reinforcementId))
        {
            return null;
        }

        for (int i = 0; i < resolvedReinforcements.Count; i++)
        {
            ReinforcementDefinition definition = resolvedReinforcements[i];

            if (definition != null && definition.EquipmentId == reinforcementId)
            {
                return definition;
            }
        }

        return null;
    }

    private ShipDefinition FindShip(string shipId)
    {
        if (shipDefinitions == null || shipDefinitions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(shipId))
        {
            for (int i = 0; i < shipDefinitions.Count; i++)
            {
                ShipDefinition ship = shipDefinitions[i];

                if (ship != null && ship.ShipId == shipId)
                {
                    return ship;
                }
            }
        }

        for (int i = 0; i < shipDefinitions.Count; i++)
        {
            if (shipDefinitions[i] != null)
            {
                return shipDefinitions[i];
            }
        }

        return null;
    }

    private RunContext ResolveRunContext()
    {
        return RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun
            : null;
    }

    private WeaponTreeType ResolveWeaponTree(RunContext run, ShipDefinition ship)
    {
        if (run != null)
        {
            return run.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return ship != null ? ship.DefaultWeaponTree : WeaponTreeType.MachineGun;
    }

    private string ResolveDefaultReinforcementId(RunContext run)
    {
        ShipDefinition ship = FindShip(run != null ? run.SelectedShipId : null);

        if (ship != null && !string.IsNullOrWhiteSpace(ship.DefaultReinforcementId))
        {
            return ship.DefaultReinforcementId;
        }

        return fallbackDefaultReinforcementId;
    }

    private string GetWeaponTreeDisplayName(WeaponTreeType weaponTree)
    {
        return weaponTree switch
        {
            WeaponTreeType.MachineGun => "기관총",
            WeaponTreeType.Shotgun => "근접 + 샷건",
            WeaponTreeType.Sniper => "관통 스나이퍼",
            _ => weaponTree.ToString()
        };
    }

    private void ConfigurePassiveScrollView()
    {
        if (passiveScrollRect != null)
        {
            passiveScrollRect.horizontal = false;
            passiveScrollRect.vertical = true;

            if (passiveContentRoot != null)
            {
                passiveScrollRect.content = passiveContentRoot;
            }
        }

        if (passiveGridLayoutGroup != null)
        {
            passiveGridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            passiveGridLayoutGroup.constraintCount = Mathf.Max(1, passiveColumnCount);
            passiveGridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            passiveGridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            passiveGridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        }

        if (passiveContentSizeFitter != null)
        {
            passiveContentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            passiveContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void ClearPassiveSlots()
    {
        for (int i = 0; i < passiveSlotInstances.Count; i++)
        {
            if (passiveSlotInstances[i] != null)
            {
                Destroy(passiveSlotInstances[i].gameObject);
            }
        }

        passiveSlotInstances.Clear();
    }

    private int FindPassiveIndex(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return -1;
        }

        for (int i = 0; i < passiveEntries.Count; i++)
        {
            if (passiveEntries[i].trait != null && passiveEntries[i].trait.TraitId == traitId)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindFirstOwnedPassiveIndex()
    {
        for (int i = 0; i < passiveEntries.Count; i++)
        {
            if (passiveEntries[i].IsOwned)
            {
                return i;
            }
        }

        return -1;
    }

    private string FindTraitFlavor(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId) || traitFlavorOverrides == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < traitFlavorOverrides.Count; i++)
        {
            TraitFlavorOverride entry = traitFlavorOverrides[i];

            if (entry != null && entry.TraitId == traitId)
            {
                return entry.FlavorText;
            }
        }

        return string.Empty;
    }

    public void SelectActiveForFieldDrop()
    {
        selectedFieldDropTarget = BuildStatusFieldDropTarget.Active;
        RefreshFieldDropSelectionVisual();
    }

    public void TryDropSelectedFieldItem()
    {
        if (selectedFieldDropTarget == BuildStatusFieldDropTarget.Active)
        {
            TryDropActiveFieldItem();
        }
        else if (selectedFieldDropTarget == BuildStatusFieldDropTarget.Passive)
        {
            TryDropSelectedPassiveFieldItem();
        }
    }

    public void TryDropActiveFieldItem()
    {
        SelectActiveForFieldDrop();

        if (reinforcementController == null || !reinforcementController.HasEquipment)
        {
            ShowFieldDropWarning("드랍할 액티브 장비가 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        ReinforcementDefinition dropped = reinforcementController.EquippedDefinition;
        bool success = reinforcementController.DropCurrentEquipment(ResolveFieldDropPosition());

        if (!success)
        {
            ShowFieldDropWarning("액티브 장비 드랍에 실패했습니다. ReinforcementPickup 프리팹 연결을 확인하세요.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        ShowFieldDropWarning($"필드 드랍: {dropped.DisplayName}");
        RefreshActiveSection();
        RefreshFieldDropSelectionVisual();
    }

    public void TryDropSelectedPassiveFieldItem()
    {
        selectedFieldDropTarget = BuildStatusFieldDropTarget.Passive;
        RefreshFieldDropSelectionVisual();

        if (selectedPassiveIndex < 0 || selectedPassiveIndex >= passiveEntries.Count)
        {
            ShowFieldDropWarning("드랍할 패시브를 선택하세요.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        PassiveEntry entry = passiveEntries[selectedPassiveIndex];

        if (entry == null || entry.trait == null)
        {
            ShowFieldDropWarning("드랍할 패시브 데이터가 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        if (!entry.trait.CanFieldDrop)
        {
            ShowFieldDropWarning("이 특성은 필드에 드랍할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        if (entry.runtimeLevel <= 0)
        {
            ShowFieldDropWarning("영구 적용 패시브는 필드에 드랍할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        if (traitDropPickupPrefab == null)
        {
            ShowFieldDropWarning("TraitPickup 프리팹이 연결되지 않았습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        RunTraitEffectApplier applier = playerObject != null
            ? playerObject.GetComponent<RunTraitEffectApplier>()
            : null;

        if (applier == null && playerObject != null)
        {
            applier = playerObject.GetComponentInChildren<RunTraitEffectApplier>(true);
        }

        if (applier == null)
        {
            applier = FindFirstObjectByType<RunTraitEffectApplier>();
        }

        if (applier == null)
        {
            ShowFieldDropWarning("RunTraitEffectApplier가 없어 패시브 효과를 안전하게 제거할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        TraitPickup spawnedPickup = SpawnTraitFieldPickup(entry.trait, ResolveFieldDropPosition());

        if (spawnedPickup == null)
        {
            ShowFieldDropWarning("패시브 픽업 생성에 실패했습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        int removedLevel = entry.runtimeLevel;
        applier.RemoveTraitLevel(entry.trait, removedLevel);

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int previousLevel = 0;
        int remainingLevel = 0;
        bool removed = false;

        if (store != null)
        {
            removed = store.TryRemoveLevel(
                entry.trait.TraitId,
                out previousLevel,
                out remainingLevel
            );
        }
        if (!removed)
        {
            applier.ApplyTraitLevel(entry.trait, removedLevel);
            ReleaseFieldDropObject(spawnedPickup.gameObject);
            ShowFieldDropWarning("패시브 제거에 실패했습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        if (remainingLevel <= 0 && RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CurrentRun.RemoveTrait(entry.trait.TraitId);
        }

        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, ResolveFieldDropPosition());
        ShowFieldDropWarning($"필드 드랍: {entry.trait.DisplayName} Lv.{previousLevel}");
        RebuildPassiveSection();
    }

    private void HandleFieldDropInput()
    {
        if (selectedFieldDropTarget == BuildStatusFieldDropTarget.Cargo)
        {
            HandleCargoJettisonInput();
            return;
        }

        bool pressed;

        if (fieldDropAction != null)
        {
            pressed = fieldDropAction.WasPressedThisFrame();
        }
        else
        {
            KeyControl keyControl = Keyboard.current != null ? Keyboard.current[fieldDropKey] : null;
            pressed = keyControl != null && keyControl.wasPressedThisFrame;
        }

        if (pressed)
        {
            TryDropSelectedFieldItem();
        }
    }

    private void HandleCargoJettisonInput()
    {
        if (cargoController == null ||
            !cargoController.CanJettison(selectedCargoType) ||
            GetCargoAmount(selectedCargoType) <= 0)
        {
            cargoJettisonHoldTimer = 0f;
            cargoJettisonConsumedUntilRelease = false;
            return;
        }

        bool held;

        if (fieldDropAction != null)
        {
            held = fieldDropAction.IsPressed();
        }
        else
        {
            KeyControl keyControl = Keyboard.current != null ? Keyboard.current[fieldDropKey] : null;
            held = keyControl != null && keyControl.isPressed;
        }

        if (!held)
        {
            bool needsRefresh = cargoJettisonHoldTimer > 0f || cargoJettisonConsumedUntilRelease;
            cargoJettisonHoldTimer = 0f;
            cargoJettisonConsumedUntilRelease = false;

            if (needsRefresh)
            {
                RefreshShipSection();
            }

            return;
        }

        if (cargoJettisonConsumedUntilRelease)
        {
            return;
        }

        cargoJettisonHoldTimer += Time.unscaledDeltaTime;
        RefreshShipSection();

        if (cargoJettisonHoldTimer < Mathf.Max(0.1f, cargoJettisonHoldDuration))
        {
            return;
        }

        cargoJettisonConsumedUntilRelease = true;
        cargoJettisonHoldTimer = 0f;
        TryJettisonSelectedCargo();
    }

    private void TryJettisonSelectedCargo()
    {
        if (cargoController == null || !cargoController.CanJettison(selectedCargoType))
        {
            return;
        }

        int ownedAmount = GetCargoAmount(selectedCargoType);
        int discardAmount = Mathf.Clamp(selectedCargoJettisonAmount, 0, ownedAmount);

        if (discardAmount <= 0)
        {
            ShowFieldDropWarning("버릴 자원이 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            RefreshShipSection();
            return;
        }

        if (cargoController == null || cargoDropPickupPrefab == null)
        {
            ShowFieldDropWarning("자원을 배출할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        Vector2 fieldPosition = ResolveFieldDropPosition();
        Vector2 origin = playerObject != null ? playerObject.transform.position : fieldPosition;
        Vector2 direction = fieldPosition - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = fallbackFieldDropDirection.sqrMagnitude > 0.001f
                ? fallbackFieldDropDirection
                : Vector2.down;
        }

        direction.Normalize();
        fieldPosition = origin + direction * Mathf.Max(fieldDropDistance, cargoJettisonDistance);

        if (!cargoController.TryJettison(
                selectedCargoType,
                discardAmount,
                cargoDropPickupPrefab,
                fieldPosition,
                direction * 1.25f,
                cargoRepickupBlockSeconds,
                out RewardPickup pickup))
        {
            ShowFieldDropWarning("자원 배출에 실패했습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, pickup.transform.position);
        ShowFieldDropWarning($"자원 배출: {GetCargoDisplayName(selectedCargoType)} ×{discardAmount}");
        selectedCargoJettisonAmount = 1;
        RefreshShipSection();
    }

    private RewardPickup SpawnCargoPickup(CurrencyType currencyType, int amount)
    {
        if (cargoDropPickupPrefab == null || amount <= 0)
        {
            return null;
        }

        Vector2 position = ResolveFieldDropPosition();
        Vector2 origin = playerObject != null ? playerObject.transform.position : position;
        Vector2 direction = position - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = fallbackFieldDropDirection.sqrMagnitude > 0.001f
                ? fallbackFieldDropDirection
                : Vector2.down;
        }

        direction.Normalize();
        position = origin + direction * Mathf.Max(fieldDropDistance, cargoJettisonDistance);
        GameObject pickupObject = PoolManager.Instance != null
            ? PoolManager.Instance.Get(cargoDropPickupPrefab.gameObject, position, Quaternion.identity)
            : Instantiate(cargoDropPickupPrefab.gameObject, position, Quaternion.identity);
        RewardPickup pickup = pickupObject != null ? pickupObject.GetComponent<RewardPickup>() : null;

        if (pickup == null)
        {
            ReleaseFieldDropObject(pickupObject);
            return null;
        }

        pickup.InitializeOwnedCurrency(
            currencyType,
            amount,
            direction * 1.25f,
            cargoRepickupBlockSeconds
        );
        return pickup;
    }

    private string BuildCargoManifestText()
    {
        string key = ResolveFieldDropKeyText();
        float holdRatio = Mathf.Clamp01(cargoJettisonHoldTimer / Mathf.Max(0.1f, cargoJettisonHoldDuration));
        string progress = holdRatio > 0f ? $"  {Mathf.RoundToInt(holdRatio * 100f)}%" : string.Empty;

        return $"적재 화물 · [{key}] 길게 눌러 버리기{progress}\n" +
               BuildCargoManifestRow(CurrencyType.ScrapParts) + "\n" +
               BuildCargoManifestRow(CurrencyType.StabilizedAlloy) + "\n" +
               BuildCargoManifestRow(CurrencyType.CoreShards);
    }

    private string BuildCargoManifestRow(CurrencyType currencyType)
    {
        string marker = selectedCargoType == currencyType ? ">" : " ";
        int amount = GetCargoAmount(currencyType);
        int contribution = cargoController != null
            ? cargoController.GetCargoWeight(currencyType) * amount
            : 0;
        return $"{marker} {GetCargoDisplayName(currencyType)}  {amount}  무게 {contribution}";
    }

    private int GetCargoAmount(CurrencyType currencyType)
    {
        return cargoController != null ? cargoController.GetCargoAmount(currencyType) : 0;
    }

    private int ResolveJettisonAmount(CurrencyType currencyType)
    {
        return currencyType == CurrencyType.ScrapParts
            ? Mathf.Max(1, scrapJettisonAmount)
            : Mathf.Max(1, rareCargoJettisonAmount);
    }

    private static string GetCargoDisplayName(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.Credits => "재화",
            CurrencyType.ScrapParts => "스크랩",
            CurrencyType.CoreShards => "코어",
            CurrencyType.TuningChips => "튜닝 칩",
            CurrencyType.StabilizedAlloy => "안정화 합금",
            _ => currencyType.ToString()
        };
    }

    private void RefreshCargoHeader(int currentCargo, int maximumCargo)
    {
        int safeMaximum = Mathf.Max(1, maximumCargo);
        SetText(cargoLoadText, $"적재량 {currentCargo} / {maximumCargo}");

        if (cargoLoadSlider != null)
        {
            cargoLoadSlider.minValue = 0f;
            cargoLoadSlider.maxValue = safeMaximum;
            cargoLoadSlider.wholeNumbers = true;
            cargoLoadSlider.SetValueWithoutNotify(Mathf.Clamp(currentCargo, 0, safeMaximum));
            cargoLoadSlider.interactable = false;
        }
    }

    private void RefreshCargoManagement()
    {
        if (cargoManagementRoot == null || cargoController == null)
        {
            return;
        }

        bool selectedRowVisible = false;
        bool hasVisibleRows = false;
        CurrencyType firstVisibleType = default;

        if (cargoManifestRows != null)
        {
            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                CargoManifestRowUI row = cargoManifestRows[i];

                if (row == null || !row.IsConfigured)
                {
                    continue;
                }

                CurrencyType currencyType = row.CurrencyType;
                int amount = cargoController.GetCargoAmount(currencyType);
                bool visible = amount > 0 || showAllCargoResources;
                row.SetVisible(visible);

                if (!visible)
                {
                    continue;
                }

                if (!hasVisibleRows)
                {
                    firstVisibleType = currencyType;
                    hasVisibleRows = true;
                }

                selectedRowVisible |= hasSelectedCargo && currencyType == selectedCargoType;
            }
        }

        if (!selectedRowVisible)
        {
            hasSelectedCargo = hasVisibleRows;

            if (hasVisibleRows)
            {
                selectedCargoType = firstVisibleType;
                selectedFieldDropTarget = BuildStatusFieldDropTarget.Cargo;
                selectedCargoJettisonAmount = GetCargoAmount(selectedCargoType) > 0 ? 1 : 0;
            }
            else
            {
                selectedCargoJettisonAmount = 0;
                selectedFieldDropTarget = BuildStatusFieldDropTarget.Active;
            }
        }

        if (cargoManifestRows != null)
        {
            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                CargoManifestRowUI row = cargoManifestRows[i];

                if (row == null || !row.IsConfigured || !row.IsVisible)
                {
                    continue;
                }

                CurrencyType currencyType = row.CurrencyType;
                int amount = cargoController.GetCargoAmount(currencyType);
                int unitWeight = cargoController.GetCargoWeight(currencyType);
                bool usesCargo = cargoController.UsesCargo(currencyType);
                Sprite icon = cargoDropPickupPrefab != null
                    ? cargoDropPickupPrefab.GetCurrencySprite(currencyType)
                    : null;
                row.Refresh(
                    icon,
                    GetCargoDisplayName(currencyType),
                    amount,
                    unitWeight,
                    amount * unitWeight,
                    usesCargo,
                    cargoController.IsAutoPickupEnabled(currencyType),
                    hasSelectedCargo && currencyType == selectedCargoType
                );
            }
        }

        SetText(cargoShowAllText, showAllCargoResources
            ? "[0개 숨기기]"
            : "[전체 보기]");
        SetColor(
            cargoShowAllText,
            showAllCargoResources
                ? new Color(0.45f, 1f, 1f, 1f)
                : new Color(0.62f, 0.78f, 0.82f, 1f)
        );
        SetActive(selectedCargoEmptyText != null ? selectedCargoEmptyText.gameObject : null, !hasVisibleRows);
        SetActive(cargoDetailActionsRoot, hasSelectedCargo);

        if (!hasSelectedCargo)
        {
            SetImage(selectedCargoIconImage, null);
            SetText(selectedCargoNameText, string.Empty);
            SetText(selectedCargoStatsText, string.Empty);
            SetText(selectedCargoQuantityText, string.Empty);
            SetActive(selectedCargoAutoPickupText != null ? selectedCargoAutoPickupText.gameObject : null, false);
            SetActive(cargoAutoPickupButton != null ? cargoAutoPickupButton.gameObject : null, false);
            RefreshCargoManifestNavigation();
            RepairCargoEventSystemSelection();
            return;
        }

        int selectedAmount = cargoController.GetCargoAmount(selectedCargoType);
        int selectedUnitWeight = cargoController.GetCargoWeight(selectedCargoType);
        int selectedContribution = selectedAmount * selectedUnitWeight;
        bool selectedUsesCargo = cargoController.UsesCargo(selectedCargoType);
        bool selectedCanJettison = cargoController.CanJettison(selectedCargoType);
        bool autoPickupEnabled = cargoController.IsAutoPickupEnabled(selectedCargoType);
        selectedCargoJettisonAmount = selectedCanJettison && selectedAmount > 0
            ? Mathf.Clamp(selectedCargoJettisonAmount, 1, selectedAmount)
            : 0;

        Sprite selectedIcon = cargoDropPickupPrefab != null
            ? cargoDropPickupPrefab.GetCurrencySprite(selectedCargoType)
            : null;
        SetImage(selectedCargoIconImage, selectedIcon);
        SetText(selectedCargoNameText, GetCargoDisplayName(selectedCargoType));
        SetText(
            selectedCargoStatsText,
            selectedUsesCargo
                ? $"보유 {selectedAmount} · 개당 {selectedUnitWeight} · 적재 {selectedContribution}"
                : selectedCanJettison
                    ? $"보유 {selectedAmount} · 임시 자원"
                    : $"보유 {selectedAmount} · 버릴 수 없는 임시 자원"
        );
        SetText(selectedCargoQuantityText, selectedCanJettison && selectedAmount > 0 ? $"{selectedCargoJettisonAmount}개" : "-");
        SetText(
            selectedCargoAutoPickupText,
            selectedUsesCargo
                ? autoPickupEnabled ? "자동 회수 켬" : "자동 회수 끔"
                : "화물 관리 제외"
        );
        SetActive(selectedCargoAutoPickupText != null ? selectedCargoAutoPickupText.gameObject : null, selectedUsesCargo);
        SetActive(cargoAutoPickupButton != null ? cargoAutoPickupButton.gameObject : null, selectedUsesCargo);
        SetActive(cargoQuantitySlider != null ? cargoQuantitySlider.gameObject : null, selectedCanJettison);
        SetActive(selectedCargoQuantityText != null ? selectedCargoQuantityText.gameObject : null, selectedCanJettison);
        SetActive(cargoQuantityOneButton != null ? cargoQuantityOneButton.gameObject : null, selectedCanJettison);
        SetActive(cargoQuantityHalfButton != null ? cargoQuantityHalfButton.gameObject : null, selectedCanJettison);
        SetActive(cargoQuantityMaxButton != null ? cargoQuantityMaxButton.gameObject : null, selectedCanJettison);
        SetActive(cargoJettisonButton != null ? cargoJettisonButton.gameObject : null, selectedCanJettison);

        if (cargoQuantitySlider != null)
        {
            cargoQuantitySlider.minValue = 1f;
            cargoQuantitySlider.maxValue = Mathf.Max(1, selectedAmount);
            cargoQuantitySlider.wholeNumbers = true;
            cargoQuantitySlider.SetValueWithoutNotify(Mathf.Max(1, selectedCargoJettisonAmount));
            cargoQuantitySlider.interactable = selectedCanJettison && selectedAmount > 0;
        }

        bool canJettisonSelection = selectedCanJettison && selectedAmount > 0;
        SetButtonInteractable(cargoQuantityOneButton, canJettisonSelection);
        SetButtonInteractable(cargoQuantityHalfButton, canJettisonSelection);
        SetButtonInteractable(cargoQuantityMaxButton, canJettisonSelection);
        SetButtonInteractable(cargoJettisonButton, canJettisonSelection);
        SetButtonInteractable(cargoAutoPickupButton, selectedUsesCargo);
        RefreshCargoManifestNavigation();
        RepairCargoEventSystemSelection();
    }

    private void ToggleCargoShowAll()
    {
        showAllCargoResources = !showAllCargoResources;
        RefreshCargoManagement();
    }

    private void RefreshCargoManifestNavigation()
    {
        Selectable firstVisible = null;
        Selectable previousVisible = null;

        if (cargoManifestRows != null)
        {
            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                CargoManifestRowUI row = cargoManifestRows[i];
                Selectable current = row != null && row.IsVisible ? row.Selectable : null;

                if (current == null)
                {
                    continue;
                }

                firstVisible ??= current;
                Navigation navigation = current.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = previousVisible != null ? previousVisible : cargoShowAllButton;
                navigation.selectOnDown = null;
                current.navigation = navigation;

                if (previousVisible != null)
                {
                    Navigation previousNavigation = previousVisible.navigation;
                    previousNavigation.selectOnDown = current;
                    previousVisible.navigation = previousNavigation;
                }

                previousVisible = current;
            }
        }

        Selectable detailSelectable = cargoQuantitySlider != null &&
                                      cargoQuantitySlider.gameObject.activeInHierarchy &&
                                      cargoQuantitySlider.interactable
            ? cargoQuantitySlider
            : cargoShowAllButton;

        if (previousVisible != null && detailSelectable != null)
        {
            Navigation navigation = previousVisible.navigation;
            navigation.selectOnDown = detailSelectable;
            previousVisible.navigation = navigation;

            if (detailSelectable == cargoQuantitySlider)
            {
                Navigation sliderNavigation = cargoQuantitySlider.navigation;
                sliderNavigation.mode = Navigation.Mode.Explicit;
                sliderNavigation.selectOnUp = previousVisible;
                cargoQuantitySlider.navigation = sliderNavigation;
            }
        }

        if (cargoShowAllButton != null)
        {
            Navigation navigation = cargoShowAllButton.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = firstVisible;
            cargoShowAllButton.navigation = navigation;
        }
    }

    private void SelectCargoManifestType(CurrencyType currencyType)
    {
        selectedCargoType = currencyType;
        hasSelectedCargo = true;
        selectedFieldDropTarget = BuildStatusFieldDropTarget.Cargo;
        selectedCargoJettisonAmount = GetCargoAmount(currencyType) > 0 ? 1 : 0;
        cargoJettisonHoldTimer = 0f;
        cargoJettisonConsumedUntilRelease = false;
        RefreshCargoManagement();
        RefreshFieldDropSelectionVisual();
    }

    private void RepairCargoEventSystemSelection()
    {
        if (!isOpen || EventSystem.current == null)
        {
            return;
        }

        GameObject current = EventSystem.current.currentSelectedGameObject;

        Selectable currentSelectable = current != null ? current.GetComponent<Selectable>() : null;

        if (current != null &&
            current.activeInHierarchy &&
            (currentSelectable == null || currentSelectable.IsInteractable()))
        {
            return;
        }

        Selectable fallback = FirstCargoSelectable ?? cargoShowAllButton;
        EventSystem.current.SetSelectedGameObject(fallback != null ? fallback.gameObject : null);
    }

    private void HandleCargoQuantityChanged(float value)
    {
        int ownedAmount = GetCargoAmount(selectedCargoType);
        selectedCargoJettisonAmount = ownedAmount > 0
            ? Mathf.Clamp(Mathf.RoundToInt(value), 1, ownedAmount)
            : 0;
        SetText(selectedCargoQuantityText, selectedCargoJettisonAmount > 0
            ? $"{selectedCargoJettisonAmount}개"
            : "0개");
    }

    private void SetCargoQuantityOne()
    {
        SetSelectedCargoQuantity(1);
    }

    private void SetCargoQuantityHalf()
    {
        int amount = GetCargoAmount(selectedCargoType);
        SetSelectedCargoQuantity(Mathf.Max(1, Mathf.CeilToInt(amount * 0.5f)));
    }

    private void SetCargoQuantityMax()
    {
        SetSelectedCargoQuantity(GetCargoAmount(selectedCargoType));
    }

    private void SetSelectedCargoQuantity(int amount)
    {
        int ownedAmount = GetCargoAmount(selectedCargoType);
        selectedCargoJettisonAmount = ownedAmount > 0
            ? Mathf.Clamp(amount, 1, ownedAmount)
            : 0;
        cargoQuantitySlider?.SetValueWithoutNotify(Mathf.Max(1, selectedCargoJettisonAmount));
        SetText(selectedCargoQuantityText, selectedCargoJettisonAmount > 0
            ? $"{selectedCargoJettisonAmount}개"
            : "0개");
    }

    private void ToggleSelectedCargoAutoPickup()
    {
        if (cargoController == null || !cargoController.UsesCargo(selectedCargoType))
        {
            return;
        }

        bool enabled = cargoController.IsAutoPickupEnabled(selectedCargoType);
        cargoController.SetAutoPickupEnabled(selectedCargoType, !enabled);
        RefreshCargoManagement();
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void BindCargoManagementLayout()
    {
        bool hasMainLayout = cargoManagementRoot != null &&
                             cargoLoadText != null &&
                             cargoLoadSlider != null &&
                             cargoManifestRows != null &&
                             cargoManifestRows.Count > 0 &&
                             cargoShowAllButton != null &&
                             cargoShowAllText != null &&
                             cargoQuantitySlider != null &&
                             cargoJettisonButton != null &&
                             cargoAutoPickupButton != null;

        if (!hasMainLayout)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "Shared Inventory Cargo references are incomplete. The production prefab must author the Cargo layout.",
                this
            );
#endif
            return;
        }

        for (int i = 0; i < cargoManifestRows.Count; i++)
        {
            CargoManifestRowUI row = cargoManifestRows[i];

            if (row != null && row.IsConfigured)
            {
                row.Bind(SelectCargoManifestType);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning($"Cargo manifest row {i} is not fully configured.", this);
            }
#endif
        }

        cargoQuantitySlider.onValueChanged.RemoveListener(HandleCargoQuantityChanged);
        cargoQuantitySlider.onValueChanged.AddListener(HandleCargoQuantityChanged);
        BindCargoButton(cargoQuantityOneButton, SetCargoQuantityOne);
        BindCargoButton(cargoQuantityHalfButton, SetCargoQuantityHalf);
        BindCargoButton(cargoQuantityMaxButton, SetCargoQuantityMax);
        BindCargoButton(cargoShowAllButton, ToggleCargoShowAll);
        BindCargoButton(cargoAutoPickupButton, ToggleSelectedCargoAutoPickup);
        BindCargoButton(cargoJettisonButton, TryJettisonSelectedCargo);
    }

    private void UnbindCargoManagementLayout()
    {
        if (cargoManifestRows != null)
        {
            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                cargoManifestRows[i]?.Unbind();
            }
        }

        cargoQuantitySlider?.onValueChanged.RemoveListener(HandleCargoQuantityChanged);
        UnbindCargoButton(cargoQuantityOneButton, SetCargoQuantityOne);
        UnbindCargoButton(cargoQuantityHalfButton, SetCargoQuantityHalf);
        UnbindCargoButton(cargoQuantityMaxButton, SetCargoQuantityMax);
        UnbindCargoButton(cargoShowAllButton, ToggleCargoShowAll);
        UnbindCargoButton(cargoAutoPickupButton, ToggleSelectedCargoAutoPickup);
        UnbindCargoButton(cargoJettisonButton, TryJettisonSelectedCargo);
    }

    private void ConfigureCargoTypography()
    {
        if (cargoShowAllText != null)
        {
            cargoShowAllText.raycastTarget = true;
        }

        if (cargoManifestRows != null)
        {
            for (int i = 0; i < cargoManifestRows.Count; i++)
            {
                cargoManifestRows[i]?.ConfigureTypography(uiFont);
            }
        }

    }

    private static void BindCargoButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindCargoButton(Button button, UnityEngine.Events.UnityAction action)
    {
        button?.onClick.RemoveListener(action);
    }
    private void BindFieldDropInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        fieldDropAction = InputBindingUtility.ResolveAction(
            inputActions,
            playerActionMapName,
            fieldDropActionName
        );
    }

    private string ResolveFieldDropKeyText()
    {
        return InputBindingUtility.GetDisplayString(
            inputActions,
            playerActionMapName,
            fieldDropActionName,
            fieldDropKey.ToString()
        );
    }

    private TraitPickup SpawnTraitFieldPickup(TraitDefinition trait, Vector2 position)
    {
        if (trait == null || traitDropPickupPrefab == null)
        {
            return null;
        }

        GameObject pickupObject;

        if (PoolManager.Instance != null)
        {
            pickupObject = PoolManager.Instance.Get(traitDropPickupPrefab.gameObject, position, Quaternion.identity);
        }
        else
        {
            pickupObject = Instantiate(traitDropPickupPrefab.gameObject, position, Quaternion.identity);
        }

        if (pickupObject == null)
        {
            return null;
        }

        TraitPickup pickup = pickupObject.GetComponent<TraitPickup>();

        if (pickup == null)
        {
            ReleaseFieldDropObject(pickupObject);
            return null;
        }

        pickup.Initialize(trait, fieldDropBlockSeconds);
        return pickup;
    }

    private Vector2 ResolveFieldDropPosition()
    {
        Transform origin = fieldDropOrigin != null
            ? fieldDropOrigin
            : playerObject != null ? playerObject.transform : null;

        Vector2 originPosition = origin != null ? origin.position : Vector2.zero;
        Vector2 direction = fallbackFieldDropDirection;

        PlayerController2D controller = playerObject != null ? playerObject.GetComponent<PlayerController2D>() : null;

        if (controller != null && controller.AimDirection.sqrMagnitude > 0.001f)
        {
            direction = -controller.AimDirection;
        }
        else if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        return originPosition + direction.normalized * Mathf.Max(0.1f, fieldDropDistance);
    }

    private void ReleaseFieldDropObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(target);
        }
        else
        {
            Destroy(target);
        }
    }

    private void RefreshFieldDropSelectionVisual()
    {
        bool activeSelected = selectedFieldDropTarget == BuildStatusFieldDropTarget.Active;
        SetActive(activeDropSelectedIndicator, activeSelected);

        if (cargoValueText != null)
        {
            cargoValueText.color = selectedFieldDropTarget == BuildStatusFieldDropTarget.Cargo
                ? new Color(1f, 0.78f, 0.2f, 1f)
                : normalStatColor;
        }

        if (fieldDropHintText == null)
        {
            return;
        }

        if (selectedFieldDropTarget == BuildStatusFieldDropTarget.Cargo)
        {
            fieldDropHintText.text = cargoController != null && cargoController.CanJettison(selectedCargoType)
                ? $"[{ResolveFieldDropKeyText()}] 길게 눌러 버리기 : {GetCargoDisplayName(selectedCargoType)}"
                : $"버릴 수 없는 자원 : {GetCargoDisplayName(selectedCargoType)}";
            return;
        }

        if (activeSelected)
        {
            string activeName = reinforcementController != null && reinforcementController.EquippedDefinition != null
                ? reinforcementController.EquippedDefinition.DisplayName
                : "장비 없음";
            fieldDropHintText.text = $"[{ResolveFieldDropKeyText()}] 액티브 필드 드랍 : {activeName}";
            return;
        }

        string passiveName = selectedPassiveIndex >= 0 && selectedPassiveIndex < passiveEntries.Count && passiveEntries[selectedPassiveIndex]?.trait != null
            ? passiveEntries[selectedPassiveIndex].trait.DisplayName
            : "패시브 미선택";
        fieldDropHintText.text = $"[{ResolveFieldDropKeyText()}] 패시브 필드 드랍 : {passiveName}";
    }

    private void ShowFieldDropWarning(string message)
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowWarning(message);
        }
    }

    private void BindRuntimeEvents()
    {
        if (runtimeEventsBound)
        {
            return;
        }

        runtimeEventsBound = true;

        if (playerHealth != null)
        {
            playerHealth.Changed += HandleHealthChanged;
            playerHealth.Died += HandlePlayerDied;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged += HandleCargoChanged;
            cargoController.AutoPickupPreferenceChanged += HandleAutoPickupPreferenceChanged;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged += HandleEquipmentChanged;
            reinforcementController.ChargesChanged += HandleChargesChanged;
        }

        subscribedCoreTrackingController = FindFirstObjectByType<CoreTrackingSignalController>();
        if (subscribedCoreTrackingController != null)
        {
            subscribedCoreTrackingController.ProgressChanged += HandleObjectiveProgressChanged;
        }
        else
        {
            subscribedObjectiveDirector = ExpeditionObjectiveDirector.Instance;
            if (subscribedObjectiveDirector != null)
            {
                subscribedObjectiveDirector.ProgressChanged += HandleObjectiveProgressChanged;
            }
        }

        subscribedRuntimeTraitStore = RunRuntimeTraitStore.Instance;
        if (subscribedRuntimeTraitStore != null)
        {
            subscribedRuntimeTraitStore.Changed += HandleTraitCollectionChanged;
        }

        subscribedPermanentProgress = PermanentProgress.Instance;
        if (subscribedPermanentProgress != null)
        {
            subscribedPermanentProgress.Changed += HandleTraitCollectionChanged;
            subscribedPermanentProgress.Changed += RefreshStoryRecovery;
        }

        subscribedRunManager = RunManager.Instance;
        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged += HandleWalletChanged;
            subscribedRunManager.RunEnded += HandleStoryRecoveryRunEnded;
        }
        subscribedStoryLocalization = VoidScrapperLocalizationService.Instance;
        if (subscribedStoryLocalization != null)
        {
            subscribedStoryLocalization.LanguageChanged += HandleStoryRecoveryLanguageChanged;
        }
    }

    private void UnbindRuntimeEvents()
    {
        if (!runtimeEventsBound)
        {
            return;
        }

        runtimeEventsBound = false;

        if (playerHealth != null)
        {
            playerHealth.Changed -= HandleHealthChanged;
            playerHealth.Died -= HandlePlayerDied;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
            cargoController.AutoPickupPreferenceChanged -= HandleAutoPickupPreferenceChanged;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged -= HandleEquipmentChanged;
            reinforcementController.ChargesChanged -= HandleChargesChanged;
        }

        if (subscribedObjectiveDirector != null)
        {
            subscribedObjectiveDirector.ProgressChanged -= HandleObjectiveProgressChanged;
            subscribedObjectiveDirector = null;
        }

        if (subscribedCoreTrackingController != null)
        {
            subscribedCoreTrackingController.ProgressChanged -= HandleObjectiveProgressChanged;
            subscribedCoreTrackingController = null;
        }

        if (subscribedRuntimeTraitStore != null)
        {
            subscribedRuntimeTraitStore.Changed -= HandleTraitCollectionChanged;
            subscribedRuntimeTraitStore = null;
        }

        if (subscribedPermanentProgress != null)
        {
            subscribedPermanentProgress.Changed -= HandleTraitCollectionChanged;
            subscribedPermanentProgress.Changed -= RefreshStoryRecovery;
            subscribedPermanentProgress = null;
        }

        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged -= HandleWalletChanged;
            subscribedRunManager.RunEnded -= HandleStoryRecoveryRunEnded;
            subscribedRunManager = null;
        }
        if (subscribedStoryLocalization != null)
        {
            subscribedStoryLocalization.LanguageChanged -= HandleStoryRecoveryLanguageChanged;
            subscribedStoryLocalization = null;
        }
    }

    private void HandleStoryRecoveryRunEnded(RunResultData _)
    {
        StopStoryRecoveryFeedback();
    }

    private void HandleStoryRecoveryLanguageChanged(string _)
    {
        RefreshStoryRecovery();
    }

    private void HandleHealthChanged(float current, float max)
    {
        RefreshShipSection();
    }

    private void HandlePlayerDied()
    {
        RefreshShipSection();
    }

    private void HandleCargoChanged(int current, int max)
    {
        RefreshShipSection();
    }

    private void HandleAutoPickupPreferenceChanged(CurrencyType currencyType, bool enabled)
    {
        RefreshCargoManagement();
    }

    private void HandleEquipmentChanged(ReinforcementDefinition definition, int currentCharges, int maxCharges)
    {
        RefreshActiveSection();
    }

    private void HandleChargesChanged(int currentCharges, int maxCharges, float rechargeRatio)
    {
        RefreshActiveSection();
    }

    private void HandleObjectiveProgressChanged(int current, int required)
    {
        RefreshShipSection();
    }

    private void HandleTraitCollectionChanged()
    {
        if (isOpen)
        {
            RebuildPassiveSection();
        }
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        RefreshShipSection();
    }

    private void CloseFromButton()
    {
        if (externalMenuControlsLifecycle)
        {
            CloseRequested?.Invoke();
            return;
        }

        suppressOpenUntilKeyReleased = holdTabToOpen && IsOpenKeyPressed();
        CloseInternal(true, true);
    }

    private void CloseFromCancel()
    {
        if (externalMenuControlsLifecycle)
        {
            CloseRequested?.Invoke();
            return;
        }

        suppressOpenUntilKeyReleased = holdTabToOpen && IsOpenKeyPressed();
        CloseInternal(true, true);
    }

    private void CloseInternal(bool playSound, bool restoreCursor)
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        StopStoryRecoveryFeedback();
        UnbindRuntimeEvents();

        if (!externalMenuControlsLifecycle && GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);

            if (pauseWhileOpen)
            {
                GameplayPauseManager.Instance.PopPause(this);
            }
        }

        if (!externalMenuControlsLifecycle && restoreCursor)
        {
            RestoreCursorState();
        }

        SetPanelVisible(false);

        if (playSound && !externalMenuControlsLifecycle)
        {
            AudioManager.Play(SoundEventIds.UiPanelClose);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (root != null && root != gameObject && visible)
        {
            root.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (!visible && deactivateVisualRootWhenClosed && root != null && root != gameObject)
        {
            root.SetActive(false);
        }
    }

    private void StoreAndApplyCursorState()
    {
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        storedCursorState = true;

        if (showCursorWhileOpen)
        {
            Cursor.visible = true;
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void RestoreCursorState()
    {
        if (!storedCursorState)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        storedCursorState = false;
    }

    private bool IsOpenKeyPressed()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.isPressed;
    }

    private bool WasOpenKeyPressedThisFrame()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.wasPressedThisFrame;
    }

    private bool ContainsTraitId(IReadOnlyList<string> traitIds, string targetId)
    {
        if (traitIds == null || string.IsNullOrWhiteSpace(targetId))
        {
            return false;
        }

        for (int i = 0; i < traitIds.Count; i++)
        {
            if (traitIds[i] == targetId)
            {
                return true;
            }
        }

        return false;
    }

    private float CalculateTotalChargeRatio(
        ReinforcementDefinition definition,
        int currentCharges,
        int maxCharges,
        float rechargeRatio)
    {
        if (definition == null)
        {
            return 0f;
        }

        maxCharges = Mathf.Max(1, maxCharges);
        currentCharges = Mathf.Clamp(currentCharges, 0, maxCharges);

        if (currentCharges >= maxCharges)
        {
            return 1f;
        }

        if (!definition.UsesRecharge)
        {
            return currentCharges / (float)maxCharges;
        }

        return Mathf.Clamp01((currentCharges + Mathf.Clamp01(rechargeRatio)) / maxCharges);
    }

    private void SetActiveChargeRatio(float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);

        if (activeChargeSlider != null)
        {
            activeChargeSlider.SetValueWithoutNotify(ratio);
        }

        if (activeChargeFillImage != null)
        {
            activeChargeFillImage.fillAmount = ratio;
            activeChargeFillImage.color = color;
        }
    }

    private string FormatTuningChipValue(int amount)
    {
        if (string.IsNullOrWhiteSpace(tuningChipValueFormat))
        {
            return $"{Mathf.Max(0, amount)}개";
        }

        try
        {
            return string.Format(tuningChipValueFormat, Mathf.Max(0, amount));
        }
        catch (FormatException)
        {
            return $"{Mathf.Max(0, amount)}개";
        }
    }

    private void SetImage(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
    }

    private void ConfigureInventoryTypography()
    {
        if (root == null)
        {
            return;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (uiFont != null)
            {
                text.font = uiFont;
            }

            text.raycastTarget = false;
        }
    }

    private void SetText(TextMeshProUGUI target, string text)
    {
        if (target != null)
        {
            target.text = text ?? string.Empty;
        }
    }

    private void SetColor(Graphic target, Color color)
    {
        if (target != null)
        {
            target.color = color;
        }
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private struct ActiveDisplayState
    {
        public ReinforcementDefinition definition;
        public int currentCharges;
        public int maxCharges;
        public float rechargeRatio;
        public float remainingSeconds;
        public bool isRecharging;
    }

    private ActiveDisplayState ResolveActiveDisplayState(RunContext run)
    {
        ActiveDisplayState state = new ActiveDisplayState();

        if (reinforcementController != null && reinforcementController.HasEquipment)
        {
            state.definition = reinforcementController.EquippedDefinition;
            state.currentCharges = reinforcementController.CurrentCharges;
            state.maxCharges = reinforcementController.MaxCharges;
            state.rechargeRatio = reinforcementController.RechargeRatio;
            state.remainingSeconds = reinforcementController.RechargeRemainingSeconds;
            state.isRecharging = reinforcementController.IsRecharging;
            return state;
        }

        if (run != null && run.HasEquippedReinforcement)
        {
            state.definition = FindReinforcement(run.EquippedReinforcementId);
            state.currentCharges = run.EquippedReinforcementCharges;
            state.maxCharges = state.definition != null ? state.definition.MaxCharges : 0;
            state.rechargeRatio = 0f;
            state.remainingSeconds = 0f;
            state.isRecharging = false;
            return state;
        }

        if (run == null && showDefaultActiveOutsideRun)
        {
            state.definition = FindReinforcement(ResolveDefaultReinforcementId(run));
            state.maxCharges = state.definition != null ? state.definition.MaxCharges : 0;
            state.currentCharges = state.definition != null && state.definition.StartWithFullCharges
                ? state.maxCharges
                : 0;
        }

        return state;
    }
}
