using UnityEngine;

// Passive Commander-only view. The intro owns the finite formation/release clock.
[DisallowMultipleComponent]
public sealed class RaiderLockdownBoundaryPresentation : MonoBehaviour
{
    private LineRenderer core, glow;
    private ParticleSystem nodes;
    private ParticleSystem.Particle[] particles;
    private float length;
    public float Progress { get; private set; }
    public int LitNodeCount { get; private set; }
    public const float CoreWidth = .045f;
    private static readonly Color CoreColor = new Color(1, .29f, .055f, .84f);
    private static readonly Color NodeColor = new Color(1, .23f, .035f, .85f);

    public void Configure(BossArenaLaserWall wall, float edgeLength, Material material, string layer, int order)
    {
        length = edgeLength;
        core = wall.GetOrCreateLineRenderer();
        var glowObject = Child("LockdownOuterGlow");
        glow = glowObject.GetComponent<LineRenderer>();
        if (glow == null) glow = glowObject.AddComponent<LineRenderer>();
        ConfigureLine(core, material, layer, order, CoreWidth);
        ConfigureLine(glow, material, layer, order - 2, .16f);
        // Reuse the ordinary region boundary's rectangular particle renderer.
        var nodeObject = Child("LockdownEnergyNodes");
        nodes = nodeObject.GetComponent<ParticleSystem>();
        if (nodes == null) nodes = nodeObject.AddComponent<ParticleSystem>();
        int count = Mathf.Clamp(Mathf.CeilToInt(length / .75f), 4, 64);
        MapBoundaryParticleVisual2D.ConfigureEdgeParticleSystem(nodes,
            new Vector2(length, .1f), Vector2.up, 1, count, count,
            new Vector2(3600, 3600), new Vector2(.1f, .24f), 0, 0, .55f,
            NodeColor, material, layer, order + 1, ParticleSystemSimulationSpace.Local, false, false, count);
        nodes.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        var main = nodes.main; main.startSize3D = true;
        particles = new ParticleSystem.Particle[count];
        for (int i = 0; i < count; i++)
        {
            float u = (i + .5f) / count;
            particles[i] = new ParticleSystem.Particle {
                position = new Vector3((u - .5f) * length, 0, 0),
                startSize3D = new Vector3(i % 4 == 0 ? .28f : .22f, .105f, 1),
                startLifetime = 3600, remainingLifetime = 3600, startColor = NodeColor
            };
        }
        nodes.Pause(false);
        SampleFormation(0);
    }
    public void SampleFormation(float progress)
    {
        Progress = Mathf.Clamp01(progress);
        Sample(Progress, Mathf.Lerp(.35f, 1, Progress), Progress);
    }
    public void SampleRelease(float progress)
    {
        float t = Mathf.Clamp01(progress);
        // Energy dims, nodes switch off, then the connected corners separate.
        Sample(1 - Mathf.Clamp01((t - .18f) / .82f), 1 - t, 1 - Mathf.Clamp01(t / .7f));
    }
    private void Sample(float connected, float energy, float nodeProgress)
    {
        if (core == null || particles == null) return;
        Progress = connected;
        float half = length * .5f * connected;
        SetLine(core, half, WithAlpha(CoreColor, CoreColor.a * energy));
        SetLine(glow, half, new Color(1, .18f, .025f, .045f * energy * energy));
        LitNodeCount = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            float fromCenter = Mathf.Abs(((i + .5f) / particles.Length - .5f) * 2);
            float alpha = Mathf.Clamp01((nodeProgress - fromCenter) * particles.Length) * energy;
            if (alpha > .001f) LitNodeCount++;
            particles[i].startColor = WithAlpha(NodeColor, NodeColor.a * alpha);
        }
        nodes.SetParticles(particles, particles.Length);
    }
    private static void SetLine(LineRenderer line, float halfLength, Color color)
    {
        line.enabled = halfLength > .001f && color.a > .001f;
        line.SetPosition(0, Vector3.left * halfLength); line.SetPosition(1, Vector3.right * halfLength);
        line.startColor = line.endColor = color;
    }
    private GameObject Child(string name)
    {
        var existing = transform.Find(name); if (existing != null) return existing.gameObject;
        var child = new GameObject(name); child.transform.SetParent(transform, false); return child;
    }
    private static void ConfigureLine(LineRenderer line, Material material, string layer, int order, float width)
    {
        line.useWorldSpace = false; line.positionCount = 2;
        line.alignment = LineAlignment.TransformZ; line.numCapVertices = line.numCornerVertices = 0;
        line.widthCurve = AnimationCurve.Constant(0, 1, 1); line.widthMultiplier = width;
        line.sharedMaterial = material; line.sortingLayerName = layer; line.sortingOrder = order;
    }
    private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
    private void OnDisable()
    {
        if (nodes != null) nodes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (core != null) core.enabled = false; if (glow != null) glow.enabled = false;
        LitNodeCount = 0;
    }
}
