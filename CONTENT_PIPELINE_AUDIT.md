# Trait and Reinforcement Content Audit

Trait audit updated: 2026-09-21, current working tree. Reinforcement sections retain their 2026-08-16 audit scope. `S/R/F` means normal Shop / random Reward / generated Field pickup eligibility. The retained Reinforcement audit covered 41 definitions after its identity-completion passes and had static-only validation. Current equipment validation, including rendered Play Mode, is recorded in the Trait inventory below.

## Counts and release gate

| Content | Authored | Normal obtainable | Direct/special only | Runtime-full | Partial identity | Disabled/deferred assets |
|---|---:|---:|---:|---:|---:|---:|
| Traits | 50 | 46 | 4 | 50 | 0 | 0 |
| Reinforcements | 41 | 40 | 1 | 41 | 0 | 0 |

Trait split: Shared normal 26, Machine Gun 7, Shotgun 6, Sniper 7, hidden boss 3, persistent story/Curse 1. Reinforcement counts below are retained from their separate audit. Five unsupported Trait enum capabilities have no authored assets; all effects used by the current normal catalog have existing runtime consumers.

## TraitEffectType implementation matrix

| Effect | Runtime implementation | Supported systems | Status |
|---|---|---|---|
| DamagePercent | `RunTraitEffectApplier` / `PlayerRuntimeStatApplier` -> `PlayerWeaponModifiers` | all weapons | Full |
| ProjectileSpeedPercent | same -> projectile setup | all projectile weapons | Full |
| RangePercent | same -> projectile range | all projectile weapons | Full |
| MoveSpeedPercent | appliers -> `PlayerController2D` | player movement | Full |
| DashCooldownReduction | appliers -> `PlayerDash.SetDashCooldown` | dash | Full |
| DashDistanceBonus | appliers -> `PlayerDash.SetDashDistance` | dash | Full |
| MaxHpBonus | appliers -> `PlayerHealth` | health | Full |
| HealEfficiencyPercent | `PlayerRuntimeBonusState.ApplyHealAmount` | reward healing and Reinforcement healing | Full |
| PickupRangeBonus | runtime bonus -> `RewardPickup` | run pickups | Full |
| SpreadReductionPercent | weapon modifiers -> firing spread | all applicable weapons | Full |
| ProjectileCountBonus | weapon modifiers -> `ShotgunWeapon` | shotgun | Full |
| PierceCountBonus | weapon modifiers -> projectile setup | sniper/projectiles | Full |
| ChargeTimeReductionPercent | weapon modifiers -> `SniperWeapon` | sniper charge | Full |
| ChargeDamagePercent | weapon modifiers -> `SniperWeapon` | sniper charge | Full |
| HomingAngleBonus | weapon modifiers -> `Bullet` homing | homing projectiles | Full |
| HomingRangeBonus | weapon modifiers -> `Bullet` homing | homing projectiles | Full |
| FireRatePercent | weapon modifiers -> weapon cadence | all weapons | Full |
| CloseRangeDamageReductionPercent | none | none | Unsupported; no asset |
| DashDamageReductionPercent | none | none | Unsupported; no asset |
| CloseRangeSuppressionPercent | none | none | Unsupported; no asset |
| ChargeSightBonusPercent | none | none | Unsupported; no asset |
| ChargedProjectileSizePercent | none | none | Unsupported; no asset |
| RemovePierceDamageFalloff | runtime appliers -> `PlayerWeaponModifiers` -> projectile snapshot | sn_piercing_amplifier Max2 | Full; authored Max payoff |
| CargoCapacityBonus | runtime bonus -> `PlayerCargoController` | cargo | Full |
| HarvestYieldPercent | runtime bonus -> `RewardPickup` | harvested currency | Full |
| HarvestObjectDamagePercent | weapon setup -> `Bullet` -> `HarvestObjectHealth` | harvesting damage | Full |
| EmergencyReturnCapacityRatioBonus | runtime bonus -> cargo/run return calculation | emergency return | Full |
| RadarScanRadiusBonus | runtime bonus -> `PlayerRadarScanner` | radar | Full |
| ActiveCooldownReductionPercent | runtime bonus -> `PlayerReinforcementController` | recharge and HUD timing | Full |
| RadarTauntDurationBonus | runtime bonus -> `RadarTarget` -> `EnemyBaseAI.ApplyRadarTaunt` | shotgun radar taunt | Full |
| RadarStealthDurationBonus | runtime bonus -> `PlayerStealthController` | sniper radar stealth | Full |
| SectorBarrierProtocol | `BossPassiveRuntimeController` | hidden boss passive | Full |
| MatterReconstructorProtocol | `BossPassiveRuntimeController` | hidden boss passive | Full |
| PhaseAfterimageProtocol | `BossPassiveRuntimeController` | hidden boss passive | Full |
| SniperSemiAutoMode | `SniperWeapon` | sniper alternate mode | Full |
| ShotgunCloseRangeDamagePercent | `PlayerWeaponModifiers` -> `Bullet` | enemy-only distance scaling | Full |
| MachineGunTerminalGuidance | MG projectile snapshot -> `Bullet` | terminal tracking | Full |
| PeriodicReflectiveShield | `PlayerPeriodicReflector2D` | same-source recharge configuration | Full |
| MachineGunDashMissileSalvo | `PlayerMachineGunDashMissileSalvo` | successful MG dash | Full |
| SniperDashEchoShot | `PlayerSniperDashEchoShot` | next-shot snapshot echo | Full |

## Trait inventory — equipment audit, 2026-09-21

