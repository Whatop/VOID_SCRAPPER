using UnityEngine;

public class ExpeditionHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerCargoController cargoController;

    [Header("Systems")]
    [SerializeField] private RunLevelSystem runLevelSystem;
    [SerializeField] private ExpeditionObjectiveDirector objectiveDirector;

    [Header("Progress Gauge Mode")]
    [Tooltip("기본 ON. 기존 EXP 게이지를 코어 추적 신호 게이지로 사용합니다.")]
    [SerializeField] private bool useObjectiveSignalGauge = true;
    [SerializeField] private string objectiveLockedText = "CORE SIGNAL {0}/{1}";
    [SerializeField] private string objectiveReadyText = "CORE SIGNAL {0}/{1}  READY";

    [Header("HUD Roots")]
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private GameObject resourceRoot;
    [SerializeField] private GameObject[] additionalObjectsToHideDuringCinematic;

    [Header("Gauges")]
    [SerializeField] private GaugeBarUI hpGauge;
    [SerializeField] private GaugeBarUI armorGauge;
    [Tooltip("신규 구조에서는 코어 추적 신호 게이지로 사용합니다.")]
    [SerializeField] private GaugeBarUI expGauge;
    [SerializeField] private GaugeBarUI dashGauge;
    [SerializeField] private GaugeBarUI cargoGauge;

    [Header("Resource Counters")]
    [SerializeField] private ResourceCounterUI creditsCounter;
    [SerializeField] private ResourceCounterUI scrapCounter;
    [SerializeField] private ResourceCounterUI coreShardCounter;
    [SerializeField] private ResourceCounterUI tuningChipCounter;

    [Header("Reinforcement")]
    [Tooltip("좌측 하단 Reinforcement 전용 슬롯 UI")]
    [SerializeField] private ReinforcementSlotUI reinforcementSlotUI;

    [Header("Messages")]
    [SerializeField] private InteractionPromptUI interactionPromptUI;
    [SerializeField] private WarningMessageUI warningMessageUI;

    [Header("Dash Gauge")]
    [SerializeField] private string dashReadyText = "READY";
    [SerializeField] private string dashCooldownText = "{0:0.0}s";

    [Header("Fallback EXP - Legacy Only")]
    [SerializeField] private int fallbackExpToNextLevel = 100;

    private bool cinematicMode;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void Start()
    {
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (cinematicMode)
        {
            return;
        }

        UpdateDashGauge();
        UpdateReinforcementSlot();
        UpdateCargoGauge();
    }

    public void SetCinematicMode(bool enabled)
    {
        cinematicMode = enabled;
        ApplyCinematicVisibility();
    }

    public void SetObjectiveSignalGaugeEnabled(bool enabled)
    {
        useObjectiveSignalGauge = enabled;
        RefreshProgressGauge();
    }

    public void RefreshAll()
    {
        ResolveReferences();

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

        RefreshProgressGauge();

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            RefreshWallet(RunManager.Instance.CurrentRun.Wallet);
        }
        else
        {
            RefreshWallet(null);
        }

        UpdateDashGauge();
        UpdateReinforcementSlot();
        UpdateCargoGauge();
    }

    public void ShowWarning(string message)
    {
        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(message);
        }
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

        if (reinforcementController == null)
        {
            reinforcementController = FindFirstObjectByType<PlayerReinforcementController>();
        }

        if (reinforcementSlotUI == null)
        {
            reinforcementSlotUI = FindFirstObjectByType<ReinforcementSlotUI>();
        }

        if (cargoController == null)
        {
            cargoController = FindFirstObjectByType<PlayerCargoController>();
        }

        if (runLevelSystem == null)
        {
            runLevelSystem = FindFirstObjectByType<RunLevelSystem>();
        }

        if (objectiveDirector == null && Application.isPlaying)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
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
            playerDash.DashEnded += HandleDashEnded;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged += HandleReinforcementEquipmentChanged;
            reinforcementController.ChargesChanged += HandleReinforcementChargesChanged;
            reinforcementController.Used += HandleReinforcementUsed;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged += HandleCargoChanged;
        }

        if (!useObjectiveSignalGauge && runLevelSystem != null)
        {
            runLevelSystem.LevelStateChanged += HandleLevelStateChanged;
        }

        if (objectiveDirector != null)
        {
            objectiveDirector.ProgressChanged += HandleObjectiveProgressChanged;
            objectiveDirector.CoreRevealedEvent += HandleCoreRevealed;
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
            playerDash.DashEnded -= HandleDashEnded;
        }

        if (reinforcementController != null)
        {
            reinforcementController.EquipmentChanged -= HandleReinforcementEquipmentChanged;
            reinforcementController.ChargesChanged -= HandleReinforcementChargesChanged;
            reinforcementController.Used -= HandleReinforcementUsed;
        }

        if (cargoController != null)
        {
            cargoController.CargoChanged -= HandleCargoChanged;
        }

        if (runLevelSystem != null)
        {
            runLevelSystem.LevelStateChanged -= HandleLevelStateChanged;
        }

        if (objectiveDirector != null)
        {
            objectiveDirector.ProgressChanged -= HandleObjectiveProgressChanged;
            objectiveDirector.CoreRevealedEvent -= HandleCoreRevealed;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged -= HandleWalletChanged;
            RunManager.Instance.RunStarted -= HandleRunStarted;
        }
    }

    private void ApplyCinematicVisibility()
    {
        bool visible = !cinematicMode;

        SetGameObjectVisible(statusRoot, visible);
        SetGameObjectVisible(resourceRoot, visible);

        if (reinforcementSlotUI != null)
        {
            reinforcementSlotUI.SetVisible(visible);
        }

        if (statusRoot == null)
        {
            SetGaugeVisible(hpGauge, visible);
            SetGaugeVisible(armorGauge, visible);
            SetGaugeVisible(expGauge, visible);
            SetGaugeVisible(dashGauge, visible);
            SetGaugeVisible(cargoGauge, visible);
        }

        if (resourceRoot == null)
        {
            SetCounterVisible(creditsCounter, visible);
            SetCounterVisible(scrapCounter, visible);
            SetCounterVisible(coreShardCounter, visible);
            SetCounterVisible(tuningChipCounter, visible);
        }

        if (additionalObjectsToHideDuringCinematic != null)
        {
            for (int i = 0; i < additionalObjectsToHideDuringCinematic.Length; i++)
            {
                SetGameObjectVisible(additionalObjectsToHideDuringCinematic[i], visible);
            }
        }
    }

    private static void SetGameObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private static void SetGaugeVisible(GaugeBarUI gauge, bool visible)
    {
        if (gauge != null)
        {
            gauge.SetVisible(visible);
        }
    }

    private static void SetCounterVisible(ResourceCounterUI counter, bool visible)
    {
        if (counter != null)
        {
            counter.gameObject.SetActive(visible);
        }
    }

    private void HandleRunStarted(RunContext runContext)
    {
        RefreshAll();
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        RefreshWallet(wallet);

        if (!useObjectiveSignalGauge && runLevelSystem == null)
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
        if (!useObjectiveSignalGauge)
        {
            RefreshExp(level, expInLevel, requiredExp);
        }
    }

    private void HandleObjectiveProgressChanged(int current, int required)
    {
        RefreshObjectiveProgress(current, required);
    }

    private void HandleCoreRevealed()
    {
        RefreshProgressGauge();
    }

    private void HandleDashStarted(Vector2 direction)
    {
        UpdateDashGauge();
    }

    private void HandleDashEnded()
    {
        UpdateDashGauge();
    }

    private void HandleReinforcementEquipmentChanged(ReinforcementDefinition definition, int charges, int maxCharges)
    {
        UpdateReinforcementSlot();
    }

    private void HandleReinforcementChargesChanged(int charges, int maxCharges, float rechargeRatio)
    {
        UpdateReinforcementSlot();
    }

    private void HandleReinforcementUsed(ReinforcementDefinition definition)
    {
        UpdateReinforcementSlot();
    }

    private void HandleCargoChanged(int currentLoad, int maxCapacity)
    {
        UpdateCargoGauge();
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

    private void RefreshProgressGauge()
    {
        if (useObjectiveSignalGauge)
        {
            if (objectiveDirector == null && Application.isPlaying)
            {
                objectiveDirector = ExpeditionObjectiveDirector.Instance;
            }

            int current = objectiveDirector != null ? objectiveDirector.SignalCount : 0;
            int required = objectiveDirector != null ? objectiveDirector.SignalsRequiredToRevealCore : 2;
            RefreshObjectiveProgress(current, required);
            return;
        }

        if (runLevelSystem != null && runLevelSystem.LegacyExperienceLevelingEnabled)
        {
            RefreshExp(
                runLevelSystem.CurrentLevel,
                runLevelSystem.CurrentExpInLevel,
                runLevelSystem.CurrentRequiredExp
            );
        }
        else
        {
            RefreshExpFallback();
        }
    }

    private void RefreshObjectiveProgress(int current, int required)
    {
        if (expGauge == null)
        {
            return;
        }

        required = Mathf.Max(1, required);
        int clamped = Mathf.Clamp(current, 0, required);
        bool ready = current >= required;

        expGauge.SetValue(clamped, required);
        expGauge.SetText(string.Format(
            ready ? objectiveReadyText : objectiveLockedText,
            current,
            required
        ));
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
        int tuningChips = wallet != null ? wallet.TuningChips : 0;

        creditsCounter?.SetAmount(credits);
        scrapCounter?.SetAmount(scrap);
        coreShardCounter?.SetAmount(core);
        tuningChipCounter?.SetAmount(tuningChips);

        UpdateCargoGauge();
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
            dashGauge.SetRatio(1f);
            dashGauge.SetText(dashReadyText);
            return;
        }

        float cooldown = Mathf.Max(0.05f, playerDash.DashCooldown);
        float remaining = Mathf.Clamp(playerDash.RemainingCooldown, 0f, cooldown);
        float ratio = 1f - Mathf.Clamp01(remaining / cooldown);

        dashGauge.SetRatio(ratio);
        dashGauge.SetText(string.Format(dashCooldownText, remaining));
    }

    private void UpdateCargoGauge()
    {
        if (cargoGauge == null)
        {
            return;
        }

        if (cargoController != null)
        {
            cargoGauge.SetValue(cargoController.CurrentLoad, Mathf.Max(1, cargoController.MaxCapacity));
            cargoGauge.SetText($"Cargo {cargoController.CurrentLoad}/{cargoController.MaxCapacity}");
            return;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunContext runContext = RunManager.Instance.CurrentRun;
            cargoGauge.SetValue(runContext.CurrentCargoLoad, Mathf.Max(1, runContext.MaxCargoCapacity));
            cargoGauge.SetText($"Cargo {runContext.CurrentCargoLoad}/{runContext.MaxCargoCapacity}");
            return;
        }

        cargoGauge.SetValue(0f, 1f);
        cargoGauge.SetText("Cargo 0/0");
    }

    private void UpdateReinforcementSlot()
    {
        if (reinforcementSlotUI == null)
        {
            return;
        }

        if (reinforcementController == null)
        {
            reinforcementSlotUI.SetEmpty();
            return;
        }

        reinforcementSlotUI.RefreshFrom(reinforcementController);
    }
}
