using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using TMPro;
using Object = UnityEngine.Object;

// Explicit opt-in Play Mode fixture. Never saves scenes or the user's save.
[InitializeOnLoad]
public static class SectorIntroProbe
{
    const string SessionKey = "SectorIntro.Probe", Dir = "Logs/SectorIntro/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static BossPatternController boss;
    static EnemyHealth bossHealth;
    static CoreBossIntroSequence intro;
    static string scenario;
    static float lastSample = -1;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing, wasHudVisible, observing;
    static object previousOwner;
    static int cameraAcquisitions, hudAppearances;
    static float materializeStart, firstHudTime, firstCombatTime, activatedAt;
    static Vector3 lastCameraPosition;
    static float lastCameraSize;
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static SectorIntroProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Dir + "Rendered");
        SetNativeGameView();
        File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json", true);
        File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json.bak", true);
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity");
        var save = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)).Single();
        var so = new SerializedObject(save); so.FindProperty("fileName").stringValue = Path.GetFullPath(Dir + "probe-save.json"); so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(SessionKey, true); EditorApplication.EnterPlaymode();
    }
    static void SetNativeGameView()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, new object[] { Enum.ToObject(kind, 1), 480, 270, "Combat QA native" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, count - 1);
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            qaMouse = InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("SectorQAMouse");
            qaKeyboard = InputSystem.AddDevice<Keyboard>("SectorQAKeyboard");
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 600; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1); else SectorIntroValidation.RestoreScenes(); }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (finishing) return;
        try
        {
            SampleIntro();
            if (EditorApplication.timeSinceStartup < next) return;
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Probe timeout");
            if (sequence.MoveNext()) next = EditorApplication.timeSinceStartup + sequence.Current;
            else Finish();
        }
        catch (Exception e) { errors.Add(e.ToString()); Finish(); }
    }
    static void Finish()
    {
        finishing = true; EditorApplication.update -= Tick;
        if (qaMouse != null) InputSystem.RemoveDevice(qaMouse);
        if (qaKeyboard != null) InputSystem.RemoveDevice(qaKeyboard);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        Flush(); EditorApplication.ExitPlaymode();
    }
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "intro-timing.csv", motion); }
    static void Note(string value) { log.Add(DateTime.UtcNow.ToString("HH:mm:ss") + " " + value); Flush(); }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Note("PASS " + message); }
    static IEnumerable<float> Until(Func<bool> check, string label, float seconds = 80)
    { double deadline = EditorApplication.timeSinceStartup + seconds; while (!check()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception(label); yield return .02f; } Note("REACHED " + label); }
    static IEnumerable<float> Scene(string name)
    { foreach (var d in Until(() => SceneManager.GetActiveScene().name == name && !SceneFlowManager.Instance.IsLoading, name)) yield return d; yield return 3; BindCamera(); }
    static void BindCamera()
    {
        camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (target == null) { target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create(); }
        camera.targetTexture = target;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
            {
                // Camera.Render excludes Overlay canvases. Preserve their original precedence
                // when adapting them for this disposable native render target.
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) c.sortingOrder += 10000;
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1;
            }
        Canvas.ForceUpdateCanvases();
    }
    static void Capture(string name)
    {
        BindCamera(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(480, 270, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); png.Apply();
        File.WriteAllBytes(Dir + "Rendered/" + name + ".png", png.EncodeToPNG()); Object.Destroy(png); RenderTexture.active = previous;
        File.WriteAllLines(Dir + "Rendered/" + name + "-text.txt", Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .Where(t => !string.IsNullOrWhiteSpace(t.text)).Select(t => t.name + " overflow=" + t.isTextOverflowing + " rect=" + t.rectTransform.rect + " text=" + t.text.Replace('\n', '|')));
        File.WriteAllLines(Dir + "Rendered/" + name + "-sprites.txt", Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && (r.name.Contains("Core") || r.name.Contains("core") || AssetDatabase.GetAssetPath(r.sprite).Contains("SectorAdministrator") || r.name.Contains("Sector") || r.name.Contains("Boss")))
            .Select(r => r.name + " sprite=" + AssetDatabase.GetAssetPath(r.sprite) + " bounds=" + r.bounds + " viewport=" + camera.WorldToViewportPoint(r.bounds.center)));
        Note("CAPTURE " + name + " 480x270");
        Note("Screen=" + Screen.width + "x" + Screen.height + "; canvases=" + string.Join(";", Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).Select(c => c.name + " scale=" + c.scaleFactor + " rect=" + ((RectTransform)c.transform).rect)));
    }
    static void Ready()
    { Keys(); player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "authored player"); player.SetDashInvincible(true); }
    static void Move(Vector3 p)
    { var rb = player.GetComponent<Rigidbody2D>(); rb.position = p; player.transform.position = p; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    static void Mouse(bool pressed) => InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new MouseState { position = new Vector2(650, 400) }.WithButton(MouseButton.Left, pressed));
    static IEnumerable<float> Dialogue()
    {
        double deadline = EditorApplication.timeSinceStartup + 90;
        while (DialogueManager.isConversationActive)
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Dialogue timeout");
            var state = DialogueManager.instance.currentConversationState;
            if (state != null && state.hasPCResponses) DialogueManager.instance.conversationView.SelectResponse(new SelectedResponseEventArgs(state.pcResponses[0]));
            else DialogueManager.instance.conversationView?.OnConversationContinueAll();
            yield return .2f;
        }
    }
    static IEnumerable<float> Launch(ExpeditionDepth depth)
    {
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun, depth); SceneFlowManager.Instance.LoadExpedition();
        foreach (var d in Scene("Expedition")) yield return d; Ready(); yield return 1;
    }
    static object Field(object o, string field) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(o);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, args);
    static void Shot(string label) { if (captured.Add(scenario + "-" + label)) Capture(scenario + "-" + label); }
    static void SampleIntro()
    {
        if (!observing || intro == null || camera == null || Time.time == lastSample) return;
        lastSample = Time.time;
        boss = intro.SpawnedBoss != null ? intro.SpawnedBoss.GetComponent<BossPatternController>() : null;
        bossHealth = boss != null ? boss.GetComponent<EnemyHealth>() : null;
        var phase = Field(intro, "currentPhase").ToString();
        var rig = GungeonStyleCamera2D.Instance;
        var owner = Field(rig, "gameplayFramingOwner");
        if (owner != null && !ReferenceEquals(previousOwner, owner)) cameraAcquisitions++;
        previousOwner = owner;
        var bar = BossHealthBarUI.Instance;
        bool visible = bar != null && bar.IsVisible;
        bool active = boss != null && boss.isActiveAndEnabled;
        bool title = (bool)Field(intro, "coreActivationTitlePresented");
        var body = Field(intro, "materializingBody") as SpriteRenderer;
        float alpha = body != null ? body.color.a : 1;
        float pixelHeight = boss != null ? ((SpriteRenderer)Field(boss, "sectorBody")).bounds.size.y * 270 / (2 * camera.orthographicSize) : 0;
        motion.Add($"{scenario},{Time.frameCount},{Time.time:F4},{phase},{camera.orthographicSize:F5},{camera.transform.position.x:F5},{camera.transform.position.y:F5},{alpha:F4},{pixelHeight:F2},{visible},{active},{title},{rig.IsCinematicFocusActive},{(owner == null ? "none" : owner.GetType().Name)}");
        if (phase == "CoreFocus" && Time.time - activatedAt > .6f) Shot("01-core-activation");
        if (body != null && alpha > .001f && alpha < 1)
        {
            if (materializeStart < 0) materializeStart = Time.time;
            if (visible) throw new Exception("HP visible during materialization");
            Shot("02-materialization-start");
            if (alpha >= .45f) Shot("03-materialization-midpoint");
        }
        if (phase == "BossReveal" && visible) throw new Exception("HP visible before camera handoff");
        if (phase == "PlayerHandoff")
        {
            if (!ReferenceEquals(owner, boss)) throw new Exception("Combat framing not prepared before handoff");
            Shot("04-final-intro-framing");
        }
        if (visible && !wasHudVisible)
        {
            hudAppearances++; firstHudTime = Time.time;
            Require(!rig.IsCinematicFocusActive && !title, "first HP frame follows camera/title release");
            Require(Mathf.Abs((float)Field(bar, "cachedCurrentHp") - bossHealth.CurrentHp) < .001f, "first HP frame uses current health " + bossHealth.CurrentHp);
            Require(Mathf.Abs((float)Field(bar, "cachedMaxHp") - bossHealth.MaxHp) < .001f, "first HP frame uses current max health");
            var value = (TextMeshProUGUI)Field(bar, "hpText");
            Require(value.text.Contains(bossHealth.CurrentHp.ToString("0")), "first visible HP text is accurate: " + value.text);
            Require(((RectTransform)Field(bar, "revealRoot")).localScale == Vector3.one, "compact bar never stretches");
            Require(!active, "first visible HP frame precedes combat ownership");
            Shot("05-hp-first-visible");
        }
        wasHudVisible = visible;
        if (active && firstCombatTime < 0)
        {
            firstCombatTime = Time.time;
            Require(ReferenceEquals(owner, boss), "existing boss camera owner ready before scheduler");
            Require(!title && !rig.IsCinematicFocusActive, "first Idle has no stale title or cinematic focus");
            Require(cameraAcquisitions == 1, "one Region A gameplay camera acquisition during intro");
            Shot("06-first-idle-handoff");
            Note($"First combat after activation={firstCombatTime - activatedAt:F3}s; materialization to combat={firstCombatTime - materializeStart:F3}s; HP first visible offset={firstHudTime - firstCombatTime:F4}s; first combat camera step={Vector3.Distance(lastCameraPosition,camera.transform.position):F5}; ortho step={Mathf.Abs(lastCameraSize-camera.orthographicSize):F5}");
        }
        if (active && boss.CurrentSectorStage != BossPatternController.SectorStage.Idle && boss.CurrentSectorStage != BossPatternController.SectorStage.Stopped)
            Shot("07-first-attack-start");
        lastCameraPosition = camera.transform.position; lastCameraSize = camera.orthographicSize;
    }
    static IEnumerable<float> Check()
    {
        motion.Add("scenario,frame,time,phase,ortho,cameraX,cameraY,bodyAlpha,bossPixelHeight,hudVisible,bossEnabled,title,cinematic,profileOwner");
        foreach (var d in Until(() => PermanentProgress.Instance != null && SceneFlowManager.Instance != null, "Boot owners")) yield return d;
        SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
        foreach (var d in Dialogue()) yield return d;
        foreach (string mode in new[] { "normal", "boss-death-intro", "player-death-intro", "abort-intro" })
        {
            scenario = mode; observing = false; boss = null; bossHealth = null; intro = null;
            wasHudVisible = false; previousOwner = null; cameraAcquisitions = 0; hudAppearances = 0;
            materializeStart = firstHudTime = firstCombatTime = lastSample = -1;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal); SceneFlowManager.Instance.LoadExpedition();
            foreach (var d in Scene("Expedition")) yield return d; Ready();
            var core = Object.FindFirstObjectByType<CoreObject>(); Require(core != null, "actual generated Region A Core: " + mode);
            RevealTrackedCore(); yield return 2;
            Move(core.transform.position + Vector3.down * .6f); yield return .2f;
            Require(core.CanInteract(player.gameObject), "normal Core interaction available");
            intro = core.GetComponent<CoreBossIntroSequence>(); activatedAt = Time.time; observing = true;
            core.Interact(player.gameObject);
            foreach (var d in Until(() => materializeStart > 0, "visible materialization")) yield return d;
            Require(Mathf.Abs(camera.orthographicSize / Object.FindFirstObjectByType<CameraZoomController2D>().BaseOrthographicSize - 1.15f) < .002f, "Region A materializes at 1.15x normal framing");
            if (mode == "normal")
            {
                foreach (var d in Until(() => firstCombatTime > 0 && captured.Contains(mode + "-07-first-attack-start"), "first combat/attack")) yield return d;
                Require(hudAppearances == 1, "boss HP appears exactly once");
                yield return 1; Shot("07b-first-attack-readable");
                observing = false;
                // Real death lifecycle, explicitly injected for presentation QA.
                Call(bossHealth, "Die");
            }
            else
            {
                observing = false;
                if (mode == "boss-death-intro") Call(bossHealth, "Die");
                else if (mode == "player-death-intro") Call(player, "Die");
                else RunManager.Instance.CompleteRun(RunEndReason.EmergencyReturn);
                yield return .1f;
                Require(intro == null || !intro.IsPlaying, mode + " cancels introduction");
                Require(boss == null || !boss.isActiveAndEnabled, mode + " never enables attack scheduler");
            }
            yield return .3f;
            Require(intro == null || !(bool)Field(intro, "cameraInputOffsetLockHeld"), mode + " releases intro input ownership immediately");
            Capture(mode + "-08a-death-presentation");
            // Death presentation legitimately acquires its own cinematic camera.
            foreach (var d in Until(() => GungeonStyleCamera2D.Instance == null ||
                (!GungeonStyleCamera2D.Instance.IsCinematicFocusActive && !GungeonStyleCamera2D.Instance.IsCinematicInputOffsetLocked), "death/abort camera presentation complete", 15)) yield return d;
            var rig = GungeonStyleCamera2D.Instance;
            if (rig != null)
            {
                Require(Field(rig, "gameplayFramingOwner") == null, mode + " releases gameplay camera");
                Require(!rig.IsCinematicFocusActive && !rig.IsCinematicInputOffsetLocked, mode + " releases intro camera/input");
                Require(!Object.FindFirstObjectByType<CameraZoomController2D>().IsCinematicZoomHeld, mode + " releases zoom hold");
            }
            Require(BossHealthBarUI.Instance == null || !BossHealthBarUI.Instance.IsVisible, mode + " leaves no stale HP");
            Require(intro == null || !(bool)Field(intro, "coreActivationTitlePresented"), mode + " clears intro title");
            Capture(mode + "-08-death-cleanup");
            yield return 8;
            SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
            foreach (var d in Dialogue()) yield return d;
        }
        Note("PASS actual generated Region A, native 480x270: normal intro/first attack and boss-death/player-death/abort interruption. Deaths are explicit QA lifecycle injections; player invulnerable for presentation capture; isolated QA save only.");
    }
    static void RevealTrackedCore()
    {
        var tracking = Object.FindFirstObjectByType<CoreTrackingSignalController>();
        if (tracking == null || !tracking.IsTrackingActive) return;
        foreach (var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
        {
            if (tracking.IsCoreRevealed) break;
            if (wreck.ObjectKind == HarvestObjectKind.HighValueWreck) wreck.TakeDamage(99999);
        }
        Require(tracking.IsCoreRevealed, "Core revealed via real wreck completion events");
    }
}
