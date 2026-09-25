# Final Equipment Development Roster — implemented foundation

## Additional research Special blueprints (2026-09-25)

Shared includes a compact Special research subsection with three additional equipment blueprints outside the 48 positions. The original story parts remain campaign state; each corresponding completed analysis unlocks its blueprint without auto-manufacturing or auto-fitting. All three are Shared-compatible, Special rarity, MaxLevel 3, with ordinary paid manufacturing, free unrestricted fitting and Lv1 deployment.

| ID | English name | Identity accent | Scrap / Core | Each level's increments |
|---|---|---|---:|---|
| `special_sector_stabilization` | Stabilized Return Module | Orange | 24 / 1 | HP +1; healing efficiency +5%; Emergency Return capacity ratio +5 percentage points |
| `special_matter_compression` | Matter Compression Module | Green | 32 / 2 | Cargo +6; harvest yield +3%; pickup range +0.20 |
| `special_phase_navigation` | Phase Navigation Module | Blue | 40 / 3 | Radar radius +1; range +5%; spread reduction +3% |

Existing multiplicative percentage semantics apply. Icons reuse Reinforced Plating, Cargo Bay and Radar Amplifier assets. The three hidden boss protocol definitions remain unchanged and distinct. See [implementation and validation report](RESEARCH_SPECIAL_EQUIPMENT_REPORT.md).

## Structural frame equipment conversion (2026-09-25)

This supersedes the separate Operating Frame system from 2026-09-22. There is no saved frame preference, selector, count tier or independent frame runtime authority. The old numeric selection survives only as a consumed migration tombstone in SaveData.

Equipment Development exposes Shared and the branch from PermanentProgress's current selected ship/WeaponTree. Existing Hangar changes refresh the panel through its existing change subscription. Unrelated authored tabs stay present but inactive. An invalid inspected branch falls back to Shared; inspecting content never selects a ship. All branches' ownership/fitting preferences remain stored; deployment still resolves only Shared plus the selected ship.

Shared Row 2 is exactly: `shared_lightweight_frame`, `shared_standard_frame`, `shared_heavy_frame`. All three unlock after the first completed boss-component analysis. They are normal manufactured Shared equipment: pay once, fit/unfit freely, and fit none, one, any pair or all three. All are Common, MaxLevel 1, deploy at Lv1/MAX, and use existing upgrade eligibility. They have empty individual LevelEffects: no single-frame effects can stack under a fusion. The common CanUpgrade check retains their active deployment Lv1 floor even after a runtime level is removed, so dismantling cannot reopen structural upgrade offers. Ordinary fitting and reward callers are unchanged.

The following exact profiles are provisional; each row completely replaces the singles. Values of zero mean no modifier. Total ordinary equipment count never enters resolution.

| Fitted frames | Move speed | Dash cooldown | Dash distance | Max HP | Cargo | Damage | Harvest yield |
|---|---:|---:|---:|---:|---:|---:|---:|
| None | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Lightweight | +12% | -15% | +0.60 | -3 | -20 | 0 | 0 |
| Standard | 0 | 0 | 0 | 0 | +10 | +5% | +5% |
| Heavy | -10% | +15% | 0 | +6 | +30 | 0 | 0 |
| Lightweight + Standard | +8% | -10% | 0 | -2 | -5 | +3% | +3% |
| Lightweight + Heavy | +4% | 0 | +0.30 | +2 | +10 | 0 | 0 |
| Standard + Heavy | -6% | +8% | 0 | +4 | +25 | +3% | +3% |
| Lightweight + Standard + Heavy (Integrated) | +3% | 0 | +0.15 | +3 | +15 | +3% | +3% |

RunContext.Begin resolves the three equipment IDs from the immutable effective fitting, records the bit set and exposes one StructuralFrameProfile. Runtime Trait levels, dismantling, live Settlement preferences and portal travel cannot change that profile. PlayerRuntimeStatApplier remains the single owner: reset/base ship stats -> structural profile -> technology/story effects -> stat commit. ExpeditionBootstrap then restores Reinforcement, region effects and current runtime Trait levels; depleted carried HP/Armor restore last on portals. Fresh runs initialize fresh vitals. Movement and cooldown use source-owned external multipliers; distance, HP and cargo use the existing rebuild/commit paths; damage and yield use existing modifier components. No authored ship data changes, manager or Update loop were added. Run end removes player/run effects through the existing lifecycle.

Manufacturing recipes (Scrap / Core): Lightweight **20 / 0**, Standard **18 / 0**, Heavy **18 / 1**. Row-2 total remains 56 / 1. Other 45 board definitions and recipes are unchanged. Initial six remain 74 Scrap; starter allowance remains 86 Scrap / 0 Core and includes no frames.

`shared_salvage_protocol`, `shared_reinforced_plating`, `shared_repair_foam` are retired from the normal board, not deleted. Their IDs, GUIDs, recipes, effects and existing ownership/fitting remain intact. They appear only in the existing legacy-owner section, with no new-player research/manufacture and no refund or automatic replacement. Catalog totals are now 72 definitions: 68 ordinary-flow equipment (48 board + 17 Shared legacy + 3 research Special), three boss and the unchanged existing story Curse trait.

