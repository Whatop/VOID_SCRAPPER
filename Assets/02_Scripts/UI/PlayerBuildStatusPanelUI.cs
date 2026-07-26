using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
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
    Passive
}

[DisallowMultipleComponent]
public class PlayerBuildStatusPanelUI : MonoBehaviour
{
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
    [SerializeField] private string coreTrackingTip = "CORE SIGNAL을 수집해 코어 위치를 추적하십시오.\n긴급복귀 시 보존 한도를 초과한 적재물은 손실됩니다.";
    [TextArea(2, 4)]
    [SerializeField] private string coreReadyTip = "코어 위치가 공개되었습니다.\n보스전에 진입하기 전에 체력과 적재량을 확인하십시오.";

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
    [Tooltip("Tab 상태창에서 선택 대상을 필드에 드랍하는 키입니다.")]
    [SerializeField] private Key fieldDropKey = Key.G;
    [Tooltip("액티브 슬롯 전체에 Button을 붙이고 연결하면 클릭으로 액티브를 드랍 대상으로 선택할 수 있습니다.")]
    [SerializeField] private Button activeSlotSelectButton;
    [SerializeField] private Button activeFieldDropButton;
    [SerializeField] private Button passiveFieldDropButton;
    [SerializeField] private TextMeshProUGUI fieldDropHintText;
    [SerializeField] private GameObject activeDropSelectedIndicator;
    [SerializeField] private TraitPickup traitDropPickupPrefab;
    [SerializeField] private Transform fieldDropOrigin;
    [SerializeField] private float fieldDropDistance = 1.25f;
    [SerializeField] private float fieldDropBlockSeconds = 0.5f;
    [SerializeField] private Vector2 fallbackFieldDropDirection = Vector2.down;

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
    private int selectedPassiveIndex = -1;
    private string selectedPassiveId;
    private BuildStatusFieldDropTarget selectedFieldDropTarget = BuildStatusFieldDropTarget.Active;

    private bool storedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private ExpeditionObjectiveDirector subscribedObjectiveDirector;
    private RunRuntimeTraitStore subscribedRuntimeTraitStore;
    private PermanentProgress subscribedPermanentProgress;
    private RunManager subscribedRunManager;

    public bool IsOpen => isOpen;

    private void OnValidate()
    {
        passiveColumnCount = Mathf.Max(1, passiveColumnCount);
    }

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
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
        ConfigurePassiveScrollView();
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
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

        if (blockOpenWhileAnotherPauseActive &&
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
        StoreAndApplyCursorState();
        SetPanelVisible(true);
        RefreshAll();

        if (pauseWhileOpen)
        {
            GameplayPauseManager.Instance.PushPause(this, "PlayerBuildStatusPanel");
        }

        GameplayPauseManager.Instance.RegisterCancelHandler(this, CloseFromCancel);
        AudioManager.Play(SoundEventIds.UiPanelOpen);
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
    }

    public void RefreshShipSection()
    {
        RunContext run = ResolveRunContext();
        ShipDefinition ship = FindShip(run != null ? run.SelectedShipId : null);
        WeaponTreeType weaponTree = ResolveWeaponTree(run, ship);

        // Tab 기체 슬롯은 월드용 무기 스프라이트가 아니라 ShipDefinition의 전용 PreviewSprite를 사용합니다.
        Sprite shipSprite = ship != null ? ship.PreviewSprite : null;

        SetImage(shipPreviewImage, shipSprite != null ? shipSprite : fallbackShipIcon);
        SetText(shipNameText, ship != null ? ship.DisplayName : "현재 기체");
        SetText(shipWeaponText, GetWeaponTreeDisplayName(weaponTree));
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

        ExpeditionObjectiveDirector objectiveDirector = ExpeditionObjectiveDirector.Instance;
        int signalCount = objectiveDirector != null
            ? objectiveDirector.SignalCount
            : run != null ? run.ObjectiveSignalCount : 0;

        int signalRequired = objectiveDirector != null
            ? objectiveDirector.SignalsRequiredToRevealCore
            : 2;

        bool coreReady = objectiveDirector != null
            ? objectiveDirector.CoreRevealed
            : signalCount >= signalRequired;

        SetText(coreSignalValueText, $"{signalCount}/{signalRequired}");
        SetColor(coreSignalValueText, coreReady ? coreReadyColor : normalStatColor);

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
        SetColor(cargoValueText, normalStatColor);
        SetColor(tuningChipValueText, normalStatColor);
        SetColor(emergencyReturnValueText, normalStatColor);
        SetText(shipTipText, coreReady ? coreReadyTip : coreTrackingTip);
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
            SetText(activeCooldownText, "재사용 대기시간 : -");
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
            ? $"재사용 대기시간 : {definition.RechargeSeconds:0.#}초"
            : "재사용 대기시간 : 없음";

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
                ? $"보유 {ownedCount}개"
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

                if (!CanDisplayTrait(trait, selectedWeaponTree) || !progress.IsTraitActive(trait.TraitId))
                {
                    continue;
                }

                PassiveEntry entry = GetOrCreatePassiveEntry(trait, order++);
                entry.permanentLevel = Mathf.Clamp(progress.GetTraitLevel(trait.TraitId), 0, trait.MaxLevel);
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

        if (!includeHiddenTraits && trait.IsHidden)
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
            return $"영구 Lv.{entry.permanentLevel} · 탐사 Lv.{entry.runtimeLevel}";
        }

