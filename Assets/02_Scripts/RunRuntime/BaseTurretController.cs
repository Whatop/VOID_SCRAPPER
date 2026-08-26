using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAttackController))]
public class BaseTurretController : MonoBehaviour, IPlayerOwnedAlly, IPlayerProjectileHitListener
{
    private const int MaxTrackedDeploymentTaunts = 8;

    [Header("Definition Optional")]
    [Tooltip("연결하면 EnemyAttackController와 EnemyHealth에 동일한 정의를 적용합니다. 비우면 각 컴포넌트의 Fallback 값을 사용합니다.")]
    [SerializeField] private EnemyDefinition turretDefinition;

    [Header("References")]
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private EnemyHealth turretHealth;
    [SerializeField] private Transform headPivot;

    [Header("Fire Points")]
    [FormerlySerializedAs("firePoint")]
    [Tooltip("단발·샷건·차징 포탑이 사용하는 중앙 발사 위치입니다.")]
    [SerializeField] private Transform centerFirePoint;
    [Tooltip("기관총 포탑 점사의 왼쪽 발사 위치입니다.")]
    [SerializeField] private Transform machineGunLeftFirePoint;
    [Tooltip("기관총 포탑 점사의 오른쪽 발사 위치입니다.")]
    [SerializeField] private Transform machineGunRightFirePoint;

    [Tooltip("전력 연결선이 도착할 포인트입니다. 비워두면 포탑 Root를 사용합니다.")]
    [SerializeField] private Transform powerLinkAnchor;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 9f;
    [SerializeField] private bool useAttackRangeAsMinimumDetectionRange = true;
    [SerializeField] private LayerMask lineOfSightBlockMask;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Enemy Base Defense Optional")]
    [SerializeField] private FieldBaseController fieldBaseOwner;
    [SerializeField] private bool exteriorFieldBaseDefense;

    [Header("Neutral Shop Defense Optional")]
    [SerializeField] private ShopStructure shopOwner;
    [Tooltip("중립 상점 포탑이 탐색할 적 레이어입니다. 비어 있으면 모든 레이어에서 EnemyHealth를 필터링합니다.")]
    [SerializeField] private LayerMask neutralEnemyTargetLayer;
    [Min(0.05f)]
    [SerializeField] private float targetRefreshInterval = 0.2f;
    [SerializeField] private bool requireThreateningEnemy = true;

    [Header("Player Deployable Optional")]
    [Min(0f)]
    [SerializeField] private float playerDeploymentOffset = 0.9f;

    [Header("Aim")]
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float headRotationOffset = -90f;
    [SerializeField] private bool requireAimAlignment = true;
    [Range(0.1f, 45f)]
    [SerializeField] private float aimToleranceDegrees = 6f;

    [Header("Power")]
    [SerializeField] private bool poweredOn = true;
    [SerializeField] private GameObject[] activeVisuals;
    [SerializeField] private GameObject[] inactiveVisuals;

    [Header("Debug")]
    [SerializeField] private bool drawDetectionGizmo;

    private readonly RaycastHit2D[] lineOfSightHits = new RaycastHit2D[16];
    private readonly Collider2D[] targetHits = new Collider2D[96];

    private Transform target;
    private PlayerHealth playerTargetHealth;
    private EnemyHealth enemyTargetHealth;
    private EnemyBaseAI enemyTargetAI;
    private float targetRefreshTimer;
    private bool lastTargetPlayerMode;
    private bool targetModeInitialized;
    private bool playerAllied;
    private Transform playerOwner;
    private RadarTarget radarTarget;
    private Collider2D[] deploymentColliders;
    private bool[] deploymentColliderStates;
    private bool playerDeploymentOverridesApplied;
    private bool radarVisibleBeforePlayerDeployment;
    private readonly EnemyBaseAI[] deploymentTauntTargets = new EnemyBaseAI[MaxTrackedDeploymentTaunts];
    private readonly float[] deploymentTauntEndsAt = new float[MaxTrackedDeploymentTaunts];
    private float playerDeploymentTauntDuration;
    private int playerDeploymentMaxTauntTargets;

