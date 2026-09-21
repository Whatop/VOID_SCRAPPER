using System.Collections.Generic;
using UnityEngine;

public partial class PermanentProgress
{
    [Header("Prepared Expedition Equipment")]
    [SerializeField] private TraitCatalog equipmentCatalog;
    [SerializeField] private List<ShipDefinition> equipmentShips = new List<ShipDefinition>();
    [SerializeField] private List<string> equipmentLoadoutTraitIds = new List<string>();

    public TraitCatalog EquipmentCatalog => equipmentCatalog;
    public IReadOnlyList<string> EquipmentLoadoutTraitIds => equipmentLoadoutTraitIds.AsReadOnly();

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

    public int EquipmentLoadoutCapacity => 3 * (1 + AnalyzedEquipmentComponentCount);

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
        return !string.IsNullOrWhiteSpace(traitId) && equipmentLoadoutTraitIds.Contains(traitId);
    }

    public bool CanPrepareEquipment(TraitDefinition trait)
    {
        return trait != null && equipmentCatalog != null && equipmentCatalog.FindById(trait.TraitId) == trait &&
            trait.CanAppearAsRandomDropTrait && trait.IsAvailableFor(lastSelectedWeaponTree);
    }

    public bool TryPrepareEquipment(int slot, TraitDefinition trait)
    {
        if (slot < 0 || slot >= EquipmentLoadoutCapacity || (trait != null && !CanPrepareEquipment(trait))) return false;
        string id = trait != null ? trait.TraitId : string.Empty;
        if (!string.IsNullOrEmpty(id))
            for (int i = 0; i < equipmentLoadoutTraitIds.Count; i++)
                if (i != slot && equipmentLoadoutTraitIds[i] == id) return false;
        ValidateEquipmentLoadout();
        if (equipmentLoadoutTraitIds[slot] == id) return true;
        equipmentLoadoutTraitIds[slot] = id;
        Changed?.Invoke();
        return true;
    }

    private void ValidateEquipmentLoadout()
    {
        equipmentLoadoutTraitIds ??= new List<string>();
        int capacity = EquipmentLoadoutCapacity;
        if (equipmentLoadoutTraitIds.Count > capacity) equipmentLoadoutTraitIds.RemoveRange(capacity, equipmentLoadoutTraitIds.Count - capacity);
        while (equipmentLoadoutTraitIds.Count < capacity) equipmentLoadoutTraitIds.Add(string.Empty);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < equipmentLoadoutTraitIds.Count; i++)
        {
            string id = equipmentLoadoutTraitIds[i];
            TraitDefinition trait = equipmentCatalog != null ? equipmentCatalog.FindById(id) : null;
            if (!CanPrepareEquipment(trait) || !seen.Add(id)) equipmentLoadoutTraitIds[i] = string.Empty;
        }
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
        ValidateEquipmentLoadout();
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
        ValidateEquipmentLoadout();
    }
}
