# VOID SCRAPPER System Design

## Equipment Development inspection and preparation (2026-09-21)

Settlement researches, inspects and prepares equipment. Expeditions acquire, upgrade
and max out equipment through RunRuntimeTraitStore and RunTraitAcquisitionService.
Prepared IDs remain a persistent candidate pool, not granted levels. Legacy permanent
ordinary levels remain save-compatible but neither apply run effects nor have a
Settlement purchase action. Persistent story traits retain their separate authority.

ShipTraitTreePanel's authored Equipment Development view separates catalog inspection
from free Activate/Deactivate. Activation fills an empty unlocked slot; a full pool
never silently replaces another item. Deactivation only removes the prepared ID.
The active run keeps its captured pool. Selected is yellow and hover/focus is blue.
The compact capacity count allows empty slots in the existing four-by-three layout.

The detail area has an icon/name, small compatibility tag, starts-unowned/MaxLv label,
description, scrollable authored growth rows, candidate status and one activation
button. Each visible row uses TraitEffectTextUtility.BuildEffectText for that exact
level. RunTraitEffectApplier applies entries incrementally at each acquisition,
including when reconstructing stored levels. Rows explicitly describe added effects;
they do not invent cumulative totals for multiplicative, capped or mechanical effects.
Only actual LevelEffects appear. Mechanical entries use the same formatter as run
rewards. The installer authors reusable rows to the catalog maximum; runtime creates
no UI hierarchy. Missing rows fail authored-reference validation.

Equipment for a research-locked ship remains inspectable, with its actual component
analysis/ship unlock condition replacing growth rows and activation. An unlocked but
incompatible record asks the player to select its ship. No price is shown: preparation
is free, and this pass adds no research economy, debuffs or balance changes.

Capacity remains 3/6/9/12. The first two analyzed components use saved DeepZone1 and
DeepZone2 authorization. The third uses campaign_phase_navigation_lens_analyzed in
PermanentProgress's existing unlock flag store, committed by SettlementController's
natural analysis completion/save transaction. Merely recovering the third part grants
no row; successful analysis grants 12 while the Core remains ReadyToAssemble.
Assembly, activation, defense and final launch stay under their existing authorities.
Old assembled saves migrate that analysis flag to preserve valid last-row selections.
F10 appends preset 11 for third analysis before assembly, preserves preset indices
0-10, and clears the flag on backward checkpoint replacement.

## Tutorial focus, Purple Core and Radar presentation (2026-09-20)

TutorialFlowController acquires the existing ExpeditionHUD cinematic owner when target
focus or Purple Core camera focus is accepted. The new optional duration overload fades
the gameplay CanvasGroup over 0.22 seconds with unscaled DOTween; existing immediate
callers retain their behavior. TutorialPrompt and the world interaction prompt hide
immediately. Pixel Crushers remains outside the gameplay canvas. Natural return, invalid
target/camera ownership, interruption and teardown release only the Tutorial owner.
The HUD uses its own Radar suppression owner once hidden, preserving other owners and
the scanner's requested open state; restoration releases only that owner.

Tutorial's existing AlienSignalSource/PulseVisual is now a sibling of VisualRoot, which
still contains CoreVisual. Ready presentation owns one stationary 1.6-second expanding,
fading wave and the existing local float. Travel stops/hides the wave before detaching
only CoreVisual. Interrupted transfer restores the authored visual and Ready presentation;
re-enable restarts the wave only for Ready, never CurseApplied/persistently consumed Core.
The existing corruption effect, trait acquisition and Tutorial checkpoint owners remain.

Both saved scene copies use RadarPanelRoot/FunctionalRadarRoot/RadarArea/
RadarScopePresentation/ScanSweep. Existing RadarFrameImage objects were repurposed in
place as sprite-free one-pixel scan lines. RadarScopeGraphic retains background, border,
rings and crosshair; it additionally accepts presentation-only border intensity.
RadarPanelAnimator retains Open/Close/Toggle/immediate methods and owner suppression,
with no frame arrays, frame-rate coroutine, runtime UI creation or replacement manager.
Open: 0.21 seconds, alpha 0-to-1, scale 0.92-to-1, five-pixel vertical offset, OutQuad/
OutCubic. Close: 0.18 seconds. Idle: one 1.5-second Yoyo border intensity 1-to-1.1.
Successful scan: one reusable 0.32-second sweep and border pulse, with circular clipping
by line width. All are unscaled; transitions cancel prior owned tweens, and suppression/
disable stops idle and scan without changing the requested state.

PlayerRadarScanner adds only a successful-scan presentation notification. Scan authority,
cooldown, discovery, map registration, stealth, marker semantics/icons, world pulse,
Radar audio and Tutorial progression remain unchanged. RadarHUD's post-scan background
alpha multiplier remains 0.55. No external artwork or new audio is used. The exact
Settlement dialogue-active launch guard now uses its existing Korean message if the
localization service is absent, rather than exposing the localization key.

## Boot persistence and authored result/options lifecycle (2026-09-20)

Boot owns the complete canonical CoreRoot, including Canvas_RunResult. GameBootstrap
rejects incoming duplicate roots before the loaded-scene gate and preserves canonical
Dialogue Manager pruning. Initial persistence waits for sceneLoaded when Awake sees an
unloaded scene. A one-shot guard is claimed before DontDestroyOnLoad; Start only executes
the initialized start flow. Preview, invalid, unloaded and Editor backup ownership never
authorize a persistence mutation. Backup-scene configuration recognition is separate.

RunResultPanelUI inherits CoreRoot persistence. Result/settlement/record cards, accent
rails, texts, Continue and twelve reusable resource rows are authored and serialized in
Boot. Runtime validates references, subscribes to RunEnded, updates values/visibility and
animates existing objects. It neither creates nor reparents the hierarchy. Missing layout
is a configuration error, not a fallback. Explicit legacy migration is Editor-only and
rejects Play Mode. Existing reward settlement, ending pause, fades and Continue ->
Settlement authorities remain unchanged; disable cleanup cancels callbacks and routines.

Boot options retain the SettingsMenuTabController component on
BootMainMenuUI/SafeArea/OptionsPanel. Deep validation covers four tab roots/buttons,
eighteen rebind rows and corresponding guards, nested settings controls and canonical
shared bindings. Dropdown guards are Resolution, Fullscreen, FrameLimit, in that order.
MainMenuController uses ConfigureAuthored, never the runtime Build fallback. Shared Build
remains available for the existing gameplay pause context. SettlementSettingsPanel keeps
an accepted Open request through first activation/Awake instead of immediately closing.
A destroyed-owner check in the Settlement navigation pointer prevents its OnDisable
callback from reaching an unloaded controller during the required return-to-Boot flow.

## Progressive Settlement Recovery / Route Core hub (2026-09-20)

The existing recovery primary button and Repair_HUD are one progressive campaign hub.
Before PermanentProgress.CurrentRouteCoreState reaches Assembled, they retain Settlement
Recovery and the existing facility carousel, including explicit Recovery Processor story
restoration. PermanentProgress.TryRestoreDamagedAccessKey remains the restoration owner;
SettlementController still performs its normal save. No optional building level gates
this handoff. Assembled and Activated change the same primary label to Route Core.

SettlementUIController immediately projects the handoff through existing Changed events.
Its transient facility-view preference is never saved as campaign state. Route Core mode
reuses the detail footprint, replaces the building preview with three restored component
names, and presents assembled, activated/defense pending, defense complete, or campaign
complete status. Text and actions use Localization.csv; story part-name keys are reused.
A subordinate Manage Facilities action keeps all four optional facility views reachable,
with a return action to Route Core. Selecting the primary slot always opens the story view.
Blue hover/focus and persistent yellow section selection are preserved.

The original RouteCoreDeckButton is now inside Repair_HUD. Its persistent EnterDeck call
still targets SettlementDefenseEncounterController: existing player placement, existing
camera adjustment, management visibility and ReturnToFacilities/LeaveDeck restoration.
The menu never activates the core, starts defense, or launches FinalNetwork. Physical
SettlementRouteCoreController interaction retains those authorities and prerequisites.
Completed campaigns may still visit the deck, preserving the existing replay policy.

SettlementProgressPanelUI remains a separate read-only next-objective projection. Neither
its authority nor reinforcement selection, remote intros, defense or final combat changed.

## Settlement / Tutorial UX polish (2026-09-20)

The Settlement sidebar includes a compact, scene-authored CampaignProgressPanel below
Additional Traits, with Settings below the progress block. SettlementProgressPanelUI is
a read-only projection of PermanentProgress. It shows the next objective, required boss
parts out of three, Route Core state, and relevant defense/final-expedition availability.
Pending Settlement analysis takes precedence over collecting the next part. Later goals
are Recovery Processor restoration, core activation, defense, final launch, and network
neutralization. No quest state, progression writes, or runtime UI construction are added.
The panel refreshes on enable/start, PermanentProgress.Changed, existing Settlement
controller readiness/change notifications, and settings/language changes. Text comes
from Localization.csv with named {count} and {state} placeholders.

Settlement navigation and Ship Reinforcement use blue for hover/keyboard focus and
yellow for the selected section or purchase target. Hover does not change selection.
Reinforcement details and Upgrade always show/use the yellow selected card. Click or
Submit commits a card; moving focus merely previews its blue highlight. The existing
SettlementController/PermanentProgress economy transaction is unchanged.

Explicit remote starts retain DialogueCommIncoming (111), then request a 0.24-second,
unscaled DOTween scale reveal from the existing DialogueCinematicPresentationController.
The existing DialogueSubtitleTypewriter holds visible characters until this reveal ends.
Pixel Crushers still owns conversation acceptance, subtitles, Continue and completion.
Intro ownership resets on conversation end, interruption, scene unload, disable/destroy;
duplicate requests in one session do not restart it. The existing panel Animator owns
alpha while the intro owns only a temporary scale, restored on completion/cancel.

Remote authorities are DialogueStoryEntryPoint's serialized audio opt-in (the authored
NULL DISPATCHER treatment/ending entry) and TutorialFlowController.StartRemoteConversation
(Operator guidance, relay analysis/fake takeover, unknown-key contact, rescue transmission).
FieldNpcObjective's direct RescueContact and the local Settlement story entry retain
their existing presentation without this cue/intro. No actor-name inference, global
dialogue beep, generated dialogue graph changes or new presentation singleton.

## NULL DISPATCHER Pass 4E (2026-09-14)

Final death now follows: authoritative combat cleanup -> existing BossDeathPresentation
-> optional authored NullDispatcherEndingPresentation -> FINAL_NullDispatcherTermination
-> network shutdown -> the existing BossDummyController FinalVictory branch -> RunResult
-> the existing Continue / Settlement load. BossDummyController remains the only boss-side
CompleteRun owner; the ending never saves, changes progression, or loads scenes.

The concrete ending restores the surviving InnerCoreRoot renderer after death breakup,
keeps OuterShellRoot inactive, frames player/core with source-owned camera focus, cancels
dash safely, and acquires source-owned movement/control and weapon locks. DOTween provides
1.2 seconds of weakening purple flicker and 1.3 seconds of neutral shutdown/pulse. The six
subtitles use the existing readable Dialogue System pace and Continue controls; there is
no fixed timeout that skips an active conversation. Actual total duration depends on the
reader. Existing background recovery is deferred until this final-only ending completes.

The deterministic graph has START (0), four Dispatcher lines (1-4), two Operator lines
(5-6), and a natural-completion terminal (7). No responses, gameplay actions, save writes,
or treatment-branch ending variants are added. The purple administrative copy remains
independent inside the player; the story does not remove the Pixel Curse.

The shared treatment entry retains its all-ended notification setting. Ending completion
checks its own bridge terminal marker. A live interruption permits one restart. Three
failed start attempts, or a second interruption, report an explicit error and permit a
controlled shutdown fallback; neither bypasses BossDummyController's final authority.
Scene/state exit, run completion, disable/destroy or cancellation revoke only ending-owned
locks, dialogue subscription, camera focus and tweens. Foreign conversations/locks remain.
Missing optional ending binding preserves the legacy handoff; the canonical final prefab
requires and validates the binding. No beacon, portal, new reward or ending save flag.

Prototype ending content is implemented. Dedicated final art, audio, credits, and manual
480x270 readability/combat acceptance remain polish. See IMPLEMENTATION_STATUS for actual
validation; no Play Mode acceptance is implied by source or EditMode tests.

## NULL DISPATCHER Pass 4D (2026-09-14)

The final controller now reserves 20% HP through EnemyHealth's existing source-owned
floor API. This is a distinct owner from the 50% treatment gate. It is armed before
Weapon Lab support can deal damage; a support strike that reaches the floor waits for
facility support completion before starting the final transition. No damage API or
FinalVictory authority is duplicated.

- PolarityPhase at <=20% enters FinalTransition exactly once. The controller revokes
  spawning/collision authority, stops the one scheduler and fixed-step packet routine,
  clears lasers/relays and both packet kinds, cancels a pending polarity switch, then
  acquires only a source-owned weapon lock. Movement and dash remain available.
- A 2-second scaled gameplay timer owns completion independently of DOTween. Four
  authored shell remnants move/rotate/fade outward, the outer core fades, and a smaller
  corrupted purple core appears. DOTween failure cannot softlock progression. Pause
  freezes the timer and scaled presentation; cancel/run exit stops owned work.
- PF_Boss_NullDispatcher has OuterShellRoot (the preserved TemporaryCoreVisual plus four
  existing Square-sprite remnants) and a separate, initially hidden InnerCoreRoot using
  existing core5 art. At completion the outer root hides, inner core is exposed, and
  BossDeathPresentation.SetBodyRenderer selects the inner core for the existing death
  presentation. No physics debris or procedural placeholder visuals are added.
- FinalPhase removes the 20% floor and releases only its weapon owner. It preserves
  current WHITE/BLACK polarity. One scheduler alternates two reduced combinations:
  A: three telegraphed narrow partition lanes plus four individual shots at 0.6-second
  intervals; B: two relay bursts of two packets plus one three-packet compression fan.
  There are never more than two major mechanics; the global 24-hostile/2-support caps
  are unchanged. Telegraph is 0.8 seconds; final recovery is 0.8 seconds.
- Each final combination clears owned hazards/packets before recovery. After two
  combinations, the existing clear-before-warning-before-toggle polarity rule runs.
  Support remains 1 HP, now every 3.5 scaled seconds; launches defer while partition
  lasers are damaging. The one-time facility support sequence is never retriggered.
- Death immediately clears final routines, packets, lasers, HUD and owned tweens/locks.
  BossDummyController remains the sole boss-side FinalVictory delegate to RunManager:
  commit resources/progression, save, end run and enter RunResult. No beacon/portal or
  new scene load is introduced. Ending insertion remains a later lifecycle task.

Implementation does not imply manual Play Mode acceptance. See Implementation Status
for actual validation. Ending cinematic/dialogue, credits, dedicated final art and
final audio polish remain unimplemented. BossPhase2 is reused as a provisional cue.

## NULL DISPATCHER Pass 4C (2026-09-13)

The one-time facility support completion now enters PolarityPhase, superseding the
historical PostSupportCombat continuation described in Pass 4B below. Accept reclaim,
Reject, treatment completion, support scaling and FinalVictory ownership are unchanged.

- NullDispatcherBossController owns runtime-only White/Black polarity, initially White.
  Hostile packets always spawn opposite to that polarity; Settlement support packets
  match it. No progression, save, build or permanent player state is added.
- One existing main-pattern scheduler remains: Route Partition uses neutral lasers;
  Compression Dispatch uses bounded hostile fans; Phase Redirect charges the paired
  relays and emits hostile packets at the exit. No simultaneous major patterns.
- Two completed main patterns form each cycle. At the safe boundary, admission/collision
  authority stops, BOTH packet kinds are released with their colliders disabled, then
  a 0.5-second scaled warning/pulse plays. Only afterward does polarity toggle, the HUD
  update and spawning/scheduling resume. Existing flying packets never change allegiance.
- One fixed-step encounter routine advances all packets and the support/switch timers.
  GameplayPauseManager freezes these and the scheduler. Movement/dash/fire are not locked
  by Phase 2. DOTween owns only the reused boss pulse, with cleanup on encounter cancellation.
- PoolManager serves two explicit packet prefabs using one concrete packet component.
  Caps are 24 hostile and 2 support. Hostile diamonds deal 1 ordinary damage through
  PlayerHealth.TakeDamage; Armor, Component Shield and invulnerability apply. Support plus
  packets heal 1 through Heal, at most once and clamped to MaxHp. Support launches every
  5 scaled seconds from the left arena edge with light bounded steering and an 8-second
  lifetime; its timer resets during switching. It never retriggers facility support.
- Existing Motion Titles Square sprites form authored diamond and plus silhouettes with
  contrasting outlines in both polarities. No runtime-generated art or fallback spawning.
  Expedition.unity owns one new inactive PolarityIndicator TMP child of Canvas_ExpeditionHUD,
  bound to ExpeditionHUD; no matching HUD prefab exists. Tutorial remains unchanged.
  The 8-point persistent label is the player cue; ship/weapon/Curse colors remain intact.
- Boss death/run exit/scene unload/disable cancel the fixed-step routine and scheduler,
  immediately revoke packet collision authority, release pooled packets and hide the owned
  HUD label. The treatment floor is already removed before support; Phase 2 can cross 20%
  and die normally through the existing FinalVictory. No new 20% floor or ending is added.

Implemented and statically checked; Unity Test Runner and manual Play Mode verification
remain pending. Import/Validate Localization Catalog to install the three new polarity
labels. No treatment graph reinstall is required for Pass 4C.

Still deferred: <=20% final phase, combined-pattern climax, shell break, inner corrupted
core exposure, ending and dedicated final artwork. Phase-2 balance and 480x270 readability
require Play Mode verification.

## NULL DISPATCHER Pass 4B (2026-09-13)

