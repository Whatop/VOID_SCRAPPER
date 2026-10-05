# NULL DISPATCHER — Signature VFX Pack

**7 editable VFX families, 8 frame tags, 45 authored frames.** Aseprite CLI + Lua; transparent gameplay PNGs, binary alpha, hard pixel edges and a seven-color purple/white/void palette. Designed for native 480×270 presentation at PPU 32. No approved boss sprite or existing VFX was changed.

## Assets and playback

Each filename prefix has a layered `.aseprite`, a transparent horizontal `.png` sprite sheet, and matching `.json` frame/timing metadata.

| Filename prefix | Frame canvas | Frames | Tags | Playback |
|---|---|---:|---|---|
| `VFX_NULL_LaserRain_Telegraph` | 32×64 | 6 | Warning | 720 ms, then hand off |
| `VFX_NULL_LaserRain_Beam` | 32×64 | 6 | Fire / Release | Fire 240 ms + Release 100 ms |
| `VFX_NULL_CompressionDispatch` | 64×64 | 6 | Compress | 480 ms one-shot |
| `VFX_NULL_PhaseRedirectCorruption` | 64×64 | 5 | Redirect | 280 ms one-shot |
| `VFX_NULL_InterventionBeam` | 96×32 | 8 | Pull | 640 ms loop |
| `VFX_NULL_CoreGlitchBurst` | 64×64 | 6 | Burst | 400 ms one-shot |
| `VFX_NULL_CriticalLoop` | 64×64 | 8 | Loop | 800 ms loop |

The separate `VFX_NULL_LaserRain_Beam_Fire.png` and `_Release.png` strips are also included. `VFX_NULL_InterventionBeam_Tile32.png` contains eight 32×32 repeat cells in a 256×32 strip.

Frame durations are authoritative in the Aseprite files and JSON:

- Rain Warning: 160, 140, 120, 120, 100, 80 ms.
- Rain Beam: 40, 60, 60, 80, 60, 40 ms. Fire is frames 1–4; Release is frames 5–6.
- Compression: 100, 100, 100, 80, 60, 40 ms.
- Redirect: 40, 60, 60, 80, 40 ms.
- Intervention: eight frames at 80 ms each.
- Core Burst: 60, 60, 60, 80, 100, 40 ms.
- Critical: eight frames at 100 ms each.

**Intentional transparent cleanup frames:** Rain Beam 6, Compression 6, Redirect 5, Core Burst 6. These are required endings, not failed exports. Warning ends visibly and must be hidden at handoff; it is not a loop. Pull and Critical loop continuously without an empty frame.

## Visual and placement notes

**Rain warning/beam.** A single vertical lane uses corrupted register marks, interrupted address lines and displaced packets. Both share the strike axis at image X=16 and centered pivot. Warning contains no pale-white or white attack pixels. Its center becomes more legible during the countdown; after 720 ms, hide it and play Beam/Fire at the same position. Fire has a continuous white strike axis with broken displaced edges; Release is visual residue. Gameplay owns damage timing. Spawn separate lane instances for a rain pattern; the sheet itself contains one lane per frame.

Both rain assets repeat vertically every 16 pixels, including edge alignment. Repeat whole 32×64 frame segments end-to-end for longer lanes. Keep the width fixed and align placement to integer pixels. The nontransparent top/bottom endpoints are intentional tile boundaries.

**Compression.** Four misregistered pressure jaws and resource-like packets close inward. The field contracts on every visible frame, then leaves a short compact terminal flash. It does not use an expanding circular explosion silhouette.

**Redirect.** A horizontal incoming route bends toward the upper right through split, offset aperture halves. The displaced afterimage arrives after the white discharge and dissolves into a few fragments. Rotate the effect as a whole to match the redirected path; the supplied orientation is +X incoming, upper-right outgoing in image coordinates.

**Intervention.** White payload blocks, purple grips and broken register rails read as a forced transport link. Place the emitter at local X=0; packets travel toward it from the right, moving **4 native pixels left per frame**, including the loop seam. The entire pattern repeats every 32 pixels horizontally. Use the supplied 32×32 tile strip to extend its length while preserving pixel size and beam height. The full 96×32 strip shows three repeats. Stop the loop when the connection ends.

**Core Burst.** Only the approved Purple Core's Energy layer supplies the central pulse. The peak frame preserves `core_purple_corrupted_overloaded.aseprite`, Energy layer, frame 1 exactly. No Housing, Details or boss-body pixels are baked in. A compact pulse grows into that peak, breaks into displaced horizontal bands, then disperses into broken square fragments.

