VOID SCRAPPER - REGION BACKGROUND PACK

Five full-screen top-down combat backgrounds, each exactly 480x270:
  BG_Tutorial.aseprite / .png - sparse outer salvage / guidance remnants
  BG_RegionA.aseprite / .png  - green salvage field and broken hull ribs
  BG_RegionB.aseprite / .png  - orange compression jaws and cargo hardware
  BG_RegionC.aseprite / .png  - blue phase-lens fragments and route traces
  BG_Final.aseprite / .png    - purple fractured authority / NULL network

Each source contains six editable layers:
  Void Base
  Distant Starfield
  Peripheral Remnants
  Mechanical Structure
  Regional Traces
  Fractures and Residue

The main PNG is a complete opaque gameplay background, with no labels or gameplay
sprites baked in. The optional _Overlay.png contains all non-base layers over
transparency; place it over the region's base color from manifest.json.
These are fixed-view plates, not seamless scrolling tiles or splash illustrations.

READABILITY
The central 288x162 rectangle (x96..383, y54..215) contains no structural geometry,
circuits or debris. It is larger than the center third of the screen. Only sparse,
very dim star pixels appear there. Heavy shapes and accents stay at the edges.
The finished backgrounds use 4-6 opaque colors each, on an integer 2px grid.
All source layers use binary alpha. No anti-aliasing, gradients or painted glow.
Tutorial is the sparsest composition; Region B has the most manufactured detail.
Final uses small muted lavender highlights rather than a bright purple field.

REVIEW
RegionBackground_ContactSheet.png places all five 480x270 exports at native size,
with labels outside each artwork panel. No image rescaling is used.
RegionBackground_Readability.png and Readability_<region>_480x270.png are optional
asset compositions using copied approved player, enemy and credit references.
Small projectile marks are only review placeholders. These review files are not
Unity runtime captures and should not be imported as gameplay backgrounds.

IMPORT
Use Point filtering, no mipmaps, and lossless/uncompressed pixels. Preserve the
480x270 composition with integer scaling. If used as a world sprite, the existing
32 PPU convention gives a 15 x 8.4375 world-unit plate. Place behind gameplay.
No Unity scenes, materials, prefabs, camera settings or gameplay code were changed.

VALIDATION
validation.json records 648,000 exact Aseprite-to-PNG pixel comparisons, dimensions,
palette/alpha/grid checks, six-layer reassembly, native contact-sheet fidelity,
center-clearance and contrast/density checks. All checks passed.
All 1,013 protected Input, approved Output and Script files remain unchanged.
protected_hashes.json records the verified baseline.

WORKFLOW / REBUILD
The five existing Presentation Support backgrounds were verified, copied to Temp,
and developed into this separate pack. Their originals were not overwritten.
The new runner, Lua and reference manifest belong under ArtTools/Aseprite/Scripts.
From ArtTools/Aseprite, run:
  & '.\Scripts\Generate-RegionBackground.ps1' -ReplaceGenerated
The runner checks exact reference paths/hashes before executing, rejects missing
or changed sources, validates UTF-8 without BOM, works on fresh reference copies,
and writes only this generator's Output/31_RegionBackground output.
The archive includes the unchanged shared pixel helper for repeatability.
Build logs and copied references are retained under Temp/region_background_*.
The installed PixelLab extension emitted its existing handle-pose.lua startup
warning; source generation and export validation completed successfully.