Pass 4B replaces Pass 4A's immediate PostChoiceCombat with encounter-local branches.
The existing 50% health gate, natural-END commitment and interrupted-dialogue re-offer
remain authoritative. Treatment choice is never saved.

- Accept enters ReclaimBeam for 4.5 scaled seconds. Normal movement, aiming and dash
  remain available; a separate source-owned weapon lock prevents firing. The player's
  existing movement composition adds a source-owned 4-unit/second inward velocity to
  movement/dash and legacy impulses. The boss never writes the player's Rigidbody velocity.
- Drain uses PlayerHealth.RestoreCurrentHp, bypassing Armor, Component Shield and hit/dash
  invulnerability. Capture start HP and max HP; floor = min(start HP, max(1, max HP * 0.5)).
  Each scaled step reduces HP by captured max HP * 0.111112 * seconds, clamped to that
  floor and current HP. It cannot heal or kill, and installs no player health-floor state.
- The authored purple two-point LineRenderer follows boss/player. An owned DOTween pulse
  pauses with gameplay. At interruption, remove pull/drain/beam; show an Operator HUD line
  and a core5 break pulse. If near the boss, apply a bounded 0.3-second outward push toward
  a validated destination. Physics and existing player movement constraints own travel.
- Reject skips beam/pull/drain and uses a 0.75-second hostile pulse. Both branches converge
  on SettlementIntervention, allowing 1.5 seconds for the compact non-modal HUD message.
- The same authored SettlementFinalSupportController runs once. Player fire stays locked;
  old player-owned bullets are cleared before removing the boss's transition floor, so
  Weapon Lab support can damage it. Attacks remain stopped until support's C# Completed
  event (alongside the retained UnityEvent) enters PostSupportCombat and releases firing.
- Facility values and building-level availability remain unchanged. Engine support now
  uses the existing source-owned move/dash modifiers instead of snapshotting base values,
  allowing safe expiry/cancellation without undoing later stat changes. Missing progression
  or no available buildings still completes the sequence rather than blocking the boss.
- Cancellation on death/run exit/scene change/disable/destroy stops owned branch effects,
  force/drain, support callbacks and engine modifiers. It releases no foreign input/camera/
  pause owner. Dialogue framing returns before resistance; no beam movement lock is acquired.
- Existing BossLaserWarning/BossPhase2 one-shots provide provisional audio. Four new static
  Localization.csv keys provide non-modal branch messages; the two-choice graph is unchanged.

PostSupportCombat temporarily reuses the three Phase-1 attacks and existing FinalVictory.
BLACK/WHITE polarity, support packets, polarity-switch cleanup, 20% shell break, final art
and ending are not implemented. Static validation is separate from pending Unity testing.

## NULL DISPATCHER Pass 4A (2026-09-10)

This supersedes the final-boss foundation-only status in the historical passes below.
The existing FinalNetwork Core binds PF_Boss_NullDispatcher with BossCampaign_Final,
EnemyHealth, BossDummyController and the existing FinalVictory/death presentation.
One NullDispatcherBossController now begins only at the Core intro's combat handoff.

- One coroutine cycles Route Partition -> Compression Dispatch -> Phase Redirect,
  with 0.8-second telegraphs and 0.9-second recovery. Route Partition borrows the
  Administrator's lane concept: three narrow lasers with open gaps. Compression
  Dispatch uses two bounded five-shot fan sources and a delayed sampled aimed fan.
  Phase Redirect routes four harmless entry shots through two authored relay sprites,
  then reuses the same enemy bullets for aimed outgoing shots. These reuse BossLaserHazard,
  Projectile_Enemy, Bullet and PoolManager; no previous boss controllers run concurrently.
- EnemyHealth source-owned damage floors protect intro at full HP and Phase 1 at 50%.
  Damage clamps before health/death events, so a lethal crossing cannot bypass treatment.
  The first crossing stops scheduling, lasers and owned bullets. The floor remains until
  a naturally completed choice. Removing one owner's floor preserves other owners.
- FINAL_NullDispatcherTreatmentOffer has six NPC lines, two player responses and one
  END marker. Central administrative access was incompletely copied, attached through
  the purple core, and strengthened by recovered components. NULL DISPATCHER claims
  surrendering it permits ship restoration; this is not confirmation of benevolent intent.
- The existing bridge registers an IFinalBossTreatmentAuthority. Typed Accept/Reject
  response actions stage one provisional choice; only natural END commits the local
  result. Interrupted responses are discarded and one active-run coroutine re-offers.
  No save/progression/RunContext choice field exists. Scene/run exit cancels retries.
- Cinematic ownership cancels the active dash, acquires source control/weapon/camera
  locks, and releases only those locks. It never snapshots transient MovementLocked.
  The purple pulse uses unscaled DOTween. Death, cancellation and disable/destroy kill
  owned effects/projectiles, unsubscribe and release protection/presentation ownership.
- FinalBossSettlementSupportPhase is manual-only through TriggerSupportNow. Its old
  serialized 45% field is inert for binding compatibility. Support capabilities remain.
- Either choice enters temporary PostChoiceCombat using the same three-pattern pool.
  The boss becomes killable and BossDummyController still owns FinalVictory exactly once.
  This is NOT finished Phase 2. Reclaim Beam, HP drain/resistance, NPC intervention,
  Reject-specific consequences, BLACK/WHITE polarity, 20% shell break and ending remain
  unimplemented. Temporary core5 art remains; two small core5 relay children are authored.

Content generation remains CSV -> Localization Import/Validate -> deterministic
NULL DISPATCHER Treatment Install/Validate. Generated catalog/database are not manually
edited. This pass is statically verified; Unity Test Runner and Play Mode are pending.

## Settlement Defense Rework (2026-09-10)

The production defense replaces the transmission-anchor/Raider prototype described
below. Route Core activation now corrupts the three already-owned component cores.
Recovery Processor restoration/assembly remains an explicit player operation in the
existing implementation; this pass adds no automatic restoration or new save state.

- Existing SettlementDefenseEncounterController owns the deck and encounter lifecycle.
  One PF_SettlementCorruptedCore prefab / SettlementDefenseCorruptedCore component uses
  EnemyHealth and existing pooled enemy Bullet/Projectile_Enemy infrastructure.
- Exactly three serialized configurations: green SectorStabilizer (70 HP), blue
  PhaseNavigationLens (70 HP), orange MatterCompressor (90 HP). These are runtime
  encounter representations of permanent parts, never cargo or inventory copies.
- A roughly two-second unscaled DOTween introduction moves clean components inward,
  pulses the existing purple Core and an authored wave sprite, then blasts components
  to their combat positions. No movement/dash/fire/cinematic locks are acquired.
- Sequential combat: green radial bursts (10 shots with 36-degree gaps, rotated between
  bursts); blue three aimed shots with delayed target reacquisition; orange two fast
  five-shot fans. Inactive cores have no combat collider or attack coroutine. No Raiders.
- Combat defeat stops attacks and clears that core's bullets. The visual remains with
  a separate interaction collider. Explicit reactivation claims Fusing once, cleanses,
  rises, reparents to the central Route Core, and DOLocalMove/scale/fade fuses it there.
  Only the fused callback advances the green -> blue -> orange order.
- Three defeats alone cannot complete defense. After three explicit fusions and the
  stabilization pulse, the existing Route Core CompleteSettlementDefense commits/saves
  SettlementDefenseCleared and releases SettlementDefense state. Facilities return;
  the player later chooses existing Final Expedition launch. No automatic launch.
- Activation remains Activated on death/cancel. All encounter actors, bullets, tweens,
  and interaction subscriptions clean up; every retry starts three full-health cores
  and zero fused. No part loss, curse change, retry cost, cooldown or saved retry flag.
- RouteCoreDeckHUD/ReturnToFacilities is bound directly and hidden throughout defense.
  Management Canvas stays hidden. Cleanup restores prior navigation visibility and
  normal management state. The deck's existing viewport constraint remains source-owned.
- Five defense.core localization keys replace the old anchor objective. Existing story
  recovery name keys are reused. Generated catalogs are not edited.

Temporary core5 sprite art is reused for components, central Core and pulse. Core.prefab
finalBossPrefab and PF_Boss_NullDispatcher are unchanged. Real final-boss combat/ending
remain future work. This implementation is statically compiled/audited; Unity tests and
manual Play Mode acceptance remain pending.

## Campaign Vertical Slice Pass 3 (2026-09-09)

The three missing authored-content connections from Pass 2 are now present. This is
an authored, statically checked encounter foundation, not verified final-boss combat.

Settlement remains a management screen. Its new Route Core button enters an authored
world deck with a copy of the existing Expedition player components, input/loadout
bindings and ship visuals. The deck creates no RunContext. PlayerRuntimeStatApplier
reads the selected ship, weapon and permanent upgrades. Expedition-only death/return,
radar, run-trait and reinforcement behaviors are disabled on this copy. Leaving or
failing defense restores the facilities UI; returning to the deck permits a fresh
attempt without losing story parts. Camera projection and viewport confinement are
owned by the deck visit and restored on exit; no cinematic/control/pause lock is used.

RouteCoreRoot uses the existing SettlementRouteCoreController and IInteractable path.
Before restoration, interaction directs the player to Recovery Processor; it does
not call the assembly API from the world object. Explicit Recovery Processor
restoration already produces Assembled in this project. The next Core interaction
activates/saves it and starts defense. After defense, the same interaction calls
SettlementController.LaunchFinalExpedition, retaining dialogue/ship/save/scene guards.

The one concrete SettlementDefenseEncounterController spawns three authored stationary
transmission anchors (EnemyHealth, 100 HP) and exactly six existing Raider escorts
(one Enemy_Basic and one Enemy_Shotgun per anchor, 24 HP each). These are the same
escort prefabs used by Raider Commander. Defenders target the deck player; no rewards
drop. Only the three unique EnemyHealth.Died events complete the objective. Remaining
escorts and their projectiles are cleaned up; stray enemies never block completion.
Cancellation, death, external state changes and scene exit cannot grant completion.
Route Core alone records and saves SettlementDefenseCleared. The prototype auto-clear
flag is false. DOTween only reveals anchor visuals; it has no completion authority.
SettlementHUD mirrors its message into the small deck objective label (3/3 -> 0/3 ->
complete). HP and the current binding-aware interaction prompt use authored TMP labels.

Core.prefab now binds PF_Boss_NullDispatcher for FinalNetwork. The dedicated prefab
contains EnemyHealth (300 HP), a fixed Rigidbody2D, collider, BossDummyController bound
to BossCampaign_Final, BossDeathPresentation and a visible enlarged core5 sprite.
EnemyHealth recognizes the shared boss authority for boss feedback. FinalBossBattle
and SettlementDefense are gameplay states; Emergency Return also recognizes the
final boss state as boss combat. Defeat delegates to existing FinalVictory ownership.
There is no final attack controller. Existing support components are authored but
disabled, so the old 45% support callback does not pre-empt the future narrative design.

Route Core, transmission anchors and NULL DISPATCHER use existing core5 artwork as
temporary presentation. Replace it with dedicated station/anchor/spherical-shell art
later. The 50% choice, treatment beam, polarity, NPC support sequence, final phases and
ending remain unimplemented. Ordinary Region 1-3 story/route/portal rules are unchanged.

## Campaign Vertical Slice Pass 2 (2026-09-09)

Region 2 first clear uses the authored Salvage Devourer Frigate Triad and scripted
corridor; its repeat map uses the shared Raider/Core path. Region 3 first clear
remains Coreless, with the generator placing Phase Gatekeeper and reflector plates.
Existing PermanentProgress boss/part collections, highestUnlockedDepth, RouteCoreState
and settlementDefenseCleared remain authoritative. First story kills save the unique
part before presentation, expose Beacon only, and require natural Settlement analysis
for ordinary route authorization. Region 3 never offers a FinalNetwork portal.

The shared boss death authority now stops the region-specific combat controllers
before releasing BossBattle or starting death presentation. Phase Gatekeeper camera
focus and offset locks are source-owned; late/repeated cleanup cannot release another
presentation's ownership. Its intro cancels an active dash through PlayerDash.
Triad cleanup preserves BossDummyController for its saved reward/exit handoff.

Portal entry preserves the active RunContext. ExpeditionBootstrap restores carried
equipment/charges, but no longer replaces an empty or unresolved carried active slot
with starting equipment. Existing vital carryover marks this transition; no new
progression or inventory state is introduced. New ordinary runs still start Normal.
Non-Normal run creation now fails closed when PermanentProgress is absent.

All three parts only produce ReadyToRestore. Explicit Recovery Processor restoration
assembles the Route Core; activation and a real Settlement defense clear are still
required for final launch. An unwired Route Core defense event now reports the missing
binding without entering SettlementDefense. Defense requests are non-reentrant, cancel
only their own state, and cannot accept unsolicited/repeated completion callbacks.
The existing event bridge expects an enabled, authored persistent defense listener.

Content boundary: the current checkout has no authored SettlementRouteCoreController,
no Settlement defense encounter, and no NullDispatcher prefab bound to
Core.finalBossPrefab. BossCampaign_Final and final victory/support hooks exist, but
they are not a playable final encounter. These missing assets/connections prevent a
complete playable final handoff; this pass does not auto-clear defense or invent combat.

## Story Recovery Inventory Presentation (2026-09-09)

This supersedes the missing-art behavior in the previous hotfix. A null
BossCampaignDefinition.StoryPartSprite keeps the shared recovery prefab's authored
generic core for world flight. An assigned sprite overrides it. Neither path
changes the campaign asset or creates art at runtime. Inventory icons instead stay
bound but disabled when dedicated art is missing; text and reserved space remain.

The existing InventoryRoot contains a compact StoryRecoverySection with three
horizontal read-only slots. PlayerBuildStatusPanelUI reads HasBossStoryPart directly,
refreshes on open and the existing PermanentProgress.Changed subscription, and owns
only the slot visuals. There are no buttons, drops, context menus, additional
ScrollViews, cargo entries, saved new-item flags, or duplicate ownership collections.

The permanent part is saved before world recovery. The unscaled DOTween sequence
rises 1.15 units, holds 0.2 seconds, resolves the current player at handoff, reparents
with world position preserved, then moves locally to zero while shrinking/fading.
Pickup feedback/message precedes rewards/exits. If the inventory is already open,
visual completion refreshes the acquired slot and pulses its scale 1 -> 1.08 -> 1
over 0.32 seconds. Closed inventory stays closed and shows correct state on next open.
Both animations clean up owned tweens on disable/destroy/run ending; they add no
movement, dash, weapon, camera, or pause locks.

## Boss Death Recovery Hotfix (2026-09-09)

Accepted ordinary boss death saves the permanent story reward, stops boss combat,
and leaves BossBattle immediately. Reward choice and exit animation do not define
combat lifetime. FinalBossBattle/NullDispatcher and run-ending states retain their
existing owners; Emergency Return still checks ordinary recent combat.

Death/phase/intro input uses source-owned control and weapon locks. Active dash is
cancelled through PlayerDash, never snapshotted as a movement lock to restore later.
Death and phase camera focus/input-offset ownership is released idempotently.
Pickup flight owns no movement, dash, camera, or pause lock.

Capture the resolved death position once, including the Region-2 final-part override.
Boss-generated exits use that anchor: a lone Beacon prefers the exact clear point
and searches outward if blocked; a pair uses the same point as its safe-placement
center. Retain bounded collision-tested placement and existing all-blocked fallback.

A newly saved story part uses the shared Region2BossCoreRewardPresentation prefab:
rise/fade, brief hold, reparent to the player with world position preserved, then
unscaled DOLocalMove toward local zero with shrink/fade. Pickup feedback and the
existing localized acquisition message precede selectable rewards and exit reveal.
BossCampaignDefinition.storyPartSprite supplies dedicated art. Missing dedicated
art skips only this optional visual; do not substitute a generic core as a story
part. PermanentProgress.AcquiredBossStoryParts remains the sole inventory, with no
cargo, manual pickup, or loss rule. No new localization keys or dialogue changes.

## Campaign Spine Pass 1 (2026-09-09)

This contract supersedes older descriptions of defeat-driven route unlocks.
Campaign IDs and permanent boss/story-part collections remain unchanged.
`highestUnlockedDepth` is saved route authorization; loading a save or registering
a boss defeat must not derive a higher authorization from defeated bosses. Existing
saves retain their already stored authorized depth. No migration revokes old routes.
Pixel Curse remains derived from distinct permanent parts and its persistent Trait.

| Region | First story clear | Later cleared-region encounter and exits |
|---|---|---|
| Normal | SectorAdministrator grants SectorStabilizer once, saves before presentation, then rewards and Return Beacon only. Active1 Settlement analysis naturally completing authorizes DeepZone1. | Existing Raider Commander prototype. Beacon plus DeepZone1 portal only when authorization and the current run's boss-clear requirements pass. |
| DeepZone1 | Existing SalvageDevourer corridor/triad grants MatterCompressor once, then Beacon only. Active2 analysis naturally completing authorizes DeepZone2. | Same Raider prototype through the existing Core/intro path, without the story corridor. Beacon plus authorized DeepZone2 portal. |
| DeepZone2 | Preserve the locally implemented coreless PhaseGatekeeper/reflector foundation. PhaseNavigationLens is saved once; only Beacon is available. | Same Raider through the normal Core-based generated-map path. Beacon only; there is no ordinary next-region portal. |

Map encounter selection is captured when generating the map so a first defeat
cannot change the active story map's placement/corridor policy mid-run. The Core's
legacy `region1RepeatBossPrefab` / `region1RepeatBossDefinition` bindings are now the
shared prototype for all three repeat regions. Their names, GUIDs and asset values
are retained. Raider run-clear identity follows the current region; its definition
does not grant story parts or record a new permanent story defeat. Missing repeat
bindings report an error rather than substituting a story boss. Development
`bossstart` follows the selected story/repeat path.

