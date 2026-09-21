using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)] // Resolve modal Cancel before EventSystem dropdown/submit dispatch.
public class EscSettingsMenuController : MonoBehaviour
{
    [Header("Input Actions Optional")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "UI";
    [SerializeField] private string cancelActionName = "Cancel";

    [Header("Root")]
    [SerializeField] private GameObject menuRoot;

    [Header("Settings Panel")]
    [SerializeField] private SettlementSettingsPanel settingsPanel;
    [SerializeField] private GameObject settingsRoot;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    [Header("Shared Options Presentation")]
    [SerializeField] private bool useSharedOptionsPresentation;
    [SerializeField] private bool useKoreanSharedOptions;
    [SerializeField] private bool showReturnToMainMenuAction;
    [SerializeField] private Button openButton;
    [SerializeField] private AudioMixer masterAudioMixer;
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhileOpen = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private InputAction cancelAction;
    private bool isOpen;
    [SerializeField] private SharedOptionsMenuUI sharedOptionsMenu;
    [SerializeField] private GameObject sharedOptionsModal;
    private bool sharedOptionsDiagnosticReported;
    private bool sharedOptionsInitialized;
    private SharedOptionsMenuUI subscribedOptionsMenu;
    private Button subscribedCloseButton;
    private Button subscribedOpenButton;

    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool storedCursorState;
    private int lastTransitionFrame = -1;
    private int lastCancelFrame = -1;

    public bool IsOpen => isOpen;

    public event Action<bool> OpenStateChanged;
    public event Action Opening;

    private void Awake()
    {
        if (useSharedOptionsPresentation)
        {
            if (!InitializeSharedOptions()) return;
        }

        if (menuRoot == null)
        {
            menuRoot = gameObject;
        }

        if (settingsPanel == null && settingsRoot != null)
        {
            settingsPanel = settingsRoot.GetComponentInChildren<SettlementSettingsPanel>(true);
        }

        CloseImmediate();
    }

    private void OnEnable()
    {
        BindInput();
        BindButtons();
    }

    private void OnDisable()
    {
        if (cancelAction != null)
        {
            cancelAction.Disable();
        }

        UnbindButtons();

        if (isOpen)
        {
            Close();
        }
    }

    private void Update()
    {
        if (!WasCancelPressedThisFrame())
        {
            return;
        }

        HandleEscape();
    }

    public void Open()
    {
        if (useSharedOptionsPresentation && !InitializeSharedOptions()) return;
        if (isOpen || GameplayPauseManager.IsPaused || SettlementExpeditionLaunchGuard.IsDialogueActive)
        {
            return;
        }

        Opening?.Invoke();
        lastTransitionFrame = Time.frameCount;
        isOpen = true;

        AudioManager.Play(SoundEventIds.UiPause);
        GameAudioLoopController.PauseEnvironmentForMenu();

        StoreCursorState();

        if (menuRoot != null)
        {
            menuRoot.SetActive(true);
        }

        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.Open();
        }
        else if (settingsPanel != null)
        {
            settingsPanel.Open();
        }
        else if (settingsRoot != null)
        {
            settingsRoot.SetActive(true);
        }

        GameplayPauseManager.Instance.PushPause(this, "ESC Settings");
        GameplayPauseManager.Instance.RegisterCancelHandler(this, Close);

        if (showCursorWhileOpen)
        {
            Cursor.visible = true;
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        OpenStateChanged?.Invoke(true);
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        lastTransitionFrame = Time.frameCount;

        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.Close();
        }
        else if (settingsPanel != null)
        {
            settingsPanel.Close();
        }
        else if (settingsRoot != null)
        {
            settingsRoot.SetActive(false);
        }

        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);
            GameplayPauseManager.Instance.PopPause(this);
        }

        RestoreCursorState();
        AudioManager.Play(SoundEventIds.UiBack);
        GameAudioLoopController.ResumeForCurrentState();
        OpenStateChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void HandleEscape()
    {
        if (lastTransitionFrame == Time.frameCount || lastCancelFrame == Time.frameCount) return;
        lastCancelFrame = Time.frameCount;
        if (SettlementExpeditionLaunchGuard.IsDialogueActive) return;
        // Let rebinding and dropdowns consume Cancel; do not also close their containing modal.
        if (sharedOptionsMenu != null && sharedOptionsMenu.TryHandleNestedCancel()) return;
        GameplayPauseManager pauseManager = GameplayPauseManager.Instance;

        if (pauseManager != null && pauseManager.TryHandleCancel())
        {
            return;
        }

        // A dialogue, cinematic, or another owner may intentionally hold pause
        // without exposing a cancel action. Never open settings underneath it.
        if (pauseManager != null && GameplayPauseManager.IsPaused)
        {
            return;
        }

        if (CanOpenInCurrentState())
        {
            Open();
        }
    }

    private bool CanOpenInCurrentState()
    {
        if (GameStateManager.Instance == null)
        {
            return true;
        }

        GameState state = GameStateManager.Instance.CurrentState;

        return state == GameState.Settlement ||
               state == GameState.Tutorial ||
               state == GameState.Expedition ||
               state == GameState.BossBattle;
    }

    private void BindInput()
    {
        cancelAction = InputBindingUtility.ResolveAction(
            inputActions,
            actionMapName,
            cancelActionName
        );
        cancelAction?.Enable();
    }

    private bool WasCancelPressedThisFrame()
    {
        if (cancelAction != null)
        {
            return cancelAction.WasPressedThisFrame();
        }

        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void BindButtons()
    {
        UnbindButtons();
        subscribedOptionsMenu = sharedOptionsMenu;
        subscribedCloseButton = sharedOptionsMenu == null ? closeButton : null;
        subscribedOpenButton = openButton;
        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.BackRequested += Close;
            sharedOptionsMenu.ReturnToMainMenuRequested += ReturnToMainMenu;
            ConfigureCloseButtonSound(sharedOptionsMenu.BackButton);
        }
        else if (closeButton != null)
        {
            ConfigureCloseButtonSound(closeButton);
            BindOwnedClick(closeButton, Close);
        }

        if (openButton != null)
        {
            BindOwnedClick(openButton, Open);
        }
    }

    private void UnbindButtons()
    {
        if (subscribedOptionsMenu != null)
        {
            subscribedOptionsMenu.BackRequested -= Close;
            subscribedOptionsMenu.ReturnToMainMenuRequested -= ReturnToMainMenu;
        }
        if (subscribedCloseButton != null)
        {
            subscribedCloseButton.onClick.RemoveListener(Close);
        }

        if (subscribedOpenButton != null)
        {
            subscribedOpenButton.onClick.RemoveListener(Open);
        }
        subscribedOptionsMenu = null;
        subscribedCloseButton = null;
        subscribedOpenButton = null;
    }

    private void BindOwnedClick(Button button, UnityEngine.Events.UnityAction action)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == action.Method.Name &&
                button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off) return;
        button.onClick.AddListener(action);
    }

    private void ReturnToMainMenu()
    {
        if (!showReturnToMainMenuAction)
        {
            return;
        }

        GameState state = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Settlement;
        if (state != GameState.Settlement)
        {
            Debug.LogWarning("메인 화면 복귀는 정착지 설정에서만 사용할 수 있습니다.", this);
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            Debug.LogWarning("진행 중인 탐사가 있어 메인 화면으로 이동할 수 없습니다.", this);
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        SceneFlowManager flow = SceneFlowManager.Instance;
        if (flow == null) flow = FindFirstObjectByType<SceneFlowManager>();
        if (flow == null)
        {
            Debug.LogError("SceneFlowManager가 없어 메인 화면으로 이동할 수 없습니다.", this);
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        if (flow.IsLoading)
        {
            return;
        }

        if (SaveManager.Instance == null || PermanentProgress.Instance == null)
        {
            Debug.LogError("진행 상황 저장 서비스를 찾을 수 없어 메인 화면 이동을 취소합니다.", this);
            AudioManager.Play(SoundEventIds.UiDisabled);
            return;
        }

        SaveManager.Instance.Save(PermanentProgress.Instance);
        Close();
        flow.LoadBoot();
    }

    private void ConfigureCloseButtonSound(Button targetButton)
    {
        if (targetButton == null)
        {
            return;
        }

        UISoundButton soundButton = targetButton.GetComponent<UISoundButton>();
        if (soundButton == null)
        {
            soundButton = targetButton.gameObject.AddComponent<UISoundButton>();
        }

        soundButton.SetClickSoundEnabled(false);
        soundButton.SetHoverSoundEnabled(true);
        soundButton.SetDisabledClickSoundEnabled(true);
        soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
        soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
    }

    private void StoreCursorState()
    {
        if (storedCursorState)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        storedCursorState = true;
    }

    private void RestoreCursorState()
    {
        if (!storedCursorState)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        storedCursorState = false;
    }

    private void CloseImmediate()
    {
        isOpen = false;

        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.Close();
        }
        else if (settingsPanel != null)
        {
            settingsPanel.Close();
        }
        else if (settingsRoot != null)
        {
            settingsRoot.SetActive(false);
        }

        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    public void CollectSharedOptionsBindingErrors(System.Collections.Generic.List<string> errors)
    {
        if (!useSharedOptionsPresentation) return;
        void Issue(string field, Component value, Transform expected, string reason)
        {
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(
                "EscSettingsMenuController." + field, value, expected, gameObject.scene, reason));
        }
        if (sharedOptionsModal == null || sharedOptionsModal.scene != gameObject.scene ||
            sharedOptionsModal.transform == transform || !sharedOptionsModal.transform.IsChildOf(transform))
            Issue(nameof(sharedOptionsModal), sharedOptionsModal != null ? sharedOptionsModal.transform : null, transform, "Missing binding, scene ownership or ancestry");
        Transform modal = sharedOptionsModal != null ? sharedOptionsModal.transform : transform;
        if (sharedOptionsMenu == null || sharedOptionsMenu.gameObject.scene != gameObject.scene ||
            sharedOptionsMenu.transform == modal || !sharedOptionsMenu.transform.IsChildOf(modal))
            Issue(nameof(sharedOptionsMenu), sharedOptionsMenu, modal, "Missing binding, scene ownership or ancestry");
        else sharedOptionsMenu.CollectAuthoredBindingErrors(errors);
    }

    private bool SharedOptionsReady()
    {
        if (!useSharedOptionsPresentation) return true;
        var errors = new System.Collections.Generic.List<string>();
        CollectSharedOptionsBindingErrors(errors);
        if (errors.Count == 0) return true;
        if (!sharedOptionsDiagnosticReported)
        {
            sharedOptionsDiagnosticReported = true;
            Debug.LogWarning(string.Join("\n", errors) +
                "\nRestore the listed authored Inspector bindings. Settings was skipped before acquiring pause/input.", this);
        }
        return false;
    }

    private bool InitializeSharedOptions()
    {
        if (!SharedOptionsReady()) return false;
        if (sharedOptionsInitialized) return true;
        if (menuRoot != null && menuRoot != sharedOptionsModal) menuRoot.SetActive(false);
        if (settingsPanel != null && settingsPanel != sharedOptionsMenu.SettingsPanel) settingsPanel.enabled = false;
        menuRoot = sharedOptionsModal;
        settingsRoot = sharedOptionsMenu.gameObject;
        settingsPanel = null;
        closeButton = sharedOptionsMenu.BackButton;
        // Run the existing settings Awake/Close sequence once, at runtime, before the
        // first user Open. No construction or authoring helper is involved.
        sharedOptionsModal.SetActive(true);
        sharedOptionsMenu.gameObject.SetActive(true);
        sharedOptionsMenu.Close();
        sharedOptionsModal.SetActive(false);
        sharedOptionsInitialized = true;
        return true;
    }
}
