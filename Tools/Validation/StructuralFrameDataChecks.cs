using System;
using System.Collections.Generic;
using System.Linq;

// Runs the production value resolver and save DTO without a Unity license.
// Unity serialization, scene lifecycle and rendering remain covered by the Editor suite.
public static class StructuralFrameDataChecks
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    public static int Main()
    {
        float[][] expected = {
            new[] { 1f, 1f, 0f, 0f, 0f, 0f },
            new[] { 1.12f, .85f, .6f, -3f, -20f, 0f },
            new[] { 1f, 1f, 0f, 0f, 10f, 5f },
            new[] { 1.08f, .9f, 0f, -2f, -5f, 3f },
            new[] { .9f, 1.15f, 0f, 6f, 30f, 0f },
            new[] { 1.04f, 1f, .3f, 2f, 10f, 0f },
            new[] { .94f, 1.08f, 0f, 4f, 25f, 3f },
            new[] { 1.03f, 1f, .15f, 3f, 15f, 3f }
        };
        string[] frames = { StructuralFrameProfile.LightweightId, StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId };
        for (int bits = 0; bits < 8; bits++)
        {
            var modules = (StructuralFrameModules)bits;
            var profile = new StructuralFrameProfile(modules);
            float[] actual = { profile.MoveMultiplier, profile.DashCooldownMultiplier, profile.DashDistanceBonus,
                profile.MaxHpBonus, profile.CargoBonus, profile.DamagePercent };
            for (int i = 0; i < actual.Length; i++) Check(Math.Abs(actual[i] - expected[bits][i]) < .00001, "Profile " + bits + " stat " + i);
            Check(profile.HarvestYieldPercent == expected[bits][5], "Harvest " + bits);
            foreach (int count in new[] { 0, 6, 12, 18, 24, 38 })
            {
                var fitted = frames.Where(id => (StructuralFrameProfile.ModuleFor(id) & modules) != 0).ToList();
                fitted.AddRange(Enumerable.Repeat("shared_cargo_bay", count));
                fitted.AddRange(fitted.ToArray());
                fitted.Add("unknown_equipment"); fitted.Add(null);
                Check(StructuralFrameProfile.ResolveModules(fitted) == modules, "Exact set/count independence");
            }
        }
        Check(StructuralFrameProfile.ResolveModules(null) == StructuralFrameModules.None, "Null fitting");
        for (int version = 5; version <= 8; version++)
        foreach (int oldFrame in new[] { -1, 0, 1, 2, 99 })
        foreach (bool analyzed in new[] { false, true })
        {
            var data = new SaveData {
                version = version, selectedOperatingFrame = oldFrame,
                scrapParts = 73, coreShards = 5, stabilizedAlloy = 9,
                highestUnlockedDepth = analyzed ? ExpeditionDepth.DeepZone1 : ExpeditionDepth.Normal
            };
            data.acquiredBossStoryParts.Add(BossStoryPart.SectorStabilizer);
            data.manufacturedEquipmentIds.Add("shared_salvage_protocol");
            data.equipmentLoadoutTraitIds.Add("shared_salvage_protocol");
            data.RetireLegacyOperatingFrame();
            bool grant = version == 7 && analyzed && oldFrame >= 0 && oldFrame <= 2;
            Check(data.manufacturedEquipmentIds.Count == (grant ? 2 : 1), "Research/version/selection boundary");
            if (grant)
            {
                string id = frames[oldFrame == 1 ? 0 : oldFrame == 2 ? 2 : 1];
                Check(data.manufacturedEquipmentIds.Contains(id) && data.equipmentLoadoutTraitIds.Contains(id), "Corresponding module only");
                data.equipmentLoadoutTraitIds.Remove(id);
            }
            string ownership = string.Join("|", data.manufacturedEquipmentIds);
            string fitting = string.Join("|", data.equipmentLoadoutTraitIds);
            for (int i = 0; i < 3; i++) data.RetireLegacyOperatingFrame();
            Check(string.Join("|", data.manufacturedEquipmentIds) == ownership, "Exact-once ownership");
            Check(string.Join("|", data.equipmentLoadoutTraitIds) == fitting, "No re-fit after retirement");
            Check(data.selectedOperatingFrame == -1, "Retired tombstone");
            Check(data.scrapParts == 73 && data.coreShards == 5 && data.stabilizedAlloy == 9, "Currency preserved");
            Check(data.acquiredBossStoryParts.SequenceEqual(new[] { BossStoryPart.SectorStabilizer }), "Campaign preserved");
        }
        var missingPart = new SaveData { version = 7, selectedOperatingFrame = 0, highestUnlockedDepth = ExpeditionDepth.DeepZone1 };
        missingPart.RetireLegacyOperatingFrame();
        Check(missingPart.manufacturedEquipmentIds.Count == 0, "Depth alone is not completed analysis");
        Console.WriteLine("STRUCTURAL_DATA_CHECKS: " + assertions + " assertions passed.");
        return 0;
    }
}
