using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public enum LocalizationValidationSeverity
{
    Warning,
    Error
}

public sealed class LocalizationValidationIssue
{
    public LocalizationValidationIssue(
        LocalizationValidationSeverity severity,
        string code,
        string sourceName,
        int sourceRow,
        string message)
    {
        Severity = severity;
        Code = code;
        SourceName = sourceName;
        SourceRow = sourceRow;
        Message = message;
    }

    public LocalizationValidationSeverity Severity { get; }
    public string Code { get; }
    public string SourceName { get; }
    public int SourceRow { get; }
    public string Message { get; }

    public override string ToString()
    {
        string rowText = SourceRow > 0 ? $":{SourceRow}" : string.Empty;
        return $"[{Severity}] {SourceName}{rowText} {Code}: {Message}";
    }
}

public sealed class LocalizationCsvRecord
{
    public LocalizationCsvRecord(
        int sourceRow,
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
        SourceRow = sourceRow;
        TextKey = textKey;
        Korean = korean;
        English = english;
        Japanese = japanese;
        SimplifiedChinese = simplifiedChinese;
        TraditionalChinese = traditionalChinese;
        Context = context;
        Notes = notes;
        MaxLengthHint = maxLengthHint;
    }

    public int SourceRow { get; }
    public string TextKey { get; }
    public string Korean { get; }
    public string English { get; }
    public string Japanese { get; }
    public string SimplifiedChinese { get; }
    public string TraditionalChinese { get; }
    public string Context { get; }
    public string Notes { get; }
    public int MaxLengthHint { get; }

    public LocalizationEntry ToCatalogEntry()
    {
        return new LocalizationEntry(
            TextKey,
            Korean,
            English,
            Japanese,
            SimplifiedChinese,
            TraditionalChinese,
            Context,
            Notes,
            MaxLengthHint);
    }
}

public sealed class LocalizationValidationReport
{
    private readonly List<LocalizationValidationIssue> issues =
        new List<LocalizationValidationIssue>();
    private readonly List<LocalizationCsvRecord> records =
        new List<LocalizationCsvRecord>();

    public IReadOnlyList<LocalizationValidationIssue> Issues => issues;
    public IReadOnlyList<LocalizationCsvRecord> Records => records;
    public string ContentHash { get; internal set; }

    public bool HasErrors
    {
        get
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == LocalizationValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal void AddIssue(LocalizationValidationIssue issue)
    {
        issues.Add(issue);
    }

    internal void AddRecord(LocalizationCsvRecord record)
    {
        records.Add(record);
    }
}

public readonly struct LocalizationKeyReference
{
    public LocalizationKeyReference(string textKey, string owner)
    {
        TextKey = textKey;
        Owner = owner;
    }

    public string TextKey { get; }
    public string Owner { get; }
}

public static class LocalizationContentValidator
{
    private static readonly string[] RequiredHeaders =
    {
        "TextKey",
        "ko",
        "en",
        "ja",
        "zh-Hans",
        "zh-Hant",
        "Context",
        "Notes",
        "MaxLengthHint"
    };

    public static LocalizationValidationReport ValidateCsv(
        string sourceName,
        string csvContent)
    {
        LocalizationValidationReport report = new LocalizationValidationReport();
        LocalizationCsvParseResult parseResult = LocalizationCsvParser.Parse(
            sourceName,
            csvContent);

        for (int i = 0; i < parseResult.Issues.Count; i++)
        {
            report.AddIssue(parseResult.Issues[i]);
        }

        if (parseResult.HasErrors ||
            parseResult.Document == null ||
            parseResult.Document.Rows.Count == 0)
        {
            if (!parseResult.HasErrors)
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "MissingHeader",
                    sourceName,
                    1,
                    "The localization CSV has no header row."));
            }

