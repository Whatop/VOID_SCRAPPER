# VOID SCRAPPER 설계 변경 원장

## 2026-09-23 - Equipment manufacturing economy calibration

- Finalize all 48 Scrap/Core recipes from actual generated-map supply and cargo/return-risk estimates; no reward inflation or combat/rarity changes.
- Add exact-once saved 86-Scrap starter materials after first Settlement introduction, derived from the six initial recipes plus a rounded 15% buffer.
- Extend existing authored inspection with cost/current-funds icon rows, zero-cost hiding and insufficient-funds state.
- Add Editor-only captured-map diagnostics and focused starter/recipe/return/save/UI tests; preserve ownership, unlimited fitting, Lv1 deployment, frames and campaign authorities.
- Document the first Region3 arena/repeat-route distinction, limited non-boss Core, and optional-trip assumptions in EQUIPMENT_ECONOMY.md. Operating Frame numerical balance and Special upgrade frequency remain separate follow-ups.

## 2026-09-22 - Operating Frame foundation

- Add free saved Lightweight/Standard/Heavy selection separate from the 48-position equipment roster.
- Snapshot frame and effective ordinary fitted count once per run; conditional/legacy equipment counts without bypassing acquisition prerequisites.
- Apply the specified provisional profiles through existing player stat/modifier owners, with portal-safe vital and upgrade reconstruction.
- Author three frame controls above Settlement branch tabs and a read-only inventory inspection; update the unpacked Expedition copy in place.
- Add named-placeholder localization, F10 projection and 30 regression/diagnostic cases in the existing Equipment Development suite.
- Preserve version-7 saves via default Standard, equipment ownership/fitting and campaign checkpoints. No dash charges, Curse or economy work.

## 2026-09-21 - Equipment Development correction, pass 1

- Supersede global capacity and ordinary Lv0 preparation with four research branches, one-time manufacturing and unrestricted fitting.
- Author 32 real blueprint positions / 16 pending positions; retain 14 surplus Shared definitions for existing owners. Keep IDs, combat values and rarities.
- Save manufacture/fitting separately in version 6; migrate valid old fitting/purchased ownership once, preserving backward F10 research gating.
- Seed ordinary deployment Lv1 after run reset; reconstruct effects once per player, preserve portal upgrades/vitals, and clear runtime levels on abandon as well as RunEnded.
- Preserve Terminal Guidance prerequisites, immutable reward filtering and earned refunds; free starting levels carry zero-refund field-drop provenance.
- Reuse the authored Settlement panel/tabs/detail controls, localized manufacture/Fit/Unfit states and separate fitted indicators. No new manager or Settlement Upgrade action.
- Validation and remaining content/economy decisions are recorded in IMPLEMENTATION_STATUS and EQUIPMENT_DEVELOPMENT_MAPPING.

## 2026-09-21 - Equipment catalog role and balance polish

- Audit the actual 50-entry catalog: 46 normal, three hidden boss, one Pixel Curse.
  Preserve the existing seventh MG/Sniper dash signatures, stable IDs/GUIDs and rarity.
- Rewrite 46 purpose descriptions; adjust 19 incremental curves/level packages, including
  seven former Max1 equipment with supported Max3 progression. Retain distinct specialists,
  hybrids, salvage, tactical and signature roles; add no universal debuffs.
- Correct charge-speed effect text, unify Settlement/run effect formatting and describe
  per-level application rather than falsely summing mechanical configuration.
- Add catalog/consumer/UI/source eligibility regressions and deterministic 3/6/9/12
  reward diagnostics. Keep production reward generation and prepared-loadout authority unchanged.
- Exact before/after values and playtesting questions are in CONTENT_PIPELINE_AUDIT.md
  and Docs/Design/EQUIPMENT_CATALOG_AUDIT.csv. No scene or prefab changes.

## 2026-09-21 - Equipment Development UI refinement

- Retire the remaining legacy Upgrade action display and old paragraph/clear-slot
  presentation from the authored Equipment Development path. Keep legacy save data
  and unrelated persistent story trait authority compatible.
- Separate inspection from free activation/deactivation; retain optional empty slots,
  fixed capacity, compatibility and the existing run candidate snapshot.
- Author compact per-level effect rows, MaxLv/unowned wording, compatibility tag,
  locked research conditions and explicit active/inactive status. Share the run reward
  effect formatter; do not invent cumulative totals, prices or equipment drawbacks.
- Grant the final row at natural third-component analysis completion before Route Core
  assembly. Persist that milestone in the existing flag store and add F10 preset 11.
- Keep activation controls above the existing launch footer; validate both bounds and
  the rendered 480x270 result. Runtime acquisition and catalog balance are unchanged.

## 2026-09-20 - Tutorial cinematic and Radar presentation polish

- Extend owner-scoped HUD cinematic presentation with an optional unscaled fade;
  Tutorial camera focus uses 0.22 seconds and hides irrelevant prompts immediately.
- Separate the existing Core pulse from VisualRoot; own one Ready-only stationary wave,
  stop it before travel and restore only unconsumed presentation after interruption.
- Retire Radar sprite-frame arrays and coroutine interpolation. Repurpose the existing
  frame Image into an authored sprite-free sweep in both Tutorial and Expedition.
- Use owned DOTween open/close/idle/scan presentation and preserve boss suppression,
  manual close, marker symbols, background reduction and scanner gameplay authority.
- Fix the exact dialogue-active launch-guard fallback when localization is unavailable.
- Extend existing Tutorial authoring/discovery tests; actual Play Mode probe covers
  paused fading, scanner presentation and interrupted Core travel. Visual review at
  480x270 and a complete Tutorial narrative playthrough remain outstanding.

## 2026-09-20 - Boot lifecycle and authored Options recovery

- Separate Boot configuration recognition from permission to mutate persistence; claim
  a one-shot request and use sceneLoaded instead of retrying persistence from Start.
- Author the existing result presentation under CoreRoot; remove runtime hierarchy
  construction/reparenting and retain RunEnded, counters, fades and Continue behavior.
- Preserve all existing Options objects and Inspector values. Repair only the reversed
  Resolution/FrameLimit dropdown guards; add deep validation and a Boot-only authored path.
- Preserve the first Settings Open across deferred Awake to fix the blank screen.
- Guard the navigation pointer's destroyed owner during Settlement -> Boot unload;
  leave Settlement progression, layouts and interaction behavior unchanged.

## 2026-09-20 - Unified Settlement Recovery / Route Core panel

- Evolve one existing primary navigation slot at the authoritative Assembled boundary;
  retain Activated and later status without new flags or optional-facility gates.
- Reuse Repair_HUD for restoration and a compact Route Core summary; transition in place
  immediately after the existing explicit Recovery Processor action and save.
- Move the existing authored deck shortcut into the core view while retaining EnterDeck.
  Preserve the physical core's activation, corrupted-defense and final-launch ownership.
- Keep optional facilities accessible through a subordinate mode toggle; preserve the
  shared blue hover/yellow selected style and the independent read-only progress panel.
- Add 14 Korean/English UI localization keys and clarify the final required restoration
  description. Reuse component names; import the catalog through the normal Unity tool.
- Extend the existing restoration tests with authority-driven status, immediate handoff,
  optional access, navigation visuals, deck round trips and localized layout coverage.

## 2026-09-20 - Settlement / Tutorial UX polish

- Add a scene-authored read-only objective/progress block to the existing Settlement sidebar.
- Project story parts, pending analysis, restoration, activation, defense and final state
  from PermanentProgress; refresh through existing change events and scene lifecycle.
- Unify navigation/card hover and focus as blue; preserve yellow selected state on exit.
- Separate reinforcement preview from selection. Details and the purchase transaction use
  the explicitly selected card; Click/Submit commits it, hover/focus cannot steal it.
- Extend the existing explicitly remote audio hooks with a compact unscaled DOTween panel
  reveal and first-subtitle gate. Preserve local NPC paths and Pixel Crushers lifecycle.
- Add 20 Korean/English localization keys with named placeholders and extend existing UI,
  dialogue and audio regression coverage. Gameplay, economy and dialogue content unchanged.

## 2026-09-14 - NULL DISPATCHER Pass 4E

- Add a final-only optional ending hook after BossDeathPresentation; retain the existing
  BossDummyController -> RunManager FinalVictory authority and exact-once death guard.
- Explicitly cancel final combat before death VFX, independent of health-event subscriber
  order. Defer final background recovery until after the ending shutdown.
- Add NullDispatcherEndingPresentation with inner-core flicker, neutral shutdown pulse,
  source-owned player/weapon/camera locks, safe dash cancellation and idempotent cleanup.
- Install FINAL_NullDispatcherTermination: four Dispatcher subtitles, two Operator
  subtitles, no choices and one natural terminal marker. Keep the player's purple copy.
- Reuse the existing treatment entry without changing its interruption notifications.
  Allow one ending restart and controlled fallback on genuine start/lifecycle failure.
- Author ending bindings on the existing final prefab using supported Unity prefab APIs.
  Reuse core5/inner-core/outer-shell/pulse art. Add seven ending CSV keys and regenerate
  the catalog/database only through Unity's importer and deterministic installer.
- Preserve RunResult copy, resource commitment, final completion save authority, Continue
  -> Settlement, all previous combat phases, corrupted defense, and Story Recovery.
- Ending visual timing is 2.5 seconds plus existing user-paced subtitles; do not enforce
  a short timeout on readable dialogue. Art/audio/credits and Play Mode acceptance remain.

## 2026-09-14 - NULL DISPATCHER Pass 4D

- Add an exact-once 20% gate using the existing owner-keyed health-floor API, including
  the Weapon Lab support damage window. Release it permanently when FinalPhase starts.
- Author outer shell, four sprite remnants and an inner corrupted core on the existing
  prefab. Add a 2-second DOTween shell-break presentation with independent gameplay timing.
- Alternate two sparse final combinations, preserve polarity, and increase ongoing
  1-HP support to a 3.5-second cadence. Keep packet caps and one-time support unchanged.
- Retain BossDummyController -> RunManager -> FinalVictory/RunResult authority. No ending,
  credits, dedicated art, final audio or new run-ending system is implemented.
- Extend QA regression coverage for the final gate, interruption, visuals, support,
  cleanup and floor ownership; update previous prototype-death tests to traverse the gate.
- Execute Runtime/Editor static compilation and Unity catalog import/validation. Final full
  EditMode suite: 415/415 passed. Correct two pre-existing inactive-physics test fixtures;
  player movement production code is unchanged. Manual Play Mode remains pending.

## 2026-09-13 - NULL DISPATCHER Pass 4C

- Replace the temporary post-support attack continuation with encounter-local White/Black
  Phase 2. Retain the Pass 4A/4B treatment, branch and one-time facility support authority.
- Add two pooled packet prefabs and one concrete component: opposite-polarity hostile
  diamonds use ordinary damage; matching Settlement plus packets heal 1 every ~5 seconds.
- Evolve Compression Dispatch and Phase Redirect; retain neutral Route Partition lasers.
  Clear both packet kinds BEFORE each two-pattern polarity switch and its 0.5-second cue.
- Author a compact persistent polarity label in the Expedition scene HUD and three CSV keys.
  Both black and white packet silhouettes have contrasting outlines from existing sprites.
- Extend QAStabilizationPass1Tests with Phase-2 rules, scheduler protocol, health, pause,
  reuse, cleanup and authoring coverage; update three earlier post-support expectations.
- Static compilation, source localization validation and serialized-reference checks are
  distinct from Unity execution. Unity Test Runner and Play Mode are pending.
- No <=20% phase, dual-pattern climax, shell break, ending or dedicated art in this pass.

## 2026-09-13 - NULL DISPATCHER Pass 4B

- Replace immediate post-choice attacks with playable Accept reclaim resistance or a
  damage-free Reject transition, converging on the existing Settlement facility support.
- Add a source-owned contribution to PlayerController2D's external-push composition;
  preserve normal movement/dash and use direct current-HP restoration for capped drain.
- Author ReclaimBeamRoot and ReclaimBreakPulse on the existing final prefab. Add four
  localized non-modal HUD messages and reuse existing boss warning/transition sounds.
- Keep boss protection through branches; hold player firing through support, clear old
  player shots before removing protection, and allow Weapon Lab damage. Support completion
  resumes the existing pattern pool in temporary PostSupportCombat.
- Preserve support UnityEvents and add exact-once C# completion. Fix missing-progress
  completion and cancellation of engine buffs using existing source-owned stat modifiers.
- Extend existing QA tests and adapt Pass 4A continuation assertions. No dialogue graph,
  permanent progression, regional campaign, defense or StoryRecovery changes.
- Static Runtime/Editor builds and CSV validation pass; Unity Test Runner and Play Mode
  are pending. Polarity, final shell break, final art and ending remain future work.

## 2026-09-10 - NULL DISPATCHER Pass 4A

- Add one final-boss combat controller and authored prefab bindings for three sequential
  telegraphed patterns using existing laser and pooled bullet infrastructure.
- Introduce source-owned EnemyHealth damage floors: intro protection and an exact 50%
  treatment gate that cannot be skipped by lethal damage. No regional health policy changes.
- Add nine CSV keys and a deterministic Pixel Crushers installer/validator for six
  disclosures, Accept/Reject responses and one completion marker. Runtime choice is
  provisional until natural completion; interruption clears it and re-offers safely.
- Make legacy automatic 45% support manual-only; retain the callable support system.
- Source-owned cinematic locks, dash cancellation and idempotent cleanup preserve the
  recent movement fix. Either choice resumes temporary Phase-1 patterns and FinalVictory.
- Extend QA and story dialogue suites for threshold/choice/floor/cleanup/authoring
  contracts. Static Runtime + Editor/test compilation and CSV validation pass. No Unity
  Test Runner or Play Mode execution claimed; generated content import/install is pending.
- Reclaim Beam, NPC intervention, branch consequences, polarity, shell break, ending and
  final art remain later work. Settlement defense and StoryRecovery code/bindings are preserved.

## 2026-09-10 - Corrupted Three-Core Settlement Defense

- Replace live transmission anchors and Raider defenders with one reusable corrupted
  component prefab and green -> blue -> orange sequential combat configurations.
- Separate combat defeat from explicit reactivation and fusion; only the third fusion
  permits existing Route Core defense completion/save and final expedition eligibility.
- Add failed-fusion intro, corruption wave, cleanse/central fusion, and stabilization
  DOTween presentation without player-control locks. Retain explicit Recovery Processor
  restoration/assembly and existing Activated -> DefenseCleared progression ownership.
