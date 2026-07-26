using System;
using UnityEngine;

public class RunLevelSystem : MonoBehaviour
{
    [Header("Legacy EXP Leveling")]
    [Tooltip("기본 OFF. VOID SCRAPPER의 신규 성장 구조는 특수 상자/NPC/보스 보상을 사용합니다.")]
    [SerializeField] private bool legacyExperienceLevelingEnabled;

    [Header("EXP Table - Legacy Only")]
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

    public bool LegacyExperienceLevelingEnabled => legacyExperienceLevelingEnabled;
    public int TotalExperience { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int CurrentExpInLevel { get; private set; }
    public int CurrentRequiredExp { get; private set; } = 1;

    public float CurrentExpRatio => legacyExperienceLevelingEnabled && CurrentRequiredExp > 0
        ? Mathf.Clamp01((float)CurrentExpInLevel / CurrentRequiredExp)
        : 0f;

    public event Action<int, int, int> LevelStateChanged;
    public event Action<int> LeveledUp;

    private int previousLevel = 1;

    private void OnEnable()
    {
        if (legacyExperienceLevelingEnabled)
        {
            SubscribeRunManager();
            RefreshFromRun();
        }
        else
        {
            ResetLegacyState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeRunManager();
    }

    public void SetLegacyExperienceLevelingEnabled(bool enabled)
    {
        if (legacyExperienceLevelingEnabled == enabled)
        {
            return;
        }

        legacyExperienceLevelingEnabled = enabled;
        UnsubscribeRunManager();

        if (legacyExperienceLevelingEnabled)
        {
            SubscribeRunManager();
            RefreshFromRun();
        }
        else
        {
            ResetLegacyState();
        }
    }

    private void SubscribeRunManager()
    {
        if (RunManager.Instance == null)
        {
            return;
        }

        RunManager.Instance.RunStarted -= HandleRunStarted;
        RunManager.Instance.WalletChanged -= HandleWalletChanged;
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
        if (!legacyExperienceLevelingEnabled)
        {
            return;
        }

        SetTotalExperience(wallet != null ? wallet.Experience : 0);
    }

    public void RefreshFromRun()
    {
        if (!legacyExperienceLevelingEnabled)
        {
            ResetLegacyState();
            return;
        }

        RunWallet wallet = null;

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            wallet = RunManager.Instance.CurrentRun.Wallet;
        }

        SetTotalExperience(wallet != null ? wallet.Experience : 0);
    }

    public void SetTotalExperience(int totalExperience)
    {
        if (!legacyExperienceLevelingEnabled)
        {
            ResetLegacyState();
            return;
        }

        TotalExperience = Mathf.Max(0, totalExperience);
        CurrentLevel = CalculateLevel(TotalExperience);

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

    public int GetCumulativeRequiredExpForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        if (cumulativeExpTable == null || cumulativeExpTable.Length == 0)
        {
            return 0;
        }

        int index = Mathf.Clamp(level - 2, 0, cumulativeExpTable.Length - 1);
        return cumulativeExpTable[index];
    }

    private void ResetLegacyState()
    {
        TotalExperience = 0;
        CurrentLevel = 1;
        CurrentExpInLevel = 0;
        CurrentRequiredExp = 1;
        previousLevel = 1;

        if (RunManager.Instance != null && RunManager.Instance.CurrentRun != null)
        {
            RunManager.Instance.CurrentRun.SetLevel(1);
        }

        LevelStateChanged?.Invoke(CurrentLevel, CurrentExpInLevel, CurrentRequiredExp);
    }

    private int CalculateLevel(int totalExperience)
    {
        int level = 1;

        if (cumulativeExpTable == null)
        {
            return level;
        }

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

        return Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    }

    private void CalculateCurrentLevelProgress(
        int totalExperience,
        int level,
        out int expInLevel,
        out int requiredExp)
    {
        int finalMaxLevel = Mathf.Max(1, maxLevel);

        if (level >= finalMaxLevel)
        {
            int previousThreshold = GetCumulativeRequiredExpForLevel(finalMaxLevel - 1);
            int maxThreshold = GetCumulativeRequiredExpForLevel(finalMaxLevel);

            expInLevel = Mathf.Max(0, totalExperience - previousThreshold);
            requiredExp = Mathf.Max(1, maxThreshold - previousThreshold);
            return;
        }

        int currentLevelStart = GetCumulativeRequiredExpForLevel(level);
        int nextLevelRequired = GetCumulativeRequiredExpForLevel(level + 1);

        expInLevel = Mathf.Max(0, totalExperience - currentLevelStart);
        requiredExp = Mathf.Max(1, nextLevelRequired - currentLevelStart);
    }
}
