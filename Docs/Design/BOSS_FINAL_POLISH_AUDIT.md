# Campaign boss final polish audit

## 2026-10-02 - Raider Sniper Commander (Region C revisit)

Final validation: **280/280 focused tests passed** (43 Sniper cases), seven generated Region C revisit encounters including three actual-weapon victories and four lifecycle/timing fixtures, **130 native 480x270 captures**, no TMP overflow, and zero final Console errors including teardown. Each weapon passed an input-driven post-commit dodge with invulnerability off. Mine timing measured 1.1254 s warning, 7.9945 s armed lifetime and 0.2074 s detonation cleanup; one proximity hit dealt 2 Armor damage. 12,444 protected baseline files are unchanged. Assisted victories are not unassisted difficulty acceptance; details: `Logs/RaiderSniper/REPORT.txt`.

Added a separate 125-HP / one 50% Critical Sniper boss for Region C revisits. Region A Assault, Region B Carrier and all first-clear SYSTEM routes remain unchanged. Existing BossDummyController/current-depth reward/return authority is reused.

One scheduler owns 0.9/0.7-second Rail Lock acquisition, 0.65-second committed charge, the approved rail sequence (0.09 seconds damaging, 0.13 seconds total), and 2.2/2.0-second recovery. Existing bounded prediction caps lead at 0.25 seconds. Two/three pooled warning mines have a hard cap of three, 1.1-second warning, eight-second armed timeout, no rewards, and lifecycle cleanup. Deliberate range control uses one physics owner and one visual facing owner. Critical safely cancels the active sequence; rail/mine damage stays at the shared enemy value of 2.

Native captures exposed name wrapping and hull clipping after retreat. The new boss uses a compact display name, full Raider identity subtitle, and sequence-boundary framing updates through the existing camera owner. Other boss framing is unchanged. Approved Sniper states, Rail Lock/Shot, Raider warning, sparks and Critical Loop are reused; no artwork was generated.

Validation evidence and limits: `Logs/RaiderSniper/REPORT.txt`, `tests.xml`, `metrics.json`, `screenshots.html`. Assisted native Play Mode coverage is distinct from unassisted difficulty acceptance. Full runtime semantics are in SYSTEM_DESIGN.md.


## Current Region B revisit Carrier authority

Final scoped validation: **237/237 focused tests passed**, including 33 Carrier cases. Generated Region B revisit wins with Machine Gun, Shotgun and Sniper plus applied-pull death/abort cleanup checks exited with zero Console errors. **152 native 480x270 captures**, no TMP overflow. QA used assisted positioning/invulnerability; unassisted difficulty and reward-choice/Beacon interactions are not claimed. Full evidence and measured timings: `Logs/RaiderSalvage/REPORT.txt`.

The historical shared-Raider roster below is superseded only for Region B revisits: Raider Salvage Carrier now has its own 125-HP prefab/controller and one 50% Overload threshold. Region A and Region C revisit bindings remain the existing Commander. Carrier collection uses encounter-owned destroyable ammunition, never actual economy pickups. Existing BossDummyController/current-depth repeat reward and death ownership remain unchanged. Scoped validation: `Logs/RaiderSalvage/REPORT.txt`.


## 2026-10-01 Raider follow-up

The historical Raider fan/cover-blast/escort findings below are superseded by the current Assault Commander sequence: alternating committed mount bursts, short pressure runs, temporary Weapons Hot and recovery. Its 125 HP, existing 50% transition and repeat death/reward authority are unchanged. Current scoped verification: `Logs/RaiderAssault/REPORT.txt`. Other roster entries below are historical snapshots and are not rewritten by this Raider-only pass.


Date: 2026-09-28. Authority: the current local working tree, resolved Unity prefab/scene serialization and existing runtime owners. This is a scripted structural/readability audit, not final human balance approval.

## Production roster and rewards

