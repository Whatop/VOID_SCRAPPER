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

// Explicit opt-in Play Mode fixture. Uses the production prefab and transient owners;
// no SaveManager, player save, scene save, or production progression mutations.
[InitializeOnLoad]
public static class InventoryPolishRenderProbe
{
    private const string Key = "InventoryPolish.RenderProbe";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static PlayerBuildStatusPanelUI panel;
    private static RunContext run;
    private static PermanentProgress progress;
    private static VoidScrapperLocalizationService localization;
    private static Camera camera;
    private static RenderTexture target;
    private static GameObject instance;
    private static int frame, capture;
    private static double captureAt;
    private static bool prepared;
    private static readonly StringBuilder audit = new StringBuilder();
    private static readonly string[] Cases = { "equipment", "special", "structural", "cargo-zero", "cargo-partial", "cargo-near-full", "cargo-full", "recovery-zero", "recovery-mixed", "cargo-owned-empty" };
    static InventoryPolishRenderProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work first.");
        Directory.CreateDirectory("Logs/InventoryPolish/Rendered");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { frame = capture = 0; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    private static void Set(object o, string field, object v) => o.GetType().GetField(field, Private).SetValue(o, v);
    private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    private static void Tick()
    {
        try
        {
            if (frame++ == 5) Setup();
            if (frame < 24) return;
            if (panel == null) return;
            if (!prepared) { Prepare(); captureAt = EditorApplication.timeSinceStartup + .5; prepared = true; }
            if (EditorApplication.timeSinceStartup < captureAt) return;
            Capture();
            prepared = false;
            if (++capture < Cases.Length * 2) return;
            File.WriteAllText("Logs/InventoryPolish/Rendered/audit.txt", audit.ToString());
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText("Logs/InventoryPolish/Rendered/failure.txt", ex.ToString());
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Setup()
    {
        var services = new GameObject("Transient inventory owners"); services.SetActive(false);
        var manager = services.AddComponent<RunManager>(); progress = services.AddComponent<PermanentProgress>();
        typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        var store = services.AddComponent<RunRuntimeTraitStore>();
        typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        localization = services.AddComponent<VoidScrapperLocalizationService>();
        Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        Set(progress, "equipmentCatalog", catalog);
        run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        run.SetCargoRule(100, .75f, 1, 5, 5);
        Set(manager, "currentRun", run);
        foreach (string id in new[] { "shared_reinforced_plating", "shared_engine_tuning", "shared_cargo_bay", "shared_salvage_protocol", StructuralFrameProfile.HeavyId })
        { var trait = catalog.FindById(id); if (trait != null) { run.AddTrait(id); store.AddOrUpgrade(trait); } }
        var special = catalog.TraitDefinitions.First(t => t.IsResearchSpecialEquipment && t.IsAvailableFor(WeaponTreeType.MachineGun));
        run.AddTrait(special.TraitId); store.AddOrUpgrade(special);
        Set(run, "structuralFrames", StructuralFrameModules.Heavy);
        var cargo = services.AddComponent<PlayerCargoController>();
        var reinforcement = services.AddComponent<PlayerReinforcementController>();
        var reinforcementCatalog = AssetDatabase.FindAssets("t:ReinforcementDefinition").Select(g => AssetDatabase.LoadAssetAtPath<ReinforcementDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        var active = reinforcementCatalog.First(t => t.MaxCharges > 1 && t.CanUseFor(WeaponTreeType.MachineGun));
        Set(reinforcement, "equippedDefinition", active); Set(reinforcement, "currentCharges", 1);
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create();
        camera = new GameObject("UI capture camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .025f, .035f);
        camera.orthographic = true; camera.targetTexture = target; camera.transform.position = new Vector3(0, 0, -10);
        var canvasObject = new GameObject("480x270 authored inventory", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        new GameObject("ProbeEventSystem", typeof(EventSystem));
        instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPolishAuthoring.PrefabPath), canvas.transform, false);
        panel = instance.GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        Set(panel, "playerObject", services); Set(panel, "cargoController", cargo); Set(panel, "reinforcementController", reinforcement);
        foreach (var controller in instance.GetComponentsInChildren<ExpeditionMenuController>(true)) controller.enabled = false;
        for (Transform p = panel.transform; p != canvas.transform; p = p.parent) p.gameObject.SetActive(true);
        foreach (var group in instance.GetComponentsInChildren<CanvasGroup>(true)) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
        var map = instance.GetComponentInChildren<ExpeditionMapPanelUI>(true);
        if (map != null) map.enabled = false;
        panel.Open();
        audit.AppendLine("Actual Unity Play Mode; production prefab; 480x270 camera RenderTexture; transient owners; no SaveManager.");
    }
    private static void Prepare()
    {
        string language = capture < Cases.Length ? "ko" : "en";
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, language);
        Set(localization, "currentLanguageCode", language);
        panel.CloseStoryProgressInspection();
        Set(panel, "showAllCargoResources", true);
        Set(progress, "acquiredBossStoryParts", new List<BossStoryPart>());
        Set(progress, "highestUnlockedDepth", ExpeditionDepth.Normal);
        run.Wallet.Clear();
        string name = Cases[capture % Cases.Length];
        if (name.StartsWith("cargo"))
        {
            if (name == "cargo-partial") { run.Wallet.Add(CurrencyType.ScrapParts, 32); run.Wallet.Add(CurrencyType.CoreShards, 3); }
            if (name == "cargo-near-full") { run.Wallet.Add(CurrencyType.ScrapParts, 75); run.Wallet.Add(CurrencyType.CoreShards, 3); run.Wallet.Add(CurrencyType.StabilizedAlloy, 1); }
            if (name == "cargo-full") { run.Wallet.Add(CurrencyType.ScrapParts, 75); run.Wallet.Add(CurrencyType.CoreShards, 3); run.Wallet.Add(CurrencyType.StabilizedAlloy, 2); }
            panel.ShowCargoTab();
            if (name == "cargo-owned-empty") Call(panel, "ToggleCargoShowAll");
        }
        else
        {
            panel.ShowEquipmentTab();
            Call(panel, "SelectPassive", 0);
            if (name == "special" || name == "structural")
            {
                var entries = Get<System.Collections.IList>(panel, "passiveEntries");
                for (int i = 0; i < entries.Count; i++)
                {
                    var trait = (TraitDefinition)entries[i].GetType().GetField("trait").GetValue(entries[i]);
                    if (name == "special" ? trait.IsResearchSpecialEquipment : StructuralFrameProfile.ModuleFor(trait.TraitId) != StructuralFrameModules.None)
                    { Call(panel, "SelectPassive", i); break; }
                }
            }
            if (name.StartsWith("recovery"))
            {
                if (name == "recovery-mixed")
                {
                    Set(progress, "acquiredBossStoryParts", new List<BossStoryPart> { BossStoryPart.SectorStabilizer, BossStoryPart.MatterCompressor });
                    Set(progress, "highestUnlockedDepth", ExpeditionDepth.DeepZone1);
                }
                panel.OpenStoryProgressInspection();
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
        File.WriteAllBytes("Logs/InventoryPolish/Rendered/" + name + ".png", texture.EncodeToPNG());
        Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine(name + " focus=" + EventSystem.current.currentSelectedGameObject?.name);
        var gauge = Get<Slider>(panel, "cargoLoadSlider");
        audit.AppendLine("Gauge " + gauge.value + "/" + gauge.maxValue + " fill anchors=" + gauge.fillRect.anchorMax + " width=" + gauge.fillRect.rect.width);
        foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrEmpty(text.text) || text.font == null) continue;
            text.ForceMeshUpdate();
            if (text.text.Contains("\u2713") || text.text.Contains("\uFFFD")) audit.AppendLine("BAD GLYPH " + text.name + ": " + text.text);
            foreach (char c in System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", ""))
                if (!char.IsWhiteSpace(c) && !text.font.HasCharacter(c, true, true)) audit.AppendLine("MISSING U+" + ((int)c).ToString("X4") + " in " + text.name);
            if (text.isTextOverflowing) audit.AppendLine("OVERFLOW " + text.name + ": " + text.text.Replace('\n', '|'));
        }
        Debug.Log("INVENTORY RENDER " + name + " captured 480x270 in Play Mode");
        // Drive the actual EventSystem handlers, without issuing destructive gameplay actions.
        if (name.EndsWith("equipment"))
        {
            var active = Get<Button>(panel, "activeSlotSelectButton");
            EventSystem.current.SetSelectedGameObject(Get<Button>(panel, "equipmentTabButton").gameObject);
            Move(MoveDirection.Down);
            if (EventSystem.current.currentSelectedGameObject != active.gameObject) throw new InvalidOperationException("Tab cannot reach Active.");
            ExecuteEvents.Execute(active.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Move(MoveDirection.Right);
            if (EventSystem.current.currentSelectedGameObject != Get<Button>(panel, "activeFieldDropButton").gameObject) throw new InvalidOperationException("Active Drop is unreachable.");
            Move(MoveDirection.Down);
            var cards = Get<List<BuildStatusSlotButtonUI>>(panel, "passiveSlotInstances");
            if (EventSystem.current.currentSelectedGameObject != cards[0].gameObject) throw new InvalidOperationException("Storage is unreachable.");
            Move(MoveDirection.Down); Move(MoveDirection.Right);
            if (EventSystem.current.currentSelectedGameObject != cards[5].gameObject) throw new InvalidOperationException("Storage row navigation failed.");
            Move(MoveDirection.Right);
            if (EventSystem.current.currentSelectedGameObject != Get<Button>(panel, "passiveFieldDropButton").gameObject) throw new InvalidOperationException("Selected equipment Drop is unreachable.");
            audit.AppendLine("PASS Equipment tab / Active / Active Drop / both storage rows / selected Drop directional focus and Active pointer selection");
        }
        if (name.EndsWith("cargo-partial"))
        {
            var filter = Get<Button>(panel, "cargoShowAllButton");
            ExecuteEvents.Execute(filter.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            if (Get<bool>(panel, "showAllCargoResources")) throw new InvalidOperationException("Pointer filter event did not reach the bound control.");
            ExecuteEvents.Execute(filter.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            EventSystem.current.SetSelectedGameObject(filter.gameObject);
            int steps = 0;
            while (EventSystem.current.currentSelectedGameObject != Get<Button>(panel, "cargoTabButton").gameObject)
            {
                GameObject current = EventSystem.current.currentSelectedGameObject;
                if (current == null || !current.activeInHierarchy || ++steps > 15) throw new InvalidOperationException("Cargo navigation escaped its visible controls.");
                ExecuteEvents.Execute(current, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
            }
            audit.AppendLine("PASS pointer filter click and directional Cargo navigation: " + steps + " controls");
        }
        if (name.EndsWith("recovery-mixed"))
        {
            ExecuteEvents.Execute(Get<Button>(panel, "storyProgressCloseButton").gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            if (Get<GameObject>(panel, "storyProgressInspectionRoot").activeSelf || EventSystem.current.currentSelectedGameObject != panel.FirstInventorySelectable.gameObject)
                throw new InvalidOperationException("Recovery Submit/Close did not repair focus.");
            audit.AppendLine("PASS Recovery Submit/Close restored visible focus");
        }
    }
    private static void Move(MoveDirection direction)
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current == null || !current.activeInHierarchy) throw new InvalidOperationException("Hidden or missing focus target.");
        ExecuteEvents.Execute(current, new AxisEventData(EventSystem.current) { moveDir = direction }, ExecuteEvents.moveHandler);
    }
}
