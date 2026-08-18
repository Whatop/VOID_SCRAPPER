# Repository Guidelines

## Project Overview

VOID SCRAPPER is a Unity `6000.0.69f1` project written in C#.

Genre:

- 2D top-down shooting roguelite
- exploration / harvesting / combat / return / settlement growth

Core gameplay loop:

`Explore -> Harvest -> Combat -> Return -> Settlement Growth`

Combat is primarily a way to secure harvesting routes and valuable locations, not the sole objective.

Target presentation:

- 480x270
- Pixel Perfect
- PPU 32

---

## Project Structure

First-party C# lives in:

`Assets/02_Scripts/`

Main feature folders include:

- `Core/`
- `Player/`
- `Enemies/`
- `RunRuntime/`
- `Settlement/`
- `Tutorial/`
- `UI/`
- `Boss/`
- `Audio/`
- `Data/`

Other important folders:

- Scenes: `Assets/01_Scenes/`
- Prefabs: `Assets/03_Prefabs/`
- Input Actions: `Assets/04_Input/`
- Audio: `Assets/06_Audio/`
- Text / Fonts: `Assets/07_Txt/`
- Packages: `Packages/`
- Project Settings: `ProjectSettings/`

Build scene order:

1. `Boot`
2. `Tutorial`
3. `Settlement`
4. `Expedition`

Treat `Assets/Plugins/`, Asset Store packages, and imported third-party asset folders as vendor code unless explicitly asked to modify them.

Do not modify vendor source code when the same result can be achieved through first-party wrappers, prefabs, materials, configuration, or integration components.

---

## Required Workflow

Before changing any system:

1. Search the existing related scripts.
2. Inspect the relevant prefabs.
3. Inspect the relevant scene objects when applicable.
4. Inspect related ScriptableObjects.
5. Inspect Input Actions if input or displayed controls are involved.
6. Search project-wide usages before renaming or deleting anything.
7. Check whether an installed asset already provides the requested functionality.
8. Prefer the smallest safe change that fits the current architecture.

Do not invent a replacement system before checking whether the project already contains one.

If required project files, prefabs, definitions, or references are missing, report what is missing instead of guessing.

When an existing system already works and only needs an extension, extend it instead of replacing it.

---

## Architecture Rules

Reuse existing classes, interfaces, naming, events, and data structures whenever possible.

Avoid:

- unnecessary managers
- unnecessary singletons
- speculative abstractions
- excessive design patterns
- duplicate systems
- large unrelated refactors during feature work

Do not move logic into a new manager simply to make the code look cleaner.

Keep responsibilities close to the feature that owns them.

Examples:

- projectile interception belongs to the interceptor / summon
- temporary deployable behavior belongs to the deployable
- Reinforcement charge/recharge belongs to `PlayerReinforcementController`
- player visual state belongs to the existing player visual controllers
- UI reads gameplay state instead of duplicating it

Do not duplicate authoritative state in:

- UI
- spawned objects
- temporary VFX
- debug tools

When modifying an existing feature, check its impact on related systems.

---

## Performance Rules

This game can have many:

- enemies
- projectiles
- pickups
- debris objects
- summons
- temporary fields
- VFX

active simultaneously.

Avoid hot-path allocations and expensive repeated work.

Do not use in frequent gameplay loops:

- repeated `Instantiate/Destroy` for commonly spawned objects
- LINQ
- repeated `FindObjectsByType`
- repeated hierarchy searches
- unnecessary temporary arrays/lists
- unnecessary per-object `Update()`
- avoidable GC allocations

Use the existing `PoolManager` for frequently spawned:

- projectiles
- VFX
- temporary deployables
- support units
- repeated gameplay objects where pooling is safe

Prefer:

- events
- physics trigger callbacks
- reusable NonAlloc buffers
- low-frequency refreshes

over repeated full-scene searches.

Event-driven updates are preferred when data changes infrequently.

Do not optimize low-frequency setup code at the cost of maintainability.

---

# Existing Third-Party Assets

Available packages include:

- DOTween Pro
- Motion Titles Pack
- Dialogue System for Unity
- Text Animator for Unity | UI Toolkit and Text Mesh Pro
- Flow Fx - Magic Motion Blur Effect
- Real Materials vol.10 - Patterns

Use these packages only where they are a good architectural fit.

Do not use an asset simply because it exists.

Prefer an installed asset when it cleanly solves the requested feature and avoids duplicating systems.

---

## DOTween Pro

Prefer DOTween for:

- UI transitions
- scale / fade / pulse effects
- short VFX interpolation
- presentation movement
- cinematic movement
- temporary visual feedback

Prefer DOTween over:

- presentation-only `Update()` loops
- unnecessary Coroutines

when appropriate.

