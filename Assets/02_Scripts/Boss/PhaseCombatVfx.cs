using UnityEngine;

// Passive pooled presentation. The existing boss scheduler supplies all time.
[DisallowMultipleComponent]
public sealed class PhaseCombatVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameDurations;
    public bool IsVisible => visual != null && visual.enabled;
    public void Sample(float elapsed, int first = 0, int count = 0, bool holdLast = false)
    {
        int last = count > 0 ? Mathf.Min(frames.Length, first + count) : frames.Length;
        for (int i = first; i < last; i++)
        {
            if (elapsed < frameDurations[i] || (holdLast && i == last - 1))
            { visual.sprite = frames[i]; visual.enabled = true; return; }
            elapsed -= frameDurations[i];
        }
        Clear();
    }
    public void Clear() { if (visual != null) visual.enabled = false; }
    private void OnDisable() => Clear();
}
