using System;
using UnityEngine;

[Serializable]
public class RunWallet
{
    [SerializeField] private int experience;
    [SerializeField] private int credits;
    [SerializeField] private int pendingScrapParts;
    [SerializeField] private int pendingCoreShards;
    [SerializeField] private int pendingStabilizedAlloy;
    [SerializeField] private int tuningChips;

    public int Experience => experience;
    public int Credits => credits;
    public int PendingScrapParts => pendingScrapParts;
    public int PendingCoreShards => pendingCoreShards;
    public int PendingStabilizedAlloy => pendingStabilizedAlloy;
    public int TuningChips => tuningChips;

    public event Action Changed;

    public void Clear()
    {
        experience = 0;
        credits = 0;
        pendingScrapParts = 0;
        pendingCoreShards = 0;
        pendingStabilizedAlloy = 0;
        tuningChips = 0;
        Changed?.Invoke();
    }

    public int GetAmount(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => experience,
            CurrencyType.Credits => credits,
            CurrencyType.ScrapParts => pendingScrapParts,
            CurrencyType.CoreShards => pendingCoreShards,
            CurrencyType.StabilizedAlloy => pendingStabilizedAlloy,
            CurrencyType.TuningChips => tuningChips,
            _ => 0
        };
    }

    public void Add(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (type)
        {
            case CurrencyType.Experience:
                experience += amount;
                break;

            case CurrencyType.Credits:
                credits += amount;
                break;

            case CurrencyType.ScrapParts:
                pendingScrapParts += amount;
                break;

            case CurrencyType.CoreShards:
                pendingCoreShards += amount;
                break;

            case CurrencyType.StabilizedAlloy:
                pendingStabilizedAlloy += amount;
                break;

            case CurrencyType.TuningChips:
                tuningChips += amount;
                break;
        }

        Changed?.Invoke();
    }

    public void Add(CurrencyAmount amount)
    {
        Add(amount.CurrencyType, amount.Amount);
    }

    public bool CanSpend(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return GetAmount(type) >= amount;
    }

    public bool TrySpend(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!CanSpend(type, amount))
        {
            return false;
        }

        switch (type)
        {
            case CurrencyType.Experience:
                experience -= amount;
                break;

            case CurrencyType.Credits:
                credits -= amount;
                break;

            case CurrencyType.ScrapParts:
                pendingScrapParts -= amount;
                break;

            case CurrencyType.CoreShards:
                pendingCoreShards -= amount;
                break;

            case CurrencyType.StabilizedAlloy:
                pendingStabilizedAlloy -= amount;
                break;

            case CurrencyType.TuningChips:
                tuningChips -= amount;
                break;
        }

        Changed?.Invoke();
        return true;
    }
}
