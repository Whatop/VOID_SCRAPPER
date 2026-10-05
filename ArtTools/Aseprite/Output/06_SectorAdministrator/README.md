# SECTOR ADMINISTRATOR

SYSTEM faction / Region A / green. Three aligned 128x128 boss poses authored with Aseprite CLI + Lua.

## Generated art

| State | Aseprite | Transparent PNG |
| --- | --- | --- |
| Idle | sector_administrator_idle.aseprite | sector_administrator_idle.png |
| Charged / attack-ready | sector_administrator_charged.aseprite | sector_administrator_charged.png |
| Exposed core | sector_administrator_exposed_core.aseprite | sector_administrator_exposed_core.png |

- `sector_administrator_states.aseprite`: three frames with single-frame tags `idle`, `charged`, and `exposed_core`; ten editable layers per frame.
- `sector_administrator_states.png`: transparent 384x128 horizontal sheet, in the same order; 128x128 cells, no padding or trimming.
- `sector_administrator_comparison.png`: labeled presentation at 2x and native size, plus approved Green Guard / Green Control references. This presentation image has an opaque checkerboard background.
- `manifest.json`: palette, state descriptions, layer names, module pivots and export layout.
- `validation.json`: successful reopened-file and pixel checks.
- `reference_hashes.json`: original-source SHA256 snapshot.
- `generation_complete.txt`: CLI completion marker.

## Construction and animation handoff

Front faces up (negative image Y). Keep all states on the full 128x128 canvas; center anchor is (64,64), normalized (0.5,0.5). The reactor is centered between pixels at (63.5,63.5).

Two forward laser/control emitters and two broader rear containment pylons surround the central armored reactor. Narrow mechanical bridges preserve open gaps between modules. The crown and stern management terminal establish front/rear direction.

Idle keeps the reactor shuttered and the weapons at standby. Charged opens the optic jaws, lights the routing and containment gates, and exposes more of the reactor. Exposed core retracts all four iris plates and powers down the outer modules to emphasize the vulnerability window. It remains a clean SYSTEM machine without corrosion, scavenged panels, or purple effects.

The idle reactor retains the exact approved Green Guard diamond center. The white/gray palette, green energy family and segmented armor treatment are shared with the approved units and structures. The four-module composition is newly authored; no ReferenceOnly ship pixels were copied or downscaled.

Layer order:

1. Structural Spine
2. Central Armor
3. Laser Module L
4. Laser Module R
5. Containment Pylon L
6. Containment Pylon R
7. Control Core
8. Armored Iris
9. Energy Routing
10. SYSTEM Registration

The four outer modules are separated into layers for later articulation; registration details and connecting routes remain on shared layers. No redundant standalone module images are included. Suggested module pivots in top-left image coordinates are (21,36), (106,36), (19,90), and (108,90), also recorded in the manifest.

These are three authored state poses, not a finished looping animation. The master stores each at 200 ms for convenient inspection; gameplay state timing should be controlled by the game. No Unity assets or runtime behavior were changed.

Art uses an exact 2x construction grid, opaque palette colors and transparent background pixels. Use point filtering and no texture compression if importing to Unity.

## Rebuild

Scripts are UTF-8 without BOM in `ArtTools\Aseprite\Scripts`:

- `Generate-SectorAdministrator.ps1`
- `build_sector_administrator.lua`
- `validate_sector_administrator.lua`

From PowerShell in the project's Aseprite tools root:

```powershell
& .\Scripts\Generate-SectorAdministrator.ps1 -ReplaceGenerated
```

The helper verifies the executable and exact approved input paths/hashes, copies references into a new timestamped Temp folder, and invokes Aseprite in batch mode. It only rebuilds an existing output folder if its manifest identifies this generator. If approved references change, inspect them before updating the recorded hashes. Originals are never opened for saving.

The downloadable archive contains `Output\06_SectorAdministrator` and `Scripts`. Reproduction uses the existing project references; the archive does not duplicate those original assets.

## Validation

- All three PNGs match both their reopened Aseprite files and the tagged master/combined sheet: 147,456 pixel comparisons.
- Correct 128x128 canvas, ten layers, single-frame state tags, bilateral symmetry, common alignment and canvas margins.
- Binary alpha, exact 2x pixels, only the approved 13-color SYSTEM/green palette; idle uses 12 visible colors.
- Original diamond center preserved in idle: 336 pixel comparisons.
- Module separation and meaningful differences among all three states checked.
- All 18 Input files and four approved reference PNGs remain unchanged by SHA256.
- Comparison preview visually inspected at native and enlarged sizes.

A pre-existing PixelLab extension prints `handle-pose.lua:58` during batch startup. Both final Aseprite processes completed successfully and their output checks passed; the extension was not modified.
