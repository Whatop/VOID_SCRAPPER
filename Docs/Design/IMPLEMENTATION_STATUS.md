# VOID SCRAPPER 구현 상태 매트릭스

## 2026-09-21 - Equipment Development UI refinement

Implemented against the current working tree; no commit or push. Settlement now
researches/inspects/prepares; expedition acquisition owns Lv1 through MaxLevel.
The saved Settlement panel has reusable dynamic effect rows, locked research details,
explicit free activation/deactivation and no normal permanent equipment Upgrade action.
Third-component analysis grants 12 slots before assembly; old assembled saves migrate
safely and F10 preset 11 tests that pre-assembly state.

Validation actually executed:
- Runtime and Editor/test static compilation: zero errors (existing warnings retained).
- Focused Equipment/campaign/save/runtime/Settlement/localization EditMode selection:
  182/182 passed.
- Full EditMode suite: 576/576 passed.
- Normal Localization Import/Validate and focused scene authoring/reference validation.
- Rendered 480x270 Boot -> Settlement -> Expedition smoke: Guidance inspection,
  active/inactive states, locked Breacher record, reflective-shield mechanic, free
  activation/save, F10 third analysis before assembly/backward replacement, prepared
  reward pool, unowned expedition start and normal Guidance Lv1 -> Max acquisition.

Remaining: subjective text/scroll comfort across all equipment and supported languages,
  controller navigation feel, and a natural full campaign playthrough. No catalog
  rebalance or equipment drawback system was implemented in this presentation pass.

## 2026-09-20 - Radar visibility hotfix

Rendered Boot -> Tutorial and Boot -> Expedition checks reproduced the same cause:
RadarScopePresentation lacked an authored CanvasRenderer in both scenes. Its active
60x60 scope and CanvasGroup alpha 1 produced zero mesh vertices, while the independent
ScanSweep Image still rendered. Neither low contrast nor an unfinished tween caused it.

Both existing scopes now have a CanvasRenderer. RadarScopeGraphic requires that
component, and authored validation rejects missing renderers, disabled graphics and
collapsed bounds. Both scene copies use an explicit 60x60 panel, lower-left anchors,
center (42,72), and full-stretch functional/marker bounds. No colors, alpha values,
DOTween timings, sounds, marker semantics or scan/map/stealth authority changed.
No runtime hierarchy construction was added.

Validation: runtime static compilation 0 errors / 52 existing warnings; Editor/test
static compilation 0 errors / 4 existing warnings; focused EditMode 96/96; full
EditMode 519/519. Three new regression cases check real rendered mesh geometry,
layout, scan/open/close/rapid-toggle/suppression recovery and invalid authoring.
Actual rendered Play Mode at 480x270 passed in both scenes, with isolated temporary
saves: each visible scope produced 450 vertices, alpha 1, scale 1 and screen bounds
(12,42)-(72,102). Captures confirm ordinary scope, markers and successful scan sweep.
No runtime assertions or exceptions occurred in the successful probe. Scene checks
found one added CanvasRenderer per scene, no removed IDs or dangling local references;
scoped whitespace and existing-work preservation checks passed. Temporary Editor
helpers were removed. This supersedes the earlier unverified Radar visual status.

Remaining: review all contact types in dense combat, natural Tutorial/boss cinematic
suppression and other supported aspect ratios. The probe enabled Tutorial Radar for
testing and did not complete the full narrative. The separate scene copies remain a
drift risk; the same parameterized authoring tests now validate both.

## 2026-09-20 - Tutorial cinematic and Radar polish

Implemented against the current local tree; no commit or push. Tutorial focus fades
ordinary HUD through the existing owner API, leaving Pixel Crushers presentation
independent. Ready Core float and stationary wave are separate; travel moves only the
Core visual. Radar's old sprite-frame playback is retired in both authored scene copies,
with one reusable sprite-free sweep and owned unscaled DOTween transitions. Marker,
scan/map/stealth/audio/progression behavior remains under existing authorities.

Validation actually executed:
- Runtime static compilation: 0 errors / 52 existing warnings.
- Editor/test static compilation: 0 errors / 4 existing warnings.
- Focused Tutorial/Radar/discovery/dialogue/Expedition UI EditMode suite: 134/134 passed.
- Full EditMode suite: 516/516 passed.
- Actual hidden Editor Play Mode, Boot -> Tutorial, isolated temporary save: Radar open,
  rapid toggle, successful scan/cooldown, boss suppression and manual close; HUD fade
  with timeScale zero; foreign HUD owner preservation; stationary wave, Core-only travel,
  disable interruption/restored parent, Ready restart and consumed-state non-restart.
  No runtime assertions/errors/exceptions occurred in the successful probe. Core stages
  were controlled by the test harness; this was not a full campaign/narrative playthrough.
- Both scenes retain every original object/component file ID, with no added/removed
  objects, no duplicate IDs and no dangling local references. New Radar/Core bindings
  pass authored tests. Existing outside-scope values survive Unity serialization.
- External GUID audit adds no missing references. Pre-existing unresolved references
  remain: Tutorial ship Part_* sprites (59f918cce96e97e44bfa4766bf4a5411), disabled exhaust
  Animator controllers in both scenes (9b15af481430d24458b4a876af70b716), and Expedition
  InterfaceTab legacy sprite reference (ef696ea238a6c7c4da05ebee40c886b1).
- Scoped hand-authored diff whitespace and preservation checks completed. Temporary
  authoring/probe scripts are archived outside Assets; no new assets or meta files remain.

Remaining: visually review both independent scene copies at 480x270; tune Radar border/
sweep contrast (alpha 0.24), five-pixel offset, 0.21/0.18-second transitions, 1.5-second
idle and 0.32-second sweep as needed. Review Core wave alpha cap 0.22, 1.6-second cycle
and 1.55 scale; play natural discovery/operator/Core acquisition through completion,
interrupt/retry and scene return, checking subtitle visibility, prompt restoration and
all marker categories. Verify boss suppression and world-pulse/audio alignment in real
combat. The full narrative and rendered visual checks are not claimed complete.

## 2026-09-20 - Boot runtime lifecycle and Options hotfix

Completed against the current local working tree, with no commit or push.

Diagnosis was reproduced in actual Editor Play Mode. On repeated entries, CoreRoot Awake
reported Boot / Assets/01_Scenes/Boot.unity with isLoaded=false; Start saw the same scene
loaded and retried persistence. Editor logs also showed Temp/__Backupscenes restoration.
The public scene API did not identify CoreRoot as already persistent before the request.
RunResult Start then created ResultCard in Boot, moved it to DontDestroyOnLoad, and parented
it under the persistent result panel. Isolating that runtime construction removed both
assertions across repeated entries; merely moving bootstrap persistence to sceneLoaded
did not. Final code combines the one-shot lifecycle guard with fully authored result UI.
No assertion filtering or suppression was added.

Settings blank-screen cause: first Open activated the initially inactive OptionsPanel;
its deferred SettlementSettingsPanel.Awake immediately called Close, after MainMenuController
had hidden Main. The four tabs/controls existed. Their original references were preserved;
only the guard array was repaired from FrameLimit/Fullscreen/Resolution to
Resolution/Fullscreen/FrameLimit. The canonical tab-controller component remains on the
same OptionsPanel, with exactly 18 rebind rows. Boot cannot invoke runtime Build.

Validation actually executed:
- Runtime static compilation: 0 errors / 52 existing warnings.
- Editor/test static compilation: 0 errors / 4 existing warnings.
- Focused Boot/options/scene-ownership/dialogue EditMode suite: 71/71 passed.
- Full EditMode suite: 507/507 passed.
- Actual hidden Editor Play Mode: three consecutive sessions in one process, with
  SafeReturn, Death and FinalVictory result data from RunManager.CompleteRun respectively.
  Each checked first/repeated Settings open, all four tab-button callbacks, active content,
  18 active keyboard rows, exact dropdown guards, Back, result committed/lost counters,
  Continue/fades to Settlement, released run-ending pause and return to Boot.
  All retained the same single Bootstrap and Dialogue Manager, created/reparented zero
  result objects, and produced zero DontDestroyOnLoad/SetParent assertions or runtime exceptions.
- Serialized Boot validation: 4,116 non-null references resolve; 60 existing blocks
  changed, 162 added, zero removed. Outside the result subtree, the only serialized
  change is the dropdown guard order. No other scene or prefab changed in this pass.
- Scoped hand-authored git diff checks and baseline preservation checks passed.

Twenty new Boot/options/result/unload regression cases extend existing tests; persistence
and migration tests were updated. Temporary authoring and Play Mode probe files/metas
were removed; snapshots, scripts, logs and XML remain in the local temporary
void-boot-hotfix folder. No new permanent assets/metas, localization or save flags.

Remaining manual checks: inspect actual rendered Settings tabs and result cards at
480x270, including scrolling all 18 bindings; check mouse/gamepad focus, display
apply/revert confirmation, audio levels and fade timing. The Play Mode probe used an
isolated temporary save and invoked button callbacks; it did not visually inspect pixels,
exercise settings-value changes, or play through Tutorial/combat/final-boss victory.

## 2026-09-20 - Unified Settlement Recovery / Route Core panel

Implemented in existing SettlementUIController and SettlementHUD. The exact handoff is
PermanentProgress.CurrentRouteCoreState == Assembled (and remains available in Activated).
Recovery Processor restoration/save remains explicit and authoritative. UI presentation
changes immediately without activation, defense, launch or travel. No save fields added.
Optional facilities remain reachable after the handoff; ordinary building levels do not
gate it. The independent progress panel, reinforcement selection and remote intro remain.

Unity authoring reuses Repair_HUD and the same five primary navigation slots, reparents
the existing deck button while retaining its persistent EnterDeck listener, adds a small
component summary and facility toggle, and binds existing owners/catalog. ReturnToFacilities
and the physical core remain unchanged. No new Player, Camera, runtime fallback or prefab.
The normal localization import contains 139 records (14 added; one description revised).
Temporary authoring/Editor bridge scripts are removed after use.

Validation actually executed:
- Runtime static compilation: 0 errors / 52 existing warnings.
- Editor/test static compilation: 0 errors / 4 existing warnings.
- Focused Settlement/recovery/navigation, campaign, corrupted-defense and localization
  EditMode suite: 324/324 passed.
- Full EditMode suite: 487/487 passed, including reinforcement, progress and remote dialogue regressions.
- Unity localization import/catalog validation and named-placeholder checks passed.
- Saved-scene serialized reference checks: 252 non-null references
  across 9 changed and 19 new blocks resolve;
  0 existing blocks removed.
- Scoped hand-authored diff checks and current-task preservation comparisons passed.
No Play Mode verification is claimed. Logs, XML, preservation snapshots and the one-time
authoring source are retained in the local temporary void-recovery-hub folder.

Play Mode checks: at 480x270 inspect Korean/English restoration and Route Core views;
complete story restoration and observe the in-place handoff before choosing travel;
verify mouse/keyboard/gamepad focus and optional facility access; enter/return repeatedly;
physically activate the core, retry/complete corrupted defense, launch the final route
from the physical core, then return after final victory. Confirm progress text and the
Route Core label persist across scene reloads and the existing save/load flow.

## 2026-09-20 - Settlement / Tutorial UX polish

Implementation: SettlementProgressPanelUI, shared Settlement selection colors, separate
card preview/commit callbacks, and an explicit remote-only DOTween intro in the existing
dialogue presenter/typewriter. The progress projection owns no progression or save state.
Details and Upgrade stay bound to the selected reinforcement card, including while another
card is hovered or keyboard-focused. The intro retains the existing incoming audio event.

Authoring: a one-time Unity editor pass bound the progress texts/catalog/controller,
adjusted sidebar spacing and set existing navigation/card colors in Settlement.unity.
The pass waited for the user's unsaved scene work to be saved. The temporary installer
and Editor bridge were then removed, preserving the existing no-completed-UI-menu rule.
No active PreviewScene or internal component-array manipulation was used.

Validation actually executed (2026-09-20):
- Runtime static compilation: 0 errors / 52 warnings.
- Editor/test static compilation: 0 errors / 4 warnings.
- Focused Settlement navigation/reinforcement, dialogue lifecycle/presentation,
  localization and audio EditMode tests: 110/110 passed, 0 skipped.
- Full EditMode suite: 472/472 passed, 0 skipped (16 added cases).
- Normal Unity localization import and catalog validation: 125 records.
- Against the user's saved Settlement baseline: 17 new serialized blocks,
  12 existing blocks changed for sidebar authoring, 0 existing blocks removed.
- Scoped hand-authored diff checks passed; new Unity-generated script GUIDs are unique.
No Play Mode verification was performed. Validation logs/XML and the one-time authoring
script are retained in the local temporary void-settlement-ux validation folder.

- Serialized reference check: all 192 non-null references in the 29 changed/new scene blocks resolve.


Play Mode checks still required: read the progress block at 480x270 in Korean and English;
return with each campaign milestone; hover/click/Submit different reinforcement cards and
verify target/cost/spend; check navigation on mouse/keyboard/gamepad; run Tutorial Operator,
relay/takeover, rescue and NULL DISPATCHER treatment/ending intros; interrupt/retry a remote
call; confirm local RescueContact and Settlement dialogue have no incoming intro. Assess
cue-to-panel-to-first-subtitle timing and repeat scene transitions. No Play Mode claim.

## 2026-09-14 - NULL DISPATCHER Pass 4E

Implemented: final combat cleanup before death VFX; optional final-only ending hook;
NullDispatcherEndingPresentation; six-subtitle termination conversation; neutral network
shutdown; ending -> existing FinalVictory -> RunResult -> existing Settlement return.
The ending has no progression writes or scene/run-completion calls. No new save fields.

Authored via Unity: ending component on PF_Boss_NullDispatcher binds existing inner core,
outer shell root, break pulse, and shared dialogue entry. No scene edits or new art.
Localization CSV gained seven keys; Unity imported/validated the 105-entry catalog.
The ending installer/validator installed one stable eight-node graph (six subtitles,
zero player choices), reusing the existing Dispatcher and Operator actor identities.

Validation artifacts: Logs/AgentValidation/Null4E. Focused NULL DISPATCHER EditMode:
94/94 passed after correcting one new pure fixture's missing resolved FinalNetwork
context (first run 93/94). Production campaign resolution was not changed.
Final validation:
- Runtime static compilation: 0 errors / 74 warnings; Editor/test: 0 errors / 4 warnings.
- Full Unity EditMode suite: 436/436 passed, zero skipped (21 cases added to 415).
- Includes 94 NullDispatcher cases (20 named Pass 4E plus the expanded existing victory
  integration test), 64 Phase2CStoryDialogue, 27 LocalizationFoundation, 4 BossRecovery,
  8 StoryRecovery and 12 CorruptedDefense cases.
- Localization Import/Validate: 105 records. Ending Install/Validate passed. All 17 prior
  conversations and actor fields remain unchanged; the ending is conversation 18.
- Prefab validation: 21 external GUIDs resolved, no missing local object references.
- Scoped hand-authored diff/whitespace checks passed. Unity-generated output retains
  serializer trailing spaces (catalog 21, database 72, prefab 18); it was not hand-edited.
- No Play Mode was executed. All unrelated source/prefab/scene hashes match the initial
  working-tree snapshot. No commit or push.

Manual acceptance still required: play through both treatment branches, polarity and
20% shell break; kill the exposed core; verify no damage/healing packets remain, ending
locks cancel dash safely, six subtitles remain readable at 480x270, interrupted dialogue
reoffers once, final shutdown precedes one RunResult, and Continue returns to Settlement.
Test pause and scene/run cancellation during the ending; missing dialogue in a temporary
Play Mode instance must log a failure and reach controlled shutdown rather than hang.

Dedicated final artwork, final audio pass, rolling credits, and combat/presentation
balancing remain polish. Fixed 6-10 second wall-clock duration is not forced: the 2.5-second
visual beats surround existing user-paced subtitles, which may take longer to read.

## 2026-09-14 - NULL DISPATCHER Pass 4D

Implemented: <=20% FinalTransition, shell break, purple inner core, two reduced final
combinations, preserved WHITE/BLACK polarity, 3.5-second support cadence and final cleanup.
The 50% dialogue/Accept/Reject/facility support paths remain; strong Weapon Lab strikes
cannot bypass the final gate. The inner core is selected for existing boss death VFX.

Authored: OuterShellRoot plus four Square-sprite remnants and InnerCoreRoot (core5) on
PF_Boss_NullDispatcher. Existing root collider, health, campaign ID and Core binding remain.
Two new CSV keys: final.phase.shell_break and final.phase.inner_core. Unity Import Catalog
and Validate Catalog completed successfully (98 entries, all 89 prior catalog keys retained).
The import also includes the seven previously pending Pass 4B/4C CSV keys. No generated YAML
was hand-edited. No dialogue installation or Inspector assignment is needed.

Validation under Logs/AgentValidation/Null4D:
- Runtime static compilation: zero errors (74 warnings); Editor/test: zero errors (4 warnings).
- Unity focused NullDispatcher run: 71/71 passed before two further scheduler tests.
- Final full Unity EditMode run: 415/415 passed, zero skipped. Includes 73 NullDispatcher
  cases (12 Pass 4D), 27 LocalizationFoundation, 64 Phase2CStoryDialogue, 4 BossRecovery,
  8 StoryRecovery, 12 CorruptedDefense, and existing FinalVictory ownership coverage.
- First full run was 414/415: only stale generated catalog failed. Existing importer and
  validator resolved it; final run passed. CSV validation succeeded for all 98 records.
