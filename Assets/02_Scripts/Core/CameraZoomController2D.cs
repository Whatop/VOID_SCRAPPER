using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraZoomController2D : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Camera targetCamera;

    [Header("Zoom")]
    [SerializeField] private float baseOrthographicSize = 4.2f;
    [SerializeField] private float zoomSmoothSpeed = 10f;

    [Header("Cinematic Transition")]
    [SerializeField] private bool useUnscaledTimeForTransitions = true;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float baseZoomTolerance = 0.0025f;

    [Header("Pixel Perfect Override")]
    [SerializeField] private bool disablePixelPerfectWhileZoomedOut = true;
    [SerializeField] private float pixelPerfectDisableThreshold = 1.01f;
    [SerializeField] private bool logPixelPerfectOverride;

    private readonly List<MonoBehaviour> pixelPerfectComponents = new List<MonoBehaviour>();
    private readonly Dictionary<MonoBehaviour, bool> pixelPerfectOriginalStates = new Dictionary<MonoBehaviour, bool>();

    private float targetZoomMultiplier = 1f;
    private bool initialized;
    private bool pixelPerfectScanDone;
    private bool pixelPerfectOverridden;
    private bool cinematicTransitionActive;

    public float BaseOrthographicSize => baseOrthographicSize;
    public float TargetZoomMultiplier => targetZoomMultiplier;
    public float CurrentOrthographicSize => ResolveCurrentOrthographicSize();
    public float CurrentZoomMultiplier => baseOrthographicSize <= 0f
        ? 1f
        : ResolveCurrentOrthographicSize() / baseOrthographicSize;
    public bool IsCinematicTransitionActive => cinematicTransitionActive;
    public bool IsAtBaseZoom => Mathf.Abs(CurrentZoomMultiplier - 1f) <= Mathf.Max(0.0001f, baseZoomTolerance);

    public bool HasCinemachineCamera
    {
        get
        {
            ResolveReferences();
            return cinemachineCamera != null;
        }
    }

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        targetCamera = GetComponent<Camera>();
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

        float currentMultiplier = CurrentZoomMultiplier;
        bool zoomedOut =
            cinematicTransitionActive ||
            targetZoomMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold) ||
            currentMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold);

        if (zoomedOut)
        {
            DisablePixelPerfectComponents();
        }

        ApplyZoom(false);

        currentMultiplier = CurrentZoomMultiplier;

        if (!cinematicTransitionActive &&
            targetZoomMultiplier <= Mathf.Max(1f, pixelPerfectDisableThreshold) &&
            currentMultiplier <= Mathf.Max(1f, pixelPerfectDisableThreshold) + baseZoomTolerance)
        {
            RestorePixelPerfectComponents();
        }
    }

    private void OnDisable()
    {
        cinematicTransitionActive = false;
        RestorePixelPerfectComponents();
    }

    private void OnDestroy()
    {
        cinematicTransitionActive = false;
        RestorePixelPerfectComponents();
    }

    public void Bind(CinemachineCamera cmCamera, Camera camera)
    {
        cinemachineCamera = cmCamera;
        targetCamera = camera;
        pixelPerfectScanDone = false;

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

        targetZoomMultiplier = Mathf.Max(0.1f, multiplier);

        if (targetZoomMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold) ||
            CurrentZoomMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold))
        {
            DisablePixelPerfectComponents();
        }

        ApplyZoom(immediate);

        if (!cinematicTransitionActive && immediate &&
            targetZoomMultiplier <= Mathf.Max(1f, pixelPerfectDisableThreshold) &&
            IsAtBaseZoom)
        {
            RestorePixelPerfectComponents();
        }
    }

    public void ResetZoom()
    {
        ResetZoom(false);
    }

    public void ResetZoom(bool immediate)
    {
        targetZoomMultiplier = 1f;
        ApplyZoom(immediate);

        if (immediate || IsAtBaseZoom)
        {
            RestorePixelPerfectComponents();
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

        if (Mathf.Max(startMultiplier, endMultiplier) > Mathf.Max(1f, pixelPerfectDisableThreshold))
        {
            DisablePixelPerfectComponents();
        }

        if (safeDuration <= 0.0001f)
        {
            targetZoomMultiplier = endMultiplier;
            ApplyZoom(true);
            progressCallback?.Invoke(1f, endMultiplier);
            cinematicTransitionActive = false;
            TryRestorePixelPerfectAtBase();
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
        TryRestorePixelPerfectAtBase();
    }

    public void CancelCinematicTransition(bool restoreBaseZoom)
    {
        cinematicTransitionActive = false;

        if (restoreBaseZoom)
        {
            targetZoomMultiplier = 1f;
            ApplyZoom(true);
        }

        TryRestorePixelPerfectAtBase();
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
        cinemachineCamera = null;
        targetCamera = null;
        pixelPerfectScanDone = false;

        ResolveReferences();
        CaptureBaseSizeIfPossible();
    }

    private void ResolveReferences()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main;
        }
    }

    private void CaptureBaseSizeIfPossible()
    {
        if (cinemachineCamera != null)
        {
            baseOrthographicSize = Mathf.Max(0.1f, cinemachineCamera.Lens.OrthographicSize);
            return;
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            baseOrthographicSize = Mathf.Max(0.1f, targetCamera.orthographicSize);
        }
    }

    private float ResolveCurrentOrthographicSize()
    {
        ResolveReferences();

        if (cinemachineCamera != null)
        {
            return Mathf.Max(0.1f, cinemachineCamera.Lens.OrthographicSize);
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            return Mathf.Max(0.1f, targetCamera.orthographicSize);
        }

        return Mathf.Max(0.1f, baseOrthographicSize);
    }

    private void ApplyZoom(bool immediate)
    {
        if (!initialized)
        {
            CaptureBaseSizeIfPossible();
            initialized = true;
        }

        float targetSize = Mathf.Max(0.1f, baseOrthographicSize * targetZoomMultiplier);

        if (cinemachineCamera != null)
        {
            if (immediate || zoomSmoothSpeed <= 0f)
            {
                cinemachineCamera.Lens.OrthographicSize = targetSize;
            }
            else
            {
                float current = cinemachineCamera.Lens.OrthographicSize;
                float t = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime);
                cinemachineCamera.Lens.OrthographicSize = Mathf.Lerp(current, targetSize, t);
            }

            return;
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            if (immediate || zoomSmoothSpeed <= 0f)
            {
                targetCamera.orthographicSize = targetSize;
            }
            else
            {
                float t = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime);
                targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, t);
            }
        }
    }

    private void TryRestorePixelPerfectAtBase()
    {
        if (!cinematicTransitionActive &&
            targetZoomMultiplier <= Mathf.Max(1f, pixelPerfectDisableThreshold) &&
            IsAtBaseZoom)
        {
            RestorePixelPerfectComponents();
        }
    }

    private void DisablePixelPerfectComponents()
    {
        if (!disablePixelPerfectWhileZoomedOut)
        {
            return;
        }

        RefreshPixelPerfectComponentCacheIfNeeded();

        for (int i = 0; i < pixelPerfectComponents.Count; i++)
        {
            MonoBehaviour component = pixelPerfectComponents[i];

            if (component == null)
            {
                continue;
            }

            if (!pixelPerfectOriginalStates.ContainsKey(component))
            {
                pixelPerfectOriginalStates.Add(component, component.enabled);
            }

            component.enabled = false;
        }

        if (!pixelPerfectOverridden && pixelPerfectComponents.Count > 0 && logPixelPerfectOverride)
        {
            Debug.Log("Pixel Perfect 컴포넌트를 줌아웃 동안 임시 비활성화합니다.", this);
        }

        pixelPerfectOverridden = pixelPerfectComponents.Count > 0;
    }

    private void RestorePixelPerfectComponents()
    {
        if (!pixelPerfectOverridden && pixelPerfectOriginalStates.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<MonoBehaviour, bool> pair in pixelPerfectOriginalStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        pixelPerfectOriginalStates.Clear();
        pixelPerfectOverridden = false;
    }

    private void RefreshPixelPerfectComponentCacheIfNeeded()
    {
        if (pixelPerfectScanDone)
        {
            RemoveNullPixelPerfectEntries();
            return;
        }

        pixelPerfectComponents.Clear();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            if (IsPixelPerfectComponent(behaviour))
            {
                pixelPerfectComponents.Add(behaviour);
            }
        }

        pixelPerfectScanDone = true;
    }

    private void RemoveNullPixelPerfectEntries()
    {
        for (int i = pixelPerfectComponents.Count - 1; i >= 0; i--)
        {
            if (pixelPerfectComponents[i] == null)
            {
                pixelPerfectComponents.RemoveAt(i);
            }
        }
    }

    private bool IsPixelPerfectComponent(MonoBehaviour component)
    {
        string typeName = component.GetType().Name;
        return typeName == "PixelPerfectCamera" || typeName == "CinemachinePixelPerfect";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseOrthographicSize = Mathf.Max(0.1f, baseOrthographicSize);
        zoomSmoothSpeed = Mathf.Max(0f, zoomSmoothSpeed);
        pixelPerfectDisableThreshold = Mathf.Max(1f, pixelPerfectDisableThreshold);
        baseZoomTolerance = Mathf.Max(0.0001f, baseZoomTolerance);

        if (defaultTransitionCurve == null || defaultTransitionCurve.length == 0)
        {
            defaultTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }
#endif
}
