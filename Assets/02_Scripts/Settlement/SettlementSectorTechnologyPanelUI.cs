using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SectorTechnologyEffectType
{
    MaxHp,
    StartingArmor,
    HealEfficiencyPercent
}

public sealed class SectorTechnologyDefinition
{
    private readonly int[] alloyCosts;

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string IconLabel { get; }
    public Color AccentColor { get; }
    public SectorTechnologyEffectType EffectType { get; }
    public int MaxLevel { get; }
    public float EffectPerLevel { get; }

    public SectorTechnologyDefinition(
        string id,
        string displayName,
        string description,
        string iconLabel,
        Color accentColor,
        SectorTechnologyEffectType effectType,
        int maxLevel,
        float effectPerLevel,
        params int[] alloyCosts)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        IconLabel = iconLabel;
        AccentColor = accentColor;
        EffectType = effectType;
        MaxLevel = Mathf.Max(1, maxLevel);
        EffectPerLevel = effectPerLevel;
        this.alloyCosts = alloyCosts ?? Array.Empty<int>();
    }

    public int GetUpgradeCost(int nextLevel)
    {
        if (nextLevel <= 0 || nextLevel > MaxLevel || nextLevel > alloyCosts.Length)
        {
            return 0;
        }

        return Mathf.Max(0, alloyCosts[nextLevel - 1]);
    }

    public float GetEffectValue(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * EffectPerLevel;
    }

    public string FormatEffect(int level)
    {
        float value = GetEffectValue(level);

        return EffectType switch
        {
            SectorTechnologyEffectType.MaxHp => $"최대 HP +{value:0}",
            SectorTechnologyEffectType.StartingArmor => $"시작 방어도 +{value:0}",
            SectorTechnologyEffectType.HealEfficiencyPercent => $"회복 효율 +{value:0}%",
            _ => string.Empty
        };
    }
}

public static class SectorTechnologyCatalog
{
    public const string StabilizedFrameId = "sector1_stabilized_frame";
    public const string ReinforcedBulkheadId = "sector1_reinforced_bulkhead";
    public const string FieldRepairLatticeId = "sector1_field_repair_lattice";

    private static readonly SectorTechnologyDefinition[] definitions =
    {
        new SectorTechnologyDefinition(
            StabilizedFrameId,
            "안정화 프레임",
            "기체 프레임의 구조 안정성을 높입니다.",
            "HP",
            new Color(0.72f, 0.86f, 0.92f, 1f),
            SectorTechnologyEffectType.MaxHp,
            3,
            2f,
            2, 3, 5
        ),
        new SectorTechnologyDefinition(
            ReinforcedBulkheadId,
            "강화 격벽",
            "출격 시 방어도 예비량을 확보합니다.",
            "AR",
            new Color(0.68f, 0.78f, 0.88f, 1f),
            SectorTechnologyEffectType.StartingArmor,
            3,
            2f,
            2, 3, 5
        ),
        new SectorTechnologyDefinition(
            FieldRepairLatticeId,
            "현장 수리 격자",
            "현장에서 받는 회복 효과를 증폭합니다.",
            "+",
            new Color(0.58f, 0.9f, 0.82f, 1f),
            SectorTechnologyEffectType.HealEfficiencyPercent,
            3,
            10f,
            2, 3, 5
        )
    };

    public static IReadOnlyList<SectorTechnologyDefinition> Definitions => definitions;

