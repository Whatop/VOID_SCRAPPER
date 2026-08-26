# Pirate Common Candidates 32x32 - Asset Audit and Role-Mapping Proposal

## Scope

This is a review-only artifact. No production prefab, `EnemyDefinition`, scene, map-generation asset, source texture, importer metadata, collider, fire point, AI, or gameplay code was changed.

The visual assessment uses the numbered contact sheet beside this document:

`Documentation/ArtReview/Pirate_Common_Candidates_32x32_ContactSheet.png`

## Imported asset

| Property | Current value |
|---|---|
| Texture | `Assets/Space Kit/Enemys/Pirate_Common_Candidates_32x32.png` |
| GUID | `d49e64aced3373c46aeee19b6dc2762c` |
| Texture dimensions | 256 x 256 |
| Import type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Sub-sprites | 64 (`_0` through `_63`) |
| Rects | 32 x 32, regular 8 x 8 grid |
| Effective pivot | Center (`alignment: 0`); each raw sprite record serializes `pivot: {x: 0, y: 0}`, which is ignored unless alignment is Custom |
| Pixels Per Unit | 32 |
| Filter Mode | Point |
| Mipmaps | Disabled |
| Wrap Mode | Clamp |
| Default compression | Uncompressed (`textureCompression: 0`); platform records are not overridden |
| Read/Write | Disabled |
| Authored forward | +Y / up, inferred consistently from nose, canopy, and engine placement across all 64 sprites |
| Current repository usage | None; the texture GUID is not referenced outside its own `.meta` |

The contact sheet uses 8x nearest-neighbor enlargement, a dark neutral background, exact sub-sprite labels, gray 32x32 source bounds, a cyan effective-pivot cross, and an orange +Y authored-forward marker. Every sub-sprite appears exactly once.

## Current common-enemy architecture

Combat identity is stored in `EnemyType`: Basic (0), Shotgun (1), Charging (2), and MeleeCharger (9). Simulation behavior is separate in `EnemyRoleType`: Patrol (0), Defender (1), RivalHarvester (2), and Scavenger (3).

The three current combat prefabs already contain `EnemyRoleController` with Patrol as the default role. `ExpeditionMapGenerator` reconfigures spawned Basic/Shotgun instances as Defender. The current map config requests 20 Basic, 5 Shotgun, 4 Charging, 2 Melee Charger, 4 Defender Basic, 2 Defender Shotgun, 1 Rival Harvester, and 1 Scavenger. Rival Harvester and Scavenger have dedicated definitions assigned in `Expedition.unity`, but both definitions currently have a null `enemyPrefab`; `PlaceRoleEnemyBatch` therefore returns without spawning them. `Melee_Charger.asset` also has a null `enemyPrefab`, so its configured count currently short-circuits before placement.

### Current production prefab geometry

