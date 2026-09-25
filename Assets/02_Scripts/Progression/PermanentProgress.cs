using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuildingLevelState
{
    public BuildingType buildingType;
    public int level;

    public BuildingLevelState(BuildingType buildingType, int level)
    {
        this.buildingType = buildingType;
        this.level = Mathf.Max(0, level);
    }
}

[Serializable]
public class TraitLevelState
{
    public string traitId;
    public int level;

    public TraitLevelState(string traitId, int level)
    {
        this.traitId = traitId;
        this.level = Mathf.Max(0, level);
    }
}

[Serializable]
public class SectorTechnologyLevelState
{
    public string technologyId;
    public int level;

    public SectorTechnologyLevelState(string technologyId, int level)
    {
        this.technologyId = technologyId;
        this.level = Mathf.Max(0, level);
    }
}

public partial class PermanentProgress : MonoBehaviour, IMainDamagedAccessKeyQuestReadAuthority
{
    private const string PersistentStoryTraitUnlockPrefix = "story_trait:";
    private const string TutorialCompletedUnlockFlag = "tutorial_completed";

    public static PermanentProgress Instance { get; private set; }

    [Header("Permanent Currency")]
    [SerializeField] private int scrapParts;
    [SerializeField] private int coreShards;
    [SerializeField] private int stabilizedAlloy;

    [Header("Permanent Stats")]
    [SerializeField] private int totalRunCount;
    [SerializeField] private int safeReturnCount;
    [SerializeField] private int emergencyReturnCount;
    [SerializeField] private int deathCount;
    [SerializeField] private int bossDefeatCount;
    [SerializeField] private int totalCollectedScrapParts;
    [SerializeField] private int totalCollectedCoreShards;
    [SerializeField] private int totalCollectedStabilizedAlloy;
    [SerializeField] private int totalCommittedScrapParts;
    [SerializeField] private int totalCommittedCoreShards;
    [SerializeField] private int totalCommittedStabilizedAlloy;

    [Header("Selection")]
    [SerializeField] private WeaponTreeType lastSelectedWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string selectedShipId = "basic_ship";

    [Header("Building Levels")]
    [SerializeField] private List<BuildingLevelState> buildingLevels = new List<BuildingLevelState>();

    [Header("Permanent Trait Levels")]
    [SerializeField] private List<TraitLevelState> traitLevels = new List<TraitLevelState>();

    [Header("Sector Technology Levels")]
    [SerializeField] private List<SectorTechnologyLevelState> sectorTechnologyLevels = new List<SectorTechnologyLevelState>();

    [Header("Disabled Permanent Traits")]
    [SerializeField] private List<string> disabledPermanentTraitIds = new List<string>();

    [Header("Unlock Flags")]
    [SerializeField] private List<string> unlockFlags = new List<string>();

    [Header("Campaign Progression")]
    [SerializeField] private List<CampaignBossId> defeatedCampaignBosses = new List<CampaignBossId>();
    [SerializeField] private List<BossStoryPart> acquiredBossStoryParts = new List<BossStoryPart>();
    [SerializeField] private ExpeditionDepth highestUnlockedDepth = ExpeditionDepth.Normal;
    [SerializeField] private RouteCoreState routeCoreState = RouteCoreState.MissingParts;
    [SerializeField] private bool settlementDefenseCleared;
    [SerializeField] private bool finalBossDefeated;

    public int ScrapParts => scrapParts;
    public int CoreShards => coreShards;
    public int StabilizedAlloy => stabilizedAlloy;
    public int TotalRunCount => totalRunCount;
    public int SafeReturnCount => safeReturnCount;
    public int EmergencyReturnCount => emergencyReturnCount;
    public int DeathCount => deathCount;
    public int BossDefeatCount => bossDefeatCount;
    public int TotalCollectedScrapParts => totalCollectedScrapParts;
    public int TotalCollectedCoreShards => totalCollectedCoreShards;
    public int TotalCollectedStabilizedAlloy => totalCollectedStabilizedAlloy;
    public int TotalCommittedScrapParts => totalCommittedScrapParts;
    public int TotalCommittedCoreShards => totalCommittedCoreShards;
    public int TotalCommittedStabilizedAlloy => totalCommittedStabilizedAlloy;
    public WeaponTreeType LastSelectedWeaponTree => lastSelectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? "basic_ship" : selectedShipId;

