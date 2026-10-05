# RAIDER ASSAULT COMMANDER

Raider replacement boss: a scavenged assault warship with an uneven ram bow, port rotary cannon, starboard twin breacher, offset rebuilt engines and patched central hull. This is a newly authored Raider design, using the approved Raider palette and rectangular cockpit motif.

## Generated files

| State | Editable source | Transparent sprite |
| --- | --- | --- |
| Idle | raider_assault_commander_idle.aseprite | raider_assault_commander_idle.png |
| Weapons Hot / Assault Ready | raider_assault_commander_weapons_hot.aseprite | raider_assault_commander_weapons_hot.png |
| Critical Damage | raider_assault_commander_critical_damage.aseprite | raider_assault_commander_critical_damage.png |

All canvases are 128x128. Front faces up. Keep the same center anchor (64,64), normalized (0.5,0.5), without trimming between states. The asymmetric ship is deliberately not mirrored around its anchor.

- `raider_assault_commander_states.aseprite`: three single-frame tags (`idle`, `weapons_hot`, `critical_damage`) with ten editable layers. Frames are stored at 200 ms for inspection; this is a pose/state library, not a finished looping animation.
- `raider_assault_commander_states.png`: transparent 384x128 strip, in the same order, with 128x128 cells and no padding.
- `raider_assault_commander_comparison.png`: enlarged and native-size poses, plus all four approved Raider references. This presentation image has an opaque background.
- `manifest.json`: palette, state descriptions, layer mapping, orientation and alignment.
- `validation.json`, `reference_hashes.json`, `generation_complete.txt`: validation results, unchanged-source hashes and completion marker.

## State and faction language

Idle uses salvaged light-gray plates over dark machinery, uneven red bands, visible cable clamps and mismatched weapons. The cockpit is a rectangular red Raider module above the chest. The center contains mechanical equipment and repair plates, with no glowing sector core.

Weapons Hot extends the rotary barrels by eight pixels and the breacher barrels by six pixels, lights red firing indicators and exposed weapon-feed vents, and activates hot engine plumes. These cues stay on the guns and engines.

Critical Damage removes chest and port-side armor, breaks the shorter ram plate and one barrel, and disables the port engine. The breach reveals rods, a gearbox, cut wiring and small localized sparks. It does not expose a clean energy reactor.

Only colors from the approved Raider set are used: seven grays, three faction reds, and localized heat colors from the Shotgun Raider. The states have 10, 12 and 12 visible colors. SYSTEM sprites are used only by validation to compare silhouettes; the art generator never loads them.

## Layers and later animation

1. Main Hull
2. Exposed Machinery and Cables
3. Left Rotary Weapon
4. Right Twin Breacher
5. Engine Assemblies
6. Salvaged Armor Plates
7. Cockpit and Command Mast
8. Red Faction Paint
9. Weapon Heat and Engine Plumes
10. Critical Damage VFX

Weapons, armor, engines and effects remain editable separately. A few indicators and markings are on shared overlay layers, so include those overlays when extracting modules. All pixels use an exact 2x grid and binary alpha. There are no gradients or anti-aliased edges.

No Unity assets, replacement-boss spawning logic or combat behavior were modified. Timing, attacks and transitions remain for later animation/integration.

## Rebuild

From the existing project's `ArtTools\Aseprite` directory:

```powershell
& .\Scripts\Generate-RaiderAssaultCommander.ps1 -ReplaceGenerated
```

Required scripts: `Generate-RaiderAssaultCommander.ps1`, `build_raider_assault_commander.lua`, `validate_raider_assault_commander.lua`, and `raider_boss_pixel_helpers.lua`. The helper contains generic raster/font routines only. Lua files are UTF-8 without BOM.

The runner checks exact reference paths and SHA256 hashes, copies references into a timestamped Temp folder, runs Aseprite CLI + Lua, and verifies the results. It refuses to overwrite an unrecognized output folder. The archive uses the existing project's reference assets rather than duplicating them.

## Verification

- 147,456 pixel comparisons: PNG poses match reopened Aseprite sources, tagged master frames and sprite-sheet cells.
- Correct 128x128 canvases, tags, ten layers, binary alpha, palette, common alignment and 2x grid.
- A 118-pixel-wide silhouette, unequal weapon/engine layouts, and strong red faction markings.
- Critical central light plating falls from 420 to 44 pixels; dark machinery increases from 852 to 1,116 pixels in the same region.
- Only 20 heat-colored pixels appear in the damaged central area, keeping sparks localized.
- Hot versus critical differs by 641 pixels at 64x64. Dark/light-background 64px renders were visually checked and retained in Temp.
- Silhouette comparisons distinguish this design from all three SYSTEM bosses.
- All 18 Input files and seven Raider/comparison reference PNGs remain unchanged by SHA256.

The pre-existing PixelLab extension prints `handle-pose.lua:58` during batch startup. Both final Aseprite runs exit successfully and all output checks pass; the extension was left unchanged.
