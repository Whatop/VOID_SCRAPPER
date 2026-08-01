using UnityEngine;

[DisallowMultipleComponent]
public class FieldBasePortalConsole : MonoBehaviour, IInteractable
{
    [SerializeField] private FieldBaseController baseController;
    [SerializeField] private string activateText = "포탈 제어기 가동";
    [SerializeField] private string activeText = "포탈 재동기화";

    public string InteractionText => baseController != null && baseController.IsPortalSpawned ? activeText : activateText;

    private void Awake()
    {
        if (baseController == null)
        {
            baseController = GetComponentInParent<FieldBaseController>();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return baseController != null && interactor != null;
    }

    public void Interact(GameObject interactor)
    {
        if (baseController == null)
        {
            return;
        }

        baseController.TryActivatePortal(interactor);
    }
}
