using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    [Test]
    public void StarterMaterialsRequireTutorialAndCompletedSettlementIntroduction()
    {
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False);
        progress.TryMarkTutorialCompleted();
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False, "An interrupted introduction is not eligible.");
        progress.AddUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag);
        Set(state, "currentState", GameState.Tutorial);
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False);
        Set(state, "currentState", GameState.Settlement);
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.True);
    }

    [Test]
    public void NaturalIntroductionGrantsOnceAndSavedReentryCannotRepeatIt()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 0));
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Assert.That(controller.TryCompleteFirstSettlementStory(), Is.True);
        Assert.That(progress.TryGetEquipmentStarterAllowance(out int scrap, out int core), Is.True);
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap));
        Assert.That(progress.CoreShards, Is.EqualTo(core));
        Assert.That(progress.HasUnlockFlag(PermanentProgress.EquipmentStarterMaterialsFlag), Is.True);
        for (int i = 0; i < 3; i++)
        {
            progress.LoadFromSave(save.LoadOrCreate());
            controller.TryCompleteFirstSettlementStory();
            Call(controller, "Start");
            Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False);
            Assert.That(progress.ScrapParts, Is.EqualTo(scrap));
        }
        Assert.That(progress.ManufacturedEquipmentIds, Is.Empty);
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.Empty);
    }

    [Test]
    public void OlderCompletedSaveReceivesAllowanceOnSettlementEntryButNeverOnPanelOpen()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 1));
        OpenEquipment();
        Assert.That(progress.ScrapParts, Is.Zero);
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Call(controller, "Start");
        Assert.That(progress.ScrapParts, Is.EqualTo(86));
        Assert.That(save.CurrentSaveData.unlockFlags, Does.Contain(PermanentProgress.EquipmentStarterMaterialsFlag));
        foreach (int checkpoint in Enumerable.Range(0, 12))
        {
            progress.LoadFromSave(Checkpoint(progress.CreateSaveData(), checkpoint));
            Assert.That(progress.HasUnlockFlag(PermanentProgress.EquipmentStarterMaterialsFlag), Is.True);
            Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False, "QA rollback cannot mint materials.");
        }
    }

    [Test]
    public void FailedStarterSaveDoesNotMutateResourcesOrReceiptAndCanRetry()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 1));
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        typeof(SaveManager).GetProperty("Instance").SetValue(null, null);
        LogAssert.Expect(LogType.Error, "Equipment transaction requires the authored CoreRoot/SaveManager. Resources and ownership were not changed.");
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        typeof(SaveManager).GetProperty("Instance").SetValue(null, save);
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.True);
    }

    [Test]
    public void StarterBudgetCoversExactlySixInitialBlueprintsWithFifteenPercentRoundedBuffer()
    {
        var initial = catalog.TraitDefinitions.Where(t => t.IsDevelopmentRoster && t.DevelopmentResearchTier == 0 &&
            (t.DevelopmentBranch == ShipTraitBranchKind.Shared || t.DevelopmentBranch == ShipTraitBranchKind.MachineGun)).ToArray();
        Assert.That(initial.Length, Is.EqualTo(6));
        Assert.That(initial.Sum(t => t.ManufacturingScrapCost), Is.EqualTo(74));
        Assert.That(initial.Sum(t => t.ManufacturingCoreCost), Is.Zero);
        progress.LoadFromSave(Checkpoint(new SaveData(), 1));
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.True);
        Assert.That(progress.ScrapParts, Is.EqualTo(86));
        Assert.That(86f / 74, Is.InRange(1.1f, 1.2f));
        foreach (var trait in initial)
        {
            Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.Success));
            Assert.That(store.GetLevel(trait.TraitId), Is.Zero);
        }
        Assert.That(progress.ScrapParts, Is.EqualTo(12));
        Assert.That(progress.CoreShards, Is.Zero);
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.Empty, "Manufacturing never auto-fits.");
        progress.LoadFromSave(save.LoadOrCreate());
        Assert.That(progress.ManufacturedEquipmentIds.Count, Is.EqualTo(6));
        Assert.That(progress.TryGrantEquipmentStarterMaterials(), Is.False);
    }

    [TestCase(ShipTraitBranchKind.Shared)] [TestCase(ShipTraitBranchKind.MachineGun)]
    [TestCase(ShipTraitBranchKind.Shotgun)] [TestCase(ShipTraitBranchKind.Sniper)]
    public void FinalRecipesAreCompleteAccessibleAndMonotonicByManufacturingTier(ShipTraitBranchKind branch)
    {
        var roster = catalog.TraitDefinitions.Where(t => t.IsDevelopmentRoster && t.DevelopmentBranch == branch).ToArray();
        Assert.That(roster.Length, Is.EqualTo(12));
        Assert.That(roster.Select(t => t.TraitId).Distinct().Count(), Is.EqualTo(12));
        int lastMaximum = 0;
        for (int tier = 0; tier < 4; tier++)
        {
            var row = roster.Where(t => t.DevelopmentResearchTier == tier).ToArray();
            Assert.That(row.Length, Is.EqualTo(3));
            Assert.That(row.Min(t => t.ManufacturingScrapCost), Is.GreaterThan(lastMaximum));
            lastMaximum = row.Max(t => t.ManufacturingScrapCost);
            foreach (var trait in row)
            {
                Assert.That(trait.HasValidManufacturingRecipe, Is.True);
                Assert.That(trait.ManufacturingCoreCost, Is.InRange(0, 2));
                if (tier == 0)
                {
                    Assert.That(trait.ManufacturingScrapCost, Is.InRange(10, 14));
                    Assert.That(trait.ManufacturingCoreCost, Is.Zero);
                }
            }
        }
    }

    [Test]
    public void ManufacturingCostRowsUseIconsCurrentFundsAndHideZeroOrOwnedPrices()
    {
        OpenEquipment();
        var scrap = Get<EquipmentManufacturingCostRow>(panel, "equipmentScrapCost");
        var core = Get<EquipmentManufacturingCostRow>(panel, "equipmentCoreCost");
        var heading = Get<TMP_Text>(panel, "equipmentCostHeading");
        var button = Get<Button>(panel, "equipmentActivationButton");
        panel.InspectEquipment(catalog.FindById("mg_stable_feed"));
        Assert.That(scrap.icon.sprite, Is.Not.Null); Assert.That(core.icon.sprite, Is.Not.Null);
        Assert.That(heading.gameObject.activeSelf, Is.True);
        Assert.That(scrap.root.activeSelf, Is.True); Assert.That(core.root.activeSelf, Is.False);
        Assert.That(scrap.amount.text, Does.Contain("14").And.Contain("보유 0"));
        Assert.That(button.interactable, Is.False);
        ResearchAndResources(); panel.RefreshPanel();
        panel.InspectEquipment(catalog.FindById("mg_terminal_guidance"));
        Assert.That(core.root.activeSelf, Is.True);
        Assert.That(core.amount.text, Does.Contain("2").And.Contain("9999"));
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(core.root.activeSelf, Is.False); Assert.That(scrap.root.activeSelf, Is.False);
        Assert.That(heading.gameObject.activeSelf, Is.False);
        panel.SelectOperatingFrame(OperatingFrameType.Heavy);
        Assert.That(heading.gameObject.activeSelf, Is.False, "Frames have no price.");
    }

    [TestCase(0)] [TestCase(1)] [TestCase(3)]
    public void CapturedCampaignEconomySupportsPricesAndUsesProductionReturnPolicy(int stage)
    {
        var samples = JsonUtility.FromJson<EquipmentEconomyDiagnostics.Samples>(File.ReadAllText(EquipmentEconomyDiagnostics.SamplesPath));
        var maps = samples.maps.Where(m => m.sample / 3 == stage).ToArray();
        Assert.That(maps.Length, Is.EqualTo(3));
        var standard = maps.Select(m => EquipmentEconomyDiagnostics.EstimateMap(m, OperatingFrameType.Standard, .65f, "Normal", 64)).ToArray();
        double scrap = standard.Average(r => r.safeScrap), core = standard.Average(r => r.safeCore);
        Assert.That(scrap, Is.GreaterThan(20)); Assert.That(core, Is.GreaterThan(.5));
        foreach (var result in standard)
        {
            Assert.That(result.capacity, Is.EqualTo(118));
            Assert.That(result.emergencyScrap, Is.InRange(0, result.safeScrap));
            Assert.That(result.emergencyCore, Is.InRange(0, result.safeCore));
            Assert.That(result.deathScrap, Is.InRange(0, result.safeScrap));
            Assert.That(result.deathCore, Is.InRange(0, result.safeCore));
        }
        TestContext.WriteLine($"Economy stage {stage}: normal safe Scrap={scrap:F2}, Core={core:F2}; sampling, not balance proof.");
        foreach (var trait in catalog.TraitDefinitions.Where(t => t.IsDevelopmentRoster && t.DevelopmentResearchTier == (stage == 3 ? 3 : stage)))
            Assert.That(Math.Max(trait.ManufacturingScrapCost / scrap, trait.ManufacturingCoreCost / core), Is.LessThan(2.5));
    }

    [Test]
    public void FirstRegionThreeBossArenaIsNotMisrepresentedAsAFarmingMap()
    {
        var samples = JsonUtility.FromJson<EquipmentEconomyDiagnostics.Samples>(File.ReadAllText(EquipmentEconomyDiagnostics.SamplesPath));
        foreach (var map in samples.maps.Where(m => m.sample / 3 == 2))
        {
            Assert.That(map.sources.Count, Is.EqualTo(5));
            Assert.That(map.bossCore, Is.Zero, "No live CoreObject reward path on the first Phase Gatekeeper map.");
        }
        Assert.That(samples.maps.Where(m => m.repeat).All(m => m.sources.Count > 50 && m.bossCore > 0), Is.True);
    }

    [TestCase(OperatingFrameType.Lightweight, 88)] [TestCase(OperatingFrameType.Standard, 118)] [TestCase(OperatingFrameType.Heavy, 148)]
    public void EconomyUsesActualFrameCargoAndReportsHeavyRatherThanPenalizingIt(OperatingFrameType frame, int cargo)
    {
        var samples = JsonUtility.FromJson<EquipmentEconomyDiagnostics.Samples>(File.ReadAllText(EquipmentEconomyDiagnostics.SamplesPath));
        var map = samples.maps.Single(m => m.sample == 9);
        var result = EquipmentEconomyDiagnostics.EstimateMap(map, frame, .9f, "Thorough", 64);
        Assert.That(result.capacity, Is.EqualTo(cargo));
        Assert.That(result.safeCore, Is.GreaterThan(0));
        TestContext.WriteLine($"{frame}: cargo {cargo}, thorough Scrap {result.safeScrap:F2}, Core {result.safeCore:F2}");
    }

    [Test]
    public void NonBossCoreSourceIsRealButScarcityRemainsExplicit()
    {
        var samples = JsonUtility.FromJson<EquipmentEconomyDiagnostics.Samples>(File.ReadAllText(EquipmentEconomyDiagnostics.SamplesPath));
        foreach (int sample in new[] { 0, 3, 9 })
        {
            var result = EquipmentEconomyDiagnostics.EstimateMap(samples.maps.Single(m => m.sample == sample), OperatingFrameType.Standard, .65f, "No boss", 128, false);
            Assert.That(result.safeCore, Is.GreaterThan(0), "Repeatable reactor currency must not be confused with story parts.");
            Assert.That(result.deathCore, Is.Zero, "Do not extend the Salvage Devourer guarantee to ordinary Core.");
        }
    }

    [Test]
    public void MissingCoreCannotPartiallyConsumeScrapOrAStoryPart()
    {
        SaveData data = Checkpoint(new SaveData(), 3);
        var trait = catalog.FindById("mg_salvage_sweep");
        data.scrapParts = trait.ManufacturingScrapCost;
        data.coreShards = 0;
        progress.LoadFromSave(data);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.InsufficientResources));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(progress.HasBossStoryPart(BossStoryPart.SectorStabilizer), Is.True);
    }

    [TestCase(RunEndReason.SafeReturn, 30, 3)]
    [TestCase(RunEndReason.EmergencyReturn, 23, 2)]
    [TestCase(RunEndReason.Death, 15, 2)]
    public void RealRunCompletionCommitsRiskAdjustedMaterialsAndDurablySavesThem(RunEndReason reason, int scrap, int core)
    {
        RunManager manager = StartRunFixture();
        var run = manager.CurrentRun;
        run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        run.SetCargoRule(100, .7f);
        manager.AddCurrencyRespectingCargo(CurrencyType.ScrapParts, 30);
        manager.AddCurrencyRespectingCargo(CurrencyType.CoreShards, 1);
        // Existing protected campaign reward, not a generic free Core allowance.
        Assert.That(manager.GrantGuaranteedCampaignBossCoreShards(CampaignBossId.SalvageDevourer, 2), Is.EqualTo(2));
        RunResultData result = manager.CompleteRun(reason);
        Assert.That(result.committedScrapParts, Is.EqualTo(scrap));
        Assert.That(result.committedCoreShards, Is.EqualTo(core));
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap));
        Assert.That(progress.CoreShards, Is.EqualTo(core));
        SaveData saved = save.LoadOrCreate();
        Assert.That(saved.scrapParts, Is.EqualTo(scrap));
        Assert.That(saved.coreShards, Is.EqualTo(core));
        Assert.That(manager.CompleteRun(reason), Is.Null, "Completion cannot commit twice.");
    }
}
