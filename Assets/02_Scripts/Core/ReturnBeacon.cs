using UnityEngine;

[DisallowMultipleComponent]
public class ReturnBeacon : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionText = "귀환 비콘 사용";
    [SerializeField] private bool requireBossDefeated = true;

    [Header("UI")]
    [SerializeField] private ReturnChoiceUI returnChoiceUI;

    public string InteractionText => interactionText;

    private void Awake()
    {
        if (returnChoiceUI == null)
        {
            returnChoiceUI = FindFirstObjectByType<ReturnChoiceUI>();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
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
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        RunManager.Instance.CompleteRun(RunEndReason.SafeReturn);

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadSettlementWithFade();
        }
    }
}