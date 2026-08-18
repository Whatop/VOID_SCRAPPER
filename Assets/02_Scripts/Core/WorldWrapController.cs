using System;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[AddComponentMenu("VOID SCRAPPER/World/Finite World Boundary Controller")]
public class WorldWrapController : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private Vector2 fallbackMapSize = new Vector2(80f, 80f);
    [SerializeField] private Vector2 mapCenter = Vector2.zero;
    [SerializeField] private bool autoUseGeneratedMapBounds = true;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool includeTargetColliderExtents;

    [Header("Boundary Axis")]
    [FormerlySerializedAs("wrapX")]
    [SerializeField] private bool constrainX = true;

    [FormerlySerializedAs("wrapY")]
    [SerializeField] private bool constrainY = true;

    [Header("Boundary Settings")]
    [Tooltip("켜면 MapGenerationConfig의 경계 값을 사용합니다.")]
    [SerializeField] private bool useConfigBoundarySettings = true;

    [SerializeField] private float fallbackWarningDistance = 7f;
    [SerializeField] private float fallbackResistanceDistance = 3f;

    [Range(0f, 0.95f)]
    [SerializeField] private float fallbackMaxOutwardSpeedReduction = 0.65f;

    [SerializeField] private float fallbackInwardPushSpeed = 2.4f;
    [SerializeField] private float fallbackHardInset = 0.35f;
    [SerializeField] private AnimationCurve resistanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Position Clamp")]
    [FormerlySerializedAs("useRigidbodyMovePosition")]
    [SerializeField] private bool useRigidbodyPositionClamp = true;

    [SerializeField] private bool cancelOutwardVelocityAtHardBoundary = true;

    [Header("Warning UI")]
    [SerializeField] private WarningMessageUI warningMessageUI;
    [SerializeField] private bool autoFindWarningMessageUI = true;
    [SerializeField] private string boundaryWarningMessage = "해역 경계에 접근했습니다.";
    [SerializeField] private float warningMessageDuration = 1.1f;
    [SerializeField] private float warningRepeatCooldown = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawBoundaryGizmos = true;
    [SerializeField] private Color warningGizmoColor = new Color(1f, 0.8f, 0.1f, 0.75f);
    [SerializeField] private Color hardBoundaryGizmoColor = new Color(1f, 0.15f, 0.15f, 0.9f);

    private Rigidbody2D targetRb;
    private Collider2D targetCollider;
    private ExpeditionMapGenerator mapGenerator;
    private float lastWarningTime = -999f;
    private bool wasInsideWarningZone;
    private float currentBoundaryPressure;

    public float BoundaryPressure => currentBoundaryPressure;
    public bool IsNearBoundary => currentBoundaryPressure > 0.001f;

    public event Action<Vector2> BoundaryContact;

    [Obsolete("World wrap has been removed. Use BoundaryContact instead.")]
    public event Action<Vector2> Wrapped;

    private void Start()
    {
        ResolveTarget();
        ResolveWarningUI();
    }

    private void FixedUpdate()
    {
        ResolveTarget();

        if (target == null || targetRb == null)
        {
            return;
        }

        ApplyFiniteBoundary(Time.fixedDeltaTime, true);
    }

    private void LateUpdate()
    {
        ResolveTarget();

        if (target == null || targetRb != null)
        {
            return;
        }

        ApplyFiniteBoundary(Time.deltaTime, false);
    }

    private void ApplyFiniteBoundary(float deltaTime, bool useRigidbody)
    {
        if (!IsBoundaryEnabled())
        {
            currentBoundaryPressure = 0f;
            wasInsideWarningZone = false;
            return;
        }

        Vector2 mapSize = ResolveMapSize();
        Vector2 resolvedMapCenter = ResolveMapCenter();
        Vector2 colliderExtents = ResolveTargetColliderExtents();
        float hardInset = ResolveHardInset();

        float halfWidth = Mathf.Max(0.1f, mapSize.x * 0.5f - hardInset - colliderExtents.x);
        float halfHeight = Mathf.Max(0.1f, mapSize.y * 0.5f - hardInset - colliderExtents.y);

        Vector2 position = useRigidbody ? targetRb.position : (Vector2)target.position;
        Vector2 velocity = useRigidbody ? targetRb.linearVelocity : Vector2.zero;
        Vector2 localPosition = position - resolvedMapCenter;

        float resistanceDistance = ResolveResistanceDistance();
        float warningDistance = Mathf.Max(ResolveWarningDistance(), resistanceDistance);
        float maxReduction = ResolveMaxOutwardSpeedReduction();
        float inwardPushSpeed = ResolveInwardPushSpeed();

        float xPressure = 0f;
        float yPressure = 0f;
        bool hardContact = false;
        Vector2 contactNormal = Vector2.zero;

        if (constrainX)
        {
            ApplyAxisBoundary(
                ref localPosition.x,
                ref velocity.x,
                halfWidth,
                resistanceDistance,
                warningDistance,
                maxReduction,
                inwardPushSpeed,
                deltaTime,
                out xPressure,
                out bool xContact,
                out float xNormal
            );

            if (xContact)
            {
                hardContact = true;
                contactNormal.x = xNormal;
            }
        }

        if (constrainY)
        {
            ApplyAxisBoundary(
                ref localPosition.y,
                ref velocity.y,
                halfHeight,
                resistanceDistance,
                warningDistance,
                maxReduction,
                inwardPushSpeed,
                deltaTime,
                out yPressure,
                out bool yContact,
                out float yNormal
            );

            if (yContact)
            {
                hardContact = true;
                contactNormal.y = yNormal;
            }
        }

        currentBoundaryPressure = Mathf.Clamp01(Mathf.Max(xPressure, yPressure));
        bool insideWarningZone = currentBoundaryPressure > 0.001f;

        if (insideWarningZone)
        {
            TryShowBoundaryWarning(!wasInsideWarningZone);
        }

        wasInsideWarningZone = insideWarningZone;

        Vector2 correctedPosition = resolvedMapCenter + localPosition;

        if (useRigidbody)
        {
            targetRb.linearVelocity = velocity;

            if ((targetRb.position - correctedPosition).sqrMagnitude > 0.000001f)
            {
                if (useRigidbodyPositionClamp)
                {
                    targetRb.position = correctedPosition;
                }
                else
                {
                    target.position = new Vector3(correctedPosition.x, correctedPosition.y, target.position.z);
                }
            }
        }
        else
        {
            target.position = new Vector3(correctedPosition.x, correctedPosition.y, target.position.z);
        }

        if (hardContact)
        {
            Vector2 normalized = contactNormal.sqrMagnitude > 0.001f
                ? contactNormal.normalized
                : Vector2.zero;

            BoundaryContact?.Invoke(normalized);
        }
    }

    private void ApplyAxisBoundary(
        ref float localPosition,
        ref float velocity,
        float halfExtent,
        float resistanceDistance,
        float warningDistance,
        float maxReduction,
        float inwardPushSpeed,
        float deltaTime,
        out float warningPressure,
        out bool hardContact,
        out float contactNormal)
    {
        float absPosition = Mathf.Abs(localPosition);
        float distanceToHardEdge = halfExtent - absPosition;
        float outwardSign = localPosition >= 0f ? 1f : -1f;

        warningPressure = warningDistance <= 0f
            ? 0f
            : 1f - Mathf.Clamp01(distanceToHardEdge / warningDistance);

        hardContact = false;
        contactNormal = 0f;

        if (distanceToHardEdge <= resistanceDistance)
        {
            float rawStrength = resistanceDistance <= 0.0001f
                ? 1f
                : 1f - Mathf.Clamp01(distanceToHardEdge / resistanceDistance);

            float strength = resistanceCurve != null
                ? Mathf.Clamp01(resistanceCurve.Evaluate(rawStrength))
                : rawStrength;

            if (velocity * outwardSign > 0f)
            {
                float remainingRatio = 1f - maxReduction * strength;
                velocity *= Mathf.Clamp01(remainingRatio);
            }

            // PlayerController가 매 FixedUpdate마다 속도를 다시 쓰므로,
            // 누적 가속 대신 즉시 체감되는 안쪽 속도 보정을 사용합니다.
            velocity -= outwardSign * inwardPushSpeed * strength;
        }

        if (absPosition <= halfExtent)
        {
            return;
        }

        localPosition = outwardSign * halfExtent;
        hardContact = true;
        contactNormal = -outwardSign;

        if (cancelOutwardVelocityAtHardBoundary && velocity * outwardSign > 0f)
        {
            velocity = 0f;
        }
    }

    private void TryShowBoundaryWarning(bool enteredThisFrame)
    {
        if (string.IsNullOrWhiteSpace(boundaryWarningMessage))
        {
            return;
        }

        float now = Time.unscaledTime;

        if (!enteredThisFrame && now - lastWarningTime < Mathf.Max(0f, warningRepeatCooldown))
        {
            return;
        }

        ResolveWarningUI();

        if (warningMessageUI != null)
        {
            warningMessageUI.ShowCommunication(
                ShipCommunicationChannel.Navigation,
                boundaryWarningMessage,
                ShipCommunicationSeverity.Warning,
                Mathf.Max(0.1f, warningMessageDuration)
            );
        }

        lastWarningTime = now;
    }

    private void ResolveTarget()
    {
        if (target == null && !string.IsNullOrWhiteSpace(targetTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(targetTag);

            if (found != null)
            {
                target = found.transform;
            }
        }

        if (target == null)
        {
            targetRb = null;
            targetCollider = null;
            return;
        }

        if (targetRb == null)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
        }

        if (targetCollider == null)
        {
            targetCollider = target.GetComponent<Collider2D>();
        }
    }

    private void ResolveWarningUI()
    {
        if (warningMessageUI == null && autoFindWarningMessageUI)
        {
            warningMessageUI = FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        }
    }

    private Vector2 ResolveTargetColliderExtents()
    {
        if (!includeTargetColliderExtents || targetCollider == null)
        {
            return Vector2.zero;
        }

        return targetCollider.bounds.extents;
    }

    private bool IsBoundaryEnabled()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.EnableFiniteBoundary;
        }

        return true;
    }

    private Vector2 ResolveMapSize()
    {
        ResolveMapGenerator();

        if (autoUseGeneratedMapBounds &&
            mapGenerator != null &&
            mapGenerator.MapBounds.size.x > 0f &&
            mapGenerator.MapBounds.size.y > 0f)
        {
            return mapGenerator.MapBounds.size;
        }

        if (config != null)
        {
            return config.MapSize;
        }

        return new Vector2(
            Mathf.Max(1f, fallbackMapSize.x),
            Mathf.Max(1f, fallbackMapSize.y)
        );
    }

    private Vector2 ResolveMapCenter()
    {
        ResolveMapGenerator();

        if (autoUseGeneratedMapBounds &&
            mapGenerator != null &&
            mapGenerator.MapBounds.size.x > 0f &&
            mapGenerator.MapBounds.size.y > 0f)
        {
            return mapGenerator.MapBounds.center;
        }

        return mapCenter;
    }

    private void ResolveMapGenerator()
    {
        if (!autoUseGeneratedMapBounds || mapGenerator != null)
        {
            return;
        }

        mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
    }

    private float ResolveWarningDistance()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.BoundaryWarningDistance;
        }

        return Mathf.Max(0f, fallbackWarningDistance);
    }

    private float ResolveResistanceDistance()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.BoundaryResistanceDistance;
        }

        return Mathf.Max(0.05f, fallbackResistanceDistance);
    }

    private float ResolveMaxOutwardSpeedReduction()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.BoundaryMaxOutwardSpeedReduction;
        }

        return Mathf.Clamp(fallbackMaxOutwardSpeedReduction, 0f, 0.95f);
    }

    private float ResolveInwardPushSpeed()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.BoundaryInwardPushSpeed;
        }

        return Mathf.Max(0f, fallbackInwardPushSpeed);
    }

    private float ResolveHardInset()
    {
        if (useConfigBoundarySettings && config != null)
        {
            return config.BoundaryHardInset;
        }

        return Mathf.Max(0f, fallbackHardInset);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBoundaryGizmos)
        {
            return;
        }

        Vector2 mapSize = ResolveMapSize();
        Vector2 resolvedMapCenter = ResolveMapCenter();
        float hardInset = ResolveHardInset();
        Vector2 half = mapSize * 0.5f - Vector2.one * hardInset;
        half.x = Mathf.Max(0.1f, half.x);
        half.y = Mathf.Max(0.1f, half.y);

        DrawRectangle(resolvedMapCenter, half, hardBoundaryGizmoColor);

        float warningDistance = ResolveWarningDistance();
        Vector2 warningHalf = new Vector2(
            Mathf.Max(0.1f, half.x - warningDistance),
            Mathf.Max(0.1f, half.y - warningDistance)
        );

        DrawRectangle(resolvedMapCenter, warningHalf, warningGizmoColor);
    }

    private static void DrawRectangle(Vector2 center, Vector2 halfExtents, Color color)
    {
        Vector3 bottomLeft = center + new Vector2(-halfExtents.x, -halfExtents.y);
        Vector3 topLeft = center + new Vector2(-halfExtents.x, halfExtents.y);
        Vector3 topRight = center + new Vector2(halfExtents.x, halfExtents.y);
        Vector3 bottomRight = center + new Vector2(halfExtents.x, -halfExtents.y);

        Gizmos.color = color;
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fallbackMapSize.x = Mathf.Max(1f, fallbackMapSize.x);
        fallbackMapSize.y = Mathf.Max(1f, fallbackMapSize.y);
        fallbackWarningDistance = Mathf.Max(0f, fallbackWarningDistance);
        fallbackResistanceDistance = Mathf.Max(0.05f, fallbackResistanceDistance);
        fallbackMaxOutwardSpeedReduction = Mathf.Clamp(fallbackMaxOutwardSpeedReduction, 0f, 0.95f);
        fallbackInwardPushSpeed = Mathf.Max(0f, fallbackInwardPushSpeed);
        fallbackHardInset = Mathf.Max(0f, fallbackHardInset);
        warningMessageDuration = Mathf.Max(0.1f, warningMessageDuration);
        warningRepeatCooldown = Mathf.Max(0f, warningRepeatCooldown);
    }
#endif
}