Settlement reuses `STORY_FirstSettlementUnknownCore` and its existing typed
`MainDamagedAccessKeyStart` completion action. The bridge rejects live Lua dispatch
of that action and requires the matching natural END marker plus a matching ended
conversation. `SettlementController.TryCompleteFirstSettlementStory` retains initial
quest completion and additionally calls `PermanentProgress.TryAuthorizeAnalyzedRegion`.
The transaction saves immediately when authorization changes. Repeated completion
is a no-op. Active1/Active2 pending analysis restarts through the existing scene-entry
hook and blocks new Settlement launches until completed; scene entry itself grants
nothing. ReadyToRestore is replayed on return but does not authorize FinalNetwork.

The existing final chain remains: three parts -> ReadyToRestore / ReadyToAssemble
-> explicit Recovery Processor restoration (Assembled) -> Route Core activation
-> Settlement defense cleared -> final expedition eligibility. Existing Route Core
assembly alias and NullDispatcher victory hooks remain intact. No final combat or
ending implementation is added. New ordinary runs still begin at Normal. Portals
reuse RunManager's same-run transition and its wallet/build/trait/vital carryover.

All first-defeat grants/save happen at the boss's death callback before optional
animation. The existing Region2BossCoreRewardPresentation prefab is shared by the
three story bosses. Its unscaled DOTween sequence exposes the core and moves/shrinks
it toward the player with a purple tint. Recovery is hosted by the existing
BossRewardExitCoordinator so corpse release cannot cancel reward/exit creation.
Presentation owns no progression, pause, camera or input lock. Localized HUD messages
name the recovered part; the five new campaign notification keys require catalog
import. New notification translations remain empty, with Korean source present.

After rewards, BossExitSafePlacement still chooses bounded, collision-checked exit
positions. The coordinator rechecks authoritative portal eligibility, creates only
valid exits, disables their colliders and interaction gates, then reveals both from
the reward origin over 0.65 unscaled seconds. Beacon/portal scale and fade in and
the portal rotates into its authored orientation. Owned tweens are killed on owner
or exit disable; restoration is idempotent and restores authored transforms/colors/
collider states. Tween failure completes the reveal immediately. A transient
RunContext first-story-clear flag forbids a same-clear portal even if a route is
otherwise authorized; it resets at the next region and is not a new save store.

Validation: runtime and Editor/test static compilation and read-only asset/source
checks are separate from Unity Test Runner and Play Mode. Execute the new campaign
cases in Phase2CStoryDialogueTests and exit/recovery cases in QAStabilizationPass1Tests,
then focused dialogue/localization/lifecycle suites and full EditMode. Play first,
interrupted, repeated and chained runs at 480x270, including run ending during recovery
and exit reveal. Current source compilation does not establish those runtime results.

## Targeted EditMode follow-up (2026-09-08)

Boot's manual Options bindings now pass read-only serialized validation: 18 unique
rebind rows and three unique typed dropdowns beneath OptionsPanel. Saved order is
preserved. The earlier missing-array finding below is historical, not current.

Navigation input tests use the installed InputSettings public feature flag
RUN_PLAYER_UPDATES_IN_EDIT_MODE on a disposable settings copy: ordinary EditMode
Update selects Editor updates. The real InputSystemUIInputModule receives copied
UI actions and fixture keyboard/gamepad devices. Neutral/press/release events go
through Process, not direct sidebar movement. Finally releases module callbacks,
actions/references/devices and restores the previous settings/current devices.

The reported reinforcement null focus matches installed uGUI behavior:
Selectable.interactable=false clears selection synchronously. The presenter used
to check action focus after this setter. It now captures focus before detail
refresh, restores only its own still-current EventSystem context, and preserves
any other selected control. The selected valid card is preferred, otherwise the
existing sidebar button. Transactions and authored styling remain unchanged.

Pause tracks original/runtime roots and exact component IDs, releases only its
copied action registration, and retains strict lifetime and user-scene checks.
The reported line 90 currently checks scene dirtiness, not object lifetime; new
diagnostics distinguish these cases without clearing any dirty flag. The native
Settings conflict expectation uses exact LogType.Log. No Unity execution occurred
here; the new native-setter and ownership tests are compiled, not executed.

## EditMode isolation and saved-binding audit (2026-09-08, third pass)

Temporary bootstrap, audio, navigation-service and tween fixtures now use
NewPreviewScene, not additive ordinary scenes (which reject an unsaved Untitled
scene). Roots are explicitly moved before parenting/component setup; cleanup
closes only tracked previews and restores singleton/time/cursor state in finally.
The navigation fixture no longer clones Settlement into the user's active scene.
Pause tracks its exact root from creation, destroys it explicitly after callback
release, and retains destruction assertions with scene/instance diagnostics.

Charge-presentation tests disable and unsubscribe the follower before comparing
root/track geometry. Follower tests separately permit projected position changes
while preserving anchors, pivot, scale, rotation, size and world offset. Production
charge projection and all tutorial/camera logic are unchanged.

Settlement fixtures explicitly select Hangar before Next and initialize the
existing reinforcement presenter before testing its guarded navigation action.
Test-added persistent callbacks use EditorAndRuntime in disposable copies only:
Unity skips RuntimeOnly persistent callbacks during EditMode Invoke. The trait
template's optional CanvasGroup is inspected before/after initialization rather
than added by a test and incorrectly expected to auto-bind.

Read-only Boot finding: BootMainMenuUI/SafeArea/OptionsPanel's
SettingsMenuTabController has no saved rebindRows/dropdowns arrays. Eighteen
InputRebindButtonUI rows under KeyboardTab and three TMP_Dropdowns under DisplayTab
exist. These are required cancel/rebinding/dropdown-navigation guards, not optional
fields. Authored SharedOptionsMenuUI configuration bypasses Build and therefore
does not populate these guards. Validation remains strict; manually bind them as
listed in IMPLEMENTATION_STATUS. No installer or runtime reconstruction is added.

Settlement Canvas/SettlementHUD/ShipTraitTreePanel/TraitEntryTemplate has neither
a saved CanvasGroup component nor a non-null canvasGroup reference. This is valid
under ShipTraitNodeButton's optional-group contract; no component repair is needed.
Compiled tests are not executed tests. No safe Unity runner was connected for this
pass; targeted and full same-session execution remain required.

## EditMode fixture ownership (2026-09-08, second pass)

Production scenes and prefabs remain authoritative; this repair changes tests only.
Unity 6000.0.69f1 ObjectFactory.CreateGameObject(Scene, ...) calls
GameObjectUtility.SetDefaultParentForNewObject after creation, which consults
SceneView's default parent and can reparent the result. The previous fixtures did
not verify final ownership before grouping scene roots, allowing a wrapper to
transfer authored owners out of the disposable scene and escape scene-only teardown.

AuthoredRuntimeFixture now creates a plain root, disables it, explicitly moves it
to the requested loaded scene, verifies its handle, then parents it. Runtime
components are added only after this ownership step. No Editor default-parent
state is changed. Creation failure destroys only the new root. Discovery verifies
the loaded scene/path and searches its roots with inactive descendants included;
diagnostics identify names, paths, handles, load/preview state and instance IDs.

Cleanup unregisters only owned EventSystems, releases callbacks before publishers,
destroys exact PreviewScene roots, closes the scene in finally, and checks retained
object references are destroyed. Snapshot comparisons are materialized/sorted and
scoped to the fixture or the explicitly protected user scene; no global Editor
object search is used. Third-pass PreviewScenes supersede the initial additive
Editor NewScene replacement for runtime CreateScene calls.
Deliberately missing resource bindings use exact local warning expectations and
are restored in finally. Runtime assertions and all retired-tool boundaries remain.

The saved ItemDetailRoot, ShopProjectile and Settlement debug-component cleanup is
now confirmed: the read-only 498-asset scan finds no unresolved first-party source
MonoScript GUID. Both item-detail roots/bindings and Settlement HUD/navigation
owners remain. Vendor sample findings are outside production ownership scope.
Static compilation is not Test Runner, scene-loop or rendered verification.

## Phase 2C.2 discovery presentation completion (2026-09-08)

Ancient wreck discovery now follows one reserved sequence:
Radar ScanCompleted / MapDiscoveryController.TargetDiscovered -> reserve unfinished
TutorialTargetPresentationProgress -> acquire GungeonStyleCamera2D's owned focus
and existing source-scoped player/menu/weapon/Radar/interactor/reinforcement locks
-> unscaled 0.6s SmoothStep focus -> existing Pixel Crushers ancient-signal guidance
-> terminal completion -> unscaled 0.5s return -> release -> complete once.

HighValueWreck no longer waits for the target to already enter the safe viewport.
The supply introduction retains its existing safe-viewport hold. Camera motion
still has one late-updating rig owner; TryBeginOwnedCinematicFocusBlend is reused.
TryBeginOwnedPlayerReturnBlend extends that same blend to current player framing
instead of a stale position snapshot. No second controller, timer loop or camera
package is added. The wreck prefab is stationary; its focus remains fixed during
the active conversation. Dialogue never begins during focus or return.

Natural completion still requires the existing Pixel Crushers terminal-marker
contract. Target invalidation, player loss/replacement, scene transition, camera
preemption, interruption, disable or destruction cancel checkpoint completion.
Return uses unscaled time even during dialogue pause. Cleanup releases the captured
camera only with a discovery-specific token, and input only from the actual
publishers captured at acquisition. Unity destroyed-object checks are explicit.
An interrupted presentation waits for a later valid discovery notification; no
automatic retry loop fights another camera/dialogue owner. Existing Radar remains
open; the existing menu open lock prevents map/route interaction behind guidance.

Supply progression already supports never opening Map: the existing
HarvestObjectHealth.Died -> HandleHarvestTargetDied -> TutorialSupplyProgression
path advances Map, RoutePing, TravelNormalSalvage or DestroyNormalSalvage to
CollectResources exactly once. Destruction is actual completion of the supply
objective, unlike planning a route. Existing route placement/proximity hints
remain optional and converge on that same death event. Rewards and subsequent
PlayerCargoController.Changed collection checks remain unchanged.

WorldGaugeFollower and PlayerChargeGaugeUI are untouched: their single-rounding
render-camera projection follows the same late camera during focus/return.
Production UI values, scenes/prefabs, dialogue/localization assets and retired UI
tools are unchanged. No installation or scene-reference edits are required.

## Final fixed-UI ownership and dynamic charge gauge (2026-09-07)

Production Scene and Prefab Inspector values are the sole authority for fixed UI
layout and styling. The completed Boot, Tutorial, Settlement and Expedition UI
install/repair/validate menus and their exclusive authoring helpers are removed.
There is no replacement UI menu workflow. Missing runtime bindings report the
owner/property/path/scene for manual Inspector correction and skip only that
presentation; they do not reconstruct fixed UI.

The saved fixed-HUD audit resolves required status, heat, reinforcement, cargo,
resource, prompt, operation, navigation and Settlement panel references.
Tutorial's absent Expedition objective tracker is intentionally optional.
The Map/Inventory prefab remains a complete runtime-owned menu unit with dynamic
content and existing optional map overlays; it is not reauthored by this cleanup.
Dynamic trait/technology template entries, radar contacts, world markers,
world/player-following gauges, pooled feedback, and shared Pause/Options runtime
construction remain with their existing owners.

PlayerChargeGaugeUI remains shared by Tutorial (dynamic instance) and Expedition
(saved instance). Both scenes use GungeonStyleCamera2D and CameraZoomController2D,
not an attached Cinemachine Brain or Pixel Perfect Camera. Their world-HUD canvases
are 480x270 Screen Space Overlay canvases. The old WorldGaugeFollower independently
rounded camera position to 1/32 world units, then rounded canvas-local coordinates,
despite the render camera not using that snapped position. It also assigned
Canvas-local points as anchored positions and forced a pivot. These mismatched
coordinate authorities caused stepped movement relative to the rendered player.
PlayerChargeGaugeUI additionally punched the whole root scale on cooling recovery.

WorldGaugeFollower now projects through the actual camera matrices in that
camera's render callback after player/camera LateUpdate (SRP beginCameraRendering,
built-in onPreRender fallback), with one write per camera frame. Optional snapping
rounds the projected screen point once, then converts into the immediate parent's
RectTransform space. Saved anchors/pivot/scale and worldOffset are retained.
No transform pulse, smoothing, per-frame reference search, allocation, or forced
layout rebuild is used. Renderer/collider lists for optional target-top anchoring
are cached when binding targets. Charge subscriptions detach from the publishers
actually subscribed; duplicate sources cannot dispatch duplicate callbacks.
Fill, state colors, visibility, cooling hold timing and weapon authority remain.

Runtime regressions use supported disposable PreviewScene copies of saved scenes,
not installation code. Opening these copies does not run gameplay lifecycle or
save preferences. Static compilation is not Unity Test Runner or rendered evidence.
Manual acceptance: in both Tutorial and Expedition test charge while stationary,
moving, aiming/panning the camera, moving and panning together, and zooming. Check
zero/partial/full/released/cancelled charge and machine-gun overheat/recovery,
pause/resume, scene exit/re-entry, stable track/root and unchanged authored HUD.

At this cleanup checkpoint, Phase 2C.2 remained a separate task (see the later
2026-09-08 implementation above). Dialogue, checkpoints, routes, camera
presentation settings, quests, saves, localization and input ownership are unchanged.

> All earlier UI installation/repair/validation passages below are historical
> migration records, superseded by this ownership policy. Do not rerun them.

### Historical record: Authored UI ownership freeze and Expedition reinforcement (2026-09-07)

Saved Inspector presentation is authoritative across the migrated Boot, Tutorial,
Settlement and Expedition UI. The former navigation/status/heat layout-reset menu
commands and reset-only helpers are retired. Earlier reference dimensions below
are historical, not instructions to reapply them. Install completes missing roles;
Validate never changes presentation. Positive supported CanvasScaler configurations
are accepted rather than forced to old reference dimensions.

Creation defaults run only for new objects/components. Boot no longer resets an
adopted scaler; Hangar no longer reorders adopted curse layers; operation briefings
no longer move the fixed panel to the last sibling. Heat/reinforcement configuration
does not rewrite Image geometry, aspect or raycast defaults. State colors, fill,
localized content, visibility/fade, authored-baseline animation, resource compact
ordering and dynamic catalog/contact layout remain runtime-owned. Shared pause,
Options, complete Map/Inventory prefabs and transient world gauges are retained.

Expedition's saved `Canvas_ExpeditionHUD/ReinforcementSlotUI` already has the seven
visual roles and is bound, but its root is inactive. Do not replace, activate or
resize this logical root automatically. The focused menus are
`VOID SCRAPPER > UI > Expedition > Install Reinforcement Slot UI` and
`VOID SCRAPPER > UI > Expedition > Validate Reinforcement Slot UI`.
They require loaded Expedition outside Play Mode. Installation reads Tutorial's
saved slot in an isolated PreviewScene solely for missing-object presentation
defaults, closes that scene, uses one Undo transaction and never saves either scene.
Typed renamed/wrapped references win; ambiguous/wrong-scene assignments roll back.

Inspector locations: follow `ExpeditionHUD.reinforcementSlotUI`; edit its root and
visual child RectTransforms, `iconImage`, `iconRechargeFillImage`,
`activeDurationFillImage`, `readyGlowImage`, `disabledOverlayImage`,
`chargeText` and `keyText`. Existing Expedition names include IconImage,
RechargeFill, ActiveDurationFill, ReadyGlow, UnavailableOverlay, ChargeCountText
and KeyImage/KeyText. No new LayoutGroup or actionable button is added. Preserve
the legacy RechargeGaugeRoot references: current icon-fill policy hides that
Slider. Optional state roots remain optional.

`PlayerReinforcementController` still owns equipment, charges, cooldown and
activation. ExpeditionHUD owns refresh/subscription cleanup and current input
binding text. The slot only displays state; Slider updates use no-notify assignment.
After installing/validating, enable the existing root manually only if desired,
inspect its saved geometry at 480x270, save/reload manually, and test empty/equipped,
cooldown, active/unavailable states, one activation per input and scene re-entry.
Compilation is not Unity execution, production validation or visual acceptance.

Last updated: 2026-09-01

## Dialogue Authority

Pixel Crushers Dialogue System 2.2.73.2 is the authoritative runtime for story,
NPC, tutorial, boss, and campaign conversations. The existing Dialogue Database,
`PF_DialogueManager`, `PF_CommunicationDialogueUI`, `DialogueGameplayBridge`, and
`DialogueStoryEntryPoint` remain authoritative. The project must not introduce a
second dialogue runner, database, manager, canvas, or gameplay-pause owner.

## Localization Architecture

`LocalizationCatalog` is a project-owned generated ScriptableObject containing
stable text keys, Korean source text, optional translations, authoring context,
notes, and length hints. `VoidScrapperLocalizationService` is attached to the
persistent Dialogue Manager. It reads the device-local language setting, resolves
catalog text, applies Korean fallback, formats named placeholders, raises an
instance `LanguageChanged` event, and forwards language changes through Pixel
Crushers' supported language API.

`LocalizedTextPresenter` is an event-driven binding for static TMP labels. Dynamic
gameplay UI remains responsible for supplying current values and localized named
arguments. No localization component polls in `Update()`.

## Authoring and Import Pipeline

Localization is authored in a spreadsheet and exported as UTF-8, RFC 4180 CSV to:

`Assets/02_Scripts/Config/Localization/Source/Localization.csv`

The Editor-only importer validates the complete in-memory document before changing
the generated catalog. Runtime builds read only the generated ScriptableObject and
never load XLSX or CSV. Generated entries are sorted by `TextKey`, and an invariant
content hash prevents unnecessary asset rewrites.

## Language and Fallback Policy

Supported codes are `ko`, `en`, `ja`, `zh-Hans`, and `zh-Hant`. Korean is mandatory,
is the default language, and is the fallback when another language cell is empty.
Unsupported language codes normalize to Korean. Unknown keys produce a visible
development marker. Language preference is stored through `GameSettingsRuntime`
in PlayerPrefs and is not synchronized with profile save data in this phase.

