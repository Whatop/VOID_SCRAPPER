using System;
using UnityEngine;

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
[AddComponentMenu("VOID SCRAPPER/World/Sea Boundary VFX 2D")]
public sealed class SeaBoundaryVFX2D : MonoBehaviour
{
    private enum BoundaryEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    [Serializable]
    private sealed class EdgeRuntime
    {
        public BoundaryEdge edge;
        public ParticleSystem system;
        public float baseEmission;
    }

    [Header("Map")]
    [SerializeField] private ExpeditionMapGenerator mapGenerator;
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private Transform player;
    [SerializeField] private bool autoUseGeneratedMapBounds = true;
    [SerializeField] private Vector2 fallbackMapSize = new Vector2(120f, 120f);
    [SerializeField] private Vector2 fallbackMapCenter;

    [Header("Boundary Band")]
    [Min(0.1f)]
    [SerializeField] private float bandThickness = 1.35f;
    [Min(0f)]
    [SerializeField] private float edgeOffset = 0.25f;
    [Min(0f)]
    [SerializeField] private float edgeOverhang = 2f;
    [Min(0.1f)]
    [SerializeField] private float fallbackWarningDistance = 7f;

    [Header("Particle Density")]
    [Min(0.05f)]
    [SerializeField] private float particlesPerWorldUnit = 0.6f;
    [Min(8)]
    [SerializeField] private int minimumParticlesPerEdge = 40;
    [Min(16)]
    [SerializeField] private int maximumParticlesPerEdge = 360;
    [Min(0f)]
    [SerializeField] private float farEmissionMultiplier = 0.18f;
    [Min(0f)]
    [SerializeField] private float nearEmissionMultiplier = 1.15f;

