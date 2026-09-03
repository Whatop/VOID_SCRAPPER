using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Existing Services")]
    [SerializeField] private GameBootstrap bootstrap;
    [SerializeField] private SceneFlowManager sceneFlowManager;

    [Header("Existing Assets")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer masterAudioMixer;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private Sprite[] backgroundAsteroidSprites;
    [SerializeField] private Sprite cursedBackgroundSprite;

    [Header("Authored View")]
    [SerializeField] private BootMainMenuView authoredView;
    [SerializeField] private int canvasSortOrder = 200;

    private GameObject mainPanel;
    private GameObject displayConfirmationRoot;
    private GameObject newGameConfirmationRoot;
    private Button continueButton;
    private Button newGameButton;
    private Button optionsButton;
    private Button quitButton;
    private Button optionsBackButton;
    private Button confirmNewGameButton;
    private Button cancelNewGameButton;
    private TextMeshProUGUI statusText;
    private SettingsMenuTabController settingsTabController;
    private SettlementSettingsPanel settingsPanel;
    private InputAction cancelAction;
    private bool cancelActionWasEnabled;
    private bool transitionStarted;
    private readonly List<CanvasGroup> mainMenuEntranceGroups = new List<CanvasGroup>(6);
    private readonly List<float> mainMenuEntranceRestAlphas = new List<float>(6);
    private RectTransform titleRect;
    private RectTransform subtitleRect;
    private Vector2 titleRestPosition;
    private Vector2 subtitleRestPosition;
    private Sequence mainMenuEntranceSequence;
    private Button lastMainMenuSelection;

    public BootMainMenuView AuthoredView => authoredView;
    public InputActionAsset InputActions => inputActions;
    public AudioMixer MasterAudioMixer => masterAudioMixer;
    public TMP_FontAsset UiFont => uiFont;
    public Sprite[] BackgroundAsteroidSprites => backgroundAsteroidSprites;
    public Sprite CursedBackgroundSprite => cursedBackgroundSprite;
    public int CanvasSortOrder => canvasSortOrder;

    private void Awake()
    {
        ResolveReferences();
        if (!TryBindAuthoredView())
        {
            enabled = false;
            return;
        }

        InputBindingPersistence.LoadOnce(inputActions);
        ResolveCancelAction();
    }

    private void OnEnable()
    {
        if (bootstrap != null)
        {
            bootstrap.ProgressLoaded -= HandleProgressLoaded;
            bootstrap.ProgressLoaded += HandleProgressLoaded;
        }

        EnableCancelAction();
        BindMenuButtons();
    }

    private void Start()
    {
        RefreshContinueState();
        SelectInitialButton();
        PlayMainMenuEntrance();
    }

    private void Update()
    {
        RefreshMainMenuSelectionSound();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || transitionStarted || mainPanel == null ||
            !mainPanel.activeInHierarchy ||
            (newGameConfirmationRoot != null && newGameConfirmationRoot.activeInHierarchy) ||
            !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        Button selectedButton = selectedObject != null
            ? selectedObject.GetComponent<Button>()
            : null;

        if (IsMainMenuButton(selectedButton) && selectedButton.IsInteractable())
        {
            selectedButton.onClick.Invoke();
        }
    }

    private void OnDisable()
    {
        KillMainMenuEntrance();

        if (bootstrap != null)
        {
            bootstrap.ProgressLoaded -= HandleProgressLoaded;
        }

        UnbindMenuButtons();
        DisableCancelActionIfOwned();
    }

    private void OnDestroy()
    {
        KillMainMenuEntrance();

        if (cancelAction != null)
        {
            cancelAction.performed -= HandleCancelPerformed;
        }
    }

    private void ResolveReferences()
    {
        GameBootstrap activeBootstrap = GameBootstrap.Instance;
        if (activeBootstrap != null)
        {
            bootstrap = activeBootstrap;
        }
        else if (bootstrap == null)
        {
            bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        SceneFlowManager activeSceneFlow = SceneFlowManager.Instance;
        if (activeSceneFlow != null)
        {
            sceneFlowManager = activeSceneFlow;
        }
        else if (sceneFlowManager == null)
        {
            sceneFlowManager = FindFirstObjectByType<SceneFlowManager>();
        }
    }

    private bool TryBindAuthoredView()
    {
        if (authoredView == null)
        {
            Debug.LogError(
                $"[{nameof(MainMenuController)}] '{name}' has no authored BootMainMenuView. " +
                "Open Boot and run VOID SCRAPPER > UI > Install Boot Main Menu UI.",
                this);
            return false;
        }

        if (!authoredView.TryGetMissingRuntimeReference(out string missingReference))
        {
            Debug.LogError(
                $"[{nameof(MainMenuController)}] Authored Boot UI '{authoredView.name}' is " +
                $"missing '{missingReference}'. Run Validate Boot Main Menu UI and repair it " +
                "in the Boot scene; runtime replacement UI will not be generated.",
                authoredView);
            return false;
        }

        if (!authoredView.SharedOptions.HasAuthoredLayout)
        {
            Debug.LogError(
                $"[{nameof(MainMenuController)}] '{authoredView.SharedOptions.name}' has no " +
                "serialized options layout. Run Install Boot Main Menu UI in the Boot scene.",
                authoredView.SharedOptions);
            return false;
        }

        mainPanel = authoredView.MenuRoot;
        newGameConfirmationRoot = authoredView.NewGameConfirmationRoot;
        continueButton = authoredView.ContinueButton;
        newGameButton = authoredView.NewGameButton;
        optionsButton = authoredView.SettingsButton;
        quitButton = authoredView.ExitButton;
        confirmNewGameButton = authoredView.ConfirmNewGameButton;
        cancelNewGameButton = authoredView.CancelNewGameButton;
        statusText = authoredView.StatusText;
        titleRect = authoredView.TitleRect;
        subtitleRect = authoredView.SubtitleRect;
        titleRestPosition = titleRect.anchoredPosition;
        subtitleRestPosition = subtitleRect.anchoredPosition;

        authoredView.SharedOptions.Configure(
            inputActions,
            masterAudioMixer,
            uiFont,
            true,
            true);
        optionsBackButton = authoredView.SharedOptions.BackButton;
        settingsTabController = authoredView.SharedOptions.TabController;
        settingsPanel = authoredView.SharedOptions.SettingsPanel;
        displayConfirmationRoot = authoredView.SharedOptions.DisplayConfirmationRoot;

        if (authoredView.SpaceBackground != null)
        {
            authoredView.SpaceBackground.InitializeAuthoredVisuals();
        }
        else
        {
            Debug.LogError(
                $"[{nameof(MainMenuController)}] Authored Boot UI '{authoredView.name}' has no " +
                $"{nameof(MainMenuSpaceBackground)} reference. The menu will remain usable, but " +
                "the animated space background is disabled until the installer repairs it.",
                authoredView);
        }

        mainMenuEntranceGroups.Clear();
        mainMenuEntranceRestAlphas.Clear();
        CanvasGroup[] entranceGroups = authoredView.EntranceGroups;
        for (int i = 0; i < entranceGroups.Length; i++)
        {
            if (entranceGroups[i] != null)
            {
                mainMenuEntranceGroups.Add(entranceGroups[i]);
                mainMenuEntranceRestAlphas.Add(entranceGroups[i].alpha);
            }
        }

        authoredView.ConfigureButtonPresenters();
        ShowMainPanel();
        return true;
    }

#if UNITY_EDITOR
    public void AssignAuthoredView(BootMainMenuView view)
    {
        authoredView = view;
    }
#endif

    private void RefreshMainMenuSelectionSound()
    {
        if (mainPanel == null || !mainPanel.activeInHierarchy || EventSystem.current == null)
        {
            lastMainMenuSelection = null;
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        Button selectedButton = selectedObject != null
            ? selectedObject.GetComponent<Button>()
            : null;

        if (!IsMainMenuButton(selectedButton) || selectedButton == lastMainMenuSelection)
        {
            return;
        }

        lastMainMenuSelection = selectedButton;
        AudioManager.Play(SoundEventIds.UiHover);
    }

    private bool IsMainMenuButton(Button button)
    {
        return button != null &&
               (button == continueButton ||
                button == newGameButton ||
                button == optionsButton ||
                button == quitButton);
    }

    private void PlayMainMenuEntrance()
    {
        KillMainMenuEntrance();

        if (titleRect != null)
        {
            titleRect.anchoredPosition = titleRestPosition + Vector2.left * 8f;
        }

        if (subtitleRect != null)
        {
            subtitleRect.anchoredPosition = subtitleRestPosition + Vector2.left * 6f;
        }

        mainMenuEntranceSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < mainMenuEntranceGroups.Count; i++)
        {
            CanvasGroup group = mainMenuEntranceGroups[i];
            if (group == null)
            {
                continue;
            }

            group.alpha = 0f;
            float restAlpha = i < mainMenuEntranceRestAlphas.Count
                ? mainMenuEntranceRestAlphas[i]
                : 1f;
            mainMenuEntranceSequence.Insert(i * 0.055f, group.DOFade(restAlpha, 0.18f));
        }

        if (titleRect != null)
        {
            mainMenuEntranceSequence.Insert(0f, titleRect.DOAnchorPos(titleRestPosition, 0.22f).SetEase(Ease.OutCubic));
        }

        if (subtitleRect != null)
        {
            mainMenuEntranceSequence.Insert(0.055f, subtitleRect.DOAnchorPos(subtitleRestPosition, 0.2f).SetEase(Ease.OutCubic));
        }
    }

    private void KillMainMenuEntrance()
    {
        if (mainMenuEntranceSequence != null)
        {
            mainMenuEntranceSequence.Kill(false);
            mainMenuEntranceSequence = null;
        }

        if (titleRect != null)
        {
            titleRect.anchoredPosition = titleRestPosition;
        }

        if (subtitleRect != null)
        {
            subtitleRect.anchoredPosition = subtitleRestPosition;
        }

        for (int i = 0; i < mainMenuEntranceGroups.Count; i++)
        {
            if (mainMenuEntranceGroups[i] != null)
            {
                mainMenuEntranceGroups[i].alpha = i < mainMenuEntranceRestAlphas.Count
                    ? mainMenuEntranceRestAlphas[i]
                    : 1f;
            }
        }
    }

    private void BindMenuButtons()
    {
        UnbindMenuButtons();
        continueButton?.onClick.AddListener(ContinueGame);
        newGameButton?.onClick.AddListener(RequestNewGame);
        optionsButton?.onClick.AddListener(OpenOptions);
        quitButton?.onClick.AddListener(QuitGame);
        optionsBackButton?.onClick.AddListener(CloseOptions);
        confirmNewGameButton?.onClick.AddListener(ConfirmNewGame);
        cancelNewGameButton?.onClick.AddListener(CancelNewGame);
    }

    private void UnbindMenuButtons()
    {
        continueButton?.onClick.RemoveListener(ContinueGame);
        newGameButton?.onClick.RemoveListener(RequestNewGame);
        optionsButton?.onClick.RemoveListener(OpenOptions);
        quitButton?.onClick.RemoveListener(QuitGame);
        optionsBackButton?.onClick.RemoveListener(CloseOptions);
        confirmNewGameButton?.onClick.RemoveListener(ConfirmNewGame);
        cancelNewGameButton?.onClick.RemoveListener(CancelNewGame);
    }

    private void HandleProgressLoaded()
    {
        RefreshContinueState();
    }

    private void RefreshContinueState()
    {
        ResolveReferences();

        if (continueButton != null)
        {
            continueButton.interactable =
                !transitionStarted && bootstrap != null && bootstrap.HasUsableProgression;
        }
    }

    private void ContinueGame()
    {
        if (bootstrap == null || !bootstrap.HasUsableProgression)
        {
            SetStatus("No progression is available to continue.");
            RefreshContinueState();
            return;
        }

        LoadGameEntryPoint();
    }

    private void RequestNewGame()
    {
        if (transitionStarted)
        {
            return;
        }

        SetStatus(string.Empty);

        if (bootstrap != null && bootstrap.HasUsableProgression)
        {
            newGameConfirmationRoot.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(cancelNewGameButton.gameObject);
            return;
        }

        ConfirmNewGame();
    }

    private void ConfirmNewGame()
    {
        if (transitionStarted)
        {
            return;
        }

        newGameConfirmationRoot.SetActive(false);

        if (bootstrap == null || !bootstrap.TryResetProgress())
        {
            SetStatus("새 게임을 시작할 수 없습니다. 저장 로그를 확인하세요.");
            RefreshContinueState();
            return;
        }

        LoadNewGameDestination();
    }

    private void CancelNewGame()
    {
        if (newGameConfirmationRoot != null)
        {
            newGameConfirmationRoot.SetActive(false);
        }

        SelectInitialButton();
    }

    private void OpenOptions()
    {
        if (transitionStarted || settingsPanel == null)
        {
            return;
        }

        mainPanel.SetActive(false);
        lastMainMenuSelection = null;
        newGameConfirmationRoot.SetActive(false);
        settingsPanel.Open();
        settingsTabController?.ShowTab(0);
        settingsTabController?.SelectFirstControlInCurrentTab();
    }

    private void CloseOptions()
    {
        ShowMainPanel();
        lastMainMenuSelection = optionsButton;
        EventSystem.current?.SetSelectedGameObject(
            optionsButton != null ? optionsButton.gameObject : null
        );
    }

    private void ShowMainPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Close();
        }

        if (displayConfirmationRoot != null)
        {
            displayConfirmationRoot.SetActive(false);
        }

        if (newGameConfirmationRoot != null)
        {
            newGameConfirmationRoot.SetActive(false);
        }

        settingsTabController?.ShowTab(0);

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
    }

    private void LoadNewGameDestination()
    {
        if (transitionStarted)
        {
            return;
        }

        ResolveReferences();

        if (sceneFlowManager == null)
        {
            SetStatus("튜토리얼을 불러올 수 없습니다. SceneFlowManager를 확인하세요.");
            return;
        }

        transitionStarted = true;
        continueButton.interactable = false;
        newGameButton.interactable = false;
        optionsButton.interactable = false;
        quitButton.interactable = false;
        sceneFlowManager.LoadTutorial();
    }

    private void LoadGameEntryPoint()
    {
        if (transitionStarted)
        {
            return;
        }

        ResolveReferences();

        if (sceneFlowManager == null)
        {
            SetStatus("Settlement could not be loaded because SceneFlowManager is missing.");
            return;
        }

        transitionStarted = true;
        continueButton.interactable = false;
        newGameButton.interactable = false;
        optionsButton.interactable = false;
        quitButton.interactable = false;
        sceneFlowManager.LoadSettlement();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit requested from Main Menu. The Unity Editor remains open.", this);
#else
        Application.Quit();
#endif
    }

    private void ResolveCancelAction()
    {
        if (cancelAction != null)
        {
            cancelAction.performed -= HandleCancelPerformed;
        }

        cancelAction = InputBindingUtility.ResolveAction(inputActions, "UI", "Cancel");

        if (cancelAction != null)
        {
            cancelAction.performed += HandleCancelPerformed;
        }
    }

    private void EnableCancelAction()
    {
        if (cancelAction == null)
        {
            ResolveCancelAction();
        }

        if (cancelAction == null)
        {
            return;
        }

        cancelActionWasEnabled = cancelAction.enabled;

        if (!cancelActionWasEnabled)
        {
            cancelAction.Enable();
        }
    }

    private void DisableCancelActionIfOwned()
    {
        if (cancelAction != null && !cancelActionWasEnabled && cancelAction.enabled)
        {
            cancelAction.Disable();
        }
    }

    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || transitionStarted)
        {
            return;
        }

        if (newGameConfirmationRoot != null && newGameConfirmationRoot.activeSelf)
        {
            AudioManager.Play(SoundEventIds.UiBack);
            CancelNewGame();
            return;
        }

        if (settingsPanel != null && settingsPanel.TryCancelScreenConfirmation())
        {
            return;
        }

        if (settingsPanel != null && settingsPanel.IsOpen)
        {
            AudioManager.Play(SoundEventIds.UiBack);
            CloseOptions();
        }
    }

    private void SelectInitialButton()
    {
        Button target = continueButton != null && continueButton.interactable
            ? continueButton
            : authoredView != null && authoredView.InitialSelectable != null
                ? authoredView.InitialSelectable
                : newGameButton;

        if (target != null)
        {
            lastMainMenuSelection = target;
            EventSystem.current?.SetSelectedGameObject(target.gameObject);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }
    }
}
