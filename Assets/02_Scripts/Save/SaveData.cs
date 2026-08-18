using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 4;

    public int scrapParts;
    public int coreShards;
    public int stabilizedAlloy;

    public int totalRunCount;
    public int safeReturnCount;
    public int emergencyReturnCount;
    public int deathCount;
    public int bossDefeatCount;
    public int totalCollectedScrapParts;
    public int totalCollectedCoreShards;
    public int totalCollectedStabilizedAlloy;
    public int totalCommittedScrapParts;
    public int totalCommittedCoreShards;
    public int totalCommittedStabilizedAlloy;

    public WeaponTreeType lastSelectedWeaponTree = WeaponTreeType.MachineGun;
    public string selectedShipId = "basic_ship";

    public List<BuildingSaveData> buildingLevels = new List<BuildingSaveData>();
    public List<TraitLevelSaveData> traitLevels = new List<TraitLevelSaveData>();
    public List<SectorTechnologyLevelSaveData> sectorTechnologyLevels = new List<SectorTechnologyLevelSaveData>();
    public List<string> disabledPermanentTraitIds = new List<string>();
    public List<string> unlockFlags = new List<string>();

    // Campaign progression v3.
    public List<CampaignBossId> defeatedCampaignBosses = new List<CampaignBossId>();
    public List<BossStoryPart> acquiredBossStoryParts = new List<BossStoryPart>();
    public ExpeditionDepth highestUnlockedDepth = ExpeditionDepth.Normal;
    public RouteCoreState routeCoreState = RouteCoreState.MissingParts;
    public bool settlementDefenseCleared;
    public bool finalBossDefeated;
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

[Serializable]
public class TraitLevelSaveData
{
    public string traitId;
    public int level;

    public TraitLevelSaveData(string traitId, int level)
    {
        this.traitId = traitId;
        this.level = level;
    }
}

[Serializable]
public class SectorTechnologyLevelSaveData
{
    public string technologyId;
    public int level;

    public SectorTechnologyLevelSaveData(string technologyId, int level)
    {
        this.technologyId = technologyId;
        this.level = level;
    }
}
