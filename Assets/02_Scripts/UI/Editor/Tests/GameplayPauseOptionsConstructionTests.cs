using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GameplayPauseOptionsConstructionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string TestScenePath = "Assets/02_Scripts/UI/Editor/Tests/Scenes/GameplayPauseOptionsConstructionTest.unity";
    private static string TestSceneDiskPath => Path.Combine(Application.dataPath, TestScenePath.Substring("Assets/".Length));
    private Scene scene;
    private bool ownsTestScene;
    private int testSceneHandle;
    private byte[] testSceneBytes;
    private Scene activeScene;
    private string originalSceneDescription;
    private bool activeSceneWasDirty;
    private int[] activeRootIds;
    private EventSystem previousEventSystem;
    private GameplayPauseMenuController controller;
    private GameObject ownedRoot;
    private InputActionAsset actions;
    private TMP_FontAsset font;
    private readonly Dictionary<int, string> ownedIdentities = new Dictionary<int, string>();
    private readonly List<GameObject> runtimeRoots = new List<GameObject>();
    private string firstDirtyBoundary;
    private int actionAssetId;
    private bool observingAllocations;
    private readonly List<string> allocationTrace = new List<string>();
    private string allocationFailure;
    private readonly Dictionary<int, int> allocationScenes = new Dictionary<int, int>();
    private readonly List<GameObject> factoryObjects = new List<GameObject>();
    private readonly HashSet<int> userObjectIds = new HashSet<int>();

    [SetUp]
    public void SetUp()
    {
        activeScene = SceneManager.GetActiveScene();
        originalSceneDescription = AuthoredRuntimeFixture.Describe(activeScene);
        activeSceneWasDirty = activeScene.isDirty;
        activeRootIds = AuthoredRuntimeFixture.RootIds(activeScene);
        userObjectIds.Clear();
        foreach (GameObject userRoot in activeScene.GetRootGameObjects())
            foreach (Transform item in userRoot.GetComponentsInChildren<Transform>(true))
                userObjectIds.Add(item.gameObject.GetInstanceID());
        previousEventSystem = EventSystem.current;
        ownedIdentities.Clear();
        runtimeRoots.Clear();
        firstDirtyBoundary = null;
        allocationTrace.Clear();
        allocationFailure = null;
        allocationScenes.Clear();
        factoryObjects.Clear();
        actionAssetId = 0;
        scene = default;
        ownsTestScene = false;
        testSceneHandle = 0;
        testSceneBytes = null;
        try
        {
            OpenActiveTestScene();
            ObjectFactory.componentWasAdded += ObserveFactoryAllocation;
            RecordBoundary("Before fixture root creation");
            // The allocation scene is active BEFORE creating roots or lifecycle components.
            GameObject root = ownedRoot = new GameObject("GameplayPauseMenu");
            RecordAllocation(root.transform); // Before any parenting or scene move.
            AssertBornInFixture(root);
            ownedIdentities[root.GetInstanceID()] = "Original fixture root: " + AuthoredRuntimeFixture.Describe(root);
            root.SetActive(false);
            AuthoredRuntimeFixture.AssertScene(root, scene);
            RecordBoundary("Before component creation");
            controller = root.AddComponent<GameplayPauseMenuController>();
            TrackRuntimeObjects("SetUp root");
            actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/04_Input/PlayerControls.inputactions"));
            actions.name = "PauseOptionsIsolatedInput";
            actionAssetId = actions.GetInstanceID();
            ownedIdentities[actionAssetId] = $"Fixture InputActionAsset '{actions.name}', id={actionAssetId}";
            actions.Disable();
            // A real Editor Escape press must not dispatch into this ownership fixture.
            actions.devices = Array.Empty<InputDevice>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Txt/neodgm_pro.asset");
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.FindProperty("uiFont").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        catch
        {
            // NUnit need not run TearDown after failed SetUp. Restore the user scene
            // and close our Standard Scene even if activation or component setup fails.
            TearDown();
            throw;
        }
    }

    private void OpenActiveTestScene()
    {
        testSceneBytes = File.ReadAllBytes(TestSceneDiskPath);
        Assert.That(SceneManager.GetSceneByPath(TestScenePath).IsValid(), Is.False,
            "The dedicated test scene is already open; refusing to own or close it. " + originalSceneDescription);
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            Assert.That(buildScene.path, Is.Not.EqualTo(TestScenePath), "The fixture scene must not be in Build Settings.");
        bool activated;
        try
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);
            ownsTestScene = scene.IsValid() && scene.path == TestScenePath;
            if (ownsTestScene) testSceneHandle = scene.handle;
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, TestSceneContext());
            Assert.That(scene.path, Is.EqualTo(TestScenePath), TestSceneContext());
            Assert.That(EditorSceneManager.IsPreviewScene(scene), Is.False, TestSceneContext());
            Assert.That(scene.rootCount, Is.Zero, "Dedicated test scene must be empty. " + TestSceneContext());
            activated = SceneManager.SetActiveScene(scene);
        }
        catch (Exception exception)
        {
            // OpenScene may fail after loading. It was absent before this call, so
            // only this exact path can be ours; never claim another loaded scene.
            Scene partiallyOpened = SceneManager.GetSceneByPath(TestScenePath);
            if (partiallyOpened.IsValid())
            {
                scene = partiallyOpened;
                ownsTestScene = true;
                testSceneHandle = scene.handle;
            }
            throw new InvalidOperationException("Cannot open/activate the saved additive test scene; no fallback is allowed. " +
                TestSceneContext(), exception);
        }
        Assert.That(activated, Is.True,
            "Standard test-scene activation was rejected before component creation. " + TestSceneContext());
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle),
            "SetActiveScene did not select the exact Standard Scene. " + TestSceneContext());
    }

    private string TestSceneContext() =>
        $"Expected path='{TestScenePath}'; target={AuthoredRuntimeFixture.Describe(scene)}; " +
        $"original={originalSceneDescription}; current active={AuthoredRuntimeFixture.Describe(SceneManager.GetActiveScene())}";

    private void RestoreUserActiveScene()
    {
        Assert.That(activeScene.IsValid() && activeScene.isLoaded, Is.True,
            "Cannot restore original user scene: " + AuthoredRuntimeFixture.Describe(activeScene));
        if (SceneManager.GetActiveScene().handle != activeScene.handle)
        {
            Assert.That(SceneManager.SetActiveScene(activeScene), Is.True,
                "Failed to restore original active scene: " + AuthoredRuntimeFixture.Describe(activeScene));
        }
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeScene.handle));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (controller != null)
            {
                try { TrackRuntimeObjects("Before teardown"); }
                finally
                {
                    try { Invoke(controller, "OnDisable"); }
                    finally { Invoke(controller, "OnDestroy"); }
                }
            }
        }
        finally
        {
            ObjectFactory.componentWasAdded -= ObserveFactoryAllocation;
            observingAllocations = false;
            try
            {
                if (actions != null) actions.Disable();
                FieldInfo loaded = typeof(InputBindingPersistence).GetField("LoadedAssets", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(loaded, Is.Not.Null, "Track only this fixture's InputBindingPersistence asset registration.");
                ((HashSet<int>)loaded.GetValue(null)).Remove(actionAssetId);
                if (ownedRoot != null)
                {
                    string identity = AuthoredRuntimeFixture.Describe(ownedRoot);
                    Object.DestroyImmediate(ownedRoot);
                    Assert.That(ownedRoot == null, Is.True, "Surviving tracked Pause root: " + identity);
                }
                foreach (GameObject runtimeRoot in runtimeRoots)
                    if (runtimeRoot != null) Object.DestroyImmediate(runtimeRoot);
                foreach (GameObject allocated in factoryObjects)
                    if (allocated != null) Object.DestroyImmediate(allocated);
                factoryObjects.Clear();
                // The dedicated asset was empty on opening; these are exclusively
                // fixture-created roots, including direct Configure failure cases.
                if (ownsTestScene && scene.IsValid() && scene.isLoaded)
                    foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                RecordBoundary("After fixture object destruction");
            }
            finally
            {
                try
                {
                    RestoreUserActiveScene();
                    CloseOwnedTestScene();
                }
                finally
                {
                    ownedRoot = null;
                    if (actions != null) Object.DestroyImmediate(actions);
                    actions = null;
                    controller = null;
                    runtimeRoots.Clear();
                    RecordBoundary("After Standard Scene closure");
                }
            }
        }
        AssertUserSceneAndLifetimeUnchanged();
        Assert.That(activeScene.isDirty, Is.EqualTo(activeSceneWasDirty),
            $"User scene dirty-state changed. First observed boundary: {firstDirtyBoundary ?? "teardown"}; " +
            AuthoredRuntimeFixture.Describe(activeScene));
        Assert.That(firstDirtyBoundary, Is.Null,
            "User scene dirty state changed temporarily at: " + firstDirtyBoundary);
        Assert.That(EventSystem.current, Is.SameAs(previousEventSystem));
        if (testSceneHandle != 0)
            Assert.That(SceneManager.GetSceneByPath(TestScenePath).IsValid(), Is.False,
                $"Fixture scene handle={testSceneHandle} must no longer be loaded.");
        if (testSceneBytes != null)
            CollectionAssert.AreEqual(testSceneBytes, File.ReadAllBytes(TestSceneDiskPath),
                "Serialized test scene changed; never save fixture runs: " + TestScenePath);
    }

    private void CloseOwnedTestScene()
    {
        if (!ownsTestScene) return;
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeScene.handle),
            "Restore the original active scene before closing the test scene. " + TestSceneContext());
        if (scene.IsValid())
        {
            Assert.That(scene.path, Is.EqualTo(TestScenePath), TestSceneContext());
            Assert.That(scene.handle, Is.EqualTo(testSceneHandle), TestSceneContext());
            Assert.That(EditorSceneManager.IsPreviewScene(scene), Is.False, TestSceneContext());
            // CloseScene removes the loaded copy without saving its dirty contents.
            Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True, TestSceneContext());
            Assert.That(scene.IsValid(), Is.False, "Dedicated test scene was not removed.");
        }
        ownsTestScene = false;
        scene = default;
    }

    [Test]
    public void AwakeAndOnEnable_DeferConstructionAndInputUntilStart()
    {
        Invoke(controller, "Awake");
        Assert.That(controller.transform.childCount, Is.Zero);
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.False);
        Invoke(controller, "OnEnable");
        Assert.That(controller.transform.childCount, Is.Zero);
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.False);
        controller.Open();
        Assert.That(controller.IsOpen, Is.False);
        Assert.That(Field<bool>(controller, "ownsPause"), Is.False);

        Invoke(controller, "Start");
        AssertCompleteView();
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.True);
    }

    [Test]
    public void LifecycleCleanup_PreservesCleanOrDirtyUserSceneAndIsIdempotent()
    {
        // Preserve the actual user's clean/dirty state, including an unsaved
        // Untitled scene. All lifecycle allocations use the active Standard Scene.
        // Run this case with both initially clean and initially dirty scenes.
        bool initialDirty = activeSceneWasDirty;
        int initialHandle = activeScene.handle;
        Invoke(controller, "Awake");
        Invoke(controller, "OnEnable");
        Invoke(controller, "Start");
        AssertCompleteView();
        Transform[] original = controller.GetComponentsInChildren<Transform>(true);
        SharedOptionsMenuUI options = Field<SharedOptionsMenuUI>(controller, "sharedOptions");
        Invoke(controller, "Start");
        Invoke(controller, "OnEnable");
        CollectionAssert.AreEquivalent(original, controller.GetComponentsInChildren<Transform>(true));
        Assert.That(BackSubscriberCount(options), Is.EqualTo(1));
        Invoke(controller, "OnDisable");
        Assert.That(BackSubscriberCount(options), Is.Zero);
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.False);

        TearDown();
        TearDown();
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(initialHandle));
        Assert.That(activeScene.isDirty, Is.EqualTo(initialDirty));
        AssertUserSceneAndLifetimeUnchanged();
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void FixtureCleanup_AfterAssertionFailureIsIdempotentAtEveryInitializationBoundary(int phase)
    {
        GameObject trackedRoot = ownedRoot;
        InputActionAsset trackedActions = actions;
        Scene trackedScene = scene;
        const string deliberateFailure = "Deliberate assertion failure exercises the fixture's finally path.";
        AssertionException failure = Assert.Throws<AssertionException>(() =>
        {
            try
            {
                if (phase >= 1)
                {
                    Invoke(controller, "Awake");
                    Invoke(controller, "OnEnable");
                }
                if (phase >= 2) Invoke(controller, "Start");
                Assert.Fail(deliberateFailure);
            }
            finally { TearDown(); }
        });
        // A teardown AssertionException must not masquerade as our stimulus.
        Assert.That(failure.Message, Is.EqualTo(deliberateFailure));
        TearDown();
        Assert.That(trackedRoot == null, Is.True, "The tracked GameplayPauseMenu must be destroyed, not merely deactivated.");
        Assert.That(trackedActions == null, Is.True);
        Assert.That(trackedScene.IsValid(), Is.False);
    }


    [Test]
    public void RuntimePauseOptions_CompleteChainUsesOwnerSceneIncludingDropdowns()
    {
        Invoke(controller, "Awake");
        Invoke(controller, "OnEnable");
        Invoke(controller, "Start");
        AssertCompleteView();
        SharedOptionsMenuUI options = Field<SharedOptionsMenuUI>(controller, "sharedOptions");
        Assert.That(options.HasAuthoredLayout, Is.True, "The runtime Pause builder completes its control bindings.");
        foreach (Transform child in controller.GetComponentsInChildren<Transform>(true))
        {
            Assert.That(child.gameObject.scene, Is.EqualTo(scene), child.name);
        }
    }

    [Test]
    public void RepeatedStartAndEnable_PreserveHierarchyAndOneBackSubscription()
    {
        Invoke(controller, "Awake");
        Invoke(controller, "Start");
        Transform[] original = controller.GetComponentsInChildren<Transform>(true);
        SharedOptionsMenuUI options = Field<SharedOptionsMenuUI>(controller, "sharedOptions");
        Invoke(controller, "Start");
        Invoke(controller, "OnEnable");
        Invoke(controller, "OnEnable");
        ConfigureOptions(options);
        ConfigureOptions(options);
        CollectionAssert.AreEquivalent(original, controller.GetComponentsInChildren<Transform>(true));
        Assert.That(BackSubscriberCount(options), Is.EqualTo(1));
        Invoke(controller, "OnDisable");
        Assert.That(BackSubscriberCount(options), Is.Zero);
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.False);
        Invoke(controller, "OnEnable");
        Assert.That(BackSubscriberCount(options), Is.EqualTo(1));
        CollectionAssert.AreEquivalent(original, controller.GetComponentsInChildren<Transform>(true));
    }

    [Test]
    public void CancelActionAlreadyEnabled_IsNotDisabledByMenuShutdown()
    {
        InputAction cancel = actions.FindAction("UI/Cancel");
        cancel.Enable();
        Invoke(controller, "Awake");
        Invoke(controller, "Start");
        Invoke(controller, "OnEnable");
        Invoke(controller, "OnDisable");
        Assert.That(cancel.enabled, Is.True);
    }

    [Test]
    public void SceneExitBeforeStart_HasNoCreatedViewCallbacksOrInputOwnership()
    {
        Invoke(controller, "Awake");
        Invoke(controller, "OnEnable");
        Invoke(controller, "OnDisable");
        Invoke(controller, "OnDestroy");
        Assert.That(controller.transform.childCount, Is.Zero);
        Assert.That(Field<InputAction>(controller, "cancelAction"), Is.Null);
        Assert.That(Field<bool>(controller, "listenersBound"), Is.False);
        Assert.That(Field<bool>(controller, "ownsPause"), Is.False);
        RestoreUserActiveScene();
        CloseOwnedTestScene();
        Assert.That(actions.FindAction("UI/Cancel").enabled, Is.False);
        // No application/sceneLoaded event or delayed callback is registered: Unity
        // owns and cancels Start for the destroyed instance.
    }

    [Test]
    public void DisableBeforeStart_CanInitializeOnceWhenEnabledAgain()
    {
        Invoke(controller, "Awake");
        Invoke(controller, "OnEnable");
        Invoke(controller, "OnDisable");
        Invoke(controller, "OnEnable");
        Assert.That(controller.transform.childCount, Is.Zero);
        Invoke(controller, "Start");
        Invoke(controller, "OnEnable");
        AssertCompleteView();
    }

    [Test]
    public void AuthoredOptions_RuntimeConfigurePreservesRenamedObjectsAndVisuals()
    {
        Scene authored = AuthoredRuntimeFixture.Open("Boot");
        try
        {
        SharedOptionsMenuUI options = AuthoredRuntimeFixture.Single<SharedOptionsMenuUI>(authored);
        GameObject root = options.gameObject;
        root.SetActive(false);
        Assert.That(options.HasAuthoredLayout, Is.True);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        TextMeshProUGUI title = root.transform.Find("OptionsTitle").GetComponent<TextMeshProUGUI>();
        title.name = "AuthoredSettingsHeading";
        title.fontSize = 21f;
        title.color = new Color(0.3f, 0.6f, 0.8f, 0.42f);
        title.rectTransform.anchoredPosition = new Vector2(8f, 70f);
        string before = EditorJsonUtility.ToJson(title);
        ConfigureOptions(options);
        ConfigureOptions(options);
        Assert.That(EditorJsonUtility.ToJson(title), Is.EqualTo(before));
        CollectionAssert.AreEquivalent(children, root.GetComponentsInChildren<Transform>(true));
        Assert.That(root.activeSelf, Is.False);
        foreach (Transform child in children) Assert.That(child.gameObject.scene, Is.EqualTo(authored));
        }
        finally { AuthoredRuntimeFixture.Close(authored); }
    }

    [Test]
    public void MissingRuntimeParent_FailsBeforeAllocatingInTheUserScene()
    {
        MethodInfo create = typeof(SharedOptionsMenuUI).GetMethod("CreateRuntimeChild",
            BindingFlags.Static | BindingFlags.NonPublic);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
            create.Invoke(null, new object[] { "OptionsTitle", null }));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        CollectionAssert.AreEqual(activeRootIds, AuthoredRuntimeFixture.RootIds(activeScene));
    }

    [Test]
    public void FailedOptionsBuild_RollsBackOnlyAttemptObjectsAndCanRetry()
    {
        GameObject root = new GameObject("OptionsPanel", typeof(RectTransform));
        RecordAllocation(root.transform);
        AssertBornInFixture(root);
        root.SetActive(false);
        SharedOptionsMenuUI options = root.AddComponent<SharedOptionsMenuUI>();
        // A supported component conflict fails late in the real options builder, after
        // controls/dropdowns exist. No scene flags or component serialization are forged.
        SettingsMenuTabController conflict = root.AddComponent<SettingsMenuTabController>();
        GameObject preserved = new GameObject("UnrelatedChild");
        RecordAllocation(preserved.transform);
        AssertBornInFixture(preserved);
        preserved.transform.SetParent(root.transform, false);
        LogAssert.NoUnexpectedReceived();
        Assert.Throws<NullReferenceException>(() =>
        {
            const string conflictMessage = "Can't add 'SettingsMenuTabController' to OptionsPanel because a 'SettingsMenuTabController' is already added to the game object!";
            LogAssert.Expect(LogType.Log, conflictMessage);
            ConfigureOptions(options);
        });
        LogAssert.NoUnexpectedReceived();
        Assert.That(options.HasAuthoredLayout, Is.False);
        Assert.That(root.transform.childCount, Is.EqualTo(1));
        Assert.That(root.transform.GetChild(0).gameObject, Is.SameAs(preserved));
        Assert.That(root.GetComponent<SettingsMenuTabController>(), Is.SameAs(conflict));
        Object.DestroyImmediate(conflict);
        ConfigureOptions(options);
        Assert.That(options.HasAuthoredLayout, Is.True);
        Assert.That(root.GetComponents<SettingsMenuTabController>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<SettlementSettingsPanel>().Length, Is.EqualTo(1));
        Assert.That(root.transform.Find("OptionsTitle"), Is.Not.Null);
        Assert.That(preserved.transform.parent, Is.SameAs(root.transform));
        Transform[] completed = root.GetComponentsInChildren<Transform>(true);
        ConfigureOptions(options);
        ConfigureOptions(options);
        CollectionAssert.AreEquivalent(completed, root.GetComponentsInChildren<Transform>(true));
    }

    private void AssertCompleteView()
    {
        Assert.That(controller.transform.childCount, Is.EqualTo(1));
        Transform canvas = controller.transform.GetChild(0);
        AssertBornInFixture(canvas.gameObject);
        Assert.That(canvas.name, Is.EqualTo("Canvas_PauseMenu"));
        Assert.That(canvas.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(480f, 270f)));
        Transform options = canvas.Find("OptionsPanel");
        Assert.That(options.Find("OptionsTitle").GetComponent<TextMeshProUGUI>().font, Is.SameAs(font));
        SharedOptionsMenuUI shared = options.GetComponent<SharedOptionsMenuUI>();
        Assert.That(shared.HasAuthoredLayout, Is.True);
        Assert.That(shared.BackButton, Is.Not.Null);
        Assert.That(shared.SettingsPanel, Is.Not.Null);
        Assert.That(shared.TabController, Is.Not.Null);
        Assert.That(shared.DisplayConfirmationRoot, Is.Not.Null);
        TMP_Dropdown[] dropdowns = options.GetComponentsInChildren<TMP_Dropdown>(true);
        Assert.That(dropdowns.Length, Is.EqualTo(3));
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            AssertBornInFixture(dropdown.gameObject);
            foreach (Transform child in dropdown.GetComponentsInChildren<Transform>(true))
                AssertBornInFixture(child.gameObject);
        }
        Assert.That(options.GetComponentsInChildren<Slider>(true).Length, Is.GreaterThanOrEqualTo(7));
        Assert.That(options.GetComponentsInChildren<InputRebindButtonUI>(true).Length, Is.EqualTo(18));
        Assert.That(canvas.Find("QuitConfirmationRoot/ConfirmationPanel/ConfirmQuitButton"), Is.Not.Null);
        Assert.That(canvas.gameObject.activeSelf, Is.False);
    }

    private static int BackSubscriberCount(SharedOptionsMenuUI options) =>
        Field<Action>(options, "BackRequested")?.GetInvocationList().Length ?? 0;

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);

    private void Invoke(object owner, string name)
    {
        if (name == "Awake" || name == "OnEnable" || name == "Start")
        {
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle),
                "Lifecycle construction requires the active dedicated Standard Scene: " + AuthoredRuntimeFixture.Describe(scene));
        }
        RunWithAllocationChecks(name, () => owner.GetType().GetMethod(name, PrivateInstance).Invoke(owner, null));
    }

    private void ConfigureOptions(SharedOptionsMenuUI options)
    {
        RunWithAllocationChecks("Options.Configure", () => options.Configure(actions, null, font));
    }

    private void RunWithAllocationChecks(string boundary, Action action)
    {
        RecordBoundary("Before " + boundary);
        observingAllocations = true;
        try { action(); }
        finally
        {
            observingAllocations = false;
            RecordBoundary("After " + boundary);
            TrackRuntimeObjects(boundary);
            // Check immutable first-observation results, not a later moved object's
            // scene. Report outside the native ObjectFactory callback boundary.
            AssertAllocationOwnership();
        }
    }

    private void ObserveFactoryAllocation(Component component)
    {
        if (!observingAllocations || component == null) return;
        RecordAllocation(component);
    }

    private void RecordAllocation(Component component)
    {
        GameObject item = component.gameObject;
        int id = item.GetInstanceID();
        RecordBoundary("Factory added " + component.GetType().Name + ": " + AuthoredRuntimeFixture.Describe(item));
        if (userObjectIds.Contains(id))
        {
            allocationFailure = allocationFailure ?? "Factory touched a pre-existing user object: " + AuthoredRuntimeFixture.Describe(item);
            return; // Never claim or destroy a pre-existing user object.
        }
        if (!allocationScenes.ContainsKey(id))
        {
            allocationScenes.Add(id, item.scene.handle);
            factoryObjects.Add(item);
            string first = AuthoredRuntimeFixture.Describe(item) + $", firstComponent={component.GetType().Name}, " +
                $"hideFlags={item.hideFlags}, active={AuthoredRuntimeFixture.Describe(SceneManager.GetActiveScene())}";
            allocationTrace.Add(first);
            if (!item.scene.IsValid() || !item.scene.isLoaded ||
                item.scene.handle != scene.handle || item.scene.path != TestScenePath ||
                EditorSceneManager.IsPreviewScene(item.scene))
            {
                allocationFailure = allocationFailure ?? "Wrong first-allocation ownership: " + first;
            }
        }
        string identity = AuthoredRuntimeFixture.Describe(item) + $", component={component.GetType().FullName}, " +
            $"componentId={component.GetInstanceID()}, hideFlags={item.hideFlags}/{component.hideFlags}, bornScene={allocationScenes[id]}";
        ownedIdentities[id] = identity;
        ownedIdentities[component.GetInstanceID()] = identity;
    }

    private void AssertAllocationOwnership()
    {
        if (allocationFailure != null)
        {
            Assert.Fail(allocationFailure + "\nExpected " + AuthoredRuntimeFixture.Describe(scene) +
                "\nFirst-allocation trace:\n" + string.Join("\n", allocationTrace));
        }
    }

    private void AssertBornInFixture(GameObject item)
    {
        AssertAllocationOwnership();
        Assert.That(allocationScenes.TryGetValue(item.GetInstanceID(), out int bornScene), Is.True,
            "No first-allocation observation for " + AuthoredRuntimeFixture.Describe(item));
        Assert.That(bornScene, Is.EqualTo(scene.handle),
            "Moving an object later is insufficient: " + AuthoredRuntimeFixture.Describe(item));
        AuthoredRuntimeFixture.AssertScene(item, scene);
    }

    private void RecordBoundary(string boundary)
    {
        if (firstDirtyBoundary == null && activeScene.IsValid() && activeScene.isDirty != activeSceneWasDirty)
            firstDirtyBoundary = boundary;
    }

    private void AssertUserSceneAndLifetimeUnchanged()
    {
        Assert.That(activeScene.IsValid() && activeScene.isLoaded, Is.True, "Original user scene is no longer loaded.");
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeScene.handle), "Original active scene was not restored.");
        CollectionAssert.AreEqual(activeRootIds, AuthoredRuntimeFixture.RootIds(activeScene));
        foreach (KeyValuePair<int, string> tracked in ownedIdentities)
            Assert.That(EditorUtility.InstanceIDToObject(tracked.Key) == null, Is.True,
                "Exact fixture instance still resolves after cleanup: " + tracked.Value);
    }

    private void TrackRuntimeObjects(string boundary)
    {
        if (firstDirtyBoundary == null && activeScene.isDirty != activeSceneWasDirty)
            firstDirtyBoundary = boundary;
        if (controller == null) return;
        GameObject canvas = Field<GameObject>(controller, "canvasRoot");
        if (canvas != null)
        {
            AuthoredRuntimeFixture.AssertScene(canvas, scene);
            GameObject actualRoot = canvas.transform.root.gameObject;
            if (actualRoot != ownedRoot && !runtimeRoots.Contains(actualRoot)) runtimeRoots.Add(actualRoot);
        }
        TrackComponents(controller.gameObject);
        foreach (GameObject root in scene.GetRootGameObjects()) TrackComponents(root);
        foreach (GameObject runtimeRoot in runtimeRoots)
            if (runtimeRoot != null) TrackComponents(runtimeRoot);

        void TrackComponents(GameObject trackedRoot)
        {
            AuthoredRuntimeFixture.AssertScene(trackedRoot, scene);
            foreach (Component component in trackedRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                GameObject item = component.gameObject;
                string role = item == ownedRoot ? "original fixture root" : "runtime-created presentation";
                string identity = AuthoredRuntimeFixture.Describe(item) +
                    $", component={component.GetType().FullName}, componentId={component.GetInstanceID()}, " +
                    $"parent={(item.transform.parent != null ? AuthoredRuntimeFixture.Describe(item.transform.parent.gameObject) : "none")}, " +
                    $"hideFlags={item.hideFlags}/{component.hideFlags}, role={role}, boundary={boundary}";
                ownedIdentities[item.GetInstanceID()] = identity;
                ownedIdentities[component.GetInstanceID()] = identity;
            }
        }
    }
}
