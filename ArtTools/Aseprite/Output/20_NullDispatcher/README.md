# NULL DISPATCHER — dedicated final boss visual

Four **160×160** top-down gameplay states built with Aseprite CLI + Lua. The larger canvas provides space for an integrated authority frame to separate around the enlarged approved Purple Core. It uses **18 visible colors across the complete set**, binary transparency, hard pixel edges and no antialiasing or gradients.

## Delivered art

- `NULL_Dispatcher.aseprite`: one editable source, four single-frame state tags, eleven layers.
- `NULL_Dispatcher_dormant_sealed.png`: shutters cover most of the inactive Purple Core; the shell and three functional sections form a controlled silhouette.
- `NULL_Dispatcher_active.png`: shutters withdrawn, active routing links and the entire approved active core visible.
- `NULL_Dispatcher_phase2_unbound.png`: enlarged overloaded core, separated shell quadrants and disconnected fragments.
- `NULL_Dispatcher_critical_exposed.png`: broken control register, missing compression jaw, severed routing clevis and a fractured overloaded core frame.
- `NULL_Dispatcher_States.png`: transparent **640×160**, four horizontal 160×160 cells in the above order.
- `NULL_Dispatcher.json`: frame rectangles, state tags and Unity import notes.

The tags are **Dormant - Sealed**, **Active**, **Phase 2 - Unbound**, and **Critical - Core Exposed**. These are four selectable poses, not a four-frame gameplay loop. Each source frame has a 1000 ms placeholder duration; choose the state in gameplay. The review GIF cycles poses only for comparison.

## Core provenance and visual construction

The core was **not redrawn or recolored**. Its original Housing, Energy and Details pixels are copied from the approved files in `Output/02_Core/purple_corrupted`:

| Boss state | Approved source | Source frame, one-based | Pixel scale | Placement on 160×160 canvas |
|---|---|---:|---:|---|
| Dormant / Sealed | `core_purple_corrupted_inactive.aseprite` | 1 | 1× | (48,48) |
| Active | `core_purple_corrupted_active.aseprite` | 1 | 1× | (48,48) |
| Phase 2 / Unbound | `core_purple_corrupted_overloaded.aseprite` | 1 | 2× | (16,16) |
| Critical / Core Exposed | `core_purple_corrupted_overloaded.aseprite` | 3 | 2× | (16,16) |

The 2× versions replicate each source pixel into a 2×2 block. Even pixels hidden by the sealed shutters remain present and editable on their original-derived layers. Active exposes 100% of the approved composited core pixels. Unbound and Critical use the existing overloaded source's bright and fractured frames, respectively.

The white/dark-gray shell uses the approved SYSTEM frame palette. Three sections share the surrounding frame: an upper control-register crown, a lower-left compression throat, and a lower-right phase-routing clevis. Their functions echo sector control, material recovery and phase routing without copying boss modules. Fragmented geometry and sparse displaced purple links add abnormal asymmetry; no Raider plates, red markings, organic forms or extra clean reactor design were introduced.

## Editable layers, bottom to top

1. Network Lines
2. Purple Core - Housing
3. Purple Core - Energy
4. Purple Core - Details
5. Main Shell
6. Control Section
7. Recovery Section
8. Phase Section
9. Outer Fragments
10. Corruption VFX
11. Damage

New geometry uses a two-pixel construction grid. The copied 1× core retains its original pixel grid. Each major functional section can be moved independently for later animation; the three core layers retain the source's internal separation. Void spaces and missing armor are genuinely transparent, with a few dark structural surfaces around them.

## Review images

- `NULL_Dispatcher_Comparison.png`: **1400×760**, four 2× nearest-neighbor views, four native-size views and core provenance.
- `NULL_Dispatcher_480x270_<state>.png`: four **480×270** previews. Each shows one boss at its exact native 160×160 canvas size, with a 32×32 player scale marker.
- `NULL_Dispatcher_480x270.gif`: cycles the four native previews, 1.5 seconds per pose.

Review backgrounds, text and the player scale marker are only in the review files. Gameplay sprites and the state sheet have transparent backgrounds. The marker is a review-only square, not a replacement player asset.

## Unity use

Import gameplay PNGs as Sprite (2D and UI), **PPU 32**, Point filter, compression None, mipmaps off and Full Rect. Use a centered pivot `(0.5,0.5)` for every state; front is up. All four share the same canvas and core center, so a state swap does not change the pivot. At PPU 32 the canvas spans 5 world units.

For the state sheet, slice a fixed **160×160** grid without trimming. JSON rectangles use top-left image coordinates; convert Y for Unity with `sheetHeight - frame.y - frame.h`. Use individual state PNGs or one selected tag/frame. Do not play all four tags as a looping attack animation. No Unity scene, prefab, importer or runtime changes were made, and combat playback has not been tested in Unity.

## Validation and repeatable generation

`validation.json` records successful reopening of the saved Aseprite file, exact PNG and sprite-sheet comparison (**204,800 pixels**), plus **307,200 core-layer pixel comparisons** against the copied approved sources. Checks also cover one-frame state tags, editable layers, binary alpha, the 18-color budget, native preview scale, clear state differences, canvas margins and actual shell removal in Critical.

`reference_hashes.json` verifies **510 existing files** across Input and approved Output folders 02–19 remain unchanged. Exact inspected paths and hashes are in `Scripts/null_dispatcher_references.json`. Input was inspected before generation; source files were copied into the run's `approved_copies` directory before Aseprite opened them.

From the project root, repeat with:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-NullDispatcher.ps1' -ReplaceGenerated
```

The executable is `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`. The script generates only `Output/20_NullDispatcher`, verifies reference hashes, checks Lua is UTF-8 without BOM, runs build and validation, then rechecks approved assets. Missing or changed sources stop generation. `-ReplaceGenerated` permits rebuilding this generator's own output folder; preserve manual edits before using it.

For an extracted package, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. Optional `-ToolRoot` sets the output workspace. Included reusable scripts are `Generate-NullDispatcher.ps1`, `build_null_dispatcher.lua`, `validate_null_dispatcher.lua`, `null_dispatcher_references.json`, and the unchanged shared `system_boss_pixel_helpers.lua`.

Successful build copies/logs: `Temp/null_dispatcher_20260930_083647_402`. The installed PixelLab extension emitted its pre-existing `handle-pose.lua:58` / `dlg` startup warning; both final Aseprite passes completed successfully with all checks passing. The extension was left unchanged.
