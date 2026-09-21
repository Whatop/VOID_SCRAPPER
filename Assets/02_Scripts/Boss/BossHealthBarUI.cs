using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("가로로 펼쳐질 RectTransform. 비워두면 rootObject의 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform revealRoot;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Display")]
    [SerializeField] private string defaultBossName = "구획 관리자";
    [SerializeField] private string hpFormat = "{0:0} / {1:0}";
    [SerializeField] private bool hideWhenNoBoss = true;

    [Header("Encounter Layout Override")]
    [SerializeField] private Vector2 rightSideAnchor = new Vector2(1f, 0f);
    [SerializeField] private Vector2 rightSidePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 rightSideAnchoredPosition = new Vector2(-120f, 44f);

    [Header("Salvage Devourer Triad Presentation")]
    [SerializeField] private Vector2 triadRightSideAnchoredPosition = new Vector2(-42f, 66f);
    [SerializeField] private Vector2 triadPartBarSize = new Vector2(10f, 64f);
    [SerializeField] private float triadPartBarSpacing = 16f;
    [SerializeField] private Color triadBarBackgroundColor = new Color(0.025f, 0.04f, 0.055f, 0.94f);
    [SerializeField] private Color triadBarFillColor = new Color(1f, 0.27f, 0.06f, 1f);
    [SerializeField] private Color triadDestroyedColor = new Color(0.3f, 0.32f, 0.35f, 0.72f);

    [Header("Phase Shield")]
    [SerializeField] private string phaseShieldLabel = "SHIELD";
    [SerializeField] private Color phaseShieldFillColor = new Color(0.35f, 0.9f, 1f, 1f);

    [Header("Reveal Animation")]
    [SerializeField] private bool useAnimatedReveal = true;
    [Min(0.05f)]
    [SerializeField] private float frameRevealDuration = 0.34f;
    [Min(0.05f)]
    [SerializeField] private float hpFillDuration = 0.48f;
    [SerializeField] private AnimationCurve frameRevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve hpFillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Hide Fade")]
    [SerializeField] private bool useFadeOnHide = true;
    [Min(0.05f)]
    [SerializeField] private float hideFadeDuration = 0.2f;

    private EnemyHealth currentBossHealth;
    private Coroutine visibilityRoutine;
    private Vector3 revealBaseScale = Vector3.one;
    private float cachedCurrentHp;
    private float cachedMaxHp = 1f;
    private bool revealing;
    private bool phaseShieldOverride;
    private float phaseShieldCurrent;
    private float phaseShieldMax = 1f;
    private Color cachedNormalFillColor = Color.white;
    private string cachedBossName = "";
    private object layoutOverrideOwner;
    private RectTransform layoutRect;
    private Vector2 authoredAnchorMin;
    private Vector2 authoredAnchorMax;
    private Vector2 authoredPivot;
    private Vector2 authoredAnchoredPosition;
    private bool authoredLayoutCached;
    private object triadPresentationOwner;
    private FrigateTriadBossController triadController;
    [SerializeField] private RectTransform triadPresentationRoot;
    [SerializeField] private TextMeshProUGUI triadBossNameText;
    [SerializeField] private Image[] triadPartFills = new Image[3];
    private readonly RectTransform[] triadPartFillRects = new RectTransform[3];
    [SerializeField] private TextMeshProUGUI[] triadPartLabels = new TextMeshProUGUI[3];
    private readonly float[] triadFillHeights = new float[3];
    private bool missingTriadReported;
    private bool missingCanvasGroupReported;
    private readonly FrigateBossPart[] triadParts = new FrigateBossPart[3];
    private Transform[] aggregateVisualChildren;
    private bool[] aggregateVisualChildStates;

    public float AnimatedRevealDuration => useAnimatedReveal
        ? Mathf.Max(0.05f, frameRevealDuration) + Mathf.Max(0.05f, hpFillDuration)
        : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BossHealthBarUI가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
        CacheReferences();

        if (revealRoot != null)
        {
            revealBaseScale = revealRoot.localScale;
        }

        if (fillImage != null)
        {
            cachedNormalFillColor = fillImage.color;
        }

        if (hideWhenNoBoss)
        {
            SetVisibleImmediate(false);
        }
    }

    private void OnDestroy()
    {
        UnbindTriadParts();
        triadController = null;
        triadPresentationOwner = null;
        RestoreLayoutOverride();

        if (Instance == this)
        {
            Instance = null;
        }

        StopVisibilityRoutine();
        UnbindBoss();
    }

    public void ShowBoss(EnemyHealth bossHealth)
    {
        ShowBoss(bossHealth, defaultBossName);
    }

    public void ShowBoss(EnemyHealth bossHealth, string bossName)
    {
        if (bossHealth == null)
        {
            Hide();
            return;
        }

        BindBoss(bossHealth, bossName);
        StopVisibilityRoutine();
        revealing = false;
        SetVisibleImmediate(true);
        RestoreRevealScale();
        Refresh(cachedCurrentHp, cachedMaxHp);
    }

    public void ShowBossAnimated(EnemyHealth bossHealth)
    {
        ShowBossAnimated(bossHealth, defaultBossName);
    }

    public void ShowBossAnimated(EnemyHealth bossHealth, string bossName)
    {
        if (bossHealth == null)
        {
            Hide();
            return;
        }

        BindBoss(bossHealth, bossName);
        StopVisibilityRoutine();

        if (!useAnimatedReveal)
        {
            ShowBoss(bossHealth, bossName);
            return;
        }

        visibilityRoutine = StartCoroutine(RevealRoutine());
    }

    public void ShowPhaseShield(float currentShield, float maxShield)
    {
        phaseShieldOverride = true;
        phaseShieldCurrent = Mathf.Max(0f, currentShield);
        phaseShieldMax = Mathf.Max(1f, maxShield);

        if (fillImage != null)
        {
            fillImage.color = phaseShieldFillColor;
        }

        float ratio = Mathf.Clamp01(phaseShieldCurrent / phaseShieldMax);
        SetDisplayedRatio(ratio, phaseShieldCurrent, phaseShieldMax);

        if (hpText != null)
        {
            hpText.text = $"{phaseShieldLabel} {phaseShieldCurrent:0} / {phaseShieldMax:0}";
        }
    }

    public void ClearPhaseShield()
    {
        if (!phaseShieldOverride)
        {
            return;
        }

        phaseShieldOverride = false;

        if (fillImage != null)
        {
            fillImage.color = cachedNormalFillColor;
        }

        if (currentBossHealth != null)
        {
            cachedCurrentHp = currentBossHealth.CurrentHp;
            cachedMaxHp = Mathf.Max(1f, currentBossHealth.MaxHp);
            Refresh(cachedCurrentHp, cachedMaxHp);
        }
    }

    public void Hide()
    {
        StopVisibilityRoutine();
        UnbindBoss();

        if (!useFadeOnHide || !gameObject.activeInHierarchy)
        {
            SetVisibleImmediate(false);
            return;
        }

        visibilityRoutine = StartCoroutine(HideRoutine());
    }

    public bool SetRightSideLayout(object owner, bool enabled)
    {
        if (owner == null)
        {
            return false;
        }

        if (!enabled)
        {
            if (!ReferenceEquals(layoutOverrideOwner, owner))
            {
                return false;
            }

            RestoreLayoutOverride();
            return true;
        }

        if (layoutOverrideOwner != null && !ReferenceEquals(layoutOverrideOwner, owner))
        {
            return false;
        }

        CacheReferences();
        layoutRect = rootObject != null
            ? rootObject.transform as RectTransform
            : transform as RectTransform;
        if (layoutRect == null)
        {
            return false;
        }

        if (!authoredLayoutCached)
        {
            authoredAnchorMin = layoutRect.anchorMin;
            authoredAnchorMax = layoutRect.anchorMax;
            authoredPivot = layoutRect.pivot;
            authoredAnchoredPosition = layoutRect.anchoredPosition;
            authoredLayoutCached = true;
        }

        layoutOverrideOwner = owner;
        layoutRect.anchorMin = rightSideAnchor;
        layoutRect.anchorMax = rightSideAnchor;
        layoutRect.pivot = rightSidePivot;
        layoutRect.anchoredPosition = rightSideAnchoredPosition;
        return true;
    }

    public bool SetSalvageDevourerTriadPresentation(
        object owner,
        FrigateTriadBossController controller,
        bool enabled)
    {
        if (owner == null)
        {
            return false;
        }

        if (!enabled)
        {
            if (!ReferenceEquals(triadPresentationOwner, owner))
            {
                return false;
            }

            ReleaseTriadPresentation();
            SetRightSideLayout(owner, false);
            return true;
        }

        if (controller == null ||
            (triadPresentationOwner != null &&
             !ReferenceEquals(triadPresentationOwner, owner)))
        {
            return false;
        }

        if (!TryPrepareTriadPresentation() || !SetRightSideLayout(owner, true))
        {
            return false;
        }

        if (layoutRect != null)
        {
            layoutRect.anchoredPosition = triadRightSideAnchoredPosition;
        }

        triadPresentationOwner = owner;
        triadController = controller;
        CacheAndHideAggregateVisualChildren();
        triadPresentationRoot.gameObject.SetActive(true);
        BindTriadParts();
        RefreshAllTriadParts();
        return triadPresentationRoot != null;
    }

    private void RestoreLayoutOverride()
    {
        if (authoredLayoutCached && layoutRect != null)
        {
            layoutRect.anchorMin = authoredAnchorMin;
            layoutRect.anchorMax = authoredAnchorMax;
            layoutRect.pivot = authoredPivot;
            layoutRect.anchoredPosition = authoredAnchoredPosition;
        }

        layoutOverrideOwner = null;
        layoutRect = null;
        authoredLayoutCached = false;
    }

    private void CacheAndHideAggregateVisualChildren()
    {
        Transform rootTransform = rootObject != null ? rootObject.transform : transform;
        if (aggregateVisualChildren == null)
        {
            int childCount = rootTransform.childCount;
            aggregateVisualChildren = new Transform[childCount];
            aggregateVisualChildStates = new bool[childCount];
            for (int i = 0; i < childCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
                aggregateVisualChildren[i] = child;
                aggregateVisualChildStates[i] = child != null && child.gameObject.activeSelf;
            }
        }

        for (int i = 0; i < aggregateVisualChildren.Length; i++)
        {
            Transform child = aggregateVisualChildren[i];
            if (child != null && !ReferenceEquals(child, triadPresentationRoot))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private bool TryPrepareTriadPresentation()
    {
        Transform parent = rootObject != null ? rootObject.transform : transform;
        bool valid = triadPresentationRoot != null && triadPresentationRoot.IsChildOf(parent) &&
            triadBossNameText != null && triadBossNameText.transform.IsChildOf(triadPresentationRoot) &&
            triadPartFills != null && triadPartFills.Length == 3 && triadPartLabels != null && triadPartLabels.Length == 3;
        for (int i = 0; valid && i < 3; i++)
        {
            valid = triadPartFills[i] != null && triadPartLabels[i] != null &&
                triadPartFills[i].transform.IsChildOf(triadPresentationRoot) && triadPartLabels[i].transform.IsChildOf(triadPresentationRoot);
            for (int j = 0; valid && j < i; j++) valid = triadPartFills[i] != triadPartFills[j] && triadPartLabels[i] != triadPartLabels[j];
        }
        if (!valid)
        {
            if (!missingTriadReported)
            {
                missingTriadReported = true;
                Debug.LogWarning($"[BossHealthBarUI] Missing/invalid triadPresentationRoot/triadBossNameText/triadPartFills/triadPartLabels at '{HierarchyPath(transform)}', scene '{gameObject.scene.path}'. Restore the listed authored Inspector bindings. Only triad presentation was skipped.", this);
            }
            return false;
        }
        for (int i = 0; i < 3; i++)
        {
            if (triadPartFillRects[i] == triadPartFills[i].rectTransform) continue;
            triadPartFillRects[i] = triadPartFills[i].rectTransform;
            triadFillHeights[i] = triadPartFillRects[i].sizeDelta.y;
        }
        return true;
    }

    private static string HierarchyPath(Transform target) => target.parent != null ? HierarchyPath(target.parent) + "/" + target.name : target.name;

    private void BindTriadParts()
    {
        UnbindTriadParts();
        if (triadController == null)
        {
            return;
        }

        for (int i = 0; i < triadPartFills.Length; i++)
        {
            FrigateBossPart part = triadController.GetPart((FrigateBossPartId)i);
            if (part == null)
            {
                continue;
            }

            triadParts[i] = part;
            part.HealthChanged += HandleTriadPartHealthChanged;
            part.StateChanged += HandleTriadPartStateChanged;
        }
    }

    private void UnbindTriadParts()
    {
        for (int i = 0; i < triadPartFills.Length; i++)
        {
            FrigateBossPart part = triadParts[i];
            if (part == null)
            {
                continue;
            }

            part.HealthChanged -= HandleTriadPartHealthChanged;
            part.StateChanged -= HandleTriadPartStateChanged;
            triadParts[i] = null;
        }
    }

    private void HandleTriadPartHealthChanged(
        FrigateBossPart part,
        float currentHealth,
        float maxHealth)
    {
        RefreshTriadPart(part, currentHealth, maxHealth);
    }

    private void HandleTriadPartStateChanged(
        FrigateBossPart part,
        FrigateBossPartState _)
    {
        RefreshTriadPart(part, part.CurrentHealth, part.MaxHealth);
    }

    private void RefreshAllTriadParts()
    {
        if (triadController == null)
        {
            return;
        }

        if (triadBossNameText != null)
        {
            triadBossNameText.text = triadController.BossDisplayName;
        }

        for (int i = 0; i < triadPartFills.Length; i++)
        {
            FrigateBossPart part = triadController.GetPart((FrigateBossPartId)i);
            if (part != null)
            {
                RefreshTriadPart(part, part.CurrentHealth, part.MaxHealth);
            }
        }
    }

    private void RefreshTriadPart(
        FrigateBossPart part,
        float currentHealth,
        float maxHealth)
    {
        if (part == null)
        {
            return;
        }

        int index = (int)part.PartId;
        if (index < 0 || index >= triadPartFills.Length)
        {
            return;
        }

        float ratio = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
        Image fill = triadPartFills[index];
        RectTransform fillRect = triadPartFillRects[index];
        bool activePart = part.State == FrigateBossPartState.Alive ||
                          part.State == FrigateBossPartState.FinalSequencePending;
        if (fill != null)
        {
            fill.fillAmount = ratio;
            fill.color = activePart ? triadBarFillColor : triadDestroyedColor;
        }

        if (fillRect != null)
        {
            Vector2 size = fillRect.sizeDelta;
            size.y = triadFillHeights[index] * ratio;
            fillRect.sizeDelta = size;
        }

        if (triadPartLabels[index] != null)
        {
            triadPartLabels[index].color = activePart ? Color.white : triadDestroyedColor;
        }
    }

    private void ReleaseTriadPresentation()
    {
        UnbindTriadParts();

        if (aggregateVisualChildren != null)
        {
            for (int i = 0; i < aggregateVisualChildren.Length; i++)
            {
                Transform child = aggregateVisualChildren[i];
                if (child != null)
                {
                    child.gameObject.SetActive(aggregateVisualChildStates[i]);
                }
            }
        }

        if (triadPresentationRoot != null)
        {
            triadPresentationRoot.gameObject.SetActive(false);
        }

        for (int i = 0; i < triadParts.Length; i++)
        {
            triadParts[i] = null;
        }

        aggregateVisualChildren = null;
        aggregateVisualChildStates = null;
        triadController = null;
        triadPresentationOwner = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Refresh Salvage Devourer Triad HP")]
    private void DevelopmentRefreshSalvageDevourerTriadHp()
    {
        RefreshAllTriadParts();
    }
#endif

    private void BindBoss(EnemyHealth bossHealth, string bossName)
    {
        UnbindBoss();

        currentBossHealth = bossHealth;
        currentBossHealth.HealthChanged += HandleBossHealthChanged;
        currentBossHealth.Died += HandleBossDied;

        phaseShieldOverride = false;

        if (fillImage != null)
        {
            cachedNormalFillColor = fillImage.color;
        }

        cachedBossName = string.IsNullOrWhiteSpace(bossName)
            ? defaultBossName
            : bossName;

        cachedCurrentHp = currentBossHealth.CurrentHp;
        cachedMaxHp = Mathf.Max(1f, currentBossHealth.MaxHp);

        if (bossNameText != null)
        {
            bossNameText.text = cachedBossName;
        }

        if (hpText != null)
        {
            hpText.text = string.Format(hpFormat, cachedCurrentHp, cachedMaxHp);
        }
    }

    private IEnumerator RevealRoutine()
    {
        revealing = true;

        if (CanToggleRootObjectActive())
        {
            rootObject.SetActive(true);
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (revealRoot != null)
        {
            Vector3 startScale = revealBaseScale;
            startScale.x = 0f;
            revealRoot.localScale = startScale;
        }

        SetDisplayedRatio(0f, 0f, cachedMaxHp);

        float frameDuration = Mathf.Max(0.05f, frameRevealDuration);
        float elapsed = 0f;

        while (elapsed < frameDuration)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / frameDuration);
            float eased = Evaluate(frameRevealCurve, normalized);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = eased;
            }

            if (revealRoot != null)
            {
                Vector3 scale = revealBaseScale;
                scale.x *= eased;
                revealRoot.localScale = scale;
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        RestoreRevealScale();

        float fillDuration = Mathf.Max(0.05f, hpFillDuration);
        elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / fillDuration);
            float eased = Evaluate(hpFillCurve, normalized);

            float displayedHp = cachedCurrentHp * eased;
            SetDisplayedRatio(eased * Mathf.Clamp01(cachedCurrentHp / cachedMaxHp), displayedHp, cachedMaxHp);
            yield return null;
        }

        revealing = false;
        Refresh(cachedCurrentHp, cachedMaxHp);
        visibilityRoutine = null;
    }

    private IEnumerator HideRoutine()
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, hideFadeDuration);

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, normalized);
            }

            yield return null;
        }

        SetVisibleImmediate(false);
        visibilityRoutine = null;
    }

    private void HandleBossHealthChanged(EnemyHealth health, float currentHp, float maxHp)
    {
        cachedCurrentHp = Mathf.Max(0f, currentHp);
        cachedMaxHp = Mathf.Max(1f, maxHp);

        if (!revealing && !phaseShieldOverride)
        {
            Refresh(cachedCurrentHp, cachedMaxHp);
        }
    }

    private void HandleBossDied(EnemyHealth health)
    {
        Hide();
    }

    private void Refresh(float currentHp, float maxHp)
    {
        float safeMax = Mathf.Max(1f, maxHp);
        float ratio = Mathf.Clamp01(currentHp / safeMax);
        SetDisplayedRatio(ratio, currentHp, safeMax);
    }

    private void SetDisplayedRatio(float ratio, float displayedHp, float maxHp)
    {
        ratio = Mathf.Clamp01(ratio);

        if (hpSlider != null)
        {
            hpSlider.normalizedValue = ratio;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
        }

        if (hpText != null)
        {
            hpText.text = string.Format(hpFormat, Mathf.Max(0f, displayedHp), Mathf.Max(1f, maxHp));
        }
    }

    private void UnbindBoss()
    {
        phaseShieldOverride = false;

        if (fillImage != null)
        {
            fillImage.color = cachedNormalFillColor;
        }

        if (currentBossHealth == null)
        {
            return;
        }

        currentBossHealth.HealthChanged -= HandleBossHealthChanged;
        currentBossHealth.Died -= HandleBossDied;
        currentBossHealth = null;
    }

    private void SetVisibleImmediate(bool value)
    {
        revealing = false;

        // BossHealthBarUI가 붙은 자기 GameObject를 끄면 다음 보스 때 코루틴을 시작할 수 없다.
        // rootObject가 자기 자신이면 활성 상태는 유지하고 CanvasGroup으로만 숨긴다.
        if (CanToggleRootObjectActive())
        {
            rootObject.SetActive(value || !hideWhenNoBoss);
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = value ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (value)
        {
            RestoreRevealScale();
        }
    }

    private bool CanToggleRootObjectActive()
    {
        if (rootObject == null || rootObject == gameObject)
        {
            return false;
        }

        // rootObject가 이 컴포넌트의 부모라면 비활성화 시 다음 표시 코루틴을 시작할 수 없다.
        return !transform.IsChildOf(rootObject.transform);
    }

    private void CacheReferences()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && !missingCanvasGroupReported)
        {
            missingCanvasGroupReported = true;
            Debug.LogWarning($"[BossHealthBarUI] Missing canvasGroup at '{HierarchyPath(transform)}', scene '{gameObject.scene.path}'. Restore the listed authored Inspector bindings. No replacement presentation was created.", this);
        }

        if (revealRoot == null && rootObject != null)
        {
            revealRoot = rootObject.transform as RectTransform;
        }

        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>(true);
        }

        if (fillImage == null && hpSlider != null && hpSlider.fillRect != null)
        {
            fillImage = hpSlider.fillRect.GetComponent<Image>();
        }

        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name.ToLowerInvariant().Contains("fill"))
                {
                    fillImage = images[i];
                    break;
                }
            }
        }
    }

    private void RestoreRevealScale()
    {
        if (revealRoot != null)
        {
            revealRoot.localScale = revealBaseScale;
        }
    }

    private void StopVisibilityRoutine()
    {
        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
            visibilityRoutine = null;
        }

        revealing = false;
    }

    private float Evaluate(AnimationCurve curve, float normalized)
    {
        return curve != null && curve.length > 0
            ? Mathf.Clamp01(curve.Evaluate(normalized))
            : Mathf.SmoothStep(0f, 1f, normalized);
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
