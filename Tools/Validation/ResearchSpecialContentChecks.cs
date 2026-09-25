using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

// Production TraitDefinition and localization parser/validator/lookup, with engine-only adapters.
// Does not write the generated localization asset or simulate Unity lifecycle tests.
public static class ResearchSpecialContentChecks
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static string Field(string yaml, string key) => Regex.Match(yaml, @"(?m)^  " + key + @": (.*)").Groups[1].Value.Trim().Trim('"');
    public static int Main()
    {
        const string source = "Assets/02_Scripts/Config/Localization/Source/Localization.csv";
        var report = LocalizationContentValidator.ValidateCsv(source, File.ReadAllText(source));
        foreach (var issue in report.Issues.Where(i => i.Severity == LocalizationValidationSeverity.Error)) Console.WriteLine(issue);
        Check(!report.HasErrors, "Production CSV validation");
        var catalog = new LocalizationCatalog();
        catalog.SetGeneratedContentForEditor(report.Records.Select(r => r.ToCatalogEntry()).ToArray(), source, report.ContentHash);
        string[] ids = { "special_sector_stabilization", "special_matter_compression", "special_phase_navigation" };
        for (int i = 0; i < ids.Length; i++)
        {
            string yaml = File.ReadAllText("Assets/02_Scripts/Config/TraitDefinition/Common/" + (70+i) + "_" + ids[i] + ".asset");
            var trait = new TraitDefinition();
            Set(trait, "traitId", Field(yaml, "traitId"));
            Set(trait, "displayName", Field(yaml, "displayName"));
            Set(trait, "description", Field(yaml, "description"));
            Set(trait, "displayNameKey", Field(yaml, "displayNameKey"));
            Set(trait, "descriptionKey", Field(yaml, "descriptionKey"));
            Set(trait, "localizationCatalog", catalog);
            Set(trait, "requiredStoryPartAnalysis", (BossStoryPart)int.Parse(Field(yaml, "requiredStoryPartAnalysis")));
            Set(trait, "rarity", (TraitRarity)int.Parse(Field(yaml, "rarity")));
            Set(trait, "developmentResearchTier", int.Parse(Field(yaml, "developmentResearchTier")));
            Set(trait, "manufacturingScrapCost", int.Parse(Field(yaml, "manufacturingScrapCost")));
            Set(trait, "manufacturingCoreCost", int.Parse(Field(yaml, "manufacturingCoreCost")));
            Check(trait.IsResearchSpecialEquipment && trait.IsManufacturableBlueprint, ids[i] + " blueprint");
            Check(trait.HasValidDevelopmentMetadata && trait.HasValidManufacturingRecipe, ids[i] + " metadata");
            Check(!trait.IsPersistentStoryTrait && !trait.IsDevelopmentRoster, ids[i] + " independent equipment");
            Check(trait.CanFieldDrop && trait.CanDismantle, ids[i] + " ordinary actions");
            Check(trait.CanAppearAsLevelUpTrait && trait.CanAppearAsRandomDropTrait && trait.CanAppearAsShopTrait, ids[i] + " candidate exposure");
            Check(trait.MaxLevel == 3 && trait.RequiredStoryPartAnalysis == (BossStoryPart)(i+1), ids[i] + " gate and level");
            foreach (WeaponTreeType ship in Enum.GetValues(typeof(WeaponTreeType))) Check(trait.IsAvailableFor(ship), ids[i] + " shared");
            foreach (string language in new[] { "ko", "en" })
            {
                GameSettingsRuntime.LanguageCode = language;
                Check(catalog.TryGetText(Field(yaml, "displayNameKey"), language, out string name, out bool fallback) && !fallback, ids[i] + " translated name");
                Check(trait.DisplayName == name, ids[i] + " localized name lookup");
                Check(catalog.TryGetText(Field(yaml, "descriptionKey"), language, out string description, out fallback) && !fallback, ids[i] + " translated description");
                Check(trait.Description == description, ids[i] + " localized description lookup");
                Check(trait.GetCategoryText().Contains(trait.GetRarityText()), ids[i] + " Special category");
            }
        }
        Console.WriteLine("PASS: " + checks + " production Special metadata/localization assertions; " + report.Records.Count + " CSV records validated. Generated asset unchanged.");
        return 0;
    }
}
public static class GameSettingsRuntime { public static string LanguageCode = "ko"; }
public sealed class VoidScrapperLocalizationService { public static bool HasInstance => false; public static VoidScrapperLocalizationService Instance => null; public LocalizationCatalog Catalog => null; }
namespace UnityEngine
{
    public class ScriptableObject { public string name; }
    public class Sprite { }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class TextAreaAttribute : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(int value) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    public struct Color { public Color(float r, float g, float b, float a = 1) { } public static Color white => new Color(1,1,1); }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a,b); }
    public static class Debug { public static void LogError(string text, object context) => throw new Exception(text); }
}
