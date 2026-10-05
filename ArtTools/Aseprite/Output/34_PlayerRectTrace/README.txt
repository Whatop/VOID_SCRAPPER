VOID SCRAPPER - PLAYER RECTANGULAR ACCENT / ERROR TRACE VFX

Four compact effects for the existing 16x16 square player. The player is unchanged
and is NOT baked into any source animation or VFX PNG export.

FIVE PALETTE VERSIONS
  VFX_Player_RectTrace_Neutral - Tutorial / neutral cyan
  VFX_Player_RectTrace_RegionA - green
  VFX_Player_RectTrace_RegionB - orange
  VFX_Player_RectTrace_RegionC - blue
  VFX_Player_RectTrace_Final  - purple
Each has a layered .aseprite, a 448x32 master strip, four separate tag strips,
and JSON frame/timing/import metadata. All five use exactly the same geometry.

CLIPS / TIMING
Every frame is 32x32. One-based source frame ranges:
  Idle       1-2   400/400ms           800ms subtle loop
  Active     3-6   140ms each         560ms loop
  DashBurst  7-10  40/50/70/60ms       220ms one-shot
  HitError  11-14  40/50/70/60ms       220ms one-shot
JSON frameTags use zero-based endpoints; the clips section uses one-based ranges.
DashBurst and HitError end on fully transparent cleanup frames.

IDLE: two dim corner traces and two small points; only one node changes gently.
ACTIVE: four short corner traces, two tiny side blocks and controlled node motion.
DASH: a short forward accent, rectangular expansion, then trailing fragments.
HIT/ERROR: displaced, incomplete traces with a brief small white flash and recovery.
These are signal accents, not shield spheres, smoke, auras or player replacements.

LAYERS / COLOR SLOTS
  Rectangular Traces
  Energy Nodes
  Displaced Fragments
The four palette slots consistently mean dim trace, active energy, bright node and
brief white flash. Fully exported regional versions avoid depending on a uniform
Unity tint, which would also tint the white flash. Future variants can remap these
four source colors. No smooth gradients, anti-aliasing or fractional alpha is used.

ALIGNMENT / READABILITY
Use a centered pivot and align the effect center with the existing player center.
The native player occupies x8..23, y8..23 inside the 32px effect canvas.
The larger x6..25, y6..25 area is always transparent: a 20x20 exclusion window.
All marks remain within x2..29, y2..29 and use discrete 2px construction.
Maximum visible pixels: Idle 32, Active 64, Dash 96, Hit/Error 64.
Idle and Active contain no white-hot flash. Bursts use at most 8 white flash pixels.
The player remains the central visual subject, with all of its pixels unobscured.

UNITY USE
Use Point filtering, no mipmaps, uncompressed pixels, Full Rect mesh and 32 PPU.
Keep every frame at the full 32x32 size without trimming; all pivots are identical.
Play Idle or Active as alternative baseline states, rather than stacking both.
Play DashBurst/HitError once, including the transparent final frame, then resume the
chosen baseline when appropriate. Do not run the entire master strip as one loop.
Dash source direction is upward in image space. Unity may orient the accent to the
dash direction; quarter-turn rotations preserve the authored pixel grid exactly.
This pack adds no input, movement, hit, collision or shield behavior. No Unity code,
scenes, sprites, prefabs or import settings were modified during authoring.

PREVIEWS
PlayerRectTrace_ContactSheet.png shows all neutral animation frames with the player
at exact 3x scale, followed by five matched Active palette samples.
PlayerRectTrace_480x270.png / .gif show the actual 16px player and 32px accent with
approved enemies/pickups for native-size context. The GIF is 26 frames / 5.56 seconds.
PlayerRectTrace_<palette>_480x270.png provides a native Active view in each region.
Player/enemy/pickup sprites and tiny projectile placeholders appear only in review
compositions. Previews are not Unity runtime captures or gameplay validation.

VALIDATION
71,680 source-to-sheet pixel comparisons passed across 70 frames, 20 tags and
15 editable layers. Checks cover individual strips, durations, binary alpha, four-
color palettes, 2px construction, clear player center, canvas margins, clean loop
seams, one-shot cleanup, matched regional masks and distinct dash/error silhouettes.
The preview player is pixel-identical in every effect frame.
All 1,095 protected input/output/script/reference files remain unchanged.
See validation.json, manifest.json and protected_hashes.json.

REBUILD
From the project's ArtTools/Aseprite directory:
  & '.\Scripts\Generate-PlayerRectTrace.ps1' -ReplaceGenerated
The runner verifies exact reference paths and hashes, creates fresh Temp copies,
checks Lua as UTF-8 without BOM, runs Aseprite CLI and validates output/preservation.
It targets only this generator's Output/34_PlayerRectTrace folder. The unchanged
shared pixel helper is included for repeatability. Copies and logs remain in
Temp/player_rect_trace_*. The installed PixelLab extension emitted its existing
handle-pose.lua startup warning; generation and pixel validation completed normally.
