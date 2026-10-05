using UnityEngine;

// Deck-owned readers only. Weapons retain their existing heat/charge presenters.
[DisallowMultipleComponent]
public sealed class RouteCoreCombatHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private GaugeBarUI healthGauge;
    [SerializeField] private GaugeBarUI armorGauge;

    private void OnEnable()
    {
        if (playerHealth == null || playerArmor == null || healthGauge == null || armorGauge == null)
        {
            Debug.LogError("RouteCoreDeckHUD/CombatStatus: missing authored player or gauge reference.", this);
            return;
        }
        playerHealth.Changed += RefreshHealth;
        playerArmor.Changed += RefreshArmor;
        RefreshHealth(playerHealth.CurrentHp, playerHealth.MaxHp);
        RefreshArmor(playerArmor.CurrentArmor, playerArmor.MaxArmor);
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Changed -= RefreshHealth;
        if (playerArmor != null) playerArmor.Changed -= RefreshArmor;
        healthGauge?.SetVisible(false);
        armorGauge?.SetVisible(false);
    }

    private void RefreshHealth(float current, float maximum)
    {
        healthGauge.SetVisible(true);
        healthGauge.SetValue(current, maximum);
    }

    private void RefreshArmor(float current, float maximum)
    {
        armorGauge.SetValue(current, maximum);
        armorGauge.SetVisible(maximum > 0);
    }
}
