using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorCoreTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager prior;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Flags).Invoke(o, args);
    GameObject New(string n) { var g = new GameObject(n); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    { prior = PoolManager.Instance; typeof(PoolManager).GetProperty("Instance").SetValue(null, New("Core test pool").AddComponent<PoolManager>()); }
    [TearDown] public void Cleanup()
    {
        foreach (var root in roots) if (root != null && root.TryGetComponent<BossPatternController>(out var boss))
        { Call(boss, "ClearSectorAttacks"); Call(boss, "EndSectorArenaCleanup"); Bullet.ReleaseAllActiveFromSource(boss.transform); }
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, prior);
    }
    BossPatternController Boss()
    {
        var g = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive parent").transform);
        roots.Add(g); var boss = g.GetComponent<BossPatternController>();
        Set(boss, "arenaCenter", Vector2.zero); var p = New("Player"); p.transform.position = Vector2.down * 5;
        Set(boss, "player", p.transform); return boss;
    }
    SectorRectangularAoE Area(out PlayerHealth target)
    {
        var root = PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>(SectorCoreAuthoring.Rectangle), Vector3.zero, Quaternion.identity);
        roots.Add(root); var area = root.GetComponent<SectorRectangularAoE>();
        target = New("Damage target").AddComponent<PlayerHealth>(); target.SetMaxHp(100, true);
        area.Configure(Vector2.zero, new Vector2(6, 6.5f), 1.2f, 4, target); return area;
    }
    [Test] public void AuthoredBoundedGeometryHasNoColliderOrPerEffectUpdate()
    {
        var a = Area(out _); Assert.That(a.BandCount, Is.EqualTo(5));
        Assert.That(a.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
        Assert.That(a.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(SectorRectangularAoE.MaxBands * 6));
        Assert.That(typeof(SectorRectangularAoE).GetMethod("Update", Flags), Is.Null);
        Assert.That(a.BandWidth, Is.GreaterThan(.17020631f * 2 + .5f));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void WarningsAreHarmlessAndExplosionsOwnOnlyAlternatingBands(int band)
    {
        var a = Area(out var p); p.transform.position = new Vector3(6 - 1.2f * (band + .5f), 0);
        a.Warn(0, 1); Assert.That(p.CurrentHp, Is.EqualTo(100));
        a.Explode(0); Assert.That(p.CurrentHp, Is.EqualTo(band % 2 == 0 ? 96 : 100));
        Set(p, "invincibleTimer", 0f); float after = p.CurrentHp;
        a.Warn(1, 1); Assert.That(p.CurrentHp, Is.EqualTo(after));
        a.Explode(1); Assert.That(p.CurrentHp, Is.EqualTo(96));
        Assert.That(a.ExplodedMask, Is.EqualTo(3));
    }
    [Test] public void ExplosionRequiresItsOwnWarningAndCannotRepeatOrDoubleHitCorners()
    {
        var a = Area(out var p); p.transform.position = new Vector3(3.0f, 3.5f);
        a.Explode(1); Assert.That(p.CurrentHp, Is.EqualTo(100));
        a.Warn(0, 1); a.Explode(0); Assert.That(p.CurrentHp, Is.EqualTo(96));
        Set(p, "invincibleTimer", 0f); a.Warn(0, 1); a.Explode(0); Assert.That(p.CurrentHp, Is.EqualTo(96));
    }
    [Test] public void PlayerCenterOwnsOneRingEvenWhenColliderStraddlesTheBoundary()
    {
        var a = Area(out var p); var c = p.gameObject.AddComponent<CircleCollider2D>(); c.radius = .2f;
        p.gameObject.SetActive(true); p.transform.position = new Vector2(3.61f, 4.11f); Physics2D.SyncTransforms();
        a.Configure(Vector2.zero, new Vector2(6, 6.5f), 1.2f, 4, p);
        Assert.That(a.IntersectsPlayer(0), Is.False); Assert.That(a.IntersectsPlayer(1), Is.True);
        p.transform.position = new Vector2(3.59f, 4.09f); Physics2D.SyncTransforms();
        Assert.That(a.IntersectsPlayer(0), Is.True); Assert.That(a.IntersectsPlayer(1), Is.False);
    }
    [Test] public void AReleaseHasVisibleFlashWhileBWarnsThenPoolReturnClearsEverything()
    {
        var a = Area(out _); a.Explode(0); a.Warn(1, 0);
        Assert.That(((LineRenderer[])Get(a, "fills"))[0].startColor.a, Is.GreaterThan(.3f));
        Assert.That(a.WarningPhase, Is.EqualTo(1)); a.ReleaseVisual(0, 1); Assert.That(a.IsVisible, Is.True);
        a.Explode(1); a.ReleaseVisual(1, 1); a.Clear();
        Assert.That(a.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled), Is.False);
        Assert.That(Get(a, "player"), Is.Null); Assert.That(a.ExplodedMask, Is.Zero);
    }
    [Test] public void FourStripAreaExactlyEqualsRectangularBandWithoutCornerOverlap()
    {
        var lo = new Vector2(2, 2.5f); var hi = lo + Vector2.one * 1.2f; float total = 0;
        for (int i = 0; i < 4; i++)
        {
            var a = SectorRectangularAoE.Strip(lo, hi, i); total += a.width * a.height;
            for (int j = i + 1; j < 4; j++) Assert.That(a.Overlaps(SectorRectangularAoE.Strip(lo, hi, j)), Is.False);
        }
        Assert.That(total, Is.EqualTo(4 * (hi.x * hi.y - lo.x * lo.y)).Within(.0001f));
    }
    [Test] public void RectangleStartsAtArenaPerimeterIndependentOfBossPosition()
    {
        var b = Boss(); b.transform.position = new Vector2(3, 2);
        ((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext();
        var a = (SectorRectangularAoE)Get(b, "sectorRectangles"); Assert.That(a, Is.Not.Null);
        var outer = a.OuterHalfExtents;
        Assert.That(a.BandWidth, Is.EqualTo(1.2f));
        for (int i = 0; i < 4; i++)
        {
            var offset = new Vector2((i % 2 == 0 ? -1 : 1) * outer.x, (i < 2 ? -1 : 1) * outer.y);
            Assert.That(a.RingIndex((Vector2)a.transform.position + offset), Is.EqualTo(0));
            Assert.That(outer, Is.EqualTo(b.SectorRectangleArenaBounds.size * .5f));
            Assert.That((Vector2)a.transform.position, Is.EqualTo(b.SectorRectangleArenaBounds.center));
        }
    }
    [Test] public void SavedLaunchersAreDistinctChildrenAndMGAndCannonArePreserved()
    {
        var b = Boss(); var visual = (Transform)Get(b, "visualRoot");
        var left = (Transform)Get(b, "sectorLeftMissileLauncher"); var right = (Transform)Get(b, "sectorRightMissileLauncher"); var rear = (Transform)Get(b, "sectorRearMissileLauncher");
        foreach (var t in new[] { left, right, rear }) Assert.That(t.parent, Is.EqualTo(visual));
        Assert.That(left.localPosition.x, Is.LessThan(0)); Assert.That(right.localPosition.x, Is.GreaterThan(0)); Assert.That(rear.localPosition.y, Is.LessThan(-.5f));
        Assert.That(Get(b, "sectorLeftMuzzle"), Is.Not.SameAs(left)); Assert.That(Get(b, "sectorRightMuzzle"), Is.Not.SameAs(right));
        Assert.That(((SpriteRenderer)Get(b, "sectorChargeCannon")).transform.localScale.x, Is.EqualTo(1.25f));
    }
    [Test] public void SixMissilesUseRotatedCoreAndIgnoreBossCollision()
    {
        var b = Boss(); ((Transform)Get(b, "visualRoot")).rotation = Quaternion.Euler(0, 0, 76);
        for (int i = 0; i < 6; i++) Assert.That(Call(b, "LaunchSectorMissile", i, Vector2.down), Is.True);
        Assert.That(Call(b, "LaunchSectorMissile", 0, Vector2.down), Is.False);
        var missiles = (Bullet[])Get(b, "sectorMissiles");
        Assert.That(missiles.Count(m => Vector2.Distance((Vector2)Get(m, "spawnPosition"), b.SectorMissileCorePosition) < .0001f), Is.EqualTo(6));
        foreach (var m in missiles)
        { Call(m, "OnTriggerEnter2D", b.GetComponent<Collider2D>()); Assert.That(m.gameObject.activeSelf, Is.True); Assert.That(m.Owner, Is.EqualTo(ProjectileOwner.Enemy)); }
    }
    [Test] public void EnemyVariantUsesExactPlayerSpriteWithRedOutlineAndPoolResetsTargetTrail()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SectorCoreAuthoring.PlayerMissile);
        var b = Boss(); Call(b, "LaunchSectorMissile", 0, Vector2.up);
        var m = ((Bullet[])Get(b, "sectorMissiles"))[0]; var renderers = m.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(renderers.Length, Is.EqualTo(2)); Assert.That(source.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
        Assert.That(m.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite, Is.EqualTo(source.GetComponentInChildren<SpriteRenderer>().sprite));
        var outline = m.GetComponentInChildren<LineRenderer>(); Assert.That(outline.startColor.r, Is.EqualTo(1)); Assert.That(outline.startColor.g, Is.LessThan(.1f));
        Assert.That(source.GetComponentInChildren<LineRenderer>(), Is.Null);
        m.ConfigureTimedHoming(((Transform)Get(b, "player")), 45, 20, false, .8f); m.ForceRelease(); Call(m, "OnDisable");
        Assert.That(Get(m, "homingTarget"), Is.Null); Assert.That(m.GetComponent<TrailRenderer>().positionCount, Is.Zero);
        var reused = PoolManager.Instance.Get(((Bullet)Get(b, "sectorMissilePrefab")).gameObject, Vector3.zero, Quaternion.identity).GetComponent<Bullet>();
        roots.Add(reused.gameObject); Call(reused, "OnEnable");
        Assert.That(Get(reused, "useHoming"), Is.False); Assert.That(outline.startColor.g, Is.LessThan(.1f));
    }
    [Test] public void SweepCommitSurvivesPlayerMotionAndBothSidesCrossWithTwelveShots()
    {
        var b = Boss(); Call(b, "CommitSectorSweep"); var aim = b.SectorCommittedAim;
        ((Transform)Get(b, "player")).position = Vector2.right * 10;
        for (int step = 0; step < 6; step++)
        { Call(b, "DispatchSectorSweepPair", step); Assert.That(b.SectorCommittedAim, Is.EqualTo(aim)); }
        var shots = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(m => m.SourceRoot == b.transform).ToArray();
        Assert.That(shots.Length, Is.EqualTo(12)); Assert.That(b.SectorShotsThisCycle, Is.EqualTo(12));
        foreach (string field in new[] { "sectorLeftMuzzle", "sectorRightMuzzle" })
            Assert.That(shots.Count(m => Vector2.Distance((Vector2)Get(m, "spawnPosition"), ((Transform)Get(b, field)).position) < .001f), Is.EqualTo(6));
        Assert.That(BossPatternController.SectorSweepAngle(0, false), Is.EqualTo(-40)); Assert.That(BossPatternController.SectorSweepAngle(4, false), Is.EqualTo(0).Within(.0001f));
        for (int i = 0; i < 6; i++) Assert.That(BossPatternController.SectorSweepAngle(i, true), Is.EqualTo(-BossPatternController.SectorSweepAngle(i, false)).Within(.001f));
    }
    [TestCase(0, "SectorSweepRoutine")] [TestCase(1, "SectorMissileRoutine")] [TestCase(2, "AlternatingRectangularAoERoutine")] [TestCase(3, "SectorRotationRoutine")]
    public void ExistingSchedulerSelectsExactlyOneStandaloneChild(int cycle, string routine)
    {
        var b = Boss(); Set(b, "sectorCycle", cycle); var sequence = (IEnumerator)Call(b, "SectorControlCycleRoutine", false);
        Assert.That(sequence.MoveNext(), Is.True); Assert.That(sequence.Current.GetType().Name, Does.Contain(routine));
        var attack = (IEnumerator)sequence.Current; attack.MoveNext();
        if (cycle != 3) Assert.That(b.SectorSpokeCount, Is.Zero);
        Assert.That(Get(b, "patternRoutine"), Is.Null); Assert.That(Get(b, "phase2ShieldCombatRoutine"), Is.Null);
    }
    [TestCase("StopCombatForDeathPresentation")] [TestCase("HandlePlayerDied")] [TestCase("CancelCombat")] [TestCase("OnDisable")]
    public void InterruptionsReleaseRectanglesAllSixMissilesAndAimOwnership(string entry)
    {
        var b = Boss(); ((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext(); var a = (SectorRectangularAoE)Get(b, "sectorRectangles");
        for (int i = 0; i < 6; i++) Call(b, "LaunchSectorMissile", i, Vector2.up);
        Call(b, "CommitSectorSweep"); Call(b, entry);
        Assert.That(b.SectorActiveMissiles + b.SectorZoneCount + b.SectorSpokeCount, Is.Zero);
        Assert.That(a.IsVisible, Is.False); Assert.That(Get(b, "sectorWeaponFacingLocked"), Is.False);
    }
    [Test] public void PhaseOneSweepHasOneCommitSixPairsAndNoReverse()
    {
        var b = Boss(); Set(b, "sectorSweepAimTime", 0f);
        var routine = (IEnumerator)Call(b, "SectorSweepRoutine");
        Assert.That(routine.MoveNext(), Is.True); Assert.That(b.CurrentSectorStage, Is.EqualTo(BossPatternController.SectorStage.SweepCommit));
        var aim = b.SectorCommittedAim;
        Assert.That((float)Get(routine.Current, "m_Seconds"), Is.EqualTo(.08f));
        ((Transform)Get(b, "player")).position = Vector2.left * 9;
        for (int pair = 1; pair <= 6; pair++)
        {
            Assert.That(routine.MoveNext(), Is.True); Assert.That(b.SectorShotsThisCycle, Is.EqualTo(pair * 2));
            Assert.That(b.SectorCommittedAim, Is.EqualTo(aim));
            float expected = pair == 6 ? 2f : .12f;
            Assert.That((float)Get(routine.Current, "m_Seconds"), Is.EqualTo(expected).Within(.0001f));
        }
    }
    [Test] public void SavedStandaloneTimingAndDamageBudgetsAreExplicit()
    {
        var b = Boss();
        Assert.That(Get(b,"sectorSweepAimTime"),Is.EqualTo(.5f)); Assert.That(Get(b,"sectorSweepStepTime"),Is.EqualTo(.12f));
        Assert.That(Get(b,"sectorMissileGroupDelay"),Is.EqualTo(.16f)); Assert.That(Get(b,"sectorRectangleWarningTime"),Is.EqualTo(1.35f));
        Assert.That(Get(b,"sectorRectangleMaxBands"),Is.EqualTo(4)); Assert.That(Get(b,"spreadProjectileDamage"),Is.EqualTo(2f));
        Assert.That(Get(b,"laserDamage"),Is.EqualTo(4f)); Assert.That(Get(b,"maxHp"),Is.EqualTo(140f));
        Assert.That(Get(Get(b,"sectorMissilePrefab"),"fallbackLifeTime"),Is.EqualTo(3.2f));
    }

    [Test] public void RectangleCommitCancelsResidualRecoveryVelocity()
    {
        var b = Boss(); var body = b.GetComponent<Rigidbody2D>(); Set(b,"rb",body);
        body.linearVelocity = Vector2.right * 2;
        ((IEnumerator)Call(b,"AlternatingRectangularAoERoutine")).MoveNext();
        Assert.That(body.linearVelocity,Is.EqualTo(Vector2.zero));
    }

}