- Hide the authored ReturnToFacilities button during defense and restore prior UI state
  on completion, death or cancellation. Unlimited retries reset only encounter runtime.
- Retire live anchor/Raider bindings; retain the unused old anchor prefab for asset safety.
  Reuse core5 art, EnemyHealth and enemy projectile infrastructure. NULL DISPATCHER and
  its canonical finalBossPrefab binding remain unchanged.
- Extend existing QAStabilizationPass1Tests for phase/interaction/fusion authority,
  duplicate calls, cancellation at all stages, retry, UI and unrelated input ownership.
  Add five CSV localization keys; generated catalogs require normal Unity import.
- Validation: static Runtime and Editor/test compilation; serialized references,
  ordered identities, localization rows and unchanged final bindings audited. Unity
  Test Runner / Play Mode not executed. Combat balance and presentation await gameplay.

## 2026-09-09 - Campaign Vertical Slice Pass 3

- Author RouteCoreDeck, its player/light/interaction stages, a facilities entry button
  and compact deck HUD in Settlement.unity. Preserve existing objects/components;
  only four existing records change for the button, message binding/layout and roots.
- Preserve Recovery Processor as the restoration/assembly boundary; visiting the Core
  at ReadyToAssemble cannot bypass it. Activation uses existing Route Core authority.
- Add one concrete SettlementDefenseEncounterController: three 100-HP transmission
  anchors plus six existing Raider escorts at 24 HP. Destruction events alone finish
  defense; retry/cancel/death/scene exit cleanup never grants a clear. Auto-clear stays off.
- Reuse EnemyHealth and core5 art in PF_SettlementTransmissionAnchor. Author the deck's
  existing Global Light 2D so the reused lit sprites remain visible. Clear two missing
  Animator controller references only on already-disabled copies of player Animators.
- Add PF_Boss_NullDispatcher and bind Core.finalBossPrefab. It is a 300-HP damageable
  target with existing intro/health/death/FinalVictory plumbing, no attack controller,
  and dormant existing support hooks. No final combat patterns or ending implemented.
- Extend gameplay-state recognition to defense/final combat and apply the existing
  emergency-return boss restriction to FinalBossBattle.
- Extend QAStabilizationPass1Tests for authoring, bounded defense start, destruction,
  cancellation, exact-once restoration/activation/completion, final binding and victory.
- Runtime and Editor/test assemblies compile; serialized GUID/local-reference and
  hierarchy checks pass. Unity Test Runner and manual Play Mode remain unexecuted.

## 2026-09-09 - Campaign Vertical Slice Pass 2

- Stop Region 2/3 combat synchronously at authoritative death before starting the
  shared death sequence. Remove dependence on EnemyHealth.Died subscriber order.
- Convert Phase Gatekeeper focus/offset control to owned camera calls; cancel active
  dash through its owner at intro acquisition. Preserve the verified Region-1 fix.
- Preserve intentionally empty active equipment during portal bootstrap instead of
  granting default starting gear again. Keep the same RunContext and carryover data.
- Reject non-Normal run creation without authoritative permanent progress. An unwired
  Settlement defense event no longer strands the player in SettlementDefense; reject
  duplicate requests and unsolicited completion, release only owned state on cleanup.
- Clarify three existing CSV dialogue lines: Matter Compressor/last component, stronger
  anomaly and upcoming Region-3 authorization, and unsafe field assembly/explicit
  Recovery Processor restoration. No new dialogue graph or progression actions.
- Extend existing QA and Phase2C suites for encounter selection, death cleanup, final
  prerequisites, both portal transitions, active equipment and Story Recovery stages.
- Static runtime and Editor/test compilation passed; Unity Test Runner and Play Mode
  were not executed. The full playable slice remains incomplete: Route Core authoring,
  Settlement defense encounter and NullDispatcher prefab binding are absent locally.

## 2026-09-09 - Story Recovery Inventory Presentation

- The user confirmed the boss-death stuck movement/dash fix. Preserve that flow.
- Missing dedicated story art now uses the shared prefab's authored generic core
  for world recovery; inventory icons remain disabled until dedicated art is assigned.
- Author StoryRecoverySection inside the existing inventory: title and exactly three
  horizontal read-only slots. Preserve cargo/equipment hierarchy and bindings.
  Mirror this addition into Expedition's unpacked authored inventory copy so the
  runtime scene receives it; Tutorial already references the shared prefab.
- Refresh from PermanentProgress directly; pulse only an already-open matching slot
  after the world presentation completes. No new inventory, progression, or input owner.
- Add six ui.story_recovery localization source keys (title, two statuses, three names).
  Generated localization and dialogue assets remain untouched.
- Extend existing QA tests for fallback/override, read-only bindings, authoritative
  event refresh, duplicate/cargo guards, fixed icon space, control isolation, and tween cleanup.

## 2026-09-09 - Boss Death Recovery Hotfix

- Replace death/phase/intro stale movement-state restoration with source-owned
  control/weapon locks and idempotent active-dash cancellation. Preserve foreign
  locks and component enable states; release owned camera focus/input offset.
- Accept ordinary boss death and leave BossBattle before optional death/reward/exit
  presentation. Keep Emergency Return recent-combat restrictions and final gates.
- Freeze the resolved death anchor. Place a lone Beacon there when clear; use
  nearest sampled clear positions when blocked and center repeat exit pairs there.
- Add a dedicated storyPartSprite binding to each existing campaign definition.
  Reuse the recovery prefab for rise/hold/reparent/local flight/fade with no control
  lock. Missing art skips visual only; permanent grant/save and message survive.
- Extend QAStabilizationPass1Tests for control/dash/camera ownership, cancellation,
  death-state handoff, saved rewards/duplicates/cargo, anchors, and manually sought
  recovery tweens. Static compilation is separate from Unity/Play Mode validation.

## 2026-09-09 — Campaign Spine Pass 1

- Stop deriving next-region authorization from boss defeats. Keep the existing
  saved highestUnlockedDepth, preserve old saves' stored routes, and authorize
  DeepZone1/DeepZone2 only from naturally completed Active1/Active2 Settlement analysis.
  Reuse the current typed completion transaction, save on change, and block new
  Settlement launches while analysis is pending. No graph IDs or save schema changes.
- First story defeats grant/save unique permanent parts before death animation;
  first-clear exits are Beacon-only. Reuse the existing Raider prefab/definition
  for cleared Regions 1-3, with region-correct run-clear identity and no repeated
  story grants. Region 2 repeat maps omit the story corridor; Region 3 retains its
  first-clear coreless foundation and uses Core-based Raider encounters on repeats.
- Preserve explicit Recovery Processor / Route Core / defense / final launch gates.
  Region 3 never provides a FinalNetwork portal. Ordinary new runs still start at
  Normal and existing portal carryover remains authoritative. No final combat added.
- Share the existing Region-2 recovery visual with Region-1/3 story bosses, using
  unscaled DOTween and coordinator-owned lifetime independent of corpse release.
  Add three localized part-recovery and two route-authorization messages.
- Reveal valid exits together over 0.65 seconds with interaction/colliders gated,
  safe placement retained, portal eligibility rechecked after rewards, and owned
  tween cleanup plus immediate completion on presentation failure.
- Extend Phase2CStoryDialogueTests and QAStabilizationPass1Tests for progression,
  interruption guards, save round trips, old authorizations, carryover, exit decisions,
  shared prefab bindings and cleanup. No internal component-array manipulation or
  active PreviewScene requirement is introduced.
- Static runtime and Editor/test builds passed. Unity Editor is open on this checkout;
  no connected Test Runner was available, so new Unity test/playback results are not
  claimed. Import/Validate Localization Catalog and run focused/full Unity suites.
  Only two boss prefab recovery references were edited; authored scenes, Core/exit
  prefabs, campaign definitions and generated dialogue/localization assets are preserved.
  No commit or push.

## 2026-09-09 — Settlement boss-part and Pixel Curse narrative expansion

- Preserve all six original first-Settlement Korean lines. Insert two warnings
  before departure: parts can intensify the anomalous waveform, and collecting
  three parts does not automatically restore the key; use the Recovery Processor.
- Expand Active0/Active1/Active2/ReadyToRestore/Completed to two subtitles each.
  Keep legacy text keys, add seven keys, and revise only the legacy ready line to
  acknowledge three parts and the peak anomalous response. Translations remain empty.
- Keep STORY_FirstSettlementUnknownCore and original entry IDs 0-12; add opening
  IDs 13-14 and response IDs 15-19. Six conditional entrances, unconditional
  continuations and one shared terminal 12; no dialogue gameplay actions added.
- Extend existing Phase2C tests for branch traversal/speakers, required new keys,
  repeat-state exact-once start protection, deterministic installation and read-only
  projection of real unique-part/Curse/restoration state using test-owned progress.
- No bridge, transaction, reward, Curse formula, camera, UI or Boot lifecycle edits.
  The user reports the pre-task EditMode suite and previous Boot loops passed; this
  new content has not run in Unity Test Runner or Play Mode here. Runtime and Editor
  static builds passed (0 errors; 73 and 77 warnings respectively). Generated assets
  are unchanged and require the existing Import Catalog / Install Phase 2C Story
  workflow before validation and playback. No UI authoring tools were restored.
- Static CSV checks: 64 unique records, seven additions, one changed legacy key,
  no orphaned keys or fabricated translations; new Korean lines are 34-46 characters.
  Scoped whitespace checks passed and all 512 protected asset/project hashes match.

## 2026-09-09 — Boot re-entry ownership ordering

- Incoming `GameBootstrap` duplicate rejection now precedes the `scene.isLoaded`
  initialization gate. Its existing -32000 execution order deactivates the incoming
  Dialogue Manager and duplicate CoreRoot before default-order components awake;
  initial persistence/service/progression initialization still requires a loaded scene.
- Settlement Settings retains save-before-return and Close (pause/cursor cleanup),
  then delegates to `SceneFlowManager.LoadBoot()` instead of loading Boot directly.
  Expedition pause already uses this flow; Boot fade, GameState and run-ending
  presentation release remain owned by SceneFlowManager.
- The persistent canonical manager/service, Pixel Crushers single-instance/DDOL
  configuration and unexpected-duplicate localization Error are unchanged. No scene,
  prefab, dialogue, localization or authored UI values were edited.
- Added lifecycle coverage for gate ordering, scene-scoped/idempotent deactivation
  preserving localization authority, and save/close/LoadBoot routing. Native scene-load
  Awake timing and repeated Play Mode loops still require Unity verification; the
  user-reported 256 passing EditMode tests are the pre-change baseline, not a new run.
- Validation: runtime static build 0 errors / 73 warnings; Editor/test static build
  0 errors / 77 warnings. Scoped whitespace checks passed. All 512 protected
  scene/prefab/asset/InputActions/generated-project hashes match the pre-task state,
  including manually saved Boot. No Unity Test Runner or Play Mode run performed.

## 2026-09-08 — Five-failure targeted follow-up

- Confirmed Boot's manually saved 18 rebind rows and three dropdowns read-only:
  unique, correct types, correct OptionsPanel ancestry. No assignment/reorder/save.
- Corrected the native Settings conflict expectation to exact LogType.Log directly
  before Configure; retained rollback/retry and duplicate-free reconfiguration.
- Pause cleanup records original/runtime root and component IDs, isolates actions
  from real Editor devices, and removes only its InputBindingPersistence entry.
  Retained strict lifetime and dirty-state assertions. Current line 90 is the
  dirty-state check; actual acceptance and survivor IDs still require a rerun.
- Navigation tests now use real UI-module lifecycle/input processing with temporary
  settings, copied actions and keyboard/gamepad devices. Removed obsolete sector
  backButton reflection while retaining Back/Settings/no-replacement assertions.
- Supplied reinforcement failure plus installed uGUI source identify synchronous
  focus clearing before the presenter's check. Capture ownership before refresh;
  retain external focus and use existing card/sidebar fallback. Added semantic-ID,
  native-setter and ownership regressions without changing transactions or style.
- Runtime compilation: 0 errors / 73 warnings. Editor/tests: 0 errors / 77 warnings.
  Unity execution: 0 tests; passed/failed/skipped/inconclusive totals unverified.
  No safe runner connected, XML export, Play Mode or rendered result. All 511
  protected hashes match the fresh baseline, including the new Boot save.
  Nothing committed or pushed; no retired UI tool restored.

## 2026-09-08 — Third-pass EditMode stabilization and binding classification

- Replaced all five remaining additive temporary-scene helpers with PreviewScenes;
  removed active-scene changes and an unnecessary navigation clone into the user
  scene. Added an unsaved-Untitled environment regression without opening or
  replacing the user's scene (explicitly skipped when no Untitled scene is open).
- Pause cleanup tracks/destroys the root at creation and tests finally cleanup at
  pre-Awake, pre-Start and post-Start boundaries, including repeated teardown.
  Kept strict lifetime assertions and exact synchronous native Settings-conflict
  expectations; the anchored expectation tolerates only an optional final newline.
- Split charge-presenter geometry invariants from follower position ownership.
  No PlayerChargeGaugeUI/WorldGaugeFollower runtime changes.
- Established semantic Hangar selection and reinforcement CanShow readiness;
  enabled only test-added persistent callbacks for EditMode dispatch. Production
  routing, duplicate-listener protection and transactions are unchanged.
- Classified the saved trait template's absent CanvasGroup as supported optional
  data, not refresh-time loss. Boot's missing Settings input-guard arrays are a
  genuine saved-binding blocker: 18 rows and three dropdowns require manual
  Inspector assignment. Tests report exact object/scene paths; no automatic repair.
- Runtime compilation: 0 errors / 73 warnings. Editor/test compilation: 0 errors /
  77 warnings. Unity execution: 0 tests; passed/failed/skipped/inconclusive totals
  remain unknown. No production validation, Play Mode or rendered result claimed.
- Rechecked 498 assets / 6,951 script references, with no unresolved first-party
  source MonoScript GUID. All 511 protected-file hashes are unchanged. No UI tools
  restored, no production source or assets edited, and nothing committed/pushed.

## 2026-09-08 — Second-pass EditMode scene ownership and cleanup

- Inspected installed Unity 6000.0.69f1 managed IL: targeted ObjectFactory creation
  still invokes Editor default parenting. Fixtures failed to enforce final scene
  ownership before grouping; this explains a supported escape path for leaked
  roots and subsequent zero-owner searches. Removed ObjectFactory creation from
  affected tests, without changing runtime code or Editor default-parent state.
