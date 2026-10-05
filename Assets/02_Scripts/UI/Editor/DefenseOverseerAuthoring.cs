using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DefenseOverseerAuthoring
{
    public const string Art = "Assets/Art/DefenseOverseer/";
    public const string Boss = "Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab";
    public const string Zone = "Assets/03_Prefabs/VFX/DefenseBarrageZone.prefab";
    public const string Flash = "Assets/03_Prefabs/VFX/DefenseBatteryFlash.prefab";
    [Serializable] class Cell { public int x, y, w, h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/DefenseOverseer.png");
        var charged = Body("defense_overseer_barrage_charged", idle.pixelsPerUnit);
        var broken = Body("defense_overseer_armor_broken", idle.pixelsPerUnit);
        var warning = SheetSprites("16_SystemBossVFX/VFX_Barrage_TargetTelegraph", new Vector2(.5f,.5f));
        var impact = SheetSprites("17_SystemBossVFX_Revisions/VFX_Barrage_HeavyImpact", new Vector2(.5f,.5f));
        var flash = SheetSprites("16_SystemBossVFX/VFX_Barrage_BatteryFlash", new Vector2(.125f,.5f));
        EditPrefab(Zone, root =>
        {
            var owner = Get<DefenseBarrageZone>(root);
            var rb = Get<Rigidbody2D>(root); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
            var circle = Get<CircleCollider2D>(root); circle.isTrigger = true; circle.enabled = false;
            var visual = Get<SpriteRenderer>(Child(root.transform,"Visual").gameObject);
            visual.sprite = warning[0]; visual.enabled = false; visual.sortingOrder = -2;
            Set(owner,"visual",visual); Set(owner,"impactCollider",circle);
            Array(owner,"warningFrames",warning); Array(owner,"impactFrames",impact);
        });
        var clipPath = Art + "BatteryFlash.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip,clipPath); }
        clip.frameRate = 200;
        var sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText("ArtTools/Aseprite/Output/16_SystemBossVFX/VFX_Barrage_BatteryFlash.json"));
        float elapsed = 0;
        var keys = new ObjectReferenceKeyframe[flash.Length + 1];
        for (int i=0;i<flash.Length;i++) { keys[i] = new ObjectReferenceKeyframe {time=elapsed,value=flash[i]}; elapsed += sheet.frames[i].duration*.001f; }
        keys[flash.Length] = new ObjectReferenceKeyframe { time=.205f,value=flash[flash.Length-1] };
        AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding {path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
        EditorUtility.SetDirty(clip);
        var controllerPath = Art + "BatteryFlash.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath,clip);
        EditPrefab(Flash, root =>
        {
            root.transform.localScale = Vector3.one * .32f;
            var visual = Get<SpriteRenderer>(root); visual.sprite=flash[0]; visual.sortingOrder=8;
            Get<Animator>(root).runtimeAnimatorController=controller;
        });
        EditPrefab(Boss, root =>
        {
            var c = root.GetComponent<FrigateTriadBossController>();
            Set(c,"useDefenseOverseerSequence",true);
            Set(c,"bossDisplayName","방어 감독관"); Set(c,"bossSubtitle","DEFENSE OVERSEER");
            Set(c,"defenseIdleSprite",idle); Set(c,"defenseChargedSprite",charged); Set(c,"defenseBrokenSprite",broken);
            Set(c,"defenseBarragePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Zone).GetComponent<DefenseBarrageZone>());
            Set(c,"defenseBatteryFlashPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Flash));
            var left = new Transform[3]; var right = new Transform[3];
            var parts = root.GetComponentsInChildren<FrigateBossPart>(true);
            for (int i=0;i<parts.Length;i++)
            {
                var p=parts[i];
                p.VisualRoot.localRotation=Quaternion.Euler(0,0,180); // Art faces up; formation fires downward. Physics root is untouched.
                p.VisualRenderer.sprite=idle;
                left[i]=Child(p.FirePointRoot.transform,"DefenseLeftBattery");
                right[i]=Child(p.FirePointRoot.transform,"DefenseRightBattery");
                left[i].localPosition=new Vector3(-.30f,-.39f,0);
                right[i].localPosition=new Vector3(.30f,-.39f,0);
            }
            Array(c,"defenseLeftMuzzles",left); Array(c,"defenseRightMuzzles",right);
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/DefenseOverseer/authored.txt","Region B prefab, pooled barrage/flash, and five byte-identical approved art copies. Existing IDs, health, colliders, campaign/reward owners preserved.");
    }
    static T Get<T>(GameObject g) where T:Component { var c=g.GetComponent<T>(); if(c==null)c=g.AddComponent<T>(); return c; }
    static Transform Child(Transform parent,string name) { var t=parent.Find(name); if(t==null) {t=new GameObject(name).transform;t.SetParent(parent,false);} return t; }
    static void EditPrefab(string path,Action<GameObject> action)
    {
        bool exists=File.Exists(path); var root=exists?PrefabUtility.LoadPrefabContents(path):new GameObject(Path.GetFileNameWithoutExtension(path));
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { if(exists)PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }
    static string Copy(string source)
    {
        string path=Art+Path.GetFileName(source)+".png";
        byte[] bytes=File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png");
        if(File.Exists(path)&&!File.ReadAllBytes(path).SequenceEqual(bytes))throw new Exception("Production art differs: "+path);
        if(!File.Exists(path))File.WriteAllBytes(path,bytes);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return path;
    }
    static void Settings(TextureImporter t,float ppu)
    {
        t.textureType=TextureImporterType.Sprite;t.spritePixelsPerUnit=ppu;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;
        var s=new TextureImporterSettings();t.ReadTextureSettings(s);s.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(s);
    }
    static Sprite Body(string name,float ppu)
    {
        string path=Copy("07_DefenseOverseer/"+name);var t=(TextureImporter)AssetImporter.GetAtPath(path);Settings(t,ppu);
        t.spriteImportMode=SpriteImportMode.Single;t.spritePivot=new Vector2(.5f,.5f);t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite[] SheetSprites(string source,Vector2 pivot)
    {
        string path=Copy(source);string name=Path.GetFileName(source);
        var data=JsonUtility.FromJson<Sheet>(File.ReadAllText("ArtTools/Aseprite/Output/"+source+".json"));
        var t=(TextureImporter)AssetImporter.GetAtPath(path);Settings(t,32);t.spriteImportMode=SpriteImportMode.Multiple;
        t.GetSourceTextureWidthAndHeight(out _,out int height);
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(t);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();
        var rects=data.frames.Select((f,i)=>new SpriteRect {name=name+"_"+i.ToString("00"),rect=new Rect(f.frame.x,height-f.frame.y-f.frame.h,f.frame.w,f.frame.h),pivot=pivot,alignment=SpriteAlignment.Custom,spriteID=old.FirstOrDefault(r=>r.name==name+"_"+i.ToString("00"))?.spriteID??GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();t.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
    }
    static void Set(Object o,string field,object value)
    {
        var s=new SerializedObject(o);var p=s.FindProperty(field);
        if(value is bool b)p.boolValue=b;else if(value is string str)p.stringValue=str;else p.objectReferenceValue=(Object)value;
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Array(Object o,string field,Object[] values)
    {
        var s=new SerializedObject(o);var p=s.FindProperty(field);p.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();
    }
}
