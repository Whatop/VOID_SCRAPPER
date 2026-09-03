using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum RadarMarkerShape
{
    Circle,
    Diamond,
    Hexagon,
    Square
}

public static class RadarMarkerPresentation
{
    public static Color ResolveColor(
        RadarMarkerType markerType,
        Color customColor,
        Color enemyColor,
        Color rewardColor,
        Color meteorColor,
        Color eventColor,
        Color specialColor,
        Color coreColor)
    {
        // These categories are the shared Radar/Map visual language. Individual
        // targets may still carry legacy marker colors, but category readability
        // must remain stable across both presentations.
        switch (markerType)
        {
            case RadarMarkerType.Enemy:
            case RadarMarkerType.EnemyBase:
            case RadarMarkerType.Boss:
                return enemyColor;

            case RadarMarkerType.RewardObject:
                return rewardColor;

            case RadarMarkerType.Meteor:
                return meteorColor;

            case RadarMarkerType.Event:
                return eventColor;

            case RadarMarkerType.Shop:
                return specialColor;

            case RadarMarkerType.Core:
            case RadarMarkerType.ReturnBeacon:
                return coreColor;
        }

        if (customColor.a > 0f && customColor != Color.white)
        {
            return customColor;
        }

        return markerType switch
        {
            RadarMarkerType.Enemy => enemyColor,
            RadarMarkerType.EnemyBase => enemyColor,
            RadarMarkerType.Boss => enemyColor,
            RadarMarkerType.RewardObject => rewardColor,
            RadarMarkerType.Meteor => meteorColor,
            RadarMarkerType.Shop => specialColor,
            RadarMarkerType.Event => eventColor,
            RadarMarkerType.Unknown => eventColor,
            RadarMarkerType.FieldNpc => specialColor,
            RadarMarkerType.Core => coreColor,
            RadarMarkerType.ReturnBeacon => coreColor,
            _ => Color.white
        };
    }

    public static float ResolveScale(RadarMarkerType markerType, float customScale)
    {
        float scale = Mathf.Max(0.1f, customScale);
        if (markerType == RadarMarkerType.Boss || markerType == RadarMarkerType.Core)
        {
            scale *= 1.35f;
        }

        return scale;
    }

    public static bool TryResolveShape(
        RadarMarkerType markerType,
        out RadarMarkerShape shape,
        out bool hollow)
    {
        hollow = true;

        switch (markerType)
        {
            case RadarMarkerType.Enemy:
                shape = RadarMarkerShape.Circle;
                hollow = false;
                return true;

            case RadarMarkerType.RewardObject:
            case RadarMarkerType.Event:
            case RadarMarkerType.Unknown:
                shape = RadarMarkerShape.Circle;
                return true;

            case RadarMarkerType.FieldNpc:
                // Field NPCs provide their own icon so they remain distinct from shops/events.
                shape = RadarMarkerShape.Circle;
                return false;

            case RadarMarkerType.Meteor:
                shape = RadarMarkerShape.Square;
                hollow = false;
                return true;

            case RadarMarkerType.Shop:
                shape = RadarMarkerShape.Diamond;
                return true;

            case RadarMarkerType.EnemyBase:
                shape = RadarMarkerShape.Square;
                return true;

            case RadarMarkerType.Core:
                shape = RadarMarkerShape.Hexagon;
                return true;

            default:
                shape = RadarMarkerShape.Circle;
                return false;
        }
    }
}

