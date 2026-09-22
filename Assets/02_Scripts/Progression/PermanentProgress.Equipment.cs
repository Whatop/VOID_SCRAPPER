using System.Collections.Generic;
using UnityEngine;

public enum EquipmentDevelopmentResult
{
    Success, InvalidDefinition, ResearchLocked, AlreadyManufactured, InvalidRecipe,
    InsufficientResources, UnsafeState, SaveFailed, NotManufactured
}

public partial class PermanentProgress
{
    [Header("Manufactured Expedition Equipment")]
    [SerializeField] private TraitCatalog equipmentCatalog;
    [SerializeField] private List<ShipDefinition> equipmentShips = new List<ShipDefinition>();
    [SerializeField] private List<string> equipmentLoadoutTraitIds = new List<string>();
    [SerializeField] private List<string> manufacturedEquipmentIds = new List<string>();
    private List<string> grandfatheredEquipmentResearchIds = new List<string>();
    private bool equipmentRosterMigrationPending;
    private bool equipmentTransactionInProgress;

    public TraitCatalog EquipmentCatalog => equipmentCatalog;
    public IReadOnlyList<string> EquipmentLoadoutTraitIds => equipmentLoadoutTraitIds.AsReadOnly();
    public IReadOnlyList<string> ManufacturedEquipmentIds => manufacturedEquipmentIds.AsReadOnly();

    // Depth is granted by the completed Settlement analysis transaction, not by part pickup.
    public const string FinalComponentAnalyzedFlag = "campaign_phase_navigation_lens_analyzed";

    // The last analysis opens no further expedition depth, so its completion uses the existing flag store.
    public int AnalyzedEquipmentComponentCount
    {
        get
        {
            if (HasAllRouteCoreParts && HasUnlockFlag(FinalComponentAnalyzedFlag)) return 3;
            if (!HasBossStoryPart(BossStoryPart.SectorStabilizer) || highestUnlockedDepth < ExpeditionDepth.DeepZone1) return 0;
            if (!HasBossStoryPart(BossStoryPart.MatterCompressor) || highestUnlockedDepth < ExpeditionDepth.DeepZone2) return 1;
            return 2;
        }
    }

    public int GetEquipmentResearchPositionCount(ShipTraitBranchKind branch)
    {
        int required = branch == ShipTraitBranchKind.Shotgun ? 1 : branch == ShipTraitBranchKind.Sniper ? 2 : 0;
        if (branch < ShipTraitBranchKind.Shared || branch > ShipTraitBranchKind.Shotgun ||
            AnalyzedEquipmentComponentCount < required) return 0;
        return 3 * (1 + AnalyzedEquipmentComponentCount);
    }

    // Called by the same natural Settlement analysis completion transaction as route authorization.
    public bool TryCompleteFinalComponentAnalysis()
    {
        if (!HasAllRouteCoreParts || highestUnlockedDepth < ExpeditionDepth.DeepZone2 ||
            !HasDefeatedCampaignBoss(CampaignBossId.PhaseGatekeeper) ||
            !AddUniqueString(unlockFlags, FinalComponentAnalyzedFlag)) return false;
        RefreshEquipmentResearch();
        Changed?.Invoke();
        return true;
    }

    public ShipDefinition GetEquipmentShip(TraitDefinition trait)
    {
        if (trait == null || trait.Category == TraitCategory.Shared) return null;
        foreach (ShipDefinition ship in equipmentShips)
            if (ship != null && ship.DefaultWeaponTree == trait.WeaponTreeType) return ship;
        return null;
    }

    public bool IsEquipmentPrepared(string traitId)
    {
        TraitDefinition trait = equipmentCatalog != null ? equipmentCatalog.FindById(traitId) : null;
        return IsEquipmentFitted(traitId) && IsEquipmentUsable(trait, lastSelectedWeaponTree);
    }

    public bool IsEquipmentFitted(string id) => !string.IsNullOrWhiteSpace(id) && equipmentLoadoutTraitIds.Contains(id);
    public bool IsEquipmentManufactured(string id) => !string.IsNullOrWhiteSpace(id) && manufacturedEquipmentIds.Contains(id);

    public bool IsKnownDevelopmentEquipment(TraitDefinition trait)
    {
        return trait != null && equipmentCatalog != null && equipmentCatalog.FindById(trait.TraitId) == trait &&
            trait.HasValidDevelopmentMetadata;
    }