**Correction pass 1:** The global loadout-capacity and ordinary-Lv0 rules used by the earlier reward pacing analysis below are superseded.
Development now uses per-branch blueprint research, one-time manufacture, unrestricted compatible fitting and ordinary Lv1 deployment.
The catalog's roles, incremental combat values and rarity remain unchanged. See [development mapping](Docs/Design/EQUIPMENT_DEVELOPMENT_MAPPING.md)
for all 32 implemented / 16 pending positions, fourteen retained legacy Shared definitions, exact recipe metadata and the conditional Terminal Guidance exception.
The CSV now includes branch/tier/position, participation, one-time recipe and deployment fields. Earlier random averages below are historical, not validation of Lv1 deployment pacing.


The current catalog has **50 unique referenced definitions**: 46 normal prepared-run
equipment (26 Shared, 7 Sweeper/MG, 6 Breacher/Shotgun, 7 Lancer/Sniper), plus three hidden
campaign boss traits and Pixel Curse. Existing seventh MG/Sniper entries are the dash
signatures; no item was removed to force the older six-per-family count. Rarity totals
remain Common 17 / Rare 20 / Special 12 / Curse 1. No ID, GUID, name, rarity, exposure,
prerequisite or serialized enum value changed.

The complete per-definition inventory is [EQUIPMENT_CATALOG_AUDIT.csv](Docs/Design/EQUIPMENT_CATALOG_AUDIT.csv):
46 rows with exact asset path, current display name, family, rarity, MaxLevel, each level's
effects, role, overlap, eligibility and concise description. This replaces the stale
45-entry table rather than maintaining two independent number lists.

Primary roles: Salvage/Economy 13; Offense 8; Control/Accuracy 7; Signature 6;
Tactical/Radar 5; Survival 4; Mobility 3. A primary role does not exclude a supporting role.

### Overlap decisions

- Cargo and plating specialists were being overtaken by dual-stat cargo/health items.
  Their last levels now finish at +32 cargo / +8 HP; dual-stat packages retain their broader utility.
- Pure damage, cadence and spread compete with standard upgrade, cutting ammo,
  stable feed and sustained harvest. Strengthen their finishing step without removing
  the distinct Rare/ship-specific packages. Sweeper's sustained-harvest package retains
  a stronger rate bonus plus mining synergy; specialization/rarity remain relevant.
- Guidance is acquisition/steering; terminal guidance is its prerequisite-gated behavior.
  Stable feed is accuracy plus cadence; midrange pressure trades that for reach/damage.
  Salvage sweep and sustained harvest serve pickup/projectile delivery and mining fire respectively.
- Shotgun choke is pattern concentration; extra pellet adds one projectile then supports
  that pattern. Overpressure scales enemy-only damage by distance; close harvest burst
  improves mining. Breaching drive and taunt retain survival and control roles.
- Sniper charge speed, output, pierce retention, radar/focus, stealth, semi-auto and dash
  echo retain separate purposes. Stealth already stages its controller behavior over levels;
  it did not need another mechanic or numeric buff.
- Movement/pickup, range/projectile speed, healing/HP and emergency-return hybrids remain
  for broad builds. Pure emergency preservation gains a modest stronger finish.
- No definitions retired, new equipment added, or universal drawbacks introduced.

### Incremental semantics and exact data changes

`RunTraitEffectApplier` applies only the newly acquired level; reconstruction applies
levels 1 through the saved run level once each. Damage/fire rate/range/projectile speed/
charged damage compound. Spread and active-cooldown reductions multiply remaining values.
Homing/counts/cargo and emergency-preservation percentage points add. Charge-speed bonuses
divide charge time by `(1 + bonus/100)`; a +25% speed entry reduces time by 20%, not 25%.
Reflector recharge is a replacement setting on one source, never a sum of seconds.

All 46 normal descriptions now explain role without repeating the level table.
19 definitions change level data; seven former Max1 definitions become Max3. Max2
Sniper pierce remains Max2. Values below are serialized increments/settings, **not totals**.

