using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorDeploymentTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> roots = new List<GameObject>(); PoolManager prior;
    static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    GameObject New(string name) { var g = new GameObject(name); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup() { prior = PoolManager.Instance; typeof(PoolManager).GetProperty("Instance").SetValue(null, New("Deployment pool").AddComponent<PoolManager>()); }
    [TearDown] public void Cleanup()
    {
        foreach (var root in roots) if (root != null && root.TryGetComponent<BossPatternController>(out var b)) Call(b, "ClearSectorAttacks");
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, prior);
    }
    BossPatternController Boss()
    {
        var g = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive boss parent").transform);
        roots.Add(g); var boss = g.GetComponent<BossPatternController>(); Set(boss, "player", New("Player").transform); return boss;
    }
    SectorRectangularAoE Area(out PlayerHealth target)
    {
        var g = PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>(SectorCoreAuthoring.Rectangle), Vector3.zero, Quaternion.identity);
        roots.Add(g); var area = g.GetComponent<SectorRectangularAoE>(); target = New("Target").AddComponent<PlayerHealth>(); target.SetMaxHp(100, true);
        area.Configure(Vector2.zero, new Vector2(6, 6.5f), 1.2f, 4, target); return area;
    }
    [Test] public void SixCoreOriginsHaveSixDistinctAuthoredDirectionsAndThreeSemanticGroups()
    {
        var b = Boss(); ((Transform)Get(b, "visualRoot")).rotation = Quaternion.Euler(0, 0, 73);
        var directions = new HashSet<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            Assert.That(Call(b, "LaunchSectorMissile", i, Vector2.down), Is.True);
            directions.Add(b.SectorDeploymentDirection(i));
            Assert.That(BossPatternController.MissileGroup(i), Is.EqualTo((BossPatternController.SectorDeploymentGroup)(i / 2)));
        }
        Assert.That(directions.Count, Is.EqualTo(6)); Assert.That(b.SectorActiveMissiles, Is.EqualTo(6));
        foreach (var m in (Bullet[])Get(b, "sectorMissiles"))
        { Assert.That((Vector2)Get(m, "spawnPosition"), Is.EqualTo(b.SectorMissileCorePosition)); Assert.That(m.Speed, Is.EqualTo(6f)); Assert.That(m.GetComponent<Collider2D>().enabled, Is.False); }
        Assert.That(Call(b, "LaunchSectorMissile", 0, Vector2.up), Is.False);
    }
    [Test] public void DeploymentIsHarmlessAndGuidanceStartsContinuouslyOnlyAfterDeployment()
    {
        var b = Boss(); Call(b, "LaunchSectorMissile", 0, Vector2.down);
        var m = ((Bullet[])Get(b, "sectorMissiles"))[0]; var born = (float[])Get(b, "sectorMissileBorn"); var heading = m.MoveDirection;
        born[0] = Time.time - .15f; Call(b, "UpdateSectorSupport", .01f);
        Assert.That(m.GetComponent<Collider2D>().enabled, Is.False); Assert.That(Get(m, "useHoming"), Is.False);
        born[0] = Time.time - .23f; Call(b, "UpdateSectorSupport", .01f);
        Assert.That(m.GetComponent<Collider2D>().enabled, Is.True); Assert.That(Get(m, "useHoming"), Is.False);
        born[0] = Time.time - .26f; Call(b, "UpdateSectorSupport", .01f);
        Assert.That(Get(m, "useTimedHoming"), Is.True); Assert.That(m.MoveDirection, Is.EqualTo(heading));
        Assert.That(Get(m, "homingAngle"), Is.EqualTo(140f)); Assert.That(Get(m, "allowHomingReacquisition"), Is.False);
        Assert.That(BossPatternController.SectorMissileSpeed / (BossPatternController.SectorMissileTurnRate * Mathf.Deg2Rad), Is.EqualTo(2.45553f).Within(.001f));
    }
    [Test] public void ReuseResetsDeploymentTargetTrailAndPreservesVisualScale()
    {
        var b = Boss(); Call(b, "LaunchSectorMissile", 0, Vector2.down);
        var m = ((Bullet[])Get(b, "sectorMissiles"))[0]; var p = m.GetComponent<SectorMissilePresentation>();
        Call(m, "Awake"); // Inactive EditMode pool parent does not dispatch Unity lifecycle callbacks.
        p.Sample(1); m.ConfigureTimedHoming((Transform)Get(b, "player"), 40, 20, false, .8f); m.ForceRelease(); Call(m, "OnDisable"); Call(p, "OnDisable");
        Assert.That(p.Deployed, Is.False); Assert.That(Get(m, "homingTarget"), Is.Null); Assert.That(m.GetComponent<TrailRenderer>().positionCount, Is.Zero);
        Call(b, "LaunchSectorMissile", 1, Vector2.up);
        var reused = ((Bullet[])Get(b, "sectorMissiles")).First(x => x != null && x.gameObject.activeSelf);
        Assert.That(reused.GetComponent<Collider2D>().enabled, Is.False); Assert.That(Get(reused, "useHoming"), Is.False);
        Assert.That(reused.transform.Find("VisualRoot").localScale, Is.EqualTo(new Vector3(.32f,.6f,2)));
        Assert.That(reused.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.08f));
    }
    [Test] public void EntireInteriorIncludingCenterAxesCornersAndBoundariesHasExactlyOneParity()
    {
        var a = Area(out _); var outer = a.OuterHalfExtents;
        for (int x = 0; x <= 160; x++) for (int y = 0; y <= 160; y++)
        {
            var point = new Vector2(Mathf.Lerp(-outer.x, outer.x, x / 160f), Mathf.Lerp(-outer.y, outer.y, y / 160f));
            int ring = a.RingIndex(point);
            Assert.That(ring, Is.InRange(0, 4)); Assert.That(SectorRectangularAoE.OwnsBand(ring, 0) ^ SectorRectangularAoE.OwnsBand(ring, 1), Is.True);
        }
        Assert.That(a.RingIndex(Vector2.zero), Is.EqualTo(4));
        for (int boundary = 0; boundary < a.BandCount; boundary++)
        {
            var half = a.RingBoundary(boundary); int expected = Mathf.Min(boundary, a.BandCount - 1);
            foreach (var point in new[] { new Vector2(half.x,0),new Vector2(0,half.y),half,-half,new Vector2(-half.x,half.y),new Vector2(half.x,-half.y) })
                Assert.That(a.RingIndex(point), Is.EqualTo(expected));
        }
        Assert.That(a.RingIndex(outer + Vector2.one * .0001f), Is.EqualTo(-1));
    }
    [Test] public void SharedVisualStripsExactlyCoverOuterRectangleWithoutGapsOrOverlappingArea()
    {
        var a = Area(out _); float sum = 0;
        for (int band = 0; band < a.BandCount; band++)
        {
            var lo = a.RingBoundary(band + 1); var hi = a.RingBoundary(band);
            for (int side = 0; side < 4; side++) { var r = SectorRectangularAoE.Strip(lo, hi, side); sum += r.width * r.height; }
        }
        Assert.That(sum, Is.EqualTo(4 * a.OuterHalfExtents.x * a.OuterHalfExtents.y).Within(.001f));
    }
    [TestCase(0)] [TestCase(1)]
    public void ReleaseSpritesStartOnTheSingleDamageEventAndClear(int phase)
    {
        var a = Area(out var target); var lo = a.RingBoundary(phase + 1); var hi = a.RingBoundary(phase);
        target.transform.position = new Vector2((lo.x + hi.x) * .5f, 0);
        a.Warn(phase, 1); Assert.That(target.CurrentHp, Is.EqualTo(100)); a.Explode(phase);
        Assert.That(target.CurrentHp, Is.EqualTo(96)); Set(target, "invincibleTimer", 0f); a.Explode(phase); Assert.That(target.CurrentHp, Is.EqualTo(96));
        var renderers = (SpriteRenderer[])Get(a, "releaseEdges"); Assert.That(renderers.Count(r => r.enabled), Is.EqualTo(phase == 0 ? 12 : 8));
        a.ReleaseVisual(phase, 1); Assert.That(renderers.Any(r => r.enabled), Is.False); a.Clear(); Assert.That(a.RingIndex(Vector2.zero), Is.EqualTo(-1));
    }
    [Test] public void BarrierGrowthPreservesThicknessAndDefersContactEvenWhenWallFollowsEmitters()
    {
        var start = New("Start").transform; var end = New("End").transform; end.position = Vector2.right * 24;
        var wall = New("Wall").AddComponent<BossArenaLaserWall>();
        wall.InitializeFollowBetween(start,end,.45f,true,4,.5f,null,Color.green,"Default",0,"Default");
        var root = PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>(SectorDeploymentAuthoring.Barrier),Vector3.zero,Quaternion.identity); roots.Add(root);
        var visual = root.GetComponent<SectorBarrierVisual>(); visual.Configure(wall,24);
        foreach (float progress in new[] { 0f,.3f,.6f,1f })
        {
            visual.SetFormation(progress,.08f+progress*.45f); Call(wall,"LateUpdate");
            var beam = (SpriteRenderer)Get(visual,"beam"); var box = wall.GetComponent<BoxCollider2D>();
            Assert.That(beam.size.x, Is.EqualTo(24*progress).Within(.0001f)); Assert.That(beam.transform.localScale.y, Is.EqualTo(.6f));
            Assert.That(box.size, Is.EqualTo(new Vector2(24,.45f))); Assert.That(box.enabled, Is.EqualTo(progress>=1));
            float front = beam.transform.localPosition.x + beam.size.x*.5f;
            Assert.That(front, Is.EqualTo(-12+24*progress).Within(.0001f));
        }
        visual.Release(); roots.Remove(root);
        var again = PoolManager.Instance.Get(AssetDatabase.LoadAssetAtPath<GameObject>(SectorDeploymentAuthoring.Barrier),Vector3.zero,Quaternion.identity); roots.Add(again);
        again.GetComponent<SectorBarrierVisual>().Configure(wall,24); again.GetComponent<SectorBarrierVisual>().SetFormation(0,0);
        Assert.That(wall.GetComponent<Collider2D>().enabled, Is.False);
    }
}