    public bool IsEquipmentResearched(TraitDefinition trait)
    {
        if (!IsKnownDevelopmentEquipment(trait)) return false;
        // Displaced Shared modules remain usable by existing owners; they have no sale position.
        if (!trait.IsDevelopmentRoster && trait.Category == TraitCategory.Shared && IsEquipmentManufactured(trait.TraitId)) return true;
        int tier = trait.DevelopmentResearchTier;
        if (trait.PreviousDevelopmentResearchTier >= 0 && grandfatheredEquipmentResearchIds.Contains(trait.TraitId))
            tier = Mathf.Min(tier, trait.PreviousDevelopmentResearchTier);
        if (GetEquipmentResearchPositionCount(trait.DevelopmentBranch) <= tier * 3) return false;
        ShipDefinition ship = GetEquipmentShip(trait);
        return trait.Category == TraitCategory.Shared || (ship != null && (ship.UnlockedByDefault ||
            (AnalyzedEquipmentComponentCount >= ship.RequiredAnalyzedComponents && HasUnlockFlag(ship.UnlockFlag))));
    }

    public bool IsEquipmentUsable(TraitDefinition trait, WeaponTreeType weapon)
    {
        return IsEquipmentResearched(trait) && IsEquipmentManufactured(trait.TraitId) && trait.IsAvailableFor(weapon);
    }

    public void AppendEffectiveEquipment(List<string> target, WeaponTreeType weapon)
    {
        if (target == null || equipmentCatalog == null) return;
        foreach (string id in equipmentLoadoutTraitIds)
        {
            TraitDefinition trait = equipmentCatalog.FindById(id);
            if (IsEquipmentUsable(trait, weapon) && !target.Contains(id)) target.Add(id);
        }
    }

    public bool CanEditEquipment => !equipmentTransactionInProgress && GameStateManager.Instance != null &&
        GameStateManager.Instance.CurrentState == GameState.Settlement &&
        !(RunManager.Instance != null && RunManager.Instance.HasActiveRun) &&
        !(SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLoading) &&
        !SettlementExpeditionLaunchGuard.IsDialogueActive && !GameplayPauseManager.IsPaused;

    public EquipmentDevelopmentResult GetManufacturingAvailability(TraitDefinition trait)
    {
        if (!IsKnownDevelopmentEquipment(trait) || !trait.IsDevelopmentRoster) return EquipmentDevelopmentResult.InvalidDefinition;
        if (!IsEquipmentResearched(trait)) return EquipmentDevelopmentResult.ResearchLocked;
        if (IsEquipmentManufactured(trait.TraitId)) return EquipmentDevelopmentResult.AlreadyManufactured;
        if (!trait.HasValidManufacturingRecipe) return EquipmentDevelopmentResult.InvalidRecipe;
        return CanSpend(trait.ManufacturingScrapCost, trait.ManufacturingCoreCost) ? EquipmentDevelopmentResult.Success : EquipmentDevelopmentResult.InsufficientResources;
    }

    public EquipmentDevelopmentResult TryManufactureEquipment(TraitDefinition trait)
    {
        if (!CanEditEquipment) return EquipmentDevelopmentResult.UnsafeState;
        EquipmentDevelopmentResult availability = GetManufacturingAvailability(trait);
        if (availability != EquipmentDevelopmentResult.Success) return availability;
        equipmentTransactionInProgress = true;
        try
        {
            SaveData snapshot = CreateSaveData();
            snapshot.scrapParts -= trait.ManufacturingScrapCost;
            snapshot.coreShards -= trait.ManufacturingCoreCost;
            snapshot.manufacturedEquipmentIds.Add(trait.TraitId);
            if (!SaveEquipmentTransaction(snapshot)) return EquipmentDevelopmentResult.SaveFailed;
            scrapParts = snapshot.scrapParts;
            coreShards = snapshot.coreShards;
            manufacturedEquipmentIds = snapshot.manufacturedEquipmentIds;
        }
        finally { equipmentTransactionInProgress = false; }
        Changed?.Invoke();
        return EquipmentDevelopmentResult.Success;
    }

    public EquipmentDevelopmentResult TrySetEquipmentFitted(TraitDefinition trait, bool fitted)
    {
        if (!CanEditEquipment) return EquipmentDevelopmentResult.UnsafeState;
        if (!IsKnownDevelopmentEquipment(trait)) return EquipmentDevelopmentResult.InvalidDefinition;
        if (!IsEquipmentManufactured(trait.TraitId)) return EquipmentDevelopmentResult.NotManufactured;
        if (fitted && !IsEquipmentResearched(trait)) return EquipmentDevelopmentResult.ResearchLocked;
        if (IsEquipmentFitted(trait.TraitId) == fitted) return EquipmentDevelopmentResult.Success;
        equipmentTransactionInProgress = true;
        try
        {
            SaveData snapshot = CreateSaveData();
            if (fitted) snapshot.equipmentLoadoutTraitIds.Add(trait.TraitId);
            else snapshot.equipmentLoadoutTraitIds.Remove(trait.TraitId);
            if (!SaveEquipmentTransaction(snapshot)) return EquipmentDevelopmentResult.SaveFailed;
            equipmentLoadoutTraitIds = snapshot.equipmentLoadoutTraitIds;
        }
        finally { equipmentTransactionInProgress = false; }
        Changed?.Invoke();
        return EquipmentDevelopmentResult.Success;
    }

