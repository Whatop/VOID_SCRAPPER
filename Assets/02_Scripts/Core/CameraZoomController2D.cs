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

    public float BaseOrthographicSize => baseOrthographicSize;
    public float TargetZoomMultiplier => targetZoomMultiplier;

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
        ResolveReferences();

        bool zoomedOut = targetZoomMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold);

        if (zoomedOut)
        {
            DisablePixelPerfectComponents();
        }

        ApplyZoom(false);

        if (!zoomedOut)
        {
            RestorePixelPerfectComponents();
        }
    }

    private void OnDisable()
    {
        RestorePixelPerfectComponents();
    }

    private void OnDestroy()
    {
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

        if (targetZoomMultiplier > Mathf.Max(1f, pixelPerfectDisableThreshold))
        {
            DisablePixelPerfectComponents();
        }

        ApplyZoom(immediate);
    }

    public void ResetZoom()
    {
        ResetZoom(false);
    }

    public void ResetZoom(bool immediate)
    {
        targetZoomMultiplier = 1f;
        ApplyZoom(immediate);
        RestorePixelPerfectComponents();
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
}