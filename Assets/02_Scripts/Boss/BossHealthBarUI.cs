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

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
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