Do not use DOTween as the authoritative gameplay timer when an existing gameplay controller already owns the timing.

Gameplay state and presentation state must remain separate.

---

## Dialogue System for Unity

Use Dialogue System for Unity for:

- story dialogue
- NPC dialogue
- tutorial story communication
- boss / campaign communication
- major narrative sequences

Do not build a second dialogue framework.

Do not use Dialogue System for ordinary:

- pickup prompts
- shop buttons
- interaction hints
- gameplay warnings
- item cards

unless actual dialogue behavior is required.

Dialogue gameplay pause should use the project's existing pause ownership/integration rather than introducing another independent pause system.

---

## Text Animator for Unity | UI Toolkit and Text Mesh Pro

Use Text Animator for first-party text presentation where animated text adds actual value.

Good candidates:

- Dialogue System dialogue
- tutorial/story communication
- boss introduction titles
- event titles
- important system messages
- Pixel Curse communication
- alien transmissions

Good Pixel Curse / alien uses include:

- character jitter
- corrupted reveal
- irregular typing
- short glitch-like animation
- purple alien communication effects

Do not use animated text where immediate readability matters more.

Avoid it for:

- HP / Armor values
- resource counters
- cooldown values
- Radar labels
- rapidly changing HUD numbers
- combat-critical interaction prompts

Do not create a second custom text-animation system when Text Animator already provides the required effect.

Prefer integrating Text Animator with existing:

- TextMeshPro
- Dialogue System UI
- first-party title/message UI

instead of replacing those systems.

Gameplay state and timing should not depend on text animation completion unless a sequence explicitly requires it.

---

## Motion Titles Pack

Use Motion Titles Pack only when its presentation fits the current UI/art direction.

Good candidates:

- chapter / sector title
- boss introduction
- major story transition
- special event title

Do not force it into normal HUD or gameplay prompts.

Do not create excessive title sequences that interrupt repeated roguelite gameplay.

---

## Flow Fx - Magic Motion Blur Effect

Flow Fx is a stylized fullscreen presentation tool.

Use it selectively for short, high-impact moments such as:

- Player Dash
- high-speed enemy reinforcement entry
- Pixel Curse infection / transformation
- boss entrance
- boss phase transition
- selected high-impact weapon moments
- major alien distortion

Do NOT leave Flow Fx strongly active during normal gameplay.

Avoid persistent Flow Fx during:

- regular movement
- Machine Gun sustained fire
- normal enemy movement
- dense bullet patterns
- Radar usage
- ordinary exploration

Gameplay readability has priority over motion blur.

The player must always be able to read the true position of:

- bullets
- lasers
- enemies
- harvest objects
- interaction targets
- telegraphs

Prefer effects shaped like:

`0 -> brief intensity -> 0`

rather than persistent blur.

When practical, animate Flow Fx intensity / Volume weight using DOTween.

Do not build a second custom fullscreen motion-blur system if Flow Fx already solves the requested presentation.

---

## Real Materials vol.10 - Patterns

Real Materials vol.10 - Patterns is primarily a source of patterns and surface materials.

It must NOT define the general visual style of VOID SCRAPPER.

VOID SCRAPPER remains primarily:

- 2D
- pixel-art
- 480x270
- high-readability

Use Real Materials selectively for objects that are intentionally visually foreign to the normal game world.

Good candidates:

- final boss exterior shell
- massive alien machinery
- ancient alien structures
- unusual story-critical 3D structures
- selected large background structures

Avoid applying Real Materials broadly to:

- Player
- ordinary enemies
- normal harvest objects
- ordinary debris
- normal shops
- UI
- standard 2D pixel sprites

The visual contrast should communicate:

`This does not belong to the normal world.`

---

## Real Materials - Final Boss Direction

A current final-boss visual direction is a massive spherical alien entity / artificial-looking alien core.

Initial appearance:

- nearly perfect sphere
- metallic/artificial shell
- large readable surface patterns
- controlled and mechanical behavior

As the battle progresses:

Phase 1:

- clean patterned metallic sphere
- precise movement
- artificial appearance

Phase 2:

- surface patterns shift or rotate incorrectly
- pattern alignment begins to break
- purple light leaks through seams
- shell begins to fracture

Phase 3:

- shell sections break away
- alien interior is exposed
- purple corrupted energy appears
- pixel/data corruption becomes visible
- strong visual connection to the Pixel Curse

Use Real Materials primarily for the OUTER shell.

Use existing:

- Pixel Curse visuals
- purple emissive effects
- pixel fragments
- Flow Fx
- VFX

for the alien interior and transformation.

Do not make the final boss look like an ordinary realistic metal ball.

The materials exist to support an artificial/alien visual identity.

---

## Real Materials - Material Safety

Do not directly edit third-party vendor materials when avoidable.

