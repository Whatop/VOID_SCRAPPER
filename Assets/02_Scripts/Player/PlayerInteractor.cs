using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer = ~0;

    private InputAction interactAction;
    private PlayerHealth playerHealth;

    private readonly Collider2D[] interactableBuffer = new Collider2D[32];

    public IInteractable CurrentTarget { get; private set; }

    public event Action<IInteractable> CurrentTargetChanged;
    public event Action<IInteractable> Interacted;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.Disable();
        }

        SetCurrentTarget(null);
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            SetCurrentTarget(null);
            return;
        }
        UpdateCurrentTarget();

        if (WasInteractPressed())
        {
            TryInteract();
        }
    }

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            return;
        }

        interactAction = actionMap.FindAction(interactActionName, false);
        if (interactAction != null)
        {
            interactAction.Enable();
        }
    }

    private bool WasInteractPressed()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void UpdateCurrentTarget()
    {
        IInteractable nearest = FindNearestInteractable();
        SetCurrentTarget(nearest);
    }

    private IInteractable FindNearestInteractable()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            interactRadius,
            interactableBuffer,
            interactableLayer
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

    private void SetCurrentTarget(IInteractable target)
    {
        if (ReferenceEquals(CurrentTarget, target))
        {
            return;
        }

        CurrentTarget = target;
        CurrentTargetChanged?.Invoke(CurrentTarget);
    }

    public bool TryInteract()
    {
        if (GameplayPauseManager.IsPaused)
        {
            SetCurrentTarget(null);
            return false;
        }
        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        if (CurrentTarget == null)
        {
            return false;
        }

        if (!CurrentTarget.CanInteract(gameObject))
        {
            return false;
        }

        CurrentTarget.Interact(gameObject);
        Interacted?.Invoke(CurrentTarget);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}