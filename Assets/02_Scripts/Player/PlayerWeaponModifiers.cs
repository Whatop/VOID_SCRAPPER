using UnityEngine;

public partial class PlayerWeaponModifiers : MonoBehaviour
{
    [Header("Runtime Multipliers")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float projectileSpeedMultiplier = 1f;
    [SerializeField] private float rangeMultiplier = 1f;
    [SerializeField] private float fireIntervalMultiplier = 1f;
    [SerializeField] private float spreadMultiplier = 1f;

    [Header("Runtime Bonuses")]
    [SerializeField] private int projectileCountBonus;
    [SerializeField] private int pierceBonus;
    [SerializeField] private int removePierceDamageFalloffCount;
    [SerializeField] private float homingAngleBonus;
    [SerializeField] private float homingRangeBonus;

    [Header("Machine Gun")]
    [SerializeField] private int machineGunTerminalGuidanceCount;

    [Header("Shotgun")]
    [SerializeField] private float shotgunCloseRangeDamagePercent;

    [Header("Sniper")]
    [SerializeField] private float chargeTimeMultiplier = 1f;
    [SerializeField] private float chargeDamageMultiplier = 1f;
    [SerializeField] private int sniperSemiAutoUnlockCount;

    public float DamageMultiplier => damageMultiplier;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float RangeMultiplier => rangeMultiplier;
    public float FireIntervalMultiplier => fireIntervalMultiplier;
    public float SpreadMultiplier => spreadMultiplier;

    public int ProjectileCountBonus => projectileCountBonus;
    public int PierceBonus => pierceBonus;
    public bool RemovePierceDamageFalloff => removePierceDamageFalloffCount > 0;
    public float HomingAngleBonus => homingAngleBonus;
    public float HomingRangeBonus => homingRangeBonus;
    public bool MachineGunTerminalGuidanceEnabled => machineGunTerminalGuidanceCount > 0;
    public float ShotgunCloseRangeDamagePercent => Mathf.Max(0f, shotgunCloseRangeDamagePercent);

    public float ChargeTimeMultiplier => chargeTimeMultiplier;
    public float ChargeDamageMultiplier => chargeDamageMultiplier;
    public bool SniperSemiAutoEnabled => sniperSemiAutoUnlockCount > 0;

    public void ResetModifiers()
    {
        ResetDevelopmentEquipment();
        damageMultiplier = 1f;
        projectileSpeedMultiplier = 1f;
        rangeMultiplier = 1f;
        fireIntervalMultiplier = 1f;
        spreadMultiplier = 1f;

        projectileCountBonus = 0;
        pierceBonus = 0;
        removePierceDamageFalloffCount = 0;
        homingAngleBonus = 0f;
        homingRangeBonus = 0f;
        machineGunTerminalGuidanceCount = 0;
        shotgunCloseRangeDamagePercent = 0f;

        chargeTimeMultiplier = 1f;
        chargeDamageMultiplier = 1f;
        sniperSemiAutoUnlockCount = 0;
    }

    public void AddDamagePercent(float percent)
    {
        MultiplyDamage(PercentToMultiplier(percent));
    }

    public void AddProjectileSpeedPercent(float percent)
    {
        MultiplyProjectileSpeed(PercentToMultiplier(percent));
    }

    public void AddRangePercent(float percent)
    {
        MultiplyRange(PercentToMultiplier(percent));
    }

    public void AddFireRatePercent(float percent)
    {
        MultiplyFireRate(PercentToMultiplier(percent));
    }

    public void AddSpreadReductionPercent(float percent)
    {
        float reduction = Mathf.Clamp01(percent * 0.01f);
        MultiplySpread(1f - reduction);
    }

    public void AddProjectileCount(int amount)
    {
        projectileCountBonus += amount;
    }

    public void AddPierceCount(int amount)
    {
        pierceBonus += amount;
    }

    public void AddPierceDamageFalloffRemoval(int amount)
    {
        removePierceDamageFalloffCount = Mathf.Max(0, removePierceDamageFalloffCount + amount);
    }

    public void AddHomingAngle(float amount)
    {
        homingAngleBonus += amount;
    }

    public void AddHomingRange(float amount)
    {
        homingRangeBonus += amount;
    }

    public void AddMachineGunTerminalGuidance(int amount)
    {
        machineGunTerminalGuidanceCount = Mathf.Max(
            0,
            machineGunTerminalGuidanceCount + amount
        );
    }

    public void AddShotgunCloseRangeDamagePercent(float percent)
    {
        shotgunCloseRangeDamagePercent = Mathf.Max(0f, shotgunCloseRangeDamagePercent + percent);
    }

    public void AddChargeSpeedPercent(float percent)
    {
        MultiplyChargeSpeed(PercentToMultiplier(percent));
    }

    public void AddChargeDamagePercent(float percent)
    {
        MultiplyChargeDamage(PercentToMultiplier(percent));
    }

    public void AddSniperSemiAutoMode(int amount)
    {
        sniperSemiAutoUnlockCount = Mathf.Max(0, sniperSemiAutoUnlockCount + amount);
    }

    public void MultiplyDamage(float multiplier)
    {
        damageMultiplier *= SanitizeMultiplier(multiplier);
    }

    public void MultiplyProjectileSpeed(float multiplier)
    {
        projectileSpeedMultiplier *= SanitizeMultiplier(multiplier);
    }

    public void MultiplyRange(float multiplier)
    {
        rangeMultiplier *= SanitizeMultiplier(multiplier);
    }

    public void MultiplyFireInterval(float multiplier)
    {
        fireIntervalMultiplier *= SanitizeMultiplier(multiplier);
    }

    public void MultiplyFireRate(float rateMultiplier)
    {
        fireIntervalMultiplier /= SanitizeMultiplier(rateMultiplier);
    }

    public void MultiplySpread(float multiplier)
    {
        spreadMultiplier *= Mathf.Clamp(multiplier, 0.01f, 10f);
    }

    public void MultiplyChargeSpeed(float speedMultiplier)
    {
        chargeTimeMultiplier /= SanitizeMultiplier(speedMultiplier);
    }

    public void MultiplyChargeDamage(float multiplier)
    {
        chargeDamageMultiplier *= SanitizeMultiplier(multiplier);
    }

    private float PercentToMultiplier(float percent)
    {
        return Mathf.Max(0.05f, 1f + percent * 0.01f);
    }

    private float SanitizeMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        {
            return 1f;
        }

        return Mathf.Max(0.05f, multiplier);
    }
}
