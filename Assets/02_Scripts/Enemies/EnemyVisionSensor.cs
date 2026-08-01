using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class EnemyVisionSensor : MonoBehaviour
{
    private static readonly List<EnemyVisionSensor> ActiveSensors = new List<EnemyVisionSensor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRegistry()
    {
        ActiveSensors.Clear();
    }

    [Header("References")]
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private Transform eyeOrigin;
    [SerializeField] private Transform target;

    [Header("Direct Vision")]
    [SerializeField] private float visionRange = 9f;
    [Range(1f, 360f)]
    [SerializeField] private float visionAngle = 100f;
    [SerializeField] private float closeAwarenessRadius = 1.25f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private bool nonTriggerCollidersBlockSight = true;

    [Header("Visual Detection Progress")]
    [Tooltip("시야 안에 들어온 플레이어를 완전히 발견하기까지 걸리는 기본 시간입니다.")]
    [Min(0.05f)]
    [SerializeField] private float visualDetectionTime = 0.75f;

    [Range(0.01f, 0.95f)]
    [SerializeField] private float suspicionThreshold = 0.12f;

    [Min(0.05f)]
    [SerializeField] private float awarenessDecayTime = 1.1f;

    [Min(0.05f)]
    [SerializeField] private float closeDetectionRate = 1.4f;

    [Min(0.05f)]
    [SerializeField] private float farDetectionRate = 0.65f;

    [Header("Radar Detection")]
    [SerializeField] private bool useRadarDetection = true;
    [SerializeField] private float radarDetectionRange = 6f;

    [Header("Noise Hearing")]
    [SerializeField] private bool canHearGunfire = true;
    [SerializeField] private float minimumNoiseReactionInterval = 0.15f;

    [Header("Tactical Overlay")]
    [SerializeField] private bool autoCreateVisionCone = true;
    [SerializeField] private MeshFilter coneMeshFilter;
    [SerializeField] private MeshRenderer coneMeshRenderer;
    [SerializeField] private int coneSegments = 24;
    [SerializeField] private float coneRefreshInterval = 0.08f;
    [SerializeField] private string overlaySortingLayer = "Default";
    [SerializeField] private int overlaySortingOrder = -20;
    [SerializeField] private Color patrolConeColor = new Color(0.25f, 0.85f, 1f, 0.15f);
    [SerializeField] private Color alertConeColor = new Color(1f, 0.8f, 0.15f, 0.2f);
    [SerializeField] private Color combatConeColor = new Color(1f, 0.15f, 0.12f, 0.25f);

    [Header("Definition Presets")]
    [SerializeField] private bool useEnemyTypePreset = true;

    [Header("Debug")]
    [SerializeField] private bool drawVisionGizmos = true;

    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[48];

    private PlayerStealthController targetStealth;
    private Mesh coneMesh;
    private Material coneMaterial;
    private float coneRefreshTimer;
    private float lastNoiseReactionTime = -999f;
    private float visualAwareness01;
    private bool rawDirectSight;
    private bool confirmedDirectSight;

    public float VisionRange => visionRange;
    public float VisionAngle => visionAngle;
    public float RadarDetectionRange => radarDetectionRange;
    public float VisualAwareness01 => visualAwareness01;
    public bool HasRawDirectSight => rawDirectSight;
    public bool HasConfirmedDirectSight => confirmedDirectSight;
    public bool HasVisualSuspicion =>
        rawDirectSight &&
        !confirmedDirectSight &&
        visualAwareness01 >= suspicionThreshold;

    public bool IsStateIntelVisible =>
        targetStealth != null &&
        targetStealth.CanRevealEnemyState(radarTarget);

    public bool IsVisionIntelVisible =>
        targetStealth != null &&
        targetStealth.CanRevealEnemyVision(radarTarget);

    // 기존 외부 참조 호환용. 상태 표시 기준으로 반환합니다.
    public bool IsTacticalIntelVisible => IsStateIntelVisible;
    public Vector2 EyePosition => eyeOrigin != null ? eyeOrigin.position : transform.position;
    public Vector2 FacingDirection => enemyAI != null && enemyAI.FacingDirection.sqrMagnitude > 0.001f
        ? enemyAI.FacingDirection.normalized
        : transform.up;

    private void Reset()
    {
        enemyAI = GetComponent<EnemyBaseAI>();
        radarTarget = GetComponent<RadarTarget>();
        eyeOrigin = transform;
    }

    private void Awake()
    {
        ResolveReferences();

        if (autoCreateVisionCone)
        {
            EnsureVisionCone();
        }
    }

    private void OnEnable()
    {
        if (!ActiveSensors.Contains(this))
        {
            ActiveSensors.Add(this);
        }

        ResolveReferences();
        ResetVisualAwareness();
        coneRefreshTimer = 0f;
    }

    private void OnDisable()
    {
        ActiveSensors.Remove(this);
        SetConeVisible(false);
    }

    private void OnDestroy()
    {
        if (coneMesh != null)
        {
            Destroy(coneMesh);
        }

        if (coneMaterial != null)
        {
            Destroy(coneMaterial);
        }
    }

    private void Update()
    {
        ResolveTarget();
        UpdateVisualAwareness(Time.deltaTime);

        bool showOverlay = IsVisionIntelVisible &&
                           enemyAI != null &&
                           enemyAI.CurrentState != EnemyState.Dead;

        SetConeVisible(showOverlay);

        if (!showOverlay)
        {
            return;
        }

        coneRefreshTimer -= Time.deltaTime;

        if (coneRefreshTimer <= 0f)
        {
            coneRefreshTimer = Mathf.Max(0.02f, coneRefreshInterval);
            RefreshVisionCone();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        bool targetChanged = target != newTarget;
        target = newTarget;
        targetStealth = target != null ? target.GetComponentInParent<PlayerStealthController>() : null;

        if (target != null && targetStealth == null)
        {
            targetStealth = target.gameObject.AddComponent<PlayerStealthController>();
        }

        if (targetChanged)
        {
            ResetVisualAwareness();
        }
    }

    public void ApplyDefinition(EnemyDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        visionRange = Mathf.Max(0f, definition.VisionRange);

        if (!useEnemyTypePreset)
        {
            return;
        }

        switch (definition.EnemyType)
        {
            case EnemyType.Shotgun:
                visionAngle = 120f;
                radarDetectionRange = 5.5f;
                break;

            case EnemyType.Charging:
                visionAngle = 80f;
                radarDetectionRange = 7.5f;
                break;

            case EnemyType.Elite:
                visionAngle = 135f;
                radarDetectionRange = 8f;
                break;

            case EnemyType.EliteMachineGun:
                visionAngle = 125f;
                radarDetectionRange = 8f;
                break;

            case EnemyType.EliteShotgun:
                visionAngle = 145f;
                radarDetectionRange = 7.5f;
                break;

            case EnemyType.EliteCharging:
                visionAngle = 90f;
                radarDetectionRange = 9f;
                break;

            case EnemyType.Boss:
                visionAngle = 360f;
                radarDetectionRange = Mathf.Max(visionRange, 18f);
                break;

            case EnemyType.ShopDrone:
                visionAngle = 180f;
                radarDetectionRange = Mathf.Max(visionRange, 12f);
                break;

            case EnemyType.MeleeCharger:
                visionAngle = 150f;
                radarDetectionRange = Mathf.Max(visionRange, 8f);
                closeAwarenessRadius = Mathf.Max(closeAwarenessRadius, 1.8f);
                break;

            default:
                visionAngle = 100f;
                radarDetectionRange = 6f;
                break;
        }
    }

    public bool HasDirectSight(Transform candidate)
    {
        return EvaluateDirectSight(candidate);
    }

    public bool HasConfirmedSight(Transform candidate)
    {
        if (candidate == null || candidate != target)
        {
            return false;
        }

        return confirmedDirectSight;
    }

    public void ForceDetectTarget(Transform candidate = null)
    {
        if (candidate != null && candidate != target)
        {
            SetTarget(candidate);
        }

        if (target == null)
        {
            return;
        }

        rawDirectSight = true;
        visualAwareness01 = 1f;
        confirmedDirectSight = true;
    }

    public bool HasRadarContact(Transform candidate)
    {
        if (!useRadarDetection || candidate == null)
        {
            return false;
        }

        PlayerStealthController stealth = candidate.GetComponentInParent<PlayerStealthController>();

        if (stealth != null && stealth.IsHiddenFromEnemyRadar)
        {
            return false;
        }

        float sqrDistance = ((Vector2)candidate.position - EyePosition).sqrMagnitude;
        float range = Mathf.Max(0f, radarDetectionRange);
        return sqrDistance <= range * range;
    }

    private void UpdateVisualAwareness(float deltaTime)
    {
        if (target == null ||
            enemyAI == null ||
            enemyAI.CurrentState == EnemyState.Dead)
        {
            ResetVisualAwareness();
            return;
        }

        rawDirectSight = EvaluateDirectSight(target);

        if (rawDirectSight)
        {
            Vector2 origin = EyePosition;
            float distance = Vector2.Distance(origin, target.position);
            float normalizedDistance = Mathf.Clamp01(
                distance / Mathf.Max(0.1f, visionRange)
            );

            float distanceRate = Mathf.Lerp(
                closeDetectionRate,
                farDetectionRate,
                normalizedDistance
            );

            float stealthMultiplier = targetStealth != null
                ? targetStealth.EnemyVisualDetectionTimeMultiplier
                : 1f;

            float effectiveDetectionTime = Mathf.Max(
                0.05f,
                visualDetectionTime * Mathf.Max(1f, stealthMultiplier)
            );

            visualAwareness01 +=
                Mathf.Max(0f, deltaTime) /
                effectiveDetectionTime *
                Mathf.Max(0.05f, distanceRate);
        }
        else
        {
            visualAwareness01 -=
                Mathf.Max(0f, deltaTime) /
                Mathf.Max(0.05f, awarenessDecayTime);
        }

        visualAwareness01 = Mathf.Clamp01(visualAwareness01);
        confirmedDirectSight = rawDirectSight && visualAwareness01 >= 0.999f;
    }

    private bool EvaluateDirectSight(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Vector2 origin = EyePosition;
        Vector2 toTarget = (Vector2)candidate.position - origin;
        float distance = toTarget.magnitude;

        if (distance > Mathf.Max(0f, visionRange))
        {
            return false;
        }

        if (distance > Mathf.Max(0f, closeAwarenessRadius))
        {
            Vector2 facing = FacingDirection;
            float angle = Vector2.Angle(facing, toTarget);

            if (angle > Mathf.Clamp(visionAngle, 1f, 360f) * 0.5f)
            {
                return false;
            }
        }

        return HasClearLineOfSight(origin, candidate.position, candidate);
    }

    private void ResetVisualAwareness()
    {
        visualAwareness01 = 0f;
        rawDirectSight = false;
        confirmedDirectSight = false;
    }

    public void NotifyNoise(Vector2 noisePosition, float noiseRadius)
    {
        if (!canHearGunfire ||
            enemyAI == null ||
            enemyAI.CurrentState == EnemyState.Dead ||
            noiseRadius <= 0f)
        {
            return;
        }

        if (Time.time - lastNoiseReactionTime < minimumNoiseReactionInterval)
        {
            return;
        }

        float sqrDistance = ((Vector2)transform.position - noisePosition).sqrMagnitude;

        if (sqrDistance > noiseRadius * noiseRadius)
        {
            return;
        }

        lastNoiseReactionTime = Time.time;

        if (enemyAI.CurrentState != EnemyState.Combat &&
            enemyAI.CurrentState != EnemyState.Taunt)
        {
            enemyAI.AlertTo(noisePosition);
        }
    }

    public static void BroadcastNoise(Vector2 noisePosition, float noiseRadius)
    {
        if (noiseRadius <= 0f)
        {
            return;
        }

        for (int i = ActiveSensors.Count - 1; i >= 0; i--)
        {
            EnemyVisionSensor sensor = ActiveSensors[i];

            if (sensor == null)
            {
                ActiveSensors.RemoveAt(i);
                continue;
            }

            if (sensor.isActiveAndEnabled)
            {
                sensor.NotifyNoise(noisePosition, noiseRadius);
            }
        }
    }

    private void ResolveReferences()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyBaseAI>();
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (eyeOrigin == null)
        {
            eyeOrigin = transform;
        }

        ResolveTarget();
    }

    private void ResolveTarget()
    {
        if (target == null && enemyAI != null)
        {
            target = enemyAI.Player;
        }

        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                target = playerObject.transform;
            }
        }

        if (target == null)
        {
            targetStealth = null;
            ResetVisualAwareness();
            return;
        }

        if (targetStealth == null)
        {
            targetStealth = target.GetComponentInParent<PlayerStealthController>();

            if (targetStealth == null)
            {
                targetStealth = target.gameObject.AddComponent<PlayerStealthController>();
            }
        }
    }

    private bool HasClearLineOfSight(Vector2 origin, Vector2 destination, Transform candidate)
    {
        Vector2 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        direction /= distance;

        int layerMask = obstacleMask.value != 0 ? obstacleMask.value : Physics2D.AllLayers;
        int count = Physics2D.RaycastNonAlloc(origin, direction, sightHits, distance, layerMask);

        SortHitsByDistance(count);

        for (int i = 0; i < count; i++)
        {
            Collider2D hitCollider = sightHits[i].collider;

            if (!IsValidSightBlocker(hitCollider, candidate))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private float GetVisibleDistance(Vector2 origin, Vector2 direction, float maxDistance)
    {
        int layerMask = obstacleMask.value != 0 ? obstacleMask.value : Physics2D.AllLayers;
        int count = Physics2D.RaycastNonAlloc(origin, direction, sightHits, maxDistance, layerMask);

        SortHitsByDistance(count);

        for (int i = 0; i < count; i++)
        {
            Collider2D hitCollider = sightHits[i].collider;

            if (!IsValidSightBlocker(hitCollider, target))
            {
                continue;
            }

            return Mathf.Clamp(sightHits[i].distance, 0f, maxDistance);
        }

        return maxDistance;
    }

    private bool IsValidSightBlocker(Collider2D collider, Transform candidate)
    {
        if (collider == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;

        if (hitTransform == transform ||
            hitTransform.IsChildOf(transform) ||
            transform.IsChildOf(hitTransform))
        {
            return false;
        }

        if (candidate != null &&
            (hitTransform == candidate ||
             hitTransform.IsChildOf(candidate) ||
             candidate.IsChildOf(hitTransform)))
        {
            return false;
        }

        if (!nonTriggerCollidersBlockSight && collider.isTrigger)
        {
            return false;
        }

        if (collider.isTrigger)
        {
            return false;
        }

        if (collider.GetComponentInParent<EnemyBaseAI>() != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<RewardPickup>() != null ||
            collider.GetComponentInParent<ReinforcementPickup>() != null ||
            collider.GetComponentInParent<TraitPickup>() != null)
        {
            return false;
        }

        return true;
    }

    private void SortHitsByDistance(int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit2D key = sightHits[i];
            int j = i - 1;

            while (j >= 0 && sightHits[j].distance > key.distance)
            {
                sightHits[j + 1] = sightHits[j];
                j--;
            }

            sightHits[j + 1] = key;
        }
    }

    private void EnsureVisionCone()
    {
        if (coneMeshFilter != null && coneMeshRenderer != null)
        {
            PrepareConeMaterial();
            return;
        }

        GameObject overlayObject = new GameObject("VisionConeOverlay");
        overlayObject.transform.SetParent(transform, false);
        overlayObject.layer = gameObject.layer;

        coneMeshFilter = overlayObject.AddComponent<MeshFilter>();
        coneMeshRenderer = overlayObject.AddComponent<MeshRenderer>();
        coneMeshRenderer.sortingLayerName = overlaySortingLayer;
        coneMeshRenderer.sortingOrder = overlaySortingOrder;

        PrepareConeMaterial();
        SetConeVisible(false);
    }

    private void PrepareConeMaterial()
    {
        if (coneMeshFilter == null || coneMeshRenderer == null)
        {
            return;
        }

        if (coneMesh == null)
        {
            coneMesh = new Mesh
            {
                name = $"{name}_VisionConeMesh"
            };
            coneMesh.MarkDynamic();
            coneMeshFilter.sharedMesh = coneMesh;
        }

        if (coneMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader != null)
            {
                coneMaterial = new Material(shader)
                {
                    name = $"{name}_VisionConeMaterial",
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        if (coneMaterial != null)
        {
            coneMeshRenderer.sharedMaterial = coneMaterial;
        }
    }

    private void SetConeVisible(bool visible)
    {
        if (autoCreateVisionCone && (coneMeshFilter == null || coneMeshRenderer == null))
        {
            EnsureVisionCone();
        }

        if (coneMeshRenderer != null)
        {
            coneMeshRenderer.enabled = visible;
        }
    }

    private void RefreshVisionCone()
    {
        if (coneMeshFilter == null || coneMeshRenderer == null)
        {
            EnsureVisionCone();
        }

        if (coneMesh == null || coneMeshRenderer == null)
        {
            return;
        }

        Transform overlayTransform = coneMeshRenderer.transform;
        Vector2 origin = EyePosition;
        Vector2 facing = FacingDirection;
        float facingAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

        overlayTransform.position = origin;
        overlayTransform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
        overlayTransform.localScale = Vector3.one;

        int segments = Mathf.Clamp(coneSegments, 6, 64);
        float halfAngle = Mathf.Clamp(visionAngle, 1f, 360f) * 0.5f;
        float range = Mathf.Max(0.05f, visionRange);

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        Color[] colors = new Color[vertices.Length];

        Color stateColor = ResolveConeColor();

        vertices[0] = Vector3.zero;
        colors[0] = Color.white;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float localAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 localDirection = Rotate(Vector2.right, localAngle);
            Vector2 worldDirection = Rotate(facing, localAngle).normalized;
            float visibleDistance = GetVisibleDistance(origin, worldDirection, range);

            vertices[i + 1] = (Vector3)(localDirection * visibleDistance);
            colors[i + 1] = Color.white;

            if (i < segments)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }
        }

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.colors = colors;
        coneMesh.RecalculateBounds();

        if (coneMaterial != null)
        {
            coneMaterial.color = stateColor;
        }
    }

    private Color ResolveConeColor()
    {
        if (enemyAI == null)
        {
            return patrolConeColor;
        }

        return enemyAI.CurrentState switch
        {
            EnemyState.Alert => alertConeColor,
            EnemyState.Search => alertConeColor,
            EnemyState.Combat => combatConeColor,
            EnemyState.Taunt => combatConeColor,
            _ => patrolConeColor
        };
    }

    private Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawVisionGizmos)
        {
            return;
        }

        Vector2 origin = eyeOrigin != null ? eyeOrigin.position : transform.position;
        Vector2 facing = Application.isPlaying && enemyAI != null
            ? FacingDirection
            : transform.up;

        float halfAngle = Mathf.Clamp(visionAngle, 1f, 360f) * 0.5f;
        Vector2 left = Rotate(facing, -halfAngle);
        Vector2 right = Rotate(facing, halfAngle);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, visionRange);
        Gizmos.DrawLine(origin, origin + left * visionRange);
        Gizmos.DrawLine(origin, origin + right * visionRange);

        if (useRadarDetection)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(origin, radarDetectionRange);
        }
    }
}