| Definition | Definition GUID | Prefab | Prefab GUID | Current sprite | Renderer / animation | Root scale | Solid world collider | FirePoint |
|---|---|---|---|---|---|---|---|---|
| `Basic_enemy.asset` | `e6e5bef48873ddd4488e1def09de0b86` | `Assets/03_Prefabs/Enemy/Enemy_Basic.prefab` | `a69d9f77d4313b244b3a17fd1c67de4c` | `Assets/Space Kit/Enemy/enemy_basic.png`, GUID `b8afc480fbf367743a4cfca26006360d`, fileID `21300000` | Root SpriteRenderer; no Animator | `(0.6, 0.6, 1)` | Child Circle, radius `0.2859825`, near-zero offset; root also has a legacy sprite-shaped trigger PolygonCollider2D | `(0, 0.567, 0)` |
| `Shotgun_enemy.asset` | `2fa9f4364489b8b48a441bfe87b8323e` | `Assets/03_Prefabs/Enemy/Enemy_Shotgun.prefab` | `9728b73660a7a8b43a1d5d7a9cbc0f70` | `Assets/Space Kit/Enemy/enemy_shotgun.png`, GUID `13df92a0b135f1a43aad3f476af539cb`, fileID `21300000` | Root SpriteRenderer; no Animator | `(0.8, 0.7, 1)` | Child Circle at `(0, -0.143)`, local scale `(0.75, 0.85714275)`, radius `0.548111`; root legacy trigger polygon | `(0, 0.38, 0)` |
| `Charge_enemy.asset` | `6e238d53877964e4691b4ac037651abd` | `Assets/03_Prefabs/Enemy/Enemy_Charge.prefab` | `6d6ab1ffbc7a62c44b72878be7f98509` | `Assets/Space Kit/Enemy/enemy_charging.png`, GUID `4d772bc3886367f4b9e13222f4d4d2bf`, fileID `21300000` | Root SpriteRenderer; no Animator | `(1, 1, 1)` | Child Circle, local scale `(0.6, 0.6)`, radius `0.4571011`, offset `(-0.00477, -0.06673)`; root legacy trigger polygon | `(0, 0.4, 0)` |
| `Melee_Charger.asset` | `c26b857c6ed242908d7bb62389d32a15` | None | None | None | No production prefab | N/A | N/A | N/A |
| `Rival_Harvester.asset` | `cb6415f8e7824dc8962c061786475dd2` | None | None | None | No production prefab | N/A | N/A | N/A |
| `Scavenger.asset` | `13f56c2aae0a4665906f86e3f4198448` | None | None | None | No production prefab | N/A | N/A | N/A |

All three current sprites face +Y and the AI uses `rotationOffset: -90`. Their `visualRoot` fields are unassigned, so the root transform is the current rotation/presentation authority. Hit/death/recoil behavior is component-driven rather than Animator-driven. A later sprite application should preserve the root SpriteRenderer dependency, but should not retain the old sprite-generated trigger polygon without inspection.

## Candidate-by-candidate audit

All candidates use the same effective center pivot and +Y authored direction; therefore the per-candidate `Pivot / facing` value is `Center / +Y` for every row below. `Current use` is None for every row because no asset currently references this texture GUID. Pixel bounds are the non-transparent bounds within each 32x32 rect and are included for variant-safety decisions.

