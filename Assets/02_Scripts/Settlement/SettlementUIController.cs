using UnityEngine;
using UnityEngine.UI;

public enum SettlementSelectionKind
{
    None,
    Repair,
    Building,
    Weapon,
    Launch
}

public class SettlementUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;
    [SerializeField] private SettlementHUD hud;

    [Header("Buttons")]
    [SerializeField] private Button actionButton;
    [SerializeField] private Button launchButton;

    [Header("Start")]
    [SerializeField] private bool selectRepairOnStart = true;

    private SettlementSelectionKind selectedKind = SettlementSelectionKind.None;
    private BuildingType selectedBuilding;
    private WeaponTreeType selectedWeapon;

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }

        if (hud == null)
        {
            hud = FindFirstObjectByType<SettlementHUD>();
        }
    }

    private void OnEnable()
    {
        if (settlementController != null)
        {
            settlementController.Changed += Refresh;
        }

        if (actionButton != null)
        {
            actionButton.onClick.AddListener(ExecuteSelectedAction);
        }

        if (launchButton != null)
        {
            launchButton.onClick.AddListener(LaunchExpedition);
        }
    }

    private void Start()
    {
        if (selectRepairOnStart)
        {
            SelectRepair();
        }
        else
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(ExecuteSelectedAction);
        }

        if (launchButton != null)
        {
            launchButton.onClick.RemoveListener(LaunchExpedition);
        }
    }

    public void SelectRepair()
    {
        selectedKind = SettlementSelectionKind.Repair;
        Refresh();
    }

    public void SelectBuildingHangar()
    {
        SelectBuilding(BuildingType.Hangar);
    }

    public void SelectBuildingEngineWorkshop()
    {
        SelectBuilding(BuildingType.EngineWorkshop);
    }

    public void SelectBuildingWeaponLab()
    {
        SelectBuilding(BuildingType.WeaponLab);
    }

    public void SelectBuildingRecoveryProcessor()
    {
        SelectBuilding(BuildingType.RecoveryProcessor);
    }

    public void SelectBuilding(BuildingType buildingType)
    {
        selectedKind = SettlementSelectionKind.Building;
        selectedBuilding = buildingType;
        Refresh();
    }

    public void SelectWeaponShotgun()
    {
        SelectWeapon(WeaponTreeType.Shotgun);
    }

    public void SelectWeaponSniper()
    {
        SelectWeapon(WeaponTreeType.Sniper);
    }

    public void SelectWeaponMachineGun()
    {
        SelectWeapon(WeaponTreeType.MachineGun);
    }

    public void SelectWeapon(WeaponTreeType weaponTreeType)
    {
        selectedKind = SettlementSelectionKind.Weapon;
        selectedWeapon = weaponTreeType;
        Refresh();
    }

    public void SelectLaunchPreparation()
    {
        selectedKind = SettlementSelectionKind.Launch;
        Refresh();
    }

    public void ExecuteSelectedAction()
    {
        if (settlementController == null)
        {
            return;
        }

        switch (selectedKind)
        {
            case SettlementSelectionKind.Repair:
                settlementController.TryRepairPlayer();
                break;

            case SettlementSelectionKind.Building:
                settlementController.TryUpgradeBuilding(selectedBuilding);
                break;

            case SettlementSelectionKind.Weapon:
                settlementController.SelectWeaponTree(selectedWeapon);
                break;

            case SettlementSelectionKind.Launch:
                LaunchExpedition();
                break;
        }

        Refresh();
    }

    public void LaunchExpedition()
    {
        if (settlementController == null)
        {
            return;
        }

        settlementController.LaunchExpedition();
    }

    private void Refresh()
    {
        if (settlementController == null || hud == null)
        {
            return;
        }

        hud.Refresh();
        hud.SetLaunchLabel($"탐사 시작 / {GetWeaponDisplayName(settlementController.SelectedWeaponTree)}");

        string title;
        string body;
        string actionLabel;
        bool canExecute;

        BuildSelectedDetail(out title, out body, out actionLabel, out canExecute);

        hud.SetDetail(title, body);
        hud.SetActionLabel(actionLabel);

        if (actionButton != null)
        {
            actionButton.interactable = canExecute;
        }

        if (launchButton != null)
        {
            launchButton.interactable = true;
        }
    }

    private void BuildSelectedDetail(
        out string title,
        out string body,
        out string actionLabel,
        out bool canExecute)
    {
        title = "정착지";
        body = "좌측 메뉴 또는 중앙 구역을 선택하세요.";
        actionLabel = "실행";
        canExecute = false;

        if (settlementController == null)
        {
            return;
        }

        switch (selectedKind)
        {
            case SettlementSelectionKind.Repair:
                title = "정비소";
                body =
                    "탐사 후 기체를 정비하는 구역입니다.\n\n" +
                    $"비용: 스크랩 부품 {settlementController.RepairScrapCost}\n" +
                    "효과: 현재 체력 회복\n\n" +
                    "프로토타입에서는 정비 비용 소모와 메시지 갱신만 먼저 확인합니다.";
                actionLabel = "정비 실행";
                canExecute = true;
                break;

            case SettlementSelectionKind.Building:
                BuildBuildingDetail(out title, out body, out actionLabel, out canExecute);
                break;

            case SettlementSelectionKind.Weapon:
                BuildWeaponDetail(out title, out body, out actionLabel, out canExecute);
                break;

            case SettlementSelectionKind.Launch:
                title = "출격 준비";
                body =
                    "현재 선택된 무기 트리로 탐사를 시작합니다.\n\n" +
                    $"선택 무기: {GetWeaponDisplayName(settlementController.SelectedWeaponTree)}\n" +
                    "해역: 일반 해역\n\n" +
                    "출격하면 Settlement Scene에서 Expedition Scene으로 이동합니다.";
                actionLabel = "탐사 시작";
                canExecute = true;
                break;
        }
    }

    private void BuildBuildingDetail(
        out string title,
        out string body,
        out string actionLabel,
        out bool canExecute)
    {
        int level = settlementController.GetBuildingLevel(selectedBuilding);
        bool isMaxLevel = settlementController.IsBuildingMaxLevel(selectedBuilding);

        string buildingName = settlementController.GetBuildingDisplayName(selectedBuilding);
        string currentEffect = GetBuildingEffectText(selectedBuilding, level);
        string nextEffect = isMaxLevel
            ? "최대 단계입니다."
            : GetBuildingEffectText(selectedBuilding, level + 1);

        title = buildingName;
        body =
            $"{GetBuildingDescription(selectedBuilding)}\n\n" +
            $"현재 단계: Lv {level}\n" +
            $"현재 효과: {currentEffect}\n\n" +
            $"다음 효과: {nextEffect}\n" +
            $"요구 재화: {settlementController.GetUpgradeCostText(selectedBuilding)}";

        actionLabel = isMaxLevel ? "최대 강화" : "강화 실행";
        canExecute = !isMaxLevel;
    }

    private void BuildWeaponDetail(
        out string title,
        out string body,
        out string actionLabel,
        out bool canExecute)
    {
        bool alreadySelected = settlementController.SelectedWeaponTree == selectedWeapon;

        title = GetWeaponDisplayName(selectedWeapon);
        body =
            $"{GetWeaponDescription(selectedWeapon)}\n\n" +
            $"현재 선택 무기: {GetWeaponDisplayName(settlementController.SelectedWeaponTree)}\n" +
            $"선택할 무기: {GetWeaponDisplayName(selectedWeapon)}";

        actionLabel = alreadySelected ? "이미 선택됨" : "무기 선택";
        canExecute = !alreadySelected;
    }

    private string GetBuildingDescription(BuildingType buildingType)
    {
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

        return buildingType switch
        {
            BuildingType.Hangar => level switch
            {
                1 => "최대 체력 +2",
                2 => "최대 체력 +4, 수리 효율 +1",
                3 => "최대 체력 +6, 수리 효율 +2",
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
                3 => "스크랩 부품 획득량 +10%, 회복 자원 효과 +25%, 아이템 흡수 범위 +1.5, 크레딧 획득량 +10%",
                _ => "최대 단계"
            },

            _ => "효과 없음"
        };
    }

    private string GetWeaponDisplayName(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => "근접 + 샷건",
            WeaponTreeType.Sniper => "관통 스나이퍼",
            WeaponTreeType.MachineGun => "기관총",
            _ => weaponTreeType.ToString()
        };
    }

    private string GetWeaponDescription(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun =>
                "대쉬로 진입한 뒤 근거리 산탄으로 순간 화력을 넣고 빠지는 돌입형 무기 트리입니다.",

            WeaponTreeType.Sniper =>
                "좌클릭 유지로 차징하고, 버튼을 떼면 관통탄을 발사하는 장거리 정리형 무기 트리입니다.",

            WeaponTreeType.MachineGun =>
                "좌클릭 유지로 지속 사격하며 중거리에서 안정적으로 적을 추적하는 지속 전투형 무기 트리입니다.",

            _ => "무기 트리 설명 없음."
        };
    }
}