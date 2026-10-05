# Sector Administrator Support VFX

Compact, editable Region A effects authored with Aseprite CLI 1.3.18.6 + Lua. **Seven source families, 40 frames, 10 tags.** Approved boss sprites, missile body, static barrier, and Unity assets remain unchanged.

## Contents

Each family includes `.aseprite`, transparent `.png`, `.json` frame/pivot/timing metadata, and individual preview sheets. All newly authored effect pixels have binary alpha, hard edges, a 2px construction grid, and at most four opaque colors per color variant. PNG cells are untrimmed.

| Family | Cell size | Frames / tags | Timing |
| --- | --- | --- | --- |
| `VFX_Sector_CoreMissileFlash` | 32x32 | 6 `Green` + 6 `Purple` | 60, 40, 50, 60, 60, 30 ms per version; 300 ms one-shot |
| `VFX_Sector_EnemyMissileTrail` | 32x16 | 4 `Loop` | 60 ms each; 240 ms loop |
| `VFX_Sector_RectangleRelease` | 32x16 | `Warning` frame 1; `Release` frames 2–6 | Warning seed 100 ms; Release 40, 50, 60, 60, 30 ms = 240 ms |
| `VFX_Sector_RectangleCorner` | 16x16 | Same Warning/Release tags | Same frame clock as the strip |
| `VFX_Sector_BarrierFormationFront` | 32x32 | 4 `Travel` | 80 ms each; 320 ms loop |
| `VFX_Sector_BarrierActivationSpark` | 16x16 | 4 `Activate` | 50, 50, 60, 40 ms; 200 ms one-shot |
| `VFX_Sector_BarrierStabilizationPulse` | 32x16 | 4 `Connect` | 40, 60, 80, 40 ms; 220 ms one-shot |

The core master PNG has two rows: Green above Purple, six cells each. Separate `_Green.png` and `_Purple.png` strips are included. All other master PNGs are one horizontal row. Ring strip and corner also provide separate `_Warning.png` and `_Release.png` exports for direct timing handoff. Individual `_Preview.png` files are review images with opaque backgrounds, not gameplay textures.

Editable layers: **Energy Shape, White-Hot Center, Technical Marks, Pixel Sparks**. Green and purple core clips have identical geometry, six emission ports, alpha masks and timing; only the four-color ramp changes. There is no NULL corruption or redesign of the core.

## Placement and playback

Use **PPU 32**, Point filtering, no texture compression, no mipmaps, and FullRect sprites. JSON records both cell rectangles and pivots. Play one-shots once and return them to the existing pool on completion; stop loops when their owning missile/formation ends. No gameplay logic or pooling system is added by this pack.

| Effect | Pixel pivot from top-left | Unity normalized pivot | Direction |
| --- | --- | --- | --- |
| Core flash | (16,16) | (0.5,0.5) | Radial, centered on the core |
| Missile trail | (28,8) | (0.875,0.5) | Missile travels +X; exhaust extends toward -X |
| Ring strip | (0,8) | (0,0.5) | Horizontal +X |
| Ring corner | (0,0) | (0,1) | Top-left corner; mirror X/Y for others |
| Formation front | (24,16) | (0.75,0.5) | Travel +X, stabilized wake behind it |
| Activation spark | (8,8) | (0.5,0.5) | Existing corner/emitter center |
| Stabilization pulse | (0,8) | (0,0.5) | Tile along the completed edge |

**Core flash:** place at the core attachment position. Gather, compact white flash, opening energy brackets, six outward emission ports, settling sparks, then transparent cleanup. The white flash clears before the outward ports settle, leaving projectiles readable. Peak coverage is 160 pixels, with a 16-pixel white-hot center. Choose the phase tag once per play. Use world scale 1 at PPU 32 or compensate for inherited boss scaling; do not accidentally double the flash under the boss's scaled visual hierarchy.

**Missile trail:** the existing player pursuit/dash and enemy guided missiles both reference `Assets\Space Kit\PlayerBullet.png` (GUID `4c3cbcb7bfe8fbe4ca330cd92ffb72c1`). The actual body is not redrawn or included in the effect sheet. The red exhaust occupies at most 72 pixels, extends up to 24 pixels behind its pivot, is no more than six pixels thick, and retains a four-pixel white propulsion center. Six copies remain lightweight. Attach to a tail point, independent of the missile's nonuniform VisualRoot scale. For a +Y-forward attachment, rotate the +X effect by +90 degrees. Keep the missile body and player readable above the trail; the native review also draws boss armor above overlapping exhaust. Use the existing hostile outline separately if already present.

**Rectangular release:** tile the 32x16 strip along straight edges. Its visual pattern repeats every 16 pixels. Rotate for vertical sides; avoid stretching the whole particle pattern into long rectangles. The small 16x16 corner is derived directly from the strip with a miter mapping, so it shares the exact palette, geometry progression and release timing. It is included because it closes the joins cleanly at variable rectangle sizes.

