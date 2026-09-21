using System.Globalization;

public enum MainDamagedAccessKeyQuestState
{
    Invalid = -1,
    NotStarted = 0,
    Active0 = 1,
    Active1 = 2,
    Active2 = 3,
    ReadyToRestore = 4,
    Completed = 5
}

public interface IMainDamagedAccessKeyQuestReadAuthority
{
    MainDamagedAccessKeyQuestState DamagedAccessKeyQuestState { get; }

    int DamagedAccessKeyCollectedPartCount { get; }

    int DamagedAccessKeyRequiredPartCount { get; }
}

public interface IMainDamagedAccessKeyQuestStartAuthority :
    IMainDamagedAccessKeyQuestReadAuthority
{
    bool TryCompleteFirstSettlementStory();
}

public static class StoryProgressionIds
{
    public const string FirstSettlementPendingFlag =
        "story_first_settlement_unknown_core_pending";
    public const string FirstSettlementCompleteFlag =
        "story_first_settlement_unknown_core_complete";
}

public static class MainDamagedAccessKeyQuestIds
{
    public const string QuestId = "MAIN_DAMAGED_ACCESS_KEY";
    public const string StartedUnlockFlag = "main_quest:MAIN_DAMAGED_ACCESS_KEY:started";
    public const int RequiredPartCount = 3;

    public const string TitleTextKey = "quest.main_damaged_access_key.title";
    public const string StartedNotificationTextKey =
        "quest.main_damaged_access_key.notification.started";
    public const string ObjectiveTextKey =
        "quest.main_damaged_access_key.objective.parts";
    public const string RestorationDescriptionTextKey =
        "quest.main_damaged_access_key.restoration.description";
    public const string RestorationReadyTextKey =
        "quest.main_damaged_access_key.restoration.ready";
    public const string RestorationCompletedTextKey =
        "quest.main_damaged_access_key.restoration.completed";
    public const string RestorationActionTextKey =
        "quest.main_damaged_access_key.restoration.action";
    public const string RestoredNotificationTextKey =
        "quest.main_damaged_access_key.notification.restored";
}

public static class PixelCurseProgressionIds
{
    public const string TraitId = "pixel_curse";
    public const string LevelUpNotificationTextKey =
        "system.pixel_curse.level_up";
    public const int MaximumLevel = 4;
}

public static class Phase2CStoryDialogueIds
{
    public const string OperatorActorName = "Operator";
    public const string FakeOperatorActorName = "FakeOperator";
    public const string SettlementControlActorName = "Settlement Control";
    public const string CurseActorName = "Curse";
    public const string OperatorSpeakerTextKey = "speaker.operator.name";
    public const string FakeOperatorSpeakerTextKey = "speaker.fake_operator.name";
    public const string SettlementControlSpeakerTextKey =
        "speaker.settlement_control.name";

    public const string TutorialOpeningConversation = "TUTORIAL_OperatorOpening";
    public const string TutorialRadarConversation = "TUTORIAL_OperatorRadar";
    public const string TutorialSupplyConversation =
        "TUTORIAL_OperatorSupplyContainer";
    public const string TutorialAncientSignalConversation =
        "TUTORIAL_OperatorAncientSignal";
    public const string TutorialSignalRelayAnalysisConversation =
        "TUTORIAL_SignalRelayAnalysis";
    public const string TutorialFakeOperatorTakeoverConversation =
        "TUTORIAL_FakeOperatorTakeover";
    public const string TutorialUnknownAccessKeyConversation =
        "TUTORIAL_UnknownAccessKeyContact";
    public const string TutorialRescueConversation = "TUTORIAL_RescueAfterCurse";
    public const string FirstSettlementConversation =
        "STORY_FirstSettlementUnknownCore";

