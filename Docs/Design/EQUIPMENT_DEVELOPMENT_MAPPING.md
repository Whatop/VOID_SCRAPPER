# Equipment Development mapping — final 48 positions

Live catalog: 66 definitions, 62 ordinary, 48 board positions and fourteen preserved Shared legacy definitions. Metadata owns branch/tier/order; scene order is never authority.

| Branch / row | Position 1 | Position 2 | Position 3 |
|---|---|---|---|
| Shared / 1 | `shared_cargo_bay` | `shared_salvage_magnet` | `shared_engine_tuning` |
| Shared / 2 | `shared_salvage_protocol` | `shared_reinforced_plating` | `shared_repair_foam` |
| Shared / 3 | `shared_radar_amplifier` | `shared_dash_capacitor` | `shared_cutting_ammo` |
| Shared / 4 | `shared_return_container` | `shared_active_cooler` | `shared_periodic_reflector` |
| Sweeper / 1 | `mg_traverse_servo` | `mg_guidance_control` | `mg_stable_feed` |
| Sweeper / 2 | `mg_sustained_harvest_fire` | `mg_salvage_sweep` | `mg_midrange_pressure` |
| Sweeper / 3 | `mg_heat_exchanger` | `mg_line_penetrator` | `mg_target_distributor` |
| Sweeper / 4 | `mg_terminal_guidance` | `mg_dash_missile_salvo` | `mg_twin_feed` |
| Breacher / 1 | `sg_choke_barrel` | `sg_extra_pellet` | `sg_breaching_drive` |
| Breacher / 2 | `sg_close_harvest_burst` | `sg_taunt_resonator` | `sg_cycle_actuator` |
| Breacher / 3 | `sg_pellet_penetrator` | `sg_impact_ejector` | `sg_breach_compensator` |
| Breacher / 4 | `sg_close_quarters_overpressure` | `sg_slug_coupler` | `sg_breach_sequencer` |
| Lancer / 1 | `sn_charge_accelerator` | `sn_piercing_amplifier` | `sn_ballistic_alignment` |
| Lancer / 2 | `sn_focus_lens` | `sn_high_output_core` | `sn_mobile_charge_coupler` |
| Lancer / 3 | `sn_stealth_scan` | `sn_charge_aperture` | `sn_anchor_optics` |
| Lancer / 4 | `sn_semi_auto_laser` | `sn_dash_echo_shot` | `sn_reserve_capacitor` |

### Ownership, research and legacy compatibility

Research still opens 3/6/9/12 positions **per available branch**, with Breacher opening after first analysis and Lancer after second. Third analysis remains independent of Route Core assembly. There is no global fitting cap. Normal matching fitted modules deploy at Lv1 from the immutable RunContext snapshot, after store reset; player reconstruction reapplies attained levels once without reseeding or restoring depleted HP/Armor/Active Reinforcement. Run ending clears runtime levels, not manufacturing/fitting.

Save version 7 adds an idempotent research-grandfather list. Version-6 or earlier manufactured IDs retain their old legitimate research tier when moved later; stable per-definition previousDevelopmentResearchTier metadata records that old boundary. Normal ship gates remain, so backward F10 checkpoints disable locked branches without erasing purchases. New purchases use the final row. Missing catalog defers migration; unknown IDs remain diagnosed and preserved. No ownership is fabricated beyond the existing v5 fitted/purchased migration; no currency is charged/refunded.

The fourteen non-roster Shared definitions remain recognized for old owners, compatible and usable through the compact legacy section. They have no normal blueprint or manufacturing action for new players. The three displaced entries are shared_rapid_feed, shared_targeting_bus and shared_combat_gyro; promoted entries are shared_repair_foam, shared_dash_capacitor and shared_cutting_ammo. All existing assets/IDs/GUIDs remain.

Terminal Guidance alone remains conditional: fitted but unowned until mg_guidance_control reaches Lv3 and normal acquisition succeeds. No new prerequisites. Starting deployment levels retain nonrefundable provenance; earned upgrades keep existing field-drop/dismantling treatment. No empty/maxed pool reopens the master catalog.

## Final manufacturing recipes (2026-09-23)

Finalized against captured map supply; authoritative values are the live TraitDefinition manufacturing fields. Scrap/Core only, one-time payment; no auto-fit. The starter six cost 74 Scrap; natural first Settlement completion grants 86 Scrap exactly once. See [economy model](EQUIPMENT_ECONOMY.md) for outcome/frame tables, exact price changes and pacing assumptions.

