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
                uiController.SelectRepair();
                break;

            case SettlementSelectionKind.Building:
                uiController.SelectBuilding(buildingType);
                break;

            case SettlementSelectionKind.Weapon:
                uiController.SelectWeapon(weaponTreeType);
                break;

            case SettlementSelectionKind.Launch:
                uiController.SelectLaunchPreparation();
                break;
        }

        if (executeImmediately)
        {
            uiController.ExecuteSelectedAction();
        }
    }
}