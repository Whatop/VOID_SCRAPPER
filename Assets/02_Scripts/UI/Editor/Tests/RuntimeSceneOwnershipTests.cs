using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RuntimeSceneOwnershipTests
{
    [Test]
    public void TemporaryPreviewServices_WorkAlongsideAnUnsavedUntitledScene()
    {
        Scene user = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(user.path))
            Assert.Ignore("Run this environment regression with an unsaved Untitled scene open; the test must not replace the user's scene.");
        bool dirty = user.isDirty;
        int[] roots = AuthoredRuntimeFixture.RootIds(user);
        Scene preview = EditorSceneManager.NewPreviewScene();
        UnityEngine.EventSystems.EventSystem events = null;
        try
        {
            GameObject services = AuthoredRuntimeFixture.Create(preview, null, "UntitledIsolationServices", false);
            events = services.AddComponent<UnityEngine.EventSystems.EventSystem>();
            AuthoredRuntimeFixture.SelectEventSystem(events, preview);
            AuthoredRuntimeFixture.AssertScene(services, preview);
        }
        finally
        {
            try { AuthoredRuntimeFixture.ReleaseEventSystem(ref events, preview); }
            finally { AuthoredRuntimeFixture.Close(preview); }
        }
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(user.handle));
        Assert.That(user.isDirty, Is.EqualTo(dirty));
        Assert.That(AuthoredRuntimeFixture.RootIds(user), Is.EqualTo(roots));
    }

    [Test]
    public void DisposableSettlementGrouping_KeepsInactiveOwnersInTheLoadedScene()
    {
        Scene preview = AuthoredRuntimeFixture.Open("Settlement");
        Scene userScene = SceneManager.GetActiveScene();
        int[] userRoots = AuthoredRuntimeFixture.RootIds(userScene);
        try
        {
            SettlementHUD hud = AuthoredRuntimeFixture.Single<SettlementHUD>(preview);
            SettlementUIController controller = AuthoredRuntimeFixture.Single<SettlementUIController>(preview);
            RectTransform root = AuthoredRuntimeFixture.Group(preview);
            root.gameObject.SetActive(false);
            Assert.That(AuthoredRuntimeFixture.Single<SettlementHUD>(preview), Is.SameAs(hud));
            Assert.That(AuthoredRuntimeFixture.Single<SettlementUIController>(preview), Is.SameAs(controller));
            AuthoredRuntimeFixture.AssertScene(root.gameObject, preview);
            Assert.That(AuthoredRuntimeFixture.RootIds(userScene), Is.EqualTo(userRoots),
                AuthoredRuntimeFixture.RootsDescription(userScene));
        }
        finally { AuthoredRuntimeFixture.Close(preview); }
    }

    [Test]
    public void FixtureCreation_RejectsForeignParentBeforeAllocation_AndCloseDestroysOwnedObjects()
    {
        Scene first = EditorSceneManager.NewPreviewScene();
        Scene second = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject parent = AuthoredRuntimeFixture.Create(first, null, "FirstSceneParent");
            int[] before = AuthoredRuntimeFixture.RootIds(second);
            AssertionException error = Assert.Throws<AssertionException>(() =>
                AuthoredRuntimeFixture.Create(second, parent.transform, "MustNotBeCreated"));
            StringAssert.Contains("handle=" + first.handle, error.Message);
            StringAssert.Contains("handle=" + second.handle, error.Message);
            Assert.That(AuthoredRuntimeFixture.RootIds(second), Is.EqualTo(before));
            GameObject child = AuthoredRuntimeFixture.Create(first, parent.transform, "OwnedChild");
            parent.SetActive(false);
            AuthoredRuntimeFixture.Close(first);
            AuthoredRuntimeFixture.Close(first);
            Assert.That(parent == null && child == null, Is.True);
        }
        finally
        {
            try { AuthoredRuntimeFixture.Close(first); }
            finally { AuthoredRuntimeFixture.Close(second); }
        }
    }

    [TestCase("Tutorial")]
    [TestCase("Expedition")]
    public void ItemDetailRoot_RetainsItsBindingWithoutObsoleteMissingComponents(string sceneName)
    {
        Scene preview = AuthoredRuntimeFixture.Open(sceneName);
        try
        {
            InteractionPromptUI prompt = AuthoredRuntimeFixture.Single<InteractionPromptUI>(preview);
            GameObject detail = AuthoredRuntimeFixture.Read<GameObject>(prompt, "lootDetailRoot");
            Assert.That(detail, Is.Not.Null, sceneName + ": InteractionPromptUI.lootDetailRoot");
            Assert.That(detail.scene, Is.EqualTo(preview));
            Assert.That(UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(detail), Is.Zero,
                sceneName + ": " + detail.name);
        }
        finally
        {
            if (preview.IsValid()) AuthoredRuntimeFixture.Close(preview);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreviewEventSystemCleanup_IsIdempotentAndNaturallyRestoresRegisteredOwner(bool disableFirst)
    {
        var userEvents = UnityEngine.EventSystems.EventSystem.current;
        Scene preview = EditorSceneManager.NewPreviewScene();
        UnityEngine.EventSystems.EventSystem previous = null;
        UnityEngine.EventSystems.EventSystem owned = null;
        try
        {
            previous = AuthoredRuntimeFixture.Create(preview, null, "PreviousTestEvents", false)
                .AddComponent<UnityEngine.EventSystems.EventSystem>();
            owned = AuthoredRuntimeFixture.Create(preview, null, "OwnedTestEvents", false)
                .AddComponent<UnityEngine.EventSystems.EventSystem>();
            AuthoredRuntimeFixture.SelectEventSystem(previous, preview);
            AuthoredRuntimeFixture.SelectEventSystem(owned, preview);
            AuthoredRuntimeFixture.SelectEventSystem(owned, preview);
            if (disableFirst) owned.gameObject.SetActive(false);
            GameObject ownedRoot = owned.gameObject;
            AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, preview);
            AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, preview);
            Object.DestroyImmediate(ownedRoot);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(previous));
            AuthoredRuntimeFixture.ReleaseEventSystem(ref previous, preview);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(userEvents));
        }
        finally
        {
            AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, preview);
            AuthoredRuntimeFixture.ReleaseEventSystem(ref previous, preview);
            if (preview.IsValid()) AuthoredRuntimeFixture.Close(preview);
        }
    }

    [Test]
    public void PreviewEventSystemCleanup_PartialSetupAndDestroyedUnregisteredObjectAreSafe()
    {
        var userEvents = UnityEngine.EventSystems.EventSystem.current;
        UnityEngine.EventSystems.EventSystem owned = null;
        AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, default);
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            owned = AuthoredRuntimeFixture.Create(preview, null, "TestEvents", false)
                .AddComponent<UnityEngine.EventSystems.EventSystem>();
            AuthoredRuntimeFixture.SelectEventSystem(owned, preview);
            var destroyedReference = owned;
            GameObject root = owned.gameObject;
            AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, preview);
            Object.DestroyImmediate(root);
            AuthoredRuntimeFixture.ReleaseEventSystem(ref destroyedReference, preview);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(userEvents));
        }
        finally
        {
            AuthoredRuntimeFixture.ReleaseEventSystem(ref owned, preview);
            if (preview.IsValid()) AuthoredRuntimeFixture.Close(preview);
        }
    }

    [TestCase("Assets/01_Scenes", "*.unity")]
    [TestCase("Assets/03_Prefabs", "*.prefab")]
    [TestCase("Assets/02_Scripts/Config", "*.asset")]
    public void ProductionAssets_SourceMonoScriptReferencesResolve(string directory, string pattern)
    {
        // Read serialized references without opening scenes, importing a prefab
        // instance, invoking lifecycle methods, or changing the user's assets.
        // DLL/built-in script subassets use different file IDs and require Unity's
        // importer to resolve them; this check deliberately covers source scripts.
        var errors = new System.Collections.Generic.List<string>();
        var scriptReference = new System.Text.RegularExpressions.Regex(
            @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32}),");
        var recordHeader = new System.Text.RegularExpressions.Regex(@"^--- !u!\d+ &(-?\d+)");
        foreach (string path in System.IO.Directory.GetFiles(
                     directory, pattern, System.IO.SearchOption.AllDirectories))
        {
            string componentId = "unknown";
            int lineNumber = 0;
            foreach (string line in System.IO.File.ReadLines(path))
            {
                lineNumber++;
                var header = recordHeader.Match(line);
                if (header.Success)
                {
                    componentId = header.Groups[1].Value;
                }

                var reference = scriptReference.Match(line);
                if (!reference.Success)
                {
                    continue;
                }

                string guid = reference.Groups[1].Value;
                string scriptPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                UnityEditor.MonoScript script = string.IsNullOrEmpty(scriptPath)
                    ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(scriptPath);
                if (script == null || script.GetClass() == null)
                {
                    errors.Add($"{path}:{lineNumber} component={componentId} m_Script={guid} " +
                        $"resolvedPath='{scriptPath}'. Inspect Missing Script in Unity; do not rebuild UI.");
                }
            }
        }

        Assert.That(errors, Is.Empty, string.Join("\n", errors));
    }

    [TestCase(false, false, true, true, true)]
    [TestCase(true, true, true, true, true)]
    [TestCase(true, false, false, true, true)]
    [TestCase(true, false, true, false, true)]
    [TestCase(true, false, true, true, false)]
    public void GameBootstrap_TransientLifecycleClassificationIsExplicit(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool expected)
    {
        Assert.That(
            GameBootstrap.IsTransientLifecycleStateForEditorAndTests(
                isPlaying,
                isPreviewScene,
                sceneIsValid,
                sceneIsLoaded),
            Is.EqualTo(expected));
    }

    [TestCase(false, false, true, true, true, false, false)]
    [TestCase(true, true, true, true, true, false, false)]
    [TestCase(true, false, false, true, true, false, false)]
    [TestCase(true, false, true, false, true, false, false)]
    [TestCase(true, false, true, true, false, false, false)]
    [TestCase(true, false, true, true, true, true, false)]
    [TestCase(true, false, true, true, true, false, true)]
    public void GameBootstrap_PersistenceDecisionRequiresOneValidRuntimeRoot(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool isRoot,
        bool isAlreadyPersistent,
        bool expected)
    {
        Assert.That(
            GameBootstrap.ShouldRequestPersistenceForEditorAndTests(
                isPlaying,
                isPreviewScene,
                sceneIsValid,
                sceneIsLoaded,
                isRoot,
                isAlreadyPersistent),
            Is.EqualTo(expected));
    }

    [Test]
    public void RunResultPanelUI_EditorMigrationCreatesChildrenInParentsSceneBeforeParenting()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject parentObject = AuthoredRuntimeFixture.Create(previewScene, null, "ResultPanelParent");
            parentObject.SetActive(false);
            RectTransform parent = parentObject.GetComponent<RectTransform>();

            GameObject panel =
                RunResultPanelUI.CreateSceneOwnedAuthoredObjectForEditorAndTests(
                    "RuntimePanel",
                    parent,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            GameObject text =
                RunResultPanelUI.CreateSceneOwnedAuthoredObjectForEditorAndTests(
                    "RuntimeText",
                    parent,
                    typeof(RectTransform),
                    typeof(CanvasRenderer));

            Assert.That(panel.scene, Is.EqualTo(previewScene));
            Assert.That(text.scene, Is.EqualTo(previewScene));
            Assert.That(panel.transform.parent, Is.SameAs(parent));
            Assert.That(text.transform.parent, Is.SameAs(parent));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
        }
        finally
        {
            if (previewScene.IsValid() && previewScene.isLoaded)
            {
                AuthoredRuntimeFixture.Close(previewScene);
            }
        }
    }

    [Test]
    public void GameBootstrap_NonBootRuntimeSceneIsNotAnInitialAuthority()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene misplacedScene = EditorSceneManager.NewPreviewScene();
        try
        {
            Assert.That(
                GameBootstrap.IsAuthorizedBootSceneForEditorAndTests(misplacedScene),
                Is.False);
        }
        finally
        {
            if (misplacedScene.IsValid() && misplacedScene.isLoaded)
            {
                AuthoredRuntimeFixture.Close(misplacedScene);
            }

            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(previousScene.handle));
        }
    }

    [Test]
    public void GameBootstrap_OneShotAndRestorationGuardsCannotRequestPersistence()
    {
        Assert.That(GameBootstrap.ShouldRequestPersistenceForEditorAndTests(true, false, true, true, true, false, true, false), Is.False);
        Assert.That(GameBootstrap.ShouldRequestPersistenceForEditorAndTests(true, false, true, true, true, false, false, true), Is.False);
        string source = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts/Core/Bootstrap/GameBootstrap.cs"));
        int start = source.IndexOf("private void Start()", System.StringComparison.Ordinal);
        int end = source.IndexOf("private void CacheReferences()", start, System.StringComparison.Ordinal);
        Assert.That(source.Substring(start, end - start), Does.Not.Contain("TryInitializeRuntime"));
        Assert.That(source.IndexOf("persistenceRequestIssued = true;", System.StringComparison.Ordinal),
            Is.LessThan(source.IndexOf("DontDestroyOnLoad(gameObject);", System.StringComparison.Ordinal)));
    }

}


