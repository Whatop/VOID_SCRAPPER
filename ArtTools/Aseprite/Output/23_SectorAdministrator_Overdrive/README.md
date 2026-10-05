# Sector Administrator — Phase 2 / Purple Overdrive

New art derived directly from the current production 128x128 Sector Administrator. Aseprite CLI 1.3.18.6 + UTF-8-without-BOM Lua; approved artwork is never edited.

## Files

| File | Purpose |
| --- | --- |
| `sector_administrator_overdrive.aseprite` | Editable single-frame gameplay state; 10 original layers and `Purple Overdrive` tag. |
| `sector_administrator_overdrive.png` | Transparent 128x128 gameplay sprite. |
| `sector_administrator_overdrive_transition.aseprite` | Editable five-frame transition with the original 10 layers. |
| `sector_administrator_overdrive_transition.png` | Transparent 640x128 strip, five untrimmed 128x128 cells, left to right. |
| `sector_administrator_overdrive_transition.json` | Cell rectangles, frame durations, tag, pivot, observed production PPU, and cannon crop information. |
| `sector_administrator_overdrive_center_cannon.png` | Optional transparent 30x32 crop matching the existing separate cannon renderer. |
| `sector_administrator_overdrive_center_cannon_transition.png` | Optional 150x32 cannon strip; five 30x32 cells synchronized to the body. |
| `sector_administrator_overdrive_comparison.png` | Phase 1 versus Phase 2 at integer 3x, plus native transition frames. |
| `sector_administrator_overdrive_480x270.png` | Native, unscaled 128x128 art on a 480x270 review canvas. |
| `sector_administrator_overdrive_review.gif` | Transition review with longer holds at both endpoints. |
| `manifest.json`, `validation.json`, `reference_hashes.json` | Design/export contract, actual validation results, and protected source hashes. |

## Source and preservation

Inspected production PNG:
`C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\Assets\Art\ApprovedIntegration\World\SectorAdministrator.png`

Matching editable source:
`C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\06_SectorAdministrator\sector_administrator_idle.aseprite`

The editable source renders exactly like production. The production PNG also matches `Output\06_SectorAdministrator\sector_administrator_idle.png` byte-for-byte. Production PNG SHA-256: `B3FD582D3440A6F73E3847335785205C195057C7CCD1F417940B1A2ED235E594`.

Only the 704 original visible green energy pixels change. Every armor/mechanical pixel, transparent pixel, layer opacity, cel position, and per-layer silhouette remains identical. White armor, left/right guns, center cannon, rear launcher region, symmetry, and 2px construction grid are preserved. The core has a compact 48-pixel white-hot center; violet routing is intensified within its existing footprint. There is no added external bloom, chassis movement, corruption geometry, or baked laser.

Original layer structure is retained: Structural Spine, Central Armor, Laser Module L/R, Containment Pylon L/R, Control Core, Armored Iris, Energy Routing, SYSTEM Registration. Existing module layers already separate attachments; no duplicate energy layers were added. All new sources have explicit editing palettes. Each rendered frame uses 12 opaque colors, with binary alpha only.

## Transition

One-shot, 300 ms: green (50 ms), green-white surge (50 ms), pale violet (50 ms), purple peak (60 ms), settled overdrive (90 ms). Hold the final state afterward. Frame 1 is pixel-identical to approved Phase 1; frame 5 is pixel-identical to the separate Overdrive PNG and ASE. The review GIF adds endpoint holds and is not the gameplay timing source.

## Unity integration contract

No Unity assets, prefabs, code, or imports were changed by this task. Integration is not applied or runtime-tested.

The inspected production import uses **87.671234 PPU**, a centered pivot `(0.5, 0.5)`, and a 128x128 canvas. Preserve that existing scale when swapping these art assets; do not substitute the general VFX-pack PPU of 32. Use Point filtering, no compression, no mipmaps, and untrimmed rectangular slices.

The existing separate `CenterChargeCannon` uses the production texture rectangle `(x=49, y=80, width=30, height=32)` in Unity's bottom-left coordinates, equivalent to top-left PNG `(49,16,30,32)`. The optional crop exports match those exact pixels, centered pivot, observed PPU, and existing local scale 1.25. They permit a later color-consistent cannon swap without baking its enlarged renderer into the body sprite or moving an attachment.

The 480x270 review is a native-pixel art comparison, not a Play Mode capture. Runtime energy overlays and laser colors remain the responsibility of existing presentation code. A concurrent save changed `Boss.prefab` during inspection; its sprite bindings and cannon layout were rechecked and still matched. The prefab was read only and excluded from the immutable artwork hash inventory.

## Rebuild

From the project's `ArtTools\Aseprite\Scripts` directory:

```powershell
.\Generate-SectorOverdrive.ps1 -ReplaceGenerated
```

The runner inventories Input, requires all inspected source paths and hashes, copies references into a new Temp run directory, executes both Lua scripts, reopens exported ASE/PNG files, and verifies source preservation. It only replaces the output folder bearing this generator's own manifest. Missing or changed source files stop generation. Scripts use UTF-8 without BOM; the approved shared pixel helper is unchanged.

Validation checks: production/source equivalence; all five transition frames; exact alpha masks and non-energy pixels; original layers and attachment masks; bilateral symmetry; 2px grid; binary alpha and palette limits; ASE/PNG/sheet equality; timing and tags; exact cannon crops; and native preview pixel placement. Approved Input, prior Output sets, and selected production art references are SHA-256 checked before and after generation. Existing PixelLab `handle-pose.lua` startup warnings are present in logs but generation and independent validation both complete successfully.
