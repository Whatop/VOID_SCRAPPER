using System;
using System.Collections;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

[AddComponentMenu("VOID SCRAPPER/Dialogue/Dialogue Subtitle Typewriter")]
[DisallowMultipleComponent]
public sealed class DialogueSubtitleTypewriter : TextMeshProTypewriterEffect
{
    [Header("Reveal Timing (Unscaled Seconds)")]
    [SerializeField, Min(0.001f)] private float visibleGlyphInterval = 0.03f;
    [SerializeField, Min(0f)] private float commaPause = 0.08f;
    [SerializeField, Min(0f)] private float sentencePause = 0.18f;
    [SerializeField, Min(0f)] private float ellipsisPause = 0.28f;

    [Header("Text Voice")]
    [SerializeField] private string voicedActorName = "Operator";
    [SerializeField, Min(1)] private int blipGlyphCadence = 2;
    [SerializeField, Range(0.5f, 2f)] private float minimumPitch = 0.97f;
    [SerializeField, Range(0.5f, 2f)] private float maximumPitch = 1.03f;
    [SerializeField] private AudioMixerGroup dialogueMixerGroup;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.45f;

    [Header("Additional Actor Voices")]
    [SerializeField] private AudioClip curseVoiceClip;
    [SerializeField, Min(1)] private int curseBlipGlyphCadence = 2;
    [SerializeField, Range(0f, 1f)] private float curseVoiceVolume = 0.38f;
    [SerializeField] private AudioClip settlementVoiceClip;
    [SerializeField, Min(1)] private int settlementBlipGlyphCadence = 2;
    [SerializeField, Range(0f, 1f)] private float settlementVoiceVolume = 0.40f;

    private int speakableGlyphCount;
    private bool voiceEnabledForEntry;
    private AudioClip activeVoiceClip;
    private int activeBlipGlyphCadence;
    private float activeVoiceVolume;
    private bool[] layoutVisibleCharacters = Array.Empty<bool>();
    private int layoutCharacterCount;
    [SerializeField] private DialogueTextAnimationPresentation textAnimationPresentation;

    public float VisibleGlyphInterval => visibleGlyphInterval;
    public float CommaPause => commaPause;
    public float SentencePause => sentencePause;
    public float EllipsisPause => ellipsisPause;
    public int BlipGlyphCadence => blipGlyphCadence;
    public AudioClip CurseVoiceClip => curseVoiceClip;
    public AudioClip SettlementVoiceClip => settlementVoiceClip;

    public override void Awake()
    {
        SyncBaseConfiguration();
        EnsureReusableAudioSource();
        base.Awake();
    }

    public override void StartTyping(string text, int fromIndex = 0)
    {
        PrepareEntry(text);
        base.StartTyping(textComponent.text, fromIndex);
    }

    public override void PlayText(string text, int fromIndex = 0)
    {
        PrepareEntry(text);
        base.PlayText(textComponent.text, fromIndex);
    }