## Localization Key Policy

Keys are stable namespaced strings such as `ui.shop.purchase` or
`dialogue.rescue_contact.offer`; they are never derived from translated sentences.
Translated sentences use named placeholders such as `{amount}`, `{item}`, `{key}`,
`{count}`, `{required}`, and `{region}`. Placeholder sets must match Korean in every
non-empty translation. Input binding display strings are resolved before replacing
`{key}`. Colors, sprites, asset paths, commands, and gameplay IDs are not translated
content.

## Runtime Ownership

The persistent Dialogue Manager owns the one localization service instance. The
service owns language selection and text resolution only. Open project UI subscribes
and unsubscribes to its instance event. Pixel Crushers updates active conversation
text through its existing API; changing language must not restart a conversation or
repeat node effects.

### Persistent Dialogue Lifecycle

`GameBootstrap` on the Boot scene's `CoreRoot` is the canonical creation authority
for the application-lifetime `PF_DialogueManager`. The manager remains a separate
persistent root because Pixel Crushers owns its lifetime. When Boot is loaded again,
the already-persistent `GameBootstrap` removes the incoming Boot scene's manager root
before its normal `Awake` path can create duplicate dialogue, localization, or Lua
bridge services. Component-level duplicate checks remain defensive failure guards;
they are not the normal scene-transition ownership path.

### Settlement Dialogue Modality

An active Pixel Crushers conversation owns modal input above Settlement. The
Settlement UI listens to controller `conversationStarted` and `conversationEnded`
events, disables its existing input `CanvasGroup`, clears underlying selection, and
re-evaluates ship/launch UI when the conversation ends. The authoritative
`SettlementController` independently checks the active-conversation state immediately
before starting either expedition scene load. A blocked request returns
`DialogueActive` and reports `system.settlement.expedition.dialogue_active` through
the existing Settlement status-message path. Dialogue is never ended or skipped by
the transition guard.

## Dialogue Ownership Boundary

Dialogue may present information and later request typed actions, but it does not
own damage, AI, boss patterns, radar, map generation, cargo, return settlement,
shop pricing, permanent currency, or settlement progression. Those rules remain in
their existing gameplay systems. Phase 2A adds no gameplay dialogue actions or
conditions.

## RescueContact Phase 2B Vertical Slice

`NPC_RescueContact_Service` is the first choice-driven gameplay dialogue. Pixel
Crushers remains responsible for graph traversal, response presentation, and
conversation lifecycle. `DialoguePixelCrushersBridge` exposes only the registered
Lua functions `VS_CheckCondition` and `VS_DispatchAction`. String values received
from dialogue data must match the typed `DialogueConditionId` and
`DialogueGameplayActionId` names exactly; numeric IDs and unknown names fail closed.

The implemented flow is:

1. The RescueContact speaks two localized Korean source lines.
2. `RescueContactAvailable` reads the current `FieldNpcObjective` without mutation.
3. Accept requests `RescueContactAccept` once for the active conversation session.
4. `FieldNpcObjective.TryExecuteRescueContactDialogueService()` invokes its existing
   service authority, which delegates objective-signal registration to
   `ExpeditionObjectiveDirector` and `RunContext`.
5. Decline contains no action and conversation-end fallback execution is disabled
   for RescueContact only.
6. Already-claimed or unavailable service presents a localized response instead of
   a disabled-choice reason because the current response UI hides invalid entries.

The bridge never writes Core Signal, currency, objective state, save data, or NPC
completion state. The dispatcher only deduplicates action requests within one
conversation session; authoritative gameplay owners retain validation and state.

RescueContact localization uses these stable keys:

- `speaker.rescue_contact.name`
- `dialogue.rescue_contact.service.line_01`
- `dialogue.rescue_contact.service.line_02`
- `dialogue.rescue_contact.service.choice.accept`
- `dialogue.rescue_contact.service.choice.decline`
- `dialogue.rescue_contact.service.unavailable`

Korean remains the source and fallback. Empty optional language fields are omitted
from Pixel Crushers localized fields so its normal default-field fallback applies.

The runtime bridge is serialized on `PF_DialogueManager`. Conversation graph
installation is intentionally Editor-driven: the project-owned installer validates
the existing conversation, actors, catalog, keys, and complete graph in memory,
then replaces only `NPC_RescueContact_Service` entries through Pixel Crushers data
types. Unity must run this installer and save the Dialogue Database before the
vertical slice is considered wired.

## Phase 2C Story and Main-Quest Authority Decision

Pixel Crushers remains responsible only for conversation graphs and lifecycle.
`TutorialFlowController` remains authoritative for tutorial checkpoints, the Pixel
Curse sequence, and enabling the existing return flow. Its story conversations may
gate a checkpoint until a genuine conversation-end event, but their entries do not
write tutorial, Trait, run, or save state.

`MAIN_DAMAGED_ACCESS_KEY` is a narrative view over existing campaign progression,
not a second quest framework. `PermanentProgress` stores the exact-once started flag
and already owns the saved `BossStoryPart` collection and `RouteCoreState` used to
derive `Active 0/3`, `Active 1/3`, `Active 2/3`, `ReadyToRestore`, and `Completed`.
The three part grants remain exclusively in the verified campaign-boss reward path.
`SettlementController` is the action authority that requests quest start, persists
the changed `PermanentProgress`, and publishes localized Settlement messages. Three
parts do not complete the quest: the player must confirm access-key restoration on
the existing Recovery Processor screen. That exact-once transaction changes the
owned `RouteCoreState` to `Assembled` and saves once. Dialogue conditions are
read-only projections of that authority.

The first-Settlement entry point dispatches `MainDamagedAccessKeyStart` only after a
genuine successful conversation end. `SettlementController` starts the quest only
when its persistent state is `NotStarted`, records the story-completion flag, saves
the transaction, and publishes the start notification. A recovery run where the
quest is already active completes only the missing story flag and never restarts the
quest. Repeated graph branches are condition-only and cannot grant a part. While the
pending story flag remains incomplete, the existing Settlement launch guard
continues to fail closed even between scene initialization and the conversation
start event.

### Phase 2C Conversation Flow

The deterministic Editor installer owns seven project-authored graphs. Movement,
Radar, supply-container, and ancient-signal guidance use separate one-line
conversations (`TUTORIAL_OperatorOpening`, `TUTORIAL_OperatorRadar`,
`TUTORIAL_OperatorSupplyContainer`, and `TUTORIAL_OperatorAncientSignal`) at their
matching gameplay stages. The installer also owns `TUTORIAL_UnknownAccessKeyContact`,
`TUTORIAL_RescueAfterCurse`, and `STORY_FirstSettlementUnknownCore`. It validates
actors, stable IDs, localization keys, links, condition/action adapters, and the
terminal completion marker before changing the Dialogue Database. It is safe to
re-run and never runs in a player build.

Tutorial conversations end at `VS_MarkConversationComplete`; only then does
`TutorialFlowController` record that guidance as complete. Interrupted guidance
remains pending and is replayed only for its current stage. Ordinary gameplay
objectives still advance through their existing input/world checks. Discovery of
the authoritative supply container or high-value wreck requests the same
owner-scoped presentation pipeline: source-aware gameplay input locks are acquired,
the existing `GungeonStyleCamera2D` blends to the specific target over 0.6 seconds
using unscaled time, and only then may that target's conversation start. The target
remains framed until conversation end; a natural terminal completion blends back to
the player over 0.5 seconds, releases ownership, and advances the matching checkpoint
once. Target kind, target reference, conversation ID, and completion checkpoint are
kept together so supply and wreck requests cannot collide. Interruption, invalid
target, scene exit, controller disable, or camera ownership loss releases only the
tutorial-owned locks and never completes the checkpoint. The unknown-key
conversation therefore requests the existing Pixel Curse sequence indirectly by
advancing the authoritative tutorial checkpoint, and the rescue conversation opens
the existing return path the same way. Interrupted conversations leave the
checkpoint unchanged and can be restarted by the existing tutorial flow.

The Map route step is optional guidance. Adding a waypoint remains one valid path
to `TravelNormalSalvage`; approaching the already-discovered supply box through the
existing camera/proximity objective check is the no-route path. Both paths converge
on the same `DestroyNormalSalvage` stage, and `HarvestObjectHealth.Died` remains the
authoritative box-completion event that advances to resource collection exactly once.

### Phase 2C.3 Radar and Tutorial Readability

`PlayerRadarScanner` owns one `IsRadarActive` state. The `Radar` Input Action
(`<Keyboard>/q` by default) toggles that state and the existing Radar panel but does
not scan. `RadarQuickScan` (`<Mouse>/backButton` by default) performs one immediate
scan on press only while Radar is active. The existing 0.5-second unscaled cooldown
remains authoritative. Pause, death, menu, dialogue, and tutorial cinematic locks
fail closed through the existing pause and source-aware input ownership. The former
Q hold timer, release-to-scan path, cancellation state, Radar-only charge effects,
and charge-gauge dependency are retired; shared weapon charge UI remains unchanged.

The Radar tutorial records Q activation separately and advances only after a
successful authoritative scan detects the supply target. Current Input System
binding display strings populate the Pixel Crushers Radar line and the localized HUD
objective. Q alone cannot complete the scan objective. The successful scan starts
the shared supply-target camera presentation before normal Map/optional-route travel.

Phase 2C tutorial dialogue entries contain one idea in one or two short sentences.
Ordinary entries must not embed manual line breaks: TMP owns wrapping, and the CSV
validator rejects newlines in `dialogue.tutorial.*` translation cells. Length hints
are review aids only; 480x270 visual verification remains required.

### Phase 2C.4 Dialogue Text Presentation

`DialogueSubtitleTypewriter` extends the existing Pixel Crushers TMP typewriter on
the project-owned communication UI; it does not replace the dialogue runtime or
continue-button lifecycle. After localization, Pixel Crushers variables, and input
bindings resolve, `DialogueWordWrapUtility` inserts TMP-supported invisible U+2060
word joiners between visible characters in each whitespace-delimited token. Binding
display strings protect their internal spaces before substitution. The source CSV
therefore stays free of generated formatting and manual line breaks, while Korean words
and binding-plus-particle forms remain indivisible. A token wider than the subtitle
area is an authoring error, not a reason to reduce font size.

The entire resolved subtitle is assigned and laid out before reveal. Pixel Crushers'
`maxVisibleCharacters` path reveals that stable mesh using unscaled time: 0.03 seconds
per visible glyph, plus 0.08 after commas, 0.18 after sentence punctuation, or 0.28
after an ellipsis. The existing continue adapter remains authoritative: one press
finishes the current typewriter, and a later press advances. Phase 2C tutorial entries
target no more than two rendered lines at the 480x270 reference layout.

The temporary Operator profile uses one reusable 2D AudioSource created with the
communication subtitle instance, routed through the existing UI mixer group. It
plays `Assets/06_Audio/SFX/Talk/test_talk-sfx.wav` for every second speakable glyph
at pitch 0.97-1.03. Whitespace, punctuation, TMP tags, and invisible formatting do
not produce audio. Fast-forward, conversation end, disable, interruption, and scene
destruction stop the source and invalidate the running typewriter coroutine. Other
actors remain silent unless an approved actor voice profile is assigned; Phase 2C.5
adds the approved Curse and Settlement profiles through this same source.

### Phase 2C.5 Cinematic Dialogue Presentation

`DialoguePresentationPolicy` selects presentation by stable Phase 2C conversation
and actor identifiers, never localized display text. Movement and Radar guidance use
`CompactGuidance` with a 60-pixel lower panel and no world dim. Supply-container and
high-value-wreck guidance use `ContextFocus`: the existing tutorial owner finishes
its camera focus before Pixel Crushers starts the conversation, while the UI adds
only a subtle 14% context dim. Unknown-key contact, post-Curse rescue, and the pending
first-Settlement story use `CinematicCommunication` with a 40% world dim, 14-pixel
top bar, and 72-pixel lower panel. A repeat of the completed Settlement conversation
returns to compact presentation.

`DialogueCinematicPresentationController` is a presentation adapter on
`PF_CommunicationDialogueUI`. Pixel Crushers remains authoritative for conversation,
subtitle, response, typewriter, continue, pause, and completion state. The adapter
does not acquire or release camera/input ownership and does not complete tutorial
checkpoints. Cinematic HUD suppression uses `ExpeditionHUD`'s owner-scoped API;
ContextFocus leaves camera ownership solely with `TutorialFlowController`.

Overlay fades run for 0.18 seconds and existing Pixel Crushers panel animations are
retimed to 0.15 seconds using unscaled time. The 480x270 reference layout reserves a
44-pixel portrait/signal area, attached speaker plate, two-line subtitle body, and a
small pulsing but still selectable continue control. Actor identities select cyan
Operator, purple Curse, warm Settlement, or neutral fallback accents. The single
reusable subtitle AudioSource selects the existing Operator, Curse, or Settlement
text blip; unknown actors use the neutral silent fallback. Curse presentation limits
glitch to a short accent-line entrance pulse and never mutates prepared subtitle text.

Conversation end, interruption, stop-all, scene unload, disable, and destroy all use
the same idempotent cleanup path. That path stops unscaled presentation coroutines,
releases only this adapter's HUD ownership, hides the overlay, and restores the prior
UI selection. Camera and gameplay input restoration remain the responsibility of the
owner that acquired them.

The first Settlement graph selects one branch from the read-only
`MainDamagedAccessKeyQuestState`. `NotStarted` plays eight subtitles: the original
six-line analysis plus two short warnings before departure about stronger anomalous
responses and explicit isolated Recovery Processor restoration. The five two-line
repeat branches report `Active0`, `Active1`, `Active2`, `ReadyToRestore`, or
`Completed`; only each entrance has a condition, and no subtitle has a gameplay
action. Completed dialogue does not claim the persistent Curse is cured.
Original entry IDs 0-12 remain stable; new opening lines use 13-14 and repeat
responses use 15-19. All six branches converge on the original END entry 12.
`DialogueStoryEntryPoint` requests
the typed Settlement completion transaction after the genuine terminal marker and
conversation-end event. An interruption cannot partially start the quest.

Author Korean source in `Localization.csv`, run `VOID SCRAPPER > Localization >
Import Catalog`, then `VOID SCRAPPER > Dialogue > Install Phase 2C Story` and both
existing validators. Do not hand-edit generated catalog/database YAML. The story
installer still owns its nine named conversations; this expansion changes only
the Settlement graph. Source implementation is not evidence of installed assets
or played subtitles; import/install and 480x270 playback must be verified in Unity.

The quest ID is `MAIN_DAMAGED_ACCESS_KEY`. Its started identity is an existing saved
unlock flag; collected parts are the existing `BossStoryPart` values granted by the
campaign-boss completion owner, and completion is derived from existing
`RouteCoreState`. Phase 2C.1 derives Pixel Curse level as zero while inactive, or
`min(1 + distinctBossStoryPartCount, 4)` after the persistent Curse is acquired.
Duplicate parts cannot raise the level, and save reload reconstructs it without a
new serialized level field. Phase 2C does not add part grants, reward writes, or a parallel
quest store. The localized HUD objective key is available, but binding it to a
player-facing persistent quest panel is deferred because no project-owned generic
main-quest HUD presenter is currently authoritative.

## Font and UI Layout Requirements

All supported text must be checked against assigned TMP font assets and fallback
fonts. Korean, Japanese, Simplified Chinese, and Traditional Chinese glyph coverage
must be verified in Unity. Dialogue, choices, notifications, and static labels must
be tested for wrapping and expansion at 480x270 and 960x540. Font or layout failures
must not be hidden by truncation.

## Implementation Phases

- Phase 2A: localization catalog, service, settings integration, TMP presenter,
  UTF-8 CSV import, validation, and focused tests.
- Phase 2B: first typed condition/action bridge and the RescueContact vertical
  slice. General-purpose action/condition expansion remains incremental.
- Phase 2B.1: persistent dialogue lifecycle and modal Settlement transition safety.
- Phase 2C: tutorial story, first-Settlement analysis, exact-once main-quest start,
  and state-aware repeat graph. Static implementation is complete; Dialogue Database
  installation and all Play Mode gates remain required.
- Phase 2C.1: stage tutorial guidance at its gameplay checkpoints, expose explicit
  Settlement Recovery at 3/3, and derive four active Pixel Curse levels from distinct
  boss parts.
- Phase 2C.2: focus the existing camera on the detected high-value wreck before its
  dialogue, restore ownership safely with unscaled blends, and make Map route placement
  optional for the supply-box sequence.
- Phase 2C.3: use Q as the Radar mode toggle and Mouse 4 as the active-mode instant
  scan, retire Radar charging, share target focus between supply and high-value
  guidance, and enforce automatic-wrap-friendly tutorial entries.
- Phase 2C.4: protect Korean and resolved binding tokens at TMP presentation time,
  retain stable visible-character reveal and two-press continue semantics, and add
  the first reusable Operator text-voice profile.
- Phase 2C.5: apply typed compact, context-focus, and cinematic communication
  profiles to the existing Pixel Crushers UI, add actor themes/voices, and guarantee
  owner-scoped HUD and presentation cleanup.
- Phase 2D: verify the complete story/progression loop in Play Mode, connect the localized main
  quest objective to an approved HUD surface, and select the next narrow migration.
- Later migration: convert existing dialogue and project UI incrementally; do not
  perform a bulk string replacement without feature-level verification.

## Deferred Beyond the Phase 2C Slice

- Cross-action transactions or rollback
- Central Network activation/gameplay after access-key restoration
- A persistent main-quest HUD presenter and repeat-conversation world entry
- Tutorial, quest, and conversation migration beyond the seven Phase 2C graphs
- Bulk Pixel Crushers conversation-entry localization import
- Full dialogue and UI string migration

## Unity Verification Requirements

