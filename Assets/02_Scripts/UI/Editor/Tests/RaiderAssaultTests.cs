using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RaiderAssaultTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    readonly List<GameObject> objects=new List<GameObject>(); PoolManager oldPool,pool; AudioManager oldAudio;
    static GameObject Production=>AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss);
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    GameObject New(string n){var g=new GameObject(n);g.SetActive(false);objects.Add(g);return g;}
    PirateCommanderBossController Fixture()
    {
        var c=New("Commander").AddComponent<PirateCommanderBossController>();
        Set(c,"enemyHealth",c.GetComponent<EnemyHealth>());Set(c,"body",c.GetComponent<Rigidbody2D>());
        var v=New("Visual");v.transform.SetParent(c.transform,false);Set(c,"visualRoot",v.transform);Set(c,"visualRenderer",v.AddComponent<SpriteRenderer>());
        var s=new SerializedObject(Production.GetComponent<PirateCommanderBossController>());
        foreach(string f in new[]{"projectileDefinition","idleSprite","weaponsHotSprite","criticalSprite","heavyMuzzlePrefab","weaponsHotPrefab","damageSparksPrefab","criticalLoopPrefab"})Set(c,f,s.FindProperty(f).objectReferenceValue);
        Set(c,"leftMuzzle",v.transform);Set(c,"rightMuzzle",v.transform);return c;
    }
    [SetUp] public void Setup(){oldPool=PoolManager.Instance;oldAudio=AudioManager.Instance;pool=New("Pool").AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);}
    [TearDown] public void Cleanup()
    {
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,oldPool);
        typeof(PirateCommanderBossController).GetProperty("ActiveAssaultEncounter").SetValue(null,null);
        if(oldAudio==null&&AudioManager.Instance!=null)Object.DestroyImmediate(AudioManager.Instance.gameObject);
    }
    [Test] public void ExistingSingle125HpAnd50PercentAuthorityAndRewardBindingRemain()
    {
        var g=Production;Assert.That(g.GetComponentsInChildren<EnemyHealth>(true).Length,Is.EqualTo(1));Assert.That(g.GetComponent<EnemyHealth>().MaxHp,Is.EqualTo(125));
        var s=new SerializedObject(g.GetComponent<PirateCommanderBossController>());Assert.That(s.FindProperty("maxHp").floatValue,Is.EqualTo(125));Assert.That(s.FindProperty("phase2HpRatio").floatValue,Is.EqualTo(.5f));
        var d=new SerializedObject(g.GetComponent<BossDummyController>());Assert.That(d.FindProperty("enemyHealth").objectReferenceValue,Is.SameAs(g.GetComponent<EnemyHealth>()));
        Assert.That(AssetDatabase.GetAssetPath(d.FindProperty("campaignDefinition").objectReferenceValue),Does.EndWith("BossCampaign_Region1_Repeat_Raider.asset"));Assert.That(d.FindProperty("grantSelectableBossReward").boolValue,Is.True);
    }
    [TestCase("leftMuzzle",-1)] [TestCase("rightMuzzle",1)]
    public void MountsFollowVisualFacingAndRemainAtAuthoredBarrels(string field,int side)
    {
        var c=Production.GetComponent<PirateCommanderBossController>();var t=(Transform)Get(c,field);
        Assert.That(t.parent,Is.SameAs(Get(c,"visualRoot")));Assert.That(t.localPosition.x*side,Is.GreaterThan(.5f));Assert.That(t.localPosition.y,Is.InRange(.6f,.75f));
        Assert.That(t.localScale,Is.EqualTo(Vector3.one));
    }
    [TestCase(0)] [TestCase(1)]
    public void SingleShotPreservesSourceDamageSpeedAndMuzzleDirection(int mount)
    {
        var c=Fixture();Call(c,"AcquireViews");var v=(Transform)Get(c,mount==0?"leftMuzzle":"rightMuzzle");v.position=new Vector3(3,2,0);
        Call(c,"Fire",mount,Vector2.left);
        var bullets=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None);Bullet own=null;int count=0;
        foreach(var b in bullets)if(b.SourceRoot==c.transform){count++;own=b;objects.Add(b.gameObject);}
        Assert.That(count,Is.EqualTo(1));Assert.That(own.Speed,Is.EqualTo(8));Assert.That(Get(own,"damage"),Is.EqualTo(2f));Assert.That(own.transform.position,Is.EqualTo(v.position));
        var views=(PhaseCombatVfx[])Get(c,"views");Assert.That(views[mount].IsVisible,Is.True);Assert.That(Vector2.Dot(views[mount].transform.right,Vector2.left),Is.GreaterThan(.999f));
        Assert.That(Vector2.Dot(v.up,Vector2.left),Is.GreaterThan(.999f));
        Assert.That(c.ShotsFired,Is.EqualTo(1));Assert.That(c.LastMount,Is.EqualTo(mount));
    }
    [TestCase(0,0)] [TestCase(3,0)] [TestCase(40,20)]
    public void BurstCommitIsBoundedAndDoesNotFollowLaterMovement(float vx,float vy)
    {
        var c=Fixture();var p=New("Player").AddComponent<PlayerHealth>();p.transform.position=new Vector3(0,-4,0);var rb=p.GetComponent<Rigidbody2D>();rb.linearVelocity=new Vector2(vx,vy);
        Set(c,"playerHealth",p);Set(c,"playerBody",rb);Call(c,"CommitAim");Vector2 committed=c.CommittedTarget;
        Assert.That(Vector2.Distance(committed,p.transform.position),Is.LessThanOrEqualTo(new Vector2(vx,vy).magnitude*.22f+.001f));
        p.transform.position+=Vector3.right*4;Assert.That(c.CommittedTarget,Is.EqualTo(committed));
        if(vx==0&&vy==0)Assert.That(committed,Is.EqualTo(new Vector2(0,-4)));
    }
    [TestCase(0,0,false)] [TestCase(0,3,true)] [TestCase(5,0,true)]
    public void AttackRunRejectsPlayerCrossing(float x,float y,bool clear)
    {Assert.That(PirateCommanderBossController.HasClearRun(Vector2.left*2,Vector2.right*2,new Vector2(x,y),1.9f),Is.EqualTo(clear));}
    [TestCase(-100,-100)] [TestCase(100,100)] [TestCase(4,-6)]
    public void MovementDestinationRetainsHullMargin(float x,float y)
    {var c=Fixture();var point=(Vector2)Call(c,"ClampToArena",new Vector2(x,y));Assert.That(Mathf.Abs(point.x),Is.LessThanOrEqualTo(6.901f));Assert.That(Mathf.Abs(point.y),Is.LessThanOrEqualTo(6.901f));}
    [Test] public void FiftyPercentTriggersOnceAndCriticalBodyRestoresAfterHotRecovery()
    {
        var c=Fixture();Set(c,"combatActive",true);Call(c,"HandleHealthChanged",null,62.5f,125f);Assert.That(Get(c,"phaseTransitionPending"),Is.True);
        Set(c,"phase2",true);Set(c,"phaseTransitionPending",false);Call(c,"HandleHealthChanged",null,20f,125f);Assert.That(Get(c,"phaseTransitionPending"),Is.False);
        Call(c,"SetHot",true);var visual=(SpriteRenderer)Get(c,"visualRenderer");Assert.That(visual.sprite,Is.SameAs(Get(c,"criticalSprite")));
        Call(c,"SetHot",false);Assert.That(visual.sprite,Is.SameAs(Get(c,"criticalSprite")));Assert.That(c.IsWeaponsHot,Is.False);
    }
    [TestCase("StopCombat")] [TestCase("OnDisable")] [TestCase("HandlePlayerDied")]
    public void CleanupStopsOwnedShotsViewsMovementAndHotState(string entry)
    {
        var c=Fixture();Call(c,"AcquireViews");var views=(PhaseCombatVfx[])Get(c,"views");var copies=(PhaseCombatVfx[])views.Clone();
        Call(c,"Fire",0,Vector2.down);Call(c,"SetHot",true);Call(c,entry);
        Assert.That(c.Stage,Is.EqualTo(PirateCommanderBossController.AssaultStage.Stopped));Assert.That(c.IsWeaponsHot||c.IsCombatActive,Is.False);
        foreach(var view in copies){Assert.That(view.IsVisible,Is.False);Assert.That(view.gameObject.activeSelf,Is.False);}
        foreach(var b in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))Assert.That(b.SourceRoot,Is.Not.SameAs(c.transform));
        Assert.That(c.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));Assert.That(Get(c,"combatRoutine"),Is.Null);
    }
    [TestCase("Muzzle")] [TestCase("Hot")] [TestCase("Critical")] [TestCase("Sparks")]
    public void ApprovedViewsAreColliderFreeAndReusableFromPool(string name)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Vfx+name+".prefab");Assert.That(prefab.GetComponentsInChildren<Collider2D>(true),Is.Empty);
        var g=pool.Get(prefab,Vector3.zero,Quaternion.identity);objects.Add(g);var v=g.GetComponent<PhaseCombatVfx>();v.Sample(0);Call(v,"OnDisable");pool.Release(g);Assert.That(v.IsVisible,Is.False);
        var again=pool.Get(prefab,Vector3.one,Quaternion.identity);Assert.That(again,Is.SameAs(g));pool.Release(again);
    }
    [Test] public void CurrentAttacksKeepExistingProjectileDamageSpeedWithoutEscalation()
    {
        var c=Production.GetComponent<PirateCommanderBossController>();
        Assert.That(Get(c,"suppressiveDamage"),Is.EqualTo(2f));Assert.That(((ProjectileDefinition)Get(c,"projectileDefinition")).Speed,Is.EqualTo(8));
    }
    [TestCase(true)] [TestCase(false)]
    public void RealAbsorptionSuppressesOnlyTheDuplicateProjectileImpact(bool protectedNow)
    {
        pool.gameObject.SetActive(true); // Impact lifetime uses the pool's coroutine owner.
        var c=Fixture();var hp=c.GetComponent<EnemyHealth>();hp.SetMaxHp(125,true);
        Set(c,"combatActive",true);if(protectedNow)Call(c,"BeginShieldCombat");
        var sentinel=New("Raider impact sentinel");var projectile=New("Impact fixture").AddComponent<Bullet>();
        Set(projectile,"damage",5f);Set(projectile,"remainingPierceCount",1);Set(projectile,"runtimeImpactEffectPrefab",sentinel);
        Call(projectile,"TryApplyDamageToTarget",hp,(System.Action<float>)(value=>hp.TakeDamage(value)),1f,1f);
        int effects=0;
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(t.name=="Raider impact sentinel(Clone)"){effects++;objects.Add(t.gameObject);}
        Assert.That(effects,Is.EqualTo(protectedNow?0:1));
        Assert.That(c.AbsorbedHitSequence,Is.EqualTo(protectedNow?1:0));
        Assert.That(hp.CurrentHp,Is.EqualTo(protectedNow?125:120));
    }
    [Test] public void NoFanCoverEscortOrIndependentWeaponRoutinesRemainScheduled()
    {
        string source=File.ReadAllText("Assets/02_Scripts/Boss/PirateCommanderBossController.cs");
        Assert.That(source,Does.Not.Contain("SpawnSpread("));Assert.That(source,Does.Not.Contain("SpawnPhase2Escorts("));Assert.That(source,Does.Not.Contain("CoverBlastRoutine("));
        Assert.That(source.Split(new[]{"StartCoroutine("},System.StringSplitOptions.None).Length-1,Is.EqualTo(1));
        Assert.That(source,Does.Not.Match(@"(?m)^\s*transform\.position\s*="));Assert.That(source,Does.Contain("enemyHealth.IsKnockbackActive"));
        Assert.That(Production.GetComponent<Rigidbody2D>().constraints,Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
    }
    [TestCase("09_RaiderAssaultCommander/raider_assault_commander_weapons_hot")]
    [TestCase("09_RaiderAssaultCommander/raider_assault_commander_critical_damage")]
    [TestCase("18_RaiderBossVFX/VFX_Raider_HeavyMuzzle")]
    [TestCase("18_RaiderBossVFX/VFX_Raider_Assault_WeaponsHot")]
    [TestCase("18_RaiderBossVFX/VFX_Raider_CriticalDamage")]
    [TestCase("18_RaiderBossVFX/VFX_Raider_DamageSparks")]
    public void ProductionArtIsByteIdenticalToApprovedSource(string source)
    {Assert.That(File.ReadAllBytes(RaiderAssaultAuthoring.Art+Path.GetFileName(source)+".png"),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png")));}
}
