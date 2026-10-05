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
public static class SectorPatternProbe
{
    const string SessionKey = "SectorPattern.Probe", Dir = "Logs/SectorPattern/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static BossPatternController boss;
    static EnemyHealth bossHealth;
    static bool autoFire;
    static string weaponLabel;
    static CoreBossIntroSequence intro;
    static float lastSample = -1;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing;
    static int lastShots, lastCycle;
    static float lastShotTime, lastPurpleAngle, lastGreenAngle;
    static bool wasPurple;
    static bool zoneDodgeStarted, zoneDodgeComplete, missileDodgeComplete;
    static float zoneDodgeHp, missileDodgeStart = -1;
    static Vector2 missileDodgePosition;
    static readonly Dictionary<int, Vector2> missileDirections = new Dictionary<int, Vector2>();
    static readonly Dictionary<int, float> missileTimes = new Dictionary<int, float>();
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static float nextInputDiagnostic;
    static SectorPatternProbe()
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
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 1800; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1); else SectorPatternValidation.RestoreScenes(); }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (finishing) return;
        try
        {
            SamplePattern();
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
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "combat-timing.csv", motion); }
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
    static void Shot(string label)
    {
        string name = weaponLabel + "-" + label;
        if (captured.Add(name)) Capture(name);
    }
    static void SamplePattern()
    {
        if (intro != null && intro.SpawnedBoss != null && boss == null)
        { boss = intro.SpawnedBoss.GetComponent<BossPatternController>(); bossHealth = boss.GetComponent<EnemyHealth>(); }
        if (boss == null || bossHealth == null || Time.time == lastSample) return;
        float dt = lastSample < 0 ? 0 : Time.time - lastSample; lastSample = Time.time;
        if (!boss.isActiveAndEnabled && intro != null && intro.IsPlaying)
        {
            var body = Field(intro, "materializingBody") as SpriteRenderer;
            if (body != null && body.color.a == 0)
            {
                if (boss.GetComponentsInChildren<SpriteRenderer>(true).Any(r => !r.forceRenderingOff)) throw new Exception("Pre-visible boss child before materialization");
                if (boss.SectorSpawnMarkerVisible && Object.FindFirstObjectByType<CameraZoomController2D>().CurrentZoomMultiplier < 1.6f)
                    Shot("01-spawn-marker");
            }
            if (body != null && body.color.a > .08f && boss.SectorPulseVisible) Shot("02-spawn-vfx");
            if (body != null && body.color.a > .7f) Shot("03-spawn-arrival");
            return;
        }
        if (!boss.isActiveAndEnabled) return;
        var stage = boss.CurrentSectorStage;
        bool truePhase2 = (bool)Field(boss, "phase2");
        var lanes = Object.FindObjectsByType<SectorPartitionLane>(FindObjectsSortMode.None);
        var zones = Object.FindObjectsByType<SectorDropZone>(FindObjectsSortMode.None);
        int damaging = lanes.Count(l => l.IsDamaging);
        if (lanes.Any(l => l.IsDamaging && !l.IsVisible) || zones.Any(z => z.IsDamaging && !z.IsVisible)) throw new Exception("Invisible hazard");
        if (damaging > (boss.SectorCounterActive ? 7 : 6)) throw new Exception("Too many active laser layers");
        if (boss.SectorActiveMissiles > 2) throw new Exception("Missile cap exceeded");
        if (new[] { "patternRoutine", "phase2ShieldCombatRoutine" }.Count(f => Field(boss, f) != null) > 1) throw new Exception("Duplicate attack scheduler");
        if (boss.SectorCounterActive && (!truePhase2 || boss.SectorActiveMissiles > 0 || boss.SectorZoneCount > 0 || boss.SectorShotsThisCycle > 0))
            throw new Exception("Purple exceeded its exclusive support window");
        if (boss.SectorZoneCount > 0 && (damaging > 0 || boss.SectorActiveMissiles > 0)) throw new Exception("Zones overlap peak rotation pressure");
        if (stage == BossPatternController.SectorStage.DropWarning)
        {
            if (zones.Length != 6 || zones.Any(z => z.IsDamaging)) throw new Exception("Six harmless warning zones required");
            Shot("07-red-warning-zones");
            if (zones.Any(z => z.GetComponentsInChildren<LineRenderer>().Any(l => l.name == "CoreProjection" && l.enabled && Vector3.Distance(l.GetPosition(0), l.GetPosition(1)) > .5f)))
                Shot("07b-core-zone-projection");
        }
        // Exercise real movement input during the first support cycles. The zone
        // escape also runs without QA invulnerability and checks HP at activation.
        if (boss.SectorCycle == 2 && stage == BossPatternController.SectorStage.MissileLaunch && !missileDodgeComplete)
        {
            if (missileDodgeStart < 0) { missileDodgeStart = Time.time; missileDodgePosition = player.transform.position; }
            if (Time.time - missileDodgeStart < .5f) Keys(Key.A);
            else
            {
                Keys(); missileDodgeComplete = true;
                Require(Vector2.Distance(player.transform.position, missileDodgePosition) > .5f, "real lateral input during bounded missile guidance");
                Shot("06b-guidance-after-lateral-dodge");
            }
        }
        if (boss.SectorCycle == 3 && stage == BossPatternController.SectorStage.DropWarning && !zoneDodgeComplete)
        {
            if (!zoneDodgeStarted) { zoneDodgeStarted = true; zoneDodgeHp = player.CurrentHp; player.SetDashInvincible(false); }
            float middle = (zones.Min(z => z.transform.position.y) + zones.Max(z => z.transform.position.y)) * .5f;
            float delta = middle - player.transform.position.y;
            if (Mathf.Abs(delta) > .12f) Keys(delta > 0 ? Key.W : Key.S); else Keys();
        }
        if (zoneDodgeStarted && !zoneDodgeComplete && stage == BossPatternController.SectorStage.DropActive)
        {
            Keys(); zoneDodgeComplete = true;
            Require(player.CurrentHp == zoneDodgeHp, "six-zone escape with real movement and invulnerability disabled: no HP loss");
            Require(zones.All(z => Vector2.Distance(z.transform.position, player.transform.position) > z.Radius), "player in visible gap at zone activation");
            player.SetDashInvincible(true); Shot("08b-zone-safe-route");
        }
        if (stage == BossPatternController.SectorStage.DropActive && zones.Any(z => z.IsDamaging)) Shot("08-red-zone-activation");
        if (stage == BossPatternController.SectorStage.MissileWarning) Shot("05-missile-telegraph");
        if (boss.SectorActiveMissiles > 0)
        {
            if (boss.SectorShotsThisCycle != 0 || damaging < 4) throw new Exception("Missile rotation window contains old charge/suppression fire");
            if (boss.SectorActiveMissiles == 2) Shot("06-two-guided-missiles");
            foreach (var missile in (Bullet[])Field(boss, "sectorMissiles"))
            {
                if (missile == null || !missile.gameObject.activeInHierarchy) continue;
                if (missile.Speed != BossPatternController.SectorMissileSpeed) throw new Exception("Missile speed drift");
                int id = missile.GetInstanceID();
                if (missileDirections.TryGetValue(id, out var previous) && missileTimes.TryGetValue(id, out float time) && dt > 0 && Time.time - time < .1f)
                {
                    // New pool leases can start a fresh heading; only measure while guidance is active.
                    if ((bool)Field(missile, "useTimedHoming") && Vector2.Angle(previous, missile.MoveDirection) > BossPatternController.SectorMissileTurnRate * dt + .1f)
                        throw new Exception("Missile turn cap exceeded");
                }
                missileDirections[id] = missile.MoveDirection; missileTimes[id] = Time.time;
            }
        }
        if (stage == BossPatternController.SectorStage.CounterWarning) Shot("10-purple-warning");
        if (boss.SectorCounterActive)
        {
            if (wasPurple && (boss.SectorCounterAngle > lastPurpleAngle + .01f || boss.SectorAngle < lastGreenAngle - .01f)) throw new Exception("Rotation directions not opposed");
            Shot("11-purple-counter-rotation");
        }
        wasPurple = boss.SectorCounterActive; lastPurpleAngle = boss.SectorCounterAngle; lastGreenAngle = boss.SectorAngle;
        if (boss.SectorSpokeCount == 6 && damaging >= 6) Shot("12-final-six-part-pattern");
        if (stage == BossPatternController.SectorStage.EscalationWarning)
        {
            if (damaging != 0) throw new Exception("Damage during six-spoke growth");
            float progress = (float)Field(boss, "sectorWarningProgress");
            if (progress > .06f && boss.SectorPulseVisible) Shot("09-green-pulse-fade");
            if (progress > .6f) Shot("09b-six-link-growth");
        }
        if (lastCycle != boss.SectorCycle) { lastCycle = boss.SectorCycle; lastShots = 0; }
        if (stage == BossPatternController.SectorStage.Suppression && boss.SectorShotsThisCycle > lastShots)
        {
            if (boss.SectorShotsThisCycle - lastShots != 2) throw new Exception("Suppression pair not dispatched together");
            float interval = Time.time - lastShotTime;
            if (lastShots == 8 && interval < .32f - .0001f) throw new Exception("Inter-burst breathing gap shortened");
            Note($"PAIR {weaponLabel} cycle={boss.SectorCycle} shots={boss.SectorShotsThisCycle} interval={interval:F4}");
            lastShotTime = Time.time; lastShots = boss.SectorShotsThisCycle;
            if (lastShots >= 4) Shot("04-paired-suppression");
        }
        motion.Add($"{weaponLabel},{Time.frameCount},{Time.time:F4},{boss.SectorCycle},{stage},{truePhase2},{boss.SectorSpokeCount},{damaging},{boss.SectorActiveMissiles},{boss.SectorZoneCount},{zones.Count(z=>z.IsDamaging)},{boss.SectorCounterActive},{boss.SectorAngle:F4},{boss.SectorCounterAngle:F4},{boss.SectorShotsThisCycle},{bossHealth.CurrentHp:F2}");
        if (autoFire && player != null)
        {
            Vector2 aim = camera.WorldToScreenPoint(boss.transform.position);
            var tree = player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            bool fire = tree == WeaponTreeType.MachineGun || (tree == WeaponTreeType.Sniper ? Time.time % 1.2f < .85f : Time.time % .65f < .12f);
            qaMouse.MakeCurrent(); InputSystem.QueueStateEvent(qaMouse, new MouseState { position = aim }.WithButton(MouseButton.Left, fire));
        }
    }
    static IEnumerable<float> Check()
    {
        motion.Add("weapon,frame,time,cycle,stage,truePhase2,greenSpokes,damagingLanes,missiles,zones,damagingZones,purpleActive,greenAngle,purpleAngle,shots,hp");
        foreach (var d in Until(() => PermanentProgress.Instance != null && SceneFlowManager.Instance != null, "Boot owners")) yield return d;
        SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
        foreach (var d in Dialogue()) yield return d;
        foreach (var tree in new[] { WeaponTreeType.MachineGun, WeaponTreeType.Shotgun, WeaponTreeType.Sniper })
        {
            weaponLabel = tree.ToString(); autoFire = false; lastCycle = -1; lastSample = -1; wasPurple = false;
            zoneDodgeStarted = zoneDodgeComplete = missileDodgeComplete = false; missileDodgeStart = -1;
            boss = null; bossHealth = null; intro = null; missileDirections.Clear(); missileTimes.Clear();
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            RunManager.Instance.StartNewRun(tree, ExpeditionDepth.Normal); SceneFlowManager.Instance.LoadExpedition();
            foreach (var d in Scene("Expedition")) yield return d; Ready();
            var core = Object.FindFirstObjectByType<CoreObject>(); Require(core != null, "actual generated Region A Core");
            RevealTrackedCore(); yield return 2;
            Move(core.transform.position + Vector3.down * .6f); yield return .2f;
            Require(core.CanInteract(player.gameObject), "normal Core activation available");
            intro = core.GetComponent<CoreBossIntroSequence>(); core.Interact(player.gameObject);
            foreach (var d in Until(() => boss != null && boss.isActiveAndEnabled && boss.SectorCycle > 0, "live scheduler after spawn")) yield return d;
            Require(player.GetComponent<PlayerWeaponController>().CurrentWeaponTree == tree, "actual equipped " + tree);
            Require(boss.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "no missing boss scripts");
            foreach (string field in new[] { "sectorMissilePrefab", "sectorDropZonePrefab", "sectorCounterLanePrefab", "sectorSpawnMarkerPrefab", "sectorPulsePrefab", "sectorLeftMuzzle", "sectorRightMuzzle", "sectorChargeMuzzle" })
                Require((Object)Field(boss, field) != null, "saved binding " + field);
            foreach (var d in Until(() => boss.SectorCycle >= 4, "suppression, guided missile and six-zone cycles")) yield return d;
            Require(zoneDodgeComplete && missileDodgeComplete, "support movement observations completed");
            Move(boss.transform.position + Vector3.down * 3.2f); yield return .2f; autoFire = true;
            foreach (var d in Until(() => boss.SectorEscalated, "50 percent shield escalation through weapon hits", 180)) yield return d;
            autoFire = false; Mouse(false);
            foreach (var d in Until(() => captured.Contains(weaponLabel + "-12-final-six-part-pattern") && boss.CurrentSectorStage == BossPatternController.SectorStage.Recovery, "safe growth and six-spoke rotation")) yield return d;
            autoFire = true;
            foreach (var d in Until(() => (bool)Field(boss, "phase2"), "true Phase 2 after weapon shield break", 180)) yield return d;
            autoFire = false; Mouse(false);
            foreach (var d in Until(() => captured.Contains(weaponLabel + "-11-purple-counter-rotation"), "controlled purple counter-rotation", 100)) yield return d;
            yield return .35f; Shot("13-player-readability"); autoFire = true;
            Transform encounterSource = boss.transform;
            foreach (var d in Until(() => boss == null || bossHealth.IsDead, "actual weapon kill", 210)) yield return d;
            autoFire = false; Mouse(false); Keys(); yield return 2;
            Require(!Object.FindObjectsByType<SectorPartitionLane>(FindObjectsSortMode.None).Any(l => l.IsVisible || l.IsDamaging), "laser cleanup");
            Require(!Object.FindObjectsByType<SectorDropZone>(FindObjectsSortMode.None).Any(z => z.IsVisible || z.IsDamaging), "zone cleanup");
            Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b => b.SourceRoot == encounterSource), "missile/projectile cleanup");
            Require(Field(GungeonStyleCamera2D.Instance, "gameplayFramingOwner") == null, "camera profile released after death");
            Shot("14-death-cleanup"); yield return 6;
            Note("Completed " + tree + " generated Region A: real weapon kill, isolated QA save, invulnerable presentation observation.");
            boss = null; bossHealth = null; intro = null;
            SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
            foreach (var d in Dialogue()) yield return d;
        }
        Note("PASS all three actual generated Region A encounters at native 480x270.");
    }
    static void RevealTrackedCore()
    {
        var tracking = Object.FindFirstObjectByType<CoreTrackingSignalController>();
        if (tracking == null || !tracking.IsTrackingActive) return;
        foreach (var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
        { if (tracking.IsCoreRevealed) break; if (wreck.ObjectKind == HarvestObjectKind.HighValueWreck) wreck.TakeDamage(99999); }
        Require(tracking.IsCoreRevealed, "Core revealed through real tracking completion events");
    }
}