    public IReadOnlyList<CampaignBossId> DefeatedCampaignBosses => defeatedCampaignBosses;
    public IReadOnlyList<BossStoryPart> AcquiredBossStoryParts => acquiredBossStoryParts;
    public ExpeditionDepth HighestUnlockedDepth => highestUnlockedDepth;
    public RouteCoreState CurrentRouteCoreState => ResolveRouteCoreState();
    public bool SettlementDefenseCleared => settlementDefenseCleared;
    public bool FinalBossDefeated => finalBossDefeated;
    public int AcquiredBossStoryPartCount => CountRequiredStoryParts();
    public bool HasAllRouteCoreParts =>
        HasBossStoryPart(BossStoryPart.SectorStabilizer) &&
        HasBossStoryPart(BossStoryPart.MatterCompressor) &&
        HasBossStoryPart(BossStoryPart.PhaseNavigationLens);
    public bool CanAssembleRouteCore => HasAllRouteCoreParts && CurrentRouteCoreState == RouteCoreState.ReadyToAssemble;
    public bool CanActivateRouteCore => CurrentRouteCoreState == RouteCoreState.Assembled;
    public bool CanLaunchFinalExpedition => CurrentRouteCoreState == RouteCoreState.Activated && settlementDefenseCleared;
    public bool IsTutorialCompleted => HasUnlockFlag(TutorialCompletedUnlockFlag);
    public MainDamagedAccessKeyQuestState DamagedAccessKeyQuestState =>
        ResolveDamagedAccessKeyQuestState();
    public int DamagedAccessKeyCollectedPartCount => AcquiredBossStoryPartCount;
    public int DamagedAccessKeyRequiredPartCount =>
        MainDamagedAccessKeyQuestIds.RequiredPartCount;
    public int PixelCurseLevel => ResolvePixelCurseLevel();

    public int GetCoreSynchronizationStage()
    {
        int stage = IsTutorialCompleted ? 1 : 0;
        stage += AcquiredBossStoryPartCount;

        RouteCoreState currentState = CurrentRouteCoreState;
        if (currentState == RouteCoreState.Assembled || currentState == RouteCoreState.Activated)
        {
            stage++;
        }

        return Mathf.Clamp(stage, 0, 5);
    }

