using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerBuildStatusPanelUI
{
    [Header("Read-only Operating Frame Inspection")]
    [SerializeField] private LocalizationCatalog operatingFrameLocalization;
    [SerializeField] private Button operatingFrameInspectButton;
    [SerializeField] private GameObject operatingFrameInspectionRoot;
    [SerializeField] private TMP_Text operatingFrameInspectionText;
    [SerializeField] private Button operatingFrameCloseButton;
    private bool reportedOperatingFrameLayoutError;

    public bool HasOperatingFramePresentation => operatingFrameLocalization != null && operatingFrameInspectButton != null &&
        operatingFrameInspectionRoot != null && operatingFrameInspectionText != null && operatingFrameCloseButton != null;

    private void BindOperatingFrameInspection()
    {
        if (!HasOperatingFramePresentation) return;
        UnbindOperatingFrameInspection();
        operatingFrameInspectButton.onClick.AddListener(OpenOperatingFrameInspection);
        operatingFrameCloseButton.onClick.AddListener(CloseOperatingFrameInspection);
    }

    private void UnbindOperatingFrameInspection()
    {
        if (operatingFrameInspectButton != null) operatingFrameInspectButton.onClick.RemoveListener(OpenOperatingFrameInspection);
        if (operatingFrameCloseButton != null) operatingFrameCloseButton.onClick.RemoveListener(CloseOperatingFrameInspection);
        CloseOperatingFrameInspection();
    }

    private void RefreshOperatingFrameInspection(RunContext run)
    {
        if (!HasOperatingFramePresentation)
        {
            if (Application.isPlaying && !reportedOperatingFrameLayoutError)
                Debug.LogError("PF_ExpeditionMapInventoryMenu/InventoryRoot: restore Operating Frame header button, authored inspection root/text/close and localization references. No fallback UI is created.", this);
            reportedOperatingFrameLayoutError = true;
            return;
        }
        operatingFrameInspectButton.interactable = run != null && run.IsActive;
        operatingFrameCloseButton.GetComponentInChildren<TMP_Text>(true).text = OperatingFrameText.Get("close", operatingFrameLocalization);
        if (run != null && run.IsActive)
            operatingFrameInspectionText.text = OperatingFrameText.Details(run.FrameProfile, operatingFrameLocalization) +
                "\n\n" + OperatingFrameText.Get("fixed_at_launch", operatingFrameLocalization);
    }

    public void OpenOperatingFrameInspection()
    {
        RunContext run = ResolveRunContext();
        RefreshOperatingFrameInspection(run);
        if (HasOperatingFramePresentation && run != null && run.IsActive) operatingFrameInspectionRoot.SetActive(true);
    }

    public void CloseOperatingFrameInspection()
    {
        if (operatingFrameInspectionRoot != null) operatingFrameInspectionRoot.SetActive(false);
    }
}
