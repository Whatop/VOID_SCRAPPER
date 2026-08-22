using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
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
    private SharedOptionsMenuUI sharedOptionsMenu;

    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool storedCursorState;

    public bool IsOpen => isOpen;

    public event Action<bool> OpenStateChanged;

    private void Awake()
    {
        if (useSharedOptionsPresentation)
        {
            BuildSharedOptionsMenu();
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
        if (isOpen)
        {
            return;
        }

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
        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.BackRequested += Close;
            sharedOptionsMenu.ReturnToMainMenuRequested += ReturnToMainMenu;
            ConfigureCloseButtonSound(sharedOptionsMenu.BackButton);
        }
        else if (closeButton != null)
        {
            ConfigureCloseButtonSound(closeButton);
            closeButton.onClick.AddListener(Close);
        }

        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }
    }

    private void UnbindButtons()
    {
        if (sharedOptionsMenu != null)
        {
            sharedOptionsMenu.BackRequested -= Close;
            sharedOptionsMenu.ReturnToMainMenuRequested -= ReturnToMainMenu;
        }
        else if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
        }
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

        SceneFlowManager flow = SceneFlowManager.Instance ?? FindFirstObjectByType<SceneFlowManager>();
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
        GameStateManager.Instance?.ChangeState(GameState.Boot);
        SceneManager.LoadSceneAsync(flow.BootSceneName);
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

    private void BuildSharedOptionsMenu()
    {
        GameObject legacyMenuRoot = menuRoot;

        if (legacyMenuRoot != null)
        {
            legacyMenuRoot.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.enabled = false;
        }

        GameObject modalRoot = new GameObject(
            "SettlementSharedOptionsModal",
            typeof(RectTransform),
            typeof(Image)
        );
        modalRoot.SetActive(false);
        modalRoot.transform.SetParent(transform, false);
        modalRoot.layer = gameObject.layer;

        RectTransform modalRect = modalRoot.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.anchoredPosition = Vector2.zero;
        modalRect.sizeDelta = Vector2.zero;

        Image modalBackdrop = modalRoot.GetComponent<Image>();
        modalBackdrop.color = new Color(0.005f, 0.012f, 0.02f, 0.74f);
        modalBackdrop.raycastTarget = true;

        GameObject sharedRoot = new GameObject(
            "SettlementSharedOptions",
            typeof(RectTransform)
        );
        sharedRoot.SetActive(false);
        sharedRoot.transform.SetParent(modalRoot.transform, false);
        sharedRoot.layer = gameObject.layer;

        sharedOptionsMenu = sharedRoot.AddComponent<SharedOptionsMenuUI>();
        sharedOptionsMenu.Configure(
            inputActions,
            masterAudioMixer,
            uiFont,
            true,
            true,
            showReturnToMainMenuAction
        );

        // SettlementSettingsPanel closes its root from Awake. Because this UI is
        // built inactive, defering that Awake until the first Open would close
        // only the options content and leave the modal backdrop visible.
        // Prime the generated hierarchy now so the first real Open is stable.
        modalRoot.SetActive(true);
        sharedRoot.SetActive(true);
        sharedOptionsMenu.Close();
        modalRoot.SetActive(false);

        menuRoot = modalRoot;
        settingsRoot = sharedRoot;
        settingsPanel = null;
        closeButton = sharedOptionsMenu.BackButton;
    }
}