public sealed class PlayerChargeGaugePresentationTests
{
    private Scene scene;
    private Scene activeScene;
    private bool activeDirty;
    private int[] activeRoots;
    private Camera camera;
    private Canvas canvas;
    private RectTransform root;
    private RectTransform wrapper;
    private Transform player;
    private WorldGaugeFollower follower;
    private PlayerChargeGaugeUI presenter;
    private GaugeBarUI gauge;
    private Image fill;
    private Slider slider;
    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private static readonly Vector3 Offset = new Vector3(0, 1.1f, 0);

    [SetUp]
    public void SetUp()
    {
        activeScene = SceneManager.GetActiveScene(); activeDirty = activeScene.isDirty;
        activeRoots = AuthoredRuntimeFixture.RootIds(activeScene);
        scene = EditorSceneManager.NewPreviewScene();
        camera = AuthoredRuntimeFixture.Create(scene, null, "TestCamera", false).AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 4.21875f;
        camera.pixelRect = new Rect(0, 0, 480, 270);
        camera.transform.position = new Vector3(.013f, -.027f, -10);
        canvas = AuthoredRuntimeFixture.Create(scene, null, "TestWorldCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        canvasRect.sizeDelta = new Vector2(480, 270);
        canvasRect.position = new Vector3(240, 135, 0);
        wrapper = (RectTransform)AuthoredRuntimeFixture.Create(scene, canvas.transform, "AuthoredWrapper").transform;
        wrapper.anchoredPosition = new Vector2(19, -13); wrapper.localScale = new Vector3(.8f, 1.1f, 1);
        root = (RectTransform)AuthoredRuntimeFixture.Create(scene, wrapper, "Charge").transform;
        root.anchorMin = root.anchorMax = new Vector2(.3f, .7f);
        root.pivot = new Vector2(.2f, .8f); root.sizeDelta = new Vector2(54, 6);
        root.localScale = new Vector3(.8f, .8f, 1);
        root.gameObject.AddComponent<Image>();
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        RectTransform track = (RectTransform)AuthoredRuntimeFixture.Create(scene, root, "Track").transform;
        fill = AuthoredRuntimeFixture.Create(scene, track, "Fill").AddComponent<Image>();
        fill.type = Image.Type.Filled;
        slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform;
        slider.handleRect = null; slider.interactable = false;
        gauge = root.gameObject.AddComponent<GaugeBarUI>();
        Bind(gauge, "slider", slider); Bind(gauge, "fillImage", fill); Bind(gauge, "canvasGroup", group);
        follower = root.gameObject.AddComponent<WorldGaugeFollower>();
        player = AuthoredRuntimeFixture.Create(scene, null, "TestPlayer", false).transform;
        follower.ConfigureRuntime(player, Offset, canvas, camera);
        presenter = root.gameObject.AddComponent<PlayerChargeGaugeUI>();
        Bind(presenter, "chargeGauge", gauge); Bind(presenter, "follower", follower); Bind(presenter, "canvasGroup", group);
        canvas.gameObject.SetActive(true); wrapper.gameObject.SetActive(true); root.gameObject.SetActive(true);
        follower.SetVisible(true);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (follower != null) Call(follower, "OnDisable");
        }
        finally
        {
            try
            {
                if (presenter != null) Call(presenter, "OnDisable");
            }
            finally
            {
                if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene);
                scene = default;
            }
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
        Assert.That(activeScene.isDirty, Is.EqualTo(activeDirty));
        Assert.That(AuthoredRuntimeFixture.RootIds(activeScene), Is.EqualTo(activeRoots), AuthoredRuntimeFixture.RootsDescription(activeScene));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Follow_UsesActualProjectionAndImmediateParentWithoutDoubleSnapping(bool movePlayer, bool moveCamera)
    {
        object[] authored = AuthoredGeometry();
        Vector2 anchors = root.anchorMin, pivot = root.pivot, size = root.sizeDelta;
        Vector3 scale = root.localScale;
        for (int i = 0; i < 30; i++)
        {
            if (movePlayer) player.position += new Vector3(.007f, .013f, 0);
            if (moveCamera) camera.transform.position += new Vector3(.007f, .013f, 0);
            Vector3 screen = camera.WorldToScreenPoint(player.position + Offset);
            screen.x = Mathf.Round(screen.x); screen.y = Mathf.Round(screen.y);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(wrapper, screen, null, out Vector2 expected), Is.True);
            Call(follower, "Follow");
            Assert.That(Vector2.Distance(root.localPosition, expected), Is.LessThan(.0001f));
            Vector3 position = root.localPosition;
            for (int repeat = 0; repeat < 4; repeat++) Call(follower, "Follow");
            Assert.That(root.localPosition, Is.EqualTo(position));
            Assert.That(AuthoredGeometry(), Is.EqualTo(authored), "Following owns position, not authored geometry or world offset.");
        }
        Assert.That(root.anchorMin, Is.EqualTo(anchors));
        Assert.That(root.anchorMax, Is.EqualTo(anchors));
        Assert.That(root.pivot, Is.EqualTo(pivot));
        Assert.That(root.sizeDelta, Is.EqualTo(size));
        Assert.That(root.localScale, Is.EqualTo(scale));
    }

