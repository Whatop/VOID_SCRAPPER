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
    [SerializeField] private SeaRegionType seaRegionType = SeaRegionType.DenseDebris;
    [SerializeField] private RunWallet wallet = new RunWallet();

    [SerializeField] private int currentLevel = 1;
    [SerializeField] private bool bossDefeated;
    [SerializeField] private bool shopHostileThisRun;
    [SerializeField] private List<string> selectedTraitIds = new List<string>();

    [Header("Reward Progression")]
    [SerializeField] private List<string> completedObjectiveIds = new List<string>();
    [SerializeField] private int objectiveSignalCount;
    [SerializeField] private bool coreSignalRevealed;
    [SerializeField] private int specialContainerRareMissStreak;

    [Header("Runtime Reinforcement")]
    [SerializeField] private string equippedReinforcementId;
    [SerializeField] private int equippedReinforcementCharges;

    [Header("Runtime Cargo")]
    [SerializeField] private int maxCargoCapacity = 100;
    [Range(0f, 1f)]
    [SerializeField] private float emergencyReturnCapacityRatio = 0.7f;
    [SerializeField] private int scrapCargoWeight = 1;
    [SerializeField] private int coreShardCargoWeight = 12;

    public bool IsActive => isActive;
    public WeaponTreeType SelectedWeaponTree => selectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? "basic_ship" : selectedShipId;
    public ExpeditionDepth ExpeditionDepth => expeditionDepth;
    public SeaRegionType SeaRegionType => seaRegionType;
    public string SeaRegionDisplayName => SeaRegionCatalog.GetDisplayName(seaRegionType);
    public RunWallet Wallet => wallet;

    public int CurrentLevel => currentLevel;
    public bool BossDefeated => bossDefeated;
    public bool ShopHostileThisRun => shopHostileThisRun;
    public IReadOnlyList<string> SelectedTraitIds => selectedTraitIds;

    public IReadOnlyList<string> CompletedObjectiveIds => completedObjectiveIds;
    public int ObjectiveSignalCount => Mathf.Max(0, objectiveSignalCount);
    public bool CoreSignalRevealed => coreSignalRevealed;
    public int SpecialContainerRareMissStreak => Mathf.Max(0, specialContainerRareMissStreak);

    public string EquippedReinforcementId => equippedReinforcementId;
    public int EquippedReinforcementCharges => Mathf.Max(0, equippedReinforcementCharges);
    public bool HasEquippedReinforcement => !string.IsNullOrWhiteSpace(equippedReinforcementId);

    public int MaxCargoCapacity => Mathf.Max(1, maxCargoCapacity);
    public float EmergencyReturnCapacityRatio => Mathf.Clamp01(emergencyReturnCapacityRatio);
    public int ScrapCargoWeight => Mathf.Max(1, scrapCargoWeight);
    public int CoreShardCargoWeight => Mathf.Max(1, coreShardCargoWeight);
    public int CurrentCargoLoad => CalculateCargoLoad(wallet != null ? wallet.PendingScrapParts : 0, wallet != null ? wallet.PendingCoreShards : 0);
    public float CargoRatio => MaxCargoCapacity <= 0 ? 0f : Mathf.Clamp01(CurrentCargoLoad / (float)MaxCargoCapacity);

    public RunContext()
    {
    }

    public RunContext(WeaponTreeType weaponTreeType, ExpeditionDepth depth)
    {
        Begin(weaponTreeType, depth, "basic_ship", SeaRegionCatalog.GetRandom());
    }

    public RunContext(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId)
    {
        Begin(weaponTreeType, depth, shipId, SeaRegionCatalog.GetRandom());
    }

    public RunContext(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId, SeaRegionType selectedSeaRegionType)
    {
        Begin(weaponTreeType, depth, shipId, selectedSeaRegionType);
    }

    public void Begin(WeaponTreeType weaponTreeType, ExpeditionDepth depth)
    {
        Begin(weaponTreeType, depth, "basic_ship", SeaRegionCatalog.GetRandom());
    }

    public void Begin(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId)
    {
        Begin(weaponTreeType, depth, shipId, SeaRegionCatalog.GetRandom());
    }

    public void Begin(WeaponTreeType weaponTreeType, ExpeditionDepth depth, string shipId, SeaRegionType selectedSeaRegionType)
    {
        isActive = true;
        selectedWeaponTree = weaponTreeType;
        selectedShipId = string.IsNullOrWhiteSpace(shipId) ? "basic_ship" : shipId;
        expeditionDepth = depth;
        seaRegionType = selectedSeaRegionType;

        currentLevel = 1;
        bossDefeated = false;
        shopHostileThisRun = false;

        selectedTraitIds.Clear();
        ResetExpeditionObjectiveProgress();
        specialContainerRareMissStreak = 0;
        ClearEquippedReinforcement();
        ResetCargoRule();
        wallet.Clear();
    }

    public void SetDepth(ExpeditionDepth depth)
    {
        expeditionDepth = depth;
    }

    public void SetSeaRegion(SeaRegionType selectedSeaRegionType)
    {
        seaRegionType = selectedSeaRegionType;
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

    public bool RemoveTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        return selectedTraitIds.Remove(traitId);
    }

    public void ResetExpeditionObjectiveProgress()
    {
        objectiveSignalCount = 0;
        coreSignalRevealed = false;

        if (completedObjectiveIds == null)
        {
            completedObjectiveIds = new List<string>();
        }
        else
        {
            completedObjectiveIds.Clear();
        }
    }

    public bool RegisterObjectiveSignal(string objectiveId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (completedObjectiveIds == null)
        {
            completedObjectiveIds = new List<string>();
        }

        if (!string.IsNullOrWhiteSpace(objectiveId))
        {
            if (completedObjectiveIds.Contains(objectiveId))
            {
                return false;
            }

            completedObjectiveIds.Add(objectiveId);
        }

        objectiveSignalCount = Mathf.Max(0, objectiveSignalCount + amount);
        return true;
    }

    public void SetCoreSignalRevealed(bool value)
    {
        coreSignalRevealed = value;
    }

    public void RecordSpecialContainerOffer(bool offeredRareOrBetter)
    {
        specialContainerRareMissStreak = offeredRareOrBetter
            ? 0
            : Mathf.Max(0, specialContainerRareMissStreak + 1);
    }

    public void SetEquippedReinforcement(string reinforcementId, int charges)
    {
        if (string.IsNullOrWhiteSpace(reinforcementId))
        {
            ClearEquippedReinforcement();
            return;
        }

        equippedReinforcementId = reinforcementId;
        equippedReinforcementCharges = Mathf.Max(0, charges);
    }

    public void SetEquippedReinforcementCharges(int charges)
    {
        if (string.IsNullOrWhiteSpace(equippedReinforcementId))
        {
            equippedReinforcementCharges = 0;
            return;
        }

        equippedReinforcementCharges = Mathf.Max(0, charges);
    }

    public void ClearEquippedReinforcement()
    {
        equippedReinforcementId = string.Empty;
        equippedReinforcementCharges = 0;
    }

    public void ResetCargoRule()
    {
        maxCargoCapacity = 100;
        emergencyReturnCapacityRatio = 0.7f;
        scrapCargoWeight = 1;
        coreShardCargoWeight = 12;
    }

    public void SetCargoRule(int capacity, float emergencyRatio, int scrapWeight = 1, int coreWeight = 12)
    {
        maxCargoCapacity = Mathf.Max(1, capacity);
        emergencyReturnCapacityRatio = Mathf.Clamp01(emergencyRatio);
        scrapCargoWeight = Mathf.Max(1, scrapWeight);
        coreShardCargoWeight = Mathf.Max(1, coreWeight);
    }

    public int CalculateCargoLoad(int scrapParts, int coreShards)
    {
        return Mathf.Max(0, scrapParts) * ScrapCargoWeight + Mathf.Max(0, coreShards) * CoreShardCargoWeight;
    }

    public int GetCargoWeight(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.ScrapParts => ScrapCargoWeight,
            CurrencyType.CoreShards => CoreShardCargoWeight,
            _ => 0
        };
    }

    public bool UsesCargo(CurrencyType currencyType)
    {
        return GetCargoWeight(currencyType) > 0;
    }

    public int GetFreeCargoCapacity()
    {
        return Mathf.Max(0, MaxCargoCapacity - CurrentCargoLoad);
    }

    public int GetAcceptedAmountByCargo(CurrencyType currencyType, int requestedAmount)
    {
        requestedAmount = Mathf.Max(0, requestedAmount);

        if (requestedAmount <= 0)
        {
            return 0;
        }

        int weight = GetCargoWeight(currencyType);

        if (weight <= 0)
        {
            return requestedAmount;
        }

        int freeCapacity = GetFreeCargoCapacity();
        return Mathf.Clamp(freeCapacity / weight, 0, requestedAmount);
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
    public SeaRegionType finalSeaRegionType;
    public bool bossDefeated;

    public int runExperience;
    public int unusedTuningChips;
    public int objectiveSignalCount;
    public int remainingCredits;

    public int collectedScrapParts;
    public int collectedCoreShards;

    public int committedScrapParts;
    public int committedCoreShards;

    public int lostScrapParts;
    public int lostCoreShards;

    public int maxCargoCapacity;
    public int collectedCargoLoad;
    public int committedCargoLoad;
    public int emergencyReturnCargoLimit;
}
