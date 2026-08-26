using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RaiderArenaBoundaryPresentation : MonoBehaviour
{
    private const string HazeRendererObjectName = "RaiderBoundaryHaze";
    private const string FragmentSystemObjectName = "RaiderBoundaryFragments";
    private const string LegacyFieldObjectName = "RaiderBoundaryField";
    private const string LegacyBodyObjectName = "RaiderBoundaryBody";
    private const string LegacyNoiseObjectName = "RaiderBoundaryNoise";

    private LineRenderer coreLine;
    private LineRenderer hazeLine;
    private ParticleSystem fragmentSystem;
    private Tween pulseTween;
    private Color coreColor;
    private Color hazeColor;

    private void OnDisable()
    {
        StopPulse();
        StopFragments();
    }

    private void OnDestroy()
    {
        StopPulse();
        coreLine = null;
        hazeLine = null;
        fragmentSystem = null;
    }

    public bool Configure(
        LineRenderer authoritativeCoreLine,
        float length,
        Material material,
        string sortingLayerName,
        int sortingOrder,
        Color brightCoreColor,
        Color subtleHazeColor,
        Color pixelFragmentColor,
        float coreWidth,
        float hazeWidth,
        float fragmentBandWidth,
        float pulseDuration)
    {
        StopPulse();

        if (authoritativeCoreLine == null)
        {
            Debug.LogError(
                $"Raider boundary '{name}' is missing its authoritative BossArenaLaserWall LineRenderer. " +
                "Physical containment remains active, but Raider boundary visuals were skipped.",
                this
            );
            DisableEnhancedRenderers();
            return false;
        }

        coreLine = authoritativeCoreLine;
        if (!EnsurePresentationComponents())
        {
            Debug.LogError(
                $"Raider boundary '{name}' could not create its Expedition-style haze/fragments. " +
                "Physical containment remains active with the thin authoritative line.",
                this
            );
            DisableEnhancedRenderers();
            return false;
        }

        DisableLegacyContinuousFieldRenderers();

        Material resolvedMaterial = material != null
            ? material
            : coreLine.sharedMaterial;
        coreColor = brightCoreColor;
        hazeColor = subtleHazeColor;

        ConfigureLine(
            hazeLine,
            length,
            Mathf.Clamp(hazeWidth, 0.15f, 0.4f),
            resolvedMaterial,
            sortingLayerName,
            sortingOrder - 2,
            hazeColor
        );
        ConfigureLine(
            coreLine,
            length,
            Mathf.Clamp(coreWidth, 0.025f, 0.05f),
            resolvedMaterial,
            sortingLayerName,
            sortingOrder,
            coreColor
        );

        int fragmentCount = Mathf.Clamp(Mathf.CeilToInt(length * 0.65f), 8, 28);
        MapBoundaryParticleVisual2D.ConfigureEdgeParticleSystem(
            fragmentSystem,
            new Vector2(length, Mathf.Clamp(fragmentBandWidth, 0.08f, 0.3f)),
            Vector2.up,
            0.65f,
            8,
            32,
            new Vector2(600f, 600f),
            new Vector2(0.06f, 0.18f),
            0f,
            0f,
            0.55f,
            pixelFragmentColor,
            resolvedMaterial,
            sortingLayerName,
            sortingOrder - 1,
            ParticleSystemSimulationSpace.Local,
            false,
            false,
            fragmentCount
        );

        pulseTween = DOVirtual.Float(
                0f,
                1f,
                Mathf.Max(0.1f, pulseDuration),
                ApplyPulse
            )
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);

        return true;
    }

    private bool EnsurePresentationComponents()
    {
        EnsureLineRenderer(ref hazeLine, HazeRendererObjectName);
        EnsureFragmentSystem();
        return hazeLine != null && fragmentSystem != null;
    }

    private void EnsureLineRenderer(ref LineRenderer renderer, string objectName)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(objectName);
            if (existing != null)
            {
                renderer = existing.GetComponent<LineRenderer>();
            }
        }

        if (renderer == null)
        {
            GameObject rendererObject = new GameObject(objectName);
            rendererObject.transform.SetParent(transform, false);
            renderer = rendererObject.AddComponent<LineRenderer>();
        }

        ResetLocalTransform(renderer != null ? renderer.transform : null);
    }

    private void EnsureFragmentSystem()
    {
        if (fragmentSystem == null)
        {
            Transform existing = transform.Find(FragmentSystemObjectName);
            if (existing != null)
            {
                fragmentSystem = existing.GetComponent<ParticleSystem>();
            }
        }

        if (fragmentSystem == null)
        {
            GameObject fragmentObject = new GameObject(FragmentSystemObjectName);
            fragmentObject.transform.SetParent(transform, false);
            fragmentSystem = fragmentObject.AddComponent<ParticleSystem>();
        }

        ResetLocalTransform(fragmentSystem != null ? fragmentSystem.transform : null);
    }

    private void DisableLegacyContinuousFieldRenderers()
    {
        DisableLineRenderer(LegacyFieldObjectName);
        DisableLineRenderer(LegacyBodyObjectName);
        DisableLineRenderer(LegacyNoiseObjectName);
    }

    private void DisableLineRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        LineRenderer renderer = child != null ? child.GetComponent<LineRenderer>() : null;
        if (renderer != null && renderer != hazeLine && renderer != coreLine)
        {
            renderer.enabled = false;
        }
    }

    private static void ConfigureLine(
        LineRenderer renderer,
        float length,
        float width,
        Material material,
        string sortingLayerName,
        int sortingOrder,
        Color color)
    {
        if (renderer == null)
        {
            return;
        }

        float halfLength = Mathf.Max(0.05f, length * 0.5f);
        renderer.enabled = true;
        renderer.useWorldSpace = false;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.positionCount = 2;
        renderer.SetPosition(0, new Vector3(-halfLength, 0f, 0f));
        renderer.SetPosition(1, new Vector3(halfLength, 0f, 0f));
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.numCapVertices = 0;
        renderer.numCornerVertices = 0;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void ApplyPulse(float normalized)
    {
        float pulse = Mathf.Clamp01(normalized);
        ApplyLineColor(
            coreLine,
            WithAlpha(coreColor, Mathf.Lerp(coreColor.a * 0.82f, coreColor.a, pulse))
        );
        ApplyLineColor(
            hazeLine,
            WithAlpha(hazeColor, Mathf.Lerp(hazeColor.a * 0.68f, hazeColor.a, pulse))
        );
    }

    private void StopPulse()
    {
        pulseTween?.Kill();
        pulseTween = null;
    }

    private void StopFragments()
    {
        if (fragmentSystem != null)
        {
            fragmentSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void DisableEnhancedRenderers()
    {
        if (hazeLine != null)
        {
            hazeLine.enabled = false;
        }

        StopFragments();
    }

    private static void ResetLocalTransform(Transform target)
    {
        if (target == null)
        {
            return;
        }

        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void ApplyLineColor(LineRenderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.startColor = color;
        renderer.endColor = color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
