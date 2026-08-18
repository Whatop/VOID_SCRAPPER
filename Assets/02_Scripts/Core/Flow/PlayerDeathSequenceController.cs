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
    [SerializeField] private float delayBeforeResult = 0.8f;

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

        if (delayBeforeResult > 0f)
        {
            yield return new WaitForSeconds(delayBeforeResult);
        }

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Tutorial)
        {
            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.LoadTutorial();
            }
            else
            {
                Debug.LogError("Tutorial death could not restart because SceneFlowManager is missing.", this);
            }

            yield break;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CompleteRun(deathReason);
        }

        // 여기서 씬 이동하면 안 됨.
        // 정산창 ContinueButton이 정착지 이동을 담당한다.
    }
}