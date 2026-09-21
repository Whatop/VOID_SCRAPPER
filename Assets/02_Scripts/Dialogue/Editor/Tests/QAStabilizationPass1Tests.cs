using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TMPro;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class QAStabilizationPass1Tests
{
    private readonly List<Object> createdObjects = new List<Object>();

    private NullDispatcherEndingPresentation CreateEndingFixture(out PlayerController2D player)
    {
        var boss = CreateReclaimFixture(20f, false, out player, out _, out _, out _);
        boss.CancelEncounter();
        var ending = boss.gameObject.AddComponent<NullDispatcherEndingPresentation>();
        var inner = CreateInactiveHotfixObject("Ending inner core fixture").AddComponent<SpriteRenderer>();
        inner.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        inner.color = new Color(0.85f, 0.2f, 1f);
        inner.enabled = false; // Existing BossDeathPresentation's terminal state.
        SetPrivateField(ending, "innerCore", inner);
        SetPrivateField(ending, "outerShellRoot", CreateInactiveHotfixObject("Broken shell fixture"));
        SetPrivateField(ending, "shutdownPulse", CreateInactiveHotfixObject("Shutdown pulse fixture").AddComponent<SpriteRenderer>());
        SetPrivateField(ending, "player", player);
        return ending;
    }

    [Test]
    public void NullDispatcher4E_CameraCleanupPreservesForeignFocusAndOffsetOwner()
    {
        var ending = CreateEndingFixture(out _);
        var camera = CreateInactiveHotfixObject("Ending camera fixture").AddComponent<GungeonStyleCamera2D>();
        var foreign = new object();
        SetPrivateField(camera, "cinematicFocusOwner", foreign);
        SetPrivateField(camera, "cinematicFocusActive", true);
        camera.SetCinematicInputOffsetLocked(foreign, true);
        SetPrivateField(ending, "cameraRig", camera);
        InvokeHotfix(ending, "BeginPresentation");
        Assert.That(camera.IsCinematicFocusOwnedBy(foreign), Is.True);
        ending.CancelPresentation();
        ending.CancelPresentation();
        Assert.That(camera.IsCinematicFocusOwnedBy(foreign), Is.True);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.True);
        camera.SetCinematicInputOffsetLocked(foreign, false);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.False);
    }

    [TestCase(GameState.ExpeditionLoading)]
    [TestCase(GameState.Settlement)]
    [TestCase(GameState.RunResult)]
    public void NullDispatcher4E_SceneExitStopsTweenAndPreventsDialogueRetry(GameState next)
    {
        using var scope = new ManualRecoveryTweenScope();
        var ending = CreateEndingFixture(out var player);
        var routine = ending.PlayRoutine();
        Assert.That(routine.MoveNext(), Is.True);
        var tween = (Tween)GetHotfixField(ending, "visual");
        scope.Own(tween);
        InvokeHotfix(ending, "HandleStateChanged", GameState.FinalBossBattle, next);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(ending.Canceled, Is.True);
        Assert.That(ending.Completed, Is.False);
        Assert.That(InvokeHotfix(ending, "TryRestartInterruptedDialogue"), Is.EqualTo(false));
        Assert.That(player.ControlEnabled, Is.True);
    }

    [Test]
    public void NullDispatcher4E_AcceptedDeathClearsCombatBeforeDeathPresentation()
    {
        var combat = CreatePolarityFixture(false, out var hp, out _);
        CompleteRequiredFinalGate(combat);
        var hostile = CreatePolarityPacket(combat, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        var support = CreatePolarityPacket(combat, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        var death = combat.gameObject.AddComponent<BossDummyController>();
        death.ConfigureCampaignDefinition(AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(
            "Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Final.asset"));
        // Pure death-order fixture supplies the resolved FinalNetwork context. The
        // real RunManager/depth resolution is exercised by the victory integration test.
        SetPrivateField(death, "resolvedDeathBossId", CampaignBossId.NullDispatcher);
        Assert.That(InvokeHotfix(death, "AcceptBossDeath"), Is.EqualTo(true));
        Assert.That(combat.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
        Assert.That(combat.PatternDamageEnabled, Is.False);
        Assert.That(combat.ActiveMajorMechanics, Is.Zero);
        Assert.That(hostile.TryConsume(hp), Is.False);
        Assert.That(support.TryConsume(hp), Is.False);
        Assert.That(InvokeHotfix(death, "AcceptBossDeath"), Is.EqualTo(false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4E_EndingCancelsDashAndReleasesOnlyOwnedLocks(bool foreignOwner)
    {
        var ending = CreateEndingFixture(out var player);
        var weapon = player.GetComponent<PlayerWeaponController>();
        var dash = player.GetComponent<PlayerDash>();
        var owner = new object();
        player.SetExternalControlLocked(owner, foreignOwner);
        weapon.SetExternalInputLocked(owner, foreignOwner);
        SetPrivateField(dash, "isDashing", true);
        player.SetMovementLocked(true); // A live dash owns this transient flag.
        InvokeHotfix(ending, "BeginPresentation");
        Assert.That(dash.IsDashing, Is.False);
        Assert.That(player.MovementLocked, Is.False);
        Assert.That(player.ControlEnabled, Is.False);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        ending.CancelPresentation();
        ending.CancelPresentation();
        Assert.That(ending.Canceled, Is.True);
        Assert.That(ending.Completed, Is.False);
        Assert.That(ending.IsPlaying, Is.False);
        Assert.That(player.ControlEnabled, Is.EqualTo(!foreignOwner));
        Assert.That(weapon.ExternalInputLocked, Is.EqualTo(foreignOwner));
        Assert.That(player.enabled && dash.enabled && weapon.enabled, Is.True);
        player.SetExternalControlLocked(owner, false);
        weapon.SetExternalInputLocked(owner, false);
    }

    [Test]
    public void NullDispatcher4E_InnerCoreSurvivesDeathPresentationButShellStaysBroken()
    {
        var ending = CreateEndingFixture(out _);
        InvokeHotfix(ending, "BeginPresentation");
        var inner = (SpriteRenderer)GetHotfixField(ending, "innerCore");
        Assert.That(inner.enabled && inner.gameObject.activeSelf, Is.True);
        Assert.That(((GameObject)GetHotfixField(ending, "outerShellRoot")).activeSelf, Is.False);
        Assert.That(ending.Completed, Is.False);
        Assert.That(ending.NaturallyCompletedDialogue, Is.False);
        Assert.That(ending.GetComponent<NullDispatcherBossController>().Phase,
            Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4E_OnlyNaturalDialogueOrExplicitFailureAllowsShutdown(bool natural)
    {
        using var scope = new ManualRecoveryTweenScope();
        var ending = CreateEndingFixture(out _);
        InvokeHotfix(ending, "BeginPresentation");
        SetPrivateField(ending, "awaitingDialogue", true);
        InvokeHotfix(ending, "RecordDialogueCompletion", natural);
        InvokeHotfix(ending, "BeginStabilization");
        scope.Own((Tween)GetHotfixField(ending, "visual"));
        Assert.That(GetHotfixField(ending, "stabilizationStarted"), Is.EqualTo(natural));
        Assert.That(ending.Completed, Is.False, "Dialogue alone never finalizes the ending.");
        Assert.That(ending.UsedDialogueFallback, Is.False);
        InvokeHotfix(ending, "CompletePresentation");
        Assert.That(ending.Completed, Is.EqualTo(natural));
    }

    [Test]
    public void NullDispatcher4E_InterruptedDialogueReoffersOnceWithoutSuccessThenFailsSafely()
    {
        var ending = CreateEndingFixture(out _);
        InvokeHotfix(ending, "BeginPresentation");
        Assert.That(InvokeHotfix(ending, "TryRestartInterruptedDialogue"), Is.EqualTo(true));
        Assert.That(ending.Completed || ending.NaturallyCompletedDialogue || ending.UsedDialogueFallback, Is.False);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            "Ending conversation was interrupted again after its one restart"));
        Assert.That(InvokeHotfix(ending, "TryRestartInterruptedDialogue"), Is.EqualTo(false));
        Assert.That(ending.UsedDialogueFallback, Is.True);
        Assert.That(ending.Completed, Is.False);
        ending.CancelPresentation();
        Assert.That(InvokeHotfix(ending, "TryRestartInterruptedDialogue"), Is.EqualTo(false));
    }

    [Test]
    public void NullDispatcher4E_MissingDialogueStartRetriesThenAllowsControlledFallback()
    {
        using var scope = new ManualRecoveryTweenScope();
        var ending = CreateEndingFixture(out _);
        InvokeHotfix(ending, "BeginPresentation");
        var dialogue = (System.Collections.IEnumerator)InvokeHotfix(ending, "PlayDialogue");
        Assert.That(dialogue.MoveNext(), Is.True);
        Assert.That(dialogue.MoveNext(), Is.True);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            "Ending conversation could not start after three attempts"));
        Assert.That(dialogue.MoveNext(), Is.False);
        Assert.That(ending.UsedDialogueFallback, Is.True);
        Assert.That(ending.NaturallyCompletedDialogue, Is.False);
        Assert.That(ending.Completed, Is.False);
        InvokeHotfix(ending, "BeginStabilization");
        scope.Own((Tween)GetHotfixField(ending, "visual"));
        InvokeHotfix(ending, "CompletePresentation");
        Assert.That(ending.Completed, Is.True);
    }

    [Test]
    public void NullDispatcher4E_ShutdownAndTweenCleanupAreExactOnce()
    {
        using var scope = new ManualRecoveryTweenScope();
        var ending = CreateEndingFixture(out var player);
        int pulses = 0;
        var shutdown = new UnityEngine.Events.UnityEvent();
        shutdown.AddListener(() => pulses++);
        SetPrivateField(ending, "networkShutdown", shutdown);
        InvokeHotfix(ending, "BeginPresentation");
        SetPrivateField(ending, "awaitingDialogue", true);
        InvokeHotfix(ending, "RecordDialogueCompletion", true);
        InvokeHotfix(ending, "BeginStabilization");
        var tween = (Tween)GetHotfixField(ending, "visual");
        scope.Own(tween);
        tween.Goto(0.2f);
        InvokeHotfix(ending, "BeginStabilization");
        Assert.That(pulses, Is.EqualTo(1));
        InvokeHotfix(ending, "CompletePresentation");
        InvokeHotfix(ending, "CompletePresentation");
        ending.CancelPresentation();
        Assert.That(ending.Completed, Is.True);
        Assert.That(ending.Canceled, Is.False);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(GetHotfixField(ending, "visual"), Is.Null);
        Assert.That(((SpriteRenderer)GetHotfixField(ending, "innerCore")).enabled, Is.False);
        Assert.That(((SpriteRenderer)GetHotfixField(ending, "innerCore")).transform.localScale,
            Is.EqualTo(new Vector3(2.4f, 2.4f, 1f)));
        Assert.That(player.ControlEnabled, Is.True);
        Assert.That(ending.PlayRoutine().MoveNext(), Is.False, "A completed ending cannot replay.");
    }

    [Test]
    public void NullDispatcher4E_ExternalRunEndCancelsAndCannotResumeVictory()
    {
        var ending = CreateEndingFixture(out var player);
        InvokeHotfix(ending, "BeginPresentation");
        InvokeHotfix(ending, "HandleRunEnded", new object[] { null });
        SetPrivateField(ending, "awaitingDialogue", true);
        InvokeHotfix(ending, "RecordDialogueCompletion", true);
        InvokeHotfix(ending, "CompletePresentation");
        Assert.That(ending.Completed || ending.NaturallyCompletedDialogue, Is.False);
        Assert.That(ending.Canceled, Is.True);
        Assert.That(player.ControlEnabled, Is.True);
        Assert.That(ending.PlayRoutine().MoveNext(), Is.False);
    }

    [TestCase(CampaignBossId.SectorAdministrator, false)]
    [TestCase(CampaignBossId.SalvageDevourer, false)]
    [TestCase(CampaignBossId.PhaseGatekeeper, false)]
    [TestCase(CampaignBossId.NullDispatcher, true)]
    public void NullDispatcher4E_OnlyFinalBossCanUseEnding(CampaignBossId identity, bool expected)
    {
        var ending = CreateEndingFixture(out _);
        var death = ending.gameObject.AddComponent<BossDummyController>();
        SetPrivateField(death, "resolvedDeathBossId", identity);
        SetPrivateField(death, "finalEndingPresentation", ending);
        Assert.That(InvokeHotfix(death, "ShouldPlayFinalEnding"), Is.EqualTo(expected));
        ending.enabled = false;
        Assert.That(InvokeHotfix(death, "ShouldPlayFinalEnding"), Is.EqualTo(false), "Disabled optional hook also preserves the legacy handoff.");
        SetPrivateField(death, "finalEndingPresentation", null);
        Assert.That(InvokeHotfix(death, "ShouldPlayFinalEnding"), Is.EqualTo(false), "Missing optional hook preserves legacy death handoff.");
    }

    [Test]
    public void NullDispatcher4E_InstalledGraphHasSixSubtitlesNoResponsesAndStableIdentity()
    {
        var database = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PixelCrushers.DialogueSystem.DialogueDatabase>(
            RescueContactDialogueInstaller.DialogueDatabaseAssetPath));
        createdObjects.Add(database);
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        var previousGraphs = new Dictionary<int, string>();
        foreach (var existing in database.conversations)
            if (existing.Title != NullDispatcherEndingPresentation.ConversationTitle)
                previousGraphs[existing.id] = Phase2CStoryDialogueInstaller.BuildGraphSignature(existing.dialogueEntries);
        Assert.That(NullDispatcherEndingDialogueInstaller.TryInstall(database, catalog, false, out string error), Is.True, error);
        var graph = database.GetConversation(NullDispatcherEndingPresentation.ConversationTitle);
        string signature = Phase2CStoryDialogueInstaller.BuildGraphSignature(graph.dialogueEntries);
        int actors = database.actors.Count, conversations = database.conversations.Count, id = graph.id;
        Assert.That(NullDispatcherEndingDialogueInstaller.TryInstall(database, catalog, false, out error), Is.True, error);
        graph = database.GetConversation(NullDispatcherEndingPresentation.ConversationTitle);
        Assert.That(graph.id, Is.EqualTo(id));
        Assert.That(database.actors.Count, Is.EqualTo(actors));
        Assert.That(database.conversations.Count, Is.EqualTo(conversations));
        foreach (var existing in database.conversations)
            if (previousGraphs.TryGetValue(existing.id, out string previousSignature))
                Assert.That(Phase2CStoryDialogueInstaller.BuildGraphSignature(existing.dialogueEntries), Is.EqualTo(previousSignature));
        Assert.That(graph.dialogueEntries.Count, Is.EqualTo(8));
        Assert.That(Phase2CStoryDialogueInstaller.BuildGraphSignature(graph.dialogueEntries), Is.EqualTo(signature));
        for (int node = 0; node < 8; node++)
        {
            var entry = graph.GetDialogueEntry(node);
            Assert.That(entry.ActorID, Is.Not.EqualTo(graph.ActorID), "No player response nodes.");
            Assert.That(entry.userScript ?? "", Is.EqualTo(node == 7
                ? "VS_MarkConversationComplete(\"FINAL_NullDispatcherTermination\")" : ""));
            if (node > 0 && node < 7)
            {
                Assert.That(PixelCrushers.DialogueSystem.Field.LookupValue(entry.fields, "TextKey"),
                    Is.EqualTo(NullDispatcherEndingDialogueInstaller.TextKeys[node]));
                Assert.That(entry.ActorID, Is.EqualTo(database.GetActor(node < 5
                    ? NullDispatcherDialogueIds.Actor : Phase2CStoryDialogueIds.OperatorActorName).id));
            }
        }
        Assert.That(NullDispatcherEndingDialogueInstaller.TryValidateInstalled(database, catalog, out error), Is.True, error);
        graph.GetDialogueEntry(7).userScript = "";
        Assert.That(NullDispatcherEndingDialogueInstaller.TryValidateInstalled(database, catalog, out _), Is.False);
    }

    [Test]
    public void NullDispatcher4E_AuthoredPrefabBindsExistingInnerCoreAndNaturalEntry()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NullDispatcherEndingDialogueInstaller.PrefabPath);
        Assert.That(NullDispatcherEndingDialogueInstaller.ValidatePrefab(prefab, out string error), Is.True, error);
        Assert.That(prefab.GetComponents<NullDispatcherEndingPresentation>().Length, Is.EqualTo(1));
        var database = AssetDatabase.LoadAssetAtPath<PixelCrushers.DialogueSystem.DialogueDatabase>(
            RescueContactDialogueInstaller.DialogueDatabaseAssetPath);
        var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LocalizationContentImporter.DefaultCatalogAssetPath);
        Assert.That(NullDispatcherEndingDialogueInstaller.TryValidateInstalled(database, catalog, out error), Is.True, error);
    }

    private static void CompleteRequiredFinalGate(NullDispatcherBossController boss)
    {
        var health = boss.GetComponent<EnemyHealth>();
        health.TakeDamage(10000f);
        Assert.That(health.IsDead, Is.False);
        Assert.That(health.HpRatio, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalTransition));
        InvokeHotfix(boss, "AdvanceFinalTransition", 2f);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalPhase));
    }

    private void BindFinalVisualFixture(NullDispatcherBossController boss)
    {
        var outer = CreateInactiveHotfixObject("Authored shell fixture");
        var inner = CreateInactiveHotfixObject("Authored inner fixture").AddComponent<SpriteRenderer>();
        inner.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        var parts = new SpriteRenderer[4];
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = CreateInactiveHotfixObject("Authored shell piece fixture").AddComponent<SpriteRenderer>();
            parts[i].transform.localPosition = Vector3.right * (i + 1);
        }
        SetPrivateField(boss, "outerShellRoot", outer);
        SetPrivateField(boss, "innerCore", inner);
        SetPrivateField(boss, "shellPieces", parts);
        SetPrivateField(boss, "shellPositions", null); // Explicit serialized fixture configuration before play API.
        InvokeHotfix(boss, "CacheFinalVisuals");
        SetPrivateField(boss, "deathPresentation", boss.gameObject.AddComponent<BossDeathPresentation>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4D_LargeHitStopsAtTwentyAndPreservesPolarity(bool black)
    {
        var boss = CreatePolarityFixture(false, out var hp, out var support);
        BindFinalVisualFixture(boss);
        if (black)
        {
            InvokeHotfix(boss, "CompleteMainPattern");
            InvokeHotfix(boss, "CompleteMainPattern");
            InvokeHotfix(boss, "AdvancePolarity", 0.5f);
        }
        var expectedPolarity = boss.PlayerPolarity;
        var hostile = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        var friendly = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        var health = boss.GetComponent<EnemyHealth>();
        int deaths = 0;
        health.Died += _ => deaths++;
        health.TakeDamage(health.CurrentHp - health.MaxHp * 0.23f);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.PolarityPhase));
        Assert.That(health.HpRatio, Is.EqualTo(0.23f).Within(0.0001f));
        health.TakeDamage(10000f);
        Assert.That(health.HpRatio, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(boss.FinalTransitionCount, Is.EqualTo(1));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalTransition));
        Assert.That(deaths, Is.Zero);
        Assert.That(boss.PatternDamageEnabled, Is.False);
        Assert.That(boss.PatternRunning, Is.False);
        Assert.That(boss.PolarityPacketsAllowed, Is.False);
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
        Assert.That(hostile.TryConsume(hp), Is.False);
        Assert.That(friendly.TryConsume(hp), Is.False);
        Assert.That(hostile.GetComponent<Collider2D>().enabled, Is.False);
        Assert.That(friendly.GetComponent<Collider2D>().enabled, Is.False);
        Assert.That(hp.GetComponent<PlayerController2D>().MovementLocked, Is.False);
        Assert.That(hp.GetComponent<PlayerDash>().CanDash, Is.True);
        Assert.That(hp.GetComponent<PlayerWeaponController>().ExternalInputLocked, Is.True);
        health.TakeDamage(10000f);
        InvokeHotfix(boss, "HandleHealthChanged", health, health.CurrentHp, health.MaxHp);
        InvokeHotfix(boss, "AdvancePolarity", 100f);
        Assert.That(boss.FinalTransitionCount, Is.EqualTo(1));
        Assert.That(boss.PlayerPolarity, Is.EqualTo(expectedPolarity));
        InvokeHotfix(boss, "AdvanceFinalTransition", 1.9f);
        Assert.That(boss.InnerCoreExposed, Is.False);
        InvokeHotfix(boss, "AdvanceFinalTransition", 0.2f);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalPhase));
        Assert.That(boss.InnerCoreExposed, Is.True);
        Assert.That(((GameObject)GetHotfixField(boss, "outerShellRoot")).activeSelf, Is.False);
        var inner = (SpriteRenderer)GetHotfixField(boss, "innerCore");
        Assert.That(inner.gameObject.activeSelf, Is.True);
        Assert.That(GetHotfixField(boss.GetComponent<BossDeathPresentation>(), "bodyRenderer"), Is.SameAs(inner));
        Assert.That(boss.PlayerPolarity, Is.EqualTo(expectedPolarity));
        Assert.That(boss.CurrentSupportPacketInterval, Is.EqualTo(3.5f));
        Assert.That(GetHotfixField(boss, "supportPacketHeal"), Is.EqualTo(1f));
        Assert.That(support.TriggerFullSupport(), Is.False);
        Assert.That(hp.GetComponent<PlayerWeaponController>().ExternalInputLocked, Is.False);
        InvokeHotfix(boss, "CompleteFinalTransition");
        Assert.That(boss.FinalTransitionCount, Is.EqualTo(1));
        health.TakeDamage(10000f);
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(health.IsDead, Is.True);
    }

    [Test]
    public void NullDispatcher4D_StrongFacilityStrikeCannotSkipFinalGate()
    {
        var boss = CreateReclaimFixture(20f, false, out _, out _, out _, out var support);
        InvokeHotfix(boss, "BeginSettlementIntervention");
        InvokeHotfix(boss, "RequestSettlementSupport");
        var health = boss.GetComponent<EnemyHealth>();
        InvokeHotfix(support, "ApplyWeaponLabSupport", 100);
        Assert.That(health.HpRatio, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.SettlementIntervention));
        Assert.That(boss.FinalTransitionCount, Is.Zero);
        InvokeHotfix(support, "FinishSupportSequence");
        Assert.That(boss.FinalTransitionCount, Is.EqualTo(1));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalTransition));
    }

    [Test]
    public void NullDispatcher4D_FinalProtectionRemovalPreservesForeignFloorAndWeaponOwner()
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        var health = boss.GetComponent<EnemyHealth>();
        object foreign = new object();
        var weapon = hp.GetComponent<PlayerWeaponController>();
        weapon.SetExternalInputLocked(foreign, true);
        health.SetDamageFloor(foreign, 0.1f);
        CompleteRequiredFinalGate(boss);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        health.TakeDamage(10000f);
        Assert.That(health.HpRatio, Is.EqualTo(0.1f).Within(0.0001f));
        health.RemoveDamageFloor(foreign);
        health.TakeDamage(10000f);
        Assert.That(health.IsDead, Is.True);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        weapon.SetExternalInputLocked(foreign, false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4D_CancelOrDeathClearsFinalPacketsAndTransition(bool kill)
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        CompleteRequiredFinalGate(boss);
        var hostile = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        var friendly = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        if (kill) boss.GetComponent<EnemyHealth>().TakeDamage(10000f);
        else InvokeHotfix(boss, "HandleStateChanged", GameState.BossBattle, GameState.RunResult);
        boss.CancelEncounter();
        InvokeHotfix(boss, "AdvanceFinalTransition", 10f);
        InvokeHotfix(boss, "AdvancePolarity", 10f);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
        Assert.That(hostile.TryConsume(hp), Is.False);
        Assert.That(friendly.TryConsume(hp), Is.False);
        Assert.That(boss.ActiveMajorMechanics, Is.Zero);
        Assert.That(GetHotfixField(boss, "finalTransition"), Is.Null);
        Assert.That(GetHotfixField(boss, "shellTween"), Is.Null);
        Assert.That(GetHotfixField(boss, "lasers"), Is.Empty);
        Assert.That(GetHotfixField(boss, "shots"), Is.Empty);
        Assert.That(hp.GetComponent<PlayerWeaponController>().ExternalInputLocked, Is.False);
    }

    [Test]
    public void NullDispatcher4D_PauseAndCancelDoNotFinishShellBreak()
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        boss.GetComponent<EnemyHealth>().TakeDamage(10000f);
        var singleton = typeof(GameplayPauseManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        var pause = CreateInactiveHotfixObject("Final pause").AddComponent<GameplayPauseManager>();
        object owner = new object();
        try
        {
            singleton.SetValue(null, pause);
            pause.PushPause(owner, "Final transition");
            InvokeHotfix(boss, "AdvanceFinalTransition", 10f);
            Assert.That(boss.InnerCoreExposed, Is.False);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalTransition));
            boss.CancelEncounter();
            pause.PopPause(owner);
            InvokeHotfix(boss, "AdvanceFinalTransition", 10f);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
            Assert.That(hp.GetComponent<PlayerWeaponController>().ExternalInputLocked, Is.False);
        }
        finally { pause.PopPause(owner); singleton.SetValue(null, previous); }
    }

    [Test]
    public void NullDispatcher4D_FinalSwitchClearsBeforeTogglingAndSupplyDefersForLasers()
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        CompleteRequiredFinalGate(boss);
        CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        SetPrivateField(boss, "lanesDamaging", true);
        InvokeHotfix(boss, "AdvancePolarity", 3.5f);
        Assert.That(GetHotfixField(boss, "supportElapsed"), Is.EqualTo(3.5f), "Supply waits rather than spawning into live lanes.");
        SetPrivateField(boss, "lanesDamaging", false);
        InvokeHotfix(boss, "AdvancePolarity", 0.1f);
        Assert.That(GetHotfixField(boss, "supportElapsed"), Is.EqualTo(0f));
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(boss.PolaritySwitching, Is.False);
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(boss.PolaritySwitching, Is.True);
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        InvokeHotfix(boss, "AdvancePolarity", 0.5f);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.Black));
        Assert.That(boss.PolarityPacketsAllowed, Is.True);
    }

    [Test]
    public void NullDispatcher4D_ShellTweenCleanupRestoresOnlyOwnedVisuals()
    {
        using var scope = new ManualRecoveryTweenScope();
        var boss = CreatePolarityFixture(false, out _, out _);
        BindFinalVisualFixture(boss);
        var parts = (SpriteRenderer[])GetHotfixField(boss, "shellPieces");
        Vector3 original = parts[0].transform.localPosition;
        Tween tween = parts[0].transform.DOLocalMove(original + Vector3.up, 1f);
        scope.Own(tween);
        SetPrivateField(boss, "shellTween", tween);
        tween.Goto(0.5f, false);
        Assert.That(parts[0].transform.localPosition, Is.Not.EqualTo(original));
        boss.CancelEncounter();
        boss.CancelEncounter();
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(parts[0].transform.localPosition, Is.EqualTo(original));
        Assert.That(GetHotfixField(boss, "shellTween"), Is.Null);
    }

    [Test]
    public void NullDispatcher4D_AuthoredShellAndBoundedCombinationBudgets()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab");
        var boss = prefab.GetComponent<NullDispatcherBossController>();
        var outer = (GameObject)GetHotfixField(boss, "outerShellRoot");
        var inner = (SpriteRenderer)GetHotfixField(boss, "innerCore");
        Assert.That(outer.activeSelf, Is.True);
        Assert.That(inner.gameObject.activeSelf, Is.False);
        Assert.That(inner.sprite.name, Is.EqualTo("core5"));
        Assert.That(inner.transform.IsChildOf(outer.transform), Is.False);
        var parts = (SpriteRenderer[])GetHotfixField(boss, "shellPieces");
        Assert.That(parts.Length, Is.EqualTo(4));
        foreach (var part in parts)
        {
            Assert.That(part.transform.IsChildOf(outer.transform), Is.True);
            Assert.That(part.sprite.name, Is.EqualTo("Square"));
            Assert.That(part.GetComponent<Rigidbody2D>(), Is.Null);
        }
        Assert.That(GetHotfixField(boss, "deathPresentation"), Is.SameAs(prefab.GetComponent<BossDeathPresentation>()));
        Assert.That(NullDispatcherBossController.PartitionPacketCount, Is.EqualTo(4));
        Assert.That(NullDispatcherBossController.RedirectBurstCount * NullDispatcherBossController.RedirectPacketsPerBurst + NullDispatcherBossController.CompressionFanCount, Is.EqualTo(7));
        int last = -1;
        for (int i = 0; i < 8; i++)
        {
            int next = (int)NullDispatcherBossController.NextFinalCombination(last);
            Assert.That(next, Is.Not.EqualTo(last));
            Assert.That(next, Is.InRange(0, 1));
            last = next;
        }
    }

    private NullDispatcherBossController CreatePolarityFixture(bool accept, out PlayerHealth hp,
        out SettlementFinalSupportController support)
    {
        var boss = CreateReclaimFixture(20f, accept, out _, out hp, out _, out support);
        InvokeHotfix(boss, "BeginSettlementIntervention");
        InvokeHotfix(boss, "RequestSettlementSupport");
        InvokeHotfix(support, "FinishSupportSequence");
        return boss;
    }

    [Test]
    public void NullDispatcher4D_FinalSchedulerOwnsOnlyTwoTelegraphedMechanics()
    {
        var oldRun = RunManager.Instance;
        try
        {
            var run = CreateInactiveHotfixObject("Final scheduler run").AddComponent<RunManager>();
            SetPrivateField(run, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.FinalNetwork));
            SetHotfixSingleton(typeof(RunManager), run);
            var boss = CreatePolarityFixture(false, out _, out _);
            CompleteRequiredFinalGate(boss);
            var relays = new[]
            {
                CreateInactiveHotfixObject("Relay entry").AddComponent<SpriteRenderer>(),
                CreateInactiveHotfixObject("Relay exit").AddComponent<SpriteRenderer>()
            };
            SetPrivateField(boss, "relays", relays);
            var combo = (System.Collections.IEnumerator)InvokeHotfix(boss, "FinalCombinationRoutine",
                NullDispatcherBossController.FinalCombination.RedirectCompression);
            Assert.That(combo.MoveNext(), Is.True);
            var warning = (System.Collections.IEnumerator)combo.Current;
            Assert.That(warning.MoveNext(), Is.True);
            Assert.That(boss.PatternDamageEnabled, Is.False);
            Assert.That(boss.ActiveMajorMechanics, Is.Zero);
            Assert.That(warning.MoveNext(), Is.False);
            Assert.That(combo.MoveNext(), Is.True);
            Assert.That(boss.PatternDamageEnabled, Is.True);
            Assert.That(boss.ActiveMajorMechanics, Is.EqualTo(2));
            // Walk only the scheduling protocol; spawned projectile travel is a Play Mode check.
            var loop = (System.Collections.IEnumerator)InvokeHotfix(boss, "PatternLoop");
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(GetHotfixField(boss, "lastCombination"), Is.EqualTo(0));
            Assert.That(GetHotfixField(boss, "lastPattern"), Is.EqualTo(-1));
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(boss.ActiveMajorMechanics, Is.Zero);
            Assert.That(relays[0].enabled, Is.False);
            Assert.That(relays[1].enabled, Is.False);
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(GetHotfixField(boss, "lastCombination"), Is.EqualTo(1));
            Assert.That(loop.MoveNext(), Is.True);
            Assert.That(boss.PolaritySwitching, Is.True);
            Assert.That(boss.PatternDamageEnabled, Is.False);
            Assert.That(GetHotfixField(boss, "mainPatternActive"), Is.EqualTo(false));
            boss.CancelEncounter();
        }
        finally { SetHotfixSingleton(typeof(RunManager), oldRun); }
    }

    [Test]
    public void NullDispatcher4D_FinalGateCancelsPendingPolarityToggle()
    {
        var boss = CreatePolarityFixture(false, out _, out _);
        InvokeHotfix(boss, "CompleteMainPattern");
        InvokeHotfix(boss, "CompleteMainPattern");
        InvokeHotfix(boss, "AdvancePolarity", 0.25f);
        Assert.That(boss.PolaritySwitching, Is.True);
        boss.GetComponent<EnemyHealth>().TakeDamage(10000f);
        Assert.That(boss.PolaritySwitching, Is.False);
        InvokeHotfix(boss, "AdvancePolarity", 10f);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        InvokeHotfix(boss, "AdvanceFinalTransition", 2f);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.FinalPhase));
    }

    private NullDispatcherPolarityPacket CreatePolarityPacket(NullDispatcherBossController boss,
        PlayerHealth hp, NullDispatcherPolarityPacket.PacketKind kind)
    {
        // Component logic fixture: explicit configuration; no prefab/Awake assumptions.
        var packet = CreateInactiveHotfixObject("Polarity packet logic").AddComponent<NullDispatcherPolarityPacket>();
        SetPrivateField(packet, "kind", kind);
        Assert.That(packet.Initialize(boss, hp, NullDispatcherBossController.PacketPolarity(boss.PlayerPolarity, kind),
            Vector2.right * 3f, 1f, 8f), Is.True);
        return packet;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4C_SupportEntersWhiteOnceAndReleasesDamageFloor(bool accept)
    {
        var boss = CreatePolarityFixture(accept, out var hp, out var support);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.PolarityPhase));
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        Assert.That(support.SequenceCompleted, Is.True);
        Assert.That(support.TriggerFullSupport(), Is.False);
        Assert.That(boss.PatternRunning, Is.False, "Inactive EditMode fixture does not pretend a frame loop ran.");
        Assert.That(hp.GetComponent<PlayerWeaponController>().ExternalInputLocked, Is.False);
        Assert.That(hp.GetComponent<PlayerController2D>().MovementLocked, Is.False);
        Assert.That(hp.GetComponent<PlayerDash>().CanDash, Is.True);
        InvokeHotfix(boss, "CompleteMainPattern");
        InvokeHotfix(boss, "CompleteMainPattern");
        InvokeHotfix(boss, "AdvancePolarity", 0.5f);
        InvokeHotfix(boss, "HandleSupportCompleted");
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.Black), "Duplicate completion cannot restart Phase 2.");
        CompleteRequiredFinalGate(boss);
        boss.GetComponent<EnemyHealth>().TakeDamage(10000f);
        Assert.That(boss.GetComponent<EnemyHealth>().IsDead, Is.True);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
    }

    [TestCase(NullDispatcherBossController.Polarity.White, NullDispatcherPolarityPacket.PacketKind.Hostile, NullDispatcherBossController.Polarity.Black)]
    [TestCase(NullDispatcherBossController.Polarity.White, NullDispatcherPolarityPacket.PacketKind.Support, NullDispatcherBossController.Polarity.White)]
    [TestCase(NullDispatcherBossController.Polarity.Black, NullDispatcherPolarityPacket.PacketKind.Hostile, NullDispatcherBossController.Polarity.White)]
    [TestCase(NullDispatcherBossController.Polarity.Black, NullDispatcherPolarityPacket.PacketKind.Support, NullDispatcherBossController.Polarity.Black)]
    public void NullDispatcher4C_AllegianceRule(NullDispatcherBossController.Polarity player,
        NullDispatcherPolarityPacket.PacketKind kind, NullDispatcherBossController.Polarity expected)
    {
        Assert.That(NullDispatcherBossController.PacketPolarity(player, kind), Is.EqualTo(expected));
    }

    [Test]
    public void NullDispatcher4C_OneSchedulerWaitsForSafeSwitchThenResumes()
    {
        var oldRun = RunManager.Instance;
        try
        {
            var run = CreateInactiveHotfixObject("Polarity scheduler run").AddComponent<RunManager>();
            SetPrivateField(run, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.FinalNetwork));
            SetHotfixSingleton(typeof(RunManager), run);
            var boss = CreatePolarityFixture(false, out _, out _);
            // Walk the scheduler protocol; nested attack execution/physics are Play Mode checks.
            var loop = (System.Collections.IEnumerator)InvokeHotfix(boss, "PatternLoop");
            Assert.That(loop.MoveNext(), Is.True); // recovery
            Assert.That(loop.MoveNext(), Is.True); // first nested pattern
            Assert.That(GetHotfixField(boss, "mainPatternActive"), Is.EqualTo(true));
            Assert.That(GetHotfixField(boss, "lastPattern"), Is.EqualTo(0));
            Assert.That(loop.MoveNext(), Is.True); // complete -> recovery
            Assert.That(GetHotfixField(boss, "mainPatternActive"), Is.EqualTo(false));
            Assert.That(loop.MoveNext(), Is.True); // second nested pattern
            Assert.That(GetHotfixField(boss, "lastPattern"), Is.EqualTo(1));
            Assert.That(loop.MoveNext(), Is.True); // switch warning
            Assert.That(boss.PolaritySwitching, Is.True);
            Assert.That(loop.MoveNext(), Is.True); // remains waiting, cannot start the third pattern
            Assert.That(GetHotfixField(boss, "lastPattern"), Is.EqualTo(1));
            InvokeHotfix(boss, "AdvancePolarity", 0.5f);
            Assert.That(loop.MoveNext(), Is.True); // recovery
            Assert.That(loop.MoveNext(), Is.True); // third nested pattern
            Assert.That(GetHotfixField(boss, "lastPattern"), Is.EqualTo(2));
            var warning = (System.Collections.IEnumerator)InvokeHotfix(boss, "WaitForTelegraph");
            Assert.That(warning.MoveNext(), Is.True);
            Assert.That(boss.PatternDamageEnabled, Is.False);
            Assert.That(warning.MoveNext(), Is.False);
            Assert.That(boss.PatternDamageEnabled, Is.True);
            boss.CancelEncounter();
            Assert.That(boss.PatternDamageEnabled, Is.False);
            Assert.That(GetHotfixField(boss, "mainPatternActive"), Is.EqualTo(false));
        }
        finally { SetHotfixSingleton(typeof(RunManager), oldRun); }
    }

    [Test]
    public void NullDispatcher4C_SwitchClearsBothKindsBeforeWarningAndToggle()
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        var hostile = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        var friendly = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        SetPrivateField(boss, "mainPatternActive", true);
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(InvokeHotfix(boss, "TryBeginPolaritySwitch"), Is.False);
        SetPrivateField(boss, "mainPatternActive", false);
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(boss.PolaritySwitching, Is.False);
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(boss.PolaritySwitching, Is.True);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
        Assert.That(boss.PolarityPacketsAllowed, Is.False);
        foreach (var packet in new[] { hostile, friendly })
        {
            Assert.That(packet.Consumed, Is.True);
            Assert.That(packet.GetComponent<Collider2D>().enabled, Is.False);
            Assert.That(packet.TryConsume(hp), Is.False);
            Assert.That(boss.RegisterPolarityPacket(packet), Is.False);
        }
        Assert.That(InvokeHotfix(boss, "TryBeginPolaritySwitch"), Is.False);
        InvokeHotfix(boss, "AdvancePolarity", 0.49f);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        InvokeHotfix(boss, "AdvancePolarity", 0.02f);
        Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.Black));
        Assert.That(boss.PolaritySwitching, Is.False);
        Assert.That(boss.PolarityPacketsAllowed, Is.True);
        Assert.That(hostile.Polarity, Is.EqualTo(NullDispatcherBossController.Polarity.Black), "Old flight allegiance never mutates.");
        Assert.That(friendly.Polarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
        Assert.That(friendly.Initialize(boss, hp, NullDispatcherBossController.Polarity.White, Vector2.zero, 1f, 1f), Is.False);
        Assert.That(friendly.Initialize(boss, hp, NullDispatcherBossController.Polarity.Black, Vector2.zero, 1f, 1f), Is.True, "Pooled reuse resets the consumed state.");
        Assert.That(friendly.Consumed, Is.False);
    }

    [TestCase(19.5f, 20f)]
    [TestCase(12f, 13f)]
    [TestCase(20f, 20f)]
    public void NullDispatcher4C_SupportHealsOnceAndClamps(float before, float expected)
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        hp.RestoreCurrentHp(before);
        int heals = 0;
        hp.Healed += (_, __) => heals++;
        var packet = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        Assert.That(packet.TryConsume(hp), Is.True);
        Assert.That(packet.TryConsume(hp), Is.False);
        Assert.That(hp.CurrentHp, Is.EqualTo(expected));
        Assert.That(heals, Is.EqualTo(1));
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4C_HostileUsesOrdinaryArmorAndInvulnerability(bool invincible)
    {
        var boss = CreatePolarityFixture(false, out _, out _);
        var actor = CreateInactiveHotfixObject("Ordinary packet target");
        var hp = actor.AddComponent<PlayerHealth>();
        hp.SetMaxHp(20f, true);
        var armor = actor.AddComponent<PlayerArmor>();
        armor.SetMaxArmor(2f, true);
        SetPrivateField(hp, "armor", armor);
        SetPrivateField(hp, "dashInvincible", invincible);
        SetPrivateField(hp, "autoCreateSpriteHitFlash", false);
        SetPrivateField(hp, "hitShakeAmplitude", 0f);
        var packet = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        Assert.That(packet.TryConsume(hp), Is.True);
        Assert.That(packet.TryConsume(hp), Is.False);
        Assert.That(armor.CurrentArmor, Is.EqualTo(invincible ? 2f : 1f));
        Assert.That(hp.CurrentHp, Is.EqualTo(20f), "No direct reclaim HP drain path.");
    }

    [TestCase(NullDispatcherPolarityPacket.PacketKind.Hostile, 24)]
    [TestCase(NullDispatcherPolarityPacket.PacketKind.Support, 2)]
    public void NullDispatcher4C_PacketBudgetAndCleanupAreBounded(NullDispatcherPolarityPacket.PacketKind kind, int cap)
    {
        var boss = CreatePolarityFixture(false, out var hp, out var support);
        for (int i = 0; i < cap; i++) CreatePolarityPacket(boss, hp, kind);
        var overflow = CreateInactiveHotfixObject("Packet overflow").AddComponent<NullDispatcherPolarityPacket>();
        SetPrivateField(overflow, "kind", kind);
        Assert.That(overflow.Initialize(boss, hp, NullDispatcherBossController.PacketPolarity(boss.PlayerPolarity, kind), Vector2.zero, 1f, 1f), Is.False);
        Assert.That(boss.ActivePolarityPacketCount, Is.EqualTo(cap));
        Assert.That(GetHotfixField(boss, "supportPacketInterval"), Is.EqualTo(5f));
        InvokeHotfix(boss, "AdvancePolarity", 4.9f);
        Assert.That(GetHotfixField(boss, "supportElapsed"), Is.EqualTo(4.9f));
        InvokeHotfix(boss, "AdvancePolarity", 0.1f);
        Assert.That(GetHotfixField(boss, "supportElapsed"), Is.EqualTo(0f));
        Assert.That(support.TriggerFullSupport(), Is.False);
        boss.CancelEncounter();
        boss.CancelEncounter();
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4C_PauseFreezesPacketsAndSwitch(bool switching)
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        var packet = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        var singleton = typeof(GameplayPauseManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        var pause = CreateInactiveHotfixObject("Polarity pause").AddComponent<GameplayPauseManager>();
        object owner = new object();
        try
        {
            singleton.SetValue(null, pause);
            if (switching) { InvokeHotfix(boss, "CompleteMainPattern"); InvokeHotfix(boss, "CompleteMainPattern"); }
            pause.PushPause(owner, "Polarity test");
            InvokeHotfix(boss, "AdvancePolarity", 20f);
            packet.AdvanceFlight(20f);
            Assert.That(boss.PlayerPolarity, Is.EqualTo(NullDispatcherBossController.Polarity.White));
            Assert.That(GetHotfixField(boss, "supportElapsed"), Is.EqualTo(0f));
            Assert.That(GetHotfixField(boss, "switchElapsed"), Is.EqualTo(0f));
            Assert.That(packet.TryConsume(hp), Is.False);
            if (!switching) Assert.That(GetHotfixField(packet, "remaining"), Is.EqualTo(8f));
        }
        finally { pause.PopPause(owner); singleton.SetValue(null, previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher4C_DeathOrExitClearsPacketsHudAndPendingSwitch(bool death)
    {
        var boss = CreatePolarityFixture(false, out var hp, out _);
        var ui = CreateInactiveHotfixObject("Polarity HUD fixture").AddComponent<ExpeditionHUD>();
        var label = Track(new GameObject("Authored label fixture", typeof(RectTransform), typeof(CanvasRenderer))).AddComponent<TextMeshProUGUI>();
        SetPrivateField(ui, "polarityText", label);
        SetPrivateField(boss, "hud", ui);
        InvokeHotfix(boss, "UpdatePolarityHud");
        Assert.That(label.gameObject.activeSelf, Is.True);
        string whiteLabel = label.text;
        InvokeHotfix(boss, "CompleteMainPattern");
        InvokeHotfix(boss, "CompleteMainPattern");
        Assert.That(label.text, Is.EqualTo(whiteLabel), "The indicator keeps the current state throughout the warning.");
        InvokeHotfix(boss, "AdvancePolarity", 0.5f);
        Assert.That(label.text, Is.Not.EqualTo(whiteLabel));
        ui.HidePolarity(new object());
        Assert.That(label.gameObject.activeSelf, Is.True, "Foreign cleanup cannot hide the indicator.");
        var hostile = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Hostile);
        var friendly = CreatePolarityPacket(boss, hp, NullDispatcherPolarityPacket.PacketKind.Support);
        if (death) { CompleteRequiredFinalGate(boss); boss.GetComponent<EnemyHealth>().TakeDamage(10000f); }
        else InvokeHotfix(boss, "HandleStateChanged", GameState.BossBattle, GameState.RunResult);
        boss.CancelEncounter();
        InvokeHotfix(boss, "AdvancePolarity", 100f);
        Assert.That(boss.ActivePolarityPacketCount, Is.Zero);
        Assert.That(hostile.TryConsume(hp), Is.False);
        Assert.That(friendly.TryConsume(hp), Is.False);
        Assert.That(label.gameObject.activeSelf, Is.False);
        Assert.That(boss.PolaritySwitching, Is.False);
        Assert.That(GetHotfixField(boss, "polarityRuntime"), Is.Null);
    }

    [TestCase("Hostile", NullDispatcherPolarityPacket.PacketKind.Hostile, 2)]
    [TestCase("Support", NullDispatcherPolarityPacket.PacketKind.Support, 4)]
    public void NullDispatcher4C_AuthoredPacketShapesAndBossBindings(string name, NullDispatcherPolarityPacket.PacketKind kind, int rendererCount)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_NullDispatcher_" + name + "Packet.prefab");
        var packet = prefab.GetComponent<NullDispatcherPolarityPacket>();
        Assert.That(packet.Kind, Is.EqualTo(kind));
        Assert.That(prefab.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        Assert.That(prefab.GetComponent<Collider2D>().isTrigger, Is.True);
        var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(renderers.Length, Is.EqualTo(rendererCount));
        foreach (var renderer in renderers)
        {
            Assert.That(renderer.sprite.name, Is.EqualTo("Square"));
            if (kind == NullDispatcherPolarityPacket.PacketKind.Hostile)
                Assert.That(renderer.transform.localEulerAngles.z, Is.EqualTo(45f).Within(0.01f));
            else Assert.That(renderer.transform.localScale.x, Is.Not.EqualTo(renderer.transform.localScale.y));
        }
        Assert.That(GetHotfixField(packet, "whiteFill"), Is.Not.EqualTo(GetHotfixField(packet, "whiteOutline")));
        Assert.That(GetHotfixField(packet, "blackFill"), Is.Not.EqualTo(GetHotfixField(packet, "blackOutline")));
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab").GetComponent<NullDispatcherBossController>();
        Assert.That(GetHotfixField(boss, kind == NullDispatcherPolarityPacket.PacketKind.Hostile ? "hostilePacketPrefab" : "supportPacketPrefab"), Is.SameAs(packet));
    }

    [Test]
    public void NullDispatcher4C_ExpeditionHasBoundInactivePersistentIndicator()
    {
        var scene = AuthoredRuntimeFixture.Open("Expedition");
        try
        {
            var hud = AuthoredRuntimeFixture.Single<ExpeditionHUD>(scene);
            var label = (TextMeshProUGUI)GetHotfixField(hud, "polarityText");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.gameObject.activeSelf, Is.False);
            Assert.That(label.transform.IsChildOf(hud.transform), Is.True);
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.fontSize, Is.EqualTo(8f));
        }
        finally { AuthoredRuntimeFixture.Close(scene); }
    }

    // Installed DOTween AutoInit deliberately does nothing outside Play Mode,
    // but Sequence/Goto still work. Kill is gated by its private initialized flag.
    // Model ONLY that manual-operation precondition, with no engine host, frame
    // loop, private MonoBehaviour callbacks or active PreviewScene. Restore the
    // flag and kill only these fixture-owned tweens, never clear other previews.
    private sealed class ManualRecoveryTweenScope : System.IDisposable
    {
        private readonly FieldInfo initialized = typeof(DOTween).GetField(
            "initialized", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly bool wasInitialized;
        private readonly List<Tween> ownedTweens = new List<Tween>();

        public ManualRecoveryTweenScope()
        {
            Assert.That(initialized, Is.Not.Null, "Recheck installed DOTween's EditMode initialization contract.");
            wasInitialized = (bool)initialized.GetValue(null);
            initialized.SetValue(null, true);
        }

        public void Own(Tween tween)
        {
            ownedTweens.Add(tween);
        }

        public void Dispose()
        {
            try
            {
                foreach (Tween tween in ownedTweens)
                {
                    tween?.Kill();
                }
            }
            finally
            {
                initialized.SetValue(null, wasInitialized);
            }
        }
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        Physics2D.SyncTransforms();
    }

    [Test]
    public void BossExitPlacement_IsDeterministicSeparatedAndInsideBounds()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 14f, 1f));
        Vector2[] avoidPoints = { Vector2.zero, new Vector2(3f, 2f) };
        GameObject blockerObject = Track(new GameObject("Owned exit-placement blocker"));
        blockerObject.transform.position = new Vector2(-2.475f, -1.25f);
        BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(0.6f, 0.6f);
        Physics2D.SyncTransforms();
        ExitBlockerProbe blockerProbe = new ExitBlockerProbe(blocker);

        BossExitPlacement first = BossExitSafePlacement.Resolve(
            bounds,
            Vector2.zero,
            avoidPoints,
            avoidPoints.Length,
            blockerProbe.IsBlocked,
            out bool firstFallback
        );
        BossExitPlacement second = BossExitSafePlacement.Resolve(
            bounds,
            Vector2.zero,
            avoidPoints,
            avoidPoints.Length,
            blockerProbe.IsBlocked,
            out bool secondFallback
        );

        string diagnostics = BuildExitPlacementDiagnostics(
            bounds,
            avoidPoints,
            first,
            second,
            firstFallback,
            secondFallback,
            blockerProbe
        );

        Assert.That(firstFallback, Is.False, diagnostics);
        Assert.That(secondFallback, Is.False, diagnostics);
        Assert.That(second.ReturnBeaconPosition, Is.EqualTo(first.ReturnBeaconPosition), diagnostics);
        Assert.That(second.NextRegionPosition, Is.EqualTo(first.NextRegionPosition), diagnostics);
        Assert.That(IsInside2D(bounds, first.ReturnBeaconPosition), Is.True, diagnostics);
        Assert.That(IsInside2D(bounds, first.NextRegionPosition), Is.True, diagnostics);
        Assert.That(
            Vector2.Distance(first.ReturnBeaconPosition, first.NextRegionPosition),
            Is.GreaterThanOrEqualTo(BossExitSafePlacement.MinimumExitSeparation - 0.001f),
            diagnostics
        );

        for (int i = 0; i < avoidPoints.Length; i++)
        {
            Assert.That(
                Vector2.Distance(first.ReturnBeaconPosition, avoidPoints[i]),
                Is.GreaterThanOrEqualTo(BossExitSafePlacement.AvoidPointClearance),
                diagnostics
            );
            Assert.That(
                Vector2.Distance(first.NextRegionPosition, avoidPoints[i]),
                Is.GreaterThanOrEqualTo(BossExitSafePlacement.AvoidPointClearance),
                diagnostics
            );
        }

        Assert.That(blockerProbe.IsBlocked(first.ReturnBeaconPosition), Is.False, diagnostics);
        Assert.That(blockerProbe.IsBlocked(first.NextRegionPosition), Is.False, diagnostics);
    }

    [TestCase(ExpeditionDepth.Normal, CampaignBossId.SectorAdministrator)]
    [TestCase(ExpeditionDepth.DeepZone1, CampaignBossId.SalvageDevourer)]
    public void BossExitCoordinator_RequiresAuthorizationAndRepeatClear(
        ExpeditionDepth depth, CampaignBossId boss)
    {
        GameObject root = new GameObject("ExitProgressFixture");
        root.SetActive(false);
        createdObjects.Add(root);
        PermanentProgress progress = root.AddComponent<PermanentProgress>();
        progress.TryStartDamagedAccessKeyQuest();
        progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
        if (depth == ExpeditionDepth.DeepZone1)
        {
            progress.TryAuthorizeAnalyzedRegion();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
        }
        var firstRun = new RunContext(WeaponTreeType.MachineGun, depth);
        firstRun.MarkBossDefeated(boss, true);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(firstRun, progress, false), Is.False);
        Assert.That(progress.TryAuthorizeAnalyzedRegion(), Is.True);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(firstRun, progress, false), Is.False);
        var repeatRun = new RunContext(WeaponTreeType.MachineGun, depth);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, false), Is.False);
        repeatRun.MarkBossDefeated(boss);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, false), Is.True);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, true), Is.False);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, null, false), Is.False);
        repeatRun.MarkBossDefeated(CampaignBossId.NullDispatcher);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, false), Is.False);
        repeatRun.MarkBossDefeated(boss);
        repeatRun.End();
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, false), Is.False);
        repeatRun.SetDepth(ExpeditionDepth.DeepZone2);
        repeatRun.MarkBossDefeated(CampaignBossId.PhaseGatekeeper);
        Assert.That(BossRewardExitCoordinator.IsPortalEligible(repeatRun, progress, false), Is.False);
    }

    [Test]
    public void BossExitReveal_HoldsInteractionAndRestoresAuthoredState_Idempotently()
    {
        GameObject root = new GameObject("ExitRevealFixture");
        root.SetActive(false);
        createdObjects.Add(root);
        root.transform.localScale = new Vector3(2f, 3f, 1f);
        root.transform.position = new Vector3(8f, 4f, 0f);
        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        Color color = new Color(0.3f, 0.6f, 0.8f, 0.7f);
        sprite.color = color;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        ReturnBeacon beacon = root.AddComponent<ReturnBeacon>();
        WormholePortal portal = root.AddComponent<WormholePortal>();
        System.Type revealType = typeof(BossRewardExitCoordinator).GetNestedType(
            "ExitRevealTarget", BindingFlags.NonPublic);
        object reveal = System.Activator.CreateInstance(revealType, new object[] { root });
        Assert.That(beacon.PresentationReady, Is.False);
        Assert.That(portal.PresentationReady, Is.False);
        Assert.That(collider.enabled, Is.False);
        Assert.That(sprite.color.a, Is.Zero);
        Assert.That(beacon.CanInteract(root), Is.False);
        Assert.That(portal.CanInteract(root), Is.False);

        GameObject owner = new GameObject("ExitRevealOwner");
        owner.SetActive(false);
        createdObjects.Add(owner);
        BossRewardExitCoordinator coordinator = owner.AddComponent<BossRewardExitCoordinator>();
        typeof(BossRewardExitCoordinator).GetField("beaconReveal", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(coordinator, reveal);
        coordinator.CompleteExitReveal();
        coordinator.CompleteExitReveal();
        Assert.That(beacon.PresentationReady, Is.True);
        Assert.That(portal.PresentationReady, Is.True);
        Assert.That(collider.enabled, Is.True);
        Assert.That(sprite.color, Is.EqualTo(color));
        Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 1f)));
        Assert.That(root.transform.position, Is.EqualTo(new Vector3(8f, 4f, 0f)));
    }

    [Test]
    public void BossRecovery_CleanupAndImmediateCompletionAreIdempotent()
    {
        GameObject root = new GameObject("RecoveryCleanupFixture");
        root.SetActive(false);
        createdObjects.Add(root);
        Region2BossCoreRewardPresentation presentation = root.AddComponent<Region2BossCoreRewardPresentation>();
        presentation.CompleteImmediately();
        presentation.CleanupPresentation();
        presentation.CleanupPresentation();
        Assert.That(presentation.IsPlaying, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BossDeath_ReleasesOnlyOwnedLocks_AndCancelsDashWithoutStaleRestore(bool wasDashing)
    {
        GameObject player = CreateInactiveHotfixObject("Death control player");
        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        PlayerDash dash = player.AddComponent<PlayerDash>();
        PlayerWeaponController weapon = player.AddComponent<PlayerWeaponController>();
        SetPrivateField(dash, "controller", controller);
        SetPrivateField(dash, "rb", player.GetComponent<Rigidbody2D>());
        SetPrivateField(dash, "lastDashTime", -999f);
        SetPrivateField(dash, "isDashing", wasDashing);
        controller.SetMovementLocked(wasDashing);
        if (wasDashing)
        {
            controller.SetMovementVelocityOverride(Vector2.right * 20f);
        }
        object menuOwner = new object();
        controller.SetExternalControlLocked(menuOwner, true);
        weapon.SetExternalInputLocked(menuOwner, true);
        BossDeathPresentation death = CreateInactiveHotfixObject("Death lock owner")
            .AddComponent<BossDeathPresentation>();
        SetPrivateField(death, "presentationLocksHeld", true);
        int dashEndedCount = 0;
        dash.DashEnded += () => dashEndedCount++;
        InvokeHotfix(death, "LockPlayerInput", controller);
        Assert.That(controller.ControlEnabled, Is.False);
        Assert.That(dash.IsDashing, Is.False);
        Assert.That(dash.enabled, Is.True);

        death.CancelPresentation();
        death.CancelPresentation();
        dash.CancelActiveDash();
        Assert.That(controller.ControlEnabled, Is.False, "The menu still owns a lock.");
        Assert.That(weapon.ExternalInputLocked, Is.True);
        controller.SetExternalControlLocked(menuOwner, false);
        weapon.SetExternalInputLocked(menuOwner, false);
        Assert.That(controller.ControlEnabled, Is.True);
        Assert.That(controller.MovementLocked, Is.False);
        Assert.That(controller.MovementVelocityOverrideActive, Is.False);
        Assert.That(weapon.ExternalInputLocked, Is.False);
        Assert.That(dash.CanDash, Is.True);
        Assert.That(dashEndedCount, Is.EqualTo(wasDashing ? 1 : 0));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BossCinematicBoundary_DoesNotRestoreDashMovementLock(bool phaseTransition)
    {
        GameObject player = CreateInactiveHotfixObject("Boundary player");
        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        PlayerDash dash = player.AddComponent<PlayerDash>();
        SetPrivateField(dash, "controller", controller);
        SetPrivateField(dash, "isDashing", true);
        controller.SetMovementLocked(true);
        controller.SetMovementVelocityOverride(Vector2.right);
        GameObject owner = CreateInactiveHotfixObject("Boundary owner");
        Component cinematic = phaseTransition
            ? (Component)owner.AddComponent<BossPatternController>()
            : owner.AddComponent<CoreBossIntroSequence>();
        string acquire = phaseTransition ? "LockPhase2PlayerInput" : "LockPlayer";
        string release = phaseTransition ? "RestorePhase2PlayerInput" : "RestorePlayer";
        InvokeHotfix(cinematic, acquire, player);
        Assert.That(controller.ControlEnabled, Is.False);
        Assert.That(dash.enabled, Is.True);
        InvokeHotfix(cinematic, release);
        InvokeHotfix(cinematic, release);
        Assert.That(controller.ControlEnabled, Is.True);
        Assert.That(controller.MovementLocked, Is.False);
        Assert.That(dash.IsDashing, Is.False);
        Assert.That(controller.MovementVelocityOverrideActive, Is.False);
    }

    [Test]
    public void BossDeath_CameraCleanupPreservesOtherOffsetOwners()
    {
        GungeonStyleCamera2D camera = CreateInactiveHotfixObject("Owned camera")
            .AddComponent<GungeonStyleCamera2D>();
        BossDeathPresentation death = CreateInactiveHotfixObject("Camera death owner")
            .AddComponent<BossDeathPresentation>();
        object other = new object();
        camera.SetCinematicInputOffsetLocked(other, true);
        camera.SetCinematicInputOffsetLocked(death, true);
        SetPrivateField(death, "gameplayCamera", camera);
        SetPrivateField(death, "presentationLocksHeld", true);
        death.CancelPresentation();
        death.CancelPresentation();
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.True);
        camera.SetCinematicInputOffsetLocked(other, false);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.False);
    }

    [Test]
    public void BossDeath_DoesNotEnableAnExternallyDisabledDashOrLegacyControls()
    {
        GameObject player = CreateInactiveHotfixObject("Externally locked player");
        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        PlayerDash dash = player.AddComponent<PlayerDash>();
        dash.enabled = false;
        controller.SetControlEnabled(false);
        controller.SetMovementLocked(true);
        BossDeathPresentation death = CreateInactiveHotfixObject("Foreign lock probe")
            .AddComponent<BossDeathPresentation>();
        SetPrivateField(death, "presentationLocksHeld", true);
        InvokeHotfix(death, "LockPlayerInput", controller);
        death.CancelPresentation();
        Assert.That(controller.ControlEnabled, Is.False);
        Assert.That(controller.MovementLocked, Is.True);
        Assert.That(dash.enabled, Is.False);
    }

    [Test]
    public void Dash_CancellationInsideStartEventCannotReapplyMovementOverride()
    {
        GameObject player = CreateInactiveHotfixObject("Reentrant dash cancellation");
        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        PlayerDash dash = player.AddComponent<PlayerDash>();
        SetPrivateField(dash, "controller", controller);
        dash.DashStarted += _ => dash.CancelActiveDash();
        int endCount = 0;
        dash.DashEnded += () => endCount++;
        var routine = (System.Collections.IEnumerator)InvokeHotfix(dash, "DashRoutine", Vector2.right);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(controller.MovementLocked, Is.False);
        Assert.That(controller.MovementVelocityOverrideActive, Is.False);
        Assert.That(endCount, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BossRecovery_FlightFollowsMovingParent_AndCleanupKillsOwnedTween(bool interrupt)
    {
        using var tweenScope = new ManualRecoveryTweenScope();
        GameObject player = Track(new GameObject("Moving recovery target"));
        player.transform.position = new Vector3(10f, 4f);
        GameObject root = Track(new GameObject("Recovery tween fixture"));
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        Texture2D texture = Track(new Texture2D(2, 2));
        Sprite sprite = Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f));
        renderer.sprite = sprite;
        Region2BossCoreRewardPresentation presentation = root.AddComponent<Region2BossCoreRewardPresentation>();
        var previousAudio = AudioManager.Instance;
        var previousRun = RunManager.Instance;
        try
        {
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Recovery audio fixture").AddComponent<AudioManager>());
            SetHotfixSingleton(typeof(RunManager), null);
            presentation.SetStoryPartSprite(sprite);
            System.Collections.IEnumerator routine = presentation.PlayRoutine(new Vector3(-3f, 2f), player.transform);
            Assert.That(routine.MoveNext(), Is.True);
            Sequence tween = (Sequence)GetHotfixField(presentation, "recoveryTween");
            tweenScope.Own(tween);
            Assert.That(tween, Is.Not.Null);
            tween.Pause();
            tween.GotoWithCallbacks(0.6f, false); // Execute the reparent handoff without a frame loop.
            Assert.That(root.transform.parent, Is.SameAs(player.transform));
            Vector3 before = root.transform.position;
            player.transform.position += Vector3.right * 7f;
            Assert.That(Vector3.Distance(root.transform.position, before + Vector3.right * 7f), Is.LessThan(0.001f));
            if (interrupt)
            {
                presentation.CleanupPresentation();
                presentation.CleanupPresentation();
                Assert.That(tween.IsActive(), Is.False);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(presentation.IsPlaying, Is.False);
            }
            else
            {
                tween.Complete(true);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(root.transform.localPosition.sqrMagnitude, Is.LessThan(0.0001f));
                Assert.That(presentation.IsCompleted, Is.True);
                Assert.That(renderer.enabled, Is.False);
            }
        }
        finally
        {
            presentation.CleanupPresentation();
            SetHotfixSingleton(typeof(AudioManager), previousAudio);
            SetHotfixSingleton(typeof(RunManager), previousRun);
        }
    }

    [Test]
    public void BossExitPlacement_SinglePrefersDeathAnchor_PairCentersOnDeathAnchor()
    {
        Vector2 death = new Vector2(17f, -11f);
        Bounds bounds = new Bounds(death, new Vector3(20f, 14f, 1f));
        Assert.That(BossExitSafePlacement.ResolveSingle(bounds, death, _ => false, out bool fallback),
            Is.EqualTo(death));
        Assert.That(fallback, Is.False);
        Vector2 displaced = BossExitSafePlacement.ResolveSingle(bounds, death,
            candidate => Vector2.Distance(candidate, death) < 0.9f, out fallback);
        Assert.That(fallback, Is.False);
        Assert.That(Vector2.Distance(displaced, death), Is.InRange(0.9f, 1.01f));
        BossExitPlacement pair = BossExitSafePlacement.Resolve(bounds, death, null, 0, _ => false, out fallback);
        Assert.That(fallback, Is.False);
        Assert.That((pair.ReturnBeaconPosition + pair.NextRegionPosition) * 0.5f, Is.EqualTo(death));
        Assert.That(pair.ReturnBeaconPosition.x, Is.LessThan(death.x));
        Assert.That(pair.NextRegionPosition.x, Is.GreaterThan(death.x));
    }

    [TestCase(CampaignBossId.SectorAdministrator, ExpeditionDepth.Normal)]
    [TestCase(CampaignBossId.SalvageDevourer, ExpeditionDepth.DeepZone1)]
    [TestCase(CampaignBossId.PhaseGatekeeper, ExpeditionDepth.DeepZone2)]
    public void BossDeath_AcceptanceSavesPartAndEndsCombatBeforePresentation(
        CampaignBossId bossId, ExpeditionDepth depth)
    {
        // Isolate singleton services and disk writes from the user's open scene/save.
        GameStateManager oldState = GameStateManager.Instance;
        RunManager oldRun = RunManager.Instance;
        PermanentProgress oldProgress = PermanentProgress.Instance;
        SaveManager oldSave = SaveManager.Instance;
        string savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "void-scrapper-boss-hotfix-" + System.Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameObject services = CreateInactiveHotfixObject("Death acceptance services");
            GameStateManager state = services.AddComponent<GameStateManager>();
            RunManager run = services.AddComponent<RunManager>();
            PermanentProgress progress = services.AddComponent<PermanentProgress>();
            SaveManager save = services.AddComponent<SaveManager>();
            SetHotfixSingleton(typeof(GameStateManager), state);
            SetHotfixSingleton(typeof(RunManager), run);
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetHotfixSingleton(typeof(SaveManager), save);
            SetPrivateField(save, "fileName", savePath);
            SetPrivateField(save, "logSavePath", false);
            SetPrivateField(state, "currentState", GameState.BossBattle);
            RunContext context = new RunContext(WeaponTreeType.MachineGun, depth);
            SetPrivateField(run, "currentRun", context);
            BossCampaignDefinition definition = Track(ScriptableObject.CreateInstance<BossCampaignDefinition>());
            SetPrivateField(definition, "bossId", bossId);
            SetPrivateField(definition, "expeditionDepth", depth);
            SetPrivateField(definition, "guaranteedPassive", null);
            SetPrivateField(definition, "coreShardReward", 0);
            BossDummyController boss = CreateInactiveHotfixObject("Accepted dead boss")
                .AddComponent<BossDummyController>();
            boss.ConfigureCampaignDefinition(definition);
            Vector3 deathPosition = new Vector3(12f, -7f);
            boss.transform.position = deathPosition;
            boss.ConfigureExitObjects(null, Vector3.one * 999f, null, Vector3.one * 999f);
            int cargoBefore = context.CurrentCargoLoad;
            PhaseGatekeeperBossController phase = null;
            FrigateTriadBossController triad = null;
            if (bossId == CampaignBossId.PhaseGatekeeper)
            {
                phase = boss.gameObject.AddComponent<PhaseGatekeeperBossController>();
                phase.EncounterEnded += () => Assert.That(state.CurrentState, Is.EqualTo(GameState.BossBattle),
                    "Region combat must stop before the shared authority releases BossBattle.");
            }
            else if (bossId == CampaignBossId.SalvageDevourer)
            {
                triad = boss.gameObject.AddComponent<FrigateTriadBossController>();
                SetPrivateField(triad, "initialized", true);
                SetPrivateField(triad, "campaignDeathAuthority", boss);
            }

            Assert.That(InvokeHotfix(boss, "AcceptBossDeath"), Is.EqualTo(true));
            if (phase != null)
            {
                Assert.That(GetHotfixField(phase, "cleanupComplete"), Is.EqualTo(true));
                phase.StopCombatForDeathPresentation();
            }
            if (triad != null)
            {
                Assert.That(GetHotfixField(triad, "initialized"), Is.EqualTo(false));
                triad.StopCombatForDeathPresentation();
                Assert.That(boss.enabled, Is.True, "Stopping Triad must retain the reward/death authority.");
            }
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Expedition));
            Assert.That(GetHotfixField(boss, "rewardExitCoordinatorStarted"), Is.EqualTo(false));
            Assert.That(GetHotfixField(boss, "recoveredStoryPart"), Is.EqualTo(true));
            Assert.That(progress.HasBossStoryPart(CampaignProgressionCatalog.GetStoryPart(bossId)), Is.True);
            Assert.That(context.CurrentCargoLoad, Is.EqualTo(cargoBefore));
            Assert.That(System.IO.File.Exists(savePath), Is.True, "Saved before any death/pickup iterator runs.");
            SaveData saved = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(savePath));
            Assert.That(saved.acquiredBossStoryParts, Does.Contain(CampaignProgressionCatalog.GetStoryPart(bossId)));

            // No death/reward iterator has run. Cancel twice, then accept a duplicate.
            InvokeHotfix(boss, "HandleRunEnded", new object[] { null });
            InvokeHotfix(boss, "HandleRunEnded", new object[] { null });
            Assert.That(InvokeHotfix(boss, "AcceptBossDeath"), Is.EqualTo(false));
            Assert.That(progress.AcquiredBossStoryParts.Count, Is.EqualTo(1));
            boss.transform.position = Vector3.zero;
            InvokeHotfix(boss, "CacheResolvedDeathContext");
            Assert.That(GetHotfixField(boss, "resolvedBossDeathPosition"), Is.EqualTo(deathPosition));

            BossDummyController repeat = CreateInactiveHotfixObject("Duplicate story boss")
                .AddComponent<BossDummyController>();
            repeat.ConfigureCampaignDefinition(definition);
            InvokeHotfix(repeat, "AcceptBossDeath");
            Assert.That(GetHotfixField(repeat, "recoveredStoryPart"), Is.EqualTo(false));
            Assert.That(progress.AcquiredBossStoryParts.Count, Is.EqualTo(1));

            EmergencyReturnController emergency = CreateInactiveHotfixObject("Emergency return probe")
                .AddComponent<EmergencyReturnController>();
            SetPrivateField(emergency, "bossBattleMessage", "boss-alive-sentinel");
            object[] args = { null };
            InvokeHotfix(emergency, "CanStart", args);
            Assert.That(args[0], Is.Not.EqualTo("boss-alive-sentinel"));

            SetPrivateField(state, "currentState", GameState.FinalBossBattle);
            InvokeHotfix(repeat, "ReleaseBossBattleState");
            Assert.That(state.CurrentState, Is.EqualTo(GameState.FinalBossBattle));
        }
        finally
        {
            SetHotfixSingleton(typeof(GameStateManager), oldState);
            SetHotfixSingleton(typeof(RunManager), oldRun);
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(SaveManager), oldSave);
            foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" })
            {
                if (System.IO.File.Exists(savePath + suffix))
                {
                    System.IO.File.Delete(savePath + suffix);
                }
            }
        }
    }

    private GameObject CreateInactiveHotfixObject(string name)
    {
        GameObject root = Track(new GameObject(name));
        root.SetActive(false);
        return root;
    }

    private NullDispatcherBossController CreateNullDispatcherFixture(out EnemyHealth health)
    {
        var root = CreateInactiveHotfixObject("NULL DISPATCHER runtime fixture");
        health = root.AddComponent<EnemyHealth>();
        SetPrivateField(health, "releaseOnDeath", false);
        SetPrivateField(health, "useProceduralDeathEffectWhenPrefabMissing", false);
        SetPrivateField(health, "hitShakeAmplitude", 0f);
        SetPrivateField(health, "deathShakeAmplitude", 0f);
        health.SetMaxHp(100f);
        var boss = root.AddComponent<NullDispatcherBossController>();
        SetPrivateField(boss, "settlementSupport", root.AddComponent<SettlementFinalSupportController>());
        return boss;
    }

    [TestCase(ExpeditionDepth.Normal, false)]
    [TestCase(ExpeditionDepth.DeepZone1, false)]
    [TestCase(ExpeditionDepth.DeepZone2, false)]
    [TestCase(ExpeditionDepth.FinalNetwork, true)]
    public void NullDispatcher_OnlyFinalNetworkCanConfigureAndBeginOnce(ExpeditionDepth depth, bool allowed)
    {
        var boss = CreateNullDispatcherFixture(out _);
        Assert.That(boss.ConfigureEncounter(depth, null), Is.EqualTo(allowed));
        Assert.That(boss.BeginCombat(), Is.EqualTo(allowed));
        Assert.That(boss.BeginCombat(), Is.False);
        Assert.That(boss.Choice, Is.EqualTo(NullDispatcherBossController.TreatmentChoice.None));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NullDispatcher_LethalCrossingRequiresNaturalChoiceBeforeDeath(bool accept)
    {
        AudioManager oldAudio = AudioManager.Instance;
        try
        {
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Final test audio").AddComponent<AudioManager>());
            var boss = CreateNullDispatcherFixture(out var health);
            var support = boss.gameObject.AddComponent<FinalBossSettlementSupportPhase>();
            boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork, null);
            int deaths = 0;
            health.Died += _ => deaths++;
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(100f), "Intro is protected.");
            boss.BeginCombat();
            health.TakeDamage(49f);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Phase1));
            Assert.That(boss.TreatmentGateCount, Is.Zero);
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(50f));
            Assert.That(health.IsDead, Is.False);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.TreatmentDialogue));
            Assert.That(boss.PatternRunning, Is.False);
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(50f));
            Assert.That(boss.TreatmentGateCount, Is.EqualTo(1));
            Assert.That(GetHotfixField(support, "triggered"), Is.EqualTo(false));
            Assert.That(boss.TryRequestTreatmentChoice(accept), Is.False, "No active offer yet.");
            SetPrivateField(boss, "offering", true);
            Assert.That(boss.Choice, Is.EqualTo(NullDispatcherBossController.TreatmentChoice.None));
            var dispatcher = new DialogueGameplayActionDispatcher();
            dispatcher.BeginConversation(NullDispatcherDialogueIds.Conversation);
            var action = accept ? DialogueGameplayActionId.FinalBossTreatmentAccept : DialogueGameplayActionId.FinalBossTreatmentReject;
            Assert.That(dispatcher.TryDispatch(action, null, null, null, boss), Is.True);
            Assert.That(boss.TryRequestTreatmentChoice(!accept), Is.False);
            Assert.That(boss.TryRequestTreatmentChoice(accept), Is.False);
            boss.FinishTreatmentDialogue(false);
            Assert.That(boss.Choice, Is.EqualTo(NullDispatcherBossController.TreatmentChoice.None), "Interrupted response is discarded.");
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.TreatmentDialogue));
            Assert.That(GetHotfixField(boss, "offering"), Is.EqualTo(false), "One retry loop can re-offer.");
            dispatcher.EndConversation();
            dispatcher.BeginConversation(NullDispatcherDialogueIds.Conversation);
            SetPrivateField(boss, "offering", true);
            Assert.That(dispatcher.TryDispatch(action, null, null, null, boss), Is.True);
            boss.FinishTreatmentDialogue(true);
            boss.FinishTreatmentDialogue(true);
            Assert.That(boss.Choice, Is.EqualTo(accept ? NullDispatcherBossController.TreatmentChoice.Accept : NullDispatcherBossController.TreatmentChoice.Reject));
            Assert.That(boss.Phase, Is.EqualTo(accept ? NullDispatcherBossController.EncounterPhase.ReclaimBeam :
                NullDispatcherBossController.EncounterPhase.RejectTransition));
            Assert.That(boss.TryRequestTreatmentChoice(!accept), Is.False);
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(50f), "Branch transition retains protection.");
            InvokeHotfix(boss, "BeginSettlementIntervention");
            InvokeHotfix(boss, "RequestSettlementSupport");
            InvokeHotfix(boss.GetComponent<SettlementFinalSupportController>(), "FinishSupportSequence");
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.PolarityPhase));
            CompleteRequiredFinalGate(boss);
            health.TakeDamage(1000f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
            Assert.That(GetHotfixField(support, "triggered"), Is.EqualTo(false), "Support is manual even below 45%.");
        }
        finally { SetHotfixSingleton(typeof(AudioManager), oldAudio); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NullDispatcher_CinematicCleanupPreservesForeignLocksAndCancelsDash(bool cancel)
    {
        using var scope = new ManualRecoveryTweenScope();
        var player = CreateInactiveHotfixObject("Final player");
        var controller = player.AddComponent<PlayerController2D>();
        var dash = player.AddComponent<PlayerDash>();
        var weapon = player.AddComponent<PlayerWeaponController>();
        SetPrivateField(dash, "controller", controller);
        SetPrivateField(dash, "rb", player.GetComponent<Rigidbody2D>());
        SetPrivateField(dash, "lastDashTime", -999f);
        SetPrivateField(dash, "isDashing", true);
        controller.SetMovementLocked(true);
        controller.SetMovementVelocityOverride(Vector2.right);
        object foreign = new object();
        controller.SetExternalControlLocked(foreign, true);
        weapon.SetExternalInputLocked(foreign, true);
        var boss = CreateNullDispatcherFixture(out var health);
        var body = CreateInactiveHotfixObject("Final authored body").AddComponent<SpriteRenderer>();
        body.transform.localScale = new Vector3(4f, 4f, 1f);
        SetPrivateField(boss, "body", body);
        boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork, player);
        var camera = CreateInactiveHotfixObject("Final owned camera").AddComponent<GungeonStyleCamera2D>();
        SetPrivateField(boss, "cameraRig", camera);
        camera.SetCinematicInputOffsetLocked(foreign, true);
        boss.BeginCombat();
        health.SetMaxHp(100, false);
        InvokeHotfix(boss, "HandleHealthChanged", health, 50f, 100f);
        InvokeHotfix(boss, "AcquireCinematic");
        Tween owned = (Tween)GetHotfixField(boss, "visualTween");
        scope.Own(owned);
        Assert.That(owned.IsActive(), Is.True);
        Assert.That(dash.IsDashing, Is.False);
        Assert.That(dash.enabled, Is.True);
        SetPrivateField(boss, "offering", true);
        Assert.That(boss.TryRequestTreatmentChoice(true), Is.True);
        if (cancel) boss.CancelEncounter();
        else boss.FinishTreatmentDialogue(true);
        Assert.That(owned.IsActive(), Is.False);
        Assert.That(GetHotfixField(boss, "visualTween"), Is.Null);
        Assert.That(body.transform.localScale, Is.EqualTo(new Vector3(4f, 4f, 1f)));
        Assert.That(controller.ControlEnabled, Is.False);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        controller.SetExternalControlLocked(foreign, false);
        weapon.SetExternalInputLocked(foreign, false);
        camera.SetCinematicInputOffsetLocked(foreign, false);
        Assert.That(controller.ControlEnabled, Is.True);
        Assert.That(controller.MovementLocked, Is.False);
        Assert.That(dash.CanDash, Is.True);
        Assert.That(weapon.ExternalInputLocked, Is.EqualTo(!cancel), "A valid choice now transfers the weapon to the branch owner.");
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.False);
        if (cancel)
        {
            boss.FinishTreatmentDialogue(true);
            Assert.That(boss.Choice, Is.EqualTo(NullDispatcherBossController.TreatmentChoice.None));
        }
        boss.CancelEncounter();
        boss.CancelEncounter();
        Assert.That(weapon.ExternalInputLocked, Is.False);
        Assert.That(boss.PatternRunning, Is.False);
        Assert.That(GetHotfixField(boss, "treatment"), Is.Null);
    }

    [Test]
    public void NullDispatcher_HealthFloorRemovalPreservesOtherOwners()
    {
        AudioManager oldAudio = AudioManager.Instance;
        try
        {
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Floor test audio").AddComponent<AudioManager>());
            CreateNullDispatcherFixture(out var health);
            object a = new object(), b = new object();
            health.SetDamageFloor(a, 0.5f);
            health.SetDamageFloor(b, 0.8f);
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(80f));
            health.RemoveDamageFloor(b);
            health.RemoveDamageFloor(b);
            health.TakeDamage(1000f);
            Assert.That(health.CurrentHp, Is.EqualTo(50f));
            health.SetDamageFloor(b, 1f);
            health.TakeDamage(1f);
            Assert.That(health.CurrentHp, Is.EqualTo(50f), "A floor never heals.");
            health.RemoveDamageFloor(a);
            health.RemoveDamageFloor(b);
            health.TakeDamage(1000f);
            Assert.That(health.IsDead, Is.True);
        }
        finally { SetHotfixSingleton(typeof(AudioManager), oldAudio); }
    }

    [Test]
    public void NullDispatcher_AuthoredBindingsAndPatternOrderAreComplete()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab");
        var boss = prefab.GetComponent<NullDispatcherBossController>();
        Assert.That(boss, Is.Not.Null);
        foreach (string field in new[] { "health", "body", "laserPrefab", "lineMaterial", "projectile", "treatmentEntry",
            "settlementSupport", "reclaimBeam", "beamOrigin", "breakPulse" })
            Assert.That(GetHotfixField(boss, field), Is.Not.Null, field);
        var relays = (SpriteRenderer[])GetHotfixField(boss, "relays");
        Assert.That(relays.Length, Is.EqualTo(2));
        Assert.That(relays[0], Is.Not.SameAs(relays[1]));
        foreach (var relay in relays) Assert.That(relay.enabled, Is.False);
        Assert.That((float)GetHotfixField(boss, "telegraphSeconds"), Is.InRange(0.5f, 1.2f));
        Assert.That((float)GetHotfixField(boss, "recoverySeconds"), Is.InRange(0.7f, 1.2f));
        Assert.That(prefab.GetComponent<DialogueStoryEntryPoint>().ConversationTitle, Is.EqualTo(NullDispatcherDialogueIds.Conversation));
        Assert.That(prefab.GetComponent<SettlementFinalSupportController>().enabled, Is.True);
        Assert.That(((LineRenderer)GetHotfixField(boss, "reclaimBeam")).enabled, Is.False);
        Assert.That(((LineRenderer)GetHotfixField(boss, "reclaimBeam")).positionCount, Is.EqualTo(2));
        int previous = -1;
        var seen = new HashSet<NullDispatcherBossController.Pattern>();
        for (int i = 0; i < 12; i++)
        {
            var next = NullDispatcherBossController.NextPattern(previous);
            Assert.That((int)next, Is.Not.EqualTo(previous));
            seen.Add(next);
            previous = (int)next;
        }
        Assert.That(seen.Count, Is.EqualTo(3));
    }

    [TestCase(GameState.Boot)]
    [TestCase(GameState.ExpeditionLoading)]
    [TestCase(GameState.RunResult)]
    public void NullDispatcher_SceneEndingClearsOwnedObjectsAndPreventsChoiceResume(GameState destination)
    {
        var boss = CreateNullDispatcherFixture(out var health);
        boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork, null);
        boss.BeginCombat();
        InvokeHotfix(boss, "HandleHealthChanged", health, 50f, 100f);
        SetPrivateField(boss, "offering", true);
        Assert.That(boss.TryRequestTreatmentChoice(true), Is.True);
        var ownedLaser = CreateInactiveHotfixObject("Owned stopped laser").AddComponent<BossLaserHazard>();
        var ownedShot = CreateInactiveHotfixObject("Owned stopped bullet").AddComponent<Bullet>();
        var lasers = (List<BossLaserHazard>)GetHotfixField(boss, "lasers");
        var shots = (List<Bullet>)GetHotfixField(boss, "shots");
        lasers.Add(ownedLaser);
        shots.Add(ownedShot);
        var relay = CreateInactiveHotfixObject("Owned relay").AddComponent<SpriteRenderer>();
        relay.enabled = true;
        SetPrivateField(boss, "relays", new[] { relay });
        InvokeHotfix(boss, "HandleStateChanged", GameState.FinalBossBattle, destination);
        boss.CancelEncounter();
        boss.FinishTreatmentDialogue(true);
        Assert.That(lasers, Is.Empty);
        Assert.That(shots, Is.Empty);
        Assert.That(relay.enabled, Is.False);
        Assert.That(boss.Choice, Is.EqualTo(NullDispatcherBossController.TreatmentChoice.None));
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
        Assert.That(boss.BeginCombat(), Is.False);
        Assert.That(boss.TryRequestTreatmentChoice(false), Is.False);
        Assert.That(GetHotfixField(boss, "treatment"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDispatcher_SharedTelegraphCannotEnableDamageAfterPhaseGate(bool crossGate)
    {
        RunManager oldRun = RunManager.Instance;
        try
        {
            var run = CreateInactiveHotfixObject("Final telegraph run").AddComponent<RunManager>();
            SetPrivateField(run, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.FinalNetwork));
            SetHotfixSingleton(typeof(RunManager), run);
            var boss = CreateNullDispatcherFixture(out var health);
            boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork, null);
            boss.BeginCombat();
            // Advance the shared coroutine protocol explicitly; no timing or Editor frame-loop claims.
            var warning = (System.Collections.IEnumerator)InvokeHotfix(boss, "WaitForTelegraph");
            Assert.That(warning.MoveNext(), Is.True);
            Assert.That(warning.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(boss.PatternDamageEnabled, Is.False);
            if (crossGate) InvokeHotfix(boss, "HandleHealthChanged", health, 50f, 100f);
            Assert.That(warning.MoveNext(), Is.False);
            Assert.That(boss.PatternDamageEnabled, Is.EqualTo(!crossGate));
            boss.CancelEncounter();
            Assert.That(boss.PatternDamageEnabled, Is.False);
        }
        finally { SetHotfixSingleton(typeof(RunManager), oldRun); }
    }

    private Rigidbody2D CreateLiveMovementBodyFixture(PlayerController2D controller)
    {
        var body = Track(new GameObject("Live movement body fixture")).AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = controller.transform.position;
        SetPrivateField(controller, "rb", body);
        return body;
    }

    private NullDispatcherBossController CreateReclaimFixture(float hp, bool accept, out PlayerController2D player,
        out PlayerHealth vitals, out PlayerWeaponController weapon, out SettlementFinalSupportController support)
    {
        var actor = CreateInactiveHotfixObject("Reclaim player fixture");
        actor.transform.position = Vector3.right * 10f;
        player = actor.AddComponent<PlayerController2D>();
        SetPrivateField(player, "rb", actor.GetComponent<Rigidbody2D>());
        vitals = actor.AddComponent<PlayerHealth>();
        vitals.SetMaxHp(20f, true);
        vitals.RestoreCurrentHp(hp);
        var armor = actor.AddComponent<PlayerArmor>();
        armor.SetMaxArmor(12f, true);
        SetPrivateField(vitals, "armor", armor);
        var shield = actor.AddComponent<ComponentShieldPassive>();
        shield.RechargeNow();
        SetPrivateField(vitals, "componentShield", shield);
        var dash = actor.AddComponent<PlayerDash>();
        SetPrivateField(dash, "controller", player);
        SetPrivateField(dash, "rb", actor.GetComponent<Rigidbody2D>());
        SetPrivateField(dash, "lastDashTime", -999f);
        weapon = actor.AddComponent<PlayerWeaponController>();
        var boss = CreateNullDispatcherFixture(out var bossHealth);
        support = boss.GetComponent<SettlementFinalSupportController>();
        SetPrivateField(support, "playerHealth", vitals);
        SetPrivateField(support, "playerArmor", armor);
        SetPrivateField(support, "playerController", player);
        SetPrivateField(support, "playerDash", dash);
        var beam = CreateInactiveHotfixObject("Authored beam fixture").AddComponent<LineRenderer>();
        beam.positionCount = 2;
        beam.enabled = false;
        SetPrivateField(boss, "reclaimBeam", beam);
        SetPrivateField(boss, "beamOrigin", boss.transform);
        boss.ConfigureEncounter(ExpeditionDepth.FinalNetwork, actor);
        boss.BeginCombat();
        // Public health notification reaches exactly 50% without invoking optional hit feedback.
        bossHealth.SetMaxHp(200f, false);
        SetPrivateField(boss, "offering", true);
        Assert.That(boss.TryRequestTreatmentChoice(accept), Is.True);
        boss.FinishTreatmentDialogue(true);
        return boss;
    }

    [TestCase(20f, 10f)]
    [TestCase(14f, 10f)]
    [TestCase(8f, 8f)]
    [TestCase(1f, 1f)]
    [TestCase(0.5f, 0.5f)]
    public void NullDispatcher4B_ReclaimIsDirectNonLethalAndDoesNotHeal(float startHp, float floor)
    {
        var boss = CreateReclaimFixture(startHp, true, out var player, out var hp, out var weapon, out var support);
        SetPrivateField(hp, "dashInvincible", true);
        int damageEvents = 0, deaths = 0;
        hp.Damaged += (_, __) => damageEvents++;
        hp.Died += () => deaths++;
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.ReclaimBeam));
        Assert.That(boss.PatternRunning, Is.False);
        Assert.That(boss.PatternDamageEnabled, Is.False);
        Assert.That(player.ControlEnabled, Is.True);
        Assert.That(player.MovementLocked, Is.False);
        Assert.That(player.GetComponent<PlayerDash>().CanDash, Is.True);
        Assert.That(player.GetComponent<PlayerDash>().enabled, Is.True);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        Assert.That(boss.TryRequestTreatmentChoice(true), Is.False);
        boss.FinishTreatmentDialogue(true);
        InvokeHotfix(boss, "AdvanceChoiceBranch", 4.5f);
        Assert.That(hp.CurrentHp, Is.EqualTo(floor).Within(0.001f));
        Assert.That(hp.IsDead, Is.False);
        Assert.That(hp.IsInvincible, Is.True);
        Assert.That(damageEvents, Is.Zero);
        Assert.That(deaths, Is.Zero);
        Assert.That(hp.MinimumHealthFloor, Is.Zero, "No permanent health floor was installed.");
        Assert.That(player.GetComponent<PlayerArmor>().CurrentArmor, Is.EqualTo(12f));
        Assert.That(player.GetComponent<ComponentShieldPassive>().IsCharged, Is.True);
        Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.SettlementIntervention));
        Assert.That(support.SequenceTriggered, Is.False, "Break presentation precedes facility support.");
        Assert.That(((LineRenderer)GetHotfixField(boss, "reclaimBeam")).enabled, Is.False);
        boss.CancelEncounter();
        boss.CancelEncounter();
        InvokeHotfix(boss, "AdvanceChoiceBranch", 100f);
        Assert.That(hp.CurrentHp, Is.EqualTo(floor).Within(0.001f));
        Assert.That(support.SequenceTriggered, Is.False);
        Assert.That(weapon.ExternalInputLocked, Is.False);
    }

    [Test]
    public void NullDispatcher4B_PullComposesWithMovementDashAndForeignPush()
    {
        var boss = CreateReclaimFixture(20f, true, out var player, out _, out _, out _);
        // Unity ignores velocity writes on an inactive physics body in EditMode. Keep
        // gameplay components inactive and explicitly bind a live, fixture-owned body.
        var rb = CreateLiveMovementBodyFixture(player);
        object foreign = new object();
        player.SetExternalPushVelocity(foreign, Vector2.up * 2f);
        InvokeHotfix(boss, "AdvanceChoiceBranch", 0.1f);
        InvokeHotfix(player, "MovePlayer");
        Assert.That(rb.linearVelocity.x, Is.EqualTo(-4f).Within(0.001f), "Standing still is pulled inward.");
        InvokeHotfix(player, "SetMoveInput", Vector2.right);
        InvokeHotfix(player, "MovePlayer");
        Assert.That(rb.linearVelocity.x, Is.EqualTo(2f).Within(0.001f), "Normal movement can resist.");
        player.SetMovementVelocityOverride(Vector2.right * 20f);
        InvokeHotfix(player, "MovePlayer");
        Assert.That(rb.linearVelocity.x, Is.EqualTo(16f).Within(0.001f), "Dash override overpowers pull.");
        boss.CancelEncounter();
        player.ClearMovementVelocityOverride();
        InvokeHotfix(player, "SetMoveInput", Vector2.zero);
        InvokeHotfix(player, "MovePlayer");
        Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.up * 2f));
        player.ClearExternalPush(foreign);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NullDispatcher4B_BranchesConvergeOnceAndWeaponLabCanDamage(bool accept)
    {
        AudioManager oldAudio = AudioManager.Instance;
        try
        {
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Intervention audio").AddComponent<AudioManager>());
            var boss = CreateReclaimFixture(20f, accept, out _, out var hp, out var weapon, out var support);
            Assert.That(boss.Phase, Is.EqualTo(accept ? NullDispatcherBossController.EncounterPhase.ReclaimBeam :
                NullDispatcherBossController.EncounterPhase.RejectTransition));
            InvokeHotfix(boss, "AdvanceChoiceBranch", accept ? 4.5f : 0.75f);
            float expected = accept ? 10f : 20f;
            Assert.That(hp.CurrentHp, Is.EqualTo(expected).Within(0.001f));
            Assert.That(boss.PatternRunning, Is.False);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 1.5f);
            Assert.That(boss.SupportRequested, Is.True);
            Assert.That(support.SequenceTriggered, Is.True);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.SettlementIntervention));
            Assert.That(weapon.ExternalInputLocked, Is.True);
            Assert.That(support.TriggerFullSupport(), Is.False);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 10f);
            Assert.That(hp.CurrentHp, Is.EqualTo(expected).Within(0.001f), "No drain continues during support.");
            EnemyHealth enemy = boss.GetComponent<EnemyHealth>();
            float before = enemy.CurrentHp;
            InvokeHotfix(support, "ApplyWeaponLabSupport", 2);
            Assert.That(enemy.CurrentHp, Is.EqualTo(before - 20f));
            int completions = 0;
            support.Completed += () => completions++;
            InvokeHotfix(support, "FinishSupportSequence");
            InvokeHotfix(support, "FinishSupportSequence");
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.PolarityPhase));
            Assert.That(weapon.ExternalInputLocked, Is.False);
            CompleteRequiredFinalGate(boss);
            enemy.TakeDamage(1000f);
            Assert.That(enemy.IsDead, Is.True);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
        }
        finally { SetHotfixSingleton(typeof(AudioManager), oldAudio); }
    }

    [TestCase(GameState.Boot)]
    [TestCase(GameState.ExpeditionLoading)]
    [TestCase(GameState.RunResult)]
    public void NullDispatcher4B_ExitDuringBeamStopsDrainPullAndDelayedSupport(GameState destination)
    {
        var boss = CreateReclaimFixture(20f, true, out var player, out var hp, out var weapon, out var support);
        object foreign = new object();
        weapon.SetExternalInputLocked(foreign, true);
        player.SetExternalControlLocked(foreign, true);
        InvokeHotfix(boss, "AdvanceChoiceBranch", 1f);
        float before = hp.CurrentHp;
        InvokeHotfix(boss, "HandleStateChanged", GameState.FinalBossBattle, destination);
        boss.CancelEncounter();
        InvokeHotfix(boss, "AdvanceChoiceBranch", 50f);
        InvokeHotfix(boss, "RequestSettlementSupport");
        Assert.That(hp.CurrentHp, Is.EqualTo(before));
        Assert.That(support.SequenceTriggered, Is.False);
        Assert.That(GetHotfixField(player, "ownedPushVelocities"), Is.Empty);
        Assert.That(GetHotfixField(boss, "branchRoutine"), Is.Null);
        Assert.That(GetHotfixField(boss, "branchVisualTween"), Is.Null);
        Assert.That(weapon.ExternalInputLocked, Is.True);
        Assert.That(player.ControlEnabled, Is.False);
        weapon.SetExternalInputLocked(foreign, false);
        player.SetExternalControlLocked(foreign, false);
        Assert.That(weapon.ExternalInputLocked, Is.False);
        Assert.That(player.ControlEnabled, Is.True);
    }

    [Test]
    public void NullDispatcher4B_EngineSupportCleanupPreservesBaseAndForeignModifiers()
    {
        var boss = CreateReclaimFixture(20f, false, out var player, out _, out _, out var support);
        var dash = player.GetComponent<PlayerDash>();
        object foreign = new object();
        player.SetExternalMoveSpeedMultiplier(foreign, 1.5f);
        dash.SetExternalCooldownMultiplier(foreign, 0.8f);
        float cooldown = dash.DashCooldown;
        var engine = (System.Collections.IEnumerator)InvokeHotfix(support, "EngineBuffRoutine", 2);
        Assert.That(engine.MoveNext(), Is.True);
        Assert.That(player.EffectiveMoveSpeed, Is.EqualTo(6f * 1.5f * 1.12f).Within(0.001f));
        Assert.That(dash.EffectiveDashCooldown, Is.EqualTo((cooldown - 0.06f) * 0.8f).Within(0.001f));
        player.SetMoveSpeed(7f); // A legitimate later base-stat change must survive cancellation.
        support.CancelSupportSequence();
        support.CancelSupportSequence();
        Assert.That(player.MoveSpeed, Is.EqualTo(7f));
        Assert.That(player.EffectiveMoveSpeed, Is.EqualTo(10.5f));
        Assert.That(dash.DashCooldown, Is.EqualTo(cooldown));
        Assert.That(dash.EffectiveDashCooldown, Is.EqualTo(cooldown * 0.8f).Within(0.001f));
        player.ClearExternalMoveSpeedMultiplier(foreign);
        dash.ClearExternalCooldownMultiplier(foreign);
        boss.CancelEncounter();
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(2)]
    public void NullDispatcher4B_SupportCompletesWithMissingOrUnavailableBuildings(int buildingLevel)
    {
        var oldProgress = PermanentProgress.Instance;
        var oldAudio = AudioManager.Instance;
        try
        {
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Support test audio").AddComponent<AudioManager>());
            var progress = buildingLevel < 0 ? null : CreateInactiveHotfixObject("Support progress").AddComponent<PermanentProgress>();
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            if (progress != null)
            {
                progress.SetBuildingLevel(BuildingType.Hangar, buildingLevel);
                progress.SetBuildingLevel(BuildingType.WeaponLab, buildingLevel);
                progress.SetBuildingLevel(BuildingType.RecoveryProcessor, buildingLevel);
                progress.SetBuildingLevel(BuildingType.EngineWorkshop, 0); // Engine timing is covered separately.
            }
            var boss = CreateReclaimFixture(8f, false, out var player, out var hp, out _, out var support);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 0.75f);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 1.5f);
            int completions = 0;
            support.Completed += () => completions++;
            var sequence = (System.Collections.IEnumerator)InvokeHotfix(support, "SupportRoutine");
            while (sequence.MoveNext()) Assert.That(sequence.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(support.SequenceCompleted, Is.True);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.PolarityPhase));
            Assert.That(hp.CurrentHp, Is.EqualTo(buildingLevel > 0 ? 15f : 8f));
            Assert.That(player.GetComponent<PlayerArmor>().CurrentArmor, Is.EqualTo(buildingLevel > 0 ? 14f : 12f));
            Assert.That(boss.GetComponent<EnemyHealth>().CurrentHp, Is.EqualTo(buildingLevel > 0 ? 80f : 100f));
            Assert.That(support.TriggerFullSupport(), Is.False);
        }
        finally
        {
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(AudioManager), oldAudio);
        }
    }

    [TestCase(2f, true)]
    [TestCase(10f, false)]
    public void NullDispatcher4B_BreakPushIsOutwardAndBounded(float distance, bool shouldPush)
    {
        var boss = CreateReclaimFixture(20f, true, out var player, out _, out _, out _);
        // Isolate endpoint validation from unrelated loaded-scene authoring using a distant fixture area.
        boss.transform.position = new Vector3(30000f, 30000f, 0f);
        player.transform.position = boss.transform.position + Vector3.right * distance;
        SetPrivateField(player, "repositionBlockingLayers", (LayerMask)(1 << 0));
        var rb = CreateLiveMovementBodyFixture(player);
        InvokeHotfix(boss, "AdvanceChoiceBranch", 4.5f);
        InvokeHotfix(player, "MovePlayer");
        float speed = rb.linearVelocity.x;
        Assert.That(speed > 0f, Is.EqualTo(shouldPush));
        Assert.That(speed, Is.InRange(0f, 2f / 0.3f));
        InvokeHotfix(boss, "AdvanceChoiceBranch", 0.3f);
        InvokeHotfix(player, "MovePlayer");
        Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.zero));
        boss.CancelEncounter();
    }

    [Test]
    public void NullDispatcher4B_ZeroGameplayDeltaAndRunEndingCannotAdvanceBeam()
    {
        RunManager oldRun = RunManager.Instance;
        try
        {
            var run = CreateInactiveHotfixObject("Reclaim run owner").AddComponent<RunManager>();
            SetPrivateField(run, "currentRun", new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.FinalNetwork));
            SetHotfixSingleton(typeof(RunManager), run);
            var boss = CreateReclaimFixture(20f, true, out var player, out var hp, out var weapon, out var support);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 0f);
            Assert.That(hp.CurrentHp, Is.EqualTo(20f));
            Assert.That(GetHotfixField(player, "ownedPushVelocities"), Is.Empty);
            Assert.That(GetHotfixField(boss, "branchElapsed"), Is.EqualTo(0f));
            Assert.That(NullDispatcherBossController.CalculateDrainedHp(4f, 10f, 5f), Is.EqualTo(4f));
            SetPrivateField(run, "runEndingActive", true);
            var sequence = (System.Collections.IEnumerator)InvokeHotfix(boss, "ChoiceBranchRoutine");
            Assert.That(sequence.MoveNext(), Is.False);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
            Assert.That(hp.CurrentHp, Is.EqualTo(20f));
            Assert.That(support.SequenceTriggered, Is.False);
            Assert.That(weapon.ExternalInputLocked, Is.False);
        }
        finally { SetHotfixSingleton(typeof(RunManager), oldRun); }
    }

    [Test]
    public void NullDispatcher4B_PauseStopsGameplayAndCancellationDoesNotCompleteSupport()
    {
        var singleton = typeof(GameplayPauseManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = singleton.GetValue(null);
        var pause = CreateInactiveHotfixObject("Reclaim pause fixture").AddComponent<GameplayPauseManager>();
        object pauseOwner = new object();
        try
        {
            singleton.SetValue(null, pause);
            var boss = CreateReclaimFixture(20f, true, out var player, out var hp, out _, out var support);
            pause.PushPause(pauseOwner, "Reclaim pause test");
            InvokeHotfix(boss, "AdvanceChoiceBranch", 4.5f);
            Assert.That(hp.CurrentHp, Is.EqualTo(20f));
            Assert.That(GetHotfixField(player, "ownedPushVelocities"), Is.Empty);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.ReclaimBeam));
            pause.PopPause(pauseOwner);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 4.5f);
            InvokeHotfix(boss, "AdvanceChoiceBranch", 1.5f);
            Assert.That(support.SequenceTriggered, Is.True);
            int completions = 0;
            support.Completed += () => completions++;
            boss.CancelEncounter();
            InvokeHotfix(support, "FinishSupportSequence");
            Assert.That(completions, Is.Zero);
            Assert.That(support.SequenceCompleted, Is.False);
            Assert.That(boss.Phase, Is.EqualTo(NullDispatcherBossController.EncounterPhase.Dead));
        }
        finally
        {
            pause.PopPause(pauseOwner);
            singleton.SetValue(null, previous);
        }
    }

    [Test]
    public void NullDispatcher4B_CancelKillsOwnedBeamTweenAndRestoresAuthoredWidth()
    {
        using var scope = new ManualRecoveryTweenScope();
        var boss = CreateReclaimFixture(20f, true, out _, out _, out _, out _);
        var beam = (LineRenderer)GetHotfixField(boss, "reclaimBeam");
        float authored = beam.widthMultiplier;
        Tween pulse = DOTween.To(() => beam.widthMultiplier, value => beam.widthMultiplier = value, authored * 2f, 1f);
        scope.Own(pulse);
        SetPrivateField(boss, "branchVisualTween", pulse);
        pulse.Goto(0.5f, false);
        Assert.That(beam.widthMultiplier, Is.GreaterThan(authored));
        boss.CancelEncounter();
        boss.CancelEncounter();
        Assert.That(pulse.IsActive(), Is.False);
        Assert.That(GetHotfixField(boss, "branchVisualTween"), Is.Null);
        Assert.That(beam.enabled, Is.False);
        Assert.That(beam.widthMultiplier, Is.EqualTo(authored));
    }

    private SettlementRouteCoreController CreateRouteCoreFixture(string name)
    {
        GameObject root = CreateInactiveHotfixObject(name);
        root.AddComponent<CircleCollider2D>();
        var route = root.AddComponent<SettlementRouteCoreController>();
        SetPrivateField(route, "settlementHud", root.AddComponent<SettlementHUD>());
        SetPrivateField(route, "settlementController", root.AddComponent<SettlementController>());
        return route;
    }

    private SettlementDefenseEncounterController CreateCorruptedDefenseFixture(SettlementRouteCoreController route)
    {
        var encounter = CreateInactiveHotfixObject("Corrupted defense").AddComponent<SettlementDefenseEncounterController>();
        SetPrivateField(encounter, "routeCore", route);
        SetPrivateField(encounter, "deckOpen", true);
        SetPrivateField(encounter, "managementWasActive", true);
        SetPrivateField(encounter, "managementCanvas", CreateInactiveHotfixObject("Management"));
        SetPrivateField(encounter, "deckRoot", CreateInactiveHotfixObject("Owned cores"));
        SetPrivateField(encounter, "deckCanvas", CreateInactiveHotfixObject("Deck HUD"));
        GameObject playerRoot = CreateInactiveHotfixObject("Defense player");
        playerRoot.AddComponent<PlayerController2D>();
        playerRoot.AddComponent<PlayerDash>();
        playerRoot.AddComponent<PlayerWeaponController>();
        SetPrivateField(encounter, "player", playerRoot.AddComponent<PlayerHealth>());
        SetPrivateField(encounter, "centralVisual", route.gameObject.AddComponent<SpriteRenderer>());
        SetPrivateField(encounter, "corruptionWave", CreateInactiveHotfixObject("Authored pulse").AddComponent<SpriteRenderer>());
        GameObject navigation = CreateInactiveHotfixObject("Facility navigation");
        navigation.SetActive(true);
        SetPrivateField(encounter, "facilityNavigation", navigation);
        var parts = new[] { BossStoryPart.SectorStabilizer, BossStoryPart.PhaseNavigationLens, BossStoryPart.MatterCompressor };
        var configs = new SettlementDefenseEncounterController.ComponentCore[3];
        for (int i = 0; i < 3; i++)
        {
            configs[i] = new SettlementDefenseEncounterController.ComponentCore
            {
                part = parts[i], hp = 70f, color = Color.white,
                combatPoint = CreateInactiveHotfixObject("Core position " + i).transform
            };
            configs[i].combatPoint.position = new Vector3(i * 3f, 2f);
        }
        SetPrivateField(encounter, "componentCores", configs);
        SetPrivateField(encounter, "corePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/Enemy/PF_SettlementCorruptedCore.prefab").GetComponent<SettlementDefenseCorruptedCore>());
        var requested = new UnityEngine.Events.UnityEvent();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(requested, encounter.BeginEncounter);
        requested.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        SetPrivateField(route, "defenseRequested", requested);
        return encounter;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CorruptedDefense_SequentialInteractionAndFusionOwnCompletion(bool failAndRetry)
    {
        var oldProgress = PermanentProgress.Instance;
        var oldSave = SaveManager.Instance;
        var oldState = GameStateManager.Instance;
        var oldAudio = AudioManager.Instance;
        SettlementDefenseEncounterController encounter = null;
        try
        {
            var progress = CreateInactiveHotfixObject("Defense progress").AddComponent<PermanentProgress>();
            var state = CreateInactiveHotfixObject("Defense state").AddComponent<GameStateManager>();
            SetPrivateField(state, "currentState", GameState.Settlement);
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetHotfixSingleton(typeof(SaveManager), null);
            SetHotfixSingleton(typeof(GameStateManager), state);
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Defense audio").AddComponent<AudioManager>());
            progress.TryStartDamagedAccessKeyQuest();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            var route = CreateRouteCoreFixture("Defense route");
            route.Interact(CreateInactiveHotfixObject("Before restoration"));
            Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.ReadyToAssemble));
            Assert.That(progress.TryRestoreDamagedAccessKey(), Is.True);
            Assert.That(progress.TryAssembleRouteCore(), Is.False);
            encounter = CreateCorruptedDefenseFixture(route);
            int completions = 0;
            var completed = new UnityEngine.Events.UnityEvent();
            completed.AddListener(() => completions++);
            SetPrivateField(route, "defenseCompleted", completed);
            Assert.That(route.TryActivateRouteCore(), Is.True);
            Assert.That(route.TryActivateRouteCore(), Is.False);
            GameObject navigation = (GameObject)GetHotfixField(encounter, "facilityNavigation");
            var interactor = CreateInactiveHotfixObject("Reactivate player");
            int attempts = failAndRetry ? 2 : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Assert.That(encounter.IsActive, Is.True);
                var player = (PlayerHealth)GetHotfixField(encounter, "player");
                Assert.That(player.GetComponent<PlayerController2D>().ControlEnabled, Is.True);
                Assert.That(player.GetComponent<PlayerController2D>().MovementLocked, Is.False);
                Assert.That(player.GetComponent<PlayerDash>().enabled, Is.True);
                Assert.That(player.GetComponent<PlayerWeaponController>().enabled, Is.True);
                Assert.That(encounter.Cores.Count, Is.EqualTo(3));
                Assert.That(encounter.FusedCount, Is.Zero);
                Assert.That(navigation.activeSelf, Is.False);
                Assert.That(route.BeginSettlementDefense(), Is.False);
                encounter.BeginEncounter();
                encounter.LeaveDeck();
                Assert.That(encounter.Cores.Count, Is.EqualTo(3));
                Assert.That(encounter.IsActive, Is.True, "Facility navigation cannot cancel active combat.");
                var identities = new[] { BossStoryPart.SectorStabilizer, BossStoryPart.PhaseNavigationLens, BossStoryPart.MatterCompressor };
                foreach (var core in encounter.Cores)
                {
                    Assert.That(core.State, Is.EqualTo(SettlementDefenseCorruptedCore.CoreState.Dormant));
                    Assert.That(core.Health.IsDead, Is.False);
                    Assert.That(core.Health.CurrentHp, Is.EqualTo(core.Health.MaxHp));
                    Assert.That(((Collider2D)GetHotfixField(core, "combatCollider")).enabled, Is.False);
                    SetPrivateField(core.Health, "hitShakeAmplitude", 0f);
                    SetPrivateField(core.Health, "deathShakeAmplitude", 0f);
                }
                InvokeHotfix(encounter, "CompleteEncounter");
                Assert.That(progress.CanLaunchFinalExpedition, Is.False);
                InvokeHotfix(encounter, "FinishOpening");
                for (int phase = 0; phase < 3; phase++)
                {
                    var core = encounter.Cores[phase];
                    Assert.That(core.Part, Is.EqualTo(identities[phase]));
                    Assert.That(core.State, Is.EqualTo(SettlementDefenseCorruptedCore.CoreState.CombatActive));
                    for (int pending = phase + 1; pending < 3; pending++)
                    {
                        Assert.That(encounter.Cores[pending].State, Is.EqualTo(SettlementDefenseCorruptedCore.CoreState.Dormant));
                    }
                    core.Health.TakeDamage(1000f);
                    InvokeHotfix(core, "HandleDefeat", core.Health);
                    Assert.That(core.State, Is.EqualTo(SettlementDefenseCorruptedCore.CoreState.AwaitingReactivation));
                    Assert.That(core.Visual.enabled, Is.True);
                    Assert.That(core.CanInteract(interactor), Is.True);
                    Assert.That(encounter.FusedCount, Is.EqualTo(phase));
                    encounter.CoreFused(core); // Health defeat is not a fusion callback.
                    InvokeHotfix(encounter, "CompleteEncounter");
                    Assert.That(progress.SettlementDefenseCleared, Is.False);
                    Assert.That(completions, Is.Zero);
                    if (phase == 0)
                    {
                        encounter.Cores[1].Health.TakeDamage(1000f);
                        encounter.Cores[2].Health.TakeDamage(1000f);
                        Assert.That(encounter.Cores[1].CanInteract(interactor), Is.False);
                        Assert.That(encounter.Cores[2].CanInteract(interactor), Is.False);
                        Assert.That(encounter.FusedCount, Is.Zero);
                        Assert.That(progress.CanLaunchFinalExpedition, Is.False);
                    }
                    core.Interact(interactor);
                    Assert.That(core.State, Is.EqualTo(SettlementDefenseCorruptedCore.CoreState.Fusing));
                    Assert.That(core.CanInteract(interactor), Is.False);
                    object ownedTween = GetHotfixField(core, "fusion");
                    core.Interact(interactor);
                    Assert.That(GetHotfixField(core, "fusion"), Is.SameAs(ownedTween));
                    if (failAndRetry && attempt == 0)
                    {
                        InvokeHotfix(encounter, "HandlePlayerDied"); // Interrupt the owned flight.
                        encounter.CancelEncounter();
                        Assert.That(encounter.IsActive, Is.False);
                        Assert.That(encounter.Cores.Count, Is.Zero);
                        Assert.That(navigation.activeSelf, Is.True);
                        Assert.That(((GameObject)GetHotfixField(encounter, "managementCanvas")).activeSelf, Is.True);
                        Assert.That(progress.SettlementDefenseCleared, Is.False);
                        Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.Activated));
                        Assert.That(state.CurrentState, Is.EqualTo(GameState.Settlement));
                        // Re-enter the authored deck without a live scene or InputActions.
                        SetPrivateField(encounter, "deckOpen", true);
                        Assert.That(route.BeginSettlementDefense(), Is.True);
                        break;
                    }
                    InvokeHotfix(core, "FinishFusion"); // Deterministic visual completion, no timing wait.
                    InvokeHotfix(core, "FinishFusion");
                    encounter.CoreFused(core);
                    Assert.That(encounter.FusedCount, Is.EqualTo(phase + 1));
                    var pulse = (DG.Tweening.Sequence)GetHotfixField(encounter, "centralPulse");
                    DG.Tweening.TweenExtensions.Complete(pulse, true);
                }
            }
            Assert.That(encounter.IsActive, Is.False);
            Assert.That(encounter.FusedCount, Is.EqualTo(3));
            Assert.That(completions, Is.EqualTo(1));
            route.CompleteSettlementDefense();
            InvokeHotfix(encounter, "CompleteEncounter");
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(navigation.activeSelf, Is.True);
            Assert.That(((GameObject)GetHotfixField(encounter, "managementCanvas")).activeSelf, Is.True);
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Settlement));
            Assert.That(progress.CanLaunchFinalExpedition, Is.True);
            Assert.That(progress.AcquiredBossStoryParts.Count, Is.EqualTo(3));
            var loaded = CreateInactiveHotfixObject("Reload stabilized core").AddComponent<PermanentProgress>();
            loaded.LoadFromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(progress.CreateSaveData())));
            Assert.That(loaded.CanLaunchFinalExpedition, Is.True);
            Assert.That(loaded.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.Activated));
            encounter.CancelEncounter();
            InvokeHotfix(encounter, "OnDisable");
            InvokeHotfix(encounter, "OnDisable");
        }
        finally
        {
            encounter?.CancelEncounter();
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(SaveManager), oldSave);
            SetHotfixSingleton(typeof(GameStateManager), oldState);
            SetHotfixSingleton(typeof(AudioManager), oldAudio);
        }
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    public void CorruptedDefense_CancelAtEveryStageRestoresOnlyOwnedState(int stage, bool navigationVisible)
    {
        var oldProgress = PermanentProgress.Instance;
        var oldSave = SaveManager.Instance;
        var oldState = GameStateManager.Instance;
        var oldAudio = AudioManager.Instance;
        SettlementDefenseEncounterController encounter = null;
        try
        {
            var progress = CreateInactiveHotfixObject("Cancellation progress").AddComponent<PermanentProgress>();
            var state = CreateInactiveHotfixObject("Cancellation state").AddComponent<GameStateManager>();
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetHotfixSingleton(typeof(SaveManager), null);
            SetHotfixSingleton(typeof(GameStateManager), state);
            SetHotfixSingleton(typeof(AudioManager), CreateInactiveHotfixObject("Cancellation audio").AddComponent<AudioManager>());
            progress.TryStartDamagedAccessKeyQuest();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            progress.TryRestoreDamagedAccessKey();
            progress.TryActivateRouteCore();
            string savedBefore = JsonUtility.ToJson(progress.CreateSaveData());
            var route = CreateRouteCoreFixture("Cancellation route");
            encounter = CreateCorruptedDefenseFixture(route);
            var navigation = (GameObject)GetHotfixField(encounter, "facilityNavigation");
            navigation.SetActive(navigationVisible);
            var player = (PlayerHealth)GetHotfixField(encounter, "player");
            var controller = player.GetComponent<PlayerController2D>();
            object otherOwner = new object();
            controller.SetExternalControlLocked(otherOwner, true);
            Assert.That(route.BeginSettlementDefense(), Is.True);
            var core = encounter.Cores[0];
            if (stage > 0)
            {
                InvokeHotfix(encounter, "FinishOpening");
            }
            if (stage > 1)
            {
                SetPrivateField(core.Health, "hitShakeAmplitude", 0f);
                SetPrivateField(core.Health, "deathShakeAmplitude", 0f);
                core.Health.TakeDamage(1000f);
            }
            if (stage > 2)
            {
                core.Interact(player.gameObject);
            }
            encounter.CancelEncounter();
            encounter.CancelEncounter();
            InvokeHotfix(encounter, "OnDisable");
            Assert.That(encounter.IsActive, Is.False);
            Assert.That(encounter.Cores.Count, Is.Zero);
            Assert.That(GetHotfixField(encounter, "reveal"), Is.Null);
            Assert.That(GetHotfixField(encounter, "centralPulse"), Is.Null);
            Assert.That(navigation.activeSelf, Is.EqualTo(navigationVisible));
            Assert.That(((GameObject)GetHotfixField(encounter, "managementCanvas")).activeSelf, Is.True);
            Assert.That(controller.ControlEnabled, Is.False, "An unrelated input owner is not released.");
            controller.SetExternalControlLocked(otherOwner, false);
            Assert.That(controller.ControlEnabled, Is.True);
            Assert.That(player.GetComponent<PlayerDash>().enabled, Is.True);
            Assert.That(player.GetComponent<PlayerWeaponController>().enabled, Is.True);
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Settlement));
            Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(savedBefore),
                "Cancellation cannot consume parts, inflate curse or mutate permanent progression.");
        }
        finally
        {
            encounter?.CancelEncounter();
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(SaveManager), oldSave);
            SetHotfixSingleton(typeof(GameStateManager), oldState);
            SetHotfixSingleton(typeof(AudioManager), oldAudio);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CampaignPass3_NullDispatcherDeathDelegatesFinalVictoryExactlyOnce(bool withEnding)
    {
        using var tweenScope = new ManualRecoveryTweenScope();
        var oldRun = RunManager.Instance;
        var oldProgress = PermanentProgress.Instance;
        var oldSave = SaveManager.Instance;
        var oldState = GameStateManager.Instance;
        var oldAudio = AudioManager.Instance;
        var oldMusic = GameAudioLoopController.Instance;
        FieldInfo pauseField = typeof(GameplayPauseManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldPause = pauseField.GetValue(null);
        float timeScale = Time.timeScale;
        float fixedStep = Time.fixedDeltaTime;
        RunManager manager = null;
        try
        {
            manager = CreateInactiveHotfixObject("Final victory run").AddComponent<RunManager>();
            var progress = CreateInactiveHotfixObject("Final victory progress").AddComponent<PermanentProgress>();
            var audio = CreateInactiveHotfixObject("Final victory audio").AddComponent<AudioManager>();
            var music = CreateInactiveHotfixObject("Final victory music").AddComponent<GameAudioLoopController>();
            var pause = CreateInactiveHotfixObject("Final victory pause").AddComponent<GameplayPauseManager>();
            SetHotfixSingleton(typeof(RunManager), manager);
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetHotfixSingleton(typeof(SaveManager), null);
            SetHotfixSingleton(typeof(GameStateManager), null);
            SetHotfixSingleton(typeof(AudioManager), audio);
            SetHotfixSingleton(typeof(GameAudioLoopController), music);
            SetPrivateField(music, "runEndMusicFadeRequested", true);
            pauseField.SetValue(null, pause);
            progress.TryStartDamagedAccessKeyQuest();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            progress.TryRestoreDamagedAccessKey();
            progress.TryActivateRouteCore();
            progress.MarkSettlementDefenseCleared();
            manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.FinalNetwork);
            var boss = CreateInactiveHotfixObject("Final death authority").AddComponent<BossDummyController>();
            boss.ConfigureCampaignDefinition(AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(
                "Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Final.asset"));
            int completions = 0;
            manager.RunEnded += result =>
            {
                Assert.That(result.endReason, Is.EqualTo(RunEndReason.FinalVictory));
                completions++;
            };
            if (withEnding)
            {
                var ending = boss.gameObject.AddComponent<NullDispatcherEndingPresentation>();
                SetPrivateField(boss, "finalEndingPresentation", ending);
                InvokeHotfix(boss, "CacheResolvedDeathContext");
                var handoff = (System.Collections.IEnumerator)InvokeHotfix(boss, "CompleteBossDeathAfterPresentation");
                Assert.That(handoff.MoveNext(), Is.True);
                var presentation = handoff.Current as System.Collections.IEnumerator;
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.MoveNext(), Is.True);
                tweenScope.Own((Tween)GetHotfixField(ending, "visual"));
                Assert.That(completions, Is.Zero);
                Assert.That(manager.HasActiveRun, Is.True);
                Assert.That(GetHotfixField(boss, "postDeathFlowStarted"), Is.EqualTo(false));
                SetPrivateField(ending, "awaitingDialogue", true);
                InvokeHotfix(ending, "RecordDialogueCompletion", false);
                Assert.That(ending.Completed, Is.False);
                Assert.That(completions, Is.Zero, "Interruption cannot finalize victory.");
                InvokeHotfix(ending, "RecordDialogueCompletion", true);
                InvokeHotfix(ending, "BeginStabilization");
                tweenScope.Own((Tween)GetHotfixField(ending, "visual"));
                InvokeHotfix(ending, "CompletePresentation");
                ((System.IDisposable)presentation).Dispose();
                Assert.That(handoff.MoveNext(), Is.True);
                Assert.That(((System.Collections.IEnumerator)handoff.Current).MoveNext(), Is.False);
                Assert.That(handoff.MoveNext(), Is.False);
            }
            var routine = (System.Collections.IEnumerator)InvokeHotfix(boss, "CompleteBossDeathRoutine");
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(manager.HasActiveRun, Is.False);
            Assert.That(progress.FinalBossDefeated, Is.True);
            Assert.That(GetHotfixField(boss, "rewardExitCoordinatorStarted"), Is.EqualTo(false));
            routine = (System.Collections.IEnumerator)InvokeHotfix(boss, "CompleteBossDeathRoutine");
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(completions, Is.EqualTo(1));
        }
        finally
        {
            manager?.ReleaseRunEndingPresentationOwnership();
            SetHotfixSingleton(typeof(RunManager), oldRun);
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(SaveManager), oldSave);
            SetHotfixSingleton(typeof(GameStateManager), oldState);
            SetHotfixSingleton(typeof(AudioManager), oldAudio);
            SetHotfixSingleton(typeof(GameAudioLoopController), oldMusic);
            pauseField.SetValue(null, oldPause);
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = fixedStep;
        }
    }

    [Test]
    public void CampaignPass3_AuthoredDeckAndFinalEncounterHaveCanonicalBindings()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = AuthoredRuntimeFixture.Open("Settlement");
        try
        {
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            var route = AuthoredRuntimeFixture.Single<SettlementRouteCoreController>(scene);
            var encounter = AuthoredRuntimeFixture.Single<SettlementDefenseEncounterController>(scene);
            Assert.That(route.name, Is.EqualTo("RouteCoreRoot"));
            Assert.That(route.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(GetHotfixField(route, "autoCompleteDefenseForPrototype"), Is.EqualTo(false));
            Assert.That(GetHotfixField(route, "settlementHud"), Is.Not.Null);
            Assert.That(GetHotfixField(route, "settlementController"), Is.Not.Null);
            Assert.That(route.DefenseRequested.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(route.DefenseRequested.GetPersistentTarget(0), Is.SameAs(encounter));
            Assert.That(route.DefenseRequested.GetPersistentMethodName(0), Is.EqualTo("BeginEncounter"));
            Assert.That(GetHotfixField(encounter, "routeCore"), Is.SameAs(route));
            Assert.That(InvokeHotfix(encounter, "HasValidConfiguration"), Is.EqualTo(true));
            Assert.That(((GameObject)GetHotfixField(encounter, "facilityNavigation")).name, Is.EqualTo("ReturnToFacilities"));
            var configuredCores = (SettlementDefenseEncounterController.ComponentCore[])GetHotfixField(encounter, "componentCores");
            Assert.That(configuredCores.Length, Is.EqualTo(3));
            Assert.That(configuredCores[0].part, Is.EqualTo(BossStoryPart.SectorStabilizer));
            Assert.That(configuredCores[1].part, Is.EqualTo(BossStoryPart.PhaseNavigationLens));
            Assert.That(configuredCores[2].part, Is.EqualTo(BossStoryPart.MatterCompressor));
            var corePrefab = (SettlementDefenseCorruptedCore)GetHotfixField(encounter, "corePrefab");
            Assert.That(AssetDatabase.GetAssetPath(corePrefab), Does.EndWith("PF_SettlementCorruptedCore.prefab"));
            Assert.That(GetHotfixField(corePrefab.Health, "releaseOnDeath"), Is.EqualTo(false));
            Assert.That(GetHotfixField(corePrefab, "interactionCollider"), Is.Not.SameAs(GetHotfixField(corePrefab, "combatCollider")));

            Assert.That(((GameObject)GetHotfixField(encounter, "deckRoot")).activeSelf, Is.False);
            var player = (PlayerHealth)GetHotfixField(encounter, "player");
            Assert.That(player.GetComponent<PlayerInteractor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerDash>().enabled, Is.True);
            Assert.That(player.GetComponent<PlayerDeathSequenceController>().enabled, Is.False,
                "Defense retries do not finish or create an expedition run.");
            foreach (string stage in new[] { "missingPartsRoot", "readyToAssembleRoot", "assembledRoot", "activatedRoot" })
            {
                Assert.That(GetHotfixField(route, stage), Is.Not.Null);
            }
        }
        finally
        {
            AuthoredRuntimeFixture.Close(scene);
        }
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(activeScene));

        var core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab").GetComponent<CoreObject>();
        var final = (GameObject)GetHotfixField(core, "finalBossPrefab");
        Assert.That(AssetDatabase.GetAssetPath(final), Is.EqualTo("Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab"));
        var boss = final.GetComponent<BossDummyController>();
        var definition = (BossCampaignDefinition)GetHotfixField(boss, "campaignDefinition");
        Assert.That(definition.BossId, Is.EqualTo(CampaignBossId.NullDispatcher));
        Assert.That(AssetDatabase.GetAssetPath(definition), Does.EndWith("BossCampaign_Final.asset"));
        Assert.That(GetHotfixField(boss, "completeFinalBossAsVictory"), Is.EqualTo(true));
        Assert.That(final.GetComponent<EnemyHealth>(), Is.Not.Null);
        Assert.That(final.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(final.GetComponent<BossPatternController>(), Is.Null);
        Assert.That(final.GetComponent<PhaseGatekeeperBossController>(), Is.Null);
        Assert.That(final.GetComponent<FinalBossSettlementSupportPhase>().enabled, Is.False);
        Assert.That(final.GetComponent<BossDeathPresentation>(), Is.Not.Null);
    }

    [TestCase(GameState.Settlement, false)]
    [TestCase(GameState.SettlementDefense, true)]
    [TestCase(GameState.FinalBossBattle, true)]
    [TestCase(GameState.BossBattle, true)]
    public void CampaignPass3_CombatStatesPermitWeaponGameplay(GameState value, bool expected)
    {
        var state = CreateInactiveHotfixObject("Combat classification").AddComponent<GameStateManager>();
        SetPrivateField(state, "currentState", value);
        Assert.That(state.IsGameplayState(), Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CampaignVerticalSlice_PortalDoesNotReplaceEmptyOrMissingActiveEquipment(bool missingDefinition)
    {
        var bootstrap = CreateInactiveHotfixObject("Portal bootstrap").AddComponent<ExpeditionBootstrap>();
        var equipment = CreateInactiveHotfixObject("Portal active slot").AddComponent<PlayerReinforcementController>();
        SetPrivateField(bootstrap, "reinforcementController", equipment);
        SetPrivateField(bootstrap, "equipUnlockedPermanentReinforcementOnRunStart", false);
        var defaultDefinition = Track(ScriptableObject.CreateInstance<ReinforcementDefinition>());
        SetPrivateField(defaultDefinition, "equipmentId", "rf_emergency_return_anchor");
        var run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.DeepZone1);
        if (missingDefinition)
        {
            run.SetEquippedReinforcement("unavailable_carried_active", 2);
        }
        run.CapturePlayerVitals(8f, 2f);
        string before = JsonUtility.ToJson(run);
        InvokeHotfix(bootstrap, "RestoreReinforcement", run, new[] { defaultDefinition });
        Assert.That(equipment.HasEquipment, Is.False);
        Assert.That(JsonUtility.ToJson(run), Is.EqualTo(before));
    }

    [TestCase(ExpeditionDepth.Normal, ExpeditionDepth.DeepZone1)]
    [TestCase(ExpeditionDepth.DeepZone1, ExpeditionDepth.DeepZone2)]
    public void CampaignVerticalSlice_ManagerTransitionPreservesActiveContext(
        ExpeditionDepth from, ExpeditionDepth to)
    {
        var manager = CreateInactiveHotfixObject("Portal context owner").AddComponent<RunManager>();
        var context = new RunContext(WeaponTreeType.Sniper, from, "portal_ship");
        context.Wallet.Add(CurrencyType.Credits, 81);
        context.Wallet.Add(CurrencyType.TuningChips, 4);
        context.AddTrait("portal_passive");
        context.SetEquippedReinforcement("portal_active", 2);
        context.CapturePlayerVitals(13f, 3f);
        context.MarkBossDefeated(CampaignProgressionCatalog.GetBossId(from));
        SetPrivateField(manager, "currentRun", context);
        int starts = 0;
        int transitions = 0;
        manager.RunStarted += _ => starts++;
        manager.RegionChanged += (previous, next) =>
        {
            Assert.That(previous, Is.EqualTo(from));
            Assert.That(next, Is.EqualTo(to));
            Assert.That(manager.CurrentRun, Is.SameAs(context));
            transitions++;
        };
        Assert.That(InvokeHotfix(manager, "PrepareNextRegionState", to), Is.EqualTo(from));
        Assert.That(manager.CurrentRun, Is.SameAs(context));
        Assert.That(context.IsActive, Is.True);
        Assert.That(context.BossDefeated, Is.False);
        Assert.That(context.SelectedTraitIds, Does.Contain("portal_passive"));
        Assert.That(context.EquippedReinforcementCharges, Is.EqualTo(2));
        Assert.That(context.Wallet.Credits, Is.EqualTo(81));
        Assert.That(context.Wallet.TuningChips, Is.EqualTo(4));
        Assert.That(context.TryGetPlayerVitalCarryover(out float hp, out float armor), Is.True);
        Assert.That(hp, Is.EqualTo(13f));
        Assert.That(armor, Is.EqualTo(3f));
        Assert.That(starts, Is.Zero);
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void CampaignVerticalSlice_PhaseCleanupCannotClearDeathCameraOwnership()
    {
        GungeonStyleCamera2D camera = CreateInactiveHotfixObject("Region 3 camera")
            .AddComponent<GungeonStyleCamera2D>();
        PhaseGatekeeperBossController phase = CreateInactiveHotfixObject("Phase cleanup owner")
            .AddComponent<PhaseGatekeeperBossController>();
        PlayerController2D player = CreateInactiveHotfixObject("Phase player")
            .AddComponent<PlayerController2D>();
        object deathOwner = new object();
        SetPrivateField(phase, "gameplayCamera", camera);
        SetPrivateField(phase, "playerController", player);
        SetPrivateField(phase, "introLocksHeld", true);
        player.SetExternalControlLocked(phase, true);
        camera.SetCinematicInputOffsetLocked(phase, true);
        camera.SetCinematicInputOffsetLocked(deathOwner, true);
        // Simulate the next presentation acquiring focus before a late Died subscriber.
        SetPrivateField(camera, "cinematicFocusOwner", deathOwner);
        SetPrivateField(camera, "cinematicFocusActive", true);
        Assert.That(camera.TrySetOwnedCinematicFocus(phase, Vector3.one), Is.False);

        InvokeHotfix(phase, "ReleaseIntroLocks", true);
        InvokeHotfix(phase, "ReleaseIntroLocks", true);
        Assert.That(player.ControlEnabled, Is.True);
        Assert.That(camera.IsCinematicFocusOwnedBy(deathOwner), Is.True);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.True);
        camera.SetCinematicInputOffsetLocked(deathOwner, false);
        Assert.That(camera.IsCinematicInputOffsetLocked, Is.False);
    }

    [Test]
    public void CampaignVerticalSlice_MissingDefenseDoesNotEnterStateOrGrantClear()
    {
        PermanentProgress oldProgress = PermanentProgress.Instance;
        GameStateManager oldState = GameStateManager.Instance;
        try
        {
            GameObject services = CreateInactiveHotfixObject("Defense gate services");
            var progress = services.AddComponent<PermanentProgress>();
            var state = services.AddComponent<GameStateManager>();
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetHotfixSingleton(typeof(GameStateManager), state);
            progress.TryStartDamagedAccessKeyQuest();
            progress.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            progress.RegisterCampaignBossDefeat(CampaignBossId.SalvageDevourer);
            progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
            progress.TryRestoreDamagedAccessKey();
            progress.TryActivateRouteCore();
            SetPrivateField(state, "currentState", GameState.Settlement);
            var route = CreateRouteCoreFixture("Unwired Route Core");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Settlement defense is not configured"));
            Assert.That(route.BeginSettlementDefense(), Is.False);
            route.CompleteSettlementDefense();
            Assert.That(progress.SettlementDefenseCleared, Is.False);
            Assert.That(progress.CanLaunchFinalExpedition, Is.False);
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Settlement));

            // Interrupted ownership cleanup must not overwrite a newer scene state.
            SetPrivateField(route, "defenseRequestInProgress", true);
            SetPrivateField(state, "currentState", GameState.ExpeditionLoading);
            InvokeHotfix(route, "ReleaseDefenseRequest");
            InvokeHotfix(route, "ReleaseDefenseRequest");
            Assert.That(state.CurrentState, Is.EqualTo(GameState.ExpeditionLoading));
        }
        finally
        {
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(GameStateManager), oldState);
        }
    }

    [Test]
    public void CampaignVerticalSlice_FinalRunRequiresAuthorityAndEveryPrerequisite()
    {
        PermanentProgress oldProgress = PermanentProgress.Instance;
        GameStateManager oldState = GameStateManager.Instance;
        try
        {
            var manager = CreateInactiveHotfixObject("Final run guard").AddComponent<RunManager>();
            var original = new RunContext(WeaponTreeType.Shotgun, ExpeditionDepth.Normal);
            SetPrivateField(manager, "currentRun", original);
            SetHotfixSingleton(typeof(GameStateManager), null);
            SetHotfixSingleton(typeof(PermanentProgress), null);
            manager.StartNewRun(WeaponTreeType.Sniper, ExpeditionDepth.FinalNetwork);
            Assert.That(manager.CurrentRun, Is.SameAs(original));
            Assert.That(manager.StartFinalExpeditionAndLoad(), Is.False);

            var progress = CreateInactiveHotfixObject("Final run progress").AddComponent<PermanentProgress>();
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            progress.TryStartDamagedAccessKeyQuest();
            foreach (CampaignBossId boss in new[] { CampaignBossId.SectorAdministrator,
                         CampaignBossId.SalvageDevourer, CampaignBossId.PhaseGatekeeper })
            {
                progress.RegisterCampaignBossDefeat(boss);
                manager.StartNewRun(WeaponTreeType.Sniper, ExpeditionDepth.FinalNetwork);
                Assert.That(manager.CurrentRun, Is.SameAs(original));
            }
            progress.TryRestoreDamagedAccessKey();
            manager.StartNewRun(WeaponTreeType.Sniper, ExpeditionDepth.FinalNetwork);
            Assert.That(manager.CurrentRun, Is.SameAs(original));
            progress.TryActivateRouteCore();
            manager.StartNewRun(WeaponTreeType.Sniper, ExpeditionDepth.FinalNetwork);
            Assert.That(manager.CurrentRun, Is.SameAs(original));
            progress.MarkSettlementDefenseCleared();
            manager.StartNewRun(WeaponTreeType.Sniper, ExpeditionDepth.FinalNetwork);
            Assert.That(manager.CurrentRun.ExpeditionDepth, Is.EqualTo(ExpeditionDepth.FinalNetwork));
            Assert.That(manager.CurrentRun.CurrentBossId, Is.EqualTo(CampaignBossId.NullDispatcher));
            // Exercise run creation only; this test never loads a scene.
        }
        finally
        {
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
            SetHotfixSingleton(typeof(GameStateManager), oldState);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CampaignVerticalSlice_StorySlotsReflectEveryRecoveryStage(int count)
    {
        var progress = CreateInactiveHotfixObject("Story stages").AddComponent<PermanentProgress>();
        CampaignBossId[] bosses = { CampaignBossId.SectorAdministrator,
            CampaignBossId.SalvageDevourer, CampaignBossId.PhaseGatekeeper };
        for (int i = 0; i < count; i++)
        {
            progress.RegisterCampaignBossDefeat(bosses[i]);
        }
        for (int i = 0; i < bosses.Length; i++)
        {
            var slot = new PlayerBuildStatusPanelUI.StoryRecoverySlot();
            var group = CreateInactiveHotfixObject("Story slot stage").AddComponent<CanvasGroup>();
            SetPrivateField(slot, "part", CampaignProgressionCatalog.GetStoryPart(bosses[i]));
            SetPrivateField(slot, "canvasGroup", group);
            slot.Refresh(progress);
            Assert.That(group.alpha, Is.EqualTo(i < count ? 1f : 0.55f));
        }
        Assert.That(progress.AcquiredBossStoryPartCount, Is.EqualTo(count));
    }

    [TestCase(ExpeditionDepth.DeepZone1, false)]
    [TestCase(ExpeditionDepth.DeepZone1, true)]
    [TestCase(ExpeditionDepth.DeepZone2, false)]
    [TestCase(ExpeditionDepth.DeepZone2, true)]
    public void CampaignVerticalSlice_AuthoredBossSelectionAndMapMode(
        ExpeditionDepth depth, bool repeat)
    {
        RunManager oldRun = RunManager.Instance;
        PermanentProgress oldProgress = PermanentProgress.Instance;
        try
        {
            var manager = CreateInactiveHotfixObject("Encounter run").AddComponent<RunManager>();
            var progress = CreateInactiveHotfixObject("Encounter progress").AddComponent<PermanentProgress>();
            SetHotfixSingleton(typeof(RunManager), manager);
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            SetPrivateField(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, depth));
            if (repeat)
            {
                progress.RegisterCampaignBossDefeat(CampaignProgressionCatalog.GetBossId(depth));
            }
            var map = CreateInactiveHotfixObject("Encounter map mode").AddComponent<ExpeditionMapGenerator>();
            SetPrivateField(map, "useRepeatBossForGeneratedMap",
                CampaignProgressionCatalog.ShouldUseRepeatBoss(depth, progress));
            Assert.That(InvokeHotfix(map, "IsRegion2SalvageDevourerMap"),
                Is.EqualTo(depth == ExpeditionDepth.DeepZone1 && !repeat));
            Assert.That(InvokeHotfix(map, "IsRegion3PhaseGatekeeperMap"),
                Is.EqualTo(depth == ExpeditionDepth.DeepZone2 && !repeat));

            var core = CreateInactiveHotfixObject("Encounter Core").AddComponent<CoreObject>();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab")
                .GetComponent<CoreObject>();
            foreach (string field in new[] { "bossPrefab", "region2BossPrefab",
                         "region1RepeatBossPrefab", "region1RepeatBossDefinition" })
            {
                SetPrivateField(core, field, GetHotfixField(template, field));
            }
            if (repeat || depth == ExpeditionDepth.DeepZone1)
            {
                object encounter = InvokeHotfix(core, "ResolveBossEncounter");
                var prefab = (GameObject)encounter.GetType().GetProperty("Prefab").GetValue(encounter);
                Assert.That(prefab, Is.SameAs(GetHotfixField(template,
                    repeat ? "region1RepeatBossPrefab" : "region2BossPrefab")));
                Assert.That(encounter.GetType().GetProperty("IsRepeatReplacement").GetValue(encounter),
                    Is.EqualTo(repeat));
                if (!repeat)
                {
                    Assert.That(prefab.GetComponent<FrigateTriadBossController>(), Is.Not.Null);
                }
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab");
                Assert.That(prefab.GetComponent<PhaseGatekeeperBossController>(), Is.Not.Null);
            }
        }
        finally
        {
            SetHotfixSingleton(typeof(RunManager), oldRun);
            SetHotfixSingleton(typeof(PermanentProgress), oldProgress);
        }
    }

    [Test]
    public void BossRecovery_PlayResolvesAuthoredRendererBeforeLifecycleCallbacks()
    {
        using var tweenScope = new ManualRecoveryTweenScope();
        // Component logic fixture: supply the authored visual explicitly, but
        // deliberately provide no cached renderer and invoke no lifecycle methods.
        GameObject root = CreateInactiveHotfixObject("Uninitialized recovery API");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        Sprite generic = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Space Kit/Core/core5.png");
        renderer.sprite = generic;
        var presentation = root.AddComponent<Region2BossCoreRewardPresentation>();
        Assert.That(GetHotfixField(presentation, "coreRenderer"), Is.Null);
        var routine = presentation.PlayRoutine(Vector3.zero);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            tweenScope.Own((Sequence)GetHotfixField(presentation, "recoveryTween"));
            Assert.That(GetHotfixField(presentation, "coreRenderer"), Is.SameAs(renderer));
            Assert.That(renderer.sprite, Is.SameAs(generic));
            presentation.CleanupPresentation();
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(presentation.IsCompleted, Is.False);
            Assert.That(presentation.IsPlaying, Is.False);
        }
        finally
        {
            presentation.CleanupPresentation();
        }
    }

    [Test]
    public void StoryRecovery_NullDedicatedSpriteStillStartsAuthoredWorldFlight()
    {
        using var tweenScope = new ManualRecoveryTweenScope();
        GameObject player = CreateInactiveHotfixObject("Recovery does not lock controls");
        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        PlayerDash dash = player.AddComponent<PlayerDash>();
        PlayerWeaponController weapon = player.AddComponent<PlayerWeaponController>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/Enemy/PF_Region2BossCoreRewardPresentation.prefab");
        Region2BossCoreRewardPresentation template = prefab.GetComponent<Region2BossCoreRewardPresentation>();
        Sprite generic = prefab.GetComponentInChildren<SpriteRenderer>(true).sprite;
        Assert.That(generic, Is.Not.Null);
        BossRewardExitCoordinator coordinator = CreateInactiveHotfixObject("Null sprite recovery owner")
            .AddComponent<BossRewardExitCoordinator>();
        SetPrivateField(coordinator, "recoveredPart", BossStoryPart.SectorStabilizer);
        var outer = (System.Collections.IEnumerator)InvokeHotfix(coordinator, "PresentRecoveryThenRewards",
            template, true, false, 3);
        Assert.That(outer.MoveNext(), Is.True);
        var visual = (Region2BossCoreRewardPresentation)GetHotfixField(coordinator, "recoveryPresentation");
        Track(visual.gameObject);
        try
        {
            Assert.That(visual.GetComponentInChildren<SpriteRenderer>(true).sprite, Is.SameAs(generic));
            var flight = (System.Collections.IEnumerator)outer.Current;
            Assert.That(flight.MoveNext(), Is.True, "None must still start the world recovery tween.");
            tweenScope.Own((Sequence)GetHotfixField(visual, "recoveryTween"));
            Assert.That(visual.IsPlaying, Is.True);
            Assert.That(controller.ControlEnabled, Is.True);
            Assert.That(controller.MovementLocked, Is.False);
            Assert.That(dash.enabled, Is.True);
            Assert.That(weapon.ExternalInputLocked, Is.False);
            visual.CleanupPresentation();
            visual.CleanupPresentation();
            Assert.That(visual.IsPlaying, Is.False);
            Assert.That(GetHotfixField(visual, "recoveryTween"), Is.Null);
            Assert.That(controller.ControlEnabled, Is.True);
            Assert.That(controller.MovementLocked, Is.False);
            Assert.That(dash.enabled, Is.True);
            Assert.That(weapon.ExternalInputLocked, Is.False);
        }
        finally
        {
            visual.CleanupPresentation();
            SetPrivateField(coordinator, "recoveryPresentation", null);
            (outer as System.IDisposable)?.Dispose();
        }
    }

    [Test]
    public void StoryRecovery_DedicatedSpriteOverridesGeneric_WithoutChangingDefinition()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/Enemy/PF_Region2BossCoreRewardPresentation.prefab");
        GameObject instance = Track(Object.Instantiate(prefab));
        Region2BossCoreRewardPresentation visual = instance.GetComponent<Region2BossCoreRewardPresentation>();
        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
        Sprite generic = renderer.sprite;
        BossCampaignDefinition definition = Track(ScriptableObject.CreateInstance<BossCampaignDefinition>());
        visual.SetStoryPartSprite(definition.StoryPartSprite);
        Assert.That(renderer.sprite, Is.SameAs(generic));
        Assert.That(definition.StoryPartSprite, Is.Null);
        Sprite dedicated = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Space Kit/Core/core1.png");
        Assert.That(dedicated, Is.Not.Null);
        Assert.That(dedicated, Is.Not.SameAs(generic));
        SetPrivateField(definition, "storyPartSprite", dedicated);
        visual.SetStoryPartSprite(definition.StoryPartSprite);
        Assert.That(renderer.sprite, Is.SameAs(dedicated));
        visual.CleanupPresentation();
        visual.CleanupPresentation();
        Assert.That(definition.StoryPartSprite, Is.SameAs(dedicated), "Cleanup cannot mutate the definition.");
        SetPrivateField(definition, "storyPartSprite", null);
        visual.SetStoryPartSprite(null);
        Assert.That(renderer.sprite, Is.SameAs(generic));
        Assert.That(definition.StoryPartSprite, Is.Null);
    }

    [Test]
    public void StoryRecovery_AuthoredInventoryHasExactlyThreeReadOnlySlotsAndStableIconSpace()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab");
        PlayerBuildStatusPanelUI panel = prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        var serialized = new SerializedObject(panel);
        SerializedProperty slots = serialized.FindProperty("storyRecoverySlots");
        Assert.That(slots.arraySize, Is.EqualTo(3));
        TMP_Text title = (TMP_Text)serialized.FindProperty("storyRecoveryTitle").objectReferenceValue;
        Assert.That(title, Is.Not.Null);
        Transform section = title.transform.parent;
        Assert.That(section.name, Is.EqualTo("StoryRecoverySection"));
        Assert.That(section.parent.name, Is.EqualTo("InventoryRoot"));
        Assert.That(section.GetComponentsInChildren<Selectable>(true), Is.Empty);
        Assert.That(section.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
        for (int i = 0; i < 3; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            BossStoryPart part = (BossStoryPart)slot.FindPropertyRelative("part").intValue;
            Assert.That(part, Is.EqualTo((BossStoryPart)(i + 1)));
            var definition = (BossCampaignDefinition)slot.FindPropertyRelative("definition").objectReferenceValue;
            Assert.That(definition.StoryPart, Is.EqualTo(part));
            var root = (RectTransform)slot.FindPropertyRelative("root").objectReferenceValue;
            Assert.That(root.parent, Is.SameAs(section));
            var icon = (Image)slot.FindPropertyRelative("iconImage").objectReferenceValue;
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.rectTransform.rect.width, Is.GreaterThan(0));
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(slot.FindPropertyRelative("nameText").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("statusText").objectReferenceValue, Is.Not.Null);
            Assert.That(slot.FindPropertyRelative("canvasGroup").objectReferenceValue, Is.Not.Null);
        }
        RectTransform inventory = (RectTransform)section.parent;
        Assert.That(inventory.sizeDelta.y, Is.LessThanOrEqualTo(270f));
        RectTransform row = (RectTransform)section;
        RectTransform content = (RectTransform)inventory.Find("Content");
        Assert.That(row.anchoredPosition.y - row.sizeDelta.y * 0.5f,
            Is.GreaterThan(content.anchoredPosition.y + content.sizeDelta.y * 0.5f));
    }

    [TestCase(BossStoryPart.SectorStabilizer, CampaignBossId.SectorAdministrator)]
    [TestCase(BossStoryPart.MatterCompressor, CampaignBossId.SalvageDevourer)]
    [TestCase(BossStoryPart.PhaseNavigationLens, CampaignBossId.PhaseGatekeeper)]
    public void StoryRecovery_ProgressEventRefreshesReadOnlySlot_NoCargoOrDuplicateState(
        BossStoryPart part, CampaignBossId boss)
    {
        PermanentProgress previous = PermanentProgress.Instance;
        PlayerBuildStatusPanelUI panel = CreateInactiveHotfixObject("Story inventory observer")
            .AddComponent<PlayerBuildStatusPanelUI>();
        try
        {
            PermanentProgress progress = CreateInactiveHotfixObject("Story inventory authority")
                .AddComponent<PermanentProgress>();
            SetHotfixSingleton(typeof(PermanentProgress), progress);
            var slot = new PlayerBuildStatusPanelUI.StoryRecoverySlot();
            GameObject slotObject = Track(new GameObject("Read only story slot", typeof(RectTransform)));
            slotObject.SetActive(false);
            RectTransform rect = (RectTransform)slotObject.transform;
            rect.sizeDelta = new Vector2(108f, 26f);
            CanvasGroup group = slotObject.AddComponent<CanvasGroup>();
            Image icon = slotObject.AddComponent<Image>();
            GameObject textObject = Track(new GameObject("Story status", typeof(RectTransform)));
            textObject.SetActive(false);
            TMP_Text status = textObject.AddComponent<TextMeshProUGUI>();
            BossCampaignDefinition definition = Track(ScriptableObject.CreateInstance<BossCampaignDefinition>());
            SetPrivateField(slot, "part", part);
            SetPrivateField(slot, "definition", definition);
            SetPrivateField(slot, "root", rect);
            SetPrivateField(slot, "canvasGroup", group);
            SetPrivateField(slot, "iconImage", icon);
            SetPrivateField(slot, "statusText", status);
            SetPrivateField(panel, "storyRecoverySlots", new[] { slot });
            panel.RefreshStoryRecovery();
            Assert.That(group.alpha, Is.EqualTo(0.55f));
            Assert.That(icon.enabled, Is.False);
            string unacquired = status.text;
            InvokeHotfix(panel, "BindRuntimeEvents");
            var run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
            int cargo = run.CurrentCargoLoad;
            Assert.That(progress.RegisterCampaignBossDefeat(boss), Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f), "PermanentProgress.Changed refreshes the view.");
            Assert.That(status.text, Is.Not.EqualTo(unacquired));
            Assert.That(progress.RegisterCampaignBossDefeat(boss), Is.False);
            Assert.That(progress.AcquiredBossStoryParts.Count, Is.EqualTo(1));
            Assert.That(run.CurrentCargoLoad, Is.EqualTo(cargo));
            Assert.That(icon.enabled, Is.False);
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(108f, 26f)));
            Assert.That(GetHotfixField(slot, "feedbackTween"), Is.Null, "Grant alone must not pulse before world recovery.");
            panel.PresentStoryPartAcquired(part);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(GetHotfixField(slot, "feedbackTween"), Is.Null, "Closed inventory is never opened or pulsed.");
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Space Kit/Core/core1.png");
            Assert.That(iconSprite, Is.Not.Null);
            SetPrivateField(definition, "storyPartSprite", iconSprite);
            panel.RefreshStoryRecovery();
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.SameAs(iconSprite));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(108f, 26f)));

            InvokeHotfix(panel, "UnbindRuntimeEvents");
            group.alpha = 0.3f;
            progress.RegisterCampaignBossDefeat(boss == CampaignBossId.SectorAdministrator
                ? CampaignBossId.SalvageDevourer : CampaignBossId.SectorAdministrator);
            Assert.That(group.alpha, Is.EqualTo(0.3f), "Closed lifecycle unbinds the progress event.");
            panel.RefreshStoryRecovery();
            Assert.That(group.alpha, Is.EqualTo(1f), "Reopening refresh reads authoritative progress.");
        }
        finally
        {
            InvokeHotfix(panel, "UnbindRuntimeEvents");
            SetHotfixSingleton(typeof(PermanentProgress), previous);
        }
    }

    [Test]
    public void StoryRecovery_SlotPulseCleanupRestoresAuthoredScale()
    {
        using var tweenScope = new ManualRecoveryTweenScope();
        var slot = new PlayerBuildStatusPanelUI.StoryRecoverySlot();
        GameObject root = Track(new GameObject("Story slot pulse", typeof(RectTransform)));
        root.transform.localScale = Vector3.one;
        SetPrivateField(slot, "root", (RectTransform)root.transform);
        slot.Pulse();
        Sequence tween = (Sequence)GetHotfixField(slot, "feedbackTween");
        tweenScope.Own(tween);
        Assert.That(tween, Is.Not.Null);
        tween.Pause();
        tween.Goto(0.12f, false);
        Assert.That(root.transform.localScale.x, Is.EqualTo(1.08f).Within(0.001f));
        slot.StopFeedback();
        slot.StopFeedback();
        Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(GetHotfixField(slot, "feedbackTween"), Is.Null);
    }

    private static object InvokeHotfix(object target, string method, params object[] args)
    {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, args);
    }

    private static object GetHotfixField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetHotfixSingleton(System.Type type, object value)
    {
        type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }

    [Test]
    public void CampaignSpine_ExistingPrefabBindingsSupportSharedRepeatAndRecovery()
    {
        GameObject core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Object/Core.prefab");
        var coreFields = new SerializedObject(core.GetComponent<CoreObject>());
        GameObject raider = (GameObject)coreFields.FindProperty("region1RepeatBossPrefab").objectReferenceValue;
        BossCampaignDefinition repeat = (BossCampaignDefinition)coreFields
            .FindProperty("region1RepeatBossDefinition").objectReferenceValue;
        Assert.That(raider, Is.Not.Null);
        Assert.That(raider.GetComponent<PirateCommanderBossController>(), Is.Not.Null);
        Assert.That(repeat, Is.Not.Null);
        Assert.That(repeat.GrantStoryPartOnFirstDefeat, Is.False);
        Assert.That(repeat.StoryPart, Is.EqualTo(BossStoryPart.None));
        Assert.That(coreFields.FindProperty("returnBeaconPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(coreFields.FindProperty("wormholePortalPrefab").objectReferenceValue, Is.Not.Null);
        for (int i = 0; i < 3; i++)
        {
            ExpeditionDepth depth = (ExpeditionDepth)i;
            Assert.That(CampaignBossRewardService.ResolveBossId(repeat, depth),
                Is.EqualTo(CampaignProgressionCatalog.GetBossId(depth)));
        }

        string[] paths = { "Assets/03_Prefabs/Enemy/Boss.prefab",
            "Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab",
            "Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab" };
        Object sharedRecovery = null;
        foreach (string path in paths)
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var fields = new SerializedObject(boss.GetComponent<BossDummyController>());
            Object recovery = fields.FindProperty("region2CoreRewardPresentationPrefab").objectReferenceValue;
            Assert.That(recovery, Is.Not.Null, path);
            if (sharedRecovery == null)
            {
                sharedRecovery = recovery;
            }
            Assert.That(recovery, Is.SameAs(sharedRecovery), path);
        }
    }

    [Test]
    public void BossExitPlacement_AllBlocked_UsesBoundedDeterministicFallback()
    {
        Bounds bounds = new Bounds(new Vector3(4f, -3f), new Vector3(16f, 12f, 1f));
        BossExitPlacement placement = BossExitSafePlacement.Resolve(
            bounds,
            bounds.center,
            null,
            0,
            _ => true,
            out bool usedFallback
        );

        Assert.That(usedFallback, Is.True);
        Assert.That(IsInside2D(bounds, placement.ReturnBeaconPosition), Is.True);
        Assert.That(IsInside2D(bounds, placement.NextRegionPosition), Is.True);
    }

    [Test]
    public void Region2Corridor_TerminalPoint_RemainsBottomWorldEndpoint()
    {
        ExpeditionMapGenerator.Region2BossCorridorData data =
            new ExpeditionMapGenerator.Region2BossCorridorData(
                7f,
                24f,
                -31f,
                8f,
                new Bounds(Vector3.zero, Vector3.one),
                new Bounds(Vector3.zero, Vector3.one)
            );

        Assert.That(data.TerminalPoint, Is.EqualTo(new Vector2(7f, -31f)));
    }

    [Test]
    public void FrigateHomingAimPoint_UsesDamageColliderInsteadOfPartPivot()
    {
        GameObject partObject = Track(new GameObject("Frigate part target"));
        partObject.transform.position = new Vector3(10f, 0f, 0f);
        BoxCollider2D collider = partObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(6f, 2f);
        FrigateBossPart part = partObject.AddComponent<FrigateBossPart>();
        SetPrivateField(part, "damageCollider", collider);
        Physics2D.SyncTransforms();

        Vector2 aimPoint = part.ResolveHomingAimPoint(Vector2.zero);

        Assert.That(aimPoint.x, Is.EqualTo(7f).Within(0.05f));
        Assert.That(aimPoint.y, Is.EqualTo(0f).Within(0.05f));
        Assert.That(aimPoint, Is.Not.EqualTo((Vector2)partObject.transform.position));
    }

    [Test]
    public void RadarSuccessfulScan_DoesNotDeactivateRadarMode()
    {
        RadarModeInputState state = new RadarModeInputState();
        Assert.That(state.TryToggle(true, out bool active), Is.True);
        Assert.That(active, Is.True);
        Assert.That(state.TryBeginInstantScan(true, 10f, 0.5f), Is.True);
        Assert.That(state.IsRadarActive, Is.True);
    }

    [Test]
    public void CombatActivity_RemainsPublishedWithoutOwningRadarVisibility()
    {
        GameObject playerObject = Track(new GameObject("Combat state"));
        PlayerCombatState combatState = playerObject.AddComponent<PlayerCombatState>();
        int activityCount = 0;
        combatState.CombatActivityRegistered += () => activityCount++;

        combatState.RegisterAttack();
        combatState.RegisterHit();

        Assert.That(activityCount, Is.EqualTo(2));

        MonoScript radarScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        Assert.That(radarScript, Is.Not.Null);
        Assert.That(
            radarScript.text,
            Does.Not.Contain("CombatActivityRegistered"),
            "Ordinary combat may remain observable without closing Radar.");
    }

    [Test]
    public void UnknownSignalRadarGuidance_UsesStableStateSpecificKeys()
    {
        Assert.That(
            Phase2CStoryDialogueIds.ResolveUnknownSignalRadarObjectiveTextKey(false),
            Is.EqualTo(Phase2CStoryDialogueIds.TutorialRadarObjectiveTextKey)
        );
        Assert.That(
            Phase2CStoryDialogueIds.ResolveUnknownSignalRadarObjectiveTextKey(true),
            Is.EqualTo(Phase2CStoryDialogueIds.TutorialRadarScanOnlyTextKey)
        );
    }

    [Test]
    public void SettlementActivationToggle_IsHiddenUntilUnlocked()
    {
        Assert.That(ShipTraitTreePanel.ShouldShowActivationToggle(false), Is.False);
        Assert.That(ShipTraitTreePanel.ShouldShowActivationToggle(true), Is.True);
    }

    [Test]
    public void FinalAudio_PrimaryMappingsAreUniqueDeterministicAndKeepStableNumbers()
    {
        var keys = new HashSet<string>(System.StringComparer.Ordinal);
        var values = new HashSet<string>(System.StringComparer.Ordinal);
        int previousOrder = 0;
        foreach (KeyValuePair<string, string> mapping in SoundEventIds.NumberedEventMappings)
        {
            Assert.That(keys.Add(mapping.Key), Is.True, mapping.Key);
            Assert.That(values.Add(mapping.Value), Is.True, mapping.Value);
            Assert.That(SoundEventIds.ToNumbered(mapping.Key), Is.EqualTo(mapping.Value));
            Assert.That(SoundEventIds.ToLegacy(mapping.Value), Is.EqualTo(mapping.Key));
            Assert.That(SoundEventIds.IsKnown(mapping.Key), Is.True);
            Assert.That(SoundEventIds.IsKnown(mapping.Value), Is.True);
            int order = SoundEventIds.GetOrder(mapping.Value);
            Assert.That(order, Is.GreaterThan(previousOrder));
            Assert.That(order < 100 || order > 104, Is.True, "Reserved gap was reused.");
            previousOrder = order;
        }

        // Golden sequence from the pre-pass local tree. Intentional retired 49
        // remains a gap; none of the other authored 42+ numbers may shift.
        string[] stableEvents =
        {
            "42_return_choice_open", "43_safe_return", "44_result_count_tick", "45_result_reward_total",
            "46_event_start", "47_event_complete", "48_ship_dash_start", "50_ship_hit",
            "51_ship_death_breakup", "52_machinegun_fire_loop", "53_shotgun_fire", "54_sniper_charge_start",
            "55_sniper_charge_cancel", "56_sniper_fire", "57_enemy_alert", "58_enemy_basic_fire",
            "59_enemy_shotgun_fire", "60_enemy_charger_aim_loop", "61_enemy_charger_fire", "62_enemy_elite_spread_fire",
            "63_enemy_elite_big_fire", "64_enemy_hit", "65_enemy_death", "66_object_container_hit",
            "67_object_container_break", "68_object_debris_hit", "69_object_debris_break", "70_object_shipwreck_break",
            "71_object_meteor_hit", "72_object_meteor_break", "73_core_interact_loop", "74_core_activate",
            "75_boss_spawn", "76_boss_laser_warning", "77_boss_laser_loop", "78_boss_spread_fire",
            "79_boss_charge_aim", "80_boss_charge_fire", "81_boss_phase2", "82_boss_death",
            "83_return_beacon_spawn", "84_wormhole_enter", "85_shop_shield_hit", "86_shop_shield_break",
            "87_shop_shotgun_fire", "88_security_drone_spawn", "89_radar_charge_start", "90_radar_charge_loop",
            "91_radar_charge_cancel", "92_sniper_charge_loop", "93_ship_move_puff", "94_amb_space_loop",
            "95_amb_settlement_loop", "96_music_combat_loop", "97_music_boss_loop", "98_music_settlement_loop",
            "99_music_shop_loop", "105_music_main_menu_loop", "106_music_tutorial_loop", "107_map_route_placed",
            "108_map_route_removed", "109_mission_received", "110_amb_tutorial_loop"
        };
        foreach (string id in stableEvents)
        {
            Assert.That(values, Does.Contain(id));
            string legacy = id.Substring(id.IndexOf('_') + 1);
            Assert.That(SoundEventIds.ToNumbered(legacy), Is.EqualTo(id));
        }
        Assert.That(SoundEventIds.PickupTuningChip, Is.EqualTo("40_pickup_tuning_chip"));
        Assert.That(SoundEventIds.ToNumbered("pickup_tuning_chip"), Is.EqualTo(SoundEventIds.PickupTuningChip));
        Assert.That(SoundEventIds.DialogueCommIncoming, Is.EqualTo("111_dialogue_comm_incoming"));
        Assert.That(SoundEventIds.ToNumbered("dialogue_comm_incoming"), Is.EqualTo(SoundEventIds.DialogueCommIncoming));
        Assert.That(SoundEventIds.MissionReceived, Is.Not.EqualTo(SoundEventIds.DialogueCommIncoming));
    }

    [Test]
    public void FinalAudio_RetiredSemanticsHaveNoPrimaryMappingOrRuntimeCaller()
    {
        foreach (string field in new[] { "PickupExperience", "LevelUp", "ShipDashEnd", "ShopItemSold", "ShopTransactionComplete" })
        {
            Assert.That(typeof(SoundEventIds).GetField(field), Is.Null, field);
            foreach (string file in System.IO.Directory.GetFiles(
                         Application.dataPath + "/02_Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Editor/"))
                {
                    continue;
                }
                Assert.That(System.IO.File.ReadAllText(file), Does.Not.Contain("SoundEventIds." + field), file);
            }
        }
        foreach (string id in new[] { "pickup_experience", "40_pickup_experience", "level_up", "41_level_up", "49_ship_dash_end", "19_shop_item_sold", "20_shop_transaction_complete" })
        {
            Assert.That(SoundEventIds.IsKnown(id), Is.False, id);
        }
    }

    [Test]
    public void FinalAudio_LibraryHasNoDuplicateMissingOrCompilationReferences()
    {
        AudioEventDatabase database = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(FinalAudioIntegrationInstaller.LibraryPath);
        Assert.That(database, Is.Not.Null);
        var entries = new SerializedObject(database).FindProperty("entries");
        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < entries.arraySize; i++)
        {
            string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("eventId").stringValue;
            Assert.That(ids.Add(id), Is.True, id);
            Assert.That(SoundEventIds.IsKnown(id), Is.True, id);
            Assert.That(database.TryGet(id, out AudioEventDefinition definition), Is.True);
            if (id == SoundEventIds.AmbSettlementLoop || id == SoundEventIds.DialogueCommHijack)
            {
                Assert.That(definition.Clips.Count, Is.Zero, "Awaiting user assignment for the Settlement loop / communication hijack.");
                continue;
            }
            Assert.That(definition.Clips.Count, Is.GreaterThan(0), id);
            foreach (AudioClip clip in definition.Clips)
            {
                Assert.That(clip, Is.Not.Null, id);
                Assert.That(clip.length, Is.GreaterThan(0f), id);
                string path = AssetDatabase.GetAssetPath(clip);
                Assert.That(path, Does.Not.Contain("모음집"));
                Assert.That(path, Does.Not.Contain("ImportedSource"));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty);
            }
        }
        Assert.That(ids.Count, Is.EqualTo(SoundEventIds.NumberedEventMappings.Count));
        foreach (var mapping in SoundEventIds.NumberedEventMappings)
        {
            Assert.That(ids, Does.Contain(mapping.Value));
        }
    }

    [TestCase(SoundEventIds.UiSettings, "UI/ui_confirm_01.wav")]
    [TestCase(SoundEventIds.ShopOpen, "Shop/shop_open.wav")]
    [TestCase(SoundEventIds.ShopBuyFail, "Shop/shop_buy_fail.wav")]
    [TestCase(SoundEventIds.ShopHostile, "Shop/shop_hostile_03.ogg")]
    [TestCase(SoundEventIds.PickupScrap, "Pickups/pickup_scrap_03.ogg")]
    [TestCase(SoundEventIds.PickupCore, "Pickups/pickup_core.wav")]
    [TestCase(SoundEventIds.PickupTuningChip, "UI/reinforcement_equip.wav")]
    [TestCase(SoundEventIds.ReturnBeaconSpawn, "Return/return_beacon_spawn_03.ogg")]
    [TestCase(SoundEventIds.WormholeEnter, "Return/wormhole_enter.ogg")]
    [TestCase(SoundEventIds.MapRoutePlaced, "Events/event_start.wav")]
    [TestCase(SoundEventIds.MapRouteRemoved, "UI/tab_status_close.wav")]
    [TestCase(SoundEventIds.MissionReceived, "Events/event_start.wav")]
    [TestCase(SoundEventIds.DialogueCommIncoming, "Radar/radar_scan_pulse.wav")]
    public void FinalAudio_SelectedEventsResolveTheReviewedClips(string id, string relativeClipPath)
    {
        var database = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(FinalAudioIntegrationInstaller.LibraryPath);
        Assert.That(database.TryGet(id, out AudioEventDefinition definition), Is.True);
        Assert.That(definition.Clips.Count, Is.EqualTo(1));
        Assert.That(definition.Clips[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/06_Audio/SFX/" + relativeClipPath)));
        Assert.That(definition.Loop, Is.False);
        Assert.That(definition.MaxSimultaneousVoices, Is.GreaterThan(0));
        Assert.That(definition.MinimumRetriggerInterval, Is.GreaterThan(0f));
        Assert.That(definition.PitchMin, Is.LessThanOrEqualTo(definition.PitchMax));
        Assert.That(definition.MaxDistance, Is.GreaterThan(definition.MinDistance));
        if (id == SoundEventIds.UiSettings)
        {
            Assert.That(database.TryGet(SoundEventIds.UiPanelOpen, out var panel), Is.True);
            Assert.That(definition.Clips[0], Is.SameAs(panel.Clips[0]));
        }
        if (id == SoundEventIds.DialogueCommIncoming)
        {
            Assert.That(definition.Clips[0].length, Is.InRange(0.15f, 0.5f));
            Assert.That(definition.SpatialMode, Is.EqualTo(AudioSpatialMode.Force2D));
            Assert.That(definition.Priority, Is.LessThanOrEqualTo(64));
            Assert.That(definition.MinimumRetriggerInterval, Is.GreaterThanOrEqualTo(1f));
        }
    }

    [Test]
    public void FinalAudio_IncomingCueUsesExplicitOptInAndBoundedStartSubscriptions()
    {
        var entry = CreateInactiveHotfixObject("Local dialogue audio default").AddComponent<DialogueStoryEntryPoint>();
        Assert.That(new SerializedObject(entry).FindProperty("playIncomingCommunicationCue").boolValue, Is.False);
        GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(FinalAudioIntegrationInstaller.RemoteBossPrefabPath);
        Assert.That(new SerializedObject(boss.GetComponent<DialogueStoryEntryPoint>())
            .FindProperty("playIncomingCommunicationCue").boolValue, Is.True);
        string story = ReadFinalAudioSource("Dialogue/DialogueStoryEntryPoint.cs");
        Assert.That(story, Does.Contain("if (playIncomingCommunicationCue &&"));
        Assert.That(story, Does.Contain("startingController.conversationStarted += HandleIncomingCommunicationStarted;"));
        Assert.That(story, Does.Contain("startingController.conversationStarted -= HandleIncomingCommunicationStarted;"));
        Assert.That(CountOccurrences(story, "AudioManager.Play(SoundEventIds.DialogueCommIncoming)"), Is.EqualTo(1));
        Assert.That(ReadFinalAudioSource("Dialogue/DialoguePixelCrushersBridge.cs"), Does.Not.Contain("AudioManager.Play"));
        Assert.That(ReadFinalAudioSource("RunRuntime/FieldNpcObjective.cs"), Does.Not.Contain("DialogueCommIncoming"));
        string tutorial = ReadFinalAudioSource("Tutorial/TutorialFlowController.cs");
        Assert.That(tutorial, Does.Contain("controller.conversationStarted += HandleStarted;"));
        Assert.That(tutorial, Does.Contain("controller.conversationStarted -= HandleStarted;"));
        Assert.That(tutorial, Does.Contain("StartRemoteConversation(trigger.conversation, playerRoot, null, true, trigger)"));
        Assert.That(tutorial, Does.Contain("? SoundEventIds.DialogueCommIncoming"));
        string settlement = System.IO.File.ReadAllText(Application.dataPath + "/01_Scenes/Settlement.unity");
        Assert.That(settlement, Does.Not.Contain("playIncomingCommunicationCue: 1"));
    }

    [Test]
    public void FinalAudio_IncomingRetriesUseExistingDuplicateGuardAndSurviveWorldSuppression()
    {
        var audio = CreateInactiveHotfixObject("Communication duplicate guard").AddComponent<AudioManager>();
        MethodInfo guard = typeof(AudioManager).GetMethod("IsBlockedByDuplicateGuard", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] args = { SoundEventIds.DialogueCommIncoming, 1f };
        Assert.That(guard.Invoke(audio, args), Is.False);
        Assert.That(guard.Invoke(audio, args), Is.True);
        var times = (Dictionary<string, float>)GetHotfixField(audio, "lastOneShotTimes");
        times[SoundEventIds.DialogueCommIncoming] = Time.unscaledTime - 1.1f;
        Assert.That(guard.Invoke(audio, args), Is.False);
        MethodInfo allowed = typeof(AudioManager).GetMethod("IsAllowedDuringWorldSfxSuppression", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(allowed.Invoke(null, new object[] { SoundEventIds.DialogueCommIncoming }), Is.True);
        Assert.That(allowed.Invoke(null, new object[] { SoundEventIds.MachineGunFire }), Is.False);
    }

    [Test]
    public void FinalAudio_RelayReusesEventStartOnlyAtAcceptedInteraction()
    {
        string tutorial = ReadFinalAudioSource("Tutorial/TutorialFlowController.cs");
        int start = tutorial.IndexOf("private bool TryStartRelayNarrative()", System.StringComparison.Ordinal);
        int end = tutorial.IndexOf("private void RefreshRelayNarrative()", start, System.StringComparison.Ordinal);
        string method = tutorial.Substring(start, end - start);
        Assert.That(method.IndexOf("AudioManager.PlayAt(SoundEventIds.EventStart", System.StringComparison.Ordinal),
            Is.GreaterThan(method.IndexOf("!relayNarrativeProgress.TryBeginAnalysis()", System.StringComparison.Ordinal)));
        Assert.That(CountOccurrences(tutorial, "AudioManager.PlayAt(SoundEventIds.EventStart"), Is.EqualTo(1));
        Assert.That(SoundEventIds.IsKnown("tutorial_relay_signal"), Is.False);
        Assert.That(tutorial, Does.Contain("AudioManager.Play(SoundEventIds.MissionReceived)"));
    }

    [Test]
    public void FinalAudio_ExitPickupAndShopAuthoritiesRemainDistinct()
    {
        Assert.That(ReadFinalAudioSource("Core/ReturnBeacon.cs"), Does.Contain("AudioManager.PlayAt(SoundEventIds.ReturnBeaconSpawn"));
        Assert.That(ReadFinalAudioSource("Core/WormholePortal.cs"), Does.Contain("AudioManager.PlayAt(SoundEventIds.WormholeEnter"));
        string pickups = ReadFinalAudioSource("Enemies/RewardPickup.cs");
        foreach (string name in new[] { "PickupCredit", "PickupScrap", "PickupCore", "PickupTuningChip", "PickupHeal" })
        {
            Assert.That(pickups, Does.Contain("AudioManager.PlayAt(SoundEventIds." + name));
        }
        Assert.That(CountOccurrences(pickups, "PlayPickupSound();"), Is.EqualTo(1));
        string shop = ReadFinalAudioSource("Shop/ShopTradeUI.cs");
        Assert.That(shop, Does.Contain("AudioManager.Play(SoundEventIds.ShopOpen)"));
        Assert.That(shop, Does.Contain("soundButton.SetClickSoundEnabled(false)"));
        AssertShopSuccessAudioRemainsTransactionGated(shop);
        Assert.That(ReadFinalAudioSource("Shop/ShopStructure.cs"), Does.Contain("AudioManager.PlayAt(SoundEventIds.ShopHostile"));
    }

    private static string ReadFinalAudioSource(string relativePath)
    {
        return System.IO.File.ReadAllText(Application.dataPath + "/02_Scripts/" + relativePath);
    }

    [Test]
    public void ShopPurchaseSuccessEvent_UsesExistingConfirmationClip()
    {
        AudioEventDatabase database = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(
            "Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset"
        );
        AudioClip expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/06_Audio/SFX/Shop/shop_buy_success.wav"
        );

        Assert.That(database, Is.Not.Null);
        Assert.That(expectedClip, Is.Not.Null);
        Assert.That(database.TryGet(SoundEventIds.ShopBuySuccess, out AudioEventDefinition definition), Is.True);
        Assert.That(definition.Clips, Is.Not.Null);
        Assert.That(definition.Clips.Count, Is.GreaterThan(0));

        bool containsExpectedClip = false;
        for (int i = 0; i < definition.Clips.Count; i++)
        {
            AudioClip assignedClip = definition.Clips[i];
            Assert.That(assignedClip == null, Is.False, $"Missing clip reference at index {i}.");
            containsExpectedClip |= ReferenceEquals(assignedClip, expectedClip);
        }

        Assert.That(
            containsExpectedClip,
            Is.True,
            "The success event must reference the intended shop_buy_success.wav asset."
        );

        MonoScript shopTradeScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Shop/ShopTradeUI.cs"
        );
        Assert.That(shopTradeScript, Is.Not.Null);
        AssertShopSuccessAudioRemainsTransactionGated(shopTradeScript.text);
    }

    private static string BuildExitPlacementDiagnostics(
        Bounds bounds,
        Vector2[] avoidPoints,
        BossExitPlacement first,
        BossExitPlacement second,
        bool firstFallback,
        bool secondFallback,
        ExitBlockerProbe blockerProbe)
    {
        float firstSeparation = Vector2.Distance(
            first.ReturnBeaconPosition,
            first.NextRegionPosition
        );
        Vector2 referencePoint = avoidPoints != null && avoidPoints.Length > 0
            ? avoidPoints[0]
            : Vector2.zero;

        return
            $"Bounds center={bounds.center:F3}, size={bounds.size:F3}; " +
            $"first beacon={first.ReturnBeaconPosition:F3}, portal={first.NextRegionPosition:F3}, " +
            $"fallback={firstFallback}; second beacon={second.ReturnBeaconPosition:F3}, " +
            $"portal={second.NextRegionPosition:F3}, fallback={secondFallback}; " +
            $"separation actual={firstSeparation:F3}, " +
            $"required={BossExitSafePlacement.MinimumExitSeparation:F3}; " +
            $"reference={referencePoint:F3}, beaconDistance=" +
            $"{Vector2.Distance(first.ReturnBeaconPosition, referencePoint):F3}, portalDistance=" +
            $"{Vector2.Distance(first.NextRegionPosition, referencePoint):F3}; " +
            $"beaconBlocked={blockerProbe.IsBlocked(first.ReturnBeaconPosition)}, " +
            $"portalBlocked={blockerProbe.IsBlocked(first.NextRegionPosition)}; " +
            $"blockingHits={blockerProbe.DescribeBlockingHits()}";
    }

    private static void AssertShopSuccessAudioRemainsTransactionGated(string source)
    {
        const string methodMarker = "private void OnClickBuy()";
        const string nextMethodMarker = "private bool TryBuyReinforcement()";
        const string eligibilityGuard = "if (!CanBuySelectedOption())";
        const string transactionGuard = "if (!success)";
        const string failureAudio = "AudioManager.Play(SoundEventIds.ShopBuyFail);";
        const string successAudio = "AudioManager.Play(SoundEventIds.ShopBuySuccess);";

        int methodStart = source.IndexOf(methodMarker, System.StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));

        int methodEnd = source.IndexOf(nextMethodMarker, methodStart, System.StringComparison.Ordinal);
        Assert.That(methodEnd, Is.GreaterThan(methodStart));

        string method = source.Substring(methodStart, methodEnd - methodStart);
        int eligibilityIndex = method.IndexOf(eligibilityGuard, System.StringComparison.Ordinal);
        Assert.That(eligibilityIndex, Is.GreaterThanOrEqualTo(0));

        int firstFailureIndex = method.IndexOf(failureAudio, eligibilityIndex, System.StringComparison.Ordinal);
        Assert.That(firstFailureIndex, Is.GreaterThan(eligibilityIndex));

        int transactionIndex = method.IndexOf(transactionGuard, System.StringComparison.Ordinal);
        Assert.That(transactionIndex, Is.GreaterThan(firstFailureIndex));

        int secondFailureIndex = method.IndexOf(failureAudio, firstFailureIndex + 1, System.StringComparison.Ordinal);
        Assert.That(secondFailureIndex, Is.GreaterThan(transactionIndex));

        int successIndex = method.IndexOf(successAudio, System.StringComparison.Ordinal);
        Assert.That(successIndex, Is.GreaterThan(secondFailureIndex));

        Assert.That(
            CountOccurrences(method, successAudio),
            Is.EqualTo(1),
            "The success sound must have one post-transaction call site."
        );
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class ExitBlockerProbe
    {
        private readonly Collider2D blocker;
        private readonly List<string> blockingHits = new List<string>();

        public ExitBlockerProbe(Collider2D blocker)
        {
            this.blocker = blocker;
        }

        public bool IsBlocked(Vector2 position)
        {
            if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector2 closestPoint = blocker.ClosestPoint(position);
            bool blocked = Vector2.Distance(position, closestPoint) <=
                           BossExitSafePlacement.BlockingCheckRadius;
            if (blocked)
            {
                blockingHits.Add(
                    $"candidate={position:F3}, object={blocker.name}, " +
                    $"layer={blocker.gameObject.layer}({LayerMask.LayerToName(blocker.gameObject.layer)}), " +
                    $"id={blocker.GetInstanceID()}"
                );
            }

            return blocked;
        }

        public string DescribeBlockingHits()
        {
            return blockingHits.Count > 0
                ? string.Join(" | ", blockingHits)
                : "<none>";
        }
    }

    private T Track<T>(T createdObject) where T : Object
    {
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static bool IsInside2D(Bounds bounds, Vector2 position)
    {
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
               position.y >= bounds.min.y && position.y <= bounds.max.y;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