Prefer:

1. create or duplicate a project-owned Material
2. use the required source textures/patterns
3. modify the project-owned Material

Keep project-owned boss materials outside vendor folders.

Do not overwrite third-party source textures.

Any 3D/material-based asset must be checked at actual gameplay presentation:

- 480x270
- Pixel Perfect
- actual gameplay camera distance

Do not judge material quality only in Scene View.

Prioritize:

- silhouette
- large pattern shapes
- emissive contrast
- phase readability

over tiny realistic surface details.

If a material produces excessive:

- shimmering
- aliasing
- noisy detail

at 480x270, simplify or replace it.

---

## Flow Fx + Real Materials

Flow Fx may be combined with Real Materials for major boss presentation.

Good uses:

- outer shell cracking
- phase transformation
- pattern displacement
- alien-core exposure
- boss teleport
- high-speed boss entrance

Do not leave Flow Fx active throughout the entire boss battle.

Actual attack geometry, bullets, and telegraphs must remain readable.

---

# Input

Target gameplay bindings:

- WASD: Move
- Left Mouse: Fire
- Right Mouse: Dash
- Q: Radar
- F: Interact
- E: Inventory
- Tab: Map
- R: Reinforcement
- G: Dismantle / Field Drop
- Esc: Cancel / Menu

Never hardcode displayed key labels such as:

- `F`
- `E`
- `R`
- `Q`
- `Tab`

in gameplay UI.

Use the current Unity Input System binding display string.

If a fallback key exists, it must only be used when the InputAction cannot be resolved.

Do not disable the complete shared Player InputAction asset to implement a local gameplay lock when existing pause/input ownership can handle it.

---

# Player / Weapon Identity

Weapon visual language:

- Machine Gun: green
- Shotgun: orange
- Sniper / Laser: blue
- Alien / Curse: purple
- Neutral frame: white / gray

Weapon readability should come from:

- projectile shape
- projectile color
- muzzle VFX
- trails
- hit VFX
- glow
- weapon accent
- exhaust / afterimage

Do not make every weapon projectile visually identical.

---

## Normal Player

The current normal Machine Gun visual uses:

`MuchineGun_4`

from:

`Assets/Space Kit/Player/MuchineGun.png`

This represents the normal pre-Curse ship form.

---

## Pixel Player

The intended cursed player body is:

`Px_Player_0`

from:

`Assets/Space Kit/Player/Px_Player.png`

`Px_Player` is intentionally a very simple square-pixel form.

This simple form should make future visual systems easier to build, including:

- Pixel Curse
- alien technology
- follower units
- friendly ship fusion
- linked ships
- tail-like attached units

Avoid designing future fusion states around unique composite sprites such as:

- `Fusion_1`
- `Fusion_2`
- `Fusion_3`

for every weapon/state combination when the same result can be built from simple independent visual units.

---

# Pixel Curse

The Pixel Curse is a persistent story passive / debuff, not a normal disposable Trait.

It must:

- use purple visual language
- remain persistent
- remain non-droppable
- remain non-dismantlable
- stay out of normal shop random pools
- stay out of normal random/reward pools
- appear in the Debuff UI
- remain across relevant story progression

Do not duplicate Trait infrastructure only for the Curse.

Use existing Trait metadata and persistent-story acquisition logic.

---

## Pixel Curse Visual Rule

Before Pixel Curse:

- normal ship body
- current weapon identity

Current Machine Gun example:

`MuchineGun_4`

After Pixel Curse:

- Base Body: `Px_Player_0`
- WeaponAccent silhouette: `Px_Player_0`
- CurseEdge silhouette: `Px_Player_0`
- CurseAfterimage silhouette: `Px_Player_0`

Weapon identity while cursed comes from color and effects:

- Machine Gun: green
- Shotgun: orange
- Sniper: blue
- Curse overlay: purple

Changing weapons while cursed must NOT restore a normal full-size ship sprite.

`Px_Player_0` remains the cursed body.

Do not solve Curse visuals with per-frame Sprite correction.

Use existing visual events and authoritative sprite override behavior.

---

## Pixel Curse Presentation

The intended visual is corrupted data / spatial error, not merely "more pixelated pixel art."

Good Curse presentation includes:

- purple pixel fragments
- displaced blocks
- short positional corruption
- corrupted afterimages
- brief palette errors
- glitch pulses
- strong transformation feedback

Flow Fx may be used briefly during the infection/transformation sequence.

Text Animator may be used for corrupted alien communication.

Do not keep strong fullscreen distortion active throughout normal cursed gameplay.

---

## Pixel Curse / Final Boss Connection

The final alien entity may share visual language with the Pixel Curse.

Possible shared elements:

- purple corrupted energy
- displaced pixel fragments
- data-like breakup
- unstable patterns
- visual errors
- impossible geometric movement

