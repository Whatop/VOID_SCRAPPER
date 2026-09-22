using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    // Audit metadata only. TraitDefinition remains the sole gameplay data authority.
    private static readonly Dictionary<string, string> CatalogRoles = new Dictionary<string, string>
    {
        { "shared_cargo_bay", "Salvage" }, { "shared_salvage_protocol", "Salvage" },
        { "shared_salvage_magnet", "Salvage" }, { "shared_reinforced_plating", "Survival" },
        { "shared_engine_tuning", "Mobility" }, { "shared_dash_capacitor", "Mobility" },
        { "shared_vector_thruster", "Mobility" }, { "shared_repair_foam", "Survival" },
        { "shared_targeting_bus", "Offense" }, { "shared_accelerator_coil", "Control" },
        { "shared_range_focusing", "Control" }, { "shared_rapid_feed", "Offense" },
        { "shared_combat_gyro", "Control" }, { "shared_scrap_sorter", "Salvage" },
        { "shared_return_container", "Salvage" }, { "shared_radar_amplifier", "Tactical" },
        { "shared_active_cooler", "Tactical" }, { "shared_bulkhead_cargo_frame", "Salvage" },
        { "shared_collection_route", "Salvage" }, { "shared_cutting_ammo", "Salvage" },
        { "shared_longshot_stabilizer", "Control" }, { "shared_survival_protocol", "Survival" },
        { "shared_lightweight_cargo", "Salvage" }, { "shared_return_protocol", "Salvage" },
        { "shared_standard_upgrade", "Offense" }, { "shared_periodic_reflector", "Signature" },
        { "mg_sustained_harvest_fire", "Salvage" }, { "mg_guidance_control", "Control" },
        { "mg_stable_feed", "Offense" }, { "mg_salvage_sweep", "Salvage" },
        { "mg_midrange_pressure", "Offense" }, { "mg_terminal_guidance", "Signature" },
        { "mg_dash_missile_salvo", "Signature" }, { "sg_choke_barrel", "Control" },
        { "sg_extra_pellet", "Offense" }, { "sg_breaching_drive", "Survival" },
        { "sg_close_harvest_burst", "Salvage" }, { "sg_taunt_resonator", "Tactical" },
        { "sg_close_quarters_overpressure", "Signature" }, { "sn_charge_accelerator", "Offense" },
        { "sn_piercing_amplifier", "Control" }, { "sn_focus_lens", "Tactical" },
        { "sn_high_output_core", "Offense" }, { "sn_stealth_scan", "Tactical" },
        { "sn_semi_auto_laser", "Signature" }, { "sn_dash_echo_shot", "Signature" },
        { "mg_traverse_servo", "Mobility" },
        { "mg_heat_exchanger", "Control" },
        { "mg_line_penetrator", "Control" },
        { "mg_target_distributor", "Control" },
        { "mg_twin_feed", "Signature" },
        { "sg_cycle_actuator", "Offense" },
        { "sg_pellet_penetrator", "Control" },
        { "sg_impact_ejector", "Control" },
        { "sg_breach_compensator", "Survival" },
        { "sg_slug_coupler", "Signature" },
        { "sg_breach_sequencer", "Signature" },
        { "sn_ballistic_alignment", "Control" },
        { "sn_mobile_charge_coupler", "Mobility" },
        { "sn_charge_aperture", "Control" },
        { "sn_anchor_optics", "Tactical" },
        { "sn_reserve_capacitor", "Signature" }
    };

    [Test]
    public void CatalogReferencesRolesAndFamiliesMatchCurrentAuthoredContent()
    {
        Assert.That(catalog.TraitDefinitions, Has.Count.EqualTo(66));
        Assert.That(catalog.TraitDefinitions.All(t => t != null), Is.True);
        Assert.That(catalog.TraitDefinitions.Select(t => t.TraitId).Distinct().Count(), Is.EqualTo(66));
        var normal = catalog.TraitDefinitions.Where(t => t.CanAppearAsRandomDropTrait).ToArray();
        Assert.That(normal.Select(t => t.TraitId), Is.EquivalentTo(CatalogRoles.Keys));
        Assert.That(normal.Count(t => t.Category == TraitCategory.Shared), Is.EqualTo(26));
        Assert.That(normal.Count(t => t.Category == TraitCategory.WeaponSpecific && t.WeaponTreeType == WeaponTreeType.MachineGun), Is.EqualTo(12));
        Assert.That(normal.Count(t => t.Category == TraitCategory.WeaponSpecific && t.WeaponTreeType == WeaponTreeType.Shotgun), Is.EqualTo(12));
        Assert.That(normal.Count(t => t.Category == TraitCategory.WeaponSpecific && t.WeaponTreeType == WeaponTreeType.Sniper), Is.EqualTo(12));
        foreach (TraitDefinition trait in normal)
        {
            var data = new UnityEditor.SerializedObject(trait);
            Assert.That(data.FindProperty("description").stringValue, Is.Not.Empty, trait.TraitId);
            Assert.That(data.FindProperty("maxLevel").intValue, Is.InRange(1, 3), trait.TraitId);
            for (int level = 1; level <= trait.MaxLevel; level++)
            {
                var effects = trait.LevelEffects.Where(e => e != null && e.Level == level).ToArray();
                Assert.That(effects, Is.Not.Empty, trait.TraitId + " level " + level);
                foreach (var effect in effects)
                {
                    Assert.That(float.IsNaN(effect.Value) || float.IsInfinity(effect.Value) || effect.Value == 0, Is.False);
                    Assert.That(Enum.IsDefined(typeof(TraitEffectType), effect.EffectType), Is.True);
                    Assert.That(TraitEffectTextUtility.FormatEffect(effect.EffectType, effect.Value), Does.Not.Contain(effect.EffectType.ToString()));
                }
            }
        }
    }

    [TestCase("mg_stable_feed")]
    [TestCase("mg_guidance_control")]
    [TestCase("sn_charge_accelerator")]
    [TestCase("sn_high_output_core")]
    [TestCase("sg_close_quarters_overpressure")]
    [TestCase("sn_piercing_amplifier")]
    public void CatalogAccumulationMatchesActualNewLevelAcquisition(string id)
    {
        TraitDefinition trait = catalog.FindById(id);
        progress.LoadFromSave(Checkpoint(new SaveData(), 11));
        progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == trait.WeaponTreeType).ShipId);
        Assert.That(PrepareFixture(trait), Is.True);
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "CatalogStatProbe", false);
        var modifiers = player.AddComponent<PlayerWeaponModifiers>();
        float fire = 1, spread = 1, chargeTime = 1, chargeDamage = 1, angle = 0, range = 0, overpressure = 0;
        int pierce = 0;
        for (int level = 1; level <= trait.MaxLevel; level++)
        {
            Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out int prior, out int actual), Is.True);
            Assert.That(prior, Is.EqualTo(level - 1)); Assert.That(actual, Is.EqualTo(level));
            foreach (var effect in trait.LevelEffects.Where(e => e.Level == level))
            {
                switch (effect.EffectType)
                {
                    case TraitEffectType.FireRatePercent: fire /= 1 + effect.Value / 100; break;
                    case TraitEffectType.SpreadReductionPercent: spread *= 1 - effect.Value / 100; break;
                    case TraitEffectType.ChargeTimeReductionPercent: chargeTime /= 1 + effect.Value / 100; break;
                    case TraitEffectType.ChargeDamagePercent: chargeDamage *= 1 + effect.Value / 100; break;
                    case TraitEffectType.HomingAngleBonus: angle += effect.Value; break;
                    case TraitEffectType.HomingRangeBonus: range += effect.Value; break;
                    case TraitEffectType.ShotgunCloseRangeDamagePercent: overpressure += effect.Value; break;
                    case TraitEffectType.PierceCountBonus: pierce += Mathf.RoundToInt(effect.Value); break;
                }
            }
            Assert.That(modifiers.FireIntervalMultiplier, Is.EqualTo(fire).Within(.0001f));
            Assert.That(modifiers.SpreadMultiplier, Is.EqualTo(spread).Within(.0001f));
            Assert.That(modifiers.ChargeTimeMultiplier, Is.EqualTo(chargeTime).Within(.0001f));
            Assert.That(modifiers.ChargeDamageMultiplier, Is.EqualTo(chargeDamage).Within(.0001f));
            Assert.That(modifiers.HomingAngleBonus, Is.EqualTo(angle)); Assert.That(modifiers.HomingRangeBonus, Is.EqualTo(range));
            Assert.That(modifiers.ShotgunCloseRangeDamagePercent, Is.EqualTo(overpressure)); Assert.That(modifiers.PierceBonus, Is.EqualTo(pierce));
        }
        Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out _, out _), Is.False);
        Assert.That(store.GetLevel(id), Is.EqualTo(trait.MaxLevel));
    }

    [Test]
    public void ChargeSpeedTextUsesActualTimeReductionAndUnknownEffectsNeverLeakEnumNames()
    {
        string text = TraitEffectTextUtility.FormatEffect(TraitEffectType.ChargeTimeReductionPercent, 25);
        Assert.That(text, Does.Contain("20%"), "A 25% charge speed bonus divides time by 1.25.");
        foreach (TraitEffectType effect in Enum.GetValues(typeof(TraitEffectType)))
            Assert.That(TraitEffectTextUtility.FormatEffect(effect, 1), Does.Not.Contain(effect.ToString()));
        Assert.That(TraitEffectTextUtility.FormatEffect((TraitEffectType)999, 1), Does.Not.Contain("999"));
    }

    [Test]
    public void AllNormalEquipmentRowsMatchCatalogWithoutReintroducingUpgradeUi()
    {
        OpenEquipment();
        progress.LoadFromSave(Checkpoint(new SaveData(), 11));
        foreach (TraitDefinition trait in catalog.TraitDefinitions.Where(t => t.CanAppearAsRandomDropTrait))
        {
            if (trait.Category == TraitCategory.WeaponSpecific)
                progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == trait.WeaponTreeType).ShipId);
            panel.InspectEquipment(trait);
            Assert.That(Get<TMP_Text>(panel, "equipmentDetails").text, Is.EqualTo(trait.Description));
            var rows = Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows");
            Assert.That(rows.Count(r => r.root.activeSelf), Is.EqualTo(trait.MaxLevel));
            for (int i = 0; i < trait.MaxLevel; i++)
                Assert.That(rows[i].effects.text, Is.EqualTo(TraitEffectTextUtility.BuildEffectText(trait, i + 1)));
            Assert.That(Get<Button>(ui, "traitActionButton").gameObject.activeSelf, Is.False);
            Assert.That(panel.TryUnlockSelectedTrait(), Is.False);
        }
    }

    [Test]
    public void ReflectorUpgradesReconfigureOneSourceRatherThanAddingRechargeTimes()
    {
        ResearchAndResources();
        TraitDefinition trait = catalog.FindById("shared_periodic_reflector");
        Assert.That(PrepareFixture(trait), Is.True);
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "ReflectorLevels", false);
        var reflector = player.AddComponent<PlayerPeriodicReflector2D>();
        for (int level = 1; level <= trait.MaxLevel; level++)
        {
            Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out _, out _), Is.True);
            Assert.That(reflector.RechargeDuration, Is.EqualTo(trait.LevelEffects.Single(e => e.Level == level).Value));
        }
    }

    [TestCase(3)] [TestCase(6)] [TestCase(9)] [TestCase(12)]
    public void PreparedPoolsRemainValidAcrossShopContainerBossAndTuning(int size)
    {
        var randomState = UnityEngine.Random.state;
        try
        {
            progress.LoadFromSave(Checkpoint(new SaveData(), 11));
            for (int i = 0; i < size; i++) Assert.That(PrepareFixture(catalog.FindById(DiagnosticPool[i])), Is.True);
            Assert.That(RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3), Is.Empty);
            var shopObject = AuthoredRuntimeFixture.Create(scene, services.transform, "CatalogShop", false);
            var shop = shopObject.AddComponent<ShopStockController>();
            Set(shop, "traitCatalog", catalog); Set(shop, "includeCatalogTraits", true); Set(shop, "includeManualTraitPool", false);
            var available = (List<TraitDefinition>)typeof(ShopStockController).GetMethod("BuildAvailableTraitCandidates", Private)
                .Invoke(shop, new object[] { WeaponTreeType.MachineGun });
            Assert.That(available.Select(t => t.TraitId), Is.EquivalentTo(catalog.TraitDefinitions.Where(t => RunTraitAcquisitionService.IsOrdinaryCandidate(t)).Select(t => t.TraitId)));
            var container = RunRewardChoiceGenerator.BuildOptionsFromDefinitions(SpecialRewardMode.TraitOnly, 1,
                RunRewardRarity.Common, catalog.TraitDefinitions, null, WeaponTreeType.MachineGun, true);
            Assert.That(container.Count, Is.EqualTo(1));
            Assert.That(progress.IsEquipmentPrepared(container[0].Id), Is.True);
            var reinforcement = UnityEditor.AssetDatabase.LoadAssetAtPath<ReinforcementCatalog>("Assets/02_Scripts/Config/Catalog/Reinforcement Catalog.asset");
            Assert.That(reinforcement, Is.Not.Null);
            var reinforcements = new List<ReinforcementDefinition>(); reinforcement.AppendAllTo(reinforcements);
            var boss = RunRewardChoiceGenerator.BuildOptionsFromDefinitions(SpecialRewardMode.Mixed, 3,
                RunRewardRarity.Rare, catalog.TraitDefinitions, reinforcements, WeaponTreeType.MachineGun, true);
            Assert.That(boss, Is.Not.Empty);
            Assert.That(boss.All(o => o.Rarity >= RunRewardRarity.Rare && (o.Trait == null || progress.IsEquipmentPrepared(o.Id))), Is.True);
            store.AddOrUpgrade(catalog.FindById(DiagnosticPool[0]));
            var tuning = RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3);
            Assert.That(tuning.Select(o => o.Id), Is.EquivalentTo(new[] { DiagnosticPool[0] }));
        }
        finally { UnityEngine.Random.state = randomState; }
    }

    private static readonly string[] DiagnosticPool =
    {
        "shared_cargo_bay", "mg_stable_feed", "mg_guidance_control", "shared_reinforced_plating",
        "shared_engine_tuning", "shared_salvage_protocol", "mg_sustained_harvest_fire", "shared_return_container",
        "shared_periodic_reflector", "mg_terminal_guidance", "shared_radar_amplifier", "mg_dash_missile_salvo"
    };

    [TestCase(3, false)] [TestCase(6, false)] [TestCase(9, false)] [TestCase(12, false)]
    [TestCase(3, true)] [TestCase(6, true)] [TestCase(9, true)] [TestCase(12, true)]
    public void RewardPoolDiagnostic(int size, bool preferUpgrade)
    {
        const int trials = 64;
        var previousRandom = UnityEngine.Random.state;
        float firstMaxSum = 0, owned12Sum = 0;
        int opportunities = 0, upgradeOffers = 0, expectedExhaustions = 0;
        try
        {
            for (int trial = 0; trial < trials; trial++)
            {
                store.Clear();
                progress.LoadFromSave(Checkpoint(new SaveData(), 11));
                for (int i = 0; i < size; i++) Assert.That(PrepareFixture(catalog.FindById(DiagnosticPool[i])), Is.True);
                store.InitializeDeployment(new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal), catalog);
                UnityEngine.Random.InitState(4100 + trial);
                int firstMax = 0, owned12 = 0;
                for (int opportunity = 1; opportunity <= 24; opportunity++)
                {
                    var options = RunRewardChoiceGenerator.BuildOptionsFromDefinitions(SpecialRewardMode.TraitOnly, 3,
                        RunRewardRarity.Common, catalog.TraitDefinitions, null, WeaponTreeType.MachineGun, false);
                    if (options.Count == 0)
                    {
                        Assert.That(catalog.TraitDefinitions.Any(t => RunTraitAcquisitionService.IsOrdinaryCandidate(t)), Is.False);
                        expectedExhaustions++;
                        if (opportunity <= 12) owned12 = store.TraitLevels.Count;
                        break;
                    }
                    opportunities++;
                    Assert.That(options.All(o => DiagnosticPool.Take(size).Contains(o.Id) && store.CanUpgrade(o.Trait)), Is.True);
                    if (options.Any(o => store.GetLevel(o.Id) > 0)) upgradeOffers++;
                    var tuning = RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3);
                    Assert.That(tuning.All(o => store.GetLevel(o.Id) > 0 && store.CanUpgrade(o.Trait) && progress.IsEquipmentPrepared(o.Id)), Is.True);
                    RunRewardOption choice = preferUpgrade ? options.FirstOrDefault(o => store.GetLevel(o.Id) > 0) : null;
                    choice ??= options[UnityEngine.Random.Range(0, options.Count)];
                    int level = store.AddOrUpgrade(choice.Trait); // Real level store; no runtime presentation needed for sampling.
                    if (firstMax == 0 && level == choice.Trait.MaxLevel) firstMax = opportunity;
                    if (opportunity == 12) owned12 = store.TraitLevels.Count;
                }
                Assert.That(firstMax, Is.InRange(1, 24));
                firstMaxSum += firstMax; owned12Sum += owned12;
            }
        }
        finally { UnityEngine.Random.state = previousRandom; }
        TestContext.WriteLine($"CATALOG_DIAGNOSTIC size={size} policy={(preferUpgrade ? "upgrade-first" : "random-choice")} trials={trials} firstMax={firstMaxSum / trials:F3} ownedAt12={owned12Sum / trials:F3} upgradeOfferRate={(float)upgradeOffers / opportunities:F3} invalidEmpty=0 exhaustedTrials={expectedExhaustions}");
    }
}
