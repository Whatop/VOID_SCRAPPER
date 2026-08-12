using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhileOpen = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private InputAction cancelAction;
    private bool isOpen;

    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool storedCursorState;

    public bool IsOpen => isOpen;

    private void Awake()
    {
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

        if (settingsPanel != null)
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
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        if (settingsPanel != null)
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
        if (GameplayPauseManager.Instance != null && GameplayPauseManager.Instance.TryHandleCancel())
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
        if (closeButton != null)
        {
            ConfigureCloseButtonSound(closeButton);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void UnbindButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
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

        if (settingsPanel != null)
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
}