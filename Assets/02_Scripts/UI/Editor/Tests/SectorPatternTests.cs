using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorPatternTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager prior;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Flags).Invoke(o, args);
    GameObject New(string name) { var g = new GameObject(name); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    {
        prior = PoolManager.Instance;
        typeof(PoolManager).GetProperty("Instance").SetValue(null, New("Pattern pool").AddComponent<PoolManager>());
    }
    [TearDown] public void Cleanup()
    {
        foreach (var root in roots) if (root != null && root.TryGetComponent<BossPatternController>(out var boss))
        { Call(boss, "ClearSectorAttacks"); Call(boss, "EndSectorArenaCleanup"); Bullet.ReleaseAllActiveFromSource(boss.transform); }
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, prior);
    }
    BossPatternController Boss()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive fixture").transform);
        roots.Add(root); var boss = root.GetComponent<BossPatternController>(); Set(boss, "arenaCenter", Vector2.zero);
        var target = New("Player target"); target.transform.position = Vector3.down * 4;
        Set(boss, "player", target.transform); return boss;
    }
    [Test] public void SavedBindingsReuseExistingArtAndPreserveWeaponsAndBalance()
    {
        var boss = Boss();
        foreach (string name in new[] { "sectorMissilePrefab", "sectorRectangularPrefab", "sectorSpawnMarkerPrefab", "sectorPulsePrefab", "sectorLeftMuzzle", "sectorRightMuzzle", "sectorChargeMuzzle" })
            Assert.That(Get(boss, name), Is.Not.Null, name);
        Assert.That(((SpriteRenderer)Get(boss, "sectorChargeCannon")).transform.localScale, Is.EqualTo(Vector3.one * 1.25f));
        Assert.That(Get(boss, "maxHp"), Is.EqualTo(140f)); Assert.That(Get(boss, "spreadProjectileDamage"), Is.EqualTo(2f));
        Assert.That(Get(boss, "chargeProjectileDamage"), Is.EqualTo(6f)); Assert.That(Get(boss, "laserDamage"), Is.EqualTo(4f));
        Assert.That(Get(boss, "sectorBurstCadence"), Is.EqualTo(.08f)); Assert.That(Get(boss, "sectorBurstGap"), Is.EqualTo(.32f));
        Assert.That(boss.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
    }
    [Test] public void SpawnHidesBodyAndSeparateCenterCannonTogether()
    {
        var boss = Boss(); var intro = New("Intro").AddComponent<CoreBossIntroSequence>();
        Set(intro, "spawnedBoss", boss.gameObject); Call(intro, "CacheBossPresentation", boss.gameObject);
        foreach (var visual in boss.GetComponentsInChildren<SpriteRenderer>(true).Where(v => !boss.IsSectorArrivalRenderer(v) && !boss.IsSectorShieldRenderer(v)))
        { Assert.That(visual.forceRenderingOff, Is.True); Assert.That(visual.color.a, Is.Zero); }
        Assert.That(((SpriteRenderer)Get(boss, "sectorChargeCannon")).transform.localScale.x, Is.EqualTo(1.25f));
        Call(intro, "RestoreMaterializingBody");
        foreach (var visual in boss.GetComponentsInChildren<SpriteRenderer>(true))
        { Assert.That(visual.forceRenderingOff, Is.False); Assert.That(visual.color.a, Is.GreaterThan(0)); }
    }
    [Test] public void AbortedMaterializationCannotFlashTheDormantBoss()
    {
        var boss = Boss(); var intro = New("Intro").AddComponent<CoreBossIntroSequence>();
        Set(intro, "spawnedBoss", boss.gameObject); Call(intro, "CacheBossPresentation", boss.gameObject);
        boss.PlaySectorSpawnMarker(Vector3.zero, .75f); Assert.That(boss.SectorSpawnMarkerVisible, Is.True);
        Set(intro, "introCancellationRequested", true); Call(intro, "RestoreMaterializingBody");
        Assert.That(boss.SectorSpawnMarkerVisible, Is.False);
        foreach (var visual in boss.GetComponentsInChildren<SpriteRenderer>(true)) Assert.That(visual.forceRenderingOff, Is.True);
    }
    [Test] public void PairedSprayUsesBothRotatedMuzzlesAndCommittedAim()
    {
        var boss = Boss(); ((Transform)Get(boss, "visualRoot")).rotation = Quaternion.Euler(0, 0, 53);
        Call(boss, "CommitSectorSweep"); Vector2 aim = (Vector2)Get(boss, "sectorBurstDirection");
        ((Transform)Get(boss, "player")).position = Vector3.left * 8;
        for (int i = 0; i < 6; i++) Call(boss, "DispatchSectorSweepPair", i);
        var bullets = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b => b.SourceRoot == boss.transform).ToArray();
        Assert.That(bullets.Length, Is.EqualTo(12)); Assert.That(boss.SectorShotsThisCycle, Is.EqualTo(12));
        Assert.That(Get(boss, "sectorBurstDirection"), Is.EqualTo(aim));
        foreach (string field in new[] { "sectorLeftMuzzle", "sectorRightMuzzle" })
        {
            var muzzle = (Transform)Get(boss, field);
            Assert.That(bullets.Count(b => Vector2.Distance((Vector2)Get(b, "spawnPosition"), muzzle.position) < .001f), Is.EqualTo(6));
        }
        Assert.That(bullets.All(b => !(bool)Get(b, "useHoming")), Is.True);
        Assert.That(boss.transform.rotation, Is.EqualTo(Quaternion.identity));
    }
    [Test] public void MissilesAreCappedAtSixAndLaunchFromCore()
    {
        var boss = Boss(); ((Transform)Get(boss, "visualRoot")).rotation = Quaternion.Euler(0, 0, 42);
        for (int i = 0; i < 6; i++) Assert.That(Call(boss, "LaunchSectorMissile", i, Vector2.down), Is.True);
        Assert.That(Call(boss, "LaunchSectorMissile", 6, Vector2.down), Is.False);
        Assert.That(boss.SectorActiveMissiles, Is.EqualTo(6));
        foreach (var missile in (Bullet[])Get(boss, "sectorMissiles"))
        {
            Assert.That(missile.Owner, Is.EqualTo(ProjectileOwner.Enemy)); Assert.That(missile.SourceRoot, Is.EqualTo(boss.transform));
            Assert.That(Vector2.Distance((Vector2)Get(missile, "spawnPosition"), boss.SectorMissileCorePosition), Is.LessThan(.001f));
            Assert.That(missile.Speed, Is.EqualTo(6f)); Assert.That(Get(missile, "useHoming"), Is.False);
        }
    }
    [Test] public void MissileGuidanceIsDelayedBoundedTimedAndCannotReacquire()
    {
        var boss = Boss(); Call(boss, "LaunchSectorMissile", 0, Vector2.down);
        ((Transform)Get(boss, "player")).gameObject.SetActive(true);
        var missile = ((Bullet[])Get(boss, "sectorMissiles"))[0];
        Call(boss, "UpdateSectorSupport", 0f); Assert.That(Get(missile, "useHoming"), Is.False);
        ((float[])Get(boss, "sectorMissileBorn"))[0] = Time.time - .36f;
        Call(boss, "UpdateSectorSupport", 0f);
        Assert.That(Get(missile, "homingTimeRemaining"), Is.EqualTo(1.5f)); Assert.That(Get(missile, "allowHomingReacquisition"), Is.False);
        Assert.That(Get(missile, "useTimedHoming"), Is.True); Assert.That(Get(missile, "homingAngle"), Is.EqualTo(140f));
        var direction = missile.MoveDirection; Call(missile, "UpdateHomingStep", .02f);
        Assert.That(Vector2.Angle(direction, missile.MoveDirection), Is.LessThanOrEqualTo(BossPatternController.SectorMissileTurnRate * .02f + .01f));
        Set(missile, "homingTimeRemaining", 0f); Call(missile, "UpdateHomingStep", .02f); Assert.That(Get(missile, "useHoming"), Is.False);
    }
    [Test] public void PurpleModeReversesTheSameSixLanesWithoutAnotherOwner()
    {
        var boss = Boss(); Call(boss, "EnsureSectorSpokes", 6);
        var lanes = ((SectorPartitionLane[])Get(boss, "sectorLanes")).ToArray();
        Set(boss, "sectorRotating", true); float angle = boss.SectorAngle;
        Call(boss, "UpdateSectorSpokes", .1f); Assert.That(boss.SectorAngle-angle, Is.EqualTo(3).Within(.001f));
        Call(boss, "SetSectorEnergy", 1f); Call(boss, "CommitSectorRotationMode", true); angle = boss.SectorAngle;
        Call(boss, "UpdateSectorSpokes", .1f); Assert.That(boss.SectorAngle-angle, Is.EqualTo(-3).Within(.001f));
        Assert.That((SectorPartitionLane[])Get(boss, "sectorLanes"), Is.EqualTo(lanes));
        Assert.That(boss.SectorCounterActive, Is.False);
        foreach(var lane in lanes) { Assert.That(lane.IsVisible && lane.IsDamaging, Is.True); Assert.That(lane.Empowerment, Is.EqualTo(1)); }
    }
    [Test] public void EscalationPulseAndQuickFadeKeepGrowthHarmlessAndRestoreOriginalColors()
    {
        var boss = Boss(); var body = (SpriteRenderer)Get(boss, "sectorBody"); var cannon = (SpriteRenderer)Get(boss, "sectorChargeCannon");
        var bodyColor = body.color; var cannonColor = cannon.color;
        Call(boss, "PrepareSectorEscalation"); ((IEnumerator)Call(boss, "SectorEscalationRoutine")).MoveNext();
        Assert.That(boss.SectorPulseVisible, Is.True);
        Call(boss, "SampleSectorEscalationFade", .12f); Assert.That(body.color.a, Is.EqualTo(.22f).Within(.001f));
        Assert.That(cannon.color.a, Is.EqualTo(.22f).Within(.001f));
        foreach (var lane in (SectorPartitionLane[])Get(boss, "sectorLanes")) Assert.That(lane.IsDamaging, Is.False);
        Call(boss, "SampleSectorEscalationFade", .34f); Assert.That(body.color, Is.EqualTo(bodyColor)); Assert.That(cannon.color, Is.EqualTo(cannonColor));
        Assert.That(Get(boss, "sectorEscalationWarningTime"), Is.EqualTo(1.2f));
    }
    [TestCase("StopCombatForDeathPresentation")] [TestCase("HandlePlayerDied")]
    [TestCase("HandleRunEnded")] [TestCase("OnDisable")]
    public void InterruptionClearsAllSupportAndPreservesSingleSchedulerOwnership(string entry)
    {
        var boss = Boss(); Call(boss, "LaunchSectorMissile", 0, Vector2.down);
        ((IEnumerator)Call(boss, "AlternatingRectangularAoERoutine")).MoveNext();
        Call(boss, "EnsureSectorSpokes", 6); Call(boss, "SetSectorEnergy", 1f);
        boss.PlaySectorSpawnMarker(Vector3.zero, 1); Call(boss, "BeginSectorEscalationPulse");
        if (entry == "HandleRunEnded") Call(boss, entry, new object[] { null }); else Call(boss, entry);
        Assert.That(boss.SectorActiveMissiles + boss.SectorZoneCount, Is.Zero);
        Assert.That(boss.SectorCounterActive || boss.SectorPulseVisible || boss.SectorSpawnMarkerVisible, Is.False);
        foreach (string field in new[] { "patternRoutine", "phase2ShieldCombatRoutine", "phase2TransitionRoutine" }) Assert.That(Get(boss, field), Is.Null);
    }
}
