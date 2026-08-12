using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputBindingPersistence
{
    private const string PlayerPrefsKeyPrefix = "void_scrapper_input_bindings_";
    private static readonly HashSet<int> LoadedAssets = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        LoadedAssets.Clear();
    }

    public static void LoadOnce(InputActionAsset inputActions)
    {
        if (inputActions == null)
        {
            return;
        }

        int instanceId = inputActions.GetInstanceID();
        if (!LoadedAssets.Add(instanceId))
        {
            return;
        }

        string json = PlayerPrefs.GetString(GetKey(inputActions), string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            inputActions.LoadBindingOverridesFromJson(json);
        }
    }

    public static void Save(InputActionAsset inputActions)
    {
        if (inputActions == null)
        {
            return;
        }

        PlayerPrefs.SetString(GetKey(inputActions), inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetAll(InputActionAsset inputActions)
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(GetKey(inputActions));
        PlayerPrefs.Save();
    }

    private static string GetKey(InputActionAsset inputActions)
    {
        string assetName = string.IsNullOrWhiteSpace(inputActions.name)
            ? "default"
            : inputActions.name.Trim();

        return PlayerPrefsKeyPrefix + assetName;
    }
}