- Two older Pass 4B tests initially read zero velocity from inactive Rigidbody fixtures.
  Tests now bind a live fixture-owned physics body without enabling gameplay components
  or invoking private Unity lifecycle methods. Player production code was not changed.
- Prefab local/external reference checks passed. Hand-authored scoped diff check passed.
  Unity-generated catalog YAML retains serializer trailing spaces; it was not reformatted.

Manual Play Mode has NOT been performed; combat balance and 480x270 readability remain
pending. Reopen/import the changed boss prefab; localization is already installed and valid.

Manual path: complete campaign prerequisites -> FinalNetwork -> 50% dialogue -> test
Accept and Reject in separate runs -> support -> PolarityPhase. Cross 20% from ~23% with
one large hit; confirm HP clamps, packets/lasers clear, controls remain usable and polarity
is preserved through shell break. Pause and cancel during the transition. After exposure,
verify combinations A/B, 3.5-second supply, safe polarity switches and ordinary damage
through 0 HP -> existing FinalVictory/RunResult exactly once, without beacon or portal.

Deferred: ending cinematic/dialogue, credits, dedicated shell/core art, final audio polish,
and combat balance/readability adjustments informed by Play Mode testing.

## 2026-09-13 - NULL DISPATCHER Pass 4C

Implemented in source and authored assets; static verification only. This supersedes
Pass 4B's temporary PostSupportCombat handoff below.

- Facility support completion -> PolarityPhase, initially WHITE, with one main scheduler.
- Opposite-polarity hostile diamonds (1 normal damage) and matching support plus packets
  (1 Heal, ~5-second cadence), pooled and capped at 24 hostile / 2 support.
- Two completed patterns -> disable spawning/collision -> clear both kinds -> 0.5-second
  warning -> toggle/HUD update -> resume. All Phase-2 gameplay timing pauses normally.
- Persistent bound 8-point HUD label in Expedition.unity; two explicit packet references
  on PF_Boss_NullDispatcher. Existing Square sprite art, separate fill/outline colors.
- Existing Accept/Reject, facility effects, 50% gate release and FinalVictory are retained.
  No permanent polarity store, new player control owner or new support manager.

Validation artifacts: Temp/Null4C-runtime.log, Temp/Null4C-editor.log,
Temp/Null4C-localization.log and Temp/Null4C-authoring-validation.txt.
Runtime and Editor/test static builds pass with zero errors. Actual CSV validator:
96 records, zero errors, 17 advisory row-order warnings. Scoped whitespace/reference and
protected-file preservation checks passed. Added tests compile but have NOT run in Unity.
Unity is open with an active project lockfile and no connected in-Editor runner.
Manual Play Mode remains pending; do not treat this entry as runtime acceptance.
The broad scene scan also found existing unresolved fallbackShipIcon GUID
 ef696ea238a6c7c4da05ebee40c886b1 and Animator-controller GUID
 9b15af481430d24458b4a876af70b716; neither is introduced or modified here.
Whole working-tree scene whitespace still includes prior changes; this pass's scene
addition passed a separate check against the reconstructed, hash-verified local baseline.

Editor follow-up: allow asset import; run VOID SCRAPPER > Localization > Import Catalog,
then Validate Catalog. Reopen Expedition after resolving any local unsaved scene edits.
No Inspector assignment or dialogue reinstall is required. Run NullDispatcher, localization,
dialogue, StoryRecovery/BossRecovery and CorruptedDefense EditMode tests, then the full suite.

Play Mode: reach FinalNetwork normally; test Accept and Reject in separate expeditions.
After support, confirm WHITE HUD and black diamonds/white plus packets, ordinary damage
mitigation and 1-HP healing. Observe at least two switches, pause during flight/warning,
then cancel/leave and kill the boss in another attempt. Check controls, packet cleanup,
HUD disappearance and exactly-once FinalVictory at 480x270.

Deferred: <=20% phase, dual-pattern climax, shell break, inner core, ending, final art,
and balance/accessibility polish based on actual Play Mode feedback.

## 2026-09-13 - NULL DISPATCHER Pass 4B

Implemented in source and authored prefab; statically verified, not Play Mode verified.
This supersedes Pass 4A's temporary immediate PostChoiceCombat behavior below.

- Accept: 4.5-second playable reclaim, source-owned inward force, movement/dash enabled,
  source-owned weapon lock, direct non-lethal HP drain, authored purple beam and break pulse.
- Reject: no beam/drain; 0.75-second reaction then the same intervention/support authority.
- Both: 1.5-second non-modal intervention message, existing building-scaled facility support
  exactly once, C# completion -> PostSupportCombat. Boss protection opens for Weapon Lab;
  player fire remains locked until support finishes. Existing FinalVictory remains intact.
- Support cleanup now removes its own engine move/dash modifiers. No new save state,
  manager, health system, dialogue runtime, inventory state or permanent player HP floor.

Validation:
- Runtime and Editor/test static builds pass with zero errors; warnings remain.
  Logs: Temp/Null4B-runtime.log and Temp/Null4B-editor.log.
- Actual LocalizationContentValidator/NamedPlaceholderUtility console harness: 93 rows,
  zero errors, 17 advisory warnings. New messages use no positional placeholders.
- Prefab local/GUID references, scoped whitespace and protected-file preservation checked.
- New/updated QA tests compile; Unity EditMode/full suite and Play Mode were not run.
  Existing Unity processes remain open, with no connected in-Editor runner available.

Editor/manual acceptance still required:
1. Allow source/prefab import; Localization > Import Catalog, then Validate Catalog.
   The existing treatment conversation is already present in the local database. Validate
   NULL DISPATCHER Treatment; reinstall with its deterministic installer only if needed.
2. Run NullDispatcher/NullDispatcher4B QA cases, Phase2CStoryDialogueTests,
   LocalizationFoundationTests, StoryRecovery/BossRecovery, corrupted-defense tests and
   the full EditMode suite. Tests explicitly advance coroutine contracts without pretending
   that EditMode ran a gameplay frame loop; no active PreviewScene/internal array hacks.
3. Boot -> explicit Recovery/Route Core progression -> three-core stabilization defense ->
   Final Expedition -> FinalNetwork Core -> 50% treatment. Accept: resist/dash during beam,
   confirm no firing and paused gameplay stops drain; check 20->10, 14->10 and 8->8 before
   facility healing. Observe safe break push, readable Operator line, support and combat.
4. On a separate expedition Reject: no beam or HP loss, reaction, same support, then combat.
   Verify unavailable buildings, Weapon Lab HP reduction, healing/Armor/engine effects,
   and FinalVictory. Exit/die during each branch/support; verify no residual locks/forces/
   buffs, no delayed support and a fresh choice on the next expedition.

Pending: BLACK/WHITE polarity, friendly support packets, polarity-switch projectile
cleanup, final 20% shell break, ending and final artwork. PostSupportCombat is temporary.

## 2026-09-10 - NULL DISPATCHER Pass 4A

Implemented in source/authored prefab, statically verified; supersedes historical
foundation-only final-boss descriptions below. Unity runtime acceptance is pending.

- NullDispatcherBossController: single Phase-1 loop, three telegraph/execution/recovery
  patterns, 50% treatment protection, encounter-only Accept/Reject, interruption retry,
  source-owned cinematic cleanup and temporary PostChoiceCombat. Core starts it at the
  existing final intro handoff. FinalVictory remains BossDummyController-owned.
- PF_Boss_NullDispatcher adds the controller, DialogueStoryEntryPoint and two authored
  relay SpriteRenderers. Reuses existing core5, laser prefab/material and enemy projectile
  definition. No scenes or canonical Core.finalBossPrefab reference change in this pass.
- Legacy support HP subscription removed; TriggerSupportNow remains for later orchestration.
- New deterministic NullDispatcherDialogueInstaller authors only the named final graph
  and stable NullDispatcher actor. Nine new CSV rows; generated database/catalog unchanged.

Validation actually performed:
- .NET Runtime and Editor/test builds: zero errors (existing/deprecated-field warnings remain).
  Logs: Temp/Null4A-runtime.log and Temp/Null4A-editor.log.
- Existing executable harness invoking actual LocalizationContentValidator and
  NamedPlaceholderUtility: 89 CSV rows, zero errors, 15 length/advisory warnings.
- Scoped whitespace, prefab GUID/local reference and unchanged protected-file checks.
- New QA/Phase2C tests compile. Unity Test Runner was not executed: this checkout has
  existing Unity processes and no connected in-Editor runner; no full-suite result claimed.
- No Play Mode verification. Attack balance, relay travel, 480x270 framing/response layout,
  pause/scene-exit and natural/interrupted dialogue integration still need Unity verification.

Required Editor sequence:
1. Allow script/prefab import. VOID SCRAPPER > Localization > Import Catalog, then Validate Catalog.
2. VOID SCRAPPER > Dialogue > Install NULL DISPATCHER Treatment, then Validate NULL DISPATCHER Treatment.
3. Run NullDispatcher tests in QAStabilizationPass1Tests and Phase2CStoryDialogueTests;
   existing final campaign, localization, corrupted-defense and StoryRecovery/BossRecovery
   tests; then full EditMode suite. No active PreviewScene or internal component-array hacks.
4. Boot -> restored/assembled Route Core -> activate -> complete three corrupted-core
   defeat/reactivation/fusion phases -> launch final expedition -> FinalNetwork Core intro.
5. Observe each attack's warning/gaps/recovery. Cross 50% with a large hit during each
   attack; verify hazards stop and HP cannot fall further. Finish each choice on separate
   expeditions, interrupt/re-offer, and leave scene during treatment. Verify no automatic
   support and no stale movement/dash/fire/camera/pause ownership; kill afterward for FinalVictory.

Still NOT implemented: Accept Reclaim Beam/resistance/HP drain, NPC intervention,
Reject-specific transition, BLACK/WHITE polarity/support packets, final shell-break phase,
ending and final art. PostChoiceCombat intentionally repeats Phase 1 until those passes.

## 2026-09-10 - Settlement Defense Rework

Implemented and statically checked: the live three-anchor prototype is replaced by the
corrupted three-component stabilization encounter. The older Pass 3 anchor description
below is historical. Unity Test Runner and Play Mode acceptance are still pending.

Authored changes:
- Existing Campaign/SettlementDefenseEncounterController remains the owner (there is
  no duplicate controller under Settlement/). One new SettlementDefenseCorruptedCore
  script and PF_SettlementCorruptedCore prefab provide combat health, a separate recovery
  interaction collider, core5 visual, and an existing enemy projectile definition.
- Settlement scene binds three configurations in green/blue/orange order, the actual
  ReturnToFacilities GameObject, existing activated purple renderer and one authored
  CorruptionWave child. No runtime fallback UI or new permanent central Core is created.
- Defense HP: green 70, blue 70, orange 90; no Raider adds. Defeat leaves a recoverable
  core. Interaction and completed fusion are required before the next phase. Three
  fusions trigger existing completion/save; management returns without auto-launching.
- Failure/cancel preserves Activated, all three permanent story parts and curse state.
  Retry is unlimited, with full opening and fresh local state. Owned tweens/projectiles
  and navigation overrides clean up. No cinematic movement/dash/weapon locks are used.
- FinalNetwork/NULL DISPATCHER foundation, Core.finalBossPrefab, PermanentProgress,
  SaveData schema, region campaign routes and dialogue authority were not changed.

Validation:
- Available .NET Runtime and Editor/test builds pass with zero errors; warnings remain.
  Logs: Temp/DefenseRework-runtime-build.log and DefenseRework-editor-build.log.
- Scene/prefab reference, parent/component ownership, configuration ordering, CSV keys
  and byte-for-byte unchanged final bindings checked by Temp/validate_defense_rework.py.
- Existing QA suite extended for sequence, defeat/interact/fuse separation, exact-once
  completion, failure/retry, cancellation through intro/combat/recovery/fusion, UI
  restoration and preservation of unrelated input ownership. Tests compile; not run.
- Unity is open in this checkout; no connected runner execution is available. No
  focused/full EditMode or Play Mode result is claimed.

Unity acceptance path:
1. Preserve unsaved Editor changes, let Unity import/recompile, and reload Settlement.
   Import/validate Localization.csv through the existing localization tooling. All new
   scene/prefab references are authored; no dedicated sprite assignments are required.
2. Recover all three parts through the campaign. Use Recovery Processor explicitly;
   confirm restoration/assembly, then enter the Route Core deck and activate it.
3. Verify inward movement, purple pulse/blast and three visible corrupted cores. Confirm
   the facilities button is hidden, HP/objective remain visible, and move/dash/fire work.
4. Fight green radial pressure. At zero HP it must stay visible and stop attacking.
   Blue/orange must remain inactive. Move to green and interact once; verify cleansing
   and flight into the central Core before blue activates. Repeat blue then orange.
5. Pause/unpause during introduction, combat and fusion. Check input, visibility and
   scene-exit cleanup; never grant a clear merely by leaving/changing GameState.
6. Die during combat and interrupt a fusion via normal scene exit. Re-enter/retry;
   verify three fresh cores, zero stabilized, full HP, no old bullets or lost parts.
7. Complete all three fusions. Verify stable-Core feedback, restored facilities, saved
   defense clear and explicit final launch eligibility. Save/reload and launch through
   the existing guarded Route Core path to FinalNetwork and its unchanged final prefab.
8. Run CorruptedDefense_* and campaign/final-binding tests in QAStabilizationPass1Tests,
   then the full EditMode suite when practical; regress Region 1-3 campaign progression.

Remaining: dedicated colored/core corruption artwork, pulse polish, audio, combat
balance and 480x270 readability acceptance. The foundation is ready for the next final
boss implementation pass structurally; runtime acceptance above remains required.
NULL DISPATCHER patterns, narrative choice/support phases and ending remain unimplemented.

## 2026-09-09 - Campaign Vertical Slice Pass 3

Status: **the three missing authored-content connections are implemented and statically
checked; Unity Test Runner and manual Play Mode verification are still required.**
This supersedes Pass 2's missing-content list. No commit or push. Real NULL DISPATCHER
combat and ending are not implemented.

Settlement.unity now contains:

```text
Canvas/SettlementHUD/RouteCoreDeckButton -> EnterDeck
SettlementDefenseEncounter (one concrete controller; always active)
RouteCoreDeck (inactive until entered)
  SettlementDefensePlayer (copied authored Expedition player components/visuals)
  RouteCoreRoot (trigger collider + SettlementRouteCoreController)
    Missing / Ready / Assembled / Activated
  PlayerStart / AnchorWest / AnchorEast / AnchorNorth
  Global Light 2D
RouteCoreDeckHUD (inactive until entered)
  DefenseObjective / DefenseVitals / RouteCoreInteraction / ReturnToFacilities
```

- All Route Core HUD/controller/stage references are authored. defenseRequested has
  one persistent BeginEncounter listener. autoCompleteDefenseForPrototype is false.
  No additional defenseCompleted/final-launched event is needed; existing methods own
  the save and launch. ReadyToAssemble world interaction directs to Recovery Processor;
  that processor's explicit restoration already assembles the key/Core exactly once.
- Defense has three fixed spawn points: (-4,-1.8), (4,-1.8), (0,2.3). Each creates one
  stationary 100-HP PF_SettlementTransmissionAnchor and two 24-HP Raider escorts using
  Enemy_Basic / Enemy_Shotgun, the current Raider Commander escort references. Anchors
  use EnemyHealth, a fixed Rigidbody2D, collider and red-tinted core5 visual. No cargo,
  resource payout or separate campaign store. Tuning target is 1-3 minutes, not timed
  completion; actual duration/balance needs Play Mode measurement.
- Three unique dead-anchor callbacks call RouteCore.CompleteSettlementDefense once.
  Destruction of one or two, a duplicate event, a live-anchor callback, timers, scene
  state changes and cancellation do not grant a clear. Remaining escorts/projectiles
  clean up. Player death returns to facilities; re-entering the deck restores the
  selected permanent build and permits retry without creating/ending an expedition.
- The deck switches the existing camera to an orthographic 480x270 / PPU-32 view for
  the visit, restores its previous projection on exit and owns a viewport constraint.
  It does not acquire movement/dash/fire/cinematic/pause locks. UI uses authored TMP
  labels, existing SettlementHUD messages and InputBindingUtility for interaction keys.
  Anchor reveal uses an unscaled owned DOTween; gameplay destruction is independent.
- PF_Boss_NullDispatcher is bound on Assets/03_Prefabs/Object/Core.prefab, the canonical
  Core used by Expedition generation. It references BossCampaign_Final (NullDispatcher,
  FinalNetwork), EnemyHealth 300 HP, fixed Rigidbody2D/collider, BossDummyController,
  BossDeathPresentation, enlarged purple-tinted core5 art, and disabled existing support
  hooks. No Sector Administrator/Phase Gatekeeper attack controller is attached.
  It uses existing intro/HP UI and FinalVictory. It has no attacks of its own yet.
- GameStateManager now recognizes SettlementDefense and FinalBossBattle as gameplay;
  EmergencyReturnController applies its boss restriction to FinalBossBattle too.
- No Region 1-3 boss, story-part inventory, save schema, dialogue graph, campaign
  definition, generated localization/database or vendor asset was changed in this pass.

Validation performed:
- Static runtime compile, including the new concrete controller, and Editor/test
  compile passed with zero errors. Logs: Temp/CampaignPass3-runtime-build.log and
  Temp/CampaignPass3-editor-build.log. The ignored generated C# project received the
  new Compile item for local static validation; Unity regenerates it normally.
- Serialized-reference audit resolved new local IDs and Assets/PackageCache GUIDs,
  component ownership and transform backlinks. It confirmed 153 new Settlement
  records, four narrowly changed existing records, no removed records and no edits
  to existing component arrays. Logs: Temp/CampaignPass3-authoring-validation.log.
