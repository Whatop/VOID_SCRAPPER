using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SettlementWeaponSelectTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private WeaponTreeType weaponTreeType = WeaponTreeType.MachineGun;

    public string InteractionText => $"무기 선택: {weaponTreeType}";

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

        controller.SelectWeaponTree(weaponTreeType);
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