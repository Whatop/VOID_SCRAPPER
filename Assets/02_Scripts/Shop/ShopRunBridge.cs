using UnityEngine;

public static class ShopRunBridge
{
    public static bool HasActiveRun()
    {
        return RunManager.Instance != null && RunManager.Instance.HasActiveRun;
    }

    public static bool TryGetCurrentRun(out RunContext runContext)
    {
        runContext = null;

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        runContext = RunManager.Instance.CurrentRun;
        return runContext != null;
    }

    public static bool TryGetCredits(out int credits)
    {
        credits = 0;

        if (!TryGetCurrentRun(out RunContext runContext) || runContext.Wallet == null)
        {
            return false;
        }

        credits = runContext.Wallet.Credits;
        return true;
    }

    public static bool CanSpendCredits(int amount)
    {
        amount = Mathf.Max(0, amount);

        if (amount <= 0)
        {
            return true;
        }

        return TryGetCredits(out int credits) && credits >= amount;
    }

    public static bool TrySpendCredits(int amount)
    {
        amount = Mathf.Max(0, amount);

        if (amount <= 0)
        {
            return true;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        return RunManager.Instance.TrySpendCredits(amount);
    }

    public static bool AddCredits(int amount)
    {
        return AddCurrency(CurrencyType.Credits, amount);
    }

    public static bool AddScrapParts(int amount)
    {
        return AddCurrency(CurrencyType.ScrapParts, amount);
    }

    public static bool AddCoreShards(int amount)
    {
        return AddCurrency(CurrencyType.CoreShards, amount);
    }

    public static bool AddCurrency(CurrencyType currencyType, int amount)
    {
        if (amount <= 0)
        {
            return amount == 0;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            Debug.LogWarning($"Ȱȭ Ž簡  ȭ  ߽ϴ. Type: {currencyType}, Amount: {amount}");
            return false;
        }

        RunManager.Instance.AddCurrency(currencyType, amount);
        return true;
    }

    public static bool HasRunTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        if (!TryGetCurrentRun(out RunContext runContext) || runContext.SelectedTraitIds == null)
        {
            return false;
        }

        for (int i = 0; i < runContext.SelectedTraitIds.Count; i++)
        {
            if (runContext.SelectedTraitIds[i] == traitId)
            {
                return true;
            }
        }

        return false;
    }

    public static bool AddRunTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        if (!TryGetCurrentRun(out RunContext runContext))
        {
            return false;
        }

        if (HasRunTrait(traitId))
        {
            return false;
        }

        runContext.AddTrait(traitId);
        return true;
    }

    public static bool RemoveRunTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        if (!TryGetCurrentRun(out RunContext runContext))
        {
            return false;
        }

        return runContext.RemoveTrait(traitId);
    }

    public static bool SetShopHostileThisRun(bool hostile = true)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        RunManager.Instance.SetShopHostileThisRun(hostile);
        return true;
    }

    public static bool IsShopHostileThisRun()
    {
        return TryGetCurrentRun(out RunContext runContext) && runContext.ShopHostileThisRun;
    }

    public static WeaponTreeType GetSelectedWeaponTree(WeaponTreeType fallback)
    {
        if (TryGetCurrentRun(out RunContext runContext))
        {
            return runContext.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return fallback;
    }

    public static bool HasEquippedReinforcement(string reinforcementId)
    {
        if (string.IsNullOrWhiteSpace(reinforcementId))
        {
            return false;
        }

        return TryGetCurrentRun(out RunContext runContext) &&
               runContext.HasEquippedReinforcement &&
               runContext.EquippedReinforcementId == reinforcementId;
    }

    public static bool TryGetEquippedReinforcement(out string reinforcementId, out int charges)
    {
        reinforcementId = string.Empty;
        charges = 0;

        if (!TryGetCurrentRun(out RunContext runContext) || !runContext.HasEquippedReinforcement)
        {
            return false;
        }

        reinforcementId = runContext.EquippedReinforcementId;
        charges = runContext.EquippedReinforcementCharges;
        return !string.IsNullOrWhiteSpace(reinforcementId);
    }

    public static bool SetEquippedReinforcement(string reinforcementId, int charges)
    {
        if (!TryGetCurrentRun(out RunContext runContext))
        {
            return false;
        }

        runContext.SetEquippedReinforcement(reinforcementId, charges);
        return true;
    }

    public static bool SetEquippedReinforcementCharges(int charges)
    {
        if (!TryGetCurrentRun(out RunContext runContext))
        {
            return false;
        }

        runContext.SetEquippedReinforcementCharges(charges);
        return true;
    }

    public static bool ClearEquippedReinforcement()
    {
        if (!TryGetCurrentRun(out RunContext runContext))
        {
            return false;
        }

        runContext.ClearEquippedReinforcement();
        return true;
    }
}