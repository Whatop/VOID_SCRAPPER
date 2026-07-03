using UnityEngine;
using UnityEngine.UI;

public enum SettlementPanelKind
{
    Main,
    Repair,
    Trait
}

public enum SettlementSelectionKind
{
    None,
    Repair,
    Building,
    Trait,
    Weapon,
    Launch,
    ShipPrevious,
    ShipNext,
    ShipAction,
    BuildingPrevious,
    BuildingNext,
    OpenRepairPanel,
    OpenTraitPanel,
    OpenSettingsPanel,
    BackToMain,
    CloseSettings
}

public class SettlementUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementHUD hud;
    [SerializeField] private SettlementSettingsPanel settingsPanelController;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject repairPanel;
    [SerializeField] private GameObject traitPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Panel Buttons")]
    [SerializeField] private Button openRepairPanelButton;
    [SerializeField] private Button openTraitPanelButton;
    [SerializeField] private Button openSettingsPanelButton;
    [SerializeField] private Button shipPreviousButton;
    [SerializeField] private Button shipNextButton;
    [SerializeField] private Button shipActionButton;
    [SerializeField] private Button launchButton;

    [Header("Repair Panel Buttons")]
    [SerializeField] private Button repairPreviousButton;
    [SerializeField] private Button repairNextButton;
    [SerializeField] private Button repairActionButton;
    [SerializeField] private Button repairBackButton;

    [Header("Trait Panel Buttons")]
    [SerializeField] private Button traitActionButton;
    [SerializeField] private Button traitBackButton;

    [Header("Ship Trait Tree Panel")]
    [SerializeField] private bool useShipTraitTreePanel = true;
    [SerializeField] private ShipTraitTreePanel shipTraitTreePanel;

    [Header("Start")]
    [SerializeField] private SettlementPanelKind startPanel = SettlementPanelKind.Main;
    [SerializeField] private BuildingType defaultBuilding = BuildingType.Hangar;
    [SerializeField] private int defaultTraitIndex;

    private SettlementPanelKind currentPanel = SettlementPanelKind.Main;
    private SettlementSelectionKind selectedKind = SettlementSelectionKind.None;
    private BuildingType selectedBuilding;
    private WeaponTreeType selectedWeapon;
    private TraitDefinition selectedTrait;

    private readonly BuildingType[] buildingOrder =
    {
        BuildingType.Hangar,
        BuildingType.EngineWorkshop,
        BuildingType.WeaponLab,
        BuildingType.RecoveryProcessor
    };

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        if (hud == null)
        {
            hud = FindFirstObjectByType<SettlementHUD>();
        }

        if (settingsPanelController == null)
        {
            settingsPanelController = FindFirstObjectByType<SettlementSettingsPanel>();
        }

        if (shipTraitTreePanel == null && traitPanel != null)
        {
            shipTraitTreePanel = traitPanel.GetComponentInChildren<ShipTraitTreePanel>(true);
        }

        if (shipTraitTreePanel == null)
        {
            shipTraitTreePanel = FindFirstObjectByType<ShipTraitTreePanel>();
        }

        selectedBuilding = defaultBuilding;
        selectedTrait = GetTraitByIndex(defaultTraitIndex);

        if (settlementController != null)
        {
            selectedWeapon = settlementController.SelectedWeaponTree;
        }
    }

    private void OnEnable()
    {
        SubscribeController();
        SubscribeButtons();
    }

    private void Start()
    {
        ShowPanel(startPanel);
    }

    private void OnDisable()
    {
        UnsubscribeController();
        UnsubscribeButtons();
    }

    public void ShowMainPanel()
    {
        ShowPanel(SettlementPanelKind.Main);
    }

    public void ShowRepairPanel()
    {
        ShowPanel(SettlementPanelKind.Repair);
    }

    public void ShowTraitPanel()
    {
        ShowPanel(SettlementPanelKind.Trait);
    }

    public void ShowSettingsPanel()
    {
        OpenSettingsOverlay();
    }

    public void CloseSettingsAndReturnMain()
    {
        CloseSettingsOverlay();
    }
    private void OpenSettingsOverlay()
    {
        if (settingsPanelController != null)
        {
            settingsPanelController.Open();
        }
        else
        {
            SetPanelActive(settingsPanel, true);
        }

        Refresh();
    }

    private void CloseSettingsOverlay()
    {
        if (settingsPanelController != null)
        {
            settingsPanelController.Close();
        }
        else
        {
            SetPanelActive(settingsPanel, false);
        }

        if (MouseCursorManager.Instance != null)
        {
            MouseCursorManager.Instance.ResetToSceneDefault();
        }

        Refresh();
    }
    public void SelectRepair()
    {
        ShowRepairPanel();
    }

    public void SelectBuildingHangar()
    {
        SelectBuilding(BuildingType.Hangar);
    }

    public void SelectBuildingEngineWorkshop()
    {
        SelectBuilding(BuildingType.EngineWorkshop);
    }

    public void SelectBuildingWeaponLab()
    {
        SelectBuilding(BuildingType.WeaponLab);
    }

    public void SelectBuildingRecoveryProcessor()
    {
        SelectBuilding(BuildingType.RecoveryProcessor);
    }

    public void SelectBuilding(BuildingType buildingType)
    {
        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingType;
        ShowPanel(SettlementPanelKind.Repair);
    }

    public void SelectTrait(TraitDefinition trait)
    {
        selectedKind = SettlementSelectionKind.Trait;
        selectedTrait = trait;
        ShowPanel(SettlementPanelKind.Trait);
    }

    public void SelectTraitByIndex(int index)
    {
        SelectTrait(GetTraitByIndex(index));
    }

    public void SelectTraitById(string traitId)
    {
        if (settlementController == null)
        {
            return;
        }

        SelectTrait(settlementController.FindTraitDefinition(traitId));
    }

    public void SelectWeaponShotgun()
    {
        SelectWeapon(WeaponTreeType.Shotgun);
    }

    public void SelectWeaponSniper()
    {
        SelectWeapon(WeaponTreeType.Sniper);
    }

    public void SelectWeaponMachineGun()
    {
        SelectWeapon(WeaponTreeType.MachineGun);
    }

    public void SelectWeapon(WeaponTreeType weaponTreeType)
    {
        selectedKind = SettlementSelectionKind.Weapon;
        selectedWeapon = weaponTreeType;

        if (settlementController != null)
        {
            settlementController.SelectWeaponTree(weaponTreeType);
        }

        Refresh();
    }

    public void SelectLaunchPreparation()
    {
        selectedKind = SettlementSelectionKind.Launch;
        Refresh();
    }

    public void MovePreviewShipPrevious()
    {
        if (settlementController != null)
        {
            settlementController.MovePreviewShipPrevious();
        }

        Refresh();
    }

    public void MovePreviewShipNext()
    {
        if (settlementController != null)
        {
            settlementController.MovePreviewShipNext();
        }

        Refresh();
    }

    public void ExecuteShipAction()
    {
        if (settlementController != null)
        {
            settlementController.TryExecutePreviewShipAction();
        }

        Refresh();
    }

    public void ExecuteBuildingAction()
    {
        if (settlementController != null)
        {
            settlementController.TryRepairOrUpgradeBuilding(selectedBuilding);
        }

        Refresh();
    }

    public void ExecuteTraitAction()
    {
        TryExecuteCurrentTraitAction();
        Refresh();
    }

    public void ExecuteSelectedAction()
    {
        if (settlementController == null)
        {
            return;
        }

        switch (selectedKind)
        {
            case SettlementSelectionKind.Building:
                settlementController.TryRepairOrUpgradeBuilding(selectedBuilding);
                break;

            case SettlementSelectionKind.Trait:
                TryExecuteCurrentTraitAction();
                break;

            case SettlementSelectionKind.Weapon:
                settlementController.SelectWeaponTree(selectedWeapon);
                break;

            case SettlementSelectionKind.Launch:
                LaunchExpedition();
                break;

            case SettlementSelectionKind.ShipAction:
            case SettlementSelectionKind.None:
                if (currentPanel == SettlementPanelKind.Main)
                {
                    settlementController.TryExecutePreviewShipAction();
                }
                else if (currentPanel == SettlementPanelKind.Repair)
                {
                    settlementController.TryRepairOrUpgradeBuilding(selectedBuilding);
                }
                else if (currentPanel == SettlementPanelKind.Trait)
                {
                    TryExecuteCurrentTraitAction();
                }
                break;
        }

        Refresh();
    }

    public void LaunchExpedition()
    {
        if (settlementController == null)
        {
            return;
        }

        settlementController.LaunchExpedition();
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        if (hud != null)
        {
            hud.Refresh();
        }

        RefreshMainPanel();
        RefreshRepairPanel();
        RefreshTraitPanel();
        RefreshButtons();
    }

    private void ShowPanel(SettlementPanelKind panelKind)
    {
        currentPanel = panelKind;

        SetPanelActive(mainPanel, panelKind == SettlementPanelKind.Main);
        SetPanelActive(repairPanel, panelKind == SettlementPanelKind.Repair);
        SetPanelActive(traitPanel, panelKind == SettlementPanelKind.Trait);
        
        CloseSettingsOverlay();

        if (panelKind == SettlementPanelKind.Main)
        {
            selectedKind = SettlementSelectionKind.ShipAction;
        }
        else if (panelKind == SettlementPanelKind.Repair)
        {
            selectedKind = SettlementSelectionKind.Building;
        }
        else if (panelKind == SettlementPanelKind.Trait)
        {
            selectedKind = SettlementSelectionKind.Trait;

            if (selectedTrait == null)
            {
                selectedTrait = GetTraitByIndex(defaultTraitIndex);
            }

            if (UseShipTraitTreePanel())
            {
                shipTraitTreePanel.ClearTargetShipOverride();
            }
        }

        Refresh();
    }

    private void RefreshMainPanel()
    {
        if (hud == null || settlementController == null)
        {
            return;
        }

        ShipDefinition previewShip = settlementController.PreviewShip;
        string title = settlementController.GetPreviewShipTitle();
        string body = settlementController.BuildPreviewShipDetailText();
        string actionLabel = settlementController.GetShipActionLabel(previewShip);

        hud.SetMainShipDetail(title, body, actionLabel);
        hud.SetShipPreviewState(
            settlementController.GetPreviewShipSprite(),
            settlementController.PreviewShipIndex,
            GetShipCount()
        );
        hud.SetLaunchLabel("탐사 시작");
    }

    private void RefreshRepairPanel()
    {
        if (hud == null || settlementController == null)
        {
            return;
        }

        string title = settlementController.GetBuildingDisplayName(selectedBuilding);
        string body = settlementController.BuildBuildingDetailText(selectedBuilding);
        string actionLabel = settlementController.GetBuildingActionLabel(selectedBuilding);
        int currentLevel = settlementController.GetBuildingLevel(selectedBuilding);

        hud.SetRepairDetail(title, body, actionLabel);
        hud.SetRepairPreview(
            selectedBuilding,
            currentLevel,
            GetSelectedBuildingIndex(),
            buildingOrder.Length
        );
    }

    private void RefreshTraitPanel()
    {
        if (UseShipTraitTreePanel())
        {
            if (currentPanel == SettlementPanelKind.Trait)
            {
                shipTraitTreePanel.RefreshPanel();
            }

            return;
        }

        if (hud == null || settlementController == null)
        {
            return;
        }

        string title = selectedTrait != null ? selectedTrait.DisplayName : "추가 특성";
        string body = settlementController.BuildTraitDetailText(selectedTrait);
        string actionLabel = settlementController.GetTraitActionLabel(selectedTrait);

        hud.SetTraitDetail(title, body, actionLabel);
    }

    private void RefreshButtons()
    {
        ShipDefinition previewShip = settlementController != null ? settlementController.PreviewShip : null;

        if (shipActionButton != null)
        {
            shipActionButton.interactable =
                settlementController != null &&
                settlementController.CanExecuteShipAction(previewShip);
        }

        if (repairActionButton != null)
        {
            repairActionButton.interactable =
                settlementController != null &&
                settlementController.CanExecuteBuildingAction(selectedBuilding);
        }

        if (traitActionButton != null)
        {
            traitActionButton.interactable = CanExecuteCurrentTraitAction();
        }

        if (launchButton != null)
        {
            launchButton.interactable = true;
        }
    }

    private bool TryExecuteCurrentTraitAction()
    {
        if (UseShipTraitTreePanel() && currentPanel == SettlementPanelKind.Trait)
        {
            return shipTraitTreePanel.TryUnlockSelectedTrait();
        }

        if (settlementController == null)
        {
            return false;
        }

        return settlementController.TryUnlockOrUpgradeTrait(selectedTrait);
    }

    private bool CanExecuteCurrentTraitAction()
    {
        if (UseShipTraitTreePanel() && currentPanel == SettlementPanelKind.Trait)
        {
            return shipTraitTreePanel.CanUnlockSelectedTrait();
        }

        if (settlementController == null)
        {
            return false;
        }

        return settlementController.CanExecuteTraitAction(selectedTrait);
    }

    private bool UseShipTraitTreePanel()
    {
        return useShipTraitTreePanel && shipTraitTreePanel != null;
    }

    private bool ShouldControllerHandleTraitActionButton()
    {
        if (traitActionButton == null)
        {
            return false;
        }

        if (!UseShipTraitTreePanel())
        {
            return true;
        }

        return shipTraitTreePanel.UnlockButton != traitActionButton;
    }

    private void SubscribeController()
    {
        if (settlementController != null)
        {
            settlementController.Changed += Refresh;
        }
    }

    private void UnsubscribeController()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }
    }

    private void SubscribeButtons()
    {
        if (openRepairPanelButton != null)
        {
            openRepairPanelButton.onClick.AddListener(ShowRepairPanel);
        }

        if (openTraitPanelButton != null)
        {
            openTraitPanelButton.onClick.AddListener(ShowTraitPanel);
        }

        if (openSettingsPanelButton != null)
        {
            openSettingsPanelButton.onClick.AddListener(ShowSettingsPanel);
        }

        if (shipPreviousButton != null)
        {
            shipPreviousButton.onClick.AddListener(MovePreviewShipPrevious);
        }

        if (shipNextButton != null)
        {
            shipNextButton.onClick.AddListener(MovePreviewShipNext);
        }

        if (shipActionButton != null)
        {
            shipActionButton.onClick.AddListener(ExecuteShipAction);
        }

        if (launchButton != null)
        {
            launchButton.onClick.AddListener(LaunchExpedition);
        }

        if (repairActionButton != null)
        {
            repairActionButton.onClick.AddListener(ExecuteBuildingAction);
        }

        if (repairBackButton != null)
        {
            repairBackButton.onClick.AddListener(ShowMainPanel);
        }

        if (ShouldControllerHandleTraitActionButton())
        {
            traitActionButton.onClick.AddListener(ExecuteTraitAction);
        }

        if (traitBackButton != null)
        {
            traitBackButton.onClick.AddListener(ShowMainPanel);
        }

        if (repairPreviousButton != null)
        {
            repairPreviousButton.onClick.AddListener(MoveBuildingPrevious);
        }

        if (repairNextButton != null)
        {
            repairNextButton.onClick.AddListener(MoveBuildingNext);
        }
    }

    private void UnsubscribeButtons()
    {
        if (openRepairPanelButton != null)
        {
            openRepairPanelButton.onClick.RemoveListener(ShowRepairPanel);
        }

        if (openTraitPanelButton != null)
        {
            openTraitPanelButton.onClick.RemoveListener(ShowTraitPanel);
        }

        if (openSettingsPanelButton != null)
        {
            openSettingsPanelButton.onClick.RemoveListener(ShowSettingsPanel);
        }

        if (shipPreviousButton != null)
        {
            shipPreviousButton.onClick.RemoveListener(MovePreviewShipPrevious);
        }

        if (shipNextButton != null)
        {
            shipNextButton.onClick.RemoveListener(MovePreviewShipNext);
        }

        if (shipActionButton != null)
        {
            shipActionButton.onClick.RemoveListener(ExecuteShipAction);
        }

        if (launchButton != null)
        {
            launchButton.onClick.RemoveListener(LaunchExpedition);
        }

        if (repairActionButton != null)
        {
            repairActionButton.onClick.RemoveListener(ExecuteBuildingAction);
        }

        if (repairBackButton != null)
        {
            repairBackButton.onClick.RemoveListener(ShowMainPanel);
        }

        if (ShouldControllerHandleTraitActionButton())
        {
            traitActionButton.onClick.RemoveListener(ExecuteTraitAction);
        }

        if (traitBackButton != null)
        {
            traitBackButton.onClick.RemoveListener(ShowMainPanel);
        }

        if (repairPreviousButton != null)
        {
            repairPreviousButton.onClick.RemoveListener(MoveBuildingPrevious);
        }

        if (repairNextButton != null)
        {
            repairNextButton.onClick.RemoveListener(MoveBuildingNext);
        }
    }

    private TraitDefinition GetTraitByIndex(int index)
    {
        if (settlementController == null ||
            settlementController.TraitDefinitions == null ||
            settlementController.TraitDefinitions.Count == 0)
        {
            return null;
        }

        index = Mathf.Clamp(index, 0, settlementController.TraitDefinitions.Count - 1);
        return settlementController.TraitDefinitions[index];
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    public void MoveBuildingPrevious()
    {
        int index = GetSelectedBuildingIndex();
        index--;

        if (index < 0)
        {
            index = buildingOrder.Length - 1;
        }

        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingOrder[index];
        Refresh();
    }

    public void MoveBuildingNext()
    {
        int index = GetSelectedBuildingIndex();
        index = (index + 1) % buildingOrder.Length;

        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingOrder[index];
        Refresh();
    }

    private int GetSelectedBuildingIndex()
    {
        for (int i = 0; i < buildingOrder.Length; i++)
        {
            if (buildingOrder[i] == selectedBuilding)
            {
                return i;
            }
        }

        return 0;
    }

    private int GetShipCount()
    {
        if (settlementController == null || settlementController.ShipDefinitions == null)
        {
            return 0;
        }

        return settlementController.ShipDefinitions.Count;
    }
}