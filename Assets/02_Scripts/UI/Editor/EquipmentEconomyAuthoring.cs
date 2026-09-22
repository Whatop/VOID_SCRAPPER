using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit narrow authoring; no installer menu and no runtime UI construction.
public static class EquipmentEconomyAuthoring
{
    [Serializable] public sealed class Recipe { public string id; public int scrap, core; }
    [Serializable] public sealed class Recipes { public List<Recipe> recipes; }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before economy authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before economy authoring.");
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-equipmentEconomyRecipes");
        if (index >= 0)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("Missing recipe JSON path.");
            ApplyRecipes(JsonUtility.FromJson<Recipes>(File.ReadAllText(args[index + 1])));
        }
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath, LocalizationContentImporter.DefaultCatalogAssetPath, true))
            throw new InvalidOperationException("Equipment economy localization import failed.");
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/01_Scenes/Settlement.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Settlement.unity", OpenSceneMode.Additive);
        try
        {
            ShipTraitTreePanel panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShipTraitTreePanel>(true)).Single();
            AuthorCosts(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("EQUIPMENT_ECONOMY_AUTHORED: recipes and two bound currency rows; existing Settlement navigation preserved.");
    }

    public static void ApplyRecipes(Recipes input)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        var ids = new HashSet<string>();
        if (input?.recipes == null || input.recipes.Count != 48) throw new InvalidOperationException("Supply exactly 48 development recipes.");
        foreach (Recipe recipe in input.recipes)
        {
            TraitDefinition trait = catalog.FindById(recipe.id);
            if (trait == null || !trait.IsDevelopmentRoster || !ids.Add(recipe.id) || recipe.scrap <= 0 || recipe.core < 0)
                throw new InvalidOperationException("Invalid/duplicate/non-roster manufacturing recipe: " + recipe.id);
        }
        // Validate the entire input before editing any asset. No parallel production price dictionary.
        foreach (Recipe recipe in input.recipes)
        {
            var data = new SerializedObject(catalog.FindById(recipe.id));
            data.FindProperty("manufacturingScrapCost").intValue = recipe.scrap;
            data.FindProperty("manufacturingCoreCost").intValue = recipe.core;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static void AuthorCosts(ShipTraitTreePanel panel)
    {
        var data = new SerializedObject(panel);
        var requirements = data.FindProperty("equipmentRequirements").objectReferenceValue as TMP_Text;
        if (requirements == null) throw new InvalidOperationException("Settlement EquipmentDevelopment: missing authored equipmentRequirements.");
        Transform content = requirements.transform.parent;
        if (content.GetComponent<VerticalLayoutGroup>() == null) throw new InvalidOperationException("Equipment inspection needs its existing Growth/Content layout.");
        TMP_Text heading = Label("ManufacturingCost", content, requirements.font);
        heading.text = "제작 비용";
        heading.transform.SetSiblingIndex(0);
        data.FindProperty("equipmentCostHeading").objectReferenceValue = heading;
        BindRow(data, "equipmentScrapCost", "ScrapCost", content, requirements.font,
            data.FindProperty("scrapCostIconSprite").objectReferenceValue as Sprite, 1);
        BindRow(data, "equipmentCoreCost", "CoreCost", content, requirements.font,
            data.FindProperty("coreShardCostIconSprite").objectReferenceValue as Sprite, 2);
        requirements.transform.SetSiblingIndex(3);
        data.ApplyModifiedPropertiesWithoutUndo();
        var errors = new List<string>();
        if (!panel.ValidateEquipmentPresentation(errors)) throw new InvalidOperationException(string.Join("\n", errors));
        // Scene defaults inspect the frame. Runtime refresh reveals prices only for unmanufactured modules.
        heading.gameObject.SetActive(false);
        content.Find("ScrapCost").gameObject.SetActive(false);
        content.Find("CoreCost").gameObject.SetActive(false);
    }

    private static void BindRow(SerializedObject data, string field, string name, Transform content, TMP_FontAsset font, Sprite sprite, int index)
    {
        if (sprite == null) throw new InvalidOperationException("EquipmentDevelopment: existing " + field + " currency sprite is missing.");
        GameObject root = Rect(name, content);
        root.transform.SetSiblingIndex(index);
        var layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 12;
        GameObject iconRoot = Rect("Icon", root.transform);
        var icon = iconRoot.GetComponent<Image>() ?? iconRoot.AddComponent<Image>();
        icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        var rect = (RectTransform)icon.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(10, 10);
        TMP_Text amount = Label("Amount", root.transform, font);
        rect = amount.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(14, 0); rect.offsetMax = Vector2.zero;
        amount.text = "";
        var row = data.FindProperty(field);
        row.FindPropertyRelative("root").objectReferenceValue = root;
        row.FindPropertyRelative("icon").objectReferenceValue = icon;
        row.FindPropertyRelative("amount").objectReferenceValue = amount;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        if (go.scene != parent.gameObject.scene) SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font)
    {
        GameObject go = Rect(name, parent);
        var text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = 8; text.color = Color.white; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
