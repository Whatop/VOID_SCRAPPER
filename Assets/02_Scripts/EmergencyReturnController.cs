using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EmergencyReturnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerCombatState combatState;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private EmergencyReturnGaugeUI gaugeUI;
    [SerializeField] private EmergencyReturnExitSequence exitSequence;
    [SerializeField] private GameBalanceConfig balanceConfig;

    [Header("Rule")]
    [SerializeField] private float prepareDuration = 2f;
    [SerializeField] private bool cancelOnMovement = true;
    [SerializeField] private float movementCancelDistance = 0.15f;
    [SerializeField] private bool blockDuringBossBattle = true;
    [Tooltip("홀드 입력을 쓰는 경우 게이지가 가득 찬 뒤 버튼을 놓아야 복귀합니다.")]
    [SerializeField] private bool completeOnReleaseAfterGaugeFull = true;

    [Header("Messages")]
    [SerializeField] private string preparingMessage = "긴급복귀 준비 중...";
    [SerializeField] private string readyMessage = "복귀 준비 완료. 버튼을 놓으면 귀환합니다.";
    [SerializeField] private string canceledMessage = "긴급복귀가 취소되었습니다.";
    [SerializeField] private string noRunMessage = "진행 중인 탐사가 없습니다.";
    [SerializeField] private string combatMessage = "전투 중에는 긴급복귀할 수 없습니다.";
    [SerializeField] private string bossBattleMessage = "보스전 중에는 긴급복귀할 수 없습니다.";
    [SerializeField] private string pausedMessage = "메뉴가 열린 동안에는 긴급복귀할 수 없습니다.";
    [SerializeField] private string movementCancelMessage = "이동하여 긴급복귀가 취소되었습니다.";
    [SerializeField] private string alreadyReturningMessage = "이미 귀환 연출이 진행 중입니다.";

    private Coroutine returnRoutine;
    private bool isPreparing;
    private bool holdRequired;
    private bool gaugeFilled;
    private Vector2 prepareStartPosition;

    public bool IsPreparing => isPreparing;
    public bool GaugeFilled => gaugeFilled;

    private void Reset()
    {
        playerHealth = GetComponent<PlayerHealth>();
        combatState = GetComponent<PlayerCombatState>();
        playerController = GetComponent<PlayerController2D>();
        exitSequence = GetComponent<EmergencyReturnExitSequence>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        CancelEmergencyReturn(false, null);
    }

    /// <summary>
    /// 외부 입력 컨트롤러가 홀드 시작 시 호출합니다.
    /// 입력 자체는 PlayerReinforcementController가 단독 관리합니다.
    /// </summary>
    public bool TryStartByHoldKey()
    {
        return TryStartEmergencyReturn(true);
    }

    public bool TryStartFromMenu()
    {
        return TryStartEmergencyReturn(false);
    }

    public bool TryStartEmergencyReturn(bool requireHold)
    {
        if (isPreparing)
        {
            return false;
        }

        CacheReferences();

        if (!CanStart(out string reason))
        {
            ShowWarning(reason);
            gaugeUI?.Hide();
            return false;
        }

        holdRequired = requireHold;
        isPreparing = true;
        gaugeFilled = false;
        prepareStartPosition = transform.position;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        float duration = GetPrepareDuration();
        gaugeUI?.ShowPreparing(0f, duration);

        if (gaugeUI == null)
        {
            ShowWarning(preparingMessage, ShipCommunicationSeverity.Information);
        }

        returnRoutine = StartCoroutine(PrepareRoutine());
        return true;
    }

    /// <summary>
    /// 외부 입력 컨트롤러가 홀드 버튼을 놓은 프레임에 호출합니다.
    /// </summary>
    public void NotifyHoldReleased()
    {
        if (!isPreparing || !holdRequired)
        {
            return;
        }

        if (gaugeFilled)
        {
            CompleteEmergencyReturn();
        }
        else
        {
            CancelEmergencyReturn(true, canceledMessage);
        }
    }

    public void CancelEmergencyReturnByButton()
    {
        CancelEmergencyReturn(true, canceledMessage);
    }

    private IEnumerator PrepareRoutine()
    {
        float timer = 0f;
        float duration = GetPrepareDuration();

        while (timer < duration)
        {
            if (!CanContinue(out string reason))
            {
                CancelEmergencyReturn(false, null);
                ShowWarning(reason);
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(timer / duration);
            float remaining = Mathf.Max(0f, duration - timer);
            gaugeUI?.ShowPreparing(ratio, remaining);
            yield return null;
        }

        gaugeFilled = true;
        gaugeUI?.ShowReady(readyMessage);

        if (gaugeUI == null)
        {
            ShowWarning(readyMessage, ShipCommunicationSeverity.Confirmation);
        }

        if (!holdRequired || !completeOnReleaseAfterGaugeFull)
        {
            CompleteEmergencyReturn();
            yield break;
        }

        while (isPreparing)
        {
            if (!CanContinue(out string reason))
            {
                CancelEmergencyReturn(false, null);
                ShowWarning(reason);
                yield break;
            }

            gaugeUI?.ShowReady(readyMessage);
            yield return null;
        }
    }

    private bool CanStart(out string reason)
    {
        reason = string.Empty;

        if (exitSequence != null && exitSequence.IsPlaying)
        {
            reason = alreadyReturningMessage;
            return false;
        }

        if (GameplayPauseManager.IsPaused)
        {
            reason = pausedMessage;
            return false;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            reason = noRunMessage;
            return false;
        }

        if (blockDuringBossBattle &&
            GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.BossBattle)
        {
            reason = bossBattleMessage;
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            reason = noRunMessage;
            return false;
        }

        if (combatState != null && !combatState.IsOutOfCombat())
        {
            reason = combatMessage;
            return false;
        }

        return true;
    }

    private bool CanContinue(out string reason)
    {
        if (!CanStart(out reason))
        {
            return false;
        }

        if (!cancelOnMovement)
        {
            return true;
        }

        float movedDistance = Vector2.Distance(prepareStartPosition, transform.position);

        if (movedDistance > movementCancelDistance ||
            (playerController != null && playerController.IsMoving))
        {
            reason = movementCancelMessage;
            return false;
        }

        return true;
    }

    private void CompleteEmergencyReturn()
    {
        if (!isPreparing)
        {
            return;
        }

        isPreparing = false;
        holdRequired = false;
        gaugeFilled = false;
        returnRoutine = null;
        gaugeUI?.Hide();

        GameplayPauseManager.Instance?.ResetAllPauses();

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        GameAudioLoopController.BeginRunEndMusicTransition();

        if (exitSequence == null)
        {
            exitSequence = GetComponent<EmergencyReturnExitSequence>();
        }

        if (exitSequence != null)
        {
            exitSequence.PlayEmergencyReturn();
            return;
        }

        RunManager.Instance.CompleteRun(RunEndReason.EmergencyReturn);
    }

    private void CancelEmergencyReturn(bool showMessage, string message)
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        bool wasPreparing = isPreparing;
        isPreparing = false;
        holdRequired = false;
        gaugeFilled = false;
        gaugeUI?.Hide();

        if (wasPreparing && showMessage && !string.IsNullOrWhiteSpace(message))
        {
            ShowWarning(message);
        }
    }

    private float GetPrepareDuration()
    {
        return balanceConfig != null
            ? Mathf.Max(0.01f, balanceConfig.EmergencyReturnPrepareTime)
            : Mathf.Max(0.01f, prepareDuration);
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (combatState == null)
        {
            combatState = GetComponent<PlayerCombatState>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (exitSequence == null)
        {
            exitSequence = GetComponent<EmergencyReturnExitSequence>();
        }

        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (gaugeUI == null)
        {
            gaugeUI = FindFirstObjectByType<EmergencyReturnGaugeUI>(FindObjectsInactive.Include);
        }
    }

    private void ShowWarning(
        string message,
        ShipCommunicationSeverity severity = ShipCommunicationSeverity.Warning)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowCommunication(
                ShipCommunicationChannel.Navigation,
                message,
                severity
            );
            return;
        }

        WarningMessageUI warningMessageUI = FindFirstObjectByType<WarningMessageUI>();

        if (warningMessageUI != null)
        {
            warningMessageUI.ShowCommunication(
                ShipCommunicationChannel.Navigation,
                message,
                severity
            );
            return;
        }

        Debug.Log(message, this);
    }
}
