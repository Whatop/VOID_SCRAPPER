using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Opt-in task bridge for a licensed Editor. Never runs without a menu/CLI call or request file.
[InitializeOnLoad]
public static class SectorPatternValidation
{
    const string Request = "Logs/SectorPattern/run.request";
    const string SceneKey = "SectorPattern.PreviousScenes";
    static TestRunnerApi api;
    static bool launchProbe;
    static bool refreshRequested;
    [Serializable] class SavedScene { public string path; public bool loaded, active; }
    [Serializable] class SavedScenes { public SavedScene[] scenes; }
    static SectorPatternValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        if (!refreshRequested) { refreshRequested = true; AssetDatabase.Refresh(); return; }
        File.Delete(Request); refreshRequested = false;
        try { Run(); }
        catch (Exception e) { File.WriteAllText("Logs/SectorPattern/validation-error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/VOID SCRAPPER/Validate Sector Administrator Pattern")]
    public static void Run() => RunValidation(true);
    public static void RunTestsOnly() => RunValidation(false);
    static void RunValidation(bool playMode)
    {
        launchProbe = playMode;
        ApprovedVisualIntegration.Guard();
        Directory.CreateDirectory("Logs/SectorPattern");
        SectorPatternAuthoring.Author();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var saved = new SavedScenes { scenes = new SavedScene[setup.Length] };
        for (int i = 0; i < setup.Length; i++) saved.scenes[i] = new SavedScene { path = setup[i].path, loaded = setup[i].isLoaded, active = setup[i].isActive };
        SessionState.SetString(SceneKey, JsonUtility.ToJson(saved));
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^SectorPatternTests", "^SectorIntroTests", "^SectorRefineTests", "^SectorAdministratorTests", "^SectorFollowupTests", "^CommonPresentationTests", "^CommonBossPresentationTests", "^DefenseOverseerTests", "^PhaseGatekeeperTests", "^RaiderAssaultTests", "^RaiderSalvageTests", "^RaiderSniperTests", "^NullArtTests" } }));
    }
    public static void RestoreScenes()
    {
        string json = SessionState.GetString(SceneKey, ""); SessionState.EraseString(SceneKey);
        if (string.IsNullOrEmpty(json)) return;
        var saved = JsonUtility.FromJson<SavedScenes>(json);
        var setup = new SceneSetup[saved.scenes.Length];
        for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup { path = saved.scenes[i].path, isLoaded = saved.scenes[i].loaded, isActive = saved.scenes[i].active };
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }
    sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor _) { }
        public void TestStarted(ITestAdaptor _) { }
        public void TestFinished(ITestResultAdaptor _) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/SectorPattern/editmode.xml");
            File.WriteAllText("Logs/SectorPattern/test-summary.txt", $"Passed={result.PassCount}; Failed={result.FailCount}; Skipped={result.SkipCount}");
            // RunFinished precedes the runner's scene-restoration tasks. A single
            // delayCall can enter Play Mode while those tasks still own the scene.
            var isRunActive = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);
            void AfterRunnerCleanup()
            {
                if ((bool)isRunActive.Invoke(null, null)) return;
                EditorApplication.update -= AfterRunnerCleanup;
                api.UnregisterCallbacks(this);
                if (result.FailCount == 0 && launchProbe) SectorPatternProbe.Run();
                else { RestoreScenes(); if (Application.isBatchMode) EditorApplication.Exit(1); }
            }
            EditorApplication.update += AfterRunnerCleanup;
        }
    }
}
