using UnityEngine;

public class MachineGunWeapon : PlayerWeaponBase
{
    [Header("Machine Gun Fallback Values")]
    [SerializeField] private float fallbackDamage = 1f;
    [SerializeField] private float fallbackSpeed = 14f;
    [SerializeField] private float fallbackRange = 9f;
    [SerializeField] private float fallbackFireInterval = 0.10f;
    [SerializeField] private int fallbackProjectileCount = 1;
    [SerializeField] private float fallbackSpreadAngle = 6f;
    [SerializeField] private int fallbackPierceCount = 0;

    private float fireTimer;

    public override void OnEquip()
    {
        fireTimer = 0f;
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        if (!input.Held)
        {
            fireTimer = 0f;
            return;
        }

        fireTimer -= deltaTime;

        if (fireTimer > 0f)
        {
            return;
        }

        TryFire();
        fireTimer = GetFireInterval(fallbackFireInterval);
    }

    private void TryFire()
    {
        Vector2 baseDirection = GetAimDirection();

        int projectileCount = GetProjectileCount(fallbackProjectileCount);
        float spreadAngle = GetSpreadAngle(fallbackSpreadAngle);

        float baseDamage = GetProjectileDamage(fallbackDamage);
        float baseSpeed = GetProjectileSpeed(fallbackSpeed);
        float baseRange = GetProjectileRange(fallbackRange);
        int basePierce = GetProjectilePierceCount(fallbackPierceCount);

        bool firedAny = false;

        for (int i = 0; i < projectileCount; i++)
        {
            float randomAngle = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            Vector2 shotDirection = RotateVector(baseDirection, randomAngle);

            bool fired = SpawnProjectile(
                shotDirection,
                baseDamage,
                baseSpeed,
                baseRange,
                basePierce
            );

            firedAny |= fired;
        }

        if (firedAny)
        {
            RegisterAttack();
        }
    }
}