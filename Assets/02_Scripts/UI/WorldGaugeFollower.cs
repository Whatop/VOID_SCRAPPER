using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class WorldGaugeFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Header("Auto Anchor")]
    [Tooltip("켜면 대상의 Renderer/Collider2D 크기 기준으로 위쪽 중앙을 따라갑니다.")]
    [SerializeField] private bool anchorToTargetTop = true;

    [SerializeField] private float targetTopPadding = 0.12f;
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private bool includeChildColliders = true;

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Option")]
    [SerializeField] private bool hideWhenTargetMissing = true;
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool startHidden = true;
    [Tooltip("Round the actual projected screen position once. Authored anchors, pivot and scale are never reset.")]
    [SerializeField] private bool snapToCanvasPixelGrid = true;

    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private CanvasGroup canvasGroup;
    private Component targetComponent;
    private InteractionPromptAnchor targetAnchor;

    private bool visibleRequested;
    private int lastPresentationFrame = -1;
    private Renderer[] targetRenderers = System.Array.Empty<Renderer>();
    private Collider2D[] targetColliders = System.Array.Empty<Collider2D>();

    public void ConfigureRuntime(
        Transform runtimeTarget,
        Vector3 runtimeWorldOffset,
        Canvas runtimeCanvas,
        Camera runtimeWorldCamera)
    {
        target = runtimeTarget;
        targetComponent = runtimeTarget;
        targetAnchor = null;
        worldOffset = runtimeWorldOffset;
        anchorToTargetTop = false;
        includeChildRenderers = false;
        includeChildColliders = false;
        canvas = runtimeCanvas;
        worldCamera = runtimeWorldCamera;
        uiCamera = runtimeCanvas != null && runtimeCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? runtimeCanvas.worldCamera
            : null;
        snapToCanvasPixelGrid = true;
        CacheReferences();
        CacheTargetGeometry();
        SetVisible(false);
    }

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;
    }

    private void Awake()
    {
        CacheReferences();

        if (target != null && targetComponent == null)
        {
            targetComponent = target;
            targetAnchor = target.GetComponentInChildren<InteractionPromptAnchor>(true);
        }
        CacheTargetGeometry();

        if (startHidden)
        {
            SetVisible(false);
        }
    }

    private void OnEnable()
    {
        ReleaseRenderingCallbacks();
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        Camera.onPreRender += HandleCameraPreRender;
        lastPresentationFrame = -1;
    }

    private void OnDisable()
    {
        ReleaseRenderingCallbacks();
    }

    private void OnDestroy()
    {
        ReleaseRenderingCallbacks();
    }

    private void ReleaseRenderingCallbacks()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        Camera.onPreRender -= HandleCameraPreRender;
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        PresentForCamera(renderingCamera);
    }

    private void HandleCameraPreRender(Camera renderingCamera)
    {
        if (GraphicsSettings.currentRenderPipeline == null) PresentForCamera(renderingCamera);
    }

    private void PresentForCamera(Camera renderingCamera)
    {
        // Late player interpolation, camera follow/zoom and the camera's own render
        // projection have completed. Ignore UI/SceneView cameras and repeat callbacks.
        if (!isActiveAndEnabled || renderingCamera != worldCamera || lastPresentationFrame == Time.frameCount)
        {
            return;
        }
        lastPresentationFrame = Time.frameCount;
        Follow();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetComponent = newTarget;
        targetAnchor = newTarget != null ? newTarget.GetComponentInChildren<InteractionPromptAnchor>(true) : null;
        CacheTargetGeometry();

        if (target == null && hideWhenTargetMissing)
        {
            ApplyVisible(false);
        }
    }

    public void SetTarget(Component component)
    {
        targetComponent = component;
        target = component != null ? component.transform : null;
        targetAnchor = component != null ? component.GetComponentInChildren<InteractionPromptAnchor>(true) : null;
        CacheTargetGeometry();

        if (target == null && hideWhenTargetMissing)
        {
            ApplyVisible(false);
        }
    }

    public void SetVisible(bool visible)
    {
        visibleRequested = visible;

        if (!visible)
        {
            ApplyVisible(false);
        }
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        parentRectTransform = transform.parent as RectTransform;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (uiCamera == null && canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }
    }


    private void Follow()
    {
        if (!visibleRequested)
        {
            ApplyVisible(false);
            return;
        }

        if (target == null)
        {
            if (hideWhenTargetMissing)
            {
                ApplyVisible(false);
            }

            return;
        }

        if (rectTransform == null || canvas == null || parentRectTransform == null || worldCamera == null) return;

        Vector3 targetWorldPosition = ResolveTargetWorldPosition();
        Vector3 screenPosition = ProjectWorldToScreen(targetWorldPosition);

        if (hideWhenBehindCamera && screenPosition.z < 0f)
        {
            ApplyVisible(false);
            return;
        }

        Camera eventCamera = GetCanvasEventCamera();

        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRectTransform,
            screenPosition,
            eventCamera,
            out Vector2 localPoint
        );

        if (!converted)
        {
            ApplyVisible(false);
            return;
        }

        // Parent-local position, not Canvas-local coordinates assigned as an
        // anchoredPosition. This respects saved pivots, anchors and nested wrappers.
        rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, rectTransform.localPosition.z);
        ApplyVisible(true);
    }

    private Vector3 ProjectWorldToScreen(Vector3 worldPosition)
    {
        // Never invent a separately snapped camera: WorldToScreenPoint includes
        // the actual projection/view matrices (including real pixel-perfect cameras).
        Vector3 screen = worldCamera.WorldToScreenPoint(worldPosition);
        if (snapToCanvasPixelGrid)
        {
            screen.x = Mathf.Round(screen.x);
            screen.y = Mathf.Round(screen.y);
        }
        return screen;
    }

    private Vector3 ResolveTargetWorldPosition()
    {
        if (targetAnchor != null && targetAnchor.AnchorTransform != null)
        {
            return targetAnchor.AnchorTransform.position + targetAnchor.WorldOffset;
        }

        if (anchorToTargetTop && targetComponent != null && TryCalculateTargetTop(targetComponent, out Vector3 topPosition))
        {
            return topPosition + worldOffset;
        }

        return target.position + worldOffset;
    }

    private void CacheTargetGeometry()
    {
        if (!anchorToTargetTop || targetComponent == null)
        {
            targetRenderers = System.Array.Empty<Renderer>();
            targetColliders = System.Array.Empty<Collider2D>();
            return;
        }
        targetRenderers = includeChildRenderers
            ? targetComponent.GetComponentsInChildren<Renderer>(true)
            : targetComponent.GetComponents<Renderer>();
        targetColliders = includeChildColliders
            ? targetComponent.GetComponentsInChildren<Collider2D>(true)
            : targetComponent.GetComponents<Collider2D>();
    }

    private bool TryCalculateTargetTop(Component component, out Vector3 position)
    {
        position = Vector3.zero;

        if (component == null)
        {
            return false;
        }

        if (TryCalculateRendererBounds(component, out Bounds rendererBounds))
        {
            position = new Vector3(
                rendererBounds.center.x,
                rendererBounds.max.y + Mathf.Max(0f, targetTopPadding),
                component.transform.position.z
            );
            return true;
        }

        if (TryCalculateColliderBounds(component, false, out Bounds nonTriggerBounds) ||
            TryCalculateColliderBounds(component, true, out nonTriggerBounds))
        {
            position = new Vector3(
                nonTriggerBounds.center.x,
                nonTriggerBounds.max.y + Mathf.Max(0f, targetTopPadding),
                component.transform.position.z
            );
            return true;
        }

        return false;
    }

    private bool TryCalculateRendererBounds(Component component, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = targetRenderers;

        if (renderers == null)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryCalculateColliderBounds(Component component, bool includeTriggers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Collider2D[] colliders = targetColliders;

        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!includeTriggers && collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private Camera GetCanvasEventCamera()
    {
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (uiCamera != null)
        {
            return uiCamera;
        }

        if (canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return worldCamera;
    }

    private void ApplyVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
