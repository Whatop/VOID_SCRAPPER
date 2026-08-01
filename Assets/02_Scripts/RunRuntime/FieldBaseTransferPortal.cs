using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldBaseTransferPortal : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionText = "포탈 진입";
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private Vector2 destinationOffset;
    [SerializeField] private bool keepPlayerRotation = true;
    [SerializeField] private string missingDestinationWarning = "포탈 목적지가 연결되지 않았습니다.";
    [SerializeField] private string arrivalWarning = "중립 구역 포탈로 이동했습니다.";

    public string InteractionText => interactionText;
    public Transform DestinationPoint => destinationPoint;

    private void Reset()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    public void ConfigureDestination(Transform point, Vector2 offset)
    {
        destinationPoint = point;
        destinationOffset = offset;
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null && destinationPoint != null;
    }

    public void Interact(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        if (destinationPoint == null)
        {
            ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
            if (hud != null)
            {
                hud.ShowWarning(missingDestinationWarning);
            }

            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        Transform root = interactor.transform;
        Rigidbody2D rb = interactor.GetComponentInParent<Rigidbody2D>();
        PlayerController2D controller = interactor.GetComponentInParent<PlayerController2D>();

        if (controller != null)
        {
            root = controller.transform;
        }
        else if (rb != null)
        {
            root = rb.transform;
        }

        Vector3 targetPosition = destinationPoint.position + (Vector3)destinationOffset;

        if (rb != null)
        {
            rb.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            root.position = targetPosition;
        }

        if (!keepPlayerRotation)
        {
            root.rotation = destinationPoint.rotation;
        }

        ExpeditionHUD expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        if (expeditionHUD != null)
        {
            expeditionHUD.ShowWarning(arrivalWarning);
        }

        AudioManager.PlayAt(SoundEventIds.EventComplete, targetPosition, 0.9f);
    }
}
