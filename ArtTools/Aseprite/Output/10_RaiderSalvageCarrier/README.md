# RAIDER SALVAGE CARRIER

Raider replacement harvesting boss. A broad, rear-heavy cargo hauler with mismatched storage modules, an open intake conveyor, articulated grapple, horseshoe magnet and two utility drones. The geometry is newly authored for an industrial role; it has no oversized gun mounts.

## Generated files

| State | Editable source | Transparent sprite |
| --- | --- | --- |
| Idle | raider_salvage_carrier_idle.aseprite | raider_salvage_carrier_idle.png |
| Salvage Active | raider_salvage_carrier_salvage_active.aseprite | raider_salvage_carrier_salvage_active.png |
| Cargo Overload / Critical Damage | raider_salvage_carrier_cargo_overload.aseprite | raider_salvage_carrier_cargo_overload.png |

All canvases are 128x128. Front faces up. Keep the shared anchor at (64,64), normalized (0.5,0.5), and retain the full canvas as the collectors change pose. The unequal storage modules and arm layouts are intentional Raider asymmetry.

- `raider_salvage_carrier_states.aseprite`: three single-frame tags (`idle`, `salvage_active`, `cargo_overload`), with twelve editable layers. Each pose is stored at 200 ms for inspection; this is a state library, not a finished looping animation.
- `raider_salvage_carrier_states.png`: transparent 384x128 horizontal strip in the same order, 128x128 cells, no padding.
- `raider_salvage_carrier_comparison.png`: enlarged and native-size poses alongside all five approved Raider references. This presentation image has an opaque background.
- `manifest.json`: palette, descriptions, orientation, alignment, layer mapping and export order.
- `validation.json`, `reference_hashes.json`, `generation_complete.txt`: output validation, unchanged-source hashes and completion marker.

## Gameplay-readable poses

Idle: broad cargo lids are strapped shut, the grapple and magnet are parked, both utility drones are docked, and the intake rollers are clear.

Salvage Active: the grapple extends and opens around a scrap fragment; the horseshoe magnet swings inward and attracts a plate. Orange industrial indicators light the conveyor and collector tips. Scrap appears on the processing rollers, and one utility drone undocks to assist over the intake.

Cargo Overload: cargo lids rupture, revealing mixed ingots, batteries, compacted scrap and exposed machinery. Damaged stored cells leak localized heat, sparks and debris; one arm sags and a magnet pole is broken. There is no clean central energy core. Red paint remains on the welded frame after the cargo covers fail.

The hauler retains the approved Raider gray/red palette, patched armor, rectangular cockpit and exposed cable language. Its mass is concentrated around rear storage sections rather than the Assault Commander's ram bow and weapon deck.

## Editable layers

1. Main Hull
2. Cargo Contents and Machinery
3. Port Cargo Armor
4. Starboard Cargo Armor
5. Left Grapple
6. Right Magnet Crane
7. Intake Conveyor
8. Bridge and Docking Rails
9. Utility Drones
10. Engine Assemblies
11. Industrial Indicators
12. Damage and Debris

Collectors, cargo covers, contents, drone poses, engines and effects can be adjusted separately. Some held scrap and frame markings are on Industrial Indicators, so include that overlay when extracting modules. Art uses an exact 2x pixel grid, hard edges and binary alpha. Idle uses 10 visible colors; the other states use 12, all from the approved Raider palette.

No Unity assets, resource-stealing behavior, replacement-boss spawning, drone AI or inventory systems were changed. These files provide art poses for later animation and gameplay integration.

## Rebuild

From the existing project's `ArtTools\Aseprite` directory:

```powershell
& .\Scripts\Generate-RaiderSalvageCarrier.ps1 -ReplaceGenerated
```

Required scripts: `Generate-RaiderSalvageCarrier.ps1`, `build_raider_salvage_carrier.lua`, `validate_raider_salvage_carrier.lua`, and the unchanged shared `raider_boss_pixel_helpers.lua`. The helper provides generic raster/font routines only. All Lua files are UTF-8 without BOM.

The runner verifies the exact approved reference paths and SHA256 hashes, creates copies in a timestamped Temp directory, invokes Aseprite CLI + Lua, and validates the saved output. It refuses to overwrite an unknown output folder. The archive uses the existing project references rather than duplicating those originals.

## Verification

- 147,456 pixel comparisons: PNG states match reopened Aseprite poses, tagged master frames and strip cells.
- Correct dimensions, twelve layers, tags, binary alpha, common alignment, limited palette and exact 2x pixels.
- Each pose has substantially more occupied area across the rear cargo deck than the forward collection area.
- Collector changes are physical: 772 grapple silhouette pixels and 444 magnet silhouette pixels differ between Idle and Salvage Active; the utility drone also moves.
- Cargo light plating falls from 1,104 to 312 pixels in Overload; damaged stored-resource heat increases to 156 pixels within the cargo regions.
- Salvage Active and Overload differ by 1,065 pixels at 64x64. Dark/light-background 64px renders were visually inspected and retained in Temp.
- Idle silhouette differs from Assault Commander by 4,756 pixels.
- All 18 Input files and five approved Raider reference PNGs remain unchanged by SHA256.

The existing PixelLab extension prints `handle-pose.lua:58` during batch startup. Both final Aseprite runs exit successfully and all output checks pass; the extension was left unchanged.
