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
    }

    [Header("Presentation")]
    [SerializeField] private Color panelColor = new Color(0.025f, 0.045f, 0.065f, 0.94f);
    [SerializeField] private Color cardColor = new Color(0.07f, 0.11f, 0.14f, 0.96f);
    [SerializeField] private Color selectedCardColor = new Color(0.08f, 0.23f, 0.29f, 0.98f);
    [SerializeField] private Color alloyColor = new Color(0.72f, 0.92f, 1f, 1f);

    private readonly List<TechnologyEntry> entries = new List<TechnologyEntry>();

    private SettlementController settlementController;
    private SettlementUIController settlementUIController;
    private GameObject repairPanel;
    private GameObject panelRoot;
    private TextMeshProUGUI selectedNameText;
    private TextMeshProUGUI selectedLevelText;
    private TextMeshProUGUI selectedDescriptionText;
    private TextMeshProUGUI selectedEffectText;
    private TextMeshProUGUI selectedCostText;
    private Button upgradeButton;
    private TextMeshProUGUI upgradeButtonText;
    private Button backButton;
    private int selectedIndex;
    private bool initialized;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void Initialize(
        SettlementController controller,
        SettlementUIController uiController,
        GameObject repairPanelObject,
        Button actionButtonPrototype,
        Button backButtonPrototype)
    {
        if (initialized || controller == null || uiController == null || repairPanelObject == null ||
            actionButtonPrototype == null || backButtonPrototype == null)
        {
            return;
        }

        TextMeshProUGUI textPrototype = actionButtonPrototype.GetComponentInChildren<TextMeshProUGUI>(true);
        if (textPrototype == null)
        {
            Debug.LogWarning("Sector Technology UI requires a TMP button-label prototype.", this);
            return;
        }

        settlementController = controller;
        settlementUIController = uiController;
        repairPanel = repairPanelObject;

        BuildPanel(actionButtonPrototype, backButtonPrototype, textPrototype);

        initialized = true;
        panelRoot.SetActive(false);
        Refresh();
    }

    private void OnEnable()
    {
        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed += Refresh;
        }
    }

    private void OnDisable()
    {
        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed -= Refresh;
        }
    }

    public void Show()
    {
        if (!initialized)
        {
            return;
        }

        repairPanel.SetActive(false);
        panelRoot.SetActive(true);
        Refresh();

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
        if (!initialized)
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
            entry.CardImage.color = i == selectedIndex ? selectedCardColor : cardColor;
            entry.CardOutline.enabled = i == selectedIndex;
        }

        RefreshSelectedDetail(progress, alloy);
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

    private void BuildPanel(
        Button actionButtonPrototype,
        Button backButtonPrototype,
        TextMeshProUGUI textPrototype)
    {
        panelRoot = CreateUiObject("SectorTechnologyPanel", repairPanel.transform.parent);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        SetRect(panelRect, Vector2.zero, new Vector2(480f, 270f));

        GameObject backgroundObject = CreateUiObject("ContentBackground", panelRect);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        SetRect(backgroundRect, new Vector2(34f, 0f), new Vector2(400f, 206f));
        Image background = backgroundObject.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;

        TextMeshProUGUI title = CreateText("Title", panelRect, textPrototype, "기체 보강", 12f, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, new Vector2(-65f, 92f), new Vector2(190f, 20f));

        BuildDetailPanel(panelRect, actionButtonPrototype, textPrototype);
        BuildTechnologyCards(panelRect, textPrototype);

        backButton = Instantiate(backButtonPrototype, panelRect);
        backButton.name = "SectorTechnologyBackButton";
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(settlementUIController.ShowRepairPanel);
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(-198f, -116f), new Vector2(68f, 22f));
        ConfigureButtonText(backButton, "뒤로", 7f);
        ConfigureButtonSound(backButton, SoundEventIds.UiBack, true, true, true);
        ConfigureEntryNavigation();
    }

    private void BuildDetailPanel(
        RectTransform parent,
        Button actionButtonPrototype,
        TextMeshProUGUI textPrototype)
    {
        GameObject detailPanel = CreateUiObject("SelectedTechnology", parent);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        SetRect(detailRect, new Vector2(-55f, -9f), new Vector2(210f, 174f));
        Image detailBackground = detailPanel.AddComponent<Image>();
        detailBackground.color = new Color(0.035f, 0.075f, 0.1f, 0.92f);

        selectedNameText = CreateText("Name", detailRect, textPrototype, string.Empty, 10f, FontStyles.Bold);
        selectedNameText.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(selectedNameText.rectTransform, new Vector2(0f, 67f), new Vector2(184f, 20f));

        selectedLevelText = CreateText("Level", detailRect, textPrototype, string.Empty, 7f, FontStyles.Normal);
        selectedLevelText.color = alloyColor;
        selectedLevelText.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(selectedLevelText.rectTransform, new Vector2(0f, 48f), new Vector2(184f, 16f));

        selectedDescriptionText = CreateText("Description", detailRect, textPrototype, string.Empty, 6.5f, FontStyles.Normal);
        selectedDescriptionText.alignment = TextAlignmentOptions.TopLeft;
        selectedDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        SetRect(selectedDescriptionText.rectTransform, new Vector2(0f, 24f), new Vector2(184f, 30f));

        selectedEffectText = CreateText("Effects", detailRect, textPrototype, string.Empty, 7f, FontStyles.Normal);
        selectedEffectText.alignment = TextAlignmentOptions.TopLeft;
        SetRect(selectedEffectText.rectTransform, new Vector2(0f, -13f), new Vector2(184f, 38f));

        selectedCostText = CreateText("Cost", detailRect, textPrototype, string.Empty, 7f, FontStyles.Normal);
        selectedCostText.color = alloyColor;
        selectedCostText.alignment = TextAlignmentOptions.MidlineLeft;
        selectedCostText.textWrappingMode = TextWrappingModes.Normal;
        SetRect(selectedCostText.rectTransform, new Vector2(0f, -49f), new Vector2(184f, 28f));

        upgradeButton = Instantiate(actionButtonPrototype, parent);
        upgradeButton.name = "UpgradeButton";
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(TryUpgradeSelected);
        SetRect(upgradeButton.GetComponent<RectTransform>(), new Vector2(85f, -116f), new Vector2(82f, 22f));
        upgradeButtonText = ConfigureButtonText(upgradeButton, "강화", 7.5f);
        ConfigureButtonSound(upgradeButton, string.Empty, false, true, true);
    }

    private void BuildTechnologyCards(RectTransform parent, TextMeshProUGUI textPrototype)
    {
        IReadOnlyList<SectorTechnologyDefinition> definitions = SectorTechnologyCatalog.Definitions;
        entries.Clear();
        entries.Capacity = Mathf.Max(entries.Capacity, definitions.Count);

        for (int i = 0; i < definitions.Count; i++)
        {
            int entryIndex = i;
            SectorTechnologyDefinition definition = definitions[i];
            GameObject card = CreateUiObject(definition.Id, parent);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            SetRect(cardRect, new Vector2(145f, 60f - (35f * i)), new Vector2(170f, 32f));

            Image cardImage = card.AddComponent<Image>();
            cardImage.color = cardColor;
            Outline cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = alloyColor;
            cardOutline.effectDistance = new Vector2(1f, -1f);
            cardOutline.useGraphicAlpha = false;
            cardOutline.enabled = false;
            Button cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            cardButton.onClick.AddListener(() => SelectTechnology(entryIndex));
            ConfigureButtonSound(cardButton, SoundEventIds.UiClick, true, true, true);

            GameObject iconObject = CreateUiObject("Icon", cardRect);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            SetRect(iconRect, new Vector2(-62f, 0f), new Vector2(22f, 22f));
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.color = definition.AccentColor;
            iconImage.raycastTarget = false;

            TextMeshProUGUI iconLabel = CreateText("IconLabel", iconRect, textPrototype, definition.IconLabel, 7f, FontStyles.Bold);
            StretchToParent(iconLabel.rectTransform);
            iconLabel.color = new Color(0.03f, 0.05f, 0.07f, 1f);

            TextMeshProUGUI nameText = CreateText("Name", cardRect, textPrototype, string.Empty, 7.5f, FontStyles.Bold);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            SetRect(nameText.rectTransform, new Vector2(20f, 7f), new Vector2(118f, 14f));

            TextMeshProUGUI summaryText = CreateText("Summary", cardRect, textPrototype, string.Empty, 6f, FontStyles.Normal);
            summaryText.alignment = TextAlignmentOptions.MidlineLeft;
            summaryText.color = new Color(0.72f, 0.84f, 0.9f, 1f);
            SetRect(summaryText.rectTransform, new Vector2(20f, -7f), new Vector2(118f, 13f));

            entries.Add(new TechnologyEntry
            {
                Definition = definition,
                CardButton = cardButton,
                CardImage = cardImage,
                CardOutline = cardOutline,
                NameText = nameText,
                SummaryText = summaryText
            });
        }

    }

    private void SelectTechnology(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, entries.Count - 1);
        ConfigureActionNavigation();
        Refresh();
    }

    private void TryUpgradeSelected()
    {
        TechnologyEntry selectedEntry = GetSelectedEntry();
        if (selectedEntry == null || settlementController == null)
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
                selectOnDown = i + 1 < entries.Count ? entries[i + 1].CardButton : backButton,
                selectOnLeft = upgradeButton,
                selectOnRight = null
            };
            button.navigation = navigation;

            SettlementSectorTechnologyEntrySelection selection =
                button.gameObject.AddComponent<SettlementSectorTechnologyEntrySelection>();
            int entryIndex = i;
            selection.Configure(() => SelectTechnology(entryIndex));
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
            selectOnRight = selectedEntry.CardButton,
            selectOnDown = backButton
        };
        upgradeButton.navigation = upgradeNavigation;

        if (backButton != null)
        {
            Navigation backNavigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = upgradeButton,
                selectOnRight = entries[entries.Count - 1].CardButton
            };
            backButton.navigation = backNavigation;
        }
    }

    private static void ConfigureButtonSound(
        Button button,
        string clickEventId,
        bool playClick,
        bool playHover,
        bool playDisabledClick)
    {
        if (button == null)
        {
            return;
        }

        UISoundButton soundButton = button.GetComponent<UISoundButton>();
        if (soundButton == null)
        {
            soundButton = button.gameObject.AddComponent<UISoundButton>();
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

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform));
        instance.layer = parent.gameObject.layer;
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TextMeshProUGUI prototype,
        string value,
        float fontSize,
        FontStyles fontStyle)
    {
        TextMeshProUGUI text = Instantiate(prototype, parent);
        text.name = name;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(5f, fontSize - 1.5f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }

    private static TextMeshProUGUI ConfigureButtonText(Button button, string label, float fontSize)
    {
        TextMeshProUGUI text = button != null
            ? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (text == null)
        {
            return null;
        }

        text.text = label;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(5f, fontSize - 1.5f);
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

public sealed class SettlementSectorTechnologyEntrySelection : MonoBehaviour,
    ISelectHandler,
    IPointerEnterHandler
{
    private Action selectionAction;

    public void Configure(Action action)
    {
        selectionAction = action;
    }

    public void OnSelect(BaseEventData eventData)
    {
        selectionAction?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        selectionAction?.Invoke();
    }
}
