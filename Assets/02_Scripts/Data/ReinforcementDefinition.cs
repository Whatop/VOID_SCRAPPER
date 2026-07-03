using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum ReinforcementUseType
{
    // 구버전 호환용. 신규 장비에는 쓰지 않는다. 상점/랜덤 드랍 후보에서도 제외한다.
    LegacyOneShotConsumable = 0,
    RechargeableConsumable = 1,
    ActiveEquipment = 2
}

public enum ReinforcementAvailability
{
    Any,
    MachineGunOnly,
    ShotgunOnly,
    SniperOnly
}

public enum ReinforcementRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum ReinforcementEffectType
{
    HealFlat,
    AddArmor,
    AddInvincibleTime,
    ClearEnemyProjectiles,
    DamageNearbyEnemies,
    KnockbackNearbyEnemies,
    TemporaryDamagePercent,
    TemporaryFireRatePercent,
    TemporaryMoveSpeedPercent,
    TemporaryDashCooldownReductionPercent,
    SpawnPrefabAtPlayer,
    EmergencyReturn
}

[Serializable]
public class ReinforcementEffect
{
    [SerializeField] private ReinforcementEffectType effectType;
    [SerializeField] private float value = 1f;
    [SerializeField] private float radius = 0f;
    [SerializeField] private float duration = 0f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private GameObject prefab;
    [SerializeField] private float prefabLifetime = 1f;

    public ReinforcementEffectType EffectType => effectType;
    public float Value => value;
    public float Radius => Mathf.Max(0f, radius);
    public float Duration => Mathf.Max(0f, duration);
    public LayerMask TargetLayer => targetLayer;
    public GameObject Prefab => prefab;
    public float PrefabLifetime => Mathf.Max(0.01f, prefabLifetime);
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Reinforcements/Reinforcement Definition")]
public class ReinforcementDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string equipmentId;
    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Visual")]
    [SerializeField] private Sprite icon;

    [Header("Grade")]
    [SerializeField] private ReinforcementRarity rarity = ReinforcementRarity.Common;

    [Tooltip("0 이하이면 등급 기본 배율을 사용합니다. 일반 1.0 / 희귀 1.35 / 영웅 1.8 / 전설 2.5")]
    [SerializeField] private float rarityPriceMultiplier;

    [Tooltip("랜덤 드랍 가중치. 0 이하이면 등급 기본값을 사용합니다. 일반 100 / 희귀 45 / 영웅 16 / 전설 4")]
    [SerializeField] private float randomDropWeight;

    [Tooltip("분해 시 지급할 스크랩. 0 이하이면 가격과 등급으로 자동 계산합니다.")]
    [SerializeField] private int dismantleScrapReward;

    [Header("Use Rule")]
    [SerializeField] private ReinforcementUseType useType = ReinforcementUseType.RechargeableConsumable;
    [SerializeField] private ReinforcementAvailability availability = ReinforcementAvailability.Any;

    [Header("Shop")]
    [SerializeField] private int cost = 60;
    [SerializeField] private bool canAppearInShop = true;

    [Header("Drop")]
    [SerializeField] private bool canAppearAsRandomDrop = true;

    [Header("Charge")]
    [SerializeField] private int maxCharges = 1;
    [SerializeField] private bool startWithFullCharges = true;
    [SerializeField] private float rechargeSeconds = 12f;

    [Header("Effects")]
    [SerializeField] private List<ReinforcementEffect> effects = new List<ReinforcementEffect>();

    public string EquipmentId => string.IsNullOrWhiteSpace(equipmentId) ? name : equipmentId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EquipmentId : displayName;
    public string Description => string.IsNullOrWhiteSpace(description) ? "장비 설명이 없습니다." : description;
    public Sprite Icon => icon;

    public ReinforcementRarity Rarity => rarity;
    public ReinforcementUseType UseType => useType;
    public ReinforcementAvailability Availability => availability;
    public int Cost => Mathf.Max(0, cost);
    public int MaxCharges => Mathf.Max(1, maxCharges);
    public bool StartWithFullCharges => startWithFullCharges;
    public float RechargeSeconds => Mathf.Max(0f, rechargeSeconds);
    public IReadOnlyList<ReinforcementEffect> Effects => effects;

    public bool IsLegacyOneShot => useType == ReinforcementUseType.LegacyOneShotConsumable;
    public bool CanAppearInShop => canAppearInShop && !IsLegacyOneShot;
    public bool CanAppearAsRandomDrop => canAppearAsRandomDrop && !IsLegacyOneShot && !HasEffect(ReinforcementEffectType.EmergencyReturn);

    public float EffectivePriceMultiplier => rarityPriceMultiplier > 0f ? rarityPriceMultiplier : GetDefaultPriceMultiplier(rarity);
    public float EffectiveRandomDropWeight => randomDropWeight > 0f ? randomDropWeight : GetDefaultDropWeight(rarity);

    public int DismantleScrapReward
    {
        get
        {
            if (dismantleScrapReward > 0)
            {
                return dismantleScrapReward;
            }

            float rarityBonus = rarity switch
            {
                ReinforcementRarity.Common => 0.16f,
                ReinforcementRarity.Rare => 0.20f,
                ReinforcementRarity.Epic => 0.25f,
                ReinforcementRarity.Legendary => 0.32f,
                _ => 0.16f
            };

            return Mathf.Max(2, Mathf.RoundToInt(Cost * rarityBonus));
        }
    }

