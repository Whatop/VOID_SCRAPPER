using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-6500)]
[DisallowMultipleComponent]
public sealed class MapDiscoveryController : MonoBehaviour
{
    private static MapDiscoveryController instance;

    [Header("Map Source")]
    [SerializeField] private ExpeditionMapGenerator mapGenerator;
    [SerializeField, Min(0.25f)] private float worldUnitsPerCell = 1f;
    [SerializeField, Min(16)] private int fallbackTextureSize = 120;

    [Header("Discovery Mask")]
    [Tooltip("미탐색 영역에 표시할 마스크 색입니다. 별도 지도 배경 위에 겹쳐 사용하세요.")]
    [SerializeField] private Color32 undiscoveredColor = new Color32(0, 0, 0, 215);
    [Tooltip("탐색 완료 영역의 마스크 색입니다. 기본은 완전 투명입니다.")]
    [SerializeField] private Color32 discoveredColor = new Color32(255, 255, 255, 0);

    [Header("Start Area")]
    [SerializeField] private bool revealAroundPlayerOnStart = true;
    [SerializeField, Min(0f)] private float initialRevealRadius = 3f;

    [Header("Debug")]
    [SerializeField] private bool logInitialization;

    private readonly HashSet<RadarTarget> discoveredTargets = new HashSet<RadarTarget>();
    private readonly List<Color32> colorBuffer = new List<Color32>();

