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
public static class FeedbackPolishProbe
{
    const string Key="FeedbackPolish.Probe", Dir="Logs/FeedbackPolish1/Play/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> notes=new List<string>(), errors=new List<string>(), audio=new List<string>();
    static IEnumerator<float> sequence;
    static double next, deadline;
    static bool finishing;
    static Camera camera;
    static RenderTexture target;
    static PlayerHealth player;
    static FeedbackPolishAudioCapture recorder;
    static FeedbackPolishProbe()
    {
        EditorApplication.playModeStateChanged+=Changed;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=Logged;
    }
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
            Application.runInBackground=true; sequence=Check().GetEnumerator(); deadline=EditorApplication.timeSinceStartup+600;
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
        if(recorder!=null)recorder.StopCapture(Dir+"interrupted-audio.wav");
        Flush(); EditorApplication.ExitPlaymode();
    }
    static void Note(string text){notes.Add(DateTime.UtcNow.ToString("HH:mm:ss")+" "+text);Flush();}
    static void Flush(){File.WriteAllLines(Dir+"playmode.txt",notes);File.WriteAllLines(Dir+"errors.txt",errors);File.WriteAllLines(Dir+"audio-events.txt",audio);}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static object Get(object o,string field)=>o.GetType().GetField(field,Flags).GetValue(o);
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
        camera=Camera.main;
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
    static void Capture(string name)
    {
        BindCamera();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
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
    static IEnumerable<float> Check()
    {
        foreach(var d in Until(()=>PermanentProgress.Instance!=null&&SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun,ExpeditionDepth.Normal);
        SceneFlowManager.Instance.LoadExpedition();foreach(var d in Scene("Expedition"))yield return d;foreach(var d in Dialogue())yield return d;
        player=Object.FindFirstObjectByType<PlayerHealth>();Require(player!=null,"Authored player");
        // Isolated deterministic presentation fixture; no claim of natural encounter difficulty.
        foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        var rb=player.GetComponent<Rigidbody2D>();rb.position=Vector2.zero;rb.linearVelocity=Vector2.zero;
        player.transform.position=Vector3.zero;player.GetComponent<PlayerController2D>().enabled=false;
        player.GetComponent<PlayerWeaponController>().enabled=false;
        var shield=player.GetComponent<ComponentShieldPassive>();if(shield!=null)shield.enabled=false;
        Set(player,"componentShield",null);
        player.SetDashInvincible(true);yield return 1;BindCamera();
        Set(AudioManager.Instance,"logPlayedEvents",true);
        recorder=camera.gameObject.AddComponent<FeedbackPolishAudioCapture>();
        Note("Fixture uses production event/pickup/boss prefabs and gameplay methods in Expedition; isolated save, controlled player position/damage and boss HP. No asset balances changed.");

        var reactor=Reactor(new Vector3(0,2.5f,0));yield return .3f;Capture("01-reactor-idle");
        Require(!View(reactor).IsShowing,"Idle countdown hidden");reactor.Interact(player.gameObject);
        Require(!View(reactor).IsShowing,"Arming countdown hidden");
        foreach(var d in Until(()=>reactor.State==ExpeditionEventState.Active,"Reactor timed active"))yield return d;
        yield return .3f;Capture("02-reactor-active");yield return 2;
        reactor.GetComponentInChildren<ExpeditionEventDamageReceiver>(true).TakeDamage(12);yield return .08f;Capture("03-reactor-damaged-timer-continues");
        Require((float)Get(reactor,"reactorHp")==12,"Half reactor HP");
        var before=(float)Get(reactor,"reactorTimer");yield return .3f;Require((float)Get(reactor,"reactorTimer")<before,"Timer independently decreases");
        reactor.GetComponentInChildren<ExpeditionEventDamageReceiver>(true).TakeDamage(12);yield return .4f;
        Require(!View(reactor).IsShowing,"Success clears countdown");Capture("04-reactor-success");Object.Destroy(reactor.gameObject);
        yield return 1.5f;
        reactor=Reactor(new Vector3(0,2.5f,0));yield return .1f;reactor.Interact(player.gameObject);
        foreach(var d in Until(()=>reactor.State==ExpeditionEventState.Active,"Timeout fixture active"))yield return d;
        foreach(var d in Until(()=>!View(reactor).IsShowing,"Authored 12 second timeout clears readout",20))yield return d;
        // Freeze this resolved fixture immediately; keep the production failure outcome/reinforcement request.
        reactor.enabled=false;Capture("05-reactor-timeout");Object.Destroy(reactor.gameObject);
        yield return 1.5f;
        foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))e.gameObject.SetActive(false);

        var armor=player.GetComponent<PlayerArmor>();armor.SetMaxArmor(8,true);player.RestoreCurrentHp(player.MaxHp);
        int breaks=0;armor.Broken+=()=>breaks++;int hitAudio=audio.Count;
        recorder.StartCapture();Hit(3);yield return .04f;Capture("06-ordinary-armor-hit");
        Require(armor.CurrentArmor==5&&breaks==0,"Ordinary hit no break");yield return .5f;
        Hit(5);yield return .04f;Capture("07-armor-break");
        Require(armor.CurrentArmor==0&&breaks==1,"One depletion transition");
        var hud=Object.FindFirstObjectByType<ExpeditionHUD>();var track=(Image)Get(hud,"armorTrackImage");
        Require(track.gameObject.activeInHierarchy,"Empty armor track accented and visible through its parent");
        yield return .7f;Require(!track.gameObject.activeSelf,"Armor accent settles");
        var hp=player.CurrentHp;Hit(3);yield return .04f;Capture("08-hp-hit-after-armor");
        Require(player.CurrentHp<hp&&breaks==1,"HP damage does not repeat break");yield return .6f;
        Require(AudioCount(hitAudio,SoundEventIds.ShipHit)==3,"One existing hit sound per accepted hit; no extra break audio");
        recorder.StopCapture(Dir+"armor-output.wav");player.SetDashInvincible(true);

        yield return 1.5f;
        var trait=AssetDatabase.LoadAssetAtPath<TraitDefinition>("Assets/02_Scripts/Config/TraitDefinition/Common/03_shared_salvage_magnet.asset");
        ((List<string>)Get(RunManager.Instance.CurrentRun,"preparedEquipmentIds")).Add(trait.TraitId);
        var pickup=Pickup(trait);yield return .2f;Capture("09-trait-before-grant");int traitAudio=audio.Count;
        recorder.StartCapture();pickup.Interact(player.gameObject);yield return .05f;Capture("10-trait-confirmation");
        Require(RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId)==1,"Actual Trait grant level 1");
        var slots=Object.FindObjectsByType<StatusEffectSlotUI>(FindObjectsSortMode.None);
        var slot=slots.Single(s=>s.StatusId==trait.TraitId);
        Require(((Image)Get(slot,"iconImage")).sprite==trait.Icon,"Correct acquired Trait icon");
        Require(slot.IsAcquisitionAccentActive,"Trait slot accent active");
        var message=(WarningMessageUI)Get(hud,"warningMessageUI");
        Require((int)Get(message,"currentPriority")==1,"Confirmation severity, not Warning");
        Require(((TMP_Text)Get(message,"messageText")).text.Contains(trait.DisplayName),"Correct Trait name");
        yield return .1f;Capture("11-trait-slot-accent");
        pickup=Pickup(trait);pickup.Interact(player.gameObject);yield return .05f;
        Require(RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId)==2,"Actual repeated Trait grant level 2");
        Require(Object.FindObjectsByType<StatusEffectSlotUI>(FindObjectsSortMode.None).Count(s=>s.StatusId==trait.TraitId)==1,"Repeated acquisition reuses one slot");
        Require(DG.Tweening.DOTween.TweensByTarget(Get(slot,"backgroundImage")).Count==1,"Repeated acquisition replaces its one slot tween");
        yield return 2.2f;
        Require(!Object.FindObjectsByType<StatusEffectSlotUI>(FindObjectsSortMode.None).Any(s=>s.StatusId==trait.TraitId),"Temporary acquisition slot settles");
        Require(AudioCount(traitAudio,SoundEventIds.TraitSelect)==2,"One Trait sound per successful grant");recorder.StopCapture(Dir+"trait-output.wav");
        pickup=Pickup(trait);pickup.Interact(player.gameObject);yield return 2.3f;
        traitAudio=audio.Count;pickup=Pickup(trait);pickup.Interact(player.gameObject);yield return .1f;
        Require(AudioCount(traitAudio,SoundEventIds.TraitSelect)==0,"Failed max-level grant has no success audio");
        Require(!Object.FindObjectsByType<StatusEffectSlotUI>(FindObjectsSortMode.None).Any(s=>s.StatusId==trait.TraitId),"Failed grant has no slot accent");Object.Destroy(pickup.gameObject);
        yield return 2;

        var carrier=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSalvageAuthoring.Boss),player.transform.position+Vector3.up*4,Quaternion.identity).GetComponent<RaiderSalvageCarrierBossController>();
        carrier.ConfigureEncounter(Vector2.zero,new Vector2(10,7),player.gameObject);carrier.BeginCombat();yield return .5f;
        int carrierAudio=audio.Count;recorder.StartCapture();carrier.GetComponent<EnemyHealth>().TakeDamage(carrier.GetComponent<EnemyHealth>().MaxHp*.52f);
        foreach(var d in Until(()=>carrier.Stage==RaiderSalvageCarrierBossController.CarrierStage.OverloadTransition,"Carrier actual overload"))yield return d;
        Capture("12-carrier-overload-audio");yield return 2;
        Require(AudioCount(carrierAudio,SoundEventIds.BossPhase2)==1,"One carrier overload sound");
        recorder.StopCapture(Dir+"carrier-overload-output.wav");carrier.StopCombat();Object.Destroy(carrier.gameObject);yield return 1;

        var sniper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSniperAuthoring.Boss),player.transform.position+Vector3.up*4,Quaternion.identity).GetComponent<RaiderSniperCommanderBossController>();
        sniper.ConfigureEncounter(Vector2.zero,new Vector2(10,7),player.gameObject);
        int sniperAudio=audio.Count;recorder.StartCapture();sniper.BeginCombat();
        foreach(var d in Until(()=>sniper.Stage==RaiderSniperCommanderBossController.SniperStage.Acquisition,"Sniper actual acquisition"))yield return d;
        Capture("13-sniper-charge-audio");
        foreach(var d in Until(()=>sniper.Stage==RaiderSniperCommanderBossController.SniperStage.RailShot,"Sniper actual rail shot"))yield return d;
        Capture("14-sniper-rail-shot-audio");yield return .9f;
        sniper.GetComponent<EnemyHealth>().TakeDamage(sniper.GetComponent<EnemyHealth>().MaxHp*.52f);
        foreach(var d in Until(()=>sniper.Stage==RaiderSniperCommanderBossController.SniperStage.CriticalTransition,"Sniper actual critical transition"))yield return d;
        Capture("15-sniper-critical-audio");yield return 1.8f;
        Require(AudioCount(sniperAudio,SoundEventIds.BossChargeAim)==1,"One acquisition sound");
        Require(AudioCount(sniperAudio,SoundEventIds.BossChargeFire)==1,"One rail release sound");
        Require(AudioCount(sniperAudio,SoundEventIds.BossPhase2)==1,"One critical transition sound");
        recorder.StopCapture(Dir+"sniper-output.wav");sniper.StopCombat();Object.Destroy(sniper.gameObject);yield return .5f;

        reactor=Reactor(new Vector3(0,2.5f,0));yield return .1f;reactor.Interact(player.gameObject);
        foreach(var d in Until(()=>View(reactor).IsShowing,"Death cleanup fixture active"))yield return d;
        player.SetDashInvincible(false);Set(player,"invincibleTimer",0f);player.TakeDamage(9999);
        Require(!View(reactor).IsShowing,"Player death clears countdown immediately");yield return .1f;Capture("16-player-death-cleanup");
        yield return 2;
        Object.FindFirstObjectByType<RunResultPanelUI>().Close();
        foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        Note("All fixture assertions passed. Captured mixer output is evidence of playback, not a listening assessment.");
    }
}

// QA-only audio tap on the actual listener. Never imported into a player build or saved scene.
public sealed class FeedbackPolishAudioCapture : MonoBehaviour
{
    readonly object sync=new object();
    readonly List<float> samples=new List<float>();
    bool capturing;int channels=2;
    public void StartCapture(){lock(sync){samples.Clear();capturing=true;}}
    void OnAudioFilterRead(float[] data,int count)
    {
        lock(sync){if(!capturing)return;channels=count;samples.AddRange(data);}
    }
    public void StopCapture(string path)
    {
        lock(sync)
        {
            if(!capturing)return;capturing=false;
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                int bytes=samples.Count*2,rate=AudioSettings.outputSampleRate;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                for(int i=0;i<samples.Count;i++)writer.Write((short)(Mathf.Clamp(samples[i],-1,1)*32767));
            }
            samples.Clear();
        }
    }
}
