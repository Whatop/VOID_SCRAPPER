using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEngine;

public sealed partial class Phase2CStoryDialogueTests
{
    private static SaveData Checkpoint(SaveData source, int index) => (SaveData)typeof(DebugItemGrantUI)
        .GetMethod("CreateCampaignCheckpoint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source, index });

    [TestCase(0, 0, ExpeditionDepth.Normal, RouteCoreState.MissingParts)]
    [TestCase(1, 0, ExpeditionDepth.Normal, RouteCoreState.MissingParts)]
    [TestCase(2, 1, ExpeditionDepth.Normal, RouteCoreState.MissingParts)]
    [TestCase(3, 1, ExpeditionDepth.DeepZone1, RouteCoreState.MissingParts)]
    [TestCase(4, 2, ExpeditionDepth.DeepZone1, RouteCoreState.MissingParts)]
    [TestCase(5, 2, ExpeditionDepth.DeepZone2, RouteCoreState.MissingParts)]
    [TestCase(6, 3, ExpeditionDepth.DeepZone2, RouteCoreState.ReadyToAssemble)]
    [TestCase(7, 3, ExpeditionDepth.DeepZone2, RouteCoreState.Assembled)]
    [TestCase(8, 3, ExpeditionDepth.DeepZone2, RouteCoreState.Activated)]
    [TestCase(9, 3, ExpeditionDepth.DeepZone2, RouteCoreState.Activated)]
    [TestCase(10, 3, ExpeditionDepth.DeepZone2, RouteCoreState.Activated)]
    public void CampaignQA_CheckpointsReplaceForwardAndBackwardStateWithoutLosingOptionalProgress(int index, int parts, ExpeditionDepth depth, RouteCoreState core)
    {
        SaveData source = new SaveData { selectedShipId = "preserved_ship", scrapParts = 77, coreShards = 12, stabilizedAlloy = 9 };
        source.buildingLevels.Add(new BuildingSaveData(BuildingType.Hangar, 2));
        source.traitLevels.Add(new TraitLevelSaveData("optional_trait", 2));
        source.unlockFlags.Add("optional_upgrade_flag");
        source.sectorTechnologyLevels.Add(new SectorTechnologyLevelSaveData(SectorTechnologyCatalog.Definitions[0].Id, 1));
        string original = JsonUtility.ToJson(source);
        PermanentProgress progress = CreateProgress("CampaignPreset");
        foreach (SaveData start in new[] { source, Checkpoint(source, 10) })
        {
            start.unlockFlags.AddRange(new[] { "campaign_boss_sector_administrator_defeated", "campaign_boss_salvage_devourer_defeated", "campaign_boss_phase_gatekeeper_defeated", "campaign_boss_null_dispatcher_defeated", "campaign_route_core_assembled", "campaign_route_core_activated", "campaign_settlement_defense_cleared" });
            int changes = 0;
            Action changed = () => changes++;
            progress.Changed += changed;
            progress.LoadFromSave(Checkpoint(start, index));
            progress.Changed -= changed;
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(parts));
            Assert.That(progress.DefeatedCampaignBosses.Count, Is.EqualTo(parts + (index == 10 ? 1 : 0)));
            Assert.That(progress.HighestUnlockedDepth, Is.EqualTo(depth));
            Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(core));
            Assert.That(progress.CanLaunchFinalExpedition, Is.EqualTo(index >= 9));
            Assert.That(progress.FinalBossDefeated, Is.EqualTo(index == 10));
            Assert.That(progress.HasPendingCampaignRouteAnalysis, Is.EqualTo(index == 2 || index == 4));
            Assert.That(progress.IsTutorialCompleted, Is.True);
            Assert.That(progress.PixelCurseLevel, Is.EqualTo(parts + 1));
            Assert.That(progress.DamagedAccessKeyQuestState == MainDamagedAccessKeyQuestState.NotStarted, Is.EqualTo(index == 0));
            Assert.That(progress.GetBuildingLevel(BuildingType.Hangar), Is.EqualTo(2));
            Assert.That(progress.GetTraitLevel("optional_trait"), Is.EqualTo(2));
            Assert.That(progress.GetSectorTechnologyLevel(SectorTechnologyCatalog.Definitions[0].Id), Is.EqualTo(1));
            Assert.That(progress.SelectedShipId, Is.EqualTo("preserved_ship"));
            Assert.That(progress.HasUnlockFlag("optional_upgrade_flag"), Is.True);
            Assert.That(progress.ScrapParts, Is.EqualTo(77));
            SaveData reloaded = progress.CreateSaveData();
            progress.LoadFromSave(reloaded);
            Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(core));
            Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(parts));
        }
        // The builder never edits its input snapshot.
        source = JsonUtility.FromJson<SaveData>(original);
        Checkpoint(source, index);
        Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
    }

    [Test]
    public void StoryQA_RelayContinuityHijackLatchAndStableSpeakerAreInstalled()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        Assert.That(catalog.TryGetText("dialogue.tutorial.signal_relay_analysis.line_01", "ko", out string relay, out _), Is.True);
        Assert.That(relay, Does.Contain("우회 연결"));
        catalog.TryGetText("dialogue.tutorial.signal_relay_analysis.line_02", "ko", out string hijack, out _);
        Assert.That(hijack, Does.Contain("다른 신호가 채널을 낚아채"));
        catalog.TryGetText("dialogue.tutorial.rescue_after_curse.line_02", "ko", out string rescue, out _);
        Assert.That(rescue, Does.StartWith("중계기를 건드린 뒤부터"));
        Assert.That(rescue, Does.Contain("누가 말한 거야"));
        catalog.TryGetText("speaker.settlement_control.name", "ko", out string control, out _);
        Assert.That(control, Is.EqualTo("관제탑"));
        Assert.That(Phase2CStoryDialogueIds.SettlementControlActorName, Is.EqualTo("Settlement Control"));
        Assert.That(DialoguePresentationPolicy.ResolveActorTheme("", "Settlement Control"), Is.EqualTo(DialogueActorTheme.Settlement));
        Assert.That(SoundEventIds.DialogueCommIncoming, Is.EqualTo("111_dialogue_comm_incoming"));
        Assert.That(SoundEventIds.DialogueCommHijack, Is.EqualTo("112_dialogue_comm_hijack"));
        Assert.That(SoundEventIds.ToNumbered("dialogue_comm_hijack"), Is.EqualTo(SoundEventIds.DialogueCommHijack));
        var state = new TutorialRelayNarrativeProgress();
        state.TryBeginAnalysis(); state.TryCompleteAnalysis(); state.TryBeginRealOperator();
        Assert.That(state.TryMarkFakeTakeoverCuePresented(), Is.False);
        state.TryFinishRealOperator(false); state.TryBeginRealOperator(); state.TryFinishRealOperator(true);
        Assert.That(state.TryMarkFakeTakeoverCuePresented(), Is.True);
        state.TryBeginFakeOperator(); state.TryFinishFakeOperator(false);
        Assert.That(state.TryMarkFakeTakeoverCuePresented(), Is.False);
        state.TryBeginFakeOperator(); state.CancelActivePhase();
        Assert.That(state.TryMarkFakeTakeoverCuePresented(), Is.False);
        string source = File.ReadAllText("Assets/02_Scripts/Tutorial/TutorialFlowController.cs");
        int latch = source.IndexOf("relayNarrativeProgress.TryMarkFakeTakeoverCuePresented()", StringComparison.Ordinal);
        int cue = source.IndexOf("AudioManager.Play(SoundEventIds.DialogueCommHijack)", StringComparison.Ordinal);
        Assert.That(cue, Is.GreaterThan(latch));
        Assert.That(cue, Is.LessThan(source.IndexOf("TryStartRelayConversation(", latch, StringComparison.Ordinal)));
        Assert.That(source.LastIndexOf("AudioManager.Play(SoundEventIds.DialogueCommHijack)", StringComparison.Ordinal), Is.EqualTo(cue));
        Assert.That(source, Does.Contain("AudioManager.PlayAt(SoundEventIds.EventStart"));
        var library = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(FinalAudioIntegrationInstaller.LibraryPath);
        Assert.That(library.TryGet(SoundEventIds.DialogueCommHijack, out AudioEventDefinition definition), Is.True);
        // Empty is intentional until the user supplies a clip; later valid assignments are supported.
        foreach (AudioClip clip in definition.Clips) Assert.That(clip, Is.Not.Null);
        Assert.That(definition.SpatialMode, Is.EqualTo(AudioSpatialMode.Force2D));
    }

    [TestCase(0, 8)] [TestCase(1, 9)] [TestCase(2, 9)] [TestCase(3, 10)]
    [TestCase(4, 10)] [TestCase(5, 11)] [TestCase(6, 12)] [TestCase(7, 13)]
    [TestCase(9, 13)] [TestCase(10, 14)]
    public void DialogueArchive_GatesSafeRecordsWithoutExecutingOrInventingTreatmentHistory(int checkpoint, int available)
    {
        PermanentProgress progress = CreateProgress("ArchiveProgress");
        progress.LoadFromSave(Checkpoint(new SaveData(), checkpoint));
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        var database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        int count = 0;
        foreach (var record in SettlementDialogueArchivePanelUI.Records)
        {
            int index = Array.IndexOf(SettlementDialogueArchivePanelUI.Records, record);
            Assert.That(record.Conversation, Is.Not.EqualTo(NullDispatcherDialogueIds.Conversation));
            if (!SettlementDialogueArchivePanelUI.IsAvailable(index, progress)) continue;
            count++;
            foreach (string language in new[] { "ko", "en" })
            {
                string transcript = SettlementDialogueArchivePanelUI.BuildTranscript(database, catalog, record, language, "CustomRadar", "CustomScan");
                Assert.That(transcript, Is.Not.Empty);
                Assert.That(transcript, Does.Not.Contain("VS_DispatchAction").And.Not.Contain("VS_MarkConversationComplete").And.Not.Contain("[var="));
                if (record.Key == "radar") Assert.That(transcript, Does.Contain("CustomRadar").And.Contain("CustomScan"));
            }
        }
        Assert.That(count, Is.EqualTo(available));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        string archiveSource = File.ReadAllText("Assets/02_Scripts/Settlement/SettlementDialogueArchivePanelUI.cs");
        foreach (string forbidden in new[] { "StartConversation(", "Lua.Run(", "DispatchAction(", "MarkConversationComplete(", "LoadFromSave(", "AddUnlockFlag(" })
            Assert.That(archiveSource, Does.Not.Contain(forbidden));
    }
}
