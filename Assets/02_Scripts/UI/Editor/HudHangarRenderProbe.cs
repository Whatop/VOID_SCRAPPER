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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Disposable saved-scene QA. Transient progression and a read-only loadout snapshot; never writes scenes or saves.
[InitializeOnLoad]
public static class HudHangarRenderProbe
{
    private const string Key = "HudHangar.Render";
    private const string Output = "Logs/HudHangar/Rendered/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly StringBuilder audit = new StringBuilder();
    private static IEnumerator<float> sequence;
    private static double nextAt;
    private static SettlementDefenseEncounterController encounter;
    private static SettlementRouteCoreController route;
    private static PermanentProgress progress;
    private static PlayerHealth player;
    private static PlayerController2D movement;
    private static PlayerInteractor interactor;
    private static Camera camera;
    private static Canvas canvas;
    private static GameObject deck, management, back;
    private static RenderTexture target;
    private static VoidScrapperLocalizationService localization;
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        audit.AppendLine("PASS " + description);
    }
    static HudHangarRenderProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before rendered validation.");
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "failure.txt")) File.Delete(Output + "failure.txt");
        Scene scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath, OpenSceneMode.Single);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var keep = new[] { encounter.gameObject, Get<GameObject>(encounter, "deckRoot"), Get<GameObject>(encounter, "deckCanvas"),
            Get<GameObject>(encounter, "managementCanvas"), Get<Camera>(encounter, "deckCamera").gameObject,
            RouteCoreDeckAuthoring.Single<SettlementController>(scene).gameObject,
            RouteCoreDeckAuthoring.Single<SettlementUIController>(scene).gameObject,
            RouteCoreDeckAuthoring.Single<EventSystem>(scene).gameObject };
        foreach (GameObject go in keep) { go.SetActive(false); go.transform.SetParent(null, true); }
        foreach (GameObject root in scene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        keep[5].GetComponent<SettlementController>().enabled = false;
        keep[6].GetComponent<SettlementUIController>().enabled = false;
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            sequence = Check().GetEnumerator(); nextAt = EditorApplication.timeSinceStartup + .3;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate(); // Keep the hidden Editor's actual Play Mode advancing.
        if (EditorApplication.timeSinceStartup < nextAt) return;
        try
        {
            if (sequence.MoveNext()) nextAt = EditorApplication.timeSinceStartup + sequence.Current;
            else
            {
                File.WriteAllText(Output + "audit.txt", audit.ToString());
                EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText(Output + "failure.txt", ex.ToString());
            File.WriteAllText(Output + "audit.txt", audit.ToString());
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Setup()
    {
        Application.runInBackground = true;
        Scene scene = SceneManager.GetActiveScene();
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        route = Get<SettlementRouteCoreController>(encounter, "routeCore");
        deck = Get<GameObject>(encounter, "deckRoot"); management = Get<GameObject>(encounter, "managementCanvas");
        back = Get<GameObject>(encounter, "facilityNavigation"); canvas = Get<GameObject>(encounter, "deckCanvas").GetComponent<Canvas>();
        player = Get<PlayerHealth>(encounter, "player"); movement = player.GetComponent<PlayerController2D>();
        interactor = player.GetComponent<PlayerInteractor>(); camera = Get<Camera>(encounter, "deckCamera");
        var services = new GameObject("Transient deck validation owners"); services.SetActive(false);
        progress = services.AddComponent<PermanentProgress>(); typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        Set(progress, "equipmentCatalog", Get<TraitCatalog>(encounter, "traitCatalog"));
        Set(progress, "equipmentShips", Get<ShipDefinition[]>(encounter, "ships").ToList());
        var state = services.AddComponent<GameStateManager>(); Set(state, "currentState", GameState.Settlement);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        var store = services.AddComponent<RunRuntimeTraitStore>(); typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        localization = services.AddComponent<VoidScrapperLocalizationService>();
        Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        Language("ko");
        new GameObject("Disposable projectile pool").AddComponent<PoolManager>();
        progress.LoadFromSave(new SaveData());
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create();
        camera.targetTexture = target; camera.gameObject.SetActive(true);
        foreach (Canvas c in new[] { canvas, management.GetComponent<Canvas>() })
        {
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1;
        }
        management.SetActive(true); encounter.gameObject.SetActive(true);
        RouteCoreDeckAuthoring.Single<EventSystem>(scene).gameObject.SetActive(true);
        audit.AppendLine("Actual saved world in Play Mode; actual camera/2D lighting and 480x270 RenderTexture; transient progression; no SaveManager.");
        Require(SaveManager.Instance == null && RunManager.Instance == null, "No save or expedition owner in disposable fixture");
    }
    private static void Language(string code)
    {
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, code);
        Set(localization, "currentLanguageCode", code);
    }
    private static void Move(Vector2 p)
    {
        player.GetComponent<Rigidbody2D>().position = p; player.transform.position = p;
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static PlayerRadarScanner scanner;
    private static SettlementDefensePurpleCore purple;
    private static int playerHits;
    private static float playerDamage;
    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static void AimAt(Vector3 point, bool fire = false, bool scan = false)
    {
        Vector2 screen = camera.WorldToScreenPoint(point);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)((fire ? 1 : 0) | (scan ? 1 << (int)MouseButton.Back : 0)) });
    }
    private static IEnumerable<float> Wait(Func<bool> predicate, string context, float timeout = 20)
    {
        int frame = Time.frameCount; float began = Time.time;
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!predicate() && EditorApplication.timeSinceStartup < deadline) yield return .01f;
        if (!predicate())
            audit.AppendLine("TIMEOUT " + context + " frames=" + (Time.frameCount - frame) + " gameSeconds=" + (Time.time - began) + " scale=" + Time.timeScale + " pause=" + GameplayPauseManager.IsPaused +
                " playerDead=" + player.IsDead + " active=" + encounter.IsActive + " cores=" + string.Join(",", encounter.Cores.Select(c => c.Part + ":" + c.State + ":" + c.Attack + ":" + c.ShotsFired)));
        Require(predicate(), context);
    }
    private static Bullet[] EnemyShots() => Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b => b.Owner == ProjectileOwner.Enemy).ToArray();
    private static IEnumerable<float> DefeatAndFuse(int index)
    {
        var core = encounter.Cores[index];
        core.Health.TakeDamage(10000); yield return .12f;
        Require(core.State == SettlementDefenseCorruptedCore.CoreState.AwaitingReactivation, "Defeat waits for explicit interaction " + core.Part);
        Require(!Get<LineRenderer>(core, "telegraph").enabled && EnemyShots().Length == 0, "Defeat cleans owned telegraph/projectiles " + core.Part);
        int count = core.ShotsFired; yield return .35f;
        Require(core.ShotsFired == count && encounter.FusedCount == index, "No shots or auto-fusion while awaiting reactivation");
        Move(core.transform.position + Vector3.down * .8f); yield return .15f;
        Require(ReferenceEquals(interactor.CurrentTarget, core), "Physical defeated-core target acquired " + core.Part);
        Capture("reactivate-" + index);
        interactor.TryInteract(); yield return .35f;
        Capture("fusion-" + index);
        foreach (float wait in Wait(() => index < 2 ? encounter.Cores[index + 1].State == SettlementDefenseCorruptedCore.CoreState.CombatActive : encounter.IsPurpleActive,
            "Fusion advances into " + (index < 2 ? "next component" : "Purple, not completion"))) yield return wait;
    }
    private static IEnumerable<float> QuickScan()
    {
        AimAt(purple.transform.position, false, true); yield return .12f;
        AimAt(purple.transform.position); yield return .08f;
    }
    private static int ShaderMaterials() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.shader != null && m.shader.name == "VOID SCRAPPER/Route Core/SG_RouteCoreCorruption");
    private static IEnumerable<float> ReachPurple()
    {
        Require(route.BeginSettlementDefense(), "Existing authority starts fresh encounter");
        foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Retry starts Orange")) yield return wait;
        player.SetDashInvincible(true);
        for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        Move(new Vector2(-1.8f, -1.7f)); AimAt(purple.transform.position);
    }
    private static IEnumerable<float> Check()
    {
        Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        scanner = Get<PlayerRadarScanner>(encounter, "deckRadar"); purple = Get<SettlementDefensePurpleCore>(encounter, "purpleCore");
        var data = (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), 8 });
        var ships = Get<ShipDefinition[]>(encounter, "ships");
        progress.LoadFromSave(data); progress.SetLastSelectedWeaponTree(WeaponTreeType.MachineGun); progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == WeaponTreeType.MachineGun).ShipId);
        encounter.EnterDeck(); yield return 1f; camera.Render(); yield return .2f;
        EventSystem.current.SetSelectedGameObject(null);
        Capture("01-deck-normal-mg-idle-objective-return");
        var heat = canvas.GetComponentInChildren<WeaponHeatUI>(true);
        Require(heat.GetComponent<CanvasGroup>().alpha == 1, "Machine Gun heat track visible at zero heat");
        var button = back.GetComponent<Button>();
        button.OnPointerEnter(new PointerEventData(EventSystem.current)); yield return .15f; Capture("02-return-hover");
        button.OnPointerExit(new PointerEventData(EventSystem.current)); EventSystem.current.SetSelectedGameObject(back);
        yield return .15f; Capture("03-return-focus");
        button.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        yield return .15f; Capture("04-return-pressed-selection");
        button.OnPointerUp(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        EventSystem.current.SetSelectedGameObject(null);
        Set(GameStateManager.Instance, "currentState", GameState.SettlementDefense);
        AimAt(route.transform.position, true); yield return .5f; Capture("05-machinegun-heat");
        Require(((MachineGunWeapon)player.GetComponent<PlayerWeaponController>().CurrentWeapon).HeatRatio > 0, "Real firing produces heat");
        AimAt(route.transform.position); Set(GameStateManager.Instance, "currentState", GameState.Settlement); encounter.LeaveDeck(); yield return .1f;
        data.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(SectorTechnologyCatalog.StabilizedFrameId, 3));
        data.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(SectorTechnologyCatalog.ReinforcedBulkheadId, 3));
        progress.LoadFromSave(data); progress.SetLastSelectedWeaponTree(WeaponTreeType.MachineGun);
        encounter.EnterDeck(); yield return .2f; Capture("06-hp26-armor6");
        Require(player.MaxHp == 26 && player.GetComponent<PlayerArmor>().CurrentArmor == 6, "Authored HP/Armor read permanent technology via events");
        encounter.LeaveDeck(); yield return .1f;
        progress.SetLastSelectedWeaponTree(WeaponTreeType.Shotgun); progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == WeaponTreeType.Shotgun).ShipId);
        encounter.EnterDeck(); yield return .2f; Capture("07-shotgun-no-heat");
        Require(heat.GetComponent<CanvasGroup>().alpha == 0, "Shotgun has no fake heat UI");
        encounter.LeaveDeck(); yield return .1f;
        progress.SetLastSelectedWeaponTree(WeaponTreeType.Sniper); progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == WeaponTreeType.Sniper).ShipId);
        encounter.EnterDeck(); yield return .2f;
        Set(GameStateManager.Instance, "currentState", GameState.SettlementDefense);
        AimAt(route.transform.position, true); yield return .45f; Capture("08-sniper-charging");
        var charge = canvas.GetComponentInChildren<PlayerChargeGaugeUI>(true);
        Require(charge.GetComponent<CanvasGroup>().alpha > .9f && heat.GetComponent<CanvasGroup>().alpha == 0, "Sniper charge follows player and never adds heat");
        AimAt(route.transform.position); yield return .3f; Capture("09-sniper-release");
        Require(charge.GetComponent<CanvasGroup>().alpha == 0, "Sniper release cleans charge gauge");
        Set(GameStateManager.Instance, "currentState", GameState.Settlement);
        encounter.LeaveDeck(); yield return .1f;
        progress.SetLastSelectedWeaponTree(WeaponTreeType.MachineGun); progress.SetSelectedShipId(ships.First(s => s.DefaultWeaponTree == WeaponTreeType.MachineGun).ShipId);
        encounter.EnterDeck(); yield return .2f;
        Require(route.BeginSettlementDefense(), "Existing Route Core authority starts defense");
        yield return .15f; Capture("10-active-defense-return-hidden"); Require(!back.activeSelf, "Active defense hides Return presentation");
        foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Orange combat ready")) yield return wait;
        player.SetDashInvincible(true);
        for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        Move(new Vector2(-1.8f, -1.7f)); AimAt(purple.transform.position); yield return .3f;
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .2f;
        Capture("11-purple-radar-hint"); Require(scanner.IsRadarOpen && Get<GameObject>(encounter, "radarPresentation").activeSelf, "Purple retains binding-aware Radar hints");
        foreach (float wait in QuickScan()) yield return wait;
        Capture("12-purple-exposed");
        encounter.CancelEncounter(); yield return .2f;
        Require(back.activeSelf, "Return exact prior active state restored after cancellation");
        Require(!Get<GameObject>(encounter, "radarPresentation").activeSelf, "Radar hints hidden outside Purple");
        var controller = RouteCoreDeckAuthoring.Single<SettlementController>(SceneManager.GetActiveScene());
        var ui = RouteCoreDeckAuthoring.Single<SettlementUIController>(SceneManager.GetActiveScene());
        controller.enabled = true; controller.gameObject.SetActive(true); ui.enabled = true; ui.gameObject.SetActive(true);
        management.SetActive(true); yield return .3f; ui.ShowMainPanel();
        foreach (string language in new[] { "ko", "en" })
        {
            Language(language);
            foreach (string scenario in new[] { "base", "current", "bonuses", "breacher", "lancer", "frame", "many", "locked" })
            {
                var state = scenario == "base" || scenario == "locked" ? new SaveData() :
                    (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), 10 });
                state.equipmentLoadoutTraitIds.Clear(); state.manufacturedEquipmentIds.Clear(); state.sectorTechnologyLevels.Clear();
                int index = scenario == "breacher" ? 1 : scenario == "lancer" || scenario == "locked" ? 2 : 0;
                string[] fitted = scenario == "frame" ? new[] { StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId } :
                    scenario == "many" ? EquipmentFinalRosterAuthoring.Roster.SelectMany(r => r).ToArray() :
                    scenario == "bonuses" ? new[] { "shared_cargo_bay", "shared_engine_tuning", "mg_stable_feed" } : Array.Empty<string>();
                state.equipmentLoadoutTraitIds.AddRange(fitted); state.manufacturedEquipmentIds.AddRange(fitted);
                if (scenario == "bonuses" || scenario == "many") foreach (var tech in SectorTechnologyCatalog.Definitions)
                    state.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(tech.Id, 3));
                if (scenario == "current") state = JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/HudHangar/Baseline/current-save.json"));
                progress.LoadFromSave(state); Set(controller, "previewShipIndex", index); ui.ShowMainPanel(); ui.Refresh();
                yield return .2f;
                Capture("hangar-" + language + "-" + scenario);
                var hud = RouteCoreDeckAuthoring.Single<SettlementHUD>(SceneManager.GetActiveScene());
                var body = Get<TMP_Text>(hud, "shipBodyText");
                Require(!body.text.Contains("+0%") && !body.text.Contains("추가 패시브 없음"), language + "/" + scenario + " no zero/empty passive rows");
                audit.AppendLine("BODY " + language + "/" + scenario + " " + body.text);
                foreach (string row in body.text.Split('\n')) if (row.Contains("<pos=112>"))
                    foreach (string cell in row.Split(new[] { "<pos=112>" }, StringSplitOptions.None))
                    {
                        float width = body.GetPreferredValues(cell, Mathf.Infinity, Mathf.Infinity).x;
                        Require(width <= 108, language + "/" + scenario + " column width " + width.ToString("F1") + " <= 108");
                    }
                if (scenario == "many")
                {
                    var scroll = Get<ScrollRect>(hud, "shipDetailScroll"); scroll.verticalNormalizedPosition = 0;
                    yield return .1f; Capture("hangar-" + language + "-many-bottom");
                }
            }
        }
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        audit.AppendLine("Actual saved scene in Play Mode, actual 480x270 rendering. Transient QA loadouts; no user save or authored scene was changed by rendering.");
    }

    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var charge = canvas.GetComponentInChildren<PlayerChargeGaugeUI>(true);
        if (canvas.gameObject.activeInHierarchy && charge.GetComponent<CanvasGroup>().alpha > .9f)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, charge.transform.position);
            Vector2 ship = camera.WorldToScreenPoint(player.transform.position);
            Require(point.x > 20 && point.x < 460 && point.y > 10 && point.y < 260,
                "Visible charge/cooling gauge stays inside 480x270: " + point);
            Require(Mathf.Abs(point.y - ship.y - 35.2f) < 2, "Charge/cooling uses Expedition's fixed 1.1-world-unit player offset");
        }
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine("CAPTURE " + name + " visibleProjectiles=" + VisibleProjectiles());
        foreach (TMP_Text text in (management.activeInHierarchy ? management : canvas.gameObject).GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrEmpty(text.text) || text.font == null) continue;
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) audit.AppendLine("OVERFLOW " + text.name + ": " + text.text);
            foreach (char c in System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", ""))
                if (!char.IsWhiteSpace(c) && !text.font.HasCharacter(c, true, true)) audit.AppendLine("MISSING U+" + ((int)c).ToString("X4") + " " + text.name);
        }
    }
    private static int VisibleProjectiles() => Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b =>
    {
        Vector3 p = camera.WorldToViewportPoint(b.transform.position);
        return p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f && p.z > 0;
    });
}

