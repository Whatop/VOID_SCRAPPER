using TMPro;
using UnityEngine;

// A read-only view of this reactor's existing timer and HP. No clock or outcome authority.
[DisallowMultipleComponent]
public sealed class ReactorFeedbackUI : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text healthText;
    private PlayerHealth player;
    private RunManager run;
    private int displayedSeconds = -1, displayedHealth = -1;
    public bool IsShowing { get; private set; }

    public void Begin(float seconds, float healthRatio, PlayerHealth playerHealth, RunManager runManager)
    {
        Clear();
        player = playerHealth;
        run = runManager;
        if (player != null) player.Died += Clear;
        if (run != null) run.RunEnded += RunEnded;
        if (player != null && player.IsDead) { Clear(); return; }
        IsShowing = true;
        gameObject.SetActive(true);
        Refresh(seconds, healthRatio);
    }

    public void Refresh(float seconds, float healthRatio)
    {
        if (!IsShowing) return;
        int value = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        if (value != displayedSeconds)
        {
            displayedSeconds = value;
            countdownText?.SetText("{0:0}s", value);
        }
        int health = Mathf.CeilToInt(Mathf.Clamp01(healthRatio) * 100f);
        if (health != displayedHealth)
        {
            displayedHealth = health;
            healthText?.SetText("HP {0:0}%", health);
        }
    }

    public void Clear()
    {
        IsShowing = false;
        if (player != null) player.Died -= Clear;
        if (run != null) run.RunEnded -= RunEnded;
        player = null;
        run = null;
        displayedSeconds = displayedHealth = -1;
        if (countdownText != null) countdownText.text = string.Empty;
        if (healthText != null) healthText.text = string.Empty;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void RunEnded(RunResultData _) => Clear();
    private void OnDisable() => Clear();
}
