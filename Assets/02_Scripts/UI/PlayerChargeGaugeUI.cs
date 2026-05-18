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

    [Header("Weapon Sources")]
    [SerializeField] private PlayerWeaponBase[] weaponSources;

    [Header("Display")]
    [SerializeField] private string chargingTextFormat = "{0:0}%";
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool hideWhenReleased = true;

    private PlayerWeaponBase activeChargingWeapon;
    private bool subscribed;

    private void Reset()
    {
        chargeGauge = GetComponentInChildren<GaugeBarUI>(true);
        follower = GetComponent<WorldGaugeFollower>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        CacheReferences();
        ResolvePlayerTarget();
        ResolveWeaponSources();

        if (follower != null)
        {
            follower.SetTarget(playerTarget);
        }

        if (hideOnAwake)
        {
            Hide();
        }
        else
        {
            SetVisible(true);
        }
    }

    private void OnEnable()
    {
        SubscribeWeapons();

        if (hideOnAwake && activeChargingWeapon == null)
        {
            Hide();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWeapons();
        Hide();
    }

    public void ShowRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        SetVisible(true);

        if (chargeGauge != null)
        {
            chargeGauge.SetRatio(ratio);
            chargeGauge.SetText(string.Format(chargingTextFormat, ratio * 100f));
        }
    }

    public void Hide()
    {
        activeChargingWeapon = null;

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

        foreach (PlayerWeaponBase weapon in weaponSources)
        {
            if (weapon == null)
            {
                continue;
            }

            weapon.ChargeStarted += HandleChargeStarted;
            weapon.ChargeChanged += HandleChargeChanged;
            weapon.ChargeReleased += HandleChargeReleased;
            weapon.ChargeCanceled += HandleChargeCanceled;
        }

        subscribed = true;
    }

    private void UnsubscribeWeapons()
    {
        if (!subscribed)
        {
            return;
        }

        if (weaponSources == null)
        {
            subscribed = false;
            return;
        }

        foreach (PlayerWeaponBase weapon in weaponSources)
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
    }

    private void HandleChargeStarted(PlayerWeaponBase weapon)
    {
        activeChargingWeapon = weapon;
        ShowRatio(weapon != null ? weapon.ChargeRatio : 0f);
    }

    private void HandleChargeChanged(PlayerWeaponBase weapon, float ratio)
    {
        activeChargingWeapon = weapon;
        ShowRatio(ratio);
    }

    private void HandleChargeReleased(PlayerWeaponBase weapon, float ratio)
    {
        ShowRatio(ratio);

        if (hideWhenReleased)
        {
            Hide();
        }
    }

    private void HandleChargeCanceled(PlayerWeaponBase weapon)
    {
        if (activeChargingWeapon == weapon)
        {
            Hide();
        }
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