- Plain fixture roots are explicitly moved before parenting/component setup.
  Added handle/path/load/preview diagnostics, inactive scene-scoped owner searches,
  explicit owned-root destruction and post-close lifetime assertions.
- Replaced both EditMode-invalid SceneManager.CreateScene calls with additive
  EditorSceneManager.NewScene; strengthened finally cleanup and tween ownership.
- Sorted/materialized scene-scoped snapshots (including Expedition's lazy
  projection comparison). Retained user-scene preservation checks and strict leak
  detection. No global Resources.FindObjectsOfTypeAll snapshots remain in tests.
- Added grouping/inactive-owner and foreign-parent/cleanup regression cases.
  Kept charge render-callback, tutorial coroutine, pause action, bootstrap and
  runtime UI assertions; no authoring tools, migration flags or runtime builders
  were restored.
- Deliberate Settlement/TuningChips missing-binding tests now expect exact warning
  strings and restore references in finally. Exact Settings-conflict and duplicate
  localization expectations remain unchanged.
- Saved assets confirm the user's ItemDetailRoot, ShopProjectile and Settlement
  cleanup. Scan: 498 assets / 6,951 script references; no first-party unresolved
  source GUID found. HUD/navigation owners and item-detail bindings remain.
- Runtime and Editor/test compilation passed; no connected safe Unity runner,
  so zero Unity tests executed here and latest pass/fail/skip/inconclusive totals
  remain unverified. All 511 protected-file hashes match the start of this pass.

## 2026-09-08 — Phase 2C.2 discovery focus and interruption hardening

- Verified the saved Tutorial uses the existing GameplayCameraRig/player references;
  the optional tutorialCamera reference resolves through its existing runtime path.
  No scene or prefab edits are needed.
- Removed the ancient-wreck pre-focus viewport gate; Radar and map discoveries now
  share atomic reservation. Supply introduction's existing viewport hold remains.
- Reused owned GungeonStyleCamera2D unscaled SmoothStep focus (0.6s), followed by
  Pixel Crushers guidance and an owned live-player-framing return (0.5s).
- Separated discovery camera identity from other tutorial cinematic ownership.
  Cleanup captures input publishers, respects Unity destruction, yields to camera
  takeover, checks scene exits and player replacement, and stops only its own
  conversation. Aborted guidance remains replayable on later discovery.
- Confirmed route-free supply completion already uses HarvestObjectHealth.Died
  from Map/RoutePing/Travel/Destroy -> CollectResources; retained this transaction
  authority and added controller-level duplicate/convergence coverage.
- Added test-owned PreviewScene coroutine/camera boundary tests and retained the
  existing Pixel Crushers completion/localization tests. Static compilation is
  distinct from Unity Test Runner, Play Mode and rendered acceptance.
- UI authoring tools remain retired; fixed UI, charge-gauge correction, dialogue
  content, localization, rewards, saves and route/ping implementation are untouched.

## 2026-09-07 — Retire completed UI authoring tools; fix shared charge follow

- Fixed production Scene/Prefab presentation remains authoritative and untouched.
  Removed the completed VOID SCRAPPER > UI menu tree (14 installer/validator
  scripts and metadata), exclusive authoring helpers and installer-only tests.
  Runtime regression tests now consume disposable saved-scene PreviewScene copies.
- Removed proven inert migration policy fields. Existing scene YAML scalar remnants
  are left untouched; runtime owners, their MonoScript GUIDs and legacy object
  references remain. Missing-binding diagnostics now direct manual Inspector
  correction instead of naming removed menus.
- Corrected WorldGaugeFollower's mismatched synthetic snapped camera projection,
  canvas-local/anchored conversion and forced pivot. Both gameplay cameras use
  the existing late camera rig, not an attached Pixel Perfect Camera or Cinemachine
  Brain. Follow uses the actual render camera matrix, one optional screen-pixel
  rounding, immediate-parent conversion and a once-per-frame render callback.
- Removed PlayerChargeGaugeUI's root-scale cooling pulse. Fill, colors, visibility
  and hold timing remain; subscriptions detach from actual publishers and dedupe
  repeated weapon sources. No smoothing, layout polling or per-frame allocations.
- Retained world charge construction, radar/map/inventory dynamic content, authored
  template cloning, Boot background animation, runtime Pause/Options, gameplay
  authority, localization and input/modal ownership.
- Static runtime and Editor/test compilation passed. No Unity Test Runner,
  production installation/validation, Play Mode or rendered verification claimed.
  Manual movement/zoom/charge/pause/scene-loop checks remain in both scenes.
- Phase 2C.2 remains the next separate task; no dialogue, checkpoint, route, quest,
  save, camera presentation or Pixel Crushers integration changes.

> Previous UI tool workflows below describe retired migrations only. There is
> no current Install/Repair/Validate UI menu workflow.

### Historical record: 2026-09-07 — Freeze user-authored UI and complete Expedition reinforcement

- Retired public Settlement navigation and Tutorial/Expedition status/heat reset
  commands plus reset-only code/tests. Historical entries below describe their
  former behavior, not current workflows. Use Install only for missing bindings
  and Validate for read-only diagnostics; edit presentation directly in Inspector.
- Removed adopted Boot scaler reset, Hangar curse-layer reorder and operation
  panel sibling reset. Removed heat/reinforcement default Image styling writes;
  retained state/fill/localization/visibility and authored-baseline animation.
- Added Expedition Install/Validate Reinforcement Slot UI. Saved Expedition already
  contains an inactive bound slot; adoption preserves it. Missing roles/components
  copy only presentation defaults from saved Tutorial in an isolated PreviewScene.
  No production asset or gameplay state is edited, no new action owner is created.
- Added preservation, partial/renamed adoption, duplicate/ownership rejection,
  Undo/rollback, temporary serialization, source-equivalence and state-refresh
  coverage. Static builds are distinct from Unity test and visual execution.

작성 기준일: 2026-08-30

## 문서 목적과 판정 기준

이 문서는 기존 프로토타입 문서인 Docs/Reference/VOID SCRAPPER_잔해해역의 수확자_프로토타입.pdf와 현재 Unity 프로젝트를 비교한 변경 원장이다. 새 시스템 설계서의 대체물이 아니며, 구현되어 있다는 이유만으로 수치나 콘텐츠를 최종 설계로 확정하지 않는다.

근거 우선순위는 현재 1차 제작 코드, 현재 직렬화 프리팹·씬·ScriptableObject, AGENTS.md, 기존 프로토타입 문서 순서다.

- Confirmed Design: AGENTS.md 또는 현재 구조와 문서에서 설계 의도가 명시적으로 확인됨
- Implementation: 현재 프로젝트에서 동작 경로와 연결 상태가 확인되지만 최종 설계 여부는 미확정
- Testing: 구현되어 있으나 수치·콘텐츠·사용성 검증이 더 필요함
- Planned: 정의나 진행 경로는 있으나 완전한 플레이 경로가 없음
- Deferred: 프로토타입 범위에서 명시적으로 제외되었고 현재도 완성 근거가 없음
- Legacy: 현재 설계의 권위 있는 경로가 아니거나 호환 목적으로 남아 있음

이번 감사는 정적 분석만 수행했다. Unity Editor 및 Play Mode 검증은 수행하지 않았다.

### 게임 개요와 핵심 루프

Prototype document:
- 2D 탑다운 픽셀 슈팅 로그라이트이며, 탐사 → 수확 → 전투 → 귀환 → 정착지 성장의 순환을 제시한다.
- 전투는 적 전멸 자체보다 수확 경로와 고가치 장소를 확보하는 수단이다.

Current project:
- RunManager, RunContext, ExpeditionBootstrap, SettlementController가 같은 큰 순환을 유지한다.
- 런은 함선·무기 트리·해역·재화·특성·Reinforcement·화물·보스 진행을 함께 보존한다.

Status:
- Confirmed Design

Change:
- 핵심 루프는 유지됐지만 런 상태와 성장 축이 프로토타입보다 크게 세분화됐다.

Reason:
- AGENTS.md에서 현재 핵심 루프와 전투의 역할을 동일하게 재확인한다.

System-design impact:
- 새 설계서는 핵심 루프를 유지하고, 각 단계에 화물 관리·작전 목표·해역 효과·지역 연속 진행을 하위 루프로 추가해야 한다.

### 프로토타입 범위

Prototype document:
- HUD, 레이더, 수확, 전투, 상점, 코어, 지역 1 보스, 결과 화면, 정착지 성장까지를 검증 범위로 잡고 추가 지역·보스와 장기 콘텐츠는 제한한다.

Current project:
- 지역 1~3과 최종 네트워크, 캠페인 보스 정의, 튜토리얼, 서사 특성, 다수 이벤트·NPC·Reinforcement·영구 성장 경로가 추가됐다.
- 지역 2 Salvage Devourer와 지역 3 Phase Gatekeeper는 전용 프리팹·컨트롤러·직렬화 실행 경로가 추가됐다.
- 정착지 Route Core 연결과 최종 보스 프리팹은 여전히 완성된 직렬화 경로가 없다.

Status:
- Implementation
- Planned

Change:
- 단일 지역 프로토타입에서 지역 1~3의 전용 보스 조우를 포함한 다지역 캠페인으로 확대됐다. 정착지 후반 게이트와 최종 조우는 아직 부분 상태다.

Reason:
- 추가 지역과 보스는 기존 문서에서 프로토타입 이후 범위로 명시됐다.

System-design impact:
- 새 설계서는 지역 1~3 조우를 Implementation·Testing으로 다루고, 정착지 후반 게이트와 최종 조우는 Planned 상태로 분리해야 한다.

### 플레이어 목표와 Expedition 흐름

Prototype document:
- 단기 목표는 한 번의 탐사 후 귀환, 중기 목표는 정착지 복구, 장기 목표는 심층 진입과 보스 격파다.
- 정착지 → 탐사 → 코어 → 보스 → 안전 귀환 또는 심층 진입의 흐름을 제시한다.

Current project:
- ExpeditionOperationController가 매 탐사마다 고가치 잔해, 신호 조사, 방어망 교란 중 하나의 작전 목표를 만든다.
- 지역 1 → 지역 2 → 지역 3은 한 런 안에서 이어지며 RunContext가 체력·방어도·런 재화를 계승한다.
- 최종 네트워크는 지역 3 이후 즉시 진입하지 않고 정착지 캠페인 조건을 거쳐 별도 출격한다.

Status:
- Implementation
- Testing

Change:
- 자유 탐사 중심 목표에 지역별 작전 목표가 추가됐고, 최종 지역은 일반 심층 이동과 분리됐다.

Reason:
- Reason not documented

System-design impact:
- 단기 목표를 탐사 작전, 선택적 수확, 코어 신호 확보, 보스, 귀환 결정으로 재정의하고 최종 출격은 별도 캠페인 단계로 표시해야 한다.

### HUD

Prototype document:
- HP·Armor·XP, Credits·Scrap·Core, 상호작용·경고, 우하단 레이더를 중심으로 제시한다.

Current project:
- ExpeditionHUD와 Expedition 씬은 HP·Armor, 아이콘 채움형 Dash, Core Signal, 화물 적재, Credits·Scrap·Core·Alloy·Tuning Chips, Reinforcement, 무기 열·충전, 상태 효과, 작전 브리핑과 입력 힌트를 연결한다.
- 현재 HUD에는 런 XP 레벨 게이지가 권위 있는 성장 UI로 연결되어 있지 않다.

Status:
- Implementation
- Testing

Change:
- XP 중심 HUD가 Core Signal·Cargo·Reinforcement·상태 정보 중심으로 전환됐고 표시 재화가 늘었다.

Reason:
- AGENTS.md가 현재 Expedition HUD의 정보 우선순위와 Dash·Reinforcement의 아이콘 채움 방식을 명시한다.

System-design impact:
- 새 HUD 명세는 현재 계층과 데이터 소유자를 기준으로 다시 작성하고 XP 게이지는 레거시 항목으로 분리해야 한다.

### 조작

Prototype document:
- WASD 이동, 좌클릭 발사, 우클릭 Dash, Q 누르기 레이더, E 상호작용을 제시한다.

Current project:
- PlayerControls.inputactions는 이동 WASD, 발사 좌클릭, Dash 우클릭, Radar Q, 빠른 스캔 Mouse 4, 상호작용 F, Inventory E, Map Tab, Reinforcement R, Dismantle·Field Drop G, Cancel·Menu Esc를 정의한다.
- UI는 InputBindingUtility와 InputAction 표시 문자열을 사용한다.

Status:
- Confirmed Design

Change:
- 상호작용이 E에서 F로 이동했고 Inventory, Map, Reinforcement, Dismantle, 빠른 스캔 조작이 추가됐다.

Reason:
- AGENTS.md가 현재 목표 바인딩과 표시 문자열 규칙을 명시한다.

System-design impact:
- 조작표를 현재 Input Actions 기준으로 교체하고 모든 UI 키 표시는 재바인딩 대응 규칙으로 명시해야 한다.

### Radar

Prototype document:
- Q를 누르는 동안 스캔하고, 잠시 유지되는 지역 정보만 표시하는 시스템이다.
- Shotgun은 도발, Sniper는 적 경보 억제와 넓은 시야, Machine Gun은 기본 레이더를 가진다고 설명한다.

Current project:
- PlayerRadarScanner는 Q 충전 스캔, 열린 상태에서 Mouse 4 빠른 스캔, 반경 절반의 저주기 수동 레이더, 이동에 따른 지도 공개, 스캔 기억 시간을 구현한다.
- Sniper의 은폐·감지 보조는 기본 속성보다 sn_stealth_scan 특성 단계에 연결된다.
- Shotgun 도발과 무기별 경보 반응은 RadarTarget별 허용 플래그에 의존하며, 다수 일반 적 프리팹은 이를 비활성화한다.

Status:
- Testing

Change:
- 단일 Q 스캔에서 수동·빠른·근거리 탐지와 지도 공개가 결합된 시스템으로 확장됐다.
- 프로토타입의 무기별 레이더 규칙은 현재 모든 적에게 공통 적용되지 않는다.

Reason:
- Reason not documented

System-design impact:
- 스캔 종류, 반경, 기억 시간, 지도 공개 관계를 분리해 명세하고 무기별 반응은 확정 전 테스트 항목으로 남겨야 한다.

### Map과 경로 계획

