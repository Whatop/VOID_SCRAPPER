using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class Phase2CStoryDialogueTests
{
    private sealed class FakeMainQuestAuthority :
        IMainDamagedAccessKeyQuestStartAuthority
    {
        private MainDamagedAccessKeyQuestState state;

        public FakeMainQuestAuthority(MainDamagedAccessKeyQuestState initialState)
        {
            state = initialState;
        }

        public int StateReadCount { get; private set; }
        public int CompletionRequestCount { get; private set; }
        public int QuestStartCount { get; private set; }

        public MainDamagedAccessKeyQuestState DamagedAccessKeyQuestState
        {
            get
            {
                StateReadCount++;
                return state;
            }
        }

        public int DamagedAccessKeyCollectedPartCount => state switch
        {
            MainDamagedAccessKeyQuestState.Active1 => 1,
            MainDamagedAccessKeyQuestState.Active2 => 2,
            MainDamagedAccessKeyQuestState.ReadyToRestore => 3,
            MainDamagedAccessKeyQuestState.Completed => 3,
            _ => 0
        };

        public int DamagedAccessKeyRequiredPartCount =>
            MainDamagedAccessKeyQuestIds.RequiredPartCount;

        public bool TryCompleteFirstSettlementStory()
        {
            CompletionRequestCount++;
            if (state == MainDamagedAccessKeyQuestState.Invalid)
            {
                return false;
            }

            if (state == MainDamagedAccessKeyQuestState.NotStarted)
            {
                QuestStartCount++;
                state = MainDamagedAccessKeyQuestState.Active0;
            }

            return true;
        }
    }

    private readonly List<UnityEngine.Object> createdObjects =
        new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void MainQuestConditionEvaluation_IsReadOnlyAndStateAware()
    {
        DialogueConditionRegistry registry = new DialogueConditionRegistry();
        FakeMainQuestAuthority authority = new FakeMainQuestAuthority(
            MainDamagedAccessKeyQuestState.Active1);

        bool activeOneRecognized = registry.TryEvaluate(
            DialogueConditionId.MainDamagedAccessKeyActive1,
            null,
            authority,
            out bool activeOneResult);
        bool activeTwoRecognized = registry.TryEvaluate(
            DialogueConditionId.MainDamagedAccessKeyActive2,
            null,
            authority,
            out bool activeTwoResult);

        Assert.That(activeOneRecognized, Is.True);
        Assert.That(activeOneResult, Is.True);
        Assert.That(activeTwoRecognized, Is.True);
        Assert.That(activeTwoResult, Is.False);
        Assert.That(authority.StateReadCount, Is.EqualTo(2));
        Assert.That(authority.CompletionRequestCount, Is.Zero);
        Assert.That(authority.QuestStartCount, Is.Zero);
    }

    [Test]
    public void MissingMainQuestAuthority_FailsClosedWithoutMutation()
    {
        DialogueConditionRegistry registry = new DialogueConditionRegistry();

        bool recognized = registry.TryEvaluate(
            DialogueConditionId.MainDamagedAccessKeyNotStarted,
            null,
            null,
            out bool result);

        Assert.That(recognized, Is.True);
        Assert.That(result, Is.False);
    }

    [Test]
    public void QuestStartAction_DispatchesExactlyOnceAndPersistsInAuthorityState()
    {
        DialogueGameplayActionDispatcher dispatcher =
            new DialogueGameplayActionDispatcher();
        FakeMainQuestAuthority authority = new FakeMainQuestAuthority(
            MainDamagedAccessKeyQuestState.NotStarted);
        dispatcher.BeginConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);

        bool first = dispatcher.TryDispatch(
            DialogueGameplayActionId.MainDamagedAccessKeyStart,
            null,
            authority,
            null);
        LogAssert.Expect(
            LogType.Warning,
            new System.Text.RegularExpressions.Regex(
                "Duplicate action 'MainDamagedAccessKeyStart'"));
        bool duplicate = dispatcher.TryDispatch(
            DialogueGameplayActionId.MainDamagedAccessKeyStart,
            null,
            authority,
            null);

        Assert.That(first, Is.True);
        Assert.That(duplicate, Is.False);
        Assert.That(authority.CompletionRequestCount, Is.EqualTo(1));
        Assert.That(authority.QuestStartCount, Is.EqualTo(1));
        Assert.That(
            authority.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.Active0));
    }

    [Test]
    public void InterruptedConversation_DoesNotStartQuestAndReentryCanStartOnce()
    {
        DialogueGameplayActionDispatcher dispatcher =
            new DialogueGameplayActionDispatcher();
        FakeMainQuestAuthority authority = new FakeMainQuestAuthority(
            MainDamagedAccessKeyQuestState.NotStarted);

        dispatcher.BeginConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);
        dispatcher.EndConversation();

        Assert.That(authority.CompletionRequestCount, Is.Zero);
        Assert.That(authority.QuestStartCount, Is.Zero);

        dispatcher.BeginConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);
        bool started = dispatcher.TryDispatch(
            DialogueGameplayActionId.MainDamagedAccessKeyStart,
            null,
            authority,
            null);

        Assert.That(started, Is.True);
        Assert.That(authority.CompletionRequestCount, Is.EqualTo(1));
        Assert.That(authority.QuestStartCount, Is.EqualTo(1));
    }

    [TestCase(MainDamagedAccessKeyQuestState.Active0)]
    [TestCase(MainDamagedAccessKeyQuestState.Active1)]
    [TestCase(MainDamagedAccessKeyQuestState.Active2)]
    [TestCase(MainDamagedAccessKeyQuestState.ReadyToRestore)]
    [TestCase(MainDamagedAccessKeyQuestState.Completed)]
    public void AlreadyStartedQuest_CompletesStoryWithoutAnotherQuestStart(MainDamagedAccessKeyQuestState state)
    {
        DialogueGameplayActionDispatcher dispatcher =
            new DialogueGameplayActionDispatcher();
        FakeMainQuestAuthority authority = new FakeMainQuestAuthority(
            state);
        dispatcher.BeginConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);

        bool dispatched = dispatcher.TryDispatch(
            DialogueGameplayActionId.MainDamagedAccessKeyStart,
            null,
            authority,
            null);

        Assert.That(dispatched, Is.True);
        Assert.That(authority.CompletionRequestCount, Is.EqualTo(1));
        Assert.That(authority.QuestStartCount, Is.Zero);
        Assert.That(authority.DamagedAccessKeyQuestState, Is.EqualTo(state));
    }

    [Test]
    public void QuestStartIdentity_RoundTripsThroughExistingSaveUnlockFlags()
    {
        GameObject root = new GameObject("Phase2CProgressSaveTest");
        root.SetActive(false);
        createdObjects.Add(root);
        PermanentProgress progress = root.AddComponent<PermanentProgress>();

        Assert.That(progress.TryStartDamagedAccessKeyQuest(), Is.True);
        SaveData saveData = progress.CreateSaveData();
        progress.ResetProgress();
        Assert.That(
            progress.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.NotStarted));

        progress.LoadFromSave(saveData);

        Assert.That(
            progress.HasUnlockFlag(MainDamagedAccessKeyQuestIds.StartedUnlockFlag),
            Is.True);
        Assert.That(
            progress.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.Active0));
    }

    [Test]
    public void TutorialGuidance_MapsOnlyToItsNaturalGameplayStage()
    {
        AssertGuidanceMapping(
            TutorialStep.Move,
            true,
            Phase2CStoryDialogueIds.TutorialOpeningConversation);
        AssertGuidanceMapping(
            TutorialStep.RadarDiscoverSalvage,
            true,
            Phase2CStoryDialogueIds.TutorialRadarConversation);
        AssertGuidanceMapping(
            TutorialStep.DestroyNormalSalvage,
            true,
            Phase2CStoryDialogueIds.TutorialSupplyConversation);
        AssertGuidanceMapping(
            TutorialStep.TravelHighValue,
            true,
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation);

        Assert.That(
            Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                TutorialStep.TravelHighValue,
                false,
                out _),
            Is.False,
            "Ancient-signal dialogue must wait for actual signal detection.");
        Assert.That(
            Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                TutorialStep.AimAndFire,
                true,
                out _),
            Is.False,
            "Unrelated gameplay objectives must not inherit story dialogue.");
    }

    [Test]
    public void TutorialGuidance_InterruptedStagesReplayAndNaturalCompletionIsExactOnce()
    {
        TutorialStoryGuidanceProgress progress =
            new TutorialStoryGuidanceProgress();
        TutorialStep[] stages =
        {
            TutorialStep.Move,
            TutorialStep.RadarDiscoverSalvage,
            TutorialStep.DestroyNormalSalvage,
            TutorialStep.TravelHighValue
        };

        for (int i = 0; i < stages.Length; i++)
        {
            TutorialStep stage = stages[i];
            Assert.That(progress.TryComplete(stage, false), Is.False, stage.ToString());
            Assert.That(progress.IsComplete(stage), Is.False, stage.ToString());
            Assert.That(progress.TryComplete(stage, true), Is.True, stage.ToString());
            Assert.That(progress.IsComplete(stage), Is.True, stage.ToString());
            Assert.That(progress.TryComplete(stage, true), Is.False, stage.ToString());
        }
    }

    [Test]
    public void TutorialOpening_WaitsForMovementAndRequestsConfiguredCueOnce()
    {
        TutorialOpeningTransmissionProgress progress =
            new TutorialOpeningTransmissionProgress();

        Assert.That(progress.HasConfirmedMovement, Is.False);
        Assert.That(progress.TryBeginTransmission(true, out _), Is.False);
        Assert.That(progress.TryConfirmMovement(), Is.True);
        Assert.That(progress.TryConfirmMovement(), Is.False);
        Assert.That(progress.HasConfirmedMovement, Is.True);
        Assert.That(
            progress.TryBeginTransmission(true, out bool requestIncomingCue),
            Is.True);
        Assert.That(requestIncomingCue, Is.True);
        Assert.That(
            progress.TryBeginTransmission(true, out requestIncomingCue),
            Is.False,
            "Duplicate movement notifications must not start a second transmission.");
        Assert.That(requestIncomingCue, Is.False);

        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(
            tutorialScript.text,
            Does.Contain("playerController.MovementStarted += HandlePlayerMovementStarted"));
        Assert.That(
            tutorialScript.text,
            Does.Not.Contain("movementGuidanceTriggerDistance"));
    }

    [Test]
    public void TutorialOpening_InterruptedTransmissionCanReplayWithoutCompletingGuidance()
    {
        TutorialOpeningTransmissionProgress transmission =
            new TutorialOpeningTransmissionProgress();
        TutorialStoryGuidanceProgress guidance =
            new TutorialStoryGuidanceProgress();

        Assert.That(transmission.TryConfirmMovement(), Is.True);
        Assert.That(transmission.TryBeginTransmission(), Is.True);
        transmission.FinishTransmission();
        Assert.That(guidance.TryComplete(TutorialStep.Move, false), Is.False);
        Assert.That(guidance.IsComplete(TutorialStep.Move), Is.False);
        Assert.That(transmission.TryBeginTransmission(), Is.True);
        transmission.FinishTransmission();
        Assert.That(guidance.TryComplete(TutorialStep.Move, true), Is.True);
        Assert.That(guidance.TryComplete(TutorialStep.Move, true), Is.False);
    }

    [Test]
    public void TutorialRadarPulse_UsesRestrainedCyanUnscaledProfile()
    {
        GameObject root = new GameObject("TutorialRadarPulseProfileTest");
        root.SetActive(false);
        createdObjects.Add(root);
        PlayerRadarVFXController controller =
            root.AddComponent<PlayerRadarVFXController>();

        Color color = controller.EffectiveScanPulseColor;
        Assert.That(color.b, Is.GreaterThanOrEqualTo(0.85f));
        Assert.That(color.g, Is.GreaterThanOrEqualTo(0.55f));
        Assert.That(color.r, Is.LessThanOrEqualTo(0.35f));
        Assert.That(color.a, Is.InRange(0.1f, 0.18f));
        Assert.That(controller.EffectiveScanPulseDuration, Is.InRange(0.2f, 0.35f));

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/UI/PlayerRadarVFXController.cs");
        Assert.That(script.text, Does.Contain(".SetUpdate(true)"));
        Assert.That(script.text, Does.Contain("StopActivePulse()"));
        Assert.That(script.text, Does.Not.Contain("ReleaseAfter(effect"));

        MonoScript scannerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        Assert.That(scannerScript.text, Does.Contain("radarVFX?.StopPulse()"));
    }

    [Test]
    public void TutorialUnknownObjective_UsesLocalizedPlayerFacingKeys()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            "Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(
            catalog.TryGetEntry(
                Phase2CStoryDialogueIds.TutorialUnknownSignalTitleTextKey,
                out LocalizationEntry title),
            Is.True);
        Assert.That(title.Korean, Is.EqualTo("미확인 신호"));
        Assert.That(title.Korean, Does.Not.Contain("???"));
        Assert.That(
            catalog.TryGetEntry(
                Phase2CStoryDialogueIds.TutorialUnknownSignalObjectiveTextKey,
                out LocalizationEntry objective),
            Is.True);
        Assert.That(objective.Korean, Is.Not.Empty);
        Assert.That(objective.Korean, Does.Not.Contain("???"));

        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(
            tutorialScript.text,
            Does.Contain("TutorialUnknownSignalTitleTextKey"));
        Assert.That(
            tutorialScript.text,
            Does.Not.Contain("TutorialStep.UnknownMission => \"???\""));
        Assert.That(
            File.ReadAllText("Assets/01_Scenes/Tutorial.unity"),
            Does.Not.Contain("???"),
            "Serialized fallback text must not expose an internal mystery placeholder.");
    }

    [Test]
    public void TutorialPurpleCore_RevealAndTransferOrderingIsExactAndReplaySafe()
    {
        TutorialCorePresentationProgress progress =
            new TutorialCorePresentationProgress();

        Assert.That(progress.Phase, Is.EqualTo(TutorialCorePresentationPhase.Hidden));
        Assert.That(progress.TryBeginReveal(), Is.True);
        Assert.That(progress.TryBeginReveal(), Is.False);
        Assert.That(progress.TryBeginTransfer(), Is.False);
        Assert.That(progress.TryCompleteReveal(), Is.True);
        Assert.That(progress.TryBeginTransfer(), Is.True);
        Assert.That(progress.TryBeginTransfer(), Is.False);
        Assert.That(progress.TryApplyCurse(), Is.False);
        Assert.That(progress.TryBeginImpact(), Is.True);
        Assert.That(progress.TryApplyCurse(), Is.True);
        Assert.That(
            progress.TryApplyCurse(),
            Is.False,
            "Natural completion may apply the persistent Curse only once.");

        progress.Reset();
        progress.Reset();
        Assert.That(progress.Phase, Is.EqualTo(TutorialCorePresentationPhase.Hidden));
    }

    [Test]
    public void TutorialRelayNarrative_RealCutPrecedesRetryableFakeTakeover()
    {
        TutorialRelayNarrativeProgress progress =
            new TutorialRelayNarrativeProgress();

        Assert.That(progress.TryBeginRealOperator(), Is.False);
        Assert.That(progress.TryBeginAnalysis(), Is.True);
        Assert.That(progress.TryBeginAnalysis(), Is.False);
        Assert.That(progress.TryCompleteAnalysis(), Is.True);
        Assert.That(progress.TryBeginRealOperator(), Is.True);
        Assert.That(progress.TryFinishRealOperator(true), Is.True);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialRelayNarrativePhase.AwaitingFakeOperator));
        Assert.That(progress.TryMarkFakeTakeoverCuePresented(), Is.True);
        Assert.That(progress.TryMarkFakeTakeoverCuePresented(), Is.False);
        Assert.That(progress.TryBeginFakeOperator(), Is.True);
        Assert.That(progress.TryFinishFakeOperator(false), Is.False);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialRelayNarrativePhase.AwaitingFakeOperator),
            "An interrupted fake transmission must replay without repeating analysis.");
        Assert.That(progress.TryMarkFakeTakeoverCuePresented(), Is.False);
        Assert.That(progress.TryBeginFakeOperator(), Is.True);
        Assert.That(progress.TryFinishFakeOperator(true), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialRelayNarrativePhase.Completed));
        Assert.That(progress.TryBeginFakeOperator(), Is.False);
    }

    [Test]
    public void TutorialUnknownTracking_IsRadarOnlyAfterRelayAnalysis()
    {
        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(tutorialScript, Is.Not.Null);

        string relayCleanup = ExtractMethodBody(
            tutorialScript.text,
            "private void ClearRelayMapGuidanceForUnknownTracking()");
        string unknownMission = ExtractMethodBody(
            tutorialScript.text,
            "private void RefreshUnknownMission()");
        string marker = ExtractMethodBody(
            tutorialScript.text,
            "private void EnsureUnknownSignalObjectiveMarker()");
        string searchResolution = ExtractMethodBody(
            tutorialScript.text,
            "private void ResolveUnknownSearchArea()");
        string coreRevealCompletion = ExtractMethodBody(
            tutorialScript.text,
            "private void CompletePurpleCoreReveal()");

        Assert.That(relayCleanup, Does.Contain("ClearExternalSearchRegion()"));
        Assert.That(relayCleanup, Does.Contain("radarScanTarget.SetShowOnMap(false)"));
        Assert.That(unknownMission, Does.Not.Contain("SetExternalSearchRegion("));
        Assert.That(marker, Does.Contain("SetVisible(true)"));
        Assert.That(marker, Does.Contain("SetShowOnMap(false)"));
        Assert.That(marker, Does.Not.Contain("SetShowOnMap(true)"));
        Assert.That(
            searchResolution,
            Does.Not.Contain("ClearUnknownSignalObjectiveMarker()"),
            "The Radar clue must remain while the Purple Core is still unresolved.");
        Assert.That(
            coreRevealCompletion,
            Does.Contain("ClearUnknownSignalObjectiveMarker()"),
            "The Radar clue is retired only when the Core reveal completes.");
    }

    [Test]
    public void TutorialPurpleCoreDamage_PlayerOnlyCapAndSharedLatchAreExactOnce()
    {
        TutorialPurpleCoreDamageProgress damage =
            new TutorialPurpleCoreDamageProgress();

        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Enemy,
                20f,
                0f,
                0.12f,
                4f,
                12f,
                out _),
            Is.False);
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.ShopDefense,
                20f,
                0f,
                0.12f,
                4f,
                12f,
                out _),
            Is.False);
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Player,
                20f,
                0f,
                0.12f,
                4f,
                12f,
                out float firstContribution),
            Is.True);
        Assert.That(firstContribution, Is.EqualTo(4f));
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Player,
                20f,
                0.01f,
                0.12f,
                4f,
                12f,
                out _),
            Is.False,
            "Pellets in the same contribution window must not bypass the cap.");
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Player,
                20f,
                0.2f,
                0.12f,
                4f,
                12f,
                out _),
            Is.True);
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Player,
                20f,
                0.4f,
                0.12f,
                4f,
                12f,
                out _),
            Is.True);
        Assert.That(damage.ThresholdReached, Is.True);

        damage.Reset();
        Assert.That(damage.ThresholdReached, Is.False);
        Assert.That(damage.AccumulatedDamage, Is.Zero);
        Assert.That(
            damage.TryAddPlayerDamage(
                ProjectileOwner.Player,
                2f,
                1f,
                0.12f,
                4f,
                12f,
                out _),
            Is.True,
            "An interrupted acquisition must re-arm the damage activation path.");

        TutorialPurpleCoreAcquisitionLatch latch =
            new TutorialPurpleCoreAcquisitionLatch();
        Assert.That(latch.TryBegin(), Is.True);
        Assert.That(latch.TryBegin(), Is.False);
        Assert.That(latch.TryComplete(), Is.True);
        Assert.That(latch.TryComplete(), Is.False);
        Assert.That(latch.TryBegin(), Is.False);
    }

    [Test]
    public void TutorialPurpleCore_UsesNonDestructibleProjectileReceiverAndMovesVisualOnly()
    {
        GameObject core = new GameObject("Tutorial Purple Core receiver test");
        createdObjects.Add(core);
        TutorialPurpleCoreDamageReceiver receiver =
            core.AddComponent<TutorialPurpleCoreDamageReceiver>();
        Assert.That(receiver, Is.Not.InstanceOf<IDamageable>());

        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        MonoScript receiverScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialInteractionTarget.cs");
        MonoScript bulletScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Player/Bullet.cs");
        Assert.That(receiverScript.text, Does.Contain("context.Owner != ProjectileOwner.Player"));
        Assert.That(receiverScript.text, Does.Contain("context.SourceRoot == null"));
        Assert.That(receiverScript.text, Does.Contain("GetComponentInParent<IPlayerOwnedAlly>()"));
        Assert.That(bulletScript.text, Does.Contain("IProjectileDamageReceiver"));
        Assert.That(bulletScript.text, Does.Contain("damagedTargets.Contains(receiverId)"));

        string interaction = ExtractMethodBody(
            tutorialScript.text,
            "private void HandlePlayerInteracted(IInteractable target)");
        Assert.That(interaction, Does.Contain("TryRequestPurpleCoreAcquisition("));
        Assert.That(
            receiverScript.text,
            Does.Contain("tutorialController.TryRequestPurpleCoreAcquisition("));
        string infiltration = ExtractMethodBody(
            tutorialScript.text,
            "private Sequence BeginAlienSignalInfiltration()");
        Assert.That(infiltration, Does.Contain("coreTravelTransform.DOMove(playerTarget"));
        Assert.That(infiltration, Does.Not.Contain("activeTutorialPurpleEffect.transform.DOMove"));
        Assert.That(infiltration, Does.Not.Contain("transferTransform.DOMove"));
        Assert.That(infiltration, Does.Not.Contain("alienSignal.transform.DOMove"));
        Assert.That(infiltration, Does.Contain("corruptionTransform.DOScale("));
        Assert.That(infiltration, Does.Contain(".SetUpdate(true)"));

        string restore = ExtractMethodBody(
            tutorialScript.text,
            "private void RestoreAlienSignalCoreTravelTransform(bool restoreVisual)");
        Assert.That(restore, Does.Contain("SetParent("));
        Assert.That(restore, Does.Contain("alienSignalCoreOriginalLocalPosition"));
        Assert.That(restore, Does.Contain("alienSignalCoreOriginalLocalRotation"));
        Assert.That(restore, Does.Contain("alienSignalCoreOriginalLocalScale"));

        string acquisitionRetry = ExtractMethodBody(
            tutorialScript.text,
            "private void RestorePurpleCoreAcquisitionRequest()");
        Assert.That(acquisitionRetry, Does.Contain("coreAcquisitionLatch.Cancel()"));
        Assert.That(acquisitionRetry, Does.Contain("alienSignalDamageReceiver?.ResetProgress()"));
        Assert.That(acquisitionRetry, Does.Contain("SetReceivingEnabled(true)"));
    }

    [Test]
    public void TutorialPurpleCore_PresentationUsesVisualOnlyIdleAndUnscaledTweens()
    {
        GameObject root = new GameObject("TutorialCorePresentationProfileTest");
        root.SetActive(false);
        createdObjects.Add(root);
        TutorialFlowController controller =
            root.AddComponent<TutorialFlowController>();

        Assert.That(controller.PurpleCoreRevealInitialScale, Is.InRange(0.65f, 0.75f));
        Assert.That(controller.PurpleCoreRevealSeconds, Is.InRange(0.45f, 0.65f));
        Assert.That(controller.CurseTransferSeconds, Is.InRange(0.45f, 0.7f));

        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(tutorialScript.text, Does.Contain("SetAlienSignalVisualAlpha(0f)"));
        Assert.That(tutorialScript.text, Does.Contain("SetAlienSignalCollidersEnabled(false)"));
        Assert.That(
            tutorialScript.text,
            Does.Contain("currentStep >= TutorialStep.RevealPurpleCore"));

        int idleStart = tutorialScript.text.IndexOf(
            "private void StartAlienSignalIdleMotion()",
            StringComparison.Ordinal);
        Assert.That(idleStart, Is.GreaterThanOrEqualTo(0));

        int idleStop = tutorialScript.text.IndexOf(
            "private void StopAlienSignalIdleMotion()",
            idleStart,
            StringComparison.Ordinal);
        Assert.That(idleStop, Is.GreaterThan(idleStart));

        int idleEnd = tutorialScript.text.IndexOf(
            "private void ResetPurpleCorePresentation(",
            idleStop,
            StringComparison.Ordinal);
        Assert.That(idleEnd, Is.GreaterThan(idleStop));

        string startIdleSource = tutorialScript.text.Substring(
            idleStart,
            idleStop - idleStart);
        string stopIdleSource = tutorialScript.text.Substring(
            idleStop,
            idleEnd - idleStop);

        Assert.That(
            System.Text.RegularExpressions.Regex.IsMatch(
                startIdleSource,
                @"alienSignalIdleTween\s*=\s*alienSignalVisualRoot\s*\.DOLocalMoveY\s*\("),
            Is.True,
            "The idle tween must target alienSignalVisualRoot with local-space Y movement.");
        Assert.That(startIdleSource, Does.Contain("purpleCoreIdleFloatDistance"));
        Assert.That(startIdleSource, Does.Contain("purpleCoreIdleFloatDuration"));
        Assert.That(
            startIdleSource,
            Does.Contain("alienSignalVisualRestLocalPosition.y + distance"));
        Assert.That(startIdleSource, Does.Contain(".SetLoops(-1, LoopType.Yoyo)"));
        Assert.That(startIdleSource, Does.Contain(".SetUpdate(true)"));
        Assert.That(startIdleSource, Does.Not.Contain("alienSignal.transform"));
        Assert.That(startIdleSource, Does.Not.Contain(".DOMoveY("));

        Assert.That(stopIdleSource, Does.Contain("alienSignalIdleTween?.Kill()"));
        Assert.That(stopIdleSource, Does.Contain("alienSignalIdleTween = null"));
        Assert.That(
            stopIdleSource,
            Does.Contain(
                "alienSignalVisualRoot.localPosition = alienSignalVisualRestLocalPosition"));
        Assert.That(stopIdleSource, Does.Not.Contain("alienSignal.transform"));

        Assert.That(tutorialScript.text, Does.Contain("BeginAlienSignalInfiltration()"));
        Assert.That(tutorialScript.text, Does.Contain("coreTravelTransform.DOMove(playerTarget"));
        Assert.That(tutorialScript.text, Does.Not.Contain("transferTransform.DOMove(playerTarget"));
        Assert.That(tutorialScript.text, Does.Not.Contain("alienSignalVisualRoot.DOMove"));
        Assert.That(tutorialScript.text, Does.Contain("corePresentationProgress.TryApplyCurse()"));
        Assert.That(Enum.GetValues(typeof(TutorialStep)).Length, Is.EqualTo(28));
    }

    [Test]
    public void TargetPresentation_DiscoveryFocusDialogueAndNaturalReturnAdvanceOnce()
    {
        TutorialTargetPresentationProgress progress =
            new TutorialTargetPresentationProgress();

        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.HighValueWreck,
                true),
            Is.True);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.Focusing),
            "A discovered ancient wreck reserves focus even outside the viewport.");
        Assert.That(progress.TryBeginFocus(false, 0f, 0.15f), Is.False,
            "High-value discovery has already reserved focus; it must not wait for visibility.");
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Focusing));
        Assert.That(progress.TryBeginDialogue(), Is.True);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.Dialogue));
        Assert.That(progress.TryBeginReturn(true), Is.True);
        Assert.That(progress.TryBeginReturn(true), Is.False,
            "Duplicate terminal callbacks cannot restart the return transition.");
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.Returning));
        Assert.That(
            progress.TryFinishReturn(
                out TutorialTargetPresentationKind completedKind,
                out bool shouldAdvance),
            Is.True);
        Assert.That(
            completedKind,
            Is.EqualTo(TutorialTargetPresentationKind.HighValueWreck));
        Assert.That(shouldAdvance, Is.True);
        Assert.That(progress.TryFinishReturn(out _, out _), Is.False);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.Idle));
    }

    [Test]
    public void TargetPresentation_DuplicateInterruptionAndInvalidTargetFailSafely()
    {
        TutorialTargetPresentationProgress progress =
            new TutorialTargetPresentationProgress();

        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.SupplyContainer,
                false),
            Is.False);
        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.SupplyContainer,
                true),
            Is.True);
        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.HighValueWreck,
                true),
            Is.False,
            "Supply and high-value discovery must not collide or create a second presentation.");
        Assert.That(progress.TryBeginFocus(true, 0.15f, 0.15f), Is.True);
        Assert.That(progress.TryBeginDialogue(), Is.True);
        Assert.That(progress.TryBeginReturn(false), Is.True);
        Assert.That(
            progress.TryFinishReturn(out _, out bool shouldAdvance),
            Is.True);
        Assert.That(shouldAdvance, Is.False);

        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.HighValueWreck,
                true),
            Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Focusing));
        Assert.That(progress.TryBeginDialogue(), Is.True);
        Assert.That(progress.TryBeginReturn(true), Is.True);
        progress.CancelCheckpointCompletion();
        Assert.That(progress.TryFinishReturn(out _, out shouldAdvance), Is.True);
        Assert.That(
            shouldAdvance,
            Is.False,
            "A target lost during the return blend must not complete its checkpoint.");

        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.SupplyContainer,
                true),
            Is.True,
            "Interrupted presentation must remain replayable.");
        progress.Reset();
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.Idle),
            "Disable and scene-exit cleanup must return ephemeral presentation state to Idle.");
    }

    [Test]
    public void TargetPresentation_SupplyUsesTheSameFocusDialogueReturnPipeline()
    {
        TutorialTargetPresentationProgress progress =
            new TutorialTargetPresentationProgress();

        Assert.That(
            progress.TryRequest(
                TutorialTargetPresentationKind.SupplyContainer,
                true),
            Is.True);
        Assert.That(
            progress.Phase,
            Is.EqualTo(TutorialTargetPresentationPhase.WaitingForSafeViewport));
        Assert.That(progress.TryBeginFocus(true, 0.15f, 0.15f), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Focusing));
        Assert.That(progress.TryBeginDialogue(), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Dialogue));
        Assert.That(progress.TryBeginReturn(true), Is.True);
        Assert.That(
            progress.TryFinishReturn(
                out TutorialTargetPresentationKind completedKind,
                out bool shouldAdvance),
            Is.True);
        Assert.That(completedKind, Is.EqualTo(TutorialTargetPresentationKind.SupplyContainer));
        Assert.That(shouldAdvance, Is.True);
    }

    [Test]
    public void RadarMode_QTogglesAndMouseBackScansOnceWithoutCharge()
    {
        RadarModeInputState state = new RadarModeInputState();

        Assert.That(state.IsRadarActive, Is.False);
        Assert.That(state.TryBeginInstantScan(true, 0f, 0.5f), Is.False);
        Assert.That(state.TryToggle(true, out bool active), Is.True);
        Assert.That(active, Is.True);
        Assert.That(state.TryBeginInstantScan(true, 0f, 0.5f), Is.True);
        Assert.That(
            state.TryBeginInstantScan(true, 0f, 0.5f),
            Is.False,
            "Holding one physical press must not bypass the scan cooldown.");
        Assert.That(state.TryBeginInstantScan(true, 0.49f, 0.5f), Is.False);
        Assert.That(state.TryBeginInstantScan(true, 0.5f, 0.5f), Is.True);
        Assert.That(state.TryToggle(true, out active), Is.True);
        Assert.That(active, Is.False);
        Assert.That(state.TryBeginInstantScan(true, 1f, 0.5f), Is.False);
    }

    [Test]
    public void RadarMode_InputLocksBlockToggleAndInstantScan()
    {
        RadarModeInputState state = new RadarModeInputState();

        Assert.That(state.TryToggle(false, out bool active), Is.False);
        Assert.That(active, Is.False);
        Assert.That(state.TryToggle(true, out active), Is.True);
        Assert.That(active, Is.True);
        Assert.That(state.TryBeginInstantScan(false, 0f, 0.5f), Is.False);
    }

    [Test]
    public void RadarTutorial_RequiresActivationThenSuccessfulTargetScan()
    {
        TutorialRadarGuidanceProgress progress =
            new TutorialRadarGuidanceProgress();

        Assert.That(progress.TryRecordSuccessfulTargetScan(true), Is.False);
        progress.RecordRadarActiveChanged(false);
        Assert.That(progress.HasActivatedRadar, Is.False);
        progress.RecordRadarActiveChanged(true);
        Assert.That(progress.HasActivatedRadar, Is.True);
        Assert.That(
            progress.TryRecordSuccessfulTargetScan(false),
            Is.False,
            "A scan without the authoritative supply target must not complete the objective.");
        Assert.That(progress.TryRecordSuccessfulTargetScan(true), Is.True);
        Assert.That(progress.TryRecordSuccessfulTargetScan(true), Is.False);
    }

    [Test]
    public void TutorialSupplyRoute_IsOptionalAndBothPathsConvergeExactlyOnce()
    {
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                TutorialStep.RoutePing,
                TutorialSupplyProgressSource.RoutePlaced,
                out TutorialStep routeNext),
            Is.True);
        Assert.That(routeNext, Is.EqualTo(TutorialStep.TravelNormalSalvage));

        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                TutorialStep.RoutePing,
                TutorialSupplyProgressSource.TargetApproached,
                out TutorialStep noRouteNext),
            Is.True);
        Assert.That(noRouteNext, Is.EqualTo(routeNext));
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                noRouteNext,
                TutorialSupplyProgressSource.TargetApproached,
                out TutorialStep destructionStage),
            Is.True);
        Assert.That(
            destructionStage,
            Is.EqualTo(TutorialStep.DestroyNormalSalvage));
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                destructionStage,
                TutorialSupplyProgressSource.TargetDestroyed,
                out TutorialStep collectionStage),
            Is.True);
        Assert.That(collectionStage, Is.EqualTo(TutorialStep.CollectResources));
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                collectionStage,
                TutorialSupplyProgressSource.TargetDestroyed,
                out _),
            Is.False,
            "Repeated route or destruction notifications must not advance twice.");
    }

    [Test]
    public void TutorialSupplyTarget_IsDamageableAndCompletableWithoutOpeningMap()
    {
        Assert.That(
            TutorialSupplyProgression.IsTargetDamageEnabled(TutorialStep.Map),
            Is.True,
            "Natural supply-dialogue completion enters Map; the box must already be damageable there.");
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                TutorialStep.Map,
                TutorialSupplyProgressSource.TargetDestroyed,
                out TutorialStep noMapNext),
            Is.True);
        Assert.That(noMapNext, Is.EqualTo(TutorialStep.CollectResources));

        Assert.That(
            TutorialSupplyProgression.IsTargetDamageEnabled(TutorialStep.RoutePing),
            Is.True);
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                TutorialStep.RoutePing,
                TutorialSupplyProgressSource.TargetDestroyed,
                out TutorialStep routeNext),
            Is.True);
        Assert.That(routeNext, Is.EqualTo(noMapNext));
        Assert.That(
            TutorialSupplyProgression.TryResolveNextStep(
                noMapNext,
                TutorialSupplyProgressSource.TargetDestroyed,
                out _),
            Is.False,
            "Both routes must converge on one exact-once completion boundary.");
    }

    [Test]
    public void TutorialRadar_RemainsOpenForFocusAndOrdinaryCombat()
    {
        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        MonoScript radarScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        MonoScript frigateScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Boss/FrigateTriadBossController.cs");
        Assert.That(tutorialScript, Is.Not.Null);
        Assert.That(radarScript, Is.Not.Null);
        Assert.That(frigateScript, Is.Not.Null);

        string inputLockBody = ExtractMethodBody(
            tutorialScript.text,
            "private void SetTargetPresentationInputLocked(bool locked)");
        Assert.That(inputLockBody, Does.Not.Contain("CloseRadar"));
        Assert.That(inputLockBody, Does.Contain("SetExternalInputLocked(this, locked)"));
        Assert.That(radarScript.text, Does.Not.Contain("CombatActivityRegistered"));
        Assert.That(radarScript.text, Does.Not.Contain("HandleCombatActivityRegistered"));
        Assert.That(
            frigateScript.text,
            Does.Contain("bossFightRadarPanel?.SetPresentationSuppressed(this, true)"),
            "Removing ordinary-combat close must not remove encounter-owned Radar suppression.");
    }

    [Test]
    public void TutorialSignalDevice_PositionPersistenceAndPulseAreScoped()
    {
        string sceneText = File.ReadAllText("Assets/01_Scenes/Tutorial.unity");
        Assert.That(
            sceneText,
            Does.Contain("m_LocalPosition: {x: -8.9, y: 15.33, z: 0}"));

        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(tutorialScript, Is.Not.Null);
        Assert.That(
            tutorialScript.text,
            Does.Contain("bool relayVisible = currentStep >= TutorialStep.FindSignalDevice;"));

        string interactionBody = ExtractMethodBody(
            tutorialScript.text,
            "private void HandlePlayerInteracted(IInteractable target)");
        Assert.That(interactionBody, Does.Contain("TryStartRelayNarrative();"));
        Assert.That(
            interactionBody,
            Does.Not.Contain("TryAdvanceCheckpoint(TutorialStep.InteractSignalDevice)"));

        string pulseBody = ExtractMethodBody(
            tutorialScript.text,
            "private void PlaySignalDevicePulse()");
        Assert.That(pulseBody, Does.Contain("StopSignalDevicePulse();"));
        Assert.That(pulseBody, Does.Contain("PlayStationaryTutorialPurpleEffect("));
        Assert.That(pulseBody, Does.Contain("ResolveRendererWorldCenter("));
        Assert.That(pulseBody, Does.Not.Contain("new GameObject"));

        string sharedPurpleEffectBody = ExtractMethodBody(
            tutorialScript.text,
            "private Sequence PlayStationaryTutorialPurpleEffect(");
        Assert.That(sharedPurpleEffectBody, Does.Contain(".SetUpdate(true)"));
        Assert.That(sharedPurpleEffectBody, Does.Contain("tutorialPurpleEffectTween = sequence"));
        Assert.That(sharedPurpleEffectBody, Does.Contain("ReleaseTutorialPurpleEffect(sequence)"));

        string purpleSceneText = File.ReadAllText("Assets/01_Scenes/Tutorial.unity");
        Assert.That(
            purpleSceneText,
            Does.Contain(
                "tutorialPurpleEffectPrefab: {fileID: 708775973121913217, guid: 75beeaa1a3c92eb46bb073047c8b788d, type: 3}"));
        GameObject purplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/Purple.prefab");
        Assert.That(purplePrefab, Is.Not.Null);
        Assert.That(purplePrefab.GetComponentInChildren<SpriteRenderer>(true), Is.Not.Null);

        GameObject device = new GameObject("Signal device lifecycle test");
        createdObjects.Add(device);
        TutorialInteractionTarget target =
            device.AddComponent<TutorialInteractionTarget>();
        target.SetInteractionEnabled(true);
        Assert.That(target.CanInteract(null), Is.True);
        target.Interact(null);
        Assert.That(device.activeSelf, Is.True);
        Assert.That(target.IsActivated, Is.True);
        Assert.That(target.CanInteract(null), Is.False);
        target.Interact(null);
        Assert.That(target.IsActivated, Is.True);
    }

    [Test]
    public void TutorialCurse_VisibleTransferPrecedesFadeAndCleanupAcceptsDestroyedVisualOwner()
    {
        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(tutorialScript, Is.Not.Null);

        string infiltrationBody = ExtractMethodBody(
            tutorialScript.text,
            "private Sequence BeginAlienSignalInfiltration()");
        Assert.That(infiltrationBody, Does.Contain("TryAcquireTutorialPurpleEffect("));
        Assert.That(infiltrationBody, Does.Contain("coreTravelTransform.DOMove(playerTarget"));
        Assert.That(infiltrationBody, Does.Contain("corruptionRenderer.DOFade(0f"));
        Assert.That(infiltrationBody, Does.Contain("curseTransferImpactScale"));
        Assert.That(infiltrationBody, Does.Contain(".SetUpdate(true)"));
        Assert.That(infiltrationBody, Does.Contain("SetAlienSignalCollidersEnabled(false)"));
        Assert.That(infiltrationBody, Does.Contain("RestoreAlienSignalCoreTravelTransform(false)"));
        Assert.That(infiltrationBody, Does.Not.Contain("transferTransform.DOMove"));
        Assert.That(infiltrationBody, Does.Not.Contain("alienSignalVisualRoot.DOMove"));
        Assert.That(infiltrationBody, Does.Not.Contain("new GameObject"));

        string stopInfiltrationBody = ExtractMethodBody(
            tutorialScript.text,
            "private void StopCurseInfiltration(bool restoreCoreVisual)");
        Assert.That(stopInfiltrationBody, Does.Contain("curseInfiltrationTween?.Kill()"));
        Assert.That(stopInfiltrationBody, Does.Contain("RestoreAlienSignalVisualState()"));
        Assert.That(stopInfiltrationBody, Does.Contain("SetAlienSignalCollidersEnabled(true)"));

        string continuationBody = ExtractMethodBody(
            tutorialScript.text,
            "private bool CanContinueCurseTransformation()");
        Assert.That(continuationBody, Does.Contain("playerCurseImpactVisual != null"));
        Assert.That(continuationBody, Does.Contain("alienSignalVisualRoot != null"));

        string cleanupBody = ExtractMethodBody(
            tutorialScript.text,
            "private void RefreshPlayerVisualState(bool playCurseGlitch)");
        Assert.That(cleanupBody, Does.Contain("if (playerVisualStateController == null)"));
        Assert.That(cleanupBody, Does.Not.Contain("playerVisualStateController?."));
        Assert.That(
            tutorialScript.text,
            Does.Not.Contain("playerVisualStateController?.RefreshVisualState()"));

        int infiltrationStart = tutorialScript.text.IndexOf(
            "Sequence infiltration = BeginAlienSignalInfiltration();",
            StringComparison.Ordinal);
        int impactStart = tutorialScript.text.IndexOf(
            "corePresentationProgress.TryBeginImpact()",
            infiltrationStart,
            StringComparison.Ordinal);
        int curseStart = tutorialScript.text.IndexOf(
            "corePresentationProgress.TryApplyCurse()",
            impactStart,
            StringComparison.Ordinal);
        Assert.That(infiltrationStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(impactStart, Is.GreaterThan(infiltrationStart));
        Assert.That(curseStart, Is.GreaterThan(impactStart));

        int fadeStart = tutorialScript.text.IndexOf(
            "yield return screenFader.FadeIn(",
            impactStart,
            StringComparison.Ordinal);
        int backgroundTransitionStart = tutorialScript.text.IndexOf(
            "RunTraitAcquisitionService.TryAcquirePersistentStoryTrait(",
            fadeStart,
            StringComparison.Ordinal);
        int cursedVisualStart = tutorialScript.text.IndexOf(
            "RefreshPlayerVisualState(true);",
            backgroundTransitionStart,
            StringComparison.Ordinal);
        Assert.That(fadeStart, Is.GreaterThan(impactStart));
        Assert.That(curseStart, Is.GreaterThan(fadeStart));
        Assert.That(backgroundTransitionStart, Is.GreaterThan(curseStart));
        Assert.That(cursedVisualStart, Is.GreaterThan(backgroundTransitionStart));
    }

    [Test]
    public void TutorialTargetCameraIntegration_IsSharedOwnerScopedUnscaledAndLifecycleSafe()
    {
        MonoScript cameraScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Core/GungeonStyleCamera2D.cs");
        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(cameraScript, Is.Not.Null);
        Assert.That(tutorialScript, Is.Not.Null);

        string cameraSource = cameraScript.text;
        string tutorialSource = tutorialScript.text;
        Assert.That(cameraSource, Does.Contain("TryBeginOwnedCinematicFocusBlend"));
        Assert.That(cameraSource, Does.Contain("ReleaseOwnedCinematicFocus"));
        Assert.That(
            cameraSource,
            Does.Contain("cinematicFocusBlendElapsed += unscaledDeltaTime"),
            "Focus and return blends must continue while dialogue pauses scaled time.");
        Assert.That(tutorialSource, Does.Contain("TargetDiscovered += HandleMapTargetDiscovered"));
        Assert.That(tutorialSource, Does.Contain("RequestTargetPresentation("));
        Assert.That(tutorialSource, Does.Contain("WaitingForSafeViewport"));
        Assert.That(tutorialSource, Does.Contain("WorldToViewportPoint"));
        Assert.That(tutorialSource, Does.Contain("activePresentationCollider.bounds"));
        Assert.That(tutorialSource, Does.Not.Contain("Renderer.isVisible"));
        Assert.That(tutorialSource, Does.Contain("TutorialTargetPresentationKind.SupplyContainer"));
        Assert.That(tutorialSource, Does.Contain("TutorialTargetPresentationKind.HighValueWreck"));
        Assert.That(tutorialSource, Does.Contain("StopTargetPresentation(true, true)"));
        Assert.That(tutorialSource, Does.Contain("SetExternalControlLocked(this, locked)"));
        Assert.That(tutorialSource, Does.Contain("SetExternalInputLocked(this, locked)"));
    }

    [Test]
    public void TutorialCurse_CinematicUsesOwnedCameraLetterboxFadeAndIdempotentCleanup()
    {
        MonoScript tutorialScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        Assert.That(tutorialScript, Is.Not.Null);

        string source = tutorialScript.text;
        string routine = ExtractMethodBody(
            source,
            "private IEnumerator CurseTransformationRoutine()");
        Assert.That(routine, Does.Contain("BossCinematicLetterboxUI.GetOrCreate()"));
        Assert.That(routine, Does.Contain("TryAcquireCurseCameraPresentation()"));
        Assert.That(routine, Does.Contain("WaitForSecondsRealtime"));
        Assert.That(routine, Does.Contain("screenFader.FadeIn("));
        Assert.That(routine, Does.Contain("screenFader.FadeOut("));
        Assert.That(routine, Does.Contain("RunTraitAcquisitionService.TryAcquirePersistentStoryTrait("));

        string cameraAcquire = ExtractMethodBody(
            source,
            "private bool TryAcquireCurseCameraPresentation()");
        Assert.That(cameraAcquire, Does.Contain("AcquireGameplayFramingProfile("));
        Assert.That(cameraAcquire, Does.Contain("TryBeginOwnedCinematicFocusBlend("));
        Assert.That(cameraAcquire, Does.Contain("curseCameraZoomMultiplier"));

        string abort = ExtractMethodBody(
            source,
            "private void AbortCurseTransformationFromRoutine()");
        Assert.That(abort, Does.Contain("ReleaseCurseCameraPresentation(true)"));
        Assert.That(abort, Does.Contain("ClearCurseLetterbox()"));
        Assert.That(abort, Does.Contain("SetTargetPresentationInputLocked(false)"));
        Assert.That(abort, Does.Contain("ClearTransformationScreenFade()"));

        string stop = ExtractMethodBody(
            source,
            "private void StopCurseTransformation()");
        Assert.That(stop, Does.Contain("ReleaseCurseCameraPresentation(true)"));
        Assert.That(stop, Does.Contain("ClearCurseLetterbox()"));
        Assert.That(stop, Does.Contain("SetTargetPresentationInputLocked(false)"));
    }

    [Test]
    public void RadarInputAsset_UsesQToggleAndMouseBackInstantScan()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/04_Input/PlayerControls.inputactions");
        Assert.That(actions, Is.Not.Null);

        InputAction radar = actions.FindAction("Player/Radar", true);
        InputAction instantScan = actions.FindAction("Player/RadarQuickScan", true);
        Assert.That(radar.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
        Assert.That(
            instantScan.bindings[0].effectivePath,
            Is.EqualTo("<Mouse>/backButton"));

        MonoScript scannerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        MonoScript vfxScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/UI/PlayerRadarVFXController.cs");
        Assert.That(scannerScript.text, Does.Contain("WasPressedThisFrame()"));
        Assert.That(scannerScript.text, Does.Not.Contain("WasReleasedThisFrame()"));
        Assert.That(scannerScript.text, Does.Not.Contain("IsPressed()"));
        Assert.That(scannerScript.text, Does.Not.Contain("holdTimer"));
        Assert.That(scannerScript.text, Does.Contain("externalInputLocks.Count == 0"));
        Assert.That(scannerScript.text, Does.Contain("!GameplayPauseManager.IsPaused"));
        Assert.That(vfxScript.text, Does.Not.Contain("PlayerChargeGaugeUI"));
        Assert.That(vfxScript.text, Does.Not.Contain("BeginCharge"));
    }

    [Test]
    public void ThreeDistinctBossParts_AreReadyToRestoreUntilExplicitRecovery()
    {
        PermanentProgress progress = CreateProgress("AccessKeyRecoveryProgress");
        Assert.That(progress.TryStartDamagedAccessKeyQuest(), Is.True);

        RegisterAllRequiredBossParts(progress);

        Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(3));
        Assert.That(
            progress.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.ReadyToRestore));

        SaveData readySave = progress.CreateSaveData();
        progress.ResetProgress();
        progress.LoadFromSave(readySave);
        Assert.That(
            progress.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.ReadyToRestore),
            "Save reload must preserve the explicit Settlement Recovery boundary.");

        Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True);
        Assert.That(
            progress.DamagedAccessKeyQuestState,
            Is.EqualTo(MainDamagedAccessKeyQuestState.Completed));
        Assert.That(
            progress.TryRestoreDamagedAccessKey(),
            Is.False,
            "Repeated Settlement Recovery confirmation must be idempotent.");
    }

    [Test]
    public void PixelCurseLevel_DerivesFromDistinctPartsAndSurvivesSaveReload()
    {
        PermanentProgress progress = CreateProgress("PixelCurseProgress");
        TraitDefinition pixelCurse = AssetDatabase.LoadAssetAtPath<TraitDefinition>(
            "Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset");
        Assert.That(pixelCurse, Is.Not.Null);

        List<string> levelTransitions = new List<string>();
        progress.PixelCurseLevelChanged += (previous, current) =>
            levelTransitions.Add($"{previous}->{current}");

        Assert.That(progress.PixelCurseLevel, Is.Zero);
        Assert.That(progress.TryAcquirePersistentStoryTrait(pixelCurse), Is.True);
        Assert.That(progress.PixelCurseLevel, Is.EqualTo(1));
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator),
            Is.True);
        Assert.That(progress.PixelCurseLevel, Is.EqualTo(2));
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator),
            Is.False,
            "A duplicate boss reward must not add another Curse level.");
        Assert.That(progress.PixelCurseLevel, Is.EqualTo(2));
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer),
            Is.True);
        Assert.That(progress.PixelCurseLevel, Is.EqualTo(3));
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper),
            Is.True);
        Assert.That(progress.PixelCurseLevel, Is.EqualTo(4));
        Assert.That(
            levelTransitions,
            Is.EqualTo(new[] { "0->1", "1->2", "2->3", "3->4" }));

        SaveData saveData = progress.CreateSaveData();
        PermanentProgress reloaded = CreateProgress("ReloadedPixelCurseProgress");
        reloaded.LoadFromSave(saveData);

        Assert.That(reloaded.PixelCurseLevel, Is.EqualTo(4));
        Assert.That(reloaded.AcquiredBossStoryPartCount, Is.EqualTo(3));
    }

    [Test]
    public void Phase2CGraphs_AreDeterministicTypedAndActionFree()
    {
        LoadContent(out DialogueDatabase database, out LocalizationCatalog catalog);

        bool built = Phase2CStoryDialogueInstaller.TryBuildEntries(
            database,
            catalog,
            out Dictionary<string, List<DialogueEntry>> graphs,
            out _,
            out _,
            out string result);

        Assert.That(built, Is.True, result);
        Assert.That(graphs.Count, Is.EqualTo(9));
        AssertSingleLineGuidanceGraph(
            graphs,
            Phase2CStoryDialogueIds.TutorialOpeningConversation,
            Phase2CStoryDialogueIds.TutorialMovementTextKey);
        AssertSingleLineGuidanceGraph(
            graphs,
            Phase2CStoryDialogueIds.TutorialRadarConversation,
            Phase2CStoryDialogueIds.TutorialRadarTextKey);
        AssertSingleLineGuidanceGraph(
            graphs,
            Phase2CStoryDialogueIds.TutorialSupplyConversation,
            Phase2CStoryDialogueIds.TutorialSupplyTextKey);
        AssertSingleLineGuidanceGraph(
            graphs,
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
            Phase2CStoryDialogueIds.TutorialAncientSignalTextKey);
        AssertSingleLineGuidanceGraph(
            graphs,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
            Phase2CStoryDialogueIds.GetNumberedTextKey(
                Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverLinePrefix,
                1));
        Assert.That(
            graphs[Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation].Count,
            Is.EqualTo(4));
        Assert.That(
            graphs[Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation][1].ActorID,
            Is.Not.EqualTo(
                graphs[Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation][1].ActorID),
            "FakeOperator must remain a distinct database actor despite the concealed display name.");

        bool rebuilt = Phase2CStoryDialogueInstaller.TryBuildEntries(
            database,
            catalog,
            out Dictionary<string, List<DialogueEntry>> rebuiltGraphs,
            out _,
            out _,
            out string rebuildResult);
        Assert.That(rebuilt, Is.True, rebuildResult);

        foreach (KeyValuePair<string, List<DialogueEntry>> pair in graphs)
        {
            Assert.That(rebuiltGraphs.ContainsKey(pair.Key), Is.True, pair.Key);
            Assert.That(
                Phase2CStoryDialogueInstaller.BuildGraphSignature(
                    rebuiltGraphs[pair.Key]),
                Is.EqualTo(
                    Phase2CStoryDialogueInstaller.BuildGraphSignature(pair.Value)),
                pair.Key);

            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < pair.Value.Count; i++)
            {
                DialogueEntry entry = pair.Value[i];
                Assert.That(ids.Add(entry.id), Is.True, pair.Key);
                Assert.That(
                    ContainsUnsafeDirectGameplayAction(entry.userScript),
                    Is.False,
                    pair.Key);
            }

            Assert.That(
                pair.Value[pair.Value.Count - 1].userScript,
                Does.Contain(DialoguePixelCrushersBridge.CompletionLuaFunction),
                pair.Key);
        }

        List<DialogueEntry> settlement = graphs[
            Phase2CStoryDialogueIds.FirstSettlementConversation];
        Assert.That(
            settlement[1].conditionsString,
            Does.Contain(DialogueConditionId.MainDamagedAccessKeyNotStarted.ToString()));
        Assert.That(
            settlement.Find(entry => entry.id == 7).conditionsString,
            Does.Contain(DialogueConditionId.MainDamagedAccessKeyActive0.ToString()));
        Assert.That(
            settlement.Find(entry => entry.id == 11).conditionsString,
            Does.Contain(DialogueConditionId.MainDamagedAccessKeyCompleted.ToString()));
        Assert.That(
            settlement.Find(entry => entry.id == 10).conditionsString,
            Does.Contain(DialogueConditionId.MainDamagedAccessKeyReadyToRestore.ToString()));

        Assert.That(
            ContainsUnsafeDirectGameplayAction(
                $"{DialoguePixelCrushersBridge.ActionLuaFunction}(\"unsafe\")"),
            Is.True,
            "The scalar script guard must detect an unsafe direct gameplay action.");

        string stableSignature =
            Phase2CStoryDialogueInstaller.BuildGraphSignature(settlement);
        int originalEntryId = settlement[1].id;
        settlement[1].id = originalEntryId + 1000;
        Assert.That(
            Phase2CStoryDialogueInstaller.BuildGraphSignature(settlement),
            Is.Not.EqualTo(stableSignature),
            "The deterministic signature must detect a changed graph shape.");
        settlement[1].id = originalEntryId;
    }

    [Test]
    public void Phase2CInstaller_IsIdempotentOnAnInMemoryDatabaseCopy()
    {
        LoadContent(out DialogueDatabase sourceDatabase, out LocalizationCatalog catalog);
        DialogueDatabase database = UnityEngine.Object.Instantiate(sourceDatabase);
        createdObjects.Add(database);

        bool firstInstall = Phase2CStoryDialogueInstaller.TryInstall(
            database,
            catalog,
            false,
            out string firstResult);
        string firstSignature = BuildPhase2CSignature(database);
        int firstActorCount = database.actors.Count;
        int firstConversationCount = database.conversations.Count;

        bool secondInstall = Phase2CStoryDialogueInstaller.TryInstall(
            database,
            catalog,
            false,
            out string secondResult);

        Assert.That(firstInstall, Is.True, firstResult);
        Assert.That(secondInstall, Is.True, secondResult);
        Assert.That(BuildPhase2CSignature(database), Is.EqualTo(firstSignature));
        Assert.That(database.actors.Count, Is.EqualTo(firstActorCount));
        Assert.That(database.conversations.Count, Is.EqualTo(firstConversationCount));
        Assert.That(database.conversations.FindAll(conversation =>
            conversation.Title == Phase2CStoryDialogueIds.FirstSettlementConversation).Count, Is.EqualTo(1));
        Assert.That(Phase2CStoryDialogueInstaller.TryValidateInstalled(database, catalog, out string validation),
            Is.True, validation);
    }

    [TestCase(MainDamagedAccessKeyQuestState.NotStarted, 1, 8)]
    [TestCase(MainDamagedAccessKeyQuestState.Active0, 7, 2)]
    [TestCase(MainDamagedAccessKeyQuestState.Active1, 8, 2)]
    [TestCase(MainDamagedAccessKeyQuestState.Active2, 9, 2)]
    [TestCase(MainDamagedAccessKeyQuestState.ReadyToRestore, 10, 2)]
    [TestCase(MainDamagedAccessKeyQuestState.Completed, 11, 2)]
    public void SettlementBranches_SelectOnceAndConvergeWithoutGameplayActions(
        MainDamagedAccessKeyQuestState state, int entranceId, int lineCount)
    {
        LoadContent(out DialogueDatabase database, out LocalizationCatalog catalog);
        Assert.That(Phase2CStoryDialogueInstaller.TryBuildEntries(database, catalog, out var graphs,
            out int operatorId, out _, out string result), Is.True, result);
        List<DialogueEntry> entries = graphs[Phase2CStoryDialogueIds.FirstSettlementConversation];
        Assert.That(entries.Count, Is.EqualTo(20));
        DialogueEntry root = entries.Find(entry => entry.id == 0);
        DialogueEntry terminal = entries.Find(entry => entry.id == 12);
        Assert.That(root.outgoingLinks.Count, Is.EqualTo(6));
        Assert.That(terminal.outgoingLinks, Is.Empty);
        Assert.That(terminal.userScript, Is.EqualTo(
            $"{DialoguePixelCrushersBridge.CompletionLuaFunction}(\"{Phase2CStoryDialogueIds.FirstSettlementConversation}\")"));
        var authority = new FakeMainQuestAuthority(state);
        var registry = new DialogueConditionRegistry();
        DialogueEntry selected = null;
        int matchingBranches = 0;
        foreach (Link link in root.outgoingLinks)
        {
            DialogueEntry candidate = entries.Find(entry => entry.id == link.destinationDialogueID);
            string conditionId = candidate.conditionsString.Split('"')[1];
            Assert.That(registry.TryEvaluate(conditionId, null, authority, out bool matches), Is.True);
            if (matches)
            {
                matchingBranches++;
                selected = candidate;
            }
        }
        Assert.That(matchingBranches, Is.EqualTo(1));
        Assert.That(selected.id, Is.EqualTo(entranceId));
        int settlementId = database.GetConversation(Phase2CStoryDialogueIds.FirstSettlementConversation).ConversantID;
        int[] initialIds = { 1, 2, 3, 4, 5, 13, 14, 6 };
        int[] initialLines = { 1, 2, 3, 4, 5, 7, 8, 6 };
        for (int i = 0; i < lineCount; i++)
        {
            Assert.That(selected.id, Is.Not.EqualTo(terminal.id), "Branch ended too early.");
            Assert.That(selected.userScript, Is.Null.Or.Empty, "Only END may notify completion.");
            if (i > 0) Assert.That(selected.conditionsString, Is.Null.Or.Empty);
            if (state == MainDamagedAccessKeyQuestState.NotStarted)
            {
                Assert.That(selected.id, Is.EqualTo(initialIds[i]));
                Assert.That(Field.LookupValue(selected.fields, "TextKey"), Is.EqualTo(
                    Phase2CStoryDialogueIds.GetNumberedTextKey(Phase2CStoryDialogueIds.FirstSettlementLinePrefix, initialLines[i])));
                Assert.That(selected.ActorID, Is.EqualTo(i < 3 || i == 5 ? settlementId : operatorId));
            }
            else
            {
                Assert.That(selected.id, Is.EqualTo(i == 0 ? entranceId : entranceId + 8));
                bool settlementSpeaks = state == MainDamagedAccessKeyQuestState.ReadyToRestore ? i == 0 : i == 1;
                Assert.That(selected.ActorID, Is.EqualTo(settlementSpeaks ? settlementId : operatorId));
            }
            Assert.That(selected.outgoingLinks.Count, Is.EqualTo(1));
            selected = entries.Find(entry => entry.id == selected.outgoingLinks[0].destinationDialogueID);
        }
        Assert.That(selected, Is.SameAs(terminal));
        Assert.That(authority.CompletionRequestCount, Is.Zero);
        Assert.That(authority.QuestStartCount, Is.Zero);
        Assert.That(authority.DamagedAccessKeyQuestState, Is.EqualTo(state));
    }

    [Test]
    public void SettlementNarrative_ProjectsUniquePartsWithoutChangingCurseOrRestoration()
    {
        PermanentProgress progress = CreateProgress("SettlementNarrativeReadOnlyProgress");
        Assert.That(progress.TryStartDamagedAccessKeyQuest(), Is.True);
        TraitDefinition curse = AssetDatabase.LoadAssetAtPath<TraitDefinition>(
            "Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset");
        Assert.That(progress.TryAcquirePersistentStoryTrait(curse), Is.True);
        var registry = new DialogueConditionRegistry();
        CampaignBossId[] bosses = { CampaignBossId.SectorAdministrator, CampaignBossId.SalvageDevourer, CampaignBossId.PhaseGatekeeper };
        DialogueConditionId[] conditions = { DialogueConditionId.MainDamagedAccessKeyActive0,
            DialogueConditionId.MainDamagedAccessKeyActive1, DialogueConditionId.MainDamagedAccessKeyActive2,
            DialogueConditionId.MainDamagedAccessKeyReadyToRestore, DialogueConditionId.MainDamagedAccessKeyCompleted };
        for (int stage = 0; stage <= 4; stage++)
        {
            if (stage > 0 && stage < 4)
            {
                Assert.That(progress.RegisterCampaignBossDefeat(bosses[stage - 1]), Is.True);
                Assert.That(progress.RegisterCampaignBossDefeat(bosses[stage - 1]), Is.False);
            }
            if (stage == 4)
            {
                Assert.That(progress.DamagedAccessKeyQuestState, Is.EqualTo(MainDamagedAccessKeyQuestState.ReadyToRestore));
                Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True, "Only explicit recovery changes Ready to Completed.");
            }
            string before = JsonUtility.ToJson(progress.CreateSaveData());
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; i < conditions.Length; i++)
                {
                    Assert.That(registry.TryEvaluate(conditions[i], null, progress, out bool matches), Is.True);
                    Assert.That(matches, Is.EqualTo(i == stage));
                }
            }
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
            Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(Math.Min(stage, 3)));
            Assert.That(progress.PixelCurseLevel, Is.EqualTo(Math.Min(stage + 1, 4)));
        }
    }

    [Test]
    public void SettlementNewLines_AreRequiredByInstallerAndPresentInCsv()
    {
        LoadContent(out DialogueDatabase database, out LocalizationCatalog catalog);
        Assert.That(LocalizationContentImporter.TryReadUtf8File(LocalizationContentImporter.DefaultSourceAssetPath,
            out string csv, out string readError), Is.True, readError);
        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            LocalizationContentImporter.DefaultSourceAssetPath, csv);
        Assert.That(report.HasErrors, Is.False);
        string[] newKeys = { Phase2CStoryDialogueIds.FirstSettlementLinePrefix + "07",
            Phase2CStoryDialogueIds.FirstSettlementLinePrefix + "08",
            Phase2CStoryDialogueIds.MainQuestActive0ResponseTextKey, Phase2CStoryDialogueIds.MainQuestActive1ResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestActive2ResponseTextKey, Phase2CStoryDialogueIds.MainQuestReadyResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestCompletedResponseTextKey };
        foreach (string missingKey in newKeys)
        {
            var entries = new List<LocalizationEntry>();
            bool found = false;
            foreach (LocalizationCsvRecord record in report.Records)
            {
                if (record.TextKey != missingKey) entries.Add(record.ToCatalogEntry());
                else found = true;
            }
            Assert.That(found, Is.True, missingKey);
            LocalizationCatalog incomplete = ScriptableObject.CreateInstance<LocalizationCatalog>();
            createdObjects.Add(incomplete);
            incomplete.SetGeneratedContentForEditor(entries, LocalizationContentImporter.DefaultSourceAssetPath, report.ContentHash);
            Assert.That(Phase2CStoryDialogueInstaller.TryBuildEntries(database, incomplete, out _, out _, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(missingKey));
            Assert.That(catalog.TryGetEntry(missingKey, out LocalizationEntry imported), Is.True, "Import Catalog first: " + missingKey);
            Assert.That(imported.Korean, Is.Not.Empty);
        }
    }

    [Test]
    public void Phase2CLocalizationKeys_ExistWithKoreanSource()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);
        Assert.That(catalog, Is.Not.Null);

        string[] keys = BuildPhase2CLocalizationKeys();
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.That(
                catalog.TryGetEntry(keys[i], out LocalizationEntry entry),
                Is.True,
                keys[i]);
            Assert.That(entry.Korean, Is.Not.Null.And.Not.Empty, keys[i]);
        }
    }

    [Test]
    public void Phase2CTutorialLocalization_UsesAutomaticWrapping()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);
        string[] tutorialKeys =
        {
            Phase2CStoryDialogueIds.TutorialMovementTextKey,
            Phase2CStoryDialogueIds.TutorialRadarTextKey,
            Phase2CStoryDialogueIds.TutorialSupplyTextKey,
            Phase2CStoryDialogueIds.TutorialAncientSignalTextKey
        };

        for (int i = 0; i < tutorialKeys.Length; i++)
        {
            Assert.That(catalog.TryGetEntry(tutorialKeys[i], out LocalizationEntry entry), Is.True);
            Assert.That(entry.Korean, Does.Not.Contain("\r"), tutorialKeys[i]);
            Assert.That(entry.Korean, Does.Not.Contain("\n"), tutorialKeys[i]);
        }
    }

    [Test]
    public void MandatoryStoryPending_BlocksLaunchUntilCompletionFlagExists()
    {
        bool blocked = SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            false,
            true,
            out SettlementExpeditionLaunchFailure blockedFailure);
        bool allowed = SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            false,
            false,
            out SettlementExpeditionLaunchFailure allowedFailure);

        Assert.That(blocked, Is.False);
        Assert.That(
            blockedFailure,
            Is.EqualTo(SettlementExpeditionLaunchFailure.MandatoryStoryPending));
        Assert.That(allowed, Is.True);
        Assert.That(allowedFailure, Is.EqualTo(SettlementExpeditionLaunchFailure.None));
    }

    [Test]
    public void NullDispatcher_InstallerIsIdempotentAndKeepsExactlyTwoTypedResponses()
    {
        LoadContent(out var source, out _);
        var database = UnityEngine.Object.Instantiate(source);
        createdObjects.Add(database);
        string oldStories = BuildPhase2CSignature(database);
        Assert.That(LocalizationContentImporter.TryReadUtf8File(LocalizationContentImporter.DefaultSourceAssetPath,
            out string csv, out string error), Is.True, error);
        var report = LocalizationContentValidator.ValidateCsv(LocalizationContentImporter.DefaultSourceAssetPath, csv);
        Assert.That(report.HasErrors, Is.False);
        var entries = new List<LocalizationEntry>();
        foreach (var row in report.Records) entries.Add(row.ToCatalogEntry());
        var catalog = ScriptableObject.CreateInstance<LocalizationCatalog>();
        createdObjects.Add(catalog);
        catalog.SetGeneratedContentForEditor(entries, LocalizationContentImporter.DefaultSourceAssetPath, report.ContentHash);
        Assert.That(NullDispatcherDialogueInstaller.TryInstall(database, catalog, false, out error), Is.True, error);
        int actors = database.actors.Count;
        int conversations = database.conversations.Count;
        var graph = database.GetConversation(NullDispatcherDialogueIds.Conversation);
        int stableId = graph.id;
        string signature = Phase2CStoryDialogueInstaller.BuildGraphSignature(graph.dialogueEntries);
        Assert.That(NullDispatcherDialogueInstaller.TryInstall(database, catalog, false, out error), Is.True, error);
        graph = database.GetConversation(NullDispatcherDialogueIds.Conversation);
        Assert.That(graph.id, Is.EqualTo(stableId));
        Assert.That(database.actors.Count, Is.EqualTo(actors));
        Assert.That(database.conversations.Count, Is.EqualTo(conversations));
        Assert.That(Phase2CStoryDialogueInstaller.BuildGraphSignature(graph.dialogueEntries), Is.EqualTo(signature));
        Assert.That(BuildPhase2CSignature(database), Is.EqualTo(oldStories), "Existing Settlement/defense story remains intact.");
        Assert.That(NullDispatcherDialogueInstaller.TryValidateInstalled(database, catalog, out error), Is.True, error);
        int responses = 0;
        foreach (var node in graph.dialogueEntries)
        {
            if (node.ActorID == graph.ActorID)
            {
                responses++;
                Assert.That(node.id, Is.EqualTo(7).Or.EqualTo(8));
                Assert.That(node.outgoingLinks.Count, Is.EqualTo(1));
                Assert.That(node.outgoingLinks[0].destinationDialogueID, Is.EqualTo(9));
            }
        }
        Assert.That(responses, Is.EqualTo(2));
        Assert.That(graph.GetDialogueEntry(7).userScript, Is.EqualTo("VS_DispatchAction(\"FinalBossTreatmentAccept\")"));
        Assert.That(graph.GetDialogueEntry(8).userScript, Is.EqualTo("VS_DispatchAction(\"FinalBossTreatmentReject\")"));
        Assert.That(graph.GetDialogueEntry(9).userScript, Is.EqualTo("VS_MarkConversationComplete(\"FINAL_NullDispatcherTreatmentOffer\")"));
        Assert.That(Field.LookupValue(database.GetActor(NullDispatcherDialogueIds.Actor).fields, "Name TextKey"),
            Is.EqualTo(NullDispatcherDialogueIds.SpeakerKey));
        graph.GetDialogueEntry(6).outgoingLinks.RemoveAt(1);
        Assert.That(NullDispatcherDialogueInstaller.TryValidateInstalled(database, catalog, out _), Is.False,
            "A graph with one missing response must fail validation.");
    }

    [Test]
    public void NullDispatcher_ActionsCannotDispatchOutsideTreatmentConversation()
    {
        var dispatcher = new DialogueGameplayActionDispatcher();
        var authority = new FinalTreatmentProbe();
        dispatcher.BeginConversation(Phase2CStoryDialogueIds.FirstSettlementConversation);
        Assert.That(dispatcher.TryDispatch(DialogueGameplayActionId.FinalBossTreatmentAccept, null, null, null, authority), Is.False);
        Assert.That(authority.Requests, Is.Zero);
        dispatcher.BeginConversation(NullDispatcherDialogueIds.Conversation);
        Assert.That(authority.Requests, Is.Zero, "Conversation start is read-only.");
        Assert.That(dispatcher.TryDispatch(DialogueGameplayActionId.FinalBossTreatmentReject, null, null, null, authority), Is.True);
        Assert.That(authority.Requests, Is.EqualTo(1));
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate action 'FinalBossTreatmentReject'"));
        Assert.That(dispatcher.TryDispatch(DialogueGameplayActionId.FinalBossTreatmentReject, null, null, null, authority), Is.False);
        Assert.That(authority.Requests, Is.EqualTo(1));
    }

    private sealed class FinalTreatmentProbe : IFinalBossTreatmentAuthority
    {
        public int Requests { get; private set; }
        public bool TryRequestTreatmentChoice(bool accept)
        {
            Requests++;
            return Requests == 1;
        }
    }

    [Test]
    public void NullDispatcher_BridgeRegistrationPreservesCurrentEncounterOwner()
    {
        var root = new GameObject("Final bridge registration fixture");
        root.SetActive(false);
        createdObjects.Add(root);
        var bridge = root.AddComponent<DialoguePixelCrushersBridge>();
        var first = new FinalTreatmentProbe();
        var other = new FinalTreatmentProbe();
        Assert.That(bridge.RegisterFinalBossAuthority(first), Is.True);
        Assert.That(bridge.RegisterFinalBossAuthority(first), Is.True);
        Assert.That(bridge.RegisterFinalBossAuthority(other), Is.False);
        bridge.UnregisterFinalBossAuthority(other);
        Assert.That(bridge.RegisterFinalBossAuthority(other), Is.False);
        bridge.UnregisterFinalBossAuthority(first);
        bridge.UnregisterFinalBossAuthority(first);
        Assert.That(bridge.RegisterFinalBossAuthority(other), Is.True);
        bridge.UnregisterFinalBossAuthority(other);
        Assert.That(first.Requests, Is.Zero);
        Assert.That(other.Requests, Is.Zero);
    }

    private static void LoadContent(
        out DialogueDatabase database,
        out LocalizationCatalog catalog)
    {
        database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(
            RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);
        Assert.That(database, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);
    }

    private static string BuildPhase2CSignature(DialogueDatabase database)
    {
        string[] titles =
        {
            Phase2CStoryDialogueIds.TutorialOpeningConversation,
            Phase2CStoryDialogueIds.TutorialRadarConversation,
            Phase2CStoryDialogueIds.TutorialSupplyConversation,
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
            Phase2CStoryDialogueIds.TutorialRescueConversation,
            Phase2CStoryDialogueIds.FirstSettlementConversation
        };
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < titles.Length; i++)
        {
            Conversation conversation = database.GetConversation(titles[i]);
            Assert.That(conversation, Is.Not.Null, titles[i]);
            builder.Append(titles[i]).Append(':')
                .Append(Phase2CStoryDialogueInstaller.BuildGraphSignature(
                    conversation.dialogueEntries))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string[] BuildPhase2CLocalizationKeys()
    {
        List<string> keys = new List<string>
        {
            Phase2CStoryDialogueIds.OperatorSpeakerTextKey,
            Phase2CStoryDialogueIds.FakeOperatorSpeakerTextKey,
            Phase2CStoryDialogueIds.SettlementControlSpeakerTextKey,
            Phase2CStoryDialogueIds.MainQuestActive0TextKey,
            Phase2CStoryDialogueIds.MainQuestActive1TextKey,
            Phase2CStoryDialogueIds.MainQuestActive2TextKey,
            Phase2CStoryDialogueIds.MainQuestReadyTextKey,
            Phase2CStoryDialogueIds.MainQuestCompletedTextKey,
            Phase2CStoryDialogueIds.MainQuestActive0ResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestActive1ResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestActive2ResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestReadyResponseTextKey,
            Phase2CStoryDialogueIds.MainQuestCompletedResponseTextKey,
            MainDamagedAccessKeyQuestIds.TitleTextKey,
            MainDamagedAccessKeyQuestIds.StartedNotificationTextKey,
            MainDamagedAccessKeyQuestIds.ObjectiveTextKey,
            MainDamagedAccessKeyQuestIds.RestorationDescriptionTextKey,
            MainDamagedAccessKeyQuestIds.RestorationReadyTextKey,
            MainDamagedAccessKeyQuestIds.RestorationCompletedTextKey,
            MainDamagedAccessKeyQuestIds.RestorationActionTextKey,
            MainDamagedAccessKeyQuestIds.RestoredNotificationTextKey,
            PixelCurseProgressionIds.LevelUpNotificationTextKey
        };

        keys.Add(Phase2CStoryDialogueIds.TutorialMovementTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRadarTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialSupplyTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialAncientSignalTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRadarObjectiveTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRouteOptionalTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialUnknownSignalTitleTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialUnknownSignalObjectiveTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialPurpleCoreTravelTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialPurpleCoreInteractTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialCurseErrorTitleTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialCurseErrorBodyTextKey);
        AddNumberedKeys(
            keys,
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisLinePrefix,
            2);
        AddNumberedKeys(
            keys,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverLinePrefix,
            1);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.TutorialUnknownLinePrefix, 2);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.TutorialRescueLinePrefix, 3);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.FirstSettlementLinePrefix, 8);
        return keys.ToArray();
    }

    private PermanentProgress CreateProgress(string objectName)
    {
        GameObject root = new GameObject(objectName);
        root.SetActive(false);
        createdObjects.Add(root);
        return root.AddComponent<PermanentProgress>();
    }

    [Test]
    public void CampaignSpine_FirstDefeatsRequireAnalysis_RepeatClearsAllowChaining()
    {
        PermanentProgress progress = CreateProgress("CampaignSpine");
        progress.TryStartDamagedAccessKeyQuest();
        TraitDefinition curse = AssetDatabase.LoadAssetAtPath<TraitDefinition>(
            "Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset");
        Assert.That(progress.TryAcquirePersistentStoryTrait(curse), Is.True);
        Assert.That(progress.HighestUnlockedDepth, Is.EqualTo(ExpeditionDepth.Normal));
        Assert.That(progress.IsDepthUnlocked(ExpeditionDepth.DeepZone1), Is.False);
        Assert.That(progress.IsDepthUnlocked(ExpeditionDepth.DeepZone2), Is.False);
        Assert.That(progress.IsDepthUnlocked(ExpeditionDepth.FinalNetwork), Is.False);

        CampaignBossId[] bosses = { CampaignBossId.SectorAdministrator, CampaignBossId.SalvageDevourer,
            CampaignBossId.PhaseGatekeeper };
        for (int i = 0; i < bosses.Length; i++)
        {
            ExpeditionDepth depth = (ExpeditionDepth)i;
            Assert.That(CampaignProgressionCatalog.ShouldUseRepeatBoss(depth, progress), Is.False);
            var firstRun = new RunContext(WeaponTreeType.MachineGun, depth);
            Assert.That(progress.RegisterCampaignBossDefeat(bosses[i]), Is.True);
            firstRun.MarkBossDefeated(bosses[i], true);
            Assert.That(progress.HasDefeatedCampaignBoss(bosses[i]), Is.True);
            Assert.That(progress.HasBossStoryPart(CampaignProgressionCatalog.GetStoryPart(bosses[i])), Is.True);
            Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(i + 1));
            Assert.That(progress.PixelCurseLevel, Is.EqualTo(i + 2));
            Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(firstRun, progress), Is.False);
            string saved = JsonUtility.ToJson(progress.CreateSaveData());
            Assert.That(progress.RegisterCampaignBossDefeat(bosses[i]), Is.False);
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(saved));

            PermanentProgress reloaded = CreateProgress("CampaignSpineReload" + i);
            reloaded.LoadFromSave(JsonUtility.FromJson<SaveData>(saved));
            Assert.That(reloaded.HighestUnlockedDepth, Is.EqualTo(depth));
            Assert.That(reloaded.AcquiredBossStoryPartCount, Is.EqualTo(i + 1));
            Assert.That(reloaded.HasDefeatedCampaignBoss(bosses[i]), Is.True);
            Assert.That(reloaded.PixelCurseLevel, Is.EqualTo(i + 2));

            if (i < 2)
            {
                Assert.That(progress.IsDepthUnlocked((ExpeditionDepth)(i + 1)), Is.False);
                Assert.That(progress.HasPendingCampaignRouteAnalysis, Is.True);
                Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.True);
                Assert.That(progress.HighestUnlockedDepth, Is.EqualTo((ExpeditionDepth)(i + 1)));
                Assert.That(progress.HasPendingCampaignRouteAnalysis, Is.False);
                string authorized = JsonUtility.ToJson(progress.CreateSaveData());
                int repeatedChanges = 0;
                Action countChange = () => repeatedChanges++;
                progress.Changed += countChange;
                Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.False);
                progress.Changed -= countChange;
                Assert.That(repeatedChanges, Is.Zero);
                Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(authorized));
                reloaded.LoadFromSave(JsonUtility.FromJson<SaveData>(authorized));
                Assert.That(reloaded.HighestUnlockedDepth, Is.EqualTo((ExpeditionDepth)(i + 1)));
                // Even a newly authorized route cannot turn this first clear into a portal exit.
                Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(firstRun, progress), Is.False);
                var repeatRun = new RunContext(WeaponTreeType.MachineGun, depth);
                Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(repeatRun, progress), Is.False);
                repeatRun.MarkBossDefeated(bosses[i]);
                Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(repeatRun, progress), Is.True);
                Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(repeatRun, null), Is.False);
            }
            else
            {
                Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.False);
                Assert.That(progress.DamagedAccessKeyQuestState, Is.EqualTo(MainDamagedAccessKeyQuestState.ReadyToRestore));
                Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.ReadyToAssemble));
                Assert.That(progress.IsDepthUnlocked(ExpeditionDepth.FinalNetwork), Is.False);
                Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True);
                Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.Assembled));
                Assert.That(progress.TryActivateRouteCore(), Is.True);
                Assert.That(progress.CanLaunchFinalExpedition, Is.False);
                progress.MarkSettlementDefenseCleared();
                Assert.That(progress.CanLaunchFinalExpedition, Is.True);
                Assert.That(CampaignProgressionCatalog.CanAdvanceToNextRegion(firstRun, progress), Is.False);
                reloaded.LoadFromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(progress.CreateSaveData())));
                Assert.That(reloaded.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.Activated));
                Assert.That(reloaded.SettlementDefenseCleared, Is.True);
                Assert.That(reloaded.CanLaunchFinalExpedition, Is.True);
            }
            Assert.That(CampaignProgressionCatalog.ShouldUseRepeatBoss(depth, progress), Is.True);
        }
        Assert.That(CampaignProgressionCatalog.ShouldUseRepeatBoss(ExpeditionDepth.FinalNetwork, progress), Is.False);
    }

    [TestCase(ExpeditionDepth.DeepZone1, false, false, false)]
    [TestCase(ExpeditionDepth.DeepZone1, true, false, true)]
    [TestCase(ExpeditionDepth.DeepZone1, true, true, false)]
    [TestCase(ExpeditionDepth.DeepZone2, false, false, false)]
    [TestCase(ExpeditionDepth.DeepZone2, true, false, true)]
    [TestCase(ExpeditionDepth.DeepZone2, true, true, false)]
    public void CampaignSpine_CompletionGuardRejectsInterruptedAndActiveDialogue(
        ExpeditionDepth nextDepth, bool natural, bool active, bool expected)
    {
        string title = Phase2CStoryDialogueIds.FirstSettlementConversation;
        PermanentProgress progress = CreateProgress("CampaignAnalysisBoundary");
        progress.TryStartDamagedAccessKeyQuest();
        progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        if (nextDepth == ExpeditionDepth.DeepZone2)
        {
            progress.TryAuthorizeAnalyzedRegion();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
        }
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        bool allowed = DialoguePixelCrushersBridge.IsCompletionActionEligible(
            DialogueGameplayActionId.MainDamagedAccessKeyStart, title, title, active, natural);
        Assert.That(allowed, Is.EqualTo(expected));
        if (allowed)
        {
            progress.TryAuthorizeAnalyzedRegion();
        }
        Assert.That(progress.IsDepthUnlocked(nextDepth), Is.EqualTo(expected));
        if (!allowed)
        {
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        }
        Assert.That(DialoguePixelCrushersBridge.IsCompletionActionEligible(
            DialogueGameplayActionId.MainDamagedAccessKeyStart, title, "Other", false, true), Is.False);
        Assert.That(DialoguePixelCrushersBridge.IsCompletionActionEligible(
            DialogueGameplayActionId.MainDamagedAccessKeyStart, "Other", "Other", false, true), Is.False);
    }

    [Test]
    public void CampaignSpine_LegacyAuthorizationSurvivesLoadWithoutNewUnlocks()
    {
        PermanentProgress progress = CreateProgress("LegacyCampaignAuthorization");
        var save = new SaveData { highestUnlockedDepth = ExpeditionDepth.DeepZone2 };
        save.defeatedCampaignBosses.Add(CampaignBossId.SectorAdministrator);
        save.acquiredBossStoryParts.Add(BossStoryPart.SectorStabilizer);
        progress.LoadFromSave(save);
        Assert.That(progress.HighestUnlockedDepth, Is.EqualTo(ExpeditionDepth.DeepZone2));
        Assert.That(progress.IsDepthUnlocked(ExpeditionDepth.FinalNetwork), Is.False);
        save.highestUnlockedDepth = ExpeditionDepth.Normal;
        progress.LoadFromSave(save);
        Assert.That(progress.HighestUnlockedDepth, Is.EqualTo(ExpeditionDepth.Normal));
    }

    [TestCase(ExpeditionDepth.Normal, ExpeditionDepth.DeepZone1)]
    [TestCase(ExpeditionDepth.DeepZone1, ExpeditionDepth.DeepZone2)]
    public void CampaignSpine_RegionTransitionKeepsRunCargoBuildAndVitals(
        ExpeditionDepth from, ExpeditionDepth to)
    {
        var run = new RunContext(WeaponTreeType.Sniper, from, "carryover_ship");
        run.SetLevel(5);
        run.Wallet.Add(CurrencyType.Credits, 123);
        run.Wallet.Add(CurrencyType.ScrapParts, 4);
        run.Wallet.Add(CurrencyType.TuningChips, 7);
        run.Wallet.Add(CurrencyType.StabilizedAlloy, 3);
        run.SetEquippedReinforcement("carryover_active", 2);
        run.AddTrait("campaign_fixture_trait");
        run.CapturePlayerVitals(31f, 7f);
        run.MarkBossDefeated(CampaignProgressionCatalog.GetBossId(from), true);
        RunWallet wallet = run.Wallet;
        int cargo = run.CurrentCargoLoad;
        run.PrepareNextRegion(to, SeaRegionType.RaiderOccupied);
        Assert.That(run.Wallet, Is.SameAs(wallet));
        Assert.That(run.Wallet.Credits, Is.EqualTo(123));
        Assert.That(run.Wallet.PendingScrapParts, Is.EqualTo(4));
        Assert.That(run.Wallet.TuningChips, Is.EqualTo(7));
        Assert.That(run.Wallet.PendingStabilizedAlloy, Is.EqualTo(3));
        Assert.That(run.CurrentCargoLoad, Is.EqualTo(cargo));
        Assert.That(run.SelectedShipId, Is.EqualTo("carryover_ship"));
        Assert.That(run.EquippedReinforcementId, Is.EqualTo("carryover_active"));
        Assert.That(run.EquippedReinforcementCharges, Is.EqualTo(2));
        Assert.That(run.ExpeditionDepth, Is.EqualTo(to));
        Assert.That(run.SelectedTraitIds, Does.Contain("campaign_fixture_trait"));
        Assert.That(run.SelectedWeaponTree, Is.EqualTo(WeaponTreeType.Sniper));
        Assert.That(run.CurrentLevel, Is.EqualTo(5));
        Assert.That(run.BossDefeated, Is.False);
        Assert.That(run.FirstStoryClearThisRegion, Is.False);
        Assert.That(run.TryGetPlayerVitalCarryover(out float hp, out float armor), Is.True);
        Assert.That(hp, Is.EqualTo(31f));
        Assert.That(armor, Is.EqualTo(7f));
    }

    private static void RegisterAllRequiredBossParts(PermanentProgress progress)
    {
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator),
            Is.True);
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer),
            Is.True);
        Assert.That(
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper),
            Is.True);
    }

    private static void AssertGuidanceMapping(
        TutorialStep step,
        bool ancientSignalDetected,
        string expectedConversation)
    {
        Assert.That(
            Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                step,
                ancientSignalDetected,
                out string conversation),
            Is.True,
            step.ToString());
        Assert.That(conversation, Is.EqualTo(expectedConversation));
    }

    private static void AssertSingleLineGuidanceGraph(
        IReadOnlyDictionary<string, List<DialogueEntry>> graphs,
        string conversationTitle,
        string expectedTextKey)
    {
        Assert.That(graphs.ContainsKey(conversationTitle), Is.True);
        List<DialogueEntry> entries = graphs[conversationTitle];
        Assert.That(entries.Count, Is.EqualTo(3), conversationTitle);
        Assert.That(
            Field.LookupValue(entries[1].fields, "TextKey"),
            Is.EqualTo(expectedTextKey),
            conversationTitle);
        Assert.That(
            entries[2].userScript,
            Does.Contain(DialoguePixelCrushersBridge.CompletionLuaFunction),
            conversationTitle);
    }

    private static void AddNumberedKeys(
        List<string> keys,
        string prefix,
        int count)
    {
        for (int i = 1; i <= count; i++)
        {
            keys.Add(Phase2CStoryDialogueIds.GetNumberedTextKey(prefix, i));
        }
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), signature);
        int bodyStart = source.IndexOf('{', signatureIndex);
        Assert.That(bodyStart, Is.GreaterThan(signatureIndex), signature);

        int depth = 0;
        for (int i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Method body for '{signature}' is not balanced.");
        return string.Empty;
    }

    private static bool ContainsUnsafeDirectGameplayAction(string userScript)
    {
        return !string.IsNullOrEmpty(userScript) &&
               userScript.IndexOf(
                   DialoguePixelCrushersBridge.ActionLuaFunction,
                   StringComparison.Ordinal) >= 0;
    }
}