    public const string TutorialOpeningLinePrefix =
        "dialogue.tutorial.operator_opening.line_";
    public const string TutorialMovementTextKey =
        "dialogue.tutorial.operator_opening.line_01";
    public const string TutorialRadarTextKey =
        "dialogue.tutorial.operator_radar.line_01";
    public const string TutorialSupplyTextKey =
        "dialogue.tutorial.operator_supply_container.line_01";
    public const string TutorialAncientSignalTextKey =
        "dialogue.tutorial.operator_ancient_signal.line_01";
    public const string TutorialRadarObjectiveTextKey =
        "tutorial.radar.toggle_and_scan";
    public const string TutorialRadarScanOnlyTextKey =
        "tutorial.radar.scan_only";
    public const string TutorialRouteOptionalTextKey =
        "tutorial.supply.route_optional";
    public const string TutorialUnknownSignalTitleTextKey =
        "tutorial.signal.unknown.title";
    public const string TutorialUnknownSignalObjectiveTextKey =
        "tutorial.signal.unknown.objective";
    public const string TutorialPurpleCoreTravelTextKey =
        "tutorial.signal.purple_core.travel";
    public const string TutorialPurpleCoreInteractTextKey =
        "tutorial.signal.purple_core.interact";
    public const string TutorialCurseErrorTitleTextKey =
        "tutorial.curse.system_error.title";
    public const string TutorialCurseErrorBodyTextKey =
        "tutorial.curse.system_error.body";
    public const string RadarToggleLuaVariable = "VS_RadarToggleBinding";
    public const string RadarScanLuaVariable = "VS_RadarScanBinding";
    public const string TutorialUnknownLinePrefix =
        "dialogue.tutorial.unknown_access_key.line_";
    public const string TutorialSignalRelayAnalysisLinePrefix =
        "dialogue.tutorial.signal_relay_analysis.line_";
    public const string TutorialFakeOperatorTakeoverLinePrefix =
        "dialogue.tutorial.fake_operator_takeover.line_";
    public const string TutorialRescueLinePrefix =
        "dialogue.tutorial.rescue_after_curse.line_";
    public const string FirstSettlementLinePrefix =
        "dialogue.story.first_settlement_unknown_core.line_";

    public const string MainQuestActive0TextKey =
        "dialogue.story.main_damaged_access_key.active_0";
    public const string MainQuestActive1TextKey =
        "dialogue.story.main_damaged_access_key.active_1";
    public const string MainQuestActive2TextKey =
        "dialogue.story.main_damaged_access_key.active_2";
    public const string MainQuestReadyTextKey =
        "dialogue.story.main_damaged_access_key.ready_to_restore";
    public const string MainQuestCompletedTextKey =
        "dialogue.story.main_damaged_access_key.completed";

    // Keep the original single-line keys as each branch's first subtitle.
    public const string MainQuestActive0ResponseTextKey = MainQuestActive0TextKey + ".line_02";
    public const string MainQuestActive1ResponseTextKey = MainQuestActive1TextKey + ".line_02";
    public const string MainQuestActive2ResponseTextKey = MainQuestActive2TextKey + ".line_02";
    public const string MainQuestReadyResponseTextKey = MainQuestReadyTextKey + ".line_02";
    public const string MainQuestCompletedResponseTextKey = MainQuestCompletedTextKey + ".line_02";

    public static string GetNumberedTextKey(string prefix, int oneBasedIndex)
    {
        return prefix + oneBasedIndex.ToString("00", CultureInfo.InvariantCulture);
    }

