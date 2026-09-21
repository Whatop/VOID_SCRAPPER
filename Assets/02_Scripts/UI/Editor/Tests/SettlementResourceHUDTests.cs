using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SettlementResourceHUDTests
{
    private Scene scene;
    private Scene userScene;
    private bool userDirty;
    private int[] userRoots;
    private SettlementHUD hud;
    private SettlementController controller;
    private PermanentProgress progress;
    private PermanentProgress previousProgress;
    private RectTransform container;
    private TextMeshProUGUI legacyText;
    private Image legacyScrap;
    private Image legacyCore;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userDirty = userScene.isDirty;
        userRoots = RootIds(userScene);
        previousProgress = PermanentProgress.Instance;
        scene = AuthoredRuntimeFixture.Open("Settlement");
        hud = AuthoredRuntimeFixture.Single<SettlementHUD>(scene);
        controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        container = Read<RectTransform>(hud, "resourceContainer");
        legacyText = Read<TextMeshProUGUI>(hud, "currencyText");
        legacyScrap = Read<Image>(hud, "scrapCurrencyIcon");
        legacyCore = Read<Image>(hud, "coreCurrencyIcon");
        progress = Create(scene, null, "TestOwnedPermanentProgress").gameObject.AddComponent<PermanentProgress>();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        Call(controller, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (hud != null) Call(hud, "OnDisable");
            if (controller != null) Call(controller, "OnDisable");
        }
        finally
        {
            typeof(PermanentProgress).GetProperty("Instance").SetValue(null, previousProgress);
            if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene);
            scene = default;
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(RootIds(userScene), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    [Test]
    public void ActualHudLifecycle_InitialPositiveVisibleZerosEventsAndReenableRefresh()
    {
        UseAuthoredFixture();
        progress.AddPermanentCurrency(CurrencyType.ScrapParts, 14);
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, 6);
        float typography = Cell(0).value.fontSize;
        Call(hud, "Awake");
        Call(hud, "OnEnable");
        Call(hud, "Start");
        AssertValues(14, 0, 6);
        int[] objects = ObjectIds();
        string[] style = Snapshot(Cell(0));
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Call(hud, "OnEnable");
            Assert.That(SubscriberCount(), Is.EqualTo(1));
            Call(hud, "OnDisable");
            Assert.That(SubscriberCount(), Is.Zero);
            progress.AddPermanentCurrency(CurrencyType.CoreShards, 1);
            StringAssert.EndsWith(cycle.ToString(), Cell(1).value.text);
            Call(hud, "OnEnable");
            AssertValues(14, cycle + 1, 6);
        }
        progress.TrySpend(14, 3);
        AssertValues(0, 0, 6);
        Assert.That(ObjectIds(), Is.EqualTo(objects));
        // Value content changes, but transform, background and icon serialized data do not.
        Assert.That(Snapshot(Cell(0)).Take(3).ToArray(), Is.EqualTo(style.Take(3).ToArray()));
        Assert.That(Cell(0).value.fontSize, Is.EqualTo(typography));
    }

    [Test]
    public void Cleanup_UnsubscribesActualPublisherEvenWhenInspectorControllerChanges()
    {
        UseAuthoredFixture();
        Call(hud, "OnEnable");
        Assert.That(SubscriberCount(), Is.EqualTo(1));
        SettlementController other = Create(scene, null, "OtherController").gameObject.AddComponent<SettlementController>();
        Bind(hud, "settlementController", other);
        Call(hud, "OnDisable");
        Call(hud, "OnDestroy");
        Assert.That(SubscriberCount(), Is.Zero);
    }

    [TestCase("scrapResource.icon")]
    [TestCase("scrapResource.root")]
    [TestCase("resourceStripRoot")]
    public void MissingInstalledBinding_WarnsOnceAndNeverCreatesReplacement(string binding)
    {
        UseAuthoredFixture();
        Object previous = Read<Object>(hud, binding);
        try
        {
            Bind(hud, binding, null);
            int[] objects = ObjectIds();
            ExpectMissingResourceWarning();
            Call(hud, "Awake");
            hud.SetCurrency(1, 2, 3);
            hud.SetCurrency(4, 5, 6);
            if (binding != "resourceStripRoot") StringAssert.EndsWith("5", Cell(1).value.text);
            Assert.That(ObjectIds(), Is.EqualTo(objects));
            LogAssert.NoUnexpectedReceived();
        }
        finally { Bind(hud, binding, previous); }
    }

    [Test]
    public void MissingResources_CannotConstructOrWriteLegacyPresentation()
    {
        Object previousContainer = Read<Object>(hud, "resourceContainer");
        Object previousStrip = Read<Object>(hud, "resourceStripRoot");
        try
        {
            Bind(hud, "resourceContainer", null);
            Bind(hud, "resourceStripRoot", null);
            int[] objects = ObjectIds();
            string legacy = legacyText.text;
            ExpectMissingResourceWarning();
            Call(hud, "Awake");
            Call(hud, "OnEnable");
            hud.SetCurrency(0, 12, 2);
            Call(hud, "OnDisable");
            Call(hud, "OnEnable");
            Assert.That(Strip(), Is.Null);
            Assert.That(ObjectIds(), Is.EqualTo(objects));
            Assert.That(legacyText.text, Is.EqualTo(legacy));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Bind(hud, "resourceContainer", previousContainer);
            Bind(hud, "resourceStripRoot", previousStrip);
        }
    }

    private void ExpectMissingResourceWarning()
    {
        string message = "[SettlementHUD] Missing, duplicate, or invalid persistent-resource bindings: ";
        MethodInfo describe = typeof(SettlementHUD).GetMethod("DescribeResourceBinding", PrivateInstance);
        foreach (CurrencyType resource in AuthoredRuntimeFixture.PermanentResources)
            if (!hud.HasValidResourceCell(resource)) message += (string)describe.Invoke(hud, new object[] { resource });
        LogAssert.Expect(LogType.Warning, message + ". Restore the listed authored Inspector bindings. " +
            "Only the affected resource cells were skipped; no replacement strip was created.");
    }

    [Test]
    public void AuthoredBindings_NeverInvokeConstruction()
    {
        UseAuthoredFixture();
        int[] objects = ObjectIds();
        Call(hud, "Awake");
        hud.SetCurrency(0, 0, 0);
        AssertValues(0, 0, 0);
        Assert.That(ObjectIds(), Is.EqualTo(objects));
    }

    [Test]
    public void ControllerReadyEvent_RefreshesAfterAnInitiallyUnavailableProgressPublisher()
    {
        UseAuthoredFixture();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, null);
        Call(hud, "OnEnable");
        AssertValues(0, 0, 0);
        // Existing controller readiness notification (normally Start -> NotifyChanged).
        // Do not call controller Start: it owns selection/state/save behavior outside this test.
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        progress.AddPermanentCurrency(CurrencyType.CoreShards, 4);
        Call(controller, "NotifyChanged");
        AssertValues(0, 4, 0);
        Assert.That(SubscriberCount(), Is.EqualTo(1));
    }

    private void UseAuthoredFixture()
    {
        Assert.That(scene.IsValid(), Is.True);
        Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene), Is.True);
    }    private SettlementHUD.ResourceCell Cell(int i) => hud.GetResourceCell(AuthoredRuntimeFixture.PermanentResources[i]);
    private RectTransform Strip() => Read<RectTransform>(hud, "resourceStripRoot");
    private int SubscriberCount() => ((Delegate)typeof(SettlementController).GetField("Changed", PrivateInstance).GetValue(controller))?.GetInvocationList().Length ?? 0;
    private int[] ObjectIds() => hud.GetComponentInParent<Canvas>().GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
    private static int[] RootIds(Scene target) => target.GetRootGameObjects().Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
    private static string[] Snapshot(SettlementHUD.ResourceCell cell) => new[]
    {
        EditorJsonUtility.ToJson(cell.root), EditorJsonUtility.ToJson(cell.background),
        EditorJsonUtility.ToJson(cell.icon), EditorJsonUtility.ToJson(cell.value)
    };
    private void AssertValues(int scrap, int core, int alloy)
    {
        int[] values = { scrap, core, alloy };
        for (int i = 0; i < 3; i++)
        {
            StringAssert.EndsWith(" " + values[i], Cell(i).value.text);
            Assert.That(Cell(i).root.gameObject.activeSelf, Is.True);
            Assert.That(Cell(i).value.enabled, Is.True);
            Assert.That(Cell(i).icon.enabled, Is.True);
        }
    }
    private static RectTransform Create(Scene target, Transform parent, string name) =>
        (RectTransform)AuthoredRuntimeFixture.Create(target, parent, name, true).transform;
    private static T Read<T>(Object target, string field) where T : Object => new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    private static void Bind(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Call(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
}