    [TestCase(2.5f)]
    [TestCase(4.21875f)]
    [TestCase(7f)]
    public void ZoomAndActualPixelCameraMatrix_AreUsedWithoutInventingAnotherCamera(float zoom)
    {
        camera.orthographicSize = zoom;
        // Model an actual camera render matrix override, not an invented UI-only camera position.
        Matrix4x4 matrix = camera.worldToCameraMatrix;
        matrix.m03 += .019f; matrix.m13 -= .008f;
        camera.worldToCameraMatrix = matrix;
        Vector3 world = new Vector3(.17f, .21f, 0);
        Vector3 expected = camera.WorldToScreenPoint(world);
        expected.x = Mathf.Round(expected.x); expected.y = Mathf.Round(expected.y);
        for (int i = 0; i < 8; i++)
            Assert.That((Vector3)Call(follower, "ProjectWorldToScreen", world), Is.EqualTo(expected));
        camera.ResetWorldToCameraMatrix();
    }

    [Test]
    public void RenderCallbacks_OnePositionWriterPerCameraFrame_ReleaseOnDisableAndDestroy()
    {
        object[] authored = AuthoredGeometry();
        Call(follower, "OnEnable"); Call(follower, "OnEnable");
        Assert.That(CallbackCount(Camera.onPreRender, follower), Is.EqualTo(1));
        Call(follower, "PresentForCamera", camera);
        Vector3 first = root.localPosition;
        player.position += Vector3.right;
        Call(follower, "PresentForCamera", camera);
        Assert.That(root.localPosition, Is.EqualTo(first), "Repeated render callbacks cannot write twice in one frame.");
        Call(follower, "OnDisable"); Call(follower, "OnDestroy");
        Assert.That(CallbackCount(Camera.onPreRender, follower), Is.Zero);
        Assert.That(typeof(WorldGaugeFollower).GetMethod("Update", Private), Is.Null);
        Assert.That(typeof(WorldGaugeFollower).GetMethod("LateUpdate", Private), Is.Null);
        Assert.That(typeof(WorldGaugeFollower).GetMethod("FixedUpdate", Private), Is.Null);
        Call(follower, "OnEnable"); Call(follower, "PresentForCamera", camera);
        Assert.That(root.localPosition, Is.Not.EqualTo(first), "Re-enable resets the per-frame guard.");
        Assert.That(AuthoredGeometry(), Is.EqualTo(authored));
    }

