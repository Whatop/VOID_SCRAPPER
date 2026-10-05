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
public static class BossAcceptanceProbe
{
    const string SessionKey = "BossAcceptance.Probe", PresentationKey = "BossAcceptance.Group";
    static string Dir => "Logs/BossAcceptance/" + SessionState.GetString(PresentationKey, "A") + "/";
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
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(BossAcceptanceProbe),updateDelegate=FeedGameFrame};
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
    static BossAcceptanceProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs(); string group="All";
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-acceptanceGroup")group=args[i+1];
        BeginRun(group);
    }
    static void BeginRun(string region)
    {
        string label=region; int revision=2; while(File.Exists("Logs/BossAcceptance/"+label+"/attempts.csv"))label=region+"-"+(revision++); SessionState.SetString(PresentationKey,label);
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
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 14400; EditorApplication.update += Tick;
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
    static void DriveCombat()
    {
        if(DialogueManager.isConversationActive){Keys();desiredFire=desiredDash=false;return;}
        ChooseTarget();if(aimTarget==null)return;
        desiredAim=camera.WorldToScreenPoint(aimTarget.position);
        float age=Time.time-fightStart;
        float cycle=age%2.0f;
        desiredFire=weaponTree==WeaponTreeType.MachineGun || (weaponTree==WeaponTreeType.Shotgun?age%.9f<.2f:cycle<1.25f);
        desiredDash=Time.time-dashAt<.1f;
        if(Time.time<nextDecision)return;nextDecision=Time.time+.12f;
        Vector2 p=player.transform.position, b=aimTarget.position,delta=p-b;
        float distance=delta.magnitude;Vector2 radial=distance>.01f?delta/distance:Vector2.down;
        float preferred=weaponTree==WeaponTreeType.Shotgun?2.2f:3.2f;
        // Counter-clockwise orbit uses real WASD and leaves committed target lines.
        Vector2 tangent=new Vector2(-radial.y,radial.x);
        Vector2 move=tangent*.7f+radial*Mathf.Clamp((preferred-distance)*1.5f,-1.4f,1.4f);
        Bounds bounds=PlayBounds();
        if(bounds.size.x>1)
        {
            if(p.x<bounds.min.x+.9f)move.x=Mathf.Max(.8f,move.x);
            if(p.x>bounds.max.x-.9f)move.x=Mathf.Min(-.8f,move.x);
            if(p.y<bounds.min.y+.9f)move.y=Mathf.Max(.8f,move.y);
            if(p.y>bounds.max.y-.9f)move.y=Mathf.Min(-.8f,move.y);
        }
        // Sniper's authored movement cancellation requires a deliberate charge/step rhythm.
        if(weaponTree==WeaponTreeType.Sniper && cycle<1.32f)move=Vector2.zero;
        if(move.sqrMagnitude>.01f)SetMove(move);else Keys();
        if(weaponTree!=WeaponTreeType.Sniper && age>2 && Time.time-dashAt>3.4f)dashAt=Time.time;
    }
    static void SetMove(Vector2 d)
    {if(Mathf.Abs(d.x)<.32f)Keys(d.y>0?Key.W:Key.S);else if(Mathf.Abs(d.y)<.32f)Keys(d.x>0?Key.D:Key.A);else Keys(d.x>0?Key.D:Key.A,d.y>0?Key.W:Key.S);}
    static void SampleCombat()
    {
        if(!observing||player==null||bossHealth==null||Time.time<nextSample)return;nextSample=Time.time+.1f;
        string s=State();float age=Time.time-fightStart;
        if(s!=lastState){lastState=s;furthest=s;Note("STATE "+caseLabel+" "+age.ToString("F3")+" "+s);}
        Vector3 p=player.transform.position,b=boss.transform.position;
        int bullets=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(x=>x.SourceRoot==boss.transform);
        motion.Add(caseLabel+","+age.ToString("F3")+","+s+","+player.CurrentHp+","+bossHealth.CurrentHp+","+p.x+","+p.y+","+b.x+","+b.y+","+camera.orthographicSize+","+bullets+","+player.IsInvincible+","+player.MinimumHealthFloor);
        if(age>3 && captured.Add(caseLabel+"-pressure"))Capture(caseLabel+"-pressure");
        bool escalated=sector!=null?sector.SectorEscalated:triad!=null?triad.AlivePartCount<3:phase!=null?phase.LensEscalated:assault!=null?assault.IsPhase2:carrier!=null?carrier.IsPhase2:sniper!=null?sniper.IsPhase2:dispatcher!=null&&dispatcher.PolarityCombatActive;
        if(escalated&&captured.Add(caseLabel+"-escalated"))Capture(caseLabel+"-escalated");
        if(hitCount>0&&Time.time-lastCapture>20&&captured.Add(caseLabel+"-hit")){lastCapture=Time.time;Capture(caseLabel+"-hit");}
    }
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
    static IEnumerable<float> Check()
    {
        motion.Add("attempt,time,state,playerHp,bossHp,playerX,playerY,bossX,bossY,ortho,liveBossBullets,naturalInvincibility,healthFloor");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        string group=SessionState.GetString(PresentationKey,"All").Split('-')[0];
        if(group=="Flow")attemptSerial=14;
        string[] bosses=group=="All"?new[]{"A","B","C","Assault","Salvage","Sniper","NULL"}:group=="Remaining"?new[]{"B","C","Assault","Salvage","Sniper","NULL"}:group=="Final"?new[]{"C","NULL"}:group=="Flow"?new[]{"Sniper"}:new[]{group};
        foreach(string k in bosses)foreach(var tree in group=="Flow"?new[]{WeaponTreeType.Sniper}:new[]{WeaponTreeType.MachineGun,WeaponTreeType.Shotgun,WeaponTreeType.Sniper})
        {
            kind=k;weaponTree=tree;caseLabel=k+"-"+tree+"-"+(++attemptSerial);lastState=furthest="";hitCount=0;nextSample=nextDecision=0;dashAt=-100;observing=autoFire=inCombat=walking=false;
            boss=null;bossHealth=null;sector=null;triad=null;phase=null;assault=null;carrier=null;sniper=null;dispatcher=null;parts=null;Keys();
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            if(k!="A"){PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();}
            if(k!="A"&&k!="B"){PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();}
            if(k=="Assault"||k=="Salvage"||k=="Sniper"||k=="NULL"){PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();}
            if(k=="NULL"){PermanentProgress.Instance.TryAssembleRouteCore();PermanentProgress.Instance.TryActivateRouteCore();PermanentProgress.Instance.MarkSettlementDefenseCleared();}
            var depth=k=="B"||k=="Salvage"?ExpeditionDepth.DeepZone1:k=="C"||k=="Sniper"?ExpeditionDepth.DeepZone2:k=="NULL"?ExpeditionDepth.FinalNetwork:ExpeditionDepth.Normal;
            UnityEngine.Random.InitState(61002+attemptSerial);
            RunManager.Instance.StartNewRun(tree,depth);SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;Ready();
            Note("SETUP "+caseLabel+" depth="+depth+" selectedShip="+RunManager.Instance.CurrentRun.SelectedShipId+" HP="+player.CurrentHp+" max="+player.MaxHp+" armor="+player.GetComponent<PlayerArmor>().CurrentArmor+" weapon="+player.GetComponent<PlayerWeaponController>().CurrentWeaponTree);
            if(k=="C")
            {
                phase=Object.FindFirstObjectByType<ExpeditionMapGenerator>().CurrentRegion3BossEncounter;boss=phase;bossHealth=boss.GetComponent<EnemyHealth>();SetupMove(phase.EncounterAnchor+Vector2.down*2.5f);
                foreach(var d in Until(()=>phase.CurrentRouteStage!=PhaseGatekeeperBossController.RouteStage.Stopped,"proximity intro combat handoff before first attack"))yield return d;
            }
            else
            {
                var core=Object.FindFirstObjectByType<CoreObject>();Require(core!=null,"generated Core");
                var tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();
                if(tracking!=null&&tracking.IsTrackingActive)foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
                {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
                Note("SETUP ONLY Core signals revealed via wrecks; no boss damage or combat skipped");
                SetupMove(core.transform.position+Vector3.down*.6f);yield return .2f;core.Interact(player.gameObject);
                foreach(var d in Until(()=>
                {
                    if(!(bool)Get(core,"activated")&&core.CanInteract(player.gameObject))core.Interact(player.gameObject);
                    sector=k=="A"?Object.FindFirstObjectByType<BossPatternController>():null;triad=k=="B"?Object.FindFirstObjectByType<FrigateTriadBossController>():null;
                    assault=k=="Assault"?Object.FindFirstObjectByType<PirateCommanderBossController>():null;carrier=k=="Salvage"?Object.FindFirstObjectByType<RaiderSalvageCarrierBossController>():null;
                    sniper=k=="Sniper"?Object.FindFirstObjectByType<RaiderSniperCommanderBossController>():null;dispatcher=k=="NULL"?Object.FindFirstObjectByType<NullDispatcherBossController>():null;
                    boss=(Component)sector??(Component)triad??(Component)assault??(Component)carrier??(Component)sniper??dispatcher;return boss!=null;
                },"actual routed boss spawned"))yield return d;
                bossHealth=boss.GetComponent<EnemyHealth>();
                foreach(var d in Until(()=>sector!=null?sector.SectorCycle>0:triad!=null?triad.IsGameplayActive&&triad.DefenseCycle>0:assault!=null?assault.IsCombatActive:carrier!=null?carrier.IsCombatActive:sniper!=null?sniper.IsCombatActive:dispatcher.Phase==NullDispatcherBossController.EncounterPhase.Phase1,"normal combat handoff"))yield return d;
            }
            if(triad!=null)parts=(FrigateBossPart[])Get(triad,"parts");
            inCombat=true;fightStart=Time.time;startHp=player.CurrentHp;player.Damaged+=Damage;observing=autoFire=true;
            Note("START "+caseLabel+" boss="+boss.GetType().Name+" hp="+bossHealth.CurrentHp+" player="+startHp+" floor="+player.MinimumHealthFloor);
            double limit=EditorApplication.timeSinceStartup+(k=="C"||k=="NULL"?300:180);
            while(!player.IsDead&&bossHealth!=null&&!bossHealth.IsDead&&EditorApplication.timeSinceStartup<limit)
            {
                if(DialogueManager.isConversationActive){Capture(caseLabel+"-dialogue");foreach(var d in Dialogue())yield return d;}
                yield return .05f;
            }
            float elapsed=Time.time-fightStart;string outcome=player.IsDead?"death":bossHealth==null||bossHealth.IsDead?"victory":"timeout";
            results.Add(caseLabel+","+k+","+tree+","+outcome+","+elapsed.ToString("F3")+","+startHp+","+player.CurrentHp+","+hitCount+","+(bossHealth!=null?bossHealth.CurrentHp:0)+","+furthest);
            Note("RESULT "+results[results.Count-1]);autoFire=observing=inCombat=false;Keys();desiredFire=desiredDash=false;player.Damaged-=Damage;Capture(caseLabel+"-"+outcome);
            if(outcome=="victory")
            {
                yield return 8;
                if(k=="NULL")
                {foreach(var d in Until(()=>DialogueManager.isConversationActive,"ending conversation",30))yield return d;foreach(var d in Dialogue())yield return d;yield return 6;Capture(caseLabel+"-ending");Object.FindFirstObjectByType<RunResultPanelUI>().Close();}
                else foreach(var d in RewardFlow(k=="Assault"&&tree==WeaponTreeType.MachineGun))yield return d;
            }
            else if(outcome=="death")
            {
                foreach(var d in Until(()=>RunManager.Instance.IsCompletingRun||!RunManager.Instance.HasActiveRun,"death result",30))yield return d;yield return 4;Capture(caseLabel+"-death-result");Object.FindFirstObjectByType<RunResultPanelUI>().Close();
            }
            else {Note("Timeout: normal QA scene teardown, no victory claimed");SceneFlowManager.Instance.LoadSettlement();}
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        }
    }
    static IEnumerable<float> WalkTo(Transform destination)
    {
        walking=true;double limit=EditorApplication.timeSinceStartup+25;
        while(destination!=null&&Vector2.Distance(player.transform.position,destination.position)>.2f&&EditorApplication.timeSinceStartup<limit)
        {SetMove((Vector2)(destination.position-player.transform.position));yield return .04f;}
        Keys();walking=false;Require(destination!=null&&Vector2.Distance(player.transform.position,destination.position)<1.1f,"walk reached interaction");yield return .2f;
    }
    static IEnumerable<float> RewardFlow(bool deeper)
    {
        Require(RunManager.Instance.CurrentRun.BossDefeated,"authoritative boss defeat");Capture(caseLabel+"-reward");
        var capsule=Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).FirstOrDefault(c=>c.PayloadType==RewardCapsulePayloadType.BossReward);
        Require(capsule!=null,"boss reward capsule");foreach(var d in Until(()=>capsule.CanInteract(player.gameObject),"capsule landed",25))yield return d;
        foreach(var d in WalkTo(capsule.transform))yield return d;foreach(var d in PressInteract())yield return d;
        var choices=Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();foreach(var d in Until(()=>choices.IsShowing,"authored reward choices",20))yield return d;
        Capture(caseLabel+"-reward-choice");var buttons=(RunTraitChoiceButtonUI[])Get(choices,"choiceButtons");buttons[0].HandleButtonClicked();yield return 1;
        Require(!choices.IsShowing,"reward selected");Note("FLOW reward choice selected through authored button callback");
        if(deeper)
        {
            foreach(var d in Until(()=>Object.FindFirstObjectByType<WormholePortal>()!=null,"deeper portal",20))yield return d;var portal=Object.FindFirstObjectByType<WormholePortal>();
            foreach(var d in Until(()=>portal.PresentationReady,"portal reveal",20))yield return d;foreach(var d in WalkTo(portal.transform))yield return d;foreach(var d in PressInteract())yield return d;yield return .3f;
            var ui=Object.FindFirstObjectByType<WormholeChoiceUI>();for(int press=0;press<5&&!ui.IsOpen;press++){yield return .5f;foreach(var d in PressInteract())yield return d;}Require(ui.IsOpen,"deeper route confirmation");Capture(caseLabel+"-deeper-confirm");((UnityEngine.UI.Button)Get(ui,"yesButton")).onClick.Invoke();
            foreach(var d in Until(()=>RunManager.Instance.CurrentRun.ExpeditionDepth==ExpeditionDepth.DeepZone1&&!SceneFlowManager.Instance.IsLoading,"deeper Region B arrival",60))yield return d;yield return 3;BindCamera();Capture(caseLabel+"-deeper-arrival");Note("FLOW actual deeper route reached Region B");SceneFlowManager.Instance.LoadSettlement();
        }
        else
        {
            foreach(var d in Until(()=>Object.FindFirstObjectByType<ReturnBeacon>()!=null,"Return Beacon",20))yield return d;var beacon=Object.FindFirstObjectByType<ReturnBeacon>();
            foreach(var d in Until(()=>beacon.PresentationReady,"Beacon reveal",20))yield return d;foreach(var d in WalkTo(beacon.transform))yield return d;foreach(var d in PressInteract())yield return d;yield return .3f;
            var ui=Object.FindFirstObjectByType<ReturnChoiceUI>();for(int press=0;press<5&&!ui.IsOpen;press++){yield return .5f;foreach(var d in PressInteract())yield return d;}Require(ui.IsOpen,"safe return confirmation");Capture(caseLabel+"-beacon-confirm");((UnityEngine.UI.Button)Get(ui,"yesButton")).onClick.Invoke();yield return 6;
            Capture(caseLabel+"-safe-result");Note("FLOW safe return result reached");Object.FindFirstObjectByType<RunResultPanelUI>().Close();
        }
    }
    static IEnumerable<float> PressInteract()
    {Note("FLOW before F nearest="+player.GetComponent<PlayerInteractor>().CurrentTarget);Keys(Key.F);yield return .15f;Keys();yield return .2f;Note("FLOW real F interaction input");}
}
