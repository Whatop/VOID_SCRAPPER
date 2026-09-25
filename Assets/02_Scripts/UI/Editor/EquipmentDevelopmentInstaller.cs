using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit in-place authoring entry point. No runtime fallback or retired UI installer menu.</summary>
public static class EquipmentDevelopmentInstaller
{
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before authoring.");
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        if (catalog == null) throw new InvalidOperationException("Missing TraitCatalog_Main.asset");
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Localization import failed.");
        AuthorScene("Settlement", scene =>
        {
            ShipTraitTreePanel panel = Find<ShipTraitTreePanel>(scene).Single();
            SettlementUIController owner = Find<SettlementUIController>(scene).Single();
            AuthorMetadata(catalog, panel);
            AuthorPanel(panel, owner);
        });
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("Equipment correction authored: four research branches, manufacturing metadata, fitting presentation. Combat data unchanged.");
    }

    public static void RefineDetails() => Install();

    private static void AuthorMetadata(TraitCatalog catalog, ShipTraitTreePanel panel)
    {
        EquipmentFinalRosterAuthoring.ApplyMetadata(catalog);
    }

    private static void AuthorPanel(ShipTraitTreePanel panel, SettlementUIController owner)
    {
        var data = new SerializedObject(panel);
        var root = data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue as GameObject;
        if (root == null) throw new InvalidOperationException("Settlement ShipTraitTreePanel.equipmentDevelopmentRoot is missing. Restore the existing authored panel before correction.");
        TMP_FontAsset font = ((TMP_Text)data.FindProperty("equipmentHeading").objectReferenceValue).font;
        SetRect(root.transform.Find("Heading"), new Vector2(-80, 99), new Vector2(230, 16));
        SetRect(root.transform.Find("Hint"), new Vector2(-80, 84), new Vector2(230, 14));
        var fields = new[] { "sharedTabButton", "machineGunTabButton", "shotgunTabButton", "sniperTabButton" };
        var names = new[] { "공용", "스위퍼", "브리처", "랜서" };
        for (int i = 0; i < fields.Length; i++)
        {
            var tab = data.FindProperty(fields[i]).objectReferenceValue as ShipTraitBranchTabButton;
            if (tab == null) throw new InvalidOperationException("Settlement ShipTraitTreePanel." + fields[i] + " is missing; repair the authored tab.");
            tab.transform.SetParent(root.transform, false);
            SetRect(tab.transform, new Vector2(-170 + i * 56, 64), new Vector2(54, 18));
            tab.gameObject.SetActive(true);
            var tabData = new SerializedObject(tab);
            tabData.FindProperty("useSelectionTween").boolValue = false;
            tabData.FindProperty("selectedYOffset").floatValue = 0;
            tabData.FindProperty("allowClickWhenLocked").boolValue = true;
            tabData.FindProperty("selectedTextColor").colorValue = SettlementSelectionColors.Selected;
            tabData.FindProperty("selectedLineColor").colorValue = SettlementSelectionColors.Selected;
            tabData.ApplyModifiedPropertiesWithoutUndo();
            foreach (TMP_Text text in tab.GetComponentsInChildren<TMP_Text>(true)) text.fontSize = 8;
            tab.SetLabel(names[i]);
        }
        for (int row = 0; row < 4; row++)
        {
            SetRect(root.transform.Find("ResearchRow" + row), new Vector2(-82, 46 - row * 33), new Vector2(220, 10));
            for (int col = 0; col < 3; col++)
                SetRect(root.transform.Find("Slot" + (row * 3 + col)), new Vector2(-166 + col * 73, 31 - row * 33), new Vector2(70, 24));
        }
        SetRect(root.transform.Find("Catalog"), new Vector2(-82, -15), new Vector2(220, 137));
        Bind(panel, "equipmentLegacyScroll", root.transform.Find("Catalog").GetComponent<ScrollRect>());
        root.transform.Find("Catalog").gameObject.SetActive(false);
        SetRect(root.transform.Find("ClearSlot"), new Vector2(-139, -94), new Vector2(106, 13));
        Button special = root.transform.Find("ResearchSpecial")?.GetComponent<Button>();
        if (special == null)
            special = Button("ResearchSpecial", root.transform, font, "특수 연구 장비", new Vector2(-25, -94), new Vector2(106, 13));
        Bind(panel, "researchSpecialEquipmentButton", special);
        root.transform.Find("ClearSlot").gameObject.SetActive(false);
        root.transform.Find("Effects").gameObject.SetActive(false);
        Transform growth = root.transform.Find("Inspection/Growth/Content");
        if (growth == null) throw new InvalidOperationException("Settlement/EquipmentDevelopment/Inspection/Growth/Content is missing.");
        TMP_Text requirements = growth.Find("Requirements")?.GetComponent<TMP_Text>();
        if (requirements == null)
        {
            requirements = Label("Requirements", growth, font, "", Vector2.zero, new Vector2(146, 12), 7.5f);
            requirements.alignment = TextAlignmentOptions.TopLeft;
        }
        requirements.transform.SetSiblingIndex(1);
        Bind(panel, "equipmentRequirements", requirements);
        SetRect(root.transform.Find("Inspection/CandidateState"), new Vector2(118, -60), new Vector2(156, 20));
        data.Update();
        foreach (string field in new[] { "equipmentSlots", "equipmentCandidates" })
        {
            var views = data.FindProperty(field);
            for (int i = 0; i < views.arraySize; i++)
            {
                var view = views.GetArrayElementAtIndex(i);
                var button = view.FindPropertyRelative("button").objectReferenceValue as Button;
                if (button == null) throw new InvalidOperationException("Missing " + field + "[" + i + "].button");
                TMP_Text indicator = button.transform.Find("FittedIndicator")?.GetComponent<TMP_Text>();
                if (indicator == null) indicator = Label("FittedIndicator", button.transform, font, "", Vector2.zero, new Vector2(24, 8), 6.5f);
                var rect = indicator.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0); rect.anchoredPosition = new Vector2(-2, 1);
                indicator.alignment = TextAlignmentOptions.BottomRight;
                view.FindPropertyRelative("fittedIndicator").objectReferenceValue = indicator;
                TMP_Text label = view.FindPropertyRelative("label").objectReferenceValue as TMP_Text;
                label.fontSize = 7.5f; label.margin = new Vector4(11, 0, 1, 6);
                if (field == "equipmentSlots") view.FindPropertyRelative("definition").objectReferenceValue = null;
            }
        }
        data.FindProperty("equipmentLocalization").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        data.FindProperty("equipmentDevelopmentMode").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        var ownerData = new SerializedObject(owner);
        var upgrade = ownerData.FindProperty("traitActionButton").objectReferenceValue as Button;
        if (upgrade != null) upgrade.gameObject.SetActive(false);
        StructuralFrameAuthoring.AuthorSettlement(panel);
        var errors = new List<string>();
        if (!panel.ValidateEquipmentPresentation(errors)) throw new InvalidOperationException(string.Join("\n", errors));
    }

    private static void SetRect(Transform target, Vector2 position, Vector2 size)
    {
        if (target == null) throw new InvalidOperationException("Missing existing Equipment Development RectTransform; restore authored hierarchy.");
        var rect = (RectTransform)target;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
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
