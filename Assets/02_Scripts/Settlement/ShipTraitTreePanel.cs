using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


public enum ShipTraitUnlockConditionKind
{
    UnlockFlag,
    ShipUnlocked,
    UnlockedShipCount,
    BuildingLevel,
    TraitLevel,
    TotalRunCount,
    SafeReturnCount,
    EmergencyReturnCount,
    BossDefeatCount,
    TotalCollectedScrapParts,
    TotalCollectedCoreShards,
    TotalCommittedScrapParts,
    TotalCommittedCoreShards,
    OwnedScrapParts,
    OwnedCoreShards
}

[Serializable]
public class ShipTraitUnlockCondition
{
    [Tooltip("조건 종류. 조건을 만족해야 이 특성을 해금/강화할 수 있습니다.")]
    public ShipTraitUnlockConditionKind conditionKind = ShipTraitUnlockConditionKind.UnlockFlag;

    [Tooltip("UnlockFlag, ShipUnlocked, TraitLevel 조건에서 사용하는 ID입니다. 예: boss_01_clear, basic_ship, shared_01")]
    public string targetId;

    [Tooltip("BuildingLevel 조건에서 사용하는 건물 종류입니다.")]
    public BuildingType buildingType;

    [Tooltip("필요 수치입니다. Flag/ShipUnlocked 조건은 1 이상이면 완료로 처리합니다.")]
    [Min(0)]
    public int requiredValue = 1;

    [Tooltip("비워두면 조건 종류 기준으로 자동 표시합니다. 플레이어에게 보여줄 문구를 직접 쓰고 싶을 때 사용합니다.")]
    public string displayNameOverride;
}

[Serializable]
public class ShipTraitBranchGate
{
    [Tooltip("잠금 기준을 적용할 브랜치입니다.")]
    public ShipTraitBranchKind branchKind = ShipTraitBranchKind.Shared;

    [Tooltip("켜두면 조건과 무관하게 기본 개방됩니다. 공유/기관총 브랜치에 사용합니다.")]
    public bool unlockedByDefault = true;

    [Tooltip("필요한 총 해금 기체 수입니다. 0이면 사용하지 않습니다.")]
    [Min(0)]
    public int requiredUnlockedShipCount;

    [Tooltip("필요한 특정 기체 ID입니다. 비워두면 사용하지 않습니다.")]
    public string requiredShipId;

    [Tooltip("필요한 UnlockFlag입니다. 비워두면 사용하지 않습니다.")]
    public string requiredUnlockFlag;

    [Header("Locked Visual Optional")]
    [Tooltip("브랜치가 잠겼을 때 켤 오버레이/자물쇠 오브젝트입니다.")]
    public GameObject lockedOverlayObject;

    [Tooltip("브랜치 패널 전체 알파를 조절하고 싶을 때 연결합니다.")]
    public CanvasGroup panelCanvasGroup;

    [Range(0f, 1f)]
    public float lockedAlpha = 0.35f;

    [Tooltip("잠긴 브랜치 패널의 클릭/스크롤 입력을 막습니다.")]
    public bool disableInteractionWhenLocked = true;
}

[Serializable]
public class ShipTraitBranchNodeEntry
{
    public ShipTraitBranchKind branchKind;

    [Header("Node Identity")]
    [Tooltip("비워두면 TraitDefinition.TraitId 또는 ReinforcementDefinition.EquipmentId를 사용합니다.")]
    public string nodeId;

    [Tooltip("Auto Generate Node Buttons를 쓰면 비워둡니다. 런타임에 자동 생성된 버튼이 들어갑니다.")]
    public ShipTraitNodeButton nodeButton;

    [Header("Content Source Optional")]
    [Tooltip("해금 시 영구 특성으로 적용할 TraitDefinition입니다. 탐사 시작 시 PlayerRuntimeStatApplier가 이 ID를 기준으로 기본 적용합니다.")]
    public TraitDefinition traitDefinition;

    [Tooltip("해금 시 탐사 시작 장비 후보로 사용할 ReinforcementDefinition입니다. 장비는 레벨 없이 1회 해금으로 처리합니다.")]
    public ReinforcementDefinition reinforcementDefinition;

    [Tooltip("꺼두면 위 Definition에서 ID/이름/아이콘/설명/최대레벨/브랜치를 자동으로 가져옵니다.")]
    public bool manualOverrideDefinitionFields;

    [Tooltip("ReinforcementDefinition.Cost를 스크랩 비용으로 자동 사용할지 여부입니다. 노드 Scrap/Core 비용이 0일 때만 적용합니다.")]
    public bool useReinforcementCostAsScrapCost;

    [Header("Display")]
    public string displayName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Cost / Level")]
    [Min(0)]
    public int scrapCost;

    [Min(0)]
    public int coreShardCost;

    [Min(1)]
    public int maxLevel = 1;

    [Header("Branch Activation By Ship Unlock")]
    [Min(0)]
    public int requiredUnlockedShipCount = 1;

    public string requiredShipId;
    public string requiredUnlockFlag;

    [Header("Additional Unlock Conditions")]
    public List<ShipTraitUnlockCondition> unlockConditions = new List<ShipTraitUnlockCondition>();

    [Header("Tree Prerequisites")]
    public List<string> prerequisiteNodeIds = new List<string>();

    [Header("Option")]
    public bool hideWhenLocked;
}

public class ShipTraitDetailViewData
{
    public string DescriptionText { get; }
    public string BranchText { get; }
    public string LevelText { get; }
    public string StatusText { get; }
    public string CostText { get; }

    public ShipTraitDetailViewData(
        string descriptionText,
        string branchText,
        string levelText,
        string statusText,
        string costText)
    {
        DescriptionText = string.IsNullOrWhiteSpace(descriptionText) ? "특성 설명이 없습니다." : descriptionText;
        BranchText = branchText ?? string.Empty;
        LevelText = levelText ?? string.Empty;
        StatusText = statusText ?? string.Empty;
        CostText = costText ?? string.Empty;
    }

    public string BuildFallbackBodyText()
    {
        StringBuilder builder = new StringBuilder();

        AppendSection(builder, DescriptionText);
        AppendSection(builder, BranchText);
        AppendSection(builder, LevelText);
        AppendSection(builder, StatusText);
        AppendSection(builder, CostText);

        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
        }

        builder.AppendLine(value.TrimEnd());
    }
}

