using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only fresh deployment values. Never reads a live run, vitals or temporary modifiers.</summary>
public sealed class HangarDeploymentProjection
{
    public float Hp { get; private set; }
    public int Cargo { get; private set; }
    public float Armor { get; private set; }
    public float MoveSpeed { get; private set; }
    public float DashDistance { get; private set; }
    public float DashCooldown { get; private set; }
    public float Damage { get; private set; } = 1;
    public float FireRate { get; private set; } = 1;
    public float ProjectileSpeed { get; private set; } = 1;
    public float Range { get; private set; } = 1;
    public float Spread { get; private set; } = 1;
    public float ChargeTime { get; private set; } = 1;
    public float ChargeDamage { get; private set; } = 1;
    public float Harvest { get; private set; } = 1;
    public float HarvestDamage { get; private set; } = 1;
    public float Recovery { get; private set; } = 1;
    public float Scrap { get; private set; } = 1;
    public float Pickup { get; private set; }
    public float RadarRadius { get; private set; }
    public float RadarTaunt { get; private set; }
    public float RadarStealth { get; private set; }
    public float HomingAngle { get; private set; }
    public float HomingRange { get; private set; }
    public int Projectiles { get; private set; }
    public int Pierce { get; private set; }
    public StructuralFrameProfile Frame { get; private set; }
    public float MovePercent { get; private set; }
    public float DashCooldownPercent { get; private set; }
    public float DashDistanceBonus { get; private set; }

    public static HangarDeploymentProjection Calculate(ShipDefinition ship, PermanentProgress progress,
        IReadOnlyList<TraitDefinition> storyTraits, PlayerRuntimeStatApplier baseline)
    {
        if (ship == null || baseline == null) return null;
        var p = new HangarDeploymentProjection();
        var fitted = new List<string>();
        progress?.AppendEffectiveEquipment(fitted, ship.DefaultWeaponTree);
        p.Frame = new StructuralFrameProfile(StructuralFrameProfile.ResolveModules(fitted));
        p.Hp = ship.MaxHp;
        p.Cargo = ship.CargoCapacity;
        p.Armor = Mathf.Clamp(baseline.BaseStartingArmor, 0, Mathf.Max(baseline.BaseMaxArmor, baseline.BaseStartingArmor));
        p.MoveSpeed = Mathf.Max(.1f, baseline.BaseMoveSpeed * (1 + ship.MoveSpeedBonusPercent * .01f));
        p.DashDistance = Mathf.Max(.1f, baseline.BaseDashDistance + ship.DashDistanceBonus);
        p.DashCooldown = Mathf.Max(.05f, baseline.BaseDashCooldown - Mathf.Abs(ship.DashCooldownReduction));
        p.Harvest *= Bonus(ship.HarvestYieldBonusPercent);
        p.HarvestDamage *= Bonus(ship.HarvestObjectDamageBonusPercent);
        // Same order as PlayerRuntimeStatApplier: ship, ONE fused frame, technology, story.
        p.Hp = Mathf.Max(1, p.Hp + p.Frame.MaxHpBonus);
        p.Cargo = Mathf.Max(1, p.Cargo + p.Frame.CargoBonus);
        p.DashDistance += p.Frame.DashDistanceBonus;
        p.Damage *= Weapon(p.Frame.DamagePercent);
        p.Harvest *= Bonus(p.Frame.HarvestYieldPercent);
        if (progress != null)
        {
            foreach (var tech in SectorTechnologyCatalog.Definitions)
            {
                float value = tech.GetEffectValue(progress.GetSectorTechnologyLevel(tech.Id));
                switch (tech.EffectType)
                {
                    case SectorTechnologyEffectType.MaxHp: p.Hp += value; break;
                    case SectorTechnologyEffectType.StartingArmor: p.Armor += value; break;
                    case SectorTechnologyEffectType.DamagePercent: p.Damage *= Weapon(value); break;
                    case SectorTechnologyEffectType.HealEfficiencyPercent: p.Recovery *= Bonus(value); break;
                    case SectorTechnologyEffectType.ScrapGainPercent: p.Scrap *= Bonus(value); break;
                }
            }
            p.ClampChassis();
            if (storyTraits != null) foreach (var trait in storyTraits)
                if (trait != null && trait.IsPersistentStoryTrait && progress.HasPersistentStoryTrait(trait) &&
                    trait.IsAvailableFor(ship.DefaultWeaponTree)) p.ApplyLevelOne(trait, false);
        }
        p.ClampChassis();
        // Mirror RunRuntimeTraitStore.InitializeDeployment, not permanent levels or equipment MAX.
        foreach (string id in fitted)
        {
            var trait = progress.EquipmentCatalog.FindById(id);
            if (trait == null || !trait.CanAppearAsRandomDropTrait || !trait.IsAvailableFor(ship.DefaultWeaponTree) ||
                trait.HasRuntimePrerequisites || StructuralFrameProfile.ModuleFor(id) != StructuralFrameModules.None) continue;
            p.ApplyLevelOne(trait, true);
        }
        p.MoveSpeed *= p.Frame.MoveMultiplier;
        p.DashCooldown *= p.Frame.DashCooldownMultiplier;
        p.MovePercent = (p.MoveSpeed / Mathf.Max(.1f, baseline.BaseMoveSpeed) - 1) * 100;
        p.DashCooldownPercent = (p.DashCooldown / Mathf.Max(.05f, baseline.BaseDashCooldown) - 1) * 100;
        p.DashDistanceBonus = p.DashDistance - Mathf.Max(.1f, baseline.BaseDashDistance);
        return p;
    }

