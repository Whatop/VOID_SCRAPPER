using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    private static readonly string[] ResearchSpecialIds =
    {
        "special_sector_stabilization", "special_matter_compression", "special_phase_navigation"
    };

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)]
    [TestCase(3, 1)] [TestCase(4, 1)] [TestCase(5, 2)] [TestCase(6, 2)]
    [TestCase(7, 3)] [TestCase(8, 3)] [TestCase(9, 3)] [TestCase(10, 3)] [TestCase(11, 3)]
    public void StoryPickupAndAnalysisUnlockExactlyTheCorrespondingSpecialBlueprint(int checkpoint, int count)
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), checkpoint));
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        for (int i = 0; i < ResearchSpecialIds.Length; i++)
        {
            var trait = catalog.FindById(ResearchSpecialIds[i]);
            Assert.That(progress.IsEquipmentResearched(trait), Is.EqualTo(i < count));
            Assert.That(progress.HasCompletedStoryPartAnalysis((BossStoryPart)(i + 1)), Is.EqualTo(i < count));
            Assert.That(progress.IsEquipmentManufactured(trait.TraitId), Is.False);
            Assert.That(progress.IsEquipmentFitted(trait.TraitId), Is.False);
            Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(trait), Is.False);
            if (i >= count)
                Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.ResearchLocked));
        }
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(progress.HasCompletedStoryPartAnalysis(BossStoryPart.None), Is.False);
        Assert.That(progress.HasCompletedStoryPartAnalysis((BossStoryPart)99), Is.False);
    }

    [TestCaseSource(nameof(ResearchSpecialIds))]
    public void SpecialManufacturesOnceFitsFreelyAndSurvivesDiskReload(string id)
    {
        var trait = catalog.FindById(id);
        Assert.That(trait.Rarity, Is.EqualTo(TraitRarity.Special));
        Assert.That(trait.Category, Is.EqualTo(TraitCategory.Shared));
        Assert.That(trait.IsResearchSpecialEquipment && trait.IsManufacturableBlueprint, Is.True);
        Assert.That(trait.IsDevelopmentRoster || trait.IsPersistentStoryTrait, Is.False);
        Assert.That(trait.HasValidDevelopmentMetadata && trait.HasValidManufacturingRecipe, Is.True);
        Assert.That(trait.CanFieldDrop && trait.CanDismantle, Is.True);
        Assert.That(trait.MaxLevel, Is.EqualTo(3));
        Assert.That(trait.Icon, Is.Not.Null);
        ResearchAndResources();
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.IsEquipmentFitted(id), Is.False);
        int scrap = progress.ScrapParts, core = progress.CoreShards;
        Assert.That(scrap, Is.EqualTo(9999 - trait.ManufacturingScrapCost));
        Assert.That(core, Is.EqualTo(9999 - trait.ManufacturingCoreCost));
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.AlreadyManufactured));
        Assert.That(progress.TrySetEquipmentFitted(trait, true), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.TrySetEquipmentFitted(trait, false), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.TrySetEquipmentFitted(trait, true), Is.EqualTo(EquipmentDevelopmentResult.Success));
        for (int i = 0; i < 3; i++)
        {
            progress.LoadFromSave(save.LoadOrCreate());
            Assert.That(progress.ManufacturedEquipmentIds.Count(x => x == id), Is.EqualTo(1));
            Assert.That(progress.IsEquipmentFitted(id), Is.True);
            Assert.That(progress.ScrapParts, Is.EqualTo(scrap));
            Assert.That(progress.CoreShards, Is.EqualTo(core));
        }
        Assert.That(store.GetLevel(id), Is.Zero, "Permanent ownership does not manufacture runtime levels.");
    }

    [TestCase(3)] [TestCase(5)] [TestCase(7)]
    public void AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(int checkpoint)
    {
        var data = Checkpoint(new SaveData(), checkpoint);
        // Older assembled saves did not carry the final analysis flag; existing load normalization owns this.
        data.unlockFlags.Remove(PermanentProgress.FinalComponentAnalyzedFlag);
        data.scrapParts = 127; data.coreShards = 13; data.stabilizedAlloy = 7;
        data.manufacturedEquipmentIds.Add("shared_salvage_protocol");
        data.equipmentLoadoutTraitIds.Add("shared_salvage_protocol");
        progress.LoadFromSave(data); save.Save(progress);
        string normalized = JsonUtility.ToJson(progress.CreateSaveData());
        for (int reload = 0; reload < 3; reload++)
        {
            progress.LoadFromSave(save.LoadOrCreate()); save.Save(progress);
            Assert.That(progress.IsEquipmentResearched(catalog.FindById(ResearchSpecialIds[checkpoint == 3 ? 0 : checkpoint == 5 ? 1 : 2])), Is.True);
            Assert.That(progress.ManufacturedEquipmentIds, Is.EqualTo(new[] { "shared_salvage_protocol" }));
            Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { "shared_salvage_protocol" }));
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(normalized));
        }
    }

    [TestCase(2, 0)] [TestCase(4, 1)]
    public void NaturalRouteAnalysisUnlocksItsModuleWithoutGrantingEquipment(int checkpoint, int index)
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), checkpoint));
        var trait = catalog.FindById(ResearchSpecialIds[index]);
        Assert.That(progress.IsEquipmentResearched(trait), Is.False);
        Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.True);
        Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.False);
        Assert.That(progress.IsEquipmentResearched(trait), Is.True);
        Assert.That(progress.ManufacturedEquipmentIds, Is.Empty);
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.Empty);
    }

    [Test]
    public void FinalNaturalAnalysisUnlocksLensWithoutManufacturingIt()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 6));
        var lens = catalog.FindById(ResearchSpecialIds[2]);
        Assert.That(progress.HasBossStoryPart(BossStoryPart.PhaseNavigationLens), Is.True);
        Assert.That(progress.IsEquipmentResearched(lens), Is.False);
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.True);
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.False);
        Assert.That(progress.IsEquipmentResearched(lens), Is.True);
        Assert.That(progress.ManufacturedEquipmentIds, Is.Empty);
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.Empty);
    }

    [Test]
    public void SpecialsShareOneFittingFlowAcrossEveryShipWithoutReplacingTheFinalRoster()
    {
        ResearchAndResources();
        foreach (string id in ResearchSpecialIds) ManufactureAndFit(catalog.FindById(id));
        foreach (ShipDefinition ship in ships)
        {
            progress.SetSelectedShipId(ship.ShipId);
            var fitted = new List<string>(); progress.AppendEffectiveEquipment(fitted, ship.DefaultWeaponTree);
            Assert.That(fitted, Is.EquivalentTo(ResearchSpecialIds));
        }
        Assert.That(catalog.TraitDefinitions.Count(t => t.IsDevelopmentRoster), Is.EqualTo(48));
        Assert.That(catalog.TraitDefinitions.Count(t => t.IsResearchSpecialEquipment), Is.EqualTo(3));
        Assert.That(progress.TryGetEquipmentStarterAllowance(out int scrap, out int core), Is.True);
        Assert.That(scrap, Is.EqualTo(86)); Assert.That(core, Is.Zero);
    }

    [Test]
    public void SpecialsDeployUpgradeAndReconstructWithoutStackingOrRestoringDepletedVitals()
    {
        ResearchAndResources();
        var definitions = ResearchSpecialIds.Select(catalog.FindById).ToArray();
        foreach (var trait in definitions) ManufactureAndFit(trait);
        RunManager manager = StartRunFixture(); RunContext run = manager.CurrentRun;
        Assert.That(run.PreparedEquipmentIds, Is.EquivalentTo(ResearchSpecialIds));
        GameObject player = ReconstructPlayer(run);
        foreach (var trait in definitions)
        {
            Assert.That(store.GetLevel(trait.TraitId), Is.EqualTo(1));
            Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(trait), Is.True);
            Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out int oldLevel, out int level), Is.True);
            Assert.That(oldLevel, Is.EqualTo(1)); Assert.That(level, Is.EqualTo(2));
        }
        var beforeBonus = player.GetComponent<PlayerRuntimeBonusState>();
        float heal = beforeBonus.HealEfficiencyMultiplier, harvest = beforeBonus.HarvestYieldMultiplier;
        float pickup = beforeBonus.PickupRangeBonus, radar = beforeBonus.RadarScanRadiusBonus;
        Assert.That(heal, Is.EqualTo(1.05f * 1.05f).Within(.0001f));
        Assert.That(pickup, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(radar, Is.EqualTo(2f));
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(ships[0].CargoCapacity + 12));
        var weapon = player.GetComponent<PlayerWeaponModifiers>();
        float range = weapon.RangeMultiplier, spread = weapon.SpreadMultiplier;
        float maxHp = player.GetComponent<PlayerHealth>().MaxHp;
        player.GetComponent<PlayerRuntimeStatApplier>().RestoreCurrentVitals(3, 0);
        run.Wallet.Add(CurrencyType.ScrapParts, 17);
        run.CapturePlayerVitals(3, 0); run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        // Out-of-band save changes cannot modify the accepted run fitting.
        SaveData changed = progress.CreateSaveData(); changed.equipmentLoadoutTraitIds.Clear(); progress.LoadFromSave(changed);
        GameObject next = ReconstructPlayer(run);
        var bonus = next.GetComponent<PlayerRuntimeBonusState>();
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(next.GetComponent<PlayerHealth>().MaxHp, Is.EqualTo(maxHp));
        Assert.That(next.GetComponent<PlayerArmor>().CurrentArmor, Is.Zero);
        Assert.That(bonus.HealEfficiencyMultiplier, Is.EqualTo(heal).Within(.0001f));
        Assert.That(bonus.HarvestYieldMultiplier, Is.EqualTo(harvest).Within(.0001f));
        Assert.That(bonus.PickupRangeBonus, Is.EqualTo(pickup).Within(.0001f));
        Assert.That(bonus.RadarScanRadiusBonus, Is.EqualTo(radar));
        Assert.That(next.GetComponent<PlayerWeaponModifiers>().RangeMultiplier, Is.EqualTo(range).Within(.0001f));
        Assert.That(next.GetComponent<PlayerWeaponModifiers>().SpreadMultiplier, Is.EqualTo(spread).Within(.0001f));
        Assert.That(run.Wallet.GetAmount(CurrencyType.ScrapParts), Is.EqualTo(17));
        foreach (var trait in definitions)
        {
            Assert.That(store.GetLevel(trait.TraitId), Is.EqualTo(2));
            Assert.That(RunTraitAcquisitionService.TryAcquire(trait, next, out _, out _), Is.True);
            Assert.That(store.CanUpgrade(trait), Is.False);
        }
        Assert.That(RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3), Is.Empty);
        manager.AbandonActiveRunWithoutRewards();
        Assert.That(store.TraitLevels, Is.Empty);
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        Assert.That(manager.CurrentRun.PreparedEquipmentIds, Is.Empty);
        Assert.That(store.TraitLevels, Is.Empty);
        manager.AbandonActiveRunWithoutRewards();
        changed.equipmentLoadoutTraitIds.AddRange(ResearchSpecialIds); progress.LoadFromSave(changed);
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        Assert.That(manager.CurrentRun.PreparedEquipmentIds, Is.EquivalentTo(ResearchSpecialIds));
        foreach (string id in ResearchSpecialIds) Assert.That(store.GetLevel(id), Is.EqualTo(1));
    }

    [Test]
    public void SharedSpecialSubsectionIsDiscoverableAndUsesNormalInspectionAndManufacturing()
    {
        OpenEquipment();
        string selectedShip = progress.SelectedShipId;
        panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
        var special = Get<Button>(panel, "researchSpecialEquipmentButton");
        Assert.That(special.gameObject.activeSelf, Is.True);
        panel.ToggleResearchSpecialEquipment();
        var views = Get<PreparedEquipmentView[]>(panel, "equipmentSlots");
        Assert.That(views.Where(v => v.button.gameObject.activeSelf).Select(v => v.definition.TraitId), Is.EquivalentTo(ResearchSpecialIds));
        var first = catalog.FindById(ResearchSpecialIds[0]);
        panel.InspectEquipment(first);
        Assert.That(Get<Button>(panel, "equipmentActivationButton").gameObject.activeSelf, Is.False);
        ResearchAndResources(3); panel.RefreshPanel(); panel.InspectEquipment(first);
        Assert.That(Get<TMP_Text>(panel, "equipmentCompatibility").text, Does.Contain(first.GetRarityText()));
        Assert.That(Get<TMP_Text>(panel, "equipmentCompatibility").color, Is.EqualTo(first.GetRarityColor()));
        Assert.That(Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows")[0].effects.text, Does.Contain("<color=#"));
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.IsEquipmentFitted(first.TraitId), Is.False);
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.IsEquipmentFitted(first.TraitId), Is.True);
        panel.SelectEquipmentBranch(ShipTraitBranchKind.MachineGun);
        Assert.That(special.gameObject.activeSelf, Is.False);
        Assert.That(progress.SelectedShipId, Is.EqualTo(selectedShip));
        panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
        Assert.That(views.Count(v => v.button.gameObject.activeSelf), Is.EqualTo(12));
        Assert.That(views.All(v => v.definition.IsDevelopmentRoster), Is.True);
    }
}
