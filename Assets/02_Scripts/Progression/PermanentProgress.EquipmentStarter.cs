using UnityEngine;

public partial class PermanentProgress
{
    // Uses the existing durable flag store; never reset by campaign QA checkpoints.
    public const string EquipmentStarterMaterialsFlag = "equipment_starter_materials_granted";

    public bool TryGetEquipmentStarterAllowance(out int scrap, out int core)
    {
        scrap = core = 0;
        if (equipmentCatalog == null) return false;
        long scrapTotal = 0, coreTotal = 0;
        int shared = 0, sweeper = 0;
        foreach (TraitDefinition trait in equipmentCatalog.TraitDefinitions)
        {
            if (trait == null || !trait.IsDevelopmentRoster || trait.DevelopmentResearchTier != 0 ||
                (trait.DevelopmentBranch != ShipTraitBranchKind.Shared && trait.DevelopmentBranch != ShipTraitBranchKind.MachineGun)) continue;
            if (!trait.HasValidDevelopmentMetadata || !trait.HasValidManufacturingRecipe) return false;
            if (trait.DevelopmentBranch == ShipTraitBranchKind.Shared) shared++; else sweeper++;
            scrapTotal += trait.ManufacturingScrapCost;
            coreTotal += trait.ManufacturingCoreCost;
        }
        // Six real blueprints, plus a rounded-up 15% buffer. Zero-cost currencies stay absent.
        scrapTotal += (scrapTotal * 15 + 99) / 100;
        coreTotal += (coreTotal * 15 + 99) / 100;
        if (shared != 3 || sweeper != 3 || scrapTotal > int.MaxValue || coreTotal > int.MaxValue) return false;
        scrap = (int)scrapTotal;
        core = (int)coreTotal;
        return true;
    }

    public bool TryGrantEquipmentStarterMaterials()
    {
        // Natural dialogue completion may still hold its pause. Eligibility is story completion,
        // not UI interaction, and therefore intentionally does not use CanEditEquipment.
        if (equipmentTransactionInProgress || !IsTutorialCompleted ||
            !HasUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag) || HasUnlockFlag(EquipmentStarterMaterialsFlag) ||
            GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Settlement ||
            (RunManager.Instance != null && RunManager.Instance.HasActiveRun)) return false;
        if (!TryGetEquipmentStarterAllowance(out int scrap, out int core))
        {
            Debug.LogError("PermanentProgress.equipmentCatalog: starter materials require three valid Shared and three Sweeper Row-1 manufacturing recipes.", this);
            return false;
        }
        equipmentTransactionInProgress = true;
        try
        {
            SaveData snapshot = CreateSaveData();
            snapshot.scrapParts = (int)System.Math.Min(int.MaxValue, (long)snapshot.scrapParts + scrap);
            snapshot.coreShards = (int)System.Math.Min(int.MaxValue, (long)snapshot.coreShards + core);
            snapshot.unlockFlags.Add(EquipmentStarterMaterialsFlag);
            // Resources and receipt are persisted together before either becomes visible.
            if (!SaveEquipmentTransaction(snapshot)) return false;
            scrapParts = snapshot.scrapParts;
            coreShards = snapshot.coreShards;
            unlockFlags = snapshot.unlockFlags;
        }
        finally { equipmentTransactionInProgress = false; }
        Changed?.Invoke();
        return true;
    }
}
