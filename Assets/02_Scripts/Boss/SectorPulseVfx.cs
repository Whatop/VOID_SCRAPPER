using UnityEngine;

// Short pooled Region A ring/cue; owns presentation lifetime only, never combat.
[DisallowMultipleComponent]
public sealed class SectorPulseVfx : MonoBehaviour
{
    [SerializeField] private LineRenderer ring;
    [SerializeField] private SpriteRenderer glyph;
    [SerializeField] private bool holdOpacity;
    private GameObject owner;
    private Color color;
    private float elapsed, duration, fromRadius, toRadius;
    private bool unscaled;
    public bool IsPlayingFor(GameObject source) => owner == source && gameObject.activeInHierarchy;
    public void Play(GameObject source, Color tint, float from, float to, float seconds, bool useUnscaledTime)
    {
        owner = source; color = tint; elapsed = 0; duration = Mathf.Max(.05f, seconds);
        fromRadius = from; toRadius = to; unscaled = useUnscaledTime;
        Sample(0);
    }
    private void Update()
    {
        if (owner == null || !owner.activeInHierarchy) { Release(); return; }
        if (!unscaled && GameplayPauseManager.IsPaused) return;
        elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        if (elapsed >= duration) { Release(); return; }
        Sample(elapsed / duration);
    }
    public void Sample(float progress)
    {
        float radius = Mathf.Lerp(fromRadius, toRadius, Mathf.SmoothStep(0, 1, progress));
        ring.transform.localScale = Vector3.one * radius;
        Color tint = color; tint.a *= holdOpacity ? 1 : 1 - progress;
        ring.startColor = ring.endColor = tint; ring.enabled = true;
        if (glyph != null) { glyph.color = tint; glyph.enabled = true; }
    }
    public void Release()
    {
        if (!gameObject.activeSelf) return;
        if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
        else gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        owner = null; elapsed = 0;
        if (ring != null) ring.enabled = false;
        if (glyph != null) glyph.enabled = false;
    }
}