Save version 8 consumes version-7 `selectedOperatingFrame` (0 Standard, 1 Lightweight, 2 Heavy). A valid recorded selection plus Sector Stabilizer ownership and unlocked DeepZone1 grants only the corresponding manufactured + fitted module without payment. Before Row 2, absent fields and invalid values grant nothing. Explicit DTO defaults during JSON overwrite distinguish an absent pre-frame field from recorded Standard. The tombstone resets to -1; v8 cannot regrant/refit on reload. Currency, campaign, roster ownership, fitting and prior migration receipts are preserved. Older version-5/6 ownership migrations still run normally.

Settlement's selector objects/listeners are removed. Structural equipment details show the Shared structural tag, Max Lv.1, single effects, fusion explanation, current profile and projected profile after fitting. Blue hover/focus, yellow inspection and the separate fitted indicator remain. TAB inspection uses the active immutable profile and actual modifiers; F10 reports structural manufacture/fitting and effective combination without a separate setter. Shared inventory prefab and Expedition's unpacked copy are both updated.

The integrated profile trades specialist peaks for breadth. Natural testing is still needed for Lightweight with depleted HP, Heavy plus existing cargo overload, all-three versus specialist opportunity cost, and unrestricted fitting alongside legacy stat modules. Historical count-based economy simulations are not validation of these profiles. The post-ending Curse system is not implemented.

Implementation date: 2026-09-22. This implements the approved 48-position plan, with the explicitly approved Twin Feed A-to-B cadence adjustment. Existing data was retained; sixteen ship modules were added. Combat values and rarity remain provisional. Manufacturing prices were calibrated on 2026-09-23; see EQUIPMENT_ECONOMY.md.

| Branch | Ordinary-flow definitions | Final board | Research Special | Legacy outside board |
|---|---:|---:|---:|---:|
| Shared | 32 | 12 | 3 | 17 |
| Sweeper | 12 | 12 | 0 | 0 |
| Breacher | 12 | 12 | 0 | 0 |
| Lancer | 12 | 12 | 0 | 0 |

Rows: 1 Foundation; 2 Specialization; 3 Build Transformation; 4 Signature/Capstone. A = existing effect data only; B = focused extension in an existing owner. New content: 5 A / 11 B / 0 C; provisional rarity 3 Common / 8 Rare / 5 Special. No new manager, currency, operating-frame dependency or prerequisite tree.

## Exact final 4×3 roster

Each branch table has four rows of three positions. Tier letters describe manufacturing progression; current finalized prices are listed below and in EQUIPMENT_ECONOMY.md.

### Shared

| Row | Position | Implemented ID / Korean name | Role and identity | Level progression / MAX payoff | Class / rarity / economy tier | Prerequisite | Synergy | Overlap risk |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `shared_cargo_bay` / 확장 적재실 (retained) | Salvage — Carry more on each expedition. | Lv1 cargo reserve; Lv2 more; Lv3 largest increment. MAX: Largest dedicated cargo reserve. | A / Common / A | None | salvage_protocol; return_container | Bulkhead/lightweight cargo hybrids remain legacy alternatives. |
| 1 | 2 | `shared_salvage_magnet` / 회수 자석 (retained) | Salvage — Collect loose rewards with less detouring. | Pickup reach increases at all three levels. MAX: Maximum dedicated pickup reach. | A / Common / A | None | cargo_bay; moving combat | Not harvest yield; overlaps legacy scrap_sorter/collection_route. |
| 1 | 3 | `shared_engine_tuning` / 엔진 튜닝 (retained) | Mobility — Traverse and sidestep reliably on any ship. | Three unchanged movement-speed increments. MAX: Full sustained movement benefit. | A / Common / A | None | salvage_magnet; deliberate positioning | No dash benefit; legacy movement/cargo hybrids remain recognized. |
| 2 | 1 | `shared_lightweight_frame` / 경량 프레임 | Structural — mobility specialist | Max Lv1 at deployment; exact fusion table above | B / Common / B | First analysis | Other structural modules resolve a different profile | Low HP/cargo; no individual-effect stacking |
| 2 | 2 | `shared_standard_frame` / 표준 프레임 | Structural — stable work frame | Max Lv1 at deployment; exact fusion table above | B / Common / B | First analysis | Other structural modules resolve a different profile | Best structural damage/yield; no penalty |
| 2 | 3 | `shared_heavy_frame` / 중갑 프레임 | Structural — durable expedition frame | Max Lv1 at deployment; exact fusion table above | B / Common / B | First analysis | Other structural modules resolve a different profile | Mobility tradeoff; no count scaling |
| 3 | 1 | `shared_radar_amplifier` / 레이더 증폭기 (retained) | Tactical — Plan routes using a broader successful scan. | Three unchanged scan-radius increments. MAX: Largest standalone Shared scan reach. | A / Common / C | None | taunt_resonator; stealth_scan | No discovery-rule change; focus_lens remains Lancer combat/intel hybrid. |
| 3 | 2 | `shared_dash_capacitor` / 대쉬 캐패시터 (retained) | Mobility — Make the existing dash available more often. | Three unchanged flat cooldown reductions. MAX: Full dash availability without longer displacement. | A / Common / C | None | ship dash signatures; engine_tuning | Not dash distance; return_protocol is a legacy hybrid. |
| 3 | 3 | `shared_cutting_ammo` / 절단 탄약 (retained) | Salvage — Break salvage efficiently while retaining modest combat support. | Harvest-object damage plus small general damage each level. MAX: Full mining specialization with broad supporting offense. | A / Rare / C | None | cargo_bay; salvage_protocol | Medium: MG sustained harvest / SG harvest burst are stronger family packages; monitor stacking. |
| 4 | 1 | `shared_return_container` / 복귀 보관함 (retained) | Recovery — Preserve more cargo through the existing emergency-return rule. | Lv1 preservation; Lv2 more; Lv3 strongest increment. MAX: Maximum dedicated emergency-preservation ceiling. | A / Rare / D | None | cargo_bay; active_cooler | Does not trigger return or save all cargo; return_protocol remains a legacy hybrid. |
| 4 | 2 | `shared_active_cooler` / 액티브 냉각기 (retained) | Tactical — Build around repeat use of the equipped Reinforcement. | Three multiplicative reductions in remaining recharge time. MAX: Full existing active-recharge benefit. | A / Rare / D | None | chosen Reinforcement; return planning | No extra charges or equipped-slot changes; separate from dash cooldown. |
| 4 | 3 | `shared_periodic_reflector` / 반사 방벽 (retained) | Signature — Hold a ready reflector until an eligible bullet starts its window. | Lv1 existing reflection; Lv2 shorter recharge; Lv3 shortest recharge. MAX: Existing 14-second recharge; window and supported bullets unchanged. | A / Special / D | None | plating; close-range survival | Not constant immunity, laser reflection or a new shield resource. |

