# VOID SCRAPPER — Core Gameplay VFX Pack

14 editable Aseprite families, 18 animation clips, 85 frames. Built and exported with Aseprite CLI + Lua for a 480×270 presentation at 32 pixels per unit. All gameplay sheets have transparent backgrounds, hard pixel edges, a limited palette, and a consistent 2-pixel construction grid. Fading uses discrete color, shape and fragment changes; alpha is either 0 or 255.

## Animation inventory

Times below are per-frame milliseconds in playback order. Frame counts include intentional transparent cleanup frames.

| Filename prefix | Frame size | Frames | Frame durations (ms) | Total | Playback |
|---|---:|---:|---|---:|---|
| VFX_Player_MachineGunMuzzle | 32×32 | 3 | 30, 40, 30 | 100 ms | One shot |
| VFX_Player_ShotgunMuzzle | 32×32 | 4 | 45, 50, 60, 35 | 190 ms | One shot |
| VFX_Player_SniperMuzzle | 64×32 | 4 | 35, 45, 50, 30 | 160 ms | One shot |
| VFX_Raider_Muzzle | 32×32 | 3 | 40, 45, 35 | 120 ms | One shot |
| VFX_Hit_Generic | 32×32 | 4 | 35, 45, 60, 30 | 170 ms | One shot |
| VFX_Explosion_Small | 32×32 | 5 | 40, 55, 55, 65, 35 | 250 ms | One shot |
| VFX_Explosion_Medium | 64×64 | 6 | 45, 60, 70, 80, 85, 40 | 380 ms | One shot |
| VFX_Player_Dash | 32×32 | 4 | 40, 50, 60, 30 | 180 ms | One shot |
| VFX_Player_Dash_Curse | 32×32 | 4 | 40, 50, 60, 30 | 180 ms | One shot |
| VFX_EnemyArrival_Telegraph | 64×64 | 6 | 120, 120, 120, 100, 100, 80 | 640 ms | Handoff |
| VFX_EnemyArrival_Impact | 64×64 | 4 | 45, 60, 75, 40 | 220 ms | One shot |
| VFX_ShieldHit | 64×64 | 4 | 40, 60, 75, 35 | 210 ms | One shot |
| VFX_PickupSparkle | 32×32 | 4 | 110, 80, 100, 190 | 480 ms | Loop |
| VFX_CoreActivation | 64×64 | 6 per variant | 40, 50, 65, 75, 80, 40 | 350 ms each | One shot |

## Files and editable organization

Each family has an `.aseprite`, a transparent `.png` sheet, and JSON frame rectangles, durations and import hints. Sources have four named editable layers: **Primary Shape**, **Highlights**, **Fragments**, and **Secondary Marks**. Single-clip sources use the `Default` tag.

Core activation uses one 30-frame source with five six-frame tags: Neutral (1–6), Green (7–12), Orange (13–18), Blue (19–24), Purple (25–30). All variants share the neutral master's silhouette and timing. `VFX_CoreActivation.png` is a 384×320 atlas: six columns, five rows in that order. Five additional color-named PNGs are individual 384×64 strips. Other families use a single horizontal strip with fixed-size, untrimmed cells and no padding between cells.

`manifest.json` records the complete inventory, layers, pivots, playback modes and timings. `validation.json` records export checks. `reference_hashes.json` records the unchanged Input and selected approved-reference hashes.

## Unity slicing and placement

Use Sprite (2D and UI), Multiple, **PPU 32**, Point filtering, compression None, mipmaps off, Full Rect mesh and Clamp wrapping. Slice by the frame sizes above; retain empty cleanup cells. PNG/JSON coordinates start at the top left. To map a JSON rectangle into Unity's bottom-left texture coordinates, use `y = sheetHeight - frame.y - frame.h`. Aseprite frame durations and the JSON timing values are authored timing data, not automatically imported Unity AnimationClips.

Use stepped sprite swaps at the cumulative authored times. Do not replace the timings with a single arbitrary FPS. A 200 FPS AnimationClip sample grid can represent all authored 5 ms timing increments if building clips manually. Only Pickup Sparkle should have Loop Time enabled. One-shots end on a fully transparent frame; stop or return the effect to its pool after the complete duration. The telegraph deliberately ends on a small arrival marker: hide it and start Arrival Impact at 640 ms, then clear the impact after its 220 ms duration.

