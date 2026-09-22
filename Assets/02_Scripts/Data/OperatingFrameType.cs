using UnityEngine;

// Zero intentionally remains the neutral default for saves written before frames existed.
public enum OperatingFrameType
{
    Standard = 0,
    Lightweight = 1,
    Heavy = 2
}

/// <summary>Fixed pre-run configuration, separate from Traits and equipment ownership.</summary>
public readonly struct OperatingFrameProfile
{
    public OperatingFrameType Type { get; }
    public int EquipmentCount { get; }
    public int Tier { get; }
    public float MoveMultiplier { get; }
    public float DashCooldownMultiplier { get; }
    public float DashDistanceBonus { get; }
    public int MaxHpBonus { get; }
    public int CargoBonus { get; }
    public float DamagePercent { get; }
    public float HarvestYieldPercent { get; }

    public static bool IsValid(OperatingFrameType type) => type == OperatingFrameType.Standard ||
        type == OperatingFrameType.Lightweight || type == OperatingFrameType.Heavy;

    public static OperatingFrameType Normalize(OperatingFrameType type) => IsValid(type) ? type : OperatingFrameType.Standard;

    public OperatingFrameProfile(OperatingFrameType type, int equipmentCount)
    {
        Type = Normalize(type);
        EquipmentCount = Mathf.Max(0, equipmentCount);
        // Legacy equipment can exceed 24. Saturate the profile, never cap fitting or the recorded count.
        Tier = Mathf.Clamp((EquipmentCount - 1) / 6, 0, 3);
        MoveMultiplier = 1f;
        DashCooldownMultiplier = 1f;
        DashDistanceBonus = 0f;
        MaxHpBonus = 0;
        CargoBonus = 10;
        DamagePercent = 0f;
        HarvestYieldPercent = 0f;
        if (Type == OperatingFrameType.Lightweight)
        {
            MoveMultiplier = 1f + (3 - Tier) * .04f;
            DashCooldownMultiplier = 1f - (3 - Tier) * .05f;
            DashDistanceBonus = (3 - Tier) * .2f;
            MaxHpBonus = -3;
            CargoBonus = -20;
        }
        else if (Type == OperatingFrameType.Heavy)
        {
            MoveMultiplier = .9f;
            DashCooldownMultiplier = 1.15f;
            MaxHpBonus = (Tier + 1) * 2;
            CargoBonus = (Tier + 1) * 10;
        }
        else
        {
            DamagePercent = 5f;
            HarvestYieldPercent = 5f;
        }
    }
}
