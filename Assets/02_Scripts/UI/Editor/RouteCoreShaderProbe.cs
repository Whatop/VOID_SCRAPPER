using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Disposable saved-scene QA. Uses isolated transaction-test.json only; never saves scenes or user progression.
[InitializeOnLoad]
public static class RouteCoreShaderProbe
{
    private const string Key = "RouteCoreShader.Render";
    private const string Output = "Logs/RouteCoreShader/Rendered/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly StringBuilder audit = new StringBuilder();
    private static IEnumerator<float> sequence;
    private static double nextAt;
    private static SettlementDefenseEncounterController encounter;
    private static SettlementRouteCoreController route;
    private static PermanentProgress progress;
    private static PlayerHealth player;
    private static PlayerController2D movement;
    private static PlayerInteractor interactor;
    private static Camera camera;
    private static Canvas canvas;
    private static GameObject deck, management, back;
    private static RenderTexture target;
    private static VoidScrapperLocalizationService localization;
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        audit.AppendLine("PASS " + description);
    }
    static RouteCoreShaderProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before rendered validation.");
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "failure.txt")) File.Delete(Output + "failure.txt");
        Scene scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath, OpenSceneMode.Single);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var keep = new[] { encounter.gameObject, Get<GameObject>(encounter, "deckRoot"), Get<GameObject>(encounter, "deckCanvas"),
            Get<GameObject>(encounter, "managementCanvas"), Get<Camera>(encounter, "deckCamera").gameObject,
            RouteCoreDeckAuthoring.Single<SettlementController>(scene).gameObject,
            RouteCoreDeckAuthoring.Single<SettlementUIController>(scene).gameObject,
            RouteCoreDeckAuthoring.Single<EventSystem>(scene).gameObject };
        foreach (GameObject go in keep) { go.SetActive(false); go.transform.SetParent(null, true); }
        foreach (GameObject root in scene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        keep[5].GetComponent<SettlementController>().enabled = false;
        keep[6].GetComponent<SettlementUIController>().enabled = false;
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            sequence = Check().GetEnumerator(); nextAt = EditorApplication.timeSinceStartup + .3;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate(); // Keep the hidden Editor's actual Play Mode advancing.
        if (EditorApplication.timeSinceStartup < nextAt) return;
        try
        {
            if (sequence.MoveNext()) nextAt = EditorApplication.timeSinceStartup + sequence.Current;
            else
            {
                File.WriteAllText(Output + "audit.txt", audit.ToString());
                EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText(Output + "failure.txt", ex.ToString());
            File.WriteAllText(Output + "audit.txt", audit.ToString());
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Setup()
    {
        Application.runInBackground = true;
        Scene scene = SceneManager.GetActiveScene();
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        route = Get<SettlementRouteCoreController>(encounter, "routeCore");
        deck = Get<GameObject>(encounter, "deckRoot"); management = Get<GameObject>(encounter, "managementCanvas");
        back = Get<GameObject>(encounter, "facilityNavigation"); canvas = Get<GameObject>(encounter, "deckCanvas").GetComponent<Canvas>();
        player = Get<PlayerHealth>(encounter, "player"); movement = player.GetComponent<PlayerController2D>();
        interactor = player.GetComponent<PlayerInteractor>(); camera = Get<Camera>(encounter, "deckCamera");
        var services = new GameObject("Transient deck validation owners"); services.SetActive(false);
        progress = services.AddComponent<PermanentProgress>(); typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        Set(progress, "equipmentCatalog", Get<TraitCatalog>(encounter, "traitCatalog"));
        Set(progress, "equipmentShips", Get<ShipDefinition[]>(encounter, "ships").ToList());
        var state = services.AddComponent<GameStateManager>(); Set(state, "currentState", GameState.Settlement);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        var store = services.AddComponent<RunRuntimeTraitStore>(); typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        localization = services.AddComponent<VoidScrapperLocalizationService>();
        Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        Language("ko");
        new GameObject("Disposable projectile pool").AddComponent<PoolManager>();
        progress.LoadFromSave(new SaveData());
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create();
        camera.targetTexture = target; camera.gameObject.SetActive(true);
        foreach (Canvas c in new[] { canvas, management.GetComponent<Canvas>() })
        {
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1;
        }
        management.SetActive(true); encounter.gameObject.SetActive(true);
        RouteCoreDeckAuthoring.Single<EventSystem>(scene).gameObject.SetActive(true);
        audit.AppendLine("Actual saved world in Play Mode; actual camera/2D lighting and 480x270 RenderTexture; transient progression; no SaveManager.");
        Require(SaveManager.Instance == null && RunManager.Instance == null, "No save or expedition owner in disposable fixture");
    }
    private static void Language(string code)
    {
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, code);
        Set(localization, "currentLanguageCode", code);
    }
    private static void Move(Vector2 p)
    {
        player.GetComponent<Rigidbody2D>().position = p; player.transform.position = p;
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static PlayerRadarScanner scanner;
    private static SettlementDefensePurpleCore purple;
    private static int playerHits;
    private static float playerDamage;
    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static void AimAt(Vector3 point, bool fire = false, bool scan = false)
    {
        Vector2 screen = camera.WorldToScreenPoint(point);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)((fire ? 1 : 0) | (scan ? 1 << (int)MouseButton.Back : 0)) });
    }
    private static IEnumerable<float> Wait(Func<bool> predicate, string context, float timeout = 20)
    {
        int frame = Time.frameCount; float began = Time.time;
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!predicate() && EditorApplication.timeSinceStartup < deadline) yield return .01f;
        if (!predicate())
            audit.AppendLine("TIMEOUT " + context + " frames=" + (Time.frameCount - frame) + " gameSeconds=" + (Time.time - began) + " scale=" + Time.timeScale + " pause=" + GameplayPauseManager.IsPaused +
                " playerDead=" + player.IsDead + " active=" + encounter.IsActive + " cores=" + string.Join(",", encounter.Cores.Select(c => c.Part + ":" + c.State + ":" + c.Attack + ":" + c.ShotsFired)));
        Require(predicate(), context);
    }
    private static Bullet[] EnemyShots() => Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b => b.Owner == ProjectileOwner.Enemy).ToArray();
    private static IEnumerable<float> DefeatAndFuse(int index)
    {
        var core = encounter.Cores[index];
        core.Health.TakeDamage(10000); yield return .12f;
        Require(core.State == SettlementDefenseCorruptedCore.CoreState.AwaitingReactivation, "Defeat waits for explicit interaction " + core.Part);
        Require(!Get<LineRenderer>(core, "telegraph").enabled && EnemyShots().Length == 0, "Defeat cleans owned telegraph/projectiles " + core.Part);
        int count = core.ShotsFired; yield return .35f;
        Require(core.ShotsFired == count && encounter.FusedCount == index, "No shots or auto-fusion while awaiting reactivation");
        Move(core.transform.position + Vector3.down * .8f); yield return .15f;
        Require(ReferenceEquals(interactor.CurrentTarget, core), "Physical defeated-core target acquired " + core.Part);
        Capture("reactivate-" + index);
        interactor.TryInteract(); yield return .35f;
        Capture("fusion-" + index);
        foreach (float wait in Wait(() => index < 2 ? encounter.Cores[index + 1].State == SettlementDefenseCorruptedCore.CoreState.CombatActive : encounter.IsPurpleActive,
            "Fusion advances into " + (index < 2 ? "next component" : "Purple, not completion"))) yield return wait;
    }
    private static IEnumerable<float> QuickScan()
    {
        AimAt(purple.transform.position, false, true); yield return .12f;
        AimAt(purple.transform.position); yield return .08f;
    }
    private static int ShaderMaterials() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.shader != null && m.shader.name == "VOID SCRAPPER/Route Core/SG_RouteCoreCorruption");
    private static IEnumerable<float> ReachPurple()
    {
        Require(route.BeginSettlementDefense(), "Existing authority starts fresh encounter");
        foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Retry starts Orange")) yield return wait;
        player.SetDashInvincible(true);
        for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        Move(new Vector2(-1.8f, -1.7f)); AimAt(purple.transform.position);
    }
    private static IEnumerable<float> Check()
    {
        Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        scanner = Get<PlayerRadarScanner>(encounter, "deckRadar"); purple = Get<SettlementDefensePurpleCore>(encounter, "purpleCore");
        var data = (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), 8 });
        progress.LoadFromSave(data);
        var ship = Get<ShipDefinition[]>(encounter, "ships").First(s => s.DefaultWeaponTree == WeaponTreeType.MachineGun);
        progress.SetSelectedShipId(ship.ShipId);
        var visual = Get<RouteCoreCorruptionVisual>(purple, "corruptionPresentation");
        var overlay = Get<SpriteRenderer>(visual, "overlay");
        var material = overlay.sharedMaterial;
        Require(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "Sprite Unlit graph supported and compiles on actual graphics device");
        audit.AppendLine("GPU " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceType);
        audit.AppendLine("Original base material: " + Get<SpriteRenderer>(purple, "visual").sharedMaterial.name + " / " + Get<SpriteRenderer>(purple, "visual").sharedMaterial.shader.name);
        int materials = ShaderMaterials();
        int completions = 0; route.DefenseCompleted.AddListener(() => completions++);
        encounter.EnterDeck(); yield return .3f;
        foreach (float wait in ReachPurple()) yield return wait;
        Capture("01-purple-transition"); yield return .3f;
        Capture("02-hidden-radar-closed");
        Require(purple.Health.CurrentHp == 50 && !Get<Collider2D>(purple, "damageCollider").enabled, "Hidden target retains 50 HP and blocked damage");
        object propertyBlock = Get<MaterialPropertyBlock>(visual, "block");
        // Isolated warmed shader-controller update workload; excludes DOTween creation and other encounter systems.
        var apply = (Action<float, float, float, Color>)Delegate.CreateDelegate(typeof(Action<float, float, float, Color>), visual,
            typeof(RouteCoreCorruptionVisual).GetMethod("Apply", Private));
        for (int i = 0; i < 100; i++) apply(0, 1, 1, new Color(.8f, .3f, 1, 1));
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew(); // Exclude stopwatch allocation below.
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) apply(0, 1, 1, new Color(.8f, .3f, 1, 1));
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        audit.AppendLine("PROFILE shader Apply x10000: " + bytes + " managed bytes; " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms; warmed direct delegate, no reflection inside measurement.");
        Require(bytes == 0, "Shader property-update workload has no managed allocations after warmup");
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .2f;
        Require(scanner.IsRadarOpen && purple.State == SettlementDefensePurpleCore.Phase.Hidden, "Opening Radar alone does not reveal");
        Capture("03-radar-open");
        AimAt(purple.transform.position, false, true);
        foreach (float wait in Wait(() => purple.State == SettlementDefensePurpleCore.Phase.Exposed, "Real Mouse4 active scan exposes")) yield return wait;
        AimAt(purple.transform.position);
        float exposureStart = Time.time;
        Capture("04-acquisition-start");
        Require(Get<Collider2D>(purple, "damageCollider").enabled, "Gameplay damage enabled immediately during visual acquisition");
        yield return .08f; Capture("05-acquisition-signal");
        yield return .18f; Capture("06-exposed");
        yield return .2f; Capture("07-scan-wave");
        foreach (float wait in Wait(() => purple.ShotsFired == 8, "Unchanged eight pooled pressure projectiles")) yield return wait;
        yield return .3f; Capture("08-exposed-pressure");
        // Also render at 2x resolution, with the same composition; native 480 remains authoritative.
        var large = new RenderTexture(960, 540, 24) { antiAliasing = 1 }; large.Create();
        camera.targetTexture = large; canvas.GetComponent<CanvasScaler>().scaleFactor = 2;
        camera.Render(); RenderTexture.active = large;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        File.WriteAllBytes(Output + "large-exposed.png", tex.EncodeToPNG()); Object.Destroy(tex); RenderTexture.active = null;
        camera.targetTexture = target; canvas.GetComponent<CanvasScaler>().scaleFactor = 1; large.Release(); Object.Destroy(large);
        foreach (float wait in QuickScan()) yield return wait;
        Require(purple.ExposureCount == 1, "Rescan ignored while exposed");
        foreach (float wait in Wait(() => purple.State == SettlementDefensePurpleCore.Phase.Hidden, "Original exposure expires")) yield return wait;
        audit.AppendLine("METRIC first exposure observation: " + (Time.time - exposureStart).ToString("F3") + " seconds; serialized duration=" + Get<float>(purple, "exposureDuration"));
        Capture("09-rehide-transition"); yield return .25f; Capture("10-second-hidden");
        Require(!Get<Collider2D>(purple, "damageCollider").enabled, "Rehide does not extend damage window");
        AimAt(purple.transform.position, true); yield return .5f; AimAt(purple.transform.position);
        Require(purple.Health.CurrentHp == 50, "Real player bullets cannot damage hidden target");

        // Real player death while hidden, then during exposure; all fresh attempts start Orange.
        for (int retry = 0; retry < 2; retry++)
        {
            if (retry == 1)
            {
                Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .2f;
                Require(scanner.TryQuickScan(), "Existing active scan starts acquisition before interruption");
                Require(purple.State == SettlementDefensePurpleCore.Phase.Exposed, "Retry exposed before lethal interrupt");
                Require(Get<DG.Tweening.Tween>(visual, "transition") != null, "Lethal interrupt occurs during owned acquisition tween");
            }
            player.SetDashInvincible(false); Set(player, "invincibleTimer", 0f); player.TakeDamage(100000); yield return .15f;
            Require(!deck.activeSelf && !overlay.enabled && !scanner.IsRadarOpen && !progress.SettlementDefenseCleared, "Player death restores overlay/radar/deck without saving partial progress");
            Require(Get<DG.Tweening.Tween>(visual, "transition") == null, "Disable kills owned visual tween");
            encounter.EnterDeck(); yield return .2f;
            foreach (float wait in ReachPurple()) yield return wait;
            Require(ReferenceEquals(propertyBlock, Get<MaterialPropertyBlock>(visual, "block")), "Retry reuses property block");
            Require(ShaderMaterials() == materials && overlay.sharedMaterial == material && purple.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                "Retry has stable shader material count and exactly base plus one overlay");
        }
        audit.AppendLine("PROFILE corruption-material objects before/after two full retries: " + materials + "/" + ShaderMaterials() + "; authored overlay renderers=1.");
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .2f;
        foreach (float wait in QuickScan()) yield return wait;
        AimAt(purple.transform.position, true); yield return .25f; AimAt(purple.transform.position);
        Require(purple.Health.CurrentHp < 50, "Real player fire damages exposed target");
        purple.Health.TakeDamage(10000); // Short deterministic visual-death capture, not a balance measurement.
        Require(progress.SettlementDefenseCleared && completions == 1 && !encounter.IsActive, "Campaign completion occurs immediately before purification tail");
        Require(deck.activeSelf && overlay.enabled && !scanner.IsRadarOpen && !purple.Target.IsRadarVisible && EnemyShots().Length == 0, "Purification has no live radar/damage/attack authority");
        Require(purple.Health.IsDead && purple.Health.CurrentHp == 0 &&
            !Get<Collider2D>(purple, "damageCollider").enabled && !Get<Collider2D>(purple, "radarCollider").enabled,
            "Purification never re-enables EnemyHealth or either collider");
        Capture("11-purification-start"); yield return .15f; Capture("12-purification-dissolve");
        Require(purple.Health.IsDead && !Get<Collider2D>(purple, "damageCollider").enabled, "Actor remains dead throughout the visible dissolve");
        visual.enabled = false;
        Require(Get<DG.Tweening.Tween>(visual, "transition") == null, "Disabling optional presentation kills its live purification tween");
        yield return .4f;
        Require(!deck.activeSelf && management.activeSelf && !overlay.enabled && !movement.HasTemporaryAimCamera(encounter), "Independent presentation timer returns and releases ownership");
        route.CompleteSettlementDefense(); encounter.PurpleCleansed(purple); Require(completions == 1, "Completion remains exact once");
        visual.enabled = true;
        encounter.EnterDeck(); yield return .15f; Capture("13-clean-central-core");
        Require(!purple.gameObject.activeSelf && progress.CanLaunchFinalExpedition, "Clean Route Core remains eligible for FinalNetwork");
        encounter.LeaveDeck(); yield return .1f;
        Require(!overlay.enabled && Get<DG.Tweening.Tween>(visual, "transition") == null, "No visual remnants after exit");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        audit.AppendLine("Shader-specific QA only. Timed transitions use real Play Mode; component defeats scripted; player invulnerable except deliberate deaths. No subjective balance conclusion.");
    }

    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var charge = canvas.GetComponentInChildren<PlayerChargeGaugeUI>(true);
        if (canvas.gameObject.activeInHierarchy && charge.GetComponent<CanvasGroup>().alpha > .9f)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, charge.transform.position);
            Vector2 ship = camera.WorldToScreenPoint(player.transform.position);
            Require(point.x > 20 && point.x < 460 && point.y > 10 && point.y < 260,
                "Visible charge/cooling gauge stays inside 480x270: " + point);
            Require(Mathf.Abs(point.y - ship.y - 35.2f) < 2, "Charge/cooling uses Expedition's fixed 1.1-world-unit player offset");
        }
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine("CAPTURE " + name + " visibleProjectiles=" + VisibleProjectiles());
        foreach (TMP_Text text in (management.activeInHierarchy ? management : canvas.gameObject).GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrEmpty(text.text) || text.font == null) continue;
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) audit.AppendLine("OVERFLOW " + text.name + ": " + text.text);
            foreach (char c in System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", ""))
                if (!char.IsWhiteSpace(c) && !text.font.HasCharacter(c, true, true)) audit.AppendLine("MISSING U+" + ((int)c).ToString("X4") + " " + text.name);
        }
    }
    private static int VisibleProjectiles() => Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b =>
    {
        Vector3 p = camera.WorldToViewportPoint(b.transform.position);
        return p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f && p.z > 0;
    });
}

