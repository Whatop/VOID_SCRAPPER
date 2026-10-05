# VOID SCRAPPER — World Reward / Pickup Visual Pack

**10 physical world pickups** and **4 reusable rarity treatments in 3 native sizes**. The pack contains 13 layered Aseprite sources and 25 authored frames/states. Created with Aseprite CLI + Lua for a 480×270 presentation at PPU 32. Existing approved art remains unchanged.

All gameplay PNGs have transparent backgrounds, hard pixel edges, limited palettes and binary alpha. New 32/48-pixel pickups use a 2-pixel construction grid. The 16-pixel currency items, thin rarity marks and approved Core Shard retain native one-pixel detail. No antialiasing or gradients are used.

## Pickups

| Filename prefix | Canvas | Design | Playback |
|---|---|---|---|
| Pickup_Credit | 16×16 | Stacked gold data-credit units | Static |
| Pickup_Scrap | 16×16 | Irregular gray mechanical bundle with small orange tie | Static |
| Pickup_CoreShard | 32×32 | Approved chipped core diamond, recolored neutral white | Optional four-frame idle |
| Pickup_TuningChip | 32×32 | Teal circuit package with exposed pins | Static |
| Pickup_RegionResource_A | 32×32 | Green material rod with metal clamps | Static |
| Pickup_RegionResource_B | 32×32 | Compressed orange crystal with holding tabs | Static |
| Pickup_RegionResource_C | 32×32 | Blue notched phase/navigation wafer | Static |
| Pickup_Heal | 32×32 | White repair case with green maintenance cross | Static |
| Pickup_TraitContainer | 48×48 | Tall passive-reward capsule with blank icon socket | Static |
| Pickup_ReinforcementContainer | 48×48 | Wide hardware case with side handles and blank icon socket | Static |

Each pickup has an `.aseprite`, a native-size `.png` base image and JSON metadata. The Core Shard also has `Pickup_CoreShard_Idle.png`, a 128×32 strip. Its Idle tag contains four frames at **900,120,100,380 ms**, a 1500 ms loop. Only a small highlight changes; the underlying approved shard silhouette remains exact. Its base PNG is the quiet first frame. All other pickups use a single Static tag; their 100 ms source-frame duration is a placeholder, not an instruction to animate them.

Pickup layers: **Body**, **Material Accent**, **Details**, **Idle Sparkle**. Both containers also have an empty **Item Icon Overlay** layer for later authoring. No assigned Trait or Reinforcement icon is baked into a gameplay sprite.

## Reusable rarity presentation

Use the rarity source matching the pickup canvas:

- `Pickup_Rarity_16.aseprite`
- `Pickup_Rarity_32.aseprite`
- `Pickup_Rarity_48.aseprite`

Each has four one-frame tags: **Common**, **Rare**, **Legendary**, **Curse**. These are selectable states, not a four-frame animation. Their two editable layers are **Frame** and **Glow Marks**. Thin, hard-edged marks provide the glow impression without blurred halos.

| Rarity | Color | Mark language |
|---|---|---|
| Common | Neutral gray/white | Small open corner marks |
| Rare | Blue/cyan | Corners with small top/bottom ticks |
| Legendary | Gold | Corners with doubled side or top accents |
| Curse | Purple | Offset, broken corner segments |

Individual transparent PNGs use `Pickup_Rarity_<size>_<rarity>.png`. Combined horizontal atlases use `Pickup_Rarity_<size>.png`: 64×16, 128×32 and 192×48. JSON supplies the one-frame tag ranges and rectangles.

Compose **one base pickup + one selected same-size rarity overlay**, centered at the same position. Do not scale a 16-pixel frame up to fit a 48-pixel case; use the supplied size. All 40 pickup/rarity combinations were checked: rarity pixels do not cover any base pickup pixels or either assigned-icon socket. Basic currency may omit its Common overlay to keep combat loot quiet. No per-item/per-rarity composite gameplay sprites were generated.

## Assigned icon sockets

Both containers reserve a flat dark 16×16 backplate. Add the actual item icon as a separate Unity child sprite above that backplate; the rarity frame remains a separate renderer. Preserve the icon's aspect ratio, Point sampling and integer placement. There is one generic container per reward category, rather than one sprite for every possible item.

