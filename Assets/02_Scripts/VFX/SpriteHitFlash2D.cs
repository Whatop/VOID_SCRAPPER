using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpriteHitFlash2D : MonoBehaviour
{
    private sealed class OverlayEntry
    {
        public SpriteRenderer source;
        public SpriteRenderer overlay;
    }

    [Header("Source")]
    [SerializeField] private bool autoCollectRenderers = true;
    [SerializeField] private SpriteRenderer[] sourceRenderers;
    [SerializeField] private bool includeInactiveRenderers = true;

    [Header("Flash")]
    [Min(0.01f)]
    [SerializeField] private float duration = 0.055f;
    [Range(0f, 1f)]
    [SerializeField] private float baseAlpha = 0.92f;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.45f, 0.72f),
        new Keyframe(1f, 0f)
    );

    [Header("Rendering")]
    [SerializeField] private Material overlayMaterial;
    [SerializeField] private string resourcesMaterialPath = "VFX/M_SpriteWhiteFlash";
    [SerializeField] private int sortingOrderOffset = 1;

    private readonly List<OverlayEntry> entries = new List<OverlayEntry>(4);
    private Coroutine flashRoutine;
    private Material runtimeFallbackMaterial;

    private void Awake()
    {
        ResolveMaterial();
        BuildOverlays();
        SetOverlayAlpha(0f, false);
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetOverlayAlpha(0f, false);
    }

    private void OnDestroy()
    {
        if (runtimeFallbackMaterial != null)
        {
            Destroy(runtimeFallbackMaterial);
            runtimeFallbackMaterial = null;
        }
    }

    public void Play(float intensity = 1f)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (entries.Count == 0)
        {
            BuildOverlays();
        }

        if (entries.Count == 0)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(Mathf.Clamp(intensity, 0.35f, 2.5f)));
    }

    [ContextMenu("Rebuild Hit Flash Overlays")]
    public void RebuildOverlays()
    {
        ClearOverlays();
        ResolveMaterial();
        BuildOverlays();
        SetOverlayAlpha(0f, false);
    }

    private IEnumerator FlashRoutine(float intensity)
    {
        RefreshOverlayProperties();

        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        float targetAlpha = Mathf.Clamp01(baseAlpha * Mathf.Lerp(0.8f, 1.15f, intensity / 2.5f));

        SetOverlayAlpha(targetAlpha, true);

        while (timer < safeDuration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / safeDuration);
            float curve = alphaCurve != null ? alphaCurve.Evaluate(ratio) : 1f - ratio;
            SetOverlayAlpha(targetAlpha * Mathf.Clamp01(curve), true);
            yield return null;
        }

        SetOverlayAlpha(0f, false);
        flashRoutine = null;
    }

    private void BuildOverlays()
    {
        if (entries.Count > 0)
        {
            return;
        }

        if ((sourceRenderers == null || sourceRenderers.Length == 0) && autoCollectRenderers)
        {
            sourceRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactiveRenderers);
        }

        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return;
        }

        Material material = ResolveMaterial();
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            SpriteRenderer source = sourceRenderers[i];
            if (source == null)
            {
                continue;
            }

            GameObject overlayObject = new GameObject($"{source.gameObject.name}_HitFlashOverlay");
            overlayObject.transform.SetParent(source.transform, false);
            overlayObject.layer = source.gameObject.layer;

            SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
            overlay.sharedMaterial = material;
            CopyRendererState(source, overlay);
            overlay.sortingOrder = source.sortingOrder + sortingOrderOffset;
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.enabled = false;

            entries.Add(new OverlayEntry
            {
                source = source,
                overlay = overlay
            });
        }
    }

    private void RefreshOverlayProperties()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            OverlayEntry entry = entries[i];
            if (entry == null || entry.source == null || entry.overlay == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            CopyRendererState(entry.source, entry.overlay);
            entry.overlay.sortingOrder = entry.source.sortingOrder + sortingOrderOffset;
        }
    }

    private static void CopyRendererState(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.tileMode = source.tileMode;
        target.maskInteraction = source.maskInteraction;
        target.sortingLayerID = source.sortingLayerID;
    }

    private void SetOverlayAlpha(float alpha, bool enabled)
    {
        alpha = Mathf.Clamp01(alpha);

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            OverlayEntry entry = entries[i];
            if (entry == null || entry.overlay == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.overlay.enabled = enabled && entry.source != null && entry.source.enabled && entry.overlay.sprite != null;
            entry.overlay.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private Material ResolveMaterial()
    {
        if (overlayMaterial != null)
        {
            return overlayMaterial;
        }

        if (!string.IsNullOrWhiteSpace(resourcesMaterialPath))
        {
            overlayMaterial = Resources.Load<Material>(resourcesMaterialPath);
            if (overlayMaterial != null)
            {
                return overlayMaterial;
            }
        }

        if (runtimeFallbackMaterial != null)
        {
            return runtimeFallbackMaterial;
        }

        Shader shader = Shader.Find("VOID SCRAPPER/Sprite White Flash Overlay");
        if (shader == null)
        {
            return null;
        }

        runtimeFallbackMaterial = new Material(shader)
        {
            name = "Runtime Sprite White Flash",
            hideFlags = HideFlags.HideAndDontSave
        };
        return runtimeFallbackMaterial;
    }

    private void ClearOverlays()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SpriteRenderer overlay = entries[i]?.overlay;
            if (overlay == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(overlay.gameObject);
            }
            else
            {
                DestroyImmediate(overlay.gameObject);
            }
        }

        entries.Clear();
    }
}
