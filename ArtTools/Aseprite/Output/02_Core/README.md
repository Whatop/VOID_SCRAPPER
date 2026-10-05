# VOID_SCRAPPER core variant set

Five related families derived from the approved `Input/02_Core/core1.png` through `core9.png`.
The original diamond housing, emitted shapes, and numbered activation sequence are retained.

| Folder | Role | Special state |
| --- | --- | --- |
| green | Region boss A; clean and stable | highlighted |
| orange | Region boss B; clean and stable | highlighted |
| blue | Region boss C; clean and stable | highlighted |
| purple_corrupted | Curse / network / NULL; offset energy bands and circuit terminals | overloaded |
| gray_raider | Salvaged gray metal, diagonal patch, red marks, chips, weak flicker | overloaded |

## Asset layout

Each family contains six assets, with matching `.aseprite`, `.png`, and `.json` files:

| State | Frame size | Frames | PNG layout |
| --- | --- | --- | --- |
| inactive | 64 x 64 | 1 | 64 x 64 |
| activation | 64 x 64 | 9 | 576 x 64, left to right |
| active | 64 x 64 | 4 | 256 x 64, left to right |
| highlighted / overloaded | 64 x 64 | 4 | 256 x 64, left to right |
| icon | 32 x 32 | 1 | 32 x 32 |
| shard | 32 x 32 | 4 | 128 x 32, left to right |

Total: 30 layered Aseprite assets, 30 matching art PNGs, 30 timing/layout JSON files, and 115 frames.
Every Aseprite asset has Housing, Energy, and Details layers plus a state tag.
PNGs use full, untrimmed cells with transparent backgrounds and no padding.
The origin/pivot is consistently the canvas center (32,32 for main cores; 16,16 for icons/shards).

## Source treatment

- Main cores use exact 2x nearest-neighbor pixel replication from the 32 x 32 source grid.
- Icons use the original 32 x 32 grid. Shards isolate a chipped fragment of the source's lit diamond.
- Color ramps are quantized, with no smooth gradients or anti-aliasing. Alpha is either 0 or 255.
- Green, orange, and blue have stable emission. Purple keeps the housing anchored while energy bands glitch.
- Gray caps brightness, adds metal repairs and red identification marks, and flickers more weakly.
- Activation retains source order 1 through 9, including the bright start and inactive frame 7.
- Frame durations are newly authored because the source PNGs contain no timing metadata.
- Read the matching JSON for exact timing. Stable active states intentionally hold some repeated frames.

## Review files

`core_set_overview.png` is a labeled contact sheet. Its HIGHLIGHT column shows the special state for each family.
`core_set_motion_preview.gif` is a synchronized visual overview at 140 ms per preview step; use the individual asset JSON timings in production.
The GIF includes all nine activation poses. Static-state durations are placeholders for single-frame assets.

## Repeatable generation

Run from the Aseprite tooling root:

```powershell
.\Scripts\Generate-CoreSet.ps1
```

If this exact generated set already exists and you intend to rebuild it:

```powershell
.\Scripts\Generate-CoreSet.ps1 -ReplaceGenerated
```

The runner prints the exact input file list, verifies the nine approved SHA-256 hashes, copies sources into a fresh Temp run folder, and uses Aseprite CLI + Lua.
It refuses missing/changed inputs and refuses overwriting an unrecognized output folder.
Both Lua scripts are UTF-8 without BOM. Korean directory names were checked with Aseprite file I/O.

## Verification

`validation.json` records reopened Aseprite/PNG comparisons: canvas sizes, frame counts, layer counts, state tags, JSON timing, nonempty/unclipped silhouettes, palette size, and alpha.
The current build passes 394,240 pixel comparisons across 115 frames, with at most 12 visible colors in any frame.
The nine original SHA-256 hashes remain unchanged; see `source_manifest.json`.
The installed PixelLab extension emits a pre-existing batch-startup warning; generation and validation complete successfully.
