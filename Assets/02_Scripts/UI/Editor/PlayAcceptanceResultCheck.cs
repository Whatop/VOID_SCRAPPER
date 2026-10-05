using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Opt-in, disposable QA: saved scenes, real progression owners, isolated save.
// Teleports, invulnerability and scripted damage are recorded, never saved to assets.
[InitializeOnLoad]
public static class PlayAcceptanceResultCheck
{
    const string Key = "PlayAcceptance.ResultCheck", Dir = "Logs/PlayAcceptance/ResultCheck/";
    static readonly List<string> evidence = new List<string>(), errors = new List<string>();
    static IEnumerator<float> sequence;
    static double next, deadline;
    static PlayerHealth player;
    static RenderTexture target;
    static Camera camera;
    static bool finishing;
    static PlayAcceptanceResultCheck()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(Key, false)) Application.logMessageReceived += Log;
    }
    static void Log(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Dir + "Rendered");
        typeof(PlayAcceptanceProbe).GetMethod("SetNativeGameView", BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        File.Copy("Logs/PlayAcceptance/Normal-3/probe-save.json",Dir+"after-save.json",true);
        File.Copy("Logs/PlayAcceptance/Normal-3/probe-save.json",Dir+"after-save.json.bak",true);
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity", OpenSceneMode.Single);
        var save = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)).Single();
        var so = new SerializedObject(save);
        so.FindProperty("fileName").stringValue = Path.GetFullPath(Dir + SessionState.GetString(Key + ".Save", "after-save.json"));
        so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    public static void RunFinal()
    {
        SessionState.SetString(Key + ".Save", "campaign-save.json"); Run();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true; sequence = Check().GetEnumerator();
            next = 0; deadline = EditorApplication.timeSinceStartup + 1800; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            evidence.Add("Console errors/exceptions/assertions including teardown: " + errors.Count);
            Flush(); SessionState.SetBool(Key, false); EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < next || finishing) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Campaign QA timed out.");
            if (sequence.MoveNext()) next = EditorApplication.timeSinceStartup + sequence.Current;
            else Finish();
        }
        catch (Exception e) { errors.Add(e.ToString()); Finish(); }
    }
    static void Finish() { finishing = true; Flush(); EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); }
    static void Flush() { File.WriteAllLines(Dir + "campaign.txt", evidence); File.WriteAllLines(Dir + "campaign-errors.txt", errors); }
    static void Note(string s) { evidence.Add(DateTime.UtcNow.ToString("HH:mm:ss") + " " + s); Flush(); }
    static void Require(bool b, string s) { if (!b) throw new Exception(s); Note("PASS " + s); }
    static T Get<T>(object o, string f) => (T)o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    static IEnumerable<float> Until(Func<bool> test, string label, float timeout = 90)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!test()) { if (EditorApplication.timeSinceStartup > end) throw new Exception("Timed out: " + label); yield return .05f; }
        Note("REACHED " + label);
    }
    static IEnumerable<float> Scene(string name)
    {
        foreach (var d in Until(() => SceneManager.GetActiveScene().name == name && !SceneFlowManager.Instance.IsLoading, name)) yield return d;
        yield return 3;
        BindCamera();
    }
    static void BindCamera() { camera=Camera.main; }
    static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"Rendered/"+name+".png"));
        var lines=new List<string>();
        foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            lines.Add("CANVAS "+c.name+" mode="+c.renderMode+" order="+c.sortingOrder+" active="+c.gameObject.activeInHierarchy+" camera="+c.worldCamera);
        foreach(var g in Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None))
            lines.Add("GROUP "+g.name+" alpha="+g.alpha+" path="+AnimationUtility.CalculateTransformPath(g.transform,null));
        foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            lines.Add("TEXT "+text.name+" color="+text.color+" rendererAlpha="+text.canvasRenderer.GetInheritedAlpha()+" overflow="+text.isTextOverflowing+" text="+text.text.Replace('\n','|'));
        File.WriteAllLines(Dir+"Rendered/"+name+"-diagnostics.txt",lines);
        Note("ACTUAL native GameView screenshot requested "+name+" "+Screen.width+"x"+Screen.height+". No camera/canvas/render-target adaptation.");
    }
    static void Move(Vector3 position)
    {
        player.transform.position = position; var rb = player.GetComponent<Rigidbody2D>();
        rb.position = position; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    static void ReadyPlayer()
    {
        player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "Saved player available");
        player.SetDashInvincible(true);
    }
    static IEnumerable<float> Dialogue(int preferred = -1)
    {
        if (!DialogueManager.isConversationActive) yield break;
        string title = DialogueManager.lastConversationStarted; Note("DIALOGUE " + title);
        double end = EditorApplication.timeSinceStartup + 100;
        while (DialogueManager.isConversationActive)
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Dialogue timeout: " + title);
            var state = DialogueManager.instance.currentConversationState;
            if (state != null && state.hasPCResponses)
            {
                var response = state.pcResponses.FirstOrDefault(r => r.destinationEntry.id == preferred) ?? state.pcResponses.FirstOrDefault();
                if (response != null) DialogueManager.instance.conversationView.SelectResponse(new SelectedResponseEventArgs(response));
            }
            else DialogueManager.instance.conversationView?.OnConversationContinueAll();
            yield return .3f;
        }
        Note("DIALOGUE completed " + title);
    }
    static IEnumerable<float> Settlement()
    {
        foreach (var d in Scene("Settlement")) yield return d;
        foreach (var d in Dialogue()) yield return d;
        yield return 1; Capture("settlement-" + PermanentProgress.Instance.AcquiredBossStoryPartCount);
        SaveManager.Instance.Save(PermanentProgress.Instance);
        var controller=Object.FindFirstObjectByType<SettlementController>();
        var ui=Object.FindFirstObjectByType<SettlementUIController>();
        if(controller!=null&&ui!=null&&PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator))
        {
            foreach(var building in new[]{BuildingType.Hangar,BuildingType.WeaponLab})
            {
                ui.SelectBuilding(building);yield return .2f;Capture("facility-"+building+"-"+PermanentProgress.Instance.AcquiredBossStoryPartCount);
                if(controller.CanExecuteBuildingAction(building)&&controller.TryCompleteRestorationProject(building))
                {yield return .12f;Capture("restoration-"+building);yield return .8f;Capture("restoration-"+building+"-settled");Note("RESTORATION real authority "+building);}
            }
        }
    }
    static IEnumerable<float> Check()
    {
        Note("Separate result-screen diagnostic: isolated save; scripted lethal player damage only to reach result. Unmodified native GameView rendering; not a normal fight.");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        yield return 4;Capture("boot-native");yield return 1;
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
        foreach(var d in Scene("Expedition"))yield return d;
        player=Object.FindFirstObjectByType<PlayerHealth>();yield return 1;Capture("exploration-native");yield return 1;
        Note("OPPORTUNISTIC GENERATED ROLE OBSERVATION: pre-observation teleport only, normal health, no role/target/channel edits or invented pickups.");
        foreach(var role in Object.FindObjectsByType<EnemyRoleController>(FindObjectsSortMode.None).Where(r=>r.RoleType==EnemyRoleType.RivalHarvester||r.RoleType==EnemyRoleType.Scavenger).Take(2))
        {
            if(player.IsDead)break;
            Move(role.transform.position+Vector3.left*6f);yield return .7f;
            Capture("role-"+role.RoleType+"-approach");
            var channel=role.GetComponent<PickupCollectionPresentation>();
            double roleDeadline=EditorApplication.timeSinceStartup+25; bool seen=false;
            while(role!=null&&!player.IsDead&&EditorApplication.timeSinceStartup<roleDeadline)
            {
                if(channel!=null&&channel.IsShowing)
                {
                    seen=true;var pickup=channel.Target;Note("GENERATED channel seen "+role.RoleType+" target="+pickup+" phase="+role.CurrentPhase);
                    Capture("role-"+role.RoleType+"-channel");yield return .17f;Capture("role-"+role.RoleType+"-mid");yield return .5f;
                    Note("GENERATED channel settled showing="+channel.IsShowing+" pickupAvailable="+(pickup!=null&&pickup.IsAvailable)+" cargo="+role.CargoHold.HasCargo);
                    Capture("role-"+role.RoleType+"-settled");yield return .5f;break;
                }
                yield return .025f;
            }
            if(!seen)Note("GENERATED "+role.RoleType+" channel not observed within 25s; no forced state used.");
        }
        player.TakeDamage(99999);foreach(var d in ResultVisible())yield return d;
        Capture("result-native");yield return 1;
        Object.FindFirstObjectByType<RunResultPanelUI>().Close();foreach(var d in Settlement())yield return d;
        Capture("settlement-native");yield return 2;
    }
    static IEnumerable<float> EventLifecycle()
    {
        Note("FIXTURE EVENTS: authored prefabs near player, ambient enemies disabled, QA invulnerability and scripted event-enemy damage. No normal-play or difficulty claim.");
        foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        Move(Vector3.zero);yield return .5f;
        foreach(string eventName in new[]{"UnstableReactor","RescueSignal","UnknownDevice","BlackBoxRecovery"})
        {
            var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Event/Event_"+eventName+".prefab"),player.transform.position+Vector3.up*2,Quaternion.identity);
            var evt=obj.GetComponent<ExpeditionEventObject>();Capture("event-"+eventName+"-idle");evt.Interact(player.gameObject);
            yield return 1;Capture("event-"+eventName+"-active");
            if(eventName=="UnstableReactor")
            {
                foreach(var d in Until(()=>Get<object>(evt,"phase").ToString()=="ReactorFailure","Reactor authored timeout",20))yield return d;
                float stopped=Get<float>(evt,"reactorTimer");var enemies=Get<List<EnemyHealth>>(evt,"trackedEnemies");
                var ids=enemies.Select(e=>e.GetInstanceID()).ToArray();Require(ids.Length==3,"authored Reactor failure wave 3");Capture("reactor-timeout");
                for(int sample=1;sample<=6;sample++)
                {
                    yield return 5;
                    Require(evt.State==ExpeditionEventState.Active&&!evt.CanReceiveEventDamage&&Get<float>(evt,"reactorTimer")==stopped&&!Get<ReactorFeedbackUI>(evt,"reactorFeedback").IsShowing,"Reactor timer inactive after "+sample*5+" seconds of failure combat");
                    Require(enemies.Select(e=>e.GetInstanceID()).SequenceEqual(ids),"same three enemies; no repeat spawning at "+sample*5+"s");
                    Capture("reactor-failure-"+sample*5+"s");
                }
                foreach(var e in enemies.ToArray())e.TakeDamage(99999);
                foreach(var d in Until(()=>evt.State==ExpeditionEventState.Failed,"Reactor failure combat resolves Failed"))yield return d;
            }
            else
            {
                double limit=EditorApplication.timeSinceStartup+45;
                while(evt.State!=ExpeditionEventState.Completed&&EditorApplication.timeSinceStartup<limit)
                {
                    foreach(var e in Get<List<EnemyHealth>>(evt,"trackedEnemies").ToArray())if(e!=null&&!e.IsDead)e.TakeDamage(99999);
                    yield return .5f;
                }
                Require(evt.State==ExpeditionEventState.Completed,eventName+" completion");
            }
            Capture("event-"+eventName+"-settled");Object.Destroy(obj);yield return 2;
        }
    }
    static IEnumerable<float> World()
    {
        var enemy = Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None).FirstOrDefault();
        if (enemy != null)
        {
            Move(enemy.transform.position + Vector3.left * 2); yield return .4f;
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = camera.WorldToScreenPoint(enemy.transform.position) }.WithButton(MouseButton.Left));
            yield return .35f; Capture("02-raider-combat");
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(240, 135) });
        }
        var support = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith("LaserManagerShip"));
        if (support != null) { Move(support.transform.position + Vector3.down * 2); yield return 3; Capture("03-system-support"); }
        else Note("SKIP standalone SYSTEM support not generated; SYSTEM boss covered separately");
        var shop = Object.FindFirstObjectByType<ShopStructure>();
        if (shop != null)
        {
            Move(shop.transform.position + Vector3.down * .75f); yield return 1;
            Object.FindFirstObjectByType<ExpeditionHUD>().ShowCommunication(ShipCommunicationChannel.Navigation, "거래소 신호 확인 · 주변 항로를 확인하십시오.", ShipCommunicationSeverity.Information, 4);
            yield return .2f; Capture("04-neutral-shop");
        }
        var field = Object.FindObjectsByType<ExpeditionEventObject>(FindObjectsSortMode.None).FirstOrDefault();
        if (field != null)
        {
            Move(field.transform.position + Vector3.down); yield return 1; Capture("05-field-event-idle");
            if (field.CanInteract(player.gameObject)) field.Interact(player.gameObject);
            yield return 3; Capture("06-field-event-active"); Note("FIELD " + field.EventType + " state=" + field.State);
        }
        else Note("SKIP no Field Event generated");
    }
    static IEnumerable<float> Boss(ExpeditionDepth depth)
    {
        var generator = Object.FindFirstObjectByType<ExpeditionMapGenerator>();
        GameObject boss;
        if (depth == ExpeditionDepth.DeepZone2)
        {
            var phase = generator.CurrentRegion3BossEncounter;
            Require(phase != null, "Generated coreless Phase Gatekeeper");
            Move(phase.EncounterAnchor + Vector2.down * 3);
            if (phase.State == PhaseGatekeeperBossController.EncounterState.Dormant)
                Require(phase.TryForceStartEncounterForDevelopment(out string reason), "Region 3 intro: " + reason);
            boss = phase.gameObject;
        }
        else
        {
            var core = generator.SpawnedCoreObjects.Single(); Move(core.transform.position + Vector3.down * 2);
            Require(core.TryStartBossEncounterForDevelopment(out string reason), "Core intro: " + reason);
            Note("SHORTCUT Core Signal collection bypassed via existing development encounter start");
            foreach (var d in Until(() => Object.FindFirstObjectByType<BossDummyController>() != null, "boss spawned")) yield return d;
            boss = Object.FindFirstObjectByType<BossDummyController>().gameObject;
        }
        foreach (var d in Until(() => GameStateManager.Instance.CurrentState == GameState.BossBattle || GameStateManager.Instance.CurrentState == GameState.FinalBossBattle, "intro hands off combat")) yield return d;
        var health = boss.GetComponent<EnemyHealth>(); Note("COMBAT " + depth + " HP=" + health.MaxHp);
        if (depth != ExpeditionDepth.DeepZone1) Move(boss.transform.position + new Vector3(-2f, -.3f, 0));
        yield return 8;
        if (depth == ExpeditionDepth.FinalNetwork) Move(boss.transform.position + new Vector3(-2f, -.3f, 0));
        yield return .4f; Capture(depth + "-boss-phase1");
        var raider = boss.GetComponent<PirateCommanderBossController>();
        if (raider != null)
        {
            Capture("raider-boss-phase1"); yield return 12; health.TakeDamage(health.MaxHp * .6f);
            foreach (var d in Until(() => raider.IsPhase2, "Raider Commander phase 2")) yield return d;
            yield return 8; Capture("raider-boss-phase2"); health.TakeDamage(9999);
        }
        else if (depth == ExpeditionDepth.Normal)
        {
            yield return 18; Capture("region1-pattern"); health.TakeDamage(9999);
            yield return 9; Capture("region1-phase2"); health.TakeDamage(9999);
            yield return 26; health.TakeDamage(9999);
        }
        else if (depth == ExpeditionDepth.DeepZone1)
        {
            var triad = boss.GetComponent<FrigateTriadBossController>();
            for (int n = 3; n > 0; n--)
            {
                foreach (var d in Until(() => triad.CanAcceptPartDamage, "frigate accepts damage", 70)) yield return d;
                yield return 9; Capture("region2-" + n + "-frigates");
                triad.GetComponentsInChildren<FrigateBossPart>().First(p => p.IsAlive).TakeDamage(9999);
                yield return 1;
            }
        }
        else if (depth == ExpeditionDepth.DeepZone2)
        {
            var phase = boss.GetComponent<PhaseGatekeeperBossController>();
            for (int n = 0; n < 2; n++)
            {
                foreach (var d in Until(() => phase.IsExposed, "natural reflected laser exposure", 180)) yield return d;
                Capture("region3-exposure-" + n); health.TakeDamage(n == 0 ? health.MaxHp * .55f : 9999);
                if (n == 0) foreach (var d in Until(() => !phase.IsExposed, "exposure closes")) yield return d;
            }
        }
        else
        {
            var nul = boss.GetComponent<NullDispatcherBossController>();
            Capture("null-phase1");
            foreach (var d in Until(() => Get<System.Collections.IList>(nul, "lasers").Count > 0, "NULL laser warning")) yield return d;
            float warningStart = Time.time;
            Capture("null-telegraph"); yield return .4f; Capture("null-telegraph-late");
            foreach (var vfx in Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Where(v => v.name.StartsWith("RainWarning")))
                Require(Mathf.Abs(vfx.Visual.bounds.size.x - Get<Bounds>(nul, "arena").size.x) < .01f, "Rendered warning spans the real damage lane");
            foreach (var d in Until(() => Get<bool>(nul, "lanesDamaging"), "warning damage handoff")) yield return d;
            float fireStart = Time.time;
            Require(fireStart - warningStart >= .70f && fireStart - warningStart <= .95f, "NULL warning stays 0.8 seconds: " + (fireStart - warningStart).ToString("F3"));
            yield return .2f; Capture("null-beams");
            foreach (var d in Until(() => !Get<bool>(nul, "lanesDamaging"), "rain damage ends")) yield return d;
            Require(Time.time - fireStart >= 2.3f && Time.time - fireStart <= 2.55f, "NULL damage stays 2.4 seconds: " + (Time.time - fireStart).ToString("F3"));
            health.TakeDamage(9999);
            foreach (var d in Until(() => DialogueManager.isConversationActive, "TreatmentDialogue")) yield return d;
            Capture("null-treatment"); foreach (var d in Dialogue(7)) yield return d;
            foreach (var d in Until(() => nul.Phase == NullDispatcherBossController.EncounterPhase.PolarityPhase, "NULL Phase 2 support convergence")) yield return d;
            Capture("null-phase2"); yield return 10; health.TakeDamage(9999);
            foreach (var d in Until(() => nul.Phase == NullDispatcherBossController.EncounterPhase.FinalPhase, "NULL critical")) yield return d;
            Capture("null-critical"); yield return 4; health.TakeDamage(9999);
            foreach (var d in Until(() => DialogueManager.isConversationActive, "termination dialogue")) yield return d;
            yield return .7f; Capture("ending"); foreach (var d in Dialogue()) yield return d;
            foreach (var d in Until(() => PermanentProgress.Instance.FinalBossDefeated, "FinalVictory")) yield return d;
            yield break;
        }
        foreach (var d in Until(() => RunManager.Instance.CurrentRun.BossDefeated, "boss death committed", 70)) yield return d;
    }
    static IEnumerable<float> RewardAndReturn(string region)
    {
        foreach (var d in Until(() => Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).Any(c => c.PayloadType == RewardCapsulePayloadType.BossReward), "boss reward spawned")) yield return d;
        var capsule = Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).First(c => c.PayloadType == RewardCapsulePayloadType.BossReward);
        Move(capsule.transform.position + Vector3.down); yield return 2; Capture(region + "-pickups-reward");
        foreach (var d in Until(() => capsule.CanInteract(player.gameObject), "reward landed")) yield return d;
        capsule.Interact(player.gameObject);
        var ui = Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();
        foreach (var d in Until(() => ui.IsShowing, "reward choices")) yield return d;
        yield return .5f; Capture(region + "-reward-choice");
        Call(ui, "HandleRewardSelected", Get<List<RunRewardOption>>(ui, "currentOptions")[0]);
        foreach (var d in Until(() => Object.FindFirstObjectByType<ReturnBeacon>() != null && Object.FindFirstObjectByType<ReturnBeacon>().CanInteract(player.gameObject), "Return Beacon ready")) yield return d;
        var beacon = Object.FindFirstObjectByType<ReturnBeacon>(); Move(beacon.transform.position); beacon.ConfirmReturn();
        yield return 5; foreach(var d in ResultVisible())yield return d; Capture(region + "-result");
        Object.FindFirstObjectByType<RunResultPanelUI>().Close(); foreach (var d in Settlement()) yield return d;
        Note("RETURN " + region + " real reward -> SafeReturn -> Settlement analysis");
    }
    static IEnumerable<float> ResultVisible()
    {
        foreach(var d in Until(()=>
        {
            var result=Object.FindFirstObjectByType<RunResultPanelUI>();
            return result!=null&&Get<GameObject>(result,"panelRoot").activeInHierarchy&&Get<CanvasGroup>(result,"canvasGroup").alpha>=.99f;
        },"Result fully visible"))yield return d;
        yield return .5f;
    }
    static IEnumerable<float> Defense()
    {
        var encounter = Object.FindFirstObjectByType<SettlementDefenseEncounterController>(FindObjectsInactive.Include);
        var route = Object.FindFirstObjectByType<SettlementRouteCoreController>(FindObjectsInactive.Include);
        if (PermanentProgress.Instance.SettlementDefenseCleared) { Note("RESUME previously cleared defense"); yield break; }
        encounter.EnterDeck(); yield return .5f; BindCamera(); player = Get<PlayerHealth>(encounter, "player"); player.SetDashInvincible(true);
        Capture("defense-inspection");
        if (PermanentProgress.Instance.CanAssembleRouteCore) Require(route.TryAssembleRouteCore(), "Route Core assembled using collected parts");
        if (PermanentProgress.Instance.CanActivateRouteCore) Require(route.TryActivateRouteCore(), "Route Core activates defense");
        else Require(route.BeginSettlementDefense(), "Retry existing activated core");
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            foreach (var d in Until(() => encounter.Cores.Count == 3 && encounter.Cores[index].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "defense component " + index)) yield return d;
            var core = encounter.Cores[i]; Move(core.transform.position + Vector3.down * 2); yield return 4;
            Capture("defense-component-" + i); core.Health.TakeDamage(9999); yield return .2f;
            Move(core.transform.position + Vector3.down * .8f); yield return .2f; core.Interact(player.gameObject);
        }
        foreach (var d in Until(() => encounter.IsPurpleActive, "Purple Radar phase")) yield return d;
        var purple = Object.FindFirstObjectByType<SettlementDefensePurpleCore>();
        Move(purple.transform.position + Vector3.down * 1.5f);
        var scanner = player.GetComponent<PlayerRadarScanner>(); scanner.TryToggleRadarMode(); yield return .3f;
        Require(scanner.TryQuickScan(), "Actual radar Quick Scan accepted"); yield return .3f; Capture("defense-purple");
        Require(purple.State == SettlementDefensePurpleCore.Phase.Exposed, "Scan exposes Purple"); purple.Health.TakeDamage(9999);
        foreach (var d in Until(() => PermanentProgress.Instance.SettlementDefenseCleared, "Defense completion")) yield return d;
        yield return 3; Capture("defense-complete");
    }
}
