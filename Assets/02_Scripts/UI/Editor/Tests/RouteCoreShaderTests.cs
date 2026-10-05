using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RouteCoreShaderTests
{
    private Scene scene;
    private SettlementDefenseEncounterController encounter;
    private SettlementDefensePurpleCore purple;
    private RouteCoreCorruptionVisual presentation;
    private SpriteRenderer overlay;
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Flags).Invoke(owner, args);
    [SetUp] public void Open()
    {
        scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        purple = Get<SettlementDefensePurpleCore>(encounter, "purpleCore");
        presentation = Get<RouteCoreCorruptionVisual>(purple, "corruptionPresentation");
        overlay = Get<SpriteRenderer>(presentation, "overlay");
    }
    [TearDown] public void Close() { encounter.CancelEncounter(); EditorSceneManager.ClosePreviewScene(scene); }
    private void Begin()
    {
        Get<GameObject>(encounter, "deckRoot").SetActive(true);
        Assert.That(purple.Begin(encounter, Get<PlayerRadarScanner>(encounter, "deckRadar")), Is.True);
    }
    private void Scan(params RadarTarget[] targets) => Call(purple, "HandleScan", Vector2.zero, 22f, targets);
    private float Property(string name)
    {
        var block = new MaterialPropertyBlock(); overlay.GetPropertyBlock(block); return block.GetFloat(name);
    }

    [Test] public void SavedGraphMaterialAndOverlayAreValidAndUseUnlitSpriteTarget()
    {
        RouteCoreShaderAuthoring.Validate(scene);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(RouteCoreShaderAuthoring.GraphPath);
        Assert.That(shader, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        Assert.That(overlay.sharedMaterial.shader, Is.EqualTo(shader));
        Assert.That(File.ReadAllText(RouteCoreShaderAuthoring.GraphPath), Does.Contain("UniversalSpriteUnlitSubTarget"));
        Assert.That(overlay.GetComponents<Component>().Length, Is.EqualTo(2));
        Assert.That(overlay.sprite, Is.EqualTo(Get<SpriteRenderer>(purple, "visual").sprite));
        Assert.That(overlay.enabled, Is.False);
        var dependencies = AssetDatabase.GetDependencies(RouteCoreDeckAuthoring.ScenePath, true);
        Assert.That(dependencies, Does.Contain(RouteCoreShaderAuthoring.GraphPath));
        Assert.That(dependencies, Does.Contain(RouteCoreShaderAuthoring.MaterialPath));
    }

    [Test] public void BothAuthoringHelpersPreserveOneOverlayAndTheOriginalSprite()
    {
        var original = Get<SpriteRenderer>(purple, "visual");
        string baseline = EditorJsonUtility.ToJson(original);
        int id = overlay.GetInstanceID();
        RouteCoreShaderAuthoring.Apply(purple); RouteCoreCombatAuthoring.Apply(scene); RouteCoreShaderAuthoring.Apply(purple);
        Assert.That(Get<SpriteRenderer>(presentation, "overlay").GetInstanceID(), Is.EqualTo(id));
        Assert.That(purple.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(2));
        Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(baseline));
    }

    [Test] public void ActiveScanControlsRevealWhileDamageAndTimingRemainAuthoritative()
    {
        Begin(); Assert.That(Property("_Reveal"), Is.Zero);
        Assert.That(Property("_CorruptionAmount"), Is.EqualTo(1));
        purple.Health.TakeDamage(1000); Assert.That(purple.Health.CurrentHp, Is.EqualTo(50));
        Scan(); Assert.That(Property("_Reveal"), Is.Zero);
        Scan(purple.Target); Assert.That(Property("_Reveal"), Is.EqualTo(1));
        Assert.That(Get<Collider2D>(purple, "damageCollider").enabled, Is.True);
        Assert.That(Get<float>(purple, "exposureDuration"), Is.EqualTo(4));
        Assert.That(Get<float>(purple, "maxHp"), Is.EqualTo(50));
        Assert.That(Get<int>(purple, "pulseCount"), Is.EqualTo(8));
        Scan(purple.Target); Assert.That(purple.ExposureCount, Is.EqualTo(1));
        Call(purple, "Hide", true); Assert.That(Property("_Reveal"), Is.Zero);
        Assert.That(Get<Collider2D>(purple, "damageCollider").enabled, Is.False);
        Assert.That(Get<Collider2D>(purple, "radarCollider").enabled, Is.True);
    }

    [Test] public void CancelDisableAndRetryRestoreBaselineAndReusePropertyBlock()
    {
        var baseline = new MaterialPropertyBlock(); baseline.SetFloat("_Reveal", .123f); overlay.SetPropertyBlock(baseline);
        var material = overlay.sharedMaterial; string json = EditorJsonUtility.ToJson(material);
        Begin(); object block = Get<MaterialPropertyBlock>(presentation, "block");
        Scan(purple.Target); purple.Cancel(); Assert.That(Property("_Reveal"), Is.EqualTo(.123f));
        Assert.That(overlay.enabled, Is.False);
        Begin(); Assert.That(Property("_Reveal"), Is.Zero);
        Assert.That(Get<MaterialPropertyBlock>(presentation, "block"), Is.SameAs(block));
        purple.gameObject.SetActive(false);
        Call(presentation, "OnDisable"); // Non-ExecuteAlways components do not receive EditMode lifecycle callbacks.
        Assert.That(Get<DG.Tweening.Tween>(presentation, "transition"), Is.Null);
        Assert.That(Property("_Reveal"), Is.EqualTo(.123f));
        Assert.That(overlay.sharedMaterial, Is.SameAs(material)); Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(json));
    }

    [TestCase(true)] [TestCase(false)]
    public void MissingMaterialOrOverlayFallsBackWithoutBlockingExposure(bool missingMaterial)
    {
        if (missingMaterial) overlay.sharedMaterial = null; else Set(presentation, "overlay", null);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RouteCoreDeck/PurpleCorruptionTarget/CorruptionOverlay: missing or unsupported"));
        Begin(); Scan(purple.Target); Scan(purple.Target);
        Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Exposed));
        Assert.That(Get<SpriteRenderer>(purple, "visual").color.a, Is.EqualTo(1));
        purple.Health.TakeDamage(10); Assert.That(purple.Health.CurrentHp, Is.EqualTo(40));
        purple.Cancel(); Begin(); // Missing binding warning remains once per component, not once per state/retry.
    }

    [Test] public void MissingPresentationComponentFallsBackAndNeverGatesDeath()
    {
        Set(purple, "corruptionPresentation", null);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("optional corruption presentation is not authored"));
        Begin(); Scan(purple.Target); purple.Health.TakeDamage(1000);
        Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Cleansed));
        Assert.That(purple.TryPlayPurification(out _), Is.False);
        Assert.That(purple.GetComponentInChildren<RewardDropper>(), Is.Null);
    }

    [Test] public void ShaderControllerHasNoUpdateOrMaterialInstantiationPath()
    {
        var methods = typeof(RouteCoreCorruptionVisual).GetMethods(Flags);
        Assert.That(methods.Any(m => m.Name == "Update" || m.Name == "LateUpdate" || m.Name == "FixedUpdate"), Is.False);
        string source = File.ReadAllText("Assets/02_Scripts/VFX/RouteCoreCorruptionVisual.cs");
        Assert.That(source, Does.Not.Contain(".material"));
        Assert.That(source, Does.Not.Contain("KillAll"));
        Assert.That(source, Does.Not.Contain("new Material("));
        Assert.That(source, Does.Contain("Shader.PropertyToID"));
        Assert.That(source, Does.Contain("SetLink"));
    }
}
