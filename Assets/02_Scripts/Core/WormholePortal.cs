using UnityEngine;

[DisallowMultipleComponent]
public class WormholePortal : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionText = "위상 분기 항로 진입";
    [SerializeField] private bool requireBossDefeated = true;

    [Header("UI")]
    [SerializeField] private WormholeChoiceUI wormholeChoiceUI;

    [Header("Debug")]
    [SerializeField] private bool logBlockReason = true;

    [Header("Optional")]
    [SerializeField] private RadarTarget radarTarget;

    public string InteractionText
    {
        get
        {
            if (RunManager.Instance != null &&
                RunManager.Instance.CanAdvanceToNextRegion(out ExpeditionDepth nextDepth, out _))
            {
                return $"{CampaignProgressionCatalog.GetRegionShortName(nextDepth)} 진입";
            }

            return interactionText;
        }
    }

    private void Reset()
    {
        radarTarget = GetComponent<RadarTarget>();
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (wormholeChoiceUI == null)
        {
            wormholeChoiceUI = FindFirstObjectByType<WormholeChoiceUI>();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (interactor == null)
        {
            LogBlock("상호작용 대상이 없습니다.");
            return false;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            LogBlock("활성화된 탐사가 없습니다.");
            return false;
        }

        if (requireBossDefeated && !RunManager.Instance.CurrentRun.BossDefeated)
        {
            LogBlock("현재 해역 보스를 먼저 처치해야 합니다.");
            return false;
        }

        if (!RunManager.Instance.CanAdvanceToNextRegion(out _, out string blockReason))
        {
            LogBlock(blockReason);
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

        if (wormholeChoiceUI == null)
        {
            wormholeChoiceUI = FindFirstObjectByType<WormholeChoiceUI>();
        }

        if (wormholeChoiceUI != null)
        {
            wormholeChoiceUI.Open(this);
            return;
        }

        ConfirmEnterNextArea();
    }

    public void ConfirmEnterNextArea()
    {
        if (!CanInteract(gameObject))
        {
            return;
        }

        AudioManager.PlayAt(SoundEventIds.WormholeEnter, transform.position);
        RunManager.Instance.AdvanceToNextRegion();
    }

    private void LogBlock(string reason)
    {
        if (logBlockReason)
        {
            Debug.Log($"[WormholePortal] 상호작용 불가: {reason}", this);
        }
    }
}
