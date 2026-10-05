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

// Disposable saved-scene QA. Uses isolated transaction-test.json only; never saves scenes or user progression.
[InitializeOnLoad]
public static class RouteCoreCombatProbe
{
    private const string Key = "RouteCoreCombat.Render";
    private const string Output = "Logs/RouteCoreCombat/Rendered/";
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
    static RouteCoreCombatProbe() { EditorApplication.playModeStateChanged += Changed; }
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
    private static IEnumerable<float> Check()
    {
        Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        scanner = Get<PlayerRadarScanner>(encounter, "deckRadar"); purple = Get<SettlementDefensePurpleCore>(encounter, "purpleCore");
        var data = (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), 8 });
        progress.LoadFromSave(data);
        var ship = Get<ShipDefinition[]>(encounter, "ships").First(s => s.DefaultWeaponTree == WeaponTreeType.MachineGun);
        progress.SetSelectedShipId(ship.ShipId);
        encounter.EnterDeck(); yield return .4f;
        int completions = 0; route.DefenseCompleted.AddListener(() => completions++);
        Require(!scanner.TryToggleRadarMode() && !scanner.TryQuickScan(), "Radar input locked during ordinary Deck inspection");
        Require(route.BeginSettlementDefense(), "Existing persistent request begins the full encounter");
        foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Opening reaches Orange")) yield return wait;
        Move(new Vector2(-1.5f, -2.3f));
        float previousHp = player.CurrentHp;
        player.Changed += (current, max) => { float hp = player.CurrentHp; if (hp < previousHp) { playerHits++; playerDamage += previousHp - hp; } previousHp = hp; };
        var colors = Get<SpriteRenderer[]>(encounter, "blackoutRenderers").Select(r => r.color).ToArray();
        var environment = Get<SpriteRenderer[]>(encounter, "blackoutRenderers");
        var playerColors = player.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.color).ToArray();
        var orange = encounter.Cores[0]; double started = EditorApplication.timeSinceStartup;
        foreach (float wait in Wait(() => orange.Attack == SettlementDefenseCorruptedCore.AttackStage.Locked, "Orange committed cone telegraph")) yield return wait;
        Vector2 committed = orange.CommittedDirection; Capture("01-orange-telegraph");
        Move(new Vector2(-.5f, -2.4f)); yield return .07f;
        Require(Vector2.Dot(committed, orange.CommittedDirection) > .9999f, "Orange does not track after commitment");
        foreach (float wait in Wait(() => orange.ShotsFired >= 5, "Orange first five pellets")) yield return wait;
        yield return .19f; // Slow pellets need more travel than the sniper shot to leave the opaque core.
        Capture("02-orange-first-volley");
        Require(EnemyShots().All(b => Vector2.Angle(b.MoveDirection, committed) < 40), "Orange cone has gaps and no 360-degree coverage");
        foreach (float wait in Wait(() => orange.ShotsFired >= 10, "Orange second five pellets")) yield return wait;
        yield return .2f;
        Capture("03-orange-second-volley");
        Require(orange.ShotsFired == 10, "Orange exactly two five-pellet volleys");
        yield return .6f; Require(orange.ShotsFired == 10, "Orange recovery has no extra fire");
        audit.AppendLine("METRIC Orange scripted observation " + (EditorApplication.timeSinceStartup - started).ToString("F2") + "s, shots=" + orange.ShotsFired + ", player HP=" + player.CurrentHp);
        player.SetDashInvincible(true); // Remaining QA isolates lifecycle, not difficulty or survival balance.
        foreach (float wait in DefeatAndFuse(0)) yield return wait;

        var blue = encounter.Cores[1]; started = EditorApplication.timeSinceStartup;
        Move(new Vector2(-3.5f, -.75f));
        foreach (float wait in Wait(() => blue.Attack == SettlementDefenseCorruptedCore.AttackStage.Aim, "Blue AIM line precedes damage")) yield return wait;
        var beforeAim = blue.CommittedDirection; Capture("04-blue-aim"); Move(new Vector2(-2, -2)); yield return .1f;
        Require(Vector2.Angle(beforeAim, blue.CommittedDirection) > 2, "Blue AIM tracks moving target");
        foreach (float wait in Wait(() => blue.Attack == SettlementDefenseCorruptedCore.AttackStage.Locked, "Blue LOCK freezes direction")) yield return wait;
        committed = blue.CommittedDirection; Capture("05-blue-lock"); Move(new Vector2(-3.5f, -2.5f)); yield return .05f;
        Require(Vector2.Dot(committed, blue.CommittedDirection) > .9999f, "Blue LOCK ignores later player movement");
        var line = Get<LineRenderer>(blue, "telegraph");
        Require(Vector2.Dot(((Vector2)(line.GetPosition(1) - line.GetPosition(0))).normalized, committed) > .9999f, "Locked line matches committed shot direction");
        foreach (float wait in Wait(() => blue.ShotsFired >= 1, "Blue first projectile")) yield return wait;
        Require(EnemyShots().Any(b => Vector2.Dot(b.MoveDirection, committed) > .9999f), "Blue projectile follows exact locked direction");
        yield return .09f;
        Capture("06-blue-shot");
        foreach (float wait in Wait(() => blue.ShotsFired >= 3, "Blue three-shot cycle")) yield return wait;
        yield return .09f;
        Capture("07-blue-third-shot"); Require(blue.ShotsFired == 3, "Blue exactly three precision shots");
        yield return .6f; Require(blue.ShotsFired == 3, "Blue cycle recovery separates reacquisition");
        audit.AppendLine("METRIC Blue scripted cycle " + (EditorApplication.timeSinceStartup - started).ToString("F2") + "s, shots=" + blue.ShotsFired);
        foreach (float wait in DefeatAndFuse(1)) yield return wait;

        var green = encounter.Cores[2]; started = EditorApplication.timeSinceStartup; Move(new Vector2(-2, -2));
        foreach (float wait in Wait(() => green.Attack == SettlementDefenseCorruptedCore.AttackStage.Locked, "Green warmup precedes stream")) yield return wait;
        committed = green.CommittedDirection; Capture("08-green-warmup"); Move(new Vector2(-3, 0));
        foreach (float wait in Wait(() => green.ShotsFired >= 5, "Green sustained stream")) yield return wait;
        Capture("09-green-stream");
        Require(Vector2.Dot(committed, green.CommittedDirection) > .9999f && EnemyShots().All(b => Vector2.Angle(b.MoveDirection, committed) <= 13), "Green bounded sweep does not reacquire each bullet");
        foreach (float wait in Wait(() => green.Attack == SettlementDefenseCorruptedCore.AttackStage.Recovery, "Green enters cooling")) yield return wait;
        Require(green.ShotsFired == 10, "Green ten shots per burst"); Capture("10-green-cooling"); yield return 1f;
        Require(green.ShotsFired == 10, "Green long cooling gap");
        audit.AppendLine("METRIC Green scripted cycle " + (EditorApplication.timeSinceStartup - started).ToString("F2") + "s, shots=" + green.ShotsFired);
        foreach (float wait in DefeatAndFuse(2)) yield return wait;

        Move(new Vector2(-1.8f, -1.7f)); yield return .2f;
        Require(encounter.FusedCount == 3 && encounter.IsActive && !progress.SettlementDefenseCleared && completions == 0, "Third fusion starts Purple without saving completion");
        Require(purple.Health.MaxHp == 50 && purple.State == SettlementDefensePurpleCore.Phase.Hidden, "Purple starts hidden with provisional 50 HP");
        Require(!scanner.IsRadarOpen && purple.ExposureCount == 0, "Purple starts with Radar closed and no passive exposure");
        Capture("11-purple-blackout-radar-closed");
        Require(environment.Select(r => r.color).Where((c, i) => Mathf.Abs(c.r - colors[i].r * .25f) > .0001f).Count() == 0, "Environment exactly quarter brightness while hidden");
        purple.Health.TakeDamage(1000); Require(purple.Health.CurrentHp == 50, "Hidden direct damage rejected");
        // A real player projectile crosses the separate radar trigger while damage collision is off.
        AimAt(purple.transform.position, true); yield return .55f; AimAt(purple.transform.position); yield return .15f;
        Require(purple.Health.CurrentHp == 50, "Hidden real player fire cannot damage Purple");
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .25f;
        Require(scanner.IsRadarOpen && purple.ExposureCount == 0, "Actual Q opens Radar without exposing"); Capture("12-purple-radar-open");
        int scans = 0; scanner.ScanCompleted += (p, r, targets) => scans++;
        foreach (float wait in QuickScan()) yield return wait;
        Require(scans == 1, "Actual Mouse4 produces one active scan (count=" + scans + ")");
        Require(scanner.LastScannedTargets.Contains(purple.Target) && purple.ExposureCount == 1, "Active scan detects hidden sibling target and exposes once (targets=" + scanner.LastScannedTargets.Count + ", exposures=" + purple.ExposureCount + ")");
        Capture("13-purple-detection-exposed");
        foreach (float wait in Wait(() => purple.ShotsFired == 8, "Purple single slow eight-way pressure pulse")) yield return wait;
        yield return .3f;
        Capture("14-purple-pressure");
        yield return .6f;
        foreach (float wait in QuickScan()) yield return wait;
        Require(scans == 2 && purple.ExposureCount == 1, "Scan while exposed does not restart timer or pulse");
        foreach (float wait in Wait(() => purple.State == SettlementDefensePurpleCore.Phase.Hidden, "Fixed exposure expires and re-hides")) yield return wait;
        Require(EnemyShots().Length == 0, "No stale Purple projectiles after re-hide"); Capture("15-purple-rehidden");
        Language("en");
        int windows = purple.ExposureCount;
        foreach (float wait in QuickScan()) yield return wait;
        Require(purple.ExposureCount == windows + 1, "Second active scan opens fresh window");
        AimAt(purple.transform.position, true);
        foreach (float wait in Wait(() => purple.Health.CurrentHp < 50, "Exposed real player projectile damage accepted")) yield return wait;
        Capture("16-purple-en-real-damage"); AimAt(purple.transform.position);
        encounter.gameObject.SetActive(false); yield return .15f;
        Require(!deck.activeSelf && !scanner.IsRadarOpen && !purple.Target.IsRadarVisible && EnemyShots().Length == 0 &&
            !(Get<Delegate>(scanner, "ScanCompleted")?.GetInvocationList().Any(d => ReferenceEquals(d.Target, purple)) ?? false),
            "Active Purple controller-disable cleans scan subscription, target, Radar and projectiles");
        Require(environment.Select(r => r.color).SequenceEqual(colors) && !progress.SettlementDefenseCleared,
            "Active Purple disable restores colors without clearing defense");
        encounter.gameObject.SetActive(true); encounter.EnterDeck(); player.SetDashInvincible(true);
        Require(route.BeginSettlementDefense(), "Controller re-enable can retry full encounter");
        foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Disable retry starts Orange")) yield return wait;
        for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .25f;
        foreach (float wait in QuickScan()) yield return wait;
        Require(purple.State == SettlementDefensePurpleCore.Phase.Exposed, "Death fixture reaches actual exposed phase after controller retry");
        // Validate interruptions separately before allowing authoritative completion.
        foreach (string stage in new[] { "Purple exposed", "Purple hidden", "Orange", "Blue", "Green" })
        {
            if (stage == "Purple hidden")
                foreach (float wait in Wait(() => purple.State == SettlementDefensePurpleCore.Phase.Hidden, "Death fixture reaches hidden")) yield return wait;
            if (stage == "Blue" || stage == "Green")
            {
                foreach (float wait in DefeatAndFuse(0)) yield return wait;
                if (stage == "Green") foreach (float wait in DefeatAndFuse(1)) yield return wait;
            }
            player.SetDashInvincible(false); Set(player, "invincibleTimer", 0f); player.TakeDamage(100000); yield return .15f;
            Require(!deck.activeSelf && !encounter.IsActive && !scanner.IsRadarOpen && !purple.gameObject.activeSelf && EnemyShots().Length == 0, stage + " real player death cleans combat, Radar, target and bullets");
            Require(!progress.SettlementDefenseCleared && progress.CurrentRouteCoreState == RouteCoreState.Activated, stage + " failure retains Activated / uncleared");
            Require(environment.Select(r => r.color).SequenceEqual(colors), stage + " restores exact environment colors");
            encounter.EnterDeck(); player.SetDashInvincible(true); Require(route.BeginSettlementDefense(), stage + " retry accepted");
            foreach (float wait in Wait(() => encounter.Cores.Count == 3 && encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Retry starts fresh Orange with three actors")) yield return wait;
            Require(encounter.FusedCount == 0 && !encounter.IsPurpleActive && !scanner.TryToggleRadarMode(), "Retry has no partial Purple progress and Radar is locked");
            if (stage == "Purple exposed")
                for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        }
        // Fresh full completion after all five real death paths.
        for (int i = 0; i < 3; i++) foreach (float wait in DefeatAndFuse(i)) yield return wait;
        Keys(UnityEngine.InputSystem.Key.Q); yield return .1f; Keys(); yield return .25f;
        foreach (float wait in QuickScan()) yield return wait;
        Require(purple.State == SettlementDefensePurpleCore.Phase.Exposed, "Final attempt exposed via real scan");
        Capture("17-final-attempt-exposed");
        Move(new Vector2(-1.8f, -1.7f));
        float winningStart = Time.time;
        double winningDeadline = EditorApplication.timeSinceStartup + 60;
        while (encounter.IsActive && EditorApplication.timeSinceStartup < winningDeadline)
        {
            if (purple.State == SettlementDefensePurpleCore.Phase.Hidden)
                foreach (float wait in QuickScan()) yield return wait;
            AimAt(purple.transform.position, true); yield return .1f;
        }
        AimAt(purple.transform.position); yield return .6f;
        audit.AppendLine("METRIC Purple real Machine Gun clear: " + (Time.time - winningStart).ToString("F2") +
            " gameplay seconds, " + purple.ExposureCount + " scan windows; player invulnerable for controlled aiming QA.");
        Require(progress.SettlementDefenseCleared && progress.CanLaunchFinalExpedition && completions == 1, "Purple death alone completes campaign defense once and enables FinalNetwork");
        encounter.PurpleCleansed(purple); route.CompleteSettlementDefense(); Require(completions == 1, "Duplicate death/completion cannot grant twice");
        Require(!scanner.IsRadarOpen && !purple.Target.IsRadarVisible && !Get<GameObject>(encounter, "radarPresentation").activeSelf && EnemyShots().Length == 0, "Completion closes Radar, unregisters target and clears only encounter fire");
        Require(environment.Select(r => r.color).SequenceEqual(colors), "Completion restores all original environment colors");
        encounter.EnterDeck(); Move(route.transform.position + Vector3.down * .9f); yield return .3f;
        Require(ReferenceEquals(interactor.CurrentTarget, route) && route.CanInteract(player.gameObject), "Clean post-defense physical core offers final-launch interaction");
        Capture("18-clean-post-defense");
        EventSystem.current.SetSelectedGameObject(back);
        ExecuteEvents.Execute(back, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); yield return .15f;
        Require(!deck.activeSelf && management.activeSelf && !movement.HasTemporaryAimCamera(encounter), "ReturnToFacilities restores management and releases camera");
        encounter.EnterDeck(); encounter.gameObject.SetActive(false); yield return .1f;
        Require(!deck.activeSelf && !scanner.IsRadarOpen, "Controller disable closes all Deck ownership");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        audit.AppendLine("METRIC player hits=" + playerHits + " damage=" + playerDamage + " (includes five deliberate lethal QA hits). Orange first cycle used normal damage; later lifecycle checks used invincibility.");
        audit.AppendLine("METRIC Purple first attempt: two windows, second window accepted real MG damage; final attempt cleared using real MG input/projectiles. No human balance claim.");
        audit.AppendLine("Component defeats for sequence coverage are scripted EnemyHealth damage. Actual player input verified Radar Q/Mouse4, hidden/exposed shooting, Purple clear and explicit reactivation.");
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
