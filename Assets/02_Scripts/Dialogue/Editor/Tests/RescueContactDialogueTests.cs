using System;
using System.Collections.Generic;
using NUnit.Framework;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RescueContactDialogueTests
{
    private sealed class FakeRescueContactAuthority :
        IRescueContactDialogueServiceAuthority
    {
        public bool Available = true;
        public bool ExecuteResult = true;
        public int AvailabilityReadCount;
        public int ExecuteCount;

        public bool IsRescueContactDialogueServiceAvailable
        {
            get
            {
                AvailabilityReadCount++;
                return Available;
            }
        }

        public bool TryExecuteRescueContactDialogueService(GameObject interactor)
        {
            ExecuteCount++;
            return ExecuteResult;
        }
    }

    [Test]
    public void ConditionEvaluation_IsSideEffectFree()
    {
        DialogueConditionRegistry registry = new DialogueConditionRegistry();
        FakeRescueContactAuthority authority = new FakeRescueContactAuthority();

        bool recognized = registry.TryEvaluate(
            DialogueConditionId.RescueContactAvailable,
            authority,
            out bool result);

        Assert.That(recognized, Is.True);
        Assert.That(result, Is.True);
        Assert.That(authority.AvailabilityReadCount, Is.EqualTo(1));
        Assert.That(authority.ExecuteCount, Is.Zero);
    }

    [Test]
    public void Accept_DispatchesExactlyOncePerConversation()
    {
        DialogueGameplayActionDispatcher dispatcher = CreateActiveDispatcher();
        FakeRescueContactAuthority authority = new FakeRescueContactAuthority();

        bool first = dispatcher.TryDispatch(
            DialogueGameplayActionId.RescueContactAccept,
            authority,
            null);
        LogAssert.Expect(
            LogType.Warning,
            new System.Text.RegularExpressions.Regex("Duplicate action 'RescueContactAccept'"));
        bool duplicate = dispatcher.TryDispatch(
            DialogueGameplayActionId.RescueContactAccept,
            authority,
            null);

        Assert.That(first, Is.True);
        Assert.That(duplicate, Is.False);
        Assert.That(authority.ExecuteCount, Is.EqualTo(1));
    }

    [Test]
    public void DeclineGraph_HasNoGameplayAction()
    {
        LoadContent(out DialogueDatabase database, out LocalizationCatalog catalog);

        bool built = RescueContactDialogueInstaller.TryBuildEntries(
            database,
            catalog,
            out List<DialogueEntry> entries,
            out string result);

        Assert.That(built, Is.True, result);
        Assert.That(entries[4].userScript, Is.Null.Or.Empty);
    }

    [Test]
    public void UnavailableService_GrantsNothing()
    {
        DialogueGameplayActionDispatcher dispatcher = CreateActiveDispatcher();
        FakeRescueContactAuthority authority = new FakeRescueContactAuthority
        {
            Available = false
        };

        bool dispatched = dispatcher.TryDispatch(
            DialogueGameplayActionId.RescueContactAccept,
            authority,
            null);

        Assert.That(dispatched, Is.False);
        Assert.That(authority.ExecuteCount, Is.Zero);
    }

    [Test]
    public void UnknownConditionId_FailsSafelyWithDiagnostic()
    {
        DialogueConditionRegistry registry = new DialogueConditionRegistry();
        FakeRescueContactAuthority authority = new FakeRescueContactAuthority();
        LogAssert.Expect(
            LogType.Warning,
            new System.Text.RegularExpressions.Regex("Unknown condition ID 'NotRegistered'"));

        bool recognized = registry.TryEvaluate(
            "NotRegistered",
            authority,
            out bool result);

        Assert.That(recognized, Is.False);
        Assert.That(result, Is.False);
        Assert.That(authority.ExecuteCount, Is.Zero);
    }

    [Test]
    public void UnknownActionId_FailsSafelyWithDiagnostic()
    {
        DialogueGameplayActionDispatcher dispatcher = CreateActiveDispatcher();
        FakeRescueContactAuthority authority = new FakeRescueContactAuthority();
        LogAssert.Expect(
            LogType.Warning,
            new System.Text.RegularExpressions.Regex("Unknown action ID 'NotRegistered'"));

        bool dispatched = dispatcher.TryDispatch("NotRegistered", authority, null);

        Assert.That(dispatched, Is.False);
        Assert.That(authority.ExecuteCount, Is.Zero);
    }

    [Test]
    public void RescueContactLocalizationKeys_ExistWithKoreanSource()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            RescueContactDialogueInstaller.LocalizationCatalogAssetPath);
        string[] keys =
        {
            RescueContactDialogueIds.SpeakerNameTextKey,
            RescueContactDialogueIds.Line01TextKey,
            RescueContactDialogueIds.Line02TextKey,
            RescueContactDialogueIds.AcceptChoiceTextKey,
            RescueContactDialogueIds.DeclineChoiceTextKey,
            RescueContactDialogueIds.UnavailableTextKey
        };

        Assert.That(catalog, Is.Not.Null);

        for (int i = 0; i < keys.Length; i++)
        {
            Assert.That(catalog.TryGetEntry(keys[i], out LocalizationEntry entry), Is.True, keys[i]);
            Assert.That(entry.Korean, Is.Not.Null.And.Not.Empty, keys[i]);
        }
    }

    [Test]
    public void ProductionLocalizationCsv_IsValidAndCatalogIsCurrent()
    {
        bool read = LocalizationContentImporter.TryReadUtf8File(
            LocalizationContentImporter.DefaultSourceAssetPath,
            out string csvContent,
            out string readError);

        Assert.That(read, Is.True, readError);

        LocalizationValidationReport report = LocalizationContentValidator.ValidateCsv(
            LocalizationContentImporter.DefaultSourceAssetPath,
            csvContent);

        Assert.That(report.HasErrors, Is.False, BuildIssueSummary(report));

        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);
        LocalizationValidationIssue freshness =
            LocalizationContentValidator.ValidateCatalogFreshness(
                catalog,
                LocalizationContentImporter.DefaultSourceAssetPath,
                csvContent);

        Assert.That(freshness, Is.Null, freshness != null ? freshness.ToString() : string.Empty);
    }

    [Test]
    public void RescueContactGraph_UsesTypedConditionAndActionIds()
    {
        LoadContent(out DialogueDatabase database, out LocalizationCatalog catalog);

        bool built = RescueContactDialogueInstaller.TryBuildEntries(
            database,
            catalog,
            out List<DialogueEntry> entries,
            out string result);

        Assert.That(built, Is.True, result);
        Assert.That(
            entries[3].conditionsString,
            Does.Contain(DialogueConditionId.RescueContactAvailable.ToString()));
        Assert.That(
            entries[3].userScript,
            Does.Contain(DialogueGameplayActionId.RescueContactAccept.ToString()));
        Assert.That(entries[5].conditionsString, Does.StartWith("not "));
    }

    private static DialogueGameplayActionDispatcher CreateActiveDispatcher()
    {
        DialogueGameplayActionDispatcher dispatcher =
            new DialogueGameplayActionDispatcher();
        dispatcher.BeginConversation(RescueContactDialogueIds.ConversationTitle);
        return dispatcher;
    }

    private static void LoadContent(
        out DialogueDatabase database,
        out LocalizationCatalog catalog)
    {
        database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(
            RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            RescueContactDialogueInstaller.LocalizationCatalogAssetPath);
        Assert.That(database, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);
    }

    private static string BuildIssueSummary(LocalizationValidationReport report)
    {
        if (report == null || report.Issues.Count == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < report.Issues.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(report.Issues[i]);
        }

        return builder.ToString();
    }
}
