using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ExpeditionUIAuthoringTests
{
    private Scene scene;
    private Scene userScene;
    private bool userDirty;
    private int[] userRoots;
    private GameObject canvas;
    private ExpeditionHUD hud;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene(); userDirty = userScene.isDirty;
        userRoots = AuthoredRuntimeFixture.RootIds(userScene);
        scene = AuthoredRuntimeFixture.Open("Expedition");
        hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(scene);
        canvas = hud.gameObject;
    }

    [TearDown]
    public void TearDown()
    {
        AuthoredRuntimeFixture.Close(scene);
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(AuthoredRuntimeFixture.RootIds(userScene), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    private GameObject Child(string name, Transform parent = null) => AuthoredRuntimeFixture.Create(scene, parent != null ? parent : canvas.transform, name);
    private int Count() => canvas.GetComponentsInChildren<Transform>(true).Length;

    [Test]
    public void StoryRecovery_UnpackedExpeditionInventoryHasThreeBoundReadOnlySlots()
    {
        PlayerBuildStatusPanelUI panel = AuthoredRuntimeFixture.Single<PlayerBuildStatusPanelUI>(scene);
        SerializedObject data = new SerializedObject(panel);
        SerializedProperty slots = data.FindProperty("storyRecoverySlots");
        Assert.That(slots.arraySize, Is.EqualTo(3));
        TMP_Text title = (TMP_Text)data.FindProperty("storyRecoveryTitle").objectReferenceValue;
        Assert.That(title, Is.Not.Null);
        Assert.That(title.transform.parent.name, Is.EqualTo("StoryRecoverySection"));
        Assert.That(title.transform.parent.parent.name, Is.EqualTo("InventoryRoot"));
        Assert.That(title.transform.parent.GetComponentsInChildren<Selectable>(true), Is.Empty);
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            Assert.That(slot.FindPropertyRelative("part").intValue, Is.EqualTo(i + 1));
            Assert.That(slot.FindPropertyRelative("definition").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("root").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("iconImage").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("nameText").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("statusText").objectReferenceValue, Is.Not.Null);
        }
    }
    private static object Call(object target, string method, params object[] args)
    {
        MethodInfo match = target.GetType().GetMethods(Private).Single(m => m.Name == method && m.GetParameters().Length == args.Length);
        return match.Invoke(target, args);
    }

    private ReinforcementSlotUI AuthoredSlot() => AuthoredRuntimeFixture.Read<ReinforcementSlotUI>(hud, "reinforcementSlotUI");

    [TestCase(0, .0f, false)]
    [TestCase(1, .5f, false)]
    [TestCase(2, 1f, true)]
    public void Reinforcement_StateRefreshHasNoLayoutDriftOrSliderAction(int charges, float ratio, bool usable)
    {
        ReinforcementDefinition definition = ScriptableObject.CreateInstance<ReinforcementDefinition>();
        try
        {
            SerializedObject data = new SerializedObject(definition);
            data.FindProperty("effects").arraySize = 1; data.ApplyModifiedProperties();
            ReinforcementSlotUI slot = AuthoredSlot();
            Image icon = AuthoredRuntimeFixture.Read<Image>(slot, "iconImage");
            icon.preserveAspect = false;
            RectTransform[] geometry = slot.GetComponentsInChildren<RectTransform>(true);
            TMP_Text label = AuthoredRuntimeFixture.Read<TMP_Text>(slot, "keyText"); label.fontSize = 8.5f;
            Slider legacy = Child("HiddenLegacySlider", slot.transform).AddComponent<Slider>();
            legacy.minValue = 10; legacy.maxValue = 20;
            Assign(slot, "rechargeSlider", legacy);
            string[] baseline = geometry.Select(EditorJsonUtility.ToJson).ToArray();
            int calls = 0; legacy.onValueChanged.AddListener(_ => calls++);
            int count = Count();
            for (int i = 0; i < 4; i++)
            {
                slot.SetEmpty();
                slot.SetState(definition, charges, 2, ratio, ratio, !usable, usable, usable, ratio);
                slot.SetKeyLabel("Test binding");
            }
            Assert.That(calls, Is.Zero);
            Assert.That(legacy.minValue, Is.EqualTo(10));
            Assert.That(legacy.maxValue, Is.EqualTo(20));
            Assert.That(label.fontSize, Is.EqualTo(8.5f));
            Assert.That(icon.preserveAspect, Is.False);
            Assert.That(geometry.Select(EditorJsonUtility.ToJson).ToArray(), Is.EqualTo(baseline));
            Assert.That(Count(), Is.EqualTo(count));
            Assert.That(AuthoredRuntimeFixture.Read<Image>(slot, "activeDurationFillImage").fillAmount, Is.EqualTo(ratio));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void Reinforcement_HudSubscriptionsAreIdempotentAndDetachFromActualPublisher()
    {
        try
        {
            AuthoredSlot();
            PlayerReinforcementController publisher = AuthoredRuntimeFixture.Read<PlayerReinforcementController>(hud, "reinforcementController");
            string[] events = { "EquipmentChanged", "ChargesChanged", "Used", "ActiveTimedStatusesChanged" };
            for (int i = 0; i < 3; i++)
            {
                Call(hud, "Subscribe"); Call(hud, "Subscribe");
                Call(hud, "UpdateReinforcementSlot");
                foreach (string name in events)
                {
                    Delegate callbacks = typeof(PlayerReinforcementController).GetField(name, Private).GetValue(publisher) as Delegate;
                    Assert.That(callbacks.GetInvocationList().Count(item => item.Target == hud), Is.EqualTo(1), name);
                }
                Call(hud, "Unsubscribe");
            }
            Call(hud, "Subscribe");
            Assign(hud, "reinforcementController", Child("ReplacementPublisher").AddComponent<PlayerReinforcementController>());
            Call(hud, "Unsubscribe");
            foreach (string name in events)
            {
                Delegate callbacks = typeof(PlayerReinforcementController).GetField(name, Private).GetValue(publisher) as Delegate;
                Assert.That(callbacks == null || callbacks.GetInvocationList().All(item => item.Target != hud), Is.True, name);
            }
        }
        finally { Call(hud, "Unsubscribe"); }
    }

    private static void Assign(Object owner, string field, Object value)
    {
        SerializedObject so = new SerializedObject(owner);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private RadarHUD TutorialStatusFixture()
    {
        AuthoredRuntimeFixture.Close(scene);
        scene = AuthoredRuntimeFixture.Open("Tutorial");
        hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(scene); canvas = hud.gameObject;
        return AuthoredRuntimeFixture.Single<RadarHUD>(scene);
    }

    [Test]
    public void TutorialStatus_RadarScopeStaysAuthoredAndContactsAreDynamicWithoutDuplicates()
    {
        RadarHUD radar = TutorialStatusFixture();
        RadarScopeGraphic scope = AuthoredRuntimeFixture.Read<RadarScopeGraphic>(radar, "scopeGraphic");
        string style = EditorJsonUtility.ToJson(scope);
        GameObject targetObject = AuthoredRuntimeFixture.Create(scene, null, "DynamicTarget");
        RadarTarget target = targetObject.AddComponent<RadarTarget>();
        target.SetVisible(true);
        RadarTarget[] targets = { target };
        radar.SetTargets(targets, canvas.transform);
        int count = radar.GetComponentsInChildren<RadarMarkerUI>(true).Length;
        for (int i = 0; i < 3; i++)
        {
            radar.SetTargets(targets, canvas.transform);
            radar.SetSuccessfulScanPresentation(i % 2 == 0);
        }
        Assert.That(count, Is.EqualTo(1), "One data-driven contact; the authored marker source remains a prefab.");
        Assert.That(radar.GetComponentsInChildren<RadarMarkerUI>(true).Length, Is.EqualTo(count));
        Assert.That(radar.GetComponentsInChildren<RadarScopeGraphic>(true).Length, Is.EqualTo(1));
        Assert.That(EditorJsonUtility.ToJson(scope), Is.EqualTo(style));
    }

    [Test]
    public void Status_AdoptsExistingControlsAndRefreshDoesNotConstructOrRestyle()
    {

        GaugeBarUI hp = AuthoredRuntimeFixture.Read<GaugeBarUI>(hud, "hpGauge");
        ((RectTransform)hp.transform).anchoredPosition = new Vector2(23, 45);
        TextMeshProUGUI cargoText = AuthoredRuntimeFixture.Read<TextMeshProUGUI>(hud, "cargoValueText"); cargoText.fontSize = 14;
        int count = Count();

        foreach (string method in new[] { "EnsureSharedStatusPresentation", "EnsureCargoPresentation", "EnsureCoreTrackingPresentation", "EnsureMenuHintPresentation" }) Call(hud, method);
        Call(hud, "RefreshObjectiveProgress", 1, 2);
        Call(hud, "UpdateCargoDisplay", 0, 15);
        Assert.That(Count(), Is.EqualTo(count));
        Assert.That(cargoText.fontSize, Is.EqualTo(14));
        Assert.That(((RectTransform)hp.transform).anchoredPosition, Is.EqualTo(new Vector2(23, 45)));
        Assert.That(AuthoredRuntimeFixture.Read<TextMeshProUGUI>(hud, "coreSignalCountText").text,
            Is.EqualTo(string.Format(new SerializedObject(hud).FindProperty("coreSignalCountFormat").stringValue, 1, 2)));
        Assert.That(cargoText.text, Is.EqualTo(string.Format(new SerializedObject(hud).FindProperty("cargoValueFormat").stringValue, 0, 15)));

    }

    [Test]
    public void MissingOperation_FailsOnceWithoutReplacementOrOwnershipChange()
    {
        Assign(hud, "operationRoot", null);
        int count = Count();
        LogAssert.Expect(LogType.Warning, new Regex("Authored operation briefing bindings.*operationRoot.*Inspector binding"));
        Assert.That(Call(hud, "TryPrepareOperationPresentation"), Is.False);
        Assert.That(Call(hud, "TryPrepareOperationPresentation"), Is.False);
        Assert.That(hud.IsCinematicMode, Is.False);
        Assert.That(Count(), Is.EqualTo(count));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Radar_AuthoredGraphicHasPersistentIdentityAndNoReplacementOnInitialization()
    {
        RadarHUD radar = AuthoredRuntimeFixture.Single<RadarHUD>(scene);
        RadarScopeGraphic scope = AuthoredRuntimeFixture.Read<RadarScopeGraphic>(radar, "scopeGraphic");
        Assert.That(MonoScript.FromMonoBehaviour(scope).GetClass(), Is.EqualTo(typeof(RadarScopeGraphic)));
        int count = Count();
        Call(radar, "EnsureScopePresentation"); Call(radar, "EnsureScopePresentation");
        Assert.That(Count(), Is.EqualTo(count));
    }

    [Test]
    public void BossTriad_UsesThreeRolesAndReleasesWithoutDestroyingAuthoredObjects()
    {
        BossHealthBarUI boss = AuthoredRuntimeFixture.Single<BossHealthBarUI>(scene);
        RectTransform triad = AuthoredRuntimeFixture.Read<RectTransform>(boss, "triadPresentationRoot");
        Image left = AuthoredRuntimeFixture.Read<Image>(boss, "triadPartFills.Array.data[0]");
        left.color = new Color(.2f, .3f, .4f, .5f);
        int count = Count();
        Assert.That(Call(boss, "TryPrepareTriadPresentation"), Is.True);
        Call(boss, "ReleaseTriadPresentation");
        Assert.That(triad, Is.Not.Null);
        Assert.That(triad.gameObject.activeSelf, Is.False);
        Assert.That(Count(), Is.EqualTo(count));
        Assert.That(left.color.a, Is.EqualTo(.5f));
    }

    [Test]
    public void Confirmation_AuthoredBindingsDoNotAcquireModalOwnershipAndMissingBindingsFailLocally()
    {
        ReturnChoiceUI confirmation = AuthoredRuntimeFixture.Single<ReturnChoiceUI>(scene);
        Assert.That(confirmation.IsOpen, Is.False);
        int count = Count();
        Assert.That(Count(), Is.EqualTo(count));
        SerializedObject so = new SerializedObject(confirmation);
        so.FindProperty("yesButton").objectReferenceValue = null; so.ApplyModifiedProperties();
        LogAssert.Expect(LogType.Warning, new Regex("Missing panelRoot.*Inspector binding"));
        MethodInfo open = typeof(ExpeditionTravelConfirmationUI).GetMethod("OpenModal", Private);
        open.Invoke(confirmation, new object[] { "Title", "Body", "Cancel", "Confirm" });
        open.Invoke(confirmation, new object[] { "Title", "Body", "Cancel", "Confirm" });
        Assert.That(confirmation.IsOpen, Is.False);
        Assert.That(Count(), Is.EqualTo(count));
    }

    [Test]
    public void SharedConsumersRemainAndRetiredSceneOnlyOwnersHaveNoProceduralHierarchy()
    {
        foreach (string path in new[] { "Boss/BossHealthBarUI.cs", "Core/ReturnChoiceUI.cs", "Shop/ShopTradeUI.cs", "Shop/ShopMaintenanceBayUI.cs", "UI/WarningMessageUI.cs" })
        {
            string source = File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts", path));
            Assert.That(source, Does.Not.Contain("new GameObject("), path);
            Assert.That(source, Does.Not.Contain("text.fontSize ="), path);
        }
        string hudSource = File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts/UI/ExpeditionHUD.cs"));
        Assert.That(hudSource, Does.Contain("private void EnsureWorldChargeGaugePresentation()"));
        foreach (string retired in new[] { "EnsureWeaponHeatPresentation", "EnsureReinforcementPresentation", "ApplyHealthVisualPolish", "ApplyCargoVisualPolish", "ApplySharedHudLayout", "CreateMenuKeyHint", "CreateCoreTrackingText" })
            Assert.That(hudSource, Does.Not.Contain(retired + "("), retired);
        Assert.That(File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts/UI/SharedOptionsMenuUI.cs")), Does.Contain("private void Build("));
        Assert.That(File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "02_Scripts/UI/StatusEffectHUDPresenter.cs")), Does.Contain("Instantiate(slotPrefab"));
    }

}
