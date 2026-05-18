using UnityEngine;

public class ExpeditionHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerInteractor playerInteractor;

    [Header("Systems")]
    [SerializeField] private RunLevelSystem runLevelSystem;

    [Header("Gauges")]
    [SerializeField] private GaugeBarUI hpGauge;
    [SerializeField] private GaugeBarUI armorGauge;
    [SerializeField] private GaugeBarUI expGauge;
    [SerializeField] private GaugeBarUI dashGauge;

    [Header("Resource Counters")]
    [SerializeField] private ResourceCounterUI creditsCounter;
    [SerializeField] private ResourceCounterUI scrapCounter;
    [SerializeField] private ResourceCounterUI coreShardCounter;

    [Header("Messages")]
    [SerializeField] private InteractionPromptUI interactionPromptUI;
    [SerializeField] private WarningMessageUI warningMessageUI;

    [Header("Dash Gauge")]
    [Tooltip("PlayerDash가 쿨타임 수치를 공개하지 않으므로 UI 표시용으로 같은 값을 넣는다.")]
    [SerializeField] private float dashCooldownDuration = 0.7f;
    [SerializeField] private string dashReadyText = "READY";
    [SerializeField] private string dashCooldownText = "{0:0.0}s";

    [Header("Fallback EXP")]
    [SerializeField] private int fallbackExpToNextLevel = 100;

    private float dashCooldownTimer;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
    private void Start()
    {
        RefreshAll();
    }
    private void Update()
    {
        UpdateDashGauge();
    }

    private void ResolveReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerArmor == null)
        {
            playerArmor = FindFirstObjectByType<PlayerArmor>();
        }

        if (playerDash == null)
        {
            playerDash = FindFirstObjectByType<PlayerDash>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        if (runLevelSystem == null)
        {
            runLevelSystem = FindFirstObjectByType<RunLevelSystem>();
        }
    }

    private void Subscribe()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged += HandleHealthChanged;
            playerHealth.Healed += HandleHealthChanged;
        }

        if (playerArmor != null)
        {
            playerArmor.Changed += HandleArmorChanged;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted += HandleDashStarted;
        }

        if (runLevelSystem != null)
        {
            runLevelSystem.LevelStateChanged += HandleLevelStateChanged;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged += HandleWalletChanged;
            RunManager.Instance.RunStarted += HandleRunStarted;
        }
    }

    private void Unsubscribe()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandleHealthChanged;
            playerHealth.Healed -= HandleHealthChanged;
        }

        if (playerArmor != null)
        {
            playerArmor.Changed -= HandleArmorChanged;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
        }

        if (runLevelSystem != null)
        {
            runLevelSystem.LevelStateChanged -= HandleLevelStateChanged;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged -= HandleWalletChanged;
            RunManager.Instance.RunStarted -= HandleRunStarted;
        }
    }

    public void RefreshAll()
    {
        if (playerHealth != null)
        {
            RefreshHealth(playerHealth.CurrentHp, playerHealth.MaxHp);
        }

        if (playerArmor != null)
        {
            RefreshArmor(playerArmor.CurrentArmor, playerArmor.MaxArmor);
        }
        else if (armorGauge != null)
        {
            armorGauge.SetValue(0f, 1f);
        }

        if (runLevelSystem != null)
        {
            RefreshExp(runLevelSystem.CurrentLevel, runLevelSystem.CurrentExpInLevel, runLevelSystem.CurrentRequiredExp);
        }
        else
        {
            RefreshExpFallback();
        }

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            RefreshWallet(RunManager.Instance.CurrentRun.Wallet);
        }
        else
        {
            RefreshWallet(null);
        }
    }

    public void ShowWarning(string message)
    {
        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(message);
        }
    }

    private void HandleRunStarted(RunContext runContext)
    {
        RefreshAll();
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        RefreshWallet(wallet);

        if (runLevelSystem == null)
        {
            RefreshExpFallback();
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        RefreshHealth(current, max);
    }

    private void HandleArmorChanged(float current, float max)
    {
        RefreshArmor(current, max);
    }

    private void HandleLevelStateChanged(int level, int expInLevel, int requiredExp)
    {
        RefreshExp(level, expInLevel, requiredExp);
    }

    private void HandleDashStarted(Vector2 direction)
    {
        dashCooldownTimer = Mathf.Max(0.05f, dashCooldownDuration);
    }

    private void RefreshHealth(float current, float max)
    {
        if (hpGauge != null)
        {
            hpGauge.SetValue(current, max);
        }
    }

    private void RefreshArmor(float current, float max)
    {
        if (armorGauge != null)
        {
            armorGauge.SetValue(current, Mathf.Max(1f, max));
        }
    }

    private void RefreshExp(int level, int expInLevel, int requiredExp)
    {
        if (expGauge == null)
        {
            return;
        }

        expGauge.SetValue(expInLevel, Mathf.Max(1, requiredExp));
        expGauge.SetText($"LV {level}  {expInLevel}/{requiredExp}");
    }

    private void RefreshExpFallback()
    {
        RunWallet wallet = RunManager.Instance != null && RunManager.Instance.CurrentRun != null
            ? RunManager.Instance.CurrentRun.Wallet
            : null;

        int exp = wallet != null ? wallet.Experience : 0;
        int required = Mathf.Max(1, fallbackExpToNextLevel);

        if (expGauge != null)
        {
            expGauge.SetValue(exp % required, required);
            expGauge.SetText($"EXP {exp % required}/{required}");
        }
    }

    private void RefreshWallet(RunWallet wallet)
    {
        int credits = wallet != null ? wallet.Credits : 0;
        int scrap = wallet != null ? wallet.PendingScrapParts : 0;
        int core = wallet != null ? wallet.PendingCoreShards : 0;

        if (creditsCounter != null)
        {
            creditsCounter.SetAmount(credits);
        }

        if (scrapCounter != null)
        {
            scrapCounter.SetAmount(scrap);
        }

        if (coreShardCounter != null)
        {
            coreShardCounter.SetAmount(core);
        }
    }

    private void UpdateDashGauge()
    {
        if (dashGauge == null)
        {
            return;
        }

        if (playerDash == null)
        {
            dashGauge.SetRatio(1f);
            dashGauge.SetText(dashReadyText);
            return;
        }

        if (playerDash.CanDash)
        {
            dashCooldownTimer = 0f;
            dashGauge.SetRatio(1f);
            dashGauge.SetText(dashReadyText);
            return;
        }

        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);

        float cooldown = Mathf.Max(0.05f, dashCooldownDuration);
        float ratio = 1f - Mathf.Clamp01(dashCooldownTimer / cooldown);

        dashGauge.SetRatio(ratio);
        dashGauge.SetText(string.Format(dashCooldownText, dashCooldownTimer));
    }
}
