# Enemy roster and Field Base audit

Verified against the current local working tree on 2026-09-28. This supersedes the old missing-prefab claims in the pirate candidate audit. Earlier Inventory, DarkUI and Route Core work is preserved. No commit or push.

## Region 3 repeat-operation hotfix (2026-09-28)

The operation error found during the roster pass is corrected. OperationController previously treated every DeepZone2 map as a first-visit Phase Gatekeeper foundation. `ExpeditionMapGenerator.UsesRegion3PhaseGatekeeperFoundation` now exposes the existing `IsRegion3PhaseGatekeeperMap()` predicate; OperationController reads that resolved map route instead of checking depth alone. It does not query boss defeats or story ownership again.

First visits retain identified Signal Investigation, the revealed Unknown marker and completion when the encounter begins. A missing/inactive encounter on an expected first-visit foundation still logs the existing actionable error. Repeat maps return false from the special initializer and select eligible generated wreck/event/base candidates normally. The Core's repeat Raider route, FinalNetwork access, generation, reflectors, bosses, enemy counts, rewards and campaign/save authority are unchanged.

Nineteen focused operation cases cover first-visit marker/completion, route stability after progress changes, missing/inactive foundation failures, all three candidate types on repeat Region 3, and first/repeat Regions 1/2. Runtime and Editor/test compilation passed. The operation/enemy/generated-route selection passed 220/220. Full EditMode passed 899/902; the only failures are the same three pre-existing save-normalization cases listed below. Actual Play Mode kept the saved OperationController enabled: first-visit investigation completed on encounter start, and repeat seeds 401/917/2026 selected SignalInvestigation/HighValueSalvage/DefenseNetworkSabotage respectively. All 37 assertions passed, with zero missing-Phase-Gatekeeper operation errors. Each repeat map retained 44 mobile enemies, two bases, one Core and the authored Raider boss route. Evidence: `Logs/Region3Repeat/`; the disposable probe is `EnemyRosterRenderProbe.RunRegion3Operations`. This validates operation state and live generation, not a full campaign or HUD visual pass.

## Production roster

Definition assets are under `Assets/02_Scripts/Config/EnemyDefinition/`; prefab names below are under `Assets/03_Prefabs/Enemy/`. Display names below are English descriptions of the existing localized/Korean names; no localization strings were changed. `Logs/EnemyRoster/definitions-unity.txt` records the exact serialized names and paths. Numbers are the unchanged definition values, before existing region/sea-area modifiers.

| ID / display name | EnemyType | Definition / prefab | HP | Speed | Vision | Range | Interval | Attack / projectile | Reward |
|---|---|---|---:|---:|---:|---:|---:|---|---|
| `basic_enemy` / Basic | Basic | `Basic_enemy` / `Enemy_Basic` | 8 | 2.5 | 9 | 6 | 1.2 | Standard single / Enemy | 1 Credit |
| `shotgun_enemy` / Shotgun | Shotgun | `Shotgun_enemy` / `Enemy_Shotgun` | 14 | 2.5 | 10 | 8.5 | 1.8 | 5 pellets, 50-degree fan / Shotgun | 2 Credits |
| `charge_enemy` / Charging | Charging | `Charge_enemy` / `Enemy_Charge` | 12 | 2.5 | 9 | 8 | 2.5 | 1.5-second charge / ChargeShot | 3 Credits |
| `melee_charger` / Melee Charger | MeleeCharger | `Melee_Charger` / `PF_Enemy_MeleeCharger_Common` | 14 | 3.2 | 9 | 0 | 1.2 | Existing melee charge controller / no projectile | 1 Credit |
| `elite_machinegun` / Elite Machine Gun | EliteMachineGun | `Elite_MachineGun` / `Enemy_Elite 2` | 42 | 2.8 | 11 | 8 | 1.8 | MachineGunBurst / Enemy | Elite reward |
| `elite_shotgun` / Elite Shotgun | EliteShotgun | `Elite_enemy 1` / `Enemy_Elite` | 38 | 2.5 | 10 | 8 | 2.2 | StaggeredShotgun, 6 pellets, 75-degree fan / EliteSpread | Elite reward |
| `elite_charging` / Elite Charging | EliteCharging | `Elite_Charging` / `Enemy_Elite 1` | 40 | 2.4 | 12 | 10 | 3.3 | ChargingSplit, 1.2-second charge / ChargeShot + EliteSpread | Elite reward |
| `rival_harvester` / Rival Harvester | Basic | `Rival_Harvester` / `PF_Enemy_RivalHarvester` | 20 | 3.3 | 9 | 6.5 | 1.4 | Standard single, 3-degree spread / Enemy | 1 Credit + held cargo |
| `scavenger` / Scavenger | Basic | `Scavenger` / `PF_Enemy_Scavenger` | 12 | 3.8 | 8 | 5 | 1.8 | Standard single, 5-degree spread / Enemy | 1 Credit + held cargo |