Prototype document:
- 레이더는 전체 지도를 대체하지 않으며 최근 스캔한 지역 정보만 보여 준다.

Current project:
- MapDiscoveryController가 탐사 공개 마스크와 발견한 목표를 유지한다.
- ExpeditionRoutePlanner는 최대 5개 경유지, 자동 다음 경유지, 월드 가이드를 지원한다.
- Tab 지도와 E Inventory는 ExpeditionMenuController가 관리한다.

Status:
- Implementation
- Testing

Change:
- 별도 전체 지도, 탐사 안개, 경로 계획 시스템이 추가돼 레이더와 역할이 분리됐다.

Reason:
- Reason not documented

System-design impact:
- Radar는 탐지 수단, Map은 누적 공간 정보, Route Planner는 플레이어 계획 도구로 별도 장을 구성해야 한다.

### 함선과 무기 트리

Prototype document:
- 하나의 공통 함선 프레임에서 Shotgun·Sniper·Machine Gun 트리를 출격 전 선택한다.
- 무기별 HP와 고유 레이더·전투 특성을 제시한다.

Current project:
- 01_basic_ship, 02_shotgun_ship, 03_sniper_ship이 각각 기본 무기 트리와 해금 비용을 가진 별도 ShipDefinition이다.
- 세 함선의 직렬화 HP는 모두 20이며 Machine Gun 함선만 수확물 피해 보너스가 확인된다.
- SettlementController의 기본 ID basic_ship과 Machine Gun 자산 ID muchingun_ship이 일치하지 않으며, 튜토리얼 후 경로는 해금된 함선을 다시 선택해 보정한다.
- ShipTraitTreePanel에는 공용·Machine Gun·Shotgun·Sniper 영구 트리가 있으나 일부 비용과 조건은 시험 값 성격이 강하다.

Status:
- Testing

Change:
- 공통 프레임의 독립 무기 선택에서 함선 해금과 무기 정체성이 결합된 구조로 이동했다.
- 프로토타입의 무기별 HP와 Shotgun 5발은 현재 데이터와 일치하지 않는다. 현재 Shotgun은 6발이다.

Reason:
- Reason not documented

System-design impact:
- 함선과 무기 선택의 결합 여부, 함선별 패시브, 최종 수치, ID 불일치를 확정하기 전까지 테스트 상태로 기록해야 한다.

### Run 성장과 Trait

Prototype document:
- 적·수확·이벤트에서 XP를 얻어 현재 런 레벨을 올리고, 레벨업마다 공용 2개와 무기 전용 1개 중 하나를 고른다.

Current project:
- CurrencyType.Experience와 RunContext.CurrentLevel은 레거시 호환 필드로 남아 있으나 현재 보상 자산에 XP 지급이 없고 런 레벨업 구동 경로도 확인되지 않는다.
- 현재 런 성장은 Trait 획득·교체·강화, 특별 컨테이너, 보스 선택 보상, 상점·암시장, Tuning Chips를 통한 보유 Trait 강화로 구성된다.
- Trait은 희귀도, 선행 조건, 최대 레벨, 무기 분기, 필드 폐기·분해 가능 여부를 데이터로 가진다.

Status:
- Legacy
- Implementation

Change:
- XP 자동 레벨업 선택 구조가 현재 권위 있는 성장 경로에서 빠지고, 획득원 기반 Trait 성장으로 대체됐다.

Reason:
- CoreTypes.cs가 Experience를 레거시로 명시한다. 대체 성장 구조의 설계 이유는 문서화되지 않았다.

System-design impact:
- XP 레벨업 표와 XP HUD를 삭제 또는 레거시 부록으로 이동하고, Trait 획득원·중복 강화·교체·분해·Tuning 규칙을 새로 명세해야 한다.

### Pixel Curse

Prototype document:
- 현재 프로토타입 문서에는 영구 서사 패시브로서의 Pixel Curse 규칙이 없다.

Current project:
- 45_pixel_curse.asset은 숨김·영구 서사 Trait·부정 효과·필드 폐기 및 분해 금지로 정의된다.
- 튜토리얼은 감염, 시각 변환, 구조 신호 대화와 정착지 첫 대화까지 연결한다.

Status:
- Confirmed Design

Change:
- 일반 Trait과 구분되는 지속형 서사 디버프와 전용 시각 규칙이 추가됐다.

Reason:
- AGENTS.md가 Pixel Curse의 지속성, 획득 풀 제외, Debuff UI, 시각 규칙을 명시한다.

System-design impact:
- Trait 구조를 재사용하되 획득·저장·표시·비분해·비드롭 예외와 서사 진행을 별도 명세해야 한다.

### Reinforcement

Prototype document:
- 수확물의 낮은 확률 함선 증원과 상점 구매 항목을 언급하지만, 장비 슬롯·충전·재사용 구조는 정의하지 않는다.

Current project:
- Reinforcement Catalog에는 공용 및 무기별 41개 정의가 연결돼 있다.
- PlayerReinforcementController가 장착, R 사용, 충전 수, 순차 재충전, 런 간 지역 이동 보존, 생성물 수명과 소유권을 관리한다.
- 획득물은 F 교체·획득과 G 분해를 지원하며 Emergency Return은 무작위 드롭에서 제외된다.

Status:
- Implementation
- Testing

Change:
- 단순 일회성 보조품 개념이 능동 장비 슬롯과 충전식 지원 체계로 확장됐다.

Reason:
- AGENTS.md가 PlayerReinforcementController의 권위 범위를 명시한다.

System-design impact:
- 장비 유형, 획득원, 교체, 충전·재충전, 무기 제한, 생성물 소유권, HUD 계약을 독립 시스템으로 추가해야 한다.

### Cargo

Prototype document:
- Credits·Scrap·Core를 즉시 보유하며 별도 적재량, 중량, 자동 수집 규칙은 설명하지 않는다.

Current project:
- PlayerCargoController가 Scrap·Core·Stabilized Alloy에 적재 중량을 적용한다. 기본 용량은 100, 기본 중량은 각각 2·12·2다.
- 80%와 95% 적재 구간에서 이동·Dash 불이익이 생기며, 과적 시 수동 회수, 재화별 자동 회수 전환, 투기가 가능하다.
- Credits는 런 재화지만 화물 중량에는 포함되지 않는다.

Status:
- Testing

Change:
- 수집 재화에 화물 용량, 중량, 과적 단계와 Inventory 관리가 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 재화 소유와 화물 적재를 분리하고, 중량·임계값·이동 불이익·회수·투기·긴급 귀환 보존량을 명세해야 한다.

### Currency

Prototype document:
- Heal은 즉시 효과, XP는 런 레벨, Credits는 런 전용, Scrap과 Core는 영구 성장 재화로 구분한다.

Current project:
- Credits는 상점·NPC 서비스용 런 재화이며 귀환 시 영구 저장되지 않는다.
- Scrap·Core·Stabilized Alloy는 귀환 결과에 따라 PermanentProgress에 저장된다.
- Tuning Chips는 런 안에서 보유 Trait 강화에 사용되고 귀환 시 저장되지 않는다.
- Experience는 레거시이며 현재 보상 자산의 지급 경로가 없다.

Status:
- Implementation
- Testing

Change:
- Alloy와 Tuning Chips가 추가됐고 XP는 현재 성장 경제에서 제외됐다.
- Scrap·Core는 화물 용량과 귀환 방식에 따라 실제 보존량이 달라진다.

Reason:
- Experience의 레거시화만 코드에 명시돼 있으며 나머지 경제 개편 이유는 문서화되지 않았다.

System-design impact:
- 각 재화의 획득원, 런 내 사용처, 화물 중량, 귀환 시 처리, 영구 사용처를 하나의 경제 표로 다시 작성해야 한다.

### Map 생성과 경계

Prototype document:
- 80×80 사각 맵과 네 방향 월드 래핑, 시작 안전 반경 10, 중요 지점 최소 거리 20을 제시한다.

Current project:
- New Map Generation Config.asset은 지역 1 120×120, 지역 2 128×128, 지역 3 116×116, 최종 96×96을 정의한다.
- 시작 안전 반경은 18, 중요 지점 최소 거리는 28이다.
- WorldWrapController는 Obsolete이며 현재는 경고·감속·안쪽 밀기·하드 클램프를 가진 유한 경계를 사용한다.
- POI 군집, 환경 장식, 이동 소형 운석, 지역별 적·보상 배율과 세 가지 해역 환경 변형이 추가됐다.
- 지역 2는 맵 상단 Core와 그 아래 전투 회랑을 예약한다. 현재 회랑 설정은 반폭 9, 하향 길이 48이다.
- 지역 3은 Core·현장 기지·상점을 생성하지 않고 Phase Gatekeeper와 길이 4의 반사판 8개를 전용 전투 기반으로 생성한다.

Status:
- Implementation
- Testing

Change:
- 고정 80×80 래핑 맵이 지역별 크기의 유한 맵, 군집형 배치, 지역 2 회랑과 지역 3 전용 보스 기반으로 교체됐다.

Reason:
- Reason not documented

System-design impact:
- 맵 크기와 배치 수치는 테스트 값으로 표시하고, 유한 경계·POI 군집·지역 스케일·해역 변형과 지역별 보스 공간 예외를 생성 규칙으로 기록해야 한다.

### 수확 오브젝트

Prototype document:
- 보급 컨테이너, 고가치 잔해, 파괴된 선체가 Credits·Scrap·회복·낮은 확률 증원을 제공한다.
- 운석은 보상 없이 탄환과 이동을 막는 지형이다.

Current project:
- 세 수확 유형은 유지되며 RewardDefinition과 RewardDropper로 Credits·Scrap·Alloy·회복·Trait·Reinforcement를 조합한다.
- 고가치 잔해는 작전 목표와 연결될 수 있고, 파괴된 선체의 증원 진입은 경고 이동을 포함한다.
- 소형 이동 운석과 대형 운석, 보스 구역 엄폐 배치가 생성 설정에 포함된다.

Status:
- Implementation
- Testing

Change:
- 수확 보상이 새 재화와 장비 체계로 확장됐고, 고가치 잔해가 작전·적 역할과 연결됐다.

Reason:
- Reason not documented

System-design impact:
- 수확물별 내구도와 보상은 최종 밸런스와 분리해 표기하고, 작전·화물·경쟁 적과의 연결을 추가해야 한다.

### Enemy 전투 구조

Prototype document:
- Patrol → Alert → Combat → Search → Return과 Taunt 상태, 시야·피격·총성·동료 사망·코어 활성화에 따른 전투 전파를 제시한다.
- Basic, Shotgun, Charging, 단일 Elite의 수치와 공격을 정의한다.

Current project:
- 기존 상태 구조는 유지되며 EnemyDefinition과 공격 패턴 데이터로 분리됐다.
- Elite는 Machine Gun·Shotgun·Charging으로 나뉘고 Melee Charger와 여러 보안·역할 적이 추가됐다.
- 시각 탐지는 누적 감지, 레이더 탐지, 총성 소음을 사용한다. Sniper의 무조건 비경보 규칙은 현재 기본 규칙이 아니다.
- Basic HP 8, Shotgun HP 14, Charging HP 12 등 일부 현재 수치는 프로토타입과 다르다.

Status:
- Implementation
- Testing

Change:
- 단일 전투 FSM은 유지하면서 적 종류, 공격 데이터, 감지 방식과 난이도 스케일이 확장됐다.

Reason:
- Reason not documented

System-design impact:
- 상태 전이와 전투 전파는 구조 명세로 유지하고, 개별 수치는 현재 테스트 데이터로 분리해야 한다.

### Enemy 역할

Prototype document:
- 일반 교전 적과 보스 중심이며, 수확 경쟁이나 목표 방어를 별도 역할 계층으로 정의하지 않는다.

Current project:
- EnemyRoleController는 Patrol, Defender, RivalHarvester, Scavenger 역할을 제공한다.
- Defender는 중요 지점을 방어하고, RivalHarvester는 수확 후 기지로 운반·이탈하며, Scavenger는 드롭을 훔쳐 복귀한다.
- Dormant·Preview·Active 시뮬레이션 단계로 원거리 행동과 비가역 행동을 제한한다.
- 임시 유인과 감속은 출처별로 관리돼 오래된 효과가 새 효과를 지우지 않는다.

Status:
- Confirmed Design

Change:
- 적이 단순 전투 장애물에서 목표 방어·자원 경쟁·회수 방해 행위자로 확장됐다.

Reason:
- AGENTS.md가 역할 상태 보존, 최신 유효 유인 우선, 최강 감속 우선 규칙을 명시한다.

System-design impact:
- 전투 상태와 전략 역할을 분리하고, 각 역할의 목표·이탈·화물·시뮬레이션 조건을 명세해야 한다.

### Events와 현장 작전

Prototype document:
- Rescue Signal과 50% 안전 보상·50% 증원 전투의 Unknown Device 두 이벤트를 제시한다.

Current project:
- Rescue Signal과 Unknown Device 외에 제한 시간 안정화형 Unstable Reactor와 다중 방어 웨이브형 Black Box가 있다.
- 성공 이벤트, 특수 컨테이너, NPC 구조, Elite 화물, Rival Harvester가 Objective Signal을 제공할 수 있다.
- 별도의 ExpeditionOperationController가 지역당 하나의 탐사 작전을 선택한다.

Status:
- Implementation
- Testing

Change:
- 이벤트가 네 유형으로 늘고, Core 발견과 보스 보상에 연결되는 신호 경제 및 지역 작전이 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 이벤트별 시작·성공·실패·웨이브·보상과 Objective Signal 기여도를 명시하고 지역 작전과 분리해 설명해야 한다.

### Shops, 현장 기지와 NPC

Prototype document:
- 중립 상점은 수리·Trait·함선 증원을 판매한다.
- 보호막 파괴 시 모든 상점이 해당 런 동안 적대하며, 격파 보상과 사용 Credits 일부 환급을 제공한다.

Current project:
- 중립·경고·전역 적대 구조는 유지된다.
- 상점은 수리, 무작위 Trait 3개, Reinforcement 1~3개를 판매하며 지역별 가격 배율을 적용한다.
- 런 범위 Maintenance Bay가 현재 장비 외 Reinforcement를 보관한다.
- 중립 안전 구역, 전력 그룹·보안 노드·지역별 포탑과 드론 수가 추가됐다.
- 현장 기지와 네 서비스 유형 NPC가 Credits·자원·Trait 선택과 연결된다.

Status:
- Implementation
- Testing

