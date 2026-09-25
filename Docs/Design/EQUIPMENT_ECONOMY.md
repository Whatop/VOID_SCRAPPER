# Equipment manufacturing economy — calibrated recipes (2026-09-23)

## Research Special equipment addendum (2026-09-25)

Three additional blueprints use the existing Scrap/Core transaction: Stabilized Return Module 24 / 1 after Sector Stabilizer analysis; Matter Compression Module 32 / 2 after Matter Compressor analysis; Phase Navigation Module 40 / 3 after Phase Navigation Lens analysis. They are optional progression rewards outside the 48-board budget, not free equipment grants. They do not enter the starter allowance. Existing 48 recipes, totals, world supply and legacy ownership remain unchanged. Their effects and additional purchase pacing require natural playtesting; no new economy simulation is claimed for these modules.

## Structural conversion addendum (2026-09-25)

The separate Operating Frame system is superseded. Row-2 manufacturing now offers Lightweight 20 Scrap / 0 Core, Standard 18 / 0, Heavy 18 / 1. Each is MaxLevel 1 and uses normal one-time manufacture/free fitting; none is in the starter grant. Row total 56 / 1, Shared total 322 / 6 and all-48 total 1364 / 34 remain unchanged. The other 45 recipes and six-item starter allowance are unchanged.

