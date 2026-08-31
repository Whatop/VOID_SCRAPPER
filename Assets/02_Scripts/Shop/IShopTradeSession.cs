using UnityEngine;

public interface IShopTradeSession
{
    string DisplayName { get; }
    bool CanTrade { get; }
    bool SupportsRepair { get; }
    int RepairCost { get; }
    float RepairAmount { get; }
    ShopStockController StockController { get; }
    ShopStructure MaintenanceOwner { get; }
    ShopActiveMaintenanceBay ActiveMaintenanceBay { get; }

    bool CanBuyRepair(GameObject playerObject);
    bool TryBuyRepair(GameObject playerObject);
    void RegisterSpentCredits(int amount);
}
