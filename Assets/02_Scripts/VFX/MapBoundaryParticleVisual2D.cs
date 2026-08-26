using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("VOID SCRAPPER/VFX/Map Boundary Particle Visual 2D")]
public sealed class MapBoundaryParticleVisual2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExpeditionMapGenerator mapGenerator;
    [SerializeField] private WorldWrapController boundaryController;
    [SerializeField] private Transform generatedRoot;

    [Header("Fallback Bounds")]
    [SerializeField] private Vector2 fallbackMapCenter = Vector2.zero;
    [SerializeField] private Vector2 fallbackMapSize = new Vector2(120f, 120f);

    [Header("Boundary Shape")]
    [Min(0.05f)]
    [SerializeField] private float boundaryThickness = 0.75f;
    [SerializeField] private float boundaryInset = 0.2f;
    [Min(0f)]
    [SerializeField] private float edgeCornerPadding = 0.5f;

    [Header("Particles")]
    [Min(0f)]
    [SerializeField] private float particlesPerWorldUnit = 0.7f;
    [SerializeField] private Vector2 particleLifetimeRange = new Vector2(0.75f, 1.3f);
    [SerializeField] private Vector2 particleSizeRange = new Vector2(0.18f, 0.55f);
    [SerializeField] private float outwardDriftSpeed = 0.12f;
    [SerializeField] private float noiseStrength = 0.11f;
    [SerializeField] private float noiseFrequency = 0.55f;
    [SerializeField] private Color idleParticleColor = new Color(1f, 0.04f, 0.03f, 0.22f);
    [SerializeField] private Color pressuredParticleColor = new Color(1f, 0.08f, 0.03f, 0.72f);

    [Header("Barrier Line")]
    [SerializeField] private bool createBarrierLine = true;
    [Min(0.005f)]
    [SerializeField] private float lineWidth = 0.055f;
    [SerializeField] private Color idleLineColor = new Color(1f, 0.02f, 0.02f, 0.12f);
    [SerializeField] private Color pressuredLineColor = new Color(1f, 0.08f, 0.04f, 0.6f);

    [Header("Outer Hazard Haze")]
    [SerializeField] private bool createOuterHaze = true;
    [Min(0.5f)]
    [SerializeField] private float outerHazeWidth = 3f;
    [SerializeField] private Color idleHazeColor = new Color(0.45f, 0.01f, 0.01f, 0.16f);
    [SerializeField] private Color pressuredHazeColor = new Color(0.75f, 0.02f, 0.01f, 0.3f);

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int particleSortingOrder = -20;
    [SerializeField] private int lineSortingOrder = -19;
    [SerializeField] private int hazeSortingOrder = -21;

    [Header("Runtime")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool logRebuild;

    private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>(4);
    private readonly List<SpriteRenderer> hazeRenderers = new List<SpriteRenderer>(4);
    private LineRenderer barrierLine;
    private Material particleMaterial;
    private Material lineMaterial;
    private Texture2D hazeTexture;
    private Sprite hazeSprite;
    private float lastPressure = -1f;

    private void Reset()
    {
        mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
        boundaryController = FindFirstObjectByType<WorldWrapController>();
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;

        if (rebuildOnEnable)
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
        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }

        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }

        if (hazeSprite != null)
        {
            Destroy(hazeSprite);
        }

        if (hazeTexture != null)
        {
            Destroy(hazeTexture);
        }
    }

    private void Update()
    {
        float pressure = boundaryController != null
            ? Mathf.Clamp01(boundaryController.BoundaryPressure)
            : 0f;

        if (Mathf.Abs(pressure - lastPressure) < 0.01f)
        {
            return;
        }

        lastPressure = pressure;
        ApplyPressureVisual(pressure);
    }

    [ContextMenu("Rebuild Boundary Particle Visual")]
    public void Rebuild()
    {
        ResolveReferences();
        ClearGeneratedVisuals();

        Bounds bounds = ResolveBounds();
        EnsureGeneratedRoot();
        EnsureMaterials();

        float halfWidth = Mathf.Max(0.5f, bounds.extents.x - boundaryInset);
        float halfHeight = Mathf.Max(0.5f, bounds.extents.y - boundaryInset);
        float horizontalLength = Mathf.Max(0.5f, halfWidth * 2f - edgeCornerPadding * 2f);
        float verticalLength = Mathf.Max(0.5f, halfHeight * 2f - edgeCornerPadding * 2f);
        Vector2 center = bounds.center;

        CreateEdge(
            "Boundary_Top",
            center + Vector2.up * halfHeight,
            new Vector2(horizontalLength, boundaryThickness),
            Vector2.up
        );
        CreateEdge(
            "Boundary_Bottom",
            center + Vector2.down * halfHeight,
            new Vector2(horizontalLength, boundaryThickness),
            Vector2.down
        );
        CreateEdge(
            "Boundary_Left",
            center + Vector2.left * halfWidth,
            new Vector2(boundaryThickness, verticalLength),
            Vector2.left
        );
        CreateEdge(
            "Boundary_Right",
            center + Vector2.right * halfWidth,
            new Vector2(boundaryThickness, verticalLength),
            Vector2.right
        );

        if (createBarrierLine)
        {
            CreateBarrierLine(center, halfWidth, halfHeight);
        }

        if (createOuterHaze)
        {
            EnsureHazeSprite();
            CreateOuterHaze(center, halfWidth, halfHeight);
        }

        lastPressure = -1f;
        ApplyPressureVisual(boundaryController != null ? boundaryController.BoundaryPressure : 0f);

        if (logRebuild)
        {
            Debug.Log($"Boundary particle visual rebuilt. Center: {center}, Size: {bounds.size}", this);
        }
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generatedMap)
    {
        if (mapGenerator == null || mapGenerator == generatedMap)
        {
            mapGenerator = generatedMap;
            Rebuild();
        }
    }

    private void ResolveReferences()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
        }

        if (boundaryController == null)
        {
            boundaryController = FindFirstObjectByType<WorldWrapController>();
        }
    }

    private Bounds ResolveBounds()
    {
        if (mapGenerator != null &&
            mapGenerator.MapBounds.size.x > 0.01f &&
            mapGenerator.MapBounds.size.y > 0.01f)
        {
            return mapGenerator.MapBounds;
        }

        return new Bounds(
            new Vector3(fallbackMapCenter.x, fallbackMapCenter.y, 0f),
            new Vector3(
                Mathf.Max(1f, fallbackMapSize.x),
                Mathf.Max(1f, fallbackMapSize.y),
                1f
            )
        );
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Generated_BoundaryParticleVisual");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
    }

    private void ClearGeneratedVisuals()
    {
        particleSystems.Clear();
        hazeRenderers.Clear();
        barrierLine = null;

        if (generatedRoot == null)
        {
            return;
        }

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = generatedRoot.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void EnsureMaterials()
    {
        if (particleMaterial == null)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (particleShader == null)
            {
                particleShader = Shader.Find("Particles/Standard Unlit");
            }

            if (particleShader == null)
            {
                particleShader = Shader.Find("Sprites/Default");
            }

            if (particleShader != null)
            {
                particleMaterial = new Material(particleShader)
                {
                    name = "Runtime_MapBoundaryParticle"
                };
            }
        }

        if (lineMaterial == null)
        {
            Shader lineShader = Shader.Find("Sprites/Default");

            if (lineShader != null)
            {
                lineMaterial = new Material(lineShader)
                {
                    name = "Runtime_MapBoundaryLine"
                };
            }
        }
    }

    private void CreateEdge(string objectName, Vector2 position, Vector2 shapeSize, Vector2 outwardDirection)
    {
        GameObject edgeObject = new GameObject(objectName);
        edgeObject.transform.SetParent(generatedRoot, false);
        edgeObject.transform.position = new Vector3(position.x, position.y, transform.position.z);

        ParticleSystem particleSystem = edgeObject.AddComponent<ParticleSystem>();
        ConfigureEdgeParticleSystem(
            particleSystem,
            shapeSize,
            outwardDirection,
            particlesPerWorldUnit,
            64,
            768,
            particleLifetimeRange,
            particleSizeRange,
            outwardDriftSpeed,
            noiseStrength,
            noiseFrequency,
            idleParticleColor,
            particleMaterial,
            sortingLayerName,
            particleSortingOrder,
            ParticleSystemSimulationSpace.World,
            true,
            true,
            0
        );

        particleSystems.Add(particleSystem);
    }

    /// <summary>
    /// Configures the sparse, noisy edge particles used by the Expedition sector boundary.
    /// Raider arena walls reuse this presentation path without taking ownership of map containment.
    /// </summary>
    public static void ConfigureEdgeParticleSystem(
        ParticleSystem particleSystem,
        Vector2 shapeSize,
        Vector2 outwardDirection,
        float particlesPerWorldUnit,
        int minimumParticles,
        int maximumParticles,
        Vector2 lifetimeRange,
        Vector2 sizeRange,
        float outwardDriftSpeed,
        float noiseStrength,
        float noiseFrequency,
        Color startColor,
        Material sharedMaterial,
        string sortingLayerName,
        int sortingOrder,
        ParticleSystemSimulationSpace simulationSpace,
        bool loop,
        bool animateOverLifetime,
        int oneShotParticleCount)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.simulationSpace = simulationSpace;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.05f, Mathf.Min(lifetimeRange.x, lifetimeRange.y)),
            Mathf.Max(0.05f, Mathf.Max(lifetimeRange.x, lifetimeRange.y))
        );
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, Mathf.Min(sizeRange.x, sizeRange.y)),
            Mathf.Max(0.01f, Mathf.Max(sizeRange.x, sizeRange.y))
        );
        main.startColor = startColor;
        int resolvedMinimumParticles = Mathf.Max(1, minimumParticles);
        int resolvedMaximumParticles = Mathf.Max(resolvedMinimumParticles, maximumParticles);
        main.maxParticles = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(shapeSize.x, shapeSize.y) * Mathf.Max(1f, particlesPerWorldUnit) * 3f),
            resolvedMinimumParticles,
            resolvedMaximumParticles
        );
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        float edgeLength = Mathf.Max(shapeSize.x, shapeSize.y);
        emission.enabled = loop;
        emission.rateOverTime = loop
            ? Mathf.Max(0f, edgeLength * particlesPerWorldUnit)
            : 0f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(0.05f, shapeSize.x),
            Mathf.Max(0.05f, shapeSize.y),
            0.1f
        );

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = outwardDriftSpeed > 0f;
        velocity.space = simulationSpace;
        velocity.x = outwardDirection.x * outwardDriftSpeed;
        velocity.y = outwardDirection.y * outwardDriftSpeed;

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = noiseStrength > 0f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = noiseStrength;
        noise.frequency = Mathf.Max(0.01f, noiseFrequency);
        noise.scrollSpeed = 0.25f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = animateOverLifetime;
        if (animateOverLifetime)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.65f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = animateOverLifetime;
        if (animateOverLifetime)
        {
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.6f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(1f, 0.15f)
                )
            );
        }

        ParticleSystemRenderer targetRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        targetRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = sortingOrder;

        if (sharedMaterial != null)
        {
            targetRenderer.sharedMaterial = sharedMaterial;
        }

        if (loop)
        {
            particleSystem.Play(true);
        }
        else
        {
            particleSystem.Emit(Mathf.Clamp(oneShotParticleCount, 1, main.maxParticles));
        }
    }

    private void CreateBarrierLine(Vector2 center, float halfWidth, float halfHeight)
    {
        GameObject lineObject = new GameObject("Boundary_BarrierLine");
        lineObject.transform.SetParent(generatedRoot, false);

        barrierLine = lineObject.AddComponent<LineRenderer>();
        barrierLine.useWorldSpace = true;
        barrierLine.loop = true;
        barrierLine.positionCount = 4;
        barrierLine.SetPosition(0, center + new Vector2(-halfWidth, -halfHeight));
        barrierLine.SetPosition(1, center + new Vector2(-halfWidth, halfHeight));
        barrierLine.SetPosition(2, center + new Vector2(halfWidth, halfHeight));
        barrierLine.SetPosition(3, center + new Vector2(halfWidth, -halfHeight));
        barrierLine.startWidth = Mathf.Max(0.005f, lineWidth);
        barrierLine.endWidth = Mathf.Max(0.005f, lineWidth);
        barrierLine.numCornerVertices = 2;
        barrierLine.numCapVertices = 0;
        barrierLine.sortingLayerName = sortingLayerName;
        barrierLine.sortingOrder = lineSortingOrder;

        if (lineMaterial != null)
        {
            barrierLine.sharedMaterial = lineMaterial;
        }
    }

    private void EnsureHazeSprite()
    {
        if (hazeSprite != null)
        {
            return;
        }

        const int textureWidth = 32;
        hazeTexture = new Texture2D(textureWidth, 1, TextureFormat.RGBA32, false)
        {
            name = "Runtime_MapBoundaryHaze",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int x = 0; x < textureWidth; x++)
        {
            float normalized = x / (textureWidth - 1f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, normalized);
            hazeTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
        }

        hazeTexture.Apply(false, false);
        hazeSprite = Sprite.Create(
            hazeTexture,
            new Rect(0f, 0f, textureWidth, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        hazeSprite.name = "Runtime_MapBoundaryHazeSprite";
    }

    private void CreateOuterHaze(Vector2 center, float halfWidth, float halfHeight)
    {
        float width = Mathf.Max(0.5f, outerHazeWidth);
        float horizontalLength = halfWidth * 2f + width * 2f;
        float verticalLength = halfHeight * 2f + width * 2f;

        CreateHazeStrip("Boundary_HazeRight", center + Vector2.right * (halfWidth + width * 0.5f), width, verticalLength, 0f);
        CreateHazeStrip("Boundary_HazeLeft", center + Vector2.left * (halfWidth + width * 0.5f), width, verticalLength, 180f);
        CreateHazeStrip("Boundary_HazeTop", center + Vector2.up * (halfHeight + width * 0.5f), width, horizontalLength, 90f);
        CreateHazeStrip("Boundary_HazeBottom", center + Vector2.down * (halfHeight + width * 0.5f), width, horizontalLength, -90f);
    }

    private void CreateHazeStrip(string objectName, Vector2 center, float width, float length, float angle)
    {
        GameObject hazeObject = new GameObject(objectName);
        hazeObject.transform.SetParent(generatedRoot, false);
        hazeObject.transform.position = new Vector3(center.x, center.y, transform.position.z);
        hazeObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer hazeRenderer = hazeObject.AddComponent<SpriteRenderer>();
        hazeRenderer.sprite = hazeSprite;
        hazeRenderer.color = idleHazeColor;
        hazeRenderer.sortingLayerName = sortingLayerName;
        hazeRenderer.sortingOrder = hazeSortingOrder;
        hazeObject.transform.localScale = new Vector3(
            width / Mathf.Max(0.001f, hazeSprite.bounds.size.x),
            length / Mathf.Max(0.001f, hazeSprite.bounds.size.y),
            1f
        );
        hazeRenderers.Add(hazeRenderer);
    }

    private void ApplyPressureVisual(float pressure)
    {
        pressure = Mathf.Clamp01(pressure);
        Color particleColor = Color.Lerp(idleParticleColor, pressuredParticleColor, pressure);
        Color targetLineColor = Color.Lerp(idleLineColor, pressuredLineColor, pressure);

        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem system = particleSystems[i];

            if (system == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = system.main;
            main.startColor = particleColor;
        }

        if (barrierLine != null)
        {
            barrierLine.startColor = targetLineColor;
            barrierLine.endColor = targetLineColor;
        }

        Color targetHazeColor = Color.Lerp(idleHazeColor, pressuredHazeColor, pressure);
        for (int i = 0; i < hazeRenderers.Count; i++)
        {
            if (hazeRenderers[i] != null)
            {
                hazeRenderers[i].color = targetHazeColor;
            }
        }
    }
}
