# Route Core Deck World Authoring

## 2026-09-28 - Final authored HUD layout

Run `HudHangarAuthoring.Run` only from saved scene/prefab state. This narrow pass does not rebuild the Deck or call the combat/shader authoring passes. Existing object IDs, GaugeBarUI bindings, numeric HP text, PlayerChargeGaugeUI follower, encounter references and the Return Button's persistent LeaveDeck callback are preserved.

At 480x270, CombatStatus is 112x58 centered at (-164, 88), leaving a 20-pixel left inset and 18-pixel top inset. The existing HP group has a 96-pixel red bar and HP current/max text; Armor is a thin cyan strip and continues to hide through its existing MaxArmor/event rule. WeaponHeat keeps its existing heat events in the bottom of this card. Only the Deck has hideAtZeroHeat=false and the optional HEAT area label; Shotgun and Sniper hide the entire heat group. Expedition's default hide policy is unchanged.

The original DefenseObjective TMP binding is reparented into a 196x30 DarkUI `32` panel at (0, 99), below the world rail. It is still written by SettlementHUD/SettlementDefenseEncounterController. RouteCoreInteraction remains separate at (0, -91). PurpleRadar, binding-aware InputHint at (183, 43), scanner rules and player-relative charge at the original 1.1-world-unit offset are untouched.

ReturnToFacilities is 96x18 at (-172, -108), using the approved `Button_Small_A` sliced role at PPU multiplier 8. The legacy sprite-state overrides are cleared; normal is dark neutral, hover/navigation focus uses SettlementSelectionColors.HoverBackground, and press/selection feedback uses SelectedBackground. There is no white elliptical focus sprite. The existing encounter owner hides Return during defense and restores its exact prior active state on cleanup.

No Orange/Blue/Green pattern, component HP, projectile damage, Purple HP/exposure/radar, shader transition, fusion, completion or save behaviour changed. Final verification and native screenshots: [HUD_HANGAR_PRESENTATION_REPORT.md](HUD_HANGAR_PRESENTATION_REPORT.md).

The final disposable Play Mode probe passed 126 assertions and produced 36 native 480x270 captures across Deck and Hangar. All 25 new layout/projection tests pass. Full/focused suite exceptions and the pre-existing missing Hangar sprite are recorded in the report; no baseline failure is hidden by this authoring pass.

## 2026-09-28 - Purple corruption shader overlay

This presentation-only layer supersedes the earlier no-shader note. Unity 6000.0.69f1 / URP and Shader Graph 17.0.4 continue to use the existing Renderer2D. Its existing Watercolor/Flow features, lighting, pipeline settings and global shader stripping are untouched. The original core uses Sprite-Lit-Default; the optional overlay uses **URP Sprite Unlit**, so the signal stays legible during the existing sprite-color blackout.

Assets:

- `Assets/02_Scripts/VFX/RouteCore/SG_RouteCoreCorruption.shadergraph`
- `Assets/02_Scripts/VFX/RouteCore/MAT_RouteCoreCorruption.mat`
- `RouteCoreCorruptionVisual.cs` controls one saved `RouteCoreDeck/PurpleCorruptionTarget/CorruptionOverlay` SpriteRenderer. The original `CorruptionVisual`, material, sprite and gameplay colliders remain intact. The overlay reuses the original sprite/transform, sits one sorting order above it, and has no collider or gameplay component.

The graph samples the sprite without UV displacement. A single embedded Custom Function quantizes UV noise into 12 blocks per axis and changes its sparse pattern at 3 Hz. Hard signal cuts and a restrained four-level pulse preserve the silhouette without blur, bloom or chromatic offsets. No second clean-core shader or scan-wave shader was added.

| Property | Meaning / saved default |
|---|---|
| `_MainTex` | SpriteRenderer-supplied original sprite texture |
| `_BaseColor` | White tint; alpha controls purification opacity |
| `_CorruptionColor` | Purple (0.8, 0.3, 1), briefly cyan during acquisition |
| `_CorruptionAmount` | 1 hidden/purifying; 0.35 exposed, controls signal-cut strength |
| `_Reveal` | 0 sparse fragments; 1 exposed silhouette |
| `_NoiseScale` / `_NoiseSpeed` | 12 blocks / 3 pattern steps per second |
| `_GlitchStrength` / `_PulseStrength` | 0.12 sparse-cut attenuation / 0.08 energy pulse |

