using UnityEngine;

public enum DialogueConditionId
{
    None = 0,
    RescueContactAvailable = 1,
    MainDamagedAccessKeyNotStarted = 2,
    MainDamagedAccessKeyActive0 = 3,
    MainDamagedAccessKeyActive1 = 4,
    MainDamagedAccessKeyActive2 = 5,
    MainDamagedAccessKeyReadyToAssemble = 6,
    MainDamagedAccessKeyCompleted = 7,
    MainDamagedAccessKeyReadyToRestore = 8
}

public enum DialogueGameplayActionId
{
    None = 0,
    RescueContactAccept = 1,
    MainDamagedAccessKeyStart = 2,
    FinalBossTreatmentAccept = 3,
    FinalBossTreatmentReject = 4
}

public interface IFinalBossTreatmentAuthority
{
    bool TryRequestTreatmentChoice(bool accept);
}

public static class NullDispatcherDialogueIds
{
    public const string Conversation = "FINAL_NullDispatcherTreatmentOffer";
    public const string Actor = "NullDispatcher";
    public const string SpeakerKey = "speaker.null_dispatcher.name";
}

public interface IRescueContactDialogueServiceAuthority
{
    bool IsRescueContactDialogueServiceAvailable { get; }

    bool TryExecuteRescueContactDialogueService(GameObject interactor);
}

public static class RescueContactDialogueIds
{
    public const string ConversationTitle = "NPC_RescueContact_Service";

    public const string SpeakerNameTextKey = "speaker.rescue_contact.name";
    public const string Line01TextKey = "dialogue.rescue_contact.service.line_01";
    public const string Line02TextKey = "dialogue.rescue_contact.service.line_02";
    public const string AcceptChoiceTextKey = "dialogue.rescue_contact.service.choice.accept";
    public const string DeclineChoiceTextKey = "dialogue.rescue_contact.service.choice.decline";
    public const string UnavailableTextKey = "dialogue.rescue_contact.service.unavailable";
}
