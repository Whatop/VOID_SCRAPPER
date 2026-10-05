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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit opt-in fixture: real Boot/Expedition Play Mode, disposable save, no saved scene writes.
[InitializeOnLoad]
public static class FeedbackPolish2Probe
{
    const string Key="FeedbackPolish2.Probe";
    static string Dir => SessionState.GetBool("FeedbackPolish2.Tutorial",false) ? "Logs/FeedbackPolish2/Tutorial/" : "Logs/FeedbackPolish2/Play/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> notes=new List<string>(), errors=new List<string>(), audio=new List<string>();
    static IEnumerator<float> sequence;
    static double next, deadline;
    static bool finishing;
    static Camera camera;
    static RenderTexture target;
    static PlayerHealth player;
    static FeedbackPolish2Probe()
    {
        EditorApplication.playModeStateChanged+=Changed;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=Logged;
    }
    public static void RunTutorial() { SessionState.SetBool("FeedbackPolish2.Tutorial",true); Run(); }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Dir+"Rendered");
        typeof(CommonPresentationProbe).GetMethod("SetNativeGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        File.Copy("Logs/SectorAdministrator/fresh-save.json",Dir+"probe-save.json",true);
        File.Copy("Logs/SectorAdministrator/fresh-save.json",Dir+"probe-save.json.bak",true);
        var scene=EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity");
        var save=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SaveManager>(true)).Single();
        var so=new SerializedObject(save); so.FindProperty("fileName").stringValue=Path.GetFullPath(Dir+"probe-save.json"); so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground=true; sequence=(SessionState.GetBool("FeedbackPolish2.Tutorial",false)?CheckTutorial():Check()).GetEnumerator(); deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            Note("Console errors including teardown="+errors.Count); Flush();
            SessionState.SetBool(Key,false); EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
    static void Logged(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);
        if(message.StartsWith("[Audio]"))audio.Add(Time.time.ToString("F3")+" "+message);
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(finishing)return;
        try
        {
            if(errors.Count>0){Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;
            Require(EditorApplication.timeSinceStartup<deadline,"Fixture deadline");
            if(sequence.MoveNext())next=EditorApplication.timeSinceStartup+sequence.Current;else Finish();
        }
        catch(Exception e){errors.Add(e.ToString());Finish();}
    }
    static void Finish()
    {
        finishing=true; EditorApplication.update-=Tick;
        Flush(); EditorApplication.ExitPlaymode();
    }
    static void Note(string text){notes.Add(DateTime.UtcNow.ToString("HH:mm:ss")+" "+text);Flush();}
    static void Flush(){File.WriteAllLines(Dir+"playmode.txt",notes);File.WriteAllLines(Dir+"errors.txt",errors);File.WriteAllLines(Dir+"audio-events.txt",audio);}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static object Get(object o,string field)
    {
        for(var type=o.GetType();type!=null;type=type.BaseType)
        { var info=type.GetField(field,Flags);if(info!=null)return info.GetValue(o); }
        throw new MissingFieldException(o.GetType().Name,field);
    }
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
    static IEnumerable<float> Until(Func<bool> condition,string label,float timeout=60)
    {
        double limit=EditorApplication.timeSinceStartup+timeout;
        while(!condition()){Require(EditorApplication.timeSinceStartup<limit,label);yield return .01f;}
        Note("PASS "+label);
    }
    static IEnumerable<float> Scene(string name)
    {
        foreach(var d in Until(()=>SceneManager.GetActiveScene().name==name&&!SceneFlowManager.Instance.IsLoading,name))yield return d;
        yield return 2;
    }
    static IEnumerable<float> Dialogue()
    {
        double limit=EditorApplication.timeSinceStartup+90;
        while(DialogueManager.isConversationActive)
        {
            Require(EditorApplication.timeSinceStartup<limit,"Dialogue completion");
            var ui=DialogueManager.dialogueUI as StandardDialogueUI;
            if(ui!=null)
            {
                var choices=ui.GetComponentsInChildren<StandardUIResponseButton>(true).Where(x=>x.gameObject.activeInHierarchy&&x.response!=null).ToArray();
                if(choices.Length>0)choices[choices.Length-1].OnClick();else ui.OnContinue();
            }
            yield return .3f;
        }
    }
    static void BindCamera()
    {
        var main=Camera.main;
        if(main!=null)camera=main;
        if(camera==null)
        {
            // Settlement management is authored as overlay UI without an active MainCamera.
            // A fixture-only camera renders those same canvases to the native target.
            camera=new GameObject("QA native overlay capture camera").AddComponent<Camera>();
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.012f,.02f,.035f);camera.transform.position=new Vector3(0,0,-10);
        }
        if(target==null){target=new RenderTexture(480,270,24){antiAliasing=1};target.Create();}
        camera.targetTexture=target;
        foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(c.isRootCanvas&&c.renderMode!=RenderMode.WorldSpace)
            {
                if(c.renderMode==RenderMode.ScreenSpaceOverlay)c.sortingOrder+=10000;
                c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;
            }
        Canvas.ForceUpdateCanvases();
    }
    static Vector3? captureFocus;
    static void Capture(string name)
    {
        BindCamera();var savedCameraPosition=camera.transform.position;
        if(captureFocus.HasValue) camera.transform.position=new Vector3(captureFocus.Value.x,captureFocus.Value.y,savedCameraPosition.z);
        camera.Render();camera.transform.position=savedCameraPosition;var previous=RenderTexture.active;RenderTexture.active=target;
        var png=new Texture2D(480,270,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,480,270),0,0);png.Apply();
        File.WriteAllBytes(Dir+"Rendered/"+name+".png",png.EncodeToPNG());Object.Destroy(png);RenderTexture.active=previous;
        var texts=Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
        foreach(var t in texts)t.ForceMeshUpdate();
        File.WriteAllLines(Dir+"Rendered/"+name+"-text.txt",texts.Select(t=>t.name+" overflow="+t.isTextOverflowing+" rect="+t.rectTransform.rect+" text="+t.text.Replace('\n','|')));
        Require(!texts.Any(t=>t.isTextOverflowing),"TMP overflow in "+name+": "+string.Join(",",texts.Where(t=>t.isTextOverflowing).Select(t=>t.name)));
        Note("CAPTURE "+name+" native="+Screen.width+"x"+Screen.height+" target=480x270 TMP overflow=0");
    }
    static ExpeditionEventObject Reactor(Vector3 offset)
    {
        var g=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackPolishAuthoring.ReactorPath),player.transform.position+offset,Quaternion.identity);
        return g.GetComponent<ExpeditionEventObject>();
    }
    static ReactorFeedbackUI View(ExpeditionEventObject e)=>(ReactorFeedbackUI)Get(e,"reactorFeedback");
    static TraitPickup Pickup(TraitDefinition trait)
    {
        var g=PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/TraitPickup.prefab"),player.transform.position+Vector3.left*1.2f,Quaternion.identity);
        var p=g.GetComponent<TraitPickup>();p.Initialize(trait,0);return p;
    }
    static int AudioCount(int begin,string id)=>audio.Skip(begin).Count(s=>s.Contains("EventId="+id+" |"));
    static void Hit(float amount)
    {
        player.SetDashInvincible(false);Set(player,"invincibleTimer",0f);
        player.TakeDamage(amount,player.transform.position,Vector2.right);
    }

    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Move(Vector3 position) { player.transform.position=position;player.GetComponent<Rigidbody2D>().position=position;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero; }
    static RewardPickup Credits(Vector3 position)
    {
        var g=PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/RewardPickup.prefab"),position,Quaternion.identity);
        var p=g.GetComponent<RewardPickup>();p.InitializeCurrency(CurrencyType.Credits,3,Vector2.zero);return p;
    }
    static IEnumerable<float> Theft(string path,string label,bool interrupt)
    {
        Move(new Vector3(-5,0,0)); captureFocus=new Vector3(-.15f,1,0);
        var pickup=Credits(new Vector3(4.7f,1.6f,0));yield return 1;
        var thief=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),new Vector3(3.4f,1.6f,0),Quaternion.identity);
        var role=thief.GetComponent<EnemyRoleController>();var ai=thief.GetComponent<EnemyBaseAI>();
        var bounds=new Bounds(Vector3.zero,new Vector3(40,30,0));
        if(path==FeedbackPolish2Authoring.Rival)
        {
            role.ConfigureAsRivalHarvester(bounds);
            Set(role,"currentPhase",EnemyRolePhase.CollectingLoot);Set(role,"lastHarvestPosition",(Vector2)pickup.transform.position);Set(role,"collectTimer",4f);
        }
        else role.ConfigureAsScavenger(bounds);
        thief.GetComponent<EnemyRoleSimulationGate>().ForceActive(30);ai.RequestState(EnemyState.Patrol);
        var view=(PickupCollectionPresentation)Get(role,"pickupCollectionPresentation");
        Capture(label+"-approach");
        foreach(var d in Until(()=>view.IsShowing,label+" actual channel",15))yield return d;
        float began=Time.time;Require(view.Target==pickup,"Exact pickup target");Capture(label+"-channel-start");
        yield return .17f;Require(view.IsShowing,"Channel remains visible midway");Capture(label+"-mid-channel");
        if(interrupt)
        {
            thief.GetComponent<EnemyHealth>().TakeDamage(1);Require(!view.IsShowing,"Player damage immediately cancels Scavenger channel");
            Capture(label+"-interrupted");Require(pickup.IsAvailable,"Interruption leaves pickup");
        }
        else
        {
            foreach(var d in Until(()=>!pickup.IsAvailable,label+" collection success",2))yield return d;
            Require(!view.IsShowing,"Success clears channel");
            Require(thief.GetComponent<EnemyCargoHold>().GetAmount(CurrencyType.Credits)==3,"Three Credits in existing cargo authority");
            Note(label+" observed channel="+(Time.time-began).ToString("F3")+"s; configured duration="+Get(role,"pickupCollectChannelDuration"));Capture(label+"-success");
            thief.GetComponent<EnemyHealth>().SetRewardDropEnabled(false);thief.GetComponent<EnemyHealth>().TakeDamage(99999);yield return .05f;
            Require(Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Where(x=>x.CurrencyType==CurrencyType.Credits).Sum(x=>x.Amount)==3,"Existing death recovery returns three stolen Credits");
            Note(label+" stolen-resource recovery=3 Credits");
        }
        Object.Destroy(thief);if(pickup!=null&&pickup.IsAvailable)pickup.gameObject.SetActive(false);
        foreach(var p in Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None))p.gameObject.SetActive(false);
        captureFocus=null;yield return .6f;
    }
    static IEnumerable<float> CheckTutorial()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadTutorial();foreach(var d in Scene("Tutorial"))yield return d;foreach(var d in Dialogue())yield return d;
        var flow=Object.FindFirstObjectByType<TutorialFlowController>();var hud=Object.FindFirstObjectByType<ExpeditionHUD>();
        flow.SetStep(TutorialStep.Dash);foreach(var d in Dialogue())yield return d;
        Call(flow,"ApplyCurrentStepPresentation");yield return .3f;Capture("01-tutorial-dash-objective");
        Require(flow.TryAdvanceCheckpoint(TutorialStep.Dash),"Actual checkpoint advances once");
        foreach(var d in Dialogue())yield return d;Call(flow,"ApplyCurrentStepPresentation");yield return .3f;
        Capture("02-tutorial-next-objective");var tween=Get(hud,"operationBriefingSequence");
        Call(flow,"ApplyCurrentStepPresentation");Require(ReferenceEquals(tween,Get(hud,"operationBriefingSequence")),"Same objective refresh does not restart acknowledgement");
        Require(!flow.TryAdvanceCheckpoint(TutorialStep.Dash),"Old checkpoint cannot replay");yield return 3;Capture("03-tutorial-settled");
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        Note("Tutorial native fixture uses authored checkpoint presentation with controlled checkpoint selection, not a full Tutorial playthrough.");
    }
    static IEnumerable<float> Check()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);
        SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;foreach(var d in Dialogue())yield return d;
        player=Object.FindFirstObjectByType<PlayerHealth>();
        foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        Move(Vector3.zero);player.GetComponent<PlayerController2D>().enabled=false;player.GetComponent<PlayerWeaponController>().enabled=false;
        player.SetDashInvincible(true);yield return 1;
        Note("Native generated Expedition with production prefabs and real authority methods; isolated save, controlled position/HP and active simulation gate. Theft captures center the same camera between player and thief at unchanged zoom. No unassisted balance claim.");
        foreach(var d in Theft(FeedbackPolish2Authoring.Scavenger,"01-scavenger",true))yield return d;
        foreach(var d in Theft(FeedbackPolish2Authoring.Scavenger,"02-scavenger",false))yield return d;
        foreach(var d in Theft(FeedbackPolish2Authoring.Rival,"03-rival",false))yield return d;
        Move(Vector3.zero);yield return .8f;
        var reactor=Reactor(new Vector3(0,2.5f,0));reactor.Interact(player.gameObject);yield return .05f;Capture("04-event-start");
        foreach(var d in Until(()=>View(reactor).IsShowing,"Timed state begins after arming"))yield return d;
        yield return .1f;reactor.ReceiveEventDamage(24);yield return .05f;Capture("05-event-completion");
        Require(reactor.State==ExpeditionEventState.Completed&&!View(reactor).IsShowing,"Success remains authoritative and clears readout");
        Require((int)Get(Get(Object.FindFirstObjectByType<ExpeditionHUD>(),"warningMessageUI"),"currentPriority")==1,"Quick success replaces same-event warning with Confirmation");Object.Destroy(reactor.gameObject);yield return 2;
        reactor=Reactor(new Vector3(0,2.5f,0));reactor.Interact(player.gameObject);
        foreach(var d in Until(()=>View(reactor).IsShowing,"Second timed state"))yield return d;
        foreach(var d in Until(()=>!View(reactor).IsShowing,"Authored 12 second timeout",20))yield return d;
        float stopped=(float)Get(reactor,"reactorTimer");var tracked=(List<EnemyHealth>)Get(reactor,"trackedEnemies");
        Require(tracked.Count==3&&!reactor.CanReceiveEventDamage,"Exactly authored 2 basic + 1 charger; active timer authority ended");Capture("06-reactor-timeout");
        yield return 1.5f;Require(tracked.Count==3&&(float)Get(reactor,"reactorTimer")==stopped,"Failure wave does not retrigger or decrement timer");
        reactor.ReceiveEventDamage(999);Require(reactor.State==ExpeditionEventState.Active,"Failure cannot convert to success");Capture("07-reactor-failure-combat");
        Note("Failure timer stopped="+stopped+"; tracked remains 3 after 1.5s; readout hidden; damage rejected.");
        foreach(var enemy in tracked.ToArray())enemy.TakeDamage(99999);
        foreach(var d in Until(()=>reactor.State==ExpeditionEventState.Failed,"Failure combat finishes as Failed"))yield return d;
        Object.Destroy(reactor.gameObject);yield return 2;
        foreach(var p in Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None))p.gameObject.SetActive(false);
        var core=Object.FindFirstObjectByType<CoreObject>();var tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();
        if(tracking!=null&&tracking.IsTrackingActive)foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
        {
            if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind!=HarvestObjectKind.HighValueWreck)continue;
            wreck.TakeDamage(99999);yield return .1f;Capture("08-core-signal-"+tracking.CurrentSignalCount);yield return 1.8f;
        }
        Move(core.transform.position+Vector3.down*.6f);yield return .2f;core.Interact(player.gameObject);
        BossPatternController boss=null;
        foreach(var d in Until(()=>{if(!(bool)Get(core,"activated")&&core.CanInteract(player.gameObject))core.Interact(player.gameObject);boss=Object.FindFirstObjectByType<BossPatternController>();return boss!=null&&boss.SectorCycle>0;},"Generated Region A combat"))yield return d;
        foreach(var p in Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None))p.gameObject.SetActive(false);
        var health=boss.GetComponent<EnemyHealth>();
        // Respect the production escalation damage floor/shield; first damage is not necessarily lethal.
        double limit=EditorApplication.timeSinceStartup+90;
        while(!health.IsDead)
        {
            Require(EditorApplication.timeSinceStartup<limit,"Boss lethal after authored phase protection");
            health.TakeDamage(99999);if(!health.IsDead)yield return .35f;
        }
        Note("Actual lethal frame: dead="+health.IsDead+" dropEnabled="+Get(health,"dropRewardOnDeath")+" loose="+Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Length);
        Require(Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Length==0,"Loose Credit held during death");
        Capture("09-boss-lethal");yield return .2f;Capture("10-boss-death-loose-reward");
        Require(Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None).Length==0,"No early loose reward during breakup");yield return 1.4f;
        var loose=Object.FindObjectsByType<RewardPickup>(FindObjectsSortMode.None);
        Require(loose.Length==1&&loose[0].CurrencyType==CurrencyType.Credits&&loose[0].Amount==1,"One unchanged Credit after existing death settle");
        Capture("10b-loose-credit-after-settle");Note("Credit held until existing 1.47 second death finish; exactly one Credit after settle.");
        foreach(var d in Until(()=>Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).Any(c=>c.PayloadType==RewardCapsulePayloadType.BossReward),"Main reward after existing story recovery",25))yield return d;
        Require(RunManager.Instance.CurrentRun.BossDefeated,"Existing boss death authority committed");Capture("11-main-reward-after-death");
        var capsule=Object.FindObjectsByType<RewardCapsule>(FindObjectsSortMode.None).First(c=>c.PayloadType==RewardCapsulePayloadType.BossReward);
        Move(capsule.transform.position+Vector3.down*.3f);
        foreach(var d in Until(()=>capsule.CanInteract(player.gameObject),"Reward capsule ready",25))yield return d;
        capsule.Interact(player.gameObject);var choices=Object.FindFirstObjectByType<RunLevelTraitSelectionUI>();
        foreach(var d in Until(()=>choices.IsShowing,"Boss reward choices"))yield return d;
        ((RunTraitChoiceButtonUI[])Get(choices,"choiceButtons"))[0].HandleButtonClicked();yield return 1;
        foreach(var d in Until(()=>Object.FindFirstObjectByType<ReturnBeacon>()!=null&&Object.FindFirstObjectByType<ReturnBeacon>().PresentationReady,"Beacon ready after reward",25))yield return d;
        Capture("12-beacon-ready");
        var beacon=Object.FindFirstObjectByType<ReturnBeacon>();Move(beacon.transform.position);yield return .2f;beacon.Interact(player.gameObject);yield return .3f;
        var returnUi=Object.FindFirstObjectByType<ReturnChoiceUI>();Require(returnUi.IsOpen,"Beacon interaction still valid");((Button)Get(returnUi,"yesButton")).onClick.Invoke();yield return 6;
        Object.FindFirstObjectByType<RunResultPanelUI>().Close();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        PermanentProgress.Instance.TryMarkTutorialCompleted();
        var controller=Object.FindFirstObjectByType<SettlementController>();var ui=Object.FindFirstObjectByType<SettlementUIController>();var hud=Object.FindFirstObjectByType<SettlementHUD>();
        ui.SelectBuilding(BuildingType.Hangar);yield return .3f;Capture("13-before-restoration");
        Require(controller.CanExecuteBuildingAction(BuildingType.Hangar),"Authored restoration eligibility after Region A recovery");
        int completions=0;controller.RestorationCompleted+=b=>{if(b==BuildingType.Hangar)completions++;};
        Require(controller.TryCompleteRestorationProject(BuildingType.Hangar),"Authoritative restoration succeeds");
        yield return .1f;Capture("14-restoration-completion");Require(Get(hud,"restorationAccent")!=null,"Post-save affected preview accent");
        yield return .8f;Capture("15-restoration-settled");
        Require(!controller.TryCompleteRestorationProject(BuildingType.Hangar)&&completions==1,"Repeated restore cannot replay success");
        Note("Restoration 0.7s, one post-save event; repeat rejected; existing benefit/result text retained.");
    }
}
