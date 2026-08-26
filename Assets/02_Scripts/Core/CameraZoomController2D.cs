using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public class CameraZoomController2D : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Camera targetCamera;

    [Header("Zoom")]
    [SerializeField] private float baseOrthographicSize = 4.21875f;
    [SerializeField] private float zoomSmoothSpeed = 10f;

    [Header("Cinematic Transition")]
    [SerializeField] private bool useUnscaledTimeForTransitions = true;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float baseZoomTolerance = 0.0025f;

    private float targetZoomMultiplier = 1f;
    private bool initialized;
    private bool cinematicTransitionActive;
    private int cinematicZoomHoldCount;
    private float cinematicZoomHoldMultiplier = 1f;
    private object gameplayFramingOwner;
    private float gameplayFramingMultiplier = 1f;

    public float BaseOrthographicSize => baseOrthographicSize;
    public float TargetZoomMultiplier => targetZoomMultiplier;
    public float CurrentOrthographicSize => ResolveCurrentOrthographicSize();
    public float CurrentZoomMultiplier => baseOrthographicSize <= 0f
        ? 1f
        : ResolveCurrentOrthographicSize() / baseOrthographicSize;
    public bool IsCinematicTransitionActive => cinematicTransitionActive;
    public bool IsCinematicZoomHeld => cinematicZoomHoldCount > 0;
    public float GameplayFramingMultiplier => gameplayFramingMultiplier;
    public float GameplayOrthographicSize => baseOrthographicSize * gameplayFramingMultiplier;
    public bool IsAtBaseZoom => Mathf.Abs(CurrentZoomMultiplier - 1f) <= Mathf.Max(0.0001f, baseZoomTolerance);

    public bool IsAtBaseZoomWithin(float orthographicSizeTolerance)
    {
        return Mathf.Abs(CurrentOrthographicSize - BaseOrthographicSize) <=
               Mathf.Max(0.0001f, orthographicSizeTolerance);
    }

    public bool IsAtGameplayZoomWithin(float orthographicSizeTolerance)
    {
        return Mathf.Abs(CurrentOrthographicSize - GameplayOrthographicSize) <=
               Mathf.Max(0.0001f, orthographicSizeTolerance);
    }

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseSizeIfPossible();
        initialized = true;
    }

    private void LateUpdate()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }

        ResolveReferences();

        if (IsCinematicZoomHeld)
        {
            targetZoomMultiplier = cinematicZoomHoldMultiplier;
        }

        ApplyZoom(false);
    }

    private void OnDisable()
    {
        cinematicTransitionActive = false;
        cinematicZoomHoldCount = 0;
        cinematicZoomHoldMultiplier = 1f;
        gameplayFramingOwner = null;
        gameplayFramingMultiplier = 1f;
    }

    public void Bind(Camera camera)
    {
        targetCamera = camera;

        if (!initialized || baseOrthographicSize <= 0f)
        {
            CaptureBaseSizeIfPossible();
        }

        initialized = true;
    }

    public void SetZoomMultiplier(float multiplier)
    {
        SetZoomMultiplier(multiplier, false);
    }

    public void SetZoomMultiplier(float multiplier, bool immediate)
    {
        ResolveReferences();

        if (IsCinematicZoomHeld)
        {
            targetZoomMultiplier = cinematicZoomHoldMultiplier;
            ApplyZoom(true);
            return;
        }

        targetZoomMultiplier = Mathf.Max(0.1f, multiplier);
        ApplyZoom(immediate);
    }

    public void SetEffectiveZoomMultiplier(float multiplier, bool immediate)
    {
        float framingMultiplier = Mathf.Max(0.1f, gameplayFramingMultiplier);
        SetZoomMultiplier(Mathf.Max(0.1f, multiplier) / framingMultiplier, immediate);
    }

    public bool AcquireGameplayFramingProfile(
        object owner,
        float orthographicSizeMultiplier,
        bool immediate)
    {
        if (owner == null)
        {
            return false;
        }

        if (gameplayFramingOwner != null && !ReferenceEquals(gameplayFramingOwner, owner))
        {
            return false;
        }

        gameplayFramingOwner = owner;
        gameplayFramingMultiplier = Mathf.Max(0.1f, orthographicSizeMultiplier);
        ApplyZoom(immediate);
        return true;
    }

    public void ReleaseGameplayFramingProfile(object owner, bool immediate)
    {
        if (owner == null || !ReferenceEquals(gameplayFramingOwner, owner))
        {
            return;
        }

        gameplayFramingOwner = null;
        gameplayFramingMultiplier = 1f;
        ApplyZoom(immediate);
    }

    public void ResetZoom()
    {
        ResetZoom(false);
    }

    public void ResetZoom(bool immediate)
    {
        if (IsCinematicZoomHeld)
        {
            targetZoomMultiplier = cinematicZoomHoldMultiplier;
            ApplyZoom(true);
            return;
        }

        targetZoomMultiplier = 1f;
        ApplyZoom(immediate);
    }

    public void BeginCinematicZoomHold(float multiplier)
    {
        cinematicZoomHoldCount++;
        cinematicZoomHoldMultiplier = Mathf.Max(0.1f, multiplier);
        targetZoomMultiplier = cinematicZoomHoldMultiplier;
        cinematicTransitionActive = false;
        ApplyZoom(true);
    }

    public void EndCinematicZoomHold(bool keepCurrentZoom = true)
    {
        if (cinematicZoomHoldCount <= 0)
        {
            return;
        }

        cinematicZoomHoldCount--;
        if (cinematicZoomHoldCount > 0)
        {
            return;
        }

        if (keepCurrentZoom)
        {
            targetZoomMultiplier = CurrentZoomMultiplier /
                                   Mathf.Max(0.1f, gameplayFramingMultiplier);
        }
        else
        {
            targetZoomMultiplier = 1f;
            ApplyZoom(true);
        }
    }

    public void ClearCinematicZoomHold(bool restoreBaseZoom)
    {
        cinematicZoomHoldCount = 0;
        cinematicZoomHoldMultiplier = 1f;

        if (restoreBaseZoom)
        {
            targetZoomMultiplier = 1f;
            ApplyZoom(true);
        }
        else
        {
            targetZoomMultiplier = CurrentZoomMultiplier /
                                   Mathf.Max(0.1f, gameplayFramingMultiplier);
        }
    }

    public IEnumerator AnimateZoomMultiplier(
        float multiplier,
        float duration,
        AnimationCurve curve = null,
        Action<float, float> progressCallback = null)
    {
        ResolveReferences();

        float startMultiplier = CurrentZoomMultiplier;
        float endMultiplier = Mathf.Max(0.1f, multiplier);
        float safeDuration = Mathf.Max(0f, duration);
        AnimationCurve transitionCurve = curve != null && curve.length > 0
            ? curve
            : defaultTransitionCurve;

        cinematicTransitionActive = true;

        if (safeDuration <= 0.0001f)
        {
            targetZoomMultiplier = endMultiplier;
            ApplyZoom(true);
            progressCallback?.Invoke(1f, endMultiplier);
            cinematicTransitionActive = false;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            float deltaTime = useUnscaledTimeForTransitions
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            elapsed += Mathf.Max(0f, deltaTime);
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            float eased = transitionCurve != null && transitionCurve.length > 0
                ? Mathf.Clamp01(transitionCurve.Evaluate(normalized))
                : Mathf.SmoothStep(0f, 1f, normalized);
            float currentMultiplier = Mathf.LerpUnclamped(startMultiplier, endMultiplier, eased);

            targetZoomMultiplier = currentMultiplier;
            ApplyZoom(true);
            progressCallback?.Invoke(eased, currentMultiplier);
            yield return null;
        }

        targetZoomMultiplier = endMultiplier;
        ApplyZoom(true);
        progressCallback?.Invoke(1f, endMultiplier);
        cinematicTransitionActive = false;
    }

    public void CancelCinematicTransition(bool restoreBaseZoom)
    {
        cinematicTransitionActive = false;

        if (restoreBaseZoom && !IsCinematicZoomHeld)
        {
            targetZoomMultiplier = 1f;
            ApplyZoom(true);
        }
        else if (IsCinematicZoomHeld)
        {
            targetZoomMultiplier = cinematicZoomHoldMultiplier;
            ApplyZoom(true);
        }
    }

    public void SetBaseOrthographicSize(float size)
    {
        baseOrthographicSize = Mathf.Max(0.1f, size);
    }

    public void CaptureCurrentAsBase()
    {
        ResolveReferences();
        CaptureBaseSizeIfPossible();
    }

    public void RebindToCurrentSceneCamera()
    {
        targetCamera = null;
        ResolveReferences();
        CaptureBaseSizeIfPossible();
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void CaptureBaseSizeIfPossible()
    {
        if (targetCamera != null && targetCamera.orthographic)
        {
            baseOrthographicSize = Mathf.Max(0.1f, targetCamera.orthographicSize);
        }
    }

    private float ResolveCurrentOrthographicSize()
    {
        ResolveReferences();

        if (targetCamera != null && targetCamera.orthographic)
        {
            return Mathf.Max(0.1f, targetCamera.orthographicSize);
        }

        return Mathf.Max(0.1f, baseOrthographicSize);
    }

    private void ApplyZoom(bool immediate)
    {
        ResolveReferences();

        if (!initialized)
        {
            CaptureBaseSizeIfPossible();
            initialized = true;
        }

        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        float resolvedMultiplier = IsCinematicZoomHeld
            ? cinematicZoomHoldMultiplier
            : targetZoomMultiplier * gameplayFramingMultiplier;
        float targetSize = Mathf.Max(0.1f, baseOrthographicSize * resolvedMultiplier);
        if (immediate || zoomSmoothSpeed <= 0f)
        {
            targetCamera.orthographicSize = targetSize;
            return;
        }

        float blend = 1f - Mathf.Exp(-zoomSmoothSpeed * Mathf.Max(0f, Time.deltaTime));
        targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, blend);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseOrthographicSize = Mathf.Max(0.1f, baseOrthographicSize);
        zoomSmoothSpeed = Mathf.Max(0f, zoomSmoothSpeed);
        baseZoomTolerance = Mathf.Max(0.0001f, baseZoomTolerance);

        if (defaultTransitionCurve == null || defaultTransitionCurve.length == 0)
        {
            defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
#endif
}
