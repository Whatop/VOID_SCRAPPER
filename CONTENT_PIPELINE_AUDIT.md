# Trait and Reinforcement Content Audit

Current audit: 2026-08-16, current working tree. `S/R/F` means normal Shop / random Reward / generated Field pickup eligibility. The 41 Reinforcement definitions and their catalog were re-scanned after the identity-completion passes. Static validation was performed; Play Mode validation was not available in this environment.

## Counts and release gate

| Content | Authored | Normal obtainable | Direct/special only | Runtime-full | Partial identity | Disabled/deferred assets |
|---|---:|---:|---:|---:|---:|---:|
| Traits | 45 | 41 | 4 | 45 | 0 | 0 |
| Reinforcements | 41 | 40 | 1 | 41 | 0 | 0 |

Trait split: Shared 25, Machine Gun 5, Shotgun 5, Sniper 6, hidden boss 3, persistent story/Curse 1. Reinforcement split: Any 26 (including Emergency Return), Machine Gun 5, Shotgun 5, Sniper 5. Every normal item is mechanically safe to obtain. Six currently unsupported Trait enum capabilities have no authored asset and therefore cannot enter acquisition pools.

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
| RemovePierceDamageFalloff | runtime appliers -> `PlayerWeaponModifiers` -> projectile snapshot | projectile pierce retention; no authored Trait asset | Supported hook; unused by authored Traits |
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

## Trait inventory

All normal entries below are eligible for S/R/F, filtered by `TraitCategory` and `WeaponTree`. Every row is safe for acquisition. Effects are listed L1/L2/L3 unless otherwise stated.