    [Test]
    public void FillAndChargeLifecycle_NeverMoveOrResizeRootOrTrack()
    {
        // Charge presentation does not own camera projection. Prevent an Editor
        // render callback from legitimately moving the root during these assertions.
        follower.enabled = false;
        Call(follower, "OnDisable");
        object[] authored = AuthoredGeometry();
        string rootBefore = UnityEditor.EditorJsonUtility.ToJson(root);
        string trackBefore = UnityEditor.EditorJsonUtility.ToJson(fill.transform.parent);
        int sliderActions = 0; slider.onValueChanged.AddListener(_ => sliderActions++);
        foreach (float ratio in new[] { 0f, .35f, 1f, .2f, 0f })
        {
            presenter.ShowRatio(ratio);
            Assert.That(gauge.Ratio, Is.EqualTo(ratio));
            Assert.That(fill.fillAmount, Is.EqualTo(ratio));
            Assert.That(UnityEditor.EditorJsonUtility.ToJson(root), Is.EqualTo(rootBefore));
            Assert.That(UnityEditor.EditorJsonUtility.ToJson(fill.transform.parent), Is.EqualTo(trackBefore));
            Assert.That(AuthoredGeometry(), Is.EqualTo(authored));
        }
        presenter.Hide();
        Assert.That(gauge.Ratio, Is.Zero);
        Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(sliderActions, Is.Zero);
        Assert.That(UnityEditor.EditorJsonUtility.ToJson(root), Is.EqualTo(rootBefore));
    }

