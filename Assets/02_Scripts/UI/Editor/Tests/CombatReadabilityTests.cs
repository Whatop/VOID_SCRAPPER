using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CombatReadabilityTests
{
    static void Set(object o, string f, object v) => CombatReadabilityAuthoring.Set(o, f, v);
    static T Get<T>(object o, string f) => CombatReadabilityAuthoring.Get<T>(o, f);
    static object Call(object o, string f, params object[] v) => CombatReadabilityAuthoring.Invoke(o, f, v);
    [TestCase(0f)] [TestCase(1f)] [TestCase(8f)]
    public void MovingTargetLeadIsBoundedAndStationaryIsExact(float speed)
    {
        var p = new Vector2(8, 0); var v = new Vector2(0, speed);
        Vector2 aim = EnemyAttackController.PredictTargetPosition(Vector2.zero, p, v, 16, .3f);
        Assert.That(aim.x, Is.EqualTo(8).Within(.001f));
        Assert.That(aim.y, Is.EqualTo(speed * .3f).Within(.001f));
    }
    [TestCase(0f)] [TestCase(-1f)] [TestCase(.001f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
    public void InvalidProjectileSpeedFallsBack(float speed)
    {
        Assert.That(EnemyAttackController.PredictTargetPosition(Vector2.zero, Vector2.up, Vector2.right, speed, .65f), Is.EqualTo(Vector2.up));
    }
    [Test] public void InterceptUsesActualProjectileTravelTime()
    {
        Vector2 target = new Vector2(4, 0), velocity = new Vector2(0, 1);
        Vector2 aim = EnemyAttackController.PredictTargetPosition(Vector2.zero, target, velocity, 16, .65f);
        Assert.That(aim.y, Is.GreaterThan(0).And.LessThan(.3f));
        Assert.That(aim.magnitude / 16, Is.EqualTo(aim.y).Within(.0001f));
    }
    [Test] public void ChargerRepositionDoesNotFlipSightOrMovementAndRootHasPhysicsOwner()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/Enemy_Charge.prefab"));
        var player = new GameObject("QA player", typeof(Rigidbody2D)); player.tag = "Player";
        try
        {
            root.transform.position = Vector3.zero; player.transform.position = Vector3.up * 3;
            var ai = root.GetComponent<EnemyBaseAI>(); Call(ai, "Awake");
            ai.ApplyDefinition(AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset"));
            Set(ai, "player", player.transform); Set(ai, "playerBody", player.GetComponent<Rigidbody2D>());
            Set(ai, "currentState", EnemyState.Combat); Set(ai, "chargingRepositionTimer", 1f);
            Set(ai, "bossEncounterIsolated", false); // This local movement fixture is outside any ambient boss isolation.
            Set(root.GetComponent<EnemyAttackController>(), "attackTimer", 10f);
            var sensor = root.GetComponent<EnemyVisionSensor>(); Call(sensor, "Awake"); sensor.SetTarget(player.transform); sensor.ForceDetectTarget(player.transform);
            int changes = 0; ai.StateChanged += (_, __) => changes++;
            for (int i = 0; i < 12; i++)
            {
                Set(ai, "chargingRepositionTimer", 1f); // EditMode deltaTime is not a controlled 12-frame clock.
                Call(ai, "UpdateCombat"); Call(sensor, "UpdateVisualAwareness", .016f);
                Assert.That(sensor.HasRawDirectSight, Is.True);
                Assert.That(Get<Vector2>(ai, "desiredVelocity").y, Is.LessThan(0));
            }
            Assert.That(changes, Is.Zero);
            Set(ai, "facingDirection", Vector2.right); Quaternion old = root.transform.rotation;
            Call(ai, "ApplyFacingRotation", .1f, false);
            Assert.That(root.transform.rotation, Is.EqualTo(old), "Update must not write the Rigidbody root rotation");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(player); }
    }
    [Test] public void ChargeTimingAndProjectileStatsRemainAuthored()
    {
        var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset");
        Assert.That(d.ChargeTime, Is.EqualTo(1.5f)); Assert.That(d.AttackInterval, Is.EqualTo(2.5f));
        Assert.That(d.ProjectileDefinition.Speed, Is.EqualTo(16)); Assert.That(d.ProjectileDefinition.Damage, Is.EqualTo(8));
        var controller = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/Enemy_Charge.prefab").GetComponent<EnemyAttackController>();
        Assert.That(Get<float>(controller, "chargeAimLockFraction"), Is.EqualTo(.75f));
    }
    [Test] public void HangarNumericIdentityAppearsOnceAndBasePreviewRetainsIt()
    {
        var ship = AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/01_basic_ship.asset");
        var projection = new HangarDeploymentProjection();
        typeof(HangarDeploymentProjection).GetProperty("HarvestDamage").SetValue(projection, 1.1f);
        string body = HangarDeploymentText.Build(ship, projection, true, true, null, "");
        string label = HangarDeploymentText.Text("harvest_damage", "수확 오브젝트 피해");
        Assert.That(body.Split(new[] { label }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        Assert.That(body, Does.Not.Contain(HangarDeploymentText.Text("innate", "기체 특성")));
        string baseOnly = HangarDeploymentText.Build(ship, null, true, true, null, "");
        Assert.That(baseOnly, Does.Contain(label));
    }
    [Test] public void CoreFamiliesUseApprovedFramesAndOriginalActivationDuration()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab");
        var owner = root.GetComponent<CoreFamilyPresentation>(); Assert.That(owner, Is.Not.Null);
        foreach (var sprite in Get<Sprite[]>(owner, "idleSprites"))
            Assert.That(sprite.bounds.center, Is.EqualTo(Vector3.zero), "Core exporter pixel pivot must be normalized");
        float duration = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Space Kit/Core/core1.controller").animationClips[0].length;
        var controllers = Get<RuntimeAnimatorController[]>(owner, "familyControllers");
        Assert.That(controllers.Length, Is.EqualTo(4));
        var pulses = Get<GameObject[]>(owner, "activationPulses"); Assert.That(pulses.Length, Is.EqualTo(4));
        foreach (var pulse in pulses) Assert.That(pulse.GetComponent<Animator>().runtimeAnimatorController.animationClips[0].length, Is.EqualTo(.72f).Within(.001f));
        var presentation = root.GetComponent<CoreActivationPresentation>();
        Assert.That(Get<float>(presentation, "pulseDuration"), Is.EqualTo(.72f));
        Assert.That(Get<int>(presentation, "pulseCount"), Is.EqualTo(3)); Assert.That(Get<float>(presentation, "pulseInterval"), Is.EqualTo(.14f));
        foreach (var c in controllers) foreach (var clip in c.animationClips)
        {
            Assert.That(clip.length, Is.EqualTo(duration).Within(.0001f));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach (var frame in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    Assert.That(AssetDatabase.GetAssetPath(frame.value), Does.StartWith(CombatReadabilityAuthoring.Art));
        }
    }
    [TestCase("Tutorial")] [TestCase("Expedition")] [TestCase("Settlement")]
    public void PlayerVfxAreBoundInEveryProductionPlayer(string sceneName)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/" + sceneName + ".unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            foreach (var w in roots.SelectMany(r => r.GetComponentsInChildren<PlayerWeaponBase>(true))) Assert.That(w.HasAuthoredMuzzleEffect, Is.True);
            foreach (var d in roots.SelectMany(r => r.GetComponentsInChildren<PlayerDashAfterimage>(true)))
                foreach (string f in new[] { "normalDashPrefab", "curseDashPrefab" })
                    Assert.That(Get<GameObject>(d, f).GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
            if (sceneName == "Tutorial")
            {
                var t = roots.SelectMany(r => r.GetComponentsInChildren<TutorialFlowController>(true)).Single();
                Assert.That(AssetDatabase.GetAssetPath(Get<SpriteRenderer>(t, "alienSignalCoreRenderer").sprite), Is.EqualTo("Assets/Art/NullDispatcher/PurpleCoreActive.png"));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [TestCase(RaiderBarricadeCarrier.ArenaSide.North, 0)]
    [TestCase(RaiderBarricadeCarrier.ArenaSide.South, 180)]
    [TestCase(RaiderBarricadeCarrier.ArenaSide.East, -90)]
    [TestCase(RaiderBarricadeCarrier.ArenaSide.West, 90)]
    public void EmitterFacesItsSideWithoutRotatingRoot(RaiderBarricadeCarrier.ArenaSide side, float angle)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Raider_BarricadeCarrier.prefab"));
        try
        {
            var carrier = root.GetComponent<RaiderBarricadeCarrier>(); Call(carrier, "Awake"); Set(carrier, "arenaSide", side); carrier.CompleteArrival();
            Assert.That(Mathf.DeltaAngle(root.transform.Find("VisualRoot").localEulerAngles.z, angle), Is.EqualTo(0).Within(.001f));
            Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test] public void BossHudUsesCompactLocalBoundsWithNativeCanvas()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01_Scenes/Expedition.unity");
        try
        {
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHealthBarUI>(true)).Single();
            var slider = Get<Slider>(hud, "hpSlider");
            Assert.That(((RectTransform)slider.transform).rect.size, Is.EqualTo(new Vector2(196, 5)));
            Assert.That(slider.fillRect.rect.height, Is.LessThanOrEqualTo(5));
            Assert.That(Get<TextMeshProUGUI>(hud, "bossNameText").rectTransform.rect.height, Is.EqualTo(14));
            Assert.That(Get<TextMeshProUGUI>(hud, "hpText").color.grayscale, Is.GreaterThan(.5f), "HP must contrast against the dark battlefield");
            var scaler = hud.GetComponentInParent<CanvasScaler>(); Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(480, 270)));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test] public void SegmentedContainmentRetainsAuthoritativeCollisionAndDamage()
    {
        var root = new GameObject("QA containment");
        try
        {
            var wall = root.AddComponent<BossArenaLaserWall>();
            wall.InitializeBetween(new Vector2(-12, 3), new Vector2(12, 3), .35f, true, 4, .5f, null, Color.red, "Default", 15, "Default");
            var collider = root.GetComponent<BoxCollider2D>(); var line = root.GetComponent<LineRenderer>();
            Vector2 size = collider.size, offset = collider.offset; bool trigger = collider.isTrigger;
            Vector3 position = root.transform.position; Quaternion rotation = root.transform.rotation;
            var presentation = root.AddComponent<RaiderArenaBoundaryPresentation>();
            Assert.That(presentation.Configure(line, 24, line.sharedMaterial, "Default", 15, Color.red, Color.red, Color.red, .04f, .2f, .1f, 1), Is.True);
            Assert.That(line.positionCount, Is.GreaterThan(2));
            Assert.That(line.widthCurve.Evaluate(0), Is.Zero.Within(.001f));
            Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.offset, Is.EqualTo(offset)); Assert.That(collider.isTrigger, Is.EqualTo(trigger));
            Assert.That(root.transform.position, Is.EqualTo(position)); Assert.That(root.transform.rotation, Is.EqualTo(rotation));
            Assert.That(Get<float>(wall, "damage"), Is.EqualTo(4)); Assert.That(Get<float>(wall, "damageInterval"), Is.EqualTo(.5f));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
