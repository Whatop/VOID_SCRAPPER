using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RadarScopeGraphic : MaskableGraphic
{
    [SerializeField] private Color backgroundColor = new Color(0.015f, 0.035f, 0.055f, 0.9f);
    [SerializeField] private Color borderColor = new Color(0.2f, 0.82f, 0.95f, 0.9f);
    [SerializeField] private Color ringColor = new Color(0.18f, 0.65f, 0.78f, 0.28f);
    [SerializeField] private Color crosshairColor = new Color(0.18f, 0.65f, 0.78f, 0.18f);
    [SerializeField, Range(1, 4)] private int ringCount = 3;
    [SerializeField, Min(0.25f)] private float borderWidth = 1.25f;
    [SerializeField, Min(0.25f)] private float ringWidth = 0.5f;
    [SerializeField, Min(0.25f)] private float crosshairWidth = 0.5f;
    private float backgroundAlphaMultiplier = 1f;
    private float borderIntensity = 1f;
    public float BorderIntensity => borderIntensity;

    public void SetBorderIntensity(float intensity)
    {
        float value = Mathf.Clamp(intensity, 0f, 2f);
        if (Mathf.Approximately(value, borderIntensity)) return;
        borderIntensity = value;
        SetVerticesDirty();
    }

    public void Configure(
        Color newBackgroundColor,
        Color newBorderColor,
        Color newRingColor,
        Color newCrosshairColor,
        int newRingCount)
    {
        backgroundColor = newBackgroundColor;
        borderColor = newBorderColor;
        ringColor = newRingColor;
        crosshairColor = newCrosshairColor;
        ringCount = Mathf.Clamp(newRingCount, 1, 4);
        SetVerticesDirty();
    }

    public void SetBackgroundAlphaMultiplier(float multiplier)
    {
        float clamped = Mathf.Clamp01(multiplier);
        if (Mathf.Approximately(backgroundAlphaMultiplier, clamped))
        {
            return;
        }

        backgroundAlphaMultiplier = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f - 1f);
        if (radius <= 0f)
        {
            return;
        }

        const int segments = 48;
        Color resolvedBackground = backgroundColor;
        resolvedBackground.a *= backgroundAlphaMultiplier;
        AddSolidCircle(vertexHelper, center, radius, resolvedBackground, segments);
        AddLine(
            vertexHelper,
            center + Vector2.left * (radius - 2f),
            center + Vector2.right * (radius - 2f),
            crosshairWidth,
            crosshairColor
        );
        AddLine(
            vertexHelper,
            center + Vector2.down * (radius - 2f),
            center + Vector2.up * (radius - 2f),
            crosshairWidth,
            crosshairColor
        );

        for (int i = 1; i <= ringCount; i++)
        {
            float ringRadius = radius * i / (ringCount + 1f);
            AddRing(vertexHelper, center, ringRadius, ringWidth, ringColor, segments);
        }

        Color border = borderColor;
        border.r *= borderIntensity;
        border.g *= borderIntensity;
        border.b *= borderIntensity;
        border.a = Mathf.Clamp01(border.a * borderIntensity);
        AddRing(vertexHelper, center, radius, borderWidth, border, segments);
    }

    private static void AddSolidCircle(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        Color color,
        int segments)
    {
        int centerIndex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, center, color);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            AddVertex(
                vertexHelper,
                center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                color
            );

            if (i > 0)
            {
                vertexHelper.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
            }
        }
    }

    private static void AddRing(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        float width,
        Color color,
        int segments)
    {
        float innerRadius = Mathf.Max(0f, radius - width * 0.5f);
        float outerRadius = radius + width * 0.5f;
        int startIndex = vertexHelper.currentVertCount;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            AddVertex(vertexHelper, center + direction * outerRadius, color);
            AddVertex(vertexHelper, center + direction * innerRadius, color);

            if (i <= 0)
            {
                continue;
            }

            int previousOuter = startIndex + (i - 1) * 2;
            int previousInner = previousOuter + 1;
            int currentOuter = startIndex + i * 2;
            int currentInner = currentOuter + 1;
            vertexHelper.AddTriangle(previousOuter, currentOuter, currentInner);
            vertexHelper.AddTriangle(previousOuter, currentInner, previousInner);
        }
    }

    private static void AddLine(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int startIndex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, start - perpendicular, color);
        AddVertex(vertexHelper, start + perpendicular, color);
        AddVertex(vertexHelper, end + perpendicular, color);
        AddVertex(vertexHelper, end - perpendicular, color);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertexHelper.AddVert(vertex);
    }
}
