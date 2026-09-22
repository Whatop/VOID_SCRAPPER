using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    [TestCase(OperatingFrameType.Lightweight, 6, 1.12f, .85f, .6f, -3, -20)]
    [TestCase(OperatingFrameType.Lightweight, 12, 1.08f, .9f, .4f, -3, -20)]
    [TestCase(OperatingFrameType.Lightweight, 18, 1.04f, .95f, .2f, -3, -20)]
    [TestCase(OperatingFrameType.Lightweight, 24, 1f, 1f, 0f, -3, -20)]
    [TestCase(OperatingFrameType.Standard, 6, 1f, 1f, 0f, 0, 10)]
    [TestCase(OperatingFrameType.Standard, 12, 1f, 1f, 0f, 0, 10)]
    [TestCase(OperatingFrameType.Standard, 18, 1f, 1f, 0f, 0, 10)]
    [TestCase(OperatingFrameType.Standard, 24, 1f, 1f, 0f, 0, 10)]
    [TestCase(OperatingFrameType.Heavy, 6, .9f, 1.15f, 0f, 2, 10)]
    [TestCase(OperatingFrameType.Heavy, 12, .9f, 1.15f, 0f, 4, 20)]
    [TestCase(OperatingFrameType.Heavy, 18, .9f, 1.15f, 0f, 6, 30)]
    [TestCase(OperatingFrameType.Heavy, 24, .9f, 1.15f, 0f, 8, 40)]
    public void OperatingFrameProfileAppliesActualStatsWithoutStacking(OperatingFrameType type, int count,
        float move, float cooldown, float distance, int hp, int cargo)
    {
        PrepareFrameFixture(type, count);
        RunContext run = StartRunFixture().CurrentRun;
        Assert.That(run.OperatingFrame, Is.EqualTo(type));
        Assert.That(run.FittedOrdinaryEquipmentCount, Is.EqualTo(count));
        var frame = run.FrameProfile;
        Assert.That(frame.MoveMultiplier, Is.EqualTo(move).Within(.0001));
        Assert.That(frame.DashCooldownMultiplier, Is.EqualTo(cooldown).Within(.0001));
        Assert.That(frame.DashDistanceBonus, Is.EqualTo(distance).Within(.0001));
        Assert.That(frame.MaxHpBonus, Is.EqualTo(hp)); Assert.That(frame.CargoBonus, Is.EqualTo(cargo));

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
        Assert.That(modifiers.DamageMultiplier, Is.EqualTo(type == OperatingFrameType.Standard ? 1.05f : 1).Within(.0001));
        Assert.That(bonus.HarvestYieldMultiplier, Is.EqualTo((1 + ships[0].HarvestYieldBonusPercent / 100) *
            (type == OperatingFrameType.Standard ? 1.05f : 1)).Within(.0001));
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
        TestContext.WriteLine($"FRAME {type} count={count} move={move:F2} dash={cooldown:F2} distance={distance:F2} HP={hp} cargo={cargo}");
    }

    private void PrepareFrameFixture(OperatingFrameType frame, int count)
    {
        ResearchAndResources();
        Assert.That(progress.TrySetOperatingFrame(frame), Is.True);
        SaveData data = progress.CreateSaveData();
        data.equipmentLoadoutTraitIds.Clear(); data.manufacturedEquipmentIds.Clear();
        foreach (string id in EquipmentFinalRosterAuthoring.Roster[0].Concat(EquipmentFinalRosterAuthoring.Roster[1]).Take(count))
        {
            data.equipmentLoadoutTraitIds.Add(id); data.manufacturedEquipmentIds.Add(id);
        }
        progress.LoadFromSave(data);
    }

    [TestCase(0, 0)] [TestCase(6, 0)] [TestCase(7, 1)] [TestCase(12, 1)]
    [TestCase(13, 2)] [TestCase(18, 2)] [TestCase(19, 3)] [TestCase(24, 3)] [TestCase(38, 3)]
    public void FrameBandsAreInclusiveAndNeverLimitFitting(int count, int tier)
    {
        foreach (OperatingFrameType type in Enum.GetValues(typeof(OperatingFrameType)))
        {
            var frame = new OperatingFrameProfile(type, count);
            Assert.That(frame.Tier, Is.EqualTo(tier)); Assert.That(frame.EquipmentCount, Is.EqualTo(count));
        }
    }

    [Test]
    public void ExistingAndMalformedSavesDefaultToStandardWithoutRepeatingEquipmentMigration()
    {
        System.IO.File.WriteAllText(savePath, "{\"version\":7,\"scrapParts\":73,\"equipmentLoadoutTraitIds\":[\"mg_guidance_control\"],\"manufacturedEquipmentIds\":[\"mg_guidance_control\"]}");
        var loaded = save.LoadOrCreate();
        Assert.That(loaded.selectedOperatingFrame, Is.EqualTo(OperatingFrameType.Standard));
        Assert.That(loaded.equipmentRosterMigrationPending, Is.False);
        progress.LoadFromSave(loaded);
        foreach (OperatingFrameType frame in Enum.GetValues(typeof(OperatingFrameType)))
        {
            Assert.That(progress.TrySetOperatingFrame(frame), Is.True);
            save.Save(progress);
            progress.LoadFromSave(save.LoadOrCreate());
            Assert.That(progress.SelectedOperatingFrame, Is.EqualTo(frame));
            Assert.That(progress.ScrapParts, Is.EqualTo(73));
            Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { "mg_guidance_control" }));
            Assert.That(progress.ManufacturedEquipmentIds, Is.EqualTo(new[] { "mg_guidance_control" }));
            var repeat = save.LoadOrCreate(); save.Save(repeat);
            Assert.That(JsonUtility.ToJson(save.LoadOrCreate()), Is.EqualTo(JsonUtility.ToJson(repeat)));
            for (int checkpoint = 0; checkpoint < 12; checkpoint++)
                Assert.That(Checkpoint(repeat, checkpoint).selectedOperatingFrame, Is.EqualTo(frame));
        }
        loaded.selectedOperatingFrame = (OperatingFrameType)99;
        progress.LoadFromSave(loaded); Assert.That(progress.SelectedOperatingFrame, Is.EqualTo(OperatingFrameType.Standard));
        Assert.That(progress.TrySetOperatingFrame((OperatingFrameType)99), Is.False);
    }

    [Test]
    public void FrameSnapshotCountsOnlyEffectiveOrdinaryFittingIncludingConditionalAndLegacy()
    {
        ResearchAndResources();
        foreach (string id in EquipmentFinalRosterAuthoring.Roster.SelectMany(branch => branch)) PrepareFixture(catalog.FindById(id));
        PrepareFixture(catalog.FindById("shared_rapid_feed"));
        SaveData data = progress.CreateSaveData();
        data.equipmentLoadoutTraitIds.Add("pixel_curse"); data.manufacturedEquipmentIds.Add("pixel_curse");
        progress.LoadFromSave(data);
        Assert.That(progress.GetEffectiveEquipmentCount(WeaponTreeType.MachineGun), Is.EqualTo(25));
        Assert.That(progress.GetEffectiveEquipmentCount(WeaponTreeType.Shotgun), Is.EqualTo(25));
        Assert.That(progress.TrySetOperatingFrame(OperatingFrameType.Heavy), Is.True);
        var manager = StartRunFixture(); RunContext run = manager.CurrentRun;
        Assert.That(run.FittedOrdinaryEquipmentCount, Is.EqualTo(25));
        Assert.That(run.PreparedEquipmentIds, Does.Contain("mg_terminal_guidance").And.Contain("shared_rapid_feed"));
        Assert.That(store.GetLevel("mg_terminal_guidance"), Is.Zero);
        Assert.That(progress.TrySetOperatingFrame(OperatingFrameType.Lightweight), Is.False);
        data = Checkpoint(progress.CreateSaveData(), 1); data.selectedOperatingFrame = OperatingFrameType.Lightweight;
        data.equipmentLoadoutTraitIds.Clear(); progress.LoadFromSave(data);
        Assert.That(run.OperatingFrame, Is.EqualTo(OperatingFrameType.Heavy));
        Assert.That(run.FittedOrdinaryEquipmentCount, Is.EqualTo(25));
        run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        Assert.That(run.FrameProfile.CargoBonus, Is.EqualTo(40));
        Assert.That(progress.ProjectedOperatingFrame.EquipmentCount, Is.Zero);
    }

    [TestCase(OperatingFrameType.Lightweight)] [TestCase(OperatingFrameType.Standard)] [TestCase(OperatingFrameType.Heavy)]
    public void FramePortalPreservesUpgradesVitalsAndNextRunSelection(OperatingFrameType frame)
    {
        PrepareFrameFixture(frame, 24);
        var manager = StartRunFixture(); RunContext run = manager.CurrentRun;
        var player = ReconstructPlayer(run); var guidance = catalog.FindById("mg_guidance_control");
        Assert.That(RunTraitAcquisitionService.TryAcquire(guidance, player, out _, out int level), Is.True);
        Assert.That(level, Is.EqualTo(2));
        int cargo = run.MaxCargoCapacity;
        run.SetEquippedReinforcement("rf_emergency_return_anchor", 0);
        run.CapturePlayerVitals(3, 0); run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        var next = ReconstructPlayer(run);
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(next.GetComponent<PlayerArmor>().CurrentArmor, Is.Zero);
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(cargo));
        Assert.That(store.GetLevel(guidance.TraitId), Is.EqualTo(2));
        Assert.That(run.EquippedReinforcementCharges, Is.Zero);
        Assert.That(run.OperatingFrame, Is.EqualTo(frame)); Assert.That(run.FittedOrdinaryEquipmentCount, Is.EqualTo(24));
        next.GetComponent<ExpeditionBootstrap>().Initialize();
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        manager.AbandonActiveRunWithoutRewards(); manager.ReleaseRunEndingPresentationOwnership();
        OperatingFrameType changed = frame == OperatingFrameType.Heavy ? OperatingFrameType.Lightweight : OperatingFrameType.Heavy;
        Set(state, "currentState", GameState.Settlement);
        Assert.That(progress.TrySetOperatingFrame(changed), Is.True);
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        var fresh = ReconstructPlayer(manager.CurrentRun);
        Assert.That(manager.CurrentRun.OperatingFrame, Is.EqualTo(changed));
        Assert.That(store.GetLevel(guidance.TraitId), Is.EqualTo(1));
        Assert.That(fresh.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(fresh.GetComponent<PlayerHealth>().MaxHp));
    }

    [Test]
    public void FrameControlsAreAuthoredSeparateFreeAndKeepBlueHoverYellowSelection()
    {
        OpenEquipment(); var errors = new List<string>();
        Assert.That(panel.ValidateOperatingFramePresentation(errors), Is.True, string.Join("\n", errors));
        var buttons = Get<Button[]>(panel, "operatingFrameButtons");
        var slots = Get<PreparedEquipmentView[]>(panel, "equipmentSlots");
        Assert.That(slots.Length, Is.EqualTo(12));
        string[] fitting = progress.EquipmentLoadoutTraitIds.ToArray(); int scrap = progress.ScrapParts, core = progress.CoreShards;
        buttons[0].onClick.Invoke();
        Assert.That(progress.SelectedOperatingFrame, Is.EqualTo(OperatingFrameType.Lightweight));
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Contain("0종").And.Contain("MAX"));
        Assert.That(Get<Button>(panel, "equipmentActivationButton").gameObject.activeSelf, Is.False);
        buttons[0].OnPointerEnter(new PointerEventData(EventSystem.current));
        Assert.That(buttons[0].colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        buttons[0].OnPointerExit(new PointerEventData(EventSystem.current));
        Assert.That(buttons[0].colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(fitting));
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap)); Assert.That(progress.CoreShards, Is.EqualTo(core));
        buttons[1].onClick.Invoke();
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Not.Contain("MAX").And.Not.Contain("등급"));
        Transform root = Get<GameObject>(panel, "equipmentDevelopmentRoot").transform;
        var branch = Get<ShipTraitBranchTabButton>(panel, "sharedTabButton");
        float frameBottom = ((RectTransform)root.Find("OperatingFrames")).rect.yMin + ((RectTransform)root.Find("OperatingFrames")).anchoredPosition.y;
        RectTransform tab = (RectTransform)branch.transform;
        Assert.That(frameBottom, Is.GreaterThan(tab.anchoredPosition.y + tab.rect.yMax));
        foreach (var view in slots)
        {
            RectTransform rect = (RectTransform)view.button.transform;
            Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(65)); Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(22));
            Assert.That(rect.anchoredPosition.y + rect.rect.yMin, Is.GreaterThan(-100));
        }
    }

    [Test]
    public void FrameDiagnosticsAreMonotonicAndSafeForAllAuthoredShips()
    {
        float previousMove = 2; int previousHp = 0, previousCargo = 0;
        foreach (int count in new[] { 6, 12, 18, 24 })
        {
            var light = new OperatingFrameProfile(OperatingFrameType.Lightweight, count);
            var heavy = new OperatingFrameProfile(OperatingFrameType.Heavy, count);
            var standard = new OperatingFrameProfile(OperatingFrameType.Standard, count);
            Assert.That(light.MoveMultiplier, Is.LessThan(previousMove)); previousMove = light.MoveMultiplier;
            Assert.That(heavy.MaxHpBonus, Is.GreaterThan(previousHp)); previousHp = heavy.MaxHpBonus;
            Assert.That(heavy.CargoBonus, Is.GreaterThan(previousCargo)); previousCargo = heavy.CargoBonus;
            Assert.That(standard.DamagePercent, Is.EqualTo(5)); Assert.That(standard.HarvestYieldPercent, Is.EqualTo(5));
            foreach (ShipDefinition ship in ships)
            {
                Assert.That(ship.MaxHp + light.MaxHpBonus, Is.GreaterThanOrEqualTo(1));
                Assert.That(ship.CargoCapacity + light.CargoBonus, Is.GreaterThan(0));
                float heavySpeed = 6 * (1 + ship.MoveSpeedBonusPercent / 100) * heavy.MoveMultiplier;
                Assert.That(heavySpeed, Is.GreaterThan(3));
                TestContext.WriteLine($"FRAME_SHIP {ship.ShipId} count={count} lightHp={ship.MaxHp - 3} lightCargo={ship.CargoCapacity - 20} heavyMove={heavySpeed:F2} heavyHp={ship.MaxHp + heavy.MaxHpBonus} heavyCargo={ship.CargoCapacity + heavy.CargoBonus}");
            }
        }
    }

    [TestCase("Tutorial")] [TestCase("Expedition")]
    public void FrameInspectionIsAuthoredInBothSharedInventoryInstances(string sceneName)
    {
        var other = AuthoredRuntimeFixture.Open(sceneName);
        try
        {
            var inspection = AuthoredRuntimeFixture.Single<PlayerBuildStatusPanelUI>(other);
            Assert.That(inspection.HasOperatingFramePresentation, Is.True);
            Assert.That(Get<GameObject>(inspection, "operatingFrameInspectionRoot").activeSelf, Is.False);
        }
        finally { AuthoredRuntimeFixture.Close(other); }
    }
}
