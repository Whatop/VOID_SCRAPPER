using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class FeedbackPolish3Tests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    readonly List<Object> owned=new List<Object>();
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
    GameObject New(string n,params Type[] types){var g=new GameObject(n,types);owned.Add(g);return g;}
    T Own<T>(T o) where T:Object {owned.Add(o);return o;}
    ReinforcementDefinition Def(string n)=>AssetDatabase.LoadAssetAtPath<ReinforcementDefinition>("Assets/02_Scripts/Config/ReinforcementDefinition/Common/"+n+".asset");
    [TearDown] public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);owned.Clear();}
    PlayerReinforcementController Controller()=>New("Player",typeof(PlayerReinforcementController)).GetComponent<PlayerReinforcementController>();
    [Test] public void EquipAcknowledgesOnceAfterAuthorityWithUnchangedCharges()
    {
        var c=Controller();var d=Def("04_rf_burst_barrier");int count=0;
        c.AcquisitionAcknowledged+=actual=>{count++;Assert.That(c.EquippedDefinition,Is.SameAs(actual));Assert.That(c.CurrentCharges,Is.EqualTo(d.MaxCharges));};
        Assert.That(c.Equip(d,-1,false),Is.True);Assert.That(count,Is.EqualTo(1));
    }
    [Test] public void FailedNullEquipDoesNotAcknowledgeOrChangeExistingItem()
    {
        var c=Controller();var d=Def("04_rf_burst_barrier");c.Equip(d,0,false);int count=0;c.AcquisitionAcknowledged+=_=>count++;
        Assert.That(c.Equip(null),Is.False);Assert.That(count,Is.Zero);Assert.That(c.EquippedDefinition,Is.SameAs(d));Assert.That(c.CurrentCharges,Is.Zero);
    }
    [Test] public void TransactionCanCommitEquipmentBeforeOneFinalAcknowledgement()
    {
        var c=Controller();int count=0;c.AcquisitionAcknowledged+=_=>count++;
        Assert.That(c.EquipWithoutDropping(Def("04_rf_burst_barrier"),0,false,false),Is.True);
        Assert.That(count,Is.Zero);c.ConfirmAcquisition();Assert.That(count,Is.EqualTo(1));Assert.That(c.CurrentCharges,Is.Zero);
    }
    [Test] public void FieldReplacementKeepsExactOldPickupAndChargesAndAcknowledgesOnce()
    {
        var c=Controller();var old=Def("04_rf_burst_barrier");var next=Def("08_rf_overdrive_injector");c.Equip(old,0,false);
        var p=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/ReinforcementPickup.prefab"))).GetComponent<ReinforcementPickup>();
        p.Initialize(next,1,0);int count=0;c.AcquisitionAcknowledged+=_=>{count++;Assert.That(p.ReinforcementDefinition,Is.SameAs(old));};
        p.Interact(c.gameObject);Assert.That(count,Is.EqualTo(1));Assert.That(c.EquippedDefinition,Is.SameAs(next));Assert.That(c.CurrentCharges,Is.EqualTo(1));Assert.That(p.StoredCharges,Is.Zero);
    }
    [Test] public void BlockedPickupLeavesBothItemsAndNoAcknowledgement()
    {
        var c=Controller();var old=Def("04_rf_burst_barrier");var next=Def("08_rf_overdrive_injector");c.Equip(old,0,false);
        var p=New("Pickup",typeof(CircleCollider2D),typeof(ReinforcementPickup)).GetComponent<ReinforcementPickup>();p.Initialize(next,1,10);
        int count=0;c.AcquisitionAcknowledged+=_=>count++;p.Interact(c.gameObject);
        Assert.That(count,Is.Zero);Assert.That(c.EquippedDefinition,Is.SameAs(old));Assert.That(p.ReinforcementDefinition,Is.SameAs(next));
    }
    ReinforcementSlotUI Slot()
    {
        var g=New("Slot",typeof(RectTransform),typeof(CanvasGroup),typeof(ReinforcementSlotUI));var s=g.GetComponent<ReinforcementSlotUI>();
        Image Img(string n){var o=New(n,typeof(RectTransform),typeof(Image));o.transform.SetParent(g.transform,false);return o.GetComponent<Image>();}
        var text=New("Charges",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(g.transform,false);
        s.ConfigureRuntime(g,g.GetComponent<CanvasGroup>(),Img("Icon"),Img("Recharge"),Img("Duration"),null,Img("Ready"),text,null,Img("Disabled"));return s;
    }
    [Test] public void RepeatedAccentReplacesTweenAndDisableRestoresScale()
    {
        var s=Slot();var scale=s.transform.localScale;s.PresentAcquisition();var first=Get(s,"acquisitionAccent");
        s.PresentAcquisition();Assert.That(Get(s,"acquisitionAccent"),Is.Not.SameAs(first));Call(s,"OnDisable");Assert.That(Get(s,"acquisitionAccent"),Is.Null);Assert.That(s.transform.localScale,Is.EqualTo(scale));
    }
    [Test] public void ClearingEquipmentCancelsAcquisitionAccent()
    {var s=Slot();s.PresentAcquisition();s.SetEmpty();Assert.That(Get(s,"acquisitionAccent"),Is.Null);}
    [TestCase(true,false)] [TestCase(false,true)]
    public void DurationAndRechargeNeverCompeteForIconFill(bool active,bool rechargeVisible)
    {
        var s=Slot();s.SetState(Def("08_rf_overdrive_injector"),0,1,.25f,.25f,true,false,active,.7f);
        Assert.That(((Image)Get(s,"activeDurationFillImage")).enabled,Is.EqualTo(active));Assert.That(((Image)Get(s,"iconRechargeFillImage")).enabled,Is.EqualTo(rechargeVisible));
        if(active)Assert.That(((Image)Get(s,"activeDurationFillImage")).fillAmount,Is.EqualTo(.7f));
    }
    [Test] public void ReadyAndChargeCountRetainAuthoredState()
    {
        var s=Slot();s.SetState(Def("08_rf_overdrive_injector"),2,3,.5f,.8f,true,true,false,0);
        Assert.That(((TMP_Text)Get(s,"chargeText")).text,Is.EqualTo("2"));Assert.That(((Image)Get(s,"readyGlowImage")).enabled,Is.True);
    }
    InputActionAsset Inputs()
    {
        var input=Own(ScriptableObject.CreateInstance<InputActionAsset>());var map=new InputActionMap("Player");input.AddActionMap(map);
        map.AddAction("Interact",binding:"<Keyboard>/f");map.AddAction("Dismantle",binding:"<Keyboard>/g");map.AddAction("Map",binding:"<Keyboard>/tab");map.AddAction("Inventory",binding:"<Keyboard>/e");map.AddAction("Reinforcement",binding:"<Keyboard>/r");return input;
    }
    [TestCase("Interact","j","J")] [TestCase("Dismantle","k","K")] [TestCase("Map","m","M")] [TestCase("Inventory","i","I")] [TestCase("Reinforcement","q","Q")]
    public void HintLabelsUseCurrentReboundKeyboardControls(string name,string key,string expected)
    {var input=Inputs();InputBindingUtility.ResolveAction(input,"Player",name);input.FindAction(name).ApplyBindingOverride(0,"<Keyboard>/"+key);Assert.That(InputBindingUtility.GetDisplayString(input,"Player",name,"fallback"),Is.EqualTo(expected));}
    [Test] public void ContextHintSuppressionReleasesOnlyItsOwnLease()
    {
        var h=New("HUD",typeof(ExpeditionHUD)).GetComponent<ExpeditionHUD>();Set(h,"inputActions",Inputs());Set(h,"menuHintRoot",New("Menu"));
        var p=New("Prompt",typeof(RectTransform),typeof(CanvasGroup),typeof(InteractionPromptUI)).GetComponent<InteractionPromptUI>();Set(p,"hintHud",h);
        var boss=new object();h.SetMenuHintsSuppressed(boss,true);Call(p,"UpdateMenuHintPriority",true);Call(p,"UpdateMenuHintPriority",true);
        var owners=(HashSet<object>)Get(h,"menuHintSuppressors");Assert.That(owners.Count,Is.EqualTo(2));Call(p,"OnDisable");Assert.That(owners.Count,Is.EqualTo(1));Assert.That(owners.Contains(boss),Is.True);
    }
    TutorialFlowController Flow()
    {
        var f=New("Flow",typeof(TutorialFlowController)).GetComponent<TutorialFlowController>();var player=New("Player",typeof(Rigidbody2D),typeof(PlayerDash));
        Set(f,"playerRoot",player.transform);Set(f,"playerDash",player.GetComponent<PlayerDash>());Set(f,"dashPracticeMarker",New("Marker",typeof(LineRenderer)).GetComponent<LineRenderer>());Set(f,"currentStep",TutorialStep.Dash);return f;
    }
    [Test] public void DashPracticeRefreshDoesNotRestartMarkerOrMoveCenter()
    {
        var f=Flow();Call(f,"RefreshDashPractice");var tween=Get(f,"dashPracticeTween");var center=Get(f,"dashPracticeCenter");
        ((Transform)Get(f,"playerRoot")).position=Vector3.right;Call(f,"RefreshDashPractice");Assert.That(Get(f,"dashPracticeTween"),Is.SameAs(tween));Assert.That(Get(f,"dashPracticeCenter"),Is.EqualTo(center));Call(f,"ClearDashPractice");
    }
    [TestCase(false)] [TestCase(true)] public void MovementOrCancelledDashCannotCompleteCheckpoint(bool cancelledDash)
    {
        var f=Flow();Call(f,"RefreshDashPractice");if(cancelledDash)Call(f,"HandleTeachingDashStarted",Vector2.up);
        ((Transform)Get(f,"playerRoot")).position=Vector3.up*2;Call(f,"HandleDashEnded");
        Assert.That(f.CurrentStep,Is.EqualTo(TutorialStep.Dash));Assert.That(Get(f,"controlPacingRoutine"),Is.Null);Call(f,"ClearDashPractice");
    }
    [Test] public void DashOutsidePracticeZoneDoesNotQualify()
    {
        var f=Flow();Call(f,"RefreshDashPractice");((Transform)Get(f,"playerRoot")).position=Vector3.right*2;Call(f,"HandleTeachingDashStarted",Vector2.up);Assert.That(Get(f,"dashAttemptStartedInZone"),Is.False);Call(f,"ClearDashPractice");
    }
    [Test] public void AbortAndReentryClearAttemptAndReplaceMarkerTween()
    {
        var f=Flow();Call(f,"RefreshDashPractice");Call(f,"HandleTeachingDashStarted",Vector2.up);Call(f,"ClearDashPractice");
        Assert.That(Get(f,"dashAttemptStartedInZone"),Is.False);Assert.That(Get(f,"dashPracticeTween"),Is.Null);Assert.That(((LineRenderer)Get(f,"dashPracticeMarker")).enabled,Is.False);
        ((Transform)Get(f,"playerRoot")).position=Vector3.right*2;Call(f,"RefreshDashPractice");Assert.That(Get(f,"dashPracticeCenter"),Is.EqualTo(Vector2.right*2));Call(f,"ClearDashPractice");
    }
    [Test] public void AcquisitionPlacementDoesNotDriftAndRestoresForOtherMessages()
    {
        var g=New("Messages",typeof(RectTransform),typeof(WarningMessageUI));var view=g.GetComponent<WarningMessageUI>();
        var rect=(RectTransform)g.transform;rect.anchoredPosition=new Vector2(0,-32);
        Call(view,"SetAcquisitionPlacement",true);Call(view,"SetAcquisitionPlacement",true);
        Assert.That(rect.anchoredPosition.y,Is.EqualTo(-44));Call(view,"SetAcquisitionPlacement",false);
        Assert.That(rect.anchoredPosition.y,Is.EqualTo(-32));
    }
    [Test] public void DeathClearsDashTeachingMarkerAndAttempt()
    {
        var f=Flow();Call(f,"RefreshDashPractice");Call(f,"HandleTeachingDashStarted",Vector2.up);Call(f,"HandleTeachingPlayerDied");
        Assert.That(Get(f,"dashPracticeTween"),Is.Null);Assert.That(Get(f,"dashAttemptStartedInZone"),Is.False);
        Assert.That(((LineRenderer)Get(f,"dashPracticeMarker")).enabled,Is.False);
    }

}
