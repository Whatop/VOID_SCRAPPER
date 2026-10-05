using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Disposable Play Mode diagnostics of the saved production encounter paths. No scene/asset/save writes.
[InitializeOnLoad]
public static class BossFinalPolishProbe
{
    const string Key = "BossFinalPolish.Render";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static string Scenario => SessionState.GetString(Key + ".Case", "sector");
    static string Case => Scenario.Replace("-death", "");
    static bool DeathOnly => Scenario.EndsWith("-death");
    static string Output => "Logs/BossFinalPolish/Rendered/" + Scenario + "/";
    static readonly StringBuilder audit = new StringBuilder();
    static readonly HashSet<string> captures = new HashSet<string>();
    static IEnumerator<float> sequence;
    static double nextAt, reportAt;
    static ExpeditionMapGenerator generator;
    static PlayerHealth player;
    static PlayerController2D movement;
    static Camera camera;
    static RenderTexture target;
    static RunManager manager;
    static PermanentProgress progress;
    static GameObject boss;
    static EnemyHealth health;
    static float startTime;
    static int bossSourceId, deaths, results;
    static RunEndReason resultReason;
    static string lastState;
    static string pendingCapture;
    static float captureAt;
    static Unity.Profiling.ProfilerRecorder allocations;
    static readonly List<long> allocationSamples = new List<long>();
    internal static T Get<T>(object o, string f) => (T)o.GetType().GetField(f, Private).GetValue(o);
    internal static void Set(object o, string f, object v) => o.GetType().GetField(f, Private).SetValue(o, v);
    internal static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Private).Invoke(o, a);
    static void Require(bool v, string m) { if (!v) throw new InvalidOperationException(m); audit.AppendLine("PASS " + m); }
    static BossFinalPolishProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before validation.");
        string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-bossCase");
        SessionState.SetString(Key + ".Case", at >= 0 ? args[at + 1] : "sector");
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "failure.txt")) File.Delete(Output + "failure.txt");
        var scene = EditorSceneManager.OpenScene(EnemyRosterAudit.ScenePath, OpenSceneMode.Single);
        generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene);
        player = EnemyRosterAudit.Single<PlayerHealth>(scene);
        camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var keep = new List<GameObject> { generator.gameObject, player.gameObject, camera.transform.root.gameObject,
            Get<SpaceBackgroundGenerator2D>(generator, "backgroundGenerator").gameObject, Get<Transform>(generator, "startPoint").gameObject };
        keep.AddRange(scene.GetRootGameObjects().Where(g => g.GetComponent<Canvas>() != null || g.GetComponent<UnityEngine.EventSystems.EventSystem>() != null));
        keep.AddRange(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true)).Select(c => c.gameObject));
        foreach (var go in keep.Distinct()) { go.SetActive(false); go.transform.SetParent(null, true); }
        foreach (var root in scene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        Set(generator, "generateOnStart", false);
        if (Case.StartsWith("null"))
        {
            var boot = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/Boot.unity");
            try
            {
                var result = EnemyRosterAudit.Single<RunResultPanelUI>(boot);
                var canvas = Object.Instantiate(result.GetComponentInParent<Canvas>(true).gameObject);
                canvas.SetActive(false); canvas.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(canvas, scene);
                var dialogue = Object.Instantiate(EnemyRosterAudit.Single<DialogueSystemController>(boot).gameObject);
                dialogue.SetActive(false); dialogue.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(dialogue, scene);
            }
            finally { EditorSceneManager.ClosePreviewScene(boot); }
        }
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange s)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (s == PlayModeStateChange.EnteredPlayMode) { sequence = Check().GetEnumerator(); nextAt = EditorApplication.timeSinceStartup + .3; EditorApplication.update += Tick; }
        if (s == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    static void Tick()
    {
        try
        {
            if (allocations.Valid) allocationSamples.Add(allocations.LastValue);
            Observe();
            if (EditorApplication.timeSinceStartup > reportAt) { File.WriteAllText(Output + "audit.txt", audit.ToString()); reportAt = EditorApplication.timeSinceStartup + 3; }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            if (sequence.MoveNext()) nextAt = EditorApplication.timeSinceStartup + sequence.Current;
            else
            {
                if (allocationSamples.Count > 0)
                {
                    allocationSamples.Sort();
                    audit.AppendLine("Editor GC bytes/frame samples: median=" + allocationSamples[allocationSamples.Count / 2] + " p95=" + allocationSamples[(int)(allocationSamples.Count * .95f)] + " max=" + allocationSamples.Last() + "; includes Editor, logging and screenshots, not a standalone benchmark.");
                }
                allocations.Dispose(); File.WriteAllText(Output + "audit.txt", audit.ToString()); EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText(Output + "failure.txt", ex.ToString()); File.WriteAllText(Output + "audit.txt", audit.ToString());
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    static void Setup()
    {
        Time.maximumDeltaTime = .1f;
        var scene = SceneManager.GetActiveScene(); generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene);
        player = EnemyRosterAudit.Single<PlayerHealth>(scene); movement = player.GetComponent<PlayerController2D>();
        camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var owners = new GameObject("Transient boss QA owners"); owners.SetActive(false);
        manager = owners.AddComponent<RunManager>(); typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        progress = owners.AddComponent<PermanentProgress>(); typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        Set(progress, "equipmentCatalog", AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset")); progress.LoadFromSave(new SaveData());
        if (Case == "null-reject") Set(progress, "buildingLevels", Enum.GetValues(typeof(BuildingType)).Cast<BuildingType>().Select(b => new BuildingLevelState(b, 1)).ToList());
        var state = owners.AddComponent<GameStateManager>(); Set(state, "currentState", GameState.Expedition); typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        // The isolated fixture creates state after AfterSceneLoad audio bootstrap. Rebind through
        // its normal lifecycle, matching Boot's owner order so real result-music waits can complete.
        var audio = GameAudioLoopController.Instance;
        if (audio != null) { audio.enabled = false; audio.enabled = true; }
        var store = owners.AddComponent<RunRuntimeTraitStore>(); typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        new GameObject("Transient pool owner").AddComponent<PoolManager>();
        var localization = Case.StartsWith("null") ? EnemyRosterAudit.Single<VoidScrapperLocalizationService>(scene) : owners.AddComponent<VoidScrapperLocalizationService>(); Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true); typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, "en"); Set(localization, "currentLanguageCode", "en");
        var depth = Case == "salvage" ? ExpeditionDepth.DeepZone1 : Case == "phase" ? ExpeditionDepth.DeepZone2 : Case.StartsWith("null") ? ExpeditionDepth.FinalNetwork : ExpeditionDepth.Normal;
        if (Case == "raider") progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, depth));
        manager.RunEnded += r => { results++; resultReason = r.endReason; };
        Object.DontDestroyOnLoad(owners);
        if (Case.StartsWith("null"))
        {
            var flow = new GameObject("Transient scene flow"); flow.AddComponent<SceneFlowManager>(); Object.DontDestroyOnLoad(flow);
            var resultOwner = new GameObject("Transient CoreRoot for authored result"); resultOwner.SetActive(false);
            var bootstrap = resultOwner.AddComponent<GameBootstrap>();
            // Keep the authored result's parent contract, without invoking Boot/load/save startup.
            Set(bootstrap, "runtimeInitialized", true); Set(bootstrap, "startFlowExecuted", true); bootstrap.enabled = false;
            EnemyRosterAudit.Single<RunResultPanelUI>(scene).GetComponentInParent<Canvas>(true).transform.SetParent(resultOwner.transform, false);
            Object.DontDestroyOnLoad(resultOwner); resultOwner.SetActive(true);
        }
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create(); camera.targetTexture = target;
        foreach (var root in scene.GetRootGameObjects()) if (root != owners) root.SetActive(true);
        var resultPanel = Object.FindFirstObjectByType<RunResultPanelUI>(FindObjectsInactive.Include);
        if (resultPanel != null) resultPanel.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
        player.SetDashInvincible(true); player.GetComponent<PlayerWeaponController>().SetExternalInputLocked(true);
        BindCanvases(); Require(SaveManager.Instance == null, "No SaveManager in disposable Play Mode fixture");
        allocations = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
        audit.AppendLine("Actual saved Expedition and generated encounter; authored camera follows player; 480x270 render with authored HUD. Invincibility for pattern coverage; scripted boss damage, not a human balance run. No asset/save writes.");
    }
    static void BindCanvases()
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1;
            var scaler = c.GetComponent<CanvasScaler>(); if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1; }
        }
    }
    static void Move(Vector2 p) { player.transform.position = p; var rb = player.GetComponent<Rigidbody2D>(); rb.position = p; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static IEnumerable<float> Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return .02f; }
    static IEnumerable<float> Until(Func<bool> predicate, float timeout, string reason)
    {
        float until = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < until) yield return .02f;
        Require(predicate(), reason);
    }
    static IEnumerable<float> Check()
    {
        Setup(); yield return .3f; UnityEngine.Random.InitState(401); generator.Generate(); yield return .5f;
        // Keep generated boss/corridor/reflector authority; ordinary combat is outside this fixture.
        foreach (var ai in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None)) ai.gameObject.SetActive(false);
        if (Case == "phase")
        {
            var phase = generator.CurrentRegion3BossEncounter; Require(phase != null && phase.ReflectorPlateCount == 12, "Saved first-visit foundation has twelve reflectors");
            Move(phase.EncounterAnchor + Vector2.down * 3); boss = phase.gameObject; health = boss.GetComponent<EnemyHealth>();
            if (phase.State == PhaseGatekeeperBossController.EncounterState.Dormant)
                Require(phase.TryForceStartEncounterForDevelopment(out string reason), "Coreless encounter start: " + reason);
            else Require(phase.State == PhaseGatekeeperBossController.EncounterState.Intro, "Physical proximity already began the coreless encounter");
        }
        else
        {
            Require(generator.SpawnedCoreObjects.Count == 1, "Generated exactly one Core");
            var core = generator.SpawnedCoreObjects[0]; Move(core.transform.position + Vector3.down * 2);
            Require(core.TryStartBossEncounterForDevelopment(out string reason), "Production Core intro start: " + reason);
            foreach (float d in Until(() => Object.FindFirstObjectByType<BossDummyController>() != null, 70, "Intro spawned production boss")) yield return d;
            boss = Object.FindFirstObjectByType<BossDummyController>().gameObject; health = boss.GetComponent<EnemyHealth>(); Capture("intro");
        }
        foreach (float d in Until(() => GameStateManager.Instance.CurrentState == GameState.BossBattle || GameStateManager.Instance.CurrentState == GameState.FinalBossBattle, 90, "Intro handed off to combat")) yield return d;
        bossSourceId = boss.transform.GetInstanceID(); health.Died += _ => deaths++;
        Require(movement.ControlEnabled, "Intro released player control"); startTime = Time.time;
        audit.AppendLine("Production max HP=" + health.MaxHp + " camera size=" + camera.orthographicSize);
        if (DeathOnly)
        {
            foreach (float d in Wait(7)) yield return d;
            if (Case == "sector") { health.TakeDamage(9999); yield return .15f; }
            Capture("before-player-death");
            player.SetDashInvincible(false); Set(player, "invincibleTimer", 0f); player.TakeDamage(9999);
            foreach (float d in Until(() => player.IsDead && results == 1, 20, "Actual player death presentation produces one result")) yield return d;
            Require(resultReason == RunEndReason.Death, "Player death uses Death result authority");
            if (Case == "sector")
            {
                var p = boss.GetComponent<BossPatternController>();
                Require(Get<Coroutine>(p, "patternRoutine") == null && Get<Coroutine>(p, "phase2TransitionRoutine") == null, "Player death stops Sector scheduler and transition");
                Require(!Get<bool>(p, "phase2PlayerLockActive"), "Player death releases Sector cinematic input ownership");
            }
            if (Case == "salvage") Require(!boss.GetComponent<FrigateTriadBossController>().IsGameplayActive, "Player death stops frigate combat/corridor");
            if (Case == "phase") Require(boss.GetComponent<PhaseGatekeeperBossController>().State == PhaseGatekeeperBossController.EncounterState.DeadOrCleanup, "Player death cleans Phase encounter");
            if (Case.StartsWith("null")) Require(!boss.GetComponent<NullDispatcherBossController>().PatternRunning, "Player death stops final scheduler");
            Require(!GungeonStyleCamera2D.Instance.IsCinematicInputOffsetLocked, "Player death releases boss cinematic camera lock");
            Capture("player-death-cleanup"); yield break;
        }
        if (Case == "sector") foreach (float d in Sector()) yield return d;
        if (Case == "salvage") foreach (float d in Salvage()) yield return d;
        if (Case == "phase") foreach (float d in Phase()) yield return d;
        if (Case.StartsWith("null")) foreach (float d in Final()) yield return d;
        if (Case == "raider")
        {
            foreach (float d in Wait(20)) yield return d; health.TakeDamage(health.MaxHp * .6f);
            foreach (float d in Until(() => boss.GetComponent<PirateCommanderBossController>().IsPhase2, 12, "Raider enters its existing phase 2")) yield return d;
            Require(Get<GameObject[]>(boss.GetComponent<PirateCommanderBossController>(), "spawnedEscorts").Count(g => g != null && g.activeInHierarchy) == 2, "Raider has its two authored escorts");
            foreach (float d in Wait(25)) yield return d; health.TakeDamage(9999);
        }
        if (Case.StartsWith("null")) yield return .4f;
        else foreach (float d in Wait(3)) yield return d;
        Capture("death-reward");
        audit.AppendLine("Scripted encounter duration=" + (Time.time - startTime).ToString("F2") + " s; player invincible, hits/damage and natural kill time not measured.");
        Require(health == null || health.IsDead, "Boss death accepted");
        Require(deaths == 1, "Death event occurs exactly once");
        Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b => b.SourceRoot != null && b.SourceRoot.GetInstanceID() == bossSourceId), "No boss-source bullet survives death");
        if (!Case.StartsWith("null"))
        {
            Require(manager.CurrentRun.BossDefeated, "Run boss defeat registered before reward interaction");
            Require(movement.ControlEnabled && !movement.MovementLocked, "Death releases movement lock");
            var dash = player.GetComponent<PlayerDash>(); Set(dash, "lastDashTime", -999f); Set(movement, "moveInput", Vector2.left);
            Require(dash.TryDash(), "Actual dash works after boss death"); foreach (float d in Wait(.3f)) yield return d;
            var exit = Object.FindFirstObjectByType<BossRewardExitCoordinator>(); Require(exit != null, "Existing reward/exit coordinator owns choices");
            audit.AppendLine("Reward coordinator=" + exit.name + "; selectable rewards remain unresolved for capture.");
            var capsule = Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).Single(c => c.PayloadType == RewardCapsulePayloadType.BossReward);
            foreach (float d in Until(() => capsule.CanInteract(player.gameObject), 15, "Boss capsule finished landing")) yield return d;
            Move(capsule.transform.position); capsule.Interact(player.gameObject);
            var selection = Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();
            foreach (float d in Until(() => selection != null && selection.IsShowing, 15, "Reward capsule opens authored choices")) yield return d;
            Capture("reward-choice");
            var options = Get<List<RunRewardOption>>(selection, "currentOptions"); Call(selection, "HandleRewardSelected", options[0]);
            foreach (float d in Wait(2)) yield return d;
            Require(Object.FindObjectsByType<ReturnBeacon>(FindObjectsSortMode.None).Length == 1, "Reward resolution reveals one Return Beacon");
            if (Case != "raider") Require(Object.FindObjectsByType<WormholePortal>(FindObjectsSortMode.None).Length == 0, "First clear creates no next-region portal");
            Capture("exits");
        }
        else
        {
            Require(results == 1 && resultReason == RunEndReason.FinalVictory, "One FinalVictory result");
            var panel = Object.FindFirstObjectByType<RunResultPanelUI>(); Require(panel != null, "Authored Boot result panel survives to ending");
            Get<Button>(panel, "continueButton").onClick.Invoke();
            foreach (float d in Until(() => SceneManager.GetActiveScene().name == "Settlement", 45, "Result Continue returns through SceneFlow to Settlement")) yield return d;
            Require(SaveManager.Instance == null, "Scene return does not touch user save");
        }
    }
    static IEnumerable<float> Sector()
    {
        var p = boss.GetComponent<BossPatternController>();
        foreach (float d in Wait(36)) yield return d;
        health.TakeDamage(9999);
        Require(!health.IsDead && Mathf.Approximately(health.CurrentHp, health.MaxHp * .5f), "Lethal hit cannot bypass the serialized half-HP phase gate");
        foreach (float d in Wait(10)) yield return d;
        Require(Get<bool>(p, "phase2TransitionStarted"), "Phase 2 transition triggered at production threshold");
        if (Get<bool>(p, "phase2ShieldActive")) health.TakeDamage(9999);
        foreach (float d in Wait(30)) yield return d;
        health.TakeDamage(9999);
    }
    static IEnumerable<float> Salvage()
    {
        var p = boss.GetComponent<FrigateTriadBossController>(); Require(p.AlivePartCount == 3, "Three live frigates");
        foreach (float d in Wait(20)) yield return d;
        p.GetComponentsInChildren<FrigateBossPart>().First(x => x.IsAlive).TakeDamage(9999);
        foreach (float d in Until(() => p.AlivePartCount == 2 && p.CanAcceptPartDamage, 30, "Formation transitions to two")) yield return d;
        foreach (float d in Wait(17)) yield return d;
        p.GetComponentsInChildren<FrigateBossPart>().First(x => x.IsAlive).TakeDamage(9999);
        foreach (float d in Until(() => p.AlivePartCount == 1 && p.CanAcceptPartDamage, 30, "Formation transitions to one")) yield return d;
        foreach (float d in Wait(18)) yield return d;
        p.GetComponentsInChildren<FrigateBossPart>().First(x => x.IsAlive).TakeDamage(9999);
        foreach (float d in Until(() => health.IsDead, 40, "Final charge hands off death without aggregate HP strand")) yield return d;
    }
    static IEnumerable<float> Phase()
    {
        var p = boss.GetComponent<PhaseGatekeeperBossController>(); float hp = health.CurrentHp; health.TakeDamage(10);
        Require(health.CurrentHp == hp, "Protected Phase Gatekeeper rejects ordinary damage");
        foreach (float d in Until(() => p.IsExposed, 140, "Reflection cycle opens exposure")) yield return d;
        Capture("first-exposure"); health.TakeDamage(health.MaxHp * .55f);
        foreach (float d in Until(() => !p.IsExposed, 25, "Five-second exposure closes")) yield return d;
        foreach (float d in Until(() => p.IsExposed, 180, "Escalated reflection cycle reopens exposure")) yield return d;
        Capture("phase-two-exposure"); health.TakeDamage(9999);
    }
    static IEnumerable<float> Final()
    {
        var p = boss.GetComponent<NullDispatcherBossController>();
        foreach (float d in Wait(20)) yield return d;
        health.TakeDamage(9999); Require(health.CurrentHp == health.MaxHp * .5f && p.TreatmentGateCount == 1, "Treatment floor catches lethal damage exactly once");
        foreach (float d in Until(() => DialogueManager.isConversationActive, 30, "Actual Pixel Crushers treatment conversation opens")) yield return d;
        yield return .6f; Capture("treatment-dialogue");
        foreach (float d in DriveConversation(Case == "null-accept")) yield return d;
        foreach (float d in Until(() => p.Phase == NullDispatcherBossController.EncounterPhase.PolarityPhase, 70, "Choice and explicit support reach polarity combat")) yield return d;
        Require(p.Choice == (Case == "null-accept" ? NullDispatcherBossController.TreatmentChoice.Accept : NullDispatcherBossController.TreatmentChoice.Reject), "Authoritative selected treatment branch");
        foreach (float d in Wait(22)) yield return d; health.TakeDamage(9999);
        foreach (float d in Until(() => p.Phase == NullDispatcherBossController.EncounterPhase.FinalPhase, 25, "Twenty-percent gate enters final phase once")) yield return d;
        Require(p.FinalTransitionCount == 1, "Exactly one shell transition");
        foreach (float d in Wait(20)) yield return d; health.TakeDamage(9999);
        foreach (float d in Until(() => DialogueManager.isConversationActive, 30, "Actual termination conversation opens")) yield return d;
        yield return .6f; Capture("ending-dialogue"); foreach (float d in DriveConversation(false)) yield return d;
        foreach (float d in Until(() => results == 1 && manager.IsCompletingRun, 40, "Ending emits one RunManager result")) yield return d;
        var panel = Object.FindFirstObjectByType<RunResultPanelUI>();
        Require(panel != null && Get<RunManager>(panel, "subscribedRunManager") == manager, "Result panel is subscribed to the actual run owner");
        foreach (float d in Until(() => Get<GameObject>(panel, "panelRoot").activeInHierarchy &&
            Get<CanvasGroup>(panel, "canvasGroup").alpha >= .99f, 15, "Authored result panel finishes music wait and reveal")) yield return d;
        yield return .3f; Capture("result");
    }
    static IEnumerable<float> DriveConversation(bool accept)
    {
        // Exercise the installed graph through the same StandardDialogueUI callbacks as buttons.
        float deadline = Time.realtimeSinceStartup + 65;
        while (DialogueManager.isConversationActive && Time.realtimeSinceStartup < deadline)
        {
            var ui = DialogueManager.dialogueUI as StandardDialogueUI;
            Require(ui != null, "Installed StandardDialogueUI available");
            var responses = ui.GetComponentsInChildren<StandardUIResponseButton>(true);
            var active = responses.Where(b => b != null && b.gameObject.activeInHierarchy && b.response != null).ToArray();
            if (active.Length > 0)
            {
                // Capture the actual choice state after layout/typewriter, before invoking a response.
                yield return .6f; Capture("treatment-choices");
                int index = accept ? 0 : active.Length - 1;
                active[index].OnClick();
            }
            else ui.OnContinue();
            yield return .4f;
        }
        Require(!DialogueManager.isConversationActive, "Installed conversation completes");
    }
    static void Observe()
    {
        if (boss == null || target == null) return;
        string state = "";
        var s = boss.GetComponent<BossPatternController>();
        if (s != null) state = "sector-p2-" + Get<bool>(s, "phase2") + "-next-" + Get<int>(s, "nextPatternIndex") + "-casting-" + Get<bool>(s, "casting") + "-rotating-" + Get<List<BossDynamicLaserBeam>>(s, "activeRotatingLasers").Count;
        var r = boss.GetComponent<FrigateTriadBossController>(); if (r != null) state = r.State + "-" + r.ActivePattern + "-" + r.FinalSequenceState;
        var g = boss.GetComponent<PhaseGatekeeperBossController>(); if (g != null) state = g.State + "-cycle-" + g.LasersCompletedInCycle;
        var n = boss.GetComponent<NullDispatcherBossController>(); if (n != null) state = n.Phase + "-" + n.PlayerPolarity + "-pattern-" + Get<int>(n, "lastPattern") + "-damage-" + n.PatternDamageEnabled;
        var raider = boss.GetComponent<PirateCommanderBossController>();
        if (raider != null)
        {
            var telegraph = Get<RaiderCoverBlastTelegraph>(raider, "coverBlastTelegraph");
            state = "raider-p2-" + raider.IsPhase2 + "-combat-" + raider.IsCombatActive + "-cover-" +
                (telegraph != null && Get<bool>(telegraph, "presentationVisible")) + "-escorts-" +
                Get<GameObject[]>(raider, "spawnedEscorts").Count(g => g != null && g.activeInHierarchy);
        }
        if (state != lastState)
        {
            audit.AppendLine("t=" + Time.time.ToString("F2") + " hp=" + health.CurrentHp + " " + state);
            lastState = state; pendingCapture = state; captureAt = Time.time + .12f;
        }
        if (pendingCapture != null && Time.time >= captureAt) { Capture(pendingCapture); pendingCapture = null; }
    }
    static void Capture(string label)
    {
        if (!captures.Add(label)) return;
        BindCanvases(); Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + label + ".png", texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine("CAPTURE " + label + " camera=" + camera.transform.position + " size=" + camera.orthographicSize + " player=" + player.transform.position);
    }
}