public class RadarMarkerUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image iconImage;
    [SerializeField] private RadarMarkerShapeGraphic shapeGraphic;
    [SerializeField] private TextMeshProUGUI unknownLabel;

    public RectTransform RectTransform => rectTransform;

    private void Reset()
    {
        rectTransform = transform as RectTransform;
        iconImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    public void SetVisual(Sprite sprite, Color color, float scale)
    {
        SetVisual(sprite, color, scale, null);
    }

    public void SetVisual(Sprite sprite, Color color, float scale, RadarMarkerType markerType)
    {
        SetVisual(sprite, color, scale, (RadarMarkerType?)markerType);
    }

    public void SetShapeVisual(
        RadarMarkerShape shape,
        bool hollow,
        Color color,
        float scale = 1f)
    {
        EnsureShapeGraphic();
        shapeGraphic.Configure(shape, hollow, color);
        shapeGraphic.enabled = true;

        if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }

    private void SetVisual(Sprite sprite, Color color, float scale, RadarMarkerType? markerType)
    {
        RadarMarkerShape shape = RadarMarkerShape.Circle;
        bool hollow = true;
        bool useUnknownLabel = markerType == RadarMarkerType.Unknown;
        bool useCanonicalShape = !useUnknownLabel && markerType.HasValue &&
                                  RadarMarkerPresentation.TryResolveShape(
                                     markerType.Value,
                                     out shape,
                                     out hollow
                                 );

        if (useUnknownLabel)
        {
            EnsureUnknownLabel();
            unknownLabel.color = color;
            unknownLabel.enabled = true;

            if (shapeGraphic != null)
            {
                shapeGraphic.enabled = false;
            }
        }
        else if (useCanonicalShape)
        {
            EnsureShapeGraphic();
            shapeGraphic.Configure(shape, hollow, color);
            shapeGraphic.enabled = true;
        }
        else if (shapeGraphic != null)
        {
            shapeGraphic.enabled = false;
        }

        if (!useUnknownLabel && unknownLabel != null)
        {
            unknownLabel.enabled = false;
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = color;
            iconImage.enabled = !useUnknownLabel && !useCanonicalShape && sprite != null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }

    private void EnsureShapeGraphic()
    {
        if (shapeGraphic != null)
        {
            return;
        }

        shapeGraphic = GetComponentInChildren<RadarMarkerShapeGraphic>(true);
        if (shapeGraphic != null)
        {
            return;
        }

        GameObject shapeObject = new GameObject(
            "MarkerShape",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RadarMarkerShapeGraphic)
        );
        shapeObject.layer = gameObject.layer;

        RectTransform shapeRect = shapeObject.GetComponent<RectTransform>();
        shapeRect.SetParent(transform, false);
        shapeRect.anchorMin = Vector2.zero;
        shapeRect.anchorMax = Vector2.one;
        shapeRect.offsetMin = Vector2.zero;
        shapeRect.offsetMax = Vector2.zero;

        shapeGraphic = shapeObject.GetComponent<RadarMarkerShapeGraphic>();
        shapeGraphic.raycastTarget = false;
    }

    private void EnsureUnknownLabel()
    {
        if (unknownLabel != null)
        {
            return;
        }

        unknownLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        if (unknownLabel != null)
        {
            return;
        }

        GameObject labelObject = new GameObject(
            "UnknownMarkerLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = gameObject.layer;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        unknownLabel = labelObject.GetComponent<TextMeshProUGUI>();
        unknownLabel.text = "?";
        unknownLabel.alignment = TextAlignmentOptions.Center;
        unknownLabel.fontSize = 12f;
        unknownLabel.textWrappingMode = TextWrappingModes.NoWrap;
        unknownLabel.raycastTarget = false;
    }
}

[DisallowMultipleComponent]
public sealed class RadarMarkerShapeGraphic : MaskableGraphic
{
    [SerializeField] private RadarMarkerShape shape = RadarMarkerShape.Circle;
    [SerializeField] private bool hollow = true;
    [SerializeField, Min(0.5f)] private float outlineWidth = 1f;

    public void Configure(RadarMarkerShape newShape, bool newHollow, Color newColor)
    {
        bool geometryChanged = shape != newShape || hollow != newHollow;
        shape = newShape;
        hollow = newHollow;
        color = newColor;

        if (geometryChanged)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float outerRadius = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        if (outerRadius <= 0f)
        {
            return;
        }

        int segmentCount = shape switch
        {
            RadarMarkerShape.Diamond => 4,
            RadarMarkerShape.Square => 4,
            RadarMarkerShape.Hexagon => 6,
            _ => 20
        };
        float startAngle = shape == RadarMarkerShape.Square ? 45f : 90f;
        Vector2 center = rect.center;

        if (!hollow)
        {
            AddSolidShape(vertexHelper, center, outerRadius, segmentCount, startAngle);
            return;
        }

        float innerRadius = Mathf.Max(0f, outerRadius - Mathf.Max(0.5f, outlineWidth));
        AddHollowShape(vertexHelper, center, innerRadius, outerRadius, segmentCount, startAngle);
    }

    private void AddSolidShape(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        int segmentCount,
        float startAngle)
    {
        AddVertex(vertexHelper, center);
        for (int i = 0; i < segmentCount; i++)
        {
            AddVertex(vertexHelper, PointOnShape(center, radius, segmentCount, startAngle, i));
        }

        for (int i = 0; i < segmentCount; i++)
        {
            vertexHelper.AddTriangle(0, i + 1, (i + 1) % segmentCount + 1);
        }
    }

    private void AddHollowShape(
        VertexHelper vertexHelper,
        Vector2 center,
        float innerRadius,
        float outerRadius,
        int segmentCount,
        float startAngle)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            AddVertex(vertexHelper, PointOnShape(center, outerRadius, segmentCount, startAngle, i));
            AddVertex(vertexHelper, PointOnShape(center, innerRadius, segmentCount, startAngle, i));
        }

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % segmentCount;
            int outer = i * 2;
            int inner = outer + 1;
            int nextOuter = next * 2;
            int nextInner = nextOuter + 1;

            vertexHelper.AddTriangle(outer, nextOuter, nextInner);
            vertexHelper.AddTriangle(outer, nextInner, inner);
        }
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vertexHelper.AddVert(vertex);
    }

    private static Vector2 PointOnShape(
        Vector2 center,
        float radius,
        int segmentCount,
        float startAngle,
        int index)
    {
        float angle = (startAngle + index * 360f / segmentCount) * Mathf.Deg2Rad;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }
}
