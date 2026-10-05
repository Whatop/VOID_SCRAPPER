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
public static class PhaseGatekeeperProbe
{
    const string SessionKey = "PhaseGatekeeper.Probe", PresentationKey = "PhaseGatekeeper.PresentationProbe";
    static string Dir => SessionState.GetBool(PresentationKey,false) ? "Logs/PhaseGatekeeper/Interruption/" : "Logs/PhaseGatekeeper/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static PhaseGatekeeperBossController boss;
    static EnemyHealth bossHealth;
    static bool autoFire;
    static string weaponLabel;
    static float lastSample = -1;
    static PhaseGatekeeperBossController.RouteStage lastStage;
    static int lastCycle;
    static float stageStart;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing;
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static float nextInputDiagnostic;
    static PhaseGatekeeperProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run() => BeginRun(false);
    public static void RunInterruption() => BeginRun(true);
    static void BeginRun(bool presentation)
    {
        SessionState.SetBool(PresentationKey,presentation);
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
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 2000; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); EditorApplication.Exit(errors.Count == 0 ? 0 : 1); }
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
            .Where(r => r.enabled && !r.forceRenderingOff && camera.WorldToViewportPoint(r.bounds.center).x > -.1f && camera.WorldToViewportPoint(r.bounds.center).x < 1.1f)
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


    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static int deaths,commitAttack=-1;static Vector2 committedPoint,entryPoint,exitPoint;
    static bool observedPair;static BossLaserHazard transferBeam;static float beamBorn,committedAt;
    static void SampleCombat()
    {
        if(boss==null||bossHealth==null)return;
        var stage=boss.CurrentRouteStage;
        if(Time.time!=lastSample)
        {
            lastSample=Time.time;
            int count=boss.PortalEndpointCount;
            if(count!=0&&count!=2)throw new Exception("Unpaired endpoint");
            var portals=Object.FindObjectsByType<PhaseCombatVfx>(FindObjectsSortMode.None).Where(v=>v.name.StartsWith("PhasePortal")&&v.IsVisible).ToArray();
            if(portals.Length>2)throw new Exception("More than one portal pair");
            var beam=boss.RouteBeam;int active=beam!=null&&beam.IsDamaging?1:0;
            if(active>0 && (!beam.GetComponent<LineRenderer>().enabled||!beam.GetComponent<BoxCollider2D>().enabled))throw new Exception("Invisible beam damage");
            if(active>0 && stage!=PhaseGatekeeperBossController.RouteStage.Entering&&stage!=PhaseGatekeeperBossController.RouteStage.Redirected&&stage!=PhaseGatekeeperBossController.RouteStage.PrecisionFire)throw new Exception("Beam before committed fire");
            if(stage!=lastStage||lastCycle!=boss.RouteAttackCount){lastStage=stage;lastCycle=boss.RouteAttackCount;stageStart=Time.time;}
            float age=Time.time-stageStart;
            if(stage==PhaseGatekeeperBossController.RouteStage.Committed&&commitAttack!=boss.RouteAttackCount)
            {commitAttack=boss.RouteAttackCount;committedPoint=boss.CommittedRouteTarget;committedAt=Time.time;}
            if(active>0)
            {
                if(boss.CommittedRouteTarget!=committedPoint)throw new Exception("Committed target changed during firing");
                if(Time.time-committedAt<.38f)throw new Exception("Commit-to-fire delay lost");
                if(beam.RuntimeOwner!=boss.transform||beam.DamageAmount!=4||beam.DamageCooldown!=.5f)throw new Exception("Beam ownership/damage contract changed");
                if(stage==PhaseGatekeeperBossController.RouteStage.Entering){transferBeam=beam;beamBorn=Time.time-age;}
                if(stage==PhaseGatekeeperBossController.RouteStage.Redirected)
                {
                    if(beam!=transferBeam||!beam.HasRedirected)throw new Exception("Redirect spawned a second beam");
                    if(Time.time-beamBorn>.86f)throw new Exception("Redirect restarted beam lifetime");
                }
            }
            if(count==2)
            {
                if(!observedPair){observedPair=true;entryPoint=boss.RouteEntry;exitPoint=boss.RouteExit;}
                if(boss.RouteEntry!=entryPoint||boss.RouteExit!=exitPoint)throw new Exception("Portal pair moved during attack");
                if(!boss.RouteArenaBounds.Contains(boss.RouteEntry)||!boss.RouteArenaBounds.Contains(boss.RouteExit))throw new Exception("Portal outside arena");
                if(portals.Any(v=>v.GetComponent<Collider2D>()!=null))throw new Exception("Portal has collision");
                foreach(var portal in portals)
                {
                    var viewport=camera.WorldToViewportPoint(portal.transform.position);
                    if(viewport.x<.05f||viewport.x>.95f||viewport.y<.07f||viewport.y>.9f)throw new Exception("Portal outside readable camera inset");
                }
            }
            else observedPair=false;
            if(stage==PhaseGatekeeperBossController.RouteStage.PortalWarning&&boss.PortalPairActive)throw new Exception("Portal activated during opening warning");
            if(PhaseGatekeeperBossController.ActivePhaseEncounter!=null && GameStateManager.Instance.CurrentState==GameState.BossBattle)
            {
                foreach(var enemy in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None))
                    if((bool)typeof(EnemyBaseAI).GetMethod("IsAmbientEnemyForBossEncounterIsolation",Flags).Invoke(enemy,null)&&(!enemy.IsBossEncounterIsolated||enemy.GetComponentsInChildren<Renderer>(true).Any(r=>!r.forceRenderingOff)))throw new Exception("Ambient enemy not isolated");
                if(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).Any(m=>!m.IsSectorEncounterPaused))throw new Exception("Meteor not paused");
            }
            if(boss.LensEscalationCount>1)throw new Exception("Duplicate lens escalation");
            string label=weaponLabel+"-phase"+(boss.LensEscalated?2:1)+"-"+stage;
            bool capture=stage==PhaseGatekeeperBossController.RouteStage.Entering?age>.035f:stage==PhaseGatekeeperBossController.RouteStage.Redirected?age>.025f:age>.2f;
            if(capture&&stage!=PhaseGatekeeperBossController.RouteStage.Stopped&&captured.Add(label))Capture(label);
            if(boss.PortalPairActive&&age>.2f&&captured.Add(weaponLabel+"-phase"+(boss.LensEscalated?2:1)+"-active-pair"))Capture(weaponLabel+"-phase"+(boss.LensEscalated?2:1)+"-active-pair");
            motion.Add(weaponLabel+","+Time.time.ToString("F4")+","+boss.RouteAttackCount+","+stage+","+boss.LensEscalated+","+boss.LensEscalationCount+","+count+","+boss.PortalPairActive+","+active+","+bossHealth.CurrentHp.ToString("F3")+","+(beam!=null&&beam.HasRedirected)+","+boss.IsExposed);
        }
        if(autoFire&&player!=null&&!bossHealth.IsDead&&boss.IsExposed)
        {
            Move(boss.transform.position+new Vector3(Mathf.Sin(Time.time)*.15f,-2.5f,0));
            Vector2 aim=camera.WorldToScreenPoint(boss.transform.position);var tree=player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            bool fire=tree==WeaponTreeType.MachineGun||(tree==WeaponTreeType.Sniper?Time.time%1.2f<.85f:Time.time%.65f<.12f);
            qaMouse.MakeCurrent();InputSystem.QueueStateEvent(qaMouse,new MouseState{position=aim}.WithButton(MouseButton.Left,fire));
            if(Time.time>nextInputDiagnostic)
            {
                nextInputDiagnostic=Time.time+10;
                var controller=player.GetComponent<PlayerController2D>();controller.TryGetAimWorldPosition(out var aimWorld);
                Note("FIRING "+weaponLabel+" HP="+bossHealth.CurrentHp+" player="+player.transform.position+" boss="+boss.transform.position+" collider="+boss.GetComponent<CapsuleCollider2D>().bounds+" aim="+controller.AimDirection+" pointerWorld="+aimWorld+" wantedScreen="+aim);
                Capture(weaponLabel+"-live-weapon-exposure");
            }
        }
        else Mouse(false);
    }
    static IEnumerable<float> Check()
    {
        motion.Add("weapon,time,attack,stage,escalated,escalationCount,portals,pairActive,beams,hp,redirected,exposed");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        bool interruption=SessionState.GetBool(PresentationKey,false);
        foreach(var tree in interruption?new[]{WeaponTreeType.Shotgun}:new[]{WeaponTreeType.MachineGun,WeaponTreeType.Shotgun,WeaponTreeType.Sniper})
        {
            weaponLabel=tree.ToString();autoFire=false;deaths=0;commitAttack=-1;observedPair=false;transferBeam=null;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            Require(PermanentProgress.Instance.IsDepthUnlocked(ExpeditionDepth.DeepZone2),"isolated Region C route authorized");
            Require(!PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.PhaseGatekeeper),"isolated first clear");
            RunManager.Instance.StartNewRun(tree,ExpeditionDepth.DeepZone2);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            var map=Object.FindFirstObjectByType<ExpeditionMapGenerator>();boss=map.CurrentRegion3BossEncounter;Require(boss!=null&&boss.UsesPhaseRoutes,"actual generated Phase Gatekeeper");
            bossHealth=boss.GetComponent<EnemyHealth>();bossHealth.Died+=_=>deaths++;
            Require(bossHealth.MaxHp==180,"existing single 180 HP authority");
            var meteor=Object.FindFirstObjectByType<MeteorObstacle>();if(meteor!=null)meteor.transform.position=boss.transform.position+Vector3.left*3;
            Move(boss.EncounterAnchor+Vector2.down*2.5f);yield return .25f;
            foreach(var d in Until(()=>boss.State!=PhaseGatekeeperBossController.EncounterState.Dormant,"normal proximity-triggered encounter",20))yield return d;
            foreach(var d in Until(()=>GameStateManager.Instance.CurrentState==GameState.BossBattle,"normal intro handoff"))yield return d;
            Require(player.GetComponent<PlayerWeaponController>().CurrentWeaponTree==tree,"actual equipped "+tree);
            if(meteor!=null)Require(meteor.IsSectorEncounterPaused&&meteor.IsSectorEncounterHidden,"in-arena meteor hidden and drift paused");
            Keys(Key.D);yield return .4f;Keys();
            if(interruption)
            {
                foreach(var d in Until(()=>boss.CurrentRouteStage==PhaseGatekeeperBossController.RouteStage.Committed,"committed direct precision for live dodge"))yield return d;
                float dodgeHp=player.CurrentHp;Vector2 dodgeStart=player.transform.position;
                player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);
                Keys(Key.D);yield return .55f;Keys();Capture("committed-precision-live-dodge");yield return .75f;
                Require(Vector2.Distance(dodgeStart,player.transform.position)>.4f,"actual lateral input moves after commitment");
                Require(player.CurrentHp==dodgeHp,"committed precision escaped without invulnerability");
                player.SetDashInvincible(true);
                foreach(var d in Until(()=>boss.IsExposed,"live Shotgun diagnostic exposure"))yield return d;
                autoFire=true;yield return 1.3f;Capture("Shotgun-live-input");
                foreach(var d in Until(()=>!boss.IsExposed,"diagnostic exposure closes"))yield return d;
                autoFire=false;Mouse(false);Note("Shotgun diagnostic remaining HP="+bossHealth.CurrentHp);
                foreach(var d in Until(()=>boss.CurrentRouteStage==PhaseGatekeeperBossController.RouteStage.Entering,"interrupt live portal transfer"))yield return d;
                float hp=bossHealth.CurrentHp;bool result=false;RunManager.Instance.RunEnded+=r=>result=r.endReason==RunEndReason.Death;
                player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);player.TakeDamage(99999);
                Require(bossHealth.CurrentHp==hp&&!bossHealth.IsDead,"player death does not fabricate boss victory");
                Require(boss.PortalEndpointCount==0&&boss.RouteBeam==null,"player death clears transfer and both portals");
                Require(PhaseGatekeeperBossController.ActivePhaseEncounter==null,"player death releases isolation");
                foreach(var d in Until(()=>result,"existing Death result"))yield return d;
                yield return 3;Capture("player-death-cleanup");var resultPanel=Object.FindFirstObjectByType<RunResultPanelUI>();Require(resultPanel!=null,"normal result panel");resultPanel.Close();
                boss=null;bossHealth=null;foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;Note("Player-death interruption returned normally.");yield break;
            }
            foreach(var d in Until(()=>boss.RouteAttackCount>=9,"first cycle and two escalated cycles",160))yield return d;
            Require(boss.LensEscalationCount==1,"exactly one cycle-driven lens escalation");
            Require(bossHealth.CurrentHp==180,"escalation does not alter health authority");
            autoFire=true;foreach(var d in Until(()=>bossHealth==null||bossHealth.IsDead,"actual "+tree+" weapon victory through exposure windows",900))yield return d;
            autoFire=false;Mouse(false);Keys();yield return .2f;
            Require(deaths==1,"single existing boss death event");Require(PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.PhaseGatekeeper),"existing Region C campaign reward authority");
            Require(PhaseGatekeeperBossController.ActivePhaseEncounter==null,"isolation owner released after death");
            Require(!Object.FindObjectsByType<PhaseCombatVfx>(FindObjectsSortMode.None).Any(v=>v.IsVisible),"no portal or marker survives death");
            Require(!Object.FindObjectsByType<BossLaserHazard>(FindObjectsSortMode.None).Any(v=>v.IsDamaging),"no beam survives death");
            Require(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).All(m=>!m.IsSectorEncounterPaused),"ordinary meteor state restored");
            Capture(weaponLabel+"-death");yield return 8;Capture(weaponLabel+"-reward");
            Note("COMPLETED "+tree+": actual generated coreless Region C -> proximity intro -> three-shot exposure -> one lens escalation -> exposure-window weapon victory -> existing death/reward. Invulnerability/assisted positioning; no direct boss damage fixture.");
            boss=null;bossHealth=null;SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        }
        Note("Three native 480x270 Region C fights complete. QA scene return; reward choice/portal traversal skipped. No user save or scene saved.");
    }
}
