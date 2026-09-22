using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    public static readonly string[] FinalNewIds =
    {
        "mg_traverse_servo", "mg_heat_exchanger", "mg_line_penetrator", "mg_target_distributor", "mg_twin_feed",
        "sg_cycle_actuator", "sg_pellet_penetrator", "sg_impact_ejector", "sg_breach_compensator", "sg_slug_coupler", "sg_breach_sequencer",
        "sn_ballistic_alignment", "sn_mobile_charge_coupler", "sn_charge_aperture", "sn_anchor_optics", "sn_reserve_capacitor"
    };

    [TestCaseSource(nameof(FinalNewIds))]
    public void FinalBlueprintManufactureSaveFitDeploymentLevelsAndPortal(string id)
    {
        TraitDefinition trait = catalog.FindById(id);
        Assert.That(trait, Is.Not.Null); Assert.That(trait.HasRuntimePrerequisites, Is.False);
        Assert.That(trait.IsDevelopmentRoster && trait.HasValidManufacturingRecipe, Is.True);
        if (trait.DevelopmentResearchTier > 0 || trait.WeaponTreeType != WeaponTreeType.MachineGun)
            Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.ResearchLocked));
        ResearchAndResources();
        var empty = progress.CreateSaveData(); empty.scrapParts = empty.coreShards = 0; progress.LoadFromSave(empty);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.InsufficientResources));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        ResearchAndResources(); int scrap = progress.ScrapParts, core = progress.CoreShards;
        ManufactureAndFit(trait);
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap - trait.ManufacturingScrapCost));
        Assert.That(progress.CoreShards, Is.EqualTo(core - trait.ManufacturingCoreCost));
        before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.AlreadyManufactured));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        progress.LoadFromSave(save.LoadOrCreate());
        Assert.That(progress.IsEquipmentManufactured(id) && progress.IsEquipmentFitted(id), Is.True);
        ShipDefinition ship = ships.First(s => s.DefaultWeaponTree == trait.WeaponTreeType);
        progress.SetSelectedShipId(ship.ShipId);
        var manager = services.AddComponent<RunManager>();
        typeof(RunManager).GetProperty("Instance").SetValue(null, manager); Call(store, "EnsureRunManagerSubscription");
        manager.StartNewRun(trait.WeaponTreeType, ExpeditionDepth.Normal, ship.ShipId, SeaRegionType.DenseDebris);
        Assert.That(manager.CurrentRun.PreparedEquipmentIds, Is.EqualTo(new[] { id }));
        GameObject player = ReconstructPlayer(manager.CurrentRun);
        Assert.That(store.GetLevel(id), Is.EqualTo(1));
        AssertActualFinalEffect(id, 1, player);
        for (int level = 2; level <= 3; level++)
        {
            Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out int previous, out int actual), Is.True);
            Assert.That(previous, Is.EqualTo(level - 1)); Assert.That(actual, Is.EqualTo(level));
            AssertActualFinalEffect(id, level, player);
        }
        Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out _, out _), Is.False);
        player.GetComponent<PlayerRuntimeStatApplier>().RestoreCurrentVitals(3, 0);
        player.GetComponent<ExpeditionBootstrap>().Initialize(); AssertActualFinalEffect(id, 3, player);
        manager.CurrentRun.CapturePlayerVitals(3, 0);
        manager.CurrentRun.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        GameObject next = ReconstructPlayer(manager.CurrentRun); AssertActualFinalEffect(id, 3, next);
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(next.GetComponent<PlayerArmor>().CurrentArmor, Is.Zero);
        manager.AbandonActiveRunWithoutRewards(); manager.ReleaseRunEndingPresentationOwnership();
        Assert.That(store.TraitLevels, Is.Empty);
        manager.StartNewRun(trait.WeaponTreeType, ExpeditionDepth.Normal, ship.ShipId, SeaRegionType.DenseDebris);
        Assert.That(store.GetLevel(id), Is.EqualTo(1)); AssertActualFinalEffect(id, 1, ReconstructPlayer(manager.CurrentRun));
    }

    private static void AssertActualFinalEffect(string id, int level, GameObject player)
    {
        var m = player.GetComponent<PlayerWeaponModifiers>(); int i = level - 1;
        float actual, expected;
        switch (id)
        {
            case "mg_traverse_servo": actual = m.ProjectileSpeedMultiplier; expected = new[] { 1f, 1.08f, 1.188f }[i]; break;
            case "mg_heat_exchanger": actual = m.MachineGunCoolingMultiplier; expected = new[] { 1.2f, 1.2f, 1.5f }[i];
                Assert.That(m.MachineGunCoolingDelayReduction, Is.EqualTo(new[] { 0f, .05f, .1f }[i]).Within(.0001)); break;
            case "mg_line_penetrator": actual = m.ProjectileSpeedMultiplier; expected = new[] { 1f, 1.1f, 1.232f }[i]; Assert.That(m.PierceBonus, Is.EqualTo(1)); break;
            case "mg_target_distributor": actual = m.MachineGunTargetDistributionLevel; expected = level; break;
            case "mg_twin_feed": actual = m.MachineGunTwinFeedLevel; expected = level; break;
            case "sg_cycle_actuator": actual = m.FireIntervalMultiplier; expected = new[] { 1f/1.05f, 1f/1.05f/1.07f, 1f/1.05f/1.07f/1.10f }[i]; break;
            case "sg_pellet_penetrator": actual = m.ProjectileSpeedMultiplier; expected = new[] { 1f, 1.08f, 1.2096f }[i]; Assert.That(m.PierceBonus, Is.EqualTo(1)); break;
            case "sg_impact_ejector": actual = m.ShotgunImpactDisplacement; expected = new[] { .35f, .55f, .8f }[i]; break;
            case "sg_breach_compensator": actual = m.PostDashDamageMultiplier; expected = new[] { .9f, .85f, .8f }[i]; break;
            case "sg_slug_coupler": actual = m.ShotgunSlugCouplerLevel; expected = level; break;
            case "sg_breach_sequencer": actual = m.ShotgunBreachSequenceLevel; expected = level; break;
            case "sn_ballistic_alignment": actual = m.ProjectileSpeedMultiplier; expected = new[] { 1.08f, 1.08f, 1.188f }[i];
                Assert.That(m.RangeMultiplier, Is.EqualTo(new[] { 1f, 1.08f, 1.188f }[i]).Within(.0001)); break;
            case "sn_mobile_charge_coupler": actual = m.SniperMovingChargeBonus; expected = new[] { .1f, .2f, .4f }[i]; break;
            case "sn_charge_aperture": actual = m.SniperChargedWidthBonus; expected = new[] { .15f, .25f, .4f }[i]; break;
            case "sn_anchor_optics": actual = m.SniperChargeSightMultiplier; expected = new[] { 1.1f, 1.2f, 1.35f }[i]; break;
            case "sn_reserve_capacitor": actual = m.SniperReserveChargeFraction; expected = new[] { .1f, .15f, .25f }[i]; break;
            default: throw new ArgumentException(id);
        }
        Assert.That(actual, Is.EqualTo(expected).Within(.0001), id + " Lv" + level);
    }

    [Test]
    public void FinalProductionRosterHasFortyEightPositionsAndUnrestrictedMatchingFitting()
    {
        ResearchAndResources();
        foreach (string[] branch in EquipmentFinalRosterAuthoring.Roster)
            for (int i = 0; i < 12; i++)
            {
                var trait = catalog.FindById(branch[i]);
                Assert.That(trait.DevelopmentResearchTier, Is.EqualTo(i / 3));
                Assert.That(trait.DevelopmentDisplayOrder, Is.EqualTo(i % 3));
                ManufactureAndFit(trait);
            }
        Assert.That(progress.ManufacturedEquipmentIds.Count, Is.EqualTo(48));
        foreach (ShipDefinition ship in ships)
        {
            progress.SetSelectedShipId(ship.ShipId);
            var effective = new List<string>(); progress.AppendEffectiveEquipment(effective, ship.DefaultWeaponTree);
            Assert.That(effective.Count, Is.EqualTo(24));
            Assert.That(progress.EquipmentLoadoutTraitIds.Count, Is.EqualTo(48));
        }
    }

    [Test]
    public void VersionSixManufacturedRowsAndLegacyOwnersSurviveRemapWithoutCurrencyMutation()
    {
        var data = Checkpoint(new SaveData(), 3); data.version = 6; data.scrapParts = 73;
        data.manufacturedEquipmentIds.AddRange(new[] { "shared_rapid_feed", "mg_sustained_harvest_fire", "sn_semi_auto_laser" });
        data.equipmentLoadoutTraitIds.AddRange(data.manufacturedEquipmentIds);
        progress.LoadFromSave(data);
        Assert.That(progress.IsEquipmentUsable(catalog.FindById("shared_rapid_feed"), WeaponTreeType.MachineGun), Is.True);
        Assert.That(progress.GetManufacturingAvailability(catalog.FindById("shared_rapid_feed")), Is.EqualTo(EquipmentDevelopmentResult.InvalidDefinition));
        var low = Checkpoint(progress.CreateSaveData(), 1); progress.LoadFromSave(low);
        Assert.That(progress.IsEquipmentResearched(catalog.FindById("mg_sustained_harvest_fire")), Is.True, "Original row-one owned module retains its legitimate threshold.");
        Assert.That(progress.IsEquipmentResearched(catalog.FindById("sn_semi_auto_laser")), Is.False, "Ship research gate still applies.");
        var roundtrip = progress.CreateSaveData(); string json = JsonUtility.ToJson(roundtrip);
        progress.LoadFromSave(roundtrip); Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(json));
        Assert.That(progress.ScrapParts, Is.EqualTo(73));
        Assert.That(progress.ManufacturedEquipmentIds.Count, Is.EqualTo(3));
        Assert.That(progress.IsEquipmentManufactured("mg_twin_feed"), Is.False);
        ResearchAndResources(); Assert.That(progress.IsEquipmentResearched(catalog.FindById("sn_semi_auto_laser")), Is.True);
    }

    [TestCase(6)] [TestCase(12)] [TestCase(18)] [TestCase(24)]
    public void LvOneFittedRewardDiagnostics(int count)
    {
        var rng = UnityEngine.Random.state;
        try
        {
            ResearchAndResources(); int perBranch = count / 2;
            var chosen = EquipmentFinalRosterAuthoring.Roster[0].Take(perBranch)
                .Concat(EquipmentFinalRosterAuthoring.Roster[1].Take(perBranch)).Select(catalog.FindById).ToArray();
            foreach (var trait in chosen) ManufactureAndFit(trait);
            const int trials = 64; float first = 0, third = 0, special = 0; int noOffers = 0, offers = 0, upgradeOffers = 0, specialSamples = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                UnityEngine.Random.InitState(9137 + trial); store.Clear();
                store.InitializeDeployment(new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal), catalog);
                int firstAt = 0, thirdAt = 0; var maxed = new HashSet<string>();
                for (int opportunity = 1; opportunity <= count * 3 + 2; opportunity++)
                {
                    var options = RunRewardChoiceGenerator.BuildOptionsFromDefinitions(SpecialRewardMode.TraitOnly, 3,
                        RunRewardRarity.Common, catalog.TraitDefinitions, null, WeaponTreeType.MachineGun, true);
                    offers++;
                    if (options.Count == 0) { noOffers++; Assert.That(chosen.All(t => store.GetLevel(t.TraitId) == t.MaxLevel), Is.True); break; }
                    if (options.Any(o => store.GetLevel(o.Id) > 0)) upgradeOffers++;
                    var picked = options[UnityEngine.Random.Range(0, options.Count)].Trait;
                    store.AddOrUpgrade(picked);
                    if (store.GetLevel(picked.TraitId) == picked.MaxLevel && maxed.Add(picked.TraitId))
                    {
                        if (firstAt == 0) firstAt = opportunity;
                        if (maxed.Count == 3) thirdAt = opportunity;
                        if (picked.Rarity == TraitRarity.Special) { special += opportunity; specialSamples++; }
                    }
                }
                Assert.That(firstAt, Is.InRange(2, count * 2)); Assert.That(thirdAt, Is.InRange(6, count * 3));
                first += firstAt; third += thirdAt;
            }
            TestContext.WriteLine($"FINAL_REWARD count={count} trials={trials} firstMax={first/trials:F2} thirdMax={third/trials:F2} specialMax={(specialSamples > 0 ? special/specialSamples : 0):F2} upgradeOffers={upgradeOffers}/{offers} exhausted={noOffers}/{offers} prematureEmpty=0");
        }
        finally { UnityEngine.Random.state = rng; }
    }
}
