using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaiderReworkTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    readonly List<GameObject> roots=new List<GameObject>();
    GameObject Production=>AssetDatabase.LoadAssetAtPath<GameObject>(RaiderAssaultAuthoring.Boss);
    static object Get(object o,string field)=>o.GetType().GetField(field,F).GetValue(o);
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,F).SetValue(o,value);
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,F).Invoke(o,args);
    PirateCommanderBossController Fixture()
    {
        var g=new GameObject("Raider rework fixture");g.SetActive(false);roots.Add(g);
        var c=g.AddComponent<PirateCommanderBossController>();
        Set(c,"enemyHealth",g.GetComponent<EnemyHealth>());Set(c,"body",g.GetComponent<Rigidbody2D>());
        g.GetComponent<EnemyHealth>().SetMaxHp(125,true);return c;
    }
    [TearDown]public void Cleanup(){foreach(var g in roots)if(g!=null)Object.DestroyImmediate(g);roots.Clear();}
    [TestCase(0,PirateCommanderBossController.RaiderAttack.TrackingBurst)]
    [TestCase(1,PirateCommanderBossController.RaiderAttack.Scatter)]
    [TestCase(2,PirateCommanderBossController.RaiderAttack.TrackingBurst)]
    [TestCase(3,PirateCommanderBossController.RaiderAttack.ShieldRam)]
    [TestCase(7,PirateCommanderBossController.RaiderAttack.ShieldRam)]
    public void StarterLoopHasExactlyThreeSeparatedAttackTypes(int slot,PirateCommanderBossController.RaiderAttack expected)
    {Assert.That(PirateCommanderBossController.AttackForSlot(slot),Is.EqualTo(expected));}
    [TestCase(false,2,5)] [TestCase(true,3,7)]
    public void EscalationIsFinite(bool critical,int bursts,int pellets)
    {Assert.That(PirateCommanderBossController.TrackingBursts(critical),Is.EqualTo(bursts));Assert.That(PirateCommanderBossController.ScatterPellets(critical),Is.EqualTo(pellets));}
    [TestCase(false)] [TestCase(true)]
    public void ScatterHasSymmetricDiscreteGaps(bool critical)
    {
        int count=PirateCommanderBossController.ScatterPellets(critical);
        for(int i=0;i<count;i++)
        {
            float angle=PirateCommanderBossController.ScatterAngle(i,critical);
            Assert.That(angle,Is.EqualTo(-PirateCommanderBossController.ScatterAngle(count-1-i,critical)).Within(.001f));
            if(i>0)Assert.That(angle-PirateCommanderBossController.ScatterAngle(i-1,critical),Is.GreaterThanOrEqualTo(11.9f));
        }
    }
    [TestCase(1,0)] [TestCase(-1,0)] [TestCase(0,1)] [TestCase(0,-1)] [TestCase(1,1)]
    public void CommittedRamRetainsHullInsideFixedArena(float x,float y)
    {
        Vector2 start=new Vector2(3,2),direction=new Vector2(x,y).normalized;
        Vector2 end=PirateCommanderBossController.ResolveRamEnd(start,direction,Vector2.zero,Vector2.one*8.4f,30);
        Assert.That(Mathf.Abs(end.x),Is.LessThanOrEqualTo(6.901f));Assert.That(Mathf.Abs(end.y),Is.LessThanOrEqualTo(6.901f));
        Assert.That(Vector2.Dot((end-start).normalized,direction),Is.GreaterThan(.999f));
    }
    [Test] public void ShieldActuallyAbsorbsBeforeHpAndCanBeExhausted()
    {
        var c=Fixture();Set(c,"combatActive",true);Call(c,"BeginShieldCombat");var hp=c.GetComponent<EnemyHealth>();
        hp.TakeDamage(10);Assert.That(c.ShieldHp,Is.EqualTo(8));Assert.That(hp.CurrentHp,Is.EqualTo(125));
        hp.TakeDamage(9);Assert.That(c.IsShieldProtected,Is.False);Assert.That(hp.CurrentHp,Is.EqualTo(125));
        hp.TakeDamage(3);Assert.That(hp.CurrentHp,Is.EqualTo(122));
    }
    [Test] public void IntroProtectionDoesNotResetShieldOnCombatHandoff()
    {
        var c=Fixture();c.BeginIntroShield();Assert.That(c.TryAbsorbDamage(100,Vector2.zero,Vector2.up),Is.True);Assert.That(c.ShieldHp,Is.EqualTo(18));
        Set(c,"combatActive",true);Call(c,"BeginShieldCombat");Assert.That(Get(c,"introShield"),Is.False);Assert.That(c.ShieldHp,Is.EqualTo(18));
    }
    [TestCase("StopCombat")] [TestCase("OnDisable")] [TestCase("HandlePlayerDied")]
    public void EveryAbortClearsProtectionAndRamState(string entry)
    {
        var c=Fixture();c.BeginIntroShield();Set(c,"ramHitPlayer",true);Call(c,entry);
        Assert.That(c.IsShieldProtected,Is.False);Assert.That(c.ShieldHp,Is.Zero);Assert.That(c.RamHitPlayer,Is.False);
        Assert.That(c.Stage,Is.EqualTo(PirateCommanderBossController.AssaultStage.Stopped));
    }
    [Test] public void ProductionUsesApprovedFieldAndExactRamPivotWithoutColliders()
    {
        var c=Production.GetComponent<PirateCommanderBossController>();var view=(RaiderShieldPresentation)Get(c,"shieldPresentation");Assert.That(view,Is.Not.Null);
        Assert.That(view.GetComponentsInChildren<Collider2D>(true),Is.Empty);
        var field=(SpriteRenderer)Get(view,"normalField");Assert.That(field.sprite.texture.width,Is.EqualTo(128));Assert.That(field.transform.localScale.x,Is.EqualTo(field.transform.localScale.y));
        var ram=(PhaseCombatVfx)Get(view,"ramField");var frames=(Sprite[])Get(ram,"frames");Assert.That(frames.Length,Is.EqualTo(13));
        Assert.That(frames.All(s=>s.rect.size==new Vector2(96,48)&&s.pivot==new Vector2(48,0)),Is.True);
        Assert.That(((float[])Get(ram,"frameDurations")).Sum(),Is.EqualTo(1.21f).Within(.0001f));
        foreach(string name in new[]{"RamWarningFill","RamWarningBorder"})Assert.That(Production.transform.Find(name).GetComponents<Collider2D>(),Is.Empty);
    }
    [Test] public void TelegraphAndProtectionClearWithoutDisablingTheBoss()
    {
        var g=Object.Instantiate(Production);g.SetActive(false);roots.Add(g);var view=g.GetComponentInChildren<RaiderShieldPresentation>(true);
        view.ShowNormal(true);view.ShowLane(Vector2.zero,Vector2.up*6,2.6f);Assert.That(view.LaneVisible&&view.NormalVisible,Is.True);
        view.Clear();Assert.That(view.LaneVisible||view.NormalVisible,Is.False);
    }
    [TestCase("29_RaiderRamShield/Raider_ShieldField")]
    [TestCase("29_RaiderRamShield/VFX_Raider_RamShield")]
    [TestCase("02_Core/gray_raider/core_gray_raider_inactive")]
    [TestCase("02_Core/gray_raider/core_gray_raider_activation")]
    public void ImportedArtIsByteIdentical(string source)
    {Assert.That(File.ReadAllBytes(RaiderReworkAuthoring.Art+Path.GetFileName(source)+".png"),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png")));}
    [Test] public void DestroyedCommanderReferenceAllowsCoreTeardown()
    {
        var root=new GameObject("Intro teardown fixture");root.SetActive(false);roots.Add(root);
        var intro=root.AddComponent<CoreBossIntroSequence>();var deadBoss=new GameObject("Destroyed commander");
        Set(intro,"raiderCommanderIntro",true);Set(intro,"spawnedBoss",deadBoss);Object.DestroyImmediate(deadBoss);
        Assert.DoesNotThrow(()=>Call(intro,"OnDisable"));
    }
    [Test] public void GrayPresentationDoesNotRenumberRegionalFamilies()
    {
        Assert.That(CoreFamilyPresentation.FamilyIndex(ExpeditionDepth.Normal),Is.Zero);Assert.That(CoreFamilyPresentation.FamilyIndex(ExpeditionDepth.FinalNetwork),Is.EqualTo(3));
        var core=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab").GetComponent<CoreFamilyPresentation>();
        foreach(string f in new[]{"grayController","grayIdle","grayIcon","grayShard","grayPulse"})Assert.That((Object)Get(core,f),Is.Not.Null,f);
    }
}
