#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The existing F10 owner also owns these controls. No production progression setter.
public sealed partial class DebugItemGrantUI
{
    internal const int PermanentResourceTarget = 9999;
    internal static readonly string[] CampaignCheckpointNames =
    {
        "0 · 튜토리얼 완료 / 첫 정착지 대기",
        "1 · 주요 임무 시작 / 부품 0개",
        "2 · 구획 안정기 회수 / 분석 대기",
        "3 · 1해역 분석 완료 / 2해역 허가",
        "4 · 물질 압축로 회수 / 분석 대기",
        "5 · 2해역 분석 완료 / 3해역 허가",
        "6 · 위상 항법 렌즈 회수 / 복구 가능",
        "7 · 항로 코어 조립 완료",
        "8 · 항로 코어 활성화 / 방어 대기",
        "9 · 정착지 방어 완료 / 최종 출격 가능",
        "10 · 중앙 배차자 격파 / 작전 완료",
        "11 · 3해역 분석 완료 / 코어 조립 전"
    };

    private static readonly string[] CampaignCheckpointFlags =
    {
        "tutorial_completed", StoryProgressionIds.FirstSettlementPendingFlag,
        StoryProgressionIds.FirstSettlementCompleteFlag, MainDamagedAccessKeyQuestIds.StartedUnlockFlag,
        "campaign_boss_sector_administrator_defeated", "campaign_boss_salvage_devourer_defeated",
        "campaign_boss_phase_gatekeeper_defeated", "campaign_boss_null_dispatcher_defeated",
        "campaign_route_core_assembled", "campaign_route_core_activated", "campaign_settlement_defense_cleared",
        PermanentProgress.FinalComponentAnalyzedFlag
    };

    private readonly List<GameObject> itemModeControls = new List<GameObject>();
    private GameObject campaignControlsRoot;
    private TMP_Dropdown campaignCheckpointDropdown;
    private Button resourceMaxButton;
    private Button applyCheckpointButton;
    private bool campaignMode;

