using UnityEngine;

public class  PlayerProgression : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerAutoShooter playerAutoShooter;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Level Settings")]
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private int expToLevel2 = 5;
    [SerializeField] private int expToLevel3 = 12;

    [Header("Score Settings")]
    [SerializeField] private int scorePerReward = 100; // 부품 1개당 점수

    private int currentExp;
    private int currentLevel = 1;
    private int currentScore;

    // PlayerProgression.cs - 임시 안전 처리
    [SerializeField] private bool useLegacyAutoShooterUpgrade;
    public int CurrentExp => currentExp;
    public int CurrentLevel => currentLevel;
    public int CurrentScore => currentScore;
    public bool IsMaxLevel => currentLevel >= maxLevel;

    private void Reset()
    {
        playerController = GetComponent<PlayerController2D>();
        playerAutoShooter = GetComponent<PlayerAutoShooter>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        ContainerPickup.Collected += HandleContainerCollected;
    }

    private void OnDisable()
    {
        ContainerPickup.Collected -= HandleContainerCollected;
    }

    /// <summary>
    /// 컨테이너를 획득했을 때 호출됩니다.
    /// 경험치와 점수를 동시에 누적합니다.
    /// </summary>
    private void HandleContainerCollected(int rewardAmount)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        AddScore(rewardAmount * scorePerReward);
        AddExperience(rewardAmount);
    }

    /// <summary>
    /// 경험치를 증가시키고, 필요 경험치에 도달하면 자동으로 레벨업합니다.
    /// </summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExp += amount;
        CheckLevelUp();
    }

    /// <summary>
    /// 점수를 증가시킵니다.
    /// 이번 프로젝트에서는 부품 수집량을 점수로 변환합니다.
    /// </summary>
    public void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentScore += amount;
    }

    private void CheckLevelUp()
    {
        while (currentLevel < maxLevel && currentExp >= GetRequiredExp(currentLevel + 1))
        {
            currentLevel++;
            ApplyLevelUpgrade(currentLevel);
        }
    }

    private int GetRequiredExp(int targetLevel)
    {
        switch (targetLevel)
        {
            case 2:
                return expToLevel2;
            case 3:
                return expToLevel3;
            default:
                return int.MaxValue;
        }
    }

    /// <summary>
    /// HUD에서 다음 레벨까지 필요한 경험치를 표시할 때 사용합니다.
    /// 최대 레벨이면 현재 경험치를 그대로 반환합니다.
    /// </summary>
    public int GetRequiredExpForNextLevel()
    {
        if (IsMaxLevel)
        {
            return currentExp;
        }

        return GetRequiredExp(currentLevel + 1);
    }


    private void ApplyLevelUpgrade(int newLevel)
    {
        switch (newLevel)
        {
            case 2:
                if (playerController != null)
                {
                    playerController.AddMoveSpeed(1.2f);
                }

                if (useLegacyAutoShooterUpgrade &&
                    playerAutoShooter != null &&
                    playerAutoShooter.isActiveAndEnabled)
                {
                    playerAutoShooter.ApplyUpgradeLevel(2);
                }
                break;

            case 3:
                if (playerController != null)
                {
                    playerController.AddMoveSpeed(0.8f);
                }

                if (useLegacyAutoShooterUpgrade &&
                    playerAutoShooter != null &&
                    playerAutoShooter.isActiveAndEnabled)
                {
                    playerAutoShooter.ApplyUpgradeLevel(3);
                }
                break;
        }
    }
}