### Sweeper

| Row | Position | Implemented ID / Korean name | Role and identity | Level progression / MAX payoff | Class / rarity / economy tier | Prerequisite | Synergy | Overlap risk |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `mg_traverse_servo` / 횡행 서보 (new) | Mobility — Sidestep while keeping rapid rounds easy to place. | Lv1 small move speed; Lv2 projectile speed; Lv3 stronger movement plus modest delivery speed. MAX: Comfortable mobile pressure without a second firing mode. | A / Common / A | None | stable_feed; salvage_sweep | Medium: engine_tuning and projectile-speed legacy items; benefits are modest and paired. |
| 1 | 2 | `mg_guidance_control` / 유도 제어 (retained) | Control — Improve ordinary homing acquisition and steering. | Lv1 angle/range; Lv2 larger; Lv3 strongest increments. MAX: Complete the existing prerequisite for Terminal Guidance. | A / Rare / A | None | terminal_guidance; target_distributor | Guidance controls flight; distribution controls initial target assignment. |
| 1 | 3 | `mg_stable_feed` / 안정 급탄 (retained) | Control — Keep sustained fire grouped and regular. | Spread reduction plus cadence; both steps grow toward Lv3. MAX: Full accuracy/cadence package. | A / Common / A | None | sustained_harvest_fire; twin_feed | Does not grant movement or heat relief; removed Shared spread/cadence items remain legacy. |
| 2 | 1 | `mg_sustained_harvest_fire` / 지속 수확 사격 (retained) | Offense — Maintain fire while cutting through salvage. | Cadence and harvest-object damage at all three levels. MAX: Complete sustained-fire/mining package. | A / Rare / B | None | heat_exchanger; cargo_bay | Medium: cutting_ammo is broad mining support, without this cadence identity. |
| 2 | 2 | `mg_salvage_sweep` / 수확 탄막 (retained) | Salvage — Deliver rounds and collect nearby salvage efficiently. | Projectile speed and pickup reach at every level. MAX: Full delivery/collection utility while fighting. | A / Rare / B | None | sustained_harvest_fire; traverse_servo | Despite its name, it adds neither extra bullets nor harvest yield. |
| 2 | 3 | `mg_midrange_pressure` / 중거리 압박 (retained) | Offense — Maintain damage over a longer firing lane. | Range and general damage at every level. MAX: Full existing reach/pressure package. | A / Rare / B | None | line_penetrator; guidance_control | Not heat, rate or spread; overlap with legacy range/damage is intentional specialization. |
| 3 | 1 | `mg_heat_exchanger` / 열교환 급탄기 (new) | Control — Use short firing pauses to recover sustained pressure. | Lv1 modest cooling rate; Lv2 shorter cooling-start delay; Lv3 stronger cooling with a bounded delay floor. MAX: Short controlled pauses become a practical heat-management rhythm. | B / Rare / C | None | sustained_harvest_fire; twin_feed | Avoid turning Max into infinite firing or immunity to overheat. |
| 3 | 2 | `mg_line_penetrator` / 관통 급탄기 (new) | Control — Pressure aligned targets through the first contact. | Lv1 one extra pierce; Lv2 modest projectile speed; Lv3 stronger delivery speed and a small range benefit. MAX: A reliable through-line of bullets without unrestricted penetration. | A / Rare / C | None | midrange_pressure; twin_feed | Lancer retains deeper pierce/falloff removal; no falloff-removal effect here. |
| 3 | 3 | `mg_target_distributor` / 표적 분배기 (new) | Control — Spread manually aimed fire across a small enemy cluster. | Lv1 choose a different eligible target for successive fire events; Lv2 modest cone tolerance; Lv3 avoid the last two targets when alternatives exist. MAX: Readable cycling pressure rather than every round choosing the same nearest enemy. | B / Special / C | None | guidance_control; terminal_guidance | High: aim assistance and homing overlap; tightly bound to the player aim cone and line of sight. |
| 4 | 1 | `mg_terminal_guidance` / 종말 유도 (retained) | Signature — Evolve ordinary guidance into persistent terminal pursuit. | Conditional Lv1 tracking mode; Lv2/3 add angle and range. MAX: Complete terminal acquisition/retention with existing Lv3 support. | A / Special / D | `mg_guidance_control` at current-run Lv3; conditional acquisition | guidance_control; target_distributor | True evolution only; no free deployment until its runtime prerequisite is met. |
| 4 | 2 | `mg_dash_missile_salvo` / 추격 미사일 사출 (retained) | Signature — A successful dash launches existing tracking missiles. | Lv1 existing three-missile salvo; Lv2/3 general damage support. MAX: Existing salvo with full supporting damage, also affecting the main gun. | A / Special / D | None | dash_capacitor; mobile pressure | No new missile count, cooldown reduction or chained salvos promised. |
| 4 | 3 | `mg_twin_feed` / 병렬 급탄기 (new) | Signature — Sustained fire develops a periodic rapid paired-feed rhythm. | Lv1 baseline grouping; Lv2 sustained grouping; Lv3 periodic rapid follow-up. MAX: Readable six-volley / paired-follow-up rhythm with normal heat and pooling. | B / Special / D | None | stable_feed; heat_exchanger; line_penetrator | Heat and output tuning risk; does not use an always-on projectile-count bonus. |

