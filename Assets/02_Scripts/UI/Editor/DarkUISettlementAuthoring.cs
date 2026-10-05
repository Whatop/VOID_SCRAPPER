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

// Saved Settlement presentation only. No runtime lookup, hierarchy builder or gameplay owner.
public static class DarkUISettlementAuthoring
{
    public const string Root = "Assets/Dark UI/Free/";
    public const string ScenePath = "Assets/01_Scenes/Settlement.unity";
    public const string HudPath = "Canvas/SettlementHUD";
    public const string OptionsPath = "Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel";
    public static readonly string[] Navigation = { "HangarNavigationButton", "SettlementButton", "SectorTechnologyNavigationButton", "AddButton", "DialogueArchiveNavigationButton", "OptionButton" };
    public static readonly string[] Panels = {
        "NavigationBackground", "MainPanel/Player_Select", "Repair_HUD/line", "Repair_HUD/Bg",
        "ShipTraitTreePanel/EquipmentDevelopment", "ShipTraitTreePanel/EquipmentDevelopment/Inspection/Growth",
        "ShipTraitTreePanel/EquipmentDevelopment/Catalog", "ShipTraitTreePanel/EquipmentDevelopment/Effects",
        "SectorTechnologyPanel/ContentBackground", "SectorTechnologyPanel/SelectedTechnology",
        "CampaignProgressPanel", "DialogueArchivePanel", "DialogueArchivePanel/RecordList", "DialogueArchivePanel/Transcript"
    };

    // Source-pixel cuts measured past the antialiased corner, into the straight edge.
    // Pills/circles/icons remain Simple: their aspect ratio is part of the artwork.
    public static Vector4 Border(string name)
    {
        if (name == "A" || name == "B" || name == "C") return Vector4.one * 35;
        if (name.StartsWith("BTN_A")) return Vector4.one * 34;
        if (name == "Button_Small_A" || name == "Button_Medium_A" || name == "Button_Large_A") return Vector4.one * 34;
        foreach (int radius in new[] { 32, 64, 128 })
        {
            if (name == radius.ToString()) return Vector4.one * (radius + 2);
            if (name == radius + " Light") return Vector4.one * (radius + 4);
            if (name == radius + " Light 2") return Vector4.one * (radius + 2);
        }
        return Vector4.zero;
    }

    public static void ConfigureImports()
    {
        foreach (string path in Directory.GetFiles(Root, "*.png").OrderBy(p => p))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100; importer.maxTextureSize = 2048;
            importer.spriteBorder = Border(Path.GetFileNameWithoutExtension(path));
            importer.SaveAndReimport();
        }
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before Settlement visual authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before Settlement visual authoring.");
        ConfigureImports();
        Scene original = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Apply(scene); Validate(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            WriteUsage(scene);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("DARKUI_SETTLEMENT: 65 source images configured; authored presentation saved and validated.");
    }