| Candidate | FileID | Rect | Pixel bounds | Silhouette | Visible equipment / armor | Current use | Likely roles and constraints |
|---|---:|---|---|---|---|---|---|
| `Pirate_Common_Candidates_32x32_0` | `-165298391677540540` | `(0,224,32,32)` | `20x29` | Medium rounded, slightly asymmetric | Central cyan canopy; rounded side armor; no clear gun | None | Basic or Defender; asymmetry is readable but not role-specific |
| `Pirate_Common_Candidates_32x32_1` | `3074508233203580657` | `(32,224,32,32)` | `20x28` | Chunky left-heavy slab | Broad armor plate and lower red markings | None | Defender or utility; asymmetric mass makes muzzle alignment less obvious |
| `Pirate_Common_Candidates_32x32_2` | `8821114818567158907` | `(64,224,32,32)` | `14x26` | Narrow rectangular craft | Twin upper prongs and red side bands | None | Scout, Basic, or light artillery; too narrow for Shotgun/Defender |
| `Pirate_Common_Candidates_32x32_3` | `-3692818513181773324` | `(96,224,32,32)` | `19x25` | T-shaped arms around a narrow body | Long side structures; no obvious central weapon | None | Support or Scout; broad side hardware may imply non-combat utility |
| `Pirate_Common_Candidates_32x32_4` | `8313702003833434161` | `(128,224,32,32)` | `22x27` | Broad rounded shoulders | Reinforced side pods and lower red panel | None | Shotgun alternate or Basic heavy variant; centered muzzle remains plausible |
| `Pirate_Common_Candidates_32x32_5` | `6287638510399086547` | `(160,224,32,32)` | `14x24` | Small narrow box | Paired red side markings and small lower appendage | None | Scavenger alternate or Scout; intentionally light silhouette |
| `Pirate_Common_Candidates_32x32_6` | `-7918928145898029670` | `(192,224,32,32)` | `24x26` | Broad V/shoulder silhouette | Wide reinforced prow/side pods and central keel | None | Strong Shotgun candidate; width requires broad trigger/circle review |
| `Pirate_Common_Candidates_32x32_7` | `-2778024909581857607` | `(224,224,32,32)` | `20x26` | Compact layered armor | Side tips and multiple horizontal armor bands | None | Defender or Heavy Basic; could read above ordinary Basic weight |
| `Pirate_Common_Candidates_32x32_8` | `8996437089386081207` | `(0,192,32,32)` | `20x28` | Broad upper body with asymmetric lower fork | Open/forked lower bay and red service marking | None | Harvester or utility; off-center bay argues against shared centered muzzle assumptions |
| `Pirate_Common_Candidates_32x32_9` | `-526751674481448348` | `(32,192,32,32)` | `20x28` | Medium T hull | Symmetric side pods and long centerline tail | None | Basic or Support; safe centered fire point |
| `Pirate_Common_Candidates_32x32_10` | `4168453528741510495` | `(64,192,32,32)` | `14x29` | Very narrow spear | Twin upper rails and slim body | None | Scout or light Charging; too narrow for shared broad colliders |
| `Pirate_Common_Candidates_32x32_11` | `1259549369392133270` | `(96,192,32,32)` | `14x28` | Narrow asymmetric frame | External side rail/tube and long lower spar | None | Charging artillery or utility; weapon side is visually ambiguous |
| `Pirate_Common_Candidates_32x32_12` | `-1645576424982620573` | `(128,192,32,32)` | `22x26` | Rounded armored pod | Large side armor and central lower spar | None | Shotgun alternate or Defender; similar footprint to `_4` |
| `Pirate_Common_Candidates_32x32_13` | `-4443790576403174989` | `(160,192,32,32)` | `16x25` | Small compact fuselage | Minimal armor, simple centerline | None | Scout, Scavenger, or Basic light variant; low role specificity |
| `Pirate_Common_Candidates_32x32_14` | `-6386932502878901857` | `(192,192,32,32)` | `20x26` | Pointed diamond/ram shape | Reinforced angular nose and central prong | None | Strong Melee Charger candidate; compact enough for a fair circular collider |
| `Pirate_Common_Candidates_32x32_15` | `-1582722853777427958` | `(224,192,32,32)` | `28x26` | Very wide delta wing | Long center spine and large swept side plates | None | Heavy Shotgun or interceptor; near-full width needs unique collider/scale |
| `Pirate_Common_Candidates_32x32_16` | `6469542082490743866` | `(0,160,32,32)` | `20x28` | Rounded compact fighter | Side red pods and centerline tail | None | Basic or Defender; visually heavier than `_33`/`_42` |
| `Pirate_Common_Candidates_32x32_17` | `-8224561071734617339` | `(32,160,32,32)` | `15x27` | Narrow asymmetric craft | Tall external rail/antenna and exposed side module | None | Support, Charging, or utility; not safe with centered visual assumptions |
| `Pirate_Common_Candidates_32x32_18` | `-8742611057933122236` | `(64,160,32,32)` | `14x24` | Short narrow rectangle | Simple armor blocks, no obvious weapon | None | Scout, Scavenger, or Basic light variant |
| `Pirate_Common_Candidates_32x32_19` | `2147135688940533274` | `(96,160,32,32)` | `14x27` | Narrow open-frame body | Side bay/industrial rail and exposed lower structure | None | Repair/Utility or Harvester; too asymmetric for a generic shooter |
| `Pirate_Common_Candidates_32x32_20` | `8677308254349443937` | `(128,160,32,32)` | `16x25` | Tapered armored pod | Twin upper prongs and dense nose armor | None | Melee alternate or Scout; narrower than `_14` but circle-compatible |
| `Pirate_Common_Candidates_32x32_21` | `-7632526904550156468` | `(160,160,32,32)` | `14x24` | Small rectangular utility craft | Visible red side module/container | None | Strong Scavenger candidate; intentionally smaller visual footprint |
| `Pirate_Common_Candidates_32x32_22` | `6468521811339609121` | `(192,160,32,32)` | `28x23` | Very wide delta | Twin forward prongs and broad red side panels | None | Heavy Gunner or Shotgun; almost full-width and unsuitable for narrow shared collider |
| `Pirate_Common_Candidates_32x32_23` | `6602421033111634022` | `(224,160,32,32)` | `22x27` | Long T-shaped arms | Broad lateral hardware and centerline tail | None | Support or Shotgun alternate; weapon role not visually explicit |
| `Pirate_Common_Candidates_32x32_24` | `-1046447778185429886` | `(0,128,32,32)` | `22x24` | Spear/delta profile | Prominent twin forward prongs and broad rear wing | None | Charging artillery or Melee; role choice must define whether prongs are guns or ram |
| `Pirate_Common_Candidates_32x32_25` | `6846191805412555949` | `(32,128,32,32)` | `14x29` | Narrow armored slab | Red crossed lower markings; no obvious muzzle | None | Scout or Support; very narrow for common collision set |
| `Pirate_Common_Candidates_32x32_26` | `-5889695571600565518` | `(64,128,32,32)` | `16x27` | Compact angular hull | Side rails and small red panel | None | Basic, Scout, or Melee alternate; moderate asymmetry |
| `Pirate_Common_Candidates_32x32_27` | `-1628728163457478148` | `(96,128,32,32)` | `18x27` | Rounded pointed shoulders | Compact side armor, centerline front | None | Basic or Defender light variant |
| `Pirate_Common_Candidates_32x32_28` | `3687784283384683836` | `(128,128,32,32)` | `26x23` | Wide delta | Red wing tips and centered spine | None | Shotgun or interceptor; needs broad collider |
| `Pirate_Common_Candidates_32x32_29` | `-8193855011809190732` | `(160,128,32,32)` | `24x25` | Broad triangular craft | Dark lower bay/launcher and red side blocks | None | Mine Layer or Charging; lower equipment is visually prominent |
| `Pirate_Common_Candidates_32x32_30` | `13033776381460890` | `(192,128,32,32)` | `20x23` | Broad square/bunker silhouette | Twin upper prongs and heavy frontal plate | None | Strong Defender candidate; compact shared-circle footprint |
| `Pirate_Common_Candidates_32x32_31` | `-1044797692990028519` | `(224,128,32,32)` | `22x27` | Wide T-bar frame | Long lateral structure and center body | None | Support or Heavy Basic; broad top may compete with Shotgun readability |
| `Pirate_Common_Candidates_32x32_32` | `3568678480461964920` | `(0,96,32,32)` | `24x26` | Delta/spear fighter | Tall twin forward rails and broad rear wing | None | Charging or interceptor; visually similar family to `_24` |
| `Pirate_Common_Candidates_32x32_33` | `1191463379897425728` | `(32,96,32,32)` | `18x27` | Generic compact fuselage | Short shoulders, central canopy, no oversized equipment | None | Strong Basic Patrol candidate; smaller width than current Basic art |
| `Pirate_Common_Candidates_32x32_34` | `-1215433353091641725` | `(64,96,32,32)` | `20x29` | T-shaped shooter | Red side pods and long centerline tail | None | Basic or Shotgun alternate; clear centered front |
| `Pirate_Common_Candidates_32x32_35` | `-1803458088092104641` | `(96,96,32,32)` | `18x27` | Narrow armored frame | Side rails and layered center armor | None | Defender light or Basic variant; denser than `_33` |
| `Pirate_Common_Candidates_32x32_36` | `5386196994251432748` | `(128,96,32,32)` | `28x24` | Wide delta artillery chassis | Dense lower machinery and twin upper rails | None | Charging alternate or Heavy Gunner; full width needs dedicated collider |
| `Pirate_Common_Candidates_32x32_37` | `-5440076151862804825` | `(160,96,32,32)` | `26x25` | Wide delta with exposed center | Large dark coil/cannon-like center module and twin upper rails | None | Strong Charging Artillery candidate; broad but readable |
| `Pirate_Common_Candidates_32x32_38` | `2673646472935156003` | `(192,96,32,32)` | `26x24` | Wide delta twin-pod frame | Two dark launcher/energy pods and upper rails | None | Charging alternate or Heavy Gunner; same family as `_36`/`_37` |
| `Pirate_Common_Candidates_32x32_39` | `2619476614394048218` | `(224,96,32,32)` | `22x27` | Complex asymmetric open frame | Exposed pipe/coil and side machinery | None | Repair/Utility or Harvester; intentionally poor generic-combat readability |
| `Pirate_Common_Candidates_32x32_40` | `5441008558729017624` | `(0,64,32,32)` | `20x24` | Boxy asymmetric hull | Dark cargo/service panel and red side plate | None | Harvester or Defender utility variant; asymmetrical center of mass |
| `Pirate_Common_Candidates_32x32_41` | `-8297229540534922217` | `(32,64,32,32)` | `18x27` | Slim exposed-mechanism craft | Visible internal lattice and red service panel | None | Repair/Utility or Scavenger; detail may be noisy at gameplay scale |
| `Pirate_Common_Candidates_32x32_42` | `-2990729717544800652` | `(64,64,32,32)` | `18x26` | Simple medium T hull | Small side pods and one red band | None | Strong Basic alternate; nearly identical structural envelope to `_33` |
| `Pirate_Common_Candidates_32x32_43` | `6837433013111796361` | `(96,64,32,32)` | `22x25` | Rounded lower pods with narrow nose | Broad lower armor/engine structure | None | Support or Defender; rear-heavy silhouette is not an obvious breacher |
| `Pirate_Common_Candidates_32x32_44` | `-8884274876834411387` | `(128,64,32,32)` | `19x23` | Broad armored box | Short side arms and stable rectangular field | None | Strong Defender alternate; close bounds match `_30` |
| `Pirate_Common_Candidates_32x32_45` | `-330272513575672367` | `(160,64,32,32)` | `22x27` | Medium T hull | Side pods and long centerline spar | None | Basic heavy or Support; wider than `_33`/`_42` variant group |
| `Pirate_Common_Candidates_32x32_46` | `-6516941366412215531` | `(192,64,32,32)` | `22x26` | Broad T hull | Red checker/port pattern and lower center module | None | Shotgun or Heavy Basic; readable breadth but not an explicit barrel |
| `Pirate_Common_Candidates_32x32_47` | `4412107620276805825` | `(224,64,32,32)` | `22x26` | Medium T hull | Vertical red service stripe and centerline front | None | Support or Basic heavy; very close family to `_45`/`_46` |
| `Pirate_Common_Candidates_32x32_48` | `8790213808239245158` | `(0,32,32,32)` | `24x26` | Delta with asymmetric paneling | Vent/grille and red armor on one wing | None | Heavy Gunner or utility fighter; asymmetric equipment location |
| `Pirate_Common_Candidates_32x32_49` | `-3463518787694270701` | `(32,32,32,32)` | `20x26` | Tall armored shell | Dense lower vents and enclosed nose | None | Defender or Heavy Basic; little visible weapon identity |
| `Pirate_Common_Candidates_32x32_50` | `7828895019165745527` | `(64,32,32,32)` | `20x27` | Tall heavy chassis | Prominent red upper shoulder blocks | None | Heavy Gunner or Defender; stronger visual weight than common Basic |
| `Pirate_Common_Candidates_32x32_51` | `-8178609627847051447` | `(96,32,32,32)` | `26x25` | Wide triangular/box hull | Large dark lower bay or intake | None | Shotgun heavy or Mine Layer; broad bay dominates silhouette |
| `Pirate_Common_Candidates_32x32_52` | `4340481523353477287` | `(128,32,32,32)` | `26x27` | Wide industrial asymmetric chassis | External pipe/collection rig and red service area | None | Strong Rival Harvester candidate; needs broad collider and role-specific prefab |
| `Pirate_Common_Candidates_32x32_53` | `-4144349639160851158` | `(160,32,32,32)` | `20x26` | Tall narrow service hull | Exposed lower rails/bay | None | Repair/Utility or Harvester alternate; centered fire point remains possible |
| `Pirate_Common_Candidates_32x32_54` | `5219349963463462912` | `(192,32,32,32)` | `22x26` | Tall armored slab | Strong red cross-band and dense lower armor | None | Defender or Heavy Gunner; heavy role language |
| `Pirate_Common_Candidates_32x32_55` | `-1879086260224785238` | `(224,32,32,32)` | `22x27` | Solid tall block | Large uninterrupted armor plates | None | Defender; lacks distinct weapon or utility equipment |
| `Pirate_Common_Candidates_32x32_56` | `4014584480848265015` | `(0,0,32,32)` | `26x20` | Low wide wedge | Sharp nose and red edge plates | None | Melee alternate or Scout interceptor; not collider-compatible with tall `_14` group |
| `Pirate_Common_Candidates_32x32_57` | `7148189173715102411` | `(32,0,32,32)` | `20x28` | Tall frame with outer hardpoints | Rectangular side rails and reinforced center | None | Heavy Gunner or Support; strong hardpoint language |
| `Pirate_Common_Candidates_32x32_58` | `2878140899087840839` | `(64,0,32,32)` | `21x22` | Compact asymmetric box craft | Large square side cargo/service pod | None | Harvester alternate or Repair/Utility; too small for `_52` shared collider without metadata |
| `Pirate_Common_Candidates_32x32_59` | `8051829816514960722` | `(96,0,32,32)` | `22x26` | Broad T hull | Long side arms and a lower red panel | None | Support or Basic heavy; centered front is clear |
| `Pirate_Common_Candidates_32x32_60` | `-3539656150702143059` | `(128,0,32,32)` | `28x20` | Low very wide delta | Textured/pixelated wing panel and clustered underside | None | Mine Layer or Scout interceptor; requires unique wide collider |
| `Pirate_Common_Candidates_32x32_61` | `7256956107871223586` | `(160,0,32,32)` | `24x28` | Swept broad frame | Clustered lower pods and pointed center | None | Mine Layer, Shotgun heavy, or Heavy Gunner |
| `Pirate_Common_Candidates_32x32_62` | `8845221023954451977` | `(192,0,32,32)` | `28x25` | Very wide heavy platform | Red bracket/frame around central machinery | None | Heavy Gunner or industrial support; visually near elite weight despite 32x32 canvas |
| `Pirate_Common_Candidates_32x32_63` | `3733838398414488650` | `(224,0,32,32)` | `24x27` | Wide triangular utility chassis | Exposed lower machinery/service bay | None | Rival Harvester alternate or Repair/Utility; close enough to `_52` for a controlled variant pair |

