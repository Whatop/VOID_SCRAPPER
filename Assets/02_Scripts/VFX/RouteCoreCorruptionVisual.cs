using DG.Tweening;
using UnityEngine;

/// <summary>Optional, renderer-local polish. Never gates damage, radar or progression.</summary>
[DisallowMultipleComponent]
public sealed class RouteCoreCorruptionVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer overlay;
    [SerializeField, Range(.15f, .3f)] private float revealDuration = .2f;
    [SerializeField, Range(.15f, .3f)] private float hideDuration = .2f;
    [SerializeField, Range(.3f, .6f)] private float purificationDuration = .45f;
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int CorruptionId = Shader.PropertyToID("_CorruptionAmount");
    private static readonly int BaseId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_CorruptionColor");
    private static readonly Color Purple = new Color(.8f, .3f, 1f, 1f);
    private static readonly Color Acquisition = new Color(.2f, .85f, 1f, 1f);
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock baseline;
    private Tween transition;
    private float reveal;
    private bool captured;
    private bool baselineEnabled;
    private bool warned;
    public float PurificationDuration => purificationDuration;
    public bool HasAuthoredOverlay => overlay != null && overlay.sharedMaterial != null &&
        overlay.sharedMaterial.shader != null && overlay.sharedMaterial.HasProperty(RevealId) &&
        overlay.sharedMaterial.HasProperty(CorruptionId) && overlay.sharedMaterial.HasProperty(BaseId) &&
        overlay.sharedMaterial.HasProperty(ColorId);

    private bool Prepare()
    {
        if (!HasAuthoredOverlay || !overlay.sharedMaterial.shader.isSupported)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("RouteCoreDeck/PurpleCorruptionTarget/CorruptionOverlay: missing or unsupported authored corruption material; original sprite presentation remains active.", this);
            }
#endif
            if (overlay != null) overlay.enabled = false;
            return false;
        }
        if (block == null) { block = new MaterialPropertyBlock(); baseline = new MaterialPropertyBlock(); }
        if (!captured)
        {
            overlay.GetPropertyBlock(baseline);
            baselineEnabled = overlay.enabled;
            captured = true;
        }
        overlay.GetPropertyBlock(block); // Preserve unrelated renderer properties, including sprite data.
        overlay.enabled = true;
        transition?.Kill(); transition = null;
        return true;
    }

    public void Hidden(bool immediate)
    {
        if (!Prepare()) return;
        float from = immediate ? 0f : reveal;
        Animate(hideDuration, t => Apply(Mathf.Lerp(from, 0, t), 1, 1, Purple), immediate);
    }

    public void Exposed()
    {
        if (!Prepare()) return;
        float from = reveal;
        Animate(revealDuration, t => Apply(Mathf.Lerp(from, 1, t), Mathf.Lerp(1, .35f, t), 1,
            Color.Lerp(Acquisition, Purple, t)), false);
    }

    public bool Purify()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !Prepare()) return false;
        // Actor damage/radar/campaign cleanup has already completed. Only the overlay remains.
        Animate(purificationDuration, t => Apply(1 - t, 1, 1 - t, Purple), false);
        return true;
    }

    private void Animate(float duration, TweenCallback<float> update, bool immediate)
    {
        if (immediate || !Application.isPlaying) { update(1); return; }
        update(0);
        transition = DOVirtual.Float(0, 1, duration, update).SetEase(Ease.Linear)
            .SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void Apply(float amount, float corruption, float opacity, Color color)
    {
        if (overlay == null) return;
        reveal = amount;
        block.SetFloat(RevealId, amount);
        block.SetFloat(CorruptionId, corruption);
        block.SetColor(BaseId, new Color(1, 1, 1, opacity));
        block.SetColor(ColorId, color);
        overlay.SetPropertyBlock(block);
    }

    public void ResetPresentation()
    {
        transition?.Kill(); transition = null;
        if (captured && overlay != null)
        {
            overlay.SetPropertyBlock(baseline);
            overlay.enabled = baselineEnabled;
        }
        captured = false;
        reveal = 0;
    }
    private void OnDisable() => ResetPresentation();
    private void OnDestroy() => ResetPresentation();
}