    [Header("Particle Motion")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.9f, 1.8f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.08f, 0.22f);
    [Min(0f)]
    [SerializeField] private float inwardDriftSpeed = 0.22f;
    [Min(0f)]
    [SerializeField] private float noiseStrength = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float noiseFrequency = 0.55f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Color")]
    [SerializeField] private Color boundaryColor = new Color(1f, 0.08f, 0.05f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float farAlpha = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float nearAlpha = 0.72f;

    [Header("Rendering")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 4;
    [SerializeField] private bool stretchParticles = true;

    [Header("Runtime")]
    [SerializeField] private bool rebuildOnEnable = true;
    [Min(0.02f)]
    [SerializeField] private float intensityRefreshInterval = 0.08f;

    private readonly EdgeRuntime[] edges = new EdgeRuntime[4];
    private Material runtimeMaterial;
    private Texture2D runtimeParticleTexture;
    private Bounds currentBounds;
    private float refreshTimer;
    private bool built;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        ResolveReferences();

        if (rebuildOnEnable)
        {
            Rebuild();
        }
    }

    private void Start()
    {
        if (!built)
        {
            Rebuild();
        }
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        if (runtimeParticleTexture != null)
        {
            Destroy(runtimeParticleTexture);
            runtimeParticleTexture = null;
        }
    }

    private void Update()
    {
        if (!built)
        {
            return;
        }

        refreshTimer -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        refreshTimer = Mathf.Max(0.02f, intensityRefreshInterval);
        ResolvePlayer();
        RefreshEdgeIntensity();
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generatedMap)
    {
        if (generatedMap != null)
        {
            mapGenerator = generatedMap;
        }

        Rebuild();
    }

    [ContextMenu("Rebuild Boundary VFX")]
    public void Rebuild()
    {
        ResolveReferences();
        ClearGeneratedEdges();
        currentBounds = ResolveBounds();

        CreateEdge(0, BoundaryEdge.Left);
        CreateEdge(1, BoundaryEdge.Right);
        CreateEdge(2, BoundaryEdge.Bottom);
        CreateEdge(3, BoundaryEdge.Top);

        built = true;
        refreshTimer = 0f;
        RefreshEdgeIntensity();
    }

    private void CreateEdge(int index, BoundaryEdge edge)
    {
        Vector2 size = currentBounds.size;
        Vector2 center = currentBounds.center;
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        GameObject edgeObject = new GameObject($"BoundaryParticles_{edge}");
        edgeObject.transform.SetParent(transform, false);

        Vector3 shapeScale;
        Vector2 inwardDirection;
        float edgeLength;

        switch (edge)
        {
            case BoundaryEdge.Left:
                edgeObject.transform.position = new Vector3(center.x - halfWidth - edgeOffset, center.y, transform.position.z);
                shapeScale = new Vector3(bandThickness, size.y + edgeOverhang * 2f, 0.1f);
                inwardDirection = Vector2.right;
                edgeLength = size.y;
                break;

            case BoundaryEdge.Right:
                edgeObject.transform.position = new Vector3(center.x + halfWidth + edgeOffset, center.y, transform.position.z);
                shapeScale = new Vector3(bandThickness, size.y + edgeOverhang * 2f, 0.1f);
                inwardDirection = Vector2.left;
                edgeLength = size.y;
                break;

            case BoundaryEdge.Bottom:
                edgeObject.transform.position = new Vector3(center.x, center.y - halfHeight - edgeOffset, transform.position.z);
                shapeScale = new Vector3(size.x + edgeOverhang * 2f, bandThickness, 0.1f);
                inwardDirection = Vector2.up;
                edgeLength = size.x;
                break;

            default:
                edgeObject.transform.position = new Vector3(center.x, center.y + halfHeight + edgeOffset, transform.position.z);
                shapeScale = new Vector3(size.x + edgeOverhang * 2f, bandThickness, 0.1f);
                inwardDirection = Vector2.down;
                edgeLength = size.x;
                break;
        }

        ParticleSystem system = edgeObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(system, shapeScale, inwardDirection, edgeLength);

        edges[index] = new EdgeRuntime
        {
            edge = edge,
            system = system,
            baseEmission = Mathf.Max(minimumParticlesPerEdge, edgeLength * particlesPerWorldUnit)
        };
    }

    private void ConfigureParticleSystem(
        ParticleSystem system,
        Vector3 shapeScale,
        Vector2 inwardDirection,
        float edgeLength)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.useUnscaledTime = useUnscaledTime;
        main.maxParticles = Mathf.Clamp(
            Mathf.CeilToInt(edgeLength * particlesPerWorldUnit * Mathf.Max(1.5f, lifetimeRange.y)),
            minimumParticlesPerEdge,
            maximumParticlesPerEdge
        );
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.05f, Mathf.Min(lifetimeRange.x, lifetimeRange.y)),
            Mathf.Max(0.05f, Mathf.Max(lifetimeRange.x, lifetimeRange.y))
        );
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, Mathf.Min(sizeRange.x, sizeRange.y)),
            Mathf.Max(0.01f, Mathf.Max(sizeRange.x, sizeRange.y))
        );
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(boundaryColor, farAlpha));
        main.gravityModifier = 0f;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, edgeLength * particlesPerWorldUnit * farEmissionMultiplier);

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = shapeScale;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = inwardDirection.x * inwardDriftSpeed;
        velocity.y = inwardDirection.y * inwardDriftSpeed;
        velocity.z = 0f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = noiseStrength > 0f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.strength = noiseStrength;
        noise.frequency = Mathf.Max(0.01f, noiseFrequency);
        noise.scrollSpeed = 0.25f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(boundaryColor, 0.45f),
                new GradientColorKey(boundaryColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.22f),
                new GradientAlphaKey(0.65f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.22f, 1f),
                new Keyframe(0.8f, 0.75f),
                new Keyframe(1f, 0.1f)
            )
        );

        ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = stretchParticles
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        particleRenderer.velocityScale = stretchParticles ? 0.18f : 0f;
        particleRenderer.lengthScale = stretchParticles ? 1.6f : 1f;
        particleRenderer.sortingLayerName = sortingLayerName;
        particleRenderer.sortingOrder = sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sharedMaterial = ResolveMaterial();

        system.Play(true);
    }

    private void RefreshEdgeIntensity()
    {
        if (player == null)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                ApplyIntensity(edges[i], 0f);
            }

            return;
        }

        Vector2 position = player.position;
        float warningDistance = ResolveWarningDistance();

        for (int i = 0; i < edges.Length; i++)
        {
            EdgeRuntime edge = edges[i];
            if (edge == null || edge.system == null)
            {
                continue;
            }

            float distance = edge.edge switch
            {
                BoundaryEdge.Left => position.x - currentBounds.min.x,
                BoundaryEdge.Right => currentBounds.max.x - position.x,
                BoundaryEdge.Bottom => position.y - currentBounds.min.y,
                _ => currentBounds.max.y - position.y
            };

            float pressure = warningDistance <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(distance / warningDistance);
            ApplyIntensity(edge, pressure);
        }
    }

    private void ApplyIntensity(EdgeRuntime edge, float pressure)
    {
        if (edge == null || edge.system == null)
        {
            return;
        }

        pressure = Mathf.Clamp01(pressure);
        float alpha = Mathf.Lerp(farAlpha, nearAlpha, pressure);
        float emissionMultiplier = Mathf.Lerp(farEmissionMultiplier, nearEmissionMultiplier, pressure);

        ParticleSystem.MainModule main = edge.system.main;
        main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(boundaryColor, alpha));

        ParticleSystem.EmissionModule emission = edge.system.emission;
        emission.rateOverTime = Mathf.Clamp(
            edge.baseEmission * emissionMultiplier,
            0f,
            maximumParticlesPerEdge * 2f
        );
    }

    private Bounds ResolveBounds()
    {
        if (autoUseGeneratedMapBounds)
        {
            if (mapGenerator == null)
            {
                mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
            }

            if (mapGenerator != null && mapGenerator.MapBounds.size.sqrMagnitude > 0.01f)
            {
                return mapGenerator.MapBounds;
            }
        }

        Vector2 size = fallbackMapSize;
        if (config != null)
        {
            ExpeditionDepth depth = RunManager.Instance != null && RunManager.Instance.CurrentRun != null
                ? RunManager.Instance.CurrentRun.ExpeditionDepth
                : ExpeditionDepth.Normal;
            size = config.GetMapSize(depth);
        }

        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);
        return new Bounds(fallbackMapCenter, size);
    }

    private float ResolveWarningDistance()
    {
        return config != null
            ? Mathf.Max(0.1f, config.BoundaryWarningDistance)
            : Mathf.Max(0.1f, fallbackWarningDistance);
    }

    private void ResolveReferences()
    {
        mapGenerator ??= FindFirstObjectByType<ExpeditionMapGenerator>();
        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (playerController != null)
        {
            player = playerController.transform;
        }
    }

    private Material ResolveMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime Sea Boundary Particle Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        runtimeParticleTexture = CreateSoftParticleTexture();

        if (runtimeParticleTexture != null)
        {
            if (runtimeMaterial.HasProperty("_BaseMap"))
            {
                runtimeMaterial.SetTexture("_BaseMap", runtimeParticleTexture);
            }

            if (runtimeMaterial.HasProperty("_MainTex"))
            {
                runtimeMaterial.SetTexture("_MainTex", runtimeParticleTexture);
            }
        }

        return runtimeMaterial;
    }

    private Texture2D CreateSoftParticleTexture()
    {
        const int textureSize = 16;
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true
        )
        {
            name = "Runtime Sea Boundary Particle Texture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nx = ((x + 0.5f) / textureSize) * 2f - 1f;
                float ny = ((y + 0.5f) / textureSize) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void ClearGeneratedEdges()
    {
        for (int i = 0; i < edges.Length; i++)
        {
            if (edges[i]?.system != null)
            {
                GameObject target = edges[i].system.gameObject;

                if (Application.isPlaying)
                {
                    Destroy(target);
                }
                else
                {
                    DestroyImmediate(target);
                }
            }

            edges[i] = null;
        }

        built = false;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
