using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorIdentityTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager prior;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Flags).Invoke(o, a);
    GameObject New(string n) { var g = new GameObject(n); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    { prior = PoolManager.Instance; typeof(PoolManager).GetProperty("Instance").SetValue(null, New("Identity pool").AddComponent<PoolManager>()); }
    [TearDown] public void Cleanup()
    {
        foreach (var g in roots) if (g != null && g.TryGetComponent<BossPatternController>(out var b)) Call(b, "ClearSectorAttacks");
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, prior);
    }
    BossPatternController Boss()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive fixture").transform);
        roots.Add(root); var c = root.GetComponent<BossPatternController>(); Set(c, "arenaCenter", Vector2.zero); return c;
    }
    [Test] public void SavedEnergyAccentsAreLocalAndLeaveOriginalWhiteChassisAndScaleIntact()
    {
        var b = Boss(); var body = (SpriteRenderer)Get(b, "sectorBody"); var sprite = body.sprite;
        var cannon = (SpriteRenderer)Get(b, "sectorChargeCannon"); var color = body.color;
        Call(b, "ResetSectorIdentity"); Assert.That(b.SectorEnergyColor, Is.EqualTo(BossPatternController.SectorGreen));
        var accents = (LineRenderer[])Get(b, "sectorEnergyAccents"); Assert.That(accents.Length, Is.EqualTo(16));
        Assert.That(accents.All(x => !x.enabled && x.transform.IsChildOf(body.transform)), Is.True);
        Call(b, "SetSectorEnergy", 1f);
        Assert.That(b.SectorEnergyColor, Is.EqualTo(BossPatternController.SectorPurple));
        Assert.That(accents.All(x => x.enabled && Vector4.Distance(x.startColor, BossPatternController.SectorPurple) < .01f), Is.True);
        Assert.That(body.color, Is.EqualTo(color)); Assert.That(body.sprite, Is.SameAs(sprite));
        Assert.That(cannon.transform.localScale.x, Is.EqualTo(1.25f)); Assert.That(b.transform.localScale, Is.EqualTo(new Vector3(2,2,1)));
        Assert.That(Get(b, "sectorPhase2Sprite"), Is.Null, "future authored swap is optional");
    }
    [Test] public void ExistingFadeEstablishesPurpleOnceBeforeAnySixSpokeDamage()
    {
        var b = Boss(); Call(b, "PrepareSectorEscalation"); var routine = (IEnumerator)Call(b, "SectorEscalationRoutine"); routine.MoveNext();
        Call(b, "SampleSectorEscalationFade", 0f); Assert.That(b.SectorEmpowered, Is.False);
        Call(b, "SampleSectorEscalationFade", .23f); Assert.That((float)Get(b,"sectorEnergyBlend"), Is.EqualTo(.5f).Within(.001f));
        Call(b, "SampleSectorEscalationFade", .34f); Call(b, "UpdateSectorSpokes", 0f);
        Assert.That(b.SectorEmpowered, Is.True); Assert.That(((SectorPartitionLane[])Get(b,"sectorLanes")).All(l=>!l.IsDamaging), Is.True);
        Assert.That(((IEnumerator)Call(b,"SectorEscalationRoutine")).MoveNext(), Is.False);
        Assert.That(Get(b,"sectorEscalationWarningTime"), Is.EqualTo(1.2f));
    }
    [Test] public void PurpleLaneHasOneVisiblePresentationAndSameExactColliderEndpoint()
    {
        var b = Boss(); Call(b,"EnsureSectorSpokes",6); Call(b,"SetSectorEnergy",1f); Set(b,"sectorRotating",true); Call(b,"UpdateSectorSpokes",.1f);
        foreach(var lane in (SectorPartitionLane[])Get(b,"sectorLanes"))
        {
            var line=(LineRenderer)Get(lane,"solidVisual"); var sprite=(SpriteRenderer)Get(lane,"visual"); var box=lane.GetComponent<BoxCollider2D>();
            Assert.That(!line.enabled && sprite.enabled && box.enabled, Is.True);
            Assert.That(sprite.size.x, Is.EqualTo(box.size.x).Within(.001f));
            Assert.That(sprite.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(AssetDatabase.GetAssetPath(sprite.sprite), Does.Contain("PurpleLaserBeam"));
        }
        Assert.That(typeof(BossPatternController).GetField("sectorCounterLane",Flags), Is.Null);
        Assert.That(typeof(BossPatternController).GetMethod("BeginSectorCounterWarning",Flags), Is.Null);
    }
    [Test] public void PoolReturnRestoresGreenLaneAndEncounterIdentity()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Lane);
        var lane=PoolManager.Instance.Get(prefab,Vector3.zero,Quaternion.identity).GetComponent<SectorPartitionLane>();
        lane.Configure(Vector2.zero,Vector2.up*4,.45f,4,.75f); lane.SetEmpowerment(1);lane.Activate(.3f);PoolManager.Instance.Release(lane.gameObject);
        var reused=PoolManager.Instance.Get(prefab,Vector3.zero,Quaternion.identity).GetComponent<SectorPartitionLane>();
        reused.Configure(Vector2.zero,Vector2.up*4,.45f,4,.75f);reused.Activate(.3f);
        Assert.That(reused.Empowerment, Is.Zero); Assert.That(((SpriteRenderer)Get(reused,"visual")).enabled, Is.True);Assert.That(((LineRenderer)Get(reused,"solidVisual")).enabled, Is.False);
        var b=Boss();Call(b,"SetSectorEnergy",1f);Call(b,"ResetSectorIdentity");Assert.That(b.SectorSignedAngularSpeed, Is.EqualTo(30));
    }
    [TestCase("StopCombatForDeathPresentation")] [TestCase("HandlePlayerDied")] [TestCase("CancelCombat")] [TestCase("OnDisable")]
    public void InterruptionClearsEnergyAndLaserOwnership(string method)
    {
        var b=Boss();Call(b,"EnsureSectorSpokes",6);Call(b,"SetSectorEnergy",1f);Call(b,method);
        Assert.That(b.SectorSpokeCount, Is.Zero);Assert.That(((LineRenderer[])Get(b,"sectorEnergyAccents")).Any(l=>l.enabled), Is.False);
    }
    [Test] public void MissileEnlargementIsVisualOnlyAndSteeringRemainsFinite()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SectorPatternAuthoring.Missile);var sprite=prefab.GetComponentInChildren<SpriteRenderer>();
        Assert.That(sprite.transform.localScale, Is.EqualTo(new Vector3(.32f,.6f,2)));
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));Assert.That(prefab.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.08f));
        Assert.That(prefab.transform.Find("HostileRedOutline").localScale.x, Is.EqualTo(1.25f));
        Assert.That(SectorCoreAuthoring.PlayerMissile, Is.Not.EqualTo(SectorPatternAuthoring.Missile));
        Assert.That(BossPatternController.SectorMissileTurnRate, Is.EqualTo(140));Assert.That(BossPatternController.SectorMissileGuidanceDuration, Is.EqualTo(1.5f));
        Assert.That(BossPatternController.SectorMissileGuidanceDelay, Is.EqualTo(.25f));Assert.That(BossPatternController.SectorMissileSpeed, Is.EqualTo(6f));
        Assert.That(BossPatternController.SectorMissileTurnRate*BossPatternController.SectorMissileGuidanceDuration, Is.LessThan(270), "finite steering window ends before a full orbit");
    }
    [Test] public void OnlyOrdinaryChargerDefinitionDisablesPredictionWhileSharedUtilityStillLeads()
    {
        var d=AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset");Assert.That(d.PredictiveShotChance,Is.Zero);
        var a=New("Charger").AddComponent<EnemyAttackController>();Call(a,"ApplyDefinition",d);
        var p=New("Moving player");var rb=p.AddComponent<Rigidbody2D>();p.transform.position=Vector3.up*3;rb.linearVelocity=Vector2.right*5;
        for(int i=0;i<20;i++)Assert.That(Call(a,"ShouldUsePredictiveAim",rb), Is.False);
        Assert.That(Call(a,"GetAimDirection",Vector2.zero,p.transform,false), Is.EqualTo(Vector2.up));
        Assert.That(EnemyAttackController.PredictTargetPosition(Vector2.zero,Vector2.up*3,Vector2.right*5,16,.65f).x, Is.GreaterThan(0));
        Assert.That(d.ChargeTime,Is.EqualTo(1.5f));Assert.That(d.AttackInterval,Is.EqualTo(2.5f));Assert.That(d.ProjectileDefinition.Speed,Is.EqualTo(16));Assert.That(d.ProjectileDefinition.Damage,Is.EqualTo(8));
    }
    [TestCase(-11.67f,0)] [TestCase(11.67f,0)] [TestCase(0,11.67f)] [TestCase(0,-11.67f)]
    [TestCase(-11.67f,-11.67f)] [TestCase(11.67f,11.67f)]
    public void RegionAFramingKeepsBossAndPlayerInsideHudSafeArea(float x,float y)
    {
        Vector2 player=new Vector2(x,y);
        BossPatternController.ResolveSectorCombatFraming(player,Vector2.zero,16f/9,4.21875f,out var offset,out float size);
        Vector2 center=player+offset;
        Assert.That(Mathf.Abs(center.x)+2.1f,Is.LessThanOrEqualTo(size*16/9*.9f+.001f));
        Assert.That(Mathf.Abs(center.y)+2.1f,Is.LessThanOrEqualTo(size*.8f+.001f));
        Assert.That(Mathf.Abs(player.x-center.x)+.7f,Is.LessThanOrEqualTo(size*16/9*.9f+.001f));
        Assert.That(Mathf.Abs(player.y-center.y)+.7f,Is.LessThanOrEqualTo(size*.8f+.001f));
    }
    [Test] public void OrdinaryChargerApproachDoesNotLeadButEliteApproachStillDoes()
    {
        var ai=New("Approach").AddComponent<EnemyBaseAI>();var target=New("Moving target");var rb=target.AddComponent<Rigidbody2D>();
        target.SetActive(true);target.transform.position=Vector2.up*5;rb.linearVelocity=Vector2.right*4;Set(ai,"player",target.transform);Set(ai,"playerBody",rb);
        Set(ai,"enemyDefinition",AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset"));
        Assert.That(Call(ai,"GetPlayerInterceptPosition",.3f,2f),Is.EqualTo((Vector2)target.transform.position));
        Set(ai,"enemyDefinition",AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Elite_Charging.asset"));
        Assert.That(((Vector2)Call(ai,"GetPlayerInterceptPosition",.3f,2f)).x,Is.EqualTo(1.2f).Within(.001f));
    }

}
