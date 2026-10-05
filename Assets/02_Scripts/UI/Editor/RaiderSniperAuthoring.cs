using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RaiderSniperAuthoring
{
    public const string Art="Assets/Art/RaiderSniper/";
    public const string Boss="Assets/03_Prefabs/Enemy/PF_Boss_RaiderSniperCommander.prefab";
    public const string Mine="Assets/03_Prefabs/Enemy/RaiderSniperMine.prefab";
    public const string Rail="Assets/03_Prefabs/VFX/RaiderSniperRail.prefab";
    public const string Definition="Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region3_Repeat_Sniper.asset";
    [Serializable] class Cell { public int x,y,w,h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();Directory.CreateDirectory(Art);AssetDatabase.Refresh();
        var idle=Body("idle",64);var locked=Body("target_lock",64);var critical=Body("critical_damage",64);
        MakeVfx(Rail,"18_RaiderBossVFX/VFX_Raider_Sniper_Rail",1,new Vector2(0,.5f));
        string marker="Assets/03_Prefabs/VFX/RaiderSniperMineWarning.prefab";
        MakeVfx(marker,"18_RaiderBossVFX/VFX_Raider_BarrageTelegraph",1,new Vector2(.5f,.5f));
        var railFrames=Frames(Art+"VFX_Raider_Sniper_Rail.png");
        var warningFrames=Frames(Art+"VFX_Raider_BarrageTelegraph.png");
        var sparks=Frames("Assets/Art/RaiderAssault/VFX_Raider_DamageSparks.png");
        EditPrefab(Rail,root=>
        {
            var old=root.GetComponent<PhaseCombatVfx>();if(old!=null)Object.DestroyImmediate(old);
            var rb=root.GetComponent<Rigidbody2D>();if(rb==null)rb=root.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
            var collider=root.GetComponent<BoxCollider2D>();if(collider==null)collider=root.AddComponent<BoxCollider2D>();collider.isTrigger=true;collider.enabled=false;
            var component=root.GetComponent<RaiderRailShot>();if(component==null)component=root.AddComponent<RaiderRailShot>();var s=new SerializedObject(component);
            s.FindProperty("visual").objectReferenceValue=root.GetComponent<SpriteRenderer>();s.FindProperty("damageCollider").objectReferenceValue=collider;Array(s,"frames",railFrames);s.ApplyModifiedPropertiesWithoutUndo();
        });
        EditPrefab(Mine,root=>
        {
            var rb=root.GetComponent<Rigidbody2D>();if(rb==null)rb=root.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
            var collider=root.GetComponent<CircleCollider2D>();if(collider==null)collider=root.AddComponent<CircleCollider2D>();collider.isTrigger=true;collider.radius=.55f;collider.enabled=false;
            var ring=Child(root.transform,"WarningRing");ring.localScale=Vector3.one*(1.1f*32f/58f);
            var renderer=ring.GetComponent<SpriteRenderer>();if(renderer==null)renderer=ring.gameObject.AddComponent<SpriteRenderer>();renderer.sprite=warningFrames[0];renderer.sortingOrder=6;renderer.enabled=false;
            var node=Child(root.transform,"MechanicalMine");node.localScale=Vector3.one*.22f;
            var v=node.GetComponent<SpriteRenderer>();if(v==null)v=node.gameObject.AddComponent<SpriteRenderer>();v.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/RaiderPowerNodeActive.png");v.sortingOrder=7;v.enabled=false;
            var m=root.GetComponent<RaiderSniperMine>();if(m==null)m=root.AddComponent<RaiderSniperMine>();var s=new SerializedObject(m);
            s.FindProperty("machinerySprite").objectReferenceValue=v.sprite;s.FindProperty("ring").objectReferenceValue=renderer;s.FindProperty("machinery").objectReferenceValue=v;s.FindProperty("trigger").objectReferenceValue=collider;
            Array(s,"warningFrames",warningFrames);Array(s,"impactFrames",sparks);s.ApplyModifiedPropertiesWithoutUndo();
        });
        if(!File.Exists(Definition))AssetDatabase.CopyAsset("Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region1_Repeat_Raider.asset",Definition);
        var definition=AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(Definition);var ds=new SerializedObject(definition);
        ds.FindProperty("displayName").stringValue="SNIPER COMMANDER";ds.FindProperty("subtitle").stringValue="RAIDER SNIPER COMMANDER";ds.ApplyModifiedPropertiesWithoutUndo();
        if(!File.Exists(Boss))AssetDatabase.CopyAsset(RaiderAssaultAuthoring.Boss,Boss);
        EditPrefab(Boss,root=>
        {
            var old=root.GetComponent<PirateCommanderBossController>();if(old!=null)Object.DestroyImmediate(old);root.name="PF_Boss_RaiderSniperCommander";
            var c=root.GetComponent<RaiderSniperCommanderBossController>();if(c==null)c=root.AddComponent<RaiderSniperCommanderBossController>();var s=new SerializedObject(c);
            var visual=root.transform.Find("BossVisualRoot");var renderer=visual.GetComponentInChildren<SpriteRenderer>();renderer.sprite=idle;renderer.color=Color.white;
            foreach(string name in new[]{"AssaultLeftMuzzle","AssaultRightMuzzle"}){var t=visual.Find(name);if(t!=null)Object.DestroyImmediate(t.gameObject);}
            var muzzle=Child(visual,"RailMuzzle");muzzle.localPosition=new Vector3(0,.94f,0);
            s.FindProperty("visualRoot").objectReferenceValue=visual;s.FindProperty("railMuzzle").objectReferenceValue=muzzle;s.FindProperty("bodyRenderer").objectReferenceValue=renderer;
            s.FindProperty("idleSprite").objectReferenceValue=idle;s.FindProperty("lockSprite").objectReferenceValue=locked;s.FindProperty("criticalSprite").objectReferenceValue=critical;
            s.FindProperty("railPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Rail).GetComponent<RaiderRailShot>();
            s.FindProperty("minePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Mine).GetComponent<RaiderSniperMine>();
            s.FindProperty("aimDefinition").objectReferenceValue=AssetDatabase.LoadAssetAtPath<ProjectileDefinition>("Assets/02_Scripts/Config/ProjectileDefinition/Projectile_Enemy.asset");
            s.FindProperty("sparksPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Vfx+"Sparks.prefab").GetComponent<PhaseCombatVfx>();
            s.FindProperty("criticalPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Vfx+"Critical.prefab").GetComponent<PhaseCombatVfx>();s.ApplyModifiedPropertiesWithoutUndo();
            var h=new SerializedObject(root.GetComponent<EnemyHealth>());h.FindProperty("componentsToDisableOnDeath").arraySize=1;h.FindProperty("componentsToDisableOnDeath").GetArrayElementAtIndex(0).objectReferenceValue=c;h.ApplyModifiedPropertiesWithoutUndo();
            var d=new SerializedObject(root.GetComponent<BossDummyController>());d.FindProperty("campaignDefinition").objectReferenceValue=definition;d.ApplyModifiedPropertiesWithoutUndo();
        });
        EditPrefab("Assets/03_Prefabs/Object/Core.prefab",root=>
        {
            var s=new SerializedObject(root.GetComponent<CoreObject>());s.FindProperty("region3RepeatBossPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Boss);s.FindProperty("region3RepeatBossDefinition").objectReferenceValue=definition;s.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/RaiderSniper/authored.txt","Dedicated Sniper 125HP / 50%, Region C revisit only. Approved bytes copied unchanged.");
    }
    static Sprite[] Frames(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(v=>v.name).ToArray();
    static void Array(SerializedObject s,string field,Sprite[] frames){var a=s.FindProperty(field);a.arraySize=frames.Length;for(int i=0;i<frames.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];}
    static Transform Child(Transform p,string name) { var t=p.Find(name); if(t==null){t=new GameObject(name).transform;t.SetParent(p,false);}return t; }
    static Sprite Body(string state,float ppu)
    {
        string path=Copy("11_RaiderSniperCommander/raider_sniper_commander_"+state);
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
