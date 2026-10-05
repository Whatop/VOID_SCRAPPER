using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Phase=NullDispatcherBossController.EncounterPhase;

// Disposable Play Mode fixture using the saved Boot/Expedition scenes and real dialogue graph.
[InitializeOnLoad]
public static class NullArtSmoke
{
    private const string Key="NullArt.Smoke", Dir="Logs/NullArt/";
    private static IEnumerator<float> steps;
    private static double next, deadline;
    private static readonly List<string> evidence=new List<string>(), errors=new List<string>();
    private static NullDispatcherBossController boss;
    private static PlayerController2D player;
    private static EnemyHealth health;
    static NullArtSmoke()
    {
        EditorApplication.playModeStateChanged+=Changed;
        if(SessionState.GetBool(Key,false))Application.logMessageReceived+=Log;
    }
    private static void Log(string m,string s,LogType t) { if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors.Add(m+"\n"+s); }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard();Directory.CreateDirectory(Dir+"Rendered");
        var scene=EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity",OpenSceneMode.Single);
        var save=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SaveManager>(true)).Single();
        var so=new SerializedObject(save);so.FindProperty("fileName").stringValue=Path.GetFullPath(Dir+"smoke-save.json");so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode) { Application.runInBackground=true;steps=Check().GetEnumerator();next=0;deadline=EditorApplication.timeSinceStartup+360;EditorApplication.update+=Tick; }
        if(state==PlayModeStateChange.EnteredEditMode) {
            evidence.RemoveAll(s=>s.StartsWith("Console errors"));
            evidence.Add("Console errors/exceptions/assertions including teardown: "+errors.Count);
            File.WriteAllLines(Dir+"smoke.txt",evidence);File.WriteAllLines(Dir+"smoke-errors.txt",errors);
            SessionState.SetBool(Key,false);EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.timeSinceStartup<next)return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("NULL Play Mode probe timeout.");
            if(steps.MoveNext())next=EditorApplication.timeSinceStartup+steps.Current;else Finish();
        }catch(Exception e){errors.Add(e.ToString());Finish();}
    }
    private static void Finish()
    {
        evidence.Add("Console errors/exceptions/assertions: "+errors.Count);
        File.WriteAllLines(Dir+"smoke.txt",evidence);File.WriteAllLines(Dir+"smoke-errors.txt",errors);
        EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
    }
    private static T Field<T>(Object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private static void Require(bool valid,string text) { if(!valid)throw new Exception(text);evidence.Add("PASS "+text); }
    private static IEnumerable<float> Until(Func<bool> condition,string label,float timeout=30)
    {
        double end=EditorApplication.timeSinceStartup+timeout;
        while(!condition()) {if(EditorApplication.timeSinceStartup>end)throw new Exception("Timed out: "+label);yield return .02f;}
    }
    private static IEnumerable<float> Dialogue(bool accept)
    {
        foreach(float d in Until(()=>DialogueManager.isConversationActive,"treatment started"))yield return d;
        Require(Field<bool>(boss,"ownsInput") && player.GetComponent<PlayerWeaponController>().ExternalInputLocked,"treatment owns player/weapon/camera lock path");
        Capture(accept?"03-treatment-accept":"09-treatment-refuse");
        while(DialogueManager.isConversationActive)
        {
            var state=DialogueManager.instance.currentConversationState;
            if(state!=null && state.hasPCResponses)
            {
                var response=state.pcResponses.FirstOrDefault(r=>r.destinationEntry.id==(accept?7:8));
                if(response!=null)DialogueManager.instance.conversationView.SelectResponse(new SelectedResponseEventArgs(response));
                else DialogueManager.instance.conversationView.OnConversationContinueAll();
            }
            else DialogueManager.instance.conversationView?.OnConversationContinueAll();
            yield return .3f;
        }
        Require(boss.Choice==(accept?NullDispatcherBossController.TreatmentChoice.Accept:NullDispatcherBossController.TreatmentChoice.Reject),"natural treatment response committed: "+accept);
    }
    private static void SpawnBoss()
    {
        Vector3 center=player.transform.position;
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.BossPath),center+new Vector3(1.5f,.35f,0),Quaternion.identity);
        boss=go.GetComponent<NullDispatcherBossController>();health=go.GetComponent<EnemyHealth>();
        Require(boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork,player.gameObject),"saved boss configured in final-network runtime");
        GameStateManager.Instance.ChangeState(GameState.FinalBossBattle);
        BossHealthBarUI.Instance.ShowBoss(health,"NULL DISPATCHER");
        Require(boss.BeginCombat(),"Phase 1 began");
    }
    private static IEnumerable<float> Check()
    {
        yield return 4f;
        RunManager.Instance.StartNewRun(WeaponTreeType.MachineGun);
        RunManager.Instance.CurrentRun.SetDepth(ExpeditionDepth.FinalNetwork);
        // Test-only progress in the redirected save; exercises the actual four-step support sequence.
        foreach(var b in new[]{BuildingType.Hangar,BuildingType.EngineWorkshop,BuildingType.WeaponLab,BuildingType.RecoveryProcessor})PermanentProgress.Instance.SetBuildingLevel(b,1);
        SceneFlowManager.Instance.LoadExpeditionWithMotionTitle();
        foreach(float d in Until(()=>SceneManager.GetActiveScene().name=="Expedition"&&!SceneFlowManager.Instance.IsLoading,"Expedition loaded",90))yield return d;
        yield return 5;
        player=Object.FindFirstObjectByType<PlayerController2D>();Require(player!=null,"saved Expedition player present");
        SpawnBoss();yield return .25f;Capture("01-null-active");
        foreach(float d in Until(()=>Field<System.Collections.IList>(boss,"lasers").Count>0,"warning"))yield return d;
        float warningStart=Time.time;
        var warningIds=Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Where(v=>v.name.StartsWith("RainWarning")).Select(v=>v.GetInstanceID()).ToArray();
        var lasers=Field<List<BossLaserHazard>>(boss,"lasers");
        Vector3 safe=player.transform.position;
        Vector3 inLane=lasers[1].transform.position+Vector3.left*2;
        player.transform.position=inLane;player.GetComponent<Rigidbody2D>().position=inLane;
        float hpBefore=player.GetComponent<PlayerHealth>().CurrentHp;
        yield return .2f;Capture("02-laser-rain-telegraph");
        foreach(float d in Until(()=>{
            bool active=Field<bool>(boss,"lanesDamaging");
            if(!active && player.GetComponent<PlayerHealth>().CurrentHp!=hpBefore)throw new Exception("Damage occurred during warning.");
            return active;
        },"damage handoff"))yield return d;
        float fireStart=Time.time;
        float warningElapsed=fireStart-warningStart;
        Require(warningElapsed>=.65f && warningElapsed<=1.15f,"warning follows the existing 0.8s scheduler: "+warningElapsed.ToString("F3")+"s");
        Require(player.GetComponent<PlayerHealth>().CurrentHp>=hpBefore-2.01f,"warning has no early damage (first active tick allowed at handoff)");
        yield return .2f;Capture("02b-laser-rain-fire");
        Require(player.GetComponent<PlayerHealth>().CurrentHp<hpBefore,"same lane hazard damages only in active window");
        player.transform.position=safe;player.GetComponent<Rigidbody2D>().position=safe;
        foreach(float d in Until(()=>!Field<bool>(boss,"lanesDamaging"),"beam ended"))yield return d;
        Require(Time.time-fireStart>=2.25f&&Time.time-fireStart<=2.75f,"damage window remains 2.4s: "+(Time.time-fireStart).ToString("F3")+"s");
        // Observe the next two scheduled attacks using their actual projectile emission events.
        foreach(float d in Until(()=>Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Any(v=>v.name.StartsWith("Compression")),"compression"))yield return d;
        Capture("02c-compression");
        foreach(float d in Until(()=>Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Any(v=>v.name.StartsWith("Redirect")),"redirect"))yield return d;
        Capture("02d-redirect");
        health.TakeDamage(10000);
        Require(boss.Phase==Phase.TreatmentDialogue&&Mathf.Approximately(health.HpRatio,.5f),"lethal crossing clamps at 50% and enters treatment");
        foreach(float d in Dialogue(true))yield return d;
        yield return .2f;Capture("04-forced-link");
        foreach(float d in Until(()=>boss.Phase==Phase.PolarityPhase,"accepted branch support and Phase 2",45))yield return d;
        Require(boss.SupportRequested&&!player.GetComponent<PlayerWeaponController>().ExternalInputLocked,"support completed and branch weapon lock released");
        Capture("05-phase2-unbound");
        health.TakeDamage(10000);
        Require(boss.Phase==Phase.FinalTransition&&Mathf.Approximately(health.HpRatio,.2f),"existing 20% protection enters shell break");
        foreach(float d in Until(()=>boss.Phase==Phase.FinalPhase,"critical"))yield return d;
        yield return .15f;Capture("06-critical-exposed");
        Require(boss.InnerCoreExposed&&boss.FinalTransitionCount==1,"Critical is reached through the existing final gate exactly once");
        boss.CancelEncounter();Object.Destroy(boss.gameObject);yield return .3f;
        Require(Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Length==0,"cancellation clears pooled presentation effects");
        player.GetComponent<PlayerHealth>().Heal(100);
        SpawnBoss();
        foreach(float d in Until(()=>Field<System.Collections.IList>(boss,"lasers").Count>0,"reused warning"))yield return d;
        Require(Object.FindObjectsByType<NullSignatureVfx>(FindObjectsSortMode.None).Count(v=>warningIds.Contains(v.GetInstanceID()))==3,"all three warning VFX instances reused by PoolManager");
        health.TakeDamage(10000);
        foreach(float d in Dialogue(false))yield return d;
        foreach(float d in Until(()=>boss.Phase==Phase.PolarityPhase,"refused branch Phase 2",45))yield return d;
        Require(boss.SupportRequested,"refuse flow retains support convergence");
        health.TakeDamage(10000);
        foreach(float d in Until(()=>boss.Phase==Phase.FinalPhase,"second critical"))yield return d;
        int ended=0;RunManager.Instance.RunEnded+=_=>ended++;
        health.TakeDamage(10000);
        foreach(float d in Until(()=>DialogueManager.isConversationActive&&DialogueManager.lastConversationStarted==NullDispatcherEndingPresentation.ConversationTitle,"ending dialogue"))yield return d;
        Capture("10-ending-core");
        while(DialogueManager.isConversationActive) {DialogueManager.instance.conversationView?.OnConversationContinueAll();yield return .3f;}
        foreach(float d in Until(()=>ended>0,"FinalVictory",40))yield return d;
        yield return 1;
        Require(ended==1,"death/ending delegates run completion exactly once");
        Object.FindFirstObjectByType<RunResultPanelUI>(FindObjectsInactive.Include).Close();
        foreach(float d in Until(()=>SceneManager.GetActiveScene().name=="Settlement"&&!SceneFlowManager.Instance.IsLoading,"Continue to Settlement",45))yield return d;
        yield return 3;
        Require(GameStateManager.Instance.CurrentState==GameState.Settlement,"result Continue returns to Settlement");
    }
    private static void Capture(string name)
    {
        var camera=Camera.main;var rt=new RenderTexture(480,270,24);rt.Create();var old=camera.targetTexture;var active=RenderTexture.active;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(480,270,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,480,270),0,0);image.Apply();File.WriteAllBytes(Dir+"Rendered/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
        Object.Destroy(image);rt.Release();Object.Destroy(rt);evidence.Add("CAPTURE "+name+" 480x270; camera ortho="+camera.orthographicSize);
    }
}
