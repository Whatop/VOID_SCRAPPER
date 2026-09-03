using System;
using System.Collections.Generic;
using System.Text;

public static class NamedPlaceholderUtility
{
    public static bool TryFormat(
        string template,
        IReadOnlyDictionary<string, string> arguments,
        out string formatted,
        out string missingArgument)
    {
        template ??= string.Empty;
        missingArgument = string.Empty;
        StringBuilder builder = null;
        int copyStart = 0;

        for (int i = 0; i < template.Length; i++)
        {
            char current = template[i];

            if (current == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                builder ??= new StringBuilder(template.Length + 16);
                builder.Append(template, copyStart, i - copyStart);
                builder.Append('{');
                i++;
                copyStart = i + 1;
                continue;
            }

            if (current == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                builder ??= new StringBuilder(template.Length + 16);
                builder.Append(template, copyStart, i - copyStart);
                builder.Append('}');
                i++;
                copyStart = i + 1;
                continue;
            }

            if (current != '{')
            {
                continue;
            }

            int closeIndex = template.IndexOf('}', i + 1);

            if (closeIndex < 0)
            {
                formatted = template;
                missingArgument = template.Substring(i);
                return false;
            }

            string placeholderName = template.Substring(i + 1, closeIndex - i - 1);

            if (!IsValidPlaceholderName(placeholderName) ||
                arguments == null ||
                !arguments.TryGetValue(placeholderName, out string value))
            {
                formatted = template;
                missingArgument = placeholderName;
                return false;
            }

            builder ??= new StringBuilder(template.Length + 16);
            builder.Append(template, copyStart, i - copyStart);
            builder.Append(value ?? string.Empty);
            i = closeIndex;
            copyStart = i + 1;
        }

        if (builder == null)
        {
            formatted = template;
            return true;
        }

        builder.Append(template, copyStart, template.Length - copyStart);
        formatted = builder.ToString();
        return true;
    }

    public static bool TryCollectPlaceholders(
        string template,
        HashSet<string> destination,
        out string error)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        error = string.Empty;
        template ??= string.Empty;

        for (int i = 0; i < template.Length; i++)
        {
            char current = template[i];

            if (current == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    i++;
                    continue;
                }

                int closeIndex = template.IndexOf('}', i + 1);

                if (closeIndex < 0)
                {
                    error = $"Unclosed placeholder beginning at character {i}.";
                    return false;
                }

                string placeholderName = template.Substring(i + 1, closeIndex - i - 1);

                if (!IsValidPlaceholderName(placeholderName))
                {
                    error = $"Invalid placeholder '{{{placeholderName}}}' at character {i}.";
                    return false;
                }

                destination.Add(placeholderName);
                i = closeIndex;
                continue;
            }

            if (current != '}')
            {
                continue;
            }

            if (i + 1 < template.Length && template[i + 1] == '}')
            {
                i++;
                continue;
            }

            error = $"Unmatched closing brace at character {i}.";
            return false;
        }

        return true;
    }

    public static bool IsValidPlaceholderName(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            char current = value[i];

            if (!IsAsciiLetter(current) && !char.IsDigit(current) && current != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
    }
}
