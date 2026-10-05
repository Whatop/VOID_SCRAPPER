using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CombatReadabilityAuthoring
{
    public const string Dir = "Logs/CombatReadability/";
    public const string Art = "Assets/Art/CombatReadability/";
    public const string Vfx = "Assets/02_Scripts/Resources/VFX/Approved/";
    [Serializable] class Cell { public int x, y, w, h; }
    [Serializable] class Frame { public Cell frame; public int duration; }
    [Serializable] class ImportSettings { public Vector2 pivot; public float pixelsPerUnit = 32; }
    [Serializable] class ExportMeta { public Vector2 pivot; }
    [Serializable] class Sheet { public Frame[] frames; public ImportSettings unity; public ExportMeta meta; }
    static readonly List<string> authored = new List<string>();

    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Vfx); AssetDatabase.Refresh();
        var effects = new Dictionary<string, GameObject>();
        foreach (var pair in new[] { ("MachineGunMuzzle", "Player_MachineGunMuzzle"), ("ShotgunMuzzle", "Player_ShotgunMuzzle"),
            ("SniperMuzzle", "Player_SniperMuzzle"), ("Dash", "Player_Dash"), ("CurseDash", "Player_Dash_Curse"),
            ("GenericHit", "Hit_Generic"), ("ShieldHit", "ShieldHit"), ("PickupSparkle", "PickupSparkle") })
            effects[pair.Item1] = MakeEffect(pair.Item1, pair.Item2);

        string corePath = "Assets/03_Prefabs/Object/Core.prefab";
        var core = PrefabUtility.LoadPrefabContents(corePath);
        try { BindCore(core); PrefabUtility.SaveAsPrefabAsset(core, corePath); }
        finally { PrefabUtility.UnloadPrefabContents(core); }
        foreach (string path in Directory.GetFiles("Assets/02_Scripts/Config/ProjectileDefinition", "Projectile_*.asset"))
        {
            if (!new[] { "Projectile_MachineGun", "Projectile_Shotgun", "Projectile_Sniper" }.Contains(Path.GetFileNameWithoutExtension(path))) continue;
            var definition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(path);
            Ref(definition, "impactEffectPrefab", effects["GenericHit"]); authored.Add(path + " impact only");
        }
        string pickupPath = "Assets/03_Prefabs/Object/RewardPickup.prefab";
        var pickup = PrefabUtility.LoadPrefabContents(pickupPath);
        try { Ref(pickup.GetComponent<RewardPickup>(), "pickupSparklePrefab", effects["PickupSparkle"]); PrefabUtility.SaveAsPrefabAsset(pickup, pickupPath); }
        finally { PrefabUtility.UnloadPrefabContents(pickup); }
        var icons = new[] { "green", "orange", "blue" }.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/UI/Core_" + f + ".png")).ToList();
        icons.Add(ImportSheet("02_Core/purple_corrupted/core_purple_corrupted_icon", "Core_Purple_Icon", out _)[0]);
        foreach (string name in new[] { "Boot", "Tutorial", "Expedition", "Settlement" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var w in root.GetComponentsInChildren<PlayerWeaponBase>(true))
                {
                    string role = w is MachineGunWeapon ? "MachineGunMuzzle" : w is ShotgunWeapon ? "ShotgunMuzzle" : "SniperMuzzle";
                    Ref(w, "muzzleEffectPrefab", effects[role]); Float(w, "muzzleEffectRotationOffset", 0);
                }
                foreach (var d in root.GetComponentsInChildren<PlayerDashAfterimage>(true))
                { Ref(d, "normalDashPrefab", effects["Dash"]); Ref(d, "curseDashPrefab", effects["CurseDash"]); }
                foreach (var f in root.GetComponentsInChildren<CombatFeedbackManager>(true))
                { Ref(f, "genericHitPrefab", effects["GenericHit"]); Ref(f, "shieldHitPrefab", effects["ShieldHit"]); }
                foreach (var h in root.GetComponentsInChildren<ExpeditionHUD>(true))
                { NullArtAuthoring.SetArray(h, "coreFamilyIcons", icons.Cast<Object>().ToArray()); }
                foreach (var c in root.GetComponentsInChildren<CoreObject>(true)) BindCore(c.gameObject);
                foreach (var t in root.GetComponentsInChildren<TutorialFlowController>(true))
                {
                    var renderer = Get<SpriteRenderer>(t, "alienSignalCoreRenderer");
                    if (renderer != null) FitSprite(renderer, AssetDatabase.LoadAllAssetsAtPath("Assets/Art/NullDispatcher/PurpleCoreActive.png").OfType<Sprite>().OrderBy(s => s.name).First());
                }
                if (name == "Expedition" || name == "Tutorial") LayoutHud(root);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            authored.Add("Saved existing " + name + " scene bindings");
        }
        AssetDatabase.SaveAssets(); File.WriteAllLines(Dir + "authored.txt", authored);
    }

    static Sprite[] ImportSheet(string source, string name, out Sheet data)
    {
        string sourceRoot = "ArtTools/Aseprite/Output/" + source;
        string path = Art + name + ".png";
        var bytes = File.ReadAllBytes(sourceRoot + ".png");
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(bytes)) throw new Exception("Different production copy: " + path);
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        data = JsonUtility.FromJson<Sheet>(File.ReadAllText(sourceRoot + ".json"));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.GetSourceTextureWidthAndHeight(out _, out int height);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        // Core exports store pixel pivots in meta; gameplay VFX exports store
        // normalized pivots in unity. JsonUtility may supply an empty nested
        // object for omitted fields, so do not infer the schema from null alone.
        Vector2 pivot = source.StartsWith("02_Core/", StringComparison.Ordinal)
            ? new Vector2(data.meta.pivot.x / data.frames[0].frame.w, data.meta.pivot.y / data.frames[0].frame.h)
            : data.unity.pivot;
        var rects = data.frames.Select((f, i) => new SpriteRect { name = name + "_" + i.ToString("00"),
            rect = new Rect(f.frame.x, height - f.frame.y - f.frame.h, f.frame.w, f.frame.h), pivot = pivot,
            alignment = SpriteAlignment.Custom, spriteID = old.FirstOrDefault(r => r.name == name + "_" + i.ToString("00"))?.spriteID ?? GUID.Generate() }).ToArray();
        provider.SetSpriteRects(rects); provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport(); authored.Add(path + " <- " + sourceRoot + ".png");
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }

    static AnimationClip Clip(string path, Sprite[] sprites, Sheet sheet, float duration = 0, AnimationClip template = null)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        if (template != null) EditorUtility.CopySerialized(template, clip);
        else clip.frameRate = 100;
        clip.name = Path.GetFileNameWithoutExtension(path);
        float total = sheet.frames.Sum(f => f.duration) * .001f;
        if (duration <= 0) duration = total;
        // Unity object-reference curves include the final key's frame interval.
        // End one sample before the original duration instead of adding a frame.
        float lastKeyTime = Mathf.Max(0f, duration - 1f / clip.frameRate);
        var frames = new ObjectReferenceKeyframe[sprites.Length + 1]; float elapsed = 0;
        for (int i = 0; i < sprites.Length; i++)
        { frames[i] = new ObjectReferenceKeyframe { time = elapsed / total * lastKeyTime, value = sprites[i] }; elapsed += sheet.frames[i].duration * .001f; }
        frames[frames.Length - 1] = new ObjectReferenceKeyframe { time = lastKeyTime, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, frames);
        EditorUtility.SetDirty(clip); return clip;
    }
    static GameObject MakeEffect(string name, string source)
    {
        var sprites = ImportSheet("15_GameplayVFX/VFX_" + source, name, out Sheet data);
        return MakeEffectAsset(name, sprites, data);
    }
    static GameObject MakeEffectAsset(string name, Sprite[] sprites, Sheet data, float duration = 0)
    {
        var clip = Clip(Art + name + ".anim", sprites, data, duration);
        string controllerPath = Art + name + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
        string path = Vfx + name + ".prefab"; bool exists = File.Exists(path);
        var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        try
        {
            var renderer = root.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[0]; renderer.sortingOrder = name.StartsWith("CorePulse") ? 80 : name.Contains("Dash") ? 8 : 35;
            var animator = root.GetComponent<Animator>(); if (animator == null) animator = root.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
    static void BindCore(GameObject root)
    {
        var presentation = root.GetComponent<CoreActivationPresentation>();
        if (presentation == null) return;
        var animator = Get<Animator>(presentation, "animator");
        if (animator == null) return;
        var body = animator.GetComponent<SpriteRenderer>();
        var original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Space Kit/Core/core1.controller");
        var controllers = new List<Object>(); var idle = new List<Object>(); var pulses = new List<Object>();
        var pulseFrames = ImportSheet("15_GameplayVFX/VFX_CoreActivation", "CoreActivation", out Sheet pulseSheet);
        int pulseFamily = 1; // The sheet's first six frames are Neutral, followed by Green/Orange/Blue/Purple.
        foreach (string family in new[] { "green", "orange", "blue", "purple_corrupted" })
        {
            Sprite first = ImportSheet("02_Core/" + family + "/core_" + family + "_inactive", "Core_" + family + "_Inactive", out _)[0];
            var activation = ImportSheet("02_Core/" + family + "/core_" + family + "_activation", "Core_" + family + "_Activation", out Sheet data);
            string path = Art + "Core_" + family + ".overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null) { controller = new AnimatorOverrideController(original); AssetDatabase.CreateAsset(controller, path); }
            foreach (var oldClip in original.animationClips)
            {
                var clip = Clip(Art + "Core_" + family + "_" + oldClip.name + ".anim", activation, data, oldClip.length, oldClip);
                controller[oldClip] = clip;
                authored.Add(family + " activation duration " + oldClip.length + " -> " + clip.length);
            }
            controllers.Add(controller); idle.Add(first); EditorUtility.SetDirty(controller);
            pulses.Add(MakeEffectAsset("CorePulse_" + family, pulseFrames.Skip(pulseFamily * 6).Take(6).ToArray(),
                new Sheet { frames = pulseSheet.frames.Skip(pulseFamily * 6).Take(6).ToArray() }, .72f));
            pulseFamily++;
        }
        var owner = root.GetComponent<CoreFamilyPresentation>(); if (owner == null) owner = root.AddComponent<CoreFamilyPresentation>();
        Ref(owner, "animator", animator); Ref(owner, "body", body);
        Ref(owner, "activationPresentation", presentation); Ref(presentation, "approvedPulsePrefab", pulses[0]);
        NullArtAuthoring.SetArray(owner, "activationPulses", pulses.ToArray());
        NullArtAuthoring.SetArray(owner, "familyControllers", controllers.ToArray()); NullArtAuthoring.SetArray(owner, "idleSprites", idle.ToArray());
        // The old tiny placeholder's bounds reduce the approved 64px core to
        // about 5 screen pixels. Fit its visual child only; root/collider stay authored.
        body.sprite = (Sprite)idle[0]; body.transform.localScale = Vector3.one;
        animator.runtimeAnimatorController = (RuntimeAnimatorController)controllers[0];
    }
    static void FitSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer.sprite != null && renderer.sprite != sprite)
        {
            float ratio = renderer.sprite.bounds.size.x / sprite.bounds.size.x;
            renderer.transform.localScale *= ratio;
        }
        renderer.sprite = sprite;
    }
    static void LayoutHud(GameObject root)
    {
        foreach (var hud in root.GetComponentsInChildren<BossHealthBarUI>(true))
        {
            var holder = Get<GameObject>(hud, "rootObject").GetComponent<RectTransform>(); holder.anchoredPosition = new Vector2(0, 17);
            var slider = Get<Slider>(hud, "hpSlider"); var rect = (RectTransform)slider.transform;
            HudHangarAuthoring.Layout(rect, new Vector2(0, -4), new Vector2(196, 5));
            var background = slider.transform.Find("Background").GetComponent<Image>();
            HudHangarAuthoring.Layout(background.rectTransform, Vector2.zero, new Vector2(196, 5));
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DarkUISettlementAuthoring.Root + "32.png"); background.type = Image.Type.Sliced; background.pixelsPerUnitMultiplier = 8;
            var area = (RectTransform)slider.fillRect.parent; HudHangarAuthoring.Layout(area, Vector2.zero, new Vector2(194, 3));
            slider.fillRect.anchorMin = Vector2.zero; slider.fillRect.anchorMax = Vector2.one;
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            var name = Get<TextMeshProUGUI>(hud, "bossNameText"); var value = Get<TextMeshProUGUI>(hud, "hpText");
            HudHangarAuthoring.Layout(name.rectTransform, new Vector2(-28, 12), new Vector2(140, 14));
            HudHangarAuthoring.Layout(value.rectTransform, new Vector2(72, 12), new Vector2(52, 14));
            name.enableAutoSizing = false; name.fontSize = 12; name.alignment = TextAlignmentOptions.Left;
            value.fontSize = 10; value.enableAutoSizing = false; value.alignment = TextAlignmentOptions.Right;
            value.color = name.color;
        }
        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            string path = ApprovedVisualIntegration.PathOf(rect);
            if (path.EndsWith("StatusRoot/HPGauge ")) rect.anchoredPosition = new Vector2(-184, 116);
            if (path.EndsWith("StatusRoot/HPGauge /ValueRow")) HudHangarAuthoring.Layout(rect, new Vector2(12, 4), new Vector2(62, 12));
            if (path.EndsWith("StatusRoot/HPGauge /ValueRow/ValueText") || path.EndsWith("StatusRoot/HPGauge /HPLabel"))
            { var text = rect.GetComponent<TextMeshProUGUI>(); text.enableAutoSizing = false; text.fontSize = 10; }
            if (path.EndsWith("CoreSignal/BackGround")) rect.anchoredPosition = new Vector2(0, 105);
            if (path.EndsWith("CoreSignal/BackGround/CoreTrackingObjective"))
            { HudHangarAuthoring.Layout(rect, new Vector2(-12, 15), new Vector2(132, 12)); var text = rect.GetComponent<TextMeshProUGUI>(); text.enableAutoSizing = false; text.fontSize = 10; }
            if (path.EndsWith("CoreSignal/BackGround/CoreTrackingCount"))
            { HudHangarAuthoring.Layout(rect, new Vector2(75, 15), new Vector2(34, 12)); var text = rect.GetComponent<TextMeshProUGUI>(); text.enableAutoSizing = false; text.fontSize = 10; }
        }
    }
    static void Ref(Object owner, string field, Object value) => NullArtAuthoring.Set(owner, field, value);
    static void Float(Object owner, string field, float value)
    { var so = new SerializedObject(owner); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static void Diagnose()
    {
        ApprovedVisualIntegration.Guard();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/Enemy_Charge.prefab"));
        var player = new GameObject("QA player", typeof(Rigidbody2D)); player.tag = "Player";
        root.transform.position = Vector3.zero; player.transform.position = Vector3.up * 3;
        var ai = root.GetComponent<EnemyBaseAI>();
        Invoke(ai, "Awake");
        ai.ApplyDefinition(AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset"));
        Set(ai, "player", player.transform); Set(ai, "playerBody", player.GetComponent<Rigidbody2D>());
        Set(ai, "currentState", EnemyState.Combat); Set(ai, "chargingRepositionTimer", 1f);
        var attack = root.GetComponent<EnemyAttackController>(); Set(attack, "attackTimer", 10f);
        var sensor = root.GetComponent<EnemyVisionSensor>(); Invoke(sensor, "Awake"); sensor.SetTarget(player.transform);
        sensor.ForceDetectTarget(player.transform);
        var lines = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            Invoke(ai, "UpdateCombat");
            Invoke(sensor, "UpdateVisualAwareness", .016f);
            lines.Add("frame=" + i + " state=" + ai.CurrentState + " facing=" + ai.FacingDirection +
                " desired=" + Get<Vector2>(ai, "desiredVelocity") + " sight=" + sensor.HasRawDirectSight);
        }
        File.WriteAllLines(Dir + "charging-diagnosis.txt", lines);
        Object.DestroyImmediate(root); Object.DestroyImmediate(player);
        DumpScene();
    }
    static void DumpScene()
    {
        var lines = new List<string>();
        foreach (string sceneName in new[] { "Tutorial", "Expedition" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + sceneName + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<RectTransform>(true))
                {
                    string path = ApprovedVisualIntegration.PathOf(r);
                    if (path.Contains("BossHealth") || path.Contains("HPGauge") || path.Contains("CoreSignal"))
                        lines.Add(sceneName + "/" + path + " pos=" + r.anchoredPosition + " rect=" + r.rect + " scale=" + r.localScale);
                }
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (ApprovedVisualIntegration.PathOf(r.transform).ToLowerInvariant().Contains("core"))
                        lines.Add(sceneName + " CORE " + ApprovedVisualIntegration.PathOf(r.transform) + " sprite=" + AssetDatabase.GetAssetPath(r.sprite) + " scale=" + r.transform.lossyScale);
                foreach (var c in root.GetComponentsInChildren<CanvasScaler>(true))
                    lines.Add(sceneName + " Canvas=" + c.name + " mode=" + c.uiScaleMode + " ref=" + c.referenceResolution + " match=" + c.matchWidthOrHeight);
            }
        }
        File.WriteAllLines(Dir + "scene-before.txt", lines);
    }
    public static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    public static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    public static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
}
