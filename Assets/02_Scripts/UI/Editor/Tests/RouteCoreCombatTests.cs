using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RouteCoreCombatTests
{
    private Scene scene;
    private SettlementDefenseEncounterController encounter;
    private SettlementDefensePurpleCore purple;
    private PlayerRadarScanner scanner;
    private GameObject deck;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    [SetUp] public void Open()
    {
        scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        purple = Get<SettlementDefensePurpleCore>(encounter, "purpleCore");
        scanner = Get<PlayerRadarScanner>(encounter, "deckRadar"); deck = Get<GameObject>(encounter, "deckRoot");
    }
    [TearDown] public void Close() { encounter.CancelEncounter(); EditorSceneManager.ClosePreviewScene(scene); }

    [Test] public void SavedBindingsAndAuthoringPreserveIdsAndManagement()
    {
        RouteCoreCombatAuthoring.Validate(scene);
        var management = Get<GameObject>(encounter, "managementCanvas");
        var components = management.GetComponentsInChildren<Component>(true).Where(c => c != null).ToArray();
        var json = components.Select(c => EditorJsonUtility.ToJson(c)).ToArray();
        var ids = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();
        RouteCoreCombatAuthoring.Apply(scene); RouteCoreCombatAuthoring.Apply(scene);
        Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.GetInstanceID()).OrderBy(i => i), Is.EqualTo(ids));
        Assert.That(components.Select(c => EditorJsonUtility.ToJson(c)), Is.EqualTo(json));
        Assert.That(Get<GameObject>(encounter, "radarPresentation").activeSelf, Is.False);
        Assert.That(Get<RadarHUD>(scanner, "radarHUD"), Is.Not.Null);
        Assert.That(Get<bool>(scanner, "enablePassiveRadar"), Is.False);
        Assert.That(Get<float>(scanner, "scanCooldown"), Is.EqualTo(.5f));
    }

    [TestCase("fanCount", 5)] [TestCase("volleyCount", 2)]
    [TestCase("sniperShots", 3)] [TestCase("machineGunShots", 10)]
    public void ProductionWeaponCounts(string field, int value)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RouteCoreCombatAuthoring.CorePrefab).GetComponent<SettlementDefenseCorruptedCore>();
        Assert.That(Get<int>(prefab, field), Is.EqualTo(value));
    }

    [TestCase("shotgunTelegraph", .65f)] [TestCase("volleyInterval", .24f)]
    [TestCase("shotgunRecovery", 1.4f)] [TestCase("fanSpread", 64f)]
    [TestCase("sniperAim", .65f)] [TestCase("sniperLock", .3f)] [TestCase("sniperSpeed", 14f)]
    [TestCase("machineGunInterval", .13f)] [TestCase("machineGunRecovery", 1.8f)]
    public void ProductionPatternTimingAndGeometry(string field, float value)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RouteCoreCombatAuthoring.CorePrefab).GetComponent<SettlementDefenseCorruptedCore>();
        Assert.That(Get<float>(prefab, field), Is.EqualTo(value));
        Assert.That(Get<LineRenderer>(prefab, "telegraph").enabled, Is.False);
        Assert.That(Get<ProjectileDefinition>(prefab, "projectile").Damage, Is.EqualTo(2));
    }
    [Test] public void PurpleHasSeparateNonBlockingDetectionNoRewardAndModestTuning()
    {
        Assert.That(purple.HasAuthoredBindings, Is.True);
        Assert.That(Get<Collider2D>(purple, "radarCollider").GetComponentInParent<EnemyHealth>(true), Is.Null);
        Assert.That(Get<Collider2D>(purple, "radarCollider").isTrigger, Is.True);
        Assert.That(Get<Collider2D>(purple, "damageCollider").isTrigger, Is.True);
        Assert.That(purple.GetComponentInChildren<RewardDropper>(true), Is.Null);
        Assert.That(purple.GetComponentInChildren<BossDummyController>(true), Is.Null);
        Assert.That(Get<bool>(purple.Health, "dropRewardOnDeath"), Is.False);
        Assert.That(Get<bool>(purple.Health, "releaseOnDeath"), Is.False);
        Assert.That(Get<float>(purple, "maxHp"), Is.EqualTo(50));
        Assert.That(Get<float>(purple, "exposureDuration"), Is.EqualTo(4));
        Assert.That(Get<float>(purple, "openingGrace"), Is.EqualTo(1));
        Assert.That(Get<int>(purple, "pulseCount"), Is.EqualTo(8));
        Assert.That(purple.Target.ShowOnMap, Is.False);
    }
    private void Begin()
    {
        deck.SetActive(true);
        Assert.That(purple.Begin(encounter, scanner), Is.True);
    }
    private void Scan() => Call(purple, "HandleScan", Vector2.zero, 22f, new[] { purple.Target });

    [Test] public void HiddenBlocksDirectDamageButRemainsDetectable()
    {
        Begin(); purple.Health.TakeDamage(10000);
        Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Hidden));
        Assert.That(purple.Health.CurrentHp, Is.EqualTo(50));
        Assert.That(Get<Collider2D>(purple, "damageCollider").enabled, Is.False);
        Assert.That(Get<Collider2D>(purple, "radarCollider").enabled && purple.Target.IsRadarVisible, Is.True);
    }
    [Test] public void OnlySuccessfulActiveScanContainingTargetExposesAndRescansDoNotStack()
    {
        Begin();
        purple.Target.SetMapDiscovered(true); // Passive/map state is not an exposure trigger.
        Assert.That(purple.ExposureCount, Is.Zero);
        Call(purple, "HandleScan", Vector2.zero, 22f, Array.Empty<RadarTarget>());
        Assert.That(purple.ExposureCount, Is.Zero);
        Scan(); Scan(); Scan();
        Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Exposed));
        Assert.That(purple.ExposureCount, Is.EqualTo(1));
        Assert.That(Get<Collider2D>(purple, "damageCollider").enabled, Is.True);
        purple.Health.TakeDamage(10);
        Assert.That(purple.Health.CurrentHp, Is.EqualTo(40));
        Call(purple, "Hide", false);
        purple.Health.TakeDamage(10000);
        Assert.That(purple.Health.CurrentHp, Is.EqualTo(40), "Re-hide preserves remaining HP and rejects damage.");
        Scan(); Assert.That(purple.ExposureCount, Is.EqualTo(2));
    }
    [TestCase(false)] [TestCase(true)]
    public void CancelOrDisableRemovesScanSubscriptionTargetAndDamage(bool disable)
    {
        Begin(); Scan();
        if (disable) Call(purple, "OnDisable"); else purple.Cancel();
        Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Cancelled));
        Assert.That(Get<Delegate>(scanner, "ScanCompleted")?.GetInvocationList().Any(d => ReferenceEquals(d.Target, purple)) ?? false, Is.False);
        Assert.That(Get<Collider2D>(purple, "radarCollider").enabled, Is.False);
        Assert.That(Get<Collider2D>(purple, "damageCollider").enabled, Is.False);
        Assert.That(purple.Target.IsRadarVisible, Is.False);
        Scan(); Assert.That(purple.State, Is.EqualTo(SettlementDefensePurpleCore.Phase.Cancelled));
    }
    [Test] public void ThirdFusionStartsPurpleOnceAndCleanupRestoresExactEnvironment()
    {
        deck.SetActive(true);
        var renderers = Get<SpriteRenderer[]>(encounter, "blackoutRenderers");
        var colors = renderers.Select(r => r.color).ToArray();
        Set(encounter, "active", true); Set(encounter, "fusedCount", 3);
        scanner.SetExternalInputLocked(encounter, true);
        Assert.That(scanner.TryToggleRadarMode(), Is.False);
        Call(encounter, "CompleteEncounter"); Assert.That(encounter.IsActive, Is.True);
        Call(encounter, "StartPurplePhase"); Call(encounter, "StartPurplePhase");
        Assert.That(encounter.IsPurpleActive, Is.True);
        Assert.That(Get<Delegate>(scanner, "ScanCompleted").GetInvocationList().Count(d => ReferenceEquals(d.Target, purple)), Is.EqualTo(1));
        for (int i = 0; i < renderers.Length; i++) Assert.That(renderers[i].color.r, Is.EqualTo(colors[i].r * .25f).Within(.0001));
        encounter.CancelEncounter();
        Assert.That(renderers.Select(r => r.color), Is.EqualTo(colors));
        Assert.That(scanner.IsRadarOpen, Is.False);
        Assert.That(scanner.TryToggleRadarMode(), Is.False);
        Assert.That(purple.gameObject.activeSelf, Is.False);
    }
    [Test] public void BlackoutExcludesPlayerAndUsesNoMaterialsOrGlobalRadarChanges()
    {
        var player = Get<PlayerHealth>(encounter, "player");
        foreach (var renderer in Get<SpriteRenderer[]>(encounter, "blackoutRenderers"))
            Assert.That(renderer.transform.IsChildOf(player.transform) || renderer.transform.IsChildOf(purple.transform), Is.False);
        Assert.That(Get<float>(encounter, "hiddenEnvironmentVisibility"), Is.EqualTo(.25f));
        Assert.That(Get<float>(encounter, "exposedEnvironmentVisibility"), Is.EqualTo(.45f));
        Assert.That(Get<TMP_Text>(encounter, "radarHint").fontSize, Is.GreaterThanOrEqualTo(8));
    }
}
