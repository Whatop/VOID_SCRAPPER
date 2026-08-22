using System;
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
    private readonly struct RebindRowDefinition
    {
        public readonly string label;
        public readonly string actionMap;
        public readonly string action;
        public readonly int bindingIndex;
        public readonly string fallback;

        public RebindRowDefinition(
            string label,
            string actionMap,
            string action,
            int bindingIndex,
            string fallback)
        {
            this.label = label;
            this.actionMap = actionMap;
            this.action = action;
            this.bindingIndex = bindingIndex;
            this.fallback = fallback;
        }
    }

    private static readonly Color BackgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.94f);
    private static readonly Color MainMenuBackingColor = new Color(0.035f, 0.055f, 0.085f, 0.48f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.17f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.2f, 0.85f, 0.72f, 1f);
    private static readonly Color TextColor = new Color(0.9f, 0.95f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.62f, 0.7f, 0.78f, 1f);

    [Header("Existing Services")]
    [SerializeField] private GameBootstrap bootstrap;
    [SerializeField] private SceneFlowManager sceneFlowManager;

    [Header("Existing Assets")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer masterAudioMixer;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private Sprite[] backgroundAsteroidSprites;
    [SerializeField] private Sprite cursedBackgroundSprite;

    [Header("Runtime View")]
    [SerializeField] private int canvasSortOrder = 200;

    private GameObject runtimeCanvasRoot;
    private GameObject mainPanel;
    private GameObject optionsPanel;
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
    private RectTransform titleRect;
    private RectTransform subtitleRect;
    private Vector2 titleRestPosition;
    private Vector2 subtitleRestPosition;
    private Sequence mainMenuEntranceSequence;
    private Button lastMainMenuSelection;

    private const string ContinueLabel = "이어하기";
    private const string NewGameLabel = "새 게임";
    private const string OptionsLabel = "설정";
    private const string QuitLabel = "종료";

    private static readonly RebindRowDefinition[] RebindRows =
    {
        new RebindRowDefinition("Move Up", "Player", "Move", 1, "W"),
        new RebindRowDefinition("Move Down", "Player", "Move", 2, "S"),
        new RebindRowDefinition("Move Left", "Player", "Move", 3, "A"),
        new RebindRowDefinition("Move Right", "Player", "Move", 4, "D"),
        new RebindRowDefinition("Fire", "Player", "Fire", 0, "LMB"),
        new RebindRowDefinition("Dash", "Player", "Dash", 0, "RMB"),
        new RebindRowDefinition("Radar", "Player", "Radar", 0, "Q"),
        new RebindRowDefinition("Interact", "Player", "Interact", 0, "F"),
        new RebindRowDefinition("Inventory", "Player", "Inventory", 0, "E"),
        new RebindRowDefinition("Map", "Player", "Map", 0, "Tab"),
        new RebindRowDefinition("Reinforcement", "Player", "UseReinforcement", 0, "R"),
        new RebindRowDefinition("Dismantle", "Player", "Dismantle", 0, "G"),
        new RebindRowDefinition("Cancel / Menu", "UI", "Cancel", 0, "Esc")
    };

    private void Awake()
    {
        ResolveReferences();
        BuildRuntimeView();
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

    private void BuildRuntimeView()
    {
        if (runtimeCanvasRoot != null)
        {
            return;
        }

        runtimeCanvasRoot = new GameObject(
            "Canvas_MainMenu",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        runtimeCanvasRoot.transform.SetParent(transform, false);

        Canvas canvas = runtimeCanvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = runtimeCanvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreateStretchObject(
            "Background",
            runtimeCanvasRoot.transform
        );
        AddImage(background, BackgroundColor).raycastTarget = false;
        MainMenuSpaceBackground spaceBackground =
            background.AddComponent<MainMenuSpaceBackground>();
        spaceBackground.Configure(backgroundAsteroidSprites, cursedBackgroundSprite);

        BuildMainPanel();
        BuildOptionsPanel();
        BuildNewGameConfirmation();
        ShowMainPanel();
    }

    private void BuildMainPanel()
    {
        mainPanel = CreatePanel(
            "MainPanel",
            runtimeCanvasRoot.transform,
            new Vector2(-132f, 0f),
            new Vector2(204f, 230f),
            MainMenuBackingColor
        );
        Image backingImage = mainPanel.GetComponent<Image>();
        backingImage.raycastTarget = false;

        GameObject accentBar = CreateRectObject(
            "MenuAccent",
            mainPanel.transform,
            new Vector2(-99f, 0f),
            new Vector2(2f, 208f)
        );
        AddImage(accentBar, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.72f)).raycastTarget = false;

        TextMeshProUGUI title = CreateText(
            "Title",
            mainPanel.transform,
            "VOID SCRAPPER",
            new Vector2(-8f, 88f),
            new Vector2(178f, 34f),
            24f,
            TextAlignmentOptions.Left,
            AccentColor
        );
        titleRect = title.rectTransform;
        titleRestPosition = titleRect.anchoredPosition;
        RegisterEntranceElement(title.gameObject);

        TextMeshProUGUI subtitle = CreateText(
            "Subtitle",
            mainPanel.transform,
            "EXPEDITION COMMAND",
            new Vector2(-8f, 64f),
            new Vector2(178f, 14f),
            7.5f,
            TextAlignmentOptions.Left,
            MutedTextColor
        );
        subtitleRect = subtitle.rectTransform;
        subtitleRestPosition = subtitleRect.anchoredPosition;
        RegisterEntranceElement(subtitle.gameObject);

        GameObject divider = CreateRectObject(
            "TitleDivider",
            mainPanel.transform,
            new Vector2(-10f, 50f),
            new Vector2(160f, 1f)
        );
        AddImage(divider, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f)).raycastTarget = false;

        continueButton = CreateButton("ContinueButton", mainPanel.transform, ContinueLabel, new Vector2(-10f, 26f), new Vector2(158f, 24f), 9f);
        newGameButton = CreateButton("NewGameButton", mainPanel.transform, NewGameLabel, new Vector2(-10f, -6f), new Vector2(158f, 24f), 9f);
        optionsButton = CreateButton("OptionsButton", mainPanel.transform, OptionsLabel, new Vector2(-10f, -38f), new Vector2(158f, 24f), 9f);
        quitButton = CreateButton("QuitButton", mainPanel.transform, QuitLabel, new Vector2(-10f, -70f), new Vector2(158f, 24f), 9f);

        ConfigureMainMenuButton(continueButton);
        ConfigureMainMenuButton(newGameButton);
        ConfigureMainMenuButton(optionsButton);
        ConfigureMainMenuButton(quitButton);

        statusText = CreateText(
            "StatusText",
            mainPanel.transform,
            string.Empty,
            new Vector2(-10f, -101f),
            new Vector2(170f, 24f),
            7f,
            TextAlignmentOptions.Left,
            new Color(1f, 0.55f, 0.45f, 1f)
        );
    }

    private void ConfigureMainMenuButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        Image background = button.targetGraphic as Image;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (background != null)
        {
            background.color = new Color(0.075f, 0.115f, 0.16f, 0.78f);
        }

        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(14f, 0f, 4f, 0f);
        }

        GameObject stripObject = CreateRectObject(
            "SelectionAccent",
            button.transform,
            new Vector2(-77f, 0f),
            new Vector2(2f, 18f)
        );
        Image strip = AddImage(stripObject, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.48f));
        strip.raycastTarget = false;

        MainMenuButtonPresenter presenter = button.gameObject.AddComponent<MainMenuButtonPresenter>();
        presenter.Configure(buttonRect, background, label, strip, AccentColor, TextColor);

        UISoundButton soundButton = button.GetComponent<UISoundButton>();
        if (soundButton == null)
        {
            soundButton = button.gameObject.AddComponent<UISoundButton>();
        }

        // Selection changes are sounded centrally so pointer hover and keyboard
        // navigation cannot both emit the same hover event.
        soundButton.SetClickSoundEnabled(true);
        soundButton.SetHoverSoundEnabled(false);
        soundButton.SetDisabledClickSoundEnabled(true);
        soundButton.SetClickSoundEventId(SoundEventIds.UiClick);
        soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
        RegisterEntranceElement(button.gameObject);
    }

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

    private void RegisterEntranceElement(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        mainMenuEntranceGroups.Add(group);
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
            mainMenuEntranceSequence.Insert(i * 0.055f, group.DOFade(1f, 0.18f));
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
                mainMenuEntranceGroups[i].alpha = 1f;
            }
        }
    }

    private void BuildOptionsPanel()
    {
        GameObject sharedOptionsObject = new GameObject("OptionsPanel", typeof(RectTransform));
        sharedOptionsObject.transform.SetParent(runtimeCanvasRoot.transform, false);
        SharedOptionsMenuUI sharedOptions = sharedOptionsObject.AddComponent<SharedOptionsMenuUI>();
        sharedOptions.Configure(inputActions, masterAudioMixer, uiFont, true, true);

        optionsPanel = sharedOptionsObject;
        optionsBackButton = sharedOptions.BackButton;
        settingsTabController = sharedOptions.TabController;
        settingsPanel = sharedOptions.SettingsPanel;
        displayConfirmationRoot = sharedOptions.DisplayConfirmationRoot;
    }

    private void BuildGameplayTab(Transform parent)
    {
        CreateText(
            "GameplayHelp",
            parent,
            "Changes apply immediately and are stored as user preferences.",
            new Vector2(0f, 59f),
            new Vector2(410f, 18f),
            9f,
            TextAlignmentOptions.Center,
            MutedTextColor
        );

        CreateToggleRow("ShowHintsToggle", parent, "Show HUD control hints", 22f);
        CreateSliderRow("CameraShakeSlider", parent, "Camera shake", -16f, 0f, 1f);
        CreateSliderRow("WarningOpacitySlider", parent, "Warning intensity", -54f, 0.25f, 1f);
    }

    private void BuildSoundTab(Transform parent)
    {
        CreateSliderRow("MasterSlider", parent, "Master", 56f, 0f, 1f);
        CreateSliderRow("BgmSlider", parent, "BGM", 28f, 0f, 1f);
        CreateSliderRow("SfxSlider", parent, "SFX", 0f, 0f, 1f);
        CreateSliderRow("AmbientSlider", parent, "Ambient", -28f, 0f, 1f);
        CreateSliderRow("UiSlider", parent, "UI", -56f, 0f, 1f);
    }

    private List<InputRebindButtonUI> BuildKeyboardTab(Transform parent)
    {
        List<InputRebindButtonUI> rows = new List<InputRebindButtonUI>(RebindRows.Length);

        CreateText(
            "KeyboardHelp",
            parent,
            "Select a binding to rebind. Move uses separate composite parts.",
            new Vector2(-50f, 68f),
            new Vector2(320f, 16f),
            8f,
            TextAlignmentOptions.Left,
            MutedTextColor
        );

        Button resetButton = CreateButton(
            "ResetBindingsButton",
            parent,
            "RESET ALL",
            new Vector2(168f, 67f),
            new Vector2(72f, 20f),
            8f
        );

        for (int i = 0; i < RebindRows.Length; i++)
        {
            RebindRowDefinition definition = RebindRows[i];
            int column = i < 7 ? 0 : 1;
            int rowIndex = column == 0 ? i : i - 7;
            float columnCenter = column == 0 ? -108f : 108f;
            float y = 45f - (rowIndex * 20f);

            CreateText(
                definition.label + "Label",
                parent,
                definition.label,
                new Vector2(columnCenter - 49f, y),
                new Vector2(98f, 18f),
                8f,
                TextAlignmentOptions.Left,
                TextColor
            );

            Button rebindButton = CreateButton(
                definition.label.Replace(" ", string.Empty) + "BindingButton",
                parent,
                definition.fallback,
                new Vector2(columnCenter + 58f, y),
                new Vector2(82f, 18f),
                8f
            );
            TextMeshProUGUI bindingText = rebindButton.GetComponentInChildren<TextMeshProUGUI>();
            InputRebindButtonUI rebindRow = rebindButton.gameObject.AddComponent<InputRebindButtonUI>();
            rebindRow.Configure(
                inputActions,
                definition.actionMap,
                definition.action,
                "Keyboard&Mouse",
                definition.bindingIndex,
                rebindButton,
                null,
                bindingText,
                definition.fallback,
                "PRESS KEY"
            );
            rows.Add(rebindRow);
        }

        return rows;
    }

    private void BuildDisplayTab(
        Transform parent,
        out TMP_Dropdown resolutionDropdown,
        out TMP_Dropdown fullScreenDropdown,
        out Toggle vSyncToggle,
        out TMP_Dropdown frameLimitDropdown,
        out Button applyButton)
    {
        CreateText("ResolutionLabel", parent, "Resolution", new Vector2(-108f, 58f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        resolutionDropdown = CreateDropdown("ResolutionDropdown", parent, new Vector2(-108f, 35f), new Vector2(180f, 24f));

        CreateText("FullscreenLabel", parent, "Window mode", new Vector2(108f, 58f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        fullScreenDropdown = CreateDropdown("FullscreenDropdown", parent, new Vector2(108f, 35f), new Vector2(180f, 24f));

        vSyncToggle = CreateToggleRow("VSyncToggle", parent, "VSync", -18f, -108f);

        CreateText("FrameLimitLabel", parent, "Frame limit", new Vector2(108f, 3f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        frameLimitDropdown = CreateDropdown("FrameLimitDropdown", parent, new Vector2(108f, -20f), new Vector2(180f, 24f));

        applyButton = CreateButton("ApplyDisplayButton", parent, "APPLY DISPLAY", new Vector2(0f, -63f), new Vector2(130f, 24f), 9f);
    }

    private void BuildDisplayConfirmation(
        out GameObject confirmationRoot,
        out TextMeshProUGUI confirmationText,
        out Button keepButton,
        out Button revertButton)
    {
        confirmationRoot = CreateStretchObject("DisplayConfirmation", optionsPanel.transform);
        AddImage(confirmationRoot, new Color(0f, 0f, 0f, 0.78f));

        GameObject panel = CreatePanel("ConfirmationPanel", confirmationRoot.transform, Vector2.zero, new Vector2(310f, 118f), PanelColor);
        confirmationText = CreateText(
            "ConfirmationText",
            panel.transform,
            "Keep these display settings?",
            new Vector2(0f, 24f),
            new Vector2(270f, 48f),
            12f,
            TextAlignmentOptions.Center,
            TextColor
        );
        keepButton = CreateButton("KeepDisplayButton", panel.transform, "KEEP", new Vector2(-62f, -34f), new Vector2(100f, 24f), 9f);
        revertButton = CreateButton("RevertDisplayButton", panel.transform, "REVERT", new Vector2(62f, -34f), new Vector2(100f, 24f), 9f);
        confirmationRoot.SetActive(false);
    }

    private void BuildNewGameConfirmation()
    {
        newGameConfirmationRoot = CreateStretchObject(
            "NewGameConfirmation",
            runtimeCanvasRoot.transform
        );
        AddImage(newGameConfirmationRoot, new Color(0f, 0f, 0f, 0.8f));

        GameObject panel = CreatePanel(
            "ConfirmationPanel",
            newGameConfirmationRoot.transform,
            Vector2.zero,
            new Vector2(320f, 130f),
            PanelColor
        );
        CreateText(
            "ConfirmationTitle",
            panel.transform,
            "새 게임을 시작하시겠습니까?",
            new Vector2(0f, 38f),
            new Vector2(280f, 24f),
            15f,
            TextAlignmentOptions.Center,
            AccentColor
        );
        CreateText(
            "ConfirmationBody",
            panel.transform,
            "기존 진행 상황이 초기화됩니다.",
            new Vector2(0f, 9f),
            new Vector2(280f, 22f),
            10f,
            TextAlignmentOptions.Center,
            TextColor
        );
        confirmNewGameButton = CreateButton("ConfirmNewGameButton", panel.transform, "확인", new Vector2(-65f, -38f), new Vector2(108f, 26f), 9f);
        cancelNewGameButton = CreateButton("CancelNewGameButton", panel.transform, "취소", new Vector2(65f, -38f), new Vector2(108f, 26f), 9f);
        ConfigureButtonSound(confirmNewGameButton, SoundEventIds.UiActivate);
        ConfigureButtonSound(cancelNewGameButton, SoundEventIds.UiBack);
        newGameConfirmationRoot.SetActive(false);
    }

    private static void ConfigureButtonSound(Button button, string clickEventId)
    {
        if (button == null)
        {
            return;
        }

        UISoundButton soundButton = button.GetComponent<UISoundButton>();
        if (soundButton == null)
        {
            soundButton = button.gameObject.AddComponent<UISoundButton>();
        }

        soundButton.SetClickSoundEnabled(true);
        soundButton.SetHoverSoundEnabled(true);
        soundButton.SetDisabledClickSoundEnabled(true);
        soundButton.SetClickSoundEventId(clickEventId);
        soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
        soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
    }

    private void BindMenuButtons()
    {
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
        else if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
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

    private Slider FindSlider(string objectName)
    {
        Transform target = FindChildRecursive(optionsPanel.transform, objectName);
        return target != null ? target.GetComponent<Slider>() : null;
    }

    private Toggle FindToggle(string objectName)
    {
        Transform target = FindChildRecursive(optionsPanel.transform, objectName);
        return target != null ? target.GetComponent<Toggle>() : null;
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindChildRecursive(optionsPanel.transform, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private GameObject CreatePanel(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject panel = CreateRectObject(objectName, parent, anchoredPosition, size);
        AddImage(panel, color);
        return panel;
    }

    private GameObject CreateRectObject(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return target;
    }

    private static GameObject CreateStretchObject(string objectName, Transform parent)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return target;
    }

    private Image AddImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>();

        if (image == null)
        {
            Graphic existingGraphic = target.GetComponent<Graphic>();
            if (existingGraphic != null)
            {
                throw new InvalidOperationException(
                    $"Cannot add an Image to '{target.name}' because it already has " +
                    $"a {existingGraphic.GetType().Name} Graphic component."
                );
            }

            image = target.AddComponent<Image>();
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        return image;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string value,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateRectObject(objectName, parent, anchoredPosition, size);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        ApplyUiFont(text);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize = 11f)
    {
        GameObject buttonObject = CreateRectObject(objectName, parent, anchoredPosition, size);
        Image image = AddImage(buttonObject, ButtonColor);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 1f, 0.95f, 1f);
        colors.pressedColor = new Color(0.65f, 0.85f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.38f, 0.42f, 0.46f, 0.6f);
        button.colors = colors;

        GameObject labelObject = CreateStretchObject("Label", buttonObject.transform);
        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        ApplyUiFont(labelText);
        labelText.text = label;
        labelText.fontSize = fontSize;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = TextColor;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;
        return button;
    }

    private Slider CreateSliderRow(
        string objectName,
        Transform parent,
        string label,
        float y,
        float minimum,
        float maximum)
    {
        CreateText(
            objectName + "Label",
            parent,
            label,
            new Vector2(-145f, y),
            new Vector2(110f, 20f),
            9f,
            TextAlignmentOptions.Left,
            TextColor
        );
        return CreateSlider(objectName, parent, new Vector2(55f, y), new Vector2(260f, 16f), minimum, maximum);
    }

    private Slider CreateSlider(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float minimum,
        float maximum)
    {
        GameObject sliderObject = CreateRectObject(objectName, parent, anchoredPosition, size);
        Image background = AddImage(sliderObject, new Color(0.08f, 0.1f, 0.14f, 1f));

        GameObject fillArea = CreateStretchObject("Fill Area", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.offsetMin = new Vector2(4f, 4f);
        fillAreaRect.offsetMax = new Vector2(-4f, -4f);
        GameObject fillObject = CreateStretchObject("Fill", fillArea.transform);
        Image fillImage = AddImage(fillObject, AccentColor);

        GameObject handleArea = CreateStretchObject("Handle Slide Area", sliderObject.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.offsetMin = new Vector2(6f, 0f);
        handleAreaRect.offsetMax = new Vector2(-6f, 0f);
        GameObject handleObject = CreateRectObject("Handle", handleArea.transform, Vector2.zero, new Vector2(12f, 18f));
        Image handleImage = AddImage(handleObject, TextColor);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleObject.GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = maximum;
        background.raycastTarget = true;
        fillImage.raycastTarget = false;
        return slider;
    }

    private Toggle CreateToggleRow(
        string objectName,
        Transform parent,
        string label,
        float y,
        float x = 0f)
    {
        GameObject toggleObject = CreateRectObject(objectName, parent, new Vector2(x - 90f, y), new Vector2(18f, 18f));
        Image background = AddImage(toggleObject, new Color(0.08f, 0.1f, 0.14f, 1f));
        GameObject checkObject = CreateStretchObject("Checkmark", toggleObject.transform);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.offsetMin = new Vector2(3f, 3f);
        checkRect.offsetMax = new Vector2(-3f, -3f);
        Image checkmark = AddImage(checkObject, AccentColor);

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;

        CreateText(
            objectName + "Label",
            parent,
            label,
            new Vector2(x + 31f, y),
            new Vector2(210f, 20f),
            9f,
            TextAlignmentOptions.Left,
            TextColor
        );
        return toggle;
    }

    private TMP_Dropdown CreateDropdown(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(default);
        dropdownObject.name = objectName;
        dropdownObject.transform.SetParent(parent, false);
        RectTransform rect = dropdownObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        ConfigureDropdownPresentation(dropdownObject);

        if (dropdown.captionText != null)
        {
            ApplyUiFont(dropdown.captionText);
            dropdown.captionText.fontSize = 9f;
            dropdown.captionText.color = TextColor;
        }

        if (dropdown.itemText != null)
        {
            ApplyUiFont(dropdown.itemText);
            dropdown.itemText.fontSize = 9f;
        }

        return dropdown;
    }

    private void ConfigureDropdownPresentation(GameObject dropdownObject)
    {
        ConfigureSolidImage(dropdownObject.GetComponent<Image>(), ButtonColor);

        Transform template = FindChildRecursive(dropdownObject.transform, "Template");
        if (template != null)
        {
            ConfigureSolidImage(template.GetComponent<Image>(), PanelColor);
        }

        Transform viewport = FindChildRecursive(dropdownObject.transform, "Viewport");
        if (viewport != null)
        {
            Mask legacyMask = viewport.GetComponent<Mask>();
            if (legacyMask != null)
            {
                legacyMask.enabled = false;
            }

            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null)
            {
                viewportImage.enabled = false;
            }

            if (viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
        }

        Transform arrow = FindChildRecursive(dropdownObject.transform, "Arrow");
        if (arrow != null)
        {
            Image arrowImage = arrow.GetComponent<Image>();
            if (arrowImage != null)
            {
                arrowImage.enabled = false;
            }

            Transform glyph = arrow.Find("Glyph");
            if (glyph == null)
            {
                glyph = CreateStretchObject("Glyph", arrow).transform;
            }

            TextMeshProUGUI arrowText = glyph.GetComponent<TextMeshProUGUI>();
            if (arrowText == null)
            {
                Graphic existingGraphic = glyph.GetComponent<Graphic>();
                if (existingGraphic != null)
                {
                    throw new InvalidOperationException(
                        $"Dropdown arrow glyph '{glyph.name}' already has an incompatible " +
                        $"{existingGraphic.GetType().Name} Graphic component."
                    );
                }

                arrowText = glyph.gameObject.AddComponent<TextMeshProUGUI>();
            }

            arrowText.text = "v";
            ApplyUiFont(arrowText);
            arrowText.fontSize = 10f;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = TextColor;
            arrowText.raycastTarget = false;
        }

        Transform scrollbar = FindChildRecursive(dropdownObject.transform, "Scrollbar");
        if (scrollbar != null)
        {
            ConfigureSolidImage(scrollbar.GetComponent<Image>(), new Color(0.06f, 0.08f, 0.11f, 1f));

            Transform handle = FindChildRecursive(scrollbar, "Handle");
            if (handle != null)
            {
                ConfigureSolidImage(handle.GetComponent<Image>(), AccentColor);
            }
        }

        Transform itemBackground = FindChildRecursive(dropdownObject.transform, "Item Background");
        if (itemBackground != null)
        {
            ConfigureSolidImage(itemBackground.GetComponent<Image>(), ButtonColor);
        }

        Transform itemCheckmark = FindChildRecursive(dropdownObject.transform, "Item Checkmark");
        if (itemCheckmark != null)
        {
            ConfigureSolidImage(itemCheckmark.GetComponent<Image>(), AccentColor);
            RectTransform checkmarkRect = itemCheckmark.GetComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(8f, 8f);
            checkmarkRect.anchoredPosition = new Vector2(10f, 0f);
        }
    }

    private void ApplyUiFont(TMP_Text text)
    {
        if (text != null && uiFont != null)
        {
            text.font = uiFont;
        }
    }

    private static void ConfigureSolidImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
    }
}

[DisallowMultipleComponent]
public sealed class MainMenuButtonPresenter : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    private const float TransitionDuration = 0.12f;

    private RectTransform targetRect;
    private Image background;
    private TextMeshProUGUI label;
    private Image accentStrip;
    private Vector2 restPosition;
    private Color restBackgroundColor;
    private Color restLabelColor;
    private Color activeAccentColor;
    private bool pointerInside;
    private bool selected;
    private bool configured;

    public void Configure(
        RectTransform rect,
        Image backgroundImage,
        TextMeshProUGUI labelText,
        Image strip,
        Color accentColor,
        Color labelColor)
    {
        targetRect = rect;
        background = backgroundImage;
        label = labelText;
        accentStrip = strip;
        restPosition = targetRect != null ? targetRect.anchoredPosition : Vector2.zero;
        restBackgroundColor = background != null ? background.color : Color.white;
        restLabelColor = label != null ? label.color : labelColor;
        activeAccentColor = accentColor;
        configured = true;
        ApplyVisual(false, true);
    }

    private void OnDisable()
    {
        pointerInside = false;
        selected = false;
        KillTweens();
        ApplyVisual(false, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        EventSystem.current?.SetSelectedGameObject(gameObject);
        ApplyVisual(true, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        ApplyVisual(selected, false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        ApplyVisual(true, false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        ApplyVisual(pointerInside, false);
    }

    private void ApplyVisual(bool active, bool immediate)
    {
        if (!configured)
        {
            return;
        }

        KillTweens();
        Vector2 targetPosition = restPosition + (active ? Vector2.right * 4f : Vector2.zero);
        Color targetBackground = active
            ? new Color(0.11f, 0.2f, 0.25f, 0.94f)
            : restBackgroundColor;
        Color targetLabel = active ? Color.white : restLabelColor;
        float targetStripAlpha = active ? 1f : 0.48f;

        if (immediate)
        {
            if (targetRect != null)
            {
                targetRect.anchoredPosition = targetPosition;
            }

            if (background != null)
            {
                background.color = targetBackground;
            }

            if (label != null)
            {
                label.color = targetLabel;
            }

            SetStripAlpha(targetStripAlpha);
            return;
        }

        if (targetRect != null)
        {
            targetRect.DOAnchorPos(targetPosition, TransitionDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        if (background != null)
        {
            background.DOColor(targetBackground, TransitionDuration).SetUpdate(true);
        }

        if (label != null)
        {
            label.DOColor(targetLabel, TransitionDuration).SetUpdate(true);
        }

        if (accentStrip != null)
        {
            Color stripColor = activeAccentColor;
            stripColor.a = targetStripAlpha;
            accentStrip.DOColor(stripColor, TransitionDuration).SetUpdate(true);
        }
    }

    private void KillTweens()
    {
        targetRect?.DOKill(false);
        background?.DOKill(false);
        label?.DOKill(false);
        accentStrip?.DOKill(false);
    }

    private void SetStripAlpha(float alpha)
    {
        if (accentStrip == null)
        {
            return;
        }

        Color color = activeAccentColor;
        color.a = alpha;
        accentStrip.color = color;
    }
}

[DisallowMultipleComponent]
public sealed class MainMenuSpaceBackground : MonoBehaviour
{
    private sealed class Drifter
    {
        public RectTransform rect;
        public Vector2 position;
        public Vector2 velocity;
        public float radius;
        public float mass;
        public float rotation;
        public float angularVelocity;
    }

    private const float HalfWidth = 240f;
    private const float HalfHeight = 135f;
    private const int AsteroidCount = 7;
    private const float AsteroidMinX = -HalfWidth - 24f;
    private const float AsteroidMaxX = HalfWidth + 24f;
    private const float AsteroidMinY = -HalfHeight - 20f;
    private const float AsteroidMaxY = HalfHeight + 20f;
    private const float AsteroidMinSpeed = 10f;
    private const float AsteroidMaxSpeed = 24f;
    private const float AsteroidMinScale = 10f;
    private const float AsteroidMaxScale = 28f;
    private const float MinAngularSpeed = -34f;
    private const float MaxAngularSpeed = 34f;
    private const float CollisionRestitution = 0.2f;
    private const float CollisionVelocityFloor = 4f;
    private const float SpawnMargin = 8f;
    private const float SpawnDirectionSpreadDegrees = 50f;
    private readonly List<Drifter> asteroidDrifters = new List<Drifter>(AsteroidCount);
    private readonly System.Random random = new System.Random();
    private const float MaxSafeSpeed = 36f;

    private RectTransform cursedRect;
    private Image cursedImage;
    private Vector2 cursedPosition;
    private Vector2 cursedVelocity;
    private float cursedTimer;
    private bool cursedActive;
    private bool configured;

    public void Configure(Sprite[] asteroidSprites, Sprite cursedSprite)
    {
        if (configured)
        {
            return;
        }

        configured = true;
        BuildStaticStars();
        BuildAsteroids(asteroidSprites);
        BuildCursedPasser(cursedSprite);
        ScheduleCursedPasser();
    }

    private void LateUpdate()
    {
        if (!configured)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        UpdateAsteroids(deltaTime);

        UpdateCursedPasser(deltaTime);
    }

    private void UpdateAsteroids(float deltaTime)
    {
        for (int i = 0; i < asteroidDrifters.Count; i++)
        {
            Drifter drifter = asteroidDrifters[i];
            drifter.position += drifter.velocity * deltaTime;
            drifter.rotation += drifter.angularVelocity * deltaTime;
        }

        ResolveAsteroidCollisions();

        for (int i = 0; i < asteroidDrifters.Count; i++)
        {
            Drifter drifter = asteroidDrifters[i];
            if (IsDrifterOutsideBounds(drifter))
            {
                SpawnFromEdge(drifter);
            }

            drifter.rect.anchoredPosition = drifter.position;
            drifter.rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                drifter.rotation
            );
        }
    }

    private void ResolveAsteroidCollisions()
    {
        for (int i = 0; i < asteroidDrifters.Count - 1; i++)
        {
            Drifter first = asteroidDrifters[i];
            for (int j = i + 1; j < asteroidDrifters.Count; j++)
            {
                Drifter second = asteroidDrifters[j];
                Vector2 separation = second.position - first.position;
                float minimumDistance = first.radius + second.radius;
                float distanceSquared = separation.sqrMagnitude;
                if (distanceSquared >= minimumDistance * minimumDistance)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(Mathf.Max(distanceSquared, 0.0001f));
                Vector2 normal = distanceSquared > 0.0001f
                    ? separation / distance
                    : Vector2.right;
                float inverseFirstMass = 1f / Mathf.Max(0.01f, first.mass);
                float inverseSecondMass = 1f / Mathf.Max(0.01f, second.mass);
                float inverseMassTotal = inverseFirstMass + inverseSecondMass;
                float penetration = minimumDistance - distance;

                first.position -= normal * (penetration * inverseFirstMass / inverseMassTotal);
                second.position += normal * (penetration * inverseSecondMass / inverseMassTotal);

                Vector2 relativeVelocity = second.velocity - first.velocity;
                float normalSpeed = Vector2.Dot(relativeVelocity, normal);
                if (normalSpeed < 0f)
                {
                    float impulseMagnitude = -(1f + CollisionRestitution) * normalSpeed /
                                             inverseMassTotal;
                    Vector2 impulse = normal * impulseMagnitude;
                    first.velocity -= impulse * inverseFirstMass;
                    second.velocity += impulse * inverseSecondMass;

                    float tangentSpeed = Vector2.Dot(relativeVelocity, new Vector2(-normal.y, normal.x));
                    first.angularVelocity = Mathf.Clamp(
                        first.angularVelocity - tangentSpeed * 1.1f,
                        -18f,
                        18f
                    );
                    second.angularVelocity = Mathf.Clamp(
                        second.angularVelocity + tangentSpeed * 1.1f,
                        -18f,
                        18f
                    );
                }

                first.velocity = ClampAsteroidVelocity(first.velocity);
                second.velocity = ClampAsteroidVelocity(second.velocity);
            }
        }
    }

    private bool IsDrifterOutsideBounds(Drifter drifter)
    {
        return drifter.position.x < AsteroidMinX - SpawnMargin
            || drifter.position.x > AsteroidMaxX + SpawnMargin
            || drifter.position.y < AsteroidMinY - SpawnMargin
            || drifter.position.y > AsteroidMaxY + SpawnMargin;
    }

    private void BuildStaticStars()
    {
        for (int i = 0; i < 30; i++)
        {
            GameObject starObject = CreateImageObject(
                $"StaticStar_{i:00}",
                transform,
                null,
                new Color(0.55f, 0.68f, 0.78f, NextFloat(0.12f, 0.34f))
            );
            RectTransform rect = starObject.GetComponent<RectTransform>();
            float size = i % 9 == 0 ? 2f : 1f;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = RoundToLogicalPixel(new Vector2(
                NextFloat(-HalfWidth + 8f, HalfWidth - 8f),
                NextFloat(-HalfHeight + 8f, HalfHeight - 8f)
            ));
        }
    }

    private void BuildAsteroids(Sprite[] asteroidSprites)
    {
        if (asteroidSprites == null || asteroidSprites.Length == 0)
        {
            return;
        }

        for (int i = 0; i < AsteroidCount; i++)
        {
            Sprite sprite = asteroidSprites[random.Next(0, asteroidSprites.Length)];
            Color tint = new Color(
                NextFloat(0.33f, 0.62f),
                NextFloat(0.38f, 0.7f),
                NextFloat(0.45f, 0.8f),
                NextFloat(0.32f, 0.58f)
            );
            GameObject asteroidObject = CreateImageObject(
                $"MenuAsteroid_{i:00}",
                transform,
                sprite,
                tint
            );
            RectTransform rect = asteroidObject.GetComponent<RectTransform>();
            float size = NextFloat(AsteroidMinScale, AsteroidMaxScale);
            rect.sizeDelta = new Vector2(size, size);
            float radius = size * 0.34f;

            Drifter drifter = new Drifter
            {
                rect = rect,
                radius = radius,
                mass = Mathf.Max(1f, radius * radius),
                position = Vector2.zero,
                rotation = NextFloat(0f, 360f),
                angularVelocity = NextFloat(MinAngularSpeed, MaxAngularSpeed)
            };

            SpawnFromEdge(drifter);

            rect.anchoredPosition = RoundToLogicalPixel(drifter.position);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(drifter.rotation));
            asteroidDrifters.Add(drifter);
        }
    }

    private void SpawnFromEdge(Drifter drifter)
    {
        int edge = random.Next(0, 4);
        float radius = drifter.radius;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 position = Vector2.zero;
            Vector2 direction = Vector2.zero;
            float angleDegrees;

            switch (edge)
            {
                case 0: // Left
                    position = new Vector2(
                        AsteroidMinX - radius,
                        NextFloat(-HalfHeight + 8f, HalfHeight - 8f)
                    );
                    angleDegrees = NextFloat(-SpawnDirectionSpreadDegrees, SpawnDirectionSpreadDegrees);
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                case 1: // Right
                    position = new Vector2(
                        AsteroidMaxX + radius,
                        NextFloat(-HalfHeight + 8f, HalfHeight - 8f)
                    );
                    angleDegrees = 180f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                case 2: // Bottom
                    position = new Vector2(
                        NextFloat(-HalfWidth + 8f, HalfWidth - 8f),
                        AsteroidMinY - radius
                    );
                    angleDegrees = 90f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                default: // Top
                    position = new Vector2(
                        NextFloat(-HalfWidth + 8f, HalfWidth - 8f),
                        AsteroidMaxY + radius
                    );
                    angleDegrees = -90f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
            }

            float edgeBias = random.Next(0, 2) == 0 ? 0.8f : 1f;
            RandomizeAsteroidVelocity(drifter, direction.normalized);

            bool overlaps = false;
            for (int i = 0; i < asteroidDrifters.Count; i++)
            {
                if (asteroidDrifters[i] == drifter)
                {
                    continue;
                }

                Drifter other = asteroidDrifters[i];
                float minimumDistance = radius + other.radius + 2f;
                if ((position - other.position).sqrMagnitude < minimumDistance * minimumDistance)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                drifter.position = position;
                drifter.velocity *= edgeBias;
                drifter.velocity = ClampAsteroidVelocity(drifter.velocity);
                return;
            }

            edge = random.Next(0, 4);
        }

        drifter.position = new Vector2(
            NextFloat(AsteroidMinX, AsteroidMaxX),
            NextFloat(AsteroidMinY, AsteroidMaxY)
        );
        RandomizeAsteroidVelocity(
            drifter,
            new Vector2(NextFloat(-1f, 1f), NextFloat(-1f, 1f))
        );
        drifter.velocity = ClampAsteroidVelocity(drifter.velocity);
    }

    private void RandomizeAsteroidVelocity(Drifter drifter, Vector2 direction)
    {
        float sizeRatio = 0f;
        if (drifter.radius > 0f)
        {
            sizeRatio = Mathf.Clamp01(
                (drifter.radius * 2f - AsteroidMinScale) / (AsteroidMaxScale - AsteroidMinScale)
            );
        }

        float speed = NextFloat(AsteroidMinSpeed, AsteroidMaxSpeed);
        speed = Mathf.Lerp(speed, AsteroidMinSpeed + 2f, sizeRatio * 0.5f);
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float drift = NextFloat(-0.08f, 0.08f);
        drifter.velocity = direction.normalized * speed + tangent * drift;
        drifter.angularVelocity = NextFloat(MinAngularSpeed, MaxAngularSpeed);
    }

    private static Vector2 ClampAsteroidVelocity(Vector2 velocity)
    {
        float speed = velocity.magnitude;
        if (speed > MaxSafeSpeed)
        {
            return velocity * (MaxSafeSpeed / speed);
        }

        if (speed < AsteroidMinSpeed && speed > 0.001f)
        {
            return velocity * (CollisionVelocityFloor / speed);
        }

        return velocity;
    }

    private void BuildCursedPasser(Sprite cursedSprite)
    {
        GameObject cursedObject = CreateImageObject(
            "PixelCursePasser",
            transform,
            cursedSprite,
            new Color(0.78f, 0.28f, 1f, 0.42f)
        );
        cursedRect = cursedObject.GetComponent<RectTransform>();
        cursedImage = cursedObject.GetComponent<Image>();
        cursedRect.sizeDelta = new Vector2(9f, 9f);
        cursedObject.SetActive(false);

        GameObject ghostObject = CreateImageObject(
            "GlitchGhost",
            cursedRect,
            cursedSprite,
            new Color(0.2f, 0.9f, 1f, 0.16f)
        );
        RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
        ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = new Vector2(9f, 9f);
        ghostRect.anchoredPosition = new Vector2(-2f, 1f);
    }

    private void UpdateCursedPasser(float deltaTime)
    {
        if (cursedRect == null)
        {
            return;
        }

        if (!cursedActive)
        {
            cursedTimer -= deltaTime;

            if (cursedTimer <= 0f)
            {
                cursedActive = true;
                bool enterFromLeft = random.Next(0, 2) == 0;
                cursedPosition = new Vector2(
                    enterFromLeft ? -HalfWidth - 14f : HalfWidth + 14f,
                    NextFloat(-HalfHeight + 22f, HalfHeight - 22f)
                );
                float horizontalSpeed = NextFloat(42f, 55f) * (enterFromLeft ? 1f : -1f);
                cursedVelocity = new Vector2(horizontalSpeed, NextFloat(-1.5f, 1.5f));
                cursedRect.gameObject.SetActive(true);
            }

            return;
        }

        cursedPosition += cursedVelocity * deltaTime;
        cursedRect.anchoredPosition = RoundToLogicalPixel(cursedPosition);

        if (cursedImage != null)
        {
            Color color = cursedImage.color;
            color.a = 0.34f + Mathf.PingPong(Time.unscaledTime * 2.4f, 0.14f);
            cursedImage.color = color;
        }

        if ((cursedVelocity.x > 0f && cursedPosition.x > HalfWidth + 16f) ||
            (cursedVelocity.x < 0f && cursedPosition.x < -HalfWidth - 16f))
        {
            cursedActive = false;
            cursedRect.gameObject.SetActive(false);
            ScheduleCursedPasser();
        }
    }

    private void ScheduleCursedPasser()
    {
        cursedTimer = NextFloat(12f, 22f);
    }

    private float NextFloat(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private static Vector2 RoundToLogicalPixel(Vector2 position)
    {
        return new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
    }

    private static GameObject CreateImageObject(
        string objectName,
        Transform parent,
        Sprite sprite,
        Color color)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = target.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        image.maskable = false;
        return target;
    }
}
