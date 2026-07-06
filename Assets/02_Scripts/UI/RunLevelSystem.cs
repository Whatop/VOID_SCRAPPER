using System;
using UnityEngine;

public class RunLevelSystem : MonoBehaviour
{
    [Header("EXP Table")]
    [Tooltip("Lv2, Lv3, Lv4, Lv5, Lv6�� �ʿ��� ���� ����ġ")]
    [SerializeField]
    private int[] cumulativeExpTable =
    {
        10,
        24,
        44,
        70,
        102
    };

    [SerializeField] private int maxLevel = 6;

    public int TotalExperience { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int CurrentExpInLevel { get; private set; }
    public int CurrentRequiredExp { get; private set; } = 10;

    public float CurrentExpRatio =>
        CurrentRequiredExp <= 0 ? 1f : Mathf.Clamp01((float)CurrentExpInLevel / CurrentRequiredExp);

    public event Action<int, int, int> LevelStateChanged;
    public event Action<int> LeveledUp;

    private int previousLevel = 1;

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
        previousLevel = 1;
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

        int calculatedLevel = CalculateLevel(TotalExperience);
        CurrentLevel = calculatedLevel;

        CalculateCurrentLevelProgress(
            TotalExperience,
            CurrentLevel,
            out int expInLevel,
            out int requiredExp
        );

        CurrentExpInLevel = expInLevel;
        CurrentRequiredExp = requiredExp;

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            RunManager.Instance.CurrentRun.SetLevel(CurrentLevel);
        }

        LevelStateChanged?.Invoke(CurrentLevel, CurrentExpInLevel, CurrentRequiredExp);

        if (CurrentLevel > previousLevel)
        {
            for (int level = previousLevel + 1; level <= CurrentLevel; level++)
            {
                LeveledUp?.Invoke(level);
                AudioManager.Play(SoundEventIds.LevelUp);
            }
        }

        previousLevel = CurrentLevel;
    }

    private int CalculateLevel(int totalExperience)
    {
        int level = 1;

        for (int i = 0; i < cumulativeExpTable.Length; i++)
        {
            if (totalExperience >= cumulativeExpTable[i])
            {
                level = i + 2;
            }
            else
            {
                break;
            }
        }

        return Mathf.Clamp(level, 1, maxLevel);
    }

    private void CalculateCurrentLevelProgress(
        int totalExperience,
        int level,
        out int expInLevel,
        out int requiredExp)
    {
        if (level >= maxLevel)
        {
            int previousThreshold = GetCumulativeRequiredExpForLevel(maxLevel - 1);
            int maxThreshold = GetCumulativeRequiredExpForLevel(maxLevel);

            expInLevel = Mathf.Max(0, totalExperience - previousThreshold);
            requiredExp = Mathf.Max(1, maxThreshold - previousThreshold);
            return;
        }

        int currentLevelStart = GetCumulativeRequiredExpForLevel(level);
        int nextLevelRequired = GetCumulativeRequiredExpForLevel(level + 1);

        expInLevel = Mathf.Max(0, totalExperience - currentLevelStart);
        requiredExp = Mathf.Max(1, nextLevelRequired - currentLevelStart);
    }

    public int GetCumulativeRequiredExpForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        int index = Mathf.Clamp(level - 2, 0, cumulativeExpTable.Length - 1);
        return cumulativeExpTable[index];
    }
}