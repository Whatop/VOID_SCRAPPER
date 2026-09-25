using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SettlementRestorationState
{
    Locked,
    Available,
    Complete
}

public sealed class SettlementRestorationEvaluation
{
    public BuildingType BuildingType { get; }
    public SettlementRestorationState State { get; }
    public BossStoryPart RequiredBossStoryPart { get; }
    public bool HasRequiredBossStoryPart { get; }
    public int RequiredSynchronizationStage { get; }
    public int CurrentSynchronizationStage { get; }
    public bool RequiresPriorRestoration { get; }
    public BuildingType PriorRestorationBuilding { get; }
    public bool HasPriorRestoration { get; }
    public string RequiredUnlockFlag { get; }
    public bool HasRequiredUnlockFlag { get; }

    public bool IsComplete => State == SettlementRestorationState.Complete;
    public bool CanComplete => State == SettlementRestorationState.Available;

    public SettlementRestorationEvaluation(
        BuildingType buildingType,
        SettlementRestorationState state,
        BossStoryPart requiredBossStoryPart,
        bool hasRequiredBossStoryPart,
        int requiredSynchronizationStage,
        int currentSynchronizationStage,
        bool requiresPriorRestoration,
        BuildingType priorRestorationBuilding,
        bool hasPriorRestoration,
        string requiredUnlockFlag,
        bool hasRequiredUnlockFlag)
    {
        BuildingType = buildingType;
        State = state;
        RequiredBossStoryPart = requiredBossStoryPart;
        HasRequiredBossStoryPart = hasRequiredBossStoryPart;
        RequiredSynchronizationStage = Mathf.Max(0, requiredSynchronizationStage);
        CurrentSynchronizationStage = Mathf.Max(0, currentSynchronizationStage);
        RequiresPriorRestoration = requiresPriorRestoration;
        PriorRestorationBuilding = priorRestorationBuilding;
        HasPriorRestoration = hasPriorRestoration;
        RequiredUnlockFlag = requiredUnlockFlag ?? string.Empty;
        HasRequiredUnlockFlag = hasRequiredUnlockFlag;
    }
}

public sealed class SettlementRestorationViewData
{
    public string Title { get; }
    public string Description { get; }
    public string StateText { get; }
    public string RequirementText { get; }
    public string CompletionResultText { get; }
    public string ActionLabel { get; }
    public bool IsComplete { get; }
    public bool CanComplete { get; }

