using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SectorCinematicAuthoring
{
    public const string Art = "Assets/Art/SectorAdministrator/Shield/";
    public const string Source = "ArtTools/Aseprite/Output/28_SectorIntroShieldVFX/";
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
        var shell = Import("VFX_Sector_BossShield",128,128,Vector2.one*.5f);
        var release = Import("VFX_Sector_BossShieldRelease",128,128,Vector2.one*.5f);
        var hit = Import("VFX_Sector_BossShieldHit",32,32,Vector2.one*.5f);
        var root = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var boss=root.GetComponent<BossPatternController>();var so=new SerializedObject(boss);
            var body=(SpriteRenderer)so.FindProperty("sectorBody").objectReferenceValue;
            var holder=body.transform.Find("ApprovedProtection");
            if(holder==null){holder=new GameObject("ApprovedProtection").transform;holder.SetParent(body.transform,false);}
            // The authored shield and chassis share a 128-pixel canvas. Match
            // that canvas with uniform PPU compensation, never axis stretching.
            holder.localScale=Vector3.one*(body.sprite.bounds.size.x/4f);
            var owner=holder.GetComponent<SectorBossShieldPresentation>();if(owner==null)owner=holder.gameObject.AddComponent<SectorBossShieldPresentation>();
            Set(owner,"shell",Effect(holder,"ProtectedField",shell.Take(4).ToArray(),new[]{.12f,.12f,.12f,.12f},body.sortingOrder-1,body.sharedMaterial,shell.Skip(4).ToArray()));
            Set(owner,"release",Effect(holder,"Release",release.Take(6).ToArray(),new[]{.04f,.05f,.06f,.06f,.06f,.04f},body.sortingOrder-1,body.sharedMaterial,release.Skip(6).ToArray()));
            Set(owner,"hit",Effect(holder,"AbsorbedHit",hit.Take(4).ToArray(),new[]{.03f,.04f,.05f,.03f},body.sortingOrder+1,body.sharedMaterial,hit.Skip(4).ToArray()));
            Set(boss,"sectorShield",owner);
            so.Update();so.FindProperty("sectorPurpleRotationProbability").floatValue=.5f;so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,SectorAdministratorAuthoring.Boss);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/SectorCinematic");
        File.WriteAllText("Logs/SectorCinematic/authored.txt","Saved byte-identical approved Shield, Hit and Release sheets; fixed local renderers, uniform chassis-canvas fit; 50/50 rotation configuration. No source-art edits.");
    }
}