    public bool PoweredOn => poweredOn;
    public bool IsDestroyed => turretHealth != null && turretHealth.IsDead;
    public Transform PowerLinkAnchor => powerLinkAnchor != null ? powerLinkAnchor : transform;
    public EnemyHealth TurretHealth => turretHealth;
    public ShopStructure ShopOwner => shopOwner;
    public bool IsShopDefense => shopOwner != null;
    public bool IsPlayerAllied => playerAllied;
    public bool IsPlayerOwnedAlly => playerAllied;
    public EnemyDefinition TurretDefinition => turretDefinition;

    public event Action<BaseTurretController, bool> PowerChanged;
    public event Action<BaseTurretController> Destroyed;

    private void Reset()
    {
        attackController = GetComponent<EnemyAttackController>();
        turretHealth = GetComponent<EnemyHealth>();
        headPivot = transform;
        centerFirePoint = transform;
        machineGunLeftFirePoint = null;
        machineGunRightFirePoint = null;
        powerLinkAnchor = transform;
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyDefinitionIfAssigned();
        ConfigureProjectileAllegiance();
        RefreshVisualState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeShopOwner(true);

        if (turretHealth != null)
        {
            turretHealth.Died -= HandleTurretDied;
            turretHealth.Died += HandleTurretDied;
        }

        targetRefreshTimer = 0f;
        ClearTarget();
        ConfigureProjectileAllegiance();
        RefreshRadarAllegiancePresentation();
    }

    private void OnDisable()
    {
        SubscribeShopOwner(false);

        if (turretHealth != null)
        {
            turretHealth.Died -= HandleTurretDied;
        }

        attackController?.CancelCharge();
        ClearTarget();
        RestorePlayerDeploymentOverrides();
        ResetPlayerDeploymentCombat();
        playerAllied = false;
        playerOwner = null;
        targetModeInitialized = false;
    }

    private void Update()
    {
        if (!poweredOn || IsDestroyed)
        {
            return;
        }

        if (shopOwner != null && shopOwner.IsDead)
        {
            SetPowered(false);
            return;
        }

        bool targetPlayerMode = !playerAllied && (shopOwner == null || shopOwner.IsHostile);

        if (!targetModeInitialized || lastTargetPlayerMode != targetPlayerMode)
        {
            targetModeInitialized = true;
            lastTargetPlayerMode = targetPlayerMode;
            ClearTarget();
            ConfigureProjectileAllegiance();
        }

        targetRefreshTimer -= Time.deltaTime;

        if (!IsCurrentTargetValid(targetPlayerMode) || targetRefreshTimer <= 0f)
        {
            targetRefreshTimer = Mathf.Max(0.05f, targetRefreshInterval);
            ResolveTarget(targetPlayerMode);
        }

        if (target == null)
        {
            return;
        }

        Vector2 origin = centerFirePoint != null ? centerFirePoint.position : transform.position;
        Vector2 toTarget = (Vector2)target.position - origin;
        float finalDetectionRange = ResolveDetectionRange();

        if (toTarget.sqrMagnitude > finalDetectionRange * finalDetectionRange)
        {
            ClearTarget();
            return;
        }

        Vector2 direction = toTarget.sqrMagnitude > 0.001f
            ? toTarget.normalized
            : Vector2.up;

        bool aligned = RotateTowards(direction);

        if (requireLineOfSight && !HasLineOfSight(origin, target.position))
        {
            if (attackController != null && attackController.IsCharging)
            {
                attackController.CancelCharge();
            }

            return;
        }

        if (requireAimAlignment && !aligned)
        {
            return;
        }

        attackController?.TryAttack(target);
    }

