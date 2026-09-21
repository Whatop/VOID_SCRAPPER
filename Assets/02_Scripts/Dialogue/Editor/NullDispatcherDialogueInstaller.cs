using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;

// One deterministic graph on the existing database, using the existing localization source.
public static class NullDispatcherDialogueInstaller
{
    public static readonly string[] TextKeys =
    {
        NullDispatcherDialogueIds.SpeakerKey,
        "final.treatment.access", "final.treatment.curse", "final.treatment.duplicate",
        "final.treatment.attachment", "final.treatment.components", "final.treatment.offer",
        "final.treatment.accept", "final.treatment.reject"
    };

    [MenuItem("VOID SCRAPPER/Dialogue/Install NULL DISPATCHER Treatment")]
    public static void InstallFromMenu()
    {
        LoadAssets(out var database, out var catalog);
        if (TryInstall(database, catalog, true, out string result)) Debug.Log(result, database);
        else Debug.LogError(result);
    }

    [MenuItem("VOID SCRAPPER/Dialogue/Validate NULL DISPATCHER Treatment")]
    public static void ValidateFromMenu()
    {
        LoadAssets(out var database, out var catalog);
        if (TryValidateInstalled(database, catalog, out string result)) Debug.Log(result, database);
        else Debug.LogError(result);
    }

    private static void LoadAssets(out DialogueDatabase database, out LocalizationCatalog catalog)
    {
        database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
    }

    private static bool ValidateSource(DialogueDatabase database, LocalizationCatalog catalog, out string result)
    {
        result = "Import/validate Localization and install the existing story foundation first.";
        if (database == null || catalog == null ||
            database.GetConversation(Phase2CStoryDialogueIds.FirstSettlementConversation) == null) return false;
        if (!LocalizationContentImporter.TryReadUtf8File(LocalizationContentImporter.DefaultSourceAssetPath,
            out string csv, out result)) return false;
        var report = LocalizationContentValidator.ValidateCsv(LocalizationContentImporter.DefaultSourceAssetPath, csv);
        if (report.HasErrors || LocalizationContentValidator.ValidateCatalogFreshness(catalog,
            LocalizationContentImporter.DefaultSourceAssetPath, csv) != null)
        {
            result = "Localization source is invalid or catalog is stale. Import/validate Localization first.";
            return false;
        }
        foreach (string key in TextKeys)
        {
            if (!catalog.TryGetEntry(key, out var entry) || string.IsNullOrWhiteSpace(entry.Korean))
            {
                result = "Missing localization key: " + key;
                return false;
            }
        }
        int actorCount = 0, conversationCount = 0;
        foreach (var actor in database.actors) if (actor.Name == NullDispatcherDialogueIds.Actor) actorCount++;
        foreach (var conversation in database.conversations)
            if (conversation.Title == NullDispatcherDialogueIds.Conversation) conversationCount++;
        if (actorCount > 1 || conversationCount > 1)
        {
            result = "Duplicate NULL DISPATCHER actor/conversation must be resolved before installation.";
            return false;
        }
        return true;
    }

    public static bool TryInstall(DialogueDatabase database, LocalizationCatalog catalog, bool saveAssets, out string result)
    {
        if (!ValidateSource(database, catalog, out result)) return false;
        var template = Template.FromDefault();
        int playerId = database.GetConversation(Phase2CStoryDialogueIds.FirstSettlementConversation).ActorID;
        if (database.GetActor(playerId) == null)
        {
            result = "The existing story player actor is missing.";
            return false;
        }
        if (saveAssets) Undo.RecordObject(database, "Install NULL DISPATCHER treatment");
        Actor speaker = database.GetActor(NullDispatcherDialogueIds.Actor);
        if (speaker == null)
        {
            int id = 1;
            foreach (var actor in database.actors) id = Math.Max(id, actor.id + 1);
            speaker = template.CreateActor(id, NullDispatcherDialogueIds.Actor, false);
            database.actors.Add(speaker);
        }
        catalog.TryGetEntry(TextKeys[0], out var name);
        Field.SetValue(speaker.fields, "Name TextKey", TextKeys[0]);
        Field.SetValue(speaker.fields, "Display Name", name.Korean);
        Field.SetValue(speaker.fields, "Display Name en", name.English, FieldType.Localization);
        Conversation graph = database.GetConversation(NullDispatcherDialogueIds.Conversation);
        if (graph == null)
        {
            int id = 1;
            foreach (var conversation in database.conversations) id = Math.Max(id, conversation.id + 1);
            graph = template.CreateConversation(id, NullDispatcherDialogueIds.Conversation);
            database.AddConversation(graph);
        }
        graph.ActorID = playerId;
        graph.ConversantID = speaker.id;
        Field.SetValue(graph.fields, "Pause Gameplay", true);
        graph.dialogueEntries = BuildGraph(graph.id, playerId, speaker.id, catalog);
        if (!TryValidateInstalled(database, catalog, out result)) return false;
        if (saveAssets)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
        result = "Installed NULL DISPATCHER treatment: six lines, two responses, one natural-completion terminal.";
        return true;
    }

