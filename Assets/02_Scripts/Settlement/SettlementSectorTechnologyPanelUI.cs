using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettlementSectorTechnologyPanelUI : MonoBehaviour
{
    private sealed class TechnologyEntry
    {
        public SectorTechnologyDefinition Definition;
        public Button CardButton;
        public Image CardImage;
        public Outline CardOutline;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI SummaryText;
        public Color BaseColor;
        public bool IsPreviewed;
        public UnityEngine.Events.UnityAction ClickAction;
    }

    [Header("Presentation")]
    [SerializeField] private Color panelColor = new Color(0.025f, 0.045f, 0.065f, 0.94f);
    [SerializeField] private Color cardColor = new Color(0.07f, 0.11f, 0.14f, 0.96f);
    [SerializeField] private Color selectedCardColor = SettlementSelectionColors.SelectedBackground;
    [SerializeField] private Color alloyColor = new Color(0.72f, 0.92f, 1f, 1f);

    private readonly List<TechnologyEntry> entries = new List<TechnologyEntry>();

    [Header("Authored Ship Reinforcement UI")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementUIController settlementUIController;
    [SerializeField] private GameObject repairPanel;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image panelBackground;
    [SerializeField] private TextMeshProUGUI headingText;
    [SerializeField] private Image detailBackground;
    [SerializeField] private RectTransform catalogRoot;
    [SerializeField] private SettlementSectorTechnologyEntrySelection cardTemplate;
    [Tooltip("Clone offset from the authored template. Applied only when populating the catalog; no per-frame layout writes.")]
    [SerializeField] private Vector2 cardStep = new Vector2(0f, -35f);
    [SerializeField] private TextMeshProUGUI selectedNameText;
    [SerializeField] private TextMeshProUGUI selectedLevelText;
    [SerializeField] private TextMeshProUGUI selectedDescriptionText;
    [SerializeField] private TextMeshProUGUI selectedEffectText;
    [SerializeField] private TextMeshProUGUI selectedCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    private int selectedIndex;
    private bool initialized;
    private bool warnedMissingPresentation;
    private SettlementController subscribedController;
    private readonly List<string> bindingErrors = new List<string>();

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    public bool CanShow => initialized && CheckRuntimePresentation();
    public Transform NavigationRoot => panelRoot != null ? panelRoot.transform : null;
    public Selectable NavigationEntry => !CanShow ? null : upgradeButton != null && upgradeButton.IsInteractable()
        ? upgradeButton : GetSelectedEntry()?.CardButton;

    public void Initialize(
        SettlementController controller,
        SettlementUIController uiController,
        GameObject repairPanelObject,
        Button actionButtonPrototype,
        Button backButtonPrototype)
    {
        if (initialized)
        {
            Subscribe();
            Refresh();
            return;
        }
        if (controller == null || uiController == null || repairPanelObject == null)
        {
            string field = controller == null ? nameof(settlementController) : uiController == null ? nameof(settlementUIController) : nameof(repairPanel);
            WarnMissing(BindingDiagnostic(GetType().Name + "." + field, null, transform, gameObject.scene, "Missing initialization binding"));
            return;
        }
        if ((settlementController != null && settlementController != controller) ||
            (settlementUIController != null && settlementUIController != uiController) ||
            (repairPanel != null && repairPanel != repairPanelObject))
        {
            string field = settlementController != null && settlementController != controller ? nameof(settlementController) :
                settlementUIController != null && settlementUIController != uiController ? nameof(settlementUIController) : nameof(repairPanel);
            Component actual = field == nameof(settlementController) ? settlementController :
                field == nameof(settlementUIController) ? settlementUIController : repairPanel.transform;
            WarnMissing(BindingDiagnostic(GetType().Name + "." + field, actual, transform, gameObject.scene,
                "Assigned owner conflicts with the initializing Settlement navigation controller"));
            return;
        }
        settlementController = controller;
        settlementUIController = uiController;
        repairPanel = repairPanelObject;
        bindingErrors.Clear();
        CollectPresentationErrors(bindingErrors);
        if (bindingErrors.Count != 0)
        {
            WarnMissing(string.Join("; ", bindingErrors));
            return;
        }
        // Prototype arguments are retained for existing callers, never cloned at runtime.
        panelRoot.SetActive(false);
        PopulateAuthoredCards();
        BindUpgradeAction();
        ConfigureEntryNavigation();
        initialized = true;
        panelRoot.SetActive(false);
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        if (initialized) BindUpgradeAction();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(TryUpgradeSelected);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(TryUpgradeSelected);
        foreach (TechnologyEntry entry in entries)
        {
            if (entry.CardButton == null) continue;
            entry.CardButton.onClick.RemoveListener(entry.ClickAction);
            SettlementSectorTechnologyEntrySelection selection = entry.CardButton.GetComponent<SettlementSectorTechnologyEntrySelection>();
            if (selection != null) selection.Configure(null);
        }
    }

    public void Show()
    {
        if (!CanShow)
        {
            return;
        }

        repairPanel.SetActive(false);
        panelRoot.SetActive(true);
        Refresh();
        ConfigureActionNavigation();

        TechnologyEntry selectedEntry = GetSelectedEntry();
        if (selectedEntry != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selectedEntry.CardButton.gameObject);
        }
    }

    public void Hide(bool restoreRepairPanel)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (restoreRepairPanel && repairPanel != null)
        {
            repairPanel.SetActive(true);
        }
    }

    public void Refresh()
    {
        if (!initialized || !CheckRuntimePresentation())
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        int alloy = progress != null ? progress.StabilizedAlloy : 0;

        for (int i = 0; i < entries.Count; i++)
        {
            TechnologyEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            int level = progress != null
                ? progress.GetSectorTechnologyLevel(entry.Definition.Id)
                : 0;
            entry.NameText.text = entry.Definition.DisplayName;
            entry.SummaryText.text = $"Lv {level}/{entry.Definition.MaxLevel}  ·  {entry.Definition.FormatEffect(level)}";
            entry.CardImage.color = i == selectedIndex ? selectedCardColor :
                entry.IsPreviewed ? SettlementSelectionColors.HoverBackground : entry.BaseColor;
            entry.CardOutline.enabled = i == selectedIndex || entry.IsPreviewed;
            entry.CardOutline.effectColor = i == selectedIndex
                ? SettlementSelectionColors.Selected : SettlementSelectionColors.Hover;
        }

        // Selectable.interactable=false synchronously clears EventSystem selection.
        // Capture our ownership before refreshing availability, not after that setter.
        EventSystem focusOwner = EventSystem.current;
        bool actionHadFocus = IsOpen && settlementUIController.CanUseSettlementNavigation() &&
            focusOwner != null && focusOwner.currentSelectedGameObject == upgradeButton.gameObject;
        RefreshSelectedDetail(progress, alloy);
        ConfigureActionNavigation();
        if (actionHadFocus && focusOwner != null && EventSystem.current == focusOwner &&
            IsOpen && settlementUIController.CanUseSettlementNavigation() && !upgradeButton.IsInteractable() &&
            (focusOwner.currentSelectedGameObject == null || focusOwner.currentSelectedGameObject == upgradeButton.gameObject))
        {
            Button selectedCard = GetSelectedEntry()?.CardButton;
            if (selectedCard == null || !selectedCard.IsActive() || !selectedCard.IsInteractable())
                selectedCard = settlementUIController.ActiveContentNavigationButton;
            if (selectedCard != null && selectedCard.IsActive() && selectedCard.IsInteractable())
                focusOwner.SetSelectedGameObject(selectedCard.gameObject);
        }
    }

    private void RefreshSelectedDetail(PermanentProgress progress, int alloy)
    {
        TechnologyEntry selectedEntry = GetSelectedEntry();
        if (selectedEntry == null)
        {
            return;
        }

        SectorTechnologyDefinition definition = selectedEntry.Definition;
        int currentLevel = progress != null
            ? progress.GetSectorTechnologyLevel(definition.Id)
            : 0;
        bool isMaxLevel = currentLevel >= definition.MaxLevel;

        selectedNameText.text = definition.DisplayName;
        selectedLevelText.text = $"Lv {currentLevel} / {definition.MaxLevel}";
        selectedDescriptionText.text = definition.Description;

        if (isMaxLevel)
        {
            selectedEffectText.text = $"현재  {definition.FormatEffect(currentLevel)}\n다음  최대 단계";
            selectedCostText.text = $"필요  없음\n보유  안정화 합금 {alloy}";
            upgradeButtonText.text = "최대 단계";
            upgradeButton.interactable = false;
            return;
        }

        int nextLevel = currentLevel + 1;
        int cost = definition.GetUpgradeCost(nextLevel);
        selectedEffectText.text =
            $"현재  {definition.FormatEffect(currentLevel)}\n" +
            $"다음  {definition.FormatEffect(nextLevel)}";
        selectedCostText.text = $"필요  안정화 합금 {cost}\n보유  {alloy}";
        bool canAfford = progress != null && alloy >= cost;
        upgradeButtonText.text = canAfford ? "강화" : "합금 부족";
        upgradeButton.interactable = canAfford;
    }


    private void SelectTechnology(int index)
    {
        if (!isActiveAndEnabled || !IsOpen || settlementUIController == null ||
            !settlementUIController.CanUseSettlementNavigation()) return;
        selectedIndex = Mathf.Clamp(index, 0, entries.Count - 1);
        Refresh();
    }

    private void PreviewTechnology(int index, bool preview)
    {
        if (index < 0 || index >= entries.Count) return;
        if (preview && (!isActiveAndEnabled || !IsOpen || settlementUIController == null ||
            !settlementUIController.CanUseSettlementNavigation())) return;
        entries[index].IsPreviewed = preview;
        // Details and Upgrade deliberately remain bound to the yellow purchase target.
        if (isActiveAndEnabled && IsOpen) Refresh();
    }

    private void TryUpgradeSelected()
    {
        TechnologyEntry selectedEntry = GetSelectedEntry();
        if (selectedEntry == null || settlementController == null || !isActiveAndEnabled || !IsOpen || !CheckRuntimePresentation() ||
            !settlementUIController.CanUseSettlementNavigation() || !upgradeButton.IsInteractable())
        {
            return;
        }

        int levelBefore = settlementController.GetSectorTechnologyLevel(selectedEntry.Definition.Id);
        bool success = settlementController.TryUpgradeSectorTechnology(selectedEntry.Definition.Id);
        AudioManager.Play(!success
            ? SoundEventIds.UiDisabled
            : levelBefore <= 0
                ? SoundEventIds.UiUnlock
                : SoundEventIds.UiUpgradeSuccess);
        Refresh();
    }

    private TechnologyEntry GetSelectedEntry()
    {
        if (entries.Count == 0)
        {
            return null;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, entries.Count - 1);
        return entries[selectedIndex];
    }

    private void ConfigureEntryNavigation()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Button button = entries[i].CardButton;
            Navigation navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? entries[i - 1].CardButton : null,
                selectOnDown = i + 1 < entries.Count ? entries[i + 1].CardButton : null,
                selectOnLeft = upgradeButton,
                selectOnRight = null
            };
            button.navigation = navigation;

            SettlementSectorTechnologyEntrySelection selection = button.GetComponent<SettlementSectorTechnologyEntrySelection>();
            int entryIndex = i;
            selection.Configure(preview => PreviewTechnology(entryIndex, preview));
        }

        ConfigureActionNavigation();
    }

    private void ConfigureActionNavigation()
    {
        TechnologyEntry selectedEntry = GetSelectedEntry();
        if (selectedEntry == null)
        {
            return;
        }

        Navigation upgradeNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = settlementUIController != null && settlementUIController.PreserveNavigationTypography
                ? settlementUIController.ActiveContentNavigationButton : null,
            selectOnRight = selectedEntry.CardButton,
            selectOnDown = null
        };
        upgradeButton.navigation = upgradeNavigation;

        // When an upgrade is locked, card Left must still provide a route to the sidebar.
        foreach (TechnologyEntry entry in entries)
        {
            Navigation cardNavigation = entry.CardButton.navigation;
            cardNavigation.selectOnLeft = upgradeButton.IsInteractable() ? upgradeButton : upgradeNavigation.selectOnLeft;
            entry.CardButton.navigation = cardNavigation;
        }

    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || subscribedController == settlementController) return;
        Unsubscribe();
        subscribedController = settlementController;
        if (subscribedController != null) subscribedController.Changed += Refresh;
    }

    private void Unsubscribe()
    {
        if (subscribedController != null) subscribedController.Changed -= Refresh;
        subscribedController = null;
    }

    private void BindUpgradeAction()
    {
        if (upgradeButton == null) return;
        upgradeButton.onClick.RemoveListener(TryUpgradeSelected);
        for (int i = 0; i < upgradeButton.onClick.GetPersistentEventCount(); i++)
        {
            if (upgradeButton.onClick.GetPersistentTarget(i) == this &&
                upgradeButton.onClick.GetPersistentMethodName(i) == nameof(TryUpgradeSelected) &&
                upgradeButton.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off) return;
        }
        upgradeButton.onClick.AddListener(TryUpgradeSelected);
    }

    private void PopulateAuthoredCards()
    {
        IReadOnlyList<SectorTechnologyDefinition> definitions = SectorTechnologyCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorTechnologyDefinition definition = definitions[i];
            SettlementSectorTechnologyEntrySelection card = Instantiate(cardTemplate, catalogRoot);
            card.name = definition.Id;
            ((RectTransform)card.transform).anchoredPosition = ((RectTransform)cardTemplate.transform).anchoredPosition + cardStep * i;
            card.Icon.color *= definition.AccentColor;
            card.IconLabel.text = definition.IconLabel;
            int index = i;
            UnityEngine.Events.UnityAction click = () => SelectTechnology(index);
            card.CardButton.onClick.AddListener(click);
            entries.Add(new TechnologyEntry
            {
                Definition = definition, CardButton = card.CardButton, CardImage = card.CardImage,
                CardOutline = card.CardOutline, NameText = card.NameText, SummaryText = card.SummaryText,
                BaseColor = card.CardImage.color, ClickAction = click
            });
            card.gameObject.SetActive(true);
        }
    }

    private bool CheckRuntimePresentation()
    {
        bindingErrors.Clear();
        CollectPresentationErrors(bindingErrors);
        foreach (TechnologyEntry entry in entries)
        {
            if (entry.CardButton == null || entry.CardButton.GetComponent<SettlementSectorTechnologyEntrySelection>() == null || entry.CardImage == null || entry.CardOutline == null ||
                entry.NameText == null || entry.SummaryText == null)
            {
                bindingErrors.Add("A populated card lost its bindings. Reopen the scene after repairing the template.");
                break;
            }
        }
        if (bindingErrors.Count == 0) return true;
        WarnMissing(string.Join("; ", bindingErrors));
        GameObject focus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (focus != null && panelRoot != null && focus.transform.IsChildOf(panelRoot.transform) &&
            settlementUIController != null && settlementUIController.CanUseSettlementNavigation())
        {
            Button sidebar = settlementUIController.ActiveContentNavigationButton;
            if (sidebar != null && sidebar.IsActive() && sidebar.IsInteractable())
                EventSystem.current.SetSelectedGameObject(sidebar.gameObject);
        }
        return false;
    }

    private void WarnMissing(string reason)
    {
        if (warnedMissingPresentation) return;
        warnedMissingPresentation = true;
        Debug.LogWarning("Ship Reinforcement UI unavailable: " + reason +
            " Owner: " + BindingLocation(transform) +
            " Restore the listed authored Inspector bindings. Other Settlement panels remain available.", this);
    }

    public void CollectPresentationErrors(List<string> errors)
    {
        Transform root = panelRoot != null ? panelRoot.transform : null;
        CheckBinding(settlementController, null, nameof(settlementController), errors);
        CheckBinding(settlementUIController, null, nameof(settlementUIController), errors);
        CheckBinding(repairPanel != null ? repairPanel.transform : null, null, nameof(repairPanel), errors);
        CheckBinding(root, repairPanel != null ? repairPanel.transform.parent : null, nameof(panelRoot), errors);
        CheckBinding(panelBackground, root, nameof(panelBackground), errors);
        CheckBinding(headingText, root, nameof(headingText), errors);
        CheckBinding(detailBackground, root, nameof(detailBackground), errors);
        if (panelBackground != null && panelBackground == detailBackground) errors.Add("Duplicate panel/detail background mapping.");
        CheckBinding(catalogRoot, root, nameof(catalogRoot), errors);
        CheckBinding(cardTemplate, catalogRoot, nameof(cardTemplate), errors);
        CheckBinding(upgradeButton, root, nameof(upgradeButton), errors);
        if (upgradeButton != null && (upgradeButton.targetGraphic == null ||
            !upgradeButton.targetGraphic.transform.IsChildOf(upgradeButton.transform)))
            errors.Add("UpgradeButton requires its own targetGraphic.");
        CheckBinding(upgradeButtonText, upgradeButton != null ? upgradeButton.transform : null, nameof(upgradeButtonText), errors);
        Transform detail = detailBackground != null ? detailBackground.transform : null;
        var texts = new[] { selectedNameText, selectedLevelText, selectedDescriptionText, selectedEffectText, selectedCostText };
        var fields = new[] { nameof(selectedNameText), nameof(selectedLevelText), nameof(selectedDescriptionText), nameof(selectedEffectText), nameof(selectedCostText) };
        var unique = new HashSet<TextMeshProUGUI>();
        for (int i = 0; i < texts.Length; i++)
        {
            CheckBinding(texts[i], detail, fields[i], errors);
            if (texts[i] != null && !unique.Add(texts[i]))
                errors.Add(BindingDiagnostic(GetType().Name + "." + fields[i], texts[i], detail, gameObject.scene, "Duplicate selected detail text mapping"));
        }
        if (root != null && repairPanel != null && root.parent != repairPanel.transform.parent)
            errors.Add("panelRoot must be a sibling of the Restoration panel.");
        if (cardTemplate != null)
        {
            cardTemplate.CollectBindingErrors(errors);
            if (cardTemplate.gameObject.activeSelf) errors.Add("cardTemplate must remain inactive; it is not a catalog entry.");
        }
    }

    private void CheckBinding(Component value, Transform parent, string field, List<string> errors)
    {
        string property = GetType().Name + "." + field + " on " + BindingLocation(transform);
        if (value == null)
        {
            errors.Add(BindingDiagnostic(property, value, parent, gameObject.scene, "Missing binding"));
            return;
        }
        if (value.gameObject.scene != gameObject.scene)
            errors.Add(BindingDiagnostic(property, value, parent, gameObject.scene, "Scene ownership mismatch (cross-scene reference)"));
        else if (parent != null && (value is TMP_Text
            ? value.transform == parent || !value.transform.IsChildOf(parent)
            : value.transform.parent != parent))
            errors.Add(BindingDiagnostic(property, value, parent, gameObject.scene,
                value is TMP_Text ? "Ancestry mismatch (expected a descendant)" : "Ancestry mismatch (expected a direct child)"));
        if (value is TMP_Text text && text.font == null)
            errors.Add(BindingDiagnostic(property, value, parent, gameObject.scene, "Missing TMP font"));
    }

    // Shared by this panel's runtime validation and its Editor installer; no scene
    // loading policy is inferred from an ancestry failure. Handles distinguish unsaved scenes.
    public static string BindingDiagnostic(string field, Component actual, Transform expectedContainer,
        UnityEngine.SceneManagement.Scene expectedScene, string reason)
    {
        return $"{reason} for '{field}'. Actual: {BindingLocation(actual != null ? actual.transform : null)}. " +
            $"Expected container: {BindingLocation(expectedContainer)}; expected scene: {BindingScene(expectedScene)}. " +
            "Resolve the specific binding manually if necessary; no object should be reparented or valid reference cleared to bypass this check.";
    }

    public static string BindingLocation(Transform value)
    {
        if (value == null) return "<null>";
        string path = value.name;
        Transform parent = value.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return $"'{path}' in {BindingScene(value.gameObject.scene)}";
    }

    private static string BindingScene(UnityEngine.SceneManagement.Scene scene)
    {
        return $"scene '{scene.path}' (name='{scene.name}', handle={scene.handle}, valid={scene.IsValid()}, loaded={scene.isLoaded})";
    }

}
