using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpeditionRoutePlanner : MonoBehaviour
{
    public const int MaxWaypoints = 5;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private MapDiscoveryController mapCoordinates;

    [Header("Arrival")]
    [SerializeField, Min(0.1f)] private float arrivalRadius = 2.5f;

    [Header("World Guide")]
    [SerializeField] private LineRenderer worldGuide;
    [SerializeField, Min(0.5f)] private float worldGuideMaxLength = 6f;
    [SerializeField, Min(0f)] private float worldGuideStartOffset = 0.55f;
    [SerializeField, Min(0.005f)] private float worldGuideWidth = 0.04f;
    [SerializeField] private Color worldGuideStartColor = new Color(0.55f, 0.95f, 1f, 0.42f);
    [SerializeField] private Color worldGuideEndColor = new Color(0.55f, 0.95f, 1f, 0.06f);
    [SerializeField] private string worldGuideSortingLayer = "Default";
    [SerializeField] private int worldGuideSortingOrder = -5;

    private readonly Vector2[] worldWaypoints = new Vector2[MaxWaypoints];

    private RunManager boundRunManager;
    private Material runtimeGuideMaterial;
    private int waypointCount;
    private bool mapVisible;

    public int WaypointCount => waypointCount;
    public float ArrivalRadius => Mathf.Max(0.1f, arrivalRadius);

    public event Action RouteChanged;

    private void Awake()
    {
        ResolveReferences();
        EnsureWorldGuide();
        SetWorldGuideVisible(false);
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        TryBindRunManager();
    }

    private void Start()
    {
        ResolveReferences();
        TryBindRunManager();
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
        UnbindRunManager();
        SetWorldGuideVisible(false);
    }

    private void OnDestroy()
    {
        if (runtimeGuideMaterial != null)
        {
            Destroy(runtimeGuideMaterial);
            runtimeGuideMaterial = null;
        }
    }

    private void Update()
    {
        if (waypointCount <= 0 || player == null || !player.gameObject.activeInHierarchy)
        {
            SetWorldGuideVisible(false);
            return;
        }

        bool gameplayActive = !mapVisible && !GameplayPauseManager.IsPaused &&
                              (playerHealth == null || !playerHealth.IsDead);
        if (!gameplayActive)
        {
            SetWorldGuideVisible(false);
            return;
        }

        Vector2 playerPosition = player.position;
        if ((worldWaypoints[0] - playerPosition).sqrMagnitude <= ArrivalRadius * ArrivalRadius)
        {
            AdvanceCurrentWaypoint();
            if (waypointCount <= 0)
            {
                return;
            }
        }

        UpdateWorldGuide(playerPosition, worldWaypoints[0]);
    }

    public bool TryAddWaypoint(Vector2 worldPosition)
    {
        if (waypointCount >= MaxWaypoints || !IsInsideMapBounds(worldPosition))
        {
            return false;
        }

        worldWaypoints[waypointCount] = worldPosition;
        waypointCount++;
        NotifyRouteChanged();
        return true;
    }

    public bool RemoveWaypoint(int index)
    {
        if (index < 0 || index >= waypointCount)
        {
            return false;
        }

        for (int i = index; i < waypointCount - 1; i++)
        {
            worldWaypoints[i] = worldWaypoints[i + 1];
        }

        waypointCount--;
        worldWaypoints[waypointCount] = default;
        NotifyRouteChanged();
        return true;
    }

    public void ClearRoute()
    {
        if (waypointCount <= 0)
        {
            SetWorldGuideVisible(false);
            return;
        }

        Array.Clear(worldWaypoints, 0, worldWaypoints.Length);
        waypointCount = 0;
        NotifyRouteChanged();
    }

    public bool TryGetWaypoint(int index, out Vector2 worldPosition)
    {
        if (index < 0 || index >= waypointCount)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = worldWaypoints[index];
        return true;
    }

    public bool TryGetCurrentWaypoint(out Vector2 worldPosition)
    {
        return TryGetWaypoint(0, out worldPosition);
    }

    public bool AdvanceCurrentWaypoint()
    {
        return RemoveWaypoint(0);
    }

    public void SetMapVisible(bool value)
    {
        mapVisible = value;
        if (value)
        {
            SetWorldGuideVisible(false);
        }
    }

    private bool IsInsideMapBounds(Vector2 worldPosition)
    {
        if (mapCoordinates == null || !mapCoordinates.IsInitialized)
        {
            return true;
        }

        Bounds bounds = mapCoordinates.MapBounds;
        return worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x &&
               worldPosition.y >= bounds.min.y && worldPosition.y <= bounds.max.y;
    }

    private void NotifyRouteChanged()
    {
        if (waypointCount <= 0)
        {
            SetWorldGuideVisible(false);
        }

        RouteChanged?.Invoke();
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generator)
    {
        ClearRoute();
    }

    private void HandleRunEnded(RunResultData result)
    {
        ClearRoute();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            PlayerController2D controller = FindFirstObjectByType<PlayerController2D>();
            if (controller != null)
            {
                player = controller.transform;
            }
        }

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (mapCoordinates == null)
        {
            mapCoordinates = GetComponent<MapDiscoveryController>();
        }

        if (mapCoordinates == null)
        {
            mapCoordinates = FindFirstObjectByType<MapDiscoveryController>();
        }
    }

    private void TryBindRunManager()
    {
        RunManager current = RunManager.Instance;
        if (boundRunManager == current)
        {
            return;
        }

        UnbindRunManager();
        boundRunManager = current;
        if (boundRunManager != null)
        {
            boundRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnbindRunManager()
    {
        if (boundRunManager != null)
        {
            boundRunManager.RunEnded -= HandleRunEnded;
            boundRunManager = null;
        }
    }

    private void EnsureWorldGuide()
    {
        if (worldGuide == null)
        {
            GameObject guideObject = new GameObject("WorldRouteGuide");
            guideObject.transform.SetParent(transform, false);
            worldGuide = guideObject.AddComponent<LineRenderer>();
        }

        worldGuide.useWorldSpace = true;
        worldGuide.positionCount = 2;
        worldGuide.loop = false;
        worldGuide.numCapVertices = 0;
        worldGuide.numCornerVertices = 0;
        worldGuide.textureMode = LineTextureMode.Stretch;
        worldGuide.alignment = LineAlignment.View;
        worldGuide.startWidth = Mathf.Max(0.005f, worldGuideWidth);
        worldGuide.endWidth = Mathf.Max(0.005f, worldGuideWidth * 0.55f);
        worldGuide.startColor = worldGuideStartColor;
        worldGuide.endColor = worldGuideEndColor;
        worldGuide.sortingLayerName = string.IsNullOrWhiteSpace(worldGuideSortingLayer)
            ? "Default"
            : worldGuideSortingLayer;
        worldGuide.sortingOrder = worldGuideSortingOrder;

        if (worldGuide.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeGuideMaterial = new Material(shader)
                {
                    name = "Runtime_ExpeditionRouteGuide"
                };
                worldGuide.sharedMaterial = runtimeGuideMaterial;
            }
        }
    }

    private void UpdateWorldGuide(Vector2 playerPosition, Vector2 waypointPosition)
    {
        Vector2 offset = waypointPosition - playerPosition;
        float distance = offset.magnitude;
        if (distance <= 0.001f)
        {
            SetWorldGuideVisible(false);
            return;
        }

        Vector2 direction = offset / distance;
        float startDistance = Mathf.Min(Mathf.Max(0f, worldGuideStartOffset), distance);
        float endDistance = Mathf.Min(distance, startDistance + Mathf.Max(0.5f, worldGuideMaxLength));

        worldGuide.SetPosition(0, playerPosition + direction * startDistance);
        worldGuide.SetPosition(1, playerPosition + direction * endDistance);
        SetWorldGuideVisible(true);
    }

    private void SetWorldGuideVisible(bool value)
    {
        if (worldGuide != null && worldGuide.enabled != value)
        {
            worldGuide.enabled = value;
        }
    }
}
