using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SettlementLaunchTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementController settlementController;

    public string InteractionText
    {
        get
        {
            SettlementController controller = GetController();

            if (controller == null)
            {
                return "출격";
            }

            return $"출격 시작 / {controller.SelectedWeaponTree}";
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

        controller.LaunchExpedition();
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