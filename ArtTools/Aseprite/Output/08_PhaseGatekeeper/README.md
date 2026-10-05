# PHASE GATEKEEPER

SYSTEM / Region C / blue. A narrow navigation spear with a central oval lens, two crescent phase emitters and sliding lens guards. Its geometry is newly authored; Region A and B bosses are used only for silhouette comparisons.

## Generated files

| State | Editable source | Transparent sprite |
| --- | --- | --- |
| Idle | phase_gatekeeper_idle.aseprite | phase_gatekeeper_idle.png |
| Phase Lock / portal charged | phase_gatekeeper_phase_lock.aseprite | phase_gatekeeper_phase_lock.png |
| Navigation lens exposed | phase_gatekeeper_lens_exposed.aseprite | phase_gatekeeper_lens_exposed.png |

All canvases are 128x128, with front facing up. Keep the shared anchor at (64,64), normalized (0.5,0.5), and do not trim between states. The lens center is (63.5,65.5) in top-left image coordinates.

- `phase_gatekeeper_states.aseprite`: three tagged single-frame poses (`idle`, `phase_lock`, `lens_exposed`), ten editable layers. Each frame is stored at 200 ms for inspection; this is not a finished looping animation.
- `phase_gatekeeper_states.png`: transparent 384x128 horizontal strip in the same order, 128x128 cells without padding.
- `phase_gatekeeper_comparison.png`: enlarged and native-size views, Blue Precision / Blue Navigation references, and Region A/B comparisons. This presentation image has an opaque background.
- `manifest.json`: palette, descriptions, layers, state order and alignment.
- `validation.json`, `reference_hashes.json`, `generation_complete.txt`: validation results, unchanged-source hashes and completion marker.

## Visual and gameplay language

Idle keeps the small focusing optic visible inside an oval protective frame. A pointed forward guidance needle and flat-capped rear routing fork establish direction. The two crescent emitters are smaller than the central body.

Phase Lock activates external blue portal brackets, emitter apertures and longitudinal routing signals. The lens guards remain in their idle positions, keeping the central aperture unchanged.

Lens Exposed slides the left/right protective vanes outward by six pixels each. The large oval optic opens while external phase effects switch off and the emitters dim. The same physical guard pieces remain on editable layers, with their translation verified pixel-for-pixel.

The blue palette, diamond focusing module and precise optical/routing language come from the approved blue family. The light spear construction is independent of Sector Administrator's four-module layout and Defense Overseer's broad armored deck. No boss silhouettes were recolored or reused.

## Editable layers

1. Main Body
2. Forward Needle and Tail
3. Central Lens
4. Left Phase Emitter
5. Right Phase Emitter
6. Lens Vane L
7. Lens Vane R
8. Routing Energy
9. SYSTEM Registration
10. Phase Lock VFX

Hard-edged blue effects are isolated from the hull for later animation. All art uses an exact 2x pixel grid, fully opaque palette colors and transparent background pixels; no partial alpha, anti-aliasing or smooth gradients. Idle uses 12 colors; the other two states use 13.

No Unity assets or gameplay behavior were changed. Timing, attack effects and transitions remain for later animation/integration.

## Rebuild

From the existing project's `ArtTools\Aseprite` directory:

```powershell
& .\Scripts\Generate-PhaseGatekeeper.ps1 -ReplaceGenerated
```

Scripts: `Generate-PhaseGatekeeper.ps1`, `build_phase_gatekeeper.lua`, `validate_phase_gatekeeper.lua`, and the unchanged shared `system_boss_pixel_helpers.lua`. The helper contains generic raster/font routines only. Lua files are UTF-8 without BOM.

The runner verifies exact approved paths and SHA256 hashes, creates reference copies in a timestamped Temp directory, invokes Aseprite CLI, and validates the saved files. It refuses to overwrite an unknown output folder. Original source files are never opened for saving. Reproduction uses the existing project's blue family and Region A/B comparison PNGs; they are not duplicated in the archive.

## Validation

- 147,456 pixel comparisons: state PNGs match reopened Aseprite poses, tagged master frames and strip cells.
- 65,536 guard comparisons verify unchanged protection in Phase Lock and exact six-pixel outward translation in Lens Exposed.
- Correct dimensions, layers, tags, binary alpha, limited palette, bilateral symmetry and consistent 2x grid.
- Idle bounds: 76x118 within the 128x128 canvas; Phase Lock effects widen this to 92x118.
- Exposed central blue lens area is 800 pixels versus 256 in Phase Lock; external blue area falls from 480 to 16 pixels.
- Phase Lock and Lens Exposed differ by 522 pixels on the 64x64 construction grid. Dark/light-background 64px views were visually inspected and saved in Temp.
- Idle silhouette differs from Region A by 6,104 pixels and Region B by 5,024 pixels.
- All 18 Input files and six approved/comparison reference PNGs remain unchanged by SHA256.

The existing PixelLab extension prints `handle-pose.lua:58` at batch startup. Both final Aseprite processes finish successfully and generated-file validation passes; the extension was not modified.
