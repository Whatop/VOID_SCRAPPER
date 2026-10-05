using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class HudHangarLayoutTests
{
    private Scene scene;
    private static T Ref<T>(Object o, string field) where T : Object => RouteCoreDeckAuthoring.Ref<T>(o, field);
    [SetUp] public void Open() => scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
    [TearDown] public void Close() => EditorSceneManager.ClosePreviewScene(scene);
    [Test] public void CombatStatusKeepsGaugesGroupedAndBound()
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var root = Ref<GameObject>(encounter, "deckCanvas").transform;
        var status = root.Find("CombatStatus").GetComponent<RouteCoreCombatHUD>();
        Assert.That(Ref<PlayerHealth>(status, "playerHealth"), Is.SameAs(Ref<PlayerHealth>(encounter, "player")));
        Assert.That(Ref<PlayerArmor>(status, "playerArmor"), Is.Not.Null);
        foreach (string field in new[] { "healthGauge", "armorGauge" }) Assert.That(Ref<GaugeBarUI>(status, field).transform.parent, Is.SameAs(status.transform));
        var hp = Ref<GaugeBarUI>(status, "healthGauge"); Assert.That(hp.ValueText.transform.IsChildOf(status.transform), Is.True);
        Assert.That(hp.FillImage.rectTransform.rect.width, Is.EqualTo(96));
        Assert.That(status.transform.Find("Background").GetComponent<Image>().sprite.name, Is.EqualTo("32"));
        var rect = (RectTransform)status.transform;
        Assert.That(rect.anchoredPosition.x - rect.rect.width / 2, Is.GreaterThanOrEqualTo(-220));
        Assert.That(rect.anchoredPosition.y + rect.rect.height / 2, Is.LessThanOrEqualTo(118));
    }
    [Test] public void ObjectiveAndInteractionHaveSeparateSafeBounds()
    {
        var hud = RouteCoreDeckAuthoring.Single<SettlementHUD>(scene);
        var objective = Ref<TMP_Text>(hud, "campaignDeckMessageText");
        var panel = (RectTransform)objective.transform.parent;
        Assert.That(panel.name, Is.EqualTo("ObjectiveMessage")); Assert.That(panel.anchoredPosition.x, Is.Zero);
        Assert.That(panel.anchoredPosition.y + panel.rect.height / 2, Is.LessThanOrEqualTo(115));
        Assert.That(panel.rect.width, Is.LessThanOrEqualTo(200));
        var interaction = Ref<TMP_Text>(RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene), "interactionText");
        Assert.That(interaction.rectTransform.anchoredPosition.y, Is.LessThan(-80));
    }
    [Test] public void ReturnKeepsPersistentCallbackAndApprovedNonWhiteStyle()
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var button = Ref<GameObject>(encounter, "facilityNavigation").GetComponent<Button>();
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(encounter)); Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("LeaveDeck"));
        Assert.That(AssetDatabase.GetAssetPath(((Image)button.targetGraphic).sprite), Is.EqualTo(DarkUISettlementAuthoring.Root + "Button_Small_A.png"));
        Assert.That(button.spriteState.selectedSprite, Is.Null); Assert.That(button.colors.selectedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(button.colors.pressedColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(((Image)button.targetGraphic).pixelsPerUnitMultiplier, Is.EqualTo(8));
    }
    [Test] public void DeckHeatStaysVisibleAtZeroChargeFollowsPlayerRadarUnchanged()
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var root = Ref<GameObject>(encounter, "deckCanvas");
        var heat = root.GetComponentInChildren<WeaponHeatUI>(true);
        Assert.That(heat.transform.parent.name, Is.EqualTo("CombatStatus")); Assert.That(new SerializedObject(heat).FindProperty("hideAtZeroHeat").boolValue, Is.False);
        Assert.That(heat.transform.Find("HeatLabel"), Is.Not.Null);
        var charge = root.GetComponentInChildren<PlayerChargeGaugeUI>(true);
        Assert.That(charge.transform.parent, Is.SameAs(root.transform));
        Assert.That(Ref<Transform>(charge, "playerTarget"), Is.SameAs(Ref<PlayerHealth>(encounter, "player").transform));
        Assert.That(Ref<WorldGaugeFollower>(charge, "follower"), Is.Not.Null);
        Assert.That(Ref<GameObject>(encounter, "radarPresentation").activeSelf, Is.False);
        Assert.That(Ref<TMP_Text>(encounter, "radarHint").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(183, 43)));
    }
    [Test] public void HangarAuthoredBaselineMatchesActualExpeditionAndUsesOneAccentMapping()
    {
        var baseline = Ref<PlayerRuntimeStatApplier>(RouteCoreDeckAuthoring.Single<SettlementController>(scene), "deploymentStatSource");
        var expedition = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/Expedition.unity");
        try
        {
            var runtime = RouteCoreDeckAuthoring.Single<PlayerRuntimeStatApplier>(expedition);
            foreach (string f in new[] { "baseMoveSpeed", "baseDashDistance", "baseDashCooldown", "baseMaxArmor", "baseStartingArmor", "applyTraitEffectsCumulatively" })
                Assert.That(new SerializedObject(baseline).FindProperty(f).boxedValue, Is.EqualTo(new SerializedObject(runtime).FindProperty(f).boxedValue), f);
            Assert.That(RouteCoreDeckAuthoring.Single<WeaponHeatUI>(expedition), Is.Not.Null);
        }
        finally { EditorSceneManager.ClosePreviewScene(expedition); }
        var hud = RouteCoreDeckAuthoring.Single<SettlementHUD>(scene);
        Assert.That(Ref<TMP_Text>(hud, "shipBodyText").richText, Is.True);
        Assert.That(Ref<ScrollRect>(hud, "shipDetailScroll").content, Is.SameAs(Ref<TMP_Text>(hud, "shipBodyText").rectTransform));
        var ships = RouteCoreDeckAuthoring.Single<SettlementController>(scene).ShipDefinitions;
        foreach (var ship in ships) Assert.That(ship.ResearchAccent, Is.EqualTo(ShipDefinition.WeaponAccent(ship.DefaultWeaponTree)));
        Assert.That(ShipDefinition.WeaponAccent(WeaponTreeType.MachineGun).g, Is.EqualTo(1));
        Assert.That(ShipDefinition.WeaponAccent(WeaponTreeType.Shotgun).r, Is.EqualTo(1));
        Assert.That(ShipDefinition.WeaponAccent(WeaponTreeType.Sniper).b, Is.EqualTo(1));
    }
    [Test] public void AuthoringIsIdempotentAndPreservesExistingObjects()
    {
        var before = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();
        HudHangarAuthoring.Apply(scene); HudHangarAuthoring.Apply(scene);
        Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.GetInstanceID()).OrderBy(i => i), Is.EqualTo(before));
    }
}
