using System.Collections.Generic;
using UnityEngine;

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

        Sprite sprite = null;
        Color color = Color.green;
        if (playerVisualState != null)
        {
            PlayerShipVisualController shipVisual = playerVisualState.GetComponent<PlayerShipVisualController>();
            sprite = shipVisual != null ? shipVisual.CurrentSprite : null;
            color = playerVisualState.CurrentWeaponAccentColor;
        }

        playerMarker.SetVisual(sprite, color, 0.85f);
    }

    private float ResolveMarkerScale(RadarMarkerType markerType, float customScale)
    {
        return RadarMarkerPresentation.ResolveScale(markerType, customScale);
    }
}
