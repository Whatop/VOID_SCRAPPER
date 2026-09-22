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
    public TMP_Text fittedIndicator;
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
    // Reuse the twelve authored card objects as blueprint positions, never player-filled slots.
    [SerializeField] private PreparedEquipmentView[] equipmentSlots;
    [SerializeField] private PreparedEquipmentView[] equipmentCandidates;
    [SerializeField] private TMP_Text equipmentHeading;
    [SerializeField] private TMP_Text equipmentHint;
    [SerializeField] private TMP_Text equipmentDetails;
    [SerializeField] private TMP_Text[] equipmentResearchLabels;
    // Retired ClearSlot becomes the subordinate legacy-ownership section toggle.
    [SerializeField] private Button clearEquipmentButton;
    [SerializeField] private LocalizationCatalog equipmentLocalization;
    [SerializeField] private TMP_Text equipmentName;
    [SerializeField] private Image equipmentIcon;
    [SerializeField] private TMP_Text equipmentCompatibility;
    [SerializeField] private TMP_Text equipmentMaxLevel;
    [SerializeField] private TMP_Text equipmentGrowthHeading;
    [SerializeField] private TMP_Text equipmentCandidateState;
    [SerializeField] private TMP_Text equipmentRequirements;
    [SerializeField] private Button equipmentActivationButton;
    [SerializeField] private EquipmentGrowthRow[] equipmentGrowthRows;
    [SerializeField] private ScrollRect equipmentGrowthScroll;
    [SerializeField] private ScrollRect equipmentLegacyScroll;
    private TraitDefinition inspectedEquipment;
    private bool equipmentBindingErrorReported;
    private bool showingLegacyEquipment;
    private EquipmentDevelopmentResult lastEquipmentResult = EquipmentDevelopmentResult.Success;

    public bool IsEquipmentDevelopment => equipmentDevelopmentMode;
    public TraitDefinition InspectedEquipment => inspectedEquipment;
    private ShipTraitBranchTabButton[] EquipmentTabs() => new[] { sharedTabButton, machineGunTabButton, shotgunTabButton, sniperTabButton };

    public bool ValidateEquipmentPresentation(List<string> errors)
    {
        int start = errors.Count;
        if (equipmentDevelopmentRoot == null || equipmentHeading == null || equipmentHint == null || equipmentDetails == null ||
            equipmentName == null || equipmentIcon == null || equipmentCompatibility == null || equipmentMaxLevel == null ||
            equipmentGrowthHeading == null || equipmentCandidateState == null || equipmentActivationButton == null ||
            equipmentRequirements == null || equipmentLegacyScroll == null || clearEquipmentButton == null ||
            equipmentGrowthScroll == null || equipmentGrowthRows == null || equipmentGrowthRows.Length == 0 ||
            equipmentLocalization == null || equipmentSlots == null || equipmentSlots.Length != 12 ||
            equipmentResearchLabels == null || equipmentResearchLabels.Length != 4 || equipmentCandidates == null || equipmentCandidates.Length == 0)
            errors.Add("Settlement/EquipmentDevelopment: restore the authored 12 blueprint cards, four row labels, legacy scroll, requirements and inspection/action references. No fallback UI will be created.");
        foreach (var tab in EquipmentTabs())
        {
            if (tab == null || !tab.transform.IsChildOf(equipmentDevelopmentRoot != null ? equipmentDevelopmentRoot.transform : transform))
                errors.Add("Settlement/EquipmentDevelopment: missing or misplaced Shared/Sweeper/Breacher/Lancer tab.");
            else tab.CollectPresentationErrors(errors);
        }
        if (equipmentGrowthRows != null)
        {
            foreach (var row in equipmentGrowthRows)
                if (row == null || row.root == null || row.heading == null || row.effects == null)
                    errors.Add("Settlement/EquipmentDevelopment/Inspection/Growth: invalid level row binding.");
            if (equipmentCandidates != null)
                foreach (var view in equipmentCandidates)
                    if (view?.definition != null && view.definition.MaxLevel > equipmentGrowthRows.Length)
                        errors.Add("Settlement/EquipmentDevelopment: author more growth rows for " + view.definition.TraitId);
        }
        foreach (var views in new[] { equipmentSlots, equipmentCandidates })
            if (views != null) foreach (var view in views)
                if (view == null || view.button == null || view.label == null || view.icon == null || view.fittedIndicator == null ||
                    !view.button.transform.IsChildOf(transform)) errors.Add("Settlement/EquipmentDevelopment: invalid card/icon/label/fitted-indicator binding.");
        if (equipmentResearchLabels != null)
            foreach (TMP_Text label in equipmentResearchLabels)
                if (label == null) errors.Add("Settlement/EquipmentDevelopment: missing research row label.");
        ValidateOperatingFramePresentation(errors);
        ValidateManufacturingCosts(errors);
        return errors.Count == start;
    }

    private void BindEquipmentPresentation()
    {
        var errors = new List<string>();
        if (!ValidateEquipmentPresentation(errors)) { Debug.LogError(string.Join("\n", errors), this); return; }
        UnbindOperatingFrames();
        BindOperatingFrames();
        foreach (var views in new[] { equipmentSlots, equipmentCandidates })
            foreach (var view in views)
            {
                view.action ??= () => InspectEquipment(view.definition);
                view.button.onClick.RemoveListener(view.action);
                view.button.onClick.AddListener(view.action);
            }
        foreach (var tab in EquipmentTabs())
        {
            tab.Bind(this, tab.BranchKind, BranchName(tab.BranchKind));
            tab.SetAllowClickWhenLocked(true);
        }
        equipmentActivationButton.onClick.RemoveListener(ExecuteEquipmentAction);
        equipmentActivationButton.onClick.AddListener(ExecuteEquipmentAction);
        clearEquipmentButton.onClick.RemoveListener(ToggleLegacyEquipment);
        clearEquipmentButton.onClick.AddListener(ToggleLegacyEquipment);
        GameSettingsRuntime.Changed -= RefreshEquipmentPresentation;
        GameSettingsRuntime.Changed += RefreshEquipmentPresentation;
    }

    private void UnbindEquipmentPresentation()
    {
        UnbindOperatingFrames();
        GameSettingsRuntime.Changed -= RefreshEquipmentPresentation;
        if (equipmentActivationButton != null) equipmentActivationButton.onClick.RemoveListener(ExecuteEquipmentAction);
        if (clearEquipmentButton != null) clearEquipmentButton.onClick.RemoveListener(ToggleLegacyEquipment);
        foreach (var views in new[] { equipmentSlots, equipmentCandidates })
            if (views != null) foreach (var view in views)
                if (view?.button != null && view.action != null) view.button.onClick.RemoveListener(view.action);
    }

    public void SelectEquipmentBranch(ShipTraitBranchKind branch)
    {
        if (!CanUseTraitInput) return;
        inspectingOperatingFrame = false;
        selectedBranch = branch;
        inspectedEquipment = null;
        showingLegacyEquipment = false;
        lastEquipmentResult = EquipmentDevelopmentResult.Success;
        RefreshEquipmentPresentation();
    }

    public void InspectEquipment(TraitDefinition trait)
    {
        if (!CanUseTraitInput || trait == null) return;
        inspectingOperatingFrame = false;
        inspectedEquipment = trait;
        selectedBranch = trait.DevelopmentBranch;
        lastEquipmentResult = EquipmentDevelopmentResult.Success;
        RefreshEquipmentPresentation();
        equipmentGrowthScroll.StopMovement();
        equipmentGrowthScroll.content.anchoredPosition = Vector2.zero;
    }

    private void ToggleLegacyEquipment()
    {
        if (!CanUseTraitInput) return;
        showingLegacyEquipment = !showingLegacyEquipment;
        RefreshEquipmentPresentation();
    }

    public bool IsEquipmentResearchLocked(TraitDefinition trait) => trait != null &&
        (PermanentProgress.Instance == null || !PermanentProgress.Instance.IsEquipmentResearched(trait));

    public bool TryToggleInspectedEquipment()
    {
        if (!CanUseTraitInput || inspectedEquipment == null || PermanentProgress.Instance == null) return false;
        PermanentProgress progress = PermanentProgress.Instance;
        lastEquipmentResult = progress.TrySetEquipmentFitted(inspectedEquipment, !progress.IsEquipmentFitted(inspectedEquipment.TraitId));
        RefreshEquipmentPresentation();
        return lastEquipmentResult == EquipmentDevelopmentResult.Success;
    }

    public EquipmentDevelopmentResult TryExecuteEquipmentAction()
    {
        if (!CanUseTraitInput || inspectedEquipment == null || PermanentProgress.Instance == null) return EquipmentDevelopmentResult.UnsafeState;
        PermanentProgress progress = PermanentProgress.Instance;
        lastEquipmentResult = progress.IsEquipmentManufactured(inspectedEquipment.TraitId)
            ? progress.TrySetEquipmentFitted(inspectedEquipment, !progress.IsEquipmentFitted(inspectedEquipment.TraitId))
            : progress.TryManufactureEquipment(inspectedEquipment);
        RefreshEquipmentPresentation();
        return lastEquipmentResult;
    }

    private void ExecuteEquipmentAction() => TryExecuteEquipmentAction();

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
        equipmentHeading.text = EquipmentText("title");
        int shared = 0, ship = 0, legacy = 0;
        if (progress != null)
            foreach (string id in progress.EquipmentLoadoutTraitIds)
            {
                TraitDefinition trait = progress.EquipmentCatalog?.FindById(id);
                if (!progress.IsEquipmentUsable(trait, progress.LastSelectedWeaponTree)) continue;
                if (trait.Category == TraitCategory.Shared) shared++; else ship++;
            }
        ShipTraitBranchKind currentShipBranch = progress?.LastSelectedWeaponTree == WeaponTreeType.Shotgun ? ShipTraitBranchKind.Shotgun :
            progress?.LastSelectedWeaponTree == WeaponTreeType.Sniper ? ShipTraitBranchKind.Sniper : ShipTraitBranchKind.MachineGun;
        equipmentHint.text = EquipmentText("fitted_count").Replace("{shared}", shared.ToString()).Replace("{ship}", BranchName(currentShipBranch))
            .Replace("{specific}", ship.ToString()).Replace("{total}", (shared + ship).ToString());
        foreach (var tab in EquipmentTabs())
        {
            tab.SetLabel(BranchName(tab.BranchKind));
            tab.SetVisualState(progress != null && progress.GetEquipmentResearchPositionCount(tab.BranchKind) > 0, selectedBranch == tab.BranchKind, true);
            EquipmentColors(tab.GetComponent<Button>(), selectedBranch == tab.BranchKind);
        }
        foreach (var view in equipmentSlots) view.definition = null;
        if (progress?.EquipmentCatalog != null)
            foreach (TraitDefinition trait in progress.EquipmentCatalog.TraitDefinitions)
                if (trait != null && trait.IsDevelopmentRoster && trait.HasValidDevelopmentMetadata && trait.DevelopmentBranch == selectedBranch)
                    equipmentSlots[trait.DevelopmentResearchTier * 3 + trait.DevelopmentDisplayOrder].definition = trait;
        foreach (var view in equipmentCandidates)
        {
            bool visible = view.definition != null && !view.definition.IsDevelopmentRoster && view.definition.DevelopmentBranch == selectedBranch &&
                progress != null && progress.IsEquipmentManufactured(view.definition.TraitId);
            if (visible) legacy++;
            view.button.gameObject.SetActive(visible && showingLegacyEquipment);
            if (visible) RefreshEquipmentCard(view);
        }
        if (legacy == 0) showingLegacyEquipment = false;
        clearEquipmentButton.gameObject.SetActive(legacy > 0);
        clearEquipmentButton.GetComponentInChildren<TMP_Text>(true).text = showingLegacyEquipment ? EquipmentText("blueprints") :
            EquipmentText("legacy_owned").Replace("{count}", legacy.ToString());
        equipmentLegacyScroll.gameObject.SetActive(showingLegacyEquipment);
        for (int row = 0; row < 4; row++)
        {
            equipmentResearchLabels[row].gameObject.SetActive(!showingLegacyEquipment);
            equipmentResearchLabels[row].text = EquipmentText("row" + row) +
                (progress == null || progress.GetEquipmentResearchPositionCount(selectedBranch) <= row * 3 ? " · " + EquipmentText("locked") : "");
        }
        foreach (var view in equipmentSlots)
        {
            view.button.gameObject.SetActive(!showingLegacyEquipment);
            RefreshEquipmentCard(view);
        }
        RefreshOperatingFrames();
        if (inspectingOperatingFrame) RefreshOperatingFrameDetails();
        else RefreshEquipmentDetails();
    }

    private void RefreshEquipmentCard(PreparedEquipmentView view)
    {
        TraitDefinition trait = view.definition;
        bool locked = IsEquipmentResearchLocked(trait);
        bool fitted = trait != null && PermanentProgress.Instance != null && PermanentProgress.Instance.IsEquipmentFitted(trait.TraitId);
        view.button.interactable = trait != null;
        view.label.text = trait == null ? EquipmentText("pending") : trait.DisplayName;
        view.icon.sprite = trait?.Icon;
        view.icon.enabled = view.icon.sprite != null;
        view.fittedIndicator.text = fitted ? EquipmentText("fitted_mark") : locked ? EquipmentText("locked_short") : string.Empty;
        view.fittedIndicator.color = fitted ? new Color(.5f, .95f, .65f) : new Color(.6f, .65f, .7f);
        bool selected = trait != null && trait == inspectedEquipment;
        view.label.color = selected ? SettlementSelectionColors.Selected : locked ? new Color(.55f, .62f, .68f) : Color.white;
        EquipmentColors(view.button, selected);
    }

    private string UnlockCondition(TraitDefinition trait)
    {
        int shipTier = trait.DevelopmentBranch == ShipTraitBranchKind.Shotgun ? 1 : trait.DevelopmentBranch == ShipTraitBranchKind.Sniper ? 2 : 0;
        return EquipmentText("research" + Mathf.Max(shipTier, trait.DevelopmentResearchTier));
    }

    private string BuildEquipmentRequirements(TraitDefinition trait, bool manufactured)
    {
        if (IsEquipmentResearchLocked(trait)) return UnlockCondition(trait);
        var text = new StringBuilder();
        if (!manufactured)
        {
            EquipmentDevelopmentResult availability = PermanentProgress.Instance.GetManufacturingAvailability(trait);
            if (!trait.IsDevelopmentRoster) text.Append(EquipmentText("pending"));
            else if (availability == EquipmentDevelopmentResult.InvalidRecipe) text.Append(EquipmentText("invalid_recipe"));
            if (availability == EquipmentDevelopmentResult.InsufficientResources)
                text.Append("\n").Append(EquipmentText("insufficient"));
        }
        if (trait.HasRuntimePrerequisites)
        {
            if (text.Length > 0) text.Append("\n");
            text.Append(EquipmentText("conditional"));
            foreach (TraitPrerequisite prerequisite in trait.Prerequisites)
            {
                if (prerequisite?.Trait == null) { text.Append("\n").Append(EquipmentText("invalid_recipe")); continue; }
                text.Append("\n").Append(prerequisite.Trait.DisplayName).Append(" Lv.").Append(prerequisite.RequiredLevel);
                if (!PermanentProgress.Instance.IsEquipmentFitted(prerequisite.Trait.TraitId) ||
                    !PermanentProgress.Instance.IsEquipmentUsable(prerequisite.Trait, trait.WeaponTreeType))
                    text.Append(" · ").Append(EquipmentText("prerequisite_missing"));
            }
        }
        if (trait.Category == TraitCategory.WeaponSpecific && PermanentProgress.Instance.LastSelectedWeaponTree != trait.WeaponTreeType)
        {
            if (text.Length > 0) text.Append("\n");
            text.Append(EquipmentText("select_ship").Replace("{ship}", Compatibility(trait)));
        }
        return text.ToString();
    }

    private void RefreshEquipmentDetails()
    {
        TraitDefinition trait = inspectedEquipment;
        RefreshManufacturingCosts(trait);
        PermanentProgress progress = PermanentProgress.Instance;
        bool locked = IsEquipmentResearchLocked(trait);
        bool manufactured = trait != null && progress != null && progress.IsEquipmentManufactured(trait.TraitId);
        bool fitted = trait != null && progress != null && progress.IsEquipmentFitted(trait.TraitId);
        equipmentName.text = trait != null ? trait.DisplayName : EquipmentText("inspect");
        equipmentIcon.sprite = trait?.Icon;
        equipmentIcon.enabled = equipmentIcon.sprite != null;
        equipmentCompatibility.text = trait != null ? Compatibility(trait) : string.Empty;
        equipmentDetails.text = trait != null ? trait.Description : EquipmentText("instructions");
        equipmentRequirements.text = trait != null && progress != null ? BuildEquipmentRequirements(trait, manufactured) : string.Empty;
        equipmentMaxLevel.text = trait == null ? string.Empty : locked ? EquipmentText("locked") :
            EquipmentText(trait.HasRuntimePrerequisites ? "conditional_level" : "max_level").Replace("{max}", trait.MaxLevel.ToString());
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
        equipmentCandidateState.text = trait == null || locked ? string.Empty : EquipmentText(fitted ?
            (trait.HasRuntimePrerequisites ? "conditional_fitted" : "active") : manufactured ? "manufactured" : "unmanufactured");
        if (lastEquipmentResult == EquipmentDevelopmentResult.SaveFailed) equipmentCandidateState.text = EquipmentText("save_failed");
        equipmentActivationButton.gameObject.SetActive(trait != null && !locked && (manufactured || trait.IsDevelopmentRoster));
        equipmentActivationButton.interactable = progress != null && progress.CanEditEquipment &&
            (manufactured || (trait != null && progress.GetManufacturingAvailability(trait) == EquipmentDevelopmentResult.Success));
        equipmentActivationButton.GetComponentInChildren<TMP_Text>(true).text = EquipmentText(manufactured ? fitted ? "deactivate" : "activate" : "manufacture");
    }

    public string BuildEquipmentDetails(TraitDefinition trait)
    {
        if (trait == null) return EquipmentText("instructions");
        if (IsEquipmentResearchLocked(trait)) return trait.DisplayName + "\n" + UnlockCondition(trait);
        var text = new StringBuilder(trait.DisplayName).Append("\n").Append(Compatibility(trait)).Append("\nMax Lv ").Append(trait.MaxLevel).Append("\n");
        for (int level = 1; level <= trait.MaxLevel; level++)
            text.Append("\nLv ").Append(level).Append(level == trait.MaxLevel ? " · MAX\n" : "\n")
                .Append(TraitEffectTextUtility.BuildEffectText(trait, level)).Append("\n");
        if (PermanentProgress.Instance != null) text.Append(BuildEquipmentRequirements(trait, PermanentProgress.Instance.IsEquipmentManufactured(trait.TraitId)));
        return text.ToString();
    }

    private string BranchName(ShipTraitBranchKind branch) => EquipmentText(branch == ShipTraitBranchKind.Shared ? "shared" :
        branch == ShipTraitBranchKind.MachineGun ? "sweeper" : branch == ShipTraitBranchKind.Shotgun ? "breacher" : "lancer");
    private string Compatibility(TraitDefinition trait) => BranchName(trait.DevelopmentBranch);

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