    public void ConfigureShopDefense(ShopStructure owner)
    {
        RestorePlayerDeploymentOverrides();
        ResetPlayerDeploymentCombat();
        playerAllied = false;
        playerOwner = null;
        SetShopOwner(owner);
        fieldBaseOwner = null;
        exteriorFieldBaseDefense = false;
        targetModeInitialized = false;
        targetRefreshTimer = 0f;
        ClearTarget();
        ConfigureProjectileAllegiance();
        RefreshRadarAllegiancePresentation();
    }

    public void ConfigureFieldBaseDefense(FieldBaseController owner, bool exteriorDefense)
    {
        if (playerAllied || shopOwner != null)
        {
            return;
        }

        fieldBaseOwner = owner;
        exteriorFieldBaseDefense = exteriorDefense;
        targetRefreshTimer = 0f;
        ClearTarget();
        RefreshRadarAllegiancePresentation();
    }

    public void ConfigurePlayerAlly(
        GameObject owner,
        float attackIntervalMultiplier = 1f,
        float damageMultiplier = 1f,
        float tauntDuration = 0f,
        int maxTauntTargets = 0)
    {
        if (owner == null)
        {
            Debug.LogWarning("플레이어 포탑 소유자가 없어 아군 배치를 구성하지 못했습니다.", this);
            return;
        }

        ResetPlayerDeploymentCombat();
        playerAllied = true;
        playerOwner = owner.transform;
        SetShopOwner(null);
        fieldBaseOwner = null;
        exteriorFieldBaseDefense = false;
        targetModeInitialized = false;
        targetRefreshTimer = 0f;
        ClearTarget();

        Vector2 deployDirection = playerOwner.up;

        if (deployDirection.sqrMagnitude <= 0.001f)
        {
            deployDirection = Vector2.up;
        }

        transform.position = (Vector2)playerOwner.position +
                             deployDirection.normalized * Mathf.Max(0f, playerDeploymentOffset);
        attackController?.ConfigurePlayerDeploymentCombat(
            attackIntervalMultiplier,
            damageMultiplier
        );
        playerDeploymentTauntDuration = Mathf.Max(0f, tauntDuration);
        playerDeploymentMaxTauntTargets = Mathf.Clamp(
            maxTauntTargets,
            0,
            MaxTrackedDeploymentTaunts
        );
        ClearDeploymentTauntTracking();
        ApplyPlayerDeploymentOverrides();
        SetPowered(true);
        ConfigureProjectileAllegiance();
        RefreshRadarAllegiancePresentation();
    }

    public void HandlePlayerProjectileHit(EnemyHealth enemyHealth, EnemyBaseAI enemyAI)
    {
        if (!playerAllied ||
            playerDeploymentTauntDuration <= 0f ||
            playerDeploymentMaxTauntTargets <= 0 ||
            enemyHealth == null ||
            enemyHealth.IsDead ||
            enemyAI == null ||
            !enemyAI.IsRadarTauntable ||
            enemyAI.IsShopSecurityUnit ||
            !enemyAI.IsShopSecurityThreat)
        {
            return;
        }

        float now = Time.time;
        int firstFreeIndex = -1;

        for (int i = 0; i < playerDeploymentMaxTauntTargets; i++)
        {
            EnemyBaseAI trackedTarget = deploymentTauntTargets[i];

            if (trackedTarget == enemyAI)
            {
                ApplyDeploymentTaunt(i, enemyAI, now);
                return;
            }

            if (firstFreeIndex < 0 &&
                (trackedTarget == null ||
                 !trackedTarget.gameObject.activeInHierarchy ||
                 trackedTarget.Health == null ||
                 trackedTarget.Health.IsDead ||
                 deploymentTauntEndsAt[i] <= now))
            {
                firstFreeIndex = i;
            }
        }

        if (firstFreeIndex >= 0)
        {
            ApplyDeploymentTaunt(firstFreeIndex, enemyAI, now);
        }
    }

