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
        Assert.That(
            SceneManager.GetActiveScene(),
            Is.EqualTo(previousScene),
            "Creating the isolated preview fixture must not replace the user's active scene.");

        GameObject controllerObject = CreateTestGameObject("MainMenuRuntime", false);
        controller = controllerObject.AddComponent<MainMenuController>();
        ConfigureControllerAssets(controller);

        GameObject eventSystemObject = CreateTestGameObject("EventSystem", false);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
        Assert.That(
            EventSystem.current,
            Is.SameAs(previousEventSystem),
            "The inactive preview fixture EventSystem must not replace the user's EventSystem.");

        GameObject faderObject = CreateTestGameObject("ScreenFaderCanvas", false);
        faderObject.AddComponent<CanvasGroup>();
        faderObject.AddComponent<ScreenFader>();
    }

    [TearDown]
    public void TearDown()
    {
        if (previousScene.IsValid() &&
            previousScene.isLoaded &&
            SceneManager.GetActiveScene() != previousScene)
        {
            SceneManager.SetActiveScene(previousScene);
        }

        if (testScene.IsValid() && testScene.isLoaded)
        {
            EditorSceneManager.ClosePreviewScene(testScene);
        }

        for (int i = ownedObjects.Count - 1; i >= 0; i--)
        {
            if (ownedObjects[i] != null && !EditorUtility.IsPersistent(ownedObjects[i]))
            {
                Object.DestroyImmediate(ownedObjects[i]);
            }
        }

        ownedObjects.Clear();
        Assert.That(
            EventSystem.current,
            Is.SameAs(previousEventSystem),
            "Fixture teardown must preserve the user's EventSystem authority.");
    }

    [Test]
    public void Installer_CreatesAuthoredHierarchyAndSerializedBindings()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        Assert.That(view.MenuRoot, Is.Not.Null);
        Assert.That(view.TitleRoot, Is.Not.Null);
        Assert.That(view.ContinueButton, Is.Not.Null);
        Assert.That(view.NewGameButton, Is.Not.Null);
        Assert.That(view.SettingsButton, Is.Not.Null);
        Assert.That(view.ExitButton, Is.Not.Null);
        Assert.That(view.SharedOptions.HasAuthoredLayout, Is.True);
        Assert.That(view.BackgroundRoot, Is.Not.Null);
        Assert.That(view.SpaceBackground, Is.Not.Null);
        Assert.That(view.SpaceBackground.HasAuthoredVisuals, Is.True);
        Assert.That(
            view.SpaceBackground.AuthoredStars.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredStarCount));
        Assert.That(
            view.SpaceBackground.AuthoredAsteroids.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredAsteroidCount));
        Assert.That(view.SpaceBackground.CursePasserRect, Is.Not.Null);
        Assert.That(
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(view.BackgroundRoot.gameObject),
            Is.Zero);
        Assert.That(controller.AuthoredView, Is.SameAs(view));
        AssertContinueBindingComplete(view);
        Assert.That(view.GetComponentsInChildren<Button>(true).Length, Is.GreaterThan(4));
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
    public void Installer_ReusesCanvasAndEventSystemAndIsIdempotent()
    {
        Assert.That(Install(out BootMainMenuView first), Is.True);
        int buttonCount = first.GetComponentsInChildren<Button>(true).Length;
        EventSystem eventSystem = FindInTestScene<EventSystem>()[0];

        Assert.That(Install(out BootMainMenuView second), Is.True);
        Assert.That(second, Is.SameAs(first));
        Assert.That(FindInTestScene<BootMainMenuView>().Length, Is.EqualTo(1));
        Assert.That(FindInTestScene<Canvas>().Length, Is.EqualTo(1));
        Assert.That(FindInTestScene<EventSystem>().Length, Is.EqualTo(1));
        Assert.That(FindInTestScene<EventSystem>()[0], Is.SameAs(eventSystem));
        Assert.That(second.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(buttonCount));
        Assert.That(second.ContinueButton.onClick.GetPersistentEventCount(), Is.Zero);
    }

    [Test]
    public void Installer_RerunPreservesManualRectAndColorChanges()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        RectTransform menuRect = view.MenuRoot.transform as RectTransform;
        Image buttonImage = view.NewGameButton.targetGraphic as Image;
        Vector2 authoredPosition = new Vector2(-101f, 7f);
        Color authoredColor = new Color(0.42f, 0.18f, 0.64f, 0.73f);
        menuRect.anchoredPosition = authoredPosition;
        buttonImage.color = authoredColor;

        Assert.That(Install(out BootMainMenuView rerun), Is.True);
        Assert.That(rerun.MenuRoot.transform, Is.SameAs(menuRect));
        Assert.That(menuRect.anchoredPosition, Is.EqualTo(authoredPosition));
        Assert.That(buttonImage.color, Is.EqualTo(authoredColor));
    }

    [Test]
    public void Installer_RepairsPartialBackgroundWithoutReplacingExistingVisuals()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        RectTransform preservedStar = view.SpaceBackground.AuthoredStars[4];
        RectTransform preservedAsteroid = view.SpaceBackground.AuthoredAsteroids[2];
        Image preservedStarImage = preservedStar.GetComponent<Image>();
        Vector2 authoredPosition = new Vector2(73f, -29f);
        Color authoredColor = new Color(0.21f, 0.54f, 0.88f, 0.31f);
        preservedStar.anchoredPosition = authoredPosition;
        preservedStarImage.color = authoredColor;

        Object.DestroyImmediate(view.SpaceBackground.AuthoredStars[9].gameObject);
        Object.DestroyImmediate(view.SpaceBackground.AuthoredAsteroids[5].gameObject);
        Object.DestroyImmediate(view.SpaceBackground.CursePasserRect.gameObject);

        Assert.That(Install(out BootMainMenuView repaired), Is.True);
        Assert.That(repaired.SpaceBackground.HasAuthoredVisuals, Is.True);
        Assert.That(repaired.SpaceBackground.AuthoredStars[4], Is.SameAs(preservedStar));
        Assert.That(repaired.SpaceBackground.AuthoredAsteroids[2], Is.SameAs(preservedAsteroid));
        Assert.That(preservedStar.anchoredPosition, Is.EqualTo(authoredPosition));
        Assert.That(preservedStarImage.color, Is.EqualTo(authoredColor));
        Assert.That(repaired.SpaceBackground.AuthoredStars[9], Is.Not.Null);
        Assert.That(repaired.SpaceBackground.AuthoredAsteroids[5], Is.Not.Null);
        Assert.That(repaired.SpaceBackground.CursePasserRect, Is.Not.Null);
    }

    [Test]
    public void Installer_RepairsMissingBackgroundComponentAndViewReference()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        RectTransform backgroundRoot = view.BackgroundRoot;
        RectTransform existingStar = view.SpaceBackground.AuthoredStars[0];
        Object.DestroyImmediate(view.SpaceBackground);
        SetObjectReference(view, "spaceBackground", null);

        BootMainMenuValidationReport beforeRepair = BootMainMenuUIInstaller.ValidateForTests(testScene);
        Assert.That(beforeRepair.IsValid, Is.False);
        Assert.That(beforeRepair.Format(), Does.Contain("missing MainMenuSpaceBackground"));

        Assert.That(Install(out BootMainMenuView repaired), Is.True);
        Assert.That(repaired.BackgroundRoot, Is.SameAs(backgroundRoot));
        Assert.That(repaired.SpaceBackground, Is.Not.Null);
        Assert.That(repaired.SpaceBackground.AuthoredStars[0], Is.SameAs(existingStar));
        Assert.That(backgroundRoot.GetComponents<MainMenuSpaceBackground>().Length, Is.EqualTo(1));
        Assert.That(BootMainMenuUIInstaller.ValidateForTests(testScene).IsValid, Is.True);
    }

    [Test]
    public void Installer_RepairsMissingBackgroundComponentOnExistingBackgroundInPlace()
    {
        GameObject authoredRoot = CreateTestGameObject(
            "BootMainMenuUI",
            true,
            typeof(RectTransform),
            typeof(BootMainMenuView));
        BootMainMenuView existingView = authoredRoot.GetComponent<BootMainMenuView>();
        GameObject backgroundObject = BootMainMenuAuthoringObjectFactory.CreateChild(
            "Background",
            authoredRoot.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform preservedRect = backgroundObject.GetComponent<RectTransform>();
        Image preservedImage = backgroundObject.GetComponent<Image>();
        Vector2 preservedPosition = new Vector2(13f, -7f);
        Vector3 preservedScale = new Vector3(0.94f, 0.91f, 1f);
        Color preservedColor = new Color(0.19f, 0.23f, 0.31f, 0.87f);
        Shader uiShader = Shader.Find("UI/Default");
        Assert.That(uiShader, Is.Not.Null);
        Material preservedMaterial = Track(new Material(uiShader));
        preservedRect.anchoredPosition = preservedPosition;
        preservedRect.localScale = preservedScale;
        preservedImage.color = preservedColor;
        preservedImage.material = preservedMaterial;
        SetObjectReference(existingView, "backgroundRoot", preservedRect);

        Assert.That(backgroundObject.GetComponent<MainMenuSpaceBackground>(), Is.Null);
        Assert.That(Install(out BootMainMenuView repaired), Is.True);
        Assert.That(repaired, Is.SameAs(existingView));
        Assert.That(repaired.BackgroundRoot.gameObject, Is.SameAs(backgroundObject));
        Assert.That(repaired.BackgroundRoot, Is.SameAs(preservedRect));
        Assert.That(repaired.BackgroundRoot.GetComponent<Image>(), Is.SameAs(preservedImage));
        Assert.That(preservedRect.anchoredPosition, Is.EqualTo(preservedPosition));
        Assert.That(preservedRect.localScale, Is.EqualTo(preservedScale));
        Assert.That(preservedImage.color, Is.EqualTo(preservedColor));
        Assert.That(preservedImage.material, Is.SameAs(preservedMaterial));
        Assert.That(repaired.SpaceBackground, Is.Not.Null);
        Assert.That(
            backgroundObject.GetComponents<MainMenuSpaceBackground>().Length,
            Is.EqualTo(1));
        Assert.That(
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(backgroundObject),
            Is.Zero);
        Assert.That(
            repaired.SpaceBackground.AuthoredStars.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredStarCount));
        Assert.That(
            repaired.SpaceBackground.AuthoredAsteroids.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredAsteroidCount));
        Assert.That(repaired.SpaceBackground.CursePasserRect, Is.Not.Null);
        Assert.That(repaired.SpaceBackground.HasAuthoredVisuals, Is.True);
        Assert.That(BootMainMenuUIInstaller.ValidateForTests(testScene).IsValid, Is.True);
    }

    [Test]
    public void Installer_RepairsNullSpaceBackgroundBindingAndPreservesRenamedTypedChild()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        MainMenuSpaceBackground background = view.SpaceBackground;
        RectTransform renamedStar = background.AuthoredStars[3];
        renamedStar.name = "UserRenamedStar";
        SetObjectReference(view, "spaceBackground", null);

        BootMainMenuValidationReport beforeRepair = BootMainMenuUIInstaller.ValidateForTests(testScene);
        Assert.That(beforeRepair.Format(), Does.Contain("spaceBackground is not assigned"));

        Assert.That(Install(out BootMainMenuView repaired), Is.True);
        Assert.That(repaired.SpaceBackground, Is.SameAs(background));
        Assert.That(repaired.SpaceBackground.AuthoredStars[3], Is.SameAs(renamedStar));
        Assert.That(renamedStar.name, Is.EqualTo("UserRenamedStar"));
        Assert.That(repaired.SpaceBackground.HasDuplicateAuthoredReferences, Is.False);
        Assert.That(BootMainMenuUIInstaller.ValidateForTests(testScene).IsValid, Is.True);
    }

    [Test]
    public void Installer_RepairsMissingContinuePresenterBeforeControllerBinding()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        Button preservedButton = view.ContinueButton;
        MainMenuButtonPresenter preservedPresenter =
            preservedButton.GetComponent<MainMenuButtonPresenter>();
        RectTransform preservedRect = preservedButton.transform as RectTransform;
        Image preservedImage = preservedButton.targetGraphic as Image;
        Vector2 authoredPosition = new Vector2(-17f, 31f);
        Color authoredColor = new Color(0.18f, 0.34f, 0.52f, 0.73f);
        preservedRect.anchoredPosition = authoredPosition;
        preservedImage.color = authoredColor;

        SerializedObject serializedView = new SerializedObject(view);
        SerializedProperty continueBinding = serializedView.FindProperty("continueBinding");
        continueBinding.FindPropertyRelative("presenter").objectReferenceValue = null;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        BootMainMenuValidationReport beforeRepair =
            BootMainMenuUIInstaller.ValidateForTests(testScene);
        Assert.That(beforeRepair.IsValid, Is.False);
        Assert.That(
            beforeRepair.Errors,
            Does.Contain(
                "BootMainMenuView has a missing serialized reference: " +
                "'continueBinding.presenter'."));

        Assert.That(Install(out BootMainMenuView repaired), Is.True);
        Assert.That(repaired, Is.SameAs(view));
        Assert.That(repaired.ContinueButton, Is.SameAs(preservedButton));
        Assert.That(
            preservedButton.GetComponent<MainMenuButtonPresenter>(),
            Is.SameAs(preservedPresenter));
        Assert.That(
            preservedButton.GetComponents<MainMenuButtonPresenter>().Length,
            Is.EqualTo(1));
        Assert.That(preservedRect.anchoredPosition, Is.EqualTo(authoredPosition));
        Assert.That(preservedImage.color, Is.EqualTo(authoredColor));
        AssertContinueBindingComplete(repaired);
        Assert.That(controller.AuthoredView, Is.SameAs(repaired));

        System.Reflection.MethodInfo bindMethod = typeof(MainMenuController).GetMethod(
            "TryBindAuthoredView",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(bindMethod, Is.Not.Null);
        Assert.That((bool)bindMethod.Invoke(controller, null), Is.True);
    }

    [Test]
    public void Validator_PassesCorrectFixtureAndReferenceLayout()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        BootMainMenuValidationReport report = BootMainMenuUIInstaller.ValidateForTests(testScene);
        Assert.That(report.IsValid, Is.True, report.Format());
        Assert.That(view.CanvasScaler.referenceResolution, Is.EqualTo(new Vector2(480f, 270f)));
        Assert.That(view.InitialSelectable.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None));
        Assert.That(
            view.SpaceBackground.AuthoredStars.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredStarCount));
        Assert.That(
            view.SpaceBackground.AuthoredAsteroids.Count,
            Is.EqualTo(MainMenuSpaceBackground.RequiredAuthoredAsteroidCount));
        Assert.That(view.SpaceBackground.CursePasserRect, Is.Not.Null);
        Assert.That(
            view.BackgroundRoot.GetComponent<MainMenuSpaceBackground>(),
            Is.SameAs(view.SpaceBackground));
    }

    [Test]
    public void Validator_ReportsMissingBindingsAndDuplicateAuthorities()
    {
        Assert.That(Install(out BootMainMenuView view), Is.True);
        Object.DestroyImmediate(view.NewGameButton.gameObject);
        GameObject duplicateRoot = CreateTestGameObject("DuplicateBootMainMenuUI", false);
        duplicateRoot.AddComponent<BootMainMenuView>();
        GameObject duplicateEventSystem = CreateTestGameObject("DuplicateEventSystem", false);
        duplicateEventSystem.AddComponent<EventSystem>();

        const string missingBinding =
            "BootMainMenuView has a missing serialized reference: 'newGameBinding.button'.";
        const string duplicateRoots = "Expected one BootMainMenuView marker, found 2.";
        const string duplicateEventSystems = "Expected exactly one EventSystem in Boot, found 2.";
        BootMainMenuValidationReport report = BootMainMenuUIInstaller.ValidateForTests(testScene);
        Assert.That(report.IsValid, Is.False);
        Assert.That(report.Errors, Does.Contain(missingBinding));
        Assert.That(report.Errors, Does.Contain(duplicateRoots));
        Assert.That(report.Errors, Does.Contain(duplicateEventSystems));
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

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

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

    private bool Install(out BootMainMenuView view)
    {
        bool installed = BootMainMenuUIInstaller.TryInstallForTests(
            testScene,
            out view,
            out string result);
        Assert.That(installed, Is.True, result);
        Assert.That(controller.gameObject.activeSelf, Is.False);
        AssertContinueBindingComplete(view);
        AssertFixtureSceneOwnership(view);
        return installed;
    }

    private void ConfigureControllerAssets(MainMenuController target)
    {
        Texture2D texture = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        Sprite sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f)));
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty inputActions = serialized.FindProperty("inputActions");
        inputActions.objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/04_Input/PlayerControls.inputactions");
        SerializedProperty font = serialized.FindProperty("uiFont");
        TMP_FontAsset productionFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/07_Txt/neodgm_pro.asset");
        Assert.That(productionFont, Is.Not.Null);
        font.objectReferenceValue = productionFont;
        SerializedProperty asteroids = serialized.FindProperty("backgroundAsteroidSprites");
        asteroids.arraySize = 1;
        asteroids.GetArrayElementAtIndex(0).objectReferenceValue = sprite;
        serialized.FindProperty("cursedBackgroundSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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

    private GameObject CreateTestGameObject(
        string objectName,
        bool active,
        params System.Type[] componentTypes)
    {
        GameObject target = Track(BootMainMenuAuthoringObjectFactory.CreateRoot(
            objectName,
            testScene,
            componentTypes));
        target.SetActive(false);
        target.SetActive(active);
        return target;
    }

    private static void AssertContinueBindingComplete(BootMainMenuView view)
    {
        SerializedObject serialized = new SerializedObject(view);
        SerializedProperty binding = serialized.FindProperty("continueBinding");
        Assert.That(binding, Is.Not.Null);
        Assert.That(
            binding.FindPropertyRelative("button").objectReferenceValue,
            Is.SameAs(view.ContinueButton));
        Assert.That(binding.FindPropertyRelative("label").objectReferenceValue, Is.Not.Null);
        Assert.That(
            binding.FindPropertyRelative("selectionAccent").objectReferenceValue,
            Is.Not.Null);
        Assert.That(binding.FindPropertyRelative("presenter").objectReferenceValue, Is.Not.Null);
        Assert.That(view.ContinueButton, Is.Not.Null);
    }

    private void AssertFixtureSceneOwnership(BootMainMenuView view)
    {
        AssertBelongsToTestScene(controller.gameObject, "controller");
        AssertBelongsToTestScene(view.gameObject, "authored root/view/canvas");
        AssertBelongsToTestScene(view.SafeArea.gameObject, "SafeArea");
        AssertBelongsToTestScene(view.BackgroundRoot.gameObject, "Background");
        AssertBelongsToTestScene(view.ContinueButton.gameObject, "Continue binding");

        EventSystem[] eventSystems = FindInTestScene<EventSystem>();
        Assert.That(eventSystems.Length, Is.EqualTo(1));
        AssertBelongsToTestScene(eventSystems[0].gameObject, "EventSystem");

        for (int i = 0; i < view.SpaceBackground.AuthoredStars.Count; i++)
        {
            AssertBelongsToTestScene(
                view.SpaceBackground.AuthoredStars[i].gameObject,
                $"star {i:00}");
        }

        for (int i = 0; i < view.SpaceBackground.AuthoredAsteroids.Count; i++)
        {
            AssertBelongsToTestScene(
                view.SpaceBackground.AuthoredAsteroids[i].gameObject,
                $"asteroid {i:00}");
        }

        AssertBelongsToTestScene(
            view.SpaceBackground.CursePasserRect.gameObject,
            "Curse passer");
    }

    private void AssertBelongsToTestScene(GameObject target, string label)
    {
        Assert.That(target, Is.Not.Null, label);
        Assert.That(
            target.scene,
            Is.EqualTo(testScene),
            $"The fixture {label} must belong to its preview scene.");
        Assert.That(
            target.scene,
            Is.Not.EqualTo(previousScene),
            $"The fixture {label} must not belong to the user's active scene.");
    }
}