| Encounter | Saved prefab under Assets/03_Prefabs/Enemy | Production HP | Transition authority | Fixed first-clear progression |
| --- | --- | ---: | --- | --- |
| Sector Administrator | Boss.prefab | 140 | 50% HP; shield max(24, HP × .22) = 30.8; then true phase 2 | Sector Stabilizer |
| Salvage Devourer | PF_Boss_SalvageDevourer_FrigateTriad.prefab | 165 = 3 × 55 | Three/two/one living parts; final part reserves 1 aggregate HP for charge/death handoff | Matter Compressor; existing guaranteed 2 Core cargo grant |
| Phase Gatekeeper | PF_Boss_PhaseGatekeeper.prefab | 180 | Three reflected attacks, 5-second exposure, convergence/special cycle, restoration; not an HP threshold | Phase Navigation Lens; no guaranteed Core grant |
| NULL DISPATCHER | PF_Boss_NullDispatcher.prefab | 300 | 50% treatment floor, explicit branch/support, polarity, 20% final floor, shell break | Existing final campaign defeat and FinalVictory; no story part/capsule/ordinary exit |
| Repeat Raider Commander | PF_Boss_RaiderCommander.prefab | 125 | 50% phase-2 request; .42-second transition | Existing current-depth repeat clear, no story part |

BossCampaign_Region1/2/3/Final assets have IDs 1/2/3/4, depths 0/1/2/3 and story-part IDs 1/2/3/0. The shared Raider's serialized Region-1 definition is deliberately resolved against current run depth by CampaignBossRewardService. Story parts have no dedicated serialized sprite and retain the established generic fallback.

Region-1 and Region-3 definitions still contain legacy Core reward fields 1 and 2. These are not evidence of actual grants: BossDummyController calls the guaranteed Core service only for the first-story Salvage Devourer route. Guaranteed passive assets remain 41_boss_sector_barrier, 42_boss_matter_reconstructor and 43_boss_phase_afterimage. No reward asset or currency amount changed.

BossDummyController accepts death once, marks campaign progression before optional death visuals, delegates ordinary choices/exits to BossRewardExitCoordinator, and remains the only final-victory handoff. First story clears use Beacon only; authorized Region-1/2 repeats may also use a next-depth portal; Region-3 first/repeat clears never add a FinalNetwork portal. The Region-3 operation hotfix continues to consume the map generator's resolved first/repeat route.

## Resolved tuning snapshot

Complete read-only Unity dumps, including hierarchy paths, references, controller fields, projectile/reward dependencies and saved Expedition bindings, are in `Logs/BossFinalPolish/Snapshots/`. The following values are resolved production values, not assumed C# defaults. Distances are world units and durations are seconds.

### Sector Administrator

- Movement .85, turn 540 degrees/second; saved arena half extents (12,12), overridden by the existing Core intro's resolved arena. Base gameplay camera orthographic size 4.21875. Phase-2 wide zoom ×2.5, out .9, in .85, settle .16; letterbox .085 height ratio, .18/.22 in/out.
- Scheduler initial delay .8; recovery .35; cooldowns Section 4, Spread 3, Tracking 5, rotating 6.
- Section: 2 lines, 1.2 warning, width .45, damage 4 every .75, active 2.5/3 in phase 1/2. Warning width .44. Phase 2 uses .75 preview + .2 lock, lock width ×1.35, .16 recovery.
- Spread: 2/3 volleys, 5 bullets, 70-degree fan, 8-degree volley step, .25 interval, damage 2, speed inherited from Projectile_Enemy = 8, range 18/20.
- Tracking cannon: 1/2 shots, .9 tracking aim, .3 between shots, damage 6, speed 24, range 22, visual scale ×3. Aim line width .06 and length 24. Existing meteor/container/wreck destruction policy remains.
- Rotating laser: .2 warmup, 3 steps of 60 degrees over 2 seconds each, .15 between steps, .35 end delay; width .8, damage 4 every .5. Boundary damage 4 every .5. Manager movement .9, new phase-2 manager entry .8.
- Death presentation .45 destabilize + .7 breakup hold + .32 final emphasis. Shield transition keeps its authored 50% threshold; the C# initializer's .3 does not describe production.

### Salvage Devourer

