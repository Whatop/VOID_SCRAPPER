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
using UnityEngine.InputSystem;

// Explicit opt-in fixture: real Boot/Expedition Play Mode, disposable save, no saved scene writes.
[InitializeOnLoad]
public static class FeedbackPolish3Probe
{
    const string Key="FeedbackPolish3.Probe";
    static string Dir => SessionState.GetBool("FeedbackPolish3.Tutorial",false) ? "Logs/FeedbackPolish3/Tutorial/" : "Logs/FeedbackPolish3/Play/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> notes=new List<string>(), errors=new List<string>(), audio=new List<string>();
    static IEnumerator<float> sequence;
    static double next, deadline;
    static bool finishing;
    static Camera camera;
    static RenderTexture target;
    static PlayerHealth player;
    static FeedbackPolish3Probe()
    {
        EditorApplication.playModeStateChanged+=Changed;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=Logged;
    }
    public static void RunTutorial() { SessionState.SetBool("FeedbackPolish3.Tutorial",true); Run(); }
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
            Application.runInBackground=true; sequence=(SessionState.GetBool("FeedbackPolish3.Tutorial",false)?CheckTutorial():Check()).GetEnumerator(); deadline=EditorApplication.timeSinceStartup+600;
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
        if (player != null) Note("Player="+player.transform.position+" viewport="+camera.WorldToViewportPoint(player.transform.position)+" camera="+camera.transform.position);
        Note("CAPTURE "+name+" native="+Screen.width+"x"+Screen.height+" target=480x270 TMP overflow=0");
    }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Move(Vector3 position) { player.transform.position=position;player.GetComponent<Rigidbody2D>().position=position;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero; }
    const string Common="Assets/02_Scripts/Config/ReinforcementDefinition/Common/";
    static ReinforcementDefinition Def(string name)=>AssetDatabase.LoadAssetAtPath<ReinforcementDefinition>(Common+name+".asset");
    static ReinforcementPickup Pickup(ReinforcementDefinition def, Vector3 position)
    {
        string path=AssetDatabase.GUIDToAssetPath("68a782fdd93f485418b72f76aa265798");
        var p=PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>(path),position,Quaternion.identity).GetComponent<ReinforcementPickup>();
        p.Initialize(def,-1,0);return p;
    }
    static IEnumerable<float> Check()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);
        SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;foreach(var d in Dialogue())yield return d;
        player=Object.FindFirstObjectByType<PlayerHealth>();
        foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        foreach(var e in Object.FindObjectsByType<ExpeditionEventObject>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        Move(Vector3.zero);player.GetComponent<PlayerController2D>().enabled=false;player.GetComponent<PlayerWeaponController>().enabled=false;
        player.SetDashInvincible(true);
        var controller=player.GetComponent<PlayerReinforcementController>();controller.ClearEquipment(true);
        var hud=Object.FindFirstObjectByType<ExpeditionHUD>();var slot=(ReinforcementSlotUI)Get(hud,"reinforcementSlotUI");
        var prompt=Object.FindFirstObjectByType<InteractionPromptUI>();var menu=(GameObject)Get(hud,"menuHintRoot");
        int acknowledgements=0;controller.AcquisitionAcknowledged+=_=>acknowledgements++;
        yield return 2;Capture("01-exploration-empty-slot");
        Require(string.IsNullOrEmpty(((TMP_Text)Get(slot,"keyText")).text),"Empty Active slot hides unavailable key hint");
        var first=Def("04_rf_burst_barrier");var second=AssetDatabase.LoadAssetAtPath<ReinforcementDefinition>("Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/26_rf_mg_cooling_core.asset");
        var pickup=Pickup(first,player.transform.position+Vector3.up*2.5f);yield return .2f;Capture("02-field-item");
        Move(pickup.transform.position+Vector3.down*.4f);yield return .3f;Capture("03-local-card");
        Require(!menu.activeSelf,"Local card suppresses persistent reminders");
        var input=player.GetComponent<PlayerInteractor>().InputActions;
        string overrides=input.SaveBindingOverridesAsJson();
        var interact=input.FindAction("Player/Interact");var dismantle=input.FindAction("Player/Dismantle");
        int ib=-1,db=-1;
        for(int i=0;i<interact.bindings.Count;i++)if(interact.bindings[i].effectivePath.StartsWith("<Keyboard>")){ib=i;break;}
        for(int i=0;i<dismantle.bindings.Count;i++)if(dismantle.bindings[i].effectivePath.StartsWith("<Keyboard>")){db=i;break;}
        Require(ib>=0&&db>=0,"Keyboard binding indices");
        interact.ApplyBindingOverride(ib,"<Keyboard>/j");dismantle.ApplyBindingOverride(db,"<Keyboard>/k");yield return .1f;Capture("04-rebound-J-K-card");
        Require(((TMP_Text)Get(prompt,"lootPrimaryActionText")).text.Contains("J")&&((TMP_Text)Get(prompt,"lootDismantleActionText")).text.Contains("K"),"Live rebound key labels");
        input.LoadBindingOverridesFromJson(overrides);yield return .1f;
        pickup.Interact(player.gameObject);yield return .08f;
        Require(controller.EquippedDefinition==first&&acknowledgements==1,"One actual pickup acknowledgement");
        Require(Get(slot,"acquisitionAccent")!=null,"One live Active slot accent");Capture("05-acquisition-accent");
        yield return .7f;Require(Get(slot,"acquisitionAccent")==null,"Accent settles");Capture("06-ready-slot");
        pickup=Pickup(second,player.transform.position+Vector3.up*.4f);yield return .3f;Capture("07-replacement-card");
        int previousCharges=controller.CurrentCharges;
        pickup.Interact(player.gameObject);yield return .08f;Capture("08-replacement-confirmation");
        Require(acknowledgements==2&&controller.EquippedDefinition==second,"Replacement acknowledged once");
        Require(pickup.ReinforcementDefinition==first&&pickup.StoredCharges==previousCharges,"Old item and charges remain in exact field pickup");
        Move(player.transform.position+Vector3.down*3);yield return .5f;Capture("09-context-exit");Require(menu.activeSelf,"Persistent reminders restore after context exit");
        Require(controller.TryUseCurrent(),"Production Reinforcement activation");yield return .3f;Capture("10-active-duration");
        Require(((Image)Get(slot,"activeDurationFillImage")).enabled&&!((Image)Get(slot,"iconRechargeFillImage")).enabled,"Active cyan duration owns fill without recharge overlap");
        float duration=second.Effects.Max(e=>e.Duration);yield return duration+.3f;Capture("11-recharging");
        Require(!((Image)Get(slot,"activeDurationFillImage")).enabled&&((Image)Get(slot,"iconRechargeFillImage")).enabled,"Recharge neutral fill resumes after duration");
        Note("Production duration="+duration+" recharge="+controller.EffectiveRechargeSeconds+" charges="+controller.CurrentCharges+"/"+controller.MaxCharges);
        var bay=new GameObject("QA maintenance authority").AddComponent<ShopActiveMaintenanceBay>();
        Require(bay.TryStoreCurrentInSlot(0,controller),"Maintenance storage succeeds");yield return .1f;Capture("12-stored-confirmation");
        Require(!controller.HasEquipment&&bay.GetAt(0)==second,"Existing storage authority");
        Require(bay.TryEquipStored(0,controller),"Maintenance re-equip succeeds");yield return .1f;Capture("13-storage-equip");
        Require(acknowledgements==3,"Storage equip has one acknowledgement");
        Object.Destroy(bay.gameObject);
        RunManager.Instance.CompleteRun(RunEndReason.Death);yield return 5;
        var result=Object.FindFirstObjectByType<RunResultPanelUI>();if(result!=null)result.Close();
        foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        Note("Generated Expedition, production pickups/controller/HUD and controlled positions. Binding overrides restored, disposable save only.");
    }
    static IEnumerable<float> CheckTutorial()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadTutorial();foreach(var d in Scene("Tutorial"))yield return d;foreach(var d in Dialogue())yield return d;
        player=Object.FindFirstObjectByType<PlayerHealth>();
        var flow=Object.FindFirstObjectByType<TutorialFlowController>();var hud=Object.FindFirstObjectByType<ExpeditionHUD>();
        var dash=player.GetComponent<PlayerDash>();var control=player.GetComponent<PlayerController2D>();
        flow.SetStep(TutorialStep.Dash);foreach(var d in Dialogue())yield return d;
        Call(flow,"ApplyCurrentStepPresentation");yield return .12f;Capture("01-dash-instruction-anticipation");
        var center=(Vector2)Get(flow,"dashPracticeCenter");var marker=(LineRenderer)Get(flow,"dashPracticeMarker");
        var input=player.GetComponent<PlayerInteractor>().InputActions;string savedBindings=input.SaveBindingOverridesAsJson();
        var dashAction=input.FindAction("Player/Dash");InputBindingUtility.ResolveAction(input,"Player","Dash");
        dashAction.ApplyBindingOverride(0,"<Keyboard>/l");yield return .3f;Capture("01b-dash-rebound-L");
        Require(((TMP_Text)Get(hud,"operationDetailText")).text.Contains("L"),"Tutorial uses live rebound Dash binding");
        input.LoadBindingOverridesFromJson(savedBindings);yield return .3f;
        var tween=Get(hud,"operationBriefingSequence");Call(flow,"ApplyCurrentStepPresentation");
        Require(ReferenceEquals(tween,Get(hud,"operationBriefingSequence")),"Repeated briefing does not stack");
        yield return .5f;Capture("02-danger-zone-ready");
        Move(center+Vector2.right*1.5f);yield return .6f;
        Require(flow.CurrentStep==TutorialStep.Dash,"Ordinary movement cannot complete Dash checkpoint");Capture("03-movement-does-not-complete");
        Move(center);Set(control,"aimDirection",Vector2.up);Set(control,"moveInput",Vector2.zero);
        Require(dash.TryDash(),"Actual Dash starts for cancelled attempt");dash.CancelActiveDash();yield return .6f;
        Require(flow.CurrentStep==TutorialStep.Dash&&marker.enabled,"Cancelled Dash keeps clean retry checkpoint");Capture("04-cancel-retry");
        Require(((GameObject)Get(hud,"operationRoot")).activeSelf,"Required Dash instruction survives retry and briefing timeout");
        yield return .15f;Capture("04b-retry-settled");
        Note("Retry marker enabled="+marker.enabled+" active="+marker.gameObject.activeInHierarchy+" bounds="+marker.bounds);
        foreach(var d in Until(()=>dash.CanDash,"Existing Dash cooldown ready"))yield return d;
        Move(center);Set(control,"aimDirection",Vector2.up);Set(control,"moveInput",Vector2.zero);
        int serial=dash.CompletedDashSerial;Require(dash.TryDash(),"Actual Dash starts in practice zone");yield return .025f;Capture("05-dash-in-progress");
        foreach(var d in Until(()=>dash.CompletedDashSerial>serial,"PlayerDash actual completion"))yield return d;
        yield return .15f;Capture("06-clearance-confirmation");
        Require(((GameObject)Get(hud,"operationRoot")).activeSelf&&((TMP_Text)Get(hud,"operationTitleText")).text.Contains("대시 회피 확인"),"Tutorial guide owns visible success acknowledgement");
        foreach(var d in Until(()=>flow.CurrentStep==TutorialStep.RadarDiscoverSalvage,"Dash clearance advances existing checkpoint"))yield return d;
        Require(!marker.enabled,"Marker clears after checkpoint");Capture("07-next-checkpoint");
        foreach(var d in Dialogue())yield return d;
        flow.SetStep(TutorialStep.Dash);yield return .2f;Require(marker.enabled,"Step re-entry starts clean marker");
        flow.enabled=false;Require(!marker.enabled&&Get(flow,"dashPracticeTween")==null,"Abort disables marker and tween");Capture("08-abort-cleanup");
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        Note("Authored Tutorial checkpoint selected by fixture; actual PlayerDash movement, cancellation/cooldown/completion used. Zone is harmless rehearsal, not a damaging hazard. Not a full Tutorial playthrough.");
    }
}
