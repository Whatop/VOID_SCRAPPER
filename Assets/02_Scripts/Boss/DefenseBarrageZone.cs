using UnityEngine;

// A pooled view/damage gate. The encounter's single scheduler supplies all time.
[DisallowMultipleComponent]
public sealed class DefenseBarrageZone : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private CircleCollider2D impactCollider;
    [SerializeField] private Sprite[] warningFrames;
    [SerializeField] private Sprite[] impactFrames;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private float damage;
    private bool hit;
    public const float ImpactDuration = .48f;
    public const float DamageStart = .065f;
    public const float DamageEnd = .15f;
    public bool IsDamaging => impactCollider != null && impactCollider.enabled;
    public bool IsVisible => visual != null && visual.enabled;
    public bool IsWarning { get; private set; }
    public Vector2 CommittedPosition { get; private set; }
    public float Radius => impactCollider != null ? impactCollider.radius : 0;

    public void BeginWarning(Vector2 position, float radius, float strikeDamage, PlayerHealth target)
    {
        Clear();
        CommittedPosition = position; transform.position = position;
        transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        player = target; playerCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        damage = strikeDamage; hit = false;
        impactCollider.radius = radius;
        // Peak approved impact occupies a 60px diameter; marker encloses that same radius.
        visual.transform.localScale = Vector3.one * (radius * 2f * 32f / 60f);
        visual.color = Color.white; visual.enabled = true; IsWarning = true;
        SetWarningProgress(0);
    }
    public void SetWarningProgress(float progress)
    {
        impactCollider.enabled = false;
        if (warningFrames == null || warningFrames.Length == 0) return;
        float time = Mathf.Clamp01(progress) * .72f;
        int frame = time < .15f ? 0 : time < .29f ? 1 : time < .42f ? 2 : time < .54f ? 3 : time < .64f ? 4 : 5;
        visual.sprite = warningFrames[Mathf.Min(frame, warningFrames.Length - 1)];
    }
    public void SetImpactElapsed(float elapsed)
    {
        IsWarning = false;
        if (elapsed >= ImpactDuration) { Clear(); return; }
        int frame = elapsed < .065f ? 0 : elapsed < .15f ? 1 : elapsed < .24f ? 2 : elapsed < .325f ? 3 : elapsed < .405f ? 4 : 5;
        visual.sprite = impactFrames[Mathf.Min(frame, impactFrames.Length - 1)];
        visual.enabled = true;
        // Only the bright expanded peak is damaging; debris is a harmless visible tail.
        impactCollider.enabled = elapsed >= DamageStart && elapsed < DamageEnd;
        if (impactCollider.enabled) TryHitPlayer();
    }
    private void OnTriggerEnter2D(Collider2D other) { if (IsDamaging) TryHitPlayer(); }
    private void TryHitPlayer()
    {
        if (hit || player == null || player.IsDead || damage <= 0) return;
        Vector2 center = CommittedPosition;
        Vector2 nearest = playerCollider != null ? playerCollider.ClosestPoint(center) : (Vector2)player.transform.position;
        if ((nearest - center).sqrMagnitude > Radius * Radius) return;
        hit = true;
        player.TakeDamage(damage, nearest, (Vector2)player.transform.position - center);
    }
    public void Clear()
    {
        if (impactCollider != null) impactCollider.enabled = false;
        if (visual != null) visual.enabled = false;
        IsWarning = false; player = null; playerCollider = null; hit = false;
    }
    private void OnDisable() => Clear();
}