    public static bool TryGetTutorialGuidanceConversation(
        TutorialStep step,
        bool ancientSignalDetected,
        out string conversationTitle)
    {
        conversationTitle = step switch
        {
            TutorialStep.Move => TutorialOpeningConversation,
            TutorialStep.RadarDiscoverSalvage => TutorialRadarConversation,
            TutorialStep.DestroyNormalSalvage => TutorialSupplyConversation,
            TutorialStep.TravelHighValue when ancientSignalDetected =>
                TutorialAncientSignalConversation,
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(conversationTitle);
    }

    public static bool AdvancesCheckpointAfterGuidance(TutorialStep step)
    {
        return step == TutorialStep.TravelHighValue;
    }

    public static string ResolveUnknownSignalRadarObjectiveTextKey(bool radarActive)
    {
        return radarActive
            ? TutorialRadarScanOnlyTextKey
            : TutorialRadarObjectiveTextKey;
    }
}

public enum TutorialRelayNarrativePhase
{
    Idle = 0,
    Analyzing = 1,
    AwaitingRealOperator = 2,
    RealOperatorActive = 3,
    AwaitingFakeOperator = 4,
    FakeOperatorActive = 5,
    Completed = 6
}

public sealed class TutorialRelayNarrativeProgress
{
    public TutorialRelayNarrativePhase Phase { get; private set; }
    public bool HasPresentedFakeTakeoverCue { get; private set; }

    public bool TryBeginAnalysis()
    {
        if (Phase != TutorialRelayNarrativePhase.Idle)
        {
            return false;
        }

        Phase = TutorialRelayNarrativePhase.Analyzing;
        return true;
    }

    public bool TryCompleteAnalysis()
    {
        if (Phase != TutorialRelayNarrativePhase.Analyzing)
        {
            return false;
        }

        Phase = TutorialRelayNarrativePhase.AwaitingRealOperator;
        return true;
    }

    public bool TryBeginRealOperator()
    {
        if (Phase != TutorialRelayNarrativePhase.AwaitingRealOperator)
        {
            return false;
        }

        Phase = TutorialRelayNarrativePhase.RealOperatorActive;
        return true;
    }

    public bool TryFinishRealOperator(bool completedNaturally)
    {
        if (Phase != TutorialRelayNarrativePhase.RealOperatorActive)
        {
            return false;
        }

        Phase = completedNaturally
            ? TutorialRelayNarrativePhase.AwaitingFakeOperator
            : TutorialRelayNarrativePhase.AwaitingRealOperator;
        return completedNaturally;
    }

    public bool TryBeginFakeOperator()
    {
        if (Phase != TutorialRelayNarrativePhase.AwaitingFakeOperator)
        {
            return false;
        }

        Phase = TutorialRelayNarrativePhase.FakeOperatorActive;
        return true;
    }

    public bool TryMarkFakeTakeoverCuePresented()
    {
        if (Phase != TutorialRelayNarrativePhase.AwaitingFakeOperator ||
            HasPresentedFakeTakeoverCue)
        {
            return false;
        }

        HasPresentedFakeTakeoverCue = true;
        return true;
    }

    public bool TryFinishFakeOperator(bool completedNaturally)
    {
        if (Phase != TutorialRelayNarrativePhase.FakeOperatorActive)
        {
            return false;
        }

        Phase = completedNaturally
            ? TutorialRelayNarrativePhase.Completed
            : TutorialRelayNarrativePhase.AwaitingFakeOperator;
        return completedNaturally;
    }

    public void CancelActivePhase()
    {
        if (Phase == TutorialRelayNarrativePhase.Analyzing)
        {
            Phase = TutorialRelayNarrativePhase.Idle;
        }
        else if (Phase == TutorialRelayNarrativePhase.RealOperatorActive)
        {
            Phase = TutorialRelayNarrativePhase.AwaitingRealOperator;
        }
        else if (Phase == TutorialRelayNarrativePhase.FakeOperatorActive)
        {
            Phase = TutorialRelayNarrativePhase.AwaitingFakeOperator;
        }
    }

    public void Reset()
    {
        Phase = TutorialRelayNarrativePhase.Idle;
        HasPresentedFakeTakeoverCue = false;
    }
}

public enum TutorialPurpleCoreActivationSource
{
    Interaction = 0,
    PlayerDamage = 1
}

public sealed class TutorialPurpleCoreAcquisitionLatch
{
    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }

