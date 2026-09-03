using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BootMainMenuValidationReport
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public IReadOnlyList<string> Errors => errors;
    public IReadOnlyList<string> Warnings => warnings;
    public bool IsValid => errors.Count == 0;

    public void AddError(string message)
    {
        errors.Add(message);
    }

    public void AddWarning(string message)
    {
        warnings.Add(message);
    }

    public string Format()
    {
        List<string> lines = new List<string>(errors.Count + warnings.Count + 1)
        {
            IsValid ? "Boot Main Menu UI validation passed." : "Boot Main Menu UI validation failed."
        };

        for (int i = 0; i < errors.Count; i++)
        {
            lines.Add($"ERROR: {errors[i]}");
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            lines.Add($"WARNING: {warnings[i]}");
        }

        return string.Join("\n", lines);
    }
}

public static class BootMainMenuUIInstaller
{
    public const string BootScenePath = "Assets/01_Scenes/Boot.unity";
    public static readonly Vector2 ReferenceResolution = new Vector2(480f, 270f);

    private static readonly Color BackgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.94f);
    private static readonly Color MenuBackingColor = new Color(0.035f, 0.055f, 0.085f, 0.48f);
    private static readonly Color ButtonColor = new Color(0.075f, 0.115f, 0.16f, 0.78f);
    private static readonly Color AccentColor = new Color(0.2f, 0.85f, 0.72f, 1f);
    private static readonly Color TextColor = new Color(0.9f, 0.95f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.62f, 0.7f, 0.78f, 1f);

    [MenuItem("VOID SCRAPPER/UI/Install Boot Main Menu UI")]
    public static void InstallFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Boot Main Menu UI] Exit Play Mode before running the installer.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!TryInstall(
                scene,
                true,
                true,
                out BootMainMenuView view,
                out string result))
        {
            Debug.LogError($"[Boot Main Menu UI] {result}");
            return;
        }

        Selection.activeGameObject = view.gameObject;
        Debug.Log(
            $"[Boot Main Menu UI] {result}\nReview the authored hierarchy and Inspector values, " +
            "run Validate Boot Main Menu UI, then save the Boot scene manually.",
            view);
    }

    [MenuItem("VOID SCRAPPER/UI/Validate Boot Main Menu UI")]
    public static void ValidateFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        BootMainMenuValidationReport report = Validate(scene, true);
        if (report.IsValid)
        {
            Debug.Log($"[Boot Main Menu UI] {report.Format()}");
        }
        else
        {
            Debug.LogError($"[Boot Main Menu UI] {report.Format()}");
        }
    }

    internal static bool TryInstallForTests(
        Scene scene,
        out BootMainMenuView view,
        out string result)
    {
        return TryInstall(scene, false, false, out view, out result);
    }

    internal static BootMainMenuValidationReport ValidateForTests(Scene scene)
    {
        return Validate(scene, false);
    }

    private static bool TryInstall(
        Scene scene,
        bool requireBootScenePath,
        bool markSceneDirty,
        out BootMainMenuView view,
        out string result)
    {
        view = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            result = "The installer cannot run during Play Mode.";
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result = "The target scene is invalid or not loaded.";
            return false;
        }

        if (requireBootScenePath && !PathsEqual(scene.path, BootScenePath))
        {
            result = $"Open '{BootScenePath}' before installing. Active scene: '{scene.path}'.";
            return false;
        }

        MainMenuController[] controllers = FindInScene<MainMenuController>(scene);
        if (controllers.Length != 1)
        {
            result = $"Expected exactly one MainMenuController in '{scene.name}', found {controllers.Length}.";
            return false;
        }

        BootMainMenuView[] existingViews = FindInScene<BootMainMenuView>(scene);
        if (existingViews.Length > 1)
        {
            result = $"Found {existingViews.Length} authored Boot roots. Remove the duplicate explicitly before installing.";
            return false;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Boot Main Menu UI");

        EventSystem[] existingEventSystems = FindInScene<EventSystem>(scene);
        if (existingEventSystems.Length > 1)
        {
            result = $"Found {existingEventSystems.Length} EventSystems. Resolve the duplicate before installing.";
            return false;
        }

        if (existingEventSystems.Length == 0)
        {
            GameObject eventSystemRoot = BootMainMenuAuthoringObjectFactory.CreateRoot(
                "EventSystem",
                scene);
            Undo.RegisterCreatedObjectUndo(eventSystemRoot, "Create Boot EventSystem");
            Undo.AddComponent<EventSystem>(eventSystemRoot);
            Undo.AddComponent<InputSystemUIInputModule>(eventSystemRoot);
        }

        GameObject installationRoot = null;
        bool installationRootWasCreated = false;
        bool installationRootWasActive = false;
        try
        {
            MainMenuController controller = controllers[0];
            bool createdRoot = existingViews.Length == 0;
            GameObject root = createdRoot
                ? CreateRootObject("BootMainMenuUI", scene)
                : existingViews[0].gameObject;
            bool intendedRootActive = createdRoot || root.activeSelf;
            installationRoot = root;
            installationRootWasCreated = createdRoot;
            installationRootWasActive = intendedRootActive;
            root.SetActive(false);
            view = EnsureComponent<BootMainMenuView>(root);
            Canvas canvas = EnsureComponent<Canvas>(root, out bool canvasAdded);
            CanvasScaler scaler = EnsureComponent<CanvasScaler>(root);
            GraphicRaycaster raycaster = EnsureComponent<GraphicRaycaster>(root);
            CanvasGroup rootGroup = EnsureComponent<CanvasGroup>(root);

            if (createdRoot || canvasAdded)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = controller.CanvasSortOrder;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = root.transform as RectTransform;
            SetStretchIfNew(rootRect, createdRoot);

            MainMenuSpaceBackground background = BuildBackground(
                root.transform,
                view,
                controller,
                out RectTransform backgroundRoot);
            RectTransform safeArea = GetOrCreateRect(root.transform, "SafeArea", out bool safeAreaCreated);
            SetStretchIfNew(safeArea, safeAreaCreated);

            GameObject menuRoot = BuildMenuPanel(
                safeArea,
                controller.UiFont,
                out RectTransform titleRoot,
                out TextMeshProUGUI gameTitle,
                out TextMeshProUGUI subtitle,
                out TextMeshProUGUI statusText,
                out BootMainMenuButtonBinding continueBinding,
                out BootMainMenuButtonBinding newGameBinding,
                out BootMainMenuButtonBinding settingsBinding,
                out BootMainMenuButtonBinding exitBinding);

            ConfigureMainNavigation(
                continueBinding.Button,
                newGameBinding.Button,
                settingsBinding.Button,
                exitBinding.Button);

            GameObject optionsRoot = BuildOptionsPanel(safeArea, controller);
            SharedOptionsMenuUI sharedOptions = optionsRoot.GetComponent<SharedOptionsMenuUI>();
            GameObject confirmationRoot = BuildNewGameConfirmation(
                safeArea,
                controller.UiFont,
                out Button confirmButton,
                out Button cancelButton);

            CanvasGroup transitionOverlay = FindTransitionOverlay(scene);
            CanvasGroup[] entranceGroups =
            {
                EnsureComponent<CanvasGroup>(gameTitle.gameObject),
                EnsureComponent<CanvasGroup>(subtitle.gameObject),
                EnsureComponent<CanvasGroup>(continueBinding.Button.gameObject),
                EnsureComponent<CanvasGroup>(newGameBinding.Button.gameObject),
                EnsureComponent<CanvasGroup>(settingsBinding.Button.gameObject),
                EnsureComponent<CanvasGroup>(exitBinding.Button.gameObject)
            };

            Undo.RecordObject(view, "Bind authored Boot Main Menu UI");
            view.ConfigureForAuthoring(
                canvas,
                scaler,
                raycaster,
                rootGroup,
                safeArea,
                backgroundRoot,
                background,
                transitionOverlay,
                menuRoot,
                titleRoot,
                gameTitle,
                subtitle,
                statusText,
                continueBinding,
                newGameBinding,
                settingsBinding,
                exitBinding,
                newGameBinding.Button,
                entranceGroups,
                optionsRoot,
                sharedOptions,
                confirmationRoot,
                confirmButton,
                cancelButton);
            BindButtonReferences(view, "continueBinding", continueBinding);
            BindButtonReferences(view, "newGameBinding", newGameBinding);
            BindButtonReferences(view, "settingsBinding", settingsBinding);
            BindButtonReferences(view, "exitBinding", exitBinding);
            BindBackgroundReferences(view, backgroundRoot, background);

            Undo.RecordObject(controller, "Bind authored Boot Main Menu UI");
            controller.AssignAuthoredView(view);
            root.SetActive(intendedRootActive);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(sharedOptions);
            EditorUtility.SetDirty(background);
            if (markSceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Undo.CollapseUndoOperations(undoGroup);

            BootMainMenuValidationReport report = Validate(scene, requireBootScenePath);
            result = report.IsValid
                ? "Installed or repaired the scene-authored Boot menu without resetting existing visual values."
                : "Installation completed, but validation still requires attention:\n" + report.Format();
            return true;
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            if (!installationRootWasCreated && installationRoot != null)
            {
                installationRoot.SetActive(installationRootWasActive);
            }

            result = $"Installation failed and its Undo group was reverted: {exception.Message}";
            view = null;
            return false;
        }
    }

    private static BootMainMenuValidationReport Validate(Scene scene, bool requireBootScenePath)
    {
        BootMainMenuValidationReport report = new BootMainMenuValidationReport();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report.AddError("The target scene is invalid or not loaded.");
            return report;
        }

        if (requireBootScenePath && !PathsEqual(scene.path, BootScenePath))
        {
            report.AddError($"Wrong scene open. Expected '{BootScenePath}', got '{scene.path}'.");
        }

        BootMainMenuView[] views = FindInScene<BootMainMenuView>(scene);
        if (views.Length == 0)
        {
            report.AddError("Expected one BootMainMenuView marker, found 0.");
            return report;
        }

        if (views.Length > 1)
        {
            report.AddError($"Expected one BootMainMenuView marker, found {views.Length}.");
        }

        MainMenuController[] controllers = FindInScene<MainMenuController>(scene);
        BootMainMenuView view = ResolveValidationView(views, controllers);
        if (!view.TryGetMissingRuntimeReference(out string missing))
        {
            report.AddError($"BootMainMenuView has a missing serialized reference: '{missing}'.");
        }

        if (controllers.Length != 1)
        {
            report.AddError($"Expected one MainMenuController, found {controllers.Length}.");
        }
        else if (controllers[0].AuthoredView != view)
        {
            report.AddError("MainMenuController does not reference the authored BootMainMenuView.");
        }

        Canvas[] authoredCanvases = view.GetComponents<Canvas>();
        if (authoredCanvases.Length != 1)
        {
            report.AddError($"Authored root must contain exactly one Canvas, found {authoredCanvases.Length}.");
        }

        if (view.CanvasScaler == null ||
            (view.CanvasScaler.referenceResolution - ReferenceResolution).sqrMagnitude > 0.001f)
        {
            report.AddError("CanvasScaler reference resolution must be 480x270.");
        }

        if (view.GraphicRaycaster == null)
        {
            report.AddError("The authored root is missing GraphicRaycaster.");
        }

        EventSystem[] eventSystems = FindInScene<EventSystem>(scene);
        if (eventSystems.Length != 1)
        {
            report.AddError($"Expected exactly one EventSystem in Boot, found {eventSystems.Length}.");
        }

        if (view.InitialSelectable == null || view.InitialSelectable.navigation.mode == Navigation.Mode.None)
        {
            report.AddError("Initial selectable is missing or has navigation disabled.");
        }

        ValidateButton(view.ContinueButton, "Continue", report);
        ValidateButton(view.NewGameButton, "New Game", report);
        ValidateButton(view.SettingsButton, "Settings", report);
        ValidateButton(view.ExitButton, "Exit", report);

        if (view.SharedOptions == null || !view.SharedOptions.HasAuthoredLayout)
        {
            report.AddError("OptionsPanel is not serialized as an authored SharedOptionsMenuUI layout.");
        }

        ValidateBackground(view, report);

        if (view.TransitionOverlay == null)
        {
            report.AddError("The existing ScreenFader CanvasGroup is not assigned as transition overlay.");
        }

        if (!view.gameObject.activeSelf || view.SafeArea == null || !view.SafeArea.gameObject.activeSelf ||
            view.MenuRoot == null || !view.MenuRoot.activeSelf)
        {
            report.AddError("An inactive authored root, SafeArea, or initial MenuPanel prevents the menu from opening.");
        }

        TMP_Text[] allTexts = view.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            ValidateFont(allTexts[i], allTexts[i].name, report);
        }
        ValidateSafeBounds(view, report);

        if (typeof(MainMenuController).GetMethod(
                "BuildRuntimeView",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic) != null)
        {
            report.AddError("MainMenuController still exposes the legacy runtime visual builder.");
        }

        LocalizedTextPresenter[] localized = view.GetComponentsInChildren<LocalizedTextPresenter>(true);
        LocalizationCatalog catalog = localized.Length > 0
            ? AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
                "Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset")
            : null;
        for (int i = 0; i < localized.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(localized[i].TextKey))
            {
                report.AddError($"Localized text presenter '{localized[i].name}' has an empty key.");
            }
            else if (catalog == null)
            {
                report.AddError("LocalizationCatalog is missing while Boot contains localized text presenters.");
                break;
            }
            else if (!catalog.TryGetEntry(localized[i].TextKey, out _))
            {
                report.AddError(
                    $"Localized text presenter '{localized[i].name}' references unknown key " +
                    $"'{localized[i].TextKey}'.");
            }
        }

        return report;
    }

    private static BootMainMenuView ResolveValidationView(
        BootMainMenuView[] views,
        MainMenuController[] controllers)
    {
        if (controllers.Length == 1 && controllers[0].AuthoredView != null)
        {
            BootMainMenuView boundView = controllers[0].AuthoredView;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] == boundView)
                {
                    return boundView;
                }
            }
        }

        return views[0];
    }

    private static MainMenuSpaceBackground BuildBackground(
        Transform root,
        BootMainMenuView view,
        MainMenuController controller,
        out RectTransform rect)
    {
        rect = ResolveBackgroundRoot(root, view, out bool created);
        SetStretchIfNew(rect, created);
        Image image = EnsureComponent<Image>(rect.gameObject, out bool imageAdded);
        if (created || imageAdded)
        {
            image.color = BackgroundColor;
            image.raycastTarget = false;
        }

        RemoveMissingBackgroundScripts(rect.gameObject);
        MainMenuSpaceBackground background = EnsureComponent<MainMenuSpaceBackground>(rect.gameObject);
        Undo.RecordObject(background, "Repair Boot background references");
        background.AuthorVisuals(
            controller.BackgroundAsteroidSprites,
            controller.CursedBackgroundSprite);
        if (background.HasAuthoredVisuals)
        {
            background.enabled = true;
        }

        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(rect.gameObject);

        return background;
    }

    private static RectTransform ResolveBackgroundRoot(
        Transform authoredRoot,
        BootMainMenuView view,
        out bool created)
    {
        RectTransform typedRoot = view != null ? view.BackgroundRoot : null;
        if (typedRoot != null && typedRoot.parent == authoredRoot)
        {
            created = false;
            return typedRoot;
        }

        MainMenuSpaceBackground typedBackground = view != null ? view.SpaceBackground : null;
        if (typedBackground != null &&
            typedBackground.transform.parent == authoredRoot &&
            typedBackground.transform is RectTransform componentRoot)
        {
            created = false;
            return componentRoot;
        }

        return GetOrCreateRect(authoredRoot, "Background", out created);
    }

    private static void BindBackgroundReferences(
        BootMainMenuView view,
        RectTransform backgroundRoot,
        MainMenuSpaceBackground background)
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SerializedProperty rootProperty = serializedView.FindProperty("backgroundRoot");
        SerializedProperty backgroundProperty = serializedView.FindProperty("spaceBackground");
        if (rootProperty == null || backgroundProperty == null)
        {
            throw new InvalidOperationException(
                "BootMainMenuView background serialized fields could not be resolved.");
        }

        rootProperty.objectReferenceValue = backgroundRoot;
        backgroundProperty.objectReferenceValue = background;
        serializedView.ApplyModifiedProperties();
    }

    private static void BindButtonReferences(
        BootMainMenuView view,
        string propertyName,
        BootMainMenuButtonBinding binding)
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SerializedProperty bindingProperty = serializedView.FindProperty(propertyName);
        if (bindingProperty == null)
        {
            throw new InvalidOperationException(
                $"BootMainMenuView serialized binding '{propertyName}' could not be resolved.");
        }

        bindingProperty.FindPropertyRelative("button").objectReferenceValue = binding.Button;
        bindingProperty.FindPropertyRelative("label").objectReferenceValue = binding.Label;
        bindingProperty.FindPropertyRelative("selectionAccent").objectReferenceValue =
            binding.SelectionAccent;
        bindingProperty.FindPropertyRelative("presenter").objectReferenceValue = binding.Presenter;
        serializedView.ApplyModifiedProperties();
    }

    private static void ValidateBackground(
        BootMainMenuView view,
        BootMainMenuValidationReport report)
    {
        RectTransform backgroundRoot = view.BackgroundRoot;
        if (backgroundRoot == null)
        {
            report.AddError("BootMainMenuView is missing the authored Background root reference.");
            return;
        }

        int missingScriptCount =
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(backgroundRoot.gameObject);
        if (missingScriptCount > 0)
        {
            report.AddError(
                $"The authored Background has {missingScriptCount} Missing Script component " +
                "slot(s). Run Install Boot Main Menu UI to repair this Background in place.");
        }

        MainMenuSpaceBackground[] components =
            backgroundRoot.GetComponents<MainMenuSpaceBackground>();
        if (components.Length == 0)
        {
            report.AddError("The authored Background is missing MainMenuSpaceBackground.");
        }
        else if (components.Length > 1)
        {
            report.AddError(
                $"The authored Background has {components.Length} MainMenuSpaceBackground components; expected one.");
        }

        MainMenuSpaceBackground background = view.SpaceBackground;
        if (background == null)
        {
            report.AddError("BootMainMenuView.spaceBackground is not assigned.");
            return;
        }

        if (background.gameObject != backgroundRoot.gameObject)
        {
            report.AddError("BootMainMenuView.spaceBackground does not reference its authored Background root.");
        }

        int starCount = CountValidDirectReferences(background.AuthoredStars, backgroundRoot);
        if (starCount != MainMenuSpaceBackground.RequiredAuthoredStarCount)
        {
            report.AddError(
                $"Background has {starCount} bound authored stars; expected " +
                $"{MainMenuSpaceBackground.RequiredAuthoredStarCount}.");
        }

        int asteroidCount = CountValidDirectReferences(background.AuthoredAsteroids, backgroundRoot);
        if (asteroidCount != MainMenuSpaceBackground.RequiredAuthoredAsteroidCount)
        {
            report.AddError(
                $"Background has {asteroidCount} bound authored asteroid visuals; expected " +
                $"{MainMenuSpaceBackground.RequiredAuthoredAsteroidCount}.");
        }

        if (background.CursePasserRect == null || background.CursePasserImage == null)
        {
            report.AddError("Background is missing the bound authored Pixel Curse passer visual.");
        }

        if (background.HasDuplicateAuthoredReferences)
        {
            report.AddError("Background contains duplicate authored visual references.");
        }

        if (!background.HasAssignedAsteroidSprites)
        {
            report.AddError("One or more authored menu asteroids has a missing Sprite reference.");
        }

        if (!backgroundRoot.gameObject.activeInHierarchy || !background.enabled)
        {
            report.AddError("The authored Background hierarchy or animation component is inactive.");
        }

        ValidateNoDuplicateCanonicalChildren(
            backgroundRoot,
            "StaticStar_",
            MainMenuSpaceBackground.RequiredAuthoredStarCount,
            "star",
            background.AuthoredStars,
            report);
        ValidateNoDuplicateCanonicalChildren(
            backgroundRoot,
            "MenuAsteroid_",
            MainMenuSpaceBackground.RequiredAuthoredAsteroidCount,
            "asteroid",
            background.AuthoredAsteroids,
            report);

        ValidateReferencedChildrenActive(background.AuthoredStars, "star", report);
        ValidateReferencedChildrenActive(background.AuthoredAsteroids, "asteroid", report);

        int cursePasserCount = CountDirectChildrenNamed(backgroundRoot, "PixelCursePasser");
        if (cursePasserCount > 1)
        {
            report.AddError(
                $"Background has {cursePasserCount} canonical PixelCursePasser children; expected at most one.");
        }
    }

    private static int RemoveMissingBackgroundScripts(GameObject backgroundObject)
    {
        if (backgroundObject == null)
        {
            return 0;
        }

        int missingScriptCount =
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(backgroundObject);
        if (missingScriptCount <= 0)
        {
            return 0;
        }

        Undo.RegisterCompleteObjectUndo(
            backgroundObject,
            "Repair missing Boot background script");
        int removedCount =
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(backgroundObject);
        EditorUtility.SetDirty(backgroundObject);
        return removedCount;
    }

    private static int CountValidDirectReferences(
        IReadOnlyList<RectTransform> references,
        RectTransform expectedParent)
    {
        if (references == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < references.Count; i++)
        {
            if (references[i] != null && references[i].parent == expectedParent)
            {
                count++;
            }
        }

        return count;
    }

    private static void ValidateNoDuplicateCanonicalChildren(
        RectTransform parent,
        string prefix,
        int expectedCount,
        string label,
        IReadOnlyList<RectTransform> boundReferences,
        BootMainMenuValidationReport report)
    {
        bool[] slots = new bool[expectedCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            string objectName = parent.GetChild(i).name;
            if (!objectName.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(objectName.Substring(prefix.Length), out int slot) ||
                slot < 0 ||
                slot >= expectedCount)
            {
                continue;
            }

            if (slots[slot])
            {
                report.AddError($"Background contains a duplicate canonical {label} slot {slot:00}.");
            }

            slots[slot] = true;
            Transform child = parent.GetChild(i);
            if (!ContainsReference(boundReferences, child as RectTransform))
            {
                report.AddError(
                    $"Background contains unbound canonical {label} child '{objectName}'.");
            }
        }
    }

    private static void ValidateReferencedChildrenActive(
        IReadOnlyList<RectTransform> references,
        string label,
        BootMainMenuValidationReport report)
    {
        if (references == null)
        {
            return;
        }

        for (int i = 0; i < references.Count; i++)
        {
            if (references[i] != null && !references[i].gameObject.activeSelf)
            {
                report.AddError(
                    $"Authored background {label} reference {i:00} is inactive.");
            }
        }
    }

    private static bool ContainsReference(
        IReadOnlyList<RectTransform> references,
        RectTransform target)
    {
        if (references == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < references.Count; i++)
        {
            if (references[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDirectChildrenNamed(Transform parent, string objectName)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            if (string.Equals(parent.GetChild(i).name, objectName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static GameObject BuildMenuPanel(
        Transform parent,
        TMP_FontAsset font,
        out RectTransform titleRoot,
        out TextMeshProUGUI gameTitle,
        out TextMeshProUGUI subtitle,
        out TextMeshProUGUI statusText,
        out BootMainMenuButtonBinding continueBinding,
        out BootMainMenuButtonBinding newGameBinding,
        out BootMainMenuButtonBinding settingsBinding,
        out BootMainMenuButtonBinding exitBinding)
    {
        RectTransform panelRect = GetOrCreateRect(parent, "MenuPanel", out bool panelCreated);
        InitializeCenteredRect(panelRect, new Vector2(-132f, 0f), new Vector2(204f, 230f), panelCreated);
        Image panelImage = EnsureComponent<Image>(panelRect.gameObject);
        if (panelCreated)
        {
            panelImage.color = MenuBackingColor;
            panelImage.raycastTarget = false;
        }

        RectTransform accent = GetOrCreateRect(panelRect, "MenuAccent", out bool accentCreated);
        InitializeCenteredRect(accent, new Vector2(-99f, 0f), new Vector2(2f, 208f), accentCreated);
        Image accentImage = EnsureComponent<Image>(accent.gameObject);
        if (accentCreated)
        {
            accentImage.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.72f);
            accentImage.raycastTarget = false;
        }

        titleRoot = GetOrCreateRect(panelRect, "TitleRoot", out bool titleRootCreated);
        SetStretchIfNew(titleRoot, titleRootCreated);
        gameTitle = GetOrCreateText(titleRoot, "GameTitle", "VOID SCRAPPER", new Vector2(-8f, 88f), new Vector2(178f, 34f), 24f, TextAlignmentOptions.Left, AccentColor, font);
        subtitle = GetOrCreateText(titleRoot, "Subtitle", "EXPEDITION COMMAND", new Vector2(-8f, 64f), new Vector2(178f, 14f), 7.5f, TextAlignmentOptions.Left, MutedTextColor, font);
        RectTransform divider = GetOrCreateRect(titleRoot, "TitleDivider", out bool dividerCreated);
        InitializeCenteredRect(divider, new Vector2(-10f, 50f), new Vector2(160f, 1f), dividerCreated);
        Image dividerImage = EnsureComponent<Image>(divider.gameObject);
        if (dividerCreated)
        {
            dividerImage.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f);
            dividerImage.raycastTarget = false;
        }

        continueBinding = GetOrCreateMainButton(panelRect, "ContinueButton", "이어하기", new Vector2(-10f, 26f), font);
        newGameBinding = GetOrCreateMainButton(panelRect, "NewGameButton", "새 게임", new Vector2(-10f, -6f), font);
        settingsBinding = GetOrCreateMainButton(panelRect, "SettingsButton", "설정", new Vector2(-10f, -38f), font);
        exitBinding = GetOrCreateMainButton(panelRect, "ExitButton", "종료", new Vector2(-10f, -70f), font);
        statusText = GetOrCreateText(panelRect, "StatusText", string.Empty, new Vector2(-10f, -101f), new Vector2(170f, 24f), 7f, TextAlignmentOptions.Left, new Color(1f, 0.55f, 0.45f, 1f), font);
        return panelRect.gameObject;
    }

    private static GameObject BuildOptionsPanel(Transform parent, MainMenuController controller)
    {
        RectTransform optionsRect = GetOrCreateRect(parent, "OptionsPanel", out bool created);
        SharedOptionsMenuUI options = EnsureComponent<SharedOptionsMenuUI>(optionsRect.gameObject);
        if (!options.HasAuthoredLayout)
        {
            if (!created && optionsRect.childCount > 0)
            {
                options.RepairAuthoredReferences();
            }

            if (!options.HasAuthoredLayout && optionsRect.childCount == 0)
            {
                Undo.RegisterFullObjectHierarchyUndo(optionsRect.gameObject, "Author Boot options hierarchy");
                options.Configure(
                    controller.InputActions,
                    controller.MasterAudioMixer,
                    controller.UiFont,
                    true,
                    true);
            }
        }

        if (options.HasAuthoredLayout)
        {
            options.Configure(
                controller.InputActions,
                controller.MasterAudioMixer,
                controller.UiFont,
                true,
                true);
        }

        optionsRect.gameObject.SetActive(false);
        return optionsRect.gameObject;
    }

    private static GameObject BuildNewGameConfirmation(
        Transform parent,
        TMP_FontAsset font,
        out Button confirmButton,
        out Button cancelButton)
    {
        RectTransform root = GetOrCreateRect(parent, "NewGameConfirmation", out bool rootCreated);
        SetStretchIfNew(root, rootCreated);
        Image overlay = EnsureComponent<Image>(root.gameObject);
        if (rootCreated)
        {
            overlay.color = new Color(0f, 0f, 0f, 0.8f);
        }

        RectTransform panel = GetOrCreateRect(root, "ConfirmationPanel", out bool panelCreated);
        InitializeCenteredRect(panel, Vector2.zero, new Vector2(320f, 130f), panelCreated);
        Image panelImage = EnsureComponent<Image>(panel.gameObject);
        if (panelCreated)
        {
            panelImage.color = PanelColor;
        }

        GetOrCreateText(panel, "ConfirmationTitle", "새 게임을 시작하시겠습니까?", new Vector2(0f, 38f), new Vector2(280f, 24f), 15f, TextAlignmentOptions.Center, AccentColor, font);
        GetOrCreateText(panel, "ConfirmationBody", "기존 진행 상황이 초기화됩니다.", new Vector2(0f, 9f), new Vector2(280f, 22f), 10f, TextAlignmentOptions.Center, TextColor, font);
        confirmButton = GetOrCreateButton(panel, "ConfirmNewGameButton", "확인", new Vector2(-65f, -38f), new Vector2(108f, 26f), 9f, font, SoundEventIds.UiActivate);
        cancelButton = GetOrCreateButton(panel, "CancelNewGameButton", "취소", new Vector2(65f, -38f), new Vector2(108f, 26f), 9f, font, SoundEventIds.UiBack);
        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    private static BootMainMenuButtonBinding GetOrCreateMainButton(
        Transform parent,
        string objectName,
        string labelText,
        Vector2 position,
        TMP_FontAsset font)
    {
        Button button = GetOrCreateButton(parent, objectName, labelText, position, new Vector2(158f, 24f), 9f, font, SoundEventIds.UiClick, false);
        RemoveMissingInstallerOwnedButtonScripts(button.gameObject, objectName);
        Transform labelTransform = button.transform.Find("Label");
        TextMeshProUGUI label = labelTransform != null
            ? labelTransform.GetComponent<TextMeshProUGUI>()
            : null;
        if (label == null)
        {
            throw new InvalidOperationException(
                $"'{objectName}' could not repair its required TMP Label component.");
        }
        RectTransform accentRect = GetOrCreateRect(button.transform, "SelectionAccent", out bool accentCreated);
        InitializeCenteredRect(accentRect, new Vector2(-77f, 0f), new Vector2(2f, 18f), accentCreated);
        Image accent = EnsureComponent<Image>(accentRect.gameObject, out bool accentAdded);
        if (accentCreated || accentAdded)
        {
            accent.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.48f);
            accent.raycastTarget = false;
        }

        MainMenuButtonPresenter presenter = EnsureComponent<MainMenuButtonPresenter>(button.gameObject);
        BootMainMenuButtonBinding binding = new BootMainMenuButtonBinding();
        binding.Configure(button, label, accent, presenter);
        return binding;
    }

    private static int RemoveMissingInstallerOwnedButtonScripts(
        GameObject buttonObject,
        string buttonName)
    {
        if (buttonObject == null)
        {
            return 0;
        }

        int missingScriptCount =
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(buttonObject);
        if (missingScriptCount <= 0)
        {
            return 0;
        }

        Undo.RegisterCompleteObjectUndo(
            buttonObject,
            $"Repair missing {buttonName} presenter script");
        int removedCount =
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(buttonObject);
        EditorUtility.SetDirty(buttonObject);
        return removedCount;
    }

    private static Button GetOrCreateButton(
        Transform parent,
        string objectName,
        string labelValue,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TMP_FontAsset font,
        string soundEvent,
        bool hoverSound = true)
    {
        RectTransform rect = GetOrCreateRect(parent, objectName, out bool created);
        InitializeCenteredRect(rect, position, size, created);
        Image image = EnsureComponent<Image>(rect.gameObject, out bool imageAdded);
        Button button = EnsureComponent<Button>(rect.gameObject, out bool buttonAdded);
        if (created || imageAdded || buttonAdded)
        {
            image.color = ButtonColor;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 1f, 0.95f, 1f);
            colors.pressedColor = new Color(0.65f, 0.85f, 0.8f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.38f, 0.42f, 0.46f, 0.6f);
            button.colors = colors;
        }

        if (button.targetGraphic == null)
        {
            Undo.RecordObject(button, $"Repair {objectName} target graphic");
            button.targetGraphic = image;
        }

        RectTransform labelRect = GetOrCreateRect(rect, "Label", out bool labelCreated);
        SetStretchIfNew(labelRect, labelCreated);
        TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(
            labelRect.gameObject,
            out bool labelAdded);
        if (labelCreated || labelAdded)
        {
            label.text = labelValue;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = TextColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            if (font != null)
            {
                label.font = font;
            }
        }
        else if (label.font == null && font != null)
        {
            Undo.RecordObject(label, $"Repair {objectName} label font");
            label.font = font;
        }

        UISoundButton sound = EnsureComponent<UISoundButton>(rect.gameObject, out bool soundAdded);
        if (created || soundAdded)
        {
            sound.SetClickSoundEnabled(true);
            sound.SetHoverSoundEnabled(hoverSound);
            sound.SetDisabledClickSoundEnabled(true);
            sound.SetClickSoundEventId(soundEvent);
            sound.SetHoverSoundEventId(SoundEventIds.UiHover);
            sound.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
        }

        return button;
    }

    private static TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        TMP_FontAsset font)
    {
        RectTransform rect = GetOrCreateRect(parent, objectName, out bool created);
        InitializeCenteredRect(rect, position, size, created);
        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        if (created)
        {
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
            }
        }

        return text;
    }

    private static RectTransform GetOrCreateRect(Transform parent, string objectName, out bool created)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            created = false;
            RectTransform existingRect = existing as RectTransform;
            if (existingRect == null)
            {
                throw new InvalidOperationException($"'{objectName}' exists without RectTransform.");
            }

            return existingRect;
        }

        GameObject target = BootMainMenuAuthoringObjectFactory.CreateChild(
            objectName,
            parent,
            typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(target, $"Create {objectName}");
        target.layer = LayerMask.NameToLayer("UI");
        created = true;
        return target.GetComponent<RectTransform>();
    }

    private static GameObject CreateRootObject(string objectName, Scene scene)
    {
        GameObject root = BootMainMenuAuthoringObjectFactory.CreateRoot(
            objectName,
            scene,
            typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, $"Create {objectName}");
        root.layer = LayerMask.NameToLayer("UI");
        return root;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static T EnsureComponent<T>(GameObject target, out bool added) where T : Component
    {
        T component = target.GetComponent<T>();
        added = component == null;
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void SetStretchIfNew(RectTransform rect, bool created)
    {
        if (!created || rect == null)
        {
            return;
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void InitializeCenteredRect(RectTransform rect, Vector2 position, Vector2 size, bool created)
    {
        if (!created || rect == null)
        {
            return;
        }
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ConfigureMainNavigation(Button first, Button second, Button third, Button fourth)
    {
        ConfigureNavigationIfAutomatic(first, fourth, second);
        ConfigureNavigationIfAutomatic(second, first, third);
        ConfigureNavigationIfAutomatic(third, second, fourth);
        ConfigureNavigationIfAutomatic(fourth, third, first);
    }

    private static void ConfigureNavigationIfAutomatic(Button button, Selectable up, Selectable down)
    {
        if (button == null || button.navigation.mode != Navigation.Mode.Automatic)
        {
            return;
        }
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        button.navigation = navigation;
    }

    private static CanvasGroup FindTransitionOverlay(Scene scene)
    {
        ScreenFader[] faders = FindInScene<ScreenFader>(scene);
        return faders.Length == 1 ? faders[0].GetComponent<CanvasGroup>() : null;
    }

    private static void ValidateButton(Button button, string label, BootMainMenuValidationReport report)
    {
        if (button == null)
        {
            report.AddError($"Missing required {label} button reference.");
            return;
        }

        if (button.navigation.mode == Navigation.Mode.None)
        {
            report.AddError($"{label} button has navigation disabled.");
        }

        int missingScriptCount =
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(button.gameObject);
        if (missingScriptCount > 0)
        {
            report.AddError(
                $"{label} button has {missingScriptCount} Missing Script component slot(s). " +
                "Run Install Boot Main Menu UI to repair its presenter in place.");
        }

        MainMenuButtonPresenter[] presenters =
            button.GetComponents<MainMenuButtonPresenter>();
        if (presenters.Length != 1)
        {
            report.AddError(
                $"{label} button has {presenters.Length} valid MainMenuButtonPresenter " +
                "components; expected one.");
        }

        if (button.onClick.GetPersistentEventCount() > 0)
        {
            report.AddError($"{label} button has persistent callbacks. Runtime binding must remain the sole listener authority.");
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        ValidateFont(text, $"{label} button label", report);
        if (button.targetGraphic == null)
        {
            report.AddError($"{label} button has no target Graphic.");
        }
    }

    private static void ValidateFont(TMP_Text text, string label, BootMainMenuValidationReport report)
    {
        if (text == null)
        {
            report.AddError($"Missing {label} TMP text.");
        }
        else if (text.font == null)
        {
            report.AddError($"{label} has no TMP font asset.");
        }
    }

    private static void ValidateSafeBounds(BootMainMenuView view, BootMainMenuValidationReport report)
    {
        if (view.SafeArea == null)
        {
            return;
        }
        RectTransform[] panels =
        {
            view.MenuRoot != null ? view.MenuRoot.transform as RectTransform : null,
            view.OptionsRoot != null ? view.OptionsRoot.transform as RectTransform : null,
            view.NewGameConfirmationRoot != null ? view.NewGameConfirmationRoot.transform as RectTransform : null
        };

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null)
            {
                continue;
            }
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.SafeArea, panels[i]);
            if (bounds.min.x < -240.01f || bounds.max.x > 240.01f ||
                bounds.min.y < -135.01f || bounds.max.y > 135.01f)
            {
                report.AddError($"'{panels[i].name}' extends outside the 480x270 reference-safe area.");
            }
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T[] found = roots[i].GetComponentsInChildren<T>(true);
            for (int j = 0; j < found.Length; j++)
            {
                results.Add(found[j]);
            }
        }

        return results.ToArray();
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            (first ?? string.Empty).Replace('\\', '/'),
            (second ?? string.Empty).Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
