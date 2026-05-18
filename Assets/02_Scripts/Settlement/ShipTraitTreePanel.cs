using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ShipTraitBranchNodeEntry
{
    public ShipTraitBranchKind branchKind;

    [Header("Node Identity")]
    public string nodeId;
    public ShipTraitNodeButton nodeButton;

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

    [Header("Tree Prerequisites")]
    public List<string> prerequisiteNodeIds = new List<string>();

    [Header("Option")]
    public bool hideWhenLocked;
}

public class ShipTraitTreePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;

    [Header("Branch Panel Objects")]
    [SerializeField] private GameObject sharedPanelObject;
    [SerializeField] private GameObject machineGunPanelObject;
    [SerializeField] private GameObject sniperPanelObject;
    [SerializeField] private GameObject shotgunPanelObject;
    [SerializeField] private bool deactivateBranchPanelWhenLocked = true;

    [Header("Static Nodes In ScrollView")]
    [SerializeField] private List<ShipTraitBranchNodeEntry> branchNodes = new List<ShipTraitBranchNodeEntry>();

    [Header("Ship Catalog")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();

    [Header("Target Ship Optional")]
    [SerializeField] private string targetShipIdOverride;

    [Header("Detail UI")]
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Unlock Button")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private TextMeshProUGUI unlockButtonLabelText;

    private ShipTraitBranchKind selectedBranch = ShipTraitBranchKind.Shared;
    private string selectedNodeId;
    private bool hasSelection;

    public Button UnlockButton => unlockButton;
    public ShipTraitBranchKind SelectedBranch => selectedBranch;
    public string SelectedNodeId => selectedNodeId;
    public bool HasSelection => hasSelection;

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

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        BindNodes();

        if (unlockButton != null)
        {
            unlockButton.onClick.AddListener(HandleUnlockButtonClick);
        }
    }

    private void OnEnable()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        if (settlementController != null)
        {
            settlementController.Changed += HandleExternalChanged;
        }

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed += HandleExternalChanged;
        }

        RefreshPanel();
    }

    private void Start()
    {
        RefreshPanel();
    }

    private void OnDisable()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= HandleExternalChanged;
        }

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed -= HandleExternalChanged;
        }
    }

    private void OnDestroy()
    {
        if (unlockButton != null)
        {
            unlockButton.onClick.RemoveListener(HandleUnlockButtonClick);
        }
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
        ShipTraitBranchNodeEntry firstEntry = FindFirstSelectableEntry(branchKind, CountUnlockedShips());

        if (firstEntry == null)
        {
            firstEntry = FindFirstEntry(branchKind);
        }

        if (firstEntry == null)
        {
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
        BindNodes();

        int unlockedShipCount = CountUnlockedShips();

        RefreshBranchPanelObjects(unlockedShipCount);
        EnsureValidSelection(unlockedShipCount);
        RefreshNodeStates(unlockedShipCount);
        RefreshDetail(unlockedShipCount);
    }

    public bool CanUnlockSelectedTrait()
    {
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            return false;
        }

        return CanUnlockEntry(entry, CountUnlockedShips());
    }

    public bool TryUnlockSelectedTrait()
    {
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            SetMessage("해금할 특성 노드를 선택하세요.");
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

        int scrapCost = Mathf.Max(0, entry.scrapCost);
        int coreCost = Mathf.Max(0, entry.coreShardCost);

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족. 필요: {FormatCost(scrapCost, coreCost)}");
            RefreshPanel();
            return false;
        }

        progress.SetTraitLevel(nodeId, currentLevel + 1);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }

        SetMessage($"{GetDisplayName(entry)} 해금 완료. {currentLevel + 1}/{maxLevel}");
        RefreshPanel();
        return true;
    }

    private void BindNodes()
    {
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
                entry.icon
            );
        }
    }

    private void RefreshBranchPanelObjects(int unlockedShipCount)
    {
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

    private void SetBranchPanelActive(ShipTraitBranchKind branchKind, bool available)
    {
        GameObject panelObject = GetBranchPanelObject(branchKind);

        if (panelObject == null)
        {
            return;
        }

        if (deactivateBranchPanelWhenLocked)
        {
            panelObject.SetActive(available);
        }
        else
        {
            panelObject.SetActive(true);
        }
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

    private bool IsBranchGateAvailable(ShipTraitBranchKind branchKind, int unlockedShipCount)
    {
        ShipTraitBranchNodeEntry gateEntry = FindFirstEntry(branchKind);

        if (gateEntry == null)
        {
            return false;
        }

        return IsEntryGateAvailable(gateEntry, unlockedShipCount);
    }

    private void EnsureValidSelection(int unlockedShipCount)
    {
        ShipTraitBranchNodeEntry selectedEntry = FindEntry(selectedBranch, selectedNodeId);

        if (!hasSelection ||
            selectedEntry == null ||
            !IsNodeSelectable(selectedEntry, unlockedShipCount))
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
            bool selected =
                hasSelection &&
                selectedBranch == entry.branchKind &&
                selectedNodeId == GetEntryNodeId(entry);

            if (entry.hideWhenLocked)
            {
                entry.nodeButton.gameObject.SetActive(selectable);
            }
            else
            {
                entry.nodeButton.gameObject.SetActive(true);
            }

            entry.nodeButton.SetVisualState(selectable, unlocked, selected);
        }
    }

    private void RefreshDetail(int unlockedShipCount)
    {
        ShipTraitBranchNodeEntry entry = FindEntry(selectedBranch, selectedNodeId);

        if (entry == null)
        {
            SetIcon(null);
            SetText(titleText, "특성 없음");
            SetText(bodyText, "연결된 특성 노드가 없습니다.");
            SetText(costText, string.Empty);
            SetText(statusText, string.Empty);
            SetUnlockButton(false, "특성 없음");
            return;
        }

        bool selectable = IsNodeSelectable(entry, unlockedShipCount);
        bool unlocked = IsNodeUnlocked(entry);
        bool canUnlock = CanUnlockEntry(entry, unlockedShipCount);

        SetIcon(entry.icon);
        SetText(titleText, GetDisplayName(entry));
        SetText(bodyText, BuildDetailText(entry, selectable, unlocked, unlockedShipCount));
        SetText(costText, BuildCostText(entry));
        SetText(statusText, BuildStatusText(entry, selectable, unlocked, unlockedShipCount));
        SetUnlockButton(canUnlock, BuildUnlockButtonLabel(entry, selectable, unlocked));
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
            Mathf.Max(0, entry.scrapCost),
            Mathf.Max(0, entry.coreShardCost)
        );
    }

    private bool IsNodeSelectable(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (!IsEntryGateAvailable(entry, unlockedShipCount))
        {
            return false;
        }

        if (!AreNodePrerequisitesMet(entry))
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

    private int GetNodeLevel(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null || PermanentProgress.Instance == null)
        {
            return 0;
        }

        return PermanentProgress.Instance.GetTraitLevel(GetEntryNodeId(entry));
    }

    private int GetEntryMaxLevel(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return 1;
        }

        return Mathf.Max(1, entry.maxLevel);
    }

    private string BuildDetailText(
        ShipTraitBranchNodeEntry entry,
        bool selectable,
        bool unlocked,
        int unlockedShipCount)
    {
        StringBuilder builder = new StringBuilder();

        string description = !string.IsNullOrWhiteSpace(entry.description)
            ? entry.description
            : GetDefaultDescription(entry.branchKind);

        int currentLevel = GetNodeLevel(entry);
        int maxLevel = GetEntryMaxLevel(entry);

        builder.AppendLine(description);
        builder.AppendLine();

        builder.AppendLine($"노드 ID: {GetEntryNodeId(entry)}");
        builder.AppendLine($"브랜치: {GetBranchDisplayName(entry.branchKind)}");
        builder.AppendLine($"레벨: {currentLevel}/{maxLevel}");
        builder.AppendLine($"상태: {BuildStatusText(entry, selectable, unlocked, unlockedShipCount)}");

        if (entry.requiredUnlockedShipCount > 0)
        {
            builder.AppendLine($"필요 해금 기체 수: {entry.requiredUnlockedShipCount}");
            builder.AppendLine($"현재 해금 기체 수: {unlockedShipCount}");
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredShipId))
        {
            ShipDefinition requiredShip = FindShipDefinition(entry.requiredShipId);
            string shipName = requiredShip != null ? requiredShip.DisplayName : entry.requiredShipId;
            bool shipUnlocked = requiredShip != null && IsShipUnlocked(requiredShip);

            builder.AppendLine($"필요 기체: {shipName} / {(shipUnlocked ? "해금됨" : "잠김")}");
        }

        if (!string.IsNullOrWhiteSpace(entry.requiredUnlockFlag))
        {
            bool flagUnlocked = PermanentProgress.Instance != null &&
                                PermanentProgress.Instance.HasUnlockFlag(entry.requiredUnlockFlag);

            builder.AppendLine($"필요 플래그: {entry.requiredUnlockFlag} / {(flagUnlocked ? "보유" : "미보유")}");
        }

        if (entry.prerequisiteNodeIds != null && entry.prerequisiteNodeIds.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("선행 노드:");

            foreach (string prerequisiteNodeId in entry.prerequisiteNodeIds)
            {
                if (string.IsNullOrWhiteSpace(prerequisiteNodeId))
                {
                    continue;
                }

                builder.AppendLine($"- {prerequisiteNodeId}: {(IsNodeUnlocked(prerequisiteNodeId) ? "해금됨" : "잠김")}");
            }
        }

        if (!selectable)
        {
            builder.AppendLine();
            builder.AppendLine(BuildLockReason(entry, unlockedShipCount));
        }

        return builder.ToString().TrimEnd();
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

        if (!IsEntryGateAvailable(entry, unlockedShipCount))
        {
            return "브랜치 잠김";
        }

        if (!AreNodePrerequisitesMet(entry))
        {
            return "선행 특성 필요";
        }

        if (unlocked)
        {
            int currentLevel = GetNodeLevel(entry);
            int maxLevel = GetEntryMaxLevel(entry);

            if (currentLevel >= maxLevel)
            {
                return "최대 레벨";
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

        if (!progress.CanSpend(Mathf.Max(0, entry.scrapCost), Mathf.Max(0, entry.coreShardCost)))
        {
            return "재화 부족";
        }

        return "해금 가능";
    }

    private string BuildLockReason(ShipTraitBranchNodeEntry entry, int unlockedShipCount)
    {
        if (entry == null)
        {
            return "노드 데이터가 없습니다.";
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
                    return $"선행 특성이 필요합니다. 필요 노드: {prerequisiteNodeId}";
                }
            }
        }

        return "잠김";
    }

    private string BuildCostText(ShipTraitBranchNodeEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        int scrapCost = Mathf.Max(0, entry.scrapCost);
        int coreCost = Mathf.Max(0, entry.coreShardCost);

        if (scrapCost <= 0 && coreCost <= 0)
        {
            return "비용 없음";
        }

        return $"필요 재화: {FormatCost(scrapCost, coreCost)}";
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
            return "최대 레벨";
        }

        if (unlocked)
        {
            return $"강화 {currentLevel}/{maxLevel}";
        }

        return "해금";
    }

    private string FormatCost(int scrapCost, int coreCost)
    {
        if (scrapCost <= 0 && coreCost <= 0)
        {
            return "없음";
        }

        StringBuilder builder = new StringBuilder();

        if (scrapCost > 0)
        {
            builder.Append($"스크랩 {scrapCost}");
        }

        if (coreCost > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append($"코어 {coreCost}");
        }

        return builder.ToString();
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

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return GetBranchDisplayName(entry.branchKind);
    }

    private string GetBranchDisplayName(ShipTraitBranchKind branchKind)
    {
        return branchKind switch
        {
            ShipTraitBranchKind.Shared => "공유 특성",
            ShipTraitBranchKind.MachineGun => "기관총 특성",
            ShipTraitBranchKind.Sniper => "스나 특성",
            ShipTraitBranchKind.Shotgun => "샷건 특성",
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
                "기관총 트리 전용 특성 그룹입니다. 지속 사격과 유도 보정을 강화합니다.",

            ShipTraitBranchKind.Sniper =>
                "스나이퍼 트리 전용 특성 그룹입니다. 차징, 관통, 장거리 교전을 강화합니다.",

            ShipTraitBranchKind.Shotgun =>
                "샷건 트리 전용 특성 그룹입니다. 돌입, 산탄, 근거리 생존을 강화합니다.",

            _ => "특성 그룹입니다."
        };
    }

    private string ResolveTargetShipId()
    {
        if (!string.IsNullOrWhiteSpace(targetShipIdOverride))
        {
            return targetShipIdOverride;
        }

        if (settlementController != null)
        {
            return settlementController.SelectedShipId;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.SelectedShipId;
        }

        return "basic_ship";
    }

    private void SetIcon(Sprite icon)
    {
        if (selectedIconImage == null)
        {
            return;
        }

        selectedIconImage.sprite = icon;
        selectedIconImage.enabled = icon != null;
        selectedIconImage.preserveAspect = true;
    }

    private void SetUnlockButton(bool interactable, string label)
    {
        if (unlockButton != null)
        {
            unlockButton.interactable = interactable;
        }

        SetText(unlockButtonLabelText, label);
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
        TryUnlockSelectedTrait();
    }

    private void HandleExternalChanged()
    {
        RefreshPanel();
    }
}