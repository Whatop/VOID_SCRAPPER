using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;

public static class Phase2CStoryDialogueInstaller
{
    private const string TextKeyField = "TextKey";
    private const string NameTextKeyField = "Name TextKey";

    [MenuItem("VOID SCRAPPER/Dialogue/Install Phase 2C Story")]
    public static void InstallFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Install Phase 2C Story",
                "Install the validated tutorial and first-Settlement story graphs? " +
                "Only the named Phase 2C conversations and speaker fields are changed.",
                "Install",
                "Cancel"))
        {
            return;
        }

        LoadAssets(out DialogueDatabase database, out LocalizationCatalog catalog);
        if (!TryInstall(database, catalog, true, out string result))
        {
            Debug.LogError($"[Dialogue Phase 2C] {result}");
            return;
        }

        Debug.Log($"[Dialogue Phase 2C] {result}", database);
    }

    [MenuItem("VOID SCRAPPER/Dialogue/Validate Phase 2C Story")]
    public static void ValidateFromMenu()
    {
        LoadAssets(out DialogueDatabase database, out LocalizationCatalog catalog);
        if (!TryValidateInstalled(database, catalog, out string result))
        {
            Debug.LogError($"[Dialogue Phase 2C] {result}");
            return;
        }

        Debug.Log($"[Dialogue Phase 2C] {result}", database);
    }

    public static bool TryInstall(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        bool saveAssets,
        out string result)
    {
        if (!TryBuildEntries(
                database,
                catalog,
                out Dictionary<string, List<DialogueEntry>> graphs,
                out int operatorActorId,
                out _,
                out result))
        {
            return false;
        }

        Conversation opening = database.GetConversation(
            Phase2CStoryDialogueIds.TutorialOpeningConversation);
        Conversation settlement = database.GetConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);
        Actor playerActor = database.GetActor(opening.ActorID);
        Actor settlementActor = database.GetActor(settlement.ConversantID);
        Actor operatorActor = database.GetActor(
            Phase2CStoryDialogueIds.OperatorActorName);
        int fakeOperatorActorId = graphs[
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation][1].ActorID;
        Actor fakeOperatorActor = database.GetActor(
            Phase2CStoryDialogueIds.FakeOperatorActorName);
        Template template = ResolveTemplate(database);

        if (saveAssets)
        {
            Undo.RecordObject(database, "Install Phase 2C story dialogue");
        }

        if (operatorActor == null)
        {
            operatorActor = template.CreateActor(
                operatorActorId,
                Phase2CStoryDialogueIds.OperatorActorName,
                false);
            database.actors.Add(operatorActor);
        }

        if (fakeOperatorActor == null)
        {
            fakeOperatorActor = template.CreateActor(
                fakeOperatorActorId,
                Phase2CStoryDialogueIds.FakeOperatorActorName,
                false);
            database.actors.Add(fakeOperatorActor);
        }

        foreach (KeyValuePair<string, List<DialogueEntry>> pair in graphs)
        {
            Conversation conversation = database.GetConversation(pair.Key);
            if (conversation == null)
            {
                conversation = template.CreateConversation(
                    pair.Value[0].conversationID,
                    pair.Key);
                Field.SetValue(
                    conversation.fields,
                    "Description",
                    "Deterministically installed VOID SCRAPPER Phase 2C story content.");
                database.AddConversation(conversation);
            }

            conversation.ActorID = playerActor.id;
            if (string.Equals(
                    pair.Key,
                    Phase2CStoryDialogueIds.FirstSettlementConversation,
                    StringComparison.Ordinal))
            {
                conversation.ConversantID = settlementActor.id;
            }
            else if (string.Equals(
                         pair.Key,
                         Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
                         StringComparison.Ordinal))
            {
                conversation.ConversantID = fakeOperatorActor.id;
            }
            else
            {
                conversation.ConversantID = operatorActor.id;
            }
            conversation.dialogueEntries = pair.Value;
        }

        ApplySpeakerLocalization(
            operatorActor,
            GetRequiredEntry(catalog, Phase2CStoryDialogueIds.OperatorSpeakerTextKey));
        ApplySpeakerLocalization(
            fakeOperatorActor,
            GetRequiredEntry(
                catalog,
                Phase2CStoryDialogueIds.FakeOperatorSpeakerTextKey));
        ApplySpeakerLocalization(
            settlementActor,
            GetRequiredEntry(
                catalog,
                Phase2CStoryDialogueIds.SettlementControlSpeakerTextKey));

        if (saveAssets)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        if (!TryValidateInstalled(database, catalog, out result))
        {
            return false;
        }

        result = "Installed nine Phase 2C story graphs, stable Operator/FakeOperator actors, " +
                 "and localized speaker fields. Re-running the installer is idempotent.";
        return true;
    }

    public static bool TryBuildEntries(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        out Dictionary<string, List<DialogueEntry>> graphs,
        out int operatorActorId,
        out int unknownConversationId,
        out string result)
    {
        graphs = null;
        operatorActorId = 0;
        unknownConversationId = 0;

        if (!TryResolveFoundation(
                database,
                catalog,
                out Conversation opening,
                out Conversation rescue,
                out Conversation settlement,
                out Actor playerActor,
                out Actor settlementActor,
                out operatorActorId,
                out unknownConversationId,
                out Dictionary<string, LocalizationEntry> localized,
                out result))
        {
            return false;
        }

        int operatorId = operatorActorId;
        int fakeOperatorId = ResolveFakeOperatorActorId(
            database,
            operatorActorId);
        Dictionary<string, int> conversationIds = ResolveConversationIds(
            database,
            opening,
            rescue,
            settlement);
        unknownConversationId = conversationIds[
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation];
        graphs = new Dictionary<string, List<DialogueEntry>>(StringComparer.Ordinal)
        {
            [opening.Title] = BuildSingleLineGraph(
                opening.id,
                opening.Title,
                playerActor.id,
                operatorId,
                operatorId,
                Phase2CStoryDialogueIds.TutorialMovementTextKey,
                localized),
            [Phase2CStoryDialogueIds.TutorialRadarConversation] =
                BuildSingleLineGraph(
                    conversationIds[Phase2CStoryDialogueIds.TutorialRadarConversation],
                    Phase2CStoryDialogueIds.TutorialRadarConversation,
                    playerActor.id,
                    operatorId,
                    operatorId,
                    Phase2CStoryDialogueIds.TutorialRadarTextKey,
                    localized),
            [Phase2CStoryDialogueIds.TutorialSupplyConversation] =
                BuildSingleLineGraph(
                    conversationIds[Phase2CStoryDialogueIds.TutorialSupplyConversation],
                    Phase2CStoryDialogueIds.TutorialSupplyConversation,
                    playerActor.id,
                    operatorId,
                    operatorId,
                    Phase2CStoryDialogueIds.TutorialSupplyTextKey,
                    localized),
            [Phase2CStoryDialogueIds.TutorialAncientSignalConversation] =
                BuildSingleLineGraph(
                    conversationIds[Phase2CStoryDialogueIds.TutorialAncientSignalConversation],
                    Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
                    playerActor.id,
                    operatorId,
                    operatorId,
                    Phase2CStoryDialogueIds.TutorialAncientSignalTextKey,
                    localized),
            [Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation] =
                BuildLinearGraph(
                    conversationIds[
                        Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation],
                    Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
                    playerActor.id,
                    operatorId,
                    operatorId,
                    Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisLinePrefix,
                    2,
                    localized),
            [Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation] =
                BuildSingleLineGraph(
                    conversationIds[
                        Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation],
                    Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation,
                    playerActor.id,
                    fakeOperatorId,
                    fakeOperatorId,
                    Phase2CStoryDialogueIds.GetNumberedTextKey(
                        Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverLinePrefix,
                        1),
                    localized),
            [Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation] =
                BuildLinearGraph(
                    conversationIds[
                        Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation],
                    Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
                    playerActor.id,
                    operatorId,
                    operatorId,
                    Phase2CStoryDialogueIds.TutorialUnknownLinePrefix,
                    2,
                    localized),
            [rescue.Title] = BuildLinearGraph(
                rescue.id,
                rescue.Title,
                playerActor.id,
                operatorId,
                operatorId,
                Phase2CStoryDialogueIds.TutorialRescueLinePrefix,
                3,
                localized),
            [settlement.Title] = BuildSettlementGraph(
                settlement.id,
                settlement.Title,
                playerActor.id,
                settlementActor.id,
                operatorId,
                localized)
        };

        foreach (KeyValuePair<string, List<DialogueEntry>> pair in graphs)
        {
            if (!ValidateGraph(pair.Key, pair.Value, out result))
            {
                graphs = null;
                return false;
            }
        }

        result = $"Source validated. Conversations={graphs.Count}, " +
                 $"LocalizationKeys={localized.Count}.";
        return true;
    }

    public static bool TryValidateInstalled(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        out string result)
    {
        if (!TryBuildEntries(
                database,
                catalog,
                out Dictionary<string, List<DialogueEntry>> expectedGraphs,
                out _,
                out _,
                out result))
        {
            return false;
        }

        Actor operatorActor = database.GetActor(
            Phase2CStoryDialogueIds.OperatorActorName);
        if (operatorActor == null)
        {
            result = $"Actor '{Phase2CStoryDialogueIds.OperatorActorName}' is not installed.";
            return false;
        }

        Actor fakeOperatorActor = database.GetActor(
            Phase2CStoryDialogueIds.FakeOperatorActorName);
        if (fakeOperatorActor == null ||
            !string.Equals(
                fakeOperatorActor.LookupValue(NameTextKeyField),
                Phase2CStoryDialogueIds.FakeOperatorSpeakerTextKey,
                StringComparison.Ordinal) ||
            !catalog.TryGetEntry(
                Phase2CStoryDialogueIds.FakeOperatorSpeakerTextKey,
                out LocalizationEntry fakeOperatorSpeaker) ||
            !string.Equals(
                fakeOperatorActor.LookupValue("Display Name"),
                fakeOperatorSpeaker.Korean,
                StringComparison.Ordinal))
        {
            result = $"Actor '{Phase2CStoryDialogueIds.FakeOperatorActorName}' has an invalid " +
                     $"'{NameTextKeyField}' field.";
            return false;
        }

        if (!string.Equals(
                operatorActor.LookupValue(NameTextKeyField),
                Phase2CStoryDialogueIds.OperatorSpeakerTextKey,
                StringComparison.Ordinal) ||
            !catalog.TryGetEntry(
                Phase2CStoryDialogueIds.OperatorSpeakerTextKey,
                out LocalizationEntry operatorSpeaker) ||
            !string.Equals(
                operatorActor.LookupValue("Display Name"),
                operatorSpeaker.Korean,
                StringComparison.Ordinal))
        {
            result = $"Actor '{Phase2CStoryDialogueIds.OperatorActorName}' has an invalid " +
                     $"'{NameTextKeyField}' field.";
            return false;
        }

        Conversation settlementConversation = database.GetConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);
        Actor settlementActor = settlementConversation != null
            ? database.GetActor(settlementConversation.ConversantID)
            : null;
        if (settlementActor == null ||
            !string.Equals(
                settlementActor.LookupValue(NameTextKeyField),
                Phase2CStoryDialogueIds.SettlementControlSpeakerTextKey,
                StringComparison.Ordinal) ||
            !catalog.TryGetEntry(
                Phase2CStoryDialogueIds.SettlementControlSpeakerTextKey,
                out LocalizationEntry settlementSpeaker) ||
            !string.Equals(
                settlementActor.LookupValue("Display Name"),
                settlementSpeaker.Korean,
                StringComparison.Ordinal))
        {
            result = "Settlement Control has no valid localized speaker-name field.";
            return false;
        }

        foreach (KeyValuePair<string, List<DialogueEntry>> pair in expectedGraphs)
        {
            if (CountConversations(database, pair.Key) != 1)
            {
                result = $"Conversation '{pair.Key}' must exist exactly once.";
                return false;
            }

            Conversation installed = database.GetConversation(pair.Key);
            if (!string.Equals(
                    BuildGraphSignature(installed.dialogueEntries),
                    BuildGraphSignature(pair.Value),
                    StringComparison.Ordinal))
            {
                result = $"Conversation '{pair.Key}' differs from deterministic Phase 2C content.";
                return false;
            }
        }

        result = "Installed Phase 2C graph validation passed.";
        return true;
    }

    public static string BuildGraphSignature(IReadOnlyList<DialogueEntry> entries)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        if (entries == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            DialogueEntry entry = entries[i];
            builder.Append(entry.id).Append('|')
                .Append(entry.ActorID).Append('|')
                .Append(entry.ConversantID).Append('|')
                .Append(Field.LookupValue(entry.fields, TextKeyField)).Append('|')
                .Append(Field.LookupValue(entry.fields, "Dialogue Text")).Append('|')
                .Append(Field.LookupValue(
                    entry.fields,
                    LocalizationLanguageCodes.English)).Append('|')
                .Append(Field.LookupValue(
                    entry.fields,
                    LocalizationLanguageCodes.Japanese)).Append('|')
                .Append(Field.LookupValue(
                    entry.fields,
                    LocalizationLanguageCodes.SimplifiedChinese)).Append('|')
                .Append(Field.LookupValue(
                    entry.fields,
                    LocalizationLanguageCodes.TraditionalChinese)).Append('|')
                .Append(entry.conditionsString).Append('|')
                .Append(entry.userScript).Append('|');

            for (int linkIndex = 0; linkIndex < entry.outgoingLinks.Count; linkIndex++)
            {
                Link link = entry.outgoingLinks[linkIndex];
                builder.Append(link.destinationConversationID).Append(':')
                    .Append(link.destinationDialogueID).Append(',');
            }

            builder.Append(';');
        }

        return builder.ToString();
    }

    private static bool TryResolveFoundation(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        out Conversation opening,
        out Conversation rescue,
        out Conversation settlement,
        out Actor playerActor,
        out Actor settlementActor,
        out int operatorActorId,
        out int unknownConversationId,
        out Dictionary<string, LocalizationEntry> localized,
        out string result)
    {
        opening = null;
        rescue = null;
        settlement = null;
        playerActor = null;
        settlementActor = null;
        operatorActorId = 0;
        unknownConversationId = 0;
        localized = null;

        if (database == null || catalog == null)
        {
            result = "Dialogue Database and generated LocalizationCatalog are required.";
            return false;
        }

        opening = database.GetConversation(
            Phase2CStoryDialogueIds.TutorialOpeningConversation);
        rescue = database.GetConversation(
            Phase2CStoryDialogueIds.TutorialRescueConversation);
        settlement = database.GetConversation(
            Phase2CStoryDialogueIds.FirstSettlementConversation);

        if (opening == null || rescue == null || settlement == null)
        {
            result = "One or more existing Phase 2C conversation IDs are missing.";
            return false;
        }

        string[] optionalConversationTitles =
        {
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
            Phase2CStoryDialogueIds.TutorialRadarConversation,
            Phase2CStoryDialogueIds.TutorialSupplyConversation,
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation
        };
        bool duplicateOptionalConversation = false;
        for (int i = 0; i < optionalConversationTitles.Length; i++)
        {
            if (CountConversations(database, optionalConversationTitles[i]) > 1)
            {
                duplicateOptionalConversation = true;
                break;
            }
        }

        if (CountConversations(database, opening.Title) != 1 ||
            CountConversations(database, rescue.Title) != 1 ||
            CountConversations(database, settlement.Title) != 1 ||
            duplicateOptionalConversation)
        {
            result = "Duplicate Phase 2C conversation IDs were detected.";
            return false;
        }

        playerActor = database.GetActor(opening.ActorID);
        settlementActor = database.GetActor(settlement.ConversantID);
        if (playerActor == null || settlementActor == null)
        {
            result = "The Player or Settlement Control actor reference is missing.";
            return false;
        }

        Template template = ResolveTemplate(database);
        Actor operatorActor = database.GetActor(
            Phase2CStoryDialogueIds.OperatorActorName);
        operatorActorId = operatorActor != null
            ? operatorActor.id
            : template.GetNextActorID(database);
        Conversation unknown = database.GetConversation(
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation);
        unknownConversationId = unknown != null
            ? unknown.id
            : template.GetNextConversationID(database);

        string[] requiredKeys = BuildRequiredKeys();
        localized = new Dictionary<string, LocalizationEntry>(
            requiredKeys.Length,
            StringComparer.Ordinal);

        for (int i = 0; i < requiredKeys.Length; i++)
        {
            string key = requiredKeys[i];
            if (localized.ContainsKey(key))
            {
                result = $"Duplicate Phase 2C localization key '{key}' was requested.";
                return false;
            }

            if (!catalog.TryGetEntry(key, out LocalizationEntry entry) ||
                entry == null ||
                string.IsNullOrWhiteSpace(entry.Korean))
            {
                result = $"Localization key '{key}' is missing or has empty Korean source.";
                return false;
            }

            localized.Add(key, entry);
        }

        result = string.Empty;
        return true;
    }

    private static Dictionary<string, int> ResolveConversationIds(
        DialogueDatabase database,
        Conversation opening,
        Conversation rescue,
        Conversation settlement)
    {
        HashSet<int> usedIds = new HashSet<int>();
        int nextId = 1;
        for (int i = 0; i < database.conversations.Count; i++)
        {
            Conversation conversation = database.conversations[i];
            if (conversation == null)
            {
                continue;
            }

            usedIds.Add(conversation.id);
            nextId = Mathf.Max(nextId, conversation.id + 1);
        }

        Dictionary<string, int> ids = new Dictionary<string, int>(
            StringComparer.Ordinal)
        {
            [opening.Title] = opening.id,
            [rescue.Title] = rescue.id,
            [settlement.Title] = settlement.id
        };
        string[] generatedTitles =
        {
            Phase2CStoryDialogueIds.TutorialUnknownAccessKeyConversation,
            Phase2CStoryDialogueIds.TutorialRadarConversation,
            Phase2CStoryDialogueIds.TutorialSupplyConversation,
            Phase2CStoryDialogueIds.TutorialAncientSignalConversation,
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisConversation,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverConversation
        };

        for (int i = 0; i < generatedTitles.Length; i++)
        {
            string title = generatedTitles[i];
            Conversation existing = database.GetConversation(title);
            if (existing != null)
            {
                ids[title] = existing.id;
                continue;
            }

            while (usedIds.Contains(nextId))
            {
                nextId++;
            }

            ids[title] = nextId;
            usedIds.Add(nextId);
            nextId++;
        }

        return ids;
    }

    private static int ResolveFakeOperatorActorId(
        DialogueDatabase database,
        int reservedOperatorActorId)
    {
        Actor existing = database.GetActor(
            Phase2CStoryDialogueIds.FakeOperatorActorName);
        if (existing != null)
        {
            return existing.id;
        }

        int nextId = ResolveTemplate(database).GetNextActorID(database);
        while (nextId == reservedOperatorActorId || database.GetActor(nextId) != null)
        {
            nextId++;
        }

        return nextId;
    }

    private static List<DialogueEntry> BuildSingleLineGraph(
        int conversationId,
        string conversationTitle,
        int playerActorId,
        int conversantActorId,
        int lineActorId,
        string textKey,
        IReadOnlyDictionary<string, LocalizationEntry> localized)
    {
        List<DialogueEntry> entries = new List<DialogueEntry>(3);
        DialogueEntry root = CreateEntry(
            0,
            conversationId,
            playerActorId,
            conversantActorId,
            true,
            null,
            null);
        DialogueEntry line = CreateEntry(
            1,
            conversationId,
            lineActorId,
            playerActorId,
            false,
            textKey,
            localized[textKey]);
        DialogueEntry terminal = CreateTerminalEntry(
            2,
            conversationId,
            playerActorId,
            conversantActorId,
            conversationTitle);
        root.outgoingLinks.Add(CreateLink(root, line));
        line.outgoingLinks.Add(CreateLink(line, terminal));
        entries.Add(root);
        entries.Add(line);
        entries.Add(terminal);
        return entries;
    }

    private static List<DialogueEntry> BuildLinearGraph(
        int conversationId,
        string conversationTitle,
        int playerActorId,
        int conversantActorId,
        int lineActorId,
        string textKeyPrefix,
        int lineCount,
        IReadOnlyDictionary<string, LocalizationEntry> localized)
    {
        List<DialogueEntry> entries = new List<DialogueEntry>(lineCount + 2);
        DialogueEntry root = CreateEntry(
            0,
            conversationId,
            playerActorId,
            conversantActorId,
            true,
            null,
            null);
        entries.Add(root);

        DialogueEntry previous = root;
        for (int i = 1; i <= lineCount; i++)
        {
            string key = Phase2CStoryDialogueIds.GetNumberedTextKey(textKeyPrefix, i);
            DialogueEntry line = CreateEntry(
                i,
                conversationId,
                lineActorId,
                playerActorId,
                false,
                key,
                localized[key]);
            previous.outgoingLinks.Add(CreateLink(previous, line));
            entries.Add(line);
            previous = line;
        }

        DialogueEntry terminal = CreateTerminalEntry(
            lineCount + 1,
            conversationId,
            playerActorId,
            conversantActorId,
            conversationTitle);
        previous.outgoingLinks.Add(CreateLink(previous, terminal));
        entries.Add(terminal);
        return entries;
    }

    private static List<DialogueEntry> BuildSettlementGraph(
        int conversationId,
        string conversationTitle,
        int playerActorId,
        int settlementActorId,
        int operatorActorId,
        IReadOnlyDictionary<string, LocalizationEntry> localized)
    {
        List<DialogueEntry> entries = new List<DialogueEntry>(13);
        DialogueEntry root = CreateEntry(
            0,
            conversationId,
            playerActorId,
            settlementActorId,
            true,
            null,
            null);
        entries.Add(root);

        DialogueEntry previous = root;
        for (int i = 1; i <= 6; i++)
        {
            string key = Phase2CStoryDialogueIds.GetNumberedTextKey(
                Phase2CStoryDialogueIds.FirstSettlementLinePrefix,
                i);
            int speakerId = i <= 3 ? settlementActorId : operatorActorId;
            DialogueEntry line = CreateEntry(
                i,
                conversationId,
                speakerId,
                playerActorId,
                false,
                key,
                localized[key]);
            previous.outgoingLinks.Add(CreateLink(previous, line));
            entries.Add(line);
            previous = line;
        }

        entries[1].conditionsString = BuildCondition(
            DialogueConditionId.MainDamagedAccessKeyNotStarted);

        string[] stateKeys =
        {
            Phase2CStoryDialogueIds.MainQuestActive0TextKey,
            Phase2CStoryDialogueIds.MainQuestActive1TextKey,
            Phase2CStoryDialogueIds.MainQuestActive2TextKey,
            Phase2CStoryDialogueIds.MainQuestReadyTextKey,
            Phase2CStoryDialogueIds.MainQuestCompletedTextKey
        };
        DialogueConditionId[] stateConditions =
        {
            DialogueConditionId.MainDamagedAccessKeyActive0,
            DialogueConditionId.MainDamagedAccessKeyActive1,
            DialogueConditionId.MainDamagedAccessKeyActive2,
            DialogueConditionId.MainDamagedAccessKeyReadyToRestore,
            DialogueConditionId.MainDamagedAccessKeyCompleted
        };

        DialogueEntry terminal = CreateTerminalEntry(
            12,
            conversationId,
            playerActorId,
            settlementActorId,
            conversationTitle);
        previous.outgoingLinks.Add(CreateLink(previous, terminal));

        for (int i = 0; i < stateKeys.Length; i++)
        {
            DialogueEntry stateLine = CreateEntry(
                7 + i,
                conversationId,
                operatorActorId,
                playerActorId,
                false,
                stateKeys[i],
                localized[stateKeys[i]]);
            stateLine.conditionsString = BuildCondition(stateConditions[i]);
            stateLine.outgoingLinks.Add(CreateLink(stateLine, terminal));
            root.outgoingLinks.Add(CreateLink(root, stateLine));
            entries.Add(stateLine);
        }

        entries.Add(terminal);
        return entries;
    }

    private static DialogueEntry CreateEntry(
        int id,
        int conversationId,
        int actorId,
        int conversantId,
        bool isRoot,
        string textKey,
        LocalizationEntry localizedEntry)
    {
        DialogueEntry entry = new DialogueEntry
        {
            id = id,
            conversationID = conversationId,
            isRoot = isRoot,
            isGroup = false,
            falseConditionAction = "Block",
            conditionPriority = ConditionPriority.Normal,
            fields = new List<Field>(),
            canvasRect = new Rect(400f + id * 36f, 30f + id * 52f, 180f, 30f)
        };

        Field.SetValue(entry.fields, "Title", isRoot ? "START" : string.Empty);
        Field.SetValue(entry.fields, "Description", string.Empty);
        Field.SetValue(entry.fields, "Actor", actorId.ToString(), FieldType.Actor);
        Field.SetValue(
            entry.fields,
            "Conversant",
            conversantId.ToString(),
            FieldType.Actor);
        Field.SetValue(entry.fields, "Menu Text", string.Empty);
        Field.SetValue(entry.fields, "Dialogue Text", string.Empty);
        Field.SetValue(entry.fields, "Sequence", isRoot ? "None()" : string.Empty);

        if (localizedEntry != null && !string.IsNullOrWhiteSpace(textKey))
        {
            Field.SetValue(entry.fields, TextKeyField, textKey);
            Field.SetValue(entry.fields, "Dialogue Text", localizedEntry.Korean);
            AddLocalizedDialogueFields(entry.fields, localizedEntry);
        }

        return entry;
    }

    private static DialogueEntry CreateTerminalEntry(
        int id,
        int conversationId,
        int actorId,
        int conversantId,
        string conversationTitle)
    {
        DialogueEntry terminal = CreateEntry(
            id,
            conversationId,
            actorId,
            conversantId,
            false,
            null,
            null);
        terminal.Title = "END";
        terminal.Sequence = "None()";
        terminal.userScript =
            $"{DialoguePixelCrushersBridge.CompletionLuaFunction}(\"" +
            $"{conversationTitle}\")";
        return terminal;
    }

    private static Link CreateLink(DialogueEntry origin, DialogueEntry destination)
    {
        return new Link(
            origin.conversationID,
            origin.id,
            destination.conversationID,
            destination.id)
        {
            priority = ConditionPriority.Normal
        };
    }

    private static string BuildCondition(DialogueConditionId conditionId)
    {
        return $"{DialoguePixelCrushersBridge.ConditionLuaFunction}(\"" +
               $"{conditionId}\")";
    }

    private static bool ValidateGraph(
        string conversationTitle,
        IReadOnlyList<DialogueEntry> entries,
        out string result)
    {
        if (entries == null || entries.Count < 3 || !entries[0].isRoot)
        {
            result = $"Conversation '{conversationTitle}' has no valid START graph.";
            return false;
        }

        HashSet<int> ids = new HashSet<int>();
        HashSet<int> linkedDestinations = new HashSet<int>();
        int terminalCount = 0;
        int rootCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            DialogueEntry entry = entries[i];
            if (entry == null || !ids.Add(entry.id))
            {
                result = $"Conversation '{conversationTitle}' has a null or duplicate entry ID.";
                return false;
            }

            if (entry.isRoot)
            {
                rootCount++;
            }

            if (!string.IsNullOrWhiteSpace(entry.conditionsString) &&
                !IsSupportedConditionScript(entry.conditionsString))
            {
                result = $"Conversation '{conversationTitle}' has an unknown condition " +
                         $"script on entry {entry.id}.";
                return false;
            }

            if (!string.IsNullOrEmpty(entry.userScript))
            {
                if (!entry.userScript.Contains(
                        DialoguePixelCrushersBridge.CompletionLuaFunction) ||
                    entry.userScript.Contains(
                        DialoguePixelCrushersBridge.ActionLuaFunction))
                {
                    result = $"Conversation '{conversationTitle}' has an unsafe action script.";
                    return false;
                }

                terminalCount++;
            }

            for (int linkIndex = 0; linkIndex < entry.outgoingLinks.Count; linkIndex++)
            {
                Link link = entry.outgoingLinks[linkIndex];
                if (link.originConversationID != entry.conversationID ||
                    link.originDialogueID != entry.id ||
                    link.destinationConversationID != entry.conversationID)
                {
                    result = $"Conversation '{conversationTitle}' has a cross-graph link.";
                    return false;
                }

                linkedDestinations.Add(link.destinationDialogueID);
            }
        }

        if (rootCount != 1)
        {
            result = $"Conversation '{conversationTitle}' requires exactly one START entry.";
            return false;
        }

        for (int i = 1; i < entries.Count; i++)
        {
            if (!ids.Contains(entries[i].id) ||
                !linkedDestinations.Contains(entries[i].id))
            {
                result = $"Conversation '{conversationTitle}' has an unreachable entry.";
                return false;
            }
        }

        foreach (int destinationId in linkedDestinations)
        {
            if (!ids.Contains(destinationId))
            {
                result = $"Conversation '{conversationTitle}' links to missing entry " +
                         $"ID {destinationId}.";
                return false;
            }
        }

        if (terminalCount != 1)
        {
            result = $"Conversation '{conversationTitle}' requires one completion marker.";
            return false;
        }

        result = string.Empty;
        return true;
    }

    private static bool IsSupportedConditionScript(string conditionScript)
    {
        DialogueConditionId[] supportedConditions =
        {
            DialogueConditionId.MainDamagedAccessKeyNotStarted,
            DialogueConditionId.MainDamagedAccessKeyActive0,
            DialogueConditionId.MainDamagedAccessKeyActive1,
            DialogueConditionId.MainDamagedAccessKeyActive2,
            DialogueConditionId.MainDamagedAccessKeyReadyToRestore,
            DialogueConditionId.MainDamagedAccessKeyCompleted
        };

        for (int i = 0; i < supportedConditions.Length; i++)
        {
            if (string.Equals(
                    conditionScript,
                    BuildCondition(supportedConditions[i]),
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddLocalizedDialogueFields(
        List<Field> fields,
        LocalizationEntry entry)
    {
        AddLocalizedField(fields, LocalizationLanguageCodes.English, entry.English);
        AddLocalizedField(fields, LocalizationLanguageCodes.Japanese, entry.Japanese);
        AddLocalizedField(
            fields,
            LocalizationLanguageCodes.SimplifiedChinese,
            entry.SimplifiedChinese);
        AddLocalizedField(
            fields,
            LocalizationLanguageCodes.TraditionalChinese,
            entry.TraditionalChinese);
    }

    private static void AddLocalizedField(
        List<Field> fields,
        string languageCode,
        string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Field.SetValue(fields, languageCode, text, FieldType.Localization);
        }
    }

    private static void ApplySpeakerLocalization(
        Actor actor,
        LocalizationEntry entry)
    {
        Field.SetValue(actor.fields, NameTextKeyField, entry.TextKey);
        Field.SetValue(actor.fields, "Display Name", entry.Korean);
        AddLocalizedSpeakerField(
            actor.fields,
            LocalizationLanguageCodes.English,
            entry.English);
        AddLocalizedSpeakerField(
            actor.fields,
            LocalizationLanguageCodes.Japanese,
            entry.Japanese);
        AddLocalizedSpeakerField(
            actor.fields,
            LocalizationLanguageCodes.SimplifiedChinese,
            entry.SimplifiedChinese);
        AddLocalizedSpeakerField(
            actor.fields,
            LocalizationLanguageCodes.TraditionalChinese,
            entry.TraditionalChinese);
    }

    private static void AddLocalizedSpeakerField(
        List<Field> fields,
        string languageCode,
        string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Field.SetValue(
                fields,
                $"Display Name {languageCode}",
                text,
                FieldType.Localization);
        }
    }

    private static LocalizationEntry GetRequiredEntry(
        LocalizationCatalog catalog,
        string textKey)
    {
        catalog.TryGetEntry(textKey, out LocalizationEntry entry);
        return entry;
    }

    private static string[] BuildRequiredKeys()
    {
        List<string> keys = new List<string>(36)
        {
            Phase2CStoryDialogueIds.OperatorSpeakerTextKey,
            Phase2CStoryDialogueIds.FakeOperatorSpeakerTextKey,
            Phase2CStoryDialogueIds.SettlementControlSpeakerTextKey
        };

        keys.Add(Phase2CStoryDialogueIds.TutorialMovementTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRadarTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialSupplyTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialAncientSignalTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRadarObjectiveTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialRouteOptionalTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialUnknownSignalTitleTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialUnknownSignalObjectiveTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialPurpleCoreTravelTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialPurpleCoreInteractTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialCurseErrorTitleTextKey);
        keys.Add(Phase2CStoryDialogueIds.TutorialCurseErrorBodyTextKey);
        AddNumberedKeys(
            keys,
            Phase2CStoryDialogueIds.TutorialSignalRelayAnalysisLinePrefix,
            2);
        AddNumberedKeys(
            keys,
            Phase2CStoryDialogueIds.TutorialFakeOperatorTakeoverLinePrefix,
            1);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.TutorialUnknownLinePrefix, 2);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.TutorialRescueLinePrefix, 3);
        AddNumberedKeys(keys, Phase2CStoryDialogueIds.FirstSettlementLinePrefix, 6);
        keys.Add(Phase2CStoryDialogueIds.MainQuestActive0TextKey);
        keys.Add(Phase2CStoryDialogueIds.MainQuestActive1TextKey);
        keys.Add(Phase2CStoryDialogueIds.MainQuestActive2TextKey);
        keys.Add(Phase2CStoryDialogueIds.MainQuestReadyTextKey);
        keys.Add(Phase2CStoryDialogueIds.MainQuestCompletedTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.TitleTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.StartedNotificationTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.ObjectiveTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.RestorationDescriptionTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.RestorationReadyTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.RestorationCompletedTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.RestorationActionTextKey);
        keys.Add(MainDamagedAccessKeyQuestIds.RestoredNotificationTextKey);
        keys.Add(PixelCurseProgressionIds.LevelUpNotificationTextKey);
        return keys.ToArray();
    }

    private static void AddNumberedKeys(
        List<string> keys,
        string prefix,
        int count)
    {
        for (int i = 1; i <= count; i++)
        {
            keys.Add(Phase2CStoryDialogueIds.GetNumberedTextKey(prefix, i));
        }
    }

    private static int CountConversations(
        DialogueDatabase database,
        string conversationTitle)
    {
        int count = 0;
        for (int i = 0; i < database.conversations.Count; i++)
        {
            Conversation conversation = database.conversations[i];
            if (conversation != null &&
                string.Equals(
                    conversation.Title,
                    conversationTitle,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static Template ResolveTemplate(DialogueDatabase database)
    {
        if (!string.IsNullOrWhiteSpace(database.templateJson))
        {
            Template storedTemplate = JsonUtility.FromJson<Template>(
                database.templateJson);
            if (storedTemplate != null)
            {
                return storedTemplate;
            }
        }

        return Template.FromDefault();
    }

    private static void LoadAssets(
        out DialogueDatabase database,
        out LocalizationCatalog catalog)
    {
        database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(
            RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            RescueContactDialogueInstaller.LocalizationCatalogAssetPath);
    }
}
