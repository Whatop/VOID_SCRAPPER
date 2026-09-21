using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class SettlementNavigationUIAuthoringTests
{
    private Scene scene, userScene;
    private bool userDirty;
    private int[] userRoots;
    private GameObject fixture;
    private SettlementUIController owner;
    private SettlementHUD hud;
    private ShipTraitTreePanel traits;
    private SettlementController publisher;
    private EventSystem eventSystem;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userDirty = userScene.isDirty;
        userRoots = RootIds(userScene);
        scene = AuthoredRuntimeFixture.Open("Settlement");
        fixture = AuthoredRuntimeFixture.Group(scene).gameObject;
        fixture.SetActive(false);
        hud = AuthoredRuntimeFixture.Single<SettlementHUD>(scene);
        owner = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        publisher = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        traits = AuthoredRuntimeFixture.Single<ShipTraitTreePanel>(scene);
        eventSystem = AuthoredRuntimeFixture.Single<EventSystem>(scene);
        TestContext.WriteLine($"Initial {AuthoredRuntimeFixture.Describe(owner.gameObject)}: " +
            $"panel={Get<SettlementPanelKind>(owner, "currentPanel")}, facility={Get<BuildingType>(owner, "selectedBuilding")}, " +
            $"focus={eventSystem.currentSelectedGameObject}, publisher={AuthoredRuntimeFixture.Describe(publisher.gameObject)}");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (owner != null)
            {
                Call(owner, "UnsubscribeButtons");
                Call(owner, "UnsubscribeController");
                Call(owner, "UnsubscribeDialogueLifecycle");
                SettlementSectorTechnologyPanelUI sector = Read<SettlementSectorTechnologyPanelUI>(owner, "sectorTechnologyPanelUI");
                if (sector != null) Call(sector, "OnDisable");
            }
        }
        finally
        {
            try { AuthoredRuntimeFixture.ReleaseEventSystem(ref eventSystem, scene); }
            finally
            {
                try { if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene); }
                finally { scene = default; }
            }
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(RootIds(userScene), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    [Test]
    public void RuntimeInitializationAndSelection_PreserveAuthoredBaselinesWithoutConstructionOrDrift()
    {
        UseAuthoredFixture();
        View<Image>(0, "Background").color = new Color(.3f, .4f, .5f, .45f);
        View<TextMeshProUGUI>(0, "Label").color = new Color(.2f, .3f, .4f, .65f);
        View<Image>(0, "Icon").color = new Color(.1f, .2f, .3f, .2f);
        string[] before = PresentationSnapshots();
        int[] ids = ObjectIds();
        Call(owner, "InitializeNavigationPresentation");
        for (int i = 0; i < 10; i++)
        {
            Set(owner, "currentPanel", SettlementPanelKind.Main);
            Call(owner, "RefreshNavigationState");
            Set(owner, "currentPanel", SettlementPanelKind.Repair);
            Call(owner, "RefreshNavigationState");
            Call(owner, "InitializeNavigationPresentation");
        }
        Assert.That(View<Image>(0, "Background").color, Is.EqualTo(new Color(.3f, .4f, .5f, .45f)));
        Assert.That(View<TextMeshProUGUI>(0, "Label").color, Is.EqualTo(new Color(.2f, .3f, .4f, .65f)));
        Assert.That(View<Image>(0, "Icon").color, Is.EqualTo(new Color(.1f, .2f, .3f, .2f)));
        Assert.That(ObjectIds(), Is.EqualTo(ids));
        // Typography defaults in the two other presenters cannot reset the migrated action labels.
        TextMeshProUGUI shipLabel = Read<TextMeshProUGUI>(hud, "shipActionButtonLabelText");
        TextMeshProUGUI traitLabel = Read<TextMeshProUGUI>(traits, "unlockButtonLabelText");
        shipLabel.fontSize = 13; traitLabel.fontSize = 12;
        hud.SetMainShipDetail("Authored ship", "Content update", "Select");
        Assert.That(typeof(ShipTraitTreePanel).GetMethod("ConfigureSettlementPresentation", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        Assert.That(shipLabel.fontSize, Is.EqualTo(13));
        Assert.That(traitLabel.fontSize, Is.EqualTo(12));
        Assert.That(EditorJsonUtility.ToJson(Primary(0).transform), Is.EqualTo(before[0]));
    }

    [Test]
    public void RepeatedSubscription_OneOwnedActionPerButtonAndCleanupKeepsUnrelatedListeners()
    {
        UseAuthoredFixture();
        Call(owner, "InitializeNavigationPresentation");
        // No gameplay readiness or user save is needed to exercise the existing navigation dispatcher.
        Bind(owner, "settlementController", null);
        int unrelated = 0;
        owner.SelectBuilding(BuildingType.Hangar);
        Button next = Read<Button>(owner, "repairNextButton");
        next.onClick.AddListener(() => unrelated++);
        for (int i = 0; i < 3; i++) Call(owner, "SubscribeButtons");
        IList listeners = Get<IList>(owner, "ownedButtonListeners");
        Assert.That(listeners.Count, Is.EqualTo(15)); // trait unlock stays panel-owned
        for (int i = 0; i < AuthoredRuntimeFixture.NavigationFields.Length; i++)
        {
            KeyValuePair<Button, UnityAction> binding = listeners.Cast<KeyValuePair<Button, UnityAction>>().Single(pair => pair.Key == Primary(i));
            Assert.That(binding.Value.Method.Name, Is.EqualTo(AuthoredRuntimeFixture.NavigationActions[i]));
        }
        BuildingType before = Get<BuildingType>(owner, "selectedBuilding");
        next.onClick.Invoke();
        Assert.That(Get<BuildingType>(owner, "selectedBuilding"), Is.Not.EqualTo(before));
        Assert.That(unrelated, Is.EqualTo(1));
        Assert.That(Get<BuildingType>(owner, "selectedBuilding"), Is.EqualTo(BuildingType.EngineWorkshop));
        Call(owner, "UnsubscribeButtons");
        next.onClick.Invoke();
        Assert.That(Get<BuildingType>(owner, "selectedBuilding"), Is.EqualTo(BuildingType.EngineWorkshop));
        Assert.That(unrelated, Is.EqualTo(2));
    }

    [Test]
    public void PersistentOwnedAction_IsNotRegisteredAgainAndUnrelatedCallbacksAreRetained()
    {
        UseAuthoredFixture();
        UnityEventTools.AddPersistentListener(Primary(1).onClick, owner.ShowRepairPanel);
        Call(owner, "SubscribeButtons");
        IList listeners = Get<IList>(owner, "ownedButtonListeners");
        Assert.That(listeners.Count, Is.EqualTo(14));
        UseAuthoredFixture();
        Assert.That(Primary(1).onClick.GetPersistentEventCount(), Is.EqualTo(1));
        UnityEventTools.AddPersistentListener(Primary(1).onClick, owner.ShowTraitPanel);
        Assert.That(Primary(1).onClick.GetPersistentEventCount(), Is.EqualTo(2));
    }

    [Test]
    public void RoutesInitialSelectionBackAndDialogueRestrictions_UseExistingController()
    {
        WithPresentationServices(() =>
        {
            UseAuthoredFixture();
            Bind(owner, "settlementController", null);
            Set(owner, "useShipTraitTreePanel", false); // no trait content initialization in this presentation fixture
            fixture.SetActive(true);
            AuthoredRuntimeFixture.SelectEventSystem(eventSystem, scene);
            Call(owner, "InitializeNavigationPresentation");
            Call(owner, "SubscribeButtons");
            InitializeSectorPresentation();
            Call(owner, "Start");
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(0).gameObject));
            Primary(1).onClick.Invoke();
            Assert.That(Read<GameObject>(owner, "repairPanel").activeSelf, Is.True);
            Assert.That(Read<GameObject>(owner, "mainPanel").activeSelf, Is.False);
            Read<Button>(owner, "repairBackButton").onClick.Invoke();
            Assert.That(Read<GameObject>(owner, "mainPanel").activeSelf, Is.True);
            Primary(3).onClick.Invoke();
            Assert.That(Read<GameObject>(owner, "traitPanel").activeSelf, Is.True);
            Read<Button>(owner, "traitBackButton").onClick.Invoke();
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
            Read<Button>(owner, "sectorTechnologyNavigationButton").onClick.Invoke();
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.SectorTechnology));
            owner.ShowMainPanel();
            owner.SetDialogueModalActiveForEditorAndTests(true);
            CanvasGroup group = Read<CanvasGroup>(owner, "settlementInputGroup");
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            ExecuteEvents.Execute(Primary(1).gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
            Assert.That(SettlementExpeditionLaunchGuard.TryPassDialogueGate(true, out _), Is.False);
            Call(owner, "OnDisable");
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
            Assert.That(Get<IList>(owner, "ownedButtonListeners"), Is.Empty);
        });
    }

    [Test]
    public void PublisherSubscriptions_AreBalancedAgainstActualPublisher()
    {
        UseAuthoredFixture();
        Call(owner, "SubscribeController");
        Call(owner, "SubscribeController");
        Delegate changed = Get<Delegate>(publisher, "Changed");
        Assert.That(changed.GetInvocationList().Count(d => d.Target == owner), Is.EqualTo(1));
        Bind(owner, "settlementController", null);
        Call(owner, "UnsubscribeController");
        changed = Get<Delegate>(publisher, "Changed");
        Assert.That(changed == null || changed.GetInvocationList().All(d => d.Target != owner), Is.True);
    }

    [Test]
    public void Submit_AfterRepeatedInitializationDispatchesOnlyOneAction()
    {
        UseAuthoredFixture();
        Bind(owner, "settlementController", null);
        owner.SelectBuilding(BuildingType.Hangar);
        fixture.SetActive(true);
        AuthoredRuntimeFixture.SelectEventSystem(eventSystem, scene);
        Button next = Read<Button>(owner, "repairNextButton");
        // Deactivate after the synchronous action so no edit-mode visual submit coroutine is started.
        next.onClick.AddListener(() => next.gameObject.SetActive(false));
        for (int i = 0; i < 3; i++)
        {
            Call(owner, "InitializeNavigationPresentation");
            Call(owner, "SubscribeButtons");
        }
        ExecuteEvents.Execute(next.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        Assert.That(Get<BuildingType>(owner, "selectedBuilding"), Is.EqualTo(BuildingType.EngineWorkshop));
        Assert.That(next.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void CompleteAuthoredBindings_NeverInvokeConstruction()
    {
        UseAuthoredFixture();
        int[] ids = ObjectIds();
        Call(owner, "InitializeNavigationPresentation");
        Assert.That(ObjectIds(), Is.EqualTo(ids));
        Assert.That(Get<bool>(owner, "navigationDiagnosticReported"), Is.False);
    }

    [Test]
    public void MissingAuthoredBinding_WarnsOnceAndDoesNotConstructOrLockInput()
    {
        UseAuthoredFixture();
        Bind(owner, "hangarNavigationView.Icon", null);
        int[] ids = ObjectIds();
        LogAssert.Expect(LogType.Warning, new Regex("Settlement navigation bindings.*Inspector binding"));
        Call(owner, "InitializeNavigationPresentation");
        Call(owner, "InitializeNavigationPresentation");
        Call(owner, "RefreshNavigationState");
        Call(owner, "OnDisable");
        Assert.That(ObjectIds(), Is.EqualTo(ids));
        CanvasGroup group = Read<CanvasGroup>(owner, "settlementInputGroup");
        Assert.That(group.interactable && group.blocksRaycasts, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void MissingNavigation_CannotConstructOrRestyleControls()
    {
        Bind(owner, "navigationRoot", null);
        Bind(owner, "hangarNavigationButton", null);
        int[] objects = ObjectIds();
        Button button = Read<Button>(owner, "openRepairPanelButton");
        string layout = EditorJsonUtility.ToJson(button.transform);
        string label = EditorJsonUtility.ToJson(button.GetComponentInChildren<TextMeshProUGUI>(true));
        LogAssert.Expect(LogType.Warning, new Regex("Settlement navigation bindings.*navigationRoot.*Inspector binding"));
        Call(owner, "InitializeNavigationPresentation");
        Call(owner, "InitializeNavigationPresentation");
        Call(owner, "RefreshNavigationState");
        Call(owner, "OnDisable");
        Assert.That(Read<Button>(owner, "hangarNavigationButton"), Is.Null);
        Assert.That(ObjectIds(), Is.EqualTo(objects));
        Assert.That(EditorJsonUtility.ToJson(button.transform), Is.EqualTo(layout));
        Assert.That(EditorJsonUtility.ToJson(button.GetComponentInChildren<TextMeshProUGUI>(true)), Is.EqualTo(label));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void FocusAndActiveTab_AreSeparate_MoveClampsAndSkipsWithoutActivation()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            owner.ShowRepairPanel();
            Primary(2).interactable = false;
            Move(MoveDirection.Down);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(3).gameObject));
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Repair));
            Assert.That(View<Image>(1, "ActiveStrip").enabled, Is.True);
            Assert.That(View<Image>(3, "ActiveStrip").enabled, Is.False);
            Assert.That(View<Outline>(3, "ActiveOutline").enabled, Is.True);
            Assert.That(View<Outline>(1, "ActiveOutline").enabled, Is.False);
            Move(MoveDirection.Down);
            Move(MoveDirection.Down);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(5).gameObject));
            Move(MoveDirection.Down);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(5).gameObject));
            Primary(3).gameObject.SetActive(false);
            Primary(4).gameObject.SetActive(false);
            Move(MoveDirection.Up);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(1).gameObject));
            Move(MoveDirection.Up);
            Move(MoveDirection.Up);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(0).gameObject));
        });
    }

    [Test]
    public void PanelFocusAndDialogue_DoNotMoveSidebar_AndLeftReturnsFromPanel()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            Move(MoveDirection.Right);
            Button action = Read<Button>(owner, "shipActionButton");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(action.gameObject));
            int index = Get<int>(owner, "primaryNavigationIndex");
            Move(MoveDirection.Down);
            Assert.That(Get<int>(owner, "primaryNavigationIndex"), Is.EqualTo(index));
            Move(MoveDirection.Left);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Primary(0).gameObject));
            owner.SetDialogueModalActiveForEditorAndTests(true);
            var data = new AxisEventData(eventSystem) { moveDir = MoveDirection.Down };
            Primary(0).GetComponent<SettlementPrimaryNavigationPointer>().OnMove(data);
            Assert.That(Get<int>(owner, "primaryNavigationIndex"), Is.EqualTo(index));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
        });
    }

    [Test]
    public void ExistingNavigateBindings_WsArrowsAndControllerFeedOneMove_SpaceSubmitsOnce()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            string path = AssetDatabase.GUIDToAssetPath("ca9f5fa95ffab41fb9a615ab714db018");
            InputActionAsset actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(path));
            InputSettings previousSettings = InputSystem.settings;
            InputSettings settings = Object.Instantiate(previousSettings);
            Keyboard previousKeyboard = Keyboard.current;
            Gamepad previousGamepad = Gamepad.current;
            Keyboard keyboard = null;
            Gamepad gamepad = null;
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            bool moduleInitialized = false;
            var references = new List<InputActionReference>();
            Action<InputAction.CallbackContext> countMove = null;
            InputAction navigate = actions.FindAction("UI/Navigate", true);
            int moves = 0, invokes = 0;
            UnityAction clicked = () =>
            {
                invokes++;
                // Avoid Button's PlayMode-only submit animation coroutine after dispatch.
                Primary(1).gameObject.SetActive(false);
            };
            try
            {
                Assert.That(module, Is.Not.Null, AuthoredRuntimeFixture.Describe(eventSystem.gameObject));
                AuthoredRuntimeFixture.AssertScene(module.gameObject, scene);
                module.enabled = false;
                // PreviewScene suppressed OnEnable. BaseInputModule.OnDisable cannot
                // run first: its EventSystem cache is established only by OnEnable.
                settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings = settings;
                keyboard = InputSystem.AddDevice<Keyboard>();
                gamepad = InputSystem.AddDevice<Gamepad>();
                actions.Disable();
                actions.devices = new InputDevice[] { keyboard, gamepad };
                actions.bindingMask = null;
                Assert.That(EditorUtility.IsPersistent(actions), Is.False);
                // Bind only the disposable module. Do not enable the saved shared asset.
                Bind(module, "m_ActionsAsset", actions);
                module.point = module.leftClick = module.rightClick = module.middleClick = module.scrollWheel = null;
                module.trackedDevicePosition = module.trackedDeviceOrientation = null;
                InputActionReference Reference(string name)
                {
                    InputActionReference value = InputActionReference.Create(actions.FindAction(name, true));
                    references.Add(value);
                    return value;
                }
                module.move = Reference("UI/Navigate");
                module.submit = Reference("UI/Submit");
                module.cancel = Reference("UI/Cancel");
                for (int i = 0; i < 3; i++)
                {
                    module.enabled = true;
                    moduleInitialized = true;
                    Call(module, "OnEnable");
                    Call(owner, "SubscribeButtons");
                    if (i < 2) { Call(module, "OnDisable"); module.enabled = false; }
                }
                actions.FindActionMap("UI", true).Enable();
                eventSystem.sendNavigationEvents = true;
                module.ActivateModule();
                countMove = context => { if (context.ReadValue<Vector2>() != Vector2.zero) moves++; };
                navigate.performed += countMove;
                Primary(1).onClick.AddListener(clicked);
                foreach (string binding in new[] { "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Gamepad>/dpad" })
                    Assert.That(navigate.bindings.Any(b => b.effectivePath == binding), Is.True, binding);
                Assert.That(navigate.controls.Any(c => c.device == keyboard), Is.True);
                Assert.That(navigate.controls.Any(c => c.device == gamepad), Is.True);

                void Neutral()
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    InputSystem.Update();
                    module.Process();
                    Assert.That(navigate.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
                }
                foreach (Key key in new[] { Key.W, Key.S, Key.UpArrow, Key.DownArrow })
                {
                    Neutral();
                    bool up = key == Key.W || key == Key.UpArrow;
                    eventSystem.SetSelectedGameObject(Primary(up ? 1 : 0).gameObject);
                    int before = moves;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    InputSystem.Update();
                    Assert.That(navigate.ReadValue<Vector2>().y, Is.EqualTo(up ? 1f : -1f), key.ToString());
                    module.Process();
                    Assert.That(moves - before, Is.EqualTo(1), key.ToString());
                    Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(Primary(up ? 0 : 1).gameObject), key.ToString());
                    Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
                    Assert.That(invokes, Is.Zero);
                }
                Neutral();
                eventSystem.SetSelectedGameObject(Primary(0).gameObject);
                int beforeGamepad = moves;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
                InputSystem.Update();
                Assert.That(navigate.ReadValue<Vector2>().y, Is.EqualTo(-1f), "Gamepad DpadDown");
                module.Process();
                Assert.That(moves - beforeGamepad, Is.EqualTo(1));
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(Primary(1).gameObject));
                Assert.That(invokes, Is.Zero);
                Neutral();
                // Space is the existing sidebar-only alias, processed by the module's
                // updateSelected dispatch; gamepad South exercises the bound Submit action.
                foreach (bool useSpace in new[] { true, false })
                {
                    Primary(1).gameObject.SetActive(true);
                    owner.ShowMainPanel();
                    eventSystem.SetSelectedGameObject(Primary(1).gameObject);
                    int before = invokes;
                    if (useSpace) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    else InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                    InputSystem.Update();
                    if (!useSpace) Assert.That(module.submit.action.WasPerformedThisFrame(), Is.True);
                    module.Process();
                    Assert.That(invokes - before, Is.EqualTo(1), useSpace ? "Space alias" : "Controller Submit");
                    Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Repair));
                    Neutral();
                    Assert.That(invokes - before, Is.EqualTo(1), "Release must not submit again.");
                }
            }
            finally
            {
                try
                {
                    if (navigate != null && countMove != null) navigate.performed -= countMove;
                    Primary(1).onClick.RemoveListener(clicked);
                    if (module != null)
                    {
                        if (moduleInitialized) Call(module, "OnDisable");
                        module.enabled = false;
                        module.UnassignActions();
                    }
                    actions.Disable();
                    if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (gamepad != null) InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    InputSystem.Update();
                }
                finally
                {
                    if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                    if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                    InputSystem.settings = previousSettings;
                    foreach (InputActionReference reference in references) Object.DestroyImmediate(reference);
                    Object.DestroyImmediate(actions);
                    Object.DestroyImmediate(settings);
                    if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                    if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
                }
            }
        });
    }

    [Test]
    public void SettingsButtonAndEscape_ShareModal_RestoreContentAndPriorFocus_OneTransitionPerFrame()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            Call(owner, "SubscribeController");
            EscSettingsMenuController settings = Read<EscSettingsMenuController>(owner, "settingsMenuController");
            Bind(settings, "menuRoot", Create(settings.transform, "TestModal").gameObject);
            Set(settings, "showCursorWhileOpen", false);
            Set(settings, "unlockCursorWhileOpen", false);
            owner.ShowRepairPanel();
            Move(MoveDirection.Right);
            GameObject prior = EventSystem.current.currentSelectedGameObject;
            Set(settings, "lastTransitionFrame", -1);
            Call(settings, "HandleEscape");
            Assert.That(settings.IsOpen, Is.True);
            Call(settings, "HandleEscape");
            Assert.That(settings.IsOpen, Is.True);
            var move = new AxisEventData(eventSystem) { moveDir = MoveDirection.Down };
            Primary(1).GetComponent<SettlementPrimaryNavigationPointer>().OnMove(move);
            Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Repair));
            Set(settings, "lastTransitionFrame", -1); // Simulate the next separate press; no time delay.
            Set(settings, "lastCancelFrame", -1);
            Call(settings, "HandleEscape");
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(prior));
            Assert.That(Read<GameObject>(owner, "repairPanel").activeSelf, Is.True);
            Primary(5).onClick.Invoke();
            Assert.That(settings.IsOpen, Is.True);
            settings.Close();
            Assert.That(GameplayPauseManager.Instance.PauseRequestCount, Is.Zero);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(prior));
        });
    }

    [Test]
    public void BackControlsStayHidden_SettingsStaysVisible_AndSectorBuilderOmitsOnlyItsBack()
    {
        WithPresentationServices(() =>
        {
            ReadyNavigation();
            SettlementSectorTechnologyPanelUI sector = Read<SettlementSectorTechnologyPanelUI>(owner, "sectorTechnologyPanelUI");
            InitializeSectorPresentation();
            Assert.That(sector.NavigationRoot, Is.Not.Null);
            AuthoredRuntimeFixture.AssertScene(sector.NavigationRoot.gameObject, scene);
            Assert.That(sector.NavigationRoot.Find("SectorTechnologyBackButton"), Is.Null);
            Assert.That(sector.NavigationEntry, Is.Not.Null);
            int[] objects = ObjectIds();
            Button[] controls = sector.NavigationRoot.GetComponentsInChildren<Button>(true);
            foreach (Action show in new Action[] { owner.ShowMainPanel, owner.ShowRepairPanel, owner.ShowSectorTechnologyPanel, owner.ShowTraitPanel })
            {
                show();
                Assert.That(Read<Button>(owner, "repairBackButton").gameObject.activeSelf, Is.False);
                Assert.That(Read<Button>(owner, "traitBackButton").gameObject.activeSelf, Is.False);
                Assert.That(Primary(4).gameObject.activeInHierarchy, Is.True);
                Assert.That(Primary(4).IsInteractable(), Is.True);
                CollectionAssert.AreEquivalent(controls, sector.NavigationRoot.GetComponentsInChildren<Button>(true));
                Assert.That(ObjectIds(), Is.EqualTo(objects), "Panel switching must not create a replacement or duplicate Back control.");
            }
        });
    }

    [Test]
    public void RebindingCompletion_ConsumesSameFrameMenuCancelWithoutSavingBindings()
    {
        InputRebindButtonUI row = Create(fixture.transform, "RebindGuard").gameObject.AddComponent<InputRebindButtonUI>();
        SettingsMenuTabController tabs = Create(fixture.transform, "TabsGuard").gameObject.AddComponent<SettingsMenuTabController>();
        tabs.ConfigureInputGuards(new[] { row }, new TMP_Dropdown[0], new GameObject[0]);
        typeof(InputRebindButtonUI).GetMethod("FinishRebind", Private).Invoke(row, new object[] { false });
        Assert.That(row.IsRebinding, Is.False);
        Assert.That(tabs.TryHandleMenuCancel(), Is.True);
    }

    [Test]
    public void BlueHoverAndFocusLeaveYellowCurrentSectionSelectedAfterExit()
    {
        ReadyNavigation();
        Image selected = View<Image>(0, "Background");
        Assert.That(selected.color, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        SettlementPrimaryNavigationPointer preview = Primary(2).GetComponent<SettlementPrimaryNavigationPointer>();
        preview.OnPointerEnter(new PointerEventData(eventSystem));
        Assert.That(View<Image>(2, "Background").color, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(selected.color, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(Get<SettlementPanelKind>(owner, "currentPanel"), Is.EqualTo(SettlementPanelKind.Main));
        preview.OnPointerExit(new PointerEventData(eventSystem));
        Assert.That(View<Image>(2, "Background").color, Is.Not.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(View<Image>(0, "ActiveStrip").color, Is.EqualTo(SettlementSelectionColors.Selected));
        Assert.That(View<Image>(0, "ActiveStrip").enabled, Is.True);
        Assert.That(selected.color, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
    }

    private void InitializeSectorPresentation()
    {
        SettlementSectorTechnologyPanelUI sector = Read<SettlementSectorTechnologyPanelUI>(owner, "sectorTechnologyPanelUI");
        AuthoredRuntimeFixture.AssertScene(sector.gameObject, scene);
        sector.Initialize(publisher, owner, Read<GameObject>(owner, "repairPanel"),
            Read<Button>(owner, "repairActionButton"), Read<Button>(owner, "repairBackButton"));
        Assert.That(sector.CanShow, Is.True, AuthoredRuntimeFixture.Describe(sector.gameObject));
    }

    private void ReadyNavigation()
    {
        UseAuthoredFixture();
        Bind(owner, "settlementController", null);
        Set(owner, "useShipTraitTreePanel", false);
        fixture.SetActive(true);
        AuthoredRuntimeFixture.SelectEventSystem(eventSystem, scene);
        Call(owner, "InitializeNavigationPresentation");
        Call(owner, "SubscribeButtons");
        Call(owner, "Start");
    }

    private void Move(MoveDirection direction)
    {
        var data = new AxisEventData(eventSystem) { moveDir = direction };
        ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
    }

    private void WithPresentationServices(Action action)
    {
        Scene temporary = EditorSceneManager.NewPreviewScene();
        AudioManager previousAudio = AudioManager.Instance;
        GameAudioLoopController previousLoops = GameAudioLoopController.Instance;
        MouseCursorManager previousCursor = MouseCursorManager.Instance;
        PermanentProgress previousProgress = PermanentProgress.Instance;
        FieldInfo pauseField = typeof(GameplayPauseManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousPause = pauseField.GetValue(null);
        AudioEventDatabase emptyDatabase = null;
        GameplayPauseManager pause = null;
        float scale = Time.timeScale, fixedDelta = Time.fixedDeltaTime;
        bool cursorVisible = Cursor.visible;
        CursorLockMode cursorLock = Cursor.lockState;
        try
        {
            emptyDatabase = ScriptableObject.CreateInstance<AudioEventDatabase>();
            GameObject services = AuthoredRuntimeFixture.Create(temporary, null, "TestOnlyPresentationServices", false);
            services.SetActive(false);
            AudioManager audio = services.AddComponent<AudioManager>();
            Bind(audio, "database", emptyDatabase);
            Set(audio, "logMissingEvents", false);
            Set(audio, "pooledSourceCount", 1);
            typeof(AudioManager).GetProperty("Instance").SetValue(null, audio);
            typeof(GameAudioLoopController).GetProperty("Instance").SetValue(null, services.AddComponent<GameAudioLoopController>());
            typeof(MouseCursorManager).GetProperty("Instance").SetValue(null, null);
            typeof(PermanentProgress).GetProperty("Instance").SetValue(null, services.AddComponent<PermanentProgress>());
            pause = services.AddComponent<GameplayPauseManager>();
            pauseField.SetValue(null, pause);
            action();
        }
        finally
        {
            try
            {
                EscSettingsMenuController settings = Read<EscSettingsMenuController>(owner, "settingsMenuController");
                if (settings != null && settings.IsOpen) settings.Close();
                if (pause != null) pause.ResetAllPauses();
            }
            finally
            {
                try { AuthoredRuntimeFixture.Close(temporary); }
                finally
                {
                    typeof(AudioManager).GetProperty("Instance").SetValue(null, previousAudio);
                    typeof(GameAudioLoopController).GetProperty("Instance").SetValue(null, previousLoops);
                    typeof(MouseCursorManager).GetProperty("Instance").SetValue(null, previousCursor);
                    typeof(PermanentProgress).GetProperty("Instance").SetValue(null, previousProgress);
                    pauseField.SetValue(null, previousPause);
                    Time.timeScale = scale;
                    Time.fixedDeltaTime = fixedDelta;
                    Cursor.visible = cursorVisible;
                    Cursor.lockState = cursorLock;
                    if (emptyDatabase != null) Object.DestroyImmediate(emptyDatabase);
                }
            }
        }
    }

    private void UseAuthoredFixture()
    {
        Assert.That(scene.IsValid(), Is.True);
        Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene), Is.True);
    }    private Button Primary(int index) => Read<Button>(owner, AuthoredRuntimeFixture.NavigationFields[index]);
    private T View<T>(int index, string field) where T : Object => Read<T>(owner, AuthoredRuntimeFixture.NavigationViews[index] + "." + field);
    private string[] PresentationSnapshots() => Enumerable.Range(0, AuthoredRuntimeFixture.NavigationFields.Length).SelectMany(i => new Object[] {
        Primary(i).transform, Primary(i), View<Image>(i, "Background"), View<Image>(i, "Icon"),
        View<Image>(i, "ActiveStrip"), View<TextMeshProUGUI>(i, "Label"), View<Outline>(i, "ActiveOutline") })
        .Select(EditorJsonUtility.ToJson).ToArray();
    private int[] ObjectIds() => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject.GetInstanceID()).OrderBy(i => i).ToArray();
    private static int[] RootIds(Scene target) => target.GetRootGameObjects().Select(r => r.GetInstanceID()).OrderBy(i => i).ToArray();
    private RectTransform Create(Transform parent, string name) => (RectTransform)AuthoredRuntimeFixture.Create(scene, parent, name, true).transform;
    private Button Button(Transform parent, string name)
    {
        RectTransform rect = Create(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = Create(rect, "Text (TMP)").gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Txt/neodgm_pro.asset");
        return button;
    }
    private static T Read<T>(Object target, string field) where T : Object => AuthoredRuntimeFixture.Read<T>(target, field);
    private static void Bind(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Get<T>(object target, string field)
    {
        string identity = target is Component component && component != null
            ? AuthoredRuntimeFixture.Describe(component.gameObject) : target?.GetType().FullName ?? "null";
        Assert.That(target, Is.Not.Null, $"Requested {typeof(T).Name} field '{field}'; target={identity}");
        FieldInfo member = target.GetType().GetField(field, Private);
        Assert.That(member, Is.Not.Null, $"Missing field '{field}' requested as {typeof(T).Name} on {target.GetType().Name}; {identity}");
        return (T)member.GetValue(target);
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
