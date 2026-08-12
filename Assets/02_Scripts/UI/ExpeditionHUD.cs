using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ExpeditionHUD : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerCargoController cargoController;

    [Header("Objective")]
    [SerializeField] private ExpeditionObjectiveDirector objectiveDirector;
    [SerializeField] private string coreSignalCountFormat = "{0}/{1}";
    [SerializeField] private string coreSignalReadyText = "CORE FOUND";

    [Header("Input Binding Hints")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string mapActionName = "Map";
    [SerializeField] private string inventoryActionName = "Inventory";
    [SerializeField] private string reinforcementActionName = "UseReinforcement";
    [SerializeField] private GameObject menuHintRoot;
    [SerializeField] private TextMeshProUGUI mapHintText;
    [SerializeField] private TextMeshProUGUI inventoryHintText;
    [SerializeField] private TextMeshProUGUI reinforcementKeyText;
    [SerializeField] private string mapHintFormat = "[{0}] 지도";
    [SerializeField] private string inventoryHintFormat = "[{0}] 인벤토리";
    [SerializeField] private string reinforcementKeyFormat = "[{0}]";

    [Header("HUD Roots")]
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private GameObject resourceRoot;
    [SerializeField] private GameObject cargoRoot;
    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private GameObject[] additionalObjectsToHideDuringCinematic;

    [Header("HP + Armor")]
    [SerializeField] private GaugeBarUI hpGauge;
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private GameObject armorBonusRoot;
    [SerializeField] private TextMeshProUGUI armorBonusText;
    [Tooltip("HP Fill의 오른쪽 끝에서 이어지는 흰색 Armor 세그먼트 RectTransform입니다. 부모에 Mask/RectMask2D를 두지 마세요.")]
    [SerializeField] private RectTransform armorFillRect;
    [SerializeField] private Image armorFillImage;
    [SerializeField] private string hpValueFormat = "{0:0}/{1:0}";
    [SerializeField] private string armorBonusFormat = "(+{0:0})";
    [SerializeField] private Color armorBonusColor = Color.white;
    [SerializeField] private Color armorFillColor = Color.white;
    [SerializeField] private bool autoPositionArmorBonusBesideHpText = true;
    [Min(0f)]
    [SerializeField] private float armorBonusTextSpacing = 2f;
    [FormerlySerializedAs("armorGauge")]
    [SerializeField, HideInInspector] private GaugeBarUI legacyArmorGauge;

    [Header("Core Signal Icon")]
    [FormerlySerializedAs("objectiveSignalGauge")]
    [SerializeField] private GaugeBarUI legacyObjectiveSignalGauge;
    [SerializeField] private Image coreSignalIcon;
    [SerializeField] private Image[] coreSignalPips;
    [SerializeField] private TextMeshProUGUI coreSignalCountText;
    [SerializeField] private GameObject coreSignalReadyPulseRoot;
    [SerializeField] private Color coreSignalInactiveColor = new Color(0.22f, 0.25f, 0.3f, 0.75f);
    [SerializeField] private Color coreSignalActiveColor = new Color(1f, 0.76f, 0.16f, 1f);
    [SerializeField] private Color coreSignalReadyColor = new Color(1f, 0.95f, 0.45f, 1f);

    [Header("Dash Icon")]
    [SerializeField] private GaugeBarUI dashGauge;
    [SerializeField] private Image dashCooldownFill;
    [SerializeField] private Image dashIcon;
    [SerializeField] private GameObject dashReadyGlowRoot;
    [SerializeField] private TextMeshProUGUI dashCooldownText;
    [SerializeField] private Color dashReadyColor = Color.white;
    [SerializeField] private Color dashCooldownColor = new Color(0.45f, 0.48f, 0.55f, 1f);

    [Header("Cargo Bottom Bar")]
    [SerializeField] private GaugeBarUI cargoGauge;
    [SerializeField] private TextMeshProUGUI cargoValueText;
    [SerializeField] private string cargoValueFormat = "{0}/{1}";
    [SerializeField] private Color cargoNormalColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color cargoWarningColor = new Color(1f, 0.75f, 0.18f, 1f);
    [SerializeField] private Color cargoFullColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float cargoWarningRatio = 0.8f;

    [Header("Resource Counters - Vertical")]
    [SerializeField] private ResourceCounterUI creditsCounter;
    [SerializeField] private ResourceCounterUI scrapCounter;
    [SerializeField] private ResourceCounterUI coreShardCounter;
    [SerializeField] private ResourceCounterUI tuningChipCounter;
    [SerializeField] private bool hideZeroResources = true;

    [Header("Reinforcement / Heat")]
    [SerializeField] private ReinforcementSlotUI reinforcementSlotUI;
    [SerializeField] private WeaponHeatUI weaponHeatUI;

    [Header("Messages")]
    [SerializeField] private WarningMessageUI warningMessageUI;

    private bool cinematicMode;
    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
        ResolveInputActions();
        InputBindingPersistence.LoadOnce(inputActions);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        InputSystem.onActionChange += HandleInputActionChange;
        GameSettingsRuntime.Changed += HandleGameSettingsChanged;
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void Start()
    {
        Unsubscribe();
        ResolveReferences();
        Subscribe();
        RefreshAll();
        ApplyCinematicVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
        InputSystem.onActionChange -= HandleInputActionChange;
        GameSettingsRuntime.Changed -= HandleGameSettingsChanged;
    }

    private void Update()
    {
        if (cinematicMode)
        {
            return;
        }

        UpdateDashDisplay();
        UpdateReinforcementSlot();
        UpdateCargoDisplay();
    }

    public void SetCinematicMode(bool enabled)
    {
        cinematicMode = enabled;
        ApplyCinematicVisibility();
    }

    public void RefreshAll()
    {
        ResolveReferences();

        float hp = playerHealth != null ? playerHealth.CurrentHp : 0f;
        float maxHp = playerHealth != null ? playerHealth.MaxHp : 1f;
        float armor = playerArmor != null ? playerArmor.CurrentArmor : 0f;
        RefreshHealthAndArmor(hp, maxHp, armor);

        RefreshObjectiveProgress();
        RefreshWallet(RunManager.Instance != null && RunManager.Instance.CurrentRun != null
            ? RunManager.Instance.CurrentRun.Wallet
            : null);

        RefreshBindingHints();
        UpdateDashDisplay();
        UpdateReinforcementSlot();
        UpdateCargoDisplay();
    }

    public void ShowWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            warningMessageUI?.ShowMessage(message);
        }
    }

    private void ResolveReferences()
    {
        playerHealth ??= FindFirstObjectByType<PlayerHealth>();
        playerArmor ??= FindFirstObjectByType<PlayerArmor>();
        playerDash ??= FindFirstObjectByType<PlayerDash>();
        reinforcementController ??= FindFirstObjectByType<PlayerReinforcementController>();
        reinforcementSlotUI ??= FindFirstObjectByType<ReinforcementSlotUI>();
        cargoController ??= FindFirstObjectByType<PlayerCargoController>();
        weaponHeatUI ??= FindFirstObjectByType<WeaponHeatUI>(FindObjectsInactive.Include);
        warningMessageUI ??= FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        ResolveInputActions();

        if (objectiveDirector == null && Application.isPlaying)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

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

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

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

        subscribed = false;
    }

    private void ApplyCinematicVisibility()
    {
        bool visible = !cinematicMode;
        SetGameObjectVisible(statusRoot, visible);
        SetGameObjectVisible(resourceRoot, visible);
        SetGameObjectVisible(cargoRoot, visible);
        SetGameObjectVisible(objectiveRoot, visible);
        reinforcementSlotUI?.SetVisible(visible);
        weaponHeatUI?.SetExternalVisible(visible);

        if (statusRoot == null)
        {
            hpGauge?.SetVisible(visible);
            dashGauge?.SetVisible(visible);
        }

        if (cargoRoot == null)
        {
            cargoGauge?.SetVisible(visible);
        }

        creditsCounter?.SetExternalVisible(visible);
        scrapCounter?.SetExternalVisible(visible);
        coreShardCounter?.SetExternalVisible(visible);
        tuningChipCounter?.SetExternalVisible(visible);

        if (additionalObjectsToHideDuringCinematic != null)
        {
            for (int i = 0; i < additionalObjectsToHideDuringCinematic.Length; i++)
            {
                SetGameObjectVisible(additionalObjectsToHideDuringCinematic[i], visible);
            }
        }

        RefreshBindingHints();
    }

    private static void SetGameObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private void HandleRunStarted(RunContext _) => RefreshAll();
    private void HandleWalletChanged(RunWallet wallet) => RefreshWallet(wallet);
    private void HandleHealthChanged(float current, float max) => RefreshHealthAndArmor(current, max, playerArmor != null ? playerArmor.CurrentArmor : 0f);
    private void HandleArmorChanged(float current, float max) => RefreshHealthAndArmor(playerHealth != null ? playerHealth.CurrentHp : 0f, playerHealth != null ? playerHealth.MaxHp : 1f, current);
    private void HandleObjectiveProgressChanged(int current, int required) => RefreshObjectiveProgress(current, required);
    private void HandleCoreRevealed() => RefreshObjectiveProgress();
    private void HandleDashStarted(Vector2 _) => UpdateDashDisplay();
    private void HandleDashEnded() => UpdateDashDisplay();
    private void HandleReinforcementEquipmentChanged(ReinforcementDefinition _, int __, int ___) => UpdateReinforcementSlot();
    private void HandleReinforcementChargesChanged(int _, int __, float ___) => UpdateReinforcementSlot();
    private void HandleReinforcementUsed(ReinforcementDefinition _) => UpdateReinforcementSlot();
    private void HandleCargoChanged(int _, int __) => UpdateCargoDisplay();
    private void HandleGameSettingsChanged() => RefreshBindingHints();

    private void HandleInputActionChange(object changedObject, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            RefreshBindingHints();
        }
    }

    private void RefreshHealthAndArmor(float currentHp, float maxHp, float armor)
    {
        maxHp = Mathf.Max(1f, maxHp);
        hpGauge?.SetValue(currentHp, maxHp);

        if (hpValueText != null)
        {
            hpValueText.text = string.Format(hpValueFormat, currentHp, maxHp);
        }

        bool hasArmor = armor > 0.001f;
        GameObject resolvedArmorRoot = armorBonusRoot != null
            ? armorBonusRoot
            : armorBonusText != null ? armorBonusText.gameObject : null;

        SetGameObjectVisible(resolvedArmorRoot, hasArmor);

        if (hasArmor && armorBonusText != null)
        {
            armorBonusText.text = string.Format(armorBonusFormat, armor);
            armorBonusText.color = armorBonusColor;
            RefreshArmorBonusPosition();
        }

        RefreshArmorFill(currentHp, maxHp, armor);

        if (legacyArmorGauge != null)
        {
            legacyArmorGauge.SetVisible(false);
        }
    }


    private void ResolveInputActions()
    {
        if (inputActions != null)
        {
            return;
        }

        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);

        if (interactor != null && interactor.InputActions != null)
        {
            inputActions = interactor.InputActions;
            playerActionMapName = interactor.ActionMapName;
            InputBindingPersistence.LoadOnce(inputActions);
            return;
        }

        PlayerController2D controller = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);

        if (controller != null && controller.InputActions != null)
        {
            inputActions = controller.InputActions;
            playerActionMapName = controller.ActionMapName;
            InputBindingPersistence.LoadOnce(inputActions);
        }
    }

    private void RefreshArmorBonusPosition()
    {
        if (!autoPositionArmorBonusBesideHpText || hpValueText == null || armorBonusText == null)
        {
            return;
        }

        RectTransform hpRect = hpValueText.rectTransform;
        RectTransform armorRect = armorBonusRoot != null
            ? armorBonusRoot.transform as RectTransform
            : armorBonusText.rectTransform;

        if (armorRect == null || armorRect.parent == null)
        {
            return;
        }

        if (armorRect.parent.GetComponent<LayoutGroup>() != null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        Vector3[] worldCorners = new Vector3[4];
        hpRect.GetWorldCorners(worldCorners);
        Vector3 rightCenter = (worldCorners[2] + worldCorners[3]) * 0.5f;

        armorRect.pivot = new Vector2(0f, 0.5f);
        armorRect.position = rightCenter;
        armorRect.anchoredPosition += Vector2.right * armorBonusTextSpacing;
    }

    private void RefreshArmorFill(float currentHp, float maxHp, float armor)
    {
        if (armorFillRect == null)
        {
            return;
        }

        bool hasArmor = armor > 0.001f;
        SetGameObjectVisible(armorFillRect.gameObject, hasArmor);

        if (!hasArmor)
        {
            return;
        }

        maxHp = Mathf.Max(1f, maxHp);
        float hpRatio = Mathf.Clamp01(currentHp / maxHp);
        float armorEndRatio = Mathf.Max(hpRatio, (currentHp + armor) / maxHp);

        Vector2 anchorMin = armorFillRect.anchorMin;
        Vector2 anchorMax = armorFillRect.anchorMax;
        anchorMin.x = hpRatio;
        anchorMin.y = 0f;
        anchorMax.x = armorEndRatio;
        anchorMax.y = 1f;

        armorFillRect.anchorMin = anchorMin;
        armorFillRect.anchorMax = anchorMax;
        armorFillRect.offsetMin = Vector2.zero;
        armorFillRect.offsetMax = Vector2.zero;

        if (armorFillImage != null)
        {
            armorFillImage.color = armorFillColor;
        }
    }

    private void RefreshObjectiveProgress()
    {
        if (objectiveDirector == null && Application.isPlaying)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }

        int current = objectiveDirector != null ? objectiveDirector.SignalCount : 0;
        int required = objectiveDirector != null ? objectiveDirector.SignalsRequiredToRevealCore : 2;
        RefreshObjectiveProgress(current, required);
    }

    private void RefreshObjectiveProgress(int current, int required)
    {
        required = Mathf.Max(1, required);
        int clamped = Mathf.Clamp(current, 0, required);
        bool ready = current >= required;

        if (coreSignalPips != null && coreSignalPips.Length > 0)
        {
            for (int i = 0; i < coreSignalPips.Length; i++)
            {
                if (coreSignalPips[i] != null)
                {
                    coreSignalPips[i].color = i < clamped
                        ? ready ? coreSignalReadyColor : coreSignalActiveColor
                        : coreSignalInactiveColor;
                }
            }
        }
        else if (legacyObjectiveSignalGauge != null)
        {
            legacyObjectiveSignalGauge.SetValue(clamped, required);
        }

        if (coreSignalIcon != null)
        {
            coreSignalIcon.color = ready ? coreSignalReadyColor : coreSignalActiveColor;
        }

        if (coreSignalCountText != null)
        {
            coreSignalCountText.text = ready
                ? coreSignalReadyText
                : string.Format(coreSignalCountFormat, current, required);
        }

        SetGameObjectVisible(coreSignalReadyPulseRoot, ready);
    }

    private void RefreshWallet(RunWallet wallet)
    {
        ResourceCounterUI[] counters = { creditsCounter, scrapCounter, coreShardCounter, tuningChipCounter };
        for (int i = 0; i < counters.Length; i++)
        {
            counters[i]?.SetHideWhenZero(hideZeroResources);
        }

        creditsCounter?.SetAmount(wallet != null ? wallet.Credits : 0);
        scrapCounter?.SetAmount(wallet != null ? wallet.PendingScrapParts : 0);
        coreShardCounter?.SetAmount(wallet != null ? wallet.PendingCoreShards : 0);
        tuningChipCounter?.SetAmount(wallet != null ? wallet.TuningChips : 0);
        UpdateCargoDisplay();
    }

    private void UpdateDashDisplay()
    {
        bool ready = playerDash == null || playerDash.CanDash;
        float ratio = 1f;
        float remaining = 0f;

        if (!ready && playerDash != null)
        {
            float cooldown = Mathf.Max(0.05f, playerDash.DashCooldown);
            remaining = Mathf.Clamp(playerDash.RemainingCooldown, 0f, cooldown);
            ratio = 1f - Mathf.Clamp01(remaining / cooldown);
        }

        dashGauge?.SetRatio(ratio);
        dashGauge?.SetText(ready ? string.Empty : $"{remaining:0.0}");

        if (dashCooldownFill != null)
        {
            dashCooldownFill.fillAmount = ready ? 0f : 1f - ratio;
        }

        if (dashIcon != null)
        {
            dashIcon.color = ready ? dashReadyColor : dashCooldownColor;
        }

        if (dashCooldownText != null)
        {
            dashCooldownText.text = ready ? string.Empty : $"{remaining:0.0}";
        }

        SetGameObjectVisible(dashReadyGlowRoot, ready);
    }

    private void UpdateCargoDisplay()
    {
        int current = 0;
        int capacity = 0;

        if (cargoController != null)
        {
            current = cargoController.CurrentLoad;
            capacity = cargoController.MaxCapacity;
        }
        else if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunContext runContext = RunManager.Instance.CurrentRun;
            current = runContext.CurrentCargoLoad;
            capacity = runContext.MaxCargoCapacity;
        }

        int safeCapacity = Mathf.Max(1, capacity);
        float ratio = Mathf.Clamp01(current / (float)safeCapacity);
        Color color = ratio >= 0.999f
            ? cargoFullColor
            : ratio >= cargoWarningRatio ? cargoWarningColor : cargoNormalColor;

        cargoGauge?.SetValue(current, safeCapacity);
        cargoGauge?.SetText(string.Format(cargoValueFormat, current, capacity));
        cargoGauge?.SetFillColor(color);

        if (cargoValueText != null)
        {
            cargoValueText.text = string.Format(cargoValueFormat, current, capacity);
            cargoValueText.color = color;
        }
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

    private void RefreshBindingHints()
    {
        ResolveInputActions();

        string mapKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, mapActionName, "Tab");
        string inventoryKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, inventoryActionName, "E");
        string reinforcementKey = InputBindingUtility.GetDisplayString(inputActions, playerActionMapName, reinforcementActionName, "R");

        reinforcementSlotUI?.SetKeyLabel(reinforcementKey);

        bool visible = !cinematicMode && GameSettingsRuntime.ShowHudKeyHints;
        SetGameObjectVisible(menuHintRoot, visible);

        if (!visible)
        {
            return;
        }

        if (mapHintText != null)
        {
            mapHintText.text = string.Format(mapHintFormat, mapKey);
        }

        if (inventoryHintText != null)
        {
            inventoryHintText.text = string.Format(inventoryHintFormat, inventoryKey);
        }

        if (reinforcementKeyText != null)
        {
            reinforcementKeyText.text = string.Format(reinforcementKeyFormat, reinforcementKey);
        }
    }
}
