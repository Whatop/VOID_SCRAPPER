using UnityEngine;

[DisallowMultipleComponent]
public sealed class RaiderCoverBlastTelegraph : MonoBehaviour
{
    private const string PresentationRootObjectName = "PresentationRoot";
    private const string BaseRingObjectName = "OuterDangerRing";
    private const string ProgressRingObjectName = "ProgressRing";
    private const string ProgressSweepObjectName = "ProgressSweep";
    private const string RadialFillObjectName = "RadialChargeFill";
    private const string FallbackMaterialResourcePath = "VFX/M_SpriteWhiteFlash";
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private static Material sharedFallbackMaterial;

    [Header("References")]
    [SerializeField] private Transform presentationRoot;
    [SerializeField] private LineRenderer baseRing;
    [SerializeField] private LineRenderer progressRing;
    [SerializeField] private LineRenderer progressSweepLine;
    [SerializeField] private MeshFilter radialFillFilter;
    [SerializeField] private MeshRenderer radialFillRenderer;
    [SerializeField] private Material lineMaterial;

    [Header("Shape")]
    [SerializeField, Min(0.01f)] private float baseLineWidth = 0.24f;
    [SerializeField, Min(0.01f)] private float progressLineWidth = 0.42f;
    [SerializeField, Min(0.01f)] private float progressSweepLineWidth = 0.14f;
    [SerializeField, Range(24, 128)] private int segmentCount = 64;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 2;
    [SerializeField] private Color baseColor = new Color(0.72f, 0.05f, 0.01f, 0.52f);
    [SerializeField] private Color fillColor = new Color(1f, 0.08f, 0.015f, 0.16f);
    [SerializeField] private Color progressColor = new Color(1f, 0.2f, 0.035f, 0.98f);
    [SerializeField] private Color progressSweepColor = new Color(1f, 0.58f, 0.1f, 0.96f);
    [SerializeField] private Color finalWarningColor = new Color(1f, 0.94f, 0.62f, 1f);
    [SerializeField, Range(0.05f, 0.35f)] private float finalWarningFillAlpha = 0.28f;

    private Mesh radialFillMesh;
    private Vector3[] fillVertices;
    private int[] fillTriangles;
    private Color32[] fillVertexColors;
    private MaterialPropertyBlock fillPropertyBlock;
    private float radius = 6f;
    private float progress;
    private int cachedSegmentCount;
    private bool presentationComponentsReady;
    private bool presentationInitializationAttempted;
    private bool materialErrorLogged;
    private bool baseRingErrorLogged;
    private bool progressRingErrorLogged;
    private bool sweepErrorLogged;
    private bool radialFillErrorLogged;
    private bool presentationVisible;

    private void Awake()
    {
        EnsurePresentationComponents();
        ResetPresentationState();
        SetPresentationVisible(false);
    }

    private void OnEnable()
    {
        EnsurePresentationComponents();
    }

    private void OnDisable()
    {
        ResetPresentationState();
        SetPresentationVisible(false);
    }

    private void OnDestroy()
    {
        if (radialFillFilter != null && radialFillFilter.sharedMesh == radialFillMesh)
        {
            radialFillFilter.sharedMesh = null;
        }

        if (radialFillMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(radialFillMesh);
            }
            else
            {
                DestroyImmediate(radialFillMesh);
            }
        }

