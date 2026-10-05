using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class InventoryPolishTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private GameObject prefab, services;
    private PlayerBuildStatusPanelUI panel;
    private RunManager oldRun;
    private PermanentProgress oldProgress, progress;
    private object oldStore, oldLocalization;
    private string oldLanguage;
    private bool oldLoaded;
    private RunContext run;
    private EventSystem ownedEvent;
    private static T Read<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    private static void Set(object o, string field, object v) => o.GetType().GetField(field, Private).SetValue(o, v);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    private static void Singleton(Type t, object v) => t.GetProperty("Instance").SetValue(null, v);
    private T Field<T>(string field) => Read<T>(panel, field);

    [SetUp]
    public void Setup()
    {
        oldRun = RunManager.Instance; oldProgress = PermanentProgress.Instance;
        oldStore = typeof(RunRuntimeTraitStore).GetField("instance", Static).GetValue(null);
        oldLocalization = typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).GetValue(null);
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, null);
        oldLanguage = (string)typeof(GameSettingsRuntime).GetField("languageCode", Static).GetValue(null);
        oldLoaded = (bool)typeof(GameSettingsRuntime).GetField("loaded", Static).GetValue(null);
        Language("ko");
        prefab = PrefabUtility.LoadPrefabContents(InventoryPolishAuthoring.PrefabPath);
        panel = prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        services = AuthoredRuntimeFixture.Create(prefab.scene, null, "InventoryPolishServices", false);
        var manager = services.AddComponent<RunManager>(); progress = services.AddComponent<PermanentProgress>();
        var store = services.AddComponent<RunRuntimeTraitStore>();
        Singleton(typeof(RunManager), manager); Singleton(typeof(PermanentProgress), progress);
        typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        Set(manager, "currentRun", run);
        Set(panel, "cargoController", services.AddComponent<PlayerCargoController>());
        Set(panel, "playerObject", services);
        var eventObject = AuthoredRuntimeFixture.Create(prefab.scene, null, "InventoryPolishEvent");
        ownedEvent = eventObject.AddComponent<EventSystem>();
        AuthoredRuntimeFixture.SelectEventSystem(ownedEvent, prefab.scene);
        for (Transform t = panel.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        Field<GameObject>("root").SetActive(true);
    }

    [TearDown]
    public void Cleanup()
    {
        Set(panel, "isOpen", false);
        AuthoredRuntimeFixture.ReleaseEventSystem(ref ownedEvent, prefab.scene);
        Object.DestroyImmediate(services);
        PrefabUtility.UnloadPrefabContents(prefab);
        Singleton(typeof(RunManager), oldRun); Singleton(typeof(PermanentProgress), oldProgress);
        typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, oldStore);
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, oldLocalization);
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, oldLanguage);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, oldLoaded);
    }

    private static void Language(string code)
    {
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, code);
    }
    private void Cargo()
    {
        panel.ShowCargoTab(); Set(panel, "isOpen", true); Call(panel, "RefreshCargoManagement");
    }

    [TestCase("ko", "[보유만 보기]", "[전체 보기]")]
    [TestCase("en", "[Owned Only]", "[Show All]")]
    public void FreshCargoShowsThreeZeroRowsAndScrapThenOwnedOnlyShowsOneEmptyState(string language, string all, string owned)
    {
        Language(language);
        Assert.That(Field<bool>("showAllCargoResources"), Is.True);
        Cargo();
        var rows = Field<List<CargoManifestRowUI>>("cargoManifestRows");
        Assert.That(rows.Select(r => r.CurrencyType), Is.EqualTo(PlayerCargoController.SupportedCargoTypes));
        Assert.That(rows.All(r => r.IsVisible), Is.True);
        Assert.That(Field<CurrencyType>("selectedCargoType"), Is.EqualTo(CurrencyType.ScrapParts));
        Assert.That(Field<TMP_Text>("selectedCargoEmptyText").gameObject.activeSelf, Is.False);
        Assert.That(Field<TMP_Text>("cargoShowAllText").text, Is.EqualTo(all));
        Assert.That(Field<GameObject>("cargoDetailActionsRoot").activeSelf, Is.True);
        Assert.That(Field<Button>("cargoJettisonButton").interactable, Is.False);
        Assert.That(Field<Button>("cargoAutoPickupButton").interactable, Is.True);
        Call(panel, "ToggleCargoShowAll");
        Assert.That(rows.All(r => !r.IsVisible), Is.True);
        Assert.That(Field<TMP_Text>("selectedCargoEmptyText").gameObject.activeSelf, Is.True);
        Assert.That(Field<TMP_Text>("cargoShowAllText").text, Is.EqualTo(owned));
        Assert.That(run.CurrentCargoLoad, Is.Zero);
    }

    [Test]
    public void FilterRepairsHiddenSelectionAndKeepsAVisibleSelectionWithoutChangingCargoOrPickup()
    {
        run.Wallet.Add(CurrencyType.CoreShards, 2); Cargo();
        Call(panel, "ToggleCargoShowAll");
        Assert.That(Field<CurrencyType>("selectedCargoType"), Is.EqualTo(CurrencyType.CoreShards));
        Call(panel, "ToggleCargoShowAll");
        Assert.That(Field<CurrencyType>("selectedCargoType"), Is.EqualTo(CurrencyType.CoreShards));
        Assert.That(run.Wallet.PendingCoreShards, Is.EqualTo(2));
        Assert.That(run.Wallet.PendingScrapParts, Is.Zero);
        foreach (CurrencyType type in PlayerCargoController.SupportedCargoTypes)
            Assert.That(Field<PlayerCargoController>("cargoController").IsAutoPickupEnabled(type), Is.True);
        panel.Close();
        Assert.That(Field<bool>("showAllCargoResources"), Is.True);
    }

    [TestCase("ko", "장비", "적재 자원")]
    [TestCase("en", "Equipment", "Cargo Resources")]
    public void TabLabelsAreExactAndSelectionUsesAuthoredImageAndOutline(string language, string eq, string cargo)
    {
        Language(language); panel.ShowEquipmentTab();
        var button = Field<Button>("equipmentTabButton");
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(eq));
        Assert.That(Field<Button>("cargoTabButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo(cargo));
        Assert.That(button.targetGraphic, Is.SameAs(button.GetComponent<Image>()));
        Assert.That(button.GetComponent<Image>().color, Is.EqualTo(Color.white));
        Assert.That(button.GetComponent<Outline>().enabled, Is.True);
        panel.ShowCargoTab(); Assert.That(button.GetComponent<Outline>().enabled, Is.False);
        Assert.That(Field<Button>("cargoTabButton").GetComponent<Outline>().enabled, Is.True);
    }

    [Test]
    public void ZeroRowsRetainReadableTextAndUntintedResourceArt()
    {
        Cargo();
        foreach (var row in Field<List<CargoManifestRowUI>>("cargoManifestRows"))
        {
            var text = Read<TMP_Text>(row, "secondaryText");
            Assert.That(text.color.a, Is.EqualTo(1)); Assert.That(text.color.grayscale, Is.GreaterThan(.6f));
            Color icon = Read<Image>(row, "iconImage").color;
            Assert.That(icon.r, Is.EqualTo(icon.g)); Assert.That(icon.g, Is.EqualTo(icon.b));
            Assert.That(text.text.Split('\n').Length, Is.EqualTo(2));
        }
        Call(panel, "RefreshCargoHeader", 0, 100);
        Assert.That(Field<Slider>("cargoLoadSlider").GetComponent<Image>().color.a, Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void CargoNavigationVisitsEveryEnabledControlAndNeverHiddenControls(bool owned)
    {
        if (owned) run.Wallet.Add(CurrencyType.ScrapParts, 20);
        Cargo();
        Selectable control = Field<Button>("cargoShowAllButton");
        var visited = new HashSet<Selectable>();
        while (control != Field<Button>("cargoTabButton"))
        {
            Assert.That(control, Is.Not.Null); Assert.That(control.IsActive() && control.IsInteractable(), Is.True, control.name);
            Assert.That(visited.Add(control), Is.True, "Navigation cycle before returning to the tab");
            control = control.FindSelectableOnDown();
        }
        Assert.That(visited.Contains(Field<Button>("cargoAutoPickupButton")), Is.True);
        Assert.That(visited.Contains(Field<Button>("cargoJettisonButton")), Is.EqualTo(owned));
        Assert.That(visited.Contains(Field<Slider>("cargoQuantitySlider")), Is.EqualTo(owned));
    }

    [TestCase("ko")] [TestCase("en")]
    public void RecoveryStatesUseCompletedAnalysisAndCloseRestoresCurrentVisibleFocus(string language)
    {
        Language(language); Cargo();
        Set(progress, "acquiredBossStoryParts", new List<BossStoryPart> { BossStoryPart.SectorStabilizer, BossStoryPart.MatterCompressor });
        Set(progress, "highestUnlockedDepth", ExpeditionDepth.DeepZone1);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        panel.OpenStoryProgressInspection();
        var slots = Field<PlayerBuildStatusPanelUI.StoryRecoverySlot[]>("storyRecoverySlots");
        Assert.That(slots.Length, Is.EqualTo(3));
        Assert.That(Read<TMP_Text>(slots[0], "statusText").text, Is.EqualTo(language == "ko" ? "분석 완료" : "Analyzed"));
        Assert.That(Read<TMP_Text>(slots[1], "statusText").text, Is.EqualTo(language == "ko" ? "획득" : "Recovered"));
        Assert.That(Read<TMP_Text>(slots[2], "statusText").text, Is.EqualTo(language == "ko" ? "미획득" : "Not Acquired"));
        foreach (var slot in slots) Assert.That(Read<Image>(slot, "iconImage").sprite, Is.Not.Null);
        Call(panel, "ToggleCargoShowAll"); Call(panel, "ToggleSelectedCargoAutoPickup"); Call(panel, "TryJettisonSelectedCargo");
        Assert.That(Field<bool>("showAllCargoResources"), Is.True);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Field<Button>("storyProgressCloseButton").gameObject));
        panel.CloseStoryProgressInspection();
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.FirstInventorySelectable.gameObject));
        Assert.That(EventSystem.current.currentSelectedGameObject.activeInHierarchy, Is.True);
    }

    [Test]
    public void AuthoredStorageHasFourColumnsAndIndependentKindLevelAndSelectionLabels()
    {
        Assert.That(Field<GridLayoutGroup>("passiveGridLayoutGroup").constraintCount, Is.EqualTo(4));
        var slot = Field<BuildStatusSlotButtonUI>("passiveSlotPrefab");
        Assert.That(Read<GameObject>(slot, "selectedRoot"), Is.Not.Null);
        Assert.That(Read<TMP_Text>(slot, "kindText"), Is.Not.Null);
        Assert.That(Read<TMP_Text>(slot, "amountText").fontSize, Is.GreaterThanOrEqualTo(6.5f));
        Assert.That(Field<TMP_Text>("selectedPassiveEffectText").fontSize, Is.GreaterThanOrEqualTo(7.5f));
        foreach (string field in new[] { "activeNameText", "activeIconImage", "activeEffectText", "activeChargeText", "activeStateText", "activeFieldDropButton" })
            Assert.That(Field<Component>(field), Is.Not.Null, field);
    }

    [Test]
    public void EquipmentHasOneContextualGHintAndMaximumLevelRemainsVisible()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        var trait = catalog.FindById("shared_reinforced_plating");
        run.AddTrait(trait.TraitId);
        for (int i = 0; i < trait.MaxLevel; i++) RunRuntimeTraitStore.Instance.AddOrUpgrade(trait);
        Set(panel, "isOpen", true); panel.RebuildPassiveSection(); Call(panel, "SelectPassive", 0);
        var hints = Field<GameObject>("root").GetComponentsInChildren<TMP_Text>(false)
            .Where(t => t.text != null && t.text.Contains("[G]")).ToArray();
        Assert.That(hints, Is.EqualTo(new[] { Field<TMP_Text>("fieldDropHintText") }));
        var cards = Field<List<BuildStatusSlotButtonUI>>("passiveSlotInstances");
        Assert.That(Read<TMP_Text>(cards[0], "amountText").text, Does.Contain("MAX"));
        Assert.That(Read<TMP_Text>(cards[0], "amountText").text, Does.Contain("Lv."));
        Call(panel, "ClearSelectedPassiveDetail"); Call(panel, "RefreshFieldDropSelectionVisual");
        Assert.That(Field<TMP_Text>("fieldDropHintText").text, Is.Empty);
    }
}
