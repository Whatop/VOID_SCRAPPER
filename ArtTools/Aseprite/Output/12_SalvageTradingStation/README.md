# Salvage Trading Station

First NEUTRAL / TRADER structure for VOID SCRAPPER. A civilian salvage-sector trading and repair station with a modular C-shaped docking ring, a shop office, an external workshop with folded repair arm, freight crates, a coolant tank and a communications dish. The interaction approach is at the bottom. It is designed for a shop offering repairs, Traits and Reinforcements; this delivery is art only.

## Generated assets

All gameplay sprites are 128x128 RGBA on a fixed canvas. The shared center anchor is (64,64), and the suggested approach point is (64,112). No trimming. Art uses the established 2px grid: a 64x64 construction canvas enlarged exactly 2x. No antialiasing, smooth gradients or partial transparency.

| State | Transparent PNG | Layered Aseprite |
|---|---|---|
| Neutral | `salvage_trading_station_neutral.png` | `salvage_trading_station_neutral.aseprite` |
| Shield Active | `salvage_trading_station_shield_active.png` | `salvage_trading_station_shield_active.aseprite` |
| Hostile / Shield Broken | `salvage_trading_station_hostile_shield_broken.png` | `salvage_trading_station_hostile_shield_broken.aseprite` |

- `salvage_trading_station_states.aseprite`: three tagged, single-frame state poses. Each lasts 200ms for inspection; this is not a finished looping animation.
- `salvage_trading_station_states.png`: 384x128 horizontal strip, Neutral / Shield Active / Hostile, with three untrimmed 128x128 cells and no padding.
- `salvage_trading_station_comparison.png`: opaque presentation sheet showing enlarged and native-size states plus isolated civilian service modules.
- `manifest.json`: states, filenames, palette, layer names and approach information.
- `validation.json`: saved-file checks.
- `reference_hashes.json`: hashes of all 18 Input files and the two existing faction comparison sprites.

## Visual decisions

The 128x128 size leaves room for the lower docking entrance and the shield perimeter without shrinking the service modules. An open approach, landing registration, shop sign, wrench stencil and freight modules establish a civilian identity. There are no gun batteries, engines shaped like a warship stern, faction stripes or glowing central combat core.

Neutral uses gray/white cladding, muted teal utility lights and small amber docking guides. Shield Active adds thin, segmented cyan contours and active projector faces. The field interior remains transparent and the underlying station silhouette is preserved.

Hostile / Shield Broken loses two projector housings and part of the starboard cladding. Wiring becomes visible, the dock gate closes, and red alarm strips replace calm approach and service lights. Small local electrical shorts remain around broken hardware, but the protective perimeter is gone. The station retains over 99% of its Neutral silhouette.

The palette contains 15 colors in total: six neutral metals, three muted teals, two shield cyans, two service ambers and two warning reds. Neutral uses 11 visible colors, Shield Active 13, and Hostile 15. Red is absent from the first two states.

## Layers

1. Main Hull
2. Exposed Services
3. Docking and Trade Module
4. Left Repair Utility
5. Right Cargo Module
6. Station Cladding
7. Shield Emitters
8. Antenna and Comms
9. Utility and Warning Lights
10. Shield Field
11. Damage and Debris

Layers share the full canvas for later animation or separate Unity renderers. The Shield Field can be independently hidden or animated. No Unity scenes, prefabs, import settings, shop logic or hostility logic were changed.

## Inspected references

Paths relative to `ArtTools\Aseprite`:

- `Input\90_ReferenceOnly\Station 1.png` — 146x214; broad modular industrial layout.
- `Input\90_ReferenceOnly\Station 2.png` — 318x318; broad circular docking-ring composition.
- `Output\05_SystemStructures\system_green_control_node.png` — 64x64; world pixel scale and neutral construction-color comparison.
- `Output\10_RaiderSalvageCarrier\raider_salvage_carrier_idle.png` — 128x128; comparison to keep civilian station geometry separate from Raider cargo-warship design.

ReferenceOnly assets were used solely for broad composition ideas. No reference pixels were copied, sampled, traced or downscaled into the new station. Aseprite reads byte-verified copies from a timestamped Temp directory. All 20 unique originals were hash-checked before and after generation and remained unchanged.

## Repeatable CLI workflow

Installed output folder: `ArtTools\Aseprite\Output\12_SalvageTradingStation`.

From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-SalvageTradingStation.ps1' -ReplaceGenerated
```

The helper inventories Input, verifies the four exact reference hashes, copies them, checks Lua for UTF-8 without BOM, invokes Aseprite in batch mode, validates reopened exports, then checks original hashes again. The executable is `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe`. Reference copies, logs and the 64px dark/light QA image are retained under `Temp\salvage_trading_station_<timestamp>`.

For an extracted package, use `-ReferenceRoot` to identify the project's `ArtTools\Aseprite` folder. `-ToolRoot` optionally selects a separate build directory. The runner refuses to overwrite an existing output without `-ReplaceGenerated`, which also requires a matching generator ID.

Included scripts: `Generate-SalvageTradingStation.ps1`, `build_salvage_trading_station.lua`, `validate_salvage_trading_station.lua`, and the unchanged generic drawing helper `raider_boss_pixel_helpers.lua`. The shared helper provides only raster and bitmap-font primitives; it contains no faction palette or design geometry.

## Validation

- Reopened all four Aseprite files and checked 128x128 dimensions, 11 layers, and three single-frame master tags.
- 147,456 pixel comparisons matched state sources, PNGs, master frames and sprite-sheet cells.
- Verified binary alpha, exact 2x pixel blocks, palette limits and canvas margins.
- Confirmed no red in Neutral or Shield Active and 260 red warning pixels in Hostile.
- The active Shield Field has 664 pixels, of which 656 lie outside the Neutral silhouette. No Neutral silhouette pixels disappear.
- Six structural layers remain pixel-identical between Neutral and Shield Active.
- Hostile physically removes 200 cladding-layer pixels and 112 emitter-layer pixels; it keeps 99.29% of Neutral silhouette pixels.
- Shield Active and Hostile differ at 408 pixels on the reduced 64x64 construction grid. Native-size and 64px previews were visually checked on dark and light backgrounds.
- All original hashes remained unchanged. Unity rendering and gameplay integration are outside this art-only delivery.

The installed PixelLab extension emits its existing `handle-pose.lua:58` dialog warning during batch startup. Generation and validation returned exit code 0, completion markers were present, and saved-file validation passed. The extension was left unchanged.
