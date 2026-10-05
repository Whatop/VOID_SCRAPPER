# Raider Sniper Commander

Long-range Raider replacement boss. A narrow salvaged keel carries a long forked railgun, an offset binocular rangefinder, a port mine rack, and a mismatched starboard utility pod. White/gray repair plates and red faction paint follow the approved Raider family. The two previous boss hulls are comparison references only.

## Assets

All gameplay sprites are 128x128 RGBA, facing up, on a fixed canvas with an anchor at (64,64). No trimming. Pixel art is authored on a 64x64 construction grid and exported at exact 2x size. Alpha is either 0 or 255; there is no antialiasing, blur, gradient, or partial transparency.

| State | PNG | Layered Aseprite |
|---|---|---|
| Idle | `raider_sniper_commander_idle.png` | `raider_sniper_commander_idle.aseprite` |
| Target Lock / Rail Charged | `raider_sniper_commander_target_lock.png` | `raider_sniper_commander_target_lock.aseprite` |
| Critical Damage | `raider_sniper_commander_critical_damage.png` | `raider_sniper_commander_critical_damage.aseprite` |

- `raider_sniper_commander_states.aseprite`: all three states as single-frame tags, in the order above. The 200ms durations are inspection defaults; this is a state-pose set, not a finished looping animation.
- `raider_sniper_commander_states.png`: transparent 384x128 horizontal strip, three untrimmed 128x128 cells, no padding.
- `raider_sniper_commander_comparison.png`: presentation sheet with enlarged and native-size states plus six approved faction references. This preview intentionally has an opaque background.
- `manifest.json`: palette, state order, filenames, dimensions, layers and descriptions.
- `validation.json`: saved-file validation results.
- `reference_hashes.json`: SHA256 records for all 18 Input files and the six approved references.

## State differences

Idle has dim optics, capped mine indicators and closed rail sleeves. Target Lock extends the muzzle, slides the receiver sleeves apart, illuminates the segmented rail conductors and binocular optics, and charges the utility cells. Small hard-edged muzzle brackets signal a precision shot without drawing a fired laser beam.

Critical Damage physically breaks the left rail, removes half the rangefinder, ruptures the utility pod and removes most of the starboard hull plate. Exposed wiring and a few localized shorts replace those parts. It has no central exposed energy core.

## Editable layers

1. Main Hull
2. Exposed Machinery and Wiring
3. Railgun
4. Targeting Sensor
5. Left Mine Rack
6. Right Utility Pod
7. Salvaged Armor
8. Cockpit and Faction Paint
9. Engine
10. Target Lock VFX
11. Damage and Debris

The layers share the fixed canvas and can be separated for animation. Small debris remains in the Damage and Debris layer. The cockpit reuses the approved Elite's small rectangular motif; the hull, weapon, sensor and utility geometry are newly authored in Lua.

## References and preservation

Approved reference paths relative to `ArtTools\Aseprite`:

- `Output\03_RaiderEnemy\raider_basic.png` (64x64)
- `Output\03_RaiderEnemy\raider_shotgun.png` (64x64)
- `Output\03_RaiderEnemy\raider_sniper_charging.png` (64x64)
- `Output\03_RaiderEnemy\raider_elite.png` (64x64)
- `Output\09_RaiderAssaultCommander\raider_assault_commander_idle.png` (128x128)
- `Output\10_RaiderSalvageCarrier\raider_salvage_carrier_idle.png` (128x128)

All were inspected before authoring. Aseprite reads byte-verified copies under the timestamped Temp run directory. No original Input or approved Output file was modified. The 13-color maximum palette comes from the approved Raider sprites: seven metal shades, three faction reds and three precision blues.

## Repeatable Aseprite CLI workflow

Project installation: `ArtTools\Aseprite\Output\11_RaiderSniperCommander` and the accompanying files in `ArtTools\Aseprite\Scripts`.

From the project root, rebuild this generated set with:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-RaiderSniperCommander.ps1' -ReplaceGenerated
```

The helper uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe` in batch mode. It inventories Input, checks the six exact reference hashes, copies them, validates Lua as UTF-8 without BOM, runs generation and saved-file validation, then verifies the originals again. Logs and reference copies are retained under `Temp\raider_sniper_commander_<timestamp>`.

For an extracted package, provide `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` optionally selects a separate build directory. Without `-ReplaceGenerated`, the runner refuses to overwrite an existing set; with it, the generator ID must match.

The scripts are `Generate-RaiderSniperCommander.ps1`, `build_raider_sniper_commander.lua`, `validate_raider_sniper_commander.lua`, and the unchanged shared `raider_boss_pixel_helpers.lua`.

## Verification

- Reopened all four Aseprite files and checked dimensions, 11 layers and three single-frame master tags.
- 147,456 pixel comparisons matched each individual source, PNG, master frame and sprite-sheet cell.
- All sprite alpha is binary, all pixel blocks are exact 2x, and each state uses at most 13 approved colors.
- Idle silhouette is 72x116 visible pixels, distinctly narrower than both prior Raider bosses; opaque-mask differences are 5,500 pixels against Assault Commander and 5,616 against Salvage Carrier.
- Charged state has 404 blue pixels versus 52 in Idle, concentrated on the forward weapon and optics. Critical Damage reduces light starboard armor pixels from 160 to 48 and physically breaks the sensor and utility pod silhouettes.
- Charged and damaged states differ at 445 pixels on the reduced 64x64 construction grid. Dark/light 64x64 visual inspection was completed.
- All 24 original-file hashes remained unchanged. No Unity assets, import settings or gameplay code were edited or tested.

The installed PixelLab extension emits its pre-existing `handle-pose.lua:58` dialog warning during batch startup. Aseprite returned exit code 0 for generation and validation; completion markers and saved-file checks passed. The extension was not modified.