Elite reward remains 1 Tuning Chip, 6 Credits, 1 Scrap, with the existing 5% chance of a 1-point heal. `EnemyHealth.ApplyDefinition` assigns the definition's reward to RewardDropper; a prefab's old inspector reward is not the runtime authority. No RewardDefinition was edited.

Projectile assets live under `Assets/02_Scripts/Config/ProjectileDefinition/` and use the `Projectile_Enemy` prefix. Damage / speed / range / lifetime: Enemy = 2 / 8 / 8 / 2; Shotgun = 1.5 / 5 / 9 / 2; ChargeShot = 8 / 16 / 14 / 3; EliteSpread = 3 / 9 / 12 / 3.5. Their Bullet prefabs and impact references are valid. Pool reuse restores enemy ownership, collision state and attack state. Values and pool implementation are unchanged.

Basic predictive chance is 0.03, Shotgun 0.02, Charging 0.6 and Elite Charging 0.5. These were inspected, not retuned. Melee retains its 0.85-second telegraph, direction lock for the last 0.2 seconds, 7-unit dash over 0.36 seconds, damage 5 and 0.7-second recovery. Actual Play Mode confirms the committed dash does not follow a subsequent player turn and a missed charge recovers.

Four other EnemyDefinition assets were inspected and intentionally retain null `EnemyPrefab` because the map does not request them:

| ID / display name | Type | HP / speed / vision / range / interval | Pattern / projectile | Spawn and reward authority |
|---|---|---|---|---|
| `shop_security_melee` / Shop security drone | ShopDrone | 18 / 3.4 / 12 / 0 / 1.2 | Melee / none | ShopDefenseController2D's explicit `PF_Enemy_MeleeCharger 1` array; Basic reward |
| `shop_turret_single` / Shop single turret | Basic | 18 / 0 / 11 / 9 / 1.1 | Standard / Enemy | Available shop attack profile, currently unused; no definition reward |
| `shop_turret_machinegun` / Shop machine-gun turret | Basic | 20 / 0 / 11 / 10 / 2 | MachineGunBurst / Enemy | Available shop attack profile, currently unused; no definition reward |
| `shop_turret_charging` / Shop charge turret | Basic | 24 / 0 / 12 / 11 / 4.2 | Standard, 1.1-second charge / ChargeShot | Available shop attack profile, currently unused; no definition reward |

All 13 IDs are unique. Shops and their separate security architecture were not changed. Legacy `Enemy_Shotgun 1`, `Enemy_Shotgun 2`, `PF_Enemy_MeleeCharger` and `PF_Enemy_MeleeCharger 1` were inspected and retained. The first two are historical role templates, not the current normal roster; `Enemy_Shotgun 1` even has Basic AI identity despite its name. It is no longer used as the Field Base guard template. The shop's numbered Melee prefab is still referenced and must not be replaced with the common pirate Melee prefab.

The saved shop's `turretAttackDefinitions` array is empty. Both shop and base turrets currently use `Assets/03_Prefabs/Turret.prefab` with its Basic definition, existing BaseTurretController targeting/power policy and no RewardDropper. The three optional shop turret profiles above are available assets, not evidence of three current production turret patterns. That shop configuration was left unchanged.

## Reference and visual corrections

