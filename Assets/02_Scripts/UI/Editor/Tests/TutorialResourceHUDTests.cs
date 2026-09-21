using System;
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

public sealed class TutorialResourceHUDTests
{
    private Scene scene;
    private Scene userScene;
    private bool userDirty;
    private int[] userRoots;
    private ExpeditionHUD hud;
    private GameObject root;
    private RunManager manager;
    private RunManager previousManager;
    private RunWallet wallet;
    private Action relay;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userDirty = userScene.isDirty;
        userRoots = AuthoredRuntimeFixture.RootIds(userScene);
        previousManager = RunManager.Instance;
        scene = AuthoredRuntimeFixture.Open("Tutorial");
        hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(scene);
        root = Read<GameObject>(hud, "resourceRoot");

        manager = Create("IsolatedRunManager", null).AddComponent<RunManager>();
        RunContext context = new RunContext(default, default, "basic_ship", SeaRegionType.DenseDebris);
        typeof(RunManager).GetField("currentRun", PrivateInstance).SetValue(manager, context);
        typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        wallet = context.Wallet;
        relay = (Action)Delegate.CreateDelegate(typeof(Action), manager, typeof(RunManager).GetMethod("HandleWalletChanged", PrivateInstance));
        wallet.Changed += relay;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (hud != null) Call(hud, "OnDisable");
        }
        finally
        {
            if (wallet != null) wallet.Changed -= relay;
            relay = null;
            typeof(RunManager).GetProperty("Instance").SetValue(null, previousManager);
            if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene);
            scene = default;
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(AuthoredRuntimeFixture.RootIds(userScene), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    [Test]
    public void WalletLifecycle_InitialPositiveAndZeroTransitionsPackRowsWithoutCreatingVisuals()
    {
        UseAuthoredFixture();
        int[] objects = ObjectIds();
        wallet.Add(CurrencyType.TuningChips, 7);
        Call(hud, "Subscribe");
        Call(hud, "RefreshCurrentResourceBalances");
        AssertAmount(4, 7, true);
        AssertAmount(3, 0, false);
        Vector2 origin = ((RectTransform)Counter(0).transform).anchoredPosition;
        Assert.That(((RectTransform)Counter(4).transform).anchoredPosition, Is.EqualTo(origin));
        wallet.Add(CurrencyType.StabilizedAlloy, 3);
        AssertAmount(3, 3, true);
        Assert.That(((RectTransform)Counter(3).transform).anchoredPosition, Is.EqualTo(origin));
        float spacing = new SerializedObject(hud).FindProperty("resourceCounterRowSpacing").floatValue;
        Assert.That(((RectTransform)Counter(4).transform).anchoredPosition, Is.EqualTo(origin + Vector2.down * spacing));
        wallet.TrySpend(CurrencyType.StabilizedAlloy, 3);
        AssertAmount(3, 0, false);
        Assert.That(((RectTransform)Counter(4).transform).anchoredPosition, Is.EqualTo(origin));
        wallet.TrySpend(CurrencyType.TuningChips, 7);
        AssertAmount(4, 0, false);
        Assert.That(ObjectIds(), Is.EqualTo(objects));
    }

    [Test]
    public void DisableReenable_RefreshesCurrentWalletAndDoesNotDuplicateSubscriptions()
    {
        UseAuthoredFixture();
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            // Same resource methods called by OnEnable/RefreshAll; no unrelated HUD builders.
            Call(hud, "Subscribe");
            Call(hud, "Subscribe");
            Call(hud, "RefreshCurrentResourceBalances");
            Assert.That(SubscriberCount(), Is.EqualTo(1));
            AssertAmount(4, cycle - 1, cycle > 1);
            Call(hud, "OnDisable");
            Assert.That(SubscriberCount(), Is.Zero);
            wallet.Add(CurrencyType.TuningChips, 1);
            Assert.That(Counter(4).Amount, Is.EqualTo(cycle - 1));
        }
        Call(hud, "Subscribe");
        Call(hud, "RefreshCurrentResourceBalances");
        AssertAmount(4, 3, true);
        wallet.Add(CurrencyType.TuningChips, 2);
        AssertAmount(4, 5, true);
        // Cleanup must unsubscribe from the original publisher even if its singleton changes.
        typeof(RunManager).GetProperty("Instance").SetValue(null, previousManager);
        Call(hud, "OnDisable");
        Assert.That(SubscriberCount(), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingAuthoredBinding_WarnsOnceSkipsOnlyAffectedCounterAndNeverBuildsReplacement(bool missingRegistration)
    {
        UseAuthoredFixture();
        Object bindingOwner = missingRegistration ? (Object)hud : Counter(4);
        string property = missingRegistration ? "tuningChipCounter" : "amountText";
        Object previous = new SerializedObject(bindingOwner).FindProperty(property).objectReferenceValue;
        try
        {
            if (missingRegistration)
            {
                Bind(hud, "tuningChipCounter", null);
            }
            else
            {
                Bind(Counter(4), "amountText", null);
            }
            int[] objects = ObjectIds();
            wallet.Add(CurrencyType.StabilizedAlloy, 9);
            string path = hud.name;
            for (Transform parent = hud.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            LogAssert.Expect(LogType.Warning,
                "[ExpeditionHUD] Authored TuningChips counter has missing, invalid, or duplicate bindings. " +
                $"Resource mapping index 4 at '{path}', scene '{scene.path}'. " +
                "Restore the listed authored Inspector bindings. Only this resource counter was skipped.");
            for (int i = 0; i < 2; i++)
            {
                Call(hud, "RefreshCurrentResourceBalances");
            }
            AssertAmount(3, 9, true);
            Assert.That(ObjectIds(), Is.EqualTo(objects));
            LogAssert.NoUnexpectedReceived();
        }
        finally { Bind(bindingOwner, property, previous); }
    }

    [Test]
    public void AuthoredResources_RefreshWithoutCloning()
    {
        UseAuthoredFixture();
        int[] objects = ObjectIds();
        wallet.Add(CurrencyType.StabilizedAlloy, 4);
        wallet.Add(CurrencyType.TuningChips, 5);
        Call(hud, "RefreshCurrentResourceBalances");
        Call(hud, "RefreshCurrentResourceBalances");
        AssertAmount(3, 4, true);
        AssertAmount(4, 5, true);
        Assert.That(ObjectIds(), Is.EqualTo(objects));
    }

    private void UseAuthoredFixture()
    {
        Assert.That(scene.IsValid(), Is.True);
        Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene), Is.True);
    }    private ResourceCounterUI Counter(int i) => Read<ResourceCounterUI>(hud, AuthoredRuntimeFixture.RunResources[i]);
    private int[] ObjectIds() => hud.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
    private int SubscriberCount() => ((Delegate)typeof(RunManager).GetField("WalletChanged", PrivateInstance).GetValue(manager))?.GetInvocationList().Length ?? 0;
    private void AssertAmount(int index, int amount, bool visible)
    {
        Assert.That(Counter(index).Amount, Is.EqualTo(amount));
        Assert.That(Read<TextMeshProUGUI>(Counter(index), "amountText").text, Is.EqualTo(amount.ToString()));
        Assert.That(Counter(index).gameObject.activeSelf, Is.EqualTo(visible));
    }
    private GameObject Create(string name, Transform parent) => AuthoredRuntimeFixture.Create(scene, parent, name, true);
    private static T Read<T>(Object target, string field) where T : Object => AuthoredRuntimeFixture.Read<T>(target, field);
    private static void Bind(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Call(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
}
