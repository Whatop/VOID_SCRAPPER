using System;
using System.Text;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only projections of deterministic dialogue records. Never starts a conversation.</summary>
[DisallowMultipleComponent]
public sealed class SettlementDialogueArchivePanelUI : MonoBehaviour
{
    public sealed class Record
    {
        public readonly string Key;
        public readonly string Conversation;
        public readonly int[] Entries;
        public Record(string key, string conversation, params int[] entries)
        {
            Key = key;
            Conversation = conversation;
            Entries = entries;
        }
    }

    public static readonly Record[] Records =
    {
        new Record("opening", Phase2CStoryDialogueIds.TutorialOpeningConversation, 1),
        new Record("radar", Phase2CStoryDialogueIds.TutorialRadarConversation, 1),
        new Record("supply", Phase2CStoryDialogueIds.TutorialSupplyConversation, 1),
        new Record("ancient", Phase2CStoryDialogueIds.TutorialAncientSignalConversation, 1),
        new Record("relay", Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation, 1, 2),
        new Record("takeover", Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation, 1),
        new Record("access", Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation, 1, 2),
        new Record("rescue", Phase2CStoryDialogueIds.TutorialRescueConversation, 1, 2, 3),
        new Record("settlement", Phase2CStoryDialogueIds.FirstSettlementConversation, 1, 2, 3, 4, 5, 13, 14, 6),
        new Record("analysis1", Phase2CStoryDialogueIds.FirstSettlementConversation, 8, 16),
        new Record("analysis2", Phase2CStoryDialogueIds.FirstSettlementConversation, 9, 17),
        new Record("ready", Phase2CStoryDialogueIds.FirstSettlementConversation, 10, 18),
        new Record("restored", Phase2CStoryDialogueIds.FirstSettlementConversation, 11, 19),
        new Record("ending", NullDispatcherEndingPresentation.ConversationTitle, 1, 2, 3, 4, 5, 6)
    };

    [SerializeField] private DialogueDatabase database;
    [SerializeField] private LocalizationCatalog localizationCatalog;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private SettlementUIController owner;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text notice;
    [SerializeField] private TMP_Text transcript;
    [SerializeField] private TMP_Text backLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect transcriptScroll;
    [SerializeField] private Button[] recordButtons;
    [SerializeField] private TMP_Text[] recordLabels;
    private PermanentProgress subscribedProgress;
    private int selectedRecord = -1;
    private UnityEngine.Events.UnityAction[] selectActions;

    public Button NavigationEntry => selectedRecord >= 0 ? recordButtons[selectedRecord] : backButton;

