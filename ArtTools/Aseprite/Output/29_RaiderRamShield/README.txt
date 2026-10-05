RAIDER NORMAL SHIELD EXTRACTION + SHIELD RAM
Output: ArtTools/Aseprite/Output/29_RaiderRamShield

DELIVERY
Raider_ShieldField.png + Raider_ShieldField.aseprite
VFX_Raider_RamShield.aseprite + VFX_Raider_RamShield.png
Per-stage transparent strips and preview sheets
13-frame contact sheet, six-stage comparison, native 480x270 stills, rotation review, and 2.4-second sequence GIF
JSON slicing/timing metadata, validation report, source hashes, reproducible Aseprite CLI/Lua scripts

EXISTING NORMAL SHIELD - UNCHANGED PIXELS
Source:
ArtTools/Aseprite/Output/12_SalvageTradingStation/salvage_trading_station_shield_active.aseprite
The layer was selected by the exact name "Shield Field", not by index.
The export preserves its original 128x128 canvas, cel placement and all source-layer pixels.
Only that layer is in the editable extracted file. No hull, emitter, debris or other station layer is present.
664 opaque pixels: 500 of #64CDD9 and 164 of #C2F2EF. All remaining pixels are transparent.
No repainting, recoloring, resizing, resampling, added glow or smoothing was used.

SHIELD HIT - REFERENCE ONLY
The approved VFX_ShieldHit.aseprite was compared with Assets/Art/CombatReadability/ShieldHit.png.
All four 64x64 source frames match the imported sheet pixel-for-pixel.
The source, imported sheet, metadata and approved prefab remain unchanged.
Shield Hit is displayed only in the comparison preview; it is not recreated or supplied as a new replacement.

NEW ART - VFX_Raider_RamShield
Canvas: 96x48, exactly 13 frames, RGBA with binary alpha.
Forward: image up / Unity local +Y.
Attachment pivot: rear-center; top-left pixel-edge coordinates (48,48), Unity normalized (0.5,0).
The field extends forward from that attachment; it is not centered on the boss like an explosion.
Editable layers:
Frontal Segments / Concentrated Energy / Forward Hotspot / Leakage and Motion.
Six allowed Raider colors:
#662C32 #B84445 #ED7563 #E9913C #FFE0A2 #F0F2E9
No boss, weapon hardware, SYSTEM-green, Overdrive-purple or NULL pixels are baked into the Ram effect.

TAGGED STAGES - RUNTIME CONTROL
Charge: frames 1-4; 120,100,90,90 ms (400 ms total).
Starts faint, gathers toward the center, then establishes a reinforced front. Hold frame 4 or switch to Active when gameplay commits the attack.

Active: frames 5-7; 70,70,70 ms (210 ms loop).
Loop only these three frames, or hold frame 6. The opaque silhouette is identical across the three frames; compact highlight/flow changes animate energy without shifting the field. The white-hot forward center is the strongest part.

Destabilize: frames 8-10; 80,100,140 ms (320 ms total).
White/yellow central output disappears; separated red segments and small leakage remain. Hold frame 10 for the gameplay-owned punish/stagger window. Do not automatically recover just because the source animation reaches frame 10.

Recover: frames 11-13; 100,100,80 ms (280 ms total).
Thin segments realign, concentration contracts and fades, then frame 13 is fully transparent. Resume the existing Normal Shield Field independently. This stage introduces no second attack flash.

The complete authoring timeline is 1210 ms, but it is not a required uninterrupted gameplay sequence. Tags and companion JSON define independent clips. Damage, collision, movement, vulnerability and transition timing remain owned by the existing gameplay implementation.

UNITY IMPORT
Ram main PNG: 1248x48; slice into thirteen 96x48 cells left-to-right with no padding or trimming.
Normal Field PNG: one 128x128 sprite, centered pivot (0.5,0.5).
Use Point filtering, compression None, mipmaps Off and Full Rect meshes.
Authoring scale is 32 PPU. Fit uniformly to the actual boss SpriteRenderer bounds and existing parent transforms during Unity integration; do not assume the reference boss production PPU matches the VFX authoring PPU.
Keep the Ram pivot at (0.5,0), and place it on a front attachment socket. When parented to that socket, inherit the boss heading instead of rotating twice.
For a standalone effect, its unrotated forward is +Y: rotationZ = atan2(direction.y,direction.x) in degrees minus 90.
Quarter-turn pixel checks preserve all pixels and the rear attachment. Arbitrary-angle runtime rasterization should use the existing pixel-perfect/Point-rendering setup.
Never trim the transparent Recover cleanup frame.

PREVIEWS
RaiderRamShield_Comparison.png shows:
Existing Normal Field -> Existing Shield Hit -> Ram Charge -> Ram Active -> Destabilize -> Recover.
The original cyan field is drawn unchanged and at native size around the original Raider Commander. Uniform production fitting is deferred to Unity as requested.
RaiderRamShield_ContactSheet.png contains all thirteen numbered frames at exact 2x nearest-neighbor review scale.
Four stage preview sheets are also provided.
RaiderRamShield_480x270.png shows Active at native resolution.
RaiderRamShield_Punish_480x270.png shows the distinct punish-window silhouette.
RaiderRamShield_Normal_480x270.png shows the unchanged normal-field reference.
RaiderRamShield_Direction_480x270.png demonstrates four directions; review crosses indicate pivots and are not exported as effect pixels.
RaiderRamShield_480x270.gif demonstrates independent state control: Normal, Charge, two Active loops, Destabilize with a held punish frame, Recover, and Normal again. The boss moves in discrete integer pixels for review.
These are art compositions, not Unity Play Mode captures. Boss/player art and nearby projectile context marks are included only in previews.

VALIDATION
911 existing Input/Output/Scripts/reference files remained SHA-256 identical.
Exact layer-name selection, source opacity and color checks passed.
32,768 extraction pixel comparisons passed (original layer versus saved PNG and editable source).
59,904 Ram source-versus-main-sheet pixel comparisons passed; all per-stage strips also match.
16,384 existing Shield Hit source/import comparisons passed.
13 frames, four exact tag ranges, timings, layer visibility, limited palette, binary alpha, 2x2 construction grid, frame padding and transparent cleanup passed.
Active mask is stable and loop seam differences are balanced: 24 / 24 / 24 changed pixels.
Active has 924 opaque pixels (79.95% of its canvas remains transparent), with 28 white-hot pixels confined to the forward center.
The native Active preview preserves all 9,152 opaque original boss pixels.
No approved source or other effect was overwritten. No Unity scene, prefab, importer, gameplay code or Shield system was modified.

REBUILD
Use Scripts/Generate-RaiderRamShield.ps1 with:
build_raider_ram_shield.lua
raider_ram_shield_references.json
the unchanged shared system_boss_pixel_helpers.lua geometry helper.
Lua is UTF-8 without BOM. The runner inventories Input, verifies exact reference paths/hashes, copies inputs into Temp, runs Aseprite CLI, validates exports and verifies protected hashes.
Existing generated output requires explicit -ReplaceGenerated. Save any manual edits elsewhere before rebuilding.

