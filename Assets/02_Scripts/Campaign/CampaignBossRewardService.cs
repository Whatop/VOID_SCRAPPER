using UnityEngine;

public static class CampaignBossRewardService
{
    public static BossCampaignDefinition ResolveDefinition(
        BossCampaignDefinition explicitDefinition,
        ExpeditionDepth depth)
    {
        return explicitDefinition != null
            ? explicitDefinition
            : CampaignProgressionCatalog.GetBossDefinition(depth);
    }

    public static CampaignBossId ResolveBossId(
        BossCampaignDefinition definition,
        ExpeditionDepth depth)
    {
        return definition != null
            ? definition.BossId
            : CampaignProgressionCatalog.GetBossId(depth);
    }

    public static int ResolveCoreShardReward(
        BossCampaignDefinition definition,
        ExpeditionDepth depth)
    {
        return definition != null
            ? definition.CoreShardReward
            : CampaignProgressionCatalog.GetCoreShardReward(depth);
    }

    public static bool GrantGuaranteedPassive(
        BossCampaignDefinition definition,
        Vector2 sourcePosition)
    {
        if (definition == null ||
            !definition.GrantGuaranteedPassiveEachDefeat ||
            definition.GuaranteedPassive == null)
        {
            return false;
        }

        RunRewardChoiceResult result = RunRewardChoiceApplier.Apply(
            RunRewardOption.FromTrait(definition.GuaranteedPassive),
            sourcePosition
        );

        if (result.Success)
        {
            return true;
        }

        // 이미 Mk.III라면 빈 보상이 되지 않도록 튜닝 칩으로 전환합니다.
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.AddCurrency(CurrencyType.TuningChips, 1);
        }

        ExpeditionHUD hud = Object.FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null)
        {
            hud.ShowWarning("보스 전용 패시브가 최대 단계라 튜닝 칩으로 전환되었습니다.");
        }

        return false;
    }
}
