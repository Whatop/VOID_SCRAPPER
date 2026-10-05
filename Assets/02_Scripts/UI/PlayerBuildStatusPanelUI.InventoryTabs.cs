using System.Collections.Generic;
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
    [SerializeField] private TMP_Text activeHeadingText;
    [SerializeField] private TMP_Text storageHeadingText;
    [SerializeField] private TMP_Text cargoHeadingText;

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
            InventoryText("장비", "Equipment");
        cargoTabButton.GetComponentInChildren<TMP_Text>(true).text =
            InventoryText("적재 자원", "Cargo Resources");
        if (activeHeadingText != null) activeHeadingText.text = InventoryText("현재 액티브", "Active Reinforcement");
        if (storageHeadingText != null) storageHeadingText.text = InventoryText("장비 보관함", "Equipment Storage");
        if (cargoHeadingText != null) cargoHeadingText.text = InventoryText("적재 / 자원", "Cargo / Resources");
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
        RefreshInventoryHeaderNavigation();
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button != null) button.GetComponentInChildren<TMP_Text>(true).text = label;
    }

    private static void SetTabColor(Button button, bool selected)
    {
        // Tint the authored Image, whose white base makes the ColorBlock visible.
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? new Color(.26f, .20f, .055f) : new Color(.055f, .12f, .16f);
        colors.highlightedColor = new Color(.08f, .32f, .48f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.GetComponentInChildren<TMP_Text>(true).color = selected ? new Color(1f, .84f, .35f) : Color.white;
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = selected;
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
                InventoryText("보존 한도", "Retention limit") + " " + cargoController.EmergencyReturnCapacityLimit);
    }

    private static string InventoryRarityText(TraitDefinition trait) =>
        GameSettingsRuntime.LanguageCode == "en" && !trait.IsResearchSpecialEquipment ? trait.Rarity.ToString() : trait.GetRarityText();

    private static bool Available(Selectable control) => control != null && control.IsActive() && control.IsInteractable();

    private static void Link(Selectable control, Selectable up, Selectable down, Selectable left = null, Selectable right = null)
    {
        if (control == null) return;
        control.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = Available(up) ? up : null, selectOnDown = Available(down) ? down : null,
            selectOnLeft = Available(left) ? left : null, selectOnRight = Available(right) ? right : null };
    }

    private void RefreshInventoryHeaderNavigation()
    {
        Selectable content = inventoryTab == InventoryContentTab.Cargo ? cargoShowAllButton : activeSlotSelectButton;
        Link(equipmentTabButton, structuralFrameInspectButton, content, null, cargoTabButton);
        Link(cargoTabButton, null, content, equipmentTabButton, storyProgressInspectButton);
        Link(storyProgressInspectButton, null, content, cargoTabButton);
        Link(structuralFrameInspectButton, null, equipmentTabButton);
    }

    private void RefreshEquipmentNavigation()
    {
        if (inventoryTab != InventoryContentTab.Equipment) return;
        SetButtonInteractable(activeFieldDropButton, reinforcementController != null && reinforcementController.HasEquipment);
        bool valid = selectedPassiveIndex >= 0 && selectedPassiveIndex < passiveEntries.Count &&
            passiveEntries[selectedPassiveIndex].IsOwned && passiveEntries[selectedPassiveIndex].trait.CanFieldDrop;
        SetButtonInteractable(passiveFieldDropButton, valid);
        Selectable first = passiveSlotInstances.Count > 0 ? passiveSlotInstances[0].Button : equipmentTabButton;
        Link(activeSlotSelectButton, equipmentTabButton, first, null, activeFieldDropButton);
        Link(activeFieldDropButton, equipmentTabButton, first, activeSlotSelectButton, passiveFieldDropButton);
        int columns = Mathf.Max(1, passiveColumnCount);
        for (int i = 0; i < passiveSlotInstances.Count; i++)
        {
            Selectable up = i >= columns ? passiveSlotInstances[i - columns].Button : activeSlotSelectButton;
            Selectable down = i + columns < passiveSlotInstances.Count ? passiveSlotInstances[i + columns].Button : equipmentTabButton;
            Selectable left = i % columns > 0 ? passiveSlotInstances[i - 1].Button : equipmentTabButton;
            Selectable right = i % columns < columns - 1 && i + 1 < passiveSlotInstances.Count
                ? passiveSlotInstances[i + 1].Button : passiveFieldDropButton;
            Link(passiveSlotInstances[i].Button, up, down, left, right);
        }
        Link(passiveFieldDropButton, activeFieldDropButton, equipmentTabButton,
            selectedPassiveIndex >= 0 && selectedPassiveIndex < passiveSlotInstances.Count ? passiveSlotInstances[selectedPassiveIndex].Button : first);
        RefreshInventoryHeaderNavigation();
    }

    private void LinkCargoNavigation()
    {
        var controls = new List<Selectable> { cargoShowAllButton };
        foreach (CargoManifestRowUI row in cargoManifestRows)
            if (row != null && row.IsVisible && Available(row.Selectable)) controls.Add(row.Selectable);
        foreach (Selectable action in new Selectable[] { cargoQuantitySlider, cargoQuantityOneButton,
            cargoQuantityHalfButton, cargoQuantityMaxButton, cargoAutoPickupButton, cargoJettisonButton })
            if (Available(action)) controls.Add(action);
        for (int i = 0; i < controls.Count; i++)
            Link(controls[i], i > 0 ? controls[i - 1] : cargoTabButton,
                i + 1 < controls.Count ? controls[i + 1] : cargoTabButton);
        RefreshInventoryHeaderNavigation();
    }
}
