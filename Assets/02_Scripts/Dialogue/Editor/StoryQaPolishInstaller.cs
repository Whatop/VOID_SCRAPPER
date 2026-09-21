using System;
using System.Linq;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, idempotent authoring of this polish pass. Never invoked by gameplay.</summary>
public static class StoryQaPolishInstaller
{
    public const string SettlementPath = "Assets/01_Scenes/Settlement.unity";
    public const string EffectsPath = "Assets/02_Scripts/Config/Dialogue/DialogueTextEffects.asset";

    [MenuItem("VOID SCRAPPER/Dialogue/Install Story QA Polish")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.path == SettlementPath && open.isDirty) throw new InvalidOperationException("Save unsaved Settlement work before authoring.");
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and exit Prefab Mode before authoring.");
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Localization import failed.");
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        DialogueDatabase database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        if (!Phase2CStoryDialogueInstaller.TryInstall(database, catalog, true, out string result)) throw new InvalidOperationException(result);
        AuthorHijackAudio();
        AuthorTextAnimation();
        AuthorSettlement(database, catalog);
        AssetDatabase.SaveAssets();
        Validate();
    }

    [MenuItem("VOID SCRAPPER/Dialogue/Validate Story QA Polish")]
    public static void Validate()
    {
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        var database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        if (!Phase2CStoryDialogueInstaller.TryValidateInstalled(database, catalog, out string result)) throw new InvalidOperationException(result);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePresentationValidator.DialogueUiPrefabPath);
        var typewriter = prefab.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
        if (typewriter.SettlementVoiceClip == null || AssetDatabase.GetAssetPath(typewriter.SettlementVoiceClip) != "Assets/06_Audio/SFX/Talk/Settlement_txt.wav")
            throw new InvalidOperationException("Settlement text voice must retain its existing asset.");
        if (typewriter.GetComponent<DialogueTextAnimationPresentation>() == null || typewriter.GetComponent<TextAnimator_TMP>() == null)
            throw new InvalidOperationException("Missing authored Text Animator adapter.");
        Debug.Log("Story QA localization, dialogue and text-voice/presentation validation passed.");
    }

    private static void AuthorHijackAudio()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(FinalAudioIntegrationInstaller.LibraryPath);
        var serialized = new SerializedObject(library);
        var entries = serialized.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("eventId").stringValue == SoundEventIds.DialogueCommHijack) return;
        int index = entries.arraySize++;
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("eventId").stringValue = SoundEventIds.DialogueCommHijack;
        entry.FindPropertyRelative("clips").arraySize = 0;
        entry.FindPropertyRelative("volume").floatValue = .5f;
        entry.FindPropertyRelative("pitchMin").floatValue = 1f;
        entry.FindPropertyRelative("pitchMax").floatValue = 1f;
        entry.FindPropertyRelative("loop").boolValue = false;
        entry.FindPropertyRelative("priority").intValue = 24;
        entry.FindPropertyRelative("spatialMode").enumValueIndex = (int)AudioSpatialMode.Force2D;
        entry.FindPropertyRelative("spatialBlend").floatValue = 0f;
        entry.FindPropertyRelative("useOcclusion").boolValue = false;
        entry.FindPropertyRelative("maxSimultaneousVoices").intValue = 1;
        entry.FindPropertyRelative("minimumRetriggerInterval").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorTextAnimation()
    {
        AnimationsDatabase effects = AssetDatabase.LoadAssetAtPath<AnimationsDatabase>(EffectsPath);
        if (effects == null)
        {
            effects = ScriptableObject.CreateInstance<AnimationsDatabase>();
            AssetDatabase.CreateAsset(effects, EffectsPath);
            var serialized = new SerializedObject(effects);
            var data = serialized.FindProperty("data");
            data.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Plugins/Febucci/Text Animator for Unity/Effects/Shake Effect.asset");
                var effect = Object.Instantiate(source);
                effect.name = i == 0 ? "Dialogue Corrupted Signal" : "Dialogue Channel Hijack";
                var config = new SerializedObject(effect);
                config.FindProperty("tagId").stringValue = i == 0 ? "vs_curse" : "vs_hijack";
                config.FindProperty("persistent.stateParams.amplitude").floatValue = i == 0 ? .22f : .45f;
                config.FindProperty("persistent.phaseParams.speed").floatValue = i == 0 ? .7f : 1.1f;
                config.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(effect, effects);
                data.GetArrayElementAtIndex(i).objectReferenceValue = effect;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject root = PrefabUtility.LoadPrefabContents(DialoguePresentationValidator.DialogueUiPrefabPath);
        try
        {
            foreach (DialogueSubtitleTypewriter writer in root.GetComponentsInChildren<DialogueSubtitleTypewriter>(true))
            {
                var animator = writer.GetComponent<TextAnimator_TMP>() ?? writer.gameObject.AddComponent<TextAnimator_TMP>();
                animator.enabled = false;
                animator.DatabaseEffects = effects;
                animator.sharedSettings = null;
                animator.localSettings.defaultBehaviorTags = Array.Empty<string>();
                animator.localSettings.defaultAppearanceTags = Array.Empty<string>();
                animator.localSettings.defaultDisappearanceTags = Array.Empty<string>();
                animator.localSettings.isAnimatingAppearances = false;
                animator.localSettings.isAnimatingDisappearances = false;
                var adapter = writer.GetComponent<DialogueTextAnimationPresentation>() ?? writer.gameObject.AddComponent<DialogueTextAnimationPresentation>();
                Bind(adapter, "textAnimator", animator);
                Bind(writer, "textAnimationPresentation", adapter);
            }
            PrefabUtility.SaveAsPrefabAsset(root, DialoguePresentationValidator.DialogueUiPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AuthorSettlement(DialogueDatabase database, LocalizationCatalog catalog)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(SettlementPath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(SettlementPath, OpenSceneMode.Additive);
        try
        {
            var owner = Find<SettlementUIController>(scene).Single();
            var ui = new SerializedObject(owner);
            Transform navigation = (Transform)ui.FindProperty("navigationRoot").objectReferenceValue;
            Button settings = (Button)ui.FindProperty("openSettingsPanelButton").objectReferenceValue;
            TMP_FontAsset font = settings.GetComponentInChildren<TMP_Text>(true).font;
            if (ui.FindProperty("openDialogueArchiveButton").objectReferenceValue == null)
            {
                Button archiveButton = Object.Instantiate(settings, navigation);
                archiveButton.name = "DialogueArchiveNavigationButton";
                archiveButton.onClick = new Button.ButtonClickedEvent();
                archiveButton.transform.SetSiblingIndex(settings.transform.GetSiblingIndex());
                Position((RectTransform)archiveButton.transform, new Vector2(-208f, -101f), new Vector2(62f, 16f));
                archiveButton.GetComponent<SettlementPrimaryNavigationPointer>().Configure(owner, 4);
                settings.GetComponent<SettlementPrimaryNavigationPointer>().Configure(owner, 5);
                var source = ui.FindProperty("settingsNavigationView");
                var destination = ui.FindProperty("archiveNavigationView");
                foreach (string field in new[] { "Background", "Icon", "ActiveStrip", "ActiveOutline", "Label" })
                {
                    Component original = (Component)source.FindPropertyRelative(field).objectReferenceValue;
                    string relative = AnimationUtility.CalculateTransformPath(original.transform, settings.transform);
                    Transform copy = string.IsNullOrEmpty(relative) ? archiveButton.transform : archiveButton.transform.Find(relative);
                    destination.FindPropertyRelative(field).objectReferenceValue = copy.GetComponent(original.GetType());
                }
                destination.FindPropertyRelative("Button").objectReferenceValue = archiveButton;
                var label = (TextMeshProUGUI)destination.FindPropertyRelative("Label").objectReferenceValue;
                label.text = "통신 기록";
                label.fontSize = 7.5f;
                ui.FindProperty("openDialogueArchiveButton").objectReferenceValue = archiveButton;
                // Make room for one compact slot without shrinking the read-only progress text.
                foreach (string field in new[] { "hangarNavigationButton", "openRepairPanelButton", "sectorTechnologyNavigationButton", "openTraitPanelButton" })
                    ((RectTransform)((Button)ui.FindProperty(field).objectReferenceValue).transform).anchoredPosition += Vector2.up * 18f;
                var progress = Find<SettlementProgressPanelUI>(scene).Single();
                ((RectTransform)progress.transform).anchoredPosition += Vector2.up * 18f;
            }
            if (ui.FindProperty("dialogueArchivePanel").objectReferenceValue == null)
            {
                GameObject root = Rect("DialogueArchivePanel", navigation, new Vector2(32f, 0f), new Vector2(400f, 248f));
                root.SetActive(false);
                root.AddComponent<Image>().color = new Color(.018f, .035f, .055f, .97f);
                var archive = root.AddComponent<SettlementDialogueArchivePanelUI>();
                Bind(archive, "database", database); Bind(archive, "localizationCatalog", catalog); Bind(archive, "owner", owner);
                Bind(archive, "inputActions", new SerializedObject(Find<SettlementSettingsPanel>(scene).First()).FindProperty("inputActions").objectReferenceValue);
                Bind(archive, "title", Label("Title", root.transform, font, "통신 기록", new Vector2(0f, 109f), new Vector2(380f, 18f), 12f));
                Bind(archive, "notice", Label("Notice", root.transform, font, "진행도 기반 기록 · 선택 응답 제외", new Vector2(0f, 90f), new Vector2(380f, 16f), 7f));
                ScrollRect list = Scroll("RecordList", root.transform, new Vector2(-138f, -8f), new Vector2(112f, 172f), out RectTransform listContent);
                var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.spacing = 2f;
                listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var archiveData = new SerializedObject(archive);
                var buttons = archiveData.FindProperty("recordButtons");
                var labels = archiveData.FindProperty("recordLabels");
                buttons.arraySize = labels.arraySize = SettlementDialogueArchivePanelUI.Records.Length;
                for (int i = 0; i < buttons.arraySize; i++)
                {
                    Button button = Button("Record_" + SettlementDialogueArchivePanelUI.Records[i].Key, listContent, font, SettlementDialogueArchivePanelUI.Records[i].Key, Vector2.zero, new Vector2(110f, 20f));
                    button.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
                    buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
                    labels.GetArrayElementAtIndex(i).objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
                }
                archiveData.ApplyModifiedPropertiesWithoutUndo();
                ScrollRect transcript = Scroll("Transcript", root.transform, new Vector2(59f, -8f), new Vector2(262f, 172f), out RectTransform textContent);
                TMP_Text text = textContent.gameObject.AddComponent<TextMeshProUGUI>();
                text.font = font; text.fontSize = 8f; text.color = new Color(.91f, .95f, 1f); text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
                text.margin = new Vector4(5f, 4f, 5f, 4f);
                textContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                Bind(archive, "transcript", text); Bind(archive, "transcriptScroll", transcript);
                Button back = Button("Back", root.transform, font, "돌아가기", new Vector2(137f, -110f), new Vector2(106f, 20f));
                Bind(archive, "backButton", back); Bind(archive, "backLabel", back.GetComponentInChildren<TMP_Text>());
                ui.FindProperty("dialogueArchivePanel").objectReferenceValue = archive;
                if (!archive.ValidateAuthoredReferences(out string error)) throw new InvalidOperationException(error);
            }
            ui.ApplyModifiedPropertiesWithoutUndo();
            RefreshAuthoredArchiveRecords((SettlementDialogueArchivePanelUI)ui.FindProperty("dialogueArchivePanel").objectReferenceValue, font);
            if (!Find<DebugItemGrantUI>(scene).Any())
            {
                GameObject debugRoot = new GameObject("DevelopmentQA");
                SceneManager.MoveGameObjectToScene(debugRoot, scene);
                debugRoot.transform.SetParent(owner.transform, false);
                var debug = debugRoot.AddComponent<DebugItemGrantUI>();
                Bind(debug, "traitCatalog", AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset"));
                string reinforcementPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ReinforcementCatalog").Single());
                Bind(debug, "reinforcementCatalog", AssetDatabase.LoadAssetAtPath<ReinforcementCatalog>(reinforcementPath));
                Bind(debug, "uiFont", font);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RefreshAuthoredArchiveRecords(SettlementDialogueArchivePanelUI archive, TMP_FontAsset font)
    {
        // Extend the authored list in place while retaining every existing button and tuning.
        Transform content = archive.transform.Find("RecordList/Content");
        var data = new SerializedObject(archive);
        var buttons = data.FindProperty("recordButtons");
        var labels = data.FindProperty("recordLabels");
        buttons.arraySize = labels.arraySize = SettlementDialogueArchivePanelUI.Records.Length;
        for (int i = 0; i < buttons.arraySize; i++)
        {
            string key = SettlementDialogueArchivePanelUI.Records[i].Key;
            Transform existing = content.Find("Record_" + key);
            Button button = existing != null ? existing.GetComponent<Button>() :
                Button("Record_" + key, content, font, key, Vector2.zero, new Vector2(110f, 20f));
            if (existing == null) button.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            button.transform.SetSiblingIndex(i);
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (!archive.ValidateAuthoredReferences(out string error)) throw new InvalidOperationException(error);
    }

    private static System.Collections.Generic.IEnumerable<T> Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void Bind(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var result = new GameObject(name, typeof(RectTransform));
        if (result.scene != parent.gameObject.scene) SceneManager.MoveGameObjectToScene(result, parent.gameObject.scene);
        result.transform.SetParent(parent, false);
        Position((RectTransform)result.transform, position, size);
        return result;
    }

    private static void Position(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, position, size).AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button Button(string name, Transform parent, TMP_FontAsset font, string label, Vector2 position, Vector2 size)
    {
        GameObject root = Rect(name, parent, position, size);
        Image image = root.AddComponent<Image>();
        Button button = root.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(.07f, .11f, .15f, 1f);
        colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
        colors.pressedColor = SettlementSelectionColors.SelectedBackground;
        button.colors = colors;
        TMP_Text text = Label("Label", root.transform, font, label, Vector2.zero, size - new Vector2(6f, 0f), 7.5f);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = new Vector2(-6f, 0f);
        return button;
    }

    private static ScrollRect Scroll(string name, Transform parent, Vector2 position, Vector2 size, out RectTransform content)
    {
        GameObject root = Rect(name, parent, position, size);
        root.AddComponent<Image>().color = new Color(.035f, .065f, .09f, 1f);
        root.AddComponent<RectMask2D>();
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f; scroll.viewport = (RectTransform)root.transform;
        content = (RectTransform)Rect("Content", root.transform, Vector2.zero, new Vector2(0f, size.y)).transform;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero; scroll.content = content;
        return scroll;
    }
}
