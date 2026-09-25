using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 8;
    // Migration-only tombstone for v7 JSON (0 Standard, 1 Lightweight, 2 Heavy).
    // -1 means absent/retired. No runtime or preference authority reads this field.
    public int selectedOperatingFrame = -1;

    public void RetireLegacyOperatingFrame()
    {
        if (version == 7 && selectedOperatingFrame >= 0 && selectedOperatingFrame <= 2 &&
            acquiredBossStoryParts != null && acquiredBossStoryParts.Contains(BossStoryPart.SectorStabilizer) &&
            highestUnlockedDepth >= ExpeditionDepth.DeepZone1)
        {
            string id = selectedOperatingFrame == 1 ? StructuralFrameProfile.LightweightId :
                selectedOperatingFrame == 2 ? StructuralFrameProfile.HeavyId : StructuralFrameProfile.StandardId;
            manufacturedEquipmentIds ??= new List<string>();
            equipmentLoadoutTraitIds ??= new List<string>();
            if (!manufacturedEquipmentIds.Contains(id)) manufacturedEquipmentIds.Add(id);
            if (!equipmentLoadoutTraitIds.Contains(id)) equipmentLoadoutTraitIds.Add(id);
        }
        selectedOperatingFrame = -1;
    }

    // Stable fitting preferences across all branches; no slot padding or global capacity.
    public List<string> equipmentLoadoutTraitIds = new List<string>();
    public List<string> manufacturedEquipmentIds = new List<string>();
    public bool equipmentOwnershipMigrationPending;
    public List<string> grandfatheredEquipmentResearchIds = new List<string>();
    public bool equipmentRosterMigrationPending;

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
