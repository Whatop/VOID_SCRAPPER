using UnityEngine;

public static class CampaignProgressionCatalog
{
    private const string BossDefinitionRoot = "Campaign/BossDefinitions/";

    public static int GetRegionIndex(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1,
            ExpeditionDepth.DeepZone1 => 2,
            ExpeditionDepth.DeepZone2 => 3,
            ExpeditionDepth.FinalNetwork => 4,
            _ => 1
        };
    }

    public static string GetRegionDisplayName(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => "1해역 · 외곽 잔해 해역",
            ExpeditionDepth.DeepZone1 => "2해역 · 물류 환승 구역",
            ExpeditionDepth.DeepZone2 => "3해역 · 중앙 봉쇄 구역",
            ExpeditionDepth.FinalNetwork => "대형 우주 물류망 · 중앙 항로",
            _ => "미확인 해역"
        };
    }

    public static string GetRegionShortName(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => "1해역",
            ExpeditionDepth.DeepZone1 => "2해역",
            ExpeditionDepth.DeepZone2 => "3해역",
            ExpeditionDepth.FinalNetwork => "중앙 물류망",
            _ => depth.ToString()
        };
    }

    public static string GetRegionDescription(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => "넓고 열린 외곽 잔해 해역",
            ExpeditionDepth.DeepZone1 => "항로와 세력 활동이 집중된 물류 환승 구역",
            ExpeditionDepth.DeepZone2 => "방어 시설이 밀집한 중앙 봉쇄 구역",
            ExpeditionDepth.FinalNetwork => "중앙 통제체로 이어지는 유지보수 항로",
            _ => string.Empty
        };
    }

    public static CampaignBossId GetBossId(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => CampaignBossId.SectorAdministrator,
            ExpeditionDepth.DeepZone1 => CampaignBossId.SalvageDevourer,
            ExpeditionDepth.DeepZone2 => CampaignBossId.PhaseGatekeeper,
            ExpeditionDepth.FinalNetwork => CampaignBossId.NullDispatcher,
            _ => CampaignBossId.None
        };
    }

    public static BossStoryPart GetStoryPart(CampaignBossId bossId)
    {
        return bossId switch
        {
            CampaignBossId.SectorAdministrator => BossStoryPart.SectorStabilizer,
            CampaignBossId.SalvageDevourer => BossStoryPart.MatterCompressor,
            CampaignBossId.PhaseGatekeeper => BossStoryPart.PhaseNavigationLens,
            _ => BossStoryPart.None
        };
    }

    public static string GetBossDisplayName(CampaignBossId bossId)
    {
        return bossId switch
        {
            CampaignBossId.SectorAdministrator => "구획 관리자",
            CampaignBossId.SalvageDevourer => "회수 포식자",
            CampaignBossId.PhaseGatekeeper => "위상 관문지기",
            CampaignBossId.NullDispatcher => "중앙 배차자",
            _ => "미확인 관리 개체"
        };
    }

    public static string GetBossSubtitle(CampaignBossId bossId)
    {
        return bossId switch
        {
            CampaignBossId.SectorAdministrator => "SECTOR ADMINISTRATOR",
            CampaignBossId.SalvageDevourer => "SALVAGE DEVOURER",
            CampaignBossId.PhaseGatekeeper => "PHASE GATEKEEPER",
            CampaignBossId.NullDispatcher => "NULL DISPATCHER",
            _ => string.Empty
        };
    }

    public static string GetStoryPartDisplayName(BossStoryPart storyPart)
    {
        return storyPart switch
        {
            BossStoryPart.SectorStabilizer => "구획 안정기",
            BossStoryPart.MatterCompressor => "물질 압축로",
            BossStoryPart.PhaseNavigationLens => "위상 항법 렌즈",
            _ => "없음"
        };
    }

    public static bool TryGetNextExplorationDepth(ExpeditionDepth current, out ExpeditionDepth next)
    {
        switch (current)
        {
            case ExpeditionDepth.Normal:
                next = ExpeditionDepth.DeepZone1;
                return true;

            case ExpeditionDepth.DeepZone1:
                next = ExpeditionDepth.DeepZone2;
                return true;

            default:
                next = current;
                return false;
        }
    }

    public static bool IsFinalNetwork(ExpeditionDepth depth)
    {
        return depth == ExpeditionDepth.FinalNetwork;
    }

    public static bool ShouldUseRepeatBoss(ExpeditionDepth depth, PermanentProgress progress)
    {
        return depth != ExpeditionDepth.FinalNetwork && progress != null &&
               progress.HasDefeatedCampaignBoss(GetBossId(depth));
    }

    public static bool CanAdvanceToNextRegion(RunContext run, PermanentProgress progress)
    {
        return run != null && run.IsActive && run.BossDefeated &&
               !run.FirstStoryClearThisRegion &&
               run.CurrentBossId == GetBossId(run.ExpeditionDepth) &&
               TryGetNextExplorationDepth(run.ExpeditionDepth, out ExpeditionDepth next) &&
               progress != null && progress.IsDepthUnlocked(next);
    }

    public static bool IsCampaignRegion(ExpeditionDepth depth)
    {
        return depth == ExpeditionDepth.Normal ||
               depth == ExpeditionDepth.DeepZone1 ||
               depth == ExpeditionDepth.DeepZone2 ||
               depth == ExpeditionDepth.FinalNetwork;
    }

    public static Vector2 GetDefaultMapSize(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => new Vector2(120f, 120f),
            ExpeditionDepth.DeepZone1 => new Vector2(128f, 128f),
            ExpeditionDepth.DeepZone2 => new Vector2(116f, 116f),
            ExpeditionDepth.FinalNetwork => new Vector2(96f, 96f),
            _ => new Vector2(120f, 120f)
        };
    }

    public static float GetEnemyHpMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => 1.2f,
            ExpeditionDepth.DeepZone2 => 1.4f,
            ExpeditionDepth.FinalNetwork => 1.6f,
            _ => 1f
        };
    }

    public static float GetEnemyDamageMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => 1.15f,
            ExpeditionDepth.DeepZone2 => 1.3f,
            ExpeditionDepth.FinalNetwork => 1.45f,
            _ => 1f
        };
    }

    public static float GetScrapRewardMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => 1.25f,
            ExpeditionDepth.DeepZone2 => 1.45f,
            ExpeditionDepth.FinalNetwork => 1.6f,
            _ => 1f
        };
    }

    public static float GetShopPriceMultiplier(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1f,
            ExpeditionDepth.DeepZone1 => 1.2f,
            ExpeditionDepth.DeepZone2 => 1.35f,
            ExpeditionDepth.FinalNetwork => 1.5f,
            _ => 1f
        };
    }

    public static int GetCoreShardReward(ExpeditionDepth depth)
    {
        return depth switch
        {
            ExpeditionDepth.Normal => 1,
            ExpeditionDepth.DeepZone1 => 2,
            ExpeditionDepth.DeepZone2 => 2,
            ExpeditionDepth.FinalNetwork => 0,
            _ => 1
        };
    }

    public static BossCampaignDefinition GetBossDefinition(ExpeditionDepth depth)
    {
        return GetBossDefinition(GetBossId(depth));
    }

    public static BossCampaignDefinition GetBossDefinition(CampaignBossId bossId)
    {
        string resourceName = bossId switch
        {
            CampaignBossId.SectorAdministrator => "BossCampaign_Region1",
            CampaignBossId.SalvageDevourer => "BossCampaign_Region2",
            CampaignBossId.PhaseGatekeeper => "BossCampaign_Region3",
            CampaignBossId.NullDispatcher => "BossCampaign_Final",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        return Resources.Load<BossCampaignDefinition>(BossDefinitionRoot + resourceName);
    }
}
