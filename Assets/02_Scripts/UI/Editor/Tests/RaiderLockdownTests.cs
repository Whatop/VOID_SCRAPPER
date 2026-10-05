using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RaiderLockdownTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> owned = new List<GameObject>();
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string f, params object[] args) => o.GetType().GetMethod(f, Flags).Invoke(o, args);
    GameObject New(string name) { var g = new GameObject(name); owned.Add(g); return g; }
    [TearDown] public void Cleanup() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]); owned.Clear(); }
    RaiderLockdownBoundaryPresentation View(out BossArenaLaserWall wall)
    {
        wall = New("Lockdown test edge").AddComponent<BossArenaLaserWall>();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/02_Scripts/Resources/VFX/M_SpriteWhiteFlash.mat");
        wall.Initialize(Vector2.zero, Vector2.right, 16.8f, .32f, true, 4, .5f, material, Color.red, "Default", 30, "Default");
        var view = wall.gameObject.AddComponent<RaiderLockdownBoundaryPresentation>();
        view.Configure(wall, 16.8f, material, "Default", 30); return view;
    }
    [TestCase(0f)] [TestCase(.25f)] [TestCase(.5f)] [TestCase(1f)]
    public void FormationGrowsFromEdgesWithoutMovingOrResizingCollision(float progress)
    {
        var view = View(out var wall); var collider = wall.GetComponent<BoxCollider2D>();
        var bounds = collider.bounds; view.SampleFormation(progress);
        Assert.That(collider.enabled, Is.True, "presentation never owns collision");
        Assert.That(collider.bounds, Is.EqualTo(bounds)); Assert.That(collider.isTrigger, Is.False);
        var line = wall.GetOrCreateLineRenderer();
        Assert.That(Vector3.Distance(line.GetPosition(0), line.GetPosition(1)), Is.EqualTo(16.8f * progress).Within(.001f));
        Assert.That(line.widthMultiplier, Is.EqualTo(.045f));
        Assert.That(view.LitNodeCount, progress == 0 ? Is.EqualTo(0) : Is.GreaterThan(0));
        Assert.That(view.transform.Find("LockdownEnergyNodes").GetComponentsInChildren<Collider2D>(), Is.Empty);
    }
    [Test] public void AllNodesLightAndReleasePowersThemOffBeforeLinesEnd()
    {
        var view = View(out var wall); view.SampleFormation(1);
        Assert.That(view.LitNodeCount, Is.EqualTo(23));
        view.SampleRelease(.72f); Assert.That(view.LitNodeCount, Is.Zero); Assert.That(wall.GetOrCreateLineRenderer().enabled, Is.True);
        view.SampleRelease(1); Assert.That(wall.GetOrCreateLineRenderer().enabled, Is.False);
        Assert.That(wall.GetComponent<BoxCollider2D>().enabled, Is.True, "only intro authority changes collision");
    }
    [Test] public void ReconfigureReusesChildrenAndHasNoPerEdgeUpdate()
    {
        var view = View(out var wall); view.SampleFormation(1); int children = view.transform.childCount;
        view.Configure(wall, 16.8f, wall.GetOrCreateLineRenderer().sharedMaterial, "Default", 30);
        Assert.That(view.transform.childCount, Is.EqualTo(children)); Assert.That(view.LitNodeCount, Is.Zero);
        Assert.That(typeof(RaiderLockdownBoundaryPresentation).GetMethod("Update", Flags), Is.Null);
    }
    [TestCase(true)] [TestCase(false)]
    public void OnlyCommanderGetsTheNewBoundaryAndOriginalWallRulesRemain(bool commander)
    {
        var root = New("Intro fixture"); root.SetActive(false); var intro = root.AddComponent<CoreBossIntroSequence>();
        Set(intro, "raiderCommanderIntro", commander); Call(intro, "ActivateRaiderBarrier", (object)Vector3.zero);
        var walls = (List<GameObject>)Get(intro, "spawnedWallObjects"); Assert.That(walls.Count, Is.EqualTo(4));
        foreach (var g in walls)
        {
            owned.Add(g); var wall = g.GetComponent<BossArenaLaserWall>();
            Assert.That(g.GetComponent<RaiderLockdownBoundaryPresentation>() != null, Is.EqualTo(commander));
            Assert.That(g.GetComponent<RaiderArenaBoundaryPresentation>() != null, Is.EqualTo(!commander));
            Assert.That(wall.AllowsProjectileRicochet, Is.False);
            Assert.That(wall.GetComponent<BoxCollider2D>().enabled, Is.EqualTo(!commander));
        }
        Assert.That(((List<RaiderBarricadeCarrier>)Get(intro, "spawnedRaiderCarriers")).Count, Is.Zero);
        walls.Clear(); // Fixture owns immediate EditMode cleanup.
    }
    [Test] public void VisibleReleaseDisablesGameplayBeforeItsFirstYield()
    {
        var intro = New("Release fixture").AddComponent<CoreBossIntroSequence>();
        var view = View(out var wall); view.SampleFormation(1);
        ((List<BossArenaLaserWall>)Get(intro, "raiderLockdownWalls")).Add(wall);
        ((List<RaiderLockdownBoundaryPresentation>)Get(intro, "raiderLockdownViews")).Add(view);
        var release = (IEnumerator)Call(intro, "ReleaseRaiderLockdownRoutine");
        Assert.That(release.MoveNext(), Is.True);
        Assert.That(intro.RaiderLockdownReleasing, Is.True);
        Assert.That(wall.GetComponent<BoxCollider2D>().enabled, Is.False);
        Assert.That(wall.FormationProgress, Is.Zero); Assert.That(wall.GetOrCreateLineRenderer().enabled, Is.True);
    }
}
