using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SettlementRestorationUIAuthoringTests
{
    private Scene scene, userScene;
    private bool userDirty;
    private int[] userRoots;
    private RectTransform fixture, panel;
    private SettlementHUD hud;
    private SettlementUIController ui;
    private SettlementController controller;
    private PermanentProgress progress, previousProgress;
    private SaveManager previousSave;
    private EventSystem events;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userDirty = userScene.isDirty;
        userRoots = Roots(userScene);
        previousProgress = PermanentProgress.Instance;
        previousSave = SaveManager.Instance;
        scene = AuthoredRuntimeFixture.Open("Settlement");
        fixture = AuthoredRuntimeFixture.Group(scene);
        hud = AuthoredRuntimeFixture.Single<SettlementHUD>(scene);
        ui = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        progress = Create(fixture, "TestOwnedProgress").gameObject.AddComponent<PermanentProgress>();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, null);
        events = AuthoredRuntimeFixture.Single<EventSystem>(scene);
        AuthoredRuntimeFixture.SelectEventSystem(events, scene);
        panel = (RectTransform)Read<GameObject>(ui, "repairPanel").transform;
        panel.gameObject.SetActive(true);
        Call(controller, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (ui != null) { Call(ui, "UnsubscribeController"); Call(ui, "UnsubscribeButtons"); Call(ui, "UnsubscribeDialogueLifecycle"); }
            if (hud != null) Call(hud, "OnDisable");
            if (controller != null) Call(controller, "OnDisable");
        }
        finally
        {
            typeof(PermanentProgress).GetProperty("Instance").SetValue(null, previousProgress);
            typeof(SaveManager).GetProperty("Instance").SetValue(null, previousSave);
            try { AuthoredRuntimeFixture.ReleaseEventSystem(ref events, scene); }
            finally
            {
                try { if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene); }
                finally { scene = default; }
            }
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(Roots(userScene), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    [Test]
    public void RefreshAndSelection_UpdateCorrectRolesWithoutLayoutOrBaselineDrift()
    {
        UseAuthoredFixture();
        Image preview = Read<Image>(hud, "repairPreviewImage");
        preview.preserveAspect = false;
        TextMeshProUGUI title = Read<TextMeshProUGUI>(hud, "repairTitleText");
        title.fontSize = 8.3f;
        title.enableAutoSizing = false;
        string rect = EditorJsonUtility.ToJson(title.rectTransform);
        Image indicator = Read<Image>(hud, "repairIndicatorImages.Array.data[0]");
        indicator.transform.localScale = new Vector3(.6f, .8f, 1);
        indicator.color = new Color(.7f, .6f, .5f, .4f);
        Vector3 baseline = indicator.transform.localScale;
        Color tint = indicator.color;
        int[] objects = Ids();
        Call(hud, "Awake");
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < 4; i++)
            {
                ui.SelectBuilding((BuildingType)i);
                Assert.That(title.text, Is.EqualTo(controller.BuildRestorationViewData((BuildingType)i).Title));
                var serialized = new SerializedObject(hud);
                Sprite expected = serialized.FindProperty($"buildingPreviewSprites.Array.data[{i}].levelSprites.Array.data[0]").objectReferenceValue as Sprite;
                Assert.That(preview.sprite, Is.SameAs(expected));
            }
            ui.SelectBuilding(BuildingType.Hangar);
            Assert.That(indicator.transform.localScale, Is.EqualTo(baseline * 1.25f));
            Assert.That(indicator.color, Is.EqualTo(tint));
        }
        Assert.That(title.fontSize, Is.EqualTo(8.3f));
        Assert.That(preview.preserveAspect, Is.False);
        Assert.That(EditorJsonUtility.ToJson(title.rectTransform), Is.EqualTo(rect));
        Assert.That(Ids(), Is.EqualTo(objects));
        Assert.That(Read<Button>(ui, "repairBackButton").gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ReenableRefresh_ActualPublisherCleanup_AndOnePersistentActionPerClick()
    {
        UseAuthoredFixture();
        Button next = Read<Button>(ui, "repairNextButton");
        UnityEventTools.AddPersistentListener(next.onClick, ui.MoveBuildingNext);
        // RuntimeOnly persistent calls are intentionally skipped by Unity in EditMode.
        // Enable only this disposable fixture's callback for the actual click dispatch.
        next.onClick.SetPersistentListenerState(next.onClick.GetPersistentEventCount() - 1,
            UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        for (int i = 0; i < 3; i++) { Call(ui, "SubscribeButtons"); Call(ui, "SubscribeController"); }
        ui.SelectBuilding(BuildingType.Hangar);
        next.onClick.Invoke();
        Assert.That(Get<BuildingType>(ui, "selectedBuilding"), Is.EqualTo(BuildingType.EngineWorkshop),
            $"Semantic Next dispatch via {AuthoredRuntimeFixture.Describe(next.gameObject)} on {AuthoredRuntimeFixture.Describe(ui.gameObject)}");
        Assert.That(Read<TextMeshProUGUI>(hud, "repairTitleText").text, Is.EqualTo(controller.BuildRestorationViewData(BuildingType.EngineWorkshop).Title));
        Assert.That(Subscribers(controller), Is.EqualTo(1));
        Call(ui, "OnDisable");
        Assert.That(Subscribers(controller), Is.Zero);
        progress.SetBuildingLevel(BuildingType.EngineWorkshop, 1);
        Call(ui, "OnEnable"); // Existing lifecycle refresh; no Awake, panel builder, Start or saves.
        Assert.That(Read<TextMeshProUGUI>(hud, "repairCurrentEffectText").text, Is.EqualTo(controller.BuildRestorationViewData(BuildingType.EngineWorkshop).StateText));
        Assert.That(Subscribers(controller), Is.EqualTo(1));
        Bind(ui, "settlementController", Create(fixture, "OtherPublisher").gameObject.AddComponent<SettlementController>());
        Call(ui, "OnDisable");
        Assert.That(Subscribers(controller), Is.Zero, "Detach from the subscribed publisher, not a newly assigned field.");
    }

    [Test]
    public void MissingBinding_FailsLocally_NoReplacementOrRestorationTransaction()
    {
        UseAuthoredFixture();
        Bind(hud, "repairCurrentEffectText", null);
        int[] before = Ids();
        string progressBefore = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(Call(ui, "TryExecuteSelectedRestorationAction"), Is.False);
        ui.SelectBuilding(BuildingType.Hangar);
        ui.Refresh();
        Assert.That(Read<Button>(ui, "repairActionButton").interactable, Is.False);
        Assert.That(Ids(), Is.EqualTo(before));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(progressBefore));
        Assert.That(Get<bool>(ui, "missingRestorationBindingReported"), Is.True);
    }

    [Test]
    public void RestorationAuthority_EligibilityAndRepeatedConfirmationStayIdempotent()
    {
        UseAuthoredFixture();
        Assert.That(controller.TryCompleteRestorationProject(BuildingType.Hangar), Is.False);
        BuildingDefinition definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        try
        {
            // Test-owned definition defaults have no prerequisites; no production asset edits.
            var data = new SerializedObject(controller);
            SerializedProperty list = data.FindProperty("buildingDefinitions");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();
            int completions = 0;
            controller.RestorationCompleted += _ => completions++;
            ui.SelectBuilding(BuildingType.Hangar);
            Assert.That(progress.GetBuildingLevel(BuildingType.Hangar), Is.Zero, "Selection must not restore.");
            Assert.That(Call(ui, "TryExecuteSelectedRestorationAction"), Is.True);
            Assert.That(Call(ui, "TryExecuteSelectedRestorationAction"), Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(progress.GetBuildingLevel(BuildingType.Hangar), Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void AccessKeyReadySelection_ExplicitSaveHandsOffImmediatelyWithoutActivationOrTravel()
    {
        UseAuthoredFixture();
        Assert.That(progress.TryStartDamagedAccessKeyQuest(), Is.True);
        progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
        progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
        Assert.That(controller.CanRestoreDamagedAccessKey(), Is.True);
        ui.SelectBuilding(BuildingType.RecoveryProcessor);
        ui.Refresh();
        Assert.That(progress.DamagedAccessKeyQuestState, Is.EqualTo(MainDamagedAccessKeyQuestState.ReadyToRestore));
        Assert.That(Read<TextMeshProUGUI>(hud, "repairTitleText").text, Is.EqualTo(controller.BuildDamagedAccessKeyRestorationViewData().Title));
        string testPath = Path.Combine(Path.GetTempPath(), "VOID_Restoration_" + Guid.NewGuid().ToString("N") + ".json");
        SaveManager testSave = Create(fixture, "TestOwnedSaveManager").gameObject.AddComponent<SaveManager>();
        try
        {
            // Supported first-party configuration. Absolute test-owned filename never resolves to the player save.
            var data = new SerializedObject(testSave);
            data.FindProperty("fileName").stringValue = testPath;
            data.FindProperty("logSavePath").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            typeof(SaveManager).GetProperty("Instance").SetValue(null, testSave);
            UseAuthoredFixture();
            Call(ui, "SubscribeController");
            ui.Refresh();
            Assert.That(File.Exists(testPath), Is.False, "Authoring and refresh must not save or confirm recovery.");
            Assert.That(Call(ui, "TryExecuteSelectedRestorationAction"), Is.True);
            // No manual refresh: the existing progression/controller events must hand off now.
            Assert.That(ui.IsRouteCorePresentation, Is.True);
            Assert.That(Read<TextMeshProUGUI>(hud, "repairTitleText").text, Is.EqualTo(Local("ui.settlement.nav.route_core")));
            Assert.That(Read<Button>(ui, "routeCoreDeckButton").gameObject.activeSelf, Is.True);
            Assert.That(Read<Image>(hud, "repairPreviewImage").gameObject.activeSelf, Is.False);
            Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.Assembled));
            var encounter = AuthoredRuntimeFixture.Single<SettlementDefenseEncounterController>(scene);
            Assert.That(Get<bool>(encounter, "deckOpen"), Is.False);
            Assert.That(encounter.IsActive, Is.False);
            Assert.That(AuthoredRuntimeFixture.Single<SettlementRouteCoreController>(scene).IsDefenseRequested, Is.False);
            Assert.That(File.Exists(testPath), Is.True);
            Assert.That(Call(ui, "TryExecuteSelectedRestorationAction"), Is.False);
            Assert.That(File.Exists(testPath + ".bak"), Is.False, "Repeated confirmation must not save a second time.");
            ui.Refresh();
            Assert.That(Read<TextMeshProUGUI>(hud, "repairCurrentEffectText").text, Is.EqualTo(Local("ui.settlement.route_core.status.assembled")));
            Assert.That(Read<Button>(ui, "repairActionButton").interactable, Is.False);
        }
        finally
        {
            typeof(SaveManager).GetProperty("Instance").SetValue(null, null);
            Object.DestroyImmediate(testSave.gameObject);
            foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" })
                if (File.Exists(testPath + suffix)) File.Delete(testPath + suffix);
        }
    }

    [TestCase(0, false, "assembled")]
    [TestCase(1, false, "assembled")]
    [TestCase(2, false, "assembled")]
    [TestCase(3, true, "assembled")]
    [TestCase(4, true, "activated")]
    [TestCase(5, true, "defense_complete")]
    [TestCase(6, true, "campaign_complete")]
    public void RecoveryHub_ProjectsExistingAuthorityAndRefreshesWithoutProgressMutation(int stage, bool coreMode, string state)
    {
        Call(ui, "InitializeNavigationPresentation");
        Call(ui, "SubscribeController");
        ui.ShowRepairPanel();
        AdvanceCampaign(stage);
        Assert.That(ui.IsRouteCoreHubAvailable, Is.EqualTo(coreMode));
        Assert.That(ui.IsRouteCorePresentation, Is.EqualTo(coreMode));
        Assert.That(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label").text,
            Is.EqualTo(Local(coreMode ? "ui.settlement.nav.route_core" : "ui.settlement.nav.recovery")));
        Assert.That(Read<Button>(ui, "routeCoreDeckButton").gameObject.activeSelf, Is.EqualTo(coreMode));
        Assert.That(Read<Button>(ui, "repairActionButton").gameObject.activeSelf, Is.EqualTo(!coreMode));
        Assert.That(Read<Button>(ui, "facilityManagementButton").gameObject.activeSelf, Is.EqualTo(coreMode));
        if (coreMode)
        {
            Assert.That(Read<TextMeshProUGUI>(hud, "repairCurrentEffectText").text,
                Is.EqualTo(Local("ui.settlement.route_core.status." + state)));
            string components = Read<TextMeshProUGUI>(hud, "routeCoreComponentsText").text;
            foreach (string name in new[] { "sector_stabilizer", "matter_compressor", "phase_navigation_lens" })
                Assert.That(components, Does.Contain(Local("ui.story_recovery." + name)));
            Assert.That(Read<TextMeshProUGUI>(ui, "routeCoreDeckButtonLabel").text,
                Is.EqualTo(Local("ui.settlement.route_core.action.enter_deck")));
        }
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        int[] objects = Ids();
        for (int i = 0; i < 3; i++) ui.Refresh();
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(Ids(), Is.EqualTo(objects));
        Assert.That(Get<bool>(AuthoredRuntimeFixture.Single<SettlementDefenseEncounterController>(scene), "deckOpen"), Is.False);
    }

    [Test]
    public void RecoveryHub_OptionalFacilitiesRemainReachableAndCannotGateTheHandoff()
    {
        Call(ui, "SubscribeController");
        ui.ShowRepairPanel();
        foreach (BuildingType building in Enum.GetValues(typeof(BuildingType))) progress.SetBuildingLevel(building, 10);
        Assert.That(ui.IsRouteCoreHubAvailable, Is.False, "Optional max levels cannot replace story restoration.");
        foreach (BuildingType building in Enum.GetValues(typeof(BuildingType))) progress.SetBuildingLevel(building, 0);
        AdvanceCampaign(3);
        Assert.That(ui.IsRouteCorePresentation, Is.True, "Zero optional building levels cannot block the core.");
        string state = JsonUtility.ToJson(progress.CreateSaveData());
        Button secondary = Read<Button>(ui, "facilityManagementButton");
        Assert.That(secondary.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(secondary.onClick.GetPersistentTarget(0), Is.SameAs(ui));
        Assert.That(secondary.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(SettlementUIController.ToggleFacilityManagement)));
        secondary.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        secondary.onClick.Invoke();
        Assert.That(ui.IsRouteCorePresentation, Is.False);
        Assert.That(Read<Image>(hud, "repairPreviewImage").gameObject.activeSelf, Is.True);
        foreach (BuildingType building in Enum.GetValues(typeof(BuildingType)))
        {
            ui.SelectBuilding(building);
            Assert.That(Read<TextMeshProUGUI>(hud, "repairTitleText").text, Is.EqualTo(controller.BuildRestorationViewData(building).Title));
        }
        Assert.That(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label").text, Is.EqualTo(Local("ui.settlement.nav.route_core")));
        secondary.onClick.Invoke();
        Assert.That(ui.IsRouteCorePresentation, Is.True);
        ui.ToggleFacilityManagement();
        ui.ShowMainPanel();
        ui.ShowRepairPanel();
        Assert.That(ui.IsRouteCorePresentation, Is.True, "Reentering the primary slot returns to the story-facing hub.");
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(state));
    }

    [Test]
    public void RecoveryHub_OnePrimarySlotRetainsBlueHoverAndYellowSelection()
    {
        fixture.gameObject.SetActive(true);
        Call(ui, "InitializeNavigationPresentation");
        Call(ui, "SubscribeController");
        ui.ShowMainPanel();
        Button primary = Read<Button>(ui, "openRepairPanelButton");
        Assert.That(Get<Button[]>(ui, "primaryNavigationButtons").Length, Is.EqualTo(6));
        Assert.That(Get<Button[]>(ui, "primaryNavigationButtons"), Has.No.Member(Read<Button>(ui, "routeCoreDeckButton")));
        Assert.That(Transforms().Count(t => t.name == "RouteCoreDeckButton"), Is.EqualTo(1));
        Assert.That(Read<Button>(ui, "routeCoreDeckButton").transform.IsChildOf(panel), Is.True);
        var pointer = primary.GetComponent<SettlementPrimaryNavigationPointer>();
        pointer.OnPointerEnter(new PointerEventData(events));
        Assert.That(Read<Image>(ui, "repairNavigationView.Background").color, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        pointer.OnPointerExit(new PointerEventData(events));
        ui.ShowRepairPanel();
        AdvanceCampaign(3);
        pointer.OnPointerEnter(new PointerEventData(events));
        pointer.OnPointerExit(new PointerEventData(events));
        Assert.That(Read<Image>(ui, "repairNavigationView.Background").color, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(Read<Image>(ui, "repairNavigationView.ActiveStrip").enabled, Is.True);
        Assert.That(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label").text, Is.EqualTo(Local("ui.settlement.nav.route_core")));
    }

    [Test]
    public void RecoveryHub_DeckAndReturnUseExistingPhysicalAuthorityBindings()
    {
        var encounter = AuthoredRuntimeFixture.Single<SettlementDefenseEncounterController>(scene);
        var core = AuthoredRuntimeFixture.Single<SettlementRouteCoreController>(scene);
        Button enter = Read<Button>(ui, "routeCoreDeckButton");
        Button leave = Read<GameObject>(encounter, "facilityNavigation").GetComponent<Button>();
        Assert.That(enter.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(enter.onClick.GetPersistentTarget(0), Is.SameAs(encounter));
        Assert.That(enter.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(SettlementDefenseEncounterController.EnterDeck)));
        Assert.That(leave.name, Is.EqualTo("ReturnToFacilities"));
        Assert.That(leave.onClick.GetPersistentTarget(0), Is.SameAs(encounter));
        Assert.That(leave.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(SettlementDefenseEncounterController.LeaveDeck)));
        Assert.That(Read<SettlementRouteCoreController>(encounter, "routeCore"), Is.SameAs(core));
        Assert.That(core.DefenseRequested.GetPersistentTarget(0), Is.SameAs(encounter));
        Assert.That(core.DefenseRequested.GetPersistentMethodName(0), Is.EqualTo(nameof(SettlementDefenseEncounterController.BeginEncounter)));
        Assert.That(Read<PlayerHealth>(encounter, "player"), Is.Not.Null);
        Assert.That(Read<Camera>(encounter, "deckCamera"), Is.Not.Null);
    }

    [TestCase("ko")]
    [TestCase("en")]
    public void RecoveryHub_LocalizedContentFitsAuthoredRectangles(string language)
    {
        void Fits(TMP_Text label, string key)
        {
            string text = Local(key, language);
            Vector2 size = label.GetPreferredValues(text, label.rectTransform.rect.width, float.PositiveInfinity);
            Assert.That(size.y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), key + " " + language);
        }
        foreach (string state in new[] { "assembled", "activated", "defense_complete", "campaign_complete" })
        {
            Fits(Read<TextMeshProUGUI>(hud, "repairCurrentEffectText"), "ui.settlement.route_core.status." + state);
            Fits(Read<TextMeshProUGUI>(hud, "repairDescriptionText"), "ui.settlement.route_core.description." + state);
        }
        Fits(Read<TextMeshProUGUI>(ui, "routeCoreDeckButtonLabel"), "ui.settlement.route_core.action.enter_deck");
        Fits(Read<TextMeshProUGUI>(ui, "facilityManagementButtonLabel"), "ui.settlement.route_core.action.manage_facilities");
        Fits(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label"), "ui.settlement.nav.route_core");
        Fits(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label"), "ui.settlement.nav.recovery");
        TMP_Text components = Read<TextMeshProUGUI>(hud, "routeCoreComponentsText");
        string content = string.Join("\n\n", new[] { "sector_stabilizer", "matter_compressor", "phase_navigation_lens" }
            .Select(name => Local("ui.story_recovery." + name, language) + " · " + Local("ui.settlement.route_core.components.restored", language)));
        Assert.That(components.GetPreferredValues(content, components.rectTransform.rect.width, float.PositiveInfinity).y,
            Is.LessThanOrEqualTo(components.rectTransform.rect.height + .1f), language);
    }

    [TestCase(3)]
    [TestCase(5)]
    [TestCase(6)]
    public void RecoveryHub_EnterAndReturnReusePlayerCameraWithoutActivationDefenseOrLaunch(int stage)
    {
        AdvanceCampaign(stage);
        ui.ShowRepairPanel();
        var encounter = AuthoredRuntimeFixture.Single<SettlementDefenseEncounterController>(scene);
        var core = AuthoredRuntimeFixture.Single<SettlementRouteCoreController>(scene);
        Button enter = Read<Button>(ui, "routeCoreDeckButton");
        Button leave = Read<GameObject>(encounter, "facilityNavigation").GetComponent<Button>();
        GameObject management = Read<GameObject>(encounter, "managementCanvas");
        GameObject deck = Read<GameObject>(encounter, "deckRoot");
        Camera camera = Read<Camera>(encounter, "deckCamera");
        PlayerHealth player = Read<PlayerHealth>(encounter, "player");
        int[] players = AuthoredRuntimeFixture.Find<PlayerHealth>(scene).Select(p => p.GetInstanceID()).ToArray();
        int[] cameras = AuthoredRuntimeFixture.Find<Camera>(scene).Select(c => c.GetInstanceID()).ToArray();
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        float size = camera.orthographicSize;
        bool orthographic = camera.orthographic;
        RunManager oldRun = RunManager.Instance;
        SceneFlowManager oldFlow = SceneFlowManager.Instance;
        GameStateManager oldState = GameStateManager.Instance;
        try
        {
            typeof(RunManager).GetProperty("Instance").SetValue(null, null);
            typeof(SceneFlowManager).GetProperty("Instance").SetValue(null, null);
            typeof(GameStateManager).GetProperty("Instance").SetValue(null, null);
            Assert.That(GameplayPauseManager.IsPaused, Is.False, "EditMode fixture requires no active gameplay pause.");
            management.SetActive(true);
            enter.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            leave.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            enter.onClick.Invoke();
            Assert.That(Get<bool>(encounter, "deckOpen"), Is.True);
            Assert.That(deck.activeSelf, Is.True);
            Assert.That(management.activeSelf, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(Read<Transform>(encounter, "playerStart").position));
            Assert.That(camera.orthographicSize, Is.EqualTo(270f / (32f * 2f)));
            Assert.That(encounter.IsActive, Is.False);
            Assert.That(core.IsDefenseRequested, Is.False);
            enter.onClick.Invoke(); // Existing repeated-entry guard must retain the original camera baseline.
            leave.onClick.Invoke();
            Assert.That(Get<bool>(encounter, "deckOpen"), Is.False);
            Assert.That(deck.activeSelf, Is.False);
            Assert.That(management.activeSelf, Is.True);
            Assert.That(camera.orthographic, Is.EqualTo(orthographic));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
            ui.Refresh();
            Assert.That(Read<TextMeshProUGUI>(ui, "repairNavigationView.Label").text, Is.EqualTo(Local("ui.settlement.nav.route_core")));
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
            Assert.That(AuthoredRuntimeFixture.Find<PlayerHealth>(scene).Select(p => p.GetInstanceID()).ToArray(), Is.EqualTo(players));
            Assert.That(AuthoredRuntimeFixture.Find<Camera>(scene).Select(c => c.GetInstanceID()).ToArray(), Is.EqualTo(cameras));
            Assert.That(RunManager.Instance, Is.Null, "Deck navigation cannot create a run owner or launch FinalNetwork.");
        }
        finally
        {
            Call(encounter, "OnDisable");
            typeof(RunManager).GetProperty("Instance").SetValue(null, oldRun);
            typeof(SceneFlowManager).GetProperty("Instance").SetValue(null, oldFlow);
            typeof(GameStateManager).GetProperty("Instance").SetValue(null, oldState);
        }
    }

    private void AdvanceCampaign(int stage)
    {
        progress.TryStartDamagedAccessKeyQuest();
        if (stage >= 1) progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        if (stage >= 2)
        {
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
        }
        if (stage >= 3) Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True);
        if (stage >= 4) Assert.That(progress.TryActivateRouteCore(), Is.True);
        if (stage >= 5) progress.MarkSettlementDefenseCleared();
        if (stage >= 6) progress.RegisterCampaignBossDefeat(CampaignBossId.NullDispatcher);
    }

    private string Local(string key, string language = null)
    {
        LocalizationCatalog catalog = Read<LocalizationCatalog>(ui, "recoveryLocalizationCatalog");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGetText(key, language ?? GameSettingsRuntime.LanguageCode, out string text, out _), Is.True, key);
        return text;
    }

    [Test]
    public void UnavailableAction_HandsFocusToFacilityNavigation_WithoutMovingBehindModal()
    {
        UseAuthoredFixture();
        Call(ui, "OnEnable");
        ui.SelectBuilding(BuildingType.Hangar);
        Button action = Read<Button>(ui, "repairActionButton");
        EventSystem.current.SetSelectedGameObject(action.gameObject);
        ui.Refresh();
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Read<Button>(ui, "repairNextButton").gameObject));
        Assert.That(Read<Button>(ui, "repairNextButton").navigation.selectOnLeft, Is.SameAs(Read<Button>(ui, "repairPreviousButton")));
        ui.SetDialogueModalActiveForEditorAndTests(true);
        GameObject focus = EventSystem.current.currentSelectedGameObject;
        ui.Refresh();
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(focus));
        ui.SetDialogueModalActiveForEditorAndTests(false);
    }

    private void UseAuthoredFixture()
    {
        Assert.That(scene.IsValid(), Is.True);
        Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene), Is.True);
    }    private RectTransform Create(Transform parent, string name) => (RectTransform)AuthoredRuntimeFixture.Create(scene, parent, name, true).transform;
    private Transform[] Transforms() => fixture.GetComponentsInChildren<Transform>(true);
    private int[] Ids() => Transforms().Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();
    private static int[] Roots(Scene scene) => scene.GetRootGameObjects().Select(g => g.GetInstanceID()).OrderBy(i => i).ToArray();
    private static T Read<T>(Object owner, string field) where T : Object => new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
    private static void Bind(Object owner, string field, Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static object Call(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
    private static int Subscribers(SettlementController publisher) => Get<Delegate>(publisher, "Changed")?.GetInvocationList().Length ?? 0;
}