    public override IEnumerator Play(int fromIndex)
    {
        if (textComponent == null || string.IsNullOrEmpty(textComponent.text))
        {
            Stop();
            yield break;
        }

        PrepareEntry(textComponent.text);

        // Pixel Crushers already accepted this conversation. Only an explicitly remote
        // session opens this short gate; local dialogue starts typing immediately as before.
        DialogueCinematicPresentationController presentation =
            GetComponentInParent<DialogueCinematicPresentationController>();
        if (presentation != null && presentation.IsIncomingCommunicationIntroActive)
        {
            textComponent.maxVisibleCharacters = 0;
            while (presentation != null && presentation.IsIncomingCommunicationIntroActive)
                yield return null;
        }

        if (waitOneFrameBeforeStarting)
        {
            yield return null;
        }

        textComponent.text = textComponent.text.Replace("<br>", "\n");
        ProcessRPGMakerCodes();
        textComponent.maxVisibleCharacters = int.MaxValue;
        textComponent.ForceMeshUpdate();
        TMP_TextInfo completeTextInfo = textComponent.textInfo;

        if (completeTextInfo == null)
        {
            Stop();
            yield break;
        }

        int totalVisibleCharacters = completeTextInfo.characterCount;
        CacheLayoutVisibility(completeTextInfo, totalVisibleCharacters);
        textComponent.maxVisibleCharacters = 0;
        textComponent.ForceMeshUpdate();
        yield return null;
        textComponent.ForceMeshUpdate();

        TMP_TextInfo textInfo = textComponent.textInfo;

        if (textInfo == null)
        {
            Stop();
            yield break;
        }

        charactersTyped = Mathf.Clamp(fromIndex, 0, totalVisibleCharacters);
        textComponent.maxVisibleCharacters = charactersTyped;
        speakableGlyphCount = CountSpeakableGlyphsBefore(
            textInfo,
            charactersTyped);
        onBegin.Invoke();
        paused = false;

        while (charactersTyped < totalVisibleCharacters)
        {
            while (paused)
            {
                yield return null;
            }

            if (rpgMakerTokens.TryGetValue(
                    charactersTyped,
                    out System.Collections.Generic.List<RPGMakerTokenType> tokens))
            {
                int positionBeforeTokens = charactersTyped;

                for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
                {
                    switch (tokens[tokenIndex])
                    {
                        case RPGMakerTokenType.QuarterPause:
                            yield return WaitForUnscaledDuration(commaPause);
                            break;
                        case RPGMakerTokenType.FullPause:
                            yield return WaitForUnscaledDuration(sentencePause);
                            break;
                        case RPGMakerTokenType.SkipToEnd:
                            charactersTyped = totalVisibleCharacters;
                            break;
                        case RPGMakerTokenType.InstantOpen:
                            AdvanceThroughInstantSection(totalVisibleCharacters);
                            break;
                    }
                }

                if (charactersTyped != positionBeforeTokens)
                {
                    textComponent.maxVisibleCharacters = charactersTyped;
                    continue;
                }
            }

            if (charactersTyped >= totalVisibleCharacters)
            {
                break;
            }

            TMP_CharacterInfo characterInfo = textInfo.characterInfo[charactersTyped];
            char typedCharacter = characterInfo.character;
            bool isLayoutVisible = IsLayoutVisibleCharacter(charactersTyped);
            charactersTyped++;
            textComponent.maxVisibleCharacters = charactersTyped;
            textComponent.ForceMeshUpdate();
            HandleAutoScroll();
            onCharacter.Invoke();

            if (isLayoutVisible && IsSpeakableGlyph(typedCharacter))
            {
                speakableGlyphCount++;

                if (ShouldPlayBlip(speakableGlyphCount, activeBlipGlyphCadence))
                {
                    PlayTextBlip();
                }
            }

            float punctuationDelay = GetPunctuationDelay(typedCharacter);

            if (isLayoutVisible && punctuationDelay > 0f)
            {
                StopCharacterAudio();
            }

            float delay = isLayoutVisible
                ? visibleGlyphInterval + punctuationDelay
                : 0f;

            if (delay > 0f)
            {
                yield return WaitForUnscaledDuration(delay);
            }
        }

        Stop();
    }

    public override void Stop()
    {
        if (textAnimationPresentation != null) textAnimationPresentation.ResetPresentation();
        speakableGlyphCount = 0;
        voiceEnabledForEntry = false;
        activeVoiceClip = null;
        activeBlipGlyphCadence = 0;
        activeVoiceVolume = 0f;
        layoutCharacterCount = 0;
        base.Stop();
    }

    public override void StopCharacterAudio()
    {
        base.StopCharacterAudio();

        if (audioSource != null)
        {
            audioSource.pitch = 1f;
        }
    }

    public float GetPunctuationDelay(char character)
    {
        if (character == '…')
        {
            return ellipsisPause;
        }

        if (character == ',' || character == '，')
        {
            return commaPause;
        }

        if (character == '.' ||
            character == '?' ||
            character == '!' ||
            character == '。' ||
            character == '？' ||
            character == '！')
        {
            return sentencePause;
        }

        return 0f;
    }

    public float GetCharacterDelay(char character, bool isVisible)
    {
        return isVisible
            ? visibleGlyphInterval + GetPunctuationDelay(character)
            : 0f;
    }

    public static bool IsSpeakableGlyph(char character)
    {
        return char.IsLetterOrDigit(character);
    }

    public static bool ShouldPlayBlip(int oneBasedSpeakableGlyph, int cadence)
    {
        int safeCadence = Mathf.Max(1, cadence);
        return oneBasedSpeakableGlyph > 0 &&
               (oneBasedSpeakableGlyph - 1) % safeCadence == 0;
    }

    private void PrepareEntry(string text)
    {
        StopCharacterAudio();
        SyncBaseConfiguration();
        textComponent.text = DialogueWordWrapUtility.ApplyWordSafeWrapping(text);
        speakableGlyphCount = 0;
        ResolveActiveVoiceProfile();
        layoutCharacterCount = 0;
        if (textAnimationPresentation != null)
        {
            ConversationState state = DialogueManager.isConversationActive ? DialogueManager.currentConversationState : null;
            string actor = state?.subtitle?.speakerInfo?.nameInDatabase ?? string.Empty;
            textAnimationPresentation.Prepare(textComponent.text,
                DialogueManager.isConversationActive ? DialogueManager.lastConversationStarted : string.Empty, actor);
        }
    }

    private void SyncBaseConfiguration()
    {
        charactersPerSecond = visibleGlyphInterval > 0f
            ? 1f / visibleGlyphInterval
            : 0f;
        interruptAudioClip = true;
        usePlayOneShot = false;
        stopAudioOnSilentCharacters = true;
        stopAudioOnPauseCodes = true;
        fullPauseDuration = sentencePause;
        quarterPauseDuration = commaPause;
    }