    public void ConfigureDefinition(EnemyDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        turretDefinition = definition;
        ResolveReferences();
        attackController?.CancelCharge();
        ResetPlayerDeploymentCombat();
        attackController?.ApplyDefinition(turretDefinition);
        turretHealth?.ApplyDefinition(turretDefinition);
        targetRefreshTimer = 0f;
        ClearTarget();
        ConfigureProjectileAllegiance();
    }

    public void SetPowered(bool value)
    {
        bool changed = poweredOn != value;
        poweredOn = value;

        if (!poweredOn)
        {
            attackController?.CancelCharge();
            ClearTarget();
        }

        RefreshVisualState();

        if (changed)
        {
            PowerChanged?.Invoke(this, poweredOn);
        }
    }

    private void ResolveReferences()
    {
        if (attackController == null)
        {
            attackController = GetComponent<EnemyAttackController>();
        }

        if (turretHealth == null)
        {
            turretHealth = GetComponent<EnemyHealth>();
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (deploymentColliders == null)
        {
            deploymentColliders = GetComponentsInChildren<Collider2D>(true);
            deploymentColliderStates = new bool[deploymentColliders.Length];
        }

        if (headPivot == null)
        {
            headPivot = transform;
        }

        if (centerFirePoint == null)
        {
            centerFirePoint = headPivot != null ? headPivot : transform;
        }

        attackController?.ConfigureFirePoints(
            centerFirePoint,
            machineGunLeftFirePoint,
            machineGunRightFirePoint
        );
        attackController?.SetProjectileSourceRoot(transform);
    }

    private void SetShopOwner(ShopStructure owner)
    {
        SubscribeShopOwner(false);
        shopOwner = owner;
        SubscribeShopOwner(isActiveAndEnabled);
    }

    private void SubscribeShopOwner(bool subscribe)
    {
        if (shopOwner == null)
        {
            return;
        }

        shopOwner.StateChanged -= HandleShopStateChanged;

        if (subscribe)
        {
            shopOwner.StateChanged += HandleShopStateChanged;
        }
    }

    private void HandleShopStateChanged(ShopStructure _)
    {
        targetModeInitialized = false;
        targetRefreshTimer = 0f;
        ClearTarget();
        ConfigureProjectileAllegiance();
        RefreshRadarAllegiancePresentation();
    }

    private void RefreshRadarAllegiancePresentation()
    {
        if (radarTarget == null)
        {
            return;
        }

        if (playerAllied)
        {
            radarTarget.SetVisible(false);
            return;
        }

        if (shopOwner != null && !shopOwner.IsHostile)
        {
            radarTarget.SetMarkerType(RadarMarkerType.Shop);
            radarTarget.SetVisible(false);
            return;
        }

        radarTarget.SetMarkerType(RadarMarkerType.Enemy);
        radarTarget.SetVisible(true);
    }

    private void ApplyPlayerDeploymentOverrides()
    {
        RestorePlayerDeploymentOverrides();
        playerDeploymentOverridesApplied = true;

        if (radarTarget != null)
        {
            radarVisibleBeforePlayerDeployment = radarTarget.IsRadarVisible;
            radarTarget.SetVisible(false);
        }

        if (deploymentColliders == null || deploymentColliderStates == null)
        {
            return;
        }

        for (int i = 0; i < deploymentColliders.Length; i++)
        {
            Collider2D deploymentCollider = deploymentColliders[i];

            if (deploymentCollider == null)
            {
                continue;
            }

            deploymentColliderStates[i] = deploymentCollider.enabled;

            if (!deploymentCollider.isTrigger)
            {
                deploymentCollider.enabled = false;
            }
        }
    }

    private void RestorePlayerDeploymentOverrides()
    {
        if (!playerDeploymentOverridesApplied)
        {
            return;
        }

        playerDeploymentOverridesApplied = false;

        if (radarTarget != null)
        {
            radarTarget.SetVisible(radarVisibleBeforePlayerDeployment);
        }

        if (deploymentColliders == null || deploymentColliderStates == null)
        {
            return;
        }

        int count = Mathf.Min(deploymentColliders.Length, deploymentColliderStates.Length);

        for (int i = 0; i < count; i++)
        {
            if (deploymentColliders[i] != null)
            {
                deploymentColliders[i].enabled = deploymentColliderStates[i];
            }
        }
    }

    private void ResetPlayerDeploymentCombat()
    {
        attackController?.ResetPlayerDeploymentCombat();
        playerDeploymentTauntDuration = 0f;
        playerDeploymentMaxTauntTargets = 0;
        ClearDeploymentTauntTracking();
    }

    private void ApplyDeploymentTaunt(int index, EnemyBaseAI enemyAI, float now)
    {
        deploymentTauntTargets[index] = enemyAI;
        deploymentTauntEndsAt[index] = now + playerDeploymentTauntDuration;
        enemyAI.ApplyTemporaryAttraction(
            this,
            transform.position,
            playerDeploymentTauntDuration
        );
    }

    private void ClearDeploymentTauntTracking()
    {
        for (int i = 0; i < deploymentTauntTargets.Length; i++)
        {
            deploymentTauntTargets[i] = null;
            deploymentTauntEndsAt[i] = 0f;
        }
    }

    private void ApplyDefinitionIfAssigned()
    {
        if (turretDefinition == null)
        {
            return;
        }

        attackController?.ApplyDefinition(turretDefinition);
        turretHealth?.ApplyDefinition(turretDefinition);
    }

    private void ConfigureProjectileAllegiance()
    {
        if (attackController == null)
        {
            return;
        }

        if (playerAllied)
        {
            attackController.SetProjectileOwner(ProjectileOwner.Player, false);
        }
        else if (shopOwner != null && !shopOwner.IsHostile)
        {
            attackController.SetProjectileOwner(ProjectileOwner.ShopDefense, true);
        }
        else
        {
            attackController.SetProjectileOwner(ProjectileOwner.Enemy, false);
        }
    }

    private float ResolveDetectionRange()
    {
        float range = Mathf.Max(0.1f, detectionRange);

        if (useAttackRangeAsMinimumDetectionRange && attackController != null)
        {
            range = Mathf.Max(range, attackController.AttackRange);
        }

        return range;
    }

    private void ResolveTarget(bool targetPlayerMode)
    {
        if (targetPlayerMode)
        {
            ResolvePlayerTarget();
        }
        else
        {
            ResolveHostileEnemyTarget();
        }
    }

    private void ResolvePlayerTarget()
    {
        if (playerTargetHealth == null || playerTargetHealth.IsDead)
        {
            playerTargetHealth = FindFirstObjectByType<PlayerHealth>();
        }

        enemyTargetHealth = null;
        enemyTargetAI = null;
        bool canTargetPlayer = playerTargetHealth != null &&
                               !playerTargetHealth.IsDead &&
                               CanTargetPlayerPosition(playerTargetHealth.transform.position);

        target = canTargetPlayer ? playerTargetHealth.transform : null;

        if (!canTargetPlayer && attackController != null && attackController.IsCharging)
        {
            attackController.CancelCharge();
        }
    }

    private void ResolveHostileEnemyTarget()
    {
        ClearTarget();

        float range = ResolveDetectionRange();
        int layerMask = neutralEnemyTargetLayer.value == 0
            ? Physics2D.AllLayers
            : neutralEnemyTargetLayer.value;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            range,
            targetHits,
            layerMask
        );

        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = targetHits[i];

            if (hit == null)
            {
                continue;
            }

            EnemyHealth candidateHealth = hit.GetComponentInParent<EnemyHealth>();

            if (candidateHealth == null || candidateHealth.IsDead || candidateHealth == turretHealth)
            {
                continue;
            }

            if (candidateHealth.GetComponentInParent<BaseTurretController>() != null)
            {
                continue;
            }

            EnemyBaseAI candidateAI = candidateHealth.GetComponent<EnemyBaseAI>();

            if (candidateAI == null || candidateAI.IsShopSecurityUnit)
            {
                continue;
            }

            if (requireThreateningEnemy && !candidateAI.IsShopSecurityThreat)
            {
                continue;
            }

            float sqrDistance = ((Vector2)candidateHealth.transform.position - (Vector2)transform.position).sqrMagnitude;

            if (sqrDistance >= bestSqrDistance)
            {
                continue;
            }

            bestSqrDistance = sqrDistance;
            enemyTargetHealth = candidateHealth;
            enemyTargetAI = candidateAI;
        }

