using System.Globalization;
using System.Text;

// Read-only localized projection shared by Settlement, build inspection and development QA.
public static class StructuralFrameText
{
    public static string Get(string suffix, LocalizationCatalog catalog = null)
    {
        string key = "ui.structural_frame." + suffix;
        if (VoidScrapperLocalizationService.HasInstance) return VoidScrapperLocalizationService.Instance.GetText(key);
        return catalog != null && catalog.TryGetText(key, GameSettingsRuntime.LanguageCode, out string text, out _) ? text : string.Empty;
    }

    public static string Name(StructuralFrameModules modules, LocalizationCatalog catalog = null) =>
        Get("profile" + (int)modules, catalog);

    public static string Summary(StructuralFrameProfile profile, LocalizationCatalog catalog = null) =>
        Get("summary", catalog).Replace("{frame}", Name(profile.Modules, catalog));

    public static string Modifiers(StructuralFrameProfile profile, LocalizationCatalog catalog = null) => Modifiers(profile, catalog, false);

    public static string RichModifiers(StructuralFrameProfile profile, LocalizationCatalog catalog = null) => Modifiers(profile, catalog, true);

    private static string Modifiers(StructuralFrameProfile profile, LocalizationCatalog catalog, bool rich)
    {
        string bonuses = Effects(profile, false, catalog, rich), penalties = Effects(profile, true, catalog, rich);
        return bonuses + (bonuses.Length > 0 && penalties.Length > 0 ? "\n" : "") + penalties;
    }

    public static string DevelopmentStatus(PermanentProgress progress, LocalizationCatalog catalog = null)
    {
        var ids = new System.Collections.Generic.List<string>();
        progress.AppendEffectiveEquipment(ids, progress.LastSelectedWeaponTree);
        var text = new StringBuilder(Summary(new StructuralFrameProfile(StructuralFrameProfile.ResolveModules(ids)), catalog));
        foreach (string id in new[] { StructuralFrameProfile.LightweightId, StructuralFrameProfile.StandardId, StructuralFrameProfile.HeavyId })
            text.Append('\n').Append(Name(StructuralFrameProfile.ModuleFor(id), catalog)).Append(": ")
                .Append(progress.IsEquipmentManufactured(id) ? Get("owned", catalog) : Get("unowned", catalog))
                .Append(progress.IsEquipmentFitted(id) ? " · " + Get("fitted", catalog) : "");
        return text.ToString();
    }

    public static string Effects(StructuralFrameProfile profile, bool penalties, LocalizationCatalog catalog = null) => Effects(profile, penalties, catalog, false);

    private static string Effects(StructuralFrameProfile profile, bool penalties, LocalizationCatalog catalog, bool rich)
    {
        var text = new StringBuilder();
        Append(text, "move", (profile.MoveMultiplier - 1f) * 100f, penalties, false, catalog, rich, StatCategory.Movement);
        Append(text, "dash_cooldown", (profile.DashCooldownMultiplier - 1f) * 100f, penalties, true, catalog, rich, StatCategory.Dash);
        Append(text, "dash_distance", profile.DashDistanceBonus, penalties, false, catalog, rich, StatCategory.Dash);
        Append(text, "hp", profile.MaxHpBonus, penalties, false, catalog, rich, StatCategory.Health);
        Append(text, "cargo", profile.CargoBonus, penalties, false, catalog, rich, StatCategory.Cargo);
        Append(text, "damage", profile.DamagePercent, penalties, false, catalog, rich, StatCategory.Damage);
        Append(text, "harvest", profile.HarvestYieldPercent, penalties, false, catalog, rich, StatCategory.Harvest);
        return text.ToString();
    }

    public static string Details(StructuralFrameProfile profile, LocalizationCatalog catalog = null) => Details(profile, catalog, false);

    public static string RichDetails(StructuralFrameProfile profile, LocalizationCatalog catalog = null) => Details(profile, catalog, true);

    private static string Details(StructuralFrameProfile profile, LocalizationCatalog catalog, bool rich)
    {
        string result = Summary(profile, catalog);
        string bonuses = Effects(profile, false, catalog, rich), penalties = Effects(profile, true, catalog, rich);
        if (bonuses.Length > 0) result += "\n\n" + bonuses;
        if (penalties.Length > 0) result += "\n\n" + penalties;
        return result;
    }

    private static void Append(StringBuilder text, string stat, float value, bool penalties, bool lowerIsBetter, LocalizationCatalog catalog, bool rich, StatCategory category)
    {
        if (System.Math.Abs(value) < .001f || ((value < 0f) != lowerIsBetter) != penalties) return;
        if (text.Length > 0) text.Append('\n');
        string number = value.ToString(stat == "dash_distance" ? "+0.00;-0.00" : "+0.#;-0.#", CultureInfo.InvariantCulture);
        string plain = Get(stat, catalog).Replace("{value}", number);
        text.Append(rich ? StatPresentation.Rich(category, plain) : plain);
    }
}
