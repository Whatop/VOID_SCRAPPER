using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Activation Progress")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private CanvasGroup progressCanvasGroup;
    [SerializeField] private GameObject progressRoot;

    [Header("Canvas Follow")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Display")]
    [SerializeField] private string prefix = "E";
    [SerializeField] private string fallbackPrompt = "상호작용";

    [Header("Follow")]
    [SerializeField] private Vector3 defaultWorldOffset = new Vector3(1.7f, 1f, 0f);
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool followEveryFrame = true;

    [Header("World Space Canvas")]
    [SerializeField] private bool matchWorldCanvasRotation = true;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;

    private IInteractable currentTarget;
    private Transform currentTargetTransform;
    private Vector3 currentTargetOffset;

    private CoreObject forcedCoreTarget;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        promptText = GetComponentInChildren<TextMeshProUGUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        rootObject = gameObject;
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;

        progressSlider = GetComponentInChildren<Slider>(true);

        if (progressSlider != null)
        {
            progressRoot = progressSlider.gameObject;
            progressCanvasGroup = progressSlider.GetComponent<CanvasGroup>();
        }
    }

    private void Awake()
    {
        CacheReferences();

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        SetVisible(false);
        SetProgressVisible(false, 0f);
    }

    private void OnEnable()
    {
        CacheReferences();

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

        CoreObject.ActivationProgressChanged += HandleCoreActivationProgressChanged;
    }

    private void OnDisable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentTargetChanged -= HandleTargetChanged;
        }

        CoreObject.ActivationProgressChanged -= HandleCoreActivationProgressChanged;

        forcedCoreTarget = null;
        ClearTarget();
        SetVisible(false);
        SetProgressVisible(false, 0f);
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

        if (progressSlider == null)
        {
            progressSlider = GetComponentInChildren<Slider>(true);
        }

        if (progressSlider != null && progressRoot == null)
        {
            progressRoot = progressSlider.gameObject;
        }

        if (progressRoot != null)
        {
            if (!progressRoot.activeSelf)
            {
                progressRoot.SetActive(true);
            }

            if (progressCanvasGroup == null)
            {
                progressCanvasGroup = progressRoot.GetComponent<CanvasGroup>();
            }

            if (progressCanvasGroup == null)
            {
                progressCanvasGroup = progressRoot.AddComponent<CanvasGroup>();
            }
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
        if (forcedCoreTarget != null)
        {
            return;
        }

        SetProgressVisible(false, 0f);

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

        RefreshPromptText(target);
        FollowTarget();
    }

    private void HandleCoreActivationProgressChanged(CoreObject core, float ratio, bool active)
    {
        if (core == null)
        {
            return;
        }

        if (active)
        {
            forcedCoreTarget = core;

            currentTarget = core;
            currentTargetTransform = core.transform;
            currentTargetOffset = defaultWorldOffset;

            RefreshPromptText(core);
            FollowTarget();
            SetProgressVisible(true, ratio);
            return;
        }

        SetProgressVisible(false, 0f);

        if (forcedCoreTarget == core || ReferenceEquals(currentTarget, core))
        {
            forcedCoreTarget = null;
            ClearTarget();
            SetVisible(false);
        }
    }

    private void RefreshPromptText(IInteractable target)
    {
        if (promptText == null || target == null)
        {
            return;
        }

        string interactionText = string.IsNullOrWhiteSpace(target.InteractionText)
            ? fallbackPrompt
            : target.InteractionText;

        promptText.text = $"{prefix}  {interactionText}";
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

        CacheReferences();

        if (rectTransform == null || canvas == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPosition = currentTargetTransform.position + currentTargetOffset;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (hideWhenBehindCamera && worldCamera != null)
        {
            Vector3 screenPositionForCheck = worldCamera.WorldToScreenPoint(worldPosition);

            if (screenPositionForCheck.z < 0f)
            {
                SetVisible(false);
                return;
            }
        }

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            rectTransform.position = worldPosition;

            if (matchWorldCanvasRotation)
            {
                rectTransform.rotation = canvas.transform.rotation;
            }

            SetVisible(true);
            return;
        }

        if (worldCamera == null || canvasRectTransform == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
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
        if (rootObject != null && !rootObject.activeSelf)
        {
            rootObject.SetActive(true);
        }

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

    private void SetProgressVisible(bool visible, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (progressRoot == null && progressSlider != null)
        {
            progressRoot = progressSlider.gameObject;
        }

        if (progressRoot != null && !progressRoot.activeSelf)
        {
            progressRoot.SetActive(true);
        }

        if (progressCanvasGroup == null && progressRoot != null)
        {
            progressCanvasGroup = progressRoot.GetComponent<CanvasGroup>();

            if (progressCanvasGroup == null)
            {
                progressCanvasGroup = progressRoot.AddComponent<CanvasGroup>();
            }
        }

        if (progressSlider != null)
        {
            progressSlider.value = ratio;
        }

        if (progressCanvasGroup != null)
        {
            progressCanvasGroup.alpha = visible ? 1f : 0f;
            progressCanvasGroup.interactable = false;
            progressCanvasGroup.blocksRaycasts = false;
        }
    }
}