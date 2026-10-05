# SYSTEM region structures

Three top-down structures with transparent backgrounds, each supplied as one layered `.aseprite` and one matching `.png`.

| Structure | Dimensions | Files | Readable role |
| --- | --- | --- | --- |
| Green Control Node | 64 x 64 | system_green_control_node.aseprite / system_green_control_node.png | Compact guarded ring, continuous defensive segments, four fixed corner sockets |
| Orange Defense Node | 96 x 96 | system_orange_defense_node.aseprite / system_orange_defense_node.png | Broad fortified platform, reinforced perimeter, two twin-bore weapon emplacements |
| Blue Navigation Node | 96 x 96 | system_blue_navigation_node.aseprite / system_blue_navigation_node.png | Open routing cross, two sensor dishes, aerial masts and a lower data terminal |

`system_structures_comparison.png` compares the three structures at a common 2x display scale and at native resolution.

## Approved construction language

The approved Green Guard, Orange Assault, and Blue Precision support units supply the exact neutral and regional energy palettes.
Each structure includes its family's original diamond core window and bezel, extracted from a verified copy of the approved support PNG.
The 24 x 24 source region is masked to the diamond boundary and preserved pixel for pixel.

The structures have separately authored foundations and role modules: guarded ring, fortified platform, and sensor cross.
All artwork uses a 2x pixel grid, flat discrete shades, continuous clean armor, stable energy, and bilateral symmetry.
There are no smooth gradients, partially transparent edges, raider patches, or corrupted effects.

Editable layers:

- Foundation and Routing
- SYSTEM Armor
- Role Modules
- Approved Core Module
- Energy and Registration

## Source and reference handling

Exact approved PNG references:

- `Output/04_SystemSupport/system_support_green.png`
- `Output/04_SystemSupport/system_support_orange.png`
- `Output/04_SystemSupport/system_support_blue.png`

The three `Input/90_ReferenceOnly` assets were visually inspected for broad arrangements:

- `Station 2.png`: segmented ring and central-hub layout.
- `Station 1.png`: armored platform and paired outboard hardware.
- `Destroyer 2.png`: organization of modules along a central spine.

No ReferenceOnly pixels are sampled, downscaled, or copied into the generated art. The Lua generator never opens those files.
Working copies and logs are kept under the corresponding Temp run folder. All Input files and approved support outputs remain unchanged.

## Repeatable generation

From the Aseprite tooling root:

```powershell
.\Scripts\Generate-SystemStructures.ps1
```

To explicitly replace this generated set:

```powershell
.\Scripts\Generate-SystemStructures.ps1 -ReplaceGenerated
```

The runner inspects Input, prints exact approved-reference paths, checks hashes, creates copies, and runs Aseprite CLI + Lua.
It refuses missing or changed approved references and an unrecognized output folder. The Lua files are UTF-8 without BOM.

## Verification

`validation.json` records reopened file dimensions, layers, alpha, palette size, exact 2x pixel replication, margins, bilateral symmetry, and silhouette differences.
All 22,528 PNG pixels match the reopened Aseprite renders. The 1,008 pixels in the three masked core modules match the approved drone references exactly.
All 30 files in Input and the approved support output folder retain their original hashes; see `reference_hashes.json`.
The installed PixelLab extension emits its existing batch-startup warning; generation and validation complete successfully.
