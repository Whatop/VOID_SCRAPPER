using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathSequenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ShipDeathBreakup deathBreakup;

    [Header("Sequence")]
    [SerializeField] private float delayBeforeFade = 0.8f;
    [SerializeField] private float fadeInDuration = 0.45f;
    [SerializeField] private float fadeOutDuration = 0.45f;

    [Header("Run Result")]
    [SerializeField] private RunEndReason deathReason = RunEndReason.Death;

    private bool sequenceStarted;

    private void Reset()
    {
        playerHealth = GetComponent<PlayerHealth>();
        deathBreakup = GetComponent<ShipDeathBreakup>();
    }

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (deathBreakup == null)
        {
            deathBreakup = GetComponent<ShipDeathBreakup>();
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (sequenceStarted)
        {
            return;
        }

        sequenceStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (deathBreakup != null)
        {
            deathBreakup.Break();
        }

        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFade);
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CompleteRun(deathReason);
        }

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadSettlementWithFade(fadeInDuration, fadeOutDuration);
        }
    }
}