Before Phase 2A is marked Play Mode Verified, confirm catalog assignment, one
persistent service across scene transitions, runtime language changes, Korean
fallback, active conversation refresh without restart, unchanged dialogue pause
ownership, TMP presenter subscription cleanup, CJK glyph coverage, and 480x270
wrapping. Automated EditMode tests and a successful C# compile do not replace this
Play Mode verification.

## QA Stabilization Pass 1

Boss reward exits use `BossRewardExitCoordinator` as the exact-once spawn authority.
It resolves a deterministic two-exit arrangement within the active corridor viewport,
current Region-2 combat viewport, or generated map safe bounds, while avoiding the
Player, reward/death point, blocking colliders, and the other exit. Region 2 retains
`TerminalPoint` as the corridor's bottom world endpoint.

Region-2 Frigate lasers attach their warning, rendered beam, and damage volume to the
moving encounter root. Reused hazards explicitly reset their hierarchy-dependent
runtime geometry and active state. Homing projectiles aim at the active Frigate part's
damageable collider point instead of an arbitrary composite root pivot. Charged normal
enemies continue to track early, then commit one direction that both their final
telegraph and projectile use.

Boss entrances retain their existing authorities. Region 1 stages the boss and laser
units outside the visible frame before combat. Region 2 now blends to the corridor
start through the existing camera owner, using unscaled time, before transferring to
corridor scrolling; interruption cleanup remains idempotent.

Radar stays open after a successful scan and during ordinary combat. The manual Q
command closes it. Successful scans fade only the panel background to 55 percent,
preserve marker contrast, and recolor the reused scan pulse cyan. The tutorial owns one
temporary unknown-objective Radar target and removes it on resolution or lifecycle
cleanup. Boss encounters may still suppress Radar through their explicit owner-scoped
presentation path. Tutorial objective detail selects stable localization keys from
tutorial stage and Radar state: closed Radar explains open plus scan; open Radar
explains scan only.

The Expedition currency HUD creates the Tuning Chip entry from the existing resource
counter presentation when an authored reference is absent, reads `ResourceWallet`, and
uses the established value-change/repack lifecycle so hidden zero values leave no gap.
Settlement facility selection hides the legacy Use/activation toggle until unlock,
then preserves the existing authoritative activation control. Shop purchase audio
routes the existing `shop_buy_success.wav` clip through the existing success-only
transaction callback and Sound Event library.

## Tutorial QA Presentation Pass 2

The movement objective becomes active before the opening Operator transmission.
`TutorialFlowController` observes `PlayerController2D.MovementStarted`; the first
authoritative movement event arms the transmission exactly once, while the existing
2.75-unit movement objective remains independently authoritative. Pixel Crushers
records guidance completion only after a natural conversation end. Interruption clears
the active request but leaves unfinished guidance replayable. An optional serialized
sound-event ID may provide the incoming cue; no unrelated UI sound is substituted when
an approved clip is unavailable.

Radar scan presentation reuses the existing pooled pulse object. Its profile is clamped
to cyan/blue, 0.10-0.18 peak alpha, and 0.20-0.35 seconds, and its DOTween sequence uses
unscaled time. A new scan first releases the prior pulse, and manual close, combat close,
disable, and scene teardown restore the presentation without changing semantic marker
colors or the persistent post-scan Radar panel state.

Internal mystery identifiers remain gameplay data, but player-facing unknown-signal,
Purple Core, interaction, and system-error copy resolves stable localization keys.
The `?` Radar marker remains the unresolved-objective presentation.

Purple Core discovery starts one owner-scoped reveal: the gameplay root activates with
interaction colliders disabled, while only its visual root fades from zero alpha and
scales from 70 percent to full size over 0.55 seconds using unscaled DOTween. Interaction
is restored after completion. A visual-root-only 0.10-unit, 1.75-second yoyo supplies
idle motion without moving the authoritative collider or interaction position.

The Curse sequence retains `TutorialFlowController` input/pause ownership. Current
acquisition behavior is defined by the later Tutorial Relay Takeover section: the Core
sprite child enters the player while the authoritative Core root remains stationary.
Only after ship-origin corruption, player impact, and full black does the existing
persistent-story Trait authority apply the Curse. No dialogue entry mutates Curse or
save state.

## Tutorial QA Presentation Pass 3

The supply container is damageable from the optional Map step onward.
`HarvestObjectHealth.Died` is the sole completion signal and maps Map, Route, approach,
and destruction-step paths to one `CollectResources` transition. Map opening and route
placement never own its health, collider, reward, or completion authority.

Tutorial target focus locks Radar input without changing Radar visibility. Ordinary
combat does not own Radar visibility; manual toggle, lifecycle shutdown, and explicit
boss-owned suppression remain authoritative.

The signal relay is authored at (-8.9, 15.33), remains visible after its one-shot
interaction, and uses its sequence-owned purple pulse before the real/fake Operator
handoff defined below. Its prompt and resolved objective marker do not remain
actionable. Tutorial shutdown kills all owned pulse, Core transfer, impact, reveal, and
idle tweens before guarded access to presentation objects Unity may already have
destroyed.

## Tutorial QA Cinematic Presentation Pass

Radar/map discovery records the supply container or high-value wreck but does not own
camera or input at discovery time. `TutorialFlowController` waits until collider bounds
(renderer bounds only as fallback) remain inside the gameplay camera viewport region
X 0.10-0.90 / Y 0.12-0.88 for 0.15 unscaled seconds. Only then does the existing
owner-scoped camera focus/dialogue/return sequence begin. Invalid targets, camera-owner
loss, interruption, disable, and scene exit cancel without completing the checkpoint;
route placement remains optional.

The signal relay and Purple Core reveal reuse `Assets/03_Prefabs/Purple.prefab` as a
sequence-owned purple ring. Its authored blue Animator/scaled shockwave behavior is
temporarily disabled while the tutorial drives scale/fade with unscaled DOTween; pooled
state is restored before release. Core interaction stays disabled through the existing
zero-alpha/70-percent-scale reveal and starts visual-child-only idle motion when ready.

After the Core contact dialogue completes naturally, the tutorial locks input, shows
the existing letterbox, and uses `GungeonStyleCamera2D` owner-scoped focus/framing to
compose and zoom toward the Core. The Core sprite itself travels to and overlaps the
captured player visual; the Purple effect remains support at the Core or ship origin
and expands/fades before impact. `ScreenFader` must reach full black before the
persistent Trait authority changes background/player state. Camera framing is restored
under black, then the screen and letterbox reopen.
Every cleanup path releases only tutorial-owned camera, input, pause, fade, letterbox,
tweens, and pooled effects. Pixel Crushers and the existing tutorial checkpoints retain
dialogue and progression authority.

## Tutorial Relay Takeover and Purple Core Acquisition

Signal-relay interaction now latches one analysis sequence instead of advancing the
tutorial directly. The existing purple pulse completes first, then the real `Operator`
conversation reports analysis and cuts off. After a 0.15-0.50 second unscaled handoff,
the separate stable Pixel Crushers actor `FakeOperator` uses the concealed localized
display name `오퍼레이터`, the Curse visual/audio theme, and one deterministic takeover
line. Only natural completion of that second conversation advances the existing
`InteractSignalDevice` checkpoint. Interrupted real and fake phases retry only their
unfinished phase; they never replay the relay interaction or pulse reward.

Relay analysis completion clears the broad full-Map search presentation and relay Map
marker. Unknown-signal tracking creates one temporary Radar-only `?` at the Purple Core
position (`ShowOnMap=false`); the localized objective remains authoritative text, and
the marker is removed when the Core reveal resolves or on lifecycle cleanup.

The Purple Core uses a project-owned `IProjectileDamageReceiver` adapter rather than
enemy health. Only typed player-owned projectile contexts with a valid player/allied
source contribute damage. A 0.12-second contribution window caps shotgun/pellet damage
at 4, with a default forced-interaction threshold of 15. The Core cannot die or grant
combat rewards. Damage threshold and `F` interaction converge on one acquisition latch
and one Pixel Crushers contact conversation.

After natural contact completion, the existing camera/input/letterbox/fade sequence
moves only the Core sprite child into the captured player visual target. The
authoritative Core root, collider, and interaction origin remain stationary. The pooled
purple ring stays anchored as Core/ship-origin support, expands at the ship, and fades;
the persistent Curse is applied only after full black. Cancellation restores the Core
sprite parent, local position, rotation, scale, alpha, and active state; completed
acquisition keeps it consumed.

## Boot Main Menu UI Authoring

The Boot main menu is a scene-authored UI owned by `BootMainMenuView`. Its Canvas,
background visuals, safe area, title, menu buttons, options hierarchy, confirmation
overlay, and serialized bindings are installed while `Assets/01_Scenes/Boot.unity` is
open through `VOID SCRAPPER > UI > Boot > Install Main Menu UI`. The command never saves
the scene; the author reviews and saves it manually.

`MainMenuController` owns behavior only: save-dependent Continue availability,
selection, runtime listeners, settings visibility, audio, duplicate-submit prevention,
and scene transitions. It never creates replacement visual objects. Missing authored
menu references fail with one actionable diagnostic. Missing authored background
references disable only `MainMenuSpaceBackground`; the interactive menu remains usable.
The persistent `ScreenFader` remains the transition authority, and the existing Boot
EventSystem is reused.

Normal installation is additive and idempotent. It creates missing objects/components,
repairs serialized bindings, and preserves valid Inspector-authored layout, colors,
alpha, font sizes, sprites, and animation values. `Boot > Validate Main Menu UI` performs
read-only checks for authority duplicates, 480x270 Canvas configuration, bindings,
navigation, fonts, transition ownership, active hierarchy, and reference-safe bounds.
The background binds thirty authored stars, seven asteroid visuals, and one Curse
passer through typed serialized references. Reinstallation repairs null/missing slots
and the `BootMainMenuView.spaceBackground` binding without replacing valid or renamed
referenced objects.

## Tutorial Guidance UI Authoring — Pass 1 (2026-09-06)

Tutorial owns two authored presentation branches only:
`Canvas_Tutorial/TutorialPrompt/{InstructionText,ProgressText}` and
`Canvas_GameplayHUD/OperationStatus/{Accent,Title,Detail}`. The existing
`TutorialPromptUI` presents instruction/progress text and resolves current input
bindings; `TutorialFlowController` retains the authoritative 28-step progression.
`ExpeditionHUD.ShowObjectiveBriefing()` retains briefing content, accent updates,
show/hide, and the existing 0.24/2.4/0.24-second DOTween timing. The operation root's
CanvasGroup remains the animation binding. No tutorial view framework is introduced.

With Tutorial loaded outside Play Mode, use `VOID SCRAPPER > UI > Tutorial > Install
Guidance UI`, then `Tutorial > Validate Guidance UI`. The installer explicitly targets
`Assets/01_Scenes/Tutorial.unity`, including when another loaded scene is active.
It discovers roots within that scene, creates objects directly in their destination
scene, supports Undo, repairs missing serialized references/components, and never
saves. Existing typed references identify renamed authored objects. Ambiguous
duplicates are reported for explicit resolution; failed installation is reverted.
Existing Inspector transforms, colors/alpha, sprites, fonts, font sizes, alignments,
and active states are preserved. Missing fonts use existing Korean-safe project font
assets without editing those assets or their fallback tables. Existing invalid
CanvasScaler configuration is reported for manual review instead of reset.

New OperationStatus defaults reproduce the former Tutorial runtime layout: top-center,
position (0,-14), size 214x44, dark translucent background, left cyan accent, 9-point
bold title, and 7.5-point wrapped detail. It starts inactive. All presentation values
and the existing HUD briefing timing fields remain Inspector-editable; runtime still
updates the title/accent color from briefing content and animates CanvasGroup alpha.

Only a successfully installed Tutorial HUD has `createOperationPresentationIfMissing`
disabled. Valid authored references bypass the builder. With fallback disabled,
missing/invalid operation bindings log one actionable warning per HUD instance and
skip only the briefing visual; they never construct a replacement hierarchy or
disable tutorial progression. Expedition retains its serialized fallback opt-in and
shared construction helpers. No construction code is safely removable before the
other consuming scenes migrate.

This pass does not author Radar/Map markers, Map/Inventory, pause/options, interaction
UI, Pixel Crushers UI, letterbox, debug UI, or tutorial targets. Dialogue, camera,
input/pause release, discovery, routes, combat, and transition ownership are unchanged.
Pixel Crushers remains the sole dialogue runtime.

Installation is pending manual Unity action; no scene/prefab is changed by this code
delivery. Review and save Tutorial manually after validation, reopen it, then check
480x270 Play Mode presentation, briefing interruption/hide, Korean text and rebound
controls, all 28 stages, dialogue release, Radar/Map, and transitions. Run the focused
`TutorialGuidanceUIAuthoringTests` plus existing tutorial/dialogue/camera/input/pause
and interruption EditMode suites. Static .NET compilation is not Unity Test Runner,
scene-reopen, or Play Mode verification.

## Tutorial Resource HUD Authoring Slice (2026-09-06)

This independent, resource-only pass adopts the user's copied
`Canvas_GameplayHUD/ResourceRoot`. It does not reinstall or restyle the working
strip, migrate Settlement, or change TutorialPrompt/OperationStatus and gameplay
ownership. The five `ExpeditionHUD` counter fields are the resource registration
model; `ResourceCounterUI` has no separate currency registry or tooltip presenter.

`CurrencyType.TuningChips` (existing display name `튜닝 칩`) reads
`RunManager.CurrentRun.Wallet.TuningChips`. `CurrencyType.StabilizedAlloy`
(`안정화 합금`) reads `PendingStabilizedAlloy` from that same `RunWallet`.
Alloy uses existing cargo acceptance/accounting and is settled into permanent
progress only through the existing run-result path; Chips are run currency, not
cargo. Pickups use existing cargo/RunManager APIs, then `RunWallet.Changed` is
relayed through `RunManager.WalletChanged` to the HUD. No rewards, accounting,
saves, localization keys, or persistent balance authorities are added.

Use `VOID SCRAPPER > UI > Tutorial > Install Resource HUD`, followed by
`Tutorial > Validate Resource HUD`. Both require the already loaded Tutorial scene
and refuse Play Mode. Installation requires the existing HUD Canvas and resource
strip, adopts typed renamed references, repairs missing components/nested bindings
and HUD registrations, and authors only missing Alloy/Chip rows by copying the
inactive ScrapCounter structure. It preserves existing visual values, uses Undo,
restores previous active states, marks changes dirty, and never saves. Production
discovery is scene-scoped; isolated PreviewScenes are supported only by test entry
points. Duplicate mappings and unregistered/ambiguous rows require manual resolution,
not deletion or guessed reassignment. Validation accumulates binding, duplicate,
hierarchy, scene ownership, icon/font, and fallback-configuration diagnostics.

At inspection, Tutorial's `creditsCounter` and `scrapCounter` both referenced
ScrapCounter, while the Alloy and Chip slots were null. Before installation,
assign Credits Counter to the existing CreditsCounter and retain Scrap Counter's
ScrapCounter assignment. This is an explicit user correction, not an automatic
scene edit by the installer. Existing base rows and ResourceRoot are required.

The new rows are `StabilizedAlloyCounter` and `TuningChipCounter`. Each has a
`ResourceCounterUI` with `rootObject` bound to its own row, an `iconImage`, and an
`amountText`; optional `labelText` is retained, not invented. New icons come from
`RewardPickup.GetCurrencySprite` on the existing pickup prefab: dedicated Chip art
and the established Scrap sprite fallback for Alloy, with the HUD's resource tint.
Existing font assignments are preserved; missing TMP fonts use the HUD font or
the existing `neodgm_pro` asset without changing any font/fallback asset.
Existing Korean display names remain the optional label convention; this slice
does not introduce a tooltip or a new localization path.

Edit each row's `ResourceCounterUI` references in the Inspector. Follow Icon Image
to the child Image's Sprite/Color; follow Amount Text to the TMP component (the
copied row normally carries TMP on the row itself) for font, size, alignment and
formatting. Edit row/child RectTransforms for anchors, dimensions and icon/text
placement. ResourceRoot controls the overall strip offset. Runtime compact layout
still uses the first mapped row's initial position and the HUD's
`resourceCounterRowSpacing`, in Credits, ScrapParts, CoreShards, StabilizedAlloy,
TuningChips order; zero rows leave no gap. Absolute row positions are therefore
repacked in Play Mode, while manual Edit Mode values are preserved on reinstall.

Successful installation disables one aggregate `createResourceCountersIfMissing`
flag on the Tutorial HUD only. Runtime registration/data updates remain active:
initial/re-enable refresh reads the current wallet, wallet events update amounts,
and positive balances reactivate hidden rows. Subscription cleanup retains the
actual publisher so a singleton change cannot strand a wallet listener. Invalid
authored bindings warn once per affected resource per HUD instance and skip that
counter only; the authored path never builds replacement visuals. Expedition and
other unconverted instances retain default-enabled cloning fallbacks. Shared
fallback construction is still required and has not been removed.

Manual delivery gate: correct the duplicate mapping, install, inspect all five
rows at 480x270, validate, save Tutorial yourself, then reopen and validate again.
Run `TutorialResourceHUDTests` and the existing guidance, pause, dialogue and
tutorial regression suites. Verify initial positive balances, zero/positive/zero
visibility and compact ordering, disable/re-enable refresh, and no extra resource
rows during Tutorial play; verify Expedition's fallback and normal pickups/spending.
Do not add Tutorial rewards or use real player saves for test fixtures. Static
compilation includes the focused PreviewScene tests but does not execute Unity
Test Runner, scene reopening, or Play Mode. Settlement remains deferred.

## Settlement Persistent-Resource Strip — First Authoring Pass (2026-09-06)

