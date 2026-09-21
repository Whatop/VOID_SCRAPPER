using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable authoring only. Runtime playback stays in AudioManager.
public static class FinalAudioIntegrationInstaller
{
    public const string LibraryPath = "Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset";
    public const string RemoteBossPrefabPath = "Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab";

    private static readonly string[] RestoredClipNames =
    {
        "trait_select", "trait_select_02", "trait_select_03",
        "reinforcement_equip", "reinforcement_equip_02", "reinforcement_equip_03",
        "reinforcement_drop", "reinforcement_drop_02", "reinforcement_drop_03",
        "reinforcement_pickup", "reinforcement_pickup_02",
        "tab_status_open", "tab_status_open_02", "tab_status_close", "tab_status_close_02",
        "warning_message", "warning_message_02", "action_denied", "action_denied_02", "action_denied_03"
    };

    [MenuItem("VOID SCRAPPER/Audio/Apply Final Audio Integration")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureNewImports();
        AudioEventDatabase library = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(LibraryPath);
        if (library == null)
        {
            throw new InvalidOperationException("Missing existing SoundEventLibrary.");
        }

        var serialized = new SerializedObject(library);
        SerializedProperty entries = serialized.FindProperty("entries");
        Remove(entries, "19_shop_item_sold");
        Remove(entries, "20_shop_transaction_complete");
        Remove(entries, "41_level_up");
        Remove(entries, "49_ship_dash_end");
        SerializedProperty oldChip = Find(entries, "40_pickup_experience");
        if (oldChip != null)
        {
            if (Find(entries, SoundEventIds.PickupTuningChip) != null)
            {
                throw new InvalidOperationException("Both old and new event 40 exist; resolve before applying.");
            }
            oldChip.FindPropertyRelative("eventId").stringValue = SoundEventIds.PickupTuningChip;
        }

        Restore(entries, SoundEventIds.TraitSelect, "trait_select", "trait_select_02", "trait_select_03");
        Restore(entries, SoundEventIds.ReinforcementEquip, "reinforcement_equip", "reinforcement_equip_02", "reinforcement_equip_03");
        Restore(entries, SoundEventIds.ReinforcementDrop, "reinforcement_drop", "reinforcement_drop_02", "reinforcement_drop_03");
        Restore(entries, SoundEventIds.ReinforcementPickup, "reinforcement_pickup", "reinforcement_pickup_02", "Pickups/pickup_scrap.wav");
        Restore(entries, SoundEventIds.TabStatusOpen, "tab_status_open", "tab_status_open_02");
        Restore(entries, SoundEventIds.TabStatusClose, "tab_status_close", "tab_status_close_02");
        Restore(entries, SoundEventIds.WarningMessage, "warning_message", "warning_message_02");
        Restore(entries, SoundEventIds.ActionDenied, "action_denied", "action_denied_02", "action_denied_03");

        Configure(entries, SoundEventIds.UiSettings, "UI/ui_confirm_01.wav", 0.55f, 1f, 1f, 128, 1, 0.15f);
        Configure(entries, SoundEventIds.ShopOpen, "Shop/shop_open.wav", 0.55f, 0.99f, 1.01f, 100, 1, 0.2f);
        Configure(entries, SoundEventIds.ShopBuyFail, "Shop/shop_buy_fail.wav", 0.5f, 1f, 1f, 96, 1, 0.15f);
        Configure(entries, SoundEventIds.ShopHostile, "Shop/shop_hostile_03.ogg", 0.55f, 0.98f, 1.02f, 48, 1, 0.5f, true, 3f, 22f);
        Configure(entries, SoundEventIds.PickupScrap, "Pickups/pickup_scrap_03.ogg", 0.38f, 0.96f, 1.05f, 128, 3, 0.08f);
        Configure(entries, SoundEventIds.PickupCore, "Pickups/pickup_core.wav", 0.55f, 0.99f, 1.02f, 80, 2, 0.15f);
        Configure(entries, SoundEventIds.PickupTuningChip, "UI/reinforcement_equip.wav", 0.48f, 1.03f, 1.06f, 96, 2, 0.12f);
        Configure(entries, SoundEventIds.ReturnBeaconSpawn, "Return/return_beacon_spawn_03.ogg", 0.58f, 0.98f, 1.02f, 64, 1, 0.4f, true, 3f, 24f);
        Configure(entries, SoundEventIds.WormholeEnter, "Return/wormhole_enter.ogg", 0.62f, 0.98f, 1.02f, 48, 1, 0.4f, true, 3f, 24f);
        Configure(entries, SoundEventIds.MapRoutePlaced, "Events/event_start.wav", 0.38f, 1f, 1.02f, 112, 2, 0.08f);
        Configure(entries, SoundEventIds.MapRouteRemoved, "UI/tab_status_close.wav", 0.3f, 0.98f, 1f, 128, 2, 0.08f);
        Configure(entries, SoundEventIds.MissionReceived, "Events/event_start.wav", 0.58f, 1f, 1f, 64, 1, 0.35f);
        Configure(entries, SoundEventIds.DialogueCommIncoming, "Radar/radar_scan_pulse.wav", 0.6f, 1f, 1f, 32, 1, 1f);

        // 95 is deliberately unresolved; no verified Settlement ambience source.
        // Do not turn a music track or an unverified Sci-Fi sound into ambience.
        ValidateSerializedEntries(entries);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(library);
        AuthorRemoteBossOptIn();
        Debug.Log("Final audio integration applied: 12 previously empty events filled, " +
            "8 broken events restored, communication 111 added; Settlement ambience remains unresolved.");
    }