The exact seven nonempty fusion profiles (plus none) are in [Final Roster](EQUIPMENT_FINAL_ROSTER.md#structural-frame-equipment-conversion-2026-09-25). There is no count scaling. A researched version-7 selection converts to one module once at no cost; this migration does not spend/refund currency. Retired Salvage Protocol, Reinforced Plating and Repair Foam remain legacy equipment for existing owners.

**Historical simulation boundary:** the 2026-09-23 capture/results below measured the superseded free count-based frames. These are historical supply measurements, not executed natural-play or income validation of the new frames. Early pre-analysis saves now have no structural bonus. Heavy cargo is always +30 rather than the old +10 to +40; fusion profiles need new playtesting. Diagnostics now accept exact combinations. No reward tables or currency system changed.

The 48 live `TraitDefinition` manufacturing Scrap/Core fields are the sole runtime price authority. The transaction and UI read those fields. The tables here document them; they are not another runtime price list. Research, one-time manufacture, free unlimited fitting, immutable run loadout, Lv1 deployment and in-run upgrades are unchanged. All 66 definitions/62 ordinary definitions remain: 48 board positions and 14 legacy Shared modules. No combat effects, rarity weights, frames or world rewards were changed.

## Measurement method and limits

Twelve actual production Expedition maps were generated in rendered 480x270 Play Mode: three weather variants each for Region 1, first Region 2, first Region 3 and repeat Region 3. Fixed generation seeds were 7301–7312. `EquipmentEconomyDiagnostics.Capture` inventories live RewardDroppers, bound event outcomes and disabled/absent reward paths. The retained Editor-only fixture is `Assets/02_Scripts/UI/Editor/Tests/Fixtures/EquipmentEconomyMaps.json`. Boss Core supplements were checked against the live CoreObject/Salvage reward paths and their definitions, not blindly against unused boss reward assets.

`EquipmentEconomyDiagnostics.Run` evaluates the captured inventories against CURRENT RewardDefinition assets. Each scenario uses 512 trials, seed 9023 + sample index, production `RollCurrencies`, production pickup bonus rounding, production `RunContext` cargo acceptance and the private production `RunManager.CreateRunResult` via Editor-only reflection. It preserves Unity's random state and never completes a live run or grants progression. Run through Unity `-executeMethod EquipmentEconomyDiagnostics.Run -equipmentEconomyOutput <json path>`; recapture inventories after map authoring changes. Tests check invariants, not fragile exact means.

Quick visits 40%, Normal 65%, Thorough 90% of optional economy sources. Visits are sampled, not a pathfinding/combat bot; successful event/base completion is assumed for visited rewards. Unknown-device alternatives are mutually exclusive. Boss-cleared budgets assume the boss is completed near the end; a separate no-boss view exposes the Core dependency. No win-rate or run-duration claim is made.

All ships currently have base cargo 100. The representative build includes the initial cargo module's real Lv1 +8, with 6/12/18 fitted modules at first Regions 1/2/3 and 24 in late repeat Region 3. No optional technology, upgraded cargo/yield Traits, dismantling income, hostile shop destruction, extra stolen chest contents or previous carried resources are credited. This is a baseline, not a maximum build. Frame yield and weather bonuses are applied per actual pickup shard; rounding often removes Standard's 5% yield bonus on one-unit drops.

Cargo weights are Scrap 2, Core 12, Alloy 2. The player is assumed to retain Core first, then Alloy, then Scrap using existing inventory triage. Alloy drops only in Region 1. This prioritization is explicit: it can reduce Lightweight's retained Scrap on a Thorough trip as more Alloy/Core displace Scrap. A Scrap-first player would obtain a different mix, not free extra total cargo. Guaranteed first Region-2 boss Core is added after normal packing, matching its existing capacity-bypass award when the boss is last.

Safe Return/Final Victory keep the carried wallet. Emergency Return packs into 70% capacity using the existing proportional floor and Core/Alloy/Scrap remainder policy. Death keeps 50% Scrap, rounded down, and loses ordinary Core. Only the existing 2-Core first Salvage Devourer reward is protected on emergency/death. Repeat Raider Core is physical/unprotected. No new guarantee is introduced.

## Sources actually contributing

| Source | Authored Scrap | Authored Core | Accounting |
|---|---:|---:|---|
| Container | 0–1 | 0 | 16 baseline; Dense Debris adds 4 |
| Destroyed hull | 1–2 | 0 | 8 baseline; Dense adds 2; field-base resource chests also use this table |
| High-value wreck | 2–3 | 0 | 3 baseline; first Region-3 arena has 1 |
| Basic / shotgun / charging enemies | 0 | 0 | Credits only; not fictitious permanent income |
| Elite enemy | 1 | 0 | Actual generated elite count, including weather/depth differences |
| Field-base midboss | 3 | 0 | Two generated bases; guards/chests counted once |
| Rescue / black box / reactor | 2 | Reactor 0–1 | Actual event mix; spawned event enemies suppress normal drops |
| Unknown device | 1 safe / 2 combat | 0 | Weighted alternative, not two rewards |
| Operations/objectives | 0 | 0 | Normal-region operation Alloy 1 occupies cargo; do not count target reward twice |
| Region-1 and repeat Raider boss | 0 | 1 | Completed CoreObject physical pickup; repeat path renewable |
| First Region-2 Salvage Devourer | 0 | 2 | Existing exact-once per-run protected, capacity-bypass award |
| First Region-3 Phase Gatekeeper arena | 0 | 0 live currency path | No CoreObject; boss EnemyHealth normal reward disabled |

All 20 RewardDefinition assets were inspected. `BossReward` (Scrap 4), `EnemyContainer`, `SpecialTraitContainer` and `SpecialReinforcementContainer` (Core 1) cannot simply be added to these maps: actual boss drops are disabled, actual base chests use DestroyedHull, and the special-container prefab references/arrays are unassigned in Expedition. Shop destruction is optional hostile income and excluded. Tutorial teaching tables are not renewable expedition sources. Meteors have no currency RewardDropper in these samples.

Configured depth Scrap multipliers 1.25/1.45 have no live callers in the audited reward path; the model does NOT pretend they apply or introduce another multiplier. First Region 3 intentionally uses a sparse boss arena (two containers, two hulls, one wreck); its ordinary exploration route returns after the first boss. It is not a viable standalone manufacturing-income map. Late pricing uses that existing repeat route, not imaginary first-arena loot.

## Raw theoretical supply and discovery

Theoretical means assume every authored economy source resolves; Core includes the existing boss path. Discovery is before cargo, with the profile's visit fraction and a completed boss. Pairs below are **Scrap / Core**.

| Map | Theoretical | Quick discoverable | Normal discoverable | Thorough discoverable |
|---|---:|---:|---:|---:|
| Region 1 / Normal | 50.39 / 1.32 | 20.22 / 1.12 | 32.86 / 1.21 | 45.33 / 1.30 |
| Region 2 / DeepZone1, first boss | 52.07 / 2.50 | 20.67 / 2.20 | 33.71 / 2.32 | 46.79 / 2.45 |
| Region 3 / DeepZone2, first boss arena | 6.52 / 0.00 | 2.62 / 0.00 | 4.23 / 0.00 | 5.87 / 0.00 |
| Region 3 / DeepZone2, repeat exploration | 52.98 / 1.50 | 21.09 / 1.20 | 34.27 / 1.33 | 47.52 / 1.44 |

## Cargo-adjusted committed income

Every cell is expected Scrap / Core, rounded to two decimals. These are outcome-conditional budgets, not guaranteed payouts.

### Region 1 / Normal

| Profile | Frame | Capacity | Safe | Emergency | Death | Cargo-limited trials |
|---|---|---:|---:|---:|---:|---:|
| Quick | Lightweight | 88 | 20.07 / 1.12 | 18.35 / 0.84 | 9.79 / 0.00 | 4% |
| Quick | Standard | 118 | 20.22 / 1.12 | 19.98 / 1.11 | 9.86 / 0.00 | 0% |
| Quick | Heavy | 118 | 20.22 / 1.12 | 19.98 / 1.11 | 9.86 / 0.00 | 0% |
| Normal | Lightweight | 88 | 27.76 / 1.21 | 20.87 / 0.28 | 13.63 / 0.00 | 67% |
| Normal | Standard | 118 | 32.65 / 1.21 | 27.77 / 0.95 | 16.08 / 0.00 | 5% |
| Normal | Heavy | 118 | 32.65 / 1.21 | 27.77 / 0.95 | 16.08 / 0.00 | 5% |
| Thorough | Lightweight | 88 | 25.72 / 1.30 | 17.96 / 0.30 | 12.62 / 0.00 | 100% |
| Thorough | Standard | 118 | 39.90 / 1.30 | 28.40 / 0.48 | 19.69 / 0.00 | 74% |
| Thorough | Heavy | 118 | 39.90 / 1.30 | 28.40 / 0.48 | 19.69 / 0.00 | 74% |

### Region 2 / DeepZone1, first boss

| Profile | Frame | Capacity | Safe | Emergency | Death | Cargo-limited trials |
|---|---|---:|---:|---:|---:|---:|
| Quick | Lightweight | 88 | 20.67 / 2.20 | 19.27 / 2.01 | 10.10 / 2.00 | 0% |
| Quick | Standard | 118 | 20.67 / 2.20 | 20.58 / 2.16 | 10.10 / 2.00 | 0% |
| Quick | Heavy | 128 | 20.67 / 2.20 | 20.66 / 2.17 | 10.10 / 2.00 | 0% |
| Normal | Lightweight | 88 | 33.43 / 2.32 | 23.61 / 2.00 | 16.50 / 2.00 | 8% |
| Normal | Standard | 118 | 33.71 / 2.32 | 30.55 / 2.01 | 16.62 / 2.00 | 0% |
| Normal | Heavy | 128 | 33.71 / 2.32 | 32.04 / 2.03 | 16.62 / 2.00 | 0% |
| Thorough | Lightweight | 88 | 40.84 / 2.45 | 24.00 / 2.00 | 20.38 / 2.00 | 81% |
| Thorough | Standard | 118 | 46.72 / 2.45 | 33.10 / 2.00 | 23.11 / 2.00 | 3% |
| Thorough | Heavy | 128 | 46.79 / 2.45 | 35.74 / 2.00 | 23.15 / 2.00 | 0% |

### Region 3 / DeepZone2, first boss arena

| Profile | Frame | Capacity | Safe | Emergency | Death | Cargo-limited trials |
|---|---|---:|---:|---:|---:|---:|
| Quick | Lightweight | 88 | 2.62 / 0.00 | 2.62 / 0.00 | 1.08 / 0.00 | 0% |
| Quick | Standard | 118 | 2.62 / 0.00 | 2.62 / 0.00 | 1.08 / 0.00 | 0% |
| Quick | Heavy | 138 | 2.62 / 0.00 | 2.62 / 0.00 | 1.08 / 0.00 | 0% |
| Normal | Lightweight | 88 | 4.23 / 0.00 | 4.23 / 0.00 | 1.88 / 0.00 | 0% |
| Normal | Standard | 118 | 4.23 / 0.00 | 4.23 / 0.00 | 1.88 / 0.00 | 0% |
| Normal | Heavy | 138 | 4.23 / 0.00 | 4.23 / 0.00 | 1.88 / 0.00 | 0% |
| Thorough | Lightweight | 88 | 5.87 / 0.00 | 5.87 / 0.00 | 2.69 / 0.00 | 0% |
| Thorough | Standard | 118 | 5.87 / 0.00 | 5.87 / 0.00 | 2.69 / 0.00 | 0% |
| Thorough | Heavy | 138 | 5.87 / 0.00 | 5.87 / 0.00 | 2.69 / 0.00 | 0% |

### Region 3 / DeepZone2, repeat exploration

| Profile | Frame | Capacity | Safe | Emergency | Death | Cargo-limited trials |
|---|---|---:|---:|---:|---:|---:|
| Quick | Lightweight | 88 | 21.07 / 1.20 | 20.51 / 0.98 | 10.29 / 0.00 | 1% |
| Quick | Standard | 118 | 21.09 / 1.20 | 21.05 / 1.19 | 10.30 / 0.00 | 0% |
| Quick | Heavy | 148 | 21.09 / 1.20 | 21.09 / 1.20 | 10.30 / 0.00 | 0% |
| Normal | Lightweight | 88 | 32.43 / 1.33 | 27.41 / 0.41 | 16.06 / 0.00 | 35% |
| Normal | Standard | 118 | 34.25 / 1.33 | 32.46 / 1.07 | 16.88 / 0.00 | 1% |
| Normal | Heavy | 148 | 34.27 / 1.33 | 34.15 / 1.28 | 16.89 / 0.00 | 0% |
| Thorough | Lightweight | 88 | 35.34 / 1.44 | 27.36 / 0.44 | 17.67 / 0.00 | 99% |
| Thorough | Standard | 118 | 46.10 / 1.44 | 37.60 / 0.57 | 22.72 / 0.00 | 28% |
| Thorough | Heavy | 148 | 47.50 / 1.44 | 44.57 / 0.97 | 23.49 / 0.00 | 1% |

### Renewable Core and no-boss sensitivity

| Map | Normal safe without boss | Normal safe with relevant boss |
|---|---:|---:|
| Region 1 / Normal | 32.83 / 0.21 | 32.65 / 1.21 |
| Region 2 / DeepZone1, first boss | 33.71 / 0.32 | 33.71 / 2.32 |
| Region 3 / DeepZone2, repeat exploration | 34.27 / 0.33 | 34.25 / 1.33 |

The first Region-2 2-Core guarantee is not a repeat farming stipend. Replacing only that first-boss supplement with the existing Raider 1-Core definition gives a **same-inventory repeat sensitivity** of 33.71 / 1.32 on Normal Standard safe return, 32.16 / 1.09 Emergency and 16.62 / 0.00 Death. This row is simulated from the captured Region-2 inventory, not advertised as another rendered map capture.

Reactor Core is a sustainable non-boss path, but only roughly 0.2–0.33 per Normal trip in these samples. A boss-avoiding player can need several returns per Core. No-boss early purchases therefore retain Scrap-only alternatives in each unlock burst, including the Row-4 return container. Basic ship trials never need Core. Core-heavy signatures intentionally encourage successful boss/repeat routes; ordinary Core is not protected on death.

### Historical count-based frame tradeoff (superseded)

| Stage | Heavy vs Standard Normal safe Scrap | Heavy vs Standard Thorough safe Scrap |
|---|---:|---:|
| Region 1 / Normal | 0.0% | 0.0% |
| Region 2 / DeepZone1, first boss | 0.0% | 0.1% |
| Region 3 / DeepZone2, repeat exploration | 0.1% | 3.0% |

At 6 fitted modules Heavy and Standard have identical cargo; at late 24-module Normal collection world supply is limiting, not cargo. Heavy benefits Thorough trips more, but no extreme safe-income advantage appears in this baseline. Emergency retention also benefits from its higher capacity. High-level cargo/yield equipment and hoarding across portals were not simulated; natural-play checks remain necessary. Frame numerical values remain provisional and unchanged. No hidden frame reward penalty was added.

## Starter transaction

Initial blueprints: Shared cargo bay 12, salvage magnet 10, engine tuning 12; Sweeper traverse servo 12, guidance control 14, stable feed 14. **Total 74 Scrap / 0 Core. Allowance 86 Scrap / 0 Core**: total plus ceil(15%) = 12 buffer (16.2%). No Core is granted because no initial recipe needs it. Existing Tutorial earnings are extra, not assumed required.

`PermanentProgress.TryGrantEquipmentStarterMaterials` requires Tutorial completion, first Settlement introduction completion, Settlement state, no active run and no equipment transaction. `SettlementController.TryCompleteFirstSettlementStory` calls it after natural story completion; `Start` supplies an eligible-old-save/failed-save recovery path. Panel opening never grants it. It does not require the dialogue pause to have already released.

Receipt `equipment_starter_materials_granted` and resources are persisted atomically in the existing save-first equipment transaction before memory/Changed notification. Duplicate callbacks, reloads and scene re-entry do not replay it; failures leave memory unchanged and use the current SaveManager error policy. The existing Settlement message appears only on successful first grant. Existing eligible completed saves receive this allowance once; old manufacture/fitting is neither refunded nor erased. F10 campaign rollback retains the receipt. No save-version bump, new save authority or direct equipment grants.

## Final 48 recipes

Scrap is the main material. Core specializes later manufacture; no Alloy, Credits, Tuning Chips, XP or boss components are consumed. Fit/unfit remains free; structural frame manufacture uses the Row-2 prices above. Prices are finalized for this pass; combat values, rarities, frame values and natural-play pacing are not declared finally balanced.

| Manufacturing row | Scrap band | Core band | Intent |
|---|---:|---:|---|
| A / 1 | 10–14 | 0 | About 0.30–0.43 Normal returns; two new-ship basics within one return |
| B / 2 | 18–24 | 0–1 | About 0.53–0.83 Normal returns; choose specialization |
| C / 3 | 28–36 | 0–1 | About 0.83–1.07 Normal returns; focused advanced modules |
| D / 4 | 44–54 | 0–2 | About 1.28–1.58 Normal returns; no normal item above 2.5 |

Per-item estimates use max(Scrap price / Scrap income, Core price / Core income), a banking estimate rather than a guarantee about one random trip. Unlock-stage baseline: Row1 and early Row2 use Region1; Lancer early rows and all Row3 use the conservative Region2 repeat sensitivity; Row4 uses repeat Region3. Do not divide by the sparse first Region3 arena or by the nonrenewable first Region2 guarantee.

| ID | Branch | Row / tier | Scrap | Core | Normal safe returns at unlock |
|---|---|---|---:|---:|---:|
| `shared_cargo_bay` | Shared | 1 / A | 12 | 0 | 0.37 |
| `shared_salvage_magnet` | Shared | 1 / A | 10 | 0 | 0.31 |
| `shared_engine_tuning` | Shared | 1 / A | 12 | 0 | 0.37 |
| `shared_lightweight_frame` | Shared | 2 / B | 20 | 0 | 0.61 |
| `shared_standard_frame` | Shared | 2 / B | 18 | 0 | 0.55 |
| `shared_heavy_frame` | Shared | 2 / B | 18 | 1 | 0.83 |
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

### Completion totals

| Scope | Scrap | Core |
|---|---:|---:|
| Shared 12 | 322 | 6 |
| Sweeper 12 | 350 | 9 |
| Breacher 12 | 344 | 9 |
| Lancer 12 | 348 | 10 |
| Shared + Sweeper 24 | 672 | 15 |
| Shared + Breacher 24 | 666 | 15 |
| Shared + Lancer 24 | 670 | 16 |
| All 48 | 1364 | 34 |

### Campaign ownership budget and unresolved pacing tension

The six starter items are fully affordable. One Normal successful return supports two Breacher/Lancer Row1 basics; Core is never required to try a new ship. Research bursts do not grant purchases. Other Settlement upgrades still compete for the same bank.

An immediate three-boss campaign rush yields only about 71 permanent Scrap plus the starter bank in this baseline; it cannot buy 60–80% of a 24-item path while retaining the requested per-item return bands. That is a design constraint, not hidden by the model. A meaningful exploration campaign must include extra returns before/around analysis milestones. The following are optimistic focused-ship banking estimates: starter six plus cheapest currently researched useful modules in the chosen 24; no optional facility spending, no failures, no Core scarcity beyond the repeat-boss means. Breacher/Lancer estimates do not double-count the starter Sweeper purchases as their own branch equipment.

| Relevant path | 15/24 (62.5%) cost | 19/24 (79.2%) cost | Approx. successful economy returns beyond allowance |
|---|---:|---:|---:|
| Shared + Sweeper | 276 Scrap / 4 Core | 418 Scrap / 6 Core | 5.7 / 9.9 |
| Shared + Breacher | 274 Scrap / 3 Core | 412 Scrap / 6 Core | 6.8 / 10.9 |
| Shared + Lancer | 278 Scrap / 4 Core | 416 Scrap / 7 Core | 6.9 / 11.0 |

Add the low-income first Region3 boss trip to those economy-return counts. For Sweeper this means roughly 7 total successful trips for the lower ownership target and roughly 11 for the upper, rather than three mandatory boss clears. These trips can occur before third analysis; they are not a required campaign gate. Losing returns, expensive preferred signatures and optional facilities extend this. The upper target may feel too grindy if natural campaign play contains fewer optional returns. No global reward inflation was introduced to disguise that unresolved human pacing question. Full 24 and especially all 48 remain optional collection goals. A campaign can complete with much less ownership.

## Price changes and world preservation

All 48 board recipes were calibrated: 46 asset prices changed; shared_salvage_magnet and sn_ballistic_alignment already matched their final 10/0 prices. The following table records exact previous versus final prices. No legacy ownership charge/refund, world reward value, map count, boss guarantee, return risk, combat LevelEffect, rarity or Special reward weight changed.

| ID | Previous Scrap / Core | Final Scrap / Core |
|---|---:|---:|
| `shared_cargo_bay` | 1 / 1 | 12 / 0 |
| `shared_salvage_magnet` | 10 / 0 | 10 / 0 |
| `shared_engine_tuning` | 20 / 1 | 12 / 0 |
| `shared_salvage_protocol` | 1 / 3 | 20 / 0 |
| `shared_reinforced_plating` | 20 / 1 | 18 / 1 |
| `shared_repair_foam` | 20 / 1 | 18 / 0 |
| `shared_radar_amplifier` | 30 / 2 | 28 / 1 |
| `shared_dash_capacitor` | 30 / 2 | 30 / 0 |
| `shared_cutting_ammo` | 40 / 3 | 32 / 1 |
| `shared_return_container` | 50 / 4 | 44 / 0 |
| `shared_active_cooler` | 50 / 4 | 46 / 1 |
| `shared_periodic_reflector` | 60 / 5 | 52 / 2 |
| `mg_traverse_servo` | 10 / 0 | 12 / 0 |
| `mg_guidance_control` | 20 / 1 | 14 / 0 |
| `mg_stable_feed` | 10 / 0 | 14 / 0 |
| `mg_sustained_harvest_fire` | 20 / 1 | 20 / 0 |
| `mg_salvage_sweep` | 30 / 2 | 18 / 1 |
| `mg_midrange_pressure` | 30 / 2 | 22 / 0 |
| `mg_heat_exchanger` | 40 / 3 | 28 / 1 |
| `mg_line_penetrator` | 40 / 3 | 30 / 0 |
| `mg_target_distributor` | 50 / 4 | 36 / 1 |
| `mg_terminal_guidance` | 60 / 5 | 52 / 2 |
| `mg_dash_missile_salvo` | 50 / 4 | 50 / 2 |
| `mg_twin_feed` | 60 / 5 | 54 / 2 |
| `sg_choke_barrel` | 20 / 1 | 12 / 0 |
| `sg_extra_pellet` | 30 / 2 | 14 / 0 |
| `sg_breaching_drive` | 20 / 1 | 12 / 0 |
| `sg_close_harvest_burst` | 30 / 2 | 18 / 0 |
| `sg_taunt_resonator` | 40 / 3 | 22 / 1 |
| `sg_cycle_actuator` | 20 / 1 | 18 / 0 |
| `sg_pellet_penetrator` | 40 / 3 | 30 / 0 |
| `sg_impact_ejector` | 40 / 3 | 32 / 1 |
| `sg_breach_compensator` | 40 / 3 | 30 / 1 |
| `sg_close_quarters_overpressure` | 50 / 4 | 50 / 2 |
| `sg_slug_coupler` | 60 / 5 | 54 / 2 |
| `sg_breach_sequencer` | 60 / 5 | 52 / 2 |
| `sn_charge_accelerator` | 20 / 1 | 12 / 0 |
| `sn_piercing_amplifier` | 20 / 1 | 14 / 0 |
| `sn_ballistic_alignment` | 10 / 0 | 10 / 0 |
| `sn_focus_lens` | 30 / 2 | 20 / 0 |
| `sn_high_output_core` | 30 / 2 | 24 / 1 |
| `sn_mobile_charge_coupler` | 30 / 2 | 22 / 1 |
| `sn_stealth_scan` | 30 / 2 | 28 / 0 |
| `sn_charge_aperture` | 40 / 3 | 30 / 1 |
| `sn_anchor_optics` | 40 / 3 | 32 / 1 |
| `sn_semi_auto_laser` | 40 / 3 | 50 / 2 |
| `sn_dash_echo_shot` | 50 / 4 | 52 / 2 |
| `sn_reserve_capacitor` | 60 / 5 | 54 / 2 |

## UI and executed validation

Two authored currency rows reuse the existing Scrap/Core sprites under Settlement EquipmentDevelopment/Inspection/Growth/Content. Each shows cost and current permanent amount, insufficient funds in warning color, and a disabled manufacturing action. Zero Core rows disappear; owned/fitted/locked equipment shows no manufacturing price. Researched unmanufactured structural modules show the normal price. No runtime UI generation or Settlement Upgrade action.

- Runtime static compile: 0 errors, 52 existing warnings. Editor/test static compile: 0 errors, 4 existing warnings.
- Full EditMode: 693/693 passed, 0 failed. This includes 133 Equipment Development cases, 22 new economy cases. The pre-change 671-test result is not used as evidence for this pass.
- Normal localization import/validation executed; the first focused run caught one malformed new UTF-8 label, corrected before final validation.
- Twelve generated map inventories/screenshots were captured at 480x270. Multi-map harness attempts stalled on repeated loading; remaining inventories were captured in fresh processes. These were capture-harness timeouts, not successful full campaign playthroughs.
- Rendered 480x270 transaction smoke completed in an isolated save: natural first Settlement dialogue completion -> 86 Scrap -> manufacture/fit all six -> 12 remaining -> unaffordable craft denied -> actual Lv1 deployment -> generated hull destruction and real reactor interaction/damage -> collect **22 Scrap / 1 Core** through RewardPickup -> SafeReturn -> exact permanent commit -> manufacture `mg_salvage_sweep` for **18 Scrap / 1 Core** -> disk reload. 49 assertions, seven transaction screenshots. No direct permanent-resource top-up. A research-only F10 checkpoint opened the next recipe; combat/path traversal was API-driven.
- Reactor is legitimately random 0–1 Core. Two initial smoke seeds rolled zero; a positive roll was located in the unchanged production table (seed 2) to test the Core transaction. Income diagnostics still sample zeros normally. This is functional coverage, not a guarantee of reactor income.
- Emergency/death are covered by production settlement/commit integration and deterministic sampling, not claimed as rendered death/emergency playthroughs. Temporary probe removed.
- Task-start hash comparison: no pre-existing file deleted; 46 Trait asset changes are strictly manufacturing fields and all existing GUIDs match. Settlement retains all 4,998 pre-existing serialized object/component IDs; only Growth/Content child ordering and the panel cost bindings changed, with 26 new IDs for authored price presentation and zero missing local references. Scoped git diff --check and new-code whitespace checks passed.

Remaining natural-play judgment: campaign trip count and optional facility competition; non-boss Core droughts; first Region3 sparse reward expectations; Heavy with upgraded cargo/yield and long portal routes; translated cost-row readability; late Special upgrade opportunity frequency (separate issue, unchanged here). No new currency, post-ending Curse, Trait combat rebalance, Special weight change, commit or push.