    private void BuildCampaignControls(Transform panel)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            GameObject child = panel.GetChild(i).gameObject;
            if (child.name != "TitleText" && child != resultText.gameObject && child != closeButton.gameObject)
                itemModeControls.Add(child);
        }
        TMP_Text title = panel.Find("TitleText").GetComponent<TMP_Text>();
        title.text = "DEV QA";
        SetCenteredRect(title.rectTransform, new Vector2(-110f, 88f), new Vector2(120f, 24f));
        Button mode = CreateButton("CampaignModeButton", panel, "아이템 / 캠페인 QA", new Vector2(73f, 88f), new Vector2(205f, 22f));
        mode.onClick.AddListener(() =>
        {
            campaignMode = !campaignMode;
            RefreshCampaignControls();
        });
        campaignControlsRoot = CreateRectObject("CampaignQA", panel, new Vector2(0f, 15f), new Vector2(360f, 110f));
        CreateText("Heading", campaignControlsRoot.transform, "정착지 / 캠페인 QA · 저장 데이터에 적용", new Vector2(0f, 42f), new Vector2(340f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        resourceMaxButton = CreateButton("ResourceMax", campaignControlsRoot.transform, "자원 MAX", new Vector2(0f, 19f), new Vector2(130f, 21f));
        campaignCheckpointDropdown = CreateDropdown("Checkpoint", campaignControlsRoot.transform, new Vector2(0f, -9f), new Vector2(340f, 23f));
        campaignCheckpointDropdown.ClearOptions();
        campaignCheckpointDropdown.AddOptions(new List<string>(CampaignCheckpointNames));
        applyCheckpointButton = CreateButton("ApplyCheckpoint", campaignControlsRoot.transform, "진행도 적용", new Vector2(0f, -38f), new Vector2(130f, 21f));
        campaignControlsRoot.SetActive(false);
    }

    private static bool CanEditCampaign => GameStateManager.Instance != null &&
        GameStateManager.Instance.CurrentState == GameState.Settlement &&
        PermanentProgress.Instance != null && SaveManager.Instance != null &&
        !(RunManager.Instance != null && RunManager.Instance.HasActiveRun);

    private void RefreshCampaignControls()
    {
        foreach (GameObject control in itemModeControls) control.SetActive(!campaignMode);
        campaignControlsRoot.SetActive(campaignMode);
        resourceMaxButton.interactable = CanEditCampaign;
        applyCheckpointButton.interactable = CanEditCampaign;
        campaignCheckpointDropdown.interactable = CanEditCampaign;
        if (campaignMode)
            SetResult(CanEditCampaign ? StructuralFrameText.DevelopmentStatus(PermanentProgress.Instance) : "캠페인 QA는 정착지에서 사용할 수 있습니다.", CanEditCampaign);
    }

    private void GrantPermanentResources()
    {
        if (!CanEditCampaign) return;
        PermanentProgress progress = PermanentProgress.Instance;
        progress.AddPermanentCurrency(CurrencyType.ScrapParts, Math.Max(0, PermanentResourceTarget - progress.ScrapParts));
        progress.AddPermanentCurrency(CurrencyType.CoreShards, Math.Max(0, PermanentResourceTarget - progress.CoreShards));
        progress.AddPermanentCurrency(CurrencyType.StabilizedAlloy, Math.Max(0, PermanentResourceTarget - progress.StabilizedAlloy));
        SaveManager.Instance.Save(progress);
        SetResult("영구 자원을 각각 9999까지 보충했습니다.", true);
    }

    private void ApplySelectedCheckpoint()
    {
        if (!CanEditCampaign) return;
        SaveData snapshot = CreateCampaignCheckpoint(PermanentProgress.Instance.CreateSaveData(), campaignCheckpointDropdown.value);
        // Save first so a failed disk write cannot pretend that the checkpoint was applied.
        SaveManager.Instance.Save(snapshot);
        if (!ReferenceEquals(SaveManager.Instance.CurrentSaveData, snapshot))
        {
            SetResult("저장 실패 · 진행도를 변경하지 않았습니다.", false);
            return;
        }
        PermanentProgress.Instance.LoadFromSave(snapshot);
        SetResult("진행도 적용 완료\n" + CampaignCheckpointNames[campaignCheckpointDropdown.value], true);
    }

    internal static SaveData CreateCampaignCheckpoint(SaveData source, int checkpoint)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (checkpoint < 0 || checkpoint >= CampaignCheckpointNames.Length) throw new ArgumentOutOfRangeException(nameof(checkpoint));
        bool finalAnalysisComplete = checkpoint >= 7;
        if (checkpoint == 11) checkpoint = 6; // Preserve all established preset indices; this is analysis before assembly.
        SaveData result = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source));
        result.unlockFlags ??= new List<string>();
        foreach (string flag in CampaignCheckpointFlags) result.unlockFlags.RemoveAll(value => value == flag);
        if (finalAnalysisComplete) result.unlockFlags.Add(PermanentProgress.FinalComponentAnalyzedFlag);
        result.unlockFlags.Add("tutorial_completed");
        // All presets are after the Tutorial infection. Optional traits remain untouched.
        if (!result.unlockFlags.Contains("story_trait:pixel_curse")) result.unlockFlags.Add("story_trait:pixel_curse");
        result.unlockFlags.Add(checkpoint == 0 ? StoryProgressionIds.FirstSettlementPendingFlag : StoryProgressionIds.FirstSettlementCompleteFlag);
        if (checkpoint > 0) result.unlockFlags.Add(MainDamagedAccessKeyQuestIds.StartedUnlockFlag);
        result.defeatedCampaignBosses = new List<CampaignBossId>();
        result.acquiredBossStoryParts = new List<BossStoryPart>();
        CampaignBossId[] bosses = { CampaignBossId.SectorAdministrator, CampaignBossId.SalvageDevourer, CampaignBossId.PhaseGatekeeper };
        int parts = checkpoint >= 6 ? 3 : checkpoint >= 4 ? 2 : checkpoint >= 2 ? 1 : 0;
        for (int i = 0; i < parts; i++)
        {
            result.defeatedCampaignBosses.Add(bosses[i]);
            result.acquiredBossStoryParts.Add(CampaignProgressionCatalog.GetStoryPart(bosses[i]));
        }
        result.highestUnlockedDepth = checkpoint >= 5 ? ExpeditionDepth.DeepZone2 : checkpoint >= 3 ? ExpeditionDepth.DeepZone1 : ExpeditionDepth.Normal;
        result.routeCoreState = checkpoint >= 8 ? RouteCoreState.Activated : checkpoint >= 7 ? RouteCoreState.Assembled : checkpoint >= 6 ? RouteCoreState.ReadyToAssemble : RouteCoreState.MissingParts;
        result.settlementDefenseCleared = checkpoint >= 9;
        result.finalBossDefeated = checkpoint == 10;
        if (result.finalBossDefeated) result.defeatedCampaignBosses.Add(CampaignBossId.NullDispatcher);
        return result;
    }
}
#endif
