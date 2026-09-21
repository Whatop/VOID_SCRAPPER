using System;
using System.Collections.Generic;

public static class SoundEventIds
{
    public const string UiClick = "01_ui_click";
    public const string UiHover = "02_ui_hover";
    public const string UiBack = "03_ui_back";
    public const string UiPanelOpen = "04_ui_panel_open";
    public const string UiPanelClose = "05_ui_panel_close";
    public const string UiInsufficient = "06_ui_insufficient";
    public const string UiUpgradeSuccess = "07_ui_upgrade_success";
    public const string UiUpgradeFail = "08_ui_upgrade_fail";
    public const string UiUnlock = "09_ui_unlock";
    public const string UiActivate = "10_ui_activate";
    public const string UiDeactivate = "11_ui_deactivate";
    public const string UiDisabled = "12_ui_disabled";
    public const string UiSettings = "13_ui_settings";
    public const string UiPause = "14_ui_pause";
    public const string UiLaunch = "15_ui_launch";
    public const string ShopOpen = "16_shop_open";
    public const string ShopBuySuccess = "17_shop_buy_success";
    public const string ShopBuyFail = "18_shop_buy_fail";
    public const string ShopWarning = "21_shop_warning";
    public const string ShopHostile = "22_shop_hostile";
    public const string TraitSelect = "23_trait_select";
    public const string ReinforcementEquip = "24_reinforcement_equip";
    public const string ReinforcementDrop = "25_reinforcement_drop";
    public const string ReinforcementPickup = "26_reinforcement_pickup";
    public const string ReinforcementUse = "27_reinforcement_use";
    public const string TabStatusOpen = "28_tab_status_open";
    public const string TabStatusClose = "29_tab_status_close";
    public const string WarningMessage = "30_warning_message";
    public const string ActionDenied = "31_action_denied";
    public const string RadarOpen = "32_radar_open";
    public const string RadarClose = "33_radar_close";
    public const string RadarScanPulse = "34_radar_scan_pulse";
    public const string RadarFail = "35_radar_fail";
    public const string PickupCredit = "36_pickup_credit";
    public const string PickupScrap = "37_pickup_scrap";
    public const string PickupCore = "38_pickup_core";
    public const string PickupHeal = "39_pickup_heal";
    public const string PickupTuningChip = "40_pickup_tuning_chip";
    public const string ReturnChoiceOpen = "42_return_choice_open";
    public const string SafeReturn = "43_safe_return";
    public const string ResultCountTick = "44_result_count_tick";
    public const string ResultRewardTotal = "45_result_reward_total";
    public const string EventStart = "46_event_start";
    public const string EventComplete = "47_event_complete";
    public const string ShipDashStart = "48_ship_dash_start";
    public const string ShipHit = "50_ship_hit";
    public const string ShipDeathBreakup = "51_ship_death_breakup";
    public const string MachineGunFire = "52_machinegun_fire_loop";
    public const string ShotgunFire = "53_shotgun_fire";
    public const string SniperChargeStart = "54_sniper_charge_start";
    public const string SniperChargeCancel = "55_sniper_charge_cancel";
    public const string SniperFire = "56_sniper_fire";
    public const string EnemyAlert = "57_enemy_alert";
    public const string EnemyBasicFire = "58_enemy_basic_fire";
    public const string EnemyShotgunFire = "59_enemy_shotgun_fire";
    public const string EnemyChargerAimLoop = "60_enemy_charger_aim_loop";
    public const string EnemyChargerFire = "61_enemy_charger_fire";
    public const string EnemyEliteSpreadFire = "62_enemy_elite_spread_fire";
    public const string EnemyEliteBigFire = "63_enemy_elite_big_fire";
    public const string EnemyHit = "64_enemy_hit";
    public const string EnemyDeath = "65_enemy_death";
    public const string ObjectContainerHit = "66_object_container_hit";
    public const string ObjectContainerBreak = "67_object_container_break";
    public const string ObjectDebrisHit = "68_object_debris_hit";
    public const string ObjectDebrisBreak = "69_object_debris_break";
    public const string ObjectShipwreckBreak = "70_object_shipwreck_break";
    public const string ObjectMeteorHit = "71_object_meteor_hit";
    public const string ObjectMeteorBreak = "72_object_meteor_break";
    public const string CoreInteractLoop = "73_core_interact_loop";
    public const string CoreActivate = "74_core_activate";
    public const string BossSpawn = "75_boss_spawn";
    public const string BossLaserWarning = "76_boss_laser_warning";
    public const string BossLaserLoop = "77_boss_laser_loop";
    public const string BossSpreadFire = "78_boss_spread_fire";
    public const string BossChargeAim = "79_boss_charge_aim";
    public const string BossChargeFire = "80_boss_charge_fire";
    public const string BossPhase2 = "81_boss_phase2";
    public const string BossDeath = "82_boss_death";
    public const string ReturnBeaconSpawn = "83_return_beacon_spawn";
    public const string WormholeEnter = "84_wormhole_enter";
    public const string ShopShieldHit = "85_shop_shield_hit";
    public const string ShopShieldBreak = "86_shop_shield_break";
    public const string ShopShotgunFire = "87_shop_shotgun_fire";
    public const string SecurityDroneSpawn = "88_security_drone_spawn";
    public const string RadarChargeStart = "89_radar_charge_start";
    public const string RadarChargeLoop = "90_radar_charge_loop";
    public const string RadarChargeCancel = "91_radar_charge_cancel";
    public const string SniperChargeLoop = "92_sniper_charge_loop";
    public const string ShipMovePuff = "93_ship_move_puff";
    public const string AmbSpaceLoop = "94_amb_space_loop";
    public const string AmbSettlementLoop = "95_amb_settlement_loop";
    public const string MusicCombatLoop = "96_music_combat_loop";
    public const string MusicBossLoop = "97_music_boss_loop";
    public const string MusicSettlementLoop = "98_music_settlement_loop";
    public const string MusicShopLoop = "99_music_shop_loop";
    public const string MusicMainMenuLoop = "105_music_main_menu_loop";
    public const string MusicTutorialLoop = "106_music_tutorial_loop";
    public const string MapRoutePlaced = "107_map_route_placed";
    public const string MapRouteRemoved = "108_map_route_removed";
    public const string MissionReceived = "109_mission_received";
    public const string AmbTutorialLoop = "110_amb_tutorial_loop";
    public const string DialogueCommIncoming = "111_dialogue_comm_incoming";
    public const string DialogueCommHijack = "112_dialogue_comm_hijack";

