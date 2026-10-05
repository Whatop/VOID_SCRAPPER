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
public static class SectorCoreProbe
{
    const string SessionKey = "SectorCore.Probe", PresentationKey = "SectorCore.Group";
    static string Dir => "Logs/SectorCoreRework/Native/" + SessionState.GetString(PresentationKey, "A") + "/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static Component boss;
    static EnemyHealth bossHealth;
    static bool autoFire;
    static float lastSample = -1;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing;
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static InputAction fixtureFire;
    static bool desiredFire, desiredDash;
    static KeyboardState keyboardState;
    static InputAction fixtureDash,fixtureMove;
    static Vector2 desiredAim=new Vector2(240,135);
    static InputSettings.UpdateMode previousUpdateMode;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
    static UnityEngine.LowLevel.PlayerLoopSystem previousLoop;
    static bool inputLoopInstalled;
    static void InstallInputLoop()
    {
        if(inputLoopInstalled)return;
        previousLoop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        var loop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        for(int i=0;i<loop.subSystemList.Length;i++)
        {
            if(loop.subSystemList[i].type!=typeof(UnityEngine.PlayerLoop.Update))continue;
            var old=loop.subSystemList[i].subSystemList;
            var updated=new UnityEngine.LowLevel.PlayerLoopSystem[old.Length+1];
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(SectorCoreProbe),updateDelegate=FeedGameFrame};
            System.Array.Copy(old,0,updated,1,old.Length);loop.subSystemList[i].subSystemList=updated;
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);inputLoopInstalled=true;
    }
    public static void FeedGameFrame()
    {
        if(finishing||qaMouse==null)return;
        if(autoFire && player!=null && !player.IsDead && bossHealth!=null && !bossHealth.IsDead) DriveCombat();
        else if(!walking) {desiredFire=false;desiredDash=false;}
        if(player!=null && fixtureFire!=null)
            typeof(PlayerWeaponController).GetField("fireAction",Flags).SetValue(player.GetComponent<PlayerWeaponController>(),fixtureFire);
        if(player!=null && fixtureDash!=null)
        {
            typeof(PlayerDash).GetField("dashAction",Flags).SetValue(player.GetComponent<PlayerDash>(),fixtureDash);
            typeof(PlayerController2D).GetField("moveAction",Flags).SetValue(player.GetComponent<PlayerController2D>(),fixtureMove);
        }
        qaKeyboard.MakeCurrent();InputSystem.QueueStateEvent(qaKeyboard,keyboardState);
        qaMouse.MakeCurrent();
        InputSystem.QueueStateEvent(qaMouse,new MouseState{position=desiredAim}.WithButton(MouseButton.Left,desiredFire).WithButton(MouseButton.Right,desiredDash));
        InputSystem.Update();
    }
    static SectorCoreProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs(); string group="Patterns";
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-acceptanceGroup")group=args[i+1];
        BeginRun(group);
    }
    static void BeginRun(string region)
    {
        string label=region; int revision=2; while(File.Exists("Logs/SectorCoreRework/Native/"+label+"/attempts.csv"))label=region+"-"+(revision++); SessionState.SetString(PresentationKey,label);
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
            previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            qaMouse = InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("SectorQAMouse");
            qaKeyboard = InputSystem.AddDevice<Keyboard>("SectorQAKeyboard");
            previousUpdateMode=InputSystem.settings.updateMode; InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 1200; EditorApplication.update += Tick;
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
            if(errors.Count>0) { Finish(); return; }
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
        finishing = true; EditorApplication.update -= Tick; if(inputLoopInstalled)UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(previousLoop); inputLoopInstalled=false; InputSystem.settings.updateMode=previousUpdateMode;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorBehavior; fixtureFire?.Dispose(); fixtureFire=null;fixtureDash?.Dispose();fixtureMove?.Dispose();fixtureDash=fixtureMove=null;
        if (qaMouse != null) InputSystem.RemoveDevice(qaMouse);
        if (qaKeyboard != null) InputSystem.RemoveDevice(qaKeyboard);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        Flush(); EditorApplication.ExitPlaymode();
    }
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "combat-timing.csv", motion); File.WriteAllLines(Dir+"attempts.csv",results); File.WriteAllLines(Dir+"hits.csv",hits); }
    static void Note(string value) { log.Add(DateTime.UtcNow.ToString("HH:mm:ss") + " " + value); Flush(); }
    static void Require(bool value, string message) { if (!value) throw new Exception(message);  }
    static IEnumerable<float> Until(Func<bool> check, string label, float seconds = 80)
    { double deadline = EditorApplication.timeSinceStartup + seconds; while (!check()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception(label); yield return .02f; } Note("REACHED " + label); }
    static IEnumerable<float> Scene(string name)
    { foreach (var d in Until(() => SceneManager.GetActiveScene().name == name && !SceneFlowManager.Instance.IsLoading, name)) yield return d; yield return 3; BindCamera(); }
    static void BindCamera() { camera = Camera.main; }
    static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir + "Rendered/" + name + ".png"));
        File.WriteAllLines(Dir + "Rendered/" + name + "-text.txt", Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text)).Select(t => t.name + " overflow=" + t.isTextOverflowing + " " + t.text.Replace('\n','|')));
        File.WriteAllLines(Dir + "Rendered/" + name + "-missile-renderers.txt", Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None)
            .Where(b=>sector!=null&&b.SourceRoot==sector.transform).SelectMany(b=>b.GetComponentsInChildren<SpriteRenderer>(true)).Select(r=>r.name+" color="+r.color+" enabled="+r.enabled+" off="+r.forceRenderingOff+" lossy="+r.transform.lossyScale+" bounds="+r.bounds+" mat="+r.sharedMaterial.name+" shader="+r.sharedMaterial.shader.name+" sprite="+AssetDatabase.GetAssetPath(r.sprite)));
        Note("CAPTURE actual unmodified GameView " + name + " " + Screen.width + "x" + Screen.height);
    }
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n) {for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,Flags);if(f!=null)return f.GetValue(o);}return null;}
    static void Keys(params Key[] k)=>keyboardState=new KeyboardState(k);
    static readonly List<string> results=new List<string>{"attempt,boss,weapon,outcome,duration,startHp,endHp,hits,bossHp,furthestState"};
    static readonly List<string> hits=new List<string>{"attempt,time,hp,state,source"};
    static string caseLabel,kind,lastState,furthest;
    static int hitCount;
    static float fightStart,startHp,nextDecision,nextSample,lastCapture,dashAt;
    static bool observing,walking,inCombat;
    static WeaponTreeType weaponTree;
    static BossPatternController sector;
    static FrigateTriadBossController triad;
    static PhaseGatekeeperBossController phase;
    static PirateCommanderBossController assault;
    static RaiderSalvageCarrierBossController carrier;
    static RaiderSniperCommanderBossController sniper;
    static NullDispatcherBossController dispatcher;
    static FrigateBossPart[] parts;
    static Transform aimTarget;
    static int attemptSerial;
    static void Ready()
    {
        Keys(); player=Object.FindFirstObjectByType<PlayerHealth>();Require(player!=null,"authored player");
        player.GetComponent<PlayerController2D>().InputActions.devices=new InputDevice[]{qaMouse,qaKeyboard};
        var wc=player.GetComponent<PlayerWeaponController>();
        fixtureFire?.Dispose();fixtureFire=((InputAction)Get(wc,"fireAction")).Clone();fixtureFire.Enable();
        fixtureDash?.Dispose();fixtureMove?.Dispose();
        fixtureDash=((InputAction)Get(player.GetComponent<PlayerDash>(),"dashAction")).Clone();fixtureDash.Enable();
        fixtureMove=((InputAction)Get(player.GetComponent<PlayerController2D>(),"moveAction")).Clone();fixtureMove.Enable();
        InstallInputLoop();
    }
    static void SetupMove(Vector3 p)
    {
        Require(!inCombat,"No combat teleport permitted");
        var rb=player.GetComponent<Rigidbody2D>();rb.position=p;player.transform.position=p;rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        Note("PRECOMBAT navigation position="+p);
    }
    static string State()
    {
        if(sector!=null)return sector.CurrentSectorStage+"-spokes"+sector.SectorSpokeCount+"-p2"+sector.SectorEscalated+"-shield"+Get(sector,"phase2ShieldActive");
        if(triad!=null)return triad.CurrentDefenseStage+"-parts"+triad.AlivePartCount+"-"+triad.State;
        if(phase!=null)return phase.CurrentRouteStage+"-lens"+phase.LensEscalated+"-cycle"+(phase.RouteAttackCount/3);
        if(assault!=null)return assault.Stage+"-hot"+assault.IsWeaponsHot+"-p2"+assault.IsPhase2;
        if(carrier!=null)return carrier.Stage+"-cargo"+carrier.Cargo+"-p2"+carrier.IsPhase2;
        if(sniper!=null)return sniper.Stage+"-mines"+sniper.LiveMineCount+"-p2"+sniper.IsPhase2;
        if(dispatcher!=null)return dispatcher.Phase+"-pattern"+Get(dispatcher,"lastPattern")+"-damage"+dispatcher.PatternDamageEnabled;
        return "none";
    }
    static void Damage(float hp,float maximum)
    {
        if(!observing)return;hitCount++;
        string stack=new System.Diagnostics.StackTrace().ToString().Replace('\n','|').Replace('\r',' ');
        hits.Add(caseLabel+","+(Time.time-fightStart).ToString("F3")+","+hp+","+State()+",\""+stack.Replace("\"","'")+"\"");
        Note("HIT "+caseLabel+" t="+(Time.time-fightStart).ToString("F3")+" HP="+hp+" "+State());
    }
    static Bounds PlayBounds()
    {
        if(sector!=null)return new Bounds(sector.SectorArenaCenter,sector.SectorArenaHalfExtents*2);
        if(phase!=null)return phase.RouteArenaBounds;
        if(assault!=null)return assault.ArenaBounds;
        if(carrier!=null)return carrier.ArenaBounds;
        if(sniper!=null)return sniper.ArenaBounds;
        return new Bounds(camera.transform.position,new Vector3(camera.orthographicSize*camera.aspect*2,camera.orthographicSize*2,0));
    }
    static void ChooseTarget()
    {
        aimTarget=boss!=null?boss.transform:null;
        if(triad!=null&&parts!=null)
        {float best=float.MaxValue;foreach(var part in parts)if(part!=null&&part.IsAlive){float d=Vector2.Distance(player.transform.position,part.transform.position);if(d<best){best=d;aimTarget=part.transform;}}}
    }
    // Disposable test driver only: decisions every 120ms, normal movement/fire/Dash actions.
    // Reads actor positions and published visible state; never writes combat transforms or health.
    static bool OriginPass => SessionState.GetString(PresentationKey,"Patterns").StartsWith("Origin");
    static bool BoundsPass => SessionState.GetString(PresentationKey,"Patterns").StartsWith("Bounds");
    static bool SinglePass => BoundsPass || OriginPass || SessionState.GetString(PresentationKey,"Patterns").StartsWith("Diagnosis");
    static void DriveCombat()
    {
        desiredFire = desiredDash = false;
        if(sector == null || player == null || player.IsDead) { Keys(); return; }
        desiredAim = camera.WorldToScreenPoint(sector.transform.position);
        var stage = sector.CurrentSectorStage;
        float age = Time.time - fightStart;
        if (kind == "Sweep")
        {
            // Read commitment, then leave the fixed arc through ordinary movement.
            if(stage == BossPatternController.SectorStage.SweepAim) Keys();
            else if(stage == BossPatternController.SectorStage.SweepCommit || stage == BossPatternController.SectorStage.SweepForward || stage == BossPatternController.SectorStage.SweepRecovery || stage == BossPatternController.SectorStage.SweepReverse)
            { if(player.transform.position.x < sector.transform.position.x + 4.8f) Keys(Key.D); else Keys(); }
            else Keys();
        }
        else if(kind == "Missiles")
        {
            if(OriginPass && age < 1.03f) Keys();
            else if(OriginPass && age < 1.65f) Keys(Key.D);
            else if(age < 1.05f) Keys(Key.D);
            else if(age < 1.8f) Keys(Key.W);
            else if(age < 2.5f) Keys(Key.A);
            else Keys();
        }
        else if(kind == "Rectangles")
        {
            var area = (SectorRectangularAoE)Get(sector,"sectorRectangles");
            if(area != null && (area.ExplodedMask & 1) != 0)
            {
                float destination = area.transform.position.y - area.InnerHalfExtents.y - area.BandWidth * .5f;
                float delta = destination-player.transform.position.y;
                if(Mathf.Abs(delta) > .12f) Keys(delta > 0 ? Key.W : Key.S); else Keys();
            }
            else Keys();
        }
        else Keys();
    }
    static void SetMove(Vector2 d)
    {if(Mathf.Abs(d.x)<.32f)Keys(d.y>0?Key.W:Key.S);else if(Mathf.Abs(d.y)<.32f)Keys(d.x>0?Key.D:Key.A);else Keys(d.x>0?Key.D:Key.A,d.y>0?Key.W:Key.S);}
    static BossPatternController.SectorStage priorStage;
    static float stageBorn;
    static void Shot(string suffix)
    { string name=caseLabel+"-"+suffix; if(captured.Add(name)) Capture(name); }
    static void SampleCombat()
    {
        if(!observing || sector==null || player==null) return;
        var stage=sector.CurrentSectorStage;
        if(stage!=priorStage) {priorStage=stage;stageBorn=Time.time;Note("STAGE "+caseLabel+" "+stage+" shots="+sector.SectorShotsThisCycle+" hp="+player.CurrentHp);}
        float age=Time.time-stageBorn;
        if(Time.time>=nextSample)
        {
            nextSample=Time.time+.04f;
            var bullets=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b=>b.SourceRoot==sector.transform).ToArray();
            var area=(SectorRectangularAoE)Get(sector,"sectorRectangles");
            if(area!=null) Require(Vector2.Distance(area.transform.position,sector.transform.position)<.001f,"rectangle center stays on stopped boss root");
            motion.Add(caseLabel+","+Time.time.ToString("F4")+","+stage+","+sector.SectorShotsThisCycle+","+sector.SectorActiveMissiles+","+sector.SectorSpokeCount+","+sector.SectorZoneCount+","+player.CurrentHp+","+player.transform.position.x+","+player.transform.position.y+","+sector.SectorCommittedAim.x+","+sector.SectorCommittedAim.y+","+bullets.Length+","+player.IsInvincible+","+player.MinimumHealthFloor);
            foreach(var bullet in bullets) missileLog.Add(caseLabel+","+Time.time.ToString("F4")+","+bullet.GetInstanceID()+","+bullet.transform.position.x+","+bullet.transform.position.y+","+bullet.MoveDirection.x+","+bullet.MoveDirection.y);
            if(kind=="Rectangles"&&area!=null&&captured.Add(caseLabel+"-geometry"))
                Note("GEOMETRY center="+area.transform.position+" innerHalf="+area.InnerHalfExtents+" width="+area.BandWidth+" count="+area.BandCount+" player collider="+player.GetComponent<Collider2D>().bounds);
        }
        switch(stage)
        {
            case BossPatternController.SectorStage.SweepAim: if(age>.1f)Shot("aim"); break;
            case BossPatternController.SectorStage.SweepCommit: Shot("commit"); break;
            case BossPatternController.SectorStage.SweepForward: Shot(sector.SectorShotsThisCycle<6?"forward-early":"forward-late"); break;
            case BossPatternController.SectorStage.SweepRecovery: Shot("inter-sweep");break;
            case BossPatternController.SectorStage.SweepReverse: Shot("reverse");break;
            case BossPatternController.SectorStage.MissileLaunch: Shot("launch-"+sector.SectorShotsThisCycle);break;
            case BossPatternController.SectorStage.MissileFlight: Shot("six-pursuit");if(age>.25f)Shot("six-separated");if(age>.55f)Shot("turn-away");if(age>1.4f)Shot("overshoot");break;
            case BossPatternController.SectorStage.RectWarningA: Shot("A-warning");if(age>1.15f)Shot("A-charged");break;
            case BossPatternController.SectorStage.RectExplosionA: Shot("A-explosion");break;
            case BossPatternController.SectorStage.RectWarningB: if(age<.16f)Shot("A-release-B-warning");if(age>.25f)Shot("B-warning");if(age>.7f)Shot("move-into-A");break;
            case BossPatternController.SectorStage.RectExplosionB: Shot("B-explosion");break;
            case BossPatternController.SectorStage.Recovery: Shot("cleanup");break;
        }
    }
    static readonly List<string> missileLog=new List<string>{"case,time,id,x,y,dx,dy"};
    static IEnumerable<float> Dialogue()
    {
        double limit=EditorApplication.timeSinceStartup+70;
        while(DialogueManager.isConversationActive)
        {
            Require(EditorApplication.timeSinceStartup<limit,"Dialogue completion");
            var ui=DialogueManager.dialogueUI as StandardDialogueUI;
            if(ui!=null){var choices=ui.GetComponentsInChildren<StandardUIResponseButton>(true).Where(x=>x.gameObject.activeInHierarchy&&x.response!=null).ToArray();
            if(choices.Length>0)choices[choices.Length-1].OnClick();else ui.OnContinue();}
            yield return .4f;
        }
    }
    static void Set(object o,string f,object value)=>o.GetType().GetField(f,Flags).SetValue(o,value);
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    static void StopOwner()
    {
        var handle=(Coroutine)Get(sector,"patternRoutine"); if(handle!=null) sector.StopCoroutine(handle);
        Set(sector,"patternRoutine",null); Call(sector,"ClearSectorAttacks"); Set(sector,"casting",true); Call(sector,"StopMoving");
    }
    static IEnumerable<float> Check()
    {
        motion.Add("case,time,state,shots,missiles,spokes,bands,hp,x,y,aimX,aimY,bullets,invincible,floor");
        Note("Isolated generated Region A encounters. QA pre-attack position and scheduler slot selection only. No health/armor/value changes, no invulnerability, no attack-time teleport, no player firing. Actual WASD movement. Not human difficulty acceptance.");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        foreach(string pattern in BoundsPass ? new[]{"Rectangles"} : SinglePass ? new[]{"Missiles"} : new[]{"Sweep","Missiles","Rectangles"})
        {
            kind=pattern;weaponTree=WeaponTreeType.MachineGun;observing=autoFire=inCombat=false;Keys();
            sector=null;boss=null;bossHealth=null;captured.Clear();
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            UnityEngine.Random.InitState(72404);
            RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();
            var core=Object.FindFirstObjectByType<CoreObject>();SetupMove(core.transform.position+Vector3.down*2);
            Require(core.TryStartBossEncounterForDevelopment(out string reason),"Core fixture start "+reason);
            foreach(var d in Until(()=>Object.FindFirstObjectByType<BossPatternController>()!=null,"generated Region A"))yield return d;
            sector=Object.FindFirstObjectByType<BossPatternController>();boss=sector;bossHealth=sector.GetComponent<EnemyHealth>();
            foreach(var d in Until(()=>(bool)Get(sector,"initialized"),"intro and HUD handoff"))yield return d;
            StopOwner();yield return .4f;
            for(int repetition=0;repetition<(SinglePass?1:3)&&!player.IsDead;repetition++)
            {
                StopOwner();autoFire=observing=inCombat=false;Keys();
                caseLabel=kind+"-"+repetition;
                float y=kind=="Rectangles"?((Vector2)sector.GetComponent<Collider2D>().bounds.extents).y+Mathf.Abs(sector.GetComponent<Collider2D>().bounds.center.y-sector.transform.position.y)+.6f+1.8f:OriginPass?3f:5f;
                SetupMove(sector.transform.position+Vector3.down*y);yield return .4f;
                hitCount=0;fightStart=Time.time;startHp=player.CurrentHp;player.Damaged+=Damage;priorStage=BossPatternController.SectorStage.Stopped;
                int slot=kind=="Sweep"?0:kind=="Missiles"?1:2;Set(sector,"sectorCycle",slot);
                inCombat=observing=autoFire=true;
                var handle=sector.StartCoroutine((System.Collections.IEnumerator)Call(sector,"SectorControlCycleRoutine",false));Set(sector,"patternRoutine",handle);
                yield return .12f;
                foreach(var d in Until(()=>player.IsDead||sector.CurrentSectorStage==BossPatternController.SectorStage.Recovery,"standalone completion",20))yield return d;
                yield return .3f;
                results.Add(caseLabel+",A,MG,"+(player.IsDead?"death":"survived")+","+(Time.time-fightStart)+","+startHp+","+player.CurrentHp+","+hitCount+","+bossHealth.CurrentHp+","+sector.CurrentSectorStage);
                Note("RESULT "+results[results.Count-1]);
                player.Damaged-=Damage;observing=autoFire=inCombat=false;Keys();StopOwner();yield return .3f;
                int total=Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                Note("POOL after cycle "+repetition+" totalBulletObjects="+total+" rectangles="+Object.FindObjectsByType<SectorRectangularAoE>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" activeMissiles="+sector.SectorActiveMissiles+" activeBands="+sector.SectorZoneCount);
            }
            File.WriteAllLines(Dir+"projectiles.csv",missileLog);
            if(!player.IsDead)
            {
                caseLabel=kind+"-interruption";
                int slot=kind=="Sweep"?0:kind=="Missiles"?1:2;Set(sector,"sectorCycle",slot);
                Set(sector,"patternRoutine",sector.StartCoroutine((System.Collections.IEnumerator)Call(sector,"SectorControlCycleRoutine",false)));
                yield return kind=="Missiles"?1.05f:.3f;
                Call(sector,"CancelCombat");yield return .15f;Capture(caseLabel+"-abort-cleanup");yield return .2f;
                Require(sector.SectorActiveMissiles+sector.SectorSpokeCount+sector.SectorZoneCount==0,"abort cleanup");
            }
            if(player.IsDead) { yield return 6; Object.FindFirstObjectByType<RunResultPanelUI>().Close(); }
            else SceneFlowManager.Instance.LoadSettlement();
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        }
        if(!SinglePass)
            foreach(var d in Integration())yield return d;
    }
    static IEnumerable<float> Integration()
    {
        Note("SEPARATE ASSISTED REGRESSION: generated encounter, QA invulnerability and scripted boss/shield damage to inspect normal scheduler, 4->6, purple, death/reward. No difficulty acceptance.");
        kind="Integration";caseLabel="Integration";autoFire=observing=inCombat=false;Keys();sector=null;boss=null;bossHealth=null;captured.Clear();
        PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
        foreach(var d in Scene("Expedition"))yield return d;Ready();player.SetDashInvincible(true);
        var core=Object.FindFirstObjectByType<CoreObject>();SetupMove(core.transform.position+Vector3.down*2);
        Require(core.TryStartBossEncounterForDevelopment(out string reason),"assisted Core start "+reason);
        foreach(var d in Until(()=>Object.FindFirstObjectByType<BossPatternController>()!=null,"assisted boss spawn"))yield return d;
        sector=Object.FindFirstObjectByType<BossPatternController>();boss=sector;bossHealth=sector.GetComponent<EnemyHealth>();
        Capture("Integration-spawn");yield return .3f;
        foreach(var d in Until(()=>(bool)Get(sector,"initialized"),"assisted intro handoff"))yield return d;
        observing=true;fightStart=Time.time;
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.Partition&&sector.SectorSpokeCount==4,"normal four-slot scheduler reaches green rotation",35))yield return d;
        Capture("Integration-green-four");yield return .2f;bossHealth.TakeDamage(80);
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.EscalationWarning,"escalation warning",35))yield return d;
        Capture("Integration-growth-start");yield return .4f;
        Require(((SectorPartitionLane[])Get(sector,"sectorLanes")).All(l=>l!=null&&!l.IsDamaging),"all new and original spokes harmless during growth");
        Capture("Integration-growth-mid");yield return .6f;Capture("Integration-growth-full-hold");yield return .2f;
        foreach(var d in Until(()=>(bool)Get(sector,"phase2ShieldDamageEnabled")&&sector.CurrentSectorStage==BossPatternController.SectorStage.Partition,"six-spoke green resumes"))yield return d;
        Capture("Integration-six-green");yield return .3f;bossHealth.TakeDamage(999);
        foreach(var d in Until(()=>(bool)Get(sector,"phase2"),"true phase two shield break",25))yield return d;
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.CounterRotation,"purple counter window",60))yield return d;
        float green=sector.SectorAngle,purple=sector.SectorCounterAngle;Capture("Integration-purple-counter");yield return .25f;
        Require(sector.SectorAngle>green&&sector.SectorCounterAngle<purple,"same green direction and opposite purple direction");
        Require(sector.SectorActiveMissiles==0&&sector.SectorZoneCount==0,"no core attack overlaps counter rotation");
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.RectWarningA,"phase two rectangles bounded by actual hexagon",45))yield return d;
        var area=(SectorRectangularAoE)Get(sector,"sectorRectangles");
        Note("PHASE TWO GEOMETRY center="+area.transform.position+" inner="+area.InnerHalfExtents+" width="+area.BandWidth+" count="+area.BandCount);
        Vector2 outer=area.InnerHalfExtents+Vector2.one*area.BandWidth*area.BandCount;
        for(int i=0;i<4;i++){var corner=new Vector2((i%2==0?-1:1)*outer.x,(i<2?-1:1)*outer.y);Require(corner.magnitude+.19f<=sector.SectorBeamLength(area.transform.position,corner.normalized),"hex corner inside arena "+i);}
        Capture("Integration-hex-rectangle-warning");yield return .3f;
        Vector3 deathPosition=sector.transform.position;Transform source=sector.transform;bossHealth.TakeDamage(9999);
        Capture("Integration-death");yield return .15f;
        Require(sector.SectorZoneCount+sector.SectorActiveMissiles+sector.SectorSpokeCount==0,"death immediately clears all core and laser attack leases");
        Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b=>b.SourceRoot==source),"death releases source projectiles");
        Note("DEATH early nearby credits="+Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Where(p=>p.CurrencyType==CurrencyType.Credits&&Vector2.Distance(p.transform.position,deathPosition)<3).Sum(p=>p.Amount));
        observing=false;yield return 2;
        Capture("Integration-death-settled");Note("DEATH settled nearby credits="+Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Where(p=>p.CurrencyType==CurrencyType.Credits&&Vector2.Distance(p.transform.position,deathPosition)<3).Sum(p=>p.Amount));
        yield return 6;
        var capsule=Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).First(c=>c.PayloadType==RewardCapsulePayloadType.BossReward);
        SetupMove(capsule.transform.position+Vector3.down*.4f);foreach(var d in Until(()=>capsule.CanInteract(player.gameObject),"main reward capsule available"))yield return d;
        Capture("Integration-reward");yield return .2f;capsule.Interact(player.gameObject);yield return .5f;
        var choices=Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();foreach(var d in Until(()=>choices.IsShowing,"boss reward choice opens after capsule animation",15))yield return d;Capture("Integration-reward-choice");yield return .2f;
        ((RunTraitChoiceButtonUI[])Get(choices,"choiceButtons"))[0].HandleButtonClicked();
        foreach(var d in Until(()=>Object.FindFirstObjectByType<ReturnBeacon>()!=null&&Object.FindFirstObjectByType<ReturnBeacon>().PresentationReady,"Beacon ready after reward",25))yield return d;
        Capture("Integration-Beacon-ready");yield return .3f;
        Require(PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator),"unchanged campaign defeat authority");
        player.SetDashInvincible(false);SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
    }

}