Hidden begins immediately with faint, sparse fragments above the existing near-invisible base. Only a successful active scan drives the 0.20-second cyan-to-purple acquisition tween. Gameplay damage eligibility is immediate; the original four-second exposure coroutine is unchanged. Exposure keeps the full base silhouette and adds restrained purple energy. Timeout blocks damage immediately and fragments the overlay over 0.20 seconds. Rescans while exposed remain ignored. Environment visibility stays exactly 25% hidden / 45% exposed.

Purple death clears combat, radar, bullets and blackout and invokes the original completion/save authority **before** visual purification. The clean central core is restored while the overlay dissolves for 0.45 seconds. An independent encounter-owned timer then performs the existing automatic return to facilities; no new input lock or save gate is introduced. Leaving, disabling or scene exit cancels that timer. Missing/unsupported polish returns immediately, and exceptions in this optional visual path cannot strand the player after completion. The clean core retains its existing stable presentation.

`RouteCoreCorruptionVisual` caches shader IDs and reuses a MaterialPropertyBlock plus a baseline block; it never accesses `Renderer.material` or mutates shared materials. State transitions own short DOTweens, use SetLink, and kill/restore exact baseline properties on cancellation/disable/retry. Shader Time handles low-frequency noise and pulse, with no C# Update. Missing material/shader/overlay falls back to the existing sprite and warns once in Editor/Development builds. No runtime material or hierarchy fallback exists.

Saved authoring: `RouteCoreShaderAuthoring.Run` imports/validates the graph, creates the material if absent, and wires only the Purple overlay in the saved scene. `RouteCoreCombatAuthoring.Apply` reapplies the same role without duplicating renderers. Serialized material references pull the graph into normal scene dependencies; no Always Included or URP Global Settings changes are needed.

Preserved tuning: Orange 64-degree cone / 5 pellets / 2 volleys, Blue 3 shots, Green 10 shots; component HP 70/70/90; projectile damage 2; Purple 50 HP / 4-second exposure / ignored exposed rescan / 8-projectile pulse. No campaign/save schema, reward, boss, normal enemy/base, equipment/economy or DarkUI changes.

Shader validation results:

- Runtime and Editor/test compilation passed. Shader Graph import and actual Direct3D11 compilation/rendering passed on an NVIDIA GeForce RTX 4070 Ti. Material/scene dependency tests confirm the graph is included through serialized references. No standalone player build or other graphics API was exercised.
- Focused EditMode **85/85**. Final full EditMode **971/974**, including all **8 new shader cases**. The three `AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)` failure names and messages exactly match `Logs/RouteCoreCombat/full.xml`; no new test failures. Evidence: `Logs/RouteCoreShader/focused.xml`, `full.xml`, `test-comparison.txt`.
- Actual Play Mode **84 assertions**, **19 native 480x270 captures plus one 960x540 capture** in `Logs/RouteCoreShader/Rendered/index.html`. Inspected transition/hidden, Radar closed/open, acquisition (including the existing scan wave near the core), exposure, eight-projectile pressure, re-hide, second hidden state, purification and clean central core. No sampled TMP overflow, missing glyph, shader error or new edge halo was found. Fixed UV blocks avoid subpixel displacement; exposed targeting and player/projectile silhouettes remain readable. Static captures/sample transitions do not establish all-display temporal comfort.
- Real Q/Mouse 4 input, hidden bullet rejection, exposed damage, ignored exposed rescan and timeout passed. Observed first exposure was **4.000 seconds**, with the serialized duration still exactly 4. Two actual player-death/full-retry paths include a lethal interruption during the acquisition tween. Disabling the visual component during purification kills its tween while the independent return timer still completes. Completion is exact-once and occurs before the visual tail; FinalNetwork eligibility and camera release remain valid.
- Lifecycle review caught and fixed a presentation-tail hazard: re-enabling the dead actor would invoke EnemyHealth.OnEnable and reset its health/colliders. Completion now keeps the already-dead actor enabled only until the visual tail finishes; both colliders stay disabled, health stays dead/zero, Radar stays closed and no attack authority remains. Rendered assertions cover this invariant.
- Shader-specific profiling: **0 managed bytes across 10,000 warmed property-update calls**, approximately **1.216 ms total** in this Editor run. This measures the direct cached-block Apply workload, not whole-encounter GC or GPU cost. State transitions still allocate their small tween/delegate objects. Corruption-material objects stayed **1 -> 1** across two full retries; exactly one authored overlay remained; the same property block was reused. No per-state material instantiation or runtime Update was added.
- Scope audit: all **6,106** starting scene records remain, with **4** new records. Only the existing Purple GameObject component list, Transform child list and Purple controller presentation reference changed; zero unresolved local references. Original base-renderer records, component prefab, source pixels/importers, vendor assets, URP/Renderer2D settings and gameplay tuning assets are byte-unchanged. Detailed changed-file list: `Logs/RouteCoreShader/scope.txt`. Scoped source/document whitespace checks passed.