Change:
- 함선 증원은 정식 Reinforcement 재고로 대체됐고, 상점 방어와 현장 서비스 생태계가 확장됐다.

Reason:
- Reason not documented

System-design impact:
- 거래 재고, 가격, 적대 전파, 방어망, 파괴 보상, Maintenance Bay, 현장 기지·NPC 서비스를 분리해 명세해야 한다.

### Core 발견과 활성화

Prototype document:
- Core는 선택적으로 발견해 2초 상호작용하면 즉시 보스를 시작하고 구역을 봉쇄한다.

Current project:
- 2초 상호작용은 유지된다.
- 지역 1·2에서 사용하는 Core.prefab은 Objective Signal 2개 전까지 숨김·상호작용 불가이며 직접 월드 발견도 비활성화한다.
- Objective Signal 3개는 보스 희귀 보상을 보장하고 4개는 보상 선택지를 하나 늘린다.
- 일반 Core 경로는 활성화 시 Core 보상을 드롭하지만, 지역 2 전용 경로는 Core 2개 지급을 보스 사망 시점으로 미루고 중복 지급을 차단한다.
- 지역 3은 Core를 생성하지 않으며, 맵에 미리 생성된 Phase Gatekeeper의 근접 트리거가 전투 도입을 시작한다.

Status:
- Implementation
- Testing

Change:
- 지역 1·2 Core는 탐사 신호로 위치를 해금하는 목표 허브로 바뀌었고, 지역 3은 Core 없이 전용 보스 조우로 분기한다.

Reason:
- Reason not documented

System-design impact:
- 지역 1·2의 Core 발견·공개·활성화와 지역 2의 보상 후지급 예외를 명세하고, 지역 3의 Core 없는 전투 진입은 별도 흐름으로 작성해야 한다.

### Region 1 Boss

Prototype document:
- Sector Administrator 한 보스를 HP 140, 구역 레이저·산탄·추적 충전탄, HP 30%의 Phase 2와 회전 레이저로 정의한다.

Current project:
- Boss.prefab과 BossPatternController가 Sector Administrator 전투를 구현한다.
- 직렬화 Phase 2 임계값은 현재 HP 50%이며 보호막·도입 연출·구역 레이저·산탄·충전 공격과 특수 회전 패턴을 가진다.
- 첫 격파는 Sector Stabilizer와 sector barrier Trait을 제공하고 지역 2를 해금한다.
- 재도전에서는 별도 Raider Commander 프리팹을 사용할 수 있다.

Status:
- Implementation
- Testing

Change:
- 지역 1 캠페인 보상과 재도전 보스가 추가됐고 Phase 전환 수치가 프로토타입과 달라졌다.

Reason:
- Reason not documented

System-design impact:
- 패턴 구조와 캠페인 보상을 유지하되 HP·Phase 임계값은 최종 확정 전 테스트 수치로 표시해야 한다.

### Region 2: Salvage Devourer

Prototype document:
- 추가 지역과 보스는 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Region2.asset은 Salvage Devourer, Matter Compressor, matter reconstructor Trait, Core 2개를 정의한다.
- Core.prefab의 region2BossPrefab은 PF_Boss_SalvageDevourer_FrigateTriad.prefab에 직렬화 연결돼 있다.
- Expedition 씬은 PF_Region2BossCorridorRuntime.prefab을 연결한다. 맵 생성기는 상단 Core와 전투 회랑을 예약하고, 전용 도입 연출이 함대 진입·편대 추종·세로 스크롤을 인계한다.
- 세 호위함은 개별 파츠 체력과 하나의 집계 EnemyHealth를 사용한다. 생존 수 3·2·1에 따라 일제사격, 조준사격, 경고 구역, 제한 유도탄, 벽 도탄, 레이저, 회전탄 패턴이 바뀐다.
- 마지막 치명타는 경고된 세로 돌진을 완료한 뒤 사망 처리로 넘어가며, Core 2개는 보스 사망 시 중복 방지 경로로 지급된다.

Status:
- Implementation
- Testing

Change:
- 캠페인 정의만 있던 상태에서 전용 3기 편대, 스크롤 회랑, 생존 수별 패턴과 사망·보상 흐름이 구현됐다.

Reason:
- Reason not documented

System-design impact:
- 현재 편대·회랑·패턴·최종 돌진·보상 구조를 Implementation으로 기록하되 수치와 플레이 감각은 Testing으로 남겨야 한다.

### Region 3: Phase Gatekeeper

Prototype document:
- 추가 지역과 보스는 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Region3.asset은 Phase Gatekeeper, Phase Navigation Lens, phase afterimage Trait, Core 2개를 정의한다.
- Expedition 씬은 PF_Boss_PhaseGatekeeper.prefab과 PF_Region3_PhaseReflectorPlate.prefab을 ExpeditionMapGenerator에 직렬화 연결한다.
- 지역 3 생성 경로는 Core·현장 기지·상점을 0개로 만들고 전용 보스와 반사판 8개를 배치한다. 플레이어가 근접 트리거에 들어오면 도입 연출과 전투가 시작된다.
- 보스는 은폐 상태에서 최대 3회 반사되는 레이저를 한 주기당 3회 발사한 뒤 현재 5초 동안 피해 가능 상태가 되며, 공격 사이에 은폐 이동한다.
- BossDummyController에는 지역 3 캠페인 정의와 Return Beacon·Wormhole이 연결돼 있다.
- 다만 Core가 없는 지역 3에서 BossCampaign_Region3.asset의 Core 2개를 지급하는 전용 호출 경로는 정적 검색으로 확인되지 않았다.

Status:
- Implementation
- Testing

Change:
- 캠페인 정의만 있던 상태에서 Core 없는 전용 반사 레이저 조우와 출구 흐름이 구현됐다. 구성된 Core 보상의 지급 경로는 아직 불명확하다.

Reason:
- Reason not documented

System-design impact:
- 은폐·반사 레이저·노출 피해 창·반사판 배치를 Implementation으로 기록하되 수치는 Testing으로 두고, Core 2개 지급 규칙은 연결 확인 전 확정하면 안 된다.

### Boss 보상

Prototype document:
- 보스 사망 시 귀환 장치와 Credits·Scrap·Core·XP를 즉시 제공한다.

Current project:
- 캠페인 정의는 Core 수량과 보장 Boss Trait을 설정하고, BossRewardExitCoordinator가 기본 3개 선택 보상을 먼저 제시한다.
- 지역 1 Core 보상은 Core 활성화 시 지급되며, 지역 2 Core 보상은 보스 사망 시 보장 지급과 중복 방지를 거친다.
- Core를 생성하지 않는 지역 3은 보장 Trait과 출구 흐름은 연결됐지만, 설정된 Core 2개 지급 호출은 확인되지 않았다.
- Objective Signal 3개는 희귀 보상을 보장하고 4개는 네 번째 선택지를 추가한다.
- 보상 선택이 끝난 뒤에만 Return Beacon과, 가능할 경우 Wormhole이 생성된다.
- XP 보상은 현재 권위 있는 경로에서 확인되지 않는다.

Status:
- Implementation
- Testing

Change:
- 즉시 일괄 보상이 선택형 보상과 탐사 성과 연동 구조로 바뀌었고, Core 지급 시점은 지역별로 분기한다.

Reason:
- Reason not documented

System-design impact:
- 캠페인 고정 보상, 지역별 Core 지급 시점, 선택형 런 보상, 신호 보너스, 출구 생성 순서를 구분하고 지역 3 Core 지급 공백을 미확정으로 표시해야 한다.

### Return과 심층 진행

Prototype document:
- 비전투 중 긴급 귀환은 영구 재화 20%를 잃고, 보스 후 안전 귀환은 손실이 없다.
- 보스 후 Wormhole을 선택하면 즉시 손실 없이 심층으로 이동한다.

Current project:
- 긴급 귀환은 전투·보스전·정지 조건을 검사하고 준비 동작을 거친다.
- 기본 보존 한도는 최대 화물 용량의 70%이며 Scrap·Core·Alloy를 비례 축소한 뒤 우선순위로 빈 용량을 채운다. 고정 20% 손실이 아니다.
- 사망은 Scrap과 Alloy 50%를 보존하고 Core는 모두 잃는다.
- 안전 귀환과 최종 승리는 영구 화물 전량을 확정한다. Credits와 Tuning Chips는 영구 저장되지 않는다.
- 지역 1 → 2 → 3은 Wormhole로 진행하지만 지역 3 뒤에는 즉시 최종 지역으로 이어지지 않는다.

Status:
- Implementation
- Testing

Change:
- 귀환 손실 규칙이 화물 용량 기반으로 바뀌고 사망·안전 귀환·최종 승리의 정산 규칙이 분리됐다.

Reason:
- Reason not documented

System-design impact:
- 귀환 유형별 허용 조건, 준비 취소, 자원별 보존, 지역 이동 시 계승 상태를 정확한 정산 표로 작성해야 한다.

### Settlement 성장

Prototype document:
- Hangar, Engine, Recovery Center, Weapon Lab을 각각 3단계까지 Scrap과 Core로 업그레이드한다.

Current project:
- PermanentProgress와 SettlementController는 기존 다단계 건물 업그레이드 필드를 Legacy로 취급한다.
- 현재 건물 진행은 캠페인 부품과 선행 복구에 의해 순차적으로 열리는 이진 복구 완료 상태다.
- 영구 성장의 실제 지출 축은 함선 해금, Ship Trait Tree의 Scrap·Core, 5종 Sector Technology의 Alloy다.
- Sector Technology는 각 3레벨과 2·3·5 Alloy 비용을 사용하지만 코드 명칭이 Prototype 수치임을 드러낸다.
- 명시적 BuildingDefinition 자산은 Hangar 하나만 확인되고 나머지는 fallback 정의에 의존한다.

Status:
- Implementation
- Testing

Change:
- 4개 건물의 재화 기반 3단계 성장에서 캠페인 복구 게이트와 별도 영구 트리·기술 성장으로 전환됐다.

Reason:
- 코드가 기존 다단계 건물 경로를 Legacy로 명시한다. 새 성장 구조의 설계 이유는 문서화되지 않았다.

System-design impact:
- 건물 복구, 함선 해금, 영구 Trait Tree, Sector Technology를 서로 다른 성장 축으로 재작성하고 시험 비용을 확정 값으로 취급하지 않아야 한다.

### Campaign 진행

Prototype document:
- 지역 1 보스 이후 더 깊은 해역으로 가는 장기 목표만 제시하며 완전한 캠페인 게이트는 범위 밖이다.

Current project:
- 지역 1 격파는 지역 2, 지역 2 격파는 지역 3을 해금한다.
- 지역 1~3 보스는 각각 Route Core 부품을 제공하고, 세 부품 수집 → 조립 → 활성화 → 정착지 방어 → 최종 출격 상태를 PermanentProgress가 보존한다.
- SettlementRouteCoreController가 이 흐름을 구현하지만 해당 스크립트 GUID는 현재 씬과 프리팹에 직렬화되어 있지 않다.

Status:
- Planned

Change:
- 다지역 보스 부품 기반 캠페인 구조가 추가됐지만 정착지에서의 완전한 실행 경로는 연결되지 않았다.

Reason:
- Reason not documented

System-design impact:
- 저장 상태 모델과 실제 플레이 가능 경로를 구분하고, Route Core 상호작용·정착지 방어 연결은 미구현 의존성으로 표시해야 한다.

### Tutorial

Prototype document:
- 별도 제작형 튜토리얼 흐름을 상세히 정의하지 않는다.

Current project:
- TutorialFlowController는 이동·조준·발사·Dash·Radar·Map·경로 핑·수확·화물·Reinforcement·전투·Unknown Signal·Pixel Curse·Emergency Return까지 28단계를 관리한다.
- 40×40 유한 튜토리얼 맵에서 운영 시스템을 재사용하고, 실제 긴급 귀환 후 첫 정착지 대화를 예약한다.

Status:
- Implementation
- Testing

Change:
- 핵심 시스템과 서사 도입을 묶은 전용 온보딩 흐름이 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 튜토리얼 단계와 각 운영 시스템의 교육 목표, 완료 조건, 실패·누락 fallback을 별도 명세해야 한다.

### Story

Prototype document:
- 붕괴한 물류망과 Scrapper 조종사라는 세계관 및 장기 목표를 제시하지만 구현 대화 흐름은 제한적이다.

Current project:
- Dialogue System 데이터베이스에는 튜토리얼, 구조 후 대화, 첫 정착지, 네 NPC 서비스, 일반 보스 전·후 통신 등 10개 Conversation이 있다.
- 튜토리얼과 첫 정착지·NPC 서비스는 사용 경로가 확인된다.
- 일반 보스 전·후 통신은 현재 씬·프리팹 사용처가 확인되지 않으며 지역별 보스 서사, Route Core 연출, 최종 보스·엔딩 대화는 없다.

Status:
- Planned
- Implementation

Change:
- 세계관 개요에서 실제 대화 기반 튜토리얼·서비스 서사로 확장됐지만 캠페인 후반 서사는 부분 구현 상태다.

Reason:
- AGENTS.md가 Dialogue System을 권위 있는 서사 대화 체계로 지정한다.

System-design impact:
- 구현된 Conversation과 계획된 캠페인 비트를 구분하고, 지역별 보스·부품·최종 출격·엔딩의 누락을 명시해야 한다.

### Final Boss와 Ending

Prototype document:
- 최종 보스와 엔딩은 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Final.asset은 Null Dispatcher와 FinalVictory 상태를 정의한다.
- CoreObject와 BossDummyController에는 최종 보스전·정착지 지원 단계·최종 승리 처리 코드가 있다.
- Core.prefab에는 finalBossPrefab이 연결되지 않아 정상 경로로 최종 보스전을 시작할 수 없다.
- AGENTS.md에는 거대 구형 외피, 패턴 파손, 보라색 Pixel Curse 내부 노출의 3단계 시각 방향이 있으나 실제 전용 보스 자산과 엔딩 시퀀스는 확인되지 않는다.

Status:
- Planned
- Deferred

Change:
- 최종 진행 상태와 시각 방향은 생겼지만 전용 조우와 엔딩 콘텐츠는 아직 플레이 가능한 완성 경로가 아니다.

Reason:
- 프로토타입 문서가 최종 보스·장기 콘텐츠를 범위 밖으로 두었다. 현재 미연결 상태의 추가 이유는 문서화되지 않았다.