/// <summary>
/// Test-owned PreviewScene suppresses automatic gameplay lifecycle. These tests
/// explicitly step the existing coroutine and camera calculation; terminal-state
/// injection tests the existing dialogue boundary, not a second dialogue runtime.
/// Actual Pixel Crushers terminal-marker/localization tests remain above.
/// </summary>
public sealed class TutorialDiscoveryPresentationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene scene;
    private Scene userScene;
    private bool userSceneDirty;
    private float oldTimeScale;
    private TutorialFlowController flow;
    private GungeonStyleCamera2D camera;
    private Transform player;
    private HarvestObjectHealth wreck;
    private RadarTarget radar;
    private PlayerController2D controls;
    private PlayerWeaponController weapons;
    private TutorialTargetPresentationProgress progress;
    private ExpeditionHUD cinematicHud;
    private CanvasGroup cinematicGroup;

    [SetUp]
    public void SetUp()
    {
        userScene = SceneManager.GetActiveScene();
        userSceneDirty = userScene.isDirty;
        oldTimeScale = Time.timeScale;
        scene = EditorSceneManager.NewPreviewScene();
        player = Create("Player").transform;
        controls = player.gameObject.AddComponent<PlayerController2D>();
        weapons = player.gameObject.AddComponent<PlayerWeaponController>();
        camera = Create("CameraRig").AddComponent<GungeonStyleCamera2D>();
        Camera lens = Create("Camera").AddComponent<Camera>();
        lens.transform.SetParent(camera.transform, false);
        lens.orthographic = true;
        lens.orthographicSize = 4.21875f;
        Set(camera, "mainCamera", lens);
        Set(camera, "shakeRoot", lens.transform);
        Set(camera, "player", player);
        Set(camera, "playerController", controls);
        Set(camera, "mapGenerator", Create("TestMap").AddComponent<ExpeditionMapGenerator>());
        camera.transform.position = new Vector3(0f, 0f, -10f);
        GameObject wreckObject = Create("DiscoveredWreck");
        // HarvestObjectHealth requires Collider2D, which is abstract. Supply the
        // concrete collider used by a real wreck before adding the health owner.
        wreckObject.AddComponent<BoxCollider2D>();
        wreck = wreckObject.AddComponent<HarvestObjectHealth>();
        wreck.transform.position = new Vector3(12f, 0f, 0f);
        radar = wreck.gameObject.AddComponent<RadarTarget>();
        flow = Create("Tutorial").AddComponent<TutorialFlowController>();
        Set(flow, "playerRoot", player);
        Set(flow, "playerController", controls);
        Set(flow, "weaponController", weapons);
        Set(flow, "tutorialCamera", camera);
        Set(flow, "currentStep", TutorialStep.TravelHighValue);
        Set(flow, "highValueAutoRegistered", true);
        Set(flow, "highValueSalvageInstance", wreck.gameObject);
        Set(flow, "highValueSalvageHealth", wreck);
        Set(flow, "highValueSalvageRadarTarget", radar);
        GameObject gameplay = AuthoredRuntimeFixture.Create(scene, null, "GameplayHud", true);
        cinematicGroup = gameplay.AddComponent<CanvasGroup>();
        cinematicHud = gameplay.AddComponent<ExpeditionHUD>();
        // This fixture tests sequence ownership; authored fade interpolation is
        // covered by TutorialGuidanceUIAuthoringTests without a frame runner here.
        cinematicHud.enabled = false;
        Set(cinematicHud, "cinematicCanvasGroup", cinematicGroup);
        Set(flow, "expeditionHUD", cinematicHud);
        progress = Read<TutorialTargetPresentationProgress>(flow, "targetPresentationProgress");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (flow != null) Call(flow, "StopTargetPresentation", true, true);
            if (cinematicHud != null) cinematicHud.CompleteCinematicVisibilityTransition();
        }
        finally
        {
            try
            {
                if (scene.IsValid()) AuthoredRuntimeFixture.Close(scene);
            }
            finally
            {
                scene = default;
                Time.timeScale = oldTimeScale;
            }
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(userScene));
        Assert.That(userScene.isDirty, Is.EqualTo(userSceneDirty));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OffscreenReservation_DuplicateRadarAndMapNotificationsDoNotStartDialogue(bool fromMap)
    {
        IEnumerator routine = Begin();
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Focusing));
        Assert.That(routine.MoveNext(), Is.True);
        object owner = Read<object>(flow, "discoveryCameraOwner");
        Assert.That(camera.IsCinematicFocusOwnedBy(owner), Is.True);
        Assert.That(cinematicHud.IsCinematicMode, Is.True);
        Assert.That(Read<HashSet<object>>(cinematicHud, "cinematicModeOwners"), Is.EquivalentTo(new[] { owner }));
        Assert.That(controls.ControlEnabled, Is.False);
        Assert.That(weapons.ExternalInputLocked, Is.True);
        if (fromMap) Call(flow, "HandleMapTargetDiscovered", radar);
        else Call(flow, "HandleRadarScanCompleted", Vector2.zero, 30f, new[] { radar });
        Assert.That(Read<bool>(flow, "openingConversationStarted"), Is.False);
        Assert.That(Read<Transform>(flow, "activePresentationTarget"), Is.SameAs(wreck.transform));
        Assert.That(Call(flow, "TryReserveTargetPresentation", TutorialTargetPresentationKind.HighValueWreck), Is.False);
        Time.timeScale = 0f;
        AdvanceCamera(0.3f);
        Assert.That(camera.transform.position.x, Is.EqualTo(6f).Within(0.001f));
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Focusing));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TerminalBoundary_ReturnsUnscaledAndCompletesOnlyNaturallyOnce(bool natural)
    {
        int steps = 0;
        flow.StepChanged += (_, _) => steps++;
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        Time.timeScale = 0f;
        AdvanceCamera(0.6f);
        Assert.That(camera.IsCinematicFocusBlendActive, Is.False);
        Assert.That(progress.TryBeginDialogue(), Is.True);
        Assert.That(progress.TryBeginReturn(natural), Is.True);
        Assert.That(progress.TryBeginReturn(!natural), Is.False);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Returning));
        player.position = new Vector3(2f, 1f, 0f);
        AdvanceCamera(0.25f);
        Assert.That(camera.transform.position.x, Is.EqualTo(7f).Within(0.001f));
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(Read<bool>(flow, "openingConversationStarted"), Is.False);
        AdvanceCamera(0.25f);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(camera.transform.position, Is.EqualTo(new Vector3(2f, 1f, -10f)));
        Assert.That(steps, Is.EqualTo(natural ? 1 : 0));
        Assert.That(cinematicHud.IsCinematicMode, Is.False);
        Assert.That(Read<TutorialStoryGuidanceProgress>(flow, "operatorGuidanceProgress")
            .IsComplete(TutorialStep.TravelHighValue), Is.EqualTo(natural));
        Assert.That(controls.ControlEnabled, Is.True);
        Assert.That(weapons.ExternalInputLocked, Is.False);
        Assert.That(progress.TryFinishReturn(out _, out _), Is.False);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(steps, Is.EqualTo(natural ? 1 : 0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TargetInvalidation_DuringFocusOrDialogueReturnsWithoutCheckpoint(bool duringDialogue)
    {
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        if (duringDialogue)
        {
            AdvanceCamera(0.6f);
            Assert.That(progress.TryBeginDialogue(), Is.True);
        }
        else
        {
            AdvanceCamera(0.3f);
        }
        wreck.gameObject.SetActive(false);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Returning));
        AdvanceCamera(0.5f);
        Assert.That(routine.MoveNext(), Is.False);
        AssertUnfinishedAndReleased();
        Assert.That(Read<bool>(flow, "targetPresentationAwaitingRediscovery"), Is.True);
    }

    [TestCase("OnDisable", TutorialTargetPresentationPhase.Focusing)]
    [TestCase("OnDisable", TutorialTargetPresentationPhase.Dialogue)]
    [TestCase("OnDisable", TutorialTargetPresentationPhase.Returning)]
    [TestCase("OnDestroy", TutorialTargetPresentationPhase.Focusing)]
    [TestCase("OnDestroy", TutorialTargetPresentationPhase.Dialogue)]
    [TestCase("OnDestroy", TutorialTargetPresentationPhase.Returning)]
    [TestCase("HandleRunEnded", TutorialTargetPresentationPhase.Focusing)]
    [TestCase("HandleRunEnded", TutorialTargetPresentationPhase.Dialogue)]
    [TestCase("HandleRunEnded", TutorialTargetPresentationPhase.Returning)]
    public void LifecycleAndSceneExit_ReleaseOnlyTheDiscoveryCamera(
        string callback, TutorialTargetPresentationPhase phase)
    {
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        if (phase != TutorialTargetPresentationPhase.Focusing)
        {
            AdvanceCamera(0.6f);
            progress.TryBeginDialogue();
        }
        if (phase == TutorialTargetPresentationPhase.Returning)
        {
            progress.TryBeginReturn(true);
            Assert.That(routine.MoveNext(), Is.True);
        }
        object otherOwner = new object();
        cinematicHud.SetCinematicMode(otherOwner, true);
        weapons.SetExternalInputLocked(otherOwner, true);
        if (callback == "OnDestroy")
        {
            // Fixture owns destruction in EditMode; production OnDestroy must not
            // schedule runtime Destroy on a PreviewScene object.
            UnityEngine.Object.DestroyImmediate(wreck.gameObject);
        }
        if (callback == "HandleRunEnded") Call(flow, callback, new object[] { null });
        else Call(flow, callback);
        Assert.That(camera.IsCinematicFocusActive, Is.False);
        Assert.That(Read<HashSet<object>>(cinematicHud, "cinematicModeOwners"), Is.EquivalentTo(new[] { otherOwner }));
        cinematicHud.SetCinematicMode(otherOwner, false);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Idle));
        Assert.That(weapons.ExternalInputLocked, Is.True, "Do not release another input owner.");
        weapons.SetExternalInputLocked(otherOwner, false);
        Assert.That(weapons.ExternalInputLocked, Is.False);
        Assert.That(controls.ControlEnabled, Is.True);
    }

    [Test]
    public void ForeignCameraOwner_IsNotAcquiredOrReleasedByDiscoveryCleanup()
    {
        object other = new object();
        Assert.That(camera.TryBeginOwnedCinematicFocusBlend(other, Vector3.right * 7f, 0.6f, null, out _), Is.True);
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(camera.IsCinematicFocusOwnedBy(other), Is.True);
        Assert.That(cinematicHud.IsCinematicMode, Is.False);
        Assert.That(controls.ControlEnabled, Is.True);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Idle));
        Assert.That(camera.ReleaseOwnedCinematicFocus(other, true), Is.True);
    }

    [TestCase(TutorialTargetPresentationPhase.Focusing)]
    [TestCase(TutorialTargetPresentationPhase.Dialogue)]
    [TestCase(TutorialTargetPresentationPhase.Returning)]
    public void CameraTakeover_YieldsWithoutFightingTheNewPosition(TutorialTargetPresentationPhase phase)
    {
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        if (phase != TutorialTargetPresentationPhase.Focusing)
        {
            AdvanceCamera(0.6f);
            progress.TryBeginDialogue();
        }
        if (phase == TutorialTargetPresentationPhase.Returning)
        {
            progress.TryBeginReturn(true);
            Assert.That(routine.MoveNext(), Is.True);
        }
        camera.SetCinematicFocus(new Vector3(-6f, 3f, -10f), true);
        Vector3 newOwnerPosition = camera.transform.position;
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(camera.IsCinematicFocusActive, Is.True);
        Assert.That(camera.transform.position, Is.EqualTo(newOwnerPosition));
        Assert.That(controls.ControlEnabled, Is.True);
        Assert.That(Read<bool>(flow, "targetPresentationAwaitingRediscovery"), Is.True);
        Assert.That(cinematicHud.IsCinematicMode, Is.False);
    }

    [Test]
    public void DestroyedCamera_DoesNotDereferenceUnityObjectsOrLeaveInputLocked()
    {
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Idle));
        Assert.That(controls.ControlEnabled, Is.True);
        Assert.That(weapons.ExternalInputLocked, Is.False);
        Assert.That(Read<TutorialStoryGuidanceProgress>(flow, "operatorGuidanceProgress")
            .IsComplete(TutorialStep.TravelHighValue), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlayerLostOrReplacedDuringReturn_CancelsCompletionAndUnlocksCapturedPublishers(bool replace)
    {
        IEnumerator routine = Begin();
        Assert.That(routine.MoveNext(), Is.True);
        AdvanceCamera(0.6f);
        progress.TryBeginDialogue();
        progress.TryBeginReturn(true);
        Assert.That(routine.MoveNext(), Is.True);
        if (replace)
        {
            Transform replacement = Create("Replacement").transform;
            replacement.position = Vector3.left * 3f;
            camera.SetPlayer(replacement);
            Set(flow, "playerRoot", replacement);
            Set(flow, "weaponController", replacement.gameObject.AddComponent<PlayerWeaponController>());
            Assert.That(camera.IsCinematicFocusBlendActive, Is.True, "SetPlayer must not snap an owned blend.");
        }
        else
        {
            player.gameObject.SetActive(false);
        }
        Assert.That(routine.MoveNext(), Is.False);
        AssertUnfinishedAndReleased();
    }

    [TestCase(TutorialStep.Map)]
    [TestCase(TutorialStep.RoutePing)]
    [TestCase(TutorialStep.TravelNormalSalvage)]
    [TestCase(TutorialStep.DestroyNormalSalvage)]
    public void AuthoritativeBoxDeath_NeverRequiresRouteAndAdvancesOnce(TutorialStep stage)
    {
        Set(flow, "currentStep", stage);
        Set(flow, "harvestTargetHealth", wreck);
        int steps = 0;
        flow.StepChanged += (_, _) => steps++;
        Call(flow, "HandleHarvestTargetDied", wreck);
        Assert.That(flow.CurrentStep, Is.EqualTo(TutorialStep.CollectResources));
        Call(flow, "HandleHarvestTargetDied", wreck);
        Call(flow, "HandleRouteChanged");
        Assert.That(steps, Is.EqualTo(1));
    }

    [Test]
    public void FocusCompletion_IsTheOnlyDialogueStartBoundary_AndRetiredUiRemainsAbsent()
    {
        string source = File.ReadAllText("Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        int routine = source.IndexOf("private IEnumerator TargetPresentationRoutine()", StringComparison.Ordinal);
        int wait = source.IndexOf("while (targetPresentationCamera != null", routine, StringComparison.Ordinal);
        int dialogue = source.IndexOf("!TryStartOperatorConversation(", wait, StringComparison.Ordinal);
        int returning = source.IndexOf("TryBeginOwnedPlayerReturnBlend(", dialogue, StringComparison.Ordinal);
        Assert.That(dialogue, Is.GreaterThan(wait));
        Assert.That(returning, Is.GreaterThan(dialogue));
        Assert.That(source.Substring(returning, source.IndexOf("private bool CanContinueTargetPresentation", returning,
            StringComparison.Ordinal) - returning), Does.Not.Contain("TryStartOperatorConversation"));
        foreach (string file in Directory.GetFiles("Assets/02_Scripts", "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Tests/")) continue;
            Assert.That(File.ReadAllText(file), Does.Not.Contain("[MenuItem(\"VOID SCRAPPER/UI/"), file);
        }
        string follower = File.ReadAllText("Assets/02_Scripts/UI/WorldGaugeFollower.cs");
        Assert.That(follower, Does.Contain("beginCameraRendering"));
        Assert.That(follower.Split(new[] { "rectTransform.localPosition =" }, StringSplitOptions.None).Length,
            Is.EqualTo(2), "One shared charge-root position writer remains.");
    }

    private IEnumerator Begin()
    {
        Assert.That(Call(flow, "TryReserveTargetPresentation", TutorialTargetPresentationKind.HighValueWreck), Is.True);
        return (IEnumerator)Call(flow, "TargetPresentationRoutine");
    }

    private void AdvanceCamera(float unscaled)
    {
        Vector3 center = (Vector3)Call(camera, "ResolveDesiredCameraCenter", player.position, 0f, unscaled);
        camera.transform.position = center;
    }

    private void AssertUnfinishedAndReleased()
    {
        Assert.That(cinematicHud.IsCinematicMode, Is.False);
        Assert.That(progress.Phase, Is.EqualTo(TutorialTargetPresentationPhase.Idle));
        Assert.That(Read<TutorialStoryGuidanceProgress>(flow, "operatorGuidanceProgress")
            .IsComplete(TutorialStep.TravelHighValue), Is.False);
        Assert.That(camera.IsCinematicFocusActive, Is.False);
        Assert.That(weapons.ExternalInputLocked, Is.False);
        Assert.That(controls.ControlEnabled, Is.True);
    }

    private GameObject Create(string name)
    {
        return AuthoredRuntimeFixture.Create(scene, null, name, false);
    }

    private static T Read<T>(object owner, string field)
    {
        return (T)owner.GetType().GetField(field, Private).GetValue(owner);
    }

    private static void Set(object owner, string field, object value)
    {
        owner.GetType().GetField(field, Private).SetValue(owner, value);
    }

    private static object Call(object owner, string method, params object[] args)
    {
        return owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    }
}