Remaining art work is optional bespoke core housing/socket art and subjective signal-intensity review across displays. The existing source sprite glow is retained; this pass adds no blur or displaced silhouette. Other graphics APIs, standalone-build performance and human combat balance remain outside the measured evidence. No commit or push.

## 2026-09-28 - Route Core combat identities and Purple Radar finale

This section supersedes the earlier three-fusion completion and radial/aimed/fan attack descriptions below. The existing Route Core controller still owns defense completion, saving and FinalNetwork launch; the encounter controller owns sequencing, Deck/player/camera/HUD lifetime and retry. No campaign field or partial-phase save was added.

| Stage | Production HP | Authored attack | Telegraph / recovery |
|---|---:|---|---|
| Orange / Sector Stabilizer | 70 (unchanged) | Shotgun: 5 pellets across 64 degrees, 2 volleys, second offset 6 degrees; speed 4.2 | Committed cone .65 s; volley spacing .24 s; recovery 1.4 s |
| Blue / Phase Navigation Lens | 70 (unchanged) | Sniper: 3 fast single projectiles per cycle; speed 14 | Tracking AIM .65 s, frozen LOCK .30 s, shot recovery .35 s, cycle recovery 1 s |
| Green / Matter Compressor | 90 (unchanged) | Machine Gun: 10 shots at .13 s spacing across a committed 24-degree sweep; speed 5.2 | Warmup .65 s; cooling 1.8 s |
| Purple / central corruption | 50 (provisional new target) | Active Radar scan opens one 4 s damage window; one slow 8-way pulse, speed 3 | Opening grace 1 s; pulse delayed at least .55 s into exposure; scans while exposed ignored |

All attacks use the existing `Projectile_Enemy` definition (damage **2**, unchanged) through PoolManager/Bullet. No per-shot Instantiate fallback remains in these actors. A missing bootstrap PoolManager fails the encounter request with an actionable error. Enemy projectile range remains 16; source-scoped release runs on defeat, cancellation and phase cleanup. The prefab's authored `WeaponTelegraph` LineRenderer uses the existing built-in Sprites/Default material, with no shader asset. Orange and Green freeze a base direction before warmup; Blue tracks only during AIM, then changes line brightness/width for LOCK and fires along that exact direction. No hitscan, homing or new per-frame manager was added.

Each component still requires defeat -> explicit Interact -> fusion. Third fusion now starts Purple once and **does not clear defense**. Only Purple death calls back to the encounter, which then invokes the original `CompleteSettlementDefense` bridge once. Failure/cancellation leaves Activated + uncleared; retry starts all three full-health components and zero fusions, then Purple. No partial progress, part grant, reward drop, retry cost or new save schema exists.

Authored additions:

