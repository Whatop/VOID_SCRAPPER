using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SettlementController : MonoBehaviour
{
    public static SettlementController Instance { get; private set; }

    [Header("Selection Defaults")]
    [SerializeField] private WeaponTreeType defaultWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string defaultShipId = "basic_ship";

    [Header("Catalog")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private List<BuildingDefinition> buildingDefinitions = new List<BuildingDefinition>();
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Fallback Building Cost")]
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

    public string LastMessage { get; private set; } = "정착지에 도착했다.";

    public event Action Changed;

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

        selectedWeaponTree = progress.LastSelectedWeaponTree;
        selectedShipId = progress.SelectedShipId;

        if (string.IsNullOrWhiteSpace(selectedShipId))
        {
            selectedShipId = ResolveDefaultShipId();
        }

        int selectedIndex = FindShipIndex(selectedShipId);
        previewShipIndex = selectedIndex >= 0 ? selectedIndex : Mathf.Max(0, FindShipIndex(ResolveDefaultShipId()));
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
        previewShipIndex = Mathf.Max(0, FindShipIndex(selectedShipId));

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetSelectedShipId(selectedShipId);
        }

        SaveProgress();
        SetMessage($"출격 기체 선택: {ship.DisplayName}");
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
        return progress != null && progress.HasUnlockFlag(ship.UnlockFlag);
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
        return PreviewShip != null ? PreviewShip.PreviewSprite : null;
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

        builder.AppendLine(ship.Description);
        builder.AppendLine();

        bool unlocked = IsShipUnlocked(ship);

        if (!unlocked)
        {
            builder.AppendLine("개발 필요:");
            builder.AppendLine(FormatCost(ship.RequiredScrapParts, ship.RequiredCoreShards));

            if (!IsShipPrerequisiteMet(ship))
            {
                builder.AppendLine();
                builder.AppendLine("해금 조건:");
                builder.AppendLine(string.IsNullOrWhiteSpace(ship.RequiredUnlockFlag)
                    ? "미충족 조건 있음"
                    : $"필요 조건: {ship.RequiredUnlockFlag}");
            }

            return builder.ToString();
        }

        builder.AppendLine("기본 능력:");
        builder.AppendLine($"체력 {ship.MaxHp}");
        builder.AppendLine($"이동속도 {FormatSignedPercent(ship.MoveSpeedBonusPercent)}");
        builder.AppendLine($"대쉬 거리 {FormatSignedNumber(ship.DashDistanceBonus)}");
        builder.AppendLine($"대쉬 쿨다운 {FormatSignedCooldownReduction(ship.DashCooldownReduction)}");
        builder.AppendLine();

        builder.AppendLine("패시브:");
        builder.AppendLine(string.IsNullOrWhiteSpace(ship.PassiveDescription) ? "추가 패시브 없음." : ship.PassiveDescription);
        builder.AppendLine();

        builder.AppendLine("상태:");
        builder.AppendLine(ship.ShipId == SelectedShipId ? "현재 선택 중" : "개발 완료");

        return builder.ToString();
    }
    public bool TryRepairOrUpgradeBuilding(BuildingType buildingType)
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 정착지를 보수할 수 없습니다.");
            return false;
        }

        int currentLevel = progress.GetBuildingLevel(buildingType);
        int maxLevel = GetBuildingMaxLevel(buildingType);

        if (currentLevel >= maxLevel)
        {
            SetMessage($"{GetBuildingDisplayName(buildingType)}은 이미 최대 단계입니다.");
            return false;
        }

        int nextLevel = currentLevel + 1;
        int scrapCost = GetBuildingScrapCost(buildingType, nextLevel);
        int coreCost = GetBuildingCoreCost(buildingType, nextLevel);

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족. 필요: {FormatCost(scrapCost, coreCost)}");
            return false;
        }

        progress.SetBuildingLevel(buildingType, nextLevel);
        SaveProgress();

        string verb = currentLevel == 0 ? "수리" : "업그레이드";
        SetMessage($"{GetBuildingDisplayName(buildingType)} {verb} 완료. Lv {currentLevel} → {nextLevel}");
        return true;
    }

    public int GetBuildingLevel(BuildingType buildingType)
    {
        if (PermanentProgress.Instance == null)
        {
            return 0;
        }

        return PermanentProgress.Instance.GetBuildingLevel(buildingType);
    }

    public int GetBuildingMaxLevel(BuildingType buildingType)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        if (definition != null && definition.Levels != null && definition.Levels.Count > 0)
        {
            int max = 0;
            foreach (BuildingLevelDefinition levelDefinition in definition.Levels)
            {
                if (levelDefinition != null)
                {
                    max = Mathf.Max(max, levelDefinition.Level);
                }
            }

            return Mathf.Max(1, max);
        }

        return Mathf.Max(1, fallbackBuildingMaxLevel);
    }

    public bool IsBuildingMaxLevel(BuildingType buildingType)
    {
        return GetBuildingLevel(buildingType) >= GetBuildingMaxLevel(buildingType);
    }

    public int GetNextBuildingLevel(BuildingType buildingType)
    {
        return Mathf.Min(GetBuildingLevel(buildingType) + 1, GetBuildingMaxLevel(buildingType));
    }

    public int GetBuildingScrapCost(BuildingType buildingType, int targetLevel)
    {
        BuildingLevelDefinition definition = GetBuildingLevelDefinition(buildingType, targetLevel);
        if (definition != null)
        {
            return Mathf.Max(0, definition.ScrapCost);
        }

        if (targetLevel <= 1)
        {
            return Mathf.Max(0, fallbackRepairScrapCost);
        }

        return Mathf.Max(0, fallbackUpgradeBaseScrapCost + ((targetLevel - 2) * fallbackUpgradeScrapCostStep));
    }

    public int GetBuildingCoreCost(BuildingType buildingType, int targetLevel)
    {
        BuildingLevelDefinition definition = GetBuildingLevelDefinition(buildingType, targetLevel);
        if (definition != null)
        {
            return Mathf.Max(0, definition.CoreShardCost);
        }

        return targetLevel >= fallbackCoreCostFromLevel ? 1 : 0;
    }

    public string GetBuildingActionLabel(BuildingType buildingType)
    {
        int level = GetBuildingLevel(buildingType);

        if (IsBuildingMaxLevel(buildingType))
        {
            return "최대 단계";
        }

        int nextLevel = level + 1;
        int scrapCost = GetBuildingScrapCost(buildingType, nextLevel);
        int coreCost = GetBuildingCoreCost(buildingType, nextLevel);

        if (PermanentProgress.Instance == null || !PermanentProgress.Instance.CanSpend(scrapCost, coreCost))
        {
            return "재화 부족";
        }

        return level == 0 ? "수리" : "업그레이드";
    }

    public bool CanExecuteBuildingAction(BuildingType buildingType)
    {
        if (IsBuildingMaxLevel(buildingType))
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;
        if (progress == null)
        {
            return false;
        }

        int targetLevel = GetBuildingLevel(buildingType) + 1;
        return progress.CanSpend(GetBuildingScrapCost(buildingType, targetLevel), GetBuildingCoreCost(buildingType, targetLevel));
    }

    public string BuildBuildingDetailText(BuildingType buildingType)
    {
        int level = GetBuildingLevel(buildingType);
        int maxLevel = GetBuildingMaxLevel(buildingType);
        bool isMax = level >= maxLevel;

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(GetBuildingDescription(buildingType));
        builder.AppendLine();
        builder.AppendLine($"현재 단계: Lv {level} / {maxLevel}");
        builder.AppendLine($"현재 효과: {GetBuildingEffectText(buildingType, level)}");
        builder.AppendLine();

        if (isMax)
        {
            builder.AppendLine("다음 단계: 최대 단계입니다.");
            return builder.ToString();
        }

        int nextLevel = level + 1;
        string nextAction = level == 0 ? "수리" : "업그레이드";
        int scrapCost = GetBuildingScrapCost(buildingType, nextLevel);
        int coreCost = GetBuildingCoreCost(buildingType, nextLevel);

        builder.AppendLine($"다음 {nextAction}: Lv {nextLevel}");
        builder.AppendLine($"다음 효과: {GetBuildingEffectText(buildingType, nextLevel)}");
        builder.AppendLine();
        builder.AppendLine("필요 재화:");
        builder.AppendLine(FormatCost(scrapCost, coreCost));

        return builder.ToString();
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
            SetMessage($"재화 부족. 필요: {FormatCost(scrapCost, coreCost)}");
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
        builder.AppendLine(FormatCost(scrapCost, coreCost));
        if (!CanTraitBePurchasedInCurrentSelection(trait))
        {
            builder.AppendLine();
            builder.AppendLine($"현재 선택 무기: {GetWeaponDisplayName(selectedWeaponTree)}");
            builder.AppendLine("이 무기 트리에서는 해금할 수 없습니다.");
        }

        return builder.ToString();
    }

    public void LaunchExpedition()
    {
        if (RunManager.Instance == null)
        {
            SetMessage("RunManager가 없어 출격할 수 없습니다.");
            return;
        }

        ShipDefinition selectedShip = FindShipDefinition(SelectedShipId);
        if (selectedShip == null)
        {
            SetMessage("선택된 기체 데이터가 없습니다. 기체를 먼저 선택하세요.");
            return;
        }

        if (!IsShipUnlocked(selectedShip))
        {
            SetMessage("선택된 기체가 개발되지 않았습니다.");
            return;
        }

        SaveProgress();
        SetMessage($"탐사 시작: {selectedShip.DisplayName} / {GetWeaponDisplayName(selectedWeaponTree)}");
        RunManager.Instance.StartNewRunAndLoadExpedition(selectedWeaponTree, SelectedShipId);
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
            BuildingType.Hangar => "격납고",
            BuildingType.EngineWorkshop => "엔진 공방",
            BuildingType.WeaponLab => "화기 연구소",
            BuildingType.RecoveryProcessor => "회수 처리장",
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
        return $"스크랩 부품 {scrap}    코어 조각 {core}";
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

        builder.AppendLine("[Buildings]");
        builder.AppendLine($"격납고: Lv {progress.GetBuildingLevel(BuildingType.Hangar)}");
        builder.AppendLine($"엔진 공방: Lv {progress.GetBuildingLevel(BuildingType.EngineWorkshop)}");
        builder.AppendLine($"화기 연구소: Lv {progress.GetBuildingLevel(BuildingType.WeaponLab)}");
        builder.AppendLine($"회수 처리장: Lv {progress.GetBuildingLevel(BuildingType.RecoveryProcessor)}");

        return builder.ToString();
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

    private BuildingLevelDefinition GetBuildingLevelDefinition(BuildingType buildingType, int targetLevel)
    {
        BuildingDefinition definition = FindBuildingDefinition(buildingType);
        return definition != null ? definition.GetLevelDefinition(targetLevel) : null;
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
            BuildingType.Hangar => "격납고는 최대 체력과 수리 효율을 담당합니다.",
            BuildingType.EngineWorkshop => "엔진 공방은 이동속도, 대쉬 거리, 대쉬 쿨다운을 담당합니다.",
            BuildingType.WeaponLab => "화기 연구소는 공격력, 연사력, 탄속, 사거리를 담당합니다.",
            BuildingType.RecoveryProcessor => "회수 처리장은 스크랩 획득량, 회복 효율, 회수 편의성을 담당합니다.",
            _ => "정착지 시설입니다."
        };
    }

    private string GetBuildingEffectText(BuildingType buildingType, int level)
    {
        if (level <= 0)
        {
            return "파손 상태. 효과 없음.";
        }

        BuildingLevelDefinition definition = GetBuildingLevelDefinition(buildingType, level);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.EffectDescription))
        {
            return definition.EffectDescription;
        }

        return buildingType switch
        {
            BuildingType.Hangar => level switch
            {
                1 => "최대 체력 +2",
                2 => "최대 체력 +4, 수리 효율 +10%",
                3 => "최대 체력 +6, 수리 효율 +20%",
                _ => "최대 단계"
            },

            BuildingType.EngineWorkshop => level switch
            {
                1 => "이동속도 +5%",
                2 => "이동속도 +5%, 대쉬 거리 +0.5",
                3 => "이동속도 +8%, 대쉬 거리 +0.5, 대쉬 쿨다운 -0.1초",
                _ => "최대 단계"
            },

            BuildingType.WeaponLab => level switch
            {
                1 => "전체 공격력 +10%",
                2 => "전체 공격력 +10%, 탄속 +10%",
                3 => "전체 공격력 +10%, 탄속 +10%, 연사력 +8%",
                _ => "최대 단계"
            },

            BuildingType.RecoveryProcessor => level switch
            {
                1 => "스크랩 부품 획득량 +10%",
                2 => "스크랩 부품 획득량 +10%, 회복 자원 효과 +25%",
                3 => "스크랩 부품 획득량 +10%, 회복 자원 효과 +25%, 아이템 흡수 범위 +1.5",
                _ => "최대 단계"
            },

            _ => "효과 없음"
        };
    }

    private bool CanTraitBePurchasedInCurrentSelection(TraitDefinition trait)
    {
        if (trait == null)
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

        StringBuilder builder = new StringBuilder();
        bool wroteAny = false;

        foreach (TraitLevelEffect effect in trait.LevelEffects)
        {
            if (effect == null || effect.Level != level)
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(", ");
            }

            builder.Append(FormatTraitEffect(effect));
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : $"Lv {level} 효과 데이터 없음";
    }

    private string FormatTraitEffect(TraitLevelEffect effect)
    {
        string value = FormatEffectValue(effect.EffectType, effect.Value);

        return effect.EffectType switch
        {
            TraitEffectType.DamagePercent => $"공격력 {value}",
            TraitEffectType.ProjectileSpeedPercent => $"탄속 {value}",
            TraitEffectType.RangePercent => $"사거리 {value}",
            TraitEffectType.MoveSpeedPercent => $"이동속도 {value}",
            TraitEffectType.DashCooldownReduction => $"대쉬 쿨다운 -{effect.Value:0.##}초",
            TraitEffectType.DashDistanceBonus => $"대쉬 거리 +{effect.Value:0.##}",
            TraitEffectType.MaxHpBonus => $"최대 체력 +{effect.Value:0.#}",
            TraitEffectType.HealEfficiencyPercent => $"회복 효율 {value}",
            TraitEffectType.PickupRangeBonus => $"흡수 범위 +{effect.Value:0.##}",
            TraitEffectType.SpreadReductionPercent => $"탄 퍼짐 -{Mathf.Abs(effect.Value):0.#}%",
            TraitEffectType.ProjectileCountBonus => $"탄환 수 +{effect.Value:0}",
            TraitEffectType.PierceCountBonus => $"관통 +{effect.Value:0}",
            TraitEffectType.ChargeTimeReductionPercent => $"차징 시간 -{Mathf.Abs(effect.Value):0.#}%",
            TraitEffectType.ChargeDamagePercent => $"차징 피해 {value}",
            TraitEffectType.HomingAngleBonus => $"유도 각도 +{effect.Value:0.#}°",
            TraitEffectType.HomingRangeBonus => $"유도 거리 +{effect.Value:0.##}",
            TraitEffectType.FireRatePercent => $"연사력 {value}",
            _ => $"{effect.EffectType} {effect.Value:0.##}"
        };
    }

    private string FormatEffectValue(TraitEffectType effectType, float rawValue)
    {
        float absolute = Mathf.Abs(rawValue);
        string sign = rawValue >= 0f ? "+" : "-";
        return $"{sign}{absolute:0.#}%";
    }

    private string FormatCost(int scrapCost, int coreCost)
    {
        scrapCost = Mathf.Max(0, scrapCost);
        coreCost = Mathf.Max(0, coreCost);

        if (coreCost > 0)
        {
            return $"스크랩 부품 {scrapCost}, 코어 조각 {coreCost}";
        }

        return $"스크랩 부품 {scrapCost}";
    }

    private string BuildOwnedAndMissingBlock(int requiredScrap, int requiredCore)
    {
        PermanentProgress progress = PermanentProgress.Instance;
        int ownedScrap = progress != null ? progress.ScrapParts : 0;
        int ownedCore = progress != null ? progress.CoreShards : 0;
        int missingScrap = Mathf.Max(0, requiredScrap - ownedScrap);
        int missingCore = Mathf.Max(0, requiredCore - ownedCore);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("현재 보유:");
        builder.AppendLine($"스크랩 부품 {ownedScrap} / {Mathf.Max(0, requiredScrap)}");
        builder.AppendLine($"코어 조각 {ownedCore} / {Mathf.Max(0, requiredCore)}");

        if (missingScrap > 0 || missingCore > 0)
        {
            builder.AppendLine();
            builder.AppendLine("부족:");
            if (missingScrap > 0)
            {
                builder.AppendLine($"스크랩 부품 {missingScrap}");
            }

            if (missingCore > 0)
            {
                builder.AppendLine($"코어 조각 {missingCore}");
            }
        }

        return builder.ToString();
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
