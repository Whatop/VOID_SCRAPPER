using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class PlayerChargeGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private GaugeBarUI chargeGauge;
    [SerializeField] private WorldGaugeFollower follower;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Weapon Sources")]
    [SerializeField] private PlayerWeaponBase[] weaponSources;

    [Header("Display")]
    [SerializeField] private string chargingTextFormat = "{0:0}%";
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool hideWhenReleased = true;
    [SerializeField] private Color chargeColor = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color machineGunCoolingColor = new Color(0.25f, 1f, 0.35f, 1f);
    [SerializeField] private Color machineGunReadyColor = new Color(0.75f, 1f, 0.78f, 1f);
    [SerializeField, Min(0f)] private float readyPulseHoldSeconds = 0.3f;

    private PlayerWeaponBase activeChargingWeapon;
    private MachineGunWeapon activeMachineGun;
    private Tween readyHideTween;
    private bool machineGunCoolingVisible;
    private bool externalPresentationActive;
    private bool subscribed;
    private PlayerWeaponController subscribedWeaponController;
    private PlayerWeaponBase[] subscribedWeaponSources;

    public void ConfigureRuntime(
        Transform target,
        GaugeBarUI runtimeChargeGauge,
        WorldGaugeFollower runtimeFollower,
        CanvasGroup runtimeCanvasGroup,
        PlayerWeaponController runtimeWeaponController)
    {
        UnsubscribeWeaponController();
        BindMachineGun(null);
        UnsubscribeWeapons();

        playerTarget = target;
        chargeGauge = runtimeChargeGauge;
        follower = runtimeFollower;
        canvasGroup = runtimeCanvasGroup;
        weaponController = runtimeWeaponController;
        weaponSources = playerTarget != null
            ? playerTarget.GetComponentsInChildren<PlayerWeaponBase>(true)
            : System.Array.Empty<PlayerWeaponBase>();

        if (follower != null)
        {
            follower.SetTarget(playerTarget);
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        SubscribeWeapons();
        SubscribeWeaponController();
        if (weaponController != null)
        {
            HandleWeaponEquipped(weaponController.CurrentWeaponTree, weaponController.CurrentWeapon);
        }
    }

    private void Reset()
    {
        chargeGauge = GetComponentInChildren<GaugeBarUI>(true);
        follower = GetComponent<WorldGaugeFollower>();
        canvasGroup = GetComponent<CanvasGroup>();
        weaponController = FindFirstObjectByType<PlayerWeaponController>();
    }

    private void Awake()
    {
        CacheReferences();
        ResolvePlayerTarget();
        ResolveWeaponSources();
        ResolveWeaponController();

        if (follower != null)
        {
            follower.SetTarget(playerTarget);
        }

        if (hideOnAwake)
        {
            HideVisual();
        }
        else
        {
            SetVisible(true);
        }
    }

    private void OnEnable()
    {
        SubscribeWeapons();
        SubscribeWeaponController();

        if (weaponController != null)
        {
            HandleWeaponEquipped(weaponController.CurrentWeaponTree, weaponController.CurrentWeapon);
        }

        if (hideOnAwake && activeChargingWeapon == null && !machineGunCoolingVisible)
        {
            HideVisual();
        }
    }

    private void OnDisable()
    {
        readyHideTween?.Kill();
        readyHideTween = null;
        UnsubscribeWeaponController();
        BindMachineGun(null);
        UnsubscribeWeapons();
        activeChargingWeapon = null;
        machineGunCoolingVisible = false;
        externalPresentationActive = false;
        HideVisual();
    }

    public void ShowRatio(float ratio)
    {
        externalPresentationActive = true;
        ShowRatioInternal(ratio, chargeColor, string.Format(chargingTextFormat, Mathf.Clamp01(ratio) * 100f));
    }

    private void ShowRatioInternal(float ratio, Color color, string text)
    {
        ratio = Mathf.Clamp01(ratio);

        SetVisible(true);

        if (chargeGauge != null)
        {
            chargeGauge.SetRatio(ratio);
            chargeGauge.SetFillColor(color);
            chargeGauge.SetText(text);
        }
    }

    public void Hide()
    {
        externalPresentationActive = false;

        if (activeMachineGun != null && activeMachineGun.IsOverheated)
        {
            RefreshMachineGunCooling(activeMachineGun.CurrentHeat, activeMachineGun.MaxHeat, true);
            return;
        }

        if (activeChargingWeapon != null && activeChargingWeapon.IsCharging)
        {
            ShowChargeRatio(activeChargingWeapon.ChargeRatio);
            return;
        }

        HideVisual();
    }

    private void OnDestroy()
    {
        OnDisable();
    }

    private void HideVisual()
    {

        if (chargeGauge != null)
        {
            chargeGauge.SetRatio(0f);
        }

        SetVisible(false);
    }

    private void CacheReferences()
    {
        if (chargeGauge == null)
        {
            chargeGauge = GetComponentInChildren<GaugeBarUI>(true);
        }

        if (follower == null)
        {
            follower = GetComponent<WorldGaugeFollower>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        ResolveWeaponController();
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null)
        {
            return;
        }

        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        if (player != null)
        {
            playerTarget = player.transform;
        }
    }

    private void ResolveWeaponSources()
    {
        if (weaponSources != null && weaponSources.Length > 0)
        {
            return;
        }

        if (playerTarget != null)
        {
            weaponSources = playerTarget.GetComponentsInChildren<PlayerWeaponBase>(true);
            return;
        }

        weaponSources = FindObjectsByType<PlayerWeaponBase>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
    }

    private void SubscribeWeapons()
    {
        if (subscribed)
        {
            return;
        }

        if (weaponSources == null || weaponSources.Length == 0)
        {
            ResolveWeaponSources();
        }

        if (weaponSources == null)
        {
            return;
        }

        subscribedWeaponSources = (PlayerWeaponBase[])weaponSources.Clone();
        foreach (PlayerWeaponBase weapon in subscribedWeaponSources)
        {
            if (weapon == null)
            {
                continue;
            }

            // Serialized arrays may repeat a source; own exactly one handler.
            weapon.ChargeStarted -= HandleChargeStarted;
            weapon.ChargeChanged -= HandleChargeChanged;
            weapon.ChargeReleased -= HandleChargeReleased;
            weapon.ChargeCanceled -= HandleChargeCanceled;
            weapon.ChargeStarted += HandleChargeStarted;
            weapon.ChargeChanged += HandleChargeChanged;
            weapon.ChargeReleased += HandleChargeReleased;
            weapon.ChargeCanceled += HandleChargeCanceled;
        }

        subscribed = true;
    }

    private void ResolveWeaponController()
    {
        if (weaponController == null)
        {
            weaponController = FindFirstObjectByType<PlayerWeaponController>();
        }
    }

    private void SubscribeWeaponController()
    {
        ResolveWeaponController();
        if (subscribedWeaponController == weaponController)
        {
            return;
        }
        UnsubscribeWeaponController();
        subscribedWeaponController = weaponController;
        if (subscribedWeaponController != null)
        {
            subscribedWeaponController.WeaponEquipped += HandleWeaponEquipped;
        }
    }

    private void UnsubscribeWeaponController()
    {
        if (subscribedWeaponController != null)
        {
            subscribedWeaponController.WeaponEquipped -= HandleWeaponEquipped;
        }
        subscribedWeaponController = null;
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTree, PlayerWeaponBase weapon)
    {
        readyHideTween?.Kill();
        readyHideTween = null;
        machineGunCoolingVisible = false;
        BindMachineGun(weaponTree == WeaponTreeType.MachineGun ? weapon as MachineGunWeapon : null);

        if (activeMachineGun != null && activeMachineGun.IsOverheated)
        {
            RefreshMachineGunCooling(activeMachineGun.CurrentHeat, activeMachineGun.MaxHeat, true);
        }
        else if (activeChargingWeapon == null)
        {
            if (!externalPresentationActive)
            {
                HideVisual();
            }
        }
    }

    private void BindMachineGun(MachineGunWeapon weapon)
    {
        if (activeMachineGun == weapon)
        {
            return;
        }

        if (activeMachineGun != null)
        {
            activeMachineGun.HeatChanged -= HandleMachineGunHeatChanged;
        }

        activeMachineGun = weapon;

        if (activeMachineGun != null)
        {
            activeMachineGun.HeatChanged += HandleMachineGunHeatChanged;
        }
    }

    private void HandleMachineGunHeatChanged(float current, float max, bool overheated)
    {
        RefreshMachineGunCooling(current, max, overheated);
    }

    private void RefreshMachineGunCooling(float current, float max, bool overheated)
    {
        if (activeMachineGun == null)
        {
            return;
        }

        if (overheated)
        {
            machineGunCoolingVisible = true;
            readyHideTween?.Kill();
            readyHideTween = null;

            float recoveryHeat = activeMachineGun.OverheatRecoveryHeat;
            float recoveryRange = Mathf.Max(0.001f, max - recoveryHeat);
            float recoveryProgress = Mathf.Clamp01((max - current) / recoveryRange);
            if (!externalPresentationActive)
            {
                ShowRatioInternal(
                    recoveryProgress,
                    machineGunCoolingColor,
                    string.Format(chargingTextFormat, recoveryProgress * 100f)
                );
            }
            return;
        }

        if (!machineGunCoolingVisible)
        {
            return;
        }

        machineGunCoolingVisible = false;
        if (externalPresentationActive)
        {
            return;
        }

        ShowRatioInternal(1f, machineGunReadyColor, string.Format(chargingTextFormat, 100f));
        readyHideTween = DOVirtual.DelayedCall(
            Mathf.Max(0f, readyPulseHoldSeconds),
            () =>
            {
                readyHideTween = null;
                if (!machineGunCoolingVisible && activeChargingWeapon == null)
                {
                    HideVisual();
                }
            },
            false
        ).SetTarget(this);
    }

    private void UnsubscribeWeapons()
    {
        if (!subscribed)
        {
            return;
        }

        if (subscribedWeaponSources == null)
        {
            subscribed = false;
            return;
        }

        foreach (PlayerWeaponBase weapon in subscribedWeaponSources)
        {
            if (weapon == null)
            {
                continue;
            }

            weapon.ChargeStarted -= HandleChargeStarted;
            weapon.ChargeChanged -= HandleChargeChanged;
            weapon.ChargeReleased -= HandleChargeReleased;
            weapon.ChargeCanceled -= HandleChargeCanceled;
        }

        subscribed = false;
        subscribedWeaponSources = null;
    }

    private void HandleChargeStarted(PlayerWeaponBase weapon)
    {
        if (machineGunCoolingVisible)
        {
            return;
        }

        activeChargingWeapon = weapon;
        if (!externalPresentationActive)
        {
            ShowChargeRatio(weapon != null ? weapon.ChargeRatio : 0f);
        }
    }

    private void HandleChargeChanged(PlayerWeaponBase weapon, float ratio)
    {
        if (machineGunCoolingVisible)
        {
            return;
        }

        activeChargingWeapon = weapon;
        if (!externalPresentationActive)
        {
            ShowChargeRatio(ratio);
        }
    }

    private void HandleChargeReleased(PlayerWeaponBase weapon, float ratio)
    {
        if (machineGunCoolingVisible)
        {
            return;
        }

        if (!externalPresentationActive)
        {
            ShowChargeRatio(ratio);
        }

        if (hideWhenReleased)
        {
            activeChargingWeapon = null;
            if (!externalPresentationActive)
            {
                HideVisual();
            }
        }
    }

    private void HandleChargeCanceled(PlayerWeaponBase weapon)
    {
        if (!machineGunCoolingVisible && activeChargingWeapon == weapon)
        {
            activeChargingWeapon = null;
            if (!externalPresentationActive)
            {
                HideVisual();
            }
        }
    }

    private void ShowChargeRatio(float ratio)
    {
        ShowRatioInternal(
            ratio,
            chargeColor,
            string.Format(chargingTextFormat, Mathf.Clamp01(ratio) * 100f)
        );
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (chargeGauge != null)
        {
            chargeGauge.SetVisible(visible);
        }

        if (follower != null)
        {
            follower.SetVisible(visible);
        }
    }
}
