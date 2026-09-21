using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using PixelCrushers.DialogueSystem;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

public enum SettlementPanelKind
{
    Main,
    Repair,
    Trait,
    SectorTechnology,
    DialogueArchive
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
    [Serializable]
    private sealed class NavigationButtonView
    {
        public Button Button;
        public Image Background;
        public Image Icon;
        public Image ActiveStrip;
        public Outline ActiveOutline;
        public TextMeshProUGUI Label;
        public Color AccentColor;
        public Color SelectedBackgroundColor = SettlementSelectionColors.SelectedBackground;
        public Color SelectedLabelColor = SettlementSelectionColors.Selected;
        public float SelectedIconAlphaMultiplier = 1f / 0.56f;
        [NonSerialized] public bool BaselineCaptured;
        [NonSerialized] public Color BackgroundBaseline, LabelBaseline, IconBaseline;
    }

    [Header("References")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementHUD hud;
    [SerializeField] private EscSettingsMenuController settingsMenuController;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject repairPanel;
    [SerializeField] private GameObject traitPanel;
    [SerializeField] private SettlementDialogueArchivePanelUI dialogueArchivePanel;
    [SerializeField] private Button openDialogueArchiveButton;

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

    [Header("Recovery / Route Core Hub")]
    [SerializeField] private LocalizationCatalog recoveryLocalizationCatalog;
    [SerializeField] private Button routeCoreDeckButton;
    [SerializeField] private TextMeshProUGUI routeCoreDeckButtonLabel;
    [SerializeField] private Button facilityManagementButton;
    [SerializeField] private TextMeshProUGUI facilityManagementButtonLabel;
    private bool showOptionalFacilities;
    private bool routeCoreWasReady;

    public bool IsRouteCoreHubAvailable => PermanentProgress.Instance != null &&
        (PermanentProgress.Instance.CurrentRouteCoreState == RouteCoreState.Assembled ||
         PermanentProgress.Instance.CurrentRouteCoreState == RouteCoreState.Activated);
    public bool IsRouteCorePresentation => IsRouteCoreHubAvailable && !showOptionalFacilities;

    [Header("Trait Panel Buttons")]
    [SerializeField] private Button traitActionButton;
    [SerializeField] private Button traitBackButton;

    [Header("Ship Trait Tree Panel")]
    [SerializeField] private bool useShipTraitTreePanel = true;
    [SerializeField] private ShipTraitTreePanel shipTraitTreePanel;

    [Header("Sector Technology Panel")]
    [SerializeField] private SettlementSectorTechnologyPanelUI sectorTechnologyPanelUI;

    [Header("Authored Navigation")]
    [SerializeField] private RectTransform navigationRoot;
    [SerializeField] private Image navigationBackground;
    [SerializeField] private TextMeshProUGUI navigationHeader;
    [SerializeField] private Button hangarNavigationButton;
    [SerializeField] private Button sectorTechnologyNavigationButton;
    [SerializeField] private NavigationButtonView hangarNavigationView = new NavigationButtonView();
    [SerializeField] private NavigationButtonView repairNavigationView = new NavigationButtonView();
    [SerializeField] private NavigationButtonView sectorTechnologyNavigationView = new NavigationButtonView();
    [SerializeField] private NavigationButtonView traitNavigationView = new NavigationButtonView();
    [SerializeField] private NavigationButtonView settingsNavigationView = new NavigationButtonView();
    [SerializeField] private NavigationButtonView archiveNavigationView = new NavigationButtonView();
    [Tooltip("Launch, ship action, repair action, repair back, trait action, trait back; text content remains gameplay-owned.")]
    [SerializeField] private TextMeshProUGUI[] navigationControlLabels = new TextMeshProUGUI[6];
    private Button[] primaryNavigationButtons = new Button[0];
    private int primaryNavigationIndex;
    private int focusedPrimaryNavigationIndex = -1;
    private int hoveredPrimaryNavigationIndex = -1;
    private GameObject focusBeforeSettings;
    [SerializeField] private CanvasGroup settlementInputGroup;
    private bool navigationDiagnosticReported;
    private readonly List<KeyValuePair<Button, UnityAction>> ownedButtonListeners = new List<KeyValuePair<Button, UnityAction>>();
    private SettlementController subscribedSettlementController;
    private EscSettingsMenuController subscribedSettingsController;

    // Also consulted by the two existing panel presenters before their Awake typography defaults.
    public bool PreserveNavigationTypography => true;
    private DialogueSystemController dialogueController;
    private bool dialogueModalActive;
    private bool settlementInputRequested = true;

    [Header("Start")]
    [SerializeField] private SettlementPanelKind startPanel = SettlementPanelKind.Main;
    [SerializeField] private BuildingType defaultBuilding = BuildingType.Hangar;
    [SerializeField] private int defaultTraitIndex;

    private SettlementPanelKind currentPanel = SettlementPanelKind.Main;
    private SettlementSelectionKind selectedKind = SettlementSelectionKind.None;
    private BuildingType selectedBuilding;
    private bool accessKeyRecoveryPresentationActive;
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

        if (settingsMenuController == null)
        {
            settingsMenuController = FindFirstObjectByType<EscSettingsMenuController>();
        }

        if (shipTraitTreePanel == null && traitPanel != null)
        {
            shipTraitTreePanel = traitPanel.GetComponentInChildren<ShipTraitTreePanel>(true);
        }

        if (shipTraitTreePanel == null)
        {
            shipTraitTreePanel = FindFirstObjectByType<ShipTraitTreePanel>();
        }

        if (sectorTechnologyPanelUI == null)
        {
            sectorTechnologyPanelUI = GetComponent<SettlementSectorTechnologyPanelUI>();
        }

        selectedBuilding = defaultBuilding;
        RefreshAccessKeyRecoverySelection();
        selectedTrait = GetTraitByIndex(defaultTraitIndex);

        if (settlementController != null)
        {
            selectedWeapon = settlementController.SelectedWeaponTree;
        }

        sectorTechnologyPanelUI?.Initialize(
            settlementController,
            this,
            repairPanel,
            repairActionButton,
            repairBackButton
        );

        InitializeNavigationPresentation();
    }

