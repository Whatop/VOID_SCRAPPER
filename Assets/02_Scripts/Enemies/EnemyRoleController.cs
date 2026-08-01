using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBaseAI))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyRoleController : MonoBehaviour
{
    private static readonly HashSet<HarvestObjectHealth> ReservedHarvestTargets = new HashSet<HarvestObjectHealth>();
    private static readonly HashSet<RewardPickup> ReservedRewardPickups = new HashSet<RewardPickup>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticReservations()
    {
        ReservedHarvestTargets.Clear();
        ReservedRewardPickups.Clear();
    }

    [Header("Role")]
    [SerializeField] private EnemyRoleType roleType = EnemyRoleType.Patrol;
    [SerializeField] private EnemyRolePhase currentPhase = EnemyRolePhase.None;
    [SerializeField] private EnemyRoleSimulationGate simulationGate;

    [Header("Defender")]
    [SerializeField] private Transform protectedTarget;
    [SerializeField] private float defenderPatrolRadius = 2.5f;
    [SerializeField] private float defenderWarningRadius = 8f;
    [SerializeField] private float defenderAggroRadius = 6f;
    [SerializeField] private float defenderLeashRadius = 11f;
    [SerializeField] private float defenderDisengageRadius = 13f;

    [Header("Rival Harvester")]
    [SerializeField] private float harvestSearchRadius = 35f;
    [SerializeField] private float harvestRange = 1.2f;
    [SerializeField] private float harvestWarmupDuration = 1.5f;
    [SerializeField] private float harvestTickInterval = 0.75f;
    [SerializeField] private float harvestDamagePerTick = 0.75f;
    [SerializeField] private int maxHarvestObjectsBeforeEscape = 1;
    [SerializeField] private float harvesterMoveSpeedMultiplier = 1.05f;
    [SerializeField] private float collectLootDuration = 4f;
    [SerializeField] private float collectedLootSearchRadius = 7f;
    [SerializeField] private float rivalPlayerEngageRange = 7f;
    [SerializeField] private float rivalFleeHealthRatio = 0.3f;
    [Range(0.1f, 1f)]
    [SerializeField] private float rivalEscapeCargoRatio = 0.75f;
    [SerializeField] private int rivalCargoCapacity = 14;

    [Header("Scavenger")]
    [SerializeField] private float scavengerSearchRadius = 20f;
    [SerializeField] private float scavengerCollectRange = 0.45f;
    [SerializeField] private float pickupCollectChannelDuration = 0.5f;
    [SerializeField] private float scavengerMoveSpeedMultiplier = 1.25f;
    [SerializeField] private float scavengerPanicRange = 5f;
    [SerializeField] private float scavengerPanicDuration = 2.5f;
    [SerializeField] private float scavengerIdleBeforeEscape = 3f;
    [Range(0.1f, 1f)]
    [SerializeField] private float scavengerEscapeCargoRatio = 0.65f;
    [SerializeField] private int scavengerCargoCapacity = 12;

    [Header("Search")]
    [SerializeField] private float targetSearchInterval = 0.35f;
    [SerializeField] private float idlePatrolRadius = 3f;
    [SerializeField] private float idleArriveDistance = 0.2f;

    [Header("Escape")]
    [SerializeField] private float escapePreparationDuration = 3f;
    [SerializeField] private float escapeSpeedMultiplier = 1.7f;
    [SerializeField] private float escapeArrivalDistance = 0.6f;
    [SerializeField] private float escapeOutsidePadding = 1.5f;

    [Header("Cargo Return Base")]
    [Tooltip("직접 연결하면 이 기지로만 복귀합니다. 비어 있으면 Auto Find가 켜진 경우 가장 가까운 저장 가능 기지를 찾습니다.")]
    [SerializeField] private FieldBaseController homeBase;
    [SerializeField] private bool returnCargoToBase = true;
    [SerializeField] private bool autoFindNearestBaseWhenMissing = true;
    [SerializeField] private bool useBaseCargoRoutes = true;
    [Min(0f)]
    [SerializeField] private float cargoDepositDuration = 0.8f;
    [SerializeField] private string cargoReturnWarning = "적 화물선이 기지로 복귀합니다";
    [SerializeField] private string cargoDepositWarning = "적 기지 자원 보관량이 증가했습니다";

    [Header("Radar Role Visual")]
    [SerializeField] private bool applyRoleRadarVisual = true;
    [SerializeField] private Sprite roleMarkerSprite;
    [SerializeField] private Color defenderMarkerColor = new Color(1f, 0.35f, 0.12f, 1f);
    [SerializeField] private Color rivalMarkerColor = new Color(1f, 0.75f, 0.12f, 1f);
    [SerializeField] private Color scavengerMarkerColor = new Color(0.95f, 0.2f, 0.85f, 1f);
    [SerializeField] private float roleMarkerScale = 1.15f;

    [Header("Harvest Beam Optional")]
    [SerializeField] private LineRenderer harvestBeam;

    [Header("Player Notification")]
    [SerializeField] private float roleNotificationDistance = 22f;
    [SerializeField] private float roleNotificationCooldown = 2f;

    [Header("Debug")]
    [SerializeField] private bool logRoleFlow;

    private EnemyBaseAI ai;
    private EnemyHealth health;
    private EnemyCargoHold cargoHold;
    private RadarTarget radarTarget;
    private ExpeditionHUD expeditionHUD;
    private HarvestObjectHealth protectedHarvestTarget;
    private HarvestObjectHealth harvestTarget;
    private RewardPickup rewardPickupTarget;

    private Bounds mapBounds;
    private bool hasMapBounds;

    private Vector2 rolePatrolTarget;
    private Vector2 lastHarvestPosition;
    private Vector2 escapeTarget;

    private float targetSearchTimer;
    private float harvestTickTimer;
    private float harvestWarmupTimer;
    private float collectTimer;
    private float pickupChannelTimer;
    private float noTargetTimer;
    private float panicTimer;
    private float escapePreparationTimer;
    private float cargoDepositTimer;
    private float lastRoleNotificationTime = -999f;
    private int harvestedObjectCount;
    private RewardPickup channelingPickup;
    private bool harvestStartNotified;
    private bool returningCargoToBase;
    private bool cargoDepositStarted;
    private bool leavingCargoBase;
    private FieldBaseCargoRoute2D activeCargoRoute;
    private int cargoRouteWaypointIndex;

    public EnemyRoleType RoleType => roleType;
    public EnemyRolePhase CurrentPhase => currentPhase;
    public Transform ProtectedTarget => protectedTarget;
    public EnemyCargoHold CargoHold => cargoHold;
    public FieldBaseController HomeBase => homeBase;
    public bool IsEscaping => currentPhase == EnemyRolePhase.PreparingEscape || currentPhase == EnemyRolePhase.Fleeing;
    public bool IsReturningCargoToBase => returningCargoToBase;
    public EnemyRoleSimulationLevel SimulationLevel => simulationGate != null
        ? simulationGate.CurrentLevel
        : EnemyRoleSimulationLevel.Active;

    private void Reset()
    {
        ai = GetComponent<EnemyBaseAI>();
        health = GetComponent<EnemyHealth>();
        radarTarget = GetComponent<RadarTarget>();
        cargoHold = GetComponent<EnemyCargoHold>();
        simulationGate = GetComponent<EnemyRoleSimulationGate>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (ai != null)
        {
            ai.SetRoleController(this);
        }

        if (health != null)
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        InitializeRoleRuntime();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        UnsubscribeProtectedTarget();
        ReleaseHarvestTarget();
        ReleaseRewardPickupTarget();
        SetHarvestBeamVisible(false);

        if (simulationGate != null)
        {
            simulationGate.ReleaseIrreversibleSlot();
        }
    }

    public void ConfigureAsPatrol(Bounds bounds)
    {
        roleType = EnemyRoleType.Patrol;
        SetMapBounds(bounds);
        InitializeRoleRuntime();
    }

    public void ConfigureAsDefender(Transform target, Bounds bounds)
    {
        roleType = EnemyRoleType.Defender;
        protectedTarget = target;
        SetMapBounds(bounds);
        InitializeRoleRuntime();
    }

    public void ConfigureAsRivalHarvester(Bounds bounds)
    {
        roleType = EnemyRoleType.RivalHarvester;
        protectedTarget = null;
        SetMapBounds(bounds);
        EnsureCargoHold(rivalCargoCapacity);
        InitializeRoleRuntime();
    }

    public void ConfigureAsScavenger(Bounds bounds)
    {
        roleType = EnemyRoleType.Scavenger;
        protectedTarget = null;
        SetMapBounds(bounds);
        EnsureCargoHold(scavengerCargoCapacity);
        InitializeRoleRuntime();
    }

    public void SetHomeBase(FieldBaseController targetBase)
    {
        homeBase = targetBase;
    }

    public void ConfigureSimulationGate(
        float startGraceSeconds,
        float startMovementUnlockDistance,
        float previewDistance,
        float activeDistance,
        float activeHoldSeconds,
        float radarPreviewDuration,
        int maxConcurrentRivalActions,
        int maxConcurrentScavengerActions)
    {
        ResolveReferences();

        if (simulationGate != null)
        {
            simulationGate.Configure(
                roleType,
                startGraceSeconds,
                startMovementUnlockDistance,
                previewDistance,
                activeDistance,
                activeHoldSeconds,
                radarPreviewDuration,
                maxConcurrentRivalActions,
                maxConcurrentScavengerActions
            );
        }
    }

    public void SetRoleMarkerSprite(Sprite sprite)
    {
        roleMarkerSprite = sprite;
        RefreshRadarVisual();
    }

    public void SetMapBounds(Bounds bounds)
    {
        mapBounds = bounds;
        hasMapBounds = bounds.size.x > 0.01f && bounds.size.y > 0.01f;
    }

    public void RefreshRadarVisual()
    {
        ResolveReferences();

        if (!applyRoleRadarVisual || radarTarget == null)
        {
            return;
        }

        radarTarget.SetMarkerType(RadarMarkerType.Enemy);

        Sprite marker = roleMarkerSprite != null ? roleMarkerSprite : radarTarget.MarkerSprite;
        Color color = roleType switch
        {
            EnemyRoleType.Defender => defenderMarkerColor,
            EnemyRoleType.RivalHarvester => rivalMarkerColor,
            EnemyRoleType.Scavenger => scavengerMarkerColor,
            _ => radarTarget.MarkerColor
        };

        float scale = roleType == EnemyRoleType.Patrol
            ? radarTarget.MarkerScale
            : Mathf.Max(0.1f, roleMarkerScale);

        radarTarget.SetMarkerVisual(marker, color, scale);
    }

    public bool TryHandlePriority(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null || roleType == EnemyRoleType.Patrol)
        {
            return false;
        }

        if (currentPhase == EnemyRolePhase.PreparingEscape)
        {
            UpdateEscapePreparation(owner, deltaTime);
            return true;
        }

        if (currentPhase == EnemyRolePhase.Fleeing)
        {
            UpdateEscape(owner);
            return true;
        }

        return false;
    }

    public bool TryHandlePatrol(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        switch (roleType)
        {
            case EnemyRoleType.Defender:
                UpdateDefenderPatrol(owner);
                return true;

            case EnemyRoleType.RivalHarvester:
                UpdateRivalHarvester(owner, deltaTime);
                return true;

            case EnemyRoleType.Scavenger:
                UpdateScavenger(owner, deltaTime);
                return true;

            default:
                return false;
        }
    }

    public bool TryHandleAlert(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        if (roleType == EnemyRoleType.Defender && protectedTarget != null)
        {
            UpdateDefenderHome(owner);

            if (Vector2.Distance(transform.position, protectedTarget.position) > defenderLeashRadius)
            {
                owner.RequestState(EnemyState.Return);
                return true;
            }
        }

        if (roleType == EnemyRoleType.Scavenger)
        {
            panicTimer = Mathf.Max(panicTimer, scavengerPanicDuration);
            owner.RequestState(EnemyState.Combat);
            UpdateScavengerCombat(owner, deltaTime);
            return true;
        }

        if (roleType == EnemyRoleType.RivalHarvester)
        {
            if (ShouldRivalEscape())
            {
                BeginEscape(owner);
                return true;
            }

            ReleaseIrreversibleSlot();
        }

        return false;
    }

    public bool TryHandleCombat(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        switch (roleType)
        {
            case EnemyRoleType.Defender:
                return UpdateDefenderCombat(owner);

            case EnemyRoleType.RivalHarvester:
                if (ShouldRivalEscape())
                {
                    BeginEscape(owner);
                    return true;
                }

                ReleaseIrreversibleSlot();
                return false;

            case EnemyRoleType.Scavenger:
                UpdateScavengerCombat(owner, deltaTime);
                return true;

            default:
                return false;
        }
    }

    public bool TryHandleSearch(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        if (roleType == EnemyRoleType.Defender)
        {
            if (protectedTarget != null &&
                Vector2.Distance(transform.position, protectedTarget.position) > defenderLeashRadius)
            {
                owner.RequestState(EnemyState.Return);
                return true;
            }

            return false;
        }

        if (roleType == EnemyRoleType.RivalHarvester || roleType == EnemyRoleType.Scavenger)
        {
            owner.RequestState(EnemyState.Patrol);
            return true;
        }

        return false;
    }

    public bool TryHandleReturn(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        if (roleType == EnemyRoleType.Defender)
        {
            UpdateDefenderHome(owner);
            return false;
        }

        if (roleType == EnemyRoleType.RivalHarvester || roleType == EnemyRoleType.Scavenger)
        {
            owner.RequestState(EnemyState.Patrol);
            return true;
        }

        return false;
    }

    public bool TryHandleTaunt(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return false;
        }

        if (roleType == EnemyRoleType.Defender && protectedTarget != null)
        {
            UpdateDefenderHome(owner);

            if (Vector2.Distance(transform.position, protectedTarget.position) > defenderLeashRadius)
            {
                owner.RequestState(EnemyState.Return);
                return true;
            }
        }

        return false;
    }

    public bool CountsAsThreat(EnemyState state)
    {
        if (roleType == EnemyRoleType.Scavenger)
        {
            return false;
        }

        if (roleType == EnemyRoleType.RivalHarvester && IsEscaping)
        {
            return false;
        }

        return state == EnemyState.Alert ||
               state == EnemyState.Combat ||
               state == EnemyState.Search ||
               state == EnemyState.Taunt;
    }

    private void InitializeRoleRuntime()
    {
        ResolveReferences();
        UnsubscribeProtectedTarget();
        ReleaseHarvestTarget();
        ReleaseRewardPickupTarget();

        targetSearchTimer = 0f;
        harvestTickTimer = 0f;
        harvestWarmupTimer = 0f;
        collectTimer = 0f;
        pickupChannelTimer = 0f;
        noTargetTimer = 0f;
        panicTimer = 0f;
        escapePreparationTimer = 0f;
        cargoDepositTimer = 0f;
        lastRoleNotificationTime = -999f;
        harvestedObjectCount = 0;
        channelingPickup = null;
        harvestStartNotified = false;
        returningCargoToBase = false;
        cargoDepositStarted = false;
        leavingCargoBase = false;
        activeCargoRoute = null;
        cargoRouteWaypointIndex = 0;
        rolePatrolTarget = transform.position;
        lastHarvestPosition = transform.position;
        SetHarvestBeamVisible(false);

        if (simulationGate != null)
        {
            simulationGate.SetRoleType(roleType);
            simulationGate.ReleaseIrreversibleSlot();
        }

        switch (roleType)
        {
            case EnemyRoleType.Defender:
                currentPhase = EnemyRolePhase.Guarding;
                SubscribeProtectedTarget();
                UpdateDefenderHome(ai);
                break;

            case EnemyRoleType.RivalHarvester:
                EnsureCargoHold(rivalCargoCapacity);
                currentPhase = EnemyRolePhase.SeekingHarvest;
                break;

            case EnemyRoleType.Scavenger:
                EnsureCargoHold(scavengerCargoCapacity);
                currentPhase = EnemyRolePhase.SeekingLoot;
                break;

            default:
                currentPhase = EnemyRolePhase.None;
                break;
        }

        RefreshRadarVisual();
    }

    private void UpdateDefenderPatrol(EnemyBaseAI owner)
    {
        if (protectedTarget == null)
        {
            UpdateIdlePatrol(owner, owner.HomePosition, defenderPatrolRadius);
            return;
        }

        UpdateDefenderHome(owner);

        Transform player = owner.Player;

        if (player != null)
        {
            float playerDistanceToTarget = Vector2.Distance(player.position, protectedTarget.position);

            if (playerDistanceToTarget <= defenderAggroRadius &&
                owner.CanDirectlySeePlayerForRole())
            {
                owner.EngagePlayer();
                return;
            }

            if (playerDistanceToTarget <= defenderWarningRadius)
            {
                if (owner.CanDirectlySeePlayerForRole())
                {
                    owner.AlertTo(player.position);
                    return;
                }

                if (owner.CanSuspectPlayerForRole())
                {
                    owner.AlertTo(player.position);
                    return;
                }

                if (owner.CanDetectPlayerByRadarForRole())
                {
                    owner.AlertTo(player.position);
                    return;
                }
            }
        }

        UpdateIdlePatrol(owner, protectedTarget.position, defenderPatrolRadius);
    }

    private bool UpdateDefenderCombat(EnemyBaseAI owner)
    {
        if (protectedTarget == null)
        {
            return false;
        }

        UpdateDefenderHome(owner);

        float defenderDistance = Vector2.Distance(transform.position, protectedTarget.position);
        float playerDistance = owner.Player != null
            ? Vector2.Distance(owner.Player.position, protectedTarget.position)
            : float.MaxValue;

        if (defenderDistance > defenderLeashRadius ||
            (playerDistance > defenderDisengageRadius && defenderDistance > defenderPatrolRadius * 1.5f))
        {
            owner.CancelCurrentAttack();
            owner.RequestState(EnemyState.Return);
            return true;
        }

        return false;
    }

    private void UpdateDefenderHome(EnemyBaseAI owner)
    {
        if (owner == null || protectedTarget == null)
        {
            return;
        }

        owner.SetHomePosition(protectedTarget.position, false);
    }

    private void UpdateRivalHarvester(EnemyBaseAI owner, float deltaTime)
    {
        if (ShouldRivalEscape())
        {
            BeginEscape(owner);
            return;
        }

        if (owner.Player != null &&
            owner.CanSeePlayerForRole() &&
            Vector2.Distance(transform.position, owner.Player.position) <= rivalPlayerEngageRange)
        {
            owner.EngagePlayer();
            return;
        }

        if (owner.Player != null &&
            owner.CanSuspectPlayerForRole() &&
            Vector2.Distance(transform.position, owner.Player.position) <= rivalPlayerEngageRange)
        {
            owner.AlertTo(owner.Player.position);
            return;
        }

        switch (currentPhase)
        {
            case EnemyRolePhase.CollectingLoot:
                UpdateRivalLootCollection(owner, deltaTime);
                break;

            case EnemyRolePhase.Harvesting:
            case EnemyRolePhase.SeekingHarvest:
            default:
                UpdateHarvestTarget(owner, deltaTime);
                break;
        }
    }

    private void UpdateHarvestTarget(EnemyBaseAI owner, float deltaTime)
    {
        EnemyRoleSimulationLevel simulationLevel = RefreshSimulationLevel();

        if (harvestedObjectCount >= Mathf.Max(1, maxHarvestObjectsBeforeEscape) && harvestTarget == null)
        {
            BeginEscape(owner);
            return;
        }

        if (harvestTarget == null || harvestTarget.IsDead)
        {
            ReleaseHarvestTarget();
            currentPhase = EnemyRolePhase.SeekingHarvest;
            targetSearchTimer -= deltaTime;

            if (targetSearchTimer <= 0f)
            {
                targetSearchTimer = Mathf.Max(0.05f, targetSearchInterval);
                TryAcquireHarvestTarget();
            }

            if (harvestTarget == null)
            {
                noTargetTimer += simulationLevel == EnemyRoleSimulationLevel.Dormant ? 0f : deltaTime;

                if (cargoHold != null && cargoHold.HasCargo && noTargetTimer >= scavengerIdleBeforeEscape)
                {
                    BeginEscape(owner);
                    return;
                }

                UpdateIdlePatrol(owner, owner.HomePosition, idlePatrolRadius);
                return;
            }
        }

        noTargetTimer = 0f;
        float distance = Vector2.Distance(transform.position, harvestTarget.transform.position);

        if (distance > harvestRange)
        {
            currentPhase = EnemyRolePhase.SeekingHarvest;
            harvestWarmupTimer = 0f;
            harvestStartNotified = false;
            SetHarvestBeamVisible(false);
            owner.CommandMoveTo(
                harvestTarget.transform.position,
                harvesterMoveSpeedMultiplier,
                harvestTarget.transform
            );
            return;
        }

        currentPhase = EnemyRolePhase.Harvesting;
        owner.CommandStopMoving();
        owner.CommandFaceTo(harvestTarget.transform.position);

        if (simulationLevel == EnemyRoleSimulationLevel.Dormant)
        {
            SetHarvestBeamVisible(false);
            return;
        }

        UpdateHarvestBeam(harvestTarget.transform.position);

        if (!harvestStartNotified)
        {
            harvestStartNotified = true;
            NotifyRoleActivity("경쟁 회수 신호 감지");
        }

        if (simulationLevel != EnemyRoleSimulationLevel.Active)
        {
            harvestWarmupTimer = 0f;
            return;
        }

        float finalWarmupDuration = Mathf.Max(0.05f, harvestWarmupDuration);
        harvestWarmupTimer = Mathf.Min(finalWarmupDuration, harvestWarmupTimer + deltaTime);

        if (harvestWarmupTimer < finalWarmupDuration || !TryAcquireIrreversibleSlot())
        {
            return;
        }

        harvestTickTimer -= deltaTime;

        if (harvestTickTimer > 0f)
        {
            return;
        }

        harvestTickTimer = Mathf.Max(0.05f, harvestTickInterval);
        Vector2 incomingDirection = ((Vector2)harvestTarget.transform.position - (Vector2)transform.position).normalized;
        harvestTarget.TakeDamage(harvestDamagePerTick, harvestTarget.transform.position, incomingDirection);
    }

    private void UpdateRivalLootCollection(EnemyBaseAI owner, float deltaTime)
    {
        EnemyRoleSimulationLevel simulationLevel = RefreshSimulationLevel();
        SetHarvestBeamVisible(false);

        if (simulationLevel == EnemyRoleSimulationLevel.Active)
        {
            collectTimer -= deltaTime;
        }

        if (cargoHold != null && cargoHold.IsFull)
        {
            BeginEscape(owner);
            return;
        }

        if (rewardPickupTarget == null || !rewardPickupTarget.IsAvailable)
        {
            ReleaseRewardPickupTarget();
            targetSearchTimer -= deltaTime;

            if (targetSearchTimer <= 0f)
            {
                targetSearchTimer = Mathf.Max(0.05f, targetSearchInterval);
                TryAcquireRewardPickup(lastHarvestPosition, collectedLootSearchRadius, false);
            }
        }

        if (rewardPickupTarget != null)
        {
            if (UpdatePickupCollectionChannel(
                    owner,
                    deltaTime,
                    harvesterMoveSpeedMultiplier,
                    false,
                    false))
            {
                targetSearchTimer = 0f;
            }

            return;
        }

        if (collectTimer <= 0f)
        {
            if (harvestedObjectCount >= Mathf.Max(1, maxHarvestObjectsBeforeEscape) ||
                (cargoHold != null && cargoHold.HasCargo))
            {
                BeginEscape(owner);
            }
            else
            {
                currentPhase = EnemyRolePhase.SeekingHarvest;
                targetSearchTimer = 0f;
                ReleaseIrreversibleSlot();
            }
        }
        else
        {
            owner.CommandStopMoving();
        }
    }

    private void UpdateScavenger(EnemyBaseAI owner, float deltaTime)
    {
        EnemyRoleSimulationLevel simulationLevel = RefreshSimulationLevel();

        if (cargoHold != null && cargoHold.FillRatio >= scavengerEscapeCargoRatio)
        {
            BeginEscape(owner);
            return;
        }

        if (owner.Player != null &&
            Vector2.Distance(transform.position, owner.Player.position) <= scavengerPanicRange)
        {
            panicTimer = scavengerPanicDuration;
            owner.RequestState(EnemyState.Combat);
            UpdateScavengerCombat(owner, deltaTime);
            return;
        }

        currentPhase = EnemyRolePhase.SeekingLoot;

        if (rewardPickupTarget == null || !rewardPickupTarget.IsAvailable)
        {
            ReleaseRewardPickupTarget();
            targetSearchTimer -= deltaTime;

            if (targetSearchTimer <= 0f)
            {
                targetSearchTimer = Mathf.Max(0.05f, targetSearchInterval);
                TryAcquireRewardPickup(transform.position, scavengerSearchRadius, true);
            }
        }

        if (rewardPickupTarget != null)
        {
            noTargetTimer = 0f;
            UpdatePickupCollectionChannel(
                owner,
                deltaTime,
                scavengerMoveSpeedMultiplier,
                true,
                true
            );
            return;
        }

        ReleaseIrreversibleSlot();
        noTargetTimer += simulationLevel == EnemyRoleSimulationLevel.Dormant ? 0f : deltaTime;

        if (cargoHold != null && cargoHold.HasCargo && noTargetTimer >= scavengerIdleBeforeEscape)
        {
            BeginEscape(owner);
            return;
        }

        UpdateIdlePatrol(owner, owner.HomePosition, idlePatrolRadius);
    }

    private void UpdateScavengerCombat(EnemyBaseAI owner, float deltaTime)
    {
        ReleaseIrreversibleSlot();
        panicTimer = Mathf.Max(0f, panicTimer - deltaTime);

        if (cargoHold != null &&
            (cargoHold.FillRatio >= scavengerEscapeCargoRatio || health.HpRatio <= rivalFleeHealthRatio))
        {
            BeginEscape(owner);
            return;
        }

        Transform player = owner.Player;

        if (player == null)
        {
            owner.RequestState(EnemyState.Patrol);
            return;
        }

        Vector2 away = (Vector2)transform.position - (Vector2)player.position;

        if (away.sqrMagnitude <= 0.001f)
        {
            away = Random.insideUnitCircle;
        }

        if (away.sqrMagnitude <= 0.001f)
        {
            away = Vector2.up;
        }

        away.Normalize();
        Vector2 fleePoint = ClampInsideMap((Vector2)transform.position + away * 5f, 1f);
        owner.CommandMoveTo(fleePoint, scavengerMoveSpeedMultiplier * 1.25f);

        if (panicTimer <= 0f && Vector2.Distance(transform.position, player.position) > scavengerPanicRange * 1.5f)
        {
            owner.RequestState(EnemyState.Patrol);
        }
    }

    private void UpdateIdlePatrol(EnemyBaseAI owner, Vector2 center, float radius)
    {
        if (Vector2.Distance(transform.position, rolePatrolTarget) <= idleArriveDistance ||
            Vector2.Distance(rolePatrolTarget, center) > radius * 1.25f)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.1f, radius);
            rolePatrolTarget = ClampInsideMap(center + offset, 0.75f);
        }

        owner.CommandMoveTo(rolePatrolTarget, 1f);
    }

    private bool ShouldRivalEscape()
    {
        if (roleType != EnemyRoleType.RivalHarvester)
        {
            return false;
        }

        if (health != null && health.HpRatio <= rivalFleeHealthRatio)
        {
            return true;
        }

        return cargoHold != null && cargoHold.FillRatio >= rivalEscapeCargoRatio;
    }

    private void BeginEscape(EnemyBaseAI owner)
    {
        if (currentPhase == EnemyRolePhase.PreparingEscape || currentPhase == EnemyRolePhase.Fleeing)
        {
            return;
        }

        currentPhase = EnemyRolePhase.PreparingEscape;
        escapePreparationTimer = Mathf.Max(0f, escapePreparationDuration);
        cargoDepositTimer = Mathf.Max(0f, cargoDepositDuration);
        cargoDepositStarted = false;
        ReleaseHarvestTarget();
        ReleaseRewardPickupTarget();
        SetHarvestBeamVisible(false);

        returningCargoToBase = TryResolveCargoReturnBase();
        leavingCargoBase = false;
        PrepareCargoRoute();

        escapeTarget = returningCargoToBase
            ? ResolveCurrentCargoRouteTarget()
            : CalculateEscapeTarget();

        if (owner != null)
        {
            owner.CancelCurrentAttack();
            owner.CommandStopMoving();
            owner.CommandFaceTo(escapeTarget);
        }

        NotifyRoleActivity(returningCargoToBase ? cargoReturnWarning : "적 화물선 이탈 준비");
        Log(returningCargoToBase ? "Cargo return preparation started." : "Escape preparation started.");
    }

    private void UpdateEscapePreparation(EnemyBaseAI owner, float deltaTime)
    {
        if (owner == null)
        {
            return;
        }

        owner.CommandStopMoving();
        owner.CommandFaceTo(escapeTarget);

        if (RefreshSimulationLevel() != EnemyRoleSimulationLevel.Active || !TryAcquireIrreversibleSlot())
        {
            return;
        }

        escapePreparationTimer -= deltaTime;

        if (escapePreparationTimer > 0f)
        {
            return;
        }

        currentPhase = EnemyRolePhase.Fleeing;
        Log(returningCargoToBase ? "Cargo return started." : "Escape started.");
    }

    private void UpdateEscape(EnemyBaseAI owner)
    {
        if (owner == null)
        {
            return;
        }

        if (leavingCargoBase)
        {
            UpdateCargoBaseExit(owner);
            return;
        }

        if (returningCargoToBase)
        {
            UpdateCargoReturn(owner);
            return;
        }

        if (RefreshSimulationLevel() != EnemyRoleSimulationLevel.Active || !TryAcquireIrreversibleSlot())
        {
            Vector2 waitingPoint = ClampInsideMap(escapeTarget, 1f);
            owner.CommandMoveTo(waitingPoint, escapeSpeedMultiplier);
            return;
        }

        owner.CommandMoveTo(escapeTarget, escapeSpeedMultiplier);

        bool reached = Vector2.Distance(transform.position, escapeTarget) <= escapeArrivalDistance;
        bool outside = hasMapBounds && !mapBounds.Contains(transform.position);

        if (!reached && !outside)
        {
            return;
        }

        if (cargoHold != null)
        {
            cargoHold.ClearWithoutDrop();
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        ReleaseIrreversibleSlot();
        ReleaseSelf();
    }

    private void UpdateCargoReturn(EnemyBaseAI owner)
    {
        if (cargoHold == null || !cargoHold.HasCargo)
        {
            CompleteCargoReturn(owner);
            return;
        }

        if (homeBase == null || !homeBase.isActiveAndEnabled)
        {
            if (!TryResolveCargoReturnBase())
            {
                SwitchToWorldEscape(owner);
                return;
            }

            PrepareCargoRoute();
        }

        Transform depositPoint = homeBase.ResourceDepositPoint;
        if (depositPoint == null)
        {
            SwitchToWorldEscape(owner);
            return;
        }

        if (activeCargoRoute == null && useBaseCargoRoutes)
        {
            PrepareCargoRoute();
        }

        if (UpdateCargoRouteForward(owner))
        {
            return;
        }

        escapeTarget = depositPoint.position;
        float arrivalDistance = Mathf.Max(escapeArrivalDistance, homeBase.ResourceDepositArrivalDistance);
        float distance = Vector2.Distance(transform.position, escapeTarget);

        if (distance > arrivalDistance)
        {
            cargoDepositStarted = false;
            cargoDepositTimer = Mathf.Max(0f, cargoDepositDuration);
            owner.CommandMoveTo(escapeTarget, escapeSpeedMultiplier);
            return;
        }

        owner.CommandStopMoving();
        owner.CommandFaceTo(escapeTarget);

        if (RefreshSimulationLevel() != EnemyRoleSimulationLevel.Active || !TryAcquireIrreversibleSlot())
        {
            return;
        }

        if (!cargoDepositStarted)
        {
            cargoDepositStarted = true;
            cargoDepositTimer = Mathf.Max(0f, cargoDepositDuration);
        }

        cargoDepositTimer -= Time.deltaTime;
        if (cargoDepositTimer > 0f)
        {
            return;
        }

        bool deposited = homeBase.TryDepositCargo(cargoHold, out int depositedWeight);
        if (deposited && depositedWeight > 0)
        {
            NotifyRoleActivity(cargoDepositWarning);
        }

        if (!cargoHold.HasCargo)
        {
            CompleteCargoReturn(owner);
            return;
        }

        // 지정 기지가 가득 찼으면 남은 화물을 들고 기존 해역 이탈로 전환한다.
        if (!homeBase.CanAcceptCargo(cargoHold))
        {
            SwitchToWorldEscape(owner);
            return;
        }

        cargoDepositStarted = false;
        cargoDepositTimer = Mathf.Max(0f, cargoDepositDuration);
    }

    private void PrepareCargoRoute()
    {
        activeCargoRoute = null;
        cargoRouteWaypointIndex = 0;

        if (!returningCargoToBase || !useBaseCargoRoutes || homeBase == null)
        {
            return;
        }

        homeBase.TryGetClosestCargoRoute(transform.position, out activeCargoRoute);
    }

    private Vector2 ResolveCurrentCargoRouteTarget()
    {
        if (activeCargoRoute != null && activeCargoRoute.IsValid)
        {
            Transform waypoint = activeCargoRoute.GetWaypoint(cargoRouteWaypointIndex);
            if (waypoint != null)
            {
                return waypoint.position;
            }
        }

        return ResolveHomeBaseDepositPosition();
    }

    /// <summary>
    /// true를 반환하면 아직 경로 Waypoint를 따라가는 중입니다.
    /// </summary>
    private bool UpdateCargoRouteForward(EnemyBaseAI owner)
    {
        if (owner == null || activeCargoRoute == null || !activeCargoRoute.IsValid)
        {
            return false;
        }

        while (cargoRouteWaypointIndex < activeCargoRoute.WaypointCount)
        {
            Transform waypoint = activeCargoRoute.GetWaypoint(cargoRouteWaypointIndex);

            if (waypoint == null)
            {
                cargoRouteWaypointIndex++;
                continue;
            }

            float arrivalDistance = activeCargoRoute.WaypointArrivalDistance;
            float distance = Vector2.Distance(transform.position, waypoint.position);

            if (distance > arrivalDistance)
            {
                escapeTarget = waypoint.position;
                owner.CommandMoveTo(waypoint.position, escapeSpeedMultiplier);
                return true;
            }

            cargoRouteWaypointIndex++;
        }

        return false;
    }

    private bool TryResolveCargoReturnBase()
    {
        if (!returnCargoToBase || cargoHold == null || !cargoHold.HasCargo)
        {
            return false;
        }

        if (homeBase != null)
        {
            return homeBase.isActiveAndEnabled && homeBase.CanAcceptCargo(cargoHold);
        }

        if (!autoFindNearestBaseWhenMissing)
        {
            return false;
        }

        homeBase = FieldBaseController.FindClosestResourceBase(transform.position);
        return homeBase != null && homeBase.CanAcceptCargo(cargoHold);
    }

    private Vector2 ResolveHomeBaseDepositPosition()
    {
        if (homeBase == null)
        {
            return transform.position;
        }

        Transform point = homeBase.ResourceDepositPoint;
        return point != null ? (Vector2)point.position : (Vector2)homeBase.transform.position;
    }

    private void CompleteCargoReturn(EnemyBaseAI owner)
    {
        returningCargoToBase = false;
        cargoDepositStarted = false;
        cargoDepositTimer = 0f;
        escapePreparationTimer = 0f;
        harvestedObjectCount = 0;
        noTargetTimer = 0f;
        targetSearchTimer = 0f;
        ReleaseIrreversibleSlot();

        if (activeCargoRoute != null && activeCargoRoute.IsValid && activeCargoRoute.WaypointCount > 0)
        {
            leavingCargoBase = true;
            cargoRouteWaypointIndex = activeCargoRoute.WaypointCount - 1;
            currentPhase = EnemyRolePhase.Fleeing;

            Transform exitWaypoint = activeCargoRoute.GetWaypoint(cargoRouteWaypointIndex);
            if (owner != null && exitWaypoint != null)
            {
                owner.CommandFaceTo(exitWaypoint.position);
            }

            Log("Cargo deposited. Leaving base through cargo route.");
            return;
        }

        ResumeRoleAfterCargoReturn(owner);
    }

    private void ResumeRoleAfterCargoReturn(EnemyBaseAI owner)
    {
        leavingCargoBase = false;
        activeCargoRoute = null;
        cargoRouteWaypointIndex = 0;

        currentPhase = roleType == EnemyRoleType.Scavenger
            ? EnemyRolePhase.SeekingLoot
            : EnemyRolePhase.SeekingHarvest;

        if (owner != null)
        {
            owner.SetHomePosition(transform.position, false);
            owner.RequestState(EnemyState.Patrol);
        }

        Log("Cargo deposited. Role resumed.");
    }

    private void UpdateCargoBaseExit(EnemyBaseAI owner)
    {
        if (owner == null)
        {
            return;
        }

        if (activeCargoRoute == null || !activeCargoRoute.IsValid)
        {
            ResumeRoleAfterCargoReturn(owner);
            return;
        }

        while (cargoRouteWaypointIndex >= 0)
        {
            Transform waypoint = activeCargoRoute.GetWaypoint(cargoRouteWaypointIndex);

            if (waypoint == null)
            {
                cargoRouteWaypointIndex--;
                continue;
            }

            float arrivalDistance = activeCargoRoute.WaypointArrivalDistance;
            float distance = Vector2.Distance(transform.position, waypoint.position);

            if (distance > arrivalDistance)
            {
                owner.CommandMoveTo(waypoint.position, escapeSpeedMultiplier);
                return;
            }

            cargoRouteWaypointIndex--;
        }

        ResumeRoleAfterCargoReturn(owner);
    }

    private void SwitchToWorldEscape(EnemyBaseAI owner)
    {
        returningCargoToBase = false;
        cargoDepositStarted = false;
        cargoDepositTimer = 0f;
        leavingCargoBase = false;
        activeCargoRoute = null;
        cargoRouteWaypointIndex = 0;
        escapeTarget = CalculateEscapeTarget();
        currentPhase = EnemyRolePhase.Fleeing;

        if (owner != null)
        {
            owner.CommandFaceTo(escapeTarget);
        }

        Log("Cargo base unavailable. Switched to world escape.");
    }

    private Vector2 CalculateEscapeTarget()
    {
        Vector2 current = transform.position;

        if (!hasMapBounds)
        {
            Vector2 direction = current.sqrMagnitude > 0.001f ? current.normalized : Vector2.right;
            return current + direction * 30f;
        }

        float leftDistance = Mathf.Abs(current.x - mapBounds.min.x);
        float rightDistance = Mathf.Abs(mapBounds.max.x - current.x);
        float bottomDistance = Mathf.Abs(current.y - mapBounds.min.y);
        float topDistance = Mathf.Abs(mapBounds.max.y - current.y);

        float minDistance = Mathf.Min(leftDistance, rightDistance, bottomDistance, topDistance);

        if (Mathf.Approximately(minDistance, leftDistance))
        {
            return new Vector2(mapBounds.min.x - escapeOutsidePadding, current.y);
        }

        if (Mathf.Approximately(minDistance, rightDistance))
        {
            return new Vector2(mapBounds.max.x + escapeOutsidePadding, current.y);
        }

        if (Mathf.Approximately(minDistance, bottomDistance))
        {
            return new Vector2(current.x, mapBounds.min.y - escapeOutsidePadding);
        }

        return new Vector2(current.x, mapBounds.max.y + escapeOutsidePadding);
    }

    private void TryAcquireHarvestTarget()
    {
        CleanupReservations();

        HarvestObjectHealth best = null;
        float bestScore = float.MaxValue;

        foreach (HarvestObjectHealth candidate in HarvestObjectHealth.ActiveObjects)
        {

            if (candidate == null || candidate.IsDead || ReservedHarvestTargets.Contains(candidate))
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.transform.position);

            if (distance > harvestSearchRadius)
            {
                continue;
            }

            float priorityBonus = candidate.ObjectKind switch
            {
                HarvestObjectKind.HighValueWreck => -10f,
                HarvestObjectKind.DestroyedHull => -5f,
                _ => 0f
            };

            float score = distance + priorityBonus;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            return;
        }

        harvestTarget = best;
        ReservedHarvestTargets.Add(harvestTarget);
        harvestTarget.Died += HandleHarvestTargetDied;
        harvestWarmupTimer = 0f;
        harvestTickTimer = 0f;
        harvestStartNotified = false;
        currentPhase = EnemyRolePhase.SeekingHarvest;
        Log($"Harvest target: {harvestTarget.name}");
    }

    private void TryAcquireRewardPickup(Vector2 center, float radius, bool prioritizeValuableCargo)
    {
        CleanupReservations();

        RewardPickup best = null;
        float bestScore = float.MaxValue;

        foreach (RewardPickup candidate in RewardPickup.ActivePickups)
        {

            if (candidate == null ||
                !candidate.CanBeTakenByEnemy ||
                candidate.PickupKind != RewardPickupKind.Currency ||
                ReservedRewardPickups.Contains(candidate))
            {
                continue;
            }

            if (cargoHold != null && !cargoHold.CanStore(candidate.CurrencyType))
            {
                continue;
            }

            float distance = Vector2.Distance(center, candidate.transform.position);

            if (distance > radius)
            {
                continue;
            }

            float valueBonus = 0f;

            if (prioritizeValuableCargo)
            {
                valueBonus = candidate.CurrencyType switch
                {
                    CurrencyType.CoreShards => -8f,
                    CurrencyType.ScrapParts => -4f,
                    CurrencyType.Credits => -1f,
                    _ => 0f
                };
            }

            float score = distance + valueBonus;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            return;
        }

        rewardPickupTarget = best;
        channelingPickup = best;
        pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
        ReservedRewardPickups.Add(best);
    }

    private void ReleaseHarvestTarget()
    {
        if (harvestTarget != null)
        {
            harvestTarget.Died -= HandleHarvestTargetDied;
            ReservedHarvestTargets.Remove(harvestTarget);
        }

        harvestTarget = null;
        harvestWarmupTimer = 0f;
        harvestTickTimer = 0f;
        harvestStartNotified = false;
        SetHarvestBeamVisible(false);
    }

    private void ReleaseRewardPickupTarget()
    {
        if (rewardPickupTarget != null)
        {
            ReservedRewardPickups.Remove(rewardPickupTarget);
        }

        rewardPickupTarget = null;
        channelingPickup = null;
        pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
    }

    private void HandleHarvestTargetDied(HarvestObjectHealth target)
    {
        if (target != null)
        {
            lastHarvestPosition = target.transform.position;
        }

        ReleaseHarvestTarget();
        harvestedObjectCount++;
        currentPhase = EnemyRolePhase.CollectingLoot;
        collectTimer = Mathf.Max(0.1f, collectLootDuration);
        targetSearchTimer = 0f;
        Log("Harvest complete. Collecting dropped cargo.");
    }

    private void SubscribeProtectedTarget()
    {
        if (protectedTarget == null)
        {
            return;
        }

        protectedHarvestTarget = protectedTarget.GetComponent<HarvestObjectHealth>();

        if (protectedHarvestTarget == null)
        {
            protectedHarvestTarget = protectedTarget.GetComponentInParent<HarvestObjectHealth>();
        }

        if (protectedHarvestTarget == null)
        {
            return;
        }

        protectedHarvestTarget.Damaged += HandleProtectedTargetDamaged;
        protectedHarvestTarget.Died += HandleProtectedTargetDied;
    }

    private void UnsubscribeProtectedTarget()
    {
        if (protectedHarvestTarget != null)
        {
            protectedHarvestTarget.Damaged -= HandleProtectedTargetDamaged;
            protectedHarvestTarget.Died -= HandleProtectedTargetDied;
        }

        protectedHarvestTarget = null;
    }

    private void HandleProtectedTargetDamaged(HarvestObjectHealth _)
    {
        if (roleType != EnemyRoleType.Defender || ai == null || ai.CurrentState == EnemyState.Dead)
        {
            return;
        }

        if (ai.Player != null && protectedTarget != null &&
            Vector2.Distance(ai.Player.position, protectedTarget.position) <= defenderDisengageRadius)
        {
            ai.AlertTo(ai.Player.position);
        }
        else if (protectedTarget != null)
        {
            ai.AlertTo(protectedTarget.position);
        }
    }

    private void HandleProtectedTargetDied(HarvestObjectHealth _)
    {
        Vector2 lastPosition = protectedTarget != null ? protectedTarget.position : (Vector2)transform.position;
        UnsubscribeProtectedTarget();
        protectedTarget = null;

        if (ai != null)
        {
            ai.SetHomePosition(lastPosition, true);
        }
    }

    private void HandleDamaged(EnemyHealth _)
    {
        if (simulationGate != null)
        {
            simulationGate.ForceActive();
        }

        if (roleType == EnemyRoleType.Scavenger)
        {
            panicTimer = scavengerPanicDuration;

            if (ai != null && ai.CurrentState != EnemyState.Dead)
            {
                ai.RequestState(EnemyState.Combat);
            }
        }
        else if (roleType == EnemyRoleType.RivalHarvester && ShouldRivalEscape())
        {
            BeginEscape(ai);
        }
    }

    private void HandleDied(EnemyHealth _)
    {
        ReleaseHarvestTarget();
        ReleaseRewardPickupTarget();
        SetHarvestBeamVisible(false);
        ReleaseIrreversibleSlot();
    }

    private void EnsureCargoHold(int capacity)
    {
        ResolveReferences();

        if (cargoHold == null)
        {
            cargoHold = gameObject.AddComponent<EnemyCargoHold>();
        }

        cargoHold.Configure(
            capacity,
            false,
            true,
            true,
            true
        );
    }

    private void ResolveReferences()
    {
        if (ai == null)
        {
            ai = GetComponent<EnemyBaseAI>();
        }

        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }

        if (cargoHold == null)
        {
            cargoHold = GetComponent<EnemyCargoHold>();
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (simulationGate == null)
        {
            simulationGate = GetComponent<EnemyRoleSimulationGate>();

            if (simulationGate == null)
            {
                simulationGate = gameObject.AddComponent<EnemyRoleSimulationGate>();
            }
        }

        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }
    }

    private EnemyRoleSimulationLevel RefreshSimulationLevel()
    {
        if (simulationGate == null)
        {
            return EnemyRoleSimulationLevel.Active;
        }

        return simulationGate.RefreshState();
    }

    private bool TryAcquireIrreversibleSlot()
    {
        return simulationGate == null || simulationGate.TryAcquireIrreversibleSlot();
    }

    private void ReleaseIrreversibleSlot()
    {
        if (simulationGate != null)
        {
            simulationGate.ReleaseIrreversibleSlot();
        }
    }

    private bool UpdatePickupCollectionChannel(
        EnemyBaseAI owner,
        float deltaTime,
        float moveSpeedMultiplier,
        bool notifyOnCollect,
        bool releaseSlotAfterCollect)
    {
        if (owner == null || rewardPickupTarget == null || !rewardPickupTarget.IsAvailable)
        {
            return false;
        }

        float distance = Vector2.Distance(transform.position, rewardPickupTarget.transform.position);

        if (distance > scavengerCollectRange)
        {
            channelingPickup = rewardPickupTarget;
            pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
            owner.CommandMoveTo(
                rewardPickupTarget.transform.position,
                moveSpeedMultiplier,
                rewardPickupTarget.transform
            );
            return false;
        }

        owner.CommandStopMoving();

        if (!rewardPickupTarget.CanBeTakenByEnemy)
        {
            pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
            return false;
        }

        if (RefreshSimulationLevel() != EnemyRoleSimulationLevel.Active || !TryAcquireIrreversibleSlot())
        {
            pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
            return false;
        }

        if (channelingPickup != rewardPickupTarget)
        {
            channelingPickup = rewardPickupTarget;
            pickupChannelTimer = Mathf.Max(0.05f, pickupCollectChannelDuration);
        }

        pickupChannelTimer -= deltaTime;

        if (pickupChannelTimer > 0f)
        {
            return false;
        }

        bool collected = cargoHold != null && cargoHold.TryCollect(rewardPickupTarget);

        if (collected && notifyOnCollect)
        {
            NotifyRoleActivity("회수물이 탈취되고 있습니다");
        }

        ReleaseRewardPickupTarget();

        if (releaseSlotAfterCollect)
        {
            ReleaseIrreversibleSlot();
        }

        return collected;
    }

    private void NotifyRoleActivity(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || Time.time - lastRoleNotificationTime < roleNotificationCooldown)
        {
            return;
        }

        if (simulationGate != null && !simulationGate.ShouldNotifyPlayer(roleNotificationDistance))
        {
            return;
        }

        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (expeditionHUD == null)
        {
            return;
        }

        lastRoleNotificationTime = Time.time;
        AudioManager.Play(SoundEventIds.WarningMessage, 0.75f);
        expeditionHUD.ShowWarning(message);
    }

    private void SetHarvestBeamVisible(bool value)
    {
        if (harvestBeam != null)
        {
            harvestBeam.enabled = value;
        }
    }

    private void UpdateHarvestBeam(Vector3 targetPosition)
    {
        if (harvestBeam == null)
        {
            return;
        }

        harvestBeam.enabled = true;
        harvestBeam.positionCount = 2;
        harvestBeam.SetPosition(0, transform.position);
        harvestBeam.SetPosition(1, targetPosition);
    }

    private Vector2 ClampInsideMap(Vector2 point, float padding)
    {
        if (!hasMapBounds)
        {
            return point;
        }

        return new Vector2(
            Mathf.Clamp(point.x, mapBounds.min.x + padding, mapBounds.max.x - padding),
            Mathf.Clamp(point.y, mapBounds.min.y + padding, mapBounds.max.y - padding)
        );
    }

    private void CleanupReservations()
    {
        ReservedHarvestTargets.RemoveWhere(target => target == null || target.IsDead);
        ReservedRewardPickups.RemoveWhere(target => target == null || !target.IsAvailable);
    }

    private void ReleaseSelf()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Log(string message)
    {
        if (logRoleFlow)
        {
            Debug.Log($"[{name}/{roleType}] {message}", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (roleType == EnemyRoleType.Defender && protectedTarget != null)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(protectedTarget.position, defenderAggroRadius);

            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(protectedTarget.position, defenderLeashRadius);
        }

        if (roleType == EnemyRoleType.RivalHarvester)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, harvestSearchRadius);
        }

        if (roleType == EnemyRoleType.Scavenger)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, scavengerSearchRadius);
        }
    }
}
