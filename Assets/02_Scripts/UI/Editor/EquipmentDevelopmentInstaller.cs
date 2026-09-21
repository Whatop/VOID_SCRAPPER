using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Focused, explicit scene authoring. Never runs in gameplay.</summary>
public static class EquipmentDevelopmentInstaller
{
    [MenuItem("VOID SCRAPPER/Settlement/Install Equipment Development")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before authoring.");
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        var ships = new[] { "01_basic_ship", "02_shotgun_ship", "03_sniper_ship" }
            .Select(name => AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/" + name + ".asset")).ToArray();
        for (int i = 0; i < ships.Length; i++)
        {
            var data = new SerializedObject(ships[i]);
            data.FindProperty("requiredAnalyzedComponents").intValue = i;
            data.FindProperty("requiredScrapParts").intValue = 0;
            data.FindProperty("requiredCoreShards").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Localization import failed.");
        var localization = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        AuthorScene("Boot", scene =>
        {
            foreach (var progress in Find<PermanentProgress>(scene))
            {
                Bind(progress, "equipmentCatalog", catalog);
                var data = new SerializedObject(progress);
                var list = data.FindProperty("equipmentShips"); list.arraySize = ships.Length;
                for (int i = 0; i < ships.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = ships[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        });
        AuthorScene("Settlement", scene =>
        {
            var panel = Find<ShipTraitTreePanel>(scene).Single();
            var owner = Find<SettlementUIController>(scene).Single();
            var data = new SerializedObject(panel);
            Button sidebar = (Button)data.FindProperty("sidebarButton").objectReferenceValue;
            TMP_FontAsset font = sidebar.GetComponentInChildren<TMP_Text>(true).font;
            var ownerData = new SerializedObject(owner);
            ((TMP_Text)ownerData.FindProperty("traitNavigationView.Label").objectReferenceValue).text = "장비 개발";
            if (data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue == null)
            {
                // Preserve legacy authored controls and their Inspector values for migration; retire their presentation.
                foreach (Transform child in panel.transform) child.gameObject.SetActive(false);
                var root = Rect("EquipmentDevelopment", panel.transform, Vector2.zero, new Vector2(400f, 248f));
                root.AddComponent<Image>().color = new Color(.018f, .035f, .055f, .98f);
                Bind(panel, "equipmentDevelopmentRoot", root);
                Bind(panel, "equipmentHeading", Label("Heading", root.transform, font, "장비 개발", new Vector2(0, 111), new Vector2(382, 18), 12));
                Bind(panel, "equipmentHint", Label("Hint", root.transform, font, "슬롯 선택 → 장비 준비 · 탐사에서 획득/강화", new Vector2(0, 92), new Vector2(386, 16), 7));
                data.Update();
                var slots = data.FindProperty("equipmentSlots"); slots.arraySize = 12;
                var research = data.FindProperty("equipmentResearchLabels"); research.arraySize = 4;
                Color[] accents = { Color.white, ships[0].ResearchAccent, ships[1].ResearchAccent, ships[2].ResearchAccent };
                for (int row = 0; row < 4; row++)
                {
                    TMP_Text rowLabel = Label("ResearchRow" + row, root.transform, font, "", new Vector2(-111, 71 - row * 42), new Vector2(170, 12), 7);
                    rowLabel.color = accents[row]; research.GetArrayElementAtIndex(row).objectReferenceValue = rowLabel;
                    for (int col = 0; col < 3; col++)
                    {
                        int index = row * 3 + col;
                        var button = Button("Slot" + index, root.transform, font, "비어 있음", new Vector2(-168 + col * 57, 52 - row * 42), new Vector2(54, 28));
                        BindView(slots.GetArrayElementAtIndex(index), button, null);
                    }
                }
                Scroll("Catalog", root.transform, new Vector2(28, -5), new Vector2(104, 174), out RectTransform content);
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.spacing = 2;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var definitions = catalog.TraitDefinitions.Where(t => t != null && t.CanAppearAsRandomDropTrait).Distinct().ToArray();
                var candidates = data.FindProperty("equipmentCandidates"); candidates.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                {
                    var button = Button(definitions[i].TraitId, content, font, definitions[i].DisplayName, Vector2.zero, new Vector2(100, 26));
                    button.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                    BindView(candidates.GetArrayElementAtIndex(i), button, definitions[i]);
                }
                Scroll("Effects", root.transform, new Vector2(140, -5), new Vector2(110, 174), out RectTransform effects);
                var details = effects.gameObject.AddComponent<TextMeshProUGUI>(); details.font = font; details.fontSize = 7.5f;
                details.color = Color.white; details.raycastTarget = false; details.textWrappingMode = TextWrappingModes.Normal;
                details.margin = new Vector4(4, 3, 4, 3); details.alignment = TextAlignmentOptions.TopLeft;
                effects.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                data.FindProperty("equipmentDetails").objectReferenceValue = details;
                var clear = Button("ClearSlot", root.transform, font, "슬롯 비우기", new Vector2(-112, -111), new Vector2(160, 18));
                data.FindProperty("clearEquipmentButton").objectReferenceValue = clear;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            data.Update(); data.FindProperty("equipmentDevelopmentMode").boolValue = true;
            data.FindProperty("equipmentLocalization").objectReferenceValue = localization;
            // Retire the two stale ship-count preparation locks without changing their TraitDefinitions.
            var nodes = data.FindProperty("branchNodes");
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                string id = node.FindPropertyRelative("nodeId").stringValue;
                if (id == "mg_guidance_control" || id == "mg_stable_feed") node.FindPropertyRelative("requiredUnlockedShipCount").intValue = 0;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            // Reserve the existing left navigation, currency header and launch footer.
            var equipmentRoot = (GameObject)data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue;
            var equipmentRect = (RectTransform)equipmentRoot.transform;
            equipmentRect.anchoredPosition = new Vector2(32, 0); equipmentRect.sizeDelta = new Vector2(400, 224);
            ((RectTransform)equipmentRoot.transform.Find("Heading")).anchoredPosition = new Vector2(0, 95);
            ((RectTransform)equipmentRoot.transform.Find("Hint")).anchoredPosition = new Vector2(0, 78);
            for (int row = 0; row < 4; row++)
            {
                ((RectTransform)equipmentRoot.transform.Find("ResearchRow" + row)).anchoredPosition = new Vector2(-111, 58 - row * 36);
                for (int col = 0; col < 3; col++)
                {
                    var slotRect = (RectTransform)equipmentRoot.transform.Find("Slot" + (row * 3 + col));
                    slotRect.anchoredPosition = new Vector2(-168 + col * 57, 38 - row * 36);
                    slotRect.sizeDelta = new Vector2(54, 26);
                }
            }
            foreach (string name in new[] { "Catalog", "Effects" })
            {
                var scrollRect = (RectTransform)equipmentRoot.transform.Find(name);
                scrollRect.anchoredPosition = new Vector2(name == "Catalog" ? 28 : 140, -14);
                scrollRect.sizeDelta = new Vector2(name == "Catalog" ? 104 : 110, 158);
            }
            ((RectTransform)equipmentRoot.transform.Find("ClearSlot")).anchoredPosition = new Vector2(-112, -94);
            RefinePanel(panel, owner, catalog);
            var errors = new System.Collections.Generic.List<string>();
            if (!panel.ValidateEquipmentPresentation(errors)) throw new InvalidOperationException(string.Join("\n", errors));
        });
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("Equipment Development authored in Boot and Settlement; localization validated.");
    }

    [MenuItem("VOID SCRAPPER/Settlement/Refine Equipment Details")]
    public static void RefineDetails()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before authoring.");
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Localization import failed.");
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        AuthorScene("Settlement", scene => RefinePanel(Find<ShipTraitTreePanel>(scene).Single(), Find<SettlementUIController>(scene).Single(), catalog));
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("Equipment details refined in place; Settlement references and localization validated.");
    }

    private static void RefinePanel(ShipTraitTreePanel panel, SettlementUIController owner, TraitCatalog catalog)
    {
        var data = new SerializedObject(panel);
        var root = (GameObject)data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue;
        if (root == null) throw new InvalidOperationException("Install the authored Equipment Development foundation first.");
        TMP_FontAsset font = ((TMP_Text)data.FindProperty("equipmentHeading").objectReferenceValue).font;
        SetRect(root.transform.Find("Heading"), new Vector2(-66, 95), new Vector2(252, 18));
        SetRect(root.transform.Find("Hint"), new Vector2(-66, 78), new Vector2(252, 14));
        for (int row = 0; row < 4; row++)
        {
            SetRect(root.transform.Find("ResearchRow" + row), new Vector2(-132, 60 - row * 35), new Vector2(132, 12));
            for (int col = 0; col < 3; col++)
                SetRect(root.transform.Find("Slot" + (row * 3 + col)), new Vector2(-176 + col * 44, 43 - row * 35), new Vector2(42, 25));
        }
        SetRect(root.transform.Find("Catalog"), new Vector2(-16, -15), new Vector2(96, 172));
        root.transform.Find("Effects").gameObject.SetActive(false); // Preserve the old serialized text, retire its paragraph presentation.
        root.transform.Find("ClearSlot").gameObject.SetActive(false);
        var ownerData = new SerializedObject(owner);
        var legacyAction = ownerData.FindProperty("traitActionButton").objectReferenceValue as Button;
        if (legacyAction != null) legacyAction.gameObject.SetActive(false);

        if (root.transform.Find("Inspection") == null)
        {
            var inspection = Rect("Inspection", root.transform, Vector2.zero, new Vector2(400, 224));
            var icon = Rect("Icon", inspection.transform, new Vector2(50, 93), new Vector2(18, 18)).AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            Bind(panel, "equipmentIcon", icon);
            Bind(panel, "equipmentName", Label("Name", inspection.transform, font, "", new Vector2(130, 95), new Vector2(126, 17), 10));
            Bind(panel, "equipmentCompatibility", Label("CompatibilityTag", inspection.transform, font, "", new Vector2(130, 82), new Vector2(126, 10), 7));
            Bind(panel, "equipmentMaxLevel", Label("MaxLevel", inspection.transform, font, "", new Vector2(118, 69), new Vector2(156, 12), 7.5f));
            Scroll("Growth", inspection.transform, new Vector2(118, -7), new Vector2(156, 136), out RectTransform content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 3, 3); layout.spacing = 3;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var description = FlowLabel("Description", content, font, 8);
            Bind(panel, "equipmentDetails", description);
            Bind(panel, "equipmentGrowthHeading", FlowLabel("GrowthHeading", content, font, 8));
            Bind(panel, "equipmentCandidateState", Label("CandidateState", inspection.transform, font, "", new Vector2(118, -84), new Vector2(156, 16), 7.5f));
            Bind(panel, "equipmentActivationButton", Button("Activation", inspection.transform, font, "", new Vector2(118, -103), new Vector2(150, 18)));
        }
        // Author reusable rows up to the actual catalog maximum. Runtime only populates/shows rows.
        Transform growth = root.transform.Find("Inspection/Growth/Content");
        Bind(panel, "equipmentGrowthScroll", growth.parent.GetComponent<ScrollRect>());
        SetRect(root.transform.Find("Inspection/Icon"), new Vector2(50, 98), new Vector2(18, 18));
        SetRect(root.transform.Find("Inspection/Name"), new Vector2(130, 99), new Vector2(126, 17));
        SetRect(root.transform.Find("Inspection/CompatibilityTag"), new Vector2(130, 87), new Vector2(126, 10));
        SetRect(root.transform.Find("Inspection/MaxLevel"), new Vector2(118, 76), new Vector2(156, 12));
        SetRect(growth.parent, new Vector2(118, 9), new Vector2(156, 118));
        SetRect(root.transform.Find("Inspection/CandidateState"), new Vector2(118, -59), new Vector2(156, 14));
        SetRect(root.transform.Find("Inspection/Activation"), new Vector2(118, -81), new Vector2(150, 18));
        int maximum = catalog.TraitDefinitions.Where(t => t != null && t.CanAppearAsRandomDropTrait).Max(t => t.MaxLevel);
        data.Update();
        var rows = data.FindProperty("equipmentGrowthRows"); rows.arraySize = maximum;
        for (int i = 0; i < maximum; i++)
        {
            Transform row = growth.Find("Level" + (i + 1));
            if (row == null)
            {
                row = Rect("Level" + (i + 1), growth, Vector2.zero, new Vector2(146, 30)).transform;
                var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 1;
                FlowLabel("Heading", row, font, 8).color = new Color(.7f, .86f, 1);
                FlowLabel("Effects", row, font, 8);
            }
            var entry = rows.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
            entry.FindPropertyRelative("heading").objectReferenceValue = row.Find("Heading").GetComponent<TMP_Text>();
            entry.FindPropertyRelative("effects").objectReferenceValue = row.Find("Effects").GetComponent<TMP_Text>();
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        var errors = new System.Collections.Generic.List<string>();
        if (!panel.ValidateEquipmentPresentation(errors)) throw new InvalidOperationException(string.Join("\n", errors));
    }

    private static TMP_Text FlowLabel(string name, Transform parent, TMP_FontAsset font, float size)
    {
        TMP_Text label = Label(name, parent, font, "", Vector2.zero, new Vector2(146, 12), size);
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static void SetRect(Transform target, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)target; rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    private static void BindView(SerializedProperty view, Button button, TraitDefinition definition)
    {
        view.FindPropertyRelative("button").objectReferenceValue = button;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        label.rectTransform.offsetMin = new Vector2(13, 1); label.rectTransform.offsetMax = new Vector2(-2, -1);
        view.FindPropertyRelative("label").objectReferenceValue = label;
        var icon = Rect("Icon", button.transform, Vector2.zero, new Vector2(10, 10)).AddComponent<Image>();
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f); icon.rectTransform.anchoredPosition = new Vector2(7, 0);
        icon.raycastTarget = false; icon.preserveAspect = true; icon.sprite = definition?.Icon; icon.enabled = icon.sprite != null;
        view.FindPropertyRelative("icon").objectReferenceValue = icon;
        view.FindPropertyRelative("definition").objectReferenceValue = definition;
    }

    private static void AuthorScene(string name, Action<Scene> author)
    {
        Scene previous = SceneManager.GetActiveScene();
        string path = "Assets/01_Scenes/" + name + ".unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { author(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static void Bind(Object owner, string field, Object value)
    {
        var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (go.scene != parent.gameObject.scene) SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        return go;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, position, size).AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
        text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    private static Button Button(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size)
    {
        var go = Rect(name, parent, position, size); var image = go.AddComponent<Image>(); var button = go.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = new Color(.07f, .11f, .15f); colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
        colors.pressedColor = SettlementSelectionColors.SelectedBackground; button.colors = colors;
        var label = Label("Label", go.transform, font, value, Vector2.zero, size, 7);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.sizeDelta = new Vector2(-4, 0);
        return button;
    }
    private static void Scroll(string name, Transform parent, Vector2 position, Vector2 size, out RectTransform content)
    {
        var go = Rect(name, parent, position, size); go.AddComponent<Image>().color = new Color(.035f, .065f, .09f);
        go.AddComponent<RectMask2D>(); var scroll = go.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 18; scroll.viewport = (RectTransform)go.transform;
        content = (RectTransform)Rect("Content", go.transform, Vector2.zero, new Vector2(0, size.y)).transform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero; scroll.content = content;
    }
}
