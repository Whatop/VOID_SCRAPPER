using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBaseAI : MonoBehaviour
{
    private enum EnemyPurpose
    {
        Scout,
        Ambusher,
        Interceptor,
        Sentinel,
        Boss,
        ShopGuard
    }

    [Header("Definition")]
    [SerializeField] private EnemyDefinition enemyDefinition;
    [SerializeField] private TMP_FontAsset awarenessIndicatorFont;

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Visual Facing")]
    [Tooltip("비워두면 자기 transform을 회전합니다. 권장: VisualRoot 또는 ShipBody를 넣으세요.")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool rotateToFacingDirection = true;
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float turnSpeed = 720f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private float ambusherMoveSpeedMultiplier = 1.15f;
    [SerializeField] private float interceptorMoveSpeedMultiplier = 1.35f;
    [SerializeField] private float sentinelStrafeSpeedMultiplier = 0.75f;

    [Header("Navigation Optional")]
    [Tooltip("붙어 있으면 직선 이동 대신 장애물 회피 방향을 사용합니다. 기지 벽이 있는 적에게 연결하세요.")]
    [SerializeField] private EnemyNavigationAgent2D navigationAgent;
    [SerializeField] private bool useNavigationAgent = true;

    [Header("Detection")]
    [SerializeField] private float visionRange = 9f;
    [SerializeField] private float loseSightTime = 2f;
    [Tooltip("기본 OFF. 켜면 벽 뒤에서도 근거리 레이더 접촉으로 플레이어의 정확한 위치를 추적합니다.")]
    [SerializeField] private bool radarCanTrackExactPlayerPosition;
    [Tooltip("직접 시야를 잃는 순간 진행 중인 차징/점사/돌진 공격을 취소합니다.")]
    [SerializeField] private bool cancelAttackImmediatelyOnSightLoss = true;

    [Header("Alert")]
    [SerializeField] private float alertDuration = 3f;

    [Header("Search")]
    [SerializeField] private float searchDuration = 2.5f;

    [Header("Taunt")]
    [SerializeField] private float defaultTauntDuration = 4f;

    [Header("Combat Distance")]
    [SerializeField] private float preferredCombatDistanceRatio = 0.75f;
    [SerializeField] private float closeCombatDistanceRatio = 0.45f;

    [Header("Open Map Combat Maneuver")]
    [Range(0.1f, 1f)]
    [SerializeField] private float scoutManeuverSpeedMultiplier = 0.7f;
    [Range(0f, 0.8f)]
    [SerializeField] private float scoutApproachLateralBlend = 0.35f;
    [Min(0f)]
    [SerializeField] private float ambusherInterceptLeadTime = 0.4f;
    [Min(0f)]
    [SerializeField] private float ambusherMaximumLeadDistance = 2f;
    [Range(0f, 0.5f)]
    [SerializeField] private float ambusherFlankDistanceRatio = 0.22f;
    [Min(0f)]
    [SerializeField] private float interceptorApproachLeadTime = 0.35f;
    [Min(0f)]
    [SerializeField] private float interceptorMaximumLeadDistance = 1.75f;

    [Header("Charging Enemy Reposition")]
    [Range(0.2f, 0.9f)]
    [SerializeField] private float chargingPreferredDistanceMinRatio = 0.55f;
    [Range(0.3f, 1f)]
    [SerializeField] private float chargingPreferredDistanceMaxRatio = 0.82f;
    [Min(0.1f)]
    [SerializeField] private float chargingRepositionDuration = 1f;
    [Min(0.1f)]
    [SerializeField] private float chargingRepositionSpeedMultiplier = 0.9f;

    [Header("Debug")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;
    [SerializeField] private EnemyPurpose purpose = EnemyPurpose.Scout;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private EnemyAttackController attackController;
    private RadarTarget radarTarget;
    private EnemyRoleController roleController;
    private EnemyVisionSensor visionSensor;
    private EnemyAwarenessIndicator awarenessIndicator;
    private EnemyMeleeChargeController2D meleeChargeController;
    private Rigidbody2D playerBody;

    private ShopNeutralZone2D activeShopNeutralZone;
    private float shopSecurityThreatTimer;

    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private Vector2 alertTarget;
    private Vector2 lastSeenPlayerPosition;
    private Vector2 tauntTarget;
    private UnityEngine.Object temporaryAttractionSource;

    private Vector2 desiredVelocity;
    private Vector2 facingDirection = Vector2.up;
    private readonly Dictionary<UnityEngine.Object, float> externalMoveSpeedMultipliers =
        new Dictionary<UnityEngine.Object, float>(4);
    private readonly Dictionary<UnityEngine.Object, float> trackingDisruptionExpirations =
        new Dictionary<UnityEngine.Object, float>(2);
    private readonly List<UnityEngine.Object> expiredTrackingDisruptionSources =
        new List<UnityEngine.Object>(2);
    private float effectiveExternalMoveSpeedMultiplier = 1f;
    private bool isPlayerTrackingDisrupted;
    private float nextTrackingDisruptionExpiration;

    private float stateTimer;
    private float lostSightTimer;
    private float strafeTimer;
    private int strafeDirection = 1;
    private float chargingRepositionTimer;
    private int chargingRepositionDirection = 1;
    private bool initialized;

    public EnemyState CurrentState => currentState;
    public EnemyDefinition EnemyDefinition => enemyDefinition;
    public Transform Player => player;
    public EnemyHealth Health => health;
    public EnemyAttackController AttackController => attackController;
    public EnemyRoleController RoleController => roleController;
    public EnemyVisionSensor VisionSensor => visionSensor;
    public Vector2 HomePosition => spawnPosition;
    public float BaseMoveSpeed => moveSpeed;
    public float ExternalMoveSpeedMultiplier => effectiveExternalMoveSpeedMultiplier;
    public Vector2 DesiredVelocity => desiredVelocity;
    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => desiredVelocity.sqrMagnitude > 0.01f;
    public bool IsShopSecurityUnit =>
        enemyDefinition != null && enemyDefinition.EnemyType == EnemyType.ShopDrone;
    public bool IsInsideActiveShopNeutralZone =>
        activeShopNeutralZone != null && activeShopNeutralZone.IsActiveSafeZone;
    public bool IsShopSecurityThreat =>
        !IsShopSecurityUnit &&
        (shopSecurityThreatTimer > 0f || IsThreateningPlayer());

    public bool IsAware =>
        currentState == EnemyState.Alert ||
        currentState == EnemyState.Combat ||
        currentState == EnemyState.Search ||
        currentState == EnemyState.Taunt;

    public bool IsRadarTauntable =>
        currentState != EnemyState.Dead &&
        purpose != EnemyPurpose.Boss &&
        purpose != EnemyPurpose.ShopGuard;
    public bool CanReceiveExternalMovementControl =>
        currentState != EnemyState.Dead &&
        purpose != EnemyPurpose.Boss &&
        purpose != EnemyPurpose.ShopGuard;
    public bool CanReceiveTrackingDisruption =>
        currentState != EnemyState.Dead &&
        purpose != EnemyPurpose.Boss &&
        purpose != EnemyPurpose.ShopGuard &&
        !IsInsideActiveShopNeutralZone &&
        IsAware;
    public bool IsPlayerTrackingDisrupted => isPlayerTrackingDisrupted;

    public event Action<EnemyState, EnemyState> StateChanged;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        attackController = GetComponent<EnemyAttackController>();
        radarTarget = GetComponent<RadarTarget>();
        roleController = GetComponent<EnemyRoleController>();
        visionSensor = GetComponent<EnemyVisionSensor>();
        awarenessIndicator = GetComponent<EnemyAwarenessIndicator>();
        navigationAgent = GetComponent<EnemyNavigationAgent2D>();
        meleeChargeController = GetComponent<EnemyMeleeChargeController2D>();
        visualRoot = transform;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        attackController = GetComponent<EnemyAttackController>();
        radarTarget = GetComponent<RadarTarget>();
        roleController = GetComponent<EnemyRoleController>();
        visionSensor = GetComponent<EnemyVisionSensor>();
        awarenessIndicator = GetComponent<EnemyAwarenessIndicator>();
        navigationAgent = GetComponent<EnemyNavigationAgent2D>();
        meleeChargeController = GetComponent<EnemyMeleeChargeController2D>();

        if (visionSensor == null)
        {
            visionSensor = gameObject.AddComponent<EnemyVisionSensor>();
        }

        if (awarenessIndicator == null)
        {
            awarenessIndicator = gameObject.AddComponent<EnemyAwarenessIndicator>();
        }

        awarenessIndicator.ConfigureFont(awarenessIndicatorFont);

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDied;
        }

        if (attackController != null)
        {
            attackController.ProjectileFired += HandleProjectileFired;
        }

        spawnPosition = transform.position;
        desiredVelocity = Vector2.zero;
        navigationAgent?.ResetNavigationState();
        lostSightTimer = 0f;
        stateTimer = 0f;
        temporaryAttractionSource = null;
        ClearAllExternalMoveSpeedMultipliers();
        ClearAllTrackingDisruptions();
        strafeTimer = 0f;
        chargingRepositionTimer = 0f;
        chargingRepositionDirection = UnityEngine.Random.value >= 0.5f ? 1 : -1;
        shopSecurityThreatTimer = 0f;
        activeShopNeutralZone = null;
        initialized = true;

        ResolvePlayer();

        if (visionSensor != null)
        {
            visionSensor.SetTarget(player);
        }

        if (enemyDefinition != null)
        {
            ApplyDefinition(enemyDefinition);
        }

        SetState(EnemyState.Patrol, true);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }

        if (attackController != null)
        {
            attackController.ProjectileFired -= HandleProjectileFired;
        }

        meleeChargeController?.CancelAttack();
        temporaryAttractionSource = null;
        ClearAllExternalMoveSpeedMultipliers();
        ClearAllTrackingDisruptions();
        activeShopNeutralZone = null;
        shopSecurityThreatTimer = 0f;
        StopMoving();
        initialized = false;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (health != null && health.IsDead)
        {
            SetState(EnemyState.Dead);
            return;
        }

        ResolvePlayer();
        RefreshTrackingDisruptions();

        if (shopSecurityThreatTimer > 0f)
        {
            shopSecurityThreatTimer = Mathf.Max(0f, shopSecurityThreatTimer - Time.deltaTime);
        }

        if (activeShopNeutralZone != null && !activeShopNeutralZone.IsActiveSafeZone)
        {
            activeShopNeutralZone = null;
        }

        if (activeShopNeutralZone != null && !IsShopSecurityUnit)
        {
            UpdateShopNeutralZoneRetreat();
            ApplyFacingRotation(Time.deltaTime);
            return;
        }

        if (roleController != null && roleController.TryHandlePriority(this, Time.deltaTime))
        {
            ApplyFacingRotation(Time.deltaTime);
            return;
        }

        if (isPlayerTrackingDisrupted && currentState == EnemyState.Combat)
        {
            CancelCurrentAttack();
            SetState(EnemyState.Search, true);
        }

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            case EnemyState.Alert:
                UpdateAlert();
                break;

            case EnemyState.Combat:
                UpdateCombat();
                break;

            case EnemyState.Search:
                UpdateSearch();
                break;

            case EnemyState.Return:
                UpdateReturn();
                break;

            case EnemyState.Taunt:
                UpdateTaunt();
                break;

            case EnemyState.Dead:
                StopMoving();
                break;
        }

        ApplyFacingRotation(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        if (currentState == EnemyState.Dead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = desiredVelocity * effectiveExternalMoveSpeedMultiplier;
    }

    public void ApplyDefinition(EnemyDefinition definition)
    {
        enemyDefinition = definition;

        if (enemyDefinition == null)
        {
            return;
        }

        moveSpeed = enemyDefinition.MoveSpeed;
        visionRange = enemyDefinition.VisionRange;
        purpose = ResolvePurpose(enemyDefinition.EnemyType);

        if ((enemyDefinition.EnemyType == EnemyType.MeleeCharger ||
             enemyDefinition.EnemyType == EnemyType.ShopDrone) &&
            meleeChargeController == null)
        {
            meleeChargeController = GetComponent<EnemyMeleeChargeController2D>();

            if (meleeChargeController == null)
            {
                meleeChargeController = gameObject.AddComponent<EnemyMeleeChargeController2D>();
            }
        }

        if (health != null)
        {
            health.ApplyDefinition(enemyDefinition);
        }

        if (attackController != null)
        {
            attackController.ApplyDefinition(enemyDefinition);
        }

        if (radarTarget != null)
        {
            radarTarget.SetMarkerType(enemyDefinition.RadarMarkerType);
        }

        if (visionSensor != null)
        {
            visionSensor.ApplyDefinition(enemyDefinition);
            visionSensor.SetTarget(player);
        }

        if (roleController != null)
        {
            roleController.RefreshRadarVisual();
        }

        if (initialized && currentState == EnemyState.Patrol)
        {
            PickNewPatrolTarget();
        }
    }

    public void SetTarget(Transform target)
    {
        player = target;
        CachePlayerBody();

        if (visionSensor != null)
        {
            visionSensor.SetTarget(target);
        }
    }

    public void SetRoleController(EnemyRoleController controller)
    {
        roleController = controller;
    }

    public void SetHomePosition(Vector2 position, bool repickPatrolTarget = true)
    {
        spawnPosition = position;

        if (repickPatrolTarget && currentState == EnemyState.Patrol)
        {
            PickNewPatrolTarget();
        }
    }

    public void CommandMoveTo(
        Vector2 targetPosition,
        float speedMultiplier = 1f,
        Transform ignoredNavigationTarget = null)
    {
        MoveTo(
            targetPosition,
            moveSpeed * Mathf.Max(0f, speedMultiplier),
            ignoredNavigationTarget
        );
    }

    public void CommandStopMoving()
    {
        StopMoving();
    }

    public void CommandFaceTo(Vector2 targetPosition)
    {
        FaceTo(targetPosition);
    }

    public void CommandAttackPlayer()
    {
        TryAttackPlayer();
    }

    public void CancelCurrentAttack()
    {
        if (attackController != null)
        {
            attackController.CancelCharge();
        }

        meleeChargeController?.CancelAttack();
    }

    public void CommandSetVelocity(Vector2 velocity, bool updateFacing = true)
    {
        desiredVelocity = velocity;

        if (updateFacing && velocity.sqrMagnitude > 0.001f)
        {
            SetFacing(velocity.normalized);
        }
    }

    public bool SetExternalMoveSpeedMultiplier(UnityEngine.Object source, float multiplier)
    {
        if (source == null || !CanReceiveExternalMovementControl)
        {
            return false;
        }

        externalMoveSpeedMultipliers[source] = Mathf.Clamp01(multiplier);
        RecalculateExternalMoveSpeedMultiplier();
        return true;
    }

    public bool ClearExternalMoveSpeedMultiplier(UnityEngine.Object source)
    {
        if (source == null || !externalMoveSpeedMultipliers.Remove(source))
        {
            return false;
        }

        RecalculateExternalMoveSpeedMultiplier();
        return true;
    }

    public bool ApplyTrackingDisruption(UnityEngine.Object source, float duration)
    {
        if (source == null || duration <= 0f || !CanReceiveTrackingDisruption)
        {
            return false;
        }

        float requestedExpiration = Time.time + duration;

        if (trackingDisruptionExpirations.TryGetValue(source, out float currentExpiration))
        {
            trackingDisruptionExpirations[source] = Mathf.Max(currentExpiration, requestedExpiration);
        }
        else
        {
            trackingDisruptionExpirations.Add(source, requestedExpiration);
        }

        RecalculateTrackingDisruptionState();
        CancelCurrentAttack();

        if (currentState != EnemyState.Taunt)
        {
            if (currentState == EnemyState.Alert)
            {
                lastSeenPlayerPosition = alertTarget;
            }

            SetState(EnemyState.Search, true);
        }

        return true;
    }

    public bool ClearTrackingDisruption(UnityEngine.Object source)
    {
        if (source == null || !trackingDisruptionExpirations.Remove(source))
        {
            return false;
        }

        RecalculateTrackingDisruptionState();
        return true;
    }

    private void ClearAllExternalMoveSpeedMultipliers()
    {
        externalMoveSpeedMultipliers.Clear();
        effectiveExternalMoveSpeedMultiplier = 1f;
    }

    private void RefreshTrackingDisruptions()
    {
        if (!isPlayerTrackingDisrupted || Time.time < nextTrackingDisruptionExpiration)
        {
            return;
        }

        expiredTrackingDisruptionSources.Clear();
        float now = Time.time;

        foreach (KeyValuePair<UnityEngine.Object, float> disruption in trackingDisruptionExpirations)
        {
            if (disruption.Key == null || disruption.Value <= now)
            {
                expiredTrackingDisruptionSources.Add(disruption.Key);
            }
        }

        for (int i = 0; i < expiredTrackingDisruptionSources.Count; i++)
        {
            trackingDisruptionExpirations.Remove(expiredTrackingDisruptionSources[i]);
        }

        RecalculateTrackingDisruptionState();
    }

    private void ClearAllTrackingDisruptions()
    {
        trackingDisruptionExpirations.Clear();
        expiredTrackingDisruptionSources.Clear();
        isPlayerTrackingDisrupted = false;
        nextTrackingDisruptionExpiration = 0f;
    }

    private void RecalculateTrackingDisruptionState()
    {
        float nextExpiration = float.MaxValue;
        float now = Time.time;

        foreach (KeyValuePair<UnityEngine.Object, float> disruption in trackingDisruptionExpirations)
        {
            if (disruption.Key != null && disruption.Value > now)
            {
                nextExpiration = Mathf.Min(nextExpiration, disruption.Value);
            }
        }

        isPlayerTrackingDisrupted = nextExpiration < float.MaxValue;
        nextTrackingDisruptionExpiration = isPlayerTrackingDisrupted ? nextExpiration : 0f;
    }

    private void RecalculateExternalMoveSpeedMultiplier()
    {
        float strongestSlow = 1f;

        foreach (KeyValuePair<UnityEngine.Object, float> modifier in externalMoveSpeedMultipliers)
        {
            strongestSlow = Mathf.Min(strongestSlow, modifier.Value);
        }

        effectiveExternalMoveSpeedMultiplier = strongestSlow;
    }

    public void CommandSetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        facingDirection = direction.normalized;
    }

    public void EnterShopNeutralZone(ShopNeutralZone2D zone)
    {
        if (zone == null || IsShopSecurityUnit || currentState == EnemyState.Dead)
        {
            return;
        }

        if (IsThreateningPlayer())
        {
            shopSecurityThreatTimer = Mathf.Max(shopSecurityThreatTimer, zone.TurretThreatMemoryDuration);
        }

        activeShopNeutralZone = zone;
        CancelCurrentAttack();
        SetState(EnemyState.Return, true);
    }

    public void ExitShopNeutralZone(ShopNeutralZone2D zone)
    {
        if (zone == null || activeShopNeutralZone != zone)
        {
            return;
        }

        activeShopNeutralZone = null;
        CancelCurrentAttack();

        if (currentState != EnemyState.Dead)
        {
            SetState(EnemyState.Return, true);
        }
    }

    public void RequestState(EnemyState nextState)
    {
        SetState(nextState);
    }

    public void EngagePlayer()
    {
        ResolvePlayer();

        if (currentState == EnemyState.Dead)
        {
            return;
        }

        if (isPlayerTrackingDisrupted)
        {
            CancelCurrentAttack();
            SetState(EnemyState.Search, true);
            return;
        }

        if (player != null)
        {
            lastSeenPlayerPosition = player.position;
            FaceTo(player.position);
        }

        if (visionSensor != null)
        {
            visionSensor.ForceDetectTarget(player);
        }

        SetState(EnemyState.Combat);
    }

    public bool CanSeePlayerForRole()
    {
        return CanSeePlayer() || CanTrackPlayerByRadar();
    }

    public bool CanDirectlySeePlayerForRole()
    {
        return CanSeePlayer();
    }

    public bool CanDetectPlayerByRadarForRole()
    {
        return CanTrackPlayerByRadar();
    }

    public bool CanSuspectPlayerForRole()
    {
        return CanSuspectPlayer();
    }

    public float GetDistanceToPlayerForRole()
    {
        return GetDistanceToPlayer();
    }

    public void NotifyDamagedByPlayer()
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        if (isPlayerTrackingDisrupted)
        {
            CancelCurrentAttack();

            if (currentState != EnemyState.Taunt)
            {
                SetState(EnemyState.Search, true);
            }

            return;
        }

        if (activeShopNeutralZone != null && activeShopNeutralZone.IsActiveSafeZone && !IsShopSecurityUnit)
        {
            shopSecurityThreatTimer = Mathf.Max(
                shopSecurityThreatTimer,
                activeShopNeutralZone.TurretThreatMemoryDuration
            );
            CancelCurrentAttack();
            return;
        }

        if (player != null)
        {
            lastSeenPlayerPosition = player.position;
            FaceTo(player.position);
        }

        if (visionSensor != null)
        {
            visionSensor.ForceDetectTarget(player);
        }

        SetState(EnemyState.Combat);
    }

    public void AlertTo(Vector2 targetPosition)
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        alertTarget = targetPosition;
        FaceTo(alertTarget);
        SetState(EnemyState.Alert);
    }

    public void ApplyTaunt(Vector2 targetPosition, float duration)
    {
        ApplyTemporaryAttraction(this, targetPosition, duration);
    }

    public bool ApplyTemporaryAttraction(
        UnityEngine.Object source,
        Vector2 targetPosition,
        float duration)
    {
        if (source == null || currentState == EnemyState.Dead)
        {
            return false;
        }

        if (!IsRadarTauntable)
        {
            return false;
        }

        temporaryAttractionSource = source;
        tauntTarget = targetPosition;
        stateTimer = duration > 0f ? duration : defaultTauntDuration;
        FaceTo(tauntTarget);
        SetState(EnemyState.Taunt);
        return true;
    }

    public bool ClearTemporaryAttraction(UnityEngine.Object source)
    {
        if (source == null || temporaryAttractionSource != source)
        {
            return false;
        }

        temporaryAttractionSource = null;

        if (currentState == EnemyState.Taunt)
        {
            ResumeAfterTemporaryAttraction();
        }

        return true;
    }

    public void ApplyRadarTaunt(Vector2 scanOrigin)
    {
        ApplyRadarTaunt(this, scanOrigin, 0f);
    }

    public void ApplyRadarTaunt(Vector2 scanOrigin, float durationBonus)
    {
        ApplyRadarTaunt(this, scanOrigin, durationBonus);
    }

    public bool ApplyRadarTaunt(
        UnityEngine.Object source,
        Vector2 scanOrigin,
        float durationBonus)
    {
        return ApplyTemporaryAttraction(
            source,
            scanOrigin,
            defaultTauntDuration + Mathf.Max(0f, durationBonus)
        );
    }

    public void ApplyRadarAlert(Vector2 scanOrigin)
    {
        AlertTo(scanOrigin);
    }

    public bool IsThreateningPlayer()
    {
        if (roleController != null)
        {
            return roleController.CountsAsThreat(currentState);
        }

        return currentState == EnemyState.Alert ||
               currentState == EnemyState.Combat ||
               currentState == EnemyState.Search ||
               currentState == EnemyState.Taunt;
    }

    private void UpdatePatrol()
    {
        if (roleController != null && roleController.TryHandlePatrol(this, Time.deltaTime))
        {
            return;
        }

        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        if (CanSuspectPlayer() && player != null)
        {
            AlertTo(player.position);
            return;
        }

        if (CanTrackPlayerByRadar())
        {
            AlertTo(player.position);
            return;
        }

        if (Vector2.Distance(transform.position, patrolTarget) <= arriveDistance)
        {
            PickNewPatrolTarget();
        }

        MoveTo(patrolTarget, moveSpeed);
    }

    private void UpdateAlert()
    {
        if (!isPlayerTrackingDisrupted &&
            roleController != null &&
            roleController.TryHandleAlert(this, Time.deltaTime))
        {
            return;
        }

        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        if (CanSuspectPlayer() && player != null)
        {
            alertTarget = player.position;
            stateTimer = Mathf.Max(stateTimer, 0.35f);
        }

        if (CanTrackPlayerByRadar() && player != null)
        {
            alertTarget = player.position;
            stateTimer = Mathf.Max(stateTimer, alertDuration * 0.5f);
        }

        stateTimer -= Time.deltaTime;

        FaceTo(alertTarget);
        MoveTo(alertTarget, moveSpeed);

        if (stateTimer <= 0f || Vector2.Distance(transform.position, alertTarget) <= arriveDistance)
        {
            lastSeenPlayerPosition = alertTarget;
            SetState(EnemyState.Search);
        }
    }

    private void UpdateCombat()
    {
        if (roleController != null && roleController.TryHandleCombat(this, Time.deltaTime))
        {
            return;
        }

        if (player == null)
        {
            SetState(EnemyState.Search);
            return;
        }

        bool hasDirectSight = CanSeePlayer();
        bool hasExactRadarTracking = !hasDirectSight && CanTrackPlayerByRadar();

        if (hasDirectSight || hasExactRadarTracking)
        {
            lostSightTimer = 0f;
            lastSeenPlayerPosition = player.position;
            FaceTo(player.position);
        }
        else
        {
            bool committedCharge = attackController != null &&
                                   attackController.IsCommittedCharge;

            if (cancelAttackImmediatelyOnSightLoss && !committedCharge)
            {
                CancelCurrentAttack();
            }

            else if (committedCharge)
            {
                StopMoving();
                return;
            }

            lostSightTimer += Time.deltaTime;
            FaceTo(lastSeenPlayerPosition);

            if (Vector2.Distance(transform.position, lastSeenPlayerPosition) > arriveDistance)
            {
                MoveTo(lastSeenPlayerPosition, moveSpeed);
            }
            else
            {
                StopMoving();
            }

            if (lostSightTimer >= Mathf.Max(0.05f, loseSightTime))
            {
                SetState(EnemyState.Search);
            }

            return;
        }

        if (meleeChargeController != null)
        {
            meleeChargeController.TryHandleCombat(this, player, Time.deltaTime);
            return;
        }

        switch (purpose)
        {
            case EnemyPurpose.Ambusher:
                UpdateAmbusherCombat();
                break;

            case EnemyPurpose.Interceptor:
                UpdateInterceptorCombat();
                break;

            case EnemyPurpose.Sentinel:
            case EnemyPurpose.Boss:
            case EnemyPurpose.ShopGuard:
                UpdateSentinelCombat();
                break;

            default:
                UpdateScoutCombat();
                break;
        }
    }

    private void UpdateScoutCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackRange <= 0f || distanceToPlayer > attackRange)
        {
            MoveTo(player.position, moveSpeed);
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float preferredDistance = Mathf.Max(0.5f, attackRange * preferredCombatDistanceRatio);
        float tooCloseDistance = preferredDistance * 0.7f;
        Vector2 moveDirection;

        if (distanceToPlayer < tooCloseDistance)
        {
            moveDirection = -toPlayer.normalized;
        }
        else
        {
            Vector2 lateralDirection = GetCombatLateralDirection(toPlayer, 0.9f, 1.8f);

            if (distanceToPlayer > preferredDistance * 1.1f)
            {
                float lateralBlend = Mathf.Clamp01(scoutApproachLateralBlend);
                moveDirection = Vector2.Lerp(
                    toPlayer.normalized,
                    lateralDirection,
                    lateralBlend
                ).normalized;
            }
            else
            {
                moveDirection = lateralDirection;
            }
        }

        MoveInCombatDirection(
            moveDirection,
            moveSpeed * Mathf.Clamp(scoutManeuverSpeedMultiplier, 0.1f, 1f)
        );
        TryAttackPlayer();
    }

    private void UpdateAmbusherCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();
        float desiredCloseDistance = Mathf.Max(0.5f, attackRange * closeCombatDistanceRatio);

        if (distanceToPlayer > desiredCloseDistance)
        {
            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            Vector2 lateralDirection = GetCombatLateralDirection(toPlayer, 0.75f, 1.55f);
            Vector2 interceptPosition = GetPlayerInterceptPosition(
                ambusherInterceptLeadTime,
                ambusherMaximumLeadDistance
            );
            float flankDistance = attackRange * Mathf.Clamp(
                ambusherFlankDistanceRatio,
                0f,
                0.5f
            );

            MoveTo(
                interceptPosition + lateralDirection * flankDistance,
                moveSpeed * ambusherMoveSpeedMultiplier
            );
        }
        else
        {
            StopMoving();
        }

        if (attackRange > 0f && distanceToPlayer <= attackRange)
        {
            TryAttackPlayer();
        }
    }

    private void UpdateInterceptorCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackController != null && attackController.IsCharging)
        {
            StopMoving();
            TryAttackPlayer();
            return;
        }

        if (IsRangedChargingEnemy() && chargingRepositionTimer > 0f)
        {
            chargingRepositionTimer = Mathf.Max(
                0f,
                chargingRepositionTimer - Time.deltaTime
            );
            UpdateChargingEnemyReposition(attackRange, distanceToPlayer);
            return;
        }

        if (attackRange > 0f && distanceToPlayer <= attackRange)
        {
            StopMoving();
            TryAttackPlayer();
            return;
        }

        MoveTo(
            GetPlayerInterceptPosition(
                interceptorApproachLeadTime,
                interceptorMaximumLeadDistance
            ),
            moveSpeed * interceptorMoveSpeedMultiplier
        );
    }

    private void UpdateChargingEnemyReposition(float attackRange, float distanceToPlayer)
    {
        if (player == null || attackRange <= 0f)
        {
            StopMoving();
            return;
        }

        float minimumDistance = attackRange * Mathf.Clamp(
            chargingPreferredDistanceMinRatio,
            0.2f,
            0.9f
        );
        float maximumDistance = attackRange * Mathf.Clamp(
            chargingPreferredDistanceMaxRatio,
            chargingPreferredDistanceMinRatio,
            1f
        );
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;

        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            StopMoving();
            return;
        }

        if (distanceToPlayer < minimumDistance)
        {
            Vector2 retreatTarget = (Vector2)transform.position - toPlayer.normalized * 1.5f;
            MoveTo(
                retreatTarget,
                moveSpeed * Mathf.Max(0.1f, chargingRepositionSpeedMultiplier)
            );
        }
        else if (distanceToPlayer > maximumDistance)
        {
            MoveTo(
                GetPlayerInterceptPosition(
                    interceptorApproachLeadTime,
                    interceptorMaximumLeadDistance
                ),
                moveSpeed * Mathf.Max(0.1f, chargingRepositionSpeedMultiplier)
            );
        }
        else
        {
            Vector2 strafeDirection = new Vector2(-toPlayer.y, toPlayer.x).normalized *
                                      chargingRepositionDirection;
            desiredVelocity = strafeDirection * moveSpeed *
                              Mathf.Max(0.1f, chargingRepositionSpeedMultiplier);
            SetFacing(toPlayer);
        }
    }

    private void UpdateSentinelCombat()
    {
        float attackRange = GetAttackRange();
        float distanceToPlayer = GetDistanceToPlayer();

        if (attackRange <= 0f)
        {
            MoveTo(player.position, moveSpeed);
            return;
        }

        float preferredDistance = Mathf.Max(1f, attackRange * preferredCombatDistanceRatio);
        float tooCloseDistance = Mathf.Max(0.5f, preferredDistance * 0.65f);

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        Vector2 moveDirection = Vector2.zero;

        if (distanceToPlayer < tooCloseDistance)
        {
            moveDirection = -toPlayer.normalized;
        }
        else if (distanceToPlayer > preferredDistance)
        {
            moveDirection = toPlayer.normalized;
        }
        else
        {
            strafeTimer -= Time.deltaTime;

            if (strafeTimer <= 0f)
            {
                strafeTimer = UnityEngine.Random.Range(1.0f, 2.2f);
                strafeDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
            }

            moveDirection = new Vector2(-toPlayer.y, toPlayer.x).normalized * strafeDirection;
        }

        if (useNavigationAgent && navigationAgent != null && moveDirection.sqrMagnitude > 0.001f)
        {
            Vector2 steeringTarget = (Vector2)transform.position + moveDirection.normalized * Mathf.Max(1f, moveSpeed);
            Vector2 steeringDirection = navigationAgent.GetSteeringDirection(steeringTarget);

            if (steeringDirection.sqrMagnitude > 0.0001f)
            {
                moveDirection = steeringDirection.normalized;
            }
        }

        desiredVelocity = moveDirection * moveSpeed * sentinelStrafeSpeedMultiplier;

        bool trackPlayerWhileBursting =
            attackController != null &&
            attackController.RangedAttackPattern == EnemyRangedAttackPattern.MachineGunBurst &&
            attackController.IsAttacking;

        if (trackPlayerWhileBursting)
        {
            FaceTo(player.position);
        }
        else if (moveDirection.sqrMagnitude > 0.001f)
        {
            SetFacing(moveDirection);
        }

        if (distanceToPlayer <= attackRange)
        {
            TryAttackPlayer();
        }
    }

    private void UpdateSearch()
    {
        if (roleController != null && roleController.TryHandleSearch(this, Time.deltaTime))
        {
            return;
        }

        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        if (CanSuspectPlayer() && player != null)
        {
            AlertTo(player.position);
            return;
        }

        if (CanTrackPlayerByRadar() && player != null)
        {
            AlertTo(player.position);
            return;
        }

        stateTimer -= Time.deltaTime;

        FaceTo(lastSeenPlayerPosition);

        if (Vector2.Distance(transform.position, lastSeenPlayerPosition) > arriveDistance)
        {
            MoveTo(lastSeenPlayerPosition, moveSpeed);
        }
        else
        {
            StopMoving();
        }

        if (stateTimer <= 0f)
        {
            SetState(EnemyState.Return);
        }
    }

    private void UpdateReturn()
    {
        if (roleController != null && roleController.TryHandleReturn(this, Time.deltaTime))
        {
            return;
        }

        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
            return;
        }

        if (CanSuspectPlayer() && player != null)
        {
            AlertTo(player.position);
            return;
        }

        if (CanTrackPlayerByRadar() && player != null)
        {
            AlertTo(player.position);
            return;
        }

        FaceTo(spawnPosition);
        MoveTo(spawnPosition, moveSpeed);

        if (Vector2.Distance(transform.position, spawnPosition) <= arriveDistance)
        {
            SetState(EnemyState.Patrol);
        }
    }

    private void UpdateTaunt()
    {
        if (roleController != null && roleController.TryHandleTaunt(this, Time.deltaTime))
        {
            return;
        }

        if (CanSeePlayer() && player != null)
        {
            lastSeenPlayerPosition = player.position;
        }

        stateTimer -= Time.deltaTime;

        FaceTo(tauntTarget);
        MoveTo(tauntTarget, moveSpeed * ambusherMoveSpeedMultiplier);

        if (stateTimer <= 0f || Vector2.Distance(transform.position, tauntTarget) <= arriveDistance)
        {
            ResumeAfterTemporaryAttraction();
        }
    }

    private void ResumeAfterTemporaryAttraction()
    {
        temporaryAttractionSource = null;

        if (CanSeePlayer())
        {
            SetState(EnemyState.Combat);
        }
        else
        {
            lastSeenPlayerPosition = tauntTarget;
            SetState(EnemyState.Search);
        }
    }

    private void TryAttackPlayer()
    {
        if (attackController == null || player == null)
        {
            return;
        }

        if (!CanSeePlayer())
        {
            return;
        }

        FaceTo(player.position);
        attackController.TryAttack(player);
    }

    private void SetState(EnemyState nextState, bool force = false)
    {
        if (!force && currentState == nextState)
        {
            return;
        }

        EnemyState previousState = currentState;

        if (previousState == EnemyState.Taunt && nextState != EnemyState.Taunt)
        {
            temporaryAttractionSource = null;
        }

        currentState = nextState;

        switch (currentState)
        {
            case EnemyState.Patrol:
                lostSightTimer = 0f;
                PickNewPatrolTarget();
                break;

            case EnemyState.Alert:
                stateTimer = alertDuration;
                break;

            case EnemyState.Combat:
                lostSightTimer = 0f;

                if (player != null)
                {
                    lastSeenPlayerPosition = player.position;
                    FaceTo(player.position);
                }

                break;

            case EnemyState.Search:
                stateTimer = searchDuration;
                break;

            case EnemyState.Return:
                break;

            case EnemyState.Taunt:
                if (stateTimer <= 0f)
                {
                    stateTimer = defaultTauntDuration;
                }

                break;

            case EnemyState.Dead:
                StopMoving();
                break;
        }

        StateChanged?.Invoke(previousState, currentState);
    }

    private void PickNewPatrolTarget()
    {
        Vector2 target;

        switch (purpose)
        {
            case EnemyPurpose.Ambusher:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius * 0.65f;
                break;

            case EnemyPurpose.Interceptor:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle.normalized * patrolRadius;
                break;

            case EnemyPurpose.Sentinel:
            case EnemyPurpose.ShopGuard:
            case EnemyPurpose.Boss:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius * 0.45f;
                break;

            default:
                target = spawnPosition + UnityEngine.Random.insideUnitCircle * patrolRadius;
                break;
        }

        patrolTarget = target;
        FaceTo(patrolTarget);
    }

    private void MoveTo(
        Vector2 targetPosition,
        float speed,
        Transform ignoredNavigationTarget = null)
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = targetPosition - currentPosition;

        if (direction.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            StopMoving();
            return;
        }

        direction.Normalize();

        if (useNavigationAgent && navigationAgent != null)
        {
            Vector2 steeringDirection = navigationAgent.GetSteeringDirection(
                targetPosition,
                ignoredNavigationTarget
            );

            if (steeringDirection.sqrMagnitude > 0.0001f)
            {
                direction = steeringDirection.normalized;
            }
        }

        desiredVelocity = direction * Mathf.Max(0f, speed);
        SetFacing(direction);
    }

    private void MoveInCombatDirection(Vector2 direction, float speed)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            StopMoving();
            return;
        }

        Vector2 targetPosition = (Vector2)transform.position +
                                 direction.normalized * Mathf.Max(1f, speed);
        MoveTo(targetPosition, speed);
    }

    private Vector2 GetCombatLateralDirection(
        Vector2 toPlayer,
        float minimumDirectionDuration,
        float maximumDirectionDuration)
    {
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        strafeTimer -= Time.deltaTime;

        if (strafeTimer <= 0f)
        {
            float minimum = Mathf.Max(0.1f, minimumDirectionDuration);
            float maximum = Mathf.Max(minimum, maximumDirectionDuration);
            strafeTimer = UnityEngine.Random.Range(minimum, maximum);
            strafeDirection = UnityEngine.Random.value >= 0.5f ? 1 : -1;
        }

        return new Vector2(-toPlayer.y, toPlayer.x).normalized * strafeDirection;
    }

    private Vector2 GetPlayerInterceptPosition(float leadTime, float maximumLeadDistance)
    {
        if (player == null)
        {
            return transform.position;
        }

        Vector2 leadOffset = playerBody != null
            ? playerBody.linearVelocity * Mathf.Max(0f, leadTime)
            : Vector2.zero;
        leadOffset = Vector2.ClampMagnitude(leadOffset, Mathf.Max(0f, maximumLeadDistance));
        return (Vector2)player.position + leadOffset;
    }

    private void StopMoving()
    {
        desiredVelocity = Vector2.zero;
        navigationAgent?.NotifyMovementStopped();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void FaceTo(Vector2 targetPosition)
    {
        if (IsFacingLockedByAttack())
        {
            return;
        }

        Vector2 direction = targetPosition - (Vector2)transform.position;
        SetFacing(direction);
    }

    private void SetFacing(Vector2 direction)
    {
        if (IsFacingLockedByAttack())
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        facingDirection = direction.normalized;
    }
    private bool IsFacingLockedByAttack()
    {
        return (attackController != null && attackController.IsAimDirectionLocked) ||
               (meleeChargeController != null && meleeChargeController.LocksFacing);
    }
    private void ApplyFacingRotation(float deltaTime)
    {
        if (!rotateToFacingDirection)
        {
            return;
        }

        Transform targetRoot = visualRoot != null ? visualRoot : transform;

        if (targetRoot == null)
        {
            return;
        }

        if (facingDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        if (turnSpeed <= 0f)
        {
            targetRoot.rotation = targetRotation;
            return;
        }

        targetRoot.rotation = Quaternion.RotateTowards(
            targetRoot.rotation,
            targetRotation,
            turnSpeed * deltaTime
        );
    }

    private bool CanSeePlayer()
    {
        if (player == null || isPlayerTrackingDisrupted)
        {
            return false;
        }

        if (visionSensor != null)
        {
            return visionSensor.HasConfirmedSight(player);
        }

        float sqrDistance = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
        return sqrDistance <= visionRange * visionRange;
    }

    private bool CanSuspectPlayer()
    {
        return !isPlayerTrackingDisrupted &&
               player != null &&
               visionSensor != null &&
               visionSensor.HasVisualSuspicion;
    }

    private bool HasRadarContact()
    {
        return !isPlayerTrackingDisrupted &&
               player != null &&
               visionSensor != null &&
               visionSensor.HasRadarContact(player);
    }

    private bool CanTrackPlayerByRadar()
    {
        return radarCanTrackExactPlayerPosition && HasRadarContact();
    }

    private float GetDistanceToPlayer()
    {
        if (player == null)
        {
            return float.MaxValue;
        }

        return Vector2.Distance(transform.position, player.position);
    }

    private float GetAttackRange()
    {
        return attackController != null ? attackController.AttackRange : 0f;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            if (playerBody == null)
            {
                CachePlayerBody();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
        {
            player = found.transform;
            CachePlayerBody();

            if (visionSensor != null)
            {
                visionSensor.SetTarget(player);
            }
        }
    }

    private void CachePlayerBody()
    {
        playerBody = player != null ? player.GetComponentInParent<Rigidbody2D>() : null;
    }

    private EnemyPurpose ResolvePurpose(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Shotgun => EnemyPurpose.Ambusher,
            EnemyType.Charging => EnemyPurpose.Interceptor,
            EnemyType.Elite => EnemyPurpose.Sentinel,
            EnemyType.EliteMachineGun => EnemyPurpose.Sentinel,
            EnemyType.EliteShotgun => EnemyPurpose.Ambusher,
            EnemyType.EliteCharging => EnemyPurpose.Interceptor,
            EnemyType.Boss => EnemyPurpose.Boss,
            EnemyType.ShopDrone => EnemyPurpose.ShopGuard,
            EnemyType.MeleeCharger => EnemyPurpose.Ambusher,
            _ => EnemyPurpose.Scout
        };
    }

    private bool IsRangedChargingEnemy()
    {
        if (enemyDefinition == null)
        {
            return false;
        }

        return enemyDefinition.EnemyType == EnemyType.Charging ||
               enemyDefinition.EnemyType == EnemyType.EliteCharging;
    }

    private void HandleProjectileFired(EnemyAttackController source)
    {
        if (source != attackController || !IsRangedChargingEnemy())
        {
            return;
        }

        chargingRepositionTimer = Mathf.Max(0.1f, chargingRepositionDuration);
        chargingRepositionDirection = UnityEngine.Random.value >= 0.5f ? 1 : -1;
    }

    private void HandleDied(EnemyHealth enemyHealth)
    {
        meleeChargeController?.CancelAttack();
        ClearAllTrackingDisruptions();
        activeShopNeutralZone = null;
        SetState(EnemyState.Dead);
    }

    private void UpdateShopNeutralZoneRetreat()
    {
        if (activeShopNeutralZone == null)
        {
            return;
        }

        CancelCurrentAttack();
        Vector2 retreatPoint = activeShopNeutralZone.GetRetreatPoint(transform.position);
        FaceTo(retreatPoint);
        MoveTo(retreatPoint, moveSpeed * activeShopNeutralZone.RetreatSpeedMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        if (visionSensor == null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }

        Gizmos.color = Color.green;
        Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(facingDirection.normalized * 1.5f));
    }
}