- QAStabilizationPass1Tests extended; existing Phase2C campaign tests remain applicable.
  New tests use owned inactive fixtures and the existing non-active preview authoring
  reader; no active PreviewScene or internal component-array manipulation.
- Unity is open in the current checkout. No connected Test Runner execution was
  available; focused/full EditMode and Play Mode were NOT executed. No runtime issue
  or balance target is marked manually verified by this pass.

Exact Unity follow-up (all required Inspector bindings are already authored):
1. Let Unity import/compile new assets. Reload the updated Settlement scene from disk,
   preserving any separate unsaved Editor edits first. Inspect RouteCoreDeckButton,
   RouteCoreRoot stage references, the one defenseRequested listener, three anchor
   points, both Raider prefabs and the false auto-clear flag. Inspect Core.finalBossPrefab.
2. Run QAStabilizationPass1Tests and Phase2CStoryDialogueTests, then full EditMode.
3. Before all parts, enter the Route Core deck: inspect missing-part feedback and
   movement/dash/binding-aware interaction. Return to facilities. At three parts,
   verify ReadyToRestore; the world Core must direct to Recovery Processor without
   restoring. Perform the existing explicit Recovery Processor restoration.
4. Enter the deck again. Core must show Assembled. Interact to activate/save and start
   defense. Confirm three anchors/six escorts, objective 3/3, lit sprites, normal
   movement/dash/fire and readable UI at 480x270. Do not use debug completion.
5. Destroy one and two anchors: 2/3 and 1/3, final launch still locked. Kill the last:
   remaining defenders/projectiles disappear, completion message appears, state returns
   to Settlement and Core offers final launch. Repeat interaction cannot replay defense.
6. In a separate incomplete attempt, leave the deck or die after destroying 1-2 anchors.
   Confirm no clear/save inflation, facilities return, no stale camera/input/pause state,
   and a fresh three-anchor attempt on re-entry. Also test scene exit mid-reveal.
7. Save/reload after successful defense: Activated + SettlementDefenseCleared persist.
   Confirm active dialogue blocks final launch. Launch through Route Core: RunManager
   creates FinalNetwork, Expedition loads, and its Core resolves PF_Boss_NullDispatcher.
8. Activate the final Core, finish intro and check NULL DISPATCHER HP/visibility,
   movement/dash/fire and blocked Emergency Return while the boss is alive. Damage
   the stationary prototype to death and verify existing FinalVictory once. This is a
   plumbing test, not verification of real final combat or an ending.
9. Recheck Normal Raider -> DeepZone1 Raider -> DeepZone2 and the first-clear
   Beacon/Settlement-analysis gates; Region 3 must still offer no FinalNetwork portal.

Remaining content: dedicated Route Core/anchor/final spherical-shell art, deck scenery,
defense difficulty/audio/readability polish, and Play Mode confirmation of the whole
campaign path. The 50% dialogue/choice, treatment resistance, polarity, NPC support,
combined final phase, corrupted-shell breakup and ending are work for later passes.

## 2026-09-09 - Campaign Vertical Slice Pass 2

Status: **partially completed; final playable handoff is blocked by missing content**.
No commit/push. All earlier working-tree changes remain intact. No scenes, prefabs,
campaign definitions, generated dialogue/localization assets or vendor files changed
in this pass.

Implemented and statically compiled:
- Synchronous Triad/Phase Gatekeeper combat cleanup before shared death presentation
  and BossBattle release; Triad keeps its death/reward authority enabled.
- Phase Gatekeeper owned camera focus/offset cleanup and owner-driven dash cancellation.
- Portal bootstrap preserves an empty active slot instead of regranting starter gear.
- Non-Normal run creation rejects missing progression authority. Final prerequisites
  remain assembly/restoration, activation and Settlement defense clear.
- Missing authored defense callback fails without changing GameState; request,
  cancellation and completion ownership are idempotent.
- Three existing Localization.csv analysis lines clarified, without graph/action changes.

Inspected existing playable-code path:
- Core.prefab binds Region 2 to PF_Boss_SalvageDevourer_FrigateTriad and cleared regions
  to PF_Boss_RaiderCommander. Expedition.unity binds PF_Region2BossCorridorRuntime,
  PF_Boss_PhaseGatekeeper and its reflector prefab. Region 3 first clear stays Coreless.
- Unique MatterCompressor / PhaseNavigationLens are saved before generic recovery
  flight. StoryRecoverySection reads PermanentProgress directly. First clear gives
  Beacon only; natural Active2 analysis commits DeepZone2 via the existing save owner.
- Repeats preserve rewards and the active run through the existing Beacon/Portal
  choice. Region 3 cannot chain into FinalNetwork. Recovery remains explicit.

Exact missing authoring/content (not fabricated or bypassed):
1. Assets/01_Scenes/Settlement.unity has no SettlementRouteCoreController instance
   (script GUID 00a9873f3e754509a3801259163f5ee8). No prefab instance exists either.
   The Recovery Processor UI currently restores to Assembled; it does not expose
   Route Core activation/defense/final launch. Author the existing controller's
   interaction/collider and SettlementController/SettlementHUD references when the
   intended Route Core object is available.
2. No Settlement defense encounter/controller/prefab is present for defenseRequested.
   Supply the real encounter and wire its begin method as an enabled persistent
   listener, its successful completion to CompleteSettlementDefense, and abort to
   CancelSettlementDefenseRequest. Keep autoCompleteDefenseForPrototype off.
3. Assets/03_Prefabs/Object/Core.prefab has no finalBossPrefab binding; no NullDispatcher
   prefab/controller exists in this checkout. BossCampaign_Final.asset and final
   support/victory hooks alone do not provide an encounter. A project-authored final
   placeholder/reference is required before final handoff can be called playable.

Validation actually performed: dotnet runtime build (70 warnings, zero errors),
Editor/test build (4 additional warnings, zero errors); scoped whitespace, CSV and
serialized GUID/reference checks. Logs: Temp/CampaignVerticalSlice2-runtime-build.log
and Temp/CampaignVerticalSlice2-editor-build.log. Existing QAStabilizationPass1Tests
and Phase2CStoryDialogueTests extended. Unity is already open in this checkout; no
connected runner was available. Focused/full EditMode and Play Mode were NOT executed.
The user's prior movement/dash confirmation does not verify this pass's changes.

Unity follow-up:
- Import Catalog then Validate Catalog under VOID SCRAPPER > Localization. Only CSV
  text changed, so no dialogue graph reinstall is needed for this pass.
- Run QAStabilizationPass1Tests and Phase2CStoryDialogueTests, then full EditMode.
- Leave StoryPartSprite None. From an R1-cleared/authorized save, start a normal run,
  kill Raider, select the Region-2 portal, record/compare HP, Armor, credits, tuning
  chips, cargo/resources, traits, active equipment/charges, weapon and ship.
- First Region 2: finish corridor/Triad, including final-part destruction; check moving
  and dashing at death, generic flight and second slot, selectable rewards, Beacon
  at death and no portal. Return, interrupt analysis (no unlock), replay naturally
  (DeepZone2 saved), repeat dialogue (no mutation), and save/reload.
- New Normal run: Raider -> Region 2 Raider -> Region 3. Check paired exit reveal and
  carryover, including an intentionally empty active slot. Reach Coreless Phase
  Gatekeeper, test intro/phase-boundary death/interrupted presentation cleanup, recover
  the lens and all three inventory slots, then Beacon only. Never a FinalNetwork portal.
- Settlement must show ReadyToRestore. Use Recovery Processor explicitly to reach
  Assembled. STOP here until missing Route Core/defense authoring is supplied.
- Once real content is wired: explicitly activate Route Core, complete defense,
  save/reload, launch final expedition and confirm CurrentRun.FinalNetwork plus the
  assigned NullDispatcher encounter. Earlier final launch attempts must be blocked.
  Do not implement final combat/ending as part of verification.

## 2026-09-09 - Story Recovery Inventory Presentation

Implemented in the current local working tree without commit/push. The user confirmed
that the earlier boss-death movement/dash regression is fixed. This pass preserves
that control/state flow; its new inventory/presentation changes still need Play Mode.

- World recovery runs with Story Part Sprite = None, using the existing authored
  generic core. Dedicated definition sprites override world visuals and inventory
  icons automatically. No dedicated art or Inspector wiring is required for testing.
- PF_ExpeditionMapInventoryMenu.prefab now has InventoryRoot/StoryRecoverySection:
  header plus three horizontal slots (SectorStabilizer, MatterCompressor,
  PhaseNavigationLens). Each has name/status TMP, reserved disabled icon, CanvasGroup,
  and acquired highlight. No Selectable or new ScrollView. The inventory root grows
  from 430x232 to 430x256; header moves up 12 pixels and existing Content moves down
  14 pixels as a unit, retaining internal cargo/equipment geometry and bindings.
  Expedition.unity uses an unpacked inventory copy, so the same row and four narrow
  record changes are authored there too. Tutorial inherits the prefab directly.
- State reads PermanentProgress.HasBossStoryPart. Existing open/close lifecycle binds
  and unbinds progress/language events. World completion pulses only an open slot;
  the UI never opens itself or persists a separate acquired/new state.
- Six CSV-only ui.story_recovery keys added. Run VOID SCRAPPER > Localization >
  Import Catalog, then Validate Catalog to regenerate localization through its normal
  importer. Korean fallback labels remain readable before import; no generated catalog
  or Dialogue Database YAML was edited.

Validation: static runtime and Editor/test compilation passed, plus scoped whitespace,
CSV and prefab-reference/layout checks. Extended QAStabilizationPass1Tests; Unity Test
Runner was unavailable in this session and Play Mode was not executed. Logs:
Temp/StoryRecoveryInventory-runtime-build.log and -editor-build.log. Prior user
confirmation applies to movement/dash, not the new row, pulse, or generic flight.

Required Play Mode checks before/while continuing Region-2 campaign testing:
1. Leave all three Story Part Sprite fields None. Open inventory through the existing
   binding, verify three unacquired names/statuses and no question-mark icons. Check
   the row at 480x270 and confirm cargo, active/passive, map tabs and close still work.
2. Fresh Region-1 first kill: generic core rises at the resolved boss death position,
   holds, flies into the moving player, then shows pickup/message/rewards and Beacon.
   Move/dash/fire after breakup. Inventory must stay closed until opened manually.
3. Open inventory: Stabilizer is acquired; other slots are dim/unacquired. Save/reload,
   death or Emergency Return must preserve it without changing cargo load. A repeat
   boss must not replay a first-acquisition flight or create a duplicate part.
4. In a development case with inventory already open, complete a recovery visual:
   only its matching slot pulses after completion. Close/disable/end the run mid-pulse
   and mid-flight; verify no stale tween, scale, or control lock.
5. Temporarily assign a dedicated sprite to a campaign definition: world recovery
   and its inventory icon both use it. Clear it: world uses generic, inventory hides
   only its Image component. Geometry and text must remain stable.
6. Continue DeepZone1 first-clear testing: Matter Compressor recovers/appears once;
   Beacon only before Settlement analysis. Preserve route authorization and the
   Region-3 Recovery/Route Core chain with no direct FinalNetwork portal.

Remaining: dedicated part artwork is optional polish; visual readability, open-panel
pulse, interruption and Region-2 integration require the above Play Mode checks.

## 2026-09-09 - Boss Death Recovery Hotfix

Implemented in the current dirty working tree; no commit or push. The reproducible
source race was snapshotting MovementLocked during dash and restoring true after
dash ended. Phase setup and intro shared that defect. Death now cancels active dash
through its owner and releases only its external input locks. BossBattle previously
ended after selectable rewards; it now ends at accepted ordinary death/combat stop.

Dedicated story art is still unassigned. No project asset or sprite subasset was
identified as the three named parts; the shared prefab uses Space Kit/Core/core5.png.
Assign **Story Part Sprite** on these existing assets under
Assets/02_Scripts/Resources/Campaign/BossDefinitions/:
- BossCampaign_Region1.asset: Sector Stabilizer (SectorStabilizer).
- BossCampaign_Region2.asset: Matter Compressor (MatterCompressor).
- BossCampaign_Region3.asset: Phase Navigation Lens (PhaseNavigationLens).

No scene/prefab reconstruction or additional binding is needed for the control and
anchor fixes. Missing part art intentionally skips its flight while the saved
acquisition and localized message remain available. New/changed localization: none.

Deferred UI: Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab, InventoryRoot,
owned by PlayerBuildStatusPanelUI. There is no dedicated story-recovery container.
The next pass should author three read-only slots there and refresh them from
PermanentProgress.AcquiredBossStoryParts/Changed and the same definition sprites;
keep them separate from passive storage, field-drop actions, and cargo capacity.

Validation: static runtime and Editor/test compilation performed; focused regression
sources extended in QAStabilizationPass1Tests. Unity Test Runner and Play Mode were
not executed by this hotfix. The project is already open in Unity and this session
has no connected runner. Prior passing EditMode/manual baselines do not verify these
changes. See Temp/BossDeathHotfix-runtime-build.log and -editor-build.log.

Required manual verification (runtime issue remains UNVERIFIED until repeated):
1. Fresh Region-1 save: kill Sector Administrator while moving, then while dashing,
   and near phase-2 setup. After breakup check move/dash/fire/interact and camera aim.
2. Before choosing the reward, attempt Emergency Return: no boss-alive warning;
   a legitimate recent-combat delay may remain. Verify return/death preserves part.
3. With the three dedicated sprites assigned, verify rise/hold/flight from actual
   death position into a moving player, acquisition message, selectable reward,
   and Beacon-only reveal. Test a clear anchor and a physically blocked anchor.
4. Interrupt during breakup, pickup flight, and reveal by ending the run/scene.
   Check no stale locks/tweens, duplicate part or duplicate exits on subsequent run.
5. After Settlement natural analysis, repeat Region-1/2 Raider kills: Beacon/Portal
   pair centered on death, no story acquisition replay, normal reward choice and
   existing run carryover through Portal. First Region-2 clear still requires analysis.
6. First Region-3 clear: lens saved once, Beacon only; Recovery Processor/Route Core/
   defense/final launch gates preserved. No boss-generated FinalNetwork portal.

Residual placement limitation: all-blocked arenas retain the existing bounded
fallback; manually check unusual corridor geometry. Dedicated part artwork and
480x270 visual tuning are pending Inspector/manual verification.

## 2026-09-09 — Campaign Spine Pass 1

| Item | Status | Contract / remaining validation |
|---|---|---|
| First-defeat permanent recovery | Implemented / Static compiled | Boss death registers and saves unique parts before presentation; no cargo dependency or duplicate Curse growth. Verify death/menu/unload immediately after defeat. |
| Settlement route authorization | Implemented / Static compiled | Existing highestUnlockedDepth advances only on natural Active1/Active2 analysis completion. Pending analysis blocks new launch and restarts on Settlement entry. Interrupted conversations grant nothing. Old stored authorizations are preserved. |
| Repeat boss selection | Implemented / Static compiled | Shared existing Raider bindings cover Regions 1-3. First Region 2 retains corridor/triad; first Region 3 remains coreless. Repeat maps use existing Core/intro. Verify both repeat map variants in Play Mode. |
| Exit eligibility | Implemented / Static compiled | First clear: Beacon only. Region 1/2 repeat: Beacon plus authorized portal after current boss defeat. Missing progress/run, run ending and first-clear state fail closed. Region 3: Beacon only. |
| Recovery presentation | Implemented / Static compiled | Existing Region-2 prefab assigned to Boss and PhaseGatekeeper; unscaled DOTween under reward coordinator outlives corpse. No new pause/input/camera owner. |
| Exit presentation | Implemented / Static compiled | Existing safe placement; 0.65-second unscaled reveal gates colliders and typed interaction. Idempotent cleanup and immediate fallback. Verify at timeScale 0 and during disable/unload. |
| Final progression | Preserved | Three parts -> explicit restore/assembly -> activation -> defense -> final launch. No direct Region-3 portal, NullDispatcher combat or ending work. |
| New localization | Source authored / Import pending | Five system.campaign keys: three recovered-part names/messages and region_2/region_3 route authorization. Run Import Catalog then Validate Catalog. Existing expanded story graph is preserved; Validate Phase 2C Story after import. |
| Automated coverage | Added / Static compiled / Unity execution pending | Phase2CStoryDialogueTests campaign cases; QAStabilizationPass1Tests exit/recovery cases. Run these, localization/lifecycle suites, then full EditMode in the existing Editor. |
| Prefab setup | References edited / Unity import and visual check pending | Boss.prefab and PF_Boss_PhaseGatekeeper.prefab use the existing PF_Region2BossCoreRewardPresentation. Core's existing Raider/exit references and all campaign assets remain unchanged. No manual reconstruction required. |
| Manual acceptance | Pending | Fresh Normal first clear -> interrupted/retried analysis -> Normal Raider portal -> DeepZone1 first clear -> analysis -> repeat-chain to DeepZone2 first clear -> restore/activate/defense/final gate. Check same-run wallet, traits, HP/Armor and Beacon alternative at every eligible repeat exit. |

Compilation and static checks are not Unity test execution. The previous passing
EditMode and scene-loop baseline predates this pass. Nothing committed or pushed.

