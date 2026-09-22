using UnityEngine;

public partial class MachineGunWeapon
{
    private readonly Collider2D[] distributionTargets = new Collider2D[32];
    private readonly RaycastHit2D[] distributionObstacles = new RaycastHit2D[16];
    private Transform recentDistributionTarget;
    private Transform previousDistributionTarget;
    private Transform firingDistributionTarget;
    private Transform lastFiredDistributionTarget;
    private Camera distributionCamera;
    private int sustainedShots;
    private bool pairedFeedPending;
    private float pairedFeedDelay;

    public float EffectiveCoolingRate => Mathf.Max(0f, coolingPerSecond) * (weaponModifiers != null ? weaponModifiers.MachineGunCoolingMultiplier : 1f);
    public float EffectiveCoolingDelay => Mathf.Max(.05f, coolingStartDelay - (weaponModifiers != null ? weaponModifiers.MachineGunCoolingDelayReduction : 0f));
    public int SustainedShotCount => sustainedShots;
    public bool HasPendingPairedFeed => pairedFeedPending;
    public float TwinFeedSpreadMultiplier
    {
        get
        {
            int level = weaponModifiers != null ? weaponModifiers.MachineGunTwinFeedLevel : 0;
            if (level == 0) return 1f;
            if (sustainedShots < 4 || level == 1) return .94f;
            return level == 2 ? .86f : .82f;
        }
    }

    public override void ForceCancel()
    {
        sustainedShots = 0;
        pairedFeedPending = false;
        pairedFeedDelay = 0f;
        recentDistributionTarget = previousDistributionTarget = firingDistributionTarget = lastFiredDistributionTarget = null;
    }

    private void OnDisable()
    {
        ForceCancel();
    }

    private Transform ResolveDistributionTarget(Vector2 aim)
    {
        int level = weaponModifiers != null ? weaponModifiers.MachineGunTargetDistributionLevel : 0;
        if (level <= 0 || aim.sqrMagnitude < .001f) return null;
        // No camera means no safe visible-target contract. Keep manual aim.
        if (distributionCamera == null) return null;
        Vector2 origin = firePoint != null ? firePoint.position : transform.position;
        float range = Mathf.Min(12f, GetProjectileRange(fallbackRange) * weaponModifiers.RangeMultiplier);
        float coneCosine = Mathf.Cos((level == 1 ? 6f : level == 2 ? 9f : 12f) * Mathf.Deg2Rad);
        int count = Physics2D.OverlapCircle(origin, range, new ContactFilter2D { useTriggers = true }, distributionTargets);
        Transform selected = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = distributionTargets[i];
            distributionTargets[i] = null;
            if (!IsDistributionHostile(hit, out EnemyHealth enemy)) continue;
            Transform target = enemy.transform;
            if (target == recentDistributionTarget || (level >= 3 && target == previousDistributionTarget)) continue;
            Vector2 offset = (Vector2)target.position - origin;
            float distance = offset.magnitude;
            if (distance < .01f || distance >= bestDistance || Vector2.Dot(aim.normalized, offset / distance) < coneCosine) continue;
            Vector3 viewport = distributionCamera.WorldToViewportPoint(target.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
            if (!HasDistributionLineOfSight(origin, offset / distance, distance, enemy)) continue;
            selected = target;
            bestDistance = distance;
        }
        return selected;
    }

    private static bool IsDistributionHostile(Collider2D hit, out EnemyHealth enemy)
    {
        enemy = hit != null ? hit.GetComponentInParent<EnemyHealth>() : null;
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) return false;
        IPlayerOwnedAlly ally = enemy.GetComponentInParent<IPlayerOwnedAlly>();
        if (ally != null && ally.IsPlayerOwnedAlly) return false;
        BaseTurretController turret = enemy.GetComponentInParent<BaseTurretController>();
        if (turret != null && (turret.IsPlayerAllied || (turret.IsShopDefense && turret.ShopOwner != null && !turret.ShopOwner.IsHostile))) return false;
        EnemyBaseAI ai = enemy.GetComponentInParent<EnemyBaseAI>();
        return ai == null || !ai.IsShopSecurityUnit || ShopRunBridge.IsShopHostileThisRun();
    }

    private bool HasDistributionLineOfSight(Vector2 origin, Vector2 direction, float distance, EnemyHealth target)
    {
        int count = Physics2D.Raycast(origin, direction, new ContactFilter2D { useTriggers = false }, distributionObstacles, distance);
        if (count == distributionObstacles.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = distributionObstacles[i].collider;
            Transform ownerRoot = weaponController != null ? weaponController.transform : transform;
            if (hit == null || hit.transform.IsChildOf(ownerRoot) || hit.GetComponentInParent<EnemyHealth>() == target) continue;
            return false;
        }
        return true;
    }
}
