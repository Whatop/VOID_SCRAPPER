using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class PreparedEquipmentView
{
    public Button button;
    public TMP_Text label;
    public Image icon;
    public TraitDefinition definition;
    [NonSerialized] public UnityAction action;
}

[Serializable]
public sealed class EquipmentGrowthRow
{
    public GameObject root;
    public TMP_Text heading;
    public TMP_Text effects;
}

public partial class ShipTraitTreePanel
{
    [Header("Equipment Development")]
    [SerializeField] private bool equipmentDevelopmentMode;
    [SerializeField] private GameObject equipmentDevelopmentRoot;
    [SerializeField] private PreparedEquipmentView[] equipmentSlots;
    [SerializeField] private PreparedEquipmentView[] equipmentCandidates;
    [SerializeField] private TMP_Text equipmentHeading;
    [SerializeField] private TMP_Text equipmentHint;
    [SerializeField] private TMP_Text equipmentDetails;
    [SerializeField] private TMP_Text[] equipmentResearchLabels;
    [SerializeField] private Button clearEquipmentButton;
    [SerializeField] private LocalizationCatalog equipmentLocalization;
    [SerializeField] private TMP_Text equipmentName;
    [SerializeField] private Image equipmentIcon;
    [SerializeField] private TMP_Text equipmentCompatibility;
    [SerializeField] private TMP_Text equipmentMaxLevel;
    [SerializeField] private TMP_Text equipmentGrowthHeading;
    [SerializeField] private TMP_Text equipmentCandidateState;
    [SerializeField] private Button equipmentActivationButton;
    [SerializeField] private EquipmentGrowthRow[] equipmentGrowthRows;
    [SerializeField] private ScrollRect equipmentGrowthScroll;
    private int selectedEquipmentSlot;
    private TraitDefinition inspectedEquipment;
    private bool equipmentBindingErrorReported;

    public bool IsEquipmentDevelopment => equipmentDevelopmentMode;
    public int SelectedEquipmentSlot => selectedEquipmentSlot;

    public bool ValidateEquipmentPresentation(List<string> errors)
    {
        int start = errors.Count;
        if (equipmentDevelopmentRoot == null || equipmentHeading == null || equipmentHint == null || equipmentDetails == null ||
            equipmentName == null || equipmentIcon == null || equipmentCompatibility == null || equipmentMaxLevel == null ||
            equipmentGrowthHeading == null || equipmentCandidateState == null || equipmentActivationButton == null ||
            equipmentGrowthScroll == null || equipmentGrowthRows == null || equipmentGrowthRows.Length == 0 || equipmentLocalization == null || equipmentSlots == null || equipmentSlots.Length != 12 ||
            equipmentResearchLabels == null || equipmentResearchLabels.Length != 4 || equipmentCandidates == null || equipmentCandidates.Length == 0)
            errors.Add("Equipment Development requires authored slots, inspection, growth rows and activation controls.");
        if (equipmentGrowthRows != null)
        {
            foreach (var row in equipmentGrowthRows)
                if (row == null || row.root == null || row.heading == null || row.effects == null)
                    errors.Add("Equipment Development has an invalid growth row.");
            if (equipmentCandidates != null)
                foreach (var view in equipmentCandidates)
                    if (view?.definition != null && view.definition.MaxLevel > equipmentGrowthRows.Length)
                        errors.Add("Author more growth rows for " + view.definition.TraitId);
        }
        foreach (var views in new[] { equipmentSlots, equipmentCandidates })
            if (views != null) foreach (var view in views)
                if (view == null || view.button == null || view.label == null || view.icon == null ||
                    !view.button.transform.IsChildOf(transform)) errors.Add("Equipment Development has an invalid authored slot/catalog view.");
        if (equipmentResearchLabels != null)
            foreach (TMP_Text label in equipmentResearchLabels)
                if (label == null) errors.Add("Equipment Development has a missing research label.");
        return errors.Count == start;
    }