Pivots below are local image coordinates measured from the top left. The JSON also supplies normalized Unity pivots using `(x / width, 1 - y / height)`.

| Effect | Pixel pivot | Unity normalized pivot | Orientation |
|---|---|---|---|
| Machine gun, shotgun, Raider muzzle | 8, 16 | 0.25, 0.5 | Muzzle points +X |
| Sniper muzzle | 8, 16 | 0.125, 0.5 | Muzzle points +X |
| Both player dashes | 24, 16 | 0.75, 0.5 | Travel +X, trail extends -X |
| Shield hit | 48, 32 | 0.75, 0.5 | Local hit surface faces +X |
| Other 32×32 effects | 16, 16 | 0.5, 0.5 | Centered |
| Other 64×64 effects | 32, 32 | 0.5, 0.5 | Centered |

Use integer screen-pixel positions and integer presentation scaling. Authored movement contains no subpixel transforms. The dash silhouette follows the inspected 16×16 square player. Shield Hit is a localized facet/arc placed at a hit point, not a persistent shield sphere. No Unity assets, import settings or runtime code were changed; gameplay integration has not been run in Unity.

## Review assets

- `VFX_ContactSheet.png`: all 18 clips and all 85 frames, with frame durations and checkerboard cleanup cells. 32-pixel effects are enlarged by exactly 2× for this sheet.
- `VFX_480x270_Preview.png`: native-resolution peak-frame comparison with the copied player sprite for placement context.
- `VFX_480x270_Preview.gif`: native-resolution animated review board, 50 frames at 20 ms each, repeating every second. This board has an opaque review background and is not a gameplay sheet. Its sampling is for review; source animations preserve the exact timings above. The telegraph cell hands off to Arrival Impact.

## Inspected references and preservation

The existing Input folder was inventoried before generation. The following files were verified and copied into this run's `Temp/gameplay_vfx_*/approved_copies` before use. Paths are relative to `ArtTools/Aseprite`:

- `Input/01_Player/Px_Player.png` — 16×16 square player silhouette.
- `Output/03_RaiderEnemy/raider_basic.png` — approved green accent reference.
- `Output/03_RaiderEnemy/raider_shotgun.png` — approved orange accent reference.
- `Output/03_RaiderEnemy/raider_sniper_charging.png` — approved blue accent reference.
- `Output/14_FieldEvents/unknown_device_analyzing.png` — approved purple palette reference.

All 18 Input files and four additional approved Output references were hash-checked unchanged (22 unique originals). Existing approved art was not edited.

## Repeatable generation

The package includes `Scripts/Generate-GameplayVFX.ps1`, `Scripts/build_gameplay_vfx.lua`, `Scripts/validate_gameplay_vfx.lua`, and the unchanged shared `Scripts/raider_boss_pixel_helpers.lua`. Lua files are UTF-8 without BOM. The helper already exists in the project and was preserved there.

From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-GameplayVFX.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`. It checks the exact reference files and hashes, copies them, runs Aseprite hidden in batch mode, reopens and validates generated files, then checks originals again. It refuses missing or changed references. `-ReplaceGenerated` permits rebuilding only a folder carrying this generator's manifest; omit it for an initial build. Output goes to `Output/15_GameplayVFX`, with reference copies and logs under `Temp/gameplay_vfx_*`.

When using the extracted package away from the project, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` may point to a separate output workspace; by default it is the parent of the Scripts folder. Rebuilding generated art replaces generated files, so preserve any manual edits before requesting a rebuild.

Validation passed for all 14 families, 18 clips and 85 frames: requested dimensions, frame counts, tags, editable layers, duration metadata, pivots, binary alpha, limited palettes, integer pixel grid, unclipped bounds and frame-to-frame changes. Reopened Aseprite composites matched exported PNG pixels in **489,472 comparisons**. Core variant masks are identical, the warning ring contracts at every step, the pickup loop returns through a small glint, and one-shot cleanup frames are transparent. The review GIF reopened as 480×270 with 50 frames at 20 ms each.

The installed PixelLab extension emitted an existing `handle-pose.lua:58` / `dlg` warning during batch startup. Aseprite generation and validation both exited successfully and completed the checks above; the extension was not modified.
