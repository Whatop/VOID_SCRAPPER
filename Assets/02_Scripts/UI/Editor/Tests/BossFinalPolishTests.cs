using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BossFinalPolishTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();
    AudioManager previousAudio;
    PoolManager previousPool, pool;
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Private | BindingFlags.Public).Invoke(o, a);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Private).SetValue(o, v);
    GameObject New(string name, bool active = true) { var g = new GameObject(name); g.SetActive(active); objects.Add(g); return g; }
    [SetUp] public void Setup()
    {
        previousAudio = AudioManager.Instance; previousPool = PoolManager.Instance;
        pool = New("QA pool", false).AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, pool);
    }
    [TearDown] public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null, previousPool);
        if (previousAudio == null && AudioManager.Instance != null) Object.DestroyImmediate(AudioManager.Instance.gameObject);
    }
    Bullet Shot(GameObject source)
    {
        var template = New("Source-scoped bullet", false); template.AddComponent<Rigidbody2D>(); template.AddComponent<Bullet>();
        var g = pool.Get(template, Vector3.zero, Quaternion.identity); objects.Add(g); var b = g.GetComponent<Bullet>();
        Set(b, "sourceRoot", source.transform); Set(b, "owner", ProjectileOwner.Enemy); return b;
    }
    [TestCase("StopCombatForDeathPresentation")]
    [TestCase("StopCurrentBossPatternForTransition")]
    [TestCase("OnDisable")]
    [TestCase("CancelCombat")]
    public void SectorCleanupReleasesOnlyItsSource(string entry)
    {
        var root = New("Sector", false); var controller = root.AddComponent<BossPatternController>();
        var own = Shot(root); var unrelated = Shot(New("Unrelated ordinary enemy"));
        Call(controller, entry);
        Assert.That(own == null || !own.gameObject.activeSelf, Is.True, "Boss bullet survived " + entry);
        Assert.That(unrelated.gameObject.activeSelf, Is.True, "Unrelated actor's bullet was cleared");
    }
    [TestCase("HandleDied")]
    [TestCase("HandleRunEnded")]
    [TestCase("OnDisable")]
    public void RaiderCleanupReleasesBossAndEscortsWithoutClearingUnrelatedActors(string entry)
    {
        var root = New("Raider", false); var p = root.AddComponent<PirateCommanderBossController>();
        var escort = pool.Get(New("Encounter escort", false), Vector3.zero, Quaternion.identity); objects.Add(escort);
        var escorts = (GameObject[])typeof(PirateCommanderBossController).GetField("spawnedEscorts", Private).GetValue(p); escorts[0] = escort;
        var own = Shot(root); var escortShot = Shot(escort); var unrelated = Shot(New("Unrelated ordinary enemy"));
        if (entry == "OnDisable") Call(p, entry); else Call(p, entry, new object[] { null });
        Assert.That(own == null || !own.gameObject.activeSelf, Is.True, "Boss bullet survived");
        Assert.That(escortShot == null || !escortShot.gameObject.activeSelf, Is.True, "Escort bullet survived");
        Assert.That(unrelated.gameObject.activeSelf, Is.True, "Unrelated actor's bullet was cleared");
    }
    [TestCase(0, 140f)] [TestCase(1, 165f)] [TestCase(2, 180f)] [TestCase(3, 300f)]
    public void ProductionStoryPrefabBindingsAndHpAreValid(int index, float hp)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BossFinalPolishAudit.BossPrefabs[index]);
        Assert.That(root, Is.Not.Null);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            foreach (var component in t.GetComponents<Component>()) Assert.That(component, Is.Not.Null, EnemyRosterAudit.PathOf(t));
        var health = root.GetComponent<EnemyHealth>(); Assert.That(health, Is.Not.Null);
        Assert.That(new SerializedObject(health).FindProperty("maxHp").floatValue, Is.EqualTo(hp));
        var death = root.GetComponent<BossDummyController>(); Assert.That(death, Is.Not.Null);
        var data = new SerializedObject(death);
        Assert.That(data.FindProperty("enemyHealth").objectReferenceValue, Is.SameAs(health));
        Assert.That(data.FindProperty("deathPresentation").objectReferenceValue, Is.Not.Null);
        var definition = (BossCampaignDefinition)data.FindProperty("campaignDefinition").objectReferenceValue;
        Assert.That(definition, Is.Not.Null); Assert.That((int)definition.BossId, Is.EqualTo(index + 1));
    }
    [Test] public void SectorProductionThresholdAndReadableCommitRemainAuthored()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(BossFinalPolishAudit.BossPrefabs[0]).GetComponent<BossPatternController>(); var s = new SerializedObject(p);
        Assert.That(s.FindProperty("phase2HpRatio").floatValue, Is.EqualTo(.5f));
        Assert.That(s.FindProperty("phase2SectionLaserLockTime").floatValue, Is.GreaterThan(0));
        Assert.That(s.FindProperty("laserTelegraphTime").floatValue, Is.GreaterThan(0));
    }
    [Test] public void FrigateProductionHasThreeFixedPartsAndBoundedGuidance()
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(BossFinalPolishAudit.BossPrefabs[1]);
        var parts = g.GetComponentsInChildren<FrigateBossPart>(true); Assert.That(parts.Length, Is.EqualTo(3));
        foreach (var p in parts) Assert.That(new SerializedObject(p).FindProperty("maxHealth").floatValue, Is.EqualTo(55));
        var s = new SerializedObject(g.GetComponent<FrigateTriadBossController>());
        Assert.That(s.FindProperty("missileHomingDuration").floatValue, Is.InRange(.1f, 3f));
        Assert.That(s.FindProperty("ricochetMaximumBounces").intValue, Is.EqualTo(3));
    }
    [TestCase("EnterTruePhase2")] [TestCase("OnDisable")]
    public void SectorReleasesItsPhaseFloorWithoutRemovingForeignProtection(string entry)
    {
        var g = New("Protected Sector", false); var health = g.AddComponent<EnemyHealth>();
        var controller = g.AddComponent<BossPatternController>(); Set(controller, "enemyHealth", health);
        var other = new object(); health.SetMaxHp(140); health.SetDamageFloor(controller, .5f); health.SetDamageFloor(other, .1f);
        Call(controller, entry);
        var floors = (Dictionary<object, float>)typeof(EnemyHealth).GetField("damageFloors", Private).GetValue(health);
        Assert.That(floors.ContainsKey(controller), Is.False); Assert.That(floors[other], Is.EqualTo(.1f));
    }
    [TestCase("threeAliveLeftOffset")] [TestCase("threeAliveCenterOffset")] [TestCase("threeAliveRightOffset")]
    [TestCase("twoAliveLeftOffset")] [TestCase("twoAliveRightOffset")] [TestCase("oneAliveOffset")]
    public void FrigateFullSpriteFitsSavedCombatCameraWithTopPadding(string field)
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(BossFinalPolishAudit.BossPrefabs[1]);
        var data = new SerializedObject(g.GetComponent<FrigateTriadBossController>());
        float center = data.FindProperty("formationAnchorOffset").vector2Value.y + data.FindProperty(field).vector2Value.y;
        foreach (var part in g.GetComponentsInChildren<FrigateBossPart>(true))
        {
            var sprite = part.VisualRenderer;
            float topExtent = sprite.sprite.bounds.max.y * sprite.transform.lossyScale.y;
            Assert.That(center + topExtent, Is.LessThanOrEqualTo(4.21875f * 1.2f - .3f), field + " clips full frigate silhouette");
        }
    }
    [Test] public void PhaseIntroCleanupToleratesHudDestroyedFirstDuringSceneExit()
    {
        var p = New("Phase", false).AddComponent<PhaseGatekeeperBossController>();
        var hud = New("Unloading HUD", false).AddComponent<ExpeditionHUD>();
        Set(p, "expeditionHud", hud); Set(p, "introLocksHeld", true);
        Object.DestroyImmediate(hud.gameObject);
        Assert.DoesNotThrow(() => Call(p, "ReleaseIntroLocks", true));
        Assert.That((bool)typeof(PhaseGatekeeperBossController).GetField("introLocksHeld", Private).GetValue(p), Is.False);
    }
    [Test] public void SectorCancellationReleasesOnlyItsInputAndDoesNotKillBoss()
    {
        var g = New("Canceled Sector", false); var hp = g.AddComponent<EnemyHealth>(); hp.SetMaxHp(140);
        var p = g.AddComponent<BossPatternController>(); Set(p, "enemyHealth", hp); Set(p, "initialized", true);
        var player = New("Player", false).AddComponent<PlayerController2D>(); var foreign = new object();
        player.SetExternalControlLocked(p, true); player.SetExternalControlLocked(foreign, true);
        Set(p, "phase2LockedPlayerController", player); Set(p, "phase2PlayerLockActive", true);
        Call(p, "CancelCombat");
        var locks = (HashSet<object>)typeof(PlayerController2D).GetField("externalControlLocks", Private).GetValue(player);
        Assert.That(locks.Contains(p), Is.False); Assert.That(locks.Contains(foreign), Is.True);
        Assert.That(hp.IsDead, Is.False); Assert.That(hp.CurrentHp, Is.EqualTo(140));
        Assert.That((bool)typeof(BossPatternController).GetField("initialized", Private).GetValue(p), Is.False);
    }
}
