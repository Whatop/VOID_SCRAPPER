using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunContext
{
    [SerializeField] private bool isActive;
    [SerializeField] private WeaponTreeType selectedWeaponTree;
    [SerializeField] private string selectedShipId = "basic_ship";
    [SerializeField] private ExpeditionDepth expeditionDepth;
    [SerializeField] private RunWallet wallet = new RunWallet();

    [SerializeField] private int currentLevel = 1;
    [SerializeField] private bool bossDefeated;
    [SerializeField] private bool shopHostileThisRun;
    [SerializeField] private List<string> selectedTraitIds = new List<string>();

    public bool IsActive => isActive;
    public WeaponTreeType SelectedWeaponTree => selectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? "basic_ship" : selectedShipId;
    public ExpeditionDepth ExpeditionDepth => expeditionDepth;
    public RunWallet Wallet => wallet;

    public int CurrentLevel => currentLevel;
    public bool BossDefeated => bossDefeated;
    public bool ShopHostileThisRun => shopHostileThisRun;
    public IReadOnlyList<string> SelectedTraitIds => selectedTraitIds;

    public RunContext()
    {
    }

    public RunContext(WeaponTreeType weaponTreeType, ExpeditionDepth depth)
    {
        Begin(weaponTreeType, depth, "basic_ship");
    }

    public RunContext(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId)
    {
        Begin(weaponTreeType, depth, shipId);
    }

    public void Begin(WeaponTreeType weaponTreeType, ExpeditionDepth depth)
    {
        Begin(weaponTreeType, depth, "basic_ship");
    }

    public void Begin(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId)
    {
        isActive = true;
        selectedWeaponTree = weaponTreeType;
        selectedShipId = string.IsNullOrWhiteSpace(shipId) ? "basic_ship" : shipId;
        expeditionDepth = depth;

        currentLevel = 1;
        bossDefeated = false;
        shopHostileThisRun = false;

        selectedTraitIds.Clear();
        wallet.Clear();
    }

    public void SetDepth(ExpeditionDepth depth)
    {
        expeditionDepth = depth;
    }

    public void SetLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
    }

    public void MarkBossDefeated()
    {
        bossDefeated = true;
    }

    public void SetShopHostile(bool hostile)
    {
        shopHostileThisRun = hostile;
    }

    public void AddTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return;
        }

        if (selectedTraitIds.Contains(traitId))
        {
            return;
        }

        selectedTraitIds.Add(traitId);
    }

    public void End()
    {
        isActive = false;
    }
}

[Serializable]
public class RunResultData
{
    public RunEndReason endReason;
    public WeaponTreeType selectedWeaponTree;
    public string selectedShipId;
    public ExpeditionDepth finalDepth;

    public int runExperience;
    public int remainingCredits;

    public int collectedScrapParts;
    public int collectedCoreShards;

    public int committedScrapParts;
    public int committedCoreShards;

    public int lostScrapParts;
    public int lostCoreShards;
}
