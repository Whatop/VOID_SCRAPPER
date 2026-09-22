using UnityEngine;
using UnityEngine.Serialization;

public partial class ShotgunWeapon : PlayerWeaponBase
{
    [Header("Shotgun Fallback Values")]
    [SerializeField] private float fallbackDamage = 1.8f;
    [SerializeField] private float fallbackSpeed = 12f;
    [SerializeField] private float fallbackRange = 6.5f;
    [SerializeField] private float fallbackFireInterval = 0.85f;
    [SerializeField] private int fallbackProjectileCount = 6;
    [SerializeField] private float fallbackSpreadAngle = 40f;
    [SerializeField] private int fallbackPierceCount = 0;

    [Header("Player Shotgun Pattern")]
    [Tooltip("가운데 펠릿을 조밀하게 두고 가장자리 펠릿만 최대 각도까지 보내 적 산탄과 다른 감각을 만듭니다.")]
    [SerializeField] private bool useCenterWeightedSpread = true;
    [Range(1f, 3f)]
    [SerializeField] private float centerWeightExponent = 1.6f;
    [Min(0f)]
    [FormerlySerializedAs("randomAngleJitter")]
    [SerializeField] private float angleJitter = 1.35f;
    [Range(0f, 0.2f)]
    [SerializeField] private float projectileSpeedVariation = 0.04f;
    [Range(0f, 0.2f)]
    [SerializeField] private float projectileRangeVariation = 0.03f;

    [Header("Aim-Responsive Choke")]
    [Tooltip("Aim error at or below this distance from hostile collider geometry receives the full choke.")]
    [Min(0f)]
    [SerializeField] private float fullChokeAimError = 0.2f;
    [Tooltip("Aim error at or beyond this distance receives no choke.")]
    [Min(0f)]
    [SerializeField] private float chokeFalloffEndAimError = 0.95f;
    [Range(0f, 0.9f)]
    [SerializeField] private float maximumChokeSpreadReduction = 0.4f;
    [Min(0f)]
    [SerializeField] private float minimumChokedSpreadAngle = 12f;

    [Header("Close-Quarters Overpressure")]
    [Min(0f)]
    [SerializeField] private float overpressureFullBonusDistance = 1.25f;
    [Min(0f)]
    [SerializeField] private float overpressureFalloffEndDistance = 4f;

    private const int AimChokeBufferSize = 32;
    private readonly Collider2D[] aimChokeBuffer = new Collider2D[AimChokeBufferSize];
    private float nextFireTime;

    public override void OnEquip()
    {
        equipmentDash = weaponController != null ? weaponController.GetComponent<PlayerDash>() : GetComponentInParent<PlayerDash>();
        ForceCancel();
        nextFireTime = 0f;
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        RefreshBreachOpportunity();
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
        float aimChokeStrength = ResolveAimChokeStrength();
        float spreadAngle = ResolveShotSpread(aimChokeStrength);

        float baseDamage = GetProjectileDamage(fallbackDamage);
        float baseSpeed = GetProjectileSpeed(fallbackSpeed);
        float baseRange = GetProjectileRange(fallbackRange);
        int basePierce = GetProjectilePierceCount(fallbackPierceCount);

        bool firedAny = false;
        int slugLevel = weaponModifiers != null ? weaponModifiers.ShotgunSlugCouplerLevel : 0;
        bool convertSlug = slugLevel > 0 && projectileCount >= 2;
        int center = (projectileCount - 1) / 2;

        for (int i = 0; i < projectileCount; i++)
        {
            if (convertSlug && i == center + 1) continue;
            firingSlug = convertSlug && i == center;
            firingImpactCarrier = i == center;
            float currentAngle = ResolvePelletAngleOffset(i, projectileCount, spreadAngle);
            if (firingSlug) currentAngle = 0f;
            Vector2 shotDirection = RotateVector(baseDirection, currentAngle);
            float speedMultiplier = Random.Range(
                1f - projectileSpeedVariation,
                1f + projectileSpeedVariation
            );
            float rangeMultiplier = Random.Range(
                1f - projectileRangeVariation,
                1f + projectileRangeVariation
            );

            bool fired = SpawnProjectile(
                shotDirection,
                baseDamage * (firingSlug ? 2f : 1f),
                baseSpeed * speedMultiplier * (firingSlug ? SlugSpeedMultiplier : 1f),
                baseRange * rangeMultiplier * (firingSlug && slugLevel >= 3 ? 1.1f : 1f),
                basePierce + (firingSlug ? (slugLevel >= 3 ? 2 : 1) : 0)
            );

            firedAny |= fired;
        }
        firingSlug = firingImpactCarrier = false;

        if (!firedAny)
        {
            return;
        }

        SpawnMuzzleEffect(baseDirection);
        AudioManager.PlayAt(SoundEventIds.ShotgunFire, transform.position);

        bool hasOverpressure = weaponModifiers != null &&
                               weaponModifiers.ShotgunCloseRangeDamagePercent > 0f;
        PlaySuccessfulFireFeedback(
            WeaponTreeType.Shotgun,
            baseDirection,
            firePoint,
            1f,
            aimChokeStrength,
            hasOverpressure
        );

        RegisterAttack();
        NotifyFired();
        float interval = GetFireInterval(fallbackFireInterval);
        int sequenceLevel = weaponModifiers != null ? weaponModifiers.ShotgunBreachSequenceLevel : 0;
        if (sequenceLevel > 0 && Time.time <= breachArmedUntil)
        {
            interval = Mathf.Max(.12f, interval * (sequenceLevel >= 3 ? .6f : .75f));
            breachArmedUntil = float.NegativeInfinity;
        }
        nextFireTime = Time.time + interval;
    }

