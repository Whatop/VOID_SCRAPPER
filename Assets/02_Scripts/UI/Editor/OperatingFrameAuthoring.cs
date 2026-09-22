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

// Focused, idempotent authoring only. No menu installer or runtime hierarchy fallback.
public static class OperatingFrameAuthoring
{
    public const string MenuPrefab = "Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab";

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before frame authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before frame authoring.");
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Operating Frame localization import failed.");
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/01_Scenes/Settlement.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Settlement.unity", OpenSceneMode.Additive);
        try
        {
            ShipTraitTreePanel panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShipTraitTreePanel>(true)).Single();
            AuthorSettlement(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        GameObject prefab = PrefabUtility.LoadPrefabContents(MenuPrefab);
        try
        {
            AuthorInventory(prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true));
            PrefabUtility.SaveAsPrefabAsset(prefab, MenuPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        // Expedition currently owns an unpacked copy; Tutorial inherits the shared prefab.
        Scene expedition = SceneManager.GetSceneByPath("Assets/01_Scenes/Expedition.unity");
        bool openedExpedition = !expedition.IsValid() || !expedition.isLoaded;
        if (openedExpedition) expedition = EditorSceneManager.OpenScene("Assets/01_Scenes/Expedition.unity", OpenSceneMode.Additive);
        try
        {
            var inventory = expedition.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBuildStatusPanelUI>(true)).Single();
            AuthorInventory(inventory);
            EditorSceneManager.MarkSceneDirty(expedition);
            EditorSceneManager.SaveScene(expedition);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (openedExpedition) EditorSceneManager.CloseScene(expedition, true);
        }
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("Operating Frame authoring passed: Settlement frame controls and shared inventory inspection.");
    }

    public static void AuthorSettlement(ShipTraitTreePanel panel)
    {
        var data = new SerializedObject(panel);
        var root = data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue as GameObject;
        if (root == null) throw new InvalidOperationException("Settlement/ShipTraitTreePanel.equipmentDevelopmentRoot missing.");
        TMP_Text heading = data.FindProperty("equipmentHeading").objectReferenceValue as TMP_Text;
        if (heading == null) throw new InvalidOperationException("Equipment Development heading/font is missing.");
        TMP_FontAsset font = heading.font;
        SetRect(heading.transform, new Vector2(-82, 102), new Vector2(230, 13));
        var frameRoot = Rect("OperatingFrames", root.transform, new Vector2(-82, 77), new Vector2(230, 34));
        var frameHeading = Label("Heading", frameRoot.transform, font, "운용 프레임", new Vector2(0, 10), new Vector2(230, 10), 8);
        var buttons = data.FindProperty("operatingFrameButtons"); buttons.arraySize = 3;
        var labels = data.FindProperty("operatingFrameLabels"); labels.arraySize = 3;
        string[] names = { "Lightweight", "Standard", "Heavy" };
        OperatingFrameType[] types = { OperatingFrameType.Lightweight, OperatingFrameType.Standard, OperatingFrameType.Heavy };
        var localization = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        for (int i = 0; i < 3; i++)
        {
            Button button = MakeButton(names[i], frameRoot.transform, font, OperatingFrameText.Name(types[i], localization),
                new Vector2(-73 + i * 73, -5), new Vector2(70, 18));
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
        }
        data.FindProperty("operatingFrameHeading").objectReferenceValue = frameHeading;
        string[] tabs = { "sharedTabButton", "machineGunTabButton", "shotgunTabButton", "sniperTabButton" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = data.FindProperty(tabs[i]).objectReferenceValue as Component;
            SetRect(tab != null ? tab.transform : null, new Vector2(-170 + i * 56, 50), new Vector2(54, 17));
        }
        for (int row = 0; row < 4; row++)
        {
            SetRect(root.transform.Find("ResearchRow" + row), new Vector2(-82, 34 - row * 31), new Vector2(220, 10));
            for (int col = 0; col < 3; col++)
                SetRect(root.transform.Find("Slot" + (row * 3 + col)), new Vector2(-166 + col * 73, 19 - row * 31), new Vector2(70, 23));
        }
        SetRect(root.transform.Find("Catalog"), new Vector2(-82, -25), new Vector2(220, 118));
        SetRect(root.transform.Find("ClearSlot"), new Vector2(-82, -94), new Vector2(220, 13));
        SetRect(root.transform.Find("Hint"), new Vector2(-82, -107), new Vector2(230, 9));
        ((TMP_Text)data.FindProperty("equipmentHint").objectReferenceValue).fontSize = 7;
        data.ApplyModifiedPropertiesWithoutUndo();
        panel.RefreshPanel();
        var errors = new List<string>();
        if (!panel.ValidateEquipmentPresentation(errors)) throw new InvalidOperationException(string.Join("\n", errors));
    }

