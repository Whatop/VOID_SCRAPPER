using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SettlementShipReinforcementUIAuthoringTests
{
    private Scene scene, userScene;
    private bool userDirty;
    private int[] userRoots;
    private GameObject fixture;
    private SettlementUIController navigation;
    private SettlementSectorTechnologyPanelUI owner;
    private SettlementController controller;
    private PermanentProgress progress, previousProgress;
    private SaveManager previousSave;
    private EventSystem events;
    private RectTransform hud, repair;
    private Button sidebar, action;
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
        fixture = AuthoredRuntimeFixture.Group(scene).gameObject;
        hud = (RectTransform)AuthoredRuntimeFixture.Single<SettlementHUD>(scene).transform;
        navigation = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        owner = AuthoredRuntimeFixture.Single<SettlementSectorTechnologyPanelUI>(scene);
        controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        progress = Create(fixture.transform, "TestOwnedProgress").gameObject.AddComponent<PermanentProgress>();
        events = AuthoredRuntimeFixture.Single<EventSystem>(scene); AuthoredRuntimeFixture.SelectEventSystem(events, scene);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, null);
        repair = (RectTransform)Read<GameObject>(navigation, "repairPanel").transform;
        sidebar = Read<Button>(navigation, "sectorTechnologyNavigationButton");
        action = Read<Button>(navigation, "repairActionButton");
        typeof(SettlementUIController).GetField("primaryNavigationButtons", Private).SetValue(navigation,
            AuthoredRuntimeFixture.NavigationFields.Select(f => Read<Button>(navigation, f)).ToArray());
        typeof(SettlementUIController).GetField("currentPanel", Private).SetValue(navigation, SettlementPanelKind.SectorTechnology);
        Call(controller, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (owner != null) Call(owner, "OnDisable");
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
    public void AuthoredPopulation_OneCardPerStableId_NoStaticReplacementOrStyleReset()
    {
        UseAuthoredFixture();
        Template().CardImage.color = new Color(.3f, .4f, .5f, .6f);
        Template().NameText.fontSize = 8.25f;
        Template().NameText.enableAutoSizing = false;
        string rootBefore = EditorJsonUtility.ToJson(Root());
        int[] staticObjects = Root().GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
        Initialize();
        owner.Show();
        Assert.That(Catalog().childCount, Is.EqualTo(SectorTechnologyCatalog.Definitions.Count + 1));
        Assert.That(Template().gameObject.activeSelf, Is.False);
        Assert.That(EditorJsonUtility.ToJson(Root()), Is.EqualTo(rootBefore));
        foreach (int id in staticObjects) Assert.That(Root().GetComponentsInChildren<Transform>(true).Any(t => t.GetInstanceID() == id), Is.True);
        for (int i = 0; i < SectorTechnologyCatalog.Definitions.Count; i++)
        {
            var card = Card(i);
            Assert.That(card.name, Is.EqualTo(SectorTechnologyCatalog.Definitions[i].Id));
            Assert.That(card.NameText.text, Is.EqualTo(SectorTechnologyCatalog.Definitions[i].DisplayName));
            Assert.That(card.NameText.fontSize, Is.EqualTo(8.25f));
            Assert.That(card.CardButton.targetGraphic, Is.SameAs(card.CardImage));
            Assert.That(card.GetComponents<SettlementSectorTechnologyEntrySelection>().Length, Is.EqualTo(1));
        }
        Card(1).CardButton.onClick.Invoke();
        Assert.That(Read<TextMeshProUGUI>(owner, "selectedNameText").text, Is.EqualTo(SectorTechnologyCatalog.Definitions[1].DisplayName));
        Assert.That(Card(0).CardImage.color, Is.EqualTo(SettlementSelectionColors.HoverBackground),
            "Focus remains a blue preview when a test invokes another card's click directly.");
        int[] populated = Objects();
        for (int i = 0; i < 3; i++) { owner.Hide(false); owner.Show(); Initialize(); owner.Refresh(); }
        Assert.That(Objects(), Is.EqualTo(populated));
    }

    [Test]
    public void RefreshAndReenable_BalancedPublisherSubscriptionAndCurrentAmounts()
    {
        UseAuthoredFixture();
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 2);
        Initialize();
        owner.Show();
        Assert.That(Upgrade().interactable, Is.True);
        int count = SubscriberCount(controller);
        int[] objects = Objects();
        Call(owner, "OnEnable");
        Call(owner, "OnEnable");
        Assert.That(SubscriberCount(controller), Is.EqualTo(count));
        Call(owner, "OnDisable");
        Assert.That(SubscriberCount(controller), Is.EqualTo(count - 1));
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 3);
        Call(owner, "OnEnable");
        StringAssert.Contains("5", Read<TextMeshProUGUI>(owner, "selectedCostText").text);
        Assert.That(SubscriberCount(controller), Is.EqualTo(count));
        Assert.That(Objects(), Is.EqualTo(objects));
        SettlementController replacement = Create(fixture.transform, "ReplacementPublisher").gameObject.AddComponent<SettlementController>();
        Bind(owner, "settlementController", replacement);
        Call(owner, "OnDisable");
        Assert.That(SubscriberCount(controller), Is.EqualTo(count - 1), "Cleanup uses actual subscribed publisher, not a changed field.");
    }

    [Test]
    public void ExistingUpgradeAuthority_InsufficientFundsCostsAndMaxLevel_RefreshDetailsWithoutSave()
    {
        UseAuthoredFixture();
        Initialize();
        owner.Show();
        string id = SectorTechnologyCatalog.Definitions[0].Id;
        Assert.That(Upgrade().interactable, Is.False);
        Assert.That(controller.TryUpgradeSectorTechnology(id), Is.False);
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 10);
        Assert.That(Upgrade().interactable, Is.True);
        for (int level = 1; level <= 3; level++)
        {
            // Same controller transaction as the UI; SaveManager is explicitly absent in this test-owned fixture.
            Assert.That(controller.TryUpgradeSectorTechnology(id), Is.True);
            Assert.That(progress.GetSectorTechnologyLevel(id), Is.EqualTo(level));
            StringAssert.Contains("Lv " + level, Read<TextMeshProUGUI>(owner, "selectedLevelText").text);
        }
        Assert.That(progress.StabilizedAlloy, Is.Zero);
        Assert.That(Upgrade().interactable, Is.False);
        Assert.That(controller.TryUpgradeSectorTechnology(id), Is.False);
        StringAssert.Contains("최대", Read<TextMeshProUGUI>(owner, "upgradeButtonText").text);
    }

    [Test]
    public void CardFocusRoutesToSidebarWhenUpgradeLocked_NoBackOrTemplateFocus()
    {
        UseAuthoredFixture();
        Initialize();
        owner.Show();
        Assert.That(Card(0).CardButton.navigation.selectOnLeft, Is.SameAs(sidebar));
        Assert.That(Card(0).CardButton.navigation.selectOnUp, Is.Null);
        Assert.That(Card(4).CardButton.navigation.selectOnDown, Is.Null);
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 2);
        Assert.That(Card(0).CardButton.navigation.selectOnLeft, Is.SameAs(Upgrade()));
        Assert.That(Upgrade().navigation.selectOnLeft, Is.SameAs(sidebar));
        Assert.That(Template().CardButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
        Assert.That(Root().Find("SectorTechnologyBackButton"), Is.Null);
        navigation.SetDialogueModalActiveForEditorAndTests(true);
        string before = Read<TextMeshProUGUI>(owner, "selectedNameText").text;
        Card(1).CardButton.onClick.Invoke();
        Assert.That(Read<TextMeshProUGUI>(owner, "selectedNameText").text, Is.EqualTo(before));
    }

    [Test]
    public void MissingAuthoredBinding_WarnsOnceAndCreatesNoReplacement()
    {
        UseAuthoredFixture();
        Bind(owner, "selectedCostText", null);
        int[] before = Objects();
        LogAssert.Expect(LogType.Warning, new Regex("Ship Reinforcement UI unavailable:.*Inspector binding"));
        Initialize();
        Initialize();
        owner.Show();
        Assert.That(owner.CanShow, Is.False);
        Assert.That(Objects(), Is.EqualTo(before));
        Assert.That(sidebar.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void RepeatedInitialization_UpgradeClickSpendsOnceAndRestoresFocusWhenLocked()
    {
        UseAuthoredFixture();
        Initialize();
        owner.Show();
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 2);
        for (int i = 0; i < 3; i++) { Initialize(); Call(owner, "OnDisable"); Call(owner, "OnEnable"); }
        SettlementSectorTechnologyEntrySelection selected = Catalog().Find(SectorTechnologyCatalog.StabilizedFrameId)
            .GetComponent<SettlementSectorTechnologyEntrySelection>();
        AuthoredRuntimeFixture.AssertScene(selected.gameObject, scene);
        AuthoredRuntimeFixture.SelectEventSystem(events, scene);
        selected.CardButton.onClick.Invoke();
        Assert.That(owner.IsOpen && selected.CardButton.IsActive() && selected.CardButton.IsInteractable(), Is.True);
        int[] objects = Objects();
        int notifications = 0;
        Action notified = () => notifications++;
        Scene temporary = EditorSceneManager.NewPreviewScene();
        AudioManager previousAudio = AudioManager.Instance;
        AudioEventDatabase database = null;
        try
        {
            database = ScriptableObject.CreateInstance<AudioEventDatabase>();
            GameObject audioRoot = AuthoredRuntimeFixture.Create(temporary, null, "TestOnlyAudio", false);
            audioRoot.SetActive(false);
            AudioManager audio = audioRoot.AddComponent<AudioManager>();
            Bind(audio, "database", database);
            Policy(audio, "logMissingEvents", false);
            typeof(AudioManager).GetProperty("Instance").SetValue(null, audio);
            events.SetSelectedGameObject(Upgrade().gameObject);
            TraceFocus("Before upgrade", selected);
            Assert.That(EventSystem.current, Is.SameAs(events));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(Upgrade().gameObject));
            Assert.That(Upgrade().IsActive() && Upgrade().IsInteractable(), Is.True);
            progress.Changed += notified;
            Upgrade().onClick.Invoke();
            TraceFocus("Immediately after synchronous upgrade/Changed/Refresh", selected);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(progress.GetSectorTechnologyLevel(SectorTechnologyCatalog.StabilizedFrameId), Is.EqualTo(1));
            Assert.That(progress.StabilizedAlloy, Is.Zero);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(selected.gameObject), FocusDescription(selected));
            owner.Refresh(); // The production boundary is synchronous Refresh, not a delayed Show/focus reset.
            TraceFocus("After repeat Refresh", selected);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(selected.gameObject), FocusDescription(selected));
            Assert.That(Objects(), Is.EqualTo(objects), "Upgrade must retain the selected card, not rebuild it.");
        }
        finally
        {
            progress.Changed -= notified;
            try { AuthoredRuntimeFixture.Close(temporary); }
            finally
            {
                typeof(AudioManager).GetProperty("Instance").SetValue(null, previousAudio);
                if (database != null) Object.DestroyImmediate(database);
            }
        }
    }

    [Test]
    public void UnitySelectable_UnavailableFocusedActionClearsSelectionSynchronously()
    {
        Initialize();
        owner.Show();
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 2);
        events.SetSelectedGameObject(Upgrade().gameObject);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(Upgrade().gameObject));
        Upgrade().interactable = false;
        Assert.That(events.currentSelectedGameObject, Is.Null,
            "uGUI clears focus inside the setter; the presenter must capture ownership before its detail refresh.");
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void AvailabilityRefresh_RestoresOnlyItsOwnActionFocus(bool actionOwnsFocus, bool cardAvailable)
    {
        Initialize();
        owner.Show();
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 2);
        var selected = Catalog().Find(SectorTechnologyCatalog.StabilizedFrameId).GetComponent<SettlementSectorTechnologyEntrySelection>();
        selected.CardButton.onClick.Invoke();
        selected.CardButton.interactable = cardAvailable;
        GameObject focused = actionOwnsFocus ? Upgrade().gameObject : sidebar.gameObject;
        events.SetSelectedGameObject(focused);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(focused));
        Assert.That(progress.TryUpgradeSectorTechnology(SectorTechnologyCatalog.StabilizedFrameId), Is.True);
        owner.Refresh();
        Assert.That(Upgrade().IsInteractable(), Is.False);
        GameObject expected = actionOwnsFocus && cardAvailable ? selected.gameObject : sidebar.gameObject;
        Assert.That(events.currentSelectedGameObject, Is.SameAs(expected), FocusDescription(selected));
    }

    private string FocusDescription(SettlementSectorTechnologyEntrySelection selected)
    {
        GameObject focus = events != null ? events.currentSelectedGameObject : null;
        return $"technology={SectorTechnologyCatalog.StabilizedFrameId}, panelOpen={owner.IsOpen}, " +
            $"focus={(focus != null ? AuthoredRuntimeFixture.Describe(focus) : "null")}; " +
            $"EventSystem={AuthoredRuntimeFixture.Describe(events.gameObject)}, authoritative={EventSystem.current == events}; " +
            $"card={AuthoredRuntimeFixture.Describe(selected.gameObject)}, active={selected.CardButton.IsActive()}, " +
            $"interactable={selected.CardButton.IsInteractable()}, upgradeActive={Upgrade().IsActive()}, upgradeInteractable={Upgrade().IsInteractable()}";
    }

    private void TraceFocus(string boundary, SettlementSectorTechnologyEntrySelection selected) =>
        TestContext.WriteLine(boundary + ": " + FocusDescription(selected));

    [Test]
    public void MissingTechnology_CannotConstructOrDispatchTransactions()
    {
        Bind(owner, "panelRoot", null);
        int[] objects = Objects();
        string balance = JsonUtility.ToJson(progress.CreateSaveData());
        LogAssert.Expect(LogType.Warning, new Regex("Ship Reinforcement UI unavailable:.*panelRoot.*Inspector binding"));
        Initialize();
        Initialize();
        owner.Show();
        owner.Refresh();
        Assert.That(owner.CanShow, Is.False);
        Assert.That(owner.NavigationRoot, Is.Null);
        Assert.That(Objects(), Is.EqualTo(objects));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(balance));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void HoverAndFocusNeverStealExplicitPurchaseSelection()
    {
        Initialize();
        owner.Show();
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 50);
        Card(2).CardButton.onClick.Invoke();
        events.SetSelectedGameObject(Card(1).gameObject);
        Card(1).OnSelect(new BaseEventData(events));
        Card(1).OnPointerEnter(new PointerEventData(events));
        Assert.That(Card(1).CardImage.color, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(Card(1).CardOutline.effectColor, Is.EqualTo(SettlementSelectionColors.Hover));
        Assert.That(Card(2).CardOutline.effectColor, Is.EqualTo(SettlementSelectionColors.Selected));
        Assert.That(Read<TextMeshProUGUI>(owner, "selectedNameText").text,
            Is.EqualTo(SectorTechnologyCatalog.Definitions[2].DisplayName));
        AudioManager previousAudio = AudioManager.Instance;
        AudioEventDatabase database = ScriptableObject.CreateInstance<AudioEventDatabase>();
        GameObject audioRoot = Create(fixture.transform, "SelectionTestAudio").gameObject;
        AudioManager audio = audioRoot.AddComponent<AudioManager>();
        Bind(audio, "database", database);
        typeof(AudioManager).GetProperty("Instance").SetValue(null, audio);
        try { Upgrade().onClick.Invoke(); }
        finally
        {
            typeof(AudioManager).GetProperty("Instance").SetValue(null, previousAudio);
            Object.DestroyImmediate(audioRoot);
            Object.DestroyImmediate(database);
        }
        Assert.That(progress.GetSectorTechnologyLevel(SectorTechnologyCatalog.Definitions[2].Id), Is.EqualTo(1));
        Assert.That(progress.GetSectorTechnologyLevel(SectorTechnologyCatalog.Definitions[1].Id), Is.Zero);
        Card(1).OnPointerExit(new PointerEventData(events));
        Card(1).OnDeselect(new BaseEventData(events));
        Assert.That(Card(2).CardImage.color, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(Card(2).CardOutline.enabled, Is.True);
        Assert.That(Card(1).CardImage.color, Is.EqualTo(Template().CardImage.color));
        ExecuteEvents.Execute(Card(1).gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Assert.That(Read<TextMeshProUGUI>(owner, "selectedNameText").text,
            Is.EqualTo(SectorTechnologyCatalog.Definitions[1].DisplayName), "Submit commits a purchase selection.");
    }

    [TestCase(0, "objective_first_part", 0)]
    [TestCase(1, "objective_analyze", 1)]
    [TestCase(2, "objective_remaining_parts", 1)]
    [TestCase(3, "objective_remaining_parts", 2)]
    [TestCase(4, "objective_restore", 3)]
    [TestCase(5, "objective_activate", 3)]
    [TestCase(6, "objective_defend", 3)]
    [TestCase(7, "objective_final", 3)]
    [TestCase(8, "objective_complete", 3)]
    public void AuthoredProgressPanel_ProjectsAuthorityAndRefreshesOnChanges(int stage, string objective, int parts)
    {
        SettlementProgressPanelUI panel = AuthoredRuntimeFixture.Single<SettlementProgressPanelUI>(scene);
        Assert.That(panel.transform.parent, Is.SameAs(hud));
        Assert.That(panel.name, Is.EqualTo("CampaignProgressPanel"));
        Assert.That(Read<SettlementController>(panel, "settlementController"), Is.SameAs(controller));
        Assert.That(Read<LocalizationCatalog>(panel, "localizationCatalog"), Is.Not.Null);
        Assert.That(panel.GetComponentInChildren<Button>(true), Is.Null, "The progress projection has no action authority.");
        Call(panel, "OnEnable");
        try
        {
            progress.TryStartDamagedAccessKeyQuest();
            if (stage >= 1) progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            if (stage >= 2) progress.TryAuthorizeAnalyzedRegion();
            if (stage >= 3)
            {
                progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
                progress.TryAuthorizeAnalyzedRegion();
            }
            if (stage >= 4) progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            if (stage >= 5) Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True);
            if (stage >= 6) Assert.That(progress.TryActivateRouteCore(), Is.True);
            if (stage >= 7) progress.MarkSettlementDefenseCleared();
            if (stage >= 8) progress.RegisterCampaignBossDefeat(CampaignBossId.NullDispatcher);
            // No Refresh call here: the rendered text must follow the authority's Changed event.
            Assert.That(SettlementProgressPanelUI.GetObjectiveState(progress), Is.EqualTo(objective));
            LocalizationCatalog catalog = Read<LocalizationCatalog>(panel, "localizationCatalog");
            Assert.That(catalog.TryGetText("ui.settlement.progress." + objective,
                GameSettingsRuntime.LanguageCode, out string expected, out _), Is.True);
            Assert.That(Read<TMP_Text>(panel, "objectiveText").text, Is.EqualTo(expected));
            Assert.That(Read<TMP_Text>(panel, "statusText").text, Does.Contain(parts + "/3"));
            Assert.That(Read<TMP_Text>(panel, "statusText").text.Split('\n').Length,
                Is.EqualTo(stage >= 7 ? 4 : stage >= 6 ? 3 : 2));
            string stateBefore = JsonUtility.ToJson(progress.CreateSaveData());
            int[] before = Objects();
            for (int i = 0; i < 3; i++) panel.Refresh();
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(stateBefore));
            Assert.That(Objects(), Is.EqualTo(before));
        }
        finally { Call(panel, "OnDisable"); }
    }

    [TestCase("ko")]
    [TestCase("en")]
    public void ProgressPanel_LocalizedObjectivesAndFinalStatusFitAuthoredRectangle(string language)
    {
        SettlementProgressPanelUI panel = AuthoredRuntimeFixture.Single<SettlementProgressPanelUI>(scene);
        LocalizationCatalog catalog = Read<LocalizationCatalog>(panel, "localizationCatalog");
        TMP_Text objective = Read<TMP_Text>(panel, "objectiveText");
        TMP_Text status = Read<TMP_Text>(panel, "statusText");
        foreach (LocalizationEntry entry in catalog.Entries)
        {
            if (!entry.TextKey.StartsWith("ui.settlement.progress.objective_", StringComparison.Ordinal)) continue;
            catalog.TryGetText(entry.TextKey, language, out string content, out _);
            Vector2 preferred = objective.GetPreferredValues(content, objective.rectTransform.rect.width, float.PositiveInfinity);
            Assert.That(preferred.y, Is.LessThanOrEqualTo(objective.rectTransform.rect.height + 0.1f), entry.TextKey + " " + language);
        }
        string Local(string suffix)
        {
            Assert.That(catalog.TryGetText("ui.settlement.progress." + suffix, language, out string content, out _), Is.True);
            return content;
        }
        string finalStatus = Local("parts").Replace("{count}", "3") + "\n" +
            Local("core").Replace("{state}", Local("core_active")) + "\n" +
            Local("defense_clear") + "\n" + Local("final_available");
        Assert.That(status.GetPreferredValues(finalStatus, status.rectTransform.rect.width, float.PositiveInfinity).y,
            Is.LessThanOrEqualTo(status.rectTransform.rect.height + 0.1f), language);
    }

    [Test]
    public void ProgressPanel_EnterReenableAndCleanupKeepOneSubscription()
    {
        SettlementProgressPanelUI panel = AuthoredRuntimeFixture.Single<SettlementProgressPanelUI>(scene);
        Call(panel, "OnEnable");
        Call(panel, "OnEnable");
        Delegate subscribers = (Delegate)typeof(PermanentProgress).GetField("Changed", Private).GetValue(progress);
        Assert.That(subscribers.GetInvocationList().Count(d => ReferenceEquals(d.Target, panel)), Is.EqualTo(1));
        Call(panel, "OnDisable");
        progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        Call(panel, "OnEnable");
        Assert.That(Read<TMP_Text>(panel, "statusText").text, Does.Contain("1/3"));
        Call(panel, "OnDisable");
        Call(panel, "OnDisable");
        subscribers = (Delegate)typeof(PermanentProgress).GetField("Changed", Private).GetValue(progress);
        Assert.That(subscribers?.GetInvocationList().Any(d => ReferenceEquals(d.Target, panel)) ?? false, Is.False);
    }

    private void UseAuthoredFixture()
    {
        Assert.That(scene.IsValid(), Is.True);
        Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene), Is.True);
    }    private void Initialize() => owner.Initialize(controller, navigation, repair.gameObject, action, null);
    private RectTransform Root() => Read<GameObject>(owner, "panelRoot").GetComponent<RectTransform>();
    private RectTransform Catalog() => Read<RectTransform>(owner, "catalogRoot");
    private SettlementSectorTechnologyEntrySelection Template() => Read<SettlementSectorTechnologyEntrySelection>(owner, "cardTemplate");
    private SettlementSectorTechnologyEntrySelection Card(int i) => Catalog().Find(SectorTechnologyCatalog.Definitions[i].Id).GetComponent<SettlementSectorTechnologyEntrySelection>();
    private Button Upgrade() => Read<Button>(owner, "upgradeButton");
    private RectTransform Create(Transform parent, string name) => (RectTransform)AuthoredRuntimeFixture.Create(scene, parent, name, true).transform;
    private TextMeshProUGUI Text(Transform parent, string name)
    {
        TextMeshProUGUI text = Create(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Txt/neodgm_pro.asset");
        return text;
    }
    private Button Button(Transform parent, string name)
    {
        RectTransform root = Create(parent, name);
        Image image = root.gameObject.AddComponent<Image>();
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI label = Create(root, "Label").gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Txt/neodgm_pro.asset");
        return button;
    }
    private int[] Objects() => fixture.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).OrderBy(id => id).ToArray();
    private static int[] Roots(Scene target) => target.GetRootGameObjects().Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray();
    private static int SubscriberCount(SettlementController target) => ((Delegate)typeof(SettlementController).GetField("Changed", Private).GetValue(target))?.GetInvocationList().Length ?? 0;
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static T Read<T>(Object target, string field) where T : Object => AuthoredRuntimeFixture.Read<T>(target, field);
    private static void Bind(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Policy(Object target, string field, bool value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).boolValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
