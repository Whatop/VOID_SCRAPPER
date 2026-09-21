using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The existing Additional Traits suite now verifies the equipment preparation contract.
public sealed class SettlementAdditionalTraitsUIAuthoringTests
{
    private Scene scene;
    private ShipTraitTreePanel panel;
    private SettlementUIController ui;
    private PermanentProgress progress, priorProgress;
    private SaveManager priorSave;
    private RunRuntimeTraitStore priorStore, store;
    private RunManager priorRun;
    private TraitCatalog catalog;
    private List<ShipDefinition> ships;
    private GameObject services;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        priorProgress = PermanentProgress.Instance; priorSave = SaveManager.Instance; priorRun = RunManager.Instance;
        priorStore = (RunRuntimeTraitStore)typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        scene = AuthoredRuntimeFixture.Open("Settlement");
        AuthoredRuntimeFixture.Group(scene);
        panel = AuthoredRuntimeFixture.Single<ShipTraitTreePanel>(scene);
        ui = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        services = AuthoredRuntimeFixture.Create(scene, null, "EquipmentTestServices", false); services.SetActive(false);
        progress = services.AddComponent<PermanentProgress>(); store = services.AddComponent<RunRuntimeTraitStore>();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, null);
        typeof(RunManager).GetProperty("Instance").SetValue(null, null);
        typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, store);
        catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        ships = new[] { "01_basic_ship", "02_shotgun_ship", "03_sniper_ship" }.Select(n =>
            AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/" + n + ".asset")).ToList();
        Set(progress, "equipmentCatalog", catalog); Set(progress, "equipmentShips", ships);
        progress.LoadFromSave(new SaveData());
    }

    [TearDown]
    public void TearDown()
    {
        if (panel != null) Call(panel, "OnDisable");
        AuthoredRuntimeFixture.Close(scene);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, priorProgress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, priorSave);
        typeof(RunManager).GetProperty("Instance").SetValue(null, priorRun);
        typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, priorStore);
    }

    private static SaveData Checkpoint(SaveData source, int index) => (SaveData)typeof(DebugItemGrantUI)
        .GetMethod("CreateCampaignCheckpoint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source, index });

    [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(2, 3)] [TestCase(3, 6)]
    [TestCase(4, 6)] [TestCase(5, 9)] [TestCase(6, 9)] [TestCase(7, 12)]
    [TestCase(8, 12)] [TestCase(9, 12)] [TestCase(10, 12)] [TestCase(11, 12)]
    public void AnalysisControlsRowsAndResearchShips_ForwardAndBackward(int checkpoint, int capacity)
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 10));
        SaveData data = Checkpoint(progress.CreateSaveData(), checkpoint);
        data.buildingLevels.RemoveAll(b => b.buildingType == BuildingType.Hangar);
        data.buildingLevels.Add(new BuildingSaveData(BuildingType.Hangar, 99));
        progress.LoadFromSave(data);
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(capacity));
        Assert.That(ships[0].UnlockedByDefault, Is.True);
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Assert.That(controller.IsShipUnlocked(ships[0]), Is.True);
        Assert.That(controller.IsShipUnlocked(ships[1]), Is.EqualTo(capacity >= 6));
        Assert.That(controller.IsShipUnlocked(ships[2]), Is.EqualTo(capacity >= 9));
        Assert.That(progress.HasUnlockFlag(ships[1].UnlockFlag), Is.EqualTo(capacity >= 6));
        Assert.That(progress.HasUnlockFlag(ships[2].UnlockFlag), Is.EqualTo(capacity >= 9));
        Assert.That(progress.GetBuildingLevel(BuildingType.Hangar), Is.EqualTo(99));
    }

    [TestCase("mg_guidance_control")] [TestCase("mg_stable_feed")]
    public void SweeperCanPrepareInitialEquipmentWithoutGrantingLevels(string id)
    {
        TraitDefinition trait = catalog.FindById(id);
        Assert.That(progress.CanPrepareEquipment(trait), Is.True);
        Assert.That(progress.TryPrepareEquipment(0, trait), Is.True);
        Assert.That(store.GetLevel(id), Is.Zero);
        Assert.That(progress.GetTraitLevel(id), Is.Zero);
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(trait), Is.True);
    }

    [Test]
    public void SlotsAllowEmptyRejectDuplicatesAndValidateMigration()
    {
        TraitDefinition trait = catalog.FindById("mg_stable_feed");
        Assert.That(progress.TryPrepareEquipment(2, trait), Is.True);
        Assert.That(progress.TryPrepareEquipment(0, trait), Is.False);
        Assert.That(progress.TryPrepareEquipment(1, null), Is.True);
        SaveData data = progress.CreateSaveData();
        data.equipmentLoadoutTraitIds = new List<string> { trait.TraitId, trait.TraitId, "stale", trait.TraitId };
        progress.LoadFromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)));
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { trait.TraitId, "", "" }));
        data = progress.CreateSaveData(); data.equipmentLoadoutTraitIds = null;
        progress.LoadFromSave(data);
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { "", "", "" }));
    }

    [Test]
    public void SaveReloadPreservesPartialPoolAndShipSwitchRemovesIncompatibleSelections()
    {
        TraitDefinition shared = catalog.TraitDefinitions.First(t => t != null && t.Category == TraitCategory.Shared && t.CanAppearAsRandomDropTrait);
        progress.TryPrepareEquipment(0, shared);
        progress.TryPrepareEquipment(2, catalog.FindById("mg_guidance_control"));
        SaveData save = progress.CreateSaveData();
        progress.LoadFromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(save.equipmentLoadoutTraitIds));
        progress.SetSelectedShipId(ships[1].ShipId);
        Assert.That(progress.LastSelectedWeaponTree, Is.EqualTo(WeaponTreeType.Shotgun));
        Assert.That(progress.IsEquipmentPrepared(shared.TraitId), Is.True);
        Assert.That(progress.IsEquipmentPrepared("mg_guidance_control"), Is.False);
        Assert.That(progress.TryPrepareEquipment(2, catalog.FindById("mg_stable_feed")), Is.False);
    }

    [Test]
    public void RunPoolAndAcquisitionUsePreparedIdsOnly_StoryAndDebugRemainExplicit()
    {
        TraitDefinition prepared = catalog.FindById("mg_stable_feed");
        TraitDefinition excluded = catalog.FindById("mg_guidance_control");
        progress.TryPrepareEquipment(0, prepared);
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(excluded), Is.False);
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "PlayerFixture", false);
        Assert.That(RunTraitAcquisitionService.TryAcquire(excluded, player, out _, out _), Is.False);
        for (int level = 1; level <= prepared.MaxLevel; level++)
        {
            Assert.That(RunTraitAcquisitionService.TryAcquire(prepared, player, out int previous, out int actual), Is.True);
            Assert.That(previous, Is.EqualTo(level - 1)); Assert.That(actual, Is.EqualTo(level));
        }
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(prepared), Is.False);
        Assert.That(RunTraitAcquisitionService.TryAcquire(prepared, player, out _, out _), Is.False);
        TraitDefinition story = catalog.TraitDefinitions.First(t => t != null && t.IsPersistentStoryTrait);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            $"Persistent story Trait '{story.TraitId}' was acquired in memory, but SaveManager is unavailable.");
        Assert.That(RunTraitAcquisitionService.TryAcquire(story, player, out _, out _), Is.True);
        Assert.That(progress.HasPersistentStoryTrait(story), Is.True);
        Assert.That(store.GetLevel(story.TraitId), Is.Zero, "Story ownership never becomes an ordinary run equipment level.");
        Assert.That(RunTraitAcquisitionService.TryAcquireForDebug(excluded, player, out _, out _), Is.True);
    }

    [Test]
    public void RunSnapshotDoesNotGrantOrFollowLaterSettlementEdits()
    {
        progress.TryPrepareEquipment(0, catalog.FindById("mg_stable_feed"));
        var context = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        progress.TryPrepareEquipment(0, null);
        Assert.That(context.HasPreparedEquipment("mg_stable_feed"), Is.True);
        Assert.That(context.SelectedTraitIds, Is.Empty);
        Assert.That(store.GetLevel("mg_stable_feed"), Is.Zero);
    }

    [Test]
    public void VersionFourSaveMigratesThroughRealSaveManagerAndPreservesOtherProgress()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "void-equipment-test-" + Guid.NewGuid().ToString("N") + ".json");
        var save = services.AddComponent<SaveManager>(); Set(save, "fileName", path); Set(save, "logSavePath", false);
        try
        {
            System.IO.File.WriteAllText(path, "{\"version\":4,\"scrapParts\":73,\"selectedShipId\":\"basic_ship\",\"lastSelectedWeaponTree\":2}");
            progress.LoadFromSave(save.LoadOrCreate());
            Assert.That(progress.ScrapParts, Is.EqualTo(73));
            Assert.That(progress.SelectedShipId, Is.EqualTo(ships[0].ShipId), "Migrate the known basic_ship alias to the authored default ship.");
            Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(3));
            progress.TryPrepareEquipment(1, catalog.FindById("mg_stable_feed"));
            save.Save(progress);
            SaveData reloaded = save.LoadOrCreate();
            Assert.That(reloaded.version, Is.EqualTo(5));
            Assert.That(reloaded.equipmentLoadoutTraitIds, Is.EqualTo(new[] { "", "mg_stable_feed", "" }));
            Assert.That(reloaded.scrapParts, Is.EqualTo(73));
        }
        finally
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (System.IO.File.Exists(path + suffix)) System.IO.File.Delete(path + suffix);
        }
    }

    [Test]
    public void LegacyPermanentEquipmentLevelsCannotPreApplyRunEffects()
    {
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "LegacyTraitStats", false);
        var applier = player.AddComponent<PlayerRuntimeStatApplier>(); Set(applier, "logApplyResult", false);
        var run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        TraitDefinition cargo = catalog.FindById("shared_cargo_bay");
        applier.Apply(run, progress, ships, null, new[] { cargo }, false);
        int baseCapacity = run.MaxCargoCapacity;
        progress.SetTraitLevel(cargo.TraitId, cargo.MaxLevel);
        progress.TryPrepareEquipment(0, cargo);
        applier.Apply(run, progress, ships, null, new[] { cargo }, false);
        Assert.That(run.MaxCargoCapacity, Is.EqualTo(baseCapacity));
        Assert.That(store.GetLevel(cargo.TraitId), Is.Zero);
        Assert.That(progress.GetTraitLevel(cargo.TraitId), Is.EqualTo(cargo.MaxLevel), "Keep legacy save data without applying it as runtime equipment.");
    }

    [Test]
    public void AuthoredGridAndNavigation_RequireNoRuntimeHierarchyConstruction()
    {
        Assert.That(panel.IsEquipmentDevelopment, Is.True);
        var errors = new List<string>(); Assert.That(panel.ValidateEquipmentPresentation(errors), Is.True, string.Join("; ", errors));
        var slots = Get<PreparedEquipmentView[]>(panel, "equipmentSlots");
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 3; col++)
            {
                RectTransform rect = (RectTransform)slots[row * 3 + col].button.transform;
                Assert.That(rect.sizeDelta.x, Is.GreaterThan(40));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(-176 + col * 44));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(43 - row * 35));
            }
        var label = (TMP_Text)new SerializedObject(ui).FindProperty("traitNavigationView.Label").objectReferenceValue;
        Assert.That(label.text, Is.EqualTo("장비 개발"));
        int[] before = panel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
        for (int i = 0; i < 3; i++) { Call(panel, "OnEnable"); panel.RefreshPanel(); Call(panel, "OnDisable"); }
        Assert.That(panel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()), Is.EqualTo(before));
        Assert.That(panel.TryUnlockSelectedTrait(), Is.False, "The legacy permanent-level action is retired.");
    }

    [Test]
    public void DetailsShowEveryLevelAndMaxPayoff_HoverCannotPrepare()
    {
        TraitDefinition trait = catalog.FindById("mg_guidance_control");
        string details = panel.BuildEquipmentDetails(trait);
        Assert.That(details, Does.Contain("Max Lv " + trait.MaxLevel).And.Contain("스위퍼"));
        for (int level = 1; level <= trait.MaxLevel; level++)
            Assert.That(details, Does.Contain("Lv " + level).And.Contain(TraitEffectTextUtility.BuildEffectText(trait, level)));
        panel.RefreshPanel();
        var slot = Get<PreparedEquipmentView[]>(panel, "equipmentSlots")[0];
        Assert.That(slot.button.colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        var candidate = Get<PreparedEquipmentView[]>(panel, "equipmentCandidates").First(v => v.definition == trait);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        candidate.button.OnPointerEnter(new PointerEventData(EventSystem.current));
        candidate.button.OnPointerExit(new PointerEventData(EventSystem.current));
        Assert.That(candidate.button.colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(slot.button.colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
    }

    [Test]
    public void ThirdAnalysisImmediatelyUnlocksTwelve_WithoutAssemblingOrStartingDefense()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 6));
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(9), "An unanalysed third part is insufficient.");
        int changes = 0;
        progress.Changed += () => changes++;
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.True);
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(12));
        Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.ReadyToAssemble));
        Assert.That(progress.SettlementDefenseCleared, Is.False);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.False);
        SaveData saved = progress.CreateSaveData();
        progress.LoadFromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(12));
        progress.LoadFromSave(Checkpoint(saved, 6));
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(9), "Backward checkpoint clears the new analysis flag.");
    }

    [Test]
    public void LegacyAssembledSaveRetainsFinalResearchAndPreparedLastRow()
    {
        SaveData data = Checkpoint(new SaveData(), 7);
        data.unlockFlags.Remove(PermanentProgress.FinalComponentAnalyzedFlag);
        data.equipmentLoadoutTraitIds = Enumerable.Repeat("", 12).ToList();
        data.equipmentLoadoutTraitIds[11] = "mg_guidance_control";
        progress.LoadFromSave(data);
        Assert.That(progress.EquipmentLoadoutCapacity, Is.EqualTo(12));
        Assert.That(progress.EquipmentLoadoutTraitIds[11], Is.EqualTo("mg_guidance_control"));
    }

    private void OpenEquipment()
    {
        for (Transform t = ui.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        for (Transform t = panel.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        Set(ui, "settlementInputRequested", true);
        Set(ui, "dialogueModalActive", false);
        Call(panel, "OnEnable");
        Assert.That(panel.CanUseTraitInput, Is.True);
    }

    [Test]
    public void InspectAndHoverNeverPrepare_ActivationIsFreeAndDoesNotLevelEquipment()
    {
        OpenEquipment();
        TraitDefinition trait = catalog.FindById("mg_guidance_control");
        var view = Get<PreparedEquipmentView[]>(panel, "equipmentCandidates").First(v => v.definition == trait);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        view.button.onClick.Invoke();
        view.button.OnPointerEnter(new PointerEventData(EventSystem.current));
        view.button.OnPointerExit(new PointerEventData(EventSystem.current));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(view.button.colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(view.button.colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(Get<TMP_Text>(panel, "equipmentCandidateState").text, Is.EqualTo("출격 후보군에서 제외됨"));
        var action = Get<Button>(panel, "equipmentActivationButton");
        action.onClick.Invoke();
        Assert.That(progress.IsEquipmentPrepared(trait.TraitId), Is.True);
        Assert.That(store.GetLevel(trait.TraitId), Is.Zero);
        Assert.That(progress.GetTraitLevel(trait.TraitId), Is.Zero);
        Assert.That(Get<TMP_Text>(panel, "equipmentCandidateState").text, Is.EqualTo("출격 후보군에 포함됨"));
        Assert.That(Get<TMP_Text>(panel, "equipmentHint").text, Does.Contain("1 / 3"));
        Assert.That(action.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("비활성화"));
        action.onClick.Invoke();
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { "", "", "" }));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before), "No price, research mutation or permanent level purchase.");
        Assert.That(Get<Button>(ui, "traitActionButton").gameObject.activeSelf, Is.False);
        Assert.That(panel.TryUnlockSelectedTrait(), Is.False);
        Assert.That(AuthoredRuntimeFixture.Single<SettlementController>(scene).TryUnlockOrUpgradeTrait(trait), Is.False);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
    }

    [Test]
    public void LockedEquipmentShowsResearchInsteadOfEffectsAndCannotActivate()
    {
        OpenEquipment();
        TraitDefinition locked = catalog.TraitDefinitions.First(t => t != null && t.CanAppearAsRandomDropTrait &&
            t.Category == TraitCategory.WeaponSpecific && t.WeaponTreeType == WeaponTreeType.Shotgun);
        panel.InspectEquipment(locked);
        Assert.That(panel.IsEquipmentResearchLocked(locked), Is.True);
        Assert.That(Get<TMP_Text>(panel, "equipmentDetails").text, Does.Contain("구획 안정기 분석"));
        Assert.That(Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows").All(r => !r.root.activeSelf), Is.True);
        Assert.That(Get<Button>(panel, "equipmentActivationButton").gameObject.activeSelf, Is.False);
        Assert.That(panel.TryToggleInspectedEquipment(), Is.False);
        Assert.That(panel.PrepareEquipment(locked), Is.False);
        progress.LoadFromSave(Checkpoint(progress.CreateSaveData(), 3));
        panel.RefreshPanel();
        Assert.That(panel.IsEquipmentResearchLocked(locked), Is.False);
        Assert.That(Get<TMP_Text>(panel, "equipmentCandidateState").text, Does.Contain("브리처 기체 선택"));
    }

    [TestCase("mg_guidance_control", "유도 각도", "유도 거리")]
    [TestCase("mg_stable_feed", "탄 퍼짐", "연사력")]
    public void GrowthRowsUseOnlyAuthoredIncrementalEffects(string id, string first, string second)
    {
        OpenEquipment();
        TraitDefinition trait = catalog.FindById(id);
        panel.InspectEquipment(trait);
        var rows = Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows");
        Assert.That(rows.Count(r => r.root.activeSelf), Is.EqualTo(trait.MaxLevel));
        for (int i = 0; i < trait.MaxLevel; i++)
        {
            Assert.That(rows[i].effects.text, Is.EqualTo(TraitEffectTextUtility.BuildEffectText(trait, i + 1)));
            Assert.That(rows[i].effects.text, Does.Contain(first).And.Contain(second).And.Not.Contain("최대 체력"));
            Assert.That(rows[i].heading.text.Contains("MAX"), Is.EqualTo(i + 1 == trait.MaxLevel));
        }
        Assert.That(Get<TMP_Text>(panel, "equipmentMaxLevel").text, Does.Contain("Lv.0").And.Contain("최대 Lv." + trait.MaxLevel));
        Assert.That(Get<TMP_Text>(panel, "equipmentGrowthHeading").text, Does.Contain("획득 시 추가"));
        Assert.That(Get<TMP_Text>(panel, "equipmentCompatibility").rectTransform.rect.height, Is.LessThanOrEqualTo(12));
        Assert.That(Get<TMP_Text>(panel, "equipmentDetails").text, Is.EqualTo(trait.Description));
    }

    [Test]
    public void SpecialMechanicUsesSharedFormatterAndOnlyItsRealMaxLevelRows()
    {
        OpenEquipment();
        TraitDefinition trait = catalog.TraitDefinitions.First(t => t != null && t.CanAppearAsRandomDropTrait &&
            t.LevelEffects.Any(e => e != null && e.EffectType == TraitEffectType.PeriodicReflectiveShield));
        panel.InspectEquipment(trait);
        var rows = Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows");
        Assert.That(rows.Count(r => r.root.activeSelf), Is.EqualTo(trait.MaxLevel));
        Assert.That(string.Join("\n", rows.Where(r => r.root.activeSelf).Select(r => r.effects.text)), Does.Contain("반사 방벽 재충전"));
        Assert.That(panel.TryToggleInspectedEquipment(), Is.True);
        Assert.That(store.GetLevel(trait.TraitId), Is.Zero);
    }

    [Test]
    public void FullCapacityNeverReplacesAnotherSelection_ActiveRunKeepsItsSnapshot()
    {
        OpenEquipment();
        foreach (string id in new[] { "mg_guidance_control", "mg_stable_feed", "shared_cargo_bay" })
        {
            panel.InspectEquipment(catalog.FindById(id));
            Assert.That(panel.TryToggleInspectedEquipment(), Is.True);
        }
        var snapshot = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        var extra = catalog.TraitDefinitions.First(t => progress.CanPrepareEquipment(t) && !progress.IsEquipmentPrepared(t.TraitId));
        panel.InspectEquipment(extra);
        Assert.That(panel.TryToggleInspectedEquipment(), Is.False);
        Assert.That(Get<Button>(panel, "equipmentActivationButton").interactable, Is.False);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        panel.InspectEquipment(catalog.FindById("mg_guidance_control"));
        Assert.That(panel.TryToggleInspectedEquipment(), Is.True);
        Assert.That(snapshot.HasPreparedEquipment("mg_guidance_control"), Is.True);
        Assert.That(progress.IsEquipmentPrepared("mg_guidance_control"), Is.False);
    }

    [Test]
    public void ActivationReservesAuthoredFooterSpaceAndInspectionResetsScroll()
    {
        OpenEquipment();
        RectTransform action = Get<Button>(panel, "equipmentActivationButton").GetComponent<RectTransform>();
        RectTransform root = Get<GameObject>(panel, "equipmentDevelopmentRoot").GetComponent<RectTransform>();
        Assert.That(action.parent.parent, Is.EqualTo(root));
        // PreviewScene canvases have no rendered world bounds. Validate the authored footer reserve;
        // actual 480x270 separation is verified by the rendered Play Mode smoke.
        float bottom = action.anchoredPosition.y - action.sizeDelta.y * .5f;
        Assert.That(bottom - (-root.sizeDelta.y * .5f), Is.GreaterThanOrEqualTo(20f));
        var scroll = Get<ScrollRect>(panel, "equipmentGrowthScroll");
        scroll.content.anchoredPosition = new Vector2(0, 90);
        panel.InspectEquipment(catalog.FindById("mg_guidance_control"));
        Assert.That(scroll.content.anchoredPosition, Is.EqualTo(Vector2.zero));
    }

    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