        if (enemyTargetHealth != null)
        {
            target = enemyTargetHealth.transform;
        }
    }

    private bool IsCurrentTargetValid(bool targetPlayerMode)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (targetPlayerMode)
        {
            return playerTargetHealth != null &&
                   !playerTargetHealth.IsDead &&
                   CanTargetPlayerPosition(playerTargetHealth.transform.position);
        }

        return enemyTargetHealth != null &&
               !enemyTargetHealth.IsDead &&
               enemyTargetAI != null &&
               !enemyTargetAI.IsShopSecurityUnit &&
               (!requireThreateningEnemy || enemyTargetAI.IsShopSecurityThreat);
    }

    private bool CanTargetPlayerPosition(Vector2 playerPosition)
    {
        return !exteriorFieldBaseDefense ||
               fieldBaseOwner == null ||
               fieldBaseOwner.CanExteriorTurretTargetPlayer(playerPosition);
    }

    private void ClearTarget()
    {
        target = null;
        playerTargetHealth = null;
        enemyTargetHealth = null;
        enemyTargetAI = null;
    }

    private bool RotateTowards(Vector2 direction)
    {
        if (headPivot == null || direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + headRotationOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

        headPivot.rotation = Quaternion.RotateTowards(
            headPivot.rotation,
            targetRotation,
            Mathf.Max(0f, turnSpeed) * Time.deltaTime
        );

        float angleError = Mathf.Abs(Mathf.DeltaAngle(headPivot.eulerAngles.z, targetAngle));
        return angleError <= Mathf.Max(0.1f, aimToleranceDegrees);
    }

    private bool HasLineOfSight(Vector2 origin, Vector2 destination)
    {
        Vector2 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f || lineOfSightBlockMask.value == 0)
        {
            return true;
        }

        direction /= distance;

        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            direction,
            lineOfSightHits,
            distance,
            lineOfSightBlockMask
        );

        float closestBlockingDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = lineOfSightHits[i].collider;

            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            Transform hitTransform = hit.transform;

            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform) ||
                transform.IsChildOf(hitTransform))
            {
                continue;
            }

            if (target != null &&
                (hitTransform == target ||
                 hitTransform.IsChildOf(target) ||
                 target.IsChildOf(hitTransform)))
            {
                continue;
            }

            if (hit.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            closestBlockingDistance = Mathf.Min(
                closestBlockingDistance,
                lineOfSightHits[i].distance
            );
        }

        return closestBlockingDistance == float.MaxValue;
    }

    private void HandleTurretDied(EnemyHealth _)
    {
        SetPowered(false);
        Destroyed?.Invoke(this);
    }

    private void RefreshVisualState()
    {
        SetObjectsActive(activeVisuals, poweredOn);
        SetObjectsActive(inactiveVisuals, !poweredOn);
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDetectionGizmo)
        {
            return;
        }

        Gizmos.color = shopOwner != null && !shopOwner.IsHostile
            ? new Color(0.2f, 1f, 0.65f, 0.8f)
            : Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, detectionRange));
    }
#endif
}
