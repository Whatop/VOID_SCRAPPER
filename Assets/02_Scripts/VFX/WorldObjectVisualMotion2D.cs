using UnityEngine;

/// <summary>
/// 게임 판정용 Root를 움직이지 않고, 자식 Visual만 부유/회전시키는 공통 연출 컴포넌트입니다.
/// 반드시 Collider, RadarTarget, IInteractable이 붙은 Root가 아니라 VisualRoot 자식에 붙이세요.
/// </summary>
[DisallowMultipleComponent]
public class WorldObjectVisualMotion2D : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("비워두면 이 컴포넌트가 붙은 Transform을 움직입니다. 판정 Root가 아닌 VisualRoot를 권장합니다.")]
    [SerializeField] private Transform visualTarget;

    [Header("Float")]
    [SerializeField] private bool useFloat = true;
    [SerializeField] private Vector2 localFloatAxis = Vector2.up;
    [Min(0f)]
    [SerializeField] private float floatAmplitude = 0.05f;
    [Min(0.05f)]
    [SerializeField] private float floatDuration = 2.4f;
    [SerializeField] private bool randomizeFloatPhase = true;

    [Header("Rotation")]
    [SerializeField] private bool useContinuousRotation;
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(-2f, 2f);

    [SerializeField] private bool useRotationSway = true;
    [SerializeField] private float rotationSwayDegrees = 1f;
    [Min(0.05f)]
    [SerializeField] private float rotationSwayDuration = 3f;
    [SerializeField] private bool randomizeRotationPhase = true;

    [Header("Scale Pulse Optional")]
    [SerializeField] private bool useScalePulse;
    [Range(0f, 0.25f)]
    [SerializeField] private float scalePulseAmount = 0.02f;
    [Min(0.05f)]
    [SerializeField] private float scalePulseDuration = 2f;

    [Header("Runtime")]
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private bool restoreBaseTransformOnDisable = true;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;

    private float floatPhase;
    private float rotationPhase;
    private float scalePhase;
    private float continuousRotation;
    private float continuousRotationSpeed;
    private bool captured;
    private bool motionEnabled = true;

    public bool MotionEnabled => motionEnabled;

    private void Reset()
    {
        visualTarget = transform;
    }

    private void Awake()
    {
        ResolveTarget();
        CaptureBaseTransform();
    }

    private void OnEnable()
    {
        ResolveTarget();
        CaptureBaseTransform();
        RandomizeRuntime();
    }

    private void LateUpdate()
    {
        if (!motionEnabled || visualTarget == null || !captured)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;

        ApplyPosition(time);
        ApplyRotation(time, deltaTime);
        ApplyScale(time);
    }

    private void OnDisable()
    {
        if (restoreBaseTransformOnDisable)
        {
            RestoreBaseTransform();
        }
    }

    public void SetMotionEnabled(bool enabled, bool restoreWhenDisabled = true)
    {
        motionEnabled = enabled;

        if (!enabled && restoreWhenDisabled)
        {
            RestoreBaseTransform();
        }
    }

    public void RecaptureBaseTransform()
    {
        CaptureBaseTransform();
        RandomizeRuntime();
    }

    private void ResolveTarget()
    {
        if (visualTarget == null)
        {
            visualTarget = transform;
        }
    }

    private void CaptureBaseTransform()
    {
        if (visualTarget == null)
        {
            captured = false;
            return;
        }

        baseLocalPosition = visualTarget.localPosition;
        baseLocalRotation = visualTarget.localRotation;
        baseLocalScale = visualTarget.localScale;
        continuousRotation = 0f;
        captured = true;
    }

    private void RandomizeRuntime()
    {
        floatPhase = randomizeFloatPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        rotationPhase = randomizeRotationPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        scalePhase = Random.Range(0f, Mathf.PI * 2f);
        continuousRotationSpeed = Random.Range(
            Mathf.Min(rotationSpeedRange.x, rotationSpeedRange.y),
            Mathf.Max(rotationSpeedRange.x, rotationSpeedRange.y)
        );
    }

    private void ApplyPosition(float time)
    {
        if (!useFloat || floatAmplitude <= 0f)
        {
            visualTarget.localPosition = baseLocalPosition;
            return;
        }

        Vector2 axis = localFloatAxis.sqrMagnitude > 0.001f
            ? localFloatAxis.normalized
            : Vector2.up;

        float frequency = Mathf.PI * 2f / Mathf.Max(0.05f, floatDuration);
        float offset = Mathf.Sin(time * frequency + floatPhase) * floatAmplitude;

        visualTarget.localPosition = baseLocalPosition + (Vector3)(axis * offset);
    }

    private void ApplyRotation(float time, float deltaTime)
    {
        float z = 0f;

        if (useContinuousRotation)
        {
            continuousRotation += continuousRotationSpeed * deltaTime;
            z += continuousRotation;
        }

        if (useRotationSway && Mathf.Abs(rotationSwayDegrees) > 0.001f)
        {
            float frequency = Mathf.PI * 2f / Mathf.Max(0.05f, rotationSwayDuration);
            z += Mathf.Sin(time * frequency + rotationPhase) * rotationSwayDegrees;
        }

        visualTarget.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, z);
    }

    private void ApplyScale(float time)
    {
        if (!useScalePulse || scalePulseAmount <= 0f)
        {
            visualTarget.localScale = baseLocalScale;
            return;
        }

        float frequency = Mathf.PI * 2f / Mathf.Max(0.05f, scalePulseDuration);
        float scale = 1f + Mathf.Sin(time * frequency + scalePhase) * scalePulseAmount;
        visualTarget.localScale = baseLocalScale * scale;
    }

    private void RestoreBaseTransform()
    {
        if (visualTarget == null || !captured)
        {
            return;
        }

        visualTarget.localPosition = baseLocalPosition;
        visualTarget.localRotation = baseLocalRotation;
        visualTarget.localScale = baseLocalScale;
    }
}