    // Retired slots 19, 20, 41 and 49 stay unused. Preserve the 100-104 gap.

    // 기존 코드 호환용 별칭
    public const string UiUpgrade = UiUpgradeSuccess;
    public const string RadarScanPulseUi = RadarScanPulse;

    private static readonly KeyValuePair<string, string>[] OrderedMappings =
    {
        new KeyValuePair<string, string>("ui_click", "01_ui_click"),
        new KeyValuePair<string, string>("ui_hover", "02_ui_hover"),
        new KeyValuePair<string, string>("ui_back", "03_ui_back"),
        new KeyValuePair<string, string>("ui_panel_open", "04_ui_panel_open"),
        new KeyValuePair<string, string>("ui_panel_close", "05_ui_panel_close"),
        new KeyValuePair<string, string>("ui_insufficient", "06_ui_insufficient"),
        new KeyValuePair<string, string>("ui_upgrade_success", "07_ui_upgrade_success"),
        new KeyValuePair<string, string>("ui_upgrade_fail", "08_ui_upgrade_fail"),
        new KeyValuePair<string, string>("ui_unlock", "09_ui_unlock"),
        new KeyValuePair<string, string>("ui_activate", "10_ui_activate"),
        new KeyValuePair<string, string>("ui_deactivate", "11_ui_deactivate"),
        new KeyValuePair<string, string>("ui_disabled", "12_ui_disabled"),
        new KeyValuePair<string, string>("ui_settings", "13_ui_settings"),
        new KeyValuePair<string, string>("ui_pause", "14_ui_pause"),
        new KeyValuePair<string, string>("ui_launch", "15_ui_launch"),
        new KeyValuePair<string, string>("shop_open", "16_shop_open"),
        new KeyValuePair<string, string>("shop_buy_success", "17_shop_buy_success"),
        new KeyValuePair<string, string>("shop_buy_fail", "18_shop_buy_fail"),
        new KeyValuePair<string, string>("shop_warning", "21_shop_warning"),
        new KeyValuePair<string, string>("shop_hostile", "22_shop_hostile"),
        new KeyValuePair<string, string>("trait_select", "23_trait_select"),
        new KeyValuePair<string, string>("reinforcement_equip", "24_reinforcement_equip"),
        new KeyValuePair<string, string>("reinforcement_drop", "25_reinforcement_drop"),
        new KeyValuePair<string, string>("reinforcement_pickup", "26_reinforcement_pickup"),
        new KeyValuePair<string, string>("reinforcement_use", "27_reinforcement_use"),
        new KeyValuePair<string, string>("tab_status_open", "28_tab_status_open"),
        new KeyValuePair<string, string>("tab_status_close", "29_tab_status_close"),
        new KeyValuePair<string, string>("warning_message", "30_warning_message"),
        new KeyValuePair<string, string>("action_denied", "31_action_denied"),
        new KeyValuePair<string, string>("radar_open", "32_radar_open"),
        new KeyValuePair<string, string>("radar_close", "33_radar_close"),
        new KeyValuePair<string, string>("radar_scan_pulse", "34_radar_scan_pulse"),
        new KeyValuePair<string, string>("radar_fail", "35_radar_fail"),
        new KeyValuePair<string, string>("pickup_credit", "36_pickup_credit"),
        new KeyValuePair<string, string>("pickup_scrap", "37_pickup_scrap"),
        new KeyValuePair<string, string>("pickup_core", "38_pickup_core"),
        new KeyValuePair<string, string>("pickup_heal", "39_pickup_heal"),
        new KeyValuePair<string, string>("pickup_tuning_chip", "40_pickup_tuning_chip"),
        new KeyValuePair<string, string>("return_choice_open", "42_return_choice_open"),
        new KeyValuePair<string, string>("safe_return", "43_safe_return"),
        new KeyValuePair<string, string>("result_count_tick", "44_result_count_tick"),
        new KeyValuePair<string, string>("result_reward_total", "45_result_reward_total"),
        new KeyValuePair<string, string>("event_start", "46_event_start"),
        new KeyValuePair<string, string>("event_complete", "47_event_complete"),
        new KeyValuePair<string, string>("ship_dash_start", "48_ship_dash_start"),
        new KeyValuePair<string, string>("ship_hit", "50_ship_hit"),
        new KeyValuePair<string, string>("ship_death_breakup", "51_ship_death_breakup"),
        new KeyValuePair<string, string>("machinegun_fire_loop", "52_machinegun_fire_loop"),
        new KeyValuePair<string, string>("shotgun_fire", "53_shotgun_fire"),
        new KeyValuePair<string, string>("sniper_charge_start", "54_sniper_charge_start"),
        new KeyValuePair<string, string>("sniper_charge_cancel", "55_sniper_charge_cancel"),
        new KeyValuePair<string, string>("sniper_fire", "56_sniper_fire"),
        new KeyValuePair<string, string>("enemy_alert", "57_enemy_alert"),
        new KeyValuePair<string, string>("enemy_basic_fire", "58_enemy_basic_fire"),
        new KeyValuePair<string, string>("enemy_shotgun_fire", "59_enemy_shotgun_fire"),
        new KeyValuePair<string, string>("enemy_charger_aim_loop", "60_enemy_charger_aim_loop"),
        new KeyValuePair<string, string>("enemy_charger_fire", "61_enemy_charger_fire"),
        new KeyValuePair<string, string>("enemy_elite_spread_fire", "62_enemy_elite_spread_fire"),
        new KeyValuePair<string, string>("enemy_elite_big_fire", "63_enemy_elite_big_fire"),
        new KeyValuePair<string, string>("enemy_hit", "64_enemy_hit"),
        new KeyValuePair<string, string>("enemy_death", "65_enemy_death"),
        new KeyValuePair<string, string>("object_container_hit", "66_object_container_hit"),
        new KeyValuePair<string, string>("object_container_break", "67_object_container_break"),
        new KeyValuePair<string, string>("object_debris_hit", "68_object_debris_hit"),
        new KeyValuePair<string, string>("object_debris_break", "69_object_debris_break"),
        new KeyValuePair<string, string>("object_shipwreck_break", "70_object_shipwreck_break"),
        new KeyValuePair<string, string>("object_meteor_hit", "71_object_meteor_hit"),
        new KeyValuePair<string, string>("object_meteor_break", "72_object_meteor_break"),
        new KeyValuePair<string, string>("core_interact_loop", "73_core_interact_loop"),
        new KeyValuePair<string, string>("core_activate", "74_core_activate"),
        new KeyValuePair<string, string>("boss_spawn", "75_boss_spawn"),
        new KeyValuePair<string, string>("boss_laser_warning", "76_boss_laser_warning"),
        new KeyValuePair<string, string>("boss_laser_loop", "77_boss_laser_loop"),
        new KeyValuePair<string, string>("boss_spread_fire", "78_boss_spread_fire"),
        new KeyValuePair<string, string>("boss_charge_aim", "79_boss_charge_aim"),
        new KeyValuePair<string, string>("boss_charge_fire", "80_boss_charge_fire"),
        new KeyValuePair<string, string>("boss_phase2", "81_boss_phase2"),
        new KeyValuePair<string, string>("boss_death", "82_boss_death"),
        new KeyValuePair<string, string>("return_beacon_spawn", "83_return_beacon_spawn"),
        new KeyValuePair<string, string>("wormhole_enter", "84_wormhole_enter"),
        new KeyValuePair<string, string>("shop_shield_hit", "85_shop_shield_hit"),
        new KeyValuePair<string, string>("shop_shield_break", "86_shop_shield_break"),
        new KeyValuePair<string, string>("shop_shotgun_fire", "87_shop_shotgun_fire"),
        new KeyValuePair<string, string>("security_drone_spawn", "88_security_drone_spawn"),
        new KeyValuePair<string, string>("radar_charge_start", "89_radar_charge_start"),
        new KeyValuePair<string, string>("radar_charge_loop", "90_radar_charge_loop"),
        new KeyValuePair<string, string>("radar_charge_cancel", "91_radar_charge_cancel"),
        new KeyValuePair<string, string>("sniper_charge_loop", "92_sniper_charge_loop"),
        new KeyValuePair<string, string>("ship_move_puff", "93_ship_move_puff"),
        new KeyValuePair<string, string>("amb_space_loop", "94_amb_space_loop"),
        new KeyValuePair<string, string>("amb_settlement_loop", "95_amb_settlement_loop"),
        new KeyValuePair<string, string>("music_combat_loop", "96_music_combat_loop"),
        new KeyValuePair<string, string>("music_boss_loop", "97_music_boss_loop"),
        new KeyValuePair<string, string>("music_settlement_loop", "98_music_settlement_loop"),
        new KeyValuePair<string, string>("music_shop_loop", "99_music_shop_loop"),
        new KeyValuePair<string, string>("music_main_menu_loop", "105_music_main_menu_loop"),
        new KeyValuePair<string, string>("music_tutorial_loop", "106_music_tutorial_loop"),
        new KeyValuePair<string, string>("map_route_placed", "107_map_route_placed"),
        new KeyValuePair<string, string>("map_route_removed", "108_map_route_removed"),
        new KeyValuePair<string, string>("mission_received", "109_mission_received"),
        new KeyValuePair<string, string>("amb_tutorial_loop", "110_amb_tutorial_loop"),
        new KeyValuePair<string, string>("dialogue_comm_incoming", "111_dialogue_comm_incoming"),
        new KeyValuePair<string, string>("dialogue_comm_hijack", "112_dialogue_comm_hijack"),
    };

