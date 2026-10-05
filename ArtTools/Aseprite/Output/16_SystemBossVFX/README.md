# VOID SCRAPPER — SYSTEM Boss Signature VFX

All ten requested effects are covered by **9 layered Aseprite families, 13 tagged clips, and 65 authored frames**. Portal Open and Close share one source; optional portal Hold and laser Sustain/Release clips support reusable gameplay sequences. Created with Aseprite CLI + Lua. Boss sprites are not baked into any effect or review image.

The pack matches the approved Core Gameplay VFX palettes and 2-pixel construction scale, with binary transparency, four or fewer visible colors per clip, hard edges and no antialiasing or gradients. Presentation target: 480×270, PPU 32.

## Animation inventory

Durations are per-frame milliseconds, in playback order. Transparent cleanup frames count toward each one-shot clip's frame total.

| Family filename | Size | Tag | Frames | Durations (ms) | Total | Playback |
|---|---|---|---:|---|---:|---|
| VFX_Sector_LaserTelegraph | 64×32 | Warning | 5 | 180,160,140,120,100 | 700 ms | Handoff |
| VFX_Sector_LaserBeam | 64×32 | Fire | 3 | 40,50,50 | 140 ms | Transition |
| VFX_Sector_LaserBeam | 64×32 | Sustain | 4 | 60,60,60,60 | 240 ms | Loop |
| VFX_Sector_LaserBeam | 64×32 | Release | 3 | 50,60,40 | 150 ms | One shot |
| VFX_Sector_ContainmentBarrier | 64×64 | Loop | 6 | 100,100,100,100,100,100 | 600 ms | Loop |
| VFX_Barrage_TargetTelegraph | 64×64 | Warning | 6 | 150,140,130,120,100,80 | 720 ms | Handoff |
| VFX_Barrage_HeavyImpact | 64×64 | Impact | 7 | 55,60,65,70,85,95,40 | 470 ms | One shot |
| VFX_Barrage_BatteryFlash | 64×64 | Fire | 4 | 45,60,70,35 | 210 ms | One shot |
| VFX_Phase_Portal | 64×64 | Open | 7 | 45,50,55,60,65,70,75 | 420 ms | Transition |
| VFX_Phase_Portal | 64×64 | Hold | 4 | 100,100,100,100 | 400 ms | Loop |
| VFX_Phase_Portal | 64×64 | Close | 6 | 60,60,55,50,45,40 | 310 ms | One shot |
| VFX_Phase_PrecisionLock | 64×64 | Warning | 6 | 160,140,120,100,90,70 | 680 ms | Handoff |
| VFX_Phase_RedirectFlash | 32×32 | Redirect | 4 | 35,50,60,35 | 180 ms | One shot |

## Gameplay cues

**Region A / Green:** The laser warning begins as a thin dotted line and resolves into an 18-pixel lane matching the beam's maximum continuous energy thickness. It contains no damaging white center. Hide the warning at 700 ms, then play Fire, loop Sustain as needed, and play Release once. The beam and containment wall have matching left/right pixels on every frame and can be repeated along local X. Keep their transverse thickness fixed. The barrier is a stable rail structure with a small repeating energy pulse.

**Region B / Orange:** The artillery warning uses fixed square brackets, a diamond and six filling countdown blocks, distinguishing it from the existing contracting red arrival ring. Hide it at 720 ms and start Heavy Impact. The impact's peak silhouette is 2,204 opaque pixels versus 1,284 for the approved Medium Explosion, and its peak white flash is 340 pixels versus 160. The battery flash is a broad, forward-facing discharge with a compact three-stage decay and final cleanup.

**Region C / Blue:** A narrow seam expands into a transparent technical aperture. Open ends on the same pixels as Hold's first frame; Close begins from that aperture and retracts to a seam before cleanup. The precision marker contracts through radii 26,22,18,14,10,6 pixels, then hands off to the attack at 680 ms. Redirect is a short directional blue-white displacement burst. No attack beam or damaging collision is included in the lock marker.

These files contain presentation only. Gameplay code owns warning duration, damage windows, collision bounds, portal lifetime and pooled-object release.

## Editing, sheets and metadata

Each family includes one `.aseprite`, one transparent PNG atlas and JSON frame rectangles, timings, tags and import hints. Four editable layers are preserved: **Energy Shape**, **Bright Center**, **Technical Marks**, **Fragments**.

Single-clip families use horizontal strips. Laser Beam uses a 256×96 atlas with Fire, Sustain and Release on rows 0,1,2. Portal uses a 448×192 atlas with Open, Hold and Close on rows 0,1,2. Rows are padded to the longest tag; unused end cells are transparent atlas padding, not additional animation frames. The JSON lists only authored frames. Both multi-tag sources also have one exact-width PNG strip per tag, named with `_Fire`, `_Sustain`, `_Release`, `_Open`, `_Hold` or `_Close` suffixes.

