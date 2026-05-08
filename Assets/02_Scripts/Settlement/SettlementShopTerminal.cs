using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SettlementShopTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementController settlementController;

    public string InteractionText
    {
        get
        {
            SettlementController controller = GetController();
            if (controller == null)
            {
                return "상점";
            }

            return $"상점: 정비 구매 / Scrap {controller.RepairScrapCost}";
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

        controller.TryRepairPlayer();
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