    private void OnEnable()
    {
        if (!ValidateAuthoredReferences(out string error))
        {
            Debug.LogError(error, this);
            return;
        }
        selectActions ??= new UnityEngine.Events.UnityAction[Records.Length];
        for (int i = 0; i < Records.Length; i++)
        {
            int index = i;
            selectActions[i] ??= () => SelectRecord(index);
            recordButtons[i].onClick.RemoveListener(selectActions[i]);
            recordButtons[i].onClick.AddListener(selectActions[i]);
        }
        backButton.onClick.RemoveListener(Back);
        backButton.onClick.AddListener(Back);
        if (subscribedProgress != null) subscribedProgress.Changed -= Refresh;
        subscribedProgress = PermanentProgress.Instance;
        if (subscribedProgress != null) subscribedProgress.Changed += Refresh;
        GameSettingsRuntime.Changed -= Refresh;
        GameSettingsRuntime.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedProgress != null) subscribedProgress.Changed -= Refresh;
        subscribedProgress = null;
        GameSettingsRuntime.Changed -= Refresh;
        if (backButton != null) backButton.onClick.RemoveListener(Back);
        if (selectActions == null || recordButtons == null) return;
        for (int i = 0; i < Math.Min(selectActions.Length, recordButtons.Length); i++)
            if (recordButtons[i] != null) recordButtons[i].onClick.RemoveListener(selectActions[i]);
    }

    private void Back() => owner.ShowMainPanel();

    public bool ValidateAuthoredReferences(out string error)
    {
        bool valid = database != null && localizationCatalog != null && owner != null && title != null &&
            notice != null && transcript != null && backButton != null && backLabel != null && transcriptScroll != null &&
            recordButtons != null && recordButtons.Length == Records.Length && recordLabels != null && recordLabels.Length == Records.Length;
        for (int i = 0; valid && i < Records.Length; i++)
            valid &= recordButtons[i] != null && recordLabels[i] != null &&
                recordButtons[i].transform.IsChildOf(transform) && recordLabels[i].transform.IsChildOf(recordButtons[i].transform);
        error = valid ? string.Empty : "Settlement dialogue archive requires its authored database, labels, scroll area and record buttons. No fallback UI was created.";
        return valid;
    }

    public static bool IsAvailable(int index, PermanentProgress progress)
    {
        if (progress == null || index < 0 || index >= Records.Length) return false;
        if (index < 8) return progress.IsTutorialCompleted;
        if (index == 8) return progress.HasUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag);
        if (index == 9) return progress.HasBossStoryPart(BossStoryPart.SectorStabilizer) && progress.IsDepthUnlocked(ExpeditionDepth.DeepZone1);
        if (index == 10) return progress.HasBossStoryPart(BossStoryPart.MatterCompressor) && progress.IsDepthUnlocked(ExpeditionDepth.DeepZone2);
        if (index == 11) return progress.HasAllRouteCoreParts;
        if (index == 12) return progress.CurrentRouteCoreState >= RouteCoreState.Assembled;
        return progress.FinalBossDefeated;
    }

    public void Refresh()
    {
        if (!ValidateAuthoredReferences(out _)) return;
        title.text = Text("title");
        notice.text = Text("notice");
        backLabel.text = Text("back");
        int first = -1;
        for (int i = 0; i < Records.Length; i++)
        {
            bool available = IsAvailable(i, PermanentProgress.Instance) && database.GetConversation(Records[i].Conversation) != null;
            recordButtons[i].gameObject.SetActive(available);
            recordLabels[i].text = Text(Records[i].Key);
            if (available && first < 0) first = i;
        }
        if (!IsAvailable(selectedRecord, PermanentProgress.Instance)) selectedRecord = first;
        if (selectedRecord >= 0) SelectRecord(selectedRecord);
        else transcript.text = Text("empty");
    }

    public void SelectRecord(int index)
    {
        if (!IsAvailable(index, PermanentProgress.Instance)) return;
        selectedRecord = index;
        for (int i = 0; i < Records.Length; i++)
        {
            ColorBlock colors = recordButtons[i].colors;
            colors.normalColor = i == selectedRecord ? SettlementSelectionColors.SelectedBackground : new Color(.07f, .11f, .15f, 1f);
            colors.highlightedColor = i == selectedRecord ? SettlementSelectionColors.SelectedBackground : SettlementSelectionColors.HoverBackground;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = SettlementSelectionColors.SelectedBackground;
            recordButtons[i].colors = colors;
            recordLabels[i].color = i == selectedRecord ? SettlementSelectionColors.Selected : Color.white;
        }
        transcript.text = BuildTranscript(database, localizationCatalog, Records[index], GameSettingsRuntime.LanguageCode,
            InputBindingUtility.GetDisplayString(inputActions, "Player", "Radar", "?"),
            InputBindingUtility.GetDisplayString(inputActions, "Player", "RadarQuickScan", "?"));
        transcriptScroll.verticalNormalizedPosition = 1f;
    }

    public static string BuildTranscript(DialogueDatabase source, LocalizationCatalog catalog, Record record, string language, string radarBinding, string scanBinding)
    {
        Conversation conversation = source != null ? source.GetConversation(record.Conversation) : null;
        if (conversation == null || catalog == null) return string.Empty;
        StringBuilder result = new StringBuilder();
        foreach (int id in record.Entries)
        {
            DialogueEntry entry = conversation.dialogueEntries.Find(value => value.id == id);
            if (entry == null) continue;
            string key = Field.LookupValue(entry.fields, "TextKey");
            if (string.IsNullOrEmpty(key) || !catalog.TryGetText(key, language, out string line, out _)) continue;
            Actor speaker = source.GetActor(entry.ActorID);
            string nameKey = speaker != null ? Field.LookupValue(speaker.fields, "Name TextKey") : string.Empty;
            string name = speaker != null ? speaker.Name : string.Empty;
            if (!string.IsNullOrEmpty(nameKey) && catalog.TryGetText(nameKey, language, out string localizedName, out _)) name = localizedName;
            // Resolve only the two authored input placeholders; never execute Pixel Crushers tags/Lua.
            line = line.Replace("[var=VS_RadarToggleBinding]", radarBinding).Replace("[var=VS_RadarScanBinding]", scanBinding);
            if (result.Length > 0) result.Append("\n\n");
            result.Append(name).Append('\n').Append(line);
        }
        return result.ToString();
    }

    private string Text(string suffix)
    {
        string key = "ui.settlement.archive." + suffix;
        if (VoidScrapperLocalizationService.HasInstance) return VoidScrapperLocalizationService.Instance.GetText(key);
        return localizationCatalog != null && localizationCatalog.TryGetText(key, GameSettingsRuntime.LanguageCode, out string value, out _) ? value : string.Empty;
    }
}
