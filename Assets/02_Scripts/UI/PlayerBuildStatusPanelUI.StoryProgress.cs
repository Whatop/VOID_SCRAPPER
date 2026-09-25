using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Read-only presentation of PermanentProgress. Recovery parts never enter Trait storage.
public partial class PlayerBuildStatusPanelUI
{
    [Header("Authored Story Progress Inspection")]
    [SerializeField] private Button storyProgressInspectButton;
    [SerializeField] private GameObject storyProgressInspectionRoot;
    [SerializeField] private TMP_Text storyProgressSummaryText;
    [SerializeField] private Button storyProgressCloseButton;

    public bool HasStoryProgressPresentation => storyProgressInspectButton != null &&
        storyProgressInspectionRoot != null && storyProgressSummaryText != null && storyProgressCloseButton != null;
    private bool IsStoryProgressOpen => storyProgressInspectionRoot != null && storyProgressInspectionRoot.activeSelf;

    private void BindStoryProgressInspection()
    {
        if (!HasStoryProgressPresentation) return;
        storyProgressInspectButton.onClick.RemoveListener(OpenStoryProgressInspection);
        storyProgressInspectButton.onClick.AddListener(OpenStoryProgressInspection);
        storyProgressCloseButton.onClick.RemoveListener(CloseStoryProgressInspection);
        storyProgressCloseButton.onClick.AddListener(CloseStoryProgressInspection);
        CloseStoryProgressInspection();
    }

    private void UnbindStoryProgressInspection()
    {
        if (storyProgressInspectButton != null) storyProgressInspectButton.onClick.RemoveListener(OpenStoryProgressInspection);
        if (storyProgressCloseButton != null) storyProgressCloseButton.onClick.RemoveListener(CloseStoryProgressInspection);
        CloseStoryProgressInspection();
    }

    private void RefreshStoryProgressSummary()
    {
        if (!HasStoryProgressPresentation) return;
        PermanentProgress progress = PermanentProgress.Instance;
        int acquired = 0;
        for (int i = 1; i <= 3; i++)
            if (progress != null && progress.HasBossStoryPart((BossStoryPart)i)) acquired++;
        SetButtonLabel(storyProgressInspectButton,
            ResolveStoryText("ui.story_recovery.inspect", InventoryText("회수 기록", "Recovery log")) + " " + acquired + "/3");
        storyProgressSummaryText.text = ResolveStoryText("ui.story_recovery.analysis_count",
            InventoryText("분석 완료 {count}/3", "Analyzed {count}/3"))
            .Replace("{count}", (progress != null ? progress.AnalyzedEquipmentComponentCount : 0).ToString());
        SetButtonLabel(storyProgressCloseButton, InventoryText("닫기", "Close"));
    }

    public void OpenStoryProgressInspection()
    {
        if (!isOpen || !HasStoryProgressPresentation) return;
        CloseStructuralFrameInspection();
        StopInventoryScrolling();
        cargoJettisonHoldTimer = 0f;
        cargoJettisonConsumedUntilRelease = true;
        RefreshStoryRecovery();
        storyProgressInspectionRoot.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(storyProgressCloseButton.gameObject);
    }

    public void CloseStoryProgressInspection()
    {
        bool wasOpen = IsStoryProgressOpen;
        if (storyProgressInspectionRoot != null) storyProgressInspectionRoot.SetActive(false);
        StopStoryRecoveryFeedback();
        cargoJettisonHoldTimer = 0f;
        cargoJettisonConsumedUntilRelease = true;
        if (wasOpen && isOpen) FocusInventoryTab();
    }
}