    public bool TryBegin()
    {
        if (IsActive || IsCompleted)
        {
            return false;
        }

        IsActive = true;
        return true;
    }

    public void Cancel()
    {
        if (!IsCompleted)
        {
            IsActive = false;
        }
    }

    public bool TryComplete()
    {
        if (!IsActive || IsCompleted)
        {
            return false;
        }

        IsActive = false;
        IsCompleted = true;
        return true;
    }

    public void Reset()
    {
        IsActive = false;
        IsCompleted = false;
    }
}

public sealed class TutorialPurpleCoreDamageProgress
{
    private float accumulatedDamage;
    private float contributionWindowStartedAt = float.NegativeInfinity;
    private float contributionInCurrentWindow;

    public float AccumulatedDamage => accumulatedDamage;
    public bool ThresholdReached { get; private set; }

    public bool TryAddPlayerDamage(
        ProjectileOwner owner,
        float damage,
        float unscaledTime,
        float contributionWindowSeconds,
        float contributionCap,
        float threshold,
        out float acceptedDamage)
    {
        acceptedDamage = 0f;
        if (ThresholdReached || owner != ProjectileOwner.Player || damage <= 0f)
        {
            return false;
        }

        float safeWindow = System.Math.Max(0f, contributionWindowSeconds);
        if (unscaledTime - contributionWindowStartedAt >= safeWindow)
        {
            contributionWindowStartedAt = unscaledTime;
            contributionInCurrentWindow = 0f;
        }

        float remainingContribution = System.Math.Max(
            0f,
            contributionCap - contributionInCurrentWindow);
        acceptedDamage = System.Math.Min(damage, remainingContribution);
        if (acceptedDamage <= 0f)
        {
            return false;
        }

        contributionInCurrentWindow += acceptedDamage;
        accumulatedDamage += acceptedDamage;
        ThresholdReached = accumulatedDamage >= System.Math.Max(0.01f, threshold);
        return true;
    }

    public void Reset()
    {
        accumulatedDamage = 0f;
        contributionWindowStartedAt = float.NegativeInfinity;
        contributionInCurrentWindow = 0f;
        ThresholdReached = false;
    }
}

public sealed class TutorialOpeningTransmissionProgress
{
    public bool HasConfirmedMovement { get; private set; }
    public bool IsTransmissionActive { get; private set; }

    public bool TryConfirmMovement()
    {
        if (HasConfirmedMovement)
        {
            return false;
        }

        HasConfirmedMovement = true;
        return true;
    }

    public bool TryBeginTransmission()
    {
        return TryBeginTransmission(false, out _);
    }

    public bool TryBeginTransmission(
        bool incomingCueConfigured,
        out bool requestIncomingCue)
    {
        requestIncomingCue = false;
        if (!HasConfirmedMovement || IsTransmissionActive)
        {
            return false;
        }

        IsTransmissionActive = true;
        requestIncomingCue = incomingCueConfigured;
        return true;
    }

    public void FinishTransmission()
    {
        IsTransmissionActive = false;
    }

    public void Reset()
    {
        HasConfirmedMovement = false;
        IsTransmissionActive = false;
    }
}

public enum TutorialCorePresentationPhase
{
    Hidden = 0,
    Revealing = 1,
    Ready = 2,
    Transferring = 3,
    Impact = 4,
    CurseApplied = 5
}

public sealed class TutorialCorePresentationProgress
{
    public TutorialCorePresentationPhase Phase { get; private set; }

    public bool TryBeginReveal()
    {
        if (Phase != TutorialCorePresentationPhase.Hidden)
        {
            return false;
        }

        Phase = TutorialCorePresentationPhase.Revealing;
        return true;
    }

    public bool TryCompleteReveal()
    {
        if (Phase != TutorialCorePresentationPhase.Revealing)
        {
            return false;
        }

        Phase = TutorialCorePresentationPhase.Ready;
        return true;
    }

