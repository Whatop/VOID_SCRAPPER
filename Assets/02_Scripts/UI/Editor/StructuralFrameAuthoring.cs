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
public static class StructuralFrameAuthoring
{
    public const string MenuPrefab = "Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab";

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before frame authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before frame authoring.");
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Structural Frame localization import failed.");
        AuthorModules();
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
        Debug.Log("Structural Frame authoring passed: three equipment modules, retired selector and shared inventory inspection.");
    }

    public static void AuthorModules()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        if (catalog == null) throw new InvalidOperationException("Missing equipment catalog.");
        string[] ids = { StructuralFrameProfile.LightweightId, StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId };
        string[] names = { "경량 프레임", "표준 프레임", "중갑 프레임" };
        string[] descriptions = { "내구성과 적재량을 줄여 기동성을 극대화합니다.", "전투와 자원 회수, 적재 성능을 안정적으로 보조합니다.", "기동성을 줄여 내구성과 장거리 탐사 적재량을 높입니다." };
        string[] iconSources = { "shared_engine_tuning", "shared_cargo_bay", "shared_reinforced_plating" };
        int[] scrap = { 20, 18, 18 }, core = { 0, 0, 1 };
        for (int i = 0; i < ids.Length; i++)
        {
            string path = "Assets/02_Scripts/Config/TraitDefinition/Common/" + (67 + i) + "_" + ids[i] + ".asset";
            var matches = AssetDatabase.FindAssets("t:TraitDefinition").Select(g => AssetDatabase.LoadAssetAtPath<TraitDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null && t.TraitId == ids[i]).ToArray();
            if (matches.Length > 1 || (matches.Length == 1 && AssetDatabase.GetAssetPath(matches[0]) != path))
                throw new InvalidOperationException("Structural equipment ID collision: " + ids[i]);
            TraitDefinition trait = matches.SingleOrDefault();
            if (trait == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Occupied structural asset path: " + path);
                trait = ScriptableObject.CreateInstance<TraitDefinition>();
                var data = new SerializedObject(trait);
                data.FindProperty("traitId").stringValue = ids[i];
                data.FindProperty("displayName").stringValue = names[i];
                data.FindProperty("description").stringValue = descriptions[i];
                data.FindProperty("icon").objectReferenceValue = catalog.FindById(iconSources[i]).Icon;
                data.FindProperty("category").intValue = (int)TraitCategory.Shared;
                data.FindProperty("rarity").intValue = (int)TraitRarity.Common;
                data.FindProperty("maxLevel").intValue = 1;
                // Individual effects must stay empty. The immutable exact-set profile owns every modifier.
                data.FindProperty("levelEffects").arraySize = 0;
                data.FindProperty("developmentRoster").boolValue = true;
                data.FindProperty("developmentResearchTier").intValue = 1;
                data.FindProperty("developmentDisplayOrder").intValue = i;
                data.FindProperty("manufacturingScrapCost").intValue = scrap[i];
                data.FindProperty("manufacturingCoreCost").intValue = core[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(trait, path);
            }
            if (catalog.FindById(ids[i]) == null)
            {
                var data = new SerializedObject(catalog);
                var definitions = data.FindProperty("traitDefinitions");
                int index = definitions.arraySize++;
                definitions.GetArrayElementAtIndex(index).objectReferenceValue = trait;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        foreach (string id in new[] { "shared_salvage_protocol", "shared_reinforced_plating", "shared_repair_foam" })
        {
            var data = new SerializedObject(catalog.FindById(id));
            data.FindProperty("developmentRoster").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
    }

    public static void AuthorSettlement(ShipTraitTreePanel panel)
    {
        var data = new SerializedObject(panel);
        var root = data.FindProperty("equipmentDevelopmentRoot").objectReferenceValue as GameObject;
        if (root == null) throw new InvalidOperationException("Settlement/ShipTraitTreePanel.equipmentDevelopmentRoot missing.");
        TMP_Text heading = data.FindProperty("equipmentHeading").objectReferenceValue as TMP_Text;
        if (heading == null) throw new InvalidOperationException("Equipment Development heading/font is missing.");
        SetRect(heading.transform, new Vector2(-82, 102), new Vector2(230, 13));
        // Remove the retired selector and its listeners, rather than disabling a second authority.
        Transform oldSelector = root.transform.Find("OperatingFrames");
        if (oldSelector != null) Object.DestroyImmediate(oldSelector.gameObject);
        string[] tabs = { "sharedTabButton", "machineGunTabButton", "shotgunTabButton", "sniperTabButton" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = data.FindProperty(tabs[i]).objectReferenceValue as Component;
            SetRect(tab != null ? tab.transform : null, new Vector2(i == 0 ? -137 : -27, 78), new Vector2(106, 18));
        }
        for (int row = 0; row < 4; row++)
        {
            SetRect(root.transform.Find("ResearchRow" + row), new Vector2(-82, 54 - row * 36), new Vector2(220, 10));
            for (int col = 0; col < 3; col++)
                SetRect(root.transform.Find("Slot" + (row * 3 + col)), new Vector2(-166 + col * 73, 36 - row * 36), new Vector2(70, 25));
        }
        SetRect(root.transform.Find("Catalog"), new Vector2(-82, -25), new Vector2(220, 118));
        Transform special = root.transform.Find("ResearchSpecial");
        SetRect(root.transform.Find("ClearSlot"), new Vector2(special != null ? -139 : -82, -94), new Vector2(special != null ? 106 : 220, 13));
        if (special != null) SetRect(special, new Vector2(-25, -94), new Vector2(106, 13));
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
        title.fontSize = 7f;
        title.raycastTarget = true;
        title.textWrappingMode = TextWrappingModes.Normal;
        SetRect(title.transform, new Vector2(-131, 0), new Vector2(156, 20));
        Button inspect = title.GetComponent<Button>();
        if (inspect == null) inspect = title.gameObject.AddComponent<Button>();
        inspect.targetGraphic = title;
        ColorBlock colors = inspect.colors; colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.Hover;
        inspect.colors = colors;
        Transform inventory = title.transform.parent.parent;
        Transform previous = inventory.Find("OperatingFrameInspection");
        if (previous != null) previous.name = "StructuralFrameInspection";
        var overlay = Rect("StructuralFrameInspection", inventory, Vector2.zero, new Vector2(418, 240));
        Image shade = GetOrAdd<Image>(overlay); shade.color = new Color(0, .015f, .03f, .8f);
        var box = Rect("Panel", overlay.transform, Vector2.zero, new Vector2(270, 210));
        GetOrAdd<Image>(box).color = new Color(.025f, .06f, .09f, 1);
        TMP_Text text = Label("Details", box.transform, font, "", new Vector2(0, 12), new Vector2(246, 166), 8.5f);
        text.alignment = TextAlignmentOptions.TopLeft;
        Button close = MakeButton("Close", box.transform, font, "닫기", new Vector2(0, -91), new Vector2(108, 18));
        data.FindProperty("structuralFrameLocalization").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        data.FindProperty("structuralFrameInspectButton").objectReferenceValue = inspect;
        data.FindProperty("structuralFrameInspectionRoot").objectReferenceValue = overlay;
        data.FindProperty("structuralFrameInspectionText").objectReferenceValue = text;
        data.FindProperty("structuralFrameCloseButton").objectReferenceValue = close;
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.SetActive(false);
        if (!panel.HasStructuralFramePresentation) throw new InvalidOperationException("Authored frame inspection references are incomplete.");
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
