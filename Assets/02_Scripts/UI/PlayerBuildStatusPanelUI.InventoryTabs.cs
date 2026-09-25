using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum InventoryContentTab { Equipment, Cargo }

// Presentation state only. PlayerBuildStatusPanelUI retains every gameplay operation.
public partial class PlayerBuildStatusPanelUI
{
    [Header("Authored Inventory Tabs")]
    [SerializeField] private Button equipmentTabButton;
    [SerializeField] private Button cargoTabButton;
    [SerializeField] private GameObject equipmentContentRoot;
    [SerializeField] private GameObject cargoContentRoot;
    [SerializeField] private TMP_Text runResourcesText;
    [SerializeField] private TMP_Text cargoReturnProjectionText;
    [SerializeField] private ScrollRect equipmentDetailScrollRect;
    [SerializeField] private ScrollRect activeEffectsScrollRect;

    private InventoryContentTab inventoryTab = InventoryContentTab.Equipment;
    public InventoryContentTab CurrentInventoryTab => inventoryTab;
    public bool HasInventoryTabPresentation => equipmentTabButton != null && cargoTabButton != null &&
        equipmentContentRoot != null && cargoContentRoot != null;
    private bool CanUseEquipmentActions => isOpen && !IsStoryProgressOpen && inventoryTab == InventoryContentTab.Equipment &&
        equipmentContentRoot != null && equipmentContentRoot.activeInHierarchy;
    private bool CanUseCargoActions => isOpen && !IsStoryProgressOpen && inventoryTab == InventoryContentTab.Cargo &&
        cargoContentRoot != null && cargoContentRoot.activeInHierarchy;

    public Selectable FirstInventorySelectable => inventoryTab == InventoryContentTab.Cargo
        ? FirstCargoSelectable ?? cargoTabButton
        : equipmentTabButton;

    internal static string InventoryText(string ko, string en) =>
        GameSettingsRuntime.LanguageCode == "en" ? en : ko;

    private void BindInventoryTabs()
    {
        if (!HasInventoryTabPresentation) return;
        equipmentTabButton.onClick.AddListener(ShowEquipmentTab);
        cargoTabButton.onClick.AddListener(ShowCargoTab);
    }

    private void UnbindInventoryTabs()
    {
        if (equipmentTabButton != null) equipmentTabButton.onClick.RemoveListener(ShowEquipmentTab);
        if (cargoTabButton != null) cargoTabButton.onClick.RemoveListener(ShowCargoTab);
    }

    public void ShowEquipmentTab() => ShowInventoryTab(InventoryContentTab.Equipment);
    public void ShowCargoTab() => ShowInventoryTab(InventoryContentTab.Cargo);

    private void ShowInventoryTab(InventoryContentTab tab)
    {
        if (!HasInventoryTabPresentation) return;
        inventoryTab = tab;
        CloseStoryProgressInspection();
        CloseStructuralFrameInspection();
        StopInventoryScrolling();
        cargoJettisonHoldTimer = 0f;
        // A held G cannot carry a partly completed action across a tab change.
        cargoJettisonConsumedUntilRelease = true;
        ApplyInventoryTabPresentation();
        ResolveVisibleFieldDropTarget();
        if (!isOpen) return;
        RefreshAll();
        RefreshFieldDropSelectionVisual();
        FocusInventoryTab();
    }

    private void ResolveVisibleFieldDropTarget()
    {
        selectedFieldDropTarget = inventoryTab == InventoryContentTab.Cargo
            ? BuildStatusFieldDropTarget.Cargo
            : selectedPassiveIndex >= 0 ? BuildStatusFieldDropTarget.Passive : BuildStatusFieldDropTarget.Active;
    }

    private void ApplyInventoryTabPresentation()
    {
        if (!HasInventoryTabPresentation) return; // No generated fallback hierarchy.
        bool equipment = inventoryTab == InventoryContentTab.Equipment;
        equipmentContentRoot.SetActive(equipment);
        cargoContentRoot.SetActive(!equipment);
        equipmentTabButton.GetComponentInChildren<TMP_Text>(true).text =
            InventoryText("장비", "Equipment") + (equipment ? " ✓" : "");
        cargoTabButton.GetComponentInChildren<TMP_Text>(true).text =
            InventoryText("적재 자원", "Cargo Resources") + (!equipment ? " ✓" : "");
        SetButtonLabel(activeFieldDropButton, InventoryText("드랍", "Drop"));
        SetButtonLabel(passiveFieldDropButton, InventoryText("장비 필드 드랍", "Drop equipment"));
        SetButtonLabel(cargoQuantityOneButton, InventoryText("1개", "1"));
        SetButtonLabel(cargoQuantityHalfButton, InventoryText("절반", "Half"));
        SetButtonLabel(cargoQuantityMaxButton, InventoryText("최대", "Max"));
        SetButtonLabel(cargoJettisonButton, InventoryText("버리기", "Jettison"));
        if (selectedCargoEmptyText != null) selectedCargoEmptyText.text =
            InventoryText("보유 중인 화물이 없습니다.", "No cargo resources.");
        SetTabColor(equipmentTabButton, equipment);
        SetTabColor(cargoTabButton, !equipment);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button != null) button.GetComponentInChildren<TMP_Text>(true).text = label;
    }

    private static void SetTabColor(Button button, bool selected)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? new Color(1f, .82f, .3f) : Color.white;
        colors.highlightedColor = new Color(.4f, .75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    private void StopInventoryScrolling()
    {
        passiveScrollRect?.StopMovement();
        equipmentDetailScrollRect?.StopMovement();
        activeEffectsScrollRect?.StopMovement();
    }

    private void FocusInventoryTab()
    {
        Selectable target = FirstInventorySelectable;
        if (EventSystem.current != null && target != null && target.IsActive() && target.IsInteractable())
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private void RefreshInventoryResourceReadouts()
    {
        RunContext run = ResolveRunContext();
        if (runResourcesText != null)
            runResourcesText.text = InventoryText("크레딧", "Credits") + " " + (run?.Wallet?.Credits ?? 0) +
                "  ·  " + InventoryText("튜닝 칩", "Tuning Chips") + " " + (run?.Wallet?.TuningChips ?? 0);
        if (cargoReturnProjectionText != null && cargoController != null)
            cargoReturnProjectionText.text = StatPresentation.Rich(StatCategory.Cargo,
                InventoryText("긴급복귀 예상 보존", "Emergency Return") + "\n" +
                InventoryText("현재 적재", "Current load") + " " + cargoController.CurrentLoad + "  ·  " +
                InventoryText("보존 한도", "Retention limit") + " " + cargoController.EmergencyReturnCapacityLimit);
    }
}
