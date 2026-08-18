using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BuildingPreviewSpriteSet
{
    [SerializeField] private BuildingType buildingType;

    [Tooltip("0 = 미수리/파손, 1 = 수리 완료, 2 이상 = 업그레이드 단계")]
    [SerializeField] private Sprite[] levelSprites;

    public BuildingType BuildingType => buildingType;

    public Sprite GetSprite(int level)
    {
        if (levelSprites == null || levelSprites.Length == 0)
        {
            return null;
        }

        level = Mathf.Clamp(level, 0, levelSprites.Length - 1);

        Sprite sprite = levelSprites[level];
        if (sprite != null)
        {
            return sprite;
        }

        // 해당 레벨 이미지가 비어 있으면 낮은 레벨 쪽에서 가장 가까운 이미지 사용.
        for (int i = level - 1; i >= 0; i--)
        {
            if (levelSprites[i] != null)
            {
                return levelSprites[i];
            }
        }

        // 낮은 레벨에도 없으면 높은 레벨 쪽에서 대체 이미지 사용.
        for (int i = level + 1; i < levelSprites.Length; i++)
        {
            if (levelSprites[i] != null)
            {
                return levelSprites[i];
            }
        }

        return null;
    }
}

public class SettlementHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Image scrapCurrencyIcon;
    [SerializeField] private Image coreCurrencyIcon;
    [SerializeField] private Color alloyCurrencyColor = new Color(0.72f, 0.92f, 1f, 1f);

    [Header("Main Panel - Ship")]
    [SerializeField] private Image shipPreviewImage;
    [SerializeField] private TextMeshProUGUI shipTitleText;
    [SerializeField] private TextMeshProUGUI shipBodyText;
    [SerializeField] private TextMeshProUGUI shipActionButtonLabelText;
    [SerializeField] private TextMeshProUGUI selectedShipText;
    [SerializeField] private TextMeshProUGUI selectedWeaponText;

    [Header("Main Panel - Curse Preview")]
    [SerializeField] private TraitDefinition pixelCurseTrait;
    [SerializeField] private Sprite cursedPreviewSprite;
    [SerializeField] private Color machineGunAccentColor = new Color(0.25f, 1f, 0.42f, 0.52f);
    [SerializeField] private Color shotgunAccentColor = new Color(1f, 0.48f, 0.12f, 0.52f);
    [SerializeField] private Color sniperAccentColor = new Color(0.25f, 0.68f, 1f, 0.52f);
    [SerializeField] private Color curseEdgeColor = new Color(0.72f, 0.24f, 1f, 0.52f);
    [SerializeField] private Color curseGhostColor = new Color(0.72f, 0.24f, 1f, 0.28f);

    [Header("Main Panel - Ship Page Indicators")]
    [Tooltip("기체 선택 원. ShipDefinitions 순서와 동일하게 배치.")]
    [SerializeField] private Image[] shipIndicatorImages;

    [Header("Repair Panel")]
    [SerializeField] private TextMeshProUGUI repairTitleText;
    [SerializeField] private TextMeshProUGUI repairActionButtonLabelText;

    [Header("Repair Panel - Detail Text")]
    [SerializeField] private TextMeshProUGUI repairDescriptionText;
    [SerializeField] private TextMeshProUGUI repairCurrentStageText;
    [SerializeField] private TextMeshProUGUI repairCurrentEffectText;
    [SerializeField] private TextMeshProUGUI repairNextStageText;
    [SerializeField] private TextMeshProUGUI repairNextEffectText;
    [SerializeField] private TextMeshProUGUI repairRequiredCurrencyText;

    [Header("Repair Panel - Cost Icons")]
    [Tooltip("수리/강화에 스크랩이 필요할 때 활성화할 아이콘 루트입니다.")]
    [SerializeField] private GameObject repairScrapCostIconRoot;
    [Tooltip("수리/강화에 코어 조각이 필요할 때 활성화할 아이콘 루트입니다.")]
    [SerializeField] private GameObject repairCoreShardCostIconRoot;
    [Tooltip("스크랩 아이콘 Image입니다. 비워두면 Root의 Image를 사용합니다.")]
    [SerializeField] private Image repairScrapCostIconImage;
    [Tooltip("코어 조각 아이콘 Image입니다. 비워두면 Root의 Image를 사용합니다.")]
    [SerializeField] private Image repairCoreShardCostIconImage;
    [SerializeField] private Sprite repairScrapCostIconSprite;
    [SerializeField] private Sprite repairCoreShardCostIconSprite;
    [SerializeField] private bool hideRepairCostIconsWhenFree = true;

    [Header("Repair Panel - Building Preview")]
    [SerializeField] private Image repairPreviewImage;

    [Tooltip("건물별 레벨 이미지. 각 항목의 levelSprites[0]은 미수리, [1]은 수리 완료, [2+]는 업그레이드.")]
    [SerializeField] private BuildingPreviewSpriteSet[] buildingPreviewSprites;

    [Header("Repair Panel - Building Page Indicators")]
    [Tooltip("순서: 0 격납고, 1 엔진 공방, 2 화기 연구소, 3 회수 처리장")]
    [SerializeField] private Image[] repairIndicatorImages;

    [Header("Page Indicator Style")]
    [SerializeField] private Color indicatorActiveColor = Color.white;
    [SerializeField] private Color indicatorInactiveColor = new Color(0.72f, 0.72f, 0.72f, 0.65f);
    [SerializeField] private float indicatorActiveScale = 1.25f;
    [SerializeField] private float indicatorInactiveScale = 1f;

    [Header("Trait Panel")]
    [SerializeField] private TextMeshProUGUI traitTitleText;
    [SerializeField] private TextMeshProUGUI traitActionButtonLabelText;

    [Header("Launch")]
    [SerializeField] private TextMeshProUGUI launchButtonLabelText;

    [Header("Messages")]
    [SerializeField] private TextMeshProUGUI messageText;

    private readonly TextMeshProUGUI[] resourceValueTexts = new TextMeshProUGUI[3];
    private RectTransform resourceStripRoot;
    private Image curseBaseImage;
    private Image curseAccentImage;
    private Image curseEdgeImage;
    private Image curseGhostImage;
    private Sequence cursePreviewSequence;
    private ShipDefinition previewedCurseShip;

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        InitializePreviewImages();
        BuildResourceStrip();
        BuildCursePreviewLayers();
        ConfigureTextPresentation();
    }

    private void OnEnable()
    {
        if (settlementController != null)
        {
            settlementController.Changed += Refresh;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        StopCursePreviewTween();

        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }
    }

    private void OnDestroy()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        int scrap = progress != null ? progress.ScrapParts : 0;
        int core = progress != null ? progress.CoreShards : 0;
        int stabilizedAlloy = progress != null ? progress.StabilizedAlloy : 0;

        SetCurrency(scrap, core, stabilizedAlloy);
        SetSelectedWeapon(settlementController.GetWeaponDisplayName(settlementController.SelectedWeaponTree));
        SetSelectedShip(GetSelectedShipLabel());
        SetMessage(settlementController.LastMessage);

        int shipCount = settlementController.ShipDefinitions != null
            ? settlementController.ShipDefinitions.Count
            : 0;

        SetShipPreviewState(
            settlementController.GetPreviewShipSprite(),
            settlementController.PreviewShipIndex,
            shipCount
        );
    }

    public void SetCurrency(int scrapParts, int coreShards, int stabilizedAlloy)
    {
        if (resourceStripRoot != null)
        {
            SetText(resourceValueTexts[0], $"스크랩 {scrapParts}");
            SetText(resourceValueTexts[1], $"코어 {coreShards}");
            SetText(resourceValueTexts[2], $"합금 {stabilizedAlloy}");
            return;
        }

        SetText(currencyText, $"스크랩 {scrapParts}   코어 {coreShards}   합금 {stabilizedAlloy}");
    }

    public void SetSelectedWeapon(string weaponName)
    {
        if (selectedWeaponText == null)
        {
            return;
        }

        selectedWeaponText.text = $"선택 무기: {weaponName}";
    }

    public void SetSelectedShip(string shipName)
    {
        if (selectedShipText == null)
        {
            return;
        }

        selectedShipText.text = $"선택 기체: {shipName}";
    }

    public void SetShipPreview(Sprite sprite)
    {
        SetImageSprite(shipPreviewImage, sprite);
    }

    public void SetShipPreviewState(Sprite sprite, int selectedIndex, int totalCount)
    {
        RefreshShipPreview(sprite);
        RefreshIndicators(shipIndicatorImages, selectedIndex, totalCount);
    }

    public void SetMainShipDetail(string title, string body, string actionLabel)
    {
        SetText(shipTitleText, title);
        SetText(shipBodyText, body);
        SetText(shipActionButtonLabelText, actionLabel);
    }

    public void SetRepairDetail(string title, string body, string actionLabel)
    {
        SetText(repairTitleText, title);
        SetText(repairDescriptionText, body);
        SetText(repairCurrentStageText, string.Empty);
        SetText(repairCurrentEffectText, string.Empty);
        SetText(repairNextStageText, string.Empty);
        SetText(repairNextEffectText, string.Empty);
        SetText(repairRequiredCurrencyText, string.Empty);
        SetText(repairActionButtonLabelText, actionLabel);
        SetRepairCostIconVisuals(null);
    }

    public void SetRepairDetail(SettlementRepairViewData viewData, string actionLabel)
    {
        if (viewData == null)
        {
            SetRepairDetail("정착지 보수", "시설 데이터가 없습니다.", actionLabel);
            return;
        }

        SetText(repairTitleText, viewData.Title);
        SetText(repairDescriptionText, viewData.BodyText);
        SetText(repairCurrentStageText, viewData.CurrentStageText);
        SetText(repairCurrentEffectText, $"현재  {viewData.CurrentEffectText}");
        SetText(repairNextStageText, viewData.NextStageText);
        SetText(repairNextEffectText, $"다음  {viewData.NextEffectText}");
        SetText(repairRequiredCurrencyText, viewData.RequiredCurrencyText);
        SetText(repairActionButtonLabelText, actionLabel);
        SetRepairCostIconVisuals(viewData);
    }

    public void SetRepairPreview(BuildingType buildingType, int currentLevel, int selectedIndex, int totalCount)
    {
        Sprite sprite = GetBuildingPreviewSprite(buildingType, currentLevel);
        SetImageSprite(repairPreviewImage, sprite);
        RefreshIndicators(repairIndicatorImages, selectedIndex, totalCount);
    }

    public void SetTraitDetail(string title, string body, string actionLabel)
    {
        SetText(traitTitleText, title);
        SetText(traitActionButtonLabelText, actionLabel);
    }

    public void SetLaunchLabel(string label)
    {
        SetText(launchButtonLabelText, label);
    }

    public void SetMessage(string message)
    {
        SetText(messageText, message);
    }

    // 구버전 스크립트 호환용. 인스펙터 Legacy UI는 제거해도 됨.
    public void SetPrompt(string prompt)
    {
    }

    // 구버전 스크립트 호환용. 현재 UI에서는 사용 안 함.
    public void SetDetail(string title, string body)
    {
    }

    // 구버전 스크립트 호환용. 현재 UI에서는 사용 안 함.
    public void SetActionLabel(string label)
    {
    }

    private void InitializePreviewImages()
    {
        if (shipPreviewImage != null)
        {
            shipPreviewImage.preserveAspect = true;
        }

        if (repairPreviewImage != null)
        {
            repairPreviewImage.preserveAspect = true;
        }

        InitializeRepairCostIconImage(ref repairScrapCostIconImage, repairScrapCostIconRoot, repairScrapCostIconSprite);
        InitializeRepairCostIconImage(ref repairCoreShardCostIconImage, repairCoreShardCostIconRoot, repairCoreShardCostIconSprite);
    }

    private void BuildResourceStrip()
    {
        if (currencyText == null || currencyText.transform.parent == null)
        {
            return;
        }

        RectTransform parent = currencyText.transform.parent as RectTransform;
        GameObject stripObject = new GameObject("ResourceStrip", typeof(RectTransform));
        stripObject.layer = currencyText.gameObject.layer;
        resourceStripRoot = stripObject.GetComponent<RectTransform>();
        resourceStripRoot.SetParent(parent, false);
        resourceStripRoot.anchorMin = Vector2.zero;
        resourceStripRoot.anchorMax = Vector2.one;
        resourceStripRoot.offsetMin = new Vector2(6f, 2f);
        resourceStripRoot.offsetMax = new Vector2(-6f, -2f);

        CreateResourceChip(0, "스크랩", scrapCurrencyIcon, new Color(0.92f, 0.78f, 0.42f, 1f));
        CreateResourceChip(1, "코어", coreCurrencyIcon, new Color(1f, 0.73f, 0.26f, 1f));
        CreateResourceChip(2, "합금", scrapCurrencyIcon, alloyCurrencyColor);

        currencyText.gameObject.SetActive(false);
        SetImageActive(scrapCurrencyIcon, false);
        SetImageActive(coreCurrencyIcon, false);
    }

    private void CreateResourceChip(int index, string label, Image sourceIcon, Color accentColor)
    {
        GameObject chipObject = new GameObject($"{label}Resource", typeof(RectTransform), typeof(Image));
        chipObject.layer = resourceStripRoot.gameObject.layer;
        RectTransform chipRect = chipObject.GetComponent<RectTransform>();
        chipRect.SetParent(resourceStripRoot, false);
        chipRect.anchorMin = new Vector2(index / 3f, 0f);
        chipRect.anchorMax = new Vector2((index + 1f) / 3f, 1f);
        chipRect.offsetMin = new Vector2(2f, 0f);
        chipRect.offsetMax = new Vector2(-2f, 0f);

        Image chipBackground = chipObject.GetComponent<Image>();
        chipBackground.color = new Color(0.035f, 0.075f, 0.1f, 0.88f);
        chipBackground.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.layer = chipObject.layer;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(chipRect, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        iconRect.sizeDelta = new Vector2(9f, 9f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = sourceIcon != null ? sourceIcon.sprite : null;
        iconImage.color = accentColor;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TextMeshProUGUI valueText = Instantiate(currencyText, chipRect);
        valueText.name = "Value";
        valueText.gameObject.SetActive(true);
        valueText.text = "0";
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 5.5f;
        valueText.fontSizeMax = 7f;
        valueText.fontSize = 7f;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.overflowMode = TextOverflowModes.Overflow;
        valueText.alignment = TextAlignmentOptions.MidlineLeft;
        valueText.raycastTarget = false;
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(17f, 0f);
        valueRect.offsetMax = new Vector2(-2f, 0f);
        resourceValueTexts[index] = valueText;
    }

    private void BuildCursePreviewLayers()
    {
        if (shipPreviewImage == null)
        {
            return;
        }

        curseGhostImage = CreatePreviewLayer("CurseGhost", curseGhostColor, 0);
        curseEdgeImage = CreatePreviewLayer("CurseEdge", curseEdgeColor, 1);
        curseBaseImage = CreatePreviewLayer("CurseBase", Color.white, 2);
        curseAccentImage = CreatePreviewLayer("WeaponAccent", machineGunAccentColor, 3);

        curseGhostImage.rectTransform.anchoredPosition = new Vector2(-3f, 2f);
        curseEdgeImage.rectTransform.sizeDelta = new Vector2(62f, 62f);
        curseEdgeImage.rectTransform.localScale = Vector3.one;
        SetCursePreviewActive(false);
    }

    private Image CreatePreviewLayer(string layerName, Color color, int siblingIndex)
    {
        GameObject layerObject = new GameObject(layerName, typeof(RectTransform), typeof(Image));
        layerObject.layer = shipPreviewImage.gameObject.layer;
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.SetParent(shipPreviewImage.rectTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(54f, 54f);
        rect.SetSiblingIndex(siblingIndex);

        Image image = layerObject.GetComponent<Image>();
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void RefreshShipPreview(Sprite normalSprite)
    {
        bool isCursed = PermanentProgress.Instance != null &&
                        pixelCurseTrait != null &&
                        PermanentProgress.Instance.HasPersistentStoryTrait(pixelCurseTrait);

        if (!isCursed || cursedPreviewSprite == null || curseBaseImage == null)
        {
            previewedCurseShip = null;
            SetCursePreviewActive(false);
            SetShipPreview(normalSprite);
            return;
        }

        ShipDefinition previewShip = settlementController != null
            ? settlementController.PreviewShip
            : null;
        if (previewedCurseShip != previewShip)
        {
            StopCursePreviewTween();
            previewedCurseShip = previewShip;
        }

        shipPreviewImage.sprite = null;
        shipPreviewImage.enabled = false;
        SetCurseLayerSprite(curseGhostImage);
        SetCurseLayerSprite(curseEdgeImage);
        SetCurseLayerSprite(curseBaseImage);
        SetCurseLayerSprite(curseAccentImage);

        WeaponTreeType weaponTree = previewShip != null
            ? previewShip.DefaultWeaponTree
            : WeaponTreeType.MachineGun;
        curseAccentImage.color = ResolveWeaponAccentColor(weaponTree);
        SetCursePreviewActive(true);
        StartCursePreviewTween();
    }

    private void SetCurseLayerSprite(Image target)
    {
        if (target != null)
        {
            target.sprite = cursedPreviewSprite;
        }
    }

    private void SetCursePreviewActive(bool active)
    {
        SetImageActive(curseGhostImage, active);
        SetImageActive(curseEdgeImage, active);
        SetImageActive(curseBaseImage, active);
        SetImageActive(curseAccentImage, active);

        if (!active)
        {
            StopCursePreviewTween();
        }
    }

    private void StartCursePreviewTween()
    {
        if (cursePreviewSequence != null || curseGhostImage == null || curseEdgeImage == null ||
            shipPreviewImage == null || !shipPreviewImage.gameObject.activeInHierarchy)
        {
            return;
        }

        RectTransform ghostRect = curseGhostImage.rectTransform;
        ghostRect.anchoredPosition = new Vector2(-3f, 2f);
        curseGhostImage.color = curseGhostColor;
        curseEdgeImage.color = curseEdgeColor;

        Sequence sequence = DOTween.Sequence();
        cursePreviewSequence = sequence;
        sequence
            .SetUpdate(true)
            .AppendInterval(2.1f)
            .Append(ghostRect.DOAnchorPos(new Vector2(3f, -2f), 0.08f, true).SetEase(Ease.OutQuad))
            .Join(curseGhostImage.DOFade(0.5f, 0.08f))
            .Join(curseEdgeImage.DOFade(0.78f, 0.08f))
            .Append(ghostRect.DOAnchorPos(new Vector2(-3f, 2f), 0.12f, true).SetEase(Ease.OutCubic))
            .Join(curseGhostImage.DOFade(curseGhostColor.a, 0.12f))
            .Join(curseEdgeImage.DOFade(curseEdgeColor.a, 0.12f))
            .SetLoops(-1, LoopType.Restart)
            .SetLink(shipPreviewImage.gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                if (cursePreviewSequence == sequence)
                {
                    cursePreviewSequence = null;
                }
            });
    }

    private void StopCursePreviewTween()
    {
        cursePreviewSequence?.Kill();
        cursePreviewSequence = null;

        if (curseGhostImage != null)
        {
            curseGhostImage.rectTransform.anchoredPosition = new Vector2(-3f, 2f);
            curseGhostImage.color = curseGhostColor;
        }

        if (curseEdgeImage != null)
        {
            curseEdgeImage.color = curseEdgeColor;
        }
    }

    private Color ResolveWeaponAccentColor(WeaponTreeType weaponTree)
    {
        return weaponTree switch
        {
            WeaponTreeType.Shotgun => shotgunAccentColor,
            WeaponTreeType.Sniper => sniperAccentColor,
            _ => machineGunAccentColor
        };
    }

    private static void SetImageActive(Image image, bool active)
    {
        if (image != null)
        {
            image.gameObject.SetActive(active);
        }
    }

    private void ConfigureTextPresentation()
    {
        ConfigureText(shipTitleText, 11f, 9f, 12f, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        ConfigureText(shipBodyText, 7f, 5.5f, 7.5f, TextWrappingModes.Normal, TextOverflowModes.Truncate);
        ConfigureText(shipActionButtonLabelText, 7.5f, 6f, 8f, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        ConfigureText(repairTitleText, 11f, 9f, 12f, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        ConfigureText(repairDescriptionText, 6.5f, 5.5f, 7f, TextWrappingModes.Normal, TextOverflowModes.Truncate);
        ConfigureText(repairCurrentStageText, 7.5f, 6f, 8f, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        ConfigureText(repairCurrentEffectText, 7f, 5.5f, 7.5f, TextWrappingModes.Normal, TextOverflowModes.Truncate);
        ConfigureText(repairNextStageText, 7.5f, 6f, 8f, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        ConfigureText(repairNextEffectText, 7f, 5.5f, 7.5f, TextWrappingModes.Normal, TextOverflowModes.Truncate);
        ConfigureText(repairRequiredCurrencyText, 7f, 5.5f, 7.5f, TextWrappingModes.Normal, TextOverflowModes.Truncate);
        ConfigureText(messageText, 6.5f, 5.5f, 7f, TextWrappingModes.NoWrap, TextOverflowModes.Truncate);

        if (repairRequiredCurrencyText != null)
        {
            repairRequiredCurrencyText.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        float size,
        float minimum,
        float maximum,
        TextWrappingModes wrapping,
        TextOverflowModes overflow)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.textWrappingMode = wrapping;
        text.overflowMode = overflow;
        text.raycastTarget = false;
    }

    private Sprite GetBuildingPreviewSprite(BuildingType buildingType, int currentLevel)
    {
        if (buildingPreviewSprites == null)
        {
            return null;
        }

        foreach (BuildingPreviewSpriteSet set in buildingPreviewSprites)
        {
            if (set == null)
            {
                continue;
            }

            if (set.BuildingType == buildingType)
            {
                return set.GetSprite(currentLevel);
            }
        }

        return null;
    }

    private void SetRepairCostIconVisuals(SettlementRepairViewData viewData)
    {
        bool showScrap = viewData != null && !viewData.IsMaxLevel && viewData.RequiredScrapCost > 0;
        bool showCore = viewData != null && !viewData.IsMaxLevel && viewData.RequiredCoreShardCost > 0;

        LayoutRepairCostIcons(showScrap, showCore);

        SetRepairCostIconActive(
            repairScrapCostIconRoot,
            repairScrapCostIconImage,
            repairScrapCostIconSprite,
            showScrap
        );

        SetRepairCostIconActive(
            repairCoreShardCostIconRoot,
            repairCoreShardCostIconImage,
            repairCoreShardCostIconSprite,
            showCore
        );
    }

    private void SetRepairCostIconActive(GameObject root, Image image, Sprite sprite, bool active)
    {
        if (image == null && root != null)
        {
            image = root.GetComponent<Image>();
        }

        GameObject targetObject = root != null
            ? root
            : image != null ? image.gameObject : null;

        bool shouldShow = active || !hideRepairCostIconsWhenFree;

        if (targetObject != null)
        {
            targetObject.SetActive(shouldShow);
        }

        if (image != null)
        {
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            image.enabled = shouldShow && image.sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    private void LayoutRepairCostIcons(bool showScrap, bool showCore)
    {
        bool showBoth = showScrap && showCore;
        SetCostIconVerticalPosition(repairScrapCostIconRoot, showBoth ? 5f : 0f);
        SetCostIconVerticalPosition(repairCoreShardCostIconRoot, showBoth ? -5f : 0f);
    }

    private static void SetCostIconVerticalPosition(GameObject root, float y)
    {
        if (root != null && root.transform is RectTransform rect)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }
    }

    private void InitializeRepairCostIconImage(ref Image image, GameObject root, Sprite sprite)
    {
        if (image == null && root != null)
        {
            image = root.GetComponent<Image>();
        }

        if (image == null)
        {
            return;
        }

        if (sprite != null)
        {
            image.sprite = sprite;
        }

        image.preserveAspect = true;
    }

    private void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private void RefreshIndicators(Image[] indicators, int selectedIndex, int totalCount)
    {
        if (indicators == null || indicators.Length == 0)
        {
            return;
        }

        totalCount = Mathf.Clamp(totalCount, 0, indicators.Length);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, totalCount - 1));

        for (int i = 0; i < indicators.Length; i++)
        {
            Image indicator = indicators[i];

            if (indicator == null)
            {
                continue;
            }

            bool visible = i < totalCount;
            indicator.gameObject.SetActive(visible);

            if (!visible)
            {
                continue;
            }

            bool active = i == selectedIndex;

            indicator.color = active
                ? indicatorActiveColor
                : indicatorInactiveColor;

            float targetScale = active
                ? indicatorActiveScale
                : indicatorInactiveScale;

            indicator.transform.localScale = Vector3.one * Mathf.Max(0.01f, targetScale);
        }
    }

    private string GetSelectedShipLabel()
    {
        if (settlementController == null)
        {
            return "없음";
        }

        ShipDefinition ship = settlementController.FindShipDefinition(settlementController.SelectedShipId);
        return ship != null ? ship.DisplayName : settlementController.SelectedShipId;
    }

    private void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
