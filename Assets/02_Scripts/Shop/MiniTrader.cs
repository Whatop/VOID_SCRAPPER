using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiniTrader : MonoBehaviour, IShopTradeSession
{
    [Header("Identity")]
    [SerializeField] private string displayName = "떠돌이 회수상";
    [SerializeField] private string interactionText = "떠돌이 회수상과 거래";

    [Header("References")]
    [SerializeField] private ShopStockController stockController;
    [SerializeField] private ShopTradeUI tradeUI;

    private GameStateManager observedGameStateManager;
    private int spentCredits;

    public string DisplayName => displayName;
    public string InteractionText => interactionText;
    public bool SupportsRepair => false;
    public int RepairCost => 0;
    public float RepairAmount => 0f;
    public ShopStockController StockController => stockController;
    public ShopStructure MaintenanceOwner => null;
    public ShopActiveMaintenanceBay ActiveMaintenanceBay => null;
    public int SpentCredits => spentCredits;

    public bool CanTrade
    {
        get
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
            {
                return false;
            }

            GameStateManager stateManager = GameStateManager.Instance;
            return stateManager == null || stateManager.CurrentState == GameState.Expedition;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindGameStateManager();
    }

    private void OnDisable()
    {
        CloseOwnedTradeSession();
        UnbindGameStateManager();
    }

    private void OnDestroy()
    {
        CloseOwnedTradeSession();
        UnbindGameStateManager();
    }

    public void Configure(
        ShopStockController configuredStock,
        TraitCatalog traitCatalog,
        ReinforcementCatalog reinforcementCatalog,
        int traitOfferCount,
        int reinforcementOfferCount)
    {
        stockController = configuredStock != null
            ? configuredStock
            : GetComponent<ShopStockController>();

        if (stockController != null)
        {
            stockController.ConfigureForMiniTrader(
                traitCatalog,
                reinforcementCatalog,
                traitOfferCount,
                reinforcementOfferCount
            );
        }

        spentCredits = 0;
        ResolveReferences();
        BindGameStateManager();
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null && CanTrade && stockController != null;
    }

    public bool TryOpenTrade(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return false;
        }

        if (tradeUI == null)
        {
            tradeUI = FindFirstObjectByType<ShopTradeUI>(FindObjectsInactive.Include);
        }

        if (tradeUI == null)
        {
            Debug.LogWarning("Mini Trader could not find the existing ShopTradeUI.", this);
            return false;
        }

        tradeUI.Open(this, interactor);
        return true;
    }

    public bool CanBuyRepair(GameObject playerObject)
    {
        return false;
    }

    public bool TryBuyRepair(GameObject playerObject)
    {
        return false;
    }

    public void RegisterSpentCredits(int amount)
    {
        if (amount > 0)
        {
            spentCredits += amount;
        }
    }

    private void ResolveReferences()
    {
        if (stockController == null)
        {
            stockController = GetComponent<ShopStockController>();
        }
    }

    private void BindGameStateManager()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (observedGameStateManager == stateManager)
        {
            return;
        }

        UnbindGameStateManager();
        observedGameStateManager = stateManager;

        if (observedGameStateManager != null)
        {
            observedGameStateManager.StateChanged += HandleGameStateChanged;
        }
    }

    private void UnbindGameStateManager()
    {
        if (observedGameStateManager != null)
        {
            observedGameStateManager.StateChanged -= HandleGameStateChanged;
            observedGameStateManager = null;
        }
    }

    private void HandleGameStateChanged(GameState previousState, GameState nextState)
    {
        if (nextState != GameState.Expedition)
        {
            CloseOwnedTradeSession();
        }
    }

    private void CloseOwnedTradeSession()
    {
        if (tradeUI != null)
        {
            tradeUI.CloseIfSession(this);
        }
    }
}