- All nine production definitions already had non-null prefabs at task start. The old art audit was stale. The actual error was both Elite MG and Elite Charging pointing to the Shotgun Elite prefab despite dedicated existing variants. They now point to `Enemy_Elite 2` and `Enemy_Elite 1`; their attack-controller definition references are also corrected.
- `PF_Enemy_MeleeCharger_Common` now authors the existing EnemyMeleeChargeController2D and its AI/health/body/sprite references. Previously EnemyBaseAI added that same controller at runtime. No charge parameter changed.
- All production prefabs have EnemyBaseAI, EnemyHealth, EnemyAttackController, RadarTarget, EnemyRoleController, EnemyRoleSimulationGate, valid physics and required projectile/reward references. Rival and Scavenger have EnemyCargoHold.
- Common pirate sprites remain the approved 32x32 sheet cells: Basic 33, Shotgun 6, Charging 37, Melee 14, Defender Basic 30, Defender Shotgun 44, Rival 52, Scavenger 21. Their existing 32 PPU, Point, no-mipmap, uncompressed imports and muzzle/collider/VFX layout are retained.
- The existing 64x64 Elite silhouettes are now used correctly: MG `enemy_elite7`, Shotgun `enemy_elite4`, Charging `enemy_elite8`, also 32 PPU and Point. No image pixels, sprite imports, new enemy art or combat-prefab copies were introduced.
- RadarMarkerType remains Enemy. Existing role-marker overrides and scan/discovery visibility are retained: Defender, Rival and Scavenger use the generator's current role sprites; ordinary actors retain their enemy marker. No new radar system or hidden-objective exposure was added.
- Existing fire, hit, death, charge and alert owners remain. Actual probe logs resolve existing firing/charge/harvest/death clips; no SoundEvent ID or audio content was added. This is wiring evidence, not a subjective audio mix review.

## Combat identity and roles

EnemyType still selects combat. EnemyRoleType still selects objective/simulation behavior through EnemyRoleController.

| Role | Production setup | Verified behavior |
|---|---|---|
| Patrol | Common and Elite definition prefabs | Existing roaming, detection, attack and disengage |
| Defender | Existing `PF_Enemy_DefenderBasic` and `PF_Enemy_DefenderShotgun`; map POI targets and base guard anchors | Guards remain local and return to useful territory after breaking the leash |
| RivalHarvester | Dedicated Basic-combat definition, role setup and 14-weight cargo hold | Find salvage, harvest, collect actual drops, follow the assigned base route, physically deposit, exit along the reverse route and resume |
| Scavenger | Dedicated Basic-combat definition, role setup and 12-weight cargo hold | Approach an eligible RewardPickup, channel, take available units, flee/return and deposit; player damage/death remains authoritative |

Two narrow role bugs were fixed. A returning Defender previously alternated between generic Return and Combat when the pursuing player remained visible; role-owned Return now commands movement home before resuming Patrol. Rival target acquisition now excludes HarvestObjectHealth under FieldBaseController, so it cannot treat its own storage/prison objectives as salvage after delivering cargo.

Reservations are released on death/disable; existing target-loss cleanup and scene teardown remain. Pickup eligibility still excludes owned-cargo drops, Tuning Chips/heal and non-cargo/story authorities. The actual concurrent-pickup probe takes 4 then 3 of 7 units and rejects a third claim; pooled reuse resets availability. Campaign story components and mandatory story rewards are not generic stealable RewardPickups.

Dormant / Preview / Active gating remains: 12-second or 8-unit start grace, preview distance 26, active distance 18, active hold 8 seconds, radar preview 10 seconds, one concurrent irreversible Rival action and one Scavenger action. Bounded search cadence and existing static reservations remain. No per-helper Update, new global search, or new AI framework was added.

## Field Base encounter

The saved Expedition generator now uses an explicit A/B prefab array. The existing base placement loop cycles that authored array; shop selection remains unchanged. B is a prefab variant of A, not an independent duplicated base hierarchy.

| Base | Mobile guards | Turrets | Objectives / reward |
|---|---|---|---|
| A | 4: 2 Basic + 2 Shotgun | 3 | Existing security node, storage, prison and rescue/portal flow |
| B | 3: 2 Basic + 1 Shotgun | 4 | Same objectives, chest and reward values |

The pair previously used eight mobile guards and eight turrets. It now uses seven of each, with a guard-heavy and turret-heavy emphasis. This is the only encounter count adjustment; MapGenerationConfig and enemy/projectile/reward numbers remain unchanged. Existing anchors, root scale, walls, gates, cargo waypoints, chest and security-node locations are retained.

The original base had a null captiveNpc and null NPC portal interaction point. A nested instance of the existing `NPC_RescueContact` prefab now occupies the existing prison area, using the existing base-rescue policy; its scale compensates for the base root's 2x scale. The portal movement target is authored near the prison exit. No rescue, progression or portal owner was added.

`PF_FieldBasePowerLink` uses the existing FieldBasePowerLink2D with an authored LineRenderer, Sprites-Default material, width 0.035, wave amplitude 0.008, sorting order 3, and no collider. Each turret link starts at the security node and ends at its existing power anchor. This replaces the old corner-to-corner link assignments. The existing gate energy and turret power state still convey shutdown.

