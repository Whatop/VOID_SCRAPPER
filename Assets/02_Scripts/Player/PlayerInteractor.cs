using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private Key interactFallbackKey = Key.F;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer = ~0;

    private InputAction interactAction;
    private PlayerHealth playerHealth;

    private readonly Collider2D[] interactableBuffer = new Collider2D[32];

    private IHoldInteractable activeHoldTarget;
    private float activeHoldTimer;
    private bool activeHoldInterruptedByDamage;

    public InputActionAsset InputActions => inputActions;
    public string ActionMapName => actionMapName;
    public string InteractActionName => interactActionName;
    public IInteractable CurrentTarget { get; private set; }
    public bool IsHoldingInteraction => activeHoldTarget != null;
    public float HoldRatio => activeHoldTarget == null || activeHoldTarget.HoldDuration <= 0f
        ? 0f
        : Mathf.Clamp01(activeHoldTimer / activeHoldTarget.HoldDuration);

    public event Action<IInteractable> CurrentTargetChanged;
    public event Action<IInteractable> Interacted;
    public event Action<IInteractable, float, bool> HoldProgressChanged;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        BindInput();

        if (playerHealth != null)
        {
            playerHealth.Damaged += HandlePlayerDamaged;
        }
    }

    private void OnDisable()
    {
        CancelActiveHold();

        if (interactAction != null)
        {
            interactAction.Disable();
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
        }

        SetCurrentTarget(null);
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            CancelActiveHold();
            SetCurrentTarget(null);
            return;
        }

        UpdateCurrentTarget();

        if (activeHoldTarget != null)
        {
            UpdateActiveHold();
            return;
        }

        if (WasInteractPressed())
        {
            TryInteract();
        }
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        interactAction = InputBindingUtility.ResolveAction(
            inputActions,
            actionMapName,
            interactActionName
        );
        interactAction?.Enable();
    }

    private bool WasInteractPressed()
    {
        if (interactAction != null)
        {
            return interactAction.WasPressedThisFrame();
        }

        KeyControl key = Keyboard.current != null ? Keyboard.current[interactFallbackKey] : null;
        return key != null && key.wasPressedThisFrame;
    }

    private bool IsInteractHeld()
    {
        if (interactAction != null)
        {
            return interactAction.IsPressed();
        }

        KeyControl key = Keyboard.current != null ? Keyboard.current[interactFallbackKey] : null;
        return key != null && key.isPressed;
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

        if (activeHoldTarget != null && !ReferenceEquals(activeHoldTarget, target))
        {
            CancelActiveHold();
        }

        CurrentTarget = target;
        CurrentTargetChanged?.Invoke(CurrentTarget);
    }

    public bool TryInteract()
    {
        if (GameplayPauseManager.IsPaused)
        {
            CancelActiveHold();
            SetCurrentTarget(null);
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        if (CurrentTarget == null || !CurrentTarget.CanInteract(gameObject))
        {
            return false;
        }

        if (CurrentTarget is IHoldInteractable holdTarget && holdTarget.HoldDuration > 0f)
        {
            StartHoldInteraction(holdTarget);
            return true;
        }

        ExecuteInteraction(CurrentTarget);
        return true;
    }

    private void StartHoldInteraction(IHoldInteractable holdTarget)
    {
        CancelActiveHold();

        activeHoldTarget = holdTarget;
        activeHoldTimer = 0f;
        activeHoldInterruptedByDamage = false;

        activeHoldTarget.OnHoldStarted(gameObject);
        HoldProgressChanged?.Invoke(activeHoldTarget, 0f, true);
    }

    private void UpdateActiveHold()
    {
        if (activeHoldTarget == null)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            CancelActiveHold();
            return;
        }

        if (activeHoldInterruptedByDamage ||
            !ReferenceEquals(CurrentTarget, activeHoldTarget) ||
            !activeHoldTarget.CanInteract(gameObject) ||
            !IsInteractHeld())
        {
            CancelActiveHold();
            return;
        }

        float duration = Mathf.Max(0.01f, activeHoldTarget.HoldDuration);
        activeHoldTimer += Time.deltaTime;
        float ratio = Mathf.Clamp01(activeHoldTimer / duration);

        HoldProgressChanged?.Invoke(activeHoldTarget, ratio, true);

        if (activeHoldTimer < duration)
        {
            return;
        }

        CompleteActiveHold();
    }

    private void CompleteActiveHold()
    {
        if (activeHoldTarget == null)
        {
            return;
        }

        IHoldInteractable completedTarget = activeHoldTarget;

        activeHoldTarget = null;
        activeHoldTimer = 0f;
        activeHoldInterruptedByDamage = false;

        HoldProgressChanged?.Invoke(completedTarget, 1f, false);

        if (completedTarget.CanInteract(gameObject))
        {
            completedTarget.Interact(gameObject);
            Interacted?.Invoke(completedTarget);
        }
    }

    private void CancelActiveHold()
    {
        if (activeHoldTarget == null)
        {
            return;
        }

        IHoldInteractable canceledTarget = activeHoldTarget;

        activeHoldTarget = null;
        activeHoldTimer = 0f;
        activeHoldInterruptedByDamage = false;

        canceledTarget.OnHoldCanceled(gameObject);
        HoldProgressChanged?.Invoke(canceledTarget, 0f, false);
    }

    private void ExecuteInteraction(IInteractable target)
    {
        if (target == null)
        {
            return;
        }

        target.Interact(gameObject);
        Interacted?.Invoke(target);
        AudioManager.Play(SoundEventIds.UiClick);
    }

    private void HandlePlayerDamaged(float currentHp, float maxHp)
    {
        if (activeHoldTarget != null && activeHoldTarget.CancelHoldOnDamage)
        {
            activeHoldInterruptedByDamage = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