    public bool TryBeginTransfer()
    {
        if (Phase != TutorialCorePresentationPhase.Ready)
        {
            return false;
        }

        Phase = TutorialCorePresentationPhase.Transferring;
        return true;
    }

    public bool TryBeginImpact()
    {
        if (Phase != TutorialCorePresentationPhase.Transferring)
        {
            return false;
        }

        Phase = TutorialCorePresentationPhase.Impact;
        return true;
    }

    public bool TryApplyCurse()
    {
        if (Phase != TutorialCorePresentationPhase.Impact)
        {
            return false;
        }

        Phase = TutorialCorePresentationPhase.CurseApplied;
        return true;
    }

    public void RestoreReadyAfterInterruptedTransfer()
    {
        if (Phase == TutorialCorePresentationPhase.Transferring ||
            Phase == TutorialCorePresentationPhase.Impact)
        {
            Phase = TutorialCorePresentationPhase.Ready;
        }
    }

    public void RestoreReadyAfterFailedCurseApplication()
    {
        if (Phase == TutorialCorePresentationPhase.CurseApplied)
        {
            Phase = TutorialCorePresentationPhase.Ready;
        }
    }

    public void Reset()
    {
        Phase = TutorialCorePresentationPhase.Hidden;
    }
}

public sealed class TutorialStoryGuidanceProgress
{
    private readonly System.Collections.Generic.HashSet<TutorialStep> completedSteps =
        new System.Collections.Generic.HashSet<TutorialStep>();

    public bool IsComplete(TutorialStep step)
    {
        return completedSteps.Contains(step);
    }

    public bool TryComplete(TutorialStep step, bool completedNaturally)
    {
        if (!completedNaturally ||
            !Phase2CStoryDialogueIds.TryGetTutorialGuidanceConversation(
                step,
                true,
                out _))
        {
            return false;
        }

        return completedSteps.Add(step);
    }

    public void Reset()
    {
        completedSteps.Clear();
    }
}

public sealed class TutorialRadarGuidanceProgress
{
    public bool HasActivatedRadar { get; private set; }
    public bool HasSuccessfulTargetScan { get; private set; }

    public void RecordRadarActiveChanged(bool active)
    {
        if (active)
        {
            HasActivatedRadar = true;
        }
    }

    public bool TryRecordSuccessfulTargetScan(bool requiredTargetDetected)
    {
        if (!HasActivatedRadar || !requiredTargetDetected || HasSuccessfulTargetScan)
        {
            return false;
        }

        HasSuccessfulTargetScan = true;
        return true;
    }

    public void Reset()
    {
        HasActivatedRadar = false;
        HasSuccessfulTargetScan = false;
    }
}

public enum TutorialTargetPresentationKind
{
    None = 0,
    SupplyContainer = 1,
    HighValueWreck = 2
}

public enum TutorialTargetPresentationPhase
{
    Idle = 0,
    Focusing = 1,
    Dialogue = 2,
    Returning = 3,
    WaitingForSafeViewport = 4
}

public sealed class TutorialTargetPresentationProgress
{
    private bool dialogueCompletedNaturally;
    private float safeViewportSeconds;

    public TutorialTargetPresentationKind Kind { get; private set; }
    public TutorialTargetPresentationPhase Phase { get; private set; }

    public bool TryRequest(TutorialTargetPresentationKind kind, bool targetValid)
    {
        if (kind == TutorialTargetPresentationKind.None ||
            !targetValid ||
            Phase != TutorialTargetPresentationPhase.Idle)
        {
            return false;
        }

        dialogueCompletedNaturally = false;
        safeViewportSeconds = 0f;
        Kind = kind;
        // Discovery, not prior visibility, reserves the ancient-wreck camera trip.
        // Supply retains its existing safe-viewport introduction contract.
        Phase = kind == TutorialTargetPresentationKind.HighValueWreck
            ? TutorialTargetPresentationPhase.Focusing
            : TutorialTargetPresentationPhase.WaitingForSafeViewport;
        return true;
    }

