using UnityEngine;

public sealed class WormholeChoiceUI : ExpeditionTravelConfirmationUI
{
    private WormholePortal currentPortal;

    public void Open(WormholePortal portal)
    {
        RunManager runManager = RunManager.Instance;

        if (portal == null || runManager == null ||
            !runManager.CanAdvanceToNextRegion(out ExpeditionDepth nextDepth, out _))
        {
            return;
        }

        currentPortal = portal;
        string nextRegionName = CampaignProgressionCatalog.GetRegionDisplayName(nextDepth);
        OpenModal(
            "다음 해역 진입",
            $"{nextRegionName}으로 이동하시겠습니까?",
            "취소",
            "진입"
        );
    }

    protected override bool ConfirmSelection()
    {
        WormholePortal portal = currentPortal;
        RunManager runManager = RunManager.Instance;

        if (portal == null || runManager == null ||
            !runManager.CanAdvanceToNextRegion(out _, out _))
        {
            return false;
        }

        portal.ConfirmEnterNextArea();
        return true;
    }

    protected override void ClearSelection()
    {
        currentPortal = null;
    }
}
