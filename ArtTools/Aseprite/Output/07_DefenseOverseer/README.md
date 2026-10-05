# DEFENSE OVERSEER

SYSTEM / Region B / orange. A broad, connected mobile defense platform with two integrated triple-barrel batteries and reinforced central armor. Newly authored geometry; Sector Administrator was used only as a comparison reference.

## Files

| State | Editable source | Transparent sprite |
| --- | --- | --- |
| Idle | defense_overseer_idle.aseprite | defense_overseer_idle.png |
| Armed / barrage charged | defense_overseer_barrage_charged.aseprite | defense_overseer_barrage_charged.png |
| Armor broken / exposed reactor | defense_overseer_armor_broken.aseprite | defense_overseer_armor_broken.png |

All state canvases are 128x128. Front faces up. Use the same center anchor (64,64) / normalized (0.5,0.5) for all states; no trimming. The reactor center is (63.5,69.5) in top-left image coordinates.

- `defense_overseer_states.aseprite`: three single-frame tags (`idle`, `barrage_charged`, `armor_broken`), ten editable layers. Each pose is stored at 200 ms for inspection; this is a state library, not a completed looping animation.
- `defense_overseer_states.png`: transparent 384x128 strip in that order, 128x128 cells, no padding.
- `defense_overseer_comparison.png`: enlarged and native-size views plus approved Orange Assault / Orange Defense references and a Region A silhouette comparison. This presentation image has an opaque background.
- `manifest.json`: state descriptions, palette, layer mapping, orientation and alignment.
- `validation.json`, `reference_hashes.json`, `generation_complete.txt`: verification results, unchanged-reference hashes and completion marker.

## State language

Idle: closed blast armor, small reactor inspection port, capped suppression cells and standby barrels.

Barrage charged: the central armor stays closed. All six gun bores, battery feed rails and recessed suppression cells light up, concentrating the charge cue on the weapons.

Armor broken: both central blast covers and the forward collar are removed, leaving broken mounting edges and a large dark reactor vault. The exposed orange reactor is much larger; weapon energy is dimmed. The hull remains stable SYSTEM hardware, with no Raider patches or corrupted effects.

The boss has a continuous, low horizontal hull rather than Region A's four separated modules. The approved white/gray armor palette, orange energy, beveled plating and diamond reactor motif retain the family identity.

## Editing and animation

The ten layers separate chassis, perimeter armor, left/right batteries, reactor, left/right blast covers, fore/aft armor, power distribution and registration. Battery and blast-cover layers can be animated individually; shared power/registration layers contain a few remaining indicators. PNG exports retain transparent backgrounds and exact 2x pixels with no partial alpha or anti-aliasing. The states use 10, 12 and 13 visible palette colors respectively.

No Unity assets or runtime behavior were changed. Gameplay timing, weapon effects and transitions remain for later animation/integration.

## Rebuild

In the existing project's `ArtTools\Aseprite` directory:

```powershell
& .\Scripts\Generate-DefenseOverseer.ps1 -ReplaceGenerated
```

The helper uses the configured Aseprite executable, verifies exact approved paths/hashes, creates timestamped reference copies in Temp, and runs the Lua generator and validator. It refuses to overwrite an unknown output folder. All Lua files are UTF-8 without BOM.

Required scripts: `Generate-DefenseOverseer.ps1`, `build_defense_overseer.lua`, `validate_defense_overseer.lua`, `system_boss_pixel_helpers.lua`. The pixel helper contains generic raster drawing and font routines only. Approved source files stay in the existing project; the archive does not duplicate them.

## Verification

- 147,456 pixel comparisons: standalone PNGs equal reopened Aseprite poses, tagged master frames and strip cells.
- Verified dimensions, tags, layers, binary alpha, exact 2x grid, palette, bilateral symmetry and common alignment.
- Mid-deck is continuously filled across the platform; 4,232 silhouette pixels differ from Region A.
- Broken state removes about 91% of central light armor and exposes 4.8x the central orange area of the charged state.
- Charged versus broken differs by 716 pixels even on the 64x64 construction grid; 64px dark/light-background render inspected separately in Temp.
- All 18 Input files and five approved reference PNGs unchanged by SHA256.

The existing PixelLab extension prints `handle-pose.lua:58` during batch startup. Both final Aseprite processes exit successfully and all generated-file checks pass; the extension was left unchanged.
