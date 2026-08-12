using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettlementSettingsPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private SettingsMenuTabController tabController;

    [Header("Audio Mixer Optional")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string bgmVolumeParameter = "BGMVolume";
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";
    [SerializeField] private string ambientVolumeParameter = "AmbientVolume";
    [SerializeField] private string uiVolumeParameter = "UIVolume";

    [Header("Sound - Immediate Apply")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider ambientVolumeSlider;
    [SerializeField] private Slider uiVolumeSlider;

    [Header("Gameplay - Immediate Apply")]
    [SerializeField] private Toggle showHudHintsToggle;

    [Header("Accessibility - Immediate Apply")]
    [SerializeField] private Slider uiScaleSlider;
    [SerializeField] private Slider textScaleSlider;
    [SerializeField] private Slider cameraShakeSlider;
    [SerializeField] private Slider warningOpacitySlider;

    [Header("Controls")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Button resetAllBindingsButton;
    [SerializeField] private InputRebindButtonUI[] rebindRows;
    [SerializeField] private InputBindingLabel[] bindingLabels;

    [Header("Screen - Apply Button")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown fullScreenModeDropdown;
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Dropdown frameLimitDropdown;
    [SerializeField] private Button applyScreenButton;

    [Header("Screen Confirmation Optional")]
    [SerializeField] private GameObject screenConfirmRoot;
    [SerializeField] private TextMeshProUGUI screenConfirmCountdownText;
    [SerializeField] private Button confirmScreenButton;
    [SerializeField] private Button revertScreenButton;
    [SerializeField, Min(2f)] private float screenConfirmSeconds = 10f;

    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    private readonly int[] frameLimitValues = { 30, 60, 120, 144, -1 };

    private bool initialized;
    private bool applyingSavedValues;
    private Coroutine confirmationRoutine;

    private int pendingResolutionIndex;
    private FullScreenMode pendingFullScreenMode;
    private int pendingVSyncCount;
    private int pendingFrameLimit;

    private int previousWidth;
    private int previousHeight;
    private FullScreenMode previousFullScreenMode;
    private int previousVSyncCount;
    private int previousFrameLimit;

    private const string MasterVolumeKey = "settings_master_volume";
    private const string BgmVolumeKey = "settings_bgm_volume";
    private const string SfxVolumeKey = "settings_sfx_volume";
    private const string AmbientVolumeKey = "settings_ambient_volume";
    private const string UiVolumeKey = "settings_ui_volume";
    private const string FullScreenModeKey = "settings_fullscreen_mode";
    private const string LegacyFullScreenKey = "settings_fullscreen";
    private const string ResolutionWidthKey = "settings_resolution_width";
    private const string ResolutionHeightKey = "settings_resolution_height";
    private const string LegacyResolutionIndexKey = "settings_resolution_index";
    private const string VSyncKey = "settings_vsync";
    private const string FrameLimitKey = "settings_frame_limit";

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        InitializeSettings();
        ConfigureButtonSounds();
        SetConfirmVisible(false);
        Close();
    }

    private void OnEnable()
    {
        BindButtonsAndControls();
    }

    private void OnDisable()
    {
        UnbindButtonsAndControls();
        StopConfirmationRoutine();
    }

    public void Open()
    {
        InitializeSettings();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshAllBindingLabels();
    }

    public void Close()
    {
        if (screenConfirmRoot != null && screenConfirmRoot.activeSelf)
        {
            RevertScreenSettings();
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void SetMasterVolume(float value)
    {
        ApplyAudioVolume(value, MasterVolumeKey, masterVolumeParameter, true);
    }

    public void SetBgmVolume(float value)
    {
        ApplyAudioVolume(value, BgmVolumeKey, bgmVolumeParameter, false);
    }

    public void SetSfxVolume(float value)
    {
        ApplyAudioVolume(value, SfxVolumeKey, sfxVolumeParameter, false);
    }

    public void SetAmbientVolume(float value)
    {
        ApplyAudioVolume(value, AmbientVolumeKey, ambientVolumeParameter, false);
    }

    public void SetUiVolume(float value)
    {
        ApplyAudioVolume(value, UiVolumeKey, uiVolumeParameter, false);
    }

    public void SetShowHudHints(bool value)
    {
        if (!applyingSavedValues)
        {
            GameSettingsRuntime.SetShowHudKeyHints(value);
        }
    }

    public void SetUiScale(float value)
    {
        if (!applyingSavedValues)
        {
            GameSettingsRuntime.SetUiScale(value);
        }
    }

    public void SetTextScale(float value)
    {
        if (!applyingSavedValues)
        {
            GameSettingsRuntime.SetTextScale(value);
        }
    }

    public void SetCameraShake(float value)
    {
        if (!applyingSavedValues)
        {
            GameSettingsRuntime.SetCameraShakeMultiplier(value);
        }
    }

    public void SetWarningOpacity(float value)
    {
        if (!applyingSavedValues)
        {
            GameSettingsRuntime.SetWarningOpacity(value);
        }
    }

    public void SetPendingResolutionByIndex(int index)
    {
        if (availableResolutions.Count == 0)
        {
            return;
        }

        pendingResolutionIndex = Mathf.Clamp(index, 0, availableResolutions.Count - 1);
    }

    public void SetPendingFullScreenModeByIndex(int index)
    {
        pendingFullScreenMode = IndexToFullScreenMode(index);

        if (fullScreenToggle != null)
        {
            fullScreenToggle.SetIsOnWithoutNotify(pendingFullScreenMode != FullScreenMode.Windowed);
        }
    }

    public void SetPendingFullScreen(bool fullScreen)
    {
        pendingFullScreenMode = fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (fullScreenModeDropdown != null)
        {
            fullScreenModeDropdown.SetValueWithoutNotify(FullScreenModeToIndex(pendingFullScreenMode));
            fullScreenModeDropdown.RefreshShownValue();
        }
    }

    public void SetPendingVSync(bool enabled)
    {
        pendingVSyncCount = enabled ? 1 : 0;
    }

    public void SetPendingFrameLimitByIndex(int index)
    {
        if (frameLimitValues.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, frameLimitValues.Length - 1);
        pendingFrameLimit = frameLimitValues[index];
    }

    public void ApplyScreenSettings()
    {
        if (availableResolutions.Count == 0)
        {
            return;
        }

        previousWidth = Screen.width;
        previousHeight = Screen.height;
        previousFullScreenMode = Screen.fullScreenMode;
        previousVSyncCount = QualitySettings.vSyncCount;
        previousFrameLimit = Application.targetFrameRate;

        Resolution resolution = availableResolutions[Mathf.Clamp(pendingResolutionIndex, 0, availableResolutions.Count - 1)];
        Screen.SetResolution(resolution.width, resolution.height, pendingFullScreenMode);
        QualitySettings.vSyncCount = Mathf.Clamp(pendingVSyncCount, 0, 1);
        Application.targetFrameRate = pendingFrameLimit;

        if (screenConfirmRoot == null)
        {
            ConfirmScreenSettings();
            return;
        }

        SetConfirmVisible(true);
        StopConfirmationRoutine();
        confirmationRoutine = StartCoroutine(ScreenConfirmationRoutine());
    }

    public void ConfirmScreenSettings()
    {
        StopConfirmationRoutine();
        SetConfirmVisible(false);

        PlayerPrefs.SetInt(ResolutionWidthKey, Screen.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, Screen.height);
        PlayerPrefs.SetInt(FullScreenModeKey, (int)Screen.fullScreenMode);
        PlayerPrefs.SetInt(LegacyFullScreenKey, Screen.fullScreen ? 1 : 0);
        PlayerPrefs.SetInt(VSyncKey, QualitySettings.vSyncCount);
        PlayerPrefs.SetInt(FrameLimitKey, Application.targetFrameRate);
        PlayerPrefs.SetInt(LegacyResolutionIndexKey, FindCurrentResolutionIndex());
        PlayerPrefs.Save();

        pendingResolutionIndex = FindCurrentResolutionIndex();
        pendingFullScreenMode = Screen.fullScreenMode;
        pendingVSyncCount = QualitySettings.vSyncCount;
        pendingFrameLimit = Application.targetFrameRate;
        RefreshScreenControlsWithoutNotify();
    }

    public void RevertScreenSettings()
    {
        StopConfirmationRoutine();
        Screen.SetResolution(previousWidth, previousHeight, previousFullScreenMode);
        QualitySettings.vSyncCount = previousVSyncCount;
        Application.targetFrameRate = previousFrameLimit;
        SetConfirmVisible(false);

        pendingResolutionIndex = FindResolutionIndex(previousWidth, previousHeight);
        pendingFullScreenMode = previousFullScreenMode;
        pendingVSyncCount = previousVSyncCount;
        pendingFrameLimit = previousFrameLimit;
        RefreshScreenControlsWithoutNotify();
    }

    public void ResetAllBindings()
    {
        InputBindingPersistence.ResetAll(inputActions);
        RefreshAllBindingLabels();
        AudioManager.Play(SoundEventIds.UiActivate);
    }

    public void QuitGame()
    {
        if (SaveManager.Instance != null && PermanentProgress.Instance != null)
        {
            SaveManager.Instance.Save(PermanentProgress.Instance);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void InitializeSettings()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        applyingSavedValues = true;

        InputBindingPersistence.LoadOnce(inputActions);
        BuildResolutionOptions();
        BuildFullScreenModeOptions();
        BuildFrameLimitOptions();

        float master = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        float bgm = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        float sfx = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        float ambient = PlayerPrefs.GetFloat(AmbientVolumeKey, 1f);
        float ui = PlayerPrefs.GetFloat(UiVolumeKey, 1f);

        SetSliderWithoutNotify(masterVolumeSlider, master);
        SetSliderWithoutNotify(bgmVolumeSlider, bgm);
        SetSliderWithoutNotify(sfxVolumeSlider, sfx);
        SetSliderWithoutNotify(ambientVolumeSlider, ambient);
        SetSliderWithoutNotify(uiVolumeSlider, ui);

        if (showHudHintsToggle != null)
        {
            showHudHintsToggle.SetIsOnWithoutNotify(GameSettingsRuntime.ShowHudKeyHints);
        }

        SetSliderWithoutNotify(uiScaleSlider, GameSettingsRuntime.UiScale);
        SetSliderWithoutNotify(textScaleSlider, GameSettingsRuntime.TextScale);
        SetSliderWithoutNotify(cameraShakeSlider, GameSettingsRuntime.CameraShakeMultiplier);
        SetSliderWithoutNotify(warningOpacitySlider, GameSettingsRuntime.WarningOpacity);

        ApplyAudioVolume(master, MasterVolumeKey, masterVolumeParameter, true);
        ApplyAudioVolume(bgm, BgmVolumeKey, bgmVolumeParameter, false);
        ApplyAudioVolume(sfx, SfxVolumeKey, sfxVolumeParameter, false);
        ApplyAudioVolume(ambient, AmbientVolumeKey, ambientVolumeParameter, false);
        ApplyAudioVolume(ui, UiVolumeKey, uiVolumeParameter, false);

        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
        int legacyIndex = PlayerPrefs.GetInt(LegacyResolutionIndexKey, FindCurrentResolutionIndex());
        pendingResolutionIndex = FindResolutionIndex(savedWidth, savedHeight);

        if (pendingResolutionIndex < 0)
        {
            pendingResolutionIndex = Mathf.Clamp(legacyIndex, 0, availableResolutions.Count - 1);
        }

        int fallbackMode = PlayerPrefs.GetInt(LegacyFullScreenKey, Screen.fullScreen ? 1 : 0) == 1
            ? (int)FullScreenMode.FullScreenWindow
            : (int)FullScreenMode.Windowed;
        pendingFullScreenMode = (FullScreenMode)PlayerPrefs.GetInt(FullScreenModeKey, fallbackMode);
        pendingVSyncCount = Mathf.Clamp(PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount), 0, 1);
        pendingFrameLimit = PlayerPrefs.GetInt(FrameLimitKey, Application.targetFrameRate == 0 ? -1 : Application.targetFrameRate);

        if (availableResolutions.Count > 0)
        {
            Resolution savedResolution = availableResolutions[Mathf.Clamp(pendingResolutionIndex, 0, availableResolutions.Count - 1)];
            Screen.SetResolution(savedResolution.width, savedResolution.height, pendingFullScreenMode);
        }

        QualitySettings.vSyncCount = pendingVSyncCount;
        Application.targetFrameRate = pendingFrameLimit;
        RefreshScreenControlsWithoutNotify();
        applyingSavedValues = false;
    }

    private void BindButtonsAndControls()
    {
        openButton?.onClick.AddListener(Open);
        closeButton?.onClick.AddListener(Close);
        backButton?.onClick.AddListener(Close);
        resetAllBindingsButton?.onClick.AddListener(ResetAllBindings);
        applyScreenButton?.onClick.AddListener(ApplyScreenSettings);
        confirmScreenButton?.onClick.AddListener(ConfirmScreenSettings);
        revertScreenButton?.onClick.AddListener(RevertScreenSettings);

        masterVolumeSlider?.onValueChanged.AddListener(SetMasterVolume);
        bgmVolumeSlider?.onValueChanged.AddListener(SetBgmVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(SetSfxVolume);
        ambientVolumeSlider?.onValueChanged.AddListener(SetAmbientVolume);
        uiVolumeSlider?.onValueChanged.AddListener(SetUiVolume);

        showHudHintsToggle?.onValueChanged.AddListener(SetShowHudHints);
        uiScaleSlider?.onValueChanged.AddListener(SetUiScale);
        textScaleSlider?.onValueChanged.AddListener(SetTextScale);
        cameraShakeSlider?.onValueChanged.AddListener(SetCameraShake);
        warningOpacitySlider?.onValueChanged.AddListener(SetWarningOpacity);

        resolutionDropdown?.onValueChanged.AddListener(SetPendingResolutionByIndex);
        fullScreenModeDropdown?.onValueChanged.AddListener(SetPendingFullScreenModeByIndex);
        fullScreenToggle?.onValueChanged.AddListener(SetPendingFullScreen);
        vSyncToggle?.onValueChanged.AddListener(SetPendingVSync);
        frameLimitDropdown?.onValueChanged.AddListener(SetPendingFrameLimitByIndex);
    }

    private void UnbindButtonsAndControls()
    {
        openButton?.onClick.RemoveListener(Open);
        closeButton?.onClick.RemoveListener(Close);
        backButton?.onClick.RemoveListener(Close);
        resetAllBindingsButton?.onClick.RemoveListener(ResetAllBindings);
        applyScreenButton?.onClick.RemoveListener(ApplyScreenSettings);
        confirmScreenButton?.onClick.RemoveListener(ConfirmScreenSettings);
        revertScreenButton?.onClick.RemoveListener(RevertScreenSettings);

        masterVolumeSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        bgmVolumeSlider?.onValueChanged.RemoveListener(SetBgmVolume);
        sfxVolumeSlider?.onValueChanged.RemoveListener(SetSfxVolume);
        ambientVolumeSlider?.onValueChanged.RemoveListener(SetAmbientVolume);
        uiVolumeSlider?.onValueChanged.RemoveListener(SetUiVolume);

        showHudHintsToggle?.onValueChanged.RemoveListener(SetShowHudHints);
        uiScaleSlider?.onValueChanged.RemoveListener(SetUiScale);
        textScaleSlider?.onValueChanged.RemoveListener(SetTextScale);
        cameraShakeSlider?.onValueChanged.RemoveListener(SetCameraShake);
        warningOpacitySlider?.onValueChanged.RemoveListener(SetWarningOpacity);

        resolutionDropdown?.onValueChanged.RemoveListener(SetPendingResolutionByIndex);
        fullScreenModeDropdown?.onValueChanged.RemoveListener(SetPendingFullScreenModeByIndex);
        fullScreenToggle?.onValueChanged.RemoveListener(SetPendingFullScreen);
        vSyncToggle?.onValueChanged.RemoveListener(SetPendingVSync);
        frameLimitDropdown?.onValueChanged.RemoveListener(SetPendingFrameLimitByIndex);
    }

    private void ApplyAudioVolume(float value, string key, string parameter, bool updateListener)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(key, value);

        if (updateListener)
        {
            AudioListener.volume = value;
        }

        SetMixerVolume(parameter, value);
        PlayerPrefs.Save();
    }

    private IEnumerator ScreenConfirmationRoutine()
    {
        float remaining = Mathf.Max(2f, screenConfirmSeconds);

        while (remaining > 0f)
        {
            if (screenConfirmCountdownText != null)
            {
                screenConfirmCountdownText.text = $"이 화면 설정을 유지하시겠습니까?\n{Mathf.CeilToInt(remaining)}초 후 이전 설정으로 복구됩니다.";
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        confirmationRoutine = null;
        RevertScreenSettings();
    }

    private void StopConfirmationRoutine()
    {
        if (confirmationRoutine != null)
        {
            StopCoroutine(confirmationRoutine);
            confirmationRoutine = null;
        }
    }

    private void SetConfirmVisible(bool visible)
    {
        if (screenConfirmRoot != null)
        {
            screenConfirmRoot.SetActive(visible);
        }
    }

    private void BuildResolutionOptions()
    {
        availableResolutions.Clear();

        foreach (Resolution resolution in Screen.resolutions)
        {
            if (FindResolutionIndex(resolution.width, resolution.height) < 0)
            {
                availableResolutions.Add(resolution);
            }
        }

        if (availableResolutions.Count == 0)
        {
            availableResolutions.Add(Screen.currentResolution);
        }

        if (resolutionDropdown == null)
        {
            return;
        }

        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>(availableResolutions.Count);

        for (int i = 0; i < availableResolutions.Count; i++)
        {
            Resolution resolution = availableResolutions[i];
            options.Add($"{resolution.width} x {resolution.height}");
        }

        resolutionDropdown.AddOptions(options);
    }

    private void BuildFullScreenModeOptions()
    {
        if (fullScreenModeDropdown == null)
        {
            return;
        }

        fullScreenModeDropdown.ClearOptions();
        fullScreenModeDropdown.AddOptions(new List<string>
        {
            "창 모드",
            "전체 창모드",
            "독점 전체화면"
        });
    }

    private void BuildFrameLimitOptions()
    {
        if (frameLimitDropdown == null)
        {
            return;
        }

        frameLimitDropdown.ClearOptions();
        frameLimitDropdown.AddOptions(new List<string>
        {
            "30",
            "60",
            "120",
            "144",
            "무제한"
        });
    }

    private void RefreshScreenControlsWithoutNotify()
    {
        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            pendingResolutionIndex = Mathf.Clamp(pendingResolutionIndex, 0, availableResolutions.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(pendingResolutionIndex);
            resolutionDropdown.RefreshShownValue();
        }

        if (fullScreenModeDropdown != null)
        {
            fullScreenModeDropdown.SetValueWithoutNotify(FullScreenModeToIndex(pendingFullScreenMode));
            fullScreenModeDropdown.RefreshShownValue();
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.SetIsOnWithoutNotify(pendingFullScreenMode != FullScreenMode.Windowed);
        }

        vSyncToggle?.SetIsOnWithoutNotify(pendingVSyncCount > 0);

        if (frameLimitDropdown != null)
        {
            frameLimitDropdown.SetValueWithoutNotify(FindFrameLimitIndex(pendingFrameLimit));
            frameLimitDropdown.RefreshShownValue();
        }
    }

    private void RefreshAllBindingLabels()
    {
        if (rebindRows == null || rebindRows.Length == 0)
        {
            rebindRows = GetComponentsInChildren<InputRebindButtonUI>(true);
        }

        if (bindingLabels == null || bindingLabels.Length == 0)
        {
            bindingLabels = GetComponentsInChildren<InputBindingLabel>(true);
        }

        if (rebindRows != null)
        {
            for (int i = 0; i < rebindRows.Length; i++)
            {
                rebindRows[i]?.RefreshLabel();
            }
        }

        if (bindingLabels != null)
        {
            for (int i = 0; i < bindingLabels.Length; i++)
            {
                bindingLabels[i]?.Refresh();
            }
        }
    }

    private void ConfigureButtonSounds()
    {
        ConfigureButtonSound(openButton, SoundEventIds.UiSettings);
        ConfigureButtonSound(closeButton, SoundEventIds.UiBack);
        ConfigureButtonSound(backButton, SoundEventIds.UiBack);
        ConfigureButtonSound(applyScreenButton, SoundEventIds.UiActivate);
        ConfigureButtonSound(confirmScreenButton, SoundEventIds.UiActivate);
        ConfigureButtonSound(revertScreenButton, SoundEventIds.UiBack);
    }

    private static void ConfigureButtonSound(Button targetButton, string clickEventId)
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

        soundButton.SetClickSoundEnabled(true);
        soundButton.SetHoverSoundEnabled(true);
        soundButton.SetDisabledClickSoundEnabled(true);
        soundButton.SetClickSoundEventId(clickEventId);
        soundButton.SetHoverSoundEventId(SoundEventIds.UiHover);
        soundButton.SetDisabledClickSoundEventId(SoundEventIds.UiDisabled);
    }

    private int FindCurrentResolutionIndex()
    {
        int index = FindResolutionIndex(Screen.width, Screen.height);
        return index >= 0 ? index : Mathf.Max(0, availableResolutions.Count - 1);
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            if (availableResolutions[i].width == width && availableResolutions[i].height == height)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindFrameLimitIndex(int frameLimit)
    {
        for (int i = 0; i < frameLimitValues.Length; i++)
        {
            if (frameLimitValues[i] == frameLimit)
            {
                return i;
            }
        }

        return frameLimit < 0 ? frameLimitValues.Length - 1 : 1;
    }

    private static FullScreenMode IndexToFullScreenMode(int index)
    {
        return index switch
        {
            0 => FullScreenMode.Windowed,
            2 => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.FullScreenWindow
        };
    }

    private static int FullScreenModeToIndex(FullScreenMode mode)
    {
        return mode switch
        {
            FullScreenMode.Windowed => 0,
            FullScreenMode.ExclusiveFullScreen => 2,
            _ => 1
        };
    }

    private static void SetSliderWithoutNotify(Slider slider, float value)
    {
        slider?.SetValueWithoutNotify(value);
    }

    private void SetMixerVolume(string parameterName, float normalizedVolume)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        float db = normalizedVolume <= 0.0001f ? -80f : Mathf.Log10(normalizedVolume) * 20f;
        audioMixer.SetFloat(parameterName, db);
    }
}
