using Action = System.Action;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Action Names")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationOffset = -90f;
    [Min(0f)]
    [SerializeField] private float aimDeadZoneDistance = 0.28f;
    [Min(0f)]
    [SerializeField] private float rotationSmoothSpeed = 28f;
    [SerializeField] private bool useUnscaledRotationTime = true;

    [Header("External Push")]
    [Min(0.02f)]
    [SerializeField] private float defaultExternalPushDuration = 0.22f;

    [Header("Safe Reposition")]
    [Tooltip("Layers that make a tactical reposition destination invalid. When empty, current project blocking layers are resolved by layer name.")]
    [SerializeField] private LayerMask repositionBlockingLayers;
    [Min(0f)]
    [SerializeField] private float repositionClearancePadding = 0.04f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Camera mainCamera;
    private InputActionMap playerActionMap;
    private InputAction moveAction;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.up;
    private float currentRotationZ;

    private bool controlEnabled = true;
    private bool movementLocked;
    private bool movementInputActive;

    private Vector2 externalPushVelocity;
    private float externalPushTimer;
    private float externalPushDuration;

    private bool movementVelocityOverrideActive;
    private Vector2 movementVelocityOverride;
    private readonly Collider2D[] repositionOverlapBuffer = new Collider2D[24];

    public InputActionAsset InputActions => inputActions;
    public string ActionMapName => actionMapName;
    public Vector2 MoveInput => moveInput;
    public Vector2 AimDirection => aimDirection;
    public float MoveSpeed => moveSpeed;
    public bool IsMoving => moveInput.sqrMagnitude > 0.001f;
    public bool ControlEnabled => controlEnabled;
    public bool MovementLocked => movementLocked;
    public bool MovementVelocityOverrideActive => movementVelocityOverrideActive;

    public event Action MovementStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        currentRotationZ = transform.eulerAngles.z;
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        playerActionMap?.Disable();

        externalPushVelocity = Vector2.zero;
        externalPushTimer = 0f;
        movementVelocityOverrideActive = false;
        movementVelocityOverride = Vector2.zero;
        movementInputActive = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        ReadInput();
        RotateToMouse();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);

        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset이 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        playerActionMap = inputActions.FindActionMap(actionMapName, false);
        if (playerActionMap == null)
        {
            Debug.LogError($"Action Map을 찾을 수 없습니다: {actionMapName}", this);
            enabled = false;
            return;
        }

        InputBindingPersistence.LoadOnce(inputActions);
        moveAction = playerActionMap.FindAction(moveActionName, false);
        if (moveAction == null)
        {
            Debug.LogError($"Move Action을 찾을 수 없습니다: {moveActionName}", this);
            enabled = false;
            return;
        }

        playerActionMap.Enable();
    }

    private void ReadInput()
    {
        if (GameplayPauseManager.IsPaused)
        {
            SetMoveInput(Vector2.zero);
            return;
        }

        if (!controlEnabled || moveAction == null)
        {
            SetMoveInput(Vector2.zero);
            return;
        }

        SetMoveInput(moveAction.ReadValue<Vector2>().normalized);
    }

    private void SetMoveInput(Vector2 value)
    {
        moveInput = value;

        bool isActive = moveInput.sqrMagnitude > 0.001f;
        if (isActive && !movementInputActive)
        {
            MovementStarted?.Invoke();
        }

        movementInputActive = isActive;
    }

    private void MovePlayer()
    {
        if (rb == null)
        {
            return;
        }

        Vector2 inputVelocity;

        if (movementVelocityOverrideActive)
        {
            inputVelocity = movementVelocityOverride;
        }
        else
        {
            inputVelocity = (!controlEnabled || movementLocked)
                ? Vector2.zero
                : moveInput * moveSpeed;
        }

        Vector2 pushVelocity = Vector2.zero;

        if (externalPushTimer > 0f)
        {
            externalPushTimer = Mathf.Max(0f, externalPushTimer - Time.fixedDeltaTime);
            float ratio = externalPushDuration > 0f
                ? Mathf.Clamp01(externalPushTimer / externalPushDuration)
                : 0f;
            pushVelocity = externalPushVelocity * ratio;

            if (externalPushTimer <= 0f)
            {
                externalPushVelocity = Vector2.zero;
            }
        }

        rb.linearVelocity = inputVelocity + pushVelocity;
    }

    private void RotateToMouse()
    {
        if (GameplayPauseManager.IsPaused || !controlEnabled)
        {
            return;
        }

        if (!TryGetAimWorldPosition(out Vector2 mouseWorldPosition))
        {
            return;
        }

        Vector2 direction = mouseWorldPosition - (Vector2)transform.position;
        float deadZone = Mathf.Max(0.001f, aimDeadZoneDistance);

        if (direction.sqrMagnitude <= deadZone * deadZone)
        {
            return;
        }

        aimDirection = direction.normalized;

        float targetRotationZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        float deltaTime = useUnscaledRotationTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (rotationSmoothSpeed <= 0f || deltaTime <= 0f)
        {
            currentRotationZ = targetRotationZ;
        }
        else
        {
            float t = 1f - Mathf.Exp(-rotationSmoothSpeed * deltaTime);
            currentRotationZ = Mathf.LerpAngle(currentRotationZ, targetRotationZ, t);
        }

        transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);
    }

    public bool TryGetAimWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = transform.position;

        if (Mouse.current == null)
        {
            return false;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        worldPosition = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
        return true;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;

        if (!controlEnabled)
        {
            SetMoveInput(Vector2.zero);
            ClearMovementVelocityOverride();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetMovementVelocityOverride(Vector2 velocity)
    {
        movementVelocityOverride = velocity;
        movementVelocityOverrideActive = true;
    }

    public void ClearMovementVelocityOverride()
    {
        movementVelocityOverrideActive = false;
        movementVelocityOverride = Vector2.zero;
    }

    public void ApplyExternalPush(
        Vector2 direction,
        float distance,
        float duration = -1f)
    {
        if (rb == null || distance <= 0f)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        float finalDuration = duration > 0f
            ? duration
            : Mathf.Max(0.02f, defaultExternalPushDuration);

        externalPushDuration = Mathf.Max(0.02f, finalDuration);
        externalPushTimer = externalPushDuration;
        externalPushVelocity = direction.normalized * (distance / externalPushDuration);
    }

    public bool IsRepositionDestinationValid(Vector2 destination)
    {
        if (!IsFinite(destination))
        {
            return false;
        }

        int blockingMask = ResolveRepositionBlockingMask();
        if (blockingMask == 0)
        {
            return false;
        }

        Physics2D.SyncTransforms();

        Vector2 centerOffset = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center - (Vector2)transform.position
            : Vector2.zero;
        float clearanceRadius = bodyCollider != null
            ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y)
            : 0.18f;
        clearanceRadius = Mathf.Max(0.08f, clearanceRadius + repositionClearancePadding);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockingMask);
        // Boss walls and some generated blockers are trigger-based, so they must
        // participate in destination validation as well as solid colliders.
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            destination + centerOffset,
            clearanceRadius,
            filter,
            repositionOverlapBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = repositionOverlapBuffer[i];
            repositionOverlapBuffer[i] = null;

            if (hit != null && hit.transform.root != transform.root)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryRepositionTo(Vector2 destination)
    {
        if (rb == null || !IsRepositionDestinationValid(destination))
        {
            return false;
        }

        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = new Vector3(destination.x, destination.y, previousPosition.z);
        Vector3 positionDelta = nextPosition - previousPosition;

        SetMoveInput(Vector2.zero);
        externalPushVelocity = Vector2.zero;
        externalPushTimer = 0f;
        ClearMovementVelocityOverride();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = destination;
        transform.position = nextPosition;

        CinemachineCore.OnTargetObjectWarped(transform, positionDelta);
        Physics2D.SyncTransforms();
        return true;
    }

    private int ResolveRepositionBlockingMask()
    {
        if (repositionBlockingLayers.value != 0)
        {
            return repositionBlockingLayers.value;
        }

        return LayerMask.GetMask("Meteor", "Shop", "WorldSolid", "BaseLaser");
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    public void SetMoveSpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    public void AddMoveSpeed(float amount)
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed + amount);
    }
}
