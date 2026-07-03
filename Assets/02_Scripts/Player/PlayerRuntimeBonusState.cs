using UnityEngine;

public class PlayerRuntimeBonusState : MonoBehaviour
{
    [Header("Currency Gain")]
    [SerializeField] private float scrapGainMultiplier = 1f;
    [SerializeField] private float creditsGainMultiplier = 1f;
    [SerializeField] private float harvestYieldMultiplier = 1f;

    [Header("Recovery")]
    [SerializeField] private float healEfficiencyMultiplier = 1f;
    [SerializeField] private float repairEfficiencyMultiplier = 1f;

    [Header("Pickup")]
    [SerializeField] private float pickupRangeBonus;

    [Header("Cargo")]
    [SerializeField] private int cargoCapacityBonus;
    [SerializeField] private float emergencyReturnCapacityRatioBonus;

    [Header("Harvest Combat")]
    [SerializeField] private float harvestObjectDamageMultiplier = 1f;

    [Header("Utility")]
    [SerializeField] private float radarScanRadiusBonus;
    [SerializeField] private float activeCooldownMultiplier = 1f;
    [SerializeField] private float radarTauntDurationBonus;
    [SerializeField] private float radarStealthDurationBonus;

    public float ScrapGainMultiplier => scrapGainMultiplier;
    public float CreditsGainMultiplier => creditsGainMultiplier;
    public float HarvestYieldMultiplier => harvestYieldMultiplier;
    public float HealEfficiencyMultiplier => healEfficiencyMultiplier;
    public float RepairEfficiencyMultiplier => repairEfficiencyMultiplier;
    public float PickupRangeBonus => pickupRangeBonus;
    public int CargoCapacityBonus => cargoCapacityBonus;
    public float EmergencyReturnCapacityRatioBonus => emergencyReturnCapacityRatioBonus;
    public float HarvestObjectDamageMultiplier => harvestObjectDamageMultiplier;
    public float RadarScanRadiusBonus => radarScanRadiusBonus;
    public float ActiveCooldownMultiplier => activeCooldownMultiplier;
    public float RadarTauntDurationBonus => radarTauntDurationBonus;
    public float RadarStealthDurationBonus => radarStealthDurationBonus;

    public void ResetBonuses()
    {
        scrapGainMultiplier = 1f;
        creditsGainMultiplier = 1f;
        harvestYieldMultiplier = 1f;
        healEfficiencyMultiplier = 1f;
        repairEfficiencyMultiplier = 1f;
        pickupRangeBonus = 0f;
        cargoCapacityBonus = 0;
        emergencyReturnCapacityRatioBonus = 0f;
        harvestObjectDamageMultiplier = 1f;
        radarScanRadiusBonus = 0f;
        activeCooldownMultiplier = 1f;
        radarTauntDurationBonus = 0f;
        radarStealthDurationBonus = 0f;
    }

    public void AddScrapGainPercent(float percent)
    {
        scrapGainMultiplier *= PercentToMultiplier(percent);
    }

    public void AddCreditsGainPercent(float percent)
    {
        creditsGainMultiplier *= PercentToMultiplier(percent);
    }

    public void AddHarvestYieldPercent(float percent)
    {
        harvestYieldMultiplier *= PercentToMultiplier(percent);
    }

    public void AddHealEfficiencyPercent(float percent)
    {
        healEfficiencyMultiplier *= PercentToMultiplier(percent);
    }

    public void AddRepairEfficiencyPercent(float percent)
    {
        repairEfficiencyMultiplier *= PercentToMultiplier(percent);
    }

    public void AddPickupRangeBonus(float amount)
    {
        pickupRangeBonus = Mathf.Max(0f, pickupRangeBonus + amount);
    }

    public void AddCargoCapacityBonus(float amount)
    {
        cargoCapacityBonus += Mathf.RoundToInt(amount);
    }

    public void AddEmergencyReturnCapacityRatioBonus(float percent)
    {
        emergencyReturnCapacityRatioBonus += percent * 0.01f;
    }

    public void AddHarvestObjectDamagePercent(float percent)
    {
        harvestObjectDamageMultiplier *= PercentToMultiplier(percent);
    }

    public void AddRadarScanRadiusBonus(float amount)
    {
        radarScanRadiusBonus += amount;
    }

    public void AddActiveCooldownReductionPercent(float percent)
    {
        float reduction = Mathf.Clamp01(Mathf.Abs(percent) * 0.01f);
        activeCooldownMultiplier *= Mathf.Clamp(1f - reduction, 0.05f, 1f);
    }

    public void AddRadarTauntDurationBonus(float amount)
    {
        radarTauntDurationBonus += amount;
    }

    public void AddRadarStealthDurationBonus(float amount)
    {
        radarStealthDurationBonus += amount;
    }

    public int ApplyCurrencyGain(CurrencyType currencyType, int baseAmount)
    {
        if (baseAmount <= 0)
        {
            return 0;
        }

        float multiplier = currencyType switch
        {
            CurrencyType.ScrapParts => scrapGainMultiplier * harvestYieldMultiplier,
            CurrencyType.CoreShards => harvestYieldMultiplier,
            CurrencyType.Credits => creditsGainMultiplier,
            _ => 1f
        };

        return Mathf.Max(0, Mathf.RoundToInt(baseAmount * multiplier));
    }

    public float ApplyHealAmount(float baseHealAmount)
    {
        if (baseHealAmount <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, baseHealAmount * healEfficiencyMultiplier);
    }

    public float ApplyRepairAmount(float baseRepairAmount)
    {
        if (baseRepairAmount <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, baseRepairAmount * repairEfficiencyMultiplier);
    }

    private float PercentToMultiplier(float percent)
    {
        return Mathf.Max(0f, 1f + (percent * 0.01f));
    }
}
