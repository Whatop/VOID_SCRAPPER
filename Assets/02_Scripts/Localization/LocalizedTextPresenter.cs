using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public sealed class LocalizedTextArgument
{
    [SerializeField] private string name;
    [SerializeField] private string value;

    public string Name => name;
    public string Value => value;
}

[DisallowMultipleComponent]
public sealed class LocalizedTextPresenter : MonoBehaviour
{
    [SerializeField] private VoidScrapperLocalizationService localizationService;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private string textKey;
    [SerializeField] private List<LocalizedTextArgument> arguments =
        new List<LocalizedTextArgument>();

    private readonly Dictionary<string, string> argumentLookup =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private VoidScrapperLocalizationService subscribedService;

    public string TextKey => textKey;

    private void OnEnable()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        ResolveServiceAndSubscribe();
        RebuildArgumentLookup();
        RefreshText();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshText()
    {
        if (targetText == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[{nameof(LocalizedTextPresenter)}] '{name}' has no TMP_Text target.",
                this);
#endif
            return;
        }

        if (subscribedService == null)
        {
            ResolveServiceAndSubscribe();
        }

        if (subscribedService == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[{nameof(LocalizedTextPresenter)}] '{name}' cannot resolve the persistent " +
                "localization service.",
                this);
#endif
            return;
        }

        if (string.IsNullOrWhiteSpace(textKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[{nameof(LocalizedTextPresenter)}] '{name}' has an empty TextKey.",
                this);
#endif
            return;
        }

        targetText.text = argumentLookup.Count > 0
            ? subscribedService.FormatText(textKey, argumentLookup)
            : subscribedService.GetText(textKey);
    }

    private void ResolveServiceAndSubscribe()
    {
        VoidScrapperLocalizationService resolved = localizationService != null
            ? localizationService
            : VoidScrapperLocalizationService.Instance;

        if (resolved == subscribedService)
        {
            return;
        }

        Unsubscribe();
        subscribedService = resolved;

        if (subscribedService != null)
        {
            subscribedService.LanguageChanged += HandleLanguageChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedService != null)
        {
            subscribedService.LanguageChanged -= HandleLanguageChanged;
            subscribedService = null;
        }
    }

    private void HandleLanguageChanged(string _)
    {
        RefreshText();
    }

    private void RebuildArgumentLookup()
    {
        argumentLookup.Clear();

        if (arguments == null)
        {
            return;
        }

        for (int i = 0; i < arguments.Count; i++)
        {
            LocalizedTextArgument argument = arguments[i];

            if (argument == null || string.IsNullOrWhiteSpace(argument.Name))
            {
                continue;
            }

            argumentLookup[argument.Name.Trim()] = argument.Value ?? string.Empty;
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditorAndTests(
        VoidScrapperLocalizationService service,
        TMP_Text target,
        string key)
    {
        localizationService = service;
        targetText = target;
        textKey = key;
    }
#endif
}
