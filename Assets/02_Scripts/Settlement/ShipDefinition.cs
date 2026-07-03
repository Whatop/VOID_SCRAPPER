using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Ships/Ship Definition")]
public class ShipDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string shipId = "basic_ship";
    [SerializeField] private string displayName = "표준 회수정";
    [TextArea]
    [SerializeField] private string description = "기본 수확용 회수정. 중거리 기관총으로 적을 밀어내고 잔해와 컨테이너를 안정적으로 수확한다.";
    [SerializeField] private Sprite previewSprite;

    [Header("Weapon Tree")]
    [SerializeField] private WeaponTreeType defaultWeaponTree = WeaponTreeType.MachineGun;

    [Header("Unlock")]
    [SerializeField] private bool unlockedByDefault = true;
    [SerializeField] private string unlockFlag;
    [SerializeField] private string requiredUnlockFlag;
    [SerializeField] private int requiredScrapParts;
    [SerializeField] private int requiredCoreShards;

    [Header("Base Stats")]
    [SerializeField] private int maxHp = 20;
    [SerializeField] private float moveSpeedBonusPercent;
    [SerializeField] private float dashDistanceBonus;
    [SerializeField] private float dashCooldownReduction;

    [Header("Harvest / Cargo")]
    [SerializeField] private int cargoCapacity = 100;
    [SerializeField] private float harvestYieldBonusPercent;
    [SerializeField] private float harvestObjectDamageBonusPercent = 10f;
    [Range(0f, 1f)]
    [SerializeField] private float emergencyReturnCapacityRatio = 0.7f;

    [Header("Default Active")]
    [SerializeField] private string defaultReinforcementId = "rf_emergency_return_anchor";

    [Header("Passive")]
    [TextArea]
    [SerializeField] private string passiveDescription = "기본 회수정: 수확 오브젝트 피해 +10%, 기체용량 100.";

    public string ShipId => string.IsNullOrWhiteSpace(shipId) ? name : shipId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ShipId : displayName;
    public string Description => description;
    public Sprite PreviewSprite => previewSprite;

    public WeaponTreeType DefaultWeaponTree => defaultWeaponTree;

    public bool UnlockedByDefault => unlockedByDefault;
    public string RequiredUnlockFlag => requiredUnlockFlag;
    public int RequiredScrapParts => Mathf.Max(0, requiredScrapParts);
    public int RequiredCoreShards => Mathf.Max(0, requiredCoreShards);

    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeedBonusPercent => moveSpeedBonusPercent;
    public float DashDistanceBonus => dashDistanceBonus;
    public float DashCooldownReduction => dashCooldownReduction;

    public int CargoCapacity => Mathf.Max(1, cargoCapacity);
    public float HarvestYieldBonusPercent => harvestYieldBonusPercent;
    public float HarvestObjectDamageBonusPercent => harvestObjectDamageBonusPercent;
    public float EmergencyReturnCapacityRatio => Mathf.Clamp01(emergencyReturnCapacityRatio);
    public string DefaultReinforcementId => string.IsNullOrWhiteSpace(defaultReinforcementId) ? "rf_emergency_return_anchor" : defaultReinforcementId;

    public string PassiveDescription => passiveDescription;

    public string UnlockFlag
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(unlockFlag))
            {
                return unlockFlag;
            }

            return $"ship_{ShipId}_unlocked";
        }
    }
}
