using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DialoguePresentationValidator
{
    public const string DialogueUiPrefabPath =
        "Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab";
    public const int ReferenceWidth = 480;
    public const int ReferenceHeight = 270;
    public const int MaximumTutorialSubtitleLines = 2;

    private const string TutorialKeyPrefix = "dialogue.tutorial.";

    public static void AppendPhase2CTutorialIssues(
        LocalizationValidationReport report,
        string sourceName)
    {
        if (report == null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DialogueUiPrefabPath);

        if (prefab == null)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MissingDialoguePresentationPrefab",
                sourceName,
                0,
                $"Dialogue UI prefab '{DialogueUiPrefabPath}' is required for TMP layout validation."));
            return;
        }

        AppendCinematicConfigurationIssues(prefab, sourceName, report);

        GameObject instance = null;

        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            RectTransform root = instance.GetComponent<RectTransform>();

            if (root != null)
            {
                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ReferenceWidth);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ReferenceHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }

            Canvas.ForceUpdateCanvases();
            DialogueSubtitleTypewriter typewriter =
                instance.GetComponentInChildren<DialogueSubtitleTypewriter>(true);
            TMP_Text subtitleText = typewriter != null
                ? typewriter.GetComponent<TMP_Text>()
                : null;

            if (subtitleText == null || subtitleText.rectTransform.rect.width <= 1f)
            {
                report.AddIssue(new LocalizationValidationIssue(
                    LocalizationValidationSeverity.Error,
                    "InvalidDialoguePresentationFixture",
                    sourceName,
                    0,
                    "The dialogue prefab does not expose a measurable TMP subtitle using " +
                    nameof(DialogueSubtitleTypewriter) + "."));
                return;
            }

            for (int recordIndex = 0; recordIndex < report.Records.Count; recordIndex++)
            {
                LocalizationCsvRecord record = report.Records[recordIndex];

                if (!record.TextKey.StartsWith(
                        TutorialKeyPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ValidateText(
                    record.Korean,
                    record.TextKey,
                    "ko",
                    sourceName,
                    record.SourceRow,
                    subtitleText,
                    report);
                ValidateOptionalText(
                    record.English,
                    record.TextKey,
                    "en",
                    sourceName,
                    record.SourceRow,
                    subtitleText,
                    report);
                ValidateOptionalText(
                    record.Japanese,
                    record.TextKey,
                    "ja",
                    sourceName,
                    record.SourceRow,
                    subtitleText,
                    report);
                ValidateOptionalText(
                    record.SimplifiedChinese,
                    record.TextKey,
                    "zh-Hans",
                    sourceName,
                    record.SourceRow,
                    subtitleText,
                    report);
                ValidateOptionalText(
                    record.TraditionalChinese,
                    record.TextKey,
                    "zh-Hant",
                    sourceName,
                    record.SourceRow,
                    subtitleText,
                    report);
            }
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }

    private static void AppendCinematicConfigurationIssues(
        GameObject prefab,
        string sourceName,
        LocalizationValidationReport report)
    {
        DialogueCinematicPresentationController presenter =
            prefab.GetComponent<DialogueCinematicPresentationController>();
        if (presenter == null)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MissingDialogueCinematicPresenter",
                sourceName,
                0,
                $"'{DialogueUiPrefabPath}' must extend its existing Pixel Crushers UI with " +
                nameof(DialogueCinematicPresentationController) + "."));
            return;
        }

        if (!presenter.UsesUnscaledTransitions || presenter.TakesCameraOwnership)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "InvalidDialoguePresentationOwnership",
                sourceName,
                0,
                "Dialogue presentation must use unscaled UI transitions and must not acquire camera ownership."));
        }

        if (presenter.CinematicTopBarHeight < 12f ||
            presenter.CinematicTopBarHeight > 16f ||
            presenter.CinematicPanelHeight < 68f ||
            presenter.CinematicPanelHeight > 76f ||
            presenter.PortraitAreaWidth < 40f ||
            presenter.PortraitAreaWidth > 48f)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "InvalidDialogueReferenceLayout",
                sourceName,
                0,
                "The 480x270 communication prefab must retain a 12-16px top bar, " +
                "68-76px cinematic panel, and 40-48px portrait area."));
        }

        DialogueSubtitleTypewriter[] typewriters =
            prefab.GetComponentsInChildren<DialogueSubtitleTypewriter>(true);
        bool hasCompleteActorVoiceProfile = false;

        for (int index = 0; index < typewriters.Length; index++)
        {
            DialogueSubtitleTypewriter typewriter = typewriters[index];
            if (typewriter.audioClip != null &&
                typewriter.CurseVoiceClip != null &&
                typewriter.SettlementVoiceClip != null)
            {
                hasCompleteActorVoiceProfile = true;
                break;
            }
        }

        if (!hasCompleteActorVoiceProfile)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "MissingDialogueActorVoiceProfiles",
                sourceName,
                0,
                "The communication subtitle must reference Operator, Curse, and Settlement text voices."));
        }
    }

    public static string ResolveReferenceBindings(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return text
            .Replace(
                $"[var={Phase2CStoryDialogueIds.RadarToggleLuaVariable}]",
                "Q")
            .Replace(
                $"[var={Phase2CStoryDialogueIds.RadarScanLuaVariable}]",
                DialogueWordWrapUtility.ProtectBindingDisplayString("Mouse 4"));
    }

    public static void AppendSyntaxIssues(
        string text,
        string textKey,
        string languageCode,
        string sourceName,
        int sourceRow,
        LocalizationValidationReport report)
    {
        if (report == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        string resolved = ResolveReferenceBindings(text);

        if (text.IndexOf(DialogueWordWrapUtility.WordJoiner) >= 0)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "GeneratedDialogueFormattingInSource",
                sourceName,
                sourceRow,
                $"Tutorial TextKey '{textKey}' ({languageCode}) contains a generated word joiner. " +
                "Keep wrapping control in the runtime presentation layer."));
        }

        if (DialogueWordWrapUtility.ContainsUnresolvedInputBinding(resolved))
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "UnresolvedInputBindingPlaceholder",
                sourceName,
                sourceRow,
                $"Tutorial TextKey '{textKey}' ({languageCode}) contains an unknown input-binding variable."));
        }

        string wrapped = DialogueWordWrapUtility.ApplyWordSafeWrapping(resolved);

        if (!DialogueWordWrapUtility.TryValidateBalancedRichTextTags(
                wrapped,
                out string richTextError))
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "UnbalancedDialogueRichText",
                sourceName,
                sourceRow,
                $"Tutorial TextKey '{textKey}' ({languageCode}): {richTextError}"));
        }
    }

    private static void ValidateOptionalText(
        string text,
        string textKey,
        string languageCode,
        string sourceName,
        int sourceRow,
        TMP_Text subtitleText,
        LocalizationValidationReport report)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            ValidateText(
                text,
                textKey,
                languageCode,
                sourceName,
                sourceRow,
                subtitleText,
                report);
        }
    }

    private static void ValidateText(
        string text,
        string textKey,
        string languageCode,
        string sourceName,
        int sourceRow,
        TMP_Text subtitleText,
        LocalizationValidationReport report)
    {
        AppendSyntaxIssues(
            text,
            textKey,
            languageCode,
            sourceName,
            sourceRow,
            report);

        string resolved = ResolveReferenceBindings(text);

        if (DialogueWordWrapUtility.ContainsUnresolvedInputBinding(resolved))
        {
            return;
        }

        string wrapped = DialogueWordWrapUtility.ApplyWordSafeWrapping(resolved);
        subtitleText.text = wrapped;
        subtitleText.maxVisibleCharacters = int.MaxValue;
        subtitleText.ForceMeshUpdate(true, true);

        if (subtitleText.textInfo.lineCount > MaximumTutorialSubtitleLines)
        {
            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "TutorialSubtitleExceedsTwoLines",
                sourceName,
                sourceRow,
                $"Tutorial TextKey '{textKey}' ({languageCode}) renders as " +
                $"{subtitleText.textInfo.lineCount} lines at {ReferenceWidth}x{ReferenceHeight}. " +
                "Shorten or semantically split the Dialogue Entry."));
        }

        float availableWidth = subtitleText.rectTransform.rect.width;
        List<string> tokens = ExtractVisibleTokens(resolved);

        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            string token = tokens[tokenIndex];
            Vector2 preferred = subtitleText.GetPreferredValues(
                DialogueWordWrapUtility.ApplyWordSafeWrapping(token),
                10000f,
                10000f);

            if (preferred.x <= availableWidth + 0.01f)
            {
                continue;
            }

            report.AddIssue(new LocalizationValidationIssue(
                LocalizationValidationSeverity.Error,
                "DialogueTokenExceedsSubtitleWidth",
                sourceName,
                sourceRow,
                $"Tutorial TextKey '{textKey}' ({languageCode}) contains token '{token}' " +
                $"with preferred width {preferred.x:0.##}, exceeding subtitle width " +
                $"{availableWidth:0.##}."));
        }
    }

    private static List<string> ExtractVisibleTokens(string text)
    {
        List<string> tokens = new List<string>();
        System.Text.StringBuilder token = new System.Text.StringBuilder();
        bool insideTag = false;

        for (int index = 0; index < text.Length; index++)
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

            bool breakableWhitespace = char.IsWhiteSpace(character) &&
                                       character != DialogueWordWrapUtility.NonBreakingSpace;

            if (breakableWhitespace)
            {
                AddToken(tokens, token);
            }
            else
            {
                token.Append(character);
            }
        }

        AddToken(tokens, token);
        return tokens;
    }

    private static void AddToken(
        List<string> tokens,
        System.Text.StringBuilder token)
    {
        if (token.Length == 0)
        {
            return;
        }

        tokens.Add(token.ToString());
        token.Clear();
    }
}
