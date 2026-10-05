using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RouteCoreDeckAuthoringTests
{
    private Scene scene;
    private SettlementDefenseEncounterController encounter;
    private SettlementRouteCoreController route;
    private GameObject deck;
    private T Ref<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object => RouteCoreDeckAuthoring.Ref<T>(owner, field);
    [OneTimeSetUp] public void Open()
    {
        scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
        encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        route = RouteCoreDeckAuthoring.Single<SettlementRouteCoreController>(scene);
        deck = Ref<GameObject>(encounter, "deckRoot");
    }
    [OneTimeTearDown] public void Close() { EditorSceneManager.ClosePreviewScene(scene); }

    [Test] public void ExistingOwnersBindingsAndReturnVisibilityHookRemainIntact()
    {
        RouteCoreDeckAuthoring.Validate(scene);
        Assert.That(route.transform.parent, Is.EqualTo(deck.transform));
        Assert.That(route.name, Is.EqualTo("RouteCoreRoot"));
        Assert.That(deck.activeSelf, Is.False);
        Assert.That(Ref<GameObject>(encounter, "deckCanvas").activeSelf, Is.False);
        Assert.That(Ref<SettlementRouteCoreController>(encounter, "routeCore"), Is.SameAs(route));
        Assert.That(route.DefenseRequested.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(route.DefenseRequested.GetPersistentTarget(0), Is.SameAs(encounter));
        Assert.That(route.DefenseRequested.GetPersistentMethodName(0), Is.EqualTo("BeginEncounter"));
        var back = Ref<GameObject>(encounter, "facilityNavigation").GetComponent<Button>();
        Assert.That(back.onClick.GetPersistentMethodName(0), Is.EqualTo("LeaveDeck"));
        Assert.That(back.onClick.GetPersistentTarget(0), Is.SameAs(encounter));
        var rect = back.GetComponent<RectTransform>();
        Assert.That(Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x / 2, Is.LessThanOrEqualTo(236));
        Assert.That(Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y / 2, Is.LessThanOrEqualTo(131));
    }
    [Test] public void CameraAndStartRetainOpenPaddedSpace()
    {
        var camera = Ref<Camera>(encounter, "deckCamera");
        Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0, 0, -10)));
        Vector3 start = Ref<Transform>(encounter, "playerStart").position;
        Assert.That(start, Is.EqualTo(RouteCoreDeckAuthoring.Start));
        Assert.That(Ref<PlayerHealth>(encounter, "player").transform.position, Is.EqualTo(start));
        Assert.That(Vector3.Distance(start, route.transform.position), Is.GreaterThan(2));
        Assert.That(route.transform.position, Is.EqualTo(RouteCoreDeckAuthoring.Center));
        Assert.That(Ref<SpriteRenderer>(encounter, "centralVisual").transform, Is.EqualTo(route.transform.Find("Activated")));
        foreach (Vector3 point in new[] { start, route.transform.position })
        {
            Assert.That(Mathf.Abs(point.x), Is.LessThan(6));
            Assert.That(Mathf.Abs(point.y), Is.LessThan(2.75f));
        }
    }
    [TestCase(0, BossStoryPart.SectorStabilizer, 70)]
    [TestCase(1, BossStoryPart.PhaseNavigationLens, 70)]
    [TestCase(2, BossStoryPart.MatterCompressor, 90)]
    public void CombatOrderHealthAndCanonicalIdentities(int index, BossStoryPart part, float hp)
    {
        var array = new SerializedObject(encounter).FindProperty("componentCores");
        Assert.That(array.arraySize, Is.EqualTo(3));
        var record = array.GetArrayElementAtIndex(index);
        Assert.That(record.FindPropertyRelative("part").intValue, Is.EqualTo((int)part));
        Assert.That(record.FindPropertyRelative("hp").floatValue, Is.EqualTo(hp));
        Assert.That(record.FindPropertyRelative("color").colorValue, Is.EqualTo(RouteCoreDeckAuthoring.Identities[index]));
        var point = (Transform)record.FindPropertyRelative("combatPoint").objectReferenceValue;
        Assert.That(point.position, Is.EqualTo(RouteCoreDeckAuthoring.Stations[index]));
        Assert.That(Mathf.Abs(point.position.x) + .75f, Is.LessThan(6.5f));
        Assert.That(Mathf.Abs(point.position.y) + .75f, Is.LessThan(3));
        var station = deck.transform.Find("DeckEnvironment/ComponentStations/" + RouteCoreDeckAuthoring.StationNames[index]);
        Assert.That(station.position, Is.EqualTo(point.position));
        Assert.That(station.Find("IdentityLampLeft").GetComponent<SpriteRenderer>().color, Is.EqualTo(RouteCoreDeckAuthoring.Identities[index]));
        Assert.That(station.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        foreach (Vector3 other in RouteCoreDeckAuthoring.Stations.Where(p => p != point.position))
            Assert.That(Vector3.Distance(other, point.position), Is.GreaterThan(3));
    }
    [Test] public void DecorativeCollidersAndPermanentDuplicateCombatActorsAreAbsent()
    {
        Assert.That(route.GetComponent<Collider2D>().isTrigger, Is.True);
        var player = Ref<PlayerHealth>(encounter, "player");
        foreach (Collider2D collider in deck.GetComponentsInChildren<Collider2D>(true))
            Assert.That(collider.transform == route.transform || collider.transform.IsChildOf(player.transform) ||
                collider.transform.IsChildOf(Ref<SettlementDefensePurpleCore>(encounter, "purpleCore").transform), Is.True, collider.name);
        Assert.That(deck.GetComponentsInChildren<SettlementDefenseCorruptedCore>(true), Is.Empty);
        var prefab = Ref<SettlementDefenseCorruptedCore>(encounter, "corePrefab");
        Assert.That(prefab.HasAuthoredBindings, Is.True);
        Assert.That(Ref<SpriteRenderer>(prefab, "visual").sprite.name, Is.EqualTo("core1"));
        Assert.That(Ref<Collider2D>(prefab, "combatCollider"), Is.Not.SameAs(Ref<Collider2D>(prefab, "interactionCollider")));
    }
    [TestCase("Missing", "core7", 0)]
    [TestCase("Ready", "core6", 3)]
    [TestCase("Assembled", "core5", 3)]
    [TestCase("Activated", "core9", 3)]
    public void ProgressionRootsOwnDistinctHousingPowerAndConnections(string name, string sprite, int lamps)
    {
        Transform root = route.transform.Find(name);
        Assert.That(root.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo(sprite));
        Assert.That(root.Cast<Transform>().Count(t => t.name.StartsWith("ReadyLamp_")), Is.EqualTo(lamps));
        Assert.That(root.Find("Energy").GetComponent<SpriteRenderer>().color.b, Is.EqualTo(1));
        Assert.That(root.Find("Conduits").childCount, Is.GreaterThan(6));
        Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            Assert.That(renderer.sortingOrder, Is.LessThan(2), "Nonblocking central art must not obscure enemy projectiles.");
    }
    [Test] public void ActualProgressSelectsOneRootAndClearedDeckHasNoCorruption()
    {
        var previous = PermanentProgress.Instance;
        var holder = new GameObject("Read-only deck state fixture"); holder.SetActive(false);
        var progress = holder.AddComponent<PermanentProgress>();
        try
        {
            typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
            CheckState("Missing");
            progress.TryStartDamagedAccessKeyQuest();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            CheckState("Ready");
            Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True); CheckState("Assembled");
            Assert.That(progress.TryActivateRouteCore(), Is.True); CheckState("Activated");
            progress.MarkSettlementDefenseCleared(); CheckState("Activated");
            Assert.That(Ref<SpriteRenderer>(encounter, "corruptionWave").gameObject.activeSelf, Is.False);
            Assert.That(progress.CanLaunchFinalExpedition, Is.True);
        }
        finally { typeof(PermanentProgress).GetProperty("Instance").SetValue(null, previous); UnityEngine.Object.DestroyImmediate(holder); }
    }
    private void CheckState(string expected)
    {
        typeof(SettlementRouteCoreController).GetMethod("RefreshVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(route, null);
        foreach (string name in new[] { "Missing", "Ready", "Assembled", "Activated" })
            Assert.That(route.transform.Find(name).gameObject.activeSelf, Is.EqualTo(name == expected));
    }
    [Test] public void WorldArtAndAuthoringAreStableWithoutTouchingManagementUI()
    {
        Transform canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
        string[] ui = canvas.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => EditorJsonUtility.ToJson(c)).ToArray();
        var owners = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
        string[] bindings = owners.Select(c => EditorJsonUtility.ToJson(c)).ToArray();
        int[] ids = deck.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()).ToArray();
        RouteCoreDeckAuthoring.Apply(scene); RouteCoreDeckAuthoring.Apply(scene);
        Assert.That(deck.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()), Is.EqualTo(ids));
        Assert.That(owners.Select(c => EditorJsonUtility.ToJson(c)), Is.EqualTo(bindings));
        Assert.That(canvas.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => EditorJsonUtility.ToJson(c)), Is.EqualTo(ui));
        foreach (SpriteRenderer art in deck.transform.Find("DeckEnvironment").GetComponentsInChildren<SpriteRenderer>(true))
            Assert.That(AssetDatabase.GetAssetPath(art.sprite), Does.StartWith(RouteCoreDeckAuthoring.ArtRoot));
    }
}