### Breacher

| Row | Position | Implemented ID / Korean name | Role and identity | Level progression / MAX payoff | Class / rarity / economy tier | Prerequisite | Synergy | Overlap risk |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `sg_choke_barrel` / 산탄 압축 (retained) | Control — Concentrate the existing pellet pattern. | Three increasingly strong spread-reduction increments. MAX: Maximum existing equipment choke, respecting the weapon aim-choke floor. | A / Rare / A | None | extra_pellet; slug_coupler | No new aiming mode; current weapon already has aim-responsive choke. |
| 1 | 2 | `sg_extra_pellet` / 추가 펠릿 (retained) | Offense — Add one pellet, then control the denser volley. | Lv1 one extra pellet; Lv2/3 spread reduction. MAX: Existing extra-pellet pattern with improved grouping. | A / Special / A | None | choke_barrel; impact_ejector | Current Special rarity retained despite a simple foundation role; Max does not add further pellets. |
| 1 | 3 | `sg_breaching_drive` / 돌입 구동계 (retained) | Mobility — Reach the engagement and withstand commitment. | Dash distance and HP at every level. MAX: Full existing displacement/health package. | A / Rare / A | None | breach_compensator; overpressure | No new on-dash attack or immunity; vector_thruster remains legacy. |
| 2 | 1 | `sg_close_harvest_burst` / 근접 수확 파쇄 (retained) | Salvage — Break salvage with shotgun volleys. | Harvest-object and modest general damage at all levels. MAX: Full family harvesting package. | A / Rare / B | None | cargo_bay; salvage_protocol | Name does not impose a distance rule; only Overpressure currently does that. |
| 2 | 2 | `sg_taunt_resonator` / 도발 공진기 (retained) | Tactical — Use Radar to draw enemies into a chosen engagement. | Taunt duration plus movement at every level. MAX: Full existing taunt/reposition package. | A / Special / B | None | radar_amplifier; pellet_penetrator | Uses actual Radar taunt; not a shot-triggered stun or threat manager. |
| 2 | 3 | `sg_cycle_actuator` / 순환 구동기 (new) | Offense — Recover between deliberate shotgun blasts sooner. | Lv1 small cadence benefit; Lv2 moderate; Lv3 strongest increment. MAX: A dependable follow-up cadence without extra pellets. | A / Common / B | None | choke_barrel; sustained close engagements | Medium: breach_sequencer is a dash-gated single opportunity, not permanent cadence. |
| 3 | 1 | `sg_pellet_penetrator` / 관통 산탄 (new) | Control — Push a cone of pellets through a shallow crowd. | Lv1 one extra pierce; Lv2 modest delivery speed; Lv3 stronger speed without another pierce increment. MAX: Reliable shallow crowd penetration while preserving spread. | A / Rare / C | None | taunt_resonator; extra_pellet | Medium: MG lane penetration and Lancer deep pierce are different weapon geometries; limit pellet proliferation. |
| 3 | 2 | `sg_impact_ejector` / 충격 배출기 (new) | Control — A well-placed central pellet opens a small gap. | Lv1 central pellet displaces its first eligible enemy; Lv2 modestly stronger displacement; Lv3 bounded reliable clearance. MAX: Predictable space creation from aim, without a stun or radial blast. | B / Rare / C | None | choke_barrel; taunt_resonator | Medium: existing dash pushes nearby targets; this is a precise shot result and can push targets out of Overpressure range. |
| 3 | 3 | `sg_breach_compensator` / 돌입 보정기 (new) | Survival — Remain less exposed just after a committed dash ends. | Lv1 brief modest post-dash reduction; Lv2 stronger reduction; Lv3 full bounded reduction in the same short window. MAX: A reliable landing window, never permanent immunity. | B / Rare / C | None | breaching_drive; close-range firing | Shared plating is capacity; this is a conditional damage rule. Existing dash invulnerability is not extended. |
| 4 | 1 | `sg_close_quarters_overpressure` / 근접 과압 (retained) | Offense — Commit to close enemy hits for distance-scaled pressure. | Existing 18/18/24 percentage-point increments. MAX: Existing +60% cap at close distance, falling to zero by 4 units. | A / Special / D | None | breaching_drive; breach_compensator | Strong capstone specialization, not falsely described as a new explosion or stagger. |
| 4 | 2 | `sg_slug_coupler` / 단일탄 결합기 (new) | Signature — Put a readable penetrating center into the shotgun pattern. | Lv1 combine the two center-nearest pellet allocations into one slug; Lv2 improve slug delivery; Lv3 complete slug reach/penetration specialization. MAX: A hybrid center-line shot and outer pellet screen. | B / Special / D | None | choke_barrel; impact_ejector; extra_pellet | High: do not replace Lancer long-range charge identity or multiply the already combined damage twice. |
| 4 | 3 | `sg_breach_sequencer` / 돌입 시퀀서 (new) | Signature — Use a dash to set up a deliberate two-blast breach. | Lv1 one short follow-up opportunity after the first post-dash blast; Lv2 more usable arm window; Lv3 stronger bounded recovery reduction. MAX: An aimed breach double-tap rather than an automatic duplicate shot. | B / Special / D | None | breaching_drive; overpressure; cycle_actuator | Medium-high cadence overlap: strong but conditional; no unlimited zero-interval loop. |

