using System;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private GameBalanceConfig balanceConfig;

    [Header("Debug")]
    [SerializeField] private RunContext currentRun;

    private PlayerRuntimeStatApplier currentPlayerStatApplier;
    private bool runEndingActive;
    private bool runCompletionFinalized;
    private bool runEndingPauseOwned;
    private bool runEndingWorldSfxSuppressed;
    private RunEndReason runEndingReason = RunEndReason.None;

    public RunContext CurrentRun => currentRun;
    public bool HasActiveRun => currentRun != null && currentRun.IsActive;
    public bool IsCompletingRun => runEndingActive;

    public event Action<RunContext> RunStarted;
    public event Action<RunWallet> WalletChanged;
    public event Action<RunResultData> RunEnded;
    public event Action<CampaignBossId, bool> CampaignBossDefeated;
    public event Action<ExpeditionDepth, ExpeditionDepth> RegionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("RunManager가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        currentPlayerStatApplier = null;
        UnsubscribeWallet();
        ReleaseRunEndingPresentationOwnership();
    }

    public void RegisterCurrentPlayer(PlayerRuntimeStatApplier statApplier)
    {
        currentPlayerStatApplier = statApplier;
    }

    public void SetBalanceConfig(GameBalanceConfig config)
    {
        balanceConfig = config;
    }

    public void StartNewRun(WeaponTreeType selectedWeaponTree)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal, ResolveSelectedShipId(null));
    }

    public void StartNewRun(WeaponTreeType selectedWeaponTree, string selectedShipId)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal, selectedShipId);
    }

    public void StartNewRun(WeaponTreeType selectedWeaponTree, ExpeditionDepth depth)
    {
        StartNewRun(selectedWeaponTree, depth, ResolveSelectedShipId(null));
    }

    public void StartNewRun(WeaponTreeType selectedWeaponTree, ExpeditionDepth depth, string selectedShipId)
    {
        StartNewRun(selectedWeaponTree, depth, selectedShipId, SeaRegionCatalog.GetRandom());
    }

    public void StartNewRun(
        WeaponTreeType selectedWeaponTree,
        ExpeditionDepth depth,
        string selectedShipId,
        SeaRegionType seaRegionType)
    {
        if (depth != ExpeditionDepth.Normal &&
            PermanentProgress.Instance != null &&
            !PermanentProgress.Instance.IsDepthUnlocked(depth))
        {
            Debug.LogWarning($"아직 해금되지 않은 해역입니다: {CampaignProgressionCatalog.GetRegionDisplayName(depth)}", this);
            return;
        }

        ResetRunEndingState();
        UnsubscribeWallet();
        currentPlayerStatApplier = null;
        selectedShipId = ResolveSelectedShipId(selectedShipId);

        currentRun = new RunContext(selectedWeaponTree, depth, selectedShipId, seaRegionType);
        currentRun.Wallet.Changed += HandleWalletChanged;

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetLastSelectedWeaponTree(selectedWeaponTree);
            PermanentProgress.Instance.SetSelectedShipId(selectedShipId);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.ExpeditionLoading);
        }

        RunStarted?.Invoke(currentRun);
        WalletChanged?.Invoke(currentRun.Wallet);

        Debug.Log(
            $"새 탐사를 시작합니다. WeaponTree: {selectedWeaponTree}, Ship: {selectedShipId}, " +
            $"Region: {CampaignProgressionCatalog.GetRegionDisplayName(depth)}, " +
            $"SeaRegion: {SeaRegionCatalog.GetDisplayName(seaRegionType)}",
            this
        );
    }

    public void StartNewRunAndLoadExpedition(WeaponTreeType selectedWeaponTree)
    {
        StartNewRunAndLoadExpedition(selectedWeaponTree, ResolveSelectedShipId(null));
    }

    public void StartNewRunAndLoadExpedition(WeaponTreeType selectedWeaponTree, string selectedShipId)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal, selectedShipId);
        LoadExpeditionScene();
    }

    public bool StartFinalExpeditionAndLoad()
    {
        WeaponTreeType selectedWeapon = PermanentProgress.Instance != null
            ? PermanentProgress.Instance.LastSelectedWeaponTree
            : WeaponTreeType.MachineGun;

        return StartFinalExpeditionAndLoad(selectedWeapon, ResolveSelectedShipId(null));
    }

    public bool StartFinalExpeditionAndLoad(WeaponTreeType selectedWeaponTree, string selectedShipId)
    {
        if (PermanentProgress.Instance == null || !PermanentProgress.Instance.CanLaunchFinalExpedition)
        {
            Debug.LogWarning("완전 코어 활성화와 정착지 방어를 완료해야 중앙 물류망으로 출격할 수 있습니다.", this);
            return false;
        }

        StartNewRun(selectedWeaponTree, ExpeditionDepth.FinalNetwork, selectedShipId);

        if (!HasActiveRun || currentRun.ExpeditionDepth != ExpeditionDepth.FinalNetwork)
        {
            return false;
        }

        LoadExpeditionScene();
        return true;
    }

    public void AddCurrency(CurrencyType currencyType, int amount)
    {
        AddCurrencyRespectingCargo(currencyType, amount);
    }

    public int AddCurrencyRespectingCargo(CurrencyType currencyType, int amount)
    {
        if (!HasActiveRun)
        {
            Debug.LogWarning("활성화된 탐사가 없어 재화를 지급할 수 없습니다.", this);
            return 0;
        }

        amount = Mathf.Max(0, amount);

        if (amount <= 0)
        {
            return 0;
        }

        int acceptedAmount = currentRun.GetAcceptedAmountByCargo(currencyType, amount);

        if (acceptedAmount <= 0)
        {
            if (currentRun.UsesCargo(currencyType))
            {
                Debug.Log("기체 용량이 가득 차서 자원을 더 실을 수 없습니다.", this);
            }

            return 0;
        }

        currentRun.Wallet.Add(currencyType, acceptedAmount);
        return acceptedAmount;
    }

    public int GrantGuaranteedCampaignBossCoreShards(
        CampaignBossId bossId,
        int amount)
    {
        amount = Mathf.Max(0, amount);
        if (!HasActiveRun ||
            IsCompletingRun ||
            bossId == CampaignBossId.None ||
            currentRun.CurrentBossId != bossId ||
            amount <= 0 ||
            !currentRun.TryRegisterGuaranteedBossCoreReward(bossId, amount))
        {
            return 0;
        }

        currentRun.Wallet.Add(CurrencyType.CoreShards, amount);
        return amount;
    }

    public bool CanAddCargoCurrency(CurrencyType currencyType, int amount = 1)
    {
        return HasActiveRun && currentRun.GetAcceptedAmountByCargo(currencyType, amount) > 0;
    }

    public bool TryRemoveCargoCurrency(CurrencyType currencyType, int amount)
    {
        return HasActiveRun &&
               currentRun.UsesCargo(currencyType) &&
               currentRun.Wallet.TrySpend(currencyType, Mathf.Max(0, amount));
    }

    public bool TrySpendCredits(int amount)
    {
        return HasActiveRun && currentRun.Wallet.TrySpend(CurrencyType.Credits, amount);
    }

    public void MarkBossDefeated()
    {
        MarkBossDefeated(
            HasActiveRun ? currentRun.CurrentBossId : CampaignBossId.None,
            true
        );
    }

    public bool MarkBossDefeated(CampaignBossId bossId, bool grantStoryPart)
    {
        if (!HasActiveRun)
        {
            return false;
        }

        if (bossId == CampaignBossId.None)
        {
            bossId = CampaignProgressionCatalog.GetBossId(currentRun.ExpeditionDepth);
        }

        bool firstRunDefeat = !currentRun.HasDefeatedBossThisRun(bossId);
        currentRun.MarkBossDefeated(bossId);

        bool firstPermanentDefeat = false;

        if (PermanentProgress.Instance != null)
        {
            firstPermanentDefeat = PermanentProgress.Instance.RegisterCampaignBossDefeat(
                bossId,
                grantStoryPart
            );

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save(PermanentProgress.Instance);
            }
        }

        if (firstRunDefeat)
        {
            CampaignBossDefeated?.Invoke(bossId, firstPermanentDefeat);
        }

        return firstPermanentDefeat;
    }

    public void SetShopHostileThisRun(bool hostile)
    {
        if (HasActiveRun)
        {
            currentRun.SetShopHostile(hostile);
        }
    }

    public bool CanAdvanceToNextRegion(out ExpeditionDepth nextDepth, out string blockReason)
    {
        nextDepth = HasActiveRun ? currentRun.ExpeditionDepth : ExpeditionDepth.Normal;
        blockReason = string.Empty;

        if (!HasActiveRun)
        {
            blockReason = "활성화된 탐사가 없습니다.";
            return false;
        }

        if (!currentRun.BossDefeated)
        {
            blockReason = "현재 해역 보스를 먼저 처치해야 합니다.";
            return false;
        }

        if (!CampaignProgressionCatalog.TryGetNextExplorationDepth(currentRun.ExpeditionDepth, out nextDepth))
        {
            blockReason = currentRun.ExpeditionDepth == ExpeditionDepth.DeepZone2
                ? "3해역 이후 중앙 물류망은 정착지의 완전 코어에서 출격해야 합니다."
                : "더 깊은 일반 해역이 없습니다.";
            return false;
        }

        if (PermanentProgress.Instance != null && !PermanentProgress.Instance.IsDepthUnlocked(nextDepth))
        {
            blockReason = $"{CampaignProgressionCatalog.GetRegionShortName(nextDepth)}이 아직 해금되지 않았습니다.";
            return false;
        }

        return true;
    }

    public bool AdvanceToNextRegion()
    {
        if (!CanAdvanceToNextRegion(out ExpeditionDepth nextDepth, out string blockReason))
        {
            Debug.LogWarning(blockReason, this);
            return false;
        }

        if (!CaptureCurrentPlayerVitals())
        {
            Debug.LogError("다음 해역 진입 전에 Player HP/Armor 상태를 저장하지 못했습니다.", this);
            return false;
        }

        ExpeditionDepth previousDepth = PrepareNextRegionState(nextDepth);

        Debug.Log(
            $"위상 분기 항로 진입: {CampaignProgressionCatalog.GetRegionDisplayName(previousDepth)} → " +
            $"{CampaignProgressionCatalog.GetRegionDisplayName(nextDepth)}",
            this
        );

        LoadExpeditionScene();
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool TryAdvanceToNextRegionForDevelopment(out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasActiveRun)
        {
            resultMessage = "No active Expedition run exists.";
            return false;
        }

        ExpeditionDepth previousDepth = currentRun.ExpeditionDepth;

        if (!CampaignProgressionCatalog.TryGetNextExplorationDepth(previousDepth, out ExpeditionDepth nextDepth))
        {
            resultMessage = previousDepth == ExpeditionDepth.DeepZone2
                ? "Region 3 is the last normal exploration Region. Final Expedition must be launched separately."
                : "No next normal exploration Region is available.";
            return false;
        }

        SceneFlowManager sceneFlow = SceneFlowManager.Instance;
        if (sceneFlow == null || sceneFlow.IsLoading)
        {
            resultMessage = sceneFlow == null
                ? "SceneFlowManager is unavailable."
                : "A scene transition is already in progress.";
            return false;
        }

        bool capturedVitals = CaptureCurrentPlayerVitals();
        PrepareNextRegionState(nextDepth);
        LoadExpeditionScene();

        resultMessage =
            $"Advancing to {CampaignProgressionCatalog.GetRegionShortName(nextDepth)}" +
            (capturedVitals ? "." : " (Player vitals were unavailable.)");
        return true;
    }
