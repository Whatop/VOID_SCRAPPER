using System;
using System.Collections.Generic;
using UnityEngine;

public enum TraitCategory
{
    Shared,
    WeaponSpecific
}

public enum TraitShopItemType
{
    Trait,
    Reinforcement,
    Both,
    Hidden
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
    FireRatePercent,

    // 추후 효과용. 현재 적용 로직이 없으면 표시/저장만 되고 실제 효과는 안 먹는다.
    CloseRangeDamageReductionPercent,
    DashDamageReductionPercent,
    CloseRangeSuppressionPercent,
    ChargeSightBonusPercent,
    ChargedProjectileSizePercent,
    RemovePierceDamageFalloff
}

[Serializable]
public class TraitLevelEffect
{
    [SerializeField] private int level = 1;
    [SerializeField] private TraitEffectType effectType;
    [SerializeField] private float value;

    public int Level => Mathf.Max(1, level);
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
    [SerializeField] private TraitCategory category = TraitCategory.Shared;
    [SerializeField] private WeaponTreeType weaponTreeType = WeaponTreeType.MachineGun;

    [Header("Shop")]
    [Tooltip("Trait: 추가 특성 슬롯 / Reinforcement: 기체 보강 슬롯 / Both: 둘 다 / Hidden: 상점 미노출")]
    [SerializeField] private TraitShopItemType shopItemType = TraitShopItemType.Trait;

    [Header("Level")]
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private List<TraitLevelEffect> levelEffects = new List<TraitLevelEffect>();

    public string TraitId => string.IsNullOrWhiteSpace(traitId) ? name : traitId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TraitId : displayName;
    public string Description => string.IsNullOrWhiteSpace(description) ? "특성 설명이 없습니다." : description;

    public TraitCategory Category => category;
    public WeaponTreeType WeaponTreeType => weaponTreeType;
    public TraitShopItemType ShopItemType => shopItemType;

    public int MaxLevel => Mathf.Max(1, maxLevel);
    public IReadOnlyList<TraitLevelEffect> LevelEffects => levelEffects;

    public bool CanAppearAsShopTrait =>
        shopItemType == TraitShopItemType.Trait ||
        shopItemType == TraitShopItemType.Both;

    public bool CanAppearAsShopReinforcement =>
        shopItemType == TraitShopItemType.Reinforcement ||
        shopItemType == TraitShopItemType.Both;

    public bool IsAvailableFor(WeaponTreeType selectedTree)
    {
        if (category == TraitCategory.Shared)
        {
            return true;
        }

        return weaponTreeType == selectedTree;
    }

    public string GetCategoryText()
    {
        if (category == TraitCategory.Shared)
        {
            return "공유 특성";
        }

        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => "샷건 전용 특성",
            WeaponTreeType.Sniper => "저격 전용 특성",
            WeaponTreeType.MachineGun => "기관총 전용 특성",
            _ => "전용 특성"
        };
    }
}