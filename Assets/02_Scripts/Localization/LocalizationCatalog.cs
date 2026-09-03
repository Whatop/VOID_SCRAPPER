using System;
using System.Collections.Generic;
using UnityEngine;

public static class LocalizationLanguageCodes
{
    public const string Korean = "ko";
    public const string English = "en";
    public const string Japanese = "ja";
    public const string SimplifiedChinese = "zh-Hans";
    public const string TraditionalChinese = "zh-Hant";

    private static readonly string[] SupportedCodes =
    {
        Korean,
        English,
        Japanese,
        SimplifiedChinese,
        TraditionalChinese
    };

    public static IReadOnlyList<string> All => SupportedCodes;

    public static bool IsSupported(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        string trimmed = languageCode.Trim();

        for (int i = 0; i < SupportedCodes.Length; i++)
        {
            if (string.Equals(SupportedCodes[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeOrKorean(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Korean;
        }

        string trimmed = languageCode.Trim();

        for (int i = 0; i < SupportedCodes.Length; i++)
        {
            if (string.Equals(SupportedCodes[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return SupportedCodes[i];
            }
        }

        return Korean;
    }
}

[Serializable]
public sealed class LocalizationEntry
{
    [SerializeField] private string textKey;
    [SerializeField] private string korean;
    [SerializeField] private string english;
    [SerializeField] private string japanese;
    [SerializeField] private string simplifiedChinese;
    [SerializeField] private string traditionalChinese;
    [SerializeField] private string context;
    [SerializeField] private string notes;
    [SerializeField] private int maxLengthHint;

    public string TextKey => textKey;
    public string Korean => korean;
    public string English => english;
    public string Japanese => japanese;
    public string SimplifiedChinese => simplifiedChinese;
    public string TraditionalChinese => traditionalChinese;
    public string Context => context;
    public string Notes => notes;
    public int MaxLengthHint => maxLengthHint;

    public LocalizationEntry(
        string textKey,
        string korean,
        string english,
        string japanese,
        string simplifiedChinese,
        string traditionalChinese,
        string context,
        string notes,
        int maxLengthHint)
    {
        this.textKey = textKey ?? string.Empty;
        this.korean = korean ?? string.Empty;
        this.english = english ?? string.Empty;
        this.japanese = japanese ?? string.Empty;
        this.simplifiedChinese = simplifiedChinese ?? string.Empty;
        this.traditionalChinese = traditionalChinese ?? string.Empty;
        this.context = context ?? string.Empty;
        this.notes = notes ?? string.Empty;
        this.maxLengthHint = Mathf.Max(0, maxLengthHint);
    }

    public string GetText(string languageCode)
    {
        switch (LocalizationLanguageCodes.NormalizeOrKorean(languageCode))
        {
            case LocalizationLanguageCodes.English:
                return english;
            case LocalizationLanguageCodes.Japanese:
                return japanese;
            case LocalizationLanguageCodes.SimplifiedChinese:
                return simplifiedChinese;
            case LocalizationLanguageCodes.TraditionalChinese:
                return traditionalChinese;
            default:
                return korean;
        }
    }
}

[CreateAssetMenu(
    fileName = "LocalizationCatalog",
    menuName = "VOID SCRAPPER/Localization/Localization Catalog")]
public sealed class LocalizationCatalog : ScriptableObject
{
    [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();
    [SerializeField] private string sourceAssetPath;
    [SerializeField] private string contentHash;

    private Dictionary<string, LocalizationEntry> lookup;

    public IReadOnlyList<LocalizationEntry> Entries => entries;
    public string SourceAssetPath => sourceAssetPath;
    public string ContentHash => contentHash;

    private void OnEnable()
    {
        BuildLookup();
    }

    public bool TryGetEntry(string textKey, out LocalizationEntry entry)
    {
        if (lookup == null)
        {
            BuildLookup();
        }

        if (string.IsNullOrWhiteSpace(textKey))
        {
            entry = null;
            return false;
        }

        return lookup.TryGetValue(textKey.Trim(), out entry);
    }

    public bool TryGetText(
        string textKey,
        string languageCode,
        out string text,
        out bool usedKoreanFallback)
    {
        text = string.Empty;
        usedKoreanFallback = false;

        if (!TryGetEntry(textKey, out LocalizationEntry entry) || entry == null)
        {
            return false;
        }

        string normalizedLanguage = LocalizationLanguageCodes.NormalizeOrKorean(languageCode);
        text = entry.GetText(normalizedLanguage);

        if (string.IsNullOrEmpty(text) &&
            !string.Equals(
                normalizedLanguage,
                LocalizationLanguageCodes.Korean,
                StringComparison.Ordinal))
        {
            text = entry.Korean;
            usedKoreanFallback = true;
        }

        return true;
    }

    private void BuildLookup()
    {
        if (lookup == null)
        {
            lookup = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
        }
        else
        {
            lookup.Clear();
        }

        if (entries == null)
        {
            entries = new List<LocalizationEntry>();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            LocalizationEntry entry = entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.TextKey))
            {
                continue;
            }

            string key = entry.TextKey.Trim();

            if (!lookup.ContainsKey(key))
            {
                lookup.Add(key, entry);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogError(
                    $"[{nameof(LocalizationCatalog)}] Duplicate runtime TextKey '{key}' in {name}.",
                    this);
            }
#endif
        }
    }

#if UNITY_EDITOR
    public void SetGeneratedContentForEditor(
        IReadOnlyList<LocalizationEntry> generatedEntries,
        string generatedSourceAssetPath,
        string generatedContentHash)
    {
        entries = generatedEntries != null
            ? new List<LocalizationEntry>(generatedEntries)
            : new List<LocalizationEntry>();
        sourceAssetPath = generatedSourceAssetPath ?? string.Empty;
        contentHash = generatedContentHash ?? string.Empty;
        BuildLookup();
    }
#endif
}
