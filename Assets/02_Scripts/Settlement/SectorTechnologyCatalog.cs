using System;
using System.Collections.Generic;
using UnityEngine;

public enum SectorTechnologyEffectType
{
    MaxHp = 0,
    StartingArmor = 1,
    HealEfficiencyPercent = 2,
    DamagePercent = 3,
    ScrapGainPercent = 4
}

public sealed class SectorTechnologyDefinition
{
    private readonly int[] alloyCosts;

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string IconLabel { get; }
    public Color AccentColor { get; }
    public SectorTechnologyEffectType EffectType { get; }
    public int MaxLevel { get; }
    public float EffectPerLevel { get; }

    public SectorTechnologyDefinition(
        string id,
        string displayName,
        string description,
        string iconLabel,
        Color accentColor,
        SectorTechnologyEffectType effectType,
        int maxLevel,
        float effectPerLevel,
        params int[] alloyCosts)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        IconLabel = iconLabel;
        AccentColor = accentColor;
        EffectType = effectType;
        MaxLevel = Mathf.Max(1, maxLevel);
        EffectPerLevel = effectPerLevel;
        this.alloyCosts = alloyCosts ?? Array.Empty<int>();
    }

    public int GetUpgradeCost(int nextLevel)
    {
        if (nextLevel <= 0 || nextLevel > MaxLevel || nextLevel > alloyCosts.Length)
        {
            return 0;
        }

        return Mathf.Max(0, alloyCosts[nextLevel - 1]);
    }

    public float GetEffectValue(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * EffectPerLevel;
    }

    public string FormatEffect(int level)
    {
        float value = GetEffectValue(level);

        return EffectType switch
        {
            SectorTechnologyEffectType.MaxHp => $"최대 체력 +{value:0}",
            SectorTechnologyEffectType.StartingArmor => $"시작 방어도 +{value:0}",
            SectorTechnologyEffectType.HealEfficiencyPercent => $"회복 효율 +{value:0}%",
            SectorTechnologyEffectType.DamagePercent => $"공격력 +{value:0}%",
            SectorTechnologyEffectType.ScrapGainPercent => $"스크랩 획득량 +{value:0}%",
            _ => string.Empty
        };
    }
}

public static class SectorTechnologyCatalog
{
    public const string StabilizedFrameId = "sector1_stabilized_frame";
    public const string ReinforcedBulkheadId = "sector1_reinforced_bulkhead";
    public const string FieldRepairLatticeId = "sector1_field_repair_lattice";
    public const string DamageCalibratorId = "sector1_damage_calibrator";
    public const string ScrapOptimizerId = "sector1_scrap_optimizer";

    private const int MaxPrototypeLevel = 3;
    private static readonly int[] prototypeAlloyCosts = { 2, 3, 5 };

    private static readonly SectorTechnologyDefinition[] definitions =
    {
        CreateDefinition(
            StabilizedFrameId,
            "안정화 프레임",
            "기체 프레임의 구조 안정성을 높입니다.",
            "HP",
            new Color(0.72f, 0.86f, 0.92f, 1f),
            SectorTechnologyEffectType.MaxHp,
            2f
        ),
        CreateDefinition(
            ReinforcedBulkheadId,
            "강화 격벽",
            "출격 시 방어도 예비량을 확보합니다.",
            "AR",
            new Color(0.68f, 0.78f, 0.88f, 1f),
            SectorTechnologyEffectType.StartingArmor,
            2f
        ),
        CreateDefinition(
            FieldRepairLatticeId,
            "현장 수리 격자",
            "현장에서 받는 회복 효과를 증폭합니다.",
            "+",
            new Color(0.58f, 0.9f, 0.82f, 1f),
            SectorTechnologyEffectType.HealEfficiencyPercent,
            10f
        ),
        CreateDefinition(
            DamageCalibratorId,
            "공격 보정기",
            "기체 무장 출력을 정밀 보정해 공격력을 높입니다.",
            "AT",
            new Color(1f, 0.65f, 0.35f, 1f),
            SectorTechnologyEffectType.DamagePercent,
            5f
        ),
        CreateDefinition(
            ScrapOptimizerId,
            "회수 최적화기",
            "스크랩 회수 공정을 최적화해 획득량을 높입니다.",
            "SC",
            new Color(0.92f, 0.82f, 0.42f, 1f),
            SectorTechnologyEffectType.ScrapGainPercent,
            5f
        )
    };

    public static IReadOnlyList<SectorTechnologyDefinition> Definitions => definitions;

    public static bool TryGet(string id, out SectorTechnologyDefinition definition)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Id == id)
            {
                definition = definitions[i];
                return true;
            }
        }

        definition = null;
        return false;
    }

    private static SectorTechnologyDefinition CreateDefinition(
        string id,
        string displayName,
        string description,
        string iconLabel,
        Color accentColor,
        SectorTechnologyEffectType effectType,
        float effectPerLevel)
    {
        return new SectorTechnologyDefinition(
            id,
            displayName,
            description,
            iconLabel,
            accentColor,
            effectType,
            MaxPrototypeLevel,
            effectPerLevel,
            prototypeAlloyCosts
        );
    }
}