| Stable ID / exact asset | Before (newly acquired level entries) | After (newly acquired level entries) |
|---|---|---|
| [`shared_cargo_bay`](Assets/02_Scripts/Config/TraitDefinition/Common/01_shared_cargo_bay.asset) | L1: CargoCapacityBonus 10 / L2: CargoCapacityBonus 10 / L3: CargoCapacityBonus 10 | L1: CargoCapacityBonus 8 / L2: CargoCapacityBonus 10 / L3: CargoCapacityBonus 14 |
| [`shared_reinforced_plating`](Assets/02_Scripts/Config/TraitDefinition/Common/04_shared_reinforced_plating.asset) | L1: MaxHpBonus 2 / L2: MaxHpBonus 2 / L3: MaxHpBonus 2 | L1: MaxHpBonus 2 / L2: MaxHpBonus 2 / L3: MaxHpBonus 4 |
| [`shared_targeting_bus`](Assets/02_Scripts/Config/TraitDefinition/Common/09_shared_targeting_bus.asset) | L1: DamagePercent 5 / L2: DamagePercent 5 / L3: DamagePercent 5 | L1: DamagePercent 5 / L2: DamagePercent 6 / L3: DamagePercent 8 |
| [`shared_rapid_feed`](Assets/02_Scripts/Config/TraitDefinition/Common/12_shared_rapid_feed.asset) | L1: FireRatePercent 5 / L2: FireRatePercent 5 / L3: FireRatePercent 5 | L1: FireRatePercent 5 / L2: FireRatePercent 6 / L3: FireRatePercent 8 |
| [`shared_combat_gyro`](Assets/02_Scripts/Config/TraitDefinition/Common/13_shared_combat_gyro.asset) | L1: SpreadReductionPercent 8 / L2: SpreadReductionPercent 8 / L3: SpreadReductionPercent 8 | L1: SpreadReductionPercent 8 / L2: SpreadReductionPercent 10 / L3: SpreadReductionPercent 14 |
| [`shared_return_container`](Assets/02_Scripts/Config/TraitDefinition/Common/15_shared_return_container.asset) | L1: EmergencyReturnCapacityRatioBonus 5 / L2: EmergencyReturnCapacityRatioBonus 5 / L3: EmergencyReturnCapacityRatioBonus 5 | L1: EmergencyReturnCapacityRatioBonus 4 / L2: EmergencyReturnCapacityRatioBonus 5 / L3: EmergencyReturnCapacityRatioBonus 7 |
| [`mg_guidance_control`](Assets/02_Scripts/Config/TraitDefinition/MachineGun/27_mg_guidance_control.asset) | L1: HomingAngleBonus 8; HomingRangeBonus 1.5 / L2: HomingAngleBonus 8; HomingRangeBonus 1.5 / L3: HomingAngleBonus 8; HomingRangeBonus 1.5 | L1: HomingAngleBonus 6; HomingRangeBonus 1 / L2: HomingAngleBonus 8; HomingRangeBonus 1.5 / L3: HomingAngleBonus 10; HomingRangeBonus 2 |
| [`mg_stable_feed`](Assets/02_Scripts/Config/TraitDefinition/MachineGun/28_mg_stable_feed.asset) | L1: SpreadReductionPercent 8; FireRatePercent 5 / L2: SpreadReductionPercent 8; FireRatePercent 5 / L3: SpreadReductionPercent 8; FireRatePercent 5 | L1: SpreadReductionPercent 6; FireRatePercent 4 / L2: SpreadReductionPercent 8; FireRatePercent 5 / L3: SpreadReductionPercent 10; FireRatePercent 6 |
| [`sg_choke_barrel`](Assets/02_Scripts/Config/TraitDefinition/Shotgun/31_sg_choke_barrel.asset) | L1: SpreadReductionPercent 12 / L2: SpreadReductionPercent 12 / L3: SpreadReductionPercent 12 | L1: SpreadReductionPercent 10 / L2: SpreadReductionPercent 12 / L3: SpreadReductionPercent 16 |
| [`sg_extra_pellet`](Assets/02_Scripts/Config/TraitDefinition/Shotgun/32_sg_extra_pellet.asset) | L1: ProjectileCountBonus 1 | L1: ProjectileCountBonus 1 / L2: SpreadReductionPercent 4 / L3: SpreadReductionPercent 6 |
| [`sg_close_quarters_overpressure`](Assets/02_Scripts/Config/TraitDefinition/Shotgun/46_sg_close_quarters_overpressure.asset) | L1: ShotgunCloseRangeDamagePercent 60 | L1: ShotgunCloseRangeDamagePercent 18 / L2: ShotgunCloseRangeDamagePercent 18 / L3: ShotgunCloseRangeDamagePercent 24 |
| [`sn_charge_accelerator`](Assets/02_Scripts/Config/TraitDefinition/Sniper/36_sn_charge_accelerator.asset) | L1: ChargeTimeReductionPercent 10 / L2: ChargeTimeReductionPercent 10 / L3: ChargeTimeReductionPercent 10 | L1: ChargeTimeReductionPercent 8 / L2: ChargeTimeReductionPercent 10 / L3: ChargeTimeReductionPercent 14 |
| [`sn_piercing_amplifier`](Assets/02_Scripts/Config/TraitDefinition/Sniper/37_sn_piercing_amplifier.asset) | L1: PierceCountBonus 1 / L2: PierceCountBonus 1 | L1: PierceCountBonus 1 / L2: PierceCountBonus 1; RemovePierceDamageFalloff 1 |
| [`sn_high_output_core`](Assets/02_Scripts/Config/TraitDefinition/Sniper/39_sn_high_output_core.asset) | L1: ChargeDamagePercent 15 / L2: ChargeDamagePercent 15 / L3: ChargeDamagePercent 15 | L1: ChargeDamagePercent 12 / L2: ChargeDamagePercent 15 / L3: ChargeDamagePercent 18 |
| [`sn_semi_auto_laser`](Assets/02_Scripts/Config/TraitDefinition/Sniper/44_sn_semi_auto_laser.asset) | L1: SniperSemiAutoMode 1 | L1: SniperSemiAutoMode 1 / L2: FireRatePercent 6 / L3: FireRatePercent 9 |
| [`mg_terminal_guidance`](Assets/02_Scripts/Config/TraitDefinition/MachineGun/47_mg_terminal_guidance.asset) | L1: MachineGunTerminalGuidance 1 | L1: MachineGunTerminalGuidance 1 / L2: HomingAngleBonus 6; HomingRangeBonus 0.5 / L3: HomingAngleBonus 10; HomingRangeBonus 0.75 |
| [`shared_periodic_reflector`](Assets/02_Scripts/Config/TraitDefinition/Common/48_shared_periodic_reflector.asset) | L1: PeriodicReflectiveShield 15 | L1: PeriodicReflectiveShield 20 / L2: PeriodicReflectiveShield 17 / L3: PeriodicReflectiveShield 14 |
| [`mg_dash_missile_salvo`](Assets/02_Scripts/Config/TraitDefinition/MachineGun/49_mg_dash_missile_salvo.asset) | L1: MachineGunDashMissileSalvo 1 | L1: MachineGunDashMissileSalvo 1 / L2: DamagePercent 4 / L3: DamagePercent 6 |
| [`sn_dash_echo_shot`](Assets/02_Scripts/Config/TraitDefinition/Sniper/50_sn_dash_echo_shot.asset) | L1: SniperDashEchoShot 1 | L1: SniperDashEchoShot 1 / L2: ChargeDamagePercent 5 / L3: ChargeDamagePercent 8 |

