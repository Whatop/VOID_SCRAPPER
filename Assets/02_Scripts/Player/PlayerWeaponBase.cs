using System;
using UnityEngine;

public readonly struct WeaponFireInput
{
    public readonly bool Held;
    public readonly bool PressedThisFrame;
    public readonly bool ReleasedThisFrame;

    public WeaponFireInput(bool held, bool pressedThisFrame, bool releasedThisFrame)
    {
        Held = held;
        PressedThisFrame = pressedThisFrame;
        ReleasedThisFrame = releasedThisFrame;
    }
}

public readonly struct PlayerProjectileFireSnapshot
{
    public PlayerProjectileFireSnapshot(
        GameObject projectilePrefab,
        ProjectileDefinition projectileDefinition,
        Vector2 origin,
        Vector2 direction,
        float damage,
        float speed,
        float range,
        int pierceCount,
        float pierceDamageRetention)
    {
        ProjectilePrefab = projectilePrefab;
        ProjectileDefinition = projectileDefinition;
        Origin = origin;
        Direction = direction;
        Damage = damage;
        Speed = speed;
        Range = range;
        PierceCount = pierceCount;
        PierceDamageRetention = pierceDamageRetention;
    }

    public GameObject ProjectilePrefab { get; }
    public ProjectileDefinition ProjectileDefinition { get; }
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public float Damage { get; }
    public float Speed { get; }
    public float Range { get; }
    public int PierceCount { get; }
    public float PierceDamageRetention { get; }
}