    [Test]
    public void ChargePublisher_CancelCompleteAndCleanupUseActualSubscribedSources()
    {
        follower.enabled = false;
        Call(follower, "OnDisable");
        object[] authored = AuthoredGeometry();
        SniperWeapon weapon = AuthoredRuntimeFixture.Create(scene, null, "TestSniper", false).AddComponent<SniperWeapon>();
        PlayerWeaponController publisher = AuthoredRuntimeFixture.Create(scene, null, "TestWeapons", false).AddComponent<PlayerWeaponController>();
        Bind(presenter, "weaponController", publisher);
        Set(presenter, "weaponSources", new PlayerWeaponBase[] { weapon, weapon });
        Call(presenter, "SubscribeWeapons"); Call(presenter, "SubscribeWeapons");
        Call(presenter, "SubscribeWeaponController"); Call(presenter, "SubscribeWeaponController");
        foreach (string name in new[] { "ChargeStarted", "ChargeChanged", "ChargeCanceled", "ChargeReleased" })
            Assert.That(CallbackCount(typeof(PlayerWeaponBase).GetField(name, Private).GetValue(weapon) as System.Delegate, presenter), Is.EqualTo(1));
        foreach (bool cancelled in new[] { true, false })
        {
            Notify(weapon, "NotifyChargeStarted");
            Notify(weapon, "NotifyChargeChanged", .6f);
            Assert.That(gauge.Ratio, Is.EqualTo(.6f));
            if (cancelled) Notify(weapon, "NotifyChargeCanceled");
            else Notify(weapon, "NotifyChargeReleased", 1f);
            Assert.That(gauge.Ratio, Is.Zero);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(AuthoredGeometry(), Is.EqualTo(authored));
        }
        Bind(presenter, "weaponController", null);
        Set(presenter, "weaponSources", System.Array.Empty<PlayerWeaponBase>());
        Call(presenter, "OnDisable"); Call(presenter, "OnDestroy");
        Assert.That(CallbackCount(typeof(PlayerWeaponController).GetField("WeaponEquipped", Private).GetValue(publisher) as System.Delegate, presenter), Is.Zero);
        foreach (string name in new[] { "ChargeStarted", "ChargeChanged", "ChargeCanceled", "ChargeReleased" })
            Assert.That(CallbackCount(typeof(PlayerWeaponBase).GetField(name, Private).GetValue(weapon) as System.Delegate, presenter), Is.Zero);
    }

