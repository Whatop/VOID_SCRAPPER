using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CommonPresentationTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    static void CheckCaps(Image i, float corner)
    {
        Assert.That(i.type, Is.EqualTo(Image.Type.Sliced));
        float border = i.sprite.border.x * i.canvas.referencePixelsPerUnit / i.sprite.pixelsPerUnit / i.pixelsPerUnitMultiplier;
        Assert.That(border, Is.EqualTo(corner).Within(.001f));
        Assert.That(border * 2, Is.LessThanOrEqualTo(i.rectTransform.rect.height + .001f));
        Assert.That(i.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(i.GetComponentInParent<Mask>(), Is.Null);
    }
    [TestCase("Tutorial")] [TestCase("Expedition")]
    public void PlayerSlicedCapsAndArmorStayInsideAuthoredBounds(string name)
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + name + ".unity");
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpeditionHUD>(true)).Single();
            var gauge = (GaugeBarUI)Get(hud,"hpGauge");
            CheckCaps(gauge.FillImage, 1); CheckCaps((Image)Get(hud,"hpTrackImage"), 2);
            foreach(float ratio in new[]{1f,.5f,.1f,0f,1f})
            {
                gauge.SetRatio(ratio);
                Assert.That(gauge.FillImage.rectTransform.rect.width, Is.EqualTo(84 * ratio).Within(.001f));
                Assert.That(gauge.FillImage.rectTransform.rect.height, Is.EqualTo(3));
            }
            CheckCaps((Image)Get(hud,"armorFillImage"), .5f);
        }
        finally { if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    [Test] public void BossFillAndTrackUseProportionalBordersAndOpacityReveal()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Expedition.unity");
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHealthBarUI>(true)).Single();
            var fill = (Image)Get(hud,"fillImage"); var slider=(Slider)Get(hud,"hpSlider");
            CheckCaps(fill,1); CheckCaps(slider.transform.Find("Background").GetComponent<Image>(),2);
            foreach(float ratio in new[]{1f,.5f,.05f,0f})
            {
                Call(hud,"SetDisplayedRatio",ratio,140*ratio,140f);
                Assert.That(fill.rectTransform.rect.width,Is.EqualTo(194*ratio).Within(.001f));
                Assert.That(((RectTransform)Get(hud,"revealRoot")).localScale,Is.EqualTo(Vector3.one));
            }
            string source=System.IO.File.ReadAllText("Assets/02_Scripts/Boss/BossHealthBarUI.cs");
            Assert.That(source,Does.Not.Contain("scale.x *= eased"));
            Assert.That(source,Does.Not.Contain("startScale.x = 0f"));
        }
        finally { if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    [Test] public void StandaloneSlicedGaugeRetainsLeftEdgeAndRestoresFullWidth()
    {
        var root=new GameObject("Gauge",typeof(RectTransform)); var fillObject=new GameObject("Fill",typeof(RectTransform),typeof(Image));
        try
        {
            fillObject.transform.SetParent(root.transform,false);var image=fillObject.GetComponent<Image>();image.type=Image.Type.Sliced;
            image.rectTransform.sizeDelta=new Vector2(96,5);
            var gauge=root.AddComponent<GaugeBarUI>();gauge.ConfigureRuntime(image,null,null,root);
            foreach(float ratio in new[]{1f,.5f,.01f,0f,1f})
            {
                gauge.SetRatio(ratio);var rect=image.rectTransform;
                Assert.That(rect.rect.width,Is.EqualTo(Mathf.Round(96*ratio)));
                Assert.That(rect.anchoredPosition.x-rect.rect.width*.5f,Is.EqualTo(-48));
            }
        }
        finally { Object.DestroyImmediate(root); }
    }
    [TestCase("MachineGun",.09f)] [TestCase("Shotgun",.12f)] [TestCase("Sniper",.12f)]
    public void PooledFlashFollowsFadesAndResetsOnNextLease(string name,float duration)
    {
        var original=PoolManager.Instance;var poolObject=new GameObject("Pool");poolObject.SetActive(false);var pool=poolObject.AddComponent<PoolManager>();
        typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);
        var owner=new GameObject("Owner");GameObject instance=null;
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Scripts/Resources/VFX/Approved/"+name+"Muzzle.prefab");
            instance=pool.Get(prefab,Vector3.right,Quaternion.Euler(0,0,45));var flash=instance.GetComponent<PooledMuzzleFlash>();
            // EditMode does not dispatch Awake automatically.
            Call(flash,"Awake");flash.Play(owner.transform,owner);Assert.That(flash.Lifetime,Is.EqualTo(duration));
            owner.transform.position=Vector3.up;flash.Advance(duration*.5f);
            Assert.That(instance.transform.position,Is.EqualTo(Vector3.right+Vector3.up));
            Assert.That(instance.transform.eulerAngles.z,Is.EqualTo(45).Within(.001f));
            float alpha=instance.GetComponent<SpriteRenderer>().color.a;Assert.That(alpha,Is.GreaterThan(0).And.LessThan(1));
            flash.Advance(duration);Assert.That(instance.activeSelf,Is.False);
            var next=pool.Get(prefab,Vector3.zero,Quaternion.identity);Assert.That(next,Is.SameAs(instance));
            flash.Play(owner.transform,owner);Assert.That(instance.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(1));
            owner.SetActive(false);flash.Advance(.001f);Assert.That(instance.activeSelf,Is.False);
        }
        finally { if(instance!=null)Object.DestroyImmediate(instance);Object.DestroyImmediate(owner);Object.DestroyImmediate(poolObject);typeof(PoolManager).GetProperty("Instance").SetValue(null,original); }
    }
    [Test] public void OrdinaryFlashIsSmallAndGroupLimited()
    {
        var p=AssetDatabase.LoadAssetAtPath<GameObject>(CommonPresentationAuthoring.EnemyFlash);
        Assert.That(p.transform.localScale.x,Is.EqualTo(.18f));Assert.That(p.GetComponent<PooledMuzzleFlash>().Lifetime,Is.EqualTo(.07f));
        string source=System.IO.File.ReadAllText("Assets/02_Scripts/Enemies/EnemyAttackController.cs");
        Assert.That(source,Does.Contain("muzzleFrame != Time.frameCount"));
    }
    [TestCase(true)] [TestCase(false)]
    public void ActiveFlashReleasesImmediatelyOnOwnerDeath(bool playerOwner)
    {
        var prior=PoolManager.Instance;var poolObject=new GameObject("Pool");poolObject.SetActive(false);
        var pool=poolObject.AddComponent<PoolManager>();typeof(PoolManager).GetProperty("Instance").SetValue(null,pool);
        var owner=new GameObject("Owner");owner.SetActive(false);GameObject effect=null;
        try
        {
            Component health=playerOwner?(Component)owner.AddComponent<PlayerHealth>():owner.AddComponent<EnemyHealth>();
            owner.SetActive(true);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SectorFollowupAuthoring.Muzzle);
            effect=pool.Get(prefab,Vector3.zero,Quaternion.identity);var flash=effect.GetComponent<PooledMuzzleFlash>();Call(flash,"Awake");flash.Play(owner.transform,owner);
            Assert.That(flash.IsPlaying,Is.True);Assert.That(Get(flash,playerOwner?"player":"enemy"),Is.SameAs(health));
            if(playerOwner)((Action)Get(health,"Died"))();else ((Action<EnemyHealth>)Get(health,"Died"))((EnemyHealth)health);
            Assert.That(flash.IsPlaying,Is.False);Assert.That(effect.activeSelf,Is.False);
            var reused=pool.Get(prefab,Vector3.zero,Quaternion.identity);flash.Play(owner.transform,owner);
            Assert.That(reused,Is.SameAs(effect));Assert.That(flash.IsPlaying,Is.True);
        }
        finally {if(effect!=null)Object.DestroyImmediate(effect);Object.DestroyImmediate(owner);Object.DestroyImmediate(poolObject);typeof(PoolManager).GetProperty("Instance").SetValue(null,prior);}
    }
    [Test] public void SectorMaterializationRestoresOnAbortAndDoesNotOwnCombatTiming()
    {
        var boss=new GameObject("Boss");boss.SetActive(false);var introObject=new GameObject("Intro");introObject.SetActive(false);
        try
        {
            var sr=boss.AddComponent<SpriteRenderer>();sr.color=Color.green;var pattern=boss.AddComponent<BossPatternController>();
            typeof(BossPatternController).GetField("useSectorControlSequence",Flags).SetValue(pattern,true);
            var intro=introObject.AddComponent<CoreBossIntroSequence>();Call(intro,"CacheBossPresentation",boss);
            Assert.That(sr.color.a,Is.Zero);Call(intro,"RestoreMaterializingBody");Assert.That(sr.color,Is.EqualTo(Color.green));
            string source=System.IO.File.ReadAllText("Assets/02_Scripts/Temp/CoreBossIntroSequence.cs");
            Assert.That(source,Does.Contain("Mathf.Max(.05f, bossRevealScaleDuration)"));
            Assert.That(source,Does.Contain("yield return PlayBossRevealScaleRoutine();"));
        }
        finally {Object.DestroyImmediate(boss);Object.DestroyImmediate(introObject);}
    }
}
