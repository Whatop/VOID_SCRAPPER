using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SettlementUpgradeTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private BuildingType buildingType;

    public string InteractionText
    {
        get
        {
            SettlementController controller = GetController();

            if (controller == null)
            {
                return "건물 강화";
            }

            string buildingName = controller.GetBuildingDisplayName(buildingType);
            string costText = controller.GetUpgradeCostText(buildingType);

            return $"{buildingName} 강화 / {costText}";
        }
    }

    private void Awake()
    {
        GetController();
    }

    public bool CanInteract(GameObject interactor)
    {
        return IsSettlementState() && GetController() != null;
    }

    public void Interact(GameObject interactor)
    {
        SettlementController controller = GetController();

        if (controller == null)
        {
            return;
        }

        controller.TryUpgradeBuilding(buildingType);
    }

    private SettlementController GetController()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        return settlementController;
    }

    private bool IsSettlementState()
    {
        return GameStateManager.Instance == null ||
               GameStateManager.Instance.CurrentState == GameState.Settlement;
    }
}