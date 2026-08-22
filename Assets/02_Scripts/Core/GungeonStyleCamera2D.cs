using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class GungeonStyleCamera2D : MonoBehaviour
{
    public static GungeonStyleCamera2D Instance { get; private set; }

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
    [SerializeField] private float moveBiasSmoothSpeed = 8f;
    [SerializeField] private float moveBiasVelocityDeadZone = 0.08f;

    [Header("Idle Recentering")]
    [SerializeField] private float idleRecenteringSmoothSpeed = 5.5f;

    [Header("Camera Shake")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float maxShakeAmplitude = 0.42f;
    [SerializeField] private float shakeFrequency = 30f;
    [SerializeField, Range(0f, 1f)] private float repeatedHitStacking = 0.35f;
    [SerializeField] private bool useUnscaledTimeForShake = true;

    [Header("Pixel Perfect Stabilization")]
    [SerializeField] private bool snapOffsetToPixelGrid = true;
    [FormerlySerializedAs("snapOnlyWhenSettled")]
    [SerializeField] private bool snapOffsetOnlyWhenSettled = true;
    [SerializeField] private float pixelSnapSettleDistance = 0.012f;
    [SerializeField] private int assetsPixelsPerUnit = 32;

    private CinemachineFollow follow;
    private Vector3 currentOffset;
    private Vector2 currentMoveBiasOffset;
    private float runtimeAimOffsetMultiplier = 1f;
    private float runtimeMouseDistanceMultiplier = 1f;

    [Header("Cinematic Focus")]
    [SerializeField] private float cinematicFocusSmoothSpeed = 9f;

    private bool cinematicFocusActive;
    private bool cinematicInputOffsetLocked;
    private Vector3 cinematicFocusWorldPosition;

    private float shakeRemaining;
    private float shakeDuration;
    private float shakeAmplitude;
    private float shakeNoiseTime;
    private Vector2 shakeSeed;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
        ResetShakeNoise();
    }

    private void OnEnable()
    {
        Instance = this;
        ResolveReferences();
    }

    private void OnDisable()
    {
        cinematicFocusActive = false;
        cinematicInputOffsetLocked = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

        float activeSmoothSpeed = cinematicFocusActive
            ? cinematicFocusSmoothSpeed
            : ResolveOffsetSmoothSpeed(targetOffset);

        if (activeSmoothSpeed <= 0f)
        {
            currentOffset = targetOffset;
        }
        else
        {
            float t = 1f - Mathf.Exp(-activeSmoothSpeed * Time.unscaledDeltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, t);
        }

        Vector2 shakeOffset = EvaluateShakeOffset();
        Vector3 outputOffset = currentOffset + new Vector3(shakeOffset.x, shakeOffset.y, 0f);

        bool settledForPixelSnap = !snapOffsetOnlyWhenSettled ||
                                   (targetOffset - currentOffset).sqrMagnitude <= pixelSnapSettleDistance * pixelSnapSettleDistance;

        if (snapOffsetToPixelGrid && settledForPixelSnap)
        {
            outputOffset.x = SnapToPixelGrid(outputOffset.x);
            outputOffset.y = SnapToPixelGrid(outputOffset.y);
        }

        follow.FollowOffset = new Vector3(outputOffset.x, outputOffset.y, cameraDistanceZ);
    }

    public static void RequestShake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        if (Instance == null)
        {
            Instance = FindFirstObjectByType<GungeonStyleCamera2D>();
        }

        if (Instance != null)
        {
            Instance.AddShake(amplitude, duration);
        }
    }

    public void AddShake(float amplitude, float duration)
    {
        if (!enableCameraShake || amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        amplitude *= GameSettingsRuntime.CameraShakeMultiplier;
        if (amplitude <= 0.0001f)
        {
            return;
        }

        amplitude = Mathf.Clamp(amplitude, 0f, Mathf.Max(0.01f, maxShakeAmplitude));
        duration = Mathf.Max(0.01f, duration);

        if (shakeRemaining > 0f)
        {
            shakeAmplitude = Mathf.Min(
                Mathf.Max(0.01f, maxShakeAmplitude),
                Mathf.Max(shakeAmplitude, amplitude) + amplitude * repeatedHitStacking
            );
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }
        else
        {
            shakeAmplitude = amplitude;
            shakeRemaining = duration;
            shakeDuration = duration;
        }

        ResetShakeNoise();
    }

    public void StopShake()
    {
        shakeRemaining = 0f;
        shakeDuration = 0f;
        shakeAmplitude = 0f;
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

        if (player == null)
        {
            PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);

            if (playerController != null)
            {
                player = playerController.transform;
            }
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
        if (cinematicFocusActive && player != null)
        {
            Vector2 focusOffset = cinematicFocusWorldPosition - player.position;
            return new Vector3(focusOffset.x, focusOffset.y, 0f);
        }

        if (cinematicInputOffsetLocked)
        {
            currentMoveBiasOffset = Vector2.zero;
            return Vector3.zero;
        }

        Vector2 aimOffset = GetMouseAimOffset();
        Vector2 moveOffset = GetMoveBiasOffset();

        Vector2 finalOffset = aimOffset + moveOffset;
        float effectiveMaxAimOffset = Mathf.Max(0f, maxAimOffset * Mathf.Max(0.01f, runtimeAimOffsetMultiplier));

        if (effectiveMaxAimOffset > 0f && finalOffset.magnitude > effectiveMaxAimOffset)
        {
            finalOffset = finalOffset.normalized * effectiveMaxAimOffset;
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
        Rect pixelRect = mainCamera.pixelRect;

        if (pixelRect.width <= 0f || pixelRect.height <= 0f)
        {
            return Vector2.zero;
        }

        mouseScreen.x = Mathf.Clamp(mouseScreen.x, pixelRect.xMin, pixelRect.xMax);
        mouseScreen.y = Mathf.Clamp(mouseScreen.y, pixelRect.yMin, pixelRect.yMax);

        Vector2 viewportCenter = pixelRect.center;
        Vector2 centeredViewport = new Vector2(
            (mouseScreen.x - viewportCenter.x) / (pixelRect.width * 0.5f),
            (mouseScreen.y - viewportCenter.y) / (pixelRect.height * 0.5f)
        );

        float orthographicSize = cinemachineCamera != null
            ? cinemachineCamera.Lens.OrthographicSize
            : mainCamera.orthographicSize;
        float viewportAspect = pixelRect.width / pixelRect.height;
        Vector2 toMouse = new Vector2(
            centeredViewport.x * orthographicSize * viewportAspect,
            centeredViewport.y * orthographicSize
        );
        float distance = toMouse.magnitude;

        if (distance <= deadZoneRadius)
        {
            return Vector2.zero;
        }

        float effectiveMouseDistanceForOffset = Mathf.Max(
            deadZoneRadius + 0.01f,
            maxMouseDistanceForOffset * Mathf.Max(0.01f, runtimeMouseDistanceMultiplier)
        );
        float effectiveMaxAimOffset = Mathf.Max(0f, maxAimOffset * Mathf.Max(0.01f, runtimeAimOffsetMultiplier));

        float normalized = Mathf.InverseLerp(deadZoneRadius, effectiveMouseDistanceForOffset, distance);
        float curved = offsetCurve != null ? offsetCurve.Evaluate(normalized) : normalized;

        return toMouse.normalized * (effectiveMaxAimOffset * curved);
    }

    private Vector2 GetMoveBiasOffset()
    {
        Vector2 targetBias = Vector2.zero;

        if (playerRb != null)
        {
            Vector2 velocity = playerRb.linearVelocity;
            float deadZone = Mathf.Max(0f, moveBiasVelocityDeadZone);

            if (velocity.sqrMagnitude > deadZone * deadZone)
            {
                Vector2 bias = velocity.normalized * moveBiasStrength;
                targetBias = Vector2.ClampMagnitude(bias, maxMoveBias);
            }
        }

        float smoothSpeed = Mathf.Max(0f, moveBiasSmoothSpeed);

        if (smoothSpeed <= 0f)
        {
            currentMoveBiasOffset = targetBias;
            return currentMoveBiasOffset;
        }

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
        currentMoveBiasOffset = Vector2.Lerp(currentMoveBiasOffset, targetBias, t);
        return currentMoveBiasOffset;
    }

    private float ResolveOffsetSmoothSpeed(Vector3 targetOffset)
    {
        bool playerNearlyStopped = playerRb == null ||
                                   playerRb.linearVelocity.sqrMagnitude <= moveBiasVelocityDeadZone * moveBiasVelocityDeadZone;
        bool returningTowardTarget = targetOffset.sqrMagnitude < currentOffset.sqrMagnitude;

        if (playerNearlyStopped && returningTowardTarget)
        {
            return Mathf.Max(0f, idleRecenteringSmoothSpeed);
        }

        return Mathf.Max(0f, offsetSmoothSpeed);
    }

    private Vector2 EvaluateShakeOffset()
    {
        if (!enableCameraShake || shakeRemaining <= 0f || shakeAmplitude <= 0f)
        {
            return Vector2.zero;
        }

        float deltaTime = useUnscaledTimeForShake ? Time.unscaledDeltaTime : Time.deltaTime;
        float safeDuration = Mathf.Max(0.01f, shakeDuration);
        float envelope = Mathf.Clamp01(shakeRemaining / safeDuration);
        envelope *= envelope;

        shakeNoiseTime += deltaTime * Mathf.Max(1f, shakeFrequency);

        float x = Mathf.PerlinNoise(shakeSeed.x, shakeNoiseTime) * 2f - 1f;
        float y = Mathf.PerlinNoise(shakeSeed.y, shakeNoiseTime + 17.37f) * 2f - 1f;

        Vector2 offset = new Vector2(x, y);

        if (offset.sqrMagnitude > 1f)
        {
            offset.Normalize();
        }

        offset *= shakeAmplitude * envelope;

        shakeRemaining -= deltaTime;

        if (shakeRemaining <= 0f)
        {
            StopShake();
        }

        return offset;
    }

    private void ResetShakeNoise()
    {
        shakeSeed = new Vector2(
            Random.Range(-1000f, 1000f),
            Random.Range(-1000f, 1000f)
        );
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

    public void SetCinematicFocus(Vector3 worldPosition, bool immediate = false)
    {
        ResolveReferences();
        cinematicFocusActive = true;
        cinematicFocusWorldPosition = worldPosition;

        if (immediate && player != null)
        {
            Vector2 offset = worldPosition - player.position;
            currentOffset = new Vector3(offset.x, offset.y, 0f);

            if (follow != null)
            {
                follow.FollowOffset = new Vector3(currentOffset.x, currentOffset.y, cameraDistanceZ);
            }
        }
    }

    public void UpdateCinematicFocus(Vector3 worldPosition)
    {
        cinematicFocusWorldPosition = worldPosition;
    }

    public void ClearCinematicFocus(bool immediate = false)
    {
        cinematicFocusActive = false;

        if (immediate)
        {
            currentOffset = Vector3.zero;

            if (follow != null)
            {
                follow.FollowOffset = new Vector3(0f, 0f, cameraDistanceZ);
            }
        }
    }

    public void SetAimOffsetAssist(float aimOffsetMultiplier, float mouseDistanceMultiplier)
    {
        runtimeAimOffsetMultiplier = Mathf.Max(0.01f, aimOffsetMultiplier);
        runtimeMouseDistanceMultiplier = Mathf.Max(0.01f, mouseDistanceMultiplier);
    }

    public void SetCinematicInputOffsetLocked(bool locked)
    {
        cinematicInputOffsetLocked = locked;

        if (locked)
        {
            currentMoveBiasOffset = Vector2.zero;

            if (!cinematicFocusActive)
            {
                currentOffset = Vector3.zero;
                ResolveReferences();

                if (follow != null)
                {
                    follow.FollowOffset = new Vector3(0f, 0f, cameraDistanceZ);
                }
            }
        }
    }

    public void ResetAimOffsetAssist()
    {
        runtimeAimOffsetMultiplier = 1f;
        runtimeMouseDistanceMultiplier = 1f;
    }

    public bool IsCinematicFocusActive => cinematicFocusActive;
    public bool IsCinematicInputOffsetLocked => cinematicInputOffsetLocked;
}
