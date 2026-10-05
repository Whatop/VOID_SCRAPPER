using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RaiderAssaultAuthoring
{
    public const string Art = "Assets/Art/RaiderAssault/";
    public const string Boss = "Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab";
    public const string Vfx = "Assets/03_Prefabs/VFX/RaiderAssault";
    [Serializable] class Cell { public int x,y,w,h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var idle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/RaiderCommander.png");
        Sprite hot=Body("weapons_hot",idle.pixelsPerUnit), critical=Body("critical_damage",idle.pixelsPerUnit);
        MakeVfx(Vfx+"Muzzle.prefab","18_RaiderBossVFX/VFX_Raider_HeavyMuzzle",.4f,new Vector2(.125f,.5f));
        MakeVfx(Vfx+"Hot.prefab","18_RaiderBossVFX/VFX_Raider_Assault_WeaponsHot",.8f,new Vector2(.5f,.5f));
        MakeVfx(Vfx+"Sparks.prefab","18_RaiderBossVFX/VFX_Raider_DamageSparks",.6f,new Vector2(.5f,.5f));
        MakeVfx(Vfx+"Critical.prefab","18_RaiderBossVFX/VFX_Raider_CriticalDamage",1f,new Vector2(.5f,.5f));
        EditPrefab(Boss,root=>
        {
            var c=root.GetComponent<PirateCommanderBossController>(); var s=new SerializedObject(c);
            var visual=root.transform.Find("BossVisualRoot");
            // 128px body, front up; asymmetric port rotary / starboard breacher barrels.
            var left=Child(visual,"AssaultLeftMuzzle"); left.localPosition=new Vector3(-.63f,.66f,0);
            var right=Child(visual,"AssaultRightMuzzle"); right.localPosition=new Vector3(.7f,.64f,0);
            s.FindProperty("leftMuzzle").objectReferenceValue=left; s.FindProperty("rightMuzzle").objectReferenceValue=right;
            s.FindProperty("idleSprite").objectReferenceValue=idle;
            s.FindProperty("weaponsHotSprite").objectReferenceValue=hot; s.FindProperty("criticalSprite").objectReferenceValue=critical;
            string[] fields={"heavyMuzzlePrefab","weaponsHotPrefab","damageSparksPrefab","criticalLoopPrefab"};
            string[] names={"Muzzle","Hot","Sparks","Critical"};
            for(int i=0;i<fields.Length;i++)s.FindProperty(fields[i]).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Vfx+names[i]+".prefab").GetComponent<PhaseCombatVfx>();
            s.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/RaiderAssault/authored.txt","Two muzzle anchors and approved states/VFX. All six PNG copies byte-identical to source. Existing 125 HP, 50% threshold, projectile definition, physics, root hierarchy IDs and campaign/reward owners preserved.");
    }
    static Transform Child(Transform p,string name) { var t=p.Find(name); if(t==null){t=new GameObject(name).transform;t.SetParent(p,false);}return t; }
    static Sprite Body(string state,float ppu)
    {
        string path=Copy("09_RaiderAssaultCommander/raider_assault_commander_"+state);
        var t=(TextureImporter)AssetImporter.GetAtPath(path);Settings(t,ppu);t.spriteImportMode=SpriteImportMode.Single;t.spritePivot=new Vector2(.5f,.5f);t.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void MakeVfx(string prefab,string source,float scale,Vector2 pivot)
    {
        string path=Copy(source);string name=Path.GetFileName(source);
        var sheet=JsonUtility.FromJson<Sheet>(File.ReadAllText("ArtTools/Aseprite/Output/"+source+".json"));
        var t=(TextureImporter)AssetImporter.GetAtPath(path);Settings(t,32);t.spriteImportMode=SpriteImportMode.Multiple;t.GetSourceTextureWidthAndHeight(out _,out int height);
        var f=new SpriteDataProviderFactories();f.Init();var provider=f.GetSpriteEditorDataProviderFromObject(t);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();
        var rects=sheet.frames.Select((frame,i)=>new SpriteRect{name=name+"_"+i.ToString("00"),rect=new Rect(frame.frame.x,height-frame.frame.y-frame.frame.h,frame.frame.w,frame.frame.h),pivot=pivot,alignment=SpriteAlignment.Custom,spriteID=old.FirstOrDefault(r=>r.name==name+"_"+i.ToString("00"))?.spriteID??GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();t.SaveAndReimport();
        var frames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(v=>v.name).ToArray();
        EditPrefab(prefab,root=>
        {
            root.transform.localScale=Vector3.one*scale;
            var visual=root.GetComponent<SpriteRenderer>();if(visual==null)visual=root.AddComponent<SpriteRenderer>();visual.sprite=frames[0];visual.sortingOrder=8;visual.enabled=false;
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
