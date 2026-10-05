VOID SCRAPPER - PRESENTATION SUPPORT PACK

CONTENTS
8 layered .aseprite sources, PNG exports, frame metadata, review sheets,
native 480x270 PNG/GIF previews, and repeatable Aseprite CLI + Lua scripts.
All artwork is newly generated. Approved references are used only from copies.

A. REGION BACKGROUNDS
BG_Tutorial / BG_RegionA / BG_RegionB / BG_RegionC / BG_Final
Each is a 480x270 representative fixed-view plate, not a seamless scrolling tile.
Tutorial: restrained teaching space. A: green salvage traces. B: orange industrial
remnants. C: blue displaced navigation routes. Final: dark purple broken network.

The plain .png is a complete opaque background. The .aseprite contains:
  Layer1: Void Base (opaque)
  Layer2: Distant Points (transparent)
  Layer3: Peripheral Remnants (transparent)
  Layer4: Faint Regional Traces (transparent)
Separate Layer1..Layer4 PNGs are provided in the same stacking order.
The _Overlay.png combines layers 2..4 over transparency; place it over a dark base.
The central 288x162 area (x96..383, y54..215) contains no structural detail or
regional traces. Sparse low-contrast stars remain. Background colors are subdued
so bright sprites/projectiles remain distinct. No blur, gradients or alpha fades.

B. EQUIPMENT FRAMES
UI_EquipmentFrame_64 and UI_EquipmentFrame_96
Four single-frame tags: Idle, Selected, Equipped, Locked.
Each state has a separate transparent PNG plus a four-cell horizontal strip.
Layers: Frame Rails, Corner Hardware, State Indicators.
64px frame: clear 40x40 center, intended for a centered 32px display icon.
96px frame: clear 72x72 center, intended for a centered native 64px icon.
Keep frames at these sizes; arbitrary stretching also stretches status glyphs.
No icons are baked into the exports. Icon samples appear only in review images.
Selected uses cyan/white corner nodes and a top indicator. Equipped uses a checked
latch and three small status lights. Locked uses dim rails and a top lock badge.
The caller controls icon tint/opacity and selection behavior independently.

C. PLAYER RECTANGULAR BOUNDARY / ERROR ACCENT
VFX_Player_RectBoundary, 32x32, 18 frames, three tags:
  Idle: frames 1-6, 120ms each, 720ms looping clip.
  Activate: frames 7-12, 40/50/60/60/80/50ms, 340ms one-shot.
  Intensified: frames 13-18, 80ms each, 480ms looping clip.
Frame numbers above are one-based; JSON frameTag endpoints are zero-based.
Activate ends fully transparent. Loop geometry remains stationary, with small
node changes rather than continuous movement. Maximum visible footprint: 80px.
Layers: Corner Fragments, Activation Nodes, Error Bits.
The central 20x20 window is always transparent. Center this 32px effect on the
existing 16px square player. No player pixels are baked into any VFX export.
Use Idle/Activate/Intensified as alternative states, not stacked simultaneously.

IMPORT / PLAYBACK
Main animation PNGs are horizontal strips. Per-tag strips are also provided.
Each companion JSON includes frame rectangles, durations, tags and import notes.
Use Point filtering, no mipmaps, uncompressed pixels, Full Rect mesh and a centered
pivot. For world sprites use 32 PPU, consistent with the existing project.
Use fixed pixel dimensions for UI frames and pixel-perfect canvas scaling.
There are no new materials, shaders, prefabs or gameplay scripts in this pack.

REVIEW FILES
PresentationSupport_ContactSheet.png - complete comparison at native plate size.
PresentationSupport_480x270.png / .gif - native gameplay-scale asset composition.
Presentation_<region>_480x270.png - one native composition for each background.
EquipmentFrames_ContactSheet.png / EquipmentFrames_480x270.png - all slot states.
PlayerBoundary_ContactSheet.png - all 18 animation frames, enlarged by integer scale.
Reference player, enemy and item sprites in previews are not new deliverable art.
These are Aseprite review compositions, not Unity Play Mode captures. Integration
and actual gameplay behavior are outside this asset-authoring pass.

VALIDATION
validation.json records 719,680 source/export pixel comparisons across 31 frames,
binary alpha, limited palettes, source layers/tags/timing, 2px construction grid,
background layer reassembly, quiet-center checks, icon safety, player exclusion,
loop seams and transparent one-shot cleanup. All checks passed.
943 protected input, approved output, script and reference files remained unchanged.
protected_hashes.json preserves the verified baseline.

REBUILD
From the project's ArtTools/Aseprite directory, run:
  & '.\Scripts\Generate-PresentationSupport.ps1' -ReplaceGenerated
This script checks exact reference paths/hashes, copies them into a fresh Temp run,
checks UTF-8 without BOM, runs Aseprite in batch, validates exports, and verifies
protected sources. It replaces only this generator's Output/30_PresentationSupport.
The archive includes the pixel helper for portability; the project's existing
helper was reused without modification. Generation logs are retained in Temp.
The installed PixelLab extension emitted its existing handle-pose.lua startup
warning; the Aseprite source/export validation completed successfully.
