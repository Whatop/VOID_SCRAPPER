using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Machine Gun Animation")]
    [SerializeField] private bool machineGunStartsFromLeft = true;

    private PlayerWeaponBase currentWeapon;
    private WeaponTreeType currentWeaponTree;
    private bool nextMachineGunShotLeft;

    private readonly HashSet<int> animatorParameterHashes = new HashSet<int>();

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int ChargeRatioHash = Animator.StringToHash("ChargeRatio");
    private static readonly int WeaponTreeHash = Animator.StringToHash("WeaponTree");

    private static readonly int DashHash = Animator.StringToHash("Dash");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int FireLeftHash = Animator.StringToHash("FireLeft");
    private static readonly int FireRightHash = Animator.StringToHash("FireRight");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int ChargeStartHash = Animator.StringToHash("ChargeStart");
    private static readonly int ChargeReleaseHash = Animator.StringToHash("ChargeRelease");
    private static readonly int ChargeCancelHash = Animator.StringToHash("ChargeCancel");
    private static readonly int WeaponChangedHash = Animator.StringToHash("WeaponChanged");

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CacheAnimatorParameters();
        ResetMachineGunShotSide();
    }

    private void OnEnable()
    {
        CacheReferences();
        CacheAnimatorParameters();

        SubscribeHealth();
        SubscribeDash();
        SubscribeWeaponController();

        if (weaponController != null)
        {
            currentWeaponTree = weaponController.CurrentWeaponTree;
            AttachWeapon(weaponController.CurrentWeapon);
            SetIntegerIfExists(WeaponTreeHash, (int)currentWeaponTree);
        }

        ResetMachineGunShotSide();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
        UnsubscribeDash();
        UnsubscribeWeaponController();
        DetachWeapon();
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        bool isMoving = playerController != null && playerController.IsMoving;
        bool isDashing = playerDash != null && playerDash.IsDashing;
        bool isCharging = currentWeapon != null && currentWeapon.IsCharging;
        float chargeRatio = currentWeapon != null ? currentWeapon.ChargeRatio : 0f;

        SetBoolIfExists(IsMovingHash, isMoving);
        SetBoolIfExists(IsDashingHash, isDashing);
        SetBoolIfExists(IsChargingHash, isCharging);
        SetFloatIfExists(ChargeRatioHash, chargeRatio);
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponentInParent<PlayerDash>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<PlayerHealth>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponentInParent<PlayerWeaponController>();
        }
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterHashes.Clear();

        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        if (parameters == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in parameters)
        {
            animatorParameterHashes.Add(parameter.nameHash);
        }
    }

    private void SubscribeHealth()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.Damaged += HandleDamaged;
        playerHealth.Died += HandleDied;
    }

    private void UnsubscribeHealth()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.Damaged -= HandleDamaged;
        playerHealth.Died -= HandleDied;
    }

    private void SubscribeDash()
    {
        if (playerDash == null)
        {
            return;
        }

        playerDash.DashStarted += HandleDashStarted;
        playerDash.DashEnded += HandleDashEnded;
    }

    private void UnsubscribeDash()
    {
        if (playerDash == null)
        {
            return;
        }

        playerDash.DashStarted -= HandleDashStarted;
        playerDash.DashEnded -= HandleDashEnded;
    }

    private void SubscribeWeaponController()
    {
        if (weaponController == null)
        {
            return;
        }

        weaponController.WeaponEquipped += HandleWeaponEquipped;
    }

    private void UnsubscribeWeaponController()
    {
        if (weaponController == null)
        {
            return;
        }

        weaponController.WeaponEquipped -= HandleWeaponEquipped;
    }

    private void AttachWeapon(PlayerWeaponBase weapon)
    {
        if (currentWeapon == weapon)
        {
            return;
        }

        DetachWeapon();

        currentWeapon = weapon;

        if (currentWeapon == null)
        {
            SetBoolIfExists(IsChargingHash, false);
            SetFloatIfExists(ChargeRatioHash, 0f);
            return;
        }

        currentWeapon.Fired += HandleWeaponFired;
        currentWeapon.ChargeStarted += HandleChargeStarted;
        currentWeapon.ChargeChanged += HandleChargeChanged;
        currentWeapon.ChargeReleased += HandleChargeReleased;
        currentWeapon.ChargeCanceled += HandleChargeCanceled;

        SetBoolIfExists(IsChargingHash, currentWeapon.IsCharging);
        SetFloatIfExists(ChargeRatioHash, currentWeapon.ChargeRatio);
    }

    private void DetachWeapon()
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.Fired -= HandleWeaponFired;
        currentWeapon.ChargeStarted -= HandleChargeStarted;
        currentWeapon.ChargeChanged -= HandleChargeChanged;
        currentWeapon.ChargeReleased -= HandleChargeReleased;
        currentWeapon.ChargeCanceled -= HandleChargeCanceled;

        currentWeapon = null;
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTreeType, PlayerWeaponBase weapon)
    {
        currentWeaponTree = weaponTreeType;

        AttachWeapon(weapon);
        ResetMachineGunShotSide();

        SetIntegerIfExists(WeaponTreeHash, (int)currentWeaponTree);
        SetTriggerIfExists(WeaponChangedHash);
    }

    private void HandleWeaponFired(PlayerWeaponBase weapon, float powerRatio)
    {
        SetFloatIfExists(ChargeRatioHash, powerRatio);

        if (currentWeaponTree == WeaponTreeType.MachineGun)
        {
            PlayMachineGunFire();
            return;
        }

        SetTriggerIfExists(FireHash);
    }

    private void PlayMachineGunFire()
    {
        if (nextMachineGunShotLeft)
        {
            ResetTriggerIfExists(FireRightHash);
            SetTriggerIfExists(FireLeftHash);
        }
        else
        {
            ResetTriggerIfExists(FireLeftHash);
            SetTriggerIfExists(FireRightHash);
        }

        nextMachineGunShotLeft = !nextMachineGunShotLeft;
    }

    private void ResetMachineGunShotSide()
    {
        nextMachineGunShotLeft = machineGunStartsFromLeft;
    }

    private void HandleChargeStarted(PlayerWeaponBase weapon)
    {
        SetBoolIfExists(IsChargingHash, true);
        SetFloatIfExists(ChargeRatioHash, 0f);
        SetTriggerIfExists(ChargeStartHash);
    }

    private void HandleChargeChanged(PlayerWeaponBase weapon, float chargeRatio)
    {
        SetBoolIfExists(IsChargingHash, true);
        SetFloatIfExists(ChargeRatioHash, chargeRatio);
    }

    private void HandleChargeReleased(PlayerWeaponBase weapon, float chargeRatio)
    {
        SetFloatIfExists(ChargeRatioHash, chargeRatio);
        SetBoolIfExists(IsChargingHash, false);
        SetTriggerIfExists(ChargeReleaseHash);
    }

    private void HandleChargeCanceled(PlayerWeaponBase weapon)
    {
        SetBoolIfExists(IsChargingHash, false);
        SetFloatIfExists(ChargeRatioHash, 0f);
        SetTriggerIfExists(ChargeCancelHash);
    }

    private void HandleDashStarted(Vector2 direction)
    {
        SetBoolIfExists(IsDashingHash, true);
        SetTriggerIfExists(DashHash);
    }

    private void HandleDashEnded()
    {
        SetBoolIfExists(IsDashingHash, false);
    }

    private void HandleDamaged(float currentHp, float maxHp)
    {
        SetTriggerIfExists(HitHash);
    }

    private void HandleDied()
    {
        SetBoolIfExists(IsMovingHash, false);
        SetBoolIfExists(IsDashingHash, false);
        SetBoolIfExists(IsChargingHash, false);
        SetFloatIfExists(ChargeRatioHash, 0f);
        SetTriggerIfExists(DeathHash);
    }

    private bool HasParameter(int parameterHash)
    {
        return animator != null && animatorParameterHashes.Contains(parameterHash);
    }

    private void SetBoolIfExists(int parameterHash, bool value)
    {
        if (!HasParameter(parameterHash))
        {
            return;
        }

        animator.SetBool(parameterHash, value);
    }

    private void SetFloatIfExists(int parameterHash, float value)
    {
        if (!HasParameter(parameterHash))
        {
            return;
        }

        animator.SetFloat(parameterHash, value);
    }

    private void SetIntegerIfExists(int parameterHash, int value)
    {
        if (!HasParameter(parameterHash))
        {
            return;
        }

        animator.SetInteger(parameterHash, value);
    }

    private void SetTriggerIfExists(int parameterHash)
    {
        if (!HasParameter(parameterHash))
        {
            return;
        }

        animator.SetTrigger(parameterHash);
    }

    private void ResetTriggerIfExists(int parameterHash)
    {
        if (!HasParameter(parameterHash))
        {
            return;
        }

        animator.ResetTrigger(parameterHash);
    }
}