    [TestCase("Tutorial")]
    [TestCase("Expedition")]
    public void ProductionHud_UsesSharedChargePathAndRetainsAuthoredStatusBindings(string sceneName)
    {
        Scene preview = AuthoredRuntimeFixture.Open(sceneName);
        try
        {
            ExpeditionHUD hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(preview);
            foreach (string field in new[] { "hpGauge", "armorFillRect", "cargoGauge", "cargoCanvasGroup",
                "weaponHeatUI", "reinforcementSlotUI", "resourceRoot", "operationRoot", "menuHintRoot" })
                Assert.That(AuthoredRuntimeFixture.Read<UnityEngine.Object>(hud, field), Is.Not.Null, sceneName + "." + field);
            PlayerChargeGaugeUI[] gauges = AuthoredRuntimeFixture.Find<PlayerChargeGaugeUI>(preview);
            Assert.That(gauges.Length, Is.LessThanOrEqualTo(1));
            foreach (PlayerChargeGaugeUI charge in gauges)
            {
                WorldGaugeFollower source = AuthoredRuntimeFixture.Read<WorldGaugeFollower>(charge, "follower");
                Assert.That(source, Is.Not.Null);
                Assert.That(source.gameObject, Is.SameAs(charge.gameObject));
                Assert.That(UnityEditor.MonoScript.FromMonoBehaviour(source).GetClass(), Is.EqualTo(typeof(WorldGaugeFollower)));
                GaugeBarUI bar = AuthoredRuntimeFixture.Read<GaugeBarUI>(charge, "chargeGauge");
                Slider display = AuthoredRuntimeFixture.Read<Slider>(bar, "slider");
                Assert.That(display.fillRect, Is.Not.SameAs(charge.transform));
                Assert.That(display.fillRect.IsChildOf(charge.transform), Is.True);
            }
            string code = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts/UI/ExpeditionHUD.cs"));
            Assert.That(code, Does.Contain("root.AddComponent<WorldGaugeFollower>()"));
            Assert.That(code, Does.Contain("root.AddComponent<PlayerChargeGaugeUI>()"));
        }
        finally { AuthoredRuntimeFixture.Close(preview); }
    }