            return report;
        }

        LocalizationCsvRow headerRow = parseResult.Document.Rows[0];

        if (!ValidateHeaders(sourceName, headerRow, report))
        {
            return report;
        }

        Dictionary<string, LocalizationCsvRecord> firstByKey =
            new Dictionary<string, LocalizationCsvRecord>(StringComparer.Ordinal);
        HashSet<string> completeRows = new HashSet<string>(StringComparer.Ordinal);
        string previousKey = null;

        for (int i = 1; i < parseResult.Document.Rows.Count; i++)
        {
            LocalizationCsvRow row = parseResult.Document.Rows[i];

            if (row.Fields.Count != RequiredHeaders.Length)
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "ColumnCount",
                    sourceName,
                    row.SourceRow,
                    $"Expected {RequiredHeaders.Length} fields but found {row.Fields.Count}."));
                continue;
            }

            string textKey = row.Fields[0].Trim();
            string korean = row.Fields[1];
            int maxLengthHint = 0;

            if (string.IsNullOrEmpty(textKey))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "EmptyTextKey",
                    sourceName,
                    row.SourceRow,
                    "TextKey is required."));
            }
            else if (firstByKey.TryGetValue(textKey, out LocalizationCsvRecord firstRecord))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "DuplicateTextKey",
                    sourceName,
                    row.SourceRow,
                    $"TextKey '{textKey}' was first declared on row {firstRecord.SourceRow}."));
            }

            if (string.IsNullOrWhiteSpace(korean))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "EmptyKoreanSource",
                    sourceName,
                    row.SourceRow,
                    $"TextKey '{textKey}' requires Korean source text."));
            }

            string maxLengthText = row.Fields[8].Trim();

            if (!string.IsNullOrEmpty(maxLengthText) &&
                (!int.TryParse(
                    maxLengthText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out maxLengthHint) ||
                 maxLengthHint <= 0))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "InvalidMaxLengthHint",
                    sourceName,
                    row.SourceRow,
                    $"MaxLengthHint '{maxLengthText}' must be an empty value or a positive integer."));
                maxLengthHint = 0;
            }

            for (int fieldIndex = 0; fieldIndex < row.Fields.Count; fieldIndex++)
            {
                if (ContainsUnsafeControlCharacter(row.Fields[fieldIndex]))
                {
                    report.AddIssue(new LocalizationValidationIssue(
                        LocalizationValidationSeverity.Error,
                        "UnsafeControlCharacter",
                        sourceName,
                        row.SourceRow,
                        $"Column '{RequiredHeaders[fieldIndex]}' contains an unsafe control character."));
                }
            }

            ValidatePlaceholders(sourceName, row, report);
            ValidateTutorialDialogueLineBreaks(
                sourceName,
                textKey,
                row,
                report);

            string rowSignature = string.Join("\u001F", row.Fields);

            if (!completeRows.Add(rowSignature))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "DuplicateCsvRow",
                    sourceName,
                    row.SourceRow,
                    "This complete CSV row is duplicated."));
            }

            if (!string.IsNullOrEmpty(previousKey) &&
                string.CompareOrdinal(previousKey, textKey) > 0)
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Warning,
                    "NonDeterministicRowOrder",
                    sourceName,
                    row.SourceRow,
                    "Rows should be ordered by TextKey. Generated catalog order will be normalized."));
            }

            previousKey = textKey;

            LocalizationCsvRecord record = new LocalizationCsvRecord(
                row.SourceRow,
                textKey,
                korean,
                row.Fields[2],
                row.Fields[3],
                row.Fields[4],
                row.Fields[5],
                row.Fields[6],
                row.Fields[7],
                maxLengthHint);
            report.AddRecord(record);

            if (!string.IsNullOrEmpty(textKey) && !firstByKey.ContainsKey(textKey))
            {
                firstByKey.Add(textKey, record);
            }
        }

        List<LocalizationCsvRecord> sortedRecords =
            new List<LocalizationCsvRecord>(report.Records);
        sortedRecords.Sort((left, right) =>
            string.CompareOrdinal(left.TextKey, right.TextKey));
        report.ContentHash = ComputeContentHash(sortedRecords);
        return report;
    }

    private static void ValidateTutorialDialogueLineBreaks(
        string sourceName,
        string textKey,
        LocalizationCsvRow row,
        LocalizationValidationReport report)
    {
        if (string.IsNullOrEmpty(textKey) ||
            !textKey.StartsWith("dialogue.tutorial.", StringComparison.Ordinal))
        {
            return;
        }

        for (int fieldIndex = 1; fieldIndex <= 5; fieldIndex++)
        {
            string text = row.Fields[fieldIndex];
            if (text.IndexOf('\r') < 0 && text.IndexOf('\n') < 0)
            {
                continue;
            }

            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "TutorialDialogueEmbeddedNewline",
                sourceName,
                row.SourceRow,
                $"Tutorial TextKey '{textKey}' contains a manual newline in " +
                $"column '{RequiredHeaders[fieldIndex]}'. Use TMP automatic wrapping."));
        }
    }

    public static IReadOnlyList<LocalizationValidationIssue> ValidateReferencedKeys(
        LocalizationCatalog catalog,
        IReadOnlyList<LocalizationKeyReference> references)
    {
        List<LocalizationValidationIssue> issues =
            new List<LocalizationValidationIssue>();

        if (catalog == null)
        {
            issues.Add(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MissingGeneratedCatalog",
                "LocalizationCatalog",
                0,
                "The generated LocalizationCatalog is missing."));
            return issues;
        }

        if (references == null)
        {
            return issues;
        }

        for (int i = 0; i < references.Count; i++)
        {
            LocalizationKeyReference reference = references[i];

            if (!catalog.TryGetEntry(reference.TextKey, out _))
            {
                issues.Add(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "UnknownReferencedTextKey",
                    reference.Owner ?? "<unknown owner>",
                    0,
                    $"Referenced TextKey '{reference.TextKey}' is not in the generated catalog."));
            }
        }

        return issues;
    }

    public static LocalizationValidationIssue ValidateCatalogFreshness(
        LocalizationCatalog catalog,
        string sourceName,
        string csvContent)
    {
        if (catalog == null)
        {
            return new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MissingGeneratedCatalog",
                sourceName,
                0,
                "The generated LocalizationCatalog is missing.");
        }

        LocalizationValidationReport report = ValidateCsv(sourceName, csvContent);

        if (report.HasErrors)
        {
            return new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "InvalidSourceCsv",
                sourceName,
                0,
                "Catalog freshness cannot be evaluated because the source CSV is invalid.");
        }

        return string.Equals(
            catalog.ContentHash,
            report.ContentHash,
            StringComparison.Ordinal)
            ? null
            : new LocalizationValidationIssue(
                LocalizationValidationSeverity.Warning,
                "StaleGeneratedCatalog",
                sourceName,
                0,
                "The generated LocalizationCatalog does not match the current source CSV.");
    }

    public static string ComputeContentHash(IReadOnlyList<LocalizationCsvRecord> records)
    {
        StringBuilder builder = new StringBuilder();

        if (records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                LocalizationCsvRecord record = records[i];
                AppendHashField(builder, record.TextKey);
                AppendHashField(builder, record.Korean);
                AppendHashField(builder, record.English);
                AppendHashField(builder, record.Japanese);
                AppendHashField(builder, record.SimplifiedChinese);
                AppendHashField(builder, record.TraditionalChinese);
                AppendHashField(builder, record.Context);
                AppendHashField(builder, record.Notes);
                builder.Append(record.MaxLengthHint.ToString(CultureInfo.InvariantCulture));
                builder.Append('\u001E');
            }
        }

        using SHA256 sha256 = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        byte[] hash = sha256.ComputeHash(bytes);
        StringBuilder hex = new StringBuilder(hash.Length * 2);

        for (int i = 0; i < hash.Length; i++)
        {
            hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }

    private static bool ValidateHeaders(
        string sourceName,
        LocalizationCsvRow headerRow,
        LocalizationValidationReport report)
    {
        bool valid = true;

        if (headerRow.Fields.Count != RequiredHeaders.Length)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "HeaderColumnCount",
                sourceName,
                headerRow.SourceRow,
                $"Expected {RequiredHeaders.Length} columns but found {headerRow.Fields.Count}."));
            valid = false;
        }

        int comparisonCount = Math.Min(headerRow.Fields.Count, RequiredHeaders.Length);

        for (int i = 0; i < comparisonCount; i++)
        {
            if (string.Equals(headerRow.Fields[i], RequiredHeaders[i], StringComparison.Ordinal))
            {
                continue;
            }

            string code = IsLanguageLikeHeader(headerRow.Fields[i])
                ? "UnsupportedLanguageCode"
                : "UnexpectedHeader";
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                code,
                sourceName,
                headerRow.SourceRow,
                $"Column {i + 1} must be '{RequiredHeaders[i]}' but was '{headerRow.Fields[i]}'."));
            valid = false;
        }

        for (int i = RequiredHeaders.Length; i < headerRow.Fields.Count; i++)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "UnsupportedLanguageCode",
                sourceName,
                headerRow.SourceRow,
                $"Unsupported localization column '{headerRow.Fields[i]}'."));
            valid = false;
        }

        return valid;
    }

    private static void ValidatePlaceholders(
        string sourceName,
        LocalizationCsvRow row,
        LocalizationValidationReport report)
    {
        HashSet<string> koreanPlaceholders = new HashSet<string>(StringComparer.Ordinal);

        if (!NamedPlaceholderUtility.TryCollectPlaceholders(
            row.Fields[1],
            koreanPlaceholders,
            out string koreanError))
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MalformedPlaceholder",
                sourceName,
                row.SourceRow,
                $"Korean source: {koreanError}"));
            return;
        }

        HashSet<string> translatedPlaceholders = new HashSet<string>(StringComparer.Ordinal);

        for (int fieldIndex = 2; fieldIndex <= 5; fieldIndex++)
        {
            string translatedText = row.Fields[fieldIndex];

            if (string.IsNullOrEmpty(translatedText))
            {
                continue;
            }

            if (!NamedPlaceholderUtility.TryCollectPlaceholders(
                translatedText,
                translatedPlaceholders,
                out string translatedError))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "MalformedPlaceholder",
                    sourceName,
                    row.SourceRow,
                    $"{RequiredHeaders[fieldIndex]}: {translatedError}"));
                continue;
            }

            if (!koreanPlaceholders.SetEquals(translatedPlaceholders))
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "PlaceholderMismatch",
                    sourceName,
                    row.SourceRow,
                    $"{RequiredHeaders[fieldIndex]} placeholders must match Korean source placeholders."));
            }
        }
    }

    private static bool ContainsUnsafeControlCharacter(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];

            if (char.IsControl(current) && current != '\n')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLanguageLikeHeader(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= 12 &&
               value.IndexOf(' ') < 0;
    }

    private static void AppendHashField(StringBuilder builder, string value)
    {
        builder.Append(value ?? string.Empty);
        builder.Append('\u001F');
    }
}