This supersedes the earlier deferral only for Settlement's fixed three-resource
strip. It adopts `Canvas/resourcesUI` in `Assets/01_Scenes/Settlement.unity` and
authors `ResourceStrip/ScrapResource`, `CoreResource`, and
`StabilizedAlloyResource`. Each cell has its own background Image plus direct
`Icon` Image and `Value` TMP children. Existing valid renamed references are
authoritative; these canonical names apply only to newly created objects.

`SettlementHUD` now serializes `resourceContainer`, `resourceStripRoot` and three
plain nested `ResourceCell` binding groups (`scrapResource`, `coreResource`,
`stabilizedAlloyResource`), each containing `root`, `background`, `icon`, and
`value`. There are no new per-cell MonoBehaviours or resource services. Mapping
is explicitly `CurrencyType.ScrapParts`, `CurrencyType.CoreShards`, then
`CurrencyType.StabilizedAlloy`. Values read `PermanentProgress`, never the run
wallet; all three cells continue displaying zero balances. Existing Korean labels
and the legacy text's Korean-compatible font remain the presentation convention.

Use `VOID SCRAPPER > UI > Settlement > Install Resource HUD`, then
`VOID SCRAPPER > UI > Settlement > Validate Resource HUD`. The installer requires
the already loaded Settlement scene and its existing Canvas/resourcesUI, refuses
Play Mode, validates 480x270 scaling, and uses explicit destination-scene creation,
SerializedObject bindings and Undo. It repairs only missing parts and refuses
ambiguous mappings rather than guessing or deleting objects. It suspends runtime
objects during binding, restores prior active states, marks changes dirty, rolls
back only its own Undo group on failure, and never saves the scene.

Legacy `currencyText`, `scrapCurrencyIcon`, and `coreCurrencyIcon` references and
objects remain intact. Installation disables only those individual Graphics with
Undo; it does not deactivate resourcesUI or remove source assets. Intentionally
hidden legacy objects are valid. Validation checks ownership, unique cell/nested
bindings, font/sprite assignments, legacy suppression, hierarchy and fallback policy.

New-cell defaults reproduce `BuildResourceStrip/CreateResourceChip`: stretch strip
with offsets `(6,2)` / `(-6,-2)`; three equal-width anchor regions, each inset 2px
left/right; dark alpha-0.88 backgrounds; 9x9 icons at `(5,0)`; 7-point autosized
left-aligned values (5.5 minimum). New icons reuse the existing Scrap/Core sprites,
with the established Scrap sprite and alloy tint for StabilizedAlloy. Existing
colors, alpha, sprites, font/material assignments, typography, transforms and
active states are not restyled by reinstallation.

Inspector editing locations:

- Overall position and size: `Canvas/resourcesUI > RectTransform`.
- Strip inset/placement: `resourcesUI/ResourceStrip > RectTransform`.
- Cell spacing/width: each cell's `RectTransform` horizontal anchors and Left/Right
  offsets. Default offsets produce a 4px gap between adjacent cells. No LayoutGroup
  is added; anchors/offsets, not a runtime layout script, control these positions.
  A manually added layout component remains user-owned and may drive its children.
- Background: cell Image Sprite/Color; icon: cell `Icon > Image` Sprite/Color and
  RectTransform; number typography: cell `Value > TextMeshProUGUI` Font Asset,
  Font Size, Auto Size, Alignment, Color and spacing. Existing label-plus-number
  content is still updated by `SetCurrency`; static typography is not rewritten.
- Binding review: `Canvas/SettlementHUD > Authored Persistent Resource Strip`.

One aggregate `createResourcePresentationIfMissing` policy defaults true for
uninstalled configurations. The installer disables it only after validating all
authored bindings. Valid authored cells bypass the compatibility builder entirely,
even while the flag is true. Partial authored strips are never replaced. With
fallback disabled, missing/duplicate/invalid references produce one actionable
warning per HUD instance, skip only affected cells, and leave other HUD/gameplay
systems running. The existing compatibility construction and combined-text fallback
remain temporarily; do not retire them before manual installation and reopen checks.

`PermanentProgress.Changed -> SettlementController.Changed -> SettlementHUD.Refresh`
remains the update path. HUD OnEnable now immediately refreshes permanent balances,
Start refreshes after scene initialization, and controller notifications refresh
when ready. HUD subscriptions are idempotent and cleanup detaches from the actual
subscribed controller, even if its serialized controller reference changes.
No polling, transaction, reward, quest, settings, story or save calls are used by
the installer. Resource updates do not reset authored geometry or styling.

`SettlementResourceHUDTests` uses supported PreviewScenes and temporary test-owned
PermanentProgress state. It covers repair/adoption/preservation, Undo/rollback,
mapping diagnostics, events/lifecycle, authored and compatibility paths. Its reload
test clones the fixture into a newly created inactive additive scene, saves only a
unique test-owned temporary asset, reopens it, and removes it; no user scene or save
is used. Tests are compiled, not yet executed. Unity installation, validation,
manual saving/reopening, Test Runner and 480x270 Play Mode verification remain gates.

Navigation, Curse/ship previews, facilities, trait trees, sector technology,
settings and transitions are outside this pass. The embedded navigation/technology
MonoBehaviours without matching files, shared trait status/message text and legacy
close/back button mappings remain later work. Restoration conditions, access-key
completion, ship development, purchases, mandatory Pixel Crushers conversations,
launch guards, input/pause/camera and ScreenFader authority are unchanged.

## Settlement Main Navigation Authoring (2026-09-06)

`SettlementUIController` remains navigation and panel-selection authority. The
navigation installer adopts `Canvas/SettlementHUD` as `navigationRoot`, including
the already authored `SettlementButton`, `AddButton`, and `OptionButton`. It adds
only missing `HangarNavigationButton`, `SectorTechnologyNavigationButton`,
`NavigationBackground`, and `SettlementStationHeader` presentation. Each primary
button binds its Background Image, Label TMP, NavigationIcon Image,
NavigationActiveStrip Image, and Outline through the controller's existing five
navigation view groups. Solid-color navigation icons/strips intentionally need no
sprite asset. Typed renamed assignments take precedence over canonical names;
competing mappings are errors, not permission to delete objects.

The fixed destinations are Hangar (`ShowMainPanel`), Settlement Restoration
(`ShowRepairPanel`), Ship Reinforcement (`ShowSectorTechnologyPanel`), Additional
Traits (`ShowTraitPanel`), and the Settings overlay (`ShowSettingsPanel`). Existing
Main/Repair_HUD/ShipTraitTreePanel destinations and the sector panel's runtime
initialization remain unchanged. The initial Main panel, pointer selection,
existing W/S/Space handling, EventSystem submit, cancel/settings behavior, audio,
dialogue modal restrictions, and launch guard remain owned by existing code.
No new input actions or displayed bindings are introduced. Existing Button
Navigation modes/links are preserved; new primary buttons use the compatibility
builder's Mode.None. Hardware navigation remains a required Unity integration check.

`VOID SCRAPPER > UI > Settlement > Install Navigation UI` requires the explicitly
loaded `Assets/01_Scenes/Settlement.unity` outside Play Mode. It reuses the existing
Canvas, EventSystem, controllers, panels and action controls. Missing nested
presentation/components/bindings are repaired through SerializedObject and Undo,
with target-scene object creation, temporarily inactive roots, restored activation,
limited rollback on failure, and dirty marking without saving. It never invokes
Awake/Start, gameplay panel builders, settings preferences, actions, or saves.
`Settlement > Validate Navigation UI` accumulates binding, mapping, ownership,
hierarchy, callback, font, navigation-link, MonoScript and 480x270 scaler errors.
Inactive panels/buttons are valid. Unknown missing scripts are reported by path,
never removed. PreviewScene entry points exist only for test-owned fixtures.

One aggregate `createNavigationPresentationIfMissing` setting retains uninstalled
compatibility. Installation disables it only after complete validation. Valid
authored bindings bypass BuildPersistentNavigation and all its base styling;
partially authored bindings never trigger replacement clones. Missing authored
presentation issues one actionable warning per controller and leaves existing
actions and unrelated gameplay operational. Initialization and owned button/event
subscriptions are idempotent, with cleanup against the actual subscribed objects.
Matching persistent controller actions suppress duplicate runtime subscriptions;
unrelated UnityEvents are never cleared. Trait unlock remains ShipTraitTreePanel-owned.

Inspector editing locations:

- Overall navigation remains directly under `Canvas/SettlementHUD`; adjust each
  button RectTransform for placement, size and spacing. No LayoutGroup is added;
  any manually installed LayoutGroup still controls its children normally.
- Edit `NavigationBackground` and `SettlementStationHeader` for decoration/header.
  Edit the bound button Label TMP for base typography and the NavigationIcon Image
  for sprite/tint; renamed assigned objects work identically.
- Edit selected background/label colors and selected icon alpha multiplier in the
  controller's five Authored Navigation view groups. Inactive colors are captured
  from the authored Graphics; selection switches from those baselines without
  accumulating scale, position or color changes. Selection drives strip/Outline
  enabled states, while Button transitions drive hover/pressed/disabled feedback.
- Existing LaunchButton, MainPanel/Player_Select/ship_choiceButton,
  Repair_HUD/UpgradeButton and BackButton, ShipTraitTreePanel/UnlockButton and
  backButton retain their RectTransforms and TMP styling. The six ordered
  `navigationControlLabels` bindings map those existing controls; number/order is
  validated. HUD ship-action and trait-unlock typography defaults are narrowly
  bypassed through `navigationPresentationOwner`; action text and interactability
  remain gameplay-driven. Panel contents and other text defaults are not migrated.

`SettlementPrimaryNavigationPointer` now has its own matching runtime file/meta;
the existing SettlementUIController GUID remains unchanged. The unrelated embedded
SettlementSectorTechnologyEntrySelection and legacy shared panel mappings remain
deferred. Static scene inspection found an unresolved script GUID
`38fef9b49173e9d498b97aaa02cf1d81` on inactive `Canvas/Temp_ClearButton`, outside
navigation ownership; it is reported for separate inspection, not removed.

Manual workflow: let Unity compile, open Settlement, run Install then Validate,
inspect authored layout at 480x270, save manually, reopen and validate again. Run
SettlementNavigationUIAuthoringTests plus SettlementResourceHUDTests,
DialogueLifecycleStabilityTests and existing mandatory-story/launch regressions.
Verify all five routes, back/cancel, one action per click/submit, repeated
enable/selection, dialogue blocking, resource-strip preservation, settings/resume,
and guarded expedition launch. Tests are compiled, not executed by .NET builds;
temporary save/reload coverage is not a claim that Unity reload was performed.

### Settlement navigation usability refinement (2026-09-06)

This refinement supersedes the earlier focus-driven active highlighting and raw
W/S polling. The saved scene had OptionButton inactive (28x22), and Restoration /
Additional Traits captions at font size 20 with autosizing disabled. The prior
installer correctly preserved those values but therefore preserved their overflow.
The cyan Outline was driven by primary focus, not currentPanel; a focused Traits
button while Restoration was open was not proof of stale panel state.

The existing installer now restores OptionButton activation/interactability,
deactivates the exact repairBackButton and traitBackButton objects with Undo,
removes links/default selections targeting them, and establishes panel boundary
links. Their legacy references remain; missing hidden Back labels are not required
presentation. A copied `SectorTechnologyPanel/SectorTechnologyBackButton` is hidden
only at that known construction path. Normally that control is runtime-created;
the installed path now omits its construction and links, while uninstalled
compatibility retains it. No sector cards, transactions, or panel content migration
is included. Inactive controls occupy no LayoutGroup slot; no new layout group is added.

`SettlementPrimaryNavigationPointer` receives EventSystem move/select/deselect
events. Primary Button Navigation.Mode.None intentionally prevents Selectable from
also moving focus. The existing UI Navigate action supplies W/S, arrows and
controller input, using InputSystemUIInputModule's existing repeat delay/rate.
There is no competing Settlement Update movement loop or second input map.
Movement clamps, skips hidden/disabled primary buttons, and never invokes actions.
Enter/controller Submit stays Button/EventSystem-owned. Space is a primary-only
updateSelected alias that consumes that dispatch before the input module can
submit again. Panel controls, lists, Settings and dialogue do not receive this alias.
Input Actions assets and binding overrides are not rewritten.

The accent strip and selected background/label/icon colors identify the active
content tab; the cyan Outline identifies actual primary focus. Focus alone no
longer changes active-tab styling. Mouse activation follows the existing callbacks.
Right from the sidebar enters the active panel's action control, or an available
fallback if locked/disabled. Left on its boundary controls returns to the sidebar;
existing explicit internal links are retained where possible. A fallback entry
gets a runtime Left return link, not new presentation. Primary movement and these
boundary links are intentionally navigation-driven, not Inspector styling resets.

Settings and ESC still use EscSettingsMenuController.Open. Its Opening event lets
SettlementUIController capture focus before the modal changes selection. Closing
keeps currentPanel and restores a still-valid prior target; hidden/removed targets
fall back to an available sidebar button. The same existing pause owner and cancel
stack are retained. Cancel is processed once per frame, before EventSystem UI
dispatch; rebinding (including its completion/cancel frame), expanded dropdowns
and screen confirmation are handled before closing the containing modal. Dialogue
and other pause owners prevent a new Settings open. No settings preferences are
applied by Editor installation.

Authored editing workflow (outside Play Mode, with Settlement loaded):

1. Use `VOID SCRAPPER > UI > Settlement > Install Navigation UI` only for missing
   objects/bindings. The old typography/layout reset command is retired.
2. Edit button RectTransforms and each navigation view's Label TMP directly in the
   Inspector. Existing positions, spacing, fonts, padding and styling are authoritative.
3. Run Validate Navigation UI, inspect at 480x270, save manually and reopen. Verify
   keyboard/controller navigation, focus, Settings/ESC, dialogue and launch guards.

Expanded tests include actual TMP mesh/bounds execution when run inside Unity,
input-action/event dispatch, active-tab versus focus, disabled/hidden skipping,
modal focus/Cancel, Back omission, and explicit-repair preservation. These tests
are compiled, not executed by the static build. No screenshot, rendered bounds,
Play Mode or scene reload verification is claimed by this delivery.

### Settlement Ship Reinforcement authoring and grouped UI tools (2026-09-06)

`SettlementUIController.ShowSectorTechnologyPanel` and
`SettlementSectorTechnologyPanelUI` retain panel/selection authority. The existing
catalog has five ordered stable IDs: `sector1_stabilized_frame`,
`sector1_reinforced_bulkhead`, `sector1_field_repair_lattice`,
`sector1_damage_calibrator`, `sector1_scrap_optimizer`. Inspection found no catalog
ScriptableObject, suitable card prefab, ScrollRect, scrollbar, or separate
requirement/status label. Costs and status remain Cost TMP and the upgrade caption.
The installer does not introduce additional gameplay fields or controls.

Default authored hierarchy (renamed typed bindings are valid):

```text
Canvas/SettlementHUD/SectorTechnologyPanel
  ContentBackground (Image)
  Title (TMP)
  SelectedTechnology (Image)
    Name / Level / Description / Effects / Cost (TMP)
  UpgradeButton (Button, Image, UISoundButton)
    Label (TMP)
  TechnologyCatalog (RectTransform)
    TechnologyCardTemplate (inactive; Button, Image, Outline,
                            UISoundButton, SettlementSectorTechnologyEntrySelection)
      Icon (Image)
        IconLabel (TMP)
      Name / Summary (TMP)
```

The existing entry-selection component now has a matching runtime file/meta and
typed template bindings. Original panel script GUID/field identities are retained.
The template has no persistent button actions and is excluded from live entry
counts/navigation. Runtime clones it once per catalog ID; it does not procedurally
build visual children. The catalog still supplies icon glyph/accent, name, level,
effects and costs. Icons are colored glyphs, not required sprite assets; an authored
optional Icon sprite is preserved and template tint multiplies the catalog accent.

Inspector: edit the panel and each background/title/detail/action RectTransform
for static layout; their Image/TMP components own art and typography. Catalog
RectTransform controls the list origin. Template RectTransform controls card size
and first position; the owner's `cardStep` controls subsequent offsets (0,-35 by
default), applied only during population. There is no LayoutGroup or scrolling
structure. Template Name/Summary/IconLabel TMP and Icon Image remain editable.
Refresh changes data, upgrade interactability and intentional selection only:
`selectedCardColor`, Outline visibility and existing Button transitions. It never
resets base typography, sizes, positions or normal card colors.

Valid authored bindings bypass BuildPanel/BuildDetailPanel and procedural card
children. A single `createPresentationIfMissing` policy remains true by default
for completely uninstalled configurations, and installation disables it only after
validation. Partial authored configurations never trigger replacement construction.
Missing bindings warn once with the installation route and skip the panel without
locking input/pause; failed opening leaves the previous content intact. Lost local
focus can return to the sidebar. No polling, preferences initialization or gameplay
transactions occur during installation.

Refresh subscribes to the actual `SettlementController.Changed` publisher,
including its startup notification after progress becomes ready; initialization
and re-enable refresh immediately. Disable unsubscribes that publisher, and owned
actions are idempotent and cleaned up. The catalog API is currently immutable, so
selection persists across opens without repopulation. When upgrade becomes
unavailable while focused, focus returns to its selected card. Card Left routes
through upgrade when available, otherwise directly to the sidebar. There is no
redundant Back on the authored path. Sidebar active/focus feedback, EventSystem
movement, Settings and dialogue isolation remain navigation-owned.

Transactions remain SettlementController.TryUpgradeSectorTechnology ->
PermanentProgress.TryUpgradeSectorTechnology -> existing save/message/event paths.
Costs remain 2/3/5 StabilizedAlloy and maximum level 3; no reward/accounting changes.

All authoring commands now live below `VOID SCRAPPER > UI`:

| Group | Leaf commands |
|---|---|
| Boot | Install Main Menu UI; Validate Main Menu UI |
| Tutorial | Install Guidance UI; Validate Guidance UI; Install Resource HUD; Validate Resource HUD |
| Settlement | Install Resource HUD; Validate Resource HUD; Install Navigation UI; Validate Navigation UI; Install Ship Reinforcement UI; Validate Ship Reinforcement UI |

