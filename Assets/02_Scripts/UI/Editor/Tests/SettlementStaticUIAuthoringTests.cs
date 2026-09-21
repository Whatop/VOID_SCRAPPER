using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SettlementStaticUIAuthoringTests
{
    private Scene scene, userScene;
    private bool userDirty;
    private int[] userRoots;
    private GameObject fixture;
    private SettlementHUD hud;
    private SettlementUIController ui;
    private EscSettingsMenuController settings;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userDirty = userScene.isDirty;
        userRoots = userScene.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray();
        scene = AuthoredRuntimeFixture.Open("Settlement");
        fixture = AuthoredRuntimeFixture.Group(scene).gameObject;
        hud = AuthoredRuntimeFixture.Single<SettlementHUD>(scene);
        ui = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        settings = AuthoredRuntimeFixture.Single<EscSettingsMenuController>(scene);
    }

    [TearDown]
    public void TearDown()
    {
        if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene);
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userDirty));
        Assert.That(userScene.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray(), Is.EqualTo(userRoots), AuthoredRuntimeFixture.RootsDescription(userScene));
    }

    [Test]
    public void HangarIndicatorSelectionAndTweenCleanup_PreserveAuthoredBaselines()
    {
        Hangar();
        Image indicator = Read<Image>(hud, "shipIndicatorImages.Array.data[0]");
        indicator.transform.localScale = new Vector3(.6f, .8f, 1);
        indicator.color = new Color(.2f, .3f, .4f, .5f);
        Vector3 baseline = indicator.transform.localScale;
        Vector2 ghostBaseline = Read<Image>(hud, "curseGhostImage").rectTransform.anchoredPosition;
        for (int i = 0; i < 4; i++)
        {
            hud.SetShipPreviewState(null, 0, 3);
            Assert.That(indicator.transform.localScale, Is.EqualTo(baseline * 1.25f));
            hud.SetShipPreviewState(null, 1, 3);
            Assert.That(indicator.transform.localScale, Is.EqualTo(baseline));
        }
        Image ghost = Read<Image>(hud, "curseGhostImage");
        ghost.rectTransform.anchoredPosition = new Vector2(30, 20);
        // Baselines are captured once by the runtime preview, not from a subsequent animation frame.
        Call(hud, "StopCursePreviewTween");
        Assert.That(ghost.rectTransform.anchoredPosition, Is.EqualTo(ghostBaseline));
    }

    [Test]
    public void MissingSettingsBindings_LogOnceAndNeverAcquireInputOrPause()
    {
        var bindings = new SerializedObject(settings);
        bindings.FindProperty("sharedOptionsModal").objectReferenceValue = null;
        bindings.ApplyModifiedPropertiesWithoutUndo();
        int[] ids = Ids();
        bool paused = GameplayPauseManager.IsPaused;
        int openings = 0;
        settings.Opening += () => openings++;
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("EscSettingsMenuController.sharedOptionsModal"));
        settings.Open(); settings.Open();
        Assert.That(settings.IsOpen, Is.False);
        Assert.That(openings, Is.Zero);
        Assert.That(GameplayPauseManager.IsPaused, Is.EqualTo(paused));
        Assert.That(Ids(), Is.EqualTo(ids));
    }

    [Test]
    public void SharedAuthoredButtons_RepeatedBindingDispatchesOneOwnedEvent()
    {
        Settings();
        SharedOptionsMenuUI menu = Read<SharedOptionsMenuUI>(settings, "sharedOptionsMenu");
        int calls = 0;
        menu.BackRequested += () => calls++;
        // PreviewScene activation supports OnEnable without a fabricated component array.
        // The shared event binding is tested directly; settings preferences are never initialized.
        Read<GameObject>(settings, "sharedOptionsModal").SetActive(true);
        menu.gameObject.SetActive(true);
        fixture.SetActive(true);
        for (int i = 0; i < 3; i++) Call(menu, "OnEnable");
        menu.BackButton.onClick.Invoke();
        Assert.That(calls, Is.EqualTo(1));
        Call(menu, "OnDisable");
        menu.BackButton.onClick.Invoke();
        Assert.That(calls, Is.EqualTo(1));
        fixture.SetActive(false);
    }

    [Test]
    public void RetiredOwnersHaveNoStaticBuilders_SharedOptionsBuilderRemains()
    {
        foreach (string method in new[] { "BuildCursePreviewLayers", "CreatePreviewLayer", "ConfigureTextPresentation", "BuildResourceStrip" })
            Assert.That(typeof(SettlementHUD).GetMethod(method, Private), Is.Null, method);
        foreach (string method in new[] { "ConfigureSettlementPresentation", "RebuildGeneratedNodeButtons", "EnsureGeneratedNodeButtons", "LayoutCostIcons" })
            Assert.That(typeof(ShipTraitTreePanel).GetMethod(method, Private), Is.Null, method);
        Assert.That(typeof(EscSettingsMenuController).GetMethod("BuildSharedOptionsMenu", Private), Is.Null);
        Assert.That(typeof(SharedOptionsMenuUI).GetMethod("Build", Private), Is.Not.Null);
        Assert.That(typeof(ShipTraitTreePanel).GetMethod("EnsureAuthoredNodes", Private), Is.Not.Null);
    }

    private void Hangar() => Assert.That(Read<Image>(hud, "curseBaseImage"), Is.Not.Null);
    private void Settings() => Assert.That(settings, Is.Not.Null);

    private int[] Ids() => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject.GetInstanceID()).OrderBy(x => x).ToArray();
    private static T Read<T>(Object owner, string field) where T : Object => new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

    private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
}
