using UnityEngine;

public static class ShopRuntimeEffectApplier
{
    public static void ApplyTraitImmediate(TraitDefinition trait, GameObject playerObject)
    {
        if (trait == null || playerObject == null || trait.LevelEffects == null)
        {
            return;
        }

        for (int i = 0; i < trait.LevelEffects.Count; i++)
        {
            TraitLevelEffect effect = trait.LevelEffects[i];

            if (effect == null || effect.Level != 1)
            {
                continue;
            }

            ApplyTraitEffect(effect.EffectType, effect.Value, playerObject);
        }
    }

    public static void ApplyWeaponReinforcement(WeaponTreeType weaponTreeType, GameObject playerObject)
    {
        if (playerObject == null)
        {
            Debug.LogWarning("기체 보강 실패: Player 오브젝트가 없습니다.");
            return;
        }

        PlayerWeaponModifiers modifiers = playerObject.GetComponentInChildren<PlayerWeaponModifiers>(true);

        if (modifiers == null)
        {
            Debug.LogWarning("기체 보강 실패: PlayerWeaponModifiers를 찾지 못했습니다.");
            return;
        }

        switch (weaponTreeType)
        {
            case WeaponTreeType.Shotgun:
                modifiers.AddDamagePercent(15f);
                modifiers.AddSpreadReductionPercent(10f);
                break;

            case WeaponTreeType.Sniper:
                modifiers.AddChargeSpeedPercent(20f);
                modifiers.AddPierceCount(1);
                break;

            case WeaponTreeType.MachineGun:
                modifiers.AddFireRatePercent(15f);
                modifiers.AddHomingAngle(10f);
                break;
        }
    }

    private static void ApplyTraitEffect(TraitEffectType effectType, float value, GameObject playerObject)
    {
        PlayerHealth health = playerObject.GetComponentInChildren<PlayerHealth>(true);
        PlayerController2D controller = playerObject.GetComponentInChildren<PlayerController2D>(true);
        PlayerDash dash = playerObject.GetComponentInChildren<PlayerDash>(true);
        PlayerWeaponModifiers modifiers = playerObject.GetComponentInChildren<PlayerWeaponModifiers>(true);
        PlayerRuntimeBonusState bonusState = playerObject.GetComponentInChildren<PlayerRuntimeBonusState>(true);

        if (bonusState == null)
        {
            bonusState = playerObject.AddComponent<PlayerRuntimeBonusState>();
        }

        switch (effectType)
        {
            case TraitEffectType.DamagePercent:
                if (modifiers != null)
                {
                    modifiers.AddDamagePercent(value);
                }
                break;

            case TraitEffectType.ProjectileSpeedPercent:
                if (modifiers != null)
                {
                    modifiers.AddProjectileSpeedPercent(value);
                }
                break;

            case TraitEffectType.RangePercent:
                if (modifiers != null)
                {
                    modifiers.AddRangePercent(value);
                }
                break;

            case TraitEffectType.MoveSpeedPercent:
                if (controller != null)
                {
                    controller.SetMoveSpeed(controller.MoveSpeed * (1f + value * 0.01f));
                }
                break;

            case TraitEffectType.DashCooldownReduction:
                if (dash != null)
                {
                    dash.SetDashCooldown(dash.DashCooldown - Mathf.Abs(value));
                }
                break;

            case TraitEffectType.DashDistanceBonus:
                if (dash != null)
                {
                    dash.SetDashDistance(dash.DashDistance + value);
                }
                break;

            case TraitEffectType.MaxHpBonus:
                if (health != null)
                {
                    health.AddMaxHp(value, true);
                }
                break;

            case TraitEffectType.HealEfficiencyPercent:
                if (bonusState != null)
                {
                    bonusState.AddHealEfficiencyPercent(value);
                }
                break;

            case TraitEffectType.PickupRangeBonus:
                if (bonusState != null)
                {
                    bonusState.AddPickupRangeBonus(value);
                }
                break;

            case TraitEffectType.SpreadReductionPercent:
                if (modifiers != null)
                {
                    modifiers.AddSpreadReductionPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ProjectileCountBonus:
                if (modifiers != null)
                {
                    modifiers.AddProjectileCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.PierceCountBonus:
                if (modifiers != null)
                {
                    modifiers.AddPierceCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.ChargeTimeReductionPercent:
                if (modifiers != null)
                {
                    modifiers.AddChargeSpeedPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ChargeDamagePercent:
                if (modifiers != null)
                {
                    modifiers.AddChargeDamagePercent(value);
                }
                break;

            case TraitEffectType.HomingAngleBonus:
                if (modifiers != null)
                {
                    modifiers.AddHomingAngle(value);
                }
                break;

            case TraitEffectType.HomingRangeBonus:
                if (modifiers != null)
                {
                    modifiers.AddHomingRange(value);
                }
                break;

            case TraitEffectType.FireRatePercent:
                if (modifiers != null)
                {
                    modifiers.AddFireRatePercent(value);
                }
                break;
        }
    }
}