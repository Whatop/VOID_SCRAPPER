using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class ApprovedVisualIntegrationTests
{
    [SetUp] public void OpenEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    [TearDown] public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    private static T Ref<T>(Object o,string f) where T:Object => (T)new SerializedObject(o).FindProperty(f).objectReferenceValue;
    private static GameObject Asset(string p) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/"+p+".prefab");
    [Test] public void EveryProductionTraitHasAnIcon()
    {
        var traits=Directory.GetFiles("Assets/02_Scripts/Config/TraitDefinition","*.asset",SearchOption.AllDirectories)
            .Select(p=>AssetDatabase.LoadAssetAtPath<TraitDefinition>(p.Replace('\\','/'))).Where(t=>t!=null).ToArray();
        Assert.That(traits.Length,Is.EqualTo(72));
        foreach(var t in traits) Assert.That(t.Icon,Is.Not.Null,t.TraitId);
    }
    [TestCase("01_basic_ship")][TestCase("02_shotgun_ship")][TestCase("03_sniper_ship")]
    public void ShipPreviewUsesItsExistingWeaponSprite(string name)
    {
        var s=AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/"+name+".asset");
        Assert.That(s.PreviewSprite,Is.Not.Null);
        Assert.That(s.PreviewSprite,Is.SameAs(s.GetWeaponSprite(s.DefaultWeaponTree)));
    }
    [Test] public void CommonRaiderBodiesResolveInsideOriginalUnitBounds()
    {
        foreach(string name in new[]{"Enemy_Basic","Enemy_Shotgun","Enemy_Charge","PF_Enemy_MeleeCharger_Common","PF_Enemy_DefenderBasic","PF_Enemy_DefenderShotgun","PF_Enemy_RivalHarvester","PF_Enemy_Scavenger"})
        {
            var r=Asset("Enemy/"+name).GetComponent<SpriteRenderer>();
            Assert.That(r.sprite,Is.Not.Null,name);
            Assert.That(r.sprite.bounds.size.x,Is.LessThanOrEqualTo(1.0001f));
            Assert.That(r.sprite.bounds.size.y,Is.LessThanOrEqualTo(1.0001f));
            Assert.That(r.GetComponent<Collider2D>(),Is.Not.Null);
        }
    }
    [Test] public void PhaseGatekeeperChargeNeverRestoresLegacySprite()
    {
        var boss=Asset("Enemy/PF_Boss_PhaseGatekeeper").GetComponent<PhaseGatekeeperBossController>();
        Assert.That(AssetDatabase.GetAssetPath(Ref<Sprite>(boss,"idleSprite")),Does.Contain("ApprovedIntegration"));
        var a=new SerializedObject(boss).FindProperty("chargeSprites"); Assert.That(a.arraySize,Is.GreaterThan(0));
        for(int i=0;i<a.arraySize;i++) Assert.That(AssetDatabase.GetAssetPath(a.GetArrayElementAtIndex(i).objectReferenceValue),Does.Contain("PhaseGatekeeperCharge"));
    }
    [TestCase("Event_UnstableReactor")][TestCase("Event_BlackBoxRecovery")][TestCase("Event_RescueSignal")][TestCase("Event_UnknownDevice")]
    public void EventIdentityAndProgressAreBound(string name)
    {
        var root=Asset("Event/"+name);
        Assert.That(root.transform.Find("VisualRoot/CoreSprite").GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo(name));
        Assert.That(root.transform.Find("VisualRoot/ProgressPulse").GetComponent<SpriteRenderer>().sprite,Is.Not.Null);
    }
    [Test] public void StoryPartsHaveThreeDistinctApprovedIcons()
    {
        var icons=Enumerable.Range(1,3).Select(n=>AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>("Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region"+n+".asset").StoryPartSprite).ToArray();
        Assert.That(icons.All(i=>i!=null),Is.True); Assert.That(icons.Distinct().Count(),Is.EqualTo(3));
    }
    [Test] public void CurrencyAndCargoShareCorrectResourceIdentity()
    {
        var p=Asset("Object/RewardPickup").GetComponent<RewardPickup>();
        foreach(var t in new[]{CurrencyType.Credits,CurrencyType.ScrapParts,CurrencyType.CoreShards,CurrencyType.TuningChips,CurrencyType.StabilizedAlloy})
            Assert.That(p.GetCurrencySprite(t),Is.Not.Null,t.ToString());
        Assert.That(p.GetCurrencySprite(CurrencyType.StabilizedAlloy),Is.Not.SameAs(p.GetCurrencySprite(CurrencyType.ScrapParts)));
    }
    [TestCase("Tutorial")][TestCase("Expedition")]
    public void GameplayGaugeFillsResolve(string name)
    {
        var scene=EditorSceneManager.OpenScene("Assets/01_Scenes/"+name+".unity",OpenSceneMode.Single);
        var images=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Image>(true));
        var fills=images.Where(i=>ApprovedVisualIntegration.PathOf(i.transform).Contains("HPGauge")&&(i.name=="Fill"||i.name=="ArmorFill")).ToArray();
        Assert.That(fills.Length,Is.GreaterThanOrEqualTo(2)); foreach(var i in fills) {
            Assert.That(i.sprite,Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(i.sprite),Does.Not.Contain("/Editor/"));
        }
    }
}