Final static results for this pass: `dotnet build Assembly-CSharp.csproj --no-restore
-v:q` passed with 0 errors / 70 warnings; `dotnet build Assembly-CSharp-Editor.csproj
--no-restore -v:q` passed with 0 errors / 4 additional Editor-test warnings after the
runtime build. Logs are in `Temp/CampaignSpine-runtime-build.log` and
`Temp/CampaignSpine-editor-build.log`. The first no-restore attempt lacked project
assets; a normal local build restored them and subsequent builds passed.
CSV inspection found 69 unique populated records, five new campaign messages within
their length hints, and no fabricated translations. Prefab GUID/fileID checks and the
saved 20-node Settlement graph passed static inspection. Scoped whitespace checks
passed. Baseline hashes confirm authored scenes, generated catalog/database, campaign
definitions, Core/exit prefabs and existing metadata were preserved; only the two
intended story-boss prefab recovery references changed among those assets.

## 2026-09-08 — Latest targeted follow-up

Boot manual binding completion is confirmed by read-only YAML validation:
rebindRows contains the 18 intended components exactly once; dropdowns contains
Resolution, Fullscreen and FrameLimit exactly once. Every local fileID resolves
to its required type beneath BootMainMenuUI/SafeArea/OptionsPanel, with no external
GUID or cross-owner mapping. Saved order and all bytes are preserved. Boot SHA256:
A890BB38B69819EA64A276DB87303EDB0525338F45053D37137A5391151A6F32.
This is serialized inspection, not an executed Unity validation test.

Corrections: exact native LogType.Log expectation; Pause instance-ID cleanup and
fixture-only input/static registration; actual UI-module input lifecycle with
EditMode player updates; removal of retired backButton reflection. The input
failure was the first S-key assertion, not an arrow/controller assertion.

Reinforcement compared EventSystem.currentSelectedGameObject to the retained
stabilized-frame card. uGUI clears selection inside its availability setter, before
the presenter's check. The minimal runtime fix captures ownership before refresh,
restores only its valid card/sidebar and preserves other focus owners. Added tests
record semantic ID, hierarchy/scene, active/interactable state and focus before and
after the real upgrade and synchronous Refresh; no wait or extra Show is used.

Pause source line 90 currently checks activeScene.isDirty, not a live root. The
exact surviving instance could not be reproduced without Unity execution. Both
checks remain strict, with exact ID/type/path/hideFlags/scene diagnostics and first
dirty-state boundary. A continuing dirty-state failure must not be cleared or
accepted as a new baseline.

Runtime: **0 errors / 73 warnings**. Editor/tests: **0 errors / 77 warnings**.
**0 Unity tests executed**; passed/failed/skipped/inconclusive totals unverified.
All 511 protected hashes match this pass's fresh baseline. No production asset
was edited or saved. No safe Unity runner was connected.

Retry in Unity: Boot binding case; conflict twice; deferred Pause Start twice;
full Pause twice; navigation input twice; Back/Settings twice; full Navigation;
reinforcement upgrade-focus twice plus native-setter/focus-owner cases; full Ship
Reinforcement; RuntimeSceneOwnership. Only after targeted tests pass, run the full
EditMode suite twice in the same session and export both XML results. No Boot
reassignment or UI installer is needed.

## 2026-09-08 — Third-pass EditMode status

Test-fixture corrections are statically compiled, not Unity-executed. Runtime:
0 errors / 73 warnings; Editor/tests: 0 errors / 77 warnings. **0 Unity tests
executed here**; passed, failed, skipped and inconclusive totals are unverified.
No production runtime defect was independently reproduced or changed.

Confirmed fixture causes: additive temporary scenes reject unsaved Untitled scenes;
a navigation test cloned into the active user scene; presenter assertions included
legitimate follower position writes; tests skipped initial selection/panel readiness;
test-added RuntimeOnly persistent listeners do not dispatch in EditMode. Preview
ownership/finally cleanup, explicit Pause root tracking, separated charge/follower
invariants and semantic preconditions address these paths. Actual Pause survivor
instance IDs still require a rerun; failure diagnostics now retain their identity.

### Historical Boot assignment list (completed; verified above)

The user completed these assignments on `BootMainMenuUI/SafeArea/OptionsPanel`, Inspector
`SettingsMenuTabController`:

- Set `Rebind Rows` to the 18 existing InputRebindButtonUI components beneath
  `KeyboardTab`: MoveUpBindingButton, MoveDownBindingButton, MoveLeftBindingButton,
  MoveRightBindingButton, FireBindingButton, DashBindingButton,
  InteractBindingButton, RadarToggleBindingButton, InstantRadarScanBindingButton,
  InventoryBindingButton, MapBindingButton, ReinforcementBindingButton,
  DismantleBindingButton, `Cancel/MenuBindingButton` (slash is part of its name),
  RouteAddBindingButton, RouteRemoveBindingButton, RouteClearBindingButton and
  DialogueAdvanceBindingButton. Include each exactly once.
- Set `Dropdowns` to the existing TMP_Dropdown components at
  `DisplayTab/ResolutionDropdown`, `DisplayTab/FullscreenDropdown`, and
  `DisplayTab/FrameLimitDropdown`.
- These arrays enforce rebinding/menu-cancel/expanded-dropdown input isolation.
  Saved fields are absent, not cleared by configuration. Do not add or rebuild
  controls. Save/reload Boot manually, preserving all other authored values.

Settlement `Canvas/SettlementHUD/ShipTraitTreePanel/TraitEntryTemplate` has optional
`ShipTraitNodeButton.canvasGroup: null` and no CanvasGroup component. Keep it as
authored; runtime validation explicitly allows absence. The old test added an
unbound component, then expected initialization to adopt it automatically. The
corrected test records saved identity and preserves both absent and valid groups.

### Required Unity rerun order

Keep an unsaved Untitled scene open (do not discard dirty production work), run
TemporaryPreviewServices_WorkAlongsideAnUnsavedUntitledScene and all temporary
service/bootstrap/tween cases. Run GameplayPauseOptionsConstructionTests twice,
PlayerChargeGaugePresentationTests twice, then Additional Traits, Navigation,
Restoration and Ship Reinforcement fixtures individually, Tutorial Guidance, the
Boot SavedFixedUiBindings case, and RuntimeSceneOwnershipTests. The Boot guards now
pass the read-only audit above and await the Unity rerun. Only after targeted isolation
passes or that binding blocker is isolated, run the full EditMode suite twice in
the same Unity session and export both XML files. Record exact leaf-test totals.
The Untitled-specific regression skips explicitly if the active scene is named;
it never saves, replaces or opens an ordinary scene to manufacture its environment.

All 511 protected-file hashes still match. Existing ItemDetailRoot, Settlement and
ShopProjectile Missing Script cleanup remains intact; vendor sample issues remain
untouched. UI authoring tools remain retired. No scene/prefab/project-file save,
production behavior change, commit or push was performed.

## 2026-09-08 — Second-pass EditMode fixture isolation

Status: test-only fixes compile; filtered runs and two full same-session Unity
runs remain pending. **0 Unity tests executed in this session**. Current passed,
failed, skipped and inconclusive totals are unknown, not zero-failure results.
The earlier 240 / 130 passed / 110 failed count is historical, not the latest run.

| Confirmed fixture defect | Correction |
|---|---|
| Runtime SceneManager.CreateScene used by bootstrap classification and tween setup | Initial additive replacement superseded by third-pass PreviewScenes; no active-scene changes |
| Targeted ObjectFactory creation subsequently applies Editor default parenting | Plain test-owned root, inactive move to exact scene, handle verification before parenting/runtime components; no default-parent changes |
| Grouping could move all Settlement roots under an escaped wrapper | Verify wrapper and each root's scene before parenting; verify loaded source path; include inactive descendants |
| Opaque Scene-versus-Scene assertion | Name/path/handle/valid/loaded/preview diagnostics with object paths and IDs |
| Escaped roots and incomplete teardown cascaded between tests | Correct ownership at creation; finally cleanup; explicit PreviewScene root destruction with retained-reference leak assertions |
| Unsorted/lazy snapshots | Deterministic arrays scoped to fixture or explicitly protected user scene, with named diagnostic context |
| Broad resource-warning expectations and un-restored missing bindings | Exact local warning strings; restore each modified binding in finally |

The saved Settlement scene contains one SettlementHUD (component 1396245570,
GUID d9bf83076c254d047991803b061a15f0) and one SettlementUIController
(component 1980294058, GUID fd38947bbb2db8949b4d5c9f19695a8f), with the controller's
hud reference intact. No replacement component was added. Disposable-scene
discovery checks remain strict and require Unity execution to confirm acceptance.

### Current saved cleanup evidence

- Tutorial and Expedition Canvas_WorldHUD/ItemDetailRoot retain their
  InteractionPromptUI.lootDetailRoot bindings; obsolete missing components are gone.
  The user previously verified functional item-detail presentation.
- ShopProjectile's deleted-script GUID is absent.
- Settlement Canvas/Temp_ClearButton's deleted debug GUID and the obsolete
  ResetData / FillResourcesToLimit callbacks are now absent in the saved scene.
- Read-only audit: 498 assets / 6,951 m_Script references; no unresolved first-party
  source MonoScript GUID. Orphan vendor Watercolor sample references remain
  untouched and excluded by project ownership, not a GUID exemption list.
- No runtime implementation or protected asset changed. All 511 protected-file
  hashes match the start of this pass. UI authoring menus remain retired.

### Unity verification still required

Run the bootstrap scene-classification and guidance tween tests; PreviewEventSystem
cleanup cases; Pause Options twice; PlayerChargeGauge twice; each affected Settlement
fixture separately; Tutorial Guidance; Tutorial Resources; Expedition UI; and
RuntimeSceneOwnership. Then run the complete EditMode suite twice in the same
Unity session and export both XML results, recording passed/failed/skipped/
inconclusive leaf-test totals. No Play Mode or visual success is claimed here.

## 2026-09-08 — Phase 2C.2 tutorial presentation

Implemented: offscreen ancient-wreck discovery reserves owned focus immediately,
without waiting for player framing to include the wreck. Radar and map notifications
use one gate. Existing GungeonStyleCamera2D performs unscaled 0.6s focus, then
Pixel Crushers guidance, then unscaled 0.5s return to current player framing.
Only natural terminal completion followed by a valid return records/advances once.
Interrupted/invalid/preempted sequences release their captured ownership and wait
for rediscovery. No additional camera, input map, polling loop or dialogue runtime.

Verified route-free supply authority: HarvestObjectHealth.Died advances from Map,
RoutePing, TravelNormalSalvage or DestroyNormalSalvage to CollectResources. Route
placement/proximity remain optional; duplicate deaths/routes cannot advance again.
No resource, reward, quest or save rules changed.

Runtime and Editor/test static builds passed. New PreviewScene tests exercise
reservation, duplicate discovery, unscaled camera calculations, terminal-boundary
natural/interrupted return, invalidation, ownership takeover, cleanup and route-free
controller convergence. They do not claim a live Pixel Crushers conversation or
rendered frame was executed. Unity Test Runner and Play Mode remain pending.

Manual acceptance (no installers or asset saves required): enter through Boot,
discover the ancient wreck offscreen via Radar and map separately; confirm no
dialogue while travelling, stable framing while paused, return before release and
one checkpoint. Cancel dialogue, invalidate a target, disable the controller and
return to Main Menu during each phase; verify no completion or stuck input and
safe replay after rediscovery. Destroy the supply box without opening Map, then
repeat with a route; both must reach collection once. Check charge-root stability
during focus/return in Tutorial and ordinary camera/charge movement in Expedition.

Authored UI values and the shared single-rounding charge projection remain frozen.
The VOID SCRAPPER > UI tools stay retired; Dialogue/Localization tools are unchanged.

## 2026-09-07 — Final UI cleanup and shared PlayerChargeGauge correction

Implemented: removed all 14 completed UI installer/validator scripts and matching
metadata, their exclusive fixtures/tests and Boot/Options Editor authoring helpers.
The VOID SCRAPPER > UI menu tree is retired; Dialogue and Localization tools remain.
Runtime owners retain their script identities and authored serialized references.
Inert completed-migration booleans were removed after source/YAML/reflection searches;
old scalar keys remain untouched in saved scene YAML and no longer control runtime.
Legacy object-reference fields and active dynamic/Options compatibility remain.

Verified by read-only source and serialized inspection: required migrated HUD
bindings resolve; Expedition reinforcement is installed. Tutorial's absent
Expedition objective fields are optional, not missing installation requirements.
No production scene/prefab was opened for writing or saved by the agent.

Shared charge follow now uses the actual camera projection and parent-local
conversion in one post-LateUpdate render callback per frame. The previous
synthetic camera snap/double rounding, forced pivot and root cooling-scale pulse
are removed. Event subscriptions own and release actual publishers. Charge timing,
fill/color/visibility, weapon actions and player/camera gameplay remain unchanged.

Runtime regression tests remain, using test-owned PreviewScene copies of the saved
authored scenes instead of installers. Added deterministic projection, zoom,
movement, one-writer, charge-state, cleanup, identity and removed-menu checks.
Runtime and Editor/test static builds passed; the Editor build used a temporary
MSBuild exclusion list for deleted source entries still present in Unity-generated
projects, without editing those projects. Unity tests, production validation,
Play Mode and visual checks were not executed.

Manual gate: let Unity refresh code, run the relevant EditMode suites, then test
Tutorial and Expedition charge follow during player-only/camera-only/combined
movement and zoom. Verify cancellation, firing, overheat/recovery, pause/resume,
scene loops, no duplicate gauges/subscriptions and unchanged fixed HUD. No installer
or repair step is required. Phase 2C.2 was separate from this cleanup; its subsequent
implementation and outstanding manual verification are recorded above.

> Older UI migration sections below are historical records, not current menu
> instructions. The completed authoring menus have been removed.

### Historical record: 2026-09-07 — Authored layout freeze / Expedition Reinforcement slot

Implemented: retired the public navigation/status/heat reset commands and their
reset-only code. Existing Inspector layouts, typography and styling remain the
baseline. Installers only provide missing-object/component defaults; validation
is read-only and accepts supported positive authored canvas scaling.

New scoped menus: VOID SCRAPPER > UI > Expedition > Install Reinforcement Slot UI
and Validate Reinforcement Slot UI. The saved Expedition slot is already bound
but inactive. Installation adopts it without changing that active state or its
user-authored geometry. Missing presentation uses saved Tutorial visuals from an
isolated read-only PreviewScene, with target-local bindings and scoped Undo.
Tutorial remains unchanged. Edit slot/icon/fill RectTransforms and the charge/key
TMP components via the existing ReinforcementSlotUI Inspector fields.

Static runtime and Editor builds are the available verification path. Added tests
cover new/partial/adopted/renamed roles, preservation, ambiguity, ownership,
rollback/Undo, temporary serialization, source equivalence and state-only updates.
Unity Test Runner, production validators, Play Mode and rendered checks have not
been executed for this pass. Existing activation/subscription regressions remain
required; no new activation path or input owner was added.

Manual gate: load Expedition outside Play Mode; run the two new menus; inspect
the reported existing inactive slot and enable it manually if intended. Save and
reload manually, validate again, test empty/equipped/charges/cooldown/unavailable,
input hints, one activation, scene loops and unchanged Tutorial. Do not run any
historical layout-reset workflow described in earlier migration notes.

작성 기준일: 2026-08-30

이 표는 현재 저장소의 정적 근거를 기준으로 한다. 클래스 존재만으로 완료 판정하지 않았으며, 구현 수치는 별도 승인 근거가 없으면 테스트 값으로 취급한다. Unity Editor와 Play Mode 검증은 이번 감사 범위에 포함되지 않았다.

- Design Status는 Confirmed Design, Implementation, Testing, Planned, Deferred, Legacy 중 현재 문서화에 가장 중요한 판정을 사용한다.
- Implementation Status의 구현됨은 코드와 직렬화 연결이 확인됐다는 뜻이며, 플레이 검증 완료를 뜻하지 않는다.
- 부분 구현은 상태·데이터·코드 훅 중 일부만 있거나 전용 자산 연결이 빠진 경우다.

