using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

// Narrow opt-in authoring: edit the two existing Region A VFX prefabs in place.
public static class SectorFinalAuthoring
{
    public const string Art = "Assets/Art/SectorAdministrator/PurpleLaser/";
    public const string Source = "ArtTools/Aseprite/Output/24_SectorPurpleLaserVFX/";
    static void Array(Object owner, string field, Object[] value)
    {
        var so = new SerializedObject(owner); var a = so.FindProperty(field); a.arraySize = value.Length;
        for (int i = 0; i < value.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = value[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Transform Child(Transform parent, string name)
    { var t = parent.Find(name); if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); } return t; }
    static SpriteRenderer Sprite(Transform parent, string name, int order)
    {
        var t = Child(parent, name); var r = t.GetComponent<SpriteRenderer>();
        if (r == null) r = t.gameObject.AddComponent<SpriteRenderer>();
        r.sortingOrder = order; r.enabled = false; return r;
    }
    static Sprite[] Import(string family, int width, Vector2 pivot)
    {
        string path = Art + family + ".png", source = Source + family + ".png";
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source)))
            throw new InvalidOperationException("Refuse to overwrite changed purple art: " + path);
        if (!File.Exists(path)) File.Copy(source, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 32; ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(settings);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(ti); provider.InitSpriteEditorDataProvider();
        var prior = provider.GetSpriteRects(); var rects = new SpriteRect[4];
        for (int i = 0; i < 4; i++)
        {
            string name = family + "_" + i.ToString("D2"); var old = prior.FirstOrDefault(x => x.name == name);
            rects[i] = new SpriteRect { name = name, rect = new Rect(i * width, 0, width, 32),
                alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = old != null ? old.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        provider.Apply(); ti.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
    }
    public static void Author()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty && (stage.assetPath == SectorAdministratorAuthoring.Lane || stage.assetPath == SectorCoreAuthoring.Rectangle))
            throw new InvalidOperationException("Conflicting unsaved Region A VFX prefab changes.");
        Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var warning = Import("VFX_Sector_PurpleLaserWarning", 64, new Vector2(0, .46875f));
        var active = Import("VFX_Sector_PurpleLaserBeam", 64, new Vector2(0, .46875f));
        var emitter = Import("VFX_Sector_PurpleLaserEmitter", 32, new Vector2(.25f, .46875f));
        var root = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Lane);
        try
        {
            var lane = root.GetComponent<SectorPartitionLane>();
            Array(lane, "purpleWarningFrames", warning); Array(lane, "purpleActiveFrames", active); Array(lane, "purpleEmitterFrames", emitter);
            var so = new SerializedObject(lane);
            var beam = (SpriteRenderer)so.FindProperty("visual").objectReferenceValue;
            var flash = Sprite(root.transform, "PurpleEmitter", 2); // Visible over the neutral chassis at the source; player remains above it.
            flash.sprite = emitter[0]; flash.sharedMaterial = beam.sharedMaterial;
            so.FindProperty("purpleEmitter").objectReferenceValue = flash; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SectorAdministratorAuthoring.Lane);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(SectorCoreAuthoring.Rectangle);
        try
        {
            var area = root.GetComponent<SectorRectangularAoE>(); var so = new SerializedObject(area);
            foreach (string field in new[] { "borders", "fills" })
            {
                var a = so.FindProperty(field); a.arraySize = SectorRectangularAoE.MaxBands * (field == "borders" ? 2 : 4);
                for (int i = 0; i < a.arraySize; i++)
                {
                    var t = Child(root.transform, field + i); var line = t.GetComponent<LineRenderer>();
                    if (line == null) line = t.gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                    line.useWorldSpace = false; line.loop = field == "borders"; line.positionCount = line.loop ? 4 : 2;
                    line.numCapVertices = line.numCornerVertices = 0; line.sortingOrder = line.loop ? -1 : -3; line.enabled = false;
                    a.GetArrayElementAtIndex(i).objectReferenceValue = line;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var edges = new SpriteRenderer[SectorRectangularAoE.MaxBands * 4]; var corners = new SpriteRenderer[edges.Length];
            var edgeSprite = root.transform.Find("ReleaseEdge0").GetComponent<SpriteRenderer>().sprite;
            var cornerSprite = root.transform.Find("ReleaseCorner0").GetComponent<SpriteRenderer>().sprite;
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = Sprite(root.transform, "ReleaseEdge" + i, 0); edges[i].sprite = edgeSprite; edges[i].drawMode = SpriteDrawMode.Tiled;
                corners[i] = Sprite(root.transform, "ReleaseCorner" + i, 0); corners[i].sprite = cornerSprite;
            }
            Array(area, "releaseEdges", edges); Array(area, "releaseCorners", corners);
            PrefabUtility.SaveAsPrefabAsset(root, SectorCoreAuthoring.Rectangle);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory("Logs/SectorFinal");
        File.WriteAllText("Logs/SectorFinal/authored.txt", "Production Boss keeps its existing lane/AoE references. Bound three byte-identical approved purple sheets; expanded pooled arena ring renderers in place. No Boss, scene, missile or player prefab writes.");
    }
}
