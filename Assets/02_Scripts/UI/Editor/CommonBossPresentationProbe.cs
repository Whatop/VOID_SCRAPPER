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
public static class CommonBossPresentationProbe
{
    const string SessionKey = "CommonBossPresentation.Probe", PresentationKey = "CommonBossPresentation.Mode";
    static string Dir => "Logs/CommonBossPresentation/" + SessionState.GetString(PresentationKey, "A") + "/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static Component boss;
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
    static float nextInputDiagnostic;
    static CommonBossPresentationProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void RunA() => BeginRun("A");
    public static void RunB() => BeginRun("B");
    public static void RunC() => BeginRun("C");
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
    { Keys(); player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "authored player"); player.SetDashInvincible(true); player.GetComponent<PlayerArmor>().SetMaxArmor(8,true); }
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
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static string region, caseLabel, lastLabel;
    static ExpeditionHUD hud;
    static CoreTrackingSignalController tracking;
    static int signals;
    static SpriteRenderer playerBody;
    static Vector2 opaqueSize;
    static float labelSince, lastDiagnostic, windowHp;
    static int lastExposure;
    static void SampleCombat()
    {
        if(boss==null||player==null||camera==null)return;
        string state=sector!=null?(sector.SectorEscalated?"six-":"four-")+sector.CurrentSectorStage:
            triad!=null?triad.AlivePartCount+"parts-"+triad.CurrentDefenseStage:
            (phase.LensEscalated?"lens-":"normal-")+phase.CurrentRouteStage;
        bool combat=GameStateManager.Instance.CurrentState==GameState.BossBattle&&!bossHealth.IsDead&&!player.IsDead;
        float bodyPixels=Mathf.Min(opaqueSize.x*Mathf.Abs(playerBody.transform.lossyScale.x),opaqueSize.y*Mathf.Abs(playerBody.transform.lossyScale.y))*270/(2*camera.orthographicSize);
        var zoom=Object.FindFirstObjectByType<CameraZoomController2D>();
        bool stable=combat&&!hud.IsCinematicMode&&!zoom.IsCinematicTransitionActive&&zoom.IsAtGameplayZoomWithin(.08f);
        if(Time.time!=lastSample)
        {
            lastSample=Time.time;
            if(lastLabel!=state){lastLabel=state;labelSince=Time.time;}
            motion.Add(caseLabel+","+Time.time.ToString("F4")+","+state+","+camera.orthographicSize.ToString("F4")+","+bodyPixels.ToString("F3")+","+stable+","+hud.IsRegionBossPresentationActive+","+bossHealth.CurrentHp.ToString("F3"));
            if(combat)
            {
                if(!hud.IsRegionBossPresentationActive)throw new Exception("Region HUD scope absent: "+state);
                if(((GameObject)Get(hud,"objectiveRoot")).activeSelf)throw new Exception("Core Signal leaked into boss combat");
                if(!hud.IsOperationBriefingSuppressed)throw new Exception("Briefing not suppressed");
                if(tracking!=null&&tracking.CurrentSignalCount!=signals)throw new Exception("Core Signal state changed during presentation");
                foreach(var npc in Object.FindObjectsByType<FieldNpcObjective>(FindObjectsSortMode.None))
                    if(npc.GetComponentsInChildren<Renderer>(true).Any(v=>!v.forceRenderingOff))throw new Exception("NPC presentation not suppressed");
                if(stable && bodyPixels<6f)throw new Exception("Player below native six-pixel opaque-body threshold: "+bodyPixels+" "+state);
            }
            if(Time.time-labelSince>.25f&&captured.Add(caseLabel+"-"+state))Capture(caseLabel+"-"+state);
        }
        if(autoFire&&!bossHealth.IsDead)
        {
            bool exposed=phase==null||phase.IsExposed;
            Transform aimTarget=targetPart!=null?targetPart.transform:boss.transform;
            if(exposed)
            {
                Move(aimTarget.position+new Vector3(Mathf.Sin(Time.time)*.15f,triad!=null?-2f:-2.5f,0));
                Vector2 aim=camera.WorldToScreenPoint(aimTarget.position);
                var tree=player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
                bool fire=tree==WeaponTreeType.MachineGun||Time.time%.65f<.12f;
                qaMouse.MakeCurrent();InputSystem.QueueStateEvent(qaMouse,new MouseState{position=aim}.WithButton(MouseButton.Left,fire));
            }
            else Mouse(false);
            if(phase!=null && phase.IsExposed && lastExposure!=phase.RouteAttackCount)
            {
                if(lastExposure>=0)Note("EXPOSURE damage since previous="+(windowHp-bossHealth.CurrentHp));
                lastExposure=phase.RouteAttackCount;windowHp=bossHealth.CurrentHp;
            }
            if(phase!=null&&Time.time-lastDiagnostic>10)
            {
                lastDiagnostic=Time.time;
                var weapon=player.GetComponentInChildren<ShotgunWeapon>(true);
                var wc=player.GetComponent<PlayerWeaponController>();
                Note("SHOTGUN DIAGNOSTIC state="+phase.State+" scheduler="+phase.CurrentRouteStage+" cycle="+phase.RouteAttackCount+" portalPair="+phase.PortalEndpointCount+" beam="+(phase.RouteBeam!=null)+" exposed="+phase.IsExposed+" exposureAge="+(Time.time-labelSince)+" HP="+bossHealth.CurrentHp+" player="+player.transform.position+" boss="+phase.transform.position+" routine="+(Get(phase,"encounterRoutine")!=null)+" weaponEnabled="+(weapon!=null&&weapon.enabled)+" inputLocked="+wc.ExternalInputLocked+" nextFire="+(weapon!=null?Get(weapon,"nextFireTime"):null)+" canFire="+typeof(PlayerWeaponController).GetMethod("CanUseWeapon",Flags).Invoke(wc,null));
            }
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
        region=SessionState.GetString(PresentationKey,"A");
        motion.Add("case,time,state,orthographicSize,opaqueBodyPixels,stableCombat,regionHud,hp");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        int wins=region=="C"?3:1;
        for(int attempt=0;attempt<=wins;attempt++)
        {
            bool deathRun=attempt==wins;caseLabel=region+"-"+(deathRun?"player-death":"win"+(attempt+1));weaponLabel=caseLabel;
            autoFire=false;sector=null;triad=null;phase=null;boss=null;bossHealth=null;targetPart=null;lastExposure=-1;lastLabel=null;lastDiagnostic=0;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            if(region!="A"){PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();}
            if(region=="C"){PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();}
            var tree=region=="C"?WeaponTreeType.Shotgun:WeaponTreeType.MachineGun;
            var depth=region=="A"?ExpeditionDepth.Normal:region=="B"?ExpeditionDepth.DeepZone1:ExpeditionDepth.DeepZone2;
            RunManager.Instance.StartNewRun(tree,depth);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            hud=Object.FindFirstObjectByType<ExpeditionHUD>();tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();CacheBody();
            var map=Object.FindFirstObjectByType<ExpeditionMapGenerator>();
            if(region=="C")
            {
                phase=map.CurrentRegion3BossEncounter;boss=phase;bossHealth=phase.GetComponent<EnemyHealth>();signals=tracking!=null?tracking.CurrentSignalCount:0;
                Move(phase.EncounterAnchor+Vector2.down*2.5f);
                foreach(var d in Until(()=>phase.State==PhaseGatekeeperBossController.EncounterState.Intro,"normal proximity intro"))yield return d;
                yield return .65f; // Capture the visible decloak/title, not its first invisible frame.
                Capture(caseLabel+"-introduction");
                foreach(var d in Until(()=>phase.RouteAttackCount>0,"live Phase Gatekeeper"))yield return d;
            }
            else
            {
                var core=Object.FindFirstObjectByType<CoreObject>();Require(core!=null,"generated Core");
                if(tracking!=null&&tracking.IsTrackingActive)
                    foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
                    {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
                signals=tracking!=null?tracking.CurrentSignalCount:0;Capture(caseLabel+"-exploration-before");
                Move(core.transform.position+Vector3.down*.6f);yield return .2f;core.Interact(player.gameObject);
                foreach(var d in Until(()=>
                {
                    if(!(bool)Get(core,"activated")){Move(core.transform.position+Vector3.down*.6f);if(core.CanInteract(player.gameObject))core.Interact(player.gameObject);}
                    sector=region=="A"?Object.FindFirstObjectByType<BossPatternController>():null;
                    triad=region=="B"?Object.FindFirstObjectByType<FrigateTriadBossController>():null;
                    return sector!=null||triad!=null;
                },"actual Core intro spawned boss"))yield return d;
                boss=sector!=null?(Component)sector:triad;bossHealth=boss.GetComponent<EnemyHealth>();
                if(triad!=null){foreach(var d in Until(()=>triad.IsEntryComplete,"three frigates entry"))yield return d;}
                else {foreach(var d in Until(()=>camera.WorldToViewportPoint(boss.transform.position).y<.85f,"boss intro entry"))yield return d;}
                Capture(caseLabel+"-introduction");
                foreach(var d in Until(()=>sector!=null?sector.SectorCycle>0:triad.IsGameplayActive&&triad.DefenseCycle>0,"normal combat handoff"))yield return d;
            }
            yield return 1;
            Require(hud.IsRegionBossPresentationActive,"shared HUD owns boss scope");
            Require(((HashSet<object>)Get(player.GetComponent<PlayerInteractor>(),"externalInputLocks")).Contains(hud),"ordinary NPC interaction access suppressed");
            Capture(caseLabel+"-player-armor-core-suppressed");
            var prompt=Object.FindFirstObjectByType<InteractionPromptUI>(FindObjectsInactive.Include);prompt.SetVisible(true);
            Require(((CanvasGroup)Get(prompt,"canvasGroup")).alpha==0,"ordinary interaction suppressed");
            hud.ShowBossCommunication("SYSTEM: navigation control engaged.",1.5f);hud.ShowWarning("Ordinary NPC warning must not replace boss communication.");
            yield return .3f;Capture(caseLabel+"-communication-priority");yield return 1.6f;
            if(deathRun)
            {
                player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);player.GetComponent<PlayerArmor>().SetMaxArmor(0,true);player.TakeDamage(99999);
                foreach(var d in Until(()=>RunManager.Instance.CurrentRun==null||!RunManager.Instance.HasActiveRun,"normal player death result"))yield return d;
                yield return .5f;VerifyCleanup();Capture(caseLabel+"-cleanup");
                var resultPanel=Object.FindFirstObjectByType<RunResultPanelUI>();Require(resultPanel!=null,"normal result panel");resultPanel.Close();
            }
            else
            {
                if(sector!=null)
                {
                    foreach(var d in Until(()=>sector.SectorCycle>=3,"two Phase 1 cycles"))yield return d;
                    autoFire=true;foreach(var d in Until(()=>sector.SectorEscalated,"weapon-triggered six-spoke escalation",180))yield return d;
                    autoFire=false;Mouse(false);
                    foreach(var d in Until(()=>sector.CurrentSectorStage==BossPatternController.SectorStage.Recovery,"six-spoke recovery"))yield return d;
                    autoFire=true;
                }
                else if(triad!=null)
                {
                    foreach(int alive in new[]{3,2,1})
                    {
                        foreach(var d in Until(()=>triad.AlivePartCount==alive&&triad.State!=FrigateTriadBossState.Transition,"formation "+alive))yield return d;
                        int cycle=triad.DefenseCycle;foreach(var d in Until(()=>triad.DefenseCycle>=cycle+1,"complete formation cycle"))yield return d;
                        Capture(caseLabel+"-formation-"+alive);
                        targetPart=((FrigateBossPart[])Get(triad,"parts")).First(p=>p.IsAlive);autoFire=true;
                        foreach(var d in Until(()=>bossHealth.IsDead||!targetPart.IsAlive,"actual weapon destroys frigate",180))yield return d;
                        autoFire=false;Mouse(false);
                    }
                }
                else
                {
                    foreach(var d in Until(()=>phase.RouteAttackCount>=6,"portal/lens cycles"))yield return d;autoFire=true;
                }
                foreach(var d in Until(()=>bossHealth==null||bossHealth.IsDead,"actual weapon victory",420))yield return d;
                autoFire=false;Mouse(false);yield return .5f;
                Require(!hud.IsRegionBossPresentationActive,"boss HUD scope releases at death");
                Capture(caseLabel+"-death");yield return 8;VerifyCleanup();Capture(caseLabel+"-reward-exploration-restored");
                Note("COMPLETED "+caseLabel+": actual generated encounter, unchanged weapon inputs, existing death/reward. Invulnerable/assisted coverage.");
                SceneFlowManager.Instance.LoadSettlement();
            }
            boss=null;sector=null;triad=null;phase=null;bossHealth=null;targetPart=null;
            foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
            Require(!Object.FindObjectsByType<BossHealthBarUI>(FindObjectsSortMode.None).Any(v=>v.IsVisible),"no stale boss HUD in Settlement");
            Capture(caseLabel+"-settlement-return");
        }
        Note("Completed "+region+" common presentation validation. Victory uses QA scene return; death uses normal result return. Post-victory reward choice/portal traversal not exercised.");
    }
    static void VerifyCleanup()
    {
        Require(!hud.IsRegionBossPresentationActive&&!hud.IsOperationBriefingSuppressed,"presentation/briefing scope released");
        Require(!((HashSet<object>)Get(player.GetComponent<PlayerInteractor>(),"externalInputLocks")).Contains(hud),"ordinary interaction access restored");
        Require(BossHealthBarUI.Instance==null||!BossHealthBarUI.Instance.IsVisible,"boss HUD hidden");
        var cameraOwner=GungeonStyleCamera2D.Instance;
        foreach(string field in new[]{"gameplayFramingOwner","cinematicFocusOwner","scriptedVerticalScrollOwner"})
            Require(Get(cameraOwner,field)==null,"camera releases "+field);
        var zoom=Object.FindFirstObjectByType<CameraZoomController2D>();Require(!zoom.IsCinematicZoomHeld,"no stale cinematic zoom hold");
        foreach(var npc in Object.FindObjectsByType<FieldNpcObjective>(FindObjectsSortMode.None))
            Require(npc.GetComponentsInChildren<Renderer>(true).All(v=>!v.forceRenderingOff),"NPC rendering restored");
        Require(!RunManager.Instance.HasActiveRun||tracking==null||tracking.CurrentSignalCount==signals,"Core Signal count preserved until run ends");
    }
}
