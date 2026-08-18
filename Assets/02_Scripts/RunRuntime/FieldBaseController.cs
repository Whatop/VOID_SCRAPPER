using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FieldBaseController : MonoBehaviour
{
    [Serializable]
    private sealed class TurretPowerLinkEndpoints
    {
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;

        public Transform StartPoint => startPoint;
        public Transform EndPoint => endPoint;
    }

    private static readonly HashSet<FieldBaseController> ActiveBases = new HashSet<FieldBaseController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveBases()
    {
        ActiveBases.Clear();
    }

    [Header("Identity")]
    [SerializeField] private string baseDisplayName = "적 기지";
    [SerializeField] private RadarTarget radarTarget;

    [Header("NPC / Rescue")]
    [SerializeField] private FieldNpcObjective captiveNpc;
    [Tooltip("감금 장치가 연결되지 않은 구형 프리팹에서만 사용합니다. 감금 장치가 있으면 장치를 파괴해야 구조됩니다.")]
    [SerializeField] private bool autoReleaseNpcWhenSecurityOffline;
    [SerializeField] private string securityOfflineWarning = "기지 전력망이 차단되었다.";

    [Header("NPC Prison Machine")]
    [SerializeField] private HarvestObjectHealth npcPrisonMachine;
    [SerializeField] private bool protectNpcPrisonMachineWhilePowerOnline = true;
    [SerializeField] private GameObject[] npcProtectionOnlineVisuals;
    [SerializeField] private GameObject[] npcProtectionOfflineVisuals;
    [SerializeField] private string npcMachineVulnerableWarning = "NPC 감금 장치의 보호막이 꺼졌다. 장치를 파괴해 NPC를 구출해라.";
    [SerializeField] private string npcReleasedWarning = "NPC를 구출했다.";

    [Header("NPC Portal Cutscene")]
    [SerializeField] private bool autoCreatePortalAfterNpcRescue = true;
    [SerializeField] private bool lockPlayerDuringNpcPortalCutscene = true;
    [SerializeField] private Transform npcPortalInteractionPoint;
    [Min(0.05f)]
    [SerializeField] private float npcMoveToPortalDuration = 1.5f;
    [Min(0f)]
    [SerializeField] private float npcPortalInteractionDuration = 0.8f;
    [SerializeField] private string npcMovingWarning = "구조된 NPC가 포탈 제어기로 이동합니다.";

    [Header("Security")]
    [SerializeField] private FieldBaseSecurityNode[] securityNodes;
    [SerializeField] private FieldBaseLaserGate[] prisonGates;
    [SerializeField] private FieldBaseLaserGate[] storageGates;
    [SerializeField] private FieldBaseLaserGate[] portalCornerGates;
    [SerializeField] private FieldBaseLaserGate[] perimeterGates;
    [SerializeField] private bool openPrisonGatesWhenSecurityOffline = true;
    [SerializeField] private bool openStorageGatesWhenSecurityOffline = true;
    [SerializeField] private bool openPortalCornerGatesWhenSecurityOffline = true;
    [SerializeField] private bool openPerimeterGatesWhenSecurityOffline = true;
    [SerializeField] private bool openPortalCornerGatesWhenPortalReady;
    [Tooltip("기존 프리팹에 직접 배치한 포탑 호환용입니다. 새 방식은 아래 Turret Spawn Points를 사용합니다.")]
    [SerializeField] private BaseTurretController[] baseTurrets;

    [Header("Turret Spawning")]
    [FormerlySerializedAs("spawnTurretsOnAwake")]
    [SerializeField] private bool spawnTurretsOnStart = true;
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private Transform[] turretSpawnPoints;
    [SerializeField] private Transform turretRuntimeRoot;
    [SerializeField] private bool useTurretSpawnPointRotation = true;
    [SerializeField] private bool skipOccupiedTurretSpawnPoints = true;
    [Min(0.05f)]
    [SerializeField] private float occupiedTurretCheckRadius = 0.35f;
    [SerializeField] private bool logTurretSpawnResults;

    [Header("Turret Power Links")]
    [SerializeField] private bool createTurretPowerLinks = true;
    [SerializeField] private GameObject turretPowerLinkPrefab;
    [SerializeField] private bool autoCreatePowerLinkWhenPrefabMissing = true;
    [Tooltip("터렛 스폰 포인트와 같은 순서입니다. 각 요소의 Start/End를 벽 또는 터렛 플랫폼의 전력 단자에 배치하세요. End가 비어 있으면 터렛의 PowerLinkAnchor를 사용합니다.")]
    [SerializeField] private TurretPowerLinkEndpoints[] turretPowerLinkEndpoints;
    [Tooltip("Start Point가 비어 있을 때만 사용하는 공용 시작점입니다.")]
    [SerializeField] private Transform defaultTurretPowerSourcePoint;
    [Tooltip("구형 프리팹 호환용 Start Point 배열입니다. 새 프리팹은 Turret Power Link Endpoints를 사용하세요.")]
    [SerializeField] private Transform[] turretPowerSourcePoints;
    [SerializeField] private Transform turretPowerLinkRoot;

    [Header("Resource Storage")]
    [Tooltip("자원 구역 안에서 플레이어가 파괴할 상자들입니다. HarvestObjectHealth와 FieldBaseResourceChest가 붙은 오브젝트를 연결합니다.")]
    [SerializeField] private HarvestObjectHealth[] resourceChests;
    [SerializeField] private bool lockResourceChestsUntilSecurityOffline = true;
    [SerializeField] private Transform resourceDepositPoint;
    [Tooltip("Solid storage geometry stays physical; this trigger defines the non-blocking cargo handoff area.")]
    [SerializeField] private Collider2D resourceDepositTrigger;
    [Min(0.1f)]
    [SerializeField] private float resourceDepositArrivalDistance = 0.8f;
    [SerializeField] private string resourceStorageUnlockedWarning = "자원 보관 구역의 잠금이 해제되었다.";
    [SerializeField] private string allResourceChestsDestroyedWarning = "기지 보관 자원을 모두 회수했다.";

    [Header("Cargo Navigation")]
    [Tooltip("기지 바깥 -> 입구 -> 자원 보관 구역 순서의 경로입니다. 여러 입구가 있으면 여러 Route를 연결하세요.")]
    [SerializeField] private FieldBaseCargoRoute2D[] cargoRoutes;
    [SerializeField] private bool findCargoRoutesInChildrenWhenEmpty = true;

    [Header("Defender Assignment")]
    [SerializeField] private EnemyRoleController[] defenders;
    [SerializeField] private Transform[] defenderZoneAnchors;
    [SerializeField] private Collider2D baseBoundsCollider;
    [SerializeField] private bool spawnDefendersOnStart = true;
    [SerializeField] private Transform defenderRuntimeRoot;
    [SerializeField] private bool useDefenderAnchorRotation = true;

    [Header("Interior Defense Rule")]
    [Tooltip("Base turrets are exterior defenses and do not target the Player after they have entered this inset area.")]
    [SerializeField] private bool baseTurretsAreExterior = true;
    [Min(0f)]
    [SerializeField] private float protectedInteriorInset = 0.75f;

    [Header("Portal")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    [SerializeField] private Transform shopPortalDestinationPoint;
    [Tooltip("목적지를 직접 연결하지 않았을 때 가장 가까운 생존 상점의 Portal Arrival Point를 자동으로 사용합니다.")]
    [SerializeField] private bool autoFindNearestShopPortalDestination = true;
    [SerializeField] private Vector2 shopPortalDestinationOffset;
    [SerializeField] private bool skipCutsceneAfterFirstActivation = true;
    [SerializeField] private float portalChargeDuration = 1.2f;
    [SerializeField] private string portalLockedWarning = "먼저 NPC를 구출해라.";
    [SerializeField] private string portalChargingWarning = "포탈 좌표 동기화 중...";
    [SerializeField] private string portalReadyWarning = "상점 구역 포탈이 열렸다.";
    [SerializeField] private string portalMissingWarning = "포탈 프리팹, 생성 위치 또는 상점 도착 지점이 연결되지 않았습니다.";

    private GameObject activePortal;
    private bool securityResolved;
    private bool portalCutscenePlayed;
    private bool portalActivationRunning;
    private bool npcRescueResolved;
    private bool npcRescueCutsceneRunning;

    private readonly List<BaseTurretController> runtimeTurrets = new List<BaseTurretController>();
    private readonly List<FieldBasePowerLink2D> runtimeTurretPowerLinks = new List<FieldBasePowerLink2D>();
    private readonly List<EnemyRoleController> runtimeDefenders = new List<EnemyRoleController>();
    private bool turretSpawnPassCompleted;
    private bool defenderSpawnPassCompleted;

    public bool IsPortalSpawned => activePortal != null;
    public bool IsSecurityOffline => securityResolved || AllSecurityNodesDisabled();
    public bool IsObjectiveCompleted => securityResolved;
    public RadarTarget RadarTarget => radarTarget;
    public int ResourceChestCount => CountConfiguredResourceChests();
    public int RemainingResourceChestCount => CountRemainingResourceChests();
    public Transform ResourceDepositPoint => ResolveResourceDepositPoint();
    public float ResourceDepositArrivalDistance => Mathf.Max(0.1f, resourceDepositArrivalDistance);
    public bool HasResourceStorageSpace => HasAnyResourceStorageSpace();

    public event System.Action<FieldBaseController> ObjectiveCompleted;

    private void Reset()
    {
        radarTarget = GetComponent<RadarTarget>();
    }

    private void OnEnable()
    {
        ActiveBases.Add(this);
    }

    private void OnDisable()
    {
        ActiveBases.Remove(this);
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        SubscribeSecurityNodes(true);
        SubscribeNpcPrisonMachine(true);
        SubscribeResourceChests(true);

        RegisterManualTurrets();

        ApplyInitialGateState();
        if (captiveNpc != null)
        {
            captiveNpc.SetBaseCaptiveState(true);
        }

        if (AllSecurityNodesDisabled())
        {
            ResolveSecurityOffline();
        }
        else
        {
            ApplyPowerDependentDamageState(false);
            RefreshDefensePowerState();
        }
    }

    private void Start()
    {
        if (spawnTurretsOnStart)
        {
            SpawnConfiguredTurrets();
        }

        SpawnAndAssignDefenders();

        // Start에서 생성한 터렛에도 현재 기지 전력 상태를 즉시 반영한다.
        RefreshDefensePowerState();
    }

    private void OnDestroy()
    {
        ActiveBases.Remove(this);
        SubscribeSecurityNodes(false);
        SubscribeNpcPrisonMachine(false);
        SubscribeResourceChests(false);
    }

    public static FieldBaseController FindClosestResourceBase(Vector3 position)
    {
        FieldBaseController best = null;
        float bestSqrDistance = float.MaxValue;

        foreach (FieldBaseController candidate in ActiveBases)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.HasResourceStorageSpace)
            {
                continue;
            }

            Vector3 targetPosition = candidate.ResolveClosestCargoApproachPosition(position);
            float sqrDistance = (targetPosition - position).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = candidate;
            }
        }

        return best;
    }

    public bool TryGetClosestCargoRoute(Vector2 fromPosition, out FieldBaseCargoRoute2D route)
    {
        route = null;
        FieldBaseCargoRoute2D[] routes = ResolveCargoRoutes();

        if (routes == null || routes.Length == 0)
        {
            return false;
        }

        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < routes.Length; i++)
        {
            FieldBaseCargoRoute2D candidate = routes[i];

            if (candidate == null || !candidate.IsValid)
            {
                continue;
            }

            float sqrDistance = candidate.GetSqrDistanceToEntry(fromPosition);

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                route = candidate;
            }
        }

        return route != null;
    }

    public Vector3 ResolveClosestCargoApproachPosition(Vector3 fromPosition)
    {
        if (TryGetClosestCargoRoute(fromPosition, out FieldBaseCargoRoute2D route))
        {
            return route.GetEntryPosition();
        }

        Transform point = ResourceDepositPoint;
        return point != null ? point.position : transform.position;
    }

    public bool CanAcceptCargo(EnemyCargoHold cargoHold)
    {
        if (cargoHold == null || !cargoHold.HasCargo || resourceChests == null || resourceChests.Length == 0)
        {
            return false;
        }

        CurrencyType[] order =
        {
            CurrencyType.CoreShards,
            CurrencyType.StabilizedAlloy,
            CurrencyType.ScrapParts,
            CurrencyType.Credits,
            CurrencyType.Experience
        };

        for (int typeIndex = 0; typeIndex < order.Length; typeIndex++)
        {
            CurrencyType type = order[typeIndex];
            if (cargoHold.GetAmount(type) <= 0)
            {
                continue;
            }

            for (int chestIndex = 0; chestIndex < resourceChests.Length; chestIndex++)
            {
                FieldBaseResourceChest storage = GetResourceStorage(resourceChests[chestIndex]);
                if (storage != null && storage.CanDeposit(type))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsWithinResourceDepositArea(Vector2 worldPosition)
    {
        if (resourceDepositTrigger != null && resourceDepositTrigger.enabled)
        {
            return resourceDepositTrigger.OverlapPoint(worldPosition);
        }

        Transform point = ResourceDepositPoint;
        return point != null &&
               Vector2.Distance(worldPosition, point.position) <= ResourceDepositArrivalDistance;
    }

    public bool CanExteriorTurretTargetPlayer(Vector2 playerPosition)
    {
        if (!baseTurretsAreExterior || baseBoundsCollider == null)
        {
            return true;
        }

        Bounds bounds = baseBoundsCollider.bounds;
        float inset = Mathf.Max(0f, protectedInteriorInset);
        float insetX = Mathf.Min(inset, bounds.extents.x * 0.9f);
        float insetY = Mathf.Min(inset, bounds.extents.y * 0.9f);

        return playerPosition.x < bounds.min.x + insetX ||
               playerPosition.x > bounds.max.x - insetX ||
               playerPosition.y < bounds.min.y + insetY ||
               playerPosition.y > bounds.max.y - insetY;
    }

    /// <summary>
    /// 적 화물을 기지 자원 상자들에 순서대로 보관한다.
    /// depositedWeight는 EnemyCargoHold 기준으로 실제 이동된 화물 무게다.
    /// </summary>
    public bool TryDepositCargo(EnemyCargoHold cargoHold, out int depositedWeight)
    {
        depositedWeight = 0;

        if (cargoHold == null || !cargoHold.HasCargo || resourceChests == null || resourceChests.Length == 0)
        {
            return false;
        }

        int usedCapacityBefore = cargoHold.UsedCapacity;
        CurrencyType[] order =
        {
            CurrencyType.CoreShards,
            CurrencyType.StabilizedAlloy,
            CurrencyType.ScrapParts,
            CurrencyType.Credits,
            CurrencyType.Experience
        };

        for (int typeIndex = 0; typeIndex < order.Length; typeIndex++)
        {
            CurrencyType type = order[typeIndex];
            int remainingAmount = cargoHold.GetAmount(type);

            if (remainingAmount <= 0)
            {
                continue;
            }

            for (int chestIndex = 0; chestIndex < resourceChests.Length && remainingAmount > 0; chestIndex++)
            {
                FieldBaseResourceChest storage = GetResourceStorage(resourceChests[chestIndex]);
                if (storage == null || !storage.CanDeposit(type))
                {
                    continue;
                }

                int accepted = storage.TryDeposit(type, remainingAmount);
                if (accepted <= 0)
                {
                    continue;
                }

                int removed = cargoHold.Remove(type, accepted);
                remainingAmount -= removed;
            }
        }

        depositedWeight = Mathf.Max(0, usedCapacityBefore - cargoHold.UsedCapacity);
        return depositedWeight > 0;
    }

    public void ConfigurePortalDestination(Transform destinationPoint, Vector2 destinationOffset)
    {
        shopPortalDestinationPoint = destinationPoint;
        shopPortalDestinationOffset = destinationOffset;

        if (activePortal == null)
        {
            return;
        }

        FieldBaseTransferPortal transferPortal = activePortal.GetComponent<FieldBaseTransferPortal>();
        if (transferPortal == null)
        {
            transferPortal = activePortal.GetComponentInChildren<FieldBaseTransferPortal>(true);
        }

        if (transferPortal != null)
        {
            transferPortal.ConfigureDestination(shopPortalDestinationPoint, shopPortalDestinationOffset);
        }
    }

    public void TryActivatePortal(GameObject interactor)
    {
        if (portalActivationRunning || npcRescueCutsceneRunning)
        {
            return;
        }

        if (!IsNpcRescued())
        {
            ShowWarning(portalLockedWarning);
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        StartCoroutine(PortalActivationRoutine(interactor));
    }

    private void HandleSecurityNodeDisabled(FieldBaseSecurityNode _)
    {
        if (!AllSecurityNodesDisabled())
        {
            return;
        }

        ResolveSecurityOffline();
    }

    private void ResolveSecurityOffline()
    {
        if (securityResolved)
        {
            return;
        }

        securityResolved = true;
        ShowWarning(string.IsNullOrWhiteSpace(securityOfflineWarning)
            ? $"{baseDisplayName} 전력망 차단"
            : securityOfflineWarning);

        if (openPrisonGatesWhenSecurityOffline)
        {
            SetGatesOpen(prisonGates, true);
        }

        if (openStorageGatesWhenSecurityOffline)
        {
            SetGatesOpen(storageGates, true);
        }

        if (openPortalCornerGatesWhenSecurityOffline)
        {
            SetGatesOpen(portalCornerGates, true);
        }

        if (openPerimeterGatesWhenSecurityOffline)
        {
            SetGatesOpen(perimeterGates, true);
        }

        RefreshDefensePowerState();
        ApplyPowerDependentDamageState(true);

        if (resourceChests != null && resourceChests.Length > 0)
        {
            ShowWarning(resourceStorageUnlockedWarning);
        }

        if (npcPrisonMachine != null && !npcPrisonMachine.IsDead)
        {
            ShowWarning(npcMachineVulnerableWarning);
        }
        else if (autoReleaseNpcWhenSecurityOffline && captiveNpc != null)
        {
            ResolveNpcRescue();
        }

        ObjectiveCompleted?.Invoke(this);
    }

    private void ApplyPowerDependentDamageState(bool powerOffline)
    {
        bool npcMachineDamageEnabled = !protectNpcPrisonMachineWhilePowerOnline || powerOffline;

        if (npcPrisonMachine != null && !npcPrisonMachine.IsDead)
        {
            npcPrisonMachine.SetPlayerProjectileDamageEnabled(npcMachineDamageEnabled, true);
        }

        SetObjectsActive(npcProtectionOnlineVisuals, !npcMachineDamageEnabled);
        SetObjectsActive(npcProtectionOfflineVisuals, npcMachineDamageEnabled);

        bool resourceDamageEnabled = !lockResourceChestsUntilSecurityOffline || powerOffline;
        SetResourceChestDamageEnabled(resourceDamageEnabled);
    }

    private void HandleNpcPrisonMachineDestroyed(HarvestObjectHealth machine)
    {
        if (machine == null || machine != npcPrisonMachine)
        {
            return;
        }

        ResolveNpcRescue();
    }

    private void ResolveNpcRescue()
    {
        if (npcRescueResolved)
        {
            return;
        }

        npcRescueResolved = true;

        if (captiveNpc != null)
        {
            captiveNpc.ReleaseFromCaptivity(false);
        }

        ShowWarning(npcReleasedWarning);
        AudioManager.PlayAt(SoundEventIds.EventComplete, captiveNpc != null ? captiveNpc.transform.position : transform.position, 0.9f);

        if (autoCreatePortalAfterNpcRescue && captiveNpc != null)
        {
            StartCoroutine(NpcPortalCutsceneRoutine());
        }
    }

    private IEnumerator NpcPortalCutsceneRoutine()
    {
        if (npcRescueCutsceneRunning || captiveNpc == null)
        {
            yield break;
        }

        npcRescueCutsceneRunning = true;

        PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>();
        PlayerWeaponController weaponController = playerController != null
            ? playerController.GetComponent<PlayerWeaponController>()
            : FindFirstObjectByType<PlayerWeaponController>();

        if (lockPlayerDuringNpcPortalCutscene)
        {
            playerController?.SetMovementLocked(true);
            weaponController?.SetExternalInputLocked(true);
        }

        FieldNpcInertialMover2D npcMover = captiveNpc.GetComponent<FieldNpcInertialMover2D>();
        Rigidbody2D npcBody = captiveNpc.GetComponent<Rigidbody2D>();

        npcMover?.SetForcedHold(true);

        if (!string.IsNullOrWhiteSpace(npcMovingWarning))
        {
            ShowWarning(npcMovingWarning);
        }

        if (npcPortalInteractionPoint != null)
        {
            yield return MoveNpcToPointRoutine(
                captiveNpc.transform,
                npcBody,
                npcPortalInteractionPoint.position,
                Mathf.Max(0.05f, npcMoveToPortalDuration)
            );
        }

        if (npcPortalInteractionDuration > 0f)
        {
            ShowWarning(portalChargingWarning);
            AudioManager.PlayAt(SoundEventIds.EventStart, portalSpawnPoint != null ? portalSpawnPoint.position : transform.position, 0.8f);
            yield return new WaitForSeconds(npcPortalInteractionDuration);
        }

        bool portalCreated = SpawnOrRefreshPortal();

        if (portalCreated)
        {
            portalCutscenePlayed = true;

            if (openPortalCornerGatesWhenPortalReady)
            {
                SetGatesOpen(portalCornerGates, true);
            }

            ShowWarning(portalReadyWarning);
            AudioManager.PlayAt(SoundEventIds.EventComplete, portalSpawnPoint != null ? portalSpawnPoint.position : transform.position, 0.9f);
        }
        else
        {
            ShowWarning(portalMissingWarning);
            AudioManager.Play(SoundEventIds.ActionDenied);
        }

        if (npcMover != null)
        {
            npcMover.SetAnchorPosition(captiveNpc.transform.position, true);
            npcMover.SetForcedHold(false);
        }

        if (lockPlayerDuringNpcPortalCutscene)
        {
            weaponController?.SetExternalInputLocked(false);
            playerController?.SetMovementLocked(false);
        }

        npcRescueCutsceneRunning = false;
    }

    private static IEnumerator MoveNpcToPointRoutine(
        Transform npcTransform,
        Rigidbody2D npcBody,
        Vector3 destination,
        float duration)
    {
        if (npcTransform == null)
        {
            yield break;
        }

        Vector2 start = npcBody != null ? npcBody.position : (Vector2)npcTransform.position;
        Vector2 end = destination;
        float elapsed = 0f;

        if (npcBody != null)
        {
            npcBody.linearVelocity = Vector2.zero;
            npcBody.angularVelocity = 0f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - (2f * t));
            Vector2 position = Vector2.Lerp(start, end, eased);

            if (npcBody != null)
            {
                npcBody.MovePosition(position);
            }
            else
            {
                npcTransform.position = new Vector3(position.x, position.y, npcTransform.position.z);
            }

            yield return new WaitForFixedUpdate();
        }

        if (npcBody != null)
        {
            npcBody.position = end;
            npcBody.linearVelocity = Vector2.zero;
        }
        else
        {
            npcTransform.position = new Vector3(end.x, end.y, npcTransform.position.z);
        }
    }

    private IEnumerator PortalActivationRoutine(GameObject interactor)
    {
        portalActivationRunning = true;

        PlayerController2D playerController = interactor != null
            ? interactor.GetComponentInParent<PlayerController2D>()
            : null;

        PlayerWeaponController weaponController = playerController != null
            ? playerController.GetComponent<PlayerWeaponController>()
            : null;

        if (playerController != null)
        {
            playerController.SetMovementLocked(true);
            weaponController?.SetExternalInputLocked(true);
        }

        bool shouldPlayCutscene = !skipCutsceneAfterFirstActivation || !portalCutscenePlayed;

        if (shouldPlayCutscene)
        {
            ShowWarning(portalChargingWarning);
            AudioManager.PlayAt(SoundEventIds.EventStart, transform.position, 0.8f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, portalChargeDuration));
            portalCutscenePlayed = true;
        }

        bool portalCreated = SpawnOrRefreshPortal();

        if (portalCreated)
        {
            if (openPortalCornerGatesWhenPortalReady)
            {
                SetGatesOpen(portalCornerGates, true);
            }

            ShowWarning(portalReadyWarning);
            AudioManager.PlayAt(SoundEventIds.EventComplete, transform.position, 0.9f);
        }
        else
        {
            ShowWarning(portalMissingWarning);
            AudioManager.Play(SoundEventIds.ActionDenied);
        }

        if (playerController != null)
        {
            weaponController?.SetExternalInputLocked(false);
            playerController.SetMovementLocked(false);
        }

        portalActivationRunning = false;
    }

    private bool SpawnOrRefreshPortal()
    {
        Transform destinationPoint = ResolveShopPortalDestinationPoint();

        if (portalPrefab == null || portalSpawnPoint == null || destinationPoint == null)
        {
            return false;
        }

        Vector3 spawnPosition = portalSpawnPoint.position;

        if (activePortal == null)
        {
            activePortal = Instantiate(portalPrefab, spawnPosition, portalSpawnPoint.rotation);
        }
        else
        {
            activePortal.transform.SetPositionAndRotation(spawnPosition, portalSpawnPoint.rotation);
            activePortal.SetActive(true);
        }

        FieldBaseTransferPortal transferPortal = activePortal.GetComponent<FieldBaseTransferPortal>();
        if (transferPortal == null)
        {
            transferPortal = activePortal.GetComponentInChildren<FieldBaseTransferPortal>(true);
        }

        if (transferPortal != null)
        {
            transferPortal.ConfigureDestination(destinationPoint, shopPortalDestinationOffset);
        }

        return activePortal != null && transferPortal != null;
    }

    private Transform ResolveShopPortalDestinationPoint()
    {
        if (shopPortalDestinationPoint != null)
        {
            return shopPortalDestinationPoint;
        }

        if (!autoFindNearestShopPortalDestination)
        {
            return null;
        }

        ShopStructure[] shops = FindObjectsByType<ShopStructure>(FindObjectsSortMode.None);
        ShopStructure bestNeutralShop = null;
        ShopStructure bestAliveShop = null;
        float bestNeutralSqrDistance = float.MaxValue;
        float bestAliveSqrDistance = float.MaxValue;

        for (int i = 0; i < shops.Length; i++)
        {
            ShopStructure shop = shops[i];
            if (shop == null || shop.IsDead)
            {
                continue;
            }

            Transform arrivalPoint = shop.PortalArrivalPoint;
            if (arrivalPoint == null)
            {
                continue;
            }

            float sqrDistance = (arrivalPoint.position - transform.position).sqrMagnitude;

            if (sqrDistance < bestAliveSqrDistance)
            {
                bestAliveSqrDistance = sqrDistance;
                bestAliveShop = shop;
            }

            if (shop.CanTrade && sqrDistance < bestNeutralSqrDistance)
            {
                bestNeutralSqrDistance = sqrDistance;
                bestNeutralShop = shop;
            }
        }

        ShopStructure selectedShop = bestNeutralShop != null ? bestNeutralShop : bestAliveShop;
        if (selectedShop == null)
        {
            return null;
        }

        shopPortalDestinationPoint = selectedShop.PortalArrivalPoint;
        return shopPortalDestinationPoint;
    }

    private void ApplyInitialGateState()
    {
        SetGatesOpen(prisonGates, false);
        SetGatesOpen(storageGates, false);
        SetGatesOpen(portalCornerGates, false);
        SetGatesOpen(perimeterGates, false);
    }

    private void RefreshDefensePowerState()
    {
        bool powerOn = !IsSecurityOffline;

        RegisterManualTurrets();
        CleanupRuntimeTurrets();

        for (int i = 0; i < runtimeTurrets.Count; i++)
        {
            if (runtimeTurrets[i] != null)
            {
                runtimeTurrets[i].SetPowered(powerOn);
            }
        }

        for (int i = runtimeTurretPowerLinks.Count - 1; i >= 0; i--)
        {
            FieldBasePowerLink2D link = runtimeTurretPowerLinks[i];

            if (link == null)
            {
                runtimeTurretPowerLinks.RemoveAt(i);
                continue;
            }

            link.SetBasePowered(powerOn);
        }
    }

    private void RegisterManualTurrets()
    {
        if (baseTurrets == null)
        {
            return;
        }

        for (int i = 0; i < baseTurrets.Length; i++)
        {
            RegisterRuntimeTurret(baseTurrets[i]);
        }
    }

    private void SpawnConfiguredTurrets()
    {
        if (turretSpawnPassCompleted)
        {
            return;
        }

        turretSpawnPassCompleted = true;

        if (turretSpawnPoints == null || turretSpawnPoints.Length == 0)
        {
            if (logTurretSpawnResults)
            {
                Debug.LogWarning($"[{name}] Turret Spawn Points가 비어 있습니다.", this);
            }

            return;
        }

        Transform spawnParent = turretRuntimeRoot != null ? turretRuntimeRoot : transform;
        HashSet<Transform> processedSpawnPoints = new HashSet<Transform>();
        int spawnedCount = 0;
        int reusedCount = 0;

        for (int i = 0; i < turretSpawnPoints.Length; i++)
        {
            Transform spawnPoint = turretSpawnPoints[i];
            if (spawnPoint == null)
            {
                if (logTurretSpawnResults)
                {
                    Debug.LogWarning($"[{name}] Turret Spawn Point {i}가 비어 있습니다.", this);
                }

                continue;
            }

            if (!processedSpawnPoints.Add(spawnPoint))
            {
                Debug.LogWarning(
                    $"[{name}] Turret Spawn Points에 같은 Transform이 중복 연결되었습니다: {spawnPoint.name} (Index {i})",
                    this
                );
                continue;
            }

            BaseTurretController turret = skipOccupiedTurretSpawnPoints
                ? FindTurretAtSpawnPoint(spawnPoint)
                : null;

            if (turret != null)
            {
                reusedCount++;
            }

            if (turret == null)
            {
                if (turretPrefab == null)
                {
                    Debug.LogWarning($"[{name}] Turret Prefab이 비어 있어 {spawnPoint.name}에 생성할 수 없습니다.", this);
                    continue;
                }

                GameObject turretObject = Instantiate(turretPrefab);
                turretObject.transform.SetParent(spawnParent, false);
                turretObject.transform.localScale = turretPrefab.transform.localScale;

                Quaternion rotation = useTurretSpawnPointRotation
                    ? spawnPoint.rotation
                    : Quaternion.identity;

                turretObject.transform.SetPositionAndRotation(spawnPoint.position, rotation);
                turretObject.name = $"{turretPrefab.name}_{i:00}";

                if (!turretObject.activeSelf)
                {
                    turretObject.SetActive(true);
                }

                turret = turretObject.GetComponent<BaseTurretController>();
                if (turret == null)
                {
                    turret = turretObject.GetComponentInChildren<BaseTurretController>(true);
                }

                if (turret == null)
                {
                    Debug.LogWarning(
                        $"[{name}] Turret Prefab에 BaseTurretController가 없습니다: {turretPrefab.name}",
                        turretObject
                    );
                    continue;
                }

                spawnedCount++;
            }

            RegisterRuntimeTurret(turret);
            CreatePowerLinkForTurret(i, turret);
        }

        if (logTurretSpawnResults)
        {
            Debug.Log(
                $"[{name}] 터렛 생성 완료 | 신규 {spawnedCount} | 기존 재사용 {reusedCount} | 등록 {runtimeTurrets.Count}",
                this
            );
        }
    }

    private BaseTurretController FindTurretAtSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            return null;
        }

        BaseTurretController childTurret = spawnPoint.GetComponentInChildren<BaseTurretController>(true);
        if (childTurret != null)
        {
            return childTurret;
        }

        BaseTurretController parentTurret = spawnPoint.GetComponentInParent<BaseTurretController>();
        if (parentTurret != null)
        {
            return parentTurret;
        }

        float checkRadius = Mathf.Max(0.05f, occupiedTurretCheckRadius);
        float checkRadiusSqr = checkRadius * checkRadius;
        BaseTurretController[] sceneTurrets = FindObjectsByType<BaseTurretController>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneTurrets.Length; i++)
        {
            BaseTurretController candidate = sceneTurrets[i];
            if (candidate == null)
            {
                continue;
            }

            if ((candidate.transform.position - spawnPoint.position).sqrMagnitude <= checkRadiusSqr)
            {
                return candidate;
            }
        }

        return null;
    }

    private void RegisterRuntimeTurret(BaseTurretController turret)
    {
        if (turret == null || runtimeTurrets.Contains(turret))
        {
            return;
        }

        runtimeTurrets.Add(turret);
        turret.ConfigureFieldBaseDefense(this, baseTurretsAreExterior);
    }

    private void CleanupRuntimeTurrets()
    {
        for (int i = runtimeTurrets.Count - 1; i >= 0; i--)
        {
            if (runtimeTurrets[i] == null)
            {
                runtimeTurrets.RemoveAt(i);
            }
        }
    }

    private void CreatePowerLinkForTurret(int index, BaseTurretController turret)
    {
        if (!createTurretPowerLinks || turret == null || HasPowerLinkForTurret(turret))
        {
            return;
        }

        ResolveTurretPowerLinkEndpoints(index, turret, out Transform startPoint, out Transform endPoint);

        if (startPoint == null || endPoint == null)
        {
            if (logTurretSpawnResults)
            {
                Debug.LogWarning($"[{name}] 터렛 {index} 전력선 Start/End가 비어 있어 연결선을 만들지 않습니다.", this);
            }

            return;
        }

        Transform parent = turretPowerLinkRoot != null ? turretPowerLinkRoot : transform;
        GameObject linkObject = null;

        if (turretPowerLinkPrefab != null)
        {
            linkObject = Instantiate(
                turretPowerLinkPrefab,
                Vector3.zero,
                Quaternion.identity,
                parent
            );
        }
        else if (autoCreatePowerLinkWhenPrefabMissing)
        {
            linkObject = new GameObject($"TurretPowerLink_{index:00}");
            linkObject.transform.SetParent(parent, false);
        }

        if (linkObject == null)
        {
            return;
        }

        FieldBasePowerLink2D link = linkObject.GetComponent<FieldBasePowerLink2D>();
        if (link == null)
        {
            link = linkObject.AddComponent<FieldBasePowerLink2D>();
        }

        linkObject.name = $"TurretPowerLink_{index:00}";
        link.Bind(startPoint, endPoint, turret);
        link.SetBasePowered(!IsSecurityOffline);
        runtimeTurretPowerLinks.Add(link);
    }

    private bool HasPowerLinkForTurret(BaseTurretController turret)
    {
        for (int i = runtimeTurretPowerLinks.Count - 1; i >= 0; i--)
        {
            FieldBasePowerLink2D link = runtimeTurretPowerLinks[i];

            if (link == null)
            {
                runtimeTurretPowerLinks.RemoveAt(i);
                continue;
            }

            if (link.LinkedTurret == turret)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveTurretPowerLinkEndpoints(
        int index,
        BaseTurretController turret,
        out Transform startPoint,
        out Transform endPoint)
    {
        startPoint = null;
        endPoint = null;

        if (turretPowerLinkEndpoints != null &&
            index >= 0 &&
            index < turretPowerLinkEndpoints.Length)
        {
            TurretPowerLinkEndpoints endpoints = turretPowerLinkEndpoints[index];
            if (endpoints != null)
            {
                startPoint = endpoints.StartPoint;
                endPoint = endpoints.EndPoint;
            }
        }

        if (startPoint == null)
        {
            startPoint = ResolveTurretPowerSourcePoint(index);
        }

        if (endPoint == null && turret != null)
        {
            endPoint = turret.PowerLinkAnchor;
        }
    }

    private Transform ResolveTurretPowerSourcePoint(int index)
    {
        if (turretPowerSourcePoints != null &&
            index >= 0 &&
            index < turretPowerSourcePoints.Length &&
            turretPowerSourcePoints[index] != null)
        {
            return turretPowerSourcePoints[index];
        }

        if (defaultTurretPowerSourcePoint != null)
        {
            return defaultTurretPowerSourcePoint;
        }

        if (securityNodes != null)
        {
            for (int i = 0; i < securityNodes.Length; i++)
            {
                if (securityNodes[i] != null)
                {
                    return securityNodes[i].transform;
                }
            }
        }

        return transform;
    }

    private void SpawnAndAssignDefenders()
    {
        if (defenderSpawnPassCompleted)
        {
            return;
        }

        defenderSpawnPassCompleted = true;

        if (defenders == null || defenders.Length == 0 ||
            defenderZoneAnchors == null || defenderZoneAnchors.Length == 0)
        {
            return;
        }

        Bounds bounds = ResolveBaseBounds();
        Transform spawnParent = defenderRuntimeRoot != null ? defenderRuntimeRoot : transform;
        int sceneDefenderIndex = 0;

        for (int anchorIndex = 0; anchorIndex < defenderZoneAnchors.Length; anchorIndex++)
        {
            Transform zoneAnchor = defenderZoneAnchors[anchorIndex];
            EnemyRoleController defender = ResolveSceneDefender(ref sceneDefenderIndex);

            if (zoneAnchor == null)
            {
                continue;
            }

            if (defender == null && spawnDefendersOnStart)
            {
                EnemyRoleController template = ResolveDefenderTemplate(anchorIndex);
                if (template != null)
                {
                    Quaternion rotation = useDefenderAnchorRotation
                        ? zoneAnchor.rotation
                        : Quaternion.identity;
                    GameObject defenderObject = Instantiate(
                        template.gameObject,
                        zoneAnchor.position,
                        rotation
                    );
                    defenderObject.transform.SetParent(spawnParent, true);
                    defenderObject.name = $"{template.gameObject.name}_Defender_{anchorIndex:00}";
                    defender = defenderObject.GetComponent<EnemyRoleController>() ??
                               defenderObject.GetComponentInChildren<EnemyRoleController>(true);
                }
            }

            if (defender == null)
            {
                continue;
            }

            defender.ConfigureAsDefender(zoneAnchor, bounds);
            runtimeDefenders.Add(defender);
        }
    }

    private EnemyRoleController ResolveSceneDefender(ref int startIndex)
    {
        for (int i = startIndex; i < defenders.Length; i++)
        {
            EnemyRoleController candidate = defenders[i];
            startIndex = i + 1;

            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private EnemyRoleController ResolveDefenderTemplate(int anchorIndex)
    {
        int count = defenders != null ? defenders.Length : 0;

        for (int offset = 0; offset < count; offset++)
        {
            EnemyRoleController candidate = defenders[(anchorIndex + offset) % count];
            if (candidate != null && !candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private Bounds ResolveBaseBounds()
    {
        if (baseBoundsCollider != null)
        {
            return baseBoundsCollider.bounds;
        }

        return new Bounds(transform.position, Vector3.one * 12f);
    }

    private bool IsNpcRescued()
    {
        if (captiveNpc == null)
        {
            return true;
        }

        return captiveNpc.State == FieldNpcState.Available ||
               captiveNpc.State == FieldNpcState.Exhausted ||
               captiveNpc.State == FieldNpcState.RewardPending;
    }

    private bool AllSecurityNodesDisabled()
    {
        if (securityNodes == null || securityNodes.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < securityNodes.Length; i++)
        {
            if (securityNodes[i] != null && !securityNodes[i].IsDisabled)
            {
                return false;
            }
        }

        return true;
    }

    private void SetResourceChestDamageEnabled(bool enabled)
    {
        if (resourceChests == null)
        {
            return;
        }

        for (int i = 0; i < resourceChests.Length; i++)
        {
            HarvestObjectHealth chest = resourceChests[i];
            if (chest != null && !chest.IsDead)
            {
                chest.SetPlayerProjectileDamageEnabled(enabled, true);
            }
        }
    }

    private void HandleResourceChestDestroyed(HarvestObjectHealth chest)
    {
        if (chest == null || RemainingResourceChestCount > 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(allResourceChestsDestroyedWarning))
        {
            ShowWarning(allResourceChestsDestroyedWarning);
        }
    }

    private FieldBaseCargoRoute2D[] ResolveCargoRoutes()
    {
        if (cargoRoutes != null && cargoRoutes.Length > 0)
        {
            return cargoRoutes;
        }

        if (!findCargoRoutesInChildrenWhenEmpty)
        {
            return cargoRoutes;
        }

        cargoRoutes = GetComponentsInChildren<FieldBaseCargoRoute2D>(true);
        return cargoRoutes;
    }

    private Transform ResolveResourceDepositPoint()
    {
        if (resourceDepositPoint != null)
        {
            return resourceDepositPoint;
        }

        if (resourceChests == null)
        {
            return transform;
        }

        for (int i = 0; i < resourceChests.Length; i++)
        {
            if (resourceChests[i] != null && !resourceChests[i].IsDead)
            {
                return resourceChests[i].transform;
            }
        }

        return transform;
    }

    private bool HasAnyResourceStorageSpace()
    {
        if (resourceChests == null)
        {
            return false;
        }

        for (int i = 0; i < resourceChests.Length; i++)
        {
            FieldBaseResourceChest storage = GetResourceStorage(resourceChests[i]);
            if (storage != null && !storage.IsDestroyed && !storage.IsFull)
            {
                return true;
            }
        }

        return false;
    }

    private static FieldBaseResourceChest GetResourceStorage(HarvestObjectHealth chest)
    {
        if (chest == null || chest.IsDead)
        {
            return null;
        }

        FieldBaseResourceChest storage = chest.GetComponent<FieldBaseResourceChest>();
        if (storage == null)
        {
            storage = chest.GetComponentInChildren<FieldBaseResourceChest>(true);
        }

        return storage;
    }

    private int CountConfiguredResourceChests()
    {
        if (resourceChests == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < resourceChests.Length; i++)
        {
            if (resourceChests[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountRemainingResourceChests()
    {
        if (resourceChests == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < resourceChests.Length; i++)
        {
            if (resourceChests[i] != null && !resourceChests[i].IsDead)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetGatesOpen(FieldBaseLaserGate[] gates, bool open)
    {
        if (gates == null)
        {
            return;
        }

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] != null)
            {
                gates[i].SetGateOpen(open, true);
            }
        }
    }

    private void SubscribeSecurityNodes(bool subscribe)
    {
        if (securityNodes == null)
        {
            return;
        }

        for (int i = 0; i < securityNodes.Length; i++)
        {
            if (securityNodes[i] == null)
            {
                continue;
            }

            if (subscribe)
            {
                securityNodes[i].Disabled += HandleSecurityNodeDisabled;
            }
            else
            {
                securityNodes[i].Disabled -= HandleSecurityNodeDisabled;
            }
        }
    }

    private void SubscribeNpcPrisonMachine(bool subscribe)
    {
        if (npcPrisonMachine == null)
        {
            return;
        }

        if (subscribe)
        {
            npcPrisonMachine.Died += HandleNpcPrisonMachineDestroyed;
        }
        else
        {
            npcPrisonMachine.Died -= HandleNpcPrisonMachineDestroyed;
        }
    }

    private void SubscribeResourceChests(bool subscribe)
    {
        if (resourceChests == null)
        {
            return;
        }

        for (int i = 0; i < resourceChests.Length; i++)
        {
            HarvestObjectHealth chest = resourceChests[i];
            if (chest == null)
            {
                continue;
            }

            if (subscribe)
            {
                chest.Died += HandleResourceChestDestroyed;
            }
            else
            {
                chest.Died -= HandleResourceChestDestroyed;
            }
        }
    }

    private static void SetObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private static void ShowWarning(string message)
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null && !string.IsNullOrWhiteSpace(message))
        {
            hud.ShowWarning(message);
        }
    }
}
