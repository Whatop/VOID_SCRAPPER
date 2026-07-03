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

    public static bool EquipReinforcementImmediate(
        ReinforcementDefinition reinforcement,
        GameObject playerObject,
        ShopStructure shop = null)
    {
        if (reinforcement == null || playerObject == null)
        {
            return false;
        }

        PlayerReinforcementController controller =
            playerObject.GetComponentInChildren<PlayerReinforcementController>(true);

        if (controller == null)
        {
            controller = playerObject.AddComponent<PlayerReinforcementController>();
        }

        ShopActiveMaintenanceBay maintenanceBay = shop != null ? shop.ActiveMaintenanceBay : null;
        bool equipped = controller.EquipFromShop(reinforcement, maintenanceBay);

        if (equipped)
        {
            ShopRunBridge.SetEquippedReinforcement(reinforcement.EquipmentId, controller.CurrentCharges);
        }

        return equipped;
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
            case TraitEffectType.CargoCapacityBonus:
                if (bonusState != null)
                {
                    bonusState.AddCargoCapacityBonus(value);
                }
                ApplyCargoCapacityBonus(playerObject, value);
                break;

            case TraitEffectType.HarvestYieldPercent:
                if (bonusState != null)
                {
                    bonusState.AddHarvestYieldPercent(value);
                }
                break;

            case TraitEffectType.HarvestObjectDamagePercent:
                if (bonusState != null)
                {
                    bonusState.AddHarvestObjectDamagePercent(value);
                }
                break;

            case TraitEffectType.EmergencyReturnCapacityRatioBonus:
                if (bonusState != null)
                {
                    bonusState.AddEmergencyReturnCapacityRatioBonus(value);
                }
                ApplyEmergencyReturnRatioBonus(playerObject, value);
                break;

            case TraitEffectType.RadarScanRadiusBonus:
                if (bonusState != null)
                {
                    bonusState.AddRadarScanRadiusBonus(value);
                }
                break;

            case TraitEffectType.ActiveCooldownReductionPercent:
                if (bonusState != null)
                {
                    bonusState.AddActiveCooldownReductionPercent(value);
                }
                break;

            case TraitEffectType.RadarTauntDurationBonus:
                if (bonusState != null)
                {
                    bonusState.AddRadarTauntDurationBonus(value);
                }
                break;

            case TraitEffectType.RadarStealthDurationBonus:
                if (bonusState != null)
                {
                    bonusState.AddRadarStealthDurationBonus(value);
                }
                break;
        }
    }

    private static void ApplyCargoCapacityBonus(GameObject playerObject, float value)
    {
        if (playerObject == null)
        {
            return;
        }

        int capacityBonus = Mathf.RoundToInt(value);

        if (capacityBonus == 0)
        {
            return;
        }

        PlayerCargoController cargoController = playerObject.GetComponentInChildren<PlayerCargoController>(true);

        if (cargoController == null)
        {
            cargoController = playerObject.AddComponent<PlayerCargoController>();
        }

        int newCapacity = Mathf.Max(1, cargoController.MaxCapacity + capacityBonus);
        cargoController.SetRuntimeCargoRule(newCapacity, cargoController.EmergencyReturnRatio);
    }

    private static void ApplyEmergencyReturnRatioBonus(GameObject playerObject, float value)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerCargoController cargoController = playerObject.GetComponentInChildren<PlayerCargoController>(true);

        if (cargoController == null)
        {
            cargoController = playerObject.AddComponent<PlayerCargoController>();
        }

        float newRatio = Mathf.Clamp01(cargoController.EmergencyReturnRatio + value * 0.01f);
        cargoController.SetRuntimeCargoRule(cargoController.MaxCapacity, newRatio);
    }

}