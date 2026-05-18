using UnityEngine;

public class ShotgunWeapon : PlayerWeaponBase
{
    [Header("Shotgun Fallback Values")]
    [SerializeField] private float fallbackDamage = 1.8f;
    [SerializeField] private float fallbackSpeed = 12f;
    [SerializeField] private float fallbackRange = 6.5f;
    [SerializeField] private float fallbackFireInterval = 0.85f;
    [SerializeField] private int fallbackProjectileCount = 6;
    [SerializeField] private float fallbackSpreadAngle = 40f;
    [SerializeField] private int fallbackPierceCount = 0;

    private float nextFireTime;

    public override void OnEquip()
    {
        nextFireTime = 0f;
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        if (!input.PressedThisFrame)
        {
            return;
        }

        TryFire();
    }

    private void TryFire()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        Vector2 baseDirection = GetAimDirection();

        int projectileCount = GetProjectileCount(fallbackProjectileCount);
        float spreadAngle = GetSpreadAngle(fallbackSpreadAngle);

        float baseDamage = GetProjectileDamage(fallbackDamage);
        float baseSpeed = GetProjectileSpeed(fallbackSpeed);
        float baseRange = GetProjectileRange(fallbackRange);
        int basePierce = GetProjectilePierceCount(fallbackPierceCount);

        float startAngle = -spreadAngle * 0.5f;
        float angleStep = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0f;

        bool firedAny = false;

        for (int i = 0; i < projectileCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Vector2 shotDirection = RotateVector(baseDirection, currentAngle);

            bool fired = SpawnProjectile(
                shotDirection,
                baseDamage,
                baseSpeed,
                baseRange,
                basePierce
            );

            firedAny |= fired;
        }

        if (!firedAny)
        {
            return;
        }

        RegisterAttack();
        NotifyFired();
        nextFireTime = Time.time + GetFireInterval(fallbackFireInterval);
    }
}
