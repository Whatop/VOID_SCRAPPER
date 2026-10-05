# NULL DISPATCHER — Phase 2 / Unbound revision

Only **Phase 2 / Unbound (frame 3)** is revised. Dormant, Active and Critical retain identical pixels on all eleven source layers, identical state timing/tags, and byte-identical individual PNGs and native preview PNGs. The original approved pack remains in `Output/20_NullDispatcher`; this revision is saved separately in `Output/21_NullDispatcher_Phase2Revision`.

## Changes

- The core raster footprint changes from **128×128 to 104×104**: **18.75% smaller per axis**. Sampling uses nearest-neighbor on a 52×52 logical grid followed by exact 2× replication. The core is derived from the copied existing source, not redrawn.
- The visible bright-purple/white footprint measures **100×64 before, 80×52 after**: 20% narrower and 18.75% shorter, including the source's displaced bright pixels.
- All original core colors, binary opacity and peak brightness are retained. The entire set still uses 18 colors, with no antialiasing, gradients or blur.
- Shell quadrants move 2 pixels outward per axis; the control section moves 4 pixels upward; recovery and phase sections move 2 pixels outward and 2 pixels down.
- Floating fragments move 2–4 pixels outward and use brighter existing metal colors. Interrupted network links are brighter and more visible, with separated endpoint marks.
- No shell, functional-section or fragment pixels are removed from Phase 2. Its existing corruption marks and empty Damage layer are unchanged. Critical retains its missing armor, failed modules and unstable core presentation.

## Main files

- `NULL_Dispatcher.aseprite`: updated 160×160, four-state, eleven-layer editable source.
- `NULL_Dispatcher_phase2_unbound.png`: revised transparent Phase 2 sprite.
- `NULL_Dispatcher_States.png`: updated transparent 640×160 horizontal state sheet; 160×160 fixed cells.
- `NULL_Dispatcher.json`: unchanged frame rectangles, tags and import metadata.
- Other three state PNGs: unchanged copies included for a complete drop-in set.

The four single-frame tags remain **Dormant - Sealed**, **Active**, **Phase 2 - Unbound**, and **Critical - Core Exposed**. These are selectable gameplay poses, not a four-frame looping animation. Front is up, pivot is centered, canvas size remains 160×160.

## Native-size comparisons

- `NULL_Dispatcher_480x270.gif`: four 480×270 review frames, cycling the four states at 1.5 seconds each. Each boss is exactly 160×160; no downscaling.
- `NULL_Dispatcher_4State_Native_Comparison.png`: four 480×270 panels side by side, **1920×270** total, preserving native pixel scale.
- `NULL_Dispatcher_Phase2_BeforeAfter_480x270.png`: original and revised Phase 2 sprites side by side at native size in one 480×270 image.
- `NULL_Dispatcher_Comparison.png`: updated larger contact sheet, with only its Phase 2 image cells changed.
- `NULL_Dispatcher_480x270_<state>.png`: individual native review images; the other three are unchanged copies.

Review files have opaque backgrounds and labels. Gameplay PNGs have transparent backgrounds. Use Point filtering, PPU 32, no compression, mipmaps off, Full Rect and centered pivots in Unity. This revision changes no Unity assets or runtime code; Unity combat playback was not run.

## Validation and repeatability

`validation.json` records **2,084,800 pixel comparisons**, checking reopened Aseprite layers, unchanged states, exact nearest-neighbor core sampling, exported PNGs, state-sheet cells, native comparison panels, margins, brightness, palette, binary alpha and two-pixel construction. The unchanged three states each have **zero changed cel pixels** across eleven layers. Hardware pixel counts confirm Phase 2 has lost no structure, and Critical still has substantially less intact shell.

`reference_hashes.json` verifies **528 original files** in Input and approved Output folders 02–20 remain unchanged. The workflow opens only copied sources. The exact source is `Output/20_NullDispatcher/NULL_Dispatcher.aseprite`, SHA256 `723244B4CC29BBD974B510C074C099A4C2BE4EE24ABF6F00ABFD0CE2D65939F9`.

Included scripts are `Revise-NullDispatcherPhase2.ps1`, `revise_null_dispatcher_phase2.lua`, `validate_null_dispatcher_phase2.lua`, `null_dispatcher_phase2_references.json`, and the unchanged shared pixel helper. Lua files are UTF-8 without BOM.

From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Revise-NullDispatcherPhase2.ps1' -ReplaceGenerated
```

For an extracted package, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` folder. Optional `-ToolRoot` controls the output workspace. Missing or changed inspected references stop the run. `-ReplaceGenerated` permits rebuilding only this revision's own folder; preserve manual edits first.

Successful copies/logs: `Temp/null_dispatcher_phase2_20260930_084838_439`. The installed PixelLab extension emitted its existing `handle-pose.lua:58` / `dlg` warning; the final build and validation completed successfully. The extension was not modified.
