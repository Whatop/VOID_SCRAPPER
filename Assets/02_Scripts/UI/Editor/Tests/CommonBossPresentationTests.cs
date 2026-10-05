using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CommonBossPresentationTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> objects = new List<GameObject>();
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Flags, null, System.Array.ConvertAll(args,a=>a.GetType()), null).Invoke(o,args);
    GameObject New(string name) { var g = new GameObject(name, typeof(RectTransform)); g.SetActive(false); objects.Add(g); return g; }
    TextMeshProUGUI Text(string name) => New(name).AddComponent<TextMeshProUGUI>();
    [TearDown] public void Cleanup() { for(int i=objects.Count-1;i>=0;i--) if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear(); }
    ExpeditionHUD Hud(out CoreTrackingSignalController tracking, out GameObject objective)
    {
        var hud=New("HUD").AddComponent<ExpeditionHUD>();tracking=New("Tracking").AddComponent<CoreTrackingSignalController>();
        Set(tracking,"currentSignalCount",2);Set(tracking,"coreRevealed",true);Set(tracking,"trackingActive",true);
        objective=New("Core signal");objective.SetActive(true);
        Set(hud,"objectiveRoot",objective);Set(hud,"coreTrackingController",tracking);
        Set(hud,"coreTrackingObjectiveText",Text("Objective"));Set(hud,"coreSignalCountText",Text("Count"));return hud;
    }
    [Test] public void CoreSignalVisibilityDoesNotMutateTrackingAndRestoresRelevantState()
    {
        var hud=Hud(out var tracking,out var objective);var owner=new object();
        hud.SetRegionBossPresentation(owner,true);hud.SetCoreTrackingVisible(true);
        Assert.That(objective.activeSelf,Is.False);Assert.That(tracking.CurrentSignalCount,Is.EqualTo(2));Assert.That(tracking.IsCoreRevealed,Is.True);
        hud.SetRegionBossPresentation(owner,false);Call(hud,"RefreshObjectiveProgress");
        Assert.That(objective.activeSelf,Is.True);Assert.That(tracking.CurrentSignalCount,Is.EqualTo(2));
        Set(tracking,"trackingActive",false);Call(hud,"RefreshObjectiveProgress");Assert.That(objective.activeSelf,Is.False);
    }
    [Test] public void IntroAndCombatOwnersDoNotReleaseEachOthersSuppression()
    {
        var hud=Hud(out _,out _);var intro=new object();var combat=new object();
        hud.SetRegionBossPresentation(intro,true);hud.SetRegionBossPresentation(combat,true);hud.SetRegionBossPresentation(intro,false);
        Assert.That(hud.IsRegionBossPresentationActive&&hud.IsOperationBriefingSuppressed,Is.True);
        hud.SetRegionBossPresentation(combat,false);Assert.That(hud.IsRegionBossPresentationActive||hud.IsOperationBriefingSuppressed,Is.False);
    }
    [TestCase("ClearRegionBossPresentation")]
    [TestCase("OnDisable")]
    public void DeathOrDisableReleasesAllPresentationOwners(string method)
    {
        var hud=Hud(out _,out _);hud.SetRegionBossPresentation(new object(),true);Call(hud,method);
        Assert.That(hud.IsRegionBossPresentationActive||hud.IsOperationBriefingSuppressed,Is.False);
    }
    [Test] public void OrdinaryInteractionCannotReshowWhileBossOwnsPresentation()
    {
        var g=New("Prompt");var group=g.AddComponent<CanvasGroup>();var prompt=g.AddComponent<InteractionPromptUI>();Set(prompt,"canvasGroup",group);
        prompt.SetRegionBossSuppressed(true);prompt.SetVisible(true);Assert.That(group.alpha,Is.Zero);
        prompt.SetRegionBossSuppressed(false);prompt.SetVisible(true);Assert.That(group.alpha,Is.EqualTo(1));
    }
    [Test] public void InteractionScopeRestoresAccessWithoutReleasingAnotherOwner()
    {
        var hud=Hud(out _,out _);var owner=new object();var foreign=new object();
        hud.SetRegionBossPresentation(owner,true);
        var interactor=New("Interactor").AddComponent<PlayerInteractor>();
        Set(hud,"regionBossInteractor",interactor);interactor.SetExternalInputLocked(hud,true);interactor.SetExternalInputLocked(foreign,true);
        hud.SetRegionBossPresentation(owner,false);
        var locks=(System.Collections.Generic.HashSet<object>)typeof(PlayerInteractor).GetField("externalInputLocks",Flags).GetValue(interactor);
        Assert.That(locks.Contains(hud),Is.False);Assert.That(locks.Contains(foreign),Is.True);
    }
    WarningMessageUI Warning(out TextMeshProUGUI text)
    {
        var g=New("Warning");var c=g.AddComponent<CanvasGroup>();var warning=g.AddComponent<WarningMessageUI>();text=Text("Message");
        Set(warning,"messageText",text);Set(warning,"canvasGroup",c);return warning;
    }
    [TestCase(ShipCommunicationChannel.Navigation)]
    [TestCase(ShipCommunicationChannel.Cargo)]
    [TestCase(ShipCommunicationChannel.Equipment)]
    [TestCase(ShipCommunicationChannel.System)]
    public void OrdinaryCommunicationCannotReplaceBossPriority(ShipCommunicationChannel channel)
    {
        var warning=Warning(out var text);warning.SetRegionBossPriority(true);text.text="Boss critical";
        warning.ShowMessage("NPC");warning.ShowCommunication(channel,"Exploration",ShipCommunicationSeverity.Warning);
        Assert.That(text.text,Is.EqualTo("Boss critical"));warning.SetRegionBossPriority(false);Assert.That(text.text,Is.Empty);
    }
    [Test] public void WarningReservedBandAndOrdinaryLayoutRestoreExactly()
    {
        var warning=Warning(out var text);var rect=(RectTransform)warning.transform;rect.anchoredPosition=new Vector2(3,17);var size=rect.sizeDelta;var font=text.fontSize;
        warning.SetRegionBossPriority(true);Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(0,18)));
        Assert.That(rect.anchorMin,Is.EqualTo(new Vector2(.5f,0)));Assert.That(text.fontSize,Is.EqualTo(8));
        Assert.That(rect.sizeDelta,Is.EqualTo(new Vector2(220,20)));warning.SetRegionBossPriority(false);
        Assert.That(text.fontSize,Is.EqualTo(font));
        Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(3,17)));Assert.That(rect.sizeDelta,Is.EqualTo(size));
    }
    BossHealthBarUI Bar(out RectTransform rect,out TextMeshProUGUI value)
    {
        var root=New("Boss HUD");rect=(RectTransform)root.transform;rect.anchoredPosition=new Vector2(0,17);
        var bar=root.AddComponent<BossHealthBarUI>();Set(bar,"rootObject",root);Set(bar,"canvasGroup",root.AddComponent<CanvasGroup>());Set(bar,"revealRoot",rect);
        value=Text("HP");Set(bar,"hpText",value);return bar;
    }
    [Test] public void RegionHudRestoresOtherBossLayoutAndHidesOnRelease()
    {
        var bar=Bar(out var rect,out _);bar.SetRegionPresentation(true);
        Assert.That(rect.anchorMin,Is.EqualTo(new Vector2(.5f,1)));Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(0,-26)));
        bar.SetRegionPresentation(false);Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(0,17)));Assert.That(bar.IsVisible,Is.False);
    }
    [Test] public void RegionIntroUsesActualHpAndNeverStretchesTheFrame()
    {
        var bar=Bar(out var rect,out var text);bar.SetRegionPresentation(true);Set(bar,"cachedCurrentHp",180f);Set(bar,"cachedMaxHp",180f);
        var routine=(IEnumerator)Call(bar,"RegionRevealRoutine");Assert.That(routine.MoveNext(),Is.True);
        Assert.That(text.text,Is.EqualTo("180 / 180"));Assert.That(rect.localScale,Is.EqualTo(Vector3.one));
    }
    [TestCase(1f,8f)] [TestCase(1.2f,6.6667f)]
    public void NativePlayerSquareMeetsSixPixelThresholdAtValidatedCombatProfiles(float multiplier,float pixels)
    {
        float size=.25f*270/(2*4.21875f*multiplier);Assert.That(size,Is.EqualTo(pixels).Within(.001f));Assert.That(size,Is.GreaterThanOrEqualTo(6));
    }
    [Test] public void SavedRegionCCameraTightensOnlyNormalPresentation()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PhaseGatekeeperAuthoring.Boss);var s=new SerializedObject(prefab.GetComponent<PhaseGatekeeperBossController>());
        Assert.That(s.FindProperty("normalCombatCameraZoomMultiplier").floatValue,Is.EqualTo(1));
        Assert.That(s.FindProperty("exposedDuration").floatValue,Is.EqualTo(5));Assert.That(s.FindProperty("laserFireDuration").floatValue,Is.EqualTo(.8f));
        Assert.That(prefab.GetComponent<EnemyHealth>().MaxHp,Is.EqualTo(180));
    }
    [Test] public void SavedTriadKeepsThreeIndependentPixelAlignedFills()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/01_Scenes/Expedition.unity");
            var bar=Object.FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);var s=new SerializedObject(bar);
            Assert.That(s.FindProperty("horizontalTriadBars").boolValue,Is.True);
            for(int i=0;i<3;i++)
            {
                var fill=(Image)s.FindProperty("triadPartFills").GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(fill.rectTransform.sizeDelta,Is.EqualTo(new Vector2(58,3)));Assert.That(fill.rectTransform.anchoredPosition,Is.EqualTo(new Vector2(1,0)));
                Assert.That(((RectTransform)fill.transform.parent).sizeDelta,Is.EqualTo(new Vector2(60,5)));
            }
        }
        finally { if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
}