    [Test]
    public void NoCompletedUiMenuCommandsOrAuthoringFactoryRemain()
    {
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(Application.dataPath, "02_Scripts"), "*.cs", System.IO.SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Editor/Tests/")) continue;
            string source = System.IO.File.ReadAllText(file);
            Assert.That(source, Does.Not.Contain("MenuItem(\"VOID SCRAPPER/UI/"), file);
            Assert.That(source, Does.Not.Contain("BootMainMenuAuthoringObjectFactory"), file);
            Assert.That(source, Does.Not.Contain("ConfigureForAuthoring("), file);
        }
    }

    [TestCase("Boot")]
    [TestCase("Settlement")]
    public void SavedFixedUiBindings_ValidateWithoutChangingPresentationOrProgress(string sceneName)
    {
        Scene preview = AuthoredRuntimeFixture.Open(sceneName);
        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saves = SaveManager.Instance;
        try
        {
            Transform[] transforms = AuthoredRuntimeFixture.Find<Transform>(preview);
            string[] before = System.Array.ConvertAll(transforms, t => UnityEditor.EditorJsonUtility.ToJson(t));
            var errors = new System.Collections.Generic.List<string>();
            if (sceneName == "Boot")
            {
                BootMainMenuView view = AuthoredRuntimeFixture.Single<BootMainMenuView>(preview);
                Assert.That(view.TryGetMissingRuntimeReference(out string missing), Is.True, missing);
                view.SharedOptions.CollectAuthoredBindingErrors(errors);
                SettingsMenuTabController tabs = view.SharedOptions.GetComponent<SettingsMenuTabController>();
                Assert.That(tabs, Is.Not.Null, AuthoredRuntimeFixture.Describe(view.SharedOptions.gameObject));
                var tabData = new UnityEditor.SerializedObject(tabs);
                foreach (string field in new[] { "rebindRows", "dropdowns" })
                {
                    UnityEditor.SerializedProperty bindings = tabData.FindProperty(field);
                    Component[] controls = field == "rebindRows"
                        ? tabs.GetComponentsInChildren<InputRebindButtonUI>(true)
                        : (Component[])tabs.GetComponentsInChildren<TMPro.TMP_Dropdown>(true);
                    TestContext.WriteLine($"{AuthoredRuntimeFixture.Describe(tabs.gameObject)}.{field}: " +
                        $"saved bindings={bindings.arraySize}, authored controls={controls.Length}");
                    foreach (Component control in controls)
                    {
                        AuthoredRuntimeFixture.AssertScene(control.gameObject, preview);
                        bool assigned = false;
                        for (int i = 0; i < bindings.arraySize; i++)
                            assigned |= bindings.GetArrayElementAtIndex(i).objectReferenceValue == control;
                        if (!assigned) errors.Add($"SettingsMenuTabController.{field}: assign the existing " +
                            $"{control.GetType().Name} at {AuthoredRuntimeFixture.Describe(control.gameObject)} " +
                            $"to {AuthoredRuntimeFixture.Describe(tabs.gameObject)} in the Inspector; no runtime reconstruction.");
                    }
                }
                Assert.That(UnityEditor.MonoScript.FromMonoBehaviour(view).GetClass(), Is.EqualTo(typeof(BootMainMenuView)));
            }
            else
            {
                SettlementHUD hud = AuthoredRuntimeFixture.Single<SettlementHUD>(preview);
                SettlementUIController navigation = AuthoredRuntimeFixture.Single<SettlementUIController>(preview);
                hud.ValidateHangarPresentation(errors);
                foreach (CurrencyType resource in AuthoredRuntimeFixture.PermanentResources)
                    Assert.That(hud.HasValidResourceCell(resource), Is.True, resource.ToString());
                hud.CollectRestorationBindingErrors(
                    AuthoredRuntimeFixture.Read<GameObject>(navigation, "repairPanel"),
                    AuthoredRuntimeFixture.Read<Button>(navigation, "repairActionButton"),
                    AuthoredRuntimeFixture.Read<Button>(navigation, "repairPreviousButton"),
                    AuthoredRuntimeFixture.Read<Button>(navigation, "repairNextButton"), errors);
                AuthoredRuntimeFixture.Single<ShipTraitTreePanel>(preview).CollectAdditionalTraitsBindingErrors(errors);
                AuthoredRuntimeFixture.Single<SettlementSectorTechnologyPanelUI>(preview).CollectPresentationErrors(errors);
                Assert.That(UnityEditor.MonoScript.FromMonoBehaviour(hud).GetClass(), Is.EqualTo(typeof(SettlementHUD)));
            }
            Assert.That(System.Array.ConvertAll(transforms, t => UnityEditor.EditorJsonUtility.ToJson(t)), Is.EqualTo(before));
            Assert.That(PermanentProgress.Instance, Is.SameAs(progress));
            Assert.That(SaveManager.Instance, Is.SameAs(saves));
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }
        finally { AuthoredRuntimeFixture.Close(preview); }
    }

    private object[] AuthoredGeometry() => new object[]
    {
        root.anchorMin, root.anchorMax, root.pivot, root.localScale, root.localRotation, root.sizeDelta,
        UnityEditor.EditorJsonUtility.ToJson(fill.transform.parent),
        typeof(WorldGaugeFollower).GetField("worldOffset", Private).GetValue(follower)
    };

    private static int CallbackCount(System.Delegate callbacks, object target)
    {
        int count = 0;
        if (callbacks != null) foreach (System.Delegate entry in callbacks.GetInvocationList())
            if (entry.Target == target) count++;
        return count;
    }
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Notify(PlayerWeaponBase weapon, string method, params object[] args) =>
        typeof(PlayerWeaponBase).GetMethod(method, Private).Invoke(weapon, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new UnityEditor.SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

internal static class AuthoredRuntimeFixture
{
    internal static void SelectEventSystem(UnityEngine.EventSystems.EventSystem owned, Scene scene)
    {
        Assert.That(owned != null && owned.isActiveAndEnabled, Is.True,
            owned != null ? Describe(owned.gameObject) : "Null fixture EventSystem; " + Describe(scene));
        Assert.That(scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene), Is.True, Describe(scene));
        AssertScene(owned.gameObject, scene);
        // PreviewScene gameplay callbacks are not automatic. Balance the exact
        // test-owned instance's public lifecycle contract before selecting it;
        // never inspect or mutate uGUI's private registration list.
        EventSystemLifecycle(owned, "OnDisable");
        EventSystemLifecycle(owned, "OnEnable");
        UnityEngine.EventSystems.EventSystem.current = owned;
    }

    private static void EventSystemLifecycle(UnityEngine.EventSystems.EventSystem owned, string method)
    {
        typeof(UnityEngine.EventSystems.EventSystem).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(owned, null);
    }

    internal static void ReleaseEventSystem(ref UnityEngine.EventSystems.EventSystem owned, Scene scene)
    {
        if (owned == null)
        {
            owned = null;
            return;
        }

        // Only the disposable fixture is ours. In this uGUI version, even assigning
        // null to EventSystem.current is an error: the setter accepts registered
        // instances only. OnDisable unregisters this instance and naturally exposes
        // the previous registered system, without reviving or touching user objects.
        Assert.That(scene.IsValid() && EditorSceneManager.IsPreviewScene(scene), Is.True, Describe(scene));
        AssertScene(owned.gameObject, scene);
        if (UnityEngine.EventSystems.EventSystem.current == owned)
        {
            owned.SetSelectedGameObject(null);
        }
        owned.enabled = false;
        EventSystemLifecycle(owned, "OnDisable");
        owned = null;
    }

    // Read-only production source -> disposable supported PreviewScene copy.
    // No installer, runtime lifecycle, save, preference or gameplay API is called.
    internal static Scene Open(string sceneName)
    {
        string path = "Assets/01_Scenes/" + sceneName + ".unity";
        Scene scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            Assert.That(scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene),
                Is.True, Describe(scene));
            Assert.That(scene.path, Is.EqualTo(path), Describe(scene));
            return scene;
        }
        catch
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            throw;
        }
    }

    internal static string Describe(Scene scene) =>
        $"scene name='{scene.name}', path='{scene.path}', handle={scene.handle}, " +
        $"valid={scene.IsValid()}, loaded={scene.isLoaded}, preview={EditorSceneManager.IsPreviewScene(scene)}";

    internal static string Describe(GameObject item)
    {
        string path = item.name;
        for (Transform parent = item.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return $"GameObject '{path}', id={item.GetInstanceID()}, {Describe(item.scene)}";
    }

    internal static void AssertScene(GameObject item, Scene expected)
    {
        Assert.That(item.scene.handle, Is.EqualTo(expected.handle),
            $"Actual {Describe(item)}; expected {Describe(expected)}");
    }

    internal static int[] RootIds(Scene scene)
    {
        int[] ids = System.Array.ConvertAll(scene.GetRootGameObjects(), item => item.GetInstanceID());
        System.Array.Sort(ids);
        return ids;
    }

    internal static string RootsDescription(Scene scene) =>
        string.Join("\n", System.Array.ConvertAll(scene.GetRootGameObjects(), Describe));

    internal static void Close(Scene scene)
    {
        if (!scene.IsValid()) return;
        Assert.That(EditorSceneManager.IsPreviewScene(scene), Is.True, Describe(scene));
        // Retain exact references for leak assertions, not global Editor snapshots.
        Transform[] owned = Find<Transform>(scene);
        string[] descriptions = System.Array.ConvertAll(owned, t => Describe(t.gameObject));
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != null) Object.DestroyImmediate(root);
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
        for (int i = 0; i < owned.Length; i++)
            Assert.That(owned[i] == null, Is.True, "Leaked fixture object: " + descriptions[i]);
    }

    internal static T[] Find<T>(Scene scene) where T : Component
    {
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True, Describe(scene));
        var found = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));
        return found.ToArray();
    }

    internal static T Single<T>(Scene scene) where T : Component
    {
        T[] found = Find<T>(scene);
        Assert.That(found.Length, Is.EqualTo(1),
            $"Expected one {typeof(T).Name} in {Describe(scene)}. Roots:\n{RootsDescription(scene)}\n" +
            string.Join("\n", System.Array.ConvertAll(found, item => Describe(item.gameObject))));
        return found[0];
    }

    internal static T Read<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object =>
        new UnityEditor.SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

    internal static GameObject Create(Scene scene, Transform parent, string name, bool rect = true)
    {
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True, Describe(scene));
        if (parent != null) AssertScene(parent.gameObject, scene);
        // ObjectFactory applies SceneView's default parent AFTER targeted creation.
        // Use a plain, component-free root and explicitly move it before adding any
        // runtime owner or parenting authored fixture roots. Never move user objects.
        GameObject result = new GameObject(name,
            rect ? new[] { typeof(RectTransform) } : System.Array.Empty<System.Type>());
        try
        {
            result.SetActive(false);
            SceneManager.MoveGameObjectToScene(result, scene);
            AssertScene(result, scene);
            if (parent != null) result.transform.SetParent(parent, false);
            result.SetActive(true);
            return result;
        }
        catch
        {
            Object.DestroyImmediate(result);
            throw;
        }
    }

    internal static RectTransform Group(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        RectTransform group = (RectTransform)Create(scene, null, "TestOwnedRuntimeFixture").transform;
        AssertScene(group.gameObject, scene);
        foreach (GameObject root in roots)
        {
            AssertScene(root, scene);
            root.transform.SetParent(group, true);
        }
        return group;
    }

    internal static readonly string[] NavigationFields = { "hangarNavigationButton", "openRepairPanelButton",
        "sectorTechnologyNavigationButton", "openTraitPanelButton", "openDialogueArchiveButton", "openSettingsPanelButton" };
    internal static readonly string[] NavigationViews = { "hangarNavigationView", "repairNavigationView",
        "sectorTechnologyNavigationView", "traitNavigationView", "archiveNavigationView", "settingsNavigationView" };
    internal static readonly string[] NavigationActions = { "ShowMainPanel", "ShowRepairPanel",
        "ShowSectorTechnologyPanel", "ShowTraitPanel", "ShowDialogueArchivePanel", "ShowSettingsPanel" };
    internal static readonly string[] RunResources = { "creditsCounter", "scrapCounter", "coreShardCounter", "stabilizedAlloyCounter", "tuningChipCounter" };
    internal static readonly CurrencyType[] PermanentResources = { CurrencyType.ScrapParts, CurrencyType.CoreShards, CurrencyType.StabilizedAlloy };
}
