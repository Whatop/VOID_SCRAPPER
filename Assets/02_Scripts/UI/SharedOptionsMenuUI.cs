using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SharedOptionsMenuUI : MonoBehaviour
{
    private readonly struct RebindRowDefinition
    {
        public readonly string label;
        public readonly string koreanLabel;
        public readonly string actionMap;
        public readonly string action;
        public readonly int bindingIndex;
        public readonly string fallback;

        public RebindRowDefinition(
            string label,
            string koreanLabel,
            string actionMap,
            string action,
            int bindingIndex,
            string fallback)
        {
            this.label = label;
            this.koreanLabel = koreanLabel;
            this.actionMap = actionMap;
            this.action = action;
            this.bindingIndex = bindingIndex;
            this.fallback = fallback;
        }
    }

    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.17f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.2f, 0.85f, 0.72f, 1f);
    private static readonly Color TextColor = new Color(0.9f, 0.95f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.62f, 0.7f, 0.78f, 1f);

    private static readonly RebindRowDefinition[] RebindRows =
    {
        new RebindRowDefinition("Move Up", "이동 위", "Player", "Move", 1, "W"),
        new RebindRowDefinition("Move Down", "이동 아래", "Player", "Move", 2, "S"),
        new RebindRowDefinition("Move Left", "이동 왼쪽", "Player", "Move", 3, "A"),
        new RebindRowDefinition("Move Right", "이동 오른쪽", "Player", "Move", 4, "D"),
        new RebindRowDefinition("Fire", "사격", "Player", "Fire", 0, "LMB"),
        new RebindRowDefinition("Dash", "대시", "Player", "Dash", 0, "RMB"),
        new RebindRowDefinition("Radar Toggle", "레이더 켜기 / 끄기", "Player", "Radar", 0, "Q"),
        new RebindRowDefinition("Instant Radar Scan", "레이더 즉시 스캔", "Player", "RadarQuickScan", 0, "Mouse 4"),
        new RebindRowDefinition("Interact", "상호작용", "Player", "Interact", 0, "F"),
        new RebindRowDefinition("Inventory", "인벤토리", "Player", "Inventory", 0, "E"),
        new RebindRowDefinition("Map", "지도", "Player", "Map", 0, "Tab"),
        new RebindRowDefinition("Reinforcement", "지원 장비", "Player", "UseReinforcement", 0, "R"),
        new RebindRowDefinition("Dismantle", "해체", "Player", "Dismantle", 0, "G"),
        new RebindRowDefinition("Cancel / Menu", "취소 / 메뉴", "UI", "Cancel", 0, "Esc"),
        new RebindRowDefinition("Route Add", "경로 추가", "Map", "MapRouteAdd", 0, "LMB"),
        new RebindRowDefinition("Route Remove", "경로 제거", "Map", "MapRouteRemove", 0, "RMB"),
        new RebindRowDefinition("Route Clear", "경로 초기화", "Map", "MapRouteClear", 0, "C"),
        new RebindRowDefinition(
            "Dialogue Advance",
            "대화 진행 / 선택 확정",
            "Player",
            "DialogueAdvance",
            0,
            "Space")
    };

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer masterAudioMixer;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private Button backButton;
    [SerializeField] private Button returnToMainMenuButton;
    [SerializeField] private SettingsMenuTabController tabController;
    [SerializeField] private SettlementSettingsPanel settingsPanel;
    [SerializeField] private GameObject displayConfirmationRoot;
    [SerializeField] private bool useKoreanLabels;
    [SerializeField] private bool configureTabButtonSounds;
    [SerializeField] private bool showReturnToMainMenuAction;
    [SerializeField] private bool built;

    public bool IsOpen => settingsPanel != null && settingsPanel.IsOpen;
    public Button BackButton => backButton;
    public SettlementSettingsPanel SettingsPanel => settingsPanel;
    public SettingsMenuTabController TabController => tabController;
    public GameObject DisplayConfirmationRoot => displayConfirmationRoot;
    public bool HasAuthoredLayout =>
        built &&
        backButton != null &&
        tabController != null &&
        settingsPanel != null &&
        displayConfirmationRoot != null;

    public event Action BackRequested;
    public event Action ReturnToMainMenuRequested;

    public bool TryValidateAuthoredLayout(out string error)
    {
        var errors = new List<string>();
        CollectAuthoredBindingErrors(errors);
        error = string.Join("\n", errors);
        return errors.Count == 0;
    }

    // Boot uses this path exclusively. Missing authored bindings are configuration errors.
    public bool ConfigureAuthored(InputActionAsset actions, AudioMixer mixer, TMP_FontAsset font)
    {
        if (!TryValidateAuthoredLayout(out string error))
        {
            Debug.LogError("Authored Boot options are invalid. Repair the existing Inspector bindings; runtime Build is disabled.\n" + error, this);
            return false;
        }
        inputActions = actions;
        masterAudioMixer = mixer;
        uiFont = font;
        RefreshActionButtonBindings();
        return true;
    }

    public void CollectAuthoredBindingErrors(List<string> errors)
    {
        var roles = new HashSet<Component>();
        void Role(string field, Component value, bool sameObject = false)
        {
            if (value == null || value.gameObject.scene != gameObject.scene ||
                (sameObject ? value.transform != transform : value.transform == transform || !value.transform.IsChildOf(transform)))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SharedOptionsMenuUI." + field, value, transform,
                    gameObject.scene, value == null ? "Missing binding" : value.gameObject.scene != gameObject.scene ? "Scene ownership" : "Ancestry"));
            else if (!roles.Add(value))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SharedOptionsMenuUI." + field,
                    value, transform, gameObject.scene, "Duplicate control mapping"));
        }
        Role(nameof(backButton), backButton);
        Role(nameof(tabController), tabController, true);
        Role(nameof(settingsPanel), settingsPanel, true);
        Role(nameof(displayConfirmationRoot), displayConfirmationRoot != null ? displayConfirmationRoot.transform : null);
        if (showReturnToMainMenuAction) Role(nameof(returnToMainMenuButton), returnToMainMenuButton);
        if (!built) errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SharedOptionsMenuUI.built", this, transform, gameObject.scene, "Authored layout not installed"));
        if (settingsPanel != null) settingsPanel.CollectSharedOptionsBindingErrors(errors);
        if (tabController != null) tabController.CollectSharedOptionsBindingErrors(errors);
    }


    public void Configure(
        InputActionAsset actions,
        AudioMixer mixer,
        TMP_FontAsset font,
        bool koreanLabels = true,
        bool tabButtonSounds = true,
        bool includeReturnToMainMenuAction = false)
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(actions, this);
        masterAudioMixer = mixer;
        uiFont = font;
        useKoreanLabels = koreanLabels;
        configureTabButtonSounds = tabButtonSounds;
        showReturnToMainMenuAction = includeReturnToMainMenuAction;

        if (!built)
        {
            Build();
        }

        RefreshActionButtonBindings();
    }

    public void Open()
    {
        if (!built)
        {
            Debug.LogError("Shared Options UI must be configured before it is opened.", this);
            return;
        }

        tabController?.ShowTab(0);
        settingsPanel?.Open();
        tabController?.SelectFirstControlInCurrentTab();
    }

    public void Close()
    {
        settingsPanel?.Close();
    }

    public bool TryCancelScreenConfirmation()
    {
        return settingsPanel != null && settingsPanel.TryCancelScreenConfirmation();
    }

    public bool TryHandleNestedCancel() => (tabController != null && tabController.TryHandleMenuCancel()) || TryCancelScreenConfirmation();

    private void OnEnable()
    {
        RefreshActionButtonBindings();
    }

    private void OnDisable()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(HandleBackRequested);
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveListener(HandleReturnToMainMenuRequested);
        }
    }

    private void HandleBackRequested()
    {
        BackRequested?.Invoke();
    }

    private void HandleReturnToMainMenuRequested()
    {
        ReturnToMainMenuRequested?.Invoke();
    }

    private void RefreshActionButtonBindings()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(HandleBackRequested);

            if (isActiveAndEnabled)
            {
                backButton.onClick.AddListener(HandleBackRequested);
            }
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveListener(HandleReturnToMainMenuRequested);

            if (isActiveAndEnabled)
            {
                returnToMainMenuButton.onClick.AddListener(HandleReturnToMainMenuRequested);
            }
        }
    }

    private void Build()
    {
        ValidateRuntimeParent(transform);
        HashSet<Transform> previousChildren = new HashSet<Transform>();
        foreach (Transform child in transform)
        {
            previousChildren.Add(child);
        }
        HashSet<Component> previousComponents = new HashSet<Component>(GetComponents<Component>());
        try
        {
            BuildContents();
            built = true;
        }
        catch
        {
            // A failed attempt must not poison built or leave controls/listeners for a retry.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!previousChildren.Contains(child))
                {
                    DestroyFailedConstruction(child.gameObject);
                }
            }
            Component[] components = GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (!previousComponents.Contains(component) && !(component is Transform))
                {
                    if (component is Behaviour behaviour) behaviour.enabled = false;
                    DestroyFailedConstruction(component);
                }
            }
            backButton = null;
            returnToMainMenuButton = null;
            tabController = null;
            settingsPanel = null;
            displayConfirmationRoot = null;
            built = false;
            throw;
        }
    }

    private void BuildContents()
    {
        gameObject.name = "OptionsPanel";

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(466f, 258f);
        AddImage(gameObject, PanelColor);

        CreateText("OptionsTitle", transform, Localize("OPTIONS", "설정"), new Vector2(-184f, 112f), new Vector2(80f, 24f), 16f, TextAlignmentOptions.Left, AccentColor);

        string[] tabNames = { "Gameplay", "Sound", "Keyboard", "Display" };
        string[] tabLabels =
        {
            Localize("GAMEPLAY", "게임"),
            Localize("SOUND", "사운드"),
            Localize("KEYBOARD", "키 설정"),
            Localize("DISPLAY", "화면")
        };
        GameObject[] tabRoots = new GameObject[tabLabels.Length];
        Button[] tabButtons = new Button[tabLabels.Length];

        for (int i = 0; i < tabLabels.Length; i++)
        {
            float x = -135f + (i * 90f);
            tabButtons[i] = CreateButton(tabNames[i] + "TabButton", transform, tabLabels[i], new Vector2(x, 83f), new Vector2(82f, 24f), 9f);
            if (configureTabButtonSounds)
            {
                ConfigureCommonButtonSound(tabButtons[i]);
            }
            tabRoots[i] = CreateRectObject(tabNames[i] + "Tab", transform, new Vector2(0f, -6f), new Vector2(438f, 150f));
        }

        BuildGameplayTab(tabRoots[0].transform, out Toggle showHints, out Slider cameraShake, out Slider warningOpacity);
        BuildSoundTab(tabRoots[1].transform, out Slider master, out Slider bgm, out Slider sfx, out Slider ambient, out Slider ui);
        List<InputRebindButtonUI> rebindRows = BuildKeyboardTab(tabRoots[2].transform, out Button resetBindings);
        BuildDisplayTab(tabRoots[3].transform, out TMP_Dropdown resolution, out TMP_Dropdown fullScreen, out Toggle vSync, out TMP_Dropdown frameLimit, out Button applyDisplay);
        BuildDisplayConfirmation(out TextMeshProUGUI confirmationText, out Button keepDisplay, out Button revertDisplay);

        backButton = CreateButton("OptionsBackButton", transform, Localize("BACK", "뒤로"), new Vector2(-179f, -112f), new Vector2(88f, 24f), 9f);

        tabController = gameObject.AddComponent<SettingsMenuTabController>();
        tabController.Configure(tabRoots, tabButtons, 0);
        tabController.ConfigureInputGuards(
            rebindRows.ToArray(),
            new[] { resolution, fullScreen, frameLimit },
            new[] { displayConfirmationRoot }
        );

        settingsPanel = gameObject.AddComponent<SettlementSettingsPanel>();
        settingsPanel.ConfigurePanel(gameObject, tabController);
        settingsPanel.ConfigureAudio(masterAudioMixer, master, bgm, sfx, ambient, ui);
        settingsPanel.ConfigureGameplay(showHints, cameraShake, warningOpacity);
        settingsPanel.ConfigureControls(inputActions, resetBindings, rebindRows.ToArray());
        settingsPanel.ConfigureDisplay(resolution, fullScreen, vSync, frameLimit, applyDisplay, displayConfirmationRoot, confirmationText, keepDisplay, revertDisplay);
        // Editor authoring/PreviewScene construction must not apply display settings,
        // write PlayerPrefs, or create the runtime AudioManager. The authored panel's
        // own Awake initializes those settings when it first runs in the player.
        if (Application.isPlaying)
        {
            settingsPanel.InitializeConfiguredUi();
        }
        RefreshActionButtonBindings();
    }

    private void BuildGameplayTab(
        Transform parent,
        out Toggle showHints,
        out Slider cameraShake,
        out Slider warningOpacity)
    {
        CreateText("GameplayHelp", parent, Localize("Changes apply immediately and are stored as user preferences.", "변경 사항은 즉시 적용되고 자동 저장됩니다."), new Vector2(0f, 66f), new Vector2(410f, 16f), 7.5f, TextAlignmentOptions.Center, MutedTextColor);
        showHints = CreateToggleRow("ShowHintsToggle", parent, Localize("Show HUD control hints", "조작 힌트 표시"), 43f);
        cameraShake = CreateSliderRow("CameraShakeSlider", parent, Localize("Camera shake", "카메라 흔들림"), -9f, 0f, 1f);
        warningOpacity = CreateSliderRow("WarningOpacitySlider", parent, Localize("Warning opacity", "경고 표시 투명도"), -35f, 0.25f, 1f);

        if (showReturnToMainMenuAction)
        {
            returnToMainMenuButton = CreateButton(
                "ReturnToMainMenuButton",
                parent,
                "메인 화면으로 나가기",
                new Vector2(0f, -63f),
                new Vector2(150f, 22f),
                8f
            );
            ConfigureCommonButtonSound(returnToMainMenuButton);
        }
    }

    private void BuildSoundTab(Transform parent, out Slider master, out Slider bgm, out Slider sfx, out Slider ambient, out Slider ui)
    {
        master = CreateSliderRow("MasterSlider", parent, Localize("Master", "전체 음량"), 56f, 0f, 1f);
        bgm = CreateSliderRow("BgmSlider", parent, Localize("BGM", "배경음"), 28f, 0f, 1f);
        sfx = CreateSliderRow("SfxSlider", parent, Localize("SFX", "효과음"), 0f, 0f, 1f);
        ambient = CreateSliderRow("AmbientSlider", parent, Localize("Ambient", "환경음"), -28f, 0f, 1f);
        ui = CreateSliderRow("UiSlider", parent, Localize("UI", "UI 음량"), -56f, 0f, 1f);
    }

    private List<InputRebindButtonUI> BuildKeyboardTab(Transform parent, out Button resetBindings)
    {
        List<InputRebindButtonUI> rows = new List<InputRebindButtonUI>(RebindRows.Length);
        CreateText("KeyboardHelp", parent, Localize("Select a binding to rebind. Move uses separate composite parts.", "항목을 선택해 키를 변경합니다. 이동은 방향별로 설정됩니다."), new Vector2(-50f, 68f), new Vector2(320f, 16f), 7.5f, TextAlignmentOptions.Left, MutedTextColor);
        resetBindings = CreateButton("ResetBindingsButton", parent, Localize("RESET BINDINGS", "키 설정 초기화"), new Vector2(162f, 67f), new Vector2(84f, 20f), 7f);

        for (int i = 0; i < RebindRows.Length; i++)
        {
            RebindRowDefinition definition = RebindRows[i];
            string displayLabel = useKoreanLabels ? definition.koreanLabel : definition.label;
            int rowsPerColumn = (RebindRows.Length + 1) / 2;
            int column = i < rowsPerColumn ? 0 : 1;
            int rowIndex = column == 0 ? i : i - rowsPerColumn;
            float columnCenter = column == 0 ? -108f : 108f;
            float y = 48f - (rowIndex * 17f);
            CreateText(definition.label + "Label", parent, displayLabel, new Vector2(columnCenter - 49f, y), new Vector2(98f, 16f), 7.5f, TextAlignmentOptions.Left, TextColor);
            Button rebindButton = CreateButton(definition.label.Replace(" ", string.Empty) + "BindingButton", parent, definition.fallback, new Vector2(columnCenter + 58f, y), new Vector2(82f, 16f), 7.5f);
            TextMeshProUGUI bindingText = rebindButton.GetComponentInChildren<TextMeshProUGUI>();
            InputRebindButtonUI row = rebindButton.gameObject.AddComponent<InputRebindButtonUI>();
            row.Configure(inputActions, definition.actionMap, definition.action, "Keyboard&Mouse", definition.bindingIndex, rebindButton, null, bindingText, definition.fallback, Localize("PRESS KEY", "입력 대기..."));
            rows.Add(row);
        }

        return rows;
    }

    private void BuildDisplayTab(Transform parent, out TMP_Dropdown resolution, out TMP_Dropdown fullScreen, out Toggle vSync, out TMP_Dropdown frameLimit, out Button apply)
    {
        CreateText("ResolutionLabel", parent, Localize("Resolution", "해상도"), new Vector2(-108f, 58f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        resolution = CreateDropdown("ResolutionDropdown", parent, new Vector2(-108f, 35f), new Vector2(180f, 24f));
        CreateText("FullscreenLabel", parent, Localize("Window mode", "화면 모드"), new Vector2(108f, 58f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        fullScreen = CreateDropdown("FullscreenDropdown", parent, new Vector2(108f, 35f), new Vector2(180f, 24f));
        vSync = CreateToggleRow("VSyncToggle", parent, Localize("VSync", "수직 동기화"), -18f, -108f);
        CreateText("FrameLimitLabel", parent, Localize("Frame limit", "프레임 제한"), new Vector2(108f, 3f), new Vector2(180f, 18f), 9f, TextAlignmentOptions.Center, TextColor);
        frameLimit = CreateDropdown("FrameLimitDropdown", parent, new Vector2(108f, -20f), new Vector2(180f, 24f));
        apply = CreateButton("ApplyDisplayButton", parent, Localize("APPLY", "적용"), new Vector2(0f, -63f), new Vector2(130f, 24f), 9f);
    }

    private void BuildDisplayConfirmation(out TextMeshProUGUI confirmationText, out Button keep, out Button revert)
    {
        displayConfirmationRoot = CreateStretchObject("DisplayConfirmation", transform);
        AddImage(displayConfirmationRoot, new Color(0f, 0f, 0f, 0.78f));
        GameObject panel = CreatePanel("ConfirmationPanel", displayConfirmationRoot.transform, Vector2.zero, new Vector2(310f, 118f), PanelColor);
        confirmationText = CreateText("ConfirmationText", panel.transform, Localize("Keep these display settings?", "이 화면 설정을 유지하시겠습니까?"), new Vector2(0f, 24f), new Vector2(270f, 48f), 11f, TextAlignmentOptions.Center, TextColor);
        keep = CreateButton("KeepDisplayButton", panel.transform, Localize("CONFIRM", "확인"), new Vector2(-62f, -34f), new Vector2(100f, 24f), 9f);
        revert = CreateButton("RevertDisplayButton", panel.transform, Localize("REVERT", "되돌리기"), new Vector2(62f, -34f), new Vector2(100f, 24f), 9f);
        displayConfirmationRoot.SetActive(false);
    }

    private GameObject CreatePanel(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = CreateRectObject(objectName, parent, position, size);
        AddImage(panel, color);
        return panel;
    }

    private GameObject CreateRectObject(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject target = CreateChild(objectName, parent);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return target;
    }

    private GameObject CreateStretchObject(string objectName, Transform parent)
    {
        GameObject target = CreateChild(objectName, parent);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return target;
    }

    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        return image;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject target = CreateRectObject(objectName, parent, position, size);
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        ApplyUiFont(text);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size, float fontSize = 11f)
    {
        GameObject target = CreateRectObject(objectName, parent, position, size);
        Image image = AddImage(target, ButtonColor);
        Button button = target.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.82f, 1f, 0.95f, 1f);
        colors.pressedColor = new Color(0.65f, 0.85f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.38f, 0.42f, 0.46f, 0.6f);
        button.colors = colors;
        GameObject labelObject = CreateStretchObject("Label", target.transform);
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

    private static void ConfigureCommonButtonSound(Button button)
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
        soundButton.SetClickSoundEventId(SoundEventIds.UiClick);
        soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
        soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
    }

    private string Localize(string english, string korean)
    {
        return useKoreanLabels ? korean : english;
    }

    private Slider CreateSliderRow(string objectName, Transform parent, string label, float y, float minimum, float maximum)
    {
        CreateText(objectName + "Label", parent, label, new Vector2(-145f, y), new Vector2(110f, 20f), 9f, TextAlignmentOptions.Left, TextColor);
        GameObject sliderObject = CreateRectObject(objectName, parent, new Vector2(55f, y), new Vector2(260f, 16f));
        Image hitArea = AddImage(sliderObject, new Color(0f, 0f, 0f, 0.001f));
        GameObject backgroundObject = CreateRectObject(
            "Background",
            sliderObject.transform,
            Vector2.zero,
            new Vector2(248f, 5f)
        );
        Image background = AddImage(backgroundObject, new Color(0.08f, 0.1f, 0.14f, 1f));
        GameObject fillArea = CreateStretchObject("Fill Area", sliderObject.transform);
        fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 6f);
        fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, -6f);
        GameObject fill = CreateStretchObject("Fill", fillArea.transform);
        Image fillImage = AddImage(fill, AccentColor);
        GameObject handleArea = CreateStretchObject("Handle Slide Area", sliderObject.transform);
        handleArea.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 0f);
        handleArea.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, 0f);
        GameObject handle = CreateRectObject("Handle", handleArea.transform, Vector2.zero, new Vector2(8f, 14f));
        Image handleImage = AddImage(handle, TextColor);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = maximum;
        hitArea.raycastTarget = true;
        background.raycastTarget = false;
        fillImage.raycastTarget = false;
        return slider;
    }

    private Toggle CreateToggleRow(string objectName, Transform parent, string label, float y, float x = 0f)
    {
        GameObject target = CreateRectObject(objectName, parent, new Vector2(x - 90f, y), new Vector2(18f, 18f));
        Image background = AddImage(target, new Color(0.08f, 0.1f, 0.14f, 1f));
        GameObject checkObject = CreateStretchObject("Checkmark", target.transform);
        checkObject.GetComponent<RectTransform>().offsetMin = new Vector2(3f, 3f);
        checkObject.GetComponent<RectTransform>().offsetMax = new Vector2(-3f, -3f);
        Image checkmark = AddImage(checkObject, AccentColor);
        Toggle toggle = target.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        CreateText(objectName + "Label", parent, label, new Vector2(x + 31f, y), new Vector2(210f, 20f), 9f, TextAlignmentOptions.Left, TextColor);
        return toggle;
    }

    private TMP_Dropdown CreateDropdown(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        ValidateRuntimeParent(parent);
        // TMP_DefaultControls in Unity 6000.0.69f1 creates in the active scene.
        GameObject target = TMP_DefaultControls.CreateDropdown(default);
        target.name = objectName;
        try
        {
            {
                ParentRuntimeRoot(target, parent);
            }
        }
        catch
        {
            DestroyFailedConstruction(target);
            throw;
        }
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Dropdown dropdown = target.GetComponent<TMP_Dropdown>();
        ConfigureDropdownPresentation(target);
        ApplyUiFont(dropdown.captionText);
        ApplyUiFont(dropdown.itemText);
        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 9f;
            dropdown.captionText.color = Color.white;
        }
        if (dropdown.itemText != null)
        {
            dropdown.itemText.fontSize = 9f;
            dropdown.itemText.color = Color.white;
        }

        TextMeshProUGUI[] dropdownTexts = target.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < dropdownTexts.Length; i++)
        {
            ApplyUiFont(dropdownTexts[i]);
            dropdownTexts[i].color = Color.white;
        }
        return dropdown;
    }

    private void ConfigureDropdownPresentation(GameObject dropdownObject)
    {
        ConfigureSolidImage(dropdownObject.GetComponent<Image>(), ButtonColor);
        Transform template = FindChildRecursive(dropdownObject.transform, "Template");
        if (template != null) ConfigureSolidImage(template.GetComponent<Image>(), PanelColor);
        Transform viewport = FindChildRecursive(dropdownObject.transform, "Viewport");
        if (viewport != null)
        {
            Mask mask = viewport.GetComponent<Mask>();
            if (mask != null) mask.enabled = false;
            Image image = viewport.GetComponent<Image>();
            if (image != null) image.enabled = false;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        }

        Transform arrow = FindChildRecursive(dropdownObject.transform, "Arrow");
        if (arrow != null)
        {
            Image image = arrow.GetComponent<Image>();
            if (image != null) image.enabled = false;
            Transform glyph = arrow.Find("Glyph") ?? CreateStretchObject("Glyph", arrow).transform;
            TextMeshProUGUI text = glyph.GetComponent<TextMeshProUGUI>();
            if (text == null) text = glyph.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "v";
            ApplyUiFont(text);
            text.fontSize = 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TextColor;
            text.raycastTarget = false;
        }

        Transform scrollbar = FindChildRecursive(dropdownObject.transform, "Scrollbar");
        if (scrollbar != null)
        {
            ConfigureSolidImage(scrollbar.GetComponent<Image>(), new Color(0.06f, 0.08f, 0.11f, 1f));
            Transform handle = FindChildRecursive(scrollbar, "Handle");
            if (handle != null) ConfigureSolidImage(handle.GetComponent<Image>(), AccentColor);
        }

        Transform itemBackground = FindChildRecursive(dropdownObject.transform, "Item Background");
        if (itemBackground != null) ConfigureSolidImage(itemBackground.GetComponent<Image>(), ButtonColor);
        Transform itemCheckmark = FindChildRecursive(dropdownObject.transform, "Item Checkmark");
        if (itemCheckmark != null) ConfigureSolidImage(itemCheckmark.GetComponent<Image>(), AccentColor);
    }


    private void ApplyUiFont(TMP_Text text)
    {
        if (text != null && uiFont != null)
        {
            text.font = uiFont;
        }
    }

    private GameObject CreateChild(string objectName, Transform parent)
    {
        ValidateRuntimeParent(parent);
        if (parent.gameObject.scene != gameObject.scene || !parent.IsChildOf(transform))
        {
            throw new InvalidOperationException($"Options child '{objectName}' must belong to its OptionsPanel root.");
        }
        return CreateRuntimeChild(objectName, parent);
    }

    // Local construction shared by the runtime Pause owner and its Options presentation.
    // UNITY_EDITOR selects an available scene-targeted API, not an authoring policy.
    internal static GameObject CreateRuntimeChild(string objectName, Transform parent)
    {
        ValidateRuntimeParent(parent);
#if UNITY_EDITOR
        GameObject target = UnityEditor.ObjectFactory.CreateGameObject(
            parent.gameObject.scene, HideFlags.None, objectName, typeof(RectTransform));
#else
        GameObject target = new GameObject(objectName, typeof(RectTransform));
#endif
        try
        {
            ParentRuntimeRoot(target, parent);
            return target;
        }
        catch
        {
            DestroyFailedConstruction(target);
            throw;
        }
    }

    private static void ParentRuntimeRoot(GameObject target, Transform parent)
    {
        ValidateRuntimeParent(parent);
        if (target == null || target.transform.parent != null)
        {
            throw new InvalidOperationException("Only a newly created root can join the runtime Options hierarchy.");
        }
        if (target.scene != parent.gameObject.scene)
        {
            SceneManager.MoveGameObjectToScene(target, parent.gameObject.scene);
        }
        target.transform.SetParent(parent, false);
        if (target.scene != parent.gameObject.scene)
        {
            throw new InvalidOperationException("Runtime Options child failed to acquire its parent's scene ownership.");
        }
    }

    internal static void ValidateRuntimeParent(Transform parent)
    {
        if (parent == null || !parent.gameObject.scene.IsValid() ||
            parent.root.gameObject.scene != parent.gameObject.scene)
        {
            throw new InvalidOperationException("Runtime Options require a live parent and root in the same valid scene.");
        }
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.IsPersistent(parent))
        {
            throw new InvalidOperationException("Runtime Options cannot be built into a persistent asset.");
        }
#endif
    }

    internal static void DestroyFailedConstruction(UnityEngine.Object target)
    {
        if (target is GameObject root)
        {
            root.SetActive(false);
            root.transform.SetParent(null, false);
        }
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void ConfigureSolidImage(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName) return child;
            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }

        return null;
    }
}
