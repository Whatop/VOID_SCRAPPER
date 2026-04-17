using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerProgression playerProgression;

    [Header("UI Text")]
    [SerializeField] private Text hpText;
    [SerializeField] private Text expText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text scoreText;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
    }

    private void Update()
    {
        RefreshHUD();
    }

    /// <summary>
    /// Player 관련 참조가 비어 있으면 자동으로 찾아 연결합니다.
    /// </summary>
    private void FindReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerProgression == null)
        {
            playerProgression = FindFirstObjectByType<PlayerProgression>();
        }
    }

    /// <summary>
    /// 매 프레임마다 HP, EXP, Level, Score 텍스트를 갱신합니다.
    /// </summary>
    private void RefreshHUD()
    {
        if (playerHealth != null && hpText != null)
        {
            hpText.text = $"HP : {Mathf.Max(playerHealth.CurrentHp, 0)}/{playerHealth.MaxHp}";
        }

        if (playerProgression == null)
        {
            return;
        }

        if (levelText != null)
        {
            levelText.text = $"LV : {playerProgression.CurrentLevel}";
        }

        if (scoreText != null)
        {
            scoreText.text = $"SCORE : {playerProgression.CurrentScore}";
        }

        if (expText != null)
        {
            if (playerProgression.IsMaxLevel)
            {
                expText.text = $"EXP : MAX ({playerProgression.CurrentExp})";
            }
            else
            {
                expText.text = $"EXP : {playerProgression.CurrentExp}/{playerProgression.GetRequiredExpForNextLevel()}";
            }
        }
    }
}

