using UnityEngine;

public class PlayerRuntimeBonusState : MonoBehaviour
{
    [Header("Currency Gain")]
    [SerializeField] private float scrapGainMultiplier = 1f;
    [SerializeField] private float creditsGainMultiplier = 1f;

    [Header("Recovery")]
    [SerializeField] private float healEfficiencyMultiplier = 1f;
    [SerializeField] private float repairEfficiencyMultiplier = 1f;

    [Header("Pickup")]
    [SerializeField] private float pickupRangeBonus;

    public float ScrapGainMultiplier => scrapGainMultiplier;
    public float CreditsGainMultiplier => creditsGainMultiplier;
    public float HealEfficiencyMultiplier => healEfficiencyMultiplier;
    public float RepairEfficiencyMultiplier => repairEfficiencyMultiplier;
    public float PickupRangeBonus => pickupRangeBonus;

    public void ResetBonuses()
    {
        scrapGainMultiplier = 1f;
        creditsGainMultiplier = 1f;
        healEfficiencyMultiplier = 1f;
        repairEfficiencyMultiplier = 1f;
        pickupRangeBonus = 0f;
    }

    public void AddScrapGainPercent(float percent)
    {
        scrapGainMultiplier *= PercentToMultiplier(percent);
    }

    public void AddCreditsGainPercent(float percent)
    {
        creditsGainMultiplier *= PercentToMultiplier(percent);
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

    public int ApplyCurrencyGain(CurrencyType currencyType, int baseAmount)
    {
        if (baseAmount <= 0)
        {
            return 0;
        }

        float multiplier = currencyType switch
        {
            CurrencyType.ScrapParts => scrapGainMultiplier,
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