#endif

    private ExpeditionDepth PrepareNextRegionState(ExpeditionDepth nextDepth)
    {
        ExpeditionDepth previousDepth = currentRun.ExpeditionDepth;
        SeaRegionType nextSeaRegion = SeaRegionCatalog.GetRandom(currentRun.SeaRegionType);

        currentRun.PrepareNextRegion(nextDepth, nextSeaRegion);
        currentPlayerStatApplier = null;
        RegionChanged?.Invoke(previousDepth, nextDepth);
        return previousDepth;
    }

    private bool CaptureCurrentPlayerVitals()
    {
        if (!HasActiveRun || currentPlayerStatApplier == null)
        {
            return false;
        }

        if (!currentPlayerStatApplier.TryGetCurrentVitals(out float currentHp, out float currentArmor))
        {
            return false;
        }

        currentRun.CapturePlayerVitals(currentHp, currentArmor);
        return true;
    }

    // 기존 호출부 호환용.
    public void EnterDeepZone1()
    {
        AdvanceToNextRegion();
    }

    public RunResultData CompleteRun(RunEndReason reason)
    {
        if (!HasActiveRun)
        {
            Debug.LogWarning("완료할 탐사가 없습니다.", this);
            return null;
        }

        if (!runEndingActive)
        {
            if (!TryBeginRunEnding(reason, true))
            {
                return null;
            }
        }
        else if (runCompletionFinalized || runEndingReason != reason)
        {
            return null;
        }

        SuppressRunEndingWorldSfx();
        runCompletionFinalized = true;

        GameAudioLoopController.BeginRunEndMusicTransition();

        RunResultData resultData = CreateRunResult(reason, currentRun);

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.ApplyRunResult(resultData);
        }

        if (SaveManager.Instance != null && PermanentProgress.Instance != null)
        {
            SaveManager.Instance.Save(PermanentProgress.Instance);
        }

        currentRun.End();
        currentPlayerStatApplier = null;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.RunResult);
        }

        RunEnded?.Invoke(resultData);

        Debug.Log(
            $"탐사 종료: {reason}, Scrap Commit: {resultData.committedScrapParts}, " +
            $"Core Commit: {resultData.committedCoreShards}",
            this
        );

        return resultData;
    }

    public bool TryBeginRunEnding(RunEndReason reason, bool suppressWorldSfx = true)
    {
        if (!HasActiveRun || runEndingActive || reason == RunEndReason.None)
        {
            return false;
        }

        runEndingActive = true;
        runEndingReason = reason;
        GameplayPauseManager.Instance.PushPause(this, $"Run Ending: {reason}");
        runEndingPauseOwned = true;

        if (suppressWorldSfx)
        {
            SuppressRunEndingWorldSfx();
        }

        return true;
    }

    public bool AbandonActiveRunWithoutRewards()
    {
        if (!HasActiveRun || runEndingActive)
        {
            return false;
        }

        runEndingActive = true;
        runCompletionFinalized = true;
        runEndingReason = RunEndReason.None;
        GameplayPauseManager.Instance.PushPause(this, "Abandon Active Run");
        runEndingPauseOwned = true;
        SuppressRunEndingWorldSfx();
        AudioManager.StopAllLoops();

        UnsubscribeWallet();
        currentRun.End();
        currentRun = null;
        currentPlayerStatApplier = null;
        return true;
    }

    public void ReleaseRunEndingPresentationOwnership()
    {
        if (runEndingPauseOwned)
        {
            GameplayPauseManager pauseManager = GameplayPauseManager.Instance;
            pauseManager.PopPause(this);
            runEndingPauseOwned = false;
        }

        if (runEndingWorldSfxSuppressed)
        {
            AudioManager.SetWorldSfxSuppressed(this, false);
            runEndingWorldSfxSuppressed = false;
        }
    }

    public void CompleteRunAndReturnToSettlement(RunEndReason reason)
    {
        CompleteRun(reason);
    }

    private RunResultData CreateRunResult(RunEndReason reason, RunContext run)
    {
        RunWallet wallet = run.Wallet;

        int collectedScrap = wallet.PendingScrapParts;
        int collectedCore = wallet.PendingCoreShards;
        int collectedAlloy = wallet.PendingStabilizedAlloy;

        int committedScrap = 0;
        int committedCore = 0;
        int committedAlloy = 0;
        int lostScrap = 0;
        int lostCore = 0;
        int lostAlloy = 0;
        int emergencyCargoLimit = 0;

        switch (reason)
        {
            case RunEndReason.SafeReturn:
            case RunEndReason.FinalVictory:
                committedScrap = collectedScrap;
                committedCore = collectedCore;
                committedAlloy = collectedAlloy;
                break;

            case RunEndReason.EmergencyReturn:
                CalculateEmergencyReturnCommit(
                    run,
                    collectedScrap,
                    collectedCore,
                    collectedAlloy,
                    out committedScrap,
                    out committedCore,
                    out committedAlloy,
                    out lostScrap,
                    out lostCore,
                    out lostAlloy,
                    out emergencyCargoLimit
                );
                break;

            case RunEndReason.Death:
                CalculateDeathCommit(
                    collectedScrap,
                    collectedCore,
                    collectedAlloy,
                    out committedScrap,
                    out committedCore,
                    out committedAlloy,
                    out lostScrap,
                    out lostCore,
                    out lostAlloy
                );
                break;

            case RunEndReason.DebugAbort:
                lostScrap = collectedScrap;
                lostCore = collectedCore;
                lostAlloy = collectedAlloy;
                break;
        }

        if (reason == RunEndReason.EmergencyReturn || reason == RunEndReason.Death)
        {
            int guaranteedBossCore = Mathf.Min(
                collectedCore,
                run.GuaranteedBossCoreShardsThisRun
            );
            committedCore = Mathf.Max(committedCore, guaranteedBossCore);
            lostCore = Mathf.Max(0, collectedCore - committedCore);
        }

        int collectedCargoLoad = run.CalculateCargoLoad(collectedScrap, collectedCore, collectedAlloy);
        int committedCargoLoad = run.CalculateCargoLoad(committedScrap, committedCore, committedAlloy);

        RunResultData resultData = new RunResultData
        {
            endReason = reason,
            selectedWeaponTree = run.SelectedWeaponTree,
            selectedShipId = run.SelectedShipId,
            finalDepth = run.ExpeditionDepth,
            finalSeaRegionType = run.SeaRegionType,
            finalBossId = run.CurrentBossId,
            bossesDefeatedThisRun = run.BossesDefeatedThisRun != null
                ? run.BossesDefeatedThisRun.Count
                : 0,
            bossDefeated = run.BossDefeated,
            finalVictory = reason == RunEndReason.FinalVictory,

            runExperience = wallet.Experience,
            unusedTuningChips = wallet.TuningChips,
            objectiveSignalCount = run.ObjectiveSignalCount,
            remainingCredits = wallet.Credits,

            collectedScrapParts = collectedScrap,
            collectedCoreShards = collectedCore,
            collectedStabilizedAlloy = collectedAlloy,
            committedScrapParts = committedScrap,
            committedCoreShards = committedCore,
            committedStabilizedAlloy = committedAlloy,
            lostScrapParts = lostScrap,
            lostCoreShards = lostCore,
            lostStabilizedAlloy = lostAlloy,

            maxCargoCapacity = run.MaxCargoCapacity,
            collectedCargoLoad = collectedCargoLoad,
            committedCargoLoad = committedCargoLoad,
            emergencyReturnCargoLimit = emergencyCargoLimit
        };

        resultData.settledResources.Add(new RunSettlementResourceResult(
            CurrencyType.ScrapParts,
            collectedScrap,
            committedScrap,
            lostScrap
        ));
        resultData.settledResources.Add(new RunSettlementResourceResult(
            CurrencyType.CoreShards,
            collectedCore,
            committedCore,
            lostCore
        ));
        resultData.settledResources.Add(new RunSettlementResourceResult(
            CurrencyType.StabilizedAlloy,
            collectedAlloy,
            committedAlloy,
            lostAlloy
        ));

        return resultData;
    }

    private void CalculateEmergencyReturnCommit(
        RunContext run,
        int collectedScrap,
        int collectedCore,
        int collectedAlloy,
        out int committedScrap,
        out int committedCore,
        out int committedAlloy,
        out int lostScrap,
        out int lostCore,
        out int lostAlloy,
        out int cargoLimit)
    {
        int totalCargoLoad = run.CalculateCargoLoad(collectedScrap, collectedCore, collectedAlloy);
        cargoLimit = Mathf.FloorToInt(run.MaxCargoCapacity * run.EmergencyReturnCapacityRatio);

        if (totalCargoLoad <= cargoLimit)
        {
            committedScrap = collectedScrap;
            committedCore = collectedCore;
            committedAlloy = collectedAlloy;
            lostScrap = 0;
            lostCore = 0;
            lostAlloy = 0;
            return;
        }

        if (cargoLimit <= 0 || totalCargoLoad <= 0)
        {
            committedScrap = 0;
            committedCore = 0;
            committedAlloy = 0;
            lostScrap = collectedScrap;
            lostCore = collectedCore;
            lostAlloy = collectedAlloy;
            return;
        }

        float keepRatio = Mathf.Clamp01(cargoLimit / (float)totalCargoLoad);

        committedScrap = Mathf.FloorToInt(collectedScrap * keepRatio);
        committedCore = Mathf.FloorToInt(collectedCore * keepRatio);
        committedAlloy = Mathf.FloorToInt(collectedAlloy * keepRatio);

        int usedCargo = run.CalculateCargoLoad(committedScrap, committedCore, committedAlloy);
        int remainingCargo = Mathf.Max(0, cargoLimit - usedCargo);

        int remainingCore = collectedCore - committedCore;
        int extraCore = Mathf.Min(remainingCore, remainingCargo / run.CoreShardCargoWeight);
        committedCore += extraCore;
        remainingCargo -= extraCore * run.CoreShardCargoWeight;

        int remainingAlloy = collectedAlloy - committedAlloy;
        int extraAlloy = Mathf.Min(remainingAlloy, remainingCargo / run.StabilizedAlloyCargoWeight);
        committedAlloy += extraAlloy;
        remainingCargo -= extraAlloy * run.StabilizedAlloyCargoWeight;

        int remainingScrap = collectedScrap - committedScrap;
        int extraScrap = Mathf.Min(remainingScrap, remainingCargo / run.ScrapCargoWeight);
        committedScrap += extraScrap;

        lostScrap = Mathf.Max(0, collectedScrap - committedScrap);
        lostCore = Mathf.Max(0, collectedCore - committedCore);
        lostAlloy = Mathf.Max(0, collectedAlloy - committedAlloy);
    }

    private void CalculateDeathCommit(
        int collectedScrap,
        int collectedCore,
        int collectedAlloy,
        out int committedScrap,
        out int committedCore,
        out int committedAlloy,
        out int lostScrap,
        out int lostCore,
        out int lostAlloy)
    {
        float keepRate = balanceConfig != null ? balanceConfig.DeathScrapKeepRate : 0.5f;

        committedScrap = Mathf.FloorToInt(collectedScrap * keepRate);
        committedCore = 0;
        committedAlloy = Mathf.FloorToInt(collectedAlloy * keepRate);
        lostScrap = Mathf.Max(0, collectedScrap - committedScrap);
        lostCore = collectedCore;
        lostAlloy = Mathf.Max(0, collectedAlloy - committedAlloy);
    }

    private void SuppressRunEndingWorldSfx()
    {
        if (runEndingWorldSfxSuppressed)
        {
            return;
        }

        AudioManager.SetWorldSfxSuppressed(this, true, true);
        runEndingWorldSfxSuppressed = true;
    }

    private void ResetRunEndingState()
    {
        ReleaseRunEndingPresentationOwnership();
        runEndingActive = false;
        runCompletionFinalized = false;
        runEndingReason = RunEndReason.None;
    }

    private string ResolveSelectedShipId(string requestedShipId)
    {
        if (!string.IsNullOrWhiteSpace(requestedShipId))
        {
            return requestedShipId;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.SelectedShipId;
        }

        return "basic_ship";
    }

    private void LoadExpeditionScene()
    {
        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadExpeditionWithMotionTitle();
        }
    }

    private void UnsubscribeWallet()
    {
        if (currentRun != null && currentRun.Wallet != null)
        {
            currentRun.Wallet.Changed -= HandleWalletChanged;
        }
    }

    private void HandleWalletChanged()
    {
        if (currentRun != null)
        {
            WalletChanged?.Invoke(currentRun.Wallet);
        }
    }
}