| Asset path | ID / display | Class | Rarity / max | Serialized level effects | Runtime path | Exposure | Icon | Status |
|---|---|---|---|---|---|---|---|---|
| `Assets/02_Scripts/Config/TraitDefinition/Common/01_shared_cargo_bay.asset` | `shared_cargo_bay` / 확장 적재실 | Shared | Common / 3 | Cargo +10/+10/+10 | Cargo controller | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/02_shared_salvage_protocol.asset` | `shared_salvage_protocol` / 회수 프로토콜 | Shared | Common / 3 | Harvest yield +8/+8/+8% | Reward pickup | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/03_shared_salvage_magnet.asset` | `shared_salvage_magnet` / 회수 자석 | Shared | Common / 3 | Pickup range +1.2/+1.2/+1.2 | Reward pickup | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/04_shared_reinforced_plating.asset` | `shared_reinforced_plating` / 강화 장갑 | Shared | Common / 3 | Max HP +2/+2/+2 | Player health | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/05_shared_engine_tuning.asset` | `shared_engine_tuning` / 엔진 튜닝 | Shared | Common / 3 | Move +5/+5/+5% | Player movement | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/06_shared_dash_capacitor.asset` | `shared_dash_capacitor` / 대쉬 캐패시터 | Shared | Common / 3 | Dash cooldown -0.08/-0.08/-0.08s | Player dash | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/07_shared_vector_thruster.asset` | `shared_vector_thruster` / 벡터 추진기 | Shared | Common / 3 | Dash distance +0.5/+0.5/+0.5 | Player dash | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/08_shared_repair_foam.asset` | `shared_repair_foam` / 정비 폼 | Shared | Common / 3 | Heal efficiency +15/+15/+15% | Runtime bonus/heals | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/09_shared_targeting_bus.asset` | `shared_targeting_bus` / 조준 버스 | Shared | Common / 3 | Damage +5/+5/+5% | Weapon modifiers | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/10_shared_accelerator_coil.asset` | `shared_accelerator_coil` / 가속 코일 | Shared | Common / 3 | Projectile speed +8/+8/+8% | Weapon/projectile | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/11_shared_range_focusing.asset` | `shared_range_focusing` / 사거리 초점화 | Shared | Common / 3 | Range +8/+8/+8% | Weapon/projectile | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/12_shared_rapid_feed.asset` | `shared_rapid_feed` / 급속 급탄 | Shared | Common / 3 | Fire rate +5/+5/+5% | Weapon cadence | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/13_shared_combat_gyro.asset` | `shared_combat_gyro` / 전투 자이로 | Shared | Common / 3 | Spread reduction +8/+8/+8% | Weapon spread | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/14_shared_scrap_sorter.asset` | `shared_scrap_sorter` / 고철 분류기 | Shared | Rare / 3 | Yield +5% and pickup +0.6 each level | Reward pickup | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/15_shared_return_container.asset` | `shared_return_container` / 복귀 보관함 | Shared | Rare / 3 | Emergency-return capacity +5/+5/+5% | Cargo/return | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/16_shared_radar_amplifier.asset` | `shared_radar_amplifier` / 레이더 증폭기 | Shared | Common / 3 | Radar radius +2/+2/+2 | Radar scanner | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/17_shared_active_cooler.asset` | `shared_active_cooler` / 액티브 냉각기 | Shared | Rare / 3 | Active cooldown -8/-8/-8% | Runtime bonus/recharge | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/18_shared_bulkhead_cargo_frame.asset` | `shared_bulkhead_cargo_frame` / 격벽 적재 프레임 | Shared | Rare / 3 | Max HP +1 and cargo +8 each level | Health/cargo | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/19_shared_collection_route.asset` | `shared_collection_route` / 회수 항로 계산기 | Shared | Common / 3 | Move +3% and pickup +0.8 each level | Movement/pickup | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/20_shared_cutting_ammo.asset` | `shared_cutting_ammo` / 절단 탄약 | Shared | Rare / 3 | Harvest damage +12%, damage +3% each | Bullet/harvest | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/21_shared_longshot_stabilizer.asset` | `shared_longshot_stabilizer` / 장거리 안정기 | Shared | Rare / 3 | Range +6%, speed +6% each | Weapon/projectile | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/22_shared_survival_protocol.asset` | `shared_survival_protocol` / 생존 프로토콜 | Shared | Rare / 3 | Max HP +2, heal +8% each | Health/heals | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/23_shared_lightweight_cargo.asset` | `shared_lightweight_cargo` / 경량 적재함 | Shared | Rare / 3 | Cargo +8, move +3% each | Cargo/movement | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/24_shared_return_protocol.asset` | `shared_return_protocol` / 복귀 프로토콜 | Shared | Rare / 3 | Return capacity +4%, dash cooldown -0.04s each | Return/dash | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Common/25_shared_standard_upgrade.asset` | `shared_standard_upgrade` / 표준 개수 키트 | Shared | Common / 3 | Damage +4%, Max HP +1 each | Weapon/health | S/R/F | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/MachineGun/26_mg_sustained_harvest_fire.asset` | `mg_sustained_harvest_fire` / 지속 수확 사격 | MG | Rare / 3 | Fire rate +8%, harvest damage +8% each | Weapon/harvest | S/R/F MG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/MachineGun/27_mg_guidance_control.asset` | `mg_guidance_control` / 유도 제어 | MG | Rare / 3 | Homing angle +8, range +1.5 each | Bullet homing | S/R/F MG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/MachineGun/28_mg_stable_feed.asset` | `mg_stable_feed` / 안정 급탄 | MG | Common / 3 | Spread -8%, fire rate +5% each | Weapon cadence/spread | S/R/F MG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/MachineGun/29_mg_salvage_sweep.asset` | `mg_salvage_sweep` / 수확 탄막 | MG | Rare / 3 | Speed +8%, pickup +0.6 each | Projectile/pickup | S/R/F MG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/MachineGun/30_mg_midrange_pressure.asset` | `mg_midrange_pressure` / 중거리 압박 | MG | Rare / 3 | Range +6%, damage +5% each | Weapon/projectile | S/R/F MG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Shotgun/31_sg_choke_barrel.asset` | `sg_choke_barrel` / 산탄 압축 | Shotgun | Rare / 3 | Spread reduction +12/+12/+12% | Shotgun spread | S/R/F SG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Shotgun/32_sg_extra_pellet.asset` | `sg_extra_pellet` / 추가 펠릿 | Shotgun | Special / 1 | Projectile count +1 | Shotgun pellet count | S/R/F SG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Shotgun/33_sg_breaching_drive.asset` | `sg_breaching_drive` / 돌입 구동계 | Shotgun | Rare / 3 | Dash distance +0.6, Max HP +1 each | Dash/health | S/R/F SG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Shotgun/34_sg_close_harvest_burst.asset` | `sg_close_harvest_burst` / 근접 수확 파쇄 | Shotgun | Rare / 3 | Harvest damage +12%, damage +4% each | Weapon/harvest | S/R/F SG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Shotgun/35_sg_taunt_resonator.asset` | `sg_taunt_resonator` / 도발 공진기 | Shotgun | Special / 3 | Radar taunt +1s, move +3% each | Radar enemy taunt/movement | S/R/F SG | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/36_sn_charge_accelerator.asset` | `sn_charge_accelerator` / 차징 가속 | Sniper | Rare / 3 | Charge time -10/-10/-10% | Sniper charge | S/R/F SN | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/37_sn_piercing_amplifier.asset` | `sn_piercing_amplifier` / 관통 증폭 | Sniper | Rare / 2 | Pierce +1/+1 | Projectile pierce | S/R/F SN | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/38_sn_focus_lens.asset` | `sn_focus_lens` / 집중 렌즈 | Sniper | Rare / 3 | Radar radius +2, charge damage +8% each | Radar/sniper charge | S/R/F SN | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/39_sn_high_output_core.asset` | `sn_high_output_core` / 고출력 탄심 | Sniper | Rare / 3 | Charge damage +15/+15/+15% | Sniper charge | S/R/F SN | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/40_sn_stealth_scan.asset` | `sn_stealth_scan` / 침투 프로토콜 | Sniper | Special / 3 | Radar stealth +1/+1/+1s | Stealth controller | S/R/F SN | Assigned | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Sniper/44_sn_semi_auto_laser.asset` | `sn_semi_auto_laser` / 레이저 반복기 | Sniper | Special / 1 | Semi-auto mode 1 | Sniper weapon | S/R/F SN | Placeholder (focus-lens icon) | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Boss/41_boss_sector_barrier.asset` | `boss_sector_barrier` / 구획 방벽 | Shared hidden | Special / 3 | Sector protocol 1/2/3 | Boss passive runtime | Campaign direct only | Null | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Boss/42_boss_matter_reconstructor.asset` | `boss_matter_reconstructor` / 물질 재구성로 | Shared hidden | Special / 3 | Cargo +20/+10/+10; protocol 1/2/3 | Cargo/boss passive | Campaign direct only | Null | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Boss/43_boss_phase_afterimage.asset` | `boss_phase_afterimage` / 위상 잔상핵 | Shared hidden | Special / 3 | Phase protocol 1/2/3 | Boss passive runtime | Campaign direct only | Null | Full |
| `Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset` | `pixel_curse` / Pixel Curse | Shared story | Curse / 1 | No balance effect | Permanent story ownership/visual/status | Story API only | Null allowed | Full foundation |

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
