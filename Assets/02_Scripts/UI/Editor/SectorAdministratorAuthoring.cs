using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SectorAdministratorAuthoring
{
    public const string Art = "Assets/Art/SectorAdministrator/";
    public const string Boss = "Assets/03_Prefabs/Enemy/Boss.prefab";
    public const string Lane = "Assets/03_Prefabs/VFX/SectorPartitionLane.prefab";
    [Serializable] class Cell { public int x, y, w, h; }
    [Serializable] class Frame { public Cell frame; }
    [Serializable] class Sheet { public Frame[] frames; }

    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var warning = ImportSheet("VFX_Sector_LaserTelegraph", false);
        var beam = ImportSheet("VFX_Sector_LaserBeam", false);
        var containment = ImportSheet("VFX_Sector_ContainmentBarrier", true);
        var idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/SectorAdministrator.png");
        var charged = ImportBody("sector_administrator_charged", idle.pixelsPerUnit);
        var exposed = ImportBody("sector_administrator_exposed_core", idle.pixelsPerUnit);
        bool exists = File.Exists(Lane);
        var g = exists ? PrefabUtility.LoadPrefabContents(Lane) : new GameObject("SectorPartitionLane");
        try
        {
            var lane = g.GetComponent<SectorPartitionLane>(); if (lane == null) lane = g.AddComponent<SectorPartitionLane>();
            var rb = g.GetComponent<Rigidbody2D>(); if (rb == null) rb = g.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
            var collider = g.GetComponent<BoxCollider2D>(); if (collider == null) collider = g.AddComponent<BoxCollider2D>();
            collider.isTrigger = true; collider.enabled = false;
            var child = g.transform.Find("Visual");
            if (child == null) { child = new GameObject("Visual").transform; child.SetParent(g.transform, false); }
            var renderer = child.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = warning[0]; renderer.enabled = false; renderer.sortingOrder = -2; // Above space background, below player/body sprites.
            Set(lane, "visual", renderer); Set(lane, "damageCollider", collider);
            SetArray(lane, "warningFrames", warning); SetArray(lane, "activeFrames", beam.Take(7).ToArray());
            PrefabUtility.SaveAsPrefabAsset(g, Lane);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(g); else Object.DestroyImmediate(g); }
        var root = PrefabUtility.LoadPrefabContents(Boss);
        try
        {
            var c = root.GetComponent<BossPatternController>();
            var body = root.transform.Find("BossVisualRoot").GetComponent<SpriteRenderer>();
            Set(c, "useSectorControlSequence", true);
            Set(c, "sectorLanePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Lane).GetComponent<SectorPartitionLane>());
            Set(c, "sectorBody", body); Set(c, "sectorIdleSprite", idle);
            Set(c, "sectorChargedSprite", charged); Set(c, "sectorExposedSprite", exposed);
            Set(c, "sectorContainmentSprite", containment[0]);
            Set(c, "useRegularPhase2HexVisual", false);
            Set(c, "stopMovementWhileCasting", true);
            // Approved up-facing body rotates visually; collision root keeps its authored orientation.
            Set(c, "visualRoot", body.transform);
            Set(c, "phase2ShieldColor", new Color(.38f, .9f, .57f, .9f));
            Set(c, "phase2ShieldLowColor", new Color(.75f, 1f, .8f, .9f));
            PrefabUtility.SaveAsPrefabAsset(root, Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/SectorAdministrator/authored.txt", "Region A boss only; pooled lane prefab; five byte-identical approved art copies. No scene, definition, campaign, or shared asset writes.");
    }

    static string Copy(string source, string name)
    {
        string path = Art + name + ".png";
        byte[] data = File.ReadAllBytes("ArtTools/Aseprite/Output/" + source + ".png");
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(data)) throw new Exception("Production art differs: " + path);
        if (!File.Exists(path)) File.WriteAllBytes(path, data);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }
    static void Settings(TextureImporter importer, float ppu)
    {
        importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
    }
    static Sprite ImportBody(string name, float ppu)
    {
        string path = Copy("06_SectorAdministrator/" + name, name);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); Settings(importer, ppu);
        importer.spriteImportMode = SpriteImportMode.Single; importer.spritePivot = new Vector2(.5f, .5f);
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite[] ImportSheet(string name, bool centered)
    {
        string path = Copy("16_SystemBossVFX/" + name, name);
        var sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText("ArtTools/Aseprite/Output/16_SystemBossVFX/" + name + ".json"));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); Settings(importer, 32);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out _, out int height);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var rects = sheet.frames.Select((f, i) => new SpriteRect { name = name + "_" + i.ToString("00"),
            rect = new Rect(f.frame.x, height - f.frame.y - f.frame.h, f.frame.w, f.frame.h),
            pivot = new Vector2(centered ? .5f : 0, centered ? .5f : 15f / 32f), alignment = SpriteAlignment.Custom,
            spriteID = old.FirstOrDefault(r => r.name == name + "_" + i.ToString("00"))?.spriteID ?? GUID.Generate() }).ToArray();
        provider.SetSpriteRects(rects); provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }
    static void SetArray(Object o, string field, Sprite[] values)
    {
        var s = new SerializedObject(o); var p = s.FindProperty(field); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Set(Object o, string field, object value)
    {
        var s = new SerializedObject(o); var p = s.FindProperty(field);
        if (value is bool b) p.boolValue = b;
        else if (value is Color color) p.colorValue = color;
        else p.objectReferenceValue = (Object)value;
        s.ApplyModifiedPropertiesWithoutUndo();
    }
}