The authoritative loop remains discovery -> defended approach -> security node offline -> turrets off/gates open -> storage/prison vulnerable -> loot/rescue -> optional existing portal. Security shutdown completes the base with surviving Defenders; it does not require eliminating all guards. In Play Mode the completion event fired once, the prison released its NPC, the portal was created and interaction transferred the player through its existing destination authority.

Rival and Scavenger use the authored cargo routes and storage trigger, not long-distance deposit. The probe observed increased StoredWeight, growth/reward multiplier, cleared carrier cargo, and a route exit after delivery. The existing chest curve and `MidBossReward` asset are unchanged. In a controlled same-seed death-drop check, an empty chest emitted 16 currency units and a full chest emitted 32; reset restored multiplier 1. This checks the existing multiplier, not a new reward budget.

## Generated-map composition and placement

The unchanged MapGenerationConfig requests Basic 20, Shotgun 5, Charging 4, Melee 2, Elite MG 1, Elite Shotgun 1, Elite Charging 0. Map roles replace common slots: Basic Defender 4, Shotgun Defender 2, Rival 1, Scavenger 1. Base-owned guards are additional. Region composition modifiers remain authoritative.

Actual generated DenseDebris maps, seeds 401 / 917 / 2026, produced the following identical per-region counts. Totals include base guards and exclude seven base turrets, shop turrets and conditionally summoned shop drones.

| Actor / role | Region 1 | Region 2 | Region 3 repeat |
|---|---:|---:|---:|
| Basic Patrol | 14 | 14 | 12 |
| Shotgun Patrol | 3 | 3 | 4 |
| Charging Patrol | 4 | 5 | 6 |
| Melee Charger | 2 | 3 | 3 |
| Elite Machine Gun | 1 | 1 | 2 |
| Elite Shotgun | 1 | 1 | 2 |
| Elite Charging | 0 | 1 | 0 |
| Basic Defender, map + bases | 8 | 8 | 8 |
| Shotgun Defender, map + bases | 5 | 5 | 5 |
| RivalHarvester | 1 | 1 | 1 |
| Scavenger | 1 | 1 | 1 |
| **Total mobile actors** | **40** | **43** | **44** |

Each of nine maps produced both base variants and all requested roles. Minimum player-start distances were 23.16-31.24, 19.34-24.13 and 20.24-23.63 respectively, exceeding the existing 18-unit safe radius. Minimum actor separation across the sample was 1.21, above the configured 1.2. No sampled actor overlapped a blocking collider at its intended arrival destination. Deferred arrival positions are checked instead of transient arrival-animation positions.

The initial diagnostic found enemies inside extended wreck footprints despite valid center-point spacing. Production enemy placement now synchronizes transforms once and uses one reusable, nonallocating collider-overlap buffer per candidate. It respects the prefab layer collision matrix, ignores triggers, and stays inside the existing bounded attempt loop. Existing POI/base/core reservations and start-safe policy remain. Missing definitions/prefabs, missing guard targets and exhausted role placement now emit requested/placed counts and actionable warnings; Rival/Scavenger no longer silently substitute Basic.

Counts above are not a universal biome budget. Existing ElectromagneticStorm adds two Charging enemies and uses its 0.9 HP modifier; RaiderOccupied adds six Basic, two Shotgun and one Elite with its 1.1 HP modifier. Those paths and all region multipliers were inspected but not rebalanced. First-visit Region 3 remains the separate boss-focused path; this pass does not change it.

## Validation and evidence

Unity 6000.0.69f1 executed successfully; licensing did not block this pass.

| Check | Result |
|---|---|
| Runtime and Editor/test compilation | Passed in Unity; no C# compile errors |
| New EnemyRosterFieldBaseTests | 29/29 passed |
| Focused selection including QA stabilization and Route Core | 213/213 passed |
| Full EditMode | 880/883 passed; three pre-existing failures below |
| Read-only saved-asset / localization audit | Passed, 13 definitions and 245 localization records; existing CSV row-order warnings only |
| Actual 480x270 Editor Play Mode | 83 assertions passed, nine generated maps, 20 world screenshots |
| Serialized preservation | All original IDs retained; Expedition's only changed record is the generator's base-prefab array |
| Scope and whitespace | Checked against the task-start working-tree snapshot, not Git HEAD; prior user changes retained |

