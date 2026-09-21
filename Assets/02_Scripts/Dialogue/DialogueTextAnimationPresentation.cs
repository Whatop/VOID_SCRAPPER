using System;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using Febucci.TextAnimatorCore.Time;
using TMPro;
using UnityEngine;

public enum DialogueTextMotionProfile
{
    Stable,
    Corrupted,
    Hijack
}

/// <summary>Vertex effects only. DialogueSubtitleTypewriter still owns TMP visibility and timing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextAnimator_TMP))]
public sealed class DialogueTextAnimationPresentation : MonoBehaviour
{
    [SerializeField] private TextAnimator_TMP textAnimator;
    private TMP_Text text;
    public DialogueTextMotionProfile CurrentProfile { get; private set; }

    private static readonly string[] CorruptedTags = { "vs_curse" };
    private static readonly string[] HijackTags = { "vs_hijack" };

    public static DialogueTextMotionProfile ResolveProfile(string conversation, string actor)
    {
        if (string.Equals(conversation, Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation, StringComparison.Ordinal))
            return DialogueTextMotionProfile.Hijack;
        if (DialoguePresentationPolicy.ResolveActorTheme(conversation, actor) == DialogueActorTheme.Curse)
            return DialogueTextMotionProfile.Corrupted;
        // Operator, Control Tower and NULL DISPATCHER remain steady; local NPCs are unchanged.
        return DialogueTextMotionProfile.Stable;
    }

    public void Prepare(string resolvedText, string conversation, string actor)
    {
        ResetPresentation();
        CurrentProfile = ResolveProfile(conversation, actor);
        if (CurrentProfile == DialogueTextMotionProfile.Stable || textAnimator == null) return;
        text = textAnimator.TMProComponent;
        int visible = text.maxVisibleCharacters;
        textAnimator.sharedSettings = null;
        textAnimator.animationLoop = AnimationLoop.LateUpdate;
        textAnimator.localSettings.timeScale = TimeScale.Unscaled;
        textAnimator.localSettings.defaultBehaviorTags = CurrentProfile == DialogueTextMotionProfile.Hijack ? HijackTags : CorruptedTags;
        textAnimator.localSettings.defaultAppearanceTags = Array.Empty<string>();
        textAnimator.localSettings.defaultDisappearanceTags = Array.Empty<string>();
        textAnimator.localSettings.isAnimatingAppearances = false;
        textAnimator.localSettings.isAnimatingDisappearances = false;
        textAnimator.localSettings.isAnimatingBehaviors = true;
        textAnimator.localSettings.isResettingTimeOnNewText = true;
        textAnimator.TryInitializingOnce();
        textAnimator.SetText(resolvedText, false);
        text.maxVisibleCharacters = visible;
        textAnimator.enabled = true;
    }

    public void ResetPresentation()
    {
        CurrentProfile = DialogueTextMotionProfile.Stable;
        if (textAnimator == null) return;
        // Text Animator's Awake initializes even when disabled and claims TMP's
        // mesh with DontRender. Initialize it before restoring base rendering so
        // a cold Operator line cannot depend on a previous corrupted profile.
        textAnimator.TryInitializingOnce();
        text = textAnimator.TMProComponent;
        textAnimator.enabled = false;
        textAnimator.localSettings.defaultBehaviorTags = Array.Empty<string>();
        textAnimator.SetBehaviorsActive(false);
        // Regenerate plain TMP vertices without changing maxVisibleCharacters or source strings.
        if (text != null)
        {
            text.renderMode = TextRenderFlags.Render;
            text.ForceMeshUpdate();
        }
    }

    private void Awake() => ResetPresentation();
    private void OnDisable() => ResetPresentation();
}
