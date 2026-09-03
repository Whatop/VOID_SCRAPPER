# VOID SCRAPPER System Design

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
`MainDamagedAccessKeyQuestState`. `NotStarted` plays the six-line analysis. The five
repeat branches report `Active0`, `Active1`, `Active2`, `ReadyToRestore`, or
`Completed`; they contain no gameplay action. `DialogueStoryEntryPoint` requests
the typed Settlement completion transaction after the genuine terminal marker and
conversation-end event. An interruption cannot partially start the quest.

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
open through `VOID SCRAPPER > UI > Install Boot Main Menu UI`. The command never saves
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
alpha, font sizes, sprites, and animation values. `Validate Boot Main Menu UI` performs
read-only checks for authority duplicates, 480x270 Canvas configuration, bindings,
navigation, fonts, transition ownership, active hierarchy, and reference-safe bounds.
The background binds thirty authored stars, seven asteroid visuals, and one Curse
passer through typed serialized references. Reinstallation repairs null/missing slots
and the `BootMainMenuView.spaceBackground` binding without replacing valid or renamed
referenced objects.
