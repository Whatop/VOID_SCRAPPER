using UnityEngine;

public partial class ShotgunWeapon
{
    private PlayerDash equipmentDash;
    private int observedDashSerial;
    private float breachArmedUntil = float.NegativeInfinity;
    private bool firingSlug;
    private bool firingImpactCarrier;

    public bool HasBreachOpportunity => Time.time <= breachArmedUntil && weaponModifiers != null && weaponModifiers.ShotgunBreachSequenceLevel > 0;
    public float NextFireTime => nextFireTime;
    public float SlugSpeedMultiplier
    {
        get
        {
            int level = weaponModifiers != null ? weaponModifiers.ShotgunSlugCouplerLevel : 0;
            return level <= 0 ? 1f : level == 1 ? 1.05f : level == 2 ? 1.15f : 1.2f;
        }
    }

    public override void ForceCancel()
    {
        breachArmedUntil = float.NegativeInfinity;
        observedDashSerial = equipmentDash != null ? equipmentDash.CompletedDashSerial : 0;
        equipmentDash?.ClearEquipmentDashWindow();
        firingSlug = firingImpactCarrier = false;
    }

    public override void OnUnequip() => ForceCancel();
    private void OnDisable() => ForceCancel();

    private void RefreshBreachOpportunity()
    {
        int level = weaponModifiers != null ? weaponModifiers.ShotgunBreachSequenceLevel : 0;
        if (equipmentDash == null) return;
        int serial = equipmentDash.CompletedDashSerial;
        if (serial != observedDashSerial)
        {
            observedDashSerial = serial;
            if (level > 0 && equipmentDash.LastCompletedDashWeapon == WeaponTreeType.Shotgun)
                breachArmedUntil = equipmentDash.LastCompletedDashTime + (level >= 2 ? 1.25f : .8f);
        }
        if (level <= 0 || float.IsNegativeInfinity(equipmentDash.LastCompletedDashTime))
            breachArmedUntil = float.NegativeInfinity;
    }
}