Coordinates below are measured from the PNG's top-left corner. Container pivot is centered, PPU 32.

| Container | Icon rectangle (x,y,w,h) | Pixel center | Child local position at PPU 32 |
|---|---|---|---|
| Trait | 16,14,16,16 | 24,22 | (0, 0.0625, 0) |
| Reinforcement | 16,16,16,16 | 24,24 | (0, 0, 0) |

The contact sheet shows the Credit sprite in these sockets solely to demonstrate placement. Those example composites are review content, not exported item variants.

## Unity import notes

Use Sprite (2D and UI), **PPU 32**, Point filtering, compression None, mipmaps off and Full Rect. Native base PNGs can use Sprite Single. Use Multiple for the Core Shard idle sheet or a rarity atlas, slicing fixed cells without trimming. All supplied sprite pivots are normalized (0.5,0.5).

JSON rectangles use PNG top-left coordinates. Convert texture rectangle Y to Unity's bottom-left convention with `sheetHeight - frame.y - frame.h`. Only CoreShard/Idle should loop. Other pickup and rarity sources are static selections. Keep world placement on integer screen pixels and presentation scaling integer. No Unity assets, importer settings or runtime code were changed; Unity combat playback has not been tested.

## Review assets and validation

- `WorldPickups_ContactSheet.png`: all pickups enlarged for inspection, all 12 rarity overlays, and the two icon-socket examples. Enlargement is exact nearest-neighbor replication.
- `WorldPickups_480x270.png`: native-size comparison of all ten pickups, plus four overlays composed over the same neutral shard.
- `WorldPickups_480x270.gif`: native review over one 1500 ms idle cycle, 75 frames at 20 ms. Only the Core Shard glints; currency and rarity frames stay still. Review backgrounds are opaque; gameplay exports remain transparent.
- `manifest.json`: inventory, dimensions, tag names, rarity-source mapping, palettes and icon socket coordinates.
- `validation.json`: successful source/PNG checks, **54,272 pixel comparisons**, frame sizes/timings/tags, editable layers, binary alpha, palettes, integer construction, margins, exact approved shard mask, blank icon backplates and zero-overlap checks for all 40 rarity combinations.
- `reference_hashes.json`: **450 original files** across Input and approved Output folders 02–18 were verified unchanged.

## Inspected references and repeatable workflow

The complete Input folder was inventoried before generation. These files were inspected and copied before use (relative to `ArtTools/Aseprite`):

- `Output/02_Core/green/core_green_shard.aseprite`
- `Output/02_Core/green/core_green_shard.png`
- `Output/02_Core/green/core_green_icon.png`
- `Output/02_Core/orange/core_orange_icon.png`
- `Output/02_Core/blue/core_blue_icon.png`
- `Output/14_FieldEvents/black_box_dormant.png`

The neutral shard retains the copied approved source's Housing, Energy and Details geometry, mapped into Body, Material Accent and Details. Its six-color ramp is changed to neutral white/gray; a separate Idle Sparkle layer supplies the optional animation. Other references guided the family palette and industrial construction.

Included task scripts: `Generate-WorldPickups.ps1`, `build_world_pickups.lua`, `validate_world_pickups.lua`, plus the unchanged shared pixel helper. Lua files are UTF-8 without BOM. From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-WorldPickups.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`, verifies source paths and inspected hashes, works on copies, generates only `Output/19_WorldPickups`, then reopens/validates exports and checks originals again. Missing or changed sources stop generation. `-ReplaceGenerated` permits rebuilding only this generator's folder; preserve manual edits first. Successful-run copies and logs are in `Temp/world_pickups_20260929_222345_167`.

For an extracted package outside the project, pass `-ReferenceRoot` pointing to the project's `ArtTools\Aseprite` directory. `-ToolRoot` can direct generated Output/Temp into another workspace.

The installed PixelLab extension emitted its existing `handle-pose.lua:58` / `dlg` startup warning. Both final Aseprite passes completed successfully and all checks passed. The extension was not modified.
