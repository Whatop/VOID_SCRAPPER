using System;
using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueSystemController))]
public sealed class DialoguePixelCrushersBridge : MonoBehaviour
{
    public const string ConditionLuaFunction = "VS_CheckCondition";
    public const string ActionLuaFunction = "VS_DispatchAction";
    public const string CompletionLuaFunction = "VS_MarkConversationComplete";

    private readonly DialogueConditionRegistry conditionRegistry =
        new DialogueConditionRegistry();
    private readonly DialogueGameplayActionDispatcher actionDispatcher =
        new DialogueGameplayActionDispatcher();

    private DialogueSystemController dialogueController;
    private bool luaFunctionsRegistered;
    private string naturallyCompletedConversationTitle;

    private void Awake()
    {
        dialogueController = GetComponent<DialogueSystemController>();
    }

    private void OnEnable()
    {
        if (dialogueController == null)
        {
            dialogueController = GetComponent<DialogueSystemController>();
        }

        if (dialogueController != null)
        {
            dialogueController.conversationStarted -= HandleConversationStarted;
            dialogueController.conversationStarted += HandleConversationStarted;
            dialogueController.conversationEnded -= HandleConversationEnded;
            dialogueController.conversationEnded += HandleConversationEnded;
        }

        RegisterLuaFunctions();
    }

    private void OnDisable()
    {
        if (dialogueController != null)
        {
            dialogueController.conversationStarted -= HandleConversationStarted;
            dialogueController.conversationEnded -= HandleConversationEnded;
        }

        actionDispatcher.EndConversation();
        naturallyCompletedConversationTitle = string.Empty;
        UnregisterLuaFunctions();
    }

    private IFinalBossTreatmentAuthority finalBossAuthority;

    public bool RegisterFinalBossAuthority(IFinalBossTreatmentAuthority authority)
    {
        if (authority == null || (finalBossAuthority != null && !ReferenceEquals(finalBossAuthority, authority)))
        {
            return false;
        }
        finalBossAuthority = authority;
        return true;
    }

    public void UnregisterFinalBossAuthority(IFinalBossTreatmentAuthority authority)
    {
        if (ReferenceEquals(finalBossAuthority, authority))
        {
            finalBossAuthority = null;
        }
    }

    public bool EvaluateCondition(string conditionId)
    {
        return conditionRegistry.TryEvaluate(
                   conditionId,
                   ResolveCurrentRescueContactAuthority(),
                   ResolveMainQuestReadAuthority(),
                   out bool result) &&
               result;
    }

    public bool DispatchAction(string actionId)
    {
        if (!TryParseActionId(actionId, out DialogueGameplayActionId typedActionId))
        {
            return actionDispatcher.TryDispatch(
                actionId,
                ResolveCurrentRescueContactAuthority(),
                ResolveMainQuestStartAuthority(),
                ResolveCurrentInteractor(), finalBossAuthority);
        }

        string conversationTitle = dialogueController != null
            ? dialogueController.lastConversationStarted
            : string.Empty;
        if (typedActionId == DialogueGameplayActionId.MainDamagedAccessKeyStart ||
            !IsActionAllowedForConversation(typedActionId, conversationTitle))
        {
            Debug.LogWarning(
                $"[{nameof(DialoguePixelCrushersBridge)}] Action '{actionId}' was rejected " +
                $"for conversation '{conversationTitle}'.",
                this);
            return false;
        }

        return actionDispatcher.TryDispatch(
            typedActionId,
            ResolveCurrentRescueContactAuthority(),
            ResolveMainQuestStartAuthority(),
            ResolveCurrentInteractor(), finalBossAuthority);
    }

    public bool TryEvaluateCondition(
        DialogueConditionId conditionId,
        out bool result)
    {
        return conditionRegistry.TryEvaluate(
            conditionId,
            ResolveCurrentRescueContactAuthority(),
            ResolveMainQuestReadAuthority(),
            out result);
    }

    public bool TryDispatchCompletedConversationAction(
        DialogueGameplayActionId actionId,
        string completedConversationTitle)
    {
        if (dialogueController == null ||
            !IsCompletionActionEligible(actionId, completedConversationTitle,
                dialogueController.lastConversationEnded, DialogueManager.isConversationActive,
                WasConversationCompletedNaturally(completedConversationTitle)))
        {
            Debug.LogWarning(
                $"[{nameof(DialoguePixelCrushersBridge)}] Completion action '{actionId}' " +
                $"was rejected for '{completedConversationTitle}'.",
                this);
            return false;
        }

        actionDispatcher.BeginConversation(completedConversationTitle);
        bool dispatched = actionDispatcher.TryDispatch(
            actionId,
            ResolveCurrentRescueContactAuthority(),
            ResolveMainQuestStartAuthority(),
            ResolveCurrentInteractor());
        actionDispatcher.EndConversation();
        return dispatched;
    }

