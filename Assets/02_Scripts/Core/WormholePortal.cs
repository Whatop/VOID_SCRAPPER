using UnityEngine;

[DisallowMultipleComponent]
public class WormholePortal : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionText = "웜홀 진입";
    [SerializeField] private bool requireBossDefeated = true;
    [SerializeField] private bool normalZoneOnly = true;

    [Header("UI")]
    [SerializeField] private WormholeChoiceUI wormholeChoiceUI;

    [Header("Debug")]
    [SerializeField] private bool logBlockReason = true;

    [Header("Optional")]
    [SerializeField] private RadarTarget radarTarget;

    public string InteractionText => interactionText;

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
            LogBlock("interactor가 null입니다.");
            return false;
        }

        if (RunManager.Instance == null)
        {
            LogBlock("RunManager.Instance가 없습니다.");
            return false;
        }

        if (!RunManager.Instance.HasActiveRun)
        {
            LogBlock("활성 Run이 없습니다.");
            return false;
        }

        if (requireBossDefeated && !RunManager.Instance.CurrentRun.BossDefeated)
        {
            LogBlock("보스가 아직 처치되지 않았습니다. 테스트 중이면 Require Boss Defeated를 끄세요.");
            return false;
        }

        if (normalZoneOnly &&
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.Normal)
        {
            LogBlock("현재 지역이 Normal이 아닙니다. 심부 해역에서는 웜홀 진입을 막고 있습니다.");
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
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        if (normalZoneOnly &&
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.Normal)
        {
            return;
        }

        AudioManager.PlayAt(SoundEventIds.WormholeEnter, transform.position);
        RunManager.Instance.EnterDeepZone1();
    }

    private void LogBlock(string reason)
    {
        if (!logBlockReason)
        {
            return;
        }

        Debug.Log($"[WormholePortal] 상호작용 불가: {reason}", this);
    }
}