No Install All exists. Existing tools retain their behavior; diagnostics and test
expectations now use grouped paths. With Settlement loaded outside Play Mode and
Navigation UI installed, run Settlement > Install Ship Reinforcement UI, inspect
the panel/template, then Validate Ship Reinforcement UI. The installer uses explicit
scene-owned creation, inactive binding, typed/renamed adoption, partial repair,
SerializedObject assignments, Undo and scoped rollback. It preserves styling and
prior activation except the template/recognized redundant Back must be inactive.
Ambiguous mappings, copied runtime catalog cards and unknown missing scripts are
reported without destructive guessing. Validation checks owners, nested mappings,
scene/hierarchy, fonts, icon bindings, MonoScript identity, navigation, 480x270
CanvasScaler, unique catalog IDs and fallback policy. Save manually, reopen and
validate; then verify all five entries, upgrades, resources, focus, Settings and
dialogue/launch restrictions at 480x270.

Fallback retirement audit: Boot's MainMenuController.BuildRuntimeView is already
absent (its existing test asserts this). Boot Editor/background builders and
SharedOptionsMenuUI.ConfigureForAuthoring remain required. SharedOptionsMenuUI.Build
also serves GameplayPauseMenuController and Settlement EscSettingsMenuController.
Tutorial serializes operation/resource fallback policies false, but Expedition
still enables operation construction and has null alloy/Tuning Chip counters with
the resource fallback default enabled. These shared builders cannot be retired yet.
Settlement BuildResourceStrip, BuildPersistentNavigation and Ship Reinforcement's
compatibility builders are later candidates after manual install/save/reload and
consuming-test verification. False resource/navigation flags alone do not retire
their uninstalled configurations/tests. No fallback builder, dynamic catalog
population, validator or authoring tool is deleted in this pass.

Verification: safe .NET runtime/Editor compilation only. Fourteen focused tests are
compiled, not executed: PreviewScene installation, partial repair, styles, Undo,
rollback, ownership, script identity and temporary inactive save/reload; catalog
mapping, initial/current balances, subscriptions, single-action upgrades, max/poor
states, focus, local failures, compatibility and menu paths. Tests disconnect
SaveManager and use test-owned progress, never user saves. Unity rendering, Test
Runner, Play Mode and production scene reload remain manual verification gates.

## Settlement Restoration authored presentation (2026-09-06)

Scope is only `Canvas/SettlementHUD/Repair_HUD`. It was already scene-authored,
not a runtime-generated currency-funded building-upgrade panel. `SettlementHUD`
owns its existing preview, indicator and TMP bindings; two serialized container
references (`repairDetailRoot`, `repairResultsRoot`) make role-scoped repair
unambiguous. `SettlementUIController` retains facility selection, panel navigation,
action dispatch, EventSystem focus and its existing balanced listener lifecycle.

Inspector locations (existing names remain valid, renamed typed bindings win):

- `Repair_HUD`: overall RectTransform position/size; existing background/decorations remain authored.
- `line/PreViewImage`: Image and RectTransform preview sizing/aspect; child
  `Arrow_Left (1)` / `Arrow_Right (1)`: previous/next button visuals and layout.
- `PreViewImage/sphere` (four existing objects): indicator Image tint, sprite,
  size and baseline scale. `SettlementHUD.repairIndicatorImages` explicitly maps
  Hangar, EngineWorkshop, WeaponLab, RecoveryProcessor in that order.
- `Bg`: detail container. `TitleText`, `1Text`, `LevelText (3)`,
  `CurEffectText`, `NextLevelText (1)`, `NextEffectText (4)` are respectively
  title, description, state heading, state value, requirements heading, requirements.
- `Cost/ScrapCostText`: restoration results, **not a currency price**.
  `Cost/Scrab` and `Cost/Core` retain serialized legacy references; only their
  Image rendering is disabled, without moving or disabling the results container.
- `UpgradeButton/Text (TMP)`: restoration action caption (same typed TMP binding
  in HUD and navigation control-label slot 2). Edit Button Image/RectTransform and
  TMP typography/padding here. The legacy name does not imply paid building upgrades.
- HUD `buildingPreviewSprites`: existing facility art, level 0 unavailable and
  level 1+ restored. Existing assignments and the Korean font/fallback chain survive.

No layout group is added. RectTransforms control positions and spacing. Runtime
updates localized strings, selected facility sprite/enabled state, indicator
visibility/tint/scale, and action interactability. Indicator color and scale use
captured Inspector baselines multiplied by existing Page Indicator Style values;
they never accumulate across selection/re-enable. Preview aspect and Restoration
font size/autosizing/padding/alignment are no longer reset. No Restoration
construction/fallback policy is introduced because the panel already existed.

Workflow: outside Play Mode, load `Assets/01_Scenes/Settlement.unity`, ensure the
persistent Navigation UI is installed, then run
`VOID SCRAPPER > UI > Settlement > Install Restoration UI` and
`VOID SCRAPPER > UI > Settlement > Validate Restoration UI`. Inspect at 480x270,
save manually, reopen and validate again. The installer uses explicit scene-owned
creation, inactive binding, SerializedObject/Undo, role-specific containers and
scoped rollback with original exception stacks. It preserves manual styling and
prior activation except the intentionally hidden Restoration Back control.
Ambiguous legacy spheres require explicit facility-order binding, never sorting
user art by position or deleting duplicates. No gameplay lifecycle, preferences,
transactions, story actions or saves run during authoring.

Missing authored bindings warn once and skip Restoration refresh/action locally;
no replacement hierarchy is built. Focus moves from an unavailable Restoration
action to valid facility navigation/sidebar, without moving behind Settings or
dialogue. Existing idempotent owned listeners, persistent-callback deduplication,
publisher cleanup and immediate re-enable refresh are retained. Access-key recovery
still requires explicit confirmation through the existing authority; ready and
completed states and repeat-confirmation protection are unchanged.

Removed only the uncalled Restoration cost-icon positioning/default-style helper
chain (both previous callers always requested hidden icons), and Restoration
runtime text defaults. Legacy serialized cost references and shared ship/resource/
navigation/Ship Reinforcement/Options compatibility paths remain. Resource balances,
restoration requirements/rewards, PermanentProgress, SettlementController,
SaveManager, Pixel Crushers, launch guards, input, camera and transitions are not
replaced. Other panels remain outside this migration.

## Settlement Additional Traits authoring (2026-09-07)

`Canvas/SettlementHUD/ShipTraitTreePanel` remains owned by ShipTraitTreePanel.
The panel, four category controls, four ScrollRects and selected-node details were
already scene-authored. Runtime previously reset TMP defaults and cost icon
positions, and rebuilt prefab-based entries on every enable. There was no procedural
static panel builder to replace. The existing `branchNodes` list and its
TraitDefinition/ReinforcementDefinition references remain the only catalog; no
trait data is duplicated into the authored template.

Installed presentation adds only explicit detail/cost/catalog container references,
a sidebar Button reference and an inactive `TraitEntryTemplate` on the existing
owner. The template is cloned in Edit Mode from the assigned
`Assets/03_Prefabs/ShipTraitNodeButton.prefab`, preserving its nested visuals and
repairing missing bindings/components locally. The prefab asset is never changed.
The original `nodeButtonPrefab` reference is retained for compatibility.

Inspector editing:

- `ShipTraitTreePanel` RectTransform: overall panel placement and dimensions.
- `LeftPanel`: detail background and container placement. `Image ` is selected
  trait art; `TitleText`, `DescriptionText`, `DescriptionText (1)`, `LevelText`
  are name, description, category and level. Edit TMP size, padding, wrapping and
  alignment directly. There is no separate effects/requirements widget: existing
  description/status content carries those supported meanings.
- `LeftPanel/Cost/ScrapCostText` and `Scrab` / `Core`: costs, currency icons and
  their authored positions. Runtime changes visibility, not their positions/sprites.
- `temp1`: status/ownership/lock or prerequisite state. A separate `MessageText`
  receives action feedback. The existing scene binds both statusText and messageText
  to temp1; this is rejected as a duplicate role. Keep statusText assigned and
  manually clear only messageText (or assign a distinct TMP), then install.
- `UnlockButton/Text (TMP)` and `UnButton/Text (TMP)`: purchase/upgrade and
  active/inactive toggle controls. Neither action is a second equipment system.
- `ShareButton`, `MucinButton`, `ShotgunButton`, `SniperButton`: category
  buttons in display order. Their existing line/color/alpha/vertical selection
  motion remains intentional; authored position/tint/alpha provide baselines.
- `Panel/<branch>/SharedScrollView/Viewport/Content`: each existing GridLayoutGroup
  controls entry positions and sizes (default cells 46x44, spacing 5x6, four columns);
  ContentSizeFitter controls dynamic height. Existing scrolling/layout settings
  remain editable and are not restyled on reinstallation.
- Inactive `TraitEntryTemplate`, outside catalog layout: edit icon placeholder,
  name/level TMP, lock/state/inactive/selection visuals and base alpha. Catalog
  icons/text replace placeholders at runtime. Template is excluded from entries and
  navigation; selection visuals are separate from Button focus tint.

Runtime clones the template only for missing catalog entries, binds before enabling,
and retains generated nodes across categories and re-enable. Removed catalog nodes
clean up only runtime-owned clones. Selection, costs, prerequisites, maximum levels,
active/inactive state, resources and save transactions retain existing owners.
Shared, MachineGun, Shotgun and Sniper filtering remains catalog/gate-driven.
Opening, filtering and authoring never purchase, activate, reward or save.

EventSystem owns navigation: Up/Down traverses available tabs, visible entries and
actions in deterministic order, clamped at ends; Left returns to the existing
Additional Traits sidebar and Right reaches an available action/sidebar. Filtered,
hidden and disabled controls are skipped. Invalid focus recovers without moving
behind dialogue/Settings/pause. The redundant Back stays inactive. Existing
ShipTraitTreePanel purchase/toggle methods still dispatch transactions. Owned
listeners are idempotent, recognize matching persistent callbacks and detach from
the actual publisher/Button. The controller-forwarded change event avoids a
duplicate direct PermanentProgress subscription.

Workflow: load Settlement outside Play Mode, ensure Navigation UI is installed,
resolve the statusText/messageText alias above, then use
`VOID SCRAPPER > UI > Settlement > Install Additional Traits UI` and
`VOID SCRAPPER > UI > Settlement > Validate Additional Traits UI`.
Installer uses explicit target-scene creation, inactive binding, SerializedObject,
Undo, scoped rollback and original exception stacks. Valid renamed/wrapped typed
references and manual styles survive. It rejects ambiguous names, duplicate roles/
IDs, wrong ancestry, cross-scene mappings and missing scripts/assets without
deleting user objects. Save manually, reopen and validate again.

Compatibility remains for uninstalled panels: ConfigureSettlementPresentation,
LayoutCostIcons and prefab RebuildGeneratedNodeButtons still serve the legacy path
using nodeButtonPrefab / autoGenerateNodeButtons / rebuildGeneratedNodesOnEnable.
Installed owners set rebuildGeneratedNodesOnEnable false and use the scene template.
No shared builders or gameplay code were deleted; retirement awaits manual
installation/reload and compatibility-test execution. Missing authored data warns
once, disables local actions and creates no replacement hierarchy. Static .NET
runtime/Editor/test compilation is not Unity Test Runner or visual verification.

## Verified runtime UI retirement (2026-09-07)

This entry supersedes earlier compatibility-retention notes for the Settlement
resource strip, primary navigation and Ship Reinforcement only. Saved fileIDs were
traced to GameObjects, parents, nested Image/TMP parts, font/icon assets and the
inactive technology template. Script GUID searches across scenes/prefabs/assets
found Settlement as these owners' only production consumer. Lifecycle, source,
reflection-test and UnityEvent searches found no external builder consumers.
This is static serialized evidence, not executed Unity scene validation.

| Presentation | Decision | Remaining dependency |
|---|---|---|
| Boot menu/background | Already retired; no additional deletion | MainMenuController uses BootMainMenuView. Background construction is Editor-only and needed by the installer; animation stays runtime. |
| Tutorial prompt | No static builder to retire | TutorialPromptUI text refresh and TutorialFlowController progression/dynamic targets remain. |
| Tutorial operation/resources | Retain shared builders | ExpeditionHUD is serialized in Tutorial and Expedition. Tutorial disables these fallbacks; Expedition still uses operation and resource compatibility. |
| Settlement resources | Removed BuildResourceStrip/CreateResourceChip and legacy combined-text fallback | Canvas/resourcesUI/ResourceStrip has all three complete cells. PermanentProgress/controller events and amount refresh remain. |
| Settlement navigation | Removed BuildPersistentNavigation and background/header/button/icon/strip/default-style helpers | Five complete serialized navigation views; authored-baseline selection/focus feedback, EventSystem, modal restrictions, audio and actions remain. |
| Restoration | Already authored/runtime-safe; no additional deletion | Repair_HUD presentation refresh and restoration/access-key transactions remain. |
| Ship Reinforcement | Removed BuildPanel/BuildDetailPanel/BuildTechnologyCards and construction/style helpers | Complete SectorTechnologyPanel and inactive TechnologyCardTemplate; PopulateAuthoredCards, catalog selection, upgrades and subscriptions remain. |
| Additional Traits | Defer | Saved authoredDetailRoot/authoredCostRoot/authoredCatalogRoot/backgrounds/authoredNodeTemplate are null; rebuildGeneratedNodesOnEnable is 1. ConfigureSettlementPresentation, LayoutCostIcons, prefab generation and nodeButtonPrefab remain necessary. |
| Shared Settings/Pause | Retain | SharedOptionsMenuUI.Build still serves GameplayPauseMenuController. Boot uses authored Options; Editor helpers remain. |
| Settlement ship preview | Outside retirement scope | BuildCursePreviewLayers, ship typography and preview animation have not been migrated. |

The three retired Settlement `create*IfMissing` fields remain serialized migration
markers, not runtime permissions. Production data, installers, validators and Undo
fixtures still reference them; names/defaults and MonoScript GUIDs are preserved.
Installers still set them false, but true no longer permits construction/restyling.
Technology Initialize prototype parameters remain source-compatible and are never
cloned. Editor default colors and legacy resource icons/fonts remain installer inputs.

Missing required bindings warn once with component, property, actual hierarchy/scene,
expected container and Install/Validate menus. Only the affected presentation is
skipped; no replacement UI or transaction occurs. Optional trait CanvasGroups remain
optional. No service, catalog, input owner or polling loop was added.

Inspector locations are unchanged: resource strip RectTransform/HorizontalLayoutGroup
and cell Image/icon/Value TMP; navigation RectTransforms/Images/labels; reinforcement
panel/detail TMP, inactive card template and cardStep. Layout-group placement, dynamic
catalog arrangement and intentional selection/focus feedback remain runtime-driven.

Outside Play Mode, run the existing UI > Boot/Tutorial/Settlement validators. Repair
only the reported feature with its matching installer; inspect, save manually, reopen
and revalidate. Additional Traits still needs installation and saving before retirement
can be reconsidered. Run focused authoring and existing gameplay regressions, then
Boot -> Tutorial -> Settlement -> Expedition and return: Settings/ESC/dialogue,
resources, panel re-entry, single transactions and authored styling at 480x270.

## Settlement static presentation completion — 2026-09-07

This supersedes the preceding Additional Traits deferral and Hangar exclusion.
The saved Settlement scene now binds Additional Traits' LeftPanel, Cost, catalog
Panel, four category/content roles and inactive TraitEntryTemplate; forced rebuilding
is disabled. User-reported five-validator success is separate from this pass's
read-only serialized inspection and static compilation.

| Group / runtime entry | Ownership and final construction boundary |
|---|---|
| Resource strip / SettlementHUD.SetCurrency | Canvas/resourcesUI/ResourceStrip is authored; permanent balances and controller events remain. Earlier strip builder retirement is preserved. |
| Navigation / SettlementUIController.InitializeNavigationPresentation | Authored five-button sidebar, heading, selection/focus visuals and existing action controls; EventSystem and modal ownership remain. No static builder. |
| Restoration / RefreshRepairPanel, SetRestorationDetail | Repair_HUD preview/details/action are authored; requirements, results, access-key confirmation and art/state refresh remain authoritative. Hidden Back/cost graphics remain hidden. |
| Ship Reinforcement / Initialize, PopulateAuthoredCards | Static panel plus inactive TechnologyCardTemplate are authored. Catalog ID mapping, template cloning, cardStep arrangement, selection and upgrades remain. |
| Additional Traits / Awake, OnEnable, RefreshPanel | Only authored bindings and TraitEntryTemplate clones are used. Removed ConfigureSettlementPresentation, cost-icon layout defaults and prefab-rebuild chain. Category filtering, catalog synchronization, optional CanvasGroups, actions and subscriptions remain. |
| Hangar / SettlementHUD.RefreshShipPreview | MainPanel preview/arrows/indicators, Player_Select text/action, MessageText and LaunchButton are adopted. Four fixed curse layers are installed in Edit Mode. Runtime only changes content, visibility, weapon tint and baseline-relative animation/indicator feedback. |
| Settings / EscSettingsMenuController.Awake, Open | Authored SettlementSharedOptionsModal/OptionsPanel is required. Removed Settlement's runtime wrapper builder. Existing shared Options components own settings, confirmation, rebinding and return-to-menu. |
| Notifications / EventTitleDirector | Shared, data-triggered MotionTitleView prefab instances and animation remain; they are not Settlement-only fixed UI. |
| Cursor, dialogue, transitions | Existing shared MouseCursor, Pixel Crushers and ScreenFader owners remain unchanged. No new static Settlement-only confirmation/notification system was found. |