    public SettlementRestorationViewData(
        string title,
        string description,
        string stateText,
        string requirementText,
        string completionResultText,
        string actionLabel,
        bool isComplete,
        bool canComplete)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "정착지 복구" : title;
        Description = string.IsNullOrWhiteSpace(description) ? "복구 프로젝트 설명이 없습니다." : description;
        StateText = stateText ?? string.Empty;
        RequirementText = requirementText ?? string.Empty;
        CompletionResultText = completionResultText ?? string.Empty;
        ActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "조건 미충족" : actionLabel;
        IsComplete = isComplete;
        CanComplete = canComplete;
    }

    public string BuildFallbackBodyText()
    {
        StringBuilder builder = new StringBuilder();

        AppendSection(builder, Description);
        AppendSection(builder, StateText);
        AppendSection(builder, RequirementText);
        AppendSection(builder, CompletionResultText);

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

public class SettlementController : MonoBehaviour, IMainDamagedAccessKeyQuestStartAuthority
{
    public static SettlementController Instance { get; private set; }

    [Header("Selection Defaults")]
    [SerializeField] private WeaponTreeType defaultWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string defaultShipId = "basic_ship";

    [Header("Catalog")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private List<BuildingDefinition> buildingDefinitions = new List<BuildingDefinition>();
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Legacy Building Upgrade Data (Ignored)")]
    [Tooltip("Retained only so existing scene serialization remains compatible. Restoration does not spend currency.")]
    [SerializeField] private int fallbackBuildingMaxLevel = 3;
    [SerializeField] private int fallbackRepairScrapCost = 5;
    [SerializeField] private int fallbackUpgradeBaseScrapCost = 10;
    [SerializeField] private int fallbackUpgradeScrapCostStep = 8;
    [SerializeField] private int fallbackCoreCostFromLevel = 3;

    [Header("Fallback Permanent Trait Cost")]
    [SerializeField] private int traitBaseScrapCost = 12;
    [SerializeField] private int traitScrapCostStep = 8;
    [SerializeField] private int weaponSpecificTraitScrapBonus = 4;
    [SerializeField] private int traitCoreCostFromLevel = 3;
    [SerializeField] private bool requireSelectedWeaponForWeaponSpecificTraits;

    private WeaponTreeType selectedWeaponTree;
    private string selectedShipId;
    private int previewShipIndex;

    public WeaponTreeType SelectedWeaponTree => selectedWeaponTree;
    public string SelectedShipId => string.IsNullOrWhiteSpace(selectedShipId) ? ResolveDefaultShipId() : selectedShipId;
    public int PreviewShipIndex => previewShipIndex;
    public ShipDefinition PreviewShip => GetShipByIndex(previewShipIndex);
    public IReadOnlyList<ShipDefinition> ShipDefinitions => shipDefinitions;
    public IReadOnlyList<BuildingDefinition> BuildingDefinitions => buildingDefinitions;
    public IReadOnlyList<TraitDefinition> TraitDefinitions => traitDefinitions;
    public MainDamagedAccessKeyQuestState DamagedAccessKeyQuestState =>
        PermanentProgress.Instance != null
            ? PermanentProgress.Instance.DamagedAccessKeyQuestState
            : MainDamagedAccessKeyQuestState.Invalid;
    public int DamagedAccessKeyCollectedPartCount =>
        PermanentProgress.Instance != null
            ? PermanentProgress.Instance.DamagedAccessKeyCollectedPartCount
            : 0;
    public int DamagedAccessKeyRequiredPartCount =>
        MainDamagedAccessKeyQuestIds.RequiredPartCount;

    public string LastMessage { get; private set; } = "정착지에 도착했다.";

    public event Action Changed;
    public event Action<BuildingType> RestorationCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SettlementController가 중복으로 존재합니다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RemoveNullCatalogEntries();

        selectedWeaponTree = defaultWeaponTree;
        selectedShipId = ResolveDefaultShipId();
        previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));
    }

    private void OnEnable()
    {
        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed += HandleProgressChanged;
        }
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Settlement);
        }

        LoadSelectionFromProgress();
        NotifyChanged();
        // Recovery for older eligible saves or an interrupted/failed post-introduction save.
        TryGrantEquipmentStarterMaterials();
    }

    private void OnDisable()
    {
        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed -= HandleProgressChanged;
        }
    }

    public void LoadSelectionFromProgress()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            selectedWeaponTree = defaultWeaponTree;
            selectedShipId = ResolveDefaultShipId();
            previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));
            return;
        }

        if (TryInitializeFirstSettlementMachineGunSelection(progress))
        {
            return;
        }

        selectedWeaponTree = progress.LastSelectedWeaponTree;
        selectedShipId = progress.SelectedShipId;

        if (string.IsNullOrWhiteSpace(selectedShipId))
        {
            selectedShipId = ResolveDefaultShipId();
        }

        int selectedIndex = FindShipIndex(selectedShipId);
        previewShipIndex = selectedIndex >= 0 ? selectedIndex : Mathf.Max(0, FindShipIndex(ResolveDefaultShipId()));
    }

    private bool TryInitializeFirstSettlementMachineGunSelection(PermanentProgress progress)
    {
        if (!progress.HasUnlockFlag(StoryProgressionIds.FirstSettlementPendingFlag) ||
            progress.HasUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag))
        {
            return false;
        }

        ShipDefinition machineGunShip = null;
        for (int i = 0; i < shipDefinitions.Count; i++)
        {
            ShipDefinition candidate = shipDefinitions[i];
            if (candidate == null ||
                candidate.DefaultWeaponTree != WeaponTreeType.MachineGun ||
                !IsShipUnlocked(candidate))
            {
                continue;
            }

            machineGunShip = candidate;
            if (candidate.UnlockedByDefault)
            {
                break;
            }
        }

        if (machineGunShip == null)
        {
            Debug.LogWarning(
                "First Settlement arrival could not find an unlocked Machine Gun ShipDefinition.",
                this
            );
            return false;
        }

        selectedShipId = machineGunShip.ShipId;
        selectedWeaponTree = WeaponTreeType.MachineGun;
        previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));
        progress.SetSelectedShipId(selectedShipId);
        progress.SetLastSelectedWeaponTree(selectedWeaponTree);
        SaveProgress();
        return true;
    }

    public bool TryStartDamagedAccessKeyQuest()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saveManager = SaveManager.Instance;

        if (progress == null || saveManager == null ||
            progress.DamagedAccessKeyQuestState !=
            MainDamagedAccessKeyQuestState.NotStarted)
        {
            return false;
        }

        if (!progress.TryStartDamagedAccessKeyQuest())
        {
            return false;
        }

        saveManager.Save(progress);
        ShowLocalizedMessage(MainDamagedAccessKeyQuestIds.StartedNotificationTextKey);
        return true;
    }

    public bool TryCompleteFirstSettlementStory()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saveManager = SaveManager.Instance;

        if (progress == null || saveManager == null ||
            progress.DamagedAccessKeyQuestState ==
            MainDamagedAccessKeyQuestState.Invalid)
        {
            return false;
        }

        bool questStarted = false;
        if (progress.DamagedAccessKeyQuestState ==
            MainDamagedAccessKeyQuestState.NotStarted)
        {
            questStarted = progress.TryStartDamagedAccessKeyQuest();
            if (!questStarted)
            {
                return false;
            }
        }

        bool storyWasComplete = progress.HasUnlockFlag(
            StoryProgressionIds.FirstSettlementCompleteFlag);
        if (!storyWasComplete)
        {
            progress.AddUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag);
        }

        bool routeAuthorized = progress.TryAuthorizeAnalyzedRegion();
        bool finalComponentAnalyzed = progress.TryCompleteFinalComponentAnalysis();
        if (questStarted || !storyWasComplete || routeAuthorized || finalComponentAnalyzed)
        {
            saveManager.Save(progress);
        }

        if (questStarted)
        {
            ShowLocalizedMessage(
                MainDamagedAccessKeyQuestIds.StartedNotificationTextKey);
        }

        if (routeAuthorized)
        {
            ShowLocalizedMessage(progress.HighestUnlockedDepth == ExpeditionDepth.DeepZone1
                ? "system.campaign.route_authorized.region_2"
                : "system.campaign.route_authorized.region_3");
        }

        TryGrantEquipmentStarterMaterials();
        return progress.DamagedAccessKeyQuestState !=
                   MainDamagedAccessKeyQuestState.NotStarted &&
               progress.HasUnlockFlag(
                   StoryProgressionIds.FirstSettlementCompleteFlag);
    }

    private void TryGrantEquipmentStarterMaterials()
    {
        if (PermanentProgress.Instance != null && PermanentProgress.Instance.TryGrantEquipmentStarterMaterials())
            ShowLocalizedMessage("system.settlement.equipment.starter_materials");
    }

    public bool CanRestoreDamagedAccessKey()
    {
        return PermanentProgress.Instance != null &&
               PermanentProgress.Instance.DamagedAccessKeyQuestState ==
               MainDamagedAccessKeyQuestState.ReadyToRestore;
    }

    public bool TryRestoreDamagedAccessKey()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        SaveManager saveManager = SaveManager.Instance;

        if (progress == null || saveManager == null ||
            !progress.TryRestoreDamagedAccessKey())
        {
            return false;
        }

        saveManager.Save(progress);
        ShowLocalizedMessage(
            MainDamagedAccessKeyQuestIds.RestoredNotificationTextKey);
        return true;
    }

    public SettlementRestorationViewData BuildDamagedAccessKeyRestorationViewData()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        MainDamagedAccessKeyQuestState state = progress != null
            ? progress.DamagedAccessKeyQuestState
            : MainDamagedAccessKeyQuestState.Invalid;
        bool complete = state == MainDamagedAccessKeyQuestState.Completed;
        bool ready = state == MainDamagedAccessKeyQuestState.ReadyToRestore;

        return new SettlementRestorationViewData(
            GetLocalizedText(MainDamagedAccessKeyQuestIds.TitleTextKey),
            GetLocalizedText(
                MainDamagedAccessKeyQuestIds.RestorationDescriptionTextKey),
            GetLocalizedText(
                complete
                    ? MainDamagedAccessKeyQuestIds.RestorationCompletedTextKey
                    : MainDamagedAccessKeyQuestIds.RestorationReadyTextKey),
            FormatDamagedAccessKeyObjective(progress),
            GetLocalizedText(
                MainDamagedAccessKeyQuestIds.RestorationCompletedTextKey),
            GetLocalizedText(
                MainDamagedAccessKeyQuestIds.RestorationActionTextKey),
            complete,
            ready);
    }

    public void SelectWeaponTree(WeaponTreeType weaponTreeType)
    {
        selectedWeaponTree = weaponTreeType;

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetLastSelectedWeaponTree(weaponTreeType);
        }

        SaveProgress();
        SetMessage($"출격 무기 선택: {GetWeaponDisplayName(weaponTreeType)}");
    }

    public void MovePreviewShipNext()
    {
        if (shipDefinitions.Count == 0)
        {
            SetMessage("표시할 기체 데이터가 없습니다.");
            return;
        }

        previewShipIndex = (previewShipIndex + 1) % shipDefinitions.Count;
        SetMessage($"기체 확인: {PreviewShip.DisplayName}");
        NotifyChanged();
    }

    public void MovePreviewShipPrevious()
    {
        if (shipDefinitions.Count == 0)
        {
            SetMessage("표시할 기체 데이터가 없습니다.");
            return;
        }

        previewShipIndex--;
        if (previewShipIndex < 0)
        {
            previewShipIndex = shipDefinitions.Count - 1;
        }

        SetMessage($"기체 확인: {PreviewShip.DisplayName}");
        NotifyChanged();
    }

    public bool TryExecutePreviewShipAction()
    {
        ShipDefinition ship = PreviewShip;
        if (ship == null)
        {
            SetMessage("선택한 기체 데이터가 없습니다.");
            return false;
        }

        return TryExecuteShipAction(ship.ShipId);
    }

    public bool TryExecuteShipAction(string shipId)
    {
        ShipDefinition ship = FindShipDefinition(shipId);

        if (ship == null)
        {
            SetMessage("선택한 기체 데이터를 찾을 수 없습니다.");
            return false;
        }

        if (IsShipUnlocked(ship))
        {
            return TrySelectShip(ship.ShipId);
        }

        return TryDevelopShip(ship.ShipId);
    }

    public bool TrySelectShip(string shipId)
    {
        ShipDefinition ship = FindShipDefinition(shipId);

        if (ship == null)
        {
            SetMessage("선택한 기체 데이터를 찾을 수 없습니다.");
            return false;
        }

        if (!IsShipUnlocked(ship))
        {
            SetMessage($"{ship.DisplayName}은 아직 개발되지 않았습니다.");
            return false;
        }

        selectedShipId = ship.ShipId;
        selectedWeaponTree = ResolveWeaponTreeForShip(ship);
        previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetSelectedShipId(selectedShipId);
            PermanentProgress.Instance.SetLastSelectedWeaponTree(selectedWeaponTree);
        }

        SaveProgress();

        SetMessage($"출격 기체 선택: {ship.DisplayName} / {GetWeaponDisplayName(selectedWeaponTree)}");
        return true;
    }
    public bool TryDevelopShip(string shipId)
    {
        ShipDefinition ship = FindShipDefinition(shipId);

        if (ship == null)
        {
            SetMessage("선택한 기체 데이터를 찾을 수 없습니다.");
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 기체를 개발할 수 없습니다.");
            return false;
        }

        if (IsShipUnlocked(ship))
        {
            return TrySelectShip(ship.ShipId);
        }

        if (!IsShipPrerequisiteMet(ship))
        {
            SetMessage($"{ship.DisplayName} 개발 조건이 충족되지 않았습니다.");
            return false;
        }

        int scrapCost = ship.RequiredScrapParts;
        int coreCost = ship.RequiredCoreShards;

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족. 필요: {FormatCost(scrapCost, coreCost)}");
            return false;
        }

        progress.AddUnlockFlag(ship.UnlockFlag);
        selectedShipId = ship.ShipId;
        previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));
        progress.SetSelectedShipId(selectedShipId);

        SaveProgress();
        SetMessage($"{ship.DisplayName} 개발 완료. 출격 기체로 선택했습니다.");
        return true;
    }

    public bool IsShipUnlocked(ShipDefinition ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (ship.UnlockedByDefault)
        {
            return true;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        return progress != null && progress.AnalyzedEquipmentComponentCount >= ship.RequiredAnalyzedComponents && progress.HasUnlockFlag(ship.UnlockFlag);
    }

    public bool IsPreviewShipSelected()
    {
        ShipDefinition ship = PreviewShip;
        return ship != null && ship.ShipId == SelectedShipId;
    }

    public bool CanExecuteShipAction(ShipDefinition ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (IsShipUnlocked(ship))
        {
            return ship.ShipId != SelectedShipId;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            return false;
        }

        return IsShipPrerequisiteMet(ship) && progress.CanSpend(ship.RequiredScrapParts, ship.RequiredCoreShards);
    }

    public string GetShipActionLabel(ShipDefinition ship)
    {
        if (ship == null)
        {
            return "기체 없음";
        }

        if (IsShipUnlocked(ship))
        {
            return ship.ShipId == SelectedShipId ? "선택 완료" : "기체 선택";
        }

        if (!IsShipPrerequisiteMet(ship))
        {
            return "조건 미달";
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null || !progress.CanSpend(ship.RequiredScrapParts, ship.RequiredCoreShards))
        {
            return "재화 부족";
        }

        return "기체 개발";
    }

    public Sprite GetPreviewShipSprite()
    {
        return PreviewShip != null
            ? PreviewShip.PreviewSprite
            : null;
    }

    public string GetPreviewShipTitle()
    {
        ShipDefinition ship = PreviewShip;
        return ship != null ? ship.DisplayName : "기체 없음";
    }

    public string BuildPreviewShipDetailText()
    {
        ShipDefinition ship = PreviewShip;
        if (ship == null)
        {
            return "기체 데이터가 없습니다.";
        }

        StringBuilder builder = new StringBuilder();

        bool unlocked = IsShipUnlocked(ship);

        if (!unlocked)
        {
            builder.AppendLine(ship.Description);
            builder.AppendLine();
            builder.AppendLine($"개발 비용  {FormatCost(ship.RequiredScrapParts, ship.RequiredCoreShards)}");

            if (!IsShipPrerequisiteMet(ship))
            {
                builder.AppendLine(string.IsNullOrWhiteSpace(ship.RequiredUnlockFlag)
                    ? "미충족 조건 있음"
                    : $"필요 조건: {ship.RequiredUnlockFlag}");
            }

            return builder.ToString();
        }

        builder.AppendLine($"무장  {GetWeaponDisplayName(ship.DefaultWeaponTree)}");
        builder.AppendLine($"HP  {ship.MaxHp}    적재  {ship.CargoCapacity}");
        builder.AppendLine($"이동  {FormatSignedPercent(ship.MoveSpeedBonusPercent)}    대시  {FormatSignedNumber(ship.DashDistanceBonus)}");
        builder.AppendLine();
        builder.AppendLine("기체 특성");
        builder.AppendLine(string.IsNullOrWhiteSpace(ship.PassiveDescription) ? "추가 패시브 없음." : ship.PassiveDescription);

        return builder.ToString();
    }
    public bool TryCompleteRestorationProject(BuildingType buildingType)
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            SetMessage("복구 진행 정보를 확인할 수 없습니다.");
            return false;
        }

        SettlementRestorationEvaluation evaluation = EvaluateRestorationProject(buildingType);
        if (evaluation.IsComplete)
        {
            SetMessage($"{GetBuildingDisplayName(buildingType)}은 이미 복구 완료 상태입니다.");
            return false;
        }

        if (!evaluation.CanComplete)
        {
            SetMessage($"{GetBuildingDisplayName(buildingType)}: 복구 조건이 충족되지 않았습니다.");
            return false;
        }

        string grantedUnlockFlag = GetGrantedRestorationUnlockFlag(buildingType);
        if (!progress.TryCompleteBuildingRestoration(buildingType, grantedUnlockFlag))
        {
            SetMessage($"{GetBuildingDisplayName(buildingType)} 복구 상태를 기록하지 못했습니다.");
            return false;
        }

        SaveProgress();
        RestorationCompleted?.Invoke(buildingType);
        SetMessage($"{GetBuildingDisplayName(buildingType)} 복구 완료.");
        return true;
    }

    [Obsolete("Use TryCompleteRestorationProject. Building levels now represent restoration completion.")]
    public bool TryRepairOrUpgradeBuilding(BuildingType buildingType)
    {
        return TryCompleteRestorationProject(buildingType);
    }

    public int GetBuildingLevel(BuildingType buildingType)
    {
        if (PermanentProgress.Instance == null)
        {
            return 0;
        }

        return PermanentProgress.Instance.GetBuildingLevel(buildingType);
    }

    public int GetSectorTechnologyLevel(string technologyId)
    {
        return PermanentProgress.Instance != null
            ? PermanentProgress.Instance.GetSectorTechnologyLevel(technologyId)
            : 0;
    }

    public bool CanUpgradeSectorTechnology(string technologyId)
    {
        return PermanentProgress.Instance != null &&
               PermanentProgress.Instance.CanUpgradeSectorTechnology(technologyId);
    }

    public bool TryUpgradeSectorTechnology(string technologyId)
    {
        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 기체 보강을 진행할 수 없습니다.");
            return false;
        }

        if (!SectorTechnologyCatalog.TryGet(technologyId, out SectorTechnologyDefinition definition))
        {
            SetMessage("기체 보강 데이터를 찾을 수 없습니다.");
            return false;
        }

        int currentLevel = progress.GetSectorTechnologyLevel(technologyId);
        if (currentLevel >= definition.MaxLevel)
        {
            SetMessage($"{definition.DisplayName}은 이미 최대 단계입니다.");
            return false;
        }

        int cost = definition.GetUpgradeCost(currentLevel + 1);
        if (!progress.TryUpgradeSectorTechnology(technologyId))
        {
            SetMessage($"안정화 합금이 부족합니다. 필요: {cost}");
            return false;
        }

        SaveProgress();
        SetMessage($"{definition.DisplayName} 기체 보강 완료. Lv {currentLevel} → {currentLevel + 1}");
        return true;
    }

    public string GetBuildingActionLabel(BuildingType buildingType)
    {
        SettlementRestorationEvaluation evaluation = EvaluateRestorationProject(buildingType);
        if (evaluation.IsComplete)
        {
            return "복구 완료";
        }

        if (!evaluation.CanComplete)
        {
            return "조건 미충족";
        }

        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        return definition != null ? definition.RestorationActionLabel : "복구";
    }

    public bool CanExecuteBuildingAction(BuildingType buildingType)
    {
        return EvaluateRestorationProject(buildingType).CanComplete;
    }

    public SettlementRestorationEvaluation EvaluateRestorationProject(BuildingType buildingType)
    {
        PermanentProgress progress = PermanentProgress.Instance;
        bool complete = progress != null && progress.GetBuildingLevel(buildingType) > 0;
        BossStoryPart requiredPart = GetRequiredBossStoryPart(buildingType);
        bool hasPart = requiredPart == BossStoryPart.None ||
                       (progress != null && progress.HasBossStoryPart(requiredPart));
        int requiredSynchronization = GetRequiredSynchronizationStage(buildingType);
        int currentSynchronization = progress != null ? progress.GetCoreSynchronizationStage() : 0;
        bool synchronizationMet = currentSynchronization >= requiredSynchronization;
        bool requiresPrior = RequiresPriorRestoration(buildingType);
        BuildingType priorBuilding = GetPriorRestorationBuilding(buildingType);
        bool priorMet = !requiresPrior ||
                        (progress != null && progress.GetBuildingLevel(priorBuilding) > 0);
        string requiredFlag = GetRequiredRestorationUnlockFlag(buildingType);
        bool flagMet = string.IsNullOrWhiteSpace(requiredFlag) ||
                       (progress != null && progress.HasUnlockFlag(requiredFlag));

        SettlementRestorationState state = complete
            ? SettlementRestorationState.Complete
            : hasPart && synchronizationMet && priorMet && flagMet
                ? SettlementRestorationState.Available
                : SettlementRestorationState.Locked;

        return new SettlementRestorationEvaluation(
            buildingType,
            state,
            requiredPart,
            hasPart,
            requiredSynchronization,
            currentSynchronization,
            requiresPrior,
            priorBuilding,
            priorMet,
            requiredFlag,
            flagMet
        );
    }

    public SettlementRestorationViewData BuildRestorationViewData(BuildingType buildingType)
    {
        SettlementRestorationEvaluation evaluation = EvaluateRestorationProject(buildingType);
        string stateText = evaluation.State switch
        {
            SettlementRestorationState.Complete => "복구 완료",
            SettlementRestorationState.Available => "복구 가능",
            _ => "기능 정지"
        };

        return new SettlementRestorationViewData(
            GetBuildingDisplayName(buildingType),
            GetBuildingDescription(buildingType),
            stateText,
            BuildRestorationRequirementText(evaluation),
            GetRestorationCompletionResult(buildingType),
            GetBuildingActionLabel(buildingType),
            evaluation.IsComplete,
            evaluation.CanComplete
        );
    }

    public string BuildBuildingDetailText(BuildingType buildingType)
    {
        SettlementRestorationViewData viewData = BuildRestorationViewData(buildingType);
        return viewData != null ? viewData.BuildFallbackBodyText() : string.Empty;
    }

    public bool TryUnlockOrUpgradeTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            SetMessage("선택한 특성 데이터가 없습니다.");
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 특성을 강화할 수 없습니다.");
            return false;
        }

        int currentLevel = progress.GetTraitLevel(trait.TraitId);
        int maxLevel = Mathf.Max(1, trait.MaxLevel);

        if (currentLevel >= maxLevel)
        {
            SetMessage($"{trait.DisplayName}은 이미 최대 단계입니다.");
            return false;
        }

        if (!CanTraitBePurchasedInCurrentSelection(trait))
        {
            SetMessage($"{trait.DisplayName}은 현재 선택 무기와 맞지 않습니다.");
            return false;
        }

        int nextLevel = currentLevel + 1;
        int scrapCost = GetTraitScrapCost(trait, nextLevel);
        int coreCost = GetTraitCoreCost(trait, nextLevel);

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족. 필요: {FormatTraitCost(scrapCost, coreCost)}");
            return false;
        }

        progress.SetTraitLevel(trait.TraitId, nextLevel);
        SaveProgress();

        string verb = currentLevel == 0 ? "해금" : "강화";
        SetMessage($"{trait.DisplayName} {verb} 완료. Lv {currentLevel} → {nextLevel}");
        return true;
    }

    public bool TryUnlockOrUpgradeTrait(string traitId)
    {
        return TryUnlockOrUpgradeTrait(FindTraitDefinition(traitId));
    }

    public int GetTraitLevel(TraitDefinition trait)
    {
        if (trait == null || PermanentProgress.Instance == null)
        {
            return 0;
        }

        return PermanentProgress.Instance.GetTraitLevel(trait.TraitId);
    }

    public bool IsTraitMaxLevel(TraitDefinition trait)
    {
        if (trait == null)
        {
            return true;
        }

        return GetTraitLevel(trait) >= Mathf.Max(1, trait.MaxLevel);
    }

    public int GetTraitScrapCost(TraitDefinition trait, int targetLevel)
    {
        if (trait == null)
        {
            return 0;
        }

        int cost = traitBaseScrapCost + ((Mathf.Max(1, targetLevel) - 1) * traitScrapCostStep);

        if (trait.Category == TraitCategory.WeaponSpecific)
        {
            cost += weaponSpecificTraitScrapBonus;
        }

        return Mathf.Max(0, cost);
    }

    public int GetTraitCoreCost(TraitDefinition trait, int targetLevel)
    {
        if (trait == null)
        {
            return 0;
        }

        return targetLevel >= traitCoreCostFromLevel ? 1 : 0;
    }

    public string GetTraitActionLabel(TraitDefinition trait)
    {
        if (trait == null)
        {
            return "특성 없음";
        }

        if (!CanTraitBePurchasedInCurrentSelection(trait))
        {
            return "무기 조건 불일치";
        }

        int currentLevel = GetTraitLevel(trait);
        int maxLevel = Mathf.Max(1, trait.MaxLevel);

        if (currentLevel >= maxLevel)
        {
            return "최대 단계";
        }

        int nextLevel = currentLevel + 1;
        int scrapCost = GetTraitScrapCost(trait, nextLevel);
        int coreCost = GetTraitCoreCost(trait, nextLevel);

        if (PermanentProgress.Instance == null || !PermanentProgress.Instance.CanSpend(scrapCost, coreCost))
        {
            return "재화 부족";
        }

        return currentLevel == 0 ? "특성 해금" : "특성 강화";
    }

    public bool CanExecuteTraitAction(TraitDefinition trait)
    {
        if (trait == null || IsTraitMaxLevel(trait) || !CanTraitBePurchasedInCurrentSelection(trait))
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            return false;
        }

        int targetLevel = GetTraitLevel(trait) + 1;
        return progress.CanSpend(GetTraitScrapCost(trait, targetLevel), GetTraitCoreCost(trait, targetLevel));
    }

    public string BuildTraitDetailText(TraitDefinition trait)
    {
        if (trait == null)
        {
            return "특성을 선택하세요.";
        }

        int currentLevel = GetTraitLevel(trait);
        int maxLevel = Mathf.Max(1, trait.MaxLevel);
        bool isMax = currentLevel >= maxLevel;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(trait.Description);
        builder.AppendLine();
        builder.AppendLine($"분류: {GetTraitCategoryDisplayName(trait)}");
        builder.AppendLine($"현재 레벨: Lv {currentLevel} / {maxLevel}");
        builder.AppendLine($"현재 효과: {FormatTraitEffects(trait, currentLevel)}");
        builder.AppendLine();

        if (isMax)
        {
            builder.AppendLine("다음 효과: 최대 단계입니다.");
            return builder.ToString();
        }

        int nextLevel = currentLevel + 1;
        int scrapCost = GetTraitScrapCost(trait, nextLevel);
        int coreCost = GetTraitCoreCost(trait, nextLevel);

        builder.AppendLine($"다음 효과: {FormatTraitEffects(trait, nextLevel)}");
        builder.AppendLine();
        builder.AppendLine("필요 재화:");
        builder.AppendLine(FormatTraitCost(scrapCost, coreCost));
        if (!CanTraitBePurchasedInCurrentSelection(trait))
        {
            builder.AppendLine();
            builder.AppendLine($"현재 선택 무기: {GetWeaponDisplayName(selectedWeaponTree)}");
            builder.AppendLine("이 무기 트리에서는 해금할 수 없습니다.");
        }

        return builder.ToString();
    }

    public bool LaunchExpedition()
    {
        return TryLaunchExpedition(out _);
    }

    public bool TryLaunchExpedition(out SettlementExpeditionLaunchFailure failure)
    {
        if (!SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            SettlementExpeditionLaunchGuard.IsDialogueActive,
            SettlementExpeditionLaunchGuard.IsMandatoryFirstSettlementStoryPending,
            out failure))
        {
            ShowLocalizedMessage(SettlementExpeditionLaunchGuard.DialogueActiveTextKey);
            return false;
        }

        if (RunManager.Instance == null)
        {
            failure = SettlementExpeditionLaunchFailure.MissingRunManager;
            SetMessage("RunManager가 없어 출격할 수 없습니다.");
            return false;
        }

        ShipDefinition selectedShip = FindShipDefinition(SelectedShipId);
        if (selectedShip == null)
        {
            failure = SettlementExpeditionLaunchFailure.MissingSelectedShip;
            SetMessage("선택된 기체 데이터가 없습니다. 기체를 먼저 선택하세요.");
            return false;
        }

        if (!IsShipUnlocked(selectedShip))
        {
            failure = SettlementExpeditionLaunchFailure.SelectedShipLocked;
            SetMessage("선택된 기체가 개발되지 않았습니다.");
            return false;
        }

        selectedWeaponTree = ResolveWeaponTreeForShip(selectedShip);

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetSelectedShipId(selectedShip.ShipId);
            PermanentProgress.Instance.SetLastSelectedWeaponTree(selectedWeaponTree);
        }

        SaveProgress();

        SetMessage($"탐사 시작: {selectedShip.DisplayName} / {GetWeaponDisplayName(selectedWeaponTree)}");

        if (!SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            SettlementExpeditionLaunchGuard.IsDialogueActive,
            SettlementExpeditionLaunchGuard.IsMandatoryFirstSettlementStoryPending,
            out failure))
        {
            ShowLocalizedMessage(SettlementExpeditionLaunchGuard.DialogueActiveTextKey);
            return false;
        }

        RunManager.Instance.StartNewRunAndLoadExpedition(selectedWeaponTree, selectedShip.ShipId);
        failure = SettlementExpeditionLaunchFailure.None;
        return true;
    }

    public bool LaunchFinalExpedition()
    {
        if (!SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            SettlementExpeditionLaunchGuard.IsDialogueActive,
            SettlementExpeditionLaunchGuard.IsMandatoryFirstSettlementStoryPending,
            out _))
        {
            ShowLocalizedMessage(SettlementExpeditionLaunchGuard.DialogueActiveTextKey);
            return false;
        }

        if (RunManager.Instance == null)
        {
            SetMessage("RunManager가 없어 중앙 물류망으로 출격할 수 없습니다.");
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null || !progress.CanLaunchFinalExpedition)
        {
            SetMessage("완전 코어 활성화와 정착지 방어 완료가 필요합니다.");
            return false;
        }

        ShipDefinition selectedShip = FindShipDefinition(SelectedShipId);
        if (selectedShip == null || !IsShipUnlocked(selectedShip))
        {
            SetMessage("출격 가능한 기체를 먼저 선택하세요.");
            return false;
        }

        selectedWeaponTree = ResolveWeaponTreeForShip(selectedShip);
        progress.SetSelectedShipId(selectedShip.ShipId);
        progress.SetLastSelectedWeaponTree(selectedWeaponTree);
        SaveProgress();

        SetMessage($"중앙 물류망 출격: {selectedShip.DisplayName} / {GetWeaponDisplayName(selectedWeaponTree)}");

        if (!SettlementExpeditionLaunchGuard.TryPassMandatoryStoryGate(
            SettlementExpeditionLaunchGuard.IsDialogueActive,
            SettlementExpeditionLaunchGuard.IsMandatoryFirstSettlementStoryPending,
            out _))
        {
            ShowLocalizedMessage(SettlementExpeditionLaunchGuard.DialogueActiveTextKey);
            return false;
        }

        return RunManager.Instance.StartFinalExpeditionAndLoad(
            selectedWeaponTree,
            selectedShip.ShipId
        );
    }
    private WeaponTreeType ResolveWeaponTreeForShip(ShipDefinition ship)
    {
        if (ship == null)
        {
            return defaultWeaponTree;
        }

        string id = ship.ShipId.ToLowerInvariant();

        if (id.Contains("shotgun"))
        {
            return WeaponTreeType.Shotgun;
        }

        if (id.Contains("sniper"))
        {
            return WeaponTreeType.Sniper;
        }

        if (id.Contains("machine") || id.Contains("basic"))
        {
            return WeaponTreeType.MachineGun;
        }

        return ship.DefaultWeaponTree;
    }
    public string GetBuildingDisplayName(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName;
        }

        return buildingType switch
        {
            BuildingType.Hangar => "격납고 제어 복구",
            BuildingType.EngineWorkshop => "추진 공방 복구",
            BuildingType.WeaponLab => "화기 연구소 복구",
            BuildingType.RecoveryProcessor => "회수 처리 계통 복구",
            _ => buildingType.ToString()
        };
    }

    public string GetWeaponDisplayName(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => "근접 + 샷건",
            WeaponTreeType.Sniper => "관통 스나이퍼",
            WeaponTreeType.MachineGun => "기관총",
            _ => weaponTreeType.ToString()
        };
    }

    public string GetResourceText()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        int scrap = progress != null ? progress.ScrapParts : 0;
        int core = progress != null ? progress.CoreShards : 0;
        return $"스크랩 부품 {scrap}    코어 {core}";
    }

    public string BuildStatusText()
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("[Settlement]");
        builder.AppendLine($"Selected Weapon: {GetWeaponDisplayName(selectedWeaponTree)}");
        builder.AppendLine($"Selected Ship: {SelectedShipId}");

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            builder.AppendLine("PermanentProgress: 없음");
            return builder.ToString();
        }

        builder.AppendLine($"Scrap: {progress.ScrapParts}");
        builder.AppendLine($"Core: {progress.CoreShards}");
        builder.AppendLine();

        builder.AppendLine("[Settlement Restoration]");
        builder.AppendLine($"격납고: {GetRestorationDebugState(progress, BuildingType.Hangar)}");
        builder.AppendLine($"엔진 공방: {GetRestorationDebugState(progress, BuildingType.EngineWorkshop)}");
        builder.AppendLine($"화기 연구소: {GetRestorationDebugState(progress, BuildingType.WeaponLab)}");
        builder.AppendLine($"회수 처리장: {GetRestorationDebugState(progress, BuildingType.RecoveryProcessor)}");
        builder.AppendLine();
        builder.AppendLine("[Campaign]");
        builder.AppendLine(progress.BuildCampaignProgressText());

        return builder.ToString();
    }

    private static string GetRestorationDebugState(PermanentProgress progress, BuildingType buildingType)
    {
        int savedLevel = progress != null ? progress.GetBuildingLevel(buildingType) : 0;
        return savedLevel > 0 ? $"복구 완료 (저장 단계 {savedLevel})" : "기능 정지";
    }

    public ShipDefinition GetShipByIndex(int index)
    {
        if (shipDefinitions == null || shipDefinitions.Count == 0)
        {
            return null;
        }

        index = Mathf.Clamp(index, 0, shipDefinitions.Count - 1);
        return shipDefinitions[index];
    }

    public ShipDefinition FindShipDefinition(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        return shipDefinitions.Find(ship => ship != null && ship.ShipId == shipId);
    }

    public BuildingDefinition FindBuildingDefinition(BuildingType buildingType)
    {
        return buildingDefinitions.Find(definition => definition != null && definition.BuildingType == buildingType);
    }

    public TraitDefinition FindTraitDefinition(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        return traitDefinitions.Find(trait => trait != null && trait.TraitId == traitId);
    }

    private bool IsShipPrerequisiteMet(ShipDefinition ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (ship.RequiredAnalyzedComponents > 0 && (PermanentProgress.Instance == null ||
            PermanentProgress.Instance.AnalyzedEquipmentComponentCount < ship.RequiredAnalyzedComponents)) return false;

        if (string.IsNullOrWhiteSpace(ship.RequiredUnlockFlag))
        {
            return true;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        return progress != null && progress.HasUnlockFlag(ship.RequiredUnlockFlag);
    }

    private string ResolveDefaultShipId()
    {
        if (!string.IsNullOrWhiteSpace(defaultShipId))
        {
            return defaultShipId;
        }

        if (shipDefinitions != null && shipDefinitions.Count > 0 && shipDefinitions[0] != null)
        {
            return shipDefinitions[0].ShipId;
        }

        return "basic_ship";
    }

    private int FindShipIndex(string shipId)
    {
        if (shipDefinitions == null || shipDefinitions.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < shipDefinitions.Count; i++)
        {
            ShipDefinition ship = shipDefinitions[i];
            if (ship != null && ship.ShipId == shipId)
            {
                return i;
            }
        }

        return -1;
    }

    private string GetBuildingDescription(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.Description))
        {
            return definition.Description;
        }

        return buildingType switch
        {
            BuildingType.Hangar => "회수한 안정화 부품을 설치해 격납고 제어 계통을 복구합니다.",
            BuildingType.EngineWorkshop => "정지한 추진 정비 계통을 캠페인 부품과 동기화합니다.",
            BuildingType.WeaponLab => "위상 항법 데이터를 연결해 화기 연구소의 전력을 복구합니다.",
            BuildingType.RecoveryProcessor => "완전 코어 신호와 복구된 시설을 연결해 회수 처리 계통을 정상화합니다.",
            _ => "정착지 기능을 복구하는 캠페인 프로젝트입니다."
        };
    }

    private BossStoryPart GetRequiredBossStoryPart(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null)
        {
            return definition.RequiredBossStoryPart;
        }

        return buildingType switch
        {
            BuildingType.Hangar => BossStoryPart.SectorStabilizer,
            BuildingType.EngineWorkshop => BossStoryPart.MatterCompressor,
            BuildingType.WeaponLab => BossStoryPart.PhaseNavigationLens,
            _ => BossStoryPart.None
        };
    }

    private int GetRequiredSynchronizationStage(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null)
        {
            return definition.RequiredSynchronizationStage;
        }

        return buildingType switch
        {
            BuildingType.Hangar => 2,
            BuildingType.EngineWorkshop => 3,
            BuildingType.WeaponLab => 4,
            BuildingType.RecoveryProcessor => 5,
            _ => 0
        };
    }

    private bool RequiresPriorRestoration(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null)
        {
            return definition.RequiresPriorRestoration;
        }

        return buildingType != BuildingType.Hangar;
    }

    private BuildingType GetPriorRestorationBuilding(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && definition.RequiresPriorRestoration)
        {
            return definition.PriorRestorationBuilding;
        }

        return buildingType switch
        {
            BuildingType.EngineWorkshop => BuildingType.Hangar,
            BuildingType.WeaponLab => BuildingType.EngineWorkshop,
            BuildingType.RecoveryProcessor => BuildingType.WeaponLab,
            _ => BuildingType.Hangar
        };
    }

    private string GetRequiredRestorationUnlockFlag(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null)
        {
            return definition.RequiredUnlockFlag;
        }

        return buildingType == BuildingType.RecoveryProcessor
            ? "campaign_route_core_assembled"
            : string.Empty;
    }

    private string GetGrantedRestorationUnlockFlag(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.GrantedUnlockFlag))
        {
            return definition.GrantedUnlockFlag;
        }

        return buildingType switch
        {
            BuildingType.Hangar => "settlement_restoration_hangar_complete",
            BuildingType.EngineWorkshop => "settlement_restoration_engine_workshop_complete",
            BuildingType.WeaponLab => "settlement_restoration_weapon_lab_complete",
            BuildingType.RecoveryProcessor => "settlement_restoration_recovery_processor_complete",
            _ => string.Empty
        };
    }

    public string GetRestorationVisualStateKey(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.VisualStateKey))
        {
            return definition.VisualStateKey;
        }

        return $"restoration_{buildingType}";
    }

    private string GetRestorationCompletionResult(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.CompletionResultDescription))
        {
            return definition.CompletionResultDescription;
        }

        return buildingType switch
        {
            BuildingType.Hangar => "격납고 제어 계통 정상화\n다음 복구 프로젝트 개방",
            BuildingType.EngineWorkshop => "추진 정비 계통 정상화\n다음 복구 프로젝트 개방",
            BuildingType.WeaponLab => "화기 연구소 전력 정상화\n다음 복구 프로젝트 개방",
            BuildingType.RecoveryProcessor => "회수 처리 계통 정상화\n정착지 주요 시설 복구 완료",
            _ => "정착지 시설 정상화"
        };
    }

    private string BuildRestorationRequirementText(SettlementRestorationEvaluation evaluation)
    {
        StringBuilder builder = new StringBuilder();

        if (evaluation.RequiredBossStoryPart != BossStoryPart.None)
        {
            string partName = evaluation.HasRequiredBossStoryPart
                ? CampaignProgressionCatalog.GetStoryPartDisplayName(evaluation.RequiredBossStoryPart)
                : "미확인 보스 부품";
            AppendRequirement(builder, $"{partName}: {(evaluation.HasRequiredBossStoryPart ? "확보" : "미확보")}", evaluation.HasRequiredBossStoryPart);
        }

        if (evaluation.RequiredSynchronizationStage > 0)
        {
            bool met = evaluation.CurrentSynchronizationStage >= evaluation.RequiredSynchronizationStage;
            string status = met ? "충족" : $"{evaluation.CurrentSynchronizationStage} / {evaluation.RequiredSynchronizationStage}";
            AppendRequirement(builder, $"코어 동기화 {evaluation.RequiredSynchronizationStage}단계: {status}", met);
        }

        if (evaluation.RequiresPriorRestoration)
        {
            AppendRequirement(
                builder,
                $"선행 복구: {(evaluation.HasPriorRestoration ? "완료" : "필요")}",
                evaluation.HasPriorRestoration
            );
        }

        if (!string.IsNullOrWhiteSpace(evaluation.RequiredUnlockFlag))
        {
            AppendRequirement(
                builder,
                evaluation.HasRequiredUnlockFlag ? "선행 신호: 해석 완료" : "미확인 코어 반응: 신호 해석 중",
                evaluation.HasRequiredUnlockFlag
            );
        }

        if (builder.Length == 0)
        {
            AppendRequirement(builder, "추가 조건 없음", true);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendRequirement(StringBuilder builder, string text, bool satisfied)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        string color = satisfied ? "#74E6D2" : "#D6B36A";
        string marker = satisfied ? "✓" : "•";
        builder.Append($"<color={color}>{marker}</color> {text}");
    }

    private bool CanTraitBePurchasedInCurrentSelection(TraitDefinition trait)
    {
        // Ordinary equipment is prepared here and leveled exclusively during expeditions.
        if (trait != null && trait.CanAppearAsRandomDropTrait) return false;
        if (trait == null)
        {
            return false;
        }

        if (!RunTraitAcquisitionService.MeetsOfferPrerequisites(trait))
        {
            return false;
        }

        if (!requireSelectedWeaponForWeaponSpecificTraits)
        {
            return true;
        }

        return trait.IsAvailableFor(selectedWeaponTree);
    }

    private string GetTraitCategoryDisplayName(TraitDefinition trait)
    {
        if (trait == null)
        {
            return "없음";
        }

        if (trait.Category == TraitCategory.Shared)
        {
            return "공유 특성";
        }

        return $"{GetWeaponDisplayName(trait.WeaponTreeType)} 전용 특성";
    }

    private string FormatTraitEffects(TraitDefinition trait, int level)
    {
        if (trait == null)
        {
            return "없음";
        }

        if (level <= 0)
        {
            return "미해금 상태. 효과 없음.";
        }

        return TraitEffectTextUtility.BuildRichEffectText(trait, level).Replace("\n", ", ");
    }

    private string FormatCost(int scrapCost, int coreCost)
    {
        scrapCost = Mathf.Max(0, scrapCost);
        coreCost = Mathf.Max(0, coreCost);

        if (coreCost > 0)
        {
            return $"스크랩 부품 {scrapCost}, 코어 {coreCost}";
        }

        return $"스크랩 부품 {scrapCost}";
    }

    private string FormatTraitCost(int scrapCost, int coreCost)
    {
        scrapCost = Mathf.Max(0, scrapCost);
        coreCost = Mathf.Max(0, coreCost);

        if (coreCost > 0)
        {
            return $"스크랩 {scrapCost}, 코어 {coreCost}";
        }

        return $"스크랩 {scrapCost}";
    }

    private string FormatSignedPercent(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "+0%";
        }

        return value > 0f ? $"+{value:0.#}%" : $"{value:0.#}%";
    }

    private string FormatSignedNumber(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "+0";
        }

        return value > 0f ? $"+{value:0.##}" : $"{value:0.##}";
    }

    private string FormatSignedCooldownReduction(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "0초";
        }

        return value > 0f ? $"-{value:0.##}초" : $"+{Mathf.Abs(value):0.##}초";
    }

    private void SaveProgress()
    {
        if (SaveManager.Instance != null && PermanentProgress.Instance != null)
        {
            SaveManager.Instance.Save(PermanentProgress.Instance);
        }
    }

    private void SetMessage(string message)
    {
        LastMessage = message;
        Debug.Log(message, this);
        NotifyChanged();
    }

    private void ShowLocalizedMessage(string textKey)
    {
        string message = GetLocalizedText(textKey);
        SetMessage(message);
    }

    private static string GetLocalizedText(string textKey)
    {
        // A direct scene entry may precede the Boot localization service. Keep
        // this launch-gate fallback readable instead of exposing the raw key.
        if (!VoidScrapperLocalizationService.HasInstance &&
            textKey == SettlementExpeditionLaunchGuard.DialogueActiveTextKey)
        {
            return "통신이 끝난 후 탐사를 시작할 수 있습니다.";
        }
        return VoidScrapperLocalizationService.HasInstance
            ? VoidScrapperLocalizationService.Instance.GetText(textKey)
            : textKey;
    }

    private static string FormatDamagedAccessKeyObjective(
        PermanentProgress progress)
    {
        if (!VoidScrapperLocalizationService.HasInstance)
        {
            return MainDamagedAccessKeyQuestIds.ObjectiveTextKey;
        }

        Dictionary<string, string> arguments = new Dictionary<string, string>(2)
        {
            {
                "collected",
                (progress != null
                    ? progress.DamagedAccessKeyCollectedPartCount
                    : 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            {
                "total",
                MainDamagedAccessKeyQuestIds.RequiredPartCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            }
        };

        return VoidScrapperLocalizationService.Instance.FormatText(
            MainDamagedAccessKeyQuestIds.ObjectiveTextKey,
            arguments);
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void HandleProgressChanged()
    {
        NotifyChanged();
    }

    private void RemoveNullCatalogEntries()
    {
        if (shipDefinitions != null)
        {
            shipDefinitions.RemoveAll(ship => ship == null);
        }

        if (buildingDefinitions != null)
        {
            buildingDefinitions.RemoveAll(building => building == null);
        }

        if (traitDefinitions != null)
        {
            traitDefinitions.RemoveAll(trait => trait == null);
        }
    }
}