    public event Action Changed;
    public event Action<int, int> PixelCurseLevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PermanentProgress가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
        EnsureDefaultBuildings();
    }

    public void LoadFromSave(SaveData saveData)
    {
        int previousPixelCurseLevel = PixelCurseLevel;

        if (saveData == null)
        {
            ResetProgress();
            return;
        }

        scrapParts = Mathf.Max(0, saveData.scrapParts);
        coreShards = Mathf.Max(0, saveData.coreShards);
        stabilizedAlloy = Mathf.Max(0, saveData.stabilizedAlloy);
        totalRunCount = Mathf.Max(0, saveData.totalRunCount);
        safeReturnCount = Mathf.Max(0, saveData.safeReturnCount);
        emergencyReturnCount = Mathf.Max(0, saveData.emergencyReturnCount);
        deathCount = Mathf.Max(0, saveData.deathCount);
        bossDefeatCount = Mathf.Max(0, saveData.bossDefeatCount);
        totalCollectedScrapParts = Mathf.Max(0, saveData.totalCollectedScrapParts);
        totalCollectedCoreShards = Mathf.Max(0, saveData.totalCollectedCoreShards);
        totalCollectedStabilizedAlloy = Mathf.Max(0, saveData.totalCollectedStabilizedAlloy);
        totalCommittedScrapParts = Mathf.Max(0, saveData.totalCommittedScrapParts);
        totalCommittedCoreShards = Mathf.Max(0, saveData.totalCommittedCoreShards);
        totalCommittedStabilizedAlloy = Mathf.Max(0, saveData.totalCommittedStabilizedAlloy);
        lastSelectedWeaponTree = saveData.lastSelectedWeaponTree;
        selectedShipId = string.IsNullOrWhiteSpace(saveData.selectedShipId) ? "basic_ship" : saveData.selectedShipId;

        buildingLevels.Clear();
        if (saveData.buildingLevels != null)
        {
            foreach (BuildingSaveData data in saveData.buildingLevels)
            {
                buildingLevels.Add(new BuildingLevelState(data.buildingType, data.level));
            }
        }

        traitLevels.Clear();
        if (saveData.traitLevels != null)
        {
            foreach (TraitLevelSaveData data in saveData.traitLevels)
            {
                if (!string.IsNullOrWhiteSpace(data.traitId))
                {
                    traitLevels.Add(new TraitLevelState(data.traitId, data.level));
                }
            }
        }

        sectorTechnologyLevels.Clear();
        if (saveData.sectorTechnologyLevels != null)
        {
            for (int i = 0; i < saveData.sectorTechnologyLevels.Count; i++)
            {
                SectorTechnologyLevelSaveData data = saveData.sectorTechnologyLevels[i];
                if (data != null && SectorTechnologyCatalog.TryGet(data.technologyId, out SectorTechnologyDefinition definition))
                {
                    sectorTechnologyLevels.Add(new SectorTechnologyLevelState(
                        definition.Id,
                        Mathf.Clamp(data.level, 0, definition.MaxLevel)
                    ));
                }
            }
        }

        disabledPermanentTraitIds.Clear();
        if (saveData.disabledPermanentTraitIds != null)
        {
            foreach (string traitId in saveData.disabledPermanentTraitIds)
            {
                AddUniqueString(disabledPermanentTraitIds, traitId);
            }
        }

        unlockFlags.Clear();
        if (saveData.unlockFlags != null)
        {
            foreach (string flag in saveData.unlockFlags)
            {
                AddUniqueString(unlockFlags, flag);
            }
        }

        defeatedCampaignBosses.Clear();
        if (saveData.defeatedCampaignBosses != null)
        {
            foreach (CampaignBossId bossId in saveData.defeatedCampaignBosses)
            {
                AddUniqueBossId(defeatedCampaignBosses, bossId);
            }
        }

        acquiredBossStoryParts.Clear();
        if (saveData.acquiredBossStoryParts != null)
        {
            foreach (BossStoryPart storyPart in saveData.acquiredBossStoryParts)
            {
                AddUniqueStoryPart(acquiredBossStoryParts, storyPart);
            }
        }

        highestUnlockedDepth = ClampCampaignDepth(saveData.highestUnlockedDepth);
        routeCoreState = saveData.routeCoreState;
        settlementDefenseCleared = saveData.settlementDefenseCleared;
        finalBossDefeated = saveData.finalBossDefeated;

        saveData.RetireLegacyOperatingFrame();
        LoadEquipmentOwnership(saveData);
        RestoreCampaignProgressFromLegacyFlags();
        // Existing assembled saves already completed the old combined analysis/restoration handoff.
        if (HasAllRouteCoreParts && routeCoreState >= RouteCoreState.Assembled)
            AddUniqueString(unlockFlags, FinalComponentAnalyzedFlag);
        RefreshCampaignDerivedState();
        ValidateEquipmentShipSelection();
        RefreshEquipmentResearch();
        PruneDisabledPermanentTraitIds();
        EnsureDefaultBuildings();
        Changed?.Invoke();
        NotifyPixelCurseLevelChanged(previousPixelCurseLevel);
    }

    public SaveData CreateSaveData()
    {
        SaveData saveData = new SaveData
        {
            scrapParts = scrapParts,
            coreShards = coreShards,
            stabilizedAlloy = stabilizedAlloy,
            totalRunCount = totalRunCount,
            safeReturnCount = safeReturnCount,
            emergencyReturnCount = emergencyReturnCount,
            deathCount = deathCount,
            bossDefeatCount = bossDefeatCount,
            totalCollectedScrapParts = totalCollectedScrapParts,
            totalCollectedCoreShards = totalCollectedCoreShards,
            totalCollectedStabilizedAlloy = totalCollectedStabilizedAlloy,
            totalCommittedScrapParts = totalCommittedScrapParts,
            totalCommittedCoreShards = totalCommittedCoreShards,
            totalCommittedStabilizedAlloy = totalCommittedStabilizedAlloy,
            lastSelectedWeaponTree = lastSelectedWeaponTree,
            selectedShipId = SelectedShipId,
            highestUnlockedDepth = highestUnlockedDepth,
            routeCoreState = ResolveRouteCoreState(),
            settlementDefenseCleared = settlementDefenseCleared,
            finalBossDefeated = finalBossDefeated
        };

        foreach (BuildingLevelState state in buildingLevels)
        {
            saveData.buildingLevels.Add(new BuildingSaveData(state.buildingType, state.level));
        }

        foreach (TraitLevelState state in traitLevels)
        {
            if (!string.IsNullOrWhiteSpace(state.traitId))
            {
                saveData.traitLevels.Add(new TraitLevelSaveData(state.traitId, state.level));
            }
        }

        foreach (SectorTechnologyLevelState state in sectorTechnologyLevels)
        {
            if (!string.IsNullOrWhiteSpace(state.technologyId))
            {
                saveData.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(
                    state.technologyId,
                    state.level
                ));
            }
        }

        foreach (string traitId in disabledPermanentTraitIds)
        {
            if (!string.IsNullOrWhiteSpace(traitId))
            {
                saveData.disabledPermanentTraitIds.Add(traitId);
            }
        }

        saveData.equipmentLoadoutTraitIds.AddRange(equipmentLoadoutTraitIds);
        saveData.manufacturedEquipmentIds.AddRange(manufacturedEquipmentIds);
        saveData.equipmentOwnershipMigrationPending = equipmentOwnershipMigrationPending;
        saveData.grandfatheredEquipmentResearchIds.AddRange(grandfatheredEquipmentResearchIds);
        saveData.equipmentRosterMigrationPending = equipmentRosterMigrationPending;
        saveData.unlockFlags.AddRange(unlockFlags);
        saveData.defeatedCampaignBosses.AddRange(defeatedCampaignBosses);
        saveData.acquiredBossStoryParts.AddRange(acquiredBossStoryParts);
        return saveData;
    }

    public void ResetProgress()
    {
        int previousPixelCurseLevel = PixelCurseLevel;

        scrapParts = 0;
        coreShards = 0;
        stabilizedAlloy = 0;
        totalRunCount = 0;
        safeReturnCount = 0;
        emergencyReturnCount = 0;
        deathCount = 0;
        bossDefeatCount = 0;
        totalCollectedScrapParts = 0;
        totalCollectedCoreShards = 0;
        totalCollectedStabilizedAlloy = 0;
        totalCommittedScrapParts = 0;
        totalCommittedCoreShards = 0;
        totalCommittedStabilizedAlloy = 0;
        lastSelectedWeaponTree = WeaponTreeType.MachineGun;
        selectedShipId = "basic_ship";

        equipmentLoadoutTraitIds.Clear();
        manufacturedEquipmentIds.Clear();
        equipmentOwnershipMigrationPending = false;
        grandfatheredEquipmentResearchIds.Clear();
        equipmentRosterMigrationPending = false;
        buildingLevels.Clear();
        traitLevels.Clear();
        sectorTechnologyLevels.Clear();
        disabledPermanentTraitIds.Clear();
        unlockFlags.Clear();
        defeatedCampaignBosses.Clear();
        acquiredBossStoryParts.Clear();
        highestUnlockedDepth = ExpeditionDepth.Normal;
        routeCoreState = RouteCoreState.MissingParts;
        settlementDefenseCleared = false;
        finalBossDefeated = false;

        EnsureDefaultBuildings();
        Changed?.Invoke();
        NotifyPixelCurseLevelChanged(previousPixelCurseLevel);
    }

    public void SetLastSelectedWeaponTree(WeaponTreeType weaponTreeType)
    {
        if (lastSelectedWeaponTree == weaponTreeType)
        {
            return;
        }

        lastSelectedWeaponTree = weaponTreeType;
        Changed?.Invoke();
    }

    public void SetSelectedShipId(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            shipId = "basic_ship";
        }

        if (selectedShipId == shipId)
        {
            return;
        }

        selectedShipId = shipId;
        ValidateEquipmentShipSelection();
        Changed?.Invoke();
    }

    public void AddPermanentCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (type)
        {
            case CurrencyType.ScrapParts:
                scrapParts += amount;
                break;

            case CurrencyType.CoreShards:
                coreShards += amount;
                break;

            case CurrencyType.StabilizedAlloy:
                stabilizedAlloy += amount;
                break;

            default:
                Debug.LogWarning($"{type}은 영구 재화가 아닙니다.", this);
                return;
        }

        Changed?.Invoke();
    }

    public bool CanSpend(int scrapCost, int coreShardCost)
    {
        scrapCost = Mathf.Max(0, scrapCost);
        coreShardCost = Mathf.Max(0, coreShardCost);
        return scrapParts >= scrapCost && coreShards >= coreShardCost;
    }

    public bool TrySpend(int scrapCost, int coreShardCost)
    {
        scrapCost = Mathf.Max(0, scrapCost);
        coreShardCost = Mathf.Max(0, coreShardCost);

        if (!CanSpend(scrapCost, coreShardCost))
        {
            return false;
        }

        scrapParts -= scrapCost;
        coreShards -= coreShardCost;

        Changed?.Invoke();
        return true;
    }

    public int GetBuildingLevel(BuildingType buildingType)
    {
        BuildingLevelState state = FindBuildingState(buildingType);
        return state != null ? Mathf.Max(0, state.level) : 0;
    }

    public int GetSectorTechnologyLevel(string technologyId)
    {
        SectorTechnologyLevelState state = FindSectorTechnologyState(technologyId);
        if (state == null || !SectorTechnologyCatalog.TryGet(technologyId, out SectorTechnologyDefinition definition))
        {
            return 0;
        }

        return Mathf.Clamp(state.level, 0, definition.MaxLevel);
    }

    public bool CanUpgradeSectorTechnology(string technologyId)
    {
        if (!SectorTechnologyCatalog.TryGet(technologyId, out SectorTechnologyDefinition definition))
        {
            return false;
        }

        int currentLevel = GetSectorTechnologyLevel(technologyId);
        if (currentLevel >= definition.MaxLevel)
        {
            return false;
        }

        int nextCost = definition.GetUpgradeCost(currentLevel + 1);
        return nextCost > 0 && stabilizedAlloy >= nextCost;
    }

    public bool TryUpgradeSectorTechnology(string technologyId)
    {
        if (!SectorTechnologyCatalog.TryGet(technologyId, out SectorTechnologyDefinition definition))
        {
            return false;
        }

        int currentLevel = GetSectorTechnologyLevel(technologyId);
        if (currentLevel >= definition.MaxLevel)
        {
            return false;
        }

        int nextLevel = currentLevel + 1;
        int cost = definition.GetUpgradeCost(nextLevel);
        if (cost <= 0 || stabilizedAlloy < cost)
        {
            return false;
        }

        SectorTechnologyLevelState state = FindSectorTechnologyState(technologyId);
        if (state == null)
        {
            state = new SectorTechnologyLevelState(definition.Id, currentLevel);
            sectorTechnologyLevels.Add(state);
        }

        stabilizedAlloy -= cost;
        state.level = nextLevel;
        Changed?.Invoke();
        return true;
    }

    public void SetBuildingLevel(BuildingType buildingType, int level)
    {
        BuildingLevelState state = FindBuildingState(buildingType);

        if (state == null)
        {
            buildingLevels.Add(new BuildingLevelState(buildingType, level));
        }
        else
        {
            state.level = Mathf.Max(0, level);
        }

        Changed?.Invoke();
    }

    public bool TryCompleteBuildingRestoration(BuildingType buildingType, string grantedUnlockFlag)
    {
        if (GetBuildingLevel(buildingType) > 0)
        {
            return false;
        }

        BuildingLevelState state = FindBuildingState(buildingType);
        if (state == null)
        {
            buildingLevels.Add(new BuildingLevelState(buildingType, 1));
        }
        else
        {
            state.level = 1;
        }

        AddUniqueString(unlockFlags, grantedUnlockFlag);
        Changed?.Invoke();
        return true;
    }

    public int GetTraitLevel(string traitId)
    {
        TraitLevelState state = FindTraitState(traitId);
        return state != null ? Mathf.Max(0, state.level) : 0;
    }

    public void SetTraitLevel(string traitId, int level)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return;
        }

        int newLevel = Mathf.Max(0, level);
        TraitLevelState state = FindTraitState(traitId);
        int previousLevel = state != null ? Mathf.Max(0, state.level) : 0;

        if (state == null)
        {
            traitLevels.Add(new TraitLevelState(traitId, newLevel));
        }
        else
        {
            state.level = newLevel;
        }

        if (newLevel <= 0 || previousLevel <= 0)
        {
            RemoveDisabledPermanentTraitId(traitId);
        }

        Changed?.Invoke();
    }

    public void IncrementTraitLevel(string traitId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(traitId) || amount <= 0)
        {
            return;
        }

        SetTraitLevel(traitId, GetTraitLevel(traitId) + amount);
    }

    public bool IsTraitUnlocked(string traitId)
    {
        return GetTraitLevel(traitId) > 0;
    }

    public bool IsTraitDisabled(string traitId)
    {
        if (!IsTraitUnlocked(traitId))
        {
            return false;
        }

        return ContainsString(disabledPermanentTraitIds, traitId);
    }

    public bool IsTraitActive(string traitId)
    {
        return IsTraitUnlocked(traitId) && !IsTraitDisabled(traitId);
    }

    public void SetTraitActive(string traitId, bool active)
    {
        if (string.IsNullOrWhiteSpace(traitId) || !IsTraitUnlocked(traitId))
        {
            return;
        }

        bool changed = active
            ? RemoveDisabledPermanentTraitId(traitId)
            : AddDisabledPermanentTraitId(traitId);

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public bool ToggleTraitActive(string traitId)
    {
        bool nextActive = !IsTraitActive(traitId);
        SetTraitActive(traitId, nextActive);
        return IsTraitActive(traitId);
    }

    public bool HasUnlockFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
        {
            return false;
        }

        return unlockFlags.Contains(flag);
    }

    public void AddUnlockFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
        {
            return;
        }

        if (unlockFlags.Contains(flag))
        {
            return;
        }

        unlockFlags.Add(flag);
        Changed?.Invoke();
    }

    public bool TryMarkTutorialCompleted()
    {
        if (IsTutorialCompleted)
        {
            return false;
        }

        unlockFlags.Add(TutorialCompletedUnlockFlag);
        Changed?.Invoke();
        return true;
    }

    public bool TryStartDamagedAccessKeyQuest()
    {
        if (HasUnlockFlag(MainDamagedAccessKeyQuestIds.StartedUnlockFlag))
        {
            return false;
        }

        unlockFlags.Add(MainDamagedAccessKeyQuestIds.StartedUnlockFlag);
        Changed?.Invoke();
        return true;
    }

    public bool HasPersistentStoryTrait(TraitDefinition trait)
    {
        return trait != null &&
               trait.IsPersistentStoryTrait &&
               HasPersistentStoryTrait(trait.TraitId);
    }

    public bool HasPersistentStoryTrait(string traitId)
    {
        return !string.IsNullOrWhiteSpace(traitId) &&
               HasUnlockFlag(BuildPersistentStoryTraitUnlockFlag(traitId));
    }

    public bool TryAcquirePersistentStoryTrait(TraitDefinition trait)
    {
        if (trait == null || !trait.IsPersistentStoryTrait || string.IsNullOrWhiteSpace(trait.TraitId))
        {
            return false;
        }

        int previousPixelCurseLevel = PixelCurseLevel;
        string unlockFlag = BuildPersistentStoryTraitUnlockFlag(trait.TraitId);

        if (HasUnlockFlag(unlockFlag))
        {
            return false;
        }

        unlockFlags.Add(unlockFlag);
        Changed?.Invoke();
        NotifyPixelCurseLevelChanged(previousPixelCurseLevel);
        return true;
    }

    public bool HasDefeatedCampaignBoss(CampaignBossId bossId)
    {
        return bossId != CampaignBossId.None && defeatedCampaignBosses.Contains(bossId);
    }

    public bool HasBossStoryPart(BossStoryPart storyPart)
    {
        return storyPart != BossStoryPart.None && acquiredBossStoryParts.Contains(storyPart);
    }

    public bool IsDepthUnlocked(ExpeditionDepth depth)
    {
        if (depth == ExpeditionDepth.FinalNetwork)
        {
            return CanLaunchFinalExpedition;
        }

        return CampaignProgressionCatalog.GetRegionIndex(depth) <=
               CampaignProgressionCatalog.GetRegionIndex(highestUnlockedDepth);
    }

    public bool RegisterCampaignBossDefeat(CampaignBossId bossId, bool grantStoryPart = true)
    {
        if (bossId == CampaignBossId.None)
        {
            return false;
        }

        int previousPixelCurseLevel = PixelCurseLevel;
        bool changed = AddUniqueBossId(defeatedCampaignBosses, bossId);

        if (bossId == CampaignBossId.NullDispatcher)
        {
            if (!finalBossDefeated)
            {
                finalBossDefeated = true;
                changed = true;
            }
        }
        else if (grantStoryPart)
        {
            BossStoryPart part = CampaignProgressionCatalog.GetStoryPart(bossId);
            changed |= AddUniqueStoryPart(acquiredBossStoryParts, part);
        }

        AddUnlockFlag(GetBossUnlockFlag(bossId));
        RefreshCampaignDerivedState();

        if (changed)
        {
            Changed?.Invoke();
        }

        NotifyPixelCurseLevelChanged(previousPixelCurseLevel);

        return changed;
    }

    public bool HasPendingCampaignRouteAnalysis => TryGetAnalyzedRoute(out _);

    // Called only by Settlement's natural dialogue completion transaction.
    // The existing saved depth is authorization, not a projection of boss defeats.
    public bool TryAuthorizeAnalyzedRegion()
    {
        if (!TryGetAnalyzedRoute(out ExpeditionDepth depth))
        {
            return false;
        }

        highestUnlockedDepth = depth;
        RefreshEquipmentResearch();
        Changed?.Invoke();
        return true;
    }

    private bool TryGetAnalyzedRoute(out ExpeditionDepth depth)
    {
        depth = highestUnlockedDepth;
        if (!HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator) ||
            !HasBossStoryPart(BossStoryPart.SectorStabilizer))
        {
            return false;
        }

        if (DamagedAccessKeyQuestState == MainDamagedAccessKeyQuestState.Active1)
        {
            depth = ExpeditionDepth.DeepZone1;
        }
        else if (DamagedAccessKeyQuestState == MainDamagedAccessKeyQuestState.Active2 &&
                 HasDefeatedCampaignBoss(CampaignBossId.SalvageDevourer) &&
                 HasBossStoryPart(BossStoryPart.MatterCompressor))
        {
            depth = ExpeditionDepth.DeepZone2;
        }

        return depth > highestUnlockedDepth;
    }

    public bool TryRestoreDamagedAccessKey()
    {
        RefreshCampaignDerivedState();

        if (DamagedAccessKeyQuestState !=
            MainDamagedAccessKeyQuestState.ReadyToRestore)
        {
            return false;
        }

        routeCoreState = RouteCoreState.Assembled;
        AddUniqueString(unlockFlags, "campaign_route_core_assembled");
        RefreshEquipmentResearch();
        Changed?.Invoke();
        return true;
    }

    public bool TryAssembleRouteCore()
    {
        return TryRestoreDamagedAccessKey();
    }

    public bool TryActivateRouteCore()
    {
        if (ResolveRouteCoreState() != RouteCoreState.Assembled)
        {
            return false;
        }

        routeCoreState = RouteCoreState.Activated;
        settlementDefenseCleared = false;
        AddUnlockFlag("campaign_route_core_activated");
        Changed?.Invoke();
        return true;
    }

    public void MarkSettlementDefenseCleared()
    {
        if (ResolveRouteCoreState() != RouteCoreState.Activated)
        {
            return;
        }

        if (settlementDefenseCleared)
        {
            return;
        }

        settlementDefenseCleared = true;
        AddUnlockFlag("campaign_settlement_defense_cleared");
        Changed?.Invoke();
    }

    public string BuildCampaignProgressText()
    {
        return
            $"보스 부품 {AcquiredBossStoryPartCount}/3\n" +
            $"항로 코어: {GetRouteCoreStateDisplayName()}\n" +
            $"최고 해금 해역: {CampaignProgressionCatalog.GetRegionShortName(highestUnlockedDepth)}";
    }

    public string GetRouteCoreStateDisplayName()
    {
        return ResolveRouteCoreState() switch
        {
            RouteCoreState.MissingParts => "부품 수집 중",
            RouteCoreState.ReadyToAssemble => "조립 가능",
            RouteCoreState.Assembled => "조립 완료",
            RouteCoreState.Activated => settlementDefenseCleared ? "중앙 항로 개방" : "활성화 · 방어전 대기",
            _ => "미확인"
        };
    }

    public void ApplyRunResult(RunResultData resultData)
    {
        if (resultData == null)
        {
            return;
        }

        RecordRunStats(resultData);

        AddPermanentCurrency(CurrencyType.ScrapParts, resultData.committedScrapParts);
        AddPermanentCurrency(CurrencyType.CoreShards, resultData.committedCoreShards);
        AddPermanentCurrency(CurrencyType.StabilizedAlloy, resultData.committedStabilizedAlloy);
        SetLastSelectedWeaponTree(resultData.selectedWeaponTree);

        if (!string.IsNullOrWhiteSpace(resultData.selectedShipId))
        {
            SetSelectedShipId(resultData.selectedShipId);
        }

        Changed?.Invoke();
    }

    private void RecordRunStats(RunResultData resultData)
    {
        totalRunCount++;

        switch (resultData.endReason)
        {
            case RunEndReason.SafeReturn:
            case RunEndReason.FinalVictory:
                safeReturnCount++;
                break;

            case RunEndReason.EmergencyReturn:
                emergencyReturnCount++;
                break;

            case RunEndReason.Death:
                deathCount++;
                break;
        }

        if (resultData.bossDefeated)
        {
            bossDefeatCount++;
        }

        totalCollectedScrapParts += Mathf.Max(0, resultData.collectedScrapParts);
        totalCollectedCoreShards += Mathf.Max(0, resultData.collectedCoreShards);
        totalCollectedStabilizedAlloy += Mathf.Max(0, resultData.collectedStabilizedAlloy);
        totalCommittedScrapParts += Mathf.Max(0, resultData.committedScrapParts);
        totalCommittedCoreShards += Mathf.Max(0, resultData.committedCoreShards);
        totalCommittedStabilizedAlloy += Mathf.Max(0, resultData.committedStabilizedAlloy);
    }

    private BuildingLevelState FindBuildingState(BuildingType buildingType)
    {
        return buildingLevels.Find(state => state.buildingType == buildingType);
    }

    private TraitLevelState FindTraitState(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        return traitLevels.Find(state => state.traitId == traitId);
    }

    private SectorTechnologyLevelState FindSectorTechnologyState(string technologyId)
    {
        if (string.IsNullOrWhiteSpace(technologyId))
        {
            return null;
        }

        for (int i = 0; i < sectorTechnologyLevels.Count; i++)
        {
            SectorTechnologyLevelState state = sectorTechnologyLevels[i];
            if (state != null && state.technologyId == technologyId)
            {
                return state;
            }
        }

        return null;
    }

    private static string BuildPersistentStoryTraitUnlockFlag(string traitId)
    {
        return $"{PersistentStoryTraitUnlockPrefix}{traitId.Trim()}";
    }

    private bool AddDisabledPermanentTraitId(string traitId)
    {
        return AddUniqueString(disabledPermanentTraitIds, traitId);
    }

    private bool RemoveDisabledPermanentTraitId(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId) || disabledPermanentTraitIds == null)
        {
            return false;
        }

        return disabledPermanentTraitIds.RemoveAll(id => id == traitId) > 0;
    }

    private void PruneDisabledPermanentTraitIds()
    {
        if (disabledPermanentTraitIds == null)
        {
            disabledPermanentTraitIds = new List<string>();
            return;
        }

        disabledPermanentTraitIds.RemoveAll(traitId => string.IsNullOrWhiteSpace(traitId) || GetTraitLevel(traitId) <= 0);
    }

    private bool AddUniqueString(List<string> target, string value)
    {
        if (target == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (ContainsString(target, value))
        {
            return false;
        }

        target.Add(value);
        return true;
    }

    private bool ContainsString(List<string> target, string value)
    {
        if (target == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < target.Count; i++)
        {
            if (target[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshCampaignDerivedState()
    {
        // Preserve existing saves' authorized routes. Defeat/load alone cannot
        // authorize a route, and FinalNetwork still has its independent core gate.
        highestUnlockedDepth = ClampCampaignDepth(highestUnlockedDepth);

        if (HasAllRouteCoreParts && routeCoreState == RouteCoreState.MissingParts)
        {
            routeCoreState = RouteCoreState.ReadyToAssemble;
        }

        if (!HasAllRouteCoreParts && routeCoreState != RouteCoreState.MissingParts)
        {
            routeCoreState = RouteCoreState.MissingParts;
            settlementDefenseCleared = false;
        }
    }

    private RouteCoreState ResolveRouteCoreState()
    {
        if (routeCoreState == RouteCoreState.MissingParts && HasAllRouteCoreParts)
        {
            return RouteCoreState.ReadyToAssemble;
        }

        return routeCoreState;
    }

    private MainDamagedAccessKeyQuestState ResolveDamagedAccessKeyQuestState()
    {
        if (!HasUnlockFlag(MainDamagedAccessKeyQuestIds.StartedUnlockFlag))
        {
            return MainDamagedAccessKeyQuestState.NotStarted;
        }

        RouteCoreState currentRouteCoreState = ResolveRouteCoreState();
        if (currentRouteCoreState == RouteCoreState.Assembled ||
            currentRouteCoreState == RouteCoreState.Activated)
        {
            return MainDamagedAccessKeyQuestState.Completed;
        }

        return AcquiredBossStoryPartCount switch
        {
            0 => MainDamagedAccessKeyQuestState.Active0,
            1 => MainDamagedAccessKeyQuestState.Active1,
            2 => MainDamagedAccessKeyQuestState.Active2,
            _ => MainDamagedAccessKeyQuestState.ReadyToRestore
        };
    }

    private int ResolvePixelCurseLevel()
    {
        if (!HasPersistentStoryTrait(PixelCurseProgressionIds.TraitId))
        {
            return 0;
        }

        return Mathf.Min(
            1 + AcquiredBossStoryPartCount,
            PixelCurseProgressionIds.MaximumLevel);
    }

    private void NotifyPixelCurseLevelChanged(int previousLevel)
    {
        int currentLevel = PixelCurseLevel;
        if (currentLevel != previousLevel)
        {
            PixelCurseLevelChanged?.Invoke(previousLevel, currentLevel);
        }
    }

    private int CountRequiredStoryParts()
    {
        int count = 0;

        if (HasBossStoryPart(BossStoryPart.SectorStabilizer)) count++;
        if (HasBossStoryPart(BossStoryPart.MatterCompressor)) count++;
        if (HasBossStoryPart(BossStoryPart.PhaseNavigationLens)) count++;

        return count;
    }

    private ExpeditionDepth ClampCampaignDepth(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => ExpeditionDepth.Normal,
            ExpeditionDepth.DeepZone1 => ExpeditionDepth.DeepZone1,
            ExpeditionDepth.DeepZone2 => ExpeditionDepth.DeepZone2,
            ExpeditionDepth.FinalNetwork => ExpeditionDepth.FinalNetwork,
            _ => ExpeditionDepth.Normal
        };
    }

    private bool AddUniqueBossId(List<CampaignBossId> target, CampaignBossId bossId)
    {
        if (target == null || bossId == CampaignBossId.None || target.Contains(bossId))
        {
            return false;
        }

        target.Add(bossId);
        return true;
    }

    private bool AddUniqueStoryPart(List<BossStoryPart> target, BossStoryPart storyPart)
    {
        if (target == null || storyPart == BossStoryPart.None || target.Contains(storyPart))
        {
            return false;
        }

        target.Add(storyPart);
        return true;
    }

    private string GetBossUnlockFlag(CampaignBossId bossId)
    {
        return bossId switch
        {
            CampaignBossId.SectorAdministrator => "campaign_boss_sector_administrator_defeated",
            CampaignBossId.SalvageDevourer => "campaign_boss_salvage_devourer_defeated",
            CampaignBossId.PhaseGatekeeper => "campaign_boss_phase_gatekeeper_defeated",
            CampaignBossId.NullDispatcher => "campaign_boss_null_dispatcher_defeated",
            _ => string.Empty
        };
    }

    private void RestoreCampaignProgressFromLegacyFlags()
    {
        if (HasUnlockFlag("campaign_boss_sector_administrator_defeated"))
        {
            AddUniqueBossId(defeatedCampaignBosses, CampaignBossId.SectorAdministrator);
            AddUniqueStoryPart(acquiredBossStoryParts, BossStoryPart.SectorStabilizer);
        }

        if (HasUnlockFlag("campaign_boss_salvage_devourer_defeated"))
        {
            AddUniqueBossId(defeatedCampaignBosses, CampaignBossId.SalvageDevourer);
            AddUniqueStoryPart(acquiredBossStoryParts, BossStoryPart.MatterCompressor);
        }

        if (HasUnlockFlag("campaign_boss_phase_gatekeeper_defeated"))
        {
            AddUniqueBossId(defeatedCampaignBosses, CampaignBossId.PhaseGatekeeper);
            AddUniqueStoryPart(acquiredBossStoryParts, BossStoryPart.PhaseNavigationLens);
        }

        if (HasUnlockFlag("campaign_boss_null_dispatcher_defeated"))
        {
            AddUniqueBossId(defeatedCampaignBosses, CampaignBossId.NullDispatcher);
            finalBossDefeated = true;
        }

        if (HasUnlockFlag("campaign_route_core_assembled") && HasAllRouteCoreParts)
        {
            routeCoreState = RouteCoreState.Assembled;
        }

        if (HasUnlockFlag("campaign_route_core_activated") && HasAllRouteCoreParts)
        {
            routeCoreState = RouteCoreState.Activated;
        }

        if (HasUnlockFlag("campaign_settlement_defense_cleared"))
        {
            settlementDefenseCleared = true;
        }
    }

    private void EnsureDefaultBuildings()
    {
        EnsureBuilding(BuildingType.Hangar);
        EnsureBuilding(BuildingType.EngineWorkshop);
        EnsureBuilding(BuildingType.WeaponLab);
        EnsureBuilding(BuildingType.RecoveryProcessor);
    }

    private void EnsureBuilding(BuildingType buildingType)
    {
        if (FindBuildingState(buildingType) == null)
        {
            buildingLevels.Add(new BuildingLevelState(buildingType, 0));
        }
    }
}