New commands (no Install All):
- VOID SCRAPPER > UI > Settlement > Install Hangar UI
- VOID SCRAPPER > UI > Settlement > Validate Hangar UI
- VOID SCRAPPER > UI > Settlement > Install Settings UI
- VOID SCRAPPER > UI > Settlement > Validate Settings UI

Both installers require the loaded Settlement scene outside Play Mode, deactivate
scene roots while binding, preserve valid typed renamed/wrapped references, reject
ambiguous/wrong-scene/wrong-ancestry roles, use Undo and scoped rollback, and never
save. Missing parts are repaired without resetting existing styles. Settings uses
a disposable supported PreviewScene as a reference for existing SharedOptions
authoring defaults; its local reconciler copies only new components/layout or missing
references. No runtime lifecycle, preference application, transaction or save API is
invoked. The TMP authoring dropdown helper creates directly in the specified scene;
the Unity package's active-scene dropdown factory remains on the shared runtime path.

Inspector editing:
- Canvas/SettlementHUD/MainPanel/PreViewImage: normal ship art container/layout.
  Its CurseGhost, CurseEdge, CurseBase, WeaponAccent children expose RectTransforms
  and Images; SettlementHUD > Authored Hangar Curse Layers holds their references.
- MainPanel/Player_Select/Titletext, ExplainText and ship_choiceButton child TMP:
  title/body/action typography. Existing saved sizes (16/10/15) are preserved,
  not reset to the old runtime defaults (11/7/7.5). Inspect bounds at 480x270.
- PreViewImage's existing indicator Images: base tint/scale; HUD page-indicator
  multipliers intentionally drive selection relative to those baselines.
- Canvas/SettlementHUD/LaunchButton and MessageText: authored shared launch/message
  geometry and TMP styling; only text and availability change.
- Canvas/EscSettingsRoot/SettlementSharedOptionsModal: backdrop Image/RectTransform.
  OptionsPanel and its four tab containers expose all static settings controls,
  TMP labels and dropdown templates. Legacy SettingPanel and service references
  remain preserved but presentation-disabled.
- Curse ghost motion is authored origin plus (6,-4), then origin; ghost/edge pulses
  scale authored alpha. Weapon hue intentionally follows ship type, preserving
  the authored accent baseline. No new Hangar layout group is installed.
- Traits GridLayoutGroups/ScrollRects and reinforcement cardStep still arrange
  dynamic entries. Navigation selection/focus and standard Selectable feedback remain
  runtime-driven; base typography/layout is not reset.

SettlementSettingsPanel and SettingsMenuTabController expose read-only shared-layout
binding checks. Rebind rows and dropdown guard arrays now serialize, preserving
nested Cancel ownership after reload. EscSettingsMenuController validates before
Opening/audio/pause/cursor acquisition, reports once with property/path/scene/menu,
and skips Settings when incomplete. Its event cleanup follows the actual subscribed
Options publisher and owned buttons; persistent matching actions are not duplicated.
Other optional legacy settings controls are not made mandatory.

Compatibility retained: shared Options Build and runtime dropdown construction for
GameplayPauseMenuController; Boot authoring helpers; ExpeditionHUD operation/resource
fallbacks for Expedition; Editor template-prefab source for Additional Traits.
Legacy serialized generation/configuration fields remain for scene data, installers
and Undo fixtures but no longer authorize Settlement runtime construction. No owner
or MonoScript GUID was removed. The existing inactive Canvas/Temp_ClearButton still
references absent DebugSettlementDataButtons GUID 38fef9b49173e9d498b97aaa02cf1d81;
this unrelated pre-existing debug component was not removed or repaired.

Before the next Play session: run Install/Validate Hangar UI and Install/Validate
Settings UI, then the existing Resource HUD, Navigation UI, Restoration UI,
Ship Reinforcement UI and Additional Traits UI validators. Inspect manually, save,
reopen and validate again. Execute focused authoring/regression tests in Unity,
then verify first/repeated Settings open, ESC/nested rebinding/confirmation, return
focus, all panels, single actions and Boot/Tutorial/Settlement/Expedition scene loops.
Compilation does not establish Unity Test Runner, production validation, reload,
Play Mode or visual success.

## Expedition scene-owned static presentation (2026-09-07)

The saved Expedition scene was inspected, not edited. It contains authored HP,
dash, cargo, three resource rows, aggregate boss health, shop/maintenance controls,
return/wormhole confirmation controls and WarningMessage. Operation, two additional
resource rows, heat/hints, radar scope and triad health still needed installation.
A complete ReinforcementSlotUI already exists beneath Canvas_ExpeditionHUD despite
the null ExpeditionHUD reference: the Status installer adopts that typed component.

| Path/owner | Classification and decision |
|---|---|
| ExpeditionHUD operation/resources | Fixed scene presentation; runtime operation construction and two extra counter clones retired. Tutorial already has authored operation/resources. Both owners retain serialized migration markers for existing installers/assets. |
| ExpeditionHUD HP/dash/heat/reinforcement/cargo/core/hints | Existing fixed controls adopted, missing presentation repaired by Status installer. Installed flags bypass shared builders, typography/layout polish and font replacement. Shared construction remains for Tutorial's null heat/cargo/hint/reinforcement bindings. |
| RadarHUD scope | Fixed scene graphic authored by Radar installer; RadarScopeGraphic moved to its own matching script/meta. Shared scope builder remains for Tutorial. Contacts/player markers and their state-driven graphics remain dynamic. |
| BossHealthBarUI | Existing aggregate bar retained. Fixed Left/Center/Right triad bars now serialized and authored. Runtime only binds parts, updates ratios/colors/text, hides/reuses the triad, and applies/restores intentional encounter layout. |
| ReturnChoiceUI / WormholeChoiceUI | Existing controls adopted; fixed dimmer/canvas/layout builder retired. Separate installers bind modal presentation without opening it; missing data returns before acquiring pause/input. |
| WarningMessageUI | Existing fixed text/fade group adopted; runtime typography/size reset and CanvasGroup creation retired. Communication content, priority, suppression and fades remain. |
| ShopTradeUI / ShopMaintenanceBayUI | Saved fixed controls already bound; runtime typography/size helpers retired without replacing hierarchy. Optional legacy caption aliases remain supported. Stock selection, purchases, maintenance slots and transient drag icon stay runtime-owned. |
| PlayerBuildStatusPanelUI / PF_ExpeditionMapInventoryMenu | Authored inventory shell and passive slot template retained; variable slots/content remain dynamic. Shared ExpeditionMapPanelUI still augments null viewport/content/legend/info/hint bindings in this prefab and Tutorial. Shared map construction is not falsely classified as a complete prefab or deleted. |
| InteractionPromptUI / charging / world gauges / damage | Gameplay-lifetime world-space/transient presentation retained. No changes to pooling, interaction ownership or camera anchors. |
| RunLevelTraitSelectionUI / pause / Options / letterbox / ScreenFader / dialogue | Shared with Tutorial/Boot/persistent services; retained, including shared reward typography and shared Options construction. Pixel Crushers remains the only dialogue runtime. |

Manual workflow, outside Play Mode, with Assets/01_Scenes/Expedition.unity loaded:
under `VOID SCRAPPER > UI > Expedition`, separately run `Install Operation UI`,
`Install Resource HUD`, `Install Status UI`, `Install Radar UI`,
`Install Boss Status UI`, `Install Return Confirmation UI`,
`Install Wormhole Confirmation UI`, and `Install Message UI`.
Run the corresponding `Validate ...` commands and `Validate Shop UI`.
Installers use explicit scene ownership, inactive binding, SerializedObject,
Undo/scoped rollback and dirty marking; they do not save or initialize gameplay.
Keep each operation separate. Correct ambiguous bindings manually; no scene-wide
first-caption fallback is used. Save manually, reopen, and validate again.

Inspector editing: follow ExpeditionHUD's serialized references on
Canvas_ExpeditionHUD. Edit OperationStatus root position/size, Title/Detail TMP,
Accent/background Images; ResourceRoot rows' iconImage/amountText and the HUD's
resourceCounterRowSpacing; existing StatusRoot HP/dash/cargo and newly authored
heat/hints, and the adopted ReinforcementSlotUI's Images/text. No new layout group
is imposed. Zero-resource compaction intentionally drives row Y positions; armor
drives horizontal anchors; gauge fill, state colors, equipment sprites, cargo fades
and operation slide/fade remain runtime-driven. Operation animation restores its
authored position. BossHealthBarRoot/SalvageDevourerTriadHealth supplies three bars,
labels and BossName; fill height uses a cached authored baseline. The encounter
override still temporarily moves the aggregate bar. RadarScopeGraphic Inspector
owns scope colors/rings/widths. Return/wormhole panels own their RectTransforms,
text/button styling and input-blocking dimmer. WarningMessage owns messageText
typography/layout; Settings warning opacity and fade remain intentional.

Verification here is static compilation plus source/GUID/serialized inspection,
not executed Unity tests or production validator results. New PreviewScene tests
cover operation/status/radar/boss/confirmation installation, common-name isolation,
partial repair, styling, rollback/Undo, local missing-binding behavior and a
test-owned save/reload fixture. Existing resource lifecycle tests now expect authored
rows, not a removed fallback. Test Runner, 480x270 visuals, input/modal regression,
boss variants and Expedition -> Settlement -> Expedition loops remain manual.
The existing Settlement Temp_ClearButton missing-script debt is untouched.

### Expedition explicit HP / armor / cargo layout repair

The earlier opt-in HP/armor/cargo reset command is retired. The following numbers
are historical design evidence only, not validation requirements or runtime defaults.
Use Install Status UI only to complete missing bindings, then Validate Status UI.
Edit existing presentation directly in the Inspector; no tool resets it or saves it.

Recovered defaults are the old 96x20 HP and 104x22 cargo panels, 5.5/6.5 TMP sizes,
one-pixel Outline borders and 5/6-pixel bar insets. At 480x270, HP starts at (8,1)
from the top left; the one-pixel armor strip occupies y=22..23, before the existing
heat bar. Cargo starts at (8,54), below the existing dash/status row. These are
reference-coordinate bounds, not cropped-screenshot measurements. They still need
rendered acceptance testing with the user's reference.

Inspector editing: use `ExpeditionHUD.hpGauge` and `cargoRoot` to locate the named
panels (including trailing spaces). Edit their RectTransforms for outer placement;
`hpLabelText`, `hpValueText`, `cargoLabelText`, `cargoValueText` point to TMP typography
and padding. `hpTrackImage`/`cargoTrackImage` and Slider `Fill Rect`'s parent define
bar insets. Frame Outline components define border thickness/color. Existing visual
wrappers are retained. `armorTrackImage` is the separate background; `armorFillRect`
retains its existing parent and horizontal ratio authority. No LayoutGroup is added.
Sprites, materials, fonts/fallbacks, localization components and current values remain
unchanged. HP/cargo number formats gain spaces around `/`.

Intentional runtime writes remain: HP/armor values and horizontal fill ratios,
armor visibility/bonus, HP protection-state tint, cargo fullness colors and alpha.
Armor's cyan `armorFillColor` is an authored baseline. These are not new animations
or gameplay rules. The repair never samples gameplay state or executes transactions.
Status menu validation checks mappings, ownership, reference-resolution text/panel
bounds and overlap, not exact coordinates or a mandatory font size. It evaluates
anchors without resizing/activating the Canvas; this is not rendered verification.

### Tutorial shared Status authoring and final fixed-builder retirement

This migration supersedes the earlier temporary Tutorial status/scope fallback
retention notes. Saved GUID searches identify only Tutorial and Expedition as
`ExpeditionHUD` / `RadarHUD` consumers, with no prefab or ScriptableObject consumers
and no runtime AddComponent owner creation. Expedition's saved status/scope bindings
are authored. Tutorial still needs the following manual installation before Play Mode.

Menus: `VOID SCRAPPER > UI > Tutorial > Install Status UI`
and `Validate Status UI`. They use the existing Editor-only HUD and radar helpers
with explicit Tutorial scene checks; no runtime factory or second HUD owner is added.
Installation adopts HP/armor and repairs cargo, heat, reinforcement, binding hints
and the fixed RadarScopeGraphic, using one outer Undo/rollback transaction. Nested
radar failure rolls back the Status changes too. Existing activation, fonts, sprites,
materials and styling are preserved. Layout-reset commands are retired.
Tutorial has no objective root/director/core tracker, so no Expedition-only objective
panel is created. An explicitly assigned tracker/root is supported if present later.
ResourceRoot, TutorialPrompt, OperationStatus and TutorialFlowController are untouched.

The saved Tutorial HP GaugeBarUI still points at the shared StatusRoot CanvasGroup.
Installation deliberately rejects that invalid ancestor without clearing a user
assignment. In the HP GaugeBarUI Inspector, change `canvasGroup` to None (optional)
or a valid gauge-owned visual group, then run Install Status UI. Do not change the
shared StatusRoot group or rename the gauge. Cross-scene, sibling and ambiguous
assignments also fail with field/path/scene diagnostics and original exception stacks.
Cargo has its own required fade group; local gauge group settings are preserved.

Historical geometry (no longer reapplied by tooling): HP 96x20 at (8,1)
top-left, armor at y=22..23, cargo 104x22 at (8,54), 5.5/6.5 single-line typography.
Inspector editing remains on the typed HP/cargo roots, label/value TMP references,
track/fill Images, border Outlines and armorTrackImage. Radar's fixed frame/rings are
edited on RadarScopeGraphic. No LayoutGroup or runtime reset switch is added.

| Shared path | Decision and current responsibility |
|---|---|
| Heat/reinforcement fixed child builders; HP/cargo polish; cargo root builder | Removed; Editor tooling supplies static objects and bindings. |
| Core tracking text/pip layout and fixed hint/icon builders | Removed; optional tracker content and Input System binding text still refresh at runtime. |
| Shared layout, dash base styling, armor-label positioning, font default writes | Removed; Inspector layout survives initialization/re-entry. Armor x anchors still encode the existing combined-health ratio. |
| Radar frame/scope creation and repeated Configure call | Removed; authored graphic retains scan-alpha feedback. |
| World charge gauges and their image helper | Retained: gameplay-following world-space presentation in Tutorial and Expedition. |
| Radar contact/player-marker cloning, map/inventory templates, resources, options/pause | Retained: data-driven or separately shared presentation outside this fixed migration. |

Legacy serialized construction flags remain only as inert installer/validation
markers because saved assets and Undo fixtures still reference them. They cannot
reenable fixed runtime construction. Missing bindings warn once with the owning
component, hierarchy, scene and Tutorial/Expedition menu, skipping only that visual.
Tutorial radar availability, curse/status visibility, dialogue/checkpoints/routes,
input locks and all transactions remain under their original authorities.

Verification requires manual Install when bindings are missing, Validate, scene save/reload and Unity
tests. PreviewScene tests cover adoption/partial repair, inactive state and flow-data
preservation, strict group rejection, late radar rollback, Undo, shared layout,
dynamic contacts and temporary serialization. Static compilation is not executed
Unity tests, production validation, rendered Korean bounds or a tutorial playthrough.

### Weapon heat reference presentation repair (Tutorial and Expedition)

`WeaponHeatUI` remains a reader of `PlayerWeaponController.WeaponEquipped` and
`MachineGunWeapon.HeatChanged`; heat, cooling, overheat recovery, firing locks and
Tutorial visibility remain unchanged. The saved sprite-less Filled Images cannot
render partial fills: UGUI Image uses its plain quad path without a sprite. The
former explicit repair restored the committed display-only Slider contract instead of
introducing another fill updater. Only the leaf fill anchors move; panel and track
geometry remain authored and stationary.

The Tutorial and Expedition heat-layout reset commands are retired. The latest
committed shared layout overrides the original 91x7 builder with a 92x4 rectangle,
center (-186,110), top-left (8,23) at 480x270. Its one-pixel inset is 90x2. The root
retains the historical dark blue background; the darker inset track is a small
new visual judgment consistent with cargo, not a recovered screenshot value.
There is no heat caption or percentage: current runtime intentionally hides text.
Existing cyan/amber/red state colors, 0.65/0.85 thresholds and zero-heat hold/fade
remain authoritative, with no newly introduced heat states.

Inspector editing: under each gameplay Canvas's `StatusRoot/WeaponHeatStatus`,
edit the root RectTransform for position/size and `WeaponHeatUI.backgroundImage`
for the surrounding panel. Edit `trackImage` (normally `Fill Area`) for inset
geometry and empty-track color. `fillImage` and `GaugeBarUI.fillImage` reference
the same leaf Image under that track; Slider.fillRect drives only its horizontal
anchors. The Slider is non-interactable, has no navigation, transition or handle.
Edit state colors on WeaponHeatUI; runtime owns fill color/amount and local fade
alpha. Ordinary installation never reapplies these layout defaults.

The reset tool is retired. Install retains scoped Undo/rollback and preserves
adopted presentation without saving. Validate Status UI checks
fill mapping, pointer/navigation exclusion, ownership, reference bounds and
overlap, without enforcing fixed sizes or colors on subsequent manual edits.
Manually inspect both scenes at 480x270, save/reload, and exercise heating,
overheat, cooling, weapon changes, tutorial visibility and scene re-entry.

Tutorial heat placement now checks meaningful prompt Graphics in common 480x270
bottom-left screen coordinates. The saved prompt is a visible 360x48 panel at
(60,208), not a fullscreen input root; it overlaps the (8,243,92,4) heat baseline.
The former Tutorial repair searched for a clear position (historically (8,76));
that automatic repositioning is retired. Both scenes retain the user's saved
placement. Empty/transparent logical roots do not count as visible overlap; inactive
authored Graphics are inspected as if shown. Empty dynamic TMP strings have no
current glyphs and generate a manual populated-stage check warning. Overlay and
camera UI are projected to common screen coordinates; unprovable projections
warn without inventing overlap. Reports list contributing Graphics and canvases.
The prompt, canvas configuration, stage visibility and runtime logic are unchanged.
