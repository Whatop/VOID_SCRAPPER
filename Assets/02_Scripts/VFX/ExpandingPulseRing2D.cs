using UnityEngine;

[DisallowMultipleComponent]
public class ExpandingPulseRing2D : MonoBehaviour
{
    private static Material sharedRuntimeMaterial;

    [Header("Runtime")]
    [SerializeField] private LineRenderer lineRenderer;

    private float duration;
    private float elapsed;
    private float startRadius;
    private float endRadius;
    private float startWidth;
    private float endWidth;
    private Color color;
    private AnimationCurve expansionCurve;
    private bool useUnscaledTime;
    private bool initialized;

    public static ExpandingPulseRing2D Spawn(
        Vector3 position,
        Color pulseColor,
        float pulseDuration,
        float pulseStartRadius,
        float pulseEndRadius,
        float pulseStartWidth,
        float pulseEndWidth,
        int segmentCount,
        string sortingLayerName,
        int sortingOrder,
        Material materialOverride = null,
        AnimationCurve curve = null,
        bool useUnscaled = true)
    {
        GameObject instance = new GameObject("Runtime_ExpandingPulseRing2D");
        instance.transform.position = position;

        ExpandingPulseRing2D pulse = instance.AddComponent<ExpandingPulseRing2D>();
        pulse.Initialize(
            pulseColor,
            pulseDuration,
            pulseStartRadius,
            pulseEndRadius,
            pulseStartWidth,
            pulseEndWidth,
            segmentCount,
            sortingLayerName,
            sortingOrder,
            materialOverride,
            curve,
            useUnscaled
        );

        return pulse;
    }

    public void Initialize(
        Color pulseColor,
        float pulseDuration,
        float pulseStartRadius,
        float pulseEndRadius,
        float pulseStartWidth,
        float pulseEndWidth,
        int segmentCount,
        string sortingLayerName,
        int sortingOrder,
        Material materialOverride = null,
        AnimationCurve curve = null,
        bool useUnscaled = true)
    {
        duration = Mathf.Max(0.05f, pulseDuration);
        startRadius = Mathf.Max(0.01f, pulseStartRadius);
        endRadius = Mathf.Max(startRadius, pulseEndRadius);
        startWidth = Mathf.Max(0.001f, pulseStartWidth);
        endWidth = Mathf.Max(0.001f, pulseEndWidth);
        color = pulseColor;
        expansionCurve = curve != null && curve.length > 0
            ? curve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        useUnscaledTime = useUnscaled;

        EnsureLineRenderer(
            Mathf.Clamp(segmentCount, 12, 128),
            sortingLayerName,
            sortingOrder,
            materialOverride
        );

        elapsed = 0f;
        initialized = true;
        ApplyVisual(0f);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float normalized = Mathf.Clamp01(elapsed / duration);
        ApplyVisual(normalized);

        if (normalized >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureLineRenderer(
        int segmentCount,
        string sortingLayerName,
        int sortingOrder,
        Material materialOverride)
    {
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = segmentCount;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
            ? "Default"
            : sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.sharedMaterial = materialOverride != null
            ? materialOverride
            : GetRuntimeMaterial();

        for (int i = 0; i < segmentCount; i++)
        {
            float radians = i / (float)segmentCount * Mathf.PI * 2f;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f));
        }
    }

    private void ApplyVisual(float normalized)
    {
        if (lineRenderer == null)
        {
            return;
        }

        float eased = expansionCurve != null && expansionCurve.length > 0
            ? Mathf.Clamp01(expansionCurve.Evaluate(normalized))
            : normalized;

        float radius = Mathf.Lerp(startRadius, endRadius, eased);
        float width = Mathf.Lerp(startWidth, endWidth, normalized);
        float alpha = 1f - normalized;
        alpha *= alpha;

        transform.localScale = Vector3.one * radius;
        lineRenderer.startWidth = width / Mathf.Max(0.01f, radius);
        lineRenderer.endWidth = width / Mathf.Max(0.01f, radius);

        Color faded = color;
        faded.a *= alpha;
        lineRenderer.startColor = faded;
        lineRenderer.endColor = faded;
    }

    private static Material GetRuntimeMaterial()
    {
        if (sharedRuntimeMaterial != null)
        {
            return sharedRuntimeMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        sharedRuntimeMaterial = new Material(shader)
        {
            name = "Runtime_ExpandingPulseRing2D_Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        return sharedRuntimeMaterial;
    }
}