System-design impact:
- 최종 보스는 확정된 시각 원칙, 존재하는 진행 훅, 미정 전투·보상·엔딩을 구분해 계획 장으로만 작성해야 한다.

## 감사 중 확인된 근거 공백

- Core를 생성하지 않는 지역 3에서 BossCampaign_Region3.asset의 Core 2개를 지급하는 호출 경로
- SettlementRouteCoreController가 연결된 정착지 오브젝트 또는 프리팹
- 실제 정착지 방어 콘텐츠와 그 완료 이벤트 연결
- Core.prefab의 finalBossPrefab 연결 및 Null Dispatcher 전용 전투 자산
- 지역별 보스 서사, 최종 출격, 엔딩 Conversation 또는 연출 자산
- 현재 테스트 수치를 최종 설계로 승인한 별도 근거 문서

이 항목들은 저장 상태나 코드 훅이 있다는 이유만으로 구현 완료 또는 확정 설계로 승격하면 안 된다.

## 2026-08-31 — Dialogue and Localization Phase 2A

Status:
- Implementation
- Static Verification

Change:
- Pixel Crushers Dialogue System remains the authoritative conversation runtime.
- Added a project-owned Korean-first localization catalog and service around the
  installed runtime instead of creating a second dialogue framework.
- Localization source is authored as UTF-8 CSV and imported into generated Unity
  runtime assets. Runtime XLSX/CSV loading is prohibited.
- Korean is mandatory source text and the fallback for empty `en`, `ja`, `zh-Hans`,
  and `zh-Hant` cells.
- Unity Localization was not added because the installed dialogue system already
  supplies the required language API and a second table runtime would duplicate
  ownership at the current project scale.
- Gameplay dialogue actions, conditions, Pixel Crushers conversation migration, and
  the RescueContact vertical slice are deferred to Phase 2B.

Reason:
- The project needs stable localization keys, deterministic spreadsheet authoring,
  device-local language selection, and editor validation before dialogue content can
  safely request gameplay changes.

System-design impact:
- `Docs/Design/SYSTEM_DESIGN.md` is now the authoritative boundary for localization,
  dialogue runtime ownership, authoring/import rules, and Phase 2B deferrals.

## 2026-09-01 — RescueContact Dialogue Phase 2B Vertical Slice

Status:
- Implementation
- Static Verification
- Unity Dialogue Database installation pending

Change:
- Added one project-owned Pixel Crushers bridge with typed, fail-closed condition
  and gameplay-action registries for `NPC_RescueContact_Service` only.
- RescueContact accept requests the existing `FieldNpcObjective` service authority;
  decline has no gameplay action and no automatic post-conversation service call.
- Added per-conversation duplicate-action protection without taking ownership of
  Core Signal, objectives, run state, saving, or NPC completion.
- Added six Korean-first localization keys for the speaker, two lines, two choices,
  and unavailable feedback. Optional language cells remain empty and fall back to
  Korean.
- Added a validated Editor installer that replaces only the existing RescueContact
  conversation entries. Direct YAML rewriting of the Dialogue Database was rejected.

Reason:
- A single low-risk interaction must prove localized choices, read-only conditions,
  typed action dispatch, and existing gameplay ownership before broader migration.

System-design impact:
- Pixel Crushers remains the only dialogue runtime. The bridge is an adapter, not a
  gameplay service, and new actions require explicit typed registration and tests.

## 2026-09-01 — Dialogue Lifecycle and Settlement Transition Stability (Phase 2B.1)

Status:
- Implementation
- Static Verification
- Play Mode Verification pending

Change:
- Made the persistent Boot-owned `PF_DialogueManager` the single application-lifetime
  dialogue root and added early duplicate-Boot pruning before incoming services awake.
- Kept localization-service duplicate detection as a defensive whole-root cleanup,
  rather than silently disabling one component on a live duplicate manager.
- Made Settlement dialogue modal over ship selection and expedition controls using
  Pixel Crushers conversation lifecycle events and the existing Settlement input group.
- Added an authoritative expedition transition guard and Korean-first localized blocked
  feedback at `system.settlement.expedition.dialogue_active`.

Reason:
- Reloading Boot from Main Menu recreated its persistent manager prefab, and Settlement
  UI/launch authority did not consider an active mandatory conversation.

System-design impact:
- Phase 2C migration remains gated on repeated MainMenu/Boot/Settlement lifecycle and
  modal-input Play Mode verification.

## 2026-09-01 — Tutorial Story and Damaged Access-Key Quest Foundation (Phase 2C)

Status:
- Implementation
- Static Verification
- Dialogue Database installation and Play Mode Verification pending

Change:
- Defined Korean-first content and deterministic Pixel Crushers graphs for the
  tutorial opening, unknown access-key contact, post-Curse rescue, and first
  Settlement analysis.
- Added a terminal completion marker so interrupted conversations cannot advance a
  tutorial checkpoint or start persistent story state.
- Extended the existing typed condition/action bridge only for read-only
  `MAIN_DAMAGED_ACCESS_KEY` states and an exact-once quest-start request.
- Represented the main quest as a saved started flag plus a read-only projection of
  existing `BossStoryPart` and `RouteCoreState`; no parallel quest or reward store was
  introduced.
- Extended the Phase 2B.1 launch guard so the pending mandatory Settlement story
  blocks expedition launch even before its conversation-start event.

Reason:
- The first story migration must prove interruption safety, existing gameplay
  authority, persistent idempotency, and state-aware repeat dialogue without
  allowing Pixel Crushers entries to mutate tutorial, reward, or save state.

System-design impact:
- Pixel Crushers remains the sole conversation runtime. `TutorialFlowController`
  owns tutorial and return transitions, `PermanentProgress` owns saved campaign
  identity, and `SettlementController` owns quest-start save/notification behavior.
- Phase 2D retains the main-quest HUD binding, live boss-part progression checks,
  and the next NPC/story migration after Unity lifecycle validation.

## 2026-09-01 — Staged Tutorial and Access-Key Recovery (Phase 2C.1)

Status:
- Implementation
- Static Verification
- Dialogue Database installation and Play Mode Verification pending

Change:
- Split the four tutorial instructions into deterministic one-line Pixel Crushers
  conversations at Move, Radar, supply-container, and detected ancient-signal stages.
  Only natural terminal completion records the matching guidance checkpoint.
- Kept the first Settlement story as the exact-once quest-start boundary. Scene load
  alone still cannot start `MAIN_DAMAGED_ACCESS_KEY`.
- Changed 3/3 progression to `ReadyToRestore`. Explicit confirmation through the
  existing Settlement Recovery surface now restores the access key and saves once.
- Derived Pixel Curse level from persistent Curse ownership plus distinct campaign
  boss parts (`0`, then `1..4`) without adding duplicate saved state.
- Added Korean-first restoration and Curse-level notification keys and expanded the
  deterministic installer from four to seven story graphs.

Reason:
- Story guidance must coincide with the gameplay situation it explains, while quest
  completion and Curse progression must remain projections/actions of existing
  campaign and Settlement authorities rather than dialogue-owned mutations.

System-design impact:
- Boss rewards still own distinct `BossStoryPart` grants. `PermanentProgress` owns
  derived quest/Curse state, `SettlementController` owns the explicit recovery/save
  transaction, and Pixel Crushers remains presentation and lifecycle only.

## 2026-09-02 — High-Value Wreck Focus and Optional Route (Phase 2C.2)

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- High-value wreck discovery now starts an owner-scoped camera/input presentation
  before `TUTORIAL_OperatorAncientSignal`; the conversation cannot start until the
  existing camera finishes its target blend.
- Focus and player-return blends use the camera's unscaled cinematic path. Natural
  completion returns to the player before advancing; interruption and lifecycle
  cleanup release ownership without completing the checkpoint.
- The supply-box route waypoint is optional. Route placement and physical approach
  converge on the same travel/destruction sequence, while `HarvestObjectHealth.Died`
  remains the authoritative completion event.

Reason:
- Radar discovery can identify an offscreen wreck, so dialogue without camera context
  obscures what the Operator is describing. Route planning is useful navigation but
  should not be a mandatory resource-progression action.

System-design impact:
- `TutorialFlowController` remains the tutorial state owner, `GungeonStyleCamera2D`
  provides narrowly owner-scoped cinematic focus, and existing source-aware player
  input locks prevent control leaks. No new cinematic, route, or resource authority
  was introduced.

## 2026-09-02 — Radar Toggle, Instant Scan, and Shared Target Focus (Phase 2C.3)

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Changed Q from hold/release scanning to the sole Radar mode toggle. Mouse 4 now
  requests one immediate scan on press only while that mode is active; the existing
  cooldown and input locks remain authoritative.
- Retired Radar-only hold timing, release/cancel state, charge sounds/effects, and
  charge-gauge coupling without changing shared weapon charging UI.
- Made Radar activation and successful supply-target detection separate tutorial
  signals. Q alone cannot complete the scan objective.
- Generalized the Phase 2C.2 owner-scoped target presentation so the discovered
  supply container and high-value wreck share one unscaled camera/input pipeline.
- Shortened the four tutorial lines, resolved current binding displays at runtime,
  and made manual newlines in tutorial localization a validation error.

Reason:
- The previous Q charge and release semantics conflicted with the intended persistent
  Radar mode, while the supply dialogue lacked the same visual context as the wreck.
  Long authored lines also produced avoidable low-resolution dialogue wrapping risk.

System-design impact:
- `PlayerRadarScanner` remains the single Radar state/scan authority;
  `TutorialFlowController` observes its events and owns only tutorial sequencing.
  Pixel Crushers remains the dialogue runtime, TMP remains the wrapping authority,
  and route placement remains optional.

## 2026-09-02 — Korean-Safe Dialogue Reveal and Operator Text Voice (Phase 2C.4)

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Replaced the project communication subtitle's vendor wrapper component with a
  project-owned subclass of the same Pixel Crushers TMP typewriter authority.
- Added post-resolution TMP-supported U+2060 word joiners for whitespace-delimited
  tokens and non-breaking Input System binding display strings; generated formatting is never
  stored in localization source.
- Kept full-text TMP layout and `maxVisibleCharacters` reveal, with configurable
  unscaled glyph and punctuation timing and the existing two-press continue adapter.
- Added one temporary Operator text-voice profile using the existing short talk clip,
  one reusable 2D source, UI mixer routing, two-glyph cadence, and bounded pitch.
- Extended Editor validation with actual reference-prefab TMP measurement for the
  two-line 480x270 target, oversized tokens, unresolved bindings, and rich-text balance.

Reason:
- TMP's ordinary Korean wrapping may split a whitespace token between syllables, and
  progressively changing text can destabilize layout. Presentation-time token
  protection preserves clean authoring while stable mesh reveal and sparse blips
  improve low-resolution readability without a second dialogue or audio framework.

System-design impact:
- Pixel Crushers still owns subtitle, typewriter, pause, and continue lifecycles.
  Localization remains Korean-first CSV/catalog data, while the project-owned layer
  owns only resolved-text wrapping policy and the initial Operator voice profile.

## 2026-09-02 — Typed Cinematic Communication Presentation (Phase 2C.5)

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Added stable-ID presentation policy for compact guidance, externally focused
  context dialogue, and cinematic communication without adding dialogue actions or
  a second conversation UI.
- Extended `PF_CommunicationDialogueUI` with reusable dim/top-bar/accent presentation,
  a 480x270 lower panel layout, and a small selectable continuation indicator.
- Added Operator, Curse, Settlement, and neutral actor themes. The existing subtitle
  AudioSource now selects the three existing Talk clips by stable actor identity.
- Added unscaled overlay/panel timing and one idempotent cleanup path for completion,
  interruption, scene unload, disable, and destruction.

Reason:
- One full-screen treatment made short gameplay guidance unnecessarily intrusive,
  while major Curse/Settlement transmissions lacked hierarchy and actor identity.
  Stable typed profiles provide cinematic emphasis without coupling gameplay state to
  localized text or Dialogue Database scripts.

System-design impact:
- Pixel Crushers remains the sole conversation, subtitle, typewriter, continue,
  pause, and completion authority. Tutorial camera/input ownership remains outside
  the UI; the adapter may own only its `ExpeditionHUD` cinematic request and visual
  transition state.

## 2026-09-02 — QA Stabilization Pass 1

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Added deterministic, bounded, exact-once placement for post-boss Return Beacon and
  next-region exits without changing safe-return or progression ownership.
- Attached Region-2 damaging lasers to the moving boss frame, reset pooled hazard
  geometry explicitly, and aimed homing projectiles at active damageable colliders.
- Smoothed Region-2 entrance framing through the existing camera authority and made
  boss presentation timing unscaled; the existing Region-1 offscreen entrance remains
  authoritative.
- Kept Radar open after successful scans, closed it from combat activity or manual Q,
  faded only its background, and used a cyan reused scan pulse.
- Added exact-once tutorial unknown-objective Radar presentation and stable-key
  open-versus-scan-only guidance.
- Added runtime-safe Tuning Chip currency-counter fallback wiring and hid the legacy
  Settlement Use control until the selected facility is unlocked.
- Wired the existing Shop purchase-success clip into its existing Sound Event entry;
  playback remains after confirmed transactions only.

Reason:
- The playthrough exposed ownership mismatches: reward exits trusted raw moving boss
  coordinates, Frigate hazards detached from their scrolling root, homing used a poor
  composite pivot, and Radar/tutorial/UI surfaces did not consistently reflect their
  authoritative state.

System-design impact:
- Existing boss, camera, projectile, Radar, tutorial, wallet, Settlement, localization,
  pooling, and shop-audio authorities remain in place. No new manager, camera system,
  targeting framework, objective authority, dialogue runtime, or per-frame allocation
  path was introduced.

## 2026-09-03 — Tutorial QA Presentation Pass 2

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Delayed the opening Operator conversation until the existing player movement-start
  event, without bypassing the full movement objective or natural dialogue completion.
- Replaced the inherited yellow/olive Radar shockwave presentation with one bounded,
  pooled, unscaled cyan pulse while preserving Radar persistence and marker colors.
- Replaced player-facing `???` copy with Korean-first unknown-signal and Purple Core
  localization keys; the unresolved `?` Radar marker remains unchanged.
- Added an unscaled Purple Core fade/scale reveal with disabled interaction during the
  reveal and visual-root-only idle motion afterward.
- Added a reusable Purple Core-to-player transfer, player impact, and restrained dark
  flash before the existing persistent Pixel Curse acquisition changes the ship visual.

