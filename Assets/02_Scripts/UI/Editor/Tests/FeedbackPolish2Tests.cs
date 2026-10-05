using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class FeedbackPolish2Tests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> owned = new List<Object>();
    PoolManager previousPool, testPool;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Flags).Invoke(o, a);
    GameObject New(string n, params Type[] types) { var g = new GameObject(n, types); owned.Add(g); return g; }
    GameObject Prefab(string path) { var g = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)); owned.Add(g); return g; }
    [TearDown] public void Cleanup()
    {
        if (testPool != null) typeof(PoolManager).GetProperty("Instance").SetValue(null, previousPool);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    ExpeditionEventObject Reactor()
    {
        var e = Prefab(FeedbackPolishAuthoring.ReactorPath).GetComponent<ExpeditionEventObject>();
        Set(e, "useTween", false); Call(e, "KillTweens", false);
        Set(e, "countsAsHighValueObjective", false); Set(e, "reactorReward", null);
        Set(e, "reactorFailureBasic", 0); Set(e, "reactorFailureCharging", 0);
        var routine = (IEnumerator)Call(e, "ReactorRoutine"); routine.MoveNext(); routine.MoveNext();
        return e;
    }
    [Test] public void TimeoutRelinquishesTimerAndDamageAuthorityExactlyOnceWhileFailureCombatContinues()
    {
        var e = Reactor(); var enemy = New("Tracked", typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        ((List<EnemyHealth>)Get(e, "trackedEnemies")).Add(enemy);
        Set(e, "reactorTimer", 0f); Call(e, "UpdateReactorTimer");
        Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Active), "Failure combat still owns Active");
        Assert.That(Get(e, "phase").ToString(), Is.EqualTo("ReactorFailure"));
        Assert.That(e.CanReceiveEventDamage, Is.False);
        float stopped = (float)Get(e, "reactorTimer");
        for (int i = 0; i < 10; i++) { Call(e, "UpdateReactorTimer"); Call(e, "FailReactor"); }
        Assert.That(Get(e, "reactorTimer"), Is.EqualTo(stopped));
        e.ReceiveEventDamage(100); Call(e, "CompleteReactor");
        Assert.That(Get(e, "reactorHp"), Is.EqualTo(24f));
        Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Active));
        Assert.That(((ReactorFeedbackUI)Get(e, "reactorFeedback")).IsShowing, Is.False);
        ((List<EnemyHealth>)Get(e, "trackedEnemies")).Clear();
        var clear = (IEnumerator)Call(e, "ClearCombatPhaseRoutine"); clear.MoveNext(); clear.MoveNext();
        Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Failed));
    }
    [Test] public void ReactorSuccessRemainsAvailableBeforeFailure()
    {
        var e = Reactor(); Assert.That(e.CanReceiveEventDamage, Is.True);
        e.ReceiveEventDamage(24); Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Completed));
    }
    [TestCase(FeedbackPolish2Authoring.Rival)] [TestCase(FeedbackPolish2Authoring.Scavenger)]
    public void AuthoredChannelUsesExistingHalfSecondAndReusableBoundLines(string path)
    {
        var role = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyRoleController>();
        Assert.That(Get(role, "pickupCollectChannelDuration"), Is.EqualTo(.5f));
        var view = (PickupCollectionPresentation)Get(role, "pickupCollectionPresentation");
        Assert.That(view, Is.Not.Null);
        Assert.That(view.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(2));
        foreach(var line in view.GetComponentsInChildren<LineRenderer>())
        { Assert.That(line.enabled, Is.False); Assert.That(line.sharedMaterial, Is.Not.Null); }
    }
    RewardPickup Pickup()
    {
        if (testPool == null)
        {
            previousPool = PoolManager.Instance;
            testPool = New("Test pool", typeof(PoolManager)).GetComponent<PoolManager>();
            typeof(PoolManager).GetProperty("Instance").SetValue(null, testPool);
        }
        var p = testPool.Get(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/RewardPickup.prefab"), Vector3.zero, Quaternion.identity).GetComponent<RewardPickup>();
        owned.Add(p.gameObject); // Active pooled instances are detached from the pool root.
        Call(p, "Awake"); Call(p, "OnEnable");
        p.InitializeCurrency(CurrencyType.Credits, 3, Vector2.zero); Set(p, "activeAge", 100f); return p;
    }
    EnemyRoleController Thief(string path)
    {
        var role = Prefab(path).GetComponent<EnemyRoleController>();
        Call(role, "OnEnable");
        Set(role, "simulationGate", null);
        return role;
    }
    void Acquire(EnemyRoleController role, RewardPickup p)
    {
        p.transform.position = role.transform.position + Vector3.right * .2f;
        Call(role, "TryAcquireRewardPickup", (Vector2)role.transform.position, 1f, false);
        Assert.That(Get(role, "rewardPickupTarget"), Is.SameAs(p));
    }
    bool Advance(EnemyRoleController role, float dt) => (bool)Call(role, "UpdatePickupCollectionChannel", role.GetComponent<EnemyBaseAI>(), dt, 1f, false, true);
    [TestCase(FeedbackPolish2Authoring.Rival)] [TestCase(FeedbackPolish2Authoring.Scavenger)]
    public void ExactTargetChannelUsesExistingTimingAndCargoAccounting(string path)
    {
        var role = Thief(path); var p = Pickup(); var view = (PickupCollectionPresentation)Get(role, "pickupCollectionPresentation");
        Assert.That(view.IsShowing, Is.False); Acquire(role, p);
        Assert.That(view.IsShowing, Is.False, "Acquisition outside channel is not presentation");
        Assert.That(Advance(role, .25f), Is.False); Assert.That(view.Target, Is.SameAs(p)); Assert.That(view.IsShowing, Is.True);
        Assert.That(Get(role, "pickupChannelTimer"), Is.EqualTo(.25f));
        Assert.That(Advance(role, .26f), Is.True); Assert.That(view.IsShowing, Is.False);
        Assert.That(role.GetComponent<EnemyCargoHold>().GetAmount(CurrencyType.Credits), Is.EqualTo(3));
    }
    [TestCase("target")] [TestCase("leave")] [TestCase("death")] [TestCase("combat")] [TestCase("disable")]
    public void TheftChannelCancelsAtExistingLifecycleBoundaries(string boundary)
    {
        var role = Thief(FeedbackPolish2Authoring.Scavenger); var p = Pickup(); Acquire(role, p); Advance(role, .1f);
        var view = (PickupCollectionPresentation)Get(role, "pickupCollectionPresentation"); Assert.That(view.IsShowing, Is.True);
        switch(boundary)
        {
            case "target": p.gameObject.SetActive(false); Call(p,"OnDisable"); break;
            case "leave": p.transform.position += Vector3.right * 3; Advance(role, .1f); break;
            case "death": Call(role,"HandleDied", role.GetComponent<EnemyHealth>()); break;
            case "combat": role.TryHandleCombat(null, .1f); break;
            case "disable": Call(role,"OnDisable"); break;
        }
        Assert.That(view.IsShowing, Is.False); Assert.That(view.Target, Is.Null);
        Assert.That(role.GetComponent<EnemyCargoHold>().GetAmount(CurrencyType.Credits), Is.Zero);
    }
    [Test] public void TargetStillProtectedFromEnemiesHasNoChannel()
    {
        var role = Thief(FeedbackPolish2Authoring.Rival); var p = Pickup(); Acquire(role,p);
        Set(p,"activeAge",0f); Set(p,"enemyCollectionProtectionDuration",1f); Advance(role,.1f);
        Assert.That(((PickupCollectionPresentation)Get(role,"pickupCollectionPresentation")).IsShowing,Is.False);
        Assert.That(Get(role,"pickupChannelTimer"),Is.EqualTo(.5f));
    }
    [TestCase(false)] [TestCase(true)]
    public void PlayerDeathAndRunEndClearChannelSubscriptions(bool runEnd)
    {
        var role=Thief(FeedbackPolish2Authoring.Scavenger);var pickup=Pickup();
        var player=New("Player",typeof(PlayerHealth)).GetComponent<PlayerHealth>();
        var view=(PickupCollectionPresentation)Get(role,"pickupCollectionPresentation");
        view.Sample(role.transform,pickup,.5f,player);Assert.That(view.IsShowing,Is.True);
        if(runEnd) Call(view,"HandleRunEnded",new object[]{null});
        else ((Action)Get(player,"Died"))?.Invoke();
        Assert.That(view.IsShowing,Is.False);Assert.That(Get(view,"player"),Is.Null);
        Assert.That(Get(pickup,"BecameUnavailable"),Is.Null);
    }
    [Test] public void PipRefreshAccentsOnlyNewPipAndDoesNotReplay()
    {
        var hud = New("HUD").AddComponent<ExpeditionHUD>();
        var a = New("Old",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        var b = New("New",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        Set(hud,"coreSignalPips",new[]{a,b}); Set(hud,"presentedSignalCount",1);
        Call(hud,"HandleObjectiveProgressChanged",2,2);
        Assert.That(Get(hud,"accentedCorePip"),Is.SameAs(b.transform));
        var tween=Get(hud,"corePipAccent"); Call(hud,"HandleObjectiveProgressChanged",2,2);
        Assert.That(Get(hud,"corePipAccent"),Is.SameAs(tween)); Assert.That(a.transform.localScale,Is.EqualTo(Vector3.one));
        Call(hud,"ClearCorePipAccent"); Assert.That(b.transform.localScale,Is.EqualTo(Vector3.one));
    }
    [Test] public void EndedRunCannotResurrectChannelFromAZeroDeltaAiSample()
    {
        var previous = RunManager.Instance;
        try
        {
            var role = Thief(FeedbackPolish2Authoring.Rival); var pickup = Pickup();
            var view = (PickupCollectionPresentation)Get(role,"pickupCollectionPresentation");
            typeof(RunManager).GetProperty("Instance").SetValue(null,null);
            view.Sample(role.transform,pickup,.5f,null); Assert.That(view.IsShowing,Is.True);
            var ended = New("Ended run",typeof(RunManager)).GetComponent<RunManager>();
            typeof(RunManager).GetProperty("Instance").SetValue(null,ended);
            Call(view,"HandleRunEnded",new object[]{null});
            view.Sample(role.transform,pickup,.5f,null); Assert.That(view.IsShowing,Is.False);
            Assert.That(view.Target,Is.Null);
        }
        finally { typeof(RunManager).GetProperty("Instance").SetValue(null,previous); }
    }
    [Test] public void DeferredDeathDropIsOwnedIdempotentAndCancelledOnReuse()
    {
        var health = New("Deferred enemy", typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        object owner = new object(), foreign = new object();
        health.HoldDeathRewardForPresentation(owner);
        Assert.That(Get(health,"deathRewardPresentationOwner"), Is.Null, "Only the death boundary accepts a hold");
        Set(health,"isDead",true); health.HoldDeathRewardForPresentation(owner);
        Set(health,"deathRewardPending",true);
        health.ReleaseDeathRewardPresentation(foreign); Assert.That(Get(health,"deathRewardPending"),Is.True);
        health.ReleaseDeathRewardPresentation(owner); health.ReleaseDeathRewardPresentation(owner);
        Assert.That(Get(health,"deathRewardPending"),Is.False);
        health.HoldDeathRewardForPresentation(owner);Set(health,"deathRewardPending",true);
        health.CancelDeathRewardPresentation(owner);Assert.That(Get(health,"deathRewardPending"),Is.False);
        health.HoldDeathRewardForPresentation(owner);Set(health,"deathRewardPending",true);
        Call(health,"OnDisable");Assert.That(Get(health,"deathRewardPending"),Is.False);
        Assert.That(Get(health,"deathRewardPresentationOwner"),Is.Null);
    }
    [TestCase("Assets/03_Prefabs/Enemy/Boss.prefab", true)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab", false)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_RaiderSalvageCarrier.prefab", false)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_RaiderSniperCommander.prefab", false)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab", false)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab", false)]
    [TestCase("Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab", false)]
    public void OnlySectorControllerMayHoldLooseRewardEvenWhenCampaignIdIsShared(string path, bool expected)
    {
        var boss = Prefab(path).GetComponent<BossDummyController>(); Call(boss,"Awake");
        Set(boss,"resolvedDeathBossId",CampaignBossId.SectorAdministrator);
        Assert.That(Call(boss,"ShouldHoldLooseDeathReward"),Is.EqualTo(expected));
    }
}
