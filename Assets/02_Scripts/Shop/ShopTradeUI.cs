using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopTradeUI : MonoBehaviour
{
    private enum ShopOptionKind
    {
        None,
        Repair,
        Reinforcement,
        Trait
    }

    private enum ShopVisualState
    {
        Friendly,
        Neutral,
        Warning
    }

    private struct ShopOption
    {
        public ShopOptionKind Kind;
        public string Title;
        public string ConditionText;
        public string DescriptionText;
        public int Cost;
        public Sprite Icon;
        public TraitDefinition Trait;
        public ReinforcementDefinition Reinforcement;
    }

    [Header("루트 / 페이지")]
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject tradePageRoot;
    [SerializeField] private GameObject maintenancePageRoot;
    [SerializeField] private Button tradeTabButton;
    [SerializeField] private Button maintenanceTabButton;

    [Header("일시정지")]
    [SerializeField] private bool pauseGameWhileOpen = true;

    [Header("상점 상태 표시 - 로직 없음")]
    [SerializeField] private Image stateDotImage;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private ShopVisualState visualState = ShopVisualState.Neutral;
    [SerializeField] private Color friendlyColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color neutralColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0.15f);

    [Header("왼쪽 상세 정보")]
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TextMeshProUGUI selectedItemLabelText;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI conditionText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("거래 탭 - 수리 1개")]
    [SerializeField] private Button repairButton;
    [SerializeField] private Image repairButtonIcon;
    [SerializeField] private TextMeshProUGUI repairButtonText;
    [SerializeField] private TextMeshProUGUI repairButtonNameText;
    [SerializeField] private TextMeshProUGUI repairButtonPriceText;
    [SerializeField] private Sprite repairIcon;

    [Header("거래 탭 - Reinforcement 2개")]
    [SerializeField] private Button[] reinforcementButtons = new Button[2];
    [SerializeField] private Image[] reinforcementButtonIcons = new Image[2];
    [SerializeField] private TextMeshProUGUI[] reinforcementButtonTexts = new TextMeshProUGUI[2];
    [SerializeField] private TextMeshProUGUI[] reinforcementButtonNameTexts = new TextMeshProUGUI[2];
    [SerializeField] private TextMeshProUGUI[] reinforcementButtonPriceTexts = new TextMeshProUGUI[2];

    [Header("거래 탭 - Trait 3개")]
    [SerializeField] private Button[] traitButtons = new Button[3];
    [SerializeField] private Image[] traitButtonIcons = new Image[3];
    [SerializeField] private TextMeshProUGUI[] traitButtonTexts = new TextMeshProUGUI[3];
    [SerializeField] private TextMeshProUGUI[] traitButtonNameTexts = new TextMeshProUGUI[3];
    [SerializeField] private TextMeshProUGUI[] traitButtonPriceTexts = new TextMeshProUGUI[3];

    [Header("하단 - 현재 액티브")]
    [SerializeField] private Image currentActiveIconImage;
    [SerializeField] private TextMeshProUGUI currentActiveNameText;
    [SerializeField] private TextMeshProUGUI currentActiveTypeText;

    [Header("하단 버튼")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;
    [SerializeField] private string buyText = "구매";
    [SerializeField] private string cannotBuyText = "구매 불가";

    [Header("기본 이미지")]
    [SerializeField] private Sprite activeFallbackIcon;
    [SerializeField] private Sprite traitFallbackIcon;

    [Header("정비소 UI")]
    [SerializeField] private ShopMaintenanceBayUI maintenanceBayUI;

    private ShopStructure currentShop;
    private ShopStockController currentStock;
    private ShopActiveMaintenanceBay currentMaintenanceBay;
    private GameObject currentPlayer;
    private PlayerReinforcementController currentReinforcementController;

    private readonly List<TraitDefinition> traitChoices = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> reinforcementChoices = new List<ReinforcementDefinition>();

    private ShopOption selectedOption;
    private bool isOpen;
    private bool pauseRequested;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        BindButtons();

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnDisable()
    {
        ReleasePause();
    }

    private void OnDestroy()
    {
        ReleasePause();
        UnbindButtons();
    }

    public void Open(ShopStructure shop, GameObject playerObject)
    {
        AudioManager.Play(SoundEventIds.ShopOpen);
        currentShop = shop;
        currentPlayer = playerObject;

        currentStock = shop != null ? shop.GetComponent<ShopStockController>() : null;
        currentMaintenanceBay = shop != null ? shop.ActiveMaintenanceBay : null;

        currentReinforcementController = currentPlayer != null
            ? currentPlayer.GetComponentInChildren<PlayerReinforcementController>(true)
            : null;

        isOpen = true;

        if (root != null)
        {
            root.SetActive(true);
        }

        RequestPause();
        ApplyTopStateVisual();
        RollShopItems();
        selectedOption = default;
        RefreshTradePage();
        ShowTradePage();
    }

    public void Close()
    {
        AudioManager.Play(SoundEventIds.UiPanelClose);
        isOpen = false;

        if (maintenanceBayUI != null)
        {
            maintenanceBayUI.Close();
        }

        currentShop = null;
        currentStock = null;
        currentMaintenanceBay = null;
        currentPlayer = null;
        currentReinforcementController = null;

        traitChoices.Clear();
        reinforcementChoices.Clear();
        selectedOption = default;

        if (root != null)
        {
            root.SetActive(false);
        }

        ReleasePause();
    }

    private void BindButtons()
    {
        if (tradeTabButton != null)
        {
            tradeTabButton.onClick.RemoveListener(ShowTradePage);
            tradeTabButton.onClick.AddListener(ShowTradePage);
        }

        if (maintenanceTabButton != null)
        {
            maintenanceTabButton.onClick.RemoveListener(ShowMaintenancePage);
            maintenanceTabButton.onClick.AddListener(ShowMaintenancePage);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnClickBuy);
            buyButton.onClick.AddListener(OnClickBuy);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
            exitButton.onClick.AddListener(Close);
        }

        if (repairButton != null)
        {
            repairButton.onClick.RemoveAllListeners();
            repairButton.onClick.AddListener(() => SelectOption(BuildRepairOption()));
        }

        for (int i = 0; i < reinforcementButtons.Length; i++)
        {
            int captured = i;

            if (reinforcementButtons[captured] != null)
            {
                reinforcementButtons[captured].onClick.RemoveAllListeners();
                reinforcementButtons[captured].onClick.AddListener(() => OnClickReinforcementSlot(captured));
            }
        }

        for (int i = 0; i < traitButtons.Length; i++)
        {
            int captured = i;

            if (traitButtons[captured] != null)
            {
                traitButtons[captured].onClick.RemoveAllListeners();
                traitButtons[captured].onClick.AddListener(() => OnClickTraitSlot(captured));
            }
        }

        if (maintenanceBayUI != null)
        {
            maintenanceBayUI.ExitRequested -= Close;
            maintenanceBayUI.ExitRequested += Close;
        }
    }

    private void UnbindButtons()
    {
        if (tradeTabButton != null)
        {
            tradeTabButton.onClick.RemoveListener(ShowTradePage);
        }

        if (maintenanceTabButton != null)
        {
            maintenanceTabButton.onClick.RemoveListener(ShowMaintenancePage);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnClickBuy);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
        }

        if (maintenanceBayUI != null)
        {
            maintenanceBayUI.ExitRequested -= Close;
        }
    }

    private void ShowTradePage()
    {
        if (!isOpen)
        {
            return;
        }

        if (tradePageRoot != null)
        {
            tradePageRoot.SetActive(true);
        }

        if (maintenancePageRoot != null)
        {
            maintenancePageRoot.SetActive(false);
        }

        if (maintenanceBayUI != null)
        {
            maintenanceBayUI.Close();
        }

        RefreshCurrentActiveBar();
    }

    private void ShowMaintenancePage()
    {
        if (!isOpen)
        {
            return;
        }

        if (tradePageRoot != null)
        {
            tradePageRoot.SetActive(false);
        }

        if (maintenancePageRoot != null)
        {
            maintenancePageRoot.SetActive(true);
        }

        if (maintenanceBayUI != null)
        {
            maintenanceBayUI.Open(currentShop, currentPlayer, currentMaintenanceBay);
        }
    }

    private void RollShopItems()
    {
        traitChoices.Clear();
        reinforcementChoices.Clear();

        if (currentStock == null)
        {
            return;
        }

        List<ReinforcementDefinition> rolledReinforcements = currentStock.RollReinforcementChoices();
        List<TraitDefinition> rolledTraits = currentStock.RollTraitChoices();

        for (int i = 0; i < rolledReinforcements.Count && i < 2; i++)
        {
            if (rolledReinforcements[i] != null)
            {
                reinforcementChoices.Add(rolledReinforcements[i]);
            }
        }

        for (int i = 0; i < rolledTraits.Count && i < 3; i++)
        {
            if (rolledTraits[i] != null)
            {
                traitChoices.Add(rolledTraits[i]);
            }
        }
    }

    private void RefreshTradePage()
    {
        RefreshRepairSlot();
        RefreshReinforcementSlots();
        RefreshTraitSlots();
        RefreshCurrentActiveBar();

        if (selectedOption.Kind == ShopOptionKind.None)
        {
            SelectFirstAvailableOption();
        }
        else
        {
            RefreshBuyButtonState();
        }
    }

    private void SelectFirstAvailableOption()
    {
        if (currentShop != null)
        {
            SelectOption(BuildRepairOption());
            return;
        }

        if (reinforcementChoices.Count > 0)
        {
            SelectOption(BuildReinforcementOption(reinforcementChoices[0]));
            return;
        }

        if (traitChoices.Count > 0)
        {
            SelectOption(BuildTraitOption(traitChoices[0]));
            return;
        }

        SelectEmptyOption();
    }

    private void RefreshRepairSlot()
    {
        if (repairButton != null)
        {
            repairButton.gameObject.SetActive(currentShop != null);
        }

        SetIcon(repairButtonIcon, repairIcon);

        string name = "수리";
        string price = currentShop != null ? $"{currentShop.RepairCost}C" : string.Empty;
        SetCardText(repairButtonText, repairButtonNameText, repairButtonPriceText, name, price);
    }

    private void RefreshReinforcementSlots()
    {
        for (int i = 0; i < reinforcementButtons.Length; i++)
        {
            ReinforcementDefinition definition = i < reinforcementChoices.Count ? reinforcementChoices[i] : null;
            bool visible = definition != null;

            if (reinforcementButtons[i] != null)
            {
                reinforcementButtons[i].gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetIcon(GetArrayItem(reinforcementButtonIcons, i), null);
                ClearCardText(
                    GetArrayItem(reinforcementButtonTexts, i),
                    GetArrayItem(reinforcementButtonNameTexts, i),
                    GetArrayItem(reinforcementButtonPriceTexts, i)
                );
                continue;
            }

            Sprite icon = definition.Icon != null ? definition.Icon : activeFallbackIcon;
            int cost = currentStock != null ? currentStock.GetReinforcementCost(definition) : definition.Cost;

            SetIcon(GetArrayItem(reinforcementButtonIcons, i), icon);
            SetCardText(
                GetArrayItem(reinforcementButtonTexts, i),
                GetArrayItem(reinforcementButtonNameTexts, i),
                GetArrayItem(reinforcementButtonPriceTexts, i),
                definition.DisplayName,
                $"{cost}C"
            );
        }
    }

    private void RefreshTraitSlots()
    {
        for (int i = 0; i < traitButtons.Length; i++)
        {
            TraitDefinition definition = i < traitChoices.Count ? traitChoices[i] : null;
            bool visible = definition != null;

            if (traitButtons[i] != null)
            {
                traitButtons[i].gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetIcon(GetArrayItem(traitButtonIcons, i), null);
                ClearCardText(
                    GetArrayItem(traitButtonTexts, i),
                    GetArrayItem(traitButtonNameTexts, i),
                    GetArrayItem(traitButtonPriceTexts, i)
                );
                continue;
            }

            Sprite icon = definition.Icon != null ? definition.Icon : traitFallbackIcon;
            int cost = currentStock != null ? currentStock.TraitCost : 0;

            SetIcon(GetArrayItem(traitButtonIcons, i), icon);
            SetCardText(
                GetArrayItem(traitButtonTexts, i),
                GetArrayItem(traitButtonNameTexts, i),
                GetArrayItem(traitButtonPriceTexts, i),
                definition.DisplayName,
                $"{cost}C"
            );
        }
    }

    private void RefreshCurrentActiveBar()
    {
        currentReinforcementController = currentPlayer != null
            ? currentPlayer.GetComponentInChildren<PlayerReinforcementController>(true)
            : currentReinforcementController;

        ReinforcementDefinition currentActive = currentReinforcementController != null
            ? currentReinforcementController.EquippedDefinition
            : null;

        Sprite icon = currentActive != null && currentActive.Icon != null
            ? currentActive.Icon
            : activeFallbackIcon;

        SetIcon(currentActiveIconImage, icon);

        if (currentActiveNameText != null)
        {
            currentActiveNameText.text = currentActive != null ? currentActive.DisplayName : "장착 없음";
        }

        if (currentActiveTypeText != null)
        {
            currentActiveTypeText.text = currentActive != null ? currentActive.GetUseTypeText() : string.Empty;
        }
    }

    private void OnClickReinforcementSlot(int index)
    {
        if (index < 0 || index >= reinforcementChoices.Count)
        {
            return;
        }

        SelectOption(BuildReinforcementOption(reinforcementChoices[index]));
    }

    private void OnClickTraitSlot(int index)
    {
        if (index < 0 || index >= traitChoices.Count)
        {
            return;
        }

        SelectOption(BuildTraitOption(traitChoices[index]));
    }

    private void SelectOption(ShopOption option)
    {
        selectedOption = option;

        SetIcon(detailIconImage, option.Icon);

        SetText(selectedItemLabelText, "선택된 아이템");
        SetText(itemNameText, option.Title);
        SetText(conditionText, option.ConditionText);
        SetText(bodyText, option.DescriptionText);
        SetText(priceText, option.Cost > 0 ? $"{option.Cost}C" : "무료");

        RefreshBuyButtonState();
    }

    private void SelectEmptyOption()
    {
        selectedOption = default;

        SetIcon(detailIconImage, null);
        SetText(selectedItemLabelText, "선택된 아이템");
        SetText(itemNameText, "상품 없음");
        SetText(conditionText, string.Empty);
        SetText(bodyText, "구매 가능한 상품이 없습니다.");
        SetText(priceText, string.Empty);

        RefreshBuyButtonState();
    }

    private void RefreshBuyButtonState()
    {
        bool canBuy = CanBuySelectedOption();

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
        }

        if (buyButtonText != null)
        {
            buyButtonText.text = canBuy ? buyText : cannotBuyText;
        }
    }

    private bool CanBuySelectedOption()
    {
        switch (selectedOption.Kind)
        {
            case ShopOptionKind.Repair:
                return currentShop != null && currentShop.CanBuyRepair(currentPlayer);

            case ShopOptionKind.Reinforcement:
                return currentStock != null &&
                       currentStock.CanBuyReinforcement(selectedOption.Reinforcement, currentShop, currentPlayer);

            case ShopOptionKind.Trait:
                return currentStock != null && currentStock.CanBuyTrait(selectedOption.Trait);

            default:
                return false;
        }
    }

    private void OnClickBuy()
    {
        if (!CanBuySelectedOption())
        {
            AudioManager.Play(SoundEventIds.ShopBuyFail);
            RefreshBuyButtonState();
            return;
        }

        bool success = false;

        switch (selectedOption.Kind)
        {
            case ShopOptionKind.Repair:
                success = currentShop != null && currentShop.TryBuyRepair(currentPlayer);
                break;

            case ShopOptionKind.Reinforcement:
                success = TryBuyReinforcement();
                break;

            case ShopOptionKind.Trait:
                success = currentStock != null &&
                          currentStock.TryBuyTrait(currentShop, selectedOption.Trait, currentPlayer);
                break;
        }

        if (!success)
        {
            AudioManager.Play(SoundEventIds.ShopBuyFail);
            RefreshBuyButtonState();
            return;
        }

        AudioManager.Play(SoundEventIds.ShopBuySuccess);

        if (selectedOption.Kind == ShopOptionKind.Reinforcement)
        {
            RemoveReinforcementFromChoices(selectedOption.Reinforcement);
            selectedOption = default;
        }
        else if (selectedOption.Kind == ShopOptionKind.Trait)
        {
            RemoveTraitFromChoices(selectedOption.Trait);
            selectedOption = default;
        }

        RefreshTradePage();

        if (maintenanceBayUI != null && maintenanceBayUI.IsOpen)
        {
            maintenanceBayUI.Open(currentShop, currentPlayer, currentMaintenanceBay);
        }
    }

    private bool TryBuyReinforcement()
    {
        if (selectedOption.Reinforcement == null || currentStock == null)
        {
            return false;
        }

        bool purchased = currentStock.TryBuyReinforcement(
            currentShop,
            selectedOption.Reinforcement,
            currentPlayer
        );

        if (!purchased)
        {
            return false;
        }

        currentReinforcementController = currentPlayer != null
            ? currentPlayer.GetComponentInChildren<PlayerReinforcementController>(true)
            : null;

        RefreshCurrentActiveBar();
        return true;
    }

    private ShopOption BuildRepairOption()
    {
        return new ShopOption
        {
            Kind = ShopOptionKind.Repair,
            Title = "수리",
            ConditionText = "정비 서비스",
            DescriptionText = currentShop != null
                ? $"기체 체력을 {currentShop.RepairAmount:0.#} 회복한다."
                : "기체 체력을 회복한다.",
            Cost = currentShop != null ? currentShop.RepairCost : 0,
            Icon = repairIcon,
            Trait = null,
            Reinforcement = null
        };
    }

    private ShopOption BuildReinforcementOption(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return default;
        }

        int cost = currentStock != null ? currentStock.GetReinforcementCost(definition) : definition.Cost;

        return new ShopOption
        {
            Kind = ShopOptionKind.Reinforcement,
            Title = definition.DisplayName,
            ConditionText = $"{definition.GetRarityText()} / {definition.GetUseTypeText()}",
            DescriptionText =
                $"등급: {definition.GetRarityText()}\n" +
                $"{definition.GetAvailabilityText()}\n\n" +
                $"{definition.Description}\n\n" +
                $"효과\n{definition.BuildEffectSummary()}",
            Cost = cost,
            Icon = definition.Icon != null ? definition.Icon : activeFallbackIcon,
            Trait = null,
            Reinforcement = definition
        };
    }

    private ShopOption BuildTraitOption(TraitDefinition definition)
    {
        if (definition == null)
        {
            return default;
        }

        return new ShopOption
        {
            Kind = ShopOptionKind.Trait,
            Title = definition.DisplayName,
            ConditionText = definition.GetCategoryText(),
            DescriptionText = definition.Description,
            Cost = currentStock != null ? currentStock.TraitCost : 0,
            Icon = definition.Icon != null ? definition.Icon : traitFallbackIcon,
            Trait = definition,
            Reinforcement = null
        };
    }

    private void RemoveReinforcementFromChoices(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        for (int i = reinforcementChoices.Count - 1; i >= 0; i--)
        {
            if (reinforcementChoices[i] == null ||
                reinforcementChoices[i].EquipmentId == definition.EquipmentId)
            {
                reinforcementChoices.RemoveAt(i);
            }
        }
    }

    private void RemoveTraitFromChoices(TraitDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        for (int i = traitChoices.Count - 1; i >= 0; i--)
        {
            if (traitChoices[i] == null ||
                traitChoices[i].TraitId == definition.TraitId)
            {
                traitChoices.RemoveAt(i);
            }
        }
    }

    private void RequestPause()
    {
        if (!pauseGameWhileOpen || pauseRequested)
        {
            return;
        }

        pauseRequested = true;
        GameplayPauseManager.Instance.PushPause(this, "ShopTradeUI");
        GameplayPauseManager.Instance.RegisterCancelHandler(this, Close);
    }

    private void ReleasePause()
    {
        if (!pauseRequested)
        {
            return;
        }

        pauseRequested = false;

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);
            GameplayPauseManager.Instance.PopPause(this);
        }
    }

    private void ApplyTopStateVisual()
    {
        string stateLabel;
        Color stateColor;

        switch (visualState)
        {
            case ShopVisualState.Friendly:
                stateLabel = "친화 상태";
                stateColor = friendlyColor;
                break;

            case ShopVisualState.Warning:
                stateLabel = "경고 상태";
                stateColor = warningColor;
                break;

            default:
                stateLabel = "중립 상태";
                stateColor = neutralColor;
                break;
        }

        SetText(stateText, stateLabel);

        if (stateDotImage != null)
        {
            stateDotImage.color = stateColor;
        }
    }

    private void SetCardText(
        TextMeshProUGUI legacyCombinedText,
        TextMeshProUGUI nameText,
        TextMeshProUGUI priceTextField,
        string nameValue,
        string priceValue)
    {
        bool hasSeparateText = nameText != null || priceTextField != null;

        if (nameText != null)
        {
            nameText.text = nameValue;
        }

        if (priceTextField != null)
        {
            priceTextField.text = priceValue;
        }

        if (legacyCombinedText != null)
        {
            legacyCombinedText.text = hasSeparateText
                ? nameValue
                : string.IsNullOrWhiteSpace(priceValue) ? nameValue : $"{nameValue}\n{priceValue}";
        }
    }

    private void ClearCardText(
        TextMeshProUGUI legacyCombinedText,
        TextMeshProUGUI nameText,
        TextMeshProUGUI priceTextField)
    {
        SetText(legacyCombinedText, string.Empty);
        SetText(nameText, string.Empty);
        SetText(priceTextField, string.Empty);
    }

    private void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = value;
    }

    private T GetArrayItem<T>(T[] array, int index) where T : UnityEngine.Object
    {
        if (array == null || index < 0 || index >= array.Length)
        {
            return null;
        }

        return array[index];
    }
}
