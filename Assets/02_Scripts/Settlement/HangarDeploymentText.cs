using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using TMPro;

// Presentation only. Category colors remain owned by StatPresentation.
public static class HangarDeploymentText
{
    public static string Text(string suffix, string fallback)
    {
        if (!VoidScrapperLocalizationService.HasInstance) return fallback;
        string key = "ui.hangar." + suffix;
        string text = VoidScrapperLocalizationService.Instance.GetText(key);
        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }
    public static string ShipName(ShipDefinition ship) => Text("ship." + ship.ShipId + ".name", ship.DisplayName);
    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Signed(float value) => value.ToString("+0.##;-0.##", CultureInfo.InvariantCulture);
    private static string Row(string key, string fallback, StatCategory category, float value, string unit = "%") =>
        StatPresentation.Rich(category, Text(key, fallback) + " " + Signed(value) + unit);
    private static void Add(List<string> rows, string key, string fallback, StatCategory category, float value, string unit = "%")
    {
        if (Mathf.Abs(value) >= .005f) rows.Add(Row(key, fallback, category, value, unit));
    }
    private static void Pairs(StringBuilder text, List<string> rows)
    {
        for (int i = 0; i < rows.Count; i += 2)
        {
            text.Append(rows[i]);
            if (i + 1 < rows.Count) text.Append("<pos=112>").Append(rows[i + 1]);
            text.AppendLine();
        }
    }
    // Measure the actual authored font. A long translated label gets its own row,
    // rather than colliding with a second column or shrinking the whole panel.
    public static string FitColumns(string body, TMP_Text target)
    {
        if (target == null || string.IsNullOrEmpty(body)) return body;
        var result = new StringBuilder();
        foreach (string row in body.Split('\n'))
        {
            string[] cells = row.Split(new[] { "<pos=112>" }, System.StringSplitOptions.None);
            if (cells.Length == 2 && (target.GetPreferredValues(cells[0], Mathf.Infinity, Mathf.Infinity).x > 108 ||
                                     target.GetPreferredValues(cells[1], Mathf.Infinity, Mathf.Infinity).x > 108))
                result.Append(cells[0]).Append('\n').Append(cells[1]);
            else result.Append(row);
            result.Append('\n');
        }
        return result.ToString().TrimEnd();
    }
    public static string Build(ShipDefinition ship, HangarDeploymentProjection p, bool unlocked,
        bool prerequisiteMet, PermanentProgress progress, string cost)
    {
        var text = new StringBuilder();
        string weapon = ship.DefaultWeaponTree switch
        {
            WeaponTreeType.MachineGun => Text("weapon.mg", "기관총"),
            WeaponTreeType.Shotgun => Text("weapon.sg", "샷건"),
            _ => Text("weapon.sn", "저격총")
        };
        text.Append(Text("weapon", "무장")).Append("  ").AppendLine(weapon);
        text.AppendLine(Text("ship." + ship.ShipId + ".description", ship.Description));
        text.AppendLine();
        if (!unlocked)
        {
            text.AppendLine(Text("locked", "개발 잠김"));
            if (ship.RequiredAnalyzedComponents > 0)
                text.AppendLine(Text("analysis", "분석한 부품 {current} / {required}")
                    .Replace("{current}", (progress?.AnalyzedEquipmentComponentCount ?? 0).ToString())
                    .Replace("{required}", ship.RequiredAnalyzedComponents.ToString()));
            if (!string.IsNullOrWhiteSpace(ship.RequiredUnlockFlag) && !prerequisiteMet)
                text.AppendLine(Text("requirement", "필요 조건") + "  " + ship.RequiredUnlockFlag);
            text.AppendLine(Text(prerequisiteMet ? "available" : "unavailable", prerequisiteMet ? "개발 가능" : "조건 미충족"));
            if (ship.RequiredScrapParts > 0 || ship.RequiredCoreShards > 0)
                text.AppendLine(Text("cost", "개발 비용") + "  " + cost);
            return text.ToString().TrimEnd();
        }
        text.AppendLine("<b>" + Text(p != null ? "projection" : "base", p != null ? "출격 예상 성능" : "기본 기체 성능") + "</b>");
        text.Append(StatPresentation.Rich(StatCategory.Health, "HP " + Number(p?.Hp ?? ship.MaxHp)))
            .Append("<pos=70>").Append(StatPresentation.Rich(StatCategory.Cargo, Text("cargo", "적재") + " " + (p?.Cargo ?? ship.CargoCapacity)));
        if (p != null && p.Armor > 0)
            text.Append("<pos=142>").Append(StatPresentation.Rich(StatCategory.Defense, Text("armor", "방어도") + " " + Number(p.Armor)));
        text.AppendLine();
        if (p != null)
        {
            var rows = new List<string>();
            Add(rows, "damage", "공격력", StatCategory.Damage, (p.Damage - 1) * 100);
            Add(rows, "fire_rate", "연사력", StatCategory.FireRate, (p.FireRate - 1) * 100);
            Add(rows, "move", "이동 속도", StatCategory.Movement, p.MovePercent);
            Add(rows, "dash_cooldown", "대시 재사용", StatCategory.Dash, p.DashCooldownPercent);
            Add(rows, "dash_distance", "대시 거리", StatCategory.Dash, p.DashDistanceBonus, "");
            Add(rows, "harvest", "수확량", StatCategory.Harvest, (p.Harvest - 1) * 100);
            Add(rows, "harvest_damage", "수확 오브젝트 피해", StatCategory.Harvest, (p.HarvestDamage - 1) * 100);
            Add(rows, "recovery", "회복 효율", StatCategory.Recovery, (p.Recovery - 1) * 100);
            Add(rows, "scrap", "스크랩 획득량", StatCategory.Harvest, (p.Scrap - 1) * 100);
            Add(rows, "projectile_speed", "투사체 속도", StatCategory.ProjectileSpeed, (p.ProjectileSpeed - 1) * 100);
            Add(rows, "range", "사거리", StatCategory.Range, (p.Range - 1) * 100);
            Add(rows, "spread", "탄 퍼짐", StatCategory.Accuracy, (p.Spread - 1) * 100);
            Add(rows, "charge_time", "차징 시간", StatCategory.Charge, (p.ChargeTime - 1) * 100);
            Add(rows, "charge_damage", "차징 피해", StatCategory.Charge, (p.ChargeDamage - 1) * 100);
            Add(rows, "pickup", "흡수 범위", StatCategory.Harvest, p.Pickup, "");
            Add(rows, "radar", "레이더 반경", StatCategory.Radar, p.RadarRadius, "");
            Add(rows, "taunt", "도발 지속", StatCategory.Radar, p.RadarTaunt, "s");
            Add(rows, "stealth", "은밀 표식", StatCategory.Radar, p.RadarStealth, "s");
            Add(rows, "homing_angle", "유도 각도", StatCategory.Homing, p.HomingAngle, "°");
            Add(rows, "homing_range", "유도 거리", StatCategory.Homing, p.HomingRange, "");
            Add(rows, "projectiles", "탄 수", StatCategory.Special, p.Projectiles, "");
            Add(rows, "pierce", "관통 횟수", StatCategory.Pierce, p.Pierce, "");
            Pairs(text, rows);
        }
        var innate = new List<string>();
        // These chassis modifiers are already included in the effective rows.
        // Keep them for base-only previews, where no projection represents them.
        if (p == null)
        {
            Add(innate, "harvest_damage", "수확 오브젝트 피해", StatCategory.Harvest, ship.HarvestObjectDamageBonusPercent);
            Add(innate, "harvest", "수확량", StatCategory.Harvest, ship.HarvestYieldBonusPercent);
            Add(innate, "move", "이동 속도", StatCategory.Movement, ship.MoveSpeedBonusPercent);
            Add(innate, "dash_distance", "대시 거리", StatCategory.Dash, ship.DashDistanceBonus, "");
            Add(innate, "dash_cooldown", "대시 재사용", StatCategory.Dash, -Mathf.Abs(ship.DashCooldownReduction), "s");
        }
        if (innate.Count > 0)
        {
            text.AppendLine().AppendLine("<b>" + Text("innate", "기체 특성") + "</b>");
            Pairs(text, innate);
        }
        return text.ToString().TrimEnd();
    }
}
