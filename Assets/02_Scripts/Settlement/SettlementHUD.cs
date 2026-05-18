using System;
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
    [SerializeField] private TextMeshProUGUI[] extraCurrencyTexts; // 우측위 재화표시를 공유로 해놔서 이제 필요없긴한대..

    [Header("Main Panel - Ship")]
    [SerializeField] private Image shipPreviewImage;
    [SerializeField] private TextMeshProUGUI shipTitleText;
    [SerializeField] private TextMeshProUGUI shipBodyText;
    [SerializeField] private TextMeshProUGUI shipActionButtonLabelText;
    [SerializeField] private TextMeshProUGUI selectedShipText;
    [SerializeField] private TextMeshProUGUI selectedWeaponText;

    [Header("Main Panel - Ship Page Indicators")]
    [Tooltip("기체 선택 원. ShipDefinitions 순서와 동일하게 배치.")]
    [SerializeField] private Image[] shipIndicatorImages;

    [Header("Repair Panel")]
    [SerializeField] private TextMeshProUGUI repairTitleText;
    [SerializeField] private TextMeshProUGUI repairBodyText;
    [SerializeField] private TextMeshProUGUI repairActionButtonLabelText;

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
    [SerializeField] private TextMeshProUGUI traitBodyText;
    [SerializeField] private TextMeshProUGUI traitActionButtonLabelText;

    [Header("Launch")]
    [SerializeField] private TextMeshProUGUI launchButtonLabelText;

    [Header("Messages")]
    [SerializeField] private TextMeshProUGUI messageText;

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        InitializePreviewImages();
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

        SetCurrency(scrap, core);
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

    public void SetCurrency(int scrapParts, int coreShards)
    {
        string text = $"스크랩 부품 {scrapParts}\n코어 조각 {coreShards}";

        if (currencyText != null)
        {
            currencyText.text = text;
        }

        if (extraCurrencyTexts == null)
        {
            return;
        }

        foreach (TextMeshProUGUI target in extraCurrencyTexts)
        {
            if (target != null)
            {
                target.text = text;
            }
        }
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
        SetShipPreview(sprite);
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
        SetText(repairBodyText, body);
        SetText(repairActionButtonLabelText, actionLabel);
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
        SetText(traitBodyText, body);
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