- Three fixed 55-HP parts; root 165 HP. Entry offset 5.5, duration .9, stagger .12; rebalancing speed 4.5; state-change delay .6. Recovery .85/.9/.9 for 3/2/1 alive.
- Corridor scroll 1.25; camera ×1.2 = orthographic half-height 5.0625; lane half-width 5.75; horizontal inset .2, top inset 3.2, bottom .9, wall thickness .65. These remain unchanged.
- Formation anchor (0,.1). Final offsets: three (-2.55,2.70)/(0,3.05)/(2.55,2.70); two (-1.65,2.80)/(1.65,2.80); one (0,2.90). See measured correction below.
- N-way: 68-degree spread, 2 volleys, .55 interval, speed 7.25, 10-degree bias, .44 projectile scale. Aimed: 3 shots, .25 lock, .38 interval, prediction .22, max lead .8, variation .28.
- Area strike: 3 warnings, radius 1.05, warning 1, damage 3, minimum separation 2.8, escape lane 1.4.
- Homing: 2 missiles per frigate, damage 3, speed 7, range 14, .32 launch interval, homing limited to 1.5 seconds and 105 degrees/second, acquisition range 24, launch angle 12 degrees.
- Ricochet: 2 shots per frigate, damage 3, speed 9, range 24, maximum 3 bounces, .3 interval, base angle 46 ±11 degrees.
- One-alive laser: .9 warning + .2 lane lock, .65 active, width 1, damage 4 every .75, alignment speed 12. Rotating pattern: 7 bullets ×3 volleys, .55 interval, 15-degree step, 140-degree sector.
- Removed-part sequence: .55 warning, .4 rotation, .2 hold, speed 18, inset .55, wreck fade .35 delay/2 duration.
- Final sequence: .85 warning + .2 lane lock, alignment speed 12, charge speed 16, damage 6, hitbox (1,1.6), offset (0,-.1), end padding .65, spark interval .18. Deferred death still has one handoff and no part-level rewards.

### Phase Gatekeeper

- First-visit foundation has 12 reflector plates, not the old documented 8. Production focused map is 80×80. The existing generator owns randomized bounded templates and clearance; no generation values changed.
- Intro .8 decloak + .7 combat charge; frame animation .075. Normal/special tracking .65/.55, lock .4, firing .8, recovery .75. Three attacks per cycle, maximum 3 reflections.
- Range 72 normal/180 special; initial width 2/2.4; normal reflected width halves, special multiplier 1 with tapered end ×.28. Damage 4 every .5; environment break damage 999.
- Preview/locked widths ×.42/×.62; prediction .28, threat padding .3, candidate attempts 16, minimum segment 1.25; target marker radius .42/width .08. Origin cue .12, reflector blink .18 with .16 pulse, orientation .45.
- Special arena half-size 11, minimum 6.5; boss-slot inset 2.2; shrink .84/.70/.58, warning .75, movement .8, pulse lead .18; wall .28, padding .35. Convergence .25 pulse +1.1 move +.1 settle.
- Normal camera ×1.25; attack camera cap ×3.1, attack padding 2.2, special padding 1.25, refresh .12. Existing owner releases both camera and viewport constraints on cleanup.
- Stealth .2 cloak/.1 hold/.25 reveal/.65 preparation; reposition distance 10–32, edge padding 4, clearance 1.75, attempts 12. Exposure 5 seconds, player distance limited to 6.5 for readability, scale pulse ×1.22 at speed 9.

### NULL DISPATCHER

- 300 HP; initial intro floor 100%, treatment 50%, final gate 20%. One main scheduler rotates Route Partition, Compression Dispatch and Phase Redirect. Saved warning .8 and recovery .9; final recovery .8.
- Route Partition source-owned lanes: 3, warning width .07, damaging width .18, damage 2 every .4, active 2.4. Other Phase-1 projectiles retain damage 2 and range 16; relay entry leg is harmless and reinitializes the same projectile for the outgoing leg.
- Accept reclaim 4.5, pull speed 4, max-HP drain fraction/second .111112 with captured half-HP floor. Reject transition .75. Explicit intervention 1.5, bounded break push .3. Both branches retain explicit Settlement support orchestration.
- The legacy support component's serialized .45 HP threshold is inert compatibility data; no automatic HP trigger is subscribed. Support interval .75, skip unavailable facilities; Hangar heal 3+2/level; Engine 8 seconds, speed 6%/level and dash cooldown .03/level; Weapon Lab 8+6/level; Recovery armor 1/level.
- Polarity switches every 2 patterns over .5. Hostile packet damage 1; support heal 1, interval 5 then 3.5 final. Total cap 26 = 24 hostile +2 support. One encounter routine advances packets; pooled release resets owner/recipient/collision state.
- Final shell transition 2 seconds; combination budgets remain 4 partition packets, 2 redirect bursts ×2 packets, and 3 compression fan packets.
- Ending opening 1.2 and shutdown 1.3. The installed Pixel Crushers treatment graph has six lines, Accept/Reject and one natural terminal; termination has six lines. Existing interruption/start-failure fallback and BossDummyController FinalVictory ownership remain.

