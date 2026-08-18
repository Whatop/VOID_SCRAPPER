using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-6400)]
[DisallowMultipleComponent]
public sealed class CoreTrackingSignalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExpeditionMapGenerator mapGenerator;
    [SerializeField] private MapDiscoveryController mapDiscoveryController;
    [SerializeField] private ExpeditionHUD expeditionHUD;

    [Header("Core Tracking")]
    [SerializeField, Min(1)] private int requiredSignalCount = 2;
    [SerializeField] private bool logSourceValidation = true;

    private readonly HashSet<UnityEngine.Object> countedSources = new HashSet<UnityEngine.Object>();
    private readonly List<HarvestObjectHealth> subscribedWrecks = new List<HarvestObjectHealth>(8);
    private readonly List<ExpeditionEventObject> subscribedEvents = new List<ExpeditionEventObject>(8);
    private readonly List<FieldBaseController> subscribedFieldBases = new List<FieldBaseController>(4);

    private RunManager boundRunManager;
    private CoreObject coreObject;
    private int currentSignalCount;
    private bool coreRevealed;

    public int CurrentSignalCount => currentSignalCount;
    public int RequiredSignalCount => Mathf.Max(1, requiredSignalCount);
    public bool IsCoreRevealed => coreRevealed;

    public event Action<int, int> ProgressChanged;
    public event Action CoreRevealed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        BindRunManager();
    }

    private void Start()
    {
        ResolveReferences();
        BindRunManager();
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
        UnbindSources();
        UnbindRunManager();
        ResetRuntimeState();
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generatedMap)
    {
        UnbindSources();
        ResetRuntimeState();

        mapGenerator = generatedMap;
        ResolveReferences();

        if (generatedMap == null)
        {
            RaiseProgressChanged();
            return;
        }

        IReadOnlyList<CoreObject> cores = generatedMap.SpawnedCoreObjects;
        for (int i = 0; i < cores.Count; i++)
        {
            CoreObject candidate = cores[i];
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                coreObject = candidate;
                break;
            }
        }

        if (coreObject != null)
        {
            coreObject.SetTrackingLocked(true);
        }
        else
        {
            Debug.LogWarning("Core Tracking could not bind the generated CoreObject.", this);
        }

        int eligibleSourceCount = BindEligibleSources(generatedMap);
        if (eligibleSourceCount < RequiredSignalCount)
        {
            Debug.LogWarning(
                $"Core Tracking requires {RequiredSignalCount} signals, but this map generated only {eligibleSourceCount} eligible sources.",
                this
            );
        }
        else if (logSourceValidation)
        {
            Debug.Log(
                $"Core Tracking initialized at 0/{RequiredSignalCount} with {eligibleSourceCount} eligible sources.",
                this
            );
        }

        RaiseProgressChanged();
    }

    private int BindEligibleSources(ExpeditionMapGenerator generatedMap)
    {
        int eligibleCount = 0;
        IReadOnlyList<HarvestObjectHealth> harvestObjects = generatedMap.SpawnedHarvestObjects;
        for (int i = 0; i < harvestObjects.Count; i++)
        {
            HarvestObjectHealth harvestObject = harvestObjects[i];
            if (harvestObject == null || !harvestObject.isActiveAndEnabled ||
                harvestObject.ObjectKind != HarvestObjectKind.HighValueWreck || harvestObject.IsDead)
            {
                continue;
            }

            harvestObject.Died += HandleWreckDied;
            subscribedWrecks.Add(harvestObject);
            eligibleCount++;
        }

        IReadOnlyList<ExpeditionEventObject> eventObjects = generatedMap.SpawnedEventObjects;
        for (int i = 0; i < eventObjects.Count; i++)
        {
            ExpeditionEventObject eventObject = eventObjects[i];
            if (eventObject == null || !eventObject.isActiveAndEnabled ||
                eventObject.State == ExpeditionEventState.Completed ||
                eventObject.State == ExpeditionEventState.Failed)
            {
                continue;
            }

            eventObject.Resolved += HandleEventResolved;
            subscribedEvents.Add(eventObject);
            eligibleCount++;
        }

        IReadOnlyList<FieldBaseController> fieldBases = generatedMap.SpawnedFieldBases;
        for (int i = 0; i < fieldBases.Count; i++)
        {
            FieldBaseController fieldBase = fieldBases[i];
            if (fieldBase == null || !fieldBase.isActiveAndEnabled)
            {
                continue;
            }

            eligibleCount++;
            if (fieldBase.IsObjectiveCompleted)
            {
                TryAwardSignal(fieldBase);
                continue;
            }

            fieldBase.ObjectiveCompleted += HandleFieldBaseObjectiveCompleted;
            subscribedFieldBases.Add(fieldBase);
        }

        return eligibleCount;
    }

    private void HandleWreckDied(HarvestObjectHealth wreck)
    {
        if (wreck != null && wreck.ObjectKind == HarvestObjectKind.HighValueWreck)
        {
            TryAwardSignal(wreck);
        }
    }

    private void HandleEventResolved(ExpeditionEventObject eventObject, ExpeditionEventState state)
    {
        if (state == ExpeditionEventState.Completed)
        {
            TryAwardSignal(eventObject);
        }
    }

    private void HandleFieldBaseObjectiveCompleted(FieldBaseController fieldBase)
    {
        TryAwardSignal(fieldBase);
    }

    private bool TryAwardSignal(UnityEngine.Object source)
    {
        if (source == null || coreRevealed || currentSignalCount >= RequiredSignalCount || !countedSources.Add(source))
        {
            return false;
        }

        currentSignalCount = Mathf.Min(RequiredSignalCount, currentSignalCount + 1);
        if (currentSignalCount >= RequiredSignalCount)
        {
            RevealCore();
        }
        else
        {
            ResolveHud()?.ShowCommunication(
                ShipCommunicationChannel.Radar,
                $"코어 추적 신호 확보. {currentSignalCount}/{RequiredSignalCount}",
                ShipCommunicationSeverity.Confirmation
            );
        }

        RaiseProgressChanged();
        return true;
    }

    private void RevealCore()
    {
        if (coreRevealed)
        {
            return;
        }

        coreRevealed = true;
        if (coreObject != null)
        {
            coreObject.SetTrackingLocked(false);

            RadarTarget coreRadarTarget = coreObject.RadarTarget;
            if (coreRadarTarget != null)
            {
                coreRadarTarget.SetVisible(true);
                mapDiscoveryController?.DiscoverTarget(coreRadarTarget, false);
            }
        }

        ResolveHud()?.ShowCommunication(
            ShipCommunicationChannel.Radar,
            "코어 좌표를 확정했습니다.",
            ShipCommunicationSeverity.Confirmation
        );
        AudioManager.Play(SoundEventIds.UiUnlock);
        CoreRevealed?.Invoke();
    }

    private void UnbindSources()
    {
        for (int i = 0; i < subscribedWrecks.Count; i++)
        {
            HarvestObjectHealth wreck = subscribedWrecks[i];
            if (wreck != null)
            {
                wreck.Died -= HandleWreckDied;
            }
        }

        for (int i = 0; i < subscribedEvents.Count; i++)
        {
            ExpeditionEventObject eventObject = subscribedEvents[i];
            if (eventObject != null)
            {
                eventObject.Resolved -= HandleEventResolved;
            }
        }

        for (int i = 0; i < subscribedFieldBases.Count; i++)
        {
            FieldBaseController fieldBase = subscribedFieldBases[i];
            if (fieldBase != null)
            {
                fieldBase.ObjectiveCompleted -= HandleFieldBaseObjectiveCompleted;
            }
        }

        subscribedWrecks.Clear();
        subscribedEvents.Clear();
        subscribedFieldBases.Clear();
    }

    private void ResetRuntimeState()
    {
        if (coreObject != null)
        {
            coreObject.SetTrackingLocked(true);
        }

        countedSources.Clear();
        currentSignalCount = 0;
        coreRevealed = false;
        coreObject = null;
        RaiseProgressChanged();
    }

    private void ResolveReferences()
    {
        mapGenerator ??= GetComponent<ExpeditionMapGenerator>();
        mapGenerator ??= FindFirstObjectByType<ExpeditionMapGenerator>();
        mapDiscoveryController ??= MapDiscoveryController.Instance;
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>();
    }

    private ExpeditionHUD ResolveHud()
    {
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>();
        return expeditionHUD;
    }

    private void BindRunManager()
    {
        RunManager resolved = RunManager.Instance;
        if (boundRunManager == resolved)
        {
            return;
        }

        UnbindRunManager();
        boundRunManager = resolved;
        if (boundRunManager != null)
        {
            boundRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnbindRunManager()
    {
        if (boundRunManager != null)
        {
            boundRunManager.RunEnded -= HandleRunEnded;
            boundRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData result)
    {
        UnbindSources();
        ResetRuntimeState();
    }

    private void RaiseProgressChanged()
    {
        ProgressChanged?.Invoke(currentSignalCount, RequiredSignalCount);
    }
}
