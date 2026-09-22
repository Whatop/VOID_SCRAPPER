using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Explicit asset-only authoring. Does not open or rebuild a scene or expose a retired installer menu.
public static class EquipmentFinalRosterAuthoring
{
    private const string CatalogPath = "Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset";
    public static readonly string[][] Roster =
    {
        new[] { "shared_cargo_bay", "shared_salvage_magnet", "shared_engine_tuning", "shared_salvage_protocol", "shared_reinforced_plating", "shared_repair_foam", "shared_radar_amplifier", "shared_dash_capacitor", "shared_cutting_ammo", "shared_return_container", "shared_active_cooler", "shared_periodic_reflector" },
        new[] { "mg_traverse_servo", "mg_guidance_control", "mg_stable_feed", "mg_sustained_harvest_fire", "mg_salvage_sweep", "mg_midrange_pressure", "mg_heat_exchanger", "mg_line_penetrator", "mg_target_distributor", "mg_terminal_guidance", "mg_dash_missile_salvo", "mg_twin_feed" },
        new[] { "sg_choke_barrel", "sg_extra_pellet", "sg_breaching_drive", "sg_close_harvest_burst", "sg_taunt_resonator", "sg_cycle_actuator", "sg_pellet_penetrator", "sg_impact_ejector", "sg_breach_compensator", "sg_close_quarters_overpressure", "sg_slug_coupler", "sg_breach_sequencer" },
        new[] { "sn_charge_accelerator", "sn_piercing_amplifier", "sn_ballistic_alignment", "sn_focus_lens", "sn_high_output_core", "sn_mobile_charge_coupler", "sn_stealth_scan", "sn_charge_aperture", "sn_anchor_optics", "sn_semi_auto_laser", "sn_dash_echo_shot", "sn_reserve_capacitor" },
    };
    private static readonly Dictionary<string, int> PreviousTiers = new Dictionary<string, int>
    {
        { "shared_cargo_bay", 0 },
        { "shared_salvage_protocol", 0 },
        { "shared_salvage_magnet", 0 },
        { "shared_reinforced_plating", 1 },
        { "shared_engine_tuning", 1 },
        { "shared_dash_capacitor", 0 },
        { "shared_vector_thruster", 0 },
        { "shared_repair_foam", 0 },
        { "shared_targeting_bus", 2 },
        { "shared_accelerator_coil", 0 },
        { "shared_range_focusing", 0 },
        { "shared_rapid_feed", 1 },
        { "shared_combat_gyro", 2 },
        { "shared_scrap_sorter", 0 },
        { "shared_return_container", 3 },
        { "shared_radar_amplifier", 2 },
        { "shared_active_cooler", 3 },
        { "shared_bulkhead_cargo_frame", 0 },
        { "shared_collection_route", 0 },
        { "shared_cutting_ammo", 0 },
        { "shared_longshot_stabilizer", 0 },
        { "shared_survival_protocol", 0 },
        { "shared_lightweight_cargo", 0 },
        { "shared_return_protocol", 0 },
        { "shared_standard_upgrade", 0 },
        { "shared_periodic_reflector", 3 },
        { "mg_sustained_harvest_fire", 0 },
        { "mg_guidance_control", 0 },
        { "mg_stable_feed", 0 },
        { "mg_salvage_sweep", 1 },
        { "mg_midrange_pressure", 1 },
        { "mg_terminal_guidance", 3 },
        { "mg_dash_missile_salvo", 2 },
        { "sg_choke_barrel", 0 },
        { "sg_extra_pellet", 0 },
        { "sg_breaching_drive", 0 },
        { "sg_close_harvest_burst", 1 },
        { "sg_taunt_resonator", 1 },
        { "sg_close_quarters_overpressure", 2 },
        { "sn_charge_accelerator", 0 },
        { "sn_piercing_amplifier", 0 },
        { "sn_focus_lens", 1 },
        { "sn_high_output_core", 1 },
        { "sn_stealth_scan", 0 },
        { "sn_semi_auto_laser", 1 },
        { "sn_dash_echo_shot", 2 },
    };
    public static void Run()
    {
        Apply();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("FINAL_EQUIPMENT_AUTHORED: 48 development positions, 14 preserved legacy modules, 16 new definitions.");
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring equipment assets.");
        var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Missing " + CatalogPath);
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/MachineGun/51_mg_traverse_servo.asset", "mg_traverse_servo", "횡행 서보", "이동과 탄도 응답을 보조해 사격 위치를 바꾸기 쉽게 합니다.", WeaponTreeType.MachineGun, TraitRarity.Common, new[] { new Effect(1, TraitEffectType.MoveSpeedPercent, 3f), new Effect(2, TraitEffectType.ProjectileSpeedPercent, 8f), new Effect(3, TraitEffectType.MoveSpeedPercent, 4f), new Effect(3, TraitEffectType.ProjectileSpeedPercent, 10f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/MachineGun/52_mg_heat_exchanger.asset", "mg_heat_exchanger", "열교환 급탄기", "방아쇠를 놓은 틈에 열을 빠르게 배출해 다음 교전에 대비합니다.", WeaponTreeType.MachineGun, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.MachineGunCoolingRatePercent, 20f), new Effect(2, TraitEffectType.MachineGunCoolingDelayReduction, 0.05f), new Effect(3, TraitEffectType.MachineGunCoolingRatePercent, 30f), new Effect(3, TraitEffectType.MachineGunCoolingDelayReduction, 0.05f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/MachineGun/53_mg_line_penetrator.asset", "mg_line_penetrator", "관통 급탄기", "일렬로 겹친 적을 관통하며 사격선을 유지합니다.", WeaponTreeType.MachineGun, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.PierceCountBonus, 1f), new Effect(2, TraitEffectType.ProjectileSpeedPercent, 10f), new Effect(3, TraitEffectType.ProjectileSpeedPercent, 12f), new Effect(3, TraitEffectType.RangePercent, 5f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/MachineGun/54_mg_target_distributor.asset", "mg_target_distributor", "표적 분배기", "조준선 근처의 적으로 사격을 분산합니다. 최근 표적은 잠시 제외합니다.", WeaponTreeType.MachineGun, TraitRarity.Special, new[] { new Effect(1, TraitEffectType.MachineGunTargetDistribution, 1f), new Effect(2, TraitEffectType.MachineGunTargetDistribution, 1f), new Effect(3, TraitEffectType.MachineGunTargetDistribution, 1f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/MachineGun/55_mg_twin_feed.asset", "mg_twin_feed", "병렬 급탄기", "연속 사격 중 급탄을 안정시킵니다. 최대 단계에서는 주기적으로 빠른 후속탄을 발사합니다.", WeaponTreeType.MachineGun, TraitRarity.Special, new[] { new Effect(1, TraitEffectType.MachineGunTwinFeed, 1f), new Effect(2, TraitEffectType.MachineGunTwinFeed, 1f), new Effect(3, TraitEffectType.MachineGunTwinFeed, 1f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/56_sg_cycle_actuator.asset", "sg_cycle_actuator", "순환 구동기", "다음 산탄 사격을 빠르게 준비해 짧은 교전 틈을 줄입니다.", WeaponTreeType.Shotgun, TraitRarity.Common, new[] { new Effect(1, TraitEffectType.FireRatePercent, 5f), new Effect(2, TraitEffectType.FireRatePercent, 7f), new Effect(3, TraitEffectType.FireRatePercent, 10f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/57_sg_pellet_penetrator.asset", "sg_pellet_penetrator", "관통 산탄", "산탄이 전열을 관통해 뒤의 적까지 압박합니다.", WeaponTreeType.Shotgun, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.PierceCountBonus, 1f), new Effect(2, TraitEffectType.ProjectileSpeedPercent, 8f), new Effect(3, TraitEffectType.ProjectileSpeedPercent, 12f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/58_sg_impact_ejector.asset", "sg_impact_ejector", "충격 배출기", "중앙 탄의 첫 유효 명중으로 밀어낼 수 있는 적을 짧게 밀어냅니다.", WeaponTreeType.Shotgun, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.ShotgunImpactDisplacement, 0.35f), new Effect(2, TraitEffectType.ShotgunImpactDisplacement, 0.2f), new Effect(3, TraitEffectType.ShotgunImpactDisplacement, 0.25f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/59_sg_breach_compensator.asset", "sg_breach_compensator", "돌입 보정기", "산탄총 대쉬 직후 잠시 피격을 완화해 돌입을 보조합니다.", WeaponTreeType.Shotgun, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.DashDamageReductionPercent, 10f), new Effect(2, TraitEffectType.DashDamageReductionPercent, 5f), new Effect(3, TraitEffectType.DashDamageReductionPercent, 5f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/60_sg_slug_coupler.asset", "sg_slug_coupler", "단일탄 결합기", "산탄 두 발을 중앙 단일탄으로 결합합니다. 남은 산탄은 그대로 발사합니다.", WeaponTreeType.Shotgun, TraitRarity.Special, new[] { new Effect(1, TraitEffectType.ShotgunSlugCoupler, 1f), new Effect(2, TraitEffectType.ShotgunSlugCoupler, 1f), new Effect(3, TraitEffectType.ShotgunSlugCoupler, 1f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Shotgun/61_sg_breach_sequencer.asset", "sg_breach_sequencer", "돌입 시퀀서", "대쉬 후 첫 사격의 회복을 줄여 다음 사격을 빠르게 이어갑니다.", WeaponTreeType.Shotgun, TraitRarity.Special, new[] { new Effect(1, TraitEffectType.ShotgunBreachSequence, 1f), new Effect(2, TraitEffectType.ShotgunBreachSequence, 1f), new Effect(3, TraitEffectType.ShotgunBreachSequence, 1f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Sniper/62_sn_ballistic_alignment.asset", "sn_ballistic_alignment", "탄도 정렬기", "탄속과 사거리를 보완해 먼 사격선을 다루기 쉽게 합니다.", WeaponTreeType.Sniper, TraitRarity.Common, new[] { new Effect(1, TraitEffectType.ProjectileSpeedPercent, 8f), new Effect(2, TraitEffectType.RangePercent, 8f), new Effect(3, TraitEffectType.ProjectileSpeedPercent, 10f), new Effect(3, TraitEffectType.RangePercent, 10f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Sniper/63_sn_mobile_charge_coupler.asset", "sn_mobile_charge_coupler", "기동 차징기", "이동 중 차징 손실을 줄여 재배치하면서 사격을 준비합니다.", WeaponTreeType.Sniper, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.SniperMovingChargeBonus, 10f), new Effect(2, TraitEffectType.SniperMovingChargeBonus, 10f), new Effect(3, TraitEffectType.SniperMovingChargeBonus, 20f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Sniper/64_sn_charge_aperture.asset", "sn_charge_aperture", "차징 확장기", "차징할수록 탄의 폭을 넓혀 정밀 사격의 명중 여유를 확보합니다.", WeaponTreeType.Sniper, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.ChargedProjectileSizePercent, 15f), new Effect(2, TraitEffectType.ChargedProjectileSizePercent, 10f), new Effect(3, TraitEffectType.ChargedProjectileSizePercent, 15f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Sniper/65_sn_anchor_optics.asset", "sn_anchor_optics", "정위 조준경", "차징 중 조준 방향의 카메라 시야를 확장합니다. 탐지 범위는 바뀌지 않습니다.", WeaponTreeType.Sniper, TraitRarity.Rare, new[] { new Effect(1, TraitEffectType.ChargeSightBonusPercent, 10f), new Effect(2, TraitEffectType.ChargeSightBonusPercent, 10f), new Effect(3, TraitEffectType.ChargeSightBonusPercent, 15f) });
        Ensure(catalog, "Assets/02_Scripts/Config/TraitDefinition/Sniper/66_sn_reserve_capacitor.asset", "sn_reserve_capacitor", "잔류 축전기", "완충 사격 후 잠시 다음 차징의 일부를 보존합니다. 보조받은 사격은 예비량을 다시 만들지 않습니다.", WeaponTreeType.Sniper, TraitRarity.Special, new[] { new Effect(1, TraitEffectType.SniperReserveCapacitor, 10f), new Effect(2, TraitEffectType.SniperReserveCapacitor, 5f), new Effect(3, TraitEffectType.SniperReserveCapacitor, 10f) });
        ApplyMetadata(catalog);
        AssetDatabase.SaveAssets();
        var positions = new HashSet<string>();
        int ordinary = 0, legacy = 0;
        foreach (TraitDefinition trait in catalog.TraitDefinitions)
        {
            if (trait == null) throw new InvalidOperationException("Null catalog reference");
            if (!trait.CanAppearAsRandomDropTrait) continue;
            ordinary++;
            if (!trait.IsDevelopmentRoster) { legacy++; continue; }
            string key = trait.DevelopmentBranch + ":" + trait.DevelopmentResearchTier + ":" + trait.DevelopmentDisplayOrder;
            if (!positions.Add(key) || !trait.HasValidManufacturingRecipe) throw new InvalidOperationException("Invalid final blueprint: " + trait.TraitId);
        }
        if (positions.Count != 48 || ordinary != 62 || legacy != 14) throw new InvalidOperationException("Final equipment roster count mismatch.");
    }

    public static void ApplyMetadata(TraitCatalog catalog)
    {
        foreach (TraitDefinition trait in catalog.TraitDefinitions)
        {
            if (trait == null || !trait.CanAppearAsRandomDropTrait) continue;
            int branch = trait.Category == TraitCategory.Shared ? 0 : trait.WeaponTreeType == WeaponTreeType.MachineGun ? 1 : trait.WeaponTreeType == WeaponTreeType.Shotgun ? 2 : 3;
            int position = Array.IndexOf(Roster[branch], trait.TraitId);
            var data = new SerializedObject(trait);
            data.FindProperty("developmentRoster").boolValue = position >= 0;
            data.FindProperty("developmentResearchTier").intValue = position >= 0 ? position / 3 : 0;
            data.FindProperty("developmentDisplayOrder").intValue = position >= 0 ? position % 3 : 0;
            data.FindProperty("previousDevelopmentResearchTier").intValue = PreviousTiers.TryGetValue(trait.TraitId, out int previous) ? previous : -1;
            // Preserve every valid existing recipe, including original unlock prices.
            if (position >= 0 && !trait.HasValidManufacturingRecipe)
            {
                int rarity = (int)trait.Rarity;
                data.FindProperty("manufacturingScrapCost").intValue = 10 + 10 * (position / 3) + 10 * rarity;
                data.FindProperty("manufacturingCoreCost").intValue = position / 3 + rarity;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private readonly struct Effect
    {
        public readonly int Level; public readonly TraitEffectType Type; public readonly float Value;
        public Effect(int level, TraitEffectType type, float value) { Level = level; Type = type; Value = value; }
    }

    private static void Ensure(TraitCatalog catalog, string path, string id, string name, string description, WeaponTreeType weapon, TraitRarity rarity, Effect[] effects)
    {
        TraitDefinition trait = catalog.FindById(id);
        if (trait != null) return; // Existing local content is authoritative; never overwrite a definition.
        trait = AssetDatabase.LoadAssetAtPath<TraitDefinition>(path);
        if (trait == null)
        {
            trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var data = new SerializedObject(trait);
            data.FindProperty("traitId").stringValue = id;
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("description").stringValue = description;
            data.FindProperty("category").intValue = (int)TraitCategory.WeaponSpecific;
            data.FindProperty("weaponTreeType").intValue = (int)weapon;
            data.FindProperty("rarity").intValue = (int)rarity;
            data.FindProperty("shopItemType").intValue = (int)TraitShopItemType.Trait;
            data.FindProperty("maxLevel").intValue = 3;
            var array = data.FindProperty("levelEffects"); array.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                var item = array.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("level").intValue = effects[i].Level;
                item.FindPropertyRelative("effectType").intValue = (int)effects[i].Type;
                item.FindPropertyRelative("value").floatValue = effects[i].Value;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(trait, path); // Unity owns the new GUID/meta.
        }
        if (trait.TraitId != id) throw new InvalidOperationException("Conflicting local asset at " + path);
        var catalogData = new SerializedObject(catalog);
        var definitions = catalogData.FindProperty("traitDefinitions");
        definitions.InsertArrayElementAtIndex(definitions.arraySize);
        definitions.GetArrayElementAtIndex(definitions.arraySize - 1).objectReferenceValue = trait;
        catalogData.ApplyModifiedPropertiesWithoutUndo();
    }
}
