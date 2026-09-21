using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DG.Tweening;
using NUnit.Framework;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class DialoguePresentationTests
{
    private GameObject createdObject;

    [TearDown]
    public void TearDown()
    {
        if (createdObject != null)
        {
            UnityEngine.Object.DestroyImmediate(createdObject);
            createdObject = null;
        }
    }

    [Test]
    public void RemoteIntro_IsExplicitOncePerSessionAndCleansUpWithoutChangingLocalDialogue()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        FieldInfo instance = typeof(DialogueManager).GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = instance.GetValue(null);
        bool quitting = DialogueSystemController.applicationIsQuitting;
        GameObject priorTweenOwner = DOTween.instance != null ? DOTween.instance.gameObject : null;
        DialogueCinematicPresentationController presenter = null;
        try
        {
            GameObject root = AuthoredRuntimeFixture.Create(scene, null, "RemoteIntroFixture", true);
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(480f, 270f);
            DialogueSystemController controller = root.AddComponent<DialogueSystemController>();
            instance.SetValue(null, controller);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePresentationValidator.DialogueUiPrefabPath);
            GameObject ui = UnityEngine.Object.Instantiate(prefab, root.transform);
            ui.SetActive(true);
            RectTransform uiRect = (RectTransform)ui.transform;
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.sizeDelta = Vector2.zero;
            controller.dialogueUI = ui.GetComponent<StandardDialogueUI>();
            presenter = ui.GetComponent<DialogueCinematicPresentationController>();
            InvokePresenter(presenter, "Awake");
            controller.LastConversationStarted = Phase2CStoryDialogueIds.TutorialOpeningConversation;
            controller.isAlternateConversationActive = true;
            // Global/local conversation notification alone must not opt into remote presentation.
            InvokePresenter(presenter, "HandleConversationStarted", null);
            Assert.That(presenter.IsIncomingCommunicationIntroActive, Is.False);
            DialogueCinematicPresentationController.BeginIncomingCommunication();
            FieldInfo tweenField = typeof(DialogueCinematicPresentationController).GetField(
                "incomingCommunicationTween", BindingFlags.Instance | BindingFlags.NonPublic);
            Sequence first = (Sequence)tweenField.GetValue(presenter);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Duration(), Is.InRange(0.15f, 0.4f));
            DialogueCinematicPresentationController.BeginIncomingCommunication();
            Assert.That(tweenField.GetValue(presenter), Is.SameAs(first));

            DialogueSubtitleTypewriter typewriter = ui.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
            ActivateHierarchy(typewriter.transform, ui.transform);
            typewriter.Awake();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(uiRect);
            TMP_Text text = typewriter.GetComponent<TMP_Text>();
            Assert.That(text.gameObject.activeInHierarchy, Is.True);
            Assert.That(text.rectTransform.rect.width, Is.GreaterThan(0f));
            text.text = "Signal received.";
            IEnumerator reveal = typewriter.Play(0);
            Assert.That(reveal.MoveNext(), Is.True);
            Assert.That(text.maxVisibleCharacters, Is.Zero, "First subtitle waits for the incoming panel reveal.");
            first.Complete(true);
            Assert.That(presenter.IsIncomingCommunicationIntroActive, Is.False);
            for (int i = 0; i < 8 && text.maxVisibleCharacters == 0; i++) reveal.MoveNext();
            Assert.That(text.maxVisibleCharacters, Is.GreaterThan(0), "Subtitle reveal must resume after the intro.");
            (reveal as IDisposable)?.Dispose();
            typewriter.Stop();
            DialogueCinematicPresentationController.BeginIncomingCommunication();
            Assert.That(presenter.IsIncomingCommunicationIntroActive, Is.False, "Later subtitles cannot restart this session's intro.");

            InvokePresenter(presenter, "HandleStoppingAllConversations");
            DialogueCinematicPresentationController.BeginIncomingCommunication();
            Assert.That(presenter.IsIncomingCommunicationIntroActive, Is.True, "A new accepted session may reconnect.");
            InvokePresenter(presenter, "OnDisable");
            InvokePresenter(presenter, "OnDisable");
            Assert.That(presenter.IsIncomingCommunicationIntroActive, Is.False);
            foreach (StandardUISubtitlePanel panel in ui.GetComponentsInChildren<StandardUISubtitlePanel>(true))
                Assert.That(panel.panel.localScale, Is.EqualTo(Vector3.one));
        }
        finally
        {
            if (presenter != null) InvokePresenter(presenter, "OnDisable");
            EditorSceneManager.ClosePreviewScene(scene);
            if (priorTweenOwner == null && DOTween.instance != null)
                UnityEngine.Object.DestroyImmediate(DOTween.instance.gameObject);
            instance.SetValue(null, previous);
            DialogueSystemController.applicationIsQuitting = quitting;
        }
    }

    [Test]
    public void RemoteIntro_UsesExistingAudioOptInAndLeavesDirectNpcPathsUntouched()
    {
        string story = File.ReadAllText(Application.dataPath + "/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs");
        string tutorial = File.ReadAllText(Application.dataPath + "/02_Scripts/Tutorial/TutorialFlowController.cs");
        string bridge = File.ReadAllText(Application.dataPath + "/02_Scripts/Dialogue/DialoguePixelCrushersBridge.cs");
        string npc = File.ReadAllText(Application.dataPath + "/02_Scripts/RunRuntime/FieldNpcObjective.cs");
        Assert.That(story, Does.Contain("AudioManager.Play(SoundEventIds.DialogueCommIncoming);\n            DialogueCinematicPresentationController.BeginIncomingCommunication();"));
        Assert.That(tutorial, Does.Contain(": incomingTransmissionSoundEventId);\n                DialogueCinematicPresentationController.BeginIncomingCommunication();"));
        Assert.That(bridge, Does.Not.Contain("BeginIncomingCommunication"));
        Assert.That(npc, Does.Not.Contain("BeginIncomingCommunication"));
        GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(FinalAudioIntegrationInstaller.RemoteBossPrefabPath);
        Assert.That(new SerializedObject(boss.GetComponent<DialogueStoryEntryPoint>())
            .FindProperty("playIncomingCommunicationCue").boolValue, Is.True);
    }

    private static void InvokePresenter(DialogueCinematicPresentationController presenter, string method, params object[] arguments)
    {
        typeof(DialogueCinematicPresentationController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(presenter, arguments ?? new object[] { null });
    }

    [Test]
    public void WordSafeWrapping_ProtectsKoreanWhitespaceDelimitedTokens()
    {
        const string source = "앞 스캔해. 뒤";

        string wrapped = DialogueWordWrapUtility.ApplyWordSafeWrapping(source);

        Assert.That(
            wrapped,
            Does.Contain($"스{DialogueWordWrapUtility.WordJoiner}캔" +
                         $"{DialogueWordWrapUtility.WordJoiner}해" +
                         $"{DialogueWordWrapUtility.WordJoiner}."),
            "Every potential break inside the Korean token must be suppressed.");
        Assert.That(
            DialogueWordWrapUtility.RemoveGeneratedWordJoiners(wrapped),
            Is.EqualTo(source));
    }

    [Test]
    public void WordSafeWrapping_KeepsResolvedBindingAndParticleTogether()
    {
        string binding = DialogueWordWrapUtility.ProtectBindingDisplayString("Mouse 4");
        string wrapped = DialogueWordWrapUtility.ApplyWordSafeWrapping(binding + "로 스캔해.");

        Assert.That(binding, Is.EqualTo("Mouse\u00A04"));
        Assert.That(
            DialogueWordWrapUtility.RemoveGeneratedWordJoiners(wrapped),
            Does.StartWith("Mouse\u00A04로 "));
        Assert.That(
            wrapped,
            Does.Contain($"4{DialogueWordWrapUtility.WordJoiner}로"));
    }

    [Test]
    public void WordSafeWrapping_PreservesRichTextAndPlaceholders()
    {
        const string source = "<color=#AA44FF>{amount}개</color> [var=Binding]로 이동";

        string wrapped = DialogueWordWrapUtility.ApplyWordSafeWrapping(source);

        Assert.That(wrapped, Does.Contain("<color=#AA44FF>"));
        Assert.That(wrapped, Does.Contain("</color>"));
        Assert.That(wrapped, Does.Contain("{amount}"));
        Assert.That(wrapped, Does.Contain("[var=Binding]"));
        Assert.That(
            DialogueWordWrapUtility.TryValidateBalancedRichTextTags(
                wrapped,
                out string error),
            Is.True,
            error);
        Assert.That(
            DialogueWordWrapUtility.RemoveGeneratedWordJoiners(wrapped),
            Is.EqualTo(source));
    }

    [Test]
    public void Typewriter_PunctuationTimingAndBlipPolicyMatchConfiguredDefaults()
    {
        createdObject = new GameObject(
            "Dialogue typewriter policy test",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        DialogueSubtitleTypewriter typewriter =
            createdObject.AddComponent<DialogueSubtitleTypewriter>();

        Assert.That(typewriter.VisibleGlyphInterval, Is.EqualTo(0.03f).Within(0.0001f));
        Assert.That(typewriter.GetPunctuationDelay(','), Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(typewriter.GetPunctuationDelay('.'), Is.EqualTo(0.18f).Within(0.0001f));
        Assert.That(typewriter.GetPunctuationDelay('?'), Is.EqualTo(0.18f).Within(0.0001f));
        Assert.That(typewriter.GetPunctuationDelay('!'), Is.EqualTo(0.18f).Within(0.0001f));
        Assert.That(typewriter.GetPunctuationDelay('…'), Is.EqualTo(0.28f).Within(0.0001f));
        Assert.That(
            typewriter.GetCharacterDelay('가', true),
            Is.EqualTo(0.03f).Within(0.0001f));
        Assert.That(
            typewriter.GetCharacterDelay(
                DialogueWordWrapUtility.WordJoiner,
                false),
            Is.Zero,
            "Invisible word-joiner formatting must not slow visible-glyph cadence.");
        Assert.That(DialogueSubtitleTypewriter.ShouldPlayBlip(1, 2), Is.True);
        Assert.That(DialogueSubtitleTypewriter.ShouldPlayBlip(2, 2), Is.False);
        Assert.That(DialogueSubtitleTypewriter.ShouldPlayBlip(3, 2), Is.True);
        Assert.That(DialogueSubtitleTypewriter.IsSpeakableGlyph('한'), Is.True);
        Assert.That(DialogueSubtitleTypewriter.IsSpeakableGlyph('A'), Is.True);
        Assert.That(DialogueSubtitleTypewriter.IsSpeakableGlyph(' '), Is.False);
        Assert.That(DialogueSubtitleTypewriter.IsSpeakableGlyph(','), Is.False);
        Assert.That(DialogueSubtitleTypewriter.IsSpeakableGlyph('…'), Is.False);
    }

    [Test]
    public void PresentationPolicy_UsesStableConversationIdsAndNeverOwnsCamera()
    {
        DialoguePresentationProfile movement = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialOpeningConversation,
            true);
        DialoguePresentationProfile radar = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialRadarConversation,
            true);
        DialoguePresentationProfile supply = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialSupplyConversation,
            true);
        DialoguePresentationProfile wreck = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
            true);
        DialoguePresentationProfile curse = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
            true);
        DialoguePresentationProfile rescue = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialRescueConversation,
            true);
        DialoguePresentationProfile relayAnalysis = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
            true);
        DialoguePresentationProfile fakeTakeover = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
            true);
        DialoguePresentationProfile firstSettlement = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.FirstSettlementConversation,
            true);
        DialoguePresentationProfile repeatSettlement = DialoguePresentationPolicy.Resolve(
            Phase2CStoryDialogueIds.FirstSettlementConversation,
            false);
        DialoguePresentationProfile localizedDisplayName =
            DialoguePresentationPolicy.Resolve("오퍼레이터", true);

        Assert.That(movement.Mode, Is.EqualTo(DialoguePresentationMode.CompactGuidance));
        Assert.That(radar.Mode, Is.EqualTo(DialoguePresentationMode.CompactGuidance));
        Assert.That(supply.Mode, Is.EqualTo(DialoguePresentationMode.ContextFocus));
        Assert.That(wreck.Mode, Is.EqualTo(DialoguePresentationMode.ContextFocus));
        Assert.That(supply.UsesExternalCameraFocus, Is.True);
        Assert.That(wreck.UsesExternalCameraFocus, Is.True);
        Assert.That(curse.Mode, Is.EqualTo(DialoguePresentationMode.CinematicCommunication));
        Assert.That(rescue.Mode, Is.EqualTo(DialoguePresentationMode.CinematicCommunication));
        Assert.That(
            relayAnalysis.Mode,
            Is.EqualTo(DialoguePresentationMode.CinematicCommunication));
        Assert.That(
            fakeTakeover.Mode,
            Is.EqualTo(DialoguePresentationMode.CinematicCommunication));
        Assert.That(firstSettlement.Mode, Is.EqualTo(DialoguePresentationMode.CinematicCommunication));
        Assert.That(repeatSettlement.Mode, Is.EqualTo(DialoguePresentationMode.CompactGuidance));
        Assert.That(localizedDisplayName.Mode, Is.EqualTo(DialoguePresentationMode.CompactGuidance));
        Assert.That(movement.TakesCameraOwnership, Is.False);
        Assert.That(supply.TakesCameraOwnership, Is.False);
        Assert.That(curse.TakesCameraOwnership, Is.False);
    }

    [Test]
    public void ActorThemePolicy_MapsStableActorsAndFailsToNeutral()
    {
        Assert.That(
            DialoguePresentationPolicy.ResolveActorTheme(
                Phase2CStoryDialogueIds.TutorialOpeningConversation,
                Phase2CStoryDialogueIds.OperatorActorName),
            Is.EqualTo(DialogueActorTheme.Operator));
        Assert.That(
            DialoguePresentationPolicy.ResolveActorTheme(
                Phase2CStoryDialogueIds.FirstSettlementConversation,
                Phase2CStoryDialogueIds.SettlementControlActorName),
            Is.EqualTo(DialogueActorTheme.Settlement));
        Assert.That(
            DialoguePresentationPolicy.ResolveActorTheme(
                Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
                Phase2CStoryDialogueIds.OperatorActorName),
            Is.EqualTo(DialogueActorTheme.Curse));
        Assert.That(
            DialoguePresentationPolicy.ResolveActorTheme(
                Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
                Phase2CStoryDialogueIds.FakeOperatorActorName),
            Is.EqualTo(DialogueActorTheme.Curse));
        Assert.That(
            DialoguePresentationPolicy.ResolveActorTheme(
                "NPC_Routine",
                "Localized Display Name"),
            Is.EqualTo(DialogueActorTheme.Neutral));
    }

    [Test]
    public void PresentationLifecycle_DeduplicatesCallbacksAndAllowsCleanReentry()
    {
        DialoguePresentationLifecycleState state =
            new DialoguePresentationLifecycleState();

        Assert.That(
            state.TryBegin(Phase2CStoryDialogueIds.TutorialSupplyConversation),
            Is.True);
        Assert.That(
            state.TryBegin(Phase2CStoryDialogueIds.TutorialSupplyConversation),
            Is.False,
            "A duplicate Pixel Crushers start callback must not start another transition.");
        Assert.That(state.IsActive, Is.True);
        Assert.That(state.TryEnd(), Is.True);
        Assert.That(state.TryEnd(), Is.False, "Cleanup must be idempotent.");
        Assert.That(state.IsActive, Is.False);
        Assert.That(
            state.TryBegin(Phase2CStoryDialogueIds.TutorialSupplyConversation),
            Is.True,
            "An interrupted presentation must remain replayable.");
    }

    [Test]
    public void Typewriter_LaysOutCompleteTextBeforeRevealingCharacters()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DialoguePresentationValidator.DialogueUiPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        createdObject = new GameObject(
            "Dialogue stable layout test",
            typeof(RectTransform),
            typeof(Canvas));
        createdObject.hideFlags = HideFlags.HideAndDontSave;
        createdObject.SetActive(false);

        Canvas canvas = createdObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRoot = createdObject.GetComponent<RectTransform>();
        canvasRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            DialoguePresentationValidator.ReferenceWidth);
        canvasRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            DialoguePresentationValidator.ReferenceHeight);

        GameObject dialogueInstance = UnityEngine.Object.Instantiate(
            prefab,
            createdObject.transform,
            false);
        dialogueInstance.name = "Communication Dialogue UI fixture";
        RectTransform dialogueRoot = dialogueInstance.GetComponent<RectTransform>();
        Assert.That(dialogueRoot, Is.Not.Null);
        dialogueRoot.anchorMin = Vector2.zero;
        dialogueRoot.anchorMax = Vector2.one;
        dialogueRoot.anchoredPosition = Vector2.zero;
        dialogueRoot.sizeDelta = Vector2.zero;

        DialogueSubtitleTypewriter typewriter =
            dialogueInstance.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
        Assert.That(typewriter, Is.Not.Null);
        ActivateHierarchy(typewriter.transform, dialogueInstance.transform);
        createdObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(dialogueRoot);
        Canvas.ForceUpdateCanvases();

        TMP_Text text = typewriter.GetComponent<TMP_Text>();
        Assert.That(text, Is.Not.Null);
        Assert.That(
            text.gameObject.activeInHierarchy,
            Is.True,
            "Pixel Crushers activates the selected subtitle hierarchy before typing.");
        Assert.That(text.enabled, Is.True, "The authoritative TMP component must be enabled.");
        Assert.That(text.font, Is.Not.Null, "The production subtitle font must resolve.");
        Assert.That(
            text.rectTransform.rect.width,
            Is.GreaterThan(0f),
            "The active subtitle must have a measurable width before Play(0).");
        Assert.That(
            text.rectTransform.rect.height,
            Is.GreaterThan(0f),
            "The active subtitle must have a measurable height before Play(0).");
        AssertActiveParentChain(text.transform, dialogueInstance.transform);
        Canvas containingCanvas = text.GetComponentInParent<Canvas>();
        Assert.That(containingCanvas, Is.Not.Null, "The fixture must contain an active Canvas.");
        Assert.That(containingCanvas.isActiveAndEnabled, Is.True);
        Assert.That(dialogueInstance.transform.IsChildOf(containingCanvas.transform), Is.True);

        string binding = DialogueWordWrapUtility.ProtectBindingDisplayString("Mouse 4");
        string source = $"레이더로 스캔해. {binding}로 확인해.";
        text.text = source;
        IEnumerator play = typewriter.Play(0);

        bool initializationMovedNext = play.MoveNext();
        Assert.That(
            initializationMovedNext,
            Is.True,
            "The production coroutine must yield after preparing and hiding the complete layout.");
        string preparedText = text.text;
        int completeCharacterCount = text.textInfo.characterCount;
        string initializationDiagnostic = DescribeCoroutineStep(
            "Initialization outer step",
            play,
            initializationMovedNext,
            text,
            preparedText);
        Assert.That(
            DialogueWordWrapUtility.RemoveGeneratedWordJoiners(preparedText),
            Is.EqualTo(source));
        Assert.That(preparedText, Does.Contain(DialogueWordWrapUtility.WordJoiner.ToString()));
        Assert.That(preparedText, Does.Contain(DialogueWordWrapUtility.NonBreakingSpace.ToString()));
        Assert.That(completeCharacterCount, Is.GreaterThan(0));
        Assert.That(text.maxVisibleCharacters, Is.Zero);
        Assert.That(text.maxVisibleCharacters, Is.LessThan(completeCharacterCount));

        int hiddenCharacterCount = text.maxVisibleCharacters;
        bool revealed = AdvanceUntilFirstSpeakableGlyph(
            play,
            text,
            preparedText,
            hiddenCharacterCount,
            out bool yieldedNestedEnumerator,
            out string revealDiagnostics);
        Assert.That(
            revealed,
            Is.True,
            initializationDiagnostic + Environment.NewLine + revealDiagnostics);
        Assert.That(
            yieldedNestedEnumerator,
            Is.True,
            "The visible-glyph delay must be yielded as a nested IEnumerator. " +
            revealDiagnostics);
        Assert.That(
            text.text,
            Is.EqualTo(preparedText),
            "Reveal must change maxVisibleCharacters, not replace the laid-out string.");
        Assert.That(text.maxVisibleCharacters, Is.GreaterThan(0));
        Assert.That(text.maxVisibleCharacters, Is.LessThan(completeCharacterCount));
        Assert.That(text.textInfo.characterCount, Is.EqualTo(completeCharacterCount));
        Assert.That(
            DialogueSubtitleTypewriter.IsSpeakableGlyph(
                DialogueWordWrapUtility.WordJoiner),
            Is.False);
        Assert.That(
            DialogueSubtitleTypewriter.IsSpeakableGlyph(
                DialogueWordWrapUtility.NonBreakingSpace),
            Is.False);
        Assert.That(
            typewriter.GetCharacterDelay(DialogueWordWrapUtility.WordJoiner, false),
            Is.Zero);
        Assert.That(
            typewriter.GetCharacterDelay(DialogueWordWrapUtility.NonBreakingSpace, false),
            Is.Zero);

        typewriter.Stop();
        (play as IDisposable)?.Dispose();
    }

    [Test]
    public void DialoguePrefab_ReusesCustomTypewritersAndTwoStageContinueButtons()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DialoguePresentationValidator.DialogueUiPrefabPath);

        Assert.That(prefab, Is.Not.Null);
        DialogueSubtitleTypewriter[] typewriters =
            prefab.GetComponentsInChildren<DialogueSubtitleTypewriter>(true);
        StandardUIContinueButtonFastForward[] continueButtons =
            prefab.GetComponentsInChildren<StandardUIContinueButtonFastForward>(true);

        Assert.That(typewriters.Length, Is.EqualTo(2));
        Assert.That(continueButtons.Length, Is.EqualTo(2));

        for (int index = 0; index < typewriters.Length; index++)
        {
            Assert.That(typewriters[index].stopOnConversationEnd, Is.True);
            Assert.That(typewriters[index].usePlayOneShot, Is.False);
            Assert.That(typewriters[index].interruptAudioClip, Is.True);
        }

        for (int index = 0; index < continueButtons.Length; index++)
        {
            Assert.That(
                continueButtons[index].typewriterEffect,
                Is.TypeOf<DialogueSubtitleTypewriter>(),
                "The existing fast-forward adapter must reveal first and advance on the next press.");
        }

        int voicedProfiles = 0;

        for (int index = 0; index < typewriters.Length; index++)
        {
            if (typewriters[index].audioClip == null)
            {
                continue;
            }

            voicedProfiles++;
            Assert.That(
                AssetDatabase.GetAssetPath(typewriters[index].audioClip),
                Is.EqualTo("Assets/06_Audio/SFX/Talk/test_talk-sfx.wav"));
        }

        Assert.That(voicedProfiles, Is.EqualTo(1));

        DialogueSubtitleTypewriter voicedTypewriter = null;
        for (int index = 0; index < typewriters.Length; index++)
        {
            if (typewriters[index].audioClip != null)
            {
                voicedTypewriter = typewriters[index];
                break;
            }
        }

        Assert.That(voicedTypewriter, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(voicedTypewriter.CurseVoiceClip),
            Is.EqualTo("Assets/06_Audio/SFX/Talk/Curse_txt.wav"));
        Assert.That(
            AssetDatabase.GetAssetPath(voicedTypewriter.SettlementVoiceClip),
            Is.EqualTo("Assets/06_Audio/SFX/Talk/Settlement_txt.wav"));
    }

    [Test]
    public void DialoguePrefab_HasReferenceSafeCinematicPresenterAndSelectableContinue()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DialoguePresentationValidator.DialogueUiPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        DialogueCinematicPresentationController presenter =
            prefab.GetComponent<DialogueCinematicPresentationController>();
        Assert.That(presenter, Is.Not.Null);
        Assert.That(presenter.TakesCameraOwnership, Is.False);
        Assert.That(presenter.UsesUnscaledTransitions, Is.True);
        Assert.That(presenter.OverlayTransitionDuration, Is.InRange(0.15f, 0.20f));
        Assert.That(presenter.PanelTransitionDuration, Is.InRange(0.12f, 0.18f));
        Assert.That(presenter.CinematicTopBarHeight, Is.InRange(12f, 16f));
        Assert.That(presenter.CinematicPanelHeight, Is.InRange(68f, 76f));
        Assert.That(presenter.PortraitAreaWidth, Is.InRange(40f, 48f));
        Assert.That(
            presenter.CinematicPanelHeight + presenter.CinematicTopBarHeight,
            Is.LessThan(DialoguePresentationPolicy.ReferenceHeight));

        Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
        int continueButtonCount = 0;
        for (int index = 0; index < buttons.Length; index++)
        {
            StandardUIContinueButtonFastForward fastForward =
                buttons[index].GetComponent<StandardUIContinueButtonFastForward>();
            if (fastForward == null)
            {
                continue;
            }

            continueButtonCount++;
            Assert.That(buttons[index].interactable, Is.True);
            Assert.That(buttons[index].navigation.mode, Is.Not.EqualTo(Navigation.Mode.None));
            Assert.That(buttons[index].onClick.GetPersistentEventCount(), Is.GreaterThan(0));
            Assert.That(
                buttons[index].GetComponent<PixelCrushers.UIButtonKeyTrigger>(),
                Is.Not.Null,
                "Pixel Crushers must retain keyboard/controller submit alongside mouse click.");
        }

        Assert.That(continueButtonCount, Is.EqualTo(2));
    }

    [Test]
    public void PresentationValidator_DetectsUnknownBindingAndBrokenRichText()
    {
        LocalizationValidationReport report = new LocalizationValidationReport();

        DialoguePresentationValidator.AppendSyntaxIssues(
            $"[var=UnknownBinding] <color=red>테스트" +
            DialogueWordWrapUtility.WordJoiner,
            "dialogue.tutorial.test",
            "ko",
            "fixture.csv",
            2,
            report);

        AssertIssue(report, "UnresolvedInputBindingPlaceholder");
        AssertIssue(report, "UnbalancedDialogueRichText");
        AssertIssue(report, "GeneratedDialogueFormattingInSource");
    }

    [Test]
    public void PresentationValidator_CurrentTutorialSourceHasValidReferenceLayout()
    {
        string csv = File.ReadAllText(
            LocalizationContentImporter.DefaultSourceAssetPath,
            new System.Text.UTF8Encoding(false, true));
        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            LocalizationContentImporter.DefaultSourceAssetPath,
            csv);

        DialoguePresentationValidator.AppendPhase2CTutorialIssues(
            report,
            LocalizationContentImporter.DefaultSourceAssetPath);

        if (report.HasErrors)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int index = 0; index < report.Issues.Count; index++)
            {
                if (report.Issues[index].Severity == LocalizationValidationSeverity.Error)
                {
                    builder.AppendLine(report.Issues[index].ToString());
                }
            }

            Assert.Fail(builder.ToString());
        }
    }

    private static void AssertIssue(
        LocalizationValidationReport report,
        string code)
    {
        for (int index = 0; index < report.Issues.Count; index++)
        {
            if (string.Equals(
                    report.Issues[index].Code,
                    code,
                    StringComparison.Ordinal))
            {
                Assert.That(
                    report.Issues[index].Severity,
                    Is.EqualTo(LocalizationValidationSeverity.Error));
                return;
            }
        }

        Assert.Fail($"Expected validation issue '{code}'.");
    }

    private static void ActivateHierarchy(Transform child, Transform root)
    {
        Transform current = child;

        while (current != null)
        {
            current.gameObject.SetActive(true);

            if (current == root)
            {
                return;
            }

            current = current.parent;
        }
    }

    private static void AssertActiveParentChain(Transform child, Transform root)
    {
        Transform current = child;

        while (current != null)
        {
            Assert.That(
                current.gameObject.activeSelf,
                Is.True,
                $"'{current.name}' must be active before Pixel Crushers starts the typewriter.");

            if (current == root)
            {
                return;
            }

            current = current.parent;
        }

        Assert.Fail("The TMP subtitle is not contained by the instantiated dialogue prefab root.");
    }

    private static bool AdvanceUntilFirstSpeakableGlyph(
        IEnumerator rootRoutine,
        TMP_Text text,
        string preparedText,
        int hiddenCharacterCount,
        out bool yieldedNestedEnumerator,
        out string diagnostics)
    {
        const int MaximumCoroutineSteps = 64;
        Stack<IEnumerator> routines = new Stack<IEnumerator>();
        StringBuilder trace = new StringBuilder();
        routines.Push(rootRoutine);
        yieldedNestedEnumerator = false;
        int revealStep = -1;

        for (int step = 1;
             step <= MaximumCoroutineSteps && routines.Count > 0;
             step++)
        {
            IEnumerator routine = routines.Peek();
            bool movedNext = routine.MoveNext();
            trace.AppendLine(DescribeCoroutineStep(
                $"Step {step} Depth {routines.Count}",
                routine,
                movedNext,
                text,
                preparedText));

            if (!movedNext)
            {
                routines.Pop();
            }
            else if (routine.Current is IEnumerator nestedRoutine)
            {
                yieldedNestedEnumerator = true;
                routines.Push(nestedRoutine);
            }

            if (revealStep < 0 &&
                text.maxVisibleCharacters > hiddenCharacterCount)
            {
                revealStep = step;
            }

            if (revealStep >= 0 && step > revealStep)
            {
                diagnostics = trace.ToString();
                return true;
            }
        }

        trace.AppendLine(
            $"Stopped without a bounded first-glyph reveal. " +
            $"MaxSteps={MaximumCoroutineSteps} RemainingDepth={routines.Count}.");
        diagnostics = trace.ToString();
        return false;
    }

    private static string DescribeCoroutineStep(
        string label,
        IEnumerator routine,
        bool movedNext,
        TMP_Text text,
        string preparedText)
    {
        object current = movedNext ? routine.Current : null;
        string currentType = current != null
            ? current.GetType().FullName
            : "<null>";
        bool textMatches = string.Equals(
            text.text,
            preparedText,
            StringComparison.Ordinal);

        return $"{label}: Iterator={routine.GetType().FullName} " +
               $"MoveNext={movedNext} CurrentType={currentType} " +
               $"CurrentIsIEnumerator={current is IEnumerator} " +
               $"CharacterCount={text.textInfo.characterCount} " +
               $"MaxVisible={text.maxVisibleCharacters} " +
               $"PreparedTextMatches={textMatches}";
    }
}