Reserve a 16x16 corner area at each bend. Tile straight spans **between** corners; do not overlap both. Flip the top-left corner across X/Y for the other corners. Use one shared Release frame for all four sides and corners so the whole ring detonates together. The 160x80 and 224x128 review assemblies demonstrate the same reusable sources at different dimensions; these full-ring PNGs are review-only examples, not fixed-size gameplay effects. Very small rectangles below 32x32 need a different composition and are outside this kit's corner layout.

The ring's center stays empty; peak strip/corner coverage is 50% with binary alpha. The warning seed contains red and no white. Start the five-frame **Release** tag on the existing `AlternatingRectangularAoE` release event: its duration is **240 ms**, matching the inspected current release presentation constant. Do not play the 100 ms warning seed as a replacement for the game's full warning duration. Hold it for whatever warning time the current owner supplies. This art does not define damage, band widths, ring dimensions or colliders.

**Barrier formation:** move the `Travel` sprite with the current growing boundary endpoint. Its short wake includes exact pixels from the approved barrier; the bright leading edge is the new effect. Continue to use the existing static barrier behind it. The source barrier has 64x64 cells with a center stripe at rows 32–33. The front preserves a cropped continuation, with its stripe at rows 16–17. Align the stated pivots to the same wall center. The inspected containment renderer uses a 0.6 Y scale on its 64px source; apply consistent cross-axis scaling to an attached front instead of thickening the wall. Source art previews display raw pixels at native size.

Play `Activate` at the existing corner/emitter as extension begins, then `Connect` once along the completed edge. The connection pulse also repeats every 16 pixels and settles to transparency. The activation effect already contains a small charge stage, so no separate charge node or new corner structure is created.

## Palettes and references

- Green SYSTEM: `254F39`, `4FA96C`, `B3EDAA`, `F0F2E9`, matching approved containment/laser colors.
- Purple Overdrive: `452D66`, `BA79FF`, `D9A5FF`, `FFF6FF`, matching the approved Phase 2 energy ramp.
- Hostile: `662C32`, `FF211F`, `E9913C`, `F0F2E9`. The main red follows the current rectangular-warning color, with established dark red and small orange release accents.

Exact primary references inspected before execution:

- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\06_SectorAdministrator\sector_administrator_idle.png`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\23_SectorAdministrator_Overdrive\sector_administrator_overdrive.png`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\ArtTools\Aseprite\Output\16_SystemBossVFX\VFX_Sector_ContainmentBarrier.aseprite`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\Assets\Space Kit\PlayerBullet.png`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\Assets\02_Scripts\Resources\VFX\PF_Player_MG_DashMissile.prefab`
- `C:\Users\pc\Desktop\빠른 8주 개발\VOID_SCRAPPER\Assets\03_Prefabs\VFX\SectorGuidedMissile.prefab`

The imported barrier PNG matches the approved Output PNG byte-for-byte. All reference work uses verified copies in Temp. Boss and missile reference pixels only appear in review compositions, never in the exported gameplay VFX.

## Review and verification

`SectorSupportVFX_ContactSheet.png` shows all seven requested comparison roles, plus the optional ring corner. Individual previews use integer 3x scaling; the contact sheet uses integer 2x. The `*_480x270.png` files cover core charge/ejection, separated missiles, ring release, barrier formation and completed-edge connection. `SectorSupportVFX_480x270.gif` cycles through core ejection, synchronized rings and barrier formation at a 40 ms review sampling interval. Source ASE/JSON timings remain authoritative.

These are native-pixel **art compositions**, not Unity GameView captures. The missile preview samples the inspected 32x32 body into a 20x10 horizontal representation to approximate its current nonuniform visual scale; no modified missile sprite is delivered. Preview missile speed, staging and wall extension are illustrative and do not change gameplay. Approved reference artwork may retain its original rendering characteristics; the newly generated effects themselves pass binary-alpha and palette checks.

`validation.json` records reopened-source checks for all 40 frames and 10 tags: PNG/ASE agreement, alpha, palette, grid, timing, pivots, cleanup, loop seams, matching core variants, six ejection ports, compact trail bounds, exact barrier wake, 16px tile repetition, synchronized corner geometry, and continuous release rails at both tested rectangle sizes. `reference_hashes.json` records **620 protected files** checked before and after generation.

No Unity assets, prefabs, code, imports, approved sprites, attack timing, projectile behavior, collision geometry, static barriers, or pooling owners were changed. Runtime integration and GameView validation have not been performed by this art task.

## Rebuild

From the project's `ArtTools\Aseprite\Scripts` directory:

```powershell
.\Generate-SectorSupportVFX.ps1 -ReplaceGenerated
```

The runner inventories Input, verifies exact reference paths and SHA-256 hashes, makes fresh Temp copies, checks Lua UTF-8 without BOM, and executes generation plus independent Aseprite validation. Missing or changed references stop the run. Only this generator's recognized output folder can be replaced. The approved shared helper is unchanged. Known PixelLab `handle-pose.lua` startup warnings may remain in batch logs; generation and validation completion are checked separately.