    public bool TryBeginFocus(
        bool targetInsideSafeViewport,
        float unscaledDeltaTime,
        float requiredStableSeconds)
    {
        if (Phase != TutorialTargetPresentationPhase.WaitingForSafeViewport)
        {
            return false;
        }

        if (!targetInsideSafeViewport)
        {
            safeViewportSeconds = 0f;
            return false;
        }

        safeViewportSeconds += System.Math.Max(0f, unscaledDeltaTime);
        if (safeViewportSeconds < System.Math.Max(0f, requiredStableSeconds))
        {
            return false;
        }

        safeViewportSeconds = 0f;
        Phase = TutorialTargetPresentationPhase.Focusing;
        return true;
    }

    public bool TryBeginDialogue()
    {
        if (Phase != TutorialTargetPresentationPhase.Focusing)
        {
            return false;
        }

        Phase = TutorialTargetPresentationPhase.Dialogue;
        return true;
    }

    public bool TryBeginReturn(bool completedNaturally)
    {
        if (Phase != TutorialTargetPresentationPhase.Focusing &&
            Phase != TutorialTargetPresentationPhase.Dialogue)
        {
            return false;
        }

        dialogueCompletedNaturally = completedNaturally;
        Phase = TutorialTargetPresentationPhase.Returning;
        return true;
    }

    public bool TryFinishReturn(
        out TutorialTargetPresentationKind completedKind,
        out bool shouldAdvanceCheckpoint)
    {
        completedKind = TutorialTargetPresentationKind.None;
        shouldAdvanceCheckpoint = false;
        if (Phase != TutorialTargetPresentationPhase.Returning)
        {
            return false;
        }

        completedKind = Kind;
        shouldAdvanceCheckpoint = dialogueCompletedNaturally;
        Reset();
        return true;
    }

    public void CancelCheckpointCompletion()
    {
        if (Phase == TutorialTargetPresentationPhase.Returning)
        {
            dialogueCompletedNaturally = false;
        }
    }

    public void Reset()
    {
        dialogueCompletedNaturally = false;
        safeViewportSeconds = 0f;
        Kind = TutorialTargetPresentationKind.None;
        Phase = TutorialTargetPresentationPhase.Idle;
    }
}

public enum TutorialSupplyProgressSource
{
    RoutePlaced = 0,
    TargetApproached = 1,
    TargetDestroyed = 2
}

public static class TutorialSupplyProgression
{
    public static bool IsTargetDamageEnabled(TutorialStep currentStep)
    {
        return currentStep == TutorialStep.Map ||
               currentStep == TutorialStep.RoutePing ||
               currentStep == TutorialStep.TravelNormalSalvage ||
               currentStep == TutorialStep.DestroyNormalSalvage;
    }

    public static bool TryResolveNextStep(
        TutorialStep currentStep,
        TutorialSupplyProgressSource source,
        out TutorialStep nextStep)
    {
        nextStep = currentStep;

        if (currentStep == TutorialStep.RoutePing &&
            (source == TutorialSupplyProgressSource.RoutePlaced ||
             source == TutorialSupplyProgressSource.TargetApproached))
        {
            nextStep = TutorialStep.TravelNormalSalvage;
            return true;
        }

        if (currentStep == TutorialStep.TravelNormalSalvage &&
            source == TutorialSupplyProgressSource.TargetApproached)
        {
            nextStep = TutorialStep.DestroyNormalSalvage;
            return true;
        }

        if ((currentStep == TutorialStep.Map ||
             currentStep == TutorialStep.RoutePing ||
             currentStep == TutorialStep.TravelNormalSalvage ||
             currentStep == TutorialStep.DestroyNormalSalvage) &&
            source == TutorialSupplyProgressSource.TargetDestroyed)
        {
            nextStep = TutorialStep.CollectResources;
            return true;
        }

        return false;
    }
}
