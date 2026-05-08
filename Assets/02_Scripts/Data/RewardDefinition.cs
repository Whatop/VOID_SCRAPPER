using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CurrencyRange
{
    public CurrencyType currencyType;
    public int minAmount;
    public int maxAmount;

    public CurrencyAmount Roll()
    {
        int min = Mathf.Min(minAmount, maxAmount);
        int max = Mathf.Max(minAmount, maxAmount);
        int amount = UnityEngine.Random.Range(min, max + 1);

        return new CurrencyAmount(currencyType, amount);
    }
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Rewards/Reward Definition")]
public class RewardDefinition : ScriptableObject
{
    [Header("Fixed Rewards")]
    [SerializeField] private List<CurrencyAmount> fixedRewards = new List<CurrencyAmount>();

    [Header("Random Rewards")]
    [SerializeField] private List<CurrencyRange> randomRewards = new List<CurrencyRange>();

    [Header("Heal Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float healDropChance;
    [SerializeField] private int healAmount = 1;

    public IReadOnlyList<CurrencyAmount> FixedRewards => fixedRewards;
    public IReadOnlyList<CurrencyRange> RandomRewards => randomRewards;
    public float HealDropChance => healDropChance;
    public int HealAmount => healAmount;

    public List<CurrencyAmount> RollCurrencies()
    {
        List<CurrencyAmount> result = new List<CurrencyAmount>();

        foreach (CurrencyAmount reward in fixedRewards)
        {
            if (reward.IsValid())
            {
                result.Add(reward);
            }
        }

        foreach (CurrencyRange range in randomRewards)
        {
            CurrencyAmount rolled = range.Roll();
            if (rolled.IsValid())
            {
                result.Add(rolled);
            }
        }

        return result;
    }

    public bool RollHealDrop()
    {
        if (healDropChance <= 0f)
        {
            return false;
        }

        return UnityEngine.Random.value <= healDropChance;
    }
}