| System | Design Status | Implementation Status | Main Files/Assets | Needs Document Update | Notes |
|---|---|---|---|---|---|
| Core Loop | Confirmed Design | 구현됨 / 정적 검증 | Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Core/Flow/RunContext.cs<br>Assets/02_Scripts/Expedition/ExpeditionBootstrap.cs | 예 — 확장 | 탐사 → 수확 → 전투 → 귀환 → 정착지 성장은 유지된다. 화물·작전·지역 계승 상태를 추가해야 한다. |
| Expedition Flow | Implementation | 구현됨 / 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionOperationController.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Campaign/CampaignProgressionCatalog.cs | 예 — 전면 | 지역 1→2→3은 연속 런이며 최종 네트워크는 정착지에서 별도 출격한다. |
| Controls | Confirmed Design | 구현됨 / 직렬화 확인 | Assets/04_Input/PlayerControls.inputactions<br>Assets/02_Scripts/Player/PlayerController2D.cs<br>Assets/02_Scripts/Input/InputBindingUtility.cs | 예 — 전면 | 상호작용은 F다. E, Tab, R, G, Esc, Mouse 4가 추가됐으며 표시 키 하드코딩은 금지된다. |
| HUD | Confirmed Design | 구현됨 / 레이아웃 테스트 필요 | Assets/02_Scripts/UI/ExpeditionHUD.cs<br>Assets/01_Scenes/Expedition.unity<br>Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab | 예 — 전면 | XP 게이지 대신 Core Signal, Cargo, Reinforcement, 상태 효과, Alloy와 Tuning 표시가 중심이다. |
| Player | Testing | 부분 구현 / 데이터 점검 필요 | Assets/02_Scripts/Settlement/ShipDefinition.cs<br>Assets/02_Scripts/Settlement/01_basic_ship.asset<br>Assets/02_Scripts/Settlement/02_shotgun_ship.asset<br>Assets/02_Scripts/Settlement/03_sniper_ship.asset | 예 — 전면 | 세 함선 정의가 있으나 현재 HP가 모두 20이고 basic_ship / muchingun_ship ID 불일치가 있다. |
| Weapon Trees | Testing | 구현됨 / 콘텐츠·비용 테스트 필요 | Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs<br>Assets/01_Scenes/Settlement.unity<br>Assets/02_Scripts/Config/Weapon_MachineGun.asset<br>Assets/02_Scripts/Config/Weapon_Shotgun.asset<br>Assets/02_Scripts/Config/Weapon_Sniper.asset | 예 — 전면 | 공용 및 3개 무기 분기가 있다. 함선 해금과 무기 선택의 최종 결합 규칙은 확정 근거가 부족하다. |
| Radar | Testing | 구현됨 / 규칙 테스트 필요 | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/RadarTarget.cs<br>Assets/02_Scripts/Player/PlayerStealthController.cs | 예 — 전면 | Q 모드 전환, 활성 중 Mouse 4 즉시 스캔, 근거리 수동 탐지가 있다. Shotgun 도발과 무기별 경보는 대상 플래그에 따라 달라진다. |
| Map | Testing | 구현됨 / UX·수치 테스트 필요 | Assets/02_Scripts/Core/ExpeditionMapGenerator.cs<br>Assets/02_Scripts/Config/New Map Generation Config.asset<br>Assets/02_Scripts/UI/MapDiscoveryController.cs<br>Assets/02_Scripts/UI/ExpeditionRoutePlanner.cs | 예 — 신규 장 | 탐사 안개와 최대 5개 경유지를 가진 전체 지도가 있다. 지역 2는 상단 Core·스크롤 회랑을 예약하고, 지역 3은 Core·현장 기지·상점 없이 전용 보스와 반사판 8개를 생성한다. |
| Objectives / Operations | Testing | 구현됨 / 콘텐츠 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionOperationController.cs<br>Assets/02_Scripts/RunRuntime/ExpeditionObjectiveDirector.cs | 예 — 신규 장 | 지역당 작전 하나와 Core 공개·보스 보상에 쓰이는 Objective Signal 2/3/4 단계가 있다. |
| Cargo | Testing | 구현됨 / 밸런스 테스트 필요 | Assets/02_Scripts/Player/PlayerCargoController.cs<br>Assets/02_Scripts/Core/Flow/RunContext.cs<br>Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab | 예 — 신규 장 | Scrap·Core·Alloy에 용량·중량·과적·자동 회수·투기·긴급 귀환 보존 규칙이 적용된다. |
| Currency | Testing | 구현됨 / XP는 레거시 | Assets/02_Scripts/Economy/RunWallet.cs<br>Assets/02_Scripts/Core/Config/CoreTypes.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Progression/PermanentProgress.cs | 예 — 전면 | Credits, Scrap, Core, Tuning Chips, Stabilized Alloy가 현재 경제다. Experience는 새 보상 경로가 없다. |
| Trait | Testing | 구현됨 / 콘텐츠 테스트 필요 | Assets/02_Scripts/Data/TraitDefinition.cs<br>Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset<br>Assets/02_Scripts/RunRuntime/RunTraitAcquisitionService.cs | 예 — 전면 | 획득원 기반 런 Trait, 중복 강화, 교체·분해, 보스 Trait, Tuning 강화가 있다. XP 레벨업 선택은 현행 경로가 아니다. |
| Pixel Curse | Confirmed Design | 구현됨 / 연출 테스트 필요 | Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset<br>Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Player/PlayerVisualStateController.cs | 예 — 신규 장 | 지속·비드롭·비분해 서사 Trait이며 튜토리얼 감염 경로가 있다. |
| Reinforcement | Testing | 구현됨 / 41개 콘텐츠 테스트 필요 | Assets/02_Scripts/Player/PlayerReinforcementController.cs<br>Assets/02_Scripts/Data/ReinforcementDefinition.cs<br>Assets/02_Scripts/Config/Catalog/Reinforcement Catalog.asset | 예 — 신규 장 | R 장착 사용, 충전·재충전, 무기 제한, 교체·분해, 생성물 소유권 경로가 있다. 컨트롤러 권위 구조는 안정적이다. |
| Harvest | Testing | 구현됨 / 보상 테스트 필요 | Assets/02_Scripts/HarvestObjectHealth.cs<br>Assets/02_Scripts/Enemies/RewardDropper.cs<br>Assets/02_Scripts/Config/Container.asset<br>Assets/02_Scripts/Config/HighValueWreck.asset<br>Assets/02_Scripts/Config/DestroyedHull.asset | 예 — 부분 | 세 수확 유형은 유지되며 Alloy, Trait, Reinforcement와 작전 연계가 추가됐다. |
| Enemy Combat | Testing | 구현됨 / 수치 테스트 필요 | Assets/02_Scripts/Enemies/EnemyBaseAI.cs<br>Assets/02_Scripts/Enemies/EnemyState.cs<br>Assets/02_Scripts/Data/EnemyDefinition.cs<br>Assets/02_Scripts/Config/EnemyDefinition/ | 예 — 부분 | 기존 FSM은 유지되며 공격 패턴, 감지, Elite 변형과 지역 스케일이 확장됐다. 현재 HP 수치는 프로토타입과 다르다. |
| Enemy Roles | Confirmed Design | 구현됨 / 정적 검증 | Assets/02_Scripts/Enemies/EnemyRoleController.cs<br>Assets/02_Scripts/Enemies/EnemyRoleSimulationGate.cs<br>Assets/02_Scripts/Core/ExpeditionMapGenerator.cs | 예 — 신규 장 | Defender, RivalHarvester, Scavenger와 원거리 시뮬레이션 단계가 있다. 임시 유인·감속은 출처별 상태를 보존한다. |
| Events | Testing | 구현됨 / 보상·난이도 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionEventObject.cs<br>Assets/03_Prefabs/Event/<br>Assets/02_Scripts/Config/EventRewards/ | 예 — 전면 | Rescue, Unknown Device, Unstable Reactor, Black Box 네 유형이 있고 Objective Signal과 연결된다. |
| Shop | Testing | 구현됨 / 경제·전투 테스트 필요 | Assets/02_Scripts/Shop/ShopStructure.cs<br>Assets/02_Scripts/ShopStockController.cs<br>Assets/02_Scripts/Shop/ShopTradeUI.cs<br>Assets/03_Prefabs/Enemy/PF_ShopStructure.prefab | 예 — 전면 | 중립·경고·전역 적대는 유지된다. 무작위 Trait·Reinforcement, 안전 구역, 보안망, Maintenance Bay가 추가됐다. |
| Core | Testing | 구현됨 / 최종 보스 연결 누락 | Assets/02_Scripts/Core/CoreObject.cs<br>Assets/03_Prefabs/Object/Core.prefab<br>Assets/02_Scripts/RunRuntime/ExpeditionObjectiveDirector.cs<br>Assets/02_Scripts/Core/ExpeditionMapGenerator.cs | 예 — 전면 | 지역 1·2 Core는 Signal 2개 전까지 숨겨지고 2초 활성화를 사용한다. 지역 2 전용 보스가 연결됐으며 Core 보상은 사망 시점으로 미뤄진다. 지역 3은 Core 없는 전용 조우이고 최종 보스 참조는 여전히 없다. |
| Region 1 | Testing | 구현됨 / 플레이 검증 필요 | Assets/03_Prefabs/Enemy/Boss.prefab<br>Assets/02_Scripts/Boss/BossPatternController.cs<br>Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region1.asset<br>Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab | 예 — 전면 | Sector Administrator와 재도전 Raider Commander가 있다. 현재 Phase 2 임계값은 프로토타입 30%와 다른 50%다. |
| Region 2 | Testing | 전용 조우 연결됨 / Play Mode 검증 필요 | Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab<br>Assets/02_Scripts/Boss/FrigateTriadBossController.cs<br>Assets/02_Scripts/Boss/SalvageDevourerCorridorController.cs<br>Assets/03_Prefabs/Object/PF_Region2BossCorridorRuntime.prefab<br>Assets/03_Prefabs/Object/Core.prefab | 예 — 신규 장 | 상단 Core, 세로 스크롤 회랑, 3기 편대의 생존 수별 패턴, 마지막 돌진, 사망 시 Core 보장 지급이 직렬화 연결돼 있다. 수치와 실행 감각은 테스트 상태다. |
| Region 3 | Testing | 전용 조우 연결됨 / Core 보상 경로 미확인 | Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab<br>Assets/03_Prefabs/Object/PF_Region3_PhaseReflectorPlate.prefab<br>Assets/02_Scripts/Boss/PhaseGatekeeperBossController.cs<br>Assets/02_Scripts/Boss/PhaseReflectorPlate.cs<br>Assets/01_Scenes/Expedition.unity | 예 — 신규 장 | Core 없는 맵에 보스와 반사판 8개가 생성된다. 은폐·반사 레이저 3회·노출 피해 창은 연결됐지만, 캠페인 정의의 Core 2개 지급 호출은 정적 검색으로 확인되지 않았다. |
| Boss Rewards | Testing | 구현됨 / 지역 3 Core 연결 점검 필요 | Assets/02_Scripts/RunRuntime/BossRewardExitCoordinator.cs<br>Assets/02_Scripts/Core/BossDummyController.cs<br>Assets/02_Scripts/Campaign/CampaignBossRewardService.cs<br>Assets/02_Scripts/Data/BossCampaignDefinition.cs | 예 — 전면 | 고정 캠페인 보상과 3개 선택 보상이 분리된다. 지역 2 Core는 사망 시 보장·중복 방지 처리된다. Signal 3/4 보너스가 있으며 지역 3 Core 지급은 미확인이다. |
| Return | Testing | 구현됨 / 정산 테스트 필요 | Assets/02_Scripts/EmergencyReturnController.cs<br>Assets/02_Scripts/Core/ReturnBeacon.cs<br>Assets/02_Scripts/Core/WormholePortal.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | 예 — 전면 | 긴급 귀환은 기본 화물 용량 70% 보존이며 고정 20% 손실이 아니다. 사망·안전 귀환·지역 이동 정산이 각각 다르다. |
| Campaign Progression | Planned | 부분 구현 / 정착지 경로 미연결 | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Campaign/CampaignProgressionCatalog.cs<br>Assets/02_Scripts/Campaign/SettlementRouteCoreController.cs | 예 — 신규 장 | 세 보스 부품, Route Core, 정착지 방어, 최종 출격 상태는 있으나 Route Core 컨트롤러가 씬·프리팹에 없다. |
| Settlement | Testing | 부분 구현 / 콘텐츠·연결 점검 필요 | Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs<br>Assets/02_Scripts/Settlement/SectorTechnologyCatalog.cs<br>Assets/01_Scenes/Settlement.unity | 예 — 전면 | 건물 3단계 업그레이드는 레거시다. 현재는 이진 복구, 함선·영구 Trait Tree, Alloy 기술 성장이다. |
| Tutorial | Testing | 구현됨 / Play Mode 검증 필요 | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Tutorial/TutorialStep.cs<br>Assets/01_Scenes/Tutorial.unity | 예 — 신규 장 | 28단계가 운영 시스템과 실제 긴급 귀환을 사용한다. 이번 감사에서는 실행 검증하지 않았다. |
| Story | Planned | 부분 구현 | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset<br>Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/02_Scripts/RunRuntime/FieldNpcObjective.cs | 예 — 전면 | 튜토리얼·첫 정착지·NPC 서비스 대화는 연결된다. 지역 보스 서사와 후반 캠페인 대화는 부족하다. |
| Final Boss / Ending | Planned | 시작 불가 / 전용 자산 누락 | Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Final.asset<br>Assets/02_Scripts/Core/CoreObject.cs<br>Assets/02_Scripts/Campaign/FinalBossSettlementSupportPhase.cs<br>Assets/03_Prefabs/Object/Core.prefab | 예 — 계획 장만 | Null Dispatcher 상태와 FinalVictory 훅은 있으나 finalBossPrefab 연결, 전용 전투, 엔딩 시퀀스가 없다. |

## 문서화 우선순위

1. 새 시스템 설계서에 바로 옮길 수 있는 구조: 핵심 루프, 입력 표시 규칙, HUD 정보 우선순위, PlayerReinforcementController 권위, Enemy 역할과 임시 효과 소유권, Pixel Curse 영구 규칙.
2. 구현을 기준으로 쓰되 테스트 표기가 필요한 구조: Radar·Map, Cargo·Currency, Trait, Harvest, Events, Shop, Core, 지역 1~3 보스, 보상·귀환, Settlement 성장 수치.
3. 계획으로만 써야 하는 구조: Route Core 기반 정착지 방어, 최종 보스, 엔딩과 후반 Story.
4. 레거시로 분리할 항목: 월드 래핑, XP 런 레벨업, 건물별 3단계 업그레이드, 사용되지 않는 개발용 대화 경로.

## Dialogue and Localization Phase 2A — 2026-08-31

Status vocabulary for this section is explicit: `Not Started`, `Implemented`,
`Static Verified`, and `Play Mode Verified`. Multiple values mean the implementation
exists and has reached the listed verification level.

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| SYSTEM_DESIGN.md | Implemented / Static Verified | Docs/Design/SYSTEM_DESIGN.md | Authoritative dialogue/localization boundary created. |
| Localization catalog | Implemented / Static Verified | Assets/02_Scripts/Localization/LocalizationCatalog.cs<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Generated immutable runtime lookup with Korean fallback metadata. |
| Localization service | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Project-owned service serialized on the existing persistent Dialogue Manager. Play Mode verification pending. |
| Language settings integration | Implemented / Static Verified | Assets/02_Scripts/Settings/GameSettingsRuntime.cs | Device-local PlayerPrefs language; no SaveData change. |
| Localized TMP presenter | Implemented / Static Verified | Assets/02_Scripts/Localization/LocalizedTextPresenter.cs | Event-driven; production UI bulk migration not started. |
| Localization CSV | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv | Korean-only foundation rows; no invented production translations. |
| Localization importer | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentImporter.cs | Editor-only UTF-8 import; runtime CSV loading absent. |
| Localization validator | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentValidator.cs<br>Assets/02_Scripts/Localization/Editor/LocalizationCsvParser.cs | Validates keys, Korean source, placeholders, CSV, metadata, references, and freshness. |
| Automated tests | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/Tests/LocalizationFoundationTests.cs | Test assembly compiles; Unity EditMode execution pending. |
| Pixel Crushers language forwarding | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs | Uses supported language API; Korean maps to default/source language. |
| Runtime language refresh | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs<br>Assets/02_Scripts/Localization/LocalizedTextPresenter.cs | Instance event, no polling; Play Mode verification pending. |
| CJK font verification | Not Started | Assets/07_Txt/<br>Assets/TextMesh Pro/Resources/TMP Settings.asset | Requires Unity glyph and fallback-font checks. |
| Full UI migration | Not Started | Existing UI and data definitions | Explicitly outside Phase 2A. |
| Dialogue action system | Not Started | Deferred to Phase 2B | Existing gameplay owners must remain authoritative. |
| Dialogue condition system | Not Started | Deferred to Phase 2B | No Phase 2A condition runtime added. |
| RescueContact vertical slice | Not Started | Deferred to Phase 2B | `FieldNpcObjective` behavior remains unchanged. |

## Dialogue and Localization Phase 2B — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| RescueContact localization keys | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source and fallback; optional translations intentionally empty. |
| Dialogue condition registry | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueConditionRegistry.cs | Only `RescueContactAvailable`; read-only and fail-closed. |
| Dialogue gameplay action dispatcher | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueGameplayActionDispatcher.cs | Only `RescueContactAccept`; deduplicates within one conversation and calls existing authority. |
| Pixel Crushers Phase 2B bridge | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialoguePixelCrushersBridge.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Uses verified Lua registration and controller lifecycle APIs; Play Mode pending. |
| RescueContact service authority adapter | Implemented / Static Verified | Assets/02_Scripts/RunRuntime/FieldNpcObjective.cs<br>Assets/03_Prefabs/NPC/NPC_RescueContact.prefab | Dialogue requests the existing service; objective signal remains owned by ExpeditionObjectiveDirector/RunContext. |
| RescueContact conversation installer | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/RescueContactDialogueInstaller.cs | Validates in memory and replaces only the named conversation; must be run in Unity. |
| RescueContact Dialogue Database wiring | Not Started | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset | Manual Editor installer execution pending; asset was not rewritten outside Unity. |
| RescueContact EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/RescueContactDialogueTests.cs | Test source compiles; Unity Test Runner execution pending. |
| Full dialogue action catalog | Not Started | Future migrations | Explicit registration remains required per gameplay owner. |
| Full dialogue condition catalog | Not Started | Future migrations | Only the RescueContact availability condition exists. |
| Recommended next migration | Not Started | NPC_FieldTechnician_Service | Migrate only after RescueContact Play Mode authority and lifecycle verification. |

