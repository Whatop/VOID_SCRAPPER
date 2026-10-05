using UnityEngine;

// A pooled Region A lane. The boss's single sequence clock owns both sprite and
// collision state; there is deliberately no Update, coroutine or delayed release.
[DisallowMultipleComponent]
public sealed class SectorPartitionLane : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private BoxCollider2D damageCollider;
    [SerializeField] private Sprite[] warningFrames;
    [SerializeField] private Sprite[] activeFrames;
    [SerializeField] private Sprite[] purpleWarningFrames, purpleActiveFrames, purpleEmitterFrames;
    [SerializeField] private SpriteRenderer purpleEmitter;
    [SerializeField] private LineRenderer solidVisual;
    [SerializeField] private Color solidColor = new Color(.85f, .24f, 1f, 1);
    private float damage, interval, nextDamageTime, bandWidth, empowerment;
    // The unused historical counter-lane can retain its line. Production spokes
    // always use the approved sprite families, never the solid purple fallback.
    private bool UsesSolidEnergy => solidVisual != null && visual == null;
    private bool UsesPurpleArt => empowerment > 0 && purpleActiveFrames != null && purpleActiveFrames.Length == 4;
    public float Empowerment => empowerment;
    public void SetEmpowerment(float blend)
    {
        empowerment = Mathf.Clamp01(blend);
        if (solidVisual != null && !UsesSolidEnergy) solidVisual.enabled = false;
    }
    private static readonly int[] ActiveBandPixels = { 6, 18, 14, 14, 18, 14, 10 };
    private static readonly int[] PurpleBandPixels = { 14, 18, 14, 10 };
    private PlayerHealth lastPlayer;
    public bool IsDamaging => damageCollider != null && damageCollider.enabled;
    public bool IsVisible => UsesSolidEnergy ? solidVisual.enabled : visual != null && visual.enabled && visual.sprite != null;

    public void Configure(Vector2 start, Vector2 end, float width, float damageAmount, float damageInterval)
    {
        Clear();
        bandWidth = width;
        if (visual != null) { visual.transform.localScale = Vector3.one; visual.drawMode = SpriteDrawMode.Tiled; }
        SetEndpoints(start, end);
        damage = damageAmount; interval = damageInterval;
        Warn(0);
    }

    // Geometry-only update: preserve hit cooldown and current visual/collision state.
    public void SetEndpoints(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        transform.SetPositionAndRotation(start, Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        transform.localScale = Vector3.one;
        float length = delta.magnitude;
        if (visual != null) visual.size = new Vector2(length, 1);
        if (solidVisual != null) { solidVisual.SetPosition(0, Vector3.zero); solidVisual.SetPosition(1, Vector3.right * length); }
        damageCollider.offset = new Vector2(length * .5f, 0);
        damageCollider.size = new Vector2(length, bandWidth);
    }

    public void Warn(float progress)
    {
        damageCollider.enabled = false;
        if (purpleEmitter != null) purpleEmitter.enabled = false;
        if (UsesSolidEnergy)
        {
            Color tint = visual == null ? solidColor : Color.Lerp(BossPatternController.SectorGreen, BossPatternController.SectorPurple, empowerment); tint.a = Mathf.Lerp(.35f, .8f, progress);
            solidVisual.startColor = solidVisual.endColor = tint; solidVisual.widthMultiplier = .065f;
            solidVisual.enabled = true; return;
        }
        if (UsesPurpleArt)
        {
            visual.transform.localScale = Vector3.one;
            visual.color = Color.white;
            Show(purpleWarningFrames, Mathf.FloorToInt(Mathf.Clamp01(progress) * 1.2f / .12f) % 4);
            return;
        }
        visual.transform.localScale = new Vector3(1, Mathf.Max(.65f, bandWidth * 2), 1);
        Show(warningFrames, Mathf.FloorToInt(Mathf.Clamp01(progress) * (warningFrames.Length - 1)));
    }

    public void Activate(float elapsed)
    {
        if (UsesSolidEnergy)
        {
            solidVisual.startColor = solidVisual.endColor = visual == null ? solidColor : Color.Lerp(BossPatternController.SectorGreen, BossPatternController.SectorPurple, empowerment);
            solidVisual.widthMultiplier = bandWidth; solidVisual.enabled = true;
            damageCollider.enabled = IsVisible; return;
        }
        if (UsesPurpleArt)
        {
            int sustain = Mathf.FloorToInt(Mathf.Max(0, elapsed) / .06f) % 4;
            Show(purpleActiveFrames, sustain);
            // Full 64x32 tiles at 32 PPU. Neither the canvas nor individual pixels
            // are stretched vertically. Collision follows the authored body band.
            visual.transform.localScale = Vector3.one;
            visual.color = Color.white;
            damageCollider.size = new Vector2(damageCollider.size.x, PurpleBandPixels[sustain] / 32f);
            if (purpleEmitter != null)
            {
                int emitterFrame = elapsed < .04f ? 0 : elapsed < .10f ? 1 : elapsed < .17f ? 2 : 3;
                purpleEmitter.sprite = purpleEmitterFrames != null && purpleEmitterFrames.Length == 4 ? purpleEmitterFrames[emitterFrame] : null;
                purpleEmitter.enabled = elapsed < .21f && purpleEmitter.sprite != null;
            }
            damageCollider.enabled = IsVisible;
            return;
        }
        if (purpleEmitter != null) purpleEmitter.enabled = false;
        // Fire frames hand off to the four sustain frames on the same clock.
        int frame = elapsed < .14f ? (elapsed < .04f ? 0 : elapsed < .09f ? 1 : 2)
            : 3 + Mathf.FloorToInt((elapsed - .14f) / .06f) % 4;
        Show(activeFrames, frame);
        visual.transform.localScale = new Vector3(1, bandWidth * 32f / ActiveBandPixels[frame], 1);
        damageCollider.enabled = IsVisible;
    }

    private void Show(Sprite[] frames, int frame)
    {
        visual.sprite = frames != null && frames.Length > 0 ? frames[Mathf.Clamp(frame, 0, frames.Length - 1)] : null;
        visual.enabled = visual.sprite != null;
    }

    public void Clear()
    {
        if (damageCollider != null) damageCollider.enabled = false;
        if (visual != null) { visual.enabled = false; visual.color = Color.white; }
        if (purpleEmitter != null) { purpleEmitter.enabled = false; purpleEmitter.sprite = null; }
        if (solidVisual != null) solidVisual.enabled = false;
        lastPlayer = null; nextDamageTime = 0; empowerment = 0;
    }
    public void Disconnect(float progress)
    {
        damageCollider.enabled = false;
        if (purpleEmitter != null) purpleEmitter.enabled = false;
        if (solidVisual != null) solidVisual.enabled = false;
        if (visual != null)
        {
            visual.color = new Color(1, 1, 1, .65f * (1 - Mathf.Clamp01(progress)));
            visual.enabled = progress < 1 && visual.sprite != null;
        }
    }
    private void OnDisable() => Clear();
    private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);
    private void TryDamage(Collider2D other)
    {
        if (!IsDamaging) return;
        if (lastPlayer == null || !other.transform.IsChildOf(lastPlayer.transform))
            lastPlayer = other.GetComponentInParent<PlayerHealth>();
        if (lastPlayer == null || lastPlayer.IsDead || Time.time < nextDamageTime) return;
        nextDamageTime = Time.time + interval;
        lastPlayer.TakeDamage(damage);
    }
}