Reason:
- The recorded tutorial showed scene-entry dialogue before movement, an overlong olive
  scan flash, raw mystery placeholder copy, a one-frame Core appearance, and a blackout
  that hid the causal connection between the Core and cursed ship.

System-design impact:
- Existing tutorial, Pixel Crushers, Radar, DOTween, pause/input, Trait acquisition,
  localization, and pooled-VFX authorities remain in place. No new tutorial, dialogue,
  camera, VFX, save, or Curse ownership system was introduced.

## 2026-09-03 — Tutorial QA Presentation Pass 3

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Removed Map/route ownership from supply-container damage and destruction completion;
  the existing death event now converges optional and no-map paths exactly once.
- Preserved Radar visibility during tutorial camera focus and ordinary combat while
  retaining manual close, lifecycle close, and boss-owned suppression.
- Moved the authored signal relay to (-8.9, 15.33), kept it visible after its one-shot
  interaction, and added one unscaled purple pulse on its existing renderer.
- Replaced the detached travelling Curse transfer with a stationary Purple Core
  expansion/fade before player impact and authoritative persistent Trait acquisition.
- Guarded tutorial shutdown against Unity-destroyed player visual ownership.

Reason:
- The map tutorial accidentally gated supply damage, camera/combat events owned Radar
  visibility they did not author, and scene teardown could invoke a destroyed visual
  controller. The detached transfer also weakened the spatial relationship between the
  Purple Core and the Curse event.

System-design impact:
- Existing tutorial steps, harvest death/reward, Radar, camera/input, DOTween, and Trait
  authorities remain unchanged; this pass removes invalid coupling and reuses existing
  presentation objects without a new manager or runtime system.

## 2026-09-03 - Tutorial QA Cinematic Presentation Pass

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Separated target discovery from camera ownership: supply and high-value guidance now
  waits for 0.15 seconds inside a safe gameplay-camera viewport before focusing.
- Reused the existing Purple ring prefab for the relay, Core reveal, and visible
  Core-to-player transfer with unscaled DOTween and pool-safe reset.
- Added an owner-scoped Core cinematic using the existing letterbox, camera framing,
  input/pause locks, and `ScreenFader`; world/background state changes only after full
  black coverage.
- Replaced serialized `???` fallback copy with readable unknown-signal/Core Korean text.

Reason:
- Recorded play showed offscreen camera grabs, a static/popping Core, and a blackout
  that concealed the causal energy transfer. Safe-viewport dwell and explicit visual
  ordering preserve player orientation while making the Curse event readable.

System-design impact:
- Pixel Crushers, tutorial checkpoints, Trait acquisition, camera, input, pause, fade,
  and pooling remain authoritative. No new cutscene, dialogue, VFX, or progression
  manager was introduced.

## 2026-09-03 — Tutorial Relay Takeover and Purple Core Interaction

Status:
- Implementation
- Static Verification
- EditMode and Play Mode Verification pending

Change:
- Replaced direct relay-step advancement with deterministic real-Operator cutoff and
  hidden-identity `FakeOperator` takeover conversations.
- Removed the post-analysis full-Map search area/Map `?`; retained one Radar-only `?`
  until Purple Core reveal.
- Added a typed player-projectile receiver with bounded pellet contribution and one
  shared `F`/damage acquisition latch; the Core remains non-destructible.
- Made the actual Core sprite travel into the ship while the pooled purple effect stays
  an anchored support/ship-origin corruption effect.

Reason:
- Immediate relay progression did not establish the narrative deception, Map and Radar
  duplicated tracking information, attacks had no meaningful Core response, and the
  travelling ring read as the collected object while the Core stayed behind.

System-design impact:
- Existing tutorial steps, Pixel Crushers, localization, projectile faction data,
  camera/input ownership, pooled Purple effect, ScreenFader, and persistent Trait
  acquisition remain authoritative. No enemy health, quest, or cutscene framework was
  added.

## 2026-09-03 — Boot Main Menu UI Authoring Migration Pass 1

Status:
- Implementation
- Static Verification
- Unity installation and Play Mode verification pending

Change:
- Replaced `MainMenuController.Awake()` visual construction with a serialized
  `BootMainMenuView` authority.
- Added idempotent Editor Install/Validate commands that author the Boot Canvas,
  background, menu, settings, and confirmation hierarchy without auto-saving.
- Serialized the Boot `SharedOptionsMenuUI` and animated background references so
  their visuals are authored once while runtime retains behavior only.

Reason:
- Runtime-created main-menu objects could not be inspected or tuned reliably in the
  Hierarchy and Inspector, and allowed Scene and runtime layout authority to diverge.

System-design impact:
- The Boot scene owns static UI layout and visuals. Existing save, input, settings,
  audio, ScreenFader, EventSystem, and scene-flow owners remain unchanged.

## 2026-09-03 — Boot Main Menu Partial-Install Repair

Status:
- Implementation / static verification
- Unity installer rerun pending

Change:
- Made background authoring repair each missing star, asteroid, Curse passer, component,
  and typed view binding in dependency order.
- Added serialized star references and typed background validation while preserving
  already-bound renamed objects and user-authored visual values.
- Missing runtime background data now disables only the animated presentation instead
  of disabling the complete Boot menu.

Reason:
- The initial installer treated its aggregate authored flag as sufficient and did not
  serialize stars, so a partially installed `Background` could skip repair and leave
  `BootMainMenuView.spaceBackground` unset.

## Tutorial Guidance UI Authoring — Pass 1 (2026-09-06)

Status: Implemented; manual scene installation and Unity verification pending.

- Added Tutorial-only installer/validator menus for the existing TutorialPrompt and
  previously runtime-created OperationStatus. New objects use the former Tutorial
  appearance; existing visual values and typed renamed references remain intact.
- Repair uses SerializedObject/SerializedProperty, scene-scoped discovery, Undo,
  temporary inactivity, and direct destination-scene creation. No automatic save.
  Validation accumulates missing binding/font, duplicate, hierarchy, wrong-scene,
  cross-scene, CanvasScaler, and fallback-flag errors.
- Installed Tutorial disables only its `createOperationPresentationIfMissing` flag.
  Missing authored operation references produce one repair warning and skip the
  briefing visual. Valid bindings bypass construction. Expedition's fallback and
  serialized scene configuration are retained; no shared helper can yet be deleted.
- TutorialFlowController's 28 stages, TutorialPromptUI/input/font behavior,
  ExpeditionHUD briefing timing, Pixel Crushers dialogue, and gameplay ownership
  remain unchanged. All broader UI migrations remain out of scope.
- Added isolated PreviewScene EditMode coverage for fresh/partial authoring,
  typed/renamed adoption, preservation, idempotency, binding validation, scene
  ownership, and runtime briefing boundaries. Legacy Expedition fallback availability
  is checked statically; its runtime behavior still needs ordinary-scene Play Mode.
- Delivery changes code/tests/docs only. Install, validate, manually save/reopen,
  run Test Runner regression suites, and verify Tutorial plus Expedition in Play Mode.

## 2026-09-06 — Tutorial resource strip authoring (limited slice)

- Proven identifiers are `CurrencyType.TuningChips` and `CurrencyType.StabilizedAlloy`.
  Both read the current run wallet; Alloy retains cargo and run-result settlement
  rules. Existing wallet events and five HUD serialized fields already provide
  registration and refresh. Missing additional references previously caused runtime
  ScrapCounter clones; no separate missing resource authority was found.
- Added resource-specific Install/Tutorial > Validate Resource HUD menus. Adopt the
  copied ResourceRoot and valid renamed typed rows; repair missing components,
  nested bindings and registrations with Undo, explicit scene ownership, inactive
  binding repair and active-state restoration. Do not save or restyle existing rows.
- Tutorial's inspected Credits/Scrap slots both point to ScrapCounter. The tool
  reports this ambiguous duplicate instead of guessing: manually assign the existing
  CreditsCounter to Credits Counter before installation. Missing base strips are
  not recreated by this limited installer.
- Newly authored Alloy/Chip rows reuse ScrapCounter structure, project pickup icons,
  existing resource tint and Korean-safe font conventions. Sprite, color, font,
  text and RectTransform settings remain editable. Existing optional labels remain;
  no tooltip/localization framework or content changes are introduced.
- Successful Tutorial installation disables one aggregate resource-construction
  fallback flag. Runtime still refreshes current balances, subscribes once, hides
  zero rows and packs the existing display order; missing authored bindings warn
  once per resource and skip only that counter. Expedition keeps its runtime fallback.
- Added focused PreviewScene tests for adoption, missing row/binding repair, renamed
  references, manual preservation, ambiguity/cross-scene diagnostics, initial and
  changing balances, subscription cleanup/re-enable, and both runtime boundaries.
  Static .NET compilation passed; Unity Test Runner/Play Mode execution is pending.
- No scene/prefab/vendor/localization/font asset was edited by this pass. Manually
  install, inspect, validate, save/reopen Tutorial, run the tests and check Tutorial
  and Expedition at 480x270. Guidance, dialogue, pause, Radar, Map, saves, pickups,
  the 28-step flow and Settlement ownership remain unchanged; Settlement is deferred.

## 2026-09-06 — Settlement persistent-resource strip authoring

- Narrow first Settlement slice: adopt `Canvas/resourcesUI`, author/adopt its
  ResourceStrip and three fixed ScrapParts/CoreShards/StabilizedAlloy cells.
  Navigation, previews, facilities, traits, technology and settings remain deferred.
- Added Install/Settlement > Validate Resource HUD menus. Explicit loaded-scene
  ownership, Play Mode refusal, typed renamed-reference adoption, partial repair,
  duplicate refusal, SerializedObject assignments, Undo, safe binding inactivity,
  rollback, dirty marking and manual saving follow the prior focused installers.
- Added plain serialized cell binding groups to SettlementHUD. Existing builder
  appearance initializes only new objects; existing manual art, typography,
  anchors/offsets and active states remain intact. No LayoutGroup or cell scripts
  are added. Legacy source Graphics are individually disabled, not deleted or
  deactivated along with their shared parent.
- A single default-enabled compatibility flag retains uninstalled behavior; only
  successful authored installation disables it. Valid authored bindings bypass
  BuildResourceStrip/CreateResourceChip. Missing authored references warn once per
  HUD and skip only affected cells. Compatibility construction is not retired.
- PermanentProgress remains balance authority; all three zero balances stay
  visible. Existing controller event updates are retained, with immediate HUD
  re-enable refresh and cleanup against its actual subscribed publisher.
- Added focused PreviewScene regression coverage and a separate test-owned
  temporary additive-scene save/reload test. Static compilation passed; Unity
  tests, manual installation/save/reopen and Play Mode are not claimed as executed.
- No user scene, prefab, vendor, localization, Dialogue Database, generated project
  or protected TMP asset was edited by this delivery. No gameplay transactions,
  progression changes or saves were performed. Existing unrelated mapping and
  embedded-script risks remain later work.

## 2026-09-06 - Settlement main-navigation authoring

- Added Install/Settlement > Validate Navigation UI menus for the explicit loaded
  Settlement scene, with scene-aware creation, typed/renamed adoption, partial
  binding repair, Undo/rollback, inactive binding setup and manual saving only.
- Serialized the existing five primary navigation views, root/header/background,
  modal input CanvasGroup and six existing action/back labels. Kept the routes to
  Hangar, Settlement Restoration, Ship Reinforcement, Additional Traits and Settings.
- Valid authored navigation bypasses compatibility construction/base restyling.
  One default-enabled fallback policy remains for uninstalled owners; successful
  installation disables it. Missing authored bindings warn once without clones.
- Selection feedback uses authored inactive color baselines and Inspector-selected
  colors, with no transform/scale drift. Existing button layout/links/styles and
  unrelated callbacks are preserved. Owned runtime listeners and publisher
  subscriptions are duplicate-safe and clean up their actual sources.
- Moved SettlementPrimaryNavigationPointer into its matching script/meta without
  changing the controller GUID. No unknown missing scripts are removed; the
  inactive Canvas/Temp_ClearButton missing GUID remains a separate issue.
- Narrowly guarded HUD ship-action and trait-unlock typography defaults; their
  text updates/actions and all panel initialization/content remain unchanged.
- Added focused test-owned PreviewScene, routing/subscription, authored/fallback,
  Undo/rollback, identity and temporary save/reload regression coverage. Tests
  compile; Unity Test Runner, installation, reload and Play Mode remain unexecuted.
- Resource HUD migration, transactions, saves, Pixel Crushers, launch guards,
  input/pause/camera and transitions are preserved. No production scenes/prefabs,
  vendor/localization/Dialogue Database/generated project/protected TMP assets
  were modified. Broader Settlement panels remain later work.

## 2026-09-06 - Settlement navigation usability and explicit layout repair

- Verified saved OptionButton was inactive/28x22, while two primary captions kept
  20pt non-autosized typography. Existing cyan selection Outline followed focus,
  not the open panel; separated focus Outline from active-tab strip/colors.
- Extended Settlement > Install Navigation UI to restore Settings, hide only the
  two authored redundant Back controls, repair boundary/default links and retain
  compatibility references. Installed Ship Reinforcement skips its runtime Back
  construction; sector content/cards and transactions are otherwise unchanged.
- Added Settlement > Repair Navigation Layout: explicit Undo-supported 62x24 button
  layout and 7pt, autosize 6-7, single-line padded captions; no font/fallback/content
  replacement. Normal installation preserves subsequent styling edits.
- Replaced raw W/S polling with focused EventSystem move callbacks and existing
  repeat timing. Clamp/skip behavior, primary-only Space consumption, standard
  Enter/controller Submit, and panel/sidebar boundary navigation prevent parallel
  movement or action activation behind another input context.
- Existing Settings authority now announces opening before focus changes, restores
  prior content focus on close and consumes Cancel once per frame. Rebinding,
  dropdown and screen-confirmation cancellation stay inside their modal context.
- Expanded navigation tests, including TMP bounds execution for Unity Test Runner.
  Static compilation is verified; Unity tests/rendering/Play Mode/reload are not
  claimed as executed. No production scene, prefab, Input Actions asset, protected
  font, localization, vendor, save or Dialogue Database edits were made.

### 2026-09-06 - Ship Reinforcement authored panel/template and grouped UI menus

- Added Settlement > Install/Validate Ship Reinforcement UI. The focused installer
  authors static panel/detail/action presentation and an inactive editable card
  template, preserving typed/renamed references and manual styling. Explicit scene
  ownership, Undo, scoped rollback and accumulated diagnostics; never auto-saves
  or invokes gameplay/settings lifecycle. Navigation installation is a prerequisite.
