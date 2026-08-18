using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ExpeditionOperationType
{
    None,
    HighValueSalvage,
    SignalInvestigation,
    DefenseNetworkSabotage
}

public enum ExpeditionOperationState
{
    Inactive,
    Search,
    Identified,
    Completed,
    Failed,
    Cancelled
}

[DisallowMultipleComponent]
public sealed class ExpeditionOperationController : MonoBehaviour
{
    private readonly struct OperationCandidate
    {
        public OperationCandidate(
            ExpeditionOperationType type,
            MonoBehaviour source,
            RadarTarget radarTarget)
        {
            Type = type;
            Source = source;
            RadarTarget = radarTarget;
        }

        public ExpeditionOperationType Type { get; }
        public MonoBehaviour Source { get; }
        public RadarTarget RadarTarget { get; }
    }

    [Header("References")]
    [SerializeField] private ExpeditionMapGenerator mapGenerator;
    [SerializeField] private MapDiscoveryController mapDiscoveryController;
    [SerializeField] private PlayerRadarScanner radarScanner;
    [SerializeField] private ExpeditionHUD expeditionHUD;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float preferredMinimumTargetDistance = 24f;
    [SerializeField, Min(0f)] private float fallbackMinimumTargetDistance = 20f;

    [Header("Approximate Search Region")]
    [SerializeField] private Vector2 searchRadiusRange = new Vector2(8f, 12f);
    [SerializeField] private Vector2 searchOffsetRange = new Vector2(2f, 5f);

    [Header("Presentation")]
    [SerializeField] private Color searchAccentColor = new Color(1f, 0.72f, 0.22f, 1f);
    [SerializeField] private Color identifiedAccentColor = new Color(0.42f, 0.9f, 1f, 1f);
    [SerializeField, Min(0f)] private float resolvedHudDuration = 2.5f;
    [SerializeField, Min(0)] private int sectorOneStabilizedAlloyBonus = 1;

    private readonly List<OperationCandidate> candidateBuffer = new List<OperationCandidate>(16);

    private RunManager boundRunManager;
    private PlayerRadarScanner boundRadarScanner;
    private Coroutine resolvedHudRoutine;
    private MonoBehaviour targetSource;
    private HarvestObjectHealth targetHarvestObject;
    private ExpeditionEventObject targetEventObject;
    private FieldBaseController targetFieldBase;
    private RadarTarget targetRadar;
    private ExpeditionOperationType operationType;
    private ExpeditionOperationState state;
    private Vector2 searchRegionCenter;
    private float searchRegionRadius;
    private string directionLabel;

    public ExpeditionOperationType OperationType => operationType;
    public ExpeditionOperationState State => state;
    public bool HasOperation => state != ExpeditionOperationState.Inactive &&
                                state != ExpeditionOperationState.Cancelled;
    public bool ShowSearchRegion => state == ExpeditionOperationState.Search;
    public Vector2 SearchRegionCenter => searchRegionCenter;
    public float SearchRegionRadius => Mathf.Max(0f, searchRegionRadius);
    public RadarTarget TargetRadar => targetRadar;
    public string PresentationTitle => ResolveOperationTitle();
    public string PresentationState => state switch
    {
        ExpeditionOperationState.Search => "탐색 중",
        ExpeditionOperationState.Identified => "목표 확인",
        ExpeditionOperationState.Completed => "완료",
        ExpeditionOperationState.Failed => "종료",
        _ => "대기"
    };
    public string PresentationLocation => string.IsNullOrWhiteSpace(directionLabel)
        ? "미확인"
        : $"{directionLabel} 구획";
    public string PresentationObjective => state switch
    {
        ExpeditionOperationState.Search => operationType == ExpeditionOperationType.DefenseNetworkSabotage
            ? "적 방어 시설을 탐색하십시오."
            : "탐색 구역에서 레이더 스캔",
        ExpeditionOperationState.Identified => ResolveIdentifiedDetail(),
        ExpeditionOperationState.Completed => ResolveCompletedDetail(),
        ExpeditionOperationState.Failed => "작전 종료",
        _ => string.Empty
    };

