using System;

public enum DialoguePresentationMode
{
    CompactGuidance = 0,
    ContextFocus = 1,
    CinematicCommunication = 2
}

public enum DialogueActorTheme
{
    Neutral = 0,
    Operator = 1,
    Curse = 2,
    Settlement = 3
}

public readonly struct DialoguePresentationProfile
{
    public DialoguePresentationProfile(
        DialoguePresentationMode mode,
        float dimAlpha,
        bool showsTopBar,
        bool usesExternalCameraFocus)
    {
        Mode = mode;
        DimAlpha = dimAlpha;
        ShowsTopBar = showsTopBar;
        UsesExternalCameraFocus = usesExternalCameraFocus;
    }

    public DialoguePresentationMode Mode { get; }
    public float DimAlpha { get; }
    public bool ShowsTopBar { get; }
    public bool UsesExternalCameraFocus { get; }

    // Camera ownership stays with TutorialFlowController or another gameplay owner.
    public bool TakesCameraOwnership => false;
}

public static class DialoguePresentationPolicy
{
    public const float ContextDimAlpha = 0.14f;
    public const float CinematicDimAlpha = 0.40f;
    public const float ReferenceWidth = 480f;
    public const float ReferenceHeight = 270f;
    public const float CinematicTopBarHeight = 14f;
    public const float CommunicationPanelHeight = 72f;
    public const float PortraitAreaWidth = 44f;
    public const float OverlayTransitionDuration = 0.18f;
    public const float PanelTransitionDuration = 0.15f;

    public static DialoguePresentationProfile Resolve(
        string conversationId,
        bool firstSettlementStoryPending)
    {
        if (string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialSupplyConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
                StringComparison.Ordinal))
        {
            return new DialoguePresentationProfile(
                DialoguePresentationMode.ContextFocus,
                ContextDimAlpha,
                false,
                true);
        }

        if (string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialRescueConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.FirstSettlementConversation,
                StringComparison.Ordinal) && firstSettlementStoryPending)
        {
            return new DialoguePresentationProfile(
                DialoguePresentationMode.CinematicCommunication,
                CinematicDimAlpha,
                true,
                false);
        }

        return new DialoguePresentationProfile(
            DialoguePresentationMode.CompactGuidance,
            0f,
            false,
            false);
    }

    public static DialogueActorTheme ResolveActorTheme(
        string conversationId,
        string actorId)
    {
        if (string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                actorId,
                Phase2CStoryDialogueIds.FakeOperatorActorName,
                StringComparison.Ordinal) ||
            string.Equals(
                conversationId,
                Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
                StringComparison.Ordinal) ||
            string.Equals(
                actorId,
                Phase2CStoryDialogueIds.CurseActorName,
                StringComparison.Ordinal))
        {
            return DialogueActorTheme.Curse;
        }

        if (string.Equals(
                actorId,
                Phase2CStoryDialogueIds.OperatorActorName,
                StringComparison.Ordinal))
        {
            return DialogueActorTheme.Operator;
        }

        if (string.Equals(
                actorId,
                Phase2CStoryDialogueIds.SettlementControlActorName,
                StringComparison.Ordinal))
        {
            return DialogueActorTheme.Settlement;
        }

        return DialogueActorTheme.Neutral;
    }
}

public sealed class DialoguePresentationLifecycleState
{
    public string ActiveConversationId { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public bool TryBegin(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) ||
            IsActive && string.Equals(
                ActiveConversationId,
                conversationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        ActiveConversationId = conversationId;
        IsActive = true;
        return true;
    }

    public bool TryEnd()
    {
        if (!IsActive)
        {
            return false;
        }

        ActiveConversationId = string.Empty;
        IsActive = false;
        return true;
    }
}
