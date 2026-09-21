using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;

// One deterministic graph on the existing database, using the existing localization source.
public static class NullDispatcherEndingDialogueInstaller
{
    public const string PrefabPath = "Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab";

    [MenuItem("VOID SCRAPPER/Dialogue/Author NULL DISPATCHER Ending Prefab")]
    public static void AuthorPrefabFromMenu()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var boss = root.GetComponent<BossDummyController>();
            var combat = root.GetComponent<NullDispatcherBossController>();
            var entry = root.GetComponent<DialogueStoryEntryPoint>();
            if (boss == null || combat == null || entry == null)
                throw new InvalidOperationException("The existing final boss foundation is incomplete.");
            var source = new SerializedObject(combat);
            var ending = root.GetComponent<NullDispatcherEndingPresentation>();
            if (ending == null) ending = root.AddComponent<NullDispatcherEndingPresentation>();
            var destination = new SerializedObject(ending);
            destination.FindProperty("innerCore").objectReferenceValue = source.FindProperty("innerCore").objectReferenceValue;
            destination.FindProperty("outerShellRoot").objectReferenceValue = source.FindProperty("outerShellRoot").objectReferenceValue;
            destination.FindProperty("shutdownPulse").objectReferenceValue = source.FindProperty("breakPulse").objectReferenceValue;
            destination.FindProperty("dialogueEntry").objectReferenceValue = entry;
            destination.ApplyModifiedPropertiesWithoutUndo();
            var death = new SerializedObject(boss);
            death.FindProperty("finalEndingPresentation").objectReferenceValue = ending;
            death.ApplyModifiedPropertiesWithoutUndo();
            if (!ValidatePrefab(root, out string error)) throw new InvalidOperationException(error);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("NULL DISPATCHER ending prefab authored and validated.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static bool ValidatePrefab(GameObject root, out string result)
    {
        result = "NULL DISPATCHER ending prefab has missing or mismatched references.";
        if (root == null) return false;
        var ending = root.GetComponent<NullDispatcherEndingPresentation>();
        var boss = root.GetComponent<BossDummyController>();
        var combat = root.GetComponent<NullDispatcherBossController>();
        if (ending == null || !ending.enabled || boss == null || combat == null) return false;
        var destination = new SerializedObject(ending);
        var source = new SerializedObject(combat);
        var entry = destination.FindProperty("dialogueEntry").objectReferenceValue as DialogueStoryEntryPoint;
        // The shared treatment entry reports every end. Each encounter presentation
        // verifies its own natural marker; changing the entry would break treatment retries.
        if (entry == null || new SerializedObject(entry).FindProperty("requireNaturalCompletionMarker").boolValue ||
            new SerializedObject(entry).FindProperty("completionActionId").enumValueIndex != 0 ||
            new SerializedObject(entry).FindProperty("startOnSceneLoadWhenRequiredFlagPresent").boolValue ||
            new SerializedObject(boss).FindProperty("finalEndingPresentation").objectReferenceValue != ending) return false;
        foreach (string field in new[] { "innerCore", "outerShellRoot" })
        {
            var reference = destination.FindProperty(field).objectReferenceValue;
            if (reference == null || reference != source.FindProperty(field).objectReferenceValue) return false;
        }
        if (destination.FindProperty("shutdownPulse").objectReferenceValue == null) return false;
        result = "NULL DISPATCHER ending prefab validation passed.";
        return true;
    }

    public static readonly string[] TextKeys =
    {
        NullDispatcherDialogueIds.SpeakerKey,
        "final.ending.null.line_01", "final.ending.null.line_02",
        "final.ending.null.line_03", "final.ending.null.line_04",
        "final.ending.operator.line_01", "final.ending.operator.line_02"
    };

    [MenuItem("VOID SCRAPPER/Dialogue/Install NULL DISPATCHER Ending")]
    public static void InstallFromMenu()
    {
        LoadAssets(out var database, out var catalog);
        if (TryInstall(database, catalog, true, out string result)) Debug.Log(result, database);
        else Debug.LogError(result);
    }

    [MenuItem("VOID SCRAPPER/Dialogue/Validate NULL DISPATCHER Ending")]
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
            database.GetConversation(Phase2CStoryDialogueIds.FirstSettlementConversation) == null ||
            database.GetActor(Phase2CStoryDialogueIds.OperatorActorName) == null) return false;
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
            if (conversation.Title == NullDispatcherEndingPresentation.ConversationTitle) conversationCount++;
        if (actorCount > 1 || conversationCount > 1)
        {
            result = "Duplicate NULL DISPATCHER actor/conversation must be resolved before installation.";
            return false;
        }
        int operatorCount = 0;
        foreach (var actor in database.actors)
            if (actor.Name == Phase2CStoryDialogueIds.OperatorActorName) operatorCount++;
        if (operatorCount != 1 || database.GetActor(Phase2CStoryDialogueIds.OperatorActorName)
            .LookupValue("Name TextKey") != Phase2CStoryDialogueIds.OperatorSpeakerTextKey)
        {
            result = "Install/validate the existing Operator actor before the ending.";
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
        if (saveAssets) Undo.RecordObject(database, "Install NULL DISPATCHER ending");
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
        Conversation graph = database.GetConversation(NullDispatcherEndingPresentation.ConversationTitle);
        if (graph == null)
        {
            int id = 1;
            foreach (var conversation in database.conversations) id = Math.Max(id, conversation.id + 1);
            graph = template.CreateConversation(id, NullDispatcherEndingPresentation.ConversationTitle);
            database.AddConversation(graph);
        }
        graph.ActorID = playerId;
        graph.ConversantID = speaker.id;
        Field.SetValue(graph.fields, "Pause Gameplay", true);
        graph.dialogueEntries = BuildGraph(graph.id, playerId, speaker.id, database.GetActor(Phase2CStoryDialogueIds.OperatorActorName).id, catalog);
        if (!TryValidateInstalled(database, catalog, out result)) return false;
        if (saveAssets)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
        result = "Installed NULL DISPATCHER ending: six subtitles, no choices, one natural-completion terminal.";
        return true;
    }

