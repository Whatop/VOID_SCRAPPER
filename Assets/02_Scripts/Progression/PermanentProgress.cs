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

public class PermanentProgress : MonoBehaviour
{
    public static PermanentProgress Instance { get; private set; }

    [Header("Permanent Currency")]
    [SerializeField] private int scrapParts;
    [SerializeField] private int coreShards;

    [Header("Selection")]
    [SerializeField] private WeaponTreeType lastSelectedWeaponTree = WeaponTreeType.MachineGun;

    [Header("Building Levels")]
    [SerializeField] private List<BuildingLevelState> buildingLevels = new List<BuildingLevelState>();

    [Header("Unlock Flags")]
    [SerializeField] private List<string> unlockFlags = new List<string>();

    public int ScrapParts => scrapParts;
    public int CoreShards => coreShards;
    public WeaponTreeType LastSelectedWeaponTree => lastSelectedWeaponTree;

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
        lastSelectedWeaponTree = saveData.lastSelectedWeaponTree;

        buildingLevels.Clear();

        if (saveData.buildingLevels != null)
        {
            foreach (BuildingSaveData data in saveData.buildingLevels)
            {
                buildingLevels.Add(new BuildingLevelState(data.buildingType, data.level));
            }
        }

        unlockFlags.Clear();

        if (saveData.unlockFlags != null)
        {
            unlockFlags.AddRange(saveData.unlockFlags);
        }

        EnsureDefaultBuildings();
        Changed?.Invoke();
    }

    public SaveData CreateSaveData()
    {
        SaveData saveData = new SaveData
        {
            scrapParts = scrapParts,
            coreShards = coreShards,
            lastSelectedWeaponTree = lastSelectedWeaponTree
        };

        foreach (BuildingLevelState state in buildingLevels)
        {
            saveData.buildingLevels.Add(new BuildingSaveData(state.buildingType, state.level));
        }

        saveData.unlockFlags.AddRange(unlockFlags);

        return saveData;
    }

    public void ResetProgress()
    {
        scrapParts = 0;
        coreShards = 0;
        lastSelectedWeaponTree = WeaponTreeType.MachineGun;

        buildingLevels.Clear();
        unlockFlags.Clear();

        EnsureDefaultBuildings();
        Changed?.Invoke();
    }

    public void SetLastSelectedWeaponTree(WeaponTreeType weaponTreeType)
    {
        lastSelectedWeaponTree = weaponTreeType;
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
        return state != null ? state.level : 0;
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

        AddPermanentCurrency(CurrencyType.ScrapParts, resultData.committedScrapParts);
        AddPermanentCurrency(CurrencyType.CoreShards, resultData.committedCoreShards);
        SetLastSelectedWeaponTree(resultData.selectedWeaponTree);
    }

    private BuildingLevelState FindBuildingState(BuildingType buildingType)
    {
        return buildingLevels.Find(state => state.buildingType == buildingType);
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