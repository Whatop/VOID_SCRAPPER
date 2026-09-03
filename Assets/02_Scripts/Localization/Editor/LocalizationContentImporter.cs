using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LocalizationContentImporter
{
    public const string DefaultSourceAssetPath =
        "Assets/02_Scripts/Config/Localization/Source/Localization.csv";
    public const string DefaultCatalogAssetPath =
        "Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset";

    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    [MenuItem("VOID SCRAPPER/Localization/Import Catalog")]
    public static void ImportDefaultCatalogFromMenu()
    {
        Import(DefaultSourceAssetPath, DefaultCatalogAssetPath, true);
    }

    [MenuItem("VOID SCRAPPER/Localization/Validate Catalog")]
    public static void ValidateDefaultCatalogFromMenu()
    {
        if (!TryReadUtf8File(DefaultSourceAssetPath, out string csvContent, out string readError))
        {
            Debug.LogError(readError);
            return;
        }

        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            DefaultSourceAssetPath,
            csvContent);
        DialoguePresentationValidator.AppendPhase2CTutorialIssues(
            report,
            DefaultSourceAssetPath);
        LogReport(report);

        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            DefaultCatalogAssetPath);
        LocalizationValidationIssue freshness =
            LocalizationContentValidator.ValidateCatalogFreshness(
                catalog,
                DefaultSourceAssetPath,
                csvContent);

        if (freshness != null)
        {
            LogIssue(freshness);
        }
        else if (!report.HasErrors)
        {
            Debug.Log(
                $"[Localization] Validation passed. Records={report.Records.Count} " +
                $"Catalog='{DefaultCatalogAssetPath}'.");
        }
    }

    public static bool Import(
        string sourceAssetPath,
        string catalogAssetPath,
        bool logResults)
    {
        if (!TryReadUtf8File(sourceAssetPath, out string csvContent, out string readError))
        {
            if (logResults)
            {
                Debug.LogError(readError);
            }

            return false;
        }

        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            sourceAssetPath,
            csvContent);
        DialoguePresentationValidator.AppendPhase2CTutorialIssues(
            report,
            sourceAssetPath);

        if (report.HasErrors)
        {
            if (logResults)
            {
                LogReport(report);
                Debug.LogError(
                    "[Localization] Import aborted. The existing generated catalog was not modified.");
            }

            return false;
        }

        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            catalogAssetPath);

        if (catalog != null &&
            string.Equals(catalog.ContentHash, report.ContentHash, StringComparison.Ordinal) &&
            string.Equals(catalog.SourceAssetPath, sourceAssetPath, StringComparison.Ordinal))
        {
            if (logResults)
            {
                LogReport(report);
                Debug.Log(
                    $"[Localization] Catalog is already current. Records={report.Records.Count}.");
            }

            return true;
        }

        List<LocalizationCsvRecord> sortedRecords =
            new List<LocalizationCsvRecord>(report.Records);
        sortedRecords.Sort((left, right) =>
            string.CompareOrdinal(left.TextKey, right.TextKey));

        List<LocalizationEntry> generatedEntries =
            new List<LocalizationEntry>(sortedRecords.Count);

        for (int i = 0; i < sortedRecords.Count; i++)
        {
            generatedEntries.Add(sortedRecords[i].ToCatalogEntry());
        }

        bool createdAsset = catalog == null;

        if (createdAsset)
        {
            string directory = Path.GetDirectoryName(catalogAssetPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            catalog = ScriptableObject.CreateInstance<LocalizationCatalog>();
        }

        catalog.SetGeneratedContentForEditor(
            generatedEntries,
            sourceAssetPath,
            report.ContentHash);

        if (createdAsset)
        {
            AssetDatabase.CreateAsset(catalog, catalogAssetPath);
        }
        else
        {
            EditorUtility.SetDirty(catalog);
        }

        AssetDatabase.SaveAssets();

        if (logResults)
        {
            LogReport(report);
            Debug.Log(
                $"[Localization] {(createdAsset ? "Created" : "Updated")} catalog " +
                $"'{catalogAssetPath}'. Records={generatedEntries.Count}.");
        }

        return true;
    }

    public static bool TryReadUtf8File(
        string assetPath,
        out string content,
        out string error)
    {
        content = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            error = "[Localization] Source CSV path is empty.";
            return false;
        }

        string fullPath = Path.GetFullPath(assetPath);

        if (!File.Exists(fullPath))
        {
            error = $"[Localization] Source CSV was not found: '{assetPath}'.";
            return false;
        }

        try
        {
            content = File.ReadAllText(fullPath, StrictUtf8);
            return true;
        }
        catch (Exception exception)
        {
            error = $"[Localization] Could not read UTF-8 CSV '{assetPath}': {exception.Message}";
            return false;
        }
    }

    private static void LogReport(LocalizationValidationReport report)
    {
        if (report == null)
        {
            return;
        }

        for (int i = 0; i < report.Issues.Count; i++)
        {
            LogIssue(report.Issues[i]);
        }
    }

    private static void LogIssue(LocalizationValidationIssue issue)
    {
        if (issue.Severity == LocalizationValidationSeverity.Error)
        {
            Debug.LogError(issue.ToString());
        }
        else
        {
            Debug.LogWarning(issue.ToString());
        }
    }
}
