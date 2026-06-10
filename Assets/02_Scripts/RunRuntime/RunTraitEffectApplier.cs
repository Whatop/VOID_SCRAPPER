using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class RunTraitEffectApplier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerWeaponModifiers weaponModifiers;
    [SerializeField] private PlayerRuntimeBonusState runtimeBonusState;

    [Header("Trait Source")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeInspectorTraitDefinitions = true;
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Debug")]
    [SerializeField] private bool logAppliedTraits;

    private readonly List<TraitDefinition> resolvedTraits = new List<TraitDefinition>();

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        ApplyAllStoredTraits();
    }

    public void ApplyAllStoredTraits()
    {
        CacheReferences();

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;

        if (store == null)
        {
            return;
        }

        ResolveTraitDefinitions();

        IReadOnlyList<TraitLevelState> states = store.TraitLevels;

        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            TraitLevelState state = states[i];

            if (state == null || string.IsNullOrWhiteSpace(state.traitId))
            {
                continue;
            }

            TraitDefinition trait = FindTraitDefinition(state.traitId);

            if (trait == null)
            {
                continue;
            }

            int level = Mathf.Clamp(state.level, 1, trait.MaxLevel);

            for (int currentLevel = 1; currentLevel <= level; currentLevel++)
            {
                ApplyTraitLevel(trait, currentLevel);
            }
        }
    }

    public void ApplyTraitLevel(TraitDefinition trait, int level)
    {
        CacheReferences();

        if (trait == null || trait.LevelEffects == null)
        {
            return;
        }

        level = Mathf.Clamp(level, 1, trait.MaxLevel);

        for (int i = 0; i < trait.LevelEffects.Count; i++)
        {
            TraitLevelEffect effect = trait.LevelEffects[i];

            if (effect == null)
            {
                continue;
            }

            if (effect.Level != level)
            {
                continue;
            }

            ApplyEffect(effect.EffectType, effect.Value);
        }

        if (logAppliedTraits)
        {
            Debug.Log($"런 특성 적용: {trait.DisplayName} Lv{level}", this);
        }
    }

    private void ApplyEffect(TraitEffectType effectType, float value)
    {
        switch (effectType)
        {
            case TraitEffectType.DamagePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddDamagePercent(value);
                }
                break;

            case TraitEffectType.ProjectileSpeedPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddProjectileSpeedPercent(value);
                }
                break;

            case TraitEffectType.RangePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddRangePercent(value);
                }
                break;

            case TraitEffectType.MoveSpeedPercent:
                if (playerController != null)
                {
                    playerController.SetMoveSpeed(playerController.MoveSpeed * PercentToMultiplier(value));
                }
                break;

            case TraitEffectType.DashCooldownReduction:
                if (playerDash != null)
                {
                    playerDash.SetDashCooldown(playerDash.DashCooldown - Mathf.Abs(value));
                }
                break;

            case TraitEffectType.DashDistanceBonus:
                if (playerDash != null)
                {
                    playerDash.SetDashDistance(playerDash.DashDistance + value);
                }
                break;

            case TraitEffectType.MaxHpBonus:
                if (playerHealth != null)
                {
                    playerHealth.AddMaxHp(value, true);
                }
                break;

            case TraitEffectType.HealEfficiencyPercent:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddHealEfficiencyPercent(value);
                }
                break;

            case TraitEffectType.PickupRangeBonus:
                if (runtimeBonusState != null)
                {
                    runtimeBonusState.AddPickupRangeBonus(value);
                }
                break;

            case TraitEffectType.SpreadReductionPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddSpreadReductionPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ProjectileCountBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddProjectileCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.PierceCountBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddPierceCount(Mathf.RoundToInt(value));
                }
                break;

            case TraitEffectType.ChargeTimeReductionPercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddChargeSpeedPercent(Mathf.Abs(value));
                }
                break;

            case TraitEffectType.ChargeDamagePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddChargeDamagePercent(value);
                }
                break;

            case TraitEffectType.HomingAngleBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddHomingAngle(value);
                }
                break;

            case TraitEffectType.HomingRangeBonus:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddHomingRange(value);
                }
                break;

            case TraitEffectType.FireRatePercent:
                if (weaponModifiers != null)
                {
                    weaponModifiers.AddFireRatePercent(value);
                }
                break;

            case TraitEffectType.CloseRangeDamageReductionPercent:
            case TraitEffectType.DashDamageReductionPercent:
            case TraitEffectType.CloseRangeSuppressionPercent:
            case TraitEffectType.ChargeSightBonusPercent:
            case TraitEffectType.ChargedProjectileSizePercent:
            case TraitEffectType.RemovePierceDamageFalloff:
                Debug.LogWarning($"아직 런타임 적용 로직이 없는 특성 효과입니다: {effectType}", this);
                break;
        }
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (weaponModifiers == null)
        {
            weaponModifiers = GetComponent<PlayerWeaponModifiers>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = gameObject.AddComponent<PlayerRuntimeBonusState>();
        }
    }

    private void ResolveTraitDefinitions()
    {
        resolvedTraits.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraits);
        }

        if (!includeInspectorTraitDefinitions || traitDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            AppendUniqueTrait(traitDefinitions[i]);
        }
    }

    private void AppendUniqueTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return;
        }

        string traitId = trait.TraitId;

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition existing = resolvedTraits[i];

            if (existing != null && existing.TraitId == traitId)
            {
                return;
            }
        }

        resolvedTraits.Add(trait);
    }

    private TraitDefinition FindTraitDefinition(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition trait = resolvedTraits[i];

            if (trait != null && trait.TraitId == traitId)
            {
                return trait;
            }
        }

        return null;
    }

    private float PercentToMultiplier(float percent)
    {
        return Mathf.Max(0f, 1f + percent * 0.01f);
    }
}