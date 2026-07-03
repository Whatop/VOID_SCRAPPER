using System;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private GameBalanceConfig balanceConfig;

    [Header("Debug")]
    [SerializeField] private RunContext currentRun;

    public RunContext CurrentRun => currentRun;
    public bool HasActiveRun => currentRun != null && currentRun.IsActive;

    public event Action<RunContext> RunStarted;
    public event Action<RunWallet> WalletChanged;
    public event Action<RunResultData> RunEnded;

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
        if (currentRun != null && currentRun.Wallet != null)
        {
            currentRun.Wallet.Changed -= HandleWalletChanged;
        }
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
        if (currentRun != null && currentRun.Wallet != null)
        {
            currentRun.Wallet.Changed -= HandleWalletChanged;
        }

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
            $"Depth: {depth}, SeaRegion: {SeaRegionCatalog.GetDisplayName(seaRegionType)}"
        );
    }

    public void StartNewRunAndLoadExpedition(WeaponTreeType selectedWeaponTree)
    {
        StartNewRunAndLoadExpedition(selectedWeaponTree, ResolveSelectedShipId(null));
    }

    public void StartNewRunAndLoadExpedition(WeaponTreeType selectedWeaponTree, string selectedShipId)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal, selectedShipId);

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadExpeditionWithMotionTitle();
        }
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

    public bool CanAddCargoCurrency(CurrencyType currencyType, int amount = 1)
    {
        if (!HasActiveRun)
        {
            return false;
        }

        return currentRun.GetAcceptedAmountByCargo(currencyType, amount) > 0;
    }

    public bool TrySpendCredits(int amount)
    {
        if (!HasActiveRun)
        {
            return false;
        }

        return currentRun.Wallet.TrySpend(CurrencyType.Credits, amount);
    }

    public void MarkBossDefeated()
    {
        if (!HasActiveRun)
        {
            return;
        }

        currentRun.MarkBossDefeated();
    }

    public void SetShopHostileThisRun(bool hostile)
    {
        if (!HasActiveRun)
        {
            return;
        }

        currentRun.SetShopHostile(hostile);
    }

    public void EnterDeepZone1()
    {
        if (!HasActiveRun)
        {
            Debug.LogWarning("활성화된 탐사가 없어 심부 해역으로 이동할 수 없습니다.", this);
            return;
        }

        SeaRegionType previousRegion = currentRun.SeaRegionType;
        SeaRegionType nextRegion = SeaRegionCatalog.GetRandom(previousRegion);

        currentRun.SetDepth(ExpeditionDepth.DeepZone1);
        currentRun.SetSeaRegion(nextRegion);

        Debug.Log($"심부 해역으로 이동합니다. SeaRegion: {SeaRegionCatalog.GetDisplayName(nextRegion)}", this);

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadExpeditionWithMotionTitle();
        }
    }

    public RunResultData CompleteRun(RunEndReason reason)
    {
        if (!HasActiveRun)
        {
            Debug.LogWarning("완료할 탐사가 없습니다.", this);
            return null;
        }

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

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.RunResult);
        }

        RunEnded?.Invoke(resultData);

        Debug.Log($"탐사 종료: {reason}, Scrap Commit: {resultData.committedScrapParts}, Core Commit: {resultData.committedCoreShards}");

        return resultData;
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

        int committedScrap = 0;
        int committedCore = 0;
        int lostScrap = 0;
        int lostCore = 0;
        int emergencyCargoLimit = 0;

        switch (reason)
        {
            case RunEndReason.SafeReturn:
                committedScrap = collectedScrap;
                committedCore = collectedCore;
                break;

            case RunEndReason.EmergencyReturn:
                CalculateEmergencyReturnCommit(run, collectedScrap, collectedCore, out committedScrap, out committedCore, out lostScrap, out lostCore, out emergencyCargoLimit);
                break;

            case RunEndReason.Death:
                CalculateDeathCommit(collectedScrap, collectedCore, out committedScrap, out committedCore, out lostScrap, out lostCore);
                break;

            case RunEndReason.DebugAbort:
                committedScrap = 0;
                committedCore = 0;
                lostScrap = collectedScrap;
                lostCore = collectedCore;
                break;
        }

        int collectedCargoLoad = run.CalculateCargoLoad(collectedScrap, collectedCore);
        int committedCargoLoad = run.CalculateCargoLoad(committedScrap, committedCore);

        return new RunResultData
        {
            endReason = reason,
            selectedWeaponTree = run.SelectedWeaponTree,
            selectedShipId = run.SelectedShipId,
            finalDepth = run.ExpeditionDepth,
            finalSeaRegionType = run.SeaRegionType,

            runExperience = wallet.Experience,
            remainingCredits = wallet.Credits,

            collectedScrapParts = collectedScrap,
            collectedCoreShards = collectedCore,

            committedScrapParts = committedScrap,
            committedCoreShards = committedCore,

            lostScrapParts = lostScrap,
            lostCoreShards = lostCore,

            maxCargoCapacity = run.MaxCargoCapacity,
            collectedCargoLoad = collectedCargoLoad,
            committedCargoLoad = committedCargoLoad,
            emergencyReturnCargoLimit = emergencyCargoLimit
        };
    }

    private void CalculateEmergencyReturnCommit(
        RunContext run,
        int collectedScrap,
        int collectedCore,
        out int committedScrap,
        out int committedCore,
        out int lostScrap,
        out int lostCore,
        out int cargoLimit)
    {
        int totalCargoLoad = run.CalculateCargoLoad(collectedScrap, collectedCore);
        cargoLimit = Mathf.FloorToInt(run.MaxCargoCapacity * run.EmergencyReturnCapacityRatio);

        if (totalCargoLoad <= cargoLimit)
        {
            committedScrap = collectedScrap;
            committedCore = collectedCore;
            lostScrap = 0;
            lostCore = 0;
            return;
        }

        if (cargoLimit <= 0 || totalCargoLoad <= 0)
        {
            committedScrap = 0;
            committedCore = 0;
            lostScrap = collectedScrap;
            lostCore = collectedCore;
            return;
        }

        float keepRatio = Mathf.Clamp01(cargoLimit / (float)totalCargoLoad);

        committedScrap = Mathf.FloorToInt(collectedScrap * keepRatio);
        committedCore = Mathf.FloorToInt(collectedCore * keepRatio);

        int usedCargo = run.CalculateCargoLoad(committedScrap, committedCore);
        int remainingCargo = Mathf.Max(0, cargoLimit - usedCargo);

        int remainingCore = collectedCore - committedCore;
        int extraCore = Mathf.Min(remainingCore, remainingCargo / run.CoreShardCargoWeight);
        committedCore += extraCore;
        remainingCargo -= extraCore * run.CoreShardCargoWeight;

        int remainingScrap = collectedScrap - committedScrap;
        int extraScrap = Mathf.Min(remainingScrap, remainingCargo / run.ScrapCargoWeight);
        committedScrap += extraScrap;

        lostScrap = Mathf.Max(0, collectedScrap - committedScrap);
        lostCore = Mathf.Max(0, collectedCore - committedCore);
    }

    private void CalculateDeathCommit(
        int collectedScrap,
        int collectedCore,
        out int committedScrap,
        out int committedCore,
        out int lostScrap,
        out int lostCore)
    {
        float keepRate = balanceConfig != null ? balanceConfig.DeathScrapKeepRate : 0.5f;

        committedScrap = Mathf.FloorToInt(collectedScrap * keepRate);
        committedCore = 0;

        lostScrap = Mathf.Max(0, collectedScrap - committedScrap);
        lostCore = collectedCore;
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

    private void HandleWalletChanged()
    {
        if (currentRun != null)
        {
            WalletChanged?.Invoke(currentRun.Wallet);
        }
    }
}
