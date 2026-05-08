using UnityEngine;

public class SniperWeapon : PlayerWeaponBase
{
    [Header("Sniper Fallback Values")]
    [SerializeField] private float fallbackMinDamage = 4f;
    [SerializeField] private float fallbackMaxDamage = 10f;
    [SerializeField] private float fallbackSpeed = 22f;
    [SerializeField] private float fallbackRange = 16f;
    [SerializeField] private int fallbackPierceCount = 3;

    [Header("Charge")]
    [SerializeField] private float fallbackMaxChargeTime = 1.2f;
    [SerializeField] private float nextChargeDelay = 0.25f;
    [SerializeField] private bool cancelChargeOnMove = true;

    [Header("Camera Zoom")]
    [SerializeField] private CameraZoomController2D cameraZoomController;
    [SerializeField] private float fallbackChargeZoomBonus = 0.2f;

    private bool isCharging;
    private float chargeTimer;
    private float nextChargeAllowedTime;

    public float ChargeRatio
    {
        get
        {
            float maxChargeTime = GetMaxChargeTime();
            if (maxChargeTime <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(chargeTimer / maxChargeTime);
        }
    }

    public override void OnEquip()
    {
        ResetChargeState();
    }

    public override void OnUnequip()
    {
        CancelCharge();
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        if (input.PressedThisFrame)
        {
            TryBeginCharge();
        }

        if (!isCharging)
        {
            return;
        }

        if (cancelChargeOnMove && playerController != null && playerController.IsMoving)
        {
            CancelCharge();
            return;
        }

        chargeTimer += deltaTime;
        UpdateCameraZoom();

        if (input.ReleasedThisFrame)
        {
            FireChargedShot();
        }
    }

    private void TryBeginCharge()
    {
        if (Time.time < nextChargeAllowedTime)
        {
            return;
        }

        isCharging = true;
        chargeTimer = 0f;
        UpdateCameraZoom();
    }

    private void FireChargedShot()
    {
        if (!isCharging)
        {
            return;
        }

        Vector2 direction = GetAimDirection();

        float damage = Mathf.Lerp(
            GetMinChargeDamage(),
            GetMaxChargeDamage(),
            ChargeRatio
        );

        float baseSpeed = GetProjectileSpeed(fallbackSpeed);
        float baseRange = GetProjectileRange(fallbackRange);
        int basePierce = GetProjectilePierceCount(fallbackPierceCount);

        if (weaponModifiers != null)
        {
            damage *= weaponModifiers.ChargeDamageMultiplier;
        }

        bool fired = SpawnProjectile(
            direction,
            damage,
            baseSpeed,
            baseRange,
            basePierce
        );

        if (fired)
        {
            RegisterAttack();
        }

        ResetChargeState();
        nextChargeAllowedTime = Time.time + nextChargeDelay;
    }

    private void CancelCharge()
    {
        ResetChargeState();
    }

    private void ResetChargeState()
    {
        isCharging = false;
        chargeTimer = 0f;

        if (cameraZoomController != null)
        {
            cameraZoomController.ResetZoom();
        }
    }

    private void UpdateCameraZoom()
    {
        if (cameraZoomController == null)
        {
            return;
        }

        float zoomBonus = fallbackChargeZoomBonus;

        if (weaponDefinition != null)
        {
            zoomBonus = weaponDefinition.ChargeCameraZoomBonus;
        }

        float targetMultiplier = 1f + (zoomBonus * ChargeRatio);
        cameraZoomController.SetZoomMultiplier(targetMultiplier);
    }

    private float GetMaxChargeTime()
    {
        float chargeTime = weaponDefinition != null
            ? weaponDefinition.MaxChargeTime
            : fallbackMaxChargeTime;

        if (weaponModifiers != null)
        {
            chargeTime *= weaponModifiers.ChargeTimeMultiplier;
        }

        return Mathf.Max(0.05f, chargeTime);
    }

    private float GetMinChargeDamage()
    {
        return weaponDefinition != null
            ? weaponDefinition.MinChargeDamage
            : fallbackMinDamage;
    }

    private float GetMaxChargeDamage()
    {
        return weaponDefinition != null
            ? weaponDefinition.MaxChargeDamage
            : fallbackMaxDamage;
    }
}