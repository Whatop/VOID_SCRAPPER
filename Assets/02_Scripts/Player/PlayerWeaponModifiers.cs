using UnityEngine;

public class PlayerWeaponModifiers : MonoBehaviour
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
    [SerializeField] private float homingAngleBonus;
    [SerializeField] private float homingRangeBonus;

    [Header("Sniper")]
    [SerializeField] private float chargeTimeMultiplier = 1f;
    [SerializeField] private float chargeDamageMultiplier = 1f;

    public float DamageMultiplier => damageMultiplier;
    public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
    public float RangeMultiplier => rangeMultiplier;
    public float FireIntervalMultiplier => fireIntervalMultiplier;
    public float SpreadMultiplier => spreadMultiplier;

    public int ProjectileCountBonus => projectileCountBonus;
    public int PierceBonus => pierceBonus;
    public float HomingAngleBonus => homingAngleBonus;
    public float HomingRangeBonus => homingRangeBonus;

    public float ChargeTimeMultiplier => chargeTimeMultiplier;
    public float ChargeDamageMultiplier => chargeDamageMultiplier;

    public void ResetModifiers()
    {
        damageMultiplier = 1f;
        projectileSpeedMultiplier = 1f;
        rangeMultiplier = 1f;
        fireIntervalMultiplier = 1f;
        spreadMultiplier = 1f;

        projectileCountBonus = 0;
        pierceBonus = 0;
        homingAngleBonus = 0f;
        homingRangeBonus = 0f;

        chargeTimeMultiplier = 1f;
        chargeDamageMultiplier = 1f;
    }

    public void AddDamagePercent(float percent)
    {
        damageMultiplier *= 1f + (percent * 0.01f);
    }

    public void AddProjectileSpeedPercent(float percent)
    {
        projectileSpeedMultiplier *= 1f + (percent * 0.01f);
    }

    public void AddRangePercent(float percent)
    {
        rangeMultiplier *= 1f + (percent * 0.01f);
    }

    public void AddFireRatePercent(float percent)
    {
        float rateMultiplier = 1f + (percent * 0.01f);
        rateMultiplier = Mathf.Max(0.05f, rateMultiplier);

        fireIntervalMultiplier /= rateMultiplier;
    }

    public void AddSpreadReductionPercent(float percent)
    {
        float reduction = Mathf.Clamp01(percent * 0.01f);
        spreadMultiplier *= 1f - reduction;
    }

    public void AddProjectileCount(int amount)
    {
        projectileCountBonus += amount;
    }

    public void AddPierceCount(int amount)
    {
        pierceBonus += amount;
    }

    public void AddHomingAngle(float amount)
    {
        homingAngleBonus += amount;
    }

    public void AddHomingRange(float amount)
    {
        homingRangeBonus += amount;
    }

    public void AddChargeSpeedPercent(float percent)
    {
        float speedMultiplier = 1f + (percent * 0.01f);
        speedMultiplier = Mathf.Max(0.05f, speedMultiplier);

        chargeTimeMultiplier /= speedMultiplier;
    }

    public void AddChargeDamagePercent(float percent)
    {
        chargeDamageMultiplier *= 1f + (percent * 0.01f);
    }
}