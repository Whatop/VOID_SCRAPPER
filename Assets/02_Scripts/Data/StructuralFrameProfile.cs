using System;
using System.Collections.Generic;

[Flags]
public enum StructuralFrameModules
{
    None = 0, Lightweight = 1, Standard = 2, Heavy = 4,
    LightweightStandard = Lightweight | Standard,
    LightweightHeavy = Lightweight | Heavy,
    StandardHeavy = Standard | Heavy,
    Integrated = Lightweight | Standard | Heavy
}

/// <summary>Exactly one profile from immutable deployment fitting; never additive per module.</summary>
public readonly struct StructuralFrameProfile
{
    public const string LightweightId = "shared_lightweight_frame";
    public const string StandardId = "shared_standard_frame";
    public const string HeavyId = "shared_heavy_frame";
    public StructuralFrameModules Modules { get; }
    public float MoveMultiplier { get; }
    public float DashCooldownMultiplier { get; }
    public float DashDistanceBonus { get; }
    public int MaxHpBonus { get; }
    public int CargoBonus { get; }
    public float DamagePercent { get; }
    public float HarvestYieldPercent { get; }

    public static StructuralFrameModules ModuleFor(string id) => id switch
    {
        LightweightId => StructuralFrameModules.Lightweight,
        StandardId => StructuralFrameModules.Standard,
        HeavyId => StructuralFrameModules.Heavy,
        _ => StructuralFrameModules.None
    };

    public static StructuralFrameModules ResolveModules(IEnumerable<string> fitted)
    {
        var result = StructuralFrameModules.None;
        if (fitted != null) foreach (string id in fitted) result |= ModuleFor(id);
        return result;
    }

    public StructuralFrameProfile(StructuralFrameModules modules)
    {
        Modules = modules & StructuralFrameModules.Integrated;
        MoveMultiplier = DashCooldownMultiplier = 1f;
        DashDistanceBonus = DamagePercent = HarvestYieldPercent = 0f;
        MaxHpBonus = CargoBonus = 0;
        switch (Modules)
        {
            case StructuralFrameModules.Lightweight:
                MoveMultiplier = 1.12f; DashCooldownMultiplier = .85f; DashDistanceBonus = .6f;
                MaxHpBonus = -3; CargoBonus = -20; break;
            case StructuralFrameModules.Standard:
                DamagePercent = HarvestYieldPercent = 5f; CargoBonus = 10; break;
            case StructuralFrameModules.Heavy:
                MoveMultiplier = .9f; DashCooldownMultiplier = 1.15f; MaxHpBonus = 6; CargoBonus = 30; break;
            case StructuralFrameModules.LightweightStandard:
                MoveMultiplier = 1.08f; DashCooldownMultiplier = .9f; DamagePercent = HarvestYieldPercent = 3f;
                MaxHpBonus = -2; CargoBonus = -5; break;
            case StructuralFrameModules.LightweightHeavy:
                MoveMultiplier = 1.04f; DashDistanceBonus = .3f; MaxHpBonus = 2; CargoBonus = 10; break;
            case StructuralFrameModules.StandardHeavy:
                MoveMultiplier = .94f; DashCooldownMultiplier = 1.08f; DamagePercent = HarvestYieldPercent = 3f;
                MaxHpBonus = 4; CargoBonus = 25; break;
            case StructuralFrameModules.Integrated:
                MoveMultiplier = 1.03f; DamagePercent = HarvestYieldPercent = 3f; MaxHpBonus = 3;
                CargoBonus = 15; DashDistanceBonus = .15f; break;
        }
    }
}