    private static void AuthorInventory(PlayerBuildStatusPanelUI panel)
    {
        if (panel == null) throw new InvalidOperationException("Inventory prefab is missing PlayerBuildStatusPanelUI.");
        var data = new SerializedObject(panel);
        var title = data.FindProperty("shipNameText").objectReferenceValue as TextMeshProUGUI;
        if (title == null) throw new InvalidOperationException("Inventory shipNameText/header is missing.");
        TMP_FontAsset font = title.font;
        title.fontSize = 7.5f;
        title.raycastTarget = true;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        SetRect(title.transform, new Vector2(-144, 0), new Vector2(120, 20));
        Button inspect = title.GetComponent<Button>();
        if (inspect == null) inspect = title.gameObject.AddComponent<Button>();
        inspect.targetGraphic = title;
        ColorBlock colors = inspect.colors; colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.Hover;
        inspect.colors = colors;
        Transform inventory = title.transform.parent.parent;
        var overlay = Rect("OperatingFrameInspection", inventory, Vector2.zero, new Vector2(418, 240));
        Image shade = GetOrAdd<Image>(overlay); shade.color = new Color(0, .015f, .03f, .8f);
        var box = Rect("Panel", overlay.transform, Vector2.zero, new Vector2(270, 180));
        GetOrAdd<Image>(box).color = new Color(.025f, .06f, .09f, 1);
        TMP_Text text = Label("Details", box.transform, font, "", new Vector2(0, 14), new Vector2(246, 132), 8.5f);
        text.alignment = TextAlignmentOptions.TopLeft;
        Button close = MakeButton("Close", box.transform, font, "닫기", new Vector2(0, -74), new Vector2(108, 18));
        data.FindProperty("operatingFrameLocalization").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        data.FindProperty("operatingFrameInspectButton").objectReferenceValue = inspect;
        data.FindProperty("operatingFrameInspectionRoot").objectReferenceValue = overlay;
        data.FindProperty("operatingFrameInspectionText").objectReferenceValue = text;
        data.FindProperty("operatingFrameCloseButton").objectReferenceValue = close;
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.SetActive(false);
        if (!panel.HasOperatingFramePresentation) throw new InvalidOperationException("Authored frame inspection references are incomplete.");
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            if (go.scene != parent.gameObject.scene) SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
        }
        SetRect(go.transform, position, size);
        return go;
    }

    private static void SetRect(Transform transform, Vector2 position, Vector2 size)
    {
        if (!(transform is RectTransform rect)) throw new InvalidOperationException("Missing authored Equipment Development rect.");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = GetOrAdd<TextMeshProUGUI>(Rect(name, parent, position, size));
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static Button MakeButton(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size)
    {
        var go = Rect(name, parent, position, size);
        var image = GetOrAdd<Image>(go); image.color = Color.white;
        var button = GetOrAdd<Button>(go); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = new Color(.07f, .11f, .15f);
        colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
        colors.pressedColor = SettlementSelectionColors.SelectedBackground; button.colors = colors;
        Label("Label", go.transform, font, text, Vector2.zero, size - new Vector2(4, 0), 8);
        return button;
    }
}
