using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorRefineTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager oldPool, pool;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Flags).Invoke(o, args);
    GameObject New(string name) { var g = new GameObject(name); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    {
        oldPool = PoolManager.Instance; pool = New("Sector refine pool").AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, pool);
    }
    [TearDown] public void Teardown()
    {
        foreach (var root in roots)
            if (root != null && root.GetComponent<BossPatternController>() != null)
            {
                Call(root.GetComponent<BossPatternController>(), "ClearSectorAttacks");
                Call(root.GetComponent<BossPatternController>(), "EndSectorArenaCleanup");
                Bullet.ReleaseAllActiveFromSource(root.transform);
            }
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, oldPool);
    }
    BossPatternController Boss()
    {
        var parent = New("Inactive fixture");
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), parent.transform);
        roots.Add(root); var boss = root.GetComponent<BossPatternController>();
        Set(boss, "arenaCenter", Vector2.zero);
        var player = New("Aim target"); player.transform.position = new Vector3(0, 8, 0); Set(boss, "player", player.transform);
        return boss;
    }
    [Test] public void AuthoredMuzzlesMatchApprovedBodyAndKeepOnePhysicsRoot()
    {
        var c = Boss(); var body = (Transform)Get(c, "visualRoot");
        var left = (Transform)Get(c, "sectorLeftMuzzle"); var right = (Transform)Get(c, "sectorRightMuzzle");
        var center = (Transform)Get(c, "sectorChargeMuzzle");
        Assert.That(left.parent, Is.EqualTo(body)); Assert.That(right.parent, Is.EqualTo(body));
        Assert.That(left.localPosition.x, Is.LessThan(0)); Assert.That(right.localPosition.x, Is.GreaterThan(0));
        Assert.That(center.IsChildOf(body), Is.True); Assert.That(center.position.x, Is.EqualTo(c.transform.position.x));
        Assert.That(Get(c, "firePoint"), Is.EqualTo(center));
        Assert.That(c.GetComponentsInChildren<Rigidbody2D>(true).Length, Is.EqualTo(1));
        Assert.That(c.GetComponent<Rigidbody2D>().freezeRotation, Is.True);
        Assert.That(c.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
        var renderer = (SpriteRenderer)Get(c, "sectorChargeCannon");
        Assert.That(renderer.transform.localScale.x, Is.EqualTo(1.25f));
        Assert.That(renderer.sprite.texture, Is.EqualTo(((Sprite)Get(c, "sectorIdleSprite")).texture));
        foreach (string field in new[] { "sectorSideFlashPrefab", "sectorChargeFlashPrefab" })
            Assert.That(((GameObject)Get(c, field)).GetComponent<PooledMuzzleFlash>(), Is.Not.Null);
    }
    [TestCase(0, "sectorLeftMuzzle")] [TestCase(1, "sectorRightMuzzle")]
    public void SideShotsUseRealMuzzleAfterBodyRotationAndKeepCommittedDirection(int side, string field)
    {
        var c = Boss(); var body = (Transform)Get(c, "visualRoot");
        body.rotation = Quaternion.Euler(0, 0, 63);
        var muzzle = (Transform)Get(c, field);
        Call(c, "CommitSectorBurst", side);
        var direction = (Vector2)Get(c, "sectorBurstDirection");
        ((Transform)Get(c, "player")).position = Vector3.left * 8;
        Call(c, "UpdateFacing", 1f); Assert.That(body.eulerAngles.z, Is.EqualTo(63).Within(.001f));
        c.transform.position += Vector3.right * .3f;
        Call(c, "DispatchSectorSuppressionShot");
        var bullet = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Single(b => b.SourceRoot == c.transform);
        Assert.That(Vector2.Distance((Vector2)Get(bullet, "spawnPosition"), muzzle.position), Is.LessThan(.0001f));
        Assert.That(Get(c, "sectorBurstDirection"), Is.EqualTo(direction));
        var flash = Object.FindObjectsByType<PooledMuzzleFlash>(FindObjectsSortMode.None).Single(f => f.IsPlayingAt(muzzle));
        Assert.That(Vector2.Distance(flash.transform.position, muzzle.position), Is.LessThan(.0001f));
    }
    [TestCase(true)] [TestCase(false)]
    public void IntroFacesSectorVisualOnlyAndPreservesLegacyRootFacing(bool sectorControl)
    {
        var c = Boss(); Set(c, "useSectorControlSequence", sectorControl);
        var body = (Transform)Get(c, "visualRoot");
        var intro = New("Intro facing fixture").AddComponent<CoreBossIntroSequence>();
        intro.gameObject.SetActive(true);
        var target = New("Intro player"); target.transform.position = Vector3.down * 8;
        Call(intro, "CacheBossPresentation", c.gameObject);
        Set(intro, "sectorIntroPresentation", sectorControl);
        var routine = (IEnumerator)Call(intro, "MoveBossArrivalRoutine", c.gameObject, Vector3.zero, target);
        Assert.That(routine.MoveNext(), Is.EqualTo(!sectorControl));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(body.eulerAngles.z, 180)), Is.LessThan(.001f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.z, sectorControl ? 0 : 180)), Is.LessThan(.001f));
        Call(intro, "RestoreMaterializingBody");
    }
    [Test] public void ReplacementMissileWarningKeepsCenterCueHarmlessAndLocksFacing()
    {
        var c = Boss(); var body = (Transform)Get(c, "visualRoot"); body.rotation = Quaternion.Euler(0, 0, 31);
        var sequence = (IEnumerator)Call(c, "SectorMissileRoutine"); Assert.That(sequence.MoveNext(), Is.True);
        Assert.That(c.CurrentSectorStage, Is.EqualTo(BossPatternController.SectorStage.MissileWarning));
        var lane = (SectorPartitionLane)Get(c, "precisionWarning");
        Assert.That(Vector2.Distance(lane.transform.position, ((Transform)Get(c, "sectorChargeMuzzle")).position), Is.LessThan(.0001f));
        Assert.That(lane.IsDamaging, Is.False); Assert.That(c.SectorSpokeCount, Is.Zero);
        ((Transform)Get(c, "player")).position = Vector3.left * 8; Call(c, "UpdateFacing", 1f);
        Assert.That(body.eulerAngles.z, Is.EqualTo(31).Within(.001f));
    }
    [Test] public void AddedSpokesExtendWithoutDamageAndKeepOriginalFourInstances()
    {
        var c = Boss(); Call(c, "PrepareSectorEscalation");
        var lanes = (SectorPartitionLane[])Get(c, "sectorLanes"); var firstFour = lanes.Take(4).ToArray();
        var routine = (IEnumerator)Call(c, "SectorEscalationRoutine"); Assert.That(routine.MoveNext(), Is.True);
        Assert.That(c.SectorSpokeCount, Is.EqualTo(6));
        float angle = c.SectorAngle;
        foreach (float extension in new[] { .06f, .25f, .5f, 1f })
        {
            Set(c, "sectorAddedSpokeExtension", extension); Call(c, "UpdateSectorSpokes", .1f);
            Assert.That(c.SectorAngle, Is.EqualTo(angle));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(lanes[i].IsVisible, Is.True); Assert.That(lanes[i].IsDamaging, Is.False);
                float full = c.SectorBeamLength(c.transform.position, lanes[i].transform.right);
                Assert.That(lanes[i].GetComponent<BoxCollider2D>().size.x, Is.EqualTo(full * (i < 4 ? 1 : extension)).Within(.001f));
                if (i < 4) Assert.That(lanes[i], Is.SameAs(firstFour[i]));
            }
        }
        Assert.That(((IEnumerator)Call(c, "SectorEscalationRoutine")).MoveNext(), Is.False, "one escalation per enable");
    }
    [TestCase("StopCombatForDeathPresentation")] [TestCase("HandlePlayerDied")]
    [TestCase("CancelCombat")] [TestCase("OnDisable")]
    public void InterruptedGrowthClearsLanesAimLockAndMuzzleLeases(string entry)
    {
        var c = Boss(); Call(c, "PrepareSectorEscalation");
        ((IEnumerator)Call(c, "SectorEscalationRoutine")).MoveNext();
        var lanes = ((SectorPartitionLane[])Get(c, "sectorLanes")).ToArray();
        Call(c, "CommitSectorBurst", 0); Call(c, "DispatchSectorSuppressionShot");
        Call(c, entry);
        Assert.That(c.SectorSpokeCount, Is.Zero); Assert.That(Get(c, "sectorWeaponFacingLocked"), Is.False);
        Assert.That(Get(c, "sectorEscalationReady"), Is.False);
        foreach (var lane in lanes) { Assert.That(lane.IsVisible, Is.False); Assert.That(lane.IsDamaging, Is.False); }
        Assert.That(pool.GetComponentsInChildren<PooledMuzzleFlash>().Any(f => f.IsPlaying), Is.False);
        foreach (string f in new[] { "patternRoutine", "phase2ShieldCombatRoutine", "phase2TransitionRoutine" }) Assert.That(Get(c, f), Is.Null);
    }
}
