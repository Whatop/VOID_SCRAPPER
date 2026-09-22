using UnityEngine;

public partial class SniperWeapon
{
    private float inputChargeTime;
    private float reservedChargeFraction;
    private float reserveExpiresAt = float.NegativeInfinity;
    private bool assistedCharge;
    private float firingChargeWidthRatio;

    public float EffectiveMovingChargeMultiplier => Mathf.Clamp(movingChargeSpeedMultiplier +
        (weaponModifiers != null ? weaponModifiers.SniperMovingChargeBonus : 0f), .1f, 1f);
    public bool HasReservedCharge => reservedChargeFraction > 0f && Time.time <= reserveExpiresAt &&
        weaponModifiers != null && weaponModifiers.SniperReserveChargeFraction > 0f;
    public bool IsReserveAssistedCharge => assistedCharge;

    private void ClearReservedCharge()
    {
        reservedChargeFraction = 0f;
        reserveExpiresAt = float.NegativeInfinity;
    }

    protected override void ConfigureSpawnedProjectile(Bullet bullet)
    {
        if (bullet == null || weaponModifiers == null || firingChargeWidthRatio <= 0f) return;
        bullet.ConfigureEquipmentWidth(1f + weaponModifiers.SniperChargedWidthBonus * Mathf.Clamp01(firingChargeWidthRatio));
    }
}
