using System.Globalization;
using System.Text;

// Read-only localized projection shared by Settlement, build inspection and development QA.
public static class OperatingFrameText
{
    public static string Get(string suffix, LocalizationCatalog catalog = null)
    {
        string key = "ui.operating_frame." + suffix;
        if (VoidScrapperLocalizationService.HasInstance) return VoidScrapperLocalizationService.Instance.GetText(key);
        return catalog != null && catalog.TryGetText(key, GameSettingsRuntime.LanguageCode, out string text, out _) ? text : string.Empty;
    }

    public static string Name(OperatingFrameType type, LocalizationCatalog catalog = null) => Get(Key(type), catalog);
    public static string Identity(OperatingFrameType type, LocalizationCatalog catalog = null) => Get(Key(type) + ".description", catalog);
    private static string Key(OperatingFrameType type) => type == OperatingFrameType.Lightweight ? "lightweight" :
        type == OperatingFrameType.Heavy ? "heavy" : "standard";

    public static string Count(OperatingFrameProfile profile, LocalizationCatalog catalog = null) =>
        Get("count", catalog).Replace("{count}", profile.EquipmentCount.ToString());

    public static string Tier(OperatingFrameProfile profile, LocalizationCatalog catalog = null) => profile.Type == OperatingFrameType.Standard
        ? string.Empty : Get(Key(profile.Type) + ".tier" + profile.Tier, catalog);

    public static string Summary(OperatingFrameProfile profile, LocalizationCatalog catalog = null) =>
        Get("summary", catalog).Replace("{frame}", Name(profile.Type, catalog)).Replace("{count}", profile.EquipmentCount.ToString());

    public static string Effects(OperatingFrameProfile profile, bool penalties, LocalizationCatalog catalog = null)
    {
        var text = new StringBuilder();
        Append(text, "move", (profile.MoveMultiplier - 1f) * 100f, penalties, false, catalog);
        Append(text, "dash_cooldown", (profile.DashCooldownMultiplier - 1f) * 100f, penalties, true, catalog);
        Append(text, "dash_distance", profile.DashDistanceBonus, penalties, false, catalog);
        Append(text, "hp", profile.MaxHpBonus, penalties, false, catalog);
        Append(text, "cargo", profile.CargoBonus, penalties, false, catalog);
        Append(text, "damage", profile.DamagePercent, penalties, false, catalog);
        Append(text, "harvest", profile.HarvestYieldPercent, penalties, false, catalog);
        return text.ToString();
    }

    public static string Details(OperatingFrameProfile profile, LocalizationCatalog catalog = null)
    {
        string tier = Tier(profile, catalog);
        string result = Summary(profile, catalog) + (tier.Length > 0 ? "\n" + tier : string.Empty);
        string bonuses = Effects(profile, false, catalog), penalties = Effects(profile, true, catalog);
        if (bonuses.Length > 0) result += "\n\n" + bonuses;
        if (penalties.Length > 0) result += "\n\n" + penalties;
        return result;
    }

    private static void Append(StringBuilder text, string stat, float value, bool penalties, bool lowerIsBetter, LocalizationCatalog catalog)
    {
        if (System.Math.Abs(value) < .001f || ((value < 0f) != lowerIsBetter) != penalties) return;
        if (text.Length > 0) text.Append('\n');
        string number = value.ToString(stat == "dash_distance" ? "+0.00;-0.00" : "+0.#;-0.#", CultureInfo.InvariantCulture);
        text.Append(Get(stat, catalog).Replace("{value}", number));
    }
}
