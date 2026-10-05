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
public static class SectorRefineProbe
{
    const string SessionKey = "SectorRefine.Probe", Dir = "Logs/SectorRefine/";
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
    static float lastSample = -1;
    static BossPatternController.SectorStage lastStage;
    static int lastCycle;
    static float stageStart;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing;
    static int observedShotCycle = -1, observedShotCount;
    static float observedShotTime;
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static float nextInputDiagnostic;
    static SectorRefineProbe()
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
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 1600; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1); else SectorRefineValidation.RestoreScenes(); }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (finishing) return;
        try
        {
            SampleCombat();
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
    static void SampleCombat()
    {
        if (boss == null || bossHealth == null || !boss.isActiveAndEnabled) return;
        var stage = boss.CurrentSectorStage;
        if (Time.time != lastSample)
        {
            lastSample = Time.time;
            var lanes = Object.FindObjectsByType<SectorPartitionLane>(FindObjectsSortMode.None);
            int active = lanes.Count(l => l.IsDamaging), visible = lanes.Count(l => l.IsVisible);
            int projectiles = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b => b.SourceRoot == boss.transform);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            int attackOwners = new[] { "patternRoutine", "phase2ShieldCombatRoutine" }
                .Count(f => typeof(BossPatternController).GetField(f, flags).GetValue(boss) != null);
            if (attackOwners > 1 || active > 6) throw new Exception("Competing attack sequences");
            if (lanes.Any(l => l.IsDamaging && !l.IsVisible)) throw new Exception("Invisible damaging lane");
            if ((stage == BossPatternController.SectorStage.Warning || stage == BossPatternController.SectorStage.EscalationWarning || stage == BossPatternController.SectorStage.EscalationHold) && active > 0) throw new Exception("Early lane collider");
            if ((stage == BossPatternController.SectorStage.Recovery || stage == BossPatternController.SectorStage.Stopped) && active > 0)
                throw new Exception("Lane survived clear/recovery");
            motion.Add(weaponLabel + "," + Time.time.ToString("F4") + "," + boss.SectorCycle + "," + stage + "," +
                boss.SectorEscalated + "," + active + "," + visible + "," + boss.SectorShotsThisCycle + "," + bossHealth.HpRatio.ToString("F3") + "," + projectiles + "," + boss.SectorAngle.ToString("F4") + "," + boss.SectorSpokeCount + "," + typeof(BossPatternController).GetField("sectorMorph", flags).GetValue(boss));
            if (active > 0)
            {
                foreach (var lane in lanes.Where(l => l.IsDamaging))
                {
                    var box = lane.GetComponent<BoxCollider2D>();
                    var sprite = lane.GetComponentInChildren<SpriteRenderer>();
                    if (Mathf.Abs(box.size.x - sprite.size.x) > .0001f) throw new Exception("visible/collision length drift");
                    float expected = boss.SectorBeamLength(lane.transform.position, lane.transform.right);
                    if (Mathf.Abs(box.size.x - expected) > .02f) throw new Exception("active beam no longer reaches live containment");
                }
                foreach (var enemy in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None))
                {
                    bool ambient = (bool)typeof(EnemyBaseAI).GetMethod("IsAmbientEnemyForBossEncounterIsolation", flags).Invoke(enemy, null);
                    if (ambient && (!enemy.IsBossEncounterIsolated || enemy.GetComponentsInChildren<Renderer>(true).Any(r => !r.forceRenderingOff)))
                        throw new Exception("ordinary enemy visible/active during sector control: " + enemy.name);
                }
                foreach (var meteor in Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None))
                    if (!meteor.IsSectorEncounterPaused) throw new Exception("drifting meteor resumed during fight");
            }
            // Observe dispatches rather than object identities: a pooled bullet can be
            // reused by the opposite muzzle in the same control cycle.
            if (observedShotCycle != boss.SectorCycle)
            {
                observedShotCycle = boss.SectorCycle; observedShotCount = 0;
            }
            if (boss.SectorShotsThisCycle > observedShotCount)
            {
                string field = stage == BossPatternController.SectorStage.PrecisionFire ? "sectorChargeMuzzle" : boss.SectorShotsThisCycle <= 3 ? "sectorLeftMuzzle" : "sectorRightMuzzle";
                var muzzle = (Transform)Field(boss, field);
                bool matches = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b =>
                    b.SourceRoot == boss.transform && Vector2.Distance((Vector2)Field(b, "spawnPosition"), muzzle.position) < .06f);
                if (!matches) throw new Exception("Projectile/muzzle mismatch: " + field);
                if (Mathf.Abs(Mathf.DeltaAngle(boss.transform.eulerAngles.z, 0)) > .001f)
                    throw new Exception("Physics root rotated with weapon facing: angle=" + boss.transform.eulerAngles.z + "; angularVelocity=" + boss.GetComponent<Rigidbody2D>().angularVelocity);
                float interval = Time.time - observedShotTime;
                if (stage == BossPatternController.SectorStage.Suppression && observedShotCount == 3 && interval < .28f - .0001f)
                    throw new Exception("Inter-burst gap below .28s: " + interval);
                Note("Verified projectile origin: " + field + "; cycle=" + boss.SectorCycle + "; shot=" + boss.SectorShotsThisCycle + "; interval=" + interval.ToString("F4"));
                observedShotCount = boss.SectorShotsThisCycle; observedShotTime = Time.time;
            }
            if (stage == BossPatternController.SectorStage.EscalationWarning)
            {
                float progress = (float)Field(boss, "sectorWarningProgress");
                foreach (float threshold in new[] { .1f, .45f, .7f, .95f })
                    if (progress >= threshold && captured.Add(weaponLabel + "-transition-" + threshold))
                    {
                        Capture(weaponLabel + "-transition-" + threshold);
                        CaptureArena(weaponLabel + "-transition-arena-" + threshold);
                    }
            }
            if (stage == BossPatternController.SectorStage.PrecisionWarning && stage == lastStage && Time.time - stageStart > .65f && captured.Add(weaponLabel + "-center-charge-bright")) Capture(weaponLabel + "-center-charge-bright");
            if (stage == BossPatternController.SectorStage.Suppression && boss.SectorShotsThisCycle > 0 && captured.Add(weaponLabel + "-side-shot-" + boss.SectorEscalated + "-" + boss.SectorShotsThisCycle)) Capture(weaponLabel + "-side-shot-" + boss.SectorEscalated + "-" + boss.SectorShotsThisCycle);
            string pressureLabel = weaponLabel + "-" + ((bool)typeof(BossPatternController).GetField("phase2", flags).GetValue(boss) ? "phase2" : boss.SectorEscalated ? "shield" : "phase1");
            if (stage == BossPatternController.SectorStage.Suppression && boss.SectorShotsThisCycle >= 3 && captured.Add(pressureLabel + "-burst-live"))
            {
                Capture(pressureLabel + "-burst-live");
                if (boss.SectorEscalated) CaptureArena(pressureLabel + "-combined-arena");
            }
            if (stage != lastStage || boss.SectorCycle != lastCycle)
            {
                stageStart = Time.time;
                lastStage = stage; lastCycle = boss.SectorCycle;
                bool truePhase2 = (bool)typeof(BossPatternController).GetField("phase2", flags).GetValue(boss);
                string label = weaponLabel + "-" + (truePhase2 ? "phase2-" : boss.SectorEscalated ? "shield-" : "phase1-") + stage;
                if (stage != BossPatternController.SectorStage.Stopped && captured.Add(label)) Capture(label);
            }
            if (stage == BossPatternController.SectorStage.Warning && Time.time - stageStart > .65f && captured.Add(weaponLabel + "-warning-readable"))
                Capture(weaponLabel + "-warning-readable");
            if (stage == BossPatternController.SectorStage.Partition && Time.time - stageStart > .4f && captured.Add(weaponLabel + "-diagonal-" + boss.SectorEscalated)) CaptureArena(weaponLabel + "-diagonal-" + boss.SectorEscalated);
            if (stage == BossPatternController.SectorStage.EscalationWarning && Time.time - stageStart > .8f && captured.Add(weaponLabel + "-six-warning")) CaptureArena(weaponLabel + "-six-warning");
            if (typeof(BossPatternController).GetField("phase2ShieldBreakRoutine", flags).GetValue(boss) != null && captured.Add(weaponLabel + "-shield-break-exposed"))
                Capture(weaponLabel + "-shield-break-exposed");
        }
        if (autoFire && player != null)
        {
            Vector2 aim = camera.WorldToScreenPoint(boss.transform.position);
            var tree = player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            bool fire = tree == WeaponTreeType.MachineGun || (tree == WeaponTreeType.Sniper ? Time.time % 1.2f < .85f : Time.time % .65f < .12f);
            qaMouse.MakeCurrent();
            InputSystem.QueueStateEvent(qaMouse, new MouseState { position = aim }.WithButton(MouseButton.Left, fire));
            if (Time.time >= nextInputDiagnostic)
            {
                nextInputDiagnostic = Time.time + 5;
                var wc = player.GetComponent<PlayerWeaponController>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var action = (InputAction)typeof(PlayerWeaponController).GetField("fireAction", flags).GetValue(wc);
                Note("INPUT " + weaponLabel + " hp=" + bossHealth.CurrentHp + " paused=" + GameplayPauseManager.IsPaused +
                    " wc=" + wc.isActiveAndEnabled + " locks=" + wc.ExternalInputLocked + " mouse=" + qaMouse.enabled + "/" + qaMouse.leftButton.isPressed +
                    " action=" + action?.enabled + "/" + action?.IsPressed() + " canFire=" + typeof(PlayerWeaponController).GetMethod("CanUseWeapon", flags).Invoke(wc, null));
            }
        }
    }
    static IEnumerable<float> Check()
    {
        motion.Add("weapon,time,cycle,stage,escalated,damagingLanes,visibleLanes,shots,hpRatio,activeProjectiles,angle,spokes,morph");
        foreach (var d in Until(() => PermanentProgress.Instance != null && SceneFlowManager.Instance != null, "Boot owners")) yield return d;
        SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
        foreach (var d in Dialogue()) yield return d;
        foreach (var tree in new[] { WeaponTreeType.MachineGun, WeaponTreeType.Shotgun, WeaponTreeType.Sniper })
        {
            weaponLabel = tree.ToString(); autoFire = false;
            observedShotCycle = -1; observedShotCount = 0; lastSample = -1; lastCycle = -1;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            Require(!PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator), "isolated first-clear QA state");
            RunManager.Instance.StartNewRun(tree, ExpeditionDepth.Normal); SceneFlowManager.Instance.LoadExpedition();
            foreach (var d in Scene("Expedition")) yield return d; Ready();
            var core = Object.FindFirstObjectByType<CoreObject>(); Require(core != null, "generated Region A Core");
            RevealTrackedCore(); yield return 2;
            player.GetComponent<PlayerArmor>().SetMaxArmor(8, true);
            Capture(weaponLabel + "-hp-armor");
            // Place existing ordinary actors in the actual encounter footprint to exercise start cleanup.
            var ambient = Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None).FirstOrDefault(e => e.GetComponent<BossPatternController>() == null);
            if (ambient != null) ambient.transform.position = core.transform.position + Vector3.right * 3;
            var meteor = Object.FindFirstObjectByType<MeteorObstacle>();
            if (meteor != null) meteor.transform.position = core.transform.position + Vector3.left * 3;
            Physics2D.SyncTransforms();
            Move(core.transform.position + Vector3.down * .6f); yield return .2f;
            // Real authored interaction, activation hold and intro (no direct boss spawn).
            Require(core.CanInteract(player.gameObject), "Core accepts normal activation after tracking signals");
            core.Interact(player.gameObject);
            foreach (var d in Until(() => Object.FindFirstObjectByType<BossPatternController>() != null, "Sector introduction")) yield return d;
            boss = Object.FindFirstObjectByType<BossPatternController>(); bossHealth = boss.GetComponent<EnemyHealth>();
            var intro = core.GetComponent<CoreBossIntroSequence>();
            foreach (var d in Until(() => Field(intro, "materializingBody") is SpriteRenderer body && body.color.a > .25f && body.color.a < .95f,
                "visible boss materialization")) yield return d;
            Capture(weaponLabel + "-introduction");
            Require(boss.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "no missing boss scripts");
            foreach (string field in new[] { "sectorLeftMuzzle", "sectorRightMuzzle", "sectorChargeMuzzle", "sectorChargeCannon", "sectorSideFlashPrefab", "sectorChargeFlashPrefab" })
                Require((Object)Field(boss, field) != null, "saved binding " + field);
            foreach (var d in Until(() => boss.isActiveAndEnabled && boss.SectorCycle > 0, "live boss scheduler")) yield return d;
            CaptureArena(weaponLabel + "-arena-clean");
            Require(player.GetComponent<PlayerWeaponController>().CurrentWeaponTree == tree, "actual equipped " + tree);
            Require(Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None).Where(e => e.GetComponent<BossPatternController>() == null).All(e => e.IsBossEncounterIsolated), "all ordinary enemies isolated at battle start");
            Require(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).All(m => m.IsSectorEncounterPaused), "all meteor drift paused during encounter");
            var lanePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Lane);
            var reused = PoolManager.Instance.Get(lanePrefab, player.transform.position + Vector3.right * 30, Quaternion.identity).GetComponent<SectorPartitionLane>();
            reused.Configure(Vector2.one * 1000, Vector2.one * 1000 + Vector2.right, .45f, 0, .75f); reused.Activate(0);
            PoolManager.Instance.Release(reused.gameObject);
            Require(!reused.IsDamaging && !reused.IsVisible, "real pooled OnDisable clears collider and visual");
            // Observe two complete patterns before firing; move with actual input.
            Keys(Key.D); yield return .55f; Keys();
            foreach (var d in Until(() => boss.SectorCycle >= 3, "two full control cycles")) yield return d;
            Move(boss.transform.position + Vector3.down * 3.2f); yield return .2f;
            autoFire = true;
            yield return .32f; Capture(weaponLabel + "-sustained-fire");
            foreach (var d in Until(() => boss.SectorEscalated, "50 percent shield gate via weapon hits", 150)) yield return d;
            // Pause fire long enough to observe the real shield combat combination.
            autoFire = false; Mouse(false);
            foreach (var d in Until(() => boss.CurrentSectorStage == BossPatternController.SectorStage.Recovery, "shield sequence recovery")) yield return d;
            autoFire = true;
            foreach (var d in Until(() => boss == null || bossHealth.IsDead, "weapon kill", 210)) yield return d;
            autoFire = false; Mouse(false); Keys(); yield return .15f;
            Require(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).All(m => !m.IsSectorEncounterPaused), "meteor physics restored on encounter teardown");
            Require(!Object.FindObjectsByType<SectorPartitionLane>(FindObjectsSortMode.None).Any(l => l.IsDamaging || l.IsVisible), "all attack lanes clear on death");
            var cameraOwner = typeof(GungeonStyleCamera2D).GetField("gameplayFramingOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GungeonStyleCamera2D.Instance);
            Require(!ReferenceEquals(cameraOwner, boss), "Region A releases its camera profile on death");
            Require(PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator), "existing campaign owner records Region A defeat");
            Capture(weaponLabel + "-death"); yield return 8;
            Capture(weaponLabel + "-reward");
            Note("Completed " + tree + " Core activation -> intro -> phase 1 -> shield gate -> shield break -> phase 2 -> death/reward (invulnerable QA player, actual weapon inputs).");
            boss = null; bossHealth = null;
            SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
            foreach (var d in Dialogue()) yield return d;
        }
        Note("Completed actual Region A fights at 480x270; no user save or production scenes saved.");
    }
    static void CaptureArena(string name)
    {
        var position = camera.transform.position; float size = camera.orthographicSize;
        camera.transform.position = new Vector3(boss.SectorArenaCenter.x, boss.SectorArenaCenter.y, position.z);
        camera.orthographicSize = boss.SectorArenaHalfExtents.y + 2;
        Capture(name); camera.transform.position = position; camera.orthographicSize = size;
        Note("Arena overview capture uses QA framing only; regular captures use gameplay camera.");
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
        Require(tracking.IsCoreRevealed, "Core revealed through real wreck completion signal events (QA instant damage)");
    }
}
