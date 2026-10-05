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
public static class RaiderReworkProbe
{
    const string SessionKey = "RaiderRework.Probe", PresentationKey = "RaiderRework.Mode";
    static string Dir => SessionState.GetString(PresentationKey, "A") == "Lockdown"
        ? "Logs/RaiderLockdown/Native/" : "Logs/RaiderRework/" + SessionState.GetString(PresentationKey, "A") + "/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static PirateCommanderBossController boss;
    static BossPatternController sector;
    static FrigateTriadBossController triad;
    static PhaseGatekeeperBossController phase;
    static FrigateBossPart targetPart;
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
    static InputAction fixtureFire;
    static bool desiredFire, moveNormally;
    static KeyboardState movementKeys;
    static InputAction fixtureMove;
    static Vector2 ramDodge;
    static PirateCommanderBossController.AssaultStage movementStage;
    static int hitCount;
    static float lastPlayerHp;
    static Vector3[] fixedWalls;
    static CoreBossIntroSequence intro;
    static Vector2 desiredAim=new Vector2(240,135);
    static InputSettings.UpdateMode previousUpdateMode;
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
            updated[0]=new UnityEngine.LowLevel.PlayerLoopSystem {type=typeof(RaiderReworkProbe),updateDelegate=FeedGameFrame};
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
        if (moveNormally && boss != null && player != null && !player.IsDead) DriveMovement();
        qaKeyboard.MakeCurrent(); InputSystem.QueueStateEvent(qaKeyboard,movementKeys);
        if (fixtureMove != null && player != null) typeof(PlayerController2D).GetField("moveAction",Flags).SetValue(player.GetComponent<PlayerController2D>(),fixtureMove);
        qaMouse.MakeCurrent();
        InputSystem.QueueStateEvent(qaMouse,new MouseState{position=desiredAim}.WithButton(MouseButton.Left,desiredFire));
        InputSystem.Update();
    }
    static float nextInputDiagnostic;
    static RaiderReworkProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run() => BeginRun("Native");
    public static void RunLockdown() => BeginRun("Lockdown");
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
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            qaMouse = InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("SectorQAMouse");
            qaKeyboard = InputSystem.AddDevice<Keyboard>("SectorQAKeyboard");
            previousUpdateMode=InputSystem.settings.updateMode; InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            sequence = (SessionState.GetString(PresentationKey, "A") == "Lockdown" ? CheckLockdown() : Check()).GetEnumerator();
            end = EditorApplication.timeSinceStartup + 2400; EditorApplication.update += Tick;
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
        finishing = true; EditorApplication.update -= Tick; if(inputLoopInstalled)UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(previousLoop); inputLoopInstalled=false; InputSystem.settings.updateMode=previousUpdateMode; fixtureFire?.Dispose(); fixtureFire=null;fixtureMove?.Dispose();fixtureMove=null;
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
    { Keys(); player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "authored player"); player.SetDashInvincible(false);
        player.GetComponent<PlayerController2D>().InputActions.devices=new InputDevice[]{qaMouse,qaKeyboard};
        var wc=player.GetComponent<PlayerWeaponController>();var original=(InputAction)Get(wc,"fireAction");
        Note("FIRE BINDING before isolation: enabled="+original.enabled+" phase="+original.phase+" bindings="+string.Join(";",original.bindings.Select(b=>b.effectivePath)));
        fixtureFire?.Dispose();fixtureFire=original.Clone();fixtureFire.Enable();
        typeof(PlayerWeaponController).GetField("fireAction",Flags).SetValue(wc,fixtureFire);
        fixtureMove?.Dispose();fixtureMove=((InputAction)Get(player.GetComponent<PlayerController2D>(),"moveAction")).Clone();fixtureMove.Enable();
        lastPlayerHp=player.CurrentHp;hitCount=0;
        InstallInputLoop(); }
    static void Move(Vector3 p)
    { var rb = player.GetComponent<Rigidbody2D>(); rb.position = p; player.transform.position = p; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
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
    static void SampleCombat()
    {
        if(!observe||boss==null||player==null||camera==null)return;
        string state=(boss.IsPhase2?"critical-":"normal-")+(boss.IsWeaponsHot?"hot-":"")+boss.Stage;
        bool combat=boss.IsCombatActive&&!bossHealth.IsDead&&!player.IsDead;
        float pixels=Mathf.Min(opaqueSize.x*Mathf.Abs(playerBody.transform.lossyScale.x),opaqueSize.y*Mathf.Abs(playerBody.transform.lossyScale.y))*270/(2*camera.orthographicSize);
        if(Time.time!=lastSample)
        {
            lastSample=Time.time;
            if(state!=lastLabel){lastLabel=state;labelSince=Time.time;Note("STATE "+caseLabel+" "+Time.time.ToString("F4")+" "+state);}
            int count=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b=>b.SourceRoot==boss.transform);peak=Mathf.Max(peak,count);
            motion.Add(caseLabel+","+Time.time.ToString("F4")+","+state+","+boss.ShotsFired+","+count+","+camera.orthographicSize.ToString("F4")+","+pixels.ToString("F3")+","+bossHealth.CurrentHp.ToString("F3")+","+boss.transform.position.x+","+boss.transform.position.y+","+boss.LastMount+","+player.CurrentHp+","+boss.ShieldHp+","+boss.RamHitPlayer);
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
            if(Time.time-labelSince>.12f&&captured.Add(caseLabel+"-"+state))Capture(caseLabel+"-"+state);
            if(boss.ShotsFired!=lastShots)
            {
                Note("SHOT "+caseLabel+" t="+Time.time.ToString("F4")+" dt="+(Time.time-lastShotTime).ToString("F4")+" mount="+boss.LastMount+" total="+boss.ShotsFired+" hot="+boss.IsWeaponsHot+" target="+boss.CommittedTarget);
                lastShotTime=Time.time;lastShots=boss.ShotsFired;
            }
        }
        if (player.CurrentHp < lastPlayerHp) { hitCount++; Note("PLAYER HIT "+caseLabel+" hp="+player.CurrentHp+" state="+state); }
        lastPlayerHp=player.CurrentHp;
        Require(!player.IsDead,"normal health attempt survived; inspect recorded hit count");
        if (boss.IsCombatActive)
        {
            var view=boss.GetComponentInChildren<RaiderShieldPresentation>(true);
            Require(!view.NormalVisible || boss.IsShieldProtected,"no false normal Shield");
            Require(view.LaneVisible == (boss.Stage==PirateCommanderBossController.AssaultStage.RamWarning),"warning exists only before actual motion");
            if(boss.IsRamPunish)Require(!boss.IsShieldProtected,"Ram punish removes protection");
            if(boss.Stage==PirateCommanderBossController.AssaultStage.RamWarning && Time.time-labelSince>.4f)
            {
                foreach(var point in new[]{boss.RamStart-boss.RamDirection*1.12f,boss.RamEnd+boss.RamDirection*1.12f})
                {
                    var v=camera.WorldToViewportPoint(point);
                    Require(v.x>.02f&&v.x<.98f&&v.y>.02f&&v.y<.94f,"committed Ram path fits the camera before motion: "+v);
                }
                if(captured.Add(caseLabel+"-"+state+"-held"))Capture(caseLabel+"-"+state+"-held");
            }
            if(fixedWalls!=null)
            {
                var walls=Object.FindObjectsByType<BossArenaLaserWall>(FindObjectsSortMode.InstanceID);
                Require(walls.Length==fixedWalls.Length,"fixed wall count");
                for(int i=0;i<walls.Length;i++)Require(Vector3.Distance(walls[i].transform.position,fixedWalls[i])<.001f,"no moving gameplay boundary");
            }
        }
    }
    static void DriveMovement()
    {
        Vector2 away=(Vector2)player.transform.position-(Vector2)boss.transform.position;
        Vector2 direction;
        if(boss.Stage==PirateCommanderBossController.AssaultStage.RamWarning && movementStage!=boss.Stage)
        {
            ramDodge=new Vector2(-boss.RamDirection.y,boss.RamDirection.x);
            Vector2 center=boss.ArenaBounds.center;
            if(Vector2.Dot(ramDodge,center-(Vector2)player.transform.position)<0)ramDodge=-ramDodge;
        }
        if(boss.Stage==PirateCommanderBossController.AssaultStage.RamWarning || boss.Stage==PirateCommanderBossController.AssaultStage.RamActive)direction=ramDodge;
        else direction=new Vector2(-away.y,away.x).normalized + away.normalized*Mathf.Clamp(4.5f-away.magnitude,-1,1);
        Vector2 toCenter=(Vector2)boss.ArenaBounds.center-(Vector2)player.transform.position;
        if(Mathf.Abs(toCenter.x)>5.5f || Mathf.Abs(toCenter.y)>5.5f)direction+=toCenter.normalized*1.6f;
        var keys=new List<Key>();if(direction.x>.25f)keys.Add(Key.D);if(direction.x<-.25f)keys.Add(Key.A);if(direction.y>.25f)keys.Add(Key.W);if(direction.y<-.25f)keys.Add(Key.S);
        movementKeys=new KeyboardState(keys.ToArray());movementStage=boss.Stage;
        desiredAim=camera.WorldToScreenPoint(boss.transform.position);
        desiredFire=autoFire || boss.IsRamPunish;
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
        motion.Add("case,time,state,shots,liveProjectiles,orthographicSize,playerPixels,bossHp,bossX,bossY,mount,playerHp,shieldHp,ramContact");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        for(int attempt=0;attempt<4;attempt++)
        {
            caseLabel=new[]{"patterns","player-death","abort","intro-death"}[attempt];
            autoFire=observe=moveNormally=false;boss=null;bossHealth=null;fixedWalls=null;lastLabel=null;lastShots=peak=0;movementKeys=new KeyboardState();
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            hud=Object.FindFirstObjectByType<ExpeditionHUD>();tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();CacheBody();
            var core=Object.FindFirstObjectByType<CoreObject>();intro=core.GetComponent<CoreBossIntroSequence>();Require(core.UsesGrayCore,"actual Gray repeat routing");
            if(tracking!=null&&tracking.IsTrackingActive)
                foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
                {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
            Move(core.transform.position+Vector3.down*.6f);yield return .2f;Capture(caseLabel+"-GrayCore");core.Interact(player.gameObject);
            foreach(var d in Until(()=>
            {
                if(!(bool)Get(core,"activated")){Move(core.transform.position+Vector3.down*.6f);if(core.CanInteract(player.gameObject))core.Interact(player.gameObject);}
                boss=Object.FindFirstObjectByType<PirateCommanderBossController>();return boss!=null;
            },"actual repeat Commander spawn"))yield return d;
            bossHealth=boss.GetComponent<EnemyHealth>();
            foreach(var d in Until(()=>boss.IsShieldProtected,"arrival then real Shield"))yield return d;Capture(caseLabel+"-arrival-shield");
            foreach(var d in Until(()=>intro.RaiderLockdownForming,"temporary siege"))yield return d;
            Require(Object.FindObjectsByType<RaiderBarricadeCarrier>(FindObjectsSortMode.None).Length==0,"Commander uses no barrier craft");
            fixedWalls=Object.FindObjectsByType<BossArenaLaserWall>(FindObjectsSortMode.InstanceID).Select(w=>w.transform.position).ToArray();Require(fixedWalls.Length==4,"four fixed walls before contraction");
            Capture(caseLabel+"-siege-start");yield return .4f;Capture(caseLabel+"-siege-mid");
            if(attempt==3)
            {
                KillPlayer();yield return 1;Require(!boss.IsShieldProtected&&!intro.RaiderLockdownForming&&!boss.IsCombatActive,"intro-death cleanup");
                Object.FindFirstObjectByType<RunResultPanelUI>()?.Close();
            }
            else
            {
                foreach(var d in Until(()=>boss.IsCombatActive,"combat handoff"))yield return d;
                Require(!intro.RaiderLockdownForming,"siege gone before combat");Capture(caseLabel+"-combat");
                if(attempt==0)
                {
                    // One authored player projectile isolates the real absorption path.
                    var definition=AssetDatabase.LoadAssetAtPath<ProjectileDefinition>("Assets/02_Scripts/Config/ProjectileDefinition/Projectile_MachineGun.asset");
                    var shot=PoolManager.Instance.Get(definition.ProjectilePrefab,boss.transform.position+Vector3.down*2,Quaternion.identity).GetComponent<Bullet>();
                    shot.Initialize(Vector2.up,ProjectileOwner.Player,definition,projectileSource:player.gameObject);
                    yield return .10f;
                    int shields=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name=="ShieldHit(Clone)");
                    int generic=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name=="GenericHit(Clone)");
                    Require(boss.AbsorbedHitSequence==1&&bossHealth.CurrentHp==125,"one actual projectile absorption");
                    Require(shields==1&&generic==0,"one Shield Hit and zero duplicate Generic Hit: "+shields+"/"+generic);
                    Note("SHIELD projectile verification: absorptions=1 ShieldHit="+shields+" GenericHit="+generic+" HP="+bossHealth.CurrentHp);Capture("shield-hit-absorption");
                }
                observe=moveNormally=true;
                if(attempt==0)
                {
                    foreach(var d in Until(()=>boss.AssaultCycle>=2,"full normal loop",80))yield return d;
                    Note("NORMAL HEALTH normal loop hp="+player.CurrentHp+" hits="+hitCount+" RamContact="+boss.RamHitPlayer);
                    Require(!boss.RamHitPlayer,"sideways movement dodges the committed Ram");
                    Require(bossHealth.CurrentHp<125,"ordinary player weapon deals HP damage during the punish window");
                    bossHealth.TakeDamage(18);bossHealth.TakeDamage(70); // deterministic low-health setup, no player assistance
                    foreach(var d in Until(()=>boss.IsPhase2,"critical boundary",40))yield return d;
                    int cycle=boss.AssaultCycle;foreach(var d in Until(()=>boss.AssaultCycle>cycle,"full critical loop",80))yield return d;
                    Note("NORMAL HEALTH critical loop hp="+player.CurrentHp+" hits="+hitCount+" peakBullets="+peak);
                    observe=moveNormally=false;movementKeys=new KeyboardState();
                    bossHealth.TakeDamage(99999);bossHealth.TakeDamage(99999);yield return .4f;Capture("boss-death");yield return 7;VerifyCleanup();Capture("reward");
                    Require(RunManager.Instance.CurrentRun.BossDefeated,"reward authority");SceneFlowManager.Instance.LoadSettlement();
                }
                else
                {
                    if(attempt==1)
                    {
                        foreach(var d in Until(()=>boss.Stage==PirateCommanderBossController.AssaultStage.RamAim,"stationary Ram challenge",60))yield return d;
                        moveNormally=false;movementKeys=new KeyboardState();float before=player.CurrentHp;
                        foreach(var d in Until(()=>boss.IsRamPunish,"stationary Ram impact",10))yield return d;
                        Require(boss.RamHitPlayer&&player.CurrentHp<before,"remaining in the warned path causes actual Ram damage");
                        Note("STATIONARY RAM hp before="+before+" after="+player.CurrentHp+"; single committed contact="+boss.RamHitPlayer);Capture("stationary-Ram-impact");
                        moveNormally=true;
                    }
                    foreach(var d in Until(()=>boss.Stage==PirateCommanderBossController.AssaultStage.RamWarning,"interrupt visible Ram telegraph",60))yield return d;
                    observe=moveNormally=false;movementKeys=new KeyboardState();
                    if(attempt==1){KillPlayer();yield return 1;VerifyCleanup();Object.FindFirstObjectByType<RunResultPanelUI>()?.Close();}
                    else {boss.gameObject.SetActive(false);core.gameObject.SetActive(false);yield return .2f;VerifyCleanup();SceneFlowManager.Instance.LoadSettlement();}
                }
            }
            observe=moveNormally=autoFire=false;boss=null;bossHealth=null;
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
            Require(!Object.FindObjectsByType<RaiderShieldPresentation>(FindObjectsSortMode.None).Any(v=>v.NormalVisible||v.LaneVisible),"Settlement no stale Shield/lane");Capture(caseLabel+"-settlement");
        }
        Note("Real Region A repeat routing. Normal-health keyboard movement; no combat teleport or invulnerability. Low-health and boss-death damage injected for deterministic coverage. Human difficulty acceptance remains manual.");
    }
    static IEnumerable<float> CheckLockdown()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        for(int attempt=0;attempt<4;attempt++)
        {
            caseLabel=new[]{"lockdown","combat-death","abort","formation-death"}[attempt];
            autoFire=observe=moveNormally=false;boss=null;bossHealth=null;fixedWalls=null;movementKeys=new KeyboardState();
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            hud=Object.FindFirstObjectByType<ExpeditionHUD>();tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();
            var core=Object.FindFirstObjectByType<CoreObject>();intro=core.GetComponent<CoreBossIntroSequence>();Require(core.UsesGrayCore,"actual Gray repeat route");
            if(tracking!=null&&tracking.IsTrackingActive)
                foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
                {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
            Move(core.transform.position+Vector3.down*.6f);yield return .2f;Capture(caseLabel+"-01-GrayCore");
            core.Interact(player.gameObject);
            foreach(var d in Until(()=>
            {
                if(!(bool)Get(core,"activated")){Move(core.transform.position+Vector3.down*.6f);if(core.CanInteract(player.gameObject))core.Interact(player.gameObject);}
                boss=Object.FindFirstObjectByType<PirateCommanderBossController>();return boss!=null;
            },"Commander arrives"))yield return d;
            bossHealth=boss.GetComponent<EnemyHealth>();Capture(caseLabel+"-02-arrival");
            Require(Walls().Length==0,"boss arrives before boundary formation");
            foreach(var d in Until(()=>boss.IsShieldProtected,"real Shield and boss pulse"))yield return d;
            Capture(caseLabel+"-03-shield-pulse");
            foreach(var d in Until(()=>intro.RaiderLockdownForming,"formation starts"))yield return d;
            float formationStart=Time.unscaledTime;
            fixedWalls=Walls().Select(w=>w.transform.position).ToArray();Require(fixedWalls.Length==4,"four authoritative edges");
            VerifyLockdown(false);Capture(caseLabel+"-04-growth-start");
            foreach(float progress in new[]{.25f,.55f,.85f})
            {
                foreach(var d in Until(()=>intro.RaiderLockdownProgress>=progress,"growth "+progress,5))yield return d;
                VerifyLockdown(false);Capture(caseLabel+"-05-growth-"+progress.ToString("F2"));
                if(attempt==3)break;
            }
            if(attempt==3)
            {
                KillPlayer();yield return .25f;VerifyLockdownGone();Capture(caseLabel+"-interrupted");
                yield return 1;Object.FindFirstObjectByType<RunResultPanelUI>()?.Close();
            }
            else
            {
                foreach(var d in Until(()=>!intro.RaiderLockdownForming,"four edges close",5))yield return d;
                Note("FORMATION observed seconds="+(Time.unscaledTime-formationStart).ToString("F3"));
                VerifyLockdown(true);Capture(caseLabel+"-06-closed");
                foreach(var d in Until(()=>boss.IsCombatActive,"combat handoff"))yield return d;
                VerifyLockdown(true);Require(Vector2.Distance(boss.ArenaBounds.size,new Vector2(16.8f,16.8f))<.001f,"original arena size");
                Capture(caseLabel+"-07-combat");
                if(attempt==0)
                {
                    // Assisted placement, then real Input System movement against the live collider.
                    var b=boss.ArenaBounds;float right=b.max.x;
                    Move(new Vector3(right-.7f,b.center.y-2,0));movementKeys=new KeyboardState(Key.D);
                    yield return .9f;movementKeys=new KeyboardState();
                    Require(player.transform.position.x<right-.15f,"player blocked by solid wall");
                    Require(!player.IsDead,"normal-health boundary attempt");
                    Note("BOUNDARY right="+right+" playerX="+player.transform.position.x+" HP="+player.CurrentHp+" solid=true ricochet=false");Capture("lockdown-08-live-wall-contact");
                    Move(new Vector3(b.center.x,b.center.y-3,0));moveNormally=true;
                    foreach(var d in Until(()=>boss.AssaultCycle>=2,"normal combat loop",80))yield return d;
                    Require(!player.IsDead,"normal-health movement survives one loop");VerifyLockdown(true);
                    Note("NORMAL HEALTH loop completed HP="+player.CurrentHp+" cycle="+boss.AssaultCycle);
                    moveNormally=false;movementKeys=new KeyboardState();Move(new Vector3(right-1,b.max.y-1,0));yield return .45f;
                    Capture("lockdown-09-connected-corner");
                    bossHealth.TakeDamage(99999);bossHealth.TakeDamage(99999);
                    Require(intro.RaiderLockdownReleasing,"death starts finite visible release");
                    Require(Walls().All(w=>!w.GetComponent<BoxCollider2D>().enabled),"no collision during visible release");
                    float releaseStart=Time.unscaledTime;Capture("lockdown-10-release-start");
                    yield return .13f;Capture("lockdown-11-nodes-power-down");
                    yield return .15f;Capture("lockdown-12-lines-disconnect");
                    foreach(var d in Until(()=>!intro.RaiderLockdownReleasing,"visible release completes",4))yield return d;
                    Note("RELEASE observed seconds="+(Time.unscaledTime-releaseStart).ToString("F3"));
                    yield return .05f;VerifyLockdownGone();Capture("lockdown-13-released");
                    yield return 7;VerifyCleanup();Require(RunManager.Instance.CurrentRun.BossDefeated,"existing reward authority");Capture("lockdown-14-reward");
                    SceneFlowManager.Instance.LoadSettlement();
                }
                else if(attempt==1)
                {
                    KillPlayer();
                    foreach(var d in Until(()=>Walls().Length==0,"existing death sequence releases encounter",8))yield return d;
                    VerifyLockdownGone();VerifyCleanup();Capture(caseLabel+"-clean");
                    yield return 1;Object.FindFirstObjectByType<RunResultPanelUI>()?.Close();
                }
                else
                {
                    boss.gameObject.SetActive(false);core.gameObject.SetActive(false);yield return .2f;
                    VerifyLockdownGone();VerifyCleanup();Capture(caseLabel+"-clean");SceneFlowManager.Instance.LoadSettlement();
                }
            }
            moveNormally=autoFire=observe=false;boss=null;bossHealth=null;
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
            VerifyLockdownGone();Require(!Object.FindObjectsByType<RaiderShieldPresentation>(FindObjectsSortMode.None).Any(v=>v.NormalVisible||v.LaneVisible),"no stale shield in Settlement");
            Capture(caseLabel+"-settlement");
        }
        Note("ASSISTED validation: real repeat route, normal player health and keyboard movement; setup teleport, injected death, automatic dialogue and scene navigation. No human difficulty approval.");
    }
    static BossArenaLaserWall[] Walls()=>Object.FindObjectsByType<BossArenaLaserWall>(FindObjectsSortMode.InstanceID);
    static void VerifyLockdown(bool complete)
    {
        Require(Object.FindObjectsByType<RaiderBarricadeCarrier>(FindObjectsSortMode.None).Length==0,"no separate barrier craft");
        var walls=Walls();Require(walls.Length==4,"exactly four edges");
        for(int i=0;i<walls.Length;i++)
        {
            var wall=walls[i];var view=wall.GetComponent<RaiderLockdownBoundaryPresentation>();
            Require(Vector3.Distance(wall.transform.position,fixedWalls[i])<.001f,"collision never moves");
            Require(view!=null&&wall.GetComponent<RaiderArenaBoundaryPresentation>()==null,"only new region-style boundary");
            Require(wall.GetComponent<BoxCollider2D>().enabled==complete,"collision enables only on complete closure");
            Require(!wall.GetComponent<BoxCollider2D>().isTrigger&&!wall.AllowsProjectileRicochet,"original solid/non-ricochet rules");
            Require(wall.transform.Find("LockdownEnergyNodes").GetComponentsInChildren<Collider2D>().Length==0,"nodes do not own collision");
            if(complete)Require(view.LitNodeCount==23&&Mathf.Approximately(view.Progress,1),"all nodes connected");
            if(complete&&!boss.IsCombatActive)
                for(int endpoint=0;endpoint<2;endpoint++)
                {
                    var viewport=camera.WorldToViewportPoint(wall.transform.TransformPoint(wall.GetOrCreateLineRenderer().GetPosition(endpoint)));
                    Require(viewport.x>.01f&&viewport.x<.99f&&viewport.y>.01f&&viewport.y<.99f,"entire closed rectangle visible in modest intro framing: "+viewport);
                }
        }
        Note("BOUNDARY sample progress="+intro.RaiderLockdownProgress.ToString("F3")+" collision="+complete+" nodes="+walls[0].GetComponent<RaiderLockdownBoundaryPresentation>().LitNodeCount);
    }
    static void VerifyLockdownGone()
    {
        Require(Walls().Length==0,"no stale wall");
        Require(Object.FindObjectsByType<RaiderLockdownBoundaryPresentation>(FindObjectsSortMode.None).Length==0,"no stale lockdown view");
        Require(Object.FindObjectsByType<RaiderBarricadeCarrier>(FindObjectsSortMode.None).Length==0,"no stale craft");
        if(intro!=null)Require(!intro.RaiderLockdownForming&&!intro.RaiderLockdownReleasing,"finite animation ownership cleared");
    }
    static void KillPlayer()
    {player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);player.GetComponent<PlayerArmor>().SetMaxArmor(0,true);player.TakeDamage(99999);}
    static void VerifyCleanup()
    {
        Require(PirateCommanderBossController.ActiveAssaultEncounter==null,"arena owner released");Require(!hud.IsRegionBossPresentationActive,"HUD released");
        Require(BossHealthBarUI.Instance==null||!BossHealthBarUI.Instance.IsVisible,"HP hidden");
        Require(!Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b=>boss!=null&&b.SourceRoot==boss.transform),"source projectiles cleared");
        Require(!Object.FindObjectsByType<PhaseCombatVfx>(FindObjectsSortMode.None).Any(v=>v.name.Contains("RaiderAssault")&&v.IsVisible),"owned VFX cleared");
        if(boss!=null){var view=boss.GetComponentInChildren<RaiderShieldPresentation>(true);Require(!view.NormalVisible&&!view.LaneVisible,"shield and telegraph cleared");}
        var cameraOwner=GungeonStyleCamera2D.Instance;
        foreach(string f in new[]{"gameplayFramingOwner","cinematicFocusOwner","scriptedVerticalScrollOwner"})Require(Get(cameraOwner,f)==null,"camera releases "+f);
        foreach(var m in Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None))Require(!m.IsSectorEncounterPaused,"meteor state restored");
    }
}
