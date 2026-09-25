using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    private const int CurrentSaveVersion = 8;

    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string fileName = "void_scrapper_save.json";
    [SerializeField] private bool logSavePath = true;

    [Header("Default Save")]
    [SerializeField] private string defaultShipId = "basic_ship";

    public SaveData CurrentSaveData { get; private set; }
    public bool HasUsableProgression => HasMeaningfulProgress(CurrentSaveData);

    private string SavePath => Path.Combine(Application.persistentDataPath, fileName);
    private string BackupPath => SavePath + ".bak";
    private string TemporaryPath => SavePath + ".tmp";
    private string CorruptPath => SavePath + ".corrupt";

    [Serializable]
    private sealed class SaveVersionHeader
    {
        public int version = -1;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SaveManager가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public SaveData LoadOrCreate()
    {
        if (logSavePath)
        {
            Debug.Log($"Save Path: {SavePath}");
        }

        bool primaryExists = File.Exists(SavePath);
        bool backupExists = File.Exists(BackupPath);

        if (TryLoadSave(SavePath, out SaveData saveData, out string primaryError))
        {
            CurrentSaveData = saveData;
            return CurrentSaveData;
        }

        if (primaryExists)
        {
            Debug.LogWarning($"Primary save could not be loaded: {primaryError}. Trying backup at '{BackupPath}'.", this);
        }

        if (TryLoadSave(BackupPath, out saveData, out string backupError))
        {
            CurrentSaveData = saveData;
            Debug.LogWarning($"Recovered progression from backup because the primary save was unavailable: {primaryError}.", this);
            return CurrentSaveData;
        }

        if (backupExists)
        {
            Debug.LogWarning($"Backup save could not be loaded: {backupError}.", this);
        }

        CurrentSaveData = CreateDefaultSave();

        if (!primaryExists && !backupExists)
        {
            Save(CurrentSaveData);
        }
        else
        {
            Debug.LogError(
                $"No valid save could be loaded. Fresh progress will be used in memory; existing save files were retained. " +
                $"Primary: {primaryError}. Backup: {backupError}.",
                this
            );
        }

        return CurrentSaveData;
    }

    public void Save(PermanentProgress progress)
    {
        if (progress == null)
        {
            Debug.LogWarning("저장할 PermanentProgress가 없습니다.", this);
            return;
        }

        Save(progress.CreateSaveData());
    }

    public void Save(SaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("저장할 SaveData가 없습니다.", this);
            return;
        }

        if (!TryMigrateAndSanitize(saveData, saveData.version, out string migrationError))
        {
            Debug.LogError($"Save was not written because its data could not be prepared: {migrationError}.", this);
            return;
        }

        string json;

        try
        {
            json = JsonUtility.ToJson(saveData, true);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save serialization failed. The previous save was preserved. {exception.Message}", this);
            return;
        }

        if (!TryWriteSaveAtomically(json, out string writeError))
        {
            Debug.LogError($"Save write failed. The previous save was preserved. {writeError}", this);
            return;
        }

        CurrentSaveData = saveData;
    }

    public void DeleteSave()
    {
        if (!TryResetToDefault(out _))
        {
            Debug.LogError("Save reset failed. Existing progression was preserved when possible.", this);
        }
    }

    public bool TryResetToDefault(out SaveData freshSave)
    {
        freshSave = CreateDefaultSave();
        Save(freshSave);

        if (!ReferenceEquals(CurrentSaveData, freshSave))
        {
            freshSave = null;
            return false;
        }

        string[] auxiliaryPaths = { TemporaryPath, CorruptPath, BackupPath };

        for (int i = 0; i < auxiliaryPaths.Length; i++)
        {
            try
            {
                if (File.Exists(auxiliaryPaths[i]))
                {
                    File.Delete(auxiliaryPaths[i]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Fresh progression was saved, but old auxiliary save '{auxiliaryPaths[i]}' could not be removed. " +
                    exception.Message,
                    this
                );
            }
        }

        return true;
    }

    private SaveData CreateDefaultSave()
    {
        SaveData saveData = new SaveData
        {
            version = CurrentSaveVersion,
            selectedShipId = defaultShipId
        };

        AddDefaultBuildings(saveData.buildingLevels);
        return saveData;
    }

    private bool HasMeaningfulProgress(SaveData saveData)
    {
        if (saveData == null)
        {
            return false;
        }

        if (saveData.scrapParts > 0 ||
            saveData.coreShards > 0 ||
            saveData.stabilizedAlloy > 0 ||
            saveData.totalRunCount > 0 ||
            saveData.safeReturnCount > 0 ||
            saveData.emergencyReturnCount > 0 ||
            saveData.deathCount > 0 ||
            saveData.bossDefeatCount > 0 ||
            saveData.totalCollectedScrapParts > 0 ||
            saveData.totalCollectedCoreShards > 0 ||
            saveData.totalCollectedStabilizedAlloy > 0 ||
            saveData.totalCommittedScrapParts > 0 ||
            saveData.totalCommittedCoreShards > 0 ||
            saveData.totalCommittedStabilizedAlloy > 0 ||
            saveData.lastSelectedWeaponTree != WeaponTreeType.MachineGun ||
            !string.Equals(saveData.selectedShipId, defaultShipId, StringComparison.Ordinal) ||
            saveData.highestUnlockedDepth != ExpeditionDepth.Normal ||
            saveData.routeCoreState != RouteCoreState.MissingParts ||
            saveData.settlementDefenseCleared ||
            saveData.finalBossDefeated)
        {
            return true;
        }

        if (HasPositiveBuildingLevel(saveData.buildingLevels) ||
            HasPositiveTraitLevel(saveData.traitLevels) ||
            HasPositiveSectorTechnologyLevel(saveData.sectorTechnologyLevels))
        {
            return true;
        }

        return HasValues(saveData.manufacturedEquipmentIds) || HasValues(saveData.equipmentLoadoutTraitIds) ||
               HasValues(saveData.disabledPermanentTraitIds) ||
               HasValues(saveData.unlockFlags) ||
               HasValues(saveData.defeatedCampaignBosses) ||
               HasValues(saveData.acquiredBossStoryParts);
    }

    private static bool HasPositiveBuildingLevel(List<BuildingSaveData> entries)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].level > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPositiveTraitLevel(List<TraitLevelSaveData> entries)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].level > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValues<T>(List<T> values)
    {
        return values != null && values.Count > 0;
    }

    private bool TryLoadSave(string path, out SaveData saveData, out string error)
    {
        saveData = null;

        if (!File.Exists(path))
        {
            error = "file does not exist";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "file is empty";
                return false;
            }

            SaveVersionHeader header = JsonUtility.FromJson<SaveVersionHeader>(json);
            if (header == null || header.version < 1)
            {
                error = "save version is missing or invalid";
                return false;
            }

            // Preserve explicit DTO defaults for absent fields, especially the retired-frame -1 sentinel.
            SaveData loaded = new SaveData();
            JsonUtility.FromJsonOverwrite(json, loaded);
            if (loaded == null)
            {
                error = "JSON did not contain save data";
                return false;
            }

            if (!TryMigrateAndSanitize(loaded, header.version, out error))
            {
                return false;
            }

            saveData = loaded;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    private bool TryWriteSaveAtomically(string json, out string error)
    {
        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(TemporaryPath))
            {
                File.Delete(TemporaryPath);
            }

            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            using (FileStream stream = new FileStream(
                       TemporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            FileInfo temporaryFile = new FileInfo(TemporaryPath);
            if (!temporaryFile.Exists || temporaryFile.Length != bytes.Length)
            {
                throw new IOException("Temporary save length did not match the serialized data.");
            }

            if (!TryLoadSave(TemporaryPath, out _, out string validationError))
            {
                throw new InvalidDataException($"Temporary save validation failed: {validationError}");
            }

            if (!File.Exists(SavePath))
            {
                File.Move(TemporaryPath, SavePath);
            }
            else
            {
                bool primaryIsValid = TryLoadSave(SavePath, out _, out _);
                string preservedPath = primaryIsValid ? BackupPath : CorruptPath;
                File.Replace(TemporaryPath, SavePath, preservedPath);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(TemporaryPath))
                {
                    File.Delete(TemporaryPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not remove temporary save '{TemporaryPath}'. {exception.Message}", this);
            }
        }
    }

    private bool TryMigrateAndSanitize(SaveData saveData, int sourceVersion, out string error)
    {
        if (saveData == null)
        {
            error = "save data was null";
            return false;
        }

        if (sourceVersion < 1)
        {
            error = $"save version {sourceVersion} is invalid";
            return false;
        }

        if (sourceVersion > CurrentSaveVersion)
        {
            error = $"save version {sourceVersion} is newer than supported version {CurrentSaveVersion}";
            return false;
        }

        saveData.version = sourceVersion;

        while (saveData.version < CurrentSaveVersion)
        {
            switch (saveData.version)
            {
                case 1:
                    MigrateVersion1To2(saveData);
                    break;

                case 2:
                    MigrateVersion2To3(saveData);
                    break;

                case 3:
                    MigrateVersion3To4(saveData);
                    break;

                case 4:
                    saveData.equipmentLoadoutTraitIds ??= new List<string>();
                    saveData.version = 5;
                    break;

                case 5:
                    // Catalog identity is resolved by PermanentProgress after load, not by the file layer.
                    saveData.manufacturedEquipmentIds ??= new List<string>();
                    saveData.equipmentOwnershipMigrationPending = true;
                    saveData.version = 6;
                    break;
                case 6:
                    saveData.grandfatheredEquipmentResearchIds ??= new List<string>();
                    saveData.equipmentRosterMigrationPending = true;
                    saveData.version = 7;
                    break;

                case 7:
                    saveData.RetireLegacyOperatingFrame();
                    saveData.version = 8;
                    break;

                default:
                    error = $"no migration exists for save version {saveData.version}";
                    return false;
            }
        }

        SanitizeSaveData(saveData);
        error = string.Empty;
        return true;
    }

    private void MigrateVersion1To2(SaveData saveData)
    {
        saveData.traitLevels ??= new List<TraitLevelSaveData>();
        saveData.disabledPermanentTraitIds ??= new List<string>();

        if (string.IsNullOrWhiteSpace(saveData.selectedShipId))
        {
            saveData.selectedShipId = defaultShipId;
        }

        saveData.version = 2;
    }

    private static void MigrateVersion2To3(SaveData saveData)
    {
        saveData.defeatedCampaignBosses ??= new List<CampaignBossId>();
        saveData.acquiredBossStoryParts ??= new List<BossStoryPart>();
        saveData.version = 3;
    }

    private static void MigrateVersion3To4(SaveData saveData)
    {
        saveData.stabilizedAlloy = Mathf.Max(0, saveData.stabilizedAlloy);
        saveData.totalCollectedStabilizedAlloy = Mathf.Max(0, saveData.totalCollectedStabilizedAlloy);
        saveData.totalCommittedStabilizedAlloy = Mathf.Max(0, saveData.totalCommittedStabilizedAlloy);
        saveData.version = 4;
    }

    private void SanitizeSaveData(SaveData saveData)
    {
        saveData.version = CurrentSaveVersion;
        saveData.selectedOperatingFrame = -1;
        // Preserve cross-branch preferences and unknown IDs; progression validates usable definitions.
        saveData.equipmentLoadoutTraitIds = NormalizeUniqueStrings(saveData.equipmentLoadoutTraitIds);
        saveData.manufacturedEquipmentIds = NormalizeUniqueStrings(saveData.manufacturedEquipmentIds);
        saveData.grandfatheredEquipmentResearchIds = NormalizeUniqueStrings(saveData.grandfatheredEquipmentResearchIds);
        saveData.scrapParts = Mathf.Max(0, saveData.scrapParts);
        saveData.coreShards = Mathf.Max(0, saveData.coreShards);
        saveData.stabilizedAlloy = Mathf.Max(0, saveData.stabilizedAlloy);
        saveData.totalRunCount = Mathf.Max(0, saveData.totalRunCount);
        saveData.safeReturnCount = Mathf.Max(0, saveData.safeReturnCount);
        saveData.emergencyReturnCount = Mathf.Max(0, saveData.emergencyReturnCount);
        saveData.deathCount = Mathf.Max(0, saveData.deathCount);
        saveData.bossDefeatCount = Mathf.Max(0, saveData.bossDefeatCount);
        saveData.totalCollectedScrapParts = Mathf.Max(0, saveData.totalCollectedScrapParts);
        saveData.totalCollectedCoreShards = Mathf.Max(0, saveData.totalCollectedCoreShards);
        saveData.totalCollectedStabilizedAlloy = Mathf.Max(0, saveData.totalCollectedStabilizedAlloy);
        saveData.totalCommittedScrapParts = Mathf.Max(0, saveData.totalCommittedScrapParts);
        saveData.totalCommittedCoreShards = Mathf.Max(0, saveData.totalCommittedCoreShards);
        saveData.totalCommittedStabilizedAlloy = Mathf.Max(0, saveData.totalCommittedStabilizedAlloy);

        if (!Enum.IsDefined(typeof(WeaponTreeType), saveData.lastSelectedWeaponTree))
        {
            saveData.lastSelectedWeaponTree = WeaponTreeType.MachineGun;
        }

        if (string.IsNullOrWhiteSpace(saveData.selectedShipId))
        {
            saveData.selectedShipId = defaultShipId;
        }

        saveData.buildingLevels = NormalizeBuildingLevels(saveData.buildingLevels);
        saveData.traitLevels = NormalizeTraitLevels(saveData.traitLevels);
        saveData.sectorTechnologyLevels = NormalizeSectorTechnologyLevels(saveData.sectorTechnologyLevels);
        saveData.disabledPermanentTraitIds = NormalizeUniqueStrings(saveData.disabledPermanentTraitIds);
        saveData.unlockFlags = NormalizeUniqueStrings(saveData.unlockFlags);
        saveData.defeatedCampaignBosses = NormalizeBossIds(saveData.defeatedCampaignBosses);
        saveData.acquiredBossStoryParts = NormalizeStoryParts(saveData.acquiredBossStoryParts);

        saveData.disabledPermanentTraitIds.RemoveAll(
            traitId => FindTraitLevel(saveData.traitLevels, traitId) <= 0
        );

        if (!Enum.IsDefined(typeof(ExpeditionDepth), saveData.highestUnlockedDepth))
        {
            saveData.highestUnlockedDepth = ExpeditionDepth.Normal;
        }

        if (!Enum.IsDefined(typeof(RouteCoreState), saveData.routeCoreState))
        {
            saveData.routeCoreState = RouteCoreState.MissingParts;
        }
    }

    private static List<BuildingSaveData> NormalizeBuildingLevels(List<BuildingSaveData> source)
    {
        Dictionary<BuildingType, int> levelsByType = new Dictionary<BuildingType, int>();

        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                BuildingSaveData entry = source[i];
                if (entry == null || !Enum.IsDefined(typeof(BuildingType), entry.buildingType))
                {
                    continue;
                }

                int level = Mathf.Max(0, entry.level);
                if (!levelsByType.TryGetValue(entry.buildingType, out int existingLevel) || level > existingLevel)
                {
                    levelsByType[entry.buildingType] = level;
                }
            }
        }

        List<BuildingSaveData> normalized = new List<BuildingSaveData>();
        foreach (BuildingType buildingType in Enum.GetValues(typeof(BuildingType)))
        {
            int level = levelsByType.TryGetValue(buildingType, out int savedLevel) ? savedLevel : 0;
            normalized.Add(new BuildingSaveData(buildingType, level));
        }

        return normalized;
    }

    private static List<TraitLevelSaveData> NormalizeTraitLevels(List<TraitLevelSaveData> source)
    {
        List<TraitLevelSaveData> normalized = new List<TraitLevelSaveData>();
        Dictionary<string, TraitLevelSaveData> byId = new Dictionary<string, TraitLevelSaveData>(StringComparer.Ordinal);

        if (source == null)
        {
            return normalized;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TraitLevelSaveData entry = source[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.traitId))
            {
                continue;
            }

            int level = Mathf.Max(0, entry.level);
            if (byId.TryGetValue(entry.traitId, out TraitLevelSaveData existing))
            {
                existing.level = Mathf.Max(existing.level, level);
                continue;
            }

            TraitLevelSaveData normalizedEntry = new TraitLevelSaveData(entry.traitId, level);
            byId.Add(normalizedEntry.traitId, normalizedEntry);
            normalized.Add(normalizedEntry);
        }

        return normalized;
    }

    private static List<SectorTechnologyLevelSaveData> NormalizeSectorTechnologyLevels(
        List<SectorTechnologyLevelSaveData> source)
    {
        Dictionary<string, int> levelsById = new Dictionary<string, int>(StringComparer.Ordinal);

        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                SectorTechnologyLevelSaveData entry = source[i];
                if (entry == null ||
                    !SectorTechnologyCatalog.TryGet(entry.technologyId, out SectorTechnologyDefinition definition))
                {
                    continue;
                }

                int level = Mathf.Clamp(entry.level, 0, definition.MaxLevel);
                if (!levelsById.TryGetValue(definition.Id, out int existingLevel) || level > existingLevel)
                {
                    levelsById[definition.Id] = level;
                }
            }
        }

        List<SectorTechnologyLevelSaveData> normalized = new List<SectorTechnologyLevelSaveData>();
        IReadOnlyList<SectorTechnologyDefinition> definitions = SectorTechnologyCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorTechnologyDefinition definition = definitions[i];
            int level = levelsById.TryGetValue(definition.Id, out int savedLevel) ? savedLevel : 0;
            normalized.Add(new SectorTechnologyLevelSaveData(definition.Id, level));
        }

        return normalized;
    }

    private static bool HasPositiveSectorTechnologyLevel(List<SectorTechnologyLevelSaveData> entries)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            SectorTechnologyLevelSaveData entry = entries[i];
            if (entry != null && entry.level > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> NormalizeUniqueStrings(List<string> source)
    {
        List<string> normalized = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        if (source == null)
        {
            return normalized;
        }

        for (int i = 0; i < source.Count; i++)
        {
            string value = source[i];
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static List<CampaignBossId> NormalizeBossIds(List<CampaignBossId> source)
    {
        List<CampaignBossId> normalized = new List<CampaignBossId>();
        HashSet<CampaignBossId> seen = new HashSet<CampaignBossId>();

        if (source == null)
        {
            return normalized;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CampaignBossId bossId = source[i];
            if (bossId != CampaignBossId.None &&
                Enum.IsDefined(typeof(CampaignBossId), bossId) &&
                seen.Add(bossId))
            {
                normalized.Add(bossId);
            }
        }

        return normalized;
    }

    private static List<BossStoryPart> NormalizeStoryParts(List<BossStoryPart> source)
    {
        List<BossStoryPart> normalized = new List<BossStoryPart>();
        HashSet<BossStoryPart> seen = new HashSet<BossStoryPart>();

        if (source == null)
        {
            return normalized;
        }

        for (int i = 0; i < source.Count; i++)
        {
            BossStoryPart storyPart = source[i];
            if (storyPart != BossStoryPart.None &&
                Enum.IsDefined(typeof(BossStoryPart), storyPart) &&
                seen.Add(storyPart))
            {
                normalized.Add(storyPart);
            }
        }

        return normalized;
    }

    private static int FindTraitLevel(List<TraitLevelSaveData> traitLevels, string traitId)
    {
        for (int i = 0; i < traitLevels.Count; i++)
        {
            TraitLevelSaveData entry = traitLevels[i];
            if (entry.traitId == traitId)
            {
                return entry.level;
            }
        }

        return 0;
    }

    private static void AddDefaultBuildings(List<BuildingSaveData> target)
    {
        target.Add(new BuildingSaveData(BuildingType.Hangar, 0));
        target.Add(new BuildingSaveData(BuildingType.EngineWorkshop, 0));
        target.Add(new BuildingSaveData(BuildingType.WeaponLab, 0));
        target.Add(new BuildingSaveData(BuildingType.RecoveryProcessor, 0));
    }
}
