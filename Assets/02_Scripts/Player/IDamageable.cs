using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(float damage);
}

public readonly struct ProjectileDamageContext
{
    public ProjectileDamageContext(
        ProjectileOwner owner,
        Transform sourceRoot,
        float damage,
        Vector2 hitPoint,
        int projectileInstanceId)
    {
        Owner = owner;
        SourceRoot = sourceRoot;
        Damage = damage;
        HitPoint = hitPoint;
        ProjectileInstanceId = projectileInstanceId;
    }

    public ProjectileOwner Owner { get; }
    public Transform SourceRoot { get; }
    public float Damage { get; }
    public Vector2 HitPoint { get; }
    public int ProjectileInstanceId { get; }
}

public interface IProjectileDamageReceiver
{
    bool TryReceiveProjectileDamage(in ProjectileDamageContext context);
}
