using UnityEngine;

// Visual playback only. The boss scheduler and BossLaserHazard retain all damage authority.
[DisallowMultipleComponent]
public sealed class NullSignatureVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameSeconds;
    [SerializeField] private bool loop;
    [SerializeField] private bool holdLast;
    private float elapsed, speed, total, stopAfter;
    private Transform follow;
    public Object Owner { get; private set; }
    public int FrameIndex { get; private set; }
    public SpriteRenderer Visual => visual;

    public void Play(Object owner, float fitDuration = 0f, Transform anchor = null, float lifetime = 0f)
    {
        Owner = owner; follow = anchor; elapsed = 0f; total = 0f; stopAfter = lifetime;
        foreach (float seconds in frameSeconds) total += seconds;
        speed = fitDuration > 0f ? total / fitDuration : 1f;
        visual.drawMode = SpriteDrawMode.Simple;
        visual.color = Color.white; visual.enabled = true;
        transform.localScale = Vector3.one;
        Sample(0f);
    }

    public void SetLine(Vector2 start, Vector2 end, bool verticalSheet)
    {
        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(verticalSheet ? (start + end) * .5f : start,
            Quaternion.Euler(0f, 0f, angle - (verticalSheet ? 90f : 0f)));
        visual.drawMode = SpriteDrawMode.Tiled;
        visual.size = verticalSheet ? new Vector2(1f, delta.magnitude) : new Vector2(delta.magnitude, 1f);
        visual.tileMode = SpriteTileMode.Continuous;
        // Configure tiling before restoring the pooled transform. In Play Mode
        // the SpriteRenderer mode/size handoff can retain the old sheet footprint.
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        if (follow != null) transform.position = follow.position;
        if (!GameplayPauseManager.IsPaused) AdvancePresentation(Time.deltaTime);
    }

    public void AdvancePresentation(float seconds)
    {
        if (Owner == null || seconds <= 0f || GameplayPauseManager.IsPaused) return;
        elapsed += seconds;
        if (stopAfter > 0f && elapsed >= stopAfter) { Release(); return; }
        float time = elapsed * speed;
        if (!loop && !holdLast && time >= total) { Release(); return; }
        Sample(loop && total > 0f ? time % total : time);
    }

    private void Sample(float time)
    {
        if (frames == null || frames.Length == 0) return;
        int index = 0;
        while (index < frames.Length - 1 && time >= frameSeconds[index]) time -= frameSeconds[index++];
        FrameIndex = index; visual.sprite = frames[index];
    }

    public void Release()
    {
        if (Owner == null) return;
        Owner = null; follow = null;
        if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
        else Destroy(gameObject);
    }
    private void OnDisable() { Owner = null; follow = null; if (visual != null) visual.enabled = false; }
}