public partial class ShipTraitTreePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;

    [Header("Branch Panel Objects")]
    [SerializeField] private GameObject sharedPanelObject;
    [SerializeField] private GameObject machineGunPanelObject;
    [SerializeField] private GameObject sniperPanelObject;
    [SerializeField] private GameObject shotgunPanelObject;
    [SerializeField] private bool deactivateBranchPanelWhenLocked = true;

    [Header("Branch Tab Mode")]
    [Tooltip("켜두면 브랜치 패널을 모두 펼쳐두지 않고, 탭으로 하나씩 전환합니다.")]
    [SerializeField] private bool useBranchTabMode = true;
    [SerializeField] private ShipTraitBranchKind defaultSelectedBranch = ShipTraitBranchKind.Shared;
    [Tooltip("잠긴 탭을 화면에서 숨깁니다. 꺼두면 잠금 표시만 하고 클릭 시 잠금 사유를 표시합니다.")]
    [SerializeField] private bool hideLockedBranchTabs;
    [Tooltip("잠긴 탭 클릭을 허용합니다. 클릭하면 패널 전환 대신 잠금 사유를 메시지로 표시합니다.")]
    [SerializeField] private bool allowLockedBranchTabClick = true;
    [SerializeField] private ShipTraitBranchTabButton sharedTabButton;
    [SerializeField] private ShipTraitBranchTabButton machineGunTabButton;
    [SerializeField] private ShipTraitBranchTabButton sniperTabButton;
    [SerializeField] private ShipTraitBranchTabButton shotgunTabButton;

    [Header("Branch Unlock Gates")]
    [Tooltip("공유 브랜치. 기본 개방으로 둡니다.")]
    [SerializeField] private ShipTraitBranchGate sharedBranchGate = new ShipTraitBranchGate
    {
        branchKind = ShipTraitBranchKind.Shared,
        unlockedByDefault = true,
        requiredUnlockedShipCount = 0
    };

    [Tooltip("기관총 브랜치. 기본 기체/기본 무기로 시작하므로 기본 개방으로 둡니다.")]
    [SerializeField] private ShipTraitBranchGate machineGunBranchGate = new ShipTraitBranchGate
    {
        branchKind = ShipTraitBranchKind.MachineGun,
        unlockedByDefault = true,
        requiredUnlockedShipCount = 0
    };

    [Tooltip("스나이퍼 브랜치. 스나이퍼 기체 해금 조건을 넣습니다.")]
    [SerializeField] private ShipTraitBranchGate sniperBranchGate = new ShipTraitBranchGate
    {
        branchKind = ShipTraitBranchKind.Sniper,
        unlockedByDefault = false,
        requiredUnlockedShipCount = 2
    };

    [Tooltip("샷건 브랜치. 샷건 기체 해금 조건을 넣습니다.")]
    [SerializeField] private ShipTraitBranchGate shotgunBranchGate = new ShipTraitBranchGate
    {
        branchKind = ShipTraitBranchKind.Shotgun,
        unlockedByDefault = false,
        requiredUnlockedShipCount = 3
    };

    [Header("Branch Nodes")]
    [SerializeField] private List<ShipTraitBranchNodeEntry> branchNodes = new List<ShipTraitBranchNodeEntry>();
    [SerializeField] private bool allowLockedNodeSelection = true;

    [Header("Generated Nodes In ScrollView")]
    // Preserve the old prefab reference for serialized compatibility, never construct from it.
    [SerializeField] private ShipTraitNodeButton nodeButtonPrefab;
    [Tooltip("Legacy serialized root; authored branch content references are required at runtime.")]
    [SerializeField] private Transform defaultNodeContentRoot;
    [SerializeField] private Transform sharedNodeContentRoot;
    [SerializeField] private Transform machineGunNodeContentRoot;
    [SerializeField] private Transform sniperNodeContentRoot;
    [SerializeField] private Transform shotgunNodeContentRoot;

    [Header("Ship Catalog")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();

    [Header("Target Ship Optional")]
    [SerializeField] private string targetShipIdOverride;

    [Header("Detail UI")]
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Detail UI - Separated Text")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI branchText;
    [Tooltip("선택한 특성의 현재 레벨을 따로 표시할 텍스트입니다. 예: Lv 1 / 3")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Detail UI - Cost Icons")]
    [Tooltip("스크랩 비용이 1 이상일 때 활성화할 아이콘 루트입니다.")]
    [SerializeField] private GameObject scrapCostIconRoot;
    [Tooltip("코어 조각 비용이 1 이상일 때 활성화할 아이콘 루트입니다.")]
    [SerializeField] private GameObject coreShardCostIconRoot;
    [Tooltip("스크랩 아이콘 Image입니다. 비워두면 Root의 Image를 자동 사용합니다.")]
    [SerializeField] private Image scrapCostIconImage;
    [Tooltip("코어 조각 아이콘 Image입니다. 비워두면 Root의 Image를 자동 사용합니다.")]
    [SerializeField] private Image coreShardCostIconImage;
    [SerializeField] private Sprite scrapCostIconSprite;
    [SerializeField] private Sprite coreShardCostIconSprite;
    [SerializeField] private bool hideCostIconsWhenFree = true;

    [Header("Detail UI - Lock Mark")]
    [SerializeField] private Image detailLockImage;
    [SerializeField] private Sprite lockedDetailSprite;
    [SerializeField] private Sprite unlockedDetailSprite;
    [SerializeField] private bool hideLockImageWhenAvailable = true;

    [Header("Unlock Button")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private TextMeshProUGUI unlockButtonLabelText;
    [SerializeField] private SettlementUIController navigationPresentationOwner;

    [Header("Activation Toggle Button")]
    [Tooltip("해금된 특성을 탐사 시작 기본 적용에서 제외/재적용하는 버튼입니다.")]
    [SerializeField] private Button traitActivationToggleButton;
    [SerializeField] private TextMeshProUGUI traitActivationToggleButtonLabelText;
    [SerializeField] private bool hideActivationToggleButtonUntilUnlocked;

    private readonly List<ShipTraitNodeButton> generatedNodeButtons = new List<ShipTraitNodeButton>();

    private ShipTraitBranchKind selectedBranch = ShipTraitBranchKind.Shared;
    private string selectedNodeId;
    private bool hasSelection;

    public Button UnlockButton => unlockButton;
    public ShipTraitBranchKind SelectedBranch => selectedBranch;
    public string SelectedNodeId => selectedNodeId;
    public bool HasSelection => hasSelection;

    [Header("Authored Additional Traits Presentation")]
    [SerializeField] private RectTransform authoredDetailRoot;
    [SerializeField] private RectTransform authoredCostRoot;
    [SerializeField] private RectTransform authoredCatalogRoot;
    [SerializeField] private Image authoredDetailBackground;
    [SerializeField] private Image authoredCatalogBackground;
    [SerializeField] private ShipTraitNodeButton authoredNodeTemplate;
    [SerializeField] private Button sidebarButton;
    private SettlementController subscribedController;
    private PermanentProgress subscribedProgress;
    private Button subscribedUnlockButton, subscribedActivationButton;
    private bool authoredDiagnosticReported;
    private readonly List<Selectable> visibleTraitControls = new List<Selectable>();
    private readonly Dictionary<CanvasGroup, float> branchAlphaBaselines = new Dictionary<CanvasGroup, float>();

    public bool UsesAuthoredPresentation => true;
    public bool CanUseTraitInput => !UsesAuthoredPresentation || (isActiveAndEnabled &&
        navigationPresentationOwner != null && navigationPresentationOwner.CanUseSettlementNavigation());

    private void SubscribePresentation()
    {
        UnsubscribePresentation();
        subscribedController = settlementController;
        subscribedProgress = PermanentProgress.Instance;
        // Controller already forwards permanent changes. Subscribe directly only without that publisher.
        if (subscribedController != null) subscribedController.Changed += HandleExternalChanged;
        else if (subscribedProgress != null) subscribedProgress.Changed += HandleExternalChanged;
    }

    private void UnsubscribePresentation()
    {
        if (subscribedController != null) subscribedController.Changed -= HandleExternalChanged;
        else if (subscribedProgress != null) subscribedProgress.Changed -= HandleExternalChanged;
        subscribedController = null;
        subscribedProgress = null;
    }

    private void BindActionListeners()
    {
        UnbindActionListeners();
        if (unlockButton != null)
        {
            ConfigureActionButtonSound(unlockButton);
            if (!HasPersistentAction(unlockButton, nameof(HandleUnlockButtonClick), true))
            {
                subscribedUnlockButton = unlockButton;
                subscribedUnlockButton.onClick.AddListener(HandleUnlockButtonClick);
            }
        }
        if (traitActivationToggleButton != null)
        {
            ConfigureActionButtonSound(traitActivationToggleButton);
            if (!HasPersistentAction(traitActivationToggleButton, nameof(HandleActivationToggleButtonClick), false))
            {
                subscribedActivationButton = traitActivationToggleButton;
                subscribedActivationButton.onClick.AddListener(HandleActivationToggleButtonClick);
            }
        }
    }

    private bool HasPersistentAction(Button button, string method, bool purchase)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentListenerState(i) == UnityEventCallState.Off) continue;
            UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
            string name = button.onClick.GetPersistentMethodName(i);
            if ((target == this && name == method) || (purchase && target == navigationPresentationOwner && name == "ExecuteTraitAction")) return true;
        }
        return false;
    }

    private void UnbindActionListeners()
    {
        if (subscribedUnlockButton != null) subscribedUnlockButton.onClick.RemoveListener(HandleUnlockButtonClick);
        if (subscribedActivationButton != null) subscribedActivationButton.onClick.RemoveListener(HandleActivationToggleButtonClick);
        subscribedUnlockButton = null;
        subscribedActivationButton = null;
    }

    private bool AuthoredPresentationReady()
    {
        var errors = new List<string>();
        CollectAdditionalTraitsBindingErrors(errors);
        if (errors.Count == 0) return true;
        if (!authoredDiagnosticReported)
        {
            authoredDiagnosticReported = true;
            Debug.LogWarning("Additional Traits presentation unavailable. Restore the listed authored Inspector bindings. No replacement entries or actions will run.\n" + string.Join("\n", errors), this);
        }
        return false;
    }

    public void CollectAdditionalTraitsBindingErrors(List<string> errors)
    {
        if (equipmentDevelopmentMode) { ValidateEquipmentPresentation(errors); return; }
        var roles = new HashSet<Component>();
        void Role(string field, Component value, Transform container, bool self = false)
        {
            string reason = value == null ? "Missing binding" : value.gameObject.scene != gameObject.scene ? "Cross-scene ownership" :
                container == null || (!self && value.transform == container) || !value.transform.IsChildOf(container) ? "Wrong ancestry" :
                !roles.Add(value) ? "Duplicate role" : value is TMP_Text text && text.font == null ? "Missing font" : null;
            if (reason != null) errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("ShipTraitTreePanel." + field, value, container, gameObject.scene, reason));
        }
        Role(nameof(authoredDetailRoot), authoredDetailRoot, transform);
        Role(nameof(authoredCostRoot), authoredCostRoot, authoredDetailRoot);
        Role(nameof(authoredCatalogRoot), authoredCatalogRoot, transform);
        Role(nameof(authoredDetailBackground), authoredDetailBackground, authoredDetailRoot, true);
        Role(nameof(authoredCatalogBackground), authoredCatalogBackground, authoredCatalogRoot, true);
        Role(nameof(authoredNodeTemplate), authoredNodeTemplate, transform);
        Role(nameof(selectedIconImage), selectedIconImage, authoredDetailRoot);
        Role(nameof(detailLockImage), detailLockImage, authoredDetailRoot);
        Role(nameof(titleText), titleText, authoredDetailRoot);
        Role(nameof(descriptionText), descriptionText, authoredDetailRoot);
        Role(nameof(branchText), branchText, authoredDetailRoot);
        Role(nameof(levelText), levelText, authoredDetailRoot);
        Role(nameof(costText), costText, authoredCostRoot);
        Role(nameof(statusText), statusText, transform);
        Role(nameof(messageText), messageText, transform);
        Role(nameof(scrapCostIconImage), scrapCostIconImage, authoredCostRoot);
        Role(nameof(coreShardCostIconImage), coreShardCostIconImage, authoredCostRoot);
        if (scrapCostIconRoot != null) Role(nameof(scrapCostIconRoot), scrapCostIconRoot.transform, authoredCostRoot);
        if (coreShardCostIconRoot != null) Role(nameof(coreShardCostIconRoot), coreShardCostIconRoot.transform, authoredCostRoot);
        foreach (Image image in new[] { scrapCostIconImage, coreShardCostIconImage })
            if (image != null && image.sprite == null) errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("ShipTraitTreePanel.costIcon.sprite", image, authoredCostRoot, gameObject.scene, "Missing currency icon"));
        Role(nameof(unlockButton), unlockButton, transform);
        Role(nameof(traitActivationToggleButton), traitActivationToggleButton, transform);
        Role(nameof(unlockButtonLabelText), unlockButtonLabelText, unlockButton != null ? unlockButton.transform : null);
        Role(nameof(traitActivationToggleButtonLabelText), traitActivationToggleButtonLabelText, traitActivationToggleButton != null ? traitActivationToggleButton.transform : null);
        Role(nameof(sidebarButton), sidebarButton, transform.parent);
        if (settlementController == null || settlementController.gameObject.scene != gameObject.scene) errors.Add("ShipTraitTreePanel.settlementController must belong to Settlement.");
        if (navigationPresentationOwner == null || navigationPresentationOwner.gameObject.scene != gameObject.scene) errors.Add("ShipTraitTreePanel.navigationPresentationOwner must belong to Settlement.");
        ShipTraitBranchTabButton[] tabs = { sharedTabButton, machineGunTabButton, sniperTabButton, shotgunTabButton };
        string[] prefixes = { "shared", "machineGun", "sniper", "shotgun" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var kind = (ShipTraitBranchKind)i;
            GameObject panel = GetBranchPanelObject(kind);
            Role(prefixes[i] + "PanelObject", panel != null ? panel.transform : null, authoredCatalogRoot);
            Transform content = GetNodeContentRoot(kind);
            Role(prefixes[i] + "NodeContentRoot", content, panel != null ? panel.transform : null);
            Role(prefixes[i] + "TabButton", tabs[i], transform);
            if (tabs[i] != null)
            {
                tabs[i].CollectPresentationErrors(errors);
                if (tabs[i].BranchKind != kind) errors.Add(prefixes[i] + "TabButton has an incorrect branch mapping.");
            }
            ScrollRect scroll = content != null ? content.GetComponentInParent<ScrollRect>(true) : null;
            if (scroll == null || scroll.content != content || scroll.viewport == null || !content.IsChildOf(scroll.viewport))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(prefixes[i] + "NodeContentRoot.ScrollRect", content, panel?.transform, gameObject.scene, "Incomplete scroll/content/viewport bindings"));
        }
        if (authoredNodeTemplate != null)
        {
            if (authoredNodeTemplate.gameObject.activeSelf) errors.Add("authoredNodeTemplate must stay inactive.");
            authoredNodeTemplate.CollectPresentationErrors(errors);
            if (authoredCatalogRoot != null && authoredNodeTemplate.transform.IsChildOf(authoredCatalogRoot)) errors.Add("Place the inactive template outside the catalog/content layout.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new HashSet<ShipTraitNodeButton>();
        if (branchNodes != null)
            for (int i = 0; i < branchNodes.Count; i++)
            {
                ShipTraitBranchNodeEntry entry = branchNodes[i];
                if (entry == null || string.IsNullOrWhiteSpace(GetEntryNodeId(entry)) || !ids.Add(GetEntryNodeId(entry)))
                    errors.Add("branchNodes.Array.data[" + i + "]: missing or duplicate stable node ID.");
                if (entry?.nodeButton == null) continue;
                if (entry.nodeButton == authoredNodeTemplate || !nodes.Add(entry.nodeButton)) errors.Add("branchNodes.Array.data[" + i + "]: duplicate/template entry mapping.");
                if (entry.nodeButton != authoredNodeTemplate && (entry.nodeButton.NodeId != GetEntryNodeId(entry) || entry.nodeButton.BranchKind != entry.branchKind))
                    errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("branchNodes.Array.data[" + i + "].nodeButton", entry.nodeButton, GetNodeContentRoot(entry.branchKind), gameObject.scene, "Conflicting stable ID or category mapping"));
                Role("branchNodes.Array.data[" + i + "].nodeButton", entry.nodeButton, GetNodeContentRoot(entry.branchKind));
                entry.nodeButton.CollectPresentationErrors(errors);
            }
    }

    private void EnsureAuthoredNodes()
    {
        if (branchNodes == null) return;
        for (int i = generatedNodeButtons.Count - 1; i >= 0; i--)
        {
            ShipTraitNodeButton generated = generatedNodeButtons[i];
            bool retained = false;
            foreach (ShipTraitBranchNodeEntry entry in branchNodes) retained |= entry != null && entry.nodeButton == generated;
            if (retained && generated != null) continue;
            generatedNodeButtons.RemoveAt(i);
            if (generated == null) continue;
            generated.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(generated.gameObject);
            else DestroyImmediate(generated.gameObject);
        }
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry == null || entry.nodeButton != null) continue;
            ShipTraitNodeButton node = Instantiate(authoredNodeTemplate, new InstantiateParameters { scene = gameObject.scene, parent = GetNodeContentRoot(entry.branchKind) });
            node.name = "Trait_" + GetEntryNodeId(entry);
            node.Bind(this, entry.branchKind, GetEntryNodeId(entry), GetDisplayName(entry), GetEntryIcon(entry));
            generatedNodeButtons.Add(node);
            entry.nodeButton = node;
            // The inactive template is cloned and bound before OnEnable can execute.
            node.gameObject.SetActive(true);
        }
    }

    private void RefreshAuthoredNavigation()
    {
        visibleTraitControls.Clear();
        foreach (ShipTraitBranchTabButton tab in new[] { sharedTabButton, machineGunTabButton, shotgunTabButton, sniperTabButton })
            if (tab != null && Available(tab.GetComponent<Button>())) visibleTraitControls.Add(tab.GetComponent<Button>());
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
            if (entry?.nodeButton != null && Available(entry.nodeButton.GetComponent<Button>())) visibleTraitControls.Add(entry.nodeButton.GetComponent<Button>());
        if (Available(unlockButton)) visibleTraitControls.Add(unlockButton);
        if (Available(traitActivationToggleButton)) visibleTraitControls.Add(traitActivationToggleButton);
        for (int i = 0; i < visibleTraitControls.Count; i++)
        {
            Selectable control = visibleTraitControls[i];
            control.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = visibleTraitControls[Mathf.Max(0, i - 1)],
                selectOnDown = visibleTraitControls[Mathf.Min(visibleTraitControls.Count - 1, i + 1)],
                selectOnLeft = sidebarButton,
                selectOnRight = Available(unlockButton) ? unlockButton : sidebarButton
            };
        }
        if (!CanUseTraitInput || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform)) return;
        if (!visibleTraitControls.Contains(selected.GetComponent<Selectable>()))
            EventSystem.current.SetSelectedGameObject(visibleTraitControls.Count > 0 ? visibleTraitControls[0].gameObject : sidebarButton.gameObject);
    }

    private static bool Available(Selectable target) => target != null && target.IsActive() && target.IsInteractable();

    private void Reset()
    {
        branchNodes = new List<ShipTraitBranchNodeEntry>
        {
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Shared,
                nodeId = "shared_01",
                displayName = "공유 특성 1",
                description = "공유 특성 첫 번째 노드입니다.",
                requiredUnlockedShipCount = 1,
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Shared,
                nodeId = "shared_02",
                displayName = "공유 특성 2",
                description = "공유 특성 두 번째 노드입니다.",
                requiredUnlockedShipCount = 1,
                prerequisiteNodeIds = new List<string> { "shared_01" },
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Shared,
                nodeId = "shared_03",
                displayName = "공유 특성 3",
                description = "공유 특성 세 번째 노드입니다.",
                requiredUnlockedShipCount = 1,
                prerequisiteNodeIds = new List<string> { "shared_02" },
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Shared,
                nodeId = "shared_04",
                displayName = "공유 특성 4",
                description = "공유 특성 네 번째 노드입니다.",
                requiredUnlockedShipCount = 1,
                prerequisiteNodeIds = new List<string> { "shared_03" },
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.MachineGun,
                nodeId = "machinegun_01",
                displayName = "기관총 특성 1",
                description = "기관총 트리 첫 번째 노드입니다.",
                requiredUnlockedShipCount = 1,
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Sniper,
                nodeId = "sniper_01",
                displayName = "스나 특성 1",
                description = "스나이퍼 트리 첫 번째 노드입니다.",
                requiredUnlockedShipCount = 2,
                maxLevel = 1
            },
            new ShipTraitBranchNodeEntry
            {
                branchKind = ShipTraitBranchKind.Shotgun,
                nodeId = "shotgun_01",
                displayName = "샷건 특성 1",
                description = "샷건 트리 첫 번째 노드입니다.",
                requiredUnlockedShipCount = 3,
                maxLevel = 1
            }
        };
    }

    private void OnValidate()
    {
        SyncBranchGates();
        // Definitions are synchronized during runtime refresh, not while authoring presentation bindings.
    }

    private void Awake()
    {
        if (equipmentDevelopmentMode) return;
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        SyncBranchGates();
        if (AuthoredPresentationReady()) EnsureAuthoredNodes();
        BindNodes();
        BindBranchTabs();

        BindActionListeners();
    }

    private void OnEnable()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        SubscribePresentation();
        if (equipmentDevelopmentMode)
        {
            BindEquipmentPresentation();
            RefreshEquipmentPresentation();
            return;
        }
        BindActionListeners();

        if (AuthoredPresentationReady()) EnsureAuthoredNodes();
        RefreshPanel();
    }

    private void Start()
    {
        RefreshPanel();
    }

    private void OnDisable()
    {
        UnsubscribePresentation();
        UnbindEquipmentPresentation();
        UnbindActionListeners();
    }

    private void OnDestroy()
    {
        UnsubscribePresentation();
        UnbindEquipmentPresentation();
        UnbindActionListeners();
    }

    public void SetTargetShipId(string shipId)
    {
        targetShipIdOverride = shipId;
        RefreshPanel();
    }

    public void ClearTargetShipOverride()
    {
        targetShipIdOverride = string.Empty;
        RefreshPanel();
    }

    public void SelectBranch(ShipTraitBranchKind branchKind)
    {
        SelectBranchTab(branchKind);
    }

    public void SelectBranchTab(ShipTraitBranchKind branchKind)
    {
        if (equipmentDevelopmentMode)
        {
            SelectEquipmentBranch(branchKind);
            return;
        }
        int unlockedShipCount = CountUnlockedShips();

        if (!IsBranchGateAvailable(branchKind, unlockedShipCount))
        {
            SetMessage(BuildBranchGateLockReason(branchKind, unlockedShipCount));
            RefreshBranchTabs(unlockedShipCount);
            return;
        }

        selectedBranch = branchKind;

        ShipTraitBranchNodeEntry firstEntry = FindFirstSelectableEntry(branchKind, unlockedShipCount);

        if (firstEntry == null)
        {
            firstEntry = FindFirstEntry(branchKind);
        }

        if (firstEntry == null)
        {
            hasSelection = false;
            selectedNodeId = string.Empty;
            RefreshPanel();
            return;
        }

        SelectNode(branchKind, GetEntryNodeId(firstEntry));
    }

    public void SelectNode(ShipTraitBranchKind branchKind, string nodeId)
    {
        selectedBranch = branchKind;
        selectedNodeId = nodeId;
        hasSelection = true;

        RefreshPanel();
    }

    public void RefreshPanel()
    {
        if (equipmentDevelopmentMode) { RefreshEquipmentPresentation(); return; }
        if (UsesAuthoredPresentation && !AuthoredPresentationReady())
        {
            foreach (Button action in new[] { unlockButton, traitActivationToggleButton })
                if (action != null && action.gameObject.scene == gameObject.scene && action.transform.IsChildOf(transform)) action.interactable = false;
            if (CanUseTraitInput && EventSystem.current != null && Available(sidebarButton) && sidebarButton.gameObject.scene == gameObject.scene)
            {
                GameObject focus = EventSystem.current.currentSelectedGameObject;
                if (focus != null && focus.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(sidebarButton.gameObject);
            }
            return;
        }
        SyncBranchGates();
        SyncBranchNodesFromDefinitions();
        EnsureAuthoredNodes();
        BindNodes();
        BindBranchTabs();

        int unlockedShipCount = CountUnlockedShips();

        RefreshBranchPanelObjects(unlockedShipCount);
        EnsureValidSelection(unlockedShipCount);
        RefreshNodeStates(unlockedShipCount);
        RefreshDetail(unlockedShipCount);
        if (UsesAuthoredPresentation) RefreshAuthoredNavigation();
    }

    public bool CanUnlockSelectedTrait()
    {
        if (equipmentDevelopmentMode) return false;
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            return false;
        }

        return CanUnlockEntry(entry, CountUnlockedShips());
    }

    public bool TryUnlockSelectedTrait()
    {
        if (equipmentDevelopmentMode) return false;
        if (UsesAuthoredPresentation && (!AuthoredPresentationReady() || !CanUseTraitInput)) return false;
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            SetMessage("해금할 특성을 선택하세요.");
            return false;
        }

        int unlockedShipCount = CountUnlockedShips();

        if (!IsNodeSelectable(entry, unlockedShipCount))
        {
            SetMessage(BuildLockReason(entry, unlockedShipCount));
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 특성을 해금할 수 없습니다.");
            return false;
        }

        string nodeId = GetEntryNodeId(entry);
        int currentLevel = progress.GetTraitLevel(nodeId);
        int maxLevel = GetEntryMaxLevel(entry);

        if (currentLevel >= maxLevel)
        {
            SetMessage($"{GetDisplayName(entry)}은 이미 최대 레벨입니다.");
            return false;
        }

        int scrapCost = GetEntryScrapCost(entry);
        int coreCost = GetEntryCoreShardCost(entry);

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족.\n{FormatCost(scrapCost, coreCost)}");
            RefreshPanel();
            return false;
        }

        progress.SetTraitLevel(nodeId, currentLevel + 1);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }

        string completeMessage = IsReinforcementNode(entry)
            ? $"{GetDisplayName(entry)} 장비 해금 완료. 다음 탐사 시작 시 기본 장비 후보로 사용됩니다."
            : $"{GetDisplayName(entry)} 해금 완료. {currentLevel + 1}/{maxLevel}";
        SetMessage(completeMessage);
        RefreshPanel();
        return true;
    }

    public bool TryToggleSelectedTraitActive()
    {
        if (equipmentDevelopmentMode) return false;
        if (UsesAuthoredPresentation && (!AuthoredPresentationReady() || !CanUseTraitInput)) return false;
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            SetMessage("비활성화할 특성을 선택하세요.");
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 특성 상태를 바꿀 수 없습니다.");
            return false;
        }

        string nodeId = GetEntryNodeId(entry);
        if (progress.GetTraitLevel(nodeId) <= 0)
        {
            SetMessage("해금된 특성만 비활성화할 수 있습니다.");
            return false;
        }

        bool wasActive = progress.IsTraitActive(nodeId);
        progress.SetTraitActive(nodeId, !wasActive);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }

        bool isActiveNow = progress.IsTraitActive(nodeId);
        string displayName = GetDisplayName(entry);
        string targetText = IsReinforcementNode(entry) ? "시작 장비 후보" : "탐사 시작 기본 특성";
        string stateText = isActiveNow ? "활성화" : "비활성화";

        SetMessage($"{displayName} {stateText}. 다음 탐사부터 {targetText}에 {(isActiveNow ? "포함됩니다" : "포함되지 않습니다")}.");
        RefreshPanel();
        return true;
    }

    private void SyncBranchGates()
    {
        if (sharedBranchGate != null)
        {
            sharedBranchGate.branchKind = ShipTraitBranchKind.Shared;
        }

        if (machineGunBranchGate != null)
        {
            machineGunBranchGate.branchKind = ShipTraitBranchKind.MachineGun;
        }

        if (sniperBranchGate != null)
        {
            sniperBranchGate.branchKind = ShipTraitBranchKind.Sniper;
        }

        if (shotgunBranchGate != null)
        {
            shotgunBranchGate.branchKind = ShipTraitBranchKind.Shotgun;
        }
    }

    private void SyncBranchNodesFromDefinitions()
    {
        if (branchNodes == null)
        {
            return;
        }

        for (int i = 0; i < branchNodes.Count; i++)
        {
            SyncBranchNodeFromDefinition(branchNodes[i]);
        }
    }



    private Transform GetNodeContentRoot(ShipTraitBranchKind branchKind)
    {
        Transform branchRoot = branchKind switch
        {
            ShipTraitBranchKind.Shared => sharedNodeContentRoot,
            ShipTraitBranchKind.MachineGun => machineGunNodeContentRoot,
            ShipTraitBranchKind.Sniper => sniperNodeContentRoot,
            ShipTraitBranchKind.Shotgun => shotgunNodeContentRoot,
            _ => null
        };

        return branchRoot;
    }

    private void SyncBranchNodeFromDefinition(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null || entry.manualOverrideDefinitionFields)
        {
            return;
        }

        if (entry.traitDefinition != null)
        {
            entry.nodeId = entry.traitDefinition.TraitId;
            entry.displayName = entry.traitDefinition.DisplayName;
            entry.icon = entry.traitDefinition.Icon;
            entry.description = entry.traitDefinition.Description;
            entry.maxLevel = entry.traitDefinition.MaxLevel;
            entry.branchKind = InferBranchKind(entry.traitDefinition);
            return;
        }

        if (entry.reinforcementDefinition != null)
        {
            entry.nodeId = entry.reinforcementDefinition.EquipmentId;
            entry.displayName = entry.reinforcementDefinition.DisplayName;
            entry.icon = entry.reinforcementDefinition.Icon;
            entry.description = entry.reinforcementDefinition.Description;
            entry.maxLevel = 1;
            entry.branchKind = InferBranchKind(entry.reinforcementDefinition);
        }
    }

    private ShipTraitBranchKind InferBranchKind(TraitDefinition trait)
    {
        if (trait == null || trait.Category == TraitCategory.Shared)
        {
            return ShipTraitBranchKind.Shared;
        }

        return trait.WeaponTreeType switch
        {
            WeaponTreeType.MachineGun => ShipTraitBranchKind.MachineGun,
            WeaponTreeType.Sniper => ShipTraitBranchKind.Sniper,
            WeaponTreeType.Shotgun => ShipTraitBranchKind.Shotgun,
            _ => ShipTraitBranchKind.Shared
        };
    }

    private ShipTraitBranchKind InferBranchKind(ReinforcementDefinition reinforcement)
    {
        if (reinforcement == null)
        {
            return ShipTraitBranchKind.Shared;
        }

        return reinforcement.Availability switch
        {
            ReinforcementAvailability.MachineGunOnly => ShipTraitBranchKind.MachineGun,
            ReinforcementAvailability.SniperOnly => ShipTraitBranchKind.Sniper,
            ReinforcementAvailability.ShotgunOnly => ShipTraitBranchKind.Shotgun,
            _ => ShipTraitBranchKind.Shared
        };
    }

    private void BindNodes()
    {
        if (branchNodes == null)
        {
            return;
        }

        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry == null || entry.nodeButton == null)
            {
                continue;
            }

            entry.nodeButton.Bind(
                this,
                entry.branchKind,
                GetEntryNodeId(entry),
                GetDisplayName(entry),
                GetEntryIcon(entry)
            );
            entry.nodeButton.SetAllowClickWhenLocked(allowLockedNodeSelection);
        }
    }

    private void BindBranchTabs()
    {
        BindBranchTab(sharedTabButton, ShipTraitBranchKind.Shared);
        BindBranchTab(machineGunTabButton, ShipTraitBranchKind.MachineGun);
        BindBranchTab(sniperTabButton, ShipTraitBranchKind.Sniper);
        BindBranchTab(shotgunTabButton, ShipTraitBranchKind.Shotgun);
    }

    private void BindBranchTab(ShipTraitBranchTabButton tabButton, ShipTraitBranchKind branchKind)
    {
        if (tabButton == null)
        {
            return;
        }

        tabButton.Bind(
            this,
            branchKind,
            GetBranchDisplayName(branchKind)
        );

        tabButton.SetAllowClickWhenLocked(allowLockedBranchTabClick);
    }

    private void RefreshBranchTabs(int unlockedShipCount)
    {
        RefreshBranchTab(sharedTabButton, ShipTraitBranchKind.Shared, unlockedShipCount);
        RefreshBranchTab(machineGunTabButton, ShipTraitBranchKind.MachineGun, unlockedShipCount);
        RefreshBranchTab(sniperTabButton, ShipTraitBranchKind.Sniper, unlockedShipCount);
        RefreshBranchTab(shotgunTabButton, ShipTraitBranchKind.Shotgun, unlockedShipCount);
    }

    private void RefreshBranchTab(
        ShipTraitBranchTabButton tabButton,
        ShipTraitBranchKind branchKind,
        int unlockedShipCount)
    {
        if (tabButton == null)
        {
            return;
        }

        bool available = IsBranchGateAvailable(branchKind, unlockedShipCount);
        bool selected = selectedBranch == branchKind;
        bool visible = available || !hideLockedBranchTabs;

        tabButton.SetLabel(available ? GetBranchDisplayName(branchKind) : "잠김");
        tabButton.SetVisualState(available, selected, visible);
    }

    private void EnsureSelectedBranchAvailable(int unlockedShipCount)
    {
        if (IsBranchGateAvailable(selectedBranch, unlockedShipCount))
        {
            return;
        }

        ShipTraitBranchKind fallbackBranch = FindFirstAvailableBranch(unlockedShipCount);
        selectedBranch = fallbackBranch;
        selectedNodeId = string.Empty;
        hasSelection = false;
    }

    private ShipTraitBranchKind FindFirstAvailableBranch(int unlockedShipCount)
    {
        if (IsBranchGateAvailable(defaultSelectedBranch, unlockedShipCount))
        {
            return defaultSelectedBranch;
        }

        if (IsBranchGateAvailable(ShipTraitBranchKind.Shared, unlockedShipCount))
        {
            return ShipTraitBranchKind.Shared;
        }

        if (IsBranchGateAvailable(ShipTraitBranchKind.MachineGun, unlockedShipCount))
        {
            return ShipTraitBranchKind.MachineGun;
        }

        if (IsBranchGateAvailable(ShipTraitBranchKind.Shotgun, unlockedShipCount))
        {
            return ShipTraitBranchKind.Shotgun;
        }

        if (IsBranchGateAvailable(ShipTraitBranchKind.Sniper, unlockedShipCount))
        {
            return ShipTraitBranchKind.Sniper;
        }

        return ShipTraitBranchKind.Shared;
    }

    private void RefreshBranchPanelObjects(int unlockedShipCount)
    {
        if (useBranchTabMode)
        {
            EnsureSelectedBranchAvailable(unlockedShipCount);

            SetBranchPanelActiveForTabMode(
                ShipTraitBranchKind.Shared,
                IsBranchGateAvailable(ShipTraitBranchKind.Shared, unlockedShipCount)
            );

            SetBranchPanelActiveForTabMode(
                ShipTraitBranchKind.MachineGun,
                IsBranchGateAvailable(ShipTraitBranchKind.MachineGun, unlockedShipCount)
            );

            SetBranchPanelActiveForTabMode(
                ShipTraitBranchKind.Sniper,
                IsBranchGateAvailable(ShipTraitBranchKind.Sniper, unlockedShipCount)
            );

            SetBranchPanelActiveForTabMode(
                ShipTraitBranchKind.Shotgun,
                IsBranchGateAvailable(ShipTraitBranchKind.Shotgun, unlockedShipCount)
            );

            RefreshBranchTabs(unlockedShipCount);
            return;
        }

        SetBranchPanelActive(
            ShipTraitBranchKind.Shared,
            IsBranchGateAvailable(ShipTraitBranchKind.Shared, unlockedShipCount)
        );

        SetBranchPanelActive(
            ShipTraitBranchKind.MachineGun,
            IsBranchGateAvailable(ShipTraitBranchKind.MachineGun, unlockedShipCount)
        );

        SetBranchPanelActive(
            ShipTraitBranchKind.Sniper,
            IsBranchGateAvailable(ShipTraitBranchKind.Sniper, unlockedShipCount)
        );

        SetBranchPanelActive(
            ShipTraitBranchKind.Shotgun,
            IsBranchGateAvailable(ShipTraitBranchKind.Shotgun, unlockedShipCount)
        );
    }

    private void SetBranchPanelActiveForTabMode(ShipTraitBranchKind branchKind, bool available)
    {
        GameObject panelObject = GetBranchPanelObject(branchKind);

        if (panelObject != null)
        {
            panelObject.SetActive(available && selectedBranch == branchKind);
        }

        RefreshBranchGateVisual(branchKind, available);
    }

    private void SetBranchPanelActive(ShipTraitBranchKind branchKind, bool available)
    {
        GameObject panelObject = GetBranchPanelObject(branchKind);

        if (panelObject != null)
        {
            if (deactivateBranchPanelWhenLocked)
            {
                panelObject.SetActive(available);
            }
            else
            {
                panelObject.SetActive(true);
            }
        }

        RefreshBranchGateVisual(branchKind, available);
    }

    private GameObject GetBranchPanelObject(ShipTraitBranchKind branchKind)
    {
        return branchKind switch
        {
            ShipTraitBranchKind.Shared => sharedPanelObject,
            ShipTraitBranchKind.MachineGun => machineGunPanelObject,
            ShipTraitBranchKind.Sniper => sniperPanelObject,
            ShipTraitBranchKind.Shotgun => shotgunPanelObject,
            _ => null
        };
    }

    private ShipTraitBranchGate GetBranchGate(ShipTraitBranchKind branchKind)
    {
        return branchKind switch
        {
            ShipTraitBranchKind.Shared => sharedBranchGate,
            ShipTraitBranchKind.MachineGun => machineGunBranchGate,
            ShipTraitBranchKind.Sniper => sniperBranchGate,
            ShipTraitBranchKind.Shotgun => shotgunBranchGate,
            _ => null
        };
    }

    private void RefreshBranchGateVisual(ShipTraitBranchKind branchKind, bool available)
    {
        ShipTraitBranchGate gate = GetBranchGate(branchKind);

        if (gate == null)
        {
            return;
        }

        if (gate.lockedOverlayObject != null)
        {
            gate.lockedOverlayObject.SetActive(!available);
        }

        if (gate.panelCanvasGroup != null)
        {
            if (!branchAlphaBaselines.ContainsKey(gate.panelCanvasGroup)) branchAlphaBaselines.Add(gate.panelCanvasGroup, gate.panelCanvasGroup.alpha);
            float baseline = UsesAuthoredPresentation ? branchAlphaBaselines[gate.panelCanvasGroup] : 1f;
            gate.panelCanvasGroup.alpha = baseline * (available ? 1f : gate.lockedAlpha);

            if (gate.disableInteractionWhenLocked)
            {
                gate.panelCanvasGroup.interactable = available;
                gate.panelCanvasGroup.blocksRaycasts = available;
            }
        }
    }

    private bool IsBranchGateAvailable(ShipTraitBranchKind branchKind, int unlockedShipCount)
    {
        ShipTraitBranchGate gate = GetBranchGate(branchKind);

        if (gate == null)
        {
            return true;
        }

        gate.branchKind = branchKind;

        if (gate.unlockedByDefault)
        {
            return true;
        }

        if (gate.requiredUnlockedShipCount > 0 &&
            unlockedShipCount < gate.requiredUnlockedShipCount)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(gate.requiredShipId))
        {
            ShipDefinition requiredShip = FindShipDefinition(gate.requiredShipId);

            if (requiredShip == null || !IsShipUnlocked(requiredShip))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(gate.requiredUnlockFlag))
        {
            PermanentProgress progress = PermanentProgress.Instance;

            if (progress == null || !progress.HasUnlockFlag(gate.requiredUnlockFlag))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureValidSelection(int unlockedShipCount)
    {
        if (useBranchTabMode)
        {
            EnsureSelectedBranchAvailable(unlockedShipCount);

            ShipTraitBranchNodeEntry selectedTabEntry = FindEntry(selectedBranch, selectedNodeId);

            if (!hasSelection || selectedTabEntry == null || (selectedTabEntry.hideWhenLocked && !IsNodeSelectable(selectedTabEntry, unlockedShipCount)))
            {
                ShipTraitBranchNodeEntry firstInTab = FindFirstSelectableEntry(selectedBranch, unlockedShipCount);

                if (firstInTab == null)
                {
                    firstInTab = FindFirstEntry(selectedBranch);
                }

                if (firstInTab != null)
                {
                    selectedNodeId = GetEntryNodeId(firstInTab);
                    hasSelection = true;
                }
                else
                {
                    selectedNodeId = string.Empty;
                    hasSelection = false;
                }
            }

            return;
        }

        ShipTraitBranchNodeEntry selectedEntry = FindEntry(selectedBranch, selectedNodeId);

        if (!hasSelection || selectedEntry == null)
        {
            ShipTraitBranchNodeEntry firstSelectable = FindFirstSelectableEntry(unlockedShipCount);

            if (firstSelectable != null)
            {
                selectedBranch = firstSelectable.branchKind;
                selectedNodeId = GetEntryNodeId(firstSelectable);
                hasSelection = true;
                return;
            }

            ShipTraitBranchNodeEntry firstEntry = FindFirstEntry();

            if (firstEntry != null)
            {
                selectedBranch = firstEntry.branchKind;
                selectedNodeId = GetEntryNodeId(firstEntry);
                hasSelection = true;
            }
        }
    }

    private void RefreshNodeStates(int unlockedShipCount)
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry == null || entry.nodeButton == null)
            {
                continue;
            }

            bool selectable = IsNodeSelectable(entry, unlockedShipCount);
            bool unlocked = IsNodeUnlocked(entry);
            bool active = IsNodeActive(entry);
            bool selected =
                hasSelection &&
                selectedBranch == entry.branchKind &&
                selectedNodeId == GetEntryNodeId(entry);

            bool visibleInCurrentTab = !useBranchTabMode || entry.branchKind == selectedBranch;

            if (!visibleInCurrentTab)
            {
                entry.nodeButton.gameObject.SetActive(false);
            }
            else if (entry.hideWhenLocked)
            {
                entry.nodeButton.gameObject.SetActive(selectable);
            }
            else
            {
                entry.nodeButton.gameObject.SetActive(true);
            }

            entry.nodeButton.SetVisualState(selectable, unlocked, active, selected);
            entry.nodeButton.SetLevelView(BuildNodeButtonLevelText(entry));
        }
    }

    private void RefreshDetail(int unlockedShipCount)
    {
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            SetIcon(null);
            SetText(titleText, "특성 없음");
            ClearDetailTexts("연결된 특성 노드가 없습니다.");
            SetDetailLockVisual(false);
            SetUnlockButton(false, "특성 없음");
            SetActivationToggleButton(false, "비활성화", false);
            return;
        }

        bool selectable = IsNodeSelectable(entry, unlockedShipCount);
        bool unlocked = IsNodeUnlocked(entry);
        bool active = IsNodeActive(entry);
        bool canUnlock = CanUnlockEntry(entry, unlockedShipCount);
        ShipTraitDetailViewData viewData = BuildTraitDetailViewData(entry, selectable, unlocked, unlockedShipCount);

        SetIcon(GetEntryIcon(entry));
        SetText(titleText, GetDisplayName(entry));
        SetText(descriptionText, viewData.DescriptionText);
        SetText(branchText, viewData.BranchText);
        SetText(levelText, viewData.LevelText);
        SetText(statusText, viewData.StatusText);
        SetText(costText, viewData.CostText);
        SetCostIconVisuals(entry);
        SetDetailLockVisual(!selectable);
        SetUnlockButton(canUnlock, BuildUnlockButtonLabel(entry, selectable, unlocked));
        SetActivationToggleButton(
            unlocked,
            BuildActivationToggleButtonLabel(entry, unlocked, active),
            ShouldShowActivationToggle(unlocked)
        );
    }

    public static bool ShouldShowActivationToggle(bool unlocked)
    {
        return unlocked;
    }

    private bool CanUnlockEntry(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return false;
        }

        if (!IsNodeSelectable(entry, unlockedShipCount))
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return false;
        }

        int currentLevel = progress.GetTraitLevel(GetEntryNodeId(entry));
        int maxLevel = GetEntryMaxLevel(entry);

        if (currentLevel >= maxLevel)
        {
            return false;
        }

        return progress.CanSpend(
            GetEntryScrapCost(entry),
            GetEntryCoreShardCost(entry)
        );
    }

    private bool IsNodeSelectable(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return false;
        }

        if (!IsBranchGateAvailable(entry.branchKind, unlockedShipCount))
        {
            return false;
        }

        if (!IsEntryGateAvailable(entry, unlockedShipCount))
        {
            return false;
        }

        if (!AreNodePrerequisitesMet(entry))
        {
            return false;
        }

        if (!AreAdditionalUnlockConditionsMet(entry, unlockedShipCount))
        {
            return false;
        }

        return true;
    }

    private bool IsEntryGateAvailable(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.requiredUnlockedShipCount > 0 &&
            unlockedShipCount < entry.requiredUnlockedShipCount)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredShipId))
        {
            ShipDefinition requiredShip = FindShipDefinition(entry.requiredShipId);

            if (requiredShip == null || !IsShipUnlocked(requiredShip))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredUnlockFlag))
        {
            PermanentProgress progress = PermanentProgress.Instance;

            if (progress == null || !progress.HasUnlockFlag(entry.requiredUnlockFlag))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreNodePrerequisitesMet(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.prerequisiteNodeIds == null || entry.prerequisiteNodeIds.Count == 0)
        {
            return true;
        }

        foreach (string prerequisiteNodeId in entry.prerequisiteNodeIds)
        {
            if (string.IsNullOrWhiteSpace(prerequisiteNodeId))
            {
                continue;
            }

            if (!IsNodeUnlocked(prerequisiteNodeId))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreAdditionalUnlockConditionsMet(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.unlockConditions == null || entry.unlockConditions.Count == 0)
        {
            return true;
        }

        foreach (ShipTraitUnlockCondition condition in entry.unlockConditions)
        {
            if (!IsUnlockConditionMet(condition, unlockedShipCount))
            {
                return false;
            }
        }

        return true;
    }

    private ShipTraitUnlockCondition FindFirstUnmetAdditionalUnlockCondition(
        ShipTraitBranchNodeEntry entry,
        int unlockedShipCount)
    {
        if (entry == null || entry.unlockConditions == null)
        {
            return null;
        }

        foreach (ShipTraitUnlockCondition condition in entry.unlockConditions)
        {
            if (!IsUnlockConditionMet(condition, unlockedShipCount))
            {
                return condition;
            }
        }

        return null;
    }

    private bool IsUnlockConditionMet(ShipTraitUnlockCondition condition, int unlockedShipCount)
    {
        if (condition == null)
        {
            return true;
        }

        int requiredValue = GetConditionRequiredValue(condition);
        return GetUnlockConditionCurrentValue(condition, unlockedShipCount) >= requiredValue;
    }

    private int GetConditionRequiredValue(ShipTraitUnlockCondition condition)
    {
        if (condition == null)
        {
            return 1;
        }

        int value = Mathf.Max(0, condition.requiredValue);
        return value <= 0 ? 1 : value;
    }

    private int GetUnlockConditionCurrentValue(ShipTraitUnlockCondition condition, int unlockedShipCount)
    {
        if (condition == null)
        {
            return 0;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        switch (condition.conditionKind)
        {
            case ShipTraitUnlockConditionKind.UnlockFlag:
                if (progress == null || string.IsNullOrWhiteSpace(condition.targetId))
                {
                    return 0;
                }

                return progress.HasUnlockFlag(condition.targetId) ? 1 : 0;

            case ShipTraitUnlockConditionKind.ShipUnlocked:
                if (string.IsNullOrWhiteSpace(condition.targetId))
                {
                    return 0;
                }

                ShipDefinition ship = FindShipDefinition(condition.targetId);
                return ship != null && IsShipUnlocked(ship) ? 1 : 0;

            case ShipTraitUnlockConditionKind.UnlockedShipCount:
                return Mathf.Max(0, unlockedShipCount);

            case ShipTraitUnlockConditionKind.BuildingLevel:
                return progress != null ? progress.GetBuildingLevel(condition.buildingType) : 0;

            case ShipTraitUnlockConditionKind.TraitLevel:
                return progress != null && !string.IsNullOrWhiteSpace(condition.targetId)
                    ? progress.GetTraitLevel(condition.targetId)
                    : 0;

            case ShipTraitUnlockConditionKind.TotalRunCount:
                return progress != null ? progress.TotalRunCount : 0;

            case ShipTraitUnlockConditionKind.SafeReturnCount:
                return progress != null ? progress.SafeReturnCount : 0;

            case ShipTraitUnlockConditionKind.EmergencyReturnCount:
                return progress != null ? progress.EmergencyReturnCount : 0;

            case ShipTraitUnlockConditionKind.BossDefeatCount:
                return progress != null ? progress.BossDefeatCount : 0;

            case ShipTraitUnlockConditionKind.TotalCollectedScrapParts:
                return progress != null ? progress.TotalCollectedScrapParts : 0;

            case ShipTraitUnlockConditionKind.TotalCollectedCoreShards:
                return progress != null ? progress.TotalCollectedCoreShards : 0;

            case ShipTraitUnlockConditionKind.TotalCommittedScrapParts:
                return progress != null ? progress.TotalCommittedScrapParts : 0;

            case ShipTraitUnlockConditionKind.TotalCommittedCoreShards:
                return progress != null ? progress.TotalCommittedCoreShards : 0;

            case ShipTraitUnlockConditionKind.OwnedScrapParts:
                return progress != null ? progress.ScrapParts : 0;

            case ShipTraitUnlockConditionKind.OwnedCoreShards:
                return progress != null ? progress.CoreShards : 0;

            default:
                return 0;
        }
    }

    private bool IsNodeUnlocked(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        return IsNodeUnlocked(GetEntryNodeId(entry));
    }

    private bool IsNodeUnlocked(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return false;
        }

        return progress.GetTraitLevel(nodeId) > 0;
    }

    private bool IsNodeActive(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        return IsNodeActive(GetEntryNodeId(entry));
    }

    private bool IsNodeActive(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        return progress != null && progress.IsTraitActive(nodeId);
    }

    private int GetNodeLevel(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null || PermanentProgress.Instance == null)
        {
            return 0;
        }

        int rawLevel = PermanentProgress.Instance.GetTraitLevel(GetEntryNodeId(entry));
        return Mathf.Clamp(rawLevel, 0, GetEntryMaxLevel(entry));
    }

    private int GetEntryMaxLevel(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return 1;
        }

        if (entry.reinforcementDefinition != null)
        {
            return 1;
        }

        if (entry.traitDefinition != null)
        {
            return Mathf.Max(1, entry.traitDefinition.MaxLevel);
        }

        return Mathf.Max(1, entry.maxLevel);
    }

    private int GetEntryScrapCost(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return 0;
        }

        if (entry.traitDefinition != null && settlementController != null)
        {
            int targetLevel = GetNodeLevel(entry) + 1;
            return settlementController.GetTraitScrapCost(entry.traitDefinition, targetLevel);
        }

        int scrapCost = Mathf.Max(0, entry.scrapCost);
        int coreCost = Mathf.Max(0, entry.coreShardCost);

        if (scrapCost <= 0 && coreCost <= 0 &&
            entry.useReinforcementCostAsScrapCost &&
            entry.reinforcementDefinition != null)
        {
            return entry.reinforcementDefinition.Cost;
        }

        return scrapCost;
    }

    private int GetEntryCoreShardCost(ShipTraitBranchNodeEntry entry)
    {
        if (entry != null && entry.traitDefinition != null && settlementController != null)
        {
            int targetLevel = GetNodeLevel(entry) + 1;
            return settlementController.GetTraitCoreCost(entry.traitDefinition, targetLevel);
        }

        return entry != null ? Mathf.Max(0, entry.coreShardCost) : 0;
    }

    private bool IsReinforcementNode(ShipTraitBranchNodeEntry entry)
    {
        return entry != null && entry.reinforcementDefinition != null;
    }

    private Sprite GetEntryIcon(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.manualOverrideDefinitionFields && entry.icon != null)
        {
            return entry.icon;
        }

        if (entry.traitDefinition != null && entry.traitDefinition.Icon != null)
        {
            return entry.traitDefinition.Icon;
        }

        if (entry.reinforcementDefinition != null && entry.reinforcementDefinition.Icon != null)
        {
            return entry.reinforcementDefinition.Icon;
        }

        return entry.icon;
    }

    private string GetEntryDescription(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return "특성 설명이 없습니다.";
        }

        if (entry.manualOverrideDefinitionFields && !string.IsNullOrWhiteSpace(entry.description))
        {
            return entry.description;
        }

        if (entry.traitDefinition != null)
        {
            return entry.traitDefinition.Description;
        }

        if (entry.reinforcementDefinition != null)
        {
            return entry.reinforcementDefinition.Description;
        }

        return !string.IsNullOrWhiteSpace(entry.description)
            ? entry.description
            : GetDefaultDescription(entry.branchKind);
    }

    private ShipTraitDetailViewData BuildTraitDetailViewData(
        ShipTraitBranchNodeEntry entry,
        bool selectable,
        bool unlocked,
        int unlockedShipCount)
    {
        if (entry == null)
        {
            return new ShipTraitDetailViewData(
                "연결된 특성 노드가 없습니다.",
                string.Empty,
                string.Empty,
                "없음",
                string.Empty
            );
        }

        string description = GetEntryDescription(entry);

        return new ShipTraitDetailViewData(
            description,
            GetBranchDisplayName(entry.branchKind),
            BuildLevelText(entry),
            BuildStatusText(entry, selectable, unlocked, unlockedShipCount),
            BuildCostText(entry)
        );
    }

    private string BuildLevelText(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null || IsReinforcementNode(entry))
        {
            return string.Empty;
        }

        int currentLevel = GetNodeLevel(entry);
        int maxLevel = GetEntryMaxLevel(entry);

        return $"Lv {currentLevel} / {maxLevel}";
    }

    private string BuildNodeButtonLevelText(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null || IsReinforcementNode(entry))
        {
            return string.Empty;
        }

        int currentLevel = GetNodeLevel(entry);
        int maxLevel = GetEntryMaxLevel(entry);

        return $"{currentLevel}/{maxLevel}";
    }

    private string BuildStatusText(
        ShipTraitBranchNodeEntry entry,
        bool selectable,
        bool unlocked,
        int unlockedShipCount)
    {
        if (entry == null)
        {
            return "없음";
        }

        if (!IsBranchGateAvailable(entry.branchKind, unlockedShipCount))
        {
            return "브랜치 잠김";
        }

        if (!IsEntryGateAvailable(entry, unlockedShipCount))
        {
            return "노드 잠김";
        }

        if (!AreNodePrerequisitesMet(entry))
        {
            return "선행 특성 필요";
        }

        if (!AreAdditionalUnlockConditionsMet(entry, unlockedShipCount))
        {
            return "조건 미달";
        }

        if (unlocked)
        {
            if (!IsNodeActive(entry))
            {
                return "비활성화";
            }

            int currentLevel = GetNodeLevel(entry);
            int maxLevel = GetEntryMaxLevel(entry);

            if (currentLevel >= maxLevel)
            {
                if (IsReinforcementNode(entry))
                {
                    return "보유 중";
                }

                return maxLevel > 1 ? "최대 레벨" : "적용 완료";
            }

            return "강화 가능";
        }

        if (!selectable)
        {
            return "잠김";
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return "진행 데이터 없음";
        }

        if (!progress.CanSpend(GetEntryScrapCost(entry), GetEntryCoreShardCost(entry)))
        {
            return "재화 부족";
        }

        return "해금 가능";
    }

    private string BuildLockReason(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return "특성 데이터가 없습니다.";
        }

        if (!IsBranchGateAvailable(entry.branchKind, unlockedShipCount))
        {
            return BuildBranchGateLockReason(entry.branchKind, unlockedShipCount);
        }

        if (entry.requiredUnlockedShipCount > 0 &&
            unlockedShipCount < entry.requiredUnlockedShipCount)
        {
            return $"기체 해금 수가 부족합니다. {unlockedShipCount}/{entry.requiredUnlockedShipCount}";
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredShipId))
        {
            ShipDefinition requiredShip = FindShipDefinition(entry.requiredShipId);

            if (requiredShip == null || !IsShipUnlocked(requiredShip))
            {
                return $"필요 기체가 아직 해금되지 않았습니다. 필요 기체 ID: {entry.requiredShipId}";
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredUnlockFlag))
        {
            PermanentProgress progress = PermanentProgress.Instance;

            if (progress == null || !progress.HasUnlockFlag(entry.requiredUnlockFlag))
            {
                return $"필요 해금 플래그가 없습니다. 필요 플래그: {entry.requiredUnlockFlag}";
            }
        }

        if (entry.prerequisiteNodeIds != null)
        {
            foreach (string prerequisiteNodeId in entry.prerequisiteNodeIds)
            {
                if (string.IsNullOrWhiteSpace(prerequisiteNodeId))
                {
                    continue;
                }

                if (!IsNodeUnlocked(prerequisiteNodeId))
                {
                    return $"선행 특성이 필요합니다. 필요 특성: {GetPrerequisiteDisplayName(prerequisiteNodeId)}";
                }
            }
        }

        ShipTraitUnlockCondition unmetCondition = FindFirstUnmetAdditionalUnlockCondition(entry, unlockedShipCount);
        if (unmetCondition != null)
        {
            return $"조건이 부족합니다. {BuildUnlockConditionProgressText(unmetCondition, unlockedShipCount)}";
        }

        return "잠김";
    }

    private string BuildBranchGateLockReason(ShipTraitBranchKind branchKind, int unlockedShipCount)
    {
        ShipTraitBranchGate gate = GetBranchGate(branchKind);

        if (gate == null)
        {
            return "브랜치가 잠겨 있습니다.";
        }

        if (gate.requiredUnlockedShipCount > 0 &&
            unlockedShipCount < gate.requiredUnlockedShipCount)
        {
            return $"{GetBranchDisplayName(branchKind)} 개방 조건: 기체 해금 {unlockedShipCount}/{gate.requiredUnlockedShipCount}";
        }

        if (!string.IsNullOrWhiteSpace(gate.requiredShipId))
        {
            ShipDefinition requiredShip = FindShipDefinition(gate.requiredShipId);
            string shipName = requiredShip != null ? requiredShip.DisplayName : gate.requiredShipId;
            return $"{GetBranchDisplayName(branchKind)} 개방 조건: {shipName} 기체 해금 필요";
        }

        if (!string.IsNullOrWhiteSpace(gate.requiredUnlockFlag))
        {
            return $"{GetBranchDisplayName(branchKind)} 개방 조건: {gate.requiredUnlockFlag}";
        }

        return $"{GetBranchDisplayName(branchKind)} 브랜치가 잠겨 있습니다.";
    }

    private string BuildUnlockConditionProgressText(ShipTraitUnlockCondition condition, int unlockedShipCount)
    {
        if (condition == null)
        {
            return string.Empty;
        }

        string displayName = GetUnlockConditionDisplayName(condition);
        int currentValue = GetUnlockConditionCurrentValue(condition, unlockedShipCount);
        int requiredValue = GetConditionRequiredValue(condition);

        if (IsBinaryUnlockCondition(condition.conditionKind))
        {
            return $"{displayName}: {(currentValue >= requiredValue ? "완료" : "미완료")}";
        }

        return $"{displayName}: {currentValue}/{requiredValue}";
    }

    private string GetUnlockConditionDisplayName(ShipTraitUnlockCondition condition)
    {
        if (condition == null)
        {
            return "조건";
        }

        if (!string.IsNullOrWhiteSpace(condition.displayNameOverride))
        {
            return condition.displayNameOverride;
        }

        switch (condition.conditionKind)
        {
            case ShipTraitUnlockConditionKind.UnlockFlag:
                return string.IsNullOrWhiteSpace(condition.targetId)
                    ? "특정 조건 달성"
                    : $"조건 달성({condition.targetId})";

            case ShipTraitUnlockConditionKind.ShipUnlocked:
                if (string.IsNullOrWhiteSpace(condition.targetId))
                {
                    return "기체 해금";
                }

                ShipDefinition ship = FindShipDefinition(condition.targetId);
                string shipName = ship != null ? ship.DisplayName : condition.targetId;
                return $"기체 해금: {shipName}";

            case ShipTraitUnlockConditionKind.UnlockedShipCount:
                return "기체 해금 수";

            case ShipTraitUnlockConditionKind.BuildingLevel:
                return $"{GetBuildingDisplayName(condition.buildingType)} 레벨";

            case ShipTraitUnlockConditionKind.TraitLevel:
                return string.IsNullOrWhiteSpace(condition.targetId)
                    ? "특성 레벨"
                    : $"{GetPrerequisiteDisplayName(condition.targetId)} 레벨";

            case ShipTraitUnlockConditionKind.TotalRunCount:
                return "탐사 완료 횟수";

            case ShipTraitUnlockConditionKind.SafeReturnCount:
                return "안전 복귀 횟수";

            case ShipTraitUnlockConditionKind.EmergencyReturnCount:
                return "긴급 복귀 횟수";

            case ShipTraitUnlockConditionKind.BossDefeatCount:
                return "보스 처치 횟수";

            case ShipTraitUnlockConditionKind.TotalCollectedScrapParts:
                return "누적 스크랩 획득";

            case ShipTraitUnlockConditionKind.TotalCollectedCoreShards:
                return "누적 코어 획득";

            case ShipTraitUnlockConditionKind.TotalCommittedScrapParts:
                return "누적 스크랩 반입";

            case ShipTraitUnlockConditionKind.TotalCommittedCoreShards:
                return "누적 코어 반입";

            case ShipTraitUnlockConditionKind.OwnedScrapParts:
                return "보유 스크랩";

            case ShipTraitUnlockConditionKind.OwnedCoreShards:
                return "보유 코어";

            default:
                return "조건";
        }
    }

    private bool IsBinaryUnlockCondition(ShipTraitUnlockConditionKind conditionKind)
    {
        return conditionKind == ShipTraitUnlockConditionKind.UnlockFlag ||
               conditionKind == ShipTraitUnlockConditionKind.ShipUnlocked;
    }

    private string GetBuildingDisplayName(BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.Hangar => "격납고",
            BuildingType.EngineWorkshop => "엔진공방",
            BuildingType.WeaponLab => "화기연구소",
            BuildingType.RecoveryProcessor => "회수처리장",
            _ => buildingType.ToString()
        };
    }

    private string BuildCostText(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        int currentLevel = GetNodeLevel(entry);
        int maxLevel = GetEntryMaxLevel(entry);

        if (currentLevel >= maxLevel)
        {
            return "비용 없음";
        }

        int scrapCost = GetEntryScrapCost(entry);
        int coreCost = GetEntryCoreShardCost(entry);

        if (scrapCost <= 0 && coreCost <= 0)
        {
            return "비용 없음";
        }

        return FormatCost(scrapCost, coreCost);
    }

    private string BuildUnlockButtonLabel(
        ShipTraitBranchNodeEntry entry,
        bool selectable,
        bool unlocked)
    {
        if (entry == null)
        {
            return "특성 없음";
        }

        int currentLevel = GetNodeLevel(entry);
        int maxLevel = GetEntryMaxLevel(entry);

        if (!selectable)
        {
            return "잠김";
        }

        if (currentLevel >= maxLevel)
        {
            if (IsReinforcementNode(entry))
            {
                return "보유 중";
            }

            return maxLevel > 1 ? "최대 레벨" : "적용 완료";
        }

        if (unlocked)
        {
            return $"강화 {currentLevel}/{maxLevel}";
        }

        return IsReinforcementNode(entry) ? "장비 해금" : "해금";
    }

    private string BuildActivationToggleButtonLabel(
        ShipTraitBranchNodeEntry entry,
        bool unlocked,
        bool active)
    {
        if (entry == null)
        {
            return "비활성화";
        }

        if (!unlocked)
        {
            return "해금 후 사용";
        }

        return active ? "비활성화" : "활성화";
    }

    private string FormatCost(int scrapCost, int coreCost)
    {
        if (scrapCost <= 0 && coreCost <= 0)
        {
            return "비용 없음";
        }

        StringBuilder builder = new StringBuilder();

        if (scrapCost > 0)
        {
            builder.AppendLine($"스크랩 {scrapCost}");
        }

        if (coreCost > 0)
        {
            builder.AppendLine($"코어 {coreCost}");
        }

        return builder.ToString().TrimEnd();
    }

    private int CountUnlockedShips()
    {
        List<ShipDefinition> ships = CollectShipDefinitions();

        if (ships.Count == 0)
        {
            return 1;
        }

        int count = 0;
        HashSet<string> countedIds = new HashSet<string>();

        foreach (ShipDefinition ship in ships)
        {
            if (ship == null)
            {
                continue;
            }

            if (!countedIds.Add(ship.ShipId))
            {
                continue;
            }

            if (IsShipUnlocked(ship))
            {
                count++;
            }
        }

        return Mathf.Max(0, count);
    }

    private List<ShipDefinition> CollectShipDefinitions()
    {
        List<ShipDefinition> result = new List<ShipDefinition>();
        HashSet<string> ids = new HashSet<string>();

        AddShipDefinitions(result, ids, shipDefinitions);

        if (settlementController != null && settlementController.ShipDefinitions != null)
        {
            AddShipDefinitions(result, ids, settlementController.ShipDefinitions);
        }

        return result;
    }

    private void AddShipDefinitions(
        List<ShipDefinition> result,
        HashSet<string> ids,
        IEnumerable<ShipDefinition> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (ShipDefinition ship in source)
        {
            if (ship == null)
            {
                continue;
            }

            if (ids.Add(ship.ShipId))
            {
                result.Add(ship);
            }
        }
    }

    private bool IsShipUnlocked(ShipDefinition ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (settlementController != null)
        {
            return settlementController.IsShipUnlocked(ship);
        }

        if (ship.UnlockedByDefault)
        {
            return true;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        return progress != null && progress.HasUnlockFlag(ship.UnlockFlag);
    }

    private ShipDefinition FindShipDefinition(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        foreach (ShipDefinition ship in shipDefinitions)
        {
            if (ship != null && ship.ShipId == shipId)
            {
                return ship;
            }
        }

        if (settlementController != null && settlementController.ShipDefinitions != null)
        {
            foreach (ShipDefinition ship in settlementController.ShipDefinitions)
            {
                if (ship != null && ship.ShipId == shipId)
                {
                    return ship;
                }
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindEntry(ShipTraitBranchKind branchKind, string nodeId)
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry == null)
            {
                continue;
            }

            if (entry.branchKind != branchKind)
            {
                continue;
            }

            if (GetEntryNodeId(entry) == nodeId)
            {
                return entry;
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindEntryByNodeId(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry != null && GetEntryNodeId(entry) == nodeId)
            {
                return entry;
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindFirstEntry(ShipTraitBranchKind branchKind)
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry != null && entry.branchKind == branchKind)
            {
                return entry;
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindFirstEntry()
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry != null)
            {
                return entry;
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindFirstSelectableEntry(ShipTraitBranchKind branchKind, int unlockedShipCount)
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry == null)
            {
                continue;
            }

            if (entry.branchKind != branchKind)
            {
                continue;
            }

            if (IsNodeSelectable(entry, unlockedShipCount))
            {
                return entry;
            }
        }

        return null;
    }

    private ShipTraitBranchNodeEntry FindFirstSelectableEntry(int unlockedShipCount)
    {
        foreach (ShipTraitBranchNodeEntry entry in branchNodes)
        {
            if (entry != null && IsNodeSelectable(entry, unlockedShipCount))
            {
                return entry;
            }
        }

        return null;
    }

    private string GetEntryNodeId(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (entry.traitDefinition != null)
        {
            return entry.traitDefinition.TraitId;
        }

        if (entry.reinforcementDefinition != null)
        {
            return entry.reinforcementDefinition.EquipmentId;
        }

        if (!string.IsNullOrWhiteSpace(entry.nodeId))
        {
            return entry.nodeId;
        }

        if (entry.nodeButton != null && !string.IsNullOrWhiteSpace(entry.nodeButton.NodeId))
        {
            return entry.nodeButton.NodeId;
        }

        int index = branchNodes.IndexOf(entry);
        return $"{entry.branchKind}_{index}";
    }

    private string GetDisplayName(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return "특성";
        }

        if (entry.manualOverrideDefinitionFields && !string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        if (entry.traitDefinition != null)
        {
            return entry.traitDefinition.DisplayName;
        }

        if (entry.reinforcementDefinition != null)
        {
            return entry.reinforcementDefinition.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return GetBranchDisplayName(entry.branchKind);
    }

    private string GetPrerequisiteDisplayName(string nodeId)
    {
        ShipTraitBranchNodeEntry entry = FindEntryByNodeId(nodeId);
        return entry != null ? GetDisplayName(entry) : nodeId;
    }

    private string GetBranchDisplayName(ShipTraitBranchKind branchKind)
    {
        return branchKind switch
        {
            ShipTraitBranchKind.Shared => "공용",
            ShipTraitBranchKind.MachineGun => "스위퍼",
            ShipTraitBranchKind.Sniper => "랜서",
            ShipTraitBranchKind.Shotgun => "브리처",
            _ => "특성"
        };
    }


    private string GetDefaultDescription(ShipTraitBranchKind branchKind)
    {
        return branchKind switch
        {
            ShipTraitBranchKind.Shared =>
                "모든 기체와 무기 트리에 공통으로 적용되는 특성 그룹입니다.",

            ShipTraitBranchKind.MachineGun =>
                "스위퍼 전용 특성 그룹입니다. 지속 사격과 유도 보정을 강화합니다.",

            ShipTraitBranchKind.Sniper =>
                "랜서 전용 특성 그룹입니다. 차징, 관통, 장거리 교전을 강화합니다.",

            ShipTraitBranchKind.Shotgun =>
                "브리처 전용 특성 그룹입니다. 돌입, 산탄, 근거리 생존을 강화합니다.",

            _ => "특성 그룹입니다."
        };
    }

    private void SetIcon(Sprite icon)
    {
        if (selectedIconImage == null)
        {
            return;
        }

        selectedIconImage.sprite = icon;
        selectedIconImage.enabled = icon != null;
    }

    private void ClearDetailTexts(string bodyMessage)
    {
        SetText(descriptionText, bodyMessage);
        SetText(branchText, string.Empty);
        SetText(levelText, string.Empty);
        SetText(statusText, string.Empty);
        SetText(costText, string.Empty);
        SetCostIconVisuals(null);
    }

    private void SetCostIconVisuals(ShipTraitBranchNodeEntry entry)
    {
        bool showScrap = false;
        bool showCore = false;

        if (entry != null)
        {
            int currentLevel = GetNodeLevel(entry);
            int maxLevel = GetEntryMaxLevel(entry);

            if (currentLevel < maxLevel)
            {
                showScrap = GetEntryScrapCost(entry) > 0;
                showCore = GetEntryCoreShardCost(entry) > 0;
            }
        }

        SetCostIconActive(scrapCostIconRoot, scrapCostIconImage, scrapCostIconSprite, showScrap);
        SetCostIconActive(coreShardCostIconRoot, coreShardCostIconImage, coreShardCostIconSprite, showCore);
    }

    private void SetCostIconActive(GameObject root, Image image, Sprite sprite, bool active)
    {
        if (image == null && root != null)
        {
            image = root.GetComponent<Image>();
        }

        GameObject targetObject = root != null
            ? root
            : image != null ? image.gameObject : null;

        if (targetObject != null)
        {
            targetObject.SetActive(active || !hideCostIconsWhenFree);
        }

        if (image != null)
        {

            image.enabled = (active || !hideCostIconsWhenFree) && image.sprite != null;
        }
    }


    private void SetDetailLockVisual(bool locked)
    {
        if (detailLockImage == null)
        {
            return;
        }

        if (locked && lockedDetailSprite != null)
        {
            detailLockImage.sprite = lockedDetailSprite;
        }
        else if (!locked && unlockedDetailSprite != null)
        {
            detailLockImage.sprite = unlockedDetailSprite;
        }

        bool shouldShow = locked || !hideLockImageWhenAvailable;
        bool hasSprite = detailLockImage.sprite != null;

        detailLockImage.enabled = shouldShow && hasSprite;
        detailLockImage.gameObject.SetActive(shouldShow && hasSprite);
    }

    private void SetUnlockButton(bool interactable, string label)
    {
        if (unlockButton != null)
        {
            unlockButton.interactable = interactable;
        }

        SetText(unlockButtonLabelText, label);
    }

    private void SetActivationToggleButton(bool interactable, string label, bool visible)
    {
        if (traitActivationToggleButton != null)
        {
            traitActivationToggleButton.interactable = interactable;
            traitActivationToggleButton.gameObject.SetActive(visible);
        }

        SetText(traitActivationToggleButtonLabelText, label);
    }

    private void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void SetMessage(string message)
    {
        SetText(messageText, message);
        Debug.Log(message, this);
    }

    private void HandleUnlockButtonClick()
    {
        int levelBefore = GetSelectedNodeLevel();
        bool success = TryUnlockSelectedTrait();

        if (!success)
        {
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        AudioManager.Play(levelBefore <= 0
            ? SoundEventIds.UiUnlock
            : SoundEventIds.UiUpgradeSuccess);
    }

    private void HandleActivationToggleButtonClick()
    {
        bool wasActive = IsSelectedNodeActive();
        bool success = TryToggleSelectedTraitActive();

        if (!success)
        {
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        AudioManager.Play(wasActive
            ? SoundEventIds.UiDeactivate
            : SoundEventIds.UiActivate);
    }

    private void ConfigureActionButtonSound(Button targetButton)
    {
        if (targetButton == null)
        {
            return;
        }

        UISoundButton[] soundButtons = targetButton.GetComponents<UISoundButton>();

        if (soundButtons == null || soundButtons.Length == 0)
        {
            soundButtons = new[] { targetButton.gameObject.AddComponent<UISoundButton>() };
        }

        for (int i = 0; i < soundButtons.Length; i++)
        {
            UISoundButton soundButton = soundButtons[i];

            if (soundButton == null)
            {
                continue;
            }

            // 활성/비활성 결과음은 HandleActivationToggleButtonClick 한 곳에서만 재생한다.
            soundButton.SetClickSoundEnabled(false);
            soundButton.SetHoverSoundEnabled(false);
            soundButton.SetDisabledClickSoundEnabled(true);
            soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
        }
    }

    private int GetSelectedNodeLevel()
    {
        if (PermanentProgress.Instance == null)
        {
            return 0;
        }

        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);
        if (entry == null)
        {
            return 0;
        }

        string nodeId = GetEntryNodeId(entry);
        return string.IsNullOrWhiteSpace(nodeId)
            ? 0
            : PermanentProgress.Instance.GetTraitLevel(nodeId);
    }

    private bool IsSelectedNodeActive()
    {
        if (PermanentProgress.Instance == null)
        {
            return false;
        }

        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);
        if (entry == null)
        {
            return false;
        }

        string nodeId = GetEntryNodeId(entry);
        return !string.IsNullOrWhiteSpace(nodeId) && PermanentProgress.Instance.IsTraitActive(nodeId);
    }

    private void HandleExternalChanged()
    {
        RefreshPanel();
    }
}