        if (entry.runtimeLevel > 0)
        {
            return $"탐사 Lv.{entry.runtimeLevel}/{entry.trait.MaxLevel}";
        }

        return $"영구 Lv.{entry.permanentLevel}/{entry.trait.MaxLevel}";
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

    private string BuildTraitEffectsUpToLevel(TraitDefinition trait, int level)
    {
        return BuildTraitEffectsCombined(trait, level, 0);
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
        else
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
        if (Keyboard.current == null)
        {
            return;
        }

        KeyControl keyControl = Keyboard.current[fieldDropKey];

        if (keyControl != null && keyControl.wasPressedThisFrame)
        {
            TryDropSelectedFieldItem();
        }
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

        if (fieldDropHintText == null)
        {
            return;
        }

        if (activeSelected)
        {
            string activeName = reinforcementController != null && reinforcementController.EquippedDefinition != null
                ? reinforcementController.EquippedDefinition.DisplayName
                : "장비 없음";
            fieldDropHintText.text = $"[{fieldDropKey}] 액티브 필드 드랍 : {activeName}";
            return;
        }

        string passiveName = selectedPassiveIndex >= 0 && selectedPassiveIndex < passiveEntries.Count && passiveEntries[selectedPassiveIndex]?.trait != null
            ? passiveEntries[selectedPassiveIndex].trait.DisplayName
            : "패시브 미선택";
        fieldDropHintText.text = $"[{fieldDropKey}] 패시브 필드 드랍 : {passiveName}";
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
            playerHealth.Damaged += HandleHealthChanged;
            playerHealth.Healed += HandleHealthChanged;
            playerHealth.Died += HandlePlayerDied;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged += HandleCargoChanged;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged += HandleEquipmentChanged;
            reinforcementController.ChargesChanged += HandleChargesChanged;
        }

        subscribedObjectiveDirector = ExpeditionObjectiveDirector.Instance;
        if (subscribedObjectiveDirector != null)
        {
            subscribedObjectiveDirector.ProgressChanged += HandleObjectiveProgressChanged;
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
        }

        subscribedRunManager = RunManager.Instance;
        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged += HandleWalletChanged;
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
            playerHealth.Damaged -= HandleHealthChanged;
            playerHealth.Healed -= HandleHealthChanged;
            playerHealth.Died -= HandlePlayerDied;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
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

        if (subscribedRuntimeTraitStore != null)
        {
            subscribedRuntimeTraitStore.Changed -= HandleTraitCollectionChanged;
            subscribedRuntimeTraitStore = null;
        }

        if (subscribedPermanentProgress != null)
        {
            subscribedPermanentProgress.Changed -= HandleTraitCollectionChanged;
            subscribedPermanentProgress = null;
        }

        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged -= HandleWalletChanged;
            subscribedRunManager = null;
        }
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
        suppressOpenUntilKeyReleased = holdTabToOpen && IsOpenKeyPressed();
        CloseInternal(true, true);
    }

    private void CloseFromCancel()
    {
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
        UnbindRuntimeEvents();

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);

            if (pauseWhileOpen)
            {
                GameplayPauseManager.Instance.PopPause(this);
            }
        }

        if (restoreCursor)
        {
            RestoreCursorState();
        }

        SetPanelVisible(false);

        if (playSound)
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