    public event Action OperationChanged;
    public event Action<ExpeditionOperationType> OperationCompleted;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        RadarTarget.RegistryChanged += HandleRadarRegistryChanged;
        TryBindRadarScanner();
        TryBindRunManager();
    }

    private void Start()
    {
        ResolveReferences();
        TryBindRadarScanner();
        TryBindRunManager();
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
        RadarTarget.RegistryChanged -= HandleRadarRegistryChanged;
        StopResolvedHudRoutine();
        UnbindTarget();
        UnbindRadarScanner();
        UnbindRunManager();
        HideOperationHud();
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generatedMap)
    {
        ClearOperation();
        mapGenerator = generatedMap;

        if (generatedMap == null || !isActiveAndEnabled)
        {
            return;
        }

        ResolveReferences();
        TryBindRadarScanner();
        InitializeOperation(generatedMap);
    }

    private void InitializeOperation(ExpeditionMapGenerator generatedMap)
    {
        float preferredDistance = Mathf.Max(0f, preferredMinimumTargetDistance);
        CollectEligibleCandidates(generatedMap, preferredDistance);

        float fallbackDistance = Mathf.Clamp(
            fallbackMinimumTargetDistance,
            0f,
            preferredDistance
        );

        if (candidateBuffer.Count <= 0 && fallbackDistance < preferredDistance)
        {
            CollectEligibleCandidates(generatedMap, fallbackDistance);
        }

        if (candidateBuffer.Count <= 0)
        {
            Debug.LogWarning(
                "Expedition Operation was not created because no eligible generated High-Value Wreck, Event, or Field Base target was available.",
                this
            );
            ClearOperation();
            return;
        }

        OperationCandidate selected = candidateBuffer[UnityEngine.Random.Range(0, candidateBuffer.Count)];
        operationType = selected.Type;
        targetSource = selected.Source;
        targetRadar = selected.RadarTarget;
        targetHarvestObject = targetSource as HarvestObjectHealth;
        targetEventObject = targetSource as ExpeditionEventObject;
        targetFieldBase = targetSource as FieldBaseController;
        state = ExpeditionOperationState.Search;

        ConfigureSearchRegion(generatedMap.MapBounds, targetRadar.WorldPosition);
        directionLabel = ResolveDirectionLabel(generatedMap.StartPosition, searchRegionCenter);
        BindTarget();
        RefreshPresentation();
    }

    private void CollectEligibleCandidates(ExpeditionMapGenerator generatedMap, float minimumDistance)
    {
        candidateBuffer.Clear();
        if (generatedMap == null)
        {
            return;
        }

        float minimumDistanceSqr = minimumDistance * minimumDistance;
        Vector2 startPosition = generatedMap.StartPosition;
        Bounds bounds = generatedMap.MapBounds;
        IReadOnlyList<HarvestObjectHealth> harvestObjects = generatedMap.SpawnedHarvestObjects;

        for (int i = 0; i < harvestObjects.Count; i++)
        {
            HarvestObjectHealth harvestObject = harvestObjects[i];
            if (harvestObject == null || harvestObject.ObjectKind != HarvestObjectKind.HighValueWreck ||
                harvestObject.IsDead || !harvestObject.isActiveAndEnabled)
            {
                continue;
            }

            RadarTarget radarTarget = harvestObject.RadarTarget;
            if (!IsEligibleRadarTarget(radarTarget, startPosition, minimumDistanceSqr, bounds))
            {
                continue;
            }

            candidateBuffer.Add(new OperationCandidate(
                ExpeditionOperationType.HighValueSalvage,
                harvestObject,
                radarTarget
            ));
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

            RadarTarget radarTarget = eventObject.RadarTarget;
            if (!IsEligibleRadarTarget(radarTarget, startPosition, minimumDistanceSqr, bounds))
            {
                continue;
            }

            candidateBuffer.Add(new OperationCandidate(
                ExpeditionOperationType.SignalInvestigation,
                eventObject,
                radarTarget
            ));
        }

        IReadOnlyList<FieldBaseController> fieldBases = generatedMap.SpawnedFieldBases;
        for (int i = 0; i < fieldBases.Count; i++)
        {
            FieldBaseController fieldBase = fieldBases[i];
            if (fieldBase == null || !fieldBase.isActiveAndEnabled || fieldBase.IsObjectiveCompleted)
            {
                continue;
            }

            RadarTarget radarTarget = fieldBase.RadarTarget;
            if (!IsEligibleRadarTarget(radarTarget, startPosition, minimumDistanceSqr, bounds))
            {
                continue;
            }

            candidateBuffer.Add(new OperationCandidate(
                ExpeditionOperationType.DefenseNetworkSabotage,
                fieldBase,
                radarTarget
            ));
        }
    }

    private bool IsEligibleRadarTarget(
        RadarTarget radarTarget,
        Vector2 startPosition,
        float minimumDistanceSqr,
        Bounds mapBounds)
    {
        bool alreadyDiscovered = radarTarget != null &&
                                 (radarTarget.IsMapDiscovered ||
                                  mapDiscoveryController != null &&
                                  mapDiscoveryController.IsTargetDiscovered(radarTarget));

        if (radarTarget == null || !radarTarget.IsRadarVisible || !radarTarget.ShowOnMap ||
            alreadyDiscovered)
        {
            return false;
        }

        Vector2 targetPosition = radarTarget.WorldPosition;
        if ((targetPosition - startPosition).sqrMagnitude < minimumDistanceSqr)
        {
            return false;
        }

        return mapBounds.Contains(new Vector3(targetPosition.x, targetPosition.y, mapBounds.center.z));
    }

    private void ConfigureSearchRegion(Bounds mapBounds, Vector2 targetPosition)
    {
        float minRadius = Mathf.Max(0.5f, Mathf.Min(searchRadiusRange.x, searchRadiusRange.y));
        float maxRadius = Mathf.Max(minRadius, Mathf.Max(searchRadiusRange.x, searchRadiusRange.y));
        searchRegionRadius = UnityEngine.Random.Range(minRadius, maxRadius);

        float minOffset = Mathf.Max(0f, Mathf.Min(searchOffsetRange.x, searchOffsetRange.y));
        float maxOffset = Mathf.Max(minOffset, Mathf.Max(searchOffsetRange.x, searchOffsetRange.y));
        float offsetDistance = UnityEngine.Random.Range(minOffset, maxOffset);
        float offsetAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        Vector2 offset = new Vector2(Mathf.Cos(offsetAngle), Mathf.Sin(offsetAngle)) * offsetDistance;
        Vector2 center = targetPosition + offset;

        float minX = mapBounds.min.x + searchRegionRadius;
        float maxX = mapBounds.max.x - searchRegionRadius;
        float minY = mapBounds.min.y + searchRegionRadius;
        float maxY = mapBounds.max.y - searchRegionRadius;

        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : mapBounds.center.x;
        center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : mapBounds.center.y;
        searchRegionCenter = center;
    }

    private void HandleScanCompleted(
        Vector2 scanOrigin,
        float scanRadius,
        IReadOnlyList<RadarTarget> scannedTargets)
    {
        if (state != ExpeditionOperationState.Search || targetRadar == null || scannedTargets == null)
        {
            return;
        }

        for (int i = 0; i < scannedTargets.Count; i++)
        {
            if (scannedTargets[i] != targetRadar)
            {
                continue;
            }

            IdentifyOperationTarget();
            return;
        }
    }

    private void IdentifyOperationTarget()
    {
        if (state != ExpeditionOperationState.Search)
        {
            return;
        }

        state = ExpeditionOperationState.Identified;
        RefreshPresentation();
        expeditionHUD?.ShowCommunication(
            ShipCommunicationChannel.Radar,
            "작전 목표를 확인했습니다.",
            ShipCommunicationSeverity.Confirmation
        );
    }

    private void HandleHarvestObjectDied(HarvestObjectHealth harvestObject)
    {
        if (harvestObject == targetHarvestObject)
        {
            CompleteOperation();
        }
    }

    private void HandleEventResolved(ExpeditionEventObject eventObject, ExpeditionEventState resolvedState)
    {
        if (eventObject != targetEventObject)
        {
            return;
        }

        if (resolvedState == ExpeditionEventState.Completed)
        {
            CompleteOperation();
        }
        else if (resolvedState == ExpeditionEventState.Failed)
        {
            ResolveOperationFailure();
        }
    }

    private void HandleFieldBaseObjectiveCompleted(FieldBaseController fieldBase)
    {
        if (fieldBase == targetFieldBase)
        {
            CompleteOperation();
        }
    }

    private void HandleRadarRegistryChanged()
    {
        if (state != ExpeditionOperationState.Search && state != ExpeditionOperationState.Identified)
        {
            return;
        }

        if (IsTargetCompleted())
        {
            CompleteOperation();
            return;
        }

        if (IsTargetFailed())
        {
            ResolveOperationFailure();
            return;
        }

        if (targetSource == null || targetRadar == null || !targetSource.gameObject.activeInHierarchy)
        {
            CancelInvalidOperation();
        }
    }

    private bool IsTargetCompleted()
    {
        return operationType switch
        {
            ExpeditionOperationType.HighValueSalvage => targetHarvestObject != null && targetHarvestObject.IsDead,
            ExpeditionOperationType.SignalInvestigation => targetEventObject != null &&
                                                        targetEventObject.State == ExpeditionEventState.Completed,
            ExpeditionOperationType.DefenseNetworkSabotage => targetFieldBase != null &&
                                                               targetFieldBase.IsObjectiveCompleted,
            _ => false
        };
    }

    private bool IsTargetFailed()
    {
        return operationType == ExpeditionOperationType.SignalInvestigation &&
               targetEventObject != null &&
               targetEventObject.State == ExpeditionEventState.Failed;
    }

    private void CompleteOperation()
    {
        if (IsResolved())
        {
            return;
        }

        state = ExpeditionOperationState.Completed;
        AwardSectorOneOperationBonus();
        UnbindTarget();
        RefreshPresentation();
        if (operationType != ExpeditionOperationType.DefenseNetworkSabotage)
        {
            expeditionHUD?.ShowCommunication(
                ShipCommunicationChannel.System,
                "작전 목표 완료.",
                ShipCommunicationSeverity.Confirmation
            );
        }
        OperationCompleted?.Invoke(operationType);
        BeginResolvedHudHide();
    }

    private void AwardSectorOneOperationBonus()
    {
        if (sectorOneStabilizedAlloyBonus <= 0 ||
            RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.Normal)
        {
            return;
        }

        int acceptedAmount = RunManager.Instance.AddCurrencyRespectingCargo(
            CurrencyType.StabilizedAlloy,
            sectorOneStabilizedAlloyBonus
        );

        if (acceptedAmount >= sectorOneStabilizedAlloyBonus)
        {
            return;
        }

        string message = acceptedAmount > 0
            ? $"작전 보너스 안정화 합금을 {acceptedAmount}/{sectorOneStabilizedAlloyBonus}만 적재했습니다."
            : "적재 공간이 부족해 작전 보너스 안정화 합금을 적재하지 못했습니다.";
        expeditionHUD?.ShowCommunication(
            ShipCommunicationChannel.Cargo,
            message,
            ShipCommunicationSeverity.Warning
        );
    }

    private void ResolveOperationFailure()
    {
        if (IsResolved())
        {
            return;
        }

        state = ExpeditionOperationState.Failed;
        UnbindTarget();
        RefreshPresentation();
        expeditionHUD?.ShowCommunication(
            ShipCommunicationChannel.System,
            "작전 목표가 종료되었습니다.",
            ShipCommunicationSeverity.Warning
        );
        BeginResolvedHudHide();
    }

    private bool IsResolved()
    {
        return state == ExpeditionOperationState.Completed ||
               state == ExpeditionOperationState.Failed ||
               state == ExpeditionOperationState.Cancelled ||
               state == ExpeditionOperationState.Inactive;
    }

    private void CancelInvalidOperation()
    {
        Debug.LogWarning(
            $"Expedition Operation target became invalid before resolving. Type={operationType}",
            this
        );

        state = ExpeditionOperationState.Cancelled;
        UnbindTarget();
        HideOperationHud();
        OperationChanged?.Invoke();
    }

    private void RefreshPresentation()
    {
        OperationChanged?.Invoke();

        if (expeditionHUD == null)
        {
            return;
        }

        string detail = state switch
        {
            ExpeditionOperationState.Search => ResolveSearchDetail(),
            ExpeditionOperationState.Identified => ResolveIdentifiedDetail(),
            ExpeditionOperationState.Completed => ResolveCompletedDetail(),
            ExpeditionOperationState.Failed => "작전 종료",
            _ => string.Empty
        };

        Color accent = state == ExpeditionOperationState.Identified
            ? identifiedAccentColor
            : searchAccentColor;
        bool subdued = state == ExpeditionOperationState.Completed ||
                       state == ExpeditionOperationState.Failed;
        expeditionHUD.SetOperationDisplay(
            ResolveOperationTitle(),
            detail,
            accent,
            true,
            subdued
        );
    }

    private string ResolveOperationTitle()
    {
        return operationType switch
        {
            ExpeditionOperationType.HighValueSalvage => "고밀도 잔해 조사",
            ExpeditionOperationType.SignalInvestigation => "미확인 신호 조사",
            ExpeditionOperationType.DefenseNetworkSabotage => "방어망 제거",
            _ => "작전"
        };
    }

    private string ResolveSearchDetail()
    {
        return operationType == ExpeditionOperationType.DefenseNetworkSabotage
            ? $"{directionLabel} 구획 · 적 방어 시설 탐색"
            : $"{directionLabel} 구획 · 탐색 구역에서 레이더 스캔";
    }

    private string ResolveIdentifiedDetail()
    {
        return operationType switch
        {
            ExpeditionOperationType.HighValueSalvage => "고가치 잔해 위치 확인",
            ExpeditionOperationType.SignalInvestigation => "미확인 신호 위치 확인",
            ExpeditionOperationType.DefenseNetworkSabotage => "적 기지 확인 · 주 전력망 차단",
            _ => "작전 목표 위치 확인"
        };
    }

    private string ResolveCompletedDetail()
    {
        return operationType == ExpeditionOperationType.DefenseNetworkSabotage
            ? "방어망 제거 완료"
            : "작전 목표 완료";
    }

    private static string ResolveDirectionLabel(Vector2 origin, Vector2 destination)
    {
        Vector2 direction = destination - origin;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return "중앙";
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int directionIndex = (Mathf.RoundToInt(angle / 45f) + 8) % 8;
        return directionIndex switch
        {
            0 => "동쪽",
            1 => "북동쪽",
            2 => "북쪽",
            3 => "북서쪽",
            4 => "서쪽",
            5 => "남서쪽",
            6 => "남쪽",
            _ => "남동쪽"
        };
    }

    private void BindTarget()
    {
        if (targetHarvestObject != null)
        {
            targetHarvestObject.Died += HandleHarvestObjectDied;
        }

        if (targetEventObject != null)
        {
            targetEventObject.Resolved += HandleEventResolved;
        }

        if (targetFieldBase != null)
        {
            targetFieldBase.ObjectiveCompleted += HandleFieldBaseObjectiveCompleted;
        }
    }

    private void UnbindTarget()
    {
        if (targetHarvestObject != null)
        {
            targetHarvestObject.Died -= HandleHarvestObjectDied;
        }

        if (targetEventObject != null)
        {
            targetEventObject.Resolved -= HandleEventResolved;
        }

        if (targetFieldBase != null)
        {
            targetFieldBase.ObjectiveCompleted -= HandleFieldBaseObjectiveCompleted;
        }
    }

    private void ResolveReferences()
    {
        mapGenerator ??= GetComponent<ExpeditionMapGenerator>();
        mapGenerator ??= FindFirstObjectByType<ExpeditionMapGenerator>();
        mapDiscoveryController ??= MapDiscoveryController.Instance;
        radarScanner ??= FindFirstObjectByType<PlayerRadarScanner>();
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>();
    }

    private void TryBindRadarScanner()
    {
        PlayerRadarScanner resolved = radarScanner != null
            ? radarScanner
            : FindFirstObjectByType<PlayerRadarScanner>();

        if (boundRadarScanner == resolved)
        {
            return;
        }

        UnbindRadarScanner();
        radarScanner = resolved;
        boundRadarScanner = resolved;
        if (boundRadarScanner != null)
        {
            boundRadarScanner.ScanCompleted += HandleScanCompleted;
        }
    }

    private void UnbindRadarScanner()
    {
        if (boundRadarScanner != null)
        {
            boundRadarScanner.ScanCompleted -= HandleScanCompleted;
            boundRadarScanner = null;
        }
    }

    private void TryBindRunManager()
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
        ClearOperation();
    }

    private void BeginResolvedHudHide()
    {
        StopResolvedHudRoutine();
        if (resolvedHudDuration <= 0f)
        {
            HideOperationHud();
            return;
        }

        resolvedHudRoutine = StartCoroutine(HideResolvedHudRoutine());
    }

    private IEnumerator HideResolvedHudRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, resolvedHudDuration));
        resolvedHudRoutine = null;
        HideOperationHud();
    }

    private void ClearOperation()
    {
        StopResolvedHudRoutine();
        UnbindTarget();
        candidateBuffer.Clear();
        targetSource = null;
        targetHarvestObject = null;
        targetEventObject = null;
        targetFieldBase = null;
        targetRadar = null;
        operationType = ExpeditionOperationType.None;
        state = ExpeditionOperationState.Inactive;
        searchRegionCenter = default;
        searchRegionRadius = 0f;
        directionLabel = string.Empty;
        HideOperationHud();
        OperationChanged?.Invoke();
    }

    private void StopResolvedHudRoutine()
    {
        if (resolvedHudRoutine != null)
        {
            StopCoroutine(resolvedHudRoutine);
            resolvedHudRoutine = null;
        }
    }

    private void HideOperationHud()
    {
        expeditionHUD?.SetOperationDisplay(
            string.Empty,
            string.Empty,
            searchAccentColor,
            false,
            false
        );
    }
}
