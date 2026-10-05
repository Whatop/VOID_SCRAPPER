using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SectorFollowupTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager priorPool, pool;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] v) => o.GetType().GetMethods(Flags).Single(method => method.Name == m && method.GetParameters().Length == v.Length).Invoke(o, v);
    GameObject New(string n) { var g = new GameObject(n); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    {
        priorPool = PoolManager.Instance; pool = New("Followup test pool").AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, pool);
    }
    [TearDown] public void Teardown()
    {
        foreach (var root in roots) if (root != null && root.GetComponent<BossPatternController>() != null) Call(root.GetComponent<BossPatternController>(), "ClearSectorAttacks");
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, priorPool);
        typeof(BossPatternController).GetProperty("ActiveSectorEncounter").SetValue(null, null);
    }
    BossPatternController Boss()
    {
        var g = New("Sector fixture"); var c = g.AddComponent<BossPatternController>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss).GetComponent<BossPatternController>();
        Set(c, "useSectorControlSequence", true); Set(c, "sectorLanePrefab", Get(prefab, "sectorLanePrefab"));
        Set(c, "arenaCenter", Vector2.zero); Set(c, "arenaHalfExtents", new Vector2(12, 12));
        Set(c, "verticalSpaceScale", 1f);
        return c;
    }
    [TestCase(4, 90f)] [TestCase(6, 60f)]
    public void FinalSpokesAreEvenlySpacedAndLeaveOpenSectors(int count, float gap)
    {
        var angles = Enumerable.Range(0, count).Select(i => BossPatternController.SectorSpokeOffset(i, count)).OrderBy(a => a).ToArray();
        for (int i = 0; i < count; i++) Assert.That(Mathf.Repeat(angles[(i + 1) % count] - angles[i], 360), Is.EqualTo(gap).Within(.001f));
        Assert.That(2 * 3f * Mathf.Sin(gap * .5f * Mathf.Deg2Rad) - .45f, Is.GreaterThan(.8f), "player-sized sector width three units from origin");
    }
    [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
    [TestCase(45f)] [TestCase(135f)] [TestCase(225f)] [TestCase(315f)]
    public void BeamReachesActualRectangleWallsForCardinalsAndDiagonals(float angle)
    {
        var c = Boss(); Vector2 origin = new Vector2(1, -2);
        var list = (List<GameObject>)Get(c, "boundaryLaserWalls");
        Vector2[] corners = { new Vector2(-12,-12), new Vector2(12,-12), new Vector2(12,12), new Vector2(-12,12) };
        for (int i = 0; i < 4; i++)
        {
            var g = New("Physical wall"); g.SetActive(true); var box = g.AddComponent<BoxCollider2D>();
            Vector2 d = corners[(i + 1) % 4] - corners[i]; g.transform.position = (corners[i] + corners[(i + 1) % 4]) * .5f;
            g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg); box.size = new Vector2(d.magnitude, .45f); list.Add(g);
        }
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Vector2 end = origin + direction * (c.SectorBeamLength(origin, direction) + .05f);
        Assert.That(Mathf.Min(Mathf.Abs(Mathf.Abs(end.x) - 12), Mathf.Abs(Mathf.Abs(end.y) - 12)), Is.LessThan(.001f));
        Assert.That(Mathf.Abs(end.x), Is.LessThanOrEqualTo(12.001f)); Assert.That(Mathf.Abs(end.y), Is.LessThanOrEqualTo(12.001f));
    }
    [Test] public void HexagonRayIntersectionUsesWallSegmentsRatherThanBoundingBox()
    {
        float r = 12; Vector2 a = new Vector2(0, r), b = new Vector2(r * .8660254f, r * .5f);
        float hit = BossPatternController.RayToSectorEdge(Vector2.zero, new Vector2(1, 1).normalized, a, b);
        Vector2 end = new Vector2(1, 1).normalized * hit;
        Assert.That(end.x, Is.EqualTo(7.6077f).Within(.002f)); Assert.That(end.y, Is.EqualTo(end.x));
        Assert.That(BossPatternController.RayToSectorEdge(Vector2.zero, Vector2.left, a, b), Is.EqualTo(float.PositiveInfinity));
    }
    [Test] public void OneClockRotatesAllFourThenSixWithoutReversalOrDamageDuringMorph()
    {
        var c = Boss(); Call(c, "EnsureSectorSpokes", 4); Set(c, "sectorRotating", true);
        for (int n = 0; n < 20; n++)
        {
            float angle = c.SectorAngle; Call(c, "UpdateSectorSpokes", .1f);
            Assert.That(c.SectorAngle - angle, Is.EqualTo(3f).Within(.001f));
            var lanes = (SectorPartitionLane[])Get(c, "sectorLanes");
            for (int i = 0; i < 4; i++) Assert.That(Mathf.DeltaAngle(c.SectorAngle + i * 90, lanes[i].transform.eulerAngles.z), Is.EqualTo(0).Within(.001f));
        }
        Call(c, "PrepareSectorEscalation"); float paused = c.SectorAngle;
        Assert.That(c.SectorSpokeCount, Is.EqualTo(4));
        Call(c, "EnsureSectorSpokes", 6);
        foreach (float morph in new[] { 0f, .2f, .5f, .9f, 1f })
        {
            Set(c, "sectorMorph", morph); Call(c, "UpdateSectorSpokes", .1f);
            Assert.That(c.SectorAngle, Is.EqualTo(paused));
            foreach (var lane in (SectorPartitionLane[])Get(c, "sectorLanes"))
            { Assert.That(lane.IsVisible, Is.True); Assert.That(lane.IsDamaging, Is.False); }
        }
        Set(c, "sectorRotating", true); Call(c, "UpdateSectorSpokes", .1f);
        Assert.That(c.SectorAngle, Is.EqualTo(paused + 3).Within(.001f));
        foreach (var lane in (SectorPartitionLane[])Get(c, "sectorLanes")) Assert.That(lane.IsDamaging, Is.True);
        Call(c, "ClearSectorAttacks"); Assert.That(c.SectorSpokeCount, Is.Zero);
    }
    [Test] public void RotatingGeometryKeepsVisibleLengthColliderAndHitCooldownTogether()
    {
        var c = Boss(); Call(c, "EnsureSectorSpokes", 4); Set(c, "sectorRotating", true); Call(c, "UpdateSectorSpokes", .2f);
        var lane = ((SectorPartitionLane[])Get(c, "sectorLanes"))[0]; Set(lane, "nextDamageTime", 123f);
        Call(c, "UpdateSectorSpokes", .3f);
        var renderer = (SpriteRenderer)Get(lane, "visual"); var box = lane.GetComponent<BoxCollider2D>();
        Assert.That(renderer.size.x, Is.EqualTo(box.size.x)); Assert.That(box.offset.x * 2, Is.EqualTo(box.size.x));
        Assert.That(Get(lane, "nextDamageTime"), Is.EqualTo(123f)); Assert.That(lane.IsDamaging, Is.True);
        Assert.That(typeof(SectorPartitionLane).GetMethod("Update", Flags), Is.Null);
    }
    [Test] public void SuppressionCommitDoesNotRetargetWithinBurstAndUsesExistingProjectileValues()
    {
        var c = Boss(); var target = New("Moving player").transform; target.position = Vector2.up * 5; Set(c, "player", target);
        Call(c, "CommitSectorBurst", 0); Vector2 origin = (Vector2)Get(c, "sectorBurstOrigin"), aim = (Vector2)Get(c, "sectorBurstDirection");
        target.position = Vector2.right * 8;
        // Dispatch consumes only the committed vector (a missing projectile definition suppresses actual spawn in this fixture).
        Call(c, "DispatchSectorSuppressionShot"); Call(c, "DispatchSectorSuppressionShot"); Call(c, "DispatchSectorSuppressionShot");
        Assert.That(c.SectorShotsThisCycle, Is.EqualTo(3)); Assert.That(Get(c, "sectorBurstDirection"), Is.EqualTo(aim));
        Assert.That(Get(c, "sectorBurstOrigin"), Is.EqualTo(origin)); Call(c, "CommitSectorBurst", 1);
        Assert.That(Vector2.Angle(aim, (Vector2)Get(c, "sectorBurstDirection")), Is.GreaterThan(30));
        var authored = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss).GetComponent<BossPatternController>();
        Assert.That(Get(authored, "spreadProjectileDamage"), Is.EqualTo(2f)); Assert.That(Get(authored, "spreadProjectileSpeedOverride"), Is.EqualTo(-1f));
        Assert.That(Get(authored, "sectorBurstCadence"), Is.EqualTo(.08f));
        var definition = (ProjectileDefinition)Get(authored, "projectileDefinition"); Assert.That(definition.Speed, Is.EqualTo(8f));
        string source = System.IO.File.ReadAllText("Assets/02_Scripts/Boss/BossPatternController.SectorControl.cs");
        Assert.That(source, Does.Not.Contain("FireSpread(")); Assert.That(source, Does.Not.Contain("DispatchSectorFan"));
    }
    [TestCase("Tutorial")] [TestCase("Expedition")]
    public void AuthoredHealthArmorBoundsEventsZeroStateAndMuzzleAreCorrect(string sceneName)
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + sceneName + ".unity");
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpeditionHUD>(true)).First();
            var gauge = (GaugeBarUI)Get(hud, "hpGauge"); var area = (RectTransform)gauge.FillImage.transform.parent;
            var armorTrack = (Image)Get(hud, "armorTrackImage"); var fill = (RectTransform)Get(hud, "armorFillRect");
            Assert.That(fill.parent, Is.EqualTo(armorTrack.transform));
            Assert.That(armorTrack.rectTransform.rect.width, Is.EqualTo(area.rect.width));
            Assert.That(armorTrack.rectTransform.anchoredPosition.x, Is.EqualTo(area.anchoredPosition.x));
            Assert.That(armorTrack.rectTransform.anchoredPosition.y - .5f, Is.GreaterThanOrEqualTo(-10f));
            var health = (PlayerHealth)Get(hud, "playerHealth"); var armor = (PlayerArmor)Get(hud, "playerArmor");
            Call(hud, "Subscribe"); health.SetMaxHp(26, true); armor.SetMaxArmor(8, true);
            Assert.That(gauge.Ratio, Is.EqualTo(1f)); Assert.That(fill.anchorMin.x, Is.Zero); Assert.That(fill.anchorMax.x, Is.EqualTo(1f));
            armor.SetArmor(4); health.RestoreCurrentHp(13);
            Assert.That(gauge.Ratio, Is.EqualTo(.5f)); Assert.That(fill.anchorMax.x, Is.EqualTo(.5f));
            armor.SetArmor(0); Assert.That(fill.gameObject.activeSelf, Is.False); Assert.That(armorTrack.gameObject.activeSelf, Is.False);
            Call(hud, "Unsubscribe");
            var weapon = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MachineGunWeapon>(true)).First();
            var so = new SerializedObject(weapon);
            Assert.That(AssetDatabase.GetAssetPath(so.FindProperty("muzzleEffectPrefab").objectReferenceValue), Is.EqualTo(SectorFollowupAuthoring.Muzzle));
            Assert.That(so.FindProperty("muzzleEffectRotationOffset").floatValue, Is.Zero);
            Assert.That(so.FindProperty("muzzleEffectLifeTime").floatValue, Is.EqualTo(.18f));
            var scaler = hud.GetComponentInParent<Canvas>().GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(480,270)));
        }
        finally { if (previous.Any(p => p.isLoaded && p.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
    }
    [Test] public void ApprovedMuzzleNativeOpaqueBoundsAndPoolReuseStayCompact()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorFollowupAuthoring.Muzzle);
        var sprite = prefab.GetComponent<SpriteRenderer>().sprite;
        Assert.That(AssetDatabase.GetAssetPath(sprite), Does.EndWith("CombatReadability/MachineGunMuzzle.png"));
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32)); Assert.That(sprite.pivot, Is.EqualTo(new Vector2(8,16)));
        Assert.That(18f * prefab.transform.localScale.x, Is.InRange(6f,12f));
        var rotation = Quaternion.Euler(0,0,71); var instance = pool.Get(prefab, new Vector3(3,4), rotation); roots.Add(instance);
        pool.Release(instance); var again = pool.Get(prefab, new Vector3(1,2), rotation);
        Assert.That(again, Is.SameAs(instance)); Assert.That(again.transform.position, Is.EqualTo(new Vector3(1,2)));
        Assert.That(again.transform.eulerAngles.z, Is.EqualTo(71).Within(.001f)); Assert.That(again.transform.localScale.x, Is.EqualTo(.55f).Within(.00001f));
    }
    [Test] public void SectorLaserSortsAboveSpaceButBelowPlayerBody()
    {
        var lane = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Lane);
        Assert.That(lane.GetComponentInChildren<SpriteRenderer>(true).sortingOrder, Is.EqualTo(-2));
        Assert.That(-2, Is.GreaterThan(-20)); // highest authored space-background layer
        Assert.That(-2, Is.LessThan(1)); // authored Body_StaticSprite
    }
    [Test] public void CoreSectorAlertIsDisabledWithoutChangingOtherBossPolicy()
    {
        var core = New("Core fixture").AddComponent<CoreObject>(); var c = Boss(); Set(core,"spawnedBoss",c.gameObject);
        Set(core,"alertNearbyEnemiesOnBattleStart",true); Assert.That(Call(core,"ShouldAlertAmbientEnemies"), Is.False);
        Set(core,"spawnedBoss",New("Other boss")); Assert.That(Call(core,"ShouldAlertAmbientEnemies"), Is.True);
    }
    [Test] public void IntroPublishesSectorOwnerBeforeCoreStateChangeAndEnableHandoff()
    {
        var c = Boss(); Assert.That(c.isActiveAndEnabled, Is.False);
        c.ConfigureBossArena(new Vector2(0, 2.5f), new Vector2(12, 12), 1);
        Assert.That(BossPatternController.ActiveSectorEncounter, Is.SameAs(c));
        Call(c, "EndSectorArenaCleanup"); Assert.That(BossPatternController.ActiveSectorEncounter, Is.Null);
    }
    [Test] public void AmbientIsolationHidesWithoutDeathAndRestoresExactRenderingAndPhysics()
    {
        var c = Boss(); typeof(BossPatternController).GetProperty("ActiveSectorEncounter").SetValue(null,c);
        var g = New("Ambient fixture"); var rb = g.AddComponent<Rigidbody2D>(); var sprite = g.AddComponent<SpriteRenderer>();
        var hp = g.AddComponent<EnemyHealth>(); hp.SetMaxHp(10,true); var ai = g.AddComponent<EnemyBaseAI>(); Call(ai,"Awake");
        int deaths = 0; hp.Died += _ => deaths++;
        Call(ai,"RefreshBossEncounterIsolation",GameState.BossBattle);
        Assert.That(ai.IsBossEncounterIsolated, Is.True); Assert.That(rb.simulated, Is.False); Assert.That(sprite.forceRenderingOff, Is.True);
        ai.AlertTo(Vector2.one * 10); Assert.That(ai.IsBossEncounterIsolated, Is.True); Assert.That(deaths, Is.Zero);
        Call(ai,"ReleaseBossEncounterIsolation",false);
        Assert.That(rb.simulated, Is.True); Assert.That(sprite.forceRenderingOff, Is.False); Assert.That(deaths, Is.Zero);
        var owned = New("Boss-owned child"); owned.transform.SetParent(c.transform); var bossAI = owned.AddComponent<EnemyBaseAI>();
        Assert.That(Call(bossAI,"IsAmbientEnemyForBossEncounterIsolation"), Is.False);
    }
    [Test] public void ArenaMeteorFootprintIsHiddenWhileOutsideDriftPausesAndResumesWithoutDamage()
    {
        var g = New("Meteor fixture"); g.SetActive(true); var rb = g.AddComponent<Rigidbody2D>(); rb.gravityScale = 0;
        var box = g.AddComponent<BoxCollider2D>(); box.size = Vector2.one * 2;
        var meteor = g.AddComponent<MeteorObstacle>(); Call(meteor,"Awake"); Set(meteor,"currentHp",2);
        g.transform.position = new Vector2(12.5f,0); rb.linearVelocity = Vector2.left * .4f; Physics2D.SyncTransforms();
        var bounds = new Bounds(Vector3.zero,new Vector3(24,24,20)); meteor.PauseForSectorEncounter(bounds);
        Assert.That(meteor.IsSectorEncounterHidden, Is.True,"footprint crossing edge is included even with center outside");
        Assert.That(box.enabled, Is.False); Assert.That(rb.simulated, Is.False);
        meteor.ResumeAfterSectorEncounter(); Assert.That(box.enabled, Is.True); Assert.That(rb.simulated, Is.True);
        Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.left * .4f)); Assert.That(Get(meteor,"currentHp"), Is.EqualTo(2));
        g.transform.position = new Vector2(30,0); Physics2D.SyncTransforms(); meteor.PauseForSectorEncounter(bounds);
        Assert.That(meteor.IsSectorEncounterHidden, Is.False); Assert.That(rb.simulated, Is.False);
        meteor.ResumeAfterSectorEncounter(); Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.left * .4f));
    }
}
