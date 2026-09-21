using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LocalizationFoundationTests
{
    private readonly List<UnityEngine.Object> createdObjects =
        new List<UnityEngine.Object>();
    private readonly LocalizationEditModeLifecycle localizationLifecycle =
        new LocalizationEditModeLifecycle();

    private string originalLanguage;
    private VoidScrapperLocalizationService previousLocalizationService;

    [SetUp]
    public void SetUp()
    {
        previousLocalizationService =
            LocalizationServiceTestIsolation.DetachActiveInstance();
        originalLanguage = GameSettingsRuntime.LanguageCode;
        GameSettingsRuntime.SetLanguageCode(LocalizationLanguageCodes.Korean);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            localizationLifecycle.ShutdownAll();
        }
        finally
        {
            try
            {
                for (int i = createdObjects.Count - 1; i >= 0; i--)
                {
                    if (createdObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                    }
                }
            }
            finally
            {
                createdObjects.Clear();
                GameSettingsRuntime.SetLanguageCode(originalLanguage);
                LocalizationServiceTestIsolation.RestoreActiveInstance(
                    previousLocalizationService);
                previousLocalizationService = null;
            }
        }
    }

    [Test]
    public void Catalog_KoreanDirectLookup_ReturnsKorean()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());

        bool found = catalog.TryGetText(
            "test.greeting",
            LocalizationLanguageCodes.Korean,
            out string text,
            out bool usedFallback);

        Assert.That(found, Is.True);
        Assert.That(text, Is.EqualTo("테스트 안녕"));
        Assert.That(usedFallback, Is.False);
    }

    [Test]
    public void Catalog_EnglishDirectLookup_ReturnsEnglishTestData()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());

        bool found = catalog.TryGetText(
            "test.greeting",
            LocalizationLanguageCodes.English,
            out string text,
            out bool usedFallback);

        Assert.That(found, Is.True);
        Assert.That(text, Is.EqualTo("TEST HELLO"));
        Assert.That(usedFallback, Is.False);
    }

    [Test]
    public void Catalog_MissingEnglish_FallsBackToKorean()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());

        bool found = catalog.TryGetText(
            "test.korean_only",
            LocalizationLanguageCodes.English,
            out string text,
            out bool usedFallback);

        Assert.That(found, Is.True);
        Assert.That(text, Is.EqualTo("한국어 전용 테스트"));
        Assert.That(usedFallback, Is.True);
    }

    [Test]
    public void Catalog_UnsupportedLanguage_FallsBackToKorean()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());

        bool found = catalog.TryGetText(
            "test.greeting",
            "fr",
            out string text,
            out bool usedFallback);

        Assert.That(found, Is.True);
        Assert.That(text, Is.EqualTo("테스트 안녕"));
        Assert.That(usedFallback, Is.False);
    }

    [Test]
    public void Service_UnknownKey_ReturnsDevelopmentMarker()
    {
        VoidScrapperLocalizationService service = CreateService(
            CreateCatalog(CreateTestEntries()));
        LogAssert.Expect(
            LogType.Warning,
            new Regex("Missing TextKey 'missing\\.key'"));

        string text = service.GetText("missing.key");

        Assert.That(text, Is.EqualTo("⟦missing:missing.key⟧"));
    }

    [Test]
    public void NamedPlaceholders_FormatNamedValues()
    {
        Dictionary<string, string> arguments = new Dictionary<string, string>
        {
            { "amount", "7" },
            { "item", "TEST ITEM" }
        };

        bool success = NamedPlaceholderUtility.TryFormat(
            "{item}: {amount}",
            arguments,
            out string result,
            out string missingArgument);

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo("TEST ITEM: 7"));
        Assert.That(missingArgument, Is.Empty);
    }

    [Test]
    public void ProductionCsv_AllKoreanSourcesUseValidNamedPlaceholders()
    {
        const string path = "Assets/02_Scripts/Config/Localization/Source/Localization.csv";
        string csv = File.ReadAllText(path);
        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(path, csv);
        Assert.That(report.Records.Count, Is.GreaterThan(0));
        foreach (LocalizationValidationIssue issue in report.Issues)
        {
            Assert.That(issue.Severity, Is.Not.EqualTo(LocalizationValidationSeverity.Error), issue.ToString());
        }
        var placeholders = new HashSet<string>();
        foreach (LocalizationCsvRecord record in report.Records)
        {
            Assert.That(NamedPlaceholderUtility.TryCollectPlaceholders(record.Korean, placeholders,
                out string error), Is.True, record.TextKey + ": " + error);
        }
        Assert.That(Regex.IsMatch(csv, @"\{\d+[^}]*\}"), Is.False);
        foreach (string runtimePath in new[]
        {
            "Assets/02_Scripts/Campaign/SettlementDefenseEncounterController.cs",
            "Assets/02_Scripts/Campaign/SettlementDefenseCorruptedCore.cs"
        })
        {
            string source = File.ReadAllText(runtimePath);
            Assert.That(Regex.IsMatch(source, @"\{\d+[^}]*\}"), Is.False, runtimePath);
            Assert.That(source, Does.Not.Contain("string.Format("), runtimePath);
        }
    }

    [TestCase("defense.core.combat", "phase", "2", "오염 코어 격리 2 / 3")]
    [TestCase("defense.core.fused", "count", "1", "코어 안정화 1 / 3")]
    [TestCase("defense.core.reactivate", "partName", "구획 안정기", "구획 안정기 재활성화")]
    public void CorruptedDefense_NamedArgumentsFormatAndMissingArgumentsPreserveTemplate(
        string key, string argumentName, string value, string expected)
    {
        const string path = "Assets/02_Scripts/Config/Localization/Source/Localization.csv";
        var report = LocalizationContentValidator.ValidateCsv(path, File.ReadAllText(path));
        LocalizationCsvRecord record = null;
        foreach (var candidate in report.Records)
        {
            if (candidate.TextKey == key)
            {
                record = candidate;
                break;
            }
        }
        Assert.That(record, Is.Not.Null);
        var placeholders = new HashSet<string>();
        foreach (string template in new[] { record.Korean, record.English })
        {
            Assert.That(NamedPlaceholderUtility.TryCollectPlaceholders(template, placeholders, out _), Is.True);
            Assert.That(placeholders, Is.EquivalentTo(new[] { argumentName }));
        }
        var service = CreateService(CreateCatalog(new[] { record.ToCatalogEntry() }));
        Assert.That(service.FormatText(key, new Dictionary<string, string> { { argumentName, value } }),
            Is.EqualTo(expected));
        LogAssert.Expect(LogType.Warning, new Regex("requires placeholder argument '" + argumentName + "'"));
        Assert.That(service.FormatText(key, new Dictionary<string, string>()), Is.EqualTo(record.Korean));
    }

    [TestCase(BossStoryPart.SectorStabilizer, "구획 안정기 재활성화")]
    [TestCase(BossStoryPart.PhaseNavigationLens, "위상 항법 렌즈 재활성화")]
    [TestCase(BossStoryPart.MatterCompressor, "물질 압축로 재활성화")]
    public void CorruptedDefense_InteractionFallbackFormatsCurrentPart(BossStoryPart part, string expected)
    {
        var root = new GameObject("Inactive core text fixture");
        root.SetActive(false);
        createdObjects.Add(root);
        var core = root.AddComponent<SettlementDefenseCorruptedCore>();
        typeof(SettlementDefenseCorruptedCore).GetProperty(nameof(core.Part)).SetValue(core, part);
        Assert.That(core.InteractionText, Is.EqualTo(expected));
    }

    [Test]
    public void NamedPlaceholders_LevelAndValidIdentifierFormsRemainSupported()
    {
        const string template = "{level} {partName} {core_2}";
        var names = new HashSet<string>();
        Assert.That(NamedPlaceholderUtility.TryCollectPlaceholders(template, names, out _), Is.True);
        Assert.That(NamedPlaceholderUtility.TryFormat(template, new Dictionary<string, string>
        {
            { "level", "3" }, { "partName", "Core" }, { "core_2", "Ready" }
        }, out string result, out _), Is.True);
        Assert.That(result, Is.EqualTo("3 Core Ready"));
        Assert.That(NamedPlaceholderUtility.TryCollectPlaceholders("{0}", names, out _), Is.False);
    }

    [Test]
    public void NamedPlaceholders_MissingArgument_PreservesTemplate()
    {
        bool success = NamedPlaceholderUtility.TryFormat(
            "Count {count}/{required}",
            new Dictionary<string, string> { { "count", "1" } },
            out string result,
            out string missingArgument);

        Assert.That(success, Is.False);
        Assert.That(result, Is.EqualTo("Count {count}/{required}"));
        Assert.That(missingArgument, Is.EqualTo("required"));
    }

    [Test]
    public void NamedPlaceholders_ExtraArgument_IsIgnored()
    {
        bool success = NamedPlaceholderUtility.TryFormat(
            "Amount {amount}",
            new Dictionary<string, string>
            {
                { "amount", "4" },
                { "unused", "TEST" }
            },
            out string result,
            out string missingArgument);

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo("Amount 4"));
        Assert.That(missingArgument, Is.Empty);
    }

    [Test]
    public void Validator_PlaceholderMismatch_IsError()
    {
        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            "test.csv",
            BuildCsvRow("test.key", "{count} 테스트", "TEST {amount}"));

        AssertIssue(report, "PlaceholderMismatch");
    }

    [Test]
    public void Validator_DuplicateKey_IsError()
    {
        string csv = Header +
                     "test.key,하나,,,,,테스트,,10\n" +
                     "test.key,둘,,,,,테스트,,10\n";

        LocalizationValidationReport report =
            LocalizationContentValidator.ValidateCsv("test.csv", csv);

        AssertIssue(report, "DuplicateTextKey");
    }

    [Test]
    public void Validator_EmptyKorean_IsError()
    {
        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            "test.csv",
            BuildCsvRow("test.key", string.Empty, "TEST"));

        AssertIssue(report, "EmptyKoreanSource");
    }

    [Test]
    public void CsvParser_QuotedComma_PreservesField()
    {
        LocalizationCsvParseResult result = LocalizationCsvParser.Parse(
            "test.csv",
            Header + "test.key,\"테스트, 쉼표\",,,,,context,,10\n");

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Document.Rows[1].Fields[1], Is.EqualTo("테스트, 쉼표"));
    }

    [Test]
    public void CsvParser_QuotedMultiline_PreservesNewline()
    {
        LocalizationCsvParseResult result = LocalizationCsvParser.Parse(
            "test.csv",
            Header + "test.key,\"첫 줄\n둘째 줄\",,,,,context,,10\n");

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Document.Rows[1].Fields[1], Is.EqualTo("첫 줄\n둘째 줄"));
        Assert.That(result.Document.Rows[1].SourceRow, Is.EqualTo(2));
    }

    [Test]
    public void Validator_TutorialDialogueEmbeddedNewline_IsError()
    {
        string csv = Header +
                     "dialogue.tutorial.test.line_01,\"첫 줄\n둘째 줄\",,,,,context,,40\n";

        LocalizationValidationReport report =
            LocalizationContentValidator.ValidateCsv("test.csv", csv);

        AssertIssue(report, "TutorialDialogueEmbeddedNewline");
    }

    [Test]
    public void Validator_RowOrder_DoesNotChangeDeterministicHash()
    {
        string first = Header +
                       "test.a,가,,,,,context,,10\n" +
                       "test.b,나,,,,,context,,10\n";
        string second = Header +
                        "test.b,나,,,,,context,,10\n" +
                        "test.a,가,,,,,context,,10\n";

        LocalizationValidationReport firstReport =
            LocalizationContentValidator.ValidateCsv("first.csv", first);
        LocalizationValidationReport secondReport =
            LocalizationContentValidator.ValidateCsv("second.csv", second);

        Assert.That(firstReport.HasErrors, Is.False);
        Assert.That(secondReport.HasErrors, Is.False);
        Assert.That(secondReport.ContentHash, Is.EqualTo(firstReport.ContentHash));
    }

    [Test]
    public void Service_LanguageChanged_RaisesOnceForChange()
    {
        VoidScrapperLocalizationService service = CreateService(
            CreateCatalog(CreateTestEntries()));
        int eventCount = 0;
        string eventLanguage = null;
        service.LanguageChanged += language =>
        {
            eventCount++;
            eventLanguage = language;
        };

        service.SetLanguage(LocalizationLanguageCodes.English);

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(eventLanguage, Is.EqualTo(LocalizationLanguageCodes.English));
    }

    [Test]
    public void Presenter_DisabledPresenter_UnsubscribesFromLanguageChanges()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());
        VoidScrapperLocalizationService service = CreateService(catalog);
        GameObject labelObject = new GameObject("Localized label test");
        labelObject.SetActive(false);
        createdObjects.Add(labelObject);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        LocalizedTextPresenter presenter = labelObject.AddComponent<LocalizedTextPresenter>();
        presenter.ConfigureForEditorAndTests(service, label, "test.greeting");
        labelObject.SetActive(true);
        localizationLifecycle.InitializePresenter(presenter);

        Assert.That(label.text, Is.EqualTo("테스트 안녕"));

        presenter.enabled = false;
        localizationLifecycle.DisablePresenter(presenter);
        service.SetLanguage(LocalizationLanguageCodes.English);

        Assert.That(label.text, Is.EqualTo("테스트 안녕"));
    }

    [Test]
    public void Presenter_EnabledPresenter_RefreshesOnLanguageChange()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());
        VoidScrapperLocalizationService service = CreateService(catalog);
        GameObject labelObject = new GameObject("Localized label refresh test");
        labelObject.SetActive(false);
        createdObjects.Add(labelObject);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        LocalizedTextPresenter presenter = labelObject.AddComponent<LocalizedTextPresenter>();
        presenter.ConfigureForEditorAndTests(service, label, "test.greeting");
        labelObject.SetActive(true);
        localizationLifecycle.InitializePresenter(presenter);

        service.SetLanguage(LocalizationLanguageCodes.English);

        Assert.That(label.text, Is.EqualTo("TEST HELLO"));
    }

    [Test]
    public void Validator_UnknownFixtureKey_IsError()
    {
        LocalizationCatalog catalog = CreateCatalog(CreateTestEntries());
        IReadOnlyList<LocalizationValidationIssue> issues =
            LocalizationContentValidator.ValidateReferencedKeys(
                catalog,
                new[]
                {
                    new LocalizationKeyReference("test.missing", "Phase2A fixture")
                });

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Code, Is.EqualTo("UnknownReferencedTextKey"));
    }

    private const string Header =
        "TextKey,ko,en,ja,zh-Hans,zh-Hant,Context,Notes,MaxLengthHint\n";

    private static string BuildCsvRow(string key, string korean, string english)
    {
        return Header + $"{key},{korean},{english},,,,test context,,20\n";
    }

    private LocalizationCatalog CreateCatalog(IReadOnlyList<LocalizationEntry> entries)
    {
        LocalizationCatalog catalog = ScriptableObject.CreateInstance<LocalizationCatalog>();
        catalog.SetGeneratedContentForEditor(entries, "test.csv", "test-hash");
        createdObjects.Add(catalog);
        return catalog;
    }

    private VoidScrapperLocalizationService CreateService(LocalizationCatalog catalog)
    {
        GameObject serviceObject = new GameObject("Localization service test");
        serviceObject.SetActive(false);
        createdObjects.Add(serviceObject);
        VoidScrapperLocalizationService service =
            serviceObject.AddComponent<VoidScrapperLocalizationService>();
        service.ConfigureForEditorAndTests(catalog);
        serviceObject.SetActive(true);
        Assert.That(localizationLifecycle.InitializeService(service), Is.True);
        Assert.That(
            VoidScrapperLocalizationService.Instance,
            Is.SameAs(service),
            "The isolated fixture service must own the localization singleton.");
        return service;
    }

    private static IReadOnlyList<LocalizationEntry> CreateTestEntries()
    {
        return new[]
        {
            new LocalizationEntry(
                "test.greeting",
                "테스트 안녕",
                "TEST HELLO",
                string.Empty,
                string.Empty,
                string.Empty,
                "Test-only greeting.",
                "Not production translation data.",
                20),
            new LocalizationEntry(
                "test.korean_only",
                "한국어 전용 테스트",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "Test-only fallback entry.",
                "Not production translation data.",
                24)
        };
    }

    private static void AssertIssue(LocalizationValidationReport report, string code)
    {
        for (int i = 0; i < report.Issues.Count; i++)
        {
            if (string.Equals(report.Issues[i].Code, code, StringComparison.Ordinal))
            {
                Assert.That(
                    report.Issues[i].Severity,
                    Is.EqualTo(LocalizationValidationSeverity.Error));
                return;
            }
        }

        Assert.Fail($"Expected validation issue '{code}'.");
    }
}
