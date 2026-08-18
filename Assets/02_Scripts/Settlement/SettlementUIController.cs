using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum SettlementPanelKind
{
    Main,
    Repair,
    Trait,
    SectorTechnology
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
    private sealed class NavigationButtonView
    {
        public Button Button;
        public Image Background;
        public Image Icon;
        public Image ActiveStrip;
        public Outline ActiveOutline;
        public TextMeshProUGUI Label;
        public Color AccentColor;
    }

    [Header("References")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementHUD hud;
    [SerializeField] private EscSettingsMenuController settingsMenuController;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject repairPanel;
    [SerializeField] private GameObject traitPanel;

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

    [Header("Sector Technology Panel")]
    [SerializeField] private SettlementSectorTechnologyPanelUI sectorTechnologyPanelUI;

    private Button hangarNavigationButton;
    private Button sectorTechnologyNavigationButton;
    private NavigationButtonView hangarNavigationView;
    private NavigationButtonView repairNavigationView;
    private NavigationButtonView sectorTechnologyNavigationView;
    private NavigationButtonView traitNavigationView;
    private NavigationButtonView settingsNavigationView;

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

        BuildPersistentNavigation();
    }

    private void OnEnable()
    {
        ConfigureSettlementButtonSounds();
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

    public void ShowSectorTechnologyPanel()
    {
        if (sectorTechnologyPanelUI == null)
        {
            return;
        }

        currentPanel = SettlementPanelKind.SectorTechnology;
        SetPanelActive(mainPanel, false);
        SetPanelActive(repairPanel, false);
        SetPanelActive(traitPanel, false);
        CloseSettingsOverlay();
        sectorTechnologyPanelUI.Show();
        RefreshNavigationState();
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
        settingsMenuController?.Open();
        RefreshNavigationState();
    }

    private void CloseSettingsOverlay()
    {
        if (settingsMenuController != null && settingsMenuController.IsOpen)
        {
            settingsMenuController.Close();
        }

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
            bool success = settlementController.TryRepairOrUpgradeBuilding(selectedBuilding);
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

        AudioManager.Play(SoundEventIds.UiLaunch);
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

        SettlementRepairViewData repairViewData = settlementController.BuildBuildingRepairViewData(selectedBuilding);
        string actionLabel = settlementController.GetBuildingActionLabel(selectedBuilding);
        int currentLevel = settlementController.GetBuildingLevel(selectedBuilding);

        hud.SetRepairDetail(repairViewData, actionLabel);
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

    private void BuildPersistentNavigation()
    {
        if (openRepairPanelButton == null || mainPanel == null || mainPanel.transform.parent == null)
        {
            return;
        }

        Transform navigationParent = mainPanel.transform.parent;
        BuildNavigationBackground(navigationParent);
        hangarNavigationButton = Instantiate(openRepairPanelButton, navigationParent);
        hangarNavigationButton.name = "HangarNavigationButton";
        hangarNavigationButton.onClick.RemoveAllListeners();

        sectorTechnologyNavigationButton = Instantiate(openRepairPanelButton, navigationParent);
        sectorTechnologyNavigationButton.name = "SectorTechnologyNavigationButton";
        sectorTechnologyNavigationButton.onClick.RemoveAllListeners();

        Button legacySettingsButton = openSettingsPanelButton;
        openSettingsPanelButton = Instantiate(openRepairPanelButton, navigationParent);
        openSettingsPanelButton.name = "SettingsNavigationButton";
        openSettingsPanelButton.onClick.RemoveAllListeners();
        if (legacySettingsButton != null)
        {
            legacySettingsButton.gameObject.SetActive(false);
        }

        hangarNavigationView = ConfigureNavigationButton(hangarNavigationButton, "격납고", new Vector2(-208f, 70f), new Color(0.55f, 0.95f, 1f, 1f));
        repairNavigationView = ConfigureNavigationButton(openRepairPanelButton, "보수", new Vector2(-208f, 42f), new Color(1f, 0.66f, 0.28f, 1f));
        sectorTechnologyNavigationView = ConfigureNavigationButton(sectorTechnologyNavigationButton, "지역 기술", new Vector2(-208f, 14f), new Color(0.72f, 0.92f, 1f, 1f));
        traitNavigationView = ConfigureNavigationButton(openTraitPanelButton, "추가 특성", new Vector2(-208f, -14f), new Color(0.75f, 0.45f, 1f, 1f));
        settingsNavigationView = ConfigureNavigationButton(openSettingsPanelButton, "설정", new Vector2(-208f, -42f), new Color(0.52f, 0.72f, 0.82f, 1f));

        if (launchButton != null)
        {
            RectTransform launchRect = launchButton.transform as RectTransform;
            SetNavigationRect(launchRect, new Vector2(184f, -116f), new Vector2(104f, 26f));
            ConfigureButtonLabel(launchButton, "탐사 시작", 8f);
        }

        ConfigureButtonLabel(shipActionButton, "기체 선택", 7f);
        ConfigureButtonLabel(repairActionButton, "수리", 7f);
        ConfigureButtonLabel(repairBackButton, "뒤로", 7f);
        ConfigureButtonLabel(traitActionButton, "해금", 7f);
        ConfigureButtonLabel(traitBackButton, "뒤로", 7f);
        BuildStationHeader(navigationParent);
        RefreshNavigationState();
    }

    private static void BuildNavigationBackground(Transform parent)
    {
        GameObject backgroundObject = new GameObject("NavigationBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.layer = parent.gameObject.layer;
        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetNavigationRect(rect, new Vector2(-208f, 14f), new Vector2(70f, 178f));
        rect.SetAsFirstSibling();

        Image image = backgroundObject.GetComponent<Image>();
        image.color = new Color(0.025f, 0.055f, 0.08f, 0.88f);
        image.raycastTarget = false;
    }

    private NavigationButtonView ConfigureNavigationButton(Button button, string label, Vector2 position, Color iconColor)
    {
        if (button == null)
        {
            return null;
        }

        SetNavigationRect(button.transform as RectTransform, position, new Vector2(62f, 24f));
        ConfigureButtonLabel(button, label, 7f);
        Image icon = AddNavigationIcon(button, iconColor);
        return CreateNavigationButtonView(button, icon, iconColor);
    }

    private static Image AddNavigationIcon(Button button, Color color)
    {
        if (button == null)
        {
            return null;
        }

        GameObject iconObject = new GameObject("NavigationIcon", typeof(RectTransform), typeof(Image));
        iconObject.layer = button.gameObject.layer;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(button.transform, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(7f, 0f);
        iconRect.sizeDelta = new Vector2(5f, 10f);

        Image icon = iconObject.GetComponent<Image>();
        icon.color = color;
        icon.raycastTarget = false;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-3f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        return icon;
    }

    private static NavigationButtonView CreateNavigationButtonView(Button button, Image icon, Color accentColor)
    {
        Image background = button.targetGraphic as Image;
        if (background == null)
        {
            background = button.GetComponent<Image>();
        }

        GameObject stripObject = new GameObject("NavigationActiveStrip", typeof(RectTransform), typeof(Image));
        stripObject.layer = button.gameObject.layer;
        RectTransform stripRect = stripObject.GetComponent<RectTransform>();
        stripRect.SetParent(button.transform, false);
        stripRect.anchorMin = new Vector2(0f, 0.16f);
        stripRect.anchorMax = new Vector2(0f, 0.84f);
        stripRect.pivot = new Vector2(0f, 0.5f);
        stripRect.anchoredPosition = new Vector2(1f, 0f);
        stripRect.sizeDelta = new Vector2(2f, 0f);

        Image activeStrip = stripObject.GetComponent<Image>();
        activeStrip.color = accentColor;
        activeStrip.raycastTarget = false;
        activeStrip.enabled = false;

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
        {
            outline = button.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0.32f, 0.92f, 1f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;

        return new NavigationButtonView
        {
            Button = button,
            Background = background,
            Icon = icon,
            ActiveStrip = activeStrip,
            ActiveOutline = outline,
            Label = button.GetComponentInChildren<TextMeshProUGUI>(true),
            AccentColor = accentColor
        };
    }

    private void RefreshNavigationState()
    {
        bool settingsOpen = settingsMenuController != null && settingsMenuController.IsOpen;

        SetNavigationViewActive(
            hangarNavigationView,
            !settingsOpen && currentPanel == SettlementPanelKind.Main
        );
        SetNavigationViewActive(
            repairNavigationView,
            !settingsOpen && currentPanel == SettlementPanelKind.Repair
        );
        SetNavigationViewActive(
            sectorTechnologyNavigationView,
            !settingsOpen && currentPanel == SettlementPanelKind.SectorTechnology
        );
        SetNavigationViewActive(
            traitNavigationView,
            !settingsOpen && currentPanel == SettlementPanelKind.Trait
        );
        SetNavigationViewActive(settingsNavigationView, settingsOpen);
    }

    private static void SetNavigationViewActive(NavigationButtonView view, bool active)
    {
        if (view == null)
        {
            return;
        }

        Color backgroundColor = active
            ? new Color(0.075f, 0.24f, 0.3f, 1f)
            : new Color(0.055f, 0.1f, 0.14f, 0.92f);

        if (view.Background != null)
        {
            view.Background.color = backgroundColor;
        }

        if (view.ActiveStrip != null)
        {
            view.ActiveStrip.enabled = active;
        }

        if (view.ActiveOutline != null)
        {
            view.ActiveOutline.enabled = active;
        }

        if (view.Label != null)
        {
            view.Label.color = active
                ? new Color(0.9f, 0.98f, 1f, 1f)
                : new Color(0.66f, 0.75f, 0.8f, 1f);
        }

        if (view.Icon != null)
        {
            Color iconColor = view.AccentColor;
            iconColor.a = active ? 1f : 0.56f;
            view.Icon.color = iconColor;
        }
    }

    private static void ConfigureButtonLabel(Button button, string label, float fontSize)
    {
        TextMeshProUGUI text = button != null
            ? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (text == null)
        {
            return;
        }

        text.text = label;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(5f, fontSize - 1.5f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private static void SetNavigationRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private void BuildStationHeader(Transform parent)
    {
        TextMeshProUGUI prototype = openRepairPanelButton != null
            ? openRepairPanelButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (prototype == null)
        {
            return;
        }

        TextMeshProUGUI title = Instantiate(prototype, parent);
        title.name = "SettlementStationHeader";
        title.text = "VOID SCRAPPER  /  정착지";
        title.fontSize = 10f;
        title.enableAutoSizing = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = new Color(0.78f, 0.94f, 1f, 1f);
        title.raycastTarget = false;
        RectTransform rect = title.rectTransform;
        SetNavigationRect(rect, new Vector2(-137f, 123f), new Vector2(196f, 18f));
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
        if (settlementController != null)
        {
            settlementController.Changed += Refresh;
        }

        if (settingsMenuController != null)
        {
            settingsMenuController.OpenStateChanged += HandleSettingsOpenStateChanged;
        }
    }

    private void UnsubscribeController()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }

        if (settingsMenuController != null)
        {
            settingsMenuController.OpenStateChanged -= HandleSettingsOpenStateChanged;
        }
    }

    private void HandleSettingsOpenStateChanged(bool isOpen)
    {
        RefreshNavigationState();
    }

    private void SubscribeButtons()
    {
        if (hangarNavigationButton != null)
        {
            hangarNavigationButton.onClick.AddListener(ShowMainPanel);
        }

        if (sectorTechnologyNavigationButton != null)
        {
            sectorTechnologyNavigationButton.onClick.AddListener(ShowSectorTechnologyPanel);
        }

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
        if (hangarNavigationButton != null)
        {
            hangarNavigationButton.onClick.RemoveListener(ShowMainPanel);
        }

        if (sectorTechnologyNavigationButton != null)
        {
            sectorTechnologyNavigationButton.onClick.RemoveListener(ShowSectorTechnologyPanel);
        }

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
