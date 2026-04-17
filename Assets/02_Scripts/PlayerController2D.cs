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
    [SerializeField] private float moveSpeed = 5f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationOffset = -90f;

    private Rigidbody2D rb;
    private Camera mainCamera;
    private InputAction moveAction;
    private Vector2 moveInput;

    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
        moveAction = actionMap.FindAction(moveActionName, true);
        actionMap.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.actionMap.Disable();
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

    private void ReadInput()
    {
        moveInput = moveAction.ReadValue<Vector2>().normalized;
    }

    private void MovePlayer()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void RotateToMouse()
    {
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

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    /// <summary>
    /// 레벨업 시 플레이어 이동 속도를 증가시킬 때 사용합니다.
    /// </summary>
    public void AddMoveSpeed(float amount)
    {
        moveSpeed = Mathf.Max(0.5f, moveSpeed + amount);
    }
}
