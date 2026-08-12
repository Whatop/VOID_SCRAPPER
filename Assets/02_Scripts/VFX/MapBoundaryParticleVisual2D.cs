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

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int particleSortingOrder = -20;
    [SerializeField] private int lineSortingOrder = -19;

    [Header("Runtime")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool logRebuild;

    private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>(4);
    private LineRenderer barrierLine;
    private Material particleMaterial;
    private Material lineMaterial;
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
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.05f, Mathf.Min(particleLifetimeRange.x, particleLifetimeRange.y)),
            Mathf.Max(0.05f, Mathf.Max(particleLifetimeRange.x, particleLifetimeRange.y))
        );
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, Mathf.Min(particleSizeRange.x, particleSizeRange.y)),
            Mathf.Max(0.01f, Mathf.Max(particleSizeRange.x, particleSizeRange.y))
        );
        main.startColor = idleParticleColor;
        main.maxParticles = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(shapeSize.x, shapeSize.y) * Mathf.Max(1f, particlesPerWorldUnit) * 3f),
            64,
            768
        );
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        float edgeLength = Mathf.Max(shapeSize.x, shapeSize.y);
        emission.rateOverTime = Mathf.Max(0f, edgeLength * particlesPerWorldUnit);

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
        velocity.space = ParticleSystemSimulationSpace.World;
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
        colorOverLifetime.enabled = true;
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

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0.15f)
            )
        );

        ParticleSystemRenderer targetRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        targetRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = particleSortingOrder;

        if (particleMaterial != null)
        {
            targetRenderer.sharedMaterial = particleMaterial;
        }

        particleSystems.Add(particleSystem);
        particleSystem.Play(true);
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
    }
}
