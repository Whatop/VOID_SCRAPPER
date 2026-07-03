using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class GungeonStyleCamera2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Follow")]
    [SerializeField] private float cameraDistanceZ = -10f;

    [Header("Aim Offset")]
    [SerializeField] private float maxAimOffset = 1.5f;
    [SerializeField] private float offsetSmoothSpeed = 10f;
    [SerializeField] private float deadZoneRadius = 0.9f;
    [SerializeField] private float maxMouseDistanceForOffset = 8f;

    [SerializeField]
    private AnimationCurve offsetCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.05f),
        new Keyframe(0.55f, 0.35f),
        new Keyframe(1f, 1f)
    );

    [Header("Optional Movement Bias")]
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private float moveBiasStrength = 0.08f;
    [SerializeField] private float maxMoveBias = 0.15f;

    [Header("Pixel Perfect Stabilization")]
    [SerializeField] private bool snapOffsetToPixelGrid = true;
    [SerializeField] private int assetsPixelsPerUnit = 32;

    private CinemachineFollow follow;
    private Vector3 currentOffset;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }

        if (player == null || mainCamera == null || cinemachineCamera == null || follow == null)
        {
            ResolveReferences();

            if (player == null || mainCamera == null || cinemachineCamera == null || follow == null)
            {
                return;
            }
        }

        Vector3 targetOffset = CalculateTargetOffset();

        if (offsetSmoothSpeed <= 0f)
        {
            currentOffset = targetOffset;
        }
        else
        {
            float t = 1f - Mathf.Exp(-offsetSmoothSpeed * Time.deltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, t);
        }

        Vector3 outputOffset = currentOffset;

        if (snapOffsetToPixelGrid)
        {
            outputOffset.x = SnapToPixelGrid(outputOffset.x);
            outputOffset.y = SnapToPixelGrid(outputOffset.y);
        }

        follow.FollowOffset = new Vector3(outputOffset.x, outputOffset.y, cameraDistanceZ);
    }

    private void ResolveReferences()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (player != null && playerRb == null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }

        if (cinemachineCamera != null && follow == null)
        {
            follow = cinemachineCamera.GetComponent<CinemachineFollow>();
        }
    }

    private Vector3 CalculateTargetOffset()
    {
        Vector2 aimOffset = GetMouseAimOffset();
        Vector2 moveOffset = GetMoveBiasOffset();

        Vector2 finalOffset = aimOffset + moveOffset;

        if (finalOffset.magnitude > maxAimOffset)
        {
            finalOffset = finalOffset.normalized * maxAimOffset;
        }

        return new Vector3(finalOffset.x, finalOffset.y, 0f);
    }

    private Vector2 GetMouseAimOffset()
    {
        if (Mouse.current == null || player == null || mainCamera == null)
        {
            return Vector2.zero;
        }

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        float cameraDepth = Mathf.Abs(mainCamera.transform.position.z - player.position.z);

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, cameraDepth)
        );

        mouseWorld.z = player.position.z;

        Vector2 toMouse = mouseWorld - player.position;
        float distance = toMouse.magnitude;

        if (distance <= deadZoneRadius)
        {
            return Vector2.zero;
        }

        float normalized = Mathf.InverseLerp(deadZoneRadius, maxMouseDistanceForOffset, distance);
        float curved = offsetCurve != null ? offsetCurve.Evaluate(normalized) : normalized;

        return toMouse.normalized * (maxAimOffset * curved);
    }

    private Vector2 GetMoveBiasOffset()
    {
        if (playerRb == null)
        {
            return Vector2.zero;
        }

        Vector2 velocity = playerRb.linearVelocity;

        if (velocity.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        Vector2 bias = velocity.normalized * moveBiasStrength;
        return Vector2.ClampMagnitude(bias, maxMoveBias);
    }

    private float SnapToPixelGrid(float value)
    {
        if (assetsPixelsPerUnit <= 0)
        {
            return value;
        }

        float unit = 1f / assetsPixelsPerUnit;
        return Mathf.Round(value / unit) * unit;
    }

    public void SetPlayer(Transform target)
    {
        player = target;
        playerRb = player != null ? player.GetComponent<Rigidbody2D>() : null;
    }
}