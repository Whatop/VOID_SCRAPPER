using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;

public static class RescueContactDialogueInstaller
{
    public const string DialogueDatabaseAssetPath =
        "Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset";
    public const string LocalizationCatalogAssetPath =
        "Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset";

    private const string TextKeyField = "TextKey";
    private const string MenuTextKeyField = "Menu TextKey";
    private const string NameTextKeyField = "Name TextKey";

    [MenuItem("VOID SCRAPPER/Dialogue/Install RescueContact Service")]
    public static void InstallFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Install RescueContact Dialogue",
                "Replace only the entries in NPC_RescueContact_Service with the " +
                "validated Phase 2B vertical slice?",
                "Install",
                "Cancel"))
        {
            return;
        }

        DialogueDatabase database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(
            DialogueDatabaseAssetPath);
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationCatalogAssetPath);

        if (!TryInstall(database, catalog, out string result))
        {
            Debug.LogError($"[Dialogue Phase 2B] {result}");
            return;
        }

        Debug.Log($"[Dialogue Phase 2B] {result}", database);
    }

    [MenuItem("VOID SCRAPPER/Dialogue/Validate RescueContact Service")]
    public static void ValidateFromMenu()
    {
        DialogueDatabase database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(
            DialogueDatabaseAssetPath);
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationCatalogAssetPath);

        if (!TryBuildEntries(database, catalog, out _, out string result))
        {
            Debug.LogError($"[Dialogue Phase 2B] {result}");
            return;
        }

        Debug.Log($"[Dialogue Phase 2B] Source validation passed. {result}", database);
    }

    public static bool TryInstall(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        out string result)
    {
        if (!TryBuildEntries(database, catalog, out List<DialogueEntry> entries, out result))
        {
            return false;
        }

        Conversation conversation = database.GetConversation(
            RescueContactDialogueIds.ConversationTitle);
        Actor rescueContactActor = database.GetActor(conversation.ConversantID);

        if (!TryGetRequiredEntry(
                catalog,
                RescueContactDialogueIds.SpeakerNameTextKey,
                out LocalizationEntry speakerEntry,
                out result))
        {
            return false;
        }

        Undo.RecordObject(database, "Install RescueContact dialogue vertical slice");
        ApplySpeakerLocalization(rescueContactActor, speakerEntry);
        conversation.dialogueEntries = entries;
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        result = $"Installed '{RescueContactDialogueIds.ConversationTitle}' with " +
                 $"{entries.Count} validated entries. No other conversation was changed.";
        return true;
    }

    public static bool TryBuildEntries(
        DialogueDatabase database,
        LocalizationCatalog catalog,
        out List<DialogueEntry> entries,
        out string result)
    {
        entries = null;

        if (database == null)
        {
            result = $"Dialogue Database is missing at '{DialogueDatabaseAssetPath}'.";
            return false;
        }

        if (catalog == null)
        {
            result = $"LocalizationCatalog is missing at '{LocalizationCatalogAssetPath}'.";
            return false;
        }

        Conversation conversation = database.GetConversation(
            RescueContactDialogueIds.ConversationTitle);

        if (conversation == null)
        {
            result = $"Conversation '{RescueContactDialogueIds.ConversationTitle}' is missing.";
            return false;
        }

        if (database.GetActor(conversation.ActorID) == null ||
            database.GetActor(conversation.ConversantID) == null)
        {
            result = "The RescueContact conversation has an invalid Actor or Conversant ID.";
            return false;
        }

        string[] requiredKeys =
        {
            RescueContactDialogueIds.SpeakerNameTextKey,
            RescueContactDialogueIds.Line01TextKey,
            RescueContactDialogueIds.Line02TextKey,
            RescueContactDialogueIds.AcceptChoiceTextKey,
            RescueContactDialogueIds.DeclineChoiceTextKey,
            RescueContactDialogueIds.UnavailableTextKey
        };

        LocalizationEntry[] localizedEntries = new LocalizationEntry[requiredKeys.Length];

        for (int i = 0; i < requiredKeys.Length; i++)
        {
            if (!TryGetRequiredEntry(
                    catalog,
                    requiredKeys[i],
                    out localizedEntries[i],
                    out result))
            {
                return false;
            }
        }

        int conversationId = conversation.id;
        int playerActorId = conversation.ActorID;
        int rescueContactActorId = conversation.ConversantID;

        DialogueEntry root = CreateEntry(
            0,
            conversationId,
            playerActorId,
            rescueContactActorId,
            true,
            "START",
            null,
            false);
        DialogueEntry line01 = CreateEntry(
            1,
            conversationId,
            rescueContactActorId,
            playerActorId,
            false,
            RescueContactDialogueIds.Line01TextKey,
            localizedEntries[1],
            false);
        DialogueEntry line02 = CreateEntry(
            2,
            conversationId,
            rescueContactActorId,
            playerActorId,
            false,
            RescueContactDialogueIds.Line02TextKey,
            localizedEntries[2],
            false);
        DialogueEntry accept = CreateEntry(
            3,
            conversationId,
            playerActorId,
            rescueContactActorId,
            false,
            RescueContactDialogueIds.AcceptChoiceTextKey,
            localizedEntries[3],
            true);
        DialogueEntry decline = CreateEntry(
            4,
            conversationId,
            playerActorId,
            rescueContactActorId,
            false,
            RescueContactDialogueIds.DeclineChoiceTextKey,
            localizedEntries[4],
            true);
        DialogueEntry unavailableChoice = CreateEntry(
            5,
            conversationId,
            playerActorId,
            rescueContactActorId,
            false,
            RescueContactDialogueIds.AcceptChoiceTextKey,
            localizedEntries[3],
            true);
        DialogueEntry unavailableResponse = CreateEntry(
            6,
            conversationId,
            rescueContactActorId,
            playerActorId,
            false,
            RescueContactDialogueIds.UnavailableTextKey,
            localizedEntries[5],
            false);

        root.outgoingLinks.Add(CreateLink(root, line01));
        line01.outgoingLinks.Add(CreateLink(line01, line02));
        line02.outgoingLinks.Add(CreateLink(line02, accept));
        line02.outgoingLinks.Add(CreateLink(line02, decline));
        line02.outgoingLinks.Add(CreateLink(line02, unavailableChoice));
        unavailableChoice.outgoingLinks.Add(CreateLink(unavailableChoice, unavailableResponse));

        string conditionCall =
            $"{DialoguePixelCrushersBridge.ConditionLuaFunction}(\"" +
            $"{DialogueConditionId.RescueContactAvailable}\")";
        accept.conditionsString = conditionCall;
        accept.userScript =
            $"{DialoguePixelCrushersBridge.ActionLuaFunction}(\"" +
            $"{DialogueGameplayActionId.RescueContactAccept}\")";
        unavailableChoice.conditionsString = $"not {conditionCall}";

        entries = new List<DialogueEntry>
        {
            root,
            line01,
            line02,
            accept,
            decline,
            unavailableChoice,
            unavailableResponse
        };

        if (!ValidateBuiltEntries(entries, conversationId, out result))
        {
            entries = null;
            return false;
        }

        result = $"Conversation={RescueContactDialogueIds.ConversationTitle}, " +
                 $"Entries={entries.Count}, LocalizationKeys={requiredKeys.Length}.";
        return true;
    }

    private static DialogueEntry CreateEntry(
        int id,
        int conversationId,
        int actorId,
        int conversantId,
        bool isRoot,
        string textKey,
        LocalizationEntry localizedEntry,
        bool isChoice)
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
            canvasRect = new Rect(400f + id * 40f, 30f + id * 60f, 160f, 30f)
        };

        Field.SetValue(entry.fields, "Title", isRoot ? "START" : string.Empty);
        Field.SetValue(entry.fields, "Description", string.Empty);
        Field.SetValue(entry.fields, "Actor", actorId.ToString(), FieldType.Actor);
        Field.SetValue(entry.fields, "Conversant", conversantId.ToString(), FieldType.Actor);
        Field.SetValue(entry.fields, "Menu Text", string.Empty);
        Field.SetValue(entry.fields, "Dialogue Text", string.Empty);
        Field.SetValue(entry.fields, "Sequence", isRoot ? "None()" : string.Empty);

        if (localizedEntry == null || string.IsNullOrWhiteSpace(textKey))
        {
            return entry;
        }

        Field.SetValue(entry.fields, TextKeyField, textKey);
        Field.SetValue(entry.fields, "Dialogue Text", localizedEntry.Korean);
        AddLocalizedDialogueFields(entry.fields, localizedEntry, false);

        if (isChoice)
        {
            Field.SetValue(entry.fields, MenuTextKeyField, textKey);
            Field.SetValue(entry.fields, "Menu Text", localizedEntry.Korean);
            AddLocalizedDialogueFields(entry.fields, localizedEntry, true);
        }

        return entry;
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

    private static void AddLocalizedDialogueFields(
        List<Field> fields,
        LocalizationEntry entry,
        bool menuText)
    {
        AddLocalizedField(fields, LocalizationLanguageCodes.English, entry.English, menuText);
        AddLocalizedField(fields, LocalizationLanguageCodes.Japanese, entry.Japanese, menuText);
        AddLocalizedField(
            fields,
            LocalizationLanguageCodes.SimplifiedChinese,
            entry.SimplifiedChinese,
            menuText);
        AddLocalizedField(
            fields,
            LocalizationLanguageCodes.TraditionalChinese,
            entry.TraditionalChinese,
            menuText);
    }

    private static void AddLocalizedField(
        List<Field> fields,
        string languageCode,
        string text,
        bool menuText)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string fieldTitle = menuText
            ? $"Menu Text {languageCode}"
            : languageCode;
        Field.SetValue(fields, fieldTitle, text, FieldType.Localization);
    }

    private static void ApplySpeakerLocalization(Actor actor, LocalizationEntry entry)
    {
        Field.SetValue(actor.fields, NameTextKeyField, entry.TextKey);
        Field.SetValue(actor.fields, "Display Name", entry.Korean);
        AddLocalizedSpeakerField(actor.fields, LocalizationLanguageCodes.English, entry.English);
        AddLocalizedSpeakerField(actor.fields, LocalizationLanguageCodes.Japanese, entry.Japanese);
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

    private static bool TryGetRequiredEntry(
        LocalizationCatalog catalog,
        string textKey,
        out LocalizationEntry entry,
        out string result)
    {
        if (!catalog.TryGetEntry(textKey, out entry) || entry == null)
        {
            result = $"Localization key '{textKey}' is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Korean))
        {
            result = $"Localization key '{textKey}' has empty Korean source text.";
            return false;
        }

        result = string.Empty;
        return true;
    }

    private static bool ValidateBuiltEntries(
        IReadOnlyList<DialogueEntry> entries,
        int conversationId,
        out string result)
    {
        if (entries == null || entries.Count != 7)
        {
            result = "The generated RescueContact graph must contain exactly 7 entries.";
            return false;
        }

        bool[] foundIds = new bool[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            DialogueEntry entry = entries[i];

            if (entry == null || entry.conversationID != conversationId ||
                entry.id < 0 || entry.id >= foundIds.Length || foundIds[entry.id])
            {
                result = "The generated RescueContact graph has an invalid or duplicate entry ID.";
                return false;
            }

            foundIds[entry.id] = true;

            for (int linkIndex = 0; linkIndex < entry.outgoingLinks.Count; linkIndex++)
            {
                Link link = entry.outgoingLinks[linkIndex];

                if (link.originConversationID != conversationId ||
                    link.destinationConversationID != conversationId ||
                    link.destinationDialogueID < 0 ||
                    link.destinationDialogueID >= entries.Count)
                {
                    result = "The generated RescueContact graph contains a broken link.";
                    return false;
                }
            }
        }

        if (!entries[0].isRoot || !string.Equals(entries[0].Title, "START", StringComparison.Ordinal))
        {
            result = "The generated RescueContact graph has no valid START entry.";
            return false;
        }

        if (!string.IsNullOrEmpty(entries[4].userScript))
        {
            result = "The decline choice must not execute a gameplay action.";
            return false;
        }

        result = string.Empty;
        return true;
    }
}
