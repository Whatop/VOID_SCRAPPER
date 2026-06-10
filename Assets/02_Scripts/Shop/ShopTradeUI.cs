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
        Trait,
        Reinforcement
    }

    private struct ShopOption
    {
        public ShopOptionKind kind;
        public TraitDefinition trait;
        public string title;
        public string description;
        public int cost;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Left Detail")]
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailCostText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyButtonText;

    [Header("Right Items - Repair")]
    [SerializeField] private Button repairOptionButton;
    [SerializeField] private TextMeshProUGUI repairOptionText;

    [Header("Right Items - Trait")]
    [SerializeField] private Button[] traitOptionButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] traitOptionTexts = new TextMeshProUGUI[3];

    [Header("Right Items - Reinforcement")]
    [SerializeField] private Button[] reinforcementOptionButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] reinforcementOptionTexts = new TextMeshProUGUI[3];

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Pause")]
    [SerializeField] private bool pauseGameWhileOpen = true;

    [Header("Fallback Text")]
    [SerializeField] private string noItemTitle = "상품 없음";
    [SerializeField] private string noItemDescription = "구매 가능한 상품이 없습니다.";
    [SerializeField] private string buyText = "구매";
    [SerializeField] private string cannotBuyText = "구매 불가";

    private ShopStructure currentShop;
    private ShopStockController currentStock;
    private GameObject currentPlayer;

    private bool isOpen;
    private ShopOption selectedOption;

    private readonly List<TraitDefinition> traitChoices = new List<TraitDefinition>();
    private readonly List<TraitDefinition> reinforcementChoices = new List<TraitDefinition>();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        BindStaticButtons();
        Close();
    }

    private void OnDisable()
    {
        ReleasePause();
    }

    private void OnDestroy()
    {
        ReleasePause();
        UnbindStaticButtons();
        ClearDynamicButtonListeners();
    }

    public void Open(ShopStructure shop, GameObject playerObject)
    {
        currentShop = shop;
        currentPlayer = playerObject;
        currentStock = shop != null ? shop.GetComponent<ShopStockController>() : null;

        if (currentStock == null && shop != null)
        {
            currentStock = shop.GetComponentInChildren<ShopStockController>(true);
        }

        isOpen = true;

        if (root != null)
        {
            root.SetActive(true);
        }

        RequestPause();

        RollShopItems();
        RefreshOptionButtons();
        SelectDefaultOption();
    }

    public void Close()
    {
        isOpen = false;

        currentShop = null;
        currentStock = null;
        currentPlayer = null;

        traitChoices.Clear();
        reinforcementChoices.Clear();
        selectedOption = default;

        ClearDynamicButtonListeners();

        if (root != null)
        {
            root.SetActive(false);
        }

        ReleasePause();
    }

    private void RollShopItems()
    {
        traitChoices.Clear();
        reinforcementChoices.Clear();

        if (currentStock != null)
        {
            traitChoices.AddRange(currentStock.RollTraitChoices());
            reinforcementChoices.AddRange(currentStock.RollReinforcementChoices());
        }
    }

    private void RefreshOptionButtons()
    {
        RefreshRepairButton();
        RefreshTraitButtons();
        RefreshReinforcementButtons();
    }

    private void RefreshRepairButton()
    {
        if (repairOptionButton == null)
        {
            return;
        }

        bool visible = currentShop != null;
        repairOptionButton.gameObject.SetActive(visible);
        repairOptionButton.onClick.RemoveAllListeners();

        if (!visible)
        {
            return;
        }

        if (repairOptionText != null)
        {
            repairOptionText.text = $"수리\n{currentShop.RepairCost}C";
        }

        repairOptionButton.onClick.AddListener(() =>
        {
            SelectOption(BuildRepairOption());
        });
    }

    private void RefreshTraitButtons()
    {
        for (int i = 0; i < traitOptionButtons.Length; i++)
        {
            Button button = traitOptionButtons[i];
            TextMeshProUGUI label = i < traitOptionTexts.Length ? traitOptionTexts[i] : null;

            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();

            bool visible = i < traitChoices.Count && traitChoices[i] != null;
            button.gameObject.SetActive(visible);

            if (!visible)
            {
                if (label != null)
                {
                    label.text = string.Empty;
                }

                continue;
            }

            TraitDefinition trait = traitChoices[i];

            if (label != null)
            {
                label.text = $"{trait.DisplayName}\n{currentStock.TraitCost}C";
            }

            button.onClick.AddListener(() =>
            {
                SelectOption(BuildTraitOption(trait));
            });
        }
    }

    private void RefreshReinforcementButtons()
    {
        for (int i = 0; i < reinforcementOptionButtons.Length; i++)
        {
            Button button = reinforcementOptionButtons[i];
            TextMeshProUGUI label = i < reinforcementOptionTexts.Length ? reinforcementOptionTexts[i] : null;

            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();

            bool visible = i < reinforcementChoices.Count && reinforcementChoices[i] != null;
            button.gameObject.SetActive(visible);

            if (!visible)
            {
                if (label != null)
                {
                    label.text = string.Empty;
                }

                continue;
            }

            TraitDefinition reinforcement = reinforcementChoices[i];

            if (label != null)
            {
                label.text = $"{reinforcement.DisplayName}\n{currentStock.ReinforcementCost}C";
            }

            button.onClick.AddListener(() =>
            {
                SelectOption(BuildReinforcementOption(reinforcement));
            });
        }
    }

    private void SelectDefaultOption()
    {
        if (currentShop != null)
        {
            SelectOption(BuildRepairOption());
            return;
        }

        if (traitChoices.Count > 0)
        {
            SelectOption(BuildTraitOption(traitChoices[0]));
            return;
        }

        if (reinforcementChoices.Count > 0)
        {
            SelectOption(BuildReinforcementOption(reinforcementChoices[0]));
            return;
        }

        SelectEmptyOption();
    }

    private ShopOption BuildRepairOption()
    {
        return new ShopOption
        {
            kind = ShopOptionKind.Repair,
            trait = null,
            title = "수리",
            description = currentShop != null
                ? $"기체 체력을 {currentShop.RepairAmount:0.#} 회복한다.\n상점마다 항상 제공되는 확정 상품이다."
                : "기체 체력을 회복한다.",
            cost = currentShop != null ? currentShop.RepairCost : 0
        };
    }

    private ShopOption BuildTraitOption(TraitDefinition trait)
    {
        return new ShopOption
        {
            kind = ShopOptionKind.Trait,
            trait = trait,
            title = trait != null ? trait.DisplayName : "추가 특성",
            description = trait != null
                ? $"{GetTraitCategoryText(trait)}\n{trait.Description}"
                : "현재 런에 특성을 추가한다.",
            cost = currentStock != null ? currentStock.TraitCost : 0
        };
    }

    private ShopOption BuildReinforcementOption(TraitDefinition reinforcement)
    {
        return new ShopOption
        {
            kind = ShopOptionKind.Reinforcement,
            trait = reinforcement,
            title = reinforcement != null ? reinforcement.DisplayName : "기체 보강",
            description = reinforcement != null
                ? $"기체 보강\n{reinforcement.Description}"
                : "기체 능력치를 향상시키거나 추가 능력을 부여한다.",
            cost = currentStock != null ? currentStock.ReinforcementCost : 0
        };
    }

    private void SelectOption(ShopOption option)
    {
        selectedOption = option;

        if (detailTitleText != null)
        {
            detailTitleText.text = option.title;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = option.description;
        }

        if (detailCostText != null)
        {
            detailCostText.text = $"필요 재화: {option.cost} 크레딧";
        }

        RefreshBuyButton();
    }

    private void SelectEmptyOption()
    {
        selectedOption = default;

        if (detailTitleText != null)
        {
            detailTitleText.text = noItemTitle;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = noItemDescription;
        }

        if (detailCostText != null)
        {
            detailCostText.text = string.Empty;
        }

        RefreshBuyButton();
    }

    private void RefreshBuyButton()
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
        switch (selectedOption.kind)
        {
            case ShopOptionKind.Repair:
                return currentShop != null && currentShop.CanBuyRepair(currentPlayer);

            case ShopOptionKind.Trait:
                return currentStock != null && currentStock.CanBuyTrait(selectedOption.trait);

            case ShopOptionKind.Reinforcement:
                return currentStock != null && currentStock.CanBuyReinforcement(selectedOption.trait);

            default:
                return false;
        }
    }

    private void BuySelectedOption()
    {
        bool success = false;

        switch (selectedOption.kind)
        {
            case ShopOptionKind.Repair:
                success = currentShop != null && currentShop.TryBuyRepair(currentPlayer);
                break;

            case ShopOptionKind.Trait:
                success = currentStock != null &&
                          currentStock.TryBuyTrait(currentShop, selectedOption.trait, currentPlayer);
                break;

            case ShopOptionKind.Reinforcement:
                success = currentStock != null &&
                          currentStock.TryBuyReinforcement(currentShop, selectedOption.trait, currentPlayer);
                break;
        }

        if (!success)
        {
            RefreshBuyButton();
            return;
        }

        RemovePurchasedOptionFromLocalList();
        RefreshOptionButtons();

        if (selectedOption.kind == ShopOptionKind.Repair)
        {
            SelectOption(BuildRepairOption());
        }
        else
        {
            SelectDefaultOption();
        }
    }

    private void RemovePurchasedOptionFromLocalList()
    {
        if (selectedOption.trait == null)
        {
            return;
        }

        if (selectedOption.kind == ShopOptionKind.Trait)
        {
            traitChoices.Remove(selectedOption.trait);
        }
        else if (selectedOption.kind == ShopOptionKind.Reinforcement)
        {
            reinforcementChoices.Remove(selectedOption.trait);
        }
    }

    private string GetTraitCategoryText(TraitDefinition trait)
    {
        if (trait == null)
        {
            return string.Empty;
        }

        if (trait.Category == TraitCategory.Shared)
        {
            return "공유 특성";
        }

        return trait.WeaponTreeType switch
        {
            WeaponTreeType.Shotgun => "샷건 전용 특성",
            WeaponTreeType.Sniper => "저격 전용 특성",
            WeaponTreeType.MachineGun => "기관총 전용 특성",
            _ => "전용 특성"
        };
    }

    private void BindStaticButtons()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(BuySelectedOption);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void UnbindStaticButtons()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(BuySelectedOption);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    private void ClearDynamicButtonListeners()
    {
        if (repairOptionButton != null)
        {
            repairOptionButton.onClick.RemoveAllListeners();
        }

        ClearButtonArray(traitOptionButtons);
        ClearButtonArray(reinforcementOptionButtons);
    }

    private void ClearButtonArray(Button[] buttons)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].onClick.RemoveAllListeners();
            }
        }
    }

    private void RequestPause()
    {
        if (!pauseGameWhileOpen)
        {
            return;
        }

        GameplayPauseManager.Instance.PushPause(this, "ShopTradeUI");
        GameplayPauseManager.Instance.RegisterCancelHandler(this, Close);
    }

    private void ReleasePause()
    {
        if (!pauseGameWhileOpen)
        {
            return;
        }

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);
            GameplayPauseManager.Instance.PopPause(this);
        }
    }
}