    private void EnsureReusableAudioSource()
    {
        if (audioClip == null &&
            curseVoiceClip == null &&
            settlementVoiceClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = voiceVolume;
        audioSource.outputAudioMixerGroup = dialogueMixerGroup;
        audioSource.clip = audioClip;
    }

    private void ResolveActiveVoiceProfile()
    {
        voiceEnabledForEntry = false;
        activeVoiceClip = null;
        activeBlipGlyphCadence = 0;
        activeVoiceVolume = 0f;

        if (!DialogueManager.isConversationActive)
        {
            return;
        }

        ConversationState state = DialogueManager.currentConversationState;
        string actorId = state != null &&
                         state.subtitle != null &&
                         state.subtitle.speakerInfo != null
            ? state.subtitle.speakerInfo.nameInDatabase
            : string.Empty;
        DialogueActorTheme theme = DialoguePresentationPolicy.ResolveActorTheme(
            DialogueManager.lastConversationStarted,
            actorId);

        switch (theme)
        {
            case DialogueActorTheme.Operator:
                if (!string.Equals(actorId, voicedActorName, StringComparison.Ordinal))
                {
                    return;
                }

                activeVoiceClip = audioClip;
                activeBlipGlyphCadence = blipGlyphCadence;
                activeVoiceVolume = voiceVolume;
                break;

            case DialogueActorTheme.Curse:
                activeVoiceClip = curseVoiceClip;
                activeBlipGlyphCadence = curseBlipGlyphCadence;
                activeVoiceVolume = curseVoiceVolume;
                break;

            case DialogueActorTheme.Settlement:
                activeVoiceClip = settlementVoiceClip;
                activeBlipGlyphCadence = settlementBlipGlyphCadence;
                activeVoiceVolume = settlementVoiceVolume;
                break;

            default:
                return;
        }

        voiceEnabledForEntry = activeVoiceClip != null;
        if (!voiceEnabledForEntry)
        {
            return;
        }

        EnsureReusableAudioSource();
        if (runtimeAudioSource != null)
        {
            runtimeAudioSource.clip = activeVoiceClip;
            runtimeAudioSource.volume = activeVoiceVolume;
            runtimeAudioSource.outputAudioMixerGroup = dialogueMixerGroup;
        }
    }

    private void PlayTextBlip()
    {
        if (!voiceEnabledForEntry || activeVoiceClip == null || runtimeAudioSource == null)
        {
            return;
        }

        float lowPitch = Mathf.Min(minimumPitch, maximumPitch);
        float highPitch = Mathf.Max(minimumPitch, maximumPitch);
        runtimeAudioSource.pitch = UnityEngine.Random.Range(lowPitch, highPitch);
        runtimeAudioSource.volume = activeVoiceVolume;
        runtimeAudioSource.clip = activeVoiceClip;

        if (runtimeAudioSource.isPlaying)
        {
            runtimeAudioSource.Stop();
        }

        runtimeAudioSource.Play();
    }

    private void AdvanceThroughInstantSection(int totalVisibleCharacters)
    {
        bool foundClose = false;

        while (!foundClose && charactersTyped < totalVisibleCharacters)
        {
            charactersTyped++;

            if (rpgMakerTokens.TryGetValue(
                    charactersTyped,
                    out System.Collections.Generic.List<RPGMakerTokenType> tokens) &&
                tokens.Contains(RPGMakerTokenType.InstantClose))
            {
                foundClose = true;
            }
        }
    }

    private IEnumerator WaitForUnscaledDuration(float duration)
    {
        float remaining = duration;

        while (remaining > 0f)
        {
            if (!paused)
            {
                remaining -= Time.unscaledDeltaTime;
            }

            yield return null;
        }
    }

    private void CacheLayoutVisibility(
        TMP_TextInfo textInfo,
        int characterCount)
    {
        if (layoutVisibleCharacters.Length < characterCount)
        {
            Array.Resize(ref layoutVisibleCharacters, characterCount);
        }

        for (int index = 0; index < characterCount; index++)
        {
            layoutVisibleCharacters[index] = textInfo.characterInfo[index].isVisible;
        }

        layoutCharacterCount = characterCount;
    }

    private bool IsLayoutVisibleCharacter(int characterIndex)
    {
        return characterIndex >= 0 &&
               characterIndex < layoutCharacterCount &&
               layoutVisibleCharacters[characterIndex];
    }

    private int CountSpeakableGlyphsBefore(TMP_TextInfo textInfo, int endExclusive)
    {
        int count = 0;
        int safeEnd = Mathf.Min(
            endExclusive,
            Mathf.Min(textInfo.characterCount, layoutCharacterCount));

        for (int index = 0; index < safeEnd; index++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[index];

            if (layoutVisibleCharacters[index] &&
                IsSpeakableGlyph(characterInfo.character))
            {
                count++;
            }
        }

        return count;
    }
}
