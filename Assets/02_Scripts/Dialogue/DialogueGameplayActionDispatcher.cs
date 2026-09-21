using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueGameplayActionDispatcher
{
    private readonly HashSet<DialogueGameplayActionId> completedActions =
        new HashSet<DialogueGameplayActionId>();

    private string activeConversationTitle;

    public bool HasActiveConversation =>
        !string.IsNullOrWhiteSpace(activeConversationTitle);

    public void BeginConversation(string conversationTitle)
    {
        activeConversationTitle = string.IsNullOrWhiteSpace(conversationTitle)
            ? string.Empty
            : conversationTitle.Trim();
        completedActions.Clear();
    }

    public void EndConversation()
    {
        activeConversationTitle = string.Empty;
        completedActions.Clear();
    }

    public bool TryDispatch(
        string actionId,
        IRescueContactDialogueServiceAuthority authority,
        GameObject interactor)
    {
        return TryDispatch(actionId, authority, null, interactor);
    }

    public bool TryDispatch(
        string actionId,
        IRescueContactDialogueServiceAuthority rescueContactAuthority,
        IMainDamagedAccessKeyQuestStartAuthority mainQuestAuthority,
        GameObject interactor,
        IFinalBossTreatmentAuthority finalBossAuthority = null)
    {
        if (!Enum.TryParse(actionId, false, out DialogueGameplayActionId typedId) ||
            !Enum.IsDefined(typeof(DialogueGameplayActionId), typedId) ||
            !string.Equals(actionId, typedId.ToString(), StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[{nameof(DialogueGameplayActionDispatcher)}] Unknown action ID '{actionId}'.");
            return false;
        }

        return TryDispatch(
            typedId,
            rescueContactAuthority,
            mainQuestAuthority,
            interactor,
            finalBossAuthority);
    }

    public bool TryDispatch(
        DialogueGameplayActionId actionId,
        IRescueContactDialogueServiceAuthority authority,
        GameObject interactor)
    {
        return TryDispatch(actionId, authority, null, interactor);
    }

    public bool TryDispatch(
        DialogueGameplayActionId actionId,
        IRescueContactDialogueServiceAuthority rescueContactAuthority,
        IMainDamagedAccessKeyQuestStartAuthority mainQuestAuthority,
        GameObject interactor,
        IFinalBossTreatmentAuthority finalBossAuthority = null)
    {
        if (!HasActiveConversation)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueGameplayActionDispatcher)}] Action '{actionId}' was " +
                "rejected because no conversation session is active.");
            return false;
        }

        if (completedActions.Contains(actionId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[{nameof(DialogueGameplayActionDispatcher)}] Duplicate action '{actionId}' " +
                $"was ignored for conversation '{activeConversationTitle}'.");
#endif
            return false;
        }

        bool executed;

        switch (actionId)
        {
            case DialogueGameplayActionId.FinalBossTreatmentAccept:
            case DialogueGameplayActionId.FinalBossTreatmentReject:
                executed = activeConversationTitle == NullDispatcherDialogueIds.Conversation &&
                    finalBossAuthority != null && finalBossAuthority.TryRequestTreatmentChoice(
                        actionId == DialogueGameplayActionId.FinalBossTreatmentAccept);
                break;
            case DialogueGameplayActionId.RescueContactAccept:
                executed = rescueContactAuthority != null &&
                           rescueContactAuthority.IsRescueContactDialogueServiceAvailable &&
                           rescueContactAuthority.TryExecuteRescueContactDialogueService(interactor);
                break;
            case DialogueGameplayActionId.MainDamagedAccessKeyStart:
                // Existing typed Settlement completion transaction also commits
                // pending route analysis. The bridge admits this only after END.
                executed = mainQuestAuthority != null &&
                           mainQuestAuthority.TryCompleteFirstSettlementStory();
                break;
            default:
                Debug.LogWarning(
                    $"[{nameof(DialogueGameplayActionDispatcher)}] Unregistered action ID " +
                    $"'{actionId}'.");
                return false;
        }

        if (executed)
        {
            completedActions.Add(actionId);
        }

        return executed;
    }
}
