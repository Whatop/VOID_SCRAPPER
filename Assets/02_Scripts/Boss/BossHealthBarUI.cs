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
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Display")]
    [SerializeField] private string defaultBossName = "구획 관리자";
    [SerializeField] private string hpFormat = "{0:0} / {1:0}";
    [SerializeField] private bool hideWhenNoBoss = true;

    [Header("Fade")]
    [SerializeField] private bool useFade = true;
    [SerializeField] private float fadeSpeed = 10f;

    private EnemyHealth currentBossHealth;
    private float targetAlpha;
    private bool visible;

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

        UnbindBoss();
    }

    private void Update()
    {
        if (!useFade || canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Lerp(
            canvasGroup.alpha,
            targetAlpha,
            1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime)
        );

        if (!visible && canvasGroup.alpha <= 0.01f && rootObject != null)
        {
            rootObject.SetActive(false);
        }
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

        UnbindBoss();

        currentBossHealth = bossHealth;
        currentBossHealth.HealthChanged += HandleBossHealthChanged;
        currentBossHealth.Died += HandleBossDied;

        if (bossNameText != null)
        {
            bossNameText.text = string.IsNullOrWhiteSpace(bossName)
                ? defaultBossName
                : bossName;
        }

        Refresh(bossHealth.CurrentHp, bossHealth.MaxHp);
        SetVisible(true);
    }

    public void Hide()
    {
        UnbindBoss();
        SetVisible(false);
    }

    private void HandleBossHealthChanged(EnemyHealth health, float currentHp, float maxHp)
    {
        Refresh(currentHp, maxHp);
    }

    private void HandleBossDied(EnemyHealth health)
    {
        Hide();
    }

    private void Refresh(float currentHp, float maxHp)
    {
        float safeMax = Mathf.Max(1f, maxHp);
        float ratio = Mathf.Clamp01(currentHp / safeMax);

        if (hpSlider != null)
        {
            hpSlider.value = ratio;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
        }

        if (hpText != null)
        {
            hpText.text = string.Format(hpFormat, currentHp, safeMax);
        }
    }

    private void UnbindBoss()
    {
        if (currentBossHealth == null)
        {
            return;
        }

        currentBossHealth.HealthChanged -= HandleBossHealthChanged;
        currentBossHealth.Died -= HandleBossDied;
        currentBossHealth = null;
    }

    private void SetVisible(bool value)
    {
        visible = value;
        targetAlpha = value ? 1f : 0f;

        if (rootObject != null && value)
        {
            rootObject.SetActive(true);
        }

        if (!useFade)
        {
            SetVisibleImmediate(value);
        }
    }

    private void SetVisibleImmediate(bool value)
    {
        visible = value;
        targetAlpha = value ? 1f : 0f;

        if (rootObject != null)
        {
            rootObject.SetActive(value || !hideWhenNoBoss);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = value ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
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
                if (images[i] != null && images[i].name.ToLower().Contains("fill"))
                {
                    fillImage = images[i];
                    break;
                }
            }
        }
    }
}