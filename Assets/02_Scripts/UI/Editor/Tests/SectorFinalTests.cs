using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorFinalTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> roots = new List<GameObject>(); PoolManager previous;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Flags).Invoke(o, a);
    GameObject New(string n) { var g = new GameObject(n); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    { previous = PoolManager.Instance; typeof(PoolManager).GetProperty("Instance").SetValue(null, New("Final pool").AddComponent<PoolManager>()); }
    [TearDown] public void Cleanup()
    {
        foreach (var g in roots) if (g != null && g.TryGetComponent<BossPatternController>(out var b))
        { Call(b, "ClearSectorAttacks"); Call(b, "EndSectorArenaCleanup"); Bullet.ReleaseAllActiveFromSource(b.transform); }
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, previous);
    }
    BossPatternController Boss()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive fixture").transform);
        roots.Add(root); var b = root.GetComponent<BossPatternController>(); Set(b, "arenaCenter", Vector2.zero);
        var p = New("Player"); p.transform.position = Vector2.down * 5; Set(b, "player", p.transform); return b;
    }
    SectorPartitionLane Lane()
    {
        var boss = Boss(); var prefab = (SectorPartitionLane)Get(boss, "sectorLanePrefab");
        var root = PoolManager.Instance.Get(prefab.gameObject, Vector3.zero, Quaternion.identity); roots.Add(root);
        var lane = root.GetComponent<SectorPartitionLane>(); lane.Configure(Vector2.zero, Vector2.right * 11.7f, .45f, 4, .75f); return lane;
    }
    [TestCase("Warning")] [TestCase("Beam")] [TestCase("Emitter")]
    public void ApprovedPurpleCopiesAreByteIdenticalAndProductionBound(string family)
    {
        string name = "VFX_Sector_PurpleLaser" + family;
        Assert.That(File.ReadAllBytes(SectorFinalAuthoring.Art + name + ".png"), Is.EqualTo(File.ReadAllBytes(SectorFinalAuthoring.Source + name + ".png")));
        var lane = Lane(); string field = family == "Warning" ? "purpleWarningFrames" : family == "Beam" ? "purpleActiveFrames" : "purpleEmitterFrames";
        var frames = (Sprite[])Get(lane, field); Assert.That(frames.Length, Is.EqualTo(4));
        foreach (var f in frames)
        { Assert.That(AssetDatabase.GetAssetPath(f), Is.EqualTo(SectorFinalAuthoring.Art + name + ".png")); Assert.That(f.pixelsPerUnit, Is.EqualTo(32)); }
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
    }
    [Test] public void PurpleFramesTileAtNativeThicknessWithExactActiveCollisionAndNoSolidRenderer()
    {
        var lane = Lane(); lane.SetEmpowerment(1);
        var sprite = (SpriteRenderer)Get(lane, "visual"); var emitter = (SpriteRenderer)Get(lane, "purpleEmitter"); var box = lane.GetComponent<BoxCollider2D>();
        lane.Warn(.3f); Assert.That(lane.IsDamaging, Is.False); var warning = sprite.sprite;
        Assert.That(AssetDatabase.GetAssetPath(warning), Does.Contain("PurpleLaserWarning")); Assert.That(emitter.enabled, Is.False);
        int[] bands = { 14, 18, 14, 10 };
        for (int i = 0; i < 4; i++)
        {
            lane.Activate(i * .06f + .001f);
            Assert.That(sprite.sprite, Is.Not.EqualTo(warning)); Assert.That(sprite.color, Is.EqualTo(Color.white));
            Assert.That(sprite.drawMode, Is.EqualTo(SpriteDrawMode.Tiled)); Assert.That(sprite.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(sprite.size, Is.EqualTo(new Vector2(11.7f, 1))); Assert.That(box.size.x, Is.EqualTo(sprite.size.x));
            Assert.That(box.offset.x * 2, Is.EqualTo(sprite.size.x)); Assert.That(box.size.y, Is.EqualTo(bands[i] / 32f));
            Assert.That(((LineRenderer)Get(lane, "solidVisual")).enabled, Is.False); Assert.That(lane.IsDamaging, Is.True);
        }
        lane.Activate(.01f); Assert.That(emitter.enabled, Is.True); lane.Activate(.3f); Assert.That(emitter.enabled, Is.False);
        lane.Clear(); lane.Configure(Vector2.zero, Vector2.up * 7, .45f, 4, .75f); lane.Activate(.2f);
        Assert.That(AssetDatabase.GetAssetPath(sprite.sprite), Is.EqualTo("Assets/Art/SectorAdministrator/VFX_Sector_LaserBeam.png"));
        Assert.That(emitter.enabled, Is.False); Assert.That(lane.Empowerment, Is.Zero);
    }
    [TestCase(false)] [TestCase(true)]
    public void SchedulerPhaseTablesContainOnlyApprovedMainAttacks(bool phase2)
    {
        var b = Boss(); Set(b, "phase2", phase2);
        for (int i = 0; i < 48; i++)
        {
            Set(b, "sectorCycle", i); var r = (IEnumerator)Call(b, "SectorControlCycleRoutine", phase2); Assert.That(r.MoveNext(), Is.True);
            string routine = r.Current.GetType().Name;
            if (phase2) Assert.That(routine, Does.Not.Contain("Rectangular"));
            Assert.That(routine, Does.Not.Contain("DirectBurst"));
        }
        Set(b, "phase2", true); Assert.That(((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext(), Is.False);
    }
    [Test] public void ArenaCoverageUsesLiveContainmentFacesAndEveryPointHasOneParity()
    {
        var b = Boss(); b.transform.position = new Vector2(3, 2);
        var walls = (List<GameObject>)Get(b, "boundaryLaserWalls");
        for (int i = 0; i < 4; i++)
        {
            var wall = New("Wall"); var box = wall.AddComponent<BoxCollider2D>();
            wall.transform.position = i < 2 ? new Vector2(i == 0 ? -12 : 12, 0) : new Vector2(0, i == 2 ? -12 : 12);
            box.size = i < 2 ? new Vector2(.45f, 24) : new Vector2(24, .45f); wall.SetActive(true); walls.Add(wall);
        }
        Physics2D.SyncTransforms(); var bounds = b.SectorRectangleArenaBounds;
        Assert.That(bounds.xMin, Is.EqualTo(-11.775f).Within(.00001f)); Assert.That(bounds.yMax, Is.EqualTo(11.775f).Within(.00001f));
        ((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext(); var a = (SectorRectangularAoE)Get(b, "sectorRectangles");
        Assert.That(a.BandCount, Is.EqualTo(10)); Assert.That(a.OuterHalfExtents, Is.EqualTo(bounds.size * .5f));
        Assert.That((Vector2)a.transform.position, Is.EqualTo(bounds.center));
        for (int x = 0; x <= 200; x++) for (int y = 0; y <= 200; y++)
        {
            var p = new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, x / 200f), Mathf.Lerp(bounds.yMin, bounds.yMax, y / 200f));
            int ring = a.RingIndex(p); Assert.That(ring, Is.InRange(0, 9));
            Assert.That(SectorRectangularAoE.OwnsBand(ring, 0) ^ SectorRectangularAoE.OwnsBand(ring, 1), Is.True);
        }
        float area = 0;
        for (int i = 0; i < a.BandCount; i++)
        {
            Vector2 outer = a.RingBoundary(i), inner = a.RingBoundary(i + 1);
            foreach (var p in new[] { outer, -outer, new Vector2(-outer.x, outer.y), new Vector2(outer.x, -outer.y), new Vector2(outer.x, 0), new Vector2(0, -outer.y) })
                Assert.That(a.RingIndex(p), Is.EqualTo(i));
            for (int side = 0; side < 4; side++) { Rect strip = SectorRectangularAoE.Strip(inner, outer, side); area += strip.width * strip.height; }
        }
        Assert.That(area, Is.EqualTo(bounds.width * bounds.height).Within(.001f)); Assert.That(a.RingIndex(Vector2.zero), Is.EqualTo(9));
        Call(b, "StopCurrentBossPatternForTransition"); Assert.That(a.IsVisible, Is.False); Assert.That(b.SectorZoneCount, Is.Zero);
        Assert.That(a.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled), Is.False);
    }
    [Test] public void MissilePhysicsSamplesLiveTargetAndCapsEachTurnWithoutReacquisition()
    {
        var b = Boss(); var player = (Transform)Get(b, "player"); player.gameObject.AddComponent<PlayerHealth>(); player.gameObject.SetActive(true);
        Call(b, "LaunchSectorMissile", 0, Vector2.down); var m = ((Bullet[])Get(b, "sectorMissiles"))[0];
        Call(m, "Awake"); m.ConfigureTimedHoming(player, BossPatternController.SectorMissileTurnRate, 32, false, BossPatternController.SectorMissileGuidanceDuration, true);
        Assert.That(Get(m, "fixedStepHoming"), Is.True); Assert.That(m.Speed, Is.EqualTo(6f));
        for (int i = 0; i < 35; i++)
        {
            Vector2 before = m.MoveDirection;
            player.position = (Vector2)m.transform.position + (Vector2)(Quaternion.Euler(0, 0, i < 18 ? 80 : -80) * before) * 4;
            Call(m, "FixedUpdate"); float signed = Vector2.SignedAngle(before, m.MoveDirection);
            Assert.That(Mathf.Abs(signed), Is.LessThanOrEqualTo(BossPatternController.SectorMissileTurnRate * Time.fixedDeltaTime + .002f));
            Assert.That(signed * (i < 18 ? 1 : -1), Is.GreaterThan(.01f), "heading follows the moved live target");
        }
        player.position = (Vector2)m.transform.position - m.MoveDirection * 2;
        Vector2 heading = m.MoveDirection; Call(m, "FixedUpdate");
        Assert.That(Vector2.Angle(heading, m.MoveDirection), Is.LessThan(3)); // hard reversal overshoots
        for (int i = 0; i < 80; i++) Call(m, "FixedUpdate");
        heading = m.MoveDirection; player.position = Vector2.up * 8; Call(m, "FixedUpdate");
        Assert.That(m.MoveDirection, Is.EqualTo(heading)); Assert.That(Get(m, "useHoming"), Is.False);
        Assert.That(Get(m, "homingTarget"), Is.Null); Assert.That(Get(m, "allowHomingReacquisition"), Is.False);
        Call(m, "OnDisable"); Call(m, "OnEnable"); Assert.That(Get(m, "fixedStepHoming"), Is.False);
    }
    [Test] public void MissileLiveTargetSteeringAllocatesNoManagedMemoryAfterWarmup()
    {
        var b = Boss(); var player = (Transform)Get(b, "player"); player.gameObject.SetActive(true);
        Call(b, "LaunchSectorMissile", 0, Vector2.down); var missile = ((Bullet[])Get(b, "sectorMissiles"))[0];
        Call(missile, "Awake");
        missile.ConfigureTimedHoming(player, BossPatternController.SectorMissileTurnRate, 32, false, 100, true);
        var step = (Action)Delegate.CreateDelegate(typeof(Action), missile, typeof(Bullet).GetMethod("FixedUpdate", Flags));
        for (int i = 0; i < 10; i++) step();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            player.position = (Vector2)missile.transform.position + new Vector2(i % 2 == 0 ? 4 : -4, 3);
            step();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.Zero);
        Assert.That(Get(missile, "useHoming"), Is.True);
    }
    [TestCase(false, 4)] [TestCase(true, 5)]
    public void DirectBurstCommitsAlternatingNonHomingShotsAndStops(bool phase2, int count)
    {
        var b = Boss(); var r = (IEnumerator)Call(b, "SectorDirectBurstRoutine", phase2, false);
        Assert.That(r.MoveNext(), Is.True); Set(b, "sectorDirectBurstActive", false); // aim timer is exercised in Play Mode
        Call(b, "AimSectorDirectBurst", 1f, true);
        var left = (Vector2)Get(b, "sectorDirectLeftDirection"); var right = (Vector2)Get(b, "sectorDirectRightDirection");
        ((Transform)Get(b, "player")).position = Vector2.right * 10;
        Assert.That(Get(b, "sectorDirectLeftDirection"), Is.EqualTo(left)); Assert.That(Get(b, "sectorDirectRightDirection"), Is.EqualTo(right));
        Assert.That(BossPatternController.SectorDirectShotCount(phase2, false), Is.EqualTo(count));
        for (int i = 0; i < count; i++)
        {
            Set(b, "sectorBurstDirection", i % 2 == 0 ? left : right);
            Set(b, "sectorBurstMuzzle", Get(b, i % 2 == 0 ? "sectorLeftMuzzle" : "sectorRightMuzzle")); Call(b, "DispatchSectorSuppressionShot");
        }
        var shots = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(x => x.SourceRoot == b.transform).ToArray();
        Assert.That(shots.Length, Is.EqualTo(count));
        foreach (var s in shots) { Assert.That(Get(s, "useHoming"), Is.False); Assert.That(s.MoveDirection == left || s.MoveDirection == right, Is.True); }
    }
    [TestCase(0)] [TestCase(53)] [TestCase(180)] [TestCase(270)]
    public void SweepOpensAwayFromActualMuzzlesThenConvergesWithoutCrossedStart(float rotation)
    {
        var b = Boss(); var visual = (Transform)Get(b, "visualRoot"); visual.rotation = Quaternion.Euler(0, 0, rotation);
        Vector2 aim = visual.up;
        for (int side = 0; side < 2; side++)
        {
            var muzzle = (Transform)Get(b, side == 0 ? "sectorLeftMuzzle" : "sectorRightMuzzle");
            Vector2 offset = muzzle.position - b.transform.position;
            Vector2 lateral = offset - aim * Vector2.Dot(offset, aim);
            var start = BossPatternController.SectorSweepDirection(aim, 0, side != 0);
            Assert.That(Vector2.Dot(start - aim, lateral), Is.GreaterThan(0), "leading shot opens outside its actual muzzle");
            float previousAngle = 41;
            for (int step = 0; step < 5; step++)
            {
                var direction = BossPatternController.SectorSweepDirection(aim, step, side != 0);
                float angle = Vector2.Angle(aim, direction); Assert.That(angle, Is.LessThan(previousAngle)); previousAngle = angle;
            }
            Assert.That(Vector2.Angle(aim, BossPatternController.SectorSweepDirection(aim, 4, side != 0)), Is.LessThan(.03f));
        }
    }
    [Test] public void ArenaClassificationAllocatesNoManagedMemoryAfterConfiguration()
    {
        var b = Boss(); ((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext();
        var area = (SectorRectangularAoE)Get(b, "sectorRectangles"); int sum = area.RingIndex(Vector2.one);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) sum += area.RingIndex(new Vector2((i % 100) * .1f, (i % 57) * .2f));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(sum, Is.GreaterThan(0)); Assert.That(allocated, Is.Zero);
    }
    [TestCase("missiles")] [TestCase("aoe")] [TestCase("sweep")] [TestCase("duplicate")]
    public void LaserSupportRejectsEveryUnapprovedOverlap(string other)
    {
        var b = Boss();
        if (other == "missiles") Call(b, "LaunchSectorMissile", 0, Vector2.down);
        if (other == "aoe") ((IEnumerator)Call(b, "AlternatingRectangularAoERoutine")).MoveNext();
        if (other == "sweep") Call(b, "SetSectorStage", BossPatternController.SectorStage.SweepForward);
        if (other == "duplicate") Set(b, "sectorDirectBurstActive", true);
        Set(b, "sectorRotating", true);
        Assert.That(((IEnumerator)Call(b, "SectorDirectBurstRoutine", true, true)).MoveNext(), Is.False);
    }
    [Test] public void LaserSupportRequiresPhaseTwoAndUsesOnlyThreeShots()
    {
        var b = Boss(); Set(b, "sectorRotating", true); Call(b, "SetSectorStage", BossPatternController.SectorStage.Partition);
        Assert.That(((IEnumerator)Call(b, "SectorDirectBurstRoutine", false, true)).MoveNext(), Is.False);
        Assert.That(((IEnumerator)Call(b, "SectorDirectBurstRoutine", true, true)).MoveNext(), Is.True);
        Assert.That(((IEnumerator)Call(b, "SectorDirectBurstRoutine", true, true)).MoveNext(), Is.False);
        Assert.That(BossPatternController.SectorDirectShotCount(true, true), Is.EqualTo(3));
        Call(b, "ClearSectorAttacks"); Assert.That(b.SectorDirectBurstStage, Is.EqualTo(BossPatternController.SectorBurstStage.Idle));
    }
}
