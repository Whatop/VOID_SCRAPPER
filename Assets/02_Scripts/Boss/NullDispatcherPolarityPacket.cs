using UnityEngine;

// Presentation and flight only. The encounter owns allegiance, lifetime admission and cleanup.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class NullDispatcherPolarityPacket : MonoBehaviour
{
    public enum PacketKind { Hostile, Support }

    [SerializeField] private PacketKind kind;
    [SerializeField] private SpriteRenderer[] fills;
    [SerializeField] private SpriteRenderer[] outlines;
    [SerializeField] private Color whiteFill = Color.white;
    [SerializeField] private Color whiteOutline = new Color(0.16f, 0.08f, 0.25f);
    [SerializeField] private Color blackFill = new Color(0.08f, 0.04f, 0.12f);
    [SerializeField] private Color blackOutline = new Color(0.85f, 0.7f, 1f);

    private NullDispatcherBossController owner;
    private PlayerHealth target;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private PoolManager pool;
    private Vector2 velocity;
    private float remaining;
    private float amount;
    private bool consumed = true;

    public PacketKind Kind => kind;
    public NullDispatcherBossController.Polarity Polarity { get; private set; }
    public bool Consumed => consumed;

    public bool Initialize(NullDispatcherBossController source, PlayerHealth player,
        NullDispatcherBossController.Polarity polarity, Vector2 flightVelocity, float value, float lifetime)
    {
        if (!consumed || source == null || player == null ||
            polarity != NullDispatcherBossController.PacketPolarity(source.PlayerPolarity, kind) ||
            !source.RegisterPolarityPacket(this)) return false;
        body = body != null ? body : GetComponent<Rigidbody2D>();
        hitbox = hitbox != null ? hitbox : GetComponent<Collider2D>();
        owner = source;
        target = player;
        pool = PoolManager.Instance;
        Polarity = polarity;
        velocity = flightVelocity;
        amount = Mathf.Max(0f, value);
        remaining = Mathf.Max(0.1f, lifetime);
        consumed = false;
        body.linearVelocity = Vector2.zero;
        hitbox.enabled = true;
        bool white = polarity == NullDispatcherBossController.Polarity.White;
        SetColors(fills, white ? whiteFill : blackFill);
        SetColors(outlines, white ? whiteOutline : blackOutline);
        return true;
    }

    private static void SetColors(SpriteRenderer[] renderers, Color color)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers) if (renderer != null) renderer.color = color;
    }

    // One encounter fixed-step routine advances all packets; no per-packet Update or searches.
    public void AdvanceFlight(float deltaTime)
    {
        if (consumed || deltaTime <= 0f || GameplayPauseManager.IsPaused) return;
        if (owner == null || !owner.PolarityPacketsAllowed || target == null || target.IsDead)
        {
            Release();
            return;
        }
        remaining -= deltaTime;
        if (remaining <= 0f) { Release(); return; }
        if (kind == PacketKind.Support)
        {
            Vector2 desired = ((Vector2)target.transform.position - body.position).normalized * velocity.magnitude;
            velocity = Vector2.MoveTowards(velocity, desired, 6f * deltaTime);
        }
        body.MovePosition(body.position + velocity * deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!consumed && target != null && other.GetComponentInParent<PlayerHealth>() == target)
            TryConsume(target);
    }

    public bool TryConsume(PlayerHealth recipient)
    {
        if (consumed || recipient == null || recipient != target || recipient.IsDead ||
            owner == null || !owner.PolarityPacketsAllowed || GameplayPauseManager.IsPaused) return false;
        // Invalidate collision authority before calling health events (which can end the run).
        consumed = true;
        hitbox.enabled = false;
        if (kind == PacketKind.Support) recipient.Heal(amount);
        else recipient.TakeDamage(amount, transform.position, velocity.normalized);
        Release();
        return true;
    }

    public void Release()
    {
        Detach();
        if (gameObject.activeSelf)
        {
            if (pool != null) pool.Release(gameObject);
            else gameObject.SetActive(false); // No runtime fallback spawning or destruction loop.
        }
    }

    private void Detach()
    {
        consumed = true;
        if (hitbox != null) hitbox.enabled = false;
        if (body != null) body.linearVelocity = Vector2.zero;
        var previousOwner = owner;
        owner = null;
        target = null;
        velocity = Vector2.zero;
        previousOwner?.UnregisterPolarityPacket(this);
    }

    // Pool return/scene destruction already owns deactivation; never return to a pool from these callbacks.
    private void OnDisable() => Detach();
    private void OnDestroy() => Detach();
}
