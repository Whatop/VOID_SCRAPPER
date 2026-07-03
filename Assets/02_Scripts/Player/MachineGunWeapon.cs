using UnityEngine;

public enum MachineGunShotSide
{
    Center,
    Left,
    Right
}

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

    [Header("Machine Gun Fire Points")]
    [SerializeField] private Transform leftFirePoint;
    [SerializeField] private Transform rightFirePoint;
    [SerializeField] private bool startFromLeft = true;

    private float fireTimer;
    private bool nextShotLeft;
    private MachineGunShotSide lastShotSide = MachineGunShotSide.Center;

    public MachineGunShotSide LastShotSide => lastShotSide;

    public override void OnEquip()
    {
        fireTimer = 0f;
        ResetFirePointSide();
    }

    public override void OnUnequip()
    {
        fireTimer = 0f;
        ResetFirePointSide();
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

        Transform selectedFirePoint = GetCurrentFirePoint(out MachineGunShotSide shotSide);

        bool firedAny = false;

        for (int i = 0; i < projectileCount; i++)
        {
            float randomAngle = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            Vector2 shotDirection = RotateVector(baseDirection, randomAngle);

            bool fired = SpawnProjectileFrom(
                selectedFirePoint,
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

        SpawnMuzzleEffectFrom(selectedFirePoint, baseDirection);

        lastShotSide = shotSide;
        AdvanceFirePointSide();

        RegisterAttack();
        NotifyFired();
    }

    private Transform GetCurrentFirePoint(out MachineGunShotSide shotSide)
    {
        bool hasLeft = leftFirePoint != null;
        bool hasRight = rightFirePoint != null;

        if (hasLeft && hasRight)
        {
            if (nextShotLeft)
            {
                shotSide = MachineGunShotSide.Left;
                return leftFirePoint;
            }

            shotSide = MachineGunShotSide.Right;
            return rightFirePoint;
        }

        if (hasLeft)
        {
            shotSide = MachineGunShotSide.Left;
            return leftFirePoint;
        }

        if (hasRight)
        {
            shotSide = MachineGunShotSide.Right;
            return rightFirePoint;
        }

        shotSide = MachineGunShotSide.Center;
        return firePoint != null ? firePoint : transform;
    }

    private void AdvanceFirePointSide()
    {
        if (leftFirePoint == null || rightFirePoint == null)
        {
            return;
        }

        nextShotLeft = !nextShotLeft;
    }

    private void ResetFirePointSide()
    {
        nextShotLeft = startFromLeft;
        lastShotSide = MachineGunShotSide.Center;
    }
}