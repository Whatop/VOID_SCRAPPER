using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueSystemController))]
public sealed class DialogueGameplayBridge : MonoBehaviour
{
    [Header("Conversation Policy")]
    [SerializeField] private bool pauseConversationsByDefault = true;
    [SerializeField] private string pauseGameplayFieldName = "Pause Gameplay";
    [SerializeField] private bool allowEscapeToStopConversation;

    private DialogueSystemController dialogueController;
    private GameplayPauseManager pauseManager;
    private bool ownsPause;

    private void Awake()
    {
        dialogueController = GetComponent<DialogueSystemController>();
    }

    private void OnDisable()
    {
        ReleaseOwnedPause();
    }

    private void OnConversationStart(Transform actor)
    {
        ReleaseOwnedPause();

        if (!ShouldPauseCurrentConversation())
        {
            return;
        }

        pauseManager = GameplayPauseManager.Instance;
        pauseManager.PushPause(this, "Dialogue conversation");
        pauseManager.RegisterCancelHandler(this, HandleCancelRequested);
        ownsPause = true;
    }

    private void OnConversationEnd(Transform actor)
    {
        ReleaseOwnedPause();
    }

    private bool ShouldPauseCurrentConversation()
    {
        DialogueDatabase database = dialogueController.masterDatabase;
        string conversationTitle = dialogueController.lastConversationStarted;
        Conversation conversation = database != null && !string.IsNullOrWhiteSpace(conversationTitle)
            ? database.GetConversation(conversationTitle)
            : null;

        if (conversation != null &&
            !string.IsNullOrWhiteSpace(pauseGameplayFieldName) &&
            conversation.FieldExists(pauseGameplayFieldName))
        {
            return conversation.LookupBool(pauseGameplayFieldName);
        }

        return pauseConversationsByDefault;
    }

    private void HandleCancelRequested()
    {
        if (allowEscapeToStopConversation && DialogueManager.isConversationActive)
        {
            DialogueManager.StopConversation();
        }
    }

    private void ReleaseOwnedPause()
    {
        if (!ownsPause || pauseManager == null)
        {
            return;
        }

        pauseManager.UnregisterCancelHandler(this);
        pauseManager.PopPause(this);
        ownsPause = false;
        pauseManager = null;
    }
}
