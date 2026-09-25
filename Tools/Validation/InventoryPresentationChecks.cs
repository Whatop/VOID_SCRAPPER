using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Exercises the production formatters/resolver without loading the Unity engine.
// Thin DTO/math/localization adapters below replace Unity-only dependencies, not gameplay.
public static class InventoryPresentationChecks
{
    private static int count;
    private static void Check(bool result, string message) { count++; if (!result) throw new Exception(message); }
    private static string Plain(string rich) => Regex.Replace(rich, "<color=#[0-9A-Fa-f]{6}>|</color>", "");
    public static int Main()
    {
        string[] palette = { "#FF6B6B", "#55D6BE", "#FF9F43", "#FFD166", "#9BE564", "#46E6C8", "#D7A75E", "#6FD08C", "#6CCBFF", "#5B8CFF", "#7EE7F2", "#B58CFF", "#D77BFF", "#FF82C8", "#44DDE7", "#7AA8FF", "#70E08F", "#FFD76A" };
        foreach (StatCategory category in Enum.GetValues(typeof(StatCategory)))
        {
            Check(StatPresentation.Hex(category) == palette[(int)category], "Palette " + category);
            Check(StatPresentation.Rich(category, "Stat -3").Contains(palette[(int)category] + ">Stat -3"), "Penalty category " + category);
        }
        foreach (TraitEffectType effect in Enum.GetValues(typeof(TraitEffectType)))
        {
            Check(StatPresentation.Hex(StatPresentation.Category(effect)).Length == 7, "Trait mapping " + effect);
            foreach (float value in new[] { 1f, 3f, 25f })
            {
                string plain = TraitEffectTextUtility.FormatEffect(effect, value);
                Check(!plain.Contains("효과 정보 없음"), "Trait format " + effect);
                Check(Plain(StatPresentation.Trait(effect, value)) == plain, "Trait rich parity " + effect);
                var trait = new TraitDefinition { LevelEffects = new List<TraitLevelEffect> {
                    new TraitLevelEffect { Level = 1, EffectType = effect, Value = value },
                    new TraitLevelEffect { Level = 2, EffectType = effect, Value = value }
                }};
                for (int level = 1; level <= 2; level++)
                    Check(Plain(TraitEffectTextUtility.BuildRichEffectText(trait, level)) == TraitEffectTextUtility.BuildEffectText(trait, level), "Level parity " + effect);
            }
        }
        foreach (ReinforcementEffectType type in Enum.GetValues(typeof(ReinforcementEffectType)))
        {
            var effect = new ReinforcementEffect { EffectType = type, Value = 10, Radius = 4, Duration = 3, ResourceCost = 2, ConeAngle = 90 };
            Check(StatPresentation.Hex(StatPresentation.Category(type)).Length == 7, "Active mapping " + type);
            Check(Plain(ReinforcementEffectTextUtility.FormatRich(effect)) == ReinforcementEffectTextUtility.Format(effect), "Active rich parity " + type);
        }
        var catalog = new LocalizationCatalog();
        for (int bits = 0; bits < 8; bits++)
        {
            var profile = new StructuralFrameProfile((StructuralFrameModules)bits);
            string rich = StructuralFrameText.RichModifiers(profile, catalog);
            Check(Plain(rich) == StructuralFrameText.Modifiers(profile, catalog), "Frame parity " + bits);
            Check(Plain(StructuralFrameText.RichDetails(profile, catalog)) == StructuralFrameText.Details(profile, catalog), "Frame details " + bits);
            if (bits != 0) Check(StructuralFrameText.RichDetails(profile, catalog).Contains("<color=#"), "Frame details colors " + bits);
            if (profile.MaxHpBonus != 0) Check(rich.Contains("<color=#FF6B6B>HP "), "Frame HP " + bits);
            if (profile.CargoBonus != 0) Check(rich.Contains("<color=#D7A75E>Cargo "), "Frame cargo " + bits);
            if (profile.MoveMultiplier != 1) Check(rich.Contains("<color=#9BE564>Movement "), "Frame movement " + bits);
            if (bits == 1) Check(rich.Contains("HP -3") && rich.Contains("Cargo -20"), "Frame explicit penalties");
        }
        Check(StatPresentation.Trait(TraitEffectType.SniperSemiAutoMode, 1).Contains("세미오토 레이저"), "Mechanic identity");
        Console.WriteLine("PASS: " + count + " production presentation assertions; " + Enum.GetValues(typeof(TraitEffectType)).Length + " Trait and " + Enum.GetValues(typeof(ReinforcementEffectType)).Length + " Reinforcement effects.");
        return 0;
    }
}

public sealed class TraitDefinition { public List<TraitLevelEffect> LevelEffects; }
public sealed class TraitLevelEffect { public int Level; public TraitEffectType EffectType; public float Value; }
public sealed class ReinforcementEffect { public ReinforcementEffectType EffectType; public float Value, Radius, Duration, ConeAngle; public int ResourceCost; }
namespace UnityEngine { public static class Mathf { public static float Abs(float x) => Math.Abs(x); public static int RoundToInt(float x) => (int)Math.Round(x); } }
public static class GameSettingsRuntime { public static string LanguageCode => "en"; }
public sealed class VoidScrapperLocalizationService { public static bool HasInstance => false; public static VoidScrapperLocalizationService Instance => null; public string GetText(string key) => key; }
public sealed class PermanentProgress { public int LastSelectedWeaponTree; public void AppendEffectiveEquipment(List<string> ids, int ship) {} public bool IsEquipmentManufactured(string id) => false; public bool IsEquipmentFitted(string id) => false; }
public sealed class LocalizationCatalog
{
    public bool TryGetText(string key, string language, out string text, out bool fallback)
    {
        fallback = false;
        string suffix = key.Substring("ui.structural_frame.".Length);
        text = suffix switch {
            "move" => "Movement {value}%", "dash_cooldown" => "Dash cooldown {value}%", "dash_distance" => "Dash distance {value}",
            "hp" => "HP {value}", "cargo" => "Cargo {value}", "damage" => "Damage {value}%", "harvest" => "Harvest {value}%",
            "summary" => "Structural frame: {frame}", _ => suffix
        };
        return true;
    }
}
