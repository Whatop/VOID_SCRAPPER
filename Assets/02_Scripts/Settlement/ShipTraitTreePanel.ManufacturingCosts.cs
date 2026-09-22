using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class EquipmentManufacturingCostRow
{
    public GameObject root;
    public Image icon;
    public TMP_Text amount;
}

public partial class ShipTraitTreePanel
{
    [Header("Authored manufacturing costs")]
    [SerializeField] private TMP_Text equipmentCostHeading;
    [SerializeField] private EquipmentManufacturingCostRow equipmentScrapCost;
    [SerializeField] private EquipmentManufacturingCostRow equipmentCoreCost;

    private void ValidateManufacturingCosts(List<string> errors)
    {
        if (equipmentCostHeading == null || !ValidCostRow(equipmentScrapCost) || !ValidCostRow(equipmentCoreCost))
            errors.Add("Settlement/EquipmentDevelopment/Inspection/Growth/Content: bind ManufacturingCost, ScrapCost and CoreCost (root, currency icon, amount). No runtime replacement is created.");
    }

    private bool ValidCostRow(EquipmentManufacturingCostRow row) => row != null && row.root != null &&
        row.icon != null && row.icon.sprite != null && row.amount != null && row.root.transform.IsChildOf(transform);

    private void RefreshManufacturingCosts(TraitDefinition trait)
    {
        PermanentProgress progress = PermanentProgress.Instance;
        bool visible = trait != null && progress != null && trait.IsDevelopmentRoster && trait.HasValidManufacturingRecipe &&
            progress.IsEquipmentResearched(trait) && !progress.IsEquipmentManufactured(trait.TraitId);
        equipmentCostHeading.gameObject.SetActive(visible);
        equipmentCostHeading.text = EquipmentText("cost_heading");
        RefreshCostRow(equipmentScrapCost, visible ? trait.ManufacturingScrapCost : 0, progress != null ? progress.ScrapParts : 0);
        RefreshCostRow(equipmentCoreCost, visible ? trait.ManufacturingCoreCost : 0, progress != null ? progress.CoreShards : 0);
    }

    private void RefreshCostRow(EquipmentManufacturingCostRow row, int cost, int owned)
    {
        row.root.SetActive(cost > 0);
        if (cost <= 0) return;
        row.amount.text = EquipmentText("cost_owned").Replace("{cost}", cost.ToString()).Replace("{owned}", owned.ToString());
        row.amount.color = owned >= cost ? Color.white : new Color(1f, .5f, .42f);
    }
}
