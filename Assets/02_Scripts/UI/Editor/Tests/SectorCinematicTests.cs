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

public sealed class SectorCinematicTests
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
    [Test] public void ProductionProbabilityAndOneLocalShieldOwnerAreBound()
    {
        var b=Boss();Assert.That(b.SectorPurpleRotationProbability,Is.EqualTo(.5f));Assert.That(b.GetComponentsInChildren<SectorBossShieldPresentation>(true).Length,Is.EqualTo(1));
        Assert.That(typeof(SectorBossShieldPresentation).GetMethod("Update",Flags),Is.Null);
        Assert.That(typeof(SectorBossShieldPresentation).GetMethod("LateUpdate",Flags),Is.Null);
    }
    [TestCase(0f, true)] [TestCase(.4999f,true)] [TestCase(.5f,false)] [TestCase(.999f,false)]
    public void FiftyFiftyThresholdMapsToOneWholeAttackMode(float roll,bool purple)
    {Assert.That(BossPatternController.SectorModeForRoll(roll,.5f),Is.EqualTo(purple?BossPatternController.SectorRotationMode.Purple:BossPatternController.SectorRotationMode.Green));}
    [Test] public void FirstPurpleConsumesNoRandomStateAndResetsPerEncounter()
    {
        var b=Boss();var prior=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(5912);string before=JsonUtility.ToJson(UnityEngine.Random.state);
            Call(b,"CommitSectorRotationMode",true);
            Assert.That(b.CurrentSectorRotationMode,Is.EqualTo(BossPatternController.SectorRotationMode.Purple));Assert.That(b.SectorForcedPurpleRotations,Is.EqualTo(1));Assert.That(b.SectorRandomRotationSelections,Is.Zero);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(before));Assert.That(b.FirstPhase2RotationPending,Is.False);
            Call(b,"ResetSectorTransition");Assert.That(b.FirstPhase2RotationPending,Is.True);Assert.That(b.CurrentSectorRotationMode,Is.EqualTo(BossPatternController.SectorRotationMode.Green));
            Call(b,"CommitSectorRotationMode",true);Assert.That(b.SectorForcedPurpleRotations,Is.EqualTo(1));
        }
        finally{UnityEngine.Random.state=prior;}
    }
    [Test] public void LaterRotationsUseExactlyOneSeededSampleEach()
    {
        var b=Boss();Call(b,"CommitSectorRotationMode",true);var prior=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(8931);var expected=new BossPatternController.SectorRotationMode[32];
            for(int i=0;i<32;i++)expected[i]=BossPatternController.SectorModeForRoll(UnityEngine.Random.value,.5f);
            string after=JsonUtility.ToJson(UnityEngine.Random.state);UnityEngine.Random.InitState(8931);
            for(int i=0;i<32;i++){Call(b,"CommitSectorRotationMode",true);Assert.That(b.CurrentSectorRotationMode,Is.EqualTo(expected[i]));}
            Assert.That(b.SectorRandomRotationSelections,Is.EqualTo(32));Assert.That(b.SectorForcedPurpleRotations,Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(after));Assert.That(expected.Distinct().Count(),Is.EqualTo(2));
        }
        finally{UnityEngine.Random.state=prior;}
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void EveryTransitionBoundaryForcesRotationBeforeNormalPhaseTwoSlots(int cycle)
    {
        var b=Boss();Set(b,"sectorCycle",cycle);var r=(IEnumerator)Call(b,"SectorControlCycleRoutine",true);r.MoveNext();
        Assert.That(r.Current.GetType().Name,Does.Contain("SectorRotationRoutine"));Assert.That(b.SectorCycle,Is.EqualTo(cycle));
        var rotation=(IEnumerator)r.Current;rotation.MoveNext();
        Assert.That(b.CurrentSectorStage,Is.EqualTo(BossPatternController.SectorStage.Warning));Assert.That(b.SectorSpokeCount,Is.EqualTo(6));
        Assert.That(b.CurrentSectorRotationMode,Is.EqualTo(BossPatternController.SectorRotationMode.Purple));Assert.That(b.SectorForcedPurpleRotations,Is.EqualTo(1));
        Assert.That(((SectorPartitionLane[])Get(b,"sectorLanes")).All(l=>!l.IsDamaging),Is.True);
    }
    [TestCase(false)] [TestCase(true)]
    public void BothModesKeepPurpleBossIdentityAndSixSharedLanes(bool purple)
    {
        var b=Boss();Call(b,"SetSectorEnergy",1f);Call(b,"CommitSectorRotationMode",true);
        Set(b,"sectorPurpleRotationProbability",purple?1f:0f);Call(b,"CommitSectorRotationMode",true);
        Call(b,"EnsureSectorSpokes",6);var body=(SpriteRenderer)Get(b,"sectorBody");var sprite=body.sprite;
        var random=JsonUtility.ToJson(UnityEngine.Random.state);
        for(int i=0;i<20;i++)Call(b,"UpdateSectorSpokes",.02f);
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));Assert.That(b.SectorSignedAngularSpeed,Is.EqualTo(purple?-30:30));
        Assert.That(b.SectorEmpowered,Is.True);Assert.That(body.sprite,Is.SameAs(sprite));Assert.That(b.SectorSpokeCount,Is.EqualTo(6));
        var lanes=(SectorPartitionLane[])Get(b,"sectorLanes");
        foreach(var lane in lanes){Assert.That(lane.Empowerment,Is.EqualTo(purple?1:0));Assert.That(lane.IsDamaging,Is.False);}
        Set(b,"sectorRotating",true);Call(b,"UpdateSectorSpokes",.1f);
        foreach(var lane in lanes)
        {
            var visual=(SpriteRenderer)Get(lane,"visual");Assert.That(AssetDatabase.GetAssetPath(visual.sprite),Does.Contain(purple?"PurpleLaserBeam":"VFX_Sector_LaserBeam"));
            Assert.That(lane.IsDamaging,Is.True);Assert.That(lane.GetComponent<BoxCollider2D>().size.x,Is.EqualTo(visual.size.x));
        }
    }
    [Test] public void IntroShieldIsBackedByTheDamageAuthorityAndReleasesBeforeCombat()
    {
        var b=Boss();var health=b.GetComponent<EnemyHealth>();Set(health,"bossPatternController",b);Set(health,"maxHp",140f);Set(health,"currentHp",140f);
        b.ShowSectorIntroShield();Assert.That(b.SectorShieldVisible,Is.False);
        b.BeginSectorIntroProtection();b.ShowSectorIntroShield();Assert.That(b.SectorShieldVisible,Is.True);
        health.TakeDamage(25);Assert.That(health.CurrentHp,Is.EqualTo(140));
        var view=(SectorBossShieldPresentation)Get(b,"sectorShield");Assert.That(view.IsHitVisible,Is.False);
        view.SampleRelease(0);Assert.That(view.IsProtectedVisualVisible,Is.False);Assert.That(view.IsReleaseVisible,Is.True);
        view.SampleRelease(.32f);Assert.That(view.IsReleaseVisible,Is.False);
        Call(b,"ClearSectorShield");health.TakeDamage(5);Assert.That(health.CurrentHp,Is.EqualTo(135));
    }
    [TestCase("CancelCombat")] [TestCase("HandlePlayerDied")] [TestCase("StopCombatForDeathPresentation")] [TestCase("OnDisable")]
    public void InterruptionClearsProtectionAndSavedShieldRenderers(string entry)
    {
        var b=Boss();b.BeginSectorIntroProtection();b.ShowSectorIntroShield();Call(b,entry);
        Assert.That(b.SectorShieldVisible||b.SectorShieldReleasing||b.SectorIntroProtected,Is.False);Assert.That(b.FirstPhase2RotationPending,Is.True);
    }
    [TestCase("VFX_Sector_BossShield",8)] [TestCase("VFX_Sector_BossShieldHit",8)] [TestCase("VFX_Sector_BossShieldRelease",12)]
    public void SuppliedShieldSheetsAreByteIdenticalPixelImportedAndBound(string family,int count)
    {
        string path=SectorCinematicAuthoring.Art+family+".png";
        Assert.That(File.ReadAllBytes(path),Is.EqualTo(File.ReadAllBytes(SectorCinematicAuthoring.Source+family+".png")));
        var frames=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();Assert.That(frames.Length,Is.EqualTo(count));
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
        var b=Boss();var view=(SectorBossShieldPresentation)Get(b,"sectorShield");
        string field=family.EndsWith("Hit")?"hit":family.EndsWith("Release")?"release":"shell";
        var effect=(SectorSupportVfx)Get(view,field);Assert.That(((Sprite[])Get(effect,"frames")).All(x=>AssetDatabase.GetAssetPath(x)==path),Is.True);
        Assert.That(((Sprite[])Get(effect,"purpleFrames")).Length,Is.EqualTo(count/2));
        Assert.That(view.GetComponentsInChildren<Collider2D>(true),Is.Empty);
        Assert.That(view.transform.localScale.x,Is.EqualTo(view.transform.localScale.y));
    }
}
