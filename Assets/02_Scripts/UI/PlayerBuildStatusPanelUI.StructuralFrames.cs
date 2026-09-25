using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public partial class PlayerBuildStatusPanelUI
{
    [Header("Read-only Structural Frame Inspection")]
    [FormerlySerializedAs("operatingFrameLocalization")]
    [SerializeField] private LocalizationCatalog structuralFrameLocalization;
    [FormerlySerializedAs("operatingFrameInspectButton")]
    [SerializeField] private Button structuralFrameInspectButton;
    [FormerlySerializedAs("operatingFrameInspectionRoot")]
    [SerializeField] private GameObject structuralFrameInspectionRoot;
    [FormerlySerializedAs("operatingFrameInspectionText")]
    [SerializeField] private TMP_Text structuralFrameInspectionText;
    [FormerlySerializedAs("operatingFrameCloseButton")]
    [SerializeField] private Button structuralFrameCloseButton;
    private bool reportedStructuralFrameLayoutError;

    public bool HasStructuralFramePresentation => structuralFrameLocalization != null && structuralFrameInspectButton != null &&
        structuralFrameInspectionRoot != null && structuralFrameInspectionText != null && structuralFrameCloseButton != null;

    private void BindStructuralFrameInspection()
    {
        if (!HasStructuralFramePresentation) return;
        UnbindStructuralFrameInspection();
        structuralFrameInspectButton.onClick.AddListener(OpenStructuralFrameInspection);
        structuralFrameCloseButton.onClick.AddListener(CloseStructuralFrameInspection);
    }

    private void UnbindStructuralFrameInspection()
    {
        if (structuralFrameInspectButton != null) structuralFrameInspectButton.onClick.RemoveListener(OpenStructuralFrameInspection);
        if (structuralFrameCloseButton != null) structuralFrameCloseButton.onClick.RemoveListener(CloseStructuralFrameInspection);
        CloseStructuralFrameInspection();
    }

    private void RefreshStructuralFrameInspection(RunContext run)
    {
        if (!HasStructuralFramePresentation)
        {
            if (Application.isPlaying && !reportedStructuralFrameLayoutError)
                Debug.LogError("PF_ExpeditionMapInventoryMenu/InventoryRoot: restore Structural Frame header button, authored inspection root/text/close and localization references. No fallback UI is created.", this);
            reportedStructuralFrameLayoutError = true;
            return;
        }
        structuralFrameInspectButton.interactable = inventoryTab == InventoryContentTab.Equipment && run != null && run.IsActive;
        structuralFrameCloseButton.GetComponentInChildren<TMP_Text>(true).text = StructuralFrameText.Get("close", structuralFrameLocalization);
        if (run != null && run.IsActive)
            structuralFrameInspectionText.text = StructuralFrameText.RichDetails(run.FrameProfile, structuralFrameLocalization) +
                "\n\n" + StructuralFrameText.Get("fixed_at_launch", structuralFrameLocalization);
    }

    public void OpenStructuralFrameInspection()
    {
        if (!CanUseEquipmentActions) return;
        RunContext run = ResolveRunContext();
        RefreshStructuralFrameInspection(run);
        if (HasStructuralFramePresentation && run != null && run.IsActive) structuralFrameInspectionRoot.SetActive(true);
    }

    public void CloseStructuralFrameInspection()
    {
        if (structuralFrameInspectionRoot != null) structuralFrameInspectionRoot.SetActive(false);
    }
}