## Changes and evidence

### FIXED BUG — source-owned cleanup

Six focused regressions failed against the task-start implementation (`cleanup-before-fix.xml`). Sector bullets survived death, phase transition and disable. Raider death/run-end cleared unrelated enemy bullets; disable left its own bullets active. Sector now calls existing `Bullet.ReleaseAllActiveFromSource(transform)` at those boundaries. Raider uses the same source cleanup and separately retires each owned escort's bullets. No global enemy-projectile clear, new manager or per-frame search was added.

### FIXED BUG — Sector lethal phase bypass

Previously, a sufficiently large hit could kill Sector before Update activated its shield. OnEnable now reserves the saved phase ratio using EnemyHealth's existing source-owned floor. Entering true phase 2 and disabling the controller remove only that floor. The actual rendered test applied 9,999 damage: HP stopped at 70, shield setup completed, shield could break, phase 2 ran, and later lethal damage completed death. The threshold, max HP, shield amount and attack numbers are unchanged.

### FIXED BUG — Phase scene-unload cleanup

Closing a scene during the real Phase intro exposed a MissingReferenceException when the HUD was destroyed before the boss. The null-conditional call bypassed Unity's destroyed-object comparison. ReleaseIntroLocks now tests the Unity reference before releasing HUD cinematic ownership. A focused destroyed-HUD-first regression covers this teardown order.

### FIXED BUG — Sector player-death cancellation

An actual player death during the shield transition produced a Death result while the boss transition coroutine and cinematic ownership were still active. Sector now subscribes to the existing PlayerHealth.Died and RunManager.RunEnded events and routes cancellation through its existing cleanup. Cancellation stops the scheduler/transition, removes its floor, attacks, camera and input ownership, hides the boss HUD, and does not play the boss-death sound or mark a kill. Disable/death unsubscribe. The repeated player-death run passes; the focused test also preserves another owner's input lock and the surviving boss HP.

### CLEAR READABILITY ISSUE — frigates clipped above viewport

The first 480×270 run showed all three frigates cropped at the upper edge. The saved sprite is 188 pixels high at 100 PPU, scaled 1.65, requiring 1.551 units above its pivot. With camera half-height 5.0625 and anchor +.1, the old 4.95–5.30 offsets could not fit the full silhouette.

All six formation offsets moved down exactly 2.25 units; X and relative formation shape are unchanged. Three matching authored initial transforms also changed through PrefabUtility. Before → after Y: left/right 4.95→2.70, center 5.30→3.05, two-alive 5.05→2.80, one-alive 5.15→2.90. The highest full silhouette now has at least .3 units of top padding. No camera enlargement, sprite substitution, projectile change or attack timing change was used. This geometry correction shortens the approach distance from the upper screen; subjective pressure remains a human play question.

## Validation and remaining work

### STRUCTURAL PASS — compilation and EditMode

- Unity 6000.0.69f1 runtime and Editor/test compilation passed. The read-only audit resolves production references and runs localization validation (245 records); no localization assets were changed.
- New boss regressions: **23/23**. Focused boss/QA/operation selection: **214/214** (`Logs/BossFinalPolish/focused.xml`). This includes existing ending, dialogue interruption, camera/input foreign-owner, reward/exit, polarity cap and Region-3 first/repeat-route coverage.
- Full EditMode: **922/925**, zero skipped (`Logs/BossFinalPolish/full.xml`). Only `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)` fail. Names and complete failure messages exactly match the starting-tree Region-3 hotfix result: pre-existing sectorTechnologyLevels normalization, JSON lengths 1225→1501, 1257→1533, 1303→1579 at index 847. No save-normalization fix is included.
- Before-fix cleanup regression evidence: six failures in `cleanup-before-fix.xml`; final focused results are green. Actual Sector player-death before/after logs independently demonstrate the transition-cancellation fix.

### Actual 480×270 Play Mode method

The Editor-only probe opens the saved Expedition scene and uses its real generator, player, complete camera rig and HUD. Core intro/encounter handoff and the coreless Phase proximity path execute in Play Mode. Final scenarios copy the actual saved Boot DialogueSystemController/database and authored result Canvas, use real Pixel Crushers response callbacks, and Continue loads the saved Settlement through SceneFlowManager. Transient progression fixtures cover unavailable facilities (Accept) and level-1 facilities (Reject); no SaveManager is present and user save files are not written.

