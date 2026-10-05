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
public static class CombatReadabilityProbe
{
    const string SessionKey = "CombatReadability.Probe", Dir = "Logs/CombatReadability/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static EnemyBaseAI charger;
    static bool finishing;
    static CombatReadabilityProbe()
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
        File.Copy("Logs/FinalVisualQA/after-save.json", Dir + "probe-save.json", true);
        File.Copy("Logs/FinalVisualQA/after-save.json", Dir + "probe-save.json.bak", true);
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
        { Application.runInBackground = true; sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 1000; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); EditorApplication.Exit(errors.Count == 0 ? 0 : 1); }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (charger != null)
        {
            var attack = charger.GetComponent<EnemyAttackController>(); var rb = charger.GetComponent<Rigidbody2D>();
            motion.Add(Time.time.ToString("F3") + "," + charger.CurrentState + "," + attack.IsCharging + "," + attack.IsAimDirectionLocked + "," +
                rb.position + "," + rb.linearVelocity + "," + charger.FacingDirection + "," + attack.CurrentChargeDirection);
        }
        if (finishing || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Probe timeout");
            if (sequence.MoveNext()) next = EditorApplication.timeSinceStartup + sequence.Current;
            else Finish();
        }
        catch (Exception e) { errors.Add(e.ToString()); Finish(); }
    }
    static void Finish() { finishing = true; EditorApplication.update -= Tick; Flush(); EditorApplication.ExitPlaymode(); }
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "charging-motion.csv", motion); }
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
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
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
            .Where(r => r.enabled && (r.name.Contains("Core") || r.name.Contains("core") || AssetDatabase.GetAssetPath(r.sprite).Contains("CombatReadability")))
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
    static IEnumerable<float> Check()
    {
        Note("Controlled Play Mode scenes; isolated completed QA save, teleports, invulnerability and direct development state entry. No saved-scene changes.");
        yield return 4; SceneFlowManager.Instance.LoadSettlement(); foreach (var d in Scene("Settlement")) yield return d;
        foreach (var d in Dialogue()) yield return d; Capture("04-hangar-no-duplicate");
        SceneFlowManager.Instance.LoadTutorial(); foreach (var d in Scene("Tutorial")) yield return d; Ready();
        foreach (var d in Dialogue()) yield return d;
        var tutorial = Object.FindFirstObjectByType<TutorialFlowController>(); tutorial.SetStep(TutorialStep.TravelPurpleCore);
        foreach (var d in Dialogue()) yield return d;
        Move(tutorial.AlienSignal.transform.position + Vector3.left * 2); yield return 2; Capture("05-tutorial-purple-core");
        foreach (var d in Launch(ExpeditionDepth.Normal)) yield return d;
        Note("Camera components: " + string.Join(",", camera.GetComponents<Component>().Select(c => c.GetType().Name)) + "; actual target=480x270");
        var core = Object.FindFirstObjectByType<CoreObject>(); Require(core != null, "generated Region A core");
        RevealTrackedCore(); yield return 2;
        core.MarkCoreLocationDiscovered(false);
        Move(core.transform.position + Vector3.left * 2); yield return 2; Capture("06-region-a-green-core");
        foreach (var ai in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None)) ai.gameObject.SetActive(false);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset");
        var enemy = Object.Instantiate(definition.EnemyPrefab, player.transform.position + Vector3.right * 3, Quaternion.identity);
        charger = enemy.GetComponent<EnemyBaseAI>(); charger.ApplyDefinition(definition); charger.GetComponent<EnemyVisionSensor>().ForceDetectTarget(player.transform);
        var attack = charger.GetComponent<EnemyAttackController>();
        float started = 0, released = 0; int starts = 0, releases = 0;
        attack.ChargeStarted += _ => { started = Time.time; starts++; };
        attack.ChargeReleased += _ => { released = Time.time; releases++; Note("Charge released after " + (released - started).ToString("F3") + " seconds"); };
        foreach (var d in Until(() => attack.IsCharging, "stationary charge")) yield return d;
        yield return .4f; Capture("01-charger-stationary");
        foreach (var d in Until(() => releases > 0, "first release")) yield return d;
        Require(released - started >= 1.45f && released - started <= 1.65f, "authored 1.5-second charge preserved");
        yield return .4f; Capture("03-charger-reposition-stable");
        Move(enemy.transform.position + Vector3.left * 3); charger.GetComponent<EnemyVisionSensor>().ForceDetectTarget(player.transform);
        foreach (var d in Until(() => attack.IsCharging && CombatReadabilityAuthoring.Get<bool>(attack, "usePredictiveAimForCurrentAttack"), "predictive charge selected by authored chance", 30)) yield return d;
        Keys(Key.W);
        yield return .5f; Capture("02-charger-lateral");
        Note("Lateral target velocity=" + player.GetComponent<Rigidbody2D>().linearVelocity + "; predicted direction=" + attack.CurrentChargeDirection + "; direct=" + ((Vector2)(player.transform.position - enemy.transform.position)).normalized);
        Keys(Key.S);
        foreach (var d in Until(() => attack.IsAimDirectionLocked, "charge commitment", 3)) yield return d;
        Keys(Key.D);
        Vector2 locked = attack.CurrentChargeDirection; yield return .16f;
        Require(Vector2.Distance(locked, attack.CurrentChargeDirection) < .001f, "committed shot does not bend");
        Capture("03b-charge-locked"); Keys(); enemy.SetActive(false); charger = null;
        foreach (var bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None)) bullet.gameObject.SetActive(false);
        var dash = player.GetComponent<PlayerDash>();
        foreach (var d in Until(() => dash.CanDash, "normal dash ready", 10)) yield return d;
        Require(dash.TryDash(), "normal dash accepted");
        foreach (var d in Until(() => EffectVisible("Dash"), "normal approved dash visible", 2)) yield return d;
        Capture("09-normal-dash"); yield return 1.5f;
        var visuals = player.GetComponent<PlayerVisualStateController>(); CombatReadabilityAuthoring.Set(visuals, "isCursed", true);
        foreach (var d in Until(() => dash.CanDash, "Curse dash ready", 10)) yield return d;
        Require(dash.TryDash(), "Curse dash accepted");
        foreach (var d in Until(() => EffectVisible("CurseDash"), "approved Curse dash visible", 2)) yield return d;
        Capture("10-curse-dash"); yield return .5f;
        CombatReadabilityAuthoring.Set(visuals, "isCursed", false);
        var weapons = player.GetComponent<PlayerWeaponController>();
        foreach (var weapon in new[] { WeaponTreeType.MachineGun, WeaponTreeType.Shotgun, WeaponTreeType.Sniper })
        {
            weapons.EquipWeapon(weapon); Mouse(true); yield return weapon == WeaponTreeType.Sniper ? 1.1f : .02f;
            if (weapon == WeaponTreeType.Sniper) Mouse(false);
            foreach (var d in Until(() => EffectVisible(weapon + "Muzzle"), weapon + " authored muzzle visible", 2)) yield return d;
            Capture("11-muzzle-" + weapon); Mouse(false); yield return 1;
        }
        CombatFeedbackManager.PlayHit(player.transform.position + Vector3.right, Vector2.left, CombatFeedbackKind.Shield);
        yield return .04f; Capture("11b-shield-hit"); yield return .5f;
        Require(!Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Any(a => a.gameObject.name.Contains("ShieldHit") || a.gameObject.name.Contains("CurseDash")), "pooled effects cleaned after lifetime");
        foreach (string role in new[] { "MachineGunMuzzle", "ShotgunMuzzle", "SniperMuzzle", "Dash", "CurseDash", "GenericHit", "ShieldHit", "PickupSparkle" })
        {
            var prefab = Resources.Load<GameObject>("VFX/Approved/" + role);
            var effect = PoolManager.Instance.SpawnAutoRelease(prefab, player.transform.position + Vector3.right, .18f);
            Require(effect != null, role + " pool spawn"); yield return .03f;
            Require(effect.GetComponent<SpriteRenderer>().sprite != null, role + " animated sprite");
            yield return .3f; Require(!effect.activeSelf, role + " returned to pool");
            var again = PoolManager.Instance.Get(prefab, player.transform.position, Quaternion.identity);
            Require(again == effect, role + " instance reused"); PoolManager.Instance.Release(again);
        }
        var pulsePrefab = Resources.Load<GameObject>("VFX/Approved/CorePulse_green");
        var pausedPulse = PoolManager.Instance.Get(pulsePrefab, player.transform.position, Quaternion.identity);
        float previousTimeScale = Time.timeScale; Time.timeScale = 0;
        PoolManager.Instance.ReleaseAfter(pausedPulse, .15f, true); yield return .3f;
        Time.timeScale = previousTimeScale;
        Require(!pausedPulse.activeSelf, "Core pulse releases on unscaled presentation clock while gameplay time is paused");
        player.SetDashInvincible(true);
        weapons.EquipWeapon(WeaponTreeType.MachineGun);
        Move(core.transform.position + Vector3.down * 2);
        Require(core.TryStartBossEncounterForDevelopment(out string reason), "Raider encounter development entry: " + reason);
        foreach (var d in Until(() => GameStateManager.Instance.CurrentState == GameState.BossBattle, "Raider combat")) yield return d;
        yield return 7;
        var carriers = Object.FindObjectsByType<RaiderBarricadeCarrier>(FindObjectsSortMode.None);
        Require(carriers.Length == 4, "four actual Raider containment emitters");
        var commander = Object.FindFirstObjectByType<PirateCommanderBossController>();
        if (commander != null) { Move(commander.transform.position + Vector3.down * 2); yield return 1; }
        Capture("14-boss-hp-active");
        Vector3 center = Vector3.zero; foreach (var c in carriers) center += c.transform.position; center /= 4;
        float halfX = carriers.Max(c => Mathf.Abs(c.transform.position.x - center.x)), halfY = carriers.Max(c => Mathf.Abs(c.transform.position.y - center.y));
        var arena = new Bounds(center, new Vector3(halfX * 2 - .2f, halfY * 2 - .2f, 10));
        Require(!Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).Any(m => arena.Contains(m.transform.position)), "reserved boss area contains no legacy meteors");
        Vector3 oldPosition = camera.transform.position; float oldSize = camera.orthographicSize;
        camera.transform.position = new Vector3(center.x, center.y, oldPosition.z); camera.orthographicSize = halfY + 2;
        Capture("12-raider-full-arena"); camera.transform.position = oldPosition; camera.orthographicSize = oldSize;
        foreach (var c in carriers)
        {
            Note(c.name + " rootZ=" + c.transform.eulerAngles.z + " visualZ=" + c.transform.Find("VisualRoot").localEulerAngles.z);
            Move(c.transform.position + (center - c.transform.position).normalized * 2); yield return .6f; Capture("13-" + c.name);
        }
        foreach (var depth in new[] { ExpeditionDepth.DeepZone1, ExpeditionDepth.DeepZone2 })
        {
            foreach (var d in Launch(depth)) yield return d;
            core = Object.FindFirstObjectByType<CoreObject>();
            RevealTrackedCore(); yield return 2;
            if (core == null)
            {
                var preview = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab"), player.transform.position + Vector3.up * 2, Quaternion.identity);
                core = preview.GetComponent<CoreObject>(); core.enabled = false;
                Note("Region C remains coreless; blue capture is an isolated authored-prefab preview, not a new gameplay spawn.");
            }
            core.MarkCoreLocationDiscovered(false);
            Move(core.transform.position + Vector3.left * 2); yield return 1;
            Capture(depth == ExpeditionDepth.DeepZone1 ? "07-region-b-orange-core" : "08-region-c-blue-core-preview");
            var presentation = core.GetComponent<CoreActivationPresentation>(); presentation.StartCoroutine(presentation.PlayActivationRoutine());
            yield return .5f; Capture(depth + "-core-activation");
            Require(EffectVisible(depth == ExpeditionDepth.DeepZone1 ? "CorePulse_orange" : "CorePulse_blue"), "Core activation emits approved family pulse on existing schedule");
            yield return 1.3f;
            Require(!EffectVisible(depth == ExpeditionDepth.DeepZone1 ? "CorePulse_orange" : "CorePulse_blue"), "Core activation pulses returned to pool");
        }
        Note("COMPLETE native readability fixtures");
    }
    static bool EffectVisible(string role) => Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
        .Any(r => r.enabled && r.gameObject.name.StartsWith(role + "(Clone)") && r.sprite != null);
    static void RevealTrackedCore()
    {
        var tracking = Object.FindFirstObjectByType<CoreTrackingSignalController>();
        if (tracking == null || !tracking.IsTrackingActive) return;
        foreach (var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
        {
            if (tracking.IsCoreRevealed) break;
            if (wreck.ObjectKind == HarvestObjectKind.HighValueWreck) wreck.TakeDamage(99999);
        }
        Require(tracking.IsCoreRevealed, "Core revealed through actual wreck completion signal events (QA instant damage)");
    }
}
