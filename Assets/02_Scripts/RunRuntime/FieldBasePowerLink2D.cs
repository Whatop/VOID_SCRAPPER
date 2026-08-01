using UnityEngine;

/// <summary>
/// 기지 전력 장치와 포탑 사이의 전력 연결선을 표시한다.
/// 기지 전력이 끊기거나 연결된 포탑이 파괴되면 자동으로 꺼진다.
/// </summary>
[DisallowMultipleComponent]
public class FieldBasePowerLink2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private BaseTurretController linkedTurret;

    [Header("Line Renderer")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private bool autoCreateLineRenderer = true;
    [SerializeField] private Material lineMaterial;

    [Header("Visual")]
    [SerializeField] private Color powerColor = new Color(0.25f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color glowColor = new Color(0.75f, 1f, 1f, 0.45f);
    [Min(0.005f)]
    [SerializeField] private float lineWidth = 0.07f;
    [Min(2)]
    [SerializeField] private int segmentCount = 10;
    [Min(0f)]
    [SerializeField] private float waveAmplitude = 0.035f;
    [Min(0f)]
    [SerializeField] private float waveSpeed = 2.8f;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 9;

    private bool basePowered = true;
    private bool turretDestroyed;
    private Gradient cachedGradient;

    public BaseTurretController LinkedTurret => linkedTurret;
    public bool IsVisible => lineRenderer != null && lineRenderer.enabled;

    private void Awake()
    {
        EnsureLineRenderer();
        RebuildGradient();
        SubscribeTurret(true);
        RefreshVisibility();
    }

    private void OnEnable()
    {
        SubscribeTurret(true);
        RefreshVisibility();
    }

    private void OnDisable()
    {
        SubscribeTurret(false);

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        UpdateLineVisual();
    }

    public void Bind(
        Transform sourcePoint,
        Transform targetPoint,
        BaseTurretController turret)
    {
        SubscribeTurret(false);

        startPoint = sourcePoint;
        endPoint = targetPoint;
        linkedTurret = turret;
        turretDestroyed = linkedTurret != null && linkedTurret.IsDestroyed;

        SubscribeTurret(true);
        EnsureLineRenderer();
        RebuildGradient();
        RefreshVisibility();
    }

    public void SetBasePowered(bool powered)
    {
        basePowered = powered;
        RefreshVisibility();
    }

    private void SubscribeTurret(bool subscribe)
    {
        if (linkedTurret == null)
        {
            return;
        }

        linkedTurret.PowerChanged -= HandleTurretPowerChanged;
        linkedTurret.Destroyed -= HandleTurretDestroyed;

        if (subscribe)
        {
            linkedTurret.PowerChanged += HandleTurretPowerChanged;
            linkedTurret.Destroyed += HandleTurretDestroyed;
        }
    }

    private void HandleTurretPowerChanged(BaseTurretController _, bool __)
    {
        RefreshVisibility();
    }

    private void HandleTurretDestroyed(BaseTurretController _)
    {
        turretDestroyed = true;
        RefreshVisibility();
    }

    private bool ShouldShow()
    {
        return basePowered &&
               !turretDestroyed &&
               linkedTurret != null &&
               linkedTurret.PoweredOn &&
               !linkedTurret.IsDestroyed &&
               startPoint != null &&
               endPoint != null;
    }

    private void RefreshVisibility()
    {
        if (lineRenderer == null)
        {
            EnsureLineRenderer();
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = ShouldShow();
        }
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = GetComponentInChildren<LineRenderer>(true);
        }

        if (lineRenderer == null && autoCreateLineRenderer)
        {
            GameObject lineObject = new GameObject("PowerBeam");
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
        lineRenderer.positionCount = Mathf.Max(2, segmentCount);
        lineRenderer.startWidth = Mathf.Max(0.005f, lineWidth);
        lineRenderer.endWidth = Mathf.Max(0.005f, lineWidth);
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineRenderer.sharedMaterial == null)
        {
            if (lineMaterial != null)
            {
                lineRenderer.sharedMaterial = lineMaterial;
            }
            else if (Application.isPlaying)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    lineRenderer.sharedMaterial = new Material(shader);
                }
            }
        }
    }

    private void UpdateLineVisual()
    {
        if (lineRenderer == null)
        {
            EnsureLineRenderer();
        }

        if (lineRenderer == null)
        {
            return;
        }

        bool shouldShow = ShouldShow();
        lineRenderer.enabled = shouldShow;

        if (!shouldShow)
        {
            return;
        }

        int count = Mathf.Max(2, segmentCount);
        if (lineRenderer.positionCount != count)
        {
            lineRenderer.positionCount = count;
        }

        lineRenderer.startWidth = Mathf.Max(0.005f, lineWidth);
        lineRenderer.endWidth = Mathf.Max(0.005f, lineWidth);
        lineRenderer.colorGradient = cachedGradient ?? BuildGradient();

        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;
        Vector3 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            return;
        }

        Vector3 direction = delta / length;
        Vector3 normal = Vector3.Cross(direction, Vector3.forward).normalized;
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 position = Vector3.Lerp(start, end, t);

            if (i != 0 && i != count - 1 && waveAmplitude > 0f)
            {
                float wave = Mathf.Sin((t * 9f) + (time * waveSpeed));
                position += normal * wave * waveAmplitude;
            }

            lineRenderer.SetPosition(i, position);
        }
    }

    private void RebuildGradient()
    {
        cachedGradient = BuildGradient();
    }

    private Gradient BuildGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(glowColor, 0f),
                new GradientColorKey(powerColor, 0.2f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(powerColor, 0.8f),
                new GradientColorKey(glowColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(glowColor.a, 0f),
                new GradientAlphaKey(powerColor.a, 0.2f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(powerColor.a, 0.8f),
                new GradientAlphaKey(glowColor.a, 1f)
            }
        );
        return gradient;
    }
}