These are isolated scripted coverage runs: ordinary generated enemies are disabled, the player is invincible during kill coverage, and boss damage is scripted. Player-death cases restore actual damage and wait for PlayerDeathSequence/RunManager. RenderTexture and Canvas capture are 480×270. This establishes rendered behavior and structural execution, not natural damage intake, fair dodge windows or a human playthrough. The normal Editor was used because batch-mode rendering stalled the existing end-of-frame intro coroutine.

Per-case timestamps, HP, state/pattern changes, assertions and screenshot names are in `Logs/BossFinalPolish/Rendered/<case>/audit.txt`. Only captures named in the latest case log belong to that run; older diagnostic images may remain in the ignored log folders. Before-correction frigate evidence is retained under `FrigateBefore/`.

Ten completed scenarios pass **194 logged assertions** and produce **188 latest-run 480×270 captures**. Representative intro, warning/lock/damage, transition, reward, choice, polarity, ending/result and cleanup images were inspected. `Logs/BossFinalPolish/RENDERED_INDEX.md` links the exact current captures and logs; `rendered-summary.json` records totals. These counts include repeated dialogue-runtime checks, not 194 independent test cases.

| Scenario | Scripted gameplay duration | Coverage / observed repetitions | Cleanup / result |
| --- | ---: | --- | --- |
| Sector | 79.03 s | 36 s phase-1 hold with 23 cast entries; lethal gate at 70 HP; 10 s scripted shield/setup hold; approximately 29 s true phase 2 with two rotating-laser sequences | One death; movement/dash restored; choice and Beacon, no portal |
| Salvage | 61.95 s | Scripted 20/17/18 s holds for 3/2/1 alive; 4 N-way, 8 aimed, 3 warning, 4 homing, 3 ricochet, 4 laser and 4 rotating entries; final warning .84 s, lock .20 s, charge .56 s | 165→110→55→1→0; no health strand; one handoff; choice/Beacon; camera returns from 5.0625 to 4.21875 |
| Phase | 40.41 s | Six reflected firing entries across normal/special cycles; two exposure entries; first exposure exactly 5 s; special cycle approximately 19.9 s | Protected damage rejected; exposure damage accepted; one death; movement/dash; choice/Beacon; no portal |
| NULL Accept | 74.44 s | Approximately 20 s Phase 1, 4.5 s reclaim, unavailable-facility support completion, 22 s polarity and 20 s final scripted holds; five damage-phase entries in each combat phase | Treatment once, 150/60 HP floors; natural ending; visible result; exactly one FinalVictory; Continue→Settlement |
| NULL Reject | 73.67 s | Approximately 20 s Phase 1, .75 s rejection, explicit level-1 support (Weapon Lab takes 150→136), 22 s polarity and 20 s final holds; five damage-phase entries per combat phase | Treatment once, 150/60 HP gates; natural ending; visible result; exactly one FinalVictory; Continue→Settlement |
| Raider repeat | 48.07 s | Existing cover telegraph/highlights; one phase-2 transition after scripted damage; two authored escorts | Boss and escorts retire; unrelated source ownership preserved by regression tests; dash/choice/Beacon work |
| Four story player-death cases | Not natural fight timing | One forced lethal player hit per case; Sector specifically interrupted during phase transition | One Death result; scheduler/transition stopped and cinematic camera lock released in every case |

The durations include scripted holds and death/ending presentation before reward interaction; gameplay time pauses during dialogue. Pattern counts are observed starts and can include an attack interrupted by a scripted HP gate. Player hit counts, damage intake, natural DPS/TTK and practical retry difficulty are **not measured**. The repeat smoke did not grant region authorization merely to create a portal; authorized repeat-portal eligibility is verified by existing EditMode tests.

Both final branches were rerun after fixing a probe-only setup-order defect: AfterSceneLoad audio had preceded the transient game-state owner and did not observe RunResult, so the result panel correctly waited for a music transition that the fixture could not finish. The probe now rebinds audio through its existing lifecycle in Boot order and waits for the actual panel reveal before capture. No production audio/result/ending code was changed. Both visible result captures and actual Continue-to-Settlement checks pass.

### Performance findings