### Lancer

| Row | Position | Implemented ID / Korean name | Role and identity | Level progression / MAX payoff | Class / rarity / economy tier | Prerequisite | Synergy | Overlap risk |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | `sn_charge_accelerator` / 차징 가속 (retained) | Offense — Reach the chosen charge level sooner. | Existing increasingly strong charge-speed increments. MAX: Full charge-time reduction using reciprocal speed semantics. | A / Rare / A | None | high_output_core; mobile_charge_coupler | Changes charge accumulation, not post-shot delay or damage. |
| 1 | 2 | `sn_piercing_amplifier` / 관통 증폭 (retained) | Control — Reward lining enemies up. | Lv1 extra pierce; Lv2 MAX extra pierce and falloff removal. MAX: Existing Max2 retains damage after penetration. | A / Rare / A | None | ballistic_alignment; charge_aperture | Keep Max2; do not invent a third level or another falloff-removal item. |
| 1 | 3 | `sn_ballistic_alignment` / 탄도 정렬기 (new) | Control — Place deliberate shots along longer, faster lanes. | Lv1 modest speed; Lv2 modest range; Lv3 balanced stronger delivery and reach. MAX: Reliable long-range shot placement without extra damage. | A / Common / A | None | piercing_amplifier; anchor_optics | Medium: identical primitives to legacy Shared longshot_stabilizer; retained legacy is an alternate, not deleted. |
| 2 | 1 | `sn_focus_lens` / 집중 렌즈 (retained) | Tactical — Combine a wider scan with deliberate charged damage. | Radar radius plus charged damage at each level. MAX: Full existing information/output hybrid. | A / Rare / B | None | stealth_scan; high_output_core | Not camera zoom or reticle prediction; anchor_optics affects view presentation only. |
| 2 | 2 | `sn_high_output_core` / 고출력 탄심 (retained) | Offense — Invest upgrades in the charged shot itself. | Existing 12/15/18 charged-damage increments. MAX: Largest dedicated charged-damage specialization. | A / Rare / B | None | charge_accelerator; reserve_capacitor | No automatic multishot or charge preservation. |
| 2 | 3 | `sn_mobile_charge_coupler` / 기동 차징기 (new) | Mobility — Reposition while building the next deliberate shot. | Lv1 ease the existing moving-charge slowdown; Lv2 more; Lv3 approach stationary charge speed. MAX: Charging movement feels deliberate without becoming faster than stationary charging. | B / Rare / B | None | charge_accelerator; dash_echo_shot | No new ability to move while charging: the current weapon already allows it. |
| 3 | 1 | `sn_stealth_scan` / 침투 프로토콜 (retained) | Tactical — Combine passive anti-radar infiltration with Radar threat readouts. | Lv1 current radar cloak/state readout; Lv2 vision cones; Lv3 visual-detection delay, plus existing duration increments. MAX: Existing staged infiltration behavior fully unlocked. | A / Special / C | None | focus_lens; radar_amplifier | Radar cloak is passive while active; readouts require the Radar panel. It is not invisibility from all detection. |
| 3 | 2 | `sn_charge_aperture` / 차징 확장기 (new) | Control — Make a charged lane more forgiving without multiplying shots. | Lv1 modest charged-projectile width; Lv2 more; Lv3 full bounded width. MAX: A readable wider charged lane that helps catch aligned enemies. | B / Rare / C | None | piercing_amplifier; ballistic_alignment | High readability/collision risk: no damage/range bonus, wall bypass, splash or enlarged enemy hitboxes. |
| 3 | 3 | `sn_anchor_optics` / 정위 조준경 (new) | Tactical — See farther along the aim direction when setting up a stationary charge. | Lv1 modest stationary view assist; Lv2 more; Lv3 capped long-lane view with clear reticle. MAX: A stable long-range planning view without combat or discovery authority. | B / Rare / C | None | ballistic_alignment; piercing_amplifier | Medium: Focus Lens is radar coverage/damage; this affects only the existing charging camera assist. |
| 4 | 1 | `sn_semi_auto_laser` / 레이저 반복기 (retained) | Signature — Short clicks add an alternate projectile firing option. | Lv1 current hybrid mode; Lv2/3 cadence increments. MAX: Existing fast-click alternative while charged fire remains. | A / Special / D | None | ballistic_alignment; charge_accelerator | It is a pooled projectile, not hitscan; preserve minimum-charge/fast-click handling. |
| 4 | 2 | `sn_dash_echo_shot` / 위상 잔상 사격 (retained) | Signature — The dash origin echoes the next qualifying sniper shot. | Lv1 current 40%-power snapshot echo; Lv2/3 charged-damage support. MAX: Existing positional follow-up with full support scaling. | A / Special / D | None | dash_capacitor; high_output_core | No additional echoes or recursive shot rewards; later damage also affects normal charged shots. |
| 4 | 3 | `sn_reserve_capacitor` / 잔류 축전기 (new) | Signature — A fully charged shot leaves a limited head start for one follow-up. | Lv1 small reserved charge; Lv2 moderate reserve; Lv3 strong but sub-full reserve. MAX: A deliberate full-charge / faster-follow-up rhythm. | B / Special / D | None | high_output_core; mobile_charge_coupler | High: can compound with charge speed; bound below full charge and never re-arm from the assisted follow-up. |