    public bool UsesRecharge =>
        useType == ReinforcementUseType.RechargeableConsumable ||
        useType == ReinforcementUseType.ActiveEquipment;

    public bool IsConsumedWhenEmpty => false;

    public bool HasEffect(ReinforcementEffectType targetEffectType)
    {
        if (effects == null)
        {
            return false;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            ReinforcementEffect effect = effects[i];
            if (effect != null && effect.EffectType == targetEffectType)
            {
                return true;
            }
        }

        return false;
    }

    public bool CanUseFor(WeaponTreeType selectedTree)
    {
        return availability switch
        {
            ReinforcementAvailability.Any => true,
            ReinforcementAvailability.MachineGunOnly => selectedTree == WeaponTreeType.MachineGun,
            ReinforcementAvailability.ShotgunOnly => selectedTree == WeaponTreeType.Shotgun,
            ReinforcementAvailability.SniperOnly => selectedTree == WeaponTreeType.Sniper,
            _ => true
        };
    }

    public string GetRarityText()
    {
        return rarity switch
        {
            ReinforcementRarity.Common => "일반",
            ReinforcementRarity.Rare => "희귀",
            ReinforcementRarity.Epic => "영웅",
            ReinforcementRarity.Legendary => "전설",
            _ => "일반"
        };
    }

    public Color GetRarityColor()
    {
        return rarity switch
        {
            ReinforcementRarity.Common => Color.white,
            ReinforcementRarity.Rare => new Color(0.25f, 1f, 0.35f, 1f),
            ReinforcementRarity.Epic => new Color(0.75f, 0.35f, 1f, 1f),
            ReinforcementRarity.Legendary => new Color(1f, 0.55f, 0.12f, 1f),
            _ => Color.white
        };
    }

    public string GetUseTypeText()
    {
        return useType switch
        {
            ReinforcementUseType.LegacyOneShotConsumable => "구버전 1회용 장비",
            ReinforcementUseType.RechargeableConsumable => "충전식 사용 장비",
            ReinforcementUseType.ActiveEquipment => "액티브 장비",
            _ => "장비"
        };
    }

    public string GetAvailabilityText()
    {
        return availability switch
        {
            ReinforcementAvailability.Any => "모든 기체 사용 가능",
            ReinforcementAvailability.MachineGunOnly => "기관총 기체 전용",
            ReinforcementAvailability.ShotgunOnly => "샷건 기체 전용",
            ReinforcementAvailability.SniperOnly => "스나이퍼 기체 전용",
            _ => "모든 기체 사용 가능"
        };
    }

    public string BuildEffectSummary()
    {
        if (effects == null || effects.Count == 0)
        {
            return "효과 정보 없음";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < effects.Count; i++)
        {
            ReinforcementEffect effect = effects[i];

            if (effect == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(ReinforcementEffectTextUtility.Format(effect));
        }

        return builder.Length > 0 ? builder.ToString() : "효과 정보 없음";
    }

    public static float GetDefaultPriceMultiplier(ReinforcementRarity rarity)
    {
        return rarity switch
        {
            ReinforcementRarity.Common => 1f,
            ReinforcementRarity.Rare => 1.35f,
            ReinforcementRarity.Epic => 1.8f,
            ReinforcementRarity.Legendary => 2.5f,
            _ => 1f
        };
    }

    public static float GetDefaultDropWeight(ReinforcementRarity rarity)
    {
        return rarity switch
        {
            ReinforcementRarity.Common => 100f,
            ReinforcementRarity.Rare => 45f,
            ReinforcementRarity.Epic => 16f,
            ReinforcementRarity.Legendary => 4f,
            _ => 100f
        };
    }
}

public static class ReinforcementEffectTextUtility
{
    public static string Format(ReinforcementEffect effect)
    {
        if (effect == null)
        {
            return string.Empty;
        }

        return effect.EffectType switch
        {
            ReinforcementEffectType.HealFlat => $"HP {effect.Value:0.#} 회복",
            ReinforcementEffectType.AddArmor => $"Armor {effect.Value:0.#} 충전",
            ReinforcementEffectType.AddInvincibleTime => $"{effect.Value:0.#}초 무적",
            ReinforcementEffectType.ClearEnemyProjectiles => $"반경 {effect.Radius:0.#} 적 탄환 제거",
            ReinforcementEffectType.DamageNearbyEnemies => $"반경 {effect.Radius:0.#} 적에게 {effect.Value:0.#} 피해",
            ReinforcementEffectType.KnockbackNearbyEnemies => $"반경 {effect.Radius:0.#} 적 밀쳐내기 {effect.Value:0.#}",
            ReinforcementEffectType.TemporaryDamagePercent => $"{effect.Duration:0.#}초 공격력 +{effect.Value:0.#}%",
            ReinforcementEffectType.TemporaryFireRatePercent => $"{effect.Duration:0.#}초 연사력 +{effect.Value:0.#}%",
            ReinforcementEffectType.TemporaryMoveSpeedPercent => $"{effect.Duration:0.#}초 이동속도 +{effect.Value:0.#}%",
            ReinforcementEffectType.TemporaryDashCooldownReductionPercent => $"{effect.Duration:0.#}초 대쉬 쿨다운 -{Mathf.Abs(effect.Value):0.#}%",
            ReinforcementEffectType.SpawnPrefabAtPlayer => "플레이어 위치에 장비 효과 생성",
            ReinforcementEffectType.EmergencyReturn => "긴급복귀 준비를 시작한다",
            _ => $"{effect.EffectType} {effect.Value:0.##}"
        };
    }
}