    private void BindEquipmentPresentation()
    {
        var errors = new List<string>();
        if (!ValidateEquipmentPresentation(errors)) { Debug.LogError(string.Join("\n", errors), this); return; }
        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            int slot = i;
            var view = equipmentSlots[i];
            view.action ??= () => SelectEquipmentSlot(slot);
            view.button.onClick.RemoveListener(view.action);
            view.button.onClick.AddListener(view.action);
        }
        foreach (var view in equipmentCandidates)
        {
            view.action ??= () => InspectEquipment(view.definition);
            view.button.onClick.RemoveListener(view.action);
            view.button.onClick.AddListener(view.action);
        }
        equipmentActivationButton.onClick.RemoveListener(ToggleInspectedEquipment);
        equipmentActivationButton.onClick.AddListener(ToggleInspectedEquipment);
        GameSettingsRuntime.Changed -= RefreshEquipmentPresentation;
        GameSettingsRuntime.Changed += RefreshEquipmentPresentation;
    }

    private void UnbindEquipmentPresentation()
    {
        GameSettingsRuntime.Changed -= RefreshEquipmentPresentation;
        if (equipmentActivationButton != null) equipmentActivationButton.onClick.RemoveListener(ToggleInspectedEquipment);
        foreach (var views in new[] { equipmentSlots, equipmentCandidates })
            if (views != null) foreach (var view in views)
                if (view?.button != null && view.action != null) view.button.onClick.RemoveListener(view.action);
    }

    public void SelectEquipmentSlot(int slot)
    {
        if (!CanUseTraitInput || PermanentProgress.Instance == null || slot < 0 || slot >= PermanentProgress.Instance.EquipmentLoadoutCapacity) return;
        selectedEquipmentSlot = slot;
        var ids = PermanentProgress.Instance.EquipmentLoadoutTraitIds;
        inspectedEquipment = slot < ids.Count ? PermanentProgress.Instance.EquipmentCatalog?.FindById(ids[slot]) : null;
        RefreshEquipmentPresentation();
    }

    public bool PrepareEquipment(TraitDefinition trait)
    {
        if (!CanUseTraitInput || PermanentProgress.Instance == null || (trait != null && !CanActivateEquipment(trait))) return false;
        inspectedEquipment = trait;
        bool prepared = PermanentProgress.Instance.TryPrepareEquipment(selectedEquipmentSlot, trait);
        if (prepared && SaveManager.Instance != null) SaveManager.Instance.Save(PermanentProgress.Instance);
        RefreshEquipmentPresentation();
        return prepared;
    }

    public void InspectEquipment(TraitDefinition trait)
    {
        if (!CanUseTraitInput) return;
        inspectedEquipment = trait;
        RefreshEquipmentPresentation();
        equipmentGrowthScroll.StopMovement();
        equipmentGrowthScroll.content.anchoredPosition = Vector2.zero;
    }

    public bool IsEquipmentResearchLocked(TraitDefinition trait)
    {
        PermanentProgress progress = PermanentProgress.Instance;
        ShipDefinition ship = progress?.GetEquipmentShip(trait);
        return ship != null && !ship.UnlockedByDefault &&
            (progress.AnalyzedEquipmentComponentCount < ship.RequiredAnalyzedComponents || !progress.HasUnlockFlag(ship.UnlockFlag));
    }

    private int FindPreparedSlot(TraitDefinition trait)
    {
        var ids = PermanentProgress.Instance?.EquipmentLoadoutTraitIds;
        if (trait != null && ids != null)
            for (int i = 0; i < ids.Count; i++) if (ids[i] == trait.TraitId) return i;
        return -1;
    }

    private int FindEmptyEquipmentSlot()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null) return -1;
        var ids = progress.EquipmentLoadoutTraitIds;
        if (selectedEquipmentSlot < ids.Count && string.IsNullOrEmpty(ids[selectedEquipmentSlot])) return selectedEquipmentSlot;
        for (int i = 0; i < progress.EquipmentLoadoutCapacity; i++)
            if (i >= ids.Count || string.IsNullOrEmpty(ids[i])) return i;
        return -1;
    }

    public bool CanActivateEquipment(TraitDefinition trait) => PermanentProgress.Instance != null &&
        PermanentProgress.Instance.CanPrepareEquipment(trait) && !IsEquipmentResearchLocked(trait);

    public bool TryToggleInspectedEquipment()
    {
        if (!CanUseTraitInput || inspectedEquipment == null || !CanActivateEquipment(inspectedEquipment)) return false;
        int slot = FindPreparedSlot(inspectedEquipment);
        bool active = slot >= 0;
        if (!active) slot = FindEmptyEquipmentSlot();
        if (slot < 0) return false; // Capacity is optional; never silently replace another prepared item.
        bool changed = PermanentProgress.Instance.TryPrepareEquipment(slot, active ? null : inspectedEquipment);
        if (changed && SaveManager.Instance != null) SaveManager.Instance.Save(PermanentProgress.Instance);
        selectedEquipmentSlot = slot;
        RefreshEquipmentPresentation();
        return changed;
    }

    private void ToggleInspectedEquipment() => TryToggleInspectedEquipment();

    private void RefreshEquipmentPresentation()
    {
        if (!equipmentDevelopmentMode) return;
        var errors = new List<string>();
        if (!ValidateEquipmentPresentation(errors))
        {
            if (!equipmentBindingErrorReported) Debug.LogError(string.Join("\n", errors), this);
            equipmentBindingErrorReported = true;
            return;
        }
        PermanentProgress progress = PermanentProgress.Instance;
        int capacity = progress != null ? progress.EquipmentLoadoutCapacity : 3;
        selectedEquipmentSlot = Mathf.Clamp(selectedEquipmentSlot, 0, capacity - 1);
        equipmentHeading.text = EquipmentText("title");
        int used = 0;
        if (progress != null) foreach (string id in progress.EquipmentLoadoutTraitIds) if (!string.IsNullOrEmpty(id)) used++;
        equipmentHint.text = EquipmentText("capacity").Replace("{used}", used.ToString()).Replace("{capacity}", capacity.ToString());
        if (clearEquipmentButton != null) clearEquipmentButton.gameObject.SetActive(false);
        for (int row = 0; row < 4; row++)
            equipmentResearchLabels[row].text = EquipmentText("row" + row) + (row * 3 < capacity ? "" : " · " + EquipmentText("locked"));
        var ids = progress?.EquipmentLoadoutTraitIds;
        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            TraitDefinition trait = ids != null && i < ids.Count ? progress.EquipmentCatalog?.FindById(ids[i]) : null;
            var view = equipmentSlots[i];
            view.button.interactable = i < capacity;
            view.label.text = trait != null ? trait.DisplayName : EquipmentText(i < capacity ? "empty" : "locked");
            view.icon.sprite = trait?.Icon; view.icon.enabled = trait != null && trait.Icon != null;
            view.label.color = i == selectedEquipmentSlot ? SettlementSelectionColors.Selected :
                i < capacity ? Color.white : new Color(.42f, .49f, .55f);
            EquipmentColors(view.button, i == selectedEquipmentSlot);
        }
        foreach (var view in equipmentCandidates)
        {
            view.button.gameObject.SetActive(true);
            bool locked = IsEquipmentResearchLocked(view.definition);
            bool prepared = progress != null && progress.IsEquipmentPrepared(view.definition.TraitId);
            view.label.text = view.definition.DisplayName + "\n" + EquipmentText(locked ? "locked" : prepared ? "active_short" : "inactive_short");
            view.icon.sprite = view.definition.Icon; view.icon.enabled = view.definition.Icon != null;
            view.button.interactable = true; // Locked records remain inspectable, never activatable.
            view.label.color = view.definition == inspectedEquipment ? SettlementSelectionColors.Selected : locked ? new Color(.6f, .65f, .7f) : Color.white;
            EquipmentColors(view.button, view.definition == inspectedEquipment);
        }
        RefreshEquipmentDetails();
    }

    private string UnlockCondition(TraitDefinition trait)
    {
        ShipDefinition ship = PermanentProgress.Instance?.GetEquipmentShip(trait);
        return ship != null ? EquipmentText("research" + ship.RequiredAnalyzedComponents) : EquipmentText("locked");
    }

    private void RefreshEquipmentDetails()
    {
        TraitDefinition trait = inspectedEquipment;
        bool locked = trait != null && IsEquipmentResearchLocked(trait);
        equipmentName.text = trait != null ? trait.DisplayName : EquipmentText("inspect");
        equipmentIcon.sprite = trait?.Icon;
        equipmentIcon.enabled = equipmentIcon.sprite != null;
        equipmentCompatibility.text = trait != null ? Compatibility(trait) : string.Empty;
        equipmentDetails.text = trait == null ? EquipmentText("instructions") : locked ? UnlockCondition(trait) : trait.Description;
        equipmentMaxLevel.text = trait == null ? string.Empty : locked ? EquipmentText("locked") :
            EquipmentText("max_level").Replace("{max}", trait.MaxLevel.ToString());
        equipmentGrowthHeading.gameObject.SetActive(trait != null && !locked);
        equipmentGrowthHeading.text = EquipmentText("growth");
        for (int i = 0; i < equipmentGrowthRows.Length; i++)
        {
            EquipmentGrowthRow row = equipmentGrowthRows[i];
            bool visible = trait != null && !locked && i < trait.MaxLevel;
            row.root.SetActive(visible);
            if (!visible) continue;
            row.heading.text = "Lv " + (i + 1) + (i + 1 == trait.MaxLevel ? " · MAX" : string.Empty);
            row.effects.text = TraitEffectTextUtility.BuildEffectText(trait, i + 1);
        }
        bool active = FindPreparedSlot(trait) >= 0;
        bool compatible = trait != null && PermanentProgress.Instance != null && PermanentProgress.Instance.CanPrepareEquipment(trait);
        equipmentCandidateState.text = trait == null || locked ? string.Empty : !compatible ?
            EquipmentText("select_ship").Replace("{ship}", Compatibility(trait)) : EquipmentText(active ? "active" : "inactive");
        equipmentActivationButton.gameObject.SetActive(trait != null && !locked && compatible);
        equipmentActivationButton.interactable = active || FindEmptyEquipmentSlot() >= 0;
        equipmentActivationButton.GetComponentInChildren<TMP_Text>(true).text =
            EquipmentText(active ? "deactivate" : FindEmptyEquipmentSlot() >= 0 ? "activate" : "full");
    }

    public string BuildEquipmentDetails(TraitDefinition trait)
    {
        if (trait == null) return EquipmentText("instructions");
        if (IsEquipmentResearchLocked(trait)) return trait.DisplayName + "\n" + UnlockCondition(trait);
        var text = new StringBuilder(trait.DisplayName).Append("\n").Append(Compatibility(trait)).Append("\nMax Lv ").Append(trait.MaxLevel).Append("\n");
        for (int level = 1; level <= trait.MaxLevel; level++)
        {
            text.Append("\nLv ").Append(level);
            if (level == trait.MaxLevel) text.Append(" · MAX");
            text.Append("\n").Append(TraitEffectTextUtility.BuildEffectText(trait, level)).Append("\n");
        }
        if (trait.Prerequisites != null && trait.Prerequisites.Count > 0)
        {
            text.Append("\n").Append(EquipmentText("prerequisites"));
            foreach (var prerequisite in trait.Prerequisites)
                if (prerequisite?.Trait != null) text.Append("\n").Append(prerequisite.Trait.DisplayName).Append(" Lv ").Append(prerequisite.RequiredLevel);
        }
        return text.ToString();
    }

    private string Compatibility(TraitDefinition trait) => trait.Category == TraitCategory.Shared ? EquipmentText("shared") :
        EquipmentText(trait.WeaponTreeType == WeaponTreeType.MachineGun ? "sweeper" : trait.WeaponTreeType == WeaponTreeType.Shotgun ? "breacher" : "lancer");

    private string EquipmentText(string suffix)
    {
        string key = "ui.settlement.equipment." + suffix;
        if (VoidScrapperLocalizationService.HasInstance) return VoidScrapperLocalizationService.Instance.GetText(key);
        return equipmentLocalization != null && equipmentLocalization.TryGetText(key, GameSettingsRuntime.LanguageCode, out string text, out _) ? text : string.Empty;
    }

    private static void EquipmentColors(Button button, bool selected)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? SettlementSelectionColors.SelectedBackground : new Color(.07f, .11f, .15f);
        colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
        colors.pressedColor = SettlementSelectionColors.SelectedBackground;
        colors.disabledColor = selected ? SettlementSelectionColors.SelectedBackground : new Color(.025f, .04f, .055f);
        button.colors = colors;
    }
}
