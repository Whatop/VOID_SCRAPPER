using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorIntroTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] args) => o.GetType().GetMethod(m, Flags).Invoke(o, args);
    GameObject New(string name) { var g = new GameObject(name); g.SetActive(false); objects.Add(g); return g; }
    [TearDown] public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }
    BossPatternController Boss(out GungeonStyleCamera2D camera, out CameraZoomController2D zoom)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss), New("Inactive parent").transform);
        objects.Add(root);
        var boss = root.GetComponent<BossPatternController>();
        var target = New("Player"); target.transform.position = Vector3.down * 6;
        Set(boss, "player", target.transform);
        var cam = New("Camera"); var native = cam.AddComponent<Camera>(); native.orthographic = true; native.orthographicSize = 4.21875f;
        zoom = cam.AddComponent<CameraZoomController2D>(); zoom.Bind(native);
        camera = cam.AddComponent<GungeonStyleCamera2D>();
        Set(camera, "cameraZoomController", zoom); Set(camera, "mainCamera", native);
        Set(boss, "phase2GungeonCamera", camera);
        return boss;
    }
    CoreBossIntroSequence Intro(BossPatternController boss, GungeonStyleCamera2D camera, CameraZoomController2D zoom)
    {
        var intro = New("Intro").AddComponent<CoreBossIntroSequence>(); intro.gameObject.SetActive(true);
        Set(intro, "spawnedBoss", boss.gameObject); Set(intro, "sectorIntroPresentation", true); Set(intro, "isPlaying", true);
        Set(intro, "gungeonCamera", camera); Set(intro, "cameraZoomController", zoom);
        return intro;
    }
    [Test] public void HandoffAcquiresExistingOwnerOnceWithoutStartingCombat()
    {
        var boss = Boss(out var camera, out var zoom);
        boss.PrepareSectorIntroCameraHandoff();
        Assert.That(Get(camera, "gameplayFramingOwner"), Is.SameAs(boss));
        Assert.That(Get(camera, "targetGameplayFramingOffset"), Is.EqualTo(Vector2.up * 3.7f));
        ((Transform)Get(boss, "player")).position = Vector3.right * 6;
        boss.PrepareSectorIntroCameraHandoff();
        Assert.That(Get(camera, "targetGameplayFramingOffset"), Is.EqualTo(Vector2.up * 3.7f), "second prepare is a no-op");
        Assert.That(zoom.GameplayFramingMultiplier, Is.EqualTo(5.5f / 4.21875f).Within(.001f));
        Assert.That(boss.isActiveAndEnabled, Is.False);
        Assert.That(Get(boss, "patternRoutine"), Is.Null);
        Assert.That(Get(boss, "phase2ShieldCombatRoutine"), Is.Null);
        boss.CancelSectorIntroPresentation();
    }
    [Test] public void HandoffCannotStealForeignCameraOwnership()
    {
        var boss = Boss(out var camera, out _); var foreign = new object();
        camera.AcquireGameplayFramingProfile(foreign, Vector2.left, 1, 1, true);
        boss.PrepareSectorIntroCameraHandoff();
        Assert.That(Get(camera, "gameplayFramingOwner"), Is.SameAs(foreign));
        boss.CancelSectorIntroPresentation();
        Assert.That(Get(camera, "gameplayFramingOwner"), Is.SameAs(foreign));
    }
    [Test] public void NonSectorPresentationDoesNotAcquireOrReleaseCamera()
    {
        var boss = Boss(out var camera, out _); Set(boss, "useSectorControlSequence", false);
        boss.PrepareSectorIntroCameraHandoff(); Assert.That(Get(camera, "gameplayFramingOwner"), Is.Null);
        var foreign = new object(); camera.AcquireGameplayFramingProfile(foreign, Vector2.left, 1, 1, true);
        boss.CancelSectorIntroPresentation(); Assert.That(Get(camera, "gameplayFramingOwner"), Is.SameAs(foreign));
    }
    [TestCase(true)] [TestCase(false)]
    public void OnlySectorRevealReleasesOverviewHold(bool sector)
    {
        var boss = Boss(out var camera, out var zoom); var intro = Intro(boss, camera, zoom);
        Set(intro, "sectorIntroPresentation", sector);
        zoom.BeginCinematicZoomHold(3.5f); Set(intro, "introWideZoomHoldActive", true);
        var routine = (IEnumerator)Call(intro, "BlendBossRevealFocusRoutine", Vector3.zero, Vector3.up);
        routine.MoveNext(); Assert.That(zoom.IsCinematicZoomHeld, Is.EqualTo(!sector));
        Assert.That(zoom.CurrentZoomMultiplier, Is.EqualTo(3.5f).Within(.001f), "release itself must not snap zoom");
        Set(intro, "isPlaying", false);
    }
    [Test] public void HudRevealIsOnceAndDoesNotRunAfterCancellation()
    {
        var boss = Boss(out var camera, out var zoom); var intro = Intro(boss, camera, zoom); int count = 0;
        Action reveal = () => count++;
        Call(intro, "RevealSectorBossHud", reveal); Call(intro, "RevealSectorBossHud", reveal);
        Assert.That(count, Is.EqualTo(1));
        Set(intro, "sectorBossHudRevealed", false); Set(intro, "introCancellationRequested", true);
        Call(intro, "RevealSectorBossHud", reveal); Assert.That(count, Is.EqualTo(1));
    }
    [TestCase("HandleTrackedBossDied")] [TestCase("HandleRunEnded")] [TestCase("OnDisable")] [TestCase("HandleSectorIntroPlayerDied")]
    public void IntroInterruptionReleasesCameraAndDoesNotEnableScheduler(string entry)
    {
        var boss = Boss(out var camera, out var zoom); var intro = Intro(boss, camera, zoom);
        boss.PrepareSectorIntroCameraHandoff(); zoom.BeginCinematicZoomHold(3.5f); Set(intro, "introWideZoomHoldActive", true);
        camera.SetCinematicFocus(Vector3.up, true); Call(intro, "AcquireCameraInputOffsetLock");
        if (entry == "HandleTrackedBossDied") Call(intro, entry, boss.GetComponent<EnemyHealth>());
        else if (entry == "HandleRunEnded") Call(intro, entry, new object[] { null });
        else Call(intro, entry);
        Assert.That(intro.IsPlaying, Is.False); Assert.That(camera.IsCinematicFocusActive, Is.False);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.False); Assert.That(zoom.IsCinematicZoomHeld, Is.False);
        Assert.That(Get(camera, "gameplayFramingOwner"), Is.Null); Assert.That(zoom.CurrentZoomMultiplier, Is.EqualTo(1));
        Assert.That(boss.isActiveAndEnabled, Is.False); Assert.That(Get(boss, "patternRoutine"), Is.Null);
        Assert.That(Get(intro, "cinematicHudModeHeld"), Is.False); Assert.That(Get(intro, "coreActivationTitlePresented"), Is.False);
    }
    [Test] public void PlayerDeathCancelsBeforeRunEndedAndRemovesItsBinding()
    {
        var boss = Boss(out var camera, out var zoom); var intro = Intro(boss, camera, zoom);
        var player = New("Intro player health").AddComponent<PlayerHealth>(); player.gameObject.SetActive(true);
        Call(intro, "BindSectorIntroPlayerDeath", player.gameObject);
        Assert.That(Get(intro, "observedSectorIntroPlayer"), Is.SameAs(player));
        ((Action)Get(player, "Died"))();
        Assert.That(intro.IsPlaying, Is.False);
        Assert.That(Get(intro, "observedSectorIntroPlayer"), Is.Null);
        Assert.That(Get(player, "Died"), Is.Null);
        Assert.That(Get(boss, "patternRoutine"), Is.Null);
    }
    [Test] public void SuccessfulHandoffRemovesOnlyIntroDeathSubscription()
    {
        var boss = Boss(out var camera, out var zoom); var intro = Intro(boss, camera, zoom);
        var player = New("Intro player health").AddComponent<PlayerHealth>(); player.gameObject.SetActive(true); int foreignCalls = 0;
        player.Died += () => foreignCalls++;
        Call(intro, "BindSectorIntroPlayerDeath", player.gameObject);
        Call(intro, "UnbindSectorIntroPlayerDeath");
        ((Action)Get(player, "Died"))();
        Assert.That(foreignCalls, Is.EqualTo(1)); Assert.That(intro.IsPlaying, Is.True);
        Assert.That(Get(intro, "observedSectorIntroPlayer"), Is.Null);
    }
    [Test] public void FirstCompactFrameBindsCurrentHpWithoutStretch()
    {
        var boss = Boss(out _, out _); var health = boss.GetComponent<EnemyHealth>();
        Set(health, "currentHp", 73f); Set(health, "maxHp", 140f);
        var root = new GameObject("HP", typeof(RectTransform), typeof(CanvasGroup)); root.SetActive(false); objects.Add(root);
        var bar = root.AddComponent<BossHealthBarUI>(); var rect = (RectTransform)root.transform;
        var textObject = new GameObject("Value", typeof(RectTransform)); textObject.SetActive(false); objects.Add(textObject);
        var value = textObject.AddComponent<TextMeshProUGUI>();
        Set(bar, "rootObject", root); Set(bar, "canvasGroup", root.GetComponent<CanvasGroup>()); Set(bar, "revealRoot", rect); Set(bar, "hpText", value);
        Call(bar, "BindBoss", health, "Sector Administrator");
        var routine = (IEnumerator)Call(bar, "RegionRevealRoutine"); routine.MoveNext();
        Assert.That(value.text, Is.EqualTo("73 / 140")); Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Get(bar, "cachedCurrentHp"), Is.EqualTo(73f));
    }
}
