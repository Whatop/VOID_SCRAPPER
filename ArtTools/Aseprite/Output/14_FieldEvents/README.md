# Field Event Objects

Four exploration objects for VOID SCRAPPER, with thirteen state poses. These are environmental event assets, not another military faction. Each has its own silhouette and purpose: a life capsule, a folded artifact, a heavy pressure reactor, and a portable data recorder.

## Objects and exported states

| Object | Canvas | Aseprite source | State tags, in export order |
|---|---|---|---|
| Rescue Signal | 64x64 | `rescue_signal.aseprite` | `idle`, `signal_active`, `completed` |
| Unknown Device | 64x64 | `unknown_device.aseprite` | `dormant`, `analyzing`, `unstable` |
| Unstable Reactor | 96x96 | `unstable_reactor.aseprite` | `dormant`, `active`, `critical`, `destroyed` |
| Black Box | 64x64 | `black_box.aseprite` | `dormant`, `data_recovery`, `recovered` |

Each state is saved as a transparent `<object>_<state>.png`. Each Aseprite source contains its state poses as single-frame tags, with 200ms inspection durations. These are state poses, not finished looping animations.

The `<object>_states.png` strips follow the tag order above. Rescue Signal, Unknown Device and Black Box strips are 192x64. The reactor strip is 384x96. All cells have fixed native-size canvases, with no padding or trimming. The anchor remains at the canvas center.

`field_events_comparison.png` is an opaque presentation sheet showing all thirteen poses enlarged 2x, plus native-size examples of the three smaller objects. `manifest.json` records states, dimensions, palettes, layers and files. `validation.json` contains saved-file checks; `reference_hashes.json` records preservation hashes.

## Readable event progression

**Rescue Signal:** a narrow, worn life capsule with a damaged flank, attached life-support canister and offset beacon. Pale-green radio arcs intensify in Signal Active. Completed opens the hatch, exposes empty seating and stops the distress pulses. Its tiny red emergency indicator also switches off.

**Unknown Device:** a hollow triangular ribbon with unequal corner pieces, floating joins and a small internal shape. Analyzing routes purple energy through the structure. Unstable locally displaces a join and introduces short angular discharges. The artifact remains intact and restrained, without NULL tendrils, faction emblems or broad corruption bands.

**Unstable Reactor:** a substantially heavier frame with paired cooling vessels, external pipes, containment bars and warning segments. Dormant is dark; Active shows orange pressure cells and three amber segments. Critical spreads the containment bars, expands the hot stack and activates all five warning segments in red. Destroyed removes the chamber and much of its casing, leaving broken pipes, fallen bars and a few dim embers. The segment display is illustrative state art, not a live gameplay timer.

**Black Box:** a reinforced portable recorder with a carry handle, data contacts and a short antenna. Data Recovery opens its cover, exposes recorder cells and sends discrete cyan packets. Recovered removes the entire case and antenna, leaving an unlit, hollow mounting cradle and loose connector.

## Editable layers

All four sources use these eight layers:

1. Main Body
2. Mechanisms and Cables
3. Shell and Covers
4. Energy or Data
5. Antenna and Ports
6. Signal and Lights
7. Damage
8. VFX

The shared center anchor and full canvas are preserved across poses for later animation or separate renderers. The recorder case is on Shell and Covers, so it can be hidden after recovery. The capsule's hatch movement and the reactor's wreck are authored geometry changes, not recolors.

## Pixel style and references

Art uses the established 2px construction grid: logical 32x32 or 48x48 enlarged exactly 2x. Every gameplay pixel is either fully opaque or fully transparent. There is no antialiasing, smooth gradient, blur or semitransparent glow. Each pose uses 6-13 visible colors from its object's limited palette.

The Input inventory was inspected first. Three exact existing world references were then checked for material contrast, scale and visual separation, relative to `ArtTools\Aseprite`:

- `Output\04_SystemSupport\system_support_blue.png` — 64x64
- `Output\12_SalvageTradingStation\salvage_trading_station_neutral.png` — 128x128
- `Output\13_RaiderBase\raider_power_node_normal.png` — 64x64

All were copied to a timestamped Temp directory before Aseprite read them. No reference pixels or silhouettes were copied into the event objects. The existing neutral metal range provides world consistency, while faded capsule markings, restrained purple artifact energy, reactor heat and cyan data cues distinguish their event functions. All 18 Input files and three world references remained unchanged by SHA256.

## Repeatable Aseprite workflow

Project installation: `ArtTools\Aseprite\Output\14_FieldEvents`, with scripts in `ArtTools\Aseprite\Scripts`.

From the project root:

```powershell
& '.\ArtTools\Aseprite\Scripts\Generate-FieldEvents.ps1' -ReplaceGenerated
```

The runner uses `D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe` in batch mode. It inventories Input, verifies reference hashes, makes byte-verified copies, checks Lua as UTF-8 without BOM, generates the assets, reopens the saved files for validation, and checks original hashes again.

For an extracted package, use `-ReferenceRoot` to point at the project's `ArtTools\Aseprite` directory. `-ToolRoot` optionally selects a different build directory. Existing output is refused unless `-ReplaceGenerated` is supplied and the folder's generator ID matches.

Scripts: `Generate-FieldEvents.ps1`, `build_field_events.lua`, `validate_field_events.lua`, and the unchanged shared `raider_boss_pixel_helpers.lua`. That helper contains only raster/font primitives, not faction palettes or silhouettes.

Reference copies, logs, half-size dark/light readability images and a silhouette sheet are retained under `Temp\field_events_<timestamp>`.

## Verification

- Reopened all four Aseprite sources and verified target dimensions, eight layers, thirteen frames total and matching single-frame tags.
- 147,456 pixel comparisons matched all thirteen PNG states and four strips against the saved source frames.
- Verified binary alpha, fixed canvases, exact 2x pixel blocks, limited object palettes and distinct state pixels.
- Completed rescue opens the hatch and removes all signal VFX; bright signal pixels increase from 28 in Idle to 152 when active.
- Critical reactor has 344 red warning pixels. Destroyed loses the chamber and substantial geometry, with only four hot-orange pixels left.
- Recovered Black Box has no case, antenna, transmission VFX or lit data pixels; its visible footprint drops from 1,332 to 500 opaque pixels.
- All six pairwise event silhouettes differ by at least 344 pixels on a common 48x48 comparison canvas, alongside visual silhouette review.
- Inspected enlarged, native-size and half-size views on dark and light backgrounds.
- All 21 original hashes remained unchanged. No Unity scenes, prefabs, interaction logic, attackability, countdowns, rewards or tracking events were changed or runtime-tested.

The existing PixelLab extension emits `handle-pose.lua:58` dialog warnings on batch startup. Final generation and validation both returned exit code 0, completion markers were present, and saved-file checks passed. The extension was not changed.
