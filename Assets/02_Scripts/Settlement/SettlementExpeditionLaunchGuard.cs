using PixelCrushers.DialogueSystem;

public enum SettlementExpeditionLaunchFailure
{
    None,
    DialogueActive,
    MandatoryStoryPending,
    MissingRunManager,
    MissingSelectedShip,
    SelectedShipLocked
}

public static class SettlementExpeditionLaunchGuard
{
    public const string DialogueActiveTextKey =
        "system.settlement.expedition.dialogue_active";

    public static bool IsDialogueActive =>
        DialogueManager.hasInstance && DialogueManager.isConversationActive;

    public static bool IsMandatoryFirstSettlementStoryPending
    {
        get
        {
            PermanentProgress progress = PermanentProgress.Instance;
            return progress == null ||
                   (progress.HasUnlockFlag(
                        StoryProgressionIds.FirstSettlementPendingFlag) &&
                    !progress.HasUnlockFlag(
                        StoryProgressionIds.FirstSettlementCompleteFlag));
        }
    }

    public static bool TryPassDialogueGate(
        bool isConversationActive,
        out SettlementExpeditionLaunchFailure failure)
    {
        return TryPassMandatoryStoryGate(
            isConversationActive,
            false,
            out failure);
    }

    public static bool TryPassMandatoryStoryGate(
        bool isConversationActive,
        bool isMandatoryStoryPending,
        out SettlementExpeditionLaunchFailure failure)
    {
        if (isConversationActive)
        {
            failure = SettlementExpeditionLaunchFailure.DialogueActive;
            return false;
        }

        failure = isMandatoryStoryPending
            ? SettlementExpeditionLaunchFailure.MandatoryStoryPending
            : SettlementExpeditionLaunchFailure.None;
        return failure == SettlementExpeditionLaunchFailure.None;
    }
}
