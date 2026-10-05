using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit saved-state authoring; never executes on import. Source PNGs remain byte-identical.
public static class RaiderReworkAuthoring
{
    public const string Art = "Assets/Art/RaiderAssault/Rework/";
    [Serializable] class Cell { public int x,y,w,h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class Sheet { public Frame[] frames; }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var field = Import("29_RaiderRamShield/Raider_ShieldField",new Vector2(.5f,.5f),out _);
        var ram = Import("29_RaiderRamShield/VFX_Raider_RamShield",new Vector2(.5f,0),out var data);
        Edit(RaiderAssaultAuthoring.Boss, root =>
        {
            var boss = root.GetComponent<PirateCommanderBossController>(); var visual = root.transform.Find("BossVisualRoot");
            var hull = visual.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite!=null && r.sprite.texture.width==128);
            var holder = Child(visual,"RaiderProtection"); var view = Ensure<RaiderShieldPresentation>(holder.gameObject);
            var shell = Sprite(Child(holder,"NormalField"),field[0],hull.sortingOrder+1);
            // 128px at 32PPU versus the existing 64PPU hull, plus a uniform 1.23 fit.
            shell.transform.localScale = Vector3.one * .615f; shell.color = new Color(1,1,1,.58f); shell.enabled=false;
            var cap = Child(holder,"RamField"); cap.localPosition=new Vector3(0,.7f,0);cap.localScale=Vector3.one*.8f;
            var capSprite=Sprite(cap,ram[0],hull.sortingOrder+2); capSprite.enabled=false;
            var effect=Ensure<PhaseCombatVfx>(cap.gameObject);
            Ref(effect,"visual",capSprite); SetArray(effect,"frames",ram);
            var so=new SerializedObject(effect);var durations=so.FindProperty("frameDurations");durations.arraySize=data.frames.Length;
            for(int i=0;i<data.frames.Length;i++)durations.GetArrayElementAtIndex(i).floatValue=data.frames[i].duration*.001f;so.ApplyModifiedPropertiesWithoutUndo();
            Ref(view,"normalField",shell);Ref(view,"ramField",effect);
            Ref(view,"laneBorder",Line(Child(root.transform,"RamWarningBorder"),.055f,new Color(1,.28f,.08f,.9f),-2));
            Ref(view,"laneFill",Line(Child(root.transform,"RamWarningFill"),2.6f,new Color(1,.18f,.04f,.075f),-3));
            var ghosts=new SpriteRenderer[2];
            for(int i=0;i<ghosts.Length;i++){ghosts[i]=Sprite(Child(root.transform,"RamAfterimage"+i),hull.sprite,hull.sortingOrder-2);ghosts[i].color=new Color(.8f,.22f,.12f,.15f);ghosts[i].enabled=false;}
            SetArray(view,"afterimages",ghosts);Ref(boss,"shieldPresentation",view);
        });
        var inactive=Import("02_Core/gray_raider/core_gray_raider_inactive",Vector2.one*.5f,out _);
        var activation=Import("02_Core/gray_raider/core_gray_raider_activation",Vector2.one*.5f,out var activationData);
        var icon=Import("02_Core/gray_raider/core_gray_raider_icon",Vector2.one*.5f,out _);
        var shard=Import("02_Core/gray_raider/core_gray_raider_shard",Vector2.one*.5f,out _);
        Import("02_Core/gray_raider/core_gray_raider_active",Vector2.one*.5f,out _);
        var original=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Space Kit/Core/core1.controller");
        string controllerPath=Art+"GrayCore.overrideController";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if(controller==null){controller=new AnimatorOverrideController(original);AssetDatabase.CreateAsset(controller,controllerPath);}
        foreach(var old in original.animationClips) controller[old]=Clip("GrayCore_"+old.name,activation,activationData,old);
        EditorUtility.SetDirty(controller);
        var pulses=Import("15_GameplayVFX/VFX_CoreActivation",Vector2.one*.5f,out var pulseData);
        var pulseClip=Clip("GrayCorePulse",pulses.Take(6).ToArray(),new Sheet{frames=pulseData.frames.Take(6).ToArray()});
        string pulseControllerPath=Art+"GrayCorePulse.controller";
        var pulseController=AssetDatabase.LoadAssetAtPath<AnimatorController>(pulseControllerPath);if(pulseController==null)pulseController=AnimatorController.CreateAnimatorControllerAtPathWithClip(pulseControllerPath,pulseClip);
        string pulsePrefab=Art+"GrayCorePulse.prefab";
        Edit(pulsePrefab,root=>{Sprite(root.transform,pulses[0],80);var animator=Ensure<Animator>(root);animator.runtimeAnimatorController=pulseController;});
        Edit("Assets/03_Prefabs/Object/Core.prefab",root=>
        {
            var family=root.GetComponent<CoreFamilyPresentation>();
            Ref(family,"grayController",controller);Ref(family,"grayIdle",inactive[0]);Ref(family,"grayIcon",icon[0]);Ref(family,"grayShard",shard[0]);
            Ref(family,"grayPulse",AssetDatabase.LoadAssetAtPath<GameObject>(pulsePrefab));
        });
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/RaiderRework/authored.txt","Saved Commander local Shield/Ram/lane bindings and Gray Core presentation. No scenes, approved source pixels or gameplay definition assets rewritten.");
    }
    static T Ensure<T>(GameObject g) where T:Component { var c=g.GetComponent<T>(); if(c==null)c=g.AddComponent<T>(); return c; }
    static SpriteRenderer Sprite(Transform t,Sprite sprite,int order){var r=Ensure<SpriteRenderer>(t.gameObject);r.sprite=sprite;r.sortingOrder=order;return r;}
    static LineRenderer Line(Transform t,float width,Color color,int order)
    {var l=Ensure<LineRenderer>(t.gameObject);l.useWorldSpace=true;l.positionCount=0;l.startWidth=l.endWidth=width;l.startColor=l.endColor=color;l.numCapVertices=0;l.sortingOrder=order;l.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/02_Scripts/Resources/VFX/M_SpriteWhiteFlash.mat");l.enabled=false;return l;}
    static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}return t;}
    static void Ref(Object o,string field,Object value){var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void SetArray(Object o,string field,Object[] values){var s=new SerializedObject(o);var a=s.FindProperty(field);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    static void Edit(string path,Action<GameObject> edit)
    {bool exists=File.Exists(path);var root=exists?PrefabUtility.LoadPrefabContents(path):new GameObject(Path.GetFileNameWithoutExtension(path));try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{if(exists)PrefabUtility.UnloadPrefabContents(root);else Object.DestroyImmediate(root);}}
    static Sprite[] Import(string source,Vector2 pivot,out Sheet data)
    {
        string input="ArtTools/Aseprite/Output/"+source,path=Art+Path.GetFileName(source)+".png";var bytes=File.ReadAllBytes(input+".png");
        if(File.Exists(path)&&!File.ReadAllBytes(path).SequenceEqual(bytes))throw new Exception("Refusing different art: "+path);
        if(!File.Exists(path))File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        data=JsonUtility.FromJson<Sheet>(File.ReadAllText(input+".json"));
        if (data.frames == null) data.frames = new[]{new Frame{frame=new Cell{x=0,y=0,w=128,h=128},duration=200}};
        var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Multiple;
        t.spritePixelsPerUnit=32;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.alphaIsTransparency=true;
        var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);
        t.GetSourceTextureWidthAndHeight(out _,out int height);
        var f=new SpriteDataProviderFactories();f.Init();var provider=f.GetSpriteEditorDataProviderFromObject(t);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();
        var rects=data.frames.Select((v,i)=>new SpriteRect{name=Path.GetFileName(source)+"_"+i.ToString("00"),rect=new Rect(v.frame.x,height-v.frame.y-v.frame.h,v.frame.w,v.frame.h),pivot=pivot,alignment=SpriteAlignment.Custom,spriteID=old.FirstOrDefault(r=>r.name==Path.GetFileName(source)+"_"+i.ToString("00"))?.spriteID??GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();t.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
    }
    static AnimationClip Clip(string name,Sprite[] sprites,Sheet data,AnimationClip original=null)
    {
        string path=Art+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}if(original!=null)EditorUtility.CopySerialized(original,clip);else clip.frameRate=100;
        float total=data.frames.Sum(f=>f.duration)*.001f,duration=original!=null?original.length:total,elapsed=0;
        var keys=new ObjectReferenceKeyframe[sprites.Length+1];
        for(int i=0;i<sprites.Length;i++){keys[i]=new ObjectReferenceKeyframe{time=elapsed/total*Mathf.Max(0,duration-.01f),value=sprites[i]};elapsed+=data.frames[i].duration*.001f;}
        keys[sprites.Length]=new ObjectReferenceKeyframe{time=Mathf.Max(0,duration-.01f),value=sprites.Last()};
        AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);EditorUtility.SetDirty(clip);return clip;
    }
}
