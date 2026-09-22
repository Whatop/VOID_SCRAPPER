using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ShipTraitTreePanel
{
    [Header("Operating Frame - separate pre-run selection")]
    [SerializeField] private TMP_Text operatingFrameHeading;
    [SerializeField] private Button[] operatingFrameButtons;
    [SerializeField] private TMP_Text[] operatingFrameLabels;
    private bool inspectingOperatingFrame = true;
    private bool operatingFrameSaveFailed;
    private static readonly OperatingFrameType[] FrameOrder =
        { OperatingFrameType.Lightweight, OperatingFrameType.Standard, OperatingFrameType.Heavy };

    public bool ValidateOperatingFramePresentation(List<string> errors)
    {
        if (operatingFrameHeading == null || operatingFrameButtons == null || operatingFrameButtons.Length != 3 ||
            operatingFrameLabels == null || operatingFrameLabels.Length != 3)
        {
            errors.Add("Settlement/EquipmentDevelopment/OperatingFrames: bind the authored heading and three Lightweight/Standard/Heavy buttons and labels. No fallback UI is created.");
            return false;
        }
        for (int i = 0; i < 3; i++)
        {
            if (operatingFrameButtons[i] == null || operatingFrameLabels[i] == null ||
                !operatingFrameButtons[i].transform.IsChildOf(equipmentDevelopmentRoot.transform))
            {
                errors.Add("Settlement/EquipmentDevelopment/OperatingFrames: invalid button/label at index " + i);
                return false;
            }
        }
        return true;
    }

    private void BindOperatingFrames()
    {
        operatingFrameButtons[0].onClick.AddListener(SelectLightweight);
        operatingFrameButtons[1].onClick.AddListener(SelectStandard);
        operatingFrameButtons[2].onClick.AddListener(SelectHeavy);
    }

    private void UnbindOperatingFrames()
    {
        if (operatingFrameButtons == null || operatingFrameButtons.Length != 3) return;
        if (operatingFrameButtons[0] != null) operatingFrameButtons[0].onClick.RemoveListener(SelectLightweight);
        if (operatingFrameButtons[1] != null) operatingFrameButtons[1].onClick.RemoveListener(SelectStandard);
        if (operatingFrameButtons[2] != null) operatingFrameButtons[2].onClick.RemoveListener(SelectHeavy);
    }

    private void SelectLightweight() => SelectOperatingFrame(OperatingFrameType.Lightweight);
    private void SelectStandard() => SelectOperatingFrame(OperatingFrameType.Standard);
    private void SelectHeavy() => SelectOperatingFrame(OperatingFrameType.Heavy);

    public bool SelectOperatingFrame(OperatingFrameType type)
    {
        if (!CanUseTraitInput || PermanentProgress.Instance == null) return false;
        inspectingOperatingFrame = true;
        inspectedEquipment = null;
        operatingFrameSaveFailed = !PermanentProgress.Instance.TrySetOperatingFrame(type);
        RefreshEquipmentPresentation();
        equipmentGrowthScroll.StopMovement();
        equipmentGrowthScroll.content.anchoredPosition = Vector2.zero;
        return !operatingFrameSaveFailed;
    }

    private void RefreshOperatingFrames()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        OperatingFrameType selected = progress != null ? progress.SelectedOperatingFrame : OperatingFrameType.Standard;
        operatingFrameHeading.text = OperatingFrameText.Get("title", equipmentLocalization);
        for (int i = 0; i < 3; i++)
        {
            bool chosen = selected == FrameOrder[i];
            operatingFrameLabels[i].text = OperatingFrameText.Name(FrameOrder[i], equipmentLocalization);
            operatingFrameLabels[i].color = chosen ? SettlementSelectionColors.Selected : Color.white;
            EquipmentColors(operatingFrameButtons[i], chosen);
            operatingFrameButtons[i].interactable = progress != null && progress.CanEditEquipment;
        }
    }

    private void RefreshOperatingFrameDetails()
    {
        RefreshManufacturingCosts(null);
        OperatingFrameProfile frame = PermanentProgress.Instance != null ? PermanentProgress.Instance.ProjectedOperatingFrame :
            new OperatingFrameProfile(OperatingFrameType.Standard, 0);
        equipmentName.text = OperatingFrameText.Name(frame.Type, equipmentLocalization);
        equipmentIcon.enabled = false;
        equipmentCompatibility.text = OperatingFrameText.Get("title", equipmentLocalization);
        equipmentDetails.text = OperatingFrameText.Identity(frame.Type, equipmentLocalization);
        equipmentRequirements.text = OperatingFrameText.Count(frame, equipmentLocalization);
        string tier = OperatingFrameText.Tier(frame, equipmentLocalization);
        if (tier.Length > 0) equipmentRequirements.text += "\n" + tier;
        equipmentMaxLevel.text = OperatingFrameText.Get("fixed_at_launch", equipmentLocalization);
        equipmentGrowthHeading.gameObject.SetActive(true);
        equipmentGrowthHeading.text = OperatingFrameText.Get("projection", equipmentLocalization);
        for (int i = 0; i < equipmentGrowthRows.Length; i++)
        {
            string effects = i < 2 ? OperatingFrameText.Effects(frame, i == 1, equipmentLocalization) : string.Empty;
            EquipmentGrowthRow row = equipmentGrowthRows[i];
            row.root.SetActive(effects.Length > 0);
            if (effects.Length == 0) continue;
            row.heading.text = OperatingFrameText.Get(i == 0 ? "bonuses" : "penalties", equipmentLocalization);
            row.effects.text = effects;
        }
        equipmentCandidateState.text = operatingFrameSaveFailed ? EquipmentText("save_failed") : OperatingFrameText.Get("free_selection", equipmentLocalization);
        equipmentActivationButton.gameObject.SetActive(false);
    }
}
