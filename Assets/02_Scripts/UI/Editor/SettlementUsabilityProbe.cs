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
public static class SettlementUsabilityProbe
{
    private const string Key = "SettlementUsability.Render";
    private const string Output = "Logs/SettlementUsability/Rendered/";
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
    static SettlementUsabilityProbe() { EditorApplication.playModeStateChanged += Changed; }
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
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static readonly List<string> sounds = new List<string>();
    private static void SoundLog(string message, string trace, LogType type)
    {
        const string prefix = "[Audio] EventId=";
        if (message.StartsWith(prefix)) sounds.Add(message.Substring(prefix.Length).Split(' ')[0]);
    }
    private static void ExpectSound(string expected, string context)
    {
        var ui = sounds.Where(s => s == SoundEventIds.UiClick || s == SoundEventIds.TraitSelect ||
            s == SoundEventIds.UiUnlock || s == SoundEventIds.UiActivate || s == SoundEventIds.UiDeactivate ||
            s == SoundEventIds.UiInsufficient || s == SoundEventIds.UiDisabled).ToArray();
        Require(ui.Length == 1 && ui[0] == expected, context + " exactly one " + expected + " (" + string.Join(",", ui) + ")");
    }
    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static void Aim(Vector2 direction, bool fire = false)
    {
        Vector2 screen = camera.WorldToScreenPoint((Vector2)player.transform.position + direction * 2);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)(fire ? 1 : 0) });
    }
    private static IEnumerable<float> Check()
    {
        Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        Application.logMessageReceived += SoundLog; Set(AudioManager.EnsureExists(), "logPlayedEvents", true);
        Scene scene = SceneManager.GetActiveScene();
        var ui = RouteCoreDeckAuthoring.Single<SettlementUIController>(scene);
        var controller = RouteCoreDeckAuthoring.Single<SettlementController>(scene);
        var panel = RouteCoreDeckAuthoring.Single<ShipTraitTreePanel>(scene);
        var saveObject = new GameObject("Disposable transaction save"); saveObject.SetActive(false);
        var save = saveObject.AddComponent<SaveManager>();
        Set(save, "fileName", Path.GetFullPath(Output + "transaction-test.json")); Set(save, "logSavePath", false);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, save);
        var data = progress.CreateSaveData(); data.scrapParts = 100; progress.LoadFromSave(data);
        controller.gameObject.SetActive(true); controller.LoadSelectionFromProgress();
        ui.gameObject.SetActive(true); ui.enabled = true; yield return .2f;
        ui.ShowTraitPanel();
        panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared); yield return .5f;
        Require(panel.CanUseTraitInput, "Authored Equipment accepts Settlement input (panel=" + panel.isActiveAndEnabled + ", ui=" + ui.isActiveAndEnabled + ", requested=" + Get<bool>(ui, "settlementInputRequested") + ")");
        var tabs = new[] { Get<ShipTraitBranchTabButton>(panel, "sharedTabButton"), Get<ShipTraitBranchTabButton>(panel, "machineGunTabButton") };
        EventSystem.current.SetSelectedGameObject(tabs[0].gameObject); yield return .1f;
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.S); yield return .15f; Keys(); yield return .15f;
        Require(EventSystem.current.currentSelectedGameObject == tabs[1].gameObject, "Actual S navigates Shared to selected-ship branch");
        Require(sounds.Count(s => s == SoundEventIds.UiHover) == 1, "Keyboard focus emits one Hover");
        Keys(UnityEngine.InputSystem.Key.W); yield return .12f; Keys(); yield return .1f;
        Require(EventSystem.current.currentSelectedGameObject == tabs[0].gameObject, "Actual W returns to Shared");
        Keys(UnityEngine.InputSystem.Key.DownArrow); yield return .12f; Keys(); yield return .1f;
        Require(EventSystem.current.currentSelectedGameObject == tabs[1].gameObject, "Arrow navigation matches W/S");
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        ExpectSound(SoundEventIds.UiClick, "Branch Space");
        var view = Get<PreparedEquipmentView[]>(panel, "equipmentSlots").First(v => v.definition != null && v.definition.DevelopmentResearchTier == 0);
        EventSystem.current.SetSelectedGameObject(view.button.gameObject); yield return .1f;
        int before = progress.ScrapParts; sounds.Clear();
        Keys(UnityEngine.InputSystem.Key.Space); yield return .15f;
        Require(panel.InspectedEquipment == view.definition && progress.ScrapParts == before, "Card Space only inspects and spends zero currency");
        yield return .5f; ExpectSound(SoundEventIds.TraitSelect, "Held card Space"); Keys(); yield return .1f;
        Capture("01-ko-equipment-card-focus");
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Enter); yield return .15f; Keys(); yield return .1f;
        ExpectSound(SoundEventIds.TraitSelect, "Enter card Submit");
        Require(progress.ScrapParts == before, "Enter card Submit spends zero currency");
        var gamepad = InputSystem.AddDevice<Gamepad>(); sounds.Clear();
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return .15f;
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return .1f;
        ExpectSound(SoundEventIds.TraitSelect, "Controller card Submit"); InputSystem.RemoveDevice(gamepad);
        var action = Get<Button>(panel, "equipmentActivationButton");
        EventSystem.current.SetSelectedGameObject(action.gameObject); yield return .1f;
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .5f; Keys(); yield return .1f;
        Require(progress.ScrapParts == before - view.definition.ManufacturingScrapCost && progress.IsEquipmentManufactured(view.definition.TraitId) &&
            !progress.IsEquipmentFitted(view.definition.TraitId), "Held Manufacture Space buys once without auto-fitting");
        ExpectSound(SoundEventIds.UiUnlock, "Manufacture Space"); Capture("02-ko-manufactured");
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        Require(progress.IsEquipmentFitted(view.definition.TraitId), "Equip Space fits"); ExpectSound(SoundEventIds.UiActivate, "Equip Space");
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        Require(!progress.IsEquipmentFitted(view.definition.TraitId), "Unequip Space unfits"); ExpectSound(SoundEventIds.UiDeactivate, "Unequip Space");
        var second = Get<PreparedEquipmentView[]>(panel, "equipmentSlots").First(v => v.definition != null && v.definition.DevelopmentResearchTier == 0 && v != view);
        data = progress.CreateSaveData(); data.scrapParts = data.coreShards = 0; progress.LoadFromSave(data);
        panel.InspectEquipment(second.definition); EventSystem.current.SetSelectedGameObject(action.gameObject); yield return .1f;
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        Require(!progress.IsEquipmentManufactured(second.definition.TraitId) && progress.ScrapParts == 0, "Insufficient transaction changes neither funds nor ownership");
        ExpectSound(SoundEventIds.UiInsufficient, "Insufficient Space"); Capture("03-ko-insufficient");
        sounds.Clear(); ExecuteEvents.Execute(second.button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        yield return .1f; ExpectSound(SoundEventIds.TraitSelect, "Mouse card click");
        EventSystem.current.SetSelectedGameObject(null); yield return .05f;
        var hover = second.button.GetComponent<UISoundButton>(); sounds.Clear();
        hover.OnPointerEnter(new PointerEventData(EventSystem.current)); EventSystem.current.SetSelectedGameObject(second.button.gameObject);
        yield return .1f; Require(sounds.Count(s => s == SoundEventIds.UiHover) == 1, "Pointer plus selection emits one Hover");
        sounds.Clear(); panel.RefreshPanel(); panel.RefreshPanel(); yield return .1f;
        Require(!sounds.Contains(SoundEventIds.UiHover), "Programmatic refresh does not replay Hover"); hover.OnPointerExit(new PointerEventData(EventSystem.current));
        panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
        var hidden = Get<PreparedEquipmentView[]>(panel, "equipmentSlots")[11].button;
        EventSystem.current.SetSelectedGameObject(hidden.gameObject); panel.ToggleResearchSpecialEquipment(); yield return .1f;
        Require(!hidden.gameObject.activeInHierarchy && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.activeInHierarchy,
            "Filtering and branch switching repair visible focus (hidden active=" + hidden.gameObject.activeInHierarchy + ", selection=" + EventSystem.current.currentSelectedGameObject?.name + ")");
        Language("en"); panel.RefreshPanel(); Capture("04-en-equipment-special");
        data = progress.CreateSaveData();
        foreach (var legacy in Get<PreparedEquipmentView[]>(panel, "equipmentCandidates"))
            if (legacy.definition != null && !legacy.definition.IsDevelopmentRoster && !legacy.definition.IsResearchSpecialEquipment &&
                legacy.definition.DevelopmentBranch == ShipTraitBranchKind.Shared && !data.manufacturedEquipmentIds.Contains(legacy.definition.TraitId))
                data.manufacturedEquipmentIds.Add(legacy.definition.TraitId);
        progress.LoadFromSave(data); panel.RefreshPanel();
        var legacyToggle = Get<Button>(panel, "clearEquipmentButton");
        Require(legacyToggle.gameObject.activeInHierarchy, "Owned legacy section toggle is reachable");
        EventSystem.current.SetSelectedGameObject(legacyToggle.gameObject); yield return .1f; sounds.Clear();
        Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        ExpectSound(SoundEventIds.UiClick, "Legacy section Space");
        var lastLegacy = Get<PreparedEquipmentView[]>(panel, "equipmentCandidates").Last(v => v.button.gameObject.activeInHierarchy);
        EventSystem.current.SetSelectedGameObject(lastLegacy.button.gameObject); yield return .1f;
        var legacyScroll = Get<ScrollRect>(panel, "equipmentLegacyScroll");
        var cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(legacyScroll.viewport, lastLegacy.button.transform);
        Require(cardBounds.min.y >= legacyScroll.viewport.rect.yMin - 1 && cardBounds.max.y <= legacyScroll.viewport.rect.yMax + 1,
            "Keyboard focus scrolls last legacy card into view");
        sounds.Clear(); Keys(UnityEngine.InputSystem.Key.Space); yield return .15f; Keys(); yield return .1f;
        ExpectSound(SoundEventIds.TraitSelect, "Legacy card Space"); Capture("04b-en-legacy-focus");
        EventSystem.current.SetSelectedGameObject(null); ui.enabled = false;

        var weapons = player.GetComponent<PlayerWeaponController>();
        foreach (WeaponTreeType type in new[] { WeaponTreeType.MachineGun, WeaponTreeType.Shotgun, WeaponTreeType.Sniper })
        {
            var ship = Get<ShipDefinition[]>(encounter, "ships").First(s => s.DefaultWeaponTree == type);
            // Each ship starts from an independent checkpoint, not the legacy-ownership UI fixture.
            data = (SaveData)typeof(DebugItemGrantUI).GetMethod("CreateCampaignCheckpoint", Static).Invoke(null, new object[] { new SaveData(), 7 });
            progress.LoadFromSave(data); progress.SetSelectedShipId(ship.ShipId);
            encounter.EnterDeck(); yield return .4f;
            Require(movement.HasTemporaryAimCamera(encounter), type + " owns deck aim camera");
            Require(weapons.CurrentWeaponTree == type, type + " applied from selected ship");
            var status = canvas.GetComponentInChildren<RouteCoreCombatHUD>(true);
            Require(Get<Delegate>(player, "Changed").GetInvocationList().Count(d => ReferenceEquals(d.Target, status)) == 1,
                type + " has exactly one HP subscription");
            var hp = Get<GaugeBarUI>(status, "healthGauge"); var armorGauge = Get<GaugeBarUI>(status, "armorGauge");
            var armor = player.GetComponent<PlayerArmor>(); armor.SetMaxArmor(10, true); armor.SetArmor(6);
            player.RestoreCurrentHp(player.MaxHp * .75f);
            Require(Mathf.Abs(hp.Ratio - .75f) < .001f && Mathf.Abs(armorGauge.Ratio - .6f) < .001f, type + " HP/Armor events update immediately");
            Move(new Vector2(-1.5f, -1.4f));
            var shipVisual = player.GetComponent<PlayerShipVisualController>();
            audit.AppendLine(type + " visible sprite=" + shipVisual.CurrentSprite?.name + " path=" + AssetDatabase.GetAssetPath(shipVisual.CurrentSprite));
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left, new Vector2(1,1).normalized })
            {
                Aim(direction); yield return .2f;
                Require(Vector2.Dot(movement.AimDirection, direction) > .999f, type + " mouse aim " + direction);
                float expected = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Get<float>(movement, "rotationOffset");
                Require(Mathf.Abs(Mathf.DeltaAngle(movement.AimVisualRoot.localEulerAngles.z, expected)) < 2, type + " visual faces mouse " + direction);
                Require(Mathf.Abs(player.transform.eulerAngles.z) < .01f && player.GetComponent<Rigidbody2D>().freezeRotation, "Physics root remains unrotated");
                Require(Vector2.Dot(((Vector2)weapons.FirePoint.position - (Vector2)player.transform.position).normalized, direction) > .99f, "Muzzle follows visual pivot once");
            }
            var heat = canvas.GetComponentInChildren<WeaponHeatUI>(true);
            var charge = canvas.GetComponentInChildren<PlayerChargeGaugeUI>(true);
            // The transient gameplay state permits real fire input without starting/changing the defense encounter.
            Set(GameStateManager.Instance, "currentState", GameState.SettlementDefense);
            bool sniperFired = false; Vector2 sniperDirection = Vector2.zero;
            bool firedAlongAim = false;
            Action<PlayerWeaponBase, float> recordDirection = (weapon, power) =>
                firedAlongAim |= Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b => b.Owner == ProjectileOwner.Player &&
                    Vector2.Dot(b.MoveDirection, movement.AimDirection) > .98f);
            weapons.CurrentWeapon.Fired += recordDirection;
            Action<SniperSuccessfulShotSnapshot> recordSniper = shot => { sniperFired = true; sniperDirection = shot.Projectile.Direction; };
            if (weapons.CurrentWeapon is SniperWeapon sniper) sniper.SuccessfulShotFired += recordSniper;
            Aim(Vector2.right, true); yield return type == WeaponTreeType.Sniper ? .65f : .3f;
            if (type != WeaponTreeType.Sniper)
                Require(firedAlongAim, type + " projectile direction matches AimDirection at the actual Fired event");
            if (type == WeaponTreeType.MachineGun)
            {
                var mg = (MachineGunWeapon)weapons.CurrentWeapon;
                Require(mg.CurrentHeat > 0 && heat.GetComponent<CanvasGroup>().alpha > .9f, "Real Machine Gun firing drives normal heat");
                Capture("05-sweeper-heat");
                double deadline = EditorApplication.timeSinceStartup + 30;
                while (!mg.IsOverheated && EditorApplication.timeSinceStartup < deadline) yield return .05f;
                Require(mg.IsOverheated, "Real Machine Gun firing reaches overheat (heat=" + mg.CurrentHeat + ")"); Capture("06-sweeper-overheat");
                Aim(Vector2.up); yield return .2f;
                Require(charge.GetComponent<CanvasGroup>().alpha > .9f, "Player-relative cooling recovery gauge active");
                Capture("07-sweeper-cooling");
                deadline = EditorApplication.timeSinceStartup + 10;
                while (mg.IsOverheated && EditorApplication.timeSinceStartup < deadline) yield return .1f;
                Require(!mg.IsOverheated, "Machine Gun cooling completes");
            }
            else if (type == WeaponTreeType.Sniper)
            {
                Require(weapons.CurrentWeapon.IsCharging && charge.GetComponent<CanvasGroup>().alpha > .9f, "Sniper charge uses existing events");
                Capture("09-lancer-charge"); Aim(Vector2.up, true); yield return .2f;
                Require(Vector2.Dot(movement.AimDirection, Vector2.up) > .999f, "Aim changes while charging");
                Aim(Vector2.up); yield return .2f;
                Require(!weapons.CurrentWeapon.IsCharging && charge.GetComponent<CanvasGroup>().alpha == 0, "Sniper release hides gauge");
                Require(sniperFired && Vector2.Dot(sniperDirection, Vector2.up) > .999f, "Sniper released projectile follows current AimDirection");
                yield return .3f; Aim(Vector2.left, true); yield return .35f;
                weapons.CurrentWeapon.ForceCancel(); Aim(Vector2.left); yield return .1f;
                Require(charge.GetComponent<CanvasGroup>().alpha == 0, "Sniper cancel hides gauge");
            }
            else { Capture("08-breacher-fire"); Aim(Vector2.up); }
            if (type != WeaponTreeType.MachineGun) Require(heat.GetComponent<CanvasGroup>().alpha == 0, type + " has no fake heat");
            if (weapons.CurrentWeapon is SniperWeapon recordedSniper) recordedSniper.SuccessfulShotFired -= recordSniper;
            weapons.CurrentWeapon.Fired -= recordDirection;
            Aim(Vector2.up); Keys(UnityEngine.InputSystem.Key.D); yield return .1f;
            var dash = player.GetComponent<PlayerDash>(); Set(dash, "lastDashTime", -999f);
            Vector2 position = player.transform.position; Require(dash.TryDash(), type + " dash accepted while facing another direction");
            yield return .25f; Keys(); yield return .05f;
            Require(player.transform.position.x > position.x && Vector2.Dot(movement.AimDirection, Vector2.right) < .5f, "Movement/dash independent from facing");
            Set(GameStateManager.Instance, "currentState", GameState.Settlement);
            encounter.LeaveDeck(); yield return .1f;
            Require(!movement.HasTemporaryAimCamera(encounter) && !canvas.gameObject.activeSelf, type + " exit releases aim and hides HUD");
            Require(!(Get<Delegate>(player, "Changed")?.GetInvocationList().Any(d => ReferenceEquals(d.Target, status)) ?? false),
                type + " exit removes HP subscription");
            Require(Get<PlayerWeaponController>(heat, "subscribedWeaponController") == null && Get<PlayerWeaponController>(charge, "subscribedWeaponController") == null,
                type + " exit unsubscribes weapon HUD");
        }
        encounter.EnterDeck(); yield return .3f; Aim(Vector2.left); yield return .2f;
        Require(movement.HasTemporaryAimCamera(encounter) && Vector2.Dot(movement.AimDirection, Vector2.left) > .999f, "Reentry restores aim and HUD");
        Capture("10-reentry");
        Require((progress.CurrentRouteCoreState == RouteCoreState.Assembled || progress.TryRestoreDamagedAccessKey()) &&
            progress.TryActivateRouteCore(), "Existing assembled checkpoint activates through progress authority");
        player.SetDashInvincible(true); Require(route.BeginSettlementDefense(), "Existing route starts defense lifecycle check"); yield return .3f;
        encounter.FailEncounter(); yield return .1f;
        Require(!movement.HasTemporaryAimCamera(encounter) && !canvas.gameObject.activeSelf, "Defense failure releases aim and hides combat HUD");
        encounter.EnterDeck(); player.SetDashInvincible(true);
        Require(route.BeginSettlementDefense(), "Existing defense retries after cleanup"); yield return 2.4f;
        for (int i = 0; i < 3; i++)
        {
            var core = encounter.Cores[i]; core.Health.TakeDamage(10000); yield return .1f;
            Move(core.transform.position + Vector3.down * .6f); core.Interact(player.gameObject); yield return 1.8f;
        }
        double purpleDeadline = EditorApplication.timeSinceStartup + 5;
        while (!encounter.IsPurpleActive && EditorApplication.timeSinceStartup < purpleDeadline) yield return .1f;
        Require(encounter.IsPurpleActive && !progress.SettlementDefenseCleared, "Third fusion requires Purple finale");
        var scanner = player.GetComponent<PlayerRadarScanner>(); scanner.TryToggleRadarMode(); scanner.TryQuickScan();
        Get<SettlementDefensePurpleCore>(encounter, "purpleCore").Health.TakeDamage(10000);
        double completionDeadline = EditorApplication.timeSinceStartup + 5;
        while (!progress.SettlementDefenseCleared && EditorApplication.timeSinceStartup < completionDeadline) yield return .1f;
        Require(progress.SettlementDefenseCleared && !canvas.gameObject.activeSelf && !movement.HasTemporaryAimCamera(encounter),
            "Existing Purple completion releases aim and hides combat HUD");
        encounter.EnterDeck(); yield return .2f; encounter.gameObject.SetActive(false); yield return .1f;
        Require(!canvas.gameObject.activeSelf && !movement.HasTemporaryAimCamera(encounter), "Encounter OnDisable cleans aim and HUD");
        Application.logMessageReceived -= SoundLog; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        audit.AppendLine("Real authored UI/Deck, virtual device input, isolated save. Combat patterns and production values unchanged.");
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
