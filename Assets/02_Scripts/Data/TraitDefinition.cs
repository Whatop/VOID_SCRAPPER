using System;
using System.Collections.Generic;
using UnityEngine;

public enum TraitCategory
{
    Shared,
    WeaponSpecific
}

public enum TraitRarity
{
    Common,
    Rare,
    Special
}

public enum TraitShopItemType
{
    Trait = 0,

    // Serialized migration only. Do not use for new content.
    // 기존 Reinforcement/Both TraitDefinition 에셋이 깨지지 않도록 숫자 값만 남긴다.
    LegacyReinforcement = 1,
    LegacyBoth = 2,

    Hidden = 3
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

    CloseRangeDamageReductionPercent,
    DashDamageReductionPercent,
    CloseRangeSuppressionPercent,
    ChargeSightBonusPercent,
    ChargedProjectileSizePercent,
    RemovePierceDamageFalloff,

    // Harvest / cargo loop 확장.
    CargoCapacityBonus,
    HarvestYieldPercent,
    HarvestObjectDamagePercent,
    EmergencyReturnCapacityRatioBonus,
    RadarScanRadiusBonus,
    ActiveCooldownReductionPercent,
    RadarTauntDurationBonus,
    RadarStealthDurationBonus,

    // Campaign boss-exclusive passives. Appended to preserve serialized values.
    SectorBarrierProtocol,
    MatterReconstructorProtocol,
    PhaseAfterimageProtocol
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

    [Header("Visual")]
    [SerializeField] private Sprite icon;

    [Header("Grade")]
    [SerializeField] private TraitRarity rarity = TraitRarity.Common;

    [Header("Category")]
    [SerializeField] private TraitCategory category = TraitCategory.Shared;
    [SerializeField] private WeaponTreeType weaponTreeType = WeaponTreeType.MachineGun;

    [Header("Exposure")]
    [Tooltip("Trait: 추가 특성으로 노출 / Hidden: 기본 노출 안 함. Legacy 값은 기존 에셋 호환용이며 신규 콘텐츠에 쓰지 않습니다.")]
    [SerializeField] private TraitShopItemType shopItemType = TraitShopItemType.Trait;

    [Header("Level")]
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private List<TraitLevelEffect> levelEffects = new List<TraitLevelEffect>();

    public string TraitId => string.IsNullOrWhiteSpace(traitId) ? name : traitId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TraitId : displayName;
    public string Description => string.IsNullOrWhiteSpace(description) ? "특성 설명이 없습니다." : description;

    public Sprite Icon => icon;
    public TraitRarity Rarity => rarity;

    public TraitCategory Category => category;
    public WeaponTreeType WeaponTreeType => weaponTreeType;
    public TraitShopItemType ShopItemType => shopItemType;

    public int MaxLevel => Mathf.Max(1, maxLevel);
    public IReadOnlyList<TraitLevelEffect> LevelEffects => levelEffects;

    public bool CanAppearAsShopTrait => shopItemType == TraitShopItemType.Trait;
    public bool CanAppearAsLevelUpTrait => shopItemType == TraitShopItemType.Trait;
    public bool IsHidden => shopItemType == TraitShopItemType.Hidden;

    public bool IsLegacyReinforcementTrait =>
        shopItemType == TraitShopItemType.LegacyReinforcement ||
        shopItemType == TraitShopItemType.LegacyBoth;


    public string GetRarityText()
    {
        return rarity switch
        {
            TraitRarity.Common => "일반",
            TraitRarity.Rare => "희귀",
            TraitRarity.Special => "특수",
            _ => "일반"
        };
    }

    public Color GetRarityColor()
    {
        return rarity switch
        {
            TraitRarity.Common => Color.white,
            TraitRarity.Rare => new Color(0.25f, 0.85f, 1f, 1f),
            TraitRarity.Special => new Color(1f, 0.55f, 0.12f, 1f),
            _ => Color.white
        };
    }

    public float EffectiveRandomDropWeight => rarity switch
    {
        TraitRarity.Common => 100f,
        TraitRarity.Rare => 45f,
        TraitRarity.Special => 8f,
        _ => 100f
    };

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
            return "공용 특성";
        }

        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => "샷건 전용 특성",
            WeaponTreeType.Sniper => "스나이퍼 전용 특성",
            WeaponTreeType.MachineGun => "기관총 전용 특성",
            _ => "전용 특성"
        };
    }
}
