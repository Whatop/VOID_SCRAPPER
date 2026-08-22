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

    [Header("Optional Progression Gate")]
    [SerializeField] private bool startOnSceneLoadWhenRequiredFlagPresent;
    [SerializeField] private string requiredUnlockFlag;
    [SerializeField] private string completionUnlockFlag;
    [SerializeField] private bool saveCompletionUnlockFlag = true;

    private DialogueSystemController activeController;
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

        ClearActiveConversation();
        MarkProgressionConversationCompleted();
        conversationCompleted?.Invoke();
        Completed?.Invoke(this);
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

        if (!TryStart())
        {
            Debug.LogWarning(
                $"[{nameof(DialogueStoryEntryPoint)}] Progression-gated conversation " +
                $"'{conversationTitle}' could not start.",
                this
            );
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
