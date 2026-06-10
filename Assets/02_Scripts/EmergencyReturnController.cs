using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public class EmergencyReturnController : MonoBehaviour
{
    [Header("Input Actions Optional")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string emergencyReturnActionName = "EmergencyReturn";

    [Header("Fallback Key")]
    [SerializeField] private Key fallbackKey = Key.F;

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

    [Header("Complete")]
    [Tooltip("켜면 게이지가 100% 된 뒤 F를 떼야 탈출합니다. 끄면 게이지 100% 즉시 탈출합니다.")]
    [SerializeField] private bool completeOnReleaseAfterGaugeFull = true;

    [Header("Messages")]
    [SerializeField] private string readyMessage = "긴급탈출 준비 완료. F를 떼면 탈출합니다.";
    [SerializeField] private string canceledMessage = "긴급탈출이 취소되었습니다.";
    [SerializeField] private string noRunMessage = "진행 중인 탐사가 없습니다.";
    [SerializeField] private string combatMessage = "전투 중에는 긴급탈출할 수 없습니다.";
    [SerializeField] private string bossBattleMessage = "보스전 중에는 긴급탈출할 수 없습니다.";
    [SerializeField] private string pausedMessage = "UI가 열려 있는 동안에는 긴급탈출할 수 없습니다.";
    [SerializeField] private string movementCancelMessage = "이동해서 긴급탈출이 취소되었습니다.";
    [SerializeField] private string alreadyReturningMessage = "이미 긴급탈출 중입니다.";

    private InputAction emergencyReturnAction;
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

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        if (emergencyReturnAction != null)
        {
            emergencyReturnAction.Disable();
        }

        CancelEmergencyReturn(false, null);
    }

    private void Update()
    {
        if (isPreparing)
        {
            return;
        }

        if (WasEmergencyReturnPressedThisFrame())
        {
            TryStartByHoldKey();
        }
    }

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

        if (!CanStart(out string reason))
        {
            ShowWarning(reason);

            if (gaugeUI != null)
            {
                gaugeUI.Hide();
            }

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

        if (gaugeUI != null)
        {
            gaugeUI.ShowPreparing(0f, duration);
        }
        else
        {
            // 게이지 UI가 없는 테스트 씬에서만 최소 피드백 제공.
            ShowWarning("긴급탈출 준비 중...");
        }

        returnRoutine = StartCoroutine(PrepareRoutine());

        return true;
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

            bool releasedThisFrame = holdRequired && WasEmergencyReturnReleasedThisFrame();

            timer += Time.unscaledDeltaTime;

            float ratio = Mathf.Clamp01(timer / duration);
            float remaining = Mathf.Max(0f, duration - timer);

            if (gaugeUI != null)
            {
                gaugeUI.ShowPreparing(ratio, remaining);
            }

            if (releasedThisFrame)
            {
                if (timer >= duration)
                {
                    gaugeFilled = true;
                    CompleteEmergencyReturn();
                }
                else
                {
                    CancelEmergencyReturn(true, canceledMessage);
                }

                yield break;
            }

            yield return null;
        }

        gaugeFilled = true;

        if (gaugeUI != null)
        {
            gaugeUI.ShowReady(readyMessage);
        }
        else
        {
            ShowWarning(readyMessage);
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

            if (gaugeUI != null)
            {
                gaugeUI.ShowReady(readyMessage);
            }

            if (WasEmergencyReturnReleasedThisFrame())
            {
                CompleteEmergencyReturn();
                yield break;
            }

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

        if (cancelOnMovement)
        {
            float movedDistance = Vector2.Distance(prepareStartPosition, transform.position);

            if (movedDistance > movementCancelDistance)
            {
                reason = movementCancelMessage;
                return false;
            }

            if (playerController != null && playerController.IsMoving)
            {
                reason = movementCancelMessage;
                return false;
            }
        }

        return true;
    }

    private void CompleteEmergencyReturn()
    {
        isPreparing = false;
        holdRequired = false;
        gaugeFilled = false;
        returnRoutine = null;

        if (gaugeUI != null)
        {
            gaugeUI.Hide();
        }

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.ResetAllPauses();
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

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
        if (!isPreparing)
        {
            if (gaugeUI != null)
            {
                gaugeUI.Hide();
            }

            return;
        }

        isPreparing = false;
        holdRequired = false;
        gaugeFilled = false;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        if (gaugeUI != null)
        {
            gaugeUI.Hide();
        }

        if (showMessage && !string.IsNullOrWhiteSpace(message))
        {
            ShowWarning(message);
        }
    }

    private float GetPrepareDuration()
    {
        if (balanceConfig != null)
        {
            return Mathf.Max(0.01f, balanceConfig.EmergencyReturnPrepareTime);
        }

        return Mathf.Max(0.01f, prepareDuration);
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

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);

        if (actionMap == null)
        {
            return;
        }

        emergencyReturnAction = actionMap.FindAction(emergencyReturnActionName, false);

        if (emergencyReturnAction != null)
        {
            emergencyReturnAction.Enable();
        }
    }

    private bool WasEmergencyReturnPressedThisFrame()
    {
        if (emergencyReturnAction != null && emergencyReturnAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.wasPressedThisFrame;
    }

    private bool WasEmergencyReturnReleasedThisFrame()
    {
        if (emergencyReturnAction != null && emergencyReturnAction.WasReleasedThisFrame())
        {
            return true;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.wasReleasedThisFrame;
    }

    private void ShowWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowWarning(message);
            return;
        }

        WarningMessageUI warningMessageUI = FindFirstObjectByType<WarningMessageUI>();

        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(message);
            return;
        }

        Debug.Log(message, this);
    }
}