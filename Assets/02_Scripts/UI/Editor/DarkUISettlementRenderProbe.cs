using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit, disposable Play Mode visual fixture. Saved scene UI, transient progression,
// no SaveManager and no world-area/gameplay startup. Never saves a scene or preferences.
[InitializeOnLoad]
public static class DarkUISettlementRenderProbe
{
    private const string Key = "DarkUISettlement.Render";
    private const string Output = "Logs/DarkUISettlement/Rendered/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly string[] Cases = { "hangar", "navigation-focus", "equipment", "equipment-locked", "manufacturing", "structural", "special", "reinforcement", "recovery", "route-core", "archive", "settings", "settings-sound", "settings-display" };
    private static readonly StringBuilder audit = new StringBuilder();
    private static Scene scene;
    private static Canvas canvas;
    private static SettlementUIController ui;
    private static SettlementController controller;
    private static SettlementHUD hud;
    private static ShipTraitTreePanel equipment;
    private static EscSettingsMenuController settings;
    private static PermanentProgress progress;
    private static VoidScrapperLocalizationService localization;
    private static Camera camera;
    private static RenderTexture target;
    private static int frame, capture;
    private static bool prepared;
    private static double captureAt;
    static DarkUISettlementRenderProbe() { EditorApplication.playModeStateChanged += Changed; }

