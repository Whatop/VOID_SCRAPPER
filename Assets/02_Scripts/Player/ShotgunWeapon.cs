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

    [Header("Player Spread Feel")]
    [SerializeField] private bool useCenterWeightedSpread = true;
    [Range(1f, 3f)]
    [SerializeField] private float centerWeightExponent = 1.55f;
    [Min(0f)]
    [SerializeField] private float randomAngleJitter = 1.25f;
    [SerializeField] private Vector2 projectileSpeedMultiplierRange = new Vector2(0.96f, 1.04f);

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

        bool firedAny = false;

        for (int i = 0; i < projectileCount; i++)
        {
            float currentAngle = ResolvePelletAngle(i, projectileCount, spreadAngle);
            Vector2 shotDirection = RotateVector(baseDirection, currentAngle);
            float speedMultiplier = Random.Range(
                Mathf.Min(projectileSpeedMultiplierRange.x, projectileSpeedMultiplierRange.y),
                Mathf.Max(projectileSpeedMultiplierRange.x, projectileSpeedMultiplierRange.y)
            );

            bool fired = SpawnProjectile(
                shotDirection,
                baseDamage,
                baseSpeed * speedMultiplier,
                baseRange,
                basePierce
            );

            firedAny |= fired;
        }

        if (!firedAny)
        {
            return;
        }

        SpawnMuzzleEffect(baseDirection);
        AudioManager.PlayAt(SoundEventIds.ShotgunFire, transform.position);

        RegisterAttack();
        NotifyFired();
        nextFireTime = Time.time + GetFireInterval(fallbackFireInterval);
    }


    private float ResolvePelletAngle(int index, int pelletCount, float spreadAngle)
    {
        if (pelletCount <= 1)
        {
            return Random.Range(-randomAngleJitter, randomAngleJitter);
        }

        float normalized = (index + 0.5f) / pelletCount;
        float centered = normalized * 2f - 1f;
        float distribution = useCenterWeightedSpread
            ? Mathf.Sign(centered) * Mathf.Pow(Mathf.Abs(centered), Mathf.Max(1f, centerWeightExponent))
            : centered;

        float baseAngle = distribution * spreadAngle * 0.5f;
        return baseAngle + Random.Range(-randomAngleJitter, randomAngleJitter);
    }
}