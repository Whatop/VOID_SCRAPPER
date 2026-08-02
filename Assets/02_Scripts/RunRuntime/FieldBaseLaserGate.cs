using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldBaseLaserGate : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool allowEnemiesWhenClosed = true;
    [SerializeField] private bool allowNpcWhenClosed = true;

    [Header("Blocking")]
    [SerializeField] private Collider2D triggerZone;
    [Tooltip("끄면 레이저 시각과 전력 상태는 유지되지만 플레이어를 밀어내지 않습니다.")]
    [SerializeField] private bool pushPlayerWhenClosed = true;
    [SerializeField] private float pushOutDistance = 0.35f;
    [SerializeField] private float warningCooldown = 0.75f;
    [SerializeField] private string blockedWarning = "레이저 차단막이 활성화되어 있다.";

    [Header("Laser Visual")]
    [SerializeField] private bool useLaserVisual = true;
    [SerializeField] private Transform beamStartPoint;
    [SerializeField] private Transform beamEndPoint;
    [SerializeField] private bool autoCreateLineRenderer = true;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color laserColor = new Color(0.48f, 1f, 1f, 0.92f);
    [SerializeField] private Color laserGlowColor = new Color(1f, 0.58f, 0.95f, 0.65f);
    [Min(0.005f)]
    [SerializeField] private float laserWidth = 0.14f;
    [Min(2)]
    [SerializeField] private int laserSegments = 12;
    [Min(0f)]
    [SerializeField] private float zigzagAmplitude = 0.09f;
    [Min(0f)]
    [SerializeField] private float zigzagScrollSpeed = 3.2f;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 14;

    [Header("Visual")]
    [SerializeField] private GameObject[] closedVisuals;
    [SerializeField] private GameObject[] openVisuals;

    private bool isOpen;
    private float lastWarningTime = -999f;

    public bool IsOpen => isOpen;
    public bool PushPlayerWhenClosed => pushPlayerWhenClosed;

    private void Reset()
    {
        triggerZone = GetComponent<Collider2D>();
        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }

        if (beamStartPoint == null)
        {
            beamStartPoint = transform;
        }

        if (beamEndPoint == null)
        {
            beamEndPoint = transform;
        }
    }

    private void Awake()
    {
        if (triggerZone == null)
        {
            triggerZone = GetComponent<Collider2D>();
        }

        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }

        if (beamStartPoint == null)
        {
            beamStartPoint = transform;
        }

        if (beamEndPoint == null)
        {
            beamEndPoint = transform;
        }

        EnsureLineRenderer();
        SetGateOpen(startsOpen, true);
    }

    private void LateUpdate()
    {
        UpdateLaserVisual();
    }

    public void SetGateOpen(bool open, bool immediate = false)
    {
        isOpen = open;
        SetObjectsActive(closedVisuals, !isOpen);
        SetObjectsActive(openVisuals, isOpen);
        UpdateLaserVisual();

        if (!immediate)
        {
            AudioManager.PlayAt(isOpen ? SoundEventIds.EventComplete : SoundEventIds.ActionDenied, transform.position, 0.7f);
        }
    }

    public void SetPlayerPushEnabled(bool enabled)
    {
        pushPlayerWhenClosed = enabled;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isOpen || !pushPlayerWhenClosed || other == null)
        {
            return;
        }

        if (allowEnemiesWhenClosed && other.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        if (allowNpcWhenClosed && other.GetComponentInParent<FieldNpcObjective>() != null)
        {
            return;
        }

        PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector2 origin = player.transform.position;
        Vector2 closest = triggerZone != null ? triggerZone.ClosestPoint(origin) : (Vector2)transform.position;
        Vector2 pushDirection = origin - closest;

        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            Vector2 fallbackDirection = ((Vector2)(beamEndPoint != null ? beamEndPoint.position : transform.position)
                - (Vector2)(beamStartPoint != null ? beamStartPoint.position : transform.position)).normalized;

            pushDirection = fallbackDirection.sqrMagnitude > 0.001f
                ? new Vector2(-fallbackDirection.y, fallbackDirection.x)
                : (Vector2)transform.up;
        }

        pushDirection.Normalize();
        Vector2 correctedPosition = closest + (pushDirection * Mathf.Max(0.05f, pushOutDistance));

        if (rb != null)
        {
            rb.position = correctedPosition;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = correctedPosition;
        }

        if (Time.time - lastWarningTime >= warningCooldown)
        {
            lastWarningTime = Time.time;
            ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
            if (hud != null)
            {
                hud.ShowWarning(blockedWarning);
            }

            AudioManager.PlayAt(SoundEventIds.ActionDenied, transform.position, 0.8f);
        }
    }

    private void EnsureLineRenderer()
    {
        if (!useLaserVisual)
        {
            return;
        }

        if (lineRenderer == null && autoCreateLineRenderer)
        {
            GameObject lineObject = new GameObject("LaserBeam");
            lineObject.transform.SetParent(transform, false);
            lineRenderer = lineObject.AddComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.loop = false;
        lineRenderer.positionCount = Mathf.Max(2, laserSegments);
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineRenderer.sharedMaterial == null)
        {
            if (lineMaterial != null)
            {
                lineRenderer.sharedMaterial = lineMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    lineRenderer.sharedMaterial = new Material(shader);
                }
            }
        }
    }

    private void UpdateLaserVisual()
    {
        if (!useLaserVisual)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
            return;
        }

        if (lineRenderer == null)
        {
            EnsureLineRenderer();
        }

        if (lineRenderer == null)
        {
            return;
        }

        bool shouldShow = !isOpen && beamStartPoint != null && beamEndPoint != null;
        lineRenderer.enabled = shouldShow;

        if (!shouldShow)
        {
            return;
        }

        int segmentCount = Mathf.Max(2, laserSegments);
        if (lineRenderer.positionCount != segmentCount)
        {
            lineRenderer.positionCount = segmentCount;
        }

        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.startColor = Color.Lerp(laserGlowColor, laserColor, 0.7f);
        lineRenderer.endColor = Color.Lerp(laserGlowColor, laserColor, 0.7f);
        lineRenderer.colorGradient = BuildGradient();

        Vector3 start = beamStartPoint.position;
        Vector3 end = beamEndPoint.position;
        Vector3 line = end - start;
        float length = line.magnitude;

        if (length <= 0.001f)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            return;
        }

        Vector3 direction = line / length;
        Vector3 normal = Vector3.Cross(direction, Vector3.forward).normalized;
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = segmentCount <= 1 ? 0f : i / (float)(segmentCount - 1);
            Vector3 position = Vector3.Lerp(start, end, t);

            if (i != 0 && i != segmentCount - 1 && zigzagAmplitude > 0f)
            {
                float wave = Mathf.Sin((t * 8f) + (time * zigzagScrollSpeed));
                position += normal * wave * zigzagAmplitude;
            }

            lineRenderer.SetPosition(i, position);
        }
    }

    private Gradient BuildGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(laserGlowColor, 0f),
                new GradientColorKey(laserColor, 0.2f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(laserColor, 0.8f),
                new GradientColorKey(laserGlowColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(laserGlowColor.a, 0f),
                new GradientAlphaKey(laserColor.a, 0.2f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(laserColor.a, 0.8f),
                new GradientAlphaKey(laserGlowColor.a, 1f)
            }
        );
        return gradient;
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
}
