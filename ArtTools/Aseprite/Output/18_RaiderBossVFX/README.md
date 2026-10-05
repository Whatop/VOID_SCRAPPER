# VOID SCRAPPER — Raider Boss VFX Pack

Seven editable Aseprite families, eight tagged clips, 40 authored frames. Built with Aseprite CLI + Lua using copied approved Raider references. Existing approved art and VFX remain unchanged.

Target presentation: **480×270, PPU 32**. All gameplay sheets have transparent backgrounds, hard edges, opaque colored pixels, limited palettes and a 2-pixel construction grid. There are no gradients, antialiasing, subpixel transforms or baked-in boss sprites.

## Clips

| Family filename | Tag | Frame size | Frames | Frame durations (ms) | Duration | Playback |
|---|---|---|---:|---|---:|---|
| VFX_Raider_HeavyMuzzle | Fire | 64×64 | 4 | 45,60,70,35 | 210 ms | One shot |
| VFX_Raider_BarrageTelegraph | Warning | 64×64 | 6 | 160,140,130,120,100,90 | 740 ms | Handoff |
| VFX_Raider_DamageSparks | Burst | 32×32 | 4 | 35,55,70,40 | 200 ms | One shot |
| VFX_Raider_CriticalDamage | Loop | 64×64 | 8 | 100,100,120,160,100,100,160,160 | 1000 ms | Loop |
| VFX_Raider_Assault_WeaponsHot | Heat | 32×32 | 6 | 100,70,80,110,160,120 | 640 ms | Loop |
| VFX_Raider_Salvage_Beam | Pull | 96×32 | 6 | 90,90,90,90,90,90 | 540 ms | Loop |
| VFX_Raider_Sniper_Rail | Lock | 96×32 | 3 | 220,180,140 | 540 ms | Handoff |
| VFX_Raider_Sniper_Rail | Shot | 96×32 | 3 | 35,55,40 | 130 ms | One shot |

The sniper source contains six frames total: Lock 1–3, Shot 4–6. All other families use one tag. One-shots end on a fully transparent cleanup frame, counted in the totals above.

## Raider visual cues

- **Heavy muzzle:** an asymmetric red/orange blast, white-hot center and a few uneven ejecta. Its peak footprint is 1128 opaque pixels, six times the approved normal Raider muzzle's 188 pixels.
- **Barrage warning:** broken, skewed boundary segments and a changing central cross. It contains no white or blue damage flash. Hide it at 740 ms when gameplay starts the impact; do not loop or freeze the last marker.
- **Damage sparks:** a compact red/orange/white burst with a fast directional scatter and cleanup.
- **Critical loop:** two staggered smoke puffs, intermittent sparks and pixel debris. White flashes occur on frames 1 and 5; traveling sparks follow on frames 2 and 6. Quiet frames retain smoke. Peak coverage is only 244 of 4096 pixels, keeping the effect lightweight over a damaged boss.
- **Weapons Hot:** irregular red barrel edges, staggered vent tongues and flickering orange heat. Place the transparent overlay around a mount and loop while hot. No weapon body or armor is included.
- **Salvage beam:** broken industrial red tracks, a pale center and orange collection brackets. Flow packets move exactly **four pixels left per frame**, from +X toward the emitter at X=0. Motion continues seamlessly across the six-frame loop.
- **Rail:** a red lock line and rough end marker followed by a white-hot kinetic streak with pale blue edging and red ejecta. It does not use a blue SYSTEM aperture or precision reticle. Play Lock for 540 ms before Shot for 130 ms.

## Editable organization and exports

Each family includes one `.aseprite`, transparent PNG sheet and JSON frame rectangles, durations and import hints. Four layers are preserved: **Primary Energy**, **White Hot Center**, **Sparks and Flow**, **Smoke and Debris**.

Single-tag effects use fixed-cell horizontal strips. `VFX_Raider_Sniper_Rail.png` is a 288×64 atlas with Lock on row 0 and Shot on row 1. Separate `_Lock.png` and `_Shot.png` strips are also included, each 288×32. JSON tag indices are zero-based; manifest source frame ranges are one-based.

