using UnityEngine;

/// <summary>
/// 넓은 해역에서는 목표를 향해 직진하고, WorldSolid 같은 장애물 앞에서만
/// 좌우 회피 방향을 선택하는 경량 로컬 내비게이션입니다.
/// 전역 A* 대신 수동 기지 경로(FieldBaseCargoRoute2D)와 함께 사용하는 용도입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyNavigationAgent2D : MonoBehaviour
{
    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleMask;
    [Min(0.01f)]
    [SerializeField] private float bodyRadius = 0.28f;
    [Min(0.05f)]
    [SerializeField] private float forwardProbeDistance = 1.4f;
    [Min(0.05f)]
    [SerializeField] private float sideProbeDistance = 1.8f;
    [Range(20f, 88f)]
    [SerializeField] private float avoidanceAngle = 68f;
    [SerializeField] private bool ignoreTriggerColliders = true;

    [Header("Steering")]
    [Range(0f, 2f)]
    [SerializeField] private float targetDirectionWeight = 0.35f;
    [Range(0f, 2f)]
    [SerializeField] private float angledDirectionWeight = 0.75f;
    [Range(0f, 2f)]
    [SerializeField] private float wallFollowWeight = 1.15f;
    [Min(0f)]
    [SerializeField] private float sideHoldDuration = 0.65f;
    [Min(0f)]
    [SerializeField] private float sideSwitchAdvantage = 0.25f;

    [Header("Stuck Recovery")]
    [Min(0.05f)]
    [SerializeField] private float stuckCheckInterval = 0.45f;
    [Min(0f)]
    [SerializeField] private float stuckMoveDistance = 0.05f;
    [Min(0f)]
    [SerializeField] private float stuckSideLockDuration = 0.9f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;

    private readonly RaycastHit2D[] probeHits = new RaycastHit2D[12];

    private int avoidanceSide;
    private float sideLockUntil;
    private float nextStuckCheckTime;
    private Vector2 lastStuckCheckPosition;
    private bool movementCommanded;

    private Vector2 debugTarget;
    private Vector2 debugDirection;
    private bool debugBlocked;

    public LayerMask ObstacleMask => obstacleMask;

    private void OnEnable()
    {
        ResetNavigationState();
    }

    public void ResetNavigationState()
    {
        avoidanceSide = 0;
        sideLockUntil = 0f;
        nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);
        lastStuckCheckPosition = transform.position;
        movementCommanded = false;
        debugBlocked = false;
    }

    public void NotifyMovementStopped()
    {
        movementCommanded = false;
        lastStuckCheckPosition = transform.position;
        nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);
        debugBlocked = false;
    }

    /// <summary>
    /// 목표까지 이동할 때 사용할 정규화 방향을 반환합니다.
    /// ignoredTarget은 HarvestObject처럼 목적지 자체가 obstacleMask에 포함된 경우 전달합니다.
    /// </summary>
    public Vector2 GetSteeringDirection(Vector2 targetPosition, Transform ignoredTarget = null)
    {
        Vector2 origin = transform.position;
        Vector2 toTarget = targetPosition - origin;

        debugTarget = targetPosition;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            debugDirection = Vector2.zero;
            debugBlocked = false;
            return Vector2.zero;
        }

        if (!movementCommanded)
        {
            movementCommanded = true;
            lastStuckCheckPosition = origin;
            nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);
        }

        UpdateStuckRecovery();

        Vector2 directDirection = toTarget.normalized;

        if (obstacleMask.value == 0)
        {
            debugDirection = directDirection;
            debugBlocked = false;
            return directDirection;
        }

        float forwardClearance = ProbeClearance(
            origin,
            directDirection,
            Mathf.Max(0.05f, forwardProbeDistance),
            ignoredTarget
        );

        bool blocked = forwardClearance < Mathf.Max(0.05f, forwardProbeDistance) * 0.98f;
        debugBlocked = blocked;

        if (!blocked)
        {
            if (Time.time >= sideLockUntil)
            {
                avoidanceSide = 0;
            }

            debugDirection = directDirection;
            return directDirection;
        }

        Vector2 leftDirection = Rotate(directDirection, avoidanceAngle);
        Vector2 rightDirection = Rotate(directDirection, -avoidanceAngle);

        float leftClearance = ProbeClearance(
            origin,
            leftDirection,
            Mathf.Max(0.05f, sideProbeDistance),
            ignoredTarget
        );

        float rightClearance = ProbeClearance(
            origin,
            rightDirection,
            Mathf.Max(0.05f, sideProbeDistance),
            ignoredTarget
        );

        ChooseAvoidanceSide(leftClearance, rightClearance);

        int side = avoidanceSide == 0 ? 1 : avoidanceSide;
        Vector2 angledDirection = side > 0 ? leftDirection : rightDirection;
        Vector2 tangentDirection = side > 0
            ? new Vector2(-directDirection.y, directDirection.x)
            : new Vector2(directDirection.y, -directDirection.x);

        float blockRatio = 1f - Mathf.Clamp01(
            forwardClearance / Mathf.Max(0.05f, forwardProbeDistance)
        );

        Vector2 steering =
            directDirection * Mathf.Max(0f, targetDirectionWeight) * (1f - blockRatio * 0.8f) +
            angledDirection * Mathf.Max(0f, angledDirectionWeight) +
            tangentDirection * Mathf.Max(0f, wallFollowWeight) * blockRatio;

        if (steering.sqrMagnitude <= 0.0001f)
        {
            steering = tangentDirection;
        }

        steering.Normalize();
        debugDirection = steering;
        return steering;
    }

    private void ChooseAvoidanceSide(float leftClearance, float rightClearance)
    {
        int bestSide = leftClearance >= rightClearance ? 1 : -1;

        if (avoidanceSide == 0)
        {
            avoidanceSide = bestSide;
            sideLockUntil = Time.time + Mathf.Max(0f, sideHoldDuration);
            return;
        }

        if (Time.time < sideLockUntil)
        {
            return;
        }

        float currentClearance = avoidanceSide > 0 ? leftClearance : rightClearance;
        float oppositeClearance = avoidanceSide > 0 ? rightClearance : leftClearance;

        if (oppositeClearance > currentClearance + Mathf.Max(0f, sideSwitchAdvantage))
        {
            avoidanceSide *= -1;
        }

        sideLockUntil = Time.time + Mathf.Max(0f, sideHoldDuration);
    }

    private void UpdateStuckRecovery()
    {
        if (!movementCommanded || Time.time < nextStuckCheckTime)
        {
            return;
        }

        Vector2 currentPosition = transform.position;
        float movedDistance = Vector2.Distance(currentPosition, lastStuckCheckPosition);

        if (movedDistance <= Mathf.Max(0f, stuckMoveDistance))
        {
            avoidanceSide = avoidanceSide == 0 ? -1 : -avoidanceSide;
            sideLockUntil = Time.time + Mathf.Max(0f, stuckSideLockDuration);
        }

        lastStuckCheckPosition = currentPosition;
        nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);
    }

    private float ProbeClearance(
        Vector2 origin,
        Vector2 direction,
        float distance,
        Transform ignoredTarget)
    {
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0f)
        {
            return distance;
        }

        direction.Normalize();

        int hitCount = Physics2D.CircleCastNonAlloc(
            origin,
            Mathf.Max(0.01f, bodyRadius),
            direction,
            probeHits,
            distance,
            obstacleMask
        );

        float closestDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = probeHits[i].collider;

            if (!IsValidObstacle(hitCollider, ignoredTarget))
            {
                continue;
            }

            closestDistance = Mathf.Min(closestDistance, probeHits[i].distance);
        }

        return closestDistance;
    }

    private bool IsValidObstacle(Collider2D candidate, Transform ignoredTarget)
    {
        if (candidate == null)
        {
            return false;
        }

        if (ignoreTriggerColliders && candidate.isTrigger)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;

        if (candidateTransform == transform ||
            candidateTransform.IsChildOf(transform) ||
            transform.IsChildOf(candidateTransform))
        {
            return false;
        }

        if (ignoredTarget != null &&
            (candidateTransform == ignoredTarget ||
             candidateTransform.IsChildOf(ignoredTarget) ||
             ignoredTarget.IsChildOf(candidateTransform)))
        {
            return false;
        }

        return true;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Vector3 origin = transform.position;

        Gizmos.color = debugBlocked ? Color.red : Color.green;
        Gizmos.DrawLine(origin, origin + (Vector3)(debugDirection * forwardProbeDistance));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(debugTarget, 0.12f);
    }
#endif
}
