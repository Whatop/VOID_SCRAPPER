using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform radarArea;
    [SerializeField] private RadarMarkerUI markerPrefab;
    [SerializeField] private Transform defaultCenter;
    [SerializeField] private PlayerVisualStateController playerVisualState;

    [Header("Scan")]
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private bool autoUpdate = true;
    [SerializeField, Min(0f)] private float markerEdgePadding = 4f;
    [SerializeField, Range(0.25f, 1f)] private float meteorMarkerScaleMultiplier = 0.7f;

    [Header("Scope Presentation")]
    [SerializeField] private bool buildScopePresentation = true;
    [SerializeField] private Color scopeBackgroundColor = new Color(0.015f, 0.035f, 0.055f, 0.9f);
    [SerializeField] private Color scopeBorderColor = new Color(0.2f, 0.82f, 0.95f, 0.9f);
    [SerializeField] private Color scopeRingColor = new Color(0.18f, 0.65f, 0.78f, 0.28f);
    [SerializeField] private Color scopeCrosshairColor = new Color(0.18f, 0.65f, 0.78f, 0.18f);
    [SerializeField, Range(1, 4)] private int scopeRingCount = 3;

    [Header("Fallback Colors")]
    [SerializeField] private Color enemyColor = new Color(1f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color rewardColor = new Color(1f, 0.78f, 0.22f, 1f);
    [SerializeField] private Color meteorColor = new Color(0.68f, 0.78f, 0.9f, 1f);
    [SerializeField] private Color eventColor = new Color(0.25f, 0.82f, 1f, 1f);
    [SerializeField] private Color specialColor = new Color(0.75f, 0.25f, 1f);
    [SerializeField] private Color coreColor = Color.yellow;

    private readonly List<RadarTarget> currentTargets = new List<RadarTarget>();
    private readonly Dictionary<RadarTarget, RadarMarkerUI> markerMap = new Dictionary<RadarTarget, RadarMarkerUI>();
    private readonly List<RadarTarget> removeBuffer = new List<RadarTarget>();

    private Transform center;
    private RadarMarkerUI playerMarker;
    private RadarScopeGraphic scopeGraphic;

    private void Reset()
    {
        radarArea = transform as RectTransform;
    }

    private void Awake()
    {
        if (radarArea == null)
        {
            radarArea = transform as RectTransform;
        }

        EnsureScopePresentation();

        center = defaultCenter;
        if (center == null)
        {
            PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
            center = player != null ? player.transform : null;
        }

        playerVisualState ??= FindFirstObjectByType<PlayerVisualStateController>();
        if (playerVisualState != null)
        {
            playerVisualState.CurseStateChanged += HandlePlayerVisualChanged;
            playerVisualState.WeaponAccentChanged += HandleWeaponAccentChanged;
        }

        RadarTarget.PresentationChanged += HandleTargetPresentationChanged;

        CreatePlayerMarker();
    }

    private void Update()
    {
        if (autoUpdate)
        {
            RefreshMarkerPositions();
        }
    }

    public void SetScanRadius(float value)
    {
        scanRadius = Mathf.Max(0.01f, value);
        RefreshMarkerPositions();
    }

    public void SetTargets(IReadOnlyList<RadarTarget> targets, Transform scanCenter)
    {
        center = scanCenter != null ? scanCenter : defaultCenter;

        currentTargets.Clear();

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                {
                    currentTargets.Add(targets[i]);
                }
            }
        }

        RebuildMarkers();
    }

    public void Clear()
    {
        foreach (KeyValuePair<RadarTarget, RadarMarkerUI> pair in markerMap)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        markerMap.Clear();
        currentTargets.Clear();
    }

    public void RefreshMarkerPositions()
    {
        if (radarArea == null || center == null)
        {
            return;
        }

        Vector2 areaSize = radarArea.rect.size;
        float halfWidth = Mathf.Max(0f, areaSize.x * 0.5f - markerEdgePadding);
        float halfHeight = Mathf.Max(0f, areaSize.y * 0.5f - markerEdgePadding);
        float radius = Mathf.Max(0.01f, scanRadius);

        for (int i = currentTargets.Count - 1; i >= 0; i--)
        {
            RadarTarget target = currentTargets[i];

            if (target == null || !target.IsRadarVisible)
            {
                currentTargets.RemoveAt(i);
                continue;
            }

            RadarMarkerUI marker;

            if (!markerMap.TryGetValue(target, out marker) || marker == null)
            {
                continue;
            }

            Vector3 offset = target.WorldPosition - center.position;
            Vector2 normalized = new Vector2(offset.x / radius, offset.y / radius);
            normalized = Vector2.ClampMagnitude(normalized, 1f);

            marker.SetPosition(new Vector2(normalized.x * halfWidth, normalized.y * halfHeight));
        }

        ClearMissingMarkers();
    }

    private void RebuildMarkers()
    {
        ClearMissingMarkers();

        for (int i = 0; i < currentTargets.Count; i++)
        {
            RadarTarget target = currentTargets[i];

            if (target == null)
            {
                continue;
            }

            if (!markerMap.ContainsKey(target))
            {
                RadarMarkerUI marker = CreateMarker(target);

                if (marker != null)
                {
                    markerMap.Add(target, marker);
                }
            }
        }

        RefreshMarkerPositions();
    }

    private RadarMarkerUI CreateMarker(RadarTarget target)
    {
        if (markerPrefab == null || radarArea == null)
        {
            return null;
        }

        RadarMarkerUI marker = Instantiate(markerPrefab, radarArea);

        ApplyTargetMarkerVisual(marker, target);

        return marker;
    }

    private void ApplyTargetMarkerVisual(RadarMarkerUI marker, RadarTarget target)
    {
        if (marker == null || target == null)
        {
            return;
        }

        marker.SetVisual(
            target.MarkerSprite,
            ResolveMarkerColor(target.MarkerType, target.MarkerColor),
            ResolveMarkerScale(target.MarkerType, target.MarkerScale),
            target.MarkerType
        );
    }

    private void HandleTargetPresentationChanged(RadarTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (!target.IsRadarVisible)
        {
            ClearMissingMarkers();
            return;
        }

        if (!markerMap.TryGetValue(target, out RadarMarkerUI marker) || marker == null)
        {
            if (!currentTargets.Contains(target))
            {
                return;
            }

            marker = CreateMarker(target);
            if (marker == null)
            {
                return;
            }

            markerMap[target] = marker;
            RefreshMarkerPositions();
            return;
        }

        ApplyTargetMarkerVisual(marker, target);
    }

    private void ClearMissingMarkers()
    {
        removeBuffer.Clear();

        foreach (RadarTarget target in markerMap.Keys)
        {
            if (target == null || !currentTargets.Contains(target) || !target.IsRadarVisible)
            {
                removeBuffer.Add(target);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            RadarTarget target = removeBuffer[i];
            RadarMarkerUI marker = markerMap[target];

            if (marker != null)
            {
                Destroy(marker.gameObject);
            }

            markerMap.Remove(target);
        }
    }

    private Color ResolveMarkerColor(RadarMarkerType markerType, Color customColor)
    {
        return RadarMarkerPresentation.ResolveColor(
            markerType,
            customColor,
            enemyColor,
            rewardColor,
            meteorColor,
            eventColor,
            specialColor,
            coreColor
        );
    }

    private void OnDestroy()
    {
        RadarTarget.PresentationChanged -= HandleTargetPresentationChanged;

        if (playerVisualState != null)
        {
            playerVisualState.CurseStateChanged -= HandlePlayerVisualChanged;
            playerVisualState.WeaponAccentChanged -= HandleWeaponAccentChanged;
        }
    }

    private void CreatePlayerMarker()
    {
        if (playerMarker != null || markerPrefab == null || radarArea == null)
        {
            return;
        }

        playerMarker = Instantiate(markerPrefab, radarArea);
        playerMarker.name = "RadarPlayerMarker";
        playerMarker.SetPosition(Vector2.zero);
        RefreshPlayerMarkerVisual();
        playerMarker.transform.SetAsLastSibling();
    }

    private void HandlePlayerVisualChanged(bool _) => RefreshPlayerMarkerVisual();
    private void HandleWeaponAccentChanged(WeaponTreeType _, Color __) => RefreshPlayerMarkerVisual();

    private void RefreshPlayerMarkerVisual()
    {
        if (playerMarker == null)
        {
            return;
        }

        playerMarker.SetShapeVisual(RadarMarkerShape.Diamond, false, Color.green, 0.85f);
    }

    private float ResolveMarkerScale(RadarMarkerType markerType, float customScale)
    {
        float scale = RadarMarkerPresentation.ResolveScale(markerType, customScale);
        return markerType == RadarMarkerType.Meteor
            ? scale * meteorMarkerScaleMultiplier
            : scale;
    }

    private void EnsureScopePresentation()
    {
        if (!buildScopePresentation || radarArea == null)
        {
            return;
        }

        Transform existing = radarArea.Find("RadarScopePresentation");
        if (existing != null)
        {
            scopeGraphic = existing.GetComponent<RadarScopeGraphic>();
        }

        if (scopeGraphic == null)
        {
            GameObject scopeObject = new GameObject(
                "RadarScopePresentation",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RadarScopeGraphic)
            );
            scopeObject.layer = gameObject.layer;

            RectTransform scopeRect = scopeObject.GetComponent<RectTransform>();
            scopeRect.SetParent(radarArea, false);
            scopeRect.anchorMin = Vector2.zero;
            scopeRect.anchorMax = Vector2.one;
            scopeRect.offsetMin = Vector2.zero;
            scopeRect.offsetMax = Vector2.zero;
            scopeRect.SetAsFirstSibling();

            scopeGraphic = scopeObject.GetComponent<RadarScopeGraphic>();
            scopeGraphic.raycastTarget = false;
        }

        scopeGraphic.Configure(
            scopeBackgroundColor,
            scopeBorderColor,
            scopeRingColor,
            scopeCrosshairColor,
            scopeRingCount
        );
        scopeGraphic.transform.SetAsFirstSibling();
    }
}

[DisallowMultipleComponent]
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
        AddSolidCircle(vertexHelper, center, radius, backgroundColor, segments);
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

        AddRing(vertexHelper, center, radius, borderWidth, borderColor, segments);
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
