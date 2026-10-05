# Approved visual integration — 2026-09-29

The saved local working tree was inspected before editing. This pass changes sprite bindings and imports existing pixels; it does not generate art or change gameplay. Existing unrelated working-tree changes are preserved. Nothing was committed or pushed.

## Changes

| Production asset/system | Repair or approved source |
| --- | --- |
| Player, Hangar and Curse | Three ShipDefinition previews now resolve to their existing weapon sprites. Restored the missing Curse square from the surviving `ArtTools/Aseprite/Input/01_Player/Px_Player.png` anchor. Restored static player/accent sprites, sliced death fragments, sniper dash echo, and affected reinforcement visuals. The source pixels are unchanged. |
| Common and elite Raiders | Eight broken common bodies and three legacy elite bodies use `Output/03_RaiderEnemy`. Existing role scripts, colliders, movement, patterns and prefab identities remain intact. |
| Tutorial and Expedition HP/Armor | Four fills now use the existing runtime UI fill. Unity resolved their previous GUID to the 2D Sprite package's **Editor-only Square**; they were not truly absent assets. |
| Field Events | Reactor uses `unstable_reactor_dormant`; Black Box uses `black_box_dormant`. Rescue Signal and Unknown Device use their corresponding `Output/14_FieldEvents` art. Missing progress visuals reuse the existing generic circle. |
| Neutral shop | `PF_ShopStructure/Visual` uses `Output/12_SalvageTradingStation/salvage_trading_station_neutral`. |
| SYSTEM | Sector Administrator, Defense Overseer/frigate bodies and Phase Gatekeeper use packs 06–08. Gatekeeper's existing charge slots also reference approved art, preserving array length and timing. Laser support uses pack 04's green unit. Existing Route Core component sockets use the three pack 05 structures. |
| RAIDER structures/bosses | Assault Commander and Barricade/Salvage Carrier use packs 09–10. Existing outpost, power node, storage and turret renderers use pack 13. Base B inherits the Base A sprite changes without flattening its prefab variant. |
| Core Signal and story components | Existing signal pips use pack 02's core icon. The three campaign component definitions use distinct green/orange/blue icons through the existing story UI binding. |
| Currency and Cargo | Credits, Scrap, Core Shards, Tuning Chips and Heal use matching pack 19 sprites in world pickups and relevant UI. Stabilized Alloy reuses the existing plating glyph rather than sharing Scrap's image. |
| Traits | Filled all 24 missing icons with nearby existing `Config/Icons` glyphs. All 72 production traits now have icons. Pickup fallbacks also reuse existing glyphs. |

There were 42 references to five genuinely absent sprite GUIDs in the inspected production data. **41 were repaired.** The remaining reference is the excluded decorative Repair-screen illustration, preserved exactly from the saved baseline. The four package-based gauge references were corrected separately.

49 PNG imports live under `Assets/Art/ApprovedIntegration`, with Unity-created metadata. All 49 match existing source files byte for byte: 44 copies from Output, two copies of the surviving player anchor, two circle copies for existing world bounds, and one existing plating glyph copy. No source PNG, existing importer metadata or existing GUID was changed.

Per-role import sizes fit the previous sprite bounds without changing transforms or colliders. Rectangular footprints are retained as limits; square replacements fit inside them. Existing animation bindings and state authorities are preserved. No new animation/state driver was added.

## Changed files

66 existing serialized assets changed:

- Scenes: `Boot.unity`, `Settlement.unity`, `Expedition.unity`, `Tutorial.unity`.
- ScriptableObjects: 24 TraitDefinitions, the three production ShipDefinitions, and `BossCampaign_Region1/2/3.asset` (30 total).
- 32 prefabs: affected Raider enemies, SYSTEM/RAIDER bosses, support, faction base A, shop, turret, four field events, currency/trait/reinforcement pickups, reinforcement area visuals, sniper dash echo, inventory UI and the trait-node button.

New files include the imported PNGs and their Unity-generated metadata, plus these Editor-only tools:

- `Assets/02_Scripts/UI/Editor/ApprovedVisualIntegration.cs`
- `Assets/02_Scripts/UI/Editor/ApprovedVisualIntegrationSmoke.cs`
- `Assets/02_Scripts/UI/Editor/Tests/ApprovedVisualIntegrationTests.cs`

The exact changed-file list and source-to-import mapping are in [preservation.json](../../Logs/VisualIntegration/preservation.json). Runner, binding evidence and original snapshots are under `Logs/VisualIntegration/`. The inventory is measured against the starting working tree, not against Git HEAD, which already contained substantial unrelated changes.

## Validation

- Unity 6000.0.69f1 imported and authored the references successfully.
- Focused EditMode suites: **113/113 passed**. These include the new integration checks and existing HUD/Hangar, Route Core combat/authoring/shader, boss and enemy/base regression checks. [Results](../../Logs/VisualIntegration/tests.xml)
- The first test attempt exposed a missing scene cleanup in the new fixture. Fixed the fixture and reran successfully; no production lighting change was made.
- Real Play Mode **Boot → Settlement → Expedition** passed through `SceneFlowManager` and `RunManager`. The Hangar bindings were checked and a fresh run/player were present. The smoke used a workspace-only save filename. **Console errors/exceptions/assertions: 0.** [Evidence](../../Logs/VisualIntegration/smoke.txt)
- Reviewed actual 480×270 camera captures: [Boot](../../Logs/VisualIntegration/Rendered/01-boot.png), [Settlement](../../Logs/VisualIntegration/Rendered/02-settlement.png), [Expedition](../../Logs/VisualIntegration/Rendered/03-expedition.png).
- Offline preservation audit: zero unresolved sprite GUIDs within scope; the one excluded Repair illustration is separately recorded. Object/component IDs and existing numeric component data are retained. Verified unchanged GameObjects, transforms, colliders, rigidbodies and Animator bindings against the starting snapshots.
- All original metadata and source-art hashes match the baseline. No runtime C# file was changed by this pass.

This is a startup/navigation smoke and focused regression run, not a full campaign playthrough or a rendered review of every boss/state. Existing small text/large preview scaling remain layout matters outside this sprite-binding pass.

## Remaining visuals

| Remaining item | Status / next requirement |
| --- | --- |
| NULL Dispatcher | Deliberately untouched. `TemporaryCoreVisual`, `InnerCoreRoot`, both Phase Relays and `ReclaimBreakPulse` still reuse `core5`; four shell remnants use flat Square sprites. Dedicated NULL outer shell, exposed core, shell fragments, relay and reclaim/break presentation remain required for a future art pass. |
| Repair-screen decorative illustration | Existing missing `Repair_HUD/PreViewImage` reference retained exactly. Deferred with the other low-priority Settlement illustrations as requested. |
| Extra staged states and roles | Orange/blue support units and Raider Sniper Commander have no matching dedicated production binding in this pass. Additional shop/boss/event poses also require an existing state slot or a separately scoped presentation integration; no gameplay role or animation driver was invented to use them. |
| Trait / Alloy / generic effect glyphs | Functional existing-art reuse, not newly authored unique icons. Generic circles and the restored Curse anchor remain intentional fallbacks. |

DarkUI styling and existing decorative Settlement illustrations are unchanged. Equipment values/fitting, Sector Technology values, ShipDefinition gameplay stats, Route Core combat/shaders, boss/enemy balance, campaign progression and save schema are unchanged. No commit or push was performed.