    private static List<DialogueEntry> BuildGraph(int conversationId, int player, int speaker, LocalizationCatalog catalog)
    {
        var entries = new List<DialogueEntry>(10);
        for (int id = 0; id <= 9; id++)
        {
            bool response = id == 7 || id == 8;
            var entry = new DialogueEntry
            {
                id = id, conversationID = conversationId, isRoot = id == 0,
                falseConditionAction = "Block", conditionPriority = ConditionPriority.Normal,
                fields = new List<Field>(), canvasRect = new Rect(response ? 220 + (id - 7) * 210 : 320, id * 65, 190, 40)
            };
            Field.SetValue(entry.fields, "Actor", (response ? player : speaker).ToString(), FieldType.Actor);
            Field.SetValue(entry.fields, "Conversant", (response ? speaker : player).ToString(), FieldType.Actor);
            Field.SetValue(entry.fields, "Title", id == 0 ? "START" : id == 9 ? "END" : string.Empty);
            Field.SetValue(entry.fields, "Sequence", id == 0 || id == 9 ? "None()" : string.Empty);
            if (id > 0 && id < 9)
            {
                catalog.TryGetEntry(TextKeys[id], out var text);
                Field.SetValue(entry.fields, "TextKey", text.TextKey);
                Field.SetValue(entry.fields, "Dialogue Text", text.Korean);
                Field.SetValue(entry.fields, "en", text.English, FieldType.Localization);
                if (response)
                    entry.userScript = ActionScript(id == 7);
            }
            if (id == 9) entry.userScript = CompletionScript();
            entries.Add(entry);
        }
        for (int id = 0; id < 6; id++) AddLink(entries[id], id + 1);
        AddLink(entries[6], 7);
        AddLink(entries[6], 8);
        AddLink(entries[7], 9);
        AddLink(entries[8], 9);
        return entries;
    }

    private static string ActionScript(bool accept) =>
        $"{DialoguePixelCrushersBridge.ActionLuaFunction}(\"{(accept ? DialogueGameplayActionId.FinalBossTreatmentAccept : DialogueGameplayActionId.FinalBossTreatmentReject)}\")";
    private static string CompletionScript() =>
        $"{DialoguePixelCrushersBridge.CompletionLuaFunction}(\"{NullDispatcherDialogueIds.Conversation}\")";

    private static void AddLink(DialogueEntry entry, int target)
    {
        entry.outgoingLinks.Add(new Link(entry.conversationID, entry.id, entry.conversationID, target)
        { priority = ConditionPriority.Normal });
    }

    public static bool TryValidateInstalled(DialogueDatabase database, LocalizationCatalog catalog, out string result)
    {
        if (!ValidateSource(database, catalog, out result)) return false;
        var graph = database.GetConversation(NullDispatcherDialogueIds.Conversation);
        var speaker = database.GetActor(NullDispatcherDialogueIds.Actor);
        result = "NULL DISPATCHER graph is missing or differs from its deterministic contract. Reinstall it.";
        if (graph == null || speaker == null || graph.ConversantID != speaker.id ||
            Field.LookupValue(speaker.fields, "Name TextKey") != TextKeys[0] || graph.dialogueEntries.Count != 10) return false;
        var expected = BuildGraph(graph.id, graph.ActorID, speaker.id, catalog);
        for (int i = 0; i < expected.Count; i++)
        {
            var actual = graph.GetDialogueEntry(i);
            var node = expected[i];
            if (actual == null || actual.ActorID != node.ActorID || actual.ConversantID != node.ConversantID ||
                actual.isRoot != node.isRoot || actual.isGroup != node.isGroup ||
                actual.Sequence != node.Sequence || actual.Title != node.Title ||
                !string.IsNullOrEmpty(actual.conditionsString) ||
                (actual.userScript ?? "") != (node.userScript ?? "") || actual.DialogueText != node.DialogueText ||
                Field.LookupValue(actual.fields, "TextKey") != Field.LookupValue(node.fields, "TextKey") ||
                actual.outgoingLinks.Count != node.outgoingLinks.Count) return false;
            for (int link = 0; link < node.outgoingLinks.Count; link++)
            {
                var a = actual.outgoingLinks[link];
                var b = node.outgoingLinks[link];
                if (a.destinationConversationID != b.destinationConversationID ||
                    a.destinationDialogueID != b.destinationDialogueID) return false;
            }
        }
        result = "NULL DISPATCHER treatment validation passed.";
        return true;
    }
}
