using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class InventoryPresentationTests
{
    private const string PrefabPath = "Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Plain(string rich) => Regex.Replace(rich, "<color=#[0-9A-Fa-f]{6}>|</color>", "");
    private static T Read<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Private).SetValue(value, data);
    private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Private).Invoke(value, args);

    [Test]
    public void EveryTraitAndReinforcementEffectHasAnExplicitSemanticMapping()
    {
        foreach (TraitEffectType effect in Enum.GetValues(typeof(TraitEffectType)))
        {
            Assert.That(StatPresentation.Hex(StatPresentation.Category(effect)), Does.Match("^#[0-9A-F]{6}$"), effect.ToString());
            Assert.That(TraitEffectTextUtility.FormatEffect(effect, 3), Does.Not.Contain("효과 정보 없음"));
            Assert.That(Plain(StatPresentation.Trait(effect, 3)), Is.EqualTo(TraitEffectTextUtility.FormatEffect(effect, 3)));
        }
        foreach (ReinforcementEffectType effect in Enum.GetValues(typeof(ReinforcementEffectType)))
            Assert.That(StatPresentation.Hex(StatPresentation.Category(effect)), Does.Match("^#[0-9A-F]{6}$"), effect.ToString());
    }

    [TestCase(StatCategory.Health, "#FF6B6B")]
    [TestCase(StatCategory.Defense, "#55D6BE")]
    [TestCase(StatCategory.Damage, "#FF9F43")]
    [TestCase(StatCategory.FireRate, "#FFD166")]
    [TestCase(StatCategory.Movement, "#9BE564")]
    [TestCase(StatCategory.Dash, "#46E6C8")]
    [TestCase(StatCategory.Cargo, "#D7A75E")]
    [TestCase(StatCategory.Harvest, "#6FD08C")]
    [TestCase(StatCategory.ProjectileSpeed, "#6CCBFF")]
    [TestCase(StatCategory.Range, "#5B8CFF")]
    [TestCase(StatCategory.Accuracy, "#7EE7F2")]
    [TestCase(StatCategory.Homing, "#B58CFF")]
    [TestCase(StatCategory.Pierce, "#D77BFF")]
    [TestCase(StatCategory.Charge, "#FF82C8")]
    [TestCase(StatCategory.Radar, "#44DDE7")]
    [TestCase(StatCategory.Active, "#7AA8FF")]
    [TestCase(StatCategory.Recovery, "#70E08F")]
    [TestCase(StatCategory.Special, "#FFD76A")]
    public void PaletteIsSemanticAndPreservesPenaltyText(StatCategory category, string hex)
    {
        Assert.That(StatPresentation.Hex(category), Is.EqualTo(hex));
        Assert.That(StatPresentation.Rich(category, "Stat -3"), Is.EqualTo("<color=" + hex + ">Stat -3</color>"));
    }

    [Test]
    public void CatalogPlainAndRichEffectsHaveIdenticalContentIncludingMechanics()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        foreach (TraitDefinition trait in catalog.TraitDefinitions.Where(t => t != null))
        for (int level = 1; level <= trait.MaxLevel; level++)
        {
            string plain = TraitEffectTextUtility.BuildEffectText(trait, level);
            Assert.That(plain, Does.Not.Contain("<color"), trait.TraitId);
            Assert.That(Plain(TraitEffectTextUtility.BuildRichEffectText(trait, level)), Is.EqualTo(plain), trait.TraitId);
            Assert.That(plain, Does.Not.Contain("효과 정보 없음"), trait.TraitId);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ReinforcementDefinition"))
        {
            var active = AssetDatabase.LoadAssetAtPath<ReinforcementDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(Plain(active.BuildRichEffectSummary()), Is.EqualTo(active.BuildEffectSummary()), active.name);
        }
        Assert.That(StatPresentation.Trait(TraitEffectType.SniperSemiAutoMode, 1), Does.Contain("세미오토 레이저"));
        Assert.That(StatPresentation.Trait(TraitEffectType.ShotgunSlugCoupler, 1), Does.Contain("관통 단일탄"));
        Assert.That(StatPresentation.Trait(TraitEffectType.MachineGunTwinFeed, 3), Does.Contain("빠른 후속탄"));
        Assert.That(StatPresentation.Category(ReinforcementEffectType.HealFlat), Is.EqualTo(StatCategory.Recovery));
        Assert.That(StatPresentation.Category(ReinforcementEffectType.AddArmor), Is.EqualTo(StatCategory.Defense));
        Assert.That(StatPresentation.Category(ReinforcementEffectType.TemporaryDamagePercent), Is.EqualTo(StatCategory.Damage));
        Assert.That(StatPresentation.Category(ReinforcementEffectType.TemporaryMoveSpeedPercent), Is.EqualTo(StatCategory.Movement));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void EveryStructuralCombinationUsesCommonColorsAndPlainSemantics(int bits)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>("Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset");
        var profile = new StructuralFrameProfile((StructuralFrameModules)bits);
        string rich = StructuralFrameText.RichModifiers(profile, catalog);
        if (bits != 0) Assert.That(StructuralFrameText.RichDetails(profile, catalog), Does.Contain("<color=#"));
        Assert.That(Plain(rich), Is.EqualTo(StructuralFrameText.Modifiers(profile, catalog)));
        if (profile.MaxHpBonus != 0) Assert.That(rich, Does.Contain("<color=#FF6B6B>"));
        if (profile.CargoBonus != 0) Assert.That(rich, Does.Contain("<color=#D7A75E>"));
        if (profile.MoveMultiplier != 1) Assert.That(rich, Does.Contain("<color=#9BE564>"));
        if (profile.DamagePercent != 0) Assert.That(rich, Does.Contain("<color=#FF9F43>"));
        if (bits == 1) { Assert.That(rich, Does.Contain("-3")); Assert.That(rich, Does.Contain("-20")); }
    }

    [TestCase("Prefab")] [TestCase("Expedition")] [TestCase("Tutorial")]
    public void AllAuthoredCopiesHaveSeparatedContentAndBoundActions(string source)
    {
        GameObject prefab = null;
        UnityEngine.SceneManagement.Scene scene = default;
        try
        {
            PlayerBuildStatusPanelUI panel;
            if (source == "Prefab") { prefab = PrefabUtility.LoadPrefabContents(PrefabPath); panel = prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true); }
            else { scene = AuthoredRuntimeFixture.Open(source); panel = AuthoredRuntimeFixture.Single<PlayerBuildStatusPanelUI>(scene); }
            Assert.That(panel.HasInventoryTabPresentation, Is.True);
            var eq = Read<GameObject>(panel, "equipmentContentRoot");
            var cargo = Read<GameObject>(panel, "cargoContentRoot");
            var story = Read<TextMeshProUGUI>(panel, "storyRecoveryTitle");
            Assert.That(story.transform.IsChildOf(eq.transform), Is.False);
            Assert.That(story.transform.IsChildOf(cargo.transform), Is.False);
            Assert.That(panel.HasStoryProgressPresentation, Is.True);
            var recovery = Read<GameObject>(panel, "storyProgressInspectionRoot");
            Assert.That(story.transform.parent.parent, Is.EqualTo(recovery.transform));
            Assert.That(recovery.activeSelf, Is.False);
            Assert.That(((RectTransform)eq.transform).sizeDelta.y, Is.GreaterThanOrEqualTo(196));
            Assert.That(((RectTransform)cargo.transform).sizeDelta.y, Is.GreaterThanOrEqualTo(196));
            Assert.That(eq.activeSelf, Is.True); Assert.That(cargo.activeSelf, Is.False);
            foreach (string field in new[] { "activeNameText", "activeEffectText", "activeChargeText", "activeStateText", "passiveScrollRect", "selectedPassiveEffectText", "activeFieldDropButton", "passiveFieldDropButton", "equipmentDetailScrollRect" })
                Assert.That(Read<Component>(panel, field).transform.IsChildOf(eq.transform), Is.True, field);
            foreach (string field in new[] { "cargoLoadText", "cargoLoadSlider", "cargoManagementRoot", "cargoQuantitySlider", "cargoJettisonButton", "cargoAutoPickupButton", "cargoReturnProjectionText" })
                Assert.That(Read<Component>(panel, field).transform.IsChildOf(cargo.transform), Is.True, field);
            var data = new SerializedObject(panel);
            var rows = data.FindProperty("cargoManifestRows");
            Assert.That(rows.arraySize, Is.EqualTo(3));
            var types = Enumerable.Range(0, rows.arraySize).Select(i => (CurrencyType)rows.GetArrayElementAtIndex(i).FindPropertyRelative("currencyType").intValue);
            Assert.That(types, Is.EquivalentTo(PlayerCargoController.SupportedCargoTypes));
        }
        finally { if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab); if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene); }
    }

    [Test]
    public void TabSwitchesKeepDataAndRejectHiddenTargetsEvenWithStaleSelection()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        var eventObject = AuthoredRuntimeFixture.Create(prefab.scene, prefab.transform, "InventoryFocusTest");
        EventSystem ownedEvent = eventObject.AddComponent<EventSystem>();
        AuthoredRuntimeFixture.SelectEventSystem(ownedEvent, prefab.scene);
        var panel = prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        try
        {
            // Keep refresh closed so this test cannot acquire gameplay singletons.
            Assert.That(panel.CurrentInventoryTab, Is.EqualTo(InventoryContentTab.Equipment));
            string selectedId = "unchanged_runtime_id";
            Set(panel, "selectedPassiveId", selectedId);
            Set(panel, "selectedPassiveIndex", 2);
            panel.ShowCargoTab();
            Assert.That(Read<GameObject>(panel, "equipmentContentRoot").activeSelf, Is.False);
            Assert.That(Read<GameObject>(panel, "cargoContentRoot").activeSelf, Is.True);
            Set(panel, "isOpen", true);
            Set(panel, "selectedFieldDropTarget", BuildStatusFieldDropTarget.Passive); // Deliberately stale.
            panel.TryDropActiveFieldItem(); panel.TryDropSelectedPassiveFieldItem(); panel.TryDropSelectedFieldItem();
            Assert.That(Read<string>(panel, "selectedPassiveId"), Is.EqualTo(selectedId));
            Assert.That(Read<BuildStatusFieldDropTarget>(panel, "selectedFieldDropTarget"), Is.EqualTo(BuildStatusFieldDropTarget.Passive));
            Set(panel, "isOpen", false);
            panel.ShowEquipmentTab();
            Set(panel, "isOpen", true);
            Set(panel, "selectedFieldDropTarget", BuildStatusFieldDropTarget.Cargo);
            Set(panel, "selectedCargoJettisonAmount", 19);
            Call(panel, "TryJettisonSelectedCargo"); Call(panel, "ToggleSelectedCargoAutoPickup");
            Assert.That(Read<int>(panel, "selectedCargoJettisonAmount"), Is.EqualTo(19));
            Set(panel, "isOpen", false);
            panel.ShowCargoTab(); panel.Close();
            Assert.That(panel.CurrentInventoryTab, Is.EqualTo(InventoryContentTab.Cargo));
            Assert.That(Read<float>(panel, "cargoJettisonHoldTimer"), Is.Zero);
            Assert.That(Read<bool>(panel, "cargoJettisonConsumedUntilRelease"), Is.True);
            panel.ShowEquipmentTab();
            for (Transform parent = Read<Button>(panel, "equipmentTabButton").transform; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
            Call(panel, "FocusInventoryTab");
            // Focus method never chooses an invisible Cargo control.
            Assert.That(panel.FirstInventorySelectable, Is.EqualTo(Read<Button>(panel, "equipmentTabButton")));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.FirstInventorySelectable.gameObject));
            Assert.That(Read<string>(panel, "selectedPassiveId"), Is.EqualTo(selectedId));
        }
        finally { AuthoredRuntimeFixture.ReleaseEventSystem(ref ownedEvent, prefab.scene); PrefabUtility.UnloadPrefabContents(prefab); }
    }

    [Test]
    public void RecoveryLogIsReadOnlyBlocksDropTargetsAndClosesWithInventory()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        var panel = prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        var eventObject = AuthoredRuntimeFixture.Create(prefab.scene, prefab.transform, "RecoveryFocus");
        EventSystem ownedEvent = eventObject.AddComponent<EventSystem>();
        AuthoredRuntimeFixture.SelectEventSystem(ownedEvent, prefab.scene);
        try
        {
            var overlay = Read<GameObject>(panel, "storyProgressInspectionRoot");
            Assert.That(overlay.activeSelf, Is.False);
            for (Transform t = panel.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            Set(panel, "isOpen", true);
            Set(panel, "selectedPassiveId", "retained");
            Set(panel, "selectedCargoJettisonAmount", 17);
            panel.OpenStoryProgressInspection();
            Assert.That(overlay.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Read<Button>(panel, "storyProgressCloseButton").gameObject));
            Assert.That(Read<Button>(panel, "storyProgressCloseButton").navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(panel.GetType().GetProperty("CanUseEquipmentActions", Private).GetValue(panel), Is.False);
            Assert.That(panel.GetType().GetProperty("CanUseCargoActions", Private).GetValue(panel), Is.False);
            panel.TryDropSelectedFieldItem(); panel.TryDropSelectedPassiveFieldItem(); panel.TryDropActiveFieldItem();
            Call(panel, "TryJettisonSelectedCargo");
            Assert.That(Read<string>(panel, "selectedPassiveId"), Is.EqualTo("retained"));
            Assert.That(Read<int>(panel, "selectedCargoJettisonAmount"), Is.EqualTo(17));
            panel.CloseStoryProgressInspection();
            Assert.That(overlay.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.FirstInventorySelectable.gameObject));
            panel.OpenStoryProgressInspection(); panel.Close();
            Assert.That(overlay.activeSelf, Is.False);
            Assert.That(Read<float>(panel, "cargoJettisonHoldTimer"), Is.Zero);
        }
        finally
        {
            AuthoredRuntimeFixture.ReleaseEventSystem(ref ownedEvent, prefab.scene);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    [Test]
    public void CargoTypesUseTheExistingAuthorityAndSummaryOrderIsStable()
    {
        var run = new RunContext();
        Assert.That(run.UsesCargo(CurrencyType.Credits), Is.False);
        Assert.That(run.UsesCargo(CurrencyType.TuningChips), Is.False);
        foreach (CurrencyType type in PlayerCargoController.SupportedCargoTypes) Assert.That(run.UsesCargo(type), Is.True);
        TraitEffectType[] sorted = { TraitEffectType.ActiveCooldownReductionPercent, TraitEffectType.DamagePercent, TraitEffectType.MaxHpBonus, TraitEffectType.CargoCapacityBonus, TraitEffectType.MoveSpeedPercent };
        Array.Sort(sorted, (a,b) => StatPresentation.SortKey(a).CompareTo(StatPresentation.SortKey(b)));
        Assert.That(sorted, Is.EqualTo(new[] { TraitEffectType.MaxHpBonus, TraitEffectType.DamagePercent, TraitEffectType.MoveSpeedPercent, TraitEffectType.CargoCapacityBonus, TraitEffectType.ActiveCooldownReductionPercent }));
    }
}
