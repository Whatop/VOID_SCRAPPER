using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class FeedbackPolishTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<Object> owned = new List<Object>();
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    GameObject New(string name, params Type[] components) { var g = new GameObject(name, components); owned.Add(g); return g; }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    ExpeditionEventObject Reactor()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackPolishAuthoring.ReactorPath);
        var g = Object.Instantiate(prefab); owned.Add(g);
        var e = g.GetComponent<ExpeditionEventObject>();
        Set(e, "useTween", false); Call(e, "KillTweens", false);
        return e;
    }
    static void Activate(ExpeditionEventObject e)
    {
        var routine = (IEnumerator)Call(e, "ReactorRoutine");
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Arming));
        Assert.That(((ReactorFeedbackUI)Get(e, "reactorFeedback")).IsShowing, Is.False);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(e.State, Is.EqualTo(ExpeditionEventState.Active));
    }
    [Test] public void ReactorSavedBindingsAndGameplayValuesRemainAuthored()
    {
        var e = AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackPolishAuthoring.ReactorPath).GetComponent<ExpeditionEventObject>();
        Assert.That(Get(e,"reactorMaxHp"), Is.EqualTo(24f));
        Assert.That(Get(e,"reactorTimeLimit"), Is.EqualTo(12f));
        Assert.That(Get(e,"reactorChargeTime"), Is.EqualTo(.75f));
        Assert.That(Get(e,"reactorReward"), Is.Not.Null);
        var view = (ReactorFeedbackUI)Get(e,"reactorFeedback");
        Assert.That(view, Is.Not.Null); Assert.That(view.gameObject.activeSelf, Is.False);
        Assert.That(Get(view,"countdownText"), Is.Not.Null); Assert.That(Get(view,"healthText"), Is.Not.Null);
    }
    [Test] public void ReactorHpAndCountdownAreIndependentAndBeginWithTimedState()
    {
        var e = Reactor(); var view = (ReactorFeedbackUI)Get(e,"reactorFeedback"); Activate(e);
        var progress = (Transform)Get(e,"progressRoot");
        Assert.That(progress.localScale.x, Is.EqualTo(1.45f).Within(.001f));
        Set(e,"reactorTimer",3.2f); Call(e,"UpdateReactorTimer");
        Assert.That(progress.localScale.x, Is.EqualTo(1.45f).Within(.001f));
        Assert.That(((TMP_Text)Get(view,"countdownText")).text, Is.EqualTo("4s"));
        e.ReceiveEventDamage(12);
        Assert.That(progress.localScale.x, Is.EqualTo(1f).Within(.001f));
        Assert.That(((TMP_Text)Get(view,"healthText")).text, Is.EqualTo("HP 50%"));
        Assert.That(((TMP_Text)Get(view,"countdownText")).text, Is.EqualTo("4s"));
    }
    [TestCase(.01f, "1s")] [TestCase(0f, "0s")] [TestCase(-.01f, "0s")]
    public void CountdownNeverDisplaysZeroEarly(float seconds, string expected)
    {
        var e=Reactor(); Activate(e); var view=(ReactorFeedbackUI)Get(e,"reactorFeedback");
        view.Refresh(seconds,1); Assert.That(((TMP_Text)Get(view,"countdownText")).text,Is.EqualTo(expected));
    }
    [TestCase("CompleteReactor")] [TestCase("FailReactor")] [TestCase("OnDisable")] [TestCase("ResetRuntime")]
    public void ReactorTerminalAndTeardownPathsClearAndCannotResurrectCountdown(string method)
    {
        var e=Reactor(); Activate(e); var view=(ReactorFeedbackUI)Get(e,"reactorFeedback");
        // Outcome-specific side effects are tested by existing event regressions, not fixture loot/enemies.
        Set(e,"reactorReward",null); Set(e,"countsAsHighValueObjective",false);
        Set(e,"reactorFailureBasic",0); Set(e,"reactorFailureCharging",0);
        Call(e,method); view.Refresh(5,1);
        Assert.That(view.IsShowing,Is.False); Assert.That(view.gameObject.activeSelf,Is.False);
        Assert.That(((TMP_Text)Get(view,"countdownText")).text,Is.Empty);
    }
    [Test] public void CountdownPlayerDeathAndRunEndDetachPresentation()
    {
        var e=Reactor(); var view=(ReactorFeedbackUI)Get(e,"reactorFeedback");
        var p=New("Player",typeof(PlayerHealth)).GetComponent<PlayerHealth>();
        view.Begin(12,1,p,null);
        ((Action)Get(p,"Died"))?.Invoke();
        Assert.That(view.IsShowing,Is.False); Assert.That(Get(view,"player"),Is.Null);
        view.Begin(12,1,p,null); Call(view,"RunEnded",new object[]{null});
        Assert.That(view.IsShowing,Is.False); Assert.That(Get(view,"run"),Is.Null);
    }
    [Test] public void ArmorBreakOccursOnlyOnDamageDepletionAndCanRearmAfterRestoration()
    {
        var armor=New("Armor",typeof(PlayerArmor)).GetComponent<PlayerArmor>();
        int breaks=0; armor.Broken+=()=>breaks++; armor.SetMaxArmor(10,true);
        Assert.That(armor.AbsorbDamage(3),Is.Zero); Assert.That(armor.CurrentArmor,Is.EqualTo(7)); Assert.That(breaks,Is.Zero);
        Assert.That(armor.AbsorbDamage(8),Is.EqualTo(1)); Assert.That(breaks,Is.EqualTo(1));
        Assert.That(armor.AbsorbDamage(2),Is.EqualTo(2)); Assert.That(breaks,Is.EqualTo(1));
        armor.RestoreCurrentArmor(4); armor.AbsorbDamage(4); Assert.That(breaks,Is.EqualTo(2));
        armor.SetArmor(8); armor.SetMaxArmor(0,false); Assert.That(breaks,Is.EqualTo(2),"Unequipping capacity is not combat damage");
    }
    [Test] public void ArmorBreakUsesOneFiniteEmptyTrackAccentAndNoExtraHitAudio()
    {
        var root=New("HUD",typeof(RectTransform)); root.SetActive(false);
        var hud=root.AddComponent<ExpeditionHUD>(); var track=New("Track",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(root.transform); Set(hud,"armorTrackImage",track);
        string source=File.ReadAllText("Assets/02_Scripts/UI/ExpeditionHUD.cs");
        string method=source.Substring(source.IndexOf("private void HandleArmorBroken()",StringComparison.Ordinal));
        method=method.Substring(0,method.IndexOf("private void ClearArmorBreakAccent",StringComparison.Ordinal));
        Assert.That(method,Does.Not.Contain("AudioManager")); Assert.That(method,Does.Not.Contain("AddShake"));
        // Finite tween behavior is exercised in the Play Mode fixture with the authored HUD.
        Assert.That(File.ReadAllText("Assets/02_Scripts/Player/PlayerHealth.cs"),Does.Contain("AudioManager.PlayAt(SoundEventIds.ShipHit, hitPoint)"));
    }
    StatusEffectSlotUI Slot()
    {
        var g=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/StatusEffectSlot.prefab")); owned.Add(g);
        var slot=g.GetComponent<StatusEffectSlotUI>();
        slot.Bind("trait", "Trait", "Description", null, Color.cyan, false, 2, false,0,0); return slot;
    }
    [Test] public void TraitAccentRetainsIdentityAndCountAndReleaseClearsItsLease()
    {
        var slot=Slot(); slot.PlayAcquisitionAccent(); var first=(Tween)Get(slot,"acquisitionTween");
        Assert.That(first,Is.Not.Null);
        slot.PlayAcquisitionAccent();
        // EditMode has no DOTween player-loop disposal pass. The native Play Mode fixture
        // verifies exactly one live tween after repeated acquisition in a real frame.
        Assert.That(slot.IsAcquisitionAccentActive,Is.True); Assert.That(slot.StatusId,Is.EqualTo("trait"));
        Assert.That(((TMP_Text)Get(slot,"stackCountText")).text,Does.Contain("2"));
        slot.Release(); Assert.That(slot.IsAcquisitionAccentActive,Is.False);
        Assert.That(slot.StatusId,Is.Empty);
    }
    [Test] public void SlotRebindingAndDisableCancelPreviousTraitAccent()
    {
        var slot=Slot(); slot.PlayAcquisitionAccent();
        slot.Bind("other","Other","",null,Color.white,false,1,false,0,0);
        Assert.That(slot.IsAcquisitionAccentActive,Is.False);
        slot.PlayAcquisitionAccent(); slot.gameObject.SetActive(false); Call(slot,"OnDisable");
        Assert.That(slot.IsAcquisitionAccentActive,Is.False);
    }
    [Test] public void TraitFailureCannotEnterSuccessPresentationAndGrantOwnerIsRetained()
    {
        string pickup=File.ReadAllText("Assets/02_Scripts/RunRuntime/TraitPickup.cs");
        int grant=pickup.IndexOf("RunTraitAcquisitionService.TryAcquireFieldPickup",StringComparison.Ordinal);
        int denied=pickup.IndexOf("SoundEventIds.ActionDenied",grant,StringComparison.Ordinal);
        int success=pickup.IndexOf("acquireHud.ShowTraitAcquired",denied,StringComparison.Ordinal);
        Assert.That(pickup.Substring(denied,success-denied),Does.Contain("return;"));
        Assert.That(pickup,Does.Not.Contain("acquireHud.ShowWarning"));
        string hud=File.ReadAllText("Assets/02_Scripts/UI/ExpeditionHUD.cs");
        Assert.That(hud,Does.Contain("ShipCommunicationSeverity.Confirmation, 2f"));
        Assert.That(hud,Does.Contain("newLevel <= previousLevel"));
    }
    [TestCase(SoundEventIds.BossChargeAim)] [TestCase(SoundEventIds.BossChargeFire)] [TestCase(SoundEventIds.BossPhase2)]
    public void ReusedBossCuesHaveExistingClipsAndKeepWorldRouting(string id)
    {
        var db=AssetDatabase.LoadAssetAtPath<AudioEventDatabase>("Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset");
        Assert.That(db.TryGet(id,out var definition),Is.True); Assert.That(definition.GetRandomClip(),Is.Not.Null);
        Assert.That(definition.SpatialMode,Is.Not.EqualTo(AudioSpatialMode.Force2D)); Assert.That(definition.Loop,Is.False);
    }
    [Test] public void EmptyHijackAndMissingSoundRemainSafeAndExplicitlyUnresolved()
    {
        var db=AssetDatabase.LoadAssetAtPath<AudioEventDatabase>("Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset");
        Assert.That(db.TryGet(SoundEventIds.DialogueCommHijack,out var cue),Is.True);
        Assert.That(cue.GetRandomClip(),Is.Null); Assert.That(db.TryGet("missing_feedback_test",out _),Is.False);
    }
    [Test] public void NewlyConnectedAudioHasOneControllerOwnerAndNoViewDispatcher()
    {
        string sniper=File.ReadAllText("Assets/02_Scripts/Boss/RaiderSniperCommanderBossController.cs");
        foreach(string cue in new[]{"BossChargeAim","BossChargeFire","BossPhase2"})
            Assert.That(sniper.Split(new[]{"AudioManager.PlayAt(SoundEventIds."+cue},StringSplitOptions.None).Length-1,Is.EqualTo(1));
        string carrier=File.ReadAllText("Assets/02_Scripts/Boss/RaiderSalvageCarrierBossController.cs");
        Assert.That(carrier.Split(new[]{"AudioManager.PlayAt(SoundEventIds.BossPhase2"},StringSplitOptions.None).Length-1,Is.EqualTo(1));
        Assert.That(File.ReadAllText("Assets/02_Scripts/Boss/RaiderRailShot.cs"),Does.Not.Contain("AudioManager"));
    }
}