## Proposed primary role mapping

All proposed primaries can remain at SpriteRenderer local position `(0,0,0)` and local rotation `0` because their effective pivots and +Y authored direction match the current common-enemy convention. Any footprint compensation should be visual-only; changing the current physics/root transform scale would also scale colliders and FirePoints. The later implementation should either introduce a focused visual child or retune the entire prefab deliberately.

| Production role | Primary candidate | Alternates | Confidence | Rationale | Required prefab work in the later implementation pass |
|---|---|---|---|---|---|
| Basic Patrol Shooter | `_33` / fileID `1191463379897425728` | `_42` | High | Generic centered silhouette with no oversized gun, armor, or utility rig; reads as the common baseline | Keep +Y rotation convention. Because `_33` is narrower than current 24x30 Basic art, use visual-only scale around `1.25-1.33 X / 1.05-1.10 Y` if matching the old footprint; do not scale the physics root. Move center FirePoint from `Y 0.567` toward the actual nose (about `0.46-0.49`) and replace/review the legacy trigger polygon |
| Shotgun Breacher | `_6` / fileID `-7918928145898029670` | `_4`, `_12` | Medium | Broad, reinforced prow/shoulders clearly separate it from Basic while keeping a centered firing axis | Existing `Y 0.38` FirePoint is close to the visible nose. Retune the trigger polygon/circle for the 24x26 silhouette; only a small visual-width adjustment should be needed |
| Charging Artillery | `_37` / fileID `-5440076151862804825` | `_36`, `_38` | High | Exposed central coil/cannon-like module, twin forward rails, and broad artillery chassis communicate charge/release behavior | Keep centered FirePoint near `Y 0.4`. Use a shared broad artillery collider for `_36/_37/_38`; preserve visual-only recoil and avoid retaining the old narrow trigger polygon |
| Melee Charger | `_14` / fileID `-6386932502878901857` | `_20`; `_56` only as an unsafe alternate | Medium | Pointed diamond shape and reinforced nose read as a ram without looking like artillery | A production prefab is currently missing. Build from current common-enemy components, omit/disable ranged FirePoint use, use a fair compact circle/capsule, and keep the visual centered. `_56` needs separate wide-profile collision metadata |
| Defender | `_30` / fileID `13033776381460890` | `_44` | High | Bunker-like rectangular armor and twin spars read as static protection without reaching 64x64 Elite prominence | Defender currently reuses Basic/Shotgun prefabs. Apply a role-specific visual override only after role configuration; `_30` suits Defender Basic and `_44` can distinguish Defender Shotgun. Use one shared compact defender collider envelope and preserve each chassis' attack definition |
| Rival Harvester | `_52` / fileID `4340481523353477287` | `_63`; `_58` as a metadata-required alternate | High | External pipe/collection rig and asymmetric industrial bay clearly separate it from combat-only ships | A production prefab is currently missing. Use a dedicated presentation prefab/configured common chassis, broad centered collider, visual-only root, and centered nose FirePoint around `Y 0.42-0.45`. `_58` is too small for blind interchangeability |
| Scavenger | `_21` / fileID `-7632526904550156468` | `_5`, `_13` | High | Small/light footprint plus visible side container communicates opportunistic collection and speed | A production prefab is currently missing. A modest visual scale increase may aid 480x270 readability. `_5` shares the exact 14x24 bounds; use a small centered collider and FirePoint around `Y 0.36-0.39` |