## New module values and recipes

All values below are **incremental per acquired level**. Existing percentage consumers keep their multiplicative/divisive composition; percentage-point mode modifiers explicitly add. Existing equipment values/rarities remain unchanged. New combat values and rarity remain integration tuning. The Scrap/Core column now reflects the calibrated manufacturing recipes. No universal debuffs were added.

| New ID / Korean name | Class | Rarity | Lv1 increment | Lv2 increment | Lv3 MAX increment | Scrap / Core |
|---|---|---|---|---|---|---|
| `mg_traverse_servo` / 횡행 서보 | A | Common | MoveSpeedPercent 3 | ProjectileSpeedPercent 8 | MoveSpeedPercent 4; ProjectileSpeedPercent 10 | 12 / 0 |
| `mg_heat_exchanger` / 열교환 급탄기 | B | Rare | MachineGunCoolingRatePercent 20 | MachineGunCoolingDelayReduction 0.05 | MachineGunCoolingRatePercent 30; MachineGunCoolingDelayReduction 0.05 | 28 / 1 |
| `mg_line_penetrator` / 관통 급탄기 | A | Rare | PierceCountBonus 1 | ProjectileSpeedPercent 10 | ProjectileSpeedPercent 12; RangePercent 5 | 30 / 0 |
| `mg_target_distributor` / 표적 분배기 | B | Special | MachineGunTargetDistribution 1 | MachineGunTargetDistribution 1 | MachineGunTargetDistribution 1 | 36 / 1 |
| `mg_twin_feed` / 병렬 급탄기 | B | Special | MachineGunTwinFeed 1 | MachineGunTwinFeed 1 | MachineGunTwinFeed 1 | 54 / 2 |
| `sg_cycle_actuator` / 순환 구동기 | A | Common | FireRatePercent 5 | FireRatePercent 7 | FireRatePercent 10 | 18 / 0 |
| `sg_pellet_penetrator` / 관통 산탄 | A | Rare | PierceCountBonus 1 | ProjectileSpeedPercent 8 | ProjectileSpeedPercent 12 | 30 / 0 |
| `sg_impact_ejector` / 충격 배출기 | B | Rare | ShotgunImpactDisplacement 0.35 | ShotgunImpactDisplacement 0.2 | ShotgunImpactDisplacement 0.25 | 32 / 1 |
| `sg_breach_compensator` / 돌입 보정기 | B | Rare | DashDamageReductionPercent 10 | DashDamageReductionPercent 5 | DashDamageReductionPercent 5 | 30 / 1 |
| `sg_slug_coupler` / 단일탄 결합기 | B | Special | ShotgunSlugCoupler 1 | ShotgunSlugCoupler 1 | ShotgunSlugCoupler 1 | 54 / 2 |
| `sg_breach_sequencer` / 돌입 시퀀서 | B | Special | ShotgunBreachSequence 1 | ShotgunBreachSequence 1 | ShotgunBreachSequence 1 | 52 / 2 |
| `sn_ballistic_alignment` / 탄도 정렬기 | A | Common | ProjectileSpeedPercent 8 | RangePercent 8 | ProjectileSpeedPercent 10; RangePercent 10 | 10 / 0 |
| `sn_mobile_charge_coupler` / 기동 차징기 | B | Rare | SniperMovingChargeBonus 10 | SniperMovingChargeBonus 10 | SniperMovingChargeBonus 20 | 22 / 1 |
| `sn_charge_aperture` / 차징 확장기 | B | Rare | ChargedProjectileSizePercent 15 | ChargedProjectileSizePercent 10 | ChargedProjectileSizePercent 15 | 30 / 1 |
| `sn_anchor_optics` / 정위 조준경 | B | Rare | ChargeSightBonusPercent 10 | ChargeSightBonusPercent 10 | ChargeSightBonusPercent 15 | 32 / 1 |
| `sn_reserve_capacitor` / 잔류 축전기 | B | Special | SniperReserveCapacitor 10 | SniperReserveCapacitor 5 | SniperReserveCapacitor 10 | 54 / 2 |

### Focused runtime contracts

