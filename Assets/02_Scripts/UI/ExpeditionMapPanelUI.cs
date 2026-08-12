using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ExpeditionMapPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private RawImage discoveryMaskImage;
    [SerializeField] private RadarMarkerUI markerPrefab;
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private Transform player;
    [SerializeField] private MapDiscoveryController discoveryController;

    [Header("Marker Colors")]
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color rewardColor = Color.yellow;
    [SerializeField] private Color meteorColor = Color.white;
    [SerializeField] private Color specialColor = new Color(0.75f, 0.25f, 1f, 1f);
    [SerializeField] private Color coreColor = new Color(1f, 0.85f, 0.15f, 1f);

    [Header("Marker Rules")]
    [SerializeField] private bool showEnemiesOnlyWhenRecentlyScanned = true;
    [SerializeField, Min(0f)] private float enemyMarkerLingerSeconds = 6f;
    [SerializeField] private bool showMeteorsOnFullMap;
    [SerializeField] private bool showRewardObjectsOnFullMap = true;
    [SerializeField] private bool autoRefreshWhileVisible = true;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

    private readonly Dictionary<RadarTarget, RadarMarkerUI> markerMap = new Dictionary<RadarTarget, RadarMarkerUI>();
    private readonly List<RadarTarget> removeBuffer = new List<RadarTarget>();

    private bool visible;
    private float refreshTimer;

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (mapArea == null)
        {
            mapArea = transform as RectTransform;
        }

        if (markerRoot == null)
        {
            markerRoot = mapArea;
        }

        if (discoveryController == null)
        {
            discoveryController = FindFirstObjectByType<MapDiscoveryController>();
        }

        ResolvePlayer();
        RefreshDiscoveryTexture();
    }

    private void OnEnable()
    {
        if (discoveryController == null)
        {
            discoveryController = MapDiscoveryController.Instance;
        }

        if (discoveryController != null)
        {
            discoveryController.DiscoveryChanged += HandleDiscoveryChanged;
        }
    }

    private void OnDisable()
    {
        if (discoveryController != null)
        {
            discoveryController.DiscoveryChanged -= HandleDiscoveryChanged;
        }
    }

    private void Update()
    {
        if (!visible || !autoRefreshWhileVisible)
        {
            return;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        refreshTimer = refreshInterval;
        RefreshAll();
    }

    public void SetVisible(bool value)
    {
        visible = value;

        if (rootObject != null && rootObject != gameObject && rootObject.activeSelf != value)
        {
            rootObject.SetActive(value);
        }

        if (value)
        {
            RefreshAll();
        }
    }

    public void RefreshAll()
    {
        ResolvePlayer();
        RefreshDiscoveryTexture();
        RefreshPlayerMarker();
        RebuildTargetMarkers();
    }

    private void HandleDiscoveryChanged()
    {
        if (visible)
        {
            RefreshAll();
        }
    }

    private void RefreshDiscoveryTexture()
    {
        if (discoveryController == null)
        {
            discoveryController = MapDiscoveryController.Instance;
        }

        if (discoveryMaskImage != null && discoveryController != null)
        {
            discoveryMaskImage.texture = discoveryController.DiscoveryTexture;
        }
    }

    private void RefreshPlayerMarker()
    {
        if (playerMarker == null || player == null || discoveryController == null)
        {
            return;
        }

        playerMarker.anchoredPosition = WorldToMapPosition(player.position);
    }

    private void RebuildTargetMarkers()
    {
        if (markerPrefab == null || markerRoot == null || discoveryController == null)
        {
            return;
        }

        IReadOnlyCollection<RadarTarget> activeTargets = RadarTarget.ActiveTargets;

        foreach (RadarTarget target in activeTargets)
        {
            if (!ShouldShowTarget(target))
            {
                continue;
            }

            if (!markerMap.TryGetValue(target, out RadarMarkerUI marker) || marker == null)
            {
                marker = Instantiate(markerPrefab, markerRoot);
                markerMap[target] = marker;
            }

            marker.SetVisual(
                target.MarkerSprite,
                ResolveMarkerColor(target),
                ResolveMarkerScale(target)
            );
            marker.SetPosition(WorldToMapPosition(target.WorldPosition));
        }

        removeBuffer.Clear();

        foreach (KeyValuePair<RadarTarget, RadarMarkerUI> pair in markerMap)
        {
            if (!ShouldShowTarget(pair.Key))
            {
                removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            RadarTarget target = removeBuffer[i];
            if (markerMap.TryGetValue(target, out RadarMarkerUI marker) && marker != null)
            {
                Destroy(marker.gameObject);
            }

            markerMap.Remove(target);
        }
    }

    private Color ResolveMarkerColor(RadarTarget target)
    {
        if (target.MarkerColor.a > 0f && target.MarkerColor != Color.white)
        {
            return target.MarkerColor;
        }

        return target.MarkerType switch
        {
            RadarMarkerType.Enemy => enemyColor,
            RadarMarkerType.Boss => enemyColor,
            RadarMarkerType.RewardObject => rewardColor,
            RadarMarkerType.Meteor => meteorColor,
            RadarMarkerType.Shop => specialColor,
            RadarMarkerType.Event => specialColor,
            RadarMarkerType.Core => coreColor,
            RadarMarkerType.ReturnBeacon => coreColor,
            _ => Color.white
        };
    }

    private static float ResolveMarkerScale(RadarTarget target)
    {
        float scale = target.MarkerScale;
        if (target.MarkerType == RadarMarkerType.Boss || target.MarkerType == RadarMarkerType.Core)
        {
            scale *= 1.35f;
        }

        return scale;
    }

    private bool ShouldShowTarget(RadarTarget target)
    {
        if (target == null || !target.IsRadarVisible || !target.ShowOnMap)
        {
            return false;
        }

        if (!discoveryController.IsTargetDiscovered(target) &&
            !discoveryController.IsWorldPositionDiscovered(target.WorldPosition))
        {
            return false;
        }

        switch (target.MarkerType)
        {
            case RadarMarkerType.Enemy:
            case RadarMarkerType.Boss:
                return !showEnemiesOnlyWhenRecentlyScanned ||
                       target.WasScannedRecently(Mathf.Max(enemyMarkerLingerSeconds, target.MapMarkerLifetime));

            case RadarMarkerType.Meteor:
                return showMeteorsOnFullMap;

            case RadarMarkerType.RewardObject:
                return showRewardObjectsOnFullMap;

            default:
                return true;
        }
    }

    private Vector2 WorldToMapPosition(Vector2 worldPosition)
    {
        Vector2 normalized = discoveryController.WorldToNormalized(worldPosition);
        Rect rect = mapArea != null ? mapArea.rect : new Rect(-100f, -100f, 200f, 200f);

        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalized.y)
        );
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        PlayerController2D controller = FindFirstObjectByType<PlayerController2D>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }
}
