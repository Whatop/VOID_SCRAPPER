using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Campaign/Boss Campaign Definition")]
public class BossCampaignDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private CampaignBossId bossId = CampaignBossId.SectorAdministrator;
    [SerializeField] private ExpeditionDepth expeditionDepth = ExpeditionDepth.Normal;
    [SerializeField] private string displayName = "구획 관리자";
    [SerializeField] private string subtitle = "SECTOR ADMINISTRATOR";

    [Header("Permanent Story Reward")]
    [SerializeField] private BossStoryPart storyPart = BossStoryPart.SectorStabilizer;
    [SerializeField] private bool grantStoryPartOnFirstDefeat = true;
    [Tooltip("Optional dedicated art. None uses the authored world recovery core and hides the inventory icon.")]
    [SerializeField] private Sprite storyPartSprite;

    [Header("Guaranteed Run Passive")]
    [SerializeField] private TraitDefinition guaranteedPassive;
    [SerializeField] private bool grantGuaranteedPassiveEachDefeat = true;

    [Header("World Reward")]
    [SerializeField] private int coreShardReward = 1;

    public CampaignBossId BossId => bossId;
    public ExpeditionDepth ExpeditionDepth => expeditionDepth;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? bossId.ToString() : displayName;
    public string Subtitle => subtitle ?? string.Empty;
    public BossStoryPart StoryPart => storyPart;
    public Sprite StoryPartSprite => storyPartSprite;
    public bool GrantStoryPartOnFirstDefeat => grantStoryPartOnFirstDefeat;
    public TraitDefinition GuaranteedPassive => guaranteedPassive;
    public bool GrantGuaranteedPassiveEachDefeat => grantGuaranteedPassiveEachDefeat;
    public int CoreShardReward => Mathf.Max(0, coreShardReward);
}
