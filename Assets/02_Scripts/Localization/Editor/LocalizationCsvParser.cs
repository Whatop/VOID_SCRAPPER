using System;
using System.Collections.Generic;
using System.Text;

public sealed class LocalizationCsvRow
{
    public LocalizationCsvRow(int sourceRow, IReadOnlyList<string> fields)
    {
        SourceRow = sourceRow;
        Fields = fields;
    }

    public int SourceRow { get; }
    public IReadOnlyList<string> Fields { get; }
}

public sealed class LocalizationCsvDocument
{
    public LocalizationCsvDocument(string sourceName, IReadOnlyList<LocalizationCsvRow> rows)
    {
        SourceName = sourceName;
        Rows = rows;
    }

    public string SourceName { get; }
    public IReadOnlyList<LocalizationCsvRow> Rows { get; }
}

public sealed class LocalizationCsvParseResult
{
    public LocalizationCsvParseResult(
        LocalizationCsvDocument document,
        IReadOnlyList<LocalizationValidationIssue> issues)
    {
        Document = document;
        Issues = issues;
    }

    public LocalizationCsvDocument Document { get; }
    public IReadOnlyList<LocalizationValidationIssue> Issues { get; }
    public bool HasErrors => Issues != null && Issues.Count > 0;
}

public static class LocalizationCsvParser
{
    public static LocalizationCsvParseResult Parse(string sourceName, string content)
    {
        string safeSourceName = string.IsNullOrWhiteSpace(sourceName)
            ? "<memory>"
            : sourceName;
        content ??= string.Empty;

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content.Substring(1);
        }

        List<LocalizationCsvRow> rows = new List<LocalizationCsvRow>();
        List<LocalizationValidationIssue> issues =
            new List<LocalizationValidationIssue>();
        List<string> fields = new List<string>();
        StringBuilder fieldBuilder = new StringBuilder();

        bool inQuotes = false;
        bool closedQuotedField = false;
        int currentLine = 1;
        int recordStartLine = 1;

        for (int i = 0; i < content.Length; i++)
        {
            char current = content[i];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        fieldBuilder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        closedQuotedField = true;
                    }

                    continue;
                }

                if (current == '\r')
                {
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    fieldBuilder.Append('\n');
                    currentLine++;
                    continue;
                }

                if (current == '\n')
                {
                    fieldBuilder.Append('\n');
                    currentLine++;
                    continue;
                }

                fieldBuilder.Append(current);
                continue;
            }

            if (closedQuotedField)
            {
                if (current == ',')
                {
                    AddField(fields, fieldBuilder);
                    closedQuotedField = false;
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    AddField(fields, fieldBuilder);
                    AddRow(rows, recordStartLine, fields);
                    closedQuotedField = false;
                    ConsumeNewline(content, ref i, ref currentLine);
                    recordStartLine = currentLine;
                    continue;
                }

                issues.Add(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "InvalidCsvQuoting",
                    safeSourceName,
                    currentLine,
                    "Only a comma or line ending may follow a closing quote."));
                return new LocalizationCsvParseResult(
                    new LocalizationCsvDocument(safeSourceName, rows),
                    issues);
            }

            if (current == '"')
            {
                if (fieldBuilder.Length != 0)
                {
                    issues.Add(new LocalizationValidationIssue(
                        LocalizationValidationSeverity.Error,
                        "InvalidCsvQuoting",
                        safeSourceName,
                        currentLine,
                        "A quote may only begin at the start of a CSV field."));
                    return new LocalizationCsvParseResult(
                        new LocalizationCsvDocument(safeSourceName, rows),
                        issues);
                }

                inQuotes = true;
                continue;
            }

            if (current == ',')
            {
                AddField(fields, fieldBuilder);
                continue;
            }

            if (current == '\r' || current == '\n')
            {
                AddField(fields, fieldBuilder);
                AddRow(rows, recordStartLine, fields);
                ConsumeNewline(content, ref i, ref currentLine);
                recordStartLine = currentLine;
                continue;
            }

            fieldBuilder.Append(current);
        }

        if (inQuotes)
        {
            issues.Add(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "InvalidCsvQuoting",
                safeSourceName,
                recordStartLine,
                "The final quoted field is not closed."));
        }
        else if (closedQuotedField || fieldBuilder.Length > 0 || fields.Count > 0)
        {
            AddField(fields, fieldBuilder);
            AddRow(rows, recordStartLine, fields);
        }

        return new LocalizationCsvParseResult(
            new LocalizationCsvDocument(safeSourceName, rows),
            issues);
    }

    private static void AddField(List<string> fields, StringBuilder fieldBuilder)
    {
        fields.Add(fieldBuilder.ToString());
        fieldBuilder.Clear();
    }

    private static void AddRow(
        List<LocalizationCsvRow> rows,
        int sourceRow,
        List<string> fields)
    {
        bool hasContent = false;

        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrEmpty(fields[i]))
            {
                hasContent = true;
                break;
            }
        }

        if (hasContent)
        {
            rows.Add(new LocalizationCsvRow(sourceRow, fields.ToArray()));
        }

        fields.Clear();
    }

    private static void ConsumeNewline(
        string content,
        ref int index,
        ref int currentLine)
    {
        if (content[index] == '\r' &&
            index + 1 < content.Length &&
            content[index + 1] == '\n')
        {
            index++;
        }

        currentLine++;
    }
}
