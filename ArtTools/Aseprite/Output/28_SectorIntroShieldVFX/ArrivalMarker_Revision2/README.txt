SECTOR ADMINISTRATOR - ARRIVAL MARKER REVISION 2

Only the arrival marker was revised. The original marker, relay units, all other VFX, approved boss sprites, and gameplay files remain unchanged. This version is delivered in a separate ArrivalMarker_Revision2 folder to preserve the approved pack.

CHANGES
Visible diameter: 84 pixels to 112 pixels (+33.3%). The canvas is now 128x128 because the requested diameter increase cannot fit the previous 96x96 cell. The 112px deployment footprint fits the approved 128x128 boss reference.

Four broad inward-facing brackets and four smaller diagonal corner modules point toward an open deployment bay. The four-color palette uses bright cyan-green, white-green activation points, a darker green support color and a faint green tail. Geometry does not rotate. There is no red, filled center, soft glow, anti-aliasing or NULL distortion.

The middle 48x48 area is completely transparent except for frame 4's brief 8x8 cross (48 occupied pixels). Even the densest frame remains 91.4% transparent overall. White-green accents are limited to 48 pixels at the peak. Active-frame mean luminance is 23% higher than the original marker, with stronger directional strokes.

ANIMATION / ARRIVAL TAG
1. Faint outer registration geometry - 100 ms.
2. Four primary brackets activate - 100 ms.
3. Secondary corner segments illuminate - 100 ms.
4. Small center cross appears - 80 ms.
5. Full marker reaches peak brightness - 60 ms.
6. Sparse dim geometry remains during handoff - 40 ms.

Total: 480 ms, preserving the original frame count and timing. This is a one-shot. Frame 6 intentionally contains a dim tail; clear the renderer at the end rather than holding it or looping it indefinitely.

FILES
VFX_Sector_BossArrivalMarker.aseprite: editable four-layer source, using the existing Field Geometry / Hot Nodes / Routing Marks / Residual Energy organization.
VFX_Sector_BossArrivalMarker.png: transparent 768x128 horizontal strip, six 128x128 cells.
VFX_Sector_BossArrivalMarker.json: exact cell rectangles, centered pivot, frame timing, import hints and cleanup behavior.
VFX_Sector_BossArrivalMarker_Arrival_Preview.png: six-frame review sheet.
SectorArrivalMarker_480x270.png: native-size active marker preview.
SectorArrivalMarker_Peak_480x270.png: native-size peak preview.
SectorArrivalMarker_BeforeAfter_480x270.png/.gif: equal-scale comparison with the original.
SectorArrivalMarker_480x270.gif: short arrival/handoff review using unchanged materialization and boss references.

IMPORT
Sprite Multiple, 128x128 grid, centered pivot, 32 PPU authoring scale, Point filtering, compression None, mipmaps Off, Full Rect. Do not squeeze this canvas back into a 96px display rectangle or the diameter increase will be lost. Keep the same pixels-per-world-unit relationship as the previous marker and fit placement to actual boss bounds. No Unity assets or encounter timing were edited.

PREVIEW SCOPE
The review GIF shows marker onset, a 440 ms materialization handoff, and boss reveal after the central flash clears. It is an art composition, not a Unity capture or gameplay timing change. Approved boss and materialization pixels appear only in review previews, never in the transparent marker source or strip.

VALIDATION / REBUILD
validation.json records 98,304 saved ASE/PNG pixel comparisons, binary alpha, four-color palette, fixed fourfold geometry, unchanged frame timing, empty center, brightness increase, fade, and 480x270 preview checks. protected_hashes.json records all 890 existing Input/Output/Scripts files checked unchanged during generation. The revision uses copied, hash-verified inputs and UTF-8 Lua without BOM.

Rebuild with Scripts/Revise-SectorArrivalMarker.ps1. Supporting files are revise_sector_arrival_marker.lua, sector_arrival_marker_revision_references.json and the unchanged system_boss_pixel_helpers.lua. The runner inspects exact references and refuses missing or changed inputs. An existing revision requires explicit -ReplaceGenerated; preserve manual changes before rebuilding.
