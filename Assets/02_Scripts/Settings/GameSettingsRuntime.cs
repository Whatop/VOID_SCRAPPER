using System;
using UnityEngine;

public static class GameSettingsRuntime
{
    private const string ShowHudHintsKey = "settings_show_hud_hints";
    private const string UiScaleKey = "settings_ui_scale";
    private const string TextScaleKey = "settings_text_scale";
    private const string CameraShakeKey = "settings_camera_shake";
    private const string WarningOpacityKey = "settings_warning_opacity";

    private static bool loaded;
    private static bool showHudKeyHints = true;
    private static float uiScale = 1f;
    private static float textScale = 1f;
    private static float cameraShakeMultiplier = 1f;
    private static float warningOpacity = 1f;

    public static bool ShowHudKeyHints
    {
        get
        {
            EnsureLoaded();
            return showHudKeyHints;
        }
    }

    public static float UiScale
    {
        get
        {
            EnsureLoaded();
            return uiScale;
        }
    }

    public static float TextScale
    {
        get
        {
            EnsureLoaded();
            return textScale;
        }
    }

    public static float CameraShakeMultiplier
    {
        get
        {
            EnsureLoaded();
            return cameraShakeMultiplier;
        }
    }

    public static float WarningOpacity
    {
        get
        {
            EnsureLoaded();
            return warningOpacity;
        }
    }

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        loaded = false;
        Changed = null;
    }

    public static void SetShowHudKeyHints(bool value)
    {
        EnsureLoaded();
        showHudKeyHints = value;
        PlayerPrefs.SetInt(ShowHudHintsKey, value ? 1 : 0);
        SaveAndNotify();
    }

    public static void SetUiScale(float value)
    {
        EnsureLoaded();
        uiScale = Mathf.Clamp(value, 0.75f, 1.5f);
        PlayerPrefs.SetFloat(UiScaleKey, uiScale);
        SaveAndNotify();
    }

    public static void SetTextScale(float value)
    {
        EnsureLoaded();
        textScale = Mathf.Clamp(value, 0.8f, 1.5f);
        PlayerPrefs.SetFloat(TextScaleKey, textScale);
        SaveAndNotify();
    }

    public static void SetCameraShakeMultiplier(float value)
    {
        EnsureLoaded();
        cameraShakeMultiplier = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(CameraShakeKey, cameraShakeMultiplier);
        SaveAndNotify();
    }

    public static void SetWarningOpacity(float value)
    {
        EnsureLoaded();
        warningOpacity = Mathf.Clamp(value, 0.25f, 1f);
        PlayerPrefs.SetFloat(WarningOpacityKey, warningOpacity);
        SaveAndNotify();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        showHudKeyHints = PlayerPrefs.GetInt(ShowHudHintsKey, 1) == 1;
        uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, 1f), 0.75f, 1.5f);
        textScale = Mathf.Clamp(PlayerPrefs.GetFloat(TextScaleKey, 1f), 0.8f, 1.5f);
        cameraShakeMultiplier = Mathf.Clamp01(PlayerPrefs.GetFloat(CameraShakeKey, 1f));
        warningOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(WarningOpacityKey, 1f), 0.25f, 1f);
    }

    private static void SaveAndNotify()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