public abstract class PlayerWeaponBase : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] protected WeaponDefinition weaponDefinition;
    [SerializeField] protected ProjectileDefinition projectileDefinitionOverride;

    [Header("Fallback Projectile Prefab")]
    [SerializeField] protected GameObject fallbackProjectilePrefab;

    [Header("Muzzle VFX")]
    [SerializeField] protected GameObject muzzleEffectPrefab;
    [SerializeField] protected float muzzleEffectLifeTime = 0.18f;
    [SerializeField] protected bool rotateMuzzleEffectToShotDirection = true;
    [SerializeField] protected float muzzleEffectRotationOffset = -90f;

    protected PlayerWeaponController weaponController;
    protected PlayerController2D playerController;
    protected PlayerCombatState combatState;
    protected PlayerWeaponModifiers weaponModifiers;
    protected PlayerRuntimeBonusState runtimeBonusState;
    protected PlayerVisualStateController visualStateController;
    protected Transform firePoint;

    public WeaponDefinition WeaponDefinition => weaponDefinition;

    public virtual bool IsCharging => false;
    public virtual float ChargeRatio => 0f;

    public event Action<PlayerWeaponBase, float> Fired;
    public event Action<PlayerWeaponBase> ChargeStarted;
    public event Action<PlayerWeaponBase, float> ChargeChanged;
    public event Action<PlayerWeaponBase, float> ChargeReleased;
    public event Action<PlayerWeaponBase> ChargeCanceled;

    public virtual void SetRuntimeReferences(
        PlayerWeaponController owner,
        Transform firePointReference,
        PlayerController2D controller,
        PlayerCombatState state,
        PlayerWeaponModifiers modifiers)
    {
        weaponController = owner;
        firePoint = firePointReference;
        playerController = controller;
        combatState = state;
        weaponModifiers = modifiers;
        runtimeBonusState = owner != null ? owner.GetComponent<PlayerRuntimeBonusState>() : null;
        visualStateController = owner != null ? owner.GetComponent<PlayerVisualStateController>() : null;
    }

    public virtual void OnEquip()
    {
    }

    public virtual void OnUnequip()
    {
    }

    public virtual void ForceCancel()
    {
    }

    public abstract void TickWeapon(WeaponFireInput input, float deltaTime);

    protected ProjectileDefinition GetProjectileDefinition()
    {
        if (projectileDefinitionOverride != null)
        {
            return projectileDefinitionOverride;
        }

        if (weaponDefinition != null)
        {
            return weaponDefinition.ProjectileDefinition;
        }

        return null;
    }

    protected GameObject GetProjectilePrefab()
    {
        return GetProjectilePrefab(GetProjectileDefinition());
    }

    protected GameObject GetProjectilePrefab(ProjectileDefinition projectileDefinition)
    {
        if (projectileDefinition != null && projectileDefinition.ProjectilePrefab != null)
        {
            return projectileDefinition.ProjectilePrefab;
        }

        return fallbackProjectilePrefab;
    }

    protected float GetProjectileDamage(float fallback)
    {
        ProjectileDefinition projectileDefinition = GetProjectileDefinition();
        return projectileDefinition != null ? projectileDefinition.Damage : fallback;
    }

    protected float GetProjectileSpeed(float fallback)
    {
        ProjectileDefinition projectileDefinition = GetProjectileDefinition();
        return projectileDefinition != null ? projectileDefinition.Speed : fallback;
    }

    protected float GetProjectileRange(float fallback)
    {
        ProjectileDefinition projectileDefinition = GetProjectileDefinition();
        return projectileDefinition != null ? projectileDefinition.Range : fallback;
    }

    protected int GetProjectilePierceCount(int fallback)
    {
        ProjectileDefinition projectileDefinition = GetProjectileDefinition();
        return projectileDefinition != null ? projectileDefinition.PierceCount : fallback;
    }

    protected float GetFireInterval(float fallback)
    {
        float interval = weaponDefinition != null ? weaponDefinition.FireInterval : fallback;

        if (weaponModifiers != null)
        {
            interval *= weaponModifiers.FireIntervalMultiplier;
        }

        return Mathf.Max(0.01f, interval);
    }

    protected int GetProjectileCount(int fallback)
    {
        int count = weaponDefinition != null ? weaponDefinition.ProjectileCount : fallback;

        if (weaponModifiers != null)
        {
            count += weaponModifiers.ProjectileCountBonus;
        }

        return Mathf.Max(1, count);
    }

    protected float GetSpreadAngle(float fallback)
    {
        float spread = weaponDefinition != null ? weaponDefinition.SpreadAngle : fallback;

        if (weaponModifiers != null)
        {
            spread *= weaponModifiers.SpreadMultiplier;
        }

        return Mathf.Max(0f, spread);
    }

    protected Vector2 GetAimDirection()
    {
        if (playerController != null && playerController.AimDirection.sqrMagnitude > 0.001f)
        {
            return playerController.AimDirection.normalized;
        }

        if (firePoint != null)
        {
            return firePoint.up;
        }

        return transform.up;
    }

    protected bool SpawnProjectile(
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount)
    {
        return SpawnProjectile(
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out _
        );
    }

    protected bool SpawnProjectile(
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount,
        out PlayerProjectileFireSnapshot snapshot)
    {
        return SpawnProjectileWithDefinition(
            GetProjectileDefinition(),
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out snapshot
        );
    }

    protected bool SpawnProjectileWithDefinition(
        ProjectileDefinition projectileDefinition,
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount)
    {
        return SpawnProjectileWithDefinition(
            projectileDefinition,
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out _
        );
    }

    protected bool SpawnProjectileWithDefinition(
        ProjectileDefinition projectileDefinition,
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount,
        out PlayerProjectileFireSnapshot snapshot)
    {
        Transform spawnPoint = firePoint != null ? firePoint : transform;

        return SpawnProjectileFromWithDefinition(
            spawnPoint,
            projectileDefinition,
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out snapshot
        );
    }

    protected bool SpawnProjectileFrom(
        Transform spawnPoint,
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount)
    {
        return SpawnProjectileFromWithDefinition(
            spawnPoint,
            GetProjectileDefinition(),
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out _
        );
    }

    protected bool SpawnProjectileFromWithDefinition(
        Transform spawnPoint,
        ProjectileDefinition projectileDefinition,
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount)
    {
        return SpawnProjectileFromWithDefinition(
            spawnPoint,
            projectileDefinition,
            direction,
            baseDamage,
            baseSpeed,
            baseRange,
            basePierceCount,
            out _
        );
    }

    protected bool SpawnProjectileFromWithDefinition(
        Transform spawnPoint,
        ProjectileDefinition projectileDefinition,
        Vector2 direction,
        float baseDamage,
        float baseSpeed,
        float baseRange,
        int basePierceCount,
        out PlayerProjectileFireSnapshot snapshot)
    {
        snapshot = default;
        GameObject projectilePrefab = GetProjectilePrefab(projectileDefinition);

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"{name}: Projectile Prefab이 연결되지 않았습니다.", this);
            return false;
        }

        if (spawnPoint == null)
        {
            spawnPoint = firePoint != null ? firePoint : transform;
        }

        GameObject projectileObject;

        if (PoolManager.Instance != null)
        {
            projectileObject = PoolManager.Instance.Get(projectilePrefab, spawnPoint.position, Quaternion.identity);
        }
        else
        {
            projectileObject = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
        }

        if (projectileObject == null)
        {
            return false;
        }

        Bullet bullet = projectileObject.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogWarning($"{projectileObject.name} 루트에 Bullet 컴포넌트가 없습니다.", projectileObject);

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(projectileObject);
            }
            else
            {
                Destroy(projectileObject);
            }

            return false;
        }

        float finalDamage = baseDamage;
        float finalSpeed = baseSpeed;
        float finalRange = baseRange;
        int finalPierceCount = basePierceCount;
        float homingAngleBonus = 0f;
        float homingRangeBonus = 0f;
        float pierceDamageRetention = projectileDefinition != null
            ? projectileDefinition.PierceDamageRetention
            : 1f;
        float harvestDamageMultiplier = runtimeBonusState != null ? runtimeBonusState.HarvestObjectDamageMultiplier : 1f;

        if (weaponModifiers != null)
        {
            finalDamage *= weaponModifiers.DamageMultiplier;
            finalSpeed *= weaponModifiers.ProjectileSpeedMultiplier;
            finalRange *= weaponModifiers.RangeMultiplier;
            finalPierceCount += weaponModifiers.PierceBonus;
            homingAngleBonus += weaponModifiers.HomingAngleBonus;
            homingRangeBonus += weaponModifiers.HomingRangeBonus;

            if (weaponModifiers.RemovePierceDamageFalloff)
            {
                pierceDamageRetention = 1f;
            }
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Player,
            projectileDefinition,
            finalDamage,
            finalSpeed,
            finalRange,
            Mathf.Max(0, finalPierceCount),
            homingAngleBonus,
            homingRangeBonus,
            harvestDamageMultiplier,
            false,
            gameObject,
            pierceDamageRetention
        );

        ConfigureSpawnedProjectile(bullet);

        Vector2 finalDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.up;
        snapshot = new PlayerProjectileFireSnapshot(
            projectilePrefab,
            projectileDefinition,
            spawnPoint.position,
            finalDirection,
            finalDamage,
            finalSpeed,
            finalRange,
            Mathf.Max(0, finalPierceCount),
            pierceDamageRetention
        );

        return true;
    }

    protected virtual void ConfigureSpawnedProjectile(Bullet bullet)
    {
    }

    protected void SpawnMuzzleEffect(Vector2 shotDirection)
    {
        Transform spawnPoint = firePoint != null ? firePoint : transform;
        SpawnMuzzleEffectFrom(spawnPoint, shotDirection);
    }

    protected void SpawnMuzzleEffectFrom(Transform spawnPoint, Vector2 shotDirection)
    {
        if (muzzleEffectPrefab == null)
        {
            return;
        }

        if (spawnPoint == null)
        {
            spawnPoint = firePoint != null ? firePoint : transform;
        }

        Quaternion rotation = GetMuzzleEffectRotation(spawnPoint, shotDirection);

        GameObject instance;

        if (PoolManager.Instance != null)
        {
            instance = PoolManager.Instance.Get(muzzleEffectPrefab, spawnPoint.position, rotation);
        }
        else
        {
            instance = Instantiate(muzzleEffectPrefab, spawnPoint.position, rotation);
        }

        if (instance == null)
        {
            return;
        }

        float releaseDelay = Mathf.Max(0.01f, muzzleEffectLifeTime);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(instance, releaseDelay);
        }
        else
        {
            Destroy(instance, releaseDelay);
        }
    }

    private Quaternion GetMuzzleEffectRotation(Transform spawnPoint, Vector2 shotDirection)
    {
        if (!rotateMuzzleEffectToShotDirection)
        {
            return spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        }

        Vector2 direction = shotDirection.sqrMagnitude > 0.001f
            ? shotDirection.normalized
            : GetAimDirection();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle + muzzleEffectRotationOffset);
    }

    protected void RegisterAttack()
    {
        if (combatState != null)
        {
            combatState.RegisterAttack();
        }
    }

    protected void NotifyFired(float powerRatio = 1f)
    {
        Fired?.Invoke(this, Mathf.Clamp01(powerRatio));
    }

    protected void PlaySuccessfulFireFeedback(
        WeaponTreeType weaponTreeType,
        Vector2 shotDirection,
        Transform muzzlePoint = null,
        float powerRatio = 1f,
        float aimChokeStrength = 0f,
        bool amplifyPresentation = false)
    {
        if (visualStateController == null && weaponController != null)
        {
            visualStateController = weaponController.GetComponent<PlayerVisualStateController>();
        }

        if (visualStateController == null)
        {
            return;
        }

        Transform source = muzzlePoint != null
            ? muzzlePoint
            : firePoint != null
                ? firePoint
                : transform;

        visualStateController.PlayWeaponFireFeedback(
            weaponTreeType,
            source.position,
            shotDirection,
            powerRatio,
            aimChokeStrength,
            amplifyPresentation
        );
    }

    protected void NotifyChargeStarted()
    {
        ChargeStarted?.Invoke(this);
    }

    protected void NotifyChargeChanged(float chargeRatio)
    {
        ChargeChanged?.Invoke(this, Mathf.Clamp01(chargeRatio));
    }

    protected void NotifyChargeReleased(float chargeRatio)
    {
        ChargeReleased?.Invoke(this, Mathf.Clamp01(chargeRatio));
    }

    protected void NotifyChargeCanceled()
    {
        ChargeCanceled?.Invoke(this);
    }

    protected Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radian);
        float sin = Mathf.Sin(radian);

        return new Vector2(
            (cos * vector.x) - (sin * vector.y),
            (sin * vector.x) + (cos * vector.y)
        );
    }
}
