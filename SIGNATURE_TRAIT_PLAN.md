# Signature Trait Plan

Audit date: 2026-09-21 (catalog refresh; original concept plan 2026-08-16)
Scope: design integration against the current working tree. The first Signature batch (P6, P3, and P8) was implemented on 2026-08-16; unimplemented concepts below remain planning only. Existing MG dash missiles and Sniper dash echo are also present in the current catalog.

## 1. Current Trait library summary (2026-09-21)

The current catalog has 50 distinct references/IDs: 46 normal prepared-run equipment
(26 Shared, 7 MG/Sweeper, 6 Shotgun/Breacher, 7 Sniper/Lancer), three hidden campaign boss
traits and Pixel Curse. Category totals are Shared 30 / WeaponSpecific 20. Rarity totals
are Common 17 / Rare 20 / Special 12 / Curse 1; rarity and weighting were preserved.
The older six-MG/six-Sniper count predates the existing dash signatures. No extra item
was added or removed in this balance pass.

See [the current content audit](CONTENT_PIPELINE_AUDIT.md#trait-inventory--equipment-audit-2026-09-21)
and its [46-row inventory](Docs/Design/EQUIPMENT_CATALOG_AUDIT.csv) for role, exact
incremental data, prerequisite/eligibility, overlap decisions and measured reward diagnostics.

`TraitDefinition` remains data authority. Settlement prepares persistent IDs, never
ordinary equipment levels. `RunRuntimeTraitStore` and `RunTraitAcquisitionService`
own acquisition/upgrades; only each newly gained level is applied. Legacy permanent
ordinary levels do not pre-level prepared equipment. Persistent story traits remain separate.
`ShipTraitTreePanel` uses the authored 4x3 prepared-loadout layout and dynamic growth rows.

The 40 existing enum values are unchanged. Five unused capabilities still lack authored
equipment/consumers; the supported `RemovePierceDamageFalloff` hook is now used by
Sniper pierce at Max2. No runtime mechanic or universal equipment drawback was introduced.

Signature progression now enables the core behavior at Lv1 and develops it through
existing scalars/configuration to Max3, except distance-based Overpressure which accrues
18/18/24 percentage points to its unchanged +60% cap. Reflector recharge is 20/17/14
seconds, replacing one source setting. Terminal guidance, semi-auto, dash missiles,
dash echo and extra pellet retain their original activation/count behavior at Lv1.
Future concepts below remain proposals, not additional implemented equipment.

## 2. Proposed content hierarchy

No new rarity enum is needed.

| Content layer | Current representation | Recommended rule |
|---|---|---|
| Ordinary Trait | Common or Rare; normal exposure | Numeric or moderate mechanical improvement; repeatable levels are appropriate |
| High-tier Trait | Usually Special; normal exposure | One strong decision-changing mechanic; Lv1 establishes the mechanic; later supported scalars complete its Max payoff |
| Weapon Evolution | Special + capstone metadata + weapon-specific category | Transformation earned from a runtime prerequisite; subsequent levels reinforce its behavior |
| Boss Trait | Special + Hidden | Granted only by the campaign/boss reward path; may be systemic and thematic |
| Curse | Curse rarity + Hidden + persistent story metadata | Existing persistent negative/story path only |

Minimum Evolution metadata now implemented:

1. `isCapstone` on `TraitDefinition`.
2. A small list of serialized Trait prerequisites, each holding a `TraitDefinition` reference and minimum level. Asset references avoid free-form ID typos; runtime still compares stable `TraitId` values.

`mutuallyExclusiveGroup` remains deferred until Friendly Fusion exists; Terminal Guidance documents the future `mg_evolution` conflict without changing current candidates for nonexistent content. No manager was added. The same prerequisite check is used by shop, rewards, level-up choices, generated field drops, Settlement, and final normal acquisition validation. Debug exact-ID grant is an explicit development bypass.

Ordinary equipment prerequisites use current-run levels. Persistent story prerequisites retain their separate permanent authority. Preparing an item never grants its level or satisfies a runtime prerequisite.

## 3. Passive concept classification (P1-P11)

| Concept | Recommended category | Why and overlap | Complexity and likely systems |
|---|---|---|---|
| **P1. Stealth-oriented build** | **Sniper High-tier** | `sn_stealth_scan` already establishes Radar cloak, enemy state/vision information, and slower visual detection. A new Trait should extend that lineage with a decision mechanic, not duplicate Hunter Jammer or timed invulnerability. | Medium. `PlayerStealthController`, `EnemyVisionSensor`, `EnemyBaseAI`, `PlayerGunshotNoiseEmitter`, Radar presentation. Prefer store/progress events over adding another ID poll. |
| **P2. Friendly ship fusion** | **Machine Gun Evolution** | A major weapon identity built around persistent allied units and the compact `Px_Player` form. It overlaps support drones only visually; the retained limited abilities and follower chain make it a distinct evolution. | Very high. Player visuals/movement, explicit fusion eligibility, friendly NPC/shop relationship, follower chain, `IPlayerOwnedAlly`, projectile ownership, target filtering, cleanup. Defer. |
| **P3. Maximum homing evolution — IMPLEMENTED** | **Machine Gun Evolution** | `mg_terminal_guidance`, Special, Max3, gated by `mg_guidance_control` runtime Lv3. Lv1 enables terminal tracking; Lv2/3 add homing angle 6/10 and range 0.5/0.75. It transforms current-run Machine Gun targeting without copying the temporary Lock-on Array identity. | Implemented through capstone/prerequisite metadata, shared offer filtering, `PlayerWeaponModifiers`, a Machine-Gun-only projectile snapshot, and the existing fixed-buffer `Bullet` homing query. |
| **P4. Friendly pet / Lock Shot** | **Future / Defer** | “Pet,” “lock shot,” guidance drone, fusion follower, and maximum homing currently overlap too much. Select one core loop before authoring data. | Medium-high once clarified. Likely player-owned ally, target selection, weapon-fire event, pooling, and cleanup. |
| **P5. Mine-laying while moving** | **Better as Reinforcement instead** | Automatic passive mine trails overlap the completed `rf_gravity_mine` and can become background damage with little decision cost. It fits a future Gravity Mine upgrade/evolution or alternate active behavior better than a new Trait. | Medium. Existing area-control field/pooling plus movement-distance cadence; must cap active mines and avoid one Update per mine. |
| **P6. Shotgun close-range scaling — IMPLEMENTED** | **Shotgun High-tier** | `sg_close_quarters_overpressure`, Special, Max3. Incremental 18/18/24% reaches +60% maximum enemy-damage bonus at Max; full bonus applies through 1.25 world units and falls linearly to zero at 4 units. Harvest targets remain unaffected. | Implemented through `ShotgunCloseRangeDamagePercent`, `PlayerWeaponModifiers`, a Shotgun-only projectile configuration hook, and pooled `Bullet` spawn-origin/hit-point calculation. Enhanced pooled impact VFX begins at +25% bonus. |
| **P7. Energy projectile -> laser -> lock-on laser** | **Sniper Evolution** | A staged transformation with a clear final capstone. `sn_semi_auto_laser` is already an authored mode change and should be part of the lineage. | Very high for the final stage. Sniper charge/mode logic, cursor targeting, stable target list, pooled mark UI/VFX, and a new player-owned laser executor. Defer final lock-on stage. |
| **P8. Periodic reflective shield — IMPLEMENTED** | **High-tier Shared Trait** | `shared_periodic_reflector`, Special, Max3. Ready is held until the first eligible enemy Bullet arrives; that hit starts a 1.25-second reflection window followed by a 20/17/14-second gameplay-time recharge at Lv1/2/3. It remains distinct from Guard Drone and future directional/laser shields. | Implemented with one source-owned player reflector, an outer trigger, `Bullet` reflection snapshots/claims, `ForceRelease(false)`, and a clean pooled neutral Player projectile. Ordinary Bullets only; lasers remain unsupported. |
| **P9. Graze economy** | **High-tier Shared Trait** | A true risk/economy loop with no current Trait equivalent. It should not become a simple pickup radius or currency multiplier. | High. Player graze trigger, pooled Bullet one-award state, Dash-state exclusion, run-wallet API, and careful hit/exit ordering. Defer until bullet-heavy encounters are stable. |
| **P10. Galactic Service Center revival** | **High-tier Shared Trait** | A one-time run-saving decision. It overlaps defensive passives in outcome but is distinct because it triggers only on lethal damage and permanently changes the remainder of that run. | Medium-high. A pre-death `PlayerHealth` interception hook, one-use run state, 50% heal, temporary invulnerability, and a one-time +10% weapon modifier. Do not resurrect after `Died`. |
| **P11. Part-stealing projectile, lifesteal, +10% incoming damage** | **Boss Trait** | Strong thematic risk/reward and suitable for a specific boss reward rather than ordinary random availability. It does not overlap plain heal efficiency because it converts dealt damage while increasing vulnerability. | Medium-high. Applied-damage reporting from player attacks, heal-efficiency policy, source ownership, and a source-owned incoming-damage multiplier in `PlayerHealth`. |

## 4. Active concept classification (A1-A6)

These remain Reinforcement candidates, not Traits.

| Concept | Recommendation | Existing overlap and distinction | Complexity / priority |
|---|---|---|---|
| **A1. Hold-to-use bilateral time manipulation** | Genuinely new Reinforcement, later | No current active has held resource consumption. It must be a sustained mode, not Stasis Anchor or a normal recharge button. | Very high. Current controller is press-to-use, with release handling only for Emergency Return. Needs begin/hold/end semantics, an unscaled resource gauge, and deliberate simulation ownership. Low near-term priority. |
| **A2. Enemy-only extreme slow** | Prefer an upgrade/evolution of `rf_sn_stasis_anchor` if locomotion-only; new Reinforcement only if it truly slows attacks/projectiles too | Existing source-owned locomotion slow already supports strong local stasis. A global enemy-time effect would be a different and much larger system. | Medium for a Stasis upgrade; high for full enemy time dilation. Medium priority after design chooses scope. |
| **A3. Directional shield** | Genuinely new Reinforcement | Distinct only if aim/facing matters: front protected, rear vulnerable. Unlike Guard Drone, it is player-oriented; unlike Burst Barrier, it is not invulnerability. | Medium. Aim direction, a forward trigger/angle test, enemy Bullet filtering, and separate laser rules. High future-active priority. |
| **A4. Homing missile barrage** | Genuinely new Reinforcement | No existing missile-barrage identity. Existing Bullet homing and PoolManager make it a clean offensive active. | Medium. Serialized missile projectile, fixed-count pooled launch sequence, non-alloc target acquisition, source ownership. High future-active priority. |
| **A5. Temporary protective shield** | Upgrade/consolidate an existing Reinforcement, not a new definition | `rf_burst_barrier`, `rf_phase_cloak`, `rf_core_stabilizer`, `rf_sg_breach_shield`, and Guard Drone already cover timed protection. | Low implementation risk but very high redundancy. No new item until an existing shield is selected for evolution. |
| **A6. Movement-speed combat buff** | Upgrade/tune existing Reinforcements, not a new definition | `rf_magnetic_burst`, `rf_overdrive_injector`, `rf_thruster_ampoule`, `rf_warp_flare`, and `rf_sg_escape_thruster` already occupy this space. | Low complexity, low content priority. |

Defensive identity boundary for future work:

- Guard Drone: a moving localized interceptor.
- Directional Shield: deliberate front-facing protection with rear risk.
- Reflective Shield Trait: periodically charged passive that returns ordinary bullets.
- Burst Barrier/Phase Cloak: unconditional timed survival windows.

## 5. Prerequisite and evolution recommendations

### Machine Gun maximum homing

- **IMPLEMENTED:** `mg_terminal_guidance`, Special, Machine Gun-only, max level 1, capstone.
- Requires `mg_guidance_control` at level 3 through a serialized `TraitDefinition` reference and required level.
- Terminal projectiles add +150 degrees/second steering and +1.5 acquisition range after all existing homing modifiers, retain their target to 1.5 times acquisition range, reacquire only after target loss, and use direct close steering within 0.75 units.
- With the prerequisite maxed, the current effective values are 184 degrees/second and range 12. Lock-on Array raises these to 214 degrees/second and range 16 for its authored 8 seconds, so the active remains useful.
- Normal candidate paths enforce the prerequisite centrally. `DebugItemGrantUI` exact-ID grants bypass the offer prerequisite while preserving normal runtime acquisition/application.

### Machine Gun fusion

- No current Trait establishes a friendly-shop/follower relationship. Fusion therefore needs a new predecessor before it can become eligible.
- Recommended future chain: a Machine Gun High-tier “cooperative transponder” relationship/discount mechanic -> explicit eligible friendly recruitment -> fusion capstone.
- Shop neutrality (`ShopStructure.CanTrade`) is not the same as friendship. Do not consume or reparent a `ShopStructure`; let an eligible friendly shop offer a mobile fusion candidate.

### Sniper laser line

- Use `sn_semi_auto_laser` level 1 as the existing mode prerequisite.
- Use `sn_focus_lens` level 3 as the current precision prerequisite for a later lock-on-laser capstone. `sn_charge_accelerator` is a cadence upgrade, not target-marking evidence.
- Keep `sn_piercing_amplifier` compatible with the current hybrid semi-auto/charged mode. Only a future pure projectile/rail capstone should conflict with the final multi-lock laser.

### Shotgun close range

- Treat P6 as a High-tier, not an Evolution, for the first implementation. It does not need a hard prerequisite.
- If later promoted into a full Evolution, the most coherent current build signals are `sg_breaching_drive` and `sg_close_harvest_burst`; do not require `sg_choke_barrel`, which encourages tighter longer-range consistency rather than committed proximity.

### Acquisition rule

Prerequisites should gate candidate generation and be revalidated by `RunTraitAcquisitionService`. Exclusivity should be checked at that same final boundary. A generated world pickup that becomes invalid after another choice should remain non-acquirable or be replaced through the existing reward path; UI must explain the reason rather than silently granting it.

## 6. Exclusivity recommendations

Use exclusivity only for incompatible transformations.

1. `mg_evolution`: maximum homing and friendly-ship fusion should be exclusive. Both claim the Machine Gun capstone budget and would otherwise combine autonomous followers with near-guaranteed guidance into an unclear dominant build.
2. `sniper_delivery_evolution`: final multi-lock laser should be exclusive with a future pure rail/pierce projectile capstone. It should **not** exclude ordinary `sn_piercing_amplifier`, `sn_charge_accelerator`, or the current hybrid `sn_semi_auto_laser` prerequisite.
3. No Shared High-tier requires blanket exclusivity. Revival, graze economy, and periodic reflection can coexist unless balance testing later proves a specific pair invalid.
4. Do not make all Evolutions globally exclusive. One capstone per weapon family is enough; Shared signature Traits remain independent build texture.

## 7. Technical feasibility notes

### Friendly fusion

The visual direction is feasible without composite sprites. `PlayerVisualStateController` already keeps cursed players on `Px_Player_0`, while weapon identity is expressed through accents/projectiles/VFX. `IPlayerOwnedAlly` and source-aware Bullet filtering already keep player shots from damaging summons.

Recommended future structure:

- One focused player-owned follower-chain component stores a bounded position history and updates all follower transforms in one `LateUpdate`; do not give every follower an independent follow loop.
- Each follower is an independent pooled object implementing `IPlayerOwnedAlly`, with no Rigidbody-driven follow behavior.
- Only explicitly authored mobile friendlies are fusion candidates. Add a small eligibility marker to supported NPC/friendly ship prefabs; never infer eligibility from names or generic neutral shop state.
- Retained abilities should be curated, limited adapters to existing fire/heal/scan hooks. Do not attempt to preserve arbitrary NPC MonoBehaviour graphs.
- Cap chain length, shot cadence, and query frequency. Clear the entire chain on weapon/evolution removal, death, scene unload, and run end.

Current blockers are product decisions, not rendering: which friendlies consent/qualify, what happens to their world objective, and how their retained ability is reduced. Implement after those rules and the predecessor relationship Trait are authored.

### Graze economy

`Bullet` is the correct lifecycle anchor: it already owns `ProjectileOwner`, spawn state, collision, active lifetime, and pooled reset. `PlayerDash.IsDashing` and `DashStarted` provide an explicit Dash exclusion.

Recommended future integration:

1. Add a small trigger child around the Player, owned by a focused graze component.
2. On Enemy Bullet enter, register a candidate; do not pay immediately.
3. Invalidate current candidates when Dash starts. On trigger exit, pay only if the Bullet is still active, never hit the Player, has not paid before, and was not Dash-invalidated.
4. Store “graze awarded/invalidated for this activation” on `Bullet` and reset it in `ResetRuntimeState`/pool release. One projectile can pay once.
5. Grant Credits through the current RunManager/RunWallet API.

Paying on exit prevents a later direct hit from counting as a successful graze. One-shot Bullet state prevents per-frame and stationary-Bullet farming. Lasers need a separate future near-miss model.

### Reflective shield — IMPLEMENTED

`shared_periodic_reflector` uses approach **B**: intercept/release the Enemy Bullet and spawn a known Player projectile.

Changing an Enemy Bullet in place is riskier because `Bullet.Initialize` controls owner layer, source, homing, pierce history, damage snapshot, special motion, impact VFX, and lifetime. A focused reflector can reuse the interceptor trigger pattern, call `ForceRelease(false)`, then get a serialized reflection projectile prefab/definition from PoolManager and initialize it as `ProjectileOwner.Player` in the reversed incoming direction. This avoids carrying enemy-only special motion or stale `damagedTargets` into a reflected shot.

The implemented state flow is Ready (held indefinitely) -> first reflection -> Reflecting for 1.25 seconds -> Cooldown for the current level setting (20/17/14 seconds) -> Ready. The focused player component is source-owned so run and permanent application cannot create independent shields. It uses a 0.42-world-unit trigger around the Player, copies the incoming Bullet's current damage and velocity magnitude, aims at a still-valid hostile firing source or reverses incoming travel, and emits a clean Player-owned projectile with no homing, pierce, split, or weapon-Trait configuration. Pending radial-split carriers are excluded; ordinary split children are eligible. Boss and field lasers are bespoke LineRenderer/hazard components, not Bullets, and remain unsupported.

### Sniper lock-on laser

The current Sniper “laser” mode still launches a pooled projectile; the project has no reusable player hitscan/lock-list system. Boss laser hazards are enemy-specific and should not be reused as a player weapon executor.

Recommended future structure inside the Sniper weapon/evolution component:

- Expose a read-only aim-world position from `PlayerController2D`, which already owns mouse-to-world aiming, rather than duplicating conversion code.
- While charging/marking, run a small cursor-local `Physics2D.OverlapCircleNonAlloc` or aim-ray query at a controlled interval. Resolve `EnemyHealth`, dedupe by instance ID, and cap the list at five.
- Subscribe to target death or validate active/dead state before firing. Never scan all enemies each frame.
- On release, snapshot the valid list, damage each unique target once, spawn pooled lock/beam presentation, and clear marks on fire, cancel, unequip, disable, death, or scene unload.
- A Boss with multiple colliders must occupy one lock slot and receive one hit per firing cycle.

### Close-range Shotgun scaling

`Bullet` already snapshots `spawnPosition`, damage, ownership, pierce state, and projectile parameters. The future Shotgun modifier should snapshot a close-range multiplier/threshold when each pellet is initialized. At hit time, calculate squared travel distance from `spawnPosition` and derive a local hit multiplier; do not mutate the persistent damage snapshot merely to apply the proximity bonus.

Add a semantically exact scalar effect such as `CloseRangeDamagePercent`. Do not repurpose the currently unsupported `CloseRangeSuppressionPercent`; suppression and damage are different promises. Keep the distance threshold/curve in Shotgun weapon configuration, and drive the stronger close-hit VFX from the same resolved distance band. No search or allocation is required per hit.

### Revival

The correct hook is inside `PlayerHealth.TakeDamage` after component shield and Armor resolve `remainingDamage`, but before HP reaches zero and before `Die()` is called. `PlayerHealth.Die()` immediately sets `isDead`, disables gameplay/collision, emits `Died`, and starts `PlayerDeathSequenceController`; revival after that point is too late.

A focused future revival runtime component should expose a one-shot `TryConsumeLethalRecovery` call. On success it restores 50% max HP, grants a short safety invulnerability, records the use for the current run, and applies +10% damage exactly once through `PlayerWeaponModifiers`. `Died` must not fire, so `ShipDeathBreakup`, run settlement, and scene flow never start. Reset the one-use state on a new run, not through SaveData.

### Effect-type fit for P1-P11

| Concept | Existing effect support | Recommended future integration |
|---|---|---|
| P1 stealth | `RadarStealthDurationBonus`, current `sn_stealth_scan` ID-aware component | Extend the existing stealth component/event inputs; explicit mechanic marker only if a new behavior cannot be inferred from level |
| P2 fusion | None | Dedicated player follower-chain mechanic; not a scalar |
| P3 maximum homing — IMPLEMENTED | `MachineGunTerminalGuidance` plus existing homing bonuses | Machine-Gun-only projectile snapshot adds steering/range and Terminal retention/reacquisition behavior; no live Trait polling |
| P4 pet/lock shot | Homing and player-owned ally foundations only | Dedicated weapon/ally mechanic after identity is clarified |
| P5 moving mines | Existing Reinforcement area-field/pooling, no Trait effect | Prefer Reinforcement upgrade; dedicated cadence owner if pursued |
| P6 close-range scaling — IMPLEMENTED | `ShotgunCloseRangeDamagePercent` | `sg_close_quarters_overpressure` snapshots its modifier into Player Shotgun pellets; `Bullet` resolves enemy-only distance scaling from its own spawn origin and resets all state on pool reuse. |
| P7 lock-on laser | `SniperSemiAutoMode`, charge/pierce scalars | Dedicated Sniper evolution mode and lock list |
| P8 reflective shield — IMPLEMENTED | `PeriodicReflectiveShield` plus projectile ownership/interceptor patterns | `shared_periodic_reflector` enables one source-owned player reflector; safe release-and-respawn reflection handles ordinary Bullets only |
| P9 graze economy | Currency-gain APIs only | Dedicated graze sensor + per-Bullet activation state |
| P10 revival | Health/heal and weapon damage modifier exist | Dedicated lethal-damage guard; not a float-only effect |
| P11 part stealing | Heal path exists; no authoritative applied-damage event or incoming multiplier | Add applied-damage reporting and a source-owned incoming-damage multiplier; Boss mechanic component |

## 8. First three implementation candidates

Implement in this order, one focused task at a time:

1. **P6 Shotgun Close-Quarters Overpressure — IMPLEMENTED.** ID `sg_close_quarters_overpressure`; Special, Shotgun-only, Max3; incremental 18/18/24% reaches +60% at Max through 1.25 units, linear falloff to normal damage at 4 units. Implemented with a Shotgun-only modifier snapshot and pooled Bullet spawn-origin/hit-point damage scaling.
2. **P3 Terminal Guidance — IMPLEMENTED.** ID `mg_terminal_guidance`; Special, Machine Gun-only, Max3; requires `mg_guidance_control` level 3. Uses shared prerequisite filtering and a pooled projectile snapshot with +150 steering, +1.5 acquisition range, 1.5x retention range, and close steering within 0.75 units.
3. **P8 Periodic Reflective Shield — IMPLEMENTED.** ID `shared_periodic_reflector`; Special, Shared, Max3. Ready is held until the first eligible Bullet, the 1.25-second reflection window returns every entering ordinary Enemy Bullet, then the level-configured 20/17/14-second scaled-gameplay cooldown begins. The implementation releases the original safely and spawns a clean pooled Player projectile; laser reflection is deferred.

Weapon-specific Dash experiment: `mg_dash_missile_salvo` is a Machine Gun Special high-tier Trait (not a capstone) that launches three independently guided micro-missiles on each successful MG Dash. The existing `sn_dash_echo_shot` is the separate Sniper next-shot echo signature. Both have Max3 supporting scalars; a Shotgun dash signature remains outside this pass.

This batch covers one positional weapon mechanic, one prerequisite-driven Evolution, and one systemic Shared passive without requiring final art, friendly-NPC policy, death-sequence changes, or a full laser framework.

## 9. Deferred large features and prerequisites

| Feature | Defer until |
|---|---|
| Friendly ship fusion | Eligible friendly types, consent/recruitment rules, predecessor relationship Trait, follower ability budget, and chain cap are specified |
| Sniper five-target lock-on laser | Player laser executor, aim-world accessor, mark UX, target invalidation, and evolution exclusivity are defined |
| Graze economy | Bullet-pattern density and Credit pacing are stable; one-award pooled lifecycle tests can be run in Play Mode |
| Galactic Service Center revival | A pre-death hook and one-use run-state policy are reviewed against Tutorial death and normal run settlement |
| Part-stealing Boss Trait | A concrete boss source is chosen and authoritative applied-damage reporting exists |
| Friendly pet / Lock Shot | Its identity is separated from guidance drone, maximum homing, and fusion |
| Held bilateral time manipulation | Reinforcement input lifecycle and resource-gauge ownership are designed; pause and unscaled UI behavior are specified |
| Full enemy-only time manipulation | Decide whether it means locomotion-only Stasis or also attack/projectile simulation |
| Laser reflection | Laser hazards gain a typed reflect/deflect contract; do not infer from LineRenderer visuals |

## 10. Recommended content count

Do not create another 40-Trait batch. Plan an initial **eight-slot Signature expansion** and implement it in small validated batches:

- 2 Shared High-tier slots.
- 2 Machine Gun slots: one High-tier bridge and one Evolution.
- 2 Shotgun slots: one High-tier and one later alternate/evolution slot.
- 2 Sniper slots: one High-tier continuation and one staged Evolution.

This expansion count is the historical concept budget, not an instruction to add equipment. Current implementations also include both weapon dash signatures; re-audit actual content before allocating future slots. P11 can become a ninth, separately sourced Boss Trait when its boss/reward context is known; it should not inflate the ordinary random pool.

Quality gate for every future signature asset:

- It must change a decision, input rhythm, positioning rule, target selection, survival rule, or economy risk loop.
- It must have one authoritative runtime owner and clean run/death/scene reset.
- It must coexist with permanent and run Trait sources without cleanup removing unrelated modifiers.
- Its prerequisite/exclusivity state must be enforced at candidate selection and final acquisition.
- It must not duplicate an existing Reinforcement's tactical button identity.