Aseprite ranges: Laser Fire 1–3, Sustain 4–7, Release 8–10; Portal Open 1–7, Hold 8–11, Close 12–17. JSON tag indices are zero-based; manifest clip ranges are one-based.

## Unity placement

Use Sprite (2D and UI), Multiple, **PPU 32**, Point filtering, compression None, mipmaps off and Full Rect. Slice fixed-size cells without trimming; retain the intentional transparent cleanup frames. Use the tag strips or JSON rectangles to avoid treating atlas padding as frames. Rectangles use PNG top-left coordinates; Unity rectangle Y is `sheetHeight - frame.y - frame.h`.

| Family | Local pixel pivot from top left | Unity normalized pivot |
|---|---|---|
| Laser warning / beam | 0,16 | 0,0.5 |
| Containment barrier | 0,32 | 0,0.5 |
| Battery flash | 8,32 | 0.125,0.5 |
| Other 64×64 families | 32,32 | 0.5,0.5 |
| Redirect flash | 16,16 | 0.5,0.5 |

Laser and battery direction is +X. The wall runs along X and can be rotated 90 degrees for vertical placement. Repeat adjacent laser or wall cells at 64-pixel / 2-world-unit intervals for seamless extension; do not repeat the entire multi-frame texture atlas. If using length-only stretching, preserve Point sampling and integer screen-pixel length, accepting that repeated technical marks will stretch. Tiling preserves the intended pixel proportions.

Use stepped sprite swaps at the authored cumulative times, integer pixel positioning and integer display scaling. A 200 FPS animation sample grid represents all authored 5 ms timing increments. Enable Loop Time only for Sustain, the barrier Loop, and portal Hold. Warnings hand off while visible; one-shots include a transparent last frame. The supplied JSON is timing and slicing metadata, not an installed Unity importer or AnimationClip asset. No Unity import/runtime validation was performed and no Unity project settings were changed.

## Review and validation

- `SystemBossVFX_ContactSheet.png`: all 13 clips and 65 frames; 64-pixel effects at 1× and the redirect effect at 2×.
- `SystemBossVFX_480x270_Preview.png`: native-pixel comparison of each role.
- `SystemBossVFX_480x270_Preview.gif`: 80 review frames at 20 ms, repeating every 1.6 seconds. Shows tiled green effects, warning-to-attack timing and portal open/hold/close. The review board has an opaque background and is not a gameplay sprite. Source timing remains exact; the GIF is sampled for review.
- `manifest.json`: complete file inventory, pivots, clips, directions and playback notes.
- `validation.json`: successful re-opened Aseprite, PNG and metadata checks; 446,464 exported pixel comparisons, binary alpha, palette/grid checks, timing/tag checks, transparent cleanup, contracting precision marker, filling artillery countdown, loop seams, matching horizontal tile edges and portal transition continuity.
- `reference_hashes.json`: unchanged hashes for all 359 inspected files across Input and approved Output folders 02–15.

The complete Input directory was inventoried first. Verified copies of the following approved outputs were used under this run's `Temp/system_boss_vfx_*/approved_copies` (paths relative to `ArtTools/Aseprite`):

- `Output/06_SectorAdministrator/sector_administrator_charged.png`
- `Output/07_DefenseOverseer/defense_overseer_barrage_charged.png`
- `Output/08_PhaseGatekeeper/phase_gatekeeper_phase_lock.png`
- `Output/15_GameplayVFX/VFX_Explosion_Medium.png`
- `Output/15_GameplayVFX/VFX_EnemyArrival_Telegraph.png`
- `Output/15_GameplayVFX/VFX_CoreActivation_Green.png`
- `Output/15_GameplayVFX/VFX_CoreActivation_Orange.png`
- `Output/15_GameplayVFX/VFX_CoreActivation_Blue.png`

## Repeatable Aseprite workflow

The package includes the task runner, build/validation Lua files, and the unchanged shared pixel helper. Lua is UTF-8 without BOM. From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-SystemBossVFX.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`, checks required paths and inspected hashes, copies references, generates only `Output/16_SystemBossVFX`, validates exports and verifies originals again. It refuses missing or changed references. `-ReplaceGenerated` only permits rebuilding a folder with this pack's generator manifest. Preserve manual edits before rebuilding. Copies and logs are kept in a unique `Temp/system_boss_vfx_*` folder.

For an extracted package outside the project, supply `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` folder. `-ToolRoot` can direct generated Output/Temp into a separate workspace; its default is the parent of the Scripts folder. Existing approved VFX and the shared helper remain unchanged.

The installed PixelLab extension emitted its existing `handle-pose.lua:58` / `dlg` startup warning in the batch logs. Both Aseprite passes exited successfully and emitted generation/validation completion markers; all asset checks passed. The extension was not modified.
