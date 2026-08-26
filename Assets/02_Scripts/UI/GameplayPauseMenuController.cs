using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameplayPauseMenuController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.17f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.2f, 0.85f, 0.72f, 1f);
    private static readonly Color TextColor = new Color(0.9f, 0.95f, 1f, 1f);

    [Header("Shared Assets")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer masterAudioMixer;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private int canvasSortOrder = 500;

    private GameObject canvasRoot;
    private GameObject pauseRoot;
    private GameObject quitRoot;
    private Button continueButton;
    private Button optionsButton;
    private Button quitButton;
    private Button confirmQuitButton;
    private Button cancelQuitButton;
    private TextMeshProUGUI quitMessage;
    private SharedOptionsMenuUI sharedOptions;
    private InputAction cancelAction;
    private bool cancelActionWasEnabled;
    private bool isOpen;
    private bool ownsPause;
    private bool storedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private ExpeditionHUD expeditionHUD;
    private bool quitTransitionStarted;
    private bool ownsWorldSfxSuppression;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputBindingPersistence.LoadOnce(inputActions);
        BuildView();
        ResolveCancelAction();
        expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        SetClosedPresentation();
    }

    private void OnEnable()
    {
        EnableCancelAction();
        continueButton.onClick.AddListener(Close);
        optionsButton.onClick.AddListener(OpenOptions);
        quitButton.onClick.AddListener(RequestQuit);
        confirmQuitButton.onClick.AddListener(ConfirmQuit);
        cancelQuitButton.onClick.AddListener(CancelQuit);
        sharedOptions.BackRequested += ReturnFromOptions;
    }

    private void OnDisable()
    {
        bool wasOpen = isOpen;
        continueButton?.onClick.RemoveListener(Close);
        optionsButton?.onClick.RemoveListener(OpenOptions);
        quitButton?.onClick.RemoveListener(RequestQuit);
        confirmQuitButton?.onClick.RemoveListener(ConfirmQuit);
        cancelQuitButton?.onClick.RemoveListener(CancelQuit);

        if (sharedOptions != null)
        {
            sharedOptions.BackRequested -= ReturnFromOptions;
        }
        DisableCancelActionIfOwned();
        CloseImmediate();

        if (ownsWorldSfxSuppression)
        {
            AudioManager.SetWorldSfxSuppressed(this, false);
            ownsWorldSfxSuppression = false;
        }

        if (wasOpen && !quitTransitionStarted)
        {
            GameAudioLoopController.ResumeForCurrentState();
        }
    }

    private void OnDestroy()
    {
        if (cancelAction != null)
        {
            cancelAction.performed -= HandleCancelPerformed;
        }

        ReleasePauseOwnership();
    }

    public void Open()
    {
        GameplayPauseManager pauseManager = GameplayPauseManager.Instance;
        if (isOpen || !CanOpenInCurrentState() ||
            (GameplayPauseManager.IsPaused && !pauseManager.IsPausedBy(this)))
        {
            return;
        }

        isOpen = true;
        StoreCursorState();
        canvasRoot.SetActive(true);
        quitRoot.SetActive(false);
        sharedOptions.Close();
        pauseRoot.SetActive(true);
        pauseManager.PushPause(this, "Gameplay Pause Menu");
        pauseManager.RegisterCancelHandler(this, HandleOwnedCancel);
        ownsPause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GameAudioLoopController.PauseEnvironmentForMenu();
        AudioManager.Play(SoundEventIds.UiPause);
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        sharedOptions.Close();
        SetClosedPresentation();
        ReleasePauseOwnership();
        RestoreCursorState();
        GameAudioLoopController.ResumeForCurrentState();
        AudioManager.Play(SoundEventIds.UiBack);
    }

    private void OpenOptions()
    {
        if (!isOpen) return;
        quitRoot.SetActive(false);
        pauseRoot.SetActive(false);
        sharedOptions.Open();
    }

    private void ReturnFromOptions()
    {
        if (!isOpen) return;
        sharedOptions.Close();
        pauseRoot.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(optionsButton.gameObject);
    }

    private void RequestQuit()
    {
        if (!isOpen) return;
        bool allowed = CanReturnToBootWithoutSettling(out string message);
        quitMessage.text = message;
        confirmQuitButton.interactable = allowed;
        quitRoot.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(allowed ? confirmQuitButton.gameObject : cancelQuitButton.gameObject);
    }

    private void ConfirmQuit()
    {
        if (quitTransitionStarted || !CanReturnToBootWithoutSettling(out _))
        {
            return;
        }

        SceneFlowManager flow = SceneFlowManager.Instance ?? FindFirstObjectByType<SceneFlowManager>();

        if (flow == null || flow.IsLoading)
        {
            Debug.LogError("Cannot return to Boot because SceneFlowManager is missing or busy.", this);
            return;
        }

        RunManager runManager = RunManager.Instance;

        if (runManager != null && runManager.HasActiveRun)
        {
            if (!runManager.AbandonActiveRunWithoutRewards())
            {
                return;
            }
        }
        else
        {
            AudioManager.SetWorldSfxSuppressed(this, true, true);
            ownsWorldSfxSuppression = true;
            AudioManager.StopAllLoops();
        }

        quitTransitionStarted = true;
        confirmQuitButton.interactable = false;
        cancelQuitButton.interactable = false;
        sharedOptions.Close();
        flow.LoadBoot();
    }

    private bool CanReturnToBootWithoutSettling(out string message)
    {
        message = "메인 화면으로 돌아가시겠습니까?";
        return true;
    }

    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        GameplayPauseManager pauseManager = GameplayPauseManager.Instance;
        if (pauseManager.TryHandleCancel()) return;
        if (GameplayPauseManager.IsPaused) return;
        Open();
    }

    private void HandleOwnedCancel()
    {
        if (quitRoot.activeSelf)
        {
            CancelQuit();
        }
        else if (sharedOptions.TryCancelScreenConfirmation())
        {
        }
        else if (sharedOptions.IsOpen)
        {
            ReturnFromOptions();
        }
        else
        {
            Close();
        }
    }

    private bool CanOpenInCurrentState()
    {
        if (expeditionHUD != null && expeditionHUD.IsCinematicMode) return false;
        if (GameStateManager.Instance == null) return true;
        GameState state = GameStateManager.Instance.CurrentState;
        return state == GameState.Tutorial || state == GameState.Expedition || state == GameState.BossBattle;
    }

    private void ResolveCancelAction()
    {
        if (cancelAction != null) cancelAction.performed -= HandleCancelPerformed;
        cancelAction = InputBindingUtility.ResolveAction(inputActions, "UI", "Cancel");
        if (cancelAction != null) cancelAction.performed += HandleCancelPerformed;
    }

    private void EnableCancelAction()
    {
        if (cancelAction == null) ResolveCancelAction();
        if (cancelAction == null) return;
        cancelActionWasEnabled = cancelAction.enabled;
        if (!cancelActionWasEnabled) cancelAction.Enable();
    }

    private void DisableCancelActionIfOwned()
    {
        if (cancelAction != null && !cancelActionWasEnabled && cancelAction.enabled) cancelAction.Disable();
    }

    private void CancelQuit()
    {
        quitRoot.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(quitButton.gameObject);
    }

    private void ReleasePauseOwnership()
    {
        if (!ownsPause) return;
        GameplayPauseManager manager = GameplayPauseManager.Instance;
        manager.UnregisterCancelHandler(this);
        manager.PopPause(this);
        ownsPause = false;
    }

    private void CloseImmediate()
    {
        isOpen = false;
        sharedOptions?.Close();
        SetClosedPresentation();
        ReleasePauseOwnership();
        RestoreCursorState();
    }

    private void SetClosedPresentation()
    {
        if (pauseRoot != null) pauseRoot.SetActive(false);
        if (quitRoot != null) quitRoot.SetActive(false);
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    private void StoreCursorState()
    {
        if (storedCursorState) return;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        storedCursorState = true;
    }

    private void RestoreCursorState()
    {
        if (!storedCursorState) return;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        storedCursorState = false;
    }

    private void BuildView()
    {
        canvasRoot = new GameObject("Canvas_PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.matchWidthOrHeight = 0.5f;

        AddImage(CreateStretchObject("Dimmer", canvasRoot.transform), new Color(0f, 0f, 0f, 0.62f));
        pauseRoot = CreatePanel("PauseMenuRoot", canvasRoot.transform, Vector2.zero, new Vector2(220f, 190f), PanelColor);
        CreateText("Title", pauseRoot.transform, "일시정지", new Vector2(0f, 66f), new Vector2(180f, 28f), 18f, AccentColor);
        continueButton = CreateButton("ContinueButton", pauseRoot.transform, "계속하기", new Vector2(0f, 25f));
        optionsButton = CreateButton("OptionsButton", pauseRoot.transform, "설정", new Vector2(0f, -15f));
        quitButton = CreateButton("QuitButton", pauseRoot.transform, "나가기", new Vector2(0f, -55f));

        GameObject optionsObject = new GameObject("OptionsPanel", typeof(RectTransform));
        optionsObject.transform.SetParent(canvasRoot.transform, false);
        sharedOptions = optionsObject.AddComponent<SharedOptionsMenuUI>();
        sharedOptions.Configure(inputActions, masterAudioMixer, uiFont, true, true);

        quitRoot = CreateStretchObject("QuitConfirmationRoot", canvasRoot.transform);
        AddImage(quitRoot, new Color(0f, 0f, 0f, 0.75f));
        GameObject panel = CreatePanel("ConfirmationPanel", quitRoot.transform, Vector2.zero, new Vector2(330f, 126f), PanelColor);
        quitMessage = CreateText("Message", panel.transform, string.Empty, new Vector2(0f, 25f), new Vector2(290f, 48f), 11f, TextColor);
        confirmQuitButton = CreateButton("ConfirmQuitButton", panel.transform, "확인", new Vector2(-65f, -36f), new Vector2(108f, 26f));
        cancelQuitButton = CreateButton("CancelQuitButton", panel.transform, "취소", new Vector2(65f, -36f), new Vector2(108f, 26f));
        quitRoot.SetActive(false);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = CreateRectObject(name, parent, position, size);
        AddImage(panel, color);
        return panel;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2? size = null)
    {
        GameObject target = CreateRectObject(name, parent, position, size ?? new Vector2(160f, 28f));
        Image image = AddImage(target, ButtonColor);
        Button button = target.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateStretchObject("Label", target.transform).AddComponent<TextMeshProUGUI>();
        ApplyFont(text);
        text.text = label;
        text.fontSize = 11f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.raycastTarget = false;
        return button;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        TextMeshProUGUI text = CreateRectObject(name, parent, position, size).AddComponent<TextMeshProUGUI>();
        ApplyFont(text);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateRectObject(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return target;
    }

    private static GameObject CreateStretchObject(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return target;
    }

    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        return image;
    }

    private void ApplyFont(TMP_Text text)
    {
        if (text != null && uiFont != null) text.font = uiFont;
    }
}