        radialFillMesh = null;
        presentationComponentsReady = false;
    }

    public void Configure(Vector3 attackCenter, float worldRadius)
    {
        EnsurePresentationComponents();
        transform.position = attackCenter;
        radius = Mathf.Max(0.5f, worldRadius);
        BuildBaseRing();
        UpdateFillMeshBounds();
        ApplyProgress(0f);
    }

    public void BeginCharge(Vector3 attackCenter, float worldRadius)
    {
        Configure(attackCenter, worldRadius);
        gameObject.SetActive(true);
        SetPresentationVisible(true);
        ApplyProgress(0f);
    }

    public void SetChargeProgress(float normalizedProgress)
    {
        if (!presentationInitializationAttempted)
        {
            EnsurePresentationComponents();
        }

        ApplyProgress(normalizedProgress);
    }

    public void HideImmediate()
    {
        ResetPresentationState();
        SetPresentationVisible(false);

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private bool EnsurePresentationComponents()
    {
        presentationInitializationAttempted = true;

        if (presentationComponentsReady &&
            presentationRoot != null &&
            baseRing != null &&
            progressRing != null &&
            progressSweepLine != null &&
            radialFillFilter != null &&
            radialFillRenderer != null &&
            radialFillMesh != null)
        {
            return true;
        }

        EnsurePresentationRoot();
        Material resolvedMaterial = ResolveSharedMaterial();

        bool baseReady = ConfigureRequiredLineRenderer(
            ref baseRing,
            BaseRingObjectName,
            "outer danger ring",
            baseLineWidth,
            sortingOrder + 1,
            resolvedMaterial,
            ref baseRingErrorLogged
        );
        bool progressReady = ConfigureRequiredLineRenderer(
            ref progressRing,
            ProgressRingObjectName,
            "progress ring",
            progressLineWidth,
            sortingOrder + 2,
            resolvedMaterial,
            ref progressRingErrorLogged
        );
        bool sweepReady = ConfigureRequiredLineRenderer(
            ref progressSweepLine,
            ProgressSweepObjectName,
            "radial sweep line",
            progressSweepLineWidth,
            sortingOrder + 2,
            resolvedMaterial,
            ref sweepErrorLogged
        );
        if (progressSweepLine != null)
        {
            progressSweepLine.positionCount = 0;
            progressSweepLine.enabled = false;
        }

        bool fillReady = EnsureRadialFillRenderer(resolvedMaterial) &&
                         EnsureRadialFillMesh();

        presentationComponentsReady = baseReady && progressReady && sweepReady && fillReady;
        return presentationComponentsReady;
    }

    private void EnsurePresentationRoot()
    {
        if (presentationRoot == null)
        {
            presentationRoot = transform.Find(PresentationRootObjectName);
        }

        if (presentationRoot == null)
        {
            GameObject rootObject = new GameObject(PresentationRootObjectName);
            presentationRoot = rootObject.transform;
        }

        ResetChildTransform(presentationRoot, transform);
    }

    private bool ConfigureRequiredLineRenderer(
        ref LineRenderer renderer,
        string objectName,
        string rendererRole,
        float width,
        int order,
        Material material,
        ref bool errorLogged)
    {
        if (renderer == null)
        {
            renderer = FindOrCreateLineRenderer(objectName);
        }

        if (renderer == null)
        {
            LogMissingRenderer(rendererRole, ref errorLogged);
            return false;
        }

        ResetChildTransform(renderer.transform, presentationRoot);
        renderer.useWorldSpace = false;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.widthMultiplier = Mathf.Max(0.01f, width);
        renderer.numCornerVertices = 2;
        renderer.numCapVertices = 2;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = order;

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        return true;
    }

    private LineRenderer FindOrCreateLineRenderer(string objectName)
    {
        if (presentationRoot == null)
        {
            return null;
        }

        Transform existing = presentationRoot.Find(objectName);
        GameObject rendererObject;

        if (existing != null)
        {
            rendererObject = existing.gameObject;
        }
        else
        {
            rendererObject = new GameObject(objectName);
            rendererObject.transform.SetParent(presentationRoot, false);
        }

        LineRenderer renderer = rendererObject.GetComponent<LineRenderer>();
        if (renderer == null)
        {
            renderer = rendererObject.AddComponent<LineRenderer>();
        }

        return renderer;
    }

    private bool EnsureRadialFillRenderer(Material material)
    {
        GameObject fillObject = null;

        if (radialFillFilter != null)
        {
            fillObject = radialFillFilter.gameObject;
        }
        else if (radialFillRenderer != null)
        {
            fillObject = radialFillRenderer.gameObject;
        }
        else if (presentationRoot != null)
        {
            Transform existing = presentationRoot.Find(RadialFillObjectName);
            fillObject = existing != null ? existing.gameObject : null;
        }

        if (fillObject == null && presentationRoot != null)
        {
            fillObject = new GameObject(RadialFillObjectName);
            fillObject.transform.SetParent(presentationRoot, false);
        }

        if (fillObject == null)
        {
            LogMissingRenderer("expanding-disc mesh", ref radialFillErrorLogged);
            return false;
        }

        radialFillFilter ??= fillObject.GetComponent<MeshFilter>();
        radialFillFilter ??= fillObject.AddComponent<MeshFilter>();
        radialFillRenderer ??= fillObject.GetComponent<MeshRenderer>();
        radialFillRenderer ??= fillObject.AddComponent<MeshRenderer>();

        if (radialFillFilter == null || radialFillRenderer == null)
        {
            LogMissingRenderer("expanding-disc mesh", ref radialFillErrorLogged);
            return false;
        }

        ResetChildTransform(fillObject.transform, presentationRoot);
        radialFillRenderer.sortingLayerName = sortingLayerName;
        radialFillRenderer.sortingOrder = sortingOrder;

        if (material != null)
        {
            radialFillRenderer.sharedMaterial = material;
        }

        fillPropertyBlock ??= new MaterialPropertyBlock();
        return true;
    }

    private bool EnsureRadialFillMesh()
    {
        if (radialFillFilter == null)
        {
            LogMissingRenderer("expanding-disc MeshFilter", ref radialFillErrorLogged);
            return false;
        }

        int segments = Mathf.Clamp(segmentCount, 24, 128);
        if (radialFillMesh != null && cachedSegmentCount == segments)
        {
            return true;
        }

        if (radialFillMesh != null)
        {
            radialFillFilter.sharedMesh = null;

            if (Application.isPlaying)
            {
                Destroy(radialFillMesh);
            }
            else
            {
                DestroyImmediate(radialFillMesh);
            }
        }

        cachedSegmentCount = segments;
        fillVertices = new Vector3[segments + 2];
        fillTriangles = new int[segments * 3];
        fillVertexColors = new Color32[fillVertices.Length];

        for (int i = 0; i < fillVertexColors.Length; i++)
        {
            fillVertexColors[i] = Color.white;
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int triangleIndex = segment * 3;
            fillTriangles[triangleIndex] = 0;
            fillTriangles[triangleIndex + 1] = segment + 1;
            fillTriangles[triangleIndex + 2] = segment + 2;
        }

        fillVertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            fillVertices[i + 1] = ResolveCirclePoint(i / (float)segments, 1f);
        }

        radialFillMesh = new Mesh
        {
            name = "RaiderCoverBlastExpandingDisc"
        };
        radialFillMesh.vertices = fillVertices;
        radialFillMesh.colors32 = fillVertexColors;
        radialFillMesh.SetTriangles(fillTriangles, 0, false);
        radialFillMesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0.2f));
        radialFillFilter.sharedMesh = radialFillMesh;
        return true;
    }

    private Material ResolveSharedMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        lineMaterial = ResolveMaterialFromExistingRenderer();
        if (lineMaterial == null)
        {
            sharedFallbackMaterial ??= Resources.Load<Material>(FallbackMaterialResourcePath);
            lineMaterial = sharedFallbackMaterial;
        }

        if (lineMaterial == null && !materialErrorLogged)
        {
            materialErrorLogged = true;
            Debug.LogError(
                $"Raider Cover Blast telegraph '{name}' could not resolve its shared presentation material. " +
                "Renderer geometry will remain available, but the affected layer may not render.",
                this
            );
        }

        return lineMaterial;
    }

    private Material ResolveMaterialFromExistingRenderer()
    {
        if (baseRing != null && baseRing.sharedMaterial != null)
        {
            return baseRing.sharedMaterial;
        }

        if (progressRing != null && progressRing.sharedMaterial != null)
        {
            return progressRing.sharedMaterial;
        }

        if (progressSweepLine != null && progressSweepLine.sharedMaterial != null)
        {
            return progressSweepLine.sharedMaterial;
        }

        return radialFillRenderer != null ? radialFillRenderer.sharedMaterial : null;
    }

    private void BuildBaseRing()
    {
        if (baseRing == null)
        {
            return;
        }

        int segments = Mathf.Clamp(segmentCount, 24, 128);
        baseRing.loop = true;
        baseRing.positionCount = segments;
        baseRing.startColor = baseColor;
        baseRing.endColor = baseColor;

        for (int i = 0; i < segments; i++)
        {
            float normalized = i / (float)segments;
            baseRing.SetPosition(i, ResolveCirclePoint(normalized, radius));
        }
    }

    private void ApplyProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        float warningStrength = ResolveFinalWarningStrength(progress);
        Color resolvedProgressColor = Color.Lerp(
            progressColor,
            finalWarningColor,
            warningStrength
        );
        Color resolvedBaseColor = Color.Lerp(
            baseColor,
            WithAlpha(finalWarningColor, baseColor.a + 0.18f),
            warningStrength
        );
        Color resolvedFillColor = Color.Lerp(fillColor, finalWarningColor, warningStrength);
        resolvedFillColor.a = Mathf.Lerp(
            fillColor.a,
            finalWarningFillAlpha,
            warningStrength
        );

        BuildExpandingLeadingEdge();
        ApplyExpandingFieldRadius(progress);
        ApplyFillColor(resolvedFillColor);

        if (progressRing != null)
        {
            progressRing.widthMultiplier = progressLineWidth *
                                           Mathf.Lerp(1f, 1.45f, warningStrength);
            progressRing.startColor = resolvedProgressColor;
            progressRing.endColor = resolvedProgressColor;
        }

        if (baseRing != null)
        {
            baseRing.startColor = resolvedBaseColor;
            baseRing.endColor = resolvedBaseColor;
        }

        UpdateLayerVisibility();
    }

    private void BuildExpandingLeadingEdge()
    {
        if (progressRing == null)
        {
            return;
        }

        if (progress <= 0.0001f)
        {
            progressRing.positionCount = 0;
            return;
        }

        int segments = Mathf.Clamp(segmentCount, 24, 128);
        float displayedRadius = radius * progress;
        progressRing.loop = true;
        progressRing.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            progressRing.SetPosition(
                i,
                ResolveCirclePoint(i / (float)segments, displayedRadius)
            );
        }
    }

    private void ApplyExpandingFieldRadius(float normalizedProgress)
    {
        if (radialFillFilter == null)
        {
            return;
        }

        float displayedRadius = radius * Mathf.Clamp01(normalizedProgress);
        radialFillFilter.transform.localScale = new Vector3(
            displayedRadius,
            displayedRadius,
            1f
        );
    }

    private void UpdateFillMeshBounds()
    {
        if (radialFillMesh == null)
        {
            return;
        }

        radialFillMesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0.2f));
    }

    private void ApplyFillColor(Color color)
    {
        if (radialFillRenderer == null || fillPropertyBlock == null)
        {
            return;
        }

        fillPropertyBlock.Clear();
        fillPropertyBlock.SetColor(ColorPropertyId, color);
        radialFillRenderer.SetPropertyBlock(fillPropertyBlock);
    }

    private void ResetPresentationState()
    {
        progress = 0f;

        if (baseRing != null)
        {
            baseRing.widthMultiplier = baseLineWidth;
            baseRing.startColor = baseColor;
            baseRing.endColor = baseColor;
        }

        if (progressRing != null)
        {
            progressRing.positionCount = 0;
            progressRing.widthMultiplier = progressLineWidth;
            progressRing.startColor = progressColor;
            progressRing.endColor = progressColor;
        }

        if (progressSweepLine != null)
        {
            progressSweepLine.positionCount = 0;
            progressSweepLine.widthMultiplier = progressSweepLineWidth;
            progressSweepLine.startColor = WithAlpha(progressSweepColor, 0f);
            progressSweepLine.endColor = progressSweepColor;
        }

        if (radialFillFilter != null)
        {
            radialFillFilter.transform.localScale = new Vector3(0f, 0f, 1f);
        }

        ApplyFillColor(fillColor);
        UpdateLayerVisibility();
    }

    private void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        UpdateLayerVisibility();
    }

    private void UpdateLayerVisibility()
    {
        bool progressVisible = presentationVisible && progress > 0.0001f;

        if (baseRing != null)
        {
            baseRing.enabled = presentationVisible;
        }

        if (progressRing != null)
        {
            progressRing.enabled = progressVisible;
        }

        if (progressSweepLine != null)
        {
            progressSweepLine.enabled = false;
        }

        if (radialFillRenderer != null)
        {
            radialFillRenderer.enabled = progressVisible;
        }
    }

    private void LogMissingRenderer(string rendererRole, ref bool alreadyLogged)
    {
        if (alreadyLogged)
        {
            return;
        }

        alreadyLogged = true;
        Debug.LogError(
            $"Raider Cover Blast telegraph '{name}' could not resolve or create its {rendererRole}. " +
            "That optional presentation layer was skipped; the remaining telegraph layers will continue.",
            this
        );
    }

    private static void ResetChildTransform(Transform child, Transform parent)
    {
        if (child == null || parent == null)
        {
            return;
        }

        if (child.parent != parent)
        {
            child.SetParent(parent, false);
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
    }

    private static Vector3 ResolveCirclePoint(float normalized, float worldRadius)
    {
        float radians = -Mathf.Clamp01(normalized) * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * worldRadius;
    }

    private static float ResolveFinalWarningStrength(float normalizedProgress)
    {
        if (normalizedProgress < 0.8f)
        {
            return 0f;
        }

        float finalProgress = Mathf.InverseLerp(0.8f, 1f, normalizedProgress);
        float pulse = 0.5f + 0.5f * Mathf.Sin(finalProgress * Mathf.PI * 8f);
        return Mathf.Lerp(0.45f, 1f, finalProgress) * Mathf.Lerp(0.75f, 1f, pulse);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
