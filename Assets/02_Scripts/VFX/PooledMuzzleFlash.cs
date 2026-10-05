using UnityEngine;

// Only short firing flashes use this lifetime owner. Charge/beam/impact effects
// remain owned by their combat scheduler. No delayed pool callback can outlive a lease.
[DisallowMultipleComponent]
public sealed class PooledMuzzleFlash : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float lifetime = .09f;
    private SpriteRenderer visual;
    private Animator animator;
    private PhaseCombatVfx frames;
    private Transform anchor;
    private Vector3 offset;
    private Color baseColor;
    private PlayerHealth player;
    private EnemyHealth enemy;
    private float elapsed;
    private bool playing;
    public float Lifetime => lifetime;
    public bool IsPlaying => playing;
    public bool IsPlayingAt(Transform origin) => playing && anchor == origin;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        frames = GetComponent<PhaseCombatVfx>();
        baseColor = visual != null ? visual.color : Color.white;
    }
    public void Play(Transform origin, GameObject owner)
    {
        Unbind();
        anchor = origin; offset = origin != null ? transform.position - origin.position : Vector3.zero;
        player = owner != null ? owner.GetComponentInParent<PlayerHealth>() : null;
        enemy = owner != null ? owner.GetComponentInParent<EnemyHealth>() : null;
        if (player != null) player.Died += Release;
        if (enemy != null) enemy.Died += EnemyDied;
        elapsed = 0; playing = true;
        if (visual != null) { visual.enabled = true; visual.color = baseColor; }
        if (animator != null) { animator.Rebind(); animator.Update(0); }
        if (frames != null) frames.Sample(0);
    }
    private void LateUpdate() => Advance(Time.deltaTime);
    public void Advance(float deltaTime)
    {
        if (!playing) return;
        if (anchor == null || !anchor.gameObject.activeInHierarchy) { Release(); return; }
        elapsed += Mathf.Max(0, deltaTime);
        if (elapsed >= lifetime) { Release(); return; }
        // Follow translation only: the flash keeps the direction of the actual shot.
        transform.position = anchor.position + offset;
        if (frames != null) frames.Sample(elapsed);
        if (visual != null)
        {
            Color color = baseColor;
            color.a *= 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lifetime * .2f, lifetime, elapsed));
            visual.color = color;
        }
    }
    private void EnemyDied(EnemyHealth _) => Release();
    private void Release()
    {
        if (!playing) return;
        playing = false; Unbind();
        if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
        else gameObject.SetActive(false);
    }
    private void Unbind()
    {
        if (player != null) player.Died -= Release;
        if (enemy != null) enemy.Died -= EnemyDied;
        player = null; enemy = null;
    }
    private void OnDisable()
    {
        playing = false; Unbind(); anchor = null;
        if (visual != null) visual.color = baseColor;
    }
}
