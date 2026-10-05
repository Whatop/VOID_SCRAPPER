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
public static class RaiderSniperProbe
{
    const string SessionKey = "RaiderSniper.Probe", PresentationKey = "RaiderSniper.Mode";
    static string Dir => "Logs/RaiderSniper/" + SessionState.GetString(PresentationKey, "A") + "/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static RaiderSniperCommanderBossController boss;
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
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(RaiderSniperProbe),updateDelegate=FeedGameFrame};
            System.Array.Copy(old,0,updated,1,old.Length);loop.subSystemList[i].subSystemList=updated;
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);inputLoopInstalled=true;
    }
    public static void FeedGameFrame()
    {
        if(finishing||qaMouse==null)return;
        if(autoFire && boss!=null && !bossHealth.IsDead)
        {
            desiredAim=camera.WorldToScreenPoint(boss.transform.position);
            var tree=player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            desiredFire=tree==WeaponTreeType.MachineGun||(tree==WeaponTreeType.Sniper?Time.time%1.2f<.85f:Time.time%.85f<.2f);
        }
        else desiredFire=false;
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
    static RaiderSniperProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run() => BeginRun("Native");
    public static void RunInterruptions() => BeginRun("Interruptions");
    public static void RunHazards() => BeginRun("Hazards");
    static void BeginRun(string region)
    {
        SessionState.SetString(PresentationKey,region);
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
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 2400; EditorApplication.update += Tick;
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
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "combat-timing.csv", motion); }
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
    static void Ready()
    { Keys(); player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "authored player"); player.SetDashInvincible(true); player.GetComponent<PlayerArmor>().SetMaxArmor(8,true);
        player.GetComponent<PlayerController2D>().InputActions.devices=new InputDevice[]{qaMouse,qaKeyboard};
        var wc=player.GetComponent<PlayerWeaponController>();var original=(InputAction)Get(wc,"fireAction");
        Note("FIRE BINDING before isolation: enabled="+original.enabled+" phase="+original.phase+" bindings="+string.Join(";",original.bindings.Select(b=>b.effectivePath)));
        fixtureFire?.Dispose();fixtureFire=original.Clone();fixtureFire.Enable();
        typeof(PlayerWeaponController).GetField("fireAction",Flags).SetValue(wc,fixtureFire);
        fixtureDash?.Dispose();fixtureMove?.Dispose();
        var dash=player.GetComponent<PlayerDash>();fixtureDash=((InputAction)Get(dash,"dashAction")).Clone();fixtureDash.Enable();typeof(PlayerDash).GetField("dashAction",Flags).SetValue(dash,fixtureDash);
        var control=player.GetComponent<PlayerController2D>();fixtureMove=((InputAction)Get(control,"moveAction")).Clone();fixtureMove.Enable();typeof(PlayerController2D).GetField("moveAction",Flags).SetValue(control,fixtureMove);
        InstallInputLoop(); }
    static void Move(Vector3 p)
    { var rb = player.GetComponent<Rigidbody2D>(); rb.position = p; player.transform.position = p; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static void Keys(params Key[] keys) => keyboardState=new KeyboardState(keys);
    static void Mouse(bool pressed) { desiredFire=pressed; }
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
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static string caseLabel,lastLabel;
    static ExpeditionHUD hud;
    static CoreTrackingSignalController tracking;
    static int signals,lastShots,peak;
    static SpriteRenderer playerBody;
    static Vector2 opaqueSize;
    static float labelSince,lastShotTime,diagnosticTime;
    static bool observe;
    static float salvageDiagnostic;
    static void SampleCombat()
    {
        if(!observe||boss==null||player==null||camera==null)return;
        string state=(boss.IsPhase2?"critical-":"normal-")+boss.Stage;
        bool combat=boss.IsCombatActive&&!bossHealth.IsDead&&!player.IsDead;
        float pixels=Mathf.Min(opaqueSize.x*Mathf.Abs(playerBody.transform.lossyScale.x),opaqueSize.y*Mathf.Abs(playerBody.transform.lossyScale.y))*270/(2*camera.orthographicSize);
        if(Time.time!=lastSample)
        {
            lastSample=Time.time;
            if(state!=lastLabel){lastLabel=state;labelSince=Time.time;Note("STATE "+caseLabel+" "+Time.time.ToString("F4")+" "+state);}
            int count=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b=>b.SourceRoot==boss.transform);peak=Mathf.Max(peak,count);
            motion.Add(caseLabel+","+Time.time.ToString("F4")+","+state+","+boss.ShotsFired+","+count+","+camera.orthographicSize.ToString("F4")+","+pixels.ToString("F3")+","+bossHealth.CurrentHp.ToString("F3")+","+boss.transform.position.x+","+boss.transform.position.y+","+boss.LiveMineCount+","+boss.Cycle+","+boss.IsRailDamaging);
            if(combat)
            {
                Require(hud.IsRegionBossPresentationActive,"boss presentation active");
                Require(!((GameObject)Get(hud,"objectiveRoot")).activeSelf,"Core Signal suppressed");
                Require(pixels>=6,"native player body readable");
                foreach(var enemy in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None))
                    if(enemy.GetComponentInParent<BossDummyController>()==null)Require((bool)Get(enemy,"bossEncounterIsolated"),"ordinary AI isolated");
                foreach(var meteor in Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None))
                    Require(meteor.IsSectorEncounterPaused,"ordinary meteor drift suspended");
            }
            if((Time.time-labelSince>.12f||boss.IsRailDamaging)&&captured.Add(caseLabel+"-"+state))Capture(caseLabel+"-"+state);
            if(boss.ShotsFired!=lastShots)
            {
                Note("SHOT "+caseLabel+" t="+Time.time.ToString("F4")+" dt="+(Time.time-lastShotTime).ToString("F4")+" mines="+boss.LiveMineCount+" total="+boss.ShotsFired+" critical="+boss.IsPhase2+" target="+boss.CommittedTarget);
                lastShotTime=Time.time;lastShots=boss.ShotsFired;
            }
        }
        if(autoFire&&!bossHealth.IsDead)
        {
            if(Time.time>diagnosticTime)
            {
                diagnosticTime=Time.time+10;
                var wc=player.GetComponent<PlayerWeaponController>();var mg=player.GetComponentInChildren<MachineGunWeapon>(true);
                var input=UnityEngine.InputSystem.Mouse.current;var action=(InputAction)Get(wc,"fireAction");
                Note("INPUT DIAGNOSTIC tree="+wc.CurrentWeaponTree+" locked="+wc.ExternalInputLocked+" enabled="+wc.enabled+" canUse="+typeof(PlayerWeaponController).GetMethod("CanUseWeapon",Flags).Invoke(wc,null)+" fireEnabled="+action.enabled+" phase="+action.phase+" pressed="+action.IsPressed()+" actualWeapon="+wc.CurrentWeapon+" aim="+player.GetComponent<PlayerController2D>().AimDirection+" heat="+(mg!=null?mg.CurrentHeat:0)+" mouse="+input+" held="+(input!=null&&input.leftButton.isPressed)+" playerBullets="+Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b=>b.Owner==ProjectileOwner.Player));
                Capture(caseLabel+"-live-diagnostic");
            }
            Move(boss.transform.position+new Vector3(Mathf.Sin(Time.time)*.15f,-2.7f,0));
            Vector2 aim=camera.WorldToScreenPoint(boss.transform.position);
            var tree=player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            bool fire=tree==WeaponTreeType.MachineGun||(tree==WeaponTreeType.Sniper?Time.time%1.2f<.85f:Time.time%.85f<.2f);
            desiredAim=aim;desiredFire=fire;
        }
        else Mouse(false);
    }
    static void CacheBody()
    {
        var visual=player.GetComponent<PlayerVisualStateController>();playerBody=(SpriteRenderer)Get(visual,"baseSpriteRenderer");
        var sprite=playerBody.sprite;var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        Rect r=sprite.rect;int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
        for(int y=(int)r.y;y<r.yMax;y++)for(int x=(int)r.x;x<r.xMax;x++)if(texture.GetPixel(x,y).a>.5f){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
        opaqueSize=new Vector2(maxX-minX+1,maxY-minY+1)/sprite.pixelsPerUnit;Object.DestroyImmediate(texture);
        Note("Opaque player body world dimensions at unit scale="+opaqueSize+" actual scale="+playerBody.transform.lossyScale);
    }
    static IEnumerable<float> Check()
    {
        motion.Add("case,time,state,shots,liveProjectiles,orthographicSize,playerPixels,hp,bossX,bossY,mines,cycle,railActive");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        string mode=SessionState.GetString(PresentationKey,"Native");
        for(int attempt=mode=="Hazards"?6:mode=="Interruptions"?3:0;attempt<(mode=="Hazards"?7:6);attempt++)
        {
            var tree=attempt==1?WeaponTreeType.Shotgun:attempt==2?WeaponTreeType.Sniper:WeaponTreeType.MachineGun;
            bool deathRun=attempt==3,abortRun=attempt==4,collectionKill=attempt==5,hazardRun=attempt==6;
            caseLabel=hazardRun?"hazard-timing":deathRun?"player-death":abortRun?"abort":collectionKill?"mines-kill":tree.ToString();autoFire=observe=false;boss=null;bossHealth=null;lastLabel=null;lastShots=peak=0;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            RunManager.Instance.StartNewRun(tree,ExpeditionDepth.DeepZone2);SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            hud=Object.FindFirstObjectByType<ExpeditionHUD>();tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();CacheBody();
            var core=Object.FindFirstObjectByType<CoreObject>();Require(core!=null,"generated revisit Core");
            if(tracking!=null&&tracking.IsTrackingActive)
                foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
                {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
            signals=tracking!=null?tracking.CurrentSignalCount:0;
            Move(core.transform.position+Vector3.down*.6f);yield return .2f;core.Interact(player.gameObject);
            foreach(var d in Until(()=>
            {
                if(!(bool)Get(core,"activated")){Move(core.transform.position+Vector3.down*.6f);if(core.CanInteract(player.gameObject))core.Interact(player.gameObject);}
                boss=Object.FindFirstObjectByType<RaiderSniperCommanderBossController>();return boss!=null;
            },"Region C revisit selection spawned separate Sniper Commander"))yield return d;
            bossHealth=boss.GetComponent<EnemyHealth>();
            foreach(var d in Until(()=>camera.WorldToViewportPoint(boss.transform.position).y<.85f,"boss entry"))yield return d;Capture(caseLabel+"-intro");
            foreach(var d in Until(()=>boss.IsCombatActive,"normal combat handoff"))yield return d;
            Ready();observe=true;Move(boss.transform.position+Vector3.down*3);yield return .1f;Capture(caseLabel+"-idle");
            if(deathRun)
            {
                foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.CommittedCharge,"committed rail lock before player death"))yield return d;
                foreach(var d in EstablishLock())yield return d;player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);player.GetComponent<PlayerArmor>().SetMaxArmor(0,true);player.TakeDamage(99999);
                foreach(var d in Until(()=>!RunManager.Instance.HasActiveRun,"player death result"))yield return d;yield return 1;VerifyCleanup();Capture(caseLabel+"-cleanup");
                Object.FindFirstObjectByType<RunResultPanelUI>().Close();
            }
            else if(abortRun)
            {
                foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.CommittedCharge,"committed rail lock before abort"))yield return d;
                foreach(var d in EstablishLock())yield return d;boss.gameObject.SetActive(false);core.gameObject.SetActive(false);yield return .2f;
                VerifyCleanup();Capture(caseLabel+"-cleanup");SceneFlowManager.Instance.LoadSettlement();
            }
            else
            {
                if(hazardRun)
                {
                    foreach(var d in MeasureMine())yield return d;bossHealth.TakeDamage(99999);
                }
                else if(collectionKill)
                {
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.CommittedCharge,"lock before direct cleanup fixture"))yield return d;
                    foreach(var d in EstablishLock())yield return d;bossHealth.TakeDamage(99999);
                }
                else
                {
                    // Observe a complete natural rail/mine cycle before applying real weapon input.
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.CommittedCharge,"committed warning"))yield return d;
                    Vector2 direction=boss.CommittedDirection;float dodgeHp=player.CurrentHp,dodgeArmor=player.GetComponent<PlayerArmor>().CurrentArmor;
                    player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);
                    Vector3 dodgeStart=player.transform.position;Keys(Key.D);yield return .1f;Keys();yield return .05f;
                    Require(Vector3.Distance(dodgeStart,player.transform.position)>.25f,"real lateral movement after commitment");
                    Require(boss.CommittedDirection==direction,"later player motion does not snap committed line");Capture(caseLabel+"-committed-after-move");
                    foreach(var d in Until(()=>boss.ShotsFired>0,"rail pulse released"))yield return d;Capture(caseLabel+"-rail-shot");
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.Recovery,"post rail recovery"))yield return d;Capture(caseLabel+"-recovery");
                    Require(player.CurrentHp==dodgeHp&&player.GetComponent<PlayerArmor>().CurrentArmor==dodgeArmor,"unprotected input-driven post-commit dodge avoids rail");player.SetDashInvincible(true);Note("DODGE actual lateral input, invulnerability off, HP/Armor unchanged");
                    int dashSerial=player.GetComponent<PlayerDash>().CompletedDashSerial;Keys(Key.D);desiredDash=true;yield return .1f;
                    Capture(caseLabel+"-dash-around-mines");desiredDash=false;Keys();yield return .4f;player.SetDashInvincible(true);Require(player.GetComponent<PlayerDash>().CompletedDashSerial>dashSerial,"actual Dash completes around mine layout");
                    Move(boss.transform.position+Vector3.down*2);
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.Reposition,"close range retreat"))yield return d;yield return .5f;Capture(caseLabel+"-close-retreat");
                    autoFire=true;foreach(var d in Until(()=>boss.IsPhase2,"weapon damage crosses 50 percent threshold",90))yield return d;
                    autoFire=false;Mouse(false);
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.RailShot,"critical rail cycle",30))yield return d;Capture(caseLabel+"-critical-rail");
                    foreach(var d in Until(()=>boss.Stage==RaiderSniperCommanderBossController.SniperStage.Recovery,"critical recovery",10))yield return d;
                    yield return .5f;Capture(caseLabel+"-critical-recovery");

                }
                autoFire=true;foreach(var d in Until(()=>bossHealth==null||bossHealth.IsDead,(collectionKill||hazardRun)?"direct cleanup fixture":"actual weapon victory",240))yield return d;
                autoFire=false;Mouse(false);yield return .5f;Capture(caseLabel+"-death");yield return 8;VerifyCleanup();Capture(caseLabel+"-reward");
                Require(RunManager.Instance.CurrentRun.BossDefeated,"existing run boss-clear authority");
                Note("COMPLETED "+caseLabel+" peak live boss projectiles="+peak+" escalation count="+boss.EscalationCount);
                SceneFlowManager.Instance.LoadSettlement();
            }
            observe=autoFire=false;boss=null;bossHealth=null;
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
            Require(!Object.FindObjectsByType<BossHealthBarUI>(FindObjectsSortMode.None).Any(b=>b.IsVisible),"no stale boss HUD after Settlement");Capture(caseLabel+"-settlement");
        }
        Note("Actual Boot -> Settlement -> generated Region C revisit Core -> Sniper Commander. Case names above identify the weapons/interruption fixtures executed. Invulnerability and assisted positioning used. Victory returns via QA scene flow; reward-choice/Beacon interactions not exercised. Player death uses result return; abort explicitly disables encounter owners.");
    }
    static IEnumerable<float> MeasureMine()
    {
        RaiderSniperMine first=null;
        foreach(var d in Until(()=>{first=((RaiderSniperMine[])Get(boss,"mines")).FirstOrDefault(m=>m!=null);return first!=null;},"timed mine deployed"))yield return d;
        float born=Time.time-(float)Get(first,"age");Capture("hazard-mine-warning");
        foreach(var d in Until(()=>first.IsArmed,"timed mine armed"))yield return d;
        float armed=Time.time;Capture("hazard-mine-armed");
        foreach(var d in Until(()=>first.Owner==null,"untriggered mine expired",12))yield return d;
        float expired=Time.time;Note("MINE CLOCK warning="+(armed-born).ToString("F4")+" armed lifetime="+(expired-armed).ToString("F4"));
        Require(armed-born>=1.09f&&armed-born<1.22f,"warning measured before arming");Require(expired-armed>7.9f&&expired-armed<8.15f,"eight-second live mine expiry");Capture("hazard-mine-expired");
        RaiderSniperMine nextMine=null;
        foreach(var d in Until(()=>{nextMine=((RaiderSniperMine[])Get(boss,"mines")).FirstOrDefault(m=>m!=null&&m.IsArmed);return nextMine!=null;},"armed mine for detonation",20))yield return d;
        float armor=player.GetComponent<PlayerArmor>().CurrentArmor;player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);
        Move(nextMine.transform.position);
        foreach(var d in Until(()=>nextMine.IsDetonating,"actual proximity detonation",2))yield return d;float fired=Time.time;Capture("hazard-mine-detonation");
        foreach(var d in Until(()=>nextMine.Owner==null,"detonation cleaned",2))yield return d;
        Note("MINE CLOCK burst visible cleanup="+(Time.time-fired).ToString("F4")+" armor damage="+(armor-player.GetComponent<PlayerArmor>().CurrentArmor));
        Require(armor-player.GetComponent<PlayerArmor>().CurrentArmor==2,"one real mine hit, unchanged damage 2");player.SetDashInvincible(true);Capture("hazard-mine-cleared");
    }
    static IEnumerable<float> EstablishLock()
    {
        Require(boss.LiveMineCount>0,"live mines before interruption");Require(!boss.IsRailDamaging,"committed lock harmless before interruption");
        Capture(caseLabel+"-lock-before-interruption");yield return 0;
    }
    static void VerifyCleanup()
    {
        Require(RaiderSniperCommanderBossController.ActiveEncounter==null,"arena owner released");
        Require(!Object.FindObjectsByType<RaiderSniperMine>(FindObjectsSortMode.None).Any(v=>v.Owner!=null||v.IsVisible||v.IsArmed),"combat mines cleared");
        Require(!Object.FindObjectsByType<RaiderRailShot>(FindObjectsSortMode.None).Any(v=>v.IsVisible||v.IsDamaging),"rail cleared");
        Require(!hud.IsRegionBossPresentationActive,"HUD released");
        Require(BossHealthBarUI.Instance==null||!BossHealthBarUI.Instance.IsVisible,"HP hidden");
        Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b=>boss!=null&&b.SourceRoot==boss.transform),"source projectiles cleared");
        Require(!Object.FindObjectsByType<PhaseCombatVfx>(FindObjectsSortMode.None).Any(v=>(v.name.Contains("RaiderSniper")||v.name.Contains("RaiderAssault"))&&v.IsVisible),"owned VFX cleared");
        var cameraOwner=GungeonStyleCamera2D.Instance;
        foreach(string f in new[]{"gameplayFramingOwner","cinematicFocusOwner","scriptedVerticalScrollOwner"})Require(Get(cameraOwner,f)==null,"camera releases "+f);
        foreach(var m in Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None))Require(!m.IsSectorEncounterPaused,"meteor state restored");
    }
}