    private void OnEnable()
    {
        InitializeNavigationPresentation();
        ConfigureSettlementButtonSounds();
        SubscribeController();
        SubscribeButtons();
        SubscribeDialogueLifecycle();
        RefreshDialogueModalState();
    }

    private void Start()
    {
        ShowPanel(startPanel);
        SelectPrimaryNavigationForCurrentPanel();
    }


    private void OnDisable()
    {
        UnsubscribeDialogueLifecycle();
        dialogueModalActive = false;
        settlementInputRequested = true;
        ApplySettlementInputState();
        UnsubscribeController();
        UnsubscribeButtons();
        focusedPrimaryNavigationIndex = -1;
        focusBeforeSettings = null;
    }

    public void ShowMainPanel()
    {
        ShowPanel(SettlementPanelKind.Main);
    }

    public void ShowRepairPanel()
    {
        showOptionalFacilities = false;
        ShowPanel(SettlementPanelKind.Repair);
    }

    public void ToggleFacilityManagement()
    {
        if (!IsRouteCoreHubAvailable || !CanUseSettlementNavigation()) return;
        showOptionalFacilities = !showOptionalFacilities;
        accessKeyRecoveryPresentationActive = false;
        ShowPanel(SettlementPanelKind.Repair);
    }

    public void ShowTraitPanel()
    {
        ShowPanel(SettlementPanelKind.Trait);
    }

    public void ShowSectorTechnologyPanel()
    {
        if (sectorTechnologyPanelUI == null || !sectorTechnologyPanelUI.CanShow)
        {
            return;
        }

        currentPanel = SettlementPanelKind.SectorTechnology;
        if (dialogueArchivePanel != null) dialogueArchivePanel.gameObject.SetActive(false);
        SetPanelActive(mainPanel, false);
        SetPanelActive(repairPanel, false);
        SetPanelActive(traitPanel, false);
        CloseSettingsOverlay();
        sectorTechnologyPanelUI.Show();
        HideRedundantBackControls();
        SelectPrimaryNavigationForCurrentPanel();
        RefreshNavigationState();
    }

    public void ShowSettingsPanel()
    {
        OpenSettingsOverlay();
    }

    public void ShowDialogueArchivePanel()
    {
        if (dialogueArchivePanel == null || !CanUseSettlementNavigation()) return;
        ShowPanel(SettlementPanelKind.DialogueArchive);
    }

    public void CloseSettingsAndReturnMain()
    {
        CloseSettingsOverlay();
    }
    private void OpenSettingsOverlay()
    {
        if (settingsMenuController == null || !CanUseSettlementNavigation())
        {
            return;
        }

        settingsMenuController.Open();

        if (!settingsMenuController.IsOpen)
        {
            SetSettlementInputEnabled(true);
        }

        RefreshNavigationState();
    }

    private void CloseSettingsOverlay()
    {
        if (settingsMenuController != null && settingsMenuController.IsOpen)
        {
            settingsMenuController.Close();
        }

        SetSettlementInputEnabled(true);

        if (MouseCursorManager.Instance != null)
        {
            MouseCursorManager.Instance.ResetToSceneDefault();
        }

        RefreshNavigationState();
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
        showOptionalFacilities = IsRouteCoreHubAvailable;
        routeCoreWasReady = IsRouteCoreHubAvailable;
        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingType;
        RefreshAccessKeyRecoverySelection();
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
            ShipDefinition previewShip = settlementController.PreviewShip;
            bool wasUnlocked = previewShip != null && settlementController.IsShipUnlocked(previewShip);
            bool wasSelected = settlementController.IsPreviewShipSelected();

            bool success = settlementController.TryExecutePreviewShipAction();
            PlayShipActionResultSound(success, wasUnlocked, wasSelected);
        }