    public static bool TryGet(string id, out SectorTechnologyDefinition definition)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Id == id)
            {
                definition = definitions[i];
                return true;
            }
        }

        definition = null;
        return false;
    }
}

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

    private readonly TechnologyEntry[] entries = new TechnologyEntry[3];

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

        for (int i = 0; i < entries.Length; i++)
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
        TechnologyEntry selectedEntry = entries[Mathf.Clamp(selectedIndex, 0, entries.Length - 1)];
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
            selectedCostText.text = "필요  없음";
            upgradeButtonText.text = "최대 단계";
            upgradeButton.interactable = false;
            return;
        }

        int nextLevel = currentLevel + 1;
        int cost = definition.GetUpgradeCost(nextLevel);
        selectedEffectText.text =
            $"현재  {definition.FormatEffect(currentLevel)}\n" +
            $"다음  {definition.FormatEffect(nextLevel)}";
        selectedCostText.text = $"필요  합금 {cost}";
        upgradeButtonText.text = "업그레이드";
        upgradeButton.interactable = progress != null && alloy >= cost;
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

        TextMeshProUGUI title = CreateText("Title", panelRect, textPrototype, "지역 기술", 12f, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetRect(title.rectTransform, new Vector2(-65f, 92f), new Vector2(190f, 20f));

        BuildDetailPanel(panelRect, actionButtonPrototype, textPrototype);
        BuildTechnologyCards(panelRect, textPrototype);

        Button backButton = Instantiate(backButtonPrototype, panelRect);
        backButton.name = "SectorTechnologyBackButton";
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(settlementUIController.ShowRepairPanel);
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(-198f, -116f), new Vector2(68f, 22f));
        ConfigureButtonText(backButton, "뒤로", 7f);
        ConfigureButtonSound(backButton, SoundEventIds.UiBack, true, true, true);
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
        SetRect(selectedCostText.rectTransform, new Vector2(0f, -43f), new Vector2(184f, 18f));

        upgradeButton = Instantiate(actionButtonPrototype, parent);
        upgradeButton.name = "UpgradeButton";
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(TryUpgradeSelected);
        SetRect(upgradeButton.GetComponent<RectTransform>(), new Vector2(85f, -116f), new Vector2(82f, 22f));
        upgradeButtonText = ConfigureButtonText(upgradeButton, "업그레이드", 7.5f);
        ConfigureButtonSound(upgradeButton, string.Empty, false, true, true);
    }

    private void BuildTechnologyCards(RectTransform parent, TextMeshProUGUI textPrototype)
    {
        IReadOnlyList<SectorTechnologyDefinition> definitions = SectorTechnologyCatalog.Definitions;
        for (int i = 0; i < definitions.Count && i < entries.Length; i++)
        {
            int entryIndex = i;
            SectorTechnologyDefinition definition = definitions[i];
            GameObject card = CreateUiObject(definition.Id, parent);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            SetRect(cardRect, new Vector2(145f, 48f - (56f * i)), new Vector2(170f, 50f));

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
            SetRect(iconRect, new Vector2(-62f, 0f), new Vector2(26f, 26f));
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.color = definition.AccentColor;
            iconImage.raycastTarget = false;

            TextMeshProUGUI iconLabel = CreateText("IconLabel", iconRect, textPrototype, definition.IconLabel, 7f, FontStyles.Bold);
            StretchToParent(iconLabel.rectTransform);
            iconLabel.color = new Color(0.03f, 0.05f, 0.07f, 1f);

            TextMeshProUGUI nameText = CreateText("Name", cardRect, textPrototype, string.Empty, 7.5f, FontStyles.Bold);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            SetRect(nameText.rectTransform, new Vector2(20f, 9f), new Vector2(118f, 17f));

            TextMeshProUGUI summaryText = CreateText("Summary", cardRect, textPrototype, string.Empty, 6f, FontStyles.Normal);
            summaryText.alignment = TextAlignmentOptions.MidlineLeft;
            summaryText.color = new Color(0.72f, 0.84f, 0.9f, 1f);
            SetRect(summaryText.rectTransform, new Vector2(20f, -10f), new Vector2(118f, 17f));

            entries[i] = new TechnologyEntry
            {
                Definition = definition,
                CardButton = cardButton,
                CardImage = cardImage,
                CardOutline = cardOutline,
                NameText = nameText,
                SummaryText = summaryText
            };
        }
    }

    private void SelectTechnology(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, entries.Length - 1);
        Refresh();
    }

    private void TryUpgradeSelected()
    {
        TechnologyEntry selectedEntry = entries[Mathf.Clamp(selectedIndex, 0, entries.Length - 1)];
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