Examples of correct accumulated payoff: damage/cadence specialist 1.05×1.06×1.08 =
1.20204 (previously 1.157625); guidance totals remain +24 degrees / +4.5 range;
Overpressure still totals at most +60% at close range; high-output core is
1.12×1.15×1.18 = 1.51984 (previously 1.520875). Supporting signature scalars reuse
existing consumers: missile damage also improves the main gun, charged echo damage
also improves the original shot. No unrelated Max perk or new gameplay executor was added.

### Source-specific reward rules

Production generation is unchanged. Ordinary candidates still require the captured
prepared loadout, ship compatibility, existing prerequisites and room below Max.
`RunRewardChoiceGenerator` chooses rarity tiers (65/30/5 Common/Rare/Special) then
uniform candidates within a tier; field drops use `EffectiveRandomDropWeight`
(100/45/8, Curse 0). Shop candidate sampling remains uniform over its permitted stock pool.
Tuning only offers owned/upgradable prepared equipment. Containers retain their
rare-first preference and permitted fallback. Boss Mixed rewards retain the strict
Rare floor and may offer Reinforcements if the prepared trait pool has no eligible Rare.
Persistent story and forced campaign rewards keep their separate authority.

### Deterministic reward diagnostics

The diagnostic uses the real `RunRewardChoiceGenerator`, prerequisite/loadout filter and
`RunRuntimeTraitStore`, with 64 fixed seeds per case, up to 24 Trait-only reward opportunities
and three offered cards. It measures random selection and a separate upgrade-first policy;
the latter models deliberate specialization rather than a mandatory composition guarantee.
Tuning offers are probed but not granted. Opportunities do not represent minutes or a natural run's reward frequency.

| Prepared items | Random: first Max | Random: owned at opportunity 12 | Random: offer includes upgrade | Upgrade-first: first Max | Upgrade-first: owned at 12 | Upgrade-first: offer includes upgrade |
|---:|---:|---:|---:|---:|---:|---:|
| 3 | 5.141 | 3.000 | 85.4% | 3.000 | 3.000 | 66.7% |
| 6 | 7.047 | 5.500 | 88.2% | 4.375 | 4.000 | 66.7% |
| 9 | 9.250 | 6.719 | 87.2% | 5.750 | 4.938 | 66.7% |
| 12 | 9.484 | 7.156 | 85.4% | 6.016 | 5.125 | 64.2% |

Invalid/no-option offers while an eligible item remained: **zero** in all eight cases.
All 3/6-item trials exhaust their fully upgraded pools within the 24-opportunity window
(9/18 total acquisitions); this is expected completion, not a failed offer. No 9/12-item
trial exhausts all equipment within that window. Before this pass, random first-Max means
were 5.141 / 7.047 / 7.063 / 7.422; extending one-level signatures mainly affects the larger pools.
These broad diagnostics do not establish combat balance or whether a natural run awards too many upgrades.

The nested Sweeper pools are: cargo, stable feed, guidance; then plating, engine,
salvage protocol; then sustained harvest, return container, reflector; then terminal
guidance, radar amplifier, dash missiles. Terminal guidance remains unavailable until
Guidance Lv3. An incompatible or prerequisite-incomplete loadout is not silently repaired by reward generation.

### Executed validation and remaining balance questions

Runtime and Editor/test static compilation: zero errors (52 / 4 existing warnings).
Focused equipment/campaign/localization selection: 166/166. Full EditMode: 598/598.
Normal Localization Import/Validate succeeded. Catalog checks cover references, IDs,
roles, every normal level, all effect labels, actual incremental acquisition, Max clamp,
shop/container/boss/tuning eligibility and dynamic authored UI rows. Existing save,
campaign, F10, dialogue/archive and Route Core regressions remain green.

Rendered 480x270 Boot -> Settlement -> Expedition checks used isolated QA save data:
3-item and 12-item prepared pools started unowned, ordinary Guidance and Reflector
acquired through the normal API to Max, and the same-source reflector settings were
20/17/14 seconds. Across 64 sampled three-card draws, visible candidate variety was
3 versus 11 (terminal guidance correctly prerequisite-gated). Stable Feed and Reflector
detail screenshots were reviewed; no Upgrade action returned. Scenes/prefabs were not changed.

Remaining human judgment: reward frequency over real run lengths; one-level signature
entry strength versus supporting later increments; specialist/hybrid stacking; Max2
pierce-retention power; reflector downtime under dense fire; 480x270 text comfort for
all equipment/languages. Automated sampling and API-driven smoke are not deep combat playtesting.

## ReinforcementEffectType implementation matrix

