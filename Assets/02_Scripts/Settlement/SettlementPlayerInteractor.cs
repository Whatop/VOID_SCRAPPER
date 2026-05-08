using UnityEngine;
using UnityEngine.InputSystem;

public class SettlementPlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.4f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private SettlementHUD hud;

    private readonly Collider2D[] interactableBuffer = new Collider2D[32];
    private IInteractable currentTarget;

    public IInteractable CurrentTarget => currentTarget;

    private void Awake()
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<SettlementHUD>();
        }
    }

    private void Update()
    {
        UpdateCurrentTarget();
        UpdatePrompt();

        if (WasInteractPressed())
        {
            TryInteract();
        }
    }

    private bool WasInteractPressed()
    {
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void UpdateCurrentTarget()
    {
        currentTarget = FindNearestInteractable();
    }

    private IInteractable FindNearestInteractable()
    {
        int layerMask = interactableLayer.value == 0
            ? Physics2D.AllLayers
            : interactableLayer.value;

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            interactRadius,
            interactableBuffer,
            layerMask
        );

        IInteractable nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = interactableBuffer[i];

            if (hit == null)
            {
                continue;
            }

            IInteractable interactable = hit.GetComponentInParent<IInteractable>();

            if (interactable == null)
            {
                continue;
            }

            if (!interactable.CanInteract(gameObject))
            {
                continue;
            }

            float sqrDistance = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = interactable;
            }
        }

        return nearest;
    }

    private void UpdatePrompt()
    {
        if (hud == null)
        {
            return;
        }

        if (currentTarget == null)
        {
            hud.SetPrompt("");
            return;
        }

        hud.SetPrompt($"E : {currentTarget.InteractionText}");
    }

    public bool TryInteract()
    {
        if (currentTarget == null)
        {
            return false;
        }

        if (!currentTarget.CanInteract(gameObject))
        {
            return false;
        }

        currentTarget.Interact(gameObject);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}