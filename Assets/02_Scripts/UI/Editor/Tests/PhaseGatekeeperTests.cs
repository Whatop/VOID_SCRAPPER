using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PhaseGatekeeperTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    readonly List<GameObject> objects=new List<GameObject>();
    PoolManager oldPool,pool;AudioManager oldAudio;
    GameObject New(string name,bool active=false){var g=new GameObject(name);g.SetActive(active);objects.Add(g);return g;}
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static object Get(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static object Call(object o,string m,params object[] a)=>o.GetType().GetMethod(m,Flags).Invoke(o,a);
    [SetUp] public void Setup(){oldPool=PoolManager.Instance;oldAudio=AudioManager.Instance;pool=New("Pool").AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);}
    [TearDown] public void Cleanup()
    {
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,oldPool);
        if(oldAudio==null&&AudioManager.Instance!=null)Object.DestroyImmediate(AudioManager.Instance.gameObject);
    }
    PhaseGatekeeperBossController Fixture()
    {
        var c=New("Phase").AddComponent<PhaseGatekeeperBossController>();Set(c,"usePhaseRouteSequence",true);
        var visual=c.gameObject.AddComponent<SpriteRenderer>();Set(c,"bodyRenderer",visual);Set(c,"enemyHealth",c.GetComponent<EnemyHealth>());
        Set(c,"currentArenaBounds",new Bounds(Vector3.zero,new Vector3(14,7.4f,0)));return c;
    }
    static GameObject Production=>AssetDatabase.LoadAssetAtPath<GameObject>(PhaseGatekeeperAuthoring.Boss);
    [Test] public void SingleHealthAndExistingDeathCampaignBindingsRemainAuthoritative()
    {
        var root=Production;var c=root.GetComponent<PhaseGatekeeperBossController>();var hp=root.GetComponent<EnemyHealth>();
        Assert.That(root.GetComponentsInChildren<EnemyHealth>(true).Length,Is.EqualTo(1));Assert.That(hp.MaxHp,Is.EqualTo(180));
        Assert.That(c.UsesPhaseRoutes,Is.True);var s=new SerializedObject(c);Assert.That(s.FindProperty("exposedDuration").floatValue,Is.EqualTo(5));
        var death=new SerializedObject(root.GetComponent<BossDummyController>());
        Assert.That(death.FindProperty("enemyHealth").objectReferenceValue,Is.SameAs(hp));
        Assert.That(((BossCampaignDefinition)death.FindProperty("campaignDefinition").objectReferenceValue).BossId,Is.EqualTo(CampaignBossId.PhaseGatekeeper));
        Assert.That(death.FindProperty("deathPresentation").objectReferenceValue,Is.Not.Null);
    }
    [TestCase(PhaseGatekeeperBossController.EncounterState.Tracking,true)]
    [TestCase(PhaseGatekeeperBossController.EncounterState.Firing,true)]
    [TestCase(PhaseGatekeeperBossController.EncounterState.Converging,true)]
    [TestCase(PhaseGatekeeperBossController.EncounterState.Exposed,false)]
    [TestCase(PhaseGatekeeperBossController.EncounterState.DeadOrCleanup,true)]
    public void HealthExposureGatePreservesExistingPolicy(PhaseGatekeeperBossController.EncounterState state,bool rejects)
    {var c=Fixture();Set(c,"state",state);Assert.That(c.RejectsIncomingDamage,Is.EqualTo(rejects));}
    [TestCase("08_PhaseGatekeeper/phase_gatekeeper_lens_exposed")]
    [TestCase("17_SystemBossVFX_Revisions/VFX_Phase_Portal")]
    [TestCase("16_SystemBossVFX/VFX_Phase_PrecisionLock")]
    [TestCase("17_SystemBossVFX_Revisions/VFX_Phase_RedirectFlash")]
    public void ApprovedCopiesAreExactAndPixelImported(string source)
    {
        string p=PhaseGatekeeperAuthoring.Art+Path.GetFileName(source)+".png";
        Assert.That(File.ReadAllBytes(p),Is.EqualTo(File.ReadAllBytes("ArtTools/Aseprite/Output/"+source+".png")));
        var t=(TextureImporter)AssetImporter.GetAtPath(p);Assert.That(t.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(t.mipmapEnabled,Is.False);
    }
    [TestCase(PhaseGatekeeperAuthoring.Portal)] [TestCase(PhaseGatekeeperAuthoring.Lock)] [TestCase(PhaseGatekeeperAuthoring.Redirect)]
    public void ViewsHaveNoCollisionOrIndependentUpdateAndClearWhenPooled(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab.GetComponentsInChildren<Collider2D>(true),Is.Empty);
        Assert.That(typeof(PhaseCombatVfx).GetMethod("Update",Flags),Is.Null);
        var g=pool.Get(prefab,Vector3.zero,Quaternion.identity);objects.Add(g);var view=g.GetComponent<PhaseCombatVfx>();
        view.Sample(0);Assert.That(view.IsVisible,Is.True);Call(view,"OnDisable");Assert.That(view.IsVisible,Is.False);
    }
    [TestCase(0,0,0)] [TestCase(1,6,0)] [TestCase(2,-6,2)] [TestCase(3,0,-3)] [TestCase(4,2,1.7f)] [TestCase(5,-2,1.7f)]
    public void PairPlacementStaysInsideAndLeavesReactionSpace(int sequence,float px,float py)
    {
        var bounds=new Bounds(Vector3.zero,new Vector3(14,7.4f,0));var boss=new Vector2(0,1.7f);var player=new Vector2(px,py);
        Assert.That(PhaseGatekeeperBossController.TryResolvePhasePair(bounds,boss,player,sequence,out var a,out var b),Is.True);
        var inner=bounds;inner.Expand(new Vector3(-1.7f,-1.7f,1));Assert.That(inner.Contains(a),Is.True);Assert.That(inner.Contains(b),Is.True);
        Assert.That(Vector2.Distance(a,player),Is.GreaterThanOrEqualTo(2));Assert.That(Vector2.Distance(b,player),Is.GreaterThanOrEqualTo(3));
        Assert.That(Vector2.Distance(a,b),Is.GreaterThanOrEqualTo(3));
        Assert.That(PhaseGatekeeperBossController.TryResolvePhasePair(bounds,boss,player,sequence,out var a2,out var b2),Is.True);
        Assert.That(a2,Is.EqualTo(a));Assert.That(b2,Is.EqualTo(b));
    }
    [TestCase(1,0)] [TestCase(0,1)] [TestCase(-1,-1)] [TestCase(1,1)]
    public void CommittedPathEndsInsideContainment(float x,float y)
    {
        var bounds=new Bounds(Vector3.zero,new Vector3(14,7.4f,0));var origin=new Vector2(-2,1);var target=origin+new Vector2(x,y);
        Vector2 end=PhaseGatekeeperBossController.ResolvePhaseEndpoint(bounds,origin,target,72);
        Assert.That(bounds.Contains(end),Is.True);Assert.That(Vector2.Dot((end-origin).normalized,new Vector2(x,y).normalized),Is.GreaterThan(.999f));
    }
    [TestCase(0,0)] [TestCase(2,0)] [TestCase(0,-2)] [TestCase(40,40)]
    public void ExistingPredictionIsStationaryCorrectAndBounded(float vx,float vy)
    {
        var c=Fixture();var p=New("Player").AddComponent<PlayerHealth>();var rb=p.GetComponent<Rigidbody2D>();rb.linearVelocity=new Vector2(vx,vy);
        Set(c,"playerHealth",p);Set(c,"playerRigidbody",rb);var target=(Vector2)Call(c,"ResolvePredictedPlayerTarget");
        Assert.That(c.RouteArenaBounds.Contains(target),Is.True);
        if(vx==0&&vy==0)Assert.That(target,Is.EqualTo(Vector2.zero));else Assert.That(target.magnitude,Is.LessThanOrEqualTo(new Vector2(vx,vy).magnitude*.28f+.001f));
    }
    BossLaserHazard Beam(Transform owner)
    {
        owner.gameObject.SetActive(true);
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(new SerializedObject(Production.GetComponent<PhaseGatekeeperBossController>()).FindProperty("laserHazardPrefab").objectReferenceValue));
        var instance=pool.Get(template,Vector3.zero,Quaternion.identity);objects.Add(instance);var beam=instance.GetComponent<BossLaserHazard>();
        var material=(Material)new SerializedObject(Production.GetComponent<PhaseGatekeeperBossController>()).FindProperty("laserMaterial").objectReferenceValue;
        beam.InitializeBetweenAttached(owner,Vector2.zero,Vector2.right*2,.12f,.8f,4,.5f,material,Color.cyan,"Default",-2);return beam;
    }
    [Test] public void RedirectPreservesSamePooledObjectOwnerLifetimeDamageAndHitHistory()
    {
        var owner=New("Owner").transform;var beam=Beam(owner);int id=beam.GetInstanceID();object routine=Get(beam,"lifetimeRoutine");
        var hits=(Dictionary<int,float>)Get(beam,"lastDamageTimes");hits[42]=Time.time;
        Assert.That(beam.TryRedirectOnce(new Vector2(3,1),new Vector2(-4,-2)),Is.True);
        Assert.That(beam.GetInstanceID(),Is.EqualTo(id));Assert.That(beam.RuntimeOwner,Is.SameAs(owner));Assert.That(beam.transform.parent,Is.SameAs(owner));
        Assert.That(Get(beam,"lifetimeRoutine"),Is.SameAs(routine));Assert.That(hits.ContainsKey(42),Is.True);
        Assert.That(beam.DamageAmount,Is.EqualTo(4));Assert.That(beam.DamageCooldown,Is.EqualTo(.5f));
        Assert.That(beam.TryRedirectOnce(Vector2.zero,Vector2.up),Is.False,"No recursive transfer");
        var line=beam.GetComponent<LineRenderer>();var box=beam.GetComponent<BoxCollider2D>();
        Assert.That(line.enabled&&box.enabled&&beam.IsDamaging,Is.True);
        Assert.That(box.size.x,Is.EqualTo(Vector2.Distance(new Vector2(3,1),new Vector2(-4,-2))).Within(.001f));
        Assert.That(Vector3.Distance(line.GetPosition(0),line.GetPosition(1)),Is.EqualTo(box.size.x).Within(.001f));
        Assert.That(line.startWidth,Is.EqualTo(box.size.y));
    }
    [Test] public void RedirectDoesNotResetPlayerHitCooldownOrHitUnrelatedTargets()
    {
        var beam=Beam(New("Owner").transform);var p=New("Player",true).AddComponent<PlayerHealth>();var collider=p.gameObject.AddComponent<CircleCollider2D>();p.ResetHealth();
        Call(beam,"TryDamage",collider);Assert.That(p.CurrentHp,Is.EqualTo(16));Set(p,"invincibleTimer",0f);
        Assert.That(beam.TryRedirectOnce(Vector2.left,Vector2.right),Is.True);Call(beam,"TryDamage",collider);Assert.That(p.CurrentHp,Is.EqualTo(16));
        var unrelated=New("Player projectile").AddComponent<CircleCollider2D>();Assert.DoesNotThrow(()=>Call(beam,"TryDamage",unrelated));
    }
    [Test] public void BeamPoolResetClearsTransferAndDamageState()
    {
        var beam=Beam(New("Owner").transform);beam.TryRedirectOnce(Vector2.up,Vector2.down);beam.Deactivate();Call(beam,"OnDisable");
        Assert.That(beam.IsDamaging,Is.False);Assert.That(beam.HasRedirected,Is.False);Assert.That(beam.RuntimeOwner,Is.Null);Assert.That(beam.GetComponent<BoxCollider2D>().enabled,Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void CleanupReleasesPairAndTheOwnedBeamTogether(bool afterTransfer)
    {
        var c=Fixture();var a=pool.Get(AssetDatabase.LoadAssetAtPath<GameObject>(PhaseGatekeeperAuthoring.Portal),Vector3.left,Quaternion.identity);objects.Add(a);
        var b=pool.Get(AssetDatabase.LoadAssetAtPath<GameObject>(PhaseGatekeeperAuthoring.Portal),Vector3.right,Quaternion.identity);objects.Add(b);
        Set(c,"entryPortal",a.GetComponent<PhaseCombatVfx>());Set(c,"exitPortal",b.GetComponent<PhaseCombatVfx>());Set(c,"portalPairActive",true);
        var beam=Beam(c.transform);if(afterTransfer)beam.TryRedirectOnce(Vector2.right,Vector2.up);
        ((BossLaserHazard[])Get(c,"activeLaserHazards"))[0]=beam;
        Call(c,"EndPhaseRouteSequence");Assert.That(c.PortalEndpointCount,Is.Zero);Assert.That(c.PortalPairActive,Is.False);
        Assert.That(a.activeSelf||b.activeSelf,Is.False);Assert.That(beam.IsDamaging,Is.False);
    }
    [Test] public void ProductionWarningCommitAndDamageValuesAreRetained()
    {
        var s=new SerializedObject(Production.GetComponent<PhaseGatekeeperBossController>());
        Assert.That(s.FindProperty("laserDamage").floatValue,Is.EqualTo(4));Assert.That(s.FindProperty("laserDamageInterval").floatValue,Is.EqualTo(.5f));
        Assert.That(s.FindProperty("laserFireDuration").floatValue,Is.EqualTo(.8f));Assert.That(s.FindProperty("laserLockDuration").floatValue,Is.EqualTo(.4f));
        Assert.That(s.FindProperty("normalTrackingDuration").floatValue,Is.EqualTo(.65f));Assert.That(s.FindProperty("specialTrackingDuration").floatValue,Is.EqualTo(.55f));
        Assert.That(s.FindProperty("portalWarningDuration").floatValue,Is.GreaterThanOrEqualTo(.56f));
    }
    [Test] public void ApprovedHullFitsNativeFramingWithoutChangingDamageFootprint()
    {
        var visual=Production.transform.Find("BossVisualRoot").GetComponent<SpriteRenderer>();
        Assert.That(visual.sprite.bounds.size.y*visual.transform.localScale.y,Is.InRange(2.3f,2.8f));
        Assert.That(Production.GetComponent<CapsuleCollider2D>().size,Is.EqualTo(new Vector2(2.35f,2.35f)));
    }
    [Test] public void IntroHandoffKeepsOwnedCameraInsteadOfRacingAnUnownedReturn()
    {
        var c=Fixture();var cameraObject=New("Camera");cameraObject.AddComponent<Camera>();var camera=cameraObject.AddComponent<GungeonStyleCamera2D>();
        Set(c,"gameplayCamera",camera);Set(c,"introLocksHeld",true);Set(camera,"cinematicFocusOwner",c);Set(camera,"cinematicFocusActive",true);
        Call(c,"ReleaseIntroLocks",false);
        Assert.That(camera.IsCinematicFocusOwnedBy(c),Is.True);Assert.That((bool)Get(c,"specialCameraFocusHeld"),Is.True);
        Assert.That((bool)Get(c,"introLocksHeld"),Is.False);
    }
    [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)]
    public void BodyStateUsesLensForExposureAndEscalation(bool escalated,bool exposed)
    {
        var c=Fixture();var source=new SerializedObject(Production.GetComponent<PhaseGatekeeperBossController>());
        foreach(var f in new[]{"idleSprite","phaseLockSprite","lensExposedSprite"})Set(c,f,(Sprite)source.FindProperty(f).objectReferenceValue);
        Set(c,"phaseTwoActive",escalated);Set(c,"state",exposed?PhaseGatekeeperBossController.EncounterState.Exposed:PhaseGatekeeperBossController.EncounterState.Tracking);
        Call(c,"SetPhaseRouteBody",false);Assert.That(c.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get(c,escalated||exposed?"lensExposedSprite":"idleSprite")));
        Call(c,"SetPhaseRouteBody",true);Assert.That(c.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get(c,"phaseLockSprite")));
    }
}