    private static float Weapon(float value) => Mathf.Max(.05f, 1 + value * .01f);
    private static float Bonus(float value) => Mathf.Max(0, 1 + value * .01f);
    private void ClampChassis()
    {
        Hp = Mathf.Max(1, Hp); Cargo = Mathf.Max(1, Cargo);
        MoveSpeed = Mathf.Max(.1f, MoveSpeed); DashDistance = Mathf.Max(.1f, DashDistance);
        DashCooldown = Mathf.Max(.05f, DashCooldown);
    }
    private void ApplyLevelOne(TraitDefinition trait, bool equipment)
    {
        if (trait.LevelEffects == null) return;
        foreach (var effect in trait.LevelEffects)
        {
            if (effect == null || effect.Level != 1) continue;
            float v = effect.Value;
            switch (effect.EffectType)
            {
                case TraitEffectType.MaxHpBonus: Hp += v; break;
                case TraitEffectType.CargoCapacityBonus: Cargo += Mathf.RoundToInt(v); break;
                case TraitEffectType.MoveSpeedPercent: MoveSpeed *= equipment ? Weapon(v) : 1 + v * .01f; break;
                case TraitEffectType.DashDistanceBonus: DashDistance += v; break;
                case TraitEffectType.DashCooldownReduction: DashCooldown -= Mathf.Abs(v); break;
                case TraitEffectType.DamagePercent: Damage *= Weapon(v); break;
                case TraitEffectType.FireRatePercent: FireRate *= Weapon(v); break;
                case TraitEffectType.ProjectileSpeedPercent: ProjectileSpeed *= Weapon(v); break;
                case TraitEffectType.RangePercent: Range *= Weapon(v); break;
                case TraitEffectType.SpreadReductionPercent: Spread *= Mathf.Clamp(1 - Mathf.Clamp01(Mathf.Abs(v) * .01f), .01f, 10); break;
                case TraitEffectType.ChargeTimeReductionPercent: ChargeTime /= Weapon(Mathf.Abs(v)); break;
                case TraitEffectType.ChargeDamagePercent: ChargeDamage *= Weapon(v); break;
                case TraitEffectType.HarvestYieldPercent: Harvest *= Bonus(v); break;
                case TraitEffectType.HarvestObjectDamagePercent: HarvestDamage *= Bonus(v); break;
                case TraitEffectType.HealEfficiencyPercent: Recovery *= Bonus(v); break;
                case TraitEffectType.PickupRangeBonus: Pickup = Mathf.Max(0, Pickup + v); break;
                case TraitEffectType.RadarScanRadiusBonus: RadarRadius += v; break;
                case TraitEffectType.RadarTauntDurationBonus: RadarTaunt += v; break;
                case TraitEffectType.RadarStealthDurationBonus: RadarStealth += v; break;
                case TraitEffectType.HomingAngleBonus: HomingAngle += v; break;
                case TraitEffectType.HomingRangeBonus: HomingRange += v; break;
                case TraitEffectType.ProjectileCountBonus: Projectiles += Mathf.RoundToInt(v); break;
                case TraitEffectType.PierceCountBonus: Pierce += Mathf.RoundToInt(v); break;
                // Conditional combat behaviours/protocols are not unconditional effective stats.
            }
            if (equipment) ClampChassis(); // Runtime setters clamp each Lv1 effect.
        }
    }
}
