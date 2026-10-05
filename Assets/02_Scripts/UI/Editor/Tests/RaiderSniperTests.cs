using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RaiderSniperTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    readonly List<GameObject> objects=new List<GameObject>();
    PoolManager oldPool,pool;
    static GameObject Production=>AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSniperAuthoring.Boss);
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Property(object o,string p,object v)=>o.GetType().GetProperty(p,Flags).SetValue(o,v);
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    GameObject New(string n){var g=new GameObject(n);g.SetActive(false);objects.Add(g);return g;}
    RaiderSniperCommanderBossController Fixture()
    {
        var c=New("Carrier").AddComponent<RaiderSniperCommanderBossController>();
        Set(c,"health",c.GetComponent<EnemyHealth>());Set(c,"body",c.GetComponent<Rigidbody2D>());c.GetComponent<EnemyHealth>().SetMaxHp(125,true);
        var visual=New("Visual");visual.transform.SetParent(c.transform,false);
        Set(c,"visualRoot",visual.transform);Set(c,"railMuzzle",visual.transform);Set(c,"bodyRenderer",visual.AddComponent<SpriteRenderer>());
        var s=new SerializedObject(Production.GetComponent<RaiderSniperCommanderBossController>());
        foreach(string f in new[]{"idleSprite","lockSprite","criticalSprite","minePrefab","aimDefinition","railPrefab","sparksPrefab","criticalPrefab"})Set(c,f,s.FindProperty(f).objectReferenceValue);
        Call(c,"AcquireViews");Property(c,"IsCombatActive",true);return c;
    }
    [SetUp] public void Setup(){oldPool=PoolManager.Instance;pool=New("Pool").AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);}
    [TearDown] public void Cleanup()
    {
        foreach(var g in objects)if(g!=null&&g.TryGetComponent<RaiderSniperCommanderBossController>(out var c))c.StopCombat();
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,oldPool);
    }
    [Test] public void RegionCRoutingAndExistingRewardAuthority()
    {
        var core=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab").GetComponent<CoreObject>());
        Assert.That(core.FindProperty("region3RepeatBossPrefab").objectReferenceValue,Is.SameAs(Production));
        Assert.That(File.ReadAllText("Assets/02_Scripts/Core/CoreObject.cs"),Does.Contain("ExpeditionDepth.DeepZone2 => region3BossPrefab")); // Coreless first-clear generator owns Gatekeeper.
        Assert.That(core.FindProperty("region1RepeatBossPrefab").objectReferenceValue,Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss)));
        Assert.That(core.FindProperty("region2RepeatBossPrefab").objectReferenceValue,Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderSalvageAuthoring.Boss)));
        var def=AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(RaiderSniperAuthoring.Definition);
        Assert.That(def.GrantStoryPartOnFirstDefeat||def.GrantGuaranteedPassiveEachDefeat,Is.False);
        Assert.That(CampaignBossRewardService.ResolveBossId(def,ExpeditionDepth.DeepZone2),Is.EqualTo(CampaignBossId.PhaseGatekeeper));
        var original=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss).GetComponent<BossDummyController>());
        var next=new SerializedObject(Production.GetComponent<BossDummyController>());
        foreach(string f in new[]{"grantSelectableBossReward","bossRewardChoiceCount","bossRewardCapsulePrefab","changeStateToExpeditionAfterDeath"})Assert.That(next.FindProperty(f).boxedValue,Is.EqualTo(original.FindProperty(f).boxedValue));
    }
    [Test] public void Dedicated125HealthHalfEscalationNoSupportReuse()
    {
        Assert.That(Production.GetComponentsInChildren<EnemyHealth>(true).Length,Is.EqualTo(1));Assert.That(Production.GetComponent<EnemyHealth>().MaxHp,Is.EqualTo(125));
        Assert.That(Get(Production.GetComponent<RaiderSniperCommanderBossController>(),"escalationRatio"),Is.EqualTo(.5f));
        Assert.That(Production.GetComponent<PirateCommanderBossController>(),Is.Null);Assert.That(Production.GetComponent<RaiderSalvageCarrierBossController>(),Is.Null);Assert.That(Production.GetComponent<RaiderBarricadeCarrier>(),Is.Null);
    }
    [Test] public void WarningCommitShotRecoveryHaveSingleClock()
    {
        var c=Fixture();Call(c,"Enter",RaiderSniperCommanderBossController.SniperStage.Acquisition,.9f);Call(c,"AdvanceScheduler",.4f);
        var rail=(RaiderRailShot)Get(c,"rail");Assert.That(rail.IsVisible,Is.True);Assert.That(rail.IsDamaging,Is.False);
        Call(c,"AdvanceScheduler",.51f);Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.CommittedCharge));Assert.That(c.ShotsFired,Is.Zero);
        Call(c,"AdvanceScheduler",.64f);Assert.That(c.IsRailDamaging,Is.False);Call(c,"AdvanceScheduler",.02f);Assert.That(c.IsRailDamaging,Is.True);Assert.That(c.ShotsFired,Is.EqualTo(1));
        Call(c,"AdvanceScheduler",.14f);Assert.That(c.IsRailDamaging||rail.IsVisible,Is.False);Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.Recovery));
        Call(c,"AdvanceScheduler",2.19f);Assert.That(c.ShotsFired,Is.EqualTo(1));Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.Recovery));
        Call(c,"AdvanceScheduler",.02f);Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.MineDeployment));
    }
    [Test] public void CommitDoesNotFollowLaterPlayerMotion()
    {
        var c=Fixture();var p=DamageTarget(new Vector2(0,-5));var rb=p.GetComponent<Rigidbody2D>();rb.linearVelocity=Vector2.right*3;
        Set(c,"playerHealth",p);Set(c,"playerBody",rb);Call(c,"CommitAim");Vector2 target=c.CommittedTarget,direction=c.CommittedDirection;
        Assert.That(target.x,Is.GreaterThan(0));Assert.That(target.x,Is.LessThanOrEqualTo(.75f));Call(c,"Enter",RaiderSniperCommanderBossController.SniperStage.CommittedCharge,.65f);
        p.transform.position=new Vector3(5,0);Call(c,"AdvanceScheduler",.66f);
        Assert.That(c.CommittedTarget,Is.EqualTo(target));Assert.That(c.CommittedDirection,Is.EqualTo(direction));Assert.That(Vector2.Dot(((RaiderRailShot)Get(c,"rail")).Direction,direction),Is.GreaterThan(.999f));
    }
    [TestCase(0,8)] [TestCase(2,8)] [TestCase(20,8)] [TestCase(3,0)]
    public void PredictionUsesExistingBoundedUtility(float velocity,float speed)
    {Vector2 target=new Vector2(0,-5);var result=EnemyAttackController.PredictTargetPosition(Vector2.zero,target,Vector2.right*velocity,speed,.25f);Assert.That((result-target).magnitude,Is.LessThanOrEqualTo(velocity*.25f+.001f));if(velocity==0||speed==0)Assert.That(result,Is.EqualTo(target));else Assert.That(result.x,Is.GreaterThan(0));}
    [TestCase(false,2)] [TestCase(true,3)] public void MinesArePooledCappedAndLeaveOpenRoutes(bool critical,int expected)
    {
        var c=Fixture();Property(c,"IsPhase2",critical);Call(c,"DeployMines");Assert.That(c.LiveMineCount,Is.EqualTo(expected));
        var mines=(RaiderSniperMine[])Get(c,"mines");var first=mines[0];
        foreach(var mine in mines)if(mine!=null){Assert.That(mine.Owner,Is.SameAs(c));Assert.That(mine.GetComponent<RewardDropper>(),Is.Null);Assert.That(mine.GetComponent<RewardPickup>(),Is.Null);Assert.That(mine.GetComponent<Collider2D>().isTrigger,Is.True);Assert.That(mine.IsArmed,Is.False);Assert.That(((Vector2)mine.transform.position-new Vector2(0,-4.8f)).magnitude,Is.GreaterThan(1.6f));}
        for(int i=0;i<8;i++)Call(c,"DeployMines");Assert.That(c.LiveMineCount,Is.LessThanOrEqualTo(3));
        Call(c,"ReleaseMines");Assert.That(first.Owner,Is.Null);Assert.That(first.gameObject.activeSelf,Is.False);Call(c,"DeployMines");Assert.That(first.Owner,Is.SameAs(c));
    }
    [Test] public void MineWarningArmingExpiryMatchVisibility()
    {
        var c=Fixture();Call(c,"DeployMines");var mine=((RaiderSniperMine[])Get(c,"mines"))[0];
        Assert.That(mine.IsVisible,Is.True);mine.Advance(1.09f);Assert.That(mine.IsArmed,Is.False);mine.Advance(.02f);Assert.That(mine.IsArmed,Is.True);
        Assert.That(mine.Advance(8),Is.False);Assert.That(mine.IsArmed||mine.IsVisible,Is.False);
    }
    [TestCase("StopCombat")] [TestCase("PlayerDied")] [TestCase("OnDisable")] public void AllLifecycleRoutesReleaseMinesRailAndViews(string callback)
    {
        var c=Fixture();Call(c,"DeployMines");Call(c,"CommitAim");var rail=(RaiderRailShot)Get(c,"rail");rail.Fire(0);Call(c,callback);
        Assert.That(c.LiveMineCount,Is.Zero);Assert.That(rail.IsVisible||rail.IsDamaging||rail.gameObject.activeSelf,Is.False);Assert.That(c.IsCombatActive,Is.False);
    }
    [TestCase("Died")] [TestCase("RunEnded")] public void DeathCallbacksReleaseOwnership(string callback)
    {var c=Fixture();Call(c,"DeployMines");Call(c,callback,new object[]{null});Assert.That(c.LiveMineCount,Is.Zero);Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.Stopped));}
    [Test] public void EscalationOnceCancelsAttackPreservesDamageAndResumes()
    {
        var c=Fixture();Call(c,"DeployMines");Call(c,"CommitAim");var rail=(RaiderRailShot)Get(c,"rail");rail.Fire(0);var def=(ProjectileDefinition)Get(c,"aimDefinition");float damage=def.Damage,speed=def.Speed;
        Call(c,"HealthChanged",null,62.5f,125f);Assert.That(rail.IsDamaging,Is.False);Assert.That(c.LiveMineCount,Is.Zero);Call(c,"AdvanceScheduler",.01f);
        Assert.That(c.EscalationCount,Is.EqualTo(1));Assert.That(c.Stage,Is.EqualTo(RaiderSniperCommanderBossController.SniperStage.CriticalTransition));Assert.That(((SpriteRenderer)Get(c,"bodyRenderer")).sprite,Is.SameAs(Get(c,"criticalSprite")));
        Call(c,"HealthChanged",null,20f,125f);Call(c,"AdvanceScheduler",.66f);Assert.That(c.EscalationCount,Is.EqualTo(1));Assert.That(c.LiveMineCount,Is.EqualTo(3));Assert.That(def.Damage,Is.EqualTo(damage));Assert.That(def.Speed,Is.EqualTo(speed));
    }
    [TestCase(0,-2)] [TestCase(5,-2)] [TestCase(-7,-7)] public void RangeControlStaysContainedAndRetreatsFromClosePlayer(float x,float y)
    {
        var c=Fixture();var p=New("Player").AddComponent<PlayerHealth>();p.transform.position=new Vector3(x,y);Set(c,"playerHealth",p);Call(c,"BeginReposition");
        var target=(Vector2)Get(c,"moveTarget");Assert.That(c.ArenaBounds.Contains(target),Is.True);if(p.transform.position.magnitude<3.5f)Assert.That(Vector2.Distance(target,p.transform.position),Is.GreaterThan(p.transform.position.magnitude));
        Assert.That(c.GetComponent<Rigidbody2D>().position,Is.EqualTo(Vector2.zero));
    }
    [TestCase(RaiderSniperAuthoring.Boss)] [TestCase(RaiderSniperAuthoring.Mine)] [TestCase(RaiderSniperAuthoring.Rail)] public void NoMissingReferences(string path)
    {var g=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(g,Is.Not.Null);foreach(var t in g.GetComponentsInChildren<Transform>(true))Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),Is.Zero);foreach(var r in g.GetComponentsInChildren<SpriteRenderer>(true))Assert.That(r.sprite,Is.Not.Null);}
    [TestCase("idle")] [TestCase("target_lock")] [TestCase("critical_damage")] public void ApprovedBodyBytesUnchanged(string state)
    {string n="raider_sniper_commander_"+state+".png";Assert.That(File.ReadAllBytes(RaiderSniperAuthoring.Art+n),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/11_RaiderSniperCommander/"+n)));}
    [Test] public void NoCompetingMovementTimersEconomyOrWeaponSystems()
    {string c=File.ReadAllText("Assets/02_Scripts/Boss/RaiderSniperCommanderBossController.cs");foreach(string forbidden in new[]{"RunWallet","TrySpend","AddCurrency","StartCoroutine(","System.Linq","transform.position ="})Assert.That(c,Does.Not.Contain(forbidden));Assert.That(c,Does.Contain("body.MovePosition"));foreach(string name in new[]{"RaiderSniperMine","RaiderRailShot"})Assert.That(File.ReadAllText("Assets/02_Scripts/Boss/"+name+".cs"),Does.Not.Contain("void Update("));}

    PlayerHealth DamageTarget(Vector2 position)
    {
        var p=New("DamageTarget").AddComponent<PlayerHealth>();
        Set(p,"autoCreateSpriteHitFlash",false);Set(p,"useProceduralHitFeedback",false);Set(p,"hitShakeAmplitude",0f);
        var rb=p.GetComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;
        p.gameObject.AddComponent<CircleCollider2D>().radius=.15f;p.transform.position=position;p.gameObject.SetActive(true);p.SetMaxHp(20,true);return p;
    }
    [Test] public void RailDamagesOnlyVisibleCommittedPathOnce()
    {
        var c=Fixture();var p=DamageTarget(new Vector2(0,-3));var rail=(RaiderRailShot)Get(c,"rail");
        rail.Configure(Vector2.zero,new Vector2(0,-8),2,p);Physics2D.SyncTransforms();rail.Warn(1);Assert.That(p.CurrentHp,Is.EqualTo(20));
        rail.Fire(0);Assert.That(p.CurrentHp,Is.EqualTo(18));Set(p,"invincibleTimer",0f);rail.Fire(.04f);Assert.That(p.CurrentHp,Is.EqualTo(18));
        rail.Fire(.14f);Assert.That(rail.IsDamaging||rail.IsVisible,Is.False);
    }
    [Test] public void LateralDodgeAfterCommitAvoidsRailDamage()
    {
        var c=Fixture();var p=DamageTarget(new Vector2(0,-3));var rail=(RaiderRailShot)Get(c,"rail");rail.Configure(Vector2.zero,new Vector2(0,-8),2,p);
        p.transform.position=new Vector2(1,-3);Physics2D.SyncTransforms();rail.Fire(0);Assert.That(p.CurrentHp,Is.EqualTo(20));
    }
    [Test] public void MineDamageRequiresWarningAndVisibleDetonation()
    {
        var c=Fixture();Call(c,"DeployMines");var mine=((RaiderSniperMine[])Get(c,"mines"))[0];var p=DamageTarget(mine.transform.position);
        mine.Initialize(c,p,1.1f,8,2);Physics2D.SyncTransforms();mine.Advance(1);Assert.That(p.CurrentHp,Is.EqualTo(20));
        mine.Advance(.11f);Assert.That(mine.IsDetonating,Is.True);mine.Advance(.02f);Assert.That(p.CurrentHp,Is.EqualTo(18));
        Set(p,"invincibleTimer",0f);mine.Advance(.02f);Assert.That(p.CurrentHp,Is.EqualTo(18));mine.Advance(.25f);Assert.That(mine.IsVisible||mine.IsArmed,Is.False);
    }
    [Test] public void DashInvulnerabilityRemainsAuthoritativeForMine()
    {
        var c=Fixture();Call(c,"DeployMines");var mine=((RaiderSniperMine[])Get(c,"mines"))[0];var p=DamageTarget(mine.transform.position);p.SetDashInvincible(true);
        mine.Initialize(c,p,.1f,8,2);Physics2D.SyncTransforms();mine.Advance(.11f);mine.Advance(.02f);Assert.That(p.CurrentHp,Is.EqualTo(20));
    }
    [TestCase(0,-1)] [TestCase(1,0)] [TestCase(1,1)] [TestCase(-1,-1)]
    public void RailEndpointAndColliderMatchContainment(float x,float y)
    {
        var c=Fixture();Vector2 d=new Vector2(x,y).normalized;var end=(Vector2)Call(c,"RayEnd",Vector2.zero,d);var bounds=c.ArenaBounds;
        Assert.That(Mathf.Max(Mathf.Abs(end.x)/bounds.extents.x,Mathf.Abs(end.y)/bounds.extents.y),Is.EqualTo(1).Within(.001));
        var rail=(RaiderRailShot)Get(c,"rail");rail.Configure(Vector2.zero,end,2,null);Assert.That(rail.Length,Is.EqualTo(end.magnitude).Within(.001));
        Assert.That(((SpriteRenderer)Get(rail,"visual")).size.x,Is.EqualTo(rail.Length));
    }
    [Test] public void OneCompleteCycleHasOneRailAndMandatoryRecovery()
    {
        var c=Fixture();Call(c,"Enter",RaiderSniperCommanderBossController.SniperStage.Idle,.1f);
        bool recoverySeen=false;for(int i=0;i<700;i++){Call(c,"AdvanceScheduler",.01f);if(c.Stage==RaiderSniperCommanderBossController.SniperStage.Recovery)recoverySeen=true;}
        Assert.That(recoverySeen,Is.True);Assert.That(c.ShotsFired,Is.EqualTo(1));Assert.That(c.Cycle,Is.EqualTo(2));
    }
    [TestCase(false,.9f)] [TestCase(true,.7f)] public void CadenceEscalatesOnlyPreparationAndRecovery(bool critical,float duration)
    {var c=Fixture();Property(c,"IsPhase2",critical);Call(c,"Enter",RaiderSniperCommanderBossController.SniperStage.Reposition,0f);Call(c,"AdvanceScheduler",.01f);Assert.That(Get(c,"remaining"),Is.EqualTo(duration));Assert.That(Get(c,"commitDuration"),Is.EqualTo(.65f));}
    [TestCase("VFX_Raider_Sniper_Rail")] [TestCase("VFX_Raider_BarrageTelegraph")] public void ApprovedVfxBytesUnchanged(string n)
    {Assert.That(File.ReadAllBytes(RaiderSniperAuthoring.Art+n+".png"),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/18_RaiderBossVFX/"+n+".png")));}

    [TestCase(.0f,2f)] [TestCase(.04f,10f)] public void RailColliderExcludesApprovedTransparentPadding(float elapsed,float firstPixel)
    {
        var c=Fixture();var rail=(RaiderRailShot)Get(c,"rail");rail.Configure(Vector2.zero,new Vector2(9.6f,0),2,null);rail.Fire(elapsed);
        var box=(BoxCollider2D)Get(rail,"damageCollider");Assert.That(box.offset.x-box.size.x*.5f,Is.EqualTo(firstPixel*.1f).Within(.001));Assert.That(box.offset.x+box.size.x*.5f,Is.EqualTo(9.4f).Within(.001));
    }
    [Test] public void TransparentRailTailIsNotVisibleOrDamaging()
    {
        var c=Fixture();var rail=(RaiderRailShot)Get(c,"rail");rail.Configure(Vector2.zero,Vector2.right*8,2,null);rail.Fire(.091f);Assert.That(rail.IsVisible||rail.IsDamaging,Is.False);
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(RaiderSniperAuthoring.Art+"VFX_Raider_Sniper_Rail.png"));
        // PNG LoadImage uses bottom-left pixel coordinates: final Shot frame is lower-right.
        for(int y=0;y<32;y++)for(int x=192;x<288;x++)Assert.That(texture.GetPixel(x,y).a,Is.Zero);
        Object.DestroyImmediate(texture);
    }
}
