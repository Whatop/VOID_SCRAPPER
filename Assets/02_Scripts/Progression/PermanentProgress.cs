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

public class PermanentProgress : MonoBehaviour
{
    public static PermanentProgress Instance { get; private set; }

    [Header("Permanent Currency")]
    [SerializeField] private int scrapParts;
    [SerializeField] private int coreShards;

    [Header("Permanent Stats")]
    [SerializeField] private int totalRunCount;
    [SerializeField] private int safeReturnCount;
    [SerializeField] private int emergencyReturnCount;
    [SerializeField] private int deathCount;
    [SerializeField] private int bossDefeatCount;
    [SerializeField] private int totalCollectedScrapParts;
    [SerializeField] private int totalCollectedCoreShards;
    [SerializeField] private int totalCommittedScrapParts;
    [SerializeField] private int totalCommittedCoreShards;

    [Header("Selection")]
    [SerializeField] private WeaponTreeType lastSelectedWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string selectedShipId = "basic_ship";

    [Header("Building Levels")]
    [SerializeField] private List<BuildingLevelState> buildingLevels = new List<BuildingLevelState>();

    [Header("Permanent Trait Levels")]
    [SerializeField] private List<TraitLevelState> traitLevels = new List<TraitLevelState>();

    [Header("Disabled Permanent Traits")]
    [SerializeField] private List<string> disabledPermanentTraitIds = new List<string>();

    [Header("Unlock Flags")]
    [SerializeField] private List<string> unlockFlags = new List<string>();

    public int ScrapParts => scrapParts;
    public int CoreShards => coreShards;
    public int TotalRunCount => totalRunCount;
    public int SafeReturnCount => safeReturnCount;
    public int EmergencyReturnCount => emergencyReturnCount;
    public int DeathCount => deathCount;
    public int BossDefeatCount => bossDefeatCount;
    public int TotalCollectedScrapParts => totalCollectedScrapParts;
    public int TotalCollectedCoreShards => totalCollectedCoreShards;
    public int TotalCommittedScrapParts => totalCommittedScrapParts;
    public int TotalCommittedCoreShards => totalCommittedCoreShards;
    public WeaponTreeType LastSelectedWeaponTree => lastSelectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? "basic_ship" : selectedShipId;

    public event Action Changed;

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
        if (saveData == null)
        {
            ResetProgress();
            return;
        }

        scrapParts = Mathf.Max(0, saveData.scrapParts);
        coreShards = Mathf.Max(0, saveData.coreShards);
        totalRunCount = Mathf.Max(0, saveData.totalRunCount);
        safeReturnCount = Mathf.Max(0, saveData.safeReturnCount);
        emergencyReturnCount = Mathf.Max(0, saveData.emergencyReturnCount);
        deathCount = Mathf.Max(0, saveData.deathCount);
        bossDefeatCount = Mathf.Max(0, saveData.bossDefeatCount);
        totalCollectedScrapParts = Mathf.Max(0, saveData.totalCollectedScrapParts);
        totalCollectedCoreShards = Mathf.Max(0, saveData.totalCollectedCoreShards);
        totalCommittedScrapParts = Mathf.Max(0, saveData.totalCommittedScrapParts);
        totalCommittedCoreShards = Mathf.Max(0, saveData.totalCommittedCoreShards);
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

        PruneDisabledPermanentTraitIds();
        EnsureDefaultBuildings();
        Changed?.Invoke();
    }

    public SaveData CreateSaveData()
    {
        SaveData saveData = new SaveData
        {
            scrapParts = scrapParts,
            coreShards = coreShards,
            totalRunCount = totalRunCount,
            safeReturnCount = safeReturnCount,
            emergencyReturnCount = emergencyReturnCount,
            deathCount = deathCount,
            bossDefeatCount = bossDefeatCount,
            totalCollectedScrapParts = totalCollectedScrapParts,
            totalCollectedCoreShards = totalCollectedCoreShards,
            totalCommittedScrapParts = totalCommittedScrapParts,
            totalCommittedCoreShards = totalCommittedCoreShards,
            lastSelectedWeaponTree = lastSelectedWeaponTree,
            selectedShipId = SelectedShipId
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

        foreach (string traitId in disabledPermanentTraitIds)
        {
            if (!string.IsNullOrWhiteSpace(traitId))
            {
                saveData.disabledPermanentTraitIds.Add(traitId);
            }
        }

        saveData.unlockFlags.AddRange(unlockFlags);
        return saveData;
    }

    public void ResetProgress()
    {
        scrapParts = 0;
        coreShards = 0;
        totalRunCount = 0;
        safeReturnCount = 0;
        emergencyReturnCount = 0;
        deathCount = 0;
        bossDefeatCount = 0;
        totalCollectedScrapParts = 0;
        totalCollectedCoreShards = 0;
        totalCommittedScrapParts = 0;
        totalCommittedCoreShards = 0;
        lastSelectedWeaponTree = WeaponTreeType.MachineGun;
        selectedShipId = "basic_ship";

        buildingLevels.Clear();
        traitLevels.Clear();
        disabledPermanentTraitIds.Clear();
        unlockFlags.Clear();

        EnsureDefaultBuildings();
        Changed?.Invoke();
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

    public void ApplyRunResult(RunResultData resultData)
    {
        if (resultData == null)
        {
            return;
        }

        RecordRunStats(resultData);

        AddPermanentCurrency(CurrencyType.ScrapParts, resultData.committedScrapParts);
        AddPermanentCurrency(CurrencyType.CoreShards, resultData.committedCoreShards);
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
        totalCommittedScrapParts += Mathf.Max(0, resultData.committedScrapParts);
        totalCommittedCoreShards += Mathf.Max(0, resultData.committedCoreShards);
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
