using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SettlementUsabilityTests
{
    private Scene scene;
    private SettlementDefenseEncounterController encounter;
    private PlayerHealth health;
    private PlayerController2D movement;
    private Camera camera;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Ref<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object => RouteCoreDeckAuthoring.Ref<T>(owner, field);
    private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
    [SetUp] public void Open()
    {
        scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        health = Ref<PlayerHealth>(encounter, "player"); movement = health.GetComponent<PlayerController2D>();
        camera = Ref<Camera>(encounter, "deckCamera");
    }
    [TearDown] public void Close() => EditorSceneManager.ClosePreviewScene(scene);

    [Test] public void SavedHUDAndVisualBindingsAreCompleteAndIdempotent()
    {
        SettlementUsabilityAuthoring.Validate(scene);
        var canvas = Ref<GameObject>(encounter, "deckCanvas");
        var controller = health.GetComponent<PlayerWeaponController>();
        Assert.That(movement.AimVisualRoot.name, Is.EqualTo("AimVisualRoot"));
        Assert.That(controller.FirePoint.parent, Is.SameAs(movement.AimVisualRoot));
        Assert.That(health.GetComponent<Rigidbody2D>().freezeRotation, Is.True);
        Assert.That(Ref<PlayerWeaponController>(canvas.GetComponentInChildren<WeaponHeatUI>(true), "weaponController"), Is.SameAs(controller));
        Assert.That(Ref<PlayerWeaponController>(canvas.GetComponentInChildren<PlayerChargeGaugeUI>(true), "weaponController"), Is.SameAs(controller));
        var follower = canvas.GetComponentInChildren<WorldGaugeFollower>(true);
        Assert.That(Ref<Camera>(follower, "worldCamera"), Is.SameAs(camera));
        Assert.That(follower.transform.IsChildOf(movement.AimVisualRoot), Is.False);
        var followData = new SerializedObject(follower);
        Assert.That(followData.FindProperty("anchorToTargetTop").boolValue, Is.False, "Weapon effects must not move the gauge anchor.");
        Assert.That(followData.FindProperty("worldOffset").vector3Value, Is.EqualTo(new Vector3(0, 1.1f, 0)));
        Assert.That(follower.gameObject.layer, Is.EqualTo(canvas.layer));
        Assert.That(canvas.GetComponentInChildren<ExpeditionHUD>(true), Is.Null);
        int count = canvas.GetComponentsInChildren<Transform>(true).Length;
        SettlementUsabilityAuthoring.ApplyDeckHUD(scene);
        Assert.That(canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        SettlementUsabilityAuthoring.Validate(scene);
    }

    [Test] public void TemporaryAimCameraHasExclusiveOwnerAndDisableCleanup()
    {
        object owner = new object(), other = new object();
        Assert.That(movement.AcquireTemporaryAimCamera(null, camera), Is.False);
        Assert.That(movement.AcquireTemporaryAimCamera(owner, null), Is.False);
        Assert.That(movement.AcquireTemporaryAimCamera(owner, camera), Is.True);
        Assert.That(movement.AcquireTemporaryAimCamera(owner, camera), Is.True);
        Assert.That(movement.AcquireTemporaryAimCamera(other, camera), Is.False);
        movement.ReleaseTemporaryAimCamera(other);
        Assert.That(movement.HasTemporaryAimCamera(owner), Is.True);
        Call(movement, "OnDisable"); Assert.That(movement.HasTemporaryAimCamera(owner), Is.False);
    }

    [Test] public void CloseDeckReleasesAimOwnershipEvenWithoutActiveEncounter()
    {
        movement.AcquireTemporaryAimCamera(encounter, camera);
        typeof(SettlementDefenseEncounterController).GetMethod("CloseDeck", Private).Invoke(encounter, new object[] { false });
        Assert.That(movement.HasTemporaryAimCamera(encounter), Is.False);
    }

    [TestCase(1, 0)] [TestCase(-1, 0)] [TestCase(0, 1)] [TestCase(0, -1)]
    [TestCase(1, 1)] [TestCase(-1, -1)]
    public void ScreenAimUsesDeckCameraAndFallbackAfterRelease(float x, float y)
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        var texture = new RenderTexture(480, 270, 0);
        try
        {
            camera.orthographic = true; camera.orthographicSize = 4.21875f; camera.targetTexture = texture;
            Vector2 expected = (Vector2)movement.transform.position + new Vector2(x, y);
            Vector2 screen = camera.WorldToScreenPoint(expected);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); InputSystem.Update();
            Assert.That(movement.AcquireTemporaryAimCamera(this, camera), Is.True);
            Assert.That(movement.TryGetAimWorldPosition(out Vector2 actual), Is.True);
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.001f));
            movement.ReleaseTemporaryAimCamera(this);
            typeof(PlayerController2D).GetField("mainCamera", Private).SetValue(movement, camera);
            Assert.That(movement.TryGetAimWorldPosition(out actual), Is.True);
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.001f));
        }
        finally { InputSystem.RemoveDevice(mouse); camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(texture); }
    }

    [Test] public void VitalsEventsRefreshAndUnsubscribeWithoutPolling()
    {
        var hud = Ref<GameObject>(encounter, "deckCanvas").GetComponentInChildren<RouteCoreCombatHUD>(true);
        var hp = Ref<GaugeBarUI>(hud, "healthGauge"); var armorGauge = Ref<GaugeBarUI>(hud, "armorGauge");
        var armor = health.GetComponent<PlayerArmor>();
        Call(hud, "OnEnable"); health.SetMaxHp(40, true); health.RestoreCurrentHp(20);
        Assert.That(hp.Ratio, Is.EqualTo(.5f)); Assert.That(hp.ValueText.text, Is.EqualTo("HP 20 / 40"));
        armor.SetMaxArmor(10, true); armor.SetArmor(4);
        Assert.That(armorGauge.Ratio, Is.EqualTo(.4f)); Assert.That(armorGauge.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        armor.SetMaxArmor(0, true); Assert.That(armorGauge.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Call(hud, "OnDisable"); health.RestoreCurrentHp(10);
        Assert.That(hp.Ratio, Is.EqualTo(.5f)); Assert.That(hp.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Call(hud, "OnEnable"); Assert.That(hp.Ratio, Is.EqualTo(.25f)); Call(hud, "OnDisable");
    }

    [TestCase(EquipmentDevelopmentResult.Success, false, false, SoundEventIds.UiUnlock)]
    [TestCase(EquipmentDevelopmentResult.Success, true, false, SoundEventIds.UiActivate)]
    [TestCase(EquipmentDevelopmentResult.Success, true, true, SoundEventIds.UiDeactivate)]
    [TestCase(EquipmentDevelopmentResult.InsufficientResources, false, false, SoundEventIds.UiInsufficient)]
    [TestCase(EquipmentDevelopmentResult.ResearchLocked, false, false, SoundEventIds.UiDisabled)]
    [TestCase(EquipmentDevelopmentResult.UnsafeState, true, false, SoundEventIds.UiDisabled)]
    [TestCase(EquipmentDevelopmentResult.InvalidRecipe, false, false, SoundEventIds.UiUpgradeFail)]
    [TestCase(EquipmentDevelopmentResult.SaveFailed, false, false, SoundEventIds.UiUpgradeFail)]
    public void ActionAudioFollowsResult(EquipmentDevelopmentResult result, bool manufactured, bool fitted, string expected)
        => Assert.That(ShipTraitTreePanel.EquipmentActionSound(result, manufactured, fitted), Is.EqualTo(expected));
}

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    [Test] public void EquipmentAudioAndInputAreAuthoredWithoutDuplicateTabOrActionClicks()
    {
        var controls = panel.GetComponentsInChildren<EquipmentDevelopmentInput>(true);
        Assert.That(controls.Length, Is.GreaterThan(18));
        foreach (var input in controls)
        {
            Assert.That(Get<ShipTraitTreePanel>(input, "owner"), Is.SameAs(panel));
            var sound = input.GetComponent<UISoundButton>();
            Assert.That(Get<bool>(sound, "playSelectionHover"), Is.True);
            Assert.That(Get<string>(sound, "hoverSoundEventId"), Is.EqualTo(SoundEventIds.UiHover));
            var tab = input.GetComponent<ShipTraitBranchTabButton>();
            if (tab != null) Assert.That(Get<bool>(tab, "playClickSound"), Is.False);
        }
        foreach (var view in Get<PreparedEquipmentView[]>(panel, "equipmentSlots").Concat(Get<PreparedEquipmentView[]>(panel, "equipmentCandidates")))
            Assert.That(Get<string>(view.button.GetComponent<UISoundButton>(), "clickSoundEventId"), Is.EqualTo(SoundEventIds.TraitSelect));
        Assert.That(Get<bool>(Get<Button>(panel, "equipmentActivationButton").GetComponent<UISoundButton>(), "playClick"), Is.False);
        var ids = controls.Select(c => c.GetInstanceID()).ToArray();
        EquipmentDevelopmentInstaller.AuthorInputAndSound(panel);
        Assert.That(panel.GetComponentsInChildren<EquipmentDevelopmentInput>(true).Select(c => c.GetInstanceID()), Is.EqualTo(ids));
    }

    [Test] public void EquipmentNavigationRebuildsOnlyVisibleControlsAndInspectionNeverSpends()
    {
        OpenEquipment(); ResearchAndResources(); panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        var first = Get<PreparedEquipmentView[]>(panel, "equipmentSlots")[0];
        panel.InspectEquipment(first.definition);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        foreach (var control in Get<System.Collections.Generic.List<Selectable>>(panel, "visibleTraitControls"))
        {
            Assert.That(control.IsActive() && control.IsInteractable(), Is.True);
            Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            foreach (var next in new[] { control.navigation.selectOnUp, control.navigation.selectOnDown, control.navigation.selectOnRight })
                Assert.That(next.IsActive() && next.IsInteractable(), Is.True);
        }
        panel.SelectEquipmentBranch(ShipTraitBranchKind.MachineGun);
        Assert.That(Get<System.Collections.Generic.List<Selectable>>(panel, "visibleTraitControls").Contains(Get<Button>(panel, "researchSpecialEquipmentButton")), Is.False);
        Assert.That(panel.InspectedEquipment, Is.Null);
    }
}
