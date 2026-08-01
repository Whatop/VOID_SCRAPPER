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
    [SerializeField] private CampaignBossId currentBossId;
    [SerializeField] private List<CampaignBossId> bossesDefeatedThisRun = new List<CampaignBossId>();
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

    [Header("Campaign Boss Passive Runtime")]
    [SerializeField] private int matterReconstructorCargoProgress;
    [SerializeField] private int matterReconstructorArmorStacks;

    public bool IsActive => isActive;
    public WeaponTreeType SelectedWeaponTree => selectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? "basic_ship" : selectedShipId;
    public ExpeditionDepth ExpeditionDepth => expeditionDepth;
    public int RegionIndex => CampaignProgressionCatalog.GetRegionIndex(expeditionDepth);
    public bool IsFinalNetwork => CampaignProgressionCatalog.IsFinalNetwork(expeditionDepth);
    public SeaRegionType SeaRegionType => seaRegionType;
    public string SeaRegionDisplayName => SeaRegionCatalog.GetDisplayName(seaRegionType);
    public RunWallet Wallet => wallet;

    public int CurrentLevel => currentLevel;
    public bool BossDefeated => bossDefeated;
    public CampaignBossId CurrentBossId => currentBossId == CampaignBossId.None
        ? CampaignProgressionCatalog.GetBossId(expeditionDepth)
        : currentBossId;
    public IReadOnlyList<CampaignBossId> BossesDefeatedThisRun => bossesDefeatedThisRun;
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
    public int CurrentCargoLoad => CalculateCargoLoad(
        wallet != null ? wallet.PendingScrapParts : 0,
        wallet != null ? wallet.PendingCoreShards : 0
    );
    public float CargoRatio => MaxCargoCapacity <= 0
        ? 0f
        : Mathf.Clamp01(CurrentCargoLoad / (float)MaxCargoCapacity);
    public int MatterReconstructorCargoProgress => Mathf.Max(0, matterReconstructorCargoProgress);
    public int MatterReconstructorArmorStacks => Mathf.Max(0, matterReconstructorArmorStacks);

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

    public RunContext(
        WeaponTreeType weaponTreeType,
        ExpeditionDepth depth,
        string shipId,
        SeaRegionType selectedSeaRegionType)
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

    public void Begin(
        WeaponTreeType weaponTreeType,
        ExpeditionDepth depth,
        string shipId,
        SeaRegionType selectedSeaRegionType)
    {
        isActive = true;
        selectedWeaponTree = weaponTreeType;
        selectedShipId = string.IsNullOrWhiteSpace(shipId) ? "basic_ship" : shipId;
        expeditionDepth = depth;
        currentBossId = CampaignProgressionCatalog.GetBossId(depth);
        seaRegionType = selectedSeaRegionType;

        currentLevel = 1;
        bossDefeated = false;
        shopHostileThisRun = false;

        bossesDefeatedThisRun.Clear();
        selectedTraitIds.Clear();
        ResetExpeditionObjectiveProgress();
        specialContainerRareMissStreak = 0;
        ClearEquippedReinforcement();
        ResetCargoRule();
        matterReconstructorCargoProgress = 0;
        matterReconstructorArmorStacks = 0;
        wallet.Clear();
    }

    public void PrepareNextRegion(ExpeditionDepth depth, SeaRegionType selectedSeaRegionType)
    {
        expeditionDepth = depth;
        currentBossId = CampaignProgressionCatalog.GetBossId(depth);
        seaRegionType = selectedSeaRegionType;
        bossDefeated = false;
        ResetExpeditionObjectiveProgress();
    }

    public void SetDepth(ExpeditionDepth depth)
    {
        expeditionDepth = depth;
        currentBossId = CampaignProgressionCatalog.GetBossId(depth);
        bossDefeated = false;
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
        MarkBossDefeated(CurrentBossId);
    }

    public void MarkBossDefeated(CampaignBossId bossId)
    {
        bossDefeated = true;

        if (bossId == CampaignBossId.None)
        {
            bossId = CampaignProgressionCatalog.GetBossId(expeditionDepth);
        }

        currentBossId = bossId;

        if (bossId != CampaignBossId.None && !bossesDefeatedThisRun.Contains(bossId))
        {
            bossesDefeatedThisRun.Add(bossId);
        }
    }

    public bool HasDefeatedBossThisRun(CampaignBossId bossId)
    {
        return bossId != CampaignBossId.None && bossesDefeatedThisRun.Contains(bossId);
    }

    public void SetShopHostile(bool hostile)
    {
        shopHostileThisRun = hostile;
    }

    public void AddTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId) || selectedTraitIds.Contains(traitId))
        {
            return;
        }

        selectedTraitIds.Add(traitId);
    }

    public bool RemoveTrait(string traitId)
    {
        return !string.IsNullOrWhiteSpace(traitId) && selectedTraitIds.Remove(traitId);
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
        return Mathf.Max(0, scrapParts) * ScrapCargoWeight +
               Mathf.Max(0, coreShards) * CoreShardCargoWeight;
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

    public int AddMatterReconstructorCargoProgress(
        int cargoDelta,
        int cargoThreshold,
        int maximumArmorStacks)
    {
        cargoDelta = Mathf.Max(0, cargoDelta);
        cargoThreshold = Mathf.Max(1, cargoThreshold);
        maximumArmorStacks = Mathf.Max(0, maximumArmorStacks);

        if (cargoDelta <= 0 || matterReconstructorArmorStacks >= maximumArmorStacks)
        {
            return 0;
        }

        matterReconstructorCargoProgress += cargoDelta;
        int grantedStacks = 0;

        while (matterReconstructorCargoProgress >= cargoThreshold &&
               matterReconstructorArmorStacks < maximumArmorStacks)
        {
            matterReconstructorCargoProgress -= cargoThreshold;
            matterReconstructorArmorStacks++;
            grantedStacks++;
        }

        if (matterReconstructorArmorStacks >= maximumArmorStacks)
        {
            matterReconstructorCargoProgress = Mathf.Min(
                matterReconstructorCargoProgress,
                cargoThreshold - 1
            );
        }

        return grantedStacks;
    }

    public void ClampMatterReconstructorState(int maximumArmorStacks)
    {
        maximumArmorStacks = Mathf.Max(0, maximumArmorStacks);
        matterReconstructorArmorStacks = Mathf.Clamp(
            matterReconstructorArmorStacks,
            0,
            maximumArmorStacks
        );

        if (maximumArmorStacks <= 0)
        {
            matterReconstructorCargoProgress = 0;
        }
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
    public CampaignBossId finalBossId;
    public int bossesDefeatedThisRun;
    public bool bossDefeated;
    public bool finalVictory;

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
