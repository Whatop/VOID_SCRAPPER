using System;
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
    [SerializeField] private int fallbackPierceCount;

    [Header("Terminal Guidance Evolution")]
    [Min(0f)]
    [SerializeField] private float terminalGuidanceTurnRateBonus = 150f;
    [Min(0f)]
    [SerializeField] private float terminalGuidanceAcquisitionRangeBonus = 1.5f;
    [Min(1f)]
    [SerializeField] private float terminalGuidanceRetentionRangeMultiplier = 1.5f;
    [Min(0f)]
    [SerializeField] private float terminalGuidanceCloseSteeringDistance = 0.75f;

    [Header("Machine Gun Fire Points")]
    [SerializeField] private Transform leftFirePoint;
    [SerializeField] private Transform rightFirePoint;
    [SerializeField] private bool startFromLeft = true;

    [Header("Optional Heat")]
    [SerializeField] private bool useHeatSystem = true;
    [Min(1f)]
    [SerializeField] private float maxHeat = 100f;
    [Min(0f)]
    [SerializeField] private float heatPerShot = 4f;
    [Min(0f)]
    [SerializeField] private float coolingStartDelay = 0.25f;
    [Min(0f)]
    [SerializeField] private float coolingPerSecond = 55f;
    [Range(0f, 1f)]
    [SerializeField] private float overheatRecoveryRatio = 0.35f;
    [SerializeField] private bool resetHeatOnEquip = true;

    private float fireTimer;
    private float nextFireSoundTime;
    private bool nextShotLeft;
    private MachineGunShotSide lastShotSide = MachineGunShotSide.Center;
    private float currentHeat;
    private float lastShotTime = -999f;
    private bool overheated;

    public MachineGunShotSide LastShotSide => lastShotSide;
    public bool UsesHeatSystem => useHeatSystem;
    public float CurrentHeat => currentHeat;
    public float MaxHeat => Mathf.Max(1f, maxHeat);
    public float HeatRatio => useHeatSystem ? Mathf.Clamp01(currentHeat / MaxHeat) : 0f;
    public bool IsOverheated => useHeatSystem && overheated;
    public float OverheatRecoveryHeat => MaxHeat * Mathf.Clamp01(overheatRecoveryRatio);

    public event Action<float, float, bool> HeatChanged;

    public override void OnEquip()
    {
        fireTimer = 0f;
        nextFireSoundTime = 0f;
        ResetFirePointSide();

        if (resetHeatOnEquip)
        {
            ResetHeat();
        }
        else
        {
            NotifyHeatChanged();
        }
    }

    public override void OnUnequip()
    {
        fireTimer = 0f;
        nextFireSoundTime = 0f;
        ResetFirePointSide();
        NotifyHeatChanged();
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        UpdateHeat(deltaTime);

        if (!input.Held || IsOverheated)
        {
            fireTimer = 0f;
            return;
        }

        fireTimer -= deltaTime;

        if (fireTimer > 0f)
        {
            return;
        }

        if (TryFire())
        {
            AddHeat(heatPerShot);
        }

        fireTimer = GetFireInterval(fallbackFireInterval);
    }

    public void ResetHeat()
    {
        currentHeat = 0f;
        overheated = false;
        lastShotTime = -999f;
        NotifyHeatChanged();
    }

    private bool TryFire()
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
            float randomAngle = UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
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
            return false;
        }

        SpawnMuzzleEffectFrom(selectedFirePoint, baseDirection);

        if (Time.time >= nextFireSoundTime)
        {
            Vector3 soundPosition = selectedFirePoint != null ? selectedFirePoint.position : transform.position;
            AudioManager.PlayAt(SoundEventIds.MachineGunFire, soundPosition, 0.45f);
            nextFireSoundTime = Time.time + 0.08f;
        }

        PlaySuccessfulFireFeedback(
            WeaponTreeType.MachineGun,
            baseDirection,
            selectedFirePoint
        );

        lastShotSide = shotSide;
        AdvanceFirePointSide();
        lastShotTime = Time.time;

        RegisterAttack();
        NotifyFired();
        return true;
    }

    protected override void ConfigureSpawnedProjectile(Bullet bullet)
    {
        if (bullet == null ||
            weaponModifiers == null ||
            !weaponModifiers.MachineGunTerminalGuidanceEnabled)
        {
            return;
        }

        bullet.ConfigureTerminalGuidance(
            terminalGuidanceTurnRateBonus,
            terminalGuidanceAcquisitionRangeBonus,
            terminalGuidanceRetentionRangeMultiplier,
            terminalGuidanceCloseSteeringDistance
        );
    }

    private void UpdateHeat(float deltaTime)
    {
        if (!useHeatSystem || currentHeat <= 0f)
        {
            return;
        }

        if (Time.time - lastShotTime < Mathf.Max(0f, coolingStartDelay))
        {
            return;
        }

        float previous = currentHeat;
        currentHeat = Mathf.Max(0f, currentHeat - Mathf.Max(0f, coolingPerSecond) * Mathf.Max(0f, deltaTime));

        if (overheated && currentHeat <= MaxHeat * Mathf.Clamp01(overheatRecoveryRatio))
        {
            overheated = false;
        }

        if (!Mathf.Approximately(previous, currentHeat))
        {
            NotifyHeatChanged();
        }
    }

    private void AddHeat(float amount)
    {
        if (!useHeatSystem || amount <= 0f)
        {
            return;
        }

        currentHeat = Mathf.Clamp(currentHeat + amount, 0f, MaxHeat);

        if (currentHeat >= MaxHeat - 0.001f)
        {
            overheated = true;
            currentHeat = MaxHeat;
        }

        NotifyHeatChanged();
    }

    private void NotifyHeatChanged()
    {
        HeatChanged?.Invoke(currentHeat, MaxHeat, IsOverheated);
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
