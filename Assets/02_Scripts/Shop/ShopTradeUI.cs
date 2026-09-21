using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShopOptionSelectionRelay : MonoBehaviour, ISelectHandler
{
    private ShopTradeUI owner;
    private Button targetButton;

    public void Configure(ShopTradeUI newOwner, Button newTargetButton)
    {
        owner = newOwner;
        targetButton = newTargetButton;
    }

    public void OnSelect(BaseEventData eventData)
    {
        owner?.HandleOptionFocused(targetButton);
    }
}

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
        public string RarityText;
        public string CategoryText;
        public Color RarityColor;
        public bool HasRarity;
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

    private IShopTradeSession currentShop;
    private ShopStockController currentStock;
    private ShopActiveMaintenanceBay currentMaintenanceBay;
    private GameObject currentPlayer;
    private PlayerReinforcementController currentReinforcementController;

    private readonly List<TraitDefinition> traitChoices = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> reinforcementChoices = new List<ReinforcementDefinition>();

    private ShopOption selectedOption;
    private Button selectedOptionButton;
    private bool isOpen;
    private bool missingPresentationReported;
    private bool pauseRequested;
    private bool shopAudioModeRequested;
    private Color itemNameBaseColor = Color.white;
    private Color conditionBaseColor = Color.white;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }


        if (itemNameText != null)
        {
            itemNameBaseColor = itemNameText.color;
        }

        if (conditionText != null)
        {
            conditionBaseColor = conditionText.color;
            conditionText.richText = true;
        }

        BindButtons();
        ConfigureOptionSelectionRelays();
        ConfigureExplicitResultButtonSound(buyButton);
        ConfigureExplicitResultButtonSound(exitButton);

        if (root != null)
        {
            root.SetActive(false);
        }
    }


    private void OnDisable()
    {
        isOpen = false;
        ReleaseShopAudioMode();
        ReleasePause();
    }

    private void OnDestroy()
    {
        ReleaseShopAudioMode();
        ReleasePause();
        UnbindButtons();
    }

    public void Open(IShopTradeSession shop, GameObject playerObject)
    {
        if (root == null || buyButton == null || exitButton == null)
        {
            if (!missingPresentationReported)
            {
                missingPresentationReported = true;
                Debug.LogWarning($"[ShopTradeUI] Missing root/buyButton/exitButton at '{ShopPath(transform)}', scene '{gameObject.scene.path}'. Restore these authored Inspector bindings. Shop was skipped before pause/audio ownership or transactions.", this);
            }
            return;
        }
        bool wasOpen = isOpen;

        if (!wasOpen)
        {
            AudioManager.Play(SoundEventIds.ShopOpen);
            RequestShopAudioMode();
        }

        currentShop = shop;
        currentPlayer = playerObject;

        currentStock = shop != null ? shop.StockController : null;
        currentMaintenanceBay = shop != null ? shop.ActiveMaintenanceBay : null;

        if (maintenanceTabButton != null)
        {
            maintenanceTabButton.gameObject.SetActive(
                shop != null &&
                shop.MaintenanceOwner != null &&
                currentMaintenanceBay != null
            );
        }

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
        selectedOptionButton = null;
        RefreshTradePage();
        ShowTradePage();
    }

    private static string ShopPath(Transform target) => target.parent != null ? ShopPath(target.parent) + "/" + target.name : target.name;

    public void CloseIfSession(IShopTradeSession session)
    {
        if (session != null && ReferenceEquals(currentShop, session))
        {
            Close();
        }
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        AudioManager.Play(SoundEventIds.UiPanelClose);
        isOpen = false;
        ReleaseShopAudioMode();

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
        selectedOptionButton = null;

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
            repairButton.onClick.AddListener(() => SelectOption(BuildRepairOption(), repairButton));
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
        SelectEventSystemButton(selectedOptionButton);
    }

    private void ShowMaintenancePage()
    {
        if (!isOpen ||
            currentShop == null ||
            currentShop.MaintenanceOwner == null ||
            currentMaintenanceBay == null)
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
            maintenanceBayUI.Open(
                currentShop.MaintenanceOwner,
                currentPlayer,
                currentMaintenanceBay
            );
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
        if (currentShop != null &&
            currentShop.SupportsRepair &&
            currentShop.CanBuyRepair(currentPlayer))
        {
            SelectOption(BuildRepairOption(), repairButton);
            SelectEventSystemButton(repairButton);
            return;
        }

        for (int i = 0; i < reinforcementChoices.Count; i++)
        {
            ReinforcementDefinition definition = reinforcementChoices[i];

            if (currentStock != null &&
                currentStock.CanBuyReinforcement(definition, currentShop, currentPlayer))
            {
                Button targetButton = GetArrayItem(reinforcementButtons, i);
                SelectOption(BuildReinforcementOption(definition), targetButton);
                SelectEventSystemButton(targetButton);
                return;
            }
        }

        for (int i = 0; i < traitChoices.Count; i++)
        {
            TraitDefinition definition = traitChoices[i];

            if (currentStock != null && currentStock.CanBuyTrait(definition))
            {
                Button targetButton = GetArrayItem(traitButtons, i);
                SelectOption(BuildTraitOption(definition), targetButton);
                SelectEventSystemButton(targetButton);
                return;
            }
        }

        if (currentShop != null && currentShop.SupportsRepair)
        {
            SelectOption(BuildRepairOption(), repairButton);
            SelectEventSystemButton(repairButton);
            return;
        }

        if (reinforcementChoices.Count > 0)
        {
            Button targetButton = GetArrayItem(reinforcementButtons, 0);
            SelectOption(BuildReinforcementOption(reinforcementChoices[0]), targetButton);
            SelectEventSystemButton(targetButton);
            return;
        }

        if (traitChoices.Count > 0)
        {
            Button targetButton = GetArrayItem(traitButtons, 0);
            SelectOption(BuildTraitOption(traitChoices[0]), targetButton);
            SelectEventSystemButton(targetButton);
            return;
        }

        SelectEmptyOption();
    }

    private void RefreshRepairSlot()
    {
        if (repairButton != null)
        {
            repairButton.gameObject.SetActive(
                currentShop != null && currentShop.SupportsRepair
            );
        }

        SetIcon(repairButtonIcon, repairIcon);

        string name = "수리";
        bool supportsRepair = currentShop != null && currentShop.SupportsRepair;
        int cost = supportsRepair ? currentShop.RepairCost : 0;
        bool affordable = supportsRepair && ShopRunBridge.CanSpendCredits(cost);
        string price = supportsRepair
            ? BuildCardPriceText(cost, false, false, affordable)
            : string.Empty;
        SetCardText(repairButtonText, repairButtonNameText, repairButtonPriceText, name, price);
        ApplyCardVisualState(
            repairButtonIcon,
            repairButtonText,
            repairButtonNameText,
            repairButtonPriceText,
            false,
            affordable,
            Color.white,
            false
        );
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
            bool purchased = currentStock != null && currentStock.WasReinforcementPurchased(definition);
            bool soldOut = currentStock != null && currentStock.ReinforcementSoldOut;
            bool affordable = ShopRunBridge.CanSpendCredits(cost);

            SetIcon(GetArrayItem(reinforcementButtonIcons, i), icon);
            SetCardText(
                GetArrayItem(reinforcementButtonTexts, i),
                GetArrayItem(reinforcementButtonNameTexts, i),
                GetArrayItem(reinforcementButtonPriceTexts, i),
                definition.DisplayName,
                BuildCardPriceText(cost, purchased, soldOut, affordable)
            );
            ApplyCardVisualState(
                GetArrayItem(reinforcementButtonIcons, i),
                GetArrayItem(reinforcementButtonTexts, i),
                GetArrayItem(reinforcementButtonNameTexts, i),
                GetArrayItem(reinforcementButtonPriceTexts, i),
                purchased || soldOut,
                affordable,
                definition.GetRarityColor(),
                true
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
            bool purchased = currentStock != null && currentStock.WasTraitPurchased(definition);
            bool soldOut = currentStock != null && currentStock.TraitSoldOut;
            bool affordable = ShopRunBridge.CanSpendCredits(cost);

            SetIcon(GetArrayItem(traitButtonIcons, i), icon);
            SetCardText(
                GetArrayItem(traitButtonTexts, i),
                GetArrayItem(traitButtonNameTexts, i),
                GetArrayItem(traitButtonPriceTexts, i),
                definition.DisplayName,
                BuildCardPriceText(cost, purchased, soldOut, affordable)
            );
            ApplyCardVisualState(
                GetArrayItem(traitButtonIcons, i),
                GetArrayItem(traitButtonTexts, i),
                GetArrayItem(traitButtonNameTexts, i),
                GetArrayItem(traitButtonPriceTexts, i),
                purchased || soldOut,
                affordable,
                definition.GetRarityColor(),
                true
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
            currentActiveTypeText.text = currentActive != null
                ? $"장착 중 · {currentActive.GetUseTypeText()}"
                : string.Empty;
        }
    }

    private void OnClickReinforcementSlot(int index)
    {
        if (index < 0 || index >= reinforcementChoices.Count)
        {
            return;
        }

        SelectOption(BuildReinforcementOption(reinforcementChoices[index]), GetArrayItem(reinforcementButtons, index));
    }

    private void OnClickTraitSlot(int index)
    {
        if (index < 0 || index >= traitChoices.Count)
        {
            return;
        }

        SelectOption(BuildTraitOption(traitChoices[index]), GetArrayItem(traitButtons, index));
    }

    private void SelectOption(ShopOption option, Button sourceButton = null)
    {
        selectedOption = option;
        selectedOptionButton = sourceButton;

        SetIcon(detailIconImage, option.Icon);

        SetText(selectedItemLabelText, "선택된 아이템");
        SetText(itemNameText, option.Title);
        ApplySelectedOptionRarity(option);
        SetText(bodyText, option.DescriptionText);
        SetText(priceText, option.Cost > 0 ? $"{option.Cost}C" : "무료");

        RefreshBuyButtonState();
    }

    public void HandleOptionFocused(Button targetButton)
    {
        if (!isOpen || targetButton == null || !targetButton.gameObject.activeInHierarchy)
        {
            return;
        }

        if (targetButton == repairButton)
        {
            SelectOption(BuildRepairOption(), targetButton);
            return;
        }

        int reinforcementIndex = GetButtonIndex(reinforcementButtons, targetButton);

        if (reinforcementIndex >= 0 && reinforcementIndex < reinforcementChoices.Count)
        {
            SelectOption(BuildReinforcementOption(reinforcementChoices[reinforcementIndex]), targetButton);
            return;
        }

        int traitIndex = GetButtonIndex(traitButtons, targetButton);

        if (traitIndex >= 0 && traitIndex < traitChoices.Count)
        {
            SelectOption(BuildTraitOption(traitChoices[traitIndex]), targetButton);
        }
    }

    private void SelectEmptyOption()
    {
        selectedOption = default;
        selectedOptionButton = null;

        SetIcon(detailIconImage, null);
        SetText(selectedItemLabelText, "선택된 아이템");
        SetText(itemNameText, "상품 없음");
        SetText(conditionText, string.Empty);

        if (itemNameText != null)
        {
            itemNameText.color = itemNameBaseColor;
        }

        if (conditionText != null)
        {
            conditionText.color = conditionBaseColor;
        }

        SetText(bodyText, "구매 가능한 상품이 없습니다.");
        SetText(priceText, string.Empty);

        RefreshBuyButtonState();
    }

    private void RefreshBuyButtonState()
    {
        bool canBuy = CanBuySelectedOption();
        bool purchased = IsSelectedOptionPurchased();

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
        }

        if (buyButtonText != null)
        {
            if (canBuy)
            {
                buyButtonText.text = buyText;
            }
            else if (purchased)
            {
                buyButtonText.text = "판매 완료";
            }
            else if (IsSelectedCategorySoldOut())
            {
                buyButtonText.text = "판매 종료";
            }
            else if (selectedOption.Kind != ShopOptionKind.None &&
                     !ShopRunBridge.CanSpendCredits(selectedOption.Cost))
            {
                buyButtonText.text = "크레딧 부족";
            }
            else if (selectedOption.Kind == ShopOptionKind.Repair)
            {
                buyButtonText.text = "수리 불필요";
            }
            else
            {
                buyButtonText.text = cannotBuyText;
            }
        }
    }

    private bool CanBuySelectedOption()
    {
        switch (selectedOption.Kind)
        {
            case ShopOptionKind.Repair:
                return currentShop != null &&
                       currentShop.SupportsRepair &&
                       currentShop.CanBuyRepair(currentPlayer);

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
                success = currentShop != null &&
                          currentShop.SupportsRepair &&
                          currentShop.TryBuyRepair(currentPlayer);
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

        RefreshTradePage();

        if (maintenanceBayUI != null && maintenanceBayUI.IsOpen)
        {
            maintenanceBayUI.Open(
                currentShop != null ? currentShop.MaintenanceOwner : null,
                currentPlayer,
                currentMaintenanceBay
            );
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
            DescriptionText = currentShop != null && currentShop.SupportsRepair
                ? $"기체 체력을 {currentShop.RepairAmount:0.#} 회복한다."
                : "기체 체력을 회복한다.",
            Cost = currentShop != null && currentShop.SupportsRepair
                ? currentShop.RepairCost
                : 0,
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
            RarityText = definition.GetRarityText(),
            CategoryText = definition.GetUseTypeText(),
            RarityColor = definition.GetRarityColor(),
            HasRarity = true,
            DescriptionText =
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
            ConditionText = $"{definition.GetRarityText()} / {definition.GetCategoryText()}",
            RarityText = definition.GetRarityText(),
            CategoryText = definition.GetCategoryText(),
            RarityColor = definition.GetRarityColor(),
            HasRarity = true,
            DescriptionText = definition.Description,
            Cost = currentStock != null ? currentStock.TraitCost : 0,
            Icon = definition.Icon != null ? definition.Icon : traitFallbackIcon,
            Trait = definition,
            Reinforcement = null
        };
    }

    private void ConfigureExplicitResultButtonSound(Button targetButton)
    {
        if (targetButton == null)
        {
            return;
        }

        UISoundButton[] soundButtons = targetButton.GetComponents<UISoundButton>();

        if (soundButtons == null || soundButtons.Length == 0)
        {
            soundButtons = new[] { targetButton.gameObject.AddComponent<UISoundButton>() };
        }

        for (int i = 0; i < soundButtons.Length; i++)
        {
            UISoundButton soundButton = soundButtons[i];

            if (soundButton == null)
            {
                continue;
            }

            // 구매 성공/실패와 닫기 사운드는 ShopTradeUI가 직접 한 번만 재생한다.
            soundButton.SetClickSoundEnabled(false);
            soundButton.SetHoverSoundEnabled(true);
            soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
            soundButton.SetDisabledClickSoundEnabled(true);
            soundButton.SetDisabledClickSoundEventId(
                targetButton == buyButton ? SoundEventIds.ShopBuyFail : SoundEventIds.UiDisabled
            );
        }
    }

    private void ConfigureOptionSelectionRelays()
    {
        ConfigureOptionSelectionRelay(repairButton);

        for (int i = 0; i < reinforcementButtons.Length; i++)
        {
            ConfigureOptionSelectionRelay(reinforcementButtons[i]);
        }

        for (int i = 0; i < traitButtons.Length; i++)
        {
            ConfigureOptionSelectionRelay(traitButtons[i]);
        }
    }

    private void ConfigureOptionSelectionRelay(Button targetButton)
    {
        if (targetButton == null)
        {
            return;
        }

        ShopOptionSelectionRelay relay = targetButton.GetComponent<ShopOptionSelectionRelay>();

        if (relay == null)
        {
            relay = targetButton.gameObject.AddComponent<ShopOptionSelectionRelay>();
        }

        relay.Configure(this, targetButton);
    }

    private void SelectEventSystemButton(Button targetButton)
    {
        if (targetButton == null || !targetButton.gameObject.activeInHierarchy || !targetButton.interactable)
        {
            return;
        }

        EventSystem currentEventSystem = EventSystem.current;

        if (currentEventSystem != null)
        {
            currentEventSystem.SetSelectedGameObject(targetButton.gameObject);
        }
    }

    private int GetButtonIndex(Button[] buttons, Button targetButton)
    {
        if (buttons == null || targetButton == null)
        {
            return -1;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == targetButton)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsSelectedOptionPurchased()
    {
        if (currentStock == null)
        {
            return false;
        }

        return selectedOption.Kind switch
        {
            ShopOptionKind.Reinforcement => currentStock.WasReinforcementPurchased(selectedOption.Reinforcement),
            ShopOptionKind.Trait => currentStock.WasTraitPurchased(selectedOption.Trait),
            _ => false
        };
    }

    private bool IsSelectedCategorySoldOut()
    {
        if (currentStock == null)
        {
            return false;
        }

        return selectedOption.Kind switch
        {
            ShopOptionKind.Reinforcement => currentStock.ReinforcementSoldOut,
            ShopOptionKind.Trait => currentStock.TraitSoldOut,
            _ => false
        };
    }

    private string BuildCardPriceText(
        int cost,
        bool purchased,
        bool soldOut,
        bool affordable)
    {
        if (purchased)
        {
            return "판매 완료";
        }

        if (soldOut)
        {
            return "판매 종료";
        }

        return affordable ? $"{cost}C" : $"{cost}C 부족";
    }

    private void ApplyCardVisualState(
        Image icon,
        TextMeshProUGUI legacyText,
        TextMeshProUGUI name,
        TextMeshProUGUI price,
        bool purchased,
        bool affordable,
        Color rarityColor,
        bool useRarityColor)
    {
        Color contentColor = purchased
            ? new Color(0.48f, 0.52f, 0.56f, 0.72f)
            : Color.white;
        Color priceColor = purchased
            ? new Color(0.55f, 0.58f, 0.62f)
            : affordable
                ? new Color(0.55f, 1f, 1f)
                : new Color(1f, 0.55f, 0.35f);

        if (icon != null)
        {
            icon.color = contentColor;
        }

        if (legacyText != null)
        {
            legacyText.color = purchased || !useRarityColor
                ? contentColor
                : rarityColor;
        }

        if (name != null)
        {
            name.color = purchased || !useRarityColor
                ? contentColor
                : rarityColor;
        }

        if (price != null)
        {
            price.color = priceColor;
        }
    }

    private void ApplySelectedOptionRarity(ShopOption option)
    {
        bool muted = IsSelectedOptionPurchased() || IsSelectedCategorySoldOut();
        Color mutedColor = new Color(0.48f, 0.52f, 0.56f, 0.72f);

        if (itemNameText != null)
        {
            itemNameText.color = muted
                ? mutedColor
                : option.HasRarity
                    ? option.RarityColor
                    : itemNameBaseColor;
        }

        if (conditionText == null)
        {
            return;
        }

        conditionText.color = muted ? mutedColor : conditionBaseColor;

        if (!option.HasRarity || muted)
        {
            conditionText.text = option.ConditionText;
            return;
        }

        string rarityHex = ColorUtility.ToHtmlStringRGB(option.RarityColor);
        conditionText.text = $"<color=#{rarityHex}>{option.RarityText}</color> / {option.CategoryText}";
    }

    private void RequestShopAudioMode()
    {
        if (shopAudioModeRequested)
        {
            return;
        }

        shopAudioModeRequested = true;
        GameAudioLoopController.EnterShopMode();
    }

    private void ReleaseShopAudioMode()
    {
        if (!shopAudioModeRequested)
        {
            return;
        }

        shopAudioModeRequested = false;
        GameAudioLoopController.ExitShopMode();
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
