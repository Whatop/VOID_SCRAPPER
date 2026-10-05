using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    private PlayerRuntimeStatApplier HangarBaseline => Get<PlayerRuntimeStatApplier>(AuthoredRuntimeFixture.Single<SettlementController>(scene), "deploymentStatSource");
    private HangarDeploymentProjection Project(int ship = 0) => HangarDeploymentProjection.Calculate(ships[ship], progress, catalog.TraitDefinitions, HangarBaseline);
    private void HangarLoad(string scenario)
    {
        var data = scenario == "none" || scenario == "tech" ? new SaveData() : Checkpoint(new SaveData(), 10);
        data.equipmentLoadoutTraitIds.Clear(); data.manufacturedEquipmentIds.Clear(); data.sectorTechnologyLevels.Clear();
        string[] ids = scenario switch
        {
            "light" => new[] { StructuralFrameProfile.LightweightId },
            "fused" => new[] { StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId },
            "shared" => new[] { "shared_cargo_bay", "shared_engine_tuning" },
            "branch" => new[] { "mg_stable_feed", "mg_midrange_pressure" },
            "incompatible" => new[] { "sn_charge_accelerator" },
            "conditional" => new[] { catalog.TraitDefinitions.First(t => t != null && t.HasValidDevelopmentMetadata && t.HasRuntimePrerequisites && t.IsAvailableFor(WeaponTreeType.MachineGun)).TraitId },
            "all" => EquipmentFinalRosterAuthoring.Roster.SelectMany(r => r).ToArray(),
            _ => Array.Empty<string>()
        };
        data.equipmentLoadoutTraitIds.AddRange(ids); data.manufacturedEquipmentIds.AddRange(ids);
        if (scenario == "tech" || scenario == "all") foreach (var tech in SectorTechnologyCatalog.Definitions)
            data.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(tech.Id, 3));
        progress.LoadFromSave(data);
    }
    [TestCase("none", 0)] [TestCase("tech", 0)] [TestCase("light", 0)] [TestCase("fused", 0)]
    [TestCase("shared", 0)] [TestCase("branch", 0)] [TestCase("incompatible", 0)] [TestCase("conditional", 0)]
    [TestCase("all", 0)] [TestCase("all", 1)] [TestCase("all", 2)] [TestCase("story", 0)]
    public void HangarFreshProjectionEqualsActualRuntimeEveryDisplayedStat(string scenario, int shipIndex)
    {
        HangarLoad(scenario);
        if (scenario == "conditional")
        {
            var fitted = new System.Collections.Generic.List<string>(); progress.AppendEffectiveEquipment(fitted, WeaponTreeType.MachineGun);
            Assert.That(fitted, Has.Count.EqualTo(1), "The conditional fixture must actually be effective fitting before deployment filtering.");
        }
        var storyTraits = catalog.TraitDefinitions.ToList();
        if (scenario == "story")
        {
            var story = UnityEngine.Object.Instantiate(catalog.FindById("pixel_curse")); transientAssets.Add(story);
            Set(story, "traitId", "projection_story_fixture");
            var effects = new System.Collections.Generic.List<TraitLevelEffect>();
            foreach (var type in new[] { TraitEffectType.MaxHpBonus, TraitEffectType.DamagePercent, TraitEffectType.MoveSpeedPercent, TraitEffectType.CargoCapacityBonus })
            { var effect = new TraitLevelEffect(); Set(effect, "value", 5f); Set(effect, "effectType", type); effects.Add(effect); }
            Set(story, "levelEffects", effects); storyTraits.Add(story); progress.TryAcquirePersistentStoryTrait(story);
        }
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        var priorRun = RunManager.Instance;
        var projected = HangarDeploymentProjection.Calculate(ships[shipIndex], progress, storyTraits, HangarBaseline);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(RunManager.Instance, Is.SameAs(priorRun)); Assert.That(store.TraitLevels, Is.Empty);
        var ship = ships[shipIndex];
        var run = new RunContext(ship.DefaultWeaponTree, ExpeditionDepth.Normal, ship.ShipId, SeaRegionType.DenseDebris);
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "HangarEquivalencePlayer", false);
        var hp = player.AddComponent<PlayerHealth>(); var armor = player.AddComponent<PlayerArmor>();
        var movement = player.AddComponent<PlayerController2D>(); var dash = player.AddComponent<PlayerDash>();
        var weapon = player.AddComponent<PlayerWeaponModifiers>(); var bonus = player.AddComponent<PlayerRuntimeBonusState>();
        var cargo = player.AddComponent<PlayerCargoController>();
        var applier = player.AddComponent<PlayerRuntimeStatApplier>(); Set(applier, "logApplyResult", false);
        foreach (string field in new[] { "baseMoveSpeed", "baseDashDistance", "baseDashCooldown", "baseMaxArmor", "baseStartingArmor" })
            Set(applier, field, Get<float>(HangarBaseline, field));
        applier.Apply(run, progress, ships, Array.Empty<BuildingDefinition>(), storyTraits, true);
        store.InitializeDeployment(run, catalog);
        var equipment = player.AddComponent<RunTraitEffectApplier>(); Set(equipment, "traitCatalog", catalog);
        equipment.ApplyAllStoredTraits();
        void Equal(float preview, float actual, string stat) => Assert.That(preview, Is.EqualTo(actual).Within(.0001f), scenario + "/" + stat);
        Equal(projected.Hp, hp.MaxHp, "HP"); Equal(projected.Armor, armor.CurrentArmor, "Armor"); Equal(projected.Cargo, cargo.MaxCapacity, "Cargo");
        Equal(projected.MoveSpeed, movement.EffectiveMoveSpeed, "Move"); Equal(projected.DashCooldown, dash.EffectiveDashCooldown, "DashCooldown"); Equal(projected.DashDistance, dash.DashDistance, "DashDistance");
        Equal(projected.MovePercent, (movement.EffectiveMoveSpeed / HangarBaseline.BaseMoveSpeed - 1) * 100, "Displayed movement modifier");
        Equal(projected.DashCooldownPercent, (dash.EffectiveDashCooldown / HangarBaseline.BaseDashCooldown - 1) * 100, "Displayed cooldown modifier");
        Equal(projected.DashDistanceBonus, dash.DashDistance - HangarBaseline.BaseDashDistance, "Displayed distance modifier");
        Equal(projected.Damage, weapon.DamageMultiplier, "Damage"); Equal(projected.FireRate, 1 / weapon.FireIntervalMultiplier, "FireRate");
        Equal(projected.ProjectileSpeed, weapon.ProjectileSpeedMultiplier, "ProjectileSpeed"); Equal(projected.Range, weapon.RangeMultiplier, "Range");
        Equal(projected.Spread, weapon.SpreadMultiplier, "Spread"); Equal(projected.ChargeTime, weapon.ChargeTimeMultiplier, "ChargeTime"); Equal(projected.ChargeDamage, weapon.ChargeDamageMultiplier, "ChargeDamage");
        Equal(projected.Harvest, bonus.HarvestYieldMultiplier, "Harvest"); Equal(projected.HarvestDamage, bonus.HarvestObjectDamageMultiplier, "HarvestDamage");
        Equal(projected.Recovery, bonus.HealEfficiencyMultiplier, "Recovery"); Equal(projected.Scrap, bonus.ScrapGainMultiplier, "Scrap"); Equal(projected.Pickup, bonus.PickupRangeBonus, "Pickup");
        Equal(projected.RadarRadius, bonus.RadarScanRadiusBonus, "Radar"); Equal(projected.RadarTaunt, bonus.RadarTauntDurationBonus, "Taunt"); Equal(projected.RadarStealth, bonus.RadarStealthDurationBonus, "Stealth");
        Equal(projected.HomingAngle, weapon.HomingAngleBonus, "HomingAngle"); Equal(projected.HomingRange, weapon.HomingRangeBonus, "HomingRange");
        Equal(projected.Projectiles, weapon.ProjectileCountBonus, "Projectiles"); Equal(projected.Pierce, weapon.PierceBonus, "Pierce");
        Assert.That(store.TraitLevels.All(t => t.level == 1), Is.True);
        if (scenario == "conditional" || scenario == "incompatible") Assert.That(store.TraitLevels, Is.Empty);
        TestContext.WriteLine($"{scenario}/{ship.ShipId}: 25 projected values equal actual fresh appliers; HP={projected.Hp}, Cargo={projected.Cargo}, Damage={projected.Damage}");
    }

    [Test] public void HangarTechnologyAndFusedFrameHaveExactValues()
    {
        HangarLoad("tech"); var p = Project();
        Assert.That(p.Hp, Is.EqualTo(26)); Assert.That(p.Armor, Is.EqualTo(6)); Assert.That(p.Damage, Is.EqualTo(1.15f).Within(.0001));
        HangarLoad("fused"); p = Project();
        Assert.That(p.Frame.Modules, Is.EqualTo(StructuralFrameModules.StandardHeavy));
        Assert.That(p.Hp, Is.EqualTo(24)); Assert.That(p.Cargo, Is.EqualTo(125)); Assert.That(p.Damage, Is.EqualTo(1.03f).Within(.0001));
    }
    [Test] public void HangarIgnoresExistingRuntimeUpgradeLevels()
    {
        HangarLoad("branch"); var before = Project();
        var trait = catalog.FindById("mg_stable_feed");
        store.AddOrUpgrade(trait); store.AddOrUpgrade(trait); store.AddOrUpgrade(trait);
        var after = Project();
        Assert.That(store.GetLevel(trait.TraitId), Is.EqualTo(3));
        Assert.That(after.FireRate, Is.EqualTo(before.FireRate));
        float lv1 = trait.LevelEffects.Where(e => e.Level == 1 && e.EffectType == TraitEffectType.FireRatePercent).Sum(e => e.Value);
        Assert.That(after.FireRate, Is.EqualTo(1 + lv1 * .01f).Within(.0001));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void HangarPreviewBranchSharedLvOneAndNoZeroNoise(int index)
    {
        HangarLoad("shared"); var p = Project(index);
        Assert.That(p.Cargo, Is.GreaterThan(ships[index].CargoCapacity)); Assert.That(p.MovePercent, Is.GreaterThan(0));
        HangarLoad("branch"); p = Project(index);
        Assert.That(p.FireRate > 1, Is.EqualTo(index == 0));
        HangarLoad("none");
        string text = HangarDeploymentText.Build(ships[index], Project(index), true, true, progress, "");
        foreach (string noise in new[] { "이동 +0%", "대시 +0", "추가 패시브 없음", "+0%" }) Assert.That(text, Does.Not.Contain(noise));
        Assert.That(text, Does.Contain(StatPresentation.Rich(StatCategory.Health, "HP 20")));
        Assert.That(text, Does.Contain(StatPresentation.Hex(StatCategory.Cargo)));
        Assert.That(text.Contains("기체 특성"), Is.False, "Numeric chassis modifiers already appear in the projection.");
        Assert.That(text.Contains("방어도"), Is.False);
    }
    [Test] public void HangarLockedPreviewNeverClaimsFittedDeployment()
    {
        HangarLoad("all"); string text = HangarDeploymentText.Build(ships[2], Project(2), false, false, progress, "");
        Assert.That(text, Does.Contain("분석한 부품")); Assert.That(text, Does.Contain("저격총"));
        Assert.That(text, Does.Not.Contain("출격 예상")); Assert.That(text, Does.Not.Contain("HP "));
    }
    [Test] public void HangarChangedChainRefreshesFitUnfitAndShipReinforcement()
    {
        ResearchAndResources();
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Call(controller, "OnEnable"); Call(ui, "OnEnable");
        ui.ShowMainPanel();
        var hud = AuthoredRuntimeFixture.Single<SettlementHUD>(scene); var body = Get<TextMeshProUGUI>(hud, "shipBodyText");
        var trait = catalog.FindById("shared_cargo_bay");
        progress.TryManufactureEquipment(trait); string baseline = body.text;
        Assert.That(progress.TrySetEquipmentFitted(trait, true), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(body.text, Is.Not.EqualTo(baseline));
        Assert.That(progress.TrySetEquipmentFitted(trait, false), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(body.text, Is.EqualTo(baseline));
        Assert.That(progress.TryUpgradeSectorTechnology(SectorTechnologyCatalog.StabilizedFrameId), Is.True);
        Assert.That(body.text, Is.Not.EqualTo(baseline)); Assert.That(body.richText, Is.True);
        Call(ui, "OnDisable"); Call(controller, "OnDisable");
    }
}
