using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAttackController))]
public class BaseTurretController : MonoBehaviour
{
    [Header("Definition Optional")]
    [Tooltip("연결하면 EnemyAttackController와 EnemyHealth에 동일한 정의를 적용합니다. 비우면 각 컴포넌트의 Fallback 값을 사용합니다.")]
    [SerializeField] private EnemyDefinition turretDefinition;

    [Header("References")]
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private EnemyHealth turretHealth;
    [SerializeField] private Transform headPivot;
    [SerializeField] private Transform firePoint;
    [Tooltip("전력 연결선이 도착할 포인트입니다. 비워두면 포탑 Root를 사용합니다.")]
    [SerializeField] private Transform powerLinkAnchor;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 9f;
    [SerializeField] private bool useAttackRangeAsMinimumDetectionRange = true;
    [SerializeField] private LayerMask lineOfSightBlockMask;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Neutral Shop Defense Optional")]
    [SerializeField] private ShopStructure shopOwner;
    [Tooltip("중립 상점 포탑이 탐색할 적 레이어입니다. 비어 있으면 모든 레이어에서 EnemyHealth를 필터링합니다.")]
    [SerializeField] private LayerMask neutralEnemyTargetLayer;
    [Min(0.05f)]
    [SerializeField] private float targetRefreshInterval = 0.2f;
    [SerializeField] private bool requireThreateningEnemy = true;

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

    public bool PoweredOn => poweredOn;
    public bool IsDestroyed => turretHealth != null && turretHealth.IsDead;
    public Transform PowerLinkAnchor => powerLinkAnchor != null ? powerLinkAnchor : transform;
    public EnemyHealth TurretHealth => turretHealth;
    public ShopStructure ShopOwner => shopOwner;
    public bool IsShopDefense => shopOwner != null;

    public event Action<BaseTurretController, bool> PowerChanged;
    public event Action<BaseTurretController> Destroyed;

    private void Reset()
    {
        attackController = GetComponent<EnemyAttackController>();
        turretHealth = GetComponent<EnemyHealth>();
        headPivot = transform;
        firePoint = transform;
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

        if (turretHealth != null)
        {
            turretHealth.Died -= HandleTurretDied;
            turretHealth.Died += HandleTurretDied;
        }

        targetRefreshTimer = 0f;
        ClearTarget();
        ConfigureProjectileAllegiance();
    }

    private void OnDisable()
    {
        if (turretHealth != null)
        {
            turretHealth.Died -= HandleTurretDied;
        }

        attackController?.CancelCharge();
        ClearTarget();
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

        bool targetPlayerMode = shopOwner == null || shopOwner.IsHostile;

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

        Vector2 origin = firePoint != null ? firePoint.position : transform.position;
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
        shopOwner = owner;
        targetModeInitialized = false;
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

        if (headPivot == null)
        {
            headPivot = transform;
        }

        if (firePoint == null)
        {
            firePoint = headPivot != null ? headPivot : transform;
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

        if (shopOwner != null && !shopOwner.IsHostile)
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
        target = playerTargetHealth != null && !playerTargetHealth.IsDead
            ? playerTargetHealth.transform
            : null;
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
            return playerTargetHealth != null && !playerTargetHealth.IsDead;
        }

        return enemyTargetHealth != null &&
               !enemyTargetHealth.IsDead &&
               enemyTargetAI != null &&
               !enemyTargetAI.IsShopSecurityUnit &&
               (!requireThreateningEnemy || enemyTargetAI.IsShopSecurityThreat);
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
