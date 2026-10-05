using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SectorDeploymentAuthoring
{
    public const string Art = "Assets/Art/SectorAdministrator/Support/";
    public const string Barrier = "Assets/03_Prefabs/VFX/SectorBarrierVisual.prefab";
    static void Set(Object o, string field, Object value)
    { var so = new SerializedObject(o); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Array(Object o, string field, Object[] value)
    { var so = new SerializedObject(o); var a = so.FindProperty(field); a.arraySize = value.Length; for (int i = 0; i < value.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = value[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Transform Child(Transform parent, string name)
    { var t = parent.Find(name); if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); } return t; }
    static SpriteRenderer Sprite(Transform parent, string name, int sorting)
    { var t = Child(parent, name); var r = t.GetComponent<SpriteRenderer>(); if (r == null) r = t.gameObject.AddComponent<SpriteRenderer>(); r.sortingOrder = sorting; r.enabled = false; return r; }
    static Sprite[] Import(string family, int width, int height, Vector2 pivot, bool core = false)
    {
        string source = "ArtTools/Aseprite/Output/" + (core ? "25_SectorSupportVFX/" : "26_SectorSupportVFX_Remaining/") + family + ".png";
        string path = Art + family + ".png";
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source))) throw new Exception("Refuse to overwrite modified support art: " + path);
        if (!File.Exists(path)) File.Copy(source, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = 32;
        ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); ti.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(settings);
        ti.GetSourceTextureWidthAndHeight(out int w, out int h);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(ti); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects(); var rects = new SpriteRect[w / width * (h / height)];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = family + "_" + i.ToString("D2"); var prior = old.FirstOrDefault(x => x.name == name);
            rects[i] = new SpriteRect { name = name, rect = new Rect(i % (w / width) * width, h - height - i / (w / width) * height, width, height),
                alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = prior != null ? prior.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        provider.Apply(); ti.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
    }
    static SectorSupportVfx Effect(Transform parent, string name, Sprite[] frames, float[] durations, int order, Sprite[] purple = null)
    {
        var visual = Sprite(parent, name, order); visual.sprite = frames[0];
        var effect = visual.GetComponent<SectorSupportVfx>(); if (effect == null) effect = visual.gameObject.AddComponent<SectorSupportVfx>();
        Set(effect, "visual", visual); Array(effect, "frames", frames); Array(effect, "purpleFrames", purple ?? new Sprite[0]);
        var so = new SerializedObject(effect); var a = so.FindProperty("durations"); a.arraySize = durations.Length;
        for (int i = 0; i < durations.Length; i++) a.GetArrayElementAtIndex(i).floatValue = durations[i]; so.ApplyModifiedPropertiesWithoutUndo();
        return effect;
    }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var core = Import("VFX_Sector_CoreMissileFlash", 32, 32, Vector2.one * .5f, true);
        var propulsion = Import("VFX_Sector_EnemyMissileTrail", 32, 16, new Vector2(.875f, .5f));
        var releases = Import("VFX_Sector_RectangleRelease", 32, 16, new Vector2(0, .5f));
        var corners = Import("VFX_Sector_RectangleCorner", 16, 16, new Vector2(0, 1));
        var fronts = Import("VFX_Sector_BarrierFormationFront", 32, 16, new Vector2(.75f, .5f));
        var sparks = Import("VFX_Sector_BarrierActivationSpark", 16, 16, Vector2.one * .5f);
        var connects = Import("VFX_Sector_BarrierStabilizationPulse", 32, 16, new Vector2(0, .5f));

        bool exists = File.Exists(Barrier);
        var root = exists ? PrefabUtility.LoadPrefabContents(Barrier) : new GameObject("SectorBarrierVisual");
        try
        {
            var visual = root.GetComponent<SectorBarrierVisual>(); if (visual == null) visual = root.AddComponent<SectorBarrierVisual>();
            var beam = Sprite(root.transform, "StaticBarrier", 28);
            var bossAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss);
            var so = new SerializedObject(bossAsset.GetComponent<BossPatternController>());
            beam.sprite = (Sprite)so.FindProperty("sectorContainmentSprite").objectReferenceValue;
            beam.drawMode = SpriteDrawMode.Tiled; beam.transform.localScale = new Vector3(1, .6f, 1); beam.color = new Color(.68f, .82f, .72f, .8f);
            Set(visual, "beam", beam);
            Set(visual, "activation", Effect(root.transform, "Activation", sparks, new[] { .05f, .05f, .06f, .04f }, 29));
            Set(visual, "front", Effect(root.transform, "Front", fronts, new[] { .08f, .08f, .08f, .08f }, 29));
            var connect = Effect(root.transform, "Connect", connects, new[] { .04f, .06f, .08f, .04f }, 29);
            connect.Visual.drawMode = SpriteDrawMode.Tiled; Set(visual, "connection", connect);
            PrefabUtility.SaveAsPrefabAsset(root, Barrier);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        root = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var boss = root.GetComponent<BossPatternController>(); var body = root.transform.Find("BossVisualRoot");
            var corePoint = Child(body, "CoreEjectionPoint"); corePoint.localPosition = Vector3.zero;
            Set(boss, "sectorMissileCore", corePoint);
            var flash = Effect(corePoint, "CoreGeneration", core.Take(6).ToArray(), new[] { .06f, .04f, .05f, .06f, .06f, .03f }, 15, core.Skip(6).ToArray());
            flash.transform.localScale = new Vector3(1 / body.lossyScale.x, 1 / body.lossyScale.y, 1);
            Set(boss, "sectorCoreGeneration", flash);
            Set(boss, "sectorBarrierVisualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Barrier).GetComponent<SectorBarrierVisual>());
            PrefabUtility.SaveAsPrefabAsset(root, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(SectorPatternAuthoring.Missile);
        try
        {
            var p = root.GetComponent<SectorMissilePresentation>(); if (p == null) p = root.AddComponent<SectorMissilePresentation>();
            Set(p, "contact", root.GetComponent<CircleCollider2D>());
            var tail = Effect(root.transform, "RedPropulsion", propulsion, new[] { .06f, .06f, .06f, .06f }, 3);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 90); tail.transform.localPosition = new Vector3(0, -.13f);
            Set(p, "propulsion", tail);
            PrefabUtility.SaveAsPrefabAsset(root, SectorPatternAuthoring.Missile);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(SectorCoreAuthoring.Rectangle);
        try
        {
            var area = root.GetComponent<SectorRectangularAoE>();
            foreach (string field in new[] { "borders", "fills" })
            {
                var so = new SerializedObject(area); var a = so.FindProperty(field); a.arraySize = field == "borders" ? 10 : 20;
                for (int i = 0; i < a.arraySize; i++)
                {
                    var t = Child(root.transform, field + i); var line = t.GetComponent<LineRenderer>(); if (line == null) line = t.gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                    line.useWorldSpace = false; line.loop = field == "borders"; line.positionCount = line.loop ? 4 : 2;
                    line.numCapVertices = line.numCornerVertices = 0; line.sortingOrder = line.loop ? -1 : -3; line.enabled = false;
                    a.GetArrayElementAtIndex(i).objectReferenceValue = line;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var edges = new SpriteRenderer[20]; var bends = new SpriteRenderer[20];
            for (int i = 0; i < 20; i++)
            {
                edges[i] = Sprite(root.transform, "ReleaseEdge" + i, 0); edges[i].sprite = releases[1]; edges[i].drawMode = SpriteDrawMode.Tiled;
                bends[i] = Sprite(root.transform, "ReleaseCorner" + i, 0); bends[i].sprite = corners[1];
            }
            Array(area, "releaseEdges", edges); Array(area, "releaseCorners", bends);
            Array(area, "releaseFrames", releases.Skip(1).ToArray()); Array(area, "cornerFrames", corners.Skip(1).ToArray());
            PrefabUtility.SaveAsPrefabAsset(root, SectorCoreAuthoring.Rectangle);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory("Logs/SectorDeployment");
        File.WriteAllText("Logs/SectorDeployment/authored.txt", "Saved Region A support VFX copies, core binding, enemy propulsion, shared rectangular release and pooled containment presentation.");
    }
}
