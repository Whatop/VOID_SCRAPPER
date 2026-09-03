using System;
using System.Collections.Generic;
using System.Text;

public static class DialogueWordWrapUtility
{
    public const char WordJoiner = '\u2060';
    public const char NonBreakingSpace = '\u00A0';

    private static readonly HashSet<string> PairedRichTextTags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "b",
            "i",
            "u",
            "s",
            "strikethrough",
            "color",
            "alpha",
            "size",
            "font",
            "material",
            "mark",
            "nobr",
            "style",
            "link",
            "uppercase",
            "lowercase",
            "smallcaps",
            "sup",
            "sub",
            "cspace",
            "mspace",
            "align"
        };

    public static string ProtectBindingDisplayString(string displayString)
    {
        if (string.IsNullOrEmpty(displayString))
        {
            return string.Empty;
        }

        return displayString.Replace(' ', NonBreakingSpace);
    }

    public static string ApplyWordSafeWrapping(string resolvedText)
    {
        if (string.IsNullOrEmpty(resolvedText))
        {
            return resolvedText ?? string.Empty;
        }

        resolvedText = RemoveGeneratedWordJoiners(resolvedText);

        StringBuilder builder = new StringBuilder(resolvedText.Length * 2);
        bool hasVisibleElementInToken = false;
        bool protectCurrentToken = false;

        for (int index = 0; index < resolvedText.Length; index++)
        {
            char character = resolvedText[index];

            if (!hasVisibleElementInToken && !IsBreakableWhitespace(character))
            {
                protectCurrentToken = TokenNeedsProtection(resolvedText, index);
            }

            if (character == '<' &&
                TryFindDelimitedEnd(resolvedText, index, '>', out int tagEnd))
            {
                string tag = resolvedText.Substring(index, tagEnd - index + 1);
                builder.Append(tag);

                if (IsExplicitLineBreakTag(tag))
                {
                    hasVisibleElementInToken = false;
                    protectCurrentToken = false;
                }

                index = tagEnd;
                continue;
            }

            if ((character == '{' &&
                 TryFindDelimitedEnd(resolvedText, index, '}', out int placeholderEnd)) ||
                (character == '[' &&
                 TryFindDelimitedEnd(resolvedText, index, ']', out placeholderEnd)))
            {
                AppendJoinerIfNeeded(
                    builder,
                    protectCurrentToken && hasVisibleElementInToken);
                builder.Append(resolvedText, index, placeholderEnd - index + 1);
                hasVisibleElementInToken = true;
                index = placeholderEnd;
                continue;
            }

            if (IsBreakableWhitespace(character))
            {
                builder.Append(character);
                hasVisibleElementInToken = false;
                protectCurrentToken = false;
                continue;
            }

            AppendJoinerIfNeeded(
                builder,
                protectCurrentToken && hasVisibleElementInToken);
            builder.Append(character);

            if (char.IsHighSurrogate(character) &&
                index + 1 < resolvedText.Length &&
                char.IsLowSurrogate(resolvedText[index + 1]))
            {
                index++;
                builder.Append(resolvedText[index]);
            }

            hasVisibleElementInToken = true;
        }

        return builder.ToString();
    }

    public static bool ContainsUnresolvedInputBinding(string text)
    {
        return !string.IsNullOrEmpty(text) &&
               text.IndexOf("[var=", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool TryValidateBalancedRichTextTags(
        string text,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        List<string> openTags = new List<string>();

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '<')
            {
                continue;
            }

            if (!TryFindDelimitedEnd(text, index, '>', out int tagEnd))
            {
                error = $"Rich-text tag beginning at index {index} has no closing angle bracket.";
                return false;
            }

            string tagBody = text.Substring(index + 1, tagEnd - index - 1).Trim();
            bool closing = tagBody.StartsWith("/", StringComparison.Ordinal);
            bool selfClosing = tagBody.EndsWith("/", StringComparison.Ordinal);
            string tagName = GetTagName(tagBody, closing);

            if (PairedRichTextTags.Contains(tagName))
            {
                if (closing)
                {
                    if (openTags.Count == 0 ||
                        !string.Equals(
                            openTags[openTags.Count - 1],
                            tagName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Closing tag '</{tagName}>' does not match the active rich-text tag.";
                        return false;
                    }

                    openTags.RemoveAt(openTags.Count - 1);
                }
                else if (!selfClosing)
                {
                    openTags.Add(tagName);
                }
            }

            index = tagEnd;
        }

        if (openTags.Count > 0)
        {
            error = $"Rich-text tag '<{openTags[openTags.Count - 1]}>' is not closed.";
            return false;
        }

        return true;
    }

    public static string RemoveGeneratedWordJoiners(string text)
    {
        return string.IsNullOrEmpty(text)
            ? text ?? string.Empty
            : text.Replace(WordJoiner.ToString(), string.Empty);
    }

    private static void AppendJoinerIfNeeded(
        StringBuilder builder,
        bool hasVisibleElementInToken)
    {
        if (hasVisibleElementInToken)
        {
            builder.Append(WordJoiner);
        }
    }

    private static bool IsBreakableWhitespace(char character)
    {
        return char.IsWhiteSpace(character) && character != NonBreakingSpace;
    }

    private static bool TokenNeedsProtection(string text, int startIndex)
    {
        bool insideTag = false;

        for (int index = startIndex; index < text.Length; index++)
        {
            char character = text[index];

            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (character == '>')
                {
                    insideTag = false;
                }

                continue;
            }

            if (IsBreakableWhitespace(character))
            {
                break;
            }

            if (character == NonBreakingSpace || IsHangul(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHangul(char character)
    {
        return (character >= '\u1100' && character <= '\u11FF') ||
               (character >= '\u3130' && character <= '\u318F') ||
               (character >= '\uA960' && character <= '\uA97F') ||
               (character >= '\uAC00' && character <= '\uD7A3') ||
               (character >= '\uD7B0' && character <= '\uD7FF');
    }

    private static bool IsExplicitLineBreakTag(string tag)
    {
        return string.Equals(tag, "<br>", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, "<br/>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFindDelimitedEnd(
        string text,
        int startIndex,
        char delimiter,
        out int endIndex)
    {
        endIndex = text.IndexOf(delimiter, startIndex + 1);
        return endIndex >= 0;
    }

    private static string GetTagName(string tagBody, bool closing)
    {
        int start = closing ? 1 : 0;

        while (start < tagBody.Length && char.IsWhiteSpace(tagBody[start]))
        {
            start++;
        }

        int end = start;

        while (end < tagBody.Length &&
               !char.IsWhiteSpace(tagBody[end]) &&
               tagBody[end] != '=' &&
               tagBody[end] != '/')
        {
            end++;
        }

        return end > start
            ? tagBody.Substring(start, end - start)
            : string.Empty;
    }
}