- `RouteCoreDeck/PurpleCorruptionTarget` is inactive outside its phase. `Damage` owns EnemyHealth and a trigger enabled only during exposure. Sibling `RadarDetection` owns RadarTarget and an always-detectable phase trigger with **no EnemyHealth ancestor**, so hidden detection does not absorb bullets. The existing EnemyHealth owner-scoped damage floor also rejects direct hidden-state damage without healing already-lost HP. `CorruptionVisual` reuses `Space Kit/Core/core1.png`.
- `RouteCoreDeckHUD/PurpleRadar/RadarPanel` is a compact 64x64 scope at canvas (197,90), using RadarHUD, RadarScopeGraphic, RadarPanelAnimator and the existing marker prefab. Its input hint uses `RadarBindingDisplay` / `QuickScanBindingDisplay` and localized labels. The existing top objective alone shows Purple messages; no duplicate status strip was retained after rendered inspection.
- The saved SettlementDefensePlayer scanner/VFX were present but disabled and unbound. They are now bound to the Deck panel. The encounter locks scanner input outside Purple, unlocks it only for the finale, then closes and locks it on every exit. Deck passive discovery is disabled; Tutorial/Expedition scanners, Q/Mouse 4 bindings, cooldown, taunt, stealth and global target registry code are unchanged.
- Exposure subscribes only to `PlayerRadarScanner.ScanCompleted` and requires its own target in the active scan results. Q/opening/passive/map discovery do not expose. Repeated scans during exposure are ignored. Timeout re-hides, disables damage and clears the phase's remaining bullets.
- Blackout captures an authored list of environment/RouteCore sprite colors and scales RGB to .25 hidden / .45 exposed, preserving alpha. It excludes the player, player gauges, projectiles, temporary component actors, Purple target and HUD. The normal central Activated renderer is temporarily hidden to avoid a duplicate target. All original colors and its enabled state restore exactly; no material or lighting mutation occurs.

Cleanup stops owned exposure/attack routines, telegraphs, tweens, scan subscription, VFX/panel state and source-owned bullets on defeat, death, cancel, disable and success. A rendered aborted-scene check exposed a pre-existing cleanup ordering bug when navigation was destroyed first; Unity-null guards now make that restoration safe. Facility navigation, camera/aim overrides, HP/Armor/heat/charge HUD and normal return behavior remain with the existing owners.

Validation and limits: see the current result entry below. Evidence is in `Logs/RouteCoreCombat/`; saved authoring uses `RouteCoreCombatAuthoring`, focused tests use `RouteCoreCombatTests`, and `RouteCoreCombatProbe` renders the saved Deck at actual 480x270 using virtual keyboard/mouse input and transient progression. No user save is written. Existing world/HUD probes now also complete the Purple phase rather than assuming third fusion clears defense.

**Human balance TODO:** natural fight duration/damage with each ship, close-range Orange double-volley gaps, Blue lock/readability, Green recovery generosity, Purple windows required and blackout visibility across displays. Values are provisional presentation/identity tuning, not finalized balance. Optional later Shader Graph work may refine the darkness edge or corruption pulse only after gameplay acceptance; no shader/pipeline work is included here.

## Current combat validation results

- Unity 6000.0.69f1 runtime and Editor/test compilation passed. Saved-authoring validation passed; localization source import and catalog validation passed with **249 records** (four added Purple keys, no existing translation changed). Existing CSV row-order warnings remain; the importer normalizes catalog order.
- Focused EditMode: **77/77**. Full EditMode after final production changes: **963/966**; all **21 new cases pass**. The only failures are the identical baseline names/messages for `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)`. They remain outside this task.
- Actual saved-scene **480x270 Play Mode: 161 assertions**, 24 native-resolution captures. Coverage includes all three telegraph/fire/recovery identities, explicit reactivation/fusion, real Q and Mouse Back/Mouse 4 scan input, hidden/exposed projectile damage, ignored exposed rescans, timeout/re-hide, Purple pressure, real Machine Gun Purple clear, player death in Orange/Blue/Green/Purple-hidden/Purple-exposed, full retries, active-Purple controller disable/reentry, completion once, clean Deck, Return submit, and final-launch eligibility. Full FinalNetwork scene travel was not invoked from this disposable no-RunManager fixture; the existing launch authority is untouched.
- Korean and English captures were inspected at native size. The rendered pass removed a duplicate Purple status readout and replaced an overflowing unknown `?` contact with the existing core hexagon, colored purple **only in the Deck RadarHUD**. Final captures report no sampled TMP overflow or missing glyph. Player, projectiles, HP/heat and Radar stay readable while environment brightness changes.
- Diagnostic first-cycle observations: Orange approximately 2.5 s / 10 pellets, Blue approximately 5.1 s / 3 shots, Green approximately 3.8 s / 10 shots (scripted observation windows, not full fights). A controlled real-Machine-Gun Purple clear took approximately **8.5 gameplay seconds / 3 scan windows**, with player invulnerability. Components were defeated through scripted EnemyHealth damage for lifecycle coverage. Orange's first observed cycle took zero player damage; five later deliberate lethal QA hits are excluded from balance assessment. These do not establish natural fight duration or subjective difficulty.
- No new runtime Update, global scan loop, LINQ combat loop, shader or material mutation was added. Projectiles use the existing pool and source cleanup. Standalone performance/GC profiling and physical-device play feel remain unmeasured; Editor capture/timing is not a performance benchmark. Hidden-Editor stalls were removed from the probe by enabling background execution and requesting Player Loop updates; production fusion timing was not changed.
- Current-tree preservation: **all 6,070 original Settlement scene IDs and all 12 original component-prefab IDs survive**; 36 scene IDs and three prefab IDs were added. Only five pre-existing scene records changed: the scanner, VFX enabled flag, encounter bindings, Deck child list and Deck HUD child list. Management/DarkUI records, camera/player bindings, core HP, projectile/reward assets, campaign/save/equipment code and unrelated working-tree files are unchanged. Source added-line whitespace checks are clean; Unity's prefab serializer adds four cosmetic spaces on empty metadata fields.