| Effect | Executor / dependency | Duration owner | Recharge owner | UI source | Status |
|---|---|---|---|---|---|
| HealFlat | controller -> `PlayerHealth`, heal efficiency applied | Instant | controller | charges/recharge | Full |
| AddArmor | controller -> `PlayerArmor` | Persistent armor state | controller | charges/recharge | Full |
| AddInvincibleTime | controller -> `PlayerHealth` | PlayerHealth timer | controller | controller timed status | Full |
| ClearEnemyProjectiles | controller non-alloc overlap -> `PoolManager.Release` | Instant | controller | charges/recharge | Full |
| DamageNearbyEnemies | controller non-alloc overlap -> `EnemyHealth` | Instant | controller | charges/recharge | Full |
| KnockbackNearbyEnemies | controller non-alloc overlap -> `IKnockbackReceiver` | Instant | controller | charges/recharge | Full |
| TemporaryDamagePercent | controller timed modifier -> weapon modifiers | controller timestamp | controller | same timed status | Full |
| TemporaryFireRatePercent | controller timed modifier -> weapon modifiers | controller timestamp | controller | same timed status | Full |
| TemporaryMoveSpeedPercent | controller timed modifier -> player movement | controller timestamp | controller | same timed status | Full |
| TemporaryDashCooldownReductionPercent | controller timed modifier -> player dash | controller timestamp | controller | same timed status | Full |
| SpawnPrefabAtPlayer | controller -> PoolManager (Destroy fallback) | prefab lifetime | controller | same timed status | Full; turrets/support drones |
| EmergencyReturn | controller -> `EmergencyReturnController` | hold/return flow | special, no recharge | equipped state | Full special |
| TemporaryEnemyRadarJamming | `PlayerStealthController` subscribed to `Used` | stealth controller timestamp | controller | controller timed status | Full |
| RevealEnemyVisionAndState | `PlayerStealthController` / enemy vision consumers | stealth controller timestamp | controller | controller timed status | Full |
| RevealRadarTargets | scanner pulse -> source-owned `RadarTarget` reveal | RadarTarget source expiry | controller | charges/recharge | Full |
| DisruptEnemyTracking | controller -> source-owned `EnemyBaseAI` tracking disruption | EnemyBaseAI source expiry | controller | controller timed status | Full |
| TemporaryScrapGainPercent | controller -> source-owned `PlayerRuntimeBonusState` Scrap multiplier | controller timestamp | controller | same timed status | Full |
| ConvertCreditsToHealing | atomic `RunManager.TrySpendCredits` -> heal-efficiency-aware `PlayerHealth.Heal` | Instant | controller after valid transaction | charges/recharge | Full |
| TemporaryHomingAngleBonus | controller -> `PlayerWeaponModifiers` -> projectile snapshot | controller timestamp | controller | same timed status | Full |
| TemporaryHomingRangeBonus | controller -> `PlayerWeaponModifiers` -> projectile snapshot | controller timestamp | controller | same timed status | Full |
| ClearEnemyProjectilesInCone | non-alloc radius query + aim-angle filter -> pooled enemy Bullet release | Instant | controller | charges/recharge | Full |
| DamageEnemiesInCone | non-alloc radius query + aim-angle filter -> hostile damage | Instant | controller | charges/recharge | Full |
| TemporaryPierceCountBonus | controller -> `PlayerWeaponModifiers` -> projectile snapshot | controller timestamp | controller | same timed status | Full |
| TemporaryRemovePierceDamageFalloff | controller -> reference-counted weapon modifier -> projectile snapshot | controller timestamp | controller | same timed status | Full hook; no authored Reinforcement currently uses it |
| PlaceOrReturnToMarker | controller-owned two-stage state -> pooled marker -> safe `PlayerController2D` reposition | marker exists until return/cleanup | charge/recharge begins after return | charge state plus world marker | Full |

## Current Reinforcement closure verification (2026-08-16)

- Authored definitions: 41. Catalog references: 41. Unique catalog references: 41. Unique IDs: 41.
- Catalog gaps/nulls/duplicates: 0. Authored definitions missing from catalog: 0.
- Compatibility split: Any 26, Machine Gun 5, Shotgun 5, Sniper 5; every value is within the current metadata enum.
- Icons: 41 assigned, 0 placeholder/null detected by serialized reference.
- Full gameplay identity: 41. Partial identity: 0. Player-obtainable no-op identity: 0.
- Unsupported `ReinforcementEffectType` usage: 0. Serialized types in use are all handled by the current controller or their typed runtime consumer.
- Zero recharge: only `rf_emergency_return_anchor`; it is the direct/default special Emergency Return and does not participate in normal random/shop pools.
- `rf_return_marker`: scene-local two-stage marker, 1 logical charge, 38-second recharge beginning only after successful return. It does not complete or settle a run.
- Normal Sniper pierce retention remains 1.0. The briefly introduced 0.80 baseline had no authored weapon, projectile, Trait-description, or design-document support. `rf_sn_pierce_compressor` therefore keeps a focused +2 temporary pierce-count identity without adding a new base-weapon penalty.

## Historical Reinforcement inventory (2026-08-14, superseded)

The table below is retained only as the original audit trail. Its old effect summaries and “Partial identity” labels are superseded by the current closure verification above and by the serialized assets/runtime code.

Every entry starts with full charges, so initial charges equal maximum. “Active” below is the longest serialized timed effect used by the HUD. All entries except Emergency Return are S/R/F eligible and pass compatibility filtering.