    private bool SaveEquipmentTransaction(SaveData snapshot)
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("Equipment transaction requires the authored CoreRoot/SaveManager. Resources and ownership were not changed.", this);
            return false;
        }
        SaveManager.Instance.Save(snapshot);
        return ReferenceEquals(SaveManager.Instance.CurrentSaveData, snapshot);
    }

    private void LoadEquipmentOwnership(SaveData data)
    {
        equipmentLoadoutTraitIds = CopyEquipmentIds(data.equipmentLoadoutTraitIds);
        manufacturedEquipmentIds = CopyEquipmentIds(data.manufacturedEquipmentIds);
        grandfatheredEquipmentResearchIds = CopyEquipmentIds(data.grandfatheredEquipmentResearchIds);
        bool migrate = data.version < 6 || data.equipmentOwnershipMigrationPending;
        if (migrate && equipmentCatalog != null)
        {
            foreach (string id in equipmentLoadoutTraitIds)
            {
                TraitDefinition trait = equipmentCatalog.FindById(id);
                if (trait != null && trait.CanAppearAsRandomDropTrait) AddUniqueString(manufacturedEquipmentIds, id);
            }
            if (data.traitLevels != null)
                foreach (TraitLevelSaveData state in data.traitLevels)
                {
                    TraitDefinition trait = state != null ? equipmentCatalog.FindById(state.traitId) : null;
                    if (trait != null && trait.CanAppearAsRandomDropTrait && state.level > 0)
                        AddUniqueString(manufacturedEquipmentIds, trait.TraitId);
                }
        }
        equipmentOwnershipMigrationPending = migrate && equipmentCatalog == null;
        bool migrateRoster = data.version < 7 || data.equipmentRosterMigrationPending;
        if (migrateRoster && equipmentCatalog != null)
            foreach (string id in manufacturedEquipmentIds)
            {
                TraitDefinition trait = equipmentCatalog.FindById(id);
                if (trait != null && trait.CanAppearAsRandomDropTrait && trait.PreviousDevelopmentResearchTier >= 0)
                    AddUniqueString(grandfatheredEquipmentResearchIds, id);
            }
        equipmentRosterMigrationPending = migrateRoster && equipmentCatalog == null;
        if (equipmentOwnershipMigrationPending)
            Debug.LogError("PermanentProgress.equipmentCatalog is missing; equipment migration is deferred and saved IDs retained.", this);
        if (equipmentCatalog != null)
        {
            var checkedIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string id in equipmentLoadoutTraitIds)
                if (checkedIds.Add(id) && equipmentCatalog.FindById(id) == null) ReportUnknownEquipment(id);
            foreach (string id in manufacturedEquipmentIds)
                if (checkedIds.Add(id) && equipmentCatalog.FindById(id) == null) ReportUnknownEquipment(id);
        }
    }

    private bool equipmentOwnershipMigrationPending;

    private void ReportUnknownEquipment(string id) => Debug.LogWarning(
        "Saved equipment ID '" + id + "' is absent from PermanentProgress.equipmentCatalog. Retained for recovery, excluded from deployment.", this);

    private static List<string> CopyEquipmentIds(List<string> source)
    {
        var result = new List<string>();
        if (source != null)
            foreach (string id in source) if (!string.IsNullOrWhiteSpace(id) && !result.Contains(id)) result.Add(id);
        return result;
    }

    private void RefreshEquipmentResearch()
    {
        // Recompute only these authored research ship flags; backward QA checkpoints cannot retain them.
        for (int i = 0; i < equipmentShips.Count; i++)
        {
            ShipDefinition ship = equipmentShips[i];
            if (ship == null || ship.RequiredAnalyzedComponents <= 0) continue;
            if (AnalyzedEquipmentComponentCount >= ship.RequiredAnalyzedComponents) AddUniqueString(unlockFlags, ship.UnlockFlag);
            else unlockFlags.Remove(ship.UnlockFlag);
        }
    }

    private void ValidateEquipmentShipSelection()
    {
        // Older saves used the bootstrap alias rather than the authored default ship ID.
        if (SelectedShipId == "basic_ship")
            for (int i = 0; i < equipmentShips.Count; i++)
                if (equipmentShips[i] != null && equipmentShips[i].UnlockedByDefault)
                {
                    selectedShipId = equipmentShips[i].ShipId;
                    break;
                }
        for (int i = 0; i < equipmentShips.Count; i++)
            if (equipmentShips[i] != null && equipmentShips[i].ShipId == SelectedShipId)
            {
                lastSelectedWeaponTree = equipmentShips[i].DefaultWeaponTree;
                break;
            }
    }
}