        Refresh();
    }

    public void ExecuteBuildingAction()
    {
        if (settlementController != null)
        {
            int levelBefore = settlementController.GetBuildingLevel(selectedBuilding);
            bool success = TryExecuteSelectedRestorationAction();
            PlayProgressActionResultSound(success, levelBefore);
        }

        Refresh();
    }

    public void ExecuteTraitAction()
    {
        int levelBefore = selectedTrait != null && settlementController != null
            ? settlementController.GetTraitLevel(selectedTrait)
            : GetSelectedTreeTraitLevel();

        bool success = TryExecuteCurrentTraitAction();
        PlayProgressActionResultSound(success, levelBefore);
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
                TryExecuteSelectedRestorationAction();
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
                    TryExecuteSelectedRestorationAction();
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

        bool launched = settlementController.LaunchExpedition();
        AudioManager.Play(launched
            ? SoundEventIds.UiLaunch
            : SoundEventIds.UiDisabled);
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        bool coreReady = IsRouteCoreHubAvailable;
        if (coreReady != routeCoreWasReady)
        {
            // A successful story restoration immediately hands this same panel to the core.
            // This is transient presentation state, never campaign/save state.
            showOptionalFacilities = false;
            if (coreReady) accessKeyRecoveryPresentationActive = false;
            routeCoreWasReady = coreReady;
        }

        if (hud != null)
        {
            hud.Refresh();
        }

        RefreshMainPanel();
        RefreshRepairPanel();
        RefreshTraitPanel();
        RefreshButtons();
        RefreshNavigationState();
    }

    private void ShowPanel(SettlementPanelKind panelKind)
    {
        if (panelKind == SettlementPanelKind.SectorTechnology)
        {
            ShowSectorTechnologyPanel();
            return;
        }

        currentPanel = panelKind;

        sectorTechnologyPanelUI?.Hide(false);

        SetPanelActive(mainPanel, panelKind == SettlementPanelKind.Main);
        SetPanelActive(repairPanel, panelKind == SettlementPanelKind.Repair);
        SetPanelActive(traitPanel, panelKind == SettlementPanelKind.Trait);
        if (dialogueArchivePanel != null)
            dialogueArchivePanel.gameObject.SetActive(panelKind == SettlementPanelKind.DialogueArchive);
        HideRedundantBackControls();
        
        CloseSettingsOverlay();
        SelectPrimaryNavigationForCurrentPanel();

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
        if (previewShip != null) hud.SetShipResearchAccent(previewShip.ResearchAccent);
        hud.SetShipPreviewState(
            settlementController.GetPreviewShipSprite(),
            settlementController.PreviewShipIndex,
            GetShipCount()
        );
        hud.SetLaunchLabel("탐사 시작");
    }

    private bool missingRestorationBindingReported;

    private bool RestorationPresentationReady(bool report)
    {
        var errors = new List<string>();
        if (hud == null) errors.Add("SettlementUIController.hud is missing.");
        else hud.CollectRestorationBindingErrors(repairPanel, repairActionButton, repairPreviousButton, repairNextButton, errors);
        if (errors.Count == 0) return true;
        if (report && !missingRestorationBindingReported)
        {
            missingRestorationBindingReported = true;
            Debug.LogWarning("Restoration presentation unavailable. Restore the listed authored Inspector bindings. No replacement UI or restoration action will run.\n" + string.Join("\n", errors), this);
        }
        return false;
    }

    private void RepairRestorationFocus()
    {
        if (currentPanel != SettlementPanelKind.Repair || !CanUseSettlementNavigation() || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || repairPanel == null || !selected.transform.IsChildOf(repairPanel.transform)) return;
        Selectable control = selected.GetComponent<Selectable>();
        if (IsAvailableContentFocus(control)) return;
        if (IsRouteCorePresentation && IsAvailableContentFocus(routeCoreDeckButton)) EventSystem.current.SetSelectedGameObject(routeCoreDeckButton.gameObject);
        else if (IsAvailableContentFocus(repairNextButton)) EventSystem.current.SetSelectedGameObject(repairNextButton.gameObject);
        else if (IsAvailableContentFocus(openRepairPanelButton)) EventSystem.current.SetSelectedGameObject(openRepairPanelButton.gameObject);
        else EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshRepairPanel()
    {
        if (hud == null || settlementController == null)
        {
            return;
        }

        bool coreMode = IsRouteCorePresentation;
        SetPanelActive(routeCoreDeckButton != null ? routeCoreDeckButton.gameObject : null, coreMode);
        SetPanelActive(facilityManagementButton != null ? facilityManagementButton.gameObject : null, IsRouteCoreHubAvailable);
        SetPanelActive(repairActionButton != null ? repairActionButton.gameObject : null, !coreMode);
        if (routeCoreDeckButtonLabel != null) routeCoreDeckButtonLabel.text = RecoveryText("ui.settlement.route_core.action.enter_deck");
        if (facilityManagementButtonLabel != null) facilityManagementButtonLabel.text = RecoveryText(coreMode
            ? "ui.settlement.route_core.action.manage_facilities" : "ui.settlement.nav.route_core");
        if (facilityManagementButton != null)
        {
            Navigation navigation = facilityManagementButton.navigation;
            navigation.selectOnRight = coreMode ? routeCoreDeckButton : repairActionButton;
            navigation.selectOnUp = coreMode ? routeCoreDeckButton : repairNextButton;
            facilityManagementButton.navigation = navigation;
        }

        if (!RestorationPresentationReady(currentPanel == SettlementPanelKind.Repair)) return;
        if (coreMode)
        {
            string state = GetRouteCorePresentationState(PermanentProgress.Instance);
            string restored = RecoveryText("ui.settlement.route_core.components.restored");
            hud.SetRouteCoreDetail(
                RecoveryText("ui.settlement.nav.route_core"),
                RecoveryText("ui.settlement.route_core.status." + state),
                RecoveryText("ui.settlement.route_core.description." + state),
                RecoveryText("ui.settlement.route_core.components.header"),
                RecoveryText("ui.story_recovery.sector_stabilizer") + " · " + restored + "\n\n" +
                RecoveryText("ui.story_recovery.matter_compressor") + " · " + restored + "\n\n" +
                RecoveryText("ui.story_recovery.phase_navigation_lens") + " · " + restored);
            return;
        }

        if (!accessKeyRecoveryPresentationActive)
        {
            RefreshAccessKeyRecoverySelection();
        }

        SettlementRestorationViewData restorationViewData =
            accessKeyRecoveryPresentationActive
                ? settlementController.BuildDamagedAccessKeyRestorationViewData()
                : settlementController.BuildRestorationViewData(selectedBuilding);
        int currentLevel = settlementController.GetBuildingLevel(selectedBuilding);

        hud.SetRestorationDetail(restorationViewData);
        hud.SetRepairPreview(
            selectedBuilding,
            currentLevel,
            GetSelectedBuildingIndex(),
            buildingOrder.Length
        );
    }

    public static string GetRouteCorePresentationState(PermanentProgress progress)
    {
        if (progress != null && progress.FinalBossDefeated) return "campaign_complete";
        if (progress != null && progress.CurrentRouteCoreState == RouteCoreState.Activated)
            return progress.SettlementDefenseCleared ? "defense_complete" : "activated";
        return "assembled";
    }

    private string RecoveryText(string key)
    {
        if (recoveryLocalizationCatalog != null && recoveryLocalizationCatalog.TryGetText(
            key, GameSettingsRuntime.LanguageCode, out string text, out _)) return text;
        if (VoidScrapperLocalizationService.HasInstance)
            return VoidScrapperLocalizationService.Instance.GetText(key);
        return string.Empty;
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

        string title = selectedTrait != null ? selectedTrait.DisplayName : "장비 개발";
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
                !IsRouteCorePresentation &&
                settlementController != null &&
                RestorationPresentationReady(false) &&
                (accessKeyRecoveryPresentationActive
                    ? settlementController.CanRestoreDamagedAccessKey()
                    : settlementController.CanExecuteBuildingAction(selectedBuilding));
        }

        RepairRestorationFocus();

        if (traitActionButton != null)
        {
            if (UseShipTraitTreePanel() && shipTraitTreePanel.IsEquipmentDevelopment)
                traitActionButton.gameObject.SetActive(false);
            traitActionButton.interactable = CanExecuteCurrentTraitAction();
        }

        if (launchButton != null)
        {
            launchButton.interactable = !dialogueModalActive;
        }
    }

    private void InitializeNavigationPresentation()
    {
        HideRedundantBackControls();
        primaryNavigationButtons = new[] { hangarNavigationButton, openRepairPanelButton,
            sectorTechnologyNavigationButton, openTraitPanelButton, openDialogueArchiveButton, openSettingsPanelButton };
        bool valid = navigationRoot != null && mainPanel != null && navigationRoot == mainPanel.transform.parent &&
            navigationRoot.gameObject.scene == gameObject.scene && settlementInputGroup != null &&
            settlementInputGroup.transform == navigationRoot && ValidNavigationChild(navigationBackground, navigationRoot) &&
            ValidNavigationChild(navigationHeader, navigationRoot) && navigationHeader.font != null;
        NavigationButtonView[] views = { hangarNavigationView, repairNavigationView, sectorTechnologyNavigationView,
            traitNavigationView, archiveNavigationView, settingsNavigationView };
        for (int i = 0; i < views.Length; i++)
        {
            NavigationButtonView view = views[i];
            Button button = primaryNavigationButtons[i];
            valid &= view != null && button != null && view.Button == button &&
                ValidNavigationChild(button, navigationRoot) && button.transform.parent == navigationRoot &&
                ValidNavigationChild(view.Background, button.transform) && ValidNavigationChild(view.Icon, button.transform) &&
                ValidNavigationChild(view.ActiveStrip, button.transform) && ValidNavigationChild(view.ActiveOutline, button.transform) &&
                ValidNavigationChild(view.Label, button.transform) && view.Label.font != null &&
                button.GetComponent<SettlementPrimaryNavigationPointer>() != null;
            for (int j = 0; j < i; j++) valid &= button != primaryNavigationButtons[j];
        }
        Button[] controls = { launchButton, shipActionButton, repairActionButton, repairBackButton, traitActionButton, traitBackButton };
        valid &= navigationControlLabels != null && navigationControlLabels.Length == controls.Length;
        for (int i = 0; navigationControlLabels != null && i < Math.Min(navigationControlLabels.Length, controls.Length); i++)
        {
            // Retained compatibility Back references do not gate authored navigation.
            if (i == 3 || i == 5) continue;
            valid &= controls[i] != null && ValidNavigationChild(navigationControlLabels[i], controls[i].transform) &&
                navigationControlLabels[i].font != null;
        }
        if (valid)
        {
            for (int i = 0; i < views.Length; i++)
            {
                CaptureNavigationBaseline(views[i]);
                Navigation navigation = primaryNavigationButtons[i].navigation;
                navigation.mode = Navigation.Mode.None;
                primaryNavigationButtons[i].navigation = navigation;
                primaryNavigationButtons[i].GetComponent<SettlementPrimaryNavigationPointer>().Configure(this, i);
            }
            ApplySettlementInputState();
            return;
        }
        if (!navigationDiagnosticReported)
        {
            navigationDiagnosticReported = true;
            Debug.LogWarning("Settlement navigation bindings are incomplete or invalid. " + DescribeNavigationBindings(views) +
                " Exit Play Mode and restore the listed authored Inspector bindings. No replacement navigation was created; existing panel actions remain available.", this);
        }
    }

    private bool ValidNavigationChild(Component component, Transform parent)
    {
        return component != null && parent != null && component.gameObject.scene == gameObject.scene &&
            (component.transform == parent || component.transform.IsChildOf(parent));
    }

    private string DescribeNavigationBindings(NavigationButtonView[] views)
    {
        var errors = new List<string>();
        void Issue(string field, Component value, Transform parent, string reason)
        {
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(nameof(SettlementUIController) + "." + field +
                " on " + SettlementSectorTechnologyPanelUI.BindingLocation(transform), value, parent, gameObject.scene, reason));
        }
        void Check(string field, Component value, Transform parent)
        {
            if (!ValidNavigationChild(value, parent) || value is TMP_Text text && text.font == null)
                Issue(field, value, parent, "Missing binding, invalid ownership/ancestry, or missing TMP font");
        }
        if (mainPanel == null) Issue(nameof(mainPanel), null, navigationRoot, "Missing content panel");
        if (navigationRoot == null || mainPanel == null || navigationRoot != mainPanel.transform.parent || navigationRoot.gameObject.scene != gameObject.scene)
            Issue(nameof(navigationRoot), navigationRoot, mainPanel != null ? mainPanel.transform.parent : null, "Missing or invalid navigation root");
        Check(nameof(navigationBackground), navigationBackground, navigationRoot);
        Check(nameof(navigationHeader), navigationHeader, navigationRoot);
        if (settlementInputGroup == null || settlementInputGroup.transform != navigationRoot || settlementInputGroup.gameObject.scene != gameObject.scene)
            Issue(nameof(settlementInputGroup), settlementInputGroup, navigationRoot, "Missing or invalid input group");
        string[] fields = { nameof(hangarNavigationView), nameof(repairNavigationView), nameof(sectorTechnologyNavigationView), nameof(traitNavigationView), nameof(archiveNavigationView), nameof(settingsNavigationView) };
        for (int i = 0; i < views.Length; i++)
        {
            NavigationButtonView view = views[i];
            Button button = primaryNavigationButtons[i];
            if (view == null || button == null || view.Button != button || button.transform.parent != navigationRoot)
                Issue(fields[i] + ".Button", view != null ? view.Button : null, navigationRoot, "Missing or incorrect destination mapping");
            if (button == null || view == null) continue;
            Check(fields[i] + ".Background", view.Background, button.transform);
            Check(fields[i] + ".Icon", view.Icon, button.transform);
            Check(fields[i] + ".ActiveStrip", view.ActiveStrip, button.transform);
            Check(fields[i] + ".ActiveOutline", view.ActiveOutline, button.transform);
            Check(fields[i] + ".Label", view.Label, button.transform);
            if (button.GetComponent<SettlementPrimaryNavigationPointer>() == null)
                Issue(fields[i] + ".Button", button, navigationRoot, "Missing SettlementPrimaryNavigationPointer component");
            for (int j = 0; j < i; j++)
                if (button == primaryNavigationButtons[j]) Issue(fields[i] + ".Button", button, navigationRoot, "Duplicate destination mapping");
        }
        Button[] controls = { launchButton, shipActionButton, repairActionButton, repairBackButton, traitActionButton, traitBackButton };
        if (navigationControlLabels == null || navigationControlLabels.Length != controls.Length)
            Issue(nameof(navigationControlLabels), null, navigationRoot, "Expected six serialized label slots; legacy Back slots may be empty");
        for (int i = 0; navigationControlLabels != null && i < Math.Min(navigationControlLabels.Length, controls.Length); i++)
        {
            if (i == 3 || i == 5) continue;
            Check(nameof(navigationControlLabels) + "[" + i + "]", navigationControlLabels[i], controls[i] != null ? controls[i].transform : null);
        }
        return string.Join("; ", errors);
    }

    private static void CaptureNavigationBaseline(NavigationButtonView view)
    {
        if (view.BaselineCaptured) return;
        view.BackgroundBaseline = view.Background.color;
        view.LabelBaseline = view.Label.color;
        view.IconBaseline = view.Icon.color;
        view.BaselineCaptured = true;
    }


    private void RefreshNavigationState()
    {
        if (archiveNavigationView.Label != null)
            archiveNavigationView.Label.text = RecoveryText("ui.settlement.archive.nav");
        if (repairNavigationView.Label != null)
            repairNavigationView.Label.text = RecoveryText(IsRouteCoreHubAvailable
                ? "ui.settlement.nav.route_core" : "ui.settlement.nav.recovery");
        if (traitNavigationView.Label != null) traitNavigationView.Label.text = RecoveryText("ui.settlement.equipment.title");
        bool settingsOpen = settingsMenuController != null && settingsMenuController.IsOpen;
        int activeIndex = ActiveContentNavigationIndex;

        SetNavigationViewActive(
            hangarNavigationView,
            activeIndex == 0
        );
        SetNavigationViewActive(
            repairNavigationView,
            activeIndex == 1
        );
        SetNavigationViewActive(
            sectorTechnologyNavigationView,
            activeIndex == 2
        );
        SetNavigationViewActive(
            traitNavigationView,
            activeIndex == 3
        );
        SetNavigationViewActive(
            settingsNavigationView,
            settingsOpen
        );
        SetNavigationViewActive(archiveNavigationView, activeIndex == 4);
    }

    private void SetNavigationViewActive(NavigationButtonView view, bool active)
    {
        if (view == null || !view.BaselineCaptured)
        {
            return;
        }

        bool preview = CanUseSettlementNavigation() &&
            ((hoveredPrimaryNavigationIndex >= 0 && primaryNavigationButtons[hoveredPrimaryNavigationIndex] == view.Button) ||
             (focusedPrimaryNavigationIndex >= 0 && primaryNavigationButtons[focusedPrimaryNavigationIndex] == view.Button));
        Color backgroundColor = active ? view.SelectedBackgroundColor :
            preview ? SettlementSelectionColors.HoverBackground : view.BackgroundBaseline;

        if (view.Background != null)
        {
            view.Background.color = backgroundColor;
        }

        if (view.ActiveStrip != null)
        {
            view.ActiveStrip.enabled = active;
            view.ActiveStrip.color = SettlementSelectionColors.Selected;
        }

        if (view.ActiveOutline != null)
        {
            view.ActiveOutline.enabled = preview;
            view.ActiveOutline.effectColor = SettlementSelectionColors.Hover;
        }

        if (view.Label != null)
        {
            view.Label.color = active
                ? view.SelectedLabelColor
                : view.LabelBaseline;
        }

        if (view.Icon != null)
        {
            Color iconColor = view.IconBaseline;
            iconColor.a = Mathf.Clamp01(view.IconBaseline.a * (active ? view.SelectedIconAlphaMultiplier : 1f));
            view.Icon.color = iconColor;
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

    private void ConfigureSettlementButtonSounds()
    {
        ConfigureCommonButtonSound(hangarNavigationButton);
        ConfigureCommonButtonSound(sectorTechnologyNavigationButton);
        ConfigureCommonButtonSound(openRepairPanelButton);
        ConfigureCommonButtonSound(openTraitPanelButton);
        ConfigureCommonButtonSound(shipPreviousButton);
        ConfigureCommonButtonSound(shipNextButton);
        ConfigureCommonButtonSound(repairPreviousButton);
        ConfigureCommonButtonSound(repairNextButton);

        // EscSettingsMenuController owns the settings-open sound. Keep hover and
        // disabled feedback here without layering a second click event.
        ConfigureButtonSound(openSettingsPanelButton, string.Empty, false, true, true);
        ConfigureSpecialButtonSound(repairBackButton, SoundEventIds.UiBack);
        ConfigureSpecialButtonSound(traitBackButton, SoundEventIds.UiBack);

        ConfigureSilentButton(launchButton);

        ConfigureActionButtonSound(shipActionButton);
        ConfigureActionButtonSound(repairActionButton);
        ConfigureActionButtonSound(traitActionButton);
    }

    private void ConfigureCommonButtonSound(Button targetButton)
    {
        ConfigureButtonSound(targetButton, SoundEventIds.UiClick, true, true, true);
    }

    private void ConfigureSpecialButtonSound(Button targetButton, string clickEventId)
    {
        ConfigureButtonSound(targetButton, clickEventId, true, true, true);
    }

    private void ConfigureSilentButton(Button targetButton)
    {
        ConfigureButtonSound(targetButton, string.Empty, false, false, false);
    }

    private void ConfigureButtonSound(Button targetButton, string clickEventId, bool playClick, bool playHover, bool playDisabledClick)
    {
        UISoundButton[] soundButtons = GetOrCreateSoundButtons(targetButton);

        for (int i = 0; i < soundButtons.Length; i++)
        {
            UISoundButton soundButton = soundButtons[i];

            if (soundButton == null)
            {
                continue;
            }

            soundButton.SetClickSoundEnabled(playClick);
            soundButton.SetHoverSoundEnabled(playHover);
            soundButton.SetDisabledClickSoundEnabled(playDisabledClick);

            if (!string.IsNullOrWhiteSpace(clickEventId))
            {
                soundButton.SetClickSoundEventId(clickEventId);
            }

            soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
            soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
        }
    }

    private void ConfigureActionButtonSound(Button targetButton)
    {
        UISoundButton[] soundButtons = GetOrCreateSoundButtons(targetButton);

        for (int i = 0; i < soundButtons.Length; i++)
        {
            UISoundButton soundButton = soundButtons[i];

            if (soundButton == null)
            {
                continue;
            }

            // 결과음은 PlayShipActionResultSound / PlayProgressActionResultSound 한 곳에서만 재생한다.
            soundButton.SetClickSoundEnabled(false);
            soundButton.SetHoverSoundEnabled(false);
            soundButton.SetDisabledClickSoundEnabled(true);
            soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
        }
    }

    private UISoundButton[] GetOrCreateSoundButtons(Button targetButton)
    {
        if (targetButton == null)
        {
            return System.Array.Empty<UISoundButton>();
        }

        UISoundButton[] soundButtons = targetButton.GetComponents<UISoundButton>();

        if (soundButtons == null || soundButtons.Length == 0)
        {
            UISoundButton created = targetButton.gameObject.AddComponent<UISoundButton>();
            return new[] { created };
        }

        return soundButtons;
    }

    private void PlayShipActionResultSound(bool success, bool wasUnlocked, bool wasSelected)
    {
        if (!success)
        {
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        if (!wasUnlocked)
        {
            AudioManager.Play(SoundEventIds.UiUnlock);
            return;
        }

        if (!wasSelected)
        {
            AudioManager.Play(SoundEventIds.UiActivate);
            return;
        }

        AudioManager.Play(SoundEventIds.UiClick);
    }

    private void PlayProgressActionResultSound(bool success, int levelBefore)
    {
        if (!success)
        {
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        AudioManager.Play(levelBefore <= 0
            ? SoundEventIds.UiUnlock
            : SoundEventIds.UiUpgradeSuccess);
    }

    private int GetSelectedTreeTraitLevel()
    {
        if (shipTraitTreePanel == null || PermanentProgress.Instance == null)
        {
            return 0;
        }

        string nodeId = shipTraitTreePanel.SelectedNodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return 0;
        }

        return PermanentProgress.Instance.GetTraitLevel(nodeId);
    }

    private void SubscribeController()
    {
        UnsubscribeController();
        GameSettingsRuntime.Changed += Refresh;
        subscribedSettlementController = settlementController;
        subscribedSettingsController = settingsMenuController;
        if (subscribedSettlementController != null)
        {
            subscribedSettlementController.Changed += Refresh;
        }

        if (subscribedSettingsController != null)
        {
            subscribedSettingsController.Opening += HandleSettingsOpening;
            subscribedSettingsController.OpenStateChanged += HandleSettingsOpenStateChanged;
        }
    }

    private void UnsubscribeController()
    {
        GameSettingsRuntime.Changed -= Refresh;
        if (subscribedSettlementController != null)
        {
            subscribedSettlementController.Changed -= Refresh;
        }

        if (subscribedSettingsController != null)
        {
            subscribedSettingsController.Opening -= HandleSettingsOpening;
            subscribedSettingsController.OpenStateChanged -= HandleSettingsOpenStateChanged;
        }
        subscribedSettlementController = null;
        subscribedSettingsController = null;
    }

    private void HandleSettingsOpenStateChanged(bool isOpen)
    {
        SetSettlementInputEnabled(!isOpen);
        RefreshNavigationState();

        if (!isOpen)
        {
            RestoreContentFocus();
        }
    }

    private void HandleSettingsOpening()
    {
        focusBeforeSettings = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        SetSettlementInputEnabled(false);
        EventSystem.current?.SetSelectedGameObject(null);
        focusedPrimaryNavigationIndex = -1;
    }

    private void RestoreContentFocus()
    {
        Selectable prior = focusBeforeSettings != null ? focusBeforeSettings.GetComponent<Selectable>() : null;
        focusBeforeSettings = null;
        if (CanUseSettlementNavigation() && IsAvailableContentFocus(prior))
        {
            EventSystem.current?.SetSelectedGameObject(prior.gameObject);
        }
        else
        {
            SelectPrimaryNavigationForCurrentPanel();
        }
    }

    private void SetSettlementInputEnabled(bool inputEnabled)
    {
        settlementInputRequested = inputEnabled;
        ApplySettlementInputState();
    }

    private void ApplySettlementInputState()
    {
        if (settlementInputGroup == null)
        {
            return;
        }

        bool inputEnabled = settlementInputRequested && !dialogueModalActive;
        settlementInputGroup.interactable = inputEnabled;
        settlementInputGroup.blocksRaycasts = inputEnabled;
    }

    private void SubscribeDialogueLifecycle()
    {
        UnsubscribeDialogueLifecycle();

        if (!DialogueManager.hasInstance)
        {
            return;
        }

        dialogueController = DialogueManager.instance;
        if (dialogueController == null)
        {
            return;
        }

        dialogueController.conversationStarted += HandleConversationStarted;
        dialogueController.conversationEnded += HandleConversationEnded;
    }

    private void UnsubscribeDialogueLifecycle()
    {
        if (dialogueController == null)
        {
            return;
        }

        dialogueController.conversationStarted -= HandleConversationStarted;
        dialogueController.conversationEnded -= HandleConversationEnded;
        dialogueController = null;
    }

    private void RefreshDialogueModalState()
    {
        SetDialogueModalActive(
            dialogueController != null && dialogueController.isConversationActive);
    }

    private void HandleConversationStarted(Transform actor)
    {
        SetDialogueModalActive(true);
    }

    private void HandleConversationEnded(Transform actor)
    {
        SetDialogueModalActive(false);
    }

    private void SetDialogueModalActive(bool active)
    {
        dialogueModalActive = active;
        ApplySettlementInputState();

        if (active && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Refresh();
        if (!active && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            SelectPrimaryNavigationForCurrentPanel();
        }
    }

#if UNITY_EDITOR
    public void ConfigureDialogueModalForEditorAndTests(CanvasGroup inputGroup)
    {
        settlementInputGroup = inputGroup;
        settlementInputRequested = true;
        ApplySettlementInputState();
    }

    public void SetDialogueModalActiveForEditorAndTests(bool active)
    {
        SetDialogueModalActive(active);
    }
#endif

    internal void SelectPrimaryNavigationIndex(int index, bool playSound)
    {
        if (primaryNavigationButtons.Length == 0 || !CanUseSettlementNavigation())
        {
            return;
        }

        int nextIndex = Mathf.Clamp(index, 0, primaryNavigationButtons.Length - 1);
        Button button = primaryNavigationButtons[nextIndex];
        if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
        {
            return;
        }

        bool selectionChanged = primaryNavigationIndex != nextIndex;
        primaryNavigationIndex = nextIndex;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != button.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        RefreshNavigationState();

        if (playSound && selectionChanged)
        {
            AudioManager.Play(SoundEventIds.UiHover);
        }
    }

    internal void SetPrimaryNavigationHover(int index, bool hovered)
    {
        if (index < 0 || index >= primaryNavigationButtons.Length) return;
        if (hovered)
        {
            if (!CanUseSettlementNavigation()) return;
            hoveredPrimaryNavigationIndex = index;
        }
        else if (hoveredPrimaryNavigationIndex == index) hoveredPrimaryNavigationIndex = -1;
        RefreshNavigationState();
    }

    private int GetPrimaryNavigationIndex(GameObject selectedObject)
    {
        if (selectedObject == null)
        {
            return -1;
        }

        for (int i = 0; i < primaryNavigationButtons.Length; i++)
        {
            if (primaryNavigationButtons[i] != null &&
                primaryNavigationButtons[i].gameObject == selectedObject)
            {
                return i;
            }
        }

        return -1;
    }

    private void SelectPrimaryNavigationForCurrentPanel()
    {
        int index = ActiveContentNavigationIndex;
        if (IsAvailablePrimary(index)) SelectPrimaryNavigationIndex(index, false);
        else
        {
            for (int i = 0; i < primaryNavigationButtons.Length; i++)
            {
                if (!IsAvailablePrimary(i)) continue;
                SelectPrimaryNavigationIndex(i, false);
                break;
            }
        }
    }

    private int ActiveContentNavigationIndex => currentPanel == SettlementPanelKind.Repair ? 1 :
        currentPanel == SettlementPanelKind.SectorTechnology ? 2 : currentPanel == SettlementPanelKind.Trait ? 3 :
        currentPanel == SettlementPanelKind.DialogueArchive ? 4 : 0;

    public Button ActiveContentNavigationButton => primaryNavigationButtons.Length == 6 ?
        primaryNavigationButtons[ActiveContentNavigationIndex] : null;

    internal bool CanUseSettlementNavigation() => isActiveAndEnabled && !dialogueModalActive && settlementInputRequested &&
        !(settingsMenuController != null && settingsMenuController.IsOpen) && !GameplayPauseManager.IsPaused;

    private bool IsAvailablePrimary(int index) => index >= 0 && index < primaryNavigationButtons.Length &&
        primaryNavigationButtons[index] != null && primaryNavigationButtons[index].IsActive() && primaryNavigationButtons[index].IsInteractable();

    internal void SetPrimaryNavigationFocus(int index, bool focused)
    {
        if (index < 0 || index >= primaryNavigationButtons.Length) return;
        if (focused)
        {
            primaryNavigationIndex = index;
            focusedPrimaryNavigationIndex = index;
        }
        else if (focusedPrimaryNavigationIndex == index) focusedPrimaryNavigationIndex = -1;
        RefreshNavigationState();
    }

    internal void MovePrimaryNavigation(int index, AxisEventData eventData)
    {
        if (!CanUseSettlementNavigation() || !IsAvailablePrimary(index) || EventSystem.current == null ||
            EventSystem.current.currentSelectedGameObject != primaryNavigationButtons[index].gameObject) return;
        eventData.Use(); // The EventSystem owns repeat timing; Button Navigation.Mode.None prevents a second move.
        if (eventData.moveDir == MoveDirection.Right)
        {
            FocusActivePanel();
            return;
        }
        int direction = eventData.moveDir == MoveDirection.Up ? -1 : eventData.moveDir == MoveDirection.Down ? 1 : 0;
        if (direction == 0) return;
        for (int i = index + direction; i >= 0 && i < primaryNavigationButtons.Length; i += direction)
        {
            if (!IsAvailablePrimary(i)) continue;
            SelectPrimaryNavigationIndex(i, true);
            break; // Clamp, never wrap; moving focus never invokes an action.
        }
    }

    internal void UpdatePrimaryNavigationInput(int index, BaseEventData eventData)
    {
        if (!CanUseSettlementNavigation() || !IsAvailablePrimary(index) || EventSystem.current == null ||
            EventSystem.current.currentSelectedGameObject != primaryNavigationButtons[index].gameObject) return;
        // The installed UI Navigate action already supplies W/S, arrows and gamepad moves.
        // Space is a primary-only Submit alias. Consuming updateSelected prevents the input
        // module from also submitting/moving during this same dispatch. No input asset mutation.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            eventData.Use();
            primaryNavigationButtons[index].onClick.Invoke();
        }
    }

    private void HideRedundantBackControls()
    {
        if (repairBackButton != null) repairBackButton.gameObject.SetActive(false);
        if (!PreserveNavigationTypography) return;
        if (traitBackButton != null) traitBackButton.gameObject.SetActive(false);
    }

    private Transform ActiveContentRoot => currentPanel == SettlementPanelKind.Main ? mainPanel?.transform :
        currentPanel == SettlementPanelKind.Repair ? repairPanel?.transform :
        currentPanel == SettlementPanelKind.Trait ? traitPanel?.transform :
        currentPanel == SettlementPanelKind.DialogueArchive ? dialogueArchivePanel?.transform : sectorTechnologyPanelUI?.NavigationRoot;

    private bool IsAvailableContentFocus(Selectable target)
    {
        if (target == null || target.gameObject.scene != gameObject.scene || !target.IsActive() || !target.IsInteractable() ||
            target == repairBackButton || target == traitBackButton) return false;
        if (GetPrimaryNavigationIndex(target.gameObject) >= 0 || target == launchButton) return true;
        Transform root = ActiveContentRoot;
        return root != null && target.transform.IsChildOf(root);
    }

    private void FocusActivePanel()
    {
        Selectable preferred = currentPanel == SettlementPanelKind.Main ? shipActionButton :
            currentPanel == SettlementPanelKind.Repair ? (IsRouteCorePresentation ? routeCoreDeckButton : repairActionButton) :
            currentPanel == SettlementPanelKind.Trait ? traitActionButton :
            currentPanel == SettlementPanelKind.DialogueArchive ? dialogueArchivePanel?.NavigationEntry : sectorTechnologyPanelUI?.NavigationEntry;
        if (IsAvailableContentFocus(preferred)) EventSystem.current.SetSelectedGameObject(preferred.gameObject);
        else if (ActiveContentRoot != null)
        {
            foreach (Selectable control in ActiveContentRoot.GetComponentsInChildren<Selectable>(false))
            {
                if (!IsAvailableContentFocus(control)) continue;
                Navigation navigation = control.navigation;
                if (navigation.mode != Navigation.Mode.Explicit)
                {
                    navigation.selectOnUp = control.FindSelectableOnUp();
                    navigation.selectOnDown = control.FindSelectableOnDown();
                    navigation.selectOnRight = control.FindSelectableOnRight();
                    navigation.mode = Navigation.Mode.Explicit;
                }
                navigation.selectOnLeft = ActiveContentNavigationButton;
                control.navigation = navigation;
                EventSystem.current.SetSelectedGameObject(control.gameObject);
                break;
            }
        }
    }

    private void SubscribeButtons()
    {
        UnsubscribeButtons();
        if (openDialogueArchiveButton != null) BindOwnedButton(openDialogueArchiveButton, ShowDialogueArchivePanel);
        if (hangarNavigationButton != null)
        {
            BindOwnedButton(hangarNavigationButton, ShowMainPanel);
        }

        if (sectorTechnologyNavigationButton != null)
        {
            BindOwnedButton(sectorTechnologyNavigationButton, ShowSectorTechnologyPanel);
        }

        if (openRepairPanelButton != null)
        {
            BindOwnedButton(openRepairPanelButton, ShowRepairPanel);
        }

        if (openTraitPanelButton != null)
        {
            BindOwnedButton(openTraitPanelButton, ShowTraitPanel);
        }

        if (openSettingsPanelButton != null)
        {
            BindOwnedButton(openSettingsPanelButton, ShowSettingsPanel);
        }

        if (shipPreviousButton != null)
        {
            BindOwnedButton(shipPreviousButton, MovePreviewShipPrevious);
        }

        if (shipNextButton != null)
        {
            BindOwnedButton(shipNextButton, MovePreviewShipNext);
        }

        if (shipActionButton != null)
        {
            BindOwnedButton(shipActionButton, ExecuteShipAction);
        }

        if (launchButton != null)
        {
            BindOwnedButton(launchButton, LaunchExpedition);
        }

        if (repairActionButton != null)
        {
            BindOwnedButton(repairActionButton, ExecuteBuildingAction);
        }

        if (repairBackButton != null)
        {
            BindOwnedButton(repairBackButton, ShowMainPanel);
        }

        if (ShouldControllerHandleTraitActionButton())
        {
            BindOwnedButton(traitActionButton, ExecuteTraitAction);
        }

        if (traitBackButton != null)
        {
            BindOwnedButton(traitBackButton, ShowMainPanel);
        }

        if (repairPreviousButton != null)
        {
            BindOwnedButton(repairPreviousButton, MoveBuildingPrevious);
        }

        if (repairNextButton != null)
        {
            BindOwnedButton(repairNextButton, MoveBuildingNext);
        }
    }

    private void BindOwnedButton(Button button, UnityAction action)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == action.Method.Name &&
                button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)
            {
                return;
            }
        }
        button.onClick.AddListener(action);
        ownedButtonListeners.Add(new KeyValuePair<Button, UnityAction>(button, action));
    }

    private void UnsubscribeButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> binding in ownedButtonListeners)
        {
            if (binding.Key != null) binding.Key.onClick.RemoveListener(binding.Value);
        }
        ownedButtonListeners.Clear();
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
        if (IsRouteCorePresentation) return;
        int index = GetSelectedBuildingIndex();
        index--;

        if (index < 0)
        {
            index = buildingOrder.Length - 1;
        }

        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingOrder[index];
        RefreshAccessKeyRecoverySelection();
        Refresh();
    }

    public void MoveBuildingNext()
    {
        if (IsRouteCorePresentation) return;
        int index = GetSelectedBuildingIndex();
        index = (index + 1) % buildingOrder.Length;

        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingOrder[index];
        RefreshAccessKeyRecoverySelection();
        Refresh();
    }

    private bool TryExecuteSelectedRestorationAction()
    {
        if (IsRouteCorePresentation || settlementController == null || !RestorationPresentationReady(true) || dialogueModalActive ||
            (settingsMenuController != null && settingsMenuController.IsOpen))
        {
            return false;
        }

        return accessKeyRecoveryPresentationActive
            ? settlementController.TryRestoreDamagedAccessKey()
            : settlementController.TryCompleteRestorationProject(selectedBuilding);
    }

    private void RefreshAccessKeyRecoverySelection()
    {
        accessKeyRecoveryPresentationActive =
            selectedBuilding == BuildingType.RecoveryProcessor &&
            settlementController != null &&
            settlementController.CanRestoreDamagedAccessKey();
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