## Dialogue Stability Phase 2B.1 — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Canonical persistent Dialogue Manager owner | Implemented / Static Verified | Assets/02_Scripts/Core/Bootstrap/GameBootstrap.cs<br>Assets/01_Scenes/Boot.unity<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Boot `CoreRoot` is creation authority; repeated scene-loop Play Mode verification pending. |
| Incoming Boot duplicate pruning | Ordering corrected / Play Mode recheck pending | Assets/02_Scripts/Core/Bootstrap/GameBootstrap.cs | 2026-09-09: duplicate rejection now precedes the `scene.isLoaded` initialization gate at execution order -32000. Deactivate incoming manager/CoreRoot before deferred destruction; preserve canonical ownership and the defensive localization Error. Initial service/progression setup still waits for a loaded scene. |
| Settlement return to Boot | Unified with existing scene flow / Play Mode recheck pending | Assets/02_Scripts/EscSettingsMenuController.cs<br>Assets/02_Scripts/Core/Flow/SceneFlowManager.cs | Retain Save then Close; call LoadBoot for fade, GameState, loading guard and ending-presentation release. Expedition pause already uses LoadBoot. User-reported 256 passing EditMode tests predate this correction; no post-change Unity execution claimed. |
| Settlement authoritative transition guard | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementExpeditionLaunchGuard.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Active conversation fails closed with a typed reason; existing ship/run requirements remain authoritative. |
| Settlement modal UI input | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementUIController.cs | Event-driven CanvasGroup and keyboard gating; Pixel Crushers response UI remains interactive. |
| Settlement dialogue-block localization | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source/fallback; optional translations intentionally empty. |
| Settlement story reaction expansion (2026-09-09) | Source implemented / Static compiled / Unity import and playback pending | Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs<br>Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Eight initial subtitles and five two-line repeat branches. Original entry IDs and six opening texts preserved. Seven new Korean keys; only ready-state legacy text changed. No new gameplay actions or stored Curse state. Runtime 0 errors / 73 warnings; Editor/tests 0 errors / 77 warnings. No new Unity execution claimed. |
| Settlement expansion Unity workflow | Required before testing new content | Existing Localization and Dialogue menus | Outside Play Mode: Import Catalog, Validate Catalog, Install Phase 2C Story, Validate Phase 2C Story. Installer retains its existing nine-conversation scope. Generated catalog/database have not been edited by this task. Run Phase2C/localization/lifecycle tests then full suite; play initial/interrupted/repeated 0/1/2/3/restored states at 480x270. User reports pre-task full EditMode and Boot scene-loop success; those results do not verify this expansion. |
| Phase 2B.1 automated tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/DialogueLifecycleStabilityTests.cs | Runtime and Editor test assemblies compile; Unity Test Runner execution pending. |
| MainMenu/Boot/Settlement loop | Not Started | Unity Play Mode | Repeat three times; exactly one manager/service/bridge required. |
| Settlement pointer/keyboard/programmatic modal test | Not Started | Unity Play Mode | Must verify all launch paths block during mandatory dialogue and recover after it ends. |
| Phase 2C story migration | Implemented / Static Verified | Tutorial -> first Settlement story -> main quest | Code, content, and generated-data validation are complete; the Phase 2B.1 lifecycle gate and Phase 2C flow still require Play Mode verification. |

## Dialogue Story Phase 2C — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Phase 2C authority decision | Implemented / Static Verified | Docs/Design/SYSTEM_DESIGN.md | Pixel Crushers owns graphs; tutorial, campaign progress, rewards, and saving remain in existing owners. |
| Tutorial story guidance | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Four one-line graphs run at Move, Radar, supply-container, and detected ancient-signal stages; natural completion is exact-once and interrupted stages remain pending. Unity installation/Play Mode pending. |
| Unknown access-key contact | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Natural completion advances the existing Pixel Curse checkpoint; no direct Trait/visual/save mutation. |
| Post-Curse rescue integration | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Natural completion advances the existing rescue/return authority; interruption leaves the checkpoint pending. |
| First Settlement analysis | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/01_Scenes/Settlement.unity | Pending/complete flags and a natural terminal marker gate completion; Play Mode pending. |
| Main quest typed conditions | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueConditionRegistry.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Read-only `NotStarted`, 0/3, 1/3, 2/3, ready, and completed projections; unknown/missing authorities fail closed. |
| Main quest exact-once start | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueGameplayActionDispatcher.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Progression/PermanentProgress.cs | Settlement atomically starts when needed, records story completion, saves, and notifies after successful conversation completion; existing unlock flags supply recovery and persistent idempotency. |
| Boss-part progress binding | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | Read-only quest state derives from the existing three campaign-boss part grants; no dialogue reward write added. Play Mode pending. |
| Phase 2C localization | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source/fallback for seven graphs, speakers, quest start/progress/restoration, Curse level notification, and named placeholders; strict CSV/catalog validation passes. |
| Deterministic graph installer/validator | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Installs seven named graphs and one stable Operator actor through Pixel Crushers APIs; source compiles and cloned-database tests exist, but production Dialogue Database execution remains pending in Unity. |
| Mandatory Settlement launch lock | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementExpeditionLaunchGuard.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Pending story blocks pointer/keyboard/programmatic launch even before conversation starts; Play Mode pending. |
| Phase 2C EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Covers state reads, exact-once action, interruption, save identity, graph determinism, localization, and launch gating. Test source compiles; Unity Test Runner execution pending. |
| Dialogue Database Phase 2C wiring | Not Started | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset | Run the project Editor installer; no direct YAML edit was made in Phase 2C. |
| Main quest HUD binding | Not Started | Future approved HUD surface | Localization key exists; no generic authoritative main-quest presenter was found, so Phase 2C does not invent one. |
| Phase 2C Play Mode verification | Not Started | Tutorial / Settlement / Expedition | Requires fresh tutorial, interruption/re-entry, exact-once start, all repeat states, save/load, scene loop, RescueContact, pause, and fallback checks. |

## Dialogue Story Phase 2C.1 — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Staged tutorial pacing | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Guidance state is event-driven and runtime-local; gameplay objectives retain their existing authority. |
| Quest natural-start boundary | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Existing pending flag and natural terminal action start the quest once; Settlement load alone performs no start. |
| Ready-to-restore progression | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs | Three distinct boss parts project `ReadyToRestore`; no expedition-side automatic completion was added. |
| Settlement Recovery integration | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Settlement/SettlementUIController.cs | Recovery Processor temporarily presents explicit access-key restoration; success changes authoritative RouteCore state and saves once. Play Mode pending. |
| Pixel Curse levels | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | Level is derived as inactive 0 or `min(1 + distinct parts, 4)`; duplicate parts emit no level change. No new combat effects. |
| Phase 2C.1 localization/catalog | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | 44 records, deterministic hash, Korean fallback, and `{level}` / quest placeholders validate. |
| Phase 2C.1 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Test source compiles; Unity Test Runner execution remains pending because an Editor instance is open. |
| Phase 2C.1 Play Mode | Not Started | Tutorial / Settlement / Expedition | Verify staged checkpoints, interruption, recovery confirmation, Curse notifications, 480x270 wrapping, save reload, and no duplicate-service error during normal scene transitions. |

## Dialogue Story Phase 2C.2 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| High-value wreck camera presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Map/Radar discovery acquires owner-scoped camera and input locks, blends to the wreck for 0.6s, starts dialogue after focus, then returns for 0.5s before exact-once advancement. Both blends use unscaled time. |
| High-value interruption cleanup | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Duplicate discovery is rejected. Conversation interruption, invalid target, camera takeover, RunEnded, disable, and destroy release only tutorial-owned state; interrupted guidance remains replayable. |
| Optional supply route | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Route placement or physical camera/proximity approach enters the same supply sequence. Existing `HarvestObjectHealth.Died` is the final box-completion event. |
| Phase 2C.2 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Added presentation-phase, duplicate/interruption, owner/unscaled integration, optional-route, and exact-once convergence coverage. Unity Test Runner execution pending. |
| Phase 2C.2 Play Mode | Not Started | Tutorial | Verify offscreen focus, paused dialogue hold, natural return, interruption/scene cleanup, optional-route play, and 480x270 framing. |

## Dialogue Story Phase 2C.3 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Radar mode toggle | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/04_Input/PlayerControls.inputactions | Q toggles the sole active state and never scans. Existing binding was already `<Keyboard>/q`. |
| Active-mode instant scan | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/04_Input/PlayerControls.inputactions | Mouse 4 uses the existing `<Mouse>/backButton` action, press-only input, and existing 0.5s cooldown. Inactive/locked requests fail closed. |
| Radar charge retirement | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/PlayerRadarVFXController.cs<br>Assets/02_Scripts/Core/Config/GameBalanceConfig.cs | Radar hold/release/cancel state and Radar-only gauge/effect dependency are removed. Shared weapon charge UI is unchanged. |
| Radar tutorial signals | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Tracks Radar activation and successful authoritative supply-target scan separately; Q alone cannot advance. |
| Shared supply/wreck presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Both targets use one owner-scoped, unscaled focus/dialogue/return pipeline with typed target identity and idempotent cleanup. |
| Input/control copy | Implemented / Static Verified | Assets/02_Scripts/UI/SharedOptionsMenuUI.cs<br>Assets/02_Scripts/Config/Localization/Source/Localization.csv | Control labels identify Radar toggle and instant scan; tutorial dialogue/objective resolve current bindings. |
| Tutorial newline validation | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentValidator.cs | Embedded newlines in `dialogue.tutorial.*` language cells fail validation; normal RFC 4180 multiline support remains available elsewhere. |
| Phase 2C.3 automated tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs<br>Assets/02_Scripts/Localization/Editor/Tests/LocalizationFoundationTests.cs | Runtime and Editor assemblies compile; Unity Test Runner execution pending while the Editor is open. |
| Phase 2C.3 Play Mode | Not Started | Tutorial / Expedition | Verify Q/Mouse 4 input, lock behavior, both camera presentations, interruptions, optional route, 480x270 wrapping, and no normal-runtime duplicate localization service error. |

## Dialogue Presentation Phase 2C.4 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Korean-safe resolved wrapping | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueWordWrapUtility.cs<br>Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs | TMP word joiners protect whitespace-delimited tokens after localization/variable resolution; source content remains unmodified. |
| Stable TMP typewriter | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | Full text is laid out before `maxVisibleCharacters` reveal. Existing Pixel Crushers continue/pause ownership remains. |
| Configurable punctuation rhythm | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs | Uses unscaled 0.03 glyph, 0.08 comma, 0.18 sentence, and 0.28 ellipsis timing. Play Mode feel verification pending. |
| Operator text voice | Implemented / Static Verified | Assets/06_Audio/SFX/Talk/test_talk-sfx.wav<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | One reusable 2D source, UI mixer routing, every-two-glyph cadence, pitch 0.97-1.03. Volume/fatigue verification pending. |
| Dialogue presentation validation | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/DialoguePresentationValidator.cs<br>Assets/02_Scripts/Localization/Editor/LocalizationContentImporter.cs | Checks two-line reference layout, oversized tokens, generated wrapping characters in source, unbalanced rich text, unresolved bindings, and existing newline rules. Unity execution pending. |
| Phase 2C.4 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/DialoguePresentationTests.cs | Source compiles and covers wrapping, binding particles, rich text/placeholders, timing/cadence policy, prefab wiring, and reference layout. Unity Test Runner pending. |
| Phase 2C.4 Play Mode | Not Started | Tutorial dialogue at 480x270 | Verify four staged lines, Korean fallback, blip mix/fatigue, fast-forward, interruption, and source cleanup. |

## Cinematic Dialogue Presentation Phase 2C.5 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Typed presentation policy | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialoguePresentationPolicy.cs | Stable conversation/actor IDs select CompactGuidance, ContextFocus, CinematicCommunication, and actor theme; no localized-name matching or camera ownership. |
| Communication UI presentation | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | Reuses Pixel Crushers panels and selectable continue controls; adds 0/14/40% dim profiles, 14px top bar, 60/72px lower panel, 44px portrait area, and unscaled transitions. |
| Actor themes and text voices | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs<br>Assets/06_Audio/SFX/Talk/ | Operator cyan, Curse purple, Settlement warm, unknown neutral/silent. One reusable AudioSource selects existing Talk clips. |
| HUD/camera ownership boundary | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs<br>Assets/02_Scripts/UI/ExpeditionHUD.cs | Cinematic mode uses owner-scoped HUD suppression. ContextFocus does not acquire or release camera/input ownership. |
| Interruption cleanup | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs | Completion, stop-all, scene unload, disable, and destroy converge on idempotent unscaled cleanup and selection restoration. |
| Phase 2C.5 validation/tests | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/DialoguePresentationValidator.cs<br>Assets/02_Scripts/Dialogue/Editor/Tests/DialoguePresentationTests.cs | Policy, actor mapping, duplicate lifecycle, prefab layout/timing/audio, selectable continue, and existing 480x270 two-line validation compile. Unity Test Runner pending. |
| Phase 2C.5 Play Mode | Not Started | Tutorial / Settlement at 480x270 | Verify mode transitions, HUD restoration, target framing order, controller/mouse/keyboard continue, actor mix, interruption, scene loops, and missing-actor fallback. |

## QA Stabilization Pass 1 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Post-boss exit placement | Implemented / Static Verified | Assets/02_Scripts/RunRuntime/BossRewardExitCoordinator.cs | Exact-once deterministic pair is bounded to an authoritative combat/map area and avoids Player, reward/death point, blockers, and pair overlap. Play Mode pending. |
| Region-2 laser ownership | Implemented / Static Verified | Assets/02_Scripts/Boss/FrigateTriadBossController.cs<br>Assets/02_Scripts/Boss/BossLaserHazard.cs | Damaging beam follows the moving boss root; pooled geometry, collider, renderer, and active state reset explicitly. |
| Composite homing aim | Implemented / Static Verified | Assets/02_Scripts/Boss/FrigateBossPart.cs<br>Assets/02_Scripts/Player/Bullet.cs | Uses the active damageable collider's closest point and retains existing validity, faction, lifetime, and turn rules. |
| Boss entrance presentation | Implemented / Static Verified | Assets/02_Scripts/Temp/CoreBossIntroSequence.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Region 1 retains offscreen staging; Region 2 blends into the corridor before scroll handoff. Presentation timing is unscaled and cleanup remains owner-scoped. |
| Charged enemy aim commitment | Existing / Static Verified | Assets/02_Scripts/Enemies/EnemyAttackController.cs | Early prediction and final committed direction already drive the same warning and projectile; no production change required. |
| Radar persistence/presentation | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/RadarHUD.cs<br>Assets/02_Scripts/UI/PlayerRadarVFXController.cs | Scan remains open through ordinary combat; manual Q and lifecycle shutdown close it, while explicit boss-owned suppression remains. Background uses 55% alpha while markers remain full contrast; pulse is cyan. |
| Unknown objective/guidance | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/UI/RadarMarkerUI.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | One `?` marker follows objective lifecycle. Stable localization keys select open-plus-scan or scan-only detail without restarting dialogue. |
| Tuning Chip HUD | Implemented / Static Verified | Assets/02_Scripts/UI/ExpeditionHUD.cs | Reuses currency counter, wallet event, zero-value policy, and gapless repack; 480x270 Play Mode pending. |
| Settlement Use visibility | Implemented / Static Verified | Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs | Hidden before unlock; existing activate/deactivate authority is retained after unlock. |
| Shop purchase SFX | Implemented / Static Verified | Assets/02_Scripts/Shop/ShopTradeUI.cs<br>Assets/02_Scripts/Audio/SoundEventIds.cs<br>Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset<br>Assets/06_Audio/SFX/Shop/shop_buy_success.wav | Existing success clip is assigned to the existing Sound Event and invoked only after confirmed purchase. Play Mode mix verification pending. |
| QA Pass 1 EditMode tests | Implemented / Not Run | Assets/02_Scripts/Dialogue/Editor/Tests/QAStabilizationPass1Tests.cs | Unity-generated Editor project/test assembly refresh and Test Runner execution pending while Unity owns the project. |
| QA Pass 1 Play Mode | Not Started | Region 1 / Region 2 / Tutorial / Settlement | Verify boss exits, moving Frigate beam, homing, entrances, telegraph, Radar combat close, marker cleanup, HUD layout, unlock refresh, and success-only shop audio. |

## Tutorial QA Presentation Pass 2 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Movement-gated Operator opening | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | `PlayerController2D.MovementStarted` starts one pending transmission; natural Pixel Crushers completion remains required and the 2.75-unit movement objective is unchanged. Approved incoming cue asset/event assignment remains pending. |
| Restrained Radar pulse | Implemented / Static Verified | Assets/02_Scripts/UI/PlayerRadarVFXController.cs<br>Assets/02_Scripts/Temp/PlayerRadarScanner.cs | Reuses one pooled pulse; clamps cyan/blue color, 0.10-0.18 alpha, and 0.20-0.35s unscaled duration. Re-scan, manual/combat close, and teardown release it; Radar persistence and marker colors are unchanged. |
| Unknown-objective localization | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs<br>Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Player-facing unknown signal, Purple Core, interaction, and Curse error strings use stable Korean-first keys; the `?` Radar marker remains authoritative. |
| Purple Core reveal | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Visual starts at zero alpha/70% scale, reveals over 0.55s, keeps colliders disabled until ready, and idles only its visual root. |
| Purple Core Curse infiltration | Superseded | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | The later relay/Core interaction pass moves the Core sprite child while retaining the authoritative root position. |
| Tutorial QA Pass 2 tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Focused source/contract coverage compiles; Unity Test Runner execution is pending. |
| Tutorial QA Pass 2 Play Mode | Not Started | Tutorial at 480x270 | Verify delayed transmission, audio cue after assignment, two Radar scans, localized copy, Core reveal, transfer visibility, emergency return, and interruption cleanup. |

