using System;
using System.Collections.Generic;
using UnityEngine;

public enum TraitCategory
{
    Shared,
    WeaponSpecific
}

public enum TraitEffectType
{
    DamagePercent,
    ProjectileSpeedPercent,
    RangePercent,
    MoveSpeedPercent,
    DashCooldownReduction,
    DashDistanceBonus,
    MaxHpBonus,
    HealEfficiencyPercent,
    PickupRangeBonus,
    SpreadReductionPercent,
    ProjectileCountBonus,
    PierceCountBonus,
    ChargeTimeReductionPercent,
    ChargeDamagePercent,
    HomingAngleBonus,
    HomingRangeBonus,
    FireRatePercent
}

[Serializable]
public class TraitLevelEffect
{
    [SerializeField] private int level = 1;
    [SerializeField] private TraitEffectType effectType;
    [SerializeField] private float value;

    public int Level => level;
    public TraitEffectType EffectType => effectType;
    public float Value => value;
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Traits/Trait Definition")]
public class TraitDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string traitId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Category")]
    [SerializeField] private TraitCategory category;
    [SerializeField] private WeaponTreeType weaponTreeType;

    [Header("Level")]
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private List<TraitLevelEffect> levelEffects = new List<TraitLevelEffect>();

    public string TraitId => traitId;
    public string DisplayName => displayName;
    public string Description => description;

    public TraitCategory Category => category;
    public WeaponTreeType WeaponTreeType => weaponTreeType;

    public int MaxLevel => maxLevel;
    public IReadOnlyList<TraitLevelEffect> LevelEffects => levelEffects;

    public bool IsAvailableFor(WeaponTreeType selectedTree)
    {
        if (category == TraitCategory.Shared)
        {
            return true;
        }

        return weaponTreeType == selectedTree;
    }
}