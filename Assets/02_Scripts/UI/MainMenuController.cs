using System;
using System.Collections.Generic;
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
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.98f);
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
    }

    private void OnDisable()
    {
        if (bootstrap != null)
        {
            bootstrap.ProgressLoaded -= HandleProgressLoaded;
        }

        UnbindMenuButtons();
        DisableCancelActionIfOwned();
    }

    private void OnDestroy()
    {
        if (cancelAction != null)
        {
            cancelAction.performed -= HandleCancelPerformed;
        }
    }

    private void ResolveReferences()
    {
        if (bootstrap == null)
        {
            bootstrap = GameBootstrap.Instance ?? FindFirstObjectByType<GameBootstrap>();
        }

        if (sceneFlowManager == null)
        {
            sceneFlowManager = SceneFlowManager.Instance ?? FindFirstObjectByType<SceneFlowManager>();
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
        AddImage(background, BackgroundColor);

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
            Vector2.zero,
            new Vector2(250f, 246f),
            PanelColor
        );

        CreateText(
            "Title",
            mainPanel.transform,
            "VOID SCRAPPER",
            new Vector2(0f, 91f),
            new Vector2(220f, 38f),
            25f,
            TextAlignmentOptions.Center,
            AccentColor
        );

        CreateText(
            "Subtitle",
            mainPanel.transform,
            "EXPEDITION COMMAND",
            new Vector2(0f, 67f),
            new Vector2(220f, 18f),
            9f,
            TextAlignmentOptions.Center,
            MutedTextColor
        );

        continueButton = CreateButton("ContinueButton", mainPanel.transform, "CONTINUE", new Vector2(0f, 30f), new Vector2(180f, 28f));
        newGameButton = CreateButton("NewGameButton", mainPanel.transform, "NEW GAME", new Vector2(0f, -6f), new Vector2(180f, 28f));
        optionsButton = CreateButton("OptionsButton", mainPanel.transform, "OPTIONS", new Vector2(0f, -42f), new Vector2(180f, 28f));
        quitButton = CreateButton("QuitButton", mainPanel.transform, "QUIT", new Vector2(0f, -78f), new Vector2(180f, 28f));

        statusText = CreateText(
            "StatusText",
            mainPanel.transform,
            string.Empty,
            new Vector2(0f, -108f),
            new Vector2(220f, 24f),
            9f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.55f, 0.45f, 1f)
        );
    }

    private void BuildOptionsPanel()
    {
        GameObject sharedOptionsObject = new GameObject("OptionsPanel", typeof(RectTransform));
        sharedOptionsObject.transform.SetParent(runtimeCanvasRoot.transform, false);
        SharedOptionsMenuUI sharedOptions = sharedOptionsObject.AddComponent<SharedOptionsMenuUI>();
        sharedOptions.Configure(inputActions, masterAudioMixer, uiFont);

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
            "START NEW GAME?",
            new Vector2(0f, 38f),
            new Vector2(280f, 24f),
            15f,
            TextAlignmentOptions.Center,
            AccentColor
        );
        CreateText(
            "ConfirmationBody",
            panel.transform,
            "Existing progression will be replaced.",
            new Vector2(0f, 9f),
            new Vector2(280f, 22f),
            10f,
            TextAlignmentOptions.Center,
            TextColor
        );
        confirmNewGameButton = CreateButton("ConfirmNewGameButton", panel.transform, "CONFIRM", new Vector2(-65f, -38f), new Vector2(108f, 26f), 9f);
        cancelNewGameButton = CreateButton("CancelNewGameButton", panel.transform, "CANCEL", new Vector2(65f, -38f), new Vector2(108f, 26f), 9f);
        newGameConfirmationRoot.SetActive(false);
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
            SetStatus("New game could not be created. Check the save log.");
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
        newGameConfirmationRoot.SetActive(false);
        settingsPanel.Open();
        EventSystem.current?.SetSelectedGameObject(optionsBackButton.gameObject);
    }

    private void CloseOptions()
    {
        ShowMainPanel();
        SelectInitialButton();
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
            SetStatus("Tutorial could not be loaded because SceneFlowManager is missing.");
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
            CancelNewGame();
            return;
        }

        if (settingsPanel != null && settingsPanel.TryCancelScreenConfirmation())
        {
            return;
        }

        if (settingsPanel != null && settingsPanel.IsOpen)
        {
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
