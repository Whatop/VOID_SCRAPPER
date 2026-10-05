using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RaiderSalvageTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    readonly List<GameObject> objects=new List<GameObject>();
    PoolManager oldPool,pool;
    static GameObject Production=>AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSalvageAuthoring.Boss);
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Property(object o,string p,object v)=>o.GetType().GetProperty(p,Flags).SetValue(o,v);
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    GameObject New(string n){var g=new GameObject(n);g.SetActive(false);objects.Add(g);return g;}
    RaiderSalvageCarrierBossController Fixture()
    {
        var c=New("Carrier").AddComponent<RaiderSalvageCarrierBossController>();
        Set(c,"health",c.GetComponent<EnemyHealth>());Set(c,"body",c.GetComponent<Rigidbody2D>());c.GetComponent<EnemyHealth>().SetMaxHp(125,true);
        var visual=New("Visual");visual.transform.SetParent(c.transform,false);
        Set(c,"visualRoot",visual.transform);Set(c,"intake",visual.transform);Set(c,"dischargePort",visual.transform);Set(c,"bodyRenderer",visual.AddComponent<SpriteRenderer>());
        var s=new SerializedObject(Production.GetComponent<RaiderSalvageCarrierBossController>());
        foreach(string f in new[]{"idleSprite","salvageSprite","overloadSprite","salvagePrefab","projectileDefinition","dischargeDefinition","pullPrefab","muzzlePrefab","sparksPrefab","criticalPrefab"})Set(c,f,s.FindProperty(f).objectReferenceValue);
        Call(c,"AcquireViews");Property(c,"IsCombatActive",true);return c;
    }
    [SetUp] public void Setup(){oldPool=PoolManager.Instance;pool=New("Pool").AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);}
    [TearDown] public void Cleanup()
    {
        foreach(var g in objects)if(g!=null&&g.TryGetComponent<RaiderSalvageCarrierBossController>(out var c))c.StopCombat();
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,oldPool);
    }
    [Test] public void SeparateCarrierRetains125HpAndExistingDeathAuthority()
    {
        Assert.That(Production.GetComponent<PirateCommanderBossController>(),Is.Null);
        Assert.That(Production.GetComponent<RaiderBarricadeCarrier>(),Is.Null);
        Assert.That(Production.GetComponentsInChildren<EnemyHealth>(true).Length,Is.EqualTo(1));
        Assert.That(Production.GetComponent<EnemyHealth>().MaxHp,Is.EqualTo(125));
        var c=Production.GetComponent<RaiderSalvageCarrierBossController>();Assert.That(Get(c,"escalationRatio"),Is.EqualTo(.5f));
        var source=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss).GetComponent<BossDummyController>());
        var target=new SerializedObject(Production.GetComponent<BossDummyController>());
        foreach(string f in new[]{"grantSelectableBossReward","bossRewardChoiceCount","bossRewardCapsulePrefab","changeStateToExpeditionAfterDeath"})
            Assert.That(target.FindProperty(f).boxedValue,Is.EqualTo(source.FindProperty(f).boxedValue),f);
        var def=AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(RaiderSalvageAuthoring.Definition);
        Assert.That(def.GrantStoryPartOnFirstDefeat||def.GrantGuaranteedPassiveEachDefeat,Is.False);
        Assert.That(def.StoryPart,Is.EqualTo(BossStoryPart.None));
        Assert.That(CampaignBossRewardService.ResolveBossId(def,ExpeditionDepth.DeepZone1),Is.EqualTo(CampaignBossId.SalvageDevourer));
    }
    [Test] public void OnlyRegionBRepeatGetsNewBinding()
    {
        var c=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab").GetComponent<CoreObject>());
        Assert.That(c.FindProperty("region2RepeatBossPrefab").objectReferenceValue,Is.SameAs(Production));
        Assert.That(c.FindProperty("region1RepeatBossPrefab").objectReferenceValue,Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss)));
        Assert.That(((GameObject)c.FindProperty("region2BossPrefab").objectReferenceValue).GetComponent<FrigateTriadBossController>(),Is.Not.Null);
        string source=File.ReadAllText("Assets/02_Scripts/Core/CoreObject.cs");Assert.That(source,Does.Contain("if (depth == ExpeditionDepth.DeepZone1)"));
    }
    [TestCase(2,0,1.3f)] [TestCase(5,.8f,1.6f)] [TestCase(7,0,0)] [TestCase(2,2,0)] [TestCase(-1,0,0)] [TestCase(.9f,0,0)]
    public void DirectionalPullIsBoundedAndOutsideLaneUnaffected(float x,float y,float expected)
    {
        Vector2 force=RaiderSalvageCarrierBossController.CalculatePull(Vector2.zero,Vector2.right,new Vector2(x,y),6,1.1f,expected>0?expected:1.3f);
        Assert.That(force.magnitude,Is.EqualTo(expected).Within(.001));if(expected>0)Assert.That(Vector2.Dot(force,new Vector2(x,y)),Is.LessThan(0));
    }
    [Test] public void WarningVisibleBeforePullAndRecoveryClearsImmediately()
    {
        var c=Fixture();Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.PullWarning,1f);
        Assert.That(((PhaseCombatVfx[])Get(c,"views"))[0].IsVisible,Is.True);Assert.That(c.IsPullActive,Is.False);
        Call(c,"AdvanceScheduler",.99f);Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.PullWarning));
        Call(c,"AdvanceScheduler",.02f);Assert.That(c.IsPullActive,Is.True);
        Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Recovery,2.4f);Assert.That(c.IsPullActive,Is.False);Assert.That(((PhaseCombatVfx[])Get(c,"views"))[0].IsVisible,Is.False);
    }
    [Test] public void CombatSalvageIsNotARewardAndReusesPool()
    {
        var c=Fixture();Call(c,"SpawnSalvage");Assert.That(c.LiveSalvageCount,Is.EqualTo(5));
        var items=(CarrierCombatSalvage[])Get(c,"salvage");var first=items[0];
        Assert.That(first.Owner,Is.SameAs(c));Assert.That(first.GetComponent<RewardPickup>(),Is.Null);Assert.That(first.GetComponent<RewardDropper>(),Is.Null);Assert.That(first.GetComponent<EnemyHealth>(),Is.Null);
        Assert.That(first.GetComponent<Collider2D>().isTrigger,Is.True);
        var ctx=new ProjectileDamageContext(ProjectileOwner.Enemy,c.transform,20,Vector2.zero,1);Assert.That(first.TryReceiveProjectileDamage(in ctx),Is.False);
        first.TakeDamage(2);Assert.That(c.DestroyedSalvage,Is.EqualTo(1));Assert.That(c.Cargo,Is.Zero);Assert.That(first.gameObject.activeSelf,Is.False);
        Call(c,"ReleaseSalvage");Call(c,"SpawnSalvage");Assert.That(c.LiveSalvageCount,Is.EqualTo(5));Assert.That(first.IsDead,Is.False);Assert.That(first.Owner,Is.SameAs(c));
    }
    [Test] public void CargoCountsOnlyOwnedLiveArrivalsAndDischargesOnce()
    {
        var c=Fixture();Call(c,"SpawnSalvage");Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Collection,3.8f);
        var items=(CarrierCombatSalvage[])Get(c,"salvage");var first=items[0];
        c.RetireSalvage(first,true);c.RetireSalvage(first,true);Assert.That(c.Cargo,Is.EqualTo(1));
        c.RetireSalvage(items[1],true);c.RetireSalvage(items[2],true);Call(c,"AdvanceScheduler",4f);
        Assert.That(c.Discharges,Is.EqualTo(1));Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.CargoReady));Assert.That(c.LiveSalvageCount,Is.Zero);
        Call(c,"AdvanceScheduler",.4f);Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.DischargeWarning));Assert.That(c.ShotsFired,Is.Zero);
        Call(c,"AdvanceScheduler",.79f);Assert.That(c.ShotsFired,Is.Zero);Call(c,"AdvanceScheduler",.02f);
        for(int i=0;i<4;i++)Call(c,"AdvanceScheduler",.25f);
        Assert.That(c.ShotsFired,Is.EqualTo(3));Assert.That(c.Cargo,Is.Zero);Assert.That(c.Discharges,Is.EqualTo(1));Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.Recovery));
    }
    [TestCase(true)] [TestCase(false)] public void FailedCollectionHasFiniteExit(bool destroyAll)
    {
        var c=Fixture();Call(c,"SpawnSalvage");Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Collection,3.8f);
        if(destroyAll){var copy=(CarrierCombatSalvage[])((CarrierCombatSalvage[])Get(c,"salvage")).Clone();foreach(var v in copy)if(v!=null)v.TakeDamage(99);}
        Call(c,"AdvanceScheduler",4f);Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.Recovery));Assert.That(c.Cargo,Is.Zero);Assert.That(c.Discharges,Is.Zero);
        Call(c,"AdvanceScheduler",1.7f);Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.Reposition));
    }
    [Test] public void FiftyPercentEscalatesOnceAndClearsCollectionBeforeOverload()
    {
        var c=Fixture();Call(c,"SpawnSalvage");Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Collection,3.8f);
        Call(c,"HealthChanged",null,62.5f,125f);Assert.That(c.IsPullActive,Is.False);Call(c,"AdvanceScheduler",.01f);
        Assert.That(c.EscalationCount,Is.EqualTo(1));Assert.That(c.LiveSalvageCount,Is.Zero);Assert.That(c.IsPhase2,Is.True);
        Assert.That(((SpriteRenderer)Get(c,"bodyRenderer")).sprite,Is.SameAs(Get(c,"overloadSprite")));
        Call(c,"HealthChanged",null,20f,125f);Call(c,"AdvanceScheduler",.7f);Assert.That(c.EscalationCount,Is.EqualTo(1));Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.SecondaryWarning));
    }
    [TestCase("StopCombat")] [TestCase("PlayerDied")] [TestCase("OnDisable")]
    public void LifecycleReleasesSalvageForceProjectilesViews(string entry)
    {
        var c=Fixture();Call(c,"SpawnSalvage");Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Collection,3.8f);Call(c,"Fire",true,0);
        var views=(PhaseCombatVfx[])((PhaseCombatVfx[])Get(c,"views")).Clone();Call(c,entry);
        Assert.That(c.LiveSalvageCount,Is.Zero);Assert.That(c.IsCombatActive||c.IsPullActive,Is.False);Assert.That(c.Cargo,Is.Zero);
        foreach(var v in views){Assert.That(v.IsVisible,Is.False);Assert.That(v.gameObject.activeSelf,Is.False);}
        foreach(var b in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))Assert.That(b.SourceRoot,Is.Not.SameAs(c.transform));
    }
    [TestCase(false)] [TestCase(true)] public void DischargeUsesExistingDamageSpeedLifetimeAndBossSource(bool phase2)
    {
        var c=Fixture();Property(c,"IsPhase2",phase2);Call(c,"Fire",true,0);
        Bullet own=null;foreach(var b in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))if(b.SourceRoot==c.transform)own=b;
        Assert.That(own,Is.Not.Null);Assert.That(own.Owner,Is.EqualTo(ProjectileOwner.Enemy));Assert.That(own.Speed,Is.EqualTo(8));Assert.That(Get(own,"damage"),Is.EqualTo(2f));Assert.That(Get(own,"lifeTime"),Is.EqualTo(2f));
    }
    [Test] public void RecoveryCannotPullOrFireAndHasFiniteEnd()
    {
        var c=Fixture();Property(c,"IsPhase2",true);Call(c,"Enter",RaiderSalvageCarrierBossController.CarrierStage.Recovery,3.2f);
        for(int i=0;i<30;i++)Call(c,"AdvanceScheduler",.1f);Assert.That(c.ShotsFired,Is.Zero);Assert.That(c.IsPullActive,Is.False);
        Call(c,"AdvanceScheduler",.3f);Assert.That(c.Stage,Is.EqualTo(RaiderSalvageCarrierBossController.CarrierStage.Reposition));
    }
    [Test] public void NoEconomyCallsOrScansOrPerSalvageUpdate()
    {
        string c=File.ReadAllText("Assets/02_Scripts/Boss/RaiderSalvageCarrierBossController.cs"),item=File.ReadAllText("Assets/02_Scripts/Boss/CarrierCombatSalvage.cs");
        foreach(string forbidden in new[]{"RunWallet","AddCurrency","TrySpend","RewardPickup>","RewardDropper>","System.Linq","StartCoroutine("})Assert.That(c,Does.Not.Contain(forbidden));
        Assert.That(c,Does.Contain("SetExternalPushVelocity(this"));Assert.That(c,Does.Not.Match(@"(?m)^\s*transform\.position\s*="));Assert.That(item,Does.Not.Contain("void Update("));
    }
    [TestCase("idle")] [TestCase("salvage_active")] [TestCase("cargo_overload")]
    public void BodyCopiesMatchApprovedSource(string state)
    {string n="raider_salvage_carrier_"+state+".png";Assert.That(File.ReadAllBytes(RaiderSalvageAuthoring.Art+n),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/10_RaiderSalvageCarrier/"+n)));}

    [TestCase("Died")] [TestCase("RunEnded")]
    public void DeathAndRunEndAlsoReleaseAnAppliedForce(string callback)
    {
        var c=Fixture();var player=New("Player").AddComponent<PlayerController2D>();Set(c,"player",player);
        player.SetExternalPushVelocity(c,Vector2.up*1.3f);Call(c,"SpawnSalvage");
        Call(c,callback,new object[]{null});
        Assert.That(((System.Collections.IDictionary)Get(player,"ownedPushVelocities")).Count,Is.Zero);
        Assert.That(c.LiveSalvageCount,Is.Zero);Assert.That(c.IsCombatActive,Is.False);
    }
    [Test] public void PlayerBulletUsesActualCollisionReceiverToDenyCargo()
    {
        var c=Fixture();Call(c,"SpawnSalvage");var item=((CarrierCombatSalvage[])Get(c,"salvage"))[0];
        var definition=(ProjectileDefinition)Get(c,"projectileDefinition");
        var g=pool.Get(definition.ProjectilePrefab,item.transform.position,Quaternion.identity);objects.Add(g);
        var bullet=g.GetComponent<Bullet>();bullet.Initialize(Vector2.right,ProjectileOwner.Player,definition,damageOverride:3);
        // EditMode's inactive pool cannot run delayed cosmetic releases. The real
        // impact/pool lifetime is exercised by the generated Play Mode matrix.
        Set(bullet,"runtimeImpactEffectPrefab",null);
        Call(bullet,"OnTriggerEnter2D",item.GetComponent<Collider2D>());
        Assert.That(c.DestroyedSalvage,Is.EqualTo(1));Assert.That(c.Cargo,Is.Zero);Assert.That(item.gameObject.activeSelf,Is.False);
    }
    [TestCase(false,5)] [TestCase(true,6)]
    public void EscalationChangesCollectionAttemptNotProjectileValues(bool escalated,int count)
    {
        var c=Fixture();Property(c,"IsPhase2",escalated);Call(c,"SpawnSalvage");Assert.That(c.LiveSalvageCount,Is.EqualTo(count));
        var normal=(ProjectileDefinition)Get(c,"projectileDefinition");var heavy=(ProjectileDefinition)Get(c,"dischargeDefinition");
        Assert.That(heavy.Speed,Is.EqualTo(normal.Speed));Assert.That(heavy.Damage,Is.EqualTo(normal.Damage));Assert.That(heavy.LifeTime,Is.EqualTo(normal.LifeTime));Assert.That(heavy.Range,Is.EqualTo(normal.Range));
    }
    [Test] public void PullFramesAreApprovedAndHaveNoCollisionOrIndependentAnimationOwner()
    {
        Assert.That(File.ReadAllBytes(RaiderSalvageAuthoring.Art+"VFX_Raider_Salvage_Beam.png"),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/18_RaiderBossVFX/VFX_Raider_Salvage_Beam.png")));
        var v=AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSalvageAuthoring.Vfx+"Pull.prefab");Assert.That(v.GetComponentsInChildren<Collider2D>(true),Is.Empty);Assert.That(v.GetComponent<Animator>(),Is.Null);
        var frames=(Sprite[])Get(v.GetComponent<PhaseCombatVfx>(),"frames");Assert.That(frames.Length,Is.EqualTo(6));foreach(var frame in frames)Assert.That(frame,Is.Not.Null);
    }
    [TestCase(RaiderSalvageAuthoring.Boss)] [TestCase(RaiderSalvageAuthoring.Salvage)] [TestCase("Assets/03_Prefabs/Projectile/CarrierCargoChunk.prefab")]
    public void NewPrefabsHaveNoMissingScriptsOrSprites(string path)
    {
        var g=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(g,Is.Not.Null);
        foreach(var t in g.GetComponentsInChildren<Transform>(true))Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),Is.Zero);
        foreach(var r in g.GetComponentsInChildren<SpriteRenderer>(true))Assert.That(r.sprite,Is.Not.Null,r.name);
    }
}
