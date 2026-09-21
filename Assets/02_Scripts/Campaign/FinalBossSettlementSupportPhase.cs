using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class FinalBossSettlementSupportPhase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth finalBossHealth;
    [SerializeField] private SettlementFinalSupportController supportController;

    [Header("Phase Trigger")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float triggerHpRatio = 0.45f;
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    private void Reset()
    {
        finalBossHealth = GetComponent<EnemyHealth>();
        supportController = GetComponent<SettlementFinalSupportController>();
    }

    private void Awake()
    {
        if (finalBossHealth == null)
        {
            finalBossHealth = GetComponent<EnemyHealth>();
        }

        if (supportController == null)
        {
            supportController = GetComponent<SettlementFinalSupportController>();
        }

        if (supportController == null)
        {
            supportController = gameObject.AddComponent<SettlementFinalSupportController>();
        }

        supportController.ConfigureFinalBoss(finalBossHealth);
    }

    // Final support is explicitly orchestrated by a later encounter phase.
    // The historical serialized threshold is retained for asset compatibility only.
    private void OnEnable()
    {
        triggered = false;
    }

    public void TriggerSupportNow()
    {
        if (triggerOnce && triggered)
        {
            return;
        }

        triggered = true;

        if (supportController != null)
        {
            supportController.ConfigureFinalBoss(finalBossHealth);
            supportController.TriggerFullSupport();
        }
    }

}