    public static bool IsCompletionActionEligible(DialogueGameplayActionId actionId,
        string conversationTitle, string lastEndedTitle, bool conversationActive, bool completedNaturally)
    {
        return !conversationActive && completedNaturally &&
               string.Equals(lastEndedTitle, conversationTitle, StringComparison.Ordinal) &&
               IsActionAllowedForConversation(actionId, conversationTitle);
    }

    public bool MarkConversationComplete(string conversationTitle)
    {
        if (dialogueController == null ||
            !actionDispatcher.HasActiveConversation ||
            string.IsNullOrWhiteSpace(conversationTitle) ||
            !string.Equals(
                dialogueController.lastConversationStarted,
                conversationTitle,
                StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[{nameof(DialoguePixelCrushersBridge)}] Completion marker for " +
                $"'{conversationTitle}' was rejected.",
                this);
            return false;
        }

        naturallyCompletedConversationTitle = conversationTitle;
        return true;
    }

    public bool WasConversationCompletedNaturally(string conversationTitle)
    {
        return !string.IsNullOrWhiteSpace(conversationTitle) &&
               string.Equals(
                   naturallyCompletedConversationTitle,
                   conversationTitle,
                   StringComparison.Ordinal);
    }

    private void HandleConversationStarted(Transform actor)
    {
        naturallyCompletedConversationTitle = string.Empty;
        actionDispatcher.BeginConversation(dialogueController.lastConversationStarted);
    }

    private void HandleConversationEnded(Transform actor)
    {
        actionDispatcher.EndConversation();
    }

    private IRescueContactDialogueServiceAuthority ResolveCurrentRescueContactAuthority()
    {
        Transform conversant = dialogueController != null
            ? dialogueController.currentConversant
            : null;
        FieldNpcObjective objective = conversant != null
            ? conversant.GetComponentInParent<FieldNpcObjective>()
            : null;
        return objective;
    }

    private static IMainDamagedAccessKeyQuestReadAuthority ResolveMainQuestReadAuthority()
    {
        if (SettlementController.Instance != null)
        {
            return SettlementController.Instance;
        }

        return PermanentProgress.Instance;
    }

    private static IMainDamagedAccessKeyQuestStartAuthority ResolveMainQuestStartAuthority()
    {
        return SettlementController.Instance;
    }

    private GameObject ResolveCurrentInteractor()
    {
        return dialogueController != null && dialogueController.currentActor != null
            ? dialogueController.currentActor.gameObject
            : null;
    }

    private static bool IsActionAllowedForConversation(
        DialogueGameplayActionId actionId,
        string conversationTitle)
    {
        return actionId switch
        {
            DialogueGameplayActionId.FinalBossTreatmentAccept or DialogueGameplayActionId.FinalBossTreatmentReject =>
                string.Equals(conversationTitle, NullDispatcherDialogueIds.Conversation, StringComparison.Ordinal),
            DialogueGameplayActionId.RescueContactAccept =>
                string.Equals(
                    conversationTitle,
                    RescueContactDialogueIds.ConversationTitle,
                    StringComparison.Ordinal),
            DialogueGameplayActionId.MainDamagedAccessKeyStart =>
                string.Equals(
                    conversationTitle,
                    Phase2CStoryDialogueIds.FirstSettlementConversation,
                    StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool TryParseActionId(
        string actionId,
        out DialogueGameplayActionId typedActionId)
    {
        return Enum.TryParse(actionId, false, out typedActionId) &&
               Enum.IsDefined(typeof(DialogueGameplayActionId), typedActionId) &&
               string.Equals(actionId, typedActionId.ToString(), StringComparison.Ordinal);
    }

    private void RegisterLuaFunctions()
    {
        if (luaFunctionsRegistered)
        {
            return;
        }

        Lua.RegisterFunction(
            ConditionLuaFunction,
            this,
            SymbolExtensions.GetMethodInfo(() => EvaluateCondition(string.Empty)));
        Lua.RegisterFunction(
            ActionLuaFunction,
            this,
            SymbolExtensions.GetMethodInfo(() => DispatchAction(string.Empty)));
        Lua.RegisterFunction(
            CompletionLuaFunction,
            this,
            SymbolExtensions.GetMethodInfo(() => MarkConversationComplete(string.Empty)));
        luaFunctionsRegistered = true;
    }

    private void UnregisterLuaFunctions()
    {
        if (!luaFunctionsRegistered)
        {
            return;
        }

        Lua.UnregisterFunction(ConditionLuaFunction);
        Lua.UnregisterFunction(ActionLuaFunction);
        Lua.UnregisterFunction(CompletionLuaFunction);
        luaFunctionsRegistered = false;
    }
}
