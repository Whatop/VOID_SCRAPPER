using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class WorldGaugeFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.1f, 0f);

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Option")]
    [SerializeField] private bool hideWhenTargetMissing = true;
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool startHidden = true;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private CanvasGroup canvasGroup;

    private bool visibleRequested;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void Awake()
    {
        CacheReferences();

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

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
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

        Vector3 targetWorldPosition = target.position + worldOffset;
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