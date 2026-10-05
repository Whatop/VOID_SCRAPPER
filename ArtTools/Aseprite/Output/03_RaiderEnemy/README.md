# Standard raider enemy set

Four static, north-facing 64 x 64 units with transparent backgrounds.
Every unit has one layered `.aseprite` file and one matching `.png`.

| Unit | Files | Differentiation |
| --- | --- | --- |
| Basic | raider_basic.aseprite / raider_basic.png | Compact offset gun; green cockpit and receiver light |
| Shotgun | raider_shotgun.aseprite / raider_shotgun.png | Wide, short scattergun mounts; orange cockpit and receivers |
| Sniper / charging | raider_sniper_charging.aseprite / raider_sniper_charging.png | Long central rail, side charging pack; blue energy |
| Elite | raider_elite.aseprite / raider_elite.png | Heavy armored shoulders, widened cannon pods, stronger red warning marks |

All units use light-gray/white armor, dark mechanical internals, and red faction markings.
The editable layers are Shared Hull, Weapon Mounts, Faction Markings, and Role Lights.
`raider_enemy_preview.png` shows 3x enlarged sprites and their native 64 x 64 appearance.

## Source selection

All source reads during generation use verified copies in the Temp run directory.
The originals are preserved, and the complete Input inventory is hash-checked before and after generation.

- Shared normal hull: `Input/00_StyleAnchors/Pirate_Common_Candidates_32x32.png`, row 2 column 5 (one-based), source rectangle x=128, y=32, w=32, h=32.
- Elite body: `Input/00_StyleAnchors/Pirate_Elite_Candidates_64x64.png`, row 4 column 1 (one-based), source rectangle x=0, y=192, w=64, h=64.
- The elite reuses the common hull's cockpit module at 2x pixel scale.
- Both anchor sheets are 256 x 256. Common cells are 32 x 32; elite cells are 64 x 64.
- The common hull is preserved across the three normal roles, with exact 2x nearest-neighbor replication. The sniper hull is shifted down two source pixels to accommodate the rail.
- The elite remains at its native 64 x 64 grid. No 96 x 96 canvas was necessary.

The palette is quantized to simple neutral armor, red faction colors, and a three-color role ramp.
There are no smooth gradients or partially transparent edge pixels.

## Repeatable generation

From the Aseprite tooling root:

```powershell
.\Scripts\Generate-RaiderSet.ps1
```

To explicitly replace this generated set:

```powershell
.\Scripts\Generate-RaiderSet.ps1 -ReplaceGenerated
```

The runner inspects and prints the exact source paths, verifies approved hashes, creates fresh working copies, and invokes Aseprite CLI + Lua.
It refuses missing/changed anchors and an unrecognized output folder. Both Lua scripts use UTF-8 without BOM.
`manifest.json` records source rectangles and output structure; `validation.json` records size, layer, alpha, palette, scaling, silhouette, and pixel-match checks.
All 16,384 output pixels were compared with reopened Aseprite renders. All 18 original Input files remained unchanged.

The installed PixelLab extension emits its existing batch startup warning; the generator and validator complete successfully.
