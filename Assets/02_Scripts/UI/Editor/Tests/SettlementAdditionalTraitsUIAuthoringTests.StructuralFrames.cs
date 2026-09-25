using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    public static readonly string[] StructuralIds = { StructuralFrameProfile.LightweightId, StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId };
    private void PrepareStructuralFixture(StructuralFrameModules modules, int ordinaryCount = 0)
    {
        ResearchAndResources();
        SaveData data = progress.CreateSaveData();
        data.equipmentLoadoutTraitIds.Clear(); data.manufacturedEquipmentIds.Clear();
        foreach (string id in StructuralIds.Where(id => (StructuralFrameProfile.ModuleFor(id) & modules) != 0)
            .Concat(EquipmentFinalRosterAuthoring.Roster[0].Concat(EquipmentFinalRosterAuthoring.Roster[1])
                .Where(id => StructuralFrameProfile.ModuleFor(id) == StructuralFrameModules.None).Take(ordinaryCount)))
        {
            data.equipmentLoadoutTraitIds.Add(id); data.manufacturedEquipmentIds.Add(id);
        }
        progress.LoadFromSave(data);
    }

    [TestCase(StructuralFrameModules.None, 1f, 1f, 0f, 0, 0, 0f)]
    [TestCase(StructuralFrameModules.Lightweight, 1.12f, .85f, .6f, -3, -20, 0f)]
    [TestCase(StructuralFrameModules.Standard, 1f, 1f, 0f, 0, 10, 5f)]
    [TestCase(StructuralFrameModules.Heavy, .9f, 1.15f, 0f, 6, 30, 0f)]
    [TestCase(StructuralFrameModules.LightweightStandard, 1.08f, .9f, 0f, -2, -5, 3f)]
    [TestCase(StructuralFrameModules.LightweightHeavy, 1.04f, 1f, .3f, 2, 10, 0f)]
    [TestCase(StructuralFrameModules.StandardHeavy, .94f, 1.08f, 0f, 4, 25, 3f)]
    [TestCase(StructuralFrameModules.Integrated, 1.03f, 1f, .15f, 3, 15, 3f)]
    public void StructuralProfilesApplyExactActualStatsWithoutStacking(StructuralFrameModules type,
        float move, float cooldown, float distance, int hp, int cargo, float damage)
    {
        PrepareStructuralFixture(type);
        RunContext run = StartRunFixture().CurrentRun;
        var frame = run.FrameProfile;
        Assert.That(frame.Modules, Is.EqualTo(type));
        Assert.That(frame.MoveMultiplier, Is.EqualTo(move).Within(.0001));
        Assert.That(frame.DashCooldownMultiplier, Is.EqualTo(cooldown).Within(.0001));
        Assert.That(frame.DashDistanceBonus, Is.EqualTo(distance).Within(.0001));
        Assert.That(frame.MaxHpBonus, Is.EqualTo(hp)); Assert.That(frame.CargoBonus, Is.EqualTo(cargo));
        Assert.That(frame.DamagePercent, Is.EqualTo(damage)); Assert.That(frame.HarvestYieldPercent, Is.EqualTo(damage));
        GameObject player = AuthoredRuntimeFixture.Create(scene, services.transform, "FrameStats", false);
        var health = player.AddComponent<PlayerHealth>(); var armor = player.AddComponent<PlayerArmor>();
        var movement = player.AddComponent<PlayerController2D>(); var dash = player.AddComponent<PlayerDash>();
        var modifiers = player.AddComponent<PlayerWeaponModifiers>(); var bonus = player.AddComponent<PlayerRuntimeBonusState>();
        var applier = player.AddComponent<PlayerRuntimeStatApplier>(); Set(applier, "logApplyResult", false);
        object foreignOwner = new object();
        movement.SetExternalMoveSpeedMultiplier(foreignOwner, 1.1f); dash.SetExternalCooldownMultiplier(foreignOwner, 1.2f);
        Action<bool> apply = refill => applier.Apply(run, progress, ships, Array.Empty<BuildingDefinition>(), catalog.TraitDefinitions, refill);
        apply(true);
        Assert.That(health.MaxHp, Is.EqualTo(Mathf.Max(1, ships[0].MaxHp + hp)));
        Assert.That(health.CurrentHp, Is.EqualTo(health.MaxHp));
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(ships[0].CargoCapacity + cargo));
        Assert.That(movement.EffectiveMoveSpeed, Is.EqualTo(6 * (1 + ships[0].MoveSpeedBonusPercent / 100) * move * 1.1f).Within(.0001));
        Assert.That(dash.EffectiveDashCooldown, Is.EqualTo((1.1f - ships[0].DashCooldownReduction) * cooldown * 1.2f).Within(.0001));
        Assert.That(dash.DashDistance, Is.EqualTo(5 + ships[0].DashDistanceBonus + distance).Within(.0001));
        Assert.That(modifiers.DamageMultiplier, Is.EqualTo(1 + damage / 100f).Within(.0001));
        Assert.That(bonus.HarvestYieldMultiplier, Is.EqualTo((1 + ships[0].HarvestYieldBonusPercent / 100) *
            (1 + damage / 100f)).Within(.0001));
        float speed = movement.EffectiveMoveSpeed, dashCooldown = dash.EffectiveDashCooldown;
        applier.RestoreCurrentVitals(3, 0);
        run.Wallet.Add(CurrencyType.ScrapParts, 1000);
        string wallet = JsonUtility.ToJson(run.Wallet);
        apply(false); apply(false);
        Assert.That(health.CurrentHp, Is.EqualTo(3)); Assert.That(armor.CurrentArmor, Is.Zero);
        Assert.That(movement.EffectiveMoveSpeed, Is.EqualTo(speed * .92f).Within(.0001), "Existing full-cargo penalty remains source-owned.");
        Assert.That(dash.EffectiveDashCooldown, Is.EqualTo(dashCooldown * 1.2f).Within(.0001));
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(ships[0].CargoCapacity + cargo));
        Assert.That(JsonUtility.ToJson(run.Wallet), Is.EqualTo(wallet), "Reconstruction may not discard over-capacity cargo.");
        Call(applier, "OnDisable"); Call(applier, "OnDisable");
        Assert.That(movement.EffectiveMoveSpeed, Is.EqualTo(movement.MoveSpeed * 1.1f * .92f).Within(.0001));
        Assert.That(dash.EffectiveDashCooldown, Is.EqualTo(dash.DashCooldown * 1.2f * 1.2f).Within(.0001));
        Call(applier, "OnEnable");
        Assert.That(movement.EffectiveMoveSpeed, Is.EqualTo(speed * .92f).Within(.0001));
        TestContext.WriteLine($"FRAME {type} move={move:F2} dash={cooldown:F2} distance={distance:F2} HP={hp} cargo={cargo}");
    }


    [TestCaseSource(nameof(StructuralIds))]
    public void StructuralBlueprintUsesRowTwoExactOnceManufacturingAndMaxLevelFiltering(string id)
    {
        var trait = catalog.FindById(id);
        Assert.That(trait.MaxLevel, Is.EqualTo(1));
        Assert.That(trait.LevelEffects, Is.Empty, "The snapshot profile is the sole effect owner.");
        Assert.That(trait.DevelopmentResearchTier, Is.EqualTo(1));
        Assert.That(trait.Category, Is.EqualTo(TraitCategory.Shared));
        ResearchAndResources(2);
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.ResearchLocked));
        ResearchAndResources(3);
        int scrap = progress.ScrapParts, core = progress.CoreShards;
        ManufactureAndFit(trait);
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.AlreadyManufactured));
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap - trait.ManufacturingScrapCost));
        Assert.That(progress.CoreShards, Is.EqualTo(core - trait.ManufacturingCoreCost));
        var run = StartRunFixture().CurrentRun;
        Assert.That(store.GetLevel(id), Is.EqualTo(1));
        Assert.That(store.CanUpgrade(trait), Is.False);
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(trait), Is.False);
        Assert.That(RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3), Is.Empty);
        Assert.That(run.FrameProfile.Modules, Is.EqualTo(StructuralFrameProfile.ModuleFor(id)));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void ImmutableStructuralCombinationSurvivesPortalDismantlingAndNextRun(int bits)
    {
        var modules = (StructuralFrameModules)bits;
        PrepareStructuralFixture(modules);
        ManufactureAndFit(catalog.FindById("mg_guidance_control"));
        var manager = StartRunFixture(); var run = manager.CurrentRun;
        var player = ReconstructPlayer(run);
        Assert.That(RunTraitAcquisitionService.TryAcquire(catalog.FindById("mg_guidance_control"), player, out _, out int level), Is.True);
        Assert.That(level, Is.EqualTo(2));
        int cargo = run.MaxCargoCapacity;
        foreach (string id in StructuralIds) if (store.GetLevel(id) > 0)
        {
            player.GetComponent<RunTraitEffectApplier>().RemoveTraitLevel(catalog.FindById(id), 1);
            Assert.That(store.TryRemoveLevel(id, out _, out _), Is.True);
            Assert.That(store.CanUpgrade(catalog.FindById(id)), Is.False, "The snapshotted frame is still active at MAX.");
            Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(catalog.FindById(id)), Is.False);
        }
        var live = progress.CreateSaveData(); live.equipmentLoadoutTraitIds.Clear(); progress.LoadFromSave(live);
        run.SetEquippedReinforcement("rf_emergency_return_anchor", 0);
        run.Wallet.Add(CurrencyType.ScrapParts, 3);
        string wallet = JsonUtility.ToJson(run.Wallet);
        run.CapturePlayerVitals(3, 0); run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        var next = ReconstructPlayer(run);
        next.GetComponent<ExpeditionBootstrap>().Initialize();
        Assert.That(run.FrameProfile.Modules, Is.EqualTo(modules));
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(cargo));
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(next.GetComponent<PlayerArmor>().CurrentArmor, Is.Zero);
        Assert.That(store.GetLevel("mg_guidance_control"), Is.EqualTo(2));
        Assert.That(run.EquippedReinforcementCharges, Is.Zero);
        Assert.That(JsonUtility.ToJson(run.Wallet), Is.EqualTo(wallet));
        manager.AbandonActiveRunWithoutRewards(); manager.ReleaseRunEndingPresentationOwnership();
        Assert.That(store.TraitLevels, Is.Empty);
        Assert.That(run.IsActive, Is.False);
        Set(state, "currentState", GameState.Settlement);
        var changed = modules == StructuralFrameModules.Integrated ? StructuralFrameModules.None : StructuralFrameModules.Integrated;
        PrepareStructuralFixture(changed);
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        var fresh = ReconstructPlayer(manager.CurrentRun);
        Assert.That(manager.CurrentRun.FrameProfile.Modules, Is.EqualTo(changed));
        Assert.That(fresh.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(fresh.GetComponent<PlayerHealth>().MaxHp));
    }

    [TestCase(6)] [TestCase(12)] [TestCase(18)] [TestCase(24)]
    public void OrdinaryEquipmentCountCannotChangeAnyStructuralProfile(int count)
    {
        foreach (StructuralFrameModules modules in Enum.GetValues(typeof(StructuralFrameModules)))
        {
            string[] ids = StructuralIds.Where(id => (StructuralFrameProfile.ModuleFor(id) & modules) != 0)
                .Concat(Enumerable.Repeat("shared_cargo_bay", count)).ToArray();
            Assert.That(StructuralFrameProfile.ResolveModules(ids), Is.EqualTo(modules));
            foreach (var ship in ships)
            {
                var profile = new StructuralFrameProfile(modules);
                Assert.That(ship.MaxHp + profile.MaxHpBonus, Is.GreaterThan(0));
                Assert.That(ship.CargoCapacity + profile.CargoBonus, Is.GreaterThan(0));
            }
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void SelectedShipFiltersAuthoredBranchesAndRetainsAllPreferences(int shipIndex)
    {
        ResearchAndResources();
        foreach (string id in EquipmentFinalRosterAuthoring.Roster.SelectMany(x => x)) ManufactureAndFit(catalog.FindById(id));
        OpenEquipment();
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Call(controller, "OnEnable");
        try
        {
            progress.SetSelectedShipId(ships[shipIndex].ShipId);
            string[] fitted = progress.EquipmentLoadoutTraitIds.ToArray();
            var expected = new[] { ShipTraitBranchKind.Shared, catalog.FindById(EquipmentFinalRosterAuthoring.Roster[shipIndex + 1][0]).DevelopmentBranch };
            foreach (string field in new[] { "sharedTabButton", "machineGunTabButton", "shotgunTabButton", "sniperTabButton" })
            {
                var tab = Get<ShipTraitBranchTabButton>(panel, field);
                Assert.That(tab.gameObject.activeSelf, Is.EqualTo(expected.Contains(tab.BranchKind)));
            }
            panel.SelectEquipmentBranch(expected[1]);
            string selected = progress.SelectedShipId;
            panel.InspectEquipment(catalog.FindById(EquipmentFinalRosterAuthoring.Roster[(shipIndex + 1) % 3 + 1][0]));
            Assert.That(progress.SelectedShipId, Is.EqualTo(selected));
            Assert.That(Get<PreparedEquipmentView[]>(panel, "equipmentSlots").All(v => v.definition.DevelopmentBranch == expected[1]), Is.True);
            progress.SetSelectedShipId(ships[(shipIndex + 1) % 3].ShipId);
            Assert.That(Get<PreparedEquipmentView[]>(panel, "equipmentSlots").All(v => v.definition.Category == TraitCategory.Shared), Is.True, "Invalid tab falls back through the actual change event.");
            Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(fitted));
            var effective = new System.Collections.Generic.List<string>(); progress.AppendEffectiveEquipment(effective, progress.LastSelectedWeaponTree);
            Assert.That(effective.Count, Is.EqualTo(24));
            Assert.That(effective, Is.SupersetOf(StructuralIds));
        }
        finally { Call(controller, "OnDisable"); }
    }

    [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
    [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
    [TestCase(-1, true)] [TestCase(99, true)]
    public void VersionSevenSelectionMigratesOnceOnlyAfterResearch(int oldFrame, bool researched)
    {
        var data = Checkpoint(new SaveData(), researched ? 3 : 2);
        data.version = 7; data.selectedOperatingFrame = oldFrame;
        data.scrapParts = 73; data.coreShards = 5; data.stabilizedAlloy = 9;
        data.manufacturedEquipmentIds.Add("shared_salvage_protocol");
        data.equipmentLoadoutTraitIds.Add("shared_salvage_protocol");
        System.IO.File.WriteAllText(savePath, JsonUtility.ToJson(data));
        var migrated = save.LoadOrCreate();
        Assert.That(migrated.version, Is.EqualTo(8));
        Assert.That(migrated.selectedOperatingFrame, Is.EqualTo(-1));
        progress.LoadFromSave(migrated); save.Save(progress);
        bool granted = researched && oldFrame >= 0 && oldFrame <= 2;
        Assert.That(progress.ManufacturedEquipmentIds.Count, Is.EqualTo(granted ? 2 : 1));
        if (granted)
        {
            string id = oldFrame == 1 ? StructuralIds[0] : oldFrame == 2 ? StructuralIds[2] : StructuralIds[1];
            Assert.That(progress.IsEquipmentFitted(id), Is.True);
            Assert.That(progress.TrySetEquipmentFitted(catalog.FindById(id), false), Is.EqualTo(EquipmentDevelopmentResult.Success));
        }
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        for (int i = 0; i < 3; i++) { progress.LoadFromSave(save.LoadOrCreate()); save.Save(progress); }
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before), "No reload refits or regrants the default Standard.");
        Assert.That(progress.ScrapParts, Is.EqualTo(73)); Assert.That(progress.CoreShards, Is.EqualTo(5)); Assert.That(progress.StabilizedAlloy, Is.EqualTo(9));
        Assert.That(progress.IsEquipmentFitted("shared_salvage_protocol"), Is.True);
    }

    [Test]
    public void PreFrameVersionSevenJsonDoesNotInventDefaultStandardOwnership()
    {
        System.IO.File.WriteAllText(savePath, "{\"version\":7,\"highestUnlockedDepth\":1,\"acquiredBossStoryParts\":[1]}");
        var data = save.LoadOrCreate();
        Assert.That(data.manufacturedEquipmentIds, Is.Empty);
        Assert.That(data.selectedOperatingFrame, Is.EqualTo(-1));
    }

    [Test]
    public void StructuralRosterReplacesExactlyThreePositionsAndRetainsLegacyOwners()
    {
        Assert.That(catalog.TraitDefinitions.Where(t => t.IsDevelopmentRoster && t.Category == TraitCategory.Shared && t.DevelopmentResearchTier == 1)
            .OrderBy(t => t.DevelopmentDisplayOrder).Select(t => t.TraitId), Is.EqualTo(StructuralIds));
        foreach (string id in new[] { "shared_salvage_protocol", "shared_reinforced_plating", "shared_repair_foam" })
        {
            var trait = catalog.FindById(id);
            Assert.That(trait, Is.Not.Null); Assert.That(trait.IsDevelopmentRoster, Is.False);
            Assert.That(progress.IsEquipmentResearched(trait), Is.False);
            Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.InvalidDefinition));
            PrepareFixture(trait);
            Assert.That(progress.IsEquipmentUsable(trait, WeaponTreeType.MachineGun), Is.True);
        }
        foreach (var branch in new[] { ShipTraitBranchKind.Shared, ShipTraitBranchKind.MachineGun, ShipTraitBranchKind.Shotgun, ShipTraitBranchKind.Sniper })
            Assert.That(catalog.TraitDefinitions.Count(t => t.IsDevelopmentRoster && t.DevelopmentBranch == branch), Is.EqualTo(12));
        var run = StartRunFixture().CurrentRun; var player = ReconstructPlayer(run);
        Assert.That(store.GetLevel("shared_salvage_protocol"), Is.EqualTo(1));
        Assert.That(player.GetComponent<PlayerRuntimeBonusState>().HarvestYieldMultiplier, Is.GreaterThan(1));
    }

    [Test]
    public void StructuralInspectionProjectsFusionAndKeepsSelectionSeparateFromFitting()
    {
        ResearchAndResources(3); ManufactureAndFit(catalog.FindById(StructuralFrameProfile.StandardId));
        OpenEquipment(); panel.InspectEquipment(catalog.FindById(StructuralFrameProfile.LightweightId));
        Assert.That(Get<GameObject>(panel, "equipmentDevelopmentRoot").transform.Find("OperatingFrames"), Is.Null);
        var rows = Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows");
        Assert.That(rows[0].effects.text, Does.Contain("+12%"));
        Assert.That(rows[1].effects.text, Does.Contain("현재: 표준"));
        Assert.That(rows[2].effects.text, Does.Contain("경량 + 표준").And.Contain("+8%").And.Contain("-10%").And.Not.Contain("+12%"));
        Assert.That(Get<TMP_Text>(panel, "equipmentCompatibility").text, Is.EqualTo("구조 장비 · 공용"));
        var view = Get<PreparedEquipmentView[]>(panel, "equipmentSlots").Single(v => v.definition?.TraitId == StructuralFrameProfile.LightweightId);
        Assert.That(view.button.colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(view.button.colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(view.fittedIndicator.text, Is.Empty);
        Assert.That(progress.IsEquipmentFitted(StructuralFrameProfile.LightweightId), Is.False);
    }

    [TestCase("Tutorial")] [TestCase("Expedition")]
    public void StructuralInspectionIsAuthoredInBothInventories(string sceneName)
    {
        var other = AuthoredRuntimeFixture.Open(sceneName);
        try
        {
            var inspection = AuthoredRuntimeFixture.Single<PlayerBuildStatusPanelUI>(other);
            Assert.That(inspection.HasStructuralFramePresentation, Is.True);
            Assert.That(Get<GameObject>(inspection, "structuralFrameInspectionRoot").activeSelf, Is.False);
        }
        finally { AuthoredRuntimeFixture.Close(other); }
    }
}
