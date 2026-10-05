using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Saved Expedition generator/player/art, disposable Play Mode owners. Never saves game or scene data.
[InitializeOnLoad]
public static class EnemyRosterRenderProbe
{
    private const string Key = "EnemyRoster.Render";
    private static bool OperationsOnly => SessionState.GetBool(Key + ".OperationsOnly", false);
    private static string Output => OperationsOnly ? "Logs/Region3Repeat/Rendered/" : "Logs/EnemyRoster/Rendered/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly StringBuilder audit = new StringBuilder();
    private static IEnumerator<float> sequence;
    private static double nextAt;
    private static double nextReportAt;
    private static ExpeditionMapGenerator generator;
    private static PlayerHealth player;
    private static Camera camera;
    private static RenderTexture target;
    private static RunManager manager;
    private static PermanentProgress progress;
    private static Unity.Profiling.ProfilerRecorder allocations;
    private static readonly List<long> allocationSamples = new List<long>();
    private static int collectionsAtStart;
    internal static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    internal static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    internal static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); audit.AppendLine("PASS " + message); }
    static EnemyRosterRenderProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run() => Begin(false);
    public static void RunRegion3Operations() => Begin(true);
    private static void Begin(bool operationsOnly)
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before validation.");
        SessionState.SetBool(Key + ".OperationsOnly", operationsOnly);
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "failure.txt")) File.Delete(Output + "failure.txt");
        var scene = EditorSceneManager.OpenScene(EnemyRosterAudit.ScenePath, OpenSceneMode.Single);
        generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene);
        player = EnemyRosterAudit.Single<PlayerHealth>(scene);
        camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var keep = new List<GameObject> { generator.gameObject, player.gameObject, camera.gameObject,
            Get<SpaceBackgroundGenerator2D>(generator, "backgroundGenerator").gameObject, Get<Transform>(generator, "startPoint").gameObject };
        keep.AddRange(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true)).Select(c => c.gameObject));
        foreach (var go in keep.Distinct()) { go.SetActive(false); go.transform.SetParent(null, true); }
        foreach (var root in scene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        Set(generator, "generateOnStart", false);
        // Camera follow and pixel-perfect components remain authored; freeze follow only for reproducible captures.
        foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
            if (behaviour.GetType().Name == "GungeonStyleCamera2D") behaviour.enabled = false;
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { sequence = (OperationsOnly ? CheckRegion3Operations() : Check()).GetEnumerator(); nextAt = EditorApplication.timeSinceStartup + .2; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
    }
    private static void Tick()
    {
        if (allocations.Valid && Time.timeScale > 0) allocationSamples.Add(allocations.LastValue);
        if (EditorApplication.timeSinceStartup >= nextReportAt)
        { File.WriteAllText(Output + "audit.txt", audit.ToString()); nextReportAt = EditorApplication.timeSinceStartup + 3; }
        if (EditorApplication.timeSinceStartup < nextAt) return;
        try
        {
            if (sequence.MoveNext()) nextAt = EditorApplication.timeSinceStartup + sequence.Current;
            else
            {
                if (allocationSamples.Count > 0)
                {
                    allocationSamples.Sort();
                    audit.AppendLine("Editor Play Mode GC bytes/frame sampled: median=" + allocationSamples[allocationSamples.Count / 2] + " p95=" + allocationSamples[(int)(allocationSamples.Count * .95f)] + " max=" + allocationSamples.Last() + " gen0Collections=" + (GC.CollectionCount(0) - collectionsAtStart) + ". Includes Editor/capture/log overhead; not a standalone performance benchmark.");
                }
                allocations.Dispose(); File.WriteAllText(Output + "audit.txt", audit.ToString()); EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); File.WriteAllText(Output + "failure.txt", ex.ToString());
            File.WriteAllText(Output + "audit.txt", audit.ToString()); EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false); EditorApplication.Exit(1);
        }
    }
    private static void Setup()
    {
        // Synchronous GPU readback in the Editor can stall one frame; don't let that stall
        // skip an entire short melee dash before its next observation.
        Time.maximumDeltaTime = 1f / 30f;
        var scene = SceneManager.GetActiveScene();
        generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene); player = EnemyRosterAudit.Single<PlayerHealth>(scene);
        camera = EnemyRosterAudit.Single<Camera>(scene);
        var owners = new GameObject("Transient roster validation owners"); owners.SetActive(false);
        manager = owners.AddComponent<RunManager>(); typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        progress = owners.AddComponent<PermanentProgress>(); typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        Set(progress, "equipmentCatalog", AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset"));
        progress.LoadFromSave(new SaveData());
        var state = owners.AddComponent<GameStateManager>(); Set(state, "currentState", GameState.Expedition);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        var store = owners.AddComponent<RunRuntimeTraitStore>(); typeof(RunRuntimeTraitStore).GetField("instance", Static).SetValue(null, store);
        // Exercise the production pooled projectile/pickup path as well as the AI.
        new GameObject("Transient production pool owner").AddComponent<PoolManager>();
        var localization = owners.AddComponent<VoidScrapperLocalizationService>();
        Set(localization, "localizationCatalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath));
        typeof(VoidScrapperLocalizationService).GetField("activeInstance", Static).SetValue(null, localization);
        typeof(GameSettingsRuntime).GetField("loaded", Static).SetValue(null, true);
        typeof(GameSettingsRuntime).GetField("languageCode", Static).SetValue(null, "en"); Set(localization, "currentLanguageCode", "en");
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal));
        target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create(); camera.targetTexture = target;
        foreach (var root in scene.GetRootGameObjects()) if (root != owners) root.SetActive(true);
        player.SetDashInvincible(true); player.GetComponent<PlayerWeaponController>().SetExternalInputLocked(true);
        Require(SaveManager.Instance == null, "Disposable fixture has no SaveManager");
        collectionsAtStart = GC.CollectionCount(0);
        allocations = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
        audit.AppendLine("Saved Expedition generator/player/background/camera; actual 480x270 RenderTexture; no asset or save writes.");
    }
    private static IEnumerable<float> Check()
    {
        Setup(); yield return .2f;
        Time.timeScale = 0;
        foreach (var depth in new[] { ExpeditionDepth.Normal, ExpeditionDepth.DeepZone1, ExpeditionDepth.DeepZone2 })
        {
            if (depth == ExpeditionDepth.DeepZone2)
            {
                progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            }
            Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, depth, "basic_ship", SeaRegionType.DenseDebris));
            foreach (int seed in new[] { 401, 917, 2026 })
            {
                generator.ClearGeneratedObjects();
                int frame = Time.frameCount; while (Time.frameCount <= frame + 1) yield return .02f;
                UnityEngine.Random.InitState(seed); generator.Generate(); yield return .1f;
                frame = Time.frameCount; while (Time.frameCount <= frame + 1) yield return .02f;
                Diagnose(depth, seed);
            }
        }
        // Region 2 contains all seven combat identities. Use its actual generated actors below.
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.DeepZone1, "basic_ship", SeaRegionType.DenseDebris));
        generator.ClearGeneratedObjects(); int cleanFrame = Time.frameCount;
        while (Time.frameCount <= cleanFrame + 1) yield return .02f;
        UnityEngine.Random.InitState(401); generator.Generate(); Time.timeScale = 1; yield return 1.8f;
        foreach (float delay in CombatChecks()) yield return delay;
        foreach (float delay in RoleAndBaseChecks()) yield return delay;
    }
    private static IEnumerable<float> CheckRegion3Operations()
    {
        Setup(); yield return .2f;
        var operation = EnemyRosterAudit.Single<ExpeditionOperationController>(SceneManager.GetActiveScene());
        Require(operation.isActiveAndEnabled, "Saved OperationController remains enabled throughout generation");
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.DeepZone2, "basic_ship", SeaRegionType.DenseDebris));
        // Observe the synchronous binding before physics can trigger the encounter and complete it.
        Time.timeScale = 0;
        UnityEngine.Random.InitState(401); generator.Generate();
        var boss = generator.CurrentRegion3BossEncounter;
        Require(generator.UsesRegion3PhaseGatekeeperFoundation && boss != null, "First Region 3 generates its Phase Gatekeeper foundation");
        Require(operation.OperationType == ExpeditionOperationType.SignalInvestigation && operation.State == ExpeditionOperationState.Identified,
            "First Region 3 immediately binds identified Signal Investigation");
        Require(operation.TargetRadar == boss.GetComponent<RadarTarget>() && operation.TargetRadar.MarkerType == RadarMarkerType.Unknown &&
            operation.TargetRadar.IsTemporarilyRevealed, "First-visit Unknown marker and reveal are preserved");
        // Let the new renderers enter the render loop while physics stays paused.
        yield return .1f;
        Capture("first-visit-investigation", boss.EncounterAnchor);
        int completed = 0;
        operation.OperationCompleted += _ => completed++;
        Require(boss.TryForceStartEncounterForDevelopment(out string reason), "Actual first-visit encounter begins: " + reason);
        Require(completed == 1 && operation.State == ExpeditionOperationState.Inactive, "Investigation completes on actual encounter start");

        generator.ClearGeneratedObjects();
        int frame = Time.frameCount; while (Time.frameCount <= frame + 1) yield return .02f;
        progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.DeepZone2, "basic_ship", SeaRegionType.DenseDebris));
        Time.timeScale = 0;
        foreach (int seed in new[] { 401, 917, 2026 })
        {
            generator.ClearGeneratedObjects(); frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return .02f;
            UnityEngine.Random.InitState(seed); generator.Generate(); yield return .1f;
            frame = Time.frameCount; while (Time.frameCount <= frame + 1) yield return .02f;
            Require(!generator.UsesRegion3PhaseGatekeeperFoundation && generator.CurrentRegion3BossEncounter == null,
                "Repeat Region 3 does not expect a Phase Gatekeeper: seed " + seed);
            Require(operation.isActiveAndEnabled && operation.HasOperation && operation.State == ExpeditionOperationState.Search &&
                !Get<bool>(operation, "region3BossInvestigation"), "Repeat operation selects normal generated content: " + operation.OperationType);
            Require(operation.TargetRadar != null && !operation.TargetRadar.IsTemporarilyRevealed, "Repeat operation uses normal search/reveal policy");
            Require(generator.SpawnedCoreObjects.Count == 1, "Repeat Core remains generated");
            var encounter = Call(generator.SpawnedCoreObjects[0], "ResolveBossEncounter");
            Require((bool)encounter.GetType().GetProperty("IsRepeatReplacement").GetValue(encounter), "Core keeps existing repeat Raider route");
            Require(Get<GameObject>(generator.SpawnedCoreObjects[0], "region1RepeatBossPrefab") ==
                (GameObject)encounter.GetType().GetProperty("Prefab").GetValue(encounter), "Repeat boss prefab remains the authored Raider");
            Diagnose(ExpeditionDepth.DeepZone2, seed);
            Capture("repeat-operation-" + seed + "-" + operation.OperationType, operation.TargetRadar.WorldPosition);
        }
        Time.timeScale = 1;
    }
    private static Vector2 SpawnPosition(EnemyBaseAI ai)
    {
        var arrival = ai.GetComponent<EventEnemyArrivalMover>();
        return arrival != null ? Get<Vector2>(arrival, "arrivalPosition") : (Vector2)ai.transform.position;
    }
    private static void Diagnose(ExpeditionDepth depth, int seed)
    {
        Transform root = Get<Transform>(generator, "generatedRoot");
        var enemies = root.GetComponentsInChildren<EnemyBaseAI>(true).Where(e => e.EnemyDefinition != null && !e.EnemyDefinition.EnemyId.StartsWith("shop_")).ToArray();
        Vector2 start = Get<Vector2>(generator, "startPosition");
        audit.AppendLine($"MAP {depth} seed={seed} start={start} enemies={enemies.Length} bases={root.GetComponentsInChildren<FieldBaseController>().Length}");
        foreach (var group in enemies.GroupBy(e => e.EnemyDefinition.EnemyId + "/" + (e.GetComponent<EnemyRoleController>()?.RoleType.ToString() ?? "Patrol")))
            audit.AppendLine("  " + group.Key + "=" + group.Count());
        audit.AppendLine("  minStartDistance=" + enemies.Min(e => Vector2.Distance(start, SpawnPosition(e))).ToString("F2"));
        float pair = float.MaxValue;
        for (int i = 0; i < enemies.Length; i++) for (int j = i + 1; j < enemies.Length; j++)
            pair = Mathf.Min(pair, Vector2.Distance(SpawnPosition(enemies[i]), SpawnPosition(enemies[j])));
        audit.AppendLine("  minPairDistance=" + pair.ToString("F2"));
        Require(enemies.Min(e => Vector2.Distance(start, SpawnPosition(e))) >= 18, depth + " respects start-safe radius seed " + seed);
        Require(enemies.Count(e => e.GetComponent<EnemyRoleController>().RoleType == EnemyRoleType.RivalHarvester) == 1, depth + " generated Rival count " + seed);
        Require(enemies.Count(e => e.GetComponent<EnemyRoleController>().RoleType == EnemyRoleType.Scavenger) == 1, depth + " generated Scavenger count " + seed);
        Require(root.GetComponentsInChildren<FieldBaseController>().Length == 2, depth + " generated base pair " + seed);
        foreach (var e in enemies)
        {
            Vector2 p = SpawnPosition(e);
            var blockers = Physics2D.OverlapCircleAll(p, .3f).Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<EnemyBaseAI>() == null && c.GetComponentInParent<PlayerHealth>() == null).ToArray();
            if (blockers.Length > 0) audit.AppendLine("  OVERLAP " + e.name + " at=" + p + " : " + string.Join(",", blockers.Select(c => EnemyRosterAudit.PathOf(c.transform))));
        }
        File.WriteAllText(Output + "audit.txt", audit.ToString());
    }
    private static void Move(Transform t, Vector2 p)
    {
        var body = t.GetComponent<Rigidbody2D>(); if (body != null) { body.position = p; body.linearVelocity = Vector2.zero; }
        t.position = p; Physics2D.SyncTransforms();
    }
    private static IEnumerable<float> CombatChecks()
    {
        var actors = Get<Transform>(generator, "generatedRoot").GetComponentsInChildren<EnemyBaseAI>();
        foreach (var ai in actors) ai.gameObject.SetActive(false);
        Vector2 stage = generator.StartPosition;
        foreach (string id in new[] { "basic_enemy", "shotgun_enemy", "charge_enemy", "melee_charger", "elite_machinegun", "elite_shotgun", "elite_charging" })
        {
            var ai = actors.First(a => a.EnemyDefinition.EnemyId == id && a.GetComponent<EnemyRoleController>().RoleType == EnemyRoleType.Patrol);
            Move(player.transform, stage + Vector2.left * 2.5f); Move(ai.transform, stage + Vector2.right * 2.5f);
            ai.gameObject.SetActive(true); ai.SetTarget(player.transform); ai.SetHomePosition(ai.transform.position); ai.EngagePlayer();
            var attack = ai.GetComponent<EnemyAttackController>(); int shots = 0;
            Action<EnemyAttackController> fired = _ => shots++; attack.ProjectileFired += fired;
            double deadline = EditorApplication.timeSinceStartup + 12;
            if (id == "melee_charger")
            {
                var melee = ai.GetComponent<EnemyMeleeChargeController2D>();
                while (!melee.IsCharging && EditorApplication.timeSinceStartup < deadline) yield return .02f;
                Require(melee.IsCharging, "Melee approach enters telegraph"); Capture("04-melee-telegraph", stage);
                while (!melee.IsDashing && EditorApplication.timeSinceStartup < deadline) yield return .01f;
                Require(melee.IsDashing, "Melee commits charge");
                Vector2 direction = Get<Vector2>(melee, "lockedDirection"); Move(player.transform, stage + Vector2.up * 3);
                int dashFrame = Time.frameCount; while (Time.frameCount <= dashFrame) yield return 0;
                Require(Vector2.Distance(direction, Get<Vector2>(melee, "lockedDirection")) < .001f, "Committed melee dash does not track player turn");
                Capture("04-melee-dash", stage); yield return .7f;
                Require(!melee.IsDashing, "Missed melee dash exits into recovery");
            }
            else
            {
                if (id.Contains("charg"))
                {
                    while (!attack.IsCharging && EditorApplication.timeSinceStartup < deadline) yield return .02f;
                    Require(attack.IsCharging, id + " exposes charge telegraph"); Capture(id + "-telegraph", stage);
                }
                while (shots == 0 && EditorApplication.timeSinceStartup < deadline) yield return .02f;
                Require(shots > 0, id + " actual attack emits projectiles / " + attack.RangedAttackPattern);
                yield return .08f; Capture(id + "-combat", stage);
                if (id == "elite_machinegun" || id == "elite_shotgun")
                { yield return .6f; Require(shots > 1, id + " repeats burst/volley"); }
            }
            attack.ProjectileFired -= fired; ai.gameObject.SetActive(false); yield return .4f;
        }
    }
    private static IEnumerable<float> RoleAndBaseChecks()
    {
        var root = Get<Transform>(generator, "generatedRoot");
        var bases = root.GetComponentsInChildren<FieldBaseController>();
        var fieldBase = bases.First();
        var chest = fieldBase.GetComponentInChildren<FieldBaseResourceChest>();
        var node = fieldBase.GetComponentInChildren<FieldBaseSecurityNode>();
        var npc = Get<FieldNpcObjective>(fieldBase, "captiveNpc");
        var prison = Get<HarvestObjectHealth>(fieldBase, "npcPrisonMachine");
        var turrets = fieldBase.GetComponentsInChildren<BaseTurretController>();
        Require(turrets.Length == 3 && turrets.All(t => t.PoweredOn), "Base A has three powered turrets");
        Require(bases[1].GetComponentsInChildren<BaseTurretController>().Length == 4, "Base B has four turrets");
        Require(npc != null && npc.IsCaptiveInBase && !prison.TakesDamageFromPlayerProjectiles, "Captive and prison protected while powered");
        Require(!chest.GetComponent<HarvestObjectHealth>().TakesDamageFromPlayerProjectiles, "Storage protected while powered");
        Move(player.transform, node.transform.position + Vector3.down * 3);
        Capture("12-base-powered-security", node.transform.position);
        Capture("12-base-powered-prison", prison.transform.position);
        // Keep the authored defenses and colliders; stop only unrelated AI attacks during loop observation.
        foreach (var turret in Object.FindObjectsByType<BaseTurretController>(FindObjectsSortMode.None)) turret.enabled = false;
        var defender = fieldBase.GetComponentsInChildren<EnemyRoleController>(true).First();
        defender.gameObject.SetActive(true); var guardAI = defender.GetComponent<EnemyBaseAI>();
        Vector2 anchor = defender.ProtectedTarget.position;
        // Across the open upper corridor, not through the security machine's collider.
        Move(defender.transform, anchor + Vector2.left * 12); Move(player.transform, anchor + Vector2.left * 14);
        guardAI.SetTarget(player.transform); guardAI.EngagePlayer(); yield return .2f;
        Require(guardAI.CurrentState == EnemyState.Return || guardAI.CurrentState == EnemyState.Patrol, "Defender breaks aggro outside local leash");
        float beforeDistance = Vector2.Distance(defender.transform.position, anchor); yield return 1f;
        audit.AppendLine("Guard return " + beforeDistance + " -> " + Vector2.Distance(defender.transform.position, anchor) + " state=" + guardAI.CurrentState);
        Require(Vector2.Distance(defender.transform.position, anchor) < beforeDistance, "Defender returns toward assigned zone");
        Capture("08-defender-return", defender.transform.position);
        double guardDeadline = EditorApplication.timeSinceStartup + 12;
        while (Vector2.Distance(defender.transform.position, anchor) > 2.5f && EditorApplication.timeSinceStartup < guardDeadline) yield return .1f;
        Require(Vector2.Distance(defender.transform.position, anchor) <= 2.5f, "Defender reaches useful guard territory");
        defender.gameObject.SetActive(false);
        // Actual harvest -> pickup -> route -> deposit. Other loose harvest targets are hidden only
        // in the fixture to keep the one observed loop deterministic; base structures stay authored.
        foreach (var harvest in root.GetComponentsInChildren<HarvestObjectHealth>())
            if (harvest.GetComponentInParent<FieldBaseController>() == null) harvest.gameObject.SetActive(false);
        var route = fieldBase.GetComponentsInChildren<FieldBaseCargoRoute2D>().First();
        Vector2 entry = route.GetEntryPosition();
        var supplyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/PF_SupplyContainer.prefab");
        var supply = Object.Instantiate(supplyPrefab, entry + Vector2.left * 4, Quaternion.identity).GetComponentInChildren<HarvestObjectHealth>();
        var rival = root.GetComponentsInChildren<EnemyRoleController>(true).First(r => r.RoleType == EnemyRoleType.RivalHarvester);
        Move(rival.transform, entry + Vector2.left * 6); rival.gameObject.SetActive(true);
        rival.ConfigureAsRivalHarvester(generator.MapBounds); rival.SetHomeBase(fieldBase);
        var rivalAI = rival.GetComponent<EnemyBaseAI>(); rivalAI.SetTarget(player.transform);
        float initialHp = supply.CurrentHp; int initialWeight = chest.StoredWeight;
        double deadline = EditorApplication.timeSinceStartup + 75; bool capturedHarvest = false, capturedCargo = false;
        while (chest.StoredWeight == initialWeight && EditorApplication.timeSinceStartup < deadline)
        {
            Move(player.transform, (Vector2)rival.transform.position + Vector2.down * 9);
            if (rival.CurrentPhase == EnemyRolePhase.Harvesting && !capturedHarvest)
            { Capture("09-rival-harvesting", rival.transform.position); capturedHarvest = true; }
            if (rival.CargoHold.HasCargo && !capturedCargo)
            { Capture("10-rival-return-cargo", rival.transform.position); capturedCargo = true; }
            yield return .1f;
        }
        audit.AppendLine("Rival final phase=" + rival.CurrentPhase + " position=" + rival.transform.position + " target=" + EnemyRosterAudit.Reference(Get<HarvestObjectHealth>(rival, "harvestTarget")) + " hasCargo=" + rival.CargoHold.HasCargo);
        Require(capturedHarvest && capturedCargo, "Rival genuinely harvested and collected drops");
        Require(chest.StoredWeight > initialWeight, "Rival physically returned and deposited at authored storage route");
        Require(!rival.CargoHold.HasCargo, "Deposit removes transferred cargo from Rival");
        Capture("10-rival-deposit-chest", chest.transform.position);
        float stored = chest.StoredWeight;
        // Finish the authored reverse route and verify the next search cannot dismantle base objectives.
        deadline = EditorApplication.timeSinceStartup + 15;
        while (rival.IsEscaping && EditorApplication.timeSinceStartup < deadline)
        { Move(player.transform, (Vector2)rival.transform.position + Vector2.down * 9); yield return .1f; }
        Require(!rival.IsEscaping, "Rival exits through reverse cargo route and resumes existing role");
        yield return .5f;
        var nextHarvest = Get<HarvestObjectHealth>(rival, "harvestTarget");
        Require(nextHarvest == null || nextHarvest.GetComponentInParent<FieldBaseController>() == null, "Rival cannot harvest base storage or prison after delivery");
        rival.gameObject.SetActive(false);
        var scav = root.GetComponentsInChildren<EnemyRoleController>(true).First(r => r.RoleType == EnemyRoleType.Scavenger);
        Move(scav.transform, entry + Vector2.left * 4); scav.gameObject.SetActive(true);
        scav.ConfigureAsScavenger(generator.MapBounds); scav.SetHomeBase(fieldBase); scav.GetComponent<EnemyBaseAI>().SetTarget(player.transform);
        Require(scav.GetComponent<RewardDropper>().TryDropCurrencyRewardAt(entry + Vector2.left * 2, CurrencyType.ScrapParts, 10), "Existing dropper creates stealable field reward");
        initialWeight = chest.StoredWeight; deadline = EditorApplication.timeSinceStartup + 45; bool stolen = false;
        while (chest.StoredWeight == initialWeight && EditorApplication.timeSinceStartup < deadline)
        {
            Move(player.transform, (Vector2)scav.transform.position + Vector2.down * 8);
            if (scav.CargoHold.HasCargo && !stolen) { Capture("11-scavenger-steal-flee", scav.transform.position); stolen = true; }
            yield return .1f;
        }
        Require(stolen && chest.StoredWeight > initialWeight, "Scavenger channels a real field pickup and returns cargo");
        scav.gameObject.SetActive(false);
        float multiplier = chest.GetComponent<RewardDropper>().RuntimeCurrencyMultiplier;
        Require(multiplier > 1, "Cargo increases chest reward multiplier: " + multiplier);
        Capture("14-storage-after-cargo", chest.transform.position);
        int completion = 0; fieldBase.ObjectiveCompleted += _ => completion++;
        node.ForceDisable(false); yield return .1f;
        Require(fieldBase.IsObjectiveCompleted && completion == 1, "Security shutdown completes base with surviving Defenders");
        Require(turrets.All(t => !t.PoweredOn), "Security shutdown immediately powers off turrets");
        Require(fieldBase.GetComponentsInChildren<FieldBaseLaserGate>().All(g => g.IsOpen), "Security shutdown opens authored gates");
        Require(prison.TakesDamageFromPlayerProjectiles && chest.GetComponent<HarvestObjectHealth>().TakesDamageFromPlayerProjectiles, "Security shutdown unlocks prison and storage");
        Require(fieldBase.GetComponentsInChildren<FieldBasePowerLink2D>().All(l => !l.IsVisible), "Security shutdown removes power-link energy");
        Capture("13-base-security-off", node.transform.position);
        Move(player.transform, prison.transform.position + Vector3.down * 2); prison.TakeDamage(999); yield return 3;
        Require(!npc.IsCaptiveInBase && fieldBase.IsPortalSpawned, "Prison destruction rescues NPC and creates existing portal path");
        Capture("15-npc-rescue-portal", Get<Transform>(fieldBase, "portalSpawnPoint").position);
        var portal = Get<GameObject>(fieldBase, "activePortal").GetComponent<FieldBaseTransferPortal>();
        Require(portal != null && portal.DestinationPoint != null, "Rescue portal retains authored/generated destination");
        portal.Interact(player.gameObject); yield return .05f;
        Require(Vector2.Distance(player.transform.position, portal.DestinationPoint.position) < .5f, "Portal transfers through existing authority");
        node.ForceDisable(false); Require(completion == 1, "Security completion is emitted exactly once");
        foreach (float delay in RewardChecks(scav, chest)) yield return delay;
    }
    private static IEnumerable<float> RewardChecks(EnemyRoleController role, FieldBaseResourceChest chest)
    {
        var projectile = EnemyRosterAudit.Definitions.Single(d => d.EnemyId == "basic_enemy").ProjectileDefinition;
        var bullet = PoolManager.Instance.Get(projectile.ProjectilePrefab, new Vector3(0, 20, 0), Quaternion.identity).GetComponent<Bullet>();
        bullet.Initialize(Vector2.right, ProjectileOwner.Enemy, projectile);
        Require(bullet.Owner == ProjectileOwner.Enemy && bullet.gameObject.layer == LayerMask.NameToLayer("EnemyProjectile"), "Pooled enemy projectile restores enemy collision ownership");
        bullet.ForceRelease();
        var recycled = PoolManager.Instance.Get(projectile.ProjectilePrefab, new Vector3(0, 20, 0), Quaternion.identity).GetComponent<Bullet>();
        recycled.Initialize(Vector2.up, ProjectileOwner.Enemy, projectile);
        Require(recycled.MoveDirection == Vector2.up && !recycled.HasPendingRadialSplit && recycled.GetComponentsInChildren<Collider2D>().Any(c => c.enabled), "Pooled projectile reuse clears attack state and restores colliders");
        recycled.ForceRelease();
        var pickupPrefab = Get<GameObject>(role.GetComponent<RewardDropper>(), "rewardPickupPrefab");
        var pickup = PoolManager.Instance.Get(pickupPrefab, new Vector3(0, 20, 0), Quaternion.identity).GetComponent<RewardPickup>();
        pickup.InitializeCurrency(CurrencyType.ScrapParts, 7, Vector2.zero); Set(pickup, "activeAge", 999f);
        Require(pickup.TryTakeCurrencyByEnemy(4, out _, out int first) && first == 4, "First contender takes only four of seven units");
        Require(pickup.TryTakeCurrencyByEnemy(4, out _, out int second) && second == 3, "Second contender gets only remaining units");
        Require(!pickup.TryTakeCurrencyByEnemy(4, out _, out _), "Claimed pooled pickup cannot be taken twice");
        var reused = PoolManager.Instance.Get(pickupPrefab, new Vector3(0, 20, 0), Quaternion.identity).GetComponent<RewardPickup>();
        reused.InitializeOwnedCurrency(CurrencyType.ScrapParts, 5, Vector2.zero, 0); Set(reused, "activeAge", 999f);
        Require(reused.IsAvailable && !reused.CanBeTakenByEnemy, "Reused pickup resets availability and preserves owned-cargo exclusion");
        PoolManager.Instance.Release(reused.gameObject);
        // Compare the actual authored chest's death drops under the same roll seed.
        var prefabChest = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRosterAuthoring.BaseA).GetComponentInChildren<FieldBaseResourceChest>();
        int[] totals = new int[2];
        for (int i = 0; i < 2; i++)
        {
            var instance = Object.Instantiate(prefabChest.gameObject, new Vector3(0, 25 + i * 4, 0), Quaternion.identity).GetComponent<FieldBaseResourceChest>();
            if (i == 1) instance.TryDeposit(CurrencyType.ScrapParts, instance.StorageCapacityWeight);
            var existing = new HashSet<RewardPickup>(RewardPickup.ActivePickups);
            UnityEngine.Random.InitState(8321); instance.GetComponent<HarvestObjectHealth>().TakeDamage(999);
            yield return .1f;
            totals[i] = RewardPickup.ActivePickups.Where(p => p.IsAvailable && !existing.Contains(p) && p.PickupKind == RewardPickupKind.Currency).Sum(p => p.Amount);
        }
        Require(totals[1] > totals[0] && totals[0] > 0, "Destroying filled chest increases actual rolled drops: " + totals[0] + " -> " + totals[1]);
        chest.ResetStorage(); Require(chest.StoredWeight == 0 && chest.GetComponent<RewardDropper>().RuntimeCurrencyMultiplier == 1, "Storage reset clears reward multiplier");
    }
    private static void Capture(string name, Vector2 center)
    {
        camera.transform.position = new Vector3(center.x, center.y, -10); camera.Render();
        RenderTexture old = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG()); Object.DestroyImmediate(texture); RenderTexture.active = old;
        audit.AppendLine("CAPTURE " + name + " at " + center + " ortho=" + camera.orthographicSize);
    }
}