- **Heat Exchanger:** existing MachineGun cooling changes from 55 to 66/66/82.5 heat per second; cooling delay becomes 0.25/0.20/0.15 seconds. It does not disable heat or change ownership.
- **Target Distributor:** bounded 32-contact / 16-obstacle queries choose a visible hostile within a 6/9/12-degree aim cone, at most 12 units and within weapon range. Skip the last target (last two at MAX); no valid replacement means manual aim. LOS, neutral shop and allied exclusions remain. Homing, if independently equipped, can retain the initial chosen target.
- **Twin Feed — approved A -> B adjustment:** Lv1 6% local spread reduction. After four successful regular volleys, Lv2 uses 14% and MAX uses 18%. MAX adds one rapid follow-up after every sixth regular volley, delayed by max(0.025s, one quarter of the current fire interval). Each volley uses the existing projectile count/pool and heat charge. Release, overheat, swap, disable and modifier reset cancel pending follow-ups. No always-on extra projectile or separate fire timer manager.
- **Impact Ejector:** one designated central projectile carries 0.35/0.55/0.80 units of first-effective-enemy-hit displacement. It consumes that source once, invokes existing IKnockbackReceiver, respects its resistance, and never injects a Rigidbody impulse. Other pellets cannot multiply the displacement.
- **Breach Compensator:** completed Shotgun dash grants 10/15/20% damage reduction for 0.6s, before Armor consumption. Cancelled dash, disable/death or another active weapon does not retain that window.
- **Slug Coupler:** replaces two central pellets with one 2x-pellet-damage central slug; other pellets survive. Slug speed multipliers 1.05/1.15/1.20, pierce +1/+1/+2; MAX slug range x1.10. Width x1.5. Total base volley damage is conserved. No second shotgun implementation.
- **Breach Sequencer:** a completed Shotgun dash arms one successful shot for 0.8/1.25/1.25s. That shot's following recovery interval is x0.75/x0.75/x0.60, floor 0.12s. It does not reset an existing cooldown or automatically shoot. Source serial prevents repeated arming from one dash.
- **Mobile Charge Coupler:** moving-charge rate 0.6 -> 0.7/0.8/1.0; stationary rate unchanged. Existing Sniper Tick and cancellation own timing.
- **Charge Aperture:** full-charge projectile width x1.15/x1.25/x1.40; intermediate charge interpolates from x1. Normal and semi-auto tap shots remain base width. Width belongs to the shot snapshot, so Dash Echo reproduces it once and pooled reuse restores authored scale.
- **Anchor Optics:** existing stationary charge camera-assist excess above x1 is multiplied by 1.10/1.20/1.35, capped at x2.5. No Radar discovery/range or projectile range effect. Movement/cancel uses existing camera reset.
- **Reserve Capacitor:** a successful full, unassisted charged shot banks 10/15/25% of the next charge for up to 3s. Consume once on next charge start; actual held time still governs minimum charge/tap behavior. Assisted shots cannot re-bank; cancel, expiry, swap, disable and modifier reset clear reserve. Echo never banks it.

Typed values 40–48 are appended to TraitEffectType: MachineGunCoolingRatePercent, MachineGunCoolingDelayReduction, MachineGunTargetDistribution, MachineGunTwinFeed, ShotgunImpactDisplacement, ShotgunSlugCoupler, ShotgunBreachSequence, SniperMovingChargeBonus, SniperReserveCapacitor. Existing reserved values DashDamageReductionPercent (18), ChargeSightBonusPercent (20), ChargedProjectileSizePercent (21) now have consumers. No earlier enum value moved.

PlayerWeaponModifiers is the shared reversible modifier owner; RunTraitEffectApplier and PlayerRuntimeStatApplier both route these effects there. MachineGunWeapon, ShotgunWeapon, SniperWeapon, PlayerDash, PlayerHealth and Bullet own their existing local execution paths. TraitEffectTextUtility and build-status text cover every new effect. Staged mode text reports the attained mode, not an incorrect sum of independent mode names.

### Ownership, research and legacy compatibility

Research still opens 3/6/9/12 positions **per available branch**, with Breacher opening after first analysis and Lancer after second. Third analysis remains independent of Route Core assembly. There is no global fitting cap. Normal matching fitted modules deploy at Lv1 from the immutable RunContext snapshot, after store reset; player reconstruction reapplies attained levels once without reseeding or restoring depleted HP/Armor/Active Reinforcement. Run ending clears runtime levels, not manufacturing/fitting.

Save version 7 adds an idempotent research-grandfather list. Version-6 or earlier manufactured IDs retain their old legitimate research tier when moved later; stable per-definition previousDevelopmentResearchTier metadata records that old boundary. Normal ship gates remain, so backward F10 checkpoints disable locked branches without erasing purchases. New purchases use the final row. Missing catalog defers migration; unknown IDs remain diagnosed and preserved. No ownership is fabricated beyond the existing v5 fitted/purchased migration; no currency is charged/refunded.

The seventeen non-roster Shared definitions remain recognized for existing owners and usable through the compact legacy section. They have no normal blueprint, research or manufacturing action for new players. The latest three retired positions are shared_salvage_protocol, shared_reinforced_plating and shared_repair_foam. No assets/IDs/GUIDs were deleted; no refunds or automatic frame substitutions occur. Earlier legacy transitions remain preserved.

Terminal Guidance alone remains conditional: fitted but unowned until mg_guidance_control reaches Lv3 and normal acquisition succeeds. No new prerequisites. Starting deployment levels retain nonrefundable provenance; earned upgrades keep existing field-drop/dismantling treatment. No empty/maxed pool reopens the master catalog.

