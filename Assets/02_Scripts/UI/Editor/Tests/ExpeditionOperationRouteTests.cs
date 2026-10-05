using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ExpeditionOperationRouteTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string MissingEncounter = "Region-3 operation could not bind the generated Phase Gatekeeper encounter.";
    private readonly List<GameObject> objects = new List<GameObject>();
    private RunManager previousRun;
    private PermanentProgress previousProgress;
    private AudioManager previousAudio;
    private RunManager manager;
    private PermanentProgress progress;
    private UnityEngine.Random.State randomState;
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    private GameObject New(string name, bool active = true)
    {
        var go = new GameObject(name); go.SetActive(active); objects.Add(go); return go;
    }

    [SetUp] public void Setup()
    {
        previousRun = RunManager.Instance; previousProgress = PermanentProgress.Instance;
        previousAudio = AudioManager.Instance;
        randomState = UnityEngine.Random.state;
        manager = New("Operation route run", false).AddComponent<RunManager>();
        progress = New("Operation route progress", false).AddComponent<PermanentProgress>();
        typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
    }

    [TearDown] public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        if (previousAudio == null && AudioManager.Instance != null) Object.DestroyImmediate(AudioManager.Instance.gameObject);
        typeof(RunManager).GetProperty("Instance").SetValue(null, previousRun);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, previousProgress);
        UnityEngine.Random.state = randomState;
    }

    private ExpeditionMapGenerator Map(ExpeditionDepth depth, bool repeat)
    {
        Set(manager, "currentRun", new RunContext(WeaponTreeType.MachineGun, depth));
        var map = New("Resolved generated map", false).AddComponent<ExpeditionMapGenerator>();
        // Stand in for Generate's already-resolved route, independent of later progress changes.
        Set(map, "useRepeatBossForGeneratedMap", repeat);
        typeof(ExpeditionMapGenerator).GetProperty("MapBounds").SetValue(map, new Bounds(Vector3.zero, Vector3.one * 120));
        return map;
    }

    [TestCase(false)] [TestCase(true)]
    public void FirstVisitBindsUnknownRevealedSignalAndCompletesOnEncounterStart(bool progressChangesAfterGeneration)
    {
        var map = Map(ExpeditionDepth.DeepZone2, false);
        var boss = New("Generated Phase Gatekeeper").AddComponent<PhaseGatekeeperBossController>();
        var radar = boss.gameObject.AddComponent<RadarTarget>();
        Set(map, "currentRegion3BossEncounter", boss);
        if (progressChangesAfterGeneration) progress.RegisterCampaignBossDefeat(CampaignBossId.PhaseGatekeeper);
        var operation = New("First visit operation").AddComponent<ExpeditionOperationController>();
        Assert.That(map.UsesRegion3PhaseGatekeeperFoundation, Is.True);
        Call(operation, "InitializeOperation", map);

        Assert.That(operation.OperationType, Is.EqualTo(ExpeditionOperationType.SignalInvestigation));
        Assert.That(operation.State, Is.EqualTo(ExpeditionOperationState.Identified));
        Assert.That(operation.TargetRadar, Is.SameAs(radar));
        Assert.That(operation.ShowSearchRegion, Is.False);
        Assert.That(radar.MarkerType, Is.EqualTo(RadarMarkerType.Unknown));
        Assert.That(radar.IsTemporarilyRevealed, Is.True);
        Assert.That(radar.IsRadarVisible && radar.ShowOnMap, Is.True);
        Assert.That(radar.MarkerColor, Is.EqualTo(Get<Color>(operation, "region3InvestigationMarkerColor")));
        Assert.That(radar.MarkerScale, Is.EqualTo(Get<float>(operation, "region3InvestigationMarkerScale")));
        int completed = 0;
        operation.OperationCompleted += type => { Assert.That(type, Is.EqualTo(ExpeditionOperationType.SignalInvestigation)); completed++; };
        // Invoke the publisher's event to check the real operation subscription without starting combat in EditMode.
        Get<Action>(boss, "EncounterStarted").Invoke();
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(operation.State, Is.EqualTo(ExpeditionOperationState.Inactive));
        Assert.That(radar.IsTemporarilyRevealed, Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void FirstVisitMissingOrDisabledEncounterStillLogsActionableError(bool disabledEncounter)
    {
        var map = Map(ExpeditionDepth.DeepZone2, false);
        if (disabledEncounter)
            Set(map, "currentRegion3BossEncounter", New("Unavailable foundation", false).AddComponent<PhaseGatekeeperBossController>());
        var operation = New("Broken first visit").AddComponent<ExpeditionOperationController>();
        Assert.That(map.UsesRegion3PhaseGatekeeperFoundation, Is.True);
        LogAssert.Expect(LogType.Error, MissingEncounter);
        Assert.That(Call(operation, "TryInitializeRegion3BossInvestigation", map), Is.True);
        Assert.That(operation.HasOperation, Is.False);
    }

    public static IEnumerable<TestCaseData> OrdinaryRoutes()
    {
        foreach (ExpeditionDepth depth in new[] { ExpeditionDepth.Normal, ExpeditionDepth.DeepZone1, ExpeditionDepth.DeepZone2 })
        foreach (bool repeat in new[] { false, true })
        foreach (ExpeditionOperationType type in new[] { ExpeditionOperationType.HighValueSalvage,
                     ExpeditionOperationType.SignalInvestigation, ExpeditionOperationType.DefenseNetworkSabotage })
            if (depth != ExpeditionDepth.DeepZone2 || repeat) yield return new TestCaseData(depth, repeat, type);
    }

    [TestCaseSource(nameof(OrdinaryRoutes))]
    public void OrdinaryRoutesSelectActualCandidateWithoutRequiringPhaseGatekeeper(
        ExpeditionDepth depth, bool repeat, ExpeditionOperationType type)
    {
        var map = Map(depth, repeat);
        var target = New("Generated operation candidate"); target.transform.position = Vector3.right * 30;
        var radar = target.AddComponent<RadarTarget>(); radar.SetVisible(true); radar.SetShowOnMap(true);
        radar.SetMapDiscovered(false);
        if (type == ExpeditionOperationType.HighValueSalvage)
        {
            target.AddComponent<BoxCollider2D>();
            var wreck = target.AddComponent<HarvestObjectHealth>();
            Set(wreck, "objectKind", HarvestObjectKind.HighValueWreck); Set(wreck, "radarTarget", radar);
            ((List<HarvestObjectHealth>)map.SpawnedHarvestObjects).Add(wreck);
        }
        else if (type == ExpeditionOperationType.SignalInvestigation)
        {
            target.AddComponent<BoxCollider2D>();
            var eventObject = target.AddComponent<ExpeditionEventObject>(); Set(eventObject, "radarTarget", radar);
            ((List<ExpeditionEventObject>)map.SpawnedEventObjects).Add(eventObject);
        }
        else
        {
            var fieldBase = target.AddComponent<FieldBaseController>(); Set(fieldBase, "radarTarget", radar);
            ((List<FieldBaseController>)map.SpawnedFieldBases).Add(fieldBase);
        }
        var operation = New("Normal operation").AddComponent<ExpeditionOperationController>();
        Assert.That(map.UsesRegion3PhaseGatekeeperFoundation, Is.False);
        Assert.That(map.CurrentRegion3BossEncounter, Is.Null);
        Assert.That(Call(operation, "TryInitializeRegion3BossInvestigation", map), Is.False);
        Call(operation, "InitializeOperation", map);
        Assert.That(operation.State, Is.EqualTo(ExpeditionOperationState.Search));
        Assert.That(operation.OperationType, Is.EqualTo(type));
        Assert.That(operation.TargetRadar, Is.SameAs(radar));
        Assert.That(Get<bool>(operation, "region3BossInvestigation"), Is.False);
        Assert.That(radar.IsTemporarilyRevealed, Is.False);
    }
}