## Tutorial QA Presentation Pass 3 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Map-independent supply completion | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Supply damage is enabled from Map onward; existing death authority converges Map, route, and approach paths on one collection step. |
| Radar focus/combat persistence | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Temp/PlayerRadarScanner.cs | Target focus preserves open state; ordinary combat no longer closes Radar. Manual and boss-owned paths remain. |
| Persistent signal relay | Implemented / Static Verified | Assets/01_Scenes/Tutorial.unity<br>Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Authored at (-8.9, 15.33), remains visible after one-shot interaction, and reuses its renderer for one unscaled purple pulse. |
| Stationary Curse infiltration | Superseded | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Replaced by Core-sprite travel plus anchored ship-origin corruption; the authoritative Core root remains stationary. |
| Destroyed-reference cleanup | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Owned tweens stop first and Unity-null checks prevent refresh of a destroyed `PlayerVisualStateController`. |
| Tutorial QA Pass 3 tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Test source compiles; Unity Test Runner execution pending. |
| Tutorial QA Pass 3 Play Mode | Not Started | Tutorial at 480x270 | Verify no-map destruction, Radar persistence, relay position/pulse, stationary infiltration, and teardown safety. |

## Tutorial QA Cinematic Presentation Pass - 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Safe-viewport target presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Discovery records supply/wreck targets; focus begins only after 0.15s inside X 0.10-0.90 / Y 0.12-0.88. Invalid/interrupted targets do not complete guidance. |
| Purple relay/Core effects | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/03_Prefabs/Purple.prefab<br>Assets/01_Scenes/Tutorial.unity | Existing Purple ring is serialized once, driven with unscaled DOTween, and reset before pool release. Core reveal retains collider-safe visual-only behavior. |
| Core interaction cinematic | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Existing letterbox and owner-scoped camera framing precede Core-sprite travel and ship-origin corruption; background/player state changes only under full ScreenFader coverage. |
| Serialized mystery fallback copy | Implemented / Static Verified | Assets/01_Scenes/Tutorial.unity | Raw player-facing `???` fallbacks are replaced; stable localized IDs and the `?` Radar marker remain unchanged. |
| Focused EditMode coverage | Implemented / Not Run | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Adds viewport dwell/flicker, Purple prefab, transfer/fade ordering, and cinematic cleanup coverage. |
| Tutorial cinematic Play Mode | Not Started | Tutorial at 480x270 | Verify framing, pulse color/scale, reveal, transfer overlap, black-covered state switch, interruption cleanup, and the unchanged 28-step completion flow. |

## Tutorial Relay Takeover and Purple Core Interaction — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Real/Fake Operator relay handoff | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Analysis precedes real cutoff; stable `FakeOperator` uses a concealed Operator display name and Curse theme. Only natural fake completion advances. |
| Map/Radar unknown-signal ownership | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Relay analysis clears broad Map guidance; one Radar-only `?` persists to Core reveal and lifecycle cleanup. |
| Purple Core damage response | Implemented / Static Verified | Assets/02_Scripts/Player/IDamageable.cs<br>Assets/02_Scripts/Player/Bullet.cs<br>Assets/02_Scripts/Tutorial/TutorialInteractionTarget.cs | Typed player-only receiver, 0.12s/4-damage contribution cap, threshold 15, no enemy health/death/reward path. |
| Shared Core acquisition authority | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | `F` and threshold share one latch/conversation; simultaneous and duplicate requests fail closed. |
| Core-sprite travel cinematic | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Sprite child travels and restores on cancellation; pooled Purple effect is stationary support/ship-origin corruption; Curse remains after full black. |
| Deterministic content/tests | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/<br>Assets/02_Scripts/Dialogue/Editor/Tests/ | Nine graph sources and focused policy/progress tests compile; production Dialogue Database installation and Unity Test Runner are pending. |
| Tutorial pass Play Mode | Not Started | Tutorial at 480x270 | Tune threshold for each starting weapon; verify takeover pacing/theme, Radar-only marker, Core travel overlap, cancellation, and exact-once completion. |

## Boot Main Menu UI Authoring Migration Pass 1 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Serialized Boot view/bindings | Implemented / Static Verified | Assets/02_Scripts/UI/BootMainMenuView.cs<br>Assets/02_Scripts/UI/MainMenuController.cs | Runtime visual construction removed; missing references fail safely. |
| Editor installer | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Scene-authored hierarchy, Undo support, existing EventSystem/ScreenFader reuse, no automatic scene save. |
| Read-only validator | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Distinguishes missing root/component/view binding, missing typed children, wrong asteroid count, duplicates, inactive references, and missing sprites. |
| Idempotent repair/manual tuning preservation | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Repairs partial backgrounds slot-by-slot; valid transforms, colors, sprites, names, and references are preserved. |
| Authored options/background | Implemented / Static Verified | Assets/02_Scripts/UI/SharedOptionsMenuUI.cs<br>Assets/02_Scripts/UI/MainMenuController.cs | Thirty stars, seven asteroids, and the Curse passer are serialized; runtime only initializes behavior/motion. |
| EditMode authoring tests | Implemented / Not Run | Assets/02_Scripts/UI/Editor/Tests/BootMainMenuUIAuthoringTests.cs | Partial component, child, and null-view-binding fixtures added; Unity Test Runner execution pending. |
| Boot scene installation | Partial / Needs Retest | Assets/01_Scenes/Boot.unity | The user reproduced the partial install; rerun Install then Validate and save manually after verification. |
| Boot Play Mode | Not Started | Boot at 480x270 | Verify Continue/New Game/Settings/Exit, navigation, audio, fades, and no runtime-created duplicate UI. |

## Tutorial Guidance UI Authoring — Pass 1 (2026-09-06)

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| TutorialPrompt adoption and OperationStatus authoring | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/TutorialGuidanceUIInstaller.cs | Two presentation branches only; existing authorities, typed bindings, Inspector values, and Korean-safe fonts retained. |
| Installer and validator | Implemented / Static Verified | TutorialGuidanceUIInstaller.cs | Tutorial path only, scene-scoped discovery/direct creation, Undo, no save, accumulated diagnostics, partial repair and duplicate refusal. |
| Authored operation runtime boundary | Implemented / Static Verified | Assets/02_Scripts/UI/ExpeditionHUD.cs | Valid bindings bypass builder; fallback-disabled invalid bindings warn once and skip only visual. Timing and gameplay ownership unchanged. |
| Tutorial scene installation | Not Started | Assets/01_Scenes/Tutorial.unity | Load Tutorial, Tutorial > Install Guidance UI, review/Validate, then save manually and reopen. This delivery does not edit scenes. |
| Expedition fallback retention | Retained / Static Verified | ExpeditionHUD.cs; Assets/01_Scenes/Expedition.unity | Shared builder/default opt-in and Expedition serialized value retained. No safe construction deletion yet. |
| Focused EditMode tests | Implemented / Not Run | Assets/02_Scripts/UI/Editor/Tests/TutorialGuidanceUIAuthoringTests.cs | PreviewScene lifecycle; fresh/partial repair, adoption/rename, manual values, idempotency, ownership, missing/cross-scene diagnostics, briefing no-construction behavior. |
| Unity regression verification | Not Run | TutorialGuidanceUIAuthoringTests; existing dialogue/tutorial/UI suites | .NET compilation does not execute Unity Test Runner. Existing 28-step order, camera, input, Radar, pause, and interruption tests remain required. |
| Play Mode and reopen verification | Not Started | Tutorial and Expedition at 480x270 | Check saved bindings, briefing timing/hide/interruption, no duplicate operation hierarchy, missing-binding warning, dialogue/input release, Radar/Map, combat and transitions. |

## Tutorial Resource HUD Authoring (2026-09-06)

| Item | Status | Main Files | Notes |
|---|---|---|---|
| Resource path audit | Static Verified | RunWallet, RunManager, ResourceCounterUI, ExpeditionHUD | TuningChips and StabilizedAlloy already use run-wallet events; Alloy remains cargo. No new currency authority or rewards. |
| Resource-only Install/Validate menus | Implemented / Compiled | TutorialResourceHUDInstaller.cs | Requires existing Tutorial Canvas/ResourceRoot; typed and renamed adoption, partial repair, explicit scene ownership, Undo, no automatic save. |
| Authored row bindings and art | Implemented / Compiled | TutorialResourceHUDInstaller.cs; ResourceCounterUI.cs | Five unique field mappings; root/icon/TMP references, project pickup sprites, existing font conventions. Inspector edits survive reinstall; compact runtime layout remains intentional. |
| Current Tutorial mapping prerequisite | Manual Correction Required | Tutorial ExpeditionHUD Inspector | Credits Counter and Scrap Counter currently reference the same ScrapCounter. Assign the existing CreditsCounter to Credits Counter before Tutorial > Install Resource HUD. Ambiguities are refused, not guessed. |
| Tutorial runtime boundary | Implemented / Compiled | ExpeditionHUD.cs | One aggregate resource fallback flag disabled only by successful installation. Missing authored bindings warn once per resource; no replacement counter construction. Initial/re-enable refresh, event updates and compact visibility retained. |
| Expedition compatibility | Retained / Compiled | ExpeditionHUD.cs | Default-enabled resource fallback helpers remain; Expedition scene and its serialized configuration are not edited. |
| Focused resource regression tests | Compiled / Not Executed | TutorialResourceHUDTests.cs | Supported PreviewScene fixtures; isolated wallet/event relay, no real saves. Resource initialization methods and actual OnDisable cleanup exercised; full HUD OnEnable/Play Mode remains a manual check. |
| Static verification | Passed | Assembly-CSharp-Editor.csproj dependency build | .NET compilation with temporary external Compile items, including new tests; no generated project edits. Scoped diff/whitespace checks required at delivery. |
| Tutorial installation/save/reopen | Pending User Action | Tutorial scene | Correct mapping; Tutorial > Install Resource HUD; inspect Icon Image, Amount Text and RectTransforms; Validate; save manually, reopen and validate. |
| Unity integration verification | Not Executed | Tutorial and Expedition | Run focused and existing regression suites; check balances, zero hiding/packing, re-enable, pickups/spending, no extra rows, dialogue, pause, Radar/Map and transitions at 480x270. Settlement deferred. |

## Settlement Persistent-Resource Strip Authoring (2026-09-06)

| Item | Status | Main Files | Notes |
|---|---|---|---|
| Permanent-resource ownership | Preserved | SettlementHUD; SettlementController; PermanentProgress | ScrapParts, CoreShards, StabilizedAlloy; fixed order and visible zeros. No run-wallet binding, transactions or saves added. |
| Scene authoring tools | Implemented / Compiled | SettlementResourceHUDInstaller.cs | Install/Settlement > Validate Resource HUD; explicit loaded Settlement scene, existing Canvas/resourcesUI, 480x270 validation, Undo/rollback, no automatic save. |
| Serialized cell presentation | Implemented / Compiled | SettlementHUD.cs | Resource container/strip plus three named nested root/background/icon/value groups. No cell MonoBehaviours or new layout controller. |
| Preservation and legacy suppression | Implemented / Compiled | SettlementResourceHUDInstaller.cs | Existing references and styling adopted; only missing presentation authored. Legacy Graphics disabled with Undo; their objects, references and parent retained. |
| Runtime authored boundary | Implemented / Compiled | SettlementHUD.cs | Valid bindings bypass builder; partial authored strips never replaced. One actionable warning per HUD for missing bindings; unaffected cells/gameplay continue. |
| Compatibility retention | Retained | SettlementHUD.BuildResourceStrip/CreateResourceChip | One aggregate default-enabled policy; disabled only by validated installation. Do not retire before manual save/reopen verification. |
| Refresh and subscriptions | Implemented / Compiled | SettlementHUD.cs | Immediate re-enable balance refresh, existing Start/controller notifications, duplicate-safe subscription and actual-publisher cleanup. |
| Focused tests | Compiled / Not Executed | SettlementResourceHUDTests.cs | PreviewScene lifecycle and test-owned progress; repair, styles, Undo, rollback, ownership, events, zeros, authored/fallback behavior. Separate temporary additive scene covers save/reload without touching user scenes/saves. |
| Manual installation gate | Pending User Action | Assets/01_Scenes/Settlement.unity | Load Settlement outside Play Mode; Install; inspect resourcesUI/ResourceStrip/cells; Validate; save manually; reopen and validate. |
| Inspector layout controls | Documented | Resource container/strip/cell RectTransforms | No LayoutGroup added. Position on resourcesUI, strip insets on ResourceStrip, cell spacing via horizontal anchors/offsets; icon Image and Value TMP control art/typography. |
| Unity integration regressions | Not Executed | Settlement plus existing Boot/Tutorial/dialogue/pause suites | Verify permanent balances, zero visibility, spending updates, disable/re-enable, no extra cells or style resets, and unchanged navigation/story/launch behavior. |
| Broader Settlement work | Deferred | Navigation, previews, facilities, traits, technology, options | Embedded-script naming, shared status/message text and legacy close/back mappings are intentionally not repaired here. |

## Settlement Main Navigation Authoring (2026-09-06)

| Item | Status | Main Files | Notes |
|---|---|---|---|
| Navigation-only installer/validator | Implemented / Compiled | SettlementNavigationUIInstaller.cs | Explicit loaded Settlement, existing Canvas/owners/panels, typed/renamed adoption, partial repair, Undo/rollback, no auto-save or gameplay/settings lifecycle. |
| Authored presentation bindings | Implemented / Compiled | SettlementUIController.cs | Five existing view groups plus root, background, header, input CanvasGroup and six action/back labels. No general UI framework or per-cell scripts. |
| Destinations | Preserved | SettlementUIController | Hangar, Settlement Restoration, Ship Reinforcement, Additional Traits, Settings; panel refresh/lazy initialization and guarded launch remain unchanged. |
| Compatibility policy | Retained | BuildPersistentNavigation and helpers | Default-enabled single flag; installer disables after validation. Valid authored bindings skip construction/base styling; partial bindings warn once, never overlay clones. |
| Inspector styling | Implemented / Compiled | Controller navigation groups; button/label/icon RectTransforms and Graphics | No LayoutGroup added; direct child RectTransforms control spacing. Selected colors/icon alpha and strip/Outline visibility remain runtime-driven, using authored baselines. Button transitions and gameplay action text/interactability remain intentional. |
| Other typography writers | Narrowly guarded | SettlementHUD; ShipTraitTreePanel | Authored navigation owner suppresses only ship-action / trait-unlock font defaults. Resource HUD and panel content remain intact. |
| Persistent pointer identity | Implemented / Static Verified | SettlementPrimaryNavigationPointer.cs + meta | Matching file/class and unique GUID; original controller GUID retained. Unity MonoScript resolution/reload tests compiled, not executed. |
| Owned listeners/subscriptions | Implemented / Compiled | SettlementUIController.cs | Idempotent binding, matching persistent-action detection, actual-source cleanup, unrelated UnityEvents preserved; trait unlock remains panel-owned. |
| Focused regression tests | Compiled / Not Executed | SettlementNavigationUIAuthoringTests.cs | Test-owned PreviewScenes; install/repair/rename/styles, no replacements, Undo/rollback, routing/back/modal checks, listeners, subscriptions, compatibility and temporary save/reload. No user saves. |
| Manual installation / save / reopen | Pending User Action | Assets/01_Scenes/Settlement.unity | Settlement > Install Navigation UI; Validate; inspect at 480x270; manually save, reopen and validate again. Production scenes were not edited by this delivery. |
| Input / dialogue / launch integration | Not Executed | Existing dialogue and mandatory-story/launch suites | Verify mouse, existing W/S/Space, EventSystem/gamepad submit, authored links, cancel/settings, dialogue blocking, guarded launch and resource HUD preservation in Unity. |
| Existing unresolved scene script | Outside migration | Canvas/Temp_ClearButton (inactive) | GUID 38fef9b49173e9d498b97aaa02cf1d81 absent from Assets/package metadata. Inspect separately; never silently strip unknown components. |
| Remaining Settlement work | Deferred | Previews, facilities, traits, sector technology, settings contents | Unrelated embedded SettlementSectorTechnologyEntrySelection and shared status/message/close mappings are not repaired in this pass. |

## Settlement Navigation Usability Refinement (2026-09-06)

| Item | Status | Notes |
|---|---|---|
| Saved layout causes | Static Verified | OptionButton inactive and 28x22; Restoration / Additional Traits labels 20pt without autosizing. Cyan Outline represented focus, not active content. |
| Redundant Back removal | Implemented / Compiled | Installer hides Repair_HUD/BackButton and ShipTraitTreePanel/backButton, prunes links/default selections and retains optional compatibility references. Installed sector runtime omits SectorTechnologyBackButton; copied canonical Back objects are hidden with Undo. |
| Settings visibility and return focus | Implemented / Compiled | Existing OptionButton reused; Settings and ESC use the same EscSettingsMenuController. Prior panel remains active; valid prior focus is restored, with sidebar fallback. |
| Context-owned movement | Implemented / Compiled | Existing EventSystem Navigate/repeat; no Settlement W/S Update loop. Primary Mode.None plus local move handler, clamp/skip, standard Submit and consumed primary-only Space alias. Right enters active panel; boundary Left returns. |
| Active tab versus focus | Implemented / Compiled | Active panel drives strip and selected colors; primary focus drives cyan Outline. No label scale animation or cumulative styling changes. |
| Authored typography/layout | Reset command retired | Five-button layout, typography, padding and styling are edited only in the Inspector; ordinary installation preserves them. |
| Cancel/rebinding guards | Implemented / Compiled | Existing modal authority consumes one Cancel per frame, handles nested rebind/dropdown/display confirmation first, and preserves dialogue/pause restrictions. No Input Actions asset changes. |
| Focused test suite | 23 tests compiled / Not Executed | Expanded installation, event dispatch, bounds, focus, modal, Back, compatibility and preservation coverage. Settings tests use isolated presentation services and temporary scenes, not user saves. |
| Manual Unity gate | Pending | Run Validate Navigation UI, inspect Korean labels at 480x270, save manually and reopen. Execute navigation/resource/settings/dialogue/launch regressions and verify hardware repeat/submit and ESC transitions. |
| Broader Settlement work | Deferred | No sector-technology content migration, resource accounting, facility/trait transactions, settings preference migration or protected asset edits. |

