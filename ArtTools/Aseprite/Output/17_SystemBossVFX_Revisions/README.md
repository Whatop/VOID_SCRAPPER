# SYSTEM Boss VFX — Three Targeted Revisions

Only Heavy Barrage Impact, Phase Portal and Phase Redirect Flash were revised. The originals and all other approved VFX remain unchanged in Output folders 02–16. Revised files are saved separately in `Output/17_SystemBossVFX_Revisions` using the same family filenames.

The three copied `.aseprite` sources were opened and revised with Aseprite CLI + Lua. Their four named editable layers remain: Energy Shape, Bright Center, Technical Marks, Fragments. All gameplay PNGs use transparent backgrounds, opaque colored pixels, the approved four-color palettes and a 2-pixel construction grid. PPU remains 32, with centered pivots. No boss sprite is included in the VFX.

## Revised clips

| Source | Tag | Frame size | Frames | Frame durations (ms) | Duration |
|---|---|---|---:|---|---:|
| VFX_Barrage_HeavyImpact | Impact | 64×64 | 6 | 65,85,90,85,80,75 | 480 ms |
| VFX_Phase_Portal | Open | 64×64 | 8 | 55,55,60,60,70,70,80,110 | 560 ms |
| VFX_Phase_Portal | Close | 64×64 | 8 | 80,65,60,55,50,45,40,40 | 435 ms |
| VFX_Phase_RedirectFlash | Redirect | 32×32 | 4 | 45,60,55,40 | 200 ms |

**Heavy Barrage Impact:** Larger white/orange center, a bright radial shock ring and four directional debris groups. Every exported frame contains visible, fully opaque pixels; no blank cleanup frame is included in this six-frame sequence. Hide/recycle the effect after 480 ms instead of holding the final debris frame. Opaque pixel counts across its six frames: **1492, 2852, 2196, 1092, 448, 172**. Peak white area increased from 340 to 916 pixels; the generic Medium Explosion peaks at 160 white pixels and 1284 opaque pixels.

**Phase Portal:** Thick vertical elliptical aperture with blue-white interior distortion bands, separate alignment brackets and a readable outer ring. Open and Close each contain eight frames. Open ends at exactly the same aperture pose that starts Close. Hold the last Open pose for a persistent portal; the prior Hold tag is intentionally replaced by this two-tag layout. Close ends with a transparent cleanup frame. Aseprite ranges: Open 1–8, Close 9–16.

**Phase Redirect Flash:** Original +X direction retained. A larger white center is followed by a displaced blue-white arrow afterimage. Frame three leaves a short directional streak; frame four is transparent cleanup.

## Exports and import notes

- Each source has its `.aseprite`, transparent `.png` atlas and `.json` frame/timing metadata.
- Heavy Impact: 384×64 strip. Redirect: 128×32 strip.
- Portal: 512×128 atlas with Open on the first row and Close on the second. Separate `_Open.png` and `_Close.png` strips are also provided, each 512×64.
- Use Sprite Multiple, PPU 32, Point filtering, compression None, mipmaps off and Full Rect. Slice fixed cells without trimming. All pivots are normalized (0.5,0.5).
- JSON rectangles use top-left PNG coordinates; convert to Unity rectangle Y with `sheetHeight - frame.y - frame.h`. Use the authored durations for stepped sprite changes. None of these clips loops automatically.
- Existing Unity assets and runtime code were not changed. Update the target clip references/timings and portal tag mapping during integration. Unity runtime playback was not tested.

## Review files

- `SystemBossVFX_Revisions_ContactSheet.png`: every revised frame with timing. 64×64 effects shown at native 1×; the 32×32 redirect shown at 2×.
- `SystemBossVFX_Revisions_480x270.png`: native-resolution before/after peak comparison. Top row is the original; bottom row is revised.
- `SystemBossVFX_Revisions_480x270.gif`: native before/after animation, 60 frames at 20 ms, 1.2-second review loop. The impact starts immediately so the first preview frame is visibly populated. Review backgrounds are opaque; gameplay sheets remain transparent. Exact source timings are preserved separately from GIF sampling.

## Validation and preservation

All 26 authored frames across four clips were reopened and checked against the PNG sheets in **188,416 pixel comparisons**. Checks covered dimensions, frame/tag counts, durations, binary alpha, four-color palettes, 2-pixel grid, full layer/cel opacity, bounds, cleanup frames and portal transition continuity. Every barrage frame has at least 128 bright opaque pixels; frames 1–3 retain at least 200 white pixels. Exported portal volume, central distortion and redirect afterimage were also checked. Results are in `validation.json`.

The Input folder was inventoried first. Source copies came from these verified files under `ArtTools/Aseprite/Output/16_SystemBossVFX`:

- `VFX_Barrage_HeavyImpact.aseprite` and `.png`
- `VFX_Phase_Portal.aseprite` and `VFX_Phase_Portal_Open.png`
- `VFX_Phase_RedirectFlash.aseprite` and `.png`

`Output/15_GameplayVFX/VFX_Explosion_Medium.png` was copied for comparison. Before/after hashes confirm **400 original files** across Input and approved Output folders 02–16 are unchanged. Copies and logs are retained in `Temp/system_vfx_revision_20260929_213246_410`.

## Repeatable workflow

Three new task scripts are included: `Revise-SystemBossVFX.ps1`, `revise_system_boss_vfx.lua`, and `validate_system_vfx_revisions.lua`, plus the unchanged shared pixel helper. Lua is UTF-8 without BOM. Frame/cel/tag edits use the documented [Aseprite Sprite API](https://www.aseprite.org/api/sprite).

From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Revise-SystemBossVFX.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`, verifies inspected source hashes and works on copies only. `-ReplaceGenerated` permits rebuilding this revision folder only when its generator manifest matches. Preserve any manual edits before rebuilding. For an extracted package outside the project, supply `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` can direct Output/Temp into another workspace.

The existing PixelLab `handle-pose.lua:58` / `dlg` batch-startup warnings remain in the logs. Both Aseprite passes completed successfully and all validation checks passed. No extension files were changed.