This should visually imply that the Pixel Curse and final alien entity are related.

Do not reveal story information prematurely through explicit UI text.

Prefer visual foreshadowing.

---

# Player-Owned Allies / Summons

Player-created temporary units may include:

- turrets
- support drones
- beacons
- future deployables

Use existing ownership metadata such as:

- `IPlayerOwnedAlly`
- projectile source information
- current typed ownership metadata

Player-owned projectiles must not damage:

- Player
- firing allied turret
- other Player-owned turrets
- Player support drones
- other explicit Player-owned summons

Do not use GameObject names to determine allegiance.

Do not create a global faction manager unless the project genuinely requires a broader faction system.

Player-owned summon lifetime, charges, and recharge remain owned by the relevant existing gameplay controller.

Spawned objects should own only their local gameplay behavior.

---

# Temporary Enemy Effects

Temporary AI effects must not corrupt the enemy's permanent role state.

Temporary attraction sources may include:

- Shotgun Radar taunt
- Auto Turret limited aggro
- attraction beacons

Temporary attraction is source-aware.

Current intended precedence:

`Newest valid temporary attraction wins.`

An old source must not clear a newer source.

When the current attraction expires, normal AI behavior resumes.

Do not create:

- ThreatManager
- TauntManager
- general status-effect framework

unless broader systems genuinely require them.

---

## Temporary Movement Effects

Movement slows and attraction are separate concerns.

Do not modify permanent/base enemy movement values for temporary slow effects.

Temporary slow sources should remain independent.

If multiple movement slows overlap:

- strongest currently active slow should normally win
- clearing one source must not clear another
- enemy role/state remains intact

Do not implement temporary slow using multiply/divide restoration on the base speed.

---

# Reinforcement Responsibility

`PlayerReinforcementController` remains authoritative for:

- equipped Reinforcement
- activation
- charges
- recharge
- active-duration state
- HUD state
- spawned Reinforcement ownership/lifetime

Spawned Reinforcement objects must not independently:

- consume charges
- start recharge
- change equipped items
- manipulate ReinforcementSlotUI
- mutate save data

Temporary spawned objects may own:

- targeting
- projectile interception
- local area control
- temporary attraction
- local VFX
- local cleanup

when configured by the authoritative controller.

---

# UI Direction

The Expedition HUD should prioritize immediate gameplay information.

Important HUD systems include:

- HP + Armor
- Dash
- Core Signal
- Buff / Debuff
- Active Reinforcement
- Cargo
- Resources
- Radar
- Map / Inventory hints
- Interaction UI

Prefer icon-fill cooldown presentation for:

- Dash
- Active Reinforcement

instead of large standalone sliders.

Interaction UI should separate:

- simple world interaction prompts
- detailed Trait / Reinforcement pickup cards

Simple world prompts may follow the world target.

Large Trait/Reinforcement detail cards should remain screen-space and fully inside Canvas bounds.

All displayed controls must use current InputAction bindings.

---

# Debug / Development Tools

Development-only tools may exist for faster testing.

Current examples include:

- ID-based Trait grant
- ID-based Reinforcement grant

Debug UI must only be accessible in:

- Unity Editor
- Development Builds

Normal release builds must not expose:

- development UI
- debug shortcuts
- debug granting controls

Debug tools should use normal gameplay acquisition/equip APIs whenever possible.

Do not let debug tools directly modify:

- save JSON
- private runtime dictionaries
- unlock flags
- balance data

when an existing gameplay API can perform the operation safely.

---

# Scene / Prefab Safety

Before renaming GameObjects, hierarchy nodes, scenes, prefabs, or assets, search for:

- `GameObject.Find`
- `Transform.Find`
- child-name comparisons
- scene-name strings
- resource-path strings
- serialized references
- editor scripts that depend on names

Do not perform broad hierarchy renames without checking these usages.

Preserve serialized references.

Use `FormerlySerializedAs` when renaming serialized fields where appropriate.

Do not manually regenerate existing Unity GUIDs.

Commit Unity assets together with their `.meta` files.

---

## Pooling Safety

When pooling prefabs, verify reused objects reset:

- runtime state
- timers
- ownership
- event subscriptions
- colliders
- visual state
- renderer state
- DOTween sequences
- target references
- temporary-effect sources

before reuse.

An old pooled object must never modify or clear state owned by a newer object.

---

# Coding Style

Use four-space indentation and Allman braces.

Naming:

- types: `PascalCase`
- methods: `PascalCase`
- properties: `PascalCase`
- events: `PascalCase`
- fields: `camelCase`
- locals: `camelCase`
- parameters: `camelCase`
- interfaces: `IName`

Inspector fields:

```csharp
[SerializeField] private float moveSpeed;