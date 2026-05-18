using System;
using UnityEngine;

public class RunLevelSystem : MonoBehaviour
{
    [Header("EXP Rule")]
    [SerializeField] private int baseRequiredExp = 100;
    [SerializeField] private float requiredExpGrowth = 1.25f;
    [SerializeField] private int maxLevel = 99;

    public int TotalExperience { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int CurrentExpInLevel { get; private set; }
    public int CurrentRequiredExp { get; private set; } = 100;
    public float CurrentExpRatio => CurrentRequiredExp <= 0 ? 1f : Mathf.Clamp01((float)CurrentExpInLevel / CurrentRequiredExp);

    public event Action<int, int, int> LevelStateChanged;

    private void OnEnable()
    {
        SubscribeRunManager();
        RefreshFromRun();
    }

    private void OnDisable()
    {
        UnsubscribeRunManager();
    }

    private void SubscribeRunManager()
    {
        if (RunManager.Instance == null)
        {
            return;
        }

        RunManager.Instance.RunStarted += HandleRunStarted;
        RunManager.Instance.WalletChanged += HandleWalletChanged;
    }

    private void UnsubscribeRunManager()
    {
        if (RunManager.Instance == null)
        {
            return;
        }

        RunManager.Instance.RunStarted -= HandleRunStarted;
        RunManager.Instance.WalletChanged -= HandleWalletChanged;
    }

    private void HandleRunStarted(RunContext runContext)
    {
        RefreshFromRun();
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        SetTotalExperience(wallet != null ? wallet.Experience : 0);
    }

    public void RefreshFromRun()
    {
        RunWallet wallet = null;

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            wallet = RunManager.Instance.CurrentRun.Wallet;
        }

        SetTotalExperience(wallet != null ? wallet.Experience : 0);
    }

    public void SetTotalExperience(int totalExperience)
    {
        TotalExperience = Mathf.Max(0, totalExperience);

        int level = 1;
        int remainingExp = TotalExperience;
        int requiredExp = GetRequiredExpForLevel(level);

        while (remainingExp >= requiredExp && level < maxLevel)
        {
            remainingExp -= requiredExp;
            level++;
            requiredExp = GetRequiredExpForLevel(level);
        }

        CurrentLevel = level;
        CurrentExpInLevel = level >= maxLevel ? requiredExp : remainingExp;
        CurrentRequiredExp = requiredExp;

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            RunManager.Instance.CurrentRun.SetLevel(CurrentLevel);
        }

        LevelStateChanged?.Invoke(CurrentLevel, CurrentExpInLevel, CurrentRequiredExp);
    }

    public int GetRequiredExpForLevel(int level)
    {
        level = Mathf.Max(1, level);
        float required = baseRequiredExp * Mathf.Pow(requiredExpGrowth, level - 1);
        return Mathf.Max(1, Mathf.RoundToInt(required));
    }
}
