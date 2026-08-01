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

    [Tooltip("게이지를 InteractionPromptUI 자식이 아니라 별도 오브젝트로 쓰는 경우에만 연결합니다. 일반적으로는 비워두는 것을 권장합니다.")]
    [SerializeField] private WorldGaugeFollower progressWorldFollower;

    [Header("Canvas Follow")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera uiCamera;

    [Header("Display")]
    [SerializeField] private string prefix = "E";
    [SerializeField] private string fallbackPrompt = "상호작용";

    [Header("Auto Anchor")]
    [Tooltip("켜면 대상의 SpriteRenderer/Renderer/Collider2D 크기를 보고 자동으로 위쪽 중앙에 프롬프트를 띄웁니다.")]
    [SerializeField] private bool anchorToTargetTop = true;

    [Tooltip("대상 크기 바로 위에서 얼마나 더 띄울지. 월드 단위입니다.")]
    [SerializeField] private float targetTopPadding = 0.18f;

    [Tooltip("InteractionPromptUI의 피벗을 아래 중앙으로 강제합니다. 텍스트/슬라이더 전체 묶음의 아래가 대상 위에 붙습니다.")]
    [SerializeField] private bool forceBottomCenterPivot = true;

    [Tooltip("자동 크기 계산 실패 시 사용할 월드 오프셋입니다.")]
    [SerializeField] private Vector3 defaultWorldOffset = Vector3.zero;

    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField] private bool includeChildColliders = true;
    [SerializeField] private bool hideWhenBehindCamera = true;
    [SerializeField] private bool followEveryFrame = true;

    [Header("Auto Child Layout")]
    [Tooltip("VerticalLayoutGroup을 쓰지 않는 경우 PromptText와 ProgressRoot를 자동으로 가운데 정렬합니다.")]
    [SerializeField] private bool autoStackTextAndProgress = true;

    [Tooltip("게이지가 보일 때 텍스트와 게이지 사이 간격입니다. UI 픽셀 단위입니다.")]
    [SerializeField] private float textProgressSpacing = 6f;

    [Tooltip("게이지가 안 보일 때 텍스트를 기준점에서 얼마나 올릴지입니다. UI 픽셀 단위입니다.")]
    [SerializeField] private float textOnlyYOffset = 0f;

    [Tooltip("게이지 로컬 Y 위치입니다. 보통 0으로 둡니다.")]
    [SerializeField] private float progressLocalYOffset = 0f;

    [Header("World Space Canvas")]
    [SerializeField] private bool matchWorldCanvasRotation = true;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private RectTransform promptTextRectTransform;
    private RectTransform progressRootRectTransform;
    private LayoutGroup layoutGroup;
    private bool lastProgressVisible;

    private IInteractable currentTarget;
    private Component currentTargetComponent;
    private Transform currentTargetTransform;
    private InteractionPromptAnchor currentAnchor;

    private CoreObject forcedCoreTarget;
    private Component forcedProgressTarget;

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
            progressWorldFollower = progressSlider.GetComponent<WorldGaugeFollower>();
        }

        ApplyPivotOption();
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
            playerInteractor.HoldProgressChanged += HandleInteractionHoldProgressChanged;
            HandleTargetChanged(playerInteractor.CurrentTarget);
        }
        else
        {
            ClearTarget();
            SetVisible(false);
        }

        CoreObject.ActivationProgressChanged += HandleCoreActivationProgressChanged;
        ReinforcementPickup.DismantleProgressChanged += HandleReinforcementDismantleProgressChanged;
        TraitPickup.DismantleProgressChanged += HandleTraitDismantleProgressChanged;
    }

    private void OnDisable()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentTargetChanged -= HandleTargetChanged;
            playerInteractor.HoldProgressChanged -= HandleInteractionHoldProgressChanged;
        }

        CoreObject.ActivationProgressChanged -= HandleCoreActivationProgressChanged;
        ReinforcementPickup.DismantleProgressChanged -= HandleReinforcementDismantleProgressChanged;
        TraitPickup.DismantleProgressChanged -= HandleTraitDismantleProgressChanged;

        forcedCoreTarget = null;
        forcedProgressTarget = null;
        ClearTarget();
        SetVisible(false);
        SetProgressVisible(false, 0f);
    }

    private void LateUpdate()
    {
        if (GameplayPauseManager.IsPaused)
        {
            SetVisible(false);
            SetProgressVisible(false, 0f);
            return;
        }

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

        ApplyPivotOption();

        if (promptText == null)
        {
            promptText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        promptTextRectTransform = promptText != null ? promptText.rectTransform : null;

        if (layoutGroup == null)
        {
            layoutGroup = GetComponent<LayoutGroup>();
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

        if (progressWorldFollower == null && progressSlider != null)
        {
            progressWorldFollower = progressSlider.GetComponent<WorldGaugeFollower>();
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

            progressRootRectTransform = progressRoot.transform as RectTransform;
        }

        UpdatePromptChildLayout(lastProgressVisible);

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

        rectTransform.pivot = new Vector2(0.5f, 0f);
    }

    private void HandleTargetChanged(IInteractable target)
    {
        if (forcedCoreTarget != null || forcedProgressTarget != null)
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

        if (!SetCurrentTarget(target))
        {
            ClearTarget();
            SetVisible(false);
            return;
        }

        RefreshPromptText(target);
        FollowTarget();
    }

    private void HandleInteractionHoldProgressChanged(IInteractable target, float ratio, bool active)
    {
        if (target is not Component component)
        {
            return;
        }

        HandleDismantleProgressChanged(component, ratio, active);
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
            SetCurrentTarget(core);
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

            if (playerInteractor != null)
            {
                HandleTargetChanged(playerInteractor.CurrentTarget);
            }
        }
    }

    private void HandleReinforcementDismantleProgressChanged(ReinforcementPickup pickup, float ratio, bool active)
    {
        HandleDismantleProgressChanged(pickup, ratio, active);
    }

    private void HandleTraitDismantleProgressChanged(TraitPickup pickup, float ratio, bool active)
    {
        HandleDismantleProgressChanged(pickup, ratio, active);
    }

    private void HandleDismantleProgressChanged(Component component, float ratio, bool active)
    {
        if (component == null)
        {
            return;
        }

        if (active)
        {
            forcedProgressTarget = component;

            if (component is IInteractable interactable)
            {
                SetCurrentTarget(interactable);
                RefreshPromptText(interactable);
                FollowTarget();
            }

            SetProgressVisible(true, ratio);
            return;
        }

        if (forcedProgressTarget == component)
        {
            forcedProgressTarget = null;
            SetProgressVisible(false, 0f);

            if (ReferenceEquals(currentTarget, component as IInteractable))
            {
                ClearTarget();
                SetVisible(false);
            }

            if (playerInteractor != null)
            {
                HandleTargetChanged(playerInteractor.CurrentTarget);
            }
        }
    }

    private bool SetCurrentTarget(IInteractable target)
    {
        if (target is not Component component)
        {
            return false;
        }

        currentTarget = target;
        currentTargetComponent = component;
        currentTargetTransform = component.transform;
        currentAnchor = component.GetComponentInChildren<InteractionPromptAnchor>(true);
        return currentTargetTransform != null;
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

        if (target is ReinforcementPickup || target is TraitPickup)
        {
            promptText.text = interactionText;
            return;
        }

        promptText.text = $"{prefix}  {interactionText}";
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

        Vector3 worldPosition = ResolveTargetAnchorWorldPosition();

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
            UpdatePromptChildLayout(lastProgressVisible);

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
        UpdatePromptChildLayout(lastProgressVisible);
        SetVisible(true);
    }

    private Vector3 ResolveTargetAnchorWorldPosition()
    {
        if (currentAnchor != null && currentAnchor.AnchorTransform != null)
        {
            return currentAnchor.AnchorTransform.position + currentAnchor.WorldOffset;
        }

        if (anchorToTargetTop && currentTargetComponent != null && TryCalculateTargetTop(currentTargetComponent, out Vector3 topPosition))
        {
            return topPosition;
        }

        return currentTargetTransform.position + defaultWorldOffset;
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

    private void UpdatePromptChildLayout(bool progressVisible)
    {
        if (!autoStackTextAndProgress || rectTransform == null)
        {
            return;
        }

        // VerticalLayoutGroup/ContentSizeFitter로 직접 배치하는 경우에는 코드가 위치를 덮어쓰지 않는다.
        if (layoutGroup != null && layoutGroup.enabled)
        {
            return;
        }

        bool promptIsChild = promptTextRectTransform != null && promptTextRectTransform.transform.IsChildOf(rectTransform);
        bool progressIsChild = progressRootRectTransform != null && progressRootRectTransform.transform.IsChildOf(rectTransform);

        if (progressIsChild)
        {
            progressRootRectTransform.anchorMin = new Vector2(0.5f, 0f);
            progressRootRectTransform.anchorMax = new Vector2(0.5f, 0f);
            progressRootRectTransform.pivot = new Vector2(0.5f, 0f);
            progressRootRectTransform.anchoredPosition = new Vector2(0f, progressLocalYOffset);
        }

        if (promptIsChild)
        {
            promptText.alignment = TextAlignmentOptions.Center;
            promptTextRectTransform.anchorMin = new Vector2(0.5f, 0f);
            promptTextRectTransform.anchorMax = new Vector2(0.5f, 0f);
            promptTextRectTransform.pivot = new Vector2(0.5f, 0f);

            float y = textOnlyYOffset;

            if (progressVisible && progressIsChild)
            {
                float progressHeight = Mathf.Max(0f, progressRootRectTransform.rect.height);
                y = progressLocalYOffset + progressHeight + Mathf.Max(0f, textProgressSpacing);
            }

            promptTextRectTransform.anchoredPosition = new Vector2(0f, y);
        }
    }

    private void ClearTarget()
    {
        currentTarget = null;
        currentTargetComponent = null;
        currentTargetTransform = null;
        currentAnchor = null;
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
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = ratio;
            progressSlider.interactable = false;
        }

        lastProgressVisible = visible;

        if (progressCanvasGroup != null)
        {
            progressCanvasGroup.alpha = visible ? 1f : 0f;
            progressCanvasGroup.interactable = false;
            progressCanvasGroup.blocksRaycasts = false;
        }

        UpdatePromptChildLayout(visible);

        if (progressWorldFollower != null)
        {
            progressWorldFollower.SetTarget(currentTargetComponent);
            progressWorldFollower.SetVisible(visible);
        }
    }
}
