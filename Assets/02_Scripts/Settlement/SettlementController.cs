using System;
using System.Text;
using UnityEngine;

public class SettlementController : MonoBehaviour
{
    public static SettlementController Instance { get; private set; }

    [Header("Selection")]
    [SerializeField] private WeaponTreeType defaultWeaponTree = WeaponTreeType.MachineGun;

    [Header("Shop")]
    [SerializeField] private int repairScrapCost = 2;
    [SerializeField] private float repairAmount = 999f;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Upgrade")]
    [SerializeField] private int buildingMaxLevel = 3;
    [SerializeField] private int baseUpgradeScrapCost = 5;
    [SerializeField] private int upgradeScrapCostStep = 5;
    [SerializeField] private int coreCostFromLevel = 3;

    private WeaponTreeType selectedWeaponTree;

    public WeaponTreeType SelectedWeaponTree => selectedWeaponTree;
    public int RepairScrapCost => repairScrapCost;
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
        selectedWeaponTree = defaultWeaponTree;

        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Settlement);
        }

        if (PermanentProgress.Instance != null)
        {
            selectedWeaponTree = PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        NotifyChanged();
    }

    public void SelectWeaponTree(WeaponTreeType weaponTreeType)
    {
        selectedWeaponTree = weaponTreeType;

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetLastSelectedWeaponTree(weaponTreeType);
        }

        SetMessage($"출격 무기 선택: {weaponTreeType}");
    }

    public bool TryRepairPlayer()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress != null)
        {
            if (!progress.TrySpend(repairScrapCost, 0))
            {
                SetMessage($"스크랩 부족. 정비 비용: Scrap {repairScrapCost}");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("PermanentProgress가 없습니다. 재화 소모 없이 정비만 테스트합니다.", this);
        }

        if (playerHealth != null)
        {
            playerHealth.Heal(repairAmount);
        }

        SetMessage($"정비 완료. Scrap -{repairScrapCost}");
        return true;
    }

    public bool TryUpgradeBuilding(BuildingType buildingType)
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            SetMessage("PermanentProgress가 없어 강화할 수 없습니다.");
            return false;
        }

        int currentLevel = progress.GetBuildingLevel(buildingType);

        if (currentLevel >= buildingMaxLevel)
        {
            SetMessage($"{GetBuildingDisplayName(buildingType)}은 이미 최대 레벨입니다.");
            return false;
        }

        int nextLevel = currentLevel + 1;
        int scrapCost = GetUpgradeScrapCost(nextLevel);
        int coreCost = GetUpgradeCoreCost(nextLevel);

        if (!progress.TrySpend(scrapCost, coreCost))
        {
            SetMessage($"재화 부족. 필요: Scrap {scrapCost}, Core {coreCost}");
            return false;
        }

        progress.SetBuildingLevel(buildingType, nextLevel);
        SetMessage($"{GetBuildingDisplayName(buildingType)} 강화 완료. Lv {currentLevel} → {nextLevel}");
        return true;
    }

    public void LaunchExpedition()
    {
        if (RunManager.Instance == null)
        {
            SetMessage("RunManager가 없어 출격할 수 없습니다.");
            return;
        }

        SetMessage($"출격 시작: {selectedWeaponTree}");
        RunManager.Instance.StartNewRunAndLoadExpedition(selectedWeaponTree);
    }

    public int GetBuildingLevel(BuildingType buildingType)
    {
        if (PermanentProgress.Instance == null)
        {
            return 0;
        }

        return PermanentProgress.Instance.GetBuildingLevel(buildingType);
    }

    public bool IsBuildingMaxLevel(BuildingType buildingType)
    {
        return GetBuildingLevel(buildingType) >= buildingMaxLevel;
    }

    public int GetNextUpgradeLevel(BuildingType buildingType)
    {
        return Mathf.Min(GetBuildingLevel(buildingType) + 1, buildingMaxLevel);
    }

    public int GetUpgradeScrapCost(int targetLevel)
    {
        return baseUpgradeScrapCost + ((targetLevel - 1) * upgradeScrapCostStep);
    }

    public int GetUpgradeCoreCost(int targetLevel)
    {
        return targetLevel >= coreCostFromLevel ? 1 : 0;
    }

    public string GetUpgradeCostText(BuildingType buildingType)
    {
        if (IsBuildingMaxLevel(buildingType))
        {
            return "MAX";
        }

        int nextLevel = GetNextUpgradeLevel(buildingType);
        int scrapCost = GetUpgradeScrapCost(nextLevel);
        int coreCost = GetUpgradeCoreCost(nextLevel);

        return $"Lv {nextLevel} / Scrap {scrapCost}, Core {coreCost}";
    }

    public string GetBuildingDisplayName(BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.Hangar => "격납고",
            BuildingType.EngineWorkshop => "엔진 공방",
            BuildingType.WeaponLab => "화기 연구소",
            BuildingType.RecoveryProcessor => "회수 처리장",
            _ => buildingType.ToString()
        };
    }

    public string BuildStatusText()
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("[Settlement]");
        builder.AppendLine($"Selected Weapon: {selectedWeaponTree}");

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
}