## Variant-group feasibility

No variant-selection component should be added until the primary mapping is approved.

| Role | Proposed group | Bounds comparison | Safe with one collider / FirePoint? | Notes |
|---|---|---|---|---|
| Basic | `_33`, `_42` | `18x27`, `18x26`; centered | Yes | Same +Y direction, near-identical width/pivot, centered nose, no animation dependency. Use one visual scale and centered FirePoint |
| Shotgun | `_4`, `_6`, `_12` | `22x27`, `24x26`, `22x26`; centered | Yes, after one broad collider retune | Same rounded/broad chassis family and centered visible nose. Avoid sprite-generated per-variant polygon assumptions |
| Charging | `_36`, `_37`, `_38` | `28x24`, `26x25`, `26x24`; centered | Yes, after one broad collider retune | Strong shared delta-artillery family, consistent top-center muzzle region, no Animator dependency |
| Melee Charger | `_14`, `_20` | `20x26`, `16x25`; centered | Conditionally yes | A conservative circle/capsule can cover both. `_56` (`26x20`) is not safe in this group without per-variant collision metadata |
| Defender | `_30`, `_44` | `20x23`, `19x23`; centered | Yes | Very similar compact armored envelopes. Role-specific sprite override must not overwrite Basic/Shotgun attack ownership |
| Rival Harvester | `_52`, `_63` | `26x27`, `24x27`; centered | Yes, after one industrial collider retune | Similar large utility chassis and centered forward axis. `_58` (`21x22`) should remain separate or receive variant metadata |
| Scavenger | `_5`, `_21` | Both `14x24`; centered | Yes | Exact size match, same +Y direction, similar small box chassis, no animation dependency |

