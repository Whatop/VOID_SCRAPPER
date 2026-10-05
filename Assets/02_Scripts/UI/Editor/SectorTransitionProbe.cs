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
public static class SectorTransitionProbe
{
    const string SessionKey = "SectorTransition.Probe", PresentationKey = "SectorTransition.Group";
    static string Dir => "Logs/SectorTransition/Native/" + SessionState.GetString(PresentationKey, "A") + "/";
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
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(SectorTransitionProbe),updateDelegate=FeedGameFrame};
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
    static SectorTransitionProbe()
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
        string label=region; int revision=2; while(File.Exists("Logs/SectorTransition/Native/"+label+"/attempts.csv"))label=region+"-"+(revision++); SessionState.SetString(PresentationKey,label);
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
            SampleIntro(); SampleTransition();
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
    static int lastCaptureFrame = -1;
    static void Capture(string name)
    {
        lastCaptureFrame = Time.frameCount;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir + "Rendered/" + name + ".png"));
        File.WriteAllLines(Dir + "Rendered/" + name + "-text.txt", Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text)).Select(t => t.name + " overflow=" + t.isTextOverflowing + " " + t.text.Replace('\n','|')));
        File.WriteAllLines(Dir + "Rendered/" + name + "-missile-renderers.txt", Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None)
            .Where(b=>sector!=null&&b.SourceRoot==sector.transform).SelectMany(b=>b.GetComponentsInChildren<SpriteRenderer>(true)).Select(r=>r.name+" color="+r.color+" enabled="+r.enabled+" off="+r.forceRenderingOff+" lossy="+r.transform.lossyScale+" bounds="+r.bounds+" mat="+r.sharedMaterial.name+" shader="+r.sharedMaterial.shader.name+" sprite="+AssetDatabase.GetAssetPath(r.sprite)));
        if(sector!=null)File.WriteAllLines(Dir+"Rendered/"+name+"-laser.txt",((SectorPartitionLane[])Get(sector,"sectorLanes")).Where(l=>l!=null).Select(l=>
        {var r=(SpriteRenderer)Get(l,"visual");var e=(SpriteRenderer)Get(l,"purpleEmitter");var box=l.GetComponent<BoxCollider2D>();return "sprite="+AssetDatabase.GetAssetPath(r.sprite)+" frame="+r.sprite.name+" size="+r.size+" scale="+r.transform.localScale+" color="+r.color+" material="+r.sharedMaterial.name+" solid="+((LineRenderer)Get(l,"solidVisual")).enabled+" collider="+box.enabled+" size="+box.size+" offset="+box.offset+" emitter="+e.enabled;}));
        if(sector!=null)File.WriteAllLines(Dir+"Rendered/"+name+"-relays.txt",Object.FindObjectsByType<SectorRelayPresentation>(FindObjectsSortMode.None).Select(r=>
        {var b=(SpriteRenderer)Get(r,"body");return "id="+r.GetInstanceID()+" slot="+r.GetComponent<LaserGuardianDrone>().RuntimeIndex+" position="+r.transform.position+" viewport="+camera.WorldToViewportPoint(r.transform.position)+" purple="+r.IsPurple+" visible="+r.IsBodyVisible+" color="+b.color+" sprite="+b.sprite.name;}));
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
    static int impactCount;
    static float lastContactTime;
    static void ContactActivity()
    {
        if(!observing || player==null)return;
        float hit=player.GetComponent<PlayerCombatState>().LastHitTime;
        if(hit<=lastContactTime)return;lastContactTime=hit;impactCount++;
        Note("CONTACT including armor/shield "+caseLabel+" t="+(Time.time-fightStart).ToString("F3")+" count="+impactCount+" stack="+new System.Diagnostics.StackTrace().ToString().Replace('\n','|'));
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
        if(sector==null || player==null || player.IsDead) {Keys();return;}
        desiredAim=camera.WorldToScreenPoint(sector.transform.position);
        float age=Time.time-fightStart;
        if(kind.StartsWith("Missiles") && sector.CurrentSectorStage!=BossPatternController.SectorStage.MissileFlight){Keys();return;}
        if(kind.StartsWith("Missiles") && player.transform.position.y>sector.SectorRectangleArenaBounds.yMax-1f){Keys(Key.A);return;}
        if(kind.StartsWith("Missiles") && player.transform.position.x>sector.SectorRectangleArenaBounds.xMax-1f){Keys(Key.W);return;}
        if(kind=="MissilesStraight") Keys(Key.D);
        else if(kind=="MissilesGentle") {if(age<1.5f)Keys(Key.D);else Keys(Key.D,Key.W);}
        else if(kind=="MissilesHard") {if(age<1.0f)Keys(Key.D);else Keys(Key.W);}
        else if(kind=="MissilesImpact") {if(impactCount==0)Keys();else Keys(Key.D);}
        else if(kind=="Rectangles")
        {
            var area=(SectorRectangularAoE)Get(sector,"sectorRectangles");
            if(area!=null && (area.ExplodedMask&1)!=0 && player.transform.position.y>area.transform.position.y-area.OuterHalfExtents.y+.6f)Keys(Key.S);else Keys();
        }
        else if((kind=="SweepMove" || kind=="Sweep") || kind=="Direct")
        {
            bool committed=sector.CurrentSectorStage!=BossPatternController.SectorStage.SweepAim || kind=="Direct" && sector.SectorDirectBurstStage!=BossPatternController.SectorBurstStage.Aim;
            if(SessionState.GetString(PresentationKey,"").StartsWith("SweepEscape") && sector.SectorEscalated && kind=="SweepMove")
            {if(committed)Keys(sector.SectorSweepRepeat%2==1?Key.D:Key.A);else Keys();}
            else if(committed && player.transform.position.x<sector.transform.position.x+5)Keys(Key.D);else Keys();
        }
        else if(kind=="GreenLaser" || kind=="PurpleLaser")
        {
            Vector2 radial=(Vector2)player.transform.position-(Vector2)sector.transform.position;
            if(float.IsNaN(laserGapOffset))
            {
                float relative=Mathf.Atan2(radial.y,radial.x)*Mathf.Rad2Deg-sector.SectorAngle;
                float slot=sector.SectorEmpowered?60:90;
                laserGapOffset=Mathf.Floor(relative/slot)*slot+slot*.5f;
            }
            float angle=(sector.SectorAngle+laserGapOffset)*Mathf.Deg2Rad;
            float radius=sector.SectorDirectBurstStage==BossPatternController.SectorBurstStage.Fire?5.7f:5f;
            Vector2 destination=(Vector2)sector.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            Vector2 delta=destination-(Vector2)player.transform.position;
            if(delta.magnitude>.18f)SetMove(delta.normalized);else Keys();
        }
        else Keys();
    }
    static void SetMove(Vector2 d)
    {if(Mathf.Abs(d.x)<.32f)Keys(d.y>0?Key.W:Key.S);else if(Mathf.Abs(d.y)<.32f)Keys(d.x>0?Key.D:Key.A);else Keys(d.x>0?Key.D:Key.A,d.y>0?Key.W:Key.S);}
    static BossPatternController.SectorStage priorStage;
    static float stageBorn;
    static void Shot(string suffix)
    { if(Time.frameCount == lastCaptureFrame)return; string name=caseLabel+"-"+suffix; if(captured.Add(name)) Capture(name); }
    static int introBossId; static float formationStarted=-1; static bool introFinished;
    static void SampleIntro()
    {
        if(sector==null)return;
        if(introBossId!=sector.GetInstanceID()){introBossId=sector.GetInstanceID();formationStarted=-1;introFinished=false;}
        if((bool)Get(sector,"initialized"))
        {
            if(!introFinished&&formationStarted>=0&&captured.Add(caseLabel+"-first-combat")){Capture(caseLabel+"-first-combat");Note("FIRST COMBAT formation="+sector.SectorBarrierProgress);}
            introFinished=true;return;
        }
        if(introFinished)return;
        var marker=(SectorSupportVfx)Get(sector,"sectorArrivalMarker");
        var body=(SpriteRenderer)Get(sector,"sectorBody");
        if(marker!=null && marker.Visual.enabled)
        {
            string name="arrival-"+marker.Visual.sprite.name+"-body"+(body.forceRenderingOff?"hidden":body.color.a.ToString("F1"));
            if(captured.Add(caseLabel+name)){Capture(caseLabel+name);Note("MARKER BEFORE/OVERLAP bodyOff="+body.forceRenderingOff+" alpha="+body.color.a+" barrier="+sector.SectorBarrierProgress);}
        }

        var walls=(List<GameObject>)Get(sector,"boundaryLaserWalls");
        string key=caseLabel+"-barrier-";
        float p=sector.SectorBarrierProgress;
        if(walls.Count>0&&formationStarted<0)formationStarted=Time.unscaledTime;
        if(walls.Count>0&&p==1&&formationStarted>=0&&Time.unscaledTime-formationStarted>.76f&&captured.Add(key+"stabilized"))Capture(key+"stabilized");
        string frame=walls.Count==0?"arrival-before-formation":p<=0?"emitter-activation":p<.3f?null:p<.6f?"30-percent":p<1?"60-percent":"100-percent";
        if(frame!=null && captured.Add(key+frame))
        {
            Note("BARRIER "+frame+" progress="+p+" walls="+walls.Count+" contacts="+walls.Count(w=>w!=null&&w.GetComponent<Collider2D>().enabled)+" player="+player.transform.position+" initialized="+Get(sector,"initialized")+" unscaled="+Time.unscaledTime.ToString("F4"));
            Capture(key+frame);
        }
    }
    static void SampleCombat()
    {
        if(!observing || sector==null || player==null)return;
        var stage=sector.CurrentSectorStage;
        if(stage!=priorStage){priorStage=stage;stageBorn=Time.time;Note("STAGE "+caseLabel+" "+stage+" repeat="+sector.SectorSweepRepeat+" shots="+sector.SectorShotsThisCycle+" hp="+player.CurrentHp);}
        float age=Time.time-stageBorn;
        var burst=sector.SectorDirectBurstStage;
        if(burst!=priorBurst)
        {
            if(burst==BossPatternController.SectorBurstStage.Aim)burstOrdinal++;
            Note("BURST "+caseLabel+" ordinal="+burstOrdinal+" stage="+burst+" t="+(Time.time-fightStart).ToString("F3")+" shots="+sector.SectorDirectBurstShots+" laser="+Get(sector,"sectorRotating"));priorBurst=burst;
        }
        if(burst!=BossPatternController.SectorBurstStage.Idle)Shot("Burst"+burstOrdinal+"-"+burst);
        if(Time.time>=nextSample)
        {
            nextSample=Time.time+.02f;
            var bullets=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b=>b.SourceRoot==sector.transform).ToArray();
            motion.Add(caseLabel+","+Time.time.ToString("F4")+","+stage+","+sector.SectorShotsThisCycle+","+sector.SectorActiveMissiles+","+sector.SectorSpokeCount+","+sector.SectorZoneCount+","+player.CurrentHp+","+player.transform.position.x+","+player.transform.position.y+","+sector.SectorCommittedAim.x+","+sector.SectorCommittedAim.y+","+bullets.Length+","+player.IsInvincible+","+player.MinimumHealthFloor);
            foreach(var bullet in bullets)missileLog.Add(caseLabel+","+Time.time.ToString("F4")+","+bullet.GetInstanceID()+","+bullet.transform.position.x+","+bullet.transform.position.y+","+bullet.MoveDirection.x+","+bullet.MoveDirection.y+","+Get(bullet,"homingTimeRemaining")+","+Get(bullet,"useHoming")+","+bullet.GetComponent<Collider2D>().enabled+","+player.transform.position.x+","+player.transform.position.y);
            if(sector.SectorSpokeCount>0 && (bool)Get(sector,"sectorRotating"))
            {
                Require(sector.SectorActiveMissiles==0 && sector.SectorZoneCount==0,"laser cannot overlap missile/AoE");
                Require(stage!=BossPatternController.SectorStage.SweepForward,"laser cannot overlap Sweep");
            }
            if(sector.SectorEscalated)Require(sector.SectorZoneCount==0,"no Phase 2 AoE");
        }
        switch(stage)
        {
            case BossPatternController.SectorStage.SweepAim: if(age>.05f)Shot("Sweep"+sector.SectorSweepRepeat+"-anticipation");break;
            case BossPatternController.SectorStage.SweepCommit:Shot("Sweep"+sector.SectorSweepRepeat+"-commit");break;
            case BossPatternController.SectorStage.SweepForward:Shot("Sweep"+sector.SectorSweepRepeat+"-pair"+((sector.SectorShotsThisCycle-1)%12/2+1));break;
            case BossPatternController.SectorStage.SweepRecovery:Shot("Sweep"+sector.SectorSweepRepeat+"-gap");break;
            case BossPatternController.SectorStage.MissileWarning:if(age>.4f)Shot("core-preflash");break;
            case BossPatternController.SectorStage.MissileLaunch:Shot("core-ejection");break;
            case BossPatternController.SectorStage.MissileFlight:Shot("six-emerging");if(age>.12f)Shot("six-deployment");if(age>.27f)Shot("guidance-start");if(age>.6f)Shot("live-pursuit");if(age>.9f)Shot("curved-tracking");if(kind=="MissilesHard"&&age>.35f)Shot("hard-turn");if(age>1.65f)Shot("overshoot");break;
            case BossPatternController.SectorStage.RectWarningA:Shot("A-warning");break;
            case BossPatternController.SectorStage.RectExplosionA:Shot("A-release");break;
            case BossPatternController.SectorStage.RectWarningB:Shot("B-warning");break;
            case BossPatternController.SectorStage.RectExplosionB:Shot("B-release");break;
            case BossPatternController.SectorStage.Warning:Shot("laser-warning");break;
            case BossPatternController.SectorStage.Partition:Shot("laser-active-emitter");if(age>.2f)Shot("laser-active-pixels");if(age>.55f)Shot("laser-rotation");if(age>1.3f)Shot("laser-adjustment");if(age>2.3f)Shot("laser-late");break;
            case BossPatternController.SectorStage.Recovery:Shot("cleanup");break;
        }
    }
    static BossPatternController.SectorBurstStage priorBurst;
    static int burstOrdinal;
    static bool isolatedFinished;
    static float laserGapOffset=float.NaN;
    static System.Collections.IEnumerator Isolated(string pattern, bool empowered)
    {
        isolatedFinished=false;
        string method=pattern.StartsWith("Missiles")?"SectorMissileRoutine":pattern.StartsWith("Sweep")?"SectorSweepRoutine":pattern=="Rectangles"?"AlternatingRectangularAoERoutine":pattern=="Direct"?"SectorDirectBurstRoutine":"SectorRotationRoutine";
        yield return (System.Collections.IEnumerator)(pattern=="Direct"?Call(sector,method,empowered,false):pattern.EndsWith("Laser")?Call(sector,method,empowered):Call(sector,method));
        if(pattern=="Direct")yield return new WaitForSeconds(2);
        Note("PATTERN COMPLETE "+caseLabel+" shots="+sector.SectorShotsThisCycle+" sweepRepeats="+sector.SectorSweepRepeat+" bursts="+burstOrdinal);
        Call(sector,"ClearSectorAttacks");Bullet.ReleaseAllActiveFromSource(sector.transform);Call(sector,"SetSectorStage",BossPatternController.SectorStage.Recovery);isolatedFinished=true;
    }
    static IEnumerable<float> SpawnEncounter()
    {
        sector=null;boss=null;bossHealth=null;captured.Clear();observing=autoFire=inCombat=false;Keys();
        PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
        UnityEngine.Random.InitState(72404);RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
        foreach(var d in Scene("Expedition"))yield return d;Ready();
        var core=Object.FindFirstObjectByType<CoreObject>();SetupMove(core.transform.position+Vector3.down*2);
        Require(core.TryStartBossEncounterForDevelopment(out string reason),"Core fixture start "+reason);
        foreach(var d in Until(()=>Object.FindFirstObjectByType<BossPatternController>()!=null,"production Region A"))yield return d;
        sector=Object.FindFirstObjectByType<BossPatternController>();boss=sector;bossHealth=sector.GetComponent<EnemyHealth>();
        foreach(var d in Until(()=>(bool)Get(sector,"initialized"),"intro and HUD handoff"))yield return d;
        StopOwner();yield return .4f;
        Require(!sector.SectorEmpowered,"repeat encounter resets green");
    }
    static IEnumerable<float> LeaveEncounter()
    {
        autoFire=observing=inCombat=false;Keys();
        if(sector!=null && !player.IsDead){Call(sector,"CancelCombat");yield return .25f;}
        if(player.IsDead){yield return 6;Object.FindFirstObjectByType<RunResultPanelUI>().Close();}
        else SceneFlowManager.Instance.LoadSettlement();
        foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
    }
    static IEnumerable<float> Attempt(string pattern, bool empowered, int repetition, bool normalHealth=true)
    {
        StopOwner();autoFire=observing=inCombat=false;Keys();kind=pattern;caseLabel=(normalHealth?"":"Assisted-")+(empowered?"Phase2-":"Phase1-")+pattern+"-"+repetition;laserGapOffset=float.NaN;
        Vector2 offset=pattern=="Rectangles"?Vector2.down*(sector.SectorRectangleArenaBounds.height*.5f-1.8f):pattern.StartsWith("Missiles")?new Vector2(-3,-3):Vector2.down*4;
        SetupMove((Vector2)sector.transform.position+offset);yield return .5f;
        player.SetDashInvincible(!normalHealth);Require(player.MinimumHealthFloor==0,"attempt has no health floor");
        if(normalHealth)Require(!player.IsInvincible,"normal-health attempt starts without invulnerability");
        hitCount=impactCount=0;lastContactTime=player.GetComponent<PlayerCombatState>().LastHitTime;fightStart=Time.time;startHp=player.CurrentHp;burstOrdinal=0;priorBurst=BossPatternController.SectorBurstStage.Idle;priorStage=BossPatternController.SectorStage.Stopped;Set(sector,"SectorShotsThisCycle",0);
        var armor=player.GetComponent<PlayerArmor>();var shield=player.GetComponent<ComponentShieldPassive>();
        Note("DEFENSES "+caseLabel+" armor="+(armor!=null?armor.CurrentArmor:0)+" shield="+(shield!=null && shield.enabled && shield.IsCharged));
        player.GetComponent<PlayerCombatState>().CombatActivityRegistered+=ContactActivity;
        player.Damaged+=Damage;observing=autoFire=inCombat=true;
        Set(sector,"patternRoutine",sector.StartCoroutine(Isolated(pattern,empowered)));
        foreach(var d in Until(()=>player.IsDead||isolatedFinished,"isolated "+pattern+" completion",20))yield return d;
        yield return .15f;Capture(caseLabel+"-cleanup");yield return .15f;
        results.Add(caseLabel+",A,MG,"+(player.IsDead?"death":"survived")+","+(Time.time-fightStart)+","+startHp+","+player.CurrentHp+","+hitCount+","+bossHealth.CurrentHp+","+sector.CurrentSectorStage);Note("RESULT "+results[results.Count-1]);
        Note("CONTACT SUMMARY "+caseLabel+" contacts="+impactCount+" healthHits="+hitCount+" armorEnd="+(armor!=null?armor.CurrentArmor:0)+" shieldEnd="+(shield!=null && shield.enabled && shield.IsCharged));
        player.GetComponent<PlayerCombatState>().CombatActivityRegistered-=ContactActivity;
        player.Damaged-=Damage;autoFire=observing=inCombat=false;Keys();StopOwner();
        Note("POOL "+caseLabel+" bullets="+Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" lanes="+Object.FindObjectsByType<SectorPartitionLane>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" rectangles="+Object.FindObjectsByType<SectorRectangularAoE>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        File.WriteAllLines(Dir+"projectiles.csv",missileLog);
    }
    static readonly List<string> missileLog=new List<string>{"case,time,id,x,y,dx,dy,guidanceRemaining,homing,colliderEnabled,playerX,playerY"};
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
    static void Set(object o,string f,object value)=>(o.GetType().GetField(f,Flags)??o.GetType().GetField("<"+f+">k__BackingField",Flags)).SetValue(o,value);
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    static void StopOwner()
    {
        var handle=(Coroutine)Get(sector,"patternRoutine"); if(handle!=null) sector.StopCoroutine(handle);
        Set(sector,"patternRoutine",null); Call(sector,"ClearSectorAttacks"); Set(sector,"casting",true); Call(sector,"StopMoving");
    }
    static IEnumerable<float> IntroInterruptionCheck()
    {
        foreach(string mode in new[]{"death","abort","repeat"})
        {
            sector=null;boss=null;bossHealth=null;captured.Clear();caseLabel="Intro-"+mode;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();
            var core=Object.FindFirstObjectByType<CoreObject>();SetupMove(core.transform.position+Vector3.down*2);
            Require(core.TryStartBossEncounterForDevelopment(out string reason),"intro interruption setup "+reason);
            foreach(var d in Until(()=>Object.FindFirstObjectByType<BossPatternController>()!=null,"intro boss"))yield return d;
            sector=Object.FindFirstObjectByType<BossPatternController>();boss=sector;bossHealth=sector.GetComponent<EnemyHealth>();
            foreach(var d in Until(()=>sector.SectorSpawnMarkerVisible,"arrival marker active"))yield return d;
            Vector3 held=player.transform.position;Keys(Key.D);walking=true;yield return .12f;Keys();walking=false;
            Require(Vector2.Distance(held,player.transform.position)<.01f,"intro lock prevents movement while collision is incomplete");
            Note("INPUT LOCK movement attempted; displacement="+Vector2.Distance(held,player.transform.position));
            if(mode=="repeat")
            {
                foreach(var d in Until(()=>(bool)Get(sector,"initialized"),"repeat combat handoff"))yield return d;
                Require(sector.SectorBarrierProgress==1,"formation complete before combat");
                Require(((List<GameObject>)Get(sector,"boundaryLaserWalls")).All(w=>w.GetComponent<Collider2D>().enabled),"all containment authoritative before combat");
                Capture("Intro-repeat-first-combat");yield return .2f;Call(sector,"CancelCombat");
            }
            else
            {
                var intro=Object.FindFirstObjectByType<CoreBossIntroSequence>();
                if(mode=="death")
                {
                    // Force only the interruption condition; ordinary intro invincibility is otherwise preserved.
                    Set(player,"invincibleTimer",0f);player.SetDashInvincible(false);player.SetMinimumHealthFloor(0);player.TakeDamage(9999);
                }
                else Call(intro,"CancelSectorIntroPresentation");
                yield return .2f;
                Require(!intro.IsPlaying,"intro canceled");Require(!sector.SectorSpawnMarkerVisible,"arrival marker clears on interruption");
                Require(!(bool)Get(sector,"initialized"),"interruption never starts scheduler");
                Require(((List<GameObject>)Get(sector,"boundaryLaserWalls")).Count==0,"interruption removes containment authority");
                Require(!Object.FindObjectsByType<SectorBarrierVisual>(FindObjectsSortMode.None).Any(),"pooled formation visuals released");
                Capture("Intro-"+mode+"-cleanup");yield return .7f;
                Require(sector.SectorBarrierProgress==0,"formation state stays reset after cancellation");
            }
            if(mode=="death")
            {
                yield return 6;
                var result=Object.FindFirstObjectByType<RunResultPanelUI>();
                Require(result!=null,"normal death result UI");result.Close();
            }
            else SceneFlowManager.Instance.LoadSettlement();
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        }
    }
    static IEnumerable<float> Check()
    {
        motion.Add("case,time,state,shots,missiles,spokes,bands,hp,x,y,aimX,aimY,bullets,invincible,floor");
        Note("Native 480x270. Threshold/transition probes assisted; Sweep attempts normal health, WASD only. No human difficulty acceptance.");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        string group=SessionState.GetString(PresentationKey, "");
        if(group.StartsWith("Sweep") || group.StartsWith("Final"))
        {
            foreach(string pattern in group.StartsWith("SweepEscape")?new[]{"SweepMove"}:new[]{"SweepStand","SweepMove"})
            {
                kind=pattern;caseLabel=pattern+"-intro";foreach(var d in SpawnEncounter())yield return d;
                foreach(var d in Attempt(pattern,false,0))yield return d;
                // Three-repeat geometry uses the same production flag without introducing transition assistance into the health test.
                if(!player.IsDead){Set(sector,"phase2",true);Call(sector,"SetSectorEnergy",1f);foreach(var d in Attempt("SweepMove",true,pattern=="SweepStand"?1:2))yield return d;}
                foreach(var d in LeaveEncounter())yield return d;
            }
            if(!group.StartsWith("Final")) yield break;
        }
        foreach(string pattern in new[]{"Rectangles","Missiles","Sweep","Direct","GreenLaser","SweepAbort","SweepDeath"})
        {
            kind=pattern;caseLabel="Threshold-"+pattern;foreach(var d in SpawnEncounter())yield return d;
            player.SetDashInvincible(true);SetupMove(sector.transform.position+Vector3.down*4);yield return .3f;
            var originals=((List<LaserGuardianDrone>)Get(sector,"phase1ManagerShips")).Select(x=>x.GetInstanceID()).ToArray();
            Require(originals.Length==4 && sector.SectorPurpleRelayCount==0,"four green production relays");
            Set(sector,"sectorCycle",pattern=="Rectangles"?2:pattern=="Missiles"||pattern=="Direct"?1:pattern=="GreenLaser"?3:0);
            Set(sector,"startDelay",0f);Set(sector,"patternRoutine",sector.StartCoroutine((System.Collections.IEnumerator)Call(sector,"PatternLoopRoutine")));
            observing=true;fightStart=Time.time;priorStage=BossPatternController.SectorStage.Stopped;burstOrdinal=0;
            foreach(var d in Until(()=>pattern=="Rectangles"?sector.CurrentSectorStage==BossPatternController.SectorStage.RectWarningA:
                pattern=="Missiles"?sector.CurrentSectorStage==BossPatternController.SectorStage.MissileFlight:
                pattern.StartsWith("Sweep")?sector.CurrentSectorStage==BossPatternController.SectorStage.SweepForward:
                pattern=="Direct"?sector.SectorDirectBurstStage==BossPatternController.SectorBurstStage.Fire:
                sector.CurrentSectorStage==BossPatternController.SectorStage.Partition,"selected attack before threshold"))yield return d;
            int cycle=sector.SectorCycle;var stage=sector.CurrentSectorStage;int before=sector.SectorShotsThisCycle;
            Capture(caseLabel+"-before-threshold");yield return .04f;
            bossHealth.TakeDamage(80);float thresholdTime=Time.time;yield return .03f;
            Require(sector.SectorPhase2Pending&&!sector.SectorTransitionRunning,"pending request without interruption");
            Require(sector.SectorCycle==cycle,"no next Phase 1 slot");
            Capture(caseLabel+"-pending-continues");yield return .08f;
            Require(!sector.SectorTransitionRunning,"current attack still owns execution");
            foreach(var d in Until(()=>sector.SectorTransitionRunning,"normal cleanup and recovery precede transition",20))yield return d;
            Require(sector.SectorCycle==cycle,"no extra Phase 1 attack before transition");
            if(pattern=="SweepAbort" || pattern=="SweepDeath")
            {
                foreach(var d in Until(()=>sector.SectorTransitionState==BossPatternController.SectorTransitionStage.Deploy,"abort during relay deploy",5))yield return d;
                observing=false;
                if(pattern=="SweepDeath") {Set(player,"invincibleTimer",0f);player.SetDashInvincible(false);player.SetMinimumHealthFloor(0);player.TakeDamage(9999);}
                else Call(sector,"CancelCombat");
                yield return .2f;
                Require(!sector.SectorPhase2Pending&&!sector.SectorTransitionRunning&&sector.SectorRelayCount==0,"interruption clears transition and relays");
                Require(!Object.FindObjectsByType<SectorRelayPresentation>(FindObjectsSortMode.None).Any(),"interruption returns all relay leases");
                Capture(caseLabel+"-cleanup");yield return .2f;
                foreach(var d in LeaveEncounter())yield return d;continue;
            }

            Require(sector.SectorZoneCount+sector.SectorActiveMissiles==0,"normal attack leases finished");
            Note("THRESHOLD COMPLETE "+pattern+" delay="+(Time.time-thresholdTime)+" cycle="+cycle+" shots="+sector.SectorShotsThisCycle);
            foreach(var d in Until(()=>sector.SectorTransitionState==BossPatternController.SectorTransitionStage.Complete,"transition completes",10))yield return d;
            Require(sector.SectorTransitionCount==1&&!sector.SectorPhase2Pending,"transition once and pending consumed");
            Require(sector.SectorRelayCount==6&&sector.SectorPurpleRelayCount==6,"exactly six purple relays");
            Require(((List<LaserGuardianDrone>)Get(sector,"phase1ManagerShips")).Select(x=>x.GetInstanceID()).SequenceEqual(originals),"original four relay instances preserved");
            Require(Get(sector,"phase2ShieldCombatRoutine")==null,"only original scheduler owns Phase 2");
            Note("TRANSITION DURATION "+pattern+" "+sector.SectorTransitionElapsed);
            Capture(caseLabel+"-settled");yield return .1f;
            foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.Partition,"six reverse beams active",8))yield return d;
            float angle=sector.SectorAngle;yield return .3f;Require(sector.SectorAngle<angle,"purple reverses");
            if(pattern=="GreenLaser")
            {
                yield return 2.2f;Require(burstOrdinal==2,"approved two laser support Bursts");
                // Existing shield authority releases HP floor without cancelling the active attack.
                bossHealth.TakeDamage(999);yield return .1f;Require((bool)Get(sector,"phase2"),"existing protection authority clears");
                Capture(caseLabel+"-existing-protection-cleared");yield return .1f;
            }
            observing=false;Call(sector,"CancelCombat");
            Require(!sector.SectorPhase2Pending&&!sector.SectorTransitionRunning&&sector.SectorRelayCount==0,"abort clears pending and relays");
            yield return .2f;Require(!Object.FindObjectsByType<SectorRelayPresentation>(FindObjectsSortMode.None).Any(),"all relay leases returned");
            foreach(var d in LeaveEncounter())yield return d;
        }
        foreach(var d in IntroInterruptionCheck())yield return d;
        Note("COMPLETED all five threshold boundaries, production arrivals, relay reset, reverse laser and approved Burst combination");
    }
    static void SampleTransition()
    {
        if(sector==null || !sector.SectorTransitionRunning)return;
        var stage=sector.SectorTransitionState;
        int frame=Mathf.FloorToInt(sector.SectorTransitionElapsed/.1f);
        Shot("transition-"+stage+"-"+frame);
        var walls=((List<GameObject>)Get(sector,"boundaryLaserWalls"));
        Require(walls.All(w=>w==null||!w.GetComponent<Collider2D>().enabled),"transition boundary collision is harmless");
        Require(((SectorPartitionLane[])Get(sector,"sectorLanes")).All(l=>l==null||!l.IsDamaging),"transition spoke collision harmless");
        Note("TRANSITION SAMPLE "+caseLabel+" "+stage+" t="+sector.SectorTransitionElapsed+" relays="+sector.SectorRelayCount+" purple="+sector.SectorPurpleRelayCount+" cameraSize="+camera.orthographicSize);
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
        StopOwner(); yield return .4f;
        foreach(var d in ArenaAudit("Phase1"))yield return d;
        var ar=sector.SectorRectangleArenaBounds;Note("MEASURED PLAYABLE inner-wall bounds="+ar+" center="+sector.SectorArenaCenter);
        var areaRoutine=(System.Collections.IEnumerator)Call(sector,"AlternatingRectangularAoERoutine");areaRoutine.MoveNext();
        var area=(SectorRectangularAoE)Get(sector,"sectorRectangles");
        for(int ring=0;ring<=area.BandCount;ring++)Note("RING BOUNDARY "+ring+" "+area.RingBoundary(ring));
        int sampled=0;for(int x=0;x<=100;x++)for(int y=0;y<=100;y++)
        {int ring=area.RingIndex(new Vector2(Mathf.Lerp(ar.xMin,ar.xMax,x/100f),Mathf.Lerp(ar.yMin,ar.yMax,y/100f)));Require(ring>=0,"whole live arena classified");sampled++;}
        Note("LIVE ARENA CLASSIFICATION passed "+sampled+" samples including corners/edges/center");
        Capture("Arena-center-A-warning");yield return .2f;
        SetupMove(new Vector2(ar.xMin+.6f,ar.yMin+.6f));yield return .7f;Capture("Arena-corner-outer-boundary");yield return .2f;
        // Diagnostic overview only; ordinary combat uses the unchanged owner framing.
        var framing=(Behaviour)Get(sector,"phase2GungeonCamera");
        
        bool enabled=framing.enabled;framing.enabled=false;var oldPosition=camera.transform.position;float oldSize=camera.orthographicSize;
        camera.transform.position=new Vector3(ar.center.x,ar.center.y,oldPosition.z);camera.orthographicSize=13.1f;
        Capture("Arena-full-coverage-diagnostic-overview");yield return .3f;
        camera.transform.position=oldPosition;camera.orthographicSize=oldSize;framing.enabled=enabled;StopOwner();SetupMove(sector.transform.position+Vector3.down*4);yield return .5f;
        Set(sector,"sectorCycle",3); Set(sector,"patternRoutine",sector.StartCoroutine((System.Collections.IEnumerator)Call(sector,"SectorControlCycleRoutine",false)));
        observing=true;fightStart=Time.time;
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.Partition&&sector.SectorSpokeCount==4,"normal four-slot scheduler reaches green rotation",35))yield return d;
        Capture("Integration-green-four");yield return .2f;bossHealth.TakeDamage(80);
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.EscalationWarning,"escalation warning",35))yield return d;
        Capture("Integration-growth-start");yield return .2f; Capture("Integration-green-purple-fade");yield return .2f;
        Require(((SectorPartitionLane[])Get(sector,"sectorLanes")).All(l=>l!=null&&!l.IsDamaging),"all new and original spokes harmless during growth");
        Capture("Integration-growth-mid");yield return .6f;Capture("Integration-growth-full-hold");yield return .2f;
        foreach(var d in Until(()=>(bool)Get(sector,"phase2ShieldDamageEnabled")&&sector.CurrentSectorStage==BossPatternController.SectorStage.Partition,"six-spoke purple reverse begins"))yield return d;
        Capture("Integration-six-purple");yield return .3f;bossHealth.TakeDamage(999);
        foreach(var d in Until(()=>(bool)Get(sector,"phase2"),"true phase two shield break",25))yield return d;
        StopOwner(); yield return .4f;
        foreach(var d in ArenaAudit("Phase2"))yield return d;
        if(SessionState.GetString(PresentationKey,"").StartsWith("Limits"))
        { Call(sector,"CancelCombat");player.SetDashInvincible(false);SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;yield break; }

        foreach(string pattern in new[]{"Sweep","Direct","PurpleLaser"})
        {
            if(player.IsDead)break;
            foreach(var d in Attempt(pattern,true,0))yield return d;
        }
        Require(!player.IsDead,"Phase 2 attempts survive for assisted reward regression");
        Note("SEPARATE ASSISTED repetition: invulnerability for pooling and pixel presentation; excluded from normal-health hit/difficulty evidence.");
        for(int i=1;i<=2;i++)foreach(var d in Attempt("PurpleLaser",true,i,false))yield return d;
        player.SetDashInvincible(true);kind="Integration";caseLabel="Integration";
        observing=autoFire=inCombat=false;StopOwner();
        Set(sector,"sectorCycle",2);Set(sector,"patternRoutine",sector.StartCoroutine((System.Collections.IEnumerator)Call(sector,"SectorControlCycleRoutine",true)));
        observing=true;fightStart=Time.time;burstOrdinal=0;
        foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.Partition,"Phase2 former AoE slot is now rotating laser",15))yield return d;
        float purpleAngle=sector.SectorAngle;yield return .3f;Require(sector.SectorAngle<purpleAngle,"Phase2 rotation reverses");
        Capture("Integration-purple-screen-edge");yield return 3;
        Require(sector.SectorZoneCount==0,"Phase2 scheduler no AoE");StopOwner();
        // Edge-distance cleanup check: navigation setup is outside the normal-health attempts.
        observing=false;SetupMove(sector.SectorArenaCenter+Vector2.down*11f);yield return .8f;
        Capture("Integration-death-edge-before");yield return .15f;
        float cameraBeforeDeath=camera.orthographicSize;
        Vector3 deathPosition=sector.transform.position;Transform source=sector.transform;bossHealth.TakeDamage(9999);
        Note("CAMERA cleanup before="+cameraBeforeDeath+" synchronousAfter="+camera.orthographicSize);
        Require(camera.orthographicSize>4.3f,"Region A widened camera does not snap synchronously to base on death");
        Capture("Integration-death");yield return .15f;
        Require(sector.SectorZoneCount+sector.SectorActiveMissiles+sector.SectorSpokeCount==0,"death immediately clears all core and laser attack leases");
        Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b=>b.SourceRoot==source),"death releases source projectiles");
        Note("DEATH early nearby credits="+Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Where(p=>p.CurrencyType==CurrencyType.Credits&&Vector2.Distance(p.transform.position,deathPosition)<3).Sum(p=>p.Amount));
        observing=false;yield return 2;
        Capture("Integration-death-settled");Note("CAMERA settled after death="+camera.orthographicSize);Note("DEATH settled nearby credits="+Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Where(p=>p.CurrencyType==CurrencyType.Credits&&Vector2.Distance(p.transform.position,deathPosition)<3).Sum(p=>p.Amount));
        yield return 6;
        var capsule=Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).First(c=>c.PayloadType==RewardCapsulePayloadType.BossReward);
        SetupMove(capsule.transform.position+Vector3.down*.4f);foreach(var d in Until(()=>capsule.CanInteract(player.gameObject),"main reward capsule available"))yield return d;
        Capture("Integration-reward");yield return .2f;capsule.Interact(player.gameObject);yield return .5f;
        var choices=Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();foreach(var d in Until(()=>choices.IsShowing,"boss reward choice opens after capsule animation",15))yield return d;Capture("Integration-reward-choice");yield return .2f;
        ((RunTraitChoiceButtonUI[])Get(choices,"choiceButtons"))[0].HandleButtonClicked();
        foreach(var d in Until(()=>Object.FindFirstObjectByType<ReturnBeacon>()!=null&&Object.FindFirstObjectByType<ReturnBeacon>().PresentationReady,"Beacon ready after reward",25))yield return d;
        Capture("Integration-Beacon-ready");yield return .3f;
        Require(PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SectorAdministrator),"unchanged campaign defeat authority");
        Note("REWARD collected at boss death position="+deathPosition+" BEACON="+Object.FindFirstObjectByType<ReturnBeacon>().transform.position);

        player.SetDashInvincible(false);SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        Note("REPEAT ENCOUNTER: fresh Region A must restore green after the completed purple encounter.");
        caseLabel="RepeatEncounter";kind="GreenLaser";foreach(var d in SpawnEncounter())yield return d;
        Require(!sector.SectorEmpowered && sector.SectorSignedAngularSpeed>0,"repeat encounter identity is green and forward");
        foreach(var d in Attempt("GreenLaser",false,0,false))yield return d;
        foreach(var d in LeaveEncounter())yield return d;
    }
    static IEnumerable<float> ArenaAudit(string label)
    {
        bool wasObserving=observing; observing=false;
        Note("ARENA "+label+" center="+sector.SectorArenaCenter.ToString("F5")+" half="+sector.SectorArenaHalfExtents+" boss="+sector.transform.position.ToString("F5")+" collider="+player.GetComponent<Collider2D>().bounds.ToString("F5"));
        foreach(var wall in (List<GameObject>)Get(sector,"boundaryLaserWalls"))
        {
            if(wall==null||!wall.activeInHierarchy)continue;var box=wall.GetComponent<BoxCollider2D>();
            var a=box.transform.TransformPoint(box.offset+Vector2.left*box.size.x*.5f);var b=box.transform.TransformPoint(box.offset+Vector2.right*box.size.x*.5f);
            Note("WALL "+label+" "+wall.name+" a="+a.ToString("F5")+" b="+b.ToString("F5")+" size="+box.size.ToString("F5")+" lossy="+box.transform.lossyScale+" trigger="+box.isTrigger);
        }
        var origin=(Vector2)sector.transform.position;
        foreach(var pair in new[]{("left",Vector2.left),("right",Vector2.right),("top",Vector2.up),("bottom",Vector2.down)})
        {
            var hitBuffer=new RaycastHit2D[64];
            float radius=player.GetComponent<CircleCollider2D>().bounds.extents.x;
            int hitCount=Physics2D.CircleCastNonAlloc(sector.SectorArenaCenter+pair.Item2*3,radius,pair.Item2,hitBuffer,30,~0);
            float legal=float.PositiveInfinity; Collider2D limiting=null;
            for(int h=0;h<hitCount;h++)
            {
                var hit=hitBuffer[h];if(hit.collider==null||hit.collider.isTrigger)continue;
                if(hit.collider.GetComponent<BossArenaLaserWall>()==null&&hit.collider.GetComponent<LaserGuardianDrone>()==null)continue;
                if(hit.distance<legal){legal=hit.distance;limiting=hit.collider;}
            }
            Note("PHYSICAL AXIS LIMIT "+label+" "+pair.Item1+" radius="+radius.ToString("F6")+" distanceFromArenaCenter="+(legal+3).ToString("F6")+" collider="+(limiting!=null?limiting.name:"missing")+" bounds="+(limiting!=null?limiting.bounds.ToString("F5"):"none")+" scale="+(limiting!=null?limiting.transform.lossyScale.ToString("F5"):"none"));
            float length=sector.SectorBeamLength(sector.SectorArenaCenter,pair.Item2);
            SetupMove(sector.SectorArenaCenter+pair.Item2*(length-.8f));yield return 1;
            var body=(SpriteRenderer)Get(sector,"sectorBody");var vmin=camera.WorldToViewportPoint(body.bounds.min);var vmax=camera.WorldToViewportPoint(body.bounds.max);
            Note("CAMERA "+label+" "+pair.Item1+" player="+player.transform.position.ToString("F4")+" center="+camera.transform.position.ToString("F4")+" size="+camera.orthographicSize+" aspect="+camera.aspect+" bossViewport="+vmin.ToString("F4")+" .. "+vmax.ToString("F4"));
            Capture(label+"-edge-"+pair.Item1);yield return .2f;
        }
        SetupMove(origin+Vector2.down*4);yield return 1;
        Capture(label+"-boss-centered");yield return .2f;
        Note("Arena empty of ordinary actors="+Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None).Count(e=>e.gameObject!=sector.gameObject&&Vector2.Distance(e.transform.position,sector.SectorArenaCenter)<8));
        observing=wasObserving;
    }
    static IEnumerable<float> ChargerAudit()
    {
        observing=autoFire=inCombat=false;Keys();
        Note("Separate ordinary charger fixture: authored definition, real player movement, no firing/invulnerability for this check.");
        player.SetDashInvincible(false);
        yield return .2f;
        var definition=AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset");
        var root=Object.Instantiate(definition.EnemyPrefab,player.transform.position+Vector3.up*3,Quaternion.identity);
        var ai=root.GetComponent<EnemyBaseAI>();ai.ApplyDefinition(definition);ai.SetTarget(player.transform);Set(ai,"currentState",EnemyState.Combat);root.GetComponent<EnemyVisionSensor>().ForceDetectTarget(player.transform);
        var attack=root.GetComponent<EnemyAttackController>();Set(attack,"attackTimer",0f);
        Require(attack.TryAttack(player.transform),"ordinary charger begins");
        walking=true;Keys(Key.D);yield return .35f;Capture("Charger-anticipation-current-position");yield return .25f;
        Note("CHARGER predictive="+Get(attack,"usePredictiveAimForCurrentAttack")+" current="+attack.CurrentChargeDirection);
        foreach(var d in Until(()=>(bool)Get(attack,"chargeAimCommitted"),"charger commits",3))yield return d;
        Vector2 committed=attack.CurrentChargeDirection;Capture("Charger-commit");yield return .13f;
        Keys(Key.A);yield return .15f;
        Require(Vector2.Distance(committed,attack.CurrentChargeDirection)<.001f,"charger does not reacquire after direction change");
        foreach(var d in Until(()=>!attack.IsCharging,"charged shot release",3))yield return d;
        Capture("Charger-release-after-turn");yield return .25f;Keys();walking=false;
        Note("CHARGER result HP="+player.CurrentHp+" committed="+committed+" released="+attack.CurrentChargeDirection);
        attack.CancelCharge();Bullet.ReleaseAllActiveFromSource(root.transform);Object.Destroy(root);
    }

}
