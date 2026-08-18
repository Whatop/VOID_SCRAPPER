using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialInteractionTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionText = "Inspect Signal Relay";
    [SerializeField] private bool interactionEnabled;
    [SerializeField] private GameObject inactiveVisual;
    [SerializeField] private GameObject activatedVisual;

    public string InteractionText => interactionText;
    public bool InteractionEnabled => interactionEnabled;
    public bool IsActivated { get; private set; }

    private void OnEnable()
    {
        RefreshVisuals();
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactionEnabled && !IsActivated && isActiveAndEnabled;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        IsActivated = true;
        interactionEnabled = false;
        RefreshVisuals();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled && !IsActivated;
    }

    public void ResetTarget()
    {
        IsActivated = false;
        interactionEnabled = false;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!IsActivated);
        }

        if (activatedVisual != null)
        {
            activatedVisual.SetActive(IsActivated);
        }
    }
}