Production projectile/laser creation uses PoolManager where available; NULL keeps bounded reusable lists and a 26-packet cap with one encounter update owner. No new Update, packet scheduler, LINQ attack loop or global clear was added. Sector still allocates small arrays/lists for individual rotating/manager sequences and retains existing pool-missing Instantiate fallbacks. Those are not established frame spikes and were not speculatively rewritten.

Editor GC samples include Editor internals, logging, screenshot encoding, imports and instrumentation. Initial completed runs measured medians around 19–101 KB/frame and maxima around 5.7–12.9 MB; these cannot be attributed to combat or used as a standalone performance acceptance result. Standalone profiling remains required. Source-owned lifecycle regressions were fixed; no claim of zero GC is made.

### HUMAN BALANCE TODO / remaining presentation questions

- Assess all four bosses with ordinary movement/fire and a representative build. Invincibility/scripted HP gates do not measure hit count, player damage or natural kill time. No timing, HP, damage, projectile speed/count, homing, reward or economy retuning was made.
- Sector's wide shield setup makes actors small; the normal follow camera can clip the boss at the upper edge while the player remains at the entry side. Evaluate during active movement before changing the authored camera/arena.
- Phase's wide reflected attack can crop part of the boss silhouette at an edge. Its large exposed body can overlap the bottom boss HP strip. The reflection path and exposure work, but final framing/readability acceptance remains open; no arbitrary zoom or collider/art scaling was introduced.
- NULL's enlarged current core/shell art remains coarse, and the static entry-position view can clip its upper silhouette. Treatment dialogue shares the bottom region with the existing boss HUD. Dedicated art and active-play composition should be reviewed without changing dialogue/ending authority.
- Raider's existing distant side anchors can put its silhouette outside the static entry-position camera view. Its cover warning/highlight, phase-2 escort creation and cleanup pass structurally; active-play camera/readability approval remains separate from this repeat-route regression.
- Existing boss names, reward/result body strings and some HUD text remain Korean in the English fixture; the installed treatment/termination dialogue has English text. No broad localization rewrite was attempted. Captures show no new decorative missing-glyph marker.
- Each story boss has an actual player-death cleanup run and independent fresh encounter execution. The complete interactive death-result/relaunch/retry journey, physical controller input, save-to-disk/reload, and every possible interruption timing were not exercised. Existing EditMode ownership/retry/progression tests cover the structural contracts.
- Audio calls/bindings were exercised, but subjective mix and a complete ordinary Boot-to-boss music listening pass were not assessed.

### Exact changed files and preservation

Runtime: `Assets/02_Scripts/Boss/BossPatternController.cs`, `PhaseGatekeeperBossController.cs`, `PirateCommanderBossController.cs`. Saved asset: `Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab`. No scene, campaign definition, projectile, reward, material or sprite asset changed.

New Editor-only utilities: `Assets/02_Scripts/UI/Editor/BossFinalPolishAudit.cs`, `BossFinalPolishAuthoring.cs`, `BossFinalPolishProbe.cs`, and `Tests/BossFinalPolishTests.cs`, with Unity-generated metas. The authoring helper refuses dirty scenes/Prefab Mode and updates only the six formation properties and three matching transforms through PrefabUtility.

Documentation: this audit plus `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, `IMPLEMENTATION_STATUS.md`. Stale foundation-only NULL claims and eight-reflector/Region-3 Core-reward descriptions are corrected or marked historical.

A hash comparison against 11,193 task-start files finds only the three runtime controllers, one prefab and three requested existing documents changed. All 51 frigate object/component IDs and 171 nonzero serialized references are preserved. Unity's prefab save materializes existing null final-ending and hit-flash curve defaults and expands equivalent static-batch YAML; these are serialization-only, not tuning changes.

The refreshed resolved Unity snapshots differ only in the six intended formation fields and three part positions (`resolved-snapshot-diff.txt`). All other recorded prefab/controller/campaign/projectile/reward/Expedition bindings are identical. Scoped source/document/prefab whitespace checks pass after removing only 20 trailing spaces introduced by Unity's prefab serialization.

Normal enemies, Field Bases, Route Core Deck, Settlement DarkUI, inventory, equipment/economy, campaign route rules, save migrations, and the Region-3 repeat-operation hotfix remain unchanged from the current local starting tree. Existing working-tree edits are preserved. No commit or push.
