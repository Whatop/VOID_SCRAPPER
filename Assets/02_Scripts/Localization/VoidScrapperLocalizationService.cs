using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VoidScrapperLocalizationService : MonoBehaviour
{
    private const string MissingKeyPrefix = "⟦missing:";
    private const string MissingKeySuffix = "⟧";

    [SerializeField] private LocalizationCatalog localizationCatalog;
    [SerializeField] private bool forwardLanguageToPixelCrushers = true;
    [SerializeField] private bool logKoreanFallbacksInDevelopment;

    private static VoidScrapperLocalizationService activeInstance;

    private DialogueSystemController dialogueSystemController;
    private string currentLanguageCode = LocalizationLanguageCodes.Korean;
    private bool ownsActiveInstance;

    public static VoidScrapperLocalizationService Instance => activeInstance;
    public static bool HasInstance => activeInstance != null;

    public LocalizationCatalog Catalog => localizationCatalog;
    public string CurrentLanguageCode => currentLanguageCode;

    public event Action<string> LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        activeInstance = null;
    }

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogError(
                $"[{nameof(VoidScrapperLocalizationService)}] A second localization service " +
                $"was created on '{name}'. Its duplicate Dialogue Manager root will be removed.",
                this);

            GameObject duplicateRoot = gameObject;
            duplicateRoot.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(duplicateRoot);
                return;
            }
#endif

            Destroy(duplicateRoot);
            return;
        }

        activeInstance = this;
        ownsActiveInstance = true;
        dialogueSystemController = GetComponent<DialogueSystemController>();
        currentLanguageCode = LocalizationLanguageCodes.NormalizeOrKorean(
            GameSettingsRuntime.LanguageCode);

        if (localizationCatalog == null)
        {
            Debug.LogError(
                $"[{nameof(VoidScrapperLocalizationService)}] No LocalizationCatalog is assigned.",
                this);
        }
    }

    private void OnEnable()
    {
        if (!ownsActiveInstance)
        {
            return;
        }

        GameSettingsRuntime.Changed -= HandleSettingsChanged;
        GameSettingsRuntime.Changed += HandleSettingsChanged;
    }

    private void Start()
    {
        if (ownsActiveInstance)
        {
            ForwardLanguageToPixelCrushers();
        }
    }

    private void OnDisable()
    {
        GameSettingsRuntime.Changed -= HandleSettingsChanged;
    }

    private void OnDestroy()
    {
        GameSettingsRuntime.Changed -= HandleSettingsChanged;
        LanguageChanged = null;

        if (activeInstance == this)
        {
            activeInstance = null;
        }

        ownsActiveInstance = false;
    }

    public bool SetLanguage(string languageCode)
    {
        string normalized = LocalizationLanguageCodes.NormalizeOrKorean(languageCode);

        if (!LocalizationLanguageCodes.IsSupported(languageCode))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[{nameof(VoidScrapperLocalizationService)}] Unsupported language " +
                $"'{languageCode}' fell back to Korean.",
                this);
#endif
        }

        if (string.Equals(
            GameSettingsRuntime.LanguageCode,
            normalized,
            StringComparison.Ordinal))
        {
            return ApplyLanguage(normalized);
        }

        GameSettingsRuntime.SetLanguageCode(normalized);
        return true;
    }

    public string GetText(string textKey)
    {
        if (localizationCatalog != null &&
            localizationCatalog.TryGetText(
                textKey,
                currentLanguageCode,
                out string text,
                out bool usedKoreanFallback))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (usedKoreanFallback && logKoreanFallbacksInDevelopment)
            {
                Debug.LogWarning(
                    $"[{nameof(VoidScrapperLocalizationService)}] '{textKey}' has no " +
                    $"'{currentLanguageCode}' text. Korean fallback was used.",
                    this);
            }
#endif
            return text;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string visibleMarker = MissingKeyPrefix + (textKey ?? string.Empty) + MissingKeySuffix;
        Debug.LogWarning(
            $"[{nameof(VoidScrapperLocalizationService)}] Missing TextKey '{textKey}'.",
            this);
        return visibleMarker;
#else
        return string.Empty;
#endif
    }

    public string FormatText(
        string textKey,
        IReadOnlyDictionary<string, string> arguments)
    {
        string template = GetText(textKey);

        if (NamedPlaceholderUtility.TryFormat(
            template,
            arguments,
            out string formatted,
            out string missingArgument))
        {
            return formatted;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            $"[{nameof(VoidScrapperLocalizationService)}] TextKey '{textKey}' requires " +
            $"placeholder argument '{missingArgument}'.",
            this);
#endif
        return template;
    }

    private void HandleSettingsChanged()
    {
        ApplyLanguage(GameSettingsRuntime.LanguageCode);
    }

    private bool ApplyLanguage(string languageCode)
    {
        string normalized = LocalizationLanguageCodes.NormalizeOrKorean(languageCode);
        bool changed = !string.Equals(
            currentLanguageCode,
            normalized,
            StringComparison.Ordinal);

        currentLanguageCode = normalized;
        ForwardLanguageToPixelCrushers();

        if (changed)
        {
            LanguageChanged?.Invoke(currentLanguageCode);
        }

        return changed;
    }

    private void ForwardLanguageToPixelCrushers()
    {
        if (!forwardLanguageToPixelCrushers)
        {
            return;
        }

        string pixelCrushersLanguage = string.Equals(
            currentLanguageCode,
            LocalizationLanguageCodes.Korean,
            StringComparison.Ordinal)
            ? string.Empty
            : currentLanguageCode;

        if (DialogueManager.hasInstance)
        {
            DialogueManager.SetLanguage(pixelCrushersLanguage);
            return;
        }

        if (dialogueSystemController == null)
        {
            dialogueSystemController = GetComponent<DialogueSystemController>();
        }

        dialogueSystemController?.SetLanguage(pixelCrushersLanguage);
    }

#if UNITY_EDITOR
    public void ConfigureForEditorAndTests(
        LocalizationCatalog catalog,
        bool shouldForwardToPixelCrushers = false)
    {
        localizationCatalog = catalog;
        forwardLanguageToPixelCrushers = shouldForwardToPixelCrushers;
    }
#endif
}
