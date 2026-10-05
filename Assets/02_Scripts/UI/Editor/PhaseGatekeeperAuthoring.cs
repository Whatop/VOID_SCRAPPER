using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PhaseGatekeeperAuthoring
{
    public const string Art = "Assets/Art/PhaseGatekeeper/";
    public const string Boss = "Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab";
    public const string Portal = "Assets/03_Prefabs/VFX/PhasePortal.prefab";
    public const string Lock = "Assets/03_Prefabs/VFX/PhasePrecisionLock.prefab";
    public const string Redirect = "Assets/03_Prefabs/VFX/PhaseRedirect.prefab";
    [Serializable] class Cell { public int x,y,w,h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var idle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/PhaseGatekeeper.png");
        var phase=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/PhaseGatekeeperCharge.png");
        string lensPath=Copy("08_PhaseGatekeeper/phase_gatekeeper_lens_exposed");
        var importer=(TextureImporter)AssetImporter.GetAtPath(lensPath);Settings(importer,idle.pixelsPerUnit);
        importer.spriteImportMode=SpriteImportMode.Single;importer.spritePivot=new Vector2(.5f,.5f);importer.SaveAndReimport();
        var lens=AssetDatabase.LoadAssetAtPath<Sprite>(lensPath);
        MakeVfx(Portal,"17_SystemBossVFX_Revisions/VFX_Phase_Portal",.6f);
        MakeVfx(Lock,"16_SystemBossVFX/VFX_Phase_PrecisionLock",.45f);
        MakeVfx(Redirect,"17_SystemBossVFX_Revisions/VFX_Phase_RedirectFlash",.7f);
        EditPrefab(Boss, root=>
        {
            // The approved 128px/32PPU art inherited a legacy 3x visual root
            // (12 world units). Fit the visual to the unchanged 2.35-unit hull.
            root.transform.Find("BossVisualRoot").localScale=new Vector3(.65f,.65f,1);
            var c=root.GetComponent<PhaseGatekeeperBossController>();var s=new SerializedObject(c);
            s.FindProperty("usePhaseRouteSequence").boolValue=true;
            s.FindProperty("phaseLockSprite").objectReferenceValue=phase;
            s.FindProperty("lensExposedSprite").objectReferenceValue=lens;
            s.FindProperty("phasePortalPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Portal).GetComponent<PhaseCombatVfx>();
            s.FindProperty("precisionLockPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Lock).GetComponent<PhaseCombatVfx>();
            s.FindProperty("phaseRedirectPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Redirect).GetComponent<PhaseCombatVfx>();
            s.FindProperty("exposedColor").colorValue=new Color(.42f,.75f,1,1);
            s.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/PhaseGatekeeper/authored.txt","Region C opt-in, approved existing Idle/Phase Lock, four byte-identical approved imports and three collider-free pooled VFX. HP, death/campaign bindings and original physics retained.");
    }
    static void MakeVfx(string prefab,string source,float scale)
    {
        string path=Copy(source);string name=Path.GetFileName(source);
        var sheet=JsonUtility.FromJson<Sheet>(File.ReadAllText("ArtTools/Aseprite/Output/"+source+".json"));
        var t=(TextureImporter)AssetImporter.GetAtPath(path);Settings(t,32);t.spriteImportMode=SpriteImportMode.Multiple;t.GetSourceTextureWidthAndHeight(out _,out int height);
        var f=new SpriteDataProviderFactories();f.Init();var provider=f.GetSpriteEditorDataProviderFromObject(t);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();
        var rects=sheet.frames.Select((frame,i)=>new SpriteRect{name=name+"_"+i.ToString("00"),rect=new Rect(frame.frame.x,height-frame.frame.y-frame.frame.h,frame.frame.w,frame.frame.h),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Custom,spriteID=old.FirstOrDefault(r=>r.name==name+"_"+i.ToString("00"))?.spriteID??GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();t.SaveAndReimport();
        var frames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(v=>v.name).ToArray();
        EditPrefab(prefab,root=>
        {
            root.transform.localScale=Vector3.one*scale;
            var visual=root.GetComponent<SpriteRenderer>();if(visual==null)visual=root.AddComponent<SpriteRenderer>();visual.sprite=frames[0];visual.sortingOrder=0;visual.enabled=false;
            var view=root.GetComponent<PhaseCombatVfx>();if(view==null)view=root.AddComponent<PhaseCombatVfx>();
            var s=new SerializedObject(view);s.FindProperty("visual").objectReferenceValue=visual;
            var a=s.FindProperty("frames");var d=s.FindProperty("frameDurations");a.arraySize=d.arraySize=frames.Length;
            for(int i=0;i<frames.Length;i++){a.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];d.GetArrayElementAtIndex(i).floatValue=sheet.frames[i].duration*.001f;}
            s.ApplyModifiedPropertiesWithoutUndo();
        });
    }
    static void EditPrefab(string path,Action<GameObject> action)
    {
        bool exists=File.Exists(path);var root=exists?PrefabUtility.LoadPrefabContents(path):new GameObject(Path.GetFileNameWithoutExtension(path));
        try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{if(exists)PrefabUtility.UnloadPrefabContents(root);else Object.DestroyImmediate(root);}
    }
    static string Copy(string source)
    {
        string path=Art+Path.GetFileName(source)+".png";byte[] bytes=File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png");
        if(File.Exists(path)&&!File.ReadAllBytes(path).SequenceEqual(bytes))throw new Exception("Production art differs: "+path);
        if(!File.Exists(path))File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return path;
    }
    static void Settings(TextureImporter t,float ppu)
    {
        t.textureType=TextureImporterType.Sprite;t.spritePixelsPerUnit=ppu;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;
        var s=new TextureImporterSettings();t.ReadTextureSettings(s);s.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(s);
    }
}
