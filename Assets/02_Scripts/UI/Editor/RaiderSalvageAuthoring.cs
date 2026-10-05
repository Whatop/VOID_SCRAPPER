using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RaiderSalvageAuthoring
{
    public const string Art = "Assets/Art/RaiderSalvage/";
    public const string Boss = "Assets/03_Prefabs/Enemy/PF_Boss_RaiderSalvageCarrier.prefab";
    public const string Salvage = "Assets/03_Prefabs/Enemy/CarrierCombatSalvage.prefab";
    public const string Definition = "Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region2_Repeat_Carrier.asset";
    public const string Vfx = "Assets/03_Prefabs/VFX/RaiderSalvage";
    [Serializable] class Cell { public int x,y,w,h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        Sprite idle=Body("idle",64), active=Body("salvage_active",64), overload=Body("cargo_overload",64);
        MakeVfx(Vfx+"Pull.prefab","18_RaiderBossVFX/VFX_Raider_Salvage_Beam",1f,new Vector2(0,.5f));
        var scrap=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/ApprovedIntegration/World/Pickup_Scrap.png").OfType<Sprite>().OrderBy(v=>v.name).First();
        EditPrefab(Salvage,root=>
        {
            root.layer=LayerMask.NameToLayer("Enemy");
            var rb=root.GetComponent<Rigidbody2D>();if(rb==null)rb=root.AddComponent<Rigidbody2D>(); rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;rb.interpolation=RigidbodyInterpolation2D.Interpolate;
            var col=root.GetComponent<CircleCollider2D>();if(col==null)col=root.AddComponent<CircleCollider2D>();col.isTrigger=true;col.radius=.22f;
            var view=root.GetComponent<SpriteRenderer>();if(view==null)view=root.AddComponent<SpriteRenderer>();view.sprite=scrap;view.color=new Color(1,.36f,.24f,1);view.sortingOrder=7;
            root.transform.localScale=Vector3.one*1.5f;
            var item=root.GetComponent<CarrierCombatSalvage>();if(item==null)item=root.AddComponent<CarrierCombatSalvage>();var s=new SerializedObject(item);s.FindProperty("body").objectReferenceValue=rb;s.ApplyModifiedPropertiesWithoutUndo();
        });
        string projectile="Assets/03_Prefabs/Projectile/CarrierCargoChunk.prefab";
        var original=AssetDatabase.LoadAssetAtPath<ProjectileDefinition>("Assets/02_Scripts/Config/ProjectileDefinition/Projectile_Enemy.asset");
        Directory.CreateDirectory(Path.GetDirectoryName(projectile));
        if(!File.Exists(projectile))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original.ProjectilePrefab),projectile);
        EditPrefab(projectile,root=>
        {
            var v=root.GetComponentInChildren<SpriteRenderer>();v.sprite=scrap;v.color=new Color(1,.48f,.2f,1);v.sortingOrder=8;
            // Existing Bullet resets to the authored scale on every pool get.
            v.transform.localScale=Vector3.one*1.3f;
        });
        string shotDef="Assets/02_Scripts/Config/ProjectileDefinition/Projectile_CarrierCargo.asset";
        if(!File.Exists(shotDef))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original),shotDef);
        var ds=new SerializedObject(AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(shotDef));ds.FindProperty("projectilePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(projectile);ds.FindProperty("projectileId").stringValue="carrier_cargo";ds.ApplyModifiedPropertiesWithoutUndo();
        if(!File.Exists(Definition))AssetDatabase.CopyAsset("Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region1_Repeat_Raider.asset",Definition);
        var definition=AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(Definition);var campaign=new SerializedObject(definition);
        // Preserve the repeat definition's non-story identity and all reward flags/values.
        campaign.FindProperty("displayName").stringValue="\uC57D\uD0C8\uC790 \uC778\uC591 \uBAA8\uD568";campaign.FindProperty("subtitle").stringValue="RAIDER SALVAGE CARRIER";campaign.ApplyModifiedPropertiesWithoutUndo();
        if(!File.Exists(Boss))AssetDatabase.CopyAsset(RaiderAssaultAuthoring.Boss,Boss);
        EditPrefab(Boss,root=>
        {
            var previous=root.GetComponent<PirateCommanderBossController>();if(previous!=null)Object.DestroyImmediate(previous);
            root.name="PF_Boss_RaiderSalvageCarrier";
            var c=root.GetComponent<RaiderSalvageCarrierBossController>();if(c==null)c=root.AddComponent<RaiderSalvageCarrierBossController>();
            var s=new SerializedObject(c);var visual=root.transform.Find("BossVisualRoot");var renderer=visual.GetComponentInChildren<SpriteRenderer>();renderer.sprite=idle;renderer.color=Color.white;
            foreach(string name in new[]{"AssaultLeftMuzzle","AssaultRightMuzzle"}){var t=visual.Find(name);if(t!=null)Object.DestroyImmediate(t.gameObject);}
            var intake=Child(visual,"SalvageIntake");intake.localPosition=new Vector3(0,.7f,0);
            var port=Child(visual,"CargoPort");port.localPosition=new Vector3(.45f,.65f,0);
            s.FindProperty("visualRoot").objectReferenceValue=visual;s.FindProperty("bodyRenderer").objectReferenceValue=renderer;
            var pips=s.FindProperty("cargoIndicators");pips.arraySize=6;
            for(int i=0;i<6;i++)
            {
                var t=Child(visual,"CargoIndicator"+i);t.localPosition=new Vector3(-.5f+i*.2f,-.1f,0);t.localScale=Vector3.one*.22f;
                var v=t.GetComponent<SpriteRenderer>();if(v==null)v=t.gameObject.AddComponent<SpriteRenderer>();v.sprite=scrap;v.sortingOrder=9;v.enabled=false;pips.GetArrayElementAtIndex(i).objectReferenceValue=v;
            }
            s.FindProperty("intake").objectReferenceValue=intake;s.FindProperty("dischargePort").objectReferenceValue=port;
            s.FindProperty("idleSprite").objectReferenceValue=idle;s.FindProperty("salvageSprite").objectReferenceValue=active;s.FindProperty("overloadSprite").objectReferenceValue=overload;
            s.FindProperty("salvagePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Salvage).GetComponent<CarrierCombatSalvage>();
            s.FindProperty("projectileDefinition").objectReferenceValue=original;s.FindProperty("dischargeDefinition").objectReferenceValue=ds.targetObject;
            string[] fields={"pullPrefab","muzzlePrefab","sparksPrefab","criticalPrefab"};string[] paths={Vfx+"Pull.prefab",RaiderAssaultAuthoring.Vfx+"Muzzle.prefab",RaiderAssaultAuthoring.Vfx+"Sparks.prefab",RaiderAssaultAuthoring.Vfx+"Critical.prefab"};
            for(int i=0;i<fields.Length;i++)s.FindProperty(fields[i]).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]).GetComponent<PhaseCombatVfx>();s.ApplyModifiedPropertiesWithoutUndo();
            var h=new SerializedObject(root.GetComponent<EnemyHealth>());h.FindProperty("componentsToDisableOnDeath").arraySize=1;h.FindProperty("componentsToDisableOnDeath").GetArrayElementAtIndex(0).objectReferenceValue=c;h.ApplyModifiedPropertiesWithoutUndo();
            var death=new SerializedObject(root.GetComponent<BossDummyController>());death.FindProperty("campaignDefinition").objectReferenceValue=definition;death.ApplyModifiedPropertiesWithoutUndo();
        });
        EditPrefab("Assets/03_Prefabs/Object/Core.prefab",root=>
        {
            var s=new SerializedObject(root.GetComponent<CoreObject>());s.FindProperty("region2RepeatBossPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Boss);s.FindProperty("region2RepeatBossDefinition").objectReferenceValue=definition;s.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/RaiderSalvage/authored.txt","Independent Carrier + Region B repeat binding. Shared repeat reward/death authority retained. Approved bytes copied unchanged.");
    }
    static Transform Child(Transform p,string name) { var t=p.Find(name); if(t==null){t=new GameObject(name).transform;t.SetParent(p,false);}return t; }
    static Sprite Body(string state,float ppu)
    {
        string path=Copy("10_RaiderSalvageCarrier/raider_salvage_carrier_"+state);
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