| Asset path | ID / display | Compatibility | Rarity / type | Charges / recharge / active | Effects | Exposure | Icon | Status / safe |
|---|---|---|---|---|---|---|---|---|
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/00_rf_emergency_return_anchor.asset` | `rf_emergency_return_anchor` / 긴급복귀 앵커 | Any special | Common / Active | 1 / 0s / none | EmergencyReturn | Default/direct only | Assigned | Full / safe special |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/01_rf_emergency_repair_kit.asset` | `rf_emergency_repair_kit` / 긴급 수리 키트 | Any | Common / Rechargeable | 1 / 18s / none | Heal 8 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/02_rf_large_repair_pack.asset` | `rf_large_repair_pack` / 대형 수리 팩 | Any | Common / Rechargeable | 1 / 18s / none | Heal 14 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/03_rf_shield_cell.asset` | `rf_shield_cell` / 보호막 셀 | Any | Common / Rechargeable | 1 / 18s / none | Armor 8 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/04_rf_burst_barrier.asset` | `rf_burst_barrier` / 순간 방벽 | Any | Common / Rechargeable | 1 / 18s / 4s | Invincible 4s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/05_rf_emp_bomb.asset` | `rf_emp_bomb` / EMP 폭탄 | Any | Common / Rechargeable | 1 / 18s / none | Clear r5; damage 8 r5 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/06_rf_projectile_cleaner.asset` | `rf_projectile_cleaner` / 탄막 청소기 | Any | Common / Rechargeable | 2 / 16s / none | Clear r4 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/07_rf_magnetic_burst.asset` | `rf_magnetic_burst` / 자기 방출기 | Any | Common / Rechargeable | 2 / 12s / 3s | Move +30% 3s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/08_rf_overdrive_injector.asset` | `rf_overdrive_injector` / 과부하 주입기 | Any | Common / Active | 1 / 18s / 6s | Damage +25%, move +20%, 6s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/09_rf_coolant_canister.asset` | `rf_coolant_canister` / 냉각제 캐니스터 | Any | Common / Active | 1 / 18s / 8s | Fire rate +30% 8s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/10_rf_thruster_ampoule.asset` | `rf_thruster_ampoule` / 추진 앰플 | Any | Common / Rechargeable | 2 / 14s / 4s | Move +40% 4s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/11_rf_dash_coolant.asset` | `rf_dash_coolant` / 대쉬 냉각제 | Any | Rare / Rechargeable | 2 / 18s / 5s | Dash cooldown -45% 5s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/12_rf_decoy_beacon.asset` | `rf_decoy_beacon` / 미끼 비콘 | Any | Rare / Active | 1 / 24s / none | Knockback 2.2 r4 | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/13_rf_gravity_mine.asset` | `rf_gravity_mine` / 중력 기뢰 | Any | Rare / Active | 1 / 24s / none | Damage 5 and knockback 1.6 r4 | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/14_rf_auto_turret_pod.asset` | `rf_auto_turret_pod` / 자동 포탑 포드 | Any | Epic / Active | 1 / 30s / 10s | Allied auto turret 10s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/15_rf_repair_drone.asset` | `rf_repair_drone` / 수리 드론 | Any | Rare / Active | 1 / 25s / 1s | Heal 6; support drone 1s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/16_rf_guard_drone.asset` | `rf_guard_drone` / 호위 드론 | Any | Epic / Active | 1 / 30s / 2s | Clear r4; invincible 2s; support drone 2s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/17_rf_phase_cloak.asset` | `rf_phase_cloak` / 위상 은폐장 | Any | Rare / Active | 1 / 24s / 5s | Invincible 5s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/18_rf_armor_foam.asset` | `rf_armor_foam` / 장갑 폼 분사기 | Any | Common / Rechargeable | 2 / 20s / none | Armor 5 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/19_rf_core_stabilizer.asset` | `rf_core_stabilizer` / 코어 안정기 | Any | Epic / Active | 1 / 30s / 3s | Invincible 3s; armor 5 | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/20_rf_scanner_pulse.asset` | `rf_scanner_pulse` / 스캐너 펄스 | Any | Rare / Rechargeable | 2 / 18s / 5s | Move +25% 5s | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/21_rf_hunter_jammer.asset` | `rf_hunter_jammer` / 추적 교란기 | Any | Rare / Rechargeable | 1 / 24s / none | Knockback 2.4 r5; clear r4 | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/22_rf_warp_flare.asset` | `rf_warp_flare` / 워프 조명탄 | Any | Rare / Rechargeable | 1 / 24s / 3s | Move +45% 3s; invincible 2s | S/R/F | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/23_rf_scrap_compressor.asset` | `rf_scrap_compressor` / 스크랩 압축기 | Any | Rare / Active | 1 / 24s / 8s | Damage +20% 8s | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/24_rf_credit_converter.asset` | `rf_credit_converter` / 크레딧 변환기 | Any | Rare / Active | 1 / 30s / none | Heal 7 | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Common/25_rf_return_marker.asset` | `rf_return_marker` / 귀환 표식기 | Any | Legendary / Active | 1 / 38s / 4s | Invincible 3s; move +30% 4s | S/R/F | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/26_rf_mg_cooling_core.asset` | `rf_mg_cooling_core` / 기관총 냉각 코어 | MG | Epic / Active | 1 / 20s / 8s | Fire rate +35%, damage +10%, 8s | S/R/F MG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/27_rf_mg_guidance_drone.asset` | `rf_mg_guidance_drone` / 유도 보조 드론 | MG | Epic / Active | 1 / 24s / 10s | Damage +20%; support drone 10s | S/R/F MG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/28_rf_mg_suppression_turret.asset` | `rf_mg_suppression_turret` / 제압 포탑 | MG | Epic / Active | 1 / 30s / 12s | Allied burst turret 12s | S/R/F MG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/29_rf_mg_ammo_capacitor.asset` | `rf_mg_ammo_capacitor` / 탄약 캐패시터 | MG | Rare / Rechargeable | 2 / 18s / 6s | Damage +15%, fire rate +20%, 6s | S/R/F MG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/MachineGun/30_rf_mg_lockon_array.asset` | `rf_mg_lockon_array` / 락온 배열기 | MG | Epic / Active | 1 / 28s / 8s | Damage +30% 8s | S/R/F MG | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Shotgun/31_rf_sg_breach_shield.asset` | `rf_sg_breach_shield` / 돌입 보호막 | SG | Rare / Rechargeable | 2 / 20s / 2s | Invincible 2s; armor 4 | S/R/F SG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Shotgun/32_rf_sg_cone_emp.asset` | `rf_sg_cone_emp` / 원뿔 EMP | SG | Epic / Rechargeable | 1 / 30s / none | Clear r5; damage 8 r4 | S/R/F SG | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Shotgun/33_rf_sg_taunt_beacon.asset` | `rf_sg_taunt_beacon` / 도발 비콘 | SG | Epic / Active | 1 / 30s / none | Knockback 3 r5 | S/R/F SG | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Shotgun/34_rf_sg_impact_booster.asset` | `rf_sg_impact_booster` / 충격 증폭기 | SG | Rare / Rechargeable | 2 / 16s / 4s | Damage +60% 4s | S/R/F SG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Shotgun/35_rf_sg_escape_thruster.asset` | `rf_sg_escape_thruster` / 이탈 추진기 | SG | Rare / Active | 1 / 18s / 3s | Invincible 2s; move +45% 3s | S/R/F SG | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Sniper/36_rf_sn_focus_lens.asset` | `rf_sn_focus_lens` / 집중 렌즈 장비 | SN | Rare / Active | 1 / 22s / 8s | Damage +35% 8s | S/R/F SN | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Sniper/37_rf_sn_rail_overcharge.asset` | `rf_sn_rail_overcharge` / 레일 과충전기 | SN | Legendary / Rechargeable | 1 / 38s / 4s | Damage +100% 4s | S/R/F SN | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Sniper/38_rf_sn_scan_marker.asset` | `rf_sn_scan_marker` / 저피탐 침투 모듈 | SN | Epic / Active | 2 / 24s / 10s | Radar jam and reveal 10s | S/R/F SN | Assigned | Full / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Sniper/39_rf_sn_stasis_anchor.asset` | `rf_sn_stasis_anchor` / 정지 앵커 | SN | Epic / Active | 1 / 30s / none | Knockback 3 r4 | S/R/F SN | Assigned | Partial identity / safe |
| `Assets/02_Scripts/Config/ReinforcementDefinition/Sniper/40_rf_sn_pierce_compressor.asset` | `rf_sn_pierce_compressor` / 관통 압축기 | SN | Legendary / Active | 1 / 26s / 8s | Damage +40% 8s | S/R/F SN | Assigned | Partial identity / safe |

