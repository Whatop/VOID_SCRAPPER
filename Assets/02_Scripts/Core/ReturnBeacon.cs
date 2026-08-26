using UnityEngine;

[DisallowMultipleComponent]
public class ReturnBeacon : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionText = "귀환 비콘 사용";
    [SerializeField] private string returningText = "안전 복귀 처리 중";
    [SerializeField] private bool requireBossDefeated = true;

    [Header("UI")]
    [SerializeField] private ReturnChoiceUI returnChoiceUI;

    private bool returning;
    private Collider2D beaconCollider;

    public string InteractionText => returning ? returningText : interactionText;

    private void Awake()
    {
        beaconCollider = GetComponent<Collider2D>();

        if (returnChoiceUI == null)
        {
            returnChoiceUI = FindFirstObjectByType<ReturnChoiceUI>();
        }
    }

    private void OnEnable()
    {
        AudioManager.PlayAt(SoundEventIds.ReturnBeaconSpawn, transform.position, 0.8f);
    }

    public bool CanInteract(GameObject interactor)
    {
        if (returning)
        {
            return false;
        }

        if (interactor == null)
        {
            return false;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        if (requireBossDefeated && !RunManager.Instance.CurrentRun.BossDefeated)
        {
            return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (returnChoiceUI == null)
        {
            returnChoiceUI = FindFirstObjectByType<ReturnChoiceUI>();
        }

        if (returnChoiceUI != null)
        {
            returnChoiceUI.Open(this);
            return;
        }

        ConfirmReturn();
    }

    public void ConfirmReturn()
    {
        if (returning)
        {
            return;
        }

        RunManager runManager = RunManager.Instance;

        if (runManager == null || !runManager.HasActiveRun ||
            !runManager.TryBeginRunEnding(RunEndReason.SafeReturn, false))
        {
            return;
        }

        returning = true;
        AudioManager.PlayAt(SoundEventIds.SafeReturn, transform.position);
        runManager.CompleteRun(RunEndReason.SafeReturn);

        // 여기서 씬 이동하면 안 됨.
        // 정산창 ContinueButton이 정착지 이동을 담당한다.
    }
}
