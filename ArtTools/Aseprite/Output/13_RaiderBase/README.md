# Raider Base Structures

Four separately authored structures in the approved RAIDER visual family. All use light salvaged armor, dark machinery, red markings, repaired plates and mismatched equipment. The structures share materials and pixel scale, not a recolored common hull.

## Assets and states

| Structure | Canvas | Aseprite source | Active pose | Damaged pose |
|---|---|---|---|---|
| Raider Outpost | 96x96 | `raider_outpost.aseprite` | ALERT: beacon and open reinforced entrance | Missing roof sections, broken communications hardware, jammed door |
| Raider Turret | 64x64 | `raider_turret.aseprite` | ARMED: extended twin barrels and hot indicators | Broken barrel, damaged ammunition feed and missing side armor |
| Raider Power Node | 64x64 | `raider_power_node.aseprite` | ENERGIZED: open grille, visible linear windings and live output terminals | Severed output cable, ruptured switchgear and exposed wiring |
| Raider Storage | 96x96 | `raider_storage.aseprite` | UNSEALED: opened clamps and retracted lids reveal salvage | Broken lids, snapped clamps, exposed freight and spilled loot |

Each source contains three single-frame tags in this exact order: `normal`, `active`, `damaged`. Normal is the default intact pose. The 200ms frame durations are inspection defaults, not finished looping animations. Damaged poses represent broken structures, not fully flattened destruction rubble.

Every structure has three transparent PNGs named `<structure>_normal.png`, `<structure>_active.png`, and `<structure>_damaged.png`, plus a `<structure>_states.png` horizontal strip. The Outpost and Storage strips are 288x96; the Turret and Power Node strips are 192x64. All strips have three native-size cells with no trimming or padding.

`raider_base_comparison.png` shows every state enlarged 2x, followed by the four Normal sprites at native scale. It intentionally has an opaque presentation background. The manifest records dimensions, filenames, anchors, layers, state labels and orientation.

## Role and silhouette

- Outpost: a solid command bunker with an offset mast, territorial pennant, unequal annexes and a reinforced lower entrance. It is the local command center.
- Turret: upward-facing twin autocannons on a stationary turntable with anchoring feet and a side ammunition feed. It has no ship engines.
- Power Node: a compact generator skid, cylindrical cooling equipment and heavy looped cable outlets. Linear windings replace any central glowing combat core.
- Storage: separate container skids, gaps between freight modules, asymmetric clamps and an open loot basket. The stepped storage silhouette was checked against the solid Outpost footprint.

All sprites are authored on the existing 2px pixel grid: logical 48x48 or 32x32, exported at exact 2x size. The fixed canvas anchor is its center. The Turret fires toward negative Y; Outpost and Storage access faces positive Y. The Power Node has no firing direction.

## Editable layers

All four sources use nine layers:

1. Main Hull
2. Cables
3. Exposed Machinery
4. Role module: Command and Antenna / Weapon Assembly / Generator and Output Modules / Freight and Clamps
5. Salvaged Armor
6. Faction Markings
7. Lights
8. Damage
9. VFX

Layers retain the full canvas and common anchor in every pose. Structural machinery remains separate from removable armor. Short sparks and active indicators use solid pixels; no gradients, antialiasing or semitransparent glow are present.

## Inspected sources and preservation

Seven approved reference files were verified before generation, relative to `ArtTools\Aseprite`:

- `Output\03_RaiderEnemy\raider_basic.png` — 64x64
- `Output\03_RaiderEnemy\raider_shotgun.png` — 64x64
- `Output\03_RaiderEnemy\raider_sniper_charging.png` — 64x64
- `Output\03_RaiderEnemy\raider_elite.png` — 64x64
- `Output\09_RaiderAssaultCommander\raider_assault_commander_idle.png` — 128x128
- `Output\10_RaiderSalvageCarrier\raider_salvage_carrier_idle.png` — 128x128
- `Output\11_RaiderSniperCommander\raider_sniper_commander_idle.png` — 128x128

The 18 Input files were inventoried first. All seven approved references were copied into the timestamped Temp folder and verified by SHA256 before Aseprite read them. All 25 original files were hash-checked afterward and remained unchanged. ReferenceOnly art was not resampled, traced or copied. The four structure silhouettes are new Lua geometry following the approved Raider construction language.

The shared palette has 13 approved colors: seven metals, three faction reds and three warm industrial/loot shades. Individual exports use 9-12 visible colors. Every selected color was verified against the approved references.

## Repeatable CLI workflow

Project output folder: `ArtTools\Aseprite\Output\13_RaiderBase`.

From the project root, rebuild this generated set with:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-RaiderBase.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe` in batch mode, inventories Input, verifies the exact source hashes, works with copied references, validates UTF-8 Lua without BOM, generates the set, reopens the exported files for validation, and verifies original hashes again.

For an extracted package, supply `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` folder. `-ToolRoot` optionally selects a separate build directory. Without `-ReplaceGenerated`, existing output is refused; with it, the folder must carry this generator's ID.

Included scripts are `Generate-RaiderBase.ps1`, `build_raider_base.lua`, `validate_raider_base.lua`, and the unchanged shared raster helper `raider_boss_pixel_helpers.lua`. The helper is reused without changing previous generators or assets.

Reference copies, logs, a half-size dark/light readability sheet and solid-color silhouette comparison are kept under `Temp\raider_base_<timestamp>`.

## Verification

- Reopened all four Aseprite sources and checked canvas sizes, nine layers, three frames and three single-frame tags per source.
- 159,744 pixel comparisons matched all 12 PNG states and four sprite sheets against the saved Aseprite frames.
- Verified binary alpha, exact 2x pixel blocks, canvas margins, approved palette, red faction identity and visible state differences.
- Damaged poses remove 328 Outpost, 132 Turret, 160 Power Node and 432 Storage opaque armor-layer pixels.
- All six pairwise role-mask comparisons differ by at least 326 pixels when compared on a 48x48 canvas. These measurements accompany visual silhouette inspection, not a claim of gameplay testing.
- Inspected the complete comparison at native scale and half-size states on both dark and light backgrounds.
- All 25 original hashes remained unchanged. No Unity scenes, prefabs, import settings, gameplay behavior or runtime tests were changed or run.

The existing PixelLab extension emits `handle-pose.lua:58` dialog warnings during Aseprite batch startup. The final generation and validation returned exit code 0, both completion markers were present, and saved-file validation passed. The extension was not modified.
