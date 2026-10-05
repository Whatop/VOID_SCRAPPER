using UnityEngine;

// Encounter ammunition, deliberately not EnemyHealth, HarvestObjectHealth or RewardPickup.
// The carrier owns movement and lifetime; there is no per-object Update or reward path.
[DisallowMultipleComponent]
public sealed class CarrierCombatSalvage : MonoBehaviour, IDamageable, IProjectileDamageReceiver
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField, Min(1)] private float maxHp = 2;
    private float hp;
    public RaiderSalvageCarrierBossController Owner { get; private set; }
    public bool IsDead => hp <= 0;
    public void Initialize(RaiderSalvageCarrierBossController owner)
    { Owner = owner; hp = maxHp; body ??= GetComponent<Rigidbody2D>(); }
    public bool TryReceiveProjectileDamage(in ProjectileDamageContext context)
    {
        if (context.Owner != ProjectileOwner.Player || IsDead || Owner == null) return false;
        TakeDamage(context.Damage); return true;
    }
    public void TakeDamage(float damage)
    {
        if (IsDead || Owner == null || damage <= 0) return;
        hp = Mathf.Max(0, hp - damage);
        if (IsDead) Owner.RetireSalvage(this, false);
    }
    public void Advance(Vector2 destination, float distance)
    {
        if (body != null) body.MovePosition(Vector2.MoveTowards(body.position, destination, distance));
    }
    private void OnDisable() { Owner = null; hp = 0; }
}
