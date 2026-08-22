using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldBaseLaserGate : MonoBehaviour
{
    public enum GateMode
    {
        PlayerBarrier = 0,
        VisualOnly = 1
    }

    [Header("State")]
    [SerializeField] private bool startsOpen;

    [Header("Behavior")]
    [SerializeField] private GateMode gateMode = GateMode.PlayerBarrier;

    [Header("Blocking")]
    [FormerlySerializedAs("triggerZone")]
    [SerializeField] private Collider2D blockingCollider;

    [FormerlySerializedAs("pushPlayerWhenClosed")]
    [Tooltip("PlayerBarrier 모드에서만 사용합니다. 닫힌 상태일 때 물리 Collider로 플레이어를 막습니다.")]
    [SerializeField] private bool blockPlayerWhenClosed = true;

    [Tooltip("플레이어가 닫힌 게이트에 닿아 계속 밀고 있을 때 표시할 경고 간격입니다.")]
    [SerializeField] private float warningCooldown = 0.75f;

    [SerializeField] private string blockedWarning = "레이저 차단막이 활성화되어 있다.";

    [Header("Projectile Blocking")]
    [Tooltip("VisualOnly 모드에서는 아래 설정과 관계없이 모든 투사체가 통과합니다.")]
    [SerializeField] private bool blockPlayerProjectilesWhenClosed = true;
    [SerializeField] private bool blockEnemyProjectilesWhenClosed = true;
    [SerializeField] private bool blockShopDefenseProjectilesWhenClosed = true;

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
    private ExpeditionHUD cachedHud;

    public bool IsOpen => isOpen;
    public GateMode Mode => gateMode;
    public bool BlocksPlayerWhenClosed =>
        gateMode == GateMode.PlayerBarrier && blockPlayerWhenClosed;

    // 기존 코드 호환용. 더 이상 플레이어를 직접 밀어내지는 않는다.
    public bool PushPlayerWhenClosed => BlocksPlayerWhenClosed;

    public bool ShouldBlockProjectile(ProjectileOwner projectileOwner)
    {
        if (isOpen || gateMode == GateMode.VisualOnly)
        {
            return false;
        }

        return projectileOwner switch
        {
            ProjectileOwner.Player => blockPlayerProjectilesWhenClosed,
            ProjectileOwner.Enemy => blockEnemyProjectilesWhenClosed,
            ProjectileOwner.ShopDefense => blockShopDefenseProjectilesWhenClosed,
            _ => true
        };
    }

    private void Reset()
    {
        blockingCollider = GetComponent<Collider2D>();

        if (blockingCollider != null)
        {
            blockingCollider.isTrigger = false;
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
        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        if (beamStartPoint == null)
        {
            beamStartPoint = transform;
        }

        if (beamEndPoint == null)
        {
            beamEndPoint = transform;
        }

        ConfigureBlockingCollider();
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

        RefreshBlockingCollider();
        SetObjectsActive(closedVisuals, !isOpen);
        SetObjectsActive(openVisuals, isOpen);
        UpdateLaserVisual();

        if (!immediate)
        {
            AudioManager.PlayAt(
                isOpen ? SoundEventIds.EventComplete : SoundEventIds.ActionDenied,
                transform.position,
                0.7f
            );
        }
    }

    public void SetGateMode(GateMode mode)
    {
        gateMode = mode;
        ConfigureBlockingCollider();
        RefreshBlockingCollider();
        UpdateLaserVisual();
    }

    public void SetPlayerBlockingEnabled(bool enabled)
    {
        blockPlayerWhenClosed = enabled;
        RefreshBlockingCollider();
    }

    // 기존 호출부 호환용.
    public void SetPlayerPushEnabled(bool enabled)
    {
        SetPlayerBlockingEnabled(enabled);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleBlockedPlayerCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleBlockedPlayerCollision(collision);
    }

    private void HandleBlockedPlayerCollision(Collision2D collision)
    {
        if (isOpen ||
            gateMode != GateMode.PlayerBarrier ||
            !blockPlayerWhenClosed ||
            collision == null ||
            collision.collider == null)
        {
            return;
        }

        PlayerController2D player =
            collision.collider.GetComponentInParent<PlayerController2D>();

        if (player == null)
        {
            return;
        }

        if (Time.time - lastWarningTime < warningCooldown)
        {
            return;
        }

        lastWarningTime = Time.time;

        if (cachedHud == null)
        {
            cachedHud = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (cachedHud != null && !string.IsNullOrWhiteSpace(blockedWarning))
        {
            cachedHud.ShowWarning(blockedWarning);
        }

        AudioManager.PlayAt(
            SoundEventIds.ActionDenied,
            transform.position,
            0.8f
        );
    }

    private void ConfigureBlockingCollider()
    {
        if (blockingCollider == null)
        {
            return;
        }

        // 실제 물리 충돌로 막기 때문에 Trigger가 아니어야 한다.
        blockingCollider.isTrigger = false;
    }

    private void RefreshBlockingCollider()
    {
        if (blockingCollider == null)
        {
            return;
        }

        bool shouldBlock =
            !isOpen &&
            gateMode == GateMode.PlayerBarrier &&
            blockPlayerWhenClosed;

        blockingCollider.isTrigger = false;
        blockingCollider.enabled = shouldBlock;
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
        lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
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

        bool shouldShow =
            !isOpen &&
            beamStartPoint != null &&
            beamEndPoint != null;

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
        lineRenderer.startColor =
            Color.Lerp(laserGlowColor, laserColor, 0.7f);
        lineRenderer.endColor =
            Color.Lerp(laserGlowColor, laserColor, 0.7f);
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
        Vector3 normal =
            Vector3.Cross(direction, Vector3.forward).normalized;
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;

        for (int i = 0; i < segmentCount; i++)
        {
            float t =
                segmentCount <= 1
                    ? 0f
                    : i / (float)(segmentCount - 1);

            Vector3 position = Vector3.Lerp(start, end, t);

            if (i != 0 &&
                i != segmentCount - 1 &&
                zigzagAmplitude > 0f)
            {
                float wave =
                    Mathf.Sin((t * 8f) + (time * zigzagScrollSpeed));

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