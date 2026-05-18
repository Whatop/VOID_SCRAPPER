using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Ships/Ship Definition")]
public class ShipDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string shipId = "basic_ship";
    [SerializeField] private string displayName = "기본 기체";
    [TextArea]
    [SerializeField] private string description = "표준형 탐사용 기체.";
    [SerializeField] private Sprite previewSprite;

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

    [Header("Passive")]
    [TextArea]
    [SerializeField] private string passiveDescription = "추가 패시브 없음.";

    public string ShipId => string.IsNullOrWhiteSpace(shipId) ? name : shipId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ShipId : displayName;
    public string Description => description;
    public Sprite PreviewSprite => previewSprite;

    public bool UnlockedByDefault => unlockedByDefault;
    public string RequiredUnlockFlag => requiredUnlockFlag;
    public int RequiredScrapParts => Mathf.Max(0, requiredScrapParts);
    public int RequiredCoreShards => Mathf.Max(0, requiredCoreShards);

    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeedBonusPercent => moveSpeedBonusPercent;
    public float DashDistanceBonus => dashDistanceBonus;
    public float DashCooldownReduction => dashCooldownReduction;
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
