using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyRosterFieldBaseTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private static EnemyDefinition Definition(string id) => EnemyRosterAudit.Definitions.Single(d => d.EnemyId == id);
    private static T Ref<T>(Object owner, string name) where T : Object => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static T Get<T>(object o, string field) => EnemyRosterRenderProbe.Get<T>(o, field);
    private static void Set(object o, string field, object value) => EnemyRosterRenderProbe.Set(o, field, value);
    private static object Call(object o, string method, params object[] values) => EnemyRosterRenderProbe.Call(o, method, values);
    private GameObject New(string name) { var g = new GameObject(name); objects.Add(g); return g; }
    [TearDown] public void Cleanup()
    {
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear(); Physics2D.SyncTransforms();
    }
    [Test] public void DefinitionIdsAreUniqueIncludingSeparatelySpawnedShopDefenses()
    {
        var all = EnemyRosterAudit.Definitions;
        Assert.That(all.Length, Is.EqualTo(13));
        Assert.That(all.Select(d => d.EnemyId).Distinct().Count(), Is.EqualTo(all.Length));
        Assert.That(all.All(d => !string.IsNullOrWhiteSpace(d.EnemyId)), Is.True);
    }
    [TestCase("basic_enemy", 8, 2.5f)]
    [TestCase("shotgun_enemy", 14, 2.5f)]
    [TestCase("charge_enemy", 12, 2.5f)]
    [TestCase("melee_charger", 14, 3.2f)]
    [TestCase("elite_machinegun", 42, 2.8f)]
    [TestCase("elite_shotgun", 38, 2.5f)]
    [TestCase("elite_charging", 40, 2.4f)]
    [TestCase("rival_harvester", 20, 3.3f)]
    [TestCase("scavenger", 12, 3.8f)]
    public void ProductionDefinitionsHaveCompleteDedicatedSpawnPathsAndPreservedStats(string id, float hp, float speed)
    {
        var d = Definition(id); var prefab = d.EnemyPrefab;
        Assert.That(prefab, Is.Not.Null, id);
        Assert.That(d.MaxHp, Is.EqualTo(hp)); Assert.That(d.MoveSpeed, Is.EqualTo(speed));
        Assert.That(Ref<EnemyDefinition>(prefab.GetComponent<EnemyBaseAI>(), "enemyDefinition"), Is.SameAs(d));
        Assert.That(Ref<EnemyDefinition>(prefab.GetComponent<EnemyAttackController>(), "enemyDefinition"), Is.SameAs(d));
        Assert.That(prefab.GetComponent<EnemyHealth>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<RadarTarget>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<EnemyRoleController>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<EnemyRoleSimulationGate>(), Is.Not.Null);
        Assert.That(prefab.GetComponents<Collider2D>().Any(c => c.enabled), Is.True);
        Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Enemy")));
        Assert.That(d.RewardDefinition, Is.Not.Null);
        Assert.That(Ref<GameObject>(prefab.GetComponent<RewardDropper>(), "rewardPickupPrefab"), Is.Not.Null);
        if (id == "melee_charger") Assert.That(prefab.GetComponent<EnemyMeleeChargeController2D>(), Is.Not.Null);
        else
        {
            Assert.That(d.ProjectileDefinition, Is.Not.Null); Assert.That(d.ProjectileDefinition.ProjectilePrefab.GetComponent<Bullet>(), Is.Not.Null);
            Assert.That(Ref<Transform>(prefab.GetComponent<EnemyAttackController>(), "firePoint"), Is.Not.Null);
        }
        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
    }
    [TestCase("elite_machinegun", EnemyRangedAttackPattern.MachineGunBurst, "Enemy_Elite 2")]
    [TestCase("elite_shotgun", EnemyRangedAttackPattern.StaggeredShotgun, "Enemy_Elite")]
    [TestCase("elite_charging", EnemyRangedAttackPattern.ChargingSplit, "Enemy_Elite 1")]
    public void ElitesUseExistingDistinctSilhouettesAndPatterns(string id, EnemyRangedAttackPattern pattern, string prefab)
    {
        var d = Definition(id); Assert.That(d.RangedAttackPattern, Is.EqualTo(pattern)); Assert.That(d.EnemyPrefab.name, Is.EqualTo(prefab));
        Assert.That(d.EnemyPrefab.GetComponent<SpriteRenderer>().sprite.rect.size, Is.EqualTo(new Vector2(64, 64)));
        if (pattern == EnemyRangedAttackPattern.ChargingSplit) Assert.That(d.SplitProjectileDefinition.ProjectilePrefab.GetComponent<Bullet>(), Is.Not.Null);
    }
    [TestCase("rival_harvester", EnemyRoleType.RivalHarvester, 14)]
    [TestCase("scavenger", EnemyRoleType.Scavenger, 12)]
    public void CargoRolesStaySeparateFromBasicCombatIdentity(string id, EnemyRoleType role, int capacity)
    {
        var d = Definition(id); Assert.That(d.EnemyType, Is.EqualTo(EnemyType.Basic));
        var actor = Object.Instantiate(d.EnemyPrefab); objects.Add(actor);
        var controller = actor.GetComponent<EnemyRoleController>();
        if (role == EnemyRoleType.RivalHarvester) controller.ConfigureAsRivalHarvester(new Bounds(Vector3.zero, Vector3.one * 100));
        else controller.ConfigureAsScavenger(new Bounds(Vector3.zero, Vector3.one * 100));
        Assert.That(controller.RoleType, Is.EqualTo(role)); Assert.That(controller.CargoHold.Capacity, Is.EqualTo(capacity));
        Assert.That(controller.CargoHold.CanStore(CurrencyType.TuningChips), Is.False);
    }
    [TestCase(EnemyRoleType.RivalHarvester)] [TestCase(EnemyRoleType.Scavenger)]
    public void ConcurrentActionsRemainCappedAndDisableReleasesOwnership(EnemyRoleType type)
    {
        var a = New("First role").AddComponent<EnemyRoleSimulationGate>();
        var b = New("Second role").AddComponent<EnemyRoleSimulationGate>();
        a.Configure(type, 12, 8, 26, 18, 8, 10, 1, 1); b.Configure(type, 12, 8, 26, 18, 8, 10, 1, 1);
        a.ForceActive(30); b.ForceActive(30);
        Assert.That(a.TryAcquireIrreversibleSlot(), Is.True); Assert.That(b.TryAcquireIrreversibleSlot(), Is.False);
        a.gameObject.SetActive(false); Call(a, "OnDisable");
        Assert.That(b.TryAcquireIrreversibleSlot(), Is.True); b.ReleaseIrreversibleSlot();
    }
    [Test] public void PreviewAndGraceCannotPerformIrreversibleActions()
    {
        var gate = New("Preview role").AddComponent<EnemyRoleSimulationGate>(); var player = New("Observed player");
        gate.Configure(EnemyRoleType.RivalHarvester, 12, 8, 26, 18, 8, 10, 1, 1);
        Set(gate, "player", player.transform); Set(gate, "enabledTime", Time.time);
        Set(gate, "forcedActiveUntil", -1f); Set(gate, "activeHoldUntil", -1f);
        Assert.That(gate.RefreshState(), Is.EqualTo(EnemyRoleSimulationLevel.Dormant));
        Set(gate, "enabledTime", Time.time - 20); player.transform.position = new Vector3(22, 0);
        Assert.That(gate.RefreshState(), Is.EqualTo(EnemyRoleSimulationLevel.Preview)); Assert.That(gate.TryAcquireIrreversibleSlot(), Is.False);
        player.transform.position = new Vector3(40, 0); Assert.That(gate.RefreshState(), Is.EqualTo(EnemyRoleSimulationLevel.Dormant));
    }
    [TestCase(EnemyRosterAuthoring.BaseA, 4, 3)] [TestCase(EnemyRosterAuthoring.BaseB, 3, 4)]
    public void AuthoredBasesHaveSafeBindingsAndMixedGuardComposition(string path, int defenders, int turrets)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); var b = root.GetComponent<FieldBaseController>(); var s = new SerializedObject(b);
        Assert.That(s.FindProperty("defenderZoneAnchors").arraySize, Is.EqualTo(defenders));
        Assert.That(s.FindProperty("turretSpawnPoints").arraySize, Is.EqualTo(turrets));
        var templates = s.FindProperty("defenders"); Assert.That(templates.arraySize, Is.EqualTo(2));
        Assert.That(((EnemyRoleController)templates.GetArrayElementAtIndex(0).objectReferenceValue).GetComponent<EnemyBaseAI>().EnemyDefinition.EnemyType, Is.EqualTo(EnemyType.Basic));
        Assert.That(((EnemyRoleController)templates.GetArrayElementAtIndex(1).objectReferenceValue).GetComponent<EnemyBaseAI>().EnemyDefinition.EnemyType, Is.EqualTo(EnemyType.Shotgun));
        Assert.That(Ref<FieldNpcObjective>(b, "captiveNpc").UseBaseRescueFlow, Is.True);
        Assert.That(Ref<HarvestObjectHealth>(b, "npcPrisonMachine"), Is.Not.Null);
        Assert.That(Ref<Transform>(b, "npcPortalInteractionPoint"), Is.Not.Null);
        Assert.That(Ref<GameObject>(b, "portalPrefab"), Is.Not.Null);
        Assert.That(Ref<Collider2D>(b, "resourceDepositTrigger").isTrigger, Is.True);
        Assert.That(root.GetComponentsInChildren<FieldBaseSecurityNode>(true).Length, Is.EqualTo(1));
        foreach (var route in root.GetComponentsInChildren<FieldBaseCargoRoute2D>(true))
        {
            Assert.That(route.WaypointCount, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < route.WaypointCount; i++) Assert.That(route.GetWaypoint(i), Is.Not.Null);
        }
        var link = Ref<GameObject>(b, "turretPowerLinkPrefab");
        Assert.That(link.GetComponent<FieldBasePowerLink2D>(), Is.Not.Null); Assert.That(link.GetComponent<LineRenderer>(), Is.Not.Null);
        Assert.That(link.GetComponentsInChildren<Collider2D>(), Is.Empty);
        Assert.That(Ref<Transform>(b, "defaultTurretPowerSourcePoint").GetComponent<FieldBaseSecurityNode>(), Is.Not.Null);
    }
    [Test] public void SceneKeepsRoleReferencesAndAuthoredBasePair()
    {
        var scene = EditorSceneManager.OpenPreviewScene(EnemyRosterAudit.ScenePath);
        try
        {
            var generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene); var s = new SerializedObject(generator);
            Assert.That(s.FindProperty("fieldBasePrefabs").arraySize, Is.EqualTo(2));
            foreach (string field in new[] { "basicEnemyDefinition", "shotgunEnemyDefinition", "chargingEnemyDefinition", "meleeChargerDefinition", "eliteMachineGunDefinition", "eliteShotgunDefinition", "eliteChargingDefinition", "rivalHarvesterDefinition", "scavengerDefinition" })
                Assert.That(Ref<EnemyDefinition>(generator, field).EnemyPrefab, Is.Not.Null, field);
            Assert.That(Ref<GameObject>(generator, "defenderBasicPrefab").GetComponent<EnemyRoleController>(), Is.Not.Null);
            Assert.That(Ref<GameObject>(generator, "defenderShotgunPrefab").GetComponent<EnemyRoleController>(), Is.Not.Null);
            Assert.That(s.FindProperty("maxBasicEnemiesInsideStartSafeRadius").intValue, Is.Zero);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test] public void MissingRolePrefabProducesActionableDiagnosticWithoutBasicSubstitution()
    {
        var generator = New("Diagnostic generator").AddComponent<ExpeditionMapGenerator>();
        LogAssert.Expect(LogType.Warning, new Regex("RivalHarvester: requested 1, placed 0.*dedicated RivalHarvester.*no Basic substitution"));
        Assert.That(Call(generator, "PlaceRoleEnemyBatch", null, 1, EnemyRoleType.RivalHarvester, "RivalHarvester"), Is.EqualTo(0));
        Assert.That(generator.GetComponentsInChildren<EnemyBaseAI>(), Is.Empty);
    }
    [Test] public void DefenderReturnOwnsMovementUntilHomeEvenWithPlayerStillVisible()
    {
        var actor = Object.Instantiate(Definition("basic_enemy").EnemyPrefab); objects.Add(actor);
        var ai = actor.GetComponent<EnemyBaseAI>(); var role = actor.GetComponent<EnemyRoleController>();
        var anchor = New("Guard zone"); role.ConfigureAsDefender(anchor.transform, new Bounds(Vector3.zero, Vector3.one * 100));
        actor.transform.position = Vector3.right * 12;
        Assert.That(role.TryHandleReturn(ai, .1f), Is.True, "Must bypass generic sight-based re-aggro during return");
        Assert.That(Get<Vector2>(ai, "desiredVelocity").x, Is.LessThan(0));
        actor.transform.position = anchor.transform.position; role.TryHandleReturn(ai, .1f);
        Assert.That(Get<Vector2>(ai, "desiredVelocity"), Is.EqualTo(Vector2.zero));
    }
    [Test] public void ActualWreckColliderRejectsEnemyPlacementWhileClearSpaceRemainsAvailable()
    {
        var generator = New("Clearance generator").AddComponent<ExpeditionMapGenerator>();
        var obstacle = New("Large wreck footprint"); var collider = obstacle.AddComponent<BoxCollider2D>(); collider.size = new Vector2(6, 4);
        obstacle.layer = LayerMask.NameToLayer("WorldSolid"); Physics2D.SyncTransforms();
        var prefab = Definition("melee_charger").EnemyPrefab;
        Assert.That(Call(generator, "IsEnemySpawnClear", prefab, new Vector2(2, 0)), Is.False);
        Assert.That(Call(generator, "IsEnemySpawnClear", prefab, new Vector2(5, 0)), Is.True);
    }
    [Test] public void ChestGrowthAndResetDoNotLeakRewardMultiplier()
    {
        var g = New("Storage state"); g.AddComponent<BoxCollider2D>(); var health = g.AddComponent<HarvestObjectHealth>();
        var dropper = g.AddComponent<RewardDropper>(); var chest = g.AddComponent<FieldBaseResourceChest>();
        Call(health, "Awake"); Call(health, "OnEnable"); Call(chest, "Awake"); Call(chest, "OnEnable");
        Assert.That(chest.TryDeposit(CurrencyType.ScrapParts, 20), Is.EqualTo(20)); Assert.That(chest.StoredWeight, Is.EqualTo(20));
        Assert.That(dropper.RuntimeCurrencyMultiplier, Is.GreaterThan(1));
        Assert.That(chest.TryDeposit(CurrencyType.ScrapParts, 99), Is.EqualTo(20)); Assert.That(chest.StoredWeight, Is.EqualTo(40));
        Call(chest, "OnEnable"); Assert.That(chest.StoredWeight, Is.Zero); Assert.That(dropper.RuntimeCurrencyMultiplier, Is.EqualTo(1));
    }
    [Test] public void EnemyPickupAuthorityRejectsOwnedCargoAndMandatoryTuningChips()
    {
        var g = New("Reward authority"); g.AddComponent<CircleCollider2D>(); var pickup = g.AddComponent<RewardPickup>();
        pickup.InitializeOwnedCurrency(CurrencyType.ScrapParts, 5, Vector2.zero, 0);
        Assert.That(pickup.TryTakeCurrencyByEnemy(5, out _, out _), Is.False);
        pickup.InitializeCurrency(CurrencyType.TuningChips, 1, Vector2.zero); Set(pickup, "activeAge", 999f);
        Assert.That(pickup.TryTakeCurrencyByEnemy(1, out _, out _), Is.False);
        pickup.InitializeHeal(5, Vector2.zero); Assert.That(pickup.TryTakeCurrencyByEnemy(1, out _, out _), Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void RoleReservationsReleaseOnDisableAndDeath(bool death)
    {
        var actor = Object.Instantiate(Definition("rival_harvester").EnemyPrefab); objects.Add(actor);
        var role = actor.GetComponent<EnemyRoleController>();
        var harvestGo = New("Reserved wreck"); harvestGo.AddComponent<BoxCollider2D>(); var harvest = harvestGo.AddComponent<HarvestObjectHealth>();
        var pickupGo = New("Reserved loot"); pickupGo.AddComponent<CircleCollider2D>(); var pickup = pickupGo.AddComponent<RewardPickup>();
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var harvestSet = (HashSet<HarvestObjectHealth>)typeof(EnemyRoleController).GetField("ReservedHarvestTargets", flags).GetValue(null);
        var pickupSet = (HashSet<RewardPickup>)typeof(EnemyRoleController).GetField("ReservedRewardPickups", flags).GetValue(null);
        Set(role, "harvestTarget", harvest); Set(role, "rewardPickupTarget", pickup);
        harvestSet.Add(harvest); pickupSet.Add(pickup);
        if (death) Call(role, "HandleDied", actor.GetComponent<EnemyHealth>()); else Call(role, "OnDisable");
        Assert.That(harvestSet.Contains(harvest), Is.False); Assert.That(pickupSet.Contains(pickup), Is.False);
        Assert.That(Get<HarvestObjectHealth>(role, "harvestTarget"), Is.Null); Assert.That(Get<RewardPickup>(role, "rewardPickupTarget"), Is.Null);
    }
    [Test] public void HarvesterSearchDoesNotTreatBaseObjectivesAsSalvage()
    {
        var actor = Object.Instantiate(Definition("rival_harvester").EnemyPrefab); objects.Add(actor); actor.transform.position = Vector3.zero;
        var role = actor.GetComponent<EnemyRoleController>();
        var stronghold = New("Protected base"); stronghold.AddComponent<FieldBaseController>();
        var depot = New("Closest storage objective"); depot.transform.SetParent(stronghold.transform); depot.transform.position = Vector3.right;
        depot.AddComponent<BoxCollider2D>(); var depotHealth = depot.AddComponent<HarvestObjectHealth>(); Call(depotHealth, "OnEnable");
        var debris = New("Legitimate salvage"); debris.transform.position = Vector3.right * 4; debris.AddComponent<BoxCollider2D>();
        var salvage = debris.AddComponent<HarvestObjectHealth>(); Call(salvage, "OnEnable");
        Call(role, "TryAcquireHarvestTarget"); Assert.That(Get<HarvestObjectHealth>(role, "harvestTarget"), Is.SameAs(salvage));
        Call(role, "OnDisable");
    }
}
