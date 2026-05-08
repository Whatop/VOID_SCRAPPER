using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DebugInteractableObject : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionText = "상호작용";

    public string InteractionText => interactionText;

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log($"{name} 상호작용 실행");
    }
}