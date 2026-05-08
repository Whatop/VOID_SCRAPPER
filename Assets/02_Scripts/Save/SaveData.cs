using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 1;

    public int scrapParts;
    public int coreShards;

    public WeaponTreeType lastSelectedWeaponTree = WeaponTreeType.MachineGun;

    public List<BuildingSaveData> buildingLevels = new List<BuildingSaveData>();
    public List<string> unlockFlags = new List<string>();
}

[Serializable]
public class BuildingSaveData
{
    public BuildingType buildingType;
    public int level;

    public BuildingSaveData(BuildingType buildingType, int level)
    {
        this.buildingType = buildingType;
        this.level = level;
    }
}