using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TutorialGuidanceUIAuthoringTests
{
    private Scene scene;
    private Scene previousScene;
    private bool previousDirty;
    private int[] previousRoots;
    private EventSystem previousEventSystem;
    private TutorialFlowController flow;
    private ExpeditionHUD hud;
    private bool ownsTweenRuntime;
    private Scene tweenSetupScene;
    private AudioManager previousAudio;
    private bool manualTweenScope;
    private bool wasTweenInitialized;
    private static readonly FieldInfo TweenInitialized = typeof(DOTween).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic);
    private readonly List<Object> assets = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        previousDirty = previousScene.isDirty;
        previousRoots = RootIds(previousScene);
        previousEventSystem = EventSystem.current;
        // NewPreviewScene suppresses MonoBehaviour lifecycle callbacks through Unity's
        // supported preview lifecycle. No fabricated component arrays or scene flags.
        scene = AuthoredRuntimeFixture.Open("Tutorial");
        flow = AuthoredRuntimeFixture.Single<TutorialFlowController>(scene);
        hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(scene);
        previousAudio = AudioManager.Instance;
        GameObject audioRoot = Create("RadarTestAudio", null, false);
        AudioManager audio = audioRoot.AddComponent<AudioManager>();
        AudioEventDatabase database = ScriptableObject.CreateInstance<AudioEventDatabase>();
        assets.Add(database);
        SetReference(audio, "database", database);
        Field(audio, "logMissingEvents", false);
        typeof(AudioManager).GetProperty("Instance").SetValue(null, audio);
    }

    [TearDown]
    public void TearDown()
    {
        typeof(AudioManager).GetProperty("Instance").SetValue(null, previousAudio);
        if (flow != null) Call(flow, "StopAlienSignalIdleMotion");
        if (hud != null) hud.CompleteCinematicVisibilityTransition();
        foreach (RadarPanelAnimator panel in AuthoredRuntimeFixture.Find<RadarPanelAnimator>(scene))
            Call(panel, "StopOwnedTweens");
        try
        {
            if (hud != null) hud.HideObjectiveBriefing();
        }
        finally
        {
            try
            {
                if (ownsTweenRuntime) DOTween.Clear(true);
            }
            finally
            {
                ownsTweenRuntime = false;
                if (manualTweenScope) TweenInitialized.SetValue(null, wasTweenInitialized);
                manualTweenScope = false;
                try
                {
                    if (tweenSetupScene.IsValid()) AuthoredRuntimeFixture.Close(tweenSetupScene);
                }
                finally
                {
                    tweenSetupScene = default;
                    try { AuthoredRuntimeFixture.Close(scene); }
                    finally
                    {
                        scene = default;
                        foreach (Object asset in assets)
                            if (asset != null) Object.DestroyImmediate(asset);
                        assets.Clear();
                    }
                }
            }
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
        Assert.That(RootIds(previousScene), Is.EqualTo(previousRoots));
        Assert.That(previousScene.isDirty, Is.EqualTo(previousDirty));
        Assert.That(EventSystem.current, Is.SameAs(previousEventSystem));
    }

    [Test]
    public void Prompt_PreservesAuthoredAppearanceAndInstructionProgressBehavior()
    {
        UseAuthoredFixture();
        TutorialPromptUI prompt = Read<TutorialPromptUI>(flow, "promptUI");
        Image background = prompt.GetComponent<Image>();
        TextMeshProUGUI instruction = Read<TextMeshProUGUI>(prompt, "instructionText");
        TextMeshProUGUI progress = Read<TextMeshProUGUI>(prompt, "progressText");
        instruction.fontSize = 16f;
        instruction.color = new Color(0.4f, 0.6f, 0.8f, 0.45f);
        instruction.alignment = TextAlignmentOptions.BottomLeft;
        background.color = new Color(0.1f, 0.2f, 0.3f, 0.5f);
        ((RectTransform)prompt.transform).anchoredPosition = new Vector2(11f, -19f);
        string before = EditorJsonUtility.ToJson(instruction);
        string beforeBackground = EditorJsonUtility.ToJson(background);
        UseAuthoredFixture();
        Assert.That(EditorJsonUtility.ToJson(instruction), Is.EqualTo(before));
        Assert.That(EditorJsonUtility.ToJson(background), Is.EqualTo(beforeBackground));
        Assert.That(((RectTransform)prompt.transform).anchoredPosition, Is.EqualTo(new Vector2(11f, -19f)));
        prompt.ShowInstruction("Instruction", null, null, null, false);
        prompt.SetProgress(3, 28);
        Assert.That(instruction.text, Is.EqualTo("Instruction"));
        Assert.That(progress.text, Is.EqualTo("3/28"));
        Assert.That(prompt.gameObject.activeSelf, Is.True);
        prompt.Hide();
        Assert.That(prompt.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void AuthoredBriefing_CreatesNoGameObjectAndUsesExistingTextAndTween()
    {
        UseAuthoredFixture();
        // Preview scenes suppress unrelated HUD Awake/OnEnable; public briefing still
        // exercises its real active/enabled guard, binding predicate, and DOTween path.
        hud.gameObject.SetActive(true);
        PrepareTweenRuntime();
        int before = AuthoredRuntimeFixture.Find<Transform>(scene).Length;
        hud.ShowObjectiveBriefing("Objective", "Details", Color.cyan);
        Assert.That(Read<GameObject>(hud, "operationRoot").activeSelf, Is.True);
        Assert.That(Read<TextMeshProUGUI>(hud, "operationTitleText").text, Does.EndWith("Objective"));
        Assert.That(Read<TextMeshProUGUI>(hud, "operationDetailText").text, Is.EqualTo("Details"));
        Assert.That(AuthoredRuntimeFixture.Find<Transform>(scene).Length, Is.EqualTo(before));
        hud.HideObjectiveBriefing();
        Assert.That(Read<GameObject>(hud, "operationRoot").activeSelf, Is.False);
    }

    [TestCase("operationRoot")]
    [TestCase("operationTitleText")]
    [TestCase("operationDetailText")]
    [TestCase("operationAccentImage")]
    [TestCase("operationCanvasGroup")]
    public void MissingAuthoredBinding_SkipsVisualAndWarnsOnlyOnceWithoutConstruction(string field)
    {
        UseAuthoredFixture();
        SetReference(hud, field, null);
        hud.gameObject.SetActive(true);
        int before = AuthoredRuntimeFixture.Find<Transform>(scene).Length;
        LogAssert.Expect(LogType.Warning, new Regex("Authored operation briefing bindings are missing or invalid"));
        hud.ShowObjectiveBriefing("Objective", "Details", Color.white);
        hud.ShowObjectiveBriefing("Again", "Details", Color.white);
        Assert.That(AuthoredRuntimeFixture.Find<Transform>(scene).Length, Is.EqualTo(before));
        Assert.That(hud.enabled, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void OperationRequiresAuthoredPresentation()
    {
        SetReference(hud, "operationRoot", null);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Authored operation briefing bindings.*"));
        MethodInfo prepare = typeof(ExpeditionHUD).GetMethod("TryPrepareOperationPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(prepare.Invoke(hud, null), Is.False);
        Assert.That(prepare.Invoke(hud, null), Is.False);
        Assert.That(Read<GameObject>(hud, "operationRoot"), Is.Null);
    }

    [TestCase("Tutorial")]
    [TestCase("Expedition")]
    public void Radar_BothAuthoredScenesUseScopeAndSpriteFreeSweep(string sceneName)
    {
        if (sceneName != "Tutorial")
            foreach (UnityEngine.Rendering.Universal.Light2D light in AuthoredRuntimeFixture.Find<UnityEngine.Rendering.Universal.Light2D>(scene))
                light.enabled = false;
        Scene target = sceneName == "Tutorial" ? scene : AuthoredRuntimeFixture.Open(sceneName);
        try
        {
            RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(target);
            Assert.That(panel.TryValidateAuthoredPresentation(out string error), Is.True, error);
            Assert.That(new SerializedObject(panel).FindProperty("openFrames"), Is.Null);
            Assert.That(new SerializedObject(panel).FindProperty("closeFrames"), Is.Null);
            Assert.That(panel.GetComponentsInChildren<Animator>(true), Is.Empty);
            RadarScopeGraphic scope = Read<RadarScopeGraphic>(panel, "scopeGraphic");
            Assert.That(scope.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "A valid Graphic reference alone does not prove the scope can render.");
            Assert.That(scope.rectTransform.rect.size, Is.EqualTo(new Vector2(60f, 60f)));
            Assert.That(scope, Is.SameAs(Read<RadarScopeGraphic>(AuthoredRuntimeFixture.Single<RadarHUD>(target), "scopeGraphic")));
            Assert.That(scope.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(Read<Image>(panel, "scanSweep").transform.parent, Is.SameAs(scope.transform));
            Assert.That(Read<RadarPanelAnimator>(AuthoredRuntimeFixture.Single<ExpeditionHUD>(target), "cinematicRadarPanel"), Is.SameAs(panel));
        }
        finally { if (target != scene) AuthoredRuntimeFixture.Close(target); }
    }

    [TestCase("Tutorial")]
    [TestCase("Expedition")]
    public void Radar_ActualScopeGeometrySurvivesOpenScanSuppressionAndRapidToggle(string sceneName)
    {
        PrepareTweenRuntime();
        if (sceneName != "Tutorial")
            foreach (UnityEngine.Rendering.Universal.Light2D light in AuthoredRuntimeFixture.Find<UnityEngine.Rendering.Universal.Light2D>(scene))
                light.enabled = false;
        Scene target = sceneName == "Tutorial" ? scene : AuthoredRuntimeFixture.Open(sceneName);
        RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(target);
        try
        {
            RectTransform root = (RectTransform)panel.transform;
            RadarHUD radar = AuthoredRuntimeFixture.Single<RadarHUD>(target);
            RectTransform area = Read<RectTransform>(radar, "radarArea");
            RadarScopeGraphic scope = Read<RadarScopeGraphic>(panel, "scopeGraphic");
            Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(42f, 72f)));
            Assert.That(root.rect.size, Is.EqualTo(new Vector2(60f, 60f)));
            Assert.That(scope.transform.parent, Is.SameAs(area));
            Assert.That(scope.GetComponentInParent<Mask>(), Is.Null);
            Assert.That(scope.GetComponentInParent<RectMask2D>(), Is.Null);
            // PreviewScene has no rendered screen. Exercise UGUI's actual Rebuild
            // path in logical canvas units; the Play Mode probe covers pixel adjustment.
            scope.GetComponentInParent<Canvas>().pixelPerfect = false;
            panel.OpenImmediate();
            AssertVisibleScope(panel);
            panel.Close();
            Field<Sequence>(panel, "transitionSequence").Complete(true);
            Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
            panel.Open();
            Field<Sequence>(panel, "transitionSequence").Complete(true);
            AssertVisibleScope(panel);
            panel.PlayScanPulse();
            Field<Sequence>(panel, "scanSequence").Complete(true);
            AssertVisibleScope(panel);
            object boss = new object();
            panel.SetPresentationSuppressed(boss, true);
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
            panel.SetPresentationSuppressed(boss, false);
            AssertVisibleScope(panel);
            panel.Close(); panel.Open(); panel.Close();
            Field<Sequence>(panel, "transitionSequence").Complete(true);
            Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
            panel.SetPresentationSuppressed(boss, true);
            panel.SetPresentationSuppressed(boss, false);
            Assert.That(panel.IsOpen, Is.False);
            panel.Open();
            Field<Sequence>(panel, "transitionSequence").Complete(true);
            AssertVisibleScope(panel);
            GameObject contact = AuthoredRuntimeFixture.Create(target, null, "RadarBoundsContact", false);
            RadarTarget radarTarget = contact.AddComponent<RadarTarget>();
            radarTarget.SetVisible(true);
            radarTarget.SetMarkerType(RadarMarkerType.RewardObject);
            Transform center = Read<Transform>(radar, "defaultCenter");
            contact.transform.position = center.position + Vector3.right * 1000f;
            radar.SetTargets(new[] { radarTarget }, center);
            RadarMarkerUI marker = Field<Dictionary<RadarTarget, RadarMarkerUI>>(radar, "markerMap")[radarTarget];
            Assert.That(marker.transform.parent, Is.SameAs(area));
            Assert.That(scope.rectTransform.rect.Contains(marker.RectTransform.anchoredPosition), Is.True);
            Assert.That(marker.RectTransform.anchoredPosition.x, Is.EqualTo(26f).Within(.01f));
            Assert.That(scope.rectTransform.rect, Is.EqualTo(area.rect));
        }
        finally
        {
            Call(panel, "StopOwnedTweens");
            if (target != scene) AuthoredRuntimeFixture.Close(target);
        }
    }

    [Test]
    public void Radar_AuthoredValidationRejectsCollapsedScopeAndDisabledGraphic()
    {
        RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(scene);
        RadarScopeGraphic scope = Read<RadarScopeGraphic>(panel, "scopeGraphic");
        Assert.That(panel.TryValidateAuthoredPresentation(out _), Is.True);
        scope.enabled = false;
        Assert.That(panel.TryValidateAuthoredPresentation(out _), Is.False);
        scope.enabled = true;
        ((RectTransform)panel.transform).sizeDelta = Vector2.one;
        Assert.That(panel.TryValidateAuthoredPresentation(out _), Is.False);
    }

    private static void AssertVisibleScope(RadarPanelAnimator panel)
    {
        Assert.That(panel.IsOpen, Is.True);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.EqualTo(1f));
        RectTransform root = (RectTransform)panel.transform;
        Assert.That(root.localScale, Is.EqualTo(Vector3.one));
        Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(42f, 72f)));
        RadarScopeGraphic scope = Read<RadarScopeGraphic>(panel, "scopeGraphic");
        CanvasRenderer renderer = scope.GetComponent<CanvasRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(scope.gameObject.activeInHierarchy, Is.True);
        scope.SetAllDirty();
        scope.Rebuild(CanvasUpdate.PreRender);
        Mesh mesh = renderer.GetMesh();
        Assert.That(mesh, Is.Not.Null, "The actual CanvasRenderer must receive the procedural mesh.");
        Assert.That(mesh.vertexCount, Is.GreaterThan(100));
        Assert.That(mesh.bounds.size.x, Is.InRange(58f, 60f));
        Assert.That(mesh.bounds.size.y, Is.InRange(58f, 60f));
        Assert.That(renderer.materialCount, Is.GreaterThan(0));
    }

    [Test]
    public void Radar_OpenCloseRacesResolveLatestRequestWithoutStackedTweens()
    {
        PrepareTweenRuntime();
        RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(scene);
        panel.CloseImmediate();
        int objects = panel.GetComponentsInChildren<Transform>(true).Length;
        panel.Open();
        Sequence first = Field<Sequence>(panel, "transitionSequence");
        panel.Open();
        Assert.That(Field<Sequence>(panel, "transitionSequence"), Is.SameAs(first));
        first.Goto(0.07f);
        panel.Close();
        Assert.That(first.IsActive(), Is.False);
        Field<Sequence>(panel, "transitionSequence").Complete(true);
        Assert.That(panel.IsOpen, Is.False);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
        Assert.That(Field<Tween>(panel, "idleTween"), Is.Null);
        panel.OpenImmediate();
        panel.Close();
        Field<Sequence>(panel, "transitionSequence").Goto(0.06f);
        panel.Open();
        Field<Sequence>(panel, "transitionSequence").Complete(true);
        Assert.That(panel.IsOpen, Is.True);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.EqualTo(1f));
        Assert.That(Field<Tween>(panel, "idleTween").IsActive(), Is.True);
        Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
    }

    [Test]
    public void Radar_SuppressionPreservesRequestedStateAndStopsIdleAndScan()
    {
        PrepareTweenRuntime();
        RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(scene);
        object boss = new object(), cinematic = new object();
        panel.OpenImmediate();
        panel.PlayScanPulse();
        Sequence scan = Field<Sequence>(panel, "scanSequence");
        Assert.That(scan.IsActive(), Is.True);
        Assert.That(Field<Tween>(panel, "idleTween"), Is.Null);
        panel.SetPresentationSuppressed(boss, true);
        panel.SetPresentationSuppressed(cinematic, true);
        Assert.That(scan.IsActive(), Is.False);
        Assert.That(panel.IsOpen, Is.True);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
        Assert.That(Read<Image>(panel, "scanSweep").gameObject.activeSelf, Is.False);
        panel.SetPresentationSuppressed(cinematic, false);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
        panel.SetPresentationSuppressed(boss, false);
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.EqualTo(1f));
        panel.SetPresentationSuppressed(boss, true);
        panel.Close();
        panel.SetPresentationSuppressed(boss, false);
        Assert.That(panel.IsOpen, Is.False, "Manual close must survive boss suppression.");
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.Zero);
        panel.PlayScanPulse();
        Assert.That(Field<Sequence>(panel, "scanSequence"), Is.Null);
    }

    [Test]
    public void Radar_ScanReusesOneSweepAndDisableCleanupIsIdempotent()
    {
        PrepareTweenRuntime();
        RadarPanelAnimator panel = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(scene);
        panel.OpenImmediate();
        Image sweep = Read<Image>(panel, "scanSweep");
        int objects = panel.GetComponentsInChildren<Transform>(true).Length;
        panel.PlayScanPulse();
        Sequence first = Field<Sequence>(panel, "scanSequence");
        first.Goto(0.15f);
        Assert.That(sweep.gameObject.activeSelf, Is.True);
        Assert.That(sweep.rectTransform.sizeDelta.x, Is.GreaterThan(0f));
        panel.PlayScanPulse();
        Assert.That(first.IsActive(), Is.False);
        Field<Sequence>(panel, "scanSequence").Complete(true);
        Assert.That(sweep.gameObject.activeSelf, Is.False);
        Assert.That(Field<Tween>(panel, "idleTween").IsActive(), Is.True);
        Call(panel, "OnDisable");
        Call(panel, "OnDisable");
        Assert.That(Field<Tween>(panel, "idleTween"), Is.Null);
        Assert.That(panel.IsOpen, Is.True);
        Call(panel, "OnEnable");
        Assert.That(Read<CanvasGroup>(panel, "canvasGroup").alpha, Is.EqualTo(1f));
        Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
    }

    [Test]
    public void TutorialFocus_FadesGameplayAndHidesPromptsWithoutClearingForeignOwner()
    {
        PrepareTweenRuntime();
        RadarPanelAnimator radar = AuthoredRuntimeFixture.Single<RadarPanelAnimator>(scene);
        radar.OpenImmediate();
        CanvasGroup group = Read<CanvasGroup>(hud, "cinematicCanvasGroup");
        TutorialPromptUI prompt = Read<TutorialPromptUI>(flow, "promptUI");
        InteractionPromptUI interact = AuthoredRuntimeFixture.Single<InteractionPromptUI>(scene);
        prompt.gameObject.SetActive(true);
        interact.gameObject.SetActive(true);
        object focus = Field<object>(flow, "discoveryCameraOwner");
        group.alpha = 1f;
        Call(flow, "SetTutorialFocusHud", focus, true);
        Tween fade = Field<Tween>(hud, "cinematicVisibilityTween");
        Call(flow, "SetTutorialFocusHud", focus, true);
        Assert.That(Field<Tween>(hud, "cinematicVisibilityTween"), Is.SameAs(fade));
        Assert.That(Field<HashSet<object>>(hud, "cinematicModeOwners").Count, Is.EqualTo(1));
        Assert.That(prompt.gameObject.activeSelf, Is.False);
        Assert.That(interact.gameObject.activeSelf, Is.False);
        fade.Goto(0.11f);
        Assert.That(group.alpha, Is.InRange(0.01f, 0.99f));
        fade.Complete(true);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(Field<Tween>(radar, "idleTween"), Is.Null);
        object foreign = new object();
        hud.SetCinematicMode(foreign, true);
        Call(flow, "ClearActiveTargetPresentation");
        Assert.That(hud.IsCinematicMode, Is.True);
        Assert.That(Field<HashSet<object>>(hud, "cinematicModeOwners"), Is.EquivalentTo(new[] { foreign }));
        hud.SetCinematicMode(foreign, false, 0.22f);
        Field<Tween>(hud, "cinematicVisibilityTween").Complete(true);
        Assert.That(group.alpha, Is.EqualTo(1f));
        Assert.That(prompt.gameObject.activeSelf, Is.True);
        Assert.That(interact.gameObject.activeSelf, Is.True);
        Assert.That(radar.IsOpen, Is.True);
        Assert.That(Field<Tween>(radar, "idleTween").IsActive(), Is.True);
        GameObject[] hidden = Field<GameObject[]>(hud, "additionalObjectsToHideDuringCinematic");
        Assert.That(hidden, Is.EquivalentTo(new[] { prompt.gameObject, interact.gameObject }));
        Assert.That(hud.GetComponentsInChildren<PixelCrushers.DialogueSystem.StandardDialogueUI>(true), Is.Empty,
            "Pixel Crushers presentation must not be inside the faded gameplay canvas.");
    }

    [Test]
    public void PurpleCore_IdleWaveStaysStationaryAndConsumedCoreCannotRestartIt()
    {
        PrepareTweenRuntime();
        GameObject core = Read<GameObject>(flow, "alienSignal");
        Transform visual = Read<Transform>(flow, "alienSignalVisualRoot");
        SpriteRenderer wave = Read<SpriteRenderer>(flow, "alienSignalPulseRenderer");
        core.SetActive(true);
        visual.gameObject.SetActive(true);
        Assert.That(wave.transform.parent, Is.SameAs(core.transform));
        Assert.That(wave.transform.IsChildOf(visual), Is.False);
        Call(flow, "CacheAlienSignalVisualState");
        TutorialCorePresentationProgress progress = Field<TutorialCorePresentationProgress>(flow, "corePresentationProgress");
        Assert.That(progress.TryBeginReveal(), Is.True);
        Assert.That(progress.TryCompleteReveal(), Is.True);
        Call(flow, "StartAlienSignalIdleMotion");
        Sequence idleWave = Field<Sequence>(flow, "alienSignalIdleWaveTween");
        Tween idleFloat = Field<Tween>(flow, "alienSignalIdleTween");
        Assert.That(idleWave, Is.Not.Null);
        Call(flow, "StartAlienSignalIdleMotion");
        Assert.That(Field<Sequence>(flow, "alienSignalIdleWaveTween"), Is.SameAs(idleWave));
        Assert.That(Field<Tween>(flow, "alienSignalIdleTween"), Is.SameAs(idleFloat));
        Vector3 waveOrigin = wave.transform.position;
        idleFloat.Goto(0.8f);
        Assert.That(wave.transform.position, Is.EqualTo(waveOrigin));
        Assert.That(progress.TryBeginTransfer(), Is.True);
        Call(flow, "StopAlienSignalIdleMotion");
        Assert.That(wave.gameObject.activeSelf, Is.False);
        Assert.That(idleWave.IsActive(), Is.False);
        Transform travel = Read<SpriteRenderer>(flow, "alienSignalCoreRenderer").transform;
        travel.localPosition += Vector3.right * 8f;
        Assert.That(wave.transform.position, Is.EqualTo(waveOrigin));
        progress.RestoreReadyAfterInterruptedTransfer();
        Call(flow, "StartAlienSignalIdleMotion");
        Assert.That(wave.gameObject.activeSelf, Is.True);
        Assert.That(progress.TryBeginTransfer(), Is.True);
        Assert.That(progress.TryBeginImpact(), Is.True);
        Assert.That(progress.TryApplyCurse(), Is.True);
        Call(flow, "StartAlienSignalIdleMotion");
        Assert.That(wave.gameObject.activeSelf, Is.False);
        Assert.That(Field<Sequence>(flow, "alienSignalIdleWaveTween"), Is.Null);
    }

    [Test]
    public void RadarAndTutorial_PresentationHooksPreserveGameplayAuthority()
    {
        string animator = File.ReadAllText("Assets/02_Scripts/UI/RadarPanelAnimator.cs");
        Assert.That(animator, Does.Not.Contain("StartCoroutine"));
        Assert.That(animator, Does.Not.Contain("new GameObject"));
        Assert.That(animator, Does.Not.Contain("AddComponent"));
        Assert.That(animator, Does.Not.Contain("PlayerRadarScanner"));
        string scanner = File.ReadAllText("Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        Assert.That(Regex.Matches(scanner, @"radarPanelAnimator\?\.PlayScanPulse\(\)").Count, Is.EqualTo(1));
        string tutorial = File.ReadAllText("Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        string travel = tutorial.Substring(tutorial.IndexOf("private Sequence BeginAlienSignalInfiltration()"));
        Assert.That(travel.IndexOf("StopAlienSignalIdleMotion();"), Is.LessThan(travel.IndexOf("coreTravelTransform.DOMove")));
        Assert.That(tutorial, Does.Contain("SetTutorialFocusHud(discoveryCameraOwner, true)"));
        Assert.That(tutorial, Does.Contain("SetTutorialFocusHud(this, true)"));
        Assert.That(tutorial, Does.Contain("tutorialCamera.IsCinematicFocusOwnedBy(this)"));
    }

    [Test]
    public void Settlement_DialogueLaunchGuardHasReadableFallbackWithoutLocalizationService()
    {
        if (VoidScrapperLocalizationService.HasInstance) Assert.Ignore("An existing localization service owns this Editor session.");
        string result = (string)typeof(SettlementController).GetMethod("GetLocalizedText", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { SettlementExpeditionLaunchGuard.DialogueActiveTextKey });
        Assert.That(result, Is.EqualTo("통신이 끝난 후 탐사를 시작할 수 있습니다."));
        Assert.That(result, Does.Not.Contain("system.settlement"));
    }

    private static T Field<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Field(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

    private void PrepareTweenRuntime()
    {
        if (!manualTweenScope)
        {
            // Match the existing QAStabilizationPass1Tests manual tween fixture:
            // installed DOTween gates Kill on initialized even though Goto works in EditMode.
            // No engine host is created; restore the flag after killing only our tweens.
            wasTweenInitialized = (bool)TweenInitialized.GetValue(null);
            TweenInitialized.SetValue(null, true);
            manualTweenScope = true;
        }
        if (DOTween.instance != null)
        {
            return;
        }
        // Never require a saved active scene. The installed DOTween public Init is
        // a no-op outside Play Mode; it does not create a service in the user's scene.
        // Never clear a DOTween runtime that existed before this test.
        tweenSetupScene = EditorSceneManager.NewPreviewScene();
        try
        {
            DOTween.Init();
        }
        finally
        {
            ownsTweenRuntime = DOTween.instance != null;
        }
    }

    private void UseAuthoredFixture()
    {
        Assert.That(Read<TutorialPromptUI>(flow, "promptUI"), Is.Not.Null);
        Assert.That(Read<GameObject>(hud, "operationRoot"), Is.Not.Null);
    }

    private GameObject Create(string name, Transform parent = null, bool rect = true)
    {
        GameObject target = AuthoredRuntimeFixture.Create(scene, parent, name, rect);
        target.SetActive(false);
        return target;
    }

    private static T Read<T>(Object owner, string field) where T : Object => AuthoredRuntimeFixture.Read<T>(owner, field);

    private static void SetReference(Object owner, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int[] RootIds(Scene target)
    {
        GameObject[] roots = target.GetRootGameObjects();
        int[] ids = new int[roots.Length];
        for (int i = 0; i < roots.Length; i++)
        {
            ids[i] = roots[i].GetInstanceID();
        }
        System.Array.Sort(ids);
        System.Array.Sort(ids);
        return ids;
    }
}
