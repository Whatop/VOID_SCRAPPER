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

public sealed class SectorTransitionTests
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
    [TestCase("SectorSweepRoutine")] [TestCase("SectorMissileRoutine")] [TestCase("AlternatingRectangularAoERoutine")]
    [TestCase("SectorDirectBurstRoutine")] [TestCase("SectorRotationRoutine")]
    public void ThresholdMarksOnePendingRequestWithoutChangingActiveOwnership(string attack)
    {
        var b=Boss(); var health=b.GetComponent<EnemyHealth>(); Set(b,"enemyHealth",health); Set(health,"currentHp",70f);Set(health,"maxHp",140f);
        IEnumerator routine=(IEnumerator)(attack=="SectorDirectBurstRoutine"?Call(b,attack,false,false):attack=="SectorRotationRoutine"?Call(b,attack,false):Call(b,attack));
        routine.MoveNext();var stage=b.CurrentSectorStage; int missiles=b.SectorActiveMissiles,bands=b.SectorZoneCount,spokes=b.SectorSpokeCount;
        for(int i=0;i<3;i++)Call(b,"UpdatePhase");
        Assert.That(b.SectorPhase2Pending,Is.True);Assert.That(b.SectorTransitionRunning,Is.False);Assert.That(b.SectorTransitionCount,Is.Zero);
        Assert.That(b.CurrentSectorStage,Is.EqualTo(stage));Assert.That(b.SectorZoneCount,Is.EqualTo(bands));Assert.That(b.SectorSpokeCount,Is.EqualTo(spokes));Assert.That(b.SectorActiveMissiles,Is.EqualTo(missiles));
        Assert.That(Get(b,"phase2TransitionRoutine"),Is.Null);Assert.That(Get(b,"phase2TransitionStarted"),Is.False);
    }
    [TestCase("CancelCombat")] [TestCase("HandlePlayerDied")] [TestCase("StopCombatForDeathPresentation")] [TestCase("OnDisable")]
    public void TeardownClearsPendingAndTransitionOwnership(string entry)
    {
        var b=Boss();Set(b,"sectorPhase2Pending",true);Set(b,"sectorTransitionRunning",true);Call(b,entry);
        Assert.That(b.SectorPhase2Pending||b.SectorTransitionRunning,Is.False);Assert.That(b.SectorRelayCount,Is.Zero);
    }
    [TestCase(3f)] [TestCase(5f)] [TestCase(8f)]
    public void SixEvenShotsCrossFrozenTargetFromBothActualMuzzles(float distance)
    {
        var b=Boss();var target=(Transform)Get(b,"player");target.position=Vector2.down*distance;Vector2 committed=target.position;
        Call(b,"CommitSectorSweep");target.position=Vector2.right*9;
        for(int i=0;i<6;i++)Call(b,"DispatchSectorSweepPair",i);
        var shots=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(x=>x.SourceRoot==b.transform).ToArray();
        Assert.That(shots.Length,Is.EqualTo(12));
        foreach(string field in new[]{"sectorLeftMuzzle","sectorRightMuzzle"})
        {
            Vector2 origin=((Transform)Get(b,field)).position;
            var side=shots.Where(x=>Vector2.Distance((Vector2)Get(x,"spawnPosition"),origin)<.001f).ToArray();Assert.That(side.Length,Is.EqualTo(6));
            var angles=side.Select(x=>Vector2.SignedAngle(committed-origin,x.MoveDirection)).OrderBy(x=>x).ToArray();
            Assert.That(angles.Any(x=>Mathf.Abs(x)<.03f),Is.True,"center target is crossed at step 4");
            Assert.That(angles.First(),Is.LessThan(0));Assert.That(angles.Last(),Is.GreaterThan(0));
            for(int i=1;i<6;i++)Assert.That(angles[i]-angles[i-1],Is.EqualTo(10).Within(.03f));
            Assert.That(side.All(x=>!(bool)Get(x,"useHoming")),Is.True);
        }
    }
    [TestCase("Sector_LaserRelay")] [TestCase("VFX_Sector_RelayDeploy")] [TestCase("VFX_Sector_RelayDisconnect")]
    [TestCase("VFX_Sector_RelayReconnect")] [TestCase("VFX_Sector_RelayStabilize")] [TestCase("VFX_Sector_BossArrivalMarker")]
    public void ApprovedArtworkIsByteIdenticalAndPixelImported(string family)
    {
        string source=family.EndsWith("ArrivalMarker")?SectorTransitionAuthoring.ArrivalSource:SectorTransitionAuthoring.Source;
        Assert.That(File.ReadAllBytes(SectorTransitionAuthoring.Art+family+".png"),Is.EqualTo(File.ReadAllBytes(source+family+".png")));
        var importer=(TextureImporter)AssetImporter.GetAtPath(SectorTransitionAuthoring.Art+family+".png");
        Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(32));
    }
    [Test] public void RelayReusesOnePrefabAndResetsAllPresentationWithoutUpdate()
    {
        var b=Boss();var prefab=(GameObject)Get(b,"laserManagerShipPrefab");Assert.That(Get(b,"phase2LaserManagerShipPrefab"),Is.SameAs(prefab));
        Assert.That(AssetDatabase.GetAssetPath(prefab),Is.EqualTo(SectorTransitionAuthoring.Relay));
        var obj=PoolManager.Instance.Get(prefab,Vector3.zero,Quaternion.identity);roots.Add(obj);var relay=obj.GetComponent<SectorRelayPresentation>();
        relay.ResetPresentation();Assert.That(relay.IsPurple,Is.False);relay.SampleDeploy(0);Assert.That(relay.IsBodyVisible,Is.False);
        relay.SampleDeploy(.2f);Assert.That(relay.IsBodyVisible,Is.True);relay.SampleConversion(.16f);Assert.That(relay.IsPurple,Is.True);
        Assert.That(((SpriteRenderer)Get(relay,"body")).color,Is.EqualTo(Color.white));
        relay.ResetPresentation();Assert.That(relay.IsPurple,Is.False);Assert.That(relay.GetComponentsInChildren<SectorSupportVfx>().All(x=>!x.Visual.enabled),Is.True);
        Assert.That(typeof(SectorRelayPresentation).GetMethod("Update",Flags),Is.Null);Assert.That(typeof(SectorRelayPresentation).GetMethod("LateUpdate",Flags),Is.Null);
    }
    [Test] public void ArrivalIsRevisionTwoOneShotNoCollisionAndNoOldMarker()
    {
        var b=Boss();var marker=(SectorSupportVfx)Get(b,"sectorArrivalMarker");Assert.That(marker,Is.Not.Null);
        b.PlaySectorSpawnMarker(Vector3.zero,.4f);Assert.That(marker.Visual.sprite.rect.size,Is.EqualTo(new Vector2(128,128)));
        Assert.That(Get(b,"sectorSpawnMarker"),Is.Null);Assert.That(marker.GetComponentsInChildren<Collider2D>(),Is.Empty);
        Assert.That(marker.Visual.color,Is.EqualTo(Color.white));b.SampleSectorArrivalMarker(.4f);Assert.That(marker.Visual.enabled,Is.True);
        b.SampleSectorArrivalMarker(.49f);Assert.That(marker.Visual.enabled,Is.False);b.PlaySectorSpawnMarker(Vector3.one,.4f);Assert.That(marker.Visual.enabled,Is.True);
        b.ClearSectorSpawnEffects();Assert.That(marker.Visual.enabled,Is.False);Assert.That(Get(b,"phase2ShieldVisualRoot"),Is.Null);
    }
    [Test] public void ActiveLaserDisconnectIsHarmlessImmediatelyAndFullyClears()
    {
        var b=Boss();Call(b,"EnsureSectorSpokes",4);var lanes=(SectorPartitionLane[])Get(b,"sectorLanes");
        var lane=lanes[0];lane.Activate(.5f);Assert.That(lane.IsDamaging,Is.True);lane.Disconnect(0);Assert.That(lane.IsDamaging,Is.False);
        Assert.That(lane.GetComponent<Collider2D>().enabled,Is.False);lane.Disconnect(1);Assert.That(((SpriteRenderer)Get(lane,"visual")).enabled,Is.False);
        lane.Clear();Assert.That(((SpriteRenderer)Get(lane,"visual")).color,Is.EqualTo(Color.white));
    }
    [Test] public void PendingDuringSupportGapSkipsBurstBeforeRecovery()
    {
        var b=Boss();var health=b.GetComponent<EnemyHealth>();Set(health,"maxHp",140f);Set(health,"currentHp",140f);
        Set(b,"sectorCycle",1);var routine=(IEnumerator)Call(b,"SectorControlCycleRoutine",false);
        Assert.That(routine.MoveNext(),Is.True);Assert.That(routine.Current.GetType().Name,Does.Contain("SectorMissileRoutine"));
        Assert.That(routine.MoveNext(),Is.True);Assert.That(routine.Current.GetType().Name,Does.Contain("FinishPendingSectorLaser"));
        Assert.That(routine.MoveNext(),Is.True);Assert.That(routine.Current,Is.TypeOf<WaitForSeconds>());
        Set(b,"sectorPhase2Pending",true);Assert.That(routine.MoveNext(),Is.True);
        Assert.That(routine.Current,Is.Null,"go directly to recovery, no support attack child");Assert.That(b.SectorDirectBurstStage,Is.EqualTo(BossPatternController.SectorBurstStage.Idle));
    }
    [Test] public void ReusedContainmentReturnsToSavedGreenPresentation()
    {
        var b=Boss();var prefab=(SectorBarrierVisual)Get(b,"sectorBarrierVisualPrefab");
        var obj=PoolManager.Instance.Get(prefab.gameObject,Vector3.zero,Quaternion.identity);roots.Add(obj);var v=obj.GetComponent<SectorBarrierVisual>();
        var wall=New("Wall").AddComponent<BossArenaLaserWall>();var beam=(SpriteRenderer)Get(v,"beam");
        var green=beam.sprite;var scale=beam.transform.localScale;var color=beam.color;v.Configure(wall,20);
        v.SamplePurpleConnection(1,.2f,true);Assert.That(AssetDatabase.GetAssetPath(beam.sprite),Does.Contain("PurpleLaserBeam"));
        v.Configure(wall,20);Assert.That(beam.sprite,Is.SameAs(green));Assert.That(beam.transform.localScale,Is.EqualTo(scale));Assert.That(beam.color,Is.EqualTo(color));
        v.SampleDisconnect(1);Assert.That(beam.enabled,Is.False);v.Release();
    }
}
