using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DarkUISettlementTests
{
    public static IEnumerable<string> Sources => Directory.GetFiles(DarkUISettlementAuthoring.Root, "*.png").OrderBy(p => p);
    [TestCaseSource(nameof(Sources))]
    public void SourceIsLosslessSingleUISpriteWithMeasuredValidBorder(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
        Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null);
        Vector4 border = DarkUISettlementAuthoring.Border(Path.GetFileNameWithoutExtension(path));
        Assert.That(sprite.border, Is.EqualTo(border));
        Assert.That(border.x + border.z, Is.LessThan(sprite.rect.width));
        Assert.That(border.y + border.w, Is.LessThan(sprite.rect.height));
    }

    [Test]
    public void ProductionRolesPreserveBindingsAndReauthorWithoutHierarchyOrStateDrift()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(DarkUISettlementAuthoring.ScenePath);
        try
        {
            DarkUISettlementAuthoring.Validate(scene);
            Transform canvas = DarkUISettlementAuthoring.Find(scene, "Canvas");
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
            int[] ids = components.Select(c => c.GetInstanceID()).ToArray();
            var owners = components.Where(c => c is MonoBehaviour && !(c is UnityEngine.EventSystems.UIBehaviour)).ToArray();
            string[] bindings = owners.Select(EditorJsonUtility.ToJson).ToArray();
            var callbacks = canvas.GetComponentsInChildren<Button>(true).Select(PersistentCallbacks).ToArray();
            DarkUISettlementAuthoring.Apply(scene);
            string[] first = canvas.GetComponentsInChildren<Image>(true).Select(EditorJsonUtility.ToJson).ToArray();
            DarkUISettlementAuthoring.Apply(scene);
            Assert.That(canvas.GetComponentsInChildren<Image>(true).Select(EditorJsonUtility.ToJson), Is.EqualTo(first));
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).Select(c => c.GetInstanceID()), Is.EqualTo(ids));
            Assert.That(owners.Select(EditorJsonUtility.ToJson), Is.EqualTo(bindings), "Gameplay, localization and authored owner references are untouched.");
            Assert.That(canvas.GetComponentsInChildren<Button>(true).Select(PersistentCallbacks), Is.EqualTo(callbacks));
            Transform hud = DarkUISettlementAuthoring.Find(scene, DarkUISettlementAuthoring.HudPath);
            foreach (string path in DarkUISettlementAuthoring.Panels) AssertSprite(hud.Find(path).GetComponent<Image>(), "32");
            Assert.That(hud.Find("SectorTechnologyPanel/TechnologyCatalog/TechnologyCardTemplate").GetComponent<Image>().color,
                Is.EqualTo(new Color(.07f, .11f, .14f, .96f)), "Presenter captures this baseline; white makes all unselected cards white.");
            foreach (string name in DarkUISettlementAuthoring.Navigation)
            {
                Transform button = hud.Find(name);
                AssertSprite(button.GetComponent<Image>(), "Button_Small_A");
                Assert.That(button.Find("NavigationActiveStrip").GetComponent<Image>(), Is.Not.Null);
                Assert.That(button.GetComponent<Outline>(), Is.Not.Null, "Existing focus outline must remain bound.");
            }
            Transform equipment = hud.Find("ShipTraitTreePanel/EquipmentDevelopment");
            foreach (Button button in equipment.GetComponentsInChildren<Button>(true))
            {
                AssertSprite((Image)button.targetGraphic, "Button_Small_A");
                Assert.That(button.colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
                Assert.That(button.colors.selectedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
                Assert.That(button.colors.pressedColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
                Assert.That(button.colors.disabledColor.a, Is.EqualTo(1));
            }
            Transform options = DarkUISettlementAuthoring.Find(scene, DarkUISettlementAuthoring.OptionsPath);
            Assert.That(options.GetComponent<SharedOptionsMenuUI>().TryValidateAuthoredLayout(out string error), Is.True, error);
            foreach (Slider slider in options.GetComponentsInChildren<Slider>(true))
            {
                AssertSprite(slider.fillRect.GetComponent<Image>(), "32");
                AssertSprite(slider.handleRect.GetComponent<Image>(), "32");
            }
            foreach (Scrollbar bar in options.GetComponentsInChildren<Scrollbar>(true)) AssertSprite(bar.handleRect.GetComponent<Image>(), "32");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void MissingProductionRoleReportsExactHierarchyInsteadOfBuildingFallback()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(DarkUISettlementAuthoring.ScenePath);
        try
        {
            foreach (string path in new[] { DarkUISettlementAuthoring.HudPath + "/NavigationBackground",
                DarkUISettlementAuthoring.HudPath + "/ShipTraitTreePanel/EquipmentDevelopment/Inspection/Activation",
                DarkUISettlementAuthoring.OptionsPath + "/SoundTab/MasterSlider/Fill Area/Fill" })
            {
                Image image = DarkUISettlementAuthoring.Find(scene, path).GetComponent<Image>();
                Sprite sprite = image.sprite; image.sprite = null;
                var error = Assert.Throws<InvalidOperationException>(() => DarkUISettlementAuthoring.Validate(scene));
                Assert.That(error.Message, Does.Contain(path));
                image.sprite = sprite;
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void CurrentEquipmentAuthoringRetainsSkinAndSemanticPaletteIsStillAuthoritative()
    {
        string author = File.ReadAllText("Assets/02_Scripts/UI/Editor/StructuralFrameAuthoring.cs");
        Assert.That(author, Does.Contain("DarkUISettlementAuthoring.ApplyEquipment(panel)"));
        Assert.That(SettlementSelectionColors.Selected, Is.EqualTo(new Color(1, .83f, .24f, 1)));
        Assert.That(SettlementSelectionColors.Hover, Is.EqualTo(new Color(.25f, .65f, 1, 1)));
        Assert.That(Directory.GetFiles(DarkUISettlementAuthoring.Root, "*.png").Length, Is.EqualTo(65));
    }
    [TestCase(true, "[OK]")] [TestCase(false, "-")]
    public void RecoveryRequirementUsesSupportedStatusText(bool satisfied, string marker)
    {
        var text = new System.Text.StringBuilder();
        typeof(SettlementController).GetMethod("AppendRequirement", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Invoke(null, new object[] { text, "Requirement", satisfied });
        Assert.That(text.ToString(), Does.Contain(">" + marker + "</color> Requirement"));
        Assert.That(text.ToString(), Does.Contain(satisfied ? "#74E6D2" : "#D6B36A"));
    }
    private static void AssertSprite(Image image, string name)
    {
        Assert.That(image, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(DarkUISettlementAuthoring.Root + name + ".png"), DarkUISettlementAuthoring.PathOf(image.transform));
        Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
    }
    private static string PersistentCallbacks(Button button) => button.name + ":" + string.Join(";", Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(i =>
        button.onClick.GetPersistentTarget(i)?.GetInstanceID() + "/" + button.onClick.GetPersistentMethodName(i) + "/" + button.onClick.GetPersistentListenerState(i)));
}