    private static List<DialogueEntry> BuildGraph(int conversationId, int player, int speaker, int operatorId, LocalizationCatalog catalog)
    {
        var entries = new List<DialogueEntry>(8);
        for (int id = 0; id <= 7; id++)
        {
            var entry = new DialogueEntry
            {
                id = id, conversationID = conversationId, isRoot = id == 0,
                falseConditionAction = "Block", conditionPriority = ConditionPriority.Normal,
                fields = new List<Field>(), canvasRect = new Rect(320, id * 65, 190, 40)
            };
            Field.SetValue(entry.fields, "Actor", (id == 5 || id == 6 ? operatorId : speaker).ToString(), FieldType.Actor);
            Field.SetValue(entry.fields, "Conversant", player.ToString(), FieldType.Actor);
            Field.SetValue(entry.fields, "Title", id == 0 ? "START" : id == 7 ? "END" : string.Empty);
            Field.SetValue(entry.fields, "Sequence", id == 0 || id == 7 ? "None()" : string.Empty);
            if (id > 0 && id < 7)
            {
                catalog.TryGetEntry(TextKeys[id], out var text);
                Field.SetValue(entry.fields, "TextKey", text.TextKey);
                Field.SetValue(entry.fields, "Dialogue Text", text.Korean);
                Field.SetValue(entry.fields, "en", text.English, FieldType.Localization);
            }
            if (id == 7) entry.userScript =
                $"{DialoguePixelCrushersBridge.CompletionLuaFunction}(\"{NullDispatcherEndingPresentation.ConversationTitle}\")";
            entries.Add(entry);
        }
        for (int id = 0; id < 7; id++) AddLink(entries[id], id + 1);
        return entries;
    }

    private static void AddLink(DialogueEntry entry, int target)
    {
        entry.outgoingLinks.Add(new Link(entry.conversationID, entry.id, entry.conversationID, target)
        { priority = ConditionPriority.Normal });
    }

    public static bool TryValidateInstalled(DialogueDatabase database, LocalizationCatalog catalog, out string result)
    {
        if (!ValidateSource(database, catalog, out result)) return false;
        var graph = database.GetConversation(NullDispatcherEndingPresentation.ConversationTitle);
        var speaker = database.GetActor(NullDispatcherDialogueIds.Actor);
        result = "NULL DISPATCHER graph is missing or differs from its deterministic contract. Reinstall it.";
        if (graph == null || speaker == null || graph.ConversantID != speaker.id ||
            graph.ActorID != database.GetConversation(Phase2CStoryDialogueIds.FirstSettlementConversation).ActorID ||
            !Field.LookupBool(graph.fields, "Pause Gameplay") ||
            Field.LookupValue(speaker.fields, "Name TextKey") != TextKeys[0] || graph.dialogueEntries.Count != 8) return false;
        var expected = BuildGraph(graph.id, graph.ActorID, speaker.id, database.GetActor(Phase2CStoryDialogueIds.OperatorActorName).id, catalog);
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
        result = "NULL DISPATCHER ending validation passed.";
        return true;
    }
}