    public static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene work before running the visual fixture.");
        Directory.CreateDirectory(Output);
        scene = EditorSceneManager.OpenScene(DarkUISettlementAuthoring.ScenePath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
        var keep = new[] { all.OfType<Canvas>().First(c => c.name == "Canvas").gameObject,
            all.OfType<SettlementUIController>().Single().gameObject, all.OfType<SettlementController>().Single().gameObject,
            all.OfType<EventSystem>().Single().gameObject };
        foreach (GameObject go in keep) { go.SetActive(false); go.transform.SetParent(null, true); }
        foreach (GameObject root in scene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        // Start grants and scene entry are outside this presentation fixture.
        keep[2].GetComponent<SettlementController>().enabled = false;
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { frame = capture = 0; prepared = false; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    private static void Tick()
    {
        try
        {
            if (frame++ == 5) Setup();
            if (frame < 24 || ui == null) return;
            if (!prepared) { Prepare(); captureAt = EditorApplication.timeSinceStartup + .65; prepared = true; }
            if (EditorApplication.timeSinceStartup < captureAt) return;
            Capture(); prepared = false;
            if (++capture < Cases.Length * 2) return;
            File.WriteAllText(Output + "audit.txt", audit.ToString());
            EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText(Output + "failure.txt", ex.ToString());
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Setup()
    {
        scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
        canvas = all.OfType<Canvas>().First(c => c.name == "Canvas");
        ui = all.OfType<SettlementUIController>().Single(); controller = all.OfType<SettlementController>().Single();
        hud = all.OfType<SettlementHUD>().Single(); equipment = all.OfType<ShipTraitTreePanel>().Single();
        settings = all.OfType<EscSettingsMenuController>().Single();
        var services = new GameObject("Transient presentation owners"); services.SetActive(false);
        progress = services.AddComponent<PermanentProgress>();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        var state = services.AddComponent<GameStateManager>(); Set(state, "currentState", GameState.Settlement);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        var store = services.AddComponent<RunRuntimeTraitStore>();
        typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        localization = services.AddComponent<VoidScrapperLocalizationService>();
        Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        Set(progress, "equipmentCatalog", catalog);
        Set(progress, "equipmentShips", new[] { "01_basic_ship", "02_shotgun_ship", "03_sniper_ship" }.Select(n =>
            AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/" + n + ".asset")).ToList());
        LoadCheckpoint(7);
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create();
        camera = new GameObject("480x270 Settlement camera").AddComponent<Camera>();
        camera.gameObject.AddComponent<AudioListener>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f, .02f, .035f);
        camera.targetTexture = target; camera.transform.position = new Vector3(0, 0, -10);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1;
        controller.gameObject.SetActive(true); controller.LoadSelectionFromProgress(); Call(controller, "OnEnable");
        all.OfType<EventSystem>().Single().gameObject.SetActive(true);
        canvas.gameObject.SetActive(true); ui.gameObject.SetActive(true);
        ui.ShowMainPanel();
        audit.AppendLine("Actual Play Mode; saved Settlement UI; 480x270 RenderTexture; transient data; no SaveManager/world startup.");
    }
    private static void LoadCheckpoint(int checkpoint)
    {
        var data = (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), checkpoint });
        data.scrapParts = 86; data.coreShards = 12; data.stabilizedAlloy = 4;
        progress.LoadFromSave(data);
    }
    private static void Prepare()
    {
        string language = capture < Cases.Length ? "ko" : "en";
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, language); Set(localization, "currentLanguageCode", language);
        settings.Close();
        string name = Cases[capture % Cases.Length];
        LoadCheckpoint(name == "route-core" ? 8 : name == "recovery" ? 2 : name == "hangar" || name == "navigation-focus" || name == "equipment-locked" ? 0 : 7);
        controller.LoadSelectionFromProgress();
        if (name.StartsWith("equipment") || name == "manufacturing" || name == "special" || name == "structural")
        {
            ui.ShowTraitPanel(); equipment.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
            if (name == "special") equipment.ToggleResearchSpecialEquipment();
            string id = name == "special" ? "special_matter_compression" : name == "structural" ? StructuralFrameProfile.HeavyId : name == "equipment-locked" ? "shared_periodic_reflector" : "shared_cargo_bay";
            equipment.InspectEquipment(AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset").FindById(id));
        }
        else if (name == "reinforcement") ui.ShowSectorTechnologyPanel();
        else if (name == "recovery" || name == "route-core") ui.ShowRepairPanel();
        else if (name == "archive") ui.ShowDialogueArchivePanel();
        else
        {
            ui.ShowMainPanel();
            if (name.StartsWith("settings"))
            {
                ui.ShowSettingsPanel();
                Transform options = canvas.transform.Find("EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel");
                if (name != "settings") Click(options.Find(name == "settings-sound" ? "SoundTabButton" : "DisplayTabButton").GetComponent<Button>());
            }
            else if (name == "navigation-focus")
            {
                var button = hud.transform.Find("AddButton").GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            }
        }
        Canvas.ForceUpdateCanvases();
    }
    private static void Capture()
    {
        string name = (capture < Cases.Length ? "ko-" : "en-") + Cases[capture % Cases.Length];
        Canvas.ForceUpdateCanvases(); camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine(name + " focus=" + EventSystem.current.currentSelectedGameObject?.name);
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrEmpty(text.text) || text.font == null) continue;
            text.ForceMeshUpdate();
            foreach (char c in System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", ""))
                if (!char.IsWhiteSpace(c) && !text.font.HasCharacter(c, true, true)) audit.AppendLine("MISSING U+" + ((int)c).ToString("X4") + " " + DarkUISettlementAuthoring.PathOf(text.transform));
            if (text.isTextOverflowing) audit.AppendLine("OVERFLOW " + DarkUISettlementAuthoring.PathOf(text.transform) + ": " + text.text.Replace('\n', '|'));
        }
        if (name.EndsWith("navigation-focus"))
        {
            Button equipmentNav = hud.transform.Find("AddButton").GetComponent<Button>(); Click(equipmentNav);
            if (!Get<GameObject>(ui, "traitPanel").activeSelf) throw new InvalidOperationException("Navigation pointer click failed.");
            EventSystem.current.SetSelectedGameObject(equipmentNav.gameObject);
            ExecuteEvents.Execute(equipmentNav.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Up }, ExecuteEvents.moveHandler);
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) throw new InvalidOperationException("Navigation focus escaped visible controls.");
            audit.AppendLine("PASS navigation pointer selection + keyboard/controller directional focus.");
        }
        if (name.EndsWith("settings-display"))
        {
            settings.Close();
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) throw new InvalidOperationException("Settings close did not restore visible focus.");
            audit.AppendLine("PASS Settings focus restore: " + selected.name);
        }
        Debug.Log("DARKUI RENDER " + name + " 480x270 Play Mode");
    }
    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
}
