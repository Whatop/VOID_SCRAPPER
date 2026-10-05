using UnityEngine;

// Passive sprite playback. The existing attack/intro owner supplies elapsed time.
// Saved renderers are reused; no effect coroutine, Update, material or mesh creation.
[DisallowMultipleComponent]
public sealed class SectorSupportVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] purpleFrames;
    [SerializeField] private float[] durations;
    public SpriteRenderer Visual => visual;
    public void Sample(float elapsed, bool loop = false, bool purple = false)
    {
        var source = purple && purpleFrames != null && purpleFrames.Length > 0 ? purpleFrames : frames;
        if (visual == null || source == null || source.Length == 0 || durations == null) return;
        float total = 0;
        for (int i = 0; i < durations.Length; i++) total += durations[i];
        if (elapsed < 0 || total <= 0 || (!loop && elapsed >= total)) { Clear(); return; }
        if (loop) elapsed %= total;
        int frame = 0;
        while (frame < durations.Length - 1 && elapsed >= durations[frame]) elapsed -= durations[frame++];
        visual.sprite = source[Mathf.Min(frame, source.Length - 1)]; visual.enabled = true;
    }
    public void Clear() { if (visual != null) visual.enabled = false; }
    private void OnDisable() => Clear();
}
