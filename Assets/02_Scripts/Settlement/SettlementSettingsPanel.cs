using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettlementSettingsPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;

    [Header("Audio Mixer Optional")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string bgmVolumeParameter = "BGMVolume";
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";

    [Header("Sound")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Screen")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullScreenToggle;

    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    private bool initialized;

    private const string MasterVolumeKey = "settings_master_volume";
    private const string BgmVolumeKey = "settings_bgm_volume";
    private const string SfxVolumeKey = "settings_sfx_volume";
    private const string FullScreenKey = "settings_fullscreen";
    private const string ResolutionIndexKey = "settings_resolution_index";

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        InitializeSettings();
        ConfigureButtonSounds();
        Close();
    }

    private void OnEnable()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(Close);
        }


        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(SetResolutionByIndex);
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
        }
    }

    private void OnDisable()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Close);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(SetResolutionByIndex);
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.onValueChanged.RemoveListener(SetFullScreen);
        }
    }

    public void Open()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    public void Close()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
        AudioListener.volume = value;
        SetMixerVolume(masterVolumeParameter, value);
        PlayerPrefs.Save();
    }

    public void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, value);
        SetMixerVolume(bgmVolumeParameter, value);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
        SetMixerVolume(sfxVolumeParameter, value);
        PlayerPrefs.Save();
    }

    public void SetFullScreen(bool fullScreen)
    {
        Screen.fullScreen = fullScreen;
        PlayerPrefs.SetInt(FullScreenKey, fullScreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResolutionByIndex(int index)
    {
        if (availableResolutions.Count == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, availableResolutions.Count - 1);
        Resolution resolution = availableResolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt(ResolutionIndexKey, index);
        PlayerPrefs.Save();
    }

    private void ConfigureButtonSounds()
    {
        ConfigureButtonSound(openButton, SoundEventIds.UiSettings);
        ConfigureButtonSound(closeButton, SoundEventIds.UiBack);
        ConfigureButtonSound(backButton, SoundEventIds.UiBack);
    }

    private void ConfigureButtonSound(Button targetButton, string clickEventId)
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

        BuildResolutionOptions();

        float master = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        float bgm = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        float sfx = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        bool fullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
        int resolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, FindCurrentResolutionIndex());

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(master);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(bgm);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(sfx);
        }

        if (fullScreenToggle != null)
        {
            fullScreenToggle.SetIsOnWithoutNotify(fullScreen);
        }

        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            resolutionIndex = Mathf.Clamp(resolutionIndex, 0, availableResolutions.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            resolutionDropdown.RefreshShownValue();
        }

        SetMasterVolume(master);
        SetBgmVolume(bgm);
        SetSfxVolume(sfx);
        Screen.fullScreen = fullScreen;
    }

    private void BuildResolutionOptions()
    {
        availableResolutions.Clear();

        foreach (Resolution resolution in Screen.resolutions)
        {
            bool alreadyAdded = false;
            for (int i = 0; i < availableResolutions.Count; i++)
            {
                if (availableResolutions[i].width == resolution.width && availableResolutions[i].height == resolution.height)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
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
        List<string> options = new List<string>();

        foreach (Resolution resolution in availableResolutions)
        {
            options.Add($"{resolution.width} x {resolution.height}");
        }

        resolutionDropdown.AddOptions(options);
    }

    private int FindCurrentResolutionIndex()
    {
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            Resolution resolution = availableResolutions[i];
            if (resolution.width == Screen.width && resolution.height == Screen.height)
            {
                return i;
            }
        }

        return availableResolutions.Count > 0 ? availableResolutions.Count - 1 : 0;
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