    private static Dictionary<string, string> legacyToNumbered;
    private static Dictionary<string, string> numberedToLegacy;

    public static IReadOnlyList<KeyValuePair<string, string>> NumberedEventMappings => OrderedMappings;

    public static string ToNumbered(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return eventId;
        }

        EnsureMappings();

        if (numberedToLegacy.ContainsKey(eventId))
        {
            return eventId;
        }

        if (legacyToNumbered.TryGetValue(eventId, out string numbered))
        {
            return numbered;
        }

        string legacy = StripNumberPrefix(eventId);
        return legacyToNumbered.TryGetValue(legacy, out numbered) ? numbered : eventId;
    }

    public static string ToLegacy(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return eventId;
        }

        EnsureMappings();

        if (numberedToLegacy.TryGetValue(eventId, out string legacy))
        {
            return legacy;
        }

        string stripped = StripNumberPrefix(eventId);
        return legacyToNumbered.ContainsKey(stripped) ? stripped : eventId;
    }

    public static int GetOrder(string eventId)
    {
        string numbered = ToNumbered(eventId);
        int separatorIndex = numbered.IndexOf('_');

        if (separatorIndex <= 0)
        {
            return int.MaxValue;
        }

        return int.TryParse(numbered.Substring(0, separatorIndex), out int order)
            ? order
            : int.MaxValue;
    }

    public static bool IsKnown(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        EnsureMappings();
        return numberedToLegacy.ContainsKey(eventId) ||
               legacyToNumbered.ContainsKey(eventId) ||
               legacyToNumbered.ContainsKey(StripNumberPrefix(eventId));
    }

    private static string StripNumberPrefix(string eventId)
    {
        int separatorIndex = eventId.IndexOf('_');

        if (separatorIndex <= 0)
        {
            return eventId;
        }

        for (int i = 0; i < separatorIndex; i++)
        {
            if (!char.IsDigit(eventId[i]))
            {
                return eventId;
            }
        }

        return separatorIndex + 1 < eventId.Length
            ? eventId.Substring(separatorIndex + 1)
            : eventId;
    }

    private static void EnsureMappings()
    {
        if (legacyToNumbered != null && numberedToLegacy != null)
        {
            return;
        }

        legacyToNumbered = new Dictionary<string, string>(StringComparer.Ordinal);
        numberedToLegacy = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < OrderedMappings.Length; i++)
        {
            KeyValuePair<string, string> pair = OrderedMappings[i];
            legacyToNumbered[pair.Key] = pair.Value;
            numberedToLegacy[pair.Value] = pair.Key;
        }
    }
}