## Integrity, acquisition, and icon coverage

- Trait catalog: 45 references, no nulls, duplicate references, or duplicate IDs. Resources fallback is disabled. Hidden/story Traits remain lookup-visible but shared metadata filters block normal selection.
- Reinforcement catalog: 41 references, no nulls, duplicate references, or duplicate IDs. Resources fallback is disabled. Compatibility is metadata-based.
- Normal Trait entry points checked: shop stock, reward definitions/capsules, level-up choices, generated field pickups, owned-trait upgrades, and direct pickup acquisition. Owned-trait upgrades now also honor `CanAppearAsLevelUpTrait`, preventing hidden boss passives from leaking after campaign grants.
- Normal Reinforcement entry points checked: shop stock/purchase, reward definitions/capsules, generated field pickups, field swap, maintenance storage, dismantle, and emergency default assignment. `CanAppearInShop`, `CanRandomDrop`, emergency exclusion, and weapon compatibility are shared catalog filters.
- Trait assigned/final: IDs 01-40 (40 assets). Placeholder: 44 (`38_sn_focus_lens` icon). Null: 41, 42, 43, and 45 Pixel Curse.
- Reinforcement assigned/final: all 41. Placeholder: none. Null: none.
- No authored effect-list/max-level mismatch or unambiguous numeric description/data mismatch was found. The remaining 12 “partial identity” Reinforcements explicitly describe their current executable proxy effect, so they do not advertise a no-op.

## Charge, recharge, and active-duration findings

- All definitions have `maxCharges >= 1`; all use `startWithFullCharges`, so no initial charge exceeds max.
- One successful normal activation decrements exactly once. Recharging restores one charge per completed interval and continues until max. Swap/storage paths preserve explicit charge state; `-1` means a newly created/full item.
- Only `rf_emergency_return_anchor` has zero recharge. It is intentionally non-shop/non-drop and excluded from random selection; its hold-return path does not consume a normal charge.
- Active cooldown reduction now drives the controller’s recharge threshold, HUD ratio, and remaining-time value from the same effective duration.
- Reversible timed modifiers now use controller-owned expiry entries. Reusing the same item/effect refreshes rather than stacking an extra removal routine. Active status and gameplay modifier use the same source timestamps; active duration remains separate from recharge.
- Invincibility remains owned by `PlayerHealth`, and radar jamming/reveal remains owned by `PlayerStealthController`; the controller reports their authored durations without duplicating their gameplay timers.

## Pixel Curse regression

`45_pixel_curse.asset` remains persistent story content, Negative polarity, Curse rarity, hidden from normal shop/level/reward/drop selection, non-droppable, and non-dismantlable. Ownership uses the idempotent `story_trait:pixel_curse` unlock flag through `PermanentProgress`; save normalization prevents duplicate unlock flags. The master catalog resolves it, `PlayerVisualStateController` queries the same ownership and retains the `Px_Player` base override, and `StatusEffectHUDPresenter` emits one untimed negative slot by stable Trait ID. Its final icon remains intentionally null.

## Remaining gaps and next content gate