- Serialized existing panel fields and moved SettlementSectorTechnologyEntrySelection
  to a matching script/meta, adding its nested template bindings. The five catalog
  IDs, ordering and glyph icons remain catalog-owned; no prefab or scrolling
  structure existed to adopt. No redundant Ship Reinforcement Back is introduced.
- Authored runtime clones only the template, preserves base layout/typography and
  updates data/selection. Actual controller-publisher subscriptions refresh on
  initialization/re-enable; owned action binding is idempotent. Locked upgrade
  focus returns to the selected card; card Left retains a route to the sidebar.
- One aggregate compatibility policy stays enabled for uninstalled configurations;
  installation validates before disabling it. Partial authored failures warn once,
  never create replacement visuals, and leave other panels/input operational.
- Grouped existing menus beneath UI > Boot, Tutorial and Settlement; retained all
  Install/Repair/Validate operations, without Install All. Updated diagnostic and
  documentation/test references. No builders deleted: shared Options and Expedition
  fallbacks still have consumers, and Settlement retirement needs manual verification.
- Added fourteen focused tests including temporary test-owned save/reload, binding
  identity, catalog mapping, one-click upgrades without SaveManager, styles, focus,
  fallback, lifecycle and menu paths. Compiled only; Unity execution/rendering not
  claimed. Existing navigation/resource/settings/dialogue/launch coverage retained.

## 2026-09-06 — Settlement Restoration authoring

- Added Settlement > Install Restoration UI / Validate Restoration UI. Adopt the
  existing Repair_HUD and role-scoped Bg/Cost/preview containers; repair missing
  references/objects only, with scene ownership, Undo, rollback and manual saving.
- Retained existing facility order, art, restoration-state/requirements/results
  meanings, explicit access-key confirmation, authority and transactions.
- Removed Restoration runtime typography/aspect resets and obsolete hidden-cost
  icon layout helpers. Indicator feedback derives from authored scale/tint.
  Existing serialized legacy cost fields remain; only the Images are hidden.
- Kept Restoration Back inactive and excluded from EventSystem focus; unavailable
  action focus recovers locally without changing sidebar or Settings/dialogue rules.
- Preserved existing owned listener deduplication, actual-publisher cleanup and
  re-enable refresh. Incomplete bindings warn once and block only Restoration
  presentation/action; no runtime replacement or extra fallback flag.
- Added focused PreviewScene and inactive temporary-scene regression fixtures.
  Static runtime/Editor/test compilation passed; Unity tests, temporary reload
  tests and visual checks were not executed. Production assets were not edited.

## 2026-09-07 — Additional Traits presentation authoring

- Added Settlement > Install Additional Traits UI and Validate Additional Traits UI.
  Adopt existing detail/category/scroll structures and clone the existing node
  prefab into an inactive editable scene-local template; no production asset saves.
- Added explicit container/template/sidebar bindings to ShipTraitTreePanel.
  Dynamic catalog population remains definition-driven, reuses nodes on re-enable,
  binds inactive clones before activation, and retains uninstalled prefab fallback.
- Authored mode bypasses typography/aspect/cost-position defaults. Tab/node/branch
  feedback uses authored baselines. EventSystem focus navigation remains separate
  from selection and respects sidebar, Settings, dialogue and pause contexts.
- Balanced actual-publisher/action listeners and matching persistent callbacks;
  existing purchase, activation, prerequisites, maximum levels, currencies and
  persistence authority are unchanged.
- Removed OnValidate's definition-copy mutation; runtime definition synchronization
  remains. No compatibility builder deletion: authored scene installation and
  reload/Play Mode verification are still manual gates.
- Found existing statusText/messageText alias on temp1. Installer explicitly rejects
  it rather than silently replacing either reference. Keep statusText and clear or
  separately assign messageText in the Inspector before installation.
- Focused test fixtures cover installation/repair/Undo/rollback, identity, styles,
  mappings/filtering, transactions/activation, focus, lifecycle and compatibility.
  Compiled only; Unity test execution and Korean 480x270 visual verification pending.

## 2026-09-07 — Verified static UI runtime retirement

- Removed Settlement resource-strip construction/legacy text fallback, navigation
  construction/default styling, and Ship Reinforcement static panel/card-child
  builders. Saved local bindings, nested assets and sole production GUID consumers
  were inspected before deletion; Editor installers do not call these builders.
- Preserved serialized migration flags, Editor default fields and public technology
  Initialize arguments. Flags are now inert at runtime but still support existing
  saved data, installer assignments, validators and Undo fixtures.
- Kept runtime balances, dynamic technology template cloning, selection/focus,
  audio, modal restrictions, transactions, listeners and publisher cleanup.
- Missing bindings fail locally with once-only property/path/scene/menu diagnostics;
  no replacement UI. Replaced three legacy-construction tests with authored-only
  failure expectations; retained existing state/style/lifecycle/transaction tests.
- Additional Traits retirement deferred: saved authored containers/template remain
  null and forced rebuilding is enabled. The earlier status/message alias is cleared
  in the saved scene, but this does not constitute authored installation.
- Retained Expedition operation/resources, shared Pause/Settings construction,
  Boot Editor-only authoring helpers and unmigrated ship-preview presentation.
  No MonoBehaviour file, GUID, production asset or gameplay authority was removed.
- Runtime/Editor static compilation only; Unity tests, production validators,
  reload, Play Mode and visual verification remain pending.

## 2026-09-07 — Settlement-only static construction retirement completed in code

- Verified saved Additional Traits authored roots/template before deleting its
  prefab generation chain, ConfigureSettlementPresentation and cost-icon style/layout
  helpers. Dynamic authored-template cloning and gameplay ownership are retained.
- Replaced Hangar curse-layer construction with serialized Image bindings and
  Install/Validate Hangar UI. Removed ship/message typography and aspect resets;
  authored ghost/edge/indicator baselines now drive intentional feedback.
- Replaced Settlement's Settings modal construction with serialized modal/menu
  bindings and Install/Validate Settings UI. Scoped partial repair reuses the shared
  Editor defaults in a PreviewScene and preserves existing presentation.
- Shared Options runtime construction remains for pause; Editor dropdown construction
  now uses explicit target-scene creation instead of TMP's active-scene factory.
  Serialized rebind/dropdown guards survive scene reload. Settings input/pause is
  acquired only after valid bindings, and owned subscriptions are idempotent.
- Existing resource/navigation/Restoration/reinforcement migrations are preserved.
  Gameplay, catalogs, rewards, purchases, persistence and dialogue were not changed.
- Added focused Hangar/Settings fixtures and replaced legacy trait/re-style tests
  with authored/no-construction expectations. Runtime and Editor/test sources were
  compiled; Unity tests, production installation/reload and visuals were not executed.
- No production scene, prefab, protected asset or generated project was saved.
  Newly migrated Hangar/Settings require manual installers before Play. Saved Hangar
  typography is preserved and needs visual inspection. Pre-existing inactive
  Canvas/Temp_ClearButton has unresolved DebugSettlementDataButtons script GUID
  38fef9b49173e9d498b97aaa02cf1d81; unrelated debug repair remains separate.

## 2026-09-07 — Expedition scene-owned HUD authoring and verified retirement

- Added scoped Operation, Status, Resource, Radar, Boss Status, Return Confirmation,
  Wormhole Confirmation and Message install/validate menus under UI > Expedition;
  added read-only Shop validation. No production scene or prefab was edited/saved.
- Removed operation hierarchy construction, additional fixed resource cloning,
  boss triad child construction/destruction, travel-confirmation fixed layout
  construction, shop/maintenance typography resets and warning-message layout reset.
- Existing Expedition ReinforcementSlotUI is adopted by type. Status installation
  disables shared fallback/layout writes for its HUD only. Tutorial's remaining
  shared status/scope construction, shared Map/Inventory augmentation, pause/Options,
  dynamic catalogs/markers, world-space feedback and persistent services are retained.
- RadarScopeGraphic now has a matching script/meta. Boss triad bindings persist and
  ratio updates derive from authored height. Listener cleanup uses the subscribed
  HUD/heat publishers. No transaction, reward, save or input authority was replaced.
- Runtime and Editor/test-source compilation passed. Focused Unity tests, production
  installations/validators, save/reload, scene loops and visual verification were not
  executed. Run the separate installers and validators, inspect, save manually,
  reload, then execute EditMode and Play Mode regressions before release.

### 2026-09-07 — Explicit Expedition status layout repair

- Added `VOID SCRAPPER > UI > Expedition > Repair Status Layout`, restricted to
  existing HP/armor/cargo presentation. Ordinary installation remains nondestructive.
- Recovered compact dimensions, typography and bar insets from prior HUD defaults;
  reserved a one-pixel armor strip before heat and placed cargo below the status row.
- Added an authored armor-background binding; retained Slider mappings, dynamic
  fill ratios, visibility, state colors, fonts, sprites, materials and all input/data
  authority. No runtime layout-reset option or production asset changes.
- Added reference-bounds validation and PreviewScene regression coverage for scope,
  values, fade/full-state, manual-edit preservation, idempotency, Undo and rollback.
- Runtime/Editor compilation is separate from Unity test execution and visual
  acceptance. Use Install Status UI for missing bindings, Validate Status UI, inspect,
  save manually, reload, and verify the 480x270 Game view before accepting the layout.

### 2026-09-07 — Tutorial shared Status/Cargo/Hint/Radar authoring

- Added Tutorial Install Status UI / Repair Status Layout / Validate Status UI
  menus through explicit scene-scoped existing Editor helpers. The reference layout
  is shared with Expedition; ordinary installation preserves later manual edits.
- Tutorial's absent objective tracker stays absent. HP/armor, cargo, heat,
  reinforcement, binding hints and fixed radar scope are authored; resource and
  guidance migrations remain separate and unchanged.
- Removed final shared fixed child builders and base-layout/font writes after
  checking both scene consumers, prefab/asset GUIDs, lifecycle/source callers,
  serialized events and tests. Retained world gauges, dynamic markers, runtime
  values/visibility/animation and inert serialized compatibility markers.
- Tutorial's saved HP canvasGroup references StatusRoot: correct that optional
  field to None or a gauge-owned group in Unity before installation. Invalid
  assignments are reported rather than silently cleared.
- Added PreviewScene coverage for scoped installation/rollback/Undo, flow and
  visibility preservation, partial/renamed bindings, shared layout, dynamic radar,
  missing bindings and temporary scene serialization. Compilation does not imply
  executed Unity tests or a production Tutorial installation/playthrough.

### 2026-09-07 — Restore authored Tutorial/Expedition weapon heat gauge

- Verified both saved heat roots are 92x4 with sprite-less Filled Images and no
  Slider. UGUI's no-sprite mesh path ignores fillAmount. Restored the committed
  read-only Slider approach through explicit `Repair Heat Gauge Layout` menus,
  not runtime layout resets or a heat gameplay redesign.
- Kept the latest shared-layout center (-186,110), one-pixel inset and original
  dark root/cyan-to-amber-to-red feedback. Added a darker empty inset track as a
  documented visual judgment; current runtime supports no visible text role.
- Added serialized background/track roles to WeaponHeatUI. Repair adopts typed
  fills, corrects root-as-fill aliases, keeps only the leaf fill moving, disconnects
  and retains inactive legacy handles, excludes navigation/raycast interception,
  and reports all resets with scoped Undo/rollback. Sprites/materials/publishers
  and other HUD presentation remain intact.
- Added status-validation and regression coverage for geometry, mappings,
  publisher refresh, repeated initialization, Undo and rollback. Production
  scenes were not edited or saved; manual Unity installation and visual checks
  remain required. Existing compatibility and gameplay authorities were retained.

### 2026-09-07 — Correct Tutorial heat versus prompt overlap handling

- Confirmed a real saved background overlap: TutorialPrompt is 360x48, alpha .88,
  at screen (60,208); heat baseline is (8,243,92,4). Both canvases use matching
  overlay scaling. The failure was not caused by a full-screen logical root or
  mismatched units in this particular scene.
- Cross-canvas validation now compares contributing panel/track/prompt Graphics
  in common screen coordinates, excludes transparent/non-rendering containers,
  and reports uncertain projection/empty-text cases without root-only rollback.
- Explicit Tutorial repair relocates only heat to the nearest clear pixel position
  when visible overlap is proven; no-space failures retain scoped rollback.
  Expedition baseline, ordinary installation, prompt presentation and runtime
  ownership remain unchanged. Added test-owned projection/relocation/rollback
  fixtures; Unity execution and visual verification remain manual.

## Final equipment roster implementation — 2026-09-22

The approved development board now has 12 Shared / 12 Sweeper / 12 Breacher / 12 Lancer real blueprints. Sixteen new ship definitions complete the catalog: 66 total, 62 ordinary, fourteen Shared legacy items outside the board and four unchanged boss/story definitions. The earlier 32-position / sixteen-pending report is superseded. Old global loadout-capacity and ordinary Lv0-preparation interpretations remain superseded.

Research -> manufacture once -> free fit/unfit -> fresh-run ordinary Lv1 -> expedition upgrades -> run reset. Completed-analysis authority, branch gates, immutable RunContext, no global fitting cap, Terminal Guidance's conditional prerequisite, nonrefundable deployment provenance and existing Active Reinforcement flow remain. Existing effects, rarity, IDs and GUIDs were preserved.

New content uses five A data-only definitions and eleven B focused extensions. Twin Feed now supplies periodic paired cadence at MAX as explicitly approved. Typed modifiers, weapon/dash/health owners, projectile snapshots and pooling implement the effects; no new manager or per-module Update. Save v7 retains previous owners' earlier research availability and preserves displaced Shared use without currency migration. The existing authored Settlement board reads the final metadata without a scene/prefab rewrite.

Values, new rarity and manufacturing recipes are provisional. All sixteen dedicated icons remain TODO; generic presentation is retained. Production reward weighting is unchanged. Frames, post-ending Curse content, universal debuffs and final economy remain unimplemented.

Detailed roster, values, recipes, migration, diagnostic evidence and validation: [EQUIPMENT_FINAL_ROSTER.md](EQUIPMENT_FINAL_ROSTER.md), [EQUIPMENT_DEVELOPMENT_MAPPING.md](EQUIPMENT_DEVELOPMENT_MAPPING.md), [EQUIPMENT_CATALOG_AUDIT.csv](EQUIPMENT_CATALOG_AUDIT.csv).