    private Bounds mapBounds;
    private Texture2D discoveryTexture;
    private bool[] discoveredCells;
    private int textureWidth;
    private int textureHeight;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static MapDiscoveryController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<MapDiscoveryController>();
            }

            return instance;
        }
    }

    public Texture2D DiscoveryTexture => discoveryTexture;
    public Bounds MapBounds => mapBounds;
    public bool IsInitialized => initialized;
    public IReadOnlyCollection<RadarTarget> DiscoveredTargets => discoveredTargets;

    public event Action DiscoveryChanged;
    public event Action<RadarTarget> TargetDiscovered;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
        }
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        TryInitializeFromCurrentMap();
    }

    private void Start()
    {
        TryInitializeFromCurrentMap();

        if (revealAroundPlayerOnStart && initialRevealRadius > 0f)
        {
            PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                RevealCircle(player.transform.position, initialRevealRadius, false);
            }
        }
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (discoveryTexture != null)
        {
            Destroy(discoveryTexture);
        }
    }

    public void Initialize(Bounds bounds)
    {
        mapBounds = bounds;

        float cellSize = Mathf.Max(0.25f, worldUnitsPerCell);
        textureWidth = Mathf.Max(16, Mathf.CeilToInt(Mathf.Max(1f, bounds.size.x) / cellSize));
        textureHeight = Mathf.Max(16, Mathf.CeilToInt(Mathf.Max(1f, bounds.size.y) / cellSize));

        if (discoveryTexture != null)
        {
            Destroy(discoveryTexture);
        }

        discoveryTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime_MapDiscoveryMask",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        discoveredCells = new bool[textureWidth * textureHeight];
        discoveredTargets.Clear();
        initialized = true;
        FillMask(undiscoveredColor);

        if (logInitialization)
        {
            Debug.Log($"Map discovery initialized: {textureWidth}x{textureHeight}, Bounds={mapBounds}", this);
        }

        DiscoveryChanged?.Invoke();
    }

    public void RevealCircle(Vector2 worldCenter, float worldRadius, bool notify = true)
    {
        EnsureInitialized();

        if (!initialized || discoveredCells == null)
        {
            return;
        }

        worldRadius = Mathf.Max(0f, worldRadius);
        Vector2 minWorld = worldCenter - Vector2.one * worldRadius;
        Vector2 maxWorld = worldCenter + Vector2.one * worldRadius;

        Vector2Int minCell = WorldToCell(minWorld);
        Vector2Int maxCell = WorldToCell(maxWorld);
        float radiusSqr = worldRadius * worldRadius;
        bool changed = false;

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                Vector2 cellWorld = CellToWorldCenter(x, y);
                if ((cellWorld - worldCenter).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                int index = y * textureWidth + x;
                if (discoveredCells[index])
                {
                    continue;
                }

                discoveredCells[index] = true;
                discoveryTexture.SetPixel(x, y, discoveredColor);
                changed = true;
            }
        }

        if (changed)
        {
            discoveryTexture.Apply(false, false);

            if (notify)
            {
                DiscoveryChanged?.Invoke();
            }
        }
    }

    public void RegisterRadarScan(Vector2 worldCenter, float worldRadius, IReadOnlyList<RadarTarget> scannedTargets)
    {
        RevealCircle(worldCenter, worldRadius, false);

        if (scannedTargets != null)
        {
            for (int i = 0; i < scannedTargets.Count; i++)
            {
                DiscoverTarget(scannedTargets[i], false);
            }
        }

        DiscoveryChanged?.Invoke();
    }

    public bool DiscoverTarget(RadarTarget target, bool revealCellAroundTarget = true)
    {
        if (target == null)
        {
            return false;
        }

        bool added = discoveredTargets.Add(target);
        target.SetMapDiscovered(true);

        if (revealCellAroundTarget)
        {
            RevealCircle(target.WorldPosition, Mathf.Max(0.75f, worldUnitsPerCell), false);
        }

        if (added)
        {
            TargetDiscovered?.Invoke(target);
            DiscoveryChanged?.Invoke();
        }

        return added;
    }

    public bool IsTargetDiscovered(RadarTarget target)
    {
        return target != null && (target.IsMapDiscovered || discoveredTargets.Contains(target));
    }

    public bool IsWorldPositionDiscovered(Vector2 worldPosition)
    {
        EnsureInitialized();

        if (!initialized || discoveredCells == null || !mapBounds.Contains(new Vector3(worldPosition.x, worldPosition.y, mapBounds.center.z)))
        {
            return false;
        }

        Vector2Int cell = WorldToCell(worldPosition);
        return discoveredCells[cell.y * textureWidth + cell.x];
    }

    public Vector2 WorldToNormalized(Vector2 worldPosition)
    {
        EnsureInitialized();

        float minX = mapBounds.min.x;
        float minY = mapBounds.min.y;
        float width = Mathf.Max(0.001f, mapBounds.size.x);
        float height = Mathf.Max(0.001f, mapBounds.size.y);

        return new Vector2(
            Mathf.Clamp01((worldPosition.x - minX) / width),
            Mathf.Clamp01((worldPosition.y - minY) / height)
        );
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generator)
    {
        if (generator == null)
        {
            return;
        }

        mapGenerator = generator;
        Initialize(generator.MapBounds);

        if (revealAroundPlayerOnStart && initialRevealRadius > 0f)
        {
            PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                RevealCircle(player.transform.position, initialRevealRadius);
            }
        }
    }

    private void TryInitializeFromCurrentMap()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
        }

        if (mapGenerator != null && mapGenerator.MapBounds.size.x > 0.01f && mapGenerator.MapBounds.size.y > 0.01f)
        {
            Initialize(mapGenerator.MapBounds);
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        TryInitializeFromCurrentMap();

        if (initialized)
        {
            return;
        }

        float size = Mathf.Max(16, fallbackTextureSize);
        Initialize(new Bounds(Vector3.zero, new Vector3(size, size, 1f)));
    }

    private Vector2Int WorldToCell(Vector2 worldPosition)
    {
        Vector2 normalized = WorldToNormalized(worldPosition);
        int x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * textureWidth), 0, textureWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normalized.y * textureHeight), 0, textureHeight - 1);
        return new Vector2Int(x, y);
    }

    private Vector2 CellToWorldCenter(int x, int y)
    {
        float normalizedX = (x + 0.5f) / Mathf.Max(1, textureWidth);
        float normalizedY = (y + 0.5f) / Mathf.Max(1, textureHeight);

        return new Vector2(
            Mathf.Lerp(mapBounds.min.x, mapBounds.max.x, normalizedX),
            Mathf.Lerp(mapBounds.min.y, mapBounds.max.y, normalizedY)
        );
    }

    private void FillMask(Color32 color)
    {
        int count = textureWidth * textureHeight;
        colorBuffer.Clear();

        for (int i = 0; i < count; i++)
        {
            colorBuffer.Add(color);
        }

        discoveryTexture.SetPixels32(colorBuffer.ToArray());
        discoveryTexture.Apply(false, false);
    }
}
