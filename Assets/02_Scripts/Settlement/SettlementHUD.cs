using System;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BuildingPreviewSpriteSet
{
    [SerializeField] private BuildingType buildingType;

    [Tooltip("0 = 기능 정지, 1 이상 = 복구 완료. 기존 저장의 상위 레벨도 완료 상태로 표시합니다.")]
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
    [Serializable]
    public sealed class ResourceCell
    {
        public RectTransform root;
        public Image background;
        public Image icon;
        public TextMeshProUGUI value;
    }

    [Header("References")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementUIController navigationPresentationOwner;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Image scrapCurrencyIcon;
    [SerializeField] private Image coreCurrencyIcon;
    [SerializeField] private Color alloyCurrencyColor = new Color(0.72f, 0.92f, 1f, 1f);

    [Header("Authored Persistent Resource Strip")]
    [SerializeField] private RectTransform resourceContainer;
    [SerializeField] private RectTransform resourceStripRoot;
    [SerializeField] private ResourceCell scrapResource = new ResourceCell();
    [SerializeField] private ResourceCell coreResource = new ResourceCell();
    [SerializeField] private ResourceCell stabilizedAlloyResource = new ResourceCell();

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

    [Header("Settlement Restoration Panel")]
    [SerializeField] private RectTransform repairDetailRoot;
    [SerializeField] private RectTransform repairResultsRoot;
    [SerializeField] private TextMeshProUGUI repairTitleText;
    [SerializeField] private TextMeshProUGUI repairActionButtonLabelText;

    [Header("Settlement Restoration Panel - Detail Text")]
    [SerializeField] private TextMeshProUGUI repairDescriptionText;
    [SerializeField] private TextMeshProUGUI repairCurrentStageText;
    [SerializeField] private TextMeshProUGUI repairCurrentEffectText;
    [SerializeField] private TextMeshProUGUI repairNextStageText;
    [SerializeField] private TextMeshProUGUI repairNextEffectText;
    [SerializeField] private TextMeshProUGUI repairRequiredCurrencyText;

    [Header("Legacy Repair Cost Icons (Hidden)")]
    [Tooltip("레거시 비용 아이콘입니다. 정착지 복구에서는 표시하지 않습니다.")]
    [SerializeField] private GameObject repairScrapCostIconRoot;
    [Tooltip("레거시 비용 아이콘입니다. 정착지 복구에서는 표시하지 않습니다.")]
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

    [Header("Route Core Component Summary")]
    [SerializeField] private RectTransform routeCoreComponentsRoot;
    [SerializeField] private TextMeshProUGUI routeCoreComponentsHeader;
    [SerializeField] private TextMeshProUGUI routeCoreComponentsText;

    [Tooltip("시설별 복구 이미지. levelSprites[0]은 기능 정지, [1+]는 복구 완료입니다.")]
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
    [SerializeField] private TextMeshProUGUI campaignDeckMessageText;

    private bool missingResourceBindingReported;
    private SettlementController subscribedController;
    private readonly Dictionary<Image, Vector3> repairIndicatorScales = new Dictionary<Image, Vector3>();
    private readonly Dictionary<Image, Color> repairIndicatorColors = new Dictionary<Image, Color>();
    [Header("Authored Hangar Curse Layers")]
    [SerializeField] private Image curseBaseImage;
    [SerializeField] private Image curseAccentImage;
    [SerializeField] private Image curseEdgeImage;
    [SerializeField] private Image curseGhostImage;
    private bool previewBaselinesCaptured;
    private bool hangarDiagnosticReported;
    private Vector2 ghostPositionBaseline;
    private Color ghostColorBaseline, edgeColorBaseline, accentBaseline;
    private readonly Dictionary<Image, Color> shipIndicatorColors = new Dictionary<Image, Color>();
    private readonly Dictionary<Image, Vector3> shipIndicatorScales = new Dictionary<Image, Vector3>();
    private Sequence cursePreviewSequence;
    private ShipDefinition previewedCurseShip;

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        HideRepairCostIcons();
    }

    private void OnEnable()
    {
        SubscribeController();
        RefreshResourceBalances();
    }

    private void Start()
    {
        // Retry once after scene Awake/OnEnable ordering, without polling or a new service.
        SubscribeController();
        Refresh();
    }

    private void OnDisable()
    {
        StopCursePreviewTween();

        UnsubscribeController();
    }

    private void OnDestroy()
    {
        UnsubscribeController();
    }

    private void SubscribeController()
    {
        if (subscribedController == settlementController)
        {
            return;
        }
        UnsubscribeController();
        subscribedController = settlementController;
        if (subscribedController != null)
        {
            subscribedController.Changed += Refresh;
        }
    }

    private void UnsubscribeController()
    {
        if (subscribedController != null)
        {
            subscribedController.Changed -= Refresh;
        }
        subscribedController = null;
    }

    private void RefreshResourceBalances()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        SetCurrency(progress != null ? progress.ScrapParts : 0,
            progress != null ? progress.CoreShards : 0,
            progress != null ? progress.StabilizedAlloy : 0);
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        RefreshResourceBalances();
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
        bool scrapValid = HasValidResourceCell(CurrencyType.ScrapParts);
        bool coreValid = HasValidResourceCell(CurrencyType.CoreShards);
        bool alloyValid = HasValidResourceCell(CurrencyType.StabilizedAlloy);
        if (scrapValid) SetText(scrapResource?.value, $"스크랩 {scrapParts}");
        if (coreValid) SetText(coreResource?.value, $"코어 {coreShards}");
        if (alloyValid) SetText(stabilizedAlloyResource?.value, $"합금 {stabilizedAlloy}");
        if ((!scrapValid || !coreValid || !alloyValid) && !missingResourceBindingReported)
        {
            missingResourceBindingReported = true;
            Debug.LogWarning("[SettlementHUD] Missing, duplicate, or invalid persistent-resource bindings: " +
                (scrapValid ? "" : DescribeResourceBinding(CurrencyType.ScrapParts)) +
                (coreValid ? "" : DescribeResourceBinding(CurrencyType.CoreShards)) +
                (alloyValid ? "" : DescribeResourceBinding(CurrencyType.StabilizedAlloy)) +
                ". Restore the listed authored Inspector bindings. " +
                "Only the affected resource cells were skipped; no replacement strip was created.", this);
        }
    }

    public ResourceCell GetResourceCell(CurrencyType resource)
    {
        return resource switch
        {
            CurrencyType.ScrapParts => scrapResource,
            CurrencyType.CoreShards => coreResource,
            CurrencyType.StabilizedAlloy => stabilizedAlloyResource,
            _ => null
        };
    }

    private string DescribeResourceBinding(CurrencyType resource)
    {
        ResourceCell cell = GetResourceCell(resource);
        string field = resource == CurrencyType.ScrapParts ? nameof(scrapResource) :
            resource == CurrencyType.CoreShards ? nameof(coreResource) : nameof(stabilizedAlloyResource);
        Canvas canvas = GetComponentInParent<Canvas>(true);
        string Issue(string property, Component actual, Transform expected, string reason) =>
            SettlementSectorTechnologyPanelUI.BindingDiagnostic(nameof(SettlementHUD) + "." + property +
                " on " + SettlementSectorTechnologyPanelUI.BindingLocation(transform), actual, expected, gameObject.scene, reason) + " ";
        if (resourceContainer == null || canvas == null || resourceContainer.parent != canvas.transform || resourceContainer.gameObject.scene != gameObject.scene)
            return Issue(nameof(resourceContainer), resourceContainer, canvas != null ? canvas.transform : transform, "Missing or invalid resource container");
        if (resourceStripRoot == null || resourceStripRoot.parent != resourceContainer)
            return Issue(nameof(resourceStripRoot), resourceStripRoot, resourceContainer, "Missing or invalid strip root");
        if (cell == null || cell.root == null || cell.root.parent != resourceStripRoot)
            return Issue(field + ".root", cell?.root, resourceStripRoot, "Missing or invalid cell root");
        if (cell.background == null || cell.background.transform != cell.root)
            return Issue(field + ".background", cell.background, cell.root, "Missing or invalid cell background");
        if (cell.icon == null || cell.icon.transform.parent != cell.root || cell.icon.sprite == null)
            return Issue(field + ".icon", cell.icon, cell.root, "Missing or invalid icon binding/sprite");
        if (cell.value == null || cell.value.transform.parent != cell.root || cell.value.font == null)
            return Issue(field + ".value", cell.value, cell.root, "Missing or invalid amount binding/font");
        return Issue(field + ".root", cell.root, resourceStripRoot, "Duplicate resource cell mapping");
    }

    public bool HasValidResourceCell(CurrencyType resource)
    {
        ResourceCell cell = GetResourceCell(resource);
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null || resourceContainer == null || resourceStripRoot == null ||
            resourceContainer.gameObject.scene != gameObject.scene || resourceContainer.parent != canvas.transform ||
            resourceStripRoot.parent != resourceContainer || cell == null || cell.root == null ||
            cell.root.parent != resourceStripRoot || cell.background == null || cell.background.transform != cell.root ||
            cell.icon == null || cell.icon.transform.parent != cell.root || cell.icon.sprite == null ||
            cell.value == null || cell.value.transform.parent != cell.root || cell.value.font == null)
        {
            return false;
        }
        return (resource == CurrencyType.ScrapParts || scrapResource?.root != cell.root) &&
               (resource == CurrencyType.CoreShards || coreResource?.root != cell.root) &&
               (resource == CurrencyType.StabilizedAlloy || stabilizedAlloyResource?.root != cell.root);
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

    public void SetShipResearchAccent(Color color)
    {
        if (shipTitleText != null) shipTitleText.color = color;
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
        HideRepairCostIcons();
    }

    public void SetRestorationDetail(SettlementRestorationViewData viewData)
    {
        SetRouteCorePresentation(false);
        if (viewData == null)
        {
            SetRepairDetail("정착지 복구", "복구 프로젝트 데이터가 없습니다.", "조건 미충족");
            return;
        }

        SetText(repairTitleText, viewData.Title);
        SetText(repairDescriptionText, viewData.Description);
        SetText(repairCurrentStageText, "현재 상태");
        SetText(repairCurrentEffectText, viewData.StateText);
        SetText(repairNextStageText, "필요 조건");
        SetText(repairNextEffectText, viewData.RequirementText);
        SetText(repairRequiredCurrencyText, $"복구 결과\n{viewData.CompletionResultText}");
        SetText(repairActionButtonLabelText, viewData.ActionLabel);
        HideRepairCostIcons();
    }

    public void SetRouteCoreDetail(string title, string status, string description,
        string componentsHeader, string components)
    {
        SetRouteCorePresentation(true);
        SetRepairDetail(title, description, string.Empty);
        SetText(repairCurrentEffectText, status);
        SetText(routeCoreComponentsHeader, componentsHeader);
        SetText(routeCoreComponentsText, components);
    }

    private void SetRouteCorePresentation(bool active)
    {
        if (repairPreviewImage != null) repairPreviewImage.gameObject.SetActive(!active);
        if (routeCoreComponentsRoot != null) routeCoreComponentsRoot.gameObject.SetActive(active);
    }

    public void SetRepairPreview(BuildingType buildingType, int currentLevel, int selectedIndex, int totalCount)
    {
        Sprite sprite = GetBuildingPreviewSprite(buildingType, currentLevel);
        if (repairPreviewImage != null)
        {
            repairPreviewImage.sprite = sprite;
            repairPreviewImage.enabled = sprite != null;
        }
        if (repairIndicatorImages == null) return;
        for (int i = 0; i < repairIndicatorImages.Length; i++)
        {
            Image indicator = repairIndicatorImages[i];
            if (indicator == null) continue;
            if (!repairIndicatorScales.ContainsKey(indicator))
            {
                repairIndicatorScales.Add(indicator, indicator.transform.localScale);
                repairIndicatorColors.Add(indicator, indicator.color);
            }
            indicator.gameObject.SetActive(i < totalCount);
            indicator.transform.localScale = repairIndicatorScales[indicator] *
                Mathf.Max(.01f, i == selectedIndex ? indicatorActiveScale : indicatorInactiveScale);
            indicator.color = repairIndicatorColors[indicator] *
                (i == selectedIndex ? indicatorActiveColor : indicatorInactiveColor);
        }
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
        SetText(campaignDeckMessageText, message);
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


    public void ValidateHangarPresentation(List<string> errors)
    {
        Transform preview = shipPreviewImage != null ? shipPreviewImage.transform : transform;
        HangarRole(errors, "shipPreviewImage", shipPreviewImage, transform);
        HangarRole(errors, "shipTitleText", shipTitleText, transform);
        HangarRole(errors, "shipBodyText", shipBodyText, transform);
        HangarRole(errors, "shipActionButtonLabelText", shipActionButtonLabelText, transform);
        HangarRole(errors, "messageText", messageText, transform);
        HangarRole(errors, "launchButtonLabelText", launchButtonLabelText, transform);
        if (pixelCurseTrait != null && cursedPreviewSprite == null)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD.cursedPreviewSprite", null, preview, gameObject.scene, "Missing curse preview content sprite"));
        var roles = new HashSet<Image>();
        Image[] layers = { curseGhostImage, curseEdgeImage, curseBaseImage, curseAccentImage };
        string[] fields = { "curseGhostImage", "curseEdgeImage", "curseBaseImage", "curseAccentImage" };
        for (int i = 0; i < layers.Length; i++)
        {
            HangarRole(errors, fields[i], layers[i], preview);
            if (layers[i] != null && !roles.Add(layers[i]))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD." + fields[i], layers[i], preview, gameObject.scene, "Duplicate preview layer"));
        }
    }

    private void HangarRole(List<string> errors, string field, Component value, Transform parent)
    {
        if (value == null || value.gameObject.scene != gameObject.scene || value.transform == parent || !value.transform.IsChildOf(parent))
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD." + field, value, parent, gameObject.scene,
                value == null ? "Missing required binding" : value.gameObject.scene != gameObject.scene ? "Scene ownership" : "Ancestry"));
        else if (value is TMP_Text text && text.font == null)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD." + field, value, parent, gameObject.scene, "Missing font"));
    }

    private bool HangarPresentationReady()
    {
        var errors = new List<string>();
        ValidateHangarPresentation(errors);
        if (errors.Count == 0) return true;
        if (!hangarDiagnosticReported)
        {
            hangarDiagnosticReported = true;
            Debug.LogWarning(string.Join("\n", errors) +
                "\nRestore the listed authored Inspector bindings. Only Hangar preview is skipped.", this);
        }
        return false;
    }

    private void CapturePreviewBaselines()
    {
        if (previewBaselinesCaptured) return;
        ghostPositionBaseline = curseGhostImage.rectTransform.anchoredPosition;
        ghostColorBaseline = curseGhostImage.color;
        edgeColorBaseline = curseEdgeImage.color;
        accentBaseline = curseAccentImage.color;
        previewBaselinesCaptured = true;
    }

    private void RefreshShipPreview(Sprite normalSprite)
    {
        if (!HangarPresentationReady()) return;
        CapturePreviewBaselines();
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
        Color weaponColor = ResolveWeaponAccentColor(weaponTree);
        curseAccentImage.color = new Color(
            accentBaseline.r * weaponColor.r / Mathf.Max(.001f, machineGunAccentColor.r),
            accentBaseline.g * weaponColor.g / Mathf.Max(.001f, machineGunAccentColor.g),
            accentBaseline.b * weaponColor.b / Mathf.Max(.001f, machineGunAccentColor.b),
            accentBaseline.a);
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
        ghostRect.anchoredPosition = ghostPositionBaseline;
        curseGhostImage.color = ghostColorBaseline;
        curseEdgeImage.color = edgeColorBaseline;

        Sequence sequence = DOTween.Sequence();
        cursePreviewSequence = sequence;
        sequence
            .SetUpdate(true)
            .AppendInterval(2.1f)
            .Append(ghostRect.DOAnchorPos(ghostPositionBaseline + new Vector2(6f, -4f), 0.08f, true).SetEase(Ease.OutQuad))
            .Join(curseGhostImage.DOFade(Mathf.Clamp01(ghostColorBaseline.a * (0.5f / 0.28f)), 0.08f))
            .Join(curseEdgeImage.DOFade(Mathf.Clamp01(edgeColorBaseline.a * (0.78f / 0.52f)), 0.08f))
            .Append(ghostRect.DOAnchorPos(ghostPositionBaseline, 0.12f, true).SetEase(Ease.OutCubic))
            .Join(curseGhostImage.DOFade(ghostColorBaseline.a, 0.12f))
            .Join(curseEdgeImage.DOFade(edgeColorBaseline.a, 0.12f))
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

        if (previewBaselinesCaptured && curseGhostImage != null)
        {
            curseGhostImage.rectTransform.anchoredPosition = ghostPositionBaseline;
            curseGhostImage.color = ghostColorBaseline;
        }

        if (previewBaselinesCaptured && curseEdgeImage != null)
        {
            curseEdgeImage.color = edgeColorBaseline;
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

    private void HideRepairCostIcons()
    {
        // Legacy references stay serialized. Hide only the graphic, never its container or layout.
        Image scrap = repairScrapCostIconImage != null ? repairScrapCostIconImage :
            repairScrapCostIconRoot != null ? repairScrapCostIconRoot.GetComponent<Image>() : null;
        Image core = repairCoreShardCostIconImage != null ? repairCoreShardCostIconImage :
            repairCoreShardCostIconRoot != null ? repairCoreShardCostIconRoot.GetComponent<Image>() : null;
        if (scrap != null && scrap.gameObject.scene == gameObject.scene && scrap.transform.IsChildOf(transform)) scrap.enabled = false;
        if (core != null && core.gameObject.scene == gameObject.scene && core.transform.IsChildOf(transform)) core.enabled = false;
    }

    public void CollectRestorationBindingErrors(GameObject panel, Button action, Button previous, Button next, List<string> errors)
    {
        Transform root = panel != null ? panel.transform : null;
        var roles = new HashSet<Component>();
        CheckRestorationRole("repairPanel", root, transform, errors, roles);
        CheckRestorationRole(nameof(repairDetailRoot), repairDetailRoot, root, errors, roles);
        CheckRestorationRole(nameof(repairResultsRoot), repairResultsRoot, root, errors, roles);
        CheckRestorationRole(nameof(repairPreviewImage), repairPreviewImage, root, errors, roles);
        CheckRestorationRole("repairActionButton", action, root, errors, roles);
        CheckRestorationRole("repairPreviousButton", previous, repairPreviewImage != null ? repairPreviewImage.transform : root, errors, roles);
        CheckRestorationRole("repairNextButton", next, repairPreviewImage != null ? repairPreviewImage.transform : root, errors, roles);
        CheckRestorationRole(nameof(repairActionButtonLabelText), repairActionButtonLabelText, action != null ? action.transform : root, errors, roles);
        TextMeshProUGUI[] texts = { repairTitleText, repairDescriptionText, repairCurrentStageText, repairCurrentEffectText, repairNextStageText, repairNextEffectText };
        string[] fields = { nameof(repairTitleText), nameof(repairDescriptionText), nameof(repairCurrentStageText), nameof(repairCurrentEffectText), nameof(repairNextStageText), nameof(repairNextEffectText) };
        for (int i = 0; i < texts.Length; i++) CheckRestorationRole(fields[i], texts[i], repairDetailRoot, errors, roles);
        CheckRestorationRole(nameof(repairRequiredCurrencyText), repairRequiredCurrencyText, repairResultsRoot, errors, roles);
        if (repairScrapCostIconRoot != null) CheckRestorationRole(nameof(repairScrapCostIconRoot), repairScrapCostIconRoot.transform, repairResultsRoot, errors, roles);
        if (repairCoreShardCostIconRoot != null) CheckRestorationRole(nameof(repairCoreShardCostIconRoot), repairCoreShardCostIconRoot.transform, repairResultsRoot, errors, roles);
        if (repairScrapCostIconImage != null) CheckRestorationRole(nameof(repairScrapCostIconImage), repairScrapCostIconImage, repairResultsRoot, errors, roles);
        if (repairCoreShardCostIconImage != null) CheckRestorationRole(nameof(repairCoreShardCostIconImage), repairCoreShardCostIconImage, repairResultsRoot, errors, roles);
        if (repairIndicatorImages == null || repairIndicatorImages.Length != 4)
            errors.Add("SettlementHUD.repairIndicatorImages requires exactly four explicit facility mappings.");
        if (repairIndicatorImages != null)
            for (int i = 0; i < repairIndicatorImages.Length; i++)
                CheckRestorationRole($"repairIndicatorImages.Array.data[{i}]", repairIndicatorImages[i],
                    repairPreviewImage != null ? repairPreviewImage.transform : root, errors, roles);
        var buildings = new HashSet<BuildingType>();
        if (buildingPreviewSprites != null)
            for (int i = 0; i < buildingPreviewSprites.Length; i++)
            {
                BuildingPreviewSpriteSet set = buildingPreviewSprites[i];
                if (set == null || !buildings.Add(set.BuildingType) || set.GetSprite(0) == null || set.GetSprite(1) == null)
                    errors.Add($"SettlementHUD.buildingPreviewSprites.Array.data[{i}]: missing sprite or duplicate facility mapping.");
            }
        foreach (BuildingType type in new[] { BuildingType.Hangar, BuildingType.EngineWorkshop, BuildingType.WeaponLab, BuildingType.RecoveryProcessor })
            if (!buildings.Contains(type)) errors.Add($"SettlementHUD.buildingPreviewSprites: missing {type} mapping.");
    }

    private void CheckRestorationRole(string field, Component value, Transform parent, List<string> errors, HashSet<Component> roles)
    {
        string property = (field == "repairPanel" || field == "repairActionButton" || field == "repairPreviousButton" || field == "repairNextButton"
            ? "SettlementUIController." : "SettlementHUD.") + field;
        bool reusedObject = false;
        if (value != null)
            foreach (Component role in roles)
                if (role.transform == value.transform &&
                    !((field == nameof(repairScrapCostIconImage) || field == nameof(repairCoreShardCostIconImage)) && role is Transform))
                    reusedObject = true;
        string reason = value == null ? "Missing binding" :
            value.gameObject.scene != gameObject.scene ? "Scene ownership mismatch (cross-scene reference)" :
            parent == null || value.transform == parent || !value.transform.IsChildOf(parent) ? "Incorrect ancestry" :
            reusedObject || !roles.Add(value) ? "Duplicate presentation role" : null;
        if (reason != null)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, parent, gameObject.scene, reason));
        if (value is TMP_Text text && text.font == null)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD." + field, value, parent, gameObject.scene, "Missing TMP font"));
        if (field.StartsWith("repairIndicatorImages", StringComparison.Ordinal) && value is Image image && image.sprite == null)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettlementHUD." + field, value, parent, gameObject.scene, "Missing indicator sprite"));
    }

    private void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
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

            if (!shipIndicatorColors.ContainsKey(indicator))
            {
                shipIndicatorColors.Add(indicator, indicator.color);
                shipIndicatorScales.Add(indicator, indicator.transform.localScale);
            }
            bool active = i == selectedIndex;
            indicator.color = shipIndicatorColors[indicator] * (active ? indicatorActiveColor : indicatorInactiveColor);

            float targetScale = active
                ? indicatorActiveScale
                : indicatorInactiveScale;

            indicator.transform.localScale = shipIndicatorScales[indicator] * Mathf.Max(0.01f, targetScale);
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
