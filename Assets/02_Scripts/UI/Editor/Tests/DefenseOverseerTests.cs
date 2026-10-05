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

public sealed class DefenseOverseerTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    readonly List<GameObject> roots=new List<GameObject>();
    PoolManager previousPool,pool;
    AudioManager previousAudio;
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static object Call(object o,string m,params object[] a)=>o.GetType().GetMethod(m,Flags).Invoke(o,a);
    GameObject New(string name) {var g=new GameObject(name);g.SetActive(false);roots.Add(g);return g;}
    [SetUp] public void Setup()
    {
        previousAudio=AudioManager.Instance;previousPool=PoolManager.Instance;
        pool=New("Defense test pool").AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);
    }
    [TearDown] public void Cleanup()
    {
        foreach(var g in roots) if(g!=null && g.TryGetComponent<FrigateTriadBossController>(out var c)) Call(c,"CancelPatternWork",true);
        for(int i=roots.Count-1;i>=0;i--)if(roots[i]!=null)Object.DestroyImmediate(roots[i]);roots.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,previousPool);
        typeof(FrigateTriadBossController).GetProperty("ActiveDefenseEncounter").SetValue(null,null);
        if(previousAudio==null && AudioManager.Instance!=null)Object.DestroyImmediate(AudioManager.Instance.gameObject);
    }
    FrigateTriadBossController Prefab()=>AssetDatabase.LoadAssetAtPath<GameObject>(DefenseOverseerAuthoring.Boss).GetComponent<FrigateTriadBossController>();
    FrigateTriadBossController Boss()
    {
        var g=Object.Instantiate(Prefab().gameObject);g.SetActive(false);roots.Add(g);
        var c=g.GetComponent<FrigateTriadBossController>();c.InitializeBossState();
        foreach(var part in g.GetComponentsInChildren<FrigateBossPart>(true))Call(part,"SetRuntimeDamageEnabled",true);
        return c;
    }
    DefenseBarrageZone Zone()
    {
        var g=pool.Get(AssetDatabase.LoadAssetAtPath<GameObject>(DefenseOverseerAuthoring.Zone),Vector3.zero,Quaternion.identity);roots.Add(g);
        var z=g.GetComponent<DefenseBarrageZone>();z.BeginWarning(new Vector2(2,3),1.05f,3,null);return z;
    }
    [Test] public void ProductionRetainsThreeIndependent55HpPartsAndCampaignAuthority()
    {
        var c=Prefab();Assert.That(c.UsesDefenseOverseerSequence,Is.True);
        var parts=c.GetComponentsInChildren<FrigateBossPart>(true);Assert.That(parts.Length,Is.EqualTo(3));
        foreach(var p in parts) {Assert.That(p.MaxHealth,Is.EqualTo(55));Assert.That(p.GetComponentsInChildren<Collider2D>(true).Length,Is.GreaterThan(0));}
        Assert.That(c.TotalMaxHealth,Is.EqualTo(165));
        Assert.That(new SerializedObject(c.AggregateHealth).FindProperty("maxHp").floatValue,Is.EqualTo(165));
        var death=c.GetComponent<BossDummyController>();var s=new SerializedObject(death);
        Assert.That(s.FindProperty("enemyHealth").objectReferenceValue,Is.SameAs(c.AggregateHealth));
        var definition=(BossCampaignDefinition)s.FindProperty("campaignDefinition").objectReferenceValue;
        Assert.That(definition.BossId,Is.EqualTo(CampaignBossId.SalvageDevourer));
        Assert.That(s.FindProperty("deathPresentation").objectReferenceValue,Is.Not.Null);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void LeftAndRightMuzzlesBelongToTheirOwnPartAndLeavePhysicsRootUnrotated(int index)
    {
        var c=Prefab();var parts=(FrigateBossPart[])Get(c,"parts");
        Assert.That(c.DefenseMuzzle(index,false).IsChildOf(parts[index].FirePointRoot.transform),Is.True);
        Assert.That(c.DefenseMuzzle(index,true).IsChildOf(parts[index].FirePointRoot.transform),Is.True);
        Assert.That(c.DefenseMuzzle(index,false).localPosition.x,Is.LessThan(0));
        Assert.That(c.DefenseMuzzle(index,true).localPosition.x,Is.GreaterThan(0));
        Assert.That(Quaternion.Angle(parts[index].transform.localRotation,Quaternion.identity),Is.LessThan(.01f));
        Assert.That(Quaternion.Angle(parts[index].VisualRoot.localRotation,Quaternion.Euler(0,0,180)),Is.LessThan(.01f));
    }
    [TestCase("07_DefenseOverseer/defense_overseer_barrage_charged")]
    [TestCase("07_DefenseOverseer/defense_overseer_armor_broken")]
    [TestCase("16_SystemBossVFX/VFX_Barrage_TargetTelegraph")]
    [TestCase("17_SystemBossVFX_Revisions/VFX_Barrage_HeavyImpact")]
    [TestCase("16_SystemBossVFX/VFX_Barrage_BatteryFlash")]
    public void ProductionCopiesMatchApprovedBytes(string source)
    {
        string path=DefenseOverseerAuthoring.Art+Path.GetFileName(source)+".png";
        Assert.That(File.ReadAllBytes(path),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png")));
        var t=(TextureImporter)AssetImporter.GetAtPath(path);Assert.That(t.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(t.mipmapEnabled,Is.False);
    }
    [TestCase(0f,false,true)] [TestCase(.064f,false,true)] [TestCase(.065f,true,true)]
    [TestCase(.149f,true,true)] [TestCase(.15f,false,true)] [TestCase(.479f,false,true)] [TestCase(.48f,false,false)]
    public void DamageGateMatchesOnlyBrightExpandedImpactFrame(float time,bool damage,bool visible)
    {
        var z=Zone();Assert.That(z.IsDamaging,Is.False);z.SetImpactElapsed(time);
        Assert.That(z.IsDamaging,Is.EqualTo(damage));Assert.That(z.IsVisible,Is.EqualTo(visible));
        if(damage)Assert.That(((SpriteRenderer)Get(z,"visual")).sprite.name,Does.EndWith("_01"));
    }
    [Test] public void CommittedWarningNeverFollowsTargetAndCannotDamage()
    {
        var player=New("Moving target").AddComponent<PlayerHealth>();var z=Zone();
        z.BeginWarning(new Vector2(2,3),1.05f,3,player);player.transform.position=new Vector2(-8,-4);
        for(int i=0;i<=10;i++) {z.SetWarningProgress(i*.1f);Assert.That(z.IsDamaging,Is.False);Assert.That((Vector2)z.transform.position,Is.EqualTo(new Vector2(2,3)));}
        Assert.That(z.CommittedPosition,Is.EqualTo(new Vector2(2,3)));
    }
    [Test] public void ImpactRadiusAndApprovedPeakVisualAgree()
    {
        var z=Zone();z.SetImpactElapsed(.07f);var r=(SpriteRenderer)Get(z,"visual");
        Assert.That(60f/32*r.transform.lossyScale.x,Is.EqualTo(z.Radius*2).Within(.001f));
        Assert.That(r.sortingOrder,Is.LessThan(1),"player body remains in front of impact");
    }
    [TestCase(false)] [TestCase(true)]
    public void RealHealthOnlyReceivesOnePeakHitAndMovingOutsideEscapes(bool escape)
    {
        var player=New("Damage target").AddComponent<PlayerHealth>();player.SetMaxHp(20,true);
        var z=Zone();z.BeginWarning(Vector2.zero,1.05f,3,player);z.SetWarningProgress(1);
        Assert.That(player.CurrentHp,Is.EqualTo(20));
        if(escape)player.transform.position=Vector2.right*2;
        z.SetImpactElapsed(.04f);Assert.That(player.CurrentHp,Is.EqualTo(20));
        z.SetImpactElapsed(.07f);Assert.That(player.CurrentHp,Is.EqualTo(escape?20:17));
        Set(player,"invincibleTimer",0f);z.SetImpactElapsed(.10f);z.SetImpactElapsed(.3f);
        Assert.That(player.CurrentHp,Is.EqualTo(escape?20:17),"one hit, independent of the player's invulnerability timer");
    }
    [Test] public void PoolReturnClearsDamageAndWarningBeforeReuse()
    {
        var z=Zone();z.SetImpactElapsed(.07f);pool.Release(z.gameObject);
        // EditMode does not dispatch MonoBehaviour lifecycle for this non-ExecuteAlways component.
        // The live PlayMode probe separately checks the actual PoolManager -> OnDisable path.
        Call(z,"OnDisable");Assert.That(z.IsDamaging||z.IsVisible,Is.False);
        var reused=pool.Get(AssetDatabase.LoadAssetAtPath<GameObject>(DefenseOverseerAuthoring.Zone),Vector3.zero,Quaternion.identity);
        Assert.That(reused,Is.SameAs(z.gameObject));z.BeginWarning(Vector2.zero,1.05f,3,null);Assert.That(z.IsDamaging,Is.False);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void EveryBarragePlacementLeavesMoreThanPlayerWidthEscapeSpace(int index)
    {
        var bounds=new Bounds(Vector3.zero,new Vector3(10.9f,7.6f,1));
        foreach(var sample in new[]{new Vector2(-100,-100),Vector2.zero,new Vector2(100,100)})
        {
            var p=FrigateTriadBossController.ResolveBarrageTarget(sample,bounds,1.05f,index);
            Assert.That(p.x-1.05f,Is.GreaterThan(bounds.min.x));Assert.That(p.x+1.05f,Is.LessThan(bounds.max.x));
            Assert.That(Mathf.Max(p.x-1.05f-bounds.min.x,bounds.max.x-p.x-1.05f),Is.GreaterThan(2));
        }
    }
    [TestCase(3,.38f,.32f)] [TestCase(2,.28f,.2f)] [TestCase(1,.22f,.12f)]
    public void CadenceEscalatesWithoutChangingProjectileDefinition(int count,float cadence,float gap)
    {
        var c=Prefab();Assert.That(c.BatteryCadence(count),Is.EqualTo(cadence));Assert.That(c.BatteryGap(count),Is.EqualTo(gap));
        var definition=(ProjectileDefinition)Get(c,"bossProjectileDefinition");Assert.That(definition.Damage,Is.EqualTo(3));Assert.That(definition.Speed,Is.EqualTo(9));Assert.That(definition.Range,Is.EqualTo(12));
        Assert.That((float)Get(c,"warningAreaDuration"),Is.EqualTo(1));Assert.That((float)Get(c,"warningAreaDamage"),Is.EqualTo(3));
    }
    [TestCase(false)] [TestCase(true)]
    public void BurstConsumesCommittedDirectionAndFlashMatchesActualModule(bool right)
    {
        var c=Boss();Call(c,"CommitBatteryAim",right,new Vector2(0,-5));var aim=((Vector2[])Get(c,"defenseLockedDirections"))[0];
        Call(c,"FireDefenseBattery",0,right,aim);
        Assert.That(c.LastBatteryOrigin,Is.EqualTo((Vector2)c.DefenseMuzzle(0,right).position));Assert.That(c.LastBatteryDirection,Is.EqualTo(aim));
        var flash=((GameObject[])Get(c,"defenseFlashes"))[right?1:0];Assert.That((Vector2)flash.transform.position,Is.EqualTo(c.LastBatteryOrigin));
        Assert.That(Vector2.Angle(flash.transform.right,aim),Is.LessThan(.01f));
        c.transform.position=Vector2.right*5;Call(c,"FireDefenseBattery",0,right,aim);
        Assert.That(c.DefenseShots,Is.EqualTo(2));Assert.That(c.LastBatteryDirection,Is.EqualTo(aim));
        var bullets=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Where(b=>b.SourceRoot==c.transform).ToArray();
        Assert.That(bullets.Length,Is.EqualTo(2));foreach(var b in bullets) {roots.Add(b.gameObject);Assert.That(b.Speed,Is.EqualTo(9));Assert.That(b.MoveDirection,Is.EqualTo(aim));}
    }
    [Test] public void DestroyedPartCannotDispatchAndLivingSelectionUsesOnlySurvivors()
    {
        var c=Boss();var parts=(FrigateBossPart[])Get(c,"parts");Set(c,"initialized",false);
        Call(parts[0],"EnterDestroyedState",false);Call(c,"FireDefenseBattery",0,false,Vector2.down);Assert.That(c.DefenseShots,Is.Zero);
        Assert.That(Call(c,"LivingPartIndex",0),Is.EqualTo(1));Assert.That(Call(c,"LivingPartIndex",1),Is.EqualTo(2));
        Call(parts[1],"EnterDestroyedState",false);Assert.That(Call(c,"LivingPartIndex",2),Is.EqualTo(2));
    }
    [Test] public void BrokenPresentationPersistsThroughBarrageAndDoesNotReviveDestroyedPart()
    {
        var c=Boss();var parts=(FrigateBossPart[])Get(c,"parts");Set(c,"initialized",false);Call(parts[0],"EnterDestroyedState",false);
        var old=parts[0].VisualRenderer.sprite;Set(c,"defenseArmorBroken",true);Call(c,"SetDefenseBody",true);
        Assert.That(parts[0].VisualRenderer.sprite,Is.SameAs(old));Assert.That(parts[0].CanParticipateInPatterns,Is.False);
        Assert.That(parts[1].VisualRenderer.sprite,Is.SameAs(Get(c,"defenseBrokenSprite")));Call(c,"SetDefenseBody",false);
        Assert.That(parts[2].VisualRenderer.sprite,Is.SameAs(Get(c,"defenseBrokenSprite")));
    }
    [TestCase("StopCombatForDeathPresentation")] [TestCase("CleanupBossRuntime")]
    public void TeardownClearsPooledArtilleryFlashesAndSourceProjectiles(string entry)
    {
        var c=Boss();var z=Zone();z.SetImpactElapsed(.07f);Set(c,"defenseZone",z);Call(c,"FireDefenseBattery",0,false,Vector2.down);
        Call(c,entry);Assert.That(z.IsDamaging||z.IsVisible,Is.False);
        Assert.That(((GameObject[])Get(c,"defenseFlashes")).All(g=>g==null),Is.True);
        Assert.That(Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Any(b=>b.SourceRoot==c.transform),Is.False);
        Assert.That(Get(c,"patternRoutine"),Is.Null);
    }
    [Test] public void ExistingAmbientIsolationHidesOnlyDuringScopedDefenseEncounterAndRestoresFlags()
    {
        var c=Boss();typeof(FrigateTriadBossController).GetProperty("ActiveDefenseEncounter").SetValue(null,c);
        var g=New("Ordinary ambient");var r=g.AddComponent<SpriteRenderer>();var ai=g.AddComponent<EnemyBaseAI>();
        Call(ai,"ApplyBossEncounterIsolation");Assert.That(ai.IsBossEncounterIsolated,Is.True);Assert.That(r.forceRenderingOff,Is.True);
        Call(ai,"ReleaseBossEncounterIsolation",false);Assert.That(r.forceRenderingOff,Is.False);
        c.gameObject.SetActive(true);
        var child=New("Boss owned AI");child.transform.SetParent(c.transform);var own=child.AddComponent<EnemyBaseAI>();
        Assert.That(Call(own,"IsAmbientEnemyForBossEncounterIsolation"),Is.False);
    }
    [Test] public void ArmorTransitionIsOneTimeAndUsesExistingHealthWithoutAcceptingMidTransitionDamage()
    {
        var c=Boss();c.gameObject.SetActive(true);Set(c,"gameplayBegun",true);Set(c,"state",FrigateTriadBossState.Transition);Set(c,"alivePartCount",2);
        var corridor=New("Prepared corridor").AddComponent<SalvageDevourerCorridorController>();Set(corridor,"runtimeActive",true);Set(c,"corridorController",corridor);
        int token=(int)Get(c,"patternToken");float hp=c.AggregateHealth.CurrentHp;
        var first=(IEnumerator)Call(c,"DefenseTransitionRoutine",token,2);Assert.That(first.MoveNext(),Is.True);
        Assert.That(c.CurrentDefenseStage,Is.EqualTo(FrigateTriadBossController.DefenseStage.ArmorBreak));Assert.That(c.ArmorBreakCount,Is.EqualTo(1));
        Assert.That(c.CanAcceptPartDamage,Is.False);Assert.That(c.AggregateHealth.CurrentHp,Is.EqualTo(hp));
        var repeated=(IEnumerator)Call(c,"DefenseTransitionRoutine",token,2);repeated.MoveNext();Assert.That(c.ArmorBreakCount,Is.EqualTo(1));
        Set(c,"alivePartCount",1);var last=(IEnumerator)Call(c,"DefenseTransitionRoutine",token,1);last.MoveNext();
        Assert.That(c.ArmorBreakCount,Is.EqualTo(1));Assert.That(c.CurrentDefenseStage,Is.EqualTo(FrigateTriadBossController.DefenseStage.Reformation));
    }
    [Test] public void PooledZonesAndBatteriesHaveNoIndependentUpdateSchedulers()
    {
        Assert.That(typeof(DefenseBarrageZone).GetMethod("Update",Flags),Is.Null);
        Assert.That(typeof(DefenseBarrageZone).GetMethod("FixedUpdate",Flags),Is.Null);
        var c=Prefab();Assert.That(Get(c,"defenseBarragePrefab"),Is.Not.Null);Assert.That(Get(c,"defenseBatteryFlashPrefab"),Is.Not.Null);
        Assert.That((int)Get(c,"aimedBurstCount"),Is.EqualTo(3));
    }
    [Test] public void ScopedBriefingSuppressionPreservesPendingContentAndForeignOwner()
    {
        var hud=New("Briefing HUD").AddComponent<ExpeditionHUD>();var panel=New("Operation briefing");panel.SetActive(true);
        Set(hud,"operationRoot",panel);Set(hud,"operationBriefingPending",false);
        object bossOwner=new object(),otherOwner=new object();
        hud.SetOperationBriefingSuppressed(bossOwner,true);hud.SetOperationBriefingSuppressed(otherOwner,true);
        Assert.That(panel.activeSelf,Is.False);Assert.That(Get(hud,"operationBriefingPending"),Is.True);
        hud.SetOperationBriefingSuppressed(bossOwner,false);Assert.That(hud.IsOperationBriefingSuppressed,Is.True);
        hud.SetOperationBriefingSuppressed(otherOwner,false);Assert.That(hud.IsOperationBriefingSuppressed,Is.False);
        Assert.That(Get(hud,"operationBriefingPending"),Is.True,"briefing is deferred, not consumed or discarded");
    }
}
