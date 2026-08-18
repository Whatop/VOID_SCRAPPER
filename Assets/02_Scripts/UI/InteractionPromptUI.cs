using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private string prefix = "F";
    [SerializeField] private string fallbackPrompt = "상호작용";

    [Header("Compact Prompt Presentation")]
    [SerializeField] private bool configureCompactPrompt = true;
    [SerializeField] private bool useSimplePromptBackground = true;
    [SerializeField] private Vector2 compactPromptSize = new Vector2(200f, 28f);
    [SerializeField] private Vector2 compactPromptTextSize = new Vector2(188f, 18f);
    [SerializeField] private Vector2 compactProgressRootSize = new Vector2(54f, 8f);
    [SerializeField] private Vector2 compactProgressSliderSize = new Vector2(50f, 6f);
    [SerializeField, Min(1f)] private float compactPromptFontSize = 8f;
    [SerializeField, Min(1f)] private float compactPromptMinimumFontSize = 6f;
    [SerializeField] private Color simplePromptBackgroundColor = new Color(0.02f, 0.04f, 0.07f, 0.82f);

    [Header("Dynamic Input Labels")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string dismantleActionName = "Dismantle";

    [Header("Field Loot Detail Card Optional")]
    [SerializeField] private GameObject lootDetailRoot;
    [SerializeField] private Image lootIconImage;
    [SerializeField] private TextMeshProUGUI lootNameText;
    [SerializeField] private TextMeshProUGUI lootCategoryText;
    [SerializeField] private TextMeshProUGUI lootRarityText;
    [SerializeField] private TextMeshProUGUI lootDescriptionText;
    [SerializeField] private TextMeshProUGUI lootOwnedStateText;
    [SerializeField] private TextMeshProUGUI lootPrimaryActionText;
    [SerializeField] private TextMeshProUGUI lootDismantleActionText;
    [SerializeField] private GameObject lootCurrentItemRoot;
    [SerializeField] private Image lootCurrentIconImage;
    [SerializeField] private TextMeshProUGUI lootCurrentNameText;
    [SerializeField] private TextMeshProUGUI lootCurrentDescriptionText;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField, Min(0f)] private float lootDetailSafeMargin = 8f;

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
    private RectTransform lootDetailRectTransform;
    private LayoutGroup layoutGroup;
    private Image simplePromptBackground;
    private bool compactPromptConfigured;
    private bool lastProgressVisible;
    private bool wasGameplayPaused;
    private readonly Vector3[] lootDetailWorldCorners = new Vector3[4];

    private IInteractable currentTarget;
    private Component currentTargetComponent;
    private Transform currentTargetTransform;
    private InteractionPromptAnchor currentAnchor;

    private CoreObject forcedCoreTarget;
    private Component forcedProgressTarget;
    private RunRuntimeTraitStore subscribedTraitStore;
    private PermanentProgress subscribedPermanentProgress;
    private PlayerReinforcementController subscribedReinforcementController;

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
        ConfigureCompactPromptPresentation();

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        ResolveInputActions();
        SetVisible(false);
        SetLootDetailVisible(false);
        SetProgressVisible(false, 0f);
    }

    private void OnEnable()
    {
        CacheReferences();
        ResolveInputActions();
        InputSystem.onActionChange += HandleInputActionChange;

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
        ReinforcementPickup.PresentationChanged += HandleReinforcementPresentationChanged;
        TraitPickup.DismantleProgressChanged += HandleTraitDismantleProgressChanged;
        TraitPickup.PresentationChanged += HandleTraitPresentationChanged;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= HandleInputActionChange;

        if (playerInteractor != null)
        {
            playerInteractor.CurrentTargetChanged -= HandleTargetChanged;
            playerInteractor.HoldProgressChanged -= HandleInteractionHoldProgressChanged;
        }

        CoreObject.ActivationProgressChanged -= HandleCoreActivationProgressChanged;
        ReinforcementPickup.DismantleProgressChanged -= HandleReinforcementDismantleProgressChanged;
        ReinforcementPickup.PresentationChanged -= HandleReinforcementPresentationChanged;
        TraitPickup.DismantleProgressChanged -= HandleTraitDismantleProgressChanged;
        TraitPickup.PresentationChanged -= HandleTraitPresentationChanged;
        UnsubscribeDetailSources();

        forcedCoreTarget = null;
        forcedProgressTarget = null;
        currentTarget = null;
        currentTargetComponent = null;
        currentTargetTransform = null;
        currentAnchor = null;
        lastProgressVisible = false;

        // ExpeditionHUD owns structural activation during cinematics. OnDisable
        // must not call SetActive while that hierarchy is already deactivating.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (progressCanvasGroup != null)
        {
            progressCanvasGroup.alpha = 0f;
            progressCanvasGroup.interactable = false;
            progressCanvasGroup.blocksRaycasts = false;
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        wasGameplayPaused = false;
    }

    private void LateUpdate()
    {
        bool isGameplayPaused = GameplayPauseManager.IsPaused;

        if (isGameplayPaused)
        {
            SetVisible(false);
            SetProgressVisible(false, 0f);
            wasGameplayPaused = true;
            return;
        }

        if (wasGameplayPaused)
        {
            wasGameplayPaused = false;

            if (currentTarget != null)
            {
                RefreshPromptText(currentTarget);
            }
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

        if (lootDetailRoot != null && lootDetailRectTransform == null)
        {
            lootDetailRectTransform = lootDetailRoot.transform as RectTransform;
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

    private void ConfigureCompactPromptPresentation()
    {
        if (compactPromptConfigured || !configureCompactPrompt)
        {
            return;
        }

        compactPromptConfigured = true;

        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        if (rectTransform != null)
        {
            rectTransform.sizeDelta = compactPromptSize;
        }

        textProgressSpacing = 2f;
        textOnlyYOffset = 0f;
        progressLocalYOffset = 0f;

        if (promptText != null)
        {
            promptText.enableAutoSizing = true;
            promptText.fontSizeMin = Mathf.Min(compactPromptMinimumFontSize, compactPromptFontSize);
            promptText.fontSizeMax = Mathf.Max(compactPromptMinimumFontSize, compactPromptFontSize);
            promptText.textWrappingMode = TextWrappingModes.Normal;
            promptText.overflowMode = TextOverflowModes.Ellipsis;
            promptText.maxVisibleLines = 2;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.margin = new Vector4(4f, 1f, 4f, 1f);
        }

        if (promptTextRectTransform != null)
        {
            promptTextRectTransform.anchorMin = new Vector2(0.5f, 0f);
            promptTextRectTransform.anchorMax = new Vector2(0.5f, 0f);
            promptTextRectTransform.pivot = new Vector2(0.5f, 0f);
            promptTextRectTransform.sizeDelta = compactPromptTextSize;
        }

        if (progressRootRectTransform != null)
        {
            progressRootRectTransform.anchorMin = new Vector2(0.5f, 0f);
            progressRootRectTransform.anchorMax = new Vector2(0.5f, 0f);
            progressRootRectTransform.pivot = new Vector2(0.5f, 0f);
            progressRootRectTransform.sizeDelta = compactProgressRootSize;
        }

        RectTransform progressSliderRectTransform = progressSlider != null
            ? progressSlider.transform as RectTransform
            : null;

        if (progressSliderRectTransform != null && progressSliderRectTransform != progressRootRectTransform)
        {
            progressSliderRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            progressSliderRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            progressSliderRectTransform.pivot = new Vector2(0.5f, 0.5f);
            progressSliderRectTransform.anchoredPosition = Vector2.zero;
            progressSliderRectTransform.sizeDelta = compactProgressSliderSize;
        }

        if (useSimplePromptBackground)
        {
            simplePromptBackground = GetComponent<Image>();

            if (simplePromptBackground == null)
            {
                simplePromptBackground = gameObject.AddComponent<Image>();
            }

            simplePromptBackground.color = simplePromptBackgroundColor;
            simplePromptBackground.raycastTarget = false;
            simplePromptBackground.type = Image.Type.Simple;
            simplePromptBackground.enabled = false;
        }

        UpdatePromptChildLayout(lastProgressVisible);
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

    private void HandleReinforcementPresentationChanged(ReinforcementPickup pickup)
    {
        HandlePickupPresentationChanged(pickup, pickup != null ? pickup.ReinforcementDefinition : null);
    }

    private void HandleTraitPresentationChanged(TraitPickup pickup)
    {
        HandlePickupPresentationChanged(pickup, pickup != null ? pickup.TraitDefinition : null);
    }

    private void HandlePickupPresentationChanged(Component pickup, Object definition)
    {
        if (pickup == null || !ReferenceEquals(currentTargetComponent, pickup))
        {
            return;
        }

        if ((pickup is Behaviour behaviour && !behaviour.isActiveAndEnabled) || definition == null)
        {
            ClearTarget();
            SetVisible(false);
            SetProgressVisible(false, 0f);
            return;
        }

        RefreshPromptText(currentTarget);
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
        if (target == null)
        {
            SetLootDetailVisible(false);
            SetSimplePromptBackgroundVisible(false);
            return;
        }

        if (target is ReinforcementPickup reinforcementPickup)
        {
            SetSimplePromptBackgroundVisible(false);
            EnsureDetailSourceSubscriptions();
            RefreshReinforcementDetail(reinforcementPickup);
            return;
        }

        if (target is TraitPickup traitPickup)
        {
            SetSimplePromptBackgroundVisible(false);
            EnsureDetailSourceSubscriptions();
            RefreshTraitDetail(traitPickup);
            return;
        }

        SetLootDetailVisible(false);
        SetSimplePromptBackgroundVisible(true);

        if (promptText == null)
        {
            return;
        }

        string interactionText = string.IsNullOrWhiteSpace(target.InteractionText)
            ? fallbackPrompt
            : target.InteractionText;
        promptText.text = $"[{ResolveInteractKeyText()}] {interactionText}";
    }

    private void RefreshReinforcementDetail(ReinforcementPickup pickup)
    {
        ReinforcementDefinition definition = pickup != null ? pickup.ReinforcementDefinition : null;

        if (definition == null)
        {
            SetLootDetailVisible(false);
            return;
        }

        SetLootDetailVisible(true);
        SetLootIcon(definition.Icon);
        SetText(lootNameText, LocalizeFieldLootText(definition.DisplayName));
        SetText(lootCategoryText, definition.GetUseTypeText());
        SetText(lootRarityText, definition.GetRarityText(), definition.GetRarityColor());
        int fieldCharges = ResolvePickupCharges(pickup, definition);
        SetText(
            lootDescriptionText,
            BuildLootDescription(
                definition.Description,
                BuildReinforcementSummary(definition, fieldCharges)
            )
        );

        ReinforcementDefinition currentDefinition = reinforcementController != null
            ? reinforcementController.EquippedDefinition
            : null;
        bool hasCurrentEquipment = currentDefinition != null;
        SetCurrentItemVisible(hasCurrentEquipment);

        if (hasCurrentEquipment)
        {
            SetImage(lootCurrentIconImage, currentDefinition.Icon);
            SetText(lootCurrentNameText, LocalizeFieldLootText(currentDefinition.DisplayName));
            SetText(
                lootCurrentDescriptionText,
                BuildLootDescription(
                    currentDefinition.Description,
                    BuildReinforcementSummary(currentDefinition, reinforcementController.CurrentCharges)
                )
            );
            SetText(lootOwnedStateText, "교체 · 장착 중인 장비는 필드에 남습니다");
        }
        else
        {
            SetText(lootOwnedStateText, "빈 슬롯 · 장비를 장착합니다");
        }

        SetText(lootPrimaryActionText, $"[{ResolveInteractKeyText()}] {(hasCurrentEquipment ? "교체" : "장착")}");
        SetOptionalText(
            lootDismantleActionText,
            pickup.CanDismantle,
            $"[{ResolveDismantleKeyText()}] 길게 눌러 분해"
        );

        if (promptText != null)
        {
            promptText.text = string.Empty;
        }

        RefreshLootDetailLayout();
    }

    private void RefreshTraitDetail(TraitPickup pickup)
    {
        TraitDefinition definition = pickup != null ? pickup.TraitDefinition : null;

        if (definition == null)
        {
            SetLootDetailVisible(false);
            return;
        }

        int currentLevel = definition.IsPersistentStoryTrait && PermanentProgress.Instance != null
            ? (PermanentProgress.Instance.HasPersistentStoryTrait(definition) ? 1 : 0)
            : (RunRuntimeTraitStore.Instance != null
                ? RunRuntimeTraitStore.Instance.GetLevel(definition.TraitId)
                : 0);
        int resultingLevel = Mathf.Min(currentLevel + 1, definition.MaxLevel);
        bool maxed = currentLevel >= definition.MaxLevel;

        SetLootDetailVisible(true);
        SetLootIcon(definition.Icon);
        SetCurrentItemVisible(false);
        SetText(lootNameText, LocalizeFieldLootText(definition.DisplayName));
        SetText(lootCategoryText, definition.GetCategoryText());
        SetText(
            lootRarityText,
            definition.Rarity == TraitRarity.Curse ? "저주" : definition.GetRarityText(),
            definition.GetRarityColor()
        );
        SetText(lootDescriptionText, LocalizeFieldLootText(definition.Description));
        SetText(lootOwnedStateText, BuildTraitOwnershipState(definition, currentLevel, resultingLevel, maxed));
        SetText(
            lootPrimaryActionText,
            maxed
                ? "최대 단계"
                : $"[{ResolveInteractKeyText()}] {(currentLevel > 0 ? "강화" : "획득")}"
        );
        SetOptionalText(
            lootDismantleActionText,
            pickup.CanDismantle,
            $"[{ResolveDismantleKeyText()}] 길게 눌러 분해"
        );

        if (promptText != null)
        {
            promptText.text = string.Empty;
        }

        RefreshLootDetailLayout();
    }

    private void ResolveInputActions()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
    }

    private void HandleInputActionChange(object changedObject, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged)
        {
            return;
        }

        ResolveInputActions();

        if (currentTarget != null)
        {
            RefreshPromptText(currentTarget);
        }
    }

    private string ResolveInteractKeyText()
    {
        ResolveInputActions();
        return InputBindingUtility.GetDisplayString(
            inputActions,
            playerActionMapName,
            interactActionName,
            string.IsNullOrWhiteSpace(prefix) ? "F" : prefix
        );
    }

    private string ResolveDismantleKeyText()
    {
        ResolveInputActions();
        return InputBindingUtility.GetDisplayString(
            inputActions,
            playerActionMapName,
            dismantleActionName,
            "G"
        );
    }

    private static string BuildLootDescription(string description, string effectSummary)
    {
        description = LocalizeFieldLootText(description);
        effectSummary = LocalizeFieldLootText(effectSummary);

        if (string.IsNullOrWhiteSpace(effectSummary))
        {
            return description;
        }

        if (string.IsNullOrWhiteSpace(description) || description.Contains(effectSummary))
        {
            return string.IsNullOrWhiteSpace(description) ? effectSummary : description;
        }

        return $"{description}\n{effectSummary}";
    }

    private static string BuildReinforcementSummary(ReinforcementDefinition definition, int charges)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        string effectSummary = LocalizeFieldLootText(definition.BuildEffectSummary());
        string chargeSummary = $"사용 횟수 {Mathf.Clamp(charges, 0, definition.MaxCharges)}/{definition.MaxCharges}";

        if (definition.UsesRecharge)
        {
            chargeSummary += $" · 재충전 {definition.RechargeSeconds:0.#}초";
        }

        return string.IsNullOrWhiteSpace(effectSummary)
            ? chargeSummary
            : $"{effectSummary}\n{chargeSummary}";
    }

    private static string LocalizeFieldLootText(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        return source
            .Replace("Pixel Curse", "픽셀 저주")
            .Replace("A persistent story corruption bound to the player.", "플레이어에게 결속된 영구적인 이야기 오염입니다.")
            .Replace("Reinforcement", "지원 장비")
            .Replace("Armor", "장갑")
            .Replace("R을 유지해", "지원 장비 입력을 유지해");
    }

    private static int ResolvePickupCharges(ReinforcementPickup pickup, ReinforcementDefinition definition)
    {
        if (pickup == null || definition == null)
        {
            return 0;
        }

        if (pickup.StoredCharges >= 0)
        {
            return Mathf.Clamp(pickup.StoredCharges, 0, definition.MaxCharges);
        }

        return definition.StartWithFullCharges ? definition.MaxCharges : 0;
    }

    private static string BuildTraitOwnershipState(
        TraitDefinition definition,
        int currentLevel,
        int resultingLevel,
        bool maxed)
    {
        string levelState = maxed
            ? $"최대 단계 · Lv.{currentLevel}/{definition.MaxLevel}"
            : currentLevel > 0
                ? $"Lv.{currentLevel} → Lv.{resultingLevel}/{definition.MaxLevel}"
                : $"미보유 → Lv.{resultingLevel}/{definition.MaxLevel}";

        if (definition.IsPersistentStoryTrait)
        {
            return $"{levelState}\n스토리 특성 · 보호됨";
        }

        if (!definition.CanFieldDrop && !definition.CanDismantle)
        {
            return $"{levelState}\n보호됨 · 드랍/분해 불가";
        }

        if (!definition.CanFieldDrop)
        {
            return $"{levelState}\n보호됨 · 필드 드랍 불가";
        }

        if (!definition.CanDismantle)
        {
            return $"{levelState}\n보호됨 · 분해 불가";
        }

        return levelState;
    }

    private void SetLootIcon(Sprite sprite)
    {
        SetImage(lootIconImage, sprite);
    }

    private void SetLootDetailVisible(bool visible)
    {
        if (visible)
        {
            SetSimplePromptBackgroundVisible(false);
        }

        if (lootDetailRoot != null && lootDetailRoot.activeSelf != visible)
        {
            lootDetailRoot.SetActive(visible);
        }

        if (!visible)
        {
            SetCurrentItemVisible(false);
        }
    }

    private void SetSimplePromptBackgroundVisible(bool visible)
    {
        if (simplePromptBackground != null)
        {
            simplePromptBackground.enabled = visible;
        }
    }

    private void RefreshLootDetailLayout()
    {
        if (lootDetailRoot == null || !lootDetailRoot.activeInHierarchy)
        {
            return;
        }

        CacheReferences();

        if (lootDetailRectTransform == null || canvasRectTransform == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(lootDetailRectTransform);
        ClampLootDetailToCanvas();
    }

    private void ClampLootDetailToCanvas()
    {
        lootDetailRectTransform.GetWorldCorners(lootDetailWorldCorners);

        Vector3 localBottomLeft = canvasRectTransform.InverseTransformPoint(lootDetailWorldCorners[0]);
        Vector3 localTopRight = canvasRectTransform.InverseTransformPoint(lootDetailWorldCorners[2]);
        Rect canvasRect = canvasRectTransform.rect;
        float margin = Mathf.Max(0f, lootDetailSafeMargin);
        float safeLeft = canvasRect.xMin + margin;
        float safeRight = canvasRect.xMax - margin;
        float safeBottom = canvasRect.yMin + margin;
        float safeTop = canvasRect.yMax - margin;
        float horizontalOffset = ResolveBoundsOffset(localBottomLeft.x, localTopRight.x, safeLeft, safeRight);
        float verticalOffset = ResolveBoundsOffset(localBottomLeft.y, localTopRight.y, safeBottom, safeTop);

        if (Mathf.Approximately(horizontalOffset, 0f) && Mathf.Approximately(verticalOffset, 0f))
        {
            return;
        }

        Vector3 canvasLocalOffset = new Vector3(horizontalOffset, verticalOffset, 0f);
        lootDetailRectTransform.position += canvasRectTransform.TransformVector(canvasLocalOffset);
    }

    private static float ResolveBoundsOffset(float minimum, float maximum, float safeMinimum, float safeMaximum)
    {
        float size = maximum - minimum;
        float safeSize = safeMaximum - safeMinimum;

        if (size > safeSize)
        {
            return ((safeMinimum + safeMaximum) * 0.5f) - ((minimum + maximum) * 0.5f);
        }

        if (minimum < safeMinimum)
        {
            return safeMinimum - minimum;
        }

        if (maximum > safeMaximum)
        {
            return safeMaximum - maximum;
        }

        return 0f;
    }

    private void SetCurrentItemVisible(bool visible)
    {
        if (lootCurrentItemRoot != null && lootCurrentItemRoot.activeSelf != visible)
        {
            lootCurrentItemRoot.SetActive(visible);
        }
    }

    private static void SetImage(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
    }

    private static void SetOptionalText(TextMeshProUGUI target, bool visible, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = visible ? value ?? string.Empty : string.Empty;

        if (target.gameObject.activeSelf != visible)
        {
            target.gameObject.SetActive(visible);
        }
    }

    private void EnsureDetailSourceSubscriptions()
    {
        if (reinforcementController == null)
        {
            reinforcementController = FindFirstObjectByType<PlayerReinforcementController>();
        }

        RunRuntimeTraitStore traitStore = RunRuntimeTraitStore.Instance;
        PermanentProgress permanentProgress = PermanentProgress.Instance;

        if (subscribedReinforcementController != reinforcementController)
        {
            if (subscribedReinforcementController != null)
            {
                subscribedReinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            }

            subscribedReinforcementController = reinforcementController;

            if (subscribedReinforcementController != null)
            {
                subscribedReinforcementController.EquipmentChanged += HandleReinforcementEquipmentChanged;
            }
        }

        if (subscribedTraitStore != traitStore)
        {
            if (subscribedTraitStore != null)
            {
                subscribedTraitStore.Changed -= HandleDetailStateChanged;
            }

            subscribedTraitStore = traitStore;

            if (subscribedTraitStore != null)
            {
                subscribedTraitStore.Changed += HandleDetailStateChanged;
            }
        }

        if (subscribedPermanentProgress != permanentProgress)
        {
            if (subscribedPermanentProgress != null)
            {
                subscribedPermanentProgress.Changed -= HandleDetailStateChanged;
            }

            subscribedPermanentProgress = permanentProgress;

            if (subscribedPermanentProgress != null)
            {
                subscribedPermanentProgress.Changed += HandleDetailStateChanged;
            }
        }
    }

    private void UnsubscribeDetailSources()
    {
        if (subscribedReinforcementController != null)
        {
            subscribedReinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            subscribedReinforcementController = null;
        }

        if (subscribedTraitStore != null)
        {
            subscribedTraitStore.Changed -= HandleDetailStateChanged;
            subscribedTraitStore = null;
        }

        if (subscribedPermanentProgress != null)
        {
            subscribedPermanentProgress.Changed -= HandleDetailStateChanged;
            subscribedPermanentProgress = null;
        }
    }

    private void HandleReinforcementEquipmentChanged(ReinforcementDefinition _, int __, int ___)
    {
        HandleDetailStateChanged();
    }

    private void HandleDetailStateChanged()
    {
        if (currentTarget is TraitPickup || currentTarget is ReinforcementPickup)
        {
            RefreshPromptText(currentTarget);
        }
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private static void SetText(TextMeshProUGUI target, string value, Color color)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
            target.color = color;
        }
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
        SetLootDetailVisible(false);
        SetSimplePromptBackgroundVisible(false);
        currentTarget = null;
        currentTargetComponent = null;
        currentTargetTransform = null;
        currentAnchor = null;
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            SetLootDetailVisible(false);
        }
        else if (lootDetailRoot != null &&
                 !lootDetailRoot.activeSelf &&
                 (currentTarget is ReinforcementPickup || currentTarget is TraitPickup))
        {
            RefreshPromptText(currentTarget);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (rootObject != null && rootObject.activeSelf != visible)
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