Evidence: `Logs/RouteCoreCombat/{author.log,focused.xml,full.xml,test-comparison.txt,scope.txt,scene-record-changes.txt,whitespace.txt,render.log}` and `Rendered/{audit.txt,index.html,*.png}`. The full suite precedes only a probe-only screenshot timing refinement; no production code/asset changed afterward.

Exact task files: existing `SettlementDefenseCorruptedCore.cs`, `SettlementDefenseEncounterController.cs`, `Settlement.unity`, `PF_SettlementCorruptedCore.prefab`, localization CSV/imported catalog, `QAStabilizationPass1Tests.cs`, `RouteCoreDeckAuthoringTests.cs`, and the two existing world/HUD render probes. Added `SettlementDefensePurpleCore.cs`, `RouteCoreCombatAuthoring.cs`, `RouteCoreCombatProbe.cs`, `Tests/RouteCoreCombatTests.cs` and their Unity metas. Documentation changed only this file, `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, `IMPLEMENTATION_STATUS.md`. No shaders, campaign save schema, campaign bosses, normal enemies/Field Bases, equipment/economy or DarkUI changed. No commit/push.

## Earlier world/HUD authoring record

This pass authors the existing Settlement world deck. It introduces no new runtime owner, runtime hierarchy builder, gameplay rule, progression state, or save field. The baseline was the current local working tree, including the completed inventory and DarkUI presentation work.

## Authority and authored hierarchy

- `SettlementDefenseEncounter` retains the only `SettlementDefenseEncounterController` and its existing serialized bindings.
- `RouteCoreDeck` is saved inactive. It contains the existing `SettlementDefensePlayer`, `PlayerStart`, three combat anchors, `Global Light 2D`, and the only `RouteCoreRoot` / `SettlementRouteCoreController`.
- `RouteCoreRoot` retains `Missing`, `Ready`, `Assembled`, `Activated`, and `CorruptionWave`. Each original state root owns its housing sprite, energy, three small couplings, and conduits. `centralVisual` still references the original `Activated` SpriteRenderer.
- New `RouteCoreDeck/DeckEnvironment` contains `Floor`, `MaintenanceRails`, `CentralMount`, `ComponentStations/{SectorStabilizer,PhaseNavigationLens,MatterCompressor}`, and `FacilityEntry`. These are authored decoration only.
- `RouteCoreDeckHUD` retains its existing objective, vitals, interaction prompt, and `ReturnToFacilities` button. The button remains the encounter's `facilityNavigation` reference with the original `LeaveDeck` callback.

There is no permanent duplicate corrupted core. The existing encounter still instantiates exactly three local actors from `PF_SettlementCorruptedCore.prefab`. Recovery Processor restoration remains the assembly authority. The physical core retains activation, defense request/completion bridge, and final launch behavior.

## Camera and layout

The saved shared camera remains at `(0, 0, -10)` with its original management settings. Existing `EnterDeck` temporarily selects orthographic half-height `4.21875`, then restores the previous projection on exit. At **480 × 270**, the visible world is **15 × 8.4375 units**, or 32 pixels per world unit. No camera zoom or controller constraint was changed.

| Element | World position | Identity / purpose |
|---|---|---|
| Central Route Core | `(0, 0.375, 0)` | Purple; only world progression interaction |
| Sector Stabilizer / existing AnchorWest | `(-3.5, 1.25, 0)` | Orange; first combat stage |
| Phase Navigation Lens / existing AnchorNorth | `(3.5, 1.5, 0)` | Blue; second combat stage |
| Matter Compressor / existing AnchorEast | `(2, -1.875, 0)` | Green; third combat stage |
| PlayerStart / authored player | `(-3.75, -2.375, 0)` | Lower-left entry; clear of the core trigger |
| Facility entry guide | Around `(-3.75, -3.125, 0)` | Muted cyan floor marker; no interaction authority |
| Return button | Existing UI center `(-184, -119)`, size `102 × 20` | Separate facility return action |

The three stations form a triangle around the central mount. The central mount is 2.5 units wide; station sockets are 1.5 units wide. Component attack sightlines remain open. Floor tiles cover the whole view, including HUD gutters; rails sit outside the existing playable constraint rather than creating physical choke points.

The unchanged viewport constraint is `x = [-7, 7]`, `y = [-3.51875, 3.51875]`; the player controller also subtracts the actual body extents. Important points are kept within `|x| < 6`, `|y| < 2.75`. Positioning uses the existing 32 px/unit world convention where practical.

## State presentation

| State | Presentation | Existing physical interaction |
|---|---|---|
| MissingParts | Dark empty core housing, unlit couplings, disconnected conduits, weak central energy | Missing-component status |
| ReadyToAssemble | Recovered-component coupling lamps present; central conduit gaps remain | Directs to Recovery Processor; does not assemble in the world |
| Assembled | Complete housing, connected conduits, low purple energy | Activation |
| Activated | Brighter purple energy and powered colored conduits | Start/retry defense; after completion, Final Expedition launch |

The existing defense intro alone controls corruption. Completion kills encounter tweens, hides `CorruptionWave`, restores the original central transform/color and navigation state, removes all local cores, and returns to management. Reentering shows a stable, clean Activated deck. No decorative update loop, animation controller, coroutine, or extra DOTween ownership was added.

## Collider and boundary policy

- The existing Route Core collider remains a trigger.
- Player body/world colliders are unchanged.
- Dedicated component combat and reactivation colliders remain unchanged in the encounter prefab.
- All floor, rails, sockets, lamps, mount, couplings, and conduits have **no colliders** and no gameplay scripts.
- No physical wall colliders were added. The established viewport constraint supplies the boundary, avoiding seams or dash snags.
- Persistent central art uses sorting orders below the existing enemy projectile order 2, so the nonblocking mount does not hide shots visually. The temporary corruption wave retains its original intro-only ordering.
- Return remains hidden while defense owns navigation; the controller's existing LeaveDeck denial is unchanged. Failure and completion restore the captured navigation state.

## Reused world art

All artwork comes from existing `Assets/Space Kit/` sources. No DarkUI image is used in the world, and no source pixels or importer metadata changed.

| Source | Role |
|---|---|
| `Tile/Tile.png` / `Tile_0` | Native-size floor tiles and entry marker |
| `Tile/Tile.png` / `Tile_2` | Repeated maintenance conduit segments |
| `Walls/wall9.png` | Perimeter maintenance rails |
| `Walls/DIx4.png` | Dormant station sockets and central component couplings |
| `Ships and Stations/Station 2.png` | Central mechanical mount/frame |
| `Core/core7.png`, `core6.png`, `core5.png`, `core9.png` | Missing, Ready, Assembled, Activated housing/energy sprites |
| `Particles (Sprites)/Small Flare.png` | Small identity lamps and purple core energy |
| `Core/core1.png` | Dedicated encounter actor's existing visual renderer; white center preserves orange/blue/green tint |

The previous amber-filled actor sprite suppressed green and blue in rendered captures. Only its sprite reference changed; colliders, renderer binding, HP, damage, attack cadence, projectile definition, defeat darkening, reactivation, and fusion code remain unchanged. Existing URP/2D-light and sprite material conventions are reused; no pipeline feature, material, global tint, or lighting change was introduced.

## Authoring and validation

`RouteCoreDeckAuthoring.Run` is an explicit Editor-only saved-authoring entry point. It refuses Play/Prefab Mode and unsaved scenes, reuses existing nodes and references on rerun, and validates required bindings and decorative collider policy. It does not install a runtime fallback. `RouteCoreDeckAuthoringTests` covers camera-safe placement, original ownership/callbacks, canonical order/colors/HP, no decorative colliders or duplicate actors, all four state roots, cleared presentation, and idempotent authoring without management UI changes.

`RouteCoreDeckRenderProbe.Run` is a disposable Play Mode fixture using the saved world, camera, lighting, player, encounter and actual 480 × 270 RenderTexture. It uses transient campaign data with no SaveManager or expedition owner. EnemyHealth damage drives the real defeat/reactivation/fusion sequence; player invulnerability is fixture-only for capture. It exercises entry, movement/dash at all edges and corners, interaction acquisition, failure/retry, the three combat stages, final completion, clean reentry, Return submit, and camera/constraint restoration.

Unity runtime/Editor/test compilation and localization validation passed (245 catalog records; no localization changes). The focused selection passed **143/143**; the new deck fixture contributes **12/12** tests. The full EditMode suite passed **851/854**. The three failures are the same pre-existing `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)` save-normalization cases recorded before this pass. They do not involve deck authoring and were left unchanged.

Seventeen actual rendered captures and the Play Mode assertion audit are in `Logs/RouteCoreDeck/Rendered/`; `index.html` displays each at native resolution and `contact-sheet.png` provides an overview. These include Missing, Ready, Assembled, Activated, corruption intro, orange radial combat, blue aimed combat, green fan combat, defeated/reactivatable state, first fusion, final fusion, clean post-defense reentry, and Return focus. A supplemental blue-volley capture and travel assertion prove that an actual projectile crosses the central trigger/mount without collision or visual obstruction. Supplemental English-language-setting captures preserve the existing legacy Korean fallback strings. All eight edge/corner movement and dash cases, failure cleanup, fresh retry, actual three-fusion completion, Return submit and camera/constraint restoration passed. No missing glyph or TMP overflow was reported in sampled deck HUD text.

Execution logs/results: `Logs/RouteCoreDeck/author.log`, `focused.xml`, `full.xml`, `render.log`, and `Rendered/audit.txt`. The full suite ran before the final central-art sorting adjustment; the final focused suite and rendered crossing check were rerun after it. `preservation.txt` compares against the task's current-tree baseline: all 5,000 original scene IDs and 12 dedicated-prefab IDs survive; original UI/camera/collider/gameplay records are preserved. The only existing encounter-record changes are the two corrected identity colors; HP/order/references remain byte-equivalent apart from serializer whitespace. Source art, DarkUI and runtime gameplay files are unchanged. Task-scoped diff/whitespace checks pass; the two previously recorded scene whitespace lines remain outside this task's changes.

Changed production assets: `Assets/01_Scenes/Settlement.unity` and `Assets/03_Prefabs/Enemy/PF_SettlementCorruptedCore.prefab` (visual sprite only). New Editor files: `RouteCoreDeckAuthoring.cs`, `RouteCoreDeckRenderProbe.cs`, `Tests/RouteCoreDeckAuthoringTests.cs`, with Unity metas. Documentation: this file, `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, and `IMPLEMENTATION_STATUS.md`. No commit or push was performed.