    private float ResolveShotSpread(float aimChokeStrength)
    {
        float modifiedSpread = GetSpreadAngle(fallbackSpreadAngle);
        float strength = Mathf.Clamp01(aimChokeStrength);

        if (strength <= 0f)
        {
            return modifiedSpread;
        }

        float maximumReduction = Mathf.Clamp01(maximumChokeSpreadReduction);
        float chokeMultiplier = Mathf.Lerp(1f, 1f - maximumReduction, strength);
        return Mathf.Max(Mathf.Max(0f, minimumChokedSpreadAngle), modifiedSpread * chokeMultiplier);
    }

    private float ResolveAimChokeStrength()
    {
        if (playerController == null ||
            !playerController.TryGetAimWorldPosition(out Vector2 aimWorldPosition))
        {
            return 0f;
        }

        float fullDistance = Mathf.Max(0f, fullChokeAimError);
        float falloffEnd = Mathf.Max(fullDistance + 0.001f, chokeFalloffEndAimError);
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(
            aimWorldPosition,
            falloffEnd,
            filter,
            aimChokeBuffer
        );
        float closestAimError = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = aimChokeBuffer[i];
            aimChokeBuffer[i] = null;

            if (!IsValidAimChokeTarget(candidate))
            {
                continue;
            }

            Vector2 closestPoint = candidate.ClosestPoint(aimWorldPosition);
            float aimError = Vector2.Distance(aimWorldPosition, closestPoint);
            closestAimError = Mathf.Min(closestAimError, aimError);
        }

        if (float.IsPositiveInfinity(closestAimError) || closestAimError >= falloffEnd)
        {
            return 0f;
        }

        if (closestAimError <= fullDistance)
        {
            return 1f;
        }

        return 1f - Mathf.InverseLerp(fullDistance, falloffEnd, closestAimError);
    }

    private static bool IsValidAimChokeTarget(Collider2D candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        EnemyHealth enemyHealth = candidate.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || enemyHealth.IsDead)
        {
            return false;
        }

        IPlayerOwnedAlly playerOwnedAlly = enemyHealth.GetComponentInParent<IPlayerOwnedAlly>();
        if (playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly)
        {
            return false;
        }

        BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();
        if (turret != null)
        {
            if (turret.IsPlayerAllied)
            {
                return false;
            }

            if (turret.IsShopDefense && turret.ShopOwner != null && !turret.ShopOwner.IsHostile)
            {
                return false;
            }
        }

        EnemyBaseAI enemyAI = enemyHealth.GetComponentInParent<EnemyBaseAI>();
        return enemyAI == null ||
               !enemyAI.IsShopSecurityUnit ||
               ShopRunBridge.IsShopHostileThisRun();
    }

    private float ResolvePelletAngleOffset(int index, int projectileCount, float totalSpreadAngle)
    {
        if (projectileCount <= 1)
        {
            return Random.Range(-angleJitter, angleJitter);
        }

        float normalized = index / (float)(projectileCount - 1);
        float signed = normalized * 2f - 1f;
        float weighted = signed;

        if (useCenterWeightedSpread)
        {
            weighted = Mathf.Sign(signed) * Mathf.Pow(
                Mathf.Abs(signed),
                Mathf.Max(1f, centerWeightExponent)
            );
        }

        float halfSpread = Mathf.Max(0f, totalSpreadAngle) * 0.5f;
        return weighted * halfSpread + Random.Range(-angleJitter, angleJitter);
    }

    protected override void ConfigureSpawnedProjectile(Bullet bullet)
    {
        if (bullet == null || weaponModifiers == null)
        {
            return;
        }

        if (firingImpactCarrier) bullet.ConfigureFirstEnemyImpact(weaponModifiers.ShotgunImpactDisplacement);
        if (firingSlug) bullet.ConfigureEquipmentWidth(1.5f);

        float maxBonusPercent = weaponModifiers.ShotgunCloseRangeDamagePercent;

        if (maxBonusPercent <= 0f)
        {
            return;
        }

        bullet.ConfigurePlayerEnemyCloseRangeDamage(
            maxBonusPercent,
            overpressureFullBonusDistance,
            overpressureFalloffEndDistance
        );
    }
}
