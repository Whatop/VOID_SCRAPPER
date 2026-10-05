using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NullArtTests
{
    private GameObject fixture;
    [TearDown] public void Cleanup() { if(fixture!=null)Object.DestroyImmediate(fixture); }
    private NullSignatureVfx Effect(string name)
    {
        fixture=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.Vfx+name+".prefab"));
        return fixture.GetComponent<NullSignatureVfx>();
    }
    [Test] public void WarningUsesGameplayDurationAndWaitsForExplicitHandoff()
    {
        var effect=Effect("RainWarning");effect.Play(fixture,.8f);
        effect.AdvancePresentation(.4f);Assert.That(effect.FrameIndex,Is.EqualTo(2));
        effect.AdvancePresentation(.399f);Assert.That(effect.FrameIndex,Is.EqualTo(5));
        effect.AdvancePresentation(.2f);Assert.That(effect.Owner,Is.SameAs(fixture));
        Assert.That(effect.Visual.sprite.name,Does.Contain("Telegraph"));
        Assert.That(fixture.GetComponent<Collider2D>(),Is.Null);
    }
    [Test] public void FireStaysVisibleAcrossTheExistingDamageWindow()
    {
        var effect=Effect("RainFire");effect.Play(fixture);
        for(int i=0;i<24;i++) {
            effect.AdvancePresentation(.1f);
            Assert.That(effect.FrameIndex,Is.InRange(0,3));
            Assert.That(effect.Owner,Is.SameAs(fixture));
            Assert.That(effect.Visual.enabled,Is.True);
        }
    }
    [Test] public void RainTilesFollowTheSameStrikeAxis()
    {
        var effect=Effect("RainWarning");effect.Play(fixture,.8f);
        effect.SetLine(new Vector2(-6,2),new Vector2(6,2),true);
        Assert.That(effect.transform.position,Is.EqualTo(new Vector3(0,2,0)));
        Assert.That(effect.Visual.size,Is.EqualTo(new Vector2(1,12)));
        Assert.That(Vector3.Dot(effect.transform.up,Vector3.right),Is.GreaterThan(.999f));
    }
    [Test] public void StatesUseRevisedArtAndCoreIdentityHasNoPlaceholder()
    {
        fixture=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.BossPath));
        var p=fixture.GetComponent<NullDispatcherPresentation>();
        var outer=fixture.transform.Find("OuterShellRoot/TemporaryCoreVisual").GetComponent<SpriteRenderer>();
        var inner=fixture.transform.Find("InnerCoreRoot").GetComponent<SpriteRenderer>();
        p.ShowPhase(NullDispatcherBossController.EncounterPhase.Intro);Assert.That(outer.sprite.name,Does.EndWith("dormant_sealed"));
        p.ShowPhase(NullDispatcherBossController.EncounterPhase.Phase1);Assert.That(outer.sprite.name,Does.EndWith("active"));
        p.ShowPhase(NullDispatcherBossController.EncounterPhase.PolarityPhase);Assert.That(outer.sprite.name,Does.EndWith("phase2_unbound"));
        p.ShowPhase(NullDispatcherBossController.EncounterPhase.FinalPhase);Assert.That(inner.sprite.name,Does.EndWith("critical_exposed"));
        p.ShowEndingCore();Assert.That(inner.sprite.name,Is.EqualTo("PurpleCoreActive_00"));
        Assert.That(inner.sprite.rect.size,Is.EqualTo(new Vector2(64,64)));
    }
    [Test] public void VisualPrefabHasAllEightClipsAndNoDamageComponents()
    {
        foreach(string name in new[]{"RainWarning","RainFire","RainRelease","Compression","Redirect","Intervention","CoreBurst","CriticalLoop"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(NullArtAuthoring.Vfx+name+".prefab");
            Assert.That(root.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            Assert.That(root.GetComponents<MonoBehaviour>().All(c=>c is NullSignatureVfx),Is.True);
            var s=new SerializedObject(root.GetComponent<NullSignatureVfx>());var a=s.FindProperty("frames");
            for(int i=0;i<a.arraySize;i++) {
                var sprite=(Sprite)a.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(sprite,Is.Not.Null);Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            }
        }
    }
}
