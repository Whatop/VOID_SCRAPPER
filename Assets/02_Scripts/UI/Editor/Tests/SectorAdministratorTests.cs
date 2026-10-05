using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorAdministratorTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> roots = new List<GameObject>();
    PoolManager previousPool, pool;
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o, v);
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Flags).Invoke(o, a);
    GameObject New(string name) { var g = new GameObject(name); g.SetActive(false); roots.Add(g); return g; }
    [SetUp] public void Setup()
    {
        previousPool = PoolManager.Instance; pool = New("Sector test pool").AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, pool);
    }
    [TearDown] public void Teardown()
    {
        for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
        roots.Clear(); typeof(PoolManager).GetProperty("Instance").SetValue(null, previousPool);
    }
    SectorPartitionLane Lane()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Lane);
        Assert.That(prefab, Is.Not.Null);
        var g = pool.Get(prefab, Vector3.zero, Quaternion.identity); roots.Add(g);
        var lane = g.GetComponent<SectorPartitionLane>();
        lane.Configure(new Vector2(-4, 0), new Vector2(4, 0), .45f, 4, .75f);
        return lane;
    }
    [Test] public void WarningHasNoDamageColliderAndActiveFramesShareCollisionLifetime()
    {
        var lane = Lane();
        foreach (float t in new[] { 0f, .2f, .5f, .99f, 1f })
        { lane.Warn(t); Assert.That(lane.IsVisible, Is.True); Assert.That(lane.IsDamaging, Is.False); }
        foreach (float t in new[] { 0f, .05f, .14f, .7f, 2.49f, 2.99f })
        { lane.Activate(t); Assert.That(lane.IsVisible, Is.True); Assert.That(lane.IsDamaging, Is.True); }
        lane.Clear(); Assert.That(lane.IsVisible, Is.False); Assert.That(lane.IsDamaging, Is.False);
    }
    [Test] public void PoolReuseCannotRetainDamageFromPreviousDispatch()
    {
        var lane = Lane(); lane.Activate(.5f); pool.Release(lane.gameObject);
        // EditMode does not dispatch MonoBehaviour lifecycle messages.
        Call(lane, "OnDisable");
        Assert.That(lane.IsDamaging, Is.False); Assert.That(lane.IsVisible, Is.False);
        lane.Configure(Vector2.zero, Vector2.up * 6, .45f, 4, .75f);
        Assert.That(lane.IsDamaging, Is.False); Assert.That(lane.IsVisible, Is.True);
        var box = lane.GetComponent<BoxCollider2D>();
        Assert.That(box.size.x, Is.EqualTo(6)); Assert.That(box.size.y, Is.EqualTo(.45f));
    }
    [TestCase("OnDisable")] [TestCase("StopCombatForDeathPresentation")]
    [TestCase("StopCurrentBossPatternForTransition")] [TestCase("CancelCombat")]
    public void DeathDisableTransitionAndRunCancellationReleaseAllOwnedLanes(string entry)
    {
        var g = New("Sector boss"); var boss = g.AddComponent<BossPatternController>();
        var lanes = (SectorPartitionLane[])Get(boss, "sectorLanes");
        lanes[0] = Lane(); lanes[1] = Lane(); var aim = Lane(); Set(boss, "precisionWarning", aim);
        var first = lanes[0]; var second = lanes[1]; first.Activate(0); second.Activate(0);
        Call(boss, entry);
        foreach (var lane in new[] { first, second, aim })
        { Assert.That(lane.gameObject.activeSelf, Is.False); Assert.That(lane.IsDamaging, Is.False); }
        Assert.That(lanes[0], Is.Null); Assert.That(lanes[1], Is.Null); Assert.That(Get(boss, "precisionWarning"), Is.Null);
    }
    [Test] public void ProductionUsesApprovedStatesPooledLaneAndSingleSequence()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss);
        var boss = root.GetComponent<BossPatternController>();
        Assert.That(Get(boss, "useSectorControlSequence"), Is.True);
        foreach (string field in new[] { "sectorLanePrefab", "sectorBody", "sectorIdleSprite", "sectorChargedSprite", "sectorExposedSprite", "sectorContainmentSprite" })
            Assert.That(Get(boss, field), Is.Not.Null, field);
        Assert.That((Transform)Get(boss, "visualRoot"), Is.Not.SameAs(root.transform));
        Assert.That(Get(boss, "maxHp"), Is.EqualTo(140f)); Assert.That(Get(boss, "phase2HpRatio"), Is.EqualTo(.5f));
        Assert.That(Get(boss, "laserTelegraphTime"), Is.EqualTo(1.2f));
        Assert.That(Get(boss, "laserDurationPhase1"), Is.EqualTo(2.5f)); Assert.That(Get(boss, "laserDurationPhase2"), Is.EqualTo(3f));
        Assert.That(Get(boss, "sectorRecoveryTime"), Is.EqualTo(1.1f));
        Assert.That(Get(boss, "laserDamage"), Is.EqualTo(4f)); Assert.That(Get(boss, "spreadProjectileDamage"), Is.EqualTo(2f));
        Assert.That(Get(boss, "chargeProjectileDamage"), Is.EqualTo(6f));
    }
    [Test] public void PhaseEntryIsIdempotentAndDoesNotCreateSchedulerWhenDisabled()
    {
        var g = New("Sector phase"); var hp = g.AddComponent<EnemyHealth>(); hp.SetMaxHp(140);
        var boss = g.AddComponent<BossPatternController>(); Set(boss, "enemyHealth", hp); Set(boss, "usePhase2ShieldTransition", true);
        Call(boss, "EnterTruePhase2"); Call(boss, "UpdatePhase"); Call(boss, "UpdatePhase");
        Assert.That(Get(boss, "phase2"), Is.True);
        foreach (string field in new[] { "patternRoutine", "phase2TransitionRoutine", "phase2ShieldCombatRoutine" }) Assert.That(Get(boss, field), Is.Null);
    }
    [Test] public void ContainmentOnlyChangesPresentationAndKeepsPhysicalAuthority()
    {
        var boss = New("Sector").AddComponent<BossPatternController>();
        var authored = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss).GetComponent<BossPatternController>();
        Set(boss, "useSectorControlSequence", true); Set(boss, "sectorContainmentSprite", Get(authored, "sectorContainmentSprite"));
        Set(boss, "sectorBarrierVisualPrefab", Get(authored, "sectorBarrierVisualPrefab"));
        var wall = New("Wall").AddComponent<BossArenaLaserWall>();
        wall.Initialize(Vector2.zero, Vector2.right, 24, .45f, true, 4, .5f, null, Color.green, "Default", 30, "Default");
        var box = wall.GetComponent<BoxCollider2D>(); var size = box.size; var trigger = box.isTrigger;
        Call(boss, "ApplySectorContainment", wall, 24f);
        Assert.That(box.enabled, Is.True); Assert.That(box.size, Is.EqualTo(size)); Assert.That(box.isTrigger, Is.EqualTo(trigger));
        Assert.That(wall.GetComponent<LineRenderer>().enabled, Is.False);
        Assert.That(wall.GetComponentInChildren<SpriteRenderer>(true).sprite, Is.Not.Null);
    }
    [Test] public void CampaignRewardBindingsAndRegionBCControllersRemainTheirExistingOwners()
    {
        var a = AssetDatabase.LoadAssetAtPath<GameObject>(SectorAdministratorAuthoring.Boss);
        var dummy = new SerializedObject(a.GetComponent<BossDummyController>());
        var definition = (BossCampaignDefinition)dummy.FindProperty("campaignDefinition").objectReferenceValue;
        Assert.That(definition.BossId, Is.EqualTo(CampaignBossId.SectorAdministrator));
        foreach (string f in new[] { "returnBeaconPrefab", "wormholePortalPrefab", "bossRewardCapsulePrefab", "deathPresentation" })
            Assert.That(dummy.FindProperty(f).objectReferenceValue, Is.Not.Null);
        for (int i = 1; i <= 2; i++)
        {
            var other = AssetDatabase.LoadAssetAtPath<GameObject>(BossFinalPolishAudit.BossPrefabs[i]);
            Assert.That(other.GetComponent<BossPatternController>(), Is.Null);
            Assert.That(i == 1 ? (Component)other.GetComponent<FrigateTriadBossController>() : other.GetComponent<PhaseGatekeeperBossController>(), Is.Not.Null);
        }
    }
    [Test] public void EveryRectangleAndHexagonEmitterFacesInwardWithoutMovingItsBoundaryAnchor()
    {
        var boss = New("Sector emitters").AddComponent<BossPatternController>();
        Set(boss, "useSectorControlSequence", true); Set(boss, "arenaCenter", Vector2.zero);
        var positions = (Vector2[])Call(boss, "GetPhase1ManagerFinalPositions");
        var rotations = (float[])Call(boss, "GetPhase1ManagerRotations");
        for (int i = 0; i < positions.Length; i++)
            Assert.That(Vector2.Dot(Quaternion.Euler(0, 0, rotations[i]) * Vector3.up, -positions[i].normalized), Is.GreaterThan(.999f));
        foreach (var position in (Vector2[])Call(boss, "GetPhase2HexagonFinalPositionsCounterClockwise"))
        {
            float rotation = (float)Call(boss, "GetManagerRotationForPosition", position);
            Assert.That(Vector2.Dot(Quaternion.Euler(0, 0, rotation) * Vector3.up, -position.normalized), Is.GreaterThan(.999f));
        }
    }
    [Test] public void CompactSectorShieldTextKeepsLegacyCallPresentationAvailable()
    {
        var ui = New("Shield HUD").AddComponent<BossHealthBarUI>();
        var text = New("Value").AddComponent<TMPro.TextMeshProUGUI>(); Set(ui, "hpText", text);
        ui.ShowPhaseShield(31, 31, "SH"); Assert.That(text.text, Is.EqualTo("SH 31/31"));
        ui.ShowPhaseShield(31, 31); Assert.That(text.text, Is.EqualTo("SHIELD 31 / 31"));
    }
    [Test] public void ApprovedBeamBandMatchesColliderAcrossFireAndSustainFrames()
    {
        var lane = Lane(); var renderer = (SpriteRenderer)Get(lane, "visual");
        float[] times = { 0, .05f, .1f, .15f, .21f, .27f, .33f };
        int[] bandPixels = { 6, 18, 14, 14, 18, 14, 10 };
        for (int i = 0; i < times.Length; i++)
        {
            lane.Activate(times[i]);
            float worldBand = renderer.transform.localScale.y * bandPixels[i] / renderer.sprite.pixelsPerUnit;
            Assert.That(worldBand, Is.EqualTo(lane.GetComponent<BoxCollider2D>().size.y).Within(.0001f));
        }
    }
}
