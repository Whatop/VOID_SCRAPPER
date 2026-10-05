SECTOR ADMINISTRATOR - BOSS INTRO / SHIELD VFX

Generated in Aseprite CLI with editable Lua-authored layers. No gameplay or Unity asset was changed. All 751 protected input/reference files remain SHA-256 identical.

DELIVERABLES
6 editable Aseprite families, 44 frames, 9 tags. Transparent PNG sheets, JSON slicing/timing metadata, individual tag previews, combined contact sheet, native 480x270 stills and three animated sequence previews.

Family                               Canvas    Tags          Frames/tag  Duration
VFX_Sector_BossArrivalMarker          96x96     Arrival       6           480 ms
VFX_Sector_BossMaterialization        96x96     Materialize   5           270 ms
VFX_Sector_BossShield                 128x128   Green/Purple  4 each      480 ms loop
VFX_Sector_BossShieldHit              32x32     Green/Purple  4 each      150 ms
VFX_Sector_BossShieldRelease          128x128   Green/Purple  6 each      310 ms
VFX_Sector_PhaseTransitionRing        112x112   Overdrive     5           300 ms

Every one-shot ends with a transparent cleanup frame. Only the normal shield tags loop. Green and Purple shield, hit and release variants share pixel-identical geometry. Each energy family uses at most four colors; the transition uses both palettes. All art uses binary alpha and integer 2x2 pixel blocks. No soft bloom or anti-aliasing.

EDITABLE LAYERS
Field Geometry / Hot Nodes / Routing Marks / Residual Energy.
Boss sprites are used only in review previews; no boss pixels are baked into the VFX sheets or editable VFX sources.

UNITY IMPORT AND PLACEMENT
Use Sprite (2D and UI), Multiple, Point filtering, compression None, mipmaps Off, Full Rect mesh, centered pivot (0.5, 0.5). Default authoring PPU is 32. Do not trim cells; preserve empty cleanup frames and fixed frame rectangles. The companion JSON provides exact cell rectangles, millisecond durations, tags and looping intent. In two-tag sheets, Green is the upper row and Purple the lower row. Separate per-color PNG strips are also supplied.

Fit the shield to the actual boss SpriteRenderer bounds. The inspected production boss uses its own PPU (87.671234) and parent scaling; do not assume that a shield imported at 32 PPU and placed under that transform automatically matches it. These previews compare the approved 128x128 boss and shield at the same native pixel size. No production import settings or transforms were changed.

The normal shield occupies only 704 of 16384 pixels: 95.7% of its canvas is transparent. Its center is clear, and perimeter cells avoid every opaque pixel in the approved boss silhouette. White highlights remain restrained (16 pixels per frame). Keep the boss visible during shield release.

SHIELD HIT
Place the compact hit at the contact point on the perimeter. Its default tangent is horizontal and its outward normal points toward image top. Rotate to match other shield edges. The peak is 44 opaque pixels, with eight white pixels. Reuse/pool this local effect for rapid fire; avoid restarting or flashing the entire shield on every Machine Gun hit.

REVIEW SEQUENCES
SectorIntroShield_Arrival_480x270.gif (1.6 seconds): marker begins at 0 ms; materialization begins at 380 ms; approved boss appears at 470 ms after the central flash clears; Green shield begins at 600 ms. Local hits demonstrate small independent responses.

SectorIntroShield_Overdrive_480x270.gif (1.4 seconds): Green presentation first; the copied approved five-frame boss transition plays from 400 to 700 ms. The optional five-frame ring lasts the same 300 ms. The shield changes from Green to Purple using identical geometry. The established Purple boss state follows.

SectorIntroShield_480x270.gif (4 seconds) combines arrival, overdrive and release. GIF review cadence is 40 ms; use the exact source/JSON frame durations in gameplay. Review timelines are visual examples, not changes to encounter timing, damage, invulnerability or any gameplay state.

The 480x270 previews are native-size art compositions, not Unity Play Mode captures. The player is included only as scale context. Preview backgrounds and captions are not part of exported transparent VFX sheets.

VALIDATION
validation.json records successful dimensions, tags, timings, palette, binary-alpha, grid, loop-seam, cleanup, silhouette overlap and export checks. 1,008,672 rendered pixel comparisons passed. reference_hashes.json records all protected source hashes. Approved Green and Purple boss sources, production sprite and approved transition were opened only from verified temporary copies.

REPEATABLE GENERATION
Scripts/Generate-SectorIntroShield.ps1 inspects Input, confirms exact source paths/hashes, copies references to a new Temp run, generates through Aseprite CLI/Lua, validates exports, then verifies the original hashes. The other task files are build_sector_intro_shield.lua, validate_sector_intro_shield.lua and sector_intro_shield_references.json. system_boss_pixel_helpers.lua is the unchanged shared helper. Lua files use UTF-8 without BOM.

Place the package Output and Scripts folders beneath ArtTools/Aseprite. Run the PowerShell helper from Scripts. It refuses an existing output unless -ReplaceGenerated is explicitly provided and the folder has this generator's manifest. Do not use replacement mode after manually editing generated sources without first saving those edits separately.
