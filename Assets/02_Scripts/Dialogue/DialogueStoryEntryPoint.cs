using System;
using System.Collections;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class DialogueStoryEntryPoint : MonoBehaviour
{
    [Header("Dialogue System")]
    [SerializeField] private string conversationTitle;
    [SerializeField] private Transform defaultActor;
    [SerializeField] private Transform defaultConversant;

    [Header("Completion")]
    [SerializeField] private UnityEvent conversationStarted;
    [SerializeField] private UnityEvent conversationCompleted;
    [SerializeField] private DialogueConditionId completionActionConditionId =
        DialogueConditionId.None;
    [SerializeField] private DialogueGameplayActionId completionActionId =
        DialogueGameplayActionId.None;
    [SerializeField] private bool requireNaturalCompletionMarker;

    [Header("Optional Progression Gate")]
    [SerializeField] private bool startOnSceneLoadWhenRequiredFlagPresent;
    [SerializeField] private string requiredUnlockFlag;
    [SerializeField] private string completionUnlockFlag;
    [SerializeField] private bool saveCompletionUnlockFlag = true;

    private DialogueSystemController activeController;
    private DialogueSystemController deferredStartController;
    private string activeConversationTitle;
    private Coroutine sceneLoadStartRoutine;

    public string ConversationTitle => conversationTitle;
    public bool IsRunning => activeController != null;

    public event Action<DialogueStoryEntryPoint> Started;
    public event Action<DialogueStoryEntryPoint> Completed;

    private void Start()
    {
        if (startOnSceneLoadWhenRequiredFlagPresent)
        {
            sceneLoadStartRoutine = StartCoroutine(TryStartFromProgressionRoutine());
        }
    }

    public void StartConfiguredConversation()
    {
        TryStart(defaultActor, defaultConversant);
    }

    public bool TryStart(Transform actor = null, Transform conversant = null)
    {
        return TryStartConversation(conversationTitle, actor, conversant);
    }

    public bool TryStartConversation(
        string requestedConversationTitle,
        Transform actor = null,
        Transform conversant = null
    )
    {
        if (IsRunning)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Conversation '{activeConversationTitle}' is already running.",
                this
            );
            return false;
        }

        string trimmedTitle = string.IsNullOrWhiteSpace(requestedConversationTitle)
            ? string.Empty
            : requestedConversationTitle.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] {name} has no conversation title configured.",
                this
            );
            return false;
        }

        if (!DialogueManager.hasInstance || DialogueManager.instance == null)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Cannot start '{trimmedTitle}' because " +
                "the persistent Dialogue Manager is unavailable.",
                this
            );
            return false;
        }

        if (DialogueManager.isConversationActive)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Cannot start '{trimmedTitle}' while " +
                "another conversation is active.",
                this
            );
            return false;
        }

        DialogueDatabase database = DialogueManager.MasterDatabase;

        if (database == null || database.GetConversation(trimmedTitle) == null)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Conversation '{trimmedTitle}' is " +
                "missing from the active Dialogue Database.",
                this
            );
            return false;
        }

        activeController = DialogueManager.instance;
        activeConversationTitle = trimmedTitle;
        activeController.conversationEnded -= HandleConversationEnded;
        activeController.conversationEnded += HandleConversationEnded;

        Transform resolvedActor = actor != null ? actor : defaultActor;
        Transform resolvedConversant = conversant != null ? conversant : defaultConversant;
        DialogueManager.StartConversation(
            activeConversationTitle,
            resolvedActor,
            resolvedConversant
        );

        bool started = DialogueManager.isConversationActive &&
                       string.Equals(
                           DialogueManager.lastConversationStarted,
                           activeConversationTitle,
                           StringComparison.Ordinal
                       );

        if (!started)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Dialogue System rejected conversation " +
                $"'{activeConversationTitle}'.",
                this
            );
            ClearActiveConversation();
            return false;
        }

        conversationStarted?.Invoke();
        Started?.Invoke(this);
        return true;
    }

    private void OnDisable()
    {
        if (sceneLoadStartRoutine != null)
        {
            StopCoroutine(sceneLoadStartRoutine);
            sceneLoadStartRoutine = null;
        }

        ClearDeferredStart();
        ClearActiveConversation();
    }

    private void HandleConversationEnded(Transform actor)
    {
        if (string.IsNullOrWhiteSpace(activeConversationTitle) ||
            !string.Equals(
                DialogueManager.lastConversationEnded,
                activeConversationTitle,
                StringComparison.Ordinal
            ))
        {
            return;
        }

        if (!TryRequestCompletionAction())
        {
            Debug.LogError(
                $"[{nameof(DialogueStoryEntryPoint)}] Conversation " +
                $"'{activeConversationTitle}' ended, but its required completion " +
                "action failed. Progression completion was not recorded.",
                this
            );
            ClearActiveConversation();
            return;
        }

        ClearActiveConversation();
        if (completionActionId == DialogueGameplayActionId.None)
        {
            MarkProgressionConversationCompleted();
        }
        conversationCompleted?.Invoke();
        Completed?.Invoke(this);
    }

    private bool TryRequestCompletionAction()
    {
        if (activeController == null)
        {
            return completionActionId == DialogueGameplayActionId.None &&
                   !requireNaturalCompletionMarker;
        }

        DialoguePixelCrushersBridge bridge =
            activeController.GetComponent<DialoguePixelCrushersBridge>();
        if (bridge == null &&
            (completionActionConditionId != DialogueConditionId.None ||
             completionActionId != DialogueGameplayActionId.None ||
             requireNaturalCompletionMarker))
        {
            return false;
        }

        if (requireNaturalCompletionMarker &&
            !bridge.WasConversationCompletedNaturally(activeConversationTitle))
        {
            return false;
        }

        if (completionActionId == DialogueGameplayActionId.None)
        {
            return true;
        }

        if (completionActionConditionId != DialogueConditionId.None)
        {
            if (!bridge.TryEvaluateCondition(
                    completionActionConditionId,
                    out bool shouldRequestAction))
            {
                return false;
            }

            if (!shouldRequestAction)
            {
                return true;
            }
        }

        return bridge.TryDispatchCompletedConversationAction(
            completionActionId,
            activeConversationTitle);
    }

    private IEnumerator TryStartFromProgressionRoutine()
    {
        // Boot owns the persistent Dialogue Manager. Give scene Awake/Start
        // integration one frame to settle before evaluating this one-shot hook.
        yield return null;
        sceneLoadStartRoutine = null;

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null ||
            string.IsNullOrWhiteSpace(requiredUnlockFlag) ||
            !progress.HasUnlockFlag(requiredUnlockFlag) ||
            (!string.IsNullOrWhiteSpace(completionUnlockFlag) &&
             progress.HasUnlockFlag(completionUnlockFlag)))
        {
            yield break;
        }

        if (DialogueManager.hasInstance &&
            DialogueManager.instance != null &&
            DialogueManager.isConversationActive)
        {
            deferredStartController = DialogueManager.instance;
            deferredStartController.conversationEnded -=
                HandleDeferredConversationEnded;
            deferredStartController.conversationEnded +=
                HandleDeferredConversationEnded;
            yield break;
        }

        if (!TryStart())
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Progression-gated conversation " +
                $"'{conversationTitle}' could not start.",
                this
            );
        }
    }

    private void HandleDeferredConversationEnded(Transform actor)
    {
        ClearDeferredStart();
        if (isActiveAndEnabled && sceneLoadStartRoutine == null)
        {
            sceneLoadStartRoutine = StartCoroutine(
                TryStartFromProgressionRoutine());
        }
    }

    private void ClearDeferredStart()
    {
        if (deferredStartController != null)
        {
            deferredStartController.conversationEnded -=
                HandleDeferredConversationEnded;
            deferredStartController = null;
        }
    }

    private void MarkProgressionConversationCompleted()
    {
        if (!startOnSceneLoadWhenRequiredFlagPresent ||
            string.IsNullOrWhiteSpace(completionUnlockFlag))
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || progress.HasUnlockFlag(completionUnlockFlag))
        {
            return;
        }

        progress.AddUnlockFlag(completionUnlockFlag);

        if (saveCompletionUnlockFlag && SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }
    }

    private void ClearActiveConversation()
    {
        if (activeController != null)
        {
            activeController.conversationEnded -= HandleConversationEnded;
            activeController = null;
        }

        activeConversationTitle = null;
    }
}