## Legacy Shared ownership

The 2026-09-25 conversion also retires `shared_salvage_protocol`, `shared_reinforced_plating` and `shared_repair_foam` from the board. Existing ownership, fitting, IDs, GUIDs and effects remain; no refund or automatic frame replacement is made.

| ID | Korean name | Policy |
|---|---|---|
| `shared_vector_thruster` | 벡터 추진기 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_targeting_bus` | 조준 버스 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_accelerator_coil` | 가속 코일 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_range_focusing` | 사거리 초점화 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_rapid_feed` | 급속 급탄 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_combat_gyro` | 전투 자이로 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_scrap_sorter` | 스크랩 분류기 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_bulkhead_cargo_frame` | 격벽 적재 프레임 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_collection_route` | 회수 항로 계산기 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_longshot_stabilizer` | 장거리 안정기 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_survival_protocol` | 생존 프로토콜 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_lightweight_cargo` | 경량 적재함 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_return_protocol` | 복귀 프로토콜 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |
| `shared_standard_upgrade` | 표준 개수 키트 | Existing ownership/fitting usable; no normal blueprint/manufacturing; no refund. |

## Icon TODOs

All sixteen IDs in the new-module value table require dedicated equipment icons. No unrelated icon was copied or claimed as final art.

### Historical 2026-09-22 validation and remaining tuning

Historical 2026-09-22 validation (superseded catalog counts): static inspection confirmed 66 unique asset references/IDs/GUIDs: 62 ordinary + three boss + Pixel Curse. Final development positions are 12/12/12/12 with zero pending; fourteen Shared modules remain outside the board. All fifty previous definitions retain ID, GUID, name, category, rarity, MaxLevel, LevelEffects and prerequisites. No production scene or prefab is edited; the existing authored four-tab, twelve-card board resolves final metadata through the catalog.

Runtime and Editor/test static compilation passed. Unity asset-only authoring and localization validation executed successfully. Historical recipe policy is superseded by the 2026-09-23 economy calibration; all 48 current prices are listed below. Existing owners retain their purchases without a refund or retroactive charge. Dedicated icons remain unassigned for all sixteen new items; the existing generic equipment fallback is intentional.

Roster implementation baseline: 641/641 EditMode tests passed. The later manufacturing economy pass passed 693/693; see EQUIPMENT_ECONOMY.md for scope.

Fixed-seed diagnostic, 64 trials per fitted size, three choices, random selection, starting at Lv1 (Terminal Guidance conditional). Each trial continues to exhaustion. These are upgrade-only opportunities, not a forecast of natural run pacing. Exhausted/no-offer counts below are intentional final empty pools; premature empty = 0. The 6/12-item samples contain no Special item, so specialMax=0 means not applicable. Production weighting is unchanged.

```text
FINAL_REWARD count=6 trials=64 firstMax=3.59 thirdMax=7.81 specialMax=0.00 upgradeOffers=768/832 exhausted=64/832 prematureEmpty=0
FINAL_REWARD count=12 trials=64 firstMax=4.84 thirdMax=9.64 specialMax=0.00 upgradeOffers=1536/1600 exhausted=64/1600 prematureEmpty=0
FINAL_REWARD count=18 trials=64 firstMax=6.20 thirdMax=12.02 specialMax=26.13 upgradeOffers=2304/2368 exhausted=64/2368 prematureEmpty=0
FINAL_REWARD count=24 trials=64 firstMax=6.23 thirdMax=12.42 specialMax=42.28 upgradeOffers=3127/3200 exhausted=64/3200 prematureEmpty=0
```

Actual 480×270 Play Mode smoke completed through Boot, Settlement manufacture/fitting, all four researched boards, three ship deployments, new equipment upgrades to MAX, real weapon firing/pooling, portal handoffs with depleted vitals, and return/relaunch Lv1 reset. This automated smoke is not natural-play balance validation.
Unity exited 0; 263 smoke assertions passed and 23 screenshots were captured at 480×270. No captured runtime errors/assertions. The temporary harness and its meta were removed after execution.

Human tuning remains: sustained-fire/heat rhythm; displacement and slug readability in crowds; charge-reserve feel alongside Semi-Auto and Dash Echo; late Special MAX pacing; natural-run affordability; dedicated icons. The separate 2026-09-22 Operating Frame implementation is superseded by the three manufactured Shared Row-2 modules above. New post-ending Curse systems and universal equipment debuffs remain absent. Manufacturing recipes are now calibrated; natural-play economy validation remains open.

## Source ownership

Assets are authored by `EquipmentFinalRosterAuthoring.Apply` through AssetDatabase, without opening Settlement or rebuilding UI. `EquipmentDevelopmentInstaller` delegates metadata to the same final mapping so rerunning it cannot resurrect the provisional roster. Existing row moves and the three Shared substitutions are intentional, not category/ID changes.

Rejected/deferred for this roster: ricochet chains, elemental/status frameworks, swarm projectile cloning, extra resource economies and post-ending Curse challenges. Structural frame effects use the existing player bootstrap owner and three Shared Row-2 equipment positions; no separate frame-selection authority remains.



## Final manufacturing recipes (2026-09-23)

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

See [Equipment Economy](EQUIPMENT_ECONOMY.md) for supply, starter grant, before/after prices and validation. Structural frames use the Row-2 recipes and exact provisional combination profiles above.
