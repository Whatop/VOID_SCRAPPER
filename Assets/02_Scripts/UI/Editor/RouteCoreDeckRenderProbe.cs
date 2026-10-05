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

// Disposable rendered validation of the saved world. No SaveManager, disk saves, scene saves,
// invented encounter actors, or production state overrides. Damage is test-driven through EnemyHealth.
[InitializeOnLoad]
public static class RouteCoreDeckRenderProbe
{
    private const string Key = "RouteCoreDeck.Render";
    private const string Output = "Logs/RouteCoreDeck/Rendered/";
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
    static RouteCoreDeckRenderProbe() { EditorApplication.playModeStateChanged += Changed; }
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
    private static IEnumerable<float> Check()
    {
        Setup(); encounter.EnterDeck(); yield return .7f;
        Require(deck.activeSelf && !management.activeSelf, "EnterDeck activates saved world and hides management");
        Require(camera.orthographic && Mathf.Approximately(camera.orthographicSize, 4.21875f), "Actual entry camera preserves 15 x 8.4375 framing");
        Require(interactor.CurrentTarget == null, "PlayerStart does not overlap an interaction target");
        Capture("01-ko-missing");
        Require(movement.TryGetTemporaryCameraViewportConstraintBounds(encounter, out Bounds bounds), "Existing viewport constraint owns movement bounds");
        audit.AppendLine("Bounds: " + bounds.min + " .. " + bounds.max);
        Vector2[] directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down,
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        var dash = player.GetComponent<PlayerDash>();
        foreach (Vector2 direction in directions)
        {
            Move(new Vector2(direction.x * 6.65f, direction.y * 3.15f));
            movement.SetMovementVelocityOverride(direction.normalized * 30); yield return .2f;
            movement.ClearMovementVelocityOverride(); CheckBounds(bounds, "move " + direction);
            int serial = dash.CompletedDashSerial;
            Set(movement, "moveInput", direction.normalized); Set(dash, "lastDashTime", -999f);
            Require(dash.TryDash(), "Dash accepted toward edge/corner " + direction); yield return .25f;
            Require(dash.CompletedDashSerial == serial + 1, "Dash completes at " + direction);
            CheckBounds(bounds, "dash " + direction);
            Vector2 edge = player.transform.position;
            movement.SetMovementVelocityOverride(-direction.normalized * 4); yield return .15f;
            movement.ClearMovementVelocityOverride();
            Require(Vector2.Distance(edge, player.transform.position) > .1f, "Can move away from edge/corner " + direction);
        }
        Move(RouteCoreDeckAuthoring.Start);
        progress.TryStartDamagedAccessKeyQuest();
        progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
        progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
        Move(route.transform.position + Vector3.down * .9f); yield return .3f;
        Require(ReferenceEquals(interactor.CurrentTarget, route), "Physical Route Core target acquired");
        interactor.TryInteract();
        Require(progress.CurrentRouteCoreState == RouteCoreState.ReadyToAssemble, "Ready interaction does not assemble in world");
        Capture("02-ko-ready");
        Require(progress.TryRestoreDamagedAccessKey(), "Existing Recovery Processor restoration authority assembles fixture");
        yield return .3f; Capture("03-ko-assembled");
        Require(progress.TryActivateRouteCore(), "Existing progress authority activates fixture");
        yield return .3f; Capture("04-ko-activated");
        Language("en"); yield return .2f;
        Call(interactor, "UpdateCurrentTarget"); Call(encounter, "HandleTargetChanged", route);
        Capture("04-en-activated"); Language("ko");
        // Actual route interaction starts the persistent encounter listener.
        player.SetDashInvincible(true); interactor.TryInteract(); yield return .95f;
        Require(encounter.IsActive && encounter.Cores.Count == 3 && !back.activeSelf, "Defense intro owns three local cores and hides Return");
        Capture("05-introduction"); encounter.LeaveDeck(); Require(deck.activeSelf, "LeaveDeck denied during defense");
        yield return 1.3f;
        Move(new Vector2(-1.5f, -2.5f)); yield return .35f;
        Capture("06-orange-sector-combat");
        Require(encounter.Cores[0].State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Sector remains first active combat core");
        // Cleanup/retry is exercised before the complete sequential fusion route.
        encounter.FailEncounter(); yield return .25f;
        Require(!deck.activeSelf && management.activeSelf && back.activeSelf && encounter.Cores.Count == 0, "Failure cleans local cores and restores navigation/management");
        Require(!progress.SettlementDefenseCleared, "Failure cannot clear campaign defense");
        encounter.EnterDeck(); Move(route.transform.position + Vector3.down * .9f); yield return .2f;
        player.SetDashInvincible(true); interactor.TryInteract(); yield return 2.3f;
        Require(encounter.IsActive && encounter.Cores.Count == 3 && encounter.FusedCount == 0, "Retry creates a fresh three-core sequence");
        for (int i = 0; i < 3; i++)
        {
            var core = encounter.Cores[i];
            Require(core.State == SettlementDefenseCorruptedCore.CoreState.CombatActive, "Ordered combat stage " + core.Part);
            Move(new Vector2(-1.5f, -2.5f)); yield return .1f;
            double shotDeadline = EditorApplication.timeSinceStartup + 4;
            while (VisibleProjectiles() == 0 && EditorApplication.timeSinceStartup < shotDeadline) yield return .03f;
            yield return .12f;
            Require(VisibleProjectiles() > 0, "Actual visible projectile pattern " + core.Part);
            Capture(i == 0 ? "06-orange-sector-retry" : i == 1 ? "07-blue-phase-combat" : "08-green-matter-combat");
            if (i == 1)
            {
                // Aim a real Phase volley through the nonblocking center. Observe travel on both
                // sides of the interaction trigger and capture it over the central art.
                Move(new Vector2(-3.5f, -.75f));
                Bullet crossing = null;
                double crossingDeadline = EditorApplication.timeSinceStartup + 4;
                Vector2 expectedDirection = ((Vector2)player.transform.position - (Vector2)core.transform.position).normalized;
                while (crossing == null && EditorApplication.timeSinceStartup < crossingDeadline)
                {
                    crossing = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).FirstOrDefault(b =>
                        b.Owner == ProjectileOwner.Enemy && b.transform.position.x > 1 && Vector2.Dot(b.MoveDirection, expectedDirection) > .999f);
                    yield return .02f;
                }
                Require(crossing != null, "Phase produces an aimed shot through the center");
                while (crossing != null && crossing.gameObject.activeInHierarchy && crossing.transform.position.x > .3f && EditorApplication.timeSinceStartup < crossingDeadline) yield return .01f;
                Require(crossing != null && crossing.gameObject.activeInHierarchy, "Phase shot reaches central interaction trigger without obstruction");
                Capture("07-blue-through-core");
                while (crossing != null && crossing.gameObject.activeInHierarchy && crossing.transform.position.x > -1 && EditorApplication.timeSinceStartup < crossingDeadline) yield return .01f;
                Require(crossing != null && crossing.gameObject.activeInHierarchy && crossing.transform.position.x <= -1, "Phase shot crosses the central mount unobstructed");
            }
            core.Health.TakeDamage(10000f); Move(core.transform.position + Vector3.down * .9f); yield return .25f;
            Require(ReferenceEquals(interactor.CurrentTarget, core), "Defeated core interaction acquired " + core.Part);
            if (i == 0) Capture("09-defeated-recoverable");
            interactor.TryInteract(); yield return .36f;
            if (i == 0) Capture("10-first-fusion");
            if (i == 2) Capture("11-final-fusion");
            yield return 1.25f;
        }
        // Final center pulse includes its own unscaled completion interval; wait on the owner,
        // rather than assuming the last capture consumed a particular number of player frames.
        double purpleDeadline = EditorApplication.timeSinceStartup + 5;
        while (!encounter.IsPurpleActive && EditorApplication.timeSinceStartup < purpleDeadline) yield return .1f;
        Require(encounter.IsPurpleActive && !progress.SettlementDefenseCleared, "Third fusion requires Purple finale");
        var scanner = player.GetComponent<PlayerRadarScanner>(); scanner.TryToggleRadarMode(); scanner.TryQuickScan();
        Get<SettlementDefensePurpleCore>(encounter, "purpleCore").Health.TakeDamage(10000);
        double completeDeadline = EditorApplication.timeSinceStartup + 5;
        while (!progress.SettlementDefenseCleared && EditorApplication.timeSinceStartup < completeDeadline) yield return .1f;
        audit.AppendLine("Completion state: active=" + encounter.IsActive + " fused=" + encounter.FusedCount + " request=" + route.IsDefenseRequested);
        Require(progress.SettlementDefenseCleared && progress.CanLaunchFinalExpedition, "Purple cleanse after third fusion completes defense and enables final launch");
        Require(!deck.activeSelf && management.activeSelf && back.activeSelf && encounter.Cores.Count == 0, "Completion cleans local actors and restores management");
        encounter.EnterDeck(); Move(route.transform.position + Vector3.down * .9f); yield return .4f;
        Require(!Get<SpriteRenderer>(encounter, "corruptionWave").gameObject.activeSelf, "Post-defense activated world is clean and stable");
        Capture("12-ko-post-defense-stable"); Language("en");
        Call(encounter, "HandleTargetChanged", route); yield return .2f; Capture("12-en-post-defense-stable");
        Move(RouteCoreDeckAuthoring.Start);
        EventSystem.current.SetSelectedGameObject(back); yield return .2f; Capture("13-return-focus");
        ExecuteEvents.Execute(back, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); yield return .2f;
        Require(!deck.activeSelf && management.activeSelf, "ReturnToFacilities submit invokes existing LeaveDeck");
        Require(!movement.HasTemporaryCameraViewportConstraint(encounter) && !camera.orthographic, "Return restores original camera and releases temporary bounds");
        audit.AppendLine("No attack values, projectile patterns, campaign saves or production scene data were changed by this fixture.");
    }
    private static void CheckBounds(Bounds bounds, string context)
    {
        Vector3 p = player.transform.position;
        Require(p.x >= bounds.min.x - .01f && p.x <= bounds.max.x + .01f && p.y >= bounds.min.y - .01f && p.y <= bounds.max.y + .01f,
            "Within playable bounds after " + context + " at " + p);
    }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
        audit.AppendLine("CAPTURE " + name + " visibleProjectiles=" + VisibleProjectiles());
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(false))
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
