using System;
using UnityEngine;

public sealed class DialogueConditionRegistry
{
    public bool TryEvaluate(
        string conditionId,
        IRescueContactDialogueServiceAuthority authority,
        out bool result)
    {
        return TryEvaluate(conditionId, authority, null, out result);
    }

    public bool TryEvaluate(
        string conditionId,
        IRescueContactDialogueServiceAuthority rescueContactAuthority,
        IMainDamagedAccessKeyQuestReadAuthority mainQuestAuthority,
        out bool result)
    {
        result = false;

        if (!Enum.TryParse(conditionId, false, out DialogueConditionId typedId) ||
            !Enum.IsDefined(typeof(DialogueConditionId), typedId) ||
            !string.Equals(conditionId, typedId.ToString(), StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[{nameof(DialogueConditionRegistry)}] Unknown condition ID '{conditionId}'.");
            return false;
        }

        return TryEvaluate(
            typedId,
            rescueContactAuthority,
            mainQuestAuthority,
            out result);
    }

    public bool TryEvaluate(
        DialogueConditionId conditionId,
        IRescueContactDialogueServiceAuthority authority,
        out bool result)
    {
        return TryEvaluate(conditionId, authority, null, out result);
    }

    public bool TryEvaluate(
        DialogueConditionId conditionId,
        IRescueContactDialogueServiceAuthority rescueContactAuthority,
        IMainDamagedAccessKeyQuestReadAuthority mainQuestAuthority,
        out bool result)
    {
        switch (conditionId)
        {
            case DialogueConditionId.RescueContactAvailable:
                result = rescueContactAuthority != null &&
                         rescueContactAuthority.IsRescueContactDialogueServiceAvailable;
                return true;
            case DialogueConditionId.MainDamagedAccessKeyNotStarted:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.NotStarted);
                return true;
            case DialogueConditionId.MainDamagedAccessKeyActive0:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.Active0);
                return true;
            case DialogueConditionId.MainDamagedAccessKeyActive1:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.Active1);
                return true;
            case DialogueConditionId.MainDamagedAccessKeyActive2:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.Active2);
                return true;
            case DialogueConditionId.MainDamagedAccessKeyReadyToAssemble:
            case DialogueConditionId.MainDamagedAccessKeyReadyToRestore:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.ReadyToRestore);
                return true;
            case DialogueConditionId.MainDamagedAccessKeyCompleted:
                result = HasMainQuestState(
                    mainQuestAuthority,
                    MainDamagedAccessKeyQuestState.Completed);
                return true;
            default:
                result = false;
                Debug.LogWarning(
                    $"[{nameof(DialogueConditionRegistry)}] Unregistered condition ID " +
                    $"'{conditionId}'.");
                return false;
        }
    }

    private static bool HasMainQuestState(
        IMainDamagedAccessKeyQuestReadAuthority authority,
        MainDamagedAccessKeyQuestState expectedState)
    {
        return authority != null &&
               authority.DamagedAccessKeyQuestState == expectedState;
    }
}
