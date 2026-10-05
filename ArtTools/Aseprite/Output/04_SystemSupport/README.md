# System faction support drones

Three static, north-facing 64 x 64 units with transparent backgrounds.
Each unit has one layered `.aseprite` and one matching `.png`.

| Unit | Files | Role and silhouette |
| --- | --- | --- |
| Green | system_support_green.aseprite / system_support_green.png | Balanced guard support; paired rounded guard pods |
| Orange | system_support_orange.aseprite / system_support_orange.png | Assault support; broader armored modules and short forward emitters |
| Blue | system_support_blue.aseprite / system_support_blue.png | Precision/navigation support; slender rangefinder rails and lateral sensors |

`system_support_preview.png` shows all three at 3x scale and native 64 x 64 size.

## Shared visual identity

All three use a mirrored mechanical chassis, clean gray/white armor, a continuous diamond core bezel, and fixed energy indicators.
The green, orange, and blue energy ramps exactly reuse the corresponding region-core family colors.
There are no raider red patches, scavenged armor breaks, purple circuitry, corrupted offsets, or flicker.
Each drone has four editable layers: Mechanical Chassis, Clean Armor, Stable Core and Role Energy, and System Markings.
The entire design uses a 32 x 32 logical grid with exact 2x pixel replication to 64 x 64.
The compact body and restrained paired modules keep the drones readable as support units.

## Inspected sources

All generation reads use verified working copies. The source files remain unchanged.

- `Input/03_SpecialEnemy/enemy_elite4.png` (64 x 64): shared mechanical chassis and bilateral ship proportions.
- `Input/03_SpecialEnemy/enemy_elite7.png` (64 x 64): paired support hardware and rangefinder-module donors.
- `Input/02_Core/core9.png` (32 x 32): cropped, mirrored stable energy-window pattern.

The original colors of the mechanical donors are converted to the shared neutral frame palette.
Broad clean armor panels simplify the silhouettes. The original lit core pattern supplies the central energy window.
Exact source relationships and the energy crop are recorded in `manifest.json`.

## Repeatable generation

From the Aseprite tooling root:

```powershell
.\Scripts\Generate-SystemSupport.ps1
```

To explicitly replace this generated set:

```powershell
.\Scripts\Generate-SystemSupport.ps1 -ReplaceGenerated
```

The runner prints exact source paths, verifies the approved source hashes, creates fresh working copies under Temp, and invokes Aseprite CLI + Lua.
It refuses missing/changed inputs and unrecognized output folders. Both Lua scripts are UTF-8 without BOM.

`validation.json` records size, layers, palette, alpha, symmetry, 2x pixel scale, silhouette differences, and PNG/render equality.
All 12,288 exported pixels match the reopened Aseprite renders. All 18 original Input files retain their hashes.
The artwork uses discrete palette steps with alpha values restricted to 0 and 255, without smooth gradients or anti-aliasing.
The installed PixelLab extension emits its existing batch-startup warning; generation and validation complete successfully.