- Do not author Traits using the six unsupported enum values until their existing-system consumers are implemented. Recommended exact next Trait additions after implementation: `sg_close_range_plating`, `sg_dash_guard`, `sg_suppression_wave`, `sn_charge_optics`, `sn_heavy_charge_core`, and `sn_pierce_stabilizer`—one for each unsupported effect, with final IDs confirmed before asset creation.
- Recommended exact new Reinforcement additions for the immediate next task: **none**. The summoned-unit family is complete; next replace the proxy behavior of the remaining 12 partial-identity assets, beginning with the beacon/attraction family. Adding similarly named items now would duplicate content.
- Performance debt observed but not changed in this scoped audit: `Bullet.FindNearestHomingTarget()` allocates through `Physics2D.OverlapCircleAll` while homing is active; the phase-afterimage boss passive uses repeated scene-wide discovery. Both deserve dedicated profiling-safe fixes rather than an arbitrary fixed-cap buffer here.

## Final equipment roster implementation — 2026-09-22

The approved development board now has 12 Shared / 12 Sweeper / 12 Breacher / 12 Lancer real blueprints. Sixteen new ship definitions complete the catalog: 66 total, 62 ordinary, fourteen Shared legacy items outside the board and four unchanged boss/story definitions. The earlier 32-position / sixteen-pending report is superseded. Old global loadout-capacity and ordinary Lv0-preparation interpretations remain superseded.

Research -> manufacture once -> free fit/unfit -> fresh-run ordinary Lv1 -> expedition upgrades -> run reset. Completed-analysis authority, branch gates, immutable RunContext, no global fitting cap, Terminal Guidance's conditional prerequisite, nonrefundable deployment provenance and existing Active Reinforcement flow remain. Existing effects, rarity, IDs and GUIDs were preserved.

New content uses five A data-only definitions and eleven B focused extensions. Twin Feed now supplies periodic paired cadence at MAX as explicitly approved. Typed modifiers, weapon/dash/health owners, projectile snapshots and pooling implement the effects; no new manager or per-module Update. Save v7 retains previous owners' earlier research availability and preserves displaced Shared use without currency migration. The existing authored Settlement board reads the final metadata without a scene/prefab rewrite.

Values, new rarity and manufacturing recipes are provisional. All sixteen dedicated icons remain TODO; generic presentation is retained. Production reward weighting is unchanged. Frames, post-ending Curse content, universal debuffs and final economy remain unimplemented.

Detailed roster, values, recipes, migration, diagnostic evidence and validation: [EQUIPMENT_FINAL_ROSTER.md](Docs/Design/EQUIPMENT_FINAL_ROSTER.md), [EQUIPMENT_DEVELOPMENT_MAPPING.md](Docs/Design/EQUIPMENT_DEVELOPMENT_MAPPING.md), [EQUIPMENT_CATALOG_AUDIT.csv](Docs/Design/EQUIPMENT_CATALOG_AUDIT.csv).
### Validation and remaining tuning

Static catalog inspection confirms 66 unique asset references/IDs/GUIDs: 62 ordinary + three boss + Pixel Curse. Final development positions are 12/12/12/12 with zero pending; fourteen Shared modules remain outside the board. All fifty previous definitions retain ID, GUID, name, category, rarity, MaxLevel, LevelEffects and prerequisites. No production scene or prefab is edited; the existing authored four-tab, twelve-card board resolves final metadata through the catalog.

Runtime and Editor/test static compilation passed. Unity asset-only authoring and localization validation executed successfully. Existing recipes are preserved where valid, including original 1/1 and 1/3 unlock recipes; newly promoted entries without a recipe and all new definitions use the documented provisional tier convention. Dedicated icons remain unassigned for all sixteen new items; the existing generic equipment fallback is intentional.

Latest full EditMode result: 641 passed / 641 total; 0 failed.

Fixed-seed diagnostic, 64 trials per fitted size, three choices, random selection, starting at Lv1 (Terminal Guidance conditional). Each trial continues to exhaustion. These are upgrade-only opportunities, not a forecast of natural run pacing. Exhausted/no-offer counts below are intentional final empty pools; premature empty = 0. The 6/12-item samples contain no Special item, so specialMax=0 means not applicable. Production weighting is unchanged.

```text
FINAL_REWARD count=6 trials=64 firstMax=3.59 thirdMax=7.81 specialMax=0.00 upgradeOffers=768/832 exhausted=64/832 prematureEmpty=0
FINAL_REWARD count=12 trials=64 firstMax=4.84 thirdMax=9.64 specialMax=0.00 upgradeOffers=1536/1600 exhausted=64/1600 prematureEmpty=0
FINAL_REWARD count=18 trials=64 firstMax=6.20 thirdMax=12.02 specialMax=26.13 upgradeOffers=2304/2368 exhausted=64/2368 prematureEmpty=0
FINAL_REWARD count=24 trials=64 firstMax=6.23 thirdMax=12.42 specialMax=42.28 upgradeOffers=3127/3200 exhausted=64/3200 prematureEmpty=0
```

Actual 480×270 Play Mode smoke completed through Boot, Settlement manufacture/fitting, all four researched boards, three ship deployments, new equipment upgrades to MAX, real weapon firing/pooling, portal handoffs with depleted vitals, and return/relaunch Lv1 reset. This automated smoke is not natural-play balance validation.
Unity exited 0; 263 smoke assertions passed and 23 screenshots were captured at 480×270. No captured runtime errors/assertions. The temporary harness and its meta were removed after execution.

Human tuning remains: sustained-fire/heat rhythm; displacement and slug readability in crowds; charge-reserve feel alongside Semi-Auto and Dash Echo; late Special MAX pacing; long-run resource pricing; dedicated icons. Operating frames, new post-ending Curse systems, universal debuffs and final economy are intentionally absent.
