using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettlementSelectionButton : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private SettlementUIController uiController;

    [Header("Selection")]
    [SerializeField] private SettlementSelectionKind selectionKind;
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private WeaponTreeType weaponTreeType;
    [SerializeField] private TraitDefinition traitDefinition;
    [SerializeField] private string traitId;
    [SerializeField] private int traitIndex = -1;

    [Header("Option")]
    [SerializeField] private bool executeImmediately;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (uiController == null)
        {
            uiController = FindFirstObjectByType<SettlementUIController>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (uiController == null)
        {
            return;
        }

        switch (selectionKind)
        {
            case SettlementSelectionKind.Repair:
            case SettlementSelectionKind.OpenRepairPanel:
                uiController.ShowRepairPanel();
                break;

            case SettlementSelectionKind.OpenTraitPanel:
                uiController.ShowTraitPanel();
                break;

            case SettlementSelectionKind.OpenSettingsPanel:
                uiController.ShowSettingsPanel();
                break;

            case SettlementSelectionKind.BackToMain:
                uiController.ShowMainPanel();
                break;

            case SettlementSelectionKind.CloseSettings:
                uiController.CloseSettingsAndReturnMain();
                break;

            case SettlementSelectionKind.Building:
                uiController.SelectBuilding(buildingType);
                break;

            case SettlementSelectionKind.Trait:
                if (traitDefinition != null)
                {
                    uiController.SelectTrait(traitDefinition);
                }
                else if (!string.IsNullOrWhiteSpace(traitId))
                {
                    uiController.SelectTraitById(traitId);
                }
                else
                {
                    uiController.SelectTraitByIndex(traitIndex);
                }
                break;

            case SettlementSelectionKind.Weapon:
                uiController.SelectWeapon(weaponTreeType);
                break;

            case SettlementSelectionKind.Launch:
                uiController.LaunchExpedition();
                return;

            case SettlementSelectionKind.ShipPrevious:
                uiController.MovePreviewShipPrevious();
                break;

            case SettlementSelectionKind.ShipNext:
                uiController.MovePreviewShipNext();
                break;

            case SettlementSelectionKind.ShipAction:
                uiController.ExecuteShipAction();
                break;
            case SettlementSelectionKind.BuildingPrevious:
                uiController.MoveBuildingPrevious();
                break;

            case SettlementSelectionKind.BuildingNext:
                uiController.MoveBuildingNext();
                break;
        }

        if (executeImmediately)
        {
            uiController.ExecuteSelectedAction();
        }
    }
}
