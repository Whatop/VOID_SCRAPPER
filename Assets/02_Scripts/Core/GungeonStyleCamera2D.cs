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
    [SerializeField] private float maxAimOffset = 3.0f;
    [SerializeField] private float offsetSmoothSpeed = 10f;
    [SerializeField] private float deadZoneRadius = 0.5f;
    [SerializeField] private float maxMouseDistanceForOffset = 6f;
    [SerializeField]
    private AnimationCurve offsetCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.05f),
        new Keyframe(0.55f, 0.35f),
        new Keyframe(1f, 1f)
    );
    [Header("Optional Movement Bias")]
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private float moveBiasStrength = 0.4f;
    [SerializeField] private float maxMoveBias = 1.0f;

    private CinemachineFollow follow;
    private Vector3 currentOffset;
    private Vector3 velocity;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (player != null && playerRb == null)
            playerRb = player.GetComponent<Rigidbody2D>();

        follow = cinemachineCamera != null ? cinemachineCamera.GetComponent<CinemachineFollow>() : null;
    }

    private void LateUpdate()
    {
        if (player == null || mainCamera == null || cinemachineCamera == null || follow == null)
            return;

        Vector3 targetOffset = CalculateTargetOffset();
        currentOffset = Vector3.SmoothDamp(
            currentOffset,
            targetOffset,
            ref velocity,
            1f / Mathf.Max(0.01f, offsetSmoothSpeed)
        );

        follow.FollowOffset = new Vector3(currentOffset.x, currentOffset.y, cameraDistanceZ);
    }

    private Vector3 CalculateTargetOffset()
    {
        Vector2 aimOffset = GetMouseAimOffset();
        Vector2 moveOffset = GetMoveBiasOffset();

        Vector2 finalOffset = aimOffset + moveOffset;

        if (finalOffset.magnitude > maxAimOffset)
            finalOffset = finalOffset.normalized * maxAimOffset;

        return new Vector3(finalOffset.x, finalOffset.y, cameraDistanceZ);
    }

    private Vector2 GetMouseAimOffset()
    {
        if (Mouse.current == null)
            return Vector2.zero;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(mainCamera.transform.position.z))
        );
        mouseWorld.z = player.position.z;

        Vector2 toMouse = mouseWorld - player.position;
        float distance = toMouse.magnitude;

        // 아주 가까우면 카메라 거의 안 움직임
        if (distance <= deadZoneRadius)
            return Vector2.zero;

        // dead zone 이후부터 점점 증가
        float normalized = Mathf.InverseLerp(deadZoneRadius, maxMouseDistanceForOffset, distance);

        // 선형 대신 곡선 적용
        float curved = offsetCurve.Evaluate(normalized);

        return toMouse.normalized * (maxAimOffset * curved);
    }

    private Vector2 GetMoveBiasOffset()
    {
        if (playerRb == null)
            return Vector2.zero;

        Vector2 vel = playerRb.linearVelocity;
        if (vel.sqrMagnitude <= 0.001f)
            return Vector2.zero;

        Vector2 bias = vel.normalized * moveBiasStrength;
        return Vector2.ClampMagnitude(bias, maxMoveBias);
    }

    public void SetPlayer(Transform target)
    {
        player = target;
        if (player != null && playerRb == null)
            playerRb = player.GetComponent<Rigidbody2D>();
    }
}