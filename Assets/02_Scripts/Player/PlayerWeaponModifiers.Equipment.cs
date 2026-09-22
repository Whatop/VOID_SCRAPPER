using System;
using UnityEngine;

// Run/permanent appliers share these reversible modifiers. Weapon owners keep all timing state.
public partial class PlayerWeaponModifiers
{
    private float coolingRatePercent;
    private float coolingDelayReduction;
    private float targetDistribution;
    private float twinFeed;
    private float impactDisplacement;
    private float slugCoupler;
    private float breachSequence;
    private float dashDamageReduction;
    private float movingChargeBonus;
    private float chargedWidthPercent;
    private float chargeSightPercent;
    private float reserveChargePercent;

    public event Action DevelopmentEquipmentReset;

    public float MachineGunCoolingMultiplier => 1f + Mathf.Max(0f, coolingRatePercent) * .01f;
    public float MachineGunCoolingDelayReduction => Mathf.Max(0f, coolingDelayReduction);
    public int MachineGunTargetDistributionLevel => Mathf.Clamp(Mathf.RoundToInt(targetDistribution), 0, 3);
    public int MachineGunTwinFeedLevel => Mathf.Clamp(Mathf.RoundToInt(twinFeed), 0, 3);
    public float ShotgunImpactDisplacement => Mathf.Clamp(impactDisplacement, 0f, 1.2f);
    public int ShotgunSlugCouplerLevel => Mathf.Clamp(Mathf.RoundToInt(slugCoupler), 0, 3);
    public int ShotgunBreachSequenceLevel => Mathf.Clamp(Mathf.RoundToInt(breachSequence), 0, 3);
    public float PostDashDamageMultiplier => 1f - Mathf.Clamp(dashDamageReduction * .01f, 0f, .5f);
    public float SniperMovingChargeBonus => Mathf.Clamp(movingChargeBonus * .01f, 0f, 1f);
    public float SniperChargedWidthBonus => Mathf.Clamp(chargedWidthPercent * .01f, 0f, .5f);
    public float SniperChargeSightMultiplier => 1f + Mathf.Clamp(chargeSightPercent * .01f, 0f, .5f);
    public float SniperReserveChargeFraction => Mathf.Clamp(reserveChargePercent * .01f, 0f, .4f);

    public bool TryApplyDevelopmentEffect(TraitEffectType effect, float increment)
    {
        if (float.IsNaN(increment) || float.IsInfinity(increment)) return false;
        switch (effect)
        {
            case TraitEffectType.MachineGunCoolingRatePercent: coolingRatePercent += increment; break;
            case TraitEffectType.MachineGunCoolingDelayReduction: coolingDelayReduction += increment; break;
            case TraitEffectType.MachineGunTargetDistribution: targetDistribution += increment; break;
            case TraitEffectType.MachineGunTwinFeed: twinFeed += increment; break;
            case TraitEffectType.ShotgunImpactDisplacement: impactDisplacement += increment; break;
            case TraitEffectType.ShotgunSlugCoupler: slugCoupler += increment; break;
            case TraitEffectType.ShotgunBreachSequence: breachSequence += increment; break;
            case TraitEffectType.DashDamageReductionPercent: dashDamageReduction += increment; break;
            case TraitEffectType.SniperMovingChargeBonus: movingChargeBonus += increment; break;
            case TraitEffectType.ChargedProjectileSizePercent: chargedWidthPercent += increment; break;
            case TraitEffectType.ChargeSightBonusPercent: chargeSightPercent += increment; break;
            case TraitEffectType.SniperReserveCapacitor: reserveChargePercent += increment; break;
            default: return false;
        }
        if (increment < 0f) DevelopmentEquipmentReset?.Invoke();
        return true;
    }

    private void ResetDevelopmentEquipment()
    {
        coolingRatePercent = coolingDelayReduction = targetDistribution = twinFeed = 0f;
        impactDisplacement = slugCoupler = breachSequence = dashDamageReduction = 0f;
        movingChargeBonus = chargedWidthPercent = chargeSightPercent = reserveChargePercent = 0f;
        DevelopmentEquipmentReset?.Invoke();
    }
}
