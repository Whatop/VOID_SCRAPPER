using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossDummyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private BossDeathPresentation deathPresentation;
    [SerializeField] private NullDispatcherEndingPresentation finalEndingPresentation;

    private BossPatternController bossPatternController;

    [Header("Campaign")]
    [SerializeField] private BossCampaignDefinition campaignDefinition;
    [SerializeField] private bool completeFinalBossAsVictory = true;

    [Header("Campaign Reward Presentation Optional")]
    [SerializeField] private Region2BossCoreRewardPresentation
        region2CoreRewardPresentationPrefab;

    [Header("Exit Object Prefabs")]
    [SerializeField] private GameObject returnBeaconPrefab;
    [SerializeField] private GameObject wormholePortalPrefab;

    [Header("Spawn Positions")]
    [SerializeField] private bool useBossDeathPosition = true;

    [Tooltip("귀환 비콘은 웜홀과 겹치지 않게 보스 사망 위치 기준으로 살짝 옆에 생성합니다.")]
    [SerializeField] private Vector2 returnBeaconSpawnOffset = new Vector2(-1.5f, 0f);

    [Tooltip("웜홀은 기본적으로 보스가 죽은 위치에 생성합니다.")]
    [SerializeField] private Vector2 wormholeSpawnOffset = Vector2.zero;

    [Header("External Config Optional")]
    [SerializeField] private bool hasConfiguredReturnBeaconPosition;
    [SerializeField] private Vector3 configuredReturnBeaconPosition;

    [SerializeField] private bool hasConfiguredWormholePosition;
    [SerializeField] private Vector3 configuredWormholePosition;

    [Header("Selectable Boss Reward")]
    [Tooltip("보스 처치 후 귀환 오브젝트를 열기 전에 선택형 장비 보상을 지급합니다.")]
    [SerializeField] private bool grantSelectableBossReward = true;
    [SerializeField] private int bossRewardChoiceCount = 3;

    [Header("Boss Reward Capsule")]
    [Tooltip("연결하면 보스 사망 즉시 UI를 띄우지 않고, 보상 캡슐을 투하한 뒤 E 상호작용으로 보상을 선택합니다.")]
    [SerializeField] private GameObject bossRewardCapsulePrefab;
    [SerializeField] private Vector2 bossRewardCapsuleSpawnOffset = new Vector2(0f, -1f);

    [Header("State")]
    [SerializeField] private bool changeStateToExpeditionAfterDeath = true;

    private bool deathHandled;
    private bool campaignRewardsHandled;
    private bool postDeathFlowStarted;
    private bool rewardExitCoordinatorStarted;
    private int grantedRegion2CoreAmount;
    private bool recoveredStoryPart;
    private Vector3 resolvedBossDeathPosition;
    private bool hasResolvedDeathContext;
    private CampaignBossId resolvedDeathBossId;
    private BossCampaignDefinition resolvedDeathCampaignDefinition;
    private Coroutine postDeathRoutine;
    private Region2BossCoreRewardPresentation activeRegion2CorePresentation;
    private bool encounterBackgroundRestored;
    private SpaceBackgroundGenerator2D encounterBackgroundGenerator;
    private RunManager observedRunManager;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        deathPresentation = GetComponent<BossDeathPresentation>();
        bossPatternController = GetComponent<BossPatternController>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (deathPresentation == null)
        {
            deathPresentation = GetComponent<BossDeathPresentation>();
        }

        if (bossPatternController == null)
        {
            bossPatternController = GetComponent<BossPatternController>();
        }
    }

    private void OnEnable()
    {
        deathHandled = false;
        campaignRewardsHandled = false;
        postDeathFlowStarted = false;
        rewardExitCoordinatorStarted = false;
        grantedRegion2CoreAmount = 0;
        recoveredStoryPart = false;
        hasResolvedDeathContext = false;
        resolvedBossDeathPosition = transform.position;
        resolvedDeathBossId = CampaignBossId.None;
        resolvedDeathCampaignDefinition = null;
        postDeathRoutine = null;
        activeRegion2CorePresentation = null;
        encounterBackgroundRestored = false;
        encounterBackgroundGenerator = null;
        SubscribeRunEnd();

        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleBossDied;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleBossDied;
        }

        UnsubscribeRunEnd();

        if (postDeathRoutine != null)
        {
            StopCoroutine(postDeathRoutine);
            postDeathRoutine = null;
        }

        CleanupPostDeathPresentation();
        deathPresentation?.CancelPresentation();

        if (!deathHandled)
        {
            RestoreEncounterBackground(false);
        }
    }

    public void ConfigureEncounterBackgroundPresentation(
        SpaceBackgroundGenerator2D backgroundGenerator)
    {
        encounterBackgroundGenerator = backgroundGenerator;
        encounterBackgroundRestored = false;
        SubscribeRunEnd();
    }

    public void ConfigureCampaignDefinition(BossCampaignDefinition definition)
    {
        campaignDefinition = definition;
    }

    // 기존 CoreObject 코드와 호환용.
    // 예전에는 귀환 비콘 위치만 CoreObject에서 넘겨줬기 때문에 그대로 유지한다.
    public void ConfigureReturnBeacon(GameObject beaconPrefab, Vector3 spawnPosition)
    {
        returnBeaconPrefab = beaconPrefab;
        configuredReturnBeaconPosition = spawnPosition;
        hasConfiguredReturnBeaconPosition = true;
    }

    // 새 구조용.
    // CoreObject에서 둘 다 넘기고 싶으면 이 메서드를 호출하면 된다.
    public void ConfigureExitObjects(
        GameObject beaconPrefab,
        Vector3 beaconSpawnPosition,
        GameObject wormholePrefab,
        Vector3 wormholeSpawnPosition)
    {
        returnBeaconPrefab = beaconPrefab;
        configuredReturnBeaconPosition = beaconSpawnPosition;
        hasConfiguredReturnBeaconPosition = true;

        wormholePortalPrefab = wormholePrefab;
        configuredWormholePosition = wormholeSpawnPosition;
        hasConfiguredWormholePosition = true;
    }

    private void HandleBossDied(EnemyHealth health)
    {
        if (AcceptBossDeath())
        {
            postDeathRoutine = StartCoroutine(CompleteBossDeathAfterPresentation());
        }
    }

    private bool AcceptBossDeath()
    {
        if (deathHandled)
        {
            return false;
        }

        deathHandled = true;
        CacheResolvedDeathContext();

        // Permanent recovery/save precedes every optional death/recovery tween.
        ProcessCampaignRewardsOnce();

        if (!ShouldPlayFinalEnding()) RestoreEncounterBackground(true);
        bossPatternController?.StopCombatForDeathPresentation();
        // Do not depend on EnemyHealth.Died subscriber order. Region-specific
        // combat must relinquish its camera/arena before the death sequence starts.
        GetComponent<FrigateTriadBossController>()?.StopCombatForDeathPresentation();
        GetComponent<PhaseGatekeeperBossController>()?.StopCombatForDeathPresentation();
        // End final combat before any death VFX, independently of Died subscriber order.
        if (resolvedDeathBossId == CampaignBossId.NullDispatcher)
            GetComponent<NullDispatcherBossController>()?.CancelEncounter();
        ReleaseBossBattleState();

        return true;
    }

    private void RestoreEncounterBackground(bool playRecoveryOverlay)
    {
        if (encounterBackgroundRestored)
        {
            return;
        }

        encounterBackgroundRestored = true;

        if (encounterBackgroundGenerator != null)
        {
            encounterBackgroundGenerator.RestoreExplorationPresentation(
                -1f,
                playRecoveryOverlay
            );
        }
    }

    private void SubscribeRunEnd()
    {
        RunManager currentRunManager = RunManager.Instance;

        if (observedRunManager == currentRunManager)
        {
            return;
        }

        UnsubscribeRunEnd();
        observedRunManager = currentRunManager;

        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnsubscribeRunEnd()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        if (postDeathRoutine != null)
        {
            StopCoroutine(postDeathRoutine);
            postDeathRoutine = null;
        }
        deathPresentation?.CancelPresentation();
        RestoreEncounterBackground(false);
        CleanupPostDeathPresentation();
    }

    private IEnumerator CompleteBossDeathAfterPresentation()
    {
        try
        {
            if (deathPresentation != null)
            {
                yield return deathPresentation.PlayRoutine(resolvedBossDeathPosition);
            }
        }
        finally
        {
            deathPresentation?.CancelPresentation();
        }

        if (ShouldPlayFinalEnding())
        {
            yield return finalEndingPresentation.PlayRoutine();
            if (!finalEndingPresentation.Completed || IsRunEnding()) yield break;
            RestoreEncounterBackground(true);
        }
        yield return CompleteBossDeathRoutine();
        postDeathRoutine = null;
    }

    private bool ShouldPlayFinalEnding() =>
        resolvedDeathBossId == CampaignBossId.NullDispatcher && completeFinalBossAsVictory &&
        finalEndingPresentation != null && finalEndingPresentation.enabled;

    private IEnumerator CompleteBossDeathRoutine()
    {
        if (postDeathFlowStarted)
        {
            yield break;
        }

        postDeathFlowStarted = true;
        CacheResolvedDeathContext();
        ProcessCampaignRewardsOnce();

        if (resolvedDeathBossId == CampaignBossId.NullDispatcher &&
            completeFinalBossAsVictory)
        {
            if (RunManager.Instance != null &&
                RunManager.Instance.HasActiveRun &&
                !RunManager.Instance.IsCompletingRun)
            {
                RunManager.Instance.CompleteRun(RunEndReason.FinalVictory);
            }

            yield break;
        }

        if (IsRunEnding())
        {
            CleanupPostDeathPresentation();
            yield break;
        }

        CreateRewardExitCoordinator();

    }

    private void ReleaseBossBattleState()
    {
        GameStateManager state = GameStateManager.Instance;
        if (deathHandled && changeStateToExpeditionAfterDeath && !IsRunEnding() &&
            resolvedDeathBossId != CampaignBossId.NullDispatcher &&
            state != null && state.CurrentState == GameState.BossBattle)
        {
            state.ChangeState(GameState.Expedition);
        }
    }

    private void CacheResolvedDeathContext()
    {
        if (hasResolvedDeathContext)
        {
            return;
        }
        hasResolvedDeathContext = true;
        if (resolvedDeathCampaignDefinition == null)
        {
            resolvedDeathCampaignDefinition = ResolveCampaignDefinition();
        }

        if (resolvedDeathBossId == CampaignBossId.None)
        {
            resolvedDeathBossId = ResolveCampaignBossId(
                resolvedDeathCampaignDefinition
            );
        }

        resolvedBossDeathPosition = deathPresentation != null
            ? deathPresentation.ResolvePresentationDeathPosition(transform.position)
            : transform.position;
    }

    private void ProcessCampaignRewardsOnce()
    {
        if (campaignRewardsHandled ||
            RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            RunManager.Instance.IsCompletingRun)
        {
            return;
        }

        campaignRewardsHandled = true;
        bool grantStoryPart = resolvedDeathCampaignDefinition == null ||
                              resolvedDeathCampaignDefinition.GrantStoryPartOnFirstDefeat;

        BossStoryPart part = CampaignProgressionCatalog.GetStoryPart(resolvedDeathBossId);
        bool alreadyRecovered = PermanentProgress.Instance != null &&
            PermanentProgress.Instance.HasBossStoryPart(part);
        RunManager.Instance.MarkBossDefeated(resolvedDeathBossId, grantStoryPart);
        recoveredStoryPart = grantStoryPart && !alreadyRecovered &&
            PermanentProgress.Instance != null && PermanentProgress.Instance.HasBossStoryPart(part);

        if (resolvedDeathBossId == CampaignBossId.SalvageDevourer && grantStoryPart)
        {
            grantedRegion2CoreAmount =
                CampaignBossRewardService.GrantSalvageDevourerCoreReward(
                    resolvedDeathCampaignDefinition
                );
        }

        CampaignBossRewardService.GrantGuaranteedPassive(
            resolvedDeathCampaignDefinition,
            resolvedBossDeathPosition
        );
    }

    private void CleanupPostDeathPresentation()
    {
        finalEndingPresentation?.CancelPresentation();
        Region2BossCoreRewardPresentation presentation =
            activeRegion2CorePresentation;
        activeRegion2CorePresentation = null;

        if (presentation != null)
        {
            presentation.CleanupPresentation();
            Destroy(presentation.gameObject);
        }
    }

    public static void ShowStoryPartRecovery(BossStoryPart part)
    {
        string key = part switch
        {
            BossStoryPart.SectorStabilizer => "system.campaign.recovered.sector_stabilizer",
            BossStoryPart.MatterCompressor => "system.campaign.recovered.matter_compressor",
            BossStoryPart.PhaseNavigationLens => "system.campaign.recovered.phase_navigation_lens",
            _ => null
        };
        if (key != null && VoidScrapperLocalizationService.HasInstance)
        {
            FindFirstObjectByType<ExpeditionHUD>()?.ShowCommunication(
                ShipCommunicationChannel.System,
                VoidScrapperLocalizationService.Instance.GetText(key),
                ShipCommunicationSeverity.Confirmation, 3f);
        }
        if (key != null)
        {
            FindFirstObjectByType<PlayerBuildStatusPanelUI>(FindObjectsInactive.Include)
                ?.PresentStoryPartAcquired(part);
        }
    }

    private static bool IsRunEnding()
    {
        return RunManager.Instance != null && RunManager.Instance.IsCompletingRun;
    }

    private BossCampaignDefinition ResolveCampaignDefinition()
    {
        ExpeditionDepth depth = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.ExpeditionDepth
            : ExpeditionDepth.Normal;

        return CampaignBossRewardService.ResolveDefinition(campaignDefinition, depth);
    }

    private CampaignBossId ResolveCampaignBossId(BossCampaignDefinition definition)
    {
        ExpeditionDepth depth = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.ExpeditionDepth
            : ExpeditionDepth.Normal;

        return CampaignBossRewardService.ResolveBossId(definition, depth);
    }

    private void CreateRewardExitCoordinator()
    {
        if (rewardExitCoordinatorStarted || IsRunEnding())
        {
            return;
        }

        rewardExitCoordinatorStarted = true;
        GameObject coordinatorObject = new GameObject("BossRewardExitCoordinator");
        coordinatorObject.transform.position = resolvedBossDeathPosition;

        BossRewardExitCoordinator coordinator = coordinatorObject.AddComponent<BossRewardExitCoordinator>();
        coordinator.Initialize(
            returnBeaconPrefab,
            resolvedBossDeathPosition,
            wormholePortalPrefab,
            resolvedBossDeathPosition,
            grantSelectableBossReward,
            Mathf.Max(1, bossRewardChoiceCount),
            bossRewardCapsulePrefab,
            ResolveBossRewardCapsuleSpawnPosition(),
            recoveredStoryPart || grantedRegion2CoreAmount > 0
                ? region2CoreRewardPresentationPrefab : null,
            recoveredStoryPart,
            recoveredStoryPart ? CampaignProgressionCatalog.GetStoryPart(resolvedDeathBossId) : BossStoryPart.None,
            resolvedDeathCampaignDefinition != null ? resolvedDeathCampaignDefinition.StoryPartSprite : null
        );
    }


    private Vector3 ResolveBossRewardCapsuleSpawnPosition()
    {
        Vector3 basePosition = useBossDeathPosition ? resolvedBossDeathPosition : Vector3.zero;
        return basePosition + (Vector3)bossRewardCapsuleSpawnOffset;
    }

    private Vector3 ResolveReturnBeaconSpawnPosition()
    {
        if (hasConfiguredReturnBeaconPosition)
        {
            return configuredReturnBeaconPosition;
        }

        Vector3 basePosition = useBossDeathPosition ? transform.position : Vector3.zero;
        return basePosition + (Vector3)returnBeaconSpawnOffset;
    }

    private Vector3 ResolveWormholeSpawnPosition()
    {
        if (hasConfiguredWormholePosition)
        {
            return configuredWormholePosition;
        }

        Vector3 basePosition = useBossDeathPosition ? transform.position : Vector3.zero;
        return basePosition + (Vector3)wormholeSpawnOffset;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Region 2 Reward/Force Authoritative Reward Flow")]
    private void DevelopmentForceRegion2RewardFlow()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        CacheResolvedDeathContext();
        if (postDeathRoutine == null)
        {
            postDeathRoutine = StartCoroutine(CompleteBossDeathRoutine());
        }
    }

    [ContextMenu("Development/Region 2 Reward/Force First-Defeat Path")]
    private void DevelopmentForceFirstDefeatRewardPath()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        if (PermanentProgress.Instance != null &&
            PermanentProgress.Instance.HasDefeatedCampaignBoss(
                CampaignBossId.SalvageDevourer))
        {
            Debug.LogWarning(
                "The current save already records Salvage Devourer; use a clean first-defeat save.",
                this
            );
            return;
        }

        DevelopmentForceRegion2RewardFlow();
    }

    [ContextMenu("Development/Region 2 Reward/Simulate Repeat-Defeat Path")]
    private void DevelopmentSimulateRepeatDefeatRewardPath()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        if (PermanentProgress.Instance == null ||
            !PermanentProgress.Instance.HasDefeatedCampaignBoss(
                CampaignBossId.SalvageDevourer))
        {
            Debug.LogWarning(
                "Repeat-defeat testing requires a save that already defeated Salvage Devourer.",
                this
            );
            return;
        }

        DevelopmentForceRegion2RewardFlow();
    }

    [ContextMenu("Development/Region 2 Reward/Show Orange Core Presentation")]
    private void DevelopmentShowOrangeCorePresentation()
    {
        if (!DevelopmentCanUseRegion2RewardControls() ||
            region2CoreRewardPresentationPrefab == null ||
            activeRegion2CorePresentation != null)
        {
            return;
        }

        CacheResolvedDeathContext();
        activeRegion2CorePresentation = Instantiate(
            region2CoreRewardPresentationPrefab,
            resolvedBossDeathPosition,
            Quaternion.identity
        );
        if (activeRegion2CorePresentation != null)
        {
            StartCoroutine(DevelopmentPlayOrangeCoreRoutine(
                activeRegion2CorePresentation
            ));
        }
    }

    private IEnumerator DevelopmentPlayOrangeCoreRoutine(
        Region2BossCoreRewardPresentation presentation)
    {
        yield return presentation.PlayRoutine(resolvedBossDeathPosition);
        if (activeRegion2CorePresentation == presentation)
        {
            activeRegion2CorePresentation = null;
        }

        if (presentation != null)
        {
            Destroy(presentation.gameObject);
        }
    }

    [ContextMenu("Development/Region 2 Reward/Complete Orange Core Presentation")]
    private void DevelopmentCompleteOrangeCorePresentation()
    {
        activeRegion2CorePresentation?.CompleteImmediately();
    }

    [ContextMenu("Development/Region 2 Reward/Inspect Configured Reward")]
    private void DevelopmentInspectConfiguredRegion2Reward()
    {
        BossCampaignDefinition definition = ResolveCampaignDefinition();
        int amount = CampaignBossRewardService.ResolveCoreShardReward(
            definition,
            ExpeditionDepth.DeepZone1
        );
        bool storyPartOwned = PermanentProgress.Instance != null &&
                              PermanentProgress.Instance.HasBossStoryPart(
                                  BossStoryPart.MatterCompressor
                              );
        bool coreGrantedThisRun = RunManager.Instance != null &&
                                  RunManager.Instance.HasActiveRun &&
                                  RunManager.Instance.CurrentRun
                                      .HasGrantedGuaranteedBossCoreRewardThisRun(
                                          CampaignBossId.SalvageDevourer
                                      );
        Debug.Log(
            $"[SalvageDevourer/Reward] configuredCore={amount} " +
            $"grantedThisRun={coreGrantedThisRun} " +
            $"matterCompressorOwned={storyPartOwned} " +
            $"campaignHandled={campaignRewardsHandled} " +
            $"coordinatorStarted={rewardExitCoordinatorStarted}",
            this
        );
    }

    [ContextMenu("Development/Region 2 Reward/Verify Core Duplicate Guard")]
    private void DevelopmentVerifyCoreDuplicateGuard()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        BossCampaignDefinition definition = ResolveCampaignDefinition();
        int first = CampaignBossRewardService.GrantSalvageDevourerCoreReward(
            definition
        );
        int duplicate = CampaignBossRewardService.GrantSalvageDevourerCoreReward(
            definition
        );
        Debug.Log(
            $"[SalvageDevourer/Reward] firstGrant={first} duplicateGrant={duplicate}",
            this
        );
    }

    [ContextMenu("Development/Region 2 Reward/Verify Story-Part Duplicate Guard")]
    private void DevelopmentVerifyStoryPartDuplicateGuard()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        bool first = RunManager.Instance.MarkBossDefeated(
            CampaignBossId.SalvageDevourer,
            true
        );
        bool duplicate = RunManager.Instance.MarkBossDefeated(
            CampaignBossId.SalvageDevourer,
            true
        );
        Debug.Log(
            $"[SalvageDevourer/Reward] firstPermanentChange={first} " +
            $"duplicatePermanentChange={duplicate}",
            this
        );
    }

    [ContextMenu("Development/Region 2 Reward/Continue Selectable Reward")]
    private void DevelopmentContinueSelectableReward()
    {
        if (!DevelopmentCanUseRegion2RewardControls())
        {
            return;
        }

        CreateRewardExitCoordinator();
    }

    [ContextMenu("Development/Region 2 Reward/Cleanup Reward Flow")]
    private void DevelopmentCleanupRewardFlow()
    {
        CleanupPostDeathPresentation();
    }

    private bool DevelopmentCanUseRegion2RewardControls()
    {
        if (RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            RunManager.Instance.IsCompletingRun ||
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1 ||
            RunManager.Instance.CurrentRun.CurrentBossId !=
                CampaignBossId.SalvageDevourer)
        {
            Debug.LogWarning(
                "Region-2 reward controls require an active DeepZone1 / SalvageDevourer run.",
                this
            );
            return false;
        }

        return true;
    }
#endif
}