## Future-role reserves

These are visual reserves only; no gameplay is proposed or implemented.

| Future role | Candidates | Reason |
|---|---|---|
| Scout | `_10`, `_13`, `_18`, `_25` | Narrow/light silhouettes with minimal armor |
| Support | `_3`, `_23`, `_31`, `_59` | Broad lateral arms/hardpoints without a dominant forward weapon |
| Heavy Gunner | `_50`, `_57`, `_62` | Heavy armor, hardpoint frames, or platform-like mass |
| Mine Layer | `_29`, `_60`, `_61` | Broad rear bays/pods and wide deployment-oriented silhouettes |
| Repair/Utility | `_19`, `_39`, `_41`, `_53`, `_58` | Exposed rails, pipes, service frames, or cargo modules |

Candidates `_0`, `_1`, `_2`, `_7`, `_8`, `_9`, `_11`, `_15`, `_16`, `_17`, `_22`, `_24`, `_26`, `_27`, `_28`, `_32`, `_34`, `_35`, `_40`, `_43`, `_45`, `_46`, `_47`, `_48`, `_49`, `_51`, `_54`, and `_55` should remain unassigned until the primary seven-role mapping is approved. Preserving a reserve is preferable to forcing every silhouette into production.

## Current-versus-proposed mapping

| Gameplay role | Current definition | Current prefab | Current sprite | Proposed candidate | Alternate candidates | Required adjustments |
|---|---|---|---|---|---|---|
| Basic Patrol Shooter | `Assets/02_Scripts/Config/EnemyDefinition/Basic_enemy.asset` | `Assets/03_Prefabs/Enemy/Enemy_Basic.prefab` | `enemy_basic.png` (`b8afc480...`, fileID `21300000`) | `_33` (`1191463379897425728`) | `_42` | Visual-only scale compensation, lower FirePoint from current `0.567`, collider/trigger review |
| Shotgun Breacher | `Assets/02_Scripts/Config/EnemyDefinition/Shotgun_enemy.asset` | `Assets/03_Prefabs/Enemy/Enemy_Shotgun.prefab` | `enemy_shotgun.png` (`13df92a0...`, fileID `21300000`) | `_6` (`-7918928145898029670`) | `_4`, `_12` | Keep near-current FirePoint; broad collider/trigger review |
| Charging Artillery | `Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset` | `Assets/03_Prefabs/Enemy/Enemy_Charge.prefab` | `enemy_charging.png` (`4d772bc3...`, fileID `21300000`) | `_37` (`-5440076151862804825`) | `_36`, `_38` | Broad artillery collider, centered muzzle validation, visual-only scale review |
| Melee Charger | `Assets/02_Scripts/Config/EnemyDefinition/Melee_Charger.asset` | None | None | `_14` (`-6386932502878901857`) | `_20`; `_56` unsafe | Create/assign production prefab in later pass; no ranged muzzle requirement; fair compact collision |
| Defender Basic / Shotgun | Runtime role applied to `Basic_enemy` / `Shotgun_enemy` | Reuses Basic / Shotgun prefabs | Same as underlying combat chassis | `_30` (`13033776381460890`) | `_44` | Add role-aware visual override only; retain underlying Basic/Shotgun attack data and use compact shared defender collision |
| Rival Harvester | `Assets/02_Scripts/Config/EnemyDefinition/Rival_Harvester.asset` | None | None | `_52` (`4340481523353477287`) | `_63`; `_58` unsafe | Create/assign utility presentation prefab; broad collision, centered muzzle, preserve role/cargo controller ownership |
| Scavenger | `Assets/02_Scripts/Config/EnemyDefinition/Scavenger.asset` | None | None | `_21` (`-7632526904550156468`) | `_5`, `_13` | Create/assign light presentation prefab; small collider and modest visual-scale readability check |

## Approval decisions for the next pass

1. Approve or replace the seven primary candidates: Basic `_33`, Shotgun `_6`, Charging `_37`, Melee `_14`, Defender `_30`, Rival Harvester `_52`, Scavenger `_21`.
2. Decide whether Defender Basic and Defender Shotgun should share `_30`, or use `_30` and `_44` respectively.
3. Decide whether the first application pass should use only primaries or also introduce a later variant-selection component for the safe groups.
4. Confirm whether the currently null Melee/Rival/Scavenger prefab assignments should be completed in the sprite-application pass or kept as a separate production-wiring pass.
