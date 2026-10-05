using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Real Boot -> Settlement -> Expedition flow. The save filename is redirected only
// in the disposable Boot scene; no scene is saved and player progression is untouched.
[InitializeOnLoad]
public static class ApprovedVisualIntegrationSmoke
{
    private const string Key="ApprovedVisualIntegration.Smoke";
    private static IEnumerator<float> sequence;
    private static double nextAt;
    private static readonly List<string> evidence=new List<string>();
    private static readonly List<string> errors=new List<string>();
    static ApprovedVisualIntegrationSmoke()
    {
        EditorApplication.playModeStateChanged+=Changed;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=OnLog;
    }
    private static void OnLog(string message,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message+"\n"+stack); }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(ApprovedVisualIntegration.LogRoot+"Rendered");
        var scene=EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity",OpenSceneMode.Single);
        var save=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SaveManager>(true)).Single();
        var so=new SerializedObject(save); so.FindProperty("fileName").stringValue=Path.GetFullPath(ApprovedVisualIntegration.LogRoot+"smoke-save.json");so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode) { Application.runInBackground=true;sequence=Check().GetEnumerator();nextAt=EditorApplication.timeSinceStartup+.5;EditorApplication.update+=Tick; }
        if(state==PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key,false);EditorApplication.Exit(errors.Count==0?0:1); }
    }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.timeSinceStartup<nextAt)return;
        try {
            if(sequence.MoveNext())nextAt=EditorApplication.timeSinceStartup+sequence.Current;
            else Finish();
        }catch(Exception e){errors.Add(e.ToString());Finish();}
    }
    private static void Finish()
    {
        evidence.Add("Console errors/exceptions/assertions: "+errors.Count);
        File.WriteAllLines(ApprovedVisualIntegration.LogRoot+"smoke.txt",evidence);
        File.WriteAllLines(ApprovedVisualIntegration.LogRoot+"smoke-errors.txt",errors);
        EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
    }
    private static IEnumerable<float> AwaitScene(string scene)
    {
        double end=EditorApplication.timeSinceStartup+90;
        while(SceneManager.GetActiveScene().name!=scene||SceneFlowManager.Instance.IsLoading) {
            if(EditorApplication.timeSinceStartup>end)throw new Exception("Timed out loading "+scene);
            yield return .2f;
        }
    }
    private static IEnumerable<float> Check()
    {
        yield return 4;
        if(SceneManager.GetActiveScene().name!="Boot"||SceneFlowManager.Instance==null)throw new Exception("Boot did not initialize.");
        evidence.Add("PASS saved Boot initialized; SaveManager redirected to workspace smoke-save.json."); Capture("01-boot");
        SceneFlowManager.Instance.LoadSettlement();
        foreach(float delay in AwaitScene("Settlement"))yield return delay;
        yield return 4;
        var hud=Object.FindFirstObjectByType<SettlementHUD>(FindObjectsInactive.Include);
        var bindings=new List<string>();hud.ValidateHangarPresentation(bindings);
        if(bindings.Count>0)throw new Exception(string.Join("\n",bindings));
        evidence.Add("PASS Boot -> Settlement through SceneFlowManager; Hangar bindings valid.");Capture("02-settlement");
        var controller=Object.FindFirstObjectByType<SettlementController>();
        var ship=controller.PreviewShip;
        RunManager.Instance.StartNewRunAndLoadExpedition(ship.DefaultWeaponTree,ship.ShipId);
        foreach(float delay in AwaitScene("Expedition"))yield return delay;
        yield return 8;
        if(RunManager.Instance.CurrentRun==null||Object.FindFirstObjectByType<PlayerHealth>()==null)throw new Exception("Expedition run/player missing.");
        evidence.Add("PASS Settlement -> Expedition through RunManager/SceneFlowManager; fresh run and player present.");Capture("03-expedition");
        yield return 2;
    }
    private static void Capture(string name)
    {
        var camera=Camera.main??Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.isActiveAndEnabled);
        var rt=new RenderTexture(480,270,24);rt.Create();var old=camera.targetTexture;var oldActive=RenderTexture.active;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        var png=new Texture2D(480,270,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,480,270),0,0);png.Apply();
        File.WriteAllBytes(ApprovedVisualIntegration.LogRoot+"Rendered/"+name+".png",png.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=oldActive;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
        Object.Destroy(png);rt.Release();Object.Destroy(rt);evidence.Add("CAPTURE "+name+" 480x270");
    }
}
