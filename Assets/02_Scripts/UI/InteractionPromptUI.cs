using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class InteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject rootObject;

    [Header("Canvas Follow")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Display")]
    [SerializeField] private string prefix = "E";
    [SerializeField] private string fallbackPrompt = "상호작용";

    [Header("Follow")]
    [Tooltip("InteractionPromptAnchor가 없는 오브젝트에 적용할 기본 위치 오프셋")]
    [SerializeField] private Vector3 defaultWorldOffset = new Vector3(0f, 1f, 0f);

    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool followEveryFrame = true;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;

    private IInteractable currentTarget;
    private Transform currentTargetTransform;
    private Vector3 currentTargetOffset;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        promptText = GetComponentInChildren<TextMeshProUGUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        rootObject = gameObject;
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;
    }

    private void Awake()
    {
        CacheReferences();

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentTargetChanged += HandleTargetChanged;
            HandleTargetChanged(playerInteractor.CurrentTarget);
        }
        else
        {
            ClearTarget();
            SetVisible(false);
        }
    }

    private void OnDisable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentTargetChanged -= HandleTargetChanged;
        }

        ClearTarget();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (!followEveryFrame)
        {
            return;
        }

        if (currentTarget == null || currentTargetTransform == null)
        {
            SetVisible(false);
            return;
        }

        FollowTarget();
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (promptText == null)
        {
            promptText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (rootObject == null)
        {
            rootObject = gameObject;
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

    private void HandleTargetChanged(IInteractable target)
    {
        if (target == null)
        {
            ClearTarget();
            SetVisible(false);
            return;
        }

        if (!TryResolveTargetTransform(target, out Transform targetTransform, out Vector3 targetOffset))
        {
            ClearTarget();
            SetVisible(false);
            return;
        }

        currentTarget = target;
        currentTargetTransform = targetTransform;
        currentTargetOffset = targetOffset;

        string interactionText = string.IsNullOrWhiteSpace(target.InteractionText)
            ? fallbackPrompt
            : target.InteractionText;

        if (promptText != null)
        {
            promptText.text = $"{prefix}  {interactionText}";
        }

        FollowTarget();
    }

    private bool TryResolveTargetTransform(
        IInteractable target,
        out Transform targetTransform,
        out Vector3 targetOffset)
    {
        targetTransform = null;
        targetOffset = defaultWorldOffset;

        if (target is not Component component)
        {
            return false;
        }

        InteractionPromptAnchor anchor = component.GetComponentInChildren<InteractionPromptAnchor>(true);
        if (anchor != null && anchor.AnchorTransform != null)
        {
            targetTransform = anchor.AnchorTransform;
            targetOffset = anchor.WorldOffset;
            return true;
        }

        targetTransform = component.transform;
        targetOffset = defaultWorldOffset;
        return true;
    }

    private void FollowTarget()
    {
        if (currentTargetTransform == null)
        {
            SetVisible(false);
            return;
        }

        if (rectTransform == null || canvas == null || canvasRectTransform == null)
        {
            CacheReferences();

            if (rectTransform == null || canvas == null || canvasRectTransform == null)
            {
                SetVisible(false);
                return;
            }
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;

            if (worldCamera == null)
            {
                SetVisible(false);
                return;
            }
        }

        Vector3 worldPosition = currentTargetTransform.position + currentTargetOffset;
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

        if (hideWhenBehindCamera && screenPosition.z < 0f)
        {
            SetVisible(false);
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
            SetVisible(false);
            return;
        }

        rectTransform.anchoredPosition = localPoint;
        SetVisible(true);
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

    private void ClearTarget()
    {
        currentTarget = null;
        currentTargetTransform = null;
        currentTargetOffset = Vector3.zero;
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (rootObject != null)
        {
            rootObject.SetActive(visible);
        }
    }
}