The three full-suite failures are `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)`. They match the starting baseline: pre-existing `sectorTechnologyLevels` normalization changes serialized save strings (1225 -> 1501, 1257 -> 1533, 1303 -> 1579). They were left unchanged.

Evidence is local/ignored under `Logs/EnemyRoster/`: `focused.xml`, `full.xml`, `audit.log`, `render.log`, `preservation.txt`, `definitions-unity.txt`, prefab/data dumps and `Rendered/audit.txt`. The persistent Editor entry points are `EnemyRosterAudit.Run`, `EnemyRosterAuthoring.Run` and `EnemyRosterRenderProbe.Run`; the latter quits the batch Editor after its disposable Play Mode fixture. Authoring refuses dirty scenes or an open Prefab stage.

The 20 final captures in `Logs/EnemyRoster/Rendered/` cover Basic, Shotgun, Charging telegraph/shot, Melee telegraph/dash, Elite MG, Elite Shotgun, Elite Charging telegraph/shot, Defender return, Rival harvest/return/deposit, Scavenger steal/flee, powered security/prison, increased storage, security-off and NPC rescue/portal. Images were inspected at their original 480x270 size. The captured world uses the saved Expedition generator, camera, player, light/background and production assets, with actors isolated in a disposable unsaved fixture for reproducibility. Combat invulnerability and selected competing actors/turrets are controlled only in that fixture. This is actual running AI/physics/rendering, but not an unassisted full campaign playthrough or a HUD/input-hardware review. No SaveManager or production save write is used.

Performance sample in this Editor fixture: GC allocation median 7,478 bytes/frame, p95 12,582, maximum 9,817,285, seven generation-0 collections. These include map generation, Editor, screenshot readback and existing debug-audio logging. They do not establish standalone frame-time or allocation budgets. No persistent duplicate generated actors or stuck Rival/Scavenger route was observed in the final sample; the reproduced Defender-return stall was fixed.

## Remaining limits and follow-up

- The Region 3 repeat-operation limitation from the original roster capture is resolved by the operation-binding hotfix above. The probe workaround that disabled OperationController has been removed; the dedicated follow-up runs the controller during actual first/repeat generation. Full campaign traversal remains outside this focused check.
- Automated combat and nine seeds do not establish final balance, all biome density, or every pathfinding corner. Basic/Shotgun/Charging/Elite attack values remain untouched. Human combat assessment and a standalone performance capture remain necessary before calling balance final.
- Existing base art is a large, bright industrial tile footprint. Thin cyan node links are readable at 480x270 but cross the large interior; bespoke conduit routing and floor-art refinement remain optional presentation work. No enemy art was regenerated and no world geometry was rebuilt.
- Role radar bindings and gating were checked; a human radar-discovery/readability session and physical controller/mouse feel were not part of the isolated world captures. No new role UI was introduced.

## Files changed by this pass

- Runtime: `Assets/02_Scripts/Core/ExpeditionMapGenerator.cs`, `Assets/02_Scripts/Enemies/EnemyRoleController.cs`.
- Definitions: `Assets/02_Scripts/Config/EnemyDefinition/Elite_MachineGun.asset`, `Elite_Charging.asset` (references only; Unity also serializes already-existing default fields).
- Existing prefabs: `Assets/03_Prefabs/Enemy/Enemy_Elite 1.prefab`, `Enemy_Elite 2.prefab`, `PF_Enemy_MeleeCharger_Common.prefab`, `PF_FactionBase_A.prefab`.
- New prefab/metadata: `Assets/03_Prefabs/Enemy/PF_FactionBase_B.prefab`, `PF_FieldBasePowerLink.prefab` and their `.meta` files.
- Scene: `Assets/01_Scenes/Expedition.unity`, only the base array in its existing generator record; all 2,327 original serialized IDs retained. Base A retains all 488 original IDs.
- New Editor source/metadata: `Assets/02_Scripts/UI/Editor/EnemyRosterAudit.cs`, `EnemyRosterAuthoring.cs`, `EnemyRosterRenderProbe.cs`, `Tests/EnemyRosterFieldBaseTests.cs` and their `.meta` files.
- Documentation: this file, `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, `IMPLEMENTATION_STATUS.md`, and `Documentation/ArtReview/Pirate_Common_Candidates_32x32_Audit.md`.

Bosses, Settlement corrupted-core defense, Route Core Deck, DarkUI Settlement, equipment/manufacturing, calibrated economy assets, campaign/save progression and the prior Inventory work are unchanged. No commit/push.
