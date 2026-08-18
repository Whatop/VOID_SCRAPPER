using System.Collections.Generic;
using UnityEngine;

public class RadarHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform radarArea;
    [SerializeField] private RadarMarkerUI markerPrefab;
    [SerializeField] private Transform defaultCenter;

    [Header("Scan")]
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private bool autoUpdate = true;

    [Header("Fallback Colors")]
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color rewardColor = Color.yellow;
    [SerializeField] private Color meteorColor = Color.white;
    [SerializeField] private Color specialColor = new Color(0.75f, 0.25f, 1f);
    [SerializeField] private Color coreColor = Color.yellow;

    private readonly List<RadarTarget> currentTargets = new List<RadarTarget>();
    private readonly Dictionary<RadarTarget, RadarMarkerUI> markerMap = new Dictionary<RadarTarget, RadarMarkerUI>();

    private Transform center;

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

        center = defaultCenter;
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
        float halfWidth = areaSize.x * 0.5f;
        float halfHeight = areaSize.y * 0.5f;
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

        marker.SetVisual(
            target.MarkerSprite,
            ResolveMarkerColor(target.MarkerType, target.MarkerColor),
            ResolveMarkerScale(target.MarkerType, target.MarkerScale),
            target.MarkerType
        );

        return marker;
    }

    private void ClearMissingMarkers()
    {
        List<RadarTarget> removeList = new List<RadarTarget>();

        foreach (RadarTarget target in markerMap.Keys)
        {
            if (target == null || !currentTargets.Contains(target) || !target.IsRadarVisible)
            {
                removeList.Add(target);
            }
        }

        for (int i = 0; i < removeList.Count; i++)
        {
            RadarTarget target = removeList[i];
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
            specialColor,
            coreColor
        );
    }

    private float ResolveMarkerScale(RadarMarkerType markerType, float customScale)
    {
        return RadarMarkerPresentation.ResolveScale(markerType, customScale);
    }
}
