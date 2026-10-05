using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SectorTransitionAuthoring
{
    public const string Art = "Assets/Art/SectorAdministrator/Relay/";
    public const string Source = "ArtTools/Aseprite/Output/27_SectorLaserRelay/";
    public const string ArrivalSource = "ArtTools/Aseprite/Output/28_SectorIntroShieldVFX/ArrivalMarker_Revision2/";
    public const string Relay = "Assets/03_Prefabs/Enemy/SectorLaserRelay.prefab";
    static void Set(Object o, string field, Object value)
    { var so = new SerializedObject(o); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Array(Object o, string field, Object[] value)
    { var so = new SerializedObject(o); var a = so.FindProperty(field); a.arraySize = value.Length; for (int i = 0; i < value.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = value[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Sprite[] Import(string family, int width, int height, Vector2 pivot, bool arrival = false)
    {
        string source = (arrival ? ArrivalSource : Source) + family + ".png", path = Art + family + ".png";
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source))) throw new Exception("Refuse to overwrite modified art: " + path);
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
    static SectorSupportVfx Effect(Transform parent, string name, Sprite[] frames, float[] durations, int order, Material material, Sprite[] purple = null)
    {
        var child = parent.Find(name); if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        var visual = child.GetComponent<SpriteRenderer>(); if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = frames[0]; visual.enabled = false; visual.sortingOrder = order; visual.sharedMaterial = material;
        var effect = child.GetComponent<SectorSupportVfx>(); if (effect == null) effect = child.gameObject.AddComponent<SectorSupportVfx>();
        Set(effect, "visual", visual); Array(effect, "frames", frames); Array(effect, "purpleFrames", purple ?? new Sprite[0]);
        var so = new SerializedObject(effect); var a = so.FindProperty("durations"); a.arraySize = durations.Length;
        for (int i = 0; i < durations.Length; i++) a.GetArrayElementAtIndex(i).floatValue = durations[i]; so.ApplyModifiedPropertiesWithoutUndo();
        return effect;
    }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var states = Import("Sector_LaserRelay", 48, 48, Vector2.one * .5f);
        var deploy = Import("VFX_Sector_RelayDeploy", 48, 48, Vector2.one * .5f);
        var disconnect = Import("VFX_Sector_RelayDisconnect", 16, 16, new Vector2(.25f, .5f));
        var reconnect = Import("VFX_Sector_RelayReconnect", 32, 16, new Vector2(.25f, .5f));
        var stabilize = Import("VFX_Sector_RelayStabilize", 16, 16, Vector2.one * .5f);
        var marker = Import("VFX_Sector_BossArrivalMarker", 128, 128, Vector2.one * .5f, true);
        var root = PrefabUtility.LoadPrefabContents(File.Exists(Relay) ? Relay : "Assets/03_Prefabs/Enemy/LaserManagerShip.prefab");
        try
        {
            root.name = "SectorLaserRelay";
            var body = root.GetComponentInChildren<SpriteRenderer>(true); body.sprite = states[0]; body.color = Color.white;
            var view = root.GetComponent<SectorRelayPresentation>(); if (view == null) view = root.AddComponent<SectorRelayPresentation>();
            Set(view, "body", body); Set(view, "green", states[0]); Set(view, "purple", states[1]);
            Set(view, "deploy", Effect(root.transform, "Deployment", deploy, new[] { .05f, .04f, .05f, .06f, .07f, .04f }, body.sortingOrder + 1, body.sharedMaterial));
            Set(view, "disconnect", Effect(root.transform, "Disconnect", disconnect, new[] { .04f, .05f, .05f, .06f, .04f }, body.sortingOrder + 1, body.sharedMaterial));
            Set(view, "reconnect", Effect(root.transform, "Reconnect", reconnect, new[] { .05f, .05f, .04f, .06f, .03f }, body.sortingOrder + 1, body.sharedMaterial));
            Set(view, "stabilize", Effect(root.transform, "Stabilize", stabilize.Take(3).ToArray(), new[] { .04f, .05f, .04f }, body.sortingOrder + 1, body.sharedMaterial, stabilize.Skip(3).ToArray()));
            PrefabUtility.SaveAsPrefabAsset(root, Relay);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var boss = root.GetComponent<BossPatternController>(); var relay = AssetDatabase.LoadAssetAtPath<GameObject>(Relay);
            Set(boss, "laserManagerShipPrefab", relay); Set(boss, "phase2LaserManagerShipPrefab", relay);
            var body = root.GetComponentInChildren<SpriteRenderer>(true);
            var arrival = Effect(root.transform, "ApprovedArrivalMarker", marker, new[] { .10f, .10f, .10f, .08f, .06f, .04f }, -2, body.sharedMaterial);
            arrival.transform.localScale = new Vector3(1 / root.transform.localScale.x, 1 / root.transform.localScale.y, 1);
            Set(boss, "sectorArrivalMarker", arrival);
            PrefabUtility.SaveAsPrefabAsset(root, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        root = PrefabUtility.LoadPrefabContents(SectorDeploymentAuthoring.Barrier);
        try
        {
            Array(root.GetComponent<SectorBarrierVisual>(), "purpleBeamFrames", AssetDatabase.LoadAllAssetsAtPath(SectorFinalAuthoring.Art + "VFX_Sector_PurpleLaserBeam.png").OfType<Sprite>().OrderBy(x => x.name).ToArray());
            PrefabUtility.SaveAsPrefabAsset(root, SectorDeploymentAuthoring.Barrier);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory("Logs/SectorTransition");
        File.WriteAllText("Logs/SectorTransition/authored.txt", "Approved relay family and ArrivalMarker_Revision2 copied byte-identically and production-bound. Existing approved Purple Laser family reused. No Shield artwork bound.");
    }
}
