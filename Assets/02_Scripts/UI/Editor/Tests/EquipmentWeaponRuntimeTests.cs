using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class EquipmentWeaponRuntimeTests
{
    private readonly List<GameObject> ownedObjects = new List<GameObject>();
    private static readonly Vector3 Origin = new Vector3(10000, 10000, 0);
    private GameObject root, projectile;
    private PoolManager pool, previousPool;
    private PlayerWeaponModifiers modifiers;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [SetUp]
    public void SetUp()
    {
        previousPool = PoolManager.Instance;
        // Own only our objects in the test runner's ordinary scene; never replace unsaved scenes.
        root = Own("Equipment weapon fixture"); root.SetActive(false); root.transform.position = Origin;
        modifiers = root.AddComponent<PlayerWeaponModifiers>(); pool = root.AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, pool);
        projectile = Own("Equipment test projectile"); projectile.SetActive(false);
        projectile.AddComponent<BoxCollider2D>().isTrigger = true;
        projectile.AddComponent<Rigidbody2D>().gravityScale = 0;
        projectile.AddComponent<Bullet>();
    }

    [TearDown]
    public void TearDown()
    {
        if (pool != null)
            foreach (GameObject item in Read<Dictionary<GameObject, GameObject>>(pool, "instanceToPrefab").Keys.ToArray())
                if (item != null) Object.DestroyImmediate(item);
        foreach (GameObject item in ownedObjects) if (item != null) Object.DestroyImmediate(item);
        ownedObjects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, previousPool);
    }

    private GameObject Own(string name) { var item = new GameObject(name); ownedObjects.Add(item); return item; }

    private T Weapon<T>() where T : PlayerWeaponBase
    {
        var weapon = root.AddComponent<T>();
        Set(weapon, "fallbackProjectilePrefab", projectile);
        weapon.SetRuntimeReferences(null, root.transform, null, null, modifiers);
        weapon.OnEquip();
        return weapon;
    }

    private static void Set(object target, string field, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var found = type.GetField(field, Flags); if (found != null) { found.SetValue(target, value); return; }
        }
        throw new MissingFieldException(field);
    }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private Bullet[] Shots() => Read<Dictionary<GameObject, GameObject>>(pool, "instanceToPrefab").Keys.Where(g => g != null && g.activeSelf).Select(g => g.GetComponent<Bullet>()).ToArray();
    private void ReleaseShots() { foreach (Bullet shot in Shots()) pool.Release(shot.gameObject); }
    private static readonly WeaponFireInput Fire = new WeaponFireInput(true, true, false);
    private static readonly WeaponFireInput Hold = new WeaponFireInput(true, false, false);
    private static readonly WeaponFireInput Release = new WeaponFireInput(false, false, true);

    [Test]
    public void TwinFeedEmitsOnePooledFollowupAfterSixShotsWithHeatAndCancellation()
    {
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.MachineGunTwinFeed, 3);
        var weapon = Weapon<MachineGunWeapon>();
        int fired = 0; weapon.Fired += (_, __) => fired++;
        for (int i = 0; i < 6; i++) weapon.TickWeapon(Hold, .11f);
        Assert.That(fired, Is.EqualTo(6)); Assert.That(weapon.HasPendingPairedFeed, Is.True);
        Assert.That(weapon.TwinFeedSpreadMultiplier, Is.EqualTo(.82f));
        weapon.TickWeapon(Hold, .026f);
        Assert.That(fired, Is.EqualTo(7)); Assert.That(Shots().Length, Is.EqualTo(7));
        Assert.That(weapon.CurrentHeat, Is.EqualTo(28));
        weapon.TickWeapon(Release, 0); Assert.That(weapon.HasPendingPairedFeed, Is.False); Assert.That(weapon.SustainedShotCount, Is.Zero);
        for (int i = 0; i < 6; i++) weapon.TickWeapon(Hold, .11f);
        weapon.OnUnequip(); weapon.TickWeapon(Release, 1);
        Assert.That(fired, Is.EqualTo(13), "Unequip cancels the queued pair.");
        weapon.SetRuntimeReferences(null, root.transform, null, null, modifiers);
        weapon.SetRuntimeReferences(null, root.transform, null, null, modifiers);
        var reset = Read<Action>(modifiers, "DevelopmentEquipmentReset");
        Assert.That(reset.GetInvocationList().Count(d => ReferenceEquals(d.Target, weapon)), Is.EqualTo(1));
        modifiers.ResetModifiers(); Assert.That(weapon.TwinFeedSpreadMultiplier, Is.EqualTo(1));
    }

    [Test]
    public void CoolingChangesExistingHeatOwnerAndResetRestoresBase()
    {
        var weapon = Weapon<MachineGunWeapon>();
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.MachineGunCoolingRatePercent, 50);
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.MachineGunCoolingDelayReduction, .1f);
        Assert.That(weapon.EffectiveCoolingRate, Is.EqualTo(82.5f)); Assert.That(weapon.EffectiveCoolingDelay, Is.EqualTo(.15f).Within(.0001));
        Set(weapon, "currentHeat", 90f); Set(weapon, "lastShotTime", -999f);
        weapon.TickWeapon(Release, .5f); Assert.That(weapon.CurrentHeat, Is.EqualTo(48.75f).Within(.001));
        modifiers.ResetModifiers(); Assert.That(weapon.EffectiveCoolingRate, Is.EqualTo(55));
    }

    private EnemyHealth Enemy(Vector2 position, float resistance = 1f)
    {
        var go = Own("Fixture hostile"); go.SetActive(false); go.transform.position = Origin + (Vector3)position;
        go.AddComponent<BoxCollider2D>(); var enemy = go.AddComponent<EnemyHealth>();
        Set(enemy, "useSmoothKnockback", false); Set(enemy, "knockbackDistanceMultiplier", resistance);
        go.SetActive(true); Invoke(enemy, "Awake"); Invoke(enemy, "OnEnable");
        return enemy;
    }

    [Test]
    public void DistributorUsesVisibleHostilesExcludesRecentAndDoesNotSteerThroughObstacles()
    {
        var camera = Own("Fixture camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
        camera.transform.position = Origin + new Vector3(0, 0, -10);
        var weapon = Weapon<MachineGunWeapon>(); Set(weapon, "distributionCamera", camera);
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.MachineGunTargetDistribution, 3);
        var first = Enemy(new Vector2(.1f, 2)); var second = Enemy(new Vector2(-.1f, 4));
        Physics2D.SyncTransforms();
        Assert.That(Invoke(weapon, "ResolveDistributionTarget", Vector2.up), Is.EqualTo(first.transform));
        Set(weapon, "recentDistributionTarget", first.transform);
        first.gameObject.SetActive(false); Physics2D.SyncTransforms();
        Assert.That(Invoke(weapon, "ResolveDistributionTarget", Vector2.up), Is.EqualTo(second.transform));
        var obstacle = Own("Fixture obstacle").AddComponent<BoxCollider2D>(); obstacle.transform.position = Origin + new Vector3(0, 2, 0);
        obstacle.size = new Vector2(2, .2f); Physics2D.SyncTransforms();
        Assert.That(Invoke(weapon, "ResolveDistributionTarget", Vector2.up), Is.Null);
        weapon.ForceCancel(); Assert.That(Read<Transform>(weapon, "recentDistributionTarget"), Is.Null);
    }

    [TestCase(1, 1.05f, 1)] [TestCase(2, 1.15f, 1)] [TestCase(3, 1.2f, 2)]
    public void SlugConvertsTwoPelletsWithoutDuplicatingDamageAndPoolResets(int level, float speed, int pierce)
    {
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.ShotgunSlugCoupler, level);
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.ShotgunImpactDisplacement, .8f);
        var weapon = Weapon<ShotgunWeapon>(); Set(weapon, "projectileSpeedVariation", 0f); Set(weapon, "projectileRangeVariation", 0f);
        weapon.TickWeapon(Fire, .1f); var shots = Shots();
        Assert.That(shots.Length, Is.EqualTo(5));
        var slug = shots.Single(b => b.EquipmentWidthMultiplier > 1f);
        Assert.That(Read<float>(slug, "damage"), Is.EqualTo(3.6f).Within(.001));
        Assert.That(shots.Sum(b => Read<float>(b, "damage")), Is.EqualTo(10.8f).Within(.001));
        Assert.That(slug.Speed, Is.EqualTo(12f * speed).Within(.001));
        Assert.That(Read<int>(slug, "remainingPierceCount"), Is.EqualTo(pierce));
        Assert.That(shots.Count(b => b.EquipmentFirstHitDisplacement > 0), Is.EqualTo(1));
        pool.Release(slug.gameObject);
        var reused = pool.Get(projectile, Vector3.zero, Quaternion.identity).GetComponent<Bullet>();
        reused.Initialize(Vector2.up, ProjectileOwner.Player);
        Assert.That(reused.EquipmentWidthMultiplier, Is.EqualTo(1)); Assert.That(reused.EquipmentFirstHitDisplacement, Is.Zero);
    }

    [TestCase(1f, .8f)] [TestCase(0f, 0f)]
    public void ImpactUsesReceiverResistanceOnceAndNeverAddsForce(float resistance, float expected)
    {
        var enemy = Enemy(new Vector2(0, 2), resistance);
        var bullet = pool.Get(projectile, Origin, Quaternion.identity).GetComponent<Bullet>();
        bullet.Initialize(Vector2.up, ProjectileOwner.Player); bullet.ConfigureFirstEnemyImpact(.8f);
        float prior = enemy.CurrentHp; enemy.TakeDamage(1);
        Invoke(bullet, "ApplyEquipmentImpact", enemy, prior);
        Assert.That(enemy.GetComponent<Rigidbody2D>().position.y - Origin.y, Is.EqualTo(2 + expected).Within(.002));
        Invoke(bullet, "ApplyEquipmentImpact", enemy, prior);
        Assert.That(enemy.GetComponent<Rigidbody2D>().position.y - Origin.y, Is.EqualTo(2 + expected).Within(.002));
        Assert.That(bullet.EquipmentFirstHitDisplacement, Is.Zero);
    }

    [TestCase(1, .75f)] [TestCase(2, .75f)] [TestCase(3, .6f)]
    public void BreachOpportunityAcceleratesOneRecoveryAndCancelClearsIt(int level, float multiplier)
    {
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.ShotgunBreachSequence, level);
        var weapon = Weapon<ShotgunWeapon>(); Set(weapon, "breachArmedUntil", Time.time + 1f);
        weapon.TickWeapon(Fire, .01f);
        Assert.That(weapon.NextFireTime - Time.time, Is.EqualTo(.85f * multiplier).Within(.001));
        Assert.That(weapon.HasBreachOpportunity, Is.False);
        Set(weapon, "nextFireTime", 0f); weapon.TickWeapon(Fire, .01f);
        Assert.That(weapon.NextFireTime - Time.time, Is.EqualTo(.85f).Within(.001));
        Set(weapon, "breachArmedUntil", Time.time + 1f); weapon.ForceCancel(); Assert.That(weapon.HasBreachOpportunity, Is.False);
    }

    [Test]
    public void TraverseServoActuallyChangesMovementIncrementallyAndRemovalRestoresIt()
    {
        var controller = root.AddComponent<PlayerController2D>(); controller.SetMoveSpeed(6);
        var applier = root.AddComponent<RunTraitEffectApplier>();
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        var trait = catalog.FindById("mg_traverse_servo");
        applier.ApplyTraitLevel(trait, 1); Assert.That(controller.MoveSpeed, Is.EqualTo(6.18f).Within(.001));
        applier.ApplyTraitLevel(trait, 2); Assert.That(controller.MoveSpeed, Is.EqualTo(6.18f).Within(.001));
        applier.ApplyTraitLevel(trait, 3); Assert.That(controller.MoveSpeed, Is.EqualTo(6.4272f).Within(.001));
        for (int level = 3; level >= 1; level--) applier.RemoveTraitLevel(trait, level);
        Assert.That(controller.MoveSpeed, Is.EqualTo(6).Within(.001));
    }

    [Test]
    public void AnchorOpticsUsesExistingStationaryCameraAssistAndCancelResets()
    {
        var cameraObject = Own("Fixture camera assist"); cameraObject.SetActive(false);
        var camera = cameraObject.AddComponent<GungeonStyleCamera2D>();
        var weapon = Weapon<SniperWeapon>(); Set(weapon, "gungeonStyleCamera", camera);
        Set(weapon, "isCharging", true); Set(weapon, "chargeTimer", 99f);
        Invoke(weapon, "UpdateAimAssist"); float baseline = Read<float>(camera, "runtimeAimOffsetMultiplier");
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.ChargeSightBonusPercent, 35);
        Invoke(weapon, "UpdateAimAssist"); Assert.That(Read<float>(camera, "runtimeAimOffsetMultiplier"), Is.GreaterThan(baseline));
        weapon.ForceCancel(); Assert.That(Read<float>(camera, "runtimeAimOffsetMultiplier"), Is.EqualTo(1));
    }

    [Test]
    public void BreachCompensationReducesActualDamageOnlyInCompletedShotgunDashWindow()
    {
        var go = Own("Fixture dash health"); go.SetActive(false);
        var health = go.AddComponent<PlayerHealth>(); var dash = go.AddComponent<PlayerDash>();
        var owner = go.AddComponent<PlayerWeaponController>(); owner.enabled = false;
        Set(owner, "currentWeapon", go.AddComponent<ShotgunWeapon>());
        var mod = go.AddComponent<PlayerWeaponModifiers>(); mod.TryApplyDevelopmentEffect(TraitEffectType.DashDamageReductionPercent, 20);
        Set(owner, "currentWeaponTree", WeaponTreeType.Shotgun); Set(dash, "weaponController", owner); Set(dash, "health", health);
        Set(health, "maxHp", 30f); Set(health, "currentHp", 30f); Invoke(health, "Awake");
        go.SetActive(true);
        Set(dash, "isDashing", true); Invoke(dash, "EndDashState", true);
        Assert.That(dash.CompletedDashSerial, Is.EqualTo(1));
        Assert.That(dash.HasRecentShotgunDash, Is.True); health.TakeDamage(10);
        Assert.That(health.CurrentHp, Is.EqualTo(22).Within(.001));
        Set(health, "invincibleTimer", 0f); dash.ClearEquipmentDashWindow(); health.TakeDamage(10);
        Assert.That(health.CurrentHp, Is.EqualTo(12).Within(.001));
        typeof(PlayerDash).GetProperty("LastCompletedDashTime").SetValue(dash, Time.time);
        Invoke(dash, "OnDisable"); Assert.That(dash.HasRecentShotgunDash, Is.False);
    }

    [Test]
    public void SniperReserveChargesOnceWidthIsSnapshotOwnedAndCancelOrExpiryClears()
    {
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.SniperReserveCapacitor, 25);
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.ChargedProjectileSizePercent, 40);
        modifiers.TryApplyDevelopmentEffect(TraitEffectType.SniperMovingChargeBonus, 40);
        var weapon = Weapon<SniperWeapon>(); Set(weapon, "showChargeAimLine", false);
        Assert.That(weapon.EffectiveMovingChargeMultiplier, Is.EqualTo(1));
        weapon.TickWeapon(Fire, 5f); weapon.TickWeapon(Release, 0);
        Assert.That(Shots().Single().EquipmentWidthMultiplier, Is.EqualTo(1.4f).Within(.001));
        Assert.That(weapon.HasReservedCharge, Is.True);
        Set(weapon, "nextChargeAllowedTime", 0f); weapon.TickWeapon(Fire, 0);
        Assert.That(weapon.ChargeRatio, Is.EqualTo(.25f).Within(.001)); Assert.That(weapon.IsReserveAssistedCharge, Is.True);
        weapon.TickWeapon(Hold, 5f); weapon.TickWeapon(Release, 0);
        Assert.That(weapon.HasReservedCharge, Is.False, "Assisted full charge cannot bank itself again.");
        Set(weapon, "reservedChargeFraction", .25f); Set(weapon, "reserveExpiresAt", Time.time + 3f);
        weapon.ForceCancel(); Assert.That(weapon.HasReservedCharge, Is.False);
        Set(weapon, "reservedChargeFraction", .25f); Set(weapon, "reserveExpiresAt", Time.time - 1f);
        Assert.That(weapon.HasReservedCharge, Is.False);
        modifiers.ResetModifiers(); Assert.That(weapon.EffectiveMovingChargeMultiplier, Is.EqualTo(.6f));
    }
}
