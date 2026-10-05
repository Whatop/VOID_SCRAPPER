using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public static class NullArtAuthoring
{
    public const string BossPath = "Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab";
    public const string Art = "Assets/Art/NullDispatcher/";
    public const string Vfx = "Assets/03_Prefabs/VFX/NullDispatcher/";
    private const string Source = "ArtTools/Aseprite/Output/";
    [Serializable] private class Sheet { public Frame[] frames; }
    [Serializable] private class Frame { public int duration; public Cell frame; }
    [Serializable] private class Cell { public int x,y,w,h; }
    private static readonly System.Collections.Generic.List<string> evidence = new System.Collections.Generic.List<string>();

    public static void Run()
    {
        ApprovedVisualIntegration.Guard(); evidence.Clear();
        Directory.CreateDirectory("Logs/NullArt"); Directory.CreateDirectory(Art); Directory.CreateDirectory(Vfx);
        var body = new[] {"dormant_sealed","active","phase2_unbound","critical_exposed"}.Select(n =>
            ImportSingle("21_NullDispatcher_Phase2Revision/NULL_Dispatcher_"+n+".png", "NULL_Dispatcher_"+n)).ToArray();
        Sprite purple = ImportCoreFrame("02_Core/purple_corrupted/core_purple_corrupted_active", "PurpleCoreActive");
        Sprite overloaded = ImportCoreFrame("02_Core/purple_corrupted/core_purple_corrupted_overloaded", "PurpleCoreOverloaded");
        var warning = MakeVfx("LaserRain_Telegraph", "RainWarning", false, true);
        var fire = MakeVfx("LaserRain_Beam", "RainFire", true, false, 0, 4);
        var release = MakeVfx("LaserRain_Beam", "RainRelease", false, false, 4, 2);
        var compress = MakeVfx("CompressionDispatch", "Compression", false, false);
        var redirect = MakeVfx("PhaseRedirectCorruption", "Redirect", false, false);
        var link = MakeVfx("InterventionBeam", "Intervention", true, false, tile: true);
        var burst = MakeVfx("CoreGlitchBurst", "CoreBurst", false, false);
        var critical = MakeVfx("CriticalLoop", "CriticalLoop", true, false);
        Sprite[] pieces = ImportFragments();
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var controller = root.GetComponent<NullDispatcherBossController>();
            var presentation = root.GetComponent<NullDispatcherPresentation>();
            if(presentation==null)presentation=root.AddComponent<NullDispatcherPresentation>();
            var outer = root.transform.Find("OuterShellRoot/TemporaryCoreVisual").GetComponent<SpriteRenderer>();
            var inner = root.transform.Find("InnerCoreRoot").GetComponent<SpriteRenderer>();
            outer.sprite = body[0]; inner.sprite = body[3];
            // 160 px / 32 PPU * .8 = the previous 4-unit maximum body footprint.
            outer.transform.localScale = inner.transform.localScale = Vector3.one * .8f;
            outer.color = inner.color = Color.white;
            outer.sortingLayerName = inner.sortingLayerName = "Default";
            outer.sortingOrder = inner.sortingOrder = 35;
            var fragments = new SpriteRenderer[4];
            for(int i=0;i<4;i++)
            {
                fragments[i] = root.transform.Find("OuterShellRoot/ShellRemnant"+(i+1)).GetComponent<SpriteRenderer>();
                fragments[i].sprite=pieces[i]; fragments[i].color=Color.white;
                fragments[i].transform.localScale=Vector3.one*.65f;
                fragments[i].enabled=false; // State poses already include the shell; loose parts appear only at the existing break.
            }
            foreach(string n in new[]{"PhaseRelay1","PhaseRelay2"}) root.transform.Find(n).GetComponent<SpriteRenderer>().sprite=purple;
            root.transform.Find("ReclaimBreakPulse").GetComponent<SpriteRenderer>().sprite=overloaded;
            Set(controller,"presentation",presentation);
            Set(presentation,"shellBody",outer);Set(presentation,"exposedBody",inner);SetArray(presentation,"shellFragments",fragments);
            Set(presentation,"sealedSprite",body[0]);Set(presentation,"activeSprite",body[1]);Set(presentation,"unboundSprite",body[2]);Set(presentation,"criticalSprite",body[3]);
            Set(presentation,"endingCoreSprite",purple);
            Set(presentation,"rainWarning",warning);Set(presentation,"rainFire",fire);Set(presentation,"rainRelease",release);
            Set(presentation,"compression",compress);Set(presentation,"redirect",redirect);Set(presentation,"intervention",link);
            Set(presentation,"coreBurst",burst);Set(presentation,"criticalLoop",critical);
            var support=root.GetComponent<SettlementFinalSupportController>();
            var ev=(UnityEvent)typeof(SettlementFinalSupportController).GetField("weaponLabSupportTriggered",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(support);
            if(!Enumerable.Range(0,ev.GetPersistentEventCount()).Any(i=>ev.GetPersistentTarget(i)==presentation && ev.GetPersistentMethodName(i)=="ShowSupportIntervention"))
                UnityEventTools.AddPersistentListener(ev,presentation.ShowSupportIntervention);
            EditorUtility.SetDirty(support);
            PrefabUtility.SaveAsPrefabAsset(root,BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Logs/NullArt/imports.tsv",evidence);
        Validate();
    }

    private static TextureImporter Copy(string source, string name)
    {
        string path=Art+name+".png"; byte[] bytes=File.ReadAllBytes(Source+source);
        if(File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(bytes)) throw new Exception("Different production copy: "+path);
        if(!File.Exists(path)) File.WriteAllBytes(path,bytes);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=32;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=true; importer.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spritePivot=new Vector2(.5f,.5f);
        importer.SetTextureSettings(settings);
        evidence.Add(path+"\t"+Source+source);
        return importer;
    }
    private static Sprite ImportSingle(string source,string name)
    {
        var importer=Copy(source,name);importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(importer.assetPath);
    }
    private static Sprite ImportCoreFrame(string source, string name)
    {
        var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Source + source + ".json"));
        var importer = Copy(source + ".png", name);
        importer.GetSourceTextureWidthAndHeight(out _, out int height);
        var cells = data.frames.Select(f => new Rect(f.frame.x, height - f.frame.y - f.frame.h, f.frame.w, f.frame.h)).ToArray();
        // Existing relay/ending owners pulse a static sprite. Bind one authored
        // cell, never the entire horizontal animation sheet.
        return Slice(importer, cells, new Vector2(.5f, .5f), name)[0];
    }
    public static void RepairCoreFrames()
    {
        ApprovedVisualIntegration.Guard();
        Sprite purple = ImportCoreFrame("02_Core/purple_corrupted/core_purple_corrupted_active", "PurpleCoreActive");
        Sprite overloaded = ImportCoreFrame("02_Core/purple_corrupted/core_purple_corrupted_overloaded", "PurpleCoreOverloaded");
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            foreach (string name in new[] { "PhaseRelay1", "PhaseRelay2" }) root.transform.Find(name).GetComponent<SpriteRenderer>().sprite = purple;
            root.transform.Find("ReclaimBreakPulse").GetComponent<SpriteRenderer>().sprite = overloaded;
            Set(root.GetComponent<NullDispatcherPresentation>(), "endingCoreSprite", purple);
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static Sprite[] Slice(TextureImporter importer, Rect[] cells, Vector2 pivot, string prefix)
    {
        importer.spriteImportMode=SpriteImportMode.Multiple;
        var factory=new SpriteDataProviderFactories();factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();
        var rects=cells.Select((r,i)=>new SpriteRect { name=prefix+"_"+i.ToString("00"),rect=r,pivot=pivot,alignment=SpriteAlignment.Custom,
            spriteID=old.FirstOrDefault(s=>s.name==prefix+"_"+i.ToString("00"))?.spriteID??GUID.Generate() }).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
    }
    private static Sprite[] ImportFragments()
    {
        var importer=Copy("21_NullDispatcher_Phase2Revision/NULL_Dispatcher_phase2_unbound.png","ShellFragments");
        return Slice(importer,new[]{new Rect(20,82,36,48),new Rect(104,82,36,48),new Rect(12,20,40,44),new Rect(108,20,40,44)},new Vector2(.5f,.5f),"NULL_ShellFragment");
    }
    private static NullSignatureVfx MakeVfx(string source,string role,bool loop,bool hold,int start=0,int count=0,bool tile=false)
    {
        string name="VFX_NULL_"+source;
        var data=JsonUtility.FromJson<Sheet>(File.ReadAllText(Source+"22_NullSignatureVFX/"+name+".json"));
        var importer=Copy("22_NullSignatureVFX/"+name+(tile?"_Tile32":"")+".png",name+(tile?"_Tile32":""));
        importer.GetSourceTextureWidthAndHeight(out _,out int height);
        Rect[] rects=data.frames.Select((f,i)=>tile?new Rect(i*32,0,32,32):new Rect(f.frame.x,height-f.frame.y-f.frame.h,f.frame.w,f.frame.h)).ToArray();
        var sprites=Slice(importer,rects,tile?new Vector2(0,.5f):new Vector2(.5f,.5f),name);
        if(count==0)count=sprites.Length;
        string path=Vfx+role+".prefab";
        bool existing=File.Exists(path);
        var root=existing?PrefabUtility.LoadPrefabContents(path):new GameObject(role);
        try
        {
            var renderer=root.GetComponent<SpriteRenderer>();if(renderer==null)renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=sprites[start];
            renderer.sortingLayerName="Default";renderer.sortingOrder=role.StartsWith("Rain")?70:45;
            var effect=root.GetComponent<NullSignatureVfx>();if(effect==null)effect=root.AddComponent<NullSignatureVfx>();
            Set(effect,"visual",renderer);SetArray(effect,"frames",sprites.Skip(start).Take(count).ToArray());
            var so=new SerializedObject(effect);var durations=so.FindProperty("frameSeconds");durations.arraySize=count;
            for(int i=0;i<count;i++)durations.GetArrayElementAtIndex(i).floatValue=data.frames[start+i].duration*.001f;
            so.FindProperty("loop").boolValue=loop;so.FindProperty("holdLast").boolValue=hold;so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { if(existing)PrefabUtility.UnloadPrefabContents(root);else Object.DestroyImmediate(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NullSignatureVfx>();
    }
    public static void Set(Object owner,string field,Object value)
    {
        var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void SetArray(Object owner,string field,Object[] values)
    {
        var so=new SerializedObject(owner);var a=so.FindProperty(field);a.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void Validate()
    {
        foreach(string path in new[]{BossPath}.Concat(Directory.GetFiles(Vfx,"*.prefab")))
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+path);
            foreach(var r in root.GetComponentsInChildren<SpriteRenderer>(true))if(r.sprite==null)throw new Exception("Missing sprite: "+path+"/"+r.name);
        }
        File.WriteAllText("Logs/NullArt/validation.txt","PASS: NULL body/state/VFX production bindings resolve; no Missing Sprite or Missing Script.\n");
        Debug.Log("NULL_ART_VALIDATION PASS");
    }
}
