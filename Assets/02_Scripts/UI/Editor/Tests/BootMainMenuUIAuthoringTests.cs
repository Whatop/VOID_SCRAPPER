using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BootMainMenuUIAuthoringTests
{
    private const string SpaceBackgroundScriptPath =
        "Assets/02_Scripts/UI/MainMenuSpaceBackground.cs";
    private const string ButtonPresenterScriptPath =
        "Assets/02_Scripts/UI/MainMenuButtonPresenter.cs";

    private readonly List<Object> ownedObjects = new List<Object>();
    private Scene previousScene;
    private Scene testScene;
    private MainMenuController controller;
    private EventSystem previousEventSystem;

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        previousEventSystem = EventSystem.current;
        testScene = EditorSceneManager.NewPreviewScene();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (testScene.IsValid() && testScene.isLoaded) AuthoredRuntimeFixture.Close(testScene);
        }
        finally
        {
            testScene = default;
            for (int i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null && !EditorUtility.IsPersistent(ownedObjects[i]))
                    Object.DestroyImmediate(ownedObjects[i]);
            }
            ownedObjects.Clear();
        }
        Assert.That(
            EventSystem.current,
            Is.SameAs(previousEventSystem),
            "Fixture teardown must preserve the user's EventSystem authority.");
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(previousScene.handle));
    }

    [Test]
    public void MainMenuSpaceBackground_HasPersistentMonoScriptIdentity()
    {
        GameObject target = CreateTestGameObject("BackgroundIdentity", false);
        MainMenuSpaceBackground background = target.AddComponent<MainMenuSpaceBackground>();

        MonoScript script = MonoScript.FromMonoBehaviour(background);
        Assert.That(script, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(script), Is.EqualTo(SpaceBackgroundScriptPath));
        Assert.That(script.GetClass(), Is.EqualTo(typeof(MainMenuSpaceBackground)));
        Assert.That(
            script.GetClass().Assembly,
            Is.SameAs(typeof(MainMenuController).Assembly));
        Assert.That(EditorUtility.IsPersistent(script), Is.True);
        Assert.That(AssetDatabase.AssetPathToGUID(SpaceBackgroundScriptPath), Is.Not.Empty);
    }

    [Test]
    public void MainMenuButtonPresenter_HasPersistentMonoScriptIdentity()
    {
        GameObject target = CreateTestGameObject("ButtonPresenterIdentity", false);
        MainMenuButtonPresenter presenter = target.AddComponent<MainMenuButtonPresenter>();

        MonoScript script = MonoScript.FromMonoBehaviour(presenter);
        Assert.That(script, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(script), Is.EqualTo(ButtonPresenterScriptPath));
        Assert.That(script.GetClass(), Is.EqualTo(typeof(MainMenuButtonPresenter)));
        Assert.That(
            script.GetClass().Assembly,
            Is.SameAs(typeof(MainMenuController).Assembly));
        Assert.That(EditorUtility.IsPersistent(script), Is.True);
        Assert.That(AssetDatabase.AssetPathToGUID(ButtonPresenterScriptPath), Is.Not.Empty);
    }

    [Test]
    public void RuntimeController_HasNoVisualBuilderAndRetainsBehaviorMethods()
    {
        System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;
        Assert.That(typeof(MainMenuController).GetMethod("BuildRuntimeView", flags), Is.Null);
        Assert.That(
            typeof(MainMenuController).GetField("optionsPanel", flags),
            Is.Null,
            "The authored SharedOptions binding replaces the legacy options-root fallback.");
        Assert.That(typeof(MainMenuController).GetMethod("ContinueGame", flags), Is.Not.Null);
        Assert.That(typeof(MainMenuController).GetMethod("RequestNewGame", flags), Is.Not.Null);
        Assert.That(typeof(MainMenuController).GetMethod("OpenOptions", flags), Is.Not.Null);
        Assert.That(typeof(MainMenuController).GetMethod("QuitGame", flags), Is.Not.Null);
        Assert.That(typeof(MainMenuController).GetMethod("RefreshContinueState", flags), Is.Not.Null);

        string sourcePath = Path.Combine(
            Application.dataPath,
            "02_Scripts/UI/MainMenuController.cs");
        string controllerSource = File.ReadAllText(sourcePath);
        Assert.That(controllerSource, Does.Not.Contain("new GameObject"));
        Assert.That(
            controllerSource,
            Does.Not.Contain("public sealed class MainMenuButtonPresenter"),
            "The authored presenter must retain its own persistent MonoScript identity.");
        Assert.That(controllerSource, Does.Contain("bootstrap.HasUsableProgression"));
        Assert.That(controllerSource, Does.Contain("sceneFlowManager.LoadTutorial()"));
        Assert.That(controllerSource, Does.Contain("sceneFlowManager.LoadSettlement()"));

        string backgroundSourcePath = Path.Combine(
            Application.dataPath,
            "02_Scripts/UI/MainMenuSpaceBackground.cs");
        string backgroundSource = File.ReadAllText(backgroundSourcePath);
        int backgroundStart = backgroundSource.IndexOf(
            "public sealed class MainMenuSpaceBackground",
            System.StringComparison.Ordinal);
        Assert.That(backgroundStart, Is.GreaterThan(0));
        string backgroundRuntimeSource = RemoveEditorOnlyBlocks(
            backgroundSource.Substring(backgroundStart));
        Assert.That(backgroundRuntimeSource, Does.Not.Contain("new GameObject"));
    }


    [Test]
    public void SavedBoot_OptionsHaveCanonicalDeepBindingsAndPreserveAuthoredValues()
    {
        OpenAuthoredBoot();
        BootMainMenuView view = AuthoredRuntimeFixture.Single<BootMainMenuView>(testScene);
        SharedOptionsMenuUI options = view.SharedOptions;
        Transform[] transforms = options.GetComponentsInChildren<Transform>(true);
        string[] before = System.Array.ConvertAll(transforms, t => EditorJsonUtility.ToJson(t));
        Assert.That(view.OptionsRoot, Is.SameAs(view.transform.Find("SafeArea/OptionsPanel").gameObject));
        Assert.That(options.gameObject, Is.SameAs(view.OptionsRoot));
        Assert.That(options.TabController, Is.SameAs(view.OptionsRoot.GetComponent<SettingsMenuTabController>()));
        Assert.That(options.SettingsPanel, Is.SameAs(view.OptionsRoot.GetComponent<SettlementSettingsPanel>()));
        Assert.That(view.TryGetMissingRuntimeReference(out string error), Is.True, error);
        Assert.That(options.TryValidateAuthoredLayout(out error), Is.True, error);
        var tabs = new SerializedObject(options.TabController);
        Assert.That(tabs.FindProperty("tabRoots").arraySize, Is.EqualTo(4));
        Assert.That(tabs.FindProperty("tabButtons").arraySize, Is.EqualTo(4));
        Assert.That(options.TabController.RebindRows.Count, Is.EqualTo(18));
        Assert.That(new SerializedObject(options.SettingsPanel).FindProperty("rebindRows").arraySize, Is.EqualTo(18));
        Assert.That(options.GetComponentsInChildren<InputRebindButtonUI>(true).Length, Is.EqualTo(18));
        Assert.That(options.TabController.DropdownGuards.Count, Is.EqualTo(3));
        string[] names = { "ResolutionDropdown", "FullscreenDropdown", "FrameLimitDropdown" };
        for (int i = 0; i < names.Length; i++)
            Assert.That(options.TabController.DropdownGuards[i].name, Is.EqualTo(names[i]));
        Assert.That(System.Array.ConvertAll(transforms, t => EditorJsonUtility.ToJson(t)), Is.EqualTo(before));
    }

    [TestCase("tabRoots", 0)]
    [TestCase("tabButtons", 1)]
    [TestCase("rebindRows", 17)]
    [TestCase("dropdowns", 0)]
    public void BootOptions_StaleInternalTabReferencesFailDeepValidation(string field, int index)
    {
        OpenAuthoredBoot();
        BootMainMenuView view = AuthoredRuntimeFixture.Single<BootMainMenuView>(testScene);
        var data = new SerializedObject(view.SharedOptions.TabController);
        data.FindProperty(field).GetArrayElementAtIndex(index).objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(view.SharedOptions.HasAuthoredLayout, Is.True, "Shallow valid references are insufficient.");
        Assert.That(view.TryGetMissingRuntimeReference(out string error), Is.False);
        Assert.That(error, Does.Contain(field));
    }

    [TestCase("masterVolumeSlider")]
    [TestCase("showHudHintsToggle")]
    [TestCase("resetAllBindingsButton")]
    [TestCase("resolutionDropdown")]
    [TestCase("confirmScreenButton")]
    public void BootOptions_StaleSettingsControlsFailDeepValidation(string field)
    {
        OpenAuthoredBoot();
        SharedOptionsMenuUI options = AuthoredRuntimeFixture.Single<BootMainMenuView>(testScene).SharedOptions;
        var data = new SerializedObject(options.SettingsPanel);
        data.FindProperty(field).objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(options.TryValidateAuthoredLayout(out string error), Is.False);
        Assert.That(error, Does.Contain(field));
    }

    [Test]
    public void BootOptions_InvalidAuthoredConfigurationNeverBuildsOrReplacesObjects()
    {
        OpenAuthoredBoot();
        SharedOptionsMenuUI options = AuthoredRuntimeFixture.Single<BootMainMenuView>(testScene).SharedOptions;
        var data = new SerializedObject(options);
        data.FindProperty("built").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        Transform[] before = options.GetComponentsInChildren<Transform>(true);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("Authored Boot options are invalid"));
        Assert.That(options.ConfigureAuthored(null, null, null), Is.False);
        Assert.That(options.GetComponentsInChildren<Transform>(true), Is.EqualTo(before));
        Assert.That(new SerializedObject(options).FindProperty("built").boolValue, Is.False);
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "02_Scripts/UI/MainMenuController.cs"));
        Assert.That(source, Does.Contain("SharedOptions.ConfigureAuthored("));
        Assert.That(source, Does.Not.Contain("SharedOptions.Configure("));
    }

    [Test]
    public void BootOptions_FirstOpenSurvivesDeferredAwake_AndTabsRetainContentAcrossReopens()
    {
        OpenAuthoredBoot();
        SharedOptionsMenuUI options = AuthoredRuntimeFixture.Single<BootMainMenuView>(testScene).SharedOptions;
        SettlementSettingsPanel settings = options.SettingsPanel;
        // This fixture exercises the activation contract without applying display/audio
        // preferences to the user's Editor. Actual initialization is covered in Play Mode.
        SetPrivate(settings, "initialized", true);
        Transform[] before = options.GetComponentsInChildren<Transform>(true);
        settings.Open();
        CallPrivate(settings, "Awake");
        Assert.That(settings.IsOpen, Is.True, "Awake must not undo an accepted first Open.");
        var tabData = new SerializedObject(options.TabController);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            settings.Open();
            for (int tab = 0; tab < 4; tab++)
            {
                options.TabController.ShowTab(tab);
                for (int i = 0; i < 4; i++)
                {
                    var root = (GameObject)tabData.FindProperty("tabRoots").GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(root.activeSelf, Is.EqualTo(i == tab));
                    if (i == tab) Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Length, Is.GreaterThan(0));
                }
            }
            settings.Close();
            Assert.That(settings.IsOpen, Is.False);
        }
        Assert.That(options.GetComponentsInChildren<Transform>(true), Is.EqualTo(before));
        Assert.That(options.TryValidateAuthoredLayout(out string error), Is.True, error);
    }

    [Test]
    public void RunResult_SavedPresentationIsCompleteAndRuntimeContainsNoHierarchyBuilder()
    {
        OpenAuthoredBoot();
        RunResultPanelUI result = AuthoredRuntimeFixture.Single<RunResultPanelUI>(testScene);
        Assert.That(result.TryValidateAuthoredPresentation(out string error), Is.True, error);
        Assert.That(result.transform.root.GetComponent<GameBootstrap>(), Is.Not.Null);
        Assert.That(ReadPrivate<ResourceCounterUI[]>(result, "resourceRows").Length,
            Is.GreaterThanOrEqualTo(System.Enum.GetValues(typeof(CurrencyType)).Length * 2));
        string runtime = RemoveEditorOnlyBlocks(File.ReadAllText(Path.Combine(Application.dataPath,
            "02_Scripts/Core/Bootstrap/RunResultPanelUI.cs")));
        foreach (string mutation in new[] { "new GameObject", "Instantiate(", ".SetParent(", "DontDestroyOnLoad(", "CreateRuntimePanel", "CreateSceneOwnedRuntimeObject" })
            Assert.That(runtime, Does.Not.Contain(mutation));
        Assert.That(runtime, Does.Contain("RunEnded += HandleRunEnded"));
        Assert.That(runtime, Does.Contain("continueButton.onClick.AddListener(Close)"));
        Assert.That(runtime, Does.Contain("SceneFlowManager.Instance.LoadSettlement()"));
        Assert.That(runtime, Does.Contain("ReleaseRunEndingPresentationOwnership()"));
    }

    [TestCase("resultCard")]
    [TestCase("continueButton")]
    [TestCase("detailText")]
    public void RunResult_MissingAuthoredReferenceFailsWithoutAllocating(string field)
    {
        OpenAuthoredBoot();
        RunResultPanelUI result = AuthoredRuntimeFixture.Single<RunResultPanelUI>(testScene);
        var data = new SerializedObject(result);
        data.FindProperty(field).objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Transform[] before = result.GetComponentsInChildren<Transform>(true);
        Assert.That(result.TryValidateAuthoredPresentation(out string error), Is.False);
        Assert.That(error, Does.Contain(field));
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("RunResultPanelUI requires the authored Boot/CoreRoot presentation"));
        Assert.That(CallPrivate(result, "EnsurePresentationLayout"), Is.False);
        Assert.That(CallPrivate(result, "EnsurePresentationLayout"), Is.False, "Repeated validation does not rebuild or repeat the error.");
        Assert.That(result.GetComponentsInChildren<Transform>(true), Is.EqualTo(before));
    }

    [TestCase(RunEndReason.SafeReturn, 11, 0)]
    [TestCase(RunEndReason.Death, 0, 11)]
    [TestCase(RunEndReason.FinalVictory, 11, 0)]
    public void RunResult_AuthoredRowsRenderCommittedAndLostAmountsWithoutReparenting(RunEndReason reason, int committed, int lost)
    {
        OpenAuthoredBoot();
        RunResultPanelUI result = AuthoredRuntimeFixture.Single<RunResultPanelUI>(testScene);
        Transform[] before = result.GetComponentsInChildren<Transform>(true);
        Transform[] parents = System.Array.ConvertAll(before, t => t.parent);
        var data = new RunResultData { endReason = reason };
        data.settledResources.Add(new RunSettlementResourceResult(CurrencyType.ScrapParts, 11, committed, lost));
        CallPrivate(result, "RefreshSettlementResourceRows", data);
        var rows = ReadPrivate<ResourceCounterUI[]>(result, "resourceRows");
        Assert.That(rows[0].Amount, Is.EqualTo(11));
        Assert.That(rows[0].gameObject.activeSelf, Is.True);
        Assert.That(AuthoredRuntimeFixture.Read<TextMeshProUGUI>(rows[0], "amountText").text,
            Is.EqualTo(lost > 0 ? "-11" : "+11"));
        for (int i = 1; i < rows.Length; i++) Assert.That(rows[i].gameObject.activeSelf, Is.False);
        Assert.That((string)CallPrivate(result, "GetReasonText", reason), Is.Not.Empty);
        Assert.That((string)CallPrivate(result, "BuildSummaryText", data), Is.Not.Empty);
        CallPrivate(result, "HideImmediate");
        CallPrivate(result, "HideImmediate");
        CallPrivate(result, "UnbindRuntimeCallbacks");
        CallPrivate(result, "UnbindRuntimeCallbacks");
        Assert.That(AuthoredRuntimeFixture.Read<GameObject>(result, "panelRoot").activeSelf, Is.False);
        Assert.That(result.GetComponentsInChildren<Transform>(true), Is.EqualTo(before));
        Assert.That(System.Array.ConvertAll(before, t => t.parent), Is.EqualTo(parents));
    }

    private void OpenAuthoredBoot()
    {
        AuthoredRuntimeFixture.Close(testScene);
        testScene = AuthoredRuntimeFixture.Open("Boot");
    }

    [Test]
    public void SettlementToBootUnload_NavigationPointerCleanupToleratesDestroyedOwner()
    {
        GameObject ownerRoot = CreateTestGameObject("UnloadingSettlementOwner", false);
        SettlementUIController owner = ownerRoot.AddComponent<SettlementUIController>();
        GameObject pointerRoot = CreateTestGameObject("UnloadingNavigationPointer", false);
        var pointer = pointerRoot.AddComponent<SettlementPrimaryNavigationPointer>();
        pointer.Configure(owner, 0);
        Object.DestroyImmediate(ownerRoot);
        Assert.DoesNotThrow(() => CallPrivate(pointer, "OnDisable"));
        Assert.DoesNotThrow(() => CallPrivate(pointer, "OnDisable"));
    }

    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private static object CallPrivate(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static T ReadPrivate<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    private static string RemoveEditorOnlyBlocks(string source)
    {
        const string startToken = "#if UNITY_EDITOR";
        const string endToken = "#endif";
        while (true)
        {
            int start = source.IndexOf(startToken, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return source;
            }

            int end = source.IndexOf(endToken, start, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            source = source.Remove(start, end + endToken.Length - start);
        }
    }

    private T[] FindInTestScene<T>() where T : Component
    {
        List<T> results = new List<T>();
        GameObject[] roots = testScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            results.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }

        return results.ToArray();
    }

    private T Track<T>(T target) where T : Object
    {
        ownedObjects.Add(target);
        return target;
    }

    private GameObject CreateTestGameObject(string objectName, bool active, params System.Type[] componentTypes)
    {
        GameObject target = Track(AuthoredRuntimeFixture.Create(testScene, null, objectName,
            System.Array.IndexOf(componentTypes, typeof(RectTransform)) >= 0));
        target.SetActive(false);
        foreach (System.Type type in componentTypes)
            if (type != typeof(RectTransform) && type != typeof(Transform)) target.AddComponent(type);
        target.SetActive(active);
        return target;
    }

}
