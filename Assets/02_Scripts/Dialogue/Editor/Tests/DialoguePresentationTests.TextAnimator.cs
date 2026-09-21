using System;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed partial class DialoguePresentationTests
{
    [TestCase("Operator")]
    [TestCase("Settlement Control")]
    [TestCase("RescueContact")]
    public void TextAnimator_ColdStableSubtitleRestoresActualTMPRendering(string actor)
    {
        createdObject = new GameObject("Cold subtitle rendering", typeof(RectTransform), typeof(Canvas));
        createdObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePresentationValidator.DialogueUiPrefabPath);
        var ui = UnityEngine.Object.Instantiate(prefab, createdObject.transform);
        var writer = ui.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
        ActivateHierarchy(writer.transform, createdObject.transform);
        var text = writer.GetComponent<TextMeshProUGUI>();
        text.rectTransform.sizeDelta = new Vector2(330f, 50f);
        text.text = "Operator connection established.";
        text.maxVisibleCharacters = 4;
        var animator = writer.GetComponent<TextAnimator_TMP>();
        animator.TryInitializingOnce(); // Disabled components still initialize in runtime Awake.
        Assert.That(text.renderMode, Is.EqualTo(TextRenderFlags.DontRender), "Reproduce vendor cold-start ownership.");
        var adapter = writer.GetComponent<DialogueTextAnimationPresentation>();
        adapter.Prepare(text.text, "OrdinaryCommunication", actor);
        Assert.That(animator.enabled, Is.False);
        Assert.That(text.renderMode, Is.EqualTo(TextRenderFlags.Render));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(4));
        text.ForceMeshUpdate();
        Mesh submittedMesh = text.canvasRenderer.GetMesh();
        Assert.That(submittedMesh, Is.Not.Null);
        Assert.That(submittedMesh.vertexCount, Is.GreaterThan(0), "The body must submit geometry, not merely advance its counter.");
        Assert.That(submittedMesh.bounds.size.x, Is.GreaterThan(0f));
        Assert.That(writer.audioClip, Is.Not.Null, "Retain the existing Operator text blip.");
        Assert.That(DialogueSubtitleTypewriter.ShouldPlayBlip(1, writer.BlipGlyphCadence), Is.True);
    }

    [TestCase("TUTORIAL_FakeOperatorTakeover", "FakeOperator", DialogueTextMotionProfile.Hijack)]
    [TestCase("TUTORIAL_UnknownAccessKeyContact", "Operator", DialogueTextMotionProfile.Corrupted)]
    [TestCase("Other", "Curse", DialogueTextMotionProfile.Corrupted)]
    [TestCase("Other", "FakeOperator", DialogueTextMotionProfile.Corrupted)]
    [TestCase("Other", "Operator", DialogueTextMotionProfile.Stable)]
    [TestCase("Other", "Settlement Control", DialogueTextMotionProfile.Stable)]
    [TestCase("Other", "NullDispatcher", DialogueTextMotionProfile.Stable)]
    [TestCase("NPC_RescueContact_Service", "RescueContact", DialogueTextMotionProfile.Stable)]
    public void TextAnimator_ExplicitProfilesKeepOrdinarySpeakersStable(string conversation, string actor, DialogueTextMotionProfile profile)
    {
        Assert.That(DialogueTextAnimationPresentation.ResolveProfile(conversation, actor), Is.EqualTo(profile));
    }

    [Test]
    public void TextAnimator_RealMeshRespectsTMPRevealRichTextAndCleanup()
    {
        createdObject = new GameObject("Text Animator compatibility", typeof(RectTransform), typeof(Canvas));
        createdObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)createdObject.transform).sizeDelta = new Vector2(480f, 270f);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePresentationValidator.DialogueUiPrefabPath);
        GameObject ui = UnityEngine.Object.Instantiate(prefab, createdObject.transform);
        var writer = ui.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
        ActivateHierarchy(writer.transform, createdObject.transform);
        TMP_Text text = writer.GetComponent<TMP_Text>();
        var animator = writer.GetComponent<TextAnimator_TMP>();
        var adapter = writer.GetComponent<DialogueTextAnimationPresentation>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(adapter, Is.Not.Null);
        Assert.That(animator.DatabaseEffects.Database.ContainsKey("vs_curse"), Is.True);
        Assert.That(animator.DatabaseEffects.Database.ContainsKey("vs_hijack"), Is.True);
        text.rectTransform.sizeDelta = new Vector2(330f, 50f);
        string source = DialogueWordWrapUtility.ApplyWordSafeWrapping("<color=#AA77FF>신호 접속.</color> Receive signal!");
        text.text = source;
        text.maxVisibleCharacters = 3;
        text.ForceMeshUpdate();
        adapter.Prepare(source, Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation, Phase2CStoryDialogueIds.FakeOperatorActorName);
        animator.Animate(.1f);
        Assert.That(adapter.CurrentProfile, Is.EqualTo(DialogueTextMotionProfile.Hijack));
        Assert.That(animator.Behaviors.Length, Is.GreaterThan(0));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(3));
        Assert.That(text.text, Is.EqualTo(source));
        for (int i = 3; i < text.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo glyph = text.textInfo.characterInfo[i];
            if (!glyph.isVisible) continue;
            Assert.That(text.textInfo.meshInfo[glyph.materialReferenceIndex].vertices[glyph.vertexIndex], Is.EqualTo(Vector3.zero), "Hidden glyph must not be revealed by Text Animator.");
        }
        text.maxVisibleCharacters = 8;
        animator.Animate(.1f);
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(8));
        adapter.Prepare(source, "Other", "Operator");
        Assert.That(animator.enabled, Is.False);
        Assert.That(text.renderMode, Is.EqualTo(TextRenderFlags.Render));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(8));
        adapter.Prepare(source, "Other", "Curse");
        writer.Stop();
        Assert.That(adapter.CurrentProfile, Is.EqualTo(DialogueTextMotionProfile.Stable));
        Assert.That(animator.enabled, Is.False);
        adapter.ResetPresentation(); adapter.ResetPresentation();
        adapter.Prepare(source, "Other", "Curse");
        adapter.gameObject.SetActive(false);
        // EditMode does not dispatch non-ExecuteAlways lifecycle callbacks.
        typeof(DialogueTextAnimationPresentation).GetMethod("OnDisable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(adapter, null);
        Assert.That(adapter.CurrentProfile, Is.EqualTo(DialogueTextMotionProfile.Stable));
    }
}
