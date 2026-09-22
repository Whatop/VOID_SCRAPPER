using UnityEngine;

public partial class Bullet
{
    private float equipmentWidthMultiplier = 1f;
    private float equipmentFirstHitDisplacement;

    public float EquipmentWidthMultiplier => equipmentWidthMultiplier;
    public float EquipmentFirstHitDisplacement => equipmentFirstHitDisplacement;

    public void ConfigureEquipmentWidth(float multiplier)
    {
        equipmentWidthMultiplier = Mathf.Clamp(multiplier, 1f, 1.6f);
        Vector3 scale = defaultProjectileRootScale;
        scale.x *= equipmentWidthMultiplier;
        transform.localScale = scale;
    }

    public void ConfigureFirstEnemyImpact(float displacement)
    {
        equipmentFirstHitDisplacement = owner == ProjectileOwner.Player ? Mathf.Clamp(displacement, 0f, 1.2f) : 0f;
    }

    public void SetInitialHomingTarget(Transform target)
    {
        if (!useHoming || target == null || !IsHomingTargetValid(target)) return;
        homingTarget = target;
        homingTargetRefreshTimer = Mathf.Max(.02f, homingTargetRefreshInterval);
    }

    private void ResetEquipmentProjectileState()
    {
        equipmentFirstHitDisplacement = 0f;
        equipmentWidthMultiplier = 1f;
        transform.localScale = defaultProjectileRootScale;
    }

    private void ApplyEquipmentImpact(EnemyHealth enemy, float priorHp)
    {
        if (owner != ProjectileOwner.Player || equipmentFirstHitDisplacement <= 0f || enemy == null ||
            enemy.CurrentHp >= priorHp) return;
        float displacement = equipmentFirstHitDisplacement;
        equipmentFirstHitDisplacement = 0f;
        if (enemy.IsDead || !enemy.gameObject.activeInHierarchy) return;
        // Never force a Rigidbody or bypass the receiver's boss/resistance policy.
        IKnockbackReceiver receiver = enemy.GetComponentInParent<IKnockbackReceiver>();
        receiver?.ApplyKnockback(spawnPosition, displacement);
    }
}
