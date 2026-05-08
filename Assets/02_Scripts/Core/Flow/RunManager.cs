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

    private bool isCompletingRun;
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
    public void StartNewRun(WeaponTreeType selectedWeaponTree, ExpeditionDepth depth)
    {
        isCompletingRun = false;

        if (currentRun != null && currentRun.Wallet != null)
        {
            currentRun.Wallet.Changed -= HandleWalletChanged;
        }

        currentRun = new RunContext(selectedWeaponTree, depth);
        currentRun.Wallet.Changed += HandleWalletChanged;

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.SetLastSelectedWeaponTree(selectedWeaponTree);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.ExpeditionLoading);
        }

        RunStarted?.Invoke(currentRun);
        WalletChanged?.Invoke(currentRun.Wallet);
    }

    public RunResultData CompleteRun(RunEndReason reason)
    {
        if (isCompletingRun)
        {
            return null;
        }

        if (!HasActiveRun)
        {
            Debug.LogWarning("완료할 탐사가 없습니다.", this);
            return null;
        }

        isCompletingRun = true;

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
        return resultData;
    }

    public void CompleteRunAndReturnToSettlement(RunEndReason reason)
    {
        RunResultData resultData = CompleteRun(reason);
        if (resultData == null)
        {
            return;
        }

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadSettlement();
        }
    }
    public void SetBalanceConfig(GameBalanceConfig config)
    {
        balanceConfig = config;
    }

    public void StartNewRun(WeaponTreeType selectedWeaponTree)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal);
    }

    public void StartNewRunAndLoadExpedition(WeaponTreeType selectedWeaponTree)
    {
        StartNewRun(selectedWeaponTree, ExpeditionDepth.Normal);

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadExpedition();
        }
    }

    public void AddCurrency(CurrencyType currencyType, int amount)
    {
        if (!HasActiveRun)
        {
            Debug.LogWarning("활성화된 탐사가 없어 재화를 지급할 수 없습니다.", this);
            return;
        }

        currentRun.Wallet.Add(currencyType, amount);
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

        currentRun.SetDepth(ExpeditionDepth.DeepZone1);

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadExpedition();
        }
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

        switch (reason)
        {
            case RunEndReason.SafeReturn:
                committedScrap = collectedScrap;
                committedCore = collectedCore;
                break;

            case RunEndReason.EmergencyReturn:
                CalculateEmergencyReturnCommit(collectedScrap, collectedCore, out committedScrap, out committedCore, out lostScrap, out lostCore);
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

        return new RunResultData
        {
            endReason = reason,
            selectedWeaponTree = run.SelectedWeaponTree,
            finalDepth = run.ExpeditionDepth,

            runExperience = wallet.Experience,
            remainingCredits = wallet.Credits,

            collectedScrapParts = collectedScrap,
            collectedCoreShards = collectedCore,

            committedScrapParts = committedScrap,
            committedCoreShards = committedCore,

            lostScrapParts = lostScrap,
            lostCoreShards = lostCore
        };
    }

    private void CalculateEmergencyReturnCommit(
        int collectedScrap,
        int collectedCore,
        out int committedScrap,
        out int committedCore,
        out int lostScrap,
        out int lostCore)
    {
        float lossRate = balanceConfig != null ? balanceConfig.EmergencyReturnLossRate : 0.2f;

        lostScrap = Mathf.CeilToInt(collectedScrap * lossRate);
        lostCore = Mathf.CeilToInt(collectedCore * lossRate);

        committedScrap = Mathf.Max(0, collectedScrap - lostScrap);
        committedCore = Mathf.Max(0, collectedCore - lostCore);
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

    private void HandleWalletChanged()
    {
        if (currentRun != null)
        {
            WalletChanged?.Invoke(currentRun.Wallet);
        }
    }
}