Use Sprite (2D and UI), Multiple, **PPU 32**, Point filtering, compression None, mipmaps off and Full Rect. Slice untrimmed fixed-size cells and retain cleanup cells. JSON rectangles use top-left PNG coordinates; Unity rectangle Y is `sheetHeight - frame.y - frame.h`.

| Effect | Pixel pivot from top left | Unity normalized pivot | Direction |
|---|---|---|---|
| Heavy muzzle | 8,32 | 0.125,0.5 | +X |
| Barrage / critical damage | 32,32 | 0.5,0.5 | Centered |
| Damage sparks | 16,16 | 0.5,0.5 | Centered |
| Weapons Hot | 12,16 | 0.375,0.5 | +X |
| Salvage beam | 0,16 | 0,0.5 | Emitter left; flow toward -X |
| Rail Lock / Shot | 0,16 | 0,0.5 | +X |

The salvage pattern repeats every 24 pixels along X; its 96-pixel frame contains four complete periods. Repeat whole 24-pixel periods, or whole 96-pixel frames, without rescaling their height. Repeat the selected animation frame, not the entire multi-frame atlas. Length-only stretching is possible with Point sampling and integer screen-pixel lengths, but stretches the collection marks; repeating preserves their shape. The full beam is 3 world units at PPU 32, with 0.75-unit pattern periods.

Use authored cumulative timings with stepped sprite changes, integer screen-pixel positions and integer presentation scaling. Loop Time is appropriate only for CriticalDamage/Loop, WeaponsHot/Heat and Salvage/Pull. Hide or recycle one-shots after their complete duration. Gameplay code owns damage windows, warning handoffs and effect lifetime. No Unity runtime code, importer or AnimationClip was installed; Unity playback has not been tested.

## Review and validation

- `RaiderBossVFX_ContactSheet.png`: all eight clips and all 40 frames with durations. 32-pixel effects are shown at 2×; larger effects at 1×.
- `RaiderBossVFX_480x270.png`: native-resolution peak comparison, with Lock and Shot shown separately.
- `RaiderBossVFX_480x270.gif`: native review board, 60 sampled frames at 20 ms, repeating every 1.2 seconds. Shot follows the 540 ms lock. The review board has an opaque background; gameplay PNGs remain transparent. This review loop does not replace the exact source clip timings.
- `manifest.json`: asset inventory, tags, pivots, directions and playback notes.
- `validation.json`: passed re-opened source/PNG checks, **241,664 pixel comparisons**, requested dimensions and counts, durations, tags, layer visibility, binary alpha, palette limits, pixel grid, margins, cleanup and loop-seam checks. Salvage flow is validated through every frame transition, including last-to-first, and its 24-pixel repeat period is verified.
- `reference_hashes.json`: before/after preservation hashes for **419 original files** across Input and approved Output folders 02–17.

The Input folder was inventoried before generation. These exact approved references were inspected and copied before use (relative to `ArtTools/Aseprite`):

- `Output/09_RaiderAssaultCommander/raider_assault_commander_weapons_hot.png` — 128×128.
- `Output/10_RaiderSalvageCarrier/raider_salvage_carrier_salvage_active.png` — 128×128.
- `Output/11_RaiderSniperCommander/raider_sniper_commander_target_lock.png` — 128×128.
- `Output/15_GameplayVFX/VFX_Raider_Muzzle.png` — 96×32, three 32×32 frames.

## Repeatable workflow

Included scripts: `Generate-RaiderBossVFX.ps1`, `build_raider_boss_vfx.lua`, `validate_raider_boss_vfx.lua`, and the unchanged shared pixel helper. Lua is UTF-8 without BOM. From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-RaiderBossVFX.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`, verifies exact inspected source hashes, copies references, generates only `Output/18_RaiderBossVFX`, reopens and validates results, then checks originals again. Missing or changed references stop generation. `-ReplaceGenerated` permits rebuilding only a folder with this generator's manifest; preserve manual edits first. Copied references and logs are retained under `Temp/raider_boss_vfx_20260929_220041_259` for this successful run.

For an extracted package outside the project, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` can direct generated Output/Temp to a different workspace.

The installed PixelLab extension emitted its existing `handle-pose.lua:58` / `dlg` startup warning. Both final Aseprite passes exited successfully, completion markers were emitted and all checks passed. No extension files were changed.
