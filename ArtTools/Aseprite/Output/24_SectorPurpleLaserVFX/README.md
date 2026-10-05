# Sector Administrator — Phase 2 Purple Laser VFX

Three editable SYSTEM overdrive effects derived from the approved Region A green laser. Generated with Aseprite CLI 1.3.18.6 and UTF-8-without-BOM Lua. All existing approved art remains unchanged.

## Delivered families

| Family | Canvas / PNG strip | Animation | Pivot in Unity |
| --- | --- | --- | --- |
| `VFX_Sector_PurpleLaserWarning` | 64x32 / 256x32 | `Warning`: 4 x 120 ms, looping | (0, 0.46875) |
| `VFX_Sector_PurpleLaserBeam` | 64x32 / 256x32 | `Sustain`: 4 x 60 ms, looping | (0, 0.46875) |
| `VFX_Sector_PurpleLaserEmitter` | 32x32 / 128x32 | `Activate`: 40, 60, 70, 40 ms, one-shot | (0.25, 0.46875) |

Each family has an editable `.aseprite`, transparent `.png` strip, and `.json` import/timing metadata. Strips are untrimmed horizontal cells, numbered left to right. Four original SYSTEM layer names are retained: **Energy Shape, Bright Center, Technical Marks, Fragments**. Empty Fragments layers intentionally remain available for editing without introducing extra particles.

`VFX_Sector_PurpleLaserBeam_CoreSlice.png` is an optional 8x32 strip containing four 2x32 cross-sections. Stretch each selected slice along local X for a clean beam body without stretched decorative pixels. The corresponding rectangles, pivot and body widths are recorded in the beam JSON. The full 64x32 beam is intended for horizontal tiling.

## Visual relationship

The four active silhouettes and technical marker positions match the approved green Sustain frames exactly. Their green energy becomes violet; the central white-hot band stays six pixels thick across the full tile in every frame, including the narrow sustain pose. The beam body widths remain the approved 14, 18, 14 and 10 pixels; no larger outer silhouette or bloom is added.

The warning reuses the approved green warning's dotted axis, endpoint brackets and chevrons. Its dots advance two integer pixels each frame through a seamless four-frame loop. It contains no white pixels, compared with 384 white-hot pixels per active tile. Warning coverage is at most 168 pixels; the active beam ranges from 672 to 1184 pixels including its existing technical marks. The emitter peaks at 24x18 visible pixels inside a 32x32 canvas, then decays to a transparent final frame.

Palette comes directly from the approved Purple Overdrive boss: `452D66`, `8353B8`, `BA79FF`, `D9A5FF`, `FFF6FF`. Each effect uses at most four opaque colors from that editing palette. There are no red pixels, semitransparent painted glows, anti-aliasing, gradients, displaced NULL fragments or organic forms. Existing technical marks supply the subtle energy detail; a separate particle family would add unnecessary arena clutter.

## Inspected references

Approved source files:

- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\16_SystemBossVFX\VFX_Sector_LaserBeam.aseprite`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\16_SystemBossVFX\VFX_Sector_LaserTelegraph.aseprite`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\23_SectorAdministrator_Overdrive\sector_administrator_overdrive.png`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\02_Core\purple_corrupted\core_purple_corrupted_active.png` — color-language inspection only.

Production green beam and warning PNGs under `Assets\Art\SectorAdministrator` match their approved Output PNGs byte-for-byte. The copied Aseprite sources were also rendered and compared against every corresponding approved PNG cell before generation. No Purple Core pixels or boss pixels are baked into the gameplay effects.

## Unity use and integration boundary

Use **PPU 32**, Point filtering, no compression, no mipmaps, FullRect mesh, and untrimmed slices. The inspected production green beam and warning imports use the same pivots listed above. Their actual visual axis is top-left pixel coordinate **Y=17**, so substituting a generic centered pivot would move the effect by one pixel. The emitter's source attachment point is `(8,17)` in top-left coordinates. All effects point along local **+X**.

Tile the full beam along X and keep the cross-axis size tied to the intended damage band. The original body widths are 14/18/14/10 pixels; ignore the off-axis decorative pixels when calculating collision-aligned thickness. The 2px core slices can instead stretch along X while preserving each frame's transverse profile. Warning dots and technical marks should be tiled rather than stretched into long bars. Swap to the active clip and trigger the emitter only when the gameplay attack activates; hide the warning then. Remove the active beam when the attack ends. The warning loop does not define or alter pre-fire timing.

This task exports art only. No Unity assets, prefabs, code, materials, imports, damage widths, warning timings or rotation directions were changed. The inspected `SectorPartitionLane` currently chooses a solid LineRenderer while empowerment is positive. These textures therefore need a later presentation integration before they appear in Phase 2. The new four-frame `Sustain` clip is not a seven-frame drop-in for the existing Phase 1 Fire-plus-Sustain array. Metadata preserves the art contract without attempting to change that runtime owner.

## Review files

- `SectorPurpleLaser_Comparison.png`: approved green reference, purple warning, purple active beam and emitter; four labeled frames per row at integer 2x scale.
- `SectorPurpleLaser_480x270.png`: native-size active six-spoke composition with the approved boss and player reference above the beams.
- `SectorPurpleLaser_Warning_480x270.png`: native warning counterpart.
- `SectorPurpleLaser_480x270.gif`: 600 ms illustrative warning followed by rotating active spokes, including activation flash. This review uses a 40 ms GIF sampling interval; the ASE/JSON timings above are authoritative for the effect clips.

Native reviews use nearest-pixel rotation with no filtering. They demonstrate pixel-art readability, not Unity Play Mode behavior or a live gameplay capture. The preview's counterclockwise rotation and hold timing are illustrative; the game's existing phase controller should own actual direction and schedule.

## Validation and repeatable execution

`validation.json` records twelve frames and three tags checked after reopening the saved sources and PNGs. Checks cover ASE/PNG equivalence, binary alpha, palette limits, 2px construction grid, frame timing, tags and metadata, tile repetition, loop seams, continuous white core, exact green beam masks and warning marker masks, distinct warning/attack brightness, emitter decay/cleanup, core stretch slices, and native-size reference placement. `reference_hashes.json` contains 599 protected originals checked before and after generation.

Run from the project's `ArtTools\Aseprite\Scripts` directory:

```powershell
.\Generate-SectorPurpleLaser.ps1 -ReplaceGenerated
```

The runner inventories Input, checks all exact source paths and SHA-256 values, copies references into a fresh Temp directory, checks Lua UTF-8 without BOM, then runs the generator and independent Aseprite validator. It refuses missing/changed references and only replaces the output folder identified by this generator's own manifest. The approved shared pixel helper stays unchanged.

Existing PixelLab `handle-pose.lua` startup warnings may appear in batch logs. Both generator and validator completion are required; the warnings alone are not treated as successful generation or as an asset error.