| ID | Branch | Row / tier | Scrap | Core | Normal safe returns at unlock |
|---|---|---|---:|---:|---:|
| `shared_cargo_bay` | Shared | 1 / A | 12 | 0 | 0.37 |
| `shared_salvage_magnet` | Shared | 1 / A | 10 | 0 | 0.31 |
| `shared_engine_tuning` | Shared | 1 / A | 12 | 0 | 0.37 |
| `shared_salvage_protocol` | Shared | 2 / B | 20 | 0 | 0.61 |
| `shared_reinforced_plating` | Shared | 2 / B | 18 | 1 | 0.83 |
| `shared_repair_foam` | Shared | 2 / B | 18 | 0 | 0.55 |
| `shared_radar_amplifier` | Shared | 3 / C | 28 | 1 | 0.83 |
| `shared_dash_capacitor` | Shared | 3 / C | 30 | 0 | 0.89 |
| `shared_cutting_ammo` | Shared | 3 / C | 32 | 1 | 0.95 |
| `shared_return_container` | Shared | 4 / D | 44 | 0 | 1.28 |
| `shared_active_cooler` | Shared | 4 / D | 46 | 1 | 1.34 |
| `shared_periodic_reflector` | Shared | 4 / D | 52 | 2 | 1.52 |
| `mg_traverse_servo` | Sweeper | 1 / A | 12 | 0 | 0.37 |
| `mg_guidance_control` | Sweeper | 1 / A | 14 | 0 | 0.43 |
| `mg_stable_feed` | Sweeper | 1 / A | 14 | 0 | 0.43 |
| `mg_sustained_harvest_fire` | Sweeper | 2 / B | 20 | 0 | 0.61 |
| `mg_salvage_sweep` | Sweeper | 2 / B | 18 | 1 | 0.83 |
| `mg_midrange_pressure` | Sweeper | 2 / B | 22 | 0 | 0.67 |
| `mg_heat_exchanger` | Sweeper | 3 / C | 28 | 1 | 0.83 |
| `mg_line_penetrator` | Sweeper | 3 / C | 30 | 0 | 0.89 |
| `mg_target_distributor` | Sweeper | 3 / C | 36 | 1 | 1.07 |
| `mg_terminal_guidance` | Sweeper | 4 / D | 52 | 2 | 1.52 |
| `mg_dash_missile_salvo` | Sweeper | 4 / D | 50 | 2 | 1.51 |
| `mg_twin_feed` | Sweeper | 4 / D | 54 | 2 | 1.58 |
| `sg_choke_barrel` | Breacher | 1 / A | 12 | 0 | 0.37 |
| `sg_extra_pellet` | Breacher | 1 / A | 14 | 0 | 0.43 |
| `sg_breaching_drive` | Breacher | 1 / A | 12 | 0 | 0.37 |
| `sg_close_harvest_burst` | Breacher | 2 / B | 18 | 0 | 0.55 |
| `sg_taunt_resonator` | Breacher | 2 / B | 22 | 1 | 0.83 |
| `sg_cycle_actuator` | Breacher | 2 / B | 18 | 0 | 0.55 |
| `sg_pellet_penetrator` | Breacher | 3 / C | 30 | 0 | 0.89 |
| `sg_impact_ejector` | Breacher | 3 / C | 32 | 1 | 0.95 |
| `sg_breach_compensator` | Breacher | 3 / C | 30 | 1 | 0.89 |
| `sg_close_quarters_overpressure` | Breacher | 4 / D | 50 | 2 | 1.51 |
| `sg_slug_coupler` | Breacher | 4 / D | 54 | 2 | 1.58 |
| `sg_breach_sequencer` | Breacher | 4 / D | 52 | 2 | 1.52 |
| `sn_charge_accelerator` | Lancer | 1 / A | 12 | 0 | 0.36 |
| `sn_piercing_amplifier` | Lancer | 1 / A | 14 | 0 | 0.42 |
| `sn_ballistic_alignment` | Lancer | 1 / A | 10 | 0 | 0.30 |
| `sn_focus_lens` | Lancer | 2 / B | 20 | 0 | 0.59 |
| `sn_high_output_core` | Lancer | 2 / B | 24 | 1 | 0.76 |
| `sn_mobile_charge_coupler` | Lancer | 2 / B | 22 | 1 | 0.76 |
| `sn_stealth_scan` | Lancer | 3 / C | 28 | 0 | 0.83 |
| `sn_charge_aperture` | Lancer | 3 / C | 30 | 1 | 0.89 |
| `sn_anchor_optics` | Lancer | 3 / C | 32 | 1 | 0.95 |
| `sn_semi_auto_laser` | Lancer | 4 / D | 50 | 2 | 1.51 |
| `sn_dash_echo_shot` | Lancer | 4 / D | 52 | 2 | 1.52 |
| `sn_reserve_capacitor` | Lancer | 4 / D | 54 | 2 | 1.58 |

See [final roster](EQUIPMENT_FINAL_ROSTER.md) for actual incremental effects, hooks, icons and validation.
