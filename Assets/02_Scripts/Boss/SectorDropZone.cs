using UnityEngine;

// Passive pooled zone: the existing boss sequence supplies all timing.
[DisallowMultipleComponent]
public sealed class SectorDropZone : MonoBehaviour
{
    [SerializeField] private LineRenderer ring, projection;
    [SerializeField] private SpriteRenderer marker;
    [SerializeField] private CircleCollider2D damageCollider;
    private Vector2 origin;
    private float damage;
    private bool hit;
    private PlayerHealth player;
    private Collider2D playerCollider;
    public bool IsDamaging => damageCollider != null && damageCollider.enabled;
    public bool IsVisible => ring != null && ring.enabled;
    public bool IsWarning { get; private set; }
    public float Radius => damageCollider.radius;
    public void Configure(Vector2 core, Vector2 position, float radius, float amount, PlayerHealth target)
    {
        Clear(); origin = core; damage = amount; player = target;
        playerCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        transform.SetPositionAndRotation(position, Quaternion.identity); transform.localScale = Vector3.one;
        damageCollider.radius = radius; ring.transform.localScale = Vector3.one * radius;
        Warn(0);
    }
    public void Warn(float progress)
    {
        IsWarning = true; damageCollider.enabled = false;
        Color red = new Color(1, .12f, .1f, Mathf.Lerp(.55f, .95f, progress));
        ring.startColor = ring.endColor = red; ring.widthMultiplier = .045f; ring.enabled = true;
        marker.color = red; marker.enabled = true;
        projection.enabled = progress < .22f;
        if (projection.enabled)
        {
            projection.SetPosition(0, origin);
            projection.SetPosition(1, Vector2.Lerp(origin, transform.position, Mathf.Clamp01(progress / .16f)));
        }
    }
    public void Activate()
    {
        IsWarning = false; projection.enabled = false;
        Color red = new Color(1, .2f, .12f, 1);
        ring.startColor = ring.endColor = red; ring.widthMultiplier = .11f; ring.enabled = true;
        marker.color = red; marker.enabled = true;
        damageCollider.enabled = IsVisible;
        TryDamage();
    }
    public void Fade(float progress)
    {
        damageCollider.enabled = false; IsWarning = false; projection.enabled = false;
        Color red = new Color(1, .12f, .1f, 1 - Mathf.Clamp01(progress));
        ring.startColor = ring.endColor = red; marker.color = red;
    }
    private void OnTriggerEnter2D(Collider2D other) { if (IsDamaging) TryDamage(); }
    private void TryDamage()
    {
        if (hit || !IsDamaging || player == null || player.IsDead) return;
        Vector2 center = transform.position;
        Vector2 nearest = playerCollider != null ? playerCollider.ClosestPoint(center) : (Vector2)player.transform.position;
        if ((nearest - center).sqrMagnitude > Radius * Radius) return;
        hit = true; player.TakeDamage(damage, nearest, (Vector2)player.transform.position - center);
    }
    public void Clear()
    {
        if (damageCollider != null) damageCollider.enabled = false;
        if (ring != null) ring.enabled = false;
        if (projection != null) projection.enabled = false;
        if (marker != null) marker.enabled = false;
        player = null; playerCollider = null; hit = false; IsWarning = false;
    }
    private void OnDisable() => Clear();
}
