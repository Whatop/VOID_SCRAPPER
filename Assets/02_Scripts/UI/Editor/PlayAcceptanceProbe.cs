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
public static class PlayAcceptanceProbe
{
    const string SessionKey = "PlayAcceptance.Probe", PresentationKey = "PlayAcceptance.Group";
    static string Dir => "Logs/PlayAcceptance/" + SessionState.GetString(PresentationKey, "A") + "/";
    static bool NativeCapture => SessionState.GetString(PresentationKey, "").StartsWith("Native");
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
    static bool desiredFire, desiredDash, desiredScan;
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
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(PlayAcceptanceProbe),updateDelegate=FeedGameFrame};
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
        InputSystem.QueueStateEvent(qaMouse,new MouseState{position=desiredAim}.WithButton(MouseButton.Left,desiredFire).WithButton(MouseButton.Right,desiredDash).WithButton(MouseButton.Back,desiredScan));
        InputSystem.Update();
    }
    static PlayAcceptanceProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs(); string group="Normal";
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-acceptanceGroup")group=args[i+1];
        BeginRun(group);
    }
    static void BeginRun(string region)
    {
        string label=region; int revision=2; while(File.Exists("Logs/PlayAcceptance/"+label+"/attempts.csv"))label=region+"-"+(revision++); SessionState.SetString(PresentationKey,label);
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Dir + "Rendered");
        SetNativeGameView();
        if (region != "Normal" && region != "Native")
        {
            File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json", true);
            File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json.bak", true);
        }
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
            SampleWorld();
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
        camera = Camera.main;
        if (camera == null) camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            camera = new GameObject("QA overlay capture camera").AddComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0,0,-10);
            camera.backgroundColor = new Color(.012f,.02f,.035f); camera.clearFlags=CameraClearFlags.SolidColor;
        }
        if (NativeCapture) return;
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
        BindCamera();
        if (NativeCapture) ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir + "Rendered/" + name + ".png"));
        else
        {
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(480, 270, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); png.Apply();
        File.WriteAllBytes(Dir + "Rendered/" + name + ".png", png.EncodeToPNG()); Object.Destroy(png); RenderTexture.active = previous;
        }
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
    static IEnumerable<float> LaterCheck()
    {
        Note("SHORTCUT RUN: campaign flags/Core reveal/precombat navigation are arranged. Actual fights retain normal health, damage, weapons and no QA invulnerability.");
        motion.Add("attempt,time,state,playerHp,bossHp,playerX,playerY,bossX,bossY,ortho,liveBossBullets,naturalInvincibility,healthFloor");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        string group=SessionState.GetString(PresentationKey,"All").Split('-')[0];
        if(group=="Flow")attemptSerial=14;
        string[] bosses=group=="Later"?new[]{"A","B","C","Assault","NULL"}:group=="All"?new[]{"A","B","C","Assault","Salvage","Sniper","NULL"}:group=="Remaining"?new[]{"B","C","Assault","Salvage","Sniper","NULL"}:group=="Final"?new[]{"C","NULL"}:group=="Flow"?new[]{"Sniper"}:new[]{group};
        foreach(string k in bosses)foreach(var tree in group=="Later"?new[]{k=="B"||k=="Assault"||k=="NULL"?WeaponTreeType.Shotgun:k=="C"?WeaponTreeType.Sniper:WeaponTreeType.MachineGun}:group=="Flow"?new[]{WeaponTreeType.Sniper}:new[]{WeaponTreeType.MachineGun,WeaponTreeType.Shotgun,WeaponTreeType.Sniper})
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
        foreach(var d in Until(()=>Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).Any(c=>c.PayloadType==RewardCapsulePayloadType.BossReward),"boss reward spawned",30))yield return d;
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
    static readonly List<string> worldSamples=new List<string>{"time,scene,hp,armor,healthFloor,objects,bullets,audioSources,playingAudioSources,particles,managedBytes,frameDelta"};
    static float worldSampleAt, noPerfBefore;
    static void SampleWorld()
    {
        if(!Application.isPlaying||Time.unscaledTime<worldSampleAt)return;worldSampleAt=Time.unscaledTime+2;
        var sources=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        worldSamples.Add(Time.unscaledTime.ToString("F2")+","+SceneManager.GetActiveScene().name+","+(player!=null?player.CurrentHp:0)+","+(player!=null?player.GetComponent<PlayerArmor>()?.CurrentArmor:0)+","+(player!=null?player.MinimumHealthFloor:0)+","+Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length+","+Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length+","+sources.Length+","+sources.Count(x=>x.isPlaying)+","+Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length+","+UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()+","+Time.unscaledDeltaTime.ToString("F4"));
        File.WriteAllLines(Dir+"world-samples.csv",worldSamples);
    }
    static void StopInput(){Keys();desiredFire=desiredDash=desiredScan=false;walking=false;}
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static Key ActionKey(string action)
    {
        var a=player.GetComponent<PlayerController2D>().InputActions.FindAction("Player/"+action);
        foreach(var b in a.bindings)
        {
            if(!b.effectivePath.StartsWith("<Keyboard>/"))continue;
            var key=qaKeyboard[b.effectivePath.Substring(11)] as UnityEngine.InputSystem.Controls.KeyControl;
            if(key!=null)return key.keyCode;
        }
        throw new Exception("No keyboard binding for "+action);
    }
    static IEnumerable<float> Tap(string action,float duration=.15f)
    {walking=true;Keys(ActionKey(action));yield return duration;Keys();yield return .2f;}
    static void Steer(Vector2 destination,float stopDistance=.5f)
    {
        walking=true;Vector2 delta=destination-(Vector2)player.transform.position;
        desiredAim=camera.WorldToScreenPoint(destination);
        if(delta.magnitude>stopDistance)SetMove(delta.normalized);else Keys();
    }
    static Transform Target(object value)=>value is GameObject g?g.transform:value is Component c?c.transform:null;
    static void ShootAt(Transform target,float range=3f)
    {
        if(target==null){desiredFire=false;return;}
        Steer(target.position,range);desiredFire=true;
    }
    static void AimFire(Transform target)
    {
        if(target==null)return;desiredAim=camera.WorldToScreenPoint(target.position);
        float cycle=Time.time%2;
        desiredFire=weaponTree==WeaponTreeType.MachineGun||(weaponTree==WeaponTreeType.Shotgun?Time.time%.8f<.2f:cycle<1.25f);
    }
    static IEnumerable<float> Check()
    {
        string group=SessionState.GetString(PresentationKey,"Normal").Split('-')[0];
        if(group!="Normal"&&group!="Native") {foreach(var d in LaterCheck())yield return d;yield break;}
        Note("PRIMARY INPUT-DRIVEN RUN: fresh save via Boot New Game; no teleport, direct damage, granted items, forced checkpoint, health editing or QA invulnerability. Navigation reads authored objective positions. UI callbacks are used for menu/dialogue choices. Tutorial's authored safety floor is preserved.");
        foreach(var d in Until(()=>GameBootstrap.Instance!=null&&GameBootstrap.Instance.IsProgressLoaded,"Boot ready"))yield return d;
        yield return 2;Capture("00-boot");
        var menu=Object.FindFirstObjectByType<BootMainMenuView>();Require(menu!=null,"Authored Boot menu");menu.NewGameButton.onClick.Invoke();yield return .3f;
        if(menu!=null&&menu.NewGameConfirmationRoot.activeSelf)menu.ConfirmNewGameButton.onClick.Invoke();
        foreach(var d in Scene("Tutorial"))yield return d;Ready();weaponTree=WeaponTreeType.MachineGun;
        var flow=Object.FindFirstObjectByType<TutorialFlowController>();int previous=-1;double stepAt=EditorApplication.timeSinceStartup;float tapAt=0;bool stepAction=false;
        while(SceneManager.GetActiveScene().name=="Tutorial"&&flow!=null)
        {
            int step=(int)flow.CurrentStep;
            if(step!=previous){StopInput();previous=step;stepAt=EditorApplication.timeSinceStartup;stepAction=false;Note("TUTORIAL step="+flow.CurrentStep+" hp="+player.CurrentHp+" floor="+player.MinimumHealthFloor);yield return .25f;Capture("tutorial-"+step.ToString("00")+"-"+flow.CurrentStep);}
            if(DialogueManager.isConversationActive){StopInput();foreach(var d in Dialogue())yield return d;yield return .3f;continue;}
            if(EditorApplication.timeSinceStartup-stepAt>100)
            {Note("DRIVER STOP: Tutorial step "+flow.CurrentStep+" exceeded 100 seconds; investigate before declaring progression blocked.");Capture("tutorial-driver-stall");StopInput();yield break;}
            var panel=Object.FindFirstObjectByType<ExpeditionMenuController>();
            if(panel!=null&&panel.IsOpen&&flow.CurrentStep!=TutorialStep.Map&&flow.CurrentStep!=TutorialStep.RoutePing){panel.Close();yield return .2f;}
            walking=true;desiredDash=desiredScan=desiredFire=false;
            Transform target=null;
            switch(flow.CurrentStep)
            {
                case TutorialStep.Move: Keys(Key.W);break;
                case TutorialStep.AimAndFire: Keys();desiredAim=new Vector2(380,160);desiredFire=true;break;
                case TutorialStep.Dash:
                    if(!stepAction){Steer((Vector2)Get(flow,"dashPracticeCenter"),.25f);if(Vector2.Distance(player.transform.position,(Vector2)Get(flow,"dashPracticeCenter"))<.3f){Keys(Key.D);desiredAim=camera.WorldToScreenPoint(player.transform.position+Vector3.right*3);desiredDash=true;stepAction=true;}}
                    else Keys();break;
                case TutorialStep.RadarDiscoverSalvage:
                    // The production focus waits for the entire salvage inside its safe viewport.
                    // Stopping four units away can leave a vertically placed target at the edge.
                    target=Target(Get(flow,"harvestTarget"));Steer(target.position,2.5f);
                    var radar=player.GetComponent<PlayerRadarScanner>();
                    if(!radar.IsRadarActive&&Time.time>tapAt){foreach(var d in Tap("Radar"))yield return d;tapAt=Time.time+1;}
                    if(Time.time>tapAt){desiredScan=true;tapAt=Time.time+1;}break;
                case TutorialStep.Map:
                    if(!stepAction){foreach(var d in Tap("Map"))yield return d;stepAction=true;}break;
                case TutorialStep.RoutePing:
                case TutorialStep.TravelNormalSalvage:
                    if(panel!=null&&panel.IsOpen)panel.Close();target=Target(Get(flow,"harvestTarget"));Steer(target.position,2);break;
                case TutorialStep.DestroyNormalSalvage: ShootAt(Target(Get(flow,"harvestTarget")),2.7f);break;
                case TutorialStep.CollectResources:
                    var rewards=Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None);var reward=rewards.OrderBy(x=>Vector2.Distance(player.transform.position,x.transform.position)).FirstOrDefault();if(reward!=null)Steer(reward.transform.position,.05f);else Keys();break;
                case TutorialStep.Cargo: Keys();break;
                case TutorialStep.TravelHighValue: Steer(Target(Get(flow,"highValueSalvageInstance")).position,3);desiredScan=Time.time%1f<.2f;break;
                case TutorialStep.DestroyHighValue: ShootAt(Target(Get(flow,"highValueSalvageInstance")),2.7f);break;
                case TutorialStep.EquipDefensiveActive:
                case TutorialStep.AcquireEmergencyReturn:
                    var pickup=Object.FindObjectsByType<ReinforcementPickup>(FindObjectsSortMode.None).OrderBy(x=>Vector2.Distance(player.transform.position,x.transform.position)).FirstOrDefault();
                    if(pickup!=null){Steer(pickup.transform.position,.2f);if(Vector2.Distance(player.transform.position,pickup.transform.position)<.7f&&Time.time>tapAt){Capture("tutorial-pickup-card-"+step);if(NativeCapture)yield return .12f;foreach(var d in Tap("Interact"))yield return d;tapAt=Time.time+1;}}break;
                case TutorialStep.UseDefensiveActive:
                    if(!stepAction){foreach(var d in Tap("UseReinforcement"))yield return d;stepAction=true;}break;
                case TutorialStep.Combat:
                    var enemy=Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Where(x=>!x.IsDead).OrderBy(x=>Vector2.Distance(player.transform.position,x.transform.position)).FirstOrDefault();
                    if(enemy!=null){ShootAt(enemy.transform,3);Vector2 d=(Vector2)(player.transform.position-enemy.transform.position);SetMove(new Vector2(-d.y,d.x));}else Keys();break;
                case TutorialStep.FindSignalDevice:Steer(Target(Get(flow,"radarTarget")).position,3);desiredScan=Time.time%1f<.2f;break;
                case TutorialStep.InteractSignalDevice:
                    target=Target(Get(flow,"interactionTarget"));Steer(target.position,.3f);if(Vector2.Distance(player.transform.position,target.position)<.8f&&Time.time>tapAt){foreach(var d in Tap("Interact"))yield return d;tapAt=Time.time+1;}break;
                case TutorialStep.TravelSearchArea:Steer((Vector2)Call(flow,"ResolveUnknownSearchAreaPosition"),1);break;
                case TutorialStep.RevealPurpleCore:Keys();desiredScan=Time.time%1f<.2f;break;
                case TutorialStep.TravelPurpleCore:Steer(Target(Get(flow,"alienSignal")).position,1.8f);break;
                case TutorialStep.InteractPurpleCore:
                    target=Target(Get(flow,"alienSignal"));Steer(target.position,.3f);if(Vector2.Distance(player.transform.position,target.position)<.8f&&Time.time>tapAt){foreach(var d in Tap("Interact"))yield return d;tapAt=Time.time+1;}break;
                case TutorialStep.EmergencyReturn:
                    if(player.GetComponent<EmergencyReturnController>().GaugeFilled&&!stepAction){Capture("tutorial-return-ready");if(NativeCapture)yield return .2f;stepAction=true;}
                    if(stepAction)Keys();else Keys(ActionKey("UseReinforcement"));break;
                case TutorialStep.Complete:
                    StopInput();if(!stepAction){yield return 3;Capture("tutorial-return-result");yield return .2f;Object.FindFirstObjectByType<RunResultPanelUI>().Close();stepAction=true;}yield return .3f;break;
                default:Keys();break;
            }
            yield return .12f;
        }
        StopInput();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;Capture("settlement-after-complete-tutorial");
        Note("COMPLETE normal Tutorial through production authority; no checkpoint mutation.");
        if(NativeCapture){yield return 1;Note("Raw GameView recheck complete; no camera/canvas adaptation used.");yield break;}
        var settlement=Object.FindFirstObjectByType<SettlementController>();settlement.SelectWeaponTree(WeaponTreeType.MachineGun);
        Require(settlement.LaunchExpedition(),"Normal Settlement deployment");foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
        Note("NORMAL A entry HP="+player.CurrentHp+" armor="+player.GetComponent<PlayerArmor>().CurrentArmor+" floor="+player.MinimumHealthFloor);Capture("normal-A-entry");
        foreach(var d in ExploreRegionA())yield return d;
    }
    static IEnumerable<float> ExploreRegionA()
    {
        var tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();double deadline=EditorApplication.timeSinceStartup+420;float lastScan=-100,nextCapture=Time.time+12;HashSet<int> visited=new HashSet<int>();
        while(!player.IsDead&&tracking!=null&&!tracking.IsCoreRevealed&&EditorApplication.timeSinceStartup<deadline)
        {
            if(DialogueManager.isConversationActive){StopInput();foreach(var d in Dialogue())yield return d;continue;}
            var choices=Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();if(choices!=null&&choices.IsShowing){StopInput();Capture("normal-trait-choice-"+Time.frameCount);((RunTraitChoiceButtonUI[])Get(choices,"choiceButtons"))[0].HandleButtonClicked();yield return .3f;continue;}
            walking=true;desiredFire=desiredDash=desiredScan=false;
            var radar=player.GetComponent<PlayerRadarScanner>();if(!radar.IsRadarActive){foreach(var d in Tap("Radar"))yield return d;}
            if(Time.time-lastScan>4){desiredScan=true;lastScan=Time.time;}
            var enemy=Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Where(x=>!x.IsDead&&Vector2.Distance(player.transform.position,x.transform.position)<5).OrderBy(x=>Vector2.Distance(player.transform.position,x.transform.position)).FirstOrDefault();
            var wreck=Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None).Where(x=>!x.IsDead&&x.ObjectKind==HarvestObjectKind.HighValueWreck).OrderBy(x=>Vector2.Distance(player.transform.position,x.transform.position)).FirstOrDefault();
            if(enemy!=null){ShootAt(enemy.transform,3.2f);Vector2 delta=player.transform.position-enemy.transform.position;SetMove(new Vector2(-delta.y,delta.x)+delta.normalized*.3f);if(Time.time%3.5f<.2f)desiredDash=true;}
            else if(wreck!=null){ShootAt(wreck.transform,2.7f);}
            else Keys();
            foreach(var thief in Object.FindObjectsByType<PickupCollectionPresentation>(FindObjectsSortMode.None))if(thief.IsShowing&&visited.Add(thief.GetInstanceID())){Capture("natural-theft-channel-"+Time.frameCount);Note("NATURAL theft channel seen target="+thief.Target);}
            if(Time.time>nextCapture){nextCapture=Time.time+15;Capture("normal-exploration-"+Time.frameCount);Note("NORMAL signals="+tracking.CurrentSignalCount+" HP="+player.CurrentHp+" armor="+player.GetComponent<PlayerArmor>().CurrentArmor);}
            yield return .15f;
        }
        StopInput();
        if(player.IsDead){Note("NORMAL result: died during exploration");Capture("normal-A-exploration-death");foreach(var d in DeathReturn())yield return d;yield break;}
        if(tracking!=null&&!tracking.IsCoreRevealed){Note("DRIVER limitation: exploration navigation timed out; no production blocker assumed.");yield break;}
        var core=Object.FindFirstObjectByType<CoreObject>();foreach(var d in WalkTo(core.transform))yield return d;
        foreach(var d in PressInteract())yield return d;
        foreach(var d in Until(()=>Object.FindFirstObjectByType<BossPatternController>()!=null,"Normal A boss spawn"))yield return d;
        sector=Object.FindFirstObjectByType<BossPatternController>();boss=sector;bossHealth=boss.GetComponent<EnemyHealth>();caseLabel="normal-A-MachineGun";kind="A";
        Capture("normal-A-arrival");foreach(var d in Until(()=>sector.SectorCycle>0,"Normal A combat"))yield return d;
        fightStart=Time.time;startHp=player.CurrentHp;hitCount=0;player.Damaged+=Damage;autoFire=observing=inCombat=true;
        Note("NORMAL boss start HP="+startHp+" Armor="+player.GetComponent<PlayerArmor>().CurrentArmor+" floor="+player.MinimumHealthFloor);
        while(!player.IsDead&&!bossHealth.IsDead&&Time.time-fightStart<180)yield return .1f;
        autoFire=observing=inCombat=false;StopInput();player.Damaged-=Damage;
        results.Add(caseLabel+",A,MachineGun,"+(player.IsDead?"death":bossHealth.IsDead?"victory":"timeout")+","+(Time.time-fightStart)+","+startHp+","+player.CurrentHp+","+hitCount+","+bossHealth.CurrentHp+","+State());Note("NORMAL "+results.Last());Capture("normal-A-result");
        if(player.IsDead)foreach(var d in DeathReturn())yield return d;
        else if(bossHealth.IsDead){yield return 8;foreach(var d in RewardFlow(false))yield return d;foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;}
    }
    static IEnumerable<float> DeathReturn()
    {
        StopInput();foreach(var d in Until(()=>RunManager.Instance.IsCompletingRun||!RunManager.Instance.HasActiveRun,"natural death result",30))yield return d;yield return 4;
        Capture("normal-death-result");var panel=Object.FindFirstObjectByType<RunResultPanelUI>();if(panel!=null)panel.Close();
        foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;Capture("normal-death-return-settlement");
    }

}