    public static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split(new[] { '/' }, 2);
        Transform root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == parts[0])?.transform;
        return (parts.Length == 1 ? root : root?.Find(parts[1])) ?? throw new InvalidOperationException("Missing authored Settlement hierarchy: " + path);
    }
    private static Image At(Transform root, string path) => (path.Length == 0 ? root : root.Find(path))?.GetComponent<Image>() ??
        throw new InvalidOperationException("Missing authored Image: " + PathOf(root) + "/" + path);
    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static void Skin(Image image, string source, float multiplier = 8)
    {
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + source + ".png") ??
            throw new InvalidOperationException("Missing DarkUI sprite for " + PathOf(image.transform) + ": " + source);
        image.type = Border(source) == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier; image.fillCenter = true;
        image.preserveAspect = image.type == Image.Type.Simple && source != "Divider";
    }
    private static void Buttons(Transform root)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            Image image = button.targetGraphic as Image;
            if (image == null) throw new InvalidOperationException("Missing Button Image: " + PathOf(button.transform));
            // Directional controls retain their meaning instead of becoming blank rectangles.
            string name = button.name.ToLowerInvariant();
            if (name.Contains("arrow_left")) { Skin(image, "Left Arrow"); continue; }
            if (name.Contains("arrow_right")) { Skin(image, "Right Arrow"); continue; }
            Skin(image, "Button_Small_A");
            // Existing presenters own state tints; a white base avoids multiplying them twice.
            image.color = Color.white;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(.07f, .12f, .17f);
            colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
            colors.pressedColor = SettlementSelectionColors.SelectedBackground;
            colors.disabledColor = new Color(.09f, .11f, .14f);
            button.colors = colors;
        }
    }
    public static void ApplyEquipment(ShipTraitTreePanel panel)
    {
        Transform root = panel.transform.Find("EquipmentDevelopment") ?? throw new InvalidOperationException("Missing EquipmentDevelopment.");
        Buttons(root);
        foreach (string path in new[] { "", "Inspection/Growth", "Catalog", "Effects" }) Skin(At(root, path), "32");
        foreach (var tab in root.GetComponentsInChildren<ShipTraitBranchTabButton>(true))
        {
            // The existing line is a full-rectangle state frame, not a horizontal rule.
            Skin(At(tab.transform, "Line"), "B", 6);
        }
        // Label rectangles already reserve the icon column; avoid charging that padding twice.
        foreach (Transform child in root)
            if (child.name.StartsWith("Slot") && child.Find("Label")?.GetComponent<TMP_Text>() is TMP_Text label)
            {
                label.margin = new Vector4(0, 0, 1, 6);
                label.enableAutoSizing = true;
                label.fontSizeMin = label.fontSizeMax = 7.5f;
                label.characterWidthAdjustment = 20;
            }
    }
    public static void Apply(Scene scene)
    {
        Transform hud = Find(scene, HudPath);
        foreach (string path in Panels) Skin(At(hud, path), "32");
        foreach (string path in new[] { "MainPanel", "Repair_HUD", "SectorTechnologyPanel", "DialogueArchivePanel" }) Buttons(hud.Find(path));
        foreach (string name in Navigation.Concat(new[] { "LaunchButton" })) Buttons(hud.Find(name));
        foreach (string name in Navigation)
        {
            Image background = At(hud, name); background.color = new Color(.055f, .10f, .145f, 1);
            // Navigation owner changes this Image directly; keep Button tint neutral.
            Button button = background.GetComponent<Button>(); var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.pressedColor = Color.white;
            button.colors = colors;
        }
        ApplyEquipment(hud.GetComponentInChildren<ShipTraitTreePanel>(true));
        foreach (string path in new[] { "MainPanel/Player_Select/Line", "MainPanel/PreViewImage/Line", "Repair_HUD/line/line (1)", "Repair_HUD/Bg/Line" })
        {
            Image frame = At(hud, path); Skin(frame, "32 Light", 4);
            frame.color = new Color(.25f, .43f, .52f, 1);
        }
        // This presenter captures Image.color as its normal state (ColorTint is disabled).
        At(hud, "SectorTechnologyPanel/TechnologyCatalog/TechnologyCardTemplate").color = new Color(.07f, .11f, .14f, .96f);
        RectTransform result = (RectTransform)hud.Find("Repair_HUD/Cost/ScrapCostText");
        result.sizeDelta = new Vector2(result.sizeDelta.x, 32);
        // The resource strip occupies the upper right; keep the archive title in the left header lane.
        RectTransform archiveTitle = (RectTransform)hud.Find("DialogueArchivePanel/Title");
        archiveTitle.anchoredPosition = new Vector2(-128, archiveTitle.anchoredPosition.y);
        archiveTitle.sizeDelta = new Vector2(128, archiveTitle.sizeDelta.y);
        foreach (string path in new[] { "Repair_HUD/Bg/Divider", "Repair_HUD/Bg/Divider (1)" }) Skin(At(hud, path), "Divider");
        Transform resources = Find(scene, "Canvas/resourcesUI/ResourceStrip");
        foreach (Transform cell in resources) if (cell.GetComponent<Image>() is Image image) Skin(image, "Button_Small_A", 16);
        Transform options = Find(scene, OptionsPath);
        Buttons(options); Skin(At(options, ""), "32");
        Skin(At(options, "DisplayConfirmation/ConfirmationPanel"), "32");
        foreach (TMP_Dropdown dropdown in options.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            Skin((Image)dropdown.targetGraphic, "Button_Small_A");
            Skin(At(dropdown.transform, "Arrow"), "Down Arrow");
            Skin(dropdown.template.GetComponent<Image>(), "32");
        }
        foreach (Toggle toggle in options.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.targetGraphic is Image background) Skin(background, "32", 16);
            if (toggle.graphic is Image mark) Skin(mark, "32", 16);
        }
        foreach (Slider slider in options.GetComponentsInChildren<Slider>(true))
        {
            Skin(At(slider.transform, "Background"), "32", 32);
            Skin(slider.fillRect.GetComponent<Image>(), "32", 32);
            Skin(slider.handleRect.GetComponent<Image>(), "32", 16);
        }
        foreach (Scrollbar scrollbar in hud.GetComponentsInChildren<Scrollbar>(true).Concat(options.GetComponentsInChildren<Scrollbar>(true)))
        {
            if (scrollbar.GetComponent<Image>() is Image track) Skin(track, "32", 32);
            Skin(scrollbar.handleRect.GetComponent<Image>(), "32", 16);
        }
        // Existing authored DarkUI dividers were marked Sliced without any border.
        foreach (Image image in Find(scene, "Canvas").GetComponentsInChildren<Image>(true))
            if (image.sprite != null && AssetDatabase.GetAssetPath(image.sprite) == Root + "Divider.png")
                Skin(image, "Divider");
    }
    public static void Validate(Scene scene)
    {
        Transform hud = Find(scene, HudPath);
        foreach (string path in Panels.Concat(Navigation)) RequireSprite(At(hud, path));
        foreach (string path in new[] { "MainPanel", "Repair_HUD", "SectorTechnologyPanel", "DialogueArchivePanel", "ShipTraitTreePanel/EquipmentDevelopment", "LaunchButton" })
            foreach (Button button in Find(scene, HudPath + "/" + path).GetComponentsInChildren<Button>(true))
                RequireButtonSprite(button);
        Transform options = Find(scene, OptionsPath);
        RequireSprite(At(options, "")); RequireSprite(At(options, "DisplayConfirmation/ConfirmationPanel"));
        foreach (Button button in options.GetComponentsInChildren<Button>(true)) RequireButtonSprite(button);
        foreach (TMP_Dropdown dropdown in options.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            RequireSprite(At(dropdown.transform, "")); RequireSprite(At(dropdown.transform, "Arrow"));
            RequireSprite(At(dropdown.template, ""));
        }
        foreach (Slider slider in options.GetComponentsInChildren<Slider>(true))
        {
            RequireSprite(At(slider.transform, "Background"));
            RequireSprite(At(slider.fillRect, "")); RequireSprite(At(slider.handleRect, ""));
        }
        foreach (Scrollbar bar in hud.GetComponentsInChildren<Scrollbar>(true).Concat(options.GetComponentsInChildren<Scrollbar>(true)))
            RequireSprite(At(bar.handleRect, ""));
        foreach (Transform cell in Find(scene, "Canvas/resourcesUI/ResourceStrip"))
            if (cell.GetComponent<Image>() is Image image) RequireSprite(image);
        foreach (Image image in Find(scene, "Canvas").GetComponentsInChildren<Image>(true))
            if (image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).StartsWith(Root)) RequireSprite(image);
        var errors = new List<string>();
        var panel = hud.GetComponentInChildren<ShipTraitTreePanel>(true);
        panel.ValidateEquipmentPresentation(errors);
        hud.GetComponent<SettlementHUD>().ValidateHangarPresentation(errors);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
    }
    private static void RequireButtonSprite(Button button)
    {
        if (!(button.targetGraphic is Image image))
            throw new InvalidOperationException("Missing authored Button Image: " + PathOf(button.transform));
        RequireSprite(image);
    }
    private static void RequireSprite(Image image)
    {
        if (image.sprite == null || !AssetDatabase.GetAssetPath(image.sprite).StartsWith(Root))
            throw new InvalidOperationException("Missing authored DarkUI role: " + PathOf(image.transform));
        Vector4 b = image.sprite.border; Rect r = image.sprite.rect;
        if (b.x + b.z >= r.width || b.y + b.w >= r.height || (image.type == Image.Type.Sliced && b == Vector4.zero))
            throw new InvalidOperationException("Invalid DarkUI slicing: " + PathOf(image.transform));
    }
    private static void WriteUsage(Scene scene)
    {
        Directory.CreateDirectory("Logs/DarkUISettlement");
        File.WriteAllLines("Logs/DarkUISettlement/production-usage.tsv", Find(scene, "Canvas").GetComponentsInChildren<Image>(true)
            .Where(i => i.sprite != null && AssetDatabase.GetAssetPath(i.sprite).StartsWith(Root))
            .Select(i => string.Join("\t", PathOf(i.transform), AssetDatabase.GetAssetPath(i.sprite), i.type, i.pixelsPerUnitMultiplier, i.sprite.border))
            .OrderBy(s => s));
    }
}
