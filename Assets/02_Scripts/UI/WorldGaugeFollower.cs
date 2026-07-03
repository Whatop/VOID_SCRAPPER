using UnityEngine;

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
    [SerializeField] private bool forceBottomCenterPivot = true;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private CanvasGroup canvasGroup;
    private Component targetComponent;
    private InteractionPromptAnchor targetAnchor;

    private bool visibleRequested;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;
        ApplyPivotOption();
    }

    private void Awake()
    {
        CacheReferences();

        if (target != null && targetComponent == null)
        {
            targetComponent = target;
            targetAnchor = target.GetComponentInChildren<InteractionPromptAnchor>(true);
        }

        if (startHidden)
        {
            SetVisible(false);
        }
    }

    private void LateUpdate()
    {
        Follow();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetComponent = newTarget;
        targetAnchor = newTarget != null ? newTarget.GetComponentInChildren<InteractionPromptAnchor>(true) : null;

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

        ApplyPivotOption();

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

        if (canvas != null)
        {
            canvasRectTransform = canvas.transform as RectTransform;
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

    private void ApplyPivotOption()
    {
        if (!forceBottomCenterPivot || rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
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

        if (rectTransform == null || canvas == null || canvasRectTransform == null)
        {
            CacheReferences();

            if (rectTransform == null || canvas == null || canvasRectTransform == null)
            {
                return;
            }
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;

            if (worldCamera == null)
            {
                return;
            }
        }

        Vector3 targetWorldPosition = ResolveTargetWorldPosition();
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(targetWorldPosition);

        if (hideWhenBehindCamera && screenPosition.z < 0f)
        {
            ApplyVisible(false);
            return;
        }

        Camera eventCamera = GetCanvasEventCamera();

        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            screenPosition,
            eventCamera,
            out Vector2 localPoint
        );

        if (!converted)
        {
            ApplyVisible(false);
            return;
        }

        rectTransform.anchoredPosition = localPoint;
        ApplyVisible(true);
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

        Renderer[] renderers = includeChildRenderers
            ? component.GetComponentsInChildren<Renderer>(true)
            : new[] { component.GetComponent<Renderer>() };

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

        Collider2D[] colliders = includeChildColliders
            ? component.GetComponentsInChildren<Collider2D>(true)
            : new[] { component.GetComponent<Collider2D>() };

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
