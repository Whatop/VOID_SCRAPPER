using UnityEngine;
using UnityEngine.InputSystem;

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

    private Rigidbody2D rb;
    private Camera mainCamera;
    private InputAction moveAction;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.up;

    private bool controlEnabled = true;
    private bool movementLocked;

    public Vector2 MoveInput => moveInput;
    public Vector2 AimDirection => aimDirection;
    public float MoveSpeed => moveSpeed;
    public bool IsMoving => moveInput.sqrMagnitude > 0.001f;
    public bool ControlEnabled => controlEnabled;
    public bool MovementLocked => movementLocked;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }

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
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            Debug.LogError($"Action Map을 찾을 수 없습니다: {actionMapName}", this);
            enabled = false;
            return;
        }

        moveAction = actionMap.FindAction(moveActionName, false);
        if (moveAction == null)
        {
            Debug.LogError($"Move Action을 찾을 수 없습니다: {moveActionName}", this);
            enabled = false;
            return;
        }

        moveAction.Enable();
    }

    private void ReadInput()
    {
        if (GameplayPauseManager.IsPaused)
        {
            moveInput = Vector2.zero;
            return;
        }
        if (!controlEnabled || moveAction == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = moveAction.ReadValue<Vector2>().normalized;
    }

    private void MovePlayer()
    {
        if (rb == null)
        {
            return;
        }

        if (!controlEnabled || movementLocked)
        {
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void RotateToMouse()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }
        if (!controlEnabled)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = transform.position.z;

        Vector2 direction = mouseWorldPosition - transform.position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        aimDirection = direction.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;

        if (!controlEnabled)
        {
            moveInput = Vector2.zero;

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

    public void SetMoveSpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    public void AddMoveSpeed(float amount)
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed + amount);
    }
}