## Settlement Ship Reinforcement Authoring (2026-09-06)

| Item | Status | Notes |
|---|---|---|
| Static panel/template migration | Implemented / Compiled | SettlementSectorTechnologyPanelUI owns serialized panel/detail/action and inactive template bindings. Existing five-ID catalog remains sole list source; no scrolling/prefab structure existed. |
| Installer/validator | Implemented / Compiled | Settlement > Install/Validate Ship Reinforcement UI. Explicit loaded Settlement, inactive binding, partial repair/renames, styling preservation, Undo/rollback, no save or gameplay lifecycle. Requires installed navigation. |
| Template identity | Static Verified / Reload Pending | SettlementSectorTechnologyEntrySelection now has matching script/meta; original panel GUID preserved. Typed Button/Image/Outline/icon/TMP references clone with the template. |
| Runtime population | Implemented / Compiled | One card per catalog ID, template excluded; no authored static rebuilding. Positions derive from template plus cardStep only during population. Data/selection feedback retained without base style resets. |
| Lifecycle/focus | Implemented / Compiled | Actual controller-publisher cleanup, immediate initial/re-enable refresh, idempotent owned listeners; disabled upgrade focus returns to selected card. Sidebar route remains available without Back. |
| Compatibility policy | Retained | createPresentationIfMissing defaults true, installer disables after validation. Partial authored bindings warn once and fail locally. No fallback builder deleted. |
| Menu organization | Implemented / Compiled | All thirteen authoring commands grouped under UI > Boot, Tutorial, Settlement; no Install All. Existing tool behavior preserved. |
| Focused regression tests | 14 compiled / Not Executed | Installation/styles/partial/Undo/rollback, ownership, script identity and temporary reload, catalog/detail mapping, balance/upgrade/max rules, listener lifecycle, focus/local failures/fallback, exact menu paths. Test-owned progress and disconnected SaveManager. |
| Shared regression coverage | Compiled / Not Executed | Navigation, resource, settings, dialogue and launch suites retained; compilation does not prove Unity Test Runner or Play Mode results. |
| Manual installation gate | Pending User Action | Load Settlement outside Play Mode, confirm navigation installed, Install Ship Reinforcement UI, inspect/Validate, manually save/reopen/Validate. Check Korean layout at 480x270, all five cards and upgrade/resource/modal/focus behavior. |
| Inspector editing | Documented | SectorTechnologyPanel RectTransforms/Image/TMP; TechnologyCatalog origin; inactive TechnologyCardTemplate size/first position/art/typography; owner cardStep spacing and selectedCardColor. No LayoutGroup added. |
| Retirement candidates | Deferred | Settlement resource/navigation/technology compatibility builders await manual installation/reload and consumer/test review. Expedition operation/resource and shared runtime/Editor Options construction remain required. Boot runtime menu builder already absent. |

## Settlement Restoration UI authoring — 2026-09-06

| Slice | Status | Evidence / next verification |
|---|---|---|
| Role-scoped installation | Implemented / statically compiled | SettlementRestorationUIInstaller adopts Repair_HUD, Bg, Cost, preview/arrows, four explicit indicators, detail/action TMP. Undo/rollback, missing-only repair, typed renamed/wrapped references; no production scene save. |
| Runtime presentation | Implemented / statically compiled | HUD typography/aspect/layout defaults removed only for Restoration. Indicator baseline feedback and dynamic content remain. Missing data warns once and fails locally. No new runtime builder or fallback flag. |
| Navigation/lifecycle | Existing ownership retained | UIController owns selection/listeners/publisher cleanup/re-enable refresh. Hidden Back and action-focus recovery; no W/S polling or additional input owner. |
| Authority | Unchanged | SettlementController / PermanentProgress / SaveManager own requirements, restoration and explicit access-key confirmation. Resource/nav/Ship Reinforcement migrations and shared compatibility paths retained. |
| Regression coverage | Compiled, not executed | Adoption, partial repair, wrapper/style preservation, idempotency, ambiguity/scene errors, rollback, Undo, inactive temporary serialization, no authoring side effects, selection/refresh, publisher cleanup, persistent callback deduplication, authority/idempotence, access-key state and focus/local failure fixtures. Existing dialogue/access-key/launch tests retained. |
| Manual gate | Pending | Load Settlement outside Play Mode with Navigation installed. UI > Settlement > Install Restoration UI; inspect/Validate Restoration UI; save manually/reopen/validate. Verify 480x270 Korean content and actual facility/action/modal flows in Unity. |
| Inspector ownership | Documented | Repair_HUD RectTransform; line/PreViewImage and child arrows/spheres; Bg detail TMP; Cost/ScrapCostText results; UpgradeButton/Text (TMP) action; HUD art/indicator bindings. No new LayoutGroup. |

## Settlement Additional Traits authoring — 2026-09-07

| Slice | Status | Evidence / manual gate |
|---|---|---|
| Editor installation | Implemented / statically compiled | SettlementAdditionalTraitsUIInstaller adopts existing owner, details/categories/ScrollRects; typed reference repair, Undo/rollback, explicit scene ownership and no automatic save. |
| Template and runtime | Implemented / statically compiled | Scene-local inactive clone of existing ShipTraitNodeButton prefab; no duplicated catalog; one reusable node per entry, four-category filtering, dynamic grid arrangement. |
| Inspector ownership | Documented | LeftPanel TMP/Image; category buttons and selection-motion settings; branch ScrollRect/Content GridLayoutGroup/ContentSizeFitter; inactive TraitEntryTemplate; UnlockButton and UnButton. |
| Gameplay/lifecycle | Existing authority retained | ShipTraitTreePanel purchase/toggle -> PermanentProgress -> SaveManager; balanced publisher/listener ownership. EventSystem focus and modal restrictions; no extra input polling. |
| Known existing binding conflict | Manual correction required | statusText and messageText both reference temp1. Keep statusText; clear only messageText or assign a distinct TMP before installation. Installer rejects duplicate roles without guessing. |
| Regression tests | Compiled / Not Executed | Authoring adoption/partial/styles/ambiguity/rollback/Undo, template identity, stable IDs and real definition categories, filtering/prerequisites/costs/ownership/activation, single dispatch, lifecycle/focus, missing authored data and legacy prefab population. Test-owned progress; real SaveManager disconnected. |
| Manual installation | Pending | Load Settlement outside Play Mode with Navigation installed; resolve alias, Install Additional Traits UI, inspect/Validate, save/reopen/validate. Run focused and existing Settlement/settings/dialogue/launch suites and verify 480x270 Korean content. |
| Retirement candidates | Retained | ConfigureSettlementPresentation / LayoutCostIcons / RebuildGeneratedNodeButtons remain uninstalled-panel dependencies; nodeButtonPrefab remains the Editor template source and legacy runtime source. Other panels and shared builders are untouched. |

## Verified UI retirement — 2026-09-07

This status supersedes earlier fallback-retention entries only for the three
Settlement features listed as retired below.

| Area | Status | Evidence / next action |
|---|---|---|
| Resource strip | Runtime builder retired / compiled | Saved three complete cells, fonts/icons and correct parents; Settlement-only HUD consumer. Amount/event refresh retained. |
| Primary navigation | Runtime builder/restyling retired / compiled | Saved five complete views and nested labels/decorations. EventSystem, modal/input, sounds and actions retained. |
| Ship Reinforcement | Static panel/card-child builders retired / compiled | Saved complete detail and inactive card template. Catalog cloning/selection/upgrade transactions retained. |
| Serialized compatibility | Preserved | Existing script GUIDs and migration flags retained. Retired flags no longer enable runtime construction; installers/validators still write/check them. |
| Additional Traits | Retirement blocked by saved configuration | All authored roots/backgrounds/template null; rebuildGeneratedNodesOnEnable=1. Keep prefab/style compatibility. messageText is already cleared; install/save/reopen/validate manually. Optional CanvasGroups remain optional. |
| Boot / Tutorial prompt / Restoration | No additional dead builder | Already authored/runtime-safe; preserve Editor authoring helpers and legitimate refresh/animation. |
| Expedition / shared Options / ship previews | Retained | Active shared runtime consumers or unmigrated presentation remain. |
| Regression coverage | Compiled, not executed | Three compatibility tests replaced with no-construction/local-failure tests; existing authored style, template population, lifecycle, transactions, Settings/dialogue/launch and Expedition coverage retained. |
| Production evidence | Static inspection only | FileID/parent/asset and GUID searches are not Unity validator execution. No scene/prefab/asset saves performed. |
| Unity gate | Pending | Run scoped validators and Test Runner, manually repair/save/reopen if needed, then scene-loop/modal/action and 480x270 visual checks. |

## Settlement static UI completion — 2026-09-07

| Slice | Status | Verification / remaining gate |
|---|---|---|
| Existing five migrations | Preserved; saved bindings inspected | User reports five validators passed. This pass independently traced saved roots/template fileIDs; it did not execute those Unity validators. |
| Additional Traits legacy retirement | Implemented / compiled | Runtime uses authored bindings and inactive template only. Prefab-based rebuild/static style/cost layout removed; categories, catalog sync, transactions and optional CanvasGroups retained. |
| Hangar authoring | Implemented / compiled; manual installation required | Install/Validate Hangar UI adopts main preview/arrows/indicators/detail/action/message/launch and adds four missing curse layers. Baseline feedback; no runtime text/aspect reset. |
| Settings authoring | Implemented / compiled; manual installation required | Install/Validate Settings UI creates/repairs an authored modal using shared Editor defaults, explicit scene ownership, Undo/rollback and no preferences/gameplay/save initialization. Runtime no longer builds the Settlement wrapper. |
| Settings lifecycle | Implemented / compiled | Required shared controls validated before acquiring pause/input; actual-publisher listener cleanup; serialized rebind/dropdown guards. Shared legacy controls remain optional outside the complete shared layout. |
| Runtime construction audit | Static inspection | No fixed visual construction remains in Settlement runtime owners. Dynamic trait/technology clones, shared event titles, TMP dropdown options and shared Options/Expedition compatibility remain. |
| Regression coverage | Compiled, not executed | New Hangar/Settings partial repair, wrappers/styles, idempotency, ownership, rollback/Undo, temporary reload, missing-binding/local-failure and single-listener fixtures; existing transaction/navigation/dialogue/resource/launch suites retained. |
| Serialized compatibility | Preserved | No script GUID/owner deleted. Legacy serialized fallback/generation markers remain inert for installers, validators and saved assets. |
| Unrelated missing script | Pre-existing / not repaired | Canvas/Temp_ClearButton (inactive), DebugSettlementDataButtons GUID 38fef9b49173e9d498b97aaa02cf1d81. Not caused by this cleanup. |
| Manual gate | Pending | Install Hangar and Settings separately; validate all seven Settlement slices, inspect at 480x270, save/reopen/revalidate. Run Unity tests and scene-loop/modal/focus/transaction/visual verification. |

Inspector: edit MainPanel/PreViewImage and four curse-layer RectTransforms/Images;
Player_Select title/body/action TMP; existing ship indicator Image baselines;
LaunchButton and MessageText. Settings backdrop is under Canvas/EscSettingsRoot/
SettlementSharedOptionsModal, with editable OptionsPanel tabs/controls/templates.
Hangar has no new layout group. Existing Traits grids and reinforcement cardStep
intentionally arrange dynamic entries. Saved Hangar font sizes are not normalized
by ordinary installation; inspect bounds after the removal of runtime defaults.

This entry supersedes earlier notes deferring Additional Traits retirement and
excluding Hangar/Settlement Settings. Shared non-Settlement builder retirement is
still deferred until every consuming scene is authored and independently verified.

## Expedition static UI authoring — code implemented, manual installation pending (2026-09-07)

Implemented scoped tools at VOID SCRAPPER > UI > Expedition:
Install/Validate Operation UI, Resource HUD, Status UI, Radar UI, Boss Status UI,
Return Confirmation UI, Wormhole Confirmation UI, Message UI; Validate Shop UI.
There is no Install All and no automatic production save. The saved Expedition
scene still has the old null operation/alloy/tuning/heat/hint bindings until the
user installs and saves. Do not enter Play Mode expecting retired builders to repair them.

Removed fixed operation/counter/triad/confirmation construction and shop/maintenance/
message default-style resets. Installed status/scope paths bypass shared defaults.
Tutorial still consumes shared status/cargo/core/hints/scope construction. The shared
Map/Inventory prefab is NOT wholly static-authored: its null map viewport/content,
legend, operation information and controls remain shared migration dependencies.
Inventory slots, contacts/routes/search regions, rewards, world gauges, drag feedback,
pause/Options, letterbox, fader and dialogue remain under their existing owners.

Inspector locations and runtime-driven properties are listed in SYSTEM_DESIGN.md's
Expedition section. In particular resource compaction owns row Y; gauges own fill;
operation/cargo animate position/alpha; triad bars use authored height and encounter
position overrides; radar scope baselines are edited on RadarScopeGraphic.

Static runtime and Editor/test compilation passed; Unity test execution, production
validation, temporary reload test execution, Play Mode and visual bounds checks are
not claimed. After manual installation/save/reload, test HUD refresh/re-enable,
resource zero transitions, boss/phase/triad displays, return and region confirmations,
Map/Inventory/radar/pause/ESC/dialogue isolation, and Expedition/Settlement loops.
Existing Settlement Temp_ClearButton missing-script issue remains unrelated debt.

### Expedition HP / armor / cargo layout — authored baseline; reset retired

The Status installer preserves saved Inspector values. Layout-reset commands are
retired. Use Install Status UI only for incomplete bindings, then Validate Status UI;
inspect, save manually and reload. Existing layout and typography remain authoritative.

Edit panel RectTransforms through `hpGauge` / `cargoRoot`; edit labels/numbers through
the four HP/cargo TMP references, borders on their Outline components, bars through
track Images and Slider fill-area RectTransforms, and armor background through
`armorTrackImage`. No new layout component is introduced. HP/armor fill ratios,
protection-state tint, cargo fullness tint and cargo fade remain runtime-owned.

Focused test source covers scoped resets, wrapped text, exact reference rectangles,
pixel alignment, mappings, values/fade, reinstallation, Undo and rollback. Static
compilation is not Unity execution. Outstanding: run `ExpeditionUIAuthoringTests`
in EditMode; inspect Korean glyph bounds at 480x270, damage/healing, armor changes,
cargo zero/full/fade, other status/operation separation, scene reload and Play Mode.

### Tutorial shared Status migration — manual application required

Editor tooling and runtime cleanup implemented. Before the next Tutorial Play Mode
session, load `Assets/01_Scenes/Tutorial.unity`. Correct the HP GaugeBarUI's invalid
`canvasGroup` assignment from StatusRoot to None (or an existing gauge-owned group).
Then use `VOID SCRAPPER > UI > Tutorial > Install Status UI`
and `Validate Status UI`; inspect, save manually, reload and validate again.
Normal Install preserves styling; no layout-reset command remains.

The fixed scope is edited on RadarScopeGraphic; contacts remain dynamic. HP/cargo
root RectTransforms, TMP label/value references, track Images, border Outlines and
armorTrackImage are Inspector-owned. Tutorial's absent core tracker is not invented.
Stage-controlled activation is preserved; installers never invoke flow/lifecycle,
story, checkpoint, transaction, settings or save actions. Resource/Guidance tools
and their scene bindings remain independent.

The former Tutorial-dependent fixed runtime builders are retired. Serialized flags
remain inert for asset/Undo compatibility. World charge presentation and dynamic
Radar/Map/Inventory, shared pause/options and existing animation/state refresh are
retained. No owner script/GUID was removed.

Outstanding Unity verification: execute ExpeditionUIAuthoringTests and existing
Tutorial guidance/resource/dialogue/input regressions; verify Korean text at 480x270,
radar stage introduction and contacts, HP/armor/cargo changes/fade, heat/reinforcement,
Map/Inventory hints, pause/dialogue isolation, save/reload and scene re-entry. Test
source compilation is not test execution or production validator success.

### Weapon heat authored layout — reset retired; Unity verification pending

- Historical heat reset commands are now retired. Install Status UI completes
  missing bindings only; Validate Status UI is read-only. Edit layout in the Inspector.
- Recovered 92x4 at top-left (8,23), with stationary panel/90x2 inset track and a
  non-interactable, handle-free Slider driving the leaf fill. This fixes saved
  sprite-less Filled Images, whose fillAmount does not affect UGUI's plain quad.
- Ordinary installation preserves later manual edits. Status validation checks
  publisher and role ownership, correct fill mapping, no pointer interception or
  navigation, and 480x270 bounds/overlap. No heat gameplay or input code changed.
- Added test-owned PreviewScene coverage for both scene modes, state refresh,
  subscriber ownership, stable geometry, idempotency, Undo and late rollback.
  Static compilation is distinct from executing these tests in Unity.
- Pending: run each validator, inspect heat at zero/partial/
  full/overheated/recovered, verify tutorial restrictions and weapon changes,
  save/reload each scene, and test scene re-entry. No rendered verification claimed.

Tutorial prompt-overlap correction: implemented in Editor tooling, pending Unity
execution. Common-screen Graphic bounds replace logical prompt-root intersection.
Automatic nearest-clear repositioning is now retired; current saved positions
remain authoritative. Read-only reports distinguish blocking visible overlap
from non-rendering/uncertain cases. Added inactive/nested/transparent-container,
different-scaler/camera-viewport, genuine-overlap, no-space rollback and Undo tests.
Run Tutorial Validate Status UI, inspect populated
guidance stages, then manually save/reload. No production scene changes were made.