    private static void ConfigureNewImports()
    {
        foreach (string name in RestoredClipNames)
        {
            string path = "Assets/06_Audio/SFX/UI/" + name + ".wav";
            RequireClip(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Missing Unity audio importer: " + path);
            }
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
    }

    private static void AuthorRemoteBossOptIn()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RemoteBossPrefabPath);
        try
        {
            var entry = root.GetComponent<DialogueStoryEntryPoint>();
            if (entry == null)
            {
                throw new InvalidOperationException("Missing existing NULL DISPATCHER dialogue entry.");
            }
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("playIncomingCommunicationCue").boolValue = true;
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                PrefabUtility.SaveAsPrefabAsset(root, RemoteBossPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Restore(SerializedProperty entries, string id, params string[] clipNames)
    {
        SerializedProperty entry = Find(entries, id);
        if (entry == null)
        {
            throw new InvalidOperationException("Missing existing event: " + id);
        }
        SerializedProperty clips = entry.FindPropertyRelative("clips");
        clips.arraySize = clipNames.Length;
        for (int i = 0; i < clipNames.Length; i++)
        {
            string path = clipNames[i].Contains("/")
                ? "Assets/06_Audio/SFX/" + clipNames[i]
                : "Assets/06_Audio/SFX/UI/" + clipNames[i] + ".wav";
            clips.GetArrayElementAtIndex(i).objectReferenceValue = RequireClip(path);
        }
    }

    private static void Configure(SerializedProperty entries, string id, string clipPath,
        float volume, float pitchMin, float pitchMax, int priority, int voices, float interval,
        bool world = false, float minDistance = 1.5f, float maxDistance = 14f)
    {
        SerializedProperty entry = Find(entries, id);
        if (entry == null)
        {
            int index = entries.arraySize++;
            entry = entries.GetArrayElementAtIndex(index);
        }
        entry.FindPropertyRelative("eventId").stringValue = id;
        SerializedProperty clips = entry.FindPropertyRelative("clips");
        clips.arraySize = 1;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = RequireClip("Assets/06_Audio/SFX/" + clipPath);
        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("pitchMin").floatValue = pitchMin;
        entry.FindPropertyRelative("pitchMax").floatValue = pitchMax;
        entry.FindPropertyRelative("loop").boolValue = false;
        entry.FindPropertyRelative("priority").intValue = priority;
        entry.FindPropertyRelative("spatialMode").enumValueIndex = (int)(world ? AudioSpatialMode.World2D : AudioSpatialMode.Force2D);
        entry.FindPropertyRelative("spatialBlend").floatValue = world ? 0.25f : 0f;
        entry.FindPropertyRelative("minDistance").floatValue = minDistance;
        entry.FindPropertyRelative("maxDistance").floatValue = maxDistance;
        entry.FindPropertyRelative("rolloffMode").enumValueIndex = (int)AudioRolloffMode.Linear;
        entry.FindPropertyRelative("dopplerLevel").floatValue = 0f;
        entry.FindPropertyRelative("hardCullOutsideMaxDistance").boolValue = true;
        entry.FindPropertyRelative("useOcclusion").boolValue = world;
        entry.FindPropertyRelative("oneOccluderVolumeMultiplier").floatValue = 0.6f;
        entry.FindPropertyRelative("multipleOccluderVolumeMultiplier").floatValue = 0.3f;
        entry.FindPropertyRelative("oneOccluderLowPassCutoff").floatValue = 3500f;
        entry.FindPropertyRelative("multipleOccluderLowPassCutoff").floatValue = 1500f;
        entry.FindPropertyRelative("maxSimultaneousVoices").intValue = voices;
        entry.FindPropertyRelative("minimumRetriggerInterval").floatValue = interval;
    }

    private static AudioClip RequireClip(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null || clip.length <= 0f || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
        {
            throw new InvalidOperationException("Unity must import a valid AudioClip first: " + path);
        }
        return clip;
    }

    private static SerializedProperty Find(SerializedProperty entries, string id)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("eventId").stringValue == id)
            {
                return entry;
            }
        }
        return null;
    }

    private static void Remove(SerializedProperty entries, string id)
    {
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("eventId").stringValue == id)
            {
                entries.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private static void ValidateSerializedEntries(SerializedProperty entries)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("eventId").stringValue;
            if (!ids.Add(id) || !SoundEventIds.IsKnown(id))
            {
                throw new InvalidOperationException("Duplicate or unknown audio event: " + id);
            }
            SerializedProperty clips = entry.FindPropertyRelative("clips");
            if (clips.arraySize == 0 && id != SoundEventIds.AmbSettlementLoop)
            {
                throw new InvalidOperationException("Unexpected empty event: " + id);
            }
            for (int j = 0; j < clips.arraySize; j++)
            {
                if (!(clips.GetArrayElementAtIndex(j).objectReferenceValue is AudioClip clip) || clip.length <= 0f)
                {
                    throw new InvalidOperationException("Unresolved AudioClip in " + id);
                }
                string path = AssetDatabase.GetAssetPath(clip);
                if (path.Contains("ImportedSource") || path.Contains("모음집"))
                {
                    throw new InvalidOperationException("Source compilation/candidate assigned: " + path);
                }
            }
        }
        if (ids.Count != SoundEventIds.NumberedEventMappings.Count)
        {
            throw new InvalidOperationException("Library and primary mappings disagree.");
        }
    }
}