## Remaining art and validation limits

The composition intentionally reuses generic machinery/space-station assets. Bespoke chamber wall corners, purpose-built component socket art, and a custom central housing remain optional art work. The existing compact deck HUD, legacy Korean messages, and player art are retained. Scripted Play Mode movement and EventSystem submit checks do not establish physical controller hardware feel or a human difficulty/balance assessment. No balance change belongs to this pass.

## 2026-09-28 - Combat HUD and mouse-facing follow-up

The world hierarchy, stations, colliders, camera framing, HP/attack order, patterns and fusion/completion authority above are unchanged. This follow-up adds only authored combat presentation/input support.

- `RouteCoreDeckHUD/CombatStatus/HP`: 100x5 red HP gauge centered at canvas (-174,101), with the original Vitals TMP readout above it at 8 px. `Armor` is a 100x2 white strip at (-174,96), hidden when MaxArmor is zero. `RouteCoreCombatHUD` subscribes to existing PlayerHealth/PlayerArmor Changed events and refreshes immediately on enable.
- `CombatStatus/WeaponHeat`: 100x3 gauge at (-174,87), bound to SettlementDefensePlayer's PlayerWeaponController and existing WeaponHeatUI. It hides at zero/after cooling and for Shotgun/Sniper.
- `RouteCoreDeckHUD/PlayerCharge`: 32x3 player-relative gauge, using existing PlayerChargeGaugeUI and WorldGaugeFollower with explicit player, weapon-source, Canvas and deckCamera references. It remains outside AimVisualRoot and uses Expedition's fixed world offset `(0,1.1,0)` rather than animated child-renderer bounds. As in Expedition, its green overheat-recovery progress is distinct from the top heat amount; for Sniper it presents charge events and hides on release/cancel. No duplicate heat calculation or sniper timer exists.
- The old controller-owned text-only HP subscription is removed; its serialized Vitals reference remains valid and the same TMP object is reused by the HP gauge. Existing messages, interaction prompt and Return callback are retained. Deck disable hides the hierarchy, removes vitals/weapon subscriptions and kills existing presenter tweens.
- Proven aim cause: the saved camera has tag Untagged, and Settlement contains zero MainCamera-tagged cameras. The existing AimVisualRoot, FirePoint parent and Rigidbody freezeRotation are correct. EnterDeck acquires PlayerController2D's temporary aim camera under the encounter owner; CloseDeck releases it, and PlayerController2D.OnDisable clears ownership. The same owner can refresh; another owner cannot replace/release it. Fallback camera resolution outside the Deck is unchanged.