**Critical.** A light overlay of broken links and sparse fragments, with white flashes only on frames 2 and 6. Maximum occupied area is 248 pixels out of 4096. It can be positioned over damaged network hardware without replacing its silhouette; stop it when the damaged state ends.

All seven effects use newly authored geometry except for the deliberately copied Core Energy pulse. Existing SYSTEM VFX were used for visual comparison, not recolored. NULL laser and redirect masks were checked against the corresponding SYSTEM reference masks and differ structurally.

## Editable layers

1. Corrupted Geometry
2. Energy / White Core
3. Displaced Packets
4. Void / Residue

Most geometry uses a two-pixel construction grid. Core Burst retains the source energy's native one-pixel detail, with integer displacement and sampling. The palette is `35204F`, `603785`, `9650C1`, `CF77EB`, `F0B3FB`, `FCE9FF`, `151223`; each individual effect uses no more than seven colors. No partial alpha, blurred glow, gradients or subpixel transforms are used.

## Unity import

Use Sprite (2D and UI), Multiple for the sheets, **PPU 32**, Point filtering, compression None, mipmaps off and Full Rect. Slice fixed cells at the canvas size in the table, without trimming. The intervention tile sheet uses 32×32 cells. JSON rectangles are top-left-based; convert Y for Unity with `sheetHeight - frame.y - frame.h`.

Pivots are centered for every effect except Intervention, which uses `(0,0.5)` at the left emitter. JSON includes normalized Unity pivots. Align telegraph and beam pivots and rotate both together. Keep tiling at native pixel size; avoid scaling entire beams into blurry noninteger lengths. Runtime code, hitboxes, prefabs and Unity importer settings were not modified. Unity combat playback has not been run.

## Review and validation

- `NullSignatureVFX_ContactSheet.png`: all 45 frames, checkerboard backgrounds and durations. Most sprites are shown at 2× nearest-neighbor size; the wide intervention beam is shown at 1×.
- `NullSignatureVFX_480x270.png`: native-size peak comparison. Three rain-lane instances illustrate placement; other effects appear once.
- `NullSignatureVFX_480x270.gif`: native preview, **3200 ms / 160 frames at 20 ms**, with warning followed by attack, separate one-shots, and both loops. The review timing is illustrative; gameplay uses the actual clip timings above. The duration contains complete cycles of both looping effects.
- `manifest.json`: asset inventory, frames, tags, palette, pivots, tiling and playback notes.
- `validation.json`: successful saved-source/PNG verification, **172,032 exported pixel comparisons**, tag ranges, timings, binary alpha, palette limits, intended clipping boundaries, shrinking compression bounds, cleanup frames, loop seams, exact intervention flow and exact copied core Energy pixels.
- `reference_hashes.json`: **548 existing files** across Input and approved Output folders 02–21 verified unchanged, including the approved NULL Dispatcher sprites.

Review backgrounds and labels are not included in gameplay sheets. The warning has zero white pixels; the beam has 256 white pixels per Fire frame. The core peak is intentionally brighter and larger than its setup/residue frames, while the critical overlay stays lightweight.

## References and repeatable generation

The Input directory was inspected first. Exact inspected paths, dimensions and SHA256 hashes are recorded in `Scripts/null_vfx_references.json`. References include the revised NULL master and Phase 2/Critical PNGs in `Output/21_NullDispatcher_Phase2Revision`, the approved Purple Core active/overloaded sources, the Curse dash strip, and the SYSTEM laser/redirect effects for comparison.

All references are copied before Aseprite opens them. Lua files are UTF-8 without BOM. From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-NullSignatureVFX.ps1' -ReplaceGenerated
```

For an extracted package, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` optionally sets a separate output workspace. The runner verifies inspected hashes, uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`, generates only `Output/22_NullSignatureVFX`, validates saved results and rechecks originals. Missing or changed references stop generation. `-ReplaceGenerated` rebuilds this generator's own folder; preserve manual edits first.

Included task scripts: `Generate-NullSignatureVFX.ps1`, `build_null_signature_vfx.lua`, `validate_null_signature_vfx.lua`, `null_vfx_references.json`, and the unchanged shared `system_boss_pixel_helpers.lua`.

Successful copies/logs: `Temp/null_signature_vfx_20260930_115259_748`. The installed PixelLab extension emitted its existing `handle-pose.lua:58` / `dlg` startup warning. Final generation and validation completed successfully; the extension was not modified.