Saved authoring is maintained by `SettlementUsabilityAuthoring.Run/ApplyDeckHUD`; Equipment audio/input by `EquipmentDevelopmentInstaller.AuthorInputAndSound`, also called by its existing Install path. No runtime UI reconstruction or global styling/input manager is introduced.

Validation:

- Runtime and Editor/test compilation passed; focused EditMode 195/198, including all 20 new cases. The three failures exactly match the baseline save-normalization messages. Full EditMode: 942/945; all three failures have exactly the baseline names/messages, with no new failures.
- `Logs/SettlementUsability/Rendered/audit.txt`: 141 passing assertions and 11 actual 480x270 captures. `01`-`04b` cover Korean/English Equipment, card/transaction/failure states and legacy focus; `05`-`10` cover Sweeper heat/overheat/recovery, Breacher fire, Lancer charge and reentry. All captures were inspected; no missing glyph or TMP overflow was reported.
- Actual virtual-device W/S/arrows, Space, Enter and gamepad Submit passed. Audio event probes prove one Hover for combined pointer/selection, no refresh replay, one TraitSelect per card action, one UiClick per view action, and one result cue per successful/failed transaction. Held Space neither repeats manufacturing nor auto-fits. Transactions used only `Logs/SettlementUsability/Rendered/transaction-test.json`, never the user save.
- All ships passed cardinal plus diagonal mouse/visual/muzzle checks, unrotated physics root, firing/charge, movement/dash independent of facing, exit cleanup and reentry. Actual firing-event checks confirm projectile direction; HP subscription counts are one on entry and zero on exit. Existing defense failure, retry, three-fusion completion and encounter disable all release aim/HUD ownership. HP/Armor tests also cover zero Armor.
- Scene audit: all 5,882 original serialized IDs and original persistent callbacks retained, 188 IDs added, zero unresolved local references. Newly serialized selection-hover fields on other existing buttons are false; only Equipment controls opt in.

Remaining limits: synthetic input/audio-event validation does not replace physical-device listening or subjective combat assessment. The existing later-campaign Curse sprite (`Px_Player_4`) has a nearly square silhouette in Deck captures; it is unchanged in this HUD/aim pass. No Orange/Blue/Green redesign, Purple blackout